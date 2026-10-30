// Mirror of src/LeagueRemote.Contracts/FriendRequests.cs.

/** FriendRequestDirections */
export type FriendRequestDirection = 'incoming' | 'outgoing'

/** A pending request; the request commands take its puuid. */
export interface FriendRequest {
  puuid: string
  direction: FriendRequestDirection | string
  gameName: string
  /** Without '#'. */
  tagLine: string
  /** 0 when League sends none (always for outgoing). */
  profileIconId: number
  note: string
}
