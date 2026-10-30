namespace LeagueRemote.Contracts;

/// <summary>Which way a pending friend request goes (LCU <c>direction</c> "in" / "out").</summary>
public static class FriendRequestDirections
{
    /// <summary>Someone asked to be the user's friend: accept or decline.</summary>
    public const string Incoming = "incoming";

    /// <summary>The user asked someone: cancel.</summary>
    public const string Outgoing = "outgoing";
}

/// <summary>
/// A pending friend request from <c>GET /lol-chat/v2/friend-requests</c> (part of <see cref="FriendsState.Requests"/>).
/// The LCU keys requests by puuid (their <c>id</c> is "0"), so the request commands take <see cref="Puuid"/>.
/// </summary>
/// <param name="Direction">See <see cref="FriendRequestDirections"/>.</param>
/// <param name="GameName">Riot ID game name.</param>
/// <param name="TagLine">Riot ID tag line, without '#'.</param>
/// <param name="ProfileIconId">0 when League sends none (it sends -1 for outgoing requests).</param>
/// <param name="Note">The note League shows with the request; usually empty.</param>
public sealed record FriendRequest(string Puuid, string Direction, string GameName, string TagLine, int ProfileIconId, string Note);
