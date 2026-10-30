using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class PostGameWhitelistTests
{
    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("p1", CommandType.PlayAgain),
        new("p2", CommandType.LeavePostGame),
        // Extra fields are ignored, as for every other command.
        new("p3", CommandType.PlayAgain, QueueId: 420),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_the_post_game_commands_without_parameters(RemoteCommand command) =>
        Assert.Null(CommandWhitelist.Validate(command));

    [Theory]
    [InlineData(CommandType.PlayAgain)]
    [InlineData(CommandType.LeavePostGame)]
    public void Still_needs_a_request_id(CommandType type) =>
        Assert.NotNull(CommandWhitelist.Validate(new RemoteCommand("", type)));
}
