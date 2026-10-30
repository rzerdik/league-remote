/** Countdown thresholds (design system › Motion): orange under 10 s, red under 5 s. */
const WARNING_S = 10
const URGENT_S = 5

/**
 * The League client's champ-select timer ticks (see ClientSoundCatalog.cs): a tick each time its shown second
 * (`Math.floor(ms / 1000)`) changes to a value at or under a threshold. Your own pick or ban: the loud tick from 10;
 * everyone: the soft tick from 5 in Declare Intent, 10 in finalization and 5 in subset-pick modes.
 */
export const TURN_TICK_FROM_S = 10
export const PLANNING_TICK_FROM_S = 5
export const FINALIZATION_TICK_FROM_S = 10
export const SUBSET_PICK_TICK_FROM_S = 5

/**
 * The beat of a ticking countdown: `from + 1` … 1 while the client's shown second is at or under `from` (the client
 * shows `beat - 1`), else 0. It changes in the client's rhythm whenever the shown second does.
 */
export function finalSecond(seconds: number, from = TURN_TICK_FROM_S): number {
  if (!(seconds > 0)) return 0
  const shown = Math.floor(seconds)
  return shown <= from ? shown + 1 : 0
}

/**
 * How much earlier the phone's champ-select countdown ends than League's: timeLeftMs reaches the phone after a network
 * trip and a tap must travel back before League's deadline, so it never shows time League no longer has.
 */
export const CHAMP_SELECT_DELIVERY_ALLOWANCE_S = 0.5

export function countdownState(seconds: number): 'warning' | 'urgent' | null {
  if (seconds <= 0 || seconds >= WARNING_S) return null
  return seconds < URGENT_S ? 'urgent' : 'warning'
}

/**
 * The text of a polite live region next to a silent `role="timer"`: it changes only at 10 and 5 s, so VoiceOver hears
 * "10 seconds left" and "5 seconds left" once each.
 */
export function countdownAnnouncement(seconds: number): string {
  const left = Math.ceil(seconds)
  if (left <= URGENT_S) return `${URGENT_S} seconds left`
  if (left <= WARNING_S) return `${WARNING_S} seconds left`
  return ''
}
