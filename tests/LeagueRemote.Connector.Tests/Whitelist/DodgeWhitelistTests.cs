using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class DodgeWhitelistTests
{
    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("d1", CommandType.DodgeChampSelect),
        // Extra fields are ignored, as for every other command: the dodge takes no arguments from the phone.
        new("d2", CommandType.DodgeChampSelect, ChampionId: 103, ConversationId: "c1"),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_a_dodge_without_parameters(RemoteCommand command) => Assert.Null(CommandWhitelist.Validate(command));

    [Fact]
    public void Still_needs_a_request_id() =>
        Assert.NotNull(CommandWhitelist.Validate(new RemoteCommand("", CommandType.DodgeChampSelect)));
}
