namespace LeagueRemote.Contracts;

/// <summary>
/// Which moments push to one phone (one Web Push subscription), set with <see cref="HubMethods.Server.SetPushPreferences"/>
/// and enforced by the connector where it sends. A missing field keeps its default, so a phone that never sent
/// preferences gets match found, your turn, game started and game invites; champ select starting and friend requests are
/// opt-in.
/// </summary>
public sealed record PushPreferences
{
    /// <summary>The ready check pops ("Match found").</summary>
    public bool MatchFound { get; init; } = true;

    /// <summary>The gameflow enters champ select. Off by default.</summary>
    public bool ChampSelectStarted { get; init; }

    /// <summary>The user's own pick or ban turn starts.</summary>
    public bool YourTurn { get; init; } = true;

    /// <summary>The game has loaded.</summary>
    public bool GameStarted { get; init; } = true;

    /// <summary>Someone invites the user to a lobby (inviter name and queue only).</summary>
    public bool LobbyInvite { get; init; } = true;

    /// <summary>Someone sends the user a friend request (their Riot ID game name only). Off by default.</summary>
    public bool FriendRequest { get; init; }

    public static PushPreferences Default { get; } = new();
}
