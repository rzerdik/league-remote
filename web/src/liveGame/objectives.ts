import type { LiveDragonType, LiveInhibitorTimer, LiveObjectiveTimer, LiveTeam } from '../contracts'
import { teamRelation, teams, type SideRelation } from './index'

/** Summoner's Rift objectives of the running game: names, spawn timers and who took them. */

const DRAKES: Record<LiveDragonType, string> = {
  Fire: 'Infernal',
  Earth: 'Mountain',
  Water: 'Ocean',
  Air: 'Cloud',
  Hextech: 'Hextech',
  Chemtech: 'Chemtech',
  Elder: 'Elder',
}

/** "Infernal" (the client's names for the API's Fire, Earth, Water, Air); the raw type when new. */
export function drakeName(type: string): string {
  return DRAKES[type as LiveDragonType] ?? type
}

/** "Infernal Drake", "Elder Dragon", or "Dragon" while the next drake's element isn't known. */
export function dragonTitle(type: string): string {
  if (type === 'Elder') return 'Elder Dragon'
  return type ? `${drakeName(type)} Drake` : 'Dragon'
}

/** The row title of an objective timer. */
export function objectiveTitle(t: LiveObjectiveTimer): string {
  switch (t.kind) {
    case 'dragon':
      return dragonTitle(t.dragonType)
    case 'elder':
      return 'Elder Dragon'
    case 'baron':
      return 'Baron Nashor'
    case 'herald':
      return 'Rift Herald'
    case 'grubs':
      return 'Voidgrubs'
    default:
      return t.kind
  }
}

export type ObjectiveStatus =
  /** Spawns in `seconds` (at game time `at`). */
  | { state: 'soon'; seconds: number; at: number }
  /** Up now; `until`: it leaves at that game time if nobody takes it (Herald, grubs). */
  | { state: 'up'; until: number | null }
  /** Not again this game: taken (takenBy() says by whom) or gone untaken. */
  | { state: 'done'; taken: boolean }

/** Where an objective is on the interpolated clock. */
export function objectiveStatus(t: LiveObjectiveTimer, clock: number): ObjectiveStatus {
  if (t.spawnsAt == null) return { state: 'done', taken: teams.value.some((team) => timesTaken(team, t.kind) > 0) }
  if (t.spawnsAt > clock) return { state: 'soon', seconds: t.spawnsAt - clock, at: t.spawnsAt }
  if (t.despawnsAt != null && t.despawnsAt <= clock) return { state: 'done', taken: false }
  return { state: 'up', until: t.despawnsAt }
}

/** How often a team took a one-per-game objective (Herald, grubs). */
function timesTaken(team: LiveTeam, kind: string): number {
  return kind === 'herald' ? team.heralds : kind === 'grubs' ? team.grubs : 0
}

/** A side inside a sentence ("by the enemy", "by Red Side"). */
const SIDE_OBJECT: Record<SideRelation, string> = {
  mine: 'your team',
  enemy: 'the enemy',
  blue: 'Blue Side',
  red: 'Red Side',
  unknown: 'a team',
}

/**
 * Who took a Herald or grub camp: "Your team 2, enemy 1" / "By your team"; spectating "Blue Side 2, Red Side 1" /
 * "By Red Side"; '' when nobody (or no known side) took it.
 */
export function takenBy(kind: string): string {
  const takers = teams.value
    .map((t) => ({ relation: teamRelation(t.side), n: timesTaken(t, kind) }))
    .filter((t) => t.n > 0 && t.relation !== 'unknown')
  if (!takers.length) return ''
  if (kind === 'grubs' && takers.length > 1) {
    const list = takers.map((t) => `${SIDE_OBJECT[t.relation].replace(/^the /, '')} ${t.n}`).join(', ')
    return list.charAt(0).toUpperCase() + list.slice(1)
  }
  return `By ${SIDE_OBJECT[takers[0]!.relation]}`
}

const LANES: Record<string, string> = { top: 'Top', mid: 'Mid', bot: 'Bot' }

const OWNER: Record<SideRelation, string> = { mine: 'Your', enemy: 'Enemy', blue: 'Blue', red: 'Red', unknown: '' }

/** "Enemy Mid Inhibitor", "Your Top Inhibitor"; spectating "Red Mid Inhibitor"; "Mid Inhibitor" without a side. */
export function inhibitorTitle(i: LiveInhibitorTimer): string {
  return [OWNER[teamRelation(i.side)], LANES[i.lane], 'Inhibitor'].filter(Boolean).join(' ')
}
