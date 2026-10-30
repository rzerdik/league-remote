using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="PlayerNameResolver"/>: Riot IDs for lobby members League sends without a name.</summary>
public sealed class PlayerNameResolverTests
{
    [Fact]
    public async Task Resolver_names_the_local_player_from_current_summoner_and_others_by_puuid_or_id_with_caching()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-summoner/v1/current-summoner"] = GameStateSamples.CurrentSummoner;
        handler.Routes["/lol-summoner/v1/summoners/1003"] = """{ "summonerId": 1003, "gameName": "Third", "tagLine": "NA1" }""";
        using var http = handler.CreateClient();
        var resolver = Names(new LcuClient(http));
        var lobbyJson = Json(GameStateSamples.Lobby);

        var names = await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        var lobby = GameStateMapper.Build(new LcuSnapshot("Lobby", Lobby: lobbyJson, PlayerNames: names), DateTimeOffset.UnixEpoch).Lobby!;

        Assert.Equal(["Me#EUW", "Friend#EUNE", "Third#NA1"], lobby.Members.Select(m => m.RiotId));
        // "Friend" already has a Riot ID: never looked up. The third member has no puuid: looked up by id.
        Assert.Equal(
            ["GET /lol-summoner/v1/current-summoner", "GET /lol-summoner/v1/summoners/1003"],
            handler.Requests);

        handler.Requests.Clear();
        await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Resolver_looks_up_other_players_by_puuid_and_survives_lcu_errors()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-summoner/v1/current-summoner"] = GameStateSamples.CurrentSummoner;
        handler.Routes["/lol-summoner/v2/summoners/puuid/friend-puuid"] = """{ "puuid": "friend-puuid", "gameName": "Pal", "tagLine": "EUW" }""";
        using var http = handler.CreateClient();
        var resolver = Names(new LcuClient(http));
        var lobbyJson = Json(GameStateSamples.Lobby.Replace("\"gameName\": \"Friend\", \"gameTag\": \"EUNE\"", "\"gameName\": \"\", \"gameTag\": \"\""));

        var names = await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        var lobby = LobbyMapper.Map(lobbyJson, null, null, names)!;

        // Member 3 (id 1003) is unknown to the fake LCU (404): it stays "Unknown" instead of failing the refresh.
        Assert.Equal(["Me#EUW", "Pal#EUW", "Unknown"], lobby.Members.Select(m => m.RiotId));
        Assert.Contains("GET /lol-summoner/v2/summoners/puuid/friend-puuid", handler.Requests);
    }

    [Fact]
    public async Task Resolver_retries_unknown_players_after_the_negative_cache_expires()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-summoner/v1/current-summoner"] = GameStateSamples.CurrentSummoner;
        using var http = handler.CreateClient();
        var clock = new ManualTimeProvider();
        var resolver = Names(new LcuClient(http), clock);
        var lobbyJson = Json(GameStateSamples.Lobby);

        // Member 1003 is unknown (404) at first.
        var names = await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        Assert.DoesNotContain("Third#NA1", names.Values);

        // Within the TTL the miss is cached: no new lookup.
        handler.Routes["/lol-summoner/v1/summoners/1003"] = """{ "summonerId": 1003, "gameName": "Third", "tagLine": "NA1" }""";
        handler.Requests.Clear();
        clock.Now += PlayerNameResolver.UnknownRetryAfter - TimeSpan.FromSeconds(1);
        await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        Assert.Empty(handler.Requests);

        // After it, the player is looked up again and the name is found.
        clock.Now += TimeSpan.FromSeconds(2);
        names = await resolver.ResolveLobbyAsync(lobbyJson, CancellationToken.None);
        Assert.Contains("Third#NA1", names.Values);
        Assert.Contains("GET /lol-summoner/v1/summoners/1003", handler.Requests);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
