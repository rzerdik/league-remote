// Mirror of src/LeagueRemote.Contracts/Commands.cs.

import type { AutoPickQueueGroup, AutoPickRole } from './autoPick'

export type CommandType =
  | 'RefreshState'
  | 'AcceptReadyCheck'
  | 'DeclineReadyCheck'
  | 'CreateLobby'
  | 'StartMatchmaking'
  | 'CancelMatchmaking'
  /** Needs championId and actionId (ChampSelectState.myAction.id): the pick or ban to hover in. */
  | 'HoverChampion'
  /** Needs championId and actionId, as HoverChampion; that action must be in progress. */
  | 'LockChampion'
  | 'SetSummonerSpells'
  | 'SelectRunePage'
  /** No params. Creates a Practice Tool lobby (queue 3140); start it with 'StartMatchmaking'. */
  | 'CreatePracticeTool'
  /** Swap/trade commands: need swapKind + swapId (from ChampSelectState.swaps). */
  | 'RequestSwap'
  | 'AcceptSwap'
  | 'DeclineSwap'
  | 'CancelSwap'
  /** Lobby with LobbyState.showPositionSelector: needs firstPosition, and secondPosition unless the first is 'FILL'. */
  | 'SetPositionPreferences'
  /** No params. Leaves the lobby (the client's ✕ next to Find Match); refused while searching (Cancel first). */
  | 'LeaveLobby'
  /** Needs conversationId and body (1–ChatMaxMessageLength chars, one line). */
  | 'SendChatMessage'
  /** Needs conversationId. */
  | 'MarkConversationRead'
  /** Needs invitationId (ReceivedInvitation.id). Joins that party; refused while searching, in champ select or in game. */
  | 'AcceptInvitation'
  /** Needs invitationId. */
  | 'DeclineInvitation'
  /** Needs puuid of a friend. Refused without a lobby, while searching, when full or not allowed (LobbyState.canInvite). */
  | 'SendInvitation'
  /** Needs friendId (Friend.id). Removes the friend in the client; confirm first. */
  | 'RemoveFriend'
  /** Needs puuid (a friend's). Opens (creates if needed) the DM with that friend; it then shows up with that `puuid`. */
  | 'OpenDirectConversation'
  /** Needs gameName + tagLine (validateGameName / validateTagLine). Sends a friend request by Riot ID. */
  | 'SendFriendRequest'
  /** Needs puuid of an incoming FriendRequest. */
  | 'AcceptFriendRequest'
  /** Needs puuid of an incoming FriendRequest. */
  | 'DeclineFriendRequest'
  /** Needs puuid of an outgoing FriendRequest. Withdraws it. */
  | 'CancelFriendRequest'
  /** No params. Send when the Profile tab opens: rereads the profile at most every ProfileRefreshIntervalMs, else republishes. */
  | 'RefreshProfile'
  /**
   * Needs gameId of a game in ProfileState.matches; the game then shows up first in ProfileState.details. With puuid: a
   * game in that PlayerProfile.matches (open the profile first), into its details; the player is re-checked.
   */
  | 'LoadMatchDetails'
  /** Needs enabled. Auto pick on/off (saved on the PC); off cancels a pending auto action. Works without a League client. */
  | 'SetAutoPickEnabled'
  /**
   * Needs queueGroup + role (aram: 'ANY' only). Replaces that preset with banChampionIds (≤ AutoPickMaxBans),
   * pickChampionIds (≤ AutoPickMaxPicks; distinct positive ids, none for aram), spell1Id + spell2Id (both or neither) and
   * runePageId (optional). Nothing set removes the preset. Works without a League client.
   */
  | 'SetAutoPickPreset'
  /** No params. No automation in this (or the next) champ select; cancels a pending auto action. */
  | 'PauseAutoPick'
  /** No params. Lifts PauseAutoPick. */
  | 'ResumeAutoPick'
  /** Optional actionId (AutoPickPending.actionId). Cancels the pending auto ban / lock-in; the hover stays. */
  | 'CancelAutoPickAction'
  /**
   * Needs championId of a champion on the bench (ChampSelectState.benchChampionIds). Trades yours for it; refused unless
   * canBenchSwap and the bench still lists it.
   */
  | 'BenchSwap'
  /** No params. Rerolls your champion (the old one goes to the bench); refused unless ChampSelectState.canReroll. */
  | 'Reroll'
  /** Needs skinId: a skin or chroma in ChampSelectState.skins. Equips it; the connector refuses one you can't equip now. */
  | 'SelectSkin'
  /**
   * Needs puuid of a friend, a lobby member (LobbyMember.puuid), a champ-select teammate League names
   * (TeamMember.puuid), a named teammate in a game (MatchParticipant.puuid: your games, a player's games, the post-game
   * screen) or a player opened that way or found with 'SearchPlayer'; the connector re-checks and refuses anyone else.
   * Their profile then shows up first in ProfileState.players.
   */
  | 'LoadPlayerProfile'
  /** No params. The post-game Play Again (back to a lobby, same queue and party); refused outside EndOfGame or when !postGame.canPlayAgain. */
  | 'PlayAgain'
  /** No params. Leaves the post-game screen for Home (the client's close button); refused outside EndOfGame. */
  | 'LeavePostGame'
  /**
   * No params. Dodges champion select (the draft ends for everyone; League's dodge penalties apply); refused unless
   * ChampSelectState.canDodge. The phone confirms first.
   */
  | 'DodgeChampSelect'
  /**
   * Needs gameName + tagLine (a Riot ID, as for SendFriendRequest). The connector looks it up (any case) and loads that
   * player's profile first into ProfileState.players, under League's spelling; refused when nobody has that Riot ID or
   * it is your own.
   */
  | 'SearchPlayer'

/** SwapKind */
export type SwapKind = 'PickOrder' | 'Position' | 'Trade'
export const SwapKinds: readonly SwapKind[] = ['PickOrder', 'Position', 'Trade']

/** Positions: lobby roles as the LCU names them, in the client's order. */
export type Position = 'TOP' | 'JUNGLE' | 'MIDDLE' | 'BOTTOM' | 'UTILITY' | 'FILL'
export const Positions: readonly Position[] = ['TOP', 'JUNGLE', 'MIDDLE', 'BOTTOM', 'UTILITY', 'FILL']
export function isPosition(v: string | null | undefined): v is Position {
  return !!v && (Positions as readonly string[]).includes(v)
}

export interface RemoteCommand {
  requestId: string
  type: CommandType
  queueId?: number | null
  championId?: number | null
  spell1Id?: number | null
  spell2Id?: number | null
  runePageId?: number | null
  swapKind?: SwapKind | null
  swapId?: number | null
  firstPosition?: Position | null
  secondPosition?: Position | 'UNSELECTED' | null
  /** Hover / Lock: the action shown on the phone (the connector refuses it once it is no longer the current one). */
  actionId?: number | null
  /** Chat commands: ChatConversation.id. */
  conversationId?: string | null
  /** SendChatMessage: the text. */
  body?: string | null
  /** AcceptInvitation / DeclineInvitation: ReceivedInvitation.id. */
  invitationId?: string | null
  /** Friend commands: Friend.id. */
  friendId?: string | null
  /**
   * A player: the friend to invite (SendInvitation) or message (OpenDirectConversation); Accept / Decline /
   * CancelFriendRequest: FriendRequest.puuid; LoadPlayerProfile (and LoadMatchDetails of their game): whose profile.
   */
  puuid?: string | null
  /** SendFriendRequest: the Riot ID to add; SearchPlayer: the one to look up (trimmed; tag line without '#'). */
  gameName?: string | null
  tagLine?: string | null
  /** LoadMatchDetails: MatchSummary.gameId (of ProfileState.matches, or with puuid of that PlayerProfile.matches). */
  gameId?: number | null
  /** SetAutoPickEnabled: the new AutoPickSettings.enabled. */
  enabled?: boolean | null
  /** SetAutoPickPreset: which preset (AutoPickQueueGroups / AutoPickRoles) and its lists in priority order. */
  queueGroup?: AutoPickQueueGroup | null
  role?: AutoPickRole | null
  banChampionIds?: number[] | null
  pickChampionIds?: number[] | null
  /** SelectSkin: ChampionSkin.id or SkinChroma.id from ChampSelectState.skins. */
  skinId?: number | null
}

export interface CommandResult {
  requestId: string
  success: boolean
  error?: string | null
}
