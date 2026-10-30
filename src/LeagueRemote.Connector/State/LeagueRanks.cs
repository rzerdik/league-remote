namespace LeagueRemote.Connector.State;

/// <summary>How League's ranked fields (ranked stats, LP notifications, friends' presence) are normalised for the phone.</summary>
internal static class LeagueRanks
{
    /// <summary>The LCU game mode of Arena, whose games and ranks work per duo.</summary>
    public const string ArenaGameMode = "CHERRY";

    /// <summary>Tiers with a single division, which the client doesn't show.</summary>
    private static readonly string[] ApexTiers = ["MASTER", "GRANDMASTER", "CHALLENGER"];

    /// <summary>The tier as League spells it ("GOLD"); empty when unranked ("NONE", "UNRANKED" or missing).</summary>
    public static string Tier(string? tier) =>
        string.IsNullOrEmpty(tier) || tier is "NONE" or "UNRANKED" ? "" : tier;

    /// <summary>"I"–"IV"; empty when unranked, for apex tiers and for League's "NA".</summary>
    public static string Division(string tier, string? division) =>
        tier == "" || ApexTiers.Contains(tier) || string.IsNullOrEmpty(division) || division == "NA" ? "" : division;
}
