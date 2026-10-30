import { computed } from 'vue'
import { countdownSeconds } from '../../clock'
import { useFinalSeconds, useTimerTicks } from '../../composables/useTimerTicks'
import type { ChampSelectState } from '../../contracts'
import {
  CHAMP_SELECT_DELIVERY_ALLOWANCE_S,
  FINALIZATION_TICK_FROM_S,
  PLANNING_TICK_FROM_S,
  SUBSET_PICK_TICK_FROM_S,
} from '../../countdown'
import { myTurnNow } from '../../draft'

/**
 * Champion Select's countdown with the client's timer ticks. One countdown per phase, turn and own action: within one,
 * a fresh reading only ever brings the end earlier (a replayed snapshot changes nothing); a new one starts over.
 */
export function useDraftTimer(cs: () => ChampSelectState) {
  const key = computed(() => {
    const s = cs()
    return `${s.timerPhase}:${s.turns.filter((t) => t.completed).length}:${s.myAction?.id ?? ''}`
  })
  const timeLeft = computed(() =>
    Math.max(0, countdownSeconds('champselect', key.value, cs().timeLeftMs / 1000) - CHAMP_SELECT_DELIVERY_ALLOWANCE_S),
  )
  const showTimer = computed(() => !cs().isTimerInfinite && timeLeft.value > 0)

  /**
   * The last seconds of your own pick or ban: the client's loud tick, and the beat for the edge glow. Subset-pick modes
   * tick softly for everyone instead, as the client does.
   */
  const finalBeat = useFinalSeconds(
    () => timeLeft.value,
    () => myTurnNow(cs()) && showTimer.value && cs().timerPhase === 'BAN_PICK' && !cs().allowSubsetChampionPicks,
  )

  /** The client's soft tick for everyone: last 5 s of Declare Intent, last 10 s of finalization, last 5 s of subset picks. */
  useTimerTicks(
    () => timeLeft.value,
    () => {
      if (!showTimer.value) return null
      const s = cs()
      if (s.timerPhase === 'PLANNING') return PLANNING_TICK_FROM_S
      if (s.timerPhase === 'FINALIZATION') return FINALIZATION_TICK_FROM_S
      if (s.timerPhase === 'BAN_PICK' && s.allowSubsetChampionPicks) return SUBSET_PICK_TICK_FROM_S
      return null
    },
    'timerTickSmall',
  )

  return { timeLeft, showTimer, finalBeat }
}
