// Mirror of src/LeagueRemote.Contracts/Profile.cs.

/** ProfileState.MaxDetails */
export const ProfileMaxDetails = 3
/** ProfileState.RefreshInterval: 'RefreshProfile' rereads the client at most this often. */
export const ProfileRefreshIntervalMs = 30_000
/** ProfileState.MaxPlayers / MaxPlayerDetails: other players' profiles kept, and games per player. */
export const ProfileMaxPlayers = 3
export const ProfileMaxPlayerDetails = 1

/**
 * The local player's profile (ProfileUpdated), never pushed, plus the friends', party members' and teammates' profiles
 * the phone opened (`players`). In game details only the own team has names (enemies' gameName / tagLine are always '').
 */
export interface ProfileState {
  /** Null until the client has a logged-in summoner. */
  summoner: ProfileSummoner | null
  /** Solo/Duo then Flex (also when unranked), then other non-TFT queues that have a tier. Empty when unavailable. */
  ranks: RankedQueueStats[]
  mastery: ProfileMastery
  /** Newest first, at most 20 (ProfileState.MaxMatches). */
  matches: MatchSummary[]
  /** Games loaded with 'LoadMatchDetails', most recently requested first, at most ProfileMaxDetails; all are in `matches`. */
  details: MatchDetails[]
  updatedAt: string
  /**
   * Profiles opened with 'LoadPlayerProfile' (friends, party, your champ-select team, teammates named in a game) or found
   * with 'SearchPlayer', most recently opened first, at most ProfileMaxPlayers.
   */
  players: PlayerProfile[]
}

/**
 * Another player's profile. `matches[].me` is their line; in `details` (their games you opened, at most
 * ProfileMaxPlayerDetails) `isMe` / `isMyTeam` mean them and their team, and only their team is named — nobody but them
 * when you played that game on the other side.
 */
export interface PlayerProfile {
  puuid: string
  summoner: ProfileSummoner
  ranks: RankedQueueStats[]
  /** score = the sum of their champion levels. */
  mastery: ProfileMastery
  matches: MatchSummary[]
  details: MatchDetails[]
  loadedAt: string
}

/** From /lol-summoner/v1/current-summoner. */
export interface ProfileSummoner {
  gameName: string
  /** Without '#'. */
  tagLine: string
  level: number
  /** XP earned in the current level. */
  xpSinceLastLevel: number
  /** XP the current level needs in total (progress = xpSinceLastLevel / xpForNextLevel); 0 when unknown. */
  xpForNextLevel: number
  profileIconId: number
}

/** RankedQueueTypes */
export const RankedQueueTypes = { solo: 'RANKED_SOLO_5x5', flex: 'RANKED_FLEX_SR' } as const

/** One ranked queue. */
export interface RankedQueueStats {
  /** RankedQueueTypes.solo / .flex, or another LCU queue type passed through. */
  queueType: string
  /** "IRON" … "CHALLENGER"; '' when unranked. */
  tier: string
  /** "I"–"IV"; '' when unranked or apex. */
  division: string
  leaguePoints: number
  wins: number
  losses: number
  /** Still in placements. */
  isProvisional: boolean
  provisionalGamesRemaining: number
  /** Series progress, one char per game: 'W' won, 'L' lost, 'N' not played; '' when not in a series. */
  promos: string
  /** Highest this season in this queue; '' when none. */
  highestTier: string
  highestDivision: string
  /** Where last season ended; '' when unranked then. */
  previousSeasonEndTier: string
  previousSeasonEndDivision: string
}

/** Champion mastery. */
export interface ProfileMastery {
  /** Total mastery score (sum of champion levels). */
  score: number
  /** Champions with any mastery. */
  championCount: number
  /** By points, highest first, at most 10 (ProfileState.MaxTopChampions). */
  topChampions: ChampionMastery[]
}

export interface ChampionMastery {
  championId: number
  level: number
  points: number
  pointsSinceLastLevel: number
  /** Points still missing for the next level. */
  pointsUntilNextLevel: number
  /** Best grade this season ("S+"…); '' when none. */
  highestGrade: string
  /** ISO time; null when unknown. */
  lastPlayed: string | null
}

/** MatchResults, checked in this order: aborted (never really started), remake, win, loss. */
export type MatchResult = 'aborted' | 'remake' | 'win' | 'loss'

/** One game of the local player's match history (only their own line; teams come with MatchDetails). */
export interface MatchSummary {
  /** What 'LoadMatchDetails' takes. */
  gameId: number
  /** 420 Solo/Duo, 440 Flex, 3140 Practice Tool, …; 0 when none. */
  queueId: number
  /** "CLASSIC", "ARAM", "CHERRY" (Arena), "PRACTICETOOL", … */
  gameMode: string
  /** "MATCHED_GAME" | "CUSTOM_GAME" */
  gameType: string
  /** 11 Summoner's Rift, 12 Howling Abyss, 30 Arena, … */
  mapId: number
  /** ISO time the game was created. */
  startedAt: string
  durationSeconds: number
  result: MatchResult | string
  /** League's after-game role guess (TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY); '' when none. Unreliable: a hint at most. */
  position: string
  me: MatchParticipant
}

/** A full game. */
export interface MatchDetails {
  gameId: number
  /** The local player's team first, then the others by teamId (100 blue, 200 red). */
  teams: MatchTeam[]
}

export interface MatchTeam {
  /** 100 blue side, 200 red side. */
  teamId: number
  isMyTeam: boolean
  win: boolean
  /** Sum of the participants' kills. */
  kills: number
  /** Sum of the participants' gold earned. */
  gold: number
  towers: number
  inhibitors: number
  dragons: number
  barons: number
  heralds: number
  /** Voidgrubs. */
  grubs: number
  /** Champions banned, in pick-turn order; 0 = no ban that turn. */
  bans: number[]
  /** In the game's order (matchmade games: top, jungle, mid, bottom, support). */
  participants: MatchParticipant[]
}

/** One player's line. Enemies: gameName / tagLine / puuid always ''. No summoner ids at all. */
export interface MatchParticipant {
  championId: number
  isMe: boolean
  /** Own team only; '' for enemies. */
  gameName: string
  /** Own team only, without '#'; '' for enemies. */
  tagLine: string
  champLevel: number
  kills: number
  deaths: number
  assists: number
  /** Minions + monsters. */
  cs: number
  gold: number
  /** Damage to champions. */
  damage: number
  visionScore: number
  /** Always 7 item ids (the last is the trinket); 0 = empty slot. */
  items: number[]
  spell1Id: number
  spell2Id: number
  /** Keystone rune id (perk0); 0 when none. */
  keystone: number
  /** Primary rune tree id; 0 when none. */
  primaryStyle: number
  /** Secondary rune tree id; 0 when none. */
  subStyle: number
  /** Arena placement 1–8; 0 outside Arena. */
  placement: number
  /**
   * A named teammate's puuid, for 'LoadPlayerProfile' (tap their row). '' without a name (enemies), for you, and for the
   * player whose game it is.
   */
  puuid: string
}
