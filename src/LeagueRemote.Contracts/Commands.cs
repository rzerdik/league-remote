using System.Text.Json.Serialization;

namespace LeagueRemote.Contracts;

/// <summary>
/// The complete set of actions a phone may trigger. There is intentionally no generic
/// "call this LCU endpoint" command: anything not listed here is rejected.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandType>))]
public enum CommandType
{
    /// <summary>Ask the connector to republish the full state.</summary>
    RefreshState,
    AcceptReadyCheck,
    DeclineReadyCheck,
    /// <summary>Requires <see cref="RemoteCommand.QueueId"/>.</summary>
    CreateLobby,
    /// <summary>
    /// Starts the current lobby through matchmaking (custom games, incl. Practice Tool, are queues too); only a legacy
    /// custom lobby without a queue id starts champ select directly (see <see cref="LobbyState.IsCustom"/>).
    /// </summary>
    StartMatchmaking,
    CancelMatchmaking,
    /// <summary>
    /// Requires <see cref="RemoteCommand.ChampionId"/> and <see cref="RemoteCommand.ActionId"/> (the local player's current
    /// action, <see cref="ChampSelectState.MyAction"/>). Hovers the champion in that pick or ban action.
    /// </summary>
    HoverChampion,
    /// <summary>
    /// Requires <see cref="RemoteCommand.ChampionId"/> and <see cref="RemoteCommand.ActionId"/> (as for
    /// <see cref="HoverChampion"/>). Hovers and completes that pick or ban action; it must be in progress.
    /// </summary>
    LockChampion,
    /// <summary>Requires <see cref="RemoteCommand.Spell1Id"/> and <see cref="RemoteCommand.Spell2Id"/>.</summary>
    SetSummonerSpells,
    /// <summary>Requires <see cref="RemoteCommand.RunePageId"/>.</summary>
    SelectRunePage,
    /// <summary>Creates a Practice Tool lobby (the client's Practice Tool queue, 3140). No parameters.</summary>
    CreatePracticeTool,
    /// <summary>Requires <see cref="RemoteCommand.SwapKind"/> and <see cref="RemoteCommand.SwapId"/>. Offers a swap/trade to a teammate (state AVAILABLE).</summary>
    RequestSwap,
    /// <summary>Requires <see cref="RemoteCommand.SwapKind"/> and <see cref="RemoteCommand.SwapId"/>. Accepts a received swap/trade (state RECEIVED).</summary>
    AcceptSwap,
    /// <summary>Requires <see cref="RemoteCommand.SwapKind"/> and <see cref="RemoteCommand.SwapId"/>. Declines a received swap/trade (state RECEIVED).</summary>
    DeclineSwap,
    /// <summary>Requires <see cref="RemoteCommand.SwapKind"/> and <see cref="RemoteCommand.SwapId"/>. Withdraws an offer the local player sent (state SENT).</summary>
    CancelSwap,
    /// <summary>
    /// Requires <see cref="RemoteCommand.FirstPosition"/> and, unless the first is <see cref="Positions.Fill"/>,
    /// <see cref="RemoteCommand.SecondPosition"/>. Sets the local player's two role preferences in a lobby with
    /// position selection (Draft Pick, Ranked Solo/Duo, Ranked Flex; see <see cref="LobbyState.ShowPositionSelector"/>).
    /// </summary>
    SetPositionPreferences,
    /// <summary>Leaves the current lobby (the client's ✕ next to Find Match); the client goes back to Play. No parameters.</summary>
    LeaveLobby,
    /// <summary>
    /// Requires <see cref="RemoteCommand.ConversationId"/> and <see cref="RemoteCommand.Body"/> (1–<see cref="ChatState.MaxMessageLength"/>
    /// characters, one line). Sends a chat message to a party, draft or direct conversation the client currently has.
    /// </summary>
    SendChatMessage,
    /// <summary>Requires <see cref="RemoteCommand.ConversationId"/>. Marks the conversation read in the client.</summary>
    MarkConversationRead,
    /// <summary>
    /// Requires <see cref="RemoteCommand.InvitationId"/> (see <see cref="ReceivedInvitation.Id"/>). Joins the inviting party, like
    /// the client's Accept; refused while searching, in champ select or in game.
    /// </summary>
    AcceptInvitation,
    /// <summary>Requires <see cref="RemoteCommand.InvitationId"/>. Declines a received invitation.</summary>
    DeclineInvitation,
    /// <summary>
    /// Requires <see cref="RemoteCommand.Puuid"/> of a friend. Invites them to the current lobby (what the client's Invite sends);
    /// refused without a lobby, while searching, when the party is full or the local player may not invite.
    /// </summary>
    SendInvitation,
    /// <summary>
    /// Requires <see cref="RemoteCommand.FriendId"/> (<see cref="Friend.Id"/>). Removes that friend in the client
    /// (<c>DELETE /lol-chat/v1/friends/{id}</c>); the phone asks for confirmation first.
    /// </summary>
    RemoveFriend,
    /// <summary>
    /// Requires <see cref="RemoteCommand.Puuid"/> (a friend's). Opens the direct conversation with that friend in the client,
    /// creating it when there is none (what the client's "Message" does), without sending anything or opening a chat window
    /// on the PC. The conversation then shows up in <see cref="ChatState"/> with <see cref="ChatConversation.Puuid"/> set.
    /// </summary>
    OpenDirectConversation,
    /// <summary>
    /// Requires <see cref="RemoteCommand.GameName"/> and <see cref="RemoteCommand.TagLine"/> (see <see cref="RiotIds"/>). Sends a
    /// friend request by Riot ID (<c>POST /lol-chat/v2/friend-requests</c>, what the client's Add Friend box sends).
    /// </summary>
    SendFriendRequest,
    /// <summary>Requires <see cref="RemoteCommand.Puuid"/> of an incoming <see cref="FriendRequest"/>. Accepts it.</summary>
    AcceptFriendRequest,
    /// <summary>Requires <see cref="RemoteCommand.Puuid"/> of an incoming <see cref="FriendRequest"/>. Declines it.</summary>
    DeclineFriendRequest,
    /// <summary>Requires <see cref="RemoteCommand.Puuid"/> of an outgoing <see cref="FriendRequest"/>. Withdraws it.</summary>
    CancelFriendRequest,
    /// <summary>
    /// No parameters. Asks the connector to reread the profile (summoner, ranks, mastery, match history) and republish
    /// <see cref="ProfileState"/>; the phone sends it when the Profile tab opens. Throttled to once per
    /// <see cref="ProfileState.RefreshInterval"/>: sooner, the connector republishes what it has and still succeeds.
    /// </summary>
    RefreshProfile,
    /// <summary>
    /// Requires <see cref="RemoteCommand.GameId"/> of a game in <see cref="ProfileState.Matches"/> (anything else is
    /// refused). Reads the full game (<c>GET /lol-match-history/v1/games/{gameId}</c>) and republishes
    /// <see cref="ProfileState"/> with it first in <see cref="ProfileState.Details"/>; already loaded games just move to the
    /// front without a new read. With <see cref="RemoteCommand.Puuid"/>: a game in that player's
    /// <see cref="PlayerProfile.Matches"/> (open their profile first), into <see cref="PlayerProfile.Details"/>; the player
    /// is re-checked like <see cref="LoadPlayerProfile"/>.
    /// </summary>
    LoadMatchDetails,
    /// <summary>
    /// Requires <see cref="RemoteCommand.Enabled"/>. Turns champ-select automation on or off (<see cref="AutoPickSettings.Enabled"/>,
    /// saved on the PC). Turning it off cancels a pending auto action (the hover stays). Works without a League client.
    /// </summary>
    SetAutoPickEnabled,
    /// <summary>
    /// Requires <see cref="RemoteCommand.QueueGroup"/> (<see cref="AutoPickQueueGroups"/>) and <see cref="RemoteCommand.Role"/>
    /// (<see cref="AutoPickRoles"/>; ARAM: "ANY" only). Replaces that one preset with <see cref="RemoteCommand.BanChampionIds"/>
    /// (≤ <see cref="AutoPickSettings.MaxBans"/>), <see cref="RemoteCommand.PickChampionIds"/> (≤ <see cref="AutoPickSettings.MaxPicks"/>;
    /// both distinct positive ids, empty or null for ARAM), <see cref="RemoteCommand.Spell1Id"/> + <see cref="RemoteCommand.Spell2Id"/>
    /// (both or neither, different) and <see cref="RemoteCommand.RunePageId"/> (optional); with none of them set the preset is
    /// removed. Saved on the PC; works without a League client. A champ select already running uses the new preset for the
    /// steps that haven't started yet.
    /// </summary>
    SetAutoPickPreset,
    /// <summary>
    /// No parameters. No automation writes in the current champ select (or the next one, when sent before it starts);
    /// cancels a pending auto action (the hover stays). See <see cref="AutoPickState.PausedForThisGame"/> for when it ends.
    /// </summary>
    PauseAutoPick,
    /// <summary>No parameters. Lifts <see cref="PauseAutoPick"/>; a step still in progress and untouched by the user is then automated.</summary>
    ResumeAutoPick,
    /// <summary>
    /// Optional <see cref="RemoteCommand.ActionId"/> (<see cref="AutoPickPending.ActionId"/>; when set, only that action is
    /// cancelled, and the command is refused once it is done). Cancels the pending auto ban / lock-in; the champion stays
    /// hovered and the automation leaves that action alone. Refused when nothing is pending.
    /// </summary>
    CancelAutoPickAction,
    /// <summary>
    /// Requires <see cref="RemoteCommand.ChampionId"/> of a champion on the bench (<see cref="ChampSelectState.BenchChampionIds"/>).
    /// Trades the local player's champion for it, like clicking it on the client's bench; the old one goes to the bench.
    /// Refused unless the bench is open (<see cref="ChampSelectState.CanBenchSwap"/>) and lists that champion right now.
    /// </summary>
    BenchSwap,
    /// <summary>
    /// No parameters. Rerolls the local player's champion (the client's Reroll button); the old one goes to the bench. Refused
    /// unless <see cref="ChampSelectState.CanReroll"/> (rerolls allowed and left, FINALIZATION, no trade in progress).
    /// </summary>
    Reroll,
    /// <summary>
    /// Requires <see cref="RemoteCommand.SkinId"/>: a skin or chroma in <see cref="ChampSelectState.Skins"/>. Equips it, as the
    /// client's skin carousel does (<c>PATCH /lol-champ-select/v1/session/my-selection { selectedSkinId }</c>). The connector
    /// re-reads the carousel and refuses anything the local player can't equip right now.
    /// </summary>
    SelectSkin,
    /// <summary>
    /// Requires <see cref="RemoteCommand.Puuid"/> of a friend, a member of the local player's lobby, a teammate in champ
    /// select whose name League shows (<see cref="TeamMember.Puuid"/>), a teammate the connector named in a game it published
    /// (<see cref="MatchParticipant.Puuid"/> in <see cref="ProfileState.Details"/>, a <see cref="PlayerProfile.Details"/> or
    /// <see cref="ClientState.PostGame"/>) or a player already in <see cref="ProfileState.Players"/> that way or by
    /// <see cref="SearchPlayer"/>. The connector re-checks its own games and League's lists and refuses anyone else,
    /// enemies included. Reads their summoner, ranks, mastery and
    /// match history and republishes <see cref="ProfileState"/> with it first in <see cref="ProfileState.Players"/>; a
    /// profile read less than <see cref="ProfileState.RefreshInterval"/> ago just moves to the front.
    /// </summary>
    LoadPlayerProfile,
    /// <summary>
    /// No parameters. The post-game screen's Play Again: back to a lobby for the same queue with the same party
    /// (<c>POST /lol-lobby/v2/play-again</c>, what rcp-fe-lol-postgame sends). Refused outside the EndOfGame phase and when
    /// the client doesn't offer it for this game (<see cref="PostGameSummary.CanPlayAgain"/>).
    /// </summary>
    PlayAgain,
    /// <summary>
    /// No parameters. Leaves the post-game screen for Home, like its close button
    /// (<c>POST /lol-end-of-game/v1/state/dismiss-stats</c>, what rcp-fe-lol-postgame's League scoreboard sends; only TFT's
    /// Go Home also declines Play Again). Refused outside the EndOfGame phase.
    /// </summary>
    LeavePostGame,
    /// <summary>
    /// No parameters. Dodges the current champ select: the local player quits it and the draft ends for everyone
    /// (<c>POST /lol-lobby-team-builder/champ-select/v1/session/quit</c>, <c>PostLolLobbyTeamBuilderChampSelectV1SessionQuit</c>).
    /// League applies its dodge penalties (a queue lockout; in ranked, LP). The phone asks for confirmation first. Refused
    /// outside champ select, in custom games and once the game is starting (see <see cref="ChampSelectState.CanDodge"/>).
    /// </summary>
    DodgeChampSelect,
    /// <summary>
    /// Requires <see cref="RemoteCommand.GameName"/> and <see cref="RemoteCommand.TagLine"/> (see <see cref="RiotIds"/>). Looks
    /// the Riot ID up (<c>GET /lol-summoner/v1/alias/lookup</c>, case-insensitive) and loads that player's profile like
    /// <see cref="LoadPlayerProfile"/>: first in <see cref="ProfileState.Players"/>, under the Riot ID as League spells it.
    /// Refused when nobody has that Riot ID or it is the local player's own.
    /// </summary>
    SearchPlayer,
    /// <summary>
    /// Requires <see cref="RemoteCommand.Enabled"/>. Turns Auto Accept on or off (<see cref="AutoPickSettings.AutoAccept"/>,
    /// saved on the PC). Turning it off cancels an accept still waiting out <see cref="AutoPickState.AutoAcceptDelay"/>. Works
    /// without a League client.
    /// </summary>
    SetAutoAcceptEnabled,
}

/// <summary>Which champ-select exchange a swap command targets.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SwapKind>))]
public enum SwapKind
{
    /// <summary>Swap pick order (<c>/lol-champ-select/v1/session/pick-order-swaps</c>).</summary>
    PickOrder,
    /// <summary>Swap assigned position (<c>/lol-champ-select/v1/session/position-swaps</c>).</summary>
    Position,
    /// <summary>Trade locked champions (<c>/lol-champ-select/v1/session/trades</c>).</summary>
    Trade,
}

/// <summary>
/// Flat command envelope (easy to produce from TypeScript). Only the fields the type needs are set; which ones each
/// <see cref="CommandType"/> requires is documented on it and checked by <see cref="CommandWhitelist"/>.
/// </summary>
/// <param name="RequestId">
/// The phone's id for this command (at most <see cref="CommandWhitelist.MaxRequestIdLength"/> characters), echoed in
/// <see cref="CommandResult.RequestId"/>.
/// </param>
/// <param name="Type">What to do; anything not in <see cref="CommandType"/> is refused.</param>
/// <param name="QueueId"><see cref="CommandType.CreateLobby"/>: the queue.</param>
/// <param name="ChampionId">Hover / Lock: the champion; <see cref="CommandType.BenchSwap"/>: the bench champion to take.</param>
/// <param name="Spell1Id">Summoner spell on D (<see cref="CommandType.SetSummonerSpells"/>, <see cref="CommandType.SetAutoPickPreset"/>).</param>
/// <param name="Spell2Id">Summoner spell on F, with <paramref name="Spell1Id"/>.</param>
/// <param name="RunePageId"><see cref="RunePage.Id"/> (<see cref="CommandType.SelectRunePage"/>, <see cref="CommandType.SetAutoPickPreset"/>).</param>
/// <param name="SwapKind">Swap commands: which exchange.</param>
/// <param name="SwapId">Swap commands: <see cref="SwapOffer.Id"/>.</param>
/// <param name="FirstPosition"><see cref="CommandType.SetPositionPreferences"/>: the first role (<see cref="Positions"/>).</param>
/// <param name="SecondPosition"><see cref="CommandType.SetPositionPreferences"/>: the second role; empty with <see cref="Positions.Fill"/>.</param>
/// <param name="ActionId">
/// Hover / Lock: the champ-select action the phone was showing (<see cref="PlayerAction.Id"/>). The command only applies to
/// that action, so a tap that races the end of a turn cannot land on the next one (a ban target hovered as your pick).
/// <see cref="CommandType.CancelAutoPickAction"/>: the pending auto action to cancel.
/// </param>
/// <param name="ConversationId">Chat commands: the LCU conversation id (<see cref="ChatConversation.Id"/>).</param>
/// <param name="Body"><see cref="CommandType.SendChatMessage"/>: the text.</param>
/// <param name="InvitationId">Accept / Decline: the received invitation (<see cref="ReceivedInvitation.Id"/>).</param>
/// <param name="FriendId"><see cref="CommandType.RemoveFriend"/>: <see cref="Friend.Id"/>.</param>
/// <param name="Puuid">
/// A player: the friend to invite (<see cref="CommandType.SendInvitation"/>) or to message
/// (<see cref="CommandType.OpenDirectConversation"/>), the friend request to accept / decline / cancel
/// (<see cref="FriendRequest.Puuid"/>), or whose profile to load (<see cref="CommandType.LoadPlayerProfile"/>, and
/// <see cref="CommandType.LoadMatchDetails"/> for one of their games).
/// </param>
/// <param name="GameName"><see cref="CommandType.SendFriendRequest"/>: the Riot ID to add; <see cref="CommandType.SearchPlayer"/>: the one to look up.</param>
/// <param name="TagLine">The tag line of that Riot ID, without '#'.</param>
/// <param name="GameId">
/// <see cref="CommandType.LoadMatchDetails"/>: <see cref="MatchSummary.GameId"/> of a game in <see cref="ProfileState.Matches"/>
/// (or, with <paramref name="Puuid"/>, in that <see cref="PlayerProfile.Matches"/>).
/// </param>
/// <param name="Enabled">
/// <see cref="CommandType.SetAutoPickEnabled"/>: the new <see cref="AutoPickSettings.Enabled"/>.
/// <see cref="CommandType.SetAutoAcceptEnabled"/>: the new <see cref="AutoPickSettings.AutoAccept"/>.
/// </param>
/// <param name="QueueGroup"><see cref="CommandType.SetAutoPickPreset"/>: which preset (<see cref="AutoPickQueueGroups"/>).</param>
/// <param name="Role"><see cref="CommandType.SetAutoPickPreset"/>: which preset (<see cref="AutoPickRoles"/>).</param>
/// <param name="BanChampionIds">
/// <see cref="CommandType.SetAutoPickPreset"/>: the ban list in priority order. Plain id lists, not nested objects: one
/// preset per command keeps the envelope flat and the payload small (≤ 15 ids); spells and rune page ride
/// <paramref name="Spell1Id"/> / <paramref name="Spell2Id"/> / <paramref name="RunePageId"/>.
/// </param>
/// <param name="PickChampionIds"><see cref="CommandType.SetAutoPickPreset"/>: the pick list in priority order.</param>
/// <param name="SkinId"><see cref="CommandType.SelectSkin"/>: <see cref="ChampionSkin.Id"/> or <see cref="SkinChroma.Id"/>.</param>
public sealed record RemoteCommand(
    string RequestId,
    CommandType Type,
    int? QueueId = null,
    int? ChampionId = null,
    int? Spell1Id = null,
    int? Spell2Id = null,
    long? RunePageId = null,
    SwapKind? SwapKind = null,
    long? SwapId = null,
    string? FirstPosition = null,
    string? SecondPosition = null,
    long? ActionId = null,
    string? ConversationId = null,
    string? Body = null,
    string? InvitationId = null,
    string? FriendId = null,
    string? Puuid = null,
    string? GameName = null,
    string? TagLine = null,
    long? GameId = null,
    bool? Enabled = null,
    string? QueueGroup = null,
    string? Role = null,
    IReadOnlyList<int>? BanChampionIds = null,
    IReadOnlyList<int>? PickChampionIds = null,
    int? SkinId = null);

/// <summary>Lobby role preferences as the LCU names them (<c>firstPositionPreference</c> / <c>secondPositionPreference</c>).</summary>
public static class Positions
{
    public const string Top = "TOP";
    public const string Jungle = "JUNGLE";
    public const string Middle = "MIDDLE";
    public const string Bottom = "BOTTOM";
    public const string Utility = "UTILITY";
    public const string Fill = "FILL";

    /// <summary>What the LCU stores for a preference that is not set (the second one when the first is <see cref="Fill"/>).</summary>
    public const string Unselected = "UNSELECTED";

    /// <summary>The roles a player can choose, in the order the client shows them.</summary>
    public static readonly IReadOnlyList<string> Selectable = [Top, Jungle, Middle, Bottom, Utility, Fill];

    public static bool IsSelectable(string? position) => position is not null && Selectable.Contains(position);
}

public sealed record CommandResult(string RequestId, bool Success, string? Error = null);
