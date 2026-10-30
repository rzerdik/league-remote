using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.AutoPick;

/// <summary>
/// What the automation reads from one champ-select session. Champion ids only: no player's name, puuid or summoner id is
/// used.
/// </summary>
/// <param name="GameId">Keys the run: a dodge and a new champ select bring a new one.</param>
/// <param name="MyAction">The local player's ban or pick that is in progress right now (timer phase <c>BAN_PICK</c>), else null.</param>
/// <param name="MySpells">The local player's <c>spell1Id</c> / <c>spell2Id</c>; null when they aren't in <c>myTeam</c>.</param>
/// <param name="MyPicks">The local player's pick intent and pick hover / lock: never banned by the automation.</param>
/// <param name="Banned">Completed bans of both teams (actions and <c>bans.myTeamBans</c> / <c>theirTeamBans</c>).</param>
/// <param name="TeammateBans">Champions on a teammate's open ban action (their ban hover).</param>
/// <param name="TeammatePicks">Champions a teammate hovers, locked or declared (<c>championPickIntent</c>).</param>
/// <param name="EnemyLocks">Champions an enemy locked in.</param>
internal sealed record ChampSelectFacts(
    long GameId,
    int QueueId,
    bool IsCustomGame,
    bool BenchEnabled,
    ChampSelectTimer Timer,
    string Role,
    PlayerAction? MyAction,
    (int Spell1, int Spell2)? MySpells,
    IReadOnlySet<int> MyPicks,
    IReadOnlySet<int> Banned,
    IReadOnlySet<int> TeammateBans,
    IReadOnlySet<int> TeammatePicks,
    IReadOnlySet<int> EnemyLocks)
{
    /// <summary>
    /// The preset group of this queue (custom SR Blind / Draft / Tournament Draft queues included), or null when it isn't
    /// automated. A custom game without a queue id (≤ 0, a legacy custom lobby) is not automated: the session alone can't
    /// tell Summoner's Rift from Practice Tool or Howling Abyss (the champ-select rebuild reads no lobby or gameflow
    /// session), and a wrong guess would lock a champion or set spells the map doesn't have.
    /// </summary>
    public string? QueueGroup => AutoPickQueueGroups.ForQueue(QueueId);
}

/// <summary>
/// The automation's pure rules (see the <see cref="AutoPickState"/> XML doc, which is the spec): reading the session,
/// which champions are valid, when a hovered action completes and the log sentences. No I/O.
/// </summary>
internal static class AutoPickRules
{
    public static ChampSelectFacts Read(ChampSelectSession session)
    {
        var localCell = session.LocalCellId;
        var me = session.Me;
        var actions = session.Actions.Where(a => a.ChampionId > 0).ToList();

        var banned = actions.Where(a => a.Type == ChampSelectAction.Ban && a.Completed).Select(a => a.ChampionId).ToHashSet();
        banned.UnionWith(session.ListedBans.MyTeamBans.Concat(session.ListedBans.TheirTeamBans));

        var teammatePicks = session.MyTeam.Where(p => p.CellId != localCell)
            .SelectMany(p => new[] { p.ChampionId, p.ChampionPickIntent })
            .Concat(actions.Where(a => a.Type == ChampSelectAction.Pick && a.IsAlly && a.ActorCellId != localCell).Select(a => a.ChampionId))
            .Where(id => id > 0)
            .ToHashSet();
        var teammateBans = actions.Where(a => a.Type == ChampSelectAction.Ban && !a.Completed && a.IsAlly && a.ActorCellId != localCell)
            .Select(a => a.ChampionId)
            .ToHashSet();
        var enemyLocks = actions.Where(a => a.Type == ChampSelectAction.Pick && a.Completed && !a.IsAlly).Select(a => a.ChampionId).ToHashSet();
        // Not myTeam's championId for the local player: whether League mirrors a ban hover there isn't verified, and our own
        // pending ban must never look like one of our picks.
        var myPicks = actions.Where(a => a.Type == ChampSelectAction.Pick && a.ActorCellId == localCell).Select(a => a.ChampionId)
            .Append(me?.ChampionPickIntent ?? 0)
            .Where(id => id > 0)
            .ToHashSet();

        // League marks the first ban in progress already during PLANNING (players declare intent then): only BAN_PICK counts.
        var myAction = session.Timer.Phase == ChampSelectTimer.BanPick
                       && session.FindMyAction() is { IsInProgress: true, Completed: false } action
                       && action.Type is AutoPickActionKinds.Ban or AutoPickActionKinds.Pick
            ? action
            : null;

        return new ChampSelectFacts(
            session.GameId,
            session.QueueId,
            session.IsCustomGame,
            session.BenchEnabled,
            session.Timer,
            AutoPickRoles.FromAssignedPosition(me?.AssignedPosition),
            myAction,
            me is null ? null : (me.Spell1Id, me.Spell2Id),
            myPicks,
            banned,
            teammateBans,
            teammatePicks,
            enemyLocks);
    }

    /// <summary>
    /// A ban the automation may hover: bannable (when League gives a usable list), not banned yet, not on a teammate's ban
    /// or pick (hover, lock, intent), not one of the local player's own picks or intent, not in their own pick list.
    /// </summary>
    public static bool IsValidBan(int championId, ChampSelectFacts facts, IReadOnlyList<int>? bannable, IReadOnlyList<int> ownPickList) =>
        championId > 0
        && InList(championId, bannable)
        && !facts.Banned.Contains(championId)
        && !facts.TeammateBans.Contains(championId)
        && !facts.TeammatePicks.Contains(championId)
        && !facts.MyPicks.Contains(championId)
        && !ownPickList.Contains(championId);

    /// <summary>A pick the automation may hover: pickable (owned or free, not disabled), not banned, not a teammate's, not locked by an enemy.</summary>
    public static bool IsValidPick(int championId, ChampSelectFacts facts, IReadOnlyList<int>? pickable) =>
        championId > 0
        && InList(championId, pickable)
        && !facts.Banned.Contains(championId)
        && !facts.TeammatePicks.Contains(championId)
        && !facts.EnemyLocks.Contains(championId);

    /// <summary>
    /// League sometimes answers <c>[-1]</c> or nothing for the id lists (see <see cref="ChampSelectRules.IsUnusableIdList"/>): then
    /// the list doesn't restrict, and League itself refuses an unavailable champion (logged as failed).
    /// </summary>
    private static bool InList(int championId, IReadOnlyList<int>? ids) => ChampSelectRules.IsUnusableIdList(ids) || ids!.Contains(championId);

    /// <summary>Whether <paramref name="championId"/> is a valid choice for an action of <paramref name="kind"/>.</summary>
    public static bool IsValid(string kind, int championId, ChampSelectFacts facts, AutoPickPreset preset, IReadOnlyList<int>? ids) =>
        kind == AutoPickActionKinds.Ban
            ? IsValidBan(championId, facts, ids, preset.PickChampionIds)
            : IsValidPick(championId, facts, ids);

    /// <summary>The preset's list for <paramref name="kind"/> (bans or picks, in priority order).</summary>
    public static IReadOnlyList<int> Choices(string kind, AutoPickPreset preset) =>
        kind == AutoPickActionKinds.Ban ? preset.BanChampionIds : preset.PickChampionIds;

    /// <summary>The first valid choice of the preset's list for <paramref name="kind"/>, or null.</summary>
    public static int? FirstValid(string kind, ChampSelectFacts facts, AutoPickPreset preset, IReadOnlyList<int>? ids) =>
        Choices(kind, preset).Where(id => IsValid(kind, id, facts, preset, ids)).Select(id => (int?)id).FirstOrDefault();

    /// <summary>
    /// When a hover at <paramref name="hoverAt"/> completes: min(hover + <see cref="AutoPickState.AutoLockDelay"/>, phase end −
    /// <see cref="AutoPickState.CompletionMargin"/>), phase end = <paramref name="observedAt"/> (when the session was read) +
    /// the timer's time left at that moment (<see cref="ChampSelectTimer.TimeLeftAt"/>: League's value is as of its
    /// <c>internalNowInEpochMs</c>, which can be well before the read); an infinite timer has no phase end. Null when that
    /// isn't after the hover (hover only).
    /// </summary>
    public static DateTimeOffset? CompletesAt(DateTimeOffset hoverAt, DateTimeOffset observedAt, ChampSelectFacts facts)
    {
        var completesAt = hoverAt + AutoPickState.AutoLockDelay;
        if (!facts.Timer.IsInfinite)
        {
            var latest = observedAt + TimeSpan.FromMilliseconds(facts.Timer.TimeLeftAt(observedAt)) - AutoPickState.CompletionMargin;
            if (latest < completesAt)
                completesAt = latest;
        }

        return completesAt > hoverAt ? completesAt : null;
    }

    // --- Rune page names --------------------------------------------------------------------

    /// <summary>
    /// The name to store with a preset for page <paramref name="runePageId"/>: the client's (<paramref name="pages"/>, the
    /// newest <c>/lol-perks/v1/pages</c> read; null while League is closed), else a name a saved preset already has for
    /// that page, else "". "" for no page.
    /// </summary>
    public static string RunePageName(AutoPickSettings settings, long runePageId, JsonElement? pages)
    {
        if (runePageId <= 0)
            return "";
        if (GameStateMapper.MapRunePages(pages).FirstOrDefault(p => p.Id == runePageId) is { Name: { Length: > 0 } live })
            return live;
        return settings.Presets.FirstOrDefault(p => p.RunePageId == runePageId && p.RunePageName.Length > 0)?.RunePageName ?? "";
    }

    /// <summary>
    /// <paramref name="settings"/> with each preset's <see cref="AutoPickPreset.RunePageName"/> set to the client's current
    /// name of its page; null when nothing changes (no pages read, no page of a preset listed, names already right). A page
    /// that isn't listed (deleted, or not read) keeps its stored name, and an empty name never replaces one.
    /// </summary>
    public static AutoPickSettings? WithRunePageNames(AutoPickSettings settings, JsonElement? pages)
    {
        if (!settings.Presets.Any(p => p.RunePageId > 0))
            return null;
        var names = GameStateMapper.MapRunePages(pages)
            .Where(p => p.Name.Length > 0)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => Text.Clip(g.First().Name, AutoPickSettings.MaxRunePageNameLength));
        var changed = false;
        var presets = settings.Presets.Select(p =>
        {
            if (p.RunePageId <= 0 || !names.TryGetValue(p.RunePageId, out var name) || name == p.RunePageName)
                return p;
            changed = true;
            return p with { RunePageName = name };
        }).ToList();
        return changed ? settings with { Presets = presets } : null;
    }

    // --- Log sentences ----------------------------------------------------------------------
    // Champion names come from the client's catalog; never a player's or a rune page's name (the user typed those).

    public static string Completed(string kind, string champion) =>
        kind == AutoPickActionKinds.Ban ? $"Auto-banned {champion}" : $"Auto-locked {champion}";

    public static string HoveredOnly(string champion) => $"Hovered {champion} (too little time to lock in)";

    public static string Cancelled(string kind, string champion) =>
        kind == AutoPickActionKinds.Ban ? $"Cancelled auto-ban of {champion}" : $"Cancelled auto-lock of {champion}";

    public static string Manual(string kind) =>
        kind == AutoPickActionKinds.Ban ? "You chose yourself, auto-ban stepped aside" : "You chose yourself, auto-pick stepped aside";

    public static string NoneAvailable(string kind) =>
        kind == AutoPickActionKinds.Ban ? "No champion from your ban list is available" : "No champion from your pick list is available";

    public static string CompleteFailed(string kind) =>
        kind == AutoPickActionKinds.Ban ? "League didn't accept the auto-ban" : "League didn't accept the auto-lock";

    public static string HoverFailed(string kind) =>
        kind == AutoPickActionKinds.Ban ? "League didn't accept the auto-ban hover" : "League didn't accept the auto-pick hover";

    public const string SpellsSet = "Set your summoner spells";
    public const string SpellsFailed = "League didn't accept your summoner spells";
    public const string RunePageSet = "Switched to your rune page";
    public const string RunePageMissing = "Rune page no longer exists";
    public const string RunePageFailed = "League didn't accept your rune page";

    /// <summary>Fallback when the champion catalog isn't available.</summary>
    public static string ChampionLabel(string? name, int championId) => string.IsNullOrWhiteSpace(name) ? $"champion {championId}" : name;
}
