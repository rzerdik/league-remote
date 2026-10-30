namespace LeagueRemote.Contracts;

/// <summary>
/// The League client's chat as the phone sees it: the party (lobby) and draft (champ select) rooms plus recent direct
/// conversations. Published as its own hub message (<see cref="HubMethods.Client.ChatUpdated"/>) so message history does not
/// ride along with every <see cref="ClientState"/> update. Message bodies are shown on the phone but never logged or pushed.
/// </summary>
/// <param name="Conversations">Party first, then draft, then direct conversations (most recent first).</param>
public sealed record ChatState(IReadOnlyList<ChatConversation> Conversations, DateTimeOffset UpdatedAt)
{
    public static ChatState Empty() => new([], DateTimeOffset.UtcNow);

    /// <summary>The longest message <see cref="CommandType.SendChatMessage"/> accepts (the client's chat input allows 200).</summary>
    public const int MaxMessageLength = 200;

    public const int MaxConversationIdLength = 256;
}

/// <summary>Kinds of conversation League Remote shows.</summary>
public static class ChatKinds
{
    /// <summary>The lobby's room (LCU type "teamBuilder", or "customGame" for custom lobbies). Exists once the party has two members.</summary>
    public const string Party = "party";

    /// <summary>The champ-select room (LCU type "championSelect").</summary>
    public const string ChampSelect = "champ-select";

    /// <summary>A one-to-one conversation with a friend (LCU type "chat").</summary>
    public const string Direct = "direct";
}

/// <param name="Id">LCU conversation id (e.g. <c>{puuid}@eu2.pvp.net</c>), used by the chat commands.</param>
/// <param name="Kind">See <see cref="ChatKinds"/>.</param>
/// <param name="Title">The friend's name for direct conversations; empty for party and draft (the phone labels those).</param>
/// <param name="Unread">Unread messages as the client counts them.</param>
/// <param name="IsMuted">Muted in the client: no unread badge, no sound.</param>
/// <param name="Messages">Recent messages, oldest first (capped; see the connector's chat reader).</param>
/// <param name="Puuid">The friend's puuid for direct conversations (how the phone finds the conversation it asked for with
/// <see cref="CommandType.OpenDirectConversation"/>); empty for party and draft.</param>
public sealed record ChatConversation(
    string Id,
    string Kind,
    string Title,
    int Unread,
    bool IsMuted,
    ChatMessage? LastMessage,
    IReadOnlyList<ChatMessage> Messages,
    string Puuid);

/// <param name="Kind">"chat", or "joined" / "left" when someone entered or left a party room.</param>
/// <param name="SenderName">Riot ID game name as the client shows it; empty when League hides it (e.g. anonymous ranked draft).</param>
/// <param name="Timestamp">When the message was sent; null when League gave none.</param>
/// <param name="SenderCellId">Draft room only: the sender's champ-select cell (<see cref="TeamMember.CellId"/> in
/// <see cref="ChampSelectState.MyTeam"/>), matched by puuid, summoner id or — in anonymous drafts, where League hides
/// names — the obfuscated puuid / summoner id. Lets the phone tell hidden teammates apart by champion and position.
/// Null when unknown (other rooms, or no match).</param>
public sealed record ChatMessage(
    string Id,
    string Kind,
    string Body,
    bool FromMe,
    string SenderName,
    DateTimeOffset? Timestamp,
    long? SenderCellId = null);
