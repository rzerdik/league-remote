using System.Text.Json;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.ChampSelect;

/// <summary>
/// One champ-select session (<c>GET /lol-champ-select/v1/session</c>), read once and leniently: the state mapper, the
/// automation, the skin picker, the lock confirmation, the command checks and the draft chat all work from this. Enemies
/// carry champion, hover and position only; their names, puuids and summoner ids are never read.
/// </summary>
/// <param name="Actions">Every action of every group, in draft order (<c>actions</c> is an array of groups).</param>
/// <param name="ListedBans">
/// <c>bans.myTeamBans</c> / <c>theirTeamBans</c>: what League lists besides the completed ban actions (positive ids only).
/// </param>
/// <param name="BenchChampionIds"><c>benchChampions[].championId</c> in the client's order, positive and distinct.</param>
/// <param name="AllowRerolling">The session's <c>allowRerolling</c> as it is; see <see cref="ChampSelectRules.AllowsRerolling"/>.</param>
/// <param name="AllowSkinSelection">False only when the session says so; older clients without the flag allow skins.</param>
internal sealed record ChampSelectSession(
    long LocalCellId,
    long GameId,
    int QueueId,
    ChampSelectTimer Timer,
    IReadOnlyList<ChampSelectAction> Actions,
    IReadOnlyList<ChampSelectTeammate> MyTeam,
    IReadOnlyList<ChampSelectOpponent> TheirTeam,
    ChampSelectBans ListedBans,
    IReadOnlyList<SwapOffer> Swaps,
    IReadOnlyList<int> BenchChampionIds,
    bool BenchEnabled,
    bool AllowSubsetChampionPicks,
    bool AllowRerolling,
    int RerollsRemaining,
    bool IsCustomGame,
    bool AllowSkinSelection)
{
    /// <summary>The local player's <c>myTeam</c> entry (<c>cellId == localPlayerCellId</c>); null when the session has none.</summary>
    public ChampSelectTeammate? Me => MyTeam.FirstOrDefault(p => p.CellId == LocalCellId);

    /// <summary>The local player's actions (any type), in draft order.</summary>
    public IEnumerable<ChampSelectAction> MyActions => Actions.Where(a => a.ActorCellId == LocalCellId);

    /// <summary>
    /// The local player's action that is in progress, otherwise the next uncompleted one. During PLANNING the client declares
    /// pick intent on the pick action even when a ban comes first (League already marks that ban in progress then).
    /// </summary>
    public PlayerAction? FindMyAction()
    {
        var mine = MyActions.ToList();
        if (Timer.Phase == ChampSelectTimer.Planning && mine.FirstOrDefault(a => !a.Completed && a.Type == ChampSelectAction.Pick) is { } intent)
            return intent.ToPlayerAction();
        return (mine.FirstOrDefault(a => a.IsInProgress && !a.Completed) ?? mine.FirstOrDefault(a => !a.Completed))?.ToPlayerAction();
    }

    /// <summary>The local player's action with <paramref name="actionId"/>, whatever its state.</summary>
    public PlayerAction? FindMyAction(long actionId) => MyActions.FirstOrDefault(a => a.Id == actionId)?.ToPlayerAction();

    /// <summary>The swap/trade of <paramref name="kind"/> with <paramref name="id"/>, if the session still lists it.</summary>
    public SwapOffer? FindSwap(SwapKind kind, long id) => Swaps.FirstOrDefault(s => s.Kind == kind && s.Id == id);

    /// <summary>Whether <paramref name="puuid"/> is on the local player's team and League shows who they are.</summary>
    public bool IsVisibleTeammate(string puuid) =>
        MyTeam.Any(p => string.Equals(p.VisiblePuuid, puuid, StringComparison.OrdinalIgnoreCase));

    /// <summary>The session; null when <paramref name="json"/> isn't an object (no champ select, a 404).</summary>
    public static ChampSelectSession? Parse(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } s)
            return null;

        var myTeam = s.Arr("myTeam").Where(p => p.ValueKind == JsonValueKind.Object).Select(ChampSelectTeammate.Parse).ToList();
        var myCells = myTeam.Select(p => p.CellId).ToHashSet();
        var rerolls = s.Long("rerollsRemaining");

        return new ChampSelectSession(
            s.Long("localPlayerCellId"),
            s.Long("gameId"),
            s.Int("queueId"),
            s.Obj("timer") is { } timer ? ChampSelectTimer.Parse(timer) : ChampSelectTimer.None,
            ParseActions(s, myCells),
            myTeam,
            s.Arr("theirTeam").Where(p => p.ValueKind == JsonValueKind.Object).Select(ChampSelectOpponent.Parse).ToList(),
            s.Obj("bans") is { } bans ? new ChampSelectBans(PositiveInts(bans, "myTeamBans"), PositiveInts(bans, "theirTeamBans")) : ChampSelectBans.None,
            [.. SwapList(s, "pickOrderSwaps", SwapKind.PickOrder), .. SwapList(s, "positionSwaps", SwapKind.Position), .. SwapList(s, "trades", SwapKind.Trade)],
            ChampSelectRules.ValidIds(s.Arr("benchChampions").Where(b => b.ValueKind == JsonValueKind.Object).Select(b => b.Int("championId"))),
            s.Bool("benchEnabled"),
            s.Bool("allowSubsetChampionPicks"),
            s.Bool("allowRerolling"),
            (int)Math.Clamp(rerolls, 0, int.MaxValue),
            s.Bool("isCustomGame"),
            !(s.TryGetProperty("allowSkinSelection", out var allow) && allow.ValueKind == JsonValueKind.False));
    }

    /// <summary>
    /// The actions with their group index; an action's team is its <c>isAllyAction</c>, else whether the actor sits in my
    /// team.
    /// </summary>
    private static List<ChampSelectAction> ParseActions(JsonElement session, HashSet<long> myCells) =>
        session.Arr("actions")
            .Where(group => group.ValueKind == JsonValueKind.Array)
            .SelectMany((group, index) => group.EnumerateArray()
                .Where(a => a.ValueKind == JsonValueKind.Object)
                .Select(a => new ChampSelectAction(
                    a.Long("id"),
                    index,
                    a.Long("actorCellId"),
                    a.Str("type") ?? "",
                    a.Int("championId"),
                    a.Bool("completed"),
                    a.Bool("isInProgress"),
                    a.TryGetProperty("isAllyAction", out var ally) && ally.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? ally.GetBoolean()
                        : myCells.Contains(a.Long("actorCellId")))))
            .ToList();

    private static List<int> PositiveInts(JsonElement e, string name) =>
        e.Arr(name)
            .Where(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) && i > 0)
            .Select(v => v.GetInt32())
            .ToList();

    /// <summary><c>pickOrderSwaps</c>, <c>positionSwaps</c> and <c>trades</c>: one <c>{ id, cellId, state }</c> per teammate.</summary>
    private static IEnumerable<SwapOffer> SwapList(JsonElement session, string property, SwapKind kind) =>
        session.Arr(property)
            .Where(s => s.ValueKind == JsonValueKind.Object && s.Long("id") > 0)
            .Select(s => new SwapOffer(kind, s.Long("id"), s.Long("cellId"), (s.Str("state") ?? "").ToUpperInvariant()));
}

/// <summary>The session's <c>timer</c>.</summary>
/// <param name="Phase">"PLANNING", "BAN_PICK", "FINALIZATION", "GAME_STARTING"; "" when missing.</param>
/// <param name="AdjustedTimeLeftMs"><c>adjustedTimeLeftInPhase</c>: the time left at <paramref name="AnchorEpochMs"/>, not at the read.</param>
/// <param name="AnchorEpochMs"><c>internalNowInEpochMs</c>: when the client took the timer from the server; 0 when League doesn't say.</param>
/// <param name="TotalMs"><c>totalTimeInPhase</c>.</param>
internal sealed record ChampSelectTimer(string Phase, double AdjustedTimeLeftMs, double AnchorEpochMs, double TotalMs, bool IsInfinite)
{
    public const string Planning = "PLANNING";
    public const string BanPick = "BAN_PICK";
    public const string Finalization = "FINALIZATION";
    public const string GameStarting = "GAME_STARTING";

    public static readonly ChampSelectTimer None = new("", 0, 0, 0, false);

    public static ChampSelectTimer Parse(JsonElement timer) => new(
        timer.Str("phase") ?? "",
        timer.Dbl("adjustedTimeLeftInPhase"),
        timer.Dbl("internalNowInEpochMs"),
        timer.Dbl("totalTimeInPhase"),
        timer.Bool("isInfinite"));

    /// <summary>Milliseconds left in the phase at <paramref name="at"/>; see <see cref="TimeLeftMs(double, double, DateTimeOffset)"/>.</summary>
    public double TimeLeftAt(DateTimeOffset at) => TimeLeftMs(AdjustedTimeLeftMs, AnchorEpochMs, at);

    /// <summary>
    /// The time left at <paramref name="at"/> the way League's own countdown (<c>rcp-fe-lol-champ-select</c>) counts it:
    /// <c>max(adjustedTimeLeftInPhase − (now − internalNowInEpochMs), 0)</c>, with the connector on the same PC clock. Never
    /// more than League's value (a clock that went back counts as no time passed); without an anchor League's value as it is.
    /// </summary>
    public static double TimeLeftMs(double adjustedTimeLeftInPhase, double internalNowInEpochMs, DateTimeOffset at)
    {
        var left = Math.Max(0, adjustedTimeLeftInPhase);
        if (internalNowInEpochMs <= 0)
            return left;
        var elapsed = Math.Max(0, at.ToUnixTimeMilliseconds() - internalNowInEpochMs);
        return Math.Max(0, left - elapsed);
    }

    /// <summary>
    /// The anchor when it is older than the whole phase at <paramref name="now"/>, else null (no anchor, infinite or untimed
    /// phase). League updates the timer at least once per phase, so such an anchor means the PC clock and League disagree:
    /// the time left reads 0 and auto-lock only hovers. Worth a log line.
    /// </summary>
    public double? StaleAnchor(DateTimeOffset now) =>
        !IsInfinite && AnchorEpochMs > 0 && TotalMs > 0 && now.ToUnixTimeMilliseconds() - AnchorEpochMs > TotalMs ? AnchorEpochMs : null;
}

/// <summary>One action of the draft.</summary>
/// <param name="Group">Index of its action group: one turn of the draft, whose actions run at the same time.</param>
/// <param name="IsAlly">Resolved: <c>isAllyAction</c>, else whether the actor sits in my team.</param>
internal sealed record ChampSelectAction(
    long Id, int Group, long ActorCellId, string Type, int ChampionId, bool Completed, bool IsInProgress, bool IsAlly)
{
    public const string Pick = "pick";
    public const string Ban = "ban";

    public PlayerAction ToPlayerAction() => new(Id, Type, ChampionId, IsInProgress, Completed);
}

/// <summary>
/// A <c>myTeam</c> entry. <see cref="NameHidden"/> is the one place that decides whether the phone may learn who a
/// teammate is: an anonymous (ranked) draft sets <c>nameVisibilityType</c> to "HIDDEN" and shows "Summoner 1"… instead.
/// </summary>
/// <param name="Puuid">
/// As League sends it, even when hidden: only for matching draft-chat senders to cells. What may reach the phone is
/// <see cref="VisiblePuuid"/>.
/// </param>
/// <param name="GameName">As League sends it; see <see cref="VisibleGameName"/>.</param>
/// <param name="RiotId">"gameName#tagLine" when the entry has a game name; see <see cref="VisibleRiotId"/>.</param>
internal sealed record ChampSelectTeammate(
    long CellId,
    int Team,
    int ChampionId,
    int ChampionPickIntent,
    string AssignedPosition,
    int Spell1Id,
    int Spell2Id,
    int SelectedSkinId,
    bool NameHidden,
    string Puuid,
    string GameName,
    string? RiotId,
    string ObfuscatedPuuid,
    long SummonerId,
    long ObfuscatedSummonerId)
{
    /// <summary>The puuid while League shows who the teammate is; empty in an anonymous draft.</summary>
    public string VisiblePuuid => NameHidden ? "" : Puuid;

    /// <summary>The Riot ID ("Name#TAG") under the same rule as <see cref="VisiblePuuid"/>.</summary>
    public string VisibleRiotId => NameHidden ? "" : RiotId ?? "";

    /// <summary>The game name under the same rule as <see cref="VisiblePuuid"/>.</summary>
    public string VisibleGameName => NameHidden ? "" : GameName;

    public static ChampSelectTeammate Parse(JsonElement p) => new(
        p.Long("cellId"),
        p.Int("team"),
        p.Int("championId"),
        p.Int("championPickIntent"),
        p.Str("assignedPosition") ?? "",
        p.Int("spell1Id"),
        p.Int("spell2Id"),
        p.Int("selectedSkinId"),
        string.Equals(p.Str("nameVisibilityType"), "HIDDEN", StringComparison.OrdinalIgnoreCase),
        p.Str("puuid") ?? "",
        p.Str("gameName") ?? "",
        RiotIdNames.OwnRiotId(p),
        p.Str("obfuscatedPuuid") ?? "",
        p.Long("summonerId"),
        p.Long("obfuscatedSummonerId"));
}

/// <summary>A <c>theirTeam</c> entry: only what the client shows anyway. Names, puuids and summoner ids are deliberately not read.</summary>
internal sealed record ChampSelectOpponent(long CellId, int ChampionId, int ChampionPickIntent, string AssignedPosition)
{
    public static ChampSelectOpponent Parse(JsonElement p) =>
        new(p.Long("cellId"), p.Int("championId"), p.Int("championPickIntent"), p.Str("assignedPosition") ?? "");
}
