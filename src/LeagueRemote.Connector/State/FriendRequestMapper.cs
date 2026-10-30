using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Maps <c>GET /lol-chat/v2/friend-requests</c> (verified against a live client, patch 16.21) to <see cref="FriendRequest"/>s. Each entry has <c>direction</c> "in" / "out" (the LCU enum also has
/// "both", an accepted request on its way out of the list, which is dropped), <c>puuid</c>, <c>gameName</c>, <c>tagLine</c>,
/// <c>icon</c> (-1 for outgoing) and <c>id</c> "0" (so requests are keyed by puuid, as the client's own social plugin does).
/// </summary>
internal static class FriendRequestMapper
{
    public static IReadOnlyList<FriendRequest> Map(JsonElement? requests)
    {
        var result = new List<FriendRequest>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in requests.Objects())
        {
            if (!RiotIds.IsPuuid(r.Str("puuid")) || Direction(r) is not { } direction)
                continue;
            var puuid = r.Str("puuid")!;
            if (!seen.Add(puuid))
                continue;

            result.Add(new FriendRequest(
                puuid, direction, r.GameNameOrName(), r.Str("tagLine") ?? "", Math.Max(0, r.Int("icon")),
                Text.Clip(r.Str("note"), FriendsMapper.MaxTextLength)));
        }

        return
        [
            .. result
                .OrderBy(r => r.Direction == FriendRequestDirections.Incoming ? 0 : 1)
                .ThenBy(r => r.GameName, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(r => r.TagLine, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>The pending request for <paramref name="puuid"/> in the given direction, or null.</summary>
    public static FriendRequest? Find(JsonElement? requests, string puuid, string direction) =>
        Map(requests).FirstOrDefault(r => r.Puuid == puuid && r.Direction == direction);

    private static string? Direction(JsonElement r) => r.Str("direction") switch
    {
        "in" => FriendRequestDirections.Incoming,
        "out" => FriendRequestDirections.Outgoing,
        _ => null,
    };
}
