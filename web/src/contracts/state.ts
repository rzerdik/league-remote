// Mirror of src/LeagueRemote.Contracts/State.cs.

import type { SwapKind } from './commands'
import type { PostGameSummary } from './postGame'

/** ClientState: the game flow (StateUpdated). */
export interface ClientState {
  lcuConnected: boolean
  gameflowPhase: string
  readyCheck: ReadyCheckState | null
  lobby: LobbyState | null
  champSelect: ChampSelectState | null
  runePages: RunePage[]
  updatedAt: string
  /** Set while the gameflow phase is GameStart / InProgress / Reconnect (Riot Live Client Data API); null otherwise. */
  inGame: InGameState | null
  /** Queue ids the client offers right now; null until the client has listed its queues. */
  availableQueueIds: number[] | null
  /** Game invitations waiting for an answer; null when none or not read (champ select, in game). */
  invitations: ReceivedInvitation[] | null
  /** The post-game screen's summary while the phase is EndOfGame; null otherwise. */
  postGame: PostGameSummary | null
  /** The champions League recommends per lane, for the champion pickers' lane filter; null until the client answers. */
  championLanes: ChampionLanes | null
}

/** ChampionLanes: champion ids per lane. A champion can be listed under several lanes. */
export interface ChampionLanes {
  top: number[]
  jungle: number[]
  middle: number[]
  bottom: number[]
  utility: number[]
}

/** ReceivedInvitation: an invite to someone's party (answer with 'AcceptInvitation' / 'DeclineInvitation'). */
export interface ReceivedInvitation {
  id: string
  /** The inviter's Riot ID ("Name#TAG"); their legacy summoner name or "Someone" when unknown. */
  fromRiotId: string
  queueId: number
  /** The client's name for the queue, e.g. "Ranked Flex"; null when it doesn't list it. */
  queueName: string | null
  gameMode: string | null
  /** False when League won't let you join (a restriction); Decline still works. */
  canAccept: boolean
}

/** SentInvitation: someone this lobby invited who hasn't joined. */
export interface SentInvitation {
  puuid: string
  /** Their Riot ID ("Name#TAG"); their legacy summoner name or "Unknown" when unknown. */
  riotId: string
  /** "Requested" | "Pending" | "OnHold" | "Declined" | "Error" */
  state: string
}

/** InGameState: only the loaded flag and the game clock (scoreboard, objectives, events: LiveGameState). */
export interface InGameState {
  /** True once the game clock runs (the loading screen is over). */
  loaded: boolean
  /** In-game clock in seconds as last reported (0 while loading); republished every ~5 s, interpolate in between. */
  gameTime: number
  /** ISO UTC time at which `loaded` first became true for this game; null while loading. */
  startedAt: string | null
}

export interface ReadyCheckState {
  /** "InProgress", "EveryoneReady", "StrangerNotReady", "Invalid" */
  state: string
  /** "None", "Accepted", "Declined" */
  playerResponse: string
  /** Seconds elapsed since the ready check started (counts up to ~12). */
  timer: number
}

export interface LobbyState {
  /** -1 for custom games. */
  queueId: number
  isLeader: boolean
  canStartActivity: boolean
  isSearching: boolean
  timeInQueue: number | null
  estimatedQueueTime: number | null
  members: LobbyMember[]
  /** LCU game mode, e.g. "CLASSIC", "ARAM", "PRACTICETOOL"; null when unknown. */
  gameMode: string | null
  /** Custom game (incl. Practice Tool): its own queue (31xx); 'StartMatchmaking' starts it, there is no wait. */
  isCustom: boolean
  /** Draft Pick / Ranked Solo/Duo / Ranked Flex: everyone picks two roles; change yours with 'SetPositionPreferences'. */
  showPositionSelector: boolean
  /** You may invite (the leader, or anyone once invites are open) and there is room: 'SendInvitation'. */
  canInvite: boolean
  /** Invites sent from this lobby that haven't joined (pending or declined); null when none. */
  invitations: SentInvitation[] | null
}

export interface LobbyMember {
  /** Riot ID ("Name#TAG"); their legacy summoner name or "Unknown" when unknown. */
  riotId: string
  isLeader: boolean
  /** LCU role, e.g. "MIDDLE" (see Positions); null when not chosen. "UNSELECTED" may appear for the second one. */
  firstPositionPreference: string | null
  secondPositionPreference: string | null
  /** The player using this phone. */
  isLocalPlayer: boolean
  /** Own lobby only, for 'LoadPlayerProfile'; '' when the client doesn't say. */
  puuid: string
  /** Summoner icon (Data Dragon id); 0 when the client doesn't say. */
  profileIconId: number
}

export interface ChampSelectState {
  localPlayerCellId: number
  /** "PLANNING", "BAN_PICK", "FINALIZATION", "GAME_STARTING" */
  timerPhase: string
  /**
   * Ms left in the phase when the snapshot was taken (League's adjustedTimeLeftInPhase less the time since its
   * internalNowInEpochMs). A newer snapshot carries a smaller value; a republished one the same.
   */
  timeLeftMs: number
  myTeam: TeamMember[]
  myAction: PlayerAction | null
  /** Positive ids only. */
  pickableChampionIds: number[]
  /** Positive ids only; falls back to every champion not yet banned/picked when League reports none. */
  bannableChampionIds: number[]
  /** Enemy team as the client shows it: champion + position, never names. */
  theirTeam: EnemyMember[]
  /** Completed bans of both teams. */
  bans: ChampSelectBans
  /** Pick-order swaps, position swaps and champion trades with teammates (by cellId). */
  swaps: SwapOffer[]
  /** The draft in order, one entry per LCU action group. Nothing is in progress during PLANNING. */
  turns: DraftTurn[]
  /** "blue" | "red" | "" (unknown). */
  mySide: string
  /** The phase has no time limit (some custom games): don't show a countdown. */
  isTimerInfinite: boolean
  /** Players pick from a dealt subset of champions: the timer ticks softly for everyone in the last 5 s. */
  allowSubsetChampionPicks: boolean
  /**
   * The mode has a champion bench (ARAM): everyone is dealt a champion (myTeam[].championId, often without any pick
   * action) and can take one from the shared bench instead. No pick grid, no Lock In.
   */
  benchEnabled: boolean
  /** The bench in the client's order (positive ids, ≤ 10); null when benchEnabled is false. */
  benchChampionIds: number[] | null
  /** The client takes bench swaps right now (FINALIZATION; also BAN_PICK with subset picks). */
  canBenchSwap: boolean
  /** The mode offers rerolls: show the Reroll button (with rerollsRemaining, disabled unless canReroll). */
  allowRerolling: boolean
  /** Your rerolls left; 0 without rerolls. */
  rerollsRemaining: number
  /** 'Reroll' would be accepted now: rerolls left, FINALIZATION, no champion trade in progress. */
  canReroll: boolean
  /**
   * Your equippable skins once your champion is yours (locked in, or dealt in ARAM: TeamMember.isLocked) and the mode
   * allows skins; null before.
   */
  skins: SkinSelection | null
  /** 'DodgeChampSelect' would be accepted now: a matchmade draft (not a custom game) not yet starting. */
  canDodge: boolean
}

/**
 * SkinSelection: the client's skin carousel for your champion without disabled or locked skins (at most 100 skins, 16
 * chromas each, 300 chromas in all, ≤ 48 KB of JSON: later skins are left out first). Equip one with 'SelectSkin'.
 */
export interface SkinSelection {
  championId: number
  /** The skin or chroma equipped right now; 0 when the client says none. */
  selectedSkinId: number
  /** The default skin first. */
  skins: ChampionSkin[]
}

/** ChampionSkin. Data Dragon art: `{Champion}_{id % 1000}.jpg`. */
export interface ChampionSkin {
  id: number
  /** As the client shows it; the default skin is named after the champion. */
  name: string
  isBase: boolean
  /** Equippable chromas (or a tiered skin's forms). */
  chromas: SkinChroma[]
}

export interface SkinChroma {
  id: number
  name: string
  /** Up to two "#RRGGBB" swatch colours (two: split diagonally); empty for tiered forms. */
  colors: string[]
}

/** DraftTurn: one step of the draft; all its actions run at the same time. */
export interface DraftTurn {
  /** "ban" | "pick" | "reveal" (ban reveal) | raw LCU type */
  type: string
  /** "ally" | "enemy" | "both" (simultaneous bans, blind pick) | "none" (reveal) */
  team: string
  /** Cells acting in this turn (allies and enemies). */
  cellIds: number[]
  isInProgress: boolean
  completed: boolean
}

export interface EnemyMember {
  cellId: number
  /** Picked (or, during their turn, hovered) champion; 0 when none is visible. */
  championId: number
  championPickIntent: number
  /** "top", "jungle", "middle", "bottom", "utility" or "" when hidden. */
  assignedPosition: string
  /** Their pick action is completed. */
  isLocked: boolean
  /** Their pick is in progress right now. */
  isActing: boolean
  /** 1-based position of their pick in the whole draft; 0 when unknown. */
  pickOrder: number
}

export interface ChampSelectBans {
  myTeamBans: number[]
  theirTeamBans: number[]
}

/** "AVAILABLE", "BUSY", "INVALID", "RECEIVED", "SENT", "ACCEPTED", "DECLINED", "CANCELLED" */
export type SwapState = string

export interface SwapOffer {
  kind: SwapKind
  /** Id for the RequestSwap / AcceptSwap / DeclineSwap / CancelSwap commands. */
  id: number
  /** The teammate this offer is with. */
  cellId: number
  state: SwapState
}

export interface TeamMember {
  cellId: number
  isLocalPlayer: boolean
  /** Locked champion, or, while this player is picking, the one they hover (see isLocked). */
  championId: number
  championPickIntent: number
  assignedPosition: string
  spell1Id: number
  spell2Id: number
  /** Pick completed (bench modes: a champion was dealt, unless they still have a pick open). */
  isLocked: boolean
  /** Has a pick or ban in progress right now. */
  isActing: boolean
  /** 1-based position of their pick in the whole draft; 0 when unknown. */
  pickOrder: number
  /** Champion they ban (hovered while banning, then locked); 0 when none. */
  banChampionId: number
  hasBanned: boolean
  /**
   * For 'LoadPlayerProfile': set only while League shows who this teammate is (not in an anonymous ranked draft); ''
   * otherwise.
   */
  puuid: string
  /**
   * Their Riot ID ("Name#TAG"), under the same rule as `puuid`: only while League shows who they are; '' in an
   * anonymous draft. Own team only.
   */
  riotId: string
}

export interface PlayerAction {
  id: number
  /** "pick", "ban", ... */
  type: string
  championId: number
  isInProgress: boolean
  completed: boolean
}

export interface RunePage {
  id: number
  name: string
  isCurrent: boolean
  isEditable: boolean
}
