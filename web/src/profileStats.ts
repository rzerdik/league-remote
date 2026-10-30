import type { MatchSummary } from './contracts'
import { plural } from './format'
import { ROLES } from './labels'

/**
 * Stats the Profile computes from a player's last games (ProfileState.matches / PlayerProfile.matches, at most 20, as
 * the connector publishes them): per champion, per role and the recent form. Computed on the phone on purpose — the
 * connector already sends the games, and League has no per-champion stats endpoint for the last 20 of them.
 *
 * Only games that counted: matchmade (not custom or Practice Tool, whose 30-second "wins" mean nothing) and finished
 * (no remakes or aborted games). Arena counts as the client says: a top-half place is a win.
 */

type GameResult = 'win' | 'loss'

/** The games the stats are about, newest first. */
export function countedGames(matches: readonly MatchSummary[]): MatchSummary[] {
  return matches.filter((m) => m.gameType === 'MATCHED_GAME' && (m.result === 'win' || m.result === 'loss'))
}

interface ChampionStat {
  championId: number
  games: number
  wins: number
  kills: number
  deaths: number
  assists: number
}

/** One line per champion played, most played first (then most wins, then the most recent). */
export function championStats(matches: readonly MatchSummary[]): ChampionStat[] {
  const byChampion = new Map<number, ChampionStat>()
  for (const m of countedGames(matches)) {
    const id = m.me.championId
    if (id <= 0) continue
    const s = byChampion.get(id) ?? { championId: id, games: 0, wins: 0, kills: 0, deaths: 0, assists: 0 }
    s.games++
    if (m.result === 'win') s.wins++
    s.kills += m.me.kills
    s.deaths += m.me.deaths
    s.assists += m.me.assists
    byChampion.set(id, s)
  }
  // Map keeps insertion order = newest game first, so a stable sort breaks the last tie by recency.
  return [...byChampion.values()].sort((a, b) => b.games - a.games || b.wins - a.wins)
}

interface RoleStat {
  /** A Positions value: TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY. */
  position: string
  games: number
  wins: number
}

/** The five roles in the client's order, as plain strings to look positions up in. */
const ROLE_ORDER: readonly string[] = ROLES

/**
 * Per role, most played first. The role is League's guess after each game (MatchSummary.position): good for "mostly
 * mid", but it can be wrong in a single game (a recorded game called the top laner a jungler), so the screen says so.
 * Games without a guess (ARAM, Arena, some roams) are left out.
 */
export function roleStats(matches: readonly MatchSummary[]): RoleStat[] {
  const byRole = new Map<string, RoleStat>()
  for (const m of countedGames(matches)) {
    if (!ROLE_ORDER.includes(m.position)) continue
    const s = byRole.get(m.position) ?? { position: m.position, games: 0, wins: 0 }
    s.games++
    if (m.result === 'win') s.wins++
    byRole.set(m.position, s)
  }
  return [...byRole.values()].sort((a, b) => b.games - a.games || ROLE_ORDER.indexOf(a.position) - ROLE_ORDER.indexOf(b.position))
}

interface RecentForm {
  /** Newest first, at most `FORM_GAMES`. */
  results: GameResult[]
  wins: number
  losses: number
  /** The current run of the same result (newest games); null without games. */
  streak: { result: GameResult; count: number } | null
}

/** Games in the form strip (it fits one row at 375 px and still says something). */
const FORM_GAMES = 10

/** The last FORM_GAMES counted games as W / L, their record and the current streak. */
export function recentForm(matches: readonly MatchSummary[]): RecentForm {
  const results = countedGames(matches)
    .slice(0, FORM_GAMES)
    .map((m) => m.result as GameResult)
  const wins = results.filter((r) => r === 'win').length
  let count = 0
  while (count < results.length && results[count] === results[0]) count++
  return { results, wins, losses: results.length - wins, streak: results.length ? { result: results[0]!, count } : null }
}

/** "3W" / "2L" — a streak as League players write it. */
export function streakLabel(s: RecentForm['streak']): string {
  return s ? `${s.count}${s.result === 'win' ? 'W' : 'L'}` : ''
}

/** VoiceOver for streakLabel(): "3 wins in a row", "1 loss". */
export function streakSpoken(s: RecentForm['streak']): string {
  if (!s) return ''
  const run = s.result === 'win' ? plural(s.count, 'win') : plural(s.count, 'loss', 'losses')
  return s.count > 1 ? `${run} in a row` : run
}
