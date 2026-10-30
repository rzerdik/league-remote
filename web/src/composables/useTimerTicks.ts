import { computed, watch } from 'vue'
import { finalSecond, TURN_TICK_FROM_S } from '../countdown'
import { play, type SoundKey } from '../sound'

/**
 * Plays a timer tick on every new beat of a countdown, like the League client: `key` once per shown second at or under
 * `from` while `active`. Only on the way down (or the first beat): a fresh client reading can nudge the timer back across
 * a second (3 → 4 → 3), which must not tick twice. The tick goes through `play`, so the mute setting, the Timer Ticks
 * switch and Play in Background silence it like every other cue. Returns the beat (`from + 1` … 1, 0 outside).
 */
export function useTimerTicks(seconds: () => number, from: () => number | null, key: SoundKey) {
  const beat = computed(() => {
    const f = from()
    return f == null ? 0 : finalSecond(seconds(), f)
  })
  watch(beat, (b, prev) => {
    if (b > 0 && (prev === 0 || b < prev)) play(key)
  })
  return beat
}

/**
 * The final seconds of a turn of yours, as the client does them: the loud timer tick once a second from the client's
 * "10" down to "0" (11 ticks over the last 11 s), the same sound for picks and bans. Returns the beat (11 … 1, 0
 * outside) for `EdgeGlow`, which pulses in step with the tick; it drops to 0 once the turn is over.
 */
export function useFinalSeconds(seconds: () => number, active: () => boolean) {
  return useTimerTicks(seconds, () => (active() ? TURN_TICK_FROM_S : null), 'timerTick')
}
