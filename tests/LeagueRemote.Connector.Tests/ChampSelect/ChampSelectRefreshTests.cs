using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.Tests;

public sealed class ChampSelectRefreshTests
{
    private const string AllChampions = """
    [ { "id": -1, "name": "None" }, { "id": 1, "name": "Annie" }, { "id": 64, "name": "Lee Sin" }, { "id": 99, "name": "Lux" }, { "id": 103, "name": "Ahri" } ]
    """;

    [Fact]
    public async Task Bannable_minus_one_falls_back_to_all_champions_from_game_data_once_per_connection()
    {
        var handler = ChampSelectHandler(bannable: "[-1]");
        using var http = handler.CreateClient();
        var client = new LcuClient(http);
        var reader = TestLcu.StateReader(client);

        var first = await TestLcu.BuildStateAsync(client, reader);
        var second = await TestLcu.BuildStateAsync(client, reader);

        Assert.Equal([1, 103], first.ChampSelect!.BannableChampionIds); // 64 and 99 already banned
        Assert.Equal([1, 103], second.ChampSelect!.BannableChampionIds);
        Assert.Single(handler.Requests, r => r == "GET /lol-game-data/assets/v1/champion-summary.json");
    }

    [Fact]
    public async Task A_usable_bannable_list_does_not_touch_game_data()
    {
        var handler = ChampSelectHandler(bannable: "[1, 103]");
        using var http = handler.CreateClient();

        var state = await TestLcu.BuildStateAsync(new LcuClient(http));

        Assert.Equal([1, 103], state.ChampSelect!.BannableChampionIds);
        Assert.DoesNotContain(handler.Requests, r => r.Contains("champion-summary"));
    }

    [Fact]
    public async Task Missing_game_data_leaves_an_empty_ban_list_instead_of_failing_the_refresh()
    {
        var handler = ChampSelectHandler(bannable: "[-1]");
        handler.Routes.Remove("/lol-game-data/assets/v1/champion-summary.json");
        using var http = handler.CreateClient();

        var state = await TestLcu.BuildStateAsync(new LcuClient(http));

        Assert.Empty(state.ChampSelect!.BannableChampionIds);
    }

    private static FakeLcu ChampSelectHandler(string bannable)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-gameflow/v1/gameflow-phase"] = "\"ChampSelect\"";
        handler.Routes["/lol-perks/v1/pages"] = "[]";
        handler.Routes["/lol-champ-select/v1/session"] = GameStateSamples.BanPhaseSession;
        handler.Routes["/lol-champ-select/v1/pickable-champion-ids"] = "[1, 64, 99, 103]";
        handler.Routes["/lol-champ-select/v1/bannable-champion-ids"] = bannable;
        handler.Routes["/lol-game-data/assets/v1/champion-summary.json"] = AllChampions;
        return handler;
    }
}
