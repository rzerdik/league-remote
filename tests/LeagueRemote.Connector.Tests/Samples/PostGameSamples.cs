namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Post-game samples. <b>Composed, not recorded</b> (no game had just ended, Oct 2026): the end-of-game endpoints answer 404
/// outside the post-game screen. Field names come from the client's <c>/help</c> types (<c>LolEndOfGameEndOfGameStats</c>,
/// <c>LolEndOfGameEndOfGameTeam</c> / <c>Player</c>, <c>LolRankedLcuLeagueNotification</c>,
/// <c>LolEndOfGameChampionMasteryUpdate</c>) and the stat keys from what rcp-fe-lol-postgame (patch 16.21) reads
/// (<c>CHAMPIONS_KILLED</c>, <c>MINIONS_KILLED</c>, …); the rune keys (<c>PERK0</c>, …) are the older block's. Replace them
/// with anonymised recordings from a real post-game screen. Placeholders: <c>me-puuid</c>, <c>Ally1</c>, <c>Enemy1</c>…
/// </summary>
internal static class PostGameSamples
{
    /// <summary>
    /// <c>GET /lol-end-of-game/v1/eog-stats-block</c>: a ranked Solo/Duo win on blue side, 25:12. The red team is listed
    /// first (the mapper puts yours first); enemies carry names and puuids the mapper must drop.
    /// </summary>
    public const string RankedWin = """
    {
      "gameId": 4000000020, "gameLength": 1512, "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "queueType": "RANKED_SOLO_5x5",
      "ranked": true, "invalid": false, "gameEndedInEarlySurrender": false, "multiUserChatId": "post-game-room",
      "localPlayer": { "puuid": "me-puuid", "summonerId": 1, "riotIdGameName": "Me", "riotIdTagLine": "EUW" },
      "teams": [
        { "teamId": 200, "isPlayerTeam": false, "isWinningTeam": false, "name": "", "tag": "",
          "stats": { "CHAMPIONS_KILLED": 14, "GOLD_EARNED": 41000 },
          "players": [
            { "puuid": "enemy1-puuid", "summonerId": 11, "riotIdGameName": "Enemy1", "riotIdTagLine": "E1", "summonerName": "Enemy1",
              "championId": 122, "teamId": 200, "isLocalPlayer": false, "botPlayer": false, "level": 15, "spell1Id": 4, "spell2Id": 12,
              "items": [3071, 3047, 6333, 0, 0, 0, 3364], "profileIconId": 29,
              "stats": { "CHAMPIONS_KILLED": 6, "NUM_DEATHS": 7, "ASSISTS": 3, "MINIONS_KILLED": 160, "NEUTRAL_MINIONS_KILLED": 4,
                         "GOLD_EARNED": 10200, "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 17000, "VISION_SCORE": 14, "LEVEL": 15 } },
            { "puuid": "enemy2-puuid", "summonerId": 12, "riotIdGameName": "Enemy2", "riotIdTagLine": "E2", "summonerName": "Enemy2",
              "championId": 64, "teamId": 200, "isLocalPlayer": false, "level": 13, "spell1Id": 4, "spell2Id": 11,
              "items": [6692, 3111], "stats": { "CHAMPIONS_KILLED": 8, "NUM_DEATHS": 6, "ASSISTS": 9, "MINIONS_KILLED": 20,
                         "NEUTRAL_MINIONS_KILLED": 140, "GOLD_EARNED": 30800, "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 12000, "VISION_SCORE": 30 } }
          ] },
        { "teamId": 100, "isPlayerTeam": true, "isWinningTeam": true, "name": "", "tag": "",
          "stats": { "CHAMPIONS_KILLED": 25, "GOLD_EARNED": 52000 },
          "players": [
            { "puuid": "ally1-puuid", "summonerId": 2, "riotIdGameName": "Ally1", "riotIdTagLine": "A1", "summonerName": "Ally1",
              "championId": 86, "teamId": 100, "isLocalPlayer": false, "level": 16, "spell1Id": 4, "spell2Id": 14,
              "items": [3078, 3047, 0, 0, 0, 0, 3340],
              "stats": { "CHAMPIONS_KILLED": 7, "NUM_DEATHS": 5, "ASSISTS": 6, "MINIONS_KILLED": 180, "NEUTRAL_MINIONS_KILLED": 8,
                         "GOLD_EARNED": 22000, "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 21000, "VISION_SCORE": 18, "LEVEL": 16 } },
            { "puuid": "me-puuid", "summonerId": 1, "riotIdGameName": "Me", "riotIdTagLine": "EUW", "summonerName": "Me",
              "championId": 103, "teamId": 100, "isLocalPlayer": true, "level": 17, "spell1Id": 4, "spell2Id": 14,
              "items": [6655, 3020, 4645, 3089, 3157, 0, 3363],
              "stats": { "CHAMPIONS_KILLED": 18, "NUM_DEATHS": 3, "ASSISTS": 9, "MINIONS_KILLED": 201, "NEUTRAL_MINIONS_KILLED": 12,
                         "GOLD_EARNED": 30000, "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 38200, "VISION_SCORE": 21, "LEVEL": 17,
                         "PERK0": 8112, "PERK_PRIMARY_STYLE": 8100, "PERK_SUB_STYLE": 8200 } }
          ] }
      ]
    }
    """;

    /// <summary><c>GET /lol-ranked/v1/current-lp-change-notification</c> after <see cref="RankedWin"/>: +21 LP, promoted to Gold IV.</summary>
    public const string LpPromoted = """
    { "id": 7, "gameId": 4000000020, "queueType": "RANKED_SOLO_5x5", "notifyReason": "LEAGUE_PROMOTED", "changeReason": "",
      "tier": "GOLD", "division": "IV", "leaguePoints": 0, "leaguePointsDelta": 21, "provisionalGamesRemaining": 0,
      "miniseriesProgress": "", "wasAfkOrLeaver": false, "afkLpPenaltyAmount": 0 }
    """;

    /// <summary><c>GET /lol-end-of-game/v1/champion-mastery-updates</c> after <see cref="RankedWin"/> (Ahri, a level up).</summary>
    public const string MasteryUpdate = """
    { "id": "m1", "gameId": 4000000020, "puuid": "me-puuid", "championId": 103, "hasLeveledUp": true, "level": 12,
      "pointsBeforeGame": 110000, "pointsGained": 1240, "pointsUntilNextLevelAfterGame": 9760, "grade": "S",
      "memberGrades": [ { "puuid": "ally1-puuid", "championId": 86, "grade": "A" } ], "levelUpList": [] }
    """;

    /// <summary><c>GET /lol-gameflow/v1/session</c> on the post-game screen, trimmed to <c>gameData.queue</c> (+ a player list we never read).</summary>
    public const string Session = """
    { "phase": "EndOfGame",
      "gameData": { "gameId": 4000000020, "queue": { "id": 420, "mapId": 11, "gameMode": "CLASSIC", "isRanked": true },
                    "teamOne": [ { "puuid": "ally1-puuid", "summonerName": "Ally1" } ], "teamTwo": [ { "puuid": "enemy1-puuid", "summonerName": "Enemy1" } ] } }
    """;

    /// <summary>
    /// <c>GET /lol-gameflow/v1/gameflow-metadata/player-status</c>, shape recorded read-only from a live client in a lobby
    /// (Oct 2026), values changed to the game above.
    /// </summary>
    public const string PlayerStatus = """
    { "canInviteOthersAtEog": true,
      "currentLobbyStatus": null,
      "lastQueuedLobbyStatus": { "allowedPlayAgain": true, "isCustom": false, "isLeader": true, "isPracticeTool": false,
                                 "isSpectator": false, "lobbyId": "lobby-id", "queueId": 420 } }
    """;

    /// <summary>
    /// An Arena (CHERRY) block with three duos in one LCU team list each (how the block groups Arena players isn't known);
    /// <c>PLAYER_SUBTEAM</c> / <c>PLAYER_SUBTEAM_PLACEMENT</c> as rcp-fe-lol-postgame sets them for Arena.
    /// </summary>
    public const string Arena = """
    {
      "gameId": 4000000030, "gameLength": 1100, "gameMode": "CHERRY", "ranked": false,
      "localPlayer": { "puuid": "me-puuid" },
      "teams": [
        { "teamId": 100, "isPlayerTeam": true, "isWinningTeam": false, "players": [
          { "puuid": "me-puuid", "riotIdGameName": "Me", "riotIdTagLine": "EUW", "championId": 103, "isLocalPlayer": true,
            "stats": { "CHAMPIONS_KILLED": 9, "PLAYER_SUBTEAM": 3, "PLAYER_SUBTEAM_PLACEMENT": 2 } },
          { "puuid": "enemy1-puuid", "riotIdGameName": "Enemy1", "riotIdTagLine": "E1", "championId": 122,
            "stats": { "CHAMPIONS_KILLED": 11, "PLAYER_SUBTEAM": 1, "PLAYER_SUBTEAM_PLACEMENT": 1 } },
          { "puuid": "duo-puuid", "riotIdGameName": "Duo", "riotIdTagLine": "D1", "championId": 86,
            "stats": { "CHAMPIONS_KILLED": 4, "PLAYER_SUBTEAM": 3, "PLAYER_SUBTEAM_PLACEMENT": 2 } }
        ] },
        { "teamId": 200, "isPlayerTeam": false, "isWinningTeam": false, "players": [
          { "puuid": "enemy2-puuid", "riotIdGameName": "Enemy2", "riotIdTagLine": "E2", "championId": 64,
            "stats": { "CHAMPIONS_KILLED": 7, "PLAYER_SUBTEAM": 1, "PLAYER_SUBTEAM_PLACEMENT": 1 } },
          { "puuid": "enemy3-puuid", "riotIdGameName": "Enemy3", "riotIdTagLine": "E3", "championId": 1,
            "stats": { "CHAMPIONS_KILLED": 2, "PLAYER_SUBTEAM": 7, "PLAYER_SUBTEAM_PLACEMENT": 8 } }
        ] }
      ]
    }
    """;
}
