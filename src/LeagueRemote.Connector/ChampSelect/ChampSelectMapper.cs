using System.Text.Json;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.ChampSelect;

/// <summary>
/// Pure mapping of a champ-select session to <see cref="ChampSelectState"/>. Own team in full (names and puuids only while
/// League shows them, see <see cref="ChampSelectTeammate.NameHidden"/>); from the enemy team only what the client shows
/// anyway: champion, hover, assigned position, bans.
/// </summary>
internal static class ChampSelectMapper
{
    /// <param name="now">
    /// When the state is built: <see cref="ChampSelectState.TimeLeftMs"/> is the time left then (see
    /// <see cref="ChampSelectTimer.TimeLeftAt"/>); null keeps League's <c>adjustedTimeLeftInPhase</c> as it is.
    /// </param>
    public static ChampSelectState? Map(
        JsonElement? sessionJson,
        IReadOnlyList<int> pickable,
        IReadOnlyList<int> bannable,
        IReadOnlyList<int>? allChampionIds = null,
        JsonElement? skinCarousel = null,
        DateTimeOffset? now = null)
    {
        if (ChampSelectSession.Parse(sessionJson) is not { } session)
            return null;

        var timer = session.Timer;
        // League already marks the first group in progress while players declare intent; nobody is acting yet then.
        var planning = timer.Phase == ChampSelectTimer.Planning;
        var actions = session.Actions;
        var pickOrder = PickOrder(actions);
        var acting = planning
            ? []
            : actions.Where(a => a.IsInProgress && !a.Completed && a.Type is ChampSelectAction.Pick or ChampSelectAction.Ban)
                .Select(a => a.ActorCellId)
                .ToHashSet();
        var lockedCells = actions.Where(a => a.Type == ChampSelectAction.Pick && a.Completed).Select(a => a.ActorCellId).ToHashSet();
        var openPickCells = actions.Where(a => a.Type == ChampSelectAction.Pick && !a.Completed).Select(a => a.ActorCellId).ToHashSet();
        bool IsLocked(long cellId, int championId) =>
            ChampSelectRules.IsChampionLocked(lockedCells.Contains(cellId), openPickCells.Contains(cellId), session.BenchEnabled, championId);

        var myTeam = session.MyTeam
            .Select(p =>
            {
                var ban = actions.FirstOrDefault(a => a.Type == ChampSelectAction.Ban && a.ActorCellId == p.CellId);
                return new TeamMember(
                    p.CellId,
                    p.CellId == session.LocalCellId,
                    Positive(p.ChampionId),
                    Positive(p.ChampionPickIntent),
                    p.AssignedPosition,
                    p.Spell1Id,
                    p.Spell2Id,
                    IsLocked: IsLocked(p.CellId, p.ChampionId),
                    IsActing: acting.Contains(p.CellId),
                    PickOrder: pickOrder.GetValueOrDefault(p.CellId),
                    BanChampionId: ban is null ? 0 : Positive(ban.ChampionId),
                    HasBanned: ban is { Completed: true },
                    Puuid: p.VisiblePuuid,
                    RiotId: p.VisibleRiotId);
            })
            .ToList();

        var theirTeam = session.TheirTeam
            .Select(p => new EnemyMember(
                p.CellId,
                Positive(p.ChampionId),
                Positive(p.ChampionPickIntent),
                p.AssignedPosition,
                IsLocked(p.CellId, p.ChampionId),
                IsActing: acting.Contains(p.CellId),
                PickOrder: pickOrder.GetValueOrDefault(p.CellId)))
            .ToList();

        var bans = MapBans(session);

        return new ChampSelectState(
            session.LocalCellId,
            timer.Phase,
            now is { } at ? timer.TimeLeftAt(at) : timer.AdjustedTimeLeftMs,
            myTeam,
            session.FindMyAction(),
            ChampSelectRules.ValidIds(pickable),
            BannableOrFallback(bannable, allChampionIds, bans, actions),
            theirTeam,
            bans,
            session.Swaps,
            MapTurns(actions, planning),
            MySide(session),
            timer.IsInfinite,
            session.AllowSubsetChampionPicks,
            BenchEnabled: session.BenchEnabled,
            BenchChampionIds: session.BenchEnabled ? session.BenchChampionIds : null,
            CanBenchSwap: ChampSelectRules.IsBenchOpen(session),
            AllowRerolling: ChampSelectRules.AllowsRerolling(session),
            RerollsRemaining: session.RerollsRemaining,
            CanReroll: ChampSelectRules.RerollRefusal(session) is null,
            Skins: SkinMapper.Map(session, skinCarousel),
            CanDodge: ChampSelectRules.DodgeRefusal(session) is null);
    }

    /// <summary>The session's timer anchor when it is stale (see <see cref="ChampSelectTimer.StaleAnchor"/>); null without a session.</summary>
    public static double? StaleTimerAnchor(JsonElement? sessionJson, DateTimeOffset now) =>
        ChampSelectSession.Parse(sessionJson)?.Timer.StaleAnchor(now);

    /// <summary>
    /// One <see cref="DraftTurn"/> per action group; groups mixing both teams are "both" (simultaneous bans, blind pick).
    /// </summary>
    private static IReadOnlyList<DraftTurn> MapTurns(IReadOnlyList<ChampSelectAction> actions, bool planning) =>
        actions
            .GroupBy(a => a.Group)
            .Select(g =>
            {
                var players = g.Where(a => a.Type is ChampSelectAction.Pick or ChampSelectAction.Ban).ToList();
                var ally = players.Any(a => a.IsAlly);
                var enemy = players.Any(a => !a.IsAlly);
                var type = g.First().Type;
                return new DraftTurn(
                    type == "ten_bans_reveal" ? "reveal" : type,
                    ally && enemy ? "both" : ally ? "ally" : enemy ? "enemy" : "none",
                    players.Select(a => a.ActorCellId).Distinct().ToList(),
                    !planning && g.Any(a => a.IsInProgress && !a.Completed),
                    g.All(a => a.Completed));
            })
            .ToList();

    /// <summary>Cell id → 1-based position of that cell's pick among all pick actions, in draft order.</summary>
    private static Dictionary<long, int> PickOrder(IEnumerable<ChampSelectAction> actions)
    {
        var order = new Dictionary<long, int>();
        foreach (var pick in actions.Where(a => a.Type == ChampSelectAction.Pick))
            order.TryAdd(pick.ActorCellId, order.Count + 1);
        return order;
    }

    /// <summary>"blue" for team 1, "red" for team 2 (the local player's <c>team</c> in <c>myTeam</c>), else "".</summary>
    private static string MySide(ChampSelectSession session) =>
        session.Me?.Team switch { 1 => "blue", 2 => "red", _ => "" };

    /// <summary>
    /// The LCU's bannable ids when usable; otherwise every champion minus those already banned or picked in this
    /// session (League itself rejects anything else on lock, and the phone shows that error).
    /// </summary>
    private static IReadOnlyList<int> BannableOrFallback(
        IReadOnlyList<int> bannable,
        IReadOnlyList<int>? allChampionIds,
        ChampSelectBans bans,
        IReadOnlyList<ChampSelectAction> actions)
    {
        if (!ChampSelectRules.IsUnusableIdList(bannable) || allChampionIds is null)
            return ChampSelectRules.ValidIds(bannable);

        var taken = new HashSet<int>(bans.MyTeamBans.Concat(bans.TheirTeamBans));
        taken.UnionWith(actions.Where(a => a.Type == ChampSelectAction.Pick && a.Completed).Select(a => a.ChampionId));
        return ChampSelectRules.ValidIds(allChampionIds).Where(id => !taken.Contains(id)).ToList();
    }

    /// <summary>
    /// Completed ban actions split by team, falling back to <see cref="ChampSelectSession.ListedBans"/> for a team without
    /// completed ban actions.
    /// </summary>
    private static ChampSelectBans MapBans(ChampSelectSession session)
    {
        var completedBans = session.Actions.Where(a => a.Type == ChampSelectAction.Ban && a.Completed && a.ChampionId > 0).ToList();
        IReadOnlyList<int> mine = completedBans.Where(a => a.IsAlly).Select(a => a.ChampionId).ToList();
        IReadOnlyList<int> theirs = completedBans.Where(a => !a.IsAlly).Select(a => a.ChampionId).ToList();
        if (mine.Count == 0)
            mine = session.ListedBans.MyTeamBans;
        if (theirs.Count == 0)
            theirs = session.ListedBans.TheirTeamBans;

        return mine.Count == 0 && theirs.Count == 0 ? ChampSelectBans.None : new ChampSelectBans(mine, theirs);
    }

    private static int Positive(int id) => id > 0 ? id : 0;
}
