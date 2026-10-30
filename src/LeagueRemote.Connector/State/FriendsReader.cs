using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>The LCU reads of the friends slice: friends with presence, groups and pending friend requests.</summary>
internal static class FriendsReader
{
    public static async Task<FriendsState> ReadAsync(LcuClient client, DateTimeOffset now, CancellationToken ct) =>
        FriendsMapper.Build(await client.GetFriendsAsync(ct), await client.GetFriendGroupsAsync(ct), now) with
        {
            Requests = FriendRequestMapper.Map(await client.GetFriendRequestsAsync(ct)),
        };
}
