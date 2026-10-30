using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class FriendsTests
{
    private static async Task<FriendsState> Read(FakeLcu handler)
    {
        using var http = handler.CreateClient();
        return await FriendsReader.ReadAsync(new LcuClient(http), DateTimeOffset.UtcNow, CancellationToken.None);
    }

    private static Friend ByName(FriendsState state, string gameName) => state.Friends.Single(f => f.GameName == gameName);

    [Fact]
    public async Task Lists_every_friend_by_name_with_riot_id_and_ids()
    {
        var state = await Read(FriendsSamples.Handler());

        Assert.Equal(
            ["Friend1", "Friend3", "Friend4", "Friend5", "Friend6", "Friend7", "Friend9", "host", "OldName", "Zażółć"],
            state.Friends.Select(f => f.GameName));

        var rue = ByName(state, "Friend1");
        Assert.Equal(FriendsSamples.InGameId, rue.Id);
        Assert.Equal("aaaa0001-0000-0000-0000-000000000001", rue.Puuid);
        Assert.Equal(1001, rue.SummonerId);
        Assert.Equal("TAG1", rue.TagLine);
        Assert.Equal(1001, rue.ProfileIconId);
        Assert.Equal("note", rue.Note);
    }

    [Fact]
    public async Task Maps_availability_the_way_the_client_colours_it()
    {
        var state = await Read(FriendsSamples.Handler());

        Assert.Equal(FriendAvailability.Busy, ByName(state, "Friend1").Availability);
        Assert.Equal(FriendAvailability.Online, ByName(state, "Friend5").Availability);
        Assert.Equal(FriendAvailability.Away, ByName(state, "Friend3").Availability);
        Assert.Equal(FriendAvailability.Mobile, ByName(state, "Friend9").Availability);
        Assert.Equal(FriendAvailability.Offline, ByName(state, "Zażółć").Availability);
    }

    [Fact]
    public async Task In_game_friend_has_queue_champion_mode_and_game_start()
    {
        var rue = ByName(await Read(FriendsSamples.Handler()), "Friend1");

        Assert.Equal(FriendActivities.InGame, rue.Activity);
        Assert.Equal(420, rue.QueueId);
        Assert.Equal(16, rue.ChampionId);
        Assert.Equal("CLASSIC", rue.GameMode);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792693207373), rue.ActivitySince);
        Assert.Equal(("EMERALD", "I", "RANKED_SOLO_5x5"), (rue.RankTier, rue.RankDivision, rue.RankQueue));
    }

    [Theory]
    [InlineData("Friend6", FriendActivities.ChampSelect, 400)]
    [InlineData("Friend7", FriendActivities.Queue, 450)]
    [InlineData("host", FriendActivities.Lobby, 440)]
    [InlineData("Friend5",FriendActivities.Lobby, 2400)] // idle but in a party ("pty")
    public async Task Pre_game_activities_carry_their_queue_but_no_champion(string name, string activity, int queueId)
    {
        var friend = ByName(await Read(FriendsSamples.Handler()), name);

        Assert.Equal(activity, friend.Activity);
        Assert.Equal(queueId, friend.QueueId);
        Assert.Null(friend.ChampionId);
        Assert.Equal("", friend.GameMode);
        Assert.NotNull(friend.ActivitySince);
    }

    [Fact]
    public async Task Idle_friends_have_no_activity_even_when_league_leaves_old_values()
    {
        var panna = ByName(await Read(FriendsSamples.Handler()), "Friend3");

        Assert.Equal(FriendActivities.None, panna.Activity);
        Assert.Null(panna.QueueId);
        Assert.Null(panna.ActivitySince);
        Assert.Equal(":D", panna.StatusMessage);
    }

    [Fact]
    public async Task Friend_in_another_riot_product_shows_its_name_and_no_league_activity()
    {
        var state = await Read(FriendsSamples.Handler());

        Assert.Equal(("Riot Client", ""), (ByName(state, "Friend4").Product, ByName(state, "Friend4").Activity));
        Assert.Equal("", ByName(state, "Friend1").Product);
        Assert.Equal("", ByName(state, "Zażółć").Product);
    }

    [Fact]
    public async Task Rank_hides_apex_divisions_and_unranked()
    {
        var state = await Read(FriendsSamples.Handler());

        Assert.Equal(("MASTER", ""), (ByName(state, "Friend6").RankTier, ByName(state, "Friend6").RankDivision));
        Assert.Equal(("", "", ""), (ByName(state, "Friend7").RankTier, ByName(state, "Friend7").RankDivision, ByName(state, "Friend7").RankQueue));
    }

    [Fact]
    public async Task Offline_friend_falls_back_to_the_old_name_and_keeps_last_seen()
    {
        var old = ByName(await Read(FriendsSamples.Handler()), "OldName");

        Assert.Equal(7, old.GroupId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792600000000), old.LastSeenOnline);
        Assert.Null(ByName(await Read(FriendsSamples.Handler()), "Zażółć").LastSeenOnline);
    }

    [Fact]
    public async Task Groups_follow_the_client_order_without_meta_groups_and_the_default_is_unnamed()
    {
        var state = await Read(FriendsSamples.Handler());

        Assert.Equal([new FriendGroup(7, "Duo", false, 1), new FriendGroup(0, "", true, 3)], state.Groups);
    }

    [Fact]
    public void Friends_are_found_by_id_or_by_puuid_ignoring_case()
    {
        var friends = TestLcu.Json(FriendsSamples.Friends);

        Assert.Equal("Friend1", FriendsMapper.FindById(friends, FriendsSamples.InGameId)?.Str("gameName"));
        Assert.Equal("Friend1", FriendsMapper.FindByPuuid(friends, "AAAA0001-0000-0000-0000-000000000001")?.Str("gameName"));
        Assert.Null(FriendsMapper.FindById(friends, "stranger@eu2.pvp.net"));
        Assert.Null(FriendsMapper.FindByPuuid(friends, "stranger"));
    }

    [Theory]
    [InlineData("""{ "id": "p1@eu2.pvp.net", "pid": "p1@eu1.pvp.net" }""", "p1@eu2.pvp.net")]
    [InlineData("""{ "id": "", "pid": "p1@eu1.pvp.net" }""", "p1@eu1.pvp.net")]
    [InlineData("""{ "id": "p1" }""", null)]
    public void A_friends_chat_id_is_its_id_or_else_its_pid(string friend, string? chatId) =>
        Assert.Equal(chatId, FriendsMapper.ChatId(TestLcu.Json(friend)));

    [Fact]
    public void Without_a_friend_list_the_state_is_empty()
    {
        var state = FriendsMapper.Build(null, null, DateTimeOffset.UnixEpoch);

        Assert.Empty(state.Friends);
        Assert.Empty(state.Groups);
    }

    [Fact]
    public void A_huge_list_drops_offline_friends_first()
    {
        var entries = Enumerable.Range(0, FriendsMapper.MaxFriends + 10)
            .Select(i => $$"""{ "id": "off{{i}}@x", "gameName": "a{{i:D4}}", "availability": "offline" }""")
            .Append("""{ "id": "on@x", "gameName": "zzz", "availability": "chat", "product": "league_of_legends", "lol": {} }""");
        using var doc = System.Text.Json.JsonDocument.Parse($"[{string.Join(",", entries)}]");

        var state = FriendsMapper.Build(doc.RootElement, null, DateTimeOffset.UnixEpoch);

        Assert.Equal(FriendsMapper.MaxFriends, state.Friends.Count);
        Assert.Equal("zzz", state.Friends[^1].GameName);
    }

    [Fact]
    public void Friend_events_only_rebuild_friends()
    {
        Assert.Equal((false, false, true, false), LcuConnectionService.RefreshesFor(LcuTopics.Friends));
        Assert.Equal((false, false, true, false), LcuConnectionService.RefreshesFor(LcuTopics.FriendGroups));
        Assert.Contains(LcuTopics.Friends, LcuTopics.All);
        Assert.Contains(LcuTopics.FriendGroups, LcuTopics.All);
    }

    [Fact]
    public void FriendsStore_ignores_rebuilds_that_change_nothing_but_the_time()
    {
        var store = new FriendsStore();
        var changes = 0;
        store.Changed += () => changes++;
        var state = FriendsMapper.Build(null, null, DateTimeOffset.UnixEpoch) with { Groups = [new FriendGroup(0, "", true, 1)] };

        store.Set(state);
        store.Set(state with { UpdatedAt = DateTimeOffset.UtcNow });
        store.Set(FriendsState.Empty());

        Assert.Equal(2, changes);
        Assert.Empty(store.Snapshot().Value.Groups);
    }

    // --- Commands -------------------------------------------------------------------------------

    [Fact]
    public async Task RemoveFriend_deletes_the_escaped_friend_id()
    {
        var handler = FriendsSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.RemoveFriend, FriendId: FriendsSamples.InGameId));

        Assert.True(result.Success, result.Error);
        Assert.Equal($"DELETE {FriendsSamples.InGamePath}", handler.Requests[^1]);
    }

    [Fact]
    public async Task RemoveFriend_refuses_ids_that_are_not_on_the_friend_list()
    {
        var handler = FriendsSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.RemoveFriend, FriendId: "stranger@eu2.pvp.net"));

        Assert.False(result.Success);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemoveFriend_is_validated_before_anything_reaches_the_client()
    {
        var handler = FriendsSamples.Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.RemoveFriend));

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }
}
