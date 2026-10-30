using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.ChampSelect;

/// <summary>
/// Champ-select rules shared by the state (what the phone may offer) and the commands (what the connector refuses). The
/// bench and reroll rules copy the client's own champ-select front-end (rcp-fe-lol-champ-select, patch 16.21:
/// showChampionBench, showRerollButton, the reroll button's rerollsDisabled).
/// </summary>
internal static class ChampSelectRules
{
    /// <summary>Champion ids &gt; 0, de-duplicated (League uses -1 / 0 as "none" placeholders).</summary>
    public static IReadOnlyList<int> ValidIds(IEnumerable<int> ids) => ids.Where(id => id > 0).Distinct().ToList();

    /// <summary>True when an LCU champion id list has no real champion in it (e.g. <c>[-1]</c> or empty).</summary>
    public static bool IsUnusableIdList(IReadOnlyList<int>? ids) => ids is null || !ids.Any(id => id > 0);

    /// <summary>
    /// The rule behind <see cref="TeamMember.IsLocked"/>: a completed pick, or in a bench mode a dealt champion while that
    /// player has no pick open (bench modes with subset picks pick and lock from a dealt set: then it is a hover).
    /// </summary>
    public static bool IsChampionLocked(bool pickCompleted, bool pickOpen, bool benchEnabled, int championId) =>
        pickCompleted || (benchEnabled && championId > 0 && !pickOpen);

    /// <summary>
    /// The local player's champion once it is theirs for this game by the rule of <see cref="IsChampionLocked"/> (picked and
    /// locked in, or dealt in a bench mode); 0 while they still have a pick to make or no champion.
    /// </summary>
    public static int LocalLockedChampionId(ChampSelectSession session)
    {
        var championId = session.Me?.ChampionId ?? 0;
        if (championId <= 0)
            return 0;

        var myPicks = session.MyActions.Where(a => a.Type == ChampSelectAction.Pick).ToList();
        return IsChampionLocked(myPicks.Any(a => a.Completed), myPicks.Any(a => !a.Completed), session.BenchEnabled, championId)
            ? championId
            : 0;
    }

    // --- Bench (ARAM) ---------------------------------------------------------------------------------------------
    // Bench modes deal everyone a random champion (myTeam[].championId) and share a bench of up to 10 champions anyone may
    // take; "reroll" deals a new random one.

    /// <summary>
    /// The client shows the bench (and takes swaps): <c>benchEnabled</c> and FINALIZATION, or also BAN_PICK when players pick
    /// from a dealt subset (<c>allowSubsetChampionPicks</c>).
    /// </summary>
    public static bool IsBenchOpen(ChampSelectSession session) =>
        session.BenchEnabled
        && (session.Timer.Phase == ChampSelectTimer.Finalization
            || (session.Timer.Phase == ChampSelectTimer.BanPick && session.AllowSubsetChampionPicks));

    /// <summary>Null when <paramref name="championId"/> can be taken from the bench now, else a short reason for the phone.</summary>
    public static string? BenchSwapRefusal(ChampSelectSession session, int championId)
    {
        if (!session.BenchEnabled)
            return "This mode has no bench.";
        if (!IsBenchOpen(session))
            return "The bench isn't open yet.";
        return session.BenchChampionIds.Contains(championId) ? null : "That champion is no longer on the bench.";
    }

    /// <summary>The client shows its Reroll button in this game: <c>allowRerolling</c> and no subset picks.</summary>
    public static bool AllowsRerolling(ChampSelectSession session) => session.AllowRerolling && !session.AllowSubsetChampionPicks;

    /// <summary>
    /// Null when a reroll would be accepted now, else a short reason: like the client's button, only in FINALIZATION, with
    /// rerolls left and no champion trade in progress (its <c>activeChampionSwap</c>).
    /// </summary>
    public static string? RerollRefusal(ChampSelectSession session)
    {
        if (!AllowsRerolling(session))
            return "This mode has no rerolls.";
        if (session.RerollsRemaining <= 0)
            return "No rerolls left.";
        if (session.Timer.Phase != ChampSelectTimer.Finalization)
            return "You can't reroll right now.";
        return session.Swaps.Any(s => s.Kind == SwapKind.Trade && s.State is "SENT" or "RECEIVED")
            ? "Finish the champion trade first."
            : null;
    }

    /// <summary>
    /// Why <see cref="CommandType.DodgeChampSelect"/> would be refused, or null when it may be sent. Custom games aren't
    /// dodged: the team-builder quit is for matchmade drafts, and a custom game's champ select is ended by its lobby leader
    /// instead. Once the timer reaches GAME_STARTING the game is already being created.
    /// </summary>
    public static string? DodgeRefusal(ChampSelectSession session)
    {
        if (session.IsCustomGame)
            return "You can't dodge a custom game.";
        if (session.Timer.Phase == ChampSelectTimer.GameStarting)
            return "The game is already starting.";
        return null;
    }
}
