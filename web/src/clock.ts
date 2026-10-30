import { computed, ref } from 'vue'
import { isInGame } from './gameflow'
import { liveState, store } from './store'
import { DURATION } from './styles/tokens'

/** Ticking clock (ms, performance.now) for local timer interpolation; ticks once installClock() ran (main.ts). */
export const now = ref(performance.now())

export function installClock() {
  setInterval(() => (now.value = performance.now()), DURATION.step)
}

const readings = new Map<string, { value: string; at: number }>()

/**
 * Seconds since a server timer last showed this value (`value`) in slot `slot`. Unlike secondsSinceState, a
 * snapshot that is only republished (RefreshState on resume, the replay on sign-in) carries the same reading and does
 * not restart the count, so countdowns don't jump back up. Reactive through `now`.
 */
export function secondsSinceReading(slot: string, value: string | number): number {
  const v = String(value)
  let r = readings.get(slot)
  if (!r || r.value !== v) readings.set(slot, (r = { value: v, at: store.receivedAt.state }))
  return Math.max(0, (now.value - r.at) / 1000)
}

const deadlines = new Map<string, { key: string; seconds: number; endsAt: number }>()

/**
 * Seconds left on a server countdown that only runs down within one `key` (e.g. a champ-select phase and turn): each
 * new `seconds` value (time left when its snapshot was taken) is counted from the state that brought it, but only moves
 * the end earlier, never later. A fresh snapshot corrects a stale copy (the replay on sign-in), while delivery jitter
 * between two fresh ones can't step the count back up across a second (4 → 5 → 4) and replay a tick or pulse.
 * A new `key` starts over from its value. Reactive through `now`.
 */
export function countdownSeconds(slot: string, key: string, seconds: number): number {
  const d = deadlines.get(slot)
  const endsAt = store.receivedAt.state + seconds * 1000
  if (!d || d.key !== key) deadlines.set(slot, { key, seconds, endsAt })
  else if (d.seconds !== seconds) {
    d.seconds = seconds
    d.endsAt = Math.min(d.endsAt, endsAt)
  }
  return Math.max(0, (deadlines.get(slot)!.endsAt - now.value) / 1000)
}

/**
 * Never run on more than a couple of publish intervals past the last new value: a clock that stopped moving (paused
 * game, stale connector) must not keep counting up and then snap back.
 */
const MAX_CLOCK_EXTRAPOLATION_S = 12

/**
 * A game clock interpolated between snapshots that carry `gameTime` (seconds): call it with each snapshot's gameTime
 * and when its slice arrived. Anchored on the moment a gameTime value first arrived, not on the latest snapshot: other
 * updates republish the same (older) value and would otherwise make the clock jump backwards. Reactive through `now`.
 */
export function anchoredGameClock(): (gameTime: number, receivedAt: number) => number {
  let anchor: { gameTime: number; at: number } | null = null
  return (gameTime, receivedAt) => {
    if (anchor?.gameTime !== gameTime) anchor = { gameTime, at: receivedAt }
    return gameTime + Math.min(MAX_CLOCK_EXTRAPOLATION_S, Math.max(0, (now.value - anchor.at) / 1000))
  }
}

const clientGameClock = anchoredGameClock()

/** ClientState.inGame while the game is on (loading screen included); null otherwise. */
const inGame = computed(() => {
  const s = liveState.value
  if (!s || !s.lcuConnected || !isInGame(s.gameflowPhase)) return null
  return s.inGame ?? null
})

export const gameLoaded = computed(() => !!inGame.value?.loaded)

/**
 * Game clock in seconds, or null while loading / not in game. The connector republishes it only every ~5 s, so it is
 * interpolated in between.
 */
export const gameClock = computed<number | null>(() => {
  void now.value // re-evaluate on every clock tick
  const g = inGame.value
  return g?.loaded ? clientGameClock(g.gameTime, store.receivedAt.state) : null
})
