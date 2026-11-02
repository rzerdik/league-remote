namespace LeagueRemote.Connector.Tests;

/// <summary>
/// <c>GET /lol-matchmaking/v1/ready-check</c>, trimmed to what <c>GameStateMapper.MapReadyCheck</c> reads. Written from that
/// mapper's fields, not recorded: a live client only answers this during a real ready check. Holds no player data.
/// </summary>
internal static class ReadyCheckSamples
{
    /// <summary>A match was found and the local player hasn't answered; vary it with <see cref="Check"/>.</summary>
    public const string InProgress = """
    { "state": "InProgress", "playerResponse": "None", "timer": 1.0 }
    """;

    public static string Check(string state = "InProgress", string playerResponse = "None") =>
        InProgress.Replace("\"InProgress\"", $"\"{state}\"").Replace("\"None\"", $"\"{playerResponse}\"");
}
