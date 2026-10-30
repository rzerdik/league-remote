using System.Globalization;
using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Raw LCU chat data for one rebuild. <see cref="Messages"/> and <see cref="Participants"/> are keyed by conversation id and
/// only hold the conversations <see cref="ChatMapper.Select"/> picked.
/// </summary>
internal sealed record ChatSnapshot(
    JsonElement? Conversations,
    JsonElement? Lobby,
    JsonElement? ChampSelectSession,
    string? MyPuuid,
    IReadOnlyDictionary<string, JsonElement> Messages,
    IReadOnlyDictionary<string, JsonElement> Participants);

/// <summary>A conversation the phone gets, with the LCU JSON it came from.</summary>
internal sealed record PickedConversation(JsonElement Json, string Id, string Kind);

/// <summary>
/// Pure mapping from LCU chat JSON to <see cref="ChatState"/>. The party and draft rooms are found the way the client's
/// own social plugin finds them (rcp-fe-lol-social, game rooms): the conversation whose id starts with the lobby's
/// <c>multiUserChatId</c> and has type "teamBuilder" (or "customGame" for custom lobbies), and the one whose id starts with
/// the champ-select session's <c>chatDetails.multiUserChatId</c> and has type "championSelect".
/// Message bodies are passed through to the phone and must never be logged.
/// </summary>
internal static class ChatMapper
{
    /// <summary>Direct conversations sent to the phone (most recent first).</summary>
    public const int MaxDirectConversations = 20;

    /// <summary>Messages kept per conversation (the newest ones).</summary>
    public const int MaxMessages = 50;

    /// <summary>Incoming bodies are cut here; the client itself only lets players type 200 characters.</summary>
    public const int MaxBodyLength = 500;

    public static readonly IReadOnlySet<string> PartyTypes = new HashSet<string> { "teamBuilder", "customGame" };
    public const string ChampSelectType = "championSelect";
    public const string DirectType = "chat";

    public static ChatState Build(ChatSnapshot s, DateTimeOffset now, IReadOnlySet<string>? kept = null)
    {
        var picked = Select(s.Conversations, s.Lobby, s.ChampSelectSession, kept);
        var names = PlayerNames(s);
        var team = DraftTeam.From(ChampSelectSession.Parse(s.ChampSelectSession));
        return new ChatState([.. picked.Select(p => MapConversation(p, s, names, team))], now);
    }

    /// <summary>
    /// Party room, draft room, then the <see cref="MaxDirectConversations"/> direct conversations with the most recent
    /// messages, plus any <paramref name="kept"/> one beyond that (opened from the phone, possibly still without messages).
    /// </summary>
    public static IReadOnlyList<PickedConversation> Select(
        JsonElement? conversations, JsonElement? lobby, JsonElement? champSelectSession, IReadOnlySet<string>? kept = null)
    {
        var all = conversations.Objects().Where(c => !string.IsNullOrEmpty(c.Str("id"))).ToList();
        var picked = new List<PickedConversation>();

        if (lobby is { ValueKind: JsonValueKind.Object } l
            && FindRoom(all, l.Str("multiUserChatId"), PartyTypes) is { } party)
            picked.Add(new PickedConversation(party, party.Str("id")!, ChatKinds.Party));

        if (champSelectSession is { ValueKind: JsonValueKind.Object } session
            && FindRoom(all, session.Obj("chatDetails")?.Str("multiUserChatId"), new HashSet<string> { ChampSelectType }) is { } draft)
            picked.Add(new PickedConversation(draft, draft.Str("id")!, ChatKinds.ChampSelect));

        // Conversations without a message (opened, never written in) sort last.
        picked.AddRange(all
            .Where(c => c.Str("type") == DirectType)
            .OrderByDescending(c => LastMessage(c) is { } m ? Timestamp(m) : null)
            .Where((c, i) => i < MaxDirectConversations || kept?.Contains(c.Str("id")!) == true)
            .Select(c => new PickedConversation(c, c.Str("id")!, ChatKinds.Direct)));
        return picked;
    }

    /// <summary>The direct conversation with <paramref name="puuid"/> (its id is the friend's chat id, <c>{puuid}@{domain}</c>).</summary>
    public static PickedConversation? FindDirect(JsonElement? conversations, string puuid) =>
        conversations.Objects()
            .Where(c => c.Str("type") == DirectType && !string.IsNullOrEmpty(c.Str("id"))
                        && string.Equals(DirectPuuid(c), puuid, StringComparison.OrdinalIgnoreCase))
            .Select(c => new PickedConversation(c, c.Str("id")!, ChatKinds.Direct))
            .FirstOrDefault();

    /// <summary>A conversation League Remote may act on (send to / mark read), found by id; null otherwise.</summary>
    public static PickedConversation? Find(JsonElement? conversations, string id) =>
        conversations.Objects()
            .Where(c => c.Str("id") == id)
            .Select(c => KindOf(c.Str("type")) is { } kind ? new PickedConversation(c, id, kind) : null)
            .FirstOrDefault();

    private static string? KindOf(string? type) => type switch
    {
        ChampSelectType => ChatKinds.ChampSelect,
        DirectType => ChatKinds.Direct,
        _ when type is not null && PartyTypes.Contains(type) => ChatKinds.Party,
        _ => null,
    };

    /// <summary>
    /// The room of one of <paramref name="types"/> whose id starts with <paramref name="mucId"/>. Without a muc id (older
    /// clients) the first room of that type; League only keeps the current one.
    /// </summary>
    private static JsonElement? FindRoom(List<JsonElement> all, string? mucId, IReadOnlySet<string> types)
    {
        foreach (var c in all)
        {
            if (c.Str("type") is not { } type || !types.Contains(type))
                continue;
            if (string.IsNullOrEmpty(mucId) || c.Str("id")!.StartsWith(mucId, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static ChatConversation MapConversation(
        PickedConversation p, ChatSnapshot s, IReadOnlyDictionary<string, string> names, DraftTeam team)
    {
        var c = p.Json;
        var draftTeam = p.Kind == ChatKinds.ChampSelect ? team : null;
        var messages = s.Messages.TryGetValue(p.Id, out var raw) && raw.ValueKind == JsonValueKind.Array
            ? raw.EnumerateArray()
                .Select(m => MapMessage(m, p.Kind, s.MyPuuid, names, draftTeam))
                .OfType<ChatMessage>()
                .TakeLast(MaxMessages)
                .ToList()
            : [];

        // The newest chat line; LCU's lastMessage can be a system line or come without the history.
        var last = messages.LastOrDefault(m => m.Kind == "chat")
                   ?? (LastMessage(c) is { } lm ? MapMessage(lm, p.Kind, s.MyPuuid, names, draftTeam) : null);

        return new ChatConversation(
            Id: p.Id,
            Kind: p.Kind,
            Title: p.Kind == ChatKinds.Direct ? c.Str("gameName") ?? c.Str("name") ?? "" : "",
            Unread: (int)Math.Clamp(c.Long("unreadMessageCount"), 0, int.MaxValue),
            IsMuted: c.Bool("isMuted"),
            LastMessage: last is { Kind: "chat" } ? last : null,
            Messages: messages,
            Puuid: p.Kind == ChatKinds.Direct ? DirectPuuid(c) ?? "" : "");
    }

    /// <summary>
    /// Chat lines (type "chat" in direct conversations, "groupchat" in party and draft rooms; seen live); in party rooms
    /// also "joined" / "left". Other system lines (celebrations, notices) are dropped. With <paramref name="team"/> (draft
    /// room) the sender is matched to their champ-select cell.
    /// </summary>
    public static ChatMessage? MapMessage(
        JsonElement m, string conversationKind, string? myPuuid, IReadOnlyDictionary<string, string> names, DraftTeam? team = null)
    {
        if (m.ValueKind != JsonValueKind.Object)
            return null;

        var type = m.Str("type");
        var body = m.Str("body") ?? "";
        var kind = (type, body) switch
        {
            ("chat" or "groupchat", _) when !string.IsNullOrWhiteSpace(body) => "chat",
            ("system", "joined_room") when conversationKind == ChatKinds.Party => "joined",
            ("system", "left_room") when conversationKind == ChatKinds.Party => "left",
            _ => null,
        };
        if (kind is null)
            return null;

        var puuid = SenderPuuid(m);
        var fromMe = !string.IsNullOrEmpty(myPuuid) && string.Equals(puuid, myPuuid, StringComparison.OrdinalIgnoreCase);
        var cell = team?.CellOf(m);
        var name = puuid is not null && names.TryGetValue(puuid, out var known) ? known
            : cell is { } c && team!.Names.TryGetValue(c, out var shown) ? shown
            : "";
        return new ChatMessage(
            Id: m.Str("id") ?? "",
            Kind: kind,
            Body: kind == "chat" ? Text.Clip(body, MaxBodyLength) : "",
            FromMe: fromMe,
            SenderName: name,
            Timestamp: Timestamp(m),
            SenderCellId: cell);
    }

    /// <summary>
    /// puuid → game name from what the client shows: room participants, lobby members and direct-conversation partners.
    /// In anonymous drafts League sends no names; those senders stay unnamed.
    /// </summary>
    private static Dictionary<string, string> PlayerNames(ChatSnapshot s)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? puuid, string? name)
        {
            if (!string.IsNullOrEmpty(puuid) && !string.IsNullOrWhiteSpace(name))
                names.TryAdd(puuid, name);
        }

        foreach (var participants in s.Participants.Values)
            if (participants.ValueKind == JsonValueKind.Array)
                foreach (var p in participants.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object))
                    Add(p.Str("puuid"), p.Str("gameName"));

        if (s.Lobby is { ValueKind: JsonValueKind.Object } lobby)
            foreach (var member in lobby.Arr("members"))
                Add(member.Str("puuid"), member.Str("gameName"));

        foreach (var c in s.Conversations.Objects().Where(c => c.Str("type") == DirectType))
            Add(DirectPuuid(c), c.Str("gameName"));

        return names;
    }

    /// <summary>
    /// The champ-select session's own team, keyed by every id a draft-room message can carry. Recorded draft messages
    /// name the sender by <c>fromPuuid</c> / <c>fromPid</c> / <c>fromId</c> and <c>fromSummonerId</c>; the client's
    /// <c>/help</c> also lists <c>fromObfuscatedPuuid</c> / <c>fromObfuscatedSummonerId</c> on messages and
    /// <c>obfuscatedPuuid</c> / <c>obfuscatedSummonerId</c> / <c>nameVisibilityType</c> on <c>myTeam</c> entries, which
    /// is what an anonymous (ranked) draft leaves when it hides puuids and names. Enemies are never in here.
    /// </summary>
    internal sealed class DraftTeam
    {
        private readonly Dictionary<string, long> _cells = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Cell → game name, only for teammates whose name League shows (<see cref="ChampSelectTeammate.VisibleGameName"/>).</summary>
        public Dictionary<long, string> Names { get; } = [];

        public static DraftTeam From(ChampSelectSession? session)
        {
            var team = new DraftTeam();
            foreach (var p in session?.MyTeam ?? [])
            {
                team.Add("p:" + p.Puuid, p.CellId);
                team.Add("o:" + p.ObfuscatedPuuid, p.CellId);
                team.Add("s:" + Id(p.SummonerId), p.CellId);
                team.Add("os:" + Id(p.ObfuscatedSummonerId), p.CellId);
                if (p.VisibleGameName is { Length: > 0 } name)
                    team.Names.TryAdd(p.CellId, name);
            }

            return team;
        }

        /// <summary>The sender's cell, or null when none of its ids is on the team.</summary>
        public long? CellOf(JsonElement m)
        {
            string?[] ids = [m.Str("fromPuuid"), PuuidOf(m.Str("fromPid")), PuuidOf(m.Str("fromId"))];
            string[] keys =
            [
                .. ids.Where(id => !string.IsNullOrEmpty(id)).SelectMany(id => new[] { "p:" + id, "o:" + id }),
                "o:" + m.Str("fromObfuscatedPuuid"),
                "s:" + Id(m.Long("fromSummonerId")),
                "os:" + Id(m.Long("fromObfuscatedSummonerId")),
            ];
            foreach (var key in keys)
                if (_cells.TryGetValue(key, out var cell))
                    return cell;
            return null;
        }

        /// <summary>Empty values (no puuid, summoner id 0) never match.</summary>
        private void Add(string key, long cell)
        {
            if (!key.EndsWith(':'))
                _cells.TryAdd(key, cell);
        }

        private static string Id(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : "";
    }

    private static string? SenderPuuid(JsonElement m) =>
        m.Str("fromPuuid") is { Length: > 0 } puuid ? puuid : PuuidOf(m.Str("fromPid") ?? m.Str("fromId"));

    /// <summary>The friend's puuid of a direct conversation (from its <c>pid</c>, else its id).</summary>
    private static string? DirectPuuid(JsonElement conversation) => PuuidOf(conversation.Str("pid") ?? conversation.Str("id"));

    /// <summary>"{puuid}@eu2.pvp.net" → puuid.</summary>
    private static string? PuuidOf(string? jid) =>
        string.IsNullOrEmpty(jid) ? null : jid.Split('@', 2)[0];

    private static JsonElement? LastMessage(JsonElement c) => c.Obj("lastMessage");

    private static DateTimeOffset? Timestamp(JsonElement m) =>
        DateTimeOffset.TryParse(m.Str("timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;
}
