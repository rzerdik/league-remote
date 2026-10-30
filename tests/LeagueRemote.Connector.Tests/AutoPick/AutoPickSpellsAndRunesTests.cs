using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>Auto pick's summoner spells and rune page, the rune page names kept in the presets, and run ids.</summary>
public sealed class AutoPickSpellsAndRunesTests
{
    [Fact]
    public async Task Spells_and_rune_page_are_set_once_per_champ_select()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true,
            [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [], 4, 14, 12)]));

        await h.ObserveAsync();
        await h.ObserveAsync();
        h.Lcu.Member(2)["spell2Id"] = 3; // the user changes them afterwards
        await h.ObserveAsync();

        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":14}",
                "PUT /lol-perks/v1/currentpage 12",
                Hover11Zed,
            ],
            h.Writes);
        Assert.Equal([AutoPickLogKinds.Spells, AutoPickLogKinds.RunePage], h.Run.Log.Select(e => e.Kind));
    }

    [Fact]
    public async Task Spells_and_rune_page_that_already_match_are_not_written()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true,
            [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [], 4, 6, 11)]));

        await h.ObserveAsync();

        Assert.Empty(h.Lcu.Requests);
        Assert.Empty(h.Run.Log);
    }

    [Fact]
    public async Task A_deleted_rune_page_is_skipped()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [], RunePageId: 99)]));

        await h.ObserveAsync();
        await h.ObserveAsync();

        Assert.Empty(h.Lcu.Requests);
        var entry = Assert.Single(h.Run.Log);
        Assert.Equal((AutoPickLogKinds.Skipped, "Rune page no longer exists"), (entry.Kind, entry.Text));
    }

    [Fact]
    public async Task A_preset_saved_while_League_runs_keeps_its_rune_page_name()
    {
        using var h = new AutoPickHarness();
        await h.ObserveAsync("None");

        Assert.Null(h.Service.SetPreset(new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [103], RunePageId: 12)));

        Assert.Equal("Page B", Assert.Single(h.State.Settings.Presets).RunePageName);
        Assert.Equal("Page B", Assert.Single(new AutoPickSettingsStore(h.Folder).Current.Presets).RunePageName);
    }

    [Fact]
    public async Task A_preset_saved_while_League_is_closed_keeps_the_rune_page_name_it_knew()
    {
        using var h = new AutoPickHarness();
        await h.ObserveAsync("None");
        h.Service.SetPreset(new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [103], RunePageId: 12));
        h.Service.OnDisconnected();

        h.Service.SetPreset(new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [99], RunePageId: 12));
        h.Service.SetPreset(new(AutoPickQueueGroups.Draft, Positions.Middle, [], [7], RunePageId: 12));
        h.Service.SetPreset(new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [7], RunePageId: 77));

        Assert.Equal(["Page B", "Page B", ""], h.State.Settings.Presets.Select(p => p.RunePageName));
    }

    [Fact]
    public async Task Rune_page_names_follow_the_client_and_older_presets_get_theirs()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [], RunePageId: 11),
            new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [103], RunePageId: 12, RunePageName: "Old name"),
        ]));

        await h.ObserveAsync("None");
        Assert.Equal(["Page A", "Page B"], h.State.Settings.Presets.Select(p => p.RunePageName));

        h.Lcu.RunePages = """[{ "id": 11, "name": "Mid Electrocute", "current": true, "isEditable": true }]""";
        await h.ObserveAsync("None");
        Assert.Equal(["Mid Electrocute", "Page B"], h.State.Settings.Presets.Select(p => p.RunePageName)); // page 12 is gone: kept
        Assert.Equal(["Mid Electrocute", "Page B"], new AutoPickSettingsStore(h.Folder).Current.Presets.Select(p => p.RunePageName));

        var version = h.Store.Snapshot().Version;
        await h.ObserveAsync("None");
        Assert.Equal(version, h.Store.Snapshot().Version); // nothing changed: nothing saved or republished
        Assert.Empty(h.Writes);
    }

    [Fact]
    public async Task Rune_page_names_that_cant_be_saved_are_tried_again_later_not_with_every_rebuild()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [], RunePageId: 11)]));
        var blocker = Path.Combine(h.Folder, AutoPickSettingsStore.FileName + ".tmp");
        Directory.CreateDirectory(blocker); // the atomic write's temp file can't be created

        await h.ObserveAsync("None");
        Directory.Delete(blocker);
        await h.ObserveAsync("None");
        Assert.Equal("", Assert.Single(h.State.Settings.Presets).RunePageName);

        h.Time.Advance(AutoPick.AutoPickService.NameWriteRetryDelay);
        await h.ObserveAsync("None");
        Assert.Equal("Page A", Assert.Single(h.State.Settings.Presets).RunePageName);
    }

    [Fact]
    public void Rune_page_names_are_bounded_and_dropped_without_a_page()
    {
        var settings = AutoPickSettingsStore.Normalize(new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [], RunePageId: 11, RunePageName: new string('x', 100)),
            new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [103], RunePageName: "No page"),
            new(AutoPickQueueGroups.Aram, AutoPickRoles.Any, [], [], RunePageId: 12, RunePageName: null!),
        ]));

        Assert.Equal([AutoPickSettings.MaxRunePageNameLength, 0, 0], settings.Presets.Select(p => p.RunePageName.Length));
    }

    [Fact]
    public async Task Each_champ_select_gets_a_new_larger_run_id_that_stays_for_the_run()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        var first = h.Run.Id;
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.ObserveAsync();
        Assert.Equal(first, h.Run.Id);

        h.Lcu.Session["gameId"] = 7002; // dodge, then a new champ select without a phase in between
        await h.ObserveAsync();
        var second = h.Run.Id;
        await h.ObserveAsync("InProgress");
        h.Lcu.Session["gameId"] = 7003;
        await h.ObserveAsync();
        var third = h.Run.Id;

        // A restarted connector: its runs still come after the earlier ones.
        h.Time.Advance(TimeSpan.FromSeconds(1));
        var restarted = new AutoPick.AutoPickService(h.Settings, h.Store, FakeLcuSession.InstantLock(h.Time), h.Time,
            NullLogger<AutoPick.AutoPickService>.Instance);
        restarted.Observe(h.Client, h.Lcu.Snapshot("ChampSelect"));
        await restarted.StepAsync(CancellationToken.None);
        var afterRestart = h.Run.Id;

        Assert.True(first > 0);
        Assert.True(first < second && second < third && third < afterRestart, $"{first} {second} {third} {afterRestart}");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task After_a_position_swap_spells_follow_the_new_role_only_if_the_user_left_them(bool userChanged, bool reapplied)
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Draft, Positions.Jungle, [], [], 4, 11),
            new(AutoPickQueueGroups.Draft, Positions.Middle, [], [], 4, 14),
        ]));
        await h.ObserveAsync();
        if (userChanged)
        {
            h.Lcu.Member(2)["spell2Id"] = 3;
            await h.ObserveAsync();
        }

        h.Lcu.Member(2)["assignedPosition"] = "jungle";
        await h.ObserveAsync();

        Assert.Equal(Positions.Jungle, h.Run.Role);
        var expected = new List<string> { "PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":14}" };
        if (reapplied)
            expected.Add("PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":4,\"spell2Id\":11}");
        Assert.Equal(expected, h.Writes);
    }

    [Fact]
    public async Task Aram_sets_spells_but_never_touches_the_champion()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Aram, AutoPickRoles.Any, [], [], 32, 4)]),
            AutoPickSamples.AramSession);

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Equal(["PATCH /lol-champ-select/v1/session/my-selection {\"spell1Id\":32,\"spell2Id\":4}"], h.Writes);
        Assert.Equal((AutoPickQueueGroups.Aram, AutoPickRoles.Any, AutoPickRunStatus.Active), (h.Run.QueueGroup, h.Run.Role, h.Run.Status));
    }
}
