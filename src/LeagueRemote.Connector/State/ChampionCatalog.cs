using System.Text.Json;
using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Every champion in the game (<c>/lol-game-data/assets/v1/champion-summary.json</c>), fetched lazily, once, and cached for
/// one LCU connection. Used as the ban-list fallback when League reports no usable bannable ids (<see cref="GetAllAsync"/>),
/// by the live game to turn the Live Client Data API's champion names into ids (<see cref="GetIdsByNameAsync"/>), and by the
/// champ-select automation for its log lines (<see cref="GetNameAsync"/>).
/// Not thread-safe (each user calls it from one sequential loop and has its own instance).
/// </summary>
internal sealed class ChampionCatalog(LcuClient client)
{
    private IReadOnlyList<int>? _all;
    private IReadOnlyDictionary<string, int>? _byName;
    private IReadOnlyDictionary<int, string>? _names;

    /// <returns>The ids, or null when the game data is not available (yet); a miss is retried on the next call.</returns>
    public async Task<IReadOnlyList<int>?> GetAllAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return _all;
    }

    /// <summary>
    /// Champion id by internal name (<c>alias</c>, e.g. "MonkeyKing" → 62) and, where no alias claims it, by display name
    /// (<c>name</c>, "Wukong" → 62), case-insensitive.
    /// </summary>
    /// <returns>The map, or null when the game data is not available (yet); a miss is retried on the next call.</returns>
    public async Task<IReadOnlyDictionary<string, int>?> GetIdsByNameAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return _byName;
    }

    /// <returns>The champion's display name ("Ahri"), or null when unknown or the game data is not available (yet).</returns>
    public async Task<string?> GetNameAsync(int championId, CancellationToken ct)
    {
        await LoadAsync(ct);
        return _names?.GetValueOrDefault(championId);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        if (_all is not null)
            return;

        try
        {
            var summary = await client.GetChampionSummaryAsync(ct);
            var ids = Ids(summary);
            if (ids.Count == 0)
                return;
            _all = ids;
            _byName = IdsByName(summary);
            _names = NamesById(summary);
        }
        catch (LcuRequestException)
        {
            // Not available yet; retried on the next call.
        }
    }

    /// <summary>Ids of every champion, in the file's order; the "None" entry with id -1 is dropped.</summary>
    internal static IReadOnlyList<int> Ids(JsonElement? summary) =>
        [.. summary.Objects().Select(c => c.Int("id")).Where(id => id > 0)];

    /// <summary>Display name by id (the first entry of an id wins); entries without a name are left out.</summary>
    internal static IReadOnlyDictionary<int, string> NamesById(JsonElement? summary)
    {
        var map = new Dictionary<int, string>();
        foreach (var c in summary.Objects())
        {
            if (c.Int("id") is > 0 and var id && c.Str("name") is { Length: > 0 } name)
                map.TryAdd(id, name);
        }

        return map;
    }

    /// <summary>
    /// Aliases first, then display names that no alias or earlier entry took. The file lists the regular champions before
    /// mode variants that reuse their display name (<c>Jade_Annie</c>, id 60001, is also "Annie"), so a name keeps the
    /// lowest id.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> IdsByName(JsonElement? summary)
    {
        var champions = summary.Objects().Select(c => (Id: c.Int("id"), Alias: c.Str("alias"), Name: c.Str("name")))
            .Where(c => c.Id > 0)
            .OrderBy(c => c.Id)
            .ToList();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in champions)
        {
            if (!string.IsNullOrEmpty(c.Alias))
                map.TryAdd(c.Alias, c.Id);
        }

        foreach (var c in champions)
        {
            if (!string.IsNullOrEmpty(c.Name))
                map.TryAdd(c.Name, c.Id);
        }

        return map;
    }
}
