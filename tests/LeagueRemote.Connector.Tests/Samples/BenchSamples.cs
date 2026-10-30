namespace LeagueRemote.Connector.Tests;

/// <summary>Bench (ARAM-style) champ-select sessions.</summary>
internal static class BenchSamples
{
    /// <summary>
    /// <c>GET /lol-champ-select/v1/session</c> in an ARAM game (queue 450) during FINALIZATION. <b>Composed, not recorded</b>:
    /// no ARAM was running when this was written (Oct 2026). Field names and types come from the client's <c>/help</c>
    /// (<c>TeamBuilderDirect-ChampSelectSession</c>: <c>benchEnabled</c>, <c>benchChampions</c> of
    /// <c>{ championId, isPriority }</c>, <c>allowRerolling</c>, <c>rerollsRemaining</c>); how they are used comes from the
    /// client's champ-select front-end (patch 16.21). Bench modes deal champions without pick actions, so <c>actions</c> is
    /// empty here — replace this with a recorded session once one is seen. Names and puuids are placeholders.
    /// </summary>
    public const string AramSession = """
    {
      "gameId": 7000000001,
      "queueId": 450,
      "localPlayerCellId": 2,
      "isCustomGame": false,
      "benchEnabled": true,
      "benchChampions": [
        { "championId": 22, "isPriority": false },
        { "championId": 51, "isPriority": false },
        { "championId": -1, "isPriority": false },
        { "championId": 99, "isPriority": true }
      ],
      "allowRerolling": true,
      "rerollsRemaining": 1,
      "allowSubsetChampionPicks": false,
      "timer": { "phase": "FINALIZATION", "adjustedTimeLeftInPhase": 52000, "isInfinite": false },
      "actions": [],
      "bans": { "myTeamBans": [], "theirTeamBans": [] },
      "myTeam": [
        { "cellId": 0, "team": 1, "championId": 86, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 32, "gameName": "Ally", "puuid": "ally-puuid" },
        { "cellId": 1, "team": 1, "championId": 64, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 32, "gameName": "Ally", "puuid": "ally-puuid" },
        { "cellId": 2, "team": 1, "championId": 103, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 32, "gameName": "Me", "puuid": "me-puuid" },
        { "cellId": 3, "team": 1, "championId": 157, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 7, "gameName": "Ally", "puuid": "ally-puuid" },
        { "cellId": 4, "team": 1, "championId": 412, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 3, "gameName": "Ally", "puuid": "ally-puuid" }
      ],
      "theirTeam": [
        { "cellId": 5, "team": 2, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy", "puuid": "enemy-puuid" }
      ],
      "trades": [ { "id": 41, "cellId": 0, "state": "AVAILABLE" } ],
      "pickOrderSwaps": [],
      "positionSwaps": []
    }
    """;
}
