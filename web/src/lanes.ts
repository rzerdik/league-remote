import { computed } from 'vue'
import type { ChampionLanes } from './contracts'
import { liveState } from './store'

/** The lanes of the champion pickers' filter, in the League client's order. */
export const LANES = [
  { id: 'top', label: 'Top' },
  { id: 'jungle', label: 'Jungle' },
  { id: 'middle', label: 'Mid' },
  { id: 'bottom', label: 'Bot' },
  { id: 'utility', label: 'Support' },
] as const

export type Lane = (typeof LANES)[number]['id']

/** Null while League hasn't answered (closed, or still starting): the pickers leave the filter out then. */
export const championLanes = computed<ChampionLanes | null>(() => liveState.value?.championLanes ?? null)

/** The champions listed under `lane`; null without a lane or a list (no filtering). */
export function laneChampions(lane: Lane | null): ReadonlySet<number> | null {
  const lanes = championLanes.value
  return lane && lanes ? new Set(lanes[lane]) : null
}
