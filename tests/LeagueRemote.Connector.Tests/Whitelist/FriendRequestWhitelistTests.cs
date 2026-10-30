using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class FriendRequestWhitelistTests
{
    private const string Puuid = "bbbb0003-0000-0000-0000-000000000003";

    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("f1", CommandType.SendFriendRequest, GameName: "Player", TagLine: "KR1"),
        new("f2", CommandType.SendFriendRequest, GameName: "Zażółć Test", TagLine: "TAG1"),
        new("f3", CommandType.SendFriendRequest, GameName: "SixteenCharsName", TagLine: "11593"),
        new("f4", CommandType.AcceptFriendRequest, Puuid: Puuid),
        new("f5", CommandType.DeclineFriendRequest, Puuid: Puuid),
        new("f6", CommandType.CancelFriendRequest, Puuid: "abc_DEF-123"),
    ];

    public static TheoryData<RemoteCommand> Invalid() =>
    [
        new("f1", CommandType.SendFriendRequest),
        new("f2", CommandType.SendFriendRequest, GameName: "Player"),
        new("f3", CommandType.SendFriendRequest, TagLine: "KR1"),
        new("f4", CommandType.SendFriendRequest, GameName: "ab", TagLine: "KR1"),
        new("f5", CommandType.SendFriendRequest, GameName: "SeventeenCharName", TagLine: "KR1"),
        new("f6", CommandType.SendFriendRequest, GameName: "Player", TagLine: "KR"),
        new("f7", CommandType.SendFriendRequest, GameName: "Player", TagLine: "KR1234"),
        new("f8", CommandType.SendFriendRequest, GameName: "Player", TagLine: "K R1"),
        new("f9", CommandType.SendFriendRequest, GameName: "Pla#yer", TagLine: "KR1"),
        new("f10", CommandType.SendFriendRequest, GameName: "Player ", TagLine: "KR1"),
        new("f11", CommandType.SendFriendRequest, GameName: "Pla\nyer", TagLine: "KR1"),
        new("f12", CommandType.AcceptFriendRequest),
        new("f13", CommandType.DeclineFriendRequest, Puuid: ""),
        new("f14", CommandType.CancelFriendRequest, Puuid: "../lol-chat/v1/friends"),
        new("f15", CommandType.AcceptFriendRequest, Puuid: new string('a', RiotIds.MaxPuuidLength + 1)),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_valid_commands(RemoteCommand command) => Assert.Null(CommandWhitelist.Validate(command));

    [Theory, MemberData(nameof(Invalid))]
    public void Rejects_invalid_commands(RemoteCommand command) => Assert.NotNull(CommandWhitelist.Validate(command));
}
