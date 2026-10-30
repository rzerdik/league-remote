using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>The auto-pick commands through the executor: settings, pause, cancel, and saving them.</summary>
public sealed class AutoPickCommandTests
{
    [Fact]
    public async Task Auto_pick_commands_work_without_a_League_client_and_are_saved()
    {
        using var h = new AutoPickHarness();
        var executor = new FakeLcuSession(null).Executor(time: h.Time, autoPick: h.Service, autoPickStore: h.Store);

        Assert.True((await executor.ExecuteAsync(new RemoteCommand("a1", CommandType.SetAutoPickEnabled, Enabled: true), default)).Success);
        var preset = await executor.ExecuteAsync(new RemoteCommand("a2", CommandType.SetAutoPickPreset, Spell1Id: 4, Spell2Id: 14,
            QueueGroup: AutoPickQueueGroups.Draft, Role: Positions.Middle, PickChampionIds: [103, 99]), default);
        Assert.True(preset.Success, preset.Error);
        Assert.True((await executor.ExecuteAsync(new RemoteCommand("a3", CommandType.PauseAutoPick), default)).Success);

        Assert.True(h.State.Settings.Enabled);
        Assert.True(h.State.PausedForThisGame);
        Assert.Equal(new AutoPickPreset(AutoPickQueueGroups.Draft, Positions.Middle, [], [103, 99], 4, 14), Assert.Single(h.State.Settings.Presets),
            AutoPickPresetComparer.Instance);
        var reloaded = new AutoPickSettingsStore(h.Folder).Current;
        Assert.True(reloaded.Enabled);
        Assert.Equal([103, 99], Assert.Single(reloaded.Presets).PickChampionIds);

        Assert.True((await executor.ExecuteAsync(new RemoteCommand("a4", CommandType.ResumeAutoPick), default)).Success);
        Assert.False(h.State.PausedForThisGame);

        // Nothing set: the preset is removed.
        Assert.True((await executor.ExecuteAsync(new RemoteCommand("a5", CommandType.SetAutoPickPreset,
            QueueGroup: AutoPickQueueGroups.Draft, Role: Positions.Middle), default)).Success);
        Assert.Empty(h.State.Settings.Presets);
        Assert.Empty(new AutoPickSettingsStore(h.Folder).Current.Presets);
    }

    [Fact]
    public async Task Executor_revalidates_presets_and_changes_nothing_when_refused()
    {
        using var h = new AutoPickHarness();
        var executor = new FakeLcuSession(null).Executor(autoPick: h.Service, autoPickStore: h.Store);

        var result = await executor.ExecuteAsync(new RemoteCommand("a1", CommandType.SetAutoPickPreset,
            QueueGroup: AutoPickQueueGroups.Aram, Role: AutoPickRoles.Any, PickChampionIds: [103]), default);

        Assert.Equal((false, "ARAM presets have no ban or pick list."), (result.Success, result.Error));
        Assert.Empty(h.State.Settings.Presets);
        Assert.False(Directory.Exists(h.Folder));
    }

    [Fact]
    public async Task Cancel_refuses_without_a_pending_action_or_for_another_one()
    {
        using var h = new AutoPickHarness(Draft());
        var executor = new FakeLcuSession(h.Client).Executor(autoPick: h.Service, autoPickStore: h.Store);

        var nothing = await executor.ExecuteAsync(new RemoteCommand("c1", CommandType.CancelAutoPickAction), default);
        Assert.Equal("Nothing to cancel.", nothing.Error);

        await h.ObserveAsync();
        var other = await executor.ExecuteAsync(new RemoteCommand("c2", CommandType.CancelAutoPickAction, ActionId: 21), default);
        Assert.Equal("That auto pick is already done.", other.Error);
        Assert.NotNull(h.Run.Pending);

        var ok = await executor.ExecuteAsync(new RemoteCommand("c3", CommandType.CancelAutoPickAction, ActionId: 11), default);
        Assert.True(ok.Success, ok.Error);
        Assert.Null(h.Run.Pending);
        Assert.Equal([Hover11Zed], h.Writes); // the hover stays; nothing else is sent
    }

    [Fact]
    public async Task A_settings_file_that_cant_be_written_fails_the_command_and_keeps_the_settings()
    {
        using var h = new AutoPickHarness();
        File.WriteAllText(h.Folder, "not a folder");
        try
        {
            var executor = new FakeLcuSession(null).Executor(autoPick: h.Service, autoPickStore: h.Store);

            var result = await executor.ExecuteAsync(new RemoteCommand("a1", CommandType.SetAutoPickPreset,
                QueueGroup: AutoPickQueueGroups.Draft, Role: AutoPickRoles.Any, BanChampionIds: [238]), default);

            Assert.Equal("Couldn't save the preset on the PC.", result.Error);
            Assert.Empty(h.State.Settings.Presets);
        }
        finally
        {
            File.Delete(h.Folder);
        }
    }

    [Fact]
    public async Task RefreshState_republishes_auto_pick_and_enabling_asks_for_a_fresh_rebuild()
    {
        using var h = new AutoPickHarness();
        var session = new FakeLcuSession(h.Client);
        var executor = session.Executor(autoPick: h.Service, autoPickStore: h.Store);
        var before = h.Store.Snapshot().Version;

        await executor.ExecuteAsync(new RemoteCommand("r1", CommandType.RefreshState), default);
        Assert.Equal(before + 1, h.Store.Snapshot().Version);

        var refreshes = session.Refreshes;
        await executor.ExecuteAsync(new RemoteCommand("r2", CommandType.SetAutoPickEnabled, Enabled: true), default);
        Assert.Equal(refreshes + 1, session.Refreshes);
        Assert.Empty(h.Lcu.Requests);
    }
}
