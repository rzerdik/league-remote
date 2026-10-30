// Mirror of src/LeagueRemote.Contracts/PostGame.cs.

import type { MatchParticipant, MatchResult } from './profile'

/** PostGameSummary: the client's post-game screen (EndOfGame). Names for your own team only. */
export interface PostGameSummary {
  gameId: number
  /** 0 when unknown. */
  queueId: number
  gameMode: string
  /** 0 when unknown. */
  mapId: number
  durationSeconds: number
  /** 'remake', 'win' or 'loss' (Arena: a top-four placement wins). */
  result: MatchResult
  isRanked: boolean
  /** The LP this game gave; null for unranked games or until the ranked service reports it. */
  rank: PostGameRank | null
  /** Mastery your champion earned; null until (or unless) the client reports it. */
  mastery: PostGameMastery | null
  /** Your team first (Arena: one per duo, yours first, then by placement). */
  teams: PostGameTeam[]
  /** The client offers Play Again for this game. */
  canPlayAgain: boolean
}

/** One team (Arena: one duo). Totals are the players' added up. */
export interface PostGameTeam {
  teamId: number
  isMyTeam: boolean
  win: boolean
  kills: number
  deaths: number
  assists: number
  gold: number
  /** Enemies: gameName / tagLine / puuid always ''. Runes are 0 when the client's stats don't carry them. */
  participants: MatchParticipant[]
}

/** PostGameRankChanges */
export type PostGameRankChange = '' | 'promoted' | 'demoted'

/** The LP change (/lol-ranked/v1/current-lp-change-notification). Tier / division / LP after the game. */
export interface PostGameRank {
  queueType: string
  /** '' while unranked. */
  tier: string
  /** '' for apex tiers and while unranked. */
  division: string
  leaguePoints: number
  /** Negative for a loss; 0 when the game didn't count. */
  leaguePointsDelta: number
  provisionalGamesRemaining: number
  change: PostGameRankChange
}

/** Your champion's mastery from this game. */
export interface PostGameMastery {
  championId: number
  pointsGained: number
  level: number
  leveledUp: boolean
  /** '' when none. */
  grade: string
  pointsUntilNextLevel: number
}
