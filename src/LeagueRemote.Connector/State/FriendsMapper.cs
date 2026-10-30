using System.Globalization;
using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Pure mapping from <c>/lol-chat/v1/friends</c> and <c>/lol-chat/v1/friend-groups</c> to <see cref="FriendsState"/>.
/// Presence comes from the friend's <c>lol</c> map, whose values are all strings (verified against a live client, Oct 2026):
/// <c>gameStatus</c> ("outOfGame", "hosting_…", "inQueue", "championSelect", "inGame", "spectating"), <c>queueId</c>,
/// <c>championId</c>, <c>gameMode</c>, <c>timeStamp</c> (ms; the game's start while in game), <c>pty</c> (party JSON) and
/// the ranked fields. Friends online in another Riot product have an empty <c>lol</c> map.
/// </summary>
internal static class FriendsMapper
{
    /// <summary>
    /// Sent to the phone at most, to keep the message small (a friend is ~0.4 KB). Offline friends are dropped first; the
    /// client's own roster limit is lower in practice.
    /// </summary>
    public const int MaxFriends = 400;

    public const int MaxTextLength = 200;

    public const string DefaultGroupName = "**Default";
    public const string LeagueProduct = "league_of_legends";

    public static FriendsState Build(JsonElement? friends, JsonElement? groups, DateTimeOffset now)
    {
        var kept = friends.Objects()
            .Where(f => !string.IsNullOrEmpty(f.Str("id")))
            .Select(MapFriend)
            .OrderBy(f => f.Availability == FriendAvailability.Offline)
            .Take(MaxFriends)
            .OrderBy(f => f.GameName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(f => f.TagLine, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToList();
        return new FriendsState(MapGroups(groups), kept, now);
    }

    // Friend lookups for the commands: they only ever act on a friend the client lists right now (raw LCU JSON of
    // /lol-chat/v1/friends), never on an id or puuid as the phone sent it.

    /// <summary>The friend with this <see cref="Friend.Id"/> (chat id); null when not on the list.</summary>
    public static JsonElement? FindById(JsonElement? friends, string id) =>
        friends.Objects().Cast<JsonElement?>().FirstOrDefault(f => f!.Value.Str("id") == id);

    /// <summary>The friend with this puuid (compared case-insensitively, like chat ids); null when not on the list.</summary>
    public static JsonElement? FindByPuuid(JsonElement? friends, string puuid) =>
        friends.Objects().Cast<JsonElement?>()
            .FirstOrDefault(f => string.Equals(f!.Value.Str("puuid"), puuid, StringComparison.OrdinalIgnoreCase));

    /// <summary>A friend's chat id (<c>{puuid}@{domain}</c>: what a direct conversation is created with); null when it has none.</summary>
    public static string? ChatId(JsonElement friend) =>
        (friend.Str("id") is { Length: > 0 } id ? id : friend.Str("pid")) is { } chatId && chatId.Contains('@') ? chatId : null;

    private static IReadOnlyList<FriendGroup> MapGroups(JsonElement? groups) =>
    [
        .. groups.Objects()
            .Where(g => !g.Bool("isMetaGroup"))
            .Select(g =>
            {
                var name = g.Str("name") ?? "";
                var isDefault = name == DefaultGroupName;
                return new FriendGroup(g.Int("id"), isDefault ? "" : Text.Clip(name, MaxTextLength), isDefault, g.Int("priority"));
            })
            .OrderBy(g => g.Priority),
    ];

    private static Friend MapFriend(JsonElement f)
    {
        var lol = f.Obj("lol") ?? default;
        var availability = Availability(f.Str("availability"));
        var online = availability is not (FriendAvailability.Offline or FriendAvailability.Mobile);
        var inLeague = online && f.Str("product") == LeagueProduct;
        var activity = inLeague ? Activity(Lol(lol, "gameStatus"), Lol(lol, "pty")) : FriendActivities.None;
        var playing = activity is FriendActivities.InGame or FriendActivities.Spectating;
        var (tier, division, queue) = Rank(lol);

        return new Friend(
            Id: f.Str("id")!,
            Puuid: f.Str("puuid") ?? "",
            SummonerId: f.Long("summonerId"),
            GameName: f.GameNameOrName(),
            TagLine: f.Str("gameTag") ?? "",
            GroupId: f.Int("groupId"),
            Availability: availability,
            Activity: activity,
            Product: online && !inLeague ? f.Str("productName") ?? "" : "",
            QueueId: activity != FriendActivities.None && LolInt(lol, "queueId") is > 0 and var q ? q : null,
            GameMode: playing ? Lol(lol, "gameMode") : "",
            ChampionId: activity == FriendActivities.InGame && LolInt(lol, "championId") is > 0 and var c ? c : null,
            ActivitySince: activity != FriendActivities.None ? UnixTime.FromMs(Lol(lol, "timeStamp")) : null,
            StatusMessage: Text.Clip(f.Str("statusMessage"), MaxTextLength),
            Note: Text.Clip(f.Str("note"), MaxTextLength),
            ProfileIconId: f.Int("icon"),
            RankTier: tier,
            RankDivision: division,
            RankQueue: queue,
            LastSeenOnline: availability == FriendAvailability.Offline ? LastSeen(f.Str("lastSeenOnlineTimestamp")) : null);
    }

    internal static string Availability(string? lcu) => lcu switch
    {
        "chat" => FriendAvailability.Online,
        "away" => FriendAvailability.Away,
        "dnd" => FriendAvailability.Busy,
        "mobile" => FriendAvailability.Mobile,
        _ => FriendAvailability.Offline,
    };

    /// <summary>Idle with a party (<c>pty</c> set) counts as in a lobby: the client only says "hosting_…" for some lobbies.</summary>
    internal static string Activity(string gameStatus, string party) => gameStatus switch
    {
        "inQueue" => FriendActivities.Queue,
        "championSelect" => FriendActivities.ChampSelect,
        "inGame" => FriendActivities.InGame,
        "spectating" => FriendActivities.Spectating,
        _ when gameStatus.StartsWith("hosting", StringComparison.Ordinal) => FriendActivities.Lobby,
        "outOfGame" when party.Contains("\"partyId\"", StringComparison.Ordinal) => FriendActivities.Lobby,
        _ => FriendActivities.None,
    };

    private static (string Tier, string Division, string Queue) Rank(JsonElement lol)
    {
        var tier = LeagueRanks.Tier(Lol(lol, "rankedLeagueTier"));
        return tier == ""
            ? ("", "", "")
            : (tier, LeagueRanks.Division(tier, Lol(lol, "rankedLeagueDivision")), Lol(lol, "rankedLeagueQueue"));
    }

    private static string Lol(JsonElement lol, string name) =>
        lol.ValueKind == JsonValueKind.Object ? lol.Str(name) ?? "" : "";

    private static int? LolInt(JsonElement lol, string name) =>
        int.TryParse(Lol(lol, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    /// <summary>The LCU types it as an optional string: unix ms on some builds, ISO on others.</summary>
    private static DateTimeOffset? LastSeen(string? value) =>
        string.IsNullOrEmpty(value) ? null
        : long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? UnixTime.FromMs(value)
        : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso) ? iso
        : null;
}
