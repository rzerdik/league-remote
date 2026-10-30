using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>Pausing auto pick, where a run starts and ends, and pending actions that stop being wanted.</summary>
public sealed class AutoPickPauseTests
{
    // --- Pause and runs -------------------------------------------------------------------

    [Theory]
    [InlineData("GameStart")]
    [InlineData("Lobby")] // someone dodged
    [InlineData("None")]
    public async Task Pause_ends_with_the_champ_select(string next)
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        h.Service.Pause();
        Assert.True(h.State.PausedForThisGame);

        await h.ObserveAsync(next);

        Assert.False(h.State.PausedForThisGame);
        Assert.Null(h.State.Run);
    }

    [Fact]
    public async Task Pause_ends_when_the_gameflow_goes_back_to_none()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync("Lobby");
        h.Service.Pause();

        await h.ObserveAsync("None");

        Assert.False(h.State.PausedForThisGame);
    }

    [Fact]
    public async Task A_pause_sent_on_the_home_screen_covers_the_next_champ_select()
    {
        using var h = new AutoPickHarness(Draft());
        h.Service.Pause(); // before the client was even seen
        await h.ObserveAsync("None");
        h.Service.Pause();
        await h.ObserveAsync("None");
        await h.ObserveAsync("Lobby");
        await h.ObserveAsync("ReadyCheck");
        Assert.True(h.State.PausedForThisGame);

        await h.ObserveAsync();

        Assert.Equal(AutoPickRunStatus.Paused, h.Run.Status);
        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task Disconnecting_ends_the_run_and_the_pause_but_keeps_the_settings()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        h.Service.Pause();

        h.Service.OnDisconnected();
        await h.Service.StepAsync(CancellationToken.None);

        Assert.False(h.State.PausedForThisGame);
        Assert.Null(h.State.Run);
        Assert.True(h.State.Settings.Enabled);
        Assert.Single(h.State.Settings.Presets);
    }

    [Fact]
    public async Task A_new_champ_select_starts_a_fresh_run_and_ends_the_pause()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        h.Service.Cancel(null);
        h.Service.Pause();

        h.Lcu.Session = System.Text.Json.Nodes.JsonNode.Parse(AutoPickSamples.DraftBanTurn)!;
        h.Lcu.Session["gameId"] = 7002;
        await h.ObserveAsync();

        Assert.False(h.State.PausedForThisGame);
        Assert.Empty(h.Run.Log);
        Assert.Equal(238, h.Run.Pending!.ChampionId);
        Assert.Equal([Hover11Zed, Hover11Zed], h.Writes);
    }

    // --- A pending action never outlives an active run -----------------------------------

    [Fact]
    public async Task Removing_the_preset_during_the_grace_period_cancels_the_pending_action()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();

        Assert.Null(h.Service.SetPreset(new AutoPickPreset(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [])));
        await h.AdvanceAsync(3);
        await h.AdvanceAsync(3);

        Assert.Null(h.Run.Pending);
        Assert.Equal(AutoPickRunStatus.NoPreset, h.Run.Status);
        Assert.Equal((AutoPickLogKinds.Cancelled, "Cancelled auto-ban of Zed"), (h.Run.Log[^1].Kind, h.Run.Log[^1].Text));
        Assert.Equal([Hover11Zed], h.Writes);
    }

    [Fact]
    public async Task Dropping_the_hovered_champion_from_the_preset_cancels_the_pending_action()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();

        h.Service.SetPreset(new AutoPickPreset(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [157], [103]));
        await h.AdvanceAsync(3);
        await h.ObserveAsync();

        Assert.Null(h.Run.Pending);
        Assert.Equal(AutoPickLogKinds.Cancelled, h.Run.Log[^1].Kind);
        Assert.Equal([Hover11Zed], h.Writes);
    }

    [Fact]
    public async Task A_position_swap_to_a_role_without_a_preset_cancels_the_pending_action()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, Positions.Middle, [238], [])]));
        await h.ObserveAsync();

        h.Lcu.Member(2)["assignedPosition"] = "jungle";
        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickRunStatus.NoPreset, AutoPickLogKinds.Cancelled), (h.Run.Status, h.Run.Log[^1].Kind));
        Assert.Equal([Hover11Zed], h.Writes);
    }

    [Fact]
    public async Task A_missing_session_when_the_action_is_due_drops_it_instead_of_locking()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();

        h.Lcu.SessionGone = true;
        h.Time.Advance(TimeSpan.FromSeconds(3));
        await h.ObserveAsync();

        Assert.Null(h.Run.Pending);
        Assert.Equal([Hover11Zed], h.Writes);
    }

    [Fact]
    public async Task The_loop_does_not_spin_after_a_pending_action_stopped_being_active()
    {
        using var h = new AutoPickHarness(Draft());
        await h.Service.StartAsync(CancellationToken.None);
        try
        {
            var hovered = h.WhenState(s => s.Run?.Pending is not null);
            h.Service.Observe(h.Client, h.Lcu.Snapshot("ChampSelect"));
            await hovered;

            // Removed without waking the loop: it wakes for the due time and must then wait for news, not loop on it.
            h.Service.SetPreset(new AutoPickPreset(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], []));
            var cancelled = h.WhenState(s => s.Run is { Pending: null, Log: [.., { Kind: AutoPickLogKinds.Cancelled }] });
            h.Time.Advance(TimeSpan.FromSeconds(3));
            await cancelled;
            var steps = h.Service.Steps;
            for (var i = 0; i < 5; i++)
                h.Time.Advance(TimeSpan.FromSeconds(1));

            // A rebuild is the next thing that makes it step: once that is through, only that step was added.
            var left = h.WhenState(s => s.Run is null);
            h.Service.Observe(h.Client, h.Lcu.Snapshot("Lobby"));
            await left;

            Assert.InRange(h.Service.Steps, steps + 1, steps + 2);
            Assert.Equal([Hover11Zed], h.Writes);
        }
        finally
        {
            await h.Service.StopAsync(CancellationToken.None);
        }
    }
}
