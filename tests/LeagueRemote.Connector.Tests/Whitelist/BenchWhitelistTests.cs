using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class BenchWhitelistTests
{
    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("b1", CommandType.BenchSwap, ChampionId: 103),
        new("b2", CommandType.Reroll),
        // Extra fields are ignored, as for every other command.
        new("b3", CommandType.Reroll, ChampionId: 103),
    ];

    public static TheoryData<RemoteCommand> Invalid() =>
    [
        new("b1", CommandType.BenchSwap),
        new("b2", CommandType.BenchSwap, ChampionId: 0),
        new("b3", CommandType.BenchSwap, ChampionId: -1),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_valid_commands(RemoteCommand command) => Assert.Null(CommandWhitelist.Validate(command));

    [Theory, MemberData(nameof(Invalid))]
    public void Rejects_a_bench_swap_without_a_positive_champion(RemoteCommand command) =>
        Assert.Equal("ChampionId must be a positive integer.", CommandWhitelist.Validate(command));
}
