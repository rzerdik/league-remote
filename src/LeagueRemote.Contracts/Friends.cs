namespace LeagueRemote.Contracts;

/// <summary>
/// The League client's friend list with presence, as the phone shows it on the Friends tab. Published as its own hub message
/// (<see cref="HubMethods.Client.FriendsUpdated"/>), throttled by the connector because presence churns. Names are shown on
/// the phone (League Remote is private); only a new incoming request's game name may be pushed, if the phone opted in
/// (<see cref="PushPreferences.FriendRequest"/>).
/// </summary>
/// <param name="Groups">The user's own friend groups from the client, in the client's order (by priority).</param>
/// <param name="Friends">Every friend, sorted by name (the phone groups them by <see cref="Friend.Availability"/>).</param>
public sealed record FriendsState(IReadOnlyList<FriendGroup> Groups, IReadOnlyList<Friend> Friends, DateTimeOffset UpdatedAt)
{
    public static FriendsState Empty() => new([], [], DateTimeOffset.UtcNow);

    public const int MaxFriendIdLength = 256;

    /// <summary>Pending friend requests both ways (<see cref="FriendRequest"/>), incoming first, each direction by name.</summary>
    public IReadOnlyList<FriendRequest> Requests { get; init; } = [];
}

/// <param name="Id">LCU group id (<see cref="Friend.GroupId"/>).</param>
/// <param name="Name">As the user named it; empty for the default group (LCU name <c>**Default</c>, the phone labels it).</param>
/// <param name="IsDefault">The group friends land in unless the user moved them.</param>
public sealed record FriendGroup(int Id, string Name, bool IsDefault, int Priority);

/// <summary>How a friend shows up in the client (LCU <c>availability</c>, renamed).</summary>
public static class FriendAvailability
{
    /// <summary>Green: online and free (LCU "chat").</summary>
    public const string Online = "online";

    /// <summary>Yellow: away (LCU "away").</summary>
    public const string Away = "away";

    /// <summary>Red: in queue, champ select or a game (LCU "dnd").</summary>
    public const string Busy = "busy";

    /// <summary>Signed in on the mobile app only (LCU "mobile"): can get messages, not invites.</summary>
    public const string Mobile = "mobile";

    public const string Offline = "offline";
}

/// <summary>What a friend is doing in League (from <c>lol.gameStatus</c>); empty when not in League or idle.</summary>
public static class FriendActivities
{
    public const string None = "";

    /// <summary>In a lobby (LCU "hosting_…").</summary>
    public const string Lobby = "lobby";

    /// <summary>Searching for a match (LCU "inQueue").</summary>
    public const string Queue = "queue";

    /// <summary>LCU "championSelect".</summary>
    public const string ChampSelect = "champ-select";

    /// <summary>LCU "inGame"; <see cref="Friend.ActivitySince"/> is when the game started.</summary>
    public const string InGame = "in-game";

    /// <summary>LCU "spectating".</summary>
    public const string Spectating = "spectating";
}

/// <param name="Id">LCU friend id (<c>{puuid}@eu2.pvp.net</c>). Also the id of the direct conversation with this friend
/// (<see cref="ChatConversation.Id"/>) and what <see cref="CommandType.RemoveFriend"/> takes.</param>
/// <param name="SummonerId">0 when unknown.</param>
/// <param name="GameName">Riot ID game name.</param>
/// <param name="TagLine">Riot ID tag line, without '#'.</param>
/// <param name="GroupId">See <see cref="FriendGroup.Id"/>.</param>
/// <param name="Availability">See <see cref="FriendAvailability"/>.</param>
/// <param name="Activity">See <see cref="FriendActivities"/>; empty when idle, offline or not in League.</param>
/// <param name="Product">Online in another Riot product: its name ("Riot Client", "VALORANT"); empty for League or offline.</param>
/// <param name="QueueId">The queue of their lobby, queue, draft or game; null when none.</param>
/// <param name="GameMode">LCU game mode ("CLASSIC", "ARAM"…) while in a game; empty otherwise.</param>
/// <param name="ChampionId">The champion they play in a game; null otherwise.</param>
/// <param name="ActivitySince">When the current activity began (the game's start while in game); null when unknown.</param>
/// <param name="StatusMessage">Their own status line in the client; may be empty.</param>
/// <param name="Note">The note the user wrote about this friend in the client; may be empty.</param>
/// <param name="RankTier">Solo/duo or flex tier as the client shows it ("GOLD"); empty when unranked.</param>
/// <param name="RankDivision">"I"–"IV"; empty for apex tiers or unranked.</param>
/// <param name="RankQueue">"RANKED_SOLO_5x5" or "RANKED_FLEX_SR"; empty when unranked.</param>
/// <param name="LastSeenOnline">When League last saw them online (offline friends; often missing).</param>
public sealed record Friend(
    string Id,
    string Puuid,
    long SummonerId,
    string GameName,
    string TagLine,
    int GroupId,
    string Availability,
    string Activity,
    string Product,
    int? QueueId,
    string GameMode,
    int? ChampionId,
    DateTimeOffset? ActivitySince,
    string StatusMessage,
    string Note,
    int ProfileIconId,
    string RankTier,
    string RankDivision,
    string RankQueue,
    DateTimeOffset? LastSeenOnline);
