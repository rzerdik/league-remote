using LeagueRemote.Connector.State;

namespace LeagueRemote.Connector.Tests;

public sealed class ChampionLanesMapperTests
{
    /// <summary><c>GET /lol-perks/v1/recommended-champion-positions</c>, trimmed: a champion in two lanes, an unknown lane, an id that isn't a number.</summary>
    private const string Positions = """
        {
          "103": { "recommendedPositions": ["MIDDLE"] },
          "1": { "recommendedPositions": ["MIDDLE", "UTILITY"] },
          "102": { "recommendedPositions": ["JUNGLE"] },
          "86": { "recommendedPositions": ["TOP"] },
          "22": { "recommendedPositions": ["BOTTOM", "UTILITY"] },
          "60": { "recommendedPositions": ["NONE"] },
          "-1": { "recommendedPositions": ["TOP"] },
          "x": { "recommendedPositions": ["TOP"] },
          "7": {}
        }
        """;

    [Fact]
    public void Lists_each_champion_under_every_lane_League_recommends_in_id_order()
    {
        var lanes = ChampionLanesMapper.Map(TestLcu.Json(Positions));

        Assert.NotNull(lanes);
        Assert.Equal([86], lanes.Top);
        Assert.Equal([102], lanes.Jungle);
        Assert.Equal([1, 103], lanes.Middle);
        Assert.Equal([22], lanes.Bottom);
        Assert.Equal([1, 22], lanes.Utility);
    }

    [Theory]
    [InlineData("""{ "7": {} }""")]
    [InlineData("""[]""")]
    public void An_answer_without_any_lane_means_no_filter(string json) =>
        Assert.Null(ChampionLanesMapper.Map(TestLcu.Json(json)));

    [Fact]
    public void A_client_that_does_not_answer_means_no_filter() =>
        Assert.Null(ChampionLanesMapper.Map(null));

    [Fact]
    public void The_state_carries_the_lanes_in_every_phase()
    {
        var state = GameStateMapper.Build(new LcuSnapshot("None", ChampionPositions: TestLcu.Json(Positions)), DateTimeOffset.UnixEpoch);

        Assert.Equal([1, 103], state.ChampionLanes!.Middle);
    }
}
