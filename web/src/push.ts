/**
 * Web Push ("Match found", "Your turn", …) while the app is in the background: the PC app holds the subscriptions and
 * pushes, `public/sw.js` shows them; on iOS only in the home-screen app. Visibility and per-phone preferences go to
 * the PC app so it skips pushes the phone doesn't want.
 */
import { reactive, ref, type Ref } from 'vue'
import { defaultPushPreferences, type PushPreferences } from './contracts'
import { isRecord, KEYS, readJson, writeJson } from './lib/storage'
import { within } from './lib/timeout'
import {
  getPushPublicKey,
  onAuthenticated,
  registerPushSubscription,
  sendTestPush,
  setPushForeground,
  setPushPreferences,
  unregisterPushSubscription,
} from './hub'
import { store, toast } from './store'

export type PushStatus = 'unsupported' | 'needs-install' | 'default' | 'denied' | 'enabled'

/**
 * Dev only (`?demo=settings`): a faked push status, so the notification settings can be reviewed without a service
 * worker or PC. Null = real push.
 */
export const devPush: { status: PushStatus | null } = { status: null }
const faked = () => import.meta.env.DEV && devPush.status !== null

/** Reactive push state for the UI. `needs-install` = iOS Safari outside the installed (standalone) app. */
export const pushStatus: Ref<PushStatus> = ref(initialStatus())

/** Stored preferences over the defaults; unknown or malformed fields keep their default. */
function readPreferences(): PushPreferences {
  const prefs = { ...defaultPushPreferences }
  const raw = readJson(KEYS.pushPreferences, (r) => (isRecord(r) ? r : null))
  for (const key of Object.keys(prefs) as (keyof PushPreferences)[]) {
    const value = raw?.[key]
    if (typeof value === 'boolean') prefs[key] = value
  }
  return prefs
}

/** Which events push to this phone (Settings › Notifications). */
export const pushPreferences: PushPreferences = reactive(readPreferences())

/** Turn one event on or off for this phone: saved here and sent to the PC, which enforces it. */
export async function setPushPreference(key: keyof PushPreferences, on: boolean): Promise<void> {
  pushPreferences[key] = on
  writeJson(KEYS.pushPreferences, pushPreferences) // storage unavailable: in memory only, still sent to the PC
  await syncPreferences(true)
}

/** Send this phone's preferences to the PC. Quiet after a registration (resent on the next one anyway). */
async function syncPreferences(loud: boolean): Promise<void> {
  const endpoint = registeredEndpoint
  if (faked() || !endpoint || !store.authenticated) return
  try {
    await setPushPreferences(endpoint, { ...pushPreferences })
  } catch {
    if (loud) toast('Could not update notifications. Try again.')
  }
}

/** Ask the PC for a test notification to this phone. Resolves true when the push service took it. */
export async function sendTestNotification(): Promise<boolean> {
  if (faked()) {
    toast('Test notification sent', 'success')
    return true
  }
  const endpoint = registeredEndpoint
  if (!endpoint || !store.authenticated) {
    toast(store.authenticated ? 'Turn notifications on first.' : 'Offline', 'warning')
    return false
  }
  try {
    const sent = await sendTestPush(endpoint)
    toast(sent ? 'Test notification sent' : 'No answer. Try again.', sent ? 'success' : 'error')
    return sent
  } catch (e) {
    toast(e instanceof Error ? e.message : String(e))
    return false
  }
}

function isIos(): boolean {
  if (typeof navigator === 'undefined') return false
  // iPadOS 13+ reports itself as a Mac; touch points give it away.
  return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
}

function isStandalone(): boolean {
  return (
    (typeof matchMedia === 'function' && matchMedia('(display-mode: standalone)').matches) ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  )
}

function hasPushApis(): boolean {
  return (
    typeof window !== 'undefined' &&
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    'Notification' in window &&
    window.isSecureContext
  )
}

/** True when this browser can receive Web Push here (on iOS: only inside the installed home-screen app). */
function pushSupported(): boolean {
  return hasPushApis() && (!isIos() || isStandalone())
}

function initialStatus(): PushStatus {
  if (isIos() && !isStandalone()) return 'needs-install'
  if (!hasPushApis()) return 'unsupported'
  if (Notification.permission === 'denied') return 'denied'
  return 'default' // 'enabled' is only known once the subscription is checked (refreshPushStatus)
}

function base64UrlToBytes(value: string): Uint8Array<ArrayBuffer> {
  const base64 = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(base64)
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i)
  return bytes
}

function sameKey(subscription: PushSubscription, key: Uint8Array): boolean {
  const current = subscription.options?.applicationServerKey
  if (!current) return true // not exposed by this browser: assume it matches
  const a = new Uint8Array(current)
  return a.length === key.length && a.every((b, i) => b === key[i])
}

/** The browser's push calls talk to its push service and can stall; a stuck one must not hold the switch busy. */
const BROWSER_PUSH_TIMEOUT_MS = 15_000

/** `promise`, or a rejection once `ms` have passed. */
async function bounded<T>(promise: Promise<T>, ms = BROWSER_PUSH_TIMEOUT_MS): Promise<T> {
  const answer = await within(
    promise.then((value) => ({ value })),
    ms,
    null,
  )
  if (!answer) throw new Error('The browser took too long. Try again.')
  return answer.value
}

async function registration(): Promise<ServiceWorkerRegistration | null> {
  if (!('serviceWorker' in navigator)) return null
  // Bounded: navigator.serviceWorker.ready never settles when no worker is registered (e.g. `vite dev`).
  return within(navigator.serviceWorker.ready, 5000, null)
}

async function currentSubscription(): Promise<PushSubscription | null> {
  const reg = await registration()
  return reg ? bounded(reg.pushManager.getSubscription()) : null
}

/**
 * Subscribes with the PC's current key (re-subscribing when the key changed, e.g. another PC) and stores the
 * subscription on the PC.
 */
async function subscribeAndRegister(reg: ServiceWorkerRegistration): Promise<void> {
  const key = base64UrlToBytes(await getPushPublicKey())
  let subscription = await bounded(reg.pushManager.getSubscription())
  if (subscription && !sameKey(subscription, key)) {
    await bounded(subscription.unsubscribe())
    subscription = null
  }
  subscription ??= await bounded(reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key }))
  await register(subscription)
}

async function register(subscription: PushSubscription): Promise<void> {
  const json = subscription.toJSON()
  const p256dh = json.keys?.p256dh
  const auth = json.keys?.auth
  if (!json.endpoint || !p256dh || !auth) throw new Error('The browser returned an incomplete push subscription.')
  await registerPushSubscription(json.endpoint, p256dh, auth)
  registeredEndpoint = json.endpoint
  reportForeground()
  await syncPreferences(false)
}

/** This browser's subscription as last registered with the PC; null while push is off. */
let registeredEndpoint: string | null = null

/** The PC forgets a foreground report after 75 s (HubMethods.Server.SetPushForeground), so repeat it while visible. */
const FOREGROUND_HEARTBEAT_MS = 30_000

/** Tell the PC whether the app is visible: no pushes reach this phone while the user is looking at it. */
function reportForeground(): void {
  if (!registeredEndpoint || !store.authenticated) return
  // Best effort: on failure the PC falls back to pushing (connection closed or report expired).
  setPushForeground(registeredEndpoint, document.visibilityState === 'visible').catch(() => undefined)
}

/**
 * Turn notifications on. Call it directly from a user gesture (tap): iOS only shows the permission prompt then.
 * Resolves true when this phone is subscribed and registered with the PC.
 */
export async function enablePush(): Promise<boolean> {
  if (faked()) {
    pushStatus.value = devPush.status = 'enabled'
    return true
  }
  if (!pushSupported()) {
    await refreshPushStatus()
    return false
  }
  // First thing, still inside the gesture.
  const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
  if (permission !== 'granted') {
    pushStatus.value = permission === 'denied' ? 'denied' : 'default'
    return false
  }
  try {
    const reg = await registration()
    if (!reg) throw new Error('The app is not installed yet (no service worker). Reload and try again.')
    await subscribeAndRegister(reg)
    pushStatus.value = 'enabled'
    return true
  } catch (e) {
    toast(`Could not turn on notifications: ${e instanceof Error ? e.message : String(e)}`)
    await refreshPushStatus()
    return false
  }
}

/**
 * Before unpairing: take this phone's subscription off the pairing on the PC, or it keeps getting that PC's pushes
 * (and, after pairing with another PC, both). The browser subscription stays; a new pairing registers it again.
 */
export async function forgetPushPairing(): Promise<void> {
  const endpoint = registeredEndpoint
  registeredEndpoint = null
  if (endpoint && store.authenticated) await unregisterPushSubscription(endpoint).catch(() => undefined)
}

/** Turn notifications off for this phone (unsubscribes the browser and tells the PC). */
export async function disablePush(): Promise<void> {
  if (faked()) {
    pushStatus.value = devPush.status = 'default'
    return
  }
  try {
    const subscription = await currentSubscription()
    registeredEndpoint = null
    if (subscription) {
      const endpoint = subscription.endpoint
      await bounded(subscription.unsubscribe())
      if (store.authenticated) await unregisterPushSubscription(endpoint).catch(() => undefined)
    }
  } catch {
    /* a stalled browser: refreshPushStatus shows what is left */
  } finally {
    await refreshPushStatus()
  }
}

let running: Promise<void> | null = null
let rerun = false

/**
 * Re-reads the permission and subscription state. When enabled and signed in, registers the existing subscription
 * again (so it survives a PC app restart or a new pairing). Safe to call often (startup, after auth);
 * a call while one is running makes it run once more afterwards.
 */
export function refreshPushStatus(): Promise<void> {
  if (running) {
    rerun = true
    return running
  }
  running = (async () => {
    try {
      do {
        rerun = false
        await refreshOnce()
      } while (rerun)
    } finally {
      running = null
    }
  })()
  return running
}

async function refreshOnce(): Promise<void> {
  if (faked()) {
    pushStatus.value = devPush.status!
    return
  }
  if (isIos() && !isStandalone()) {
    pushStatus.value = 'needs-install'
    return
  }
  if (!hasPushApis()) {
    pushStatus.value = 'unsupported'
    return
  }
  if (Notification.permission === 'denied') {
    pushStatus.value = 'denied'
    return
  }
  const reg = await registration()
  const subscription = reg ? await bounded(reg.pushManager.getSubscription()).catch(() => null) : null
  if (Notification.permission !== 'granted' || !reg || !subscription) {
    pushStatus.value = 'default'
    registeredEndpoint = null
    return
  }
  pushStatus.value = 'enabled'
  if (store.authenticated) {
    try {
      await subscribeAndRegister(reg)
    } catch {
      /* PC unreachable or rejected it: try again after the next authentication */
    }
  }
}

/**
 * The app is open: its notifications ("Match found", …) are stale, so close them and clear the app icon badge
 * (public/sw.js sets it). Also re-reads the permission, which may have been changed in Settings meanwhile.
 */
async function onAppVisible(resumed: boolean): Promise<void> {
  if (resumed) void refreshPushStatus() // at startup App.vue refreshes it
  try {
    const reg = hasPushApis() ? await registration() : null
    for (const n of (await reg?.getNotifications()) ?? []) n.close()
    await (navigator as Navigator & { clearAppBadge?: () => Promise<void> }).clearAppBadge?.()
  } catch {
    /* badging / notifications unavailable here */
  }
}

/** Keep the PC's view of this phone current: after every sign-in, on visibility changes, and while visible (main.ts). */
export function installPushLifecycle() {
  // The PC app may have restarted, or this phone joined another pairing.
  onAuthenticated(() => void refreshPushStatus())
  document.addEventListener('visibilitychange', () => {
    reportForeground()
    if (document.visibilityState === 'visible') void onAppVisible(true)
  })
  setInterval(() => {
    if (document.visibilityState === 'visible') reportForeground()
  }, FOREGROUND_HEARTBEAT_MS)
  if (document.visibilityState === 'visible') void onAppVisible(false)
}
