namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Champ-select sessions for the automation, in the shape of <c>GET /lol-champ-select/v1/session</c>
/// (<c>TeamBuilderDirect-ChampSelectSession</c> in the client's <c>/help</c>, Oct 2026) and the existing recorded samples
/// (<see cref="GameStateSamples.ChampSelectSession"/>), trimmed to the fields the automation reads. No champ select ran while this
/// feature was built, so these are composed, not recorded; names and ids are placeholders.
/// </summary>
internal static class AutoPickSamples
{
    /// <summary>
    /// Ranked Solo/Duo (420), BAN_PICK with 30 s left: every player's ban is in progress, the local player is cell 2
    /// (middle, Flash + Ghost). Cell 1 (top) and cell 3 (jungle) are teammates; cells 6 and 7 enemies. Picks follow
    /// in groups: 20 (cell 1), 21 (me) + 22 (cell 6), 23 (cell 3) + 24 (cell 7).
    /// </summary>
    public const string DraftBanTurn = """
    {
      "gameId": 7001,
      "queueId": 420,
      "isCustomGame": false,
      "benchEnabled": false,
      "localPlayerCellId": 2,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 30000, "isInfinite": false },
      "bans": { "myTeamBans": [], "theirTeamBans": [] },
      "myTeam": [
        { "cellId": 1, "championId": 0, "championPickIntent": 0, "assignedPosition": "top", "spell1Id": 4, "spell2Id": 12, "gameName": "Ally", "tagLine": "EUW" },
        { "cellId": 2, "championId": 0, "championPickIntent": 0, "assignedPosition": "middle", "spell1Id": 4, "spell2Id": 6, "gameName": "Me", "tagLine": "EUW" },
        { "cellId": 3, "championId": 0, "championPickIntent": 0, "assignedPosition": "jungle", "spell1Id": 4, "spell2Id": 11, "gameName": "Ally2", "tagLine": "EUW" }
      ],
      "theirTeam": [
        { "cellId": 6, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy", "puuid": "enemy-puuid", "summonerId": 9006 },
        { "cellId": 7, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy2", "puuid": "enemy2-puuid", "summonerId": 9007 }
      ],
      "actions": [
        [
          { "id": 10, "actorCellId": 1, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 11, "actorCellId": 2, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 12, "actorCellId": 3, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 13, "actorCellId": 6, "championId": 0, "type": "ban", "completed": false, "isAllyAction": false, "isInProgress": true },
          { "id": 14, "actorCellId": 7, "championId": 0, "type": "ban", "completed": false, "isAllyAction": false, "isInProgress": true }
        ],
        [
          { "id": 20, "actorCellId": 1, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false }
        ],
        [
          { "id": 21, "actorCellId": 2, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false },
          { "id": 22, "actorCellId": 6, "championId": 0, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": false }
        ],
        [
          { "id": 23, "actorCellId": 3, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false },
          { "id": 24, "actorCellId": 7, "championId": 0, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": false }
        ]
      ]
    }
    """;

    /// <summary>
    /// The same draft later: the bans are done (Katarina 55 by cell 1, my ban Yone 777, Lee Sin 64 listed only in
    /// <c>bans.theirTeamBans</c>), cell 1 locked Ahri (103), enemy cell 6 locked Orianna (61), and the local player's pick (21)
    /// is in progress with 27.5 s left. Teammate cell 3 hovers Lux (99) on their pick and declared Ezreal (81) as intent.
    /// </summary>
    public const string DraftPickTurn = """
    {
      "gameId": 7001,
      "queueId": 420,
      "isCustomGame": false,
      "benchEnabled": false,
      "localPlayerCellId": 2,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 27500, "isInfinite": false },
      "bans": { "myTeamBans": [ 55, 777 ], "theirTeamBans": [ 64 ] },
      "myTeam": [
        { "cellId": 1, "championId": 103, "championPickIntent": 0, "assignedPosition": "top", "spell1Id": 4, "spell2Id": 12, "gameName": "Ally", "tagLine": "EUW" },
        { "cellId": 2, "championId": 0, "championPickIntent": 0, "assignedPosition": "middle", "spell1Id": 4, "spell2Id": 6, "gameName": "Me", "tagLine": "EUW" },
        { "cellId": 3, "championId": 99, "championPickIntent": 81, "assignedPosition": "jungle", "spell1Id": 4, "spell2Id": 11, "gameName": "Ally2", "tagLine": "EUW" }
      ],
      "theirTeam": [
        { "cellId": 6, "championId": 61, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy", "puuid": "enemy-puuid", "summonerId": 9006 },
        { "cellId": 7, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy2", "puuid": "enemy2-puuid", "summonerId": 9007 }
      ],
      "actions": [
        [
          { "id": 10, "actorCellId": 1, "championId": 55, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 11, "actorCellId": 2, "championId": 777, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 12, "actorCellId": 3, "championId": 0, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 13, "actorCellId": 6, "championId": 0, "type": "ban", "completed": true, "isAllyAction": false, "isInProgress": false },
          { "id": 14, "actorCellId": 7, "championId": 0, "type": "ban", "completed": true, "isAllyAction": false, "isInProgress": false }
        ],
        [
          { "id": 20, "actorCellId": 1, "championId": 103, "type": "pick", "completed": true, "isAllyAction": true, "isInProgress": false }
        ],
        [
          { "id": 21, "actorCellId": 2, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 22, "actorCellId": 6, "championId": 61, "type": "pick", "completed": true, "isAllyAction": false, "isInProgress": false }
        ],
        [
          { "id": 23, "actorCellId": 3, "championId": 99, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false },
          { "id": 24, "actorCellId": 7, "championId": 0, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": false }
        ]
      ]
    }
    """;

    /// <summary>ARAM (450): champions are rolled (<c>benchEnabled</c>), the local player already has one; no bans.</summary>
    public const string AramSession = """
    {
      "gameId": 7101,
      "queueId": 450,
      "isCustomGame": false,
      "benchEnabled": true,
      "localPlayerCellId": 0,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 60000, "isInfinite": false },
      "bans": { "myTeamBans": [], "theirTeamBans": [] },
      "myTeam": [
        { "cellId": 0, "championId": 22, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 7, "gameName": "Me", "tagLine": "EUW" }
      ],
      "theirTeam": [],
      "actions": [
        [ { "id": 1, "actorCellId": 0, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": true } ]
      ]
    }
    """;

    /// <summary>
    /// SR Blind Pick Custom (queue 3100, <c>SimulPickStrategy</c> in <c>/lol-game-queues/v1/queues</c> on a live client, Oct
    /// 2026): <c>isCustomGame</c>, no assigned positions, no bans; everyone's pick is in progress at once from the start
    /// (BAN_PICK). The local player is cell 1, cell 0 a teammate, cell 5 an enemy (blind: no enemy champion visible).
    /// Composed like the samples above, not recorded (no custom champ select was running).
    /// </summary>
    public const string CustomBlindSession = """
    {
      "gameId": 7201,
      "queueId": 3100,
      "isCustomGame": true,
      "benchEnabled": false,
      "localPlayerCellId": 1,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 90000, "internalNowInEpochMs": 0, "totalTimeInPhase": 90000, "isInfinite": false },
      "bans": { "myTeamBans": [], "theirTeamBans": [], "numBans": 0 },
      "myTeam": [
        { "cellId": 0, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 14, "gameName": "Ally", "tagLine": "EUW" },
        { "cellId": 1, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 7, "gameName": "Me", "tagLine": "EUW" }
      ],
      "theirTeam": [
        { "cellId": 5, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy", "puuid": "enemy-puuid", "summonerId": 9005 }
      ],
      "actions": [
        [
          { "id": 1, "actorCellId": 0, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 2, "actorCellId": 1, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 3, "actorCellId": 5, "championId": 0, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": true }
        ]
      ]
    }
    """;

    /// <summary>
    /// SR Draft Pick Custom (queue 3110, <c>TeamBuilderDraftPickStrategy</c> in the live queue list, Oct 2026), BAN_PICK with
    /// 30 s left: <c>isCustomGame</c>, no assigned positions (custom lobbies have no position selector), every player's ban
    /// in progress; the local player is cell 1 (ban 11, pick 21). Cell 0 a teammate, cell 5 an enemy. Composed, not recorded.
    /// </summary>
    public const string CustomDraftBanTurn = """
    {
      "gameId": 7301,
      "queueId": 3110,
      "isCustomGame": true,
      "benchEnabled": false,
      "localPlayerCellId": 1,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 30000, "internalNowInEpochMs": 0, "totalTimeInPhase": 30000, "isInfinite": false },
      "bans": { "myTeamBans": [], "theirTeamBans": [], "numBans": 6 },
      "myTeam": [
        { "cellId": 0, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 14, "gameName": "Ally", "tagLine": "EUW" },
        { "cellId": 1, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 7, "gameName": "Me", "tagLine": "EUW" }
      ],
      "theirTeam": [
        { "cellId": 5, "championId": 0, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy", "puuid": "enemy-puuid", "summonerId": 9005 }
      ],
      "actions": [
        [
          { "id": 10, "actorCellId": 0, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 11, "actorCellId": 1, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 15, "actorCellId": 5, "championId": 0, "type": "ban", "completed": false, "isAllyAction": false, "isInProgress": true }
        ],
        [
          { "id": 20, "actorCellId": 0, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false }
        ],
        [
          { "id": 21, "actorCellId": 1, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false },
          { "id": 25, "actorCellId": 5, "championId": 0, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": false }
        ]
      ]
    }
    """;

    /// <summary><c>GET /lol-perks/v1/pages</c> trimmed (page names are the user's: placeholders); page 11 is current.</summary>
    public const string RunePages = """
    [
      { "id": 11, "name": "Page A", "current": true, "isEditable": true },
      { "id": 12, "name": "Page B", "current": false, "isEditable": true }
    ]
    """;

    /// <summary><c>/lol-game-data/assets/v1/champion-summary.json</c> trimmed to the champions these tests use.</summary>
    public const string ChampionSummary = """
    [
      { "id": -1, "name": "None", "alias": "None" },
      { "id": 7, "name": "LeBlanc", "alias": "Leblanc" },
      { "id": 55, "name": "Katarina", "alias": "Katarina" },
      { "id": 99, "name": "Lux", "alias": "Lux" },
      { "id": 103, "name": "Ahri", "alias": "Ahri" },
      { "id": 157, "name": "Yasuo", "alias": "Yasuo" },
      { "id": 238, "name": "Zed", "alias": "Zed" },
      { "id": 245, "name": "Ekko", "alias": "Ekko" }
    ]
    """;
}
