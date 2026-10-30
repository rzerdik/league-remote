using System.Net;
using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary>ARAM-style champ select: the bench, rerolls and the <see cref="CommandType.BenchSwap"/> / <see cref="CommandType.Reroll"/> commands.</summary>
public sealed class BenchTests
{
    private const string SessionPath = "/lol-champ-select/v1/session";
    private const string PickablePath = "/lol-champ-select/v1/pickable-champion-ids";
    private const string BenchSwapPath = "POST /lol-champ-select/v1/session/bench/swap/";
    private const string RerollRequest = "POST /lol-champ-select/v1/session/my-selection/reroll";
    private const string Finalization = "\"phase\": \"FINALIZATION\"";

    private static ChampSelectState Map(string sessionJson) =>
        ChampSelectMapper.Map(TestLcu.Json(sessionJson), [22, 51, 99, 103], [])!;

    // --- Mapping -----------------------------------------------------------------------------------------------

    [Fact]
    public void Aram_session_maps_the_bench_the_rerolls_and_my_dealt_champion_without_any_action()
    {
        var cs = Map(BenchSamples.AramSession);

        Assert.True(cs.BenchEnabled);
        Assert.Equal([22, 51, 99], cs.BenchChampionIds); // client order; the -1 placeholder dropped
        Assert.True(cs.CanBenchSwap);
        Assert.True(cs.AllowRerolling);
        Assert.Equal(1, cs.RerollsRemaining);
        Assert.True(cs.CanReroll);

        // No actions: nothing to pick or lock, the dealt champion is myTeam's championId.
        Assert.Null(cs.MyAction);
        Assert.Empty(cs.Turns!);
        var me = cs.MyTeam.Single(m => m.IsLocalPlayer);
        Assert.Equal(103, me.ChampionId);
        Assert.Equal("FINALIZATION", cs.TimerPhase);
        // A dealt champion is theirs, not a hover (the phone would otherwise read "Hovering …" for everyone).
        Assert.All(cs.MyTeam, m => Assert.True(m.IsLocked));
        Assert.False(cs.TheirTeam.Single().IsLocked); // hidden enemy champion
    }

    [Fact]
    public void Aram_session_names_teammates_only_and_never_exposes_enemies()
    {
        var cs = Map(BenchSamples.AramSession);
        var json = JsonSerializer.Serialize(cs);

        Assert.DoesNotContain("enemy-puuid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Enemy", json);
        // League names everyone on your team in ARAM: their puuids (View Profile) and names, nothing of the enemy's.
        Assert.Equal(["ally-puuid", "ally-puuid", "me-puuid", "ally-puuid", "ally-puuid"], cs.MyTeam.Select(m => m.Puuid));
        Assert.Equal(["Ally", "Ally", "Me", "Ally", "Ally"], cs.MyTeam.Select(m => m.RiotId));
    }

    [Fact]
    public void Draft_session_has_no_bench_and_no_rerolls()
    {
        var cs = Map(GameStateSamples.ChampSelectSession);

        Assert.False(cs.BenchEnabled);
        Assert.Null(cs.BenchChampionIds);
        Assert.False(cs.CanBenchSwap);
        Assert.False(cs.AllowRerolling);
        Assert.Equal(0, cs.RerollsRemaining);
        Assert.False(cs.CanReroll);
    }

    [Theory]
    [InlineData("BAN_PICK", false, false)]
    [InlineData("GAME_STARTING", false, false)]
    [InlineData("FINALIZATION", false, true)]
    [InlineData("BAN_PICK", true, true)] // subset picks: the client shows the bench while picking too
    public void Bench_opens_when_the_client_shows_it(string phase, bool subset, bool open)
    {
        var cs = Map(BenchSamples.AramSession
            .Replace(Finalization, $"\"phase\": \"{phase}\"")
            .Replace("\"allowSubsetChampionPicks\": false", $"\"allowSubsetChampionPicks\": {(subset ? "true" : "false")}"));

        Assert.Equal(open, cs.CanBenchSwap);
        Assert.Equal([22, 51, 99], cs.BenchChampionIds); // listed either way
    }

    [Fact]
    public void Bench_with_subset_picks_keeps_an_open_pick_a_hover_until_it_is_locked()
    {
        var cs = Map(BenchSamples.AramSession
            .Replace(Finalization, "\"phase\": \"BAN_PICK\"")
            .Replace("\"allowSubsetChampionPicks\": false", "\"allowSubsetChampionPicks\": true")
            .Replace("\"actions\": []", """
                "actions": [[
                  { "id": 31, "actorCellId": 2, "championId": 103, "type": "pick", "completed": false, "isInProgress": true, "isAllyAction": true },
                  { "id": 32, "actorCellId": 1, "championId": 64, "type": "pick", "completed": true, "isInProgress": false, "isAllyAction": true }
                ]]
                """));

        var me = cs.MyTeam.Single(m => m.IsLocalPlayer);
        Assert.False(me.IsLocked); // hovering 103, still to lock in
        Assert.Equal(31, cs.MyAction?.Id);
        Assert.True(cs.MyTeam.Single(m => m.CellId == 1).IsLocked); // completed pick
        Assert.True(cs.MyTeam.Single(m => m.CellId == 0).IsLocked); // dealt, no action
        Assert.True(cs.CanBenchSwap);
    }

    [Fact]
    public void Rerolls_are_not_offered_with_subset_picks()
    {
        var cs = Map(BenchSamples.AramSession.Replace("\"allowSubsetChampionPicks\": false", "\"allowSubsetChampionPicks\": true"));

        Assert.False(cs.AllowRerolling);
        Assert.False(cs.CanReroll);
    }

    [Fact]
    public void Reroll_button_stays_but_is_unusable_with_no_rerolls_left()
    {
        var cs = Map(BenchSamples.AramSession.Replace("\"rerollsRemaining\": 1", "\"rerollsRemaining\": 0"));

        Assert.True(cs.AllowRerolling);
        Assert.Equal(0, cs.RerollsRemaining);
        Assert.False(cs.CanReroll);
    }

    // --- BenchSwap -----------------------------------------------------------------------------------------------

    private static FakeLcu Handler(string session, string pickable = "[22, 51, 99, 103]")
    {
        var handler = new FakeLcu();
        handler.Routes[SessionPath] = session;
        handler.Routes[PickablePath] = pickable;
        return handler;
    }

    [Fact]
    public async Task BenchSwap_takes_a_champion_the_bench_lists_right_now()
    {
        var handler = Handler(BenchSamples.AramSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.BenchSwap, ChampionId: 51));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET " + SessionPath, "GET " + PickablePath, BenchSwapPath + "51"], handler.Requests);
    }

    [Theory]
    [InlineData("[-1]")] // League's "no list" placeholder: the client's own swap decides
    [InlineData("[]")]
    public async Task BenchSwap_goes_ahead_when_league_reports_no_usable_pickable_list(string pickable)
    {
        var handler = Handler(BenchSamples.AramSession, pickable);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.BenchSwap, ChampionId: 99));

        Assert.True(result.Success, result.Error);
        Assert.Equal(BenchSwapPath + "99", handler.Requests[^1]);
    }

    public static TheoryData<string, int, string> BenchSwapRefusals() => new()
    {
        { BenchSamples.AramSession, 157, "That champion is no longer on the bench." }, // a teammate's champion
        { BenchSamples.AramSession, 103, "That champion is no longer on the bench." }, // my own
        { BenchSamples.AramSession.Replace(Finalization, "\"phase\": \"BAN_PICK\""), 51, "The bench isn't open yet." },
        { BenchSamples.AramSession.Replace("\"benchEnabled\": true", "\"benchEnabled\": false"), 51, "This mode has no bench." },
        { GameStateSamples.ChampSelectSession, 51, "This mode has no bench." },
    };

    [Theory, MemberData(nameof(BenchSwapRefusals))]
    public async Task BenchSwap_refuses_champions_the_bench_does_not_offer_now(string session, int championId, string error)
    {
        var handler = Handler(session);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.BenchSwap, ChampionId: championId));

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.Equal(["GET " + SessionPath], handler.Requests);
    }

    [Fact]
    public async Task BenchSwap_refuses_a_champion_the_player_cannot_play()
    {
        var handler = Handler(BenchSamples.AramSession, "[22, 99, 103]");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.BenchSwap, ChampionId: 51));

        Assert.False(result.Success);
        Assert.Equal("You can't play that champion.", result.Error);
        Assert.Equal(["GET " + SessionPath, "GET " + PickablePath], handler.Requests);
    }

    [Fact]
    public async Task BenchSwap_reports_what_league_says_when_it_refuses()
    {
        var handler = Handler(BenchSamples.AramSession);
        handler.Failures[BenchSwapPath + "51"] = (HttpStatusCode.BadRequest, "Champion is on cooldown");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.BenchSwap, ChampionId: 51));

        Assert.False(result.Success);
        Assert.Equal("League refused that. Wait a moment and try again.", result.Error);
    }

    [Theory]
    [InlineData(CommandType.BenchSwap)]
    [InlineData(CommandType.Reroll)]
    public async Task Bench_commands_outside_champ_select_fail(CommandType type)
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", type, ChampionId: 51));

        Assert.False(result.Success);
        Assert.Equal("Not in champ select.", result.Error);
        Assert.Equal(["GET " + SessionPath], handler.Requests);
    }

    // --- Reroll --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Reroll_posts_the_clients_reroll()
    {
        var handler = Handler(BenchSamples.AramSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.Reroll));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET " + SessionPath, RerollRequest], handler.Requests);
    }

    public static TheoryData<string, string> RerollRefusals() => new()
    {
        { BenchSamples.AramSession.Replace("\"rerollsRemaining\": 1", "\"rerollsRemaining\": 0"), "No rerolls left." },
        { BenchSamples.AramSession.Replace("\"allowRerolling\": true", "\"allowRerolling\": false"), "This mode has no rerolls." },
        { BenchSamples.AramSession.Replace("\"allowSubsetChampionPicks\": false", "\"allowSubsetChampionPicks\": true"), "This mode has no rerolls." },
        { BenchSamples.AramSession.Replace(Finalization, "\"phase\": \"GAME_STARTING\""), "You can't reroll right now." },
        { BenchSamples.AramSession.Replace("\"state\": \"AVAILABLE\"", "\"state\": \"RECEIVED\""), "Finish the champion trade first." },
        { BenchSamples.AramSession.Replace("\"state\": \"AVAILABLE\"", "\"state\": \"SENT\""), "Finish the champion trade first." },
        { GameStateSamples.ChampSelectSession, "This mode has no rerolls." },
    };

    [Theory, MemberData(nameof(RerollRefusals))]
    public async Task Reroll_is_refused_when_the_clients_button_would_be_disabled(string session, string error)
    {
        var handler = Handler(session);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("b1", CommandType.Reroll));

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.Equal(["GET " + SessionPath], handler.Requests);
    }
}
