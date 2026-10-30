import { computed, reactive, watch } from 'vue'
import { isInGame } from '../gameflow'
import { liveGameState, liveState } from '../store'
import { knowsMyTeam, liveClock } from './index'

/** The API has no jungle camps: buffs are timed by hand, 5:00 after a tap (Blue Sentinel, Red Brambleback). */
export const BUFF_RESPAWN_S = 5 * 60

export type BuffKey = 'myBlue' | 'myRed' | 'enemyBlue' | 'enemyRed'
/** The four timers; `spoken` is what VoiceOver hears (BuffTimers shows them under "Your Team" / "Enemy"). */
const BUFFS: { key: BuffKey; spoken: string }[] = [
  { key: 'myBlue', spoken: 'your blue buff' },
  { key: 'myRed', spoken: 'your red buff' },
  { key: 'enemyBlue', spoken: 'the enemy blue buff' },
  { key: 'enemyRed', spoken: 'the enemy red buff' },
]
/** Spectating there is no "your" side: the same four timers by map side (mine = blue side's, enemy = red side's). */
const SPECTATOR_BUFFS: Record<BuffKey, { spoken: string }> = {
  myBlue: { spoken: 'Blue Side’s blue buff' },
  myRed: { spoken: 'Blue Side’s red buff' },
  enemyBlue: { spoken: 'Red Side’s blue buff' },
  enemyRed: { spoken: 'Red Side’s red buff' },
}

/** The buff timers as labelled for this game (yours / the enemy's, or by side when spectating). */
export const buffList = computed(() => (knowsMyTeam.value ? BUFFS : BUFFS.map((b) => ({ ...b, ...SPECTATOR_BUFFS[b.key] }))))

/**
 * Game time each buff respawns at, null when not timed. Local only, on the game clock (so it survives re-renders and
 * reconnects); cleared when the game ends.
 */
const buffTimers = reactive<Record<BuffKey, number | null>>({ myBlue: null, myRed: null, enemyBlue: null, enemyRed: null })

/** Seconds until the buff respawns; null when not timed, 0 once it is up again. */
export function buffLeft(key: BuffKey): number | null {
  const at = buffTimers[key]
  const t = liveClock.value
  if (at == null || t == null) return null
  return Math.max(0, at - t)
}

/** Tap: a running timer is cleared (tapped by mistake); otherwise the buff was just taken, start 5:00. */
export function toggleBuff(key: BuffKey) {
  const t = liveClock.value
  if (t == null) return
  const left = buffLeft(key)
  buffTimers[key] = left != null && left > 0 ? null : t + BUFF_RESPAWN_S
}

function clearBuffs() {
  for (const b of BUFFS) buffTimers[b.key] = null
}

/** A clock that jumps back this far is another game. */
const NEW_GAME_JUMP_S = 30
let lastClock = 0

// Only a real end clears them: a reconnect also empties the slices for a moment (store.syncing), that must not.
watch(
  () => liveGameState.value?.available === false || (!!liveState.value?.gameflowPhase && !isInGame(liveState.value.gameflowPhase)),
  (ended) => {
    if (ended) clearBuffs()
  },
)
watch(liveClock, (t) => {
  if (t == null) return
  if (t < lastClock - NEW_GAME_JUMP_S) clearBuffs()
  lastClock = t
})
