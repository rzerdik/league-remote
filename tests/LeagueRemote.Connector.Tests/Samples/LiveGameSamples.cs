namespace LeagueRemote.Connector.Tests;

/// <summary>
/// <c>GET https://127.0.0.1:2999/liveclientdata/allgamedata</c> (Riot's Live Client Data API), trimmed to the fields the
/// live-game mapper reads. <b>Not recorded live</b> (no game was running when this was written, Oct 23 2026): the shapes
/// follow Riot's Game Client API docs (developer.riotgames.com/docs/lol#game-client-api, its <c>liveclientdata_sample.json</c>
/// and <c>liveclientdata_events.json</c>, plus the documented Riot ID fields <c>riotId</c> / <c>riotIdGameName</c> /
/// <c>riotIdTagLine</c> and <c>summonerName</c> as a "Name#TAG" shim), and the event fields Riot's sample lacks
/// (<c>FirstBlood.Recipient</c>, <c>HordeKill</c>, <c>EventID</c> counting up) follow community recordings (AlsoSylv/Irelia's
/// <c>events.json</c>). Events name players by game name only (no tag). Replace with a live recording once one is made.
/// Names are placeholders: Me#TAG0, Ally1#TAG1 … Ally4#TAG4 (ORDER), Enemy1#TAG5 … Enemy5#TAG9 (CHAOS).
/// </summary>
internal static class LiveGameSamples
{
    /// <summary>
    /// A ranked-style Summoner's Rift game at 14:00 (<c>gameTime</c> 840.2), the local player Ahri mid on ORDER. Special:
    /// Wukong and Kai'Sa (internal names <c>MonkeyKing</c> / <c>Kaisa</c> differ from the display names), Darius dead after a
    /// turret execution (<c>KillerName</c> "Turret_T1_L_03_A", no champion killer), a stolen drake, the grub camp split 2–1.
    /// What a mapper should make of it at 840.2: ORDER kills 3, towers 1 (the CHAOS bot outer, <c>Turret_T2_R_03_A</c>),
    /// dragons [Fire], grubs 2; CHAOS kills 1, dragons [Water], grubs 1. Timers: dragon 812.6 + 300 = 1112.6 (type unknown,
    /// two drakes so far), baron 1200, herald 900 (despawns 1185), grubs taken (null, despawn 885), no inhibitor down.
    /// </summary>
    public const string AllGameData = BeforeEvents + EarlyEvents + AfterEvents;

    /// <summary>
    /// <see cref="AllGameData"/> with the rest of the game's events (<see cref="LateEvents"/>) and the clock at 1815.0, just
    /// after the Nexus fell. Only the events and clock move on (the players' scores stay at 14:00), so use it for the feed,
    /// counts and timers: herald taken at 955.0 (null); the third drake (Hextech, ORDER) at 1150.3 makes the rift Hextech, so
    /// the next drake spawns at 1450.3 with type Hextech and is up (nobody took it); baron next at 1505.2 + 360 = 1865.2; the
    /// CHAOS bot inhibitor fell at 1420.8 and respawned at 1720.8 (no inhibitor timer left); result win.
    /// </summary>
    public static readonly string AllGameDataLate =
        (BeforeEvents + EarlyEvents + "," + LateEvents + AfterEvents).Replace("\"gameTime\": 840.2", "\"gameTime\": 1815.0");

    private const string BeforeEvents = """
    {
      "activePlayer": {
        "currentGold": 1234.5677490234375, "level": 9,
        "riotId": "Me#TAG0", "riotIdGameName": "Me", "riotIdTagLine": "TAG0", "summonerName": "Me#TAG0"
      },
      "allPlayers": [
        {
          "championName": "Ahri", "rawChampionName": "game_character_displayname_Ahri", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 9, "position": "MIDDLE", "team": "ORDER",
          "riotId": "Me#TAG0", "riotIdGameName": "Me", "riotIdTagLine": "TAG0", "summonerName": "Me#TAG0",
          "scores": { "assists": 0, "creepScore": 80, "deaths": 0, "kills": 3, "wardScore": 4.5 },
          "items": [
            { "itemID": 1056, "slot": 0, "count": 1 }, { "itemID": 3020, "slot": 1, "count": 1 },
            { "itemID": 2003, "slot": 2, "count": 1 }, { "itemID": 3340, "slot": 6, "count": 1 }
          ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Ignite", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerDot_DisplayName" }
          },
          "runes": { "keystone": { "id": 8112 }, "primaryRuneTree": { "id": 8100 }, "secondaryRuneTree": { "id": 8200 } }
        },
        {
          "championName": "Garen", "rawChampionName": "game_character_displayname_Garen", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 9, "position": "TOP", "team": "ORDER",
          "riotId": "Ally1#TAG1", "riotIdGameName": "Ally1", "riotIdTagLine": "TAG1", "summonerName": "Ally1#TAG1",
          "scores": { "assists": 0, "creepScore": 90, "deaths": 0, "kills": 0, "wardScore": 3.25 },
          "items": [ { "itemID": 1054, "slot": 0, "count": 1 }, { "itemID": 3047, "slot": 1, "count": 1 }, { "itemID": 3340, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Teleport", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerTeleport_DisplayName" }
          },
          "runes": { "keystone": { "id": 8010 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8400 } }
        },
        {
          "championName": "Lee Sin", "rawChampionName": "game_character_displayname_LeeSin", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 8, "position": "JUNGLE", "team": "ORDER",
          "riotId": "Ally2#TAG2", "riotIdGameName": "Ally2", "riotIdTagLine": "TAG2", "summonerName": "Ally2#TAG2",
          "scores": { "assists": 1, "creepScore": 70, "deaths": 0, "kills": 0, "wardScore": 8.0 },
          "items": [ { "itemID": 1101, "slot": 0, "count": 1 }, { "itemID": 3364, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Smite", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerSmite_DisplayName" },
            "summonerSpellTwo": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" }
          },
          "runes": { "keystone": { "id": 8010 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8300 } }
        },
        {
          "championName": "Thresh", "rawChampionName": "game_character_displayname_Thresh", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 7, "position": "UTILITY", "team": "ORDER",
          "riotId": "Ally3#TAG3", "riotIdGameName": "Ally3", "riotIdTagLine": "TAG3", "summonerName": "Ally3#TAG3",
          "scores": { "assists": 0, "creepScore": 10, "deaths": 1, "kills": 0, "wardScore": 15.75 },
          "items": [ { "itemID": 3865, "slot": 0, "count": 1 }, { "itemID": 3364, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Exhaust", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerExhaust_DisplayName" }
          },
          "runes": { "keystone": { "id": 8439 }, "primaryRuneTree": { "id": 8400 }, "secondaryRuneTree": { "id": 8300 } }
        },
        {
          "championName": "Jinx", "rawChampionName": "game_character_displayname_Jinx", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 8, "position": "BOTTOM", "team": "ORDER",
          "riotId": "Ally4#TAG4", "riotIdGameName": "Ally4", "riotIdTagLine": "TAG4", "summonerName": "Ally4#TAG4",
          "scores": { "assists": 0, "creepScore": 100, "deaths": 0, "kills": 0, "wardScore": 2.0 },
          "items": [ { "itemID": 1055, "slot": 0, "count": 1 }, { "itemID": 3006, "slot": 1, "count": 1 }, { "itemID": 3340, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Heal", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerHeal_DisplayName" }
          },
          "runes": { "keystone": { "id": 8008 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8200 } }
        },
        {
          "championName": "Darius", "rawChampionName": "game_character_displayname_Darius", "isBot": false, "isDead": true,
          "respawnTimer": 14.3, "level": 9, "position": "TOP", "team": "CHAOS",
          "riotId": "Enemy1#TAG5", "riotIdGameName": "Enemy1", "riotIdTagLine": "TAG5", "summonerName": "Enemy1#TAG5",
          "scores": { "assists": 0, "creepScore": 90, "deaths": 1, "kills": 0, "wardScore": 3.0 },
          "items": [ { "itemID": 1054, "slot": 0, "count": 1 }, { "itemID": 3047, "slot": 1, "count": 1 }, { "itemID": 3340, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Ghost", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerHaste_DisplayName" }
          },
          "runes": { "keystone": { "id": 8010 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8400 } }
        },
        {
          "championName": "Wukong", "rawChampionName": "game_character_displayname_MonkeyKing", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 8, "position": "JUNGLE", "team": "CHAOS",
          "riotId": "Enemy2#TAG6", "riotIdGameName": "Enemy2", "riotIdTagLine": "TAG6", "summonerName": "Enemy2#TAG6",
          "scores": { "assists": 0, "creepScore": 70, "deaths": 0, "kills": 0, "wardScore": 6.5 },
          "items": [ { "itemID": 1102, "slot": 0, "count": 1 }, { "itemID": 3364, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Smite", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerSmite_DisplayName" },
            "summonerSpellTwo": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" }
          },
          "runes": { "keystone": { "id": 8010 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8100 } }
        },
        {
          "championName": "Syndra", "rawChampionName": "game_character_displayname_Syndra", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 8, "position": "MIDDLE", "team": "CHAOS",
          "riotId": "Enemy3#TAG7", "riotIdGameName": "Enemy3", "riotIdTagLine": "TAG7", "summonerName": "Enemy3#TAG7",
          "scores": { "assists": 0, "creepScore": 80, "deaths": 1, "kills": 0, "wardScore": 2.5 },
          "items": [ { "itemID": 1056, "slot": 0, "count": 1 }, { "itemID": 3340, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Teleport", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerTeleport_DisplayName" }
          },
          "runes": { "keystone": { "id": 8229 }, "primaryRuneTree": { "id": 8200 }, "secondaryRuneTree": { "id": 8300 } }
        },
        {
          "championName": "Kai'Sa", "rawChampionName": "game_character_displayname_Kaisa", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 8, "position": "BOTTOM", "team": "CHAOS",
          "riotId": "Enemy4#TAG8", "riotIdGameName": "Enemy4", "riotIdTagLine": "TAG8", "summonerName": "Enemy4#TAG8",
          "scores": { "assists": 0, "creepScore": 100, "deaths": 1, "kills": 1, "wardScore": 2.0 },
          "items": [ { "itemID": 1055, "slot": 0, "count": 1 }, { "itemID": 2003, "slot": 1, "count": 2 }, { "itemID": 3340, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Heal", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerHeal_DisplayName" }
          },
          "runes": { "keystone": { "id": 8008 }, "primaryRuneTree": { "id": 8000 }, "secondaryRuneTree": { "id": 8100 } }
        },
        {
          "championName": "Nautilus", "rawChampionName": "game_character_displayname_Nautilus", "isBot": false, "isDead": false,
          "respawnTimer": 0.0, "level": 7, "position": "UTILITY", "team": "CHAOS",
          "riotId": "Enemy5#TAG9", "riotIdGameName": "Enemy5", "riotIdTagLine": "TAG9", "summonerName": "Enemy5#TAG9",
          "scores": { "assists": 1, "creepScore": 10, "deaths": 1, "kills": 0, "wardScore": 14.0 },
          "items": [ { "itemID": 3865, "slot": 0, "count": 1 }, { "itemID": 3364, "slot": 6, "count": 1 } ],
          "summonerSpells": {
            "summonerSpellOne": { "displayName": "Flash", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerFlash_DisplayName" },
            "summonerSpellTwo": { "displayName": "Ignite", "rawDisplayName": "GeneratedTip_SummonerSpell_SummonerDot_DisplayName" }
          },
          "runes": { "keystone": { "id": 8439 }, "primaryRuneTree": { "id": 8400 }, "secondaryRuneTree": { "id": 8000 } }
        }
      ],
      "gameData": { "gameMode": "CLASSIC", "gameTime": 840.2, "mapName": "Map11", "mapNumber": 11, "mapTerrain": "Default" },
      "events": {
        "Events": [
    """;

    /// <summary>The first 14 minutes, in the API's order (FirstBlood / FirstBrick / Multikill right after the event that caused them).</summary>
    private const string EarlyEvents = """
          { "EventID": 0, "EventName": "GameStart", "EventTime": 0.0325561985373497 },
          { "EventID": 1, "EventName": "MinionsSpawning", "EventTime": 65.0 },
          { "EventID": 2, "EventName": "ChampionKill", "EventTime": 190.5, "KillerName": "Me", "VictimName": "Enemy3", "Assisters": ["Ally2"] },
          { "EventID": 3, "EventName": "FirstBlood", "EventTime": 190.5, "Recipient": "Me" },
          { "EventID": 4, "EventName": "DragonKill", "EventTime": 372.4, "DragonType": "Fire", "Stolen": "False", "KillerName": "Ally2", "Assisters": ["Me"] },
          { "EventID": 5, "EventName": "HordeKill", "EventTime": 505.1, "Stolen": "False", "KillerName": "Ally2", "Assisters": [] },
          { "EventID": 6, "EventName": "HordeKill", "EventTime": 509.8, "Stolen": "False", "KillerName": "Ally2", "Assisters": [] },
          { "EventID": 7, "EventName": "HordeKill", "EventTime": 513.0, "Stolen": "False", "KillerName": "Enemy2", "Assisters": [] },
          { "EventID": 8, "EventName": "ChampionKill", "EventTime": 601.2, "KillerName": "Enemy4", "VictimName": "Ally3", "Assisters": ["Enemy5"] },
          { "EventID": 9, "EventName": "ChampionKill", "EventTime": 640.7, "KillerName": "Me", "VictimName": "Enemy4", "Assisters": [] },
          { "EventID": 10, "EventName": "ChampionKill", "EventTime": 644.0, "KillerName": "Me", "VictimName": "Enemy5", "Assisters": [] },
          { "EventID": 11, "EventName": "Multikill", "EventTime": 644.0, "KillerName": "Me", "KillStreak": 2 },
          { "EventID": 12, "EventName": "TurretKilled", "EventTime": 700.3, "TurretKilled": "Turret_T2_R_03_A", "KillerName": "Ally4", "Assisters": ["Ally3"] },
          { "EventID": 13, "EventName": "FirstBrick", "EventTime": 700.3, "KillerName": "Ally4" },
          { "EventID": 14, "EventName": "DragonKill", "EventTime": 812.6, "DragonType": "Water", "Stolen": "True", "KillerName": "Enemy2", "Assisters": [] },
          { "EventID": 15, "EventName": "ChampionKill", "EventTime": 828.5, "KillerName": "Turret_T1_L_03_A", "VictimName": "Enemy1", "Assisters": [] }
    """;

    /// <summary>
    /// The rest of the game (Riot's <c>liveclientdata_events.json</c> shapes): Herald, the third drake, an inhibitor down and
    /// back up, Baron, an ace and the end. Not valid on its own; see <see cref="AllGameDataLate"/>.
    /// </summary>
    public const string LateEvents = """
          { "EventID": 16, "EventName": "HeraldKill", "EventTime": 955.0, "Stolen": "False", "KillerName": "Ally2", "Assisters": ["Ally1"] },
          { "EventID": 17, "EventName": "DragonKill", "EventTime": 1150.3, "DragonType": "Hextech", "Stolen": "False", "KillerName": "Me", "Assisters": ["Ally2", "Ally4"] },
          { "EventID": 18, "EventName": "InhibKilled", "EventTime": 1420.8, "InhibKilled": "Barracks_T2_R1", "KillerName": "Ally4", "Assisters": ["Me", "Ally3"] },
          { "EventID": 19, "EventName": "BaronKill", "EventTime": 1505.2, "Stolen": "False", "KillerName": "Ally2", "Assisters": ["Me", "Ally1", "Ally3", "Ally4"] },
          { "EventID": 20, "EventName": "Ace", "EventTime": 1530.0, "Acer": "Me", "AcingTeam": "ORDER" },
          { "EventID": 21, "EventName": "InhibRespawned", "EventTime": 1720.8, "InhibRespawned": "Barracks_T2_R1" },
          { "EventID": 22, "EventName": "GameEnd", "EventTime": 1810.4, "Result": "Win" }
    """;

    /// <summary>
    /// <c>GET /lol-game-data/assets/v1/champion-summary.json</c> (LCU), recorded read-only from a live client (Oct 23 2026),
    /// trimmed to the sample's champions and the fields we read. Special: <c>alias</c> is the internal name the Live Client
    /// Data API's <c>rawChampionName</c> carries (MonkeyKing / Wukong, Kaisa / Kai'Sa, LeeSin / Lee Sin), and the file also
    /// lists mode variants that reuse a display name (<c>Jade_Annie</c>, 60001, "Annie"), sorted by id after the regular ones.
    /// </summary>
    public const string ChampionSummary = """
    [
      { "id": -1, "name": "None", "alias": "None" },
      { "id": 1, "name": "Annie", "alias": "Annie" },
      { "id": 62, "name": "Wukong", "alias": "MonkeyKing" },
      { "id": 64, "name": "Lee Sin", "alias": "LeeSin" },
      { "id": 86, "name": "Garen", "alias": "Garen" },
      { "id": 103, "name": "Ahri", "alias": "Ahri" },
      { "id": 111, "name": "Nautilus", "alias": "Nautilus" },
      { "id": 122, "name": "Darius", "alias": "Darius" },
      { "id": 134, "name": "Syndra", "alias": "Syndra" },
      { "id": 145, "name": "Kai'Sa", "alias": "Kaisa" },
      { "id": 222, "name": "Jinx", "alias": "Jinx" },
      { "id": 412, "name": "Thresh", "alias": "Thresh" },
      { "id": 60001, "name": "Annie", "alias": "Jade_Annie" }
    ]
    """;

    private const string AfterEvents = """
        ]
      }
    }
    """;
}
