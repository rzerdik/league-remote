namespace LeagueRemote.Contracts;

/// <summary>
/// Everything the phone is allowed to see. Deliberately narrow: own lobby, own team in champ select plus what the
/// League client itself shows of the enemy team (champions, positions, bans; never names or account ids).
/// In-game: only whether the game has loaded and its clock (<see cref="InGameState"/>), read from Riot's official Live Client
/// Data API. The scoreboard, objectives and events of the running game are their own slice, <see cref="LiveGameState"/>.
/// </summary>
/// <param name="InGame">Set while the gameflow phase is GameStart / InProgress / Reconnect; null otherwise.</param>
/// <param name="AvailableQueueIds">
/// Ids of the queues the client offers right now (<c>queueAvailability</c> "Available"); null while the client hasn't
/// listed its queues.
/// </param>
/// <param name="Invitations">
/// Game invitations the local player received and has not answered (newest last); null when none or not read in this phase
/// (champ select and in game).
/// </param>
/// <param name="PostGame">The post-game screen's summary while the gameflow phase is EndOfGame; null otherwise (or until the client has it).</param>
/// <param name="ChampionLanes">The champions League recommends per lane, for the champion pickers' lane filter; null until the client answers.</param>
public sealed record ClientState(
    bool LcuConnected,
    string GameflowPhase,
    ReadyCheckState? ReadyCheck,
    LobbyState? Lobby,
    ChampSelectState? ChampSelect,
    IReadOnlyList<RunePage> RunePages,
    DateTimeOffset UpdatedAt,
    InGameState? InGame = null,
    IReadOnlyList<int>? AvailableQueueIds = null,
    IReadOnlyList<ReceivedInvitation>? Invitations = null,
    PostGameSummary? PostGame = null,
    ChampionLanes? ChampionLanes = null)
{
    public static ClientState Disconnected() =>
        new(false, "None", null, null, null, [], DateTimeOffset.UtcNow);
}

/// <summary>
/// Champion ids per lane, from League's <c>recommendedPositions</c>. Game data, nothing about a player. A champion can be
/// listed under several lanes.
/// </summary>
public sealed record ChampionLanes(
    IReadOnlyList<int> Top,
    IReadOnlyList<int> Jungle,
    IReadOnlyList<int> Middle,
    IReadOnlyList<int> Bottom,
    IReadOnlyList<int> Utility);

/// <summary>
/// The running game's loaded flag and clock, from Riot's Live Client Data API
/// (<c>https://127.0.0.1:2999/liveclientdata/allgamedata</c>, polled while the game runs). The rest of that answer is
/// <see cref="LiveGameState"/>.
/// </summary>
/// <param name="Loaded">
/// True once the game clock runs, i.e. the loading screen is over: the API can answer during the loading screen already,
/// with a frozen clock.
/// </param>
/// <param name="GameTime">In-game clock in seconds as last reported (0 while loading). The phone interpolates between updates.</param>
/// <param name="StartedAt">UTC time at which <paramref name="Loaded"/> first became true for this game; null while loading.</param>
public sealed record InGameState(bool Loaded, double GameTime, DateTimeOffset? StartedAt);

/// <param name="State">LCU value, e.g. "InProgress", "EveryoneReady", "StrangerNotReady", "Invalid".</param>
/// <param name="PlayerResponse">LCU value: "None", "Accepted", "Declined".</param>
/// <param name="Timer">Seconds elapsed since the ready check started (LCU counts up to ~12).</param>
public sealed record ReadyCheckState(string State, string PlayerResponse, double Timer);

/// <param name="QueueId">LCU queue id; -1 for custom games.</param>
/// <param name="GameMode">LCU game mode, e.g. "CLASSIC", "ARAM", "PRACTICETOOL"; null when unknown.</param>
/// <param name="IsCustom">True for custom games (incl. Practice Tool). They have their own queues (31xx) and start like any queue.</param>
/// <param name="ShowPositionSelector">
/// True when every member picks two roles before queueing (Draft Pick, Ranked Solo/Duo, Ranked Flex); the local player can
/// change theirs with <see cref="CommandType.SetPositionPreferences"/> while not searching.
/// </param>
/// <param name="CanInvite">
/// The local player may invite others (the leader, or anyone when the leader opened invites) and the party isn't full;
/// see <see cref="CommandType.SendInvitation"/>.
/// </param>
/// <param name="Invitations">
/// Invitations sent from this lobby that haven't turned into members (pending or declined); null when there are none.
/// </param>
public sealed record LobbyState(
    int QueueId,
    bool IsLeader,
    bool CanStartActivity,
    bool IsSearching,
    double? TimeInQueue,
    double? EstimatedQueueTime,
    IReadOnlyList<LobbyMember> Members,
    string? GameMode,
    bool IsCustom,
    bool ShowPositionSelector,
    bool CanInvite,
    IReadOnlyList<SentInvitation>? Invitations);

/// <summary>An invitation the local player received (<c>/lol-lobby/v2/received-invitations</c>, state Pending).</summary>
/// <param name="Id">LCU invitation id, for <see cref="CommandType.AcceptInvitation"/> / <see cref="CommandType.DeclineInvitation"/>.</param>
/// <param name="FromRiotId">
/// The inviter's Riot ID ("Name#TAG") as the client shows it; their legacy summoner name when no Riot ID resolves,
/// "Someone" when neither does.
/// </param>
/// <param name="QueueId">Queue of the inviting lobby; -1 / 0 for legacy custom games.</param>
/// <param name="QueueName">The client's name for that queue (e.g. "Ranked Flex"); null when the client doesn't list it.</param>
/// <param name="GameMode">LCU game mode, e.g. "CLASSIC", "ARAM", "TFT"; null when unknown.</param>
/// <param name="CanAccept">False when League says the local player can't join that queue (a restriction); Decline still works.</param>
public sealed record ReceivedInvitation(string Id, string FromRiotId, int QueueId, string? QueueName, string? GameMode, bool CanAccept);

/// <summary>An invitation sent from the current lobby (<c>/lol-lobby/v2/lobby</c> <c>invitations</c>).</summary>
/// <param name="Puuid">The invited player's puuid (matches the friends list).</param>
/// <param name="RiotId">
/// Their Riot ID ("Name#TAG"); their legacy summoner name when no Riot ID resolves, "Unknown" when neither does.
/// </param>
/// <param name="State">LCU invitation state: "Requested", "Pending", "OnHold", "Declined" or "Error".</param>
public sealed record SentInvitation(string Puuid, string RiotId, string State);

/// <param name="RiotId">
/// The member's Riot ID ("Name#TAG"); their legacy summoner name when no Riot ID resolves, "Unknown" when neither does.
/// </param>
/// <param name="FirstPositionPreference">LCU role (see <see cref="Positions"/>), e.g. "MIDDLE"; null when not chosen.</param>
/// <param name="IsLocalPlayer">True for the player using this connector.</param>
/// <param name="Puuid">
/// The member's puuid (own lobby only), for <see cref="CommandType.LoadPlayerProfile"/>; empty when the client doesn't say.
/// </param>
/// <param name="ProfileIconId">Summoner icon (the lobby member's <c>summonerIconId</c>); 0 when the client doesn't say.</param>
public sealed record LobbyMember(
    string RiotId,
    bool IsLeader,
    string? FirstPositionPreference,
    string? SecondPositionPreference,
    bool IsLocalPlayer,
    string Puuid,
    int ProfileIconId);

/// <param name="TimerPhase">LCU timer phase: "PLANNING", "BAN_PICK", "FINALIZATION", "GAME_STARTING".</param>
/// <param name="TimeLeftMs">
/// Milliseconds left in the current phase when the snapshot was taken (<see cref="ClientState.UpdatedAt"/>): League's
/// <c>adjustedTimeLeftInPhase</c> less the time since its <c>internalNowInEpochMs</c>, as the client's own countdown does.
/// A newer snapshot of the same phase carries a smaller value; a republished one the same.
/// </param>
/// <param name="MyAction">The local player's in-progress (or next) action, if any.</param>
/// <param name="PickableChampionIds">Positive champion ids only.</param>
/// <param name="BannableChampionIds">
/// Positive champion ids only. When League reports no usable list, the connector falls back to every champion
/// minus those already banned or picked in this session.
/// </param>
/// <param name="TheirTeam">The enemy team as the client shows it: champion and position only (no names, no ids).</param>
/// <param name="Bans">Completed bans of both teams.</param>
/// <param name="Swaps">Pick-order swaps, position swaps and champion trades with the local player's teammates.</param>
/// <param name="Turns">
/// The draft in order, one entry per LCU action group: e.g. Ranked/Draft is one simultaneous ban turn (both teams), the
/// ban reveal, then six pick turns (blue 1, red 2, blue 2, red 2, blue 2, red 1). Nothing is in progress during PLANNING.
/// </param>
/// <param name="MySide">"blue" or "red" (the local player's team side), "" when the client doesn't say.</param>
/// <param name="IsTimerInfinite">True when the phase has no time limit (some custom games).</param>
/// <param name="AllowSubsetChampionPicks">
/// Players pick from a dealt subset of champions (the session's <c>allowSubsetChampionPicks</c>); the client then ticks
/// its timer softly for everyone in the last 5 s instead of loudly for the player whose turn it is.
/// </param>
/// <param name="BenchEnabled">
/// The mode has a champion bench (ARAM and its variants: the session's <c>benchEnabled</c>). Everyone gets a random
/// champion (their <see cref="TeamMember.ChampionId"/>, often with no pick action at all) and can trade it for one on the
/// shared bench; there is no pick grid and no Lock In.
/// </param>
/// <param name="BenchChampionIds">
/// The bench in the client's order (<c>benchChampions[].championId</c>, positive ids only, at most 10); null when
/// <paramref name="BenchEnabled"/> is false.
/// </param>
/// <param name="CanBenchSwap">
/// The client offers bench swaps right now (its <c>showChampionBench</c>: FINALIZATION, or also BAN_PICK with subset
/// picks). <see cref="CommandType.BenchSwap"/> is refused otherwise.
/// </param>
/// <param name="AllowRerolling">The mode offers rerolls (<c>allowRerolling</c>, not with subset picks): show the Reroll button.</param>
/// <param name="RerollsRemaining">The local player's rerolls left (<c>rerollsRemaining</c>); 0 without rerolls.</param>
/// <param name="CanReroll">
/// <see cref="CommandType.Reroll"/> would be accepted now: rerolls allowed and left, FINALIZATION, no champion trade in
/// progress (the client's own Reroll button rules).
/// </param>
/// <param name="Skins">
/// The local player's skins for their champion once it is theirs (locked in, or dealt as in ARAM: <see cref="TeamMember.IsLocked"/>)
/// and the mode allows skins; null before that. See <see cref="SkinSelection"/>.
/// </param>
/// <param name="CanDodge">
/// <see cref="CommandType.DodgeChampSelect"/> would be accepted now: a matchmade champ select (not a custom game,
/// <c>isCustomGame</c>) that isn't starting the game yet.
/// </param>
public sealed record ChampSelectState(
    long LocalPlayerCellId,
    string TimerPhase,
    double TimeLeftMs,
    IReadOnlyList<TeamMember> MyTeam,
    PlayerAction? MyAction,
    IReadOnlyList<int> PickableChampionIds,
    IReadOnlyList<int> BannableChampionIds,
    IReadOnlyList<EnemyMember> TheirTeam,
    ChampSelectBans Bans,
    IReadOnlyList<SwapOffer> Swaps,
    IReadOnlyList<DraftTurn> Turns,
    string MySide,
    bool IsTimerInfinite,
    bool AllowSubsetChampionPicks,
    bool BenchEnabled,
    IReadOnlyList<int>? BenchChampionIds,
    bool CanBenchSwap,
    bool AllowRerolling,
    int RerollsRemaining,
    bool CanReroll,
    SkinSelection? Skins,
    bool CanDodge);

/// <summary>
/// The skins the local player can equip for their locked-in champion: the client's skin carousel
/// (<c>/lol-champ-select/v1/skin-carousel-skins</c>) without what the client itself hides or wouldn't equip — disabled skins
/// and anything not <c>unlocked</c> (not owned, rented or otherwise granted). Bounded so <see cref="ClientState"/> stays
/// small (it is republished on every champ-select change): at most
/// <see cref="MaxSkins"/> skins, <see cref="MaxChromas"/> chromas per skin and <see cref="MaxTotalChromas"/> in all, and at
/// most <see cref="MaxJsonBytes"/> of JSON as SignalR writes it (escaped non-ASCII names included); what doesn't fit is left
/// out, later skins first. Equip one with <see cref="CommandType.SelectSkin"/>.
/// </summary>
/// <param name="ChampionId">The champion these skins belong to.</param>
/// <param name="SelectedSkinId">The skin or chroma equipped right now (<c>myTeam[].selectedSkinId</c>); 0 when the client says none.</param>
/// <param name="Skins">The default skin first, then the carousel's order.</param>
public sealed record SkinSelection(int ChampionId, int SelectedSkinId, IReadOnlyList<ChampionSkin> Skins)
{
    public const int MaxSkins = 100;

    public const int MaxChromas = 16;

    public const int MaxTotalChromas = 300;

    /// <summary>Longest skin or chroma name kept (longer ones are cut).</summary>
    public const int MaxNameLength = 64;

    /// <summary>Most JSON the skins may take in a state message (the connector counts escaped characters at their full size).</summary>
    public const int MaxJsonBytes = 48 * 1024;
}

/// <summary>One equippable skin of the carousel.</summary>
/// <param name="Id">Skin id: champion id × 1000 + the skin's number (Data Dragon art is <c>{Champion}_{Id % 1000}.jpg</c>).</param>
/// <param name="Name">The skin's name as the client shows it (the default skin is named after the champion).</param>
/// <param name="IsBase">The champion's default skin.</param>
/// <param name="Chromas">Its equippable chromas (the carousel's <c>childSkins</c>); equipping one also equips the skin.</param>
public sealed record ChampionSkin(int Id, string Name, bool IsBase, IReadOnlyList<SkinChroma> Chromas);

/// <summary>A chroma (or a tiered skin's form) of a <see cref="ChampionSkin"/>.</summary>
/// <param name="Colors">
/// Up to two <c>#RRGGBB</c> colours the client paints its chroma swatch with (two: split diagonally); empty when the
/// client has none (tiered forms).
/// </param>
public sealed record SkinChroma(int Id, string Name, IReadOnlyList<string> Colors);

/// <summary>One step of the draft: an LCU action group, whose actions all run at the same time.</summary>
/// <param name="Type">"ban", "pick", "reveal" (the ban reveal between bans and picks) or the raw LCU type.</param>
/// <param name="Team">"ally", "enemy", "both" (simultaneous bans / blind pick) or "none" (reveal).</param>
/// <param name="CellIds">Cells acting in this turn (allies and enemies; cell ids carry no identity).</param>
/// <param name="IsInProgress">This is the turn being played right now (never during PLANNING).</param>
/// <param name="Completed">Every action in the turn is done.</param>
public sealed record DraftTurn(string Type, string Team, IReadOnlyList<long> CellIds, bool IsInProgress, bool Completed);

/// <param name="ChampionId">Locked champion, or, while this player is picking, the one they hover; 0 when none.</param>
/// <param name="IsLocked">
/// True once this player's pick action is completed; in bench modes (<see cref="ChampSelectState.BenchEnabled"/>) as soon as
/// they were dealt a champion, unless they still have a pick action open (subset picks).
/// </param>
/// <param name="IsActing">True while this player has a pick or ban in progress (their turn, not during PLANNING).</param>
/// <param name="PickOrder">1-based position of this player's pick in the whole draft (1–10); 0 when unknown.</param>
/// <param name="BanChampionId">The champion this player bans (hovered while banning, then locked); 0 when none.</param>
/// <param name="HasBanned">True once this player's ban action is completed.</param>
/// <param name="Puuid">
/// The teammate's puuid, for <see cref="CommandType.LoadPlayerProfile"/>: only while League shows who they are
/// (<c>nameVisibilityType</c> not "HIDDEN" — an anonymous ranked draft hides it until the game), empty otherwise. Own team
/// only; enemies never have one.
/// </param>
/// <param name="RiotId">
/// The teammate's Riot ID ("Name#TAG", like lobby members), under the same rule as <paramref name="Puuid"/>: only while
/// League shows who they are, empty in an anonymous draft. Own team only; enemies never have one.
/// </param>
public sealed record TeamMember(
    long CellId,
    bool IsLocalPlayer,
    int ChampionId,
    int ChampionPickIntent,
    string AssignedPosition,
    int Spell1Id,
    int Spell2Id,
    bool IsLocked,
    bool IsActing,
    int PickOrder,
    int BanChampionId,
    bool HasBanned,
    string Puuid,
    string RiotId);

/// <param name="ChampionId">Picked (or, during their turn, hovered) champion; 0 when none is visible.</param>
/// <param name="ChampionPickIntent">Declared intent when the client shows it (usually 0 for the enemy).</param>
/// <param name="AssignedPosition">LCU position ("top", "jungle", "middle", "bottom", "utility") or "" when hidden.</param>
/// <param name="IsLocked">True once this player's pick action is completed (bench modes: once a champion of theirs is visible).</param>
/// <param name="IsActing">True while this player's pick is in progress (bans are hidden until the reveal).</param>
/// <param name="PickOrder">1-based position of this player's pick in the whole draft (1–10); 0 when unknown.</param>
public sealed record EnemyMember(
    long CellId,
    int ChampionId,
    int ChampionPickIntent,
    string AssignedPosition,
    bool IsLocked,
    bool IsActing,
    int PickOrder);

public sealed record ChampSelectBans(IReadOnlyList<int> MyTeamBans, IReadOnlyList<int> TheirTeamBans)
{
    public static ChampSelectBans None { get; } = new([], []);
}

/// <summary>A swap or trade League offers with the teammate in <paramref name="CellId"/>.</summary>
/// <param name="Id">Id used in the LCU swap/trade endpoints (see the <c>RequestSwap</c> family of commands).</param>
/// <param name="State">LCU value: "AVAILABLE", "BUSY", "INVALID", "RECEIVED", "SENT", "ACCEPTED", "DECLINED", "CANCELLED".</param>
public sealed record SwapOffer(SwapKind Kind, long Id, long CellId, string State);

/// <param name="Type">LCU value: "pick", "ban", ...</param>
public sealed record PlayerAction(long Id, string Type, int ChampionId, bool IsInProgress, bool Completed);

public sealed record RunePage(long Id, string Name, bool IsCurrent, bool IsEditable);
