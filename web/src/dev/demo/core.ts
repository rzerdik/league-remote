import type {
  AutoPickState,
  ChatState,
  ClientState,
  FriendsState,
  LiveGameState,
  ProfileState,
} from '../../contracts'
import { store } from '../../store'

/** The page's query: `?demo=<scenario>` plus the scenario's own options (`&game=`, `&auto=`, …). */
export const params = new URLSearchParams(location.search)
export const scenario = params.get('demo')

/**
 * What the fake PC holds, one field per slice. The demo puts it back into the store whenever the store loses it
 * (index.ts), and the command handlers answer from it, like the connector answers from the client.
 */
export const held = {
  state: null as ClientState | null,
  chat: null as ChatState | null,
  friends: null as FriendsState | null,
  profile: null as ProfileState | null,
  liveGame: null as LiveGameState | null,
  autoPick: null as AutoPickState | null,
}

/** Replace the client state the way a connector push would (fresh timer). */
export function push(next: ClientState) {
  held.state = next
  store.state = next
  store.receivedAt.state = performance.now()
}

export const wait = (ms: number) => new Promise((r) => setTimeout(r, ms))

export const minutesAgo = (m: number) => new Date(Date.now() - m * 60000).toISOString()
export const hoursAgo = (h: number) => new Date(Date.now() - h * 3600_000).toISOString()

/** Deterministic "random" numbers per seed, so the demo looks the same on every reload. */
export function seeded(seed: number) {
  let x = seed % 2147483647 || 1
  return (min: number, max: number) => {
    x = (x * 48271) % 2147483647
    return min + (x % (max - min + 1))
  }
}
export type Rnd = ReturnType<typeof seeded>
