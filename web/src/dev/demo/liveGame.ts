// ?demo=game: a ranked game on Summoner's Rift at 14:00, as the connector publishes it every few seconds (LiveGameUpdated):
// the clock runs on and the scripted events below show up when the clock passes them (an enemy just died and respawns
// at 14:13, a kill at 14:05, a turret at 14:20, the Herald at 15:00, your death at 17:30, …, Victory at 30:00).
// `&at=<minutes>` starts elsewhere (e.g. `&at=23` for an ace, a Baron kill and a destroyed inhibitor); `&mode=aram` is
// ARAM (no objective timers, no buffs); `&live=off` sends no live data, as in the first moments of a game before the
// game's own API answers (the clock card only); `&spectate=1` is a spectator's view: no `me`, no team is yours, nobody
// named, and the first turret's killer unknown (side '').
import type { LiveEvent, LiveGameState, LivePlayer, LiveTeam } from '../../contracts'
import { store } from '../../store'
import { held, params, push } from './core'
import { KESTREL, LUMEN, ME, NIGHTOWL, VEXA } from './people'

export const liveAram = params.get('mode') === 'aram'
export const liveOff = params.get('live') === 'off'
const liveSpectate = params.get('spectate') === '1'
/** Spectating, the connector can't tell whose team is whose: nobody is named, no team is yours. */
const named = (c: DemoChamp | null) => (liveSpectate ? '' : (c?.gameName ?? ''))
export const liveStart = Math.round((Number(params.get('at')) || 14) * 60)
/** How often the fake connector republishes (the real one: every few seconds). */
const LIVE_PUBLISH_MS = 3000

interface DemoChamp {
  id: number
  name: string
  position: string
  gameName?: string
  /** CS per minute and vision per minute. */
  cs: number
  vision: number
  /** Levels ahead (+) or behind (-) of the average. */
  level: number
  spells: [string, string]
  build: number[]
  runes: [number, number, number]
}

const ORDER_CHAMPS: DemoChamp[] = [
  { id: 86, name: 'Garen', position: 'TOP', gameName: NIGHTOWL.gameName, cs: 6.8, vision: 0.5, level: 1, spells: ['SummonerFlash', 'SummonerTeleport'], build: [3071, 3047, 3053, 6333, 3742, 3065], runes: [8010, 8000, 8400] },
  { id: 64, name: 'Lee Sin', position: 'JUNGLE', gameName: KESTREL.gameName, cs: 5.4, vision: 0.9, level: 0, spells: ['SummonerFlash', 'SummonerSmite'], build: [3071, 3111, 3053, 6333, 3026, 3156], runes: [8010, 8000, 8100] },
  { id: 103, name: 'Ahri', position: 'MIDDLE', gameName: ME.gameName, cs: 8.1, vision: 0.6, level: 1, spells: ['SummonerFlash', 'SummonerDot'], build: [6655, 3020, 4645, 3089, 3157, 3135], runes: [8112, 8100, 8200] },
  { id: 51, name: 'Caitlyn', position: 'BOTTOM', gameName: LUMEN.gameName, cs: 8.4, vision: 0.5, level: 0, spells: ['SummonerFlash', 'SummonerHeal'], build: [6672, 3006, 3031, 3094, 3036, 3072], runes: [8008, 8000, 8300] },
  { id: 412, name: 'Thresh', position: 'UTILITY', gameName: VEXA.gameName, cs: 1.1, vision: 2.1, level: -2, spells: ['SummonerFlash', 'SummonerExhaust'], build: [3865, 3158, 3190, 3107, 3222, 3050], runes: [8439, 8400, 8300] },
]
const CHAOS_CHAMPS: DemoChamp[] = [
  { id: 266, name: 'Aatrox', position: 'TOP', cs: 7.2, vision: 0.4, level: 1, spells: ['SummonerFlash', 'SummonerTeleport'], build: [6692, 3047, 3071, 6333, 3053, 3026], runes: [8010, 8000, 8400] },
  { id: 254, name: 'Vi', position: 'JUNGLE', cs: 5.0, vision: 0.8, level: 0, spells: ['SummonerFlash', 'SummonerSmite'], build: [6692, 3111, 3071, 6333, 3053, 3156], runes: [8010, 8000, 8100] },
  { id: 238, name: 'Zed', position: 'MIDDLE', cs: 7.6, vision: 0.5, level: 0, spells: ['SummonerFlash', 'SummonerDot'], build: [3142, 3158, 6694, 3814, 6692, 3156], runes: [8112, 8100, 8300] },
  { id: 222, name: 'Jinx', position: 'BOTTOM', cs: 7.9, vision: 0.4, level: -1, spells: ['SummonerFlash', 'SummonerHeal'], build: [6672, 3006, 3031, 3094, 3036, 3046], runes: [8008, 8000, 8300] },
  { id: 89, name: 'Leona', position: 'UTILITY', cs: 1.0, vision: 1.9, level: -2, spells: ['SummonerFlash', 'SummonerDot'], build: [3190, 3047, 3109, 3068, 3075, 3050], runes: [8439, 8400, 8300] },
]
/** ARAM's Mark (SummonerSnowball) isn't a Summoner's Rift spell; Data Dragon still has its art (ddragon.allSpells). */
const ARAM_SPELLS: [string, string] = ['SummonerFlash', 'SummonerSnowball']
const SPELL_NAMES: Record<string, string> = {
  SummonerFlash: 'Flash',
  SummonerTeleport: 'Teleport',
  SummonerSmite: 'Smite',
  SummonerDot: 'Ignite',
  SummonerHeal: 'Heal',
  SummonerExhaust: 'Exhaust',
  SummonerSnowball: 'Mark',
}

const champById = (id: number) => [...ORDER_CHAMPS, ...CHAOS_CHAMPS].find((c) => c.id === id)!
const sideOf = (id: number) => (ORDER_CHAMPS.some((c) => c.id === id) ? 'ORDER' : 'CHAOS')

type ScriptedExtra = Partial<LiveEvent> & { victim?: number; assists?: number[] }
/** A scripted event: [time, kind, actor champion (0 = a team: give `side`), extra]. Sides follow the actor. */
type Scripted = [number, string, number, ScriptedExtra?]
const TIMELINE: Scripted[] = [
  [185, 'kill', 103, { victim: 238, assists: [64] }],
  [185, 'firstBlood', 103],
  [340, 'kill', 254, { victim: 86 }],
  [372, 'dragon', 64, { dragonType: 'Fire' }],
  [500, 'grub', 254],
  [512, 'grub', 254],
  [524, 'grub', 254],
  [595, 'kill', 222, { victim: 51, assists: [89] }],
  [601, 'kill', 64, { victim: 222, assists: [412] }],
  [630, 'turret', 266, { lane: 'top' }],
  [630, 'firstTower', 266],
  [675, 'dragon', 254, { dragonType: 'Water', stolen: true }],
  [760, 'kill', 103, { victim: 238 }],
  [764, 'kill', 103, { victim: 222, assists: [412] }],
  [764, 'multikill', 103, { killStreak: 2 }],
  [800, 'kill', 89, { victim: 412, assists: [254] }],
  [828, 'kill', 86, { victim: 266, assists: [64] }],
  [845, 'kill', 51, { victim: 254, assists: [412, 64] }],
  [860, 'turret', 51, { lane: 'bot' }],
  [935, 'herald', 64],
  [990, 'dragon', 64, { dragonType: 'Hextech' }],
  [1050, 'kill', 238, { victim: 103 }],
  [1062, 'kill', 64, { victim: 238, assists: [86] }],
  [1120, 'turret', 266, { lane: 'top' }],
  [1290, 'dragon', 64, { dragonType: 'Hextech' }],
  [1300, 'kill', 103, { victim: 89, assists: [412] }],
  [1304, 'kill', 51, { victim: 222 }],
  [1309, 'kill', 86, { victim: 254, assists: [64] }],
  [1312, 'kill', 64, { victim: 266 }],
  [1315, 'kill', 103, { victim: 238, assists: [51, 412] }],
  [1315, 'ace', 0, { side: 'ORDER' }],
  [1330, 'baron', 64],
  [1360, 'turret', 103, { lane: 'mid' }],
  [1385, 'inhibitor', 0, { side: 'ORDER', lane: 'mid' }],
  [1590, 'dragon', 64, { dragonType: 'Hextech' }],
  [1685, 'inhibitorRespawned', 0, { side: 'CHAOS', lane: 'mid' }],
  [1740, 'turret', 51, { lane: 'mid' }],
  [1800, 'gameEnd', 0, { result: 'win' }],
]
const ARAM_KINDS = new Set(['kill', 'firstBlood', 'multikill', 'ace', 'turret', 'firstTower', 'inhibitor', 'gameEnd'])

/** Seconds a champion of this level stays dead (roughly the game's curve before 15:00). */
const respawnDuration = (level: number) => 6 + level * 2.5

function liveEvents(t: number): LiveEvent[] {
  return TIMELINE.filter(([time, kind]) => time <= t && (!liveAram || ARAM_KINDS.has(kind))).map(([time, kind, actorId, extra = {}], i) => {
    const { victim = 0, assists = [], ...rest } = extra
    // Spectating, the first turret (and its first-tower event) comes from a killer the connector couldn't place.
    const unresolved = liveSpectate && time === 630 && (kind === 'turret' || kind === 'firstTower')
    if (unresolved) actorId = 0
    const side = unresolved ? '' : (rest.side ?? (actorId ? sideOf(actorId) : ''))
    const actor = actorId ? champById(actorId) : null
    const target = victim ? champById(victim) : null
    return {
      id: i + 1,
      time,
      kind,
      side: kind === 'gameEnd' ? '' : side,
      isMyTeam: !liveSpectate && side === 'ORDER',
      actorChampionId: actor?.id ?? 0,
      actorChampionName: actor?.name ?? '',
      // The connector names your own team only.
      actorGameName: named(actor),
      victimChampionId: target?.id ?? 0,
      victimChampionName: target?.name ?? '',
      victimGameName: named(target),
      assistChampionIds: assists,
      killStreak: 0,
      dragonType: '',
      stolen: false,
      lane: '',
      result: '',
      ...rest,
      ...(unresolved ? { side: '' } : {}),
    } as LiveEvent
  })
}

function livePlayer(c: DemoChamp, t: number, kills: LiveEvent[]): LivePlayer {
  const level = Math.max(1, Math.min(18, Math.floor(1 + t / 80) + c.level))
  const deaths = kills.filter((e) => e.victimChampionId === c.id)
  const lastDeath = deaths[deaths.length - 1]
  const respawnAt = lastDeath ? lastDeath.time + respawnDuration(level) : 0
  const built = Math.min(6, Math.floor(t / 330))
  const [spell1, spell2] = liveAram ? ARAM_SPELLS : c.spells
  return {
    championId: c.id,
    championName: c.name,
    isMe: !liveSpectate && c.id === 103,
    isBot: false,
    gameName: named(c),
    tagLine: named(c) ? 'EUW' : '',
    position: liveAram ? '' : c.position,
    level,
    kills: kills.filter((e) => e.actorChampionId === c.id).length,
    deaths: deaths.length,
    assists: kills.filter((e) => e.assistChampionIds.includes(c.id)).length,
    creepScore: Math.floor((c.cs * t) / 600) * 10,
    wardScore: liveAram ? 0 : Math.round(((c.vision * t) / 60) * 10) / 10,
    items: [
      ...c.build.slice(0, built).map((itemId, slot) => ({ itemId, slot, count: 1 })),
      ...(built < 6 ? [{ itemId: c.position === 'UTILITY' ? 2055 : 2003, slot: built, count: 2 }] : []),
      ...(liveAram ? [] : [{ itemId: c.position === 'UTILITY' ? 3364 : 3340, slot: 6, count: 1 }]),
    ],
    spell1: { id: spell1, name: SPELL_NAMES[spell1] ?? spell1 },
    spell2: { id: spell2, name: SPELL_NAMES[spell2] ?? spell2 },
    keystone: c.runes[0],
    primaryStyle: c.runes[1],
    subStyle: c.runes[2],
    isDead: respawnAt > t,
    respawnSeconds: respawnAt > t ? Math.round((respawnAt - t) * 10) / 10 : 0,
  }
}

function liveTeam(side: 'ORDER' | 'CHAOS', champs: DemoChamp[], events: LiveEvent[], t: number): LiveTeam {
  const ours = events.filter((e) => e.side === side)
  const count = (kind: string) => ours.filter((e) => e.kind === kind).length
  const kills = events.filter((e) => e.kind === 'kill')
  return {
    side,
    isMyTeam: !liveSpectate && side === 'ORDER',
    kills: count('kill'),
    towers: count('turret'),
    inhibitors: count('inhibitor'),
    dragons: ours.filter((e) => e.kind === 'dragon' && e.dragonType !== 'Elder').map((e) => e.dragonType),
    elders: ours.filter((e) => e.kind === 'dragon' && e.dragonType === 'Elder').length,
    barons: count('baron'),
    heralds: count('herald'),
    grubs: count('grub'),
    players: champs.map((c) => livePlayer(c, t, kills)),
  }
}

/** The timers the connector infers from the events (LiveGameRules), simplified for the demo. */
function liveTimers(events: LiveEvent[], t: number): LiveGameState['timers'] {
  if (liveAram) return null
  const last = (kind: string) => events.filter((e) => e.kind === kind).pop()
  const drakes = events.filter((e) => e.kind === 'dragon')
  const elder = ['ORDER', 'CHAOS'].some((side) => drakes.filter((e) => e.side === side && e.dragonType !== 'Elder').length >= 4)
  const lastDrake = drakes[drakes.length - 1]
  const lastBaron = last('baron')
  return {
    dragon: {
      kind: elder ? 'elder' : 'dragon',
      spawnsAt: lastDrake ? lastDrake.time + (elder ? 360 : 300) : 300,
      despawnsAt: null,
      dragonType: elder ? 'Elder' : drakes.length >= 3 ? 'Hextech' : '',
    },
    baron: { kind: 'baron', spawnsAt: lastBaron ? lastBaron.time + 360 : 1200, despawnsAt: null, dragonType: '' },
    herald: { kind: 'herald', spawnsAt: last('herald') || t >= 1185 ? null : 900, despawnsAt: 1185, dragonType: '' },
    grubs: { kind: 'grubs', spawnsAt: events.filter((e) => e.kind === 'grub').length >= 3 || t >= 885 ? null : 480, despawnsAt: 885, dragonType: '' },
    inhibitors: events
      .filter((e) => e.kind === 'inhibitor' && e.time + 300 > t)
      .map((e) => ({ side: e.side === 'ORDER' ? 'CHAOS' : 'ORDER', lane: e.lane, respawnsAt: e.time + 300 })),
  }
}

/** The whole slice at game time `t` (the game ends at 30:00 with a win). */
function liveGameAt(t: number): LiveGameState {
  const all = liveEvents(t)
  const order = liveTeam('ORDER', ORDER_CHAMPS, all, t)
  const chaos = liveTeam('CHAOS', CHAOS_CHAMPS, all, t)
  const ended = all.find((e) => e.kind === 'gameEnd')
  return {
    available: true,
    gameTime: ended ? ended.time : t,
    gameMode: liveAram ? 'ARAM' : 'CLASSIC',
    mapNumber: liveAram ? 12 : 11,
    me: liveSpectate ? null : { player: order.players.find((p) => p.isMe)!, currentGold: 380 + ((Math.floor(t) * 23) % 1450) },
    teams: [order, chaos],
    timers: liveTimers(all, t),
    events: all.slice(-30),
    result: ended ? 'win' : '',
    updatedAt: new Date().toISOString(),
  }
}

const liveOpenedAt = performance.now()

/** One fake publish: the clock where the demo is now (the ClientState clock follows, as the connector's does). */
function publishLiveGame() {
  const t = liveStart + (performance.now() - liveOpenedAt) / 1000
  const game = liveGameAt(Math.round(t * 10) / 10)
  held.liveGame = game
  store.liveGame = game
  store.receivedAt.liveGame = performance.now()
  const s = held.state
  if (s?.inGame) push({ ...s, inGame: { ...s.inGame, gameTime: game.gameTime } })
}

/** Publishes now and then every LIVE_PUBLISH_MS, like the connector during a game. */
export function startLiveGame() {
  publishLiveGame()
  setInterval(publishLiveGame, LIVE_PUBLISH_MS)
}
