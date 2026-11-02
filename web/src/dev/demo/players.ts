// Other players' profiles. View Profile (friend sheet, party sheet, champ-select team menu) and a named teammate's row
// in a game (yours, a player's, the post-game screen) → LoadPlayerProfile; Profile › Search → SearchPlayer. Like the
// connector, the demo answers only for a friend, a party member, a named teammate (in champion select or in a game it
// sent) or a player a game or a search opened, and refuses anyone else. `?demo=profile&player=1` opens Vexa's (a
// friend), `&player=2` a stranger (the refusal), `&player=3` Kestrel as if tapped in your newest game; `&game=<n>` then
// opens their game n. Search finds Vexa #EUW, NightOwl #EUW, Quillon #QX7 and Duskfall #EUW (any case); anything else
// is "No player with that Riot ID.", and your own Riot ID is refused.
import {
  ProfileMaxPlayerDetails,
  ProfileMaxPlayers,
  type CommandResult,
  type MatchDetails,
  type MatchSummary,
  type PlayerProfile,
} from '../../contracts'
import { nav } from '../../navigation'
import { store } from '../../store'
import { held, hoursAgo, params, seeded, wait } from './core'
import { KESTREL, ME, NIGHTOWL, riotIdOf, VEXA } from './people'
import { ALLIES, ENEMIES, mate, player, profileState, pushProfile, team, type Build } from './profile'

interface DemoPlayer {
  name: string
  tag: string
  icon: number
  level: number
  /** Champion, build and the role League guesses for it. */
  pool: [number, Build, string][]
}

const DEMO_PLAYERS: Record<string, DemoPlayer> = {
  [VEXA.puuid]: { name: VEXA.gameName, tag: VEXA.tagLine, icon: 4568, level: 287, pool: [[412, 'support', 'UTILITY'], [89, 'support', 'UTILITY'], [117, 'support', 'UTILITY'], [51, 'adc', 'BOTTOM']] },
  [NIGHTOWL.puuid]: { name: NIGHTOWL.gameName, tag: NIGHTOWL.tagLine, icon: 29, level: 198, pool: [[64, 'jungle', 'JUNGLE'], [254, 'jungle', 'JUNGLE'], [86, 'top', 'TOP']] },
  [KESTREL.puuid]: { name: KESTREL.gameName, tag: KESTREL.tagLine, icon: 5132, level: 142, pool: [[51, 'adc', 'BOTTOM'], [222, 'adc', 'BOTTOM'], [145, 'adc', 'BOTTOM']] },
  'demo-lumen': { name: 'Lumen', tag: '7788', icon: 3500, level: 76, pool: [[412, 'support', 'UTILITY'], [267, 'support', 'UTILITY'], [103, 'mage', 'MIDDLE']] },
  'demo-quillon': { name: 'Quillon', tag: 'QX7', icon: 6, level: 812, pool: [[7, 'mage', 'MIDDLE'], [4, 'mage', 'MIDDLE'], [517, 'mage', 'MIDDLE']] },
  'demo-duskfall': { name: 'Duskfall', tag: 'EUW', icon: 588, level: 233, pool: [[266, 'top', 'TOP'], [86, 'top', 'TOP']] },
  'demo-ivory': { name: 'Ivory', tag: 'EUW', icon: 785, level: 64, pool: [[254, 'jungle', 'JUNGLE'], [64, 'jungle', 'JUNGLE']] },
  'demo-pellmell': { name: 'Pellmell', tag: 'EUW', icon: 1101, level: 155, pool: [[238, 'mage', 'MIDDLE'], [103, 'mage', 'MIDDLE']] },
  'demo-sunken': { name: 'Sunken', tag: 'EUW', icon: 3379, level: 301, pool: [[222, 'adc', 'BOTTOM'], [51, 'adc', 'BOTTOM']] },
}

/** Riot IDs the demo's search finds (any case), answered in League's spelling. */
const DEMO_SEARCHABLE = [VEXA.puuid, NIGHTOWL.puuid, 'demo-quillon', 'demo-duskfall']

/** Players a game's named line or a search opened: viewable while their profile is cached, like the connector. */
const demoAccess = new Set<string>()

/** The demo's "PC" check for a game's line: a puuid it sent beside a teammate's name (your games, a player's, post-game). */
function demoNamedInGame(puuid: string): boolean {
  const lines = [
    ...(held.profile?.details ?? []),
    ...(held.profile?.players ?? []).flatMap((p) => p.details),
  ].flatMap((d) => d.teams.flatMap((t) => t.participants))
  const post = store.state?.postGame?.teams.flatMap((t) => t.participants) ?? []
  return [...lines, ...post].some((p) => !!p.gameName && p.puuid === puuid)
}

/** Still cached with access from a game or a search (it goes with the profile). */
function demoKeepsAccess(puuid: string): boolean {
  return demoAccess.has(puuid) && !!held.profile?.players.some((p) => p.puuid === puuid)
}

/** The demo's "PC" re-check: friends, the party (lobby scenarios) and named champ-select teammates only. */
function demoCanView(puuid: string): boolean {
  if (demoKeepsAccess(puuid)) return true
  if (held.friends?.friends.some((f) => f.puuid === puuid)) return true
  const s = store.state
  if (s?.lobby?.members.some((m) => m.puuid === puuid && !m.isLocalPlayer)) return true
  return !!s?.champSelect?.myTeam.some((m) => m.puuid === puuid && !m.isLocalPlayer)
}

function demoPlayerProfile(puuid: string): PlayerProfile {
  const friendName = held.friends?.friends.find((f) => f.puuid === puuid)
  const p: DemoPlayer = DEMO_PLAYERS[puuid] ?? {
    name: friendName?.gameName ?? 'Player',
    tag: friendName?.tagLine ?? 'EUW',
    icon: 29,
    level: 120,
    pool: [[103, 'mage', 'MIDDLE'], [238, 'mage', 'MIDDLE']],
  }
  const seed = [...puuid].reduce((n, c) => (n * 31 + c.charCodeAt(0)) % 100_000, 7)
  const rnd = seeded(seed + 1)
  const matches: MatchSummary[] = Array.from({ length: 18 }, (_, i) => {
    const aram = i === 9
    const [championId, build, position] = aram ? ([99, 'mage', ''] as [number, Build, string]) : p.pool[rnd(0, p.pool.length - 1)]!
    const result: MatchSummary['result'] = i === 5 ? 'remake' : (seed + i * 3) % 5 < 3 ? 'win' : 'loss'
    const me = player(championId, build, rnd, { isMe: true, gameName: p.name, tagLine: p.tag })
    return {
      gameId: 8_000_000_000 + seed * 100 + i,
      queueId: aram ? 450 : 420,
      gameMode: aram ? 'ARAM' : 'CLASSIC',
      gameType: 'MATCHED_GAME',
      mapId: aram ? 12 : 11,
      startedAt: hoursAgo(3 + i * 7),
      durationSeconds: result === 'remake' ? 205 : rnd(1300, 2200),
      result,
      position,
      me,
    }
  })
  return {
    puuid,
    summoner: { gameName: p.name, tagLine: p.tag, level: p.level, xpSinceLastLevel: 900, xpForNextLevel: 2880, profileIconId: p.icon },
    // League reports no losses for other players.
    ranks: profileState().ranks.map((r) =>
      r.queueType === 'RANKED_SOLO_5x5'
        ? { ...r, tier: 'PLATINUM', division: 'I', leaguePoints: 71, wins: 58, losses: null }
        : { ...r, losses: null },
    ),
    mastery: {
      score: 640,
      championCount: 91,
      topChampions: p.pool.map(([championId], i) => ({
        championId,
        level: 30 - i * 8,
        points: 300_000 - i * 90_000,
        pointsSinceLastLevel: 2000,
        pointsUntilNextLevel: 9000,
        highestGrade: i === 0 ? 'S' : '',
        lastPlayed: hoursAgo(3),
      })),
    },
    matches,
    details: [],
    loadedAt: new Date().toISOString(),
  }
}

/**
 * One of their games from their side, as the connector sends it: their team first and named; the other team by
 * champion only. Their second game was against you, so there only they are named.
 */
function demoPlayerDetails(p: PlayerProfile, m: MatchSummary): MatchDetails {
  const rnd = seeded(m.gameId + 3)
  const againstYou = m.gameId === p.matches[1]?.gameId
  const names = ['Duskfall', 'Ivory', 'Pellmell', 'Sunken']
  const mates = ENEMIES.slice(0, 4).map(([id, build], i) =>
    player(id, build, rnd, againstYou ? {} : mate(names[i]!, 'EUW')),
  )
  mates.splice(2, 0, m.me)
  const others = ALLIES.map(([id, build]) => player(id, build, rnd))
  others.splice(2, 0, player(103, 'mage', rnd)) // you, in the game against you: unnamed like everyone on that side
  return {
    gameId: m.gameId,
    teams: [
      team(100, true, m.result === 'win', mates, [157, 777, 11, 122, 555], rnd),
      team(200, false, m.result === 'loss', others, [236, 17, 0, 133, 84], rnd),
    ],
  }
}

function pushPlayer(next: PlayerProfile) {
  if (!held.profile) return
  const others = held.profile.players.filter((x) => x.puuid !== next.puuid)
  const players = [next, ...others].slice(0, ProfileMaxPlayers)
  for (const puuid of demoAccess) if (!players.some((x) => x.puuid === puuid)) demoAccess.delete(puuid)
  pushProfile({ ...held.profile, players, updatedAt: new Date().toISOString() })
}

const CANNOT_VIEW = 'You can only view friends, your party and your team.'

/** LoadPlayerProfile, and LoadMatchDetails of one of their games (with a puuid), answered like the connector. */
export async function demoPlayer(cmd: { type: string; puuid?: string | null; gameId?: number | null; requestId: string }): Promise<CommandResult> {
  await wait(cmd.type === 'LoadPlayerProfile' ? 900 : 1000)
  const puuid = cmd.puuid ?? ''
  if (!held.profile) return { requestId: cmd.requestId, success: false, error: 'League client is not running.' }
  const known = held.profile.players.find((x) => x.puuid === puuid)
  if (cmd.type === 'LoadMatchDetails') {
    const m = known?.matches.find((x) => x.gameId === cmd.gameId)
    if (!known || !m) return { requestId: cmd.requestId, success: false, error: "That game isn't in their match history." }
    if (!demoCanView(puuid)) return { requestId: cmd.requestId, success: false, error: CANNOT_VIEW }
    const details = [demoPlayerDetails(known, m), ...known.details.filter((d) => d.gameId !== m.gameId)].slice(0, ProfileMaxPlayerDetails)
    pushPlayer({ ...known, details })
    return { requestId: cmd.requestId, success: true }
  }
  const named = demoKeepsAccess(puuid) || demoNamedInGame(puuid)
  if (!named && !demoCanView(puuid)) return { requestId: cmd.requestId, success: false, error: CANNOT_VIEW }
  if (named) demoAccess.add(puuid)
  pushPlayer(known ? { ...known, loadedAt: new Date().toISOString() } : demoPlayerProfile(puuid))
  return { requestId: cmd.requestId, success: true }
}

/** SearchPlayer: a lookup in DEMO_SEARCHABLE (any case), then the profile, as the connector answers it. */
export async function demoSearch(cmd: { gameName?: string | null; tagLine?: string | null; requestId: string }): Promise<CommandResult> {
  await wait(1400)
  if (!held.profile) return { requestId: cmd.requestId, success: false, error: 'League client is not running.' }
  const key = `${cmd.gameName ?? ''}#${cmd.tagLine ?? ''}`.toLowerCase()
  if (key === riotIdOf(ME).toLowerCase()) return { requestId: cmd.requestId, success: false, error: "That's your own Riot ID." }
  const puuid = DEMO_SEARCHABLE.find((id) => {
    const p = DEMO_PLAYERS[id]!
    return `${p.name}#${p.tag}`.toLowerCase() === key
  })
  if (!puuid) return { requestId: cmd.requestId, success: false, error: 'No player with that Riot ID.' }
  demoAccess.add(puuid)
  const known = held.profile.players.find((x) => x.puuid === puuid)
  pushPlayer(known ? { ...known, loadedAt: new Date().toISOString() } : demoPlayerProfile(puuid))
  return { requestId: cmd.requestId, success: true }
}

/**
 * ?demo=profile lands on the Profile tab; `&game=0` … opens that game's details. `&player=1` opens Vexa's profile over
 * it (a friend; `&player=2`: a stranger, refused; `&player=3`: Kestrel, a teammate of your newest game, whose details
 * come preloaded), and then `&game=<n>` opens their game n.
 */
export function openProfileDemo() {
  nav.tab = 'profile'
  const profile = held.profile!
  const game = params.get('game')
  const playerParam = params.get('player')
  if (playerParam === '1' || playerParam === '2' || playerParam === '3') {
    const who = playerParam === '1' ? VEXA : playerParam === '2' ? { gameName: 'Stranger', tagLine: '0000', puuid: 'demo-stranger' } : KESTREL
    const gameId = game !== null ? (demoPlayerProfile(who.puuid).matches[Number(game)]?.gameId ?? null) : null
    if (playerParam === '3') nav.profileGameId = profile.matches[0]!.gameId // Back returns to that game
    nav.player = { puuid: who.puuid, name: riotIdOf(who), gameId }
  } else {
    const opened = game !== null ? profile.matches[Number(game)] : undefined
    if (opened) nav.profileGameId = opened.gameId
  }
}
