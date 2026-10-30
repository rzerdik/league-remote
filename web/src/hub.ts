import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
} from '@microsoft/signalr'
import {
  HubMethods,
  validateCommand,
  type CommandResult,
  type PairingGrant,
  type PushPreferences,
  type RemoteCommand,
} from './contracts'
import { isDemo } from './lib/demoMode'
import { within } from './lib/timeout'
import { loadGrant, saveGrant, setSlice, SLICES, store, toast, type Slices, type StateSlice } from './store'

/** Same origin: the PC app serves this page and the hub. */
const hubUrl = HubMethods.path

/** The hub message that carries each slice. */
const SLICE_MESSAGES: Record<StateSlice, string> = {
  state: HubMethods.client.stateUpdated,
  chat: HubMethods.client.chatUpdated,
  friends: HubMethods.client.friendsUpdated,
  profile: HubMethods.client.profileUpdated,
  liveGame: HubMethods.client.liveGameUpdated,
  autoPick: HubMethods.client.autoPickUpdated,
}

/** Retry forever with capped backoff (the default policy gives up after 4 attempts). */
const retryPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (ctx) => Math.min(15000, [0, 1000, 2000, 5000, 10000][ctx.previousRetryCount] ?? 15000),
}

let connection: HubConnection | null = null
let restartTimer: ReturnType<typeof setTimeout> | undefined
let restartAttempt = 0

/** Strip SignalR's "An unexpected error occurred invoking 'X' on the server. HubException: " prefix. */
function errorText(e: unknown): string {
  const msg = e instanceof Error ? e.message : String(e)
  const m = msg.match(/HubException:\s*(.*)$/s)
  if (m && m[1]) return m[1].trim()
  return msg.replace(/^An unexpected error occurred invoking '[^']+' on the server\.\s*/, '').trim() || 'Unknown error'
}

function build(): HubConnection {
  const c = new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect(retryPolicy)
    .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
    .build()

  for (const slice of SLICES) c.on(SLICE_MESSAGES[slice], (value: Slices[typeof slice]) => setSlice(slice, value))

  c.onreconnecting(() => {
    store.connection = 'reconnecting'
    store.authenticated = false
  })
  c.onreconnected(() => {
    store.connection = 'connected'
    void authenticate().then((r) => {
      if (r === 'failed') restartAfterAuthFailure(c)
    })
  })
  c.onclose(() => {
    store.connection = 'disconnected'
    store.authenticated = false
    scheduleRestart()
  })
  return c
}

function scheduleRestart() {
  clearTimeout(restartTimer)
  const delay = Math.min(15000, 1000 * 2 ** restartAttempt++)
  restartTimer = setTimeout(() => void start(), delay)
}

let startPromise: Promise<void> | null = null

/** Start (or restart) the connection; authenticates automatically when a grant is stored. Never in a demo. */
export function start(): Promise<void> {
  if (isDemo) return Promise.resolve()
  clearTimeout(restartTimer)
  connection ??= build()
  if (startPromise) return startPromise // a start is already in flight: share it
  if (connection.state !== HubConnectionState.Disconnected) return Promise.resolve()
  const c = connection
  startPromise = (async () => {
    store.connection = 'connecting'
    try {
      await c.start()
      store.connection = 'connected'
      const r = await authenticate()
      if (r === 'failed') restartAfterAuthFailure(c)
      else restartAttempt = 0 // keep backing off while authentication keeps failing
    } catch {
      store.connection = 'disconnected'
      scheduleRestart()
    } finally {
      startPromise = null
    }
  })()
  return startPromise
}

/** How long "Pairing…" may take in total: connecting plus redeeming the code. */
const PAIRING_TIMEOUT_MS = 10_000

/** Wait (bounded) until the hub connection is up, starting it if needed. */
async function ensureConnected(timeoutMs: number): Promise<boolean> {
  const deadline = Date.now() + timeoutMs
  for (;;) {
    if (connection?.state === HubConnectionState.Connected) return true
    if (!connection || connection.state === HubConnectionState.Disconnected) {
      restartAttempt = 0
      // SignalR's start() has no timeout of its own: an unanswered negotiate would outlast the deadline.
      await within(start(), deadline - Date.now(), undefined)
    }
    if (Date.now() >= deadline) return false
    await new Promise((r) => setTimeout(r, 300))
  }
}

/** The PC's exact message when a token is unknown or revoked (PhoneHub.Authenticate). */
const TOKEN_REJECTED = /unknown or revoked token/i

type AuthResult = 'ok' | 'skipped' | 'rejected' | 'failed'

/** How long a sign-in waits for the RefreshState answer before it stops showing slices as on their way. */
const SYNC_TIMEOUT_MS = 12_000
/** After the answer: the updates RefreshState triggers travel as their own messages and may trail it a little. */
const SYNC_GRACE_MS = 1_500
/** Counts sign-ins, so an older sign-in's wait can't end a newer one's. */
let syncEpoch = 0

function nullSlices() {
  for (const s of SLICES) (store as Record<StateSlice, unknown>)[s] = null
}

/**
 * Every sign-in starts from nothing, so state from before a dropped connection (a ready check long answered) is never
 * shown as current: the slices are cleared and marked `store.syncing` until the replay, an update or the bounded
 * RefreshState fills each back in; whatever is still missing then shows its usual empty state.
 */
function beginSync(): number {
  nullSlices()
  for (const s of SLICES) store.syncing[s] = true
  return ++syncEpoch
}

function endSync(epoch = syncEpoch) {
  if (epoch !== syncEpoch) return
  for (const s of SLICES) store.syncing[s] = false
}

/** Forget every slice (unpaired, another pairing): there is nothing on its way either. */
function clearSlices() {
  nullSlices()
  endSync()
}

/** Signed in: ask the connector for a fresh snapshot of every slice, bounded. */
function signedIn(epoch: number) {
  store.authenticated = true
  void (async () => {
    const r = await sendCommand({ type: 'RefreshState' }, { silent: true, timeoutMs: SYNC_TIMEOUT_MS })
    if (r?.success) await new Promise((resolve) => setTimeout(resolve, SYNC_GRACE_MS))
    endSync(epoch)
  })()
  notifyAuthenticated()
}

/** Dev only (`?demo=reconnect`, src/dev/demo/): sign in again the way a PC with nothing cached answers. */
export function devSignInWithoutReplay() {
  if (import.meta.env.DEV) signedIn(beginSync())
}

/**
 * Re-authenticate with the stored token. Only an explicit "unknown or revoked token" rejection unpairs; any other
 * failure (transient server error, connection closing, …) keeps the grant so a reconnect can retry.
 *
 * Every tab of the app shares one localStorage grant. A tab that still holds an older token (evicted or revoked after
 * another tab paired again) must not clear or overwrite the newer grant there: when its token is rejected it switches
 * to the stored grant and signs in with that instead (the rejected sign-in left the connection unregistered), and it
 * writes back only over its own copy.
 */
async function authenticate(): Promise<AuthResult> {
  const c = connection
  const grant = store.grant
  if (!c || !grant || c.state !== HubConnectionState.Connected) return 'skipped'
  const epoch = beginSync()
  try {
    const g = await c.invoke<PairingGrant>(HubMethods.server.authenticate, grant.token)
    if (store.grant?.token !== grant.token) {
      endSync(epoch)
      return 'skipped' // re-paired or unpaired meanwhile
    }
    if (loadGrant()?.token === grant.token) saveGrant(g)
    else store.grant = g // another tab saved a different grant meanwhile: leave it there
    signedIn(epoch)
    return 'ok'
  } catch (e) {
    endSync(epoch)
    if (TOKEN_REJECTED.test(errorText(e)) && store.grant?.token === grant.token) {
      const stored = loadGrant()
      if (stored && stored.token !== grant.token) {
        // Another tab paired again: use its grant. If that one is rejected too, it is the stored one and gets cleared.
        store.grant = stored
        return authenticate()
      }
      toast('Pairing expired. Pair again.')
      unpair()
      return 'rejected'
    }
    return 'failed'
  }
}

/** Authentication failed for a non-token reason: drop the connection so onclose retries with backoff. */
function restartAfterAuthFailure(c: HubConnection) {
  if (c === connection && c.state === HubConnectionState.Connected) void c.stop()
}

/**
 * Redeem a 6-digit code from the connector. Re-pairing is always allowed: the new grant replaces any stored one
 * (a code from the already-paired PC just adds this phone to the same pairing again).
 */
export async function redeemPairingCode(code: string): Promise<boolean> {
  if (store.pairing) return false
  if (isDemo) {
    toast('No PC in a demo. Pairing works without ?demo=.', 'info')
    return false
  }
  store.pairing = true
  try {
    const deadline = Date.now() + PAIRING_TIMEOUT_MS
    if (!(await ensureConnected(PAIRING_TIMEOUT_MS)) || !connection) {
      toast('Offline. Check your connection.', 'warning')
      return false
    }
    const c = connection
    let grant: PairingGrant | null
    try {
      grant = await within(c.invoke<PairingGrant>(HubMethods.server.redeemPairingCode, code), deadline - Date.now(), null)
    } catch (e) {
      toast(errorText(e))
      return false
    }
    if (!grant) {
      toast(NO_ANSWER)
      return false
    }
    const previous = store.grant
    const switching = !!previous && previous.pairingId !== grant.pairingId
    saveGrant(grant)
    if (switching) {
      // Leave the old pairing's routing group: reconnect fresh; start() authenticates with the new grant.
      store.authenticated = false
      clearSlices()
      await c.stop()
      restartAttempt = 0
      await start() // authenticates with the new grant
    } else {
      const r = await authenticate()
      if (r === 'failed') restartAfterAuthFailure(c)
    }
    if (store.grant?.token !== grant.token) return false // rejected (already toasted and unpaired)
    if (!store.authenticated) {
      toast('Paired. Signing in…', 'success')
      return false
    }
    toast('Paired', 'success')
    return true
  } finally {
    store.pairing = false
  }
}

export function unpair() {
  saveGrant(null)
  store.authenticated = false
  clearSlices()
  // Drop the routing group membership by reconnecting fresh.
  const c = connection
  if (c && c.state === HubConnectionState.Connected) {
    void c.stop() // onclose schedules a fresh, unauthenticated reconnect
  }
}

/** How long Unpair waits for the PC to revoke this phone's token before forgetting it locally anyway. */
const REVOKE_TIMEOUT_MS = 3_000

/**
 * Unpair This Phone: revoke this phone's token on the PC first (`HubMethods.server.unpair`), so a copy of it can't be
 * used again, then forget it locally. Offline or any failure: forget it locally anyway (bounded).
 */
export async function unpairAndRevoke(): Promise<void> {
  const c = connection
  if (c && c.state === HubConnectionState.Connected && store.authenticated) {
    await within(c.invoke<boolean>(HubMethods.server.unpair).catch(() => false), REVOKE_TIMEOUT_MS, false)
  }
  unpair()
}

function newRequestId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

/** Dev-only (`?demo=`, src/dev/demo/): answers some commands locally instead of the PC. Unset in a build. */
export const devCommands: { handle: ((cmd: RemoteCommand) => Promise<CommandResult | null>) | null } = { handle: null }

/**
 * How long a command may take by default. The PC answers every command within 10 s (PhoneHub.CommandTimeout), so this
 * only runs out when the answer is lost on the way (a socket that died without SignalR noticing yet).
 */
const COMMAND_TIMEOUT_MS = 15_000
const NO_ANSWER = "The PC didn't answer. Try again."

export interface SendOptions {
  /** No banner on failure: the caller says it its own way, or it is a background refresh. */
  silent?: boolean
  /** Give up after this long (default 15 s). */
  timeoutMs?: number
}

/**
 * Send a command to the PC. Never hangs: resolves the PC's result (a failed one shows its error), or null when the
 * command is invalid, the phone is offline or signed out, the hub call fails, or no answer came within `timeoutMs`;
 * each of those shows a banner unless `silent`.
 */
export async function sendCommand(cmd: Omit<RemoteCommand, 'requestId'>, opts: SendOptions = {}): Promise<CommandResult | null> {
  const command: RemoteCommand = { requestId: newRequestId(), ...cmd }
  const fail = (text: string, kind: 'warning' | 'error' = 'error') => {
    if (!opts.silent) toast(text, kind)
    return null
  }
  const invalid = validateCommand(command)
  if (invalid) return fail(invalid)
  if (import.meta.env.DEV && devCommands.handle) {
    const r = await devCommands.handle(command)
    if (r) return r
  }
  const c = connection
  if (!c || c.state !== HubConnectionState.Connected || !store.authenticated) return fail('Offline', 'warning')
  let result: CommandResult | null
  try {
    result = await within(c.invoke<CommandResult>(HubMethods.server.sendCommand, command), opts.timeoutMs ?? COMMAND_TIMEOUT_MS, null)
  } catch (e) {
    return fail(errorText(e))
  }
  if (!result) return fail(NO_ANSWER)
  if (!result.success && !opts.silent) toast(result.error || 'Command failed.')
  return result
}

// --- Authentication listeners -------------------------------------------------------------------------------

const authListeners = new Set<() => void>()

/** Calls `cb` after every successful Authenticate (app start, reconnect, re-pair). Returns an unsubscribe function. */
export function onAuthenticated(cb: () => void): () => void {
  authListeners.add(cb)
  return () => authListeners.delete(cb)
}

function notifyAuthenticated() {
  for (const cb of authListeners) {
    try {
      cb()
    } catch {
      /* a listener's failure must not break authentication */
    }
  }
}

// --- Web Push (see push.ts) ----------------------------------------------------------------------------------

/** Push hub calls answer at once; only the test push waits for the push service. */
const PUSH_CALL_TIMEOUT_MS = 10_000
const TEST_PUSH_TIMEOUT_MS = 20_000

/**
 * Invoke a signed-in-only hub method, bounded. Rejects (with a sentence fit for a banner) when not connected and
 * signed in, when the PC refuses, or when it doesn't answer within `timeoutMs`.
 */
async function invokeAuthenticated<T>(timeoutMs: number, method: string, ...args: unknown[]): Promise<T> {
  const c = connection
  if (!c || c.state !== HubConnectionState.Connected || !store.authenticated) throw new Error('Not connected to your PC.')
  let answer: { value: T } | null
  try {
    answer = await within(
      c.invoke<T>(method, ...args).then((value) => ({ value })),
      timeoutMs,
      null,
    )
  } catch (e) {
    throw new Error(errorText(e))
  }
  if (!answer) throw new Error(NO_ANSWER)
  return answer.value
}

/** The PC's VAPID public key (base64url) for `PushManager.subscribe({ applicationServerKey })`. */
export function getPushPublicKey(): Promise<string> {
  return invokeAuthenticated<string>(PUSH_CALL_TIMEOUT_MS, HubMethods.server.getPushPublicKey)
}

/** Store this browser's push subscription for the pairing (idempotent per endpoint). */
export function registerPushSubscription(endpoint: string, p256dh: string, auth: string): Promise<void> {
  return invokeAuthenticated<void>(PUSH_CALL_TIMEOUT_MS, HubMethods.server.registerPushSubscription, endpoint, p256dh, auth)
}

export function unregisterPushSubscription(endpoint: string): Promise<void> {
  return invokeAuthenticated<void>(PUSH_CALL_TIMEOUT_MS, HubMethods.server.unregisterPushSubscription, endpoint)
}

/** Tell the PC whether this phone has the app visible (no pushes to `endpoint` meanwhile). */
export function setPushForeground(endpoint: string, foreground: boolean): Promise<void> {
  return invokeAuthenticated<void>(PUSH_CALL_TIMEOUT_MS, HubMethods.server.setPushForeground, endpoint, foreground)
}

/** Which events push to `endpoint`. Resolves false when the PC doesn't know the subscription (register first). */
export function setPushPreferences(endpoint: string, preferences: PushPreferences): Promise<boolean> {
  return invokeAuthenticated<boolean>(PUSH_CALL_TIMEOUT_MS, HubMethods.server.setPushPreferences, endpoint, preferences)
}

/** A test notification to `endpoint` now; rejects with the PC's reason (too soon, unknown subscription). */
export function sendTestPush(endpoint: string): Promise<boolean> {
  return invokeAuthenticated<boolean>(TEST_PUSH_TIMEOUT_MS, HubMethods.server.sendTestPush, endpoint)
}

// --- Resume (iOS suspends a backgrounded home-screen app) ---------------------------------------------------

/** Hidden at least this long: the socket may be dead without SignalR knowing yet, so probe it on resume. */
const PROBE_AFTER_HIDDEN_MS = 5000
const PROBE_TIMEOUT_MS = 4000
let hiddenAt = 0
let probing = false

/**
 * The app came back (visible, restored from the back/forward cache, or back online): reconnect at once instead of
 * waiting out a backoff that grew while the app was suspended, and check that a "connected" socket is still alive.
 */
function resume(hiddenMs: number) {
  const c = connection
  if (!c) return
  switch (c.state) {
    case HubConnectionState.Disconnected:
      restartAttempt = 0
      void start()
      break
    case HubConnectionState.Reconnecting:
      // Retries failed while suspended and may now be 15 s apart: restart fresh (onclose schedules a quick retry).
      restartAttempt = 0
      void c.stop().then(() => start())
      break
    case HubConnectionState.Connected:
      if (hiddenMs >= PROBE_AFTER_HIDDEN_MS) void probe(c)
      break
  }
}

/**
 * Round-trip a RefreshState (also fetches a fresh snapshot after the pause); a socket that silently died while
 * suspended does not answer in time and is dropped so onclose reconnects.
 */
async function probe(c: HubConnection) {
  if (probing || !store.authenticated) return
  probing = true
  try {
    const command: RemoteCommand = { requestId: newRequestId(), type: 'RefreshState' }
    const alive = await within(
      c.invoke(HubMethods.server.sendCommand, command).then(
        () => true,
        () => true, // the PC answered (with an error): the socket is alive
      ),
      PROBE_TIMEOUT_MS,
      false,
    )
    if (!alive && c === connection && c.state === HubConnectionState.Connected) void c.stop()
  } finally {
    probing = false
  }
}

/** Reconnect when the app comes back (main.ts). */
export function installHubLifecycle() {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      hiddenAt = Date.now()
      return
    }
    resume(hiddenAt ? Date.now() - hiddenAt : 0)
    hiddenAt = 0
  })
  window.addEventListener('pageshow', (e) => {
    if (e.persisted) resume(Number.POSITIVE_INFINITY)
  })
  window.addEventListener('online', () => resume(Number.POSITIVE_INFINITY))
}
