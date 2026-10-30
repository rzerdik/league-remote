// ?demo=profile: the Profile tab (Solo/Duo ranked, Flex in placements, 10 mastery champions, 12 games incl. a remake,
// an aborted Practice Tool game and an Arena game). The newest game's details come preloaded; others load after a
// short delay (the Viktor game fails once, to show Try Again). `&game=<index>` opens that game of the list right away.
import {
  ProfileMaxDetails,
  type MatchDetails,
  type MatchParticipant,
  type MatchSummary,
  type MatchTeam,
  type ProfileState,
} from '../../contracts'
import { store } from '../../store'
import { held, hoursAgo, seeded, wait, type Rnd } from './core'
import { KESTREL, LUMEN, ME, NIGHTOWL, VEXA } from './people'

export const BUILDS = {
  mage: [6655, 3020, 4645, 3089, 3157, 3135, 3340],
  adc: [6672, 3006, 3031, 3094, 3036, 3072, 3363],
  top: [3071, 3047, 3053, 6333, 3742, 0, 3340],
  jungle: [3071, 3111, 3053, 6333, 0, 0, 3364],
  support: [3865, 3158, 3190, 3107, 3222, 0, 3364],
}
export type Build = keyof typeof BUILDS

const SPELL2: Record<Build, number> = { mage: 14, adc: 7, top: 12, jungle: 11, support: 3 }
const KEYSTONE: Record<Build, [number, number, number]> = {
  mage: [8112, 8100, 8200],
  adc: [8008, 8000, 8300],
  top: [8010, 8000, 8400],
  jungle: [8010, 8000, 8100],
  support: [8465, 8400, 8300],
}

/** One player's line of a game: random but steady stats for the build. */
export function player(championId: number, build: Build, rnd: Rnd, extra: Partial<MatchParticipant> = {}): MatchParticipant {
  const items = BUILDS[build].map((id, i) => (i < 6 && rnd(0, 6) === 0 ? 0 : id))
  const support = build === 'support'
  const [keystone, primaryStyle, subStyle] = KEYSTONE[build]
  return {
    championId,
    isMe: false,
    gameName: '',
    tagLine: '',
    champLevel: rnd(13, 18),
    kills: rnd(0, 12),
    deaths: rnd(1, 9),
    assists: rnd(2, support ? 22 : 12),
    cs: support ? rnd(20, 50) : rnd(140, 260),
    gold: rnd(8500, 15500),
    damage: rnd(support ? 6000 : 14000, support ? 14000 : 38000),
    visionScore: rnd(support ? 50 : 12, support ? 90 : 35),
    items,
    spell1Id: 4,
    spell2Id: SPELL2[build],
    keystone,
    primaryStyle,
    subStyle,
    placement: 0,
    puuid: '',
    ...extra,
  }
}

interface DemoGame {
  gameId: number
  queueId: number
  gameMode: string
  gameType?: string
  mapId?: number
  hours: number
  minutes: number
  result: MatchSummary['result']
  championId: number
  kda: [number, number, number]
  cs: number
  placement?: number
  /** League's role guess; MIDDLE when not set (none on ARAM / Arena maps). */
  position?: string
}

const DEMO_GAMES: DemoGame[] = [
  { gameId: 7_312_004_811, queueId: 420, gameMode: 'CLASSIC', hours: 2, minutes: 31.4, result: 'win', championId: 103, kda: [7, 2, 9], cs: 212 },
  { gameId: 7_312_004_502, queueId: 420, gameMode: 'CLASSIC', hours: 3, minutes: 28.2, result: 'loss', championId: 134, kda: [3, 6, 4], cs: 168, position: 'TOP' },
  { gameId: 7_311_998_120, queueId: 440, gameMode: 'CLASSIC', hours: 5, minutes: 35.9, result: 'win', championId: 103, kda: [11, 4, 6], cs: 241 },
  { gameId: 7_311_997_004, queueId: 420, gameMode: 'CLASSIC', hours: 6, minutes: 3.7, result: 'remake', championId: 61, kda: [0, 0, 0], cs: 9 },
  { gameId: 7_311_990_417, queueId: 450, gameMode: 'ARAM', mapId: 12, hours: 20, minutes: 19.6, result: 'win', championId: 99, kda: [14, 8, 31], cs: 41 },
  { gameId: 7_311_985_330, queueId: 420, gameMode: 'CLASSIC', hours: 22, minutes: 33.1, result: 'loss', championId: 112, kda: [5, 7, 3], cs: 230 },
  { gameId: 7_311_984_001, queueId: 3140, gameMode: 'PRACTICETOOL', gameType: 'CUSTOM_GAME', hours: 23, minutes: 0.8, result: 'aborted', championId: 103, kda: [0, 0, 0], cs: 0 },
  { gameId: 7_311_950_772, queueId: 400, gameMode: 'CLASSIC', hours: 30, minutes: 26.5, result: 'win', championId: 1, kda: [9, 3, 8], cs: 189, position: 'UTILITY' },
  { gameId: 7_311_940_218, queueId: 1700, gameMode: 'CHERRY', mapId: 30, hours: 50, minutes: 18.3, result: 'loss', championId: 103, kda: [6, 5, 4], cs: 0, placement: 5 },
  { gameId: 7_311_902_664, queueId: 420, gameMode: 'CLASSIC', hours: 74, minutes: 29.8, result: 'win', championId: 103, kda: [8, 1, 12], cs: 224 },
  { gameId: 7_311_870_093, queueId: 420, gameMode: 'CLASSIC', hours: 100, minutes: 24.0, result: 'loss', championId: 238, kda: [4, 8, 2], cs: 151 },
  { gameId: 7_311_712_340, queueId: 490, gameMode: 'CLASSIC', hours: 24 * 9, minutes: 22.7, result: 'win', championId: 103, kda: [12, 3, 7], cs: 198 },
]

function demoMatch(g: DemoGame): MatchSummary {
  const rnd = seeded(g.gameId)
  const short = g.result === 'aborted' || g.result === 'remake'
  const me = player(g.championId, 'mage', rnd, {
    isMe: true,
    gameName: ME.gameName,
    tagLine: ME.tagLine,
    kills: g.kda[0],
    deaths: g.kda[1],
    assists: g.kda[2],
    cs: g.cs,
    champLevel: short ? 1 : rnd(14, 18),
    items: short ? [1056, 2003, 0, 0, 0, 0, 3340] : BUILDS.mage,
    spell2Id: g.mapId === 12 ? 32 : 14,
    placement: g.placement ?? 0,
  })
  return {
    gameId: g.gameId,
    queueId: g.queueId,
    gameMode: g.gameMode,
    gameType: g.gameType ?? 'MATCHED_GAME',
    mapId: g.mapId ?? 11,
    startedAt: hoursAgo(g.hours),
    durationSeconds: Math.round(g.minutes * 60),
    result: g.result,
    position: g.mapId ? '' : (g.position ?? 'MIDDLE'),
    me,
  }
}

/** Your team mates by Riot ID (the connector keeps names on your own team only). */
export const ALLIES: [number, Build, string, string][] = [
  [86, 'top', NIGHTOWL.gameName, NIGHTOWL.tagLine],
  [64, 'jungle', KESTREL.gameName, KESTREL.tagLine],
  [51, 'adc', LUMEN.gameName, LUMEN.tagLine],
  [412, 'support', VEXA.gameName, VEXA.tagLine],
]

/** A named teammate's puuid as the connector sends it beside the name (Vexa is the friend 'friend-1'). */
function demoPuuid(gameName: string): string {
  return gameName === VEXA.gameName ? VEXA.puuid : `demo-${gameName.toLowerCase()}`
}

/** A named teammate's line: name, tag and puuid (MatchParticipant.puuid only ever comes with a name). */
export function mate(gameName: string, tagLine: string) {
  return { gameName, tagLine, puuid: demoPuuid(gameName) }
}
export const ENEMIES: [number, Build][] = [
  [266, 'top'],
  [254, 'jungle'],
  [238, 'mage'],
  [222, 'adc'],
  [89, 'support'],
]

export function team(teamId: number, isMyTeam: boolean, win: boolean, players: MatchParticipant[], bans: number[], rnd: Rnd): MatchTeam {
  return {
    teamId,
    isMyTeam,
    win,
    kills: players.reduce((n, p) => n + p.kills, 0),
    gold: players.reduce((n, p) => n + p.gold, 0),
    towers: win ? rnd(7, 11) : rnd(1, 5),
    inhibitors: win ? rnd(1, 3) : rnd(0, 1),
    dragons: win ? rnd(2, 4) : rnd(0, 2),
    barons: win ? rnd(0, 2) : 0,
    heralds: rnd(0, 1),
    grubs: rnd(0, 6),
    bans,
    participants: players,
  }
}

/** A game's details as the connector sends them: own team first, enemies without names. */
function demoDetails(m: MatchSummary): MatchDetails {
  const rnd = seeded(m.gameId + 7)
  if (m.mapId === 30) {
    // Arena: duos, each its own team, with their placement.
    const duo = (place: number, a: number, b: number, mine: boolean) =>
      team(
        100 + place,
        mine,
        place <= 4,
        [
          mine ? m.me : player(a, 'mage', rnd, { placement: place }),
          player(b, 'adc', rnd, mine ? { ...mate(VEXA.gameName, VEXA.tagLine), placement: place } : { placement: place }),
        ],
        [],
        rnd,
      )
    return { gameId: m.gameId, teams: [duo(5, 0, 51, true), duo(1, 157, 777, false), duo(2, 92, 875, false), duo(3, 38, 69, false)] }
  }
  const allies = ALLIES.map(([id, build, gameName, tagLine]) => player(id, build, rnd, mate(gameName, tagLine)))
  allies.splice(2, 0, m.me)
  const enemies = ENEMIES.map(([id, build]) => player(id, build, rnd))
  const drafted = m.queueId === 420 || m.queueId === 440 || m.queueId === 400
  const won = m.result === 'win'
  return {
    gameId: m.gameId,
    teams: [
      team(100, true, won, allies, drafted ? [157, 777, 11, 122, 555] : [], rnd),
      team(200, false, m.result === 'loss', enemies, drafted ? [236, 17, 0, 133, 84] : [], rnd),
    ],
  }
}

const MASTERY: [number, number, number, string][] = [
  [103, 48, 562_310, 'S+'],
  [134, 21, 214_880, 'S'],
  [112, 17, 168_402, 'A+'],
  [61, 14, 131_090, 'S-'],
  [1, 12, 109_550, ''],
  [99, 11, 98_220, 'A'],
  [238, 9, 76_410, 'B+'],
  [4, 7, 54_300, ''],
  [127, 6, 43_120, 'A-'],
  [517, 5, 31_870, ''],
]

export function profileState(): ProfileState {
  const matches = DEMO_GAMES.map(demoMatch)
  return {
    summoner: { gameName: ME.gameName, tagLine: ME.tagLine, level: 312, xpSinceLastLevel: 1680, xpForNextLevel: 2880, profileIconId: 5132 },
    ranks: [
      {
        queueType: 'RANKED_SOLO_5x5',
        tier: 'EMERALD',
        division: 'III',
        leaguePoints: 47,
        wins: 34,
        losses: 28,
        isProvisional: false,
        provisionalGamesRemaining: 0,
        promos: '',
        highestTier: 'EMERALD',
        highestDivision: 'II',
        previousSeasonEndTier: 'PLATINUM',
        previousSeasonEndDivision: 'I',
      },
      {
        queueType: 'RANKED_FLEX_SR',
        tier: '',
        division: '',
        leaguePoints: 0,
        wins: 1,
        losses: 0,
        isProvisional: true,
        provisionalGamesRemaining: 4,
        promos: '',
        highestTier: '',
        highestDivision: '',
        previousSeasonEndTier: '',
        previousSeasonEndDivision: '',
      },
    ],
    mastery: {
      score: 1287,
      championCount: 143,
      topChampions: MASTERY.map(([championId, level, points, highestGrade]) => ({
        championId,
        level,
        points,
        pointsSinceLastLevel: 4200,
        pointsUntilNextLevel: 6800,
        highestGrade,
        lastPlayed: hoursAgo(2),
      })),
    },
    matches,
    details: [demoDetails(matches[0]!)],
    updatedAt: new Date().toISOString(),
    players: [],
  }
}

export function pushProfile(next: ProfileState) {
  held.profile = next
  store.profile = next
}

const failedOnce = new Set<number>()
/** RefreshProfile republishes; LoadMatchDetails adds the game after a delay (the Viktor game refuses once). */
export async function demoProfile(cmd: { type: string; gameId?: number | null; requestId: string }) {
  await wait(cmd.type === 'RefreshProfile' ? 300 : 1200)
  const profile = held.profile
  if (!profile) return { requestId: cmd.requestId, success: false, error: 'League client is not running.' }
  if (cmd.type === 'RefreshProfile') {
    pushProfile({ ...profile, updatedAt: new Date().toISOString() })
    return { requestId: cmd.requestId, success: true }
  }
  const m = profile.matches.find((x) => x.gameId === cmd.gameId)
  if (!m) return { requestId: cmd.requestId, success: false, error: 'That game is no longer in your match history.' }
  if (m.me.championId === 112 && !failedOnce.has(m.gameId)) {
    failedOnce.add(m.gameId)
    return { requestId: cmd.requestId, success: false, error: "League didn't send this game. Try again." }
  }
  const details = [demoDetails(m), ...profile.details.filter((d) => d.gameId !== m.gameId)].slice(0, ProfileMaxDetails)
  pushProfile({ ...profile, details, updatedAt: new Date().toISOString() })
  return { requestId: cmd.requestId, success: true }
}
