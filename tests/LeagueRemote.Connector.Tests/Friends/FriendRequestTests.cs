using System.Net;
using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

public sealed class FriendRequestTests
{
    private const string RequestsPath = "/lol-chat/v2/friend-requests";
    private const string IncomingPuuid = "bbbb0003-0000-0000-0000-000000000003";
    private const string OutgoingPuuid = "bbbb0001-0000-0000-0000-000000000001";
    private const string SecondOutgoingPuuid = "bbbb0002-0000-0000-0000-000000000002";

    // --- Mapping --------------------------------------------------------------------------------

    [Fact]
    public void Maps_incoming_first_then_outgoing_by_name_and_drops_accepted()
    {
        var requests = FriendRequestMapper.Map(Json(FriendsSamples.FriendRequests));

        Assert.Equal(
            [
                new FriendRequest(IncomingPuuid, FriendRequestDirections.Incoming, "Newcomer", "TAG3", 1003, ""),
                new FriendRequest(SecondOutgoingPuuid, FriendRequestDirections.Outgoing, "Alpha Test", "TAG2", 0, ""),
                new FriendRequest(OutgoingPuuid, FriendRequestDirections.Outgoing, "Zażółć Test", "TAG1", 0, ""),
            ],
            requests);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""[{"direction":"in","puuid":""},{"direction":"sideways","puuid":"abc"},{"direction":"in","puuid":"a/b"},42]""")]
    public void Ignores_malformed_lists_and_entries(string json) => Assert.Empty(FriendRequestMapper.Map(Json(json)));

    [Fact]
    public void Falls_back_to_name_and_keeps_one_entry_per_puuid()
    {
        var requests = FriendRequestMapper.Map(Json("""
            [{"direction":"in","name":"Old Name","puuid":"p1"},{"direction":"in","gameName":"Dup","puuid":"p1"}]
            """));

        Assert.Equal([new FriendRequest("p1", FriendRequestDirections.Incoming, "Old Name", "", 0, "")], requests);
    }

    [Fact]
    public void FriendsState_carries_requests_and_defaults_to_none()
    {
        Assert.Empty(FriendsState.Empty().Requests);

        var state = FriendsState.Empty() with { Requests = FriendRequestMapper.Map(Json(FriendsSamples.FriendRequests)) };
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"requests\":[{\"puuid\":\"bbbb0003-0000-0000-0000-000000000003\",\"direction\":\"incoming\"", json);
    }

    [Fact]
    public async Task The_friends_slice_carries_the_requests_and_their_event_rebuilds_it()
    {
        var handler = Handler();
        using var http = handler.CreateClient();

        var state = await FriendsReader.ReadAsync(new LcuClient(http), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal([IncomingPuuid, SecondOutgoingPuuid, OutgoingPuuid], state.Requests.Select(r => r.Puuid));
        Assert.Contains(LcuTopics.FriendRequests, LcuTopics.All);
        Assert.Equal((false, false, true, false), LcuConnectionService.RefreshesFor(LcuTopics.FriendRequests));
    }

    // --- Commands -------------------------------------------------------------------------------

    [Theory]
    [InlineData(CommandType.AcceptFriendRequest, IncomingPuuid)]
    [InlineData(CommandType.CancelFriendRequest, OutgoingPuuid)]
    public async Task Answered_requests_refresh_the_friends_slice(CommandType type, string puuid)
    {
        using var http = Handler().CreateClient();
        var session = new FakeLcuSession(new LcuClient(http));

        var result = await session.Executor().ExecuteAsync(new RemoteCommand("r1", type, Puuid: puuid), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, session.FriendsRefreshes);
    }

    [Fact]
    public async Task SendFriendRequest_posts_the_riot_id_as_the_client_does()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendFriendRequest, GameName: "Zażółć Test", TagLine: "TAG1"));

        Assert.True(result.Success, result.Error);
        Assert.Equal([$"POST {RequestsPath} {{\"gameName\":\"Za\\u017C\\u00F3\\u0142\\u0107 Test\",\"tagLine\":\"TAG1\"}}"], handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "", "No player with that Riot ID.")]
    [InlineData(HttpStatusCode.Forbidden, "", "You can't add yourself.")]
    [InlineData(HttpStatusCode.Conflict, "already-on-roster", "You're already friends.")]
    [InlineData(HttpStatusCode.Conflict, "max-inviter-buddy-invites", "You have too many pending friend requests.")]
    [InlineData(HttpStatusCode.InternalServerError, "max_roster_size_sender", "Your friend list is full.")]
    [InlineData(HttpStatusCode.BadGateway, "", "League couldn't send the friend request. Try again.")]
    public async Task SendFriendRequest_explains_what_league_refused(HttpStatusCode status, string reason, string expected)
    {
        var handler = new FakeLcu();
        handler.Failures[$"POST {RequestsPath}"] = (status, reason);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendFriendRequest, GameName: "Nobody", TagLine: "EUW"));

        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData("ab", "EUW")]
    [InlineData("SeventeenCharName", "EUW")]
    [InlineData("Name", "EU")]
    [InlineData("Name", "EUW#1")]
    [InlineData(" Name", "EUW")]
    public async Task SendFriendRequest_is_validated_before_anything_reaches_the_client(string gameName, string tagLine)
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendFriendRequest, GameName: gameName, TagLine: tagLine));

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AcceptFriendRequest_puts_direction_both_on_the_request()
    {
        var handler = Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptFriendRequest, Puuid: IncomingPuuid));

        Assert.True(result.Success, result.Error);
        Assert.Equal([$"GET {RequestsPath}", $"PUT {RequestsPath}/{IncomingPuuid} {{\"direction\":\"both\"}}"], handler.Requests);
    }

    [Fact]
    public async Task DeclineFriendRequest_deletes_the_incoming_request()
    {
        var handler = Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.DeclineFriendRequest, Puuid: IncomingPuuid));

        Assert.True(result.Success, result.Error);
        Assert.Equal($"DELETE {RequestsPath}/{IncomingPuuid}", handler.Requests[^1]);
    }

    [Fact]
    public async Task CancelFriendRequest_deletes_the_outgoing_request()
    {
        var handler = Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.CancelFriendRequest, Puuid: OutgoingPuuid));

        Assert.True(result.Success, result.Error);
        Assert.Equal($"DELETE {RequestsPath}/{OutgoingPuuid}", handler.Requests[^1]);
    }

    [Theory]
    [InlineData(CommandType.AcceptFriendRequest, OutgoingPuuid)]
    [InlineData(CommandType.DeclineFriendRequest, OutgoingPuuid)]
    [InlineData(CommandType.CancelFriendRequest, IncomingPuuid)]
    [InlineData(CommandType.AcceptFriendRequest, "bbbb0004-0000-0000-0000-000000000004")]
    [InlineData(CommandType.CancelFriendRequest, "00000000-0000-0000-0000-000000000000")]
    public async Task Request_commands_only_touch_a_pending_request_in_the_right_direction(CommandType type, string puuid)
    {
        var handler = Handler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", type, Puuid: puuid));

        Assert.False(result.Success);
        Assert.Equal("That friend request is gone.", result.Error);
        Assert.Equal([$"GET {RequestsPath}"], handler.Requests);
    }

    private static FakeLcu Handler()
    {
        var handler = new FakeLcu();
        handler.Routes[RequestsPath] = FriendsSamples.FriendRequests;
        return handler;
    }

}
