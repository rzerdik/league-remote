using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Latest <see cref="LiveGameState"/> for phones. The version moves only when something but the clock changed (the phone
/// interpolates <see cref="LiveGameState.GameTime"/>), and <see cref="SliceStore{T}.Touch"/> keeps the read's own
/// <see cref="LiveGameState.UpdatedAt"/>, so the clock and its timestamp always stay paired.
/// </summary>
internal sealed class LiveGameStore(TimeProvider? time = null) : SliceStore<LiveGameState>(
    LiveGameState.Empty(),
    game => game with { GameTime = 0, UpdatedAt = default },
    (game, _) => game,
    time ?? TimeProvider.System);
