using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Latest <see cref="AutoPickState"/> for phones, written by <see cref="AutoPick.AutoPickService"/> only. Touch restamps it
/// with the connector clock, which keeps the phone's countdown
/// (<see cref="AutoPickPending.CompletesAt"/> − <see cref="AutoPickState.UpdatedAt"/>) right.
/// </summary>
internal sealed class AutoPickStore(TimeProvider time) : SliceStore<AutoPickState>(
    AutoPickState.Empty(),
    state => state with { UpdatedAt = default },
    (state, now) => state with { UpdatedAt = now },
    time);
