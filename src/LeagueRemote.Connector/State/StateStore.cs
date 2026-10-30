using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Latest <see cref="ClientState"/> for phones. The LCU part (<see cref="Set"/>) and the in-game part
/// (<see cref="SetInGame"/>, from the Live Client Data poller) are written independently into one versioned slice.
/// </summary>
internal sealed class StateStore(TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SliceStore<ClientState> _slice = new(
        ClientState.Disconnected(),
        state => state with { UpdatedAt = default },
        (state, now) => state with { UpdatedAt = now },
        time ?? TimeProvider.System);

    /// <summary>Raised on the writer's thread when a new version is available: handlers only signal.</summary>
    public event Action? Changed
    {
        add => _slice.Changed += value;
        remove => _slice.Changed -= value;
    }

    public (ClientState State, long Version) Snapshot() => _slice.Snapshot();

    /// <summary>The current gameflow phase.</summary>
    public string GameflowPhase => _slice.Snapshot().Value.GameflowPhase;

    /// <summary>Stores the LCU state (its <see cref="ClientState.InGame"/> is ignored); ignored when nothing but the timestamp changed.</summary>
    public bool Set(ClientState state) => _slice.Update(current => state with { InGame = current.InGame });

    /// <summary>Stores the in-game part. The caller decides how often (the game clock changes on every poll).</summary>
    public bool SetInGame(InGameState? inGame) =>
        _slice.Update(current => Equals(current.InGame, inGame) ? current : current with { InGame = inGame, UpdatedAt = _time.GetUtcNow() });

    /// <summary>Forces a republish of the current state.</summary>
    public void Touch() => _slice.Touch();
}
