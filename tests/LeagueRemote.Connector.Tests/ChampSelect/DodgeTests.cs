using System.Net;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary>Dodging champ select: <see cref="ChampSelectState.CanDodge"/> and the <see cref="CommandType.DodgeChampSelect"/> command.</summary>
public sealed class DodgeTests
{
    private const string SessionPath = "/lol-champ-select/v1/session";
    private const string QuitRequest = "POST /lol-lobby-team-builder/champ-select/v1/session/quit";
    private const string LocalCell = "\"localPlayerCellId\": 2,";

    private static string WithCustomFlag(string session, bool custom) =>
        session.Replace(LocalCell, LocalCell + $" \"isCustomGame\": {(custom ? "true" : "false")},");

    private static string Starting(string session) => session.Replace("\"phase\": \"BAN_PICK\"", "\"phase\": \"GAME_STARTING\"");

    private static ChampSelectState Map(string sessionJson) =>
        ChampSelectMapper.Map(TestLcu.Json(sessionJson), [103, 222], [])!;

    private static FakeLcu Handler(string session)
    {
        var handler = new FakeLcu();
        handler.Routes[SessionPath] = session;
        return handler;
    }

    [Fact]
    public void A_matchmade_draft_can_be_dodged()
    {
        Assert.True(Map(GameStateSamples.ChampSelectSession).CanDodge);
        Assert.True(Map(WithCustomFlag(GameStateSamples.ChampSelectSession, custom: false)).CanDodge);
    }

    [Fact]
    public void A_custom_game_or_a_starting_game_cannot_be_dodged()
    {
        Assert.False(Map(WithCustomFlag(GameStateSamples.ChampSelectSession, custom: true)).CanDodge);
        Assert.False(Map(Starting(GameStateSamples.ChampSelectSession)).CanDodge);
    }

    [Fact]
    public async Task DodgeChampSelect_quits_the_draft_with_the_team_builder_endpoint()
    {
        var handler = Handler(GameStateSamples.ChampSelectSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("d1", CommandType.DodgeChampSelect));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["GET " + SessionPath, QuitRequest], handler.Requests);
    }

    public static TheoryData<string, string> Refusals() => new()
    {
        { WithCustomFlag(GameStateSamples.ChampSelectSession, custom: true), "You can't dodge a custom game." },
        { Starting(GameStateSamples.ChampSelectSession), "The game is already starting." },
    };

    [Theory, MemberData(nameof(Refusals))]
    public async Task DodgeChampSelect_is_refused_where_there_is_nothing_to_dodge(string session, string error)
    {
        var handler = Handler(session);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("d1", CommandType.DodgeChampSelect));

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.Equal(["GET " + SessionPath], handler.Requests);
    }

    [Fact]
    public async Task DodgeChampSelect_outside_champ_select_fails()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("d1", CommandType.DodgeChampSelect));

        Assert.False(result.Success);
        Assert.Equal("Not in champ select.", result.Error);
        Assert.Equal(["GET " + SessionPath], handler.Requests);
    }

    [Fact]
    public async Task DodgeChampSelect_is_revalidated_by_the_connector_before_anything_reaches_league()
    {
        var handler = Handler(GameStateSamples.ChampSelectSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("", CommandType.DodgeChampSelect));

        Assert.False(result.Success);
        Assert.Equal("RequestId is required (max 64 chars).", result.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DodgeChampSelect_refused_by_league_gives_a_short_sentence()
    {
        var handler = Handler(GameStateSamples.ChampSelectSession);
        handler.Failures[QuitRequest] = (HttpStatusCode.InternalServerError, "QUIT_FAILED");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("d1", CommandType.DodgeChampSelect));

        Assert.False(result.Success);
        Assert.Equal("League couldn't do that. Try again.", result.Error);
    }
}
