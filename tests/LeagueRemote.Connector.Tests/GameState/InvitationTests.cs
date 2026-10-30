using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

public sealed class InvitationTests
{
    private const string NotSearching = """{ "searchState": "Invalid" }""";

    // --- Mapping ------------------------------------------------------------------------------

    [Fact]
    public void Received_invitations_keep_pending_ones_with_resolved_names_and_queue_names()
    {
        var names = new Dictionary<string, string> { ["friend-puuid"] = "Friend#EUNE" };

        var invitations = InvitationMapper.MapReceived(Json(GameStateSamples.ReceivedInvitations), Json(GameStateSamples.Queues), names);

        Assert.Equal(
            [
                new ReceivedInvitation("inv-flex", "Friend#EUNE", 440, "Ranked Flex", "CLASSIC", CanAccept: true),
                // No resolved name: the legacy summoner name; League won't let the player join (a restriction).
                new ReceivedInvitation("inv-arena", "Old Name", 1700, "Arena", "CHERRY", CanAccept: false),
            ],
            invitations);
    }

    [Fact]
    public void Received_invitations_are_null_when_none_wait_and_name_falls_back_to_Someone()
    {
        Assert.Null(InvitationMapper.MapReceived(Json("[]"), null, null));
        Assert.Null(InvitationMapper.MapReceived(null, null, null));

        var unnamed = InvitationMapper.MapReceived(Json(GameStateSamples.ReceivedInvitations), null, null)!;
        Assert.Equal("Someone", unnamed[0].FromRiotId);
        Assert.Null(unnamed[0].QueueName);
    }

    [Fact]
    public void Sent_invitations_drop_the_local_player_and_members_and_keep_pending_and_declined()
    {
        var names = new Dictionary<string, string> { ["friend-puuid"] = "Friend#EUNE" };

        var lobby = LobbyMapper.Map(Json(GameStateSamples.LobbyWithInvitations), null, null, names)!;

        Assert.True(lobby.CanInvite);
        Assert.Equal(
            [new SentInvitation("friend-puuid", "Friend#EUNE", "Pending"), new SentInvitation("other-puuid", "Unknown", "Declined")],
            lobby.Invitations);
    }

    [Fact]
    public void A_solo_lobby_with_only_the_clients_own_entry_has_no_invitations()
    {
        // Live shape: the local player's own Accepted entry is all /lol-lobby/v2/lobby/invitations has in a solo lobby.
        var json = GameStateSamples.LobbyWithInvitations
            .Replace("\"state\": \"Pending\"", "\"state\": \"Joined\"")
            .Replace("\"state\": \"Declined\"", "\"state\": \"Kicked\"");

        Assert.Null(LobbyMapper.Map(Json(json), null, null)!.Invitations);
    }

    [Theory]
    [InlineData("\"allowedInviteOthers\": true", "\"allowedInviteOthers\": false")] // a member when invites aren't open
    [InlineData("\"isLobbyFull\": false", "\"isLobbyFull\": true")]
    [InlineData("\"maxLobbySize\": 5", "\"maxLobbySize\": 2")] // two members already
    public void CanInvite_needs_permission_and_room(string from, string to)
    {
        var lobby = LobbyMapper.Map(Json(GameStateSamples.LobbyWithInvitations.Replace(from, to)), null, null)!;

        Assert.False(lobby.CanInvite);
    }

    [Fact]
    public async Task Inviters_and_invitees_are_resolved_through_the_summoner_api()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-summoner/v1/current-summoner"] = GameStateSamples.CurrentSummoner;
        handler.Routes["/lol-summoner/v2/summoners/puuid/friend-puuid"] = """{ "puuid": "friend-puuid", "gameName": "Friend", "tagLine": "EUNE" }""";
        handler.Routes["/lol-summoner/v2/summoners/puuid/other-puuid"] = """{ "puuid": "other-puuid", "gameName": "Other", "tagLine": "NA1" }""";
        using var http = handler.CreateClient();
        var resolver = TestLcu.Names(new LcuClient(http));

        var inviters = await resolver.ResolveInvitersAsync(Json(GameStateSamples.ReceivedInvitations), CancellationToken.None);
        var lobbyNames = await resolver.ResolveLobbyAsync(Json(GameStateSamples.LobbyWithInvitations), CancellationToken.None);

        Assert.Equal("Friend#EUNE", inviters["friend-puuid"]);
        Assert.Equal("Other#NA1", inviters["id:1003"]);
        Assert.Equal("Friend#EUNE", lobbyNames["friend-puuid"]);
        // Declined and self-invites are not looked up; the second pass is served from the cache.
        Assert.DoesNotContain(handler.Requests, r => r.Contains("me-puuid", StringComparison.Ordinal));
        Assert.Single(handler.Requests, r => r == "GET /lol-summoner/v2/summoners/puuid/friend-puuid");
    }

    [Fact]
    public async Task State_carries_received_invitations_outside_champ_select_only()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-gameflow/v1/gameflow-phase"] = "\"None\"";
        handler.Routes["/lol-lobby/v2/received-invitations"] = GameStateSamples.ReceivedInvitations;
        handler.Routes["/lol-game-queues/v1/queues"] = GameStateSamples.Queues;
        using var http = handler.CreateClient();
        var client = new LcuClient(http);

        var state = await TestLcu.BuildStateAsync(client);

        Assert.Equal(["inv-flex", "inv-arena"], state.Invitations!.Select(i => i.Id));
        Assert.Equal("Ranked Flex", state.Invitations![0].QueueName);

        handler.Routes["/lol-gameflow/v1/gameflow-phase"] = "\"ChampSelect\"";
        handler.Requests.Clear();
        var champSelect = await TestLcu.BuildStateAsync(client);

        Assert.Null(champSelect.Invitations);
        Assert.DoesNotContain("GET /lol-lobby/v2/received-invitations", handler.Requests);
    }

    // --- Commands -----------------------------------------------------------------------------

    [Fact]
    public async Task AcceptInvitation_accepts_a_pending_invitation()
    {
        var handler = InvitationsHandler(phase: "None");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptInvitation, InvitationId: "inv-flex"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("POST /lol-lobby/v2/received-invitations/inv-flex/accept", handler.Requests[^1]);
    }

    [Theory]
    [InlineData("inv-old", "None", "That invite is gone.")] // declined already
    [InlineData("inv-self", "None", "That invite is gone.")] // the client handles self-invites
    [InlineData("nope", "None", "That invite is gone.")]
    [InlineData("inv-arena", "None", "League won't let you join that game.")]
    [InlineData("inv-flex", "ChampSelect", "You can't join a party right now.")]
    [InlineData("inv-flex", "InProgress", "You can't join a party right now.")]
    public async Task AcceptInvitation_is_refused_when_it_cannot_work(string id, string phase, string error)
    {
        var handler = InvitationsHandler(phase);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptInvitation, InvitationId: id));

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptInvitation_is_refused_while_searching()
    {
        var handler = InvitationsHandler(phase: "Matchmaking");
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = """{ "searchState": "Searching" }""";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptInvitation, InvitationId: "inv-flex"));

        Assert.Equal("Cancel the search first.", result.Error);
    }

    [Fact]
    public async Task DeclineInvitation_declines_only_a_listed_invitation()
    {
        var handler = InvitationsHandler(phase: "None");

        var declined = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.DeclineInvitation, InvitationId: "inv-arena"));
        var gone = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r2", CommandType.DeclineInvitation, InvitationId: "inv-old"));

        Assert.True(declined.Success, declined.Error);
        Assert.Equal("That invite is gone.", gone.Error);
        Assert.Single(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
        Assert.Contains("POST /lol-lobby/v2/received-invitations/inv-arena/decline", handler.Requests);
    }

    [Fact]
    public async Task SendInvitation_posts_the_friends_summoner_id_as_the_client_does()
    {
        var handler = LobbyHandler(GameStateSamples.LobbyWithInvitations);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendInvitation, Puuid: "friend-puuid"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("POST /lol-lobby/v2/lobby/invitations [{\"toSummonerId\":1002}]", handler.Requests[^1]);
    }

    [Fact]
    public async Task SendInvitation_looks_up_a_missing_summoner_id_by_puuid()
    {
        var handler = LobbyHandler(GameStateSamples.LobbyWithInvitations);
        handler.Routes["/lol-summoner/v2/summoners/puuid/no-id-puuid"] = """{ "puuid": "no-id-puuid", "summonerId": 2002 }""";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendInvitation, Puuid: "no-id-puuid"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("POST /lol-lobby/v2/lobby/invitations [{\"toSummonerId\":2002}]", handler.Requests[^1]);
    }

    [Theory]
    [InlineData("stranger-puuid", null, null, "Only friends can be invited.")]
    [InlineData("joined-puuid", null, null, "Already in your party.")]
    [InlineData("friend-puuid", "\"isLobbyFull\": false", "\"isLobbyFull\": true", "Your party is full.")]
    [InlineData("friend-puuid", "\"allowedInviteOthers\": true", "\"allowedInviteOthers\": false", "Only the party leader can invite.")]
    public async Task SendInvitation_is_refused_when_it_cannot_work(string puuid, string? from, string? to, string error)
    {
        var lobby = from is null ? GameStateSamples.LobbyWithInvitations : GameStateSamples.LobbyWithInvitations.Replace(from, to);
        var handler = LobbyHandler(lobby);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SendInvitation, Puuid: puuid));

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendInvitation_needs_a_lobby_and_no_search()
    {
        var noLobby = await FakeLcuSession.ExecuteAsync(new FakeLcu(), new RemoteCommand("r1", CommandType.SendInvitation, Puuid: "friend-puuid"));
        var searching = LobbyHandler(GameStateSamples.LobbyWithInvitations);
        searching.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = """{ "searchState": "Searching" }""";
        var whileSearching = await FakeLcuSession.ExecuteAsync(searching, new RemoteCommand("r2", CommandType.SendInvitation, Puuid: "friend-puuid"));

        Assert.Equal("Not in a lobby.", noLobby.Error);
        Assert.Equal("Cancel the search first.", whileSearching.Error);
    }

    private static FakeLcu InvitationsHandler(string phase)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-gameflow/v1/gameflow-phase"] = $"\"{phase}\"";
        handler.Routes["/lol-lobby/v2/received-invitations"] = GameStateSamples.ReceivedInvitations;
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = NotSearching;
        return handler;
    }

    private static FakeLcu LobbyHandler(string lobby)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = lobby;
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = NotSearching;
        handler.Routes["/lol-chat/v1/friends"] = GameStateSamples.Friends;
        return handler;
    }
}
