using System.Text.Json;

namespace LeagueRemote.Connector.State;

/// <summary>A player to resolve: puuid and/or summoner id (0 when unknown).</summary>
internal readonly record struct PlayerRef(string? Puuid, long SummonerId)
{
    public static PlayerRef Of(JsonElement player) => new(player.Str("puuid"), player.Long("summonerId"));
}

/// <summary>Riot IDs ("Name#TAG") from LCU player entries, and the keys resolved names are stored under.</summary>
internal static class RiotIdNames
{
    /// <summary>
    /// "gameName#tag" when the entry carries a game name, otherwise null (the name needs resolving). Lobby members carry the
    /// tag as "gameTag", champ-select members as "tagLine".
    /// </summary>
    public static string? OwnRiotId(JsonElement member) =>
        RiotId(member.Str("gameName"), member.Str("gameTag") ?? member.Str("tagLine"));

    /// <summary>Riot ID from a summoner (current-summoner, summoners/puuid/..., summoners/{id}); falls back to its display name.</summary>
    public static string? SummonerName(JsonElement summoner) =>
        RiotId(summoner.Str("gameName"), summoner.Str("tagLine")) ?? Text.NullIfBlank(summoner.Str("displayName"));

    /// <summary>Keys a player's resolved name is stored under: the puuid and "id:{summonerId}" (whichever are present).</summary>
    public static IEnumerable<string> NameKeys(PlayerRef player)
    {
        if (Text.NullIfBlank(player.Puuid) is { } puuid)
            yield return puuid;
        if (player.SummonerId > 0)
            yield return $"id:{player.SummonerId}";
    }

    /// <summary>The name resolved for <paramref name="player"/> under any of its <see cref="NameKeys"/>; null when none.</summary>
    public static string? Resolved(PlayerRef player, IReadOnlyDictionary<string, string>? playerNames)
    {
        if (playerNames is null)
            return null;
        foreach (var key in NameKeys(player))
        {
            if (playerNames.TryGetValue(key, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return null;
    }

    private static string? RiotId(string? gameName, string? tag) =>
        string.IsNullOrWhiteSpace(gameName) ? null
        : string.IsNullOrWhiteSpace(tag) ? gameName
        : $"{gameName}#{tag}";
}
