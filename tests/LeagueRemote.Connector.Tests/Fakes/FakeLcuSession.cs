using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Commands;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeagueRemote.Connector.Tests;

/// <summary>An <see cref="ILcuSession"/> around a fake LCU that records which rebuilds and kept conversations were asked for.</summary>
internal sealed class FakeLcuSession(LcuClient? client) : ILcuSession
{
    public LcuClient? Client { get; set; } = client;

    public int Refreshes { get; private set; }

    public int ChatRefreshes { get; private set; }

    public int FriendsRefreshes { get; private set; }

    public List<string> Kept { get; } = [];

    public void RequestRefresh() => Refreshes++;

    public void RequestChatRefresh() => ChatRefreshes++;

    public void RequestFriendsRefresh() => FriendsRefreshes++;

    public event Action? GameflowPhaseChanged;

    /// <summary>What the connection does after publishing a state with a new phase.</summary>
    public void RaiseGameflowPhaseChanged() => GameflowPhaseChanged?.Invoke();

    public void KeepConversation(string conversationId) => Kept.Add(conversationId);

    /// <summary>
    /// A <see cref="CommandExecutor"/> on this session with fresh stores (or the given profile store, clock, automation and
    /// game state).
    /// Without <paramref name="autoPick"/> the automation's settings folder is a temp path that is only created if a test saves
    /// a setting (pass your own, see <c>AutoPickHarness</c>, for that).
    /// </summary>
    public CommandExecutor Executor(
        ProfileStore? profile = null, TimeProvider? time = null, AutoPickService? autoPick = null, AutoPickStore? autoPickStore = null,
        StateStore? state = null)
    {
        time ??= TimeProvider.System;
        autoPickStore ??= new AutoPickStore(time);
        autoPick ??= new AutoPickService(
            new AutoPickSettingsStore(Path.Combine(Path.GetTempPath(), $"lr-autopick-unused-{Guid.NewGuid():N}")),
            autoPickStore, InstantLock(time), time, NullLogger<AutoPickService>.Instance);
        return new(this, state ?? new StateStore(), new ChatStore(), new FriendsStore(), profile ?? new ProfileStore(), new LiveGameStore(),
            autoPickStore, autoPick, InstantLock(time), time, NullLogger<CommandExecutor>.Instance);
    }

    /// <summary>A <see cref="ChampSelectLock"/> that re-reads the session without waiting between reads.</summary>
    public static ChampSelectLock InstantLock(TimeProvider time) => new(time) { ConfirmDelay = TimeSpan.Zero };

    /// <summary>Runs <paramref name="command"/> against a fake LCU served by <paramref name="handler"/>.</summary>
    public static async Task<CommandResult> ExecuteAsync(HttpMessageHandler handler, RemoteCommand command)
    {
        using var http = TestLcu.LcuHttp(handler);
        return await new FakeLcuSession(new LcuClient(http)).Executor().ExecuteAsync(command, CancellationToken.None);
    }
}
