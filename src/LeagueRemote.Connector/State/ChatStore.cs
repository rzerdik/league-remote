using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>Latest <see cref="ChatState"/> for phones; a rebuild that only moved <see cref="ChatState.UpdatedAt"/> is no change.</summary>
internal sealed class ChatStore(TimeProvider? time = null) : SliceStore<ChatState>(
    ChatState.Empty(),
    chat => chat with { UpdatedAt = default },
    (chat, now) => chat with { UpdatedAt = now },
    time ?? TimeProvider.System);
