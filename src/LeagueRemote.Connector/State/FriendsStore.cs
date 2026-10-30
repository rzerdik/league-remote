using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>Latest <see cref="FriendsState"/> for phones; a rebuild that only moved <see cref="FriendsState.UpdatedAt"/> is no change.</summary>
internal sealed class FriendsStore(TimeProvider? time = null) : SliceStore<FriendsState>(
    FriendsState.Empty(),
    friends => friends with { UpdatedAt = default },
    (friends, now) => friends with { UpdatedAt = now },
    time ?? TimeProvider.System);
