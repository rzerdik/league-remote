using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Turns <c>/lol-perks/v1/recommended-champion-positions</c> (champion id → <c>recommendedPositions</c>) into the lists the
/// champion pickers filter by. It also lists ids that aren't champions (66602, …): the phone only shows what Data Dragon
/// knows, so those drop out there.
/// </summary>
internal static class ChampionLanesMapper
{
    /// <returns>The lanes in ascending id order; null when the answer lists no lane at all.</returns>
    public static ChampionLanes? Map(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } positions)
            return null;

        var lanes = new Dictionary<string, List<int>>
        {
            ["TOP"] = [],
            ["JUNGLE"] = [],
            ["MIDDLE"] = [],
            ["BOTTOM"] = [],
            ["UTILITY"] = [],
        };
        foreach (var champion in positions.EnumerateObject())
        {
            if (!int.TryParse(champion.Name, out var id) || id <= 0 || champion.Value.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var position in champion.Value.Arr("recommendedPositions"))
            {
                if (position.ValueKind == JsonValueKind.String && lanes.TryGetValue(position.GetString()!, out var lane))
                    lane.Add(id);
            }
        }

        if (lanes.Values.All(l => l.Count == 0))
            return null;
        foreach (var lane in lanes.Values)
            lane.Sort();
        return new ChampionLanes(lanes["TOP"], lanes["JUNGLE"], lanes["MIDDLE"], lanes["BOTTOM"], lanes["UTILITY"]);
    }
}
