using LeagueRemote.Connector.State;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>What <see cref="GameStateMapper"/> builds outside the lobby and champ-select mappers.</summary>
public sealed class GameStateMapperTests
{
    [Fact]
    public void Available_queue_ids_list_only_queues_the_client_offers()
    {
        var queues = Json("""
            [{"id":490,"queueAvailability":"PlatformDisabled"},{"id":420,"queueAvailability":"Available"},
             {"id":1750,"queueAvailability":"Available"},{"id":-1,"queueAvailability":"Available"},"junk"]
            """);

        var state = GameStateMapper.Build(new LcuSnapshot("None", Queues: queues), DateTimeOffset.UnixEpoch);

        Assert.Equal([420, 1750], state.AvailableQueueIds);
        Assert.Null(GameStateMapper.Build(new LcuSnapshot("None"), DateTimeOffset.UnixEpoch).AvailableQueueIds);
    }
}
