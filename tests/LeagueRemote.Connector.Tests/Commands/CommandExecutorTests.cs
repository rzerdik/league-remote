using System.Net;
using System.Text;
using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Commands;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class CommandExecutorTests
{
    private const string SessionPath = "/lol-champ-select/v1/session";
    private const string PickInProgress = "\"id\": 21, \"actorCellId\": 2, \"championId\": 222, \"type\": \"pick\", \"completed\": false";
    private const string PickCompleted = "\"id\": 21, \"actorCellId\": 2, \"championId\": 222, \"type\": \"pick\", \"completed\": true";

    [Fact]
    public async Task LockChampion_locks_with_a_single_completed_patch_when_league_accepts_it()
    {
        var handler = LockingLcu(completesOn: "PATCH");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LockChampion, ChampionId: 222, ActionId: 21));

        Assert.True(result.Success, result.Error);
        Assert.Equal("r1", result.RequestId);
        Assert.Equal(
            [
                "GET /lol-champ-select/v1/session",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":222,\"completed\":true}",
                "GET /lol-champ-select/v1/session",
            ],
            handler.Requests);
    }

    [Fact]
    public async Task LockChampion_falls_back_to_the_complete_endpoint()
    {
        var handler = LockingLcu(completesOn: "POST");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LockChampion, ChampionId: 222, ActionId: 21));

        Assert.True(result.Success, result.Error);
        Assert.Contains("POST /lol-champ-select/v1/session/actions/21/complete", handler.Requests);
    }

    [Fact]
    public async Task LockChampion_reports_failure_when_league_never_locks()
    {
        var handler = LockingLcu(completesOn: null);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LockChampion, ChampionId: 222, ActionId: 21));

        Assert.False(result.Success);
        Assert.Contains("didn't accept", result.Error);
    }

    [Fact]
    public async Task HoverChampion_only_patches_the_champion()
    {
        var handler = LockingLcu(completesOn: null);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.HoverChampion, ChampionId: 222, ActionId: 21));

        Assert.True(result.Success, result.Error);
        Assert.Equal("PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":222}", handler.Requests[^1]);
    }

    [Fact]
    public async Task HoverChampion_refuses_an_action_that_is_no_longer_the_current_one()
    {
        var handler = LockingLcu(completesOn: null);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.HoverChampion, ChampionId: 222, ActionId: 20));

        Assert.False(result.Success);
        Assert.Equal("That turn is over.", result.Error);
        Assert.Equal(["GET /lol-champ-select/v1/session"], handler.Requests);
    }

    [Fact]
    public async Task LockChampion_applies_to_an_in_progress_ban_action()
    {
        var session = GameStateSamples.ChampSelectSession.Replace(
            "\"id\": 10, \"actorCellId\": 2, \"championId\": 55, \"type\": \"ban\", \"completed\": true, \"isAllyAction\": true, \"isInProgress\": false",
            "\"id\": 10, \"actorCellId\": 2, \"championId\": 55, \"type\": \"ban\", \"completed\": false, \"isAllyAction\": true, \"isInProgress\": true");
        var handler = LockingLcu(completesOn: "POST", session,
            inProgress: "\"id\": 10, \"actorCellId\": 2, \"championId\": 55, \"type\": \"ban\", \"completed\": false",
            completed: "\"id\": 10, \"actorCellId\": 2, \"championId\": 55, \"type\": \"ban\", \"completed\": true");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r2", CommandType.LockChampion, ChampionId: 55, ActionId: 10));

        Assert.True(result.Success, result.Error);
        Assert.Contains("POST /lol-champ-select/v1/session/actions/10/complete", handler.Requests);
    }

    [Fact]
    public async Task CreatePracticeTool_creates_a_lobby_for_the_practice_tool_queue_from_the_client()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-game-queues/v1/queues"] = """
        [
          { "id": 420, "gameMode": "CLASSIC", "queueAvailability": "Available" },
          { "id": 3141, "gameMode": "PRACTICETOOL", "queueAvailability": "PlatformDisabled" },
          { "id": 3142, "gameMode": "PRACTICETOOL", "queueAvailability": "Available" }
        ]
        """;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.CreatePracticeTool));

        Assert.True(result.Success, result.Error);
        Assert.Equal("GET /lol-game-queues/v1/queues", handler.Requests[0]);
        var body = PracticeToolBody(Assert.Single(handler.Requests.Skip(1)));
        Assert.Equal(3142, body.GetProperty("queueId").GetInt32());

        // The rest is what the client's own Practice Tool button sends (verified against the live 16.21 client).
        Assert.False(body.TryGetProperty("isCustom", out _));
        var custom = body.GetProperty("customGameLobby");
        Assert.Equal(LobbyCommands.PracticeToolLobbyName, custom.GetProperty("lobbyName").GetString());
        Assert.Equal(JsonValueKind.Null, custom.GetProperty("lobbyPassword").ValueKind);
        Assert.True(custom.GetProperty("hidePublicly").GetBoolean());
        var config = custom.GetProperty("configuration");
        Assert.Equal("PRACTICETOOL", config.GetProperty("gameMode").GetString());
        Assert.Equal(11, config.GetProperty("mapId").GetInt32());
        Assert.Equal(1, config.GetProperty("mutators").GetProperty("id").GetInt32());
        Assert.Equal(5, config.GetProperty("teamSize").GetInt32());
        Assert.Equal("AllAllowed", config.GetProperty("spectatorPolicy").GetString());
        Assert.False(config.GetProperty("spectatorDelayEnabled").GetBoolean());
        Assert.True(config.GetProperty("hidePublicly").GetBoolean());
    }

    [Fact]
    public async Task CreatePracticeTool_falls_back_to_queue_3140_without_a_queue_list()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.CreatePracticeTool));

        Assert.True(result.Success, result.Error);
        Assert.Equal(3140, PracticeToolBody(handler.Requests[^1]).GetProperty("queueId").GetInt32());
    }

    private static JsonElement PracticeToolBody(string request)
    {
        const string prefix = "POST /lol-lobby/v2/lobby ";
        Assert.StartsWith(prefix, request);
        return TestLcu.Json(request[prefix.Length..]);
    }

    [Fact]
    public async Task StartMatchmaking_searches_in_a_practice_tool_queue_lobby()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.PracticeToolLobby.Replace("\"queueId\": -1", "\"queueId\": 3140");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.StartMatchmaking));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-lobby/v2/lobby", "POST /lol-lobby/v2/lobby/matchmaking/search"], handler.Requests);
    }

    [Fact]
    public async Task StartMatchmaking_starts_champ_select_in_a_legacy_custom_lobby()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.PracticeToolLobby;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.StartMatchmaking));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-lobby/v2/lobby", "POST /lol-lobby/v1/lobby/custom/start-champ-select"], handler.Requests);
    }

    [Fact]
    public async Task StartMatchmaking_searches_in_a_queue_lobby()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.Lobby;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.StartMatchmaking));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-lobby/v2/lobby", "POST /lol-lobby/v2/lobby/matchmaking/search"], handler.Requests);
    }

    [Fact]
    public async Task SetPositionPreferences_puts_both_roles_in_a_draft_lobby()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.Lobby;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SetPositionPreferences, FirstPosition: "JUNGLE", SecondPosition: "UTILITY"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                "GET /lol-lobby/v2/lobby",
                "GET /lol-lobby/v2/lobby/matchmaking/search-state",
                "PUT /lol-lobby/v2/lobby/members/localMember/position-preferences {\"firstPreference\":\"JUNGLE\",\"secondPreference\":\"UTILITY\"}",
            ],
            handler.Requests);
    }

    [Fact]
    public async Task SetPositionPreferences_sends_unselected_as_the_second_role_with_fill()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.Lobby;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SetPositionPreferences, FirstPosition: "FILL"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            "PUT /lol-lobby/v2/lobby/members/localMember/position-preferences {\"firstPreference\":\"FILL\",\"secondPreference\":\"UNSELECTED\"}",
            handler.Requests[^1]);
    }

    [Fact]
    public async Task SetPositionPreferences_is_refused_while_searching()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.Lobby;
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = """{ "searchState": "Searching" }""";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SetPositionPreferences, FirstPosition: "TOP", SecondPosition: "MIDDLE"));

        Assert.False(result.Success);
        Assert.Contains("Cancel the search", result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("PUT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetPositionPreferences_is_refused_without_a_position_selector()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.PracticeToolLobby;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SetPositionPreferences, FirstPosition: "TOP", SecondPosition: "MIDDLE"));

        Assert.False(result.Success);
        Assert.Equal(["GET /lol-lobby/v2/lobby"], handler.Requests);
    }

    [Fact]
    public async Task SetPositionPreferences_needs_a_lobby()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.SetPositionPreferences, FirstPosition: "TOP", SecondPosition: "MIDDLE"));

        Assert.False(result.Success);
        Assert.Equal("Not in a lobby.", result.Error);
    }

    [Theory]
    [InlineData(nameof(GameStateSamples.Lobby))]
    [InlineData(nameof(GameStateSamples.PracticeToolLobby))]
    public async Task LeaveLobby_deletes_the_lobby(string sample)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = sample == nameof(GameStateSamples.Lobby) ? GameStateSamples.Lobby : GameStateSamples.PracticeToolLobby;
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = """{ "searchState": "Invalid" }""";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LeaveLobby));

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                "GET /lol-lobby/v2/lobby",
                "GET /lol-lobby/v2/lobby/matchmaking/search-state",
                "DELETE /lol-lobby/v2/lobby",
            ],
            handler.Requests);
    }

    [Theory]
    [InlineData("Searching")]
    [InlineData("Found")]
    public async Task LeaveLobby_is_refused_while_searching(string searchState)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-lobby/v2/lobby"] = GameStateSamples.Lobby;
        handler.Routes["/lol-lobby/v2/lobby/matchmaking/search-state"] = $$"""{ "searchState": "{{searchState}}" }""";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LeaveLobby));

        Assert.False(result.Success);
        Assert.Equal("Cancel the search first.", result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LeaveLobby_needs_a_lobby()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LeaveLobby));

        Assert.False(result.Success);
        Assert.Equal("Not in a lobby.", result.Error);
        Assert.Equal(["GET /lol-lobby/v2/lobby"], handler.Requests);
    }

    [Fact]
    public async Task LockChampion_succeeds_when_league_applies_the_patch_lock_after_complete_errors()
    {
        // GETs 1-5: initial read + 4 confirm polls see the pick open; POST /complete fails; GET 8 sees it locked.
        var handler = LateLockLcu(completedFromGet: 8);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LockChampion, ChampionId: 222, ActionId: 21));

        Assert.True(result.Success, result.Error);
        Assert.Contains("POST /lol-champ-select/v1/session/actions/21/complete", handler.Requests);
    }

    [Fact]
    public async Task LockChampion_reports_the_complete_error_when_the_action_stays_open()
    {
        var handler = LateLockLcu(completedFromGet: null);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LockChampion, ChampionId: 222, ActionId: 21));

        Assert.False(result.Success);
        Assert.Equal("League refused that. That turn is over.", result.Error); // League: 500 "action already completed"
        // 1 initial read + 4 confirm polls + the late-lock re-checks after /complete failed.
        Assert.Equal(5 + ChampSelectLock.LateLockAttempts, handler.Requests.Count(r => r.StartsWith("GET ")));
    }

    [Theory]
    [InlineData(CommandType.RequestSwap, SwapKind.PickOrder, 31, "POST /lol-champ-select/v1/session/pick-order-swaps/31/request")]
    [InlineData(CommandType.AcceptSwap, SwapKind.Position, 32, "POST /lol-champ-select/v1/session/position-swaps/32/accept")]
    [InlineData(CommandType.DeclineSwap, SwapKind.Position, 32, "POST /lol-champ-select/v1/session/position-swaps/32/decline")]
    [InlineData(CommandType.CancelSwap, SwapKind.Trade, 34, "POST /lol-champ-select/v1/session/trades/34/cancel")]
    public async Task Swap_commands_post_to_the_matching_lcu_endpoint(CommandType type, SwapKind kind, long id, string expected)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-champ-select/v1/session"] = GameStateSamples.BanPhaseSession.Replace(
            "\"trades\": [",
            "\"trades\": [ { \"id\": 34, \"cellId\": 1, \"state\": \"SENT\" },");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", type, SwapKind: kind, SwapId: id));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET /lol-champ-select/v1/session", expected], handler.Requests);
    }

    [Theory]
    [InlineData(CommandType.AcceptSwap, SwapKind.PickOrder, 31)] // AVAILABLE, not RECEIVED
    [InlineData(CommandType.RequestSwap, SwapKind.Trade, 33)] // INVALID
    [InlineData(CommandType.RequestSwap, SwapKind.Position, 31)] // id belongs to another kind
    [InlineData(CommandType.CancelSwap, SwapKind.Trade, 999)] // unknown
    public async Task Swap_commands_refuse_offers_league_does_not_list_in_the_right_state(CommandType type, SwapKind kind, long id)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-champ-select/v1/session"] = GameStateSamples.BanPhaseSession;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", type, SwapKind: kind, SwapId: id));

        Assert.False(result.Success);
        Assert.Contains("no longer available", result.Error);
        Assert.Equal(["GET /lol-champ-select/v1/session"], handler.Requests);
    }

    [Fact]
    public async Task Swap_commands_outside_champ_select_fail()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.RequestSwap, SwapKind: SwapKind.Trade, SwapId: 1));

        Assert.False(result.Success);
        Assert.Equal("Not in champ select.", result.Error);
    }

    [Fact]
    public async Task HoverChampion_targets_the_in_progress_ban_action()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-champ-select/v1/session"] = GameStateSamples.BanPhaseSession;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.HoverChampion, ChampionId: 103, ActionId: 10));

        Assert.True(result.Success, result.Error);
        Assert.Equal("PATCH /lol-champ-select/v1/session/actions/10 {\"championId\":103}", handler.Requests[^1]);
    }

    [Fact]
    public async Task Unexpected_exceptions_become_a_failed_result()
    {
        var result = await FakeLcuSession.ExecuteAsync(ThrowingLcu(), new RemoteCommand("r9", CommandType.AcceptReadyCheck));

        Assert.False(result.Success);
        Assert.Equal("r9", result.RequestId);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    // --- League's refusals ---------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Invalid request", "League refused that.")]
    [InlineData(HttpStatusCode.Forbidden, "", "League refused that.")]
    [InlineData(HttpStatusCode.Conflict, "Conflict", "League refused that.")]
    [InlineData((HttpStatusCode)422, "Unprocessable", "League refused that.")]
    [InlineData(HttpStatusCode.NotFound, "No ready check", "That's no longer there.")]
    [InlineData(HttpStatusCode.TooManyRequests, "", "League is busy. Wait a moment and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "Unexpected failure", "League couldn't do that. Try again.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", "League couldn't do that. Try again.")]
    [InlineData(HttpStatusCode.BadRequest, "Champion is on cooldown", "League refused that. Wait a moment and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "GATEKEEPER_RESTRICTED", "League refused that. You can't queue right now.")]
    [InlineData(HttpStatusCode.Forbidden, "Player is not the leader of the lobby", "League refused that. Only the party leader can do that.")]
    [InlineData(HttpStatusCode.BadRequest, "Skin not owned", "League refused that. You don't own that.")]
    [InlineData(HttpStatusCode.Conflict, "The party is full", "League refused that. The party is full.")]
    [InlineData(HttpStatusCode.BadRequest, "Action is not in progress", "League refused that. That turn is over.")]
    [InlineData(HttpStatusCode.BadRequest, "Queue is disabled", "League refused that. That's not available right now.")]
    public async Task League_refusals_reach_the_phone_as_a_short_sentence(HttpStatusCode status, string lcuMessage, string expected)
    {
        var handler = new FakeLcu();
        handler.Failures["POST /lol-matchmaking/v1/ready-check/accept"] = (status, lcuMessage);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptReadyCheck));

        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void A_recognised_error_code_gives_the_reason_too()
    {
        var ex = new LcuRequestException(HttpStatusCode.BadRequest, "Something went wrong", "LEAVERBUSTER_PENALTY");

        Assert.Equal("League refused that. You can't queue right now.", LcuRefusals.Message(ex));
    }

    [Fact]
    public async Task League_refusal_text_never_reaches_the_phone()
    {
        const string body = """{"errorCode":"RPC_ERROR","httpStatus":400,"message":"Summoner Friend#EUW token=abc123 at /lol-lobby/v2/lobby"}""";
        var handler = RawFailureLcu(HttpStatusCode.BadRequest, body);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.AcceptReadyCheck));

        Assert.Equal("League refused that.", result.Error);
        Assert.DoesNotContain("League client returned", result.Error);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2]")]
    [InlineData("\"just a string\"")]
    [InlineData("")]
    public async Task A_refusal_without_league_error_json_still_gets_its_sentence(string body)
    {
        var result = await FakeLcuSession.ExecuteAsync(
            RawFailureLcu(HttpStatusCode.Conflict, body), new RemoteCommand("r1", CommandType.AcceptReadyCheck));

        Assert.Equal("League refused that.", result.Error);
    }

    [Fact]
    public async Task Shutdown_cancellation_still_propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var http = TestLcu.LcuHttp(ThrowingLcu());
        var executor = new FakeLcuSession(new LcuClient(http)).Executor();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(new RemoteCommand("r9", CommandType.AcceptReadyCheck), cts.Token));
    }

    /// <summary>
    /// Serves the champ-select session; the action shows as completed from the first request with method
    /// <paramref name="completesOn"/> on (a PATCH only when it locks; never when null).
    /// </summary>
    private static FakeLcu LockingLcu(
        string? completesOn, string session = GameStateSamples.ChampSelectSession, string inProgress = PickInProgress, string completed = PickCompleted)
    {
        var lcu = new FakeLcu();
        lcu.Routes[SessionPath] = session;
        lcu.Intercept = line =>
        {
            if (completesOn is not null && line.StartsWith(completesOn + " ", StringComparison.Ordinal)
                && (completesOn != "PATCH" || line.Contains("\"completed\":true", StringComparison.Ordinal)))
                lcu.Routes[SessionPath] = session.Replace(inProgress, completed);
            return null;
        };
        return lcu;
    }

    /// <summary>
    /// The PATCH lock is applied late: session GETs show the pick completed from GET number
    /// <paramref name="completedFromGet"/> on (never when null); /complete answers 500.
    /// </summary>
    private static FakeLcu LateLockLcu(int? completedFromGet)
    {
        var lcu = new FakeLcu();
        var gets = 0;
        lcu.Routes[SessionPath] = GameStateSamples.ChampSelectSession;
        lcu.Failures["POST /lol-champ-select/v1/session/actions/21/complete"] = (HttpStatusCode.InternalServerError, "action already completed");
        lcu.Intercept = line =>
        {
            if (line == $"GET {SessionPath}" && ++gets >= completedFromGet)
                lcu.Routes[SessionPath] = GameStateSamples.ChampSelectSession.Replace(PickInProgress, PickCompleted);
            return null;
        };
        return lcu;
    }

    private static FakeLcu ThrowingLcu() => new() { Intercept = _ => throw new InvalidOperationException("boom") };

    /// <summary>Every request fails with <paramref name="status"/> and exactly <paramref name="body"/> (any shape, or none).</summary>
    private static FakeLcu RawFailureLcu(HttpStatusCode status, string body) =>
        new() { Intercept = _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") } };
}
