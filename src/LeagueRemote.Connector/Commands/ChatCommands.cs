using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>Chat: only conversations the client has right now (party, draft, direct), never an arbitrary id.</summary>
internal sealed class ChatCommands(ILcuSession session) : CommandHandler
{
    private const string ChatGone = "That chat is gone.";

    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.SendChatMessage] = RefreshSlice.Chat,
        [CommandType.MarkConversationRead] = RefreshSlice.Chat,
        [CommandType.OpenDirectConversation] = RefreshSlice.Chat,
    };

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        switch (command.Type)
        {
            case CommandType.SendChatMessage:
            {
                if (ChatMapper.Find(await client.GetConversationsAsync(ct), command.ConversationId!) is not { } conversation)
                    return ChatGone;
                await client.SendChatMessageAsync(conversation.Id, command.Body!.Trim(), ct);
                return null;
            }

            case CommandType.MarkConversationRead:
            {
                if (ChatMapper.Find(await client.GetConversationsAsync(ct), command.ConversationId!) is not { } conversation)
                    return ChatGone;
                if (conversation.Json.Long("unreadMessageCount") <= 0)
                    return null;

                // The client marks a conversation read by making it the active one (that opens its chat window), so put
                // back whatever the PC had open: no window stays open on the PC because the phone read a message.
                var previous = (await client.GetActiveConversationAsync(ct))?.Str("id");
                if (previous == conversation.Id)
                    return null;
                await client.SetActiveConversationAsync(conversation.Id, ct);
                if (string.IsNullOrEmpty(previous))
                    await client.ClearActiveConversationAsync(ct);
                else
                    await client.SetActiveConversationAsync(previous, ct);
                return null;
            }

            case CommandType.OpenDirectConversation:
            {
                // Friends only, like the client's "Message"; the conversation id is the friend's chat id.
                var id = ChatMapper.FindDirect(await client.GetConversationsAsync(ct), command.Puuid!)?.Id;
                if (id is null)
                {
                    if (FriendsMapper.FindByPuuid(await client.GetFriendsAsync(ct), command.Puuid!) is not { } friend
                        || FriendsMapper.ChatId(friend) is not { } friendChatId)
                        return "That player isn't on your friends list.";
                    // Not made active (unlike the client's "Message"): that would open a chat window on the PC.
                    await client.CreateDirectConversationAsync(friendChatId, ct);
                    id = friendChatId;
                }

                session.KeepConversation(id);
                return null;
            }

            default:
                return await base.ExecuteAsync(client, command, ct);
        }
    }
}
