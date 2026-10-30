// Mirror of src/LeagueRemote.Contracts/LiveGame.cs.

import type { MatchResult } from './profile'

/**
 * The running game (LiveGameUpdated), from Riot's Live Client Data API (`allgamedata`, read-only); never pushed. Enemies
 * only by champion: gameName / tagLine (and the events' …GameName) are set for your own team only, and only when it is
 * known (`me` not null) and has at most 5 players (LiveGameState.MaxTeamSize); '' otherwise.
 * All times (`gameTime`, event `time`, timers) are game-clock seconds; interpolate `gameTime` from `updatedAt`.
 */
export interface LiveGameState {
  /** True while the API answers with a game; false in the empty state sent when the game ends (or before it answers). */
  available: boolean
  /** In-game clock in seconds at `updatedAt`. */
  gameTime: number
  /** "CLASSIC", "ARAM", "CHERRY" (Arena), "PRACTICETOOL", "URF", "SWIFTPLAY", … */
  gameMode: string
  /** 11 Summoner's Rift, 12 Howling Abyss, 30 Arena, …; 0 when unknown. */
  mapNumber: number
  /** You; null when spectating or not matched (then no team is isMyTeam and nobody is named). */
  me: LiveActivePlayer | null
  /** Your team first, then the others (ORDER before CHAOS). Empty while unavailable. */
  teams: LiveTeam[]
  /** Null outside Summoner's Rift CLASSIC games (LiveGameRules.HasObjectiveTimers) and while unavailable. */
  timers: LiveObjectiveTimers | null
  /** Newest 30 (LiveGameState.MaxEvents) feed events, oldest first. Team counts and timers use the whole game, not just these. */
  events: LiveEvent[]
  /** 'win' | 'loss' once the game ended (GameEnd); '' before. */
  result: MatchResult | ''
  updatedAt: string
}

/** Your extras; `player` is the same line as the isMe entry in `teams`. */
export interface LiveActivePlayer {
  player: LivePlayer
  /** Unspent gold (only reported for you). */
  currentGold: number
}

/** LiveTeams: blue side ORDER (structures T1, minions T100), red side CHAOS (T2, T200). */
export const LiveTeams = { order: 'ORDER', chaos: 'CHAOS' } as const
export type LiveTeamSide = (typeof LiveTeams)[keyof typeof LiveTeams]

/** One team's scoreboard and objectives (derived from the whole event list). */
export interface LiveTeam {
  /** LiveTeams value (other strings passed through). */
  side: LiveTeamSide | string
  isMyTeam: boolean
  /** Sum of the players' kills (executions are nobody's). */
  kills: number
  /** Enemy turrets destroyed (also when minions finished them). */
  towers: number
  /** Enemy inhibitors destroyed. */
  inhibitors: number
  /** Elemental drakes slain, LiveDragonTypes values in kill order (four = soul). Elders are counted in `elders`. */
  dragons: string[]
  elders: number
  barons: number
  heralds: number
  /** Voidgrubs slain (one per grub, three per camp). */
  grubs: number
  /** In the API's order. */
  players: LivePlayer[]
}

/** One scoreboard line. Enemies: gameName / tagLine always ''. */
export interface LivePlayer {
  /** Numeric champion id (mapped by the connector from the internal name); 0 when unknown — show championName then. */
  championId: number
  /** The game's display name, in the game's language (e.g. "Wukong"). */
  championName: string
  isMe: boolean
  isBot: boolean
  /** Own team only; '' for enemies. */
  gameName: string
  /** Own team only, without '#'; '' for enemies. */
  tagLine: string
  /** 'TOP' | 'JUNGLE' | 'MIDDLE' | 'BOTTOM' | 'UTILITY'; '' when the API doesn't say (common outside ranked/draft). */
  position: string
  level: number
  kills: number
  deaths: number
  assists: number
  /** Minions + monsters (the API updates it in steps of 10). */
  creepScore: number
  /** Vision score (fractional). */
  wardScore: number
  /** Filled slots only, by slot. */
  items: LiveItem[]
  /** D */
  spell1: LiveSummonerSpell
  /** F */
  spell2: LiveSummonerSpell
  /** Keystone rune id; 0 when none. */
  keystone: number
  /** Primary rune tree id; 0 when none. */
  primaryStyle: number
  /** Secondary rune tree id; 0 when none. */
  subStyle: number
  isDead: boolean
  /** Seconds until respawn at `updatedAt`; 0 when alive. */
  respawnSeconds: number
}

/** An inventory slot. */
export interface LiveItem {
  /** Data Dragon item id. */
  itemId: number
  /** 0–5 inventory, 6 trinket. */
  slot: number
  /** Stack size (potions, wards); 1 for most items. */
  count: number
}

/** A summoner spell (the API sends names only). */
export interface LiveSummonerSpell {
  /** Data Dragon spell id ("SummonerFlash", "SummonerDot" = Ignite, …); '' when unknown. */
  id: string
  /** Display name in the game's language; show it when `id` is unknown. */
  name: string
}

/** LiveDragonTypes as the API names them: Fire = Infernal, Earth = Mountain, Water = Ocean, Air = Cloud. */
export const LiveDragonTypes = ['Fire', 'Earth', 'Water', 'Air', 'Hextech', 'Chemtech', 'Elder'] as const
export type LiveDragonType = (typeof LiveDragonTypes)[number]

/** LiveLanes: a structure's lane (the Nexus turrets count as mid). */
export type LiveLane = 'top' | 'mid' | 'bot'

/** Objective timers, on the game clock (count down against the interpolated gameTime). */
export interface LiveObjectiveTimers {
  /** Drakes, then the Elder Dragon once a team has its soul (kind 'elder'). */
  dragon: LiveObjectiveTimer
  baron: LiveObjectiveTimer
  /** One spawn per game. */
  herald: LiveObjectiveTimer
  /** One camp of three per game. */
  grubs: LiveObjectiveTimer
  /** Destroyed inhibitors, soonest respawn first. */
  inhibitors: LiveInhibitorTimer[]
}

/** LiveObjectiveKinds */
export type LiveObjectiveKind = 'dragon' | 'elder' | 'baron' | 'herald' | 'grubs'

/**
 * When an epic monster is next up (inferred from the rules and kill events; the API doesn't say whether it is alive).
 * Up from `spawnsAt` until a kill event moves `spawnsAt` on.
 */
export interface LiveObjectiveTimer {
  kind: LiveObjectiveKind | string
  /** Game time it (re)spawns; at or before gameTime = up. Null = not again this game (Herald/grubs taken, or despawned). */
  spawnsAt: number | null
  /** Game time it leaves for good if untaken (Herald, grubs); null for the others. */
  despawnsAt: number | null
  /** Dragon only: the next drake's LiveDragonType when known (after the third drake; 'Elder' for kind 'elder'); '' otherwise. */
  dragonType: LiveDragonType | ''
}

/** A destroyed inhibitor until it respawns. */
export interface LiveInhibitorTimer {
  /** The team that owns it (whose base it is in). */
  side: LiveTeamSide | string
  lane: LiveLane | ''
  /** Game time it respawns. */
  respawnsAt: number
}

/**
 * LiveGameRules: spawn rules behind the timers, game-clock seconds. Current patch (26.x, checked Oct 2026 against the
 * League wiki; not yet confirmed live); revisit when Riot changes the map.
 */
export const LiveGameRules = {
  summonersRift: 11,
  dragonFirstSpawn: 5 * 60,
  dragonRespawn: 5 * 60,
  dragonSoulDrakes: 4,
  /** Elder 6:00 after the soul drake and after each Elder kill. */
  elderSpawnDelay: 6 * 60,
  grubsSpawn: 8 * 60,
  grubsDespawn: 14 * 60 + 45,
  grubsPerCamp: 3,
  heraldSpawn: 15 * 60,
  heraldDespawn: 19 * 60 + 45,
  baronSpawn: 20 * 60,
  baronRespawn: 6 * 60,
  inhibitorRespawn: 5 * 60,
} as const

/** LiveEventKinds, one per API event (GameStart, MinionsSpawning and unknown events are left out). */
export type LiveEventKind =
  /** ChampionKill */
  | 'kill'
  /** FirstBlood (right after its kill; actor = the recipient) */
  | 'firstBlood'
  /** killStreak 2–5 */
  | 'multikill'
  /** A team killed all five enemies; side = the acing team */
  | 'ace'
  /** A drake or the Elder (dragonType) */
  | 'dragon'
  | 'baron'
  | 'herald'
  /** One Voidgrub (three per camp) */
  | 'grub'
  | 'turret'
  /** FirstBrick (right after its turret) */
  | 'firstTower'
  | 'inhibitor'
  /** side = the inhibitor's owner */
  | 'inhibitorRespawned'
  /** result = your result */
  | 'gameEnd'

/**
 * One feed line. Players appear as champions; …GameName only for your own team. A kill by a turret or minion has no
 * actor champion (actorChampionId 0).
 */
export interface LiveEvent {
  /** EventID: unique and increasing within a game (list key). */
  id: number
  /** Game-clock seconds. */
  time: number
  kind: LiveEventKind | string
  /** The team that scored (structures: the destroyer; inhibitorRespawned: the owner); '' when unknown and for gameEnd. */
  side: LiveTeamSide | string
  /** `side` is your team. */
  isMyTeam: boolean
  /** Killer / recipient / acer / objective or structure killer; 0 when none or not a champion. */
  actorChampionId: number
  actorChampionName: string
  /** Own team only; '' otherwise. */
  actorGameName: string
  /** Kills only; 0 otherwise. */
  victimChampionId: number
  victimChampionName: string
  /** Own team only; '' otherwise. */
  victimGameName: string
  /** Assisting champions (ids only). */
  assistChampionIds: number[]
  /** Multikill: 2 double … 5 penta; 0 otherwise. */
  killStreak: number
  /** Dragon events: LiveDragonType; '' otherwise. */
  dragonType: LiveDragonType | ''
  /** Dragon, Baron, Herald, grub: stolen. */
  stolen: boolean
  /** Turret / inhibitor events; '' otherwise. */
  lane: LiveLane | ''
  /** gameEnd: 'win' | 'loss'; '' otherwise. */
  result: MatchResult | ''
}
