using System.Net;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>Auto pick never acts over the user's own choice, however late it is seen, and its loop locks on time.</summary>
public sealed class AutoPickUserChoiceTests
{
    [Fact]
    public async Task A_user_hover_seen_only_in_the_fresh_read_before_the_hover_wins()
    {
        using var h = new AutoPickHarness(Draft());
        var rebuild = h.Lcu.Snapshot("ChampSelect");
        h.Lcu.Action(11)["championId"] = 157; // the user hovered as the turn opened, after the rebuild was read

        await h.ObserveAsync(rebuild);
        await h.AdvanceAsync(5);

        Assert.Equal([GetSession], h.Lcu.Requests);
        var entry = Assert.Single(h.Run.Log);
        Assert.Equal((AutoPickLogKinds.Manual, 157), (entry.Kind, entry.ChampionId));
    }

    [Fact]
    public async Task A_turn_that_ended_before_the_fresh_read_is_not_hovered()
    {
        using var h = new AutoPickHarness(Draft());
        var rebuild = h.Lcu.Snapshot("ChampSelect");
        h.Lcu.Action(11)["completed"] = true;
        h.Lcu.Action(11)["isInProgress"] = false;

        await h.ObserveAsync(rebuild);

        Assert.Empty(h.Writes);
        Assert.Empty(h.Run.Log);
    }

    [Fact]
    public async Task Moving_on_to_the_next_choice_reads_again_and_yields_to_a_user_hover()
    {
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [245, 7])]), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();

        h.Lcu.Action(23)["championId"] = 245; // a teammate takes ours…
        var rebuild = h.Lcu.Snapshot("ChampSelect");
        h.Lcu.Action(21)["championId"] = 157; // …and the user hovers their own right after
        await h.ObserveAsync(rebuild);

        Assert.Equal(["PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}"], h.Writes);
        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickLogKinds.Manual, 157), (h.Run.Log[^1].Kind, h.Run.Log[^1].ChampionId));
    }

    [Fact]
    public async Task The_user_going_back_to_an_earlier_auto_hover_is_not_locked_over()
    {
        // Auto-hover Ekko, a teammate takes Ekko so we move on to LeBlanc, the teammate lets go and the user hovers Ekko.
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [245, 7])]), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();
        h.Time.Advance(TimeSpan.FromSeconds(1));
        h.Lcu.Action(23)["championId"] = 245;
        await h.ObserveAsync();
        await h.ObserveAsync(); // a rebuild with LeBlanc hovered
        Assert.Equal(7, h.Run.Pending!.ChampionId);

        h.Lcu.Action(23)["championId"] = 99;
        h.Lcu.Action(21)["championId"] = 245;
        await h.AdvanceAsync(3);

        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":7}",
            ],
            h.Writes);
        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickLogKinds.Manual, 245), (h.Run.Log[^1].Kind, h.Run.Log[^1].ChampionId));
    }

    [Fact]
    public async Task A_user_lock_just_before_ours_is_not_reported_as_a_failure()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        static bool IsLock(string request) => request.Contains("\"completed\":true", StringComparison.Ordinal);
        h.Lcu.Intercept = request =>
        {
            if (!IsLock(request))
                return null;
            // League completed the user's own lock-in a moment earlier, so it refuses ours.
            h.Lcu.Action(11)["championId"] = 157;
            h.Lcu.Action(11)["completed"] = true;
            h.Lcu.Action(11)["isInProgress"] = false;
            return FakeLcu.Error(HttpStatusCode.BadRequest, "refused");
        };

        await h.AdvanceAsync(3);

        Assert.DoesNotContain(h.Run.Log, e => e.Kind == AutoPickLogKinds.Failed);
        Assert.Equal((AutoPickLogKinds.Manual, 157), (h.Run.Log[^1].Kind, h.Run.Log[^1].ChampionId));
        Assert.Null(h.Run.Pending);
    }

    [Fact]
    public async Task LockChampion_succeeds_when_league_refuses_it_because_the_action_was_just_completed()
    {
        using var h = new AutoPickHarness(session: AutoPickSamples.DraftPickTurn);
        h.Lcu.Intercept = request =>
        {
            if (!request.Contains("\"completed\":true", StringComparison.Ordinal))
                return null;
            h.Lcu.Action(21)["championId"] = 245;
            h.Lcu.Action(21)["completed"] = true;
            h.Lcu.Action(21)["isInProgress"] = false;
            return FakeLcu.Error(HttpStatusCode.BadRequest, "refused");
        };
        var executor = new FakeLcuSession(h.Client).Executor(autoPick: h.Service, autoPickStore: h.Store);

        var result = await executor.ExecuteAsync(new RemoteCommand("l1", CommandType.LockChampion, ChampionId: 245, ActionId: 21), default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [GetSession, "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245,\"completed\":true}", GetSession],
            h.Lcu.Requests);
    }

    [Fact]
    public async Task Pausing_while_the_spells_are_sent_stops_the_rune_page_and_the_hover_until_resumed()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [], 4, 14, 12)]));
        h.Lcu.Intercept = request =>
        {
            if (request.StartsWith("PATCH /lol-champ-select/v1/session/my-selection", StringComparison.Ordinal))
                h.Service.Pause();
            return null;
        };

        await h.ObserveAsync();
        Assert.Equal(["PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":14}"], h.Writes);

        h.Lcu.Intercept = null;
        h.Service.Resume();
        await h.ObserveAsync();

        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":14}",
                "PUT /lol-perks/v1/currentpage 12",
                Hover11Zed,
            ],
            h.Writes);
    }

    [Fact]
    public async Task Pausing_before_the_spells_leaves_them_for_resume()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [], 4, 14)]));
        h.Service.Pause();
        await h.ObserveAsync();
        Assert.Empty(h.Lcu.Requests);

        h.Service.Resume();
        await h.ObserveAsync();

        Assert.Equal(["PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":14}"], h.Writes);
    }

    [Fact]
    public async Task The_service_loop_completes_a_pending_action_when_the_clock_reaches_it()
    {
        using var h = new AutoPickHarness(Draft());
        await h.Service.StartAsync(CancellationToken.None);
        try
        {
            var hovered = h.WhenState(s => s.Run?.Pending is not null);
            h.Service.Observe(h.Client, h.Lcu.Snapshot("ChampSelect"));
            await hovered;

            var banned = h.WhenState(s => s.Run?.Log is [.., { Kind: AutoPickLogKinds.Banned }]);
            h.Time.Advance(AutoPickState.AutoLockDelay);
            await banned;

            Assert.Equal(2, h.Writes.Count);
        }
        finally
        {
            await h.Service.StopAsync(CancellationToken.None);
        }
    }
}
