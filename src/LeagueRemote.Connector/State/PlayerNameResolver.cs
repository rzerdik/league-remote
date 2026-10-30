using System.Text.Json;
using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Resolves names for lobby members and invitation senders / recipients whose JSON lacks a game name (current LCU builds
/// often send an empty <c>gameName</c> and <c>summonerName</c> in <c>/lol-lobby/v2/lobby</c>, and empty
/// <c>fromSummonerName</c> / <c>toSummonerName</c> in invitations). Uses the current summoner for the local
/// player and the read-only summoner endpoints (by puuid, else by summoner id) for the others.
/// One instance per LCU connection; successful lookups are cached for its lifetime, misses (404 / no name) only for
/// <see cref="UnknownRetryAfter"/> so a player the LCU did not know yet is retried. Not thread-safe
/// (the refresh loop is sequential).
/// </summary>
internal sealed class PlayerNameResolver(LcuClient client, ILogger logger, TimeProvider time)
{
    internal static readonly TimeSpan UnknownRetryAfter = TimeSpan.FromSeconds(45);

    private readonly Dictionary<string, (string? Name, DateTimeOffset? ExpiresAt)> _cache = [];
    private JsonElement? _currentSummoner;
    private bool _currentSummonerLoaded;

    /// <summary>
    /// Names for the members of <paramref name="lobby"/> and the players it invited that need resolving, keyed by
    /// <see cref="RiotIdNames.NameKeys"/>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> ResolveLobbyAsync(JsonElement? lobby, CancellationToken ct) =>
        ResolvePlayersAsync(LobbyMapper.PlayersToResolve(lobby), ct);

    /// <summary>Names for the senders of <paramref name="receivedInvitations"/> that need resolving.</summary>
    public Task<IReadOnlyDictionary<string, string>> ResolveInvitersAsync(JsonElement? receivedInvitations, CancellationToken ct) =>
        ResolvePlayersAsync(InvitationMapper.InvitersToResolve(receivedInvitations), ct);

    private async Task<IReadOnlyDictionary<string, string>> ResolvePlayersAsync(IEnumerable<PlayerRef> players, CancellationToken ct)
    {
        var names = new Dictionary<string, string>();
        foreach (var player in players)
        {
            var keys = RiotIdNames.NameKeys(player).ToList();
            if (keys.Count == 0 || keys.Any(names.ContainsKey))
                continue;

            if (await ResolveAsync(player, keys, ct) is { } name)
            {
                foreach (var key in keys)
                    names[key] = name;
            }
        }

        return names;
    }

    private async Task<string?> ResolveAsync(PlayerRef player, List<string> keys, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var key in keys)
        {
            if (!_cache.TryGetValue(key, out var cached))
                continue;
            if (cached.ExpiresAt is not { } expiresAt || expiresAt > now)
                return cached.Name;
            _cache.Remove(key); // expired miss: look it up again
        }

        string? name;
        try
        {
            name = await LookUpAsync(player, keys, ct);
        }
        catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
        {
            // Not cached, the next refresh retries. The mapper falls back to summonerName / "Unknown".
            logger.LogDebug("Could not resolve a player's name: {Message}", ex.Message);
            return null;
        }

        var entry = (name, name is null ? now + UnknownRetryAfter : (DateTimeOffset?)null);
        foreach (var key in keys)
            _cache[key] = entry;
        return name;
    }

    /// <returns>The name, or null when the LCU does not know the player (404 / no name).</returns>
    private async Task<string?> LookUpAsync(PlayerRef player, List<string> keys, CancellationToken ct)
    {
        if (!_currentSummonerLoaded)
        {
            _currentSummoner = await client.GetCurrentSummonerAsync(ct);
            _currentSummonerLoaded = _currentSummoner is not null; // 404 (not logged in yet): ask again next time
        }

        if (_currentSummoner is { ValueKind: JsonValueKind.Object } me && RiotIdNames.NameKeys(PlayerRef.Of(me)).Any(keys.Contains))
            return RiotIdNames.SummonerName(me);

        JsonElement? summoner = null;
        if (player.Puuid is { Length: > 0 } puuid)
            summoner = await client.GetSummonerByPuuidAsync(puuid, ct);
        if (summoner is not { ValueKind: JsonValueKind.Object } && player.SummonerId is > 0 and var summonerId)
            summoner = await client.GetSummonerByIdAsync(summonerId, ct);

        return summoner is { ValueKind: JsonValueKind.Object } s ? RiotIdNames.SummonerName(s) : null;
    }
}
