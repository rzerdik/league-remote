// Mirror of src/LeagueRemote.Contracts/Friends.cs.

import type { FriendRequest } from './friendRequests'

/** FriendsState.MaxFriendIdLength */
export const FriendMaxIdLength = 256

/** Published as its own hub message (FriendsUpdated), throttled by the connector. */
export interface FriendsState {
  /** The user's own friend groups, in the client's order. */
  groups: FriendGroup[]
  /** Every friend, sorted by name. */
  friends: Friend[]
  /** Pending friend requests, incoming first, each direction by name. */
  requests: FriendRequest[]
  updatedAt: string
}

export interface FriendGroup {
  id: number
  /** '' for the default group (label it yourself). */
  name: string
  isDefault: boolean
  priority: number
}

/** FriendAvailability ('busy' = in queue / champ select / game). */
export type FriendAvailability = 'online' | 'away' | 'busy' | 'mobile' | 'offline'

/** FriendActivities ('' = idle, offline or not in League). */
export type FriendActivity = '' | 'lobby' | 'queue' | 'champ-select' | 'in-game' | 'spectating'

export interface Friend {
  /** `{puuid}@…pvp.net`: also the id of the direct conversation with them (ChatConversation.id), and RemoveFriend's friendId. */
  id: string
  puuid: string
  /** 0 when unknown. */
  summonerId: number
  gameName: string
  /** Without '#'. */
  tagLine: string
  groupId: number
  availability: FriendAvailability | string
  activity: FriendActivity | string
  /** Online in another Riot product ("Riot Client", "VALORANT"); '' for League or offline. */
  product: string
  queueId: number | null
  /** LCU game mode while in a game ("CLASSIC", "ARAM"); '' otherwise. */
  gameMode: string
  championId: number | null
  /** ISO time the activity began (the game's start while 'in-game'); null when unknown. */
  activitySince: string | null
  statusMessage: string
  /** The user's own note about this friend. */
  note: string
  profileIconId: number
  /** "GOLD" etc.; '' when unranked. */
  rankTier: string
  /** "I"–"IV"; '' for apex tiers / unranked. */
  rankDivision: string
  /** "RANKED_SOLO_5x5" | "RANKED_FLEX_SR" | ''. */
  rankQueue: string
  lastSeenOnline: string | null
}
