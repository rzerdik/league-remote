import { ordinal } from './format'

/** Maps and teams as finished games (Profile, post-game) and the running game (In Game) show them. */

export const SUMMONERS_RIFT_MAP_ID = 11
/** ARAM (Howling Abyss). */
const ARAM_MAP_ID = 12
/** Arena (Rings of Wrath) has no minions: no CS there, and teams are duos without sides. */
export const ARENA_MAP_ID = 30

/** Maps without trinkets (no wards in ARAM or Arena): their loadouts draw no empty trinket well. */
export function hasTrinkets(mapId: number): boolean {
  return mapId !== ARAM_MAP_ID && mapId !== ARENA_MAP_ID
}

/** The client's team ids and the live game's side names, both as the map side they play on. */
const MAP_SIDES: Record<string | number, 'blue' | 'red'> = { 100: 'blue', 200: 'red', ORDER: 'blue', CHAOS: 'red' }

/** Which map side a team id (100 / 200) or live side (ORDER / CHAOS) is; null for anything else (Arena duos). */
export function mapSide(side: string | number): 'blue' | 'red' | null {
  return MAP_SIDES[side] ?? null
}

/** "Blue Side" / "Red Side"; '' for anything else. */
export function sideName(side: string | number): string {
  const s = mapSide(side)
  return s === 'blue' ? 'Blue Side' : s === 'red' ? 'Red Side' : ''
}

/**
 * The number each enemy team gets in its title ("Enemy Team 2"), by index in `teams`: only when there are several
 * (Arena, an unexpected mode); undefined for your own team and a single enemy.
 */
export function enemyNumbers(teams: readonly { isMyTeam: boolean }[]): (number | undefined)[] {
  const several = teams.filter((t) => !t.isMyTeam).length > 1
  let n = 0
  return teams.map((t) => (several && !t.isMyTeam ? ++n : undefined))
}

/**
 * A finished game's team header: "Victory · Blue Side"; only the side when the game didn't count (remake, aborted).
 * Arena duos have no side and finish in a place ("3rd Place"). Undefined when there is nothing to say.
 */
export function finishedTeamLine(
  team: { teamId: number; win: boolean; placement: number },
  result: string,
  arena: boolean,
): string | undefined {
  if (team.placement > 0) return `${ordinal(team.placement)} Place`
  const counted = result === 'win' || result === 'loss'
  const outcome = counted ? (team.win ? 'Victory' : 'Defeat') : ''
  return [outcome, arena ? '' : sideName(team.teamId)].filter(Boolean).join(' · ') || undefined
}
