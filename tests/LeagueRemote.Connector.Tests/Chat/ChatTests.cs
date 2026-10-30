using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

public sealed class ChatTests
{
    private static async Task<ChatState> Read(FakeLcu handler)
    {
        using var http = handler.CreateClient();
        return await new ChatReader(new LcuClient(http), TimeProvider.System).BuildAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Lists_party_then_draft_then_direct_conversations_by_most_recent()
    {
        var chat = await Read(ChatSamples.Handler());

        Assert.Equal(
            [
                (ChatSamples.PartyId, ChatKinds.Party),
                (ChatSamples.DraftId, ChatKinds.ChampSelect),
                (ChatSamples.FriendId, ChatKinds.Direct),
                (ChatSamples.OldFriendId, ChatKinds.Direct),
            ],
            chat.Conversations.Select(c => (c.Id, c.Kind)));
        Assert.Equal(["", "", "Friend", "OldFriend"], chat.Conversations.Select(c => c.Title));
        Assert.Equal([1, 3, 2, 0], chat.Conversations.Select(c => c.Unread));
    }

    [Fact]
    public async Task Party_room_shows_who_joined_and_who_wrote()
    {
        var party = (await Read(ChatSamples.Handler())).Conversations[0];

        Assert.Equal(
            [
                ("joined", "", true, "Me"),
                ("joined", "", false, "Partner"),
                ("chat", "hey", true, "Me"),
                ("chat", "inv me", false, "Partner"),
                ("left", "", false, "Partner"),
            ],
            party.Messages.Select(m => (m.Kind, m.Body, m.FromMe, m.SenderName)));
        Assert.Equal("inv me", party.LastMessage?.Body);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T18:01:05Z"), party.LastMessage?.Timestamp);
    }

    [Fact]
    public async Task Draft_room_drops_joins_and_notices_and_keeps_hidden_senders_unnamed()
    {
        var draft = (await Read(ChatSamples.Handler())).Conversations[1];

        Assert.Equal(
            [("mid", false, "Ally"), ("can I swap?", false, "")],
            draft.Messages.Select(m => (m.Body, m.FromMe, m.SenderName)));
    }

    [Fact]
    public async Task Draft_senders_are_matched_to_their_champ_select_cell_even_when_hidden()
    {
        var handler = ChatSamples.Handler();
        handler.Routes["/lol-champ-select/v1/session"] = ChatSamples.AnonymousChampSelectSession;
        handler.Routes[ChatSamples.DraftPath + "/messages"] = ChatSamples.AnonymousDraftMessages;

        var draft = (await Read(handler)).Conversations.Single(c => c.Kind == ChatKinds.ChampSelect);

        // Hidden teammates stay unnamed (even when a name slips into a HIDDEN entry); an id off the team maps to nothing.
        Assert.Equal(
            [("adc", true, "Me", 2L), ("top", false, "Ally", 1L), ("mid", false, "", 3L), ("can I swap?", false, "", 4L), ("hi", false, "", (long?)null)],
            draft.Messages.Select(m => (m.Body, m.FromMe, m.SenderName, m.SenderCellId)));
    }

    [Fact]
    public async Task Only_draft_messages_carry_a_sender_cell()
    {
        var handler = ChatSamples.Handler();
        handler.Routes["/lol-champ-select/v1/session"] = ChatSamples.AnonymousChampSelectSession;

        var state = await Read(handler);

        Assert.All(state.Conversations.Where(c => c.Kind != ChatKinds.ChampSelect).SelectMany(c => c.Messages),
            m => Assert.Null(m.SenderCellId));
        Assert.Equal([1L, 3L], state.Conversations.Single(c => c.Kind == ChatKinds.ChampSelect).Messages.Select(m => m.SenderCellId));
    }

    [Fact]
    public async Task Direct_conversation_names_the_friend_and_marks_own_lines()
    {
        var friend = (await Read(ChatSamples.Handler())).Conversations[2];

        Assert.Equal([("hi", true, "Me"), ("want to play?", false, "Friend")], friend.Messages.Select(m => (m.Body, m.FromMe, m.SenderName)));
    }

    [Fact]
    public async Task Without_lobby_or_champ_select_only_direct_conversations_remain()
    {
        var handler = ChatSamples.Handler();
        handler.Routes.Remove("/lol-lobby/v2/lobby");
        handler.Routes.Remove("/lol-champ-select/v1/session");

        var chat = await Read(handler);

        Assert.Equal(2, chat.Conversations.Count);
        Assert.All(chat.Conversations, c => Assert.Equal(ChatKinds.Direct, c.Kind));
    }

    [Fact]
    public async Task Stale_champ_select_room_of_an_earlier_game_is_ignored()
    {
        var handler = ChatSamples.Handler();
        handler.Routes["/lol-champ-select/v1/session"] = ChatSamples.ChampSelectSession.Replace(ChatSamples.DraftMuc, "c1~other");

        var chat = await Read(handler);

        Assert.DoesNotContain(chat.Conversations, c => c.Kind == ChatKinds.ChampSelect);
    }

    [Fact]
    public async Task Direct_history_is_only_reread_when_its_last_message_changes()
    {
        var handler = ChatSamples.Handler();
        using var http = handler.CreateClient();
        var reader = new ChatReader(new LcuClient(http), TimeProvider.System);

        await reader.BuildAsync(CancellationToken.None);
        await reader.BuildAsync(CancellationToken.None);
        handler.Routes["/lol-chat/v1/conversations"] = ChatSamples.Conversations.Replace("1792632130896:85", "1792632130999:86");
        await reader.BuildAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count(r => r == $"GET {ChatSamples.FriendPath}/messages"));
        Assert.Equal(1, handler.Requests.Count(r => r == $"GET {ChatSamples.OldFriendPath}/messages"));
        Assert.Equal(3, handler.Requests.Count(r => r == $"GET {ChatSamples.PartyPath}/messages"));
        Assert.Equal(1, handler.Requests.Count(r => r == "GET /lol-chat/v1/me"));
    }

    [Fact]
    public void Long_bodies_are_cut_and_history_is_capped()
    {
        var lines = Enumerable.Range(0, ChatMapper.MaxMessages + 5)
            .Select(i => JsonSerializer.Serialize(new
            {
                id = $"m{i}",
                type = "chat",
                body = new string('x', i == ChatMapper.MaxMessages + 4 ? 900 : 3),
                fromPuuid = "p",
            }));
        var snapshot = new ChatSnapshot(
            Json(ChatSamples.Conversations), null, null, "me-puuid",
            new Dictionary<string, JsonElement> { [ChatSamples.FriendId] = Json($"[{string.Join(",", lines)}]") },
            new Dictionary<string, JsonElement>());

        var friend = ChatMapper.Build(snapshot, DateTimeOffset.UnixEpoch).Conversations.Single(c => c.Id == ChatSamples.FriendId);

        Assert.Equal(ChatMapper.MaxMessages, friend.Messages.Count);
        Assert.Equal("m5", friend.Messages[0].Id);
        Assert.Equal(ChatMapper.MaxBodyLength, friend.Messages[^1].Body.Length);
    }

    [Fact]
    public void Chat_events_only_rebuild_chat_and_lobby_or_phase_events_rebuild_both()
    {
        Assert.Equal((false, true, false, false), LcuConnectionService.RefreshesFor(LcuTopics.Chat));
        Assert.Equal((true, true, false, false), LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-lobby_v2_lobby"));
        Assert.Equal((true, true, false, false), LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-gameflow_v1_gameflow-phase"));
        Assert.Equal((true, false, false, false), LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-champ-select_v1_session"));
        Assert.Contains(LcuTopics.Chat, LcuTopics.All);
    }

    [Fact]
    public async Task Direct_conversations_carry_the_friends_puuid()
    {
        var chat = await Read(ChatSamples.Handler());

        Assert.Equal(["", "", "friend-puuid", "old-friend-puuid"], chat.Conversations.Select(c => c.Puuid));
    }

    [Fact]
    public async Task A_conversation_opened_but_never_written_in_comes_last_without_messages()
    {
        var handler = ChatSamples.Handler();
        handler.Routes["/lol-chat/v1/conversations"] = ChatSamples.Conversations.TrimEnd()[..^1] + "," + ChatSamples.OpenedConversation + "]";
        handler.Routes[ChatSamples.QuietPath + "/messages"] = ChatSamples.OpenedMessages;

        var quiet = (await Read(handler)).Conversations[^1];

        Assert.Equal((ChatSamples.QuietId, "Quiet", "quiet-puuid"), (quiet.Id, quiet.Title, quiet.Puuid));
        Assert.Null(quiet.LastMessage);
        Assert.Empty(quiet.Messages);
    }

    [Fact]
    public void Only_the_most_recent_direct_conversations_are_listed_plus_kept_ones()
    {
        // 25 DMs, listed oldest first; dm-0 never had a message.
        var conversations = Json("[" + string.Join(",", Enumerable.Range(0, 25).Select(i => JsonSerializer.Serialize(new
        {
            id = $"dm-{i}@eu2.pvp.net",
            type = "chat",
            gameName = $"F{i}",
            lastMessage = i == 0 ? null : new { id = $"m{i}", type = "chat", body = "hi", timestamp = DateTimeOffset.Parse("2026-10-01T00:00:00Z").AddHours(i) },
        }))) + "]");

        var listed = ChatMapper.Select(conversations, null, null).Select(c => c.Id).ToList();
        var withKept = ChatMapper.Select(conversations, null, null, new HashSet<string> { "dm-0@eu2.pvp.net", "dm-3@eu2.pvp.net" })
            .Select(c => c.Id).ToList();

        Assert.Equal(Enumerable.Range(5, 20).Reverse().Select(i => $"dm-{i}@eu2.pvp.net"), listed);
        Assert.Equal([.. listed, "dm-3@eu2.pvp.net", "dm-0@eu2.pvp.net"], withKept);
    }

    // --- Commands -------------------------------------------------------------------------------

    [Fact]
    public async Task OpenDirectConversation_with_an_existing_conversation_only_keeps_it_listed()
    {
        var handler = ChatSamples.Handler();
        using var http = handler.CreateClient();
        var session = new FakeLcuSession(new LcuClient(http));

        var result = await session.Executor().ExecuteAsync(new RemoteCommand("r1", CommandType.OpenDirectConversation, Puuid: "friend-puuid"), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-chat/v1/conversations"], handler.Requests);
        Assert.Equal([ChatSamples.FriendId], session.Kept);
    }

    [Fact]
    public async Task OpenDirectConversation_creates_the_conversation_the_way_the_client_does()
    {
        var handler = ChatSamples.Handler();
        using var http = handler.CreateClient();
        var session = new FakeLcuSession(new LcuClient(http));

        var result = await session.Executor().ExecuteAsync(new RemoteCommand("r1", CommandType.OpenDirectConversation, Puuid: "new-friend-puuid"), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                "GET /lol-chat/v1/conversations",
                "GET /lol-chat/v1/friends",
                """POST /lol-chat/v1/conversations {"id":"new-friend-puuid@eu2.pvp.net","type":"chat"}""",
            ],
            handler.Requests);
        Assert.Equal(["new-friend-puuid@eu2.pvp.net"], session.Kept);
    }

    [Theory]
    [InlineData("stranger-puuid")]
    [InlineData("partner-puuid")] // in the party, not a friend
    public async Task OpenDirectConversation_refuses_players_who_are_not_friends(string puuid)
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.OpenDirectConversation, Puuid: puuid));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("friend-puuid@eu2.pvp.net")]
    [InlineData("../lol-lobby")]
    public async Task OpenDirectConversation_is_validated_before_anything_reaches_the_client(string? puuid)
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.OpenDirectConversation, Puuid: puuid));

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendChatMessage_posts_what_the_client_sends_to_the_escaped_conversation()
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendChatMessage, ConversationId: ChatSamples.DraftId, Body: " gl hf "));

        Assert.True(result.Success, result.Error);
        Assert.Equal($"POST {ChatSamples.DraftPath}/messages {{\"body\":\"gl hf\",\"type\":\"chat\"}}", handler.Requests[^1]);
    }

    [Theory]
    [InlineData("nobody@eu2.pvp.net")]
    [InlineData("club-1@clubs.eu2.pvp.net")]
    public async Task SendChatMessage_refuses_conversations_the_phone_cannot_see(string conversationId)
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendChatMessage, ConversationId: conversationId, Body: "hi"));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendChatMessage_is_validated_before_anything_reaches_the_client()
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendChatMessage, ConversationId: ChatSamples.DraftId, Body: new string('x', 201)));

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MarkConversationRead_opens_the_conversation_then_closes_the_pc_chat_window_again()
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.MarkConversationRead, ConversationId: ChatSamples.PartyId));

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                "GET /lol-chat/v1/conversations",
                "GET /lol-chat/v1/conversations/active",
                $"PUT /lol-chat/v1/conversations/active {{\"id\":\"{ChatSamples.PartyId}\"}}",
                "DELETE /lol-chat/v1/conversations/active",
            ],
            handler.Requests);
    }

    [Fact]
    public async Task MarkConversationRead_puts_back_the_conversation_open_on_the_pc()
    {
        var handler = ChatSamples.Handler();
        handler.Routes["/lol-chat/v1/conversations/active"] = $"{{\"id\":\"{ChatSamples.OldFriendId}\"}}";

        await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.MarkConversationRead, ConversationId: ChatSamples.FriendId));

        Assert.Equal($"PUT /lol-chat/v1/conversations/active {{\"id\":\"{ChatSamples.OldFriendId}\"}}", handler.Requests[^1]);
    }

    [Fact]
    public async Task MarkConversationRead_does_nothing_for_a_read_conversation()
    {
        var handler = ChatSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.MarkConversationRead, ConversationId: ChatSamples.OldFriendId));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-chat/v1/conversations"], handler.Requests);
    }

    [Fact]
    public void ChatStore_ignores_rebuilds_that_change_nothing_but_the_time()
    {
        var store = new ChatStore();
        var changes = 0;
        store.Changed += () => changes++;
        var chat = new ChatState([new ChatConversation("a", ChatKinds.Direct, "A", 0, false, null, [], "a-puuid")], DateTimeOffset.UnixEpoch);

        store.Set(chat);
        store.Set(chat with { UpdatedAt = DateTimeOffset.UtcNow });
        store.Set(ChatState.Empty());

        Assert.Equal(2, changes);
        Assert.Empty(store.Snapshot().Value.Conversations);
    }

}
