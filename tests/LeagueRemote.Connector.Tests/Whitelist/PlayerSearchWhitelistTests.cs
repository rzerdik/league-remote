using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="CommandType.SearchPlayer"/>: a Riot ID under Riot's own rules, the same ones Add Friend checks.</summary>
public sealed class PlayerSearchWhitelistTests
{
    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("s1", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "QX7"),
        new("s2", CommandType.SearchPlayer, GameName: "Zażółć Test", TagLine: "TAG1"),
        new("s3", CommandType.SearchPlayer, GameName: "SixteenCharsName", TagLine: "11593"),
        new("s4", CommandType.SearchPlayer, GameName: "quillon", TagLine: "qx7"), // League matches any case
        new("s5", CommandType.SearchPlayer, GameName: "Ünïcödé", TagLine: "ÄÖÜ"),
        new("s6", CommandType.SearchPlayer, GameName: "a&tagLine=x", TagLine: "QX7"), // as Add Friend allows; the connector escapes it
    ];

    public static TheoryData<RemoteCommand> Invalid() =>
    [
        new("s1", CommandType.SearchPlayer),
        new("s2", CommandType.SearchPlayer, GameName: "Quillon"),
        new("s3", CommandType.SearchPlayer, TagLine: "QX7"),
        new("s4", CommandType.SearchPlayer, GameName: "ab", TagLine: "QX7"),
        new("s5", CommandType.SearchPlayer, GameName: "SeventeenCharName", TagLine: "QX7"),
        new("s6", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "QX"),
        new("s7", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "QX7234"),
        new("s8", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "Q&X"),
        new("s9", CommandType.SearchPlayer, GameName: "Quil#lon", TagLine: "QX7"),
        new("s10", CommandType.SearchPlayer, GameName: " Quillon", TagLine: "QX7"),
        new("s11", CommandType.SearchPlayer, GameName: "Quil\u0000lon", TagLine: "QX7"),
        new("s13", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "Q/X7"),
        // A puuid is no Riot ID: the phone never searches by one.
        new("s14", CommandType.SearchPlayer, Puuid: "bbbb0001-0000-0000-0000-000000000001"),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_valid_commands(RemoteCommand command) => Assert.Null(CommandWhitelist.Validate(command));

    [Theory, MemberData(nameof(Invalid))]
    public void Rejects_invalid_commands(RemoteCommand command) => Assert.NotNull(CommandWhitelist.Validate(command));

    [Fact]
    public void Says_what_is_wrong_with_the_riot_id()
    {
        Assert.Equal("Enter a game name.", CommandWhitelist.Validate(new RemoteCommand("s", CommandType.SearchPlayer, TagLine: "QX7")));
        Assert.Equal("Tag lines are letters and digits only.",
            CommandWhitelist.Validate(new RemoteCommand("s", CommandType.SearchPlayer, GameName: "Quillon", TagLine: "Q-7")));
    }
}
