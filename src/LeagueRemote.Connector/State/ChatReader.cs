using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Reads the client's chat for one LCU connection. The party and draft rooms are re-read on every rebuild (small, and their
/// participants change); a direct conversation's history is only re-read when its last message changed.
/// Not thread-safe (the chat refresh loop is sequential). Never logs message contents.
/// </summary>
/// <param name="kept">Direct conversations to list even beyond the cap (see <see cref="ILcuSession.KeepConversation"/>).</param>
internal sealed class ChatReader(LcuClient client, TimeProvider time, Func<IReadOnlySet<string>>? kept = null)
{
    private readonly Dictionary<string, (string? LastMessageId, JsonElement Messages)> _directHistory = [];
    private string? _myPuuid;

    public async Task<ChatState> BuildAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_myPuuid) && await client.GetChatMeAsync(ct) is { ValueKind: JsonValueKind.Object } me)
            _myPuuid = me.Str("puuid");

        var conversations = await client.GetConversationsAsync(ct);
        var lobby = await client.GetLobbyAsync(ct);
        var session = await client.GetChampSelectSessionAsync(ct);

        var messages = new Dictionary<string, JsonElement>();
        var participants = new Dictionary<string, JsonElement>();
        var keep = kept?.Invoke();
        var picked = ChatMapper.Select(conversations, lobby, session, keep);
        foreach (var conversation in picked)
        {
            if (conversation.Kind == ChatKinds.Direct)
            {
                var lastId = conversation.Json.Obj("lastMessage")?.Str("id");
                if (!_directHistory.TryGetValue(conversation.Id, out var cached) || cached.LastMessageId != lastId)
                    _directHistory[conversation.Id] = cached = (lastId, await GetArrayAsync(client.GetConversationMessagesAsync(conversation.Id, ct)));
                messages[conversation.Id] = cached.Messages;
                continue;
            }

            messages[conversation.Id] = await GetArrayAsync(client.GetConversationMessagesAsync(conversation.Id, ct));
            participants[conversation.Id] = await GetArrayAsync(client.GetConversationParticipantsAsync(conversation.Id, ct));
        }

        foreach (var gone in _directHistory.Keys.Where(id => !messages.ContainsKey(id)).ToList())
            _directHistory.Remove(gone);

        return ChatMapper.Build(new ChatSnapshot(conversations, lobby, session, _myPuuid, messages, participants), time.GetUtcNow(), keep);
    }

    /// <summary>A room can disappear between the list and the read (404 → empty).</summary>
    private static async Task<JsonElement> GetArrayAsync(Task<JsonElement?> request) =>
        await request is { ValueKind: JsonValueKind.Array } array ? array : EmptyArray;

    private static readonly JsonElement EmptyArray = JsonDocument.Parse("[]").RootElement.Clone();
}
