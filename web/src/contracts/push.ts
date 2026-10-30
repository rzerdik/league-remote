// Mirror of src/LeagueRemote.Contracts/Push.cs.

/**
 * PushPreferences: which moments push to this phone. A missing field keeps its default (`defaultPushPreferences`), so
 * phones that never sent any get match found, your turn, game started and game invites.
 */
export interface PushPreferences {
  /** The ready check pops. */
  matchFound: boolean
  /** The gameflow enters champ select. Off by default. */
  champSelectStarted: boolean
  /** Your own pick or ban turn starts. */
  yourTurn: boolean
  /** The game has loaded. */
  gameStarted: boolean
  /** Someone invites you to a lobby. */
  lobbyInvite: boolean
  /** Someone sends you a friend request. Off by default. */
  friendRequest: boolean
}

/** PushPreferences.Default */
export const defaultPushPreferences: Readonly<PushPreferences> = {
  matchFound: true,
  champSelectStarted: false,
  yourTurn: true,
  gameStarted: true,
  lobbyInvite: true,
  friendRequest: false,
}
