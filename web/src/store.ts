import { computed, reactive, type ComputedRef } from 'vue'
import type {
  AutoPickState,
  ChatState,
  ClientState,
  FriendsState,
  LiveGameState,
  PairingGrant,
  ProfileState,
} from './contracts'
import { isRecord, KEYS, readJson, writeJson } from './lib/storage'

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

/** What the connector sends, one hub message per slice (`StateUpdated`, `ChatUpdated`, …; hub.ts maps them). */
export interface Slices {
  state: ClientState
  chat: ChatState
  friends: FriendsState
  profile: ProfileState
  /** The running game from the Live Client Data API; `available: false` once the game ends. */
  liveGame: LiveGameState
  /** Automatic pick, ban and spells, run by the connector. */
  autoPick: AutoPickState
}

export type StateSlice = keyof Slices

export const SLICES: readonly StateSlice[] = ['state', 'chat', 'friends', 'profile', 'liveGame', 'autoPick']

/** A banner (BannerHost): info and success go after dur-banner, warnings and errors after dur-banner-error. */
export interface Toast {
  id: number
  kind: 'info' | 'success' | 'warning' | 'error'
  text: string
  /** Overrides how long it stays (ms). */
  ms?: number
}

/**
 * The grant saved in localStorage right now. Shared by every tab of the app, so it can be newer than `store.grant`
 * (another tab paired again meanwhile).
 */
export function loadGrant(): PairingGrant | null {
  return readJson(KEYS.grant, (raw) =>
    isRecord(raw) && typeof raw.token === 'string' && typeof raw.pairingId === 'string' ? (raw as unknown as PairingGrant) : null,
  )
}

function perSlice<T>(value: T): Record<StateSlice, T> {
  return Object.fromEntries(SLICES.map((s) => [s, value])) as Record<StateSlice, T>
}

/** Plain reactive app store. Mutated by hub.ts and the dev demo (and toasts by anyone). */
export const store = reactive({
  connection: 'disconnected' as ConnectionStatus,
  /** True once Authenticate succeeded on the current connection. */
  authenticated: false,
  grant: loadGrant(),
  /** Each slice is null until the connector sent one (or after a sign-in, until it sent a fresh one). */
  state: null as ClientState | null,
  chat: null as ChatState | null,
  friends: null as FriendsState | null,
  profile: null as ProfileState | null,
  liveGame: null as LiveGameState | null,
  autoPick: null as AutoPickState | null,
  /**
   * Slices still owed since the last sign-in: while true the slice is null because it is on its way, not because there
   * is none, so screens show their loading state rather than an empty one. Bounded: it turns false once the copy
   * arrives or the wait is over (hub.ts).
   */
  syncing: perSlice(false),
  /** performance.now() when each slice last arrived; timers and clocks are interpolated from here. */
  receivedAt: perSlice(0),
  /** True while a pairing code is being redeemed (typed, scanned or from a `?pair=` deep link). */
  pairing: false,
  toasts: [] as Toast[],
})

/** A slice has arrived: store it and stamp when. */
export function setSlice<K extends StateSlice>(slice: K, value: Slices[K]) {
  ;(store as Record<StateSlice, unknown>)[slice] = value
  store.receivedAt[slice] = performance.now()
  store.syncing[slice] = false
}

/** Slices may be shown only while paired and signed in. */
const canShow = computed(() => !!store.grant && store.authenticated)

function live<K extends StateSlice>(slice: K): ComputedRef<Slices[K] | null> {
  return computed(() => (canShow.value ? ((store as Record<StateSlice, unknown>)[slice] as Slices[K] | null) : null))
}

/** What the UI may show of each slice (null while unpaired or signed out). Views read these, never the raw fields. */
export const liveState = live('state')
export const liveFriends = live('friends')
export const liveChat = live('chat')
export const liveProfile = live('profile')
/** Read it through `liveGame` in liveGame/index.ts. */
export const liveGameState = live('liveGame')
export const liveAutoPick = live('autoPick')

export function saveGrant(grant: PairingGrant | null) {
  store.grant = grant
  writeJson(KEYS.grant, grant) // storage unavailable: kept in memory only
}

let toastSeq = 0
/** Show a banner. BannerHost times it out (and dismisses it from here). */
export function toast(text: string, kind: Toast['kind'] = 'error', ms?: number) {
  store.toasts.push({ id: ++toastSeq, kind, text, ms })
}
export function dismissToast(id: number) {
  const i = store.toasts.findIndex((t) => t.id === id)
  if (i >= 0) store.toasts.splice(i, 1)
}
