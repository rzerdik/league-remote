namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Profile JSON as a live client sent it (Oct 23 2026, patch 16.21), read-only GETs: the local player's own profile
/// (<see cref="Own"/>) and a friend's (<see cref="Player"/>). Only the shape is the client's: names, puuids, account /
/// summoner ids and game ids are replaced, some numbers changed, and everything is trimmed to the fields the mapper reads.
/// </summary>
internal static class ProfileSamples
{
    /// <summary>
    /// The local player, <c>me-puuid</c> ("Me#EUNE"). In the full game the own team is Ally1–Ally4, the other team
    /// Enemy1–Enemy5.
    /// </summary>
    public static class Own
    {
        public const string MyPuuid = "me-puuid";

        /// <summary>The newest game in <see cref="Matches"/>, also recorded in full as <see cref="Game"/>.</summary>
        public const long RankedLossGameId = 4000000004;

        public const string MatchesPath = "/lol-match-history/v1/products/lol/current-summoner/matches";
        public const string GamePath = "/lol-match-history/v1/games/4000000004";
        public const string SummonerPath = "/lol-summoner/v1/current-summoner";
        public const string RankedPath = "/lol-ranked/v1/current-ranked-stats";
        public const string MasteryPath = "/lol-champion-mastery/v1/local-player/champion-mastery";
        public const string MasteryScorePath = "/lol-champion-mastery/v1/local-player/champion-mastery-score";

        /// <summary>The GETs of one full profile read, in the order the connector sends them.</summary>
        public static readonly IReadOnlyList<string> ProfileReads =
            [$"GET {SummonerPath}", $"GET {RankedPath}", $"GET {MasteryPath}", $"GET {MasteryScorePath}", $"GET {MatchesPath}"];

        /// <summary>A fake LCU serving every profile sample (and the full game of <see cref="RankedLossGameId"/>).</summary>
        public static FakeLcu Handler()
        {
            var handler = new FakeLcu();
            handler.Routes[SummonerPath] = CurrentSummoner;
            handler.Routes[RankedPath] = RankedStats;
            handler.Routes[MasteryPath] = Mastery;
            handler.Routes[MasteryScorePath] = MasteryScore;
            handler.Routes[MatchesPath] = Matches;
            handler.Routes[GamePath] = Game;
            return handler;
        }

        /// <summary>
        /// /lol-summoner/v1/current-summoner (trimmed). <c>xpUntilNextLevel</c> is the level's total, not what's left:
        /// the client's <c>percentCompleteForNextLevel</c> (18) is since / until.
        /// </summary>
        public const string CurrentSummoner = """
        {
          "accountId": 1001, "displayName": "Me", "gameName": "Me", "internalName": "Me", "percentCompleteForNextLevel": 18,
          "privacy": "PUBLIC", "profileIconId": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerLevel": 123,
          "tagLine": "EUNE", "unnamed": false, "xpSinceLastLevel": 668, "xpUntilNextLevel": 3648
        }
        """;

        /// <summary>
        /// /lol-ranked/v1/current-ranked-stats (trimmed to <c>queues</c>, in the client's order). Unranked queues have tier ""
        /// and division "NA". TFT queues and the unranked <c>JADE_RANKED_SOLO_5x5</c> / <c>RANKED_PREMADE_5x5</c> entries are
        /// what the client really lists; <c>miniSeriesProgress</c> is always "" (no series in patch 16.21).
        /// </summary>
        public const string RankedStats = """
        {
          "highestCurrentSeasonReachedTierSR": "EMERALD", "highestPreviousSeasonEndTier": "EMERALD", "highestPreviousSeasonEndDivision": "IV",
          "queues": [
            {"queueType": "RANKED_FLEX_SR", "tier": "PLATINUM", "division": "I", "leaguePoints": 81, "wins": 12, "losses": 9, "isProvisional": false, "provisionalGamesRemaining": 0, "provisionalGameThreshold": 5, "miniSeriesProgress": "", "highestTier": "PLATINUM", "highestDivision": "I", "previousSeasonEndTier": "PLATINUM", "previousSeasonEndDivision": "I", "ratedTier": "NONE"},
            {"queueType": "RANKED_SOLO_5x5", "tier": "PLATINUM", "division": "IV", "leaguePoints": 52, "wins": 230, "losses": 236, "isProvisional": false, "provisionalGamesRemaining": 0, "provisionalGameThreshold": 5, "miniSeriesProgress": "", "highestTier": "EMERALD", "highestDivision": "III", "previousSeasonEndTier": "EMERALD", "previousSeasonEndDivision": "IV", "ratedTier": "NONE"},
            {"queueType": "JADE_RANKED_SOLO_5x5", "tier": "", "division": "NA", "leaguePoints": 0, "wins": 0, "losses": 0, "isProvisional": false, "provisionalGamesRemaining": 0, "provisionalGameThreshold": 0, "miniSeriesProgress": "", "highestTier": "", "highestDivision": "NA", "previousSeasonEndTier": "", "previousSeasonEndDivision": "NA", "ratedTier": "NONE"},
            {"queueType": "RANKED_TFT", "tier": "", "division": "NA", "leaguePoints": 0, "wins": 0, "losses": 0, "isProvisional": true, "provisionalGamesRemaining": 5, "provisionalGameThreshold": 5, "miniSeriesProgress": "", "highestTier": "", "highestDivision": "NA", "previousSeasonEndTier": "", "previousSeasonEndDivision": "NA", "ratedTier": "NONE"},
            {"queueType": "RANKED_TFT_DOUBLE_UP", "tier": "", "division": "NA", "leaguePoints": 0, "wins": 0, "losses": 0, "isProvisional": true, "provisionalGamesRemaining": 5, "provisionalGameThreshold": 5, "miniSeriesProgress": "", "highestTier": "", "highestDivision": "NA", "previousSeasonEndTier": "", "previousSeasonEndDivision": "NA", "ratedTier": "NONE"},
            {"queueType": "RANKED_PREMADE_5x5", "tier": "", "division": "NA", "leaguePoints": 0, "wins": 0, "losses": 0, "isProvisional": true, "provisionalGamesRemaining": 5, "provisionalGameThreshold": 5, "miniSeriesProgress": "", "highestTier": "", "highestDivision": "NA", "previousSeasonEndTier": "", "previousSeasonEndDivision": "NA", "ratedTier": "NONE"}
          ]
        }
        """;

        /// <summary>
        /// /lol-champion-mastery/v1/local-player/champion-mastery (trimmed: 12 of the list's entries, points changed). The client
        /// sends the whole list (every champion with mastery), sorted by points; the top-N endpoint is a POST
        /// (<c>PostLolChampionMasteryV1ByPuuidChampionMasteryTop</c>), so the mapper sorts and caps itself.
        /// </summary>
        public const string Mastery = """
        [
          {"championId": 51, "championLevel": 57, "championPoints": 590000, "championPointsSinceLastLevel": 2107, "championPointsUntilNextLevel": 8893, "highestGrade": "S+", "lastPlayTime": 1792621129000, "markRequiredForNextLevel": 2, "tokensEarned": 15, "puuid": "me-puuid"},
          {"championId": 67, "championLevel": 49, "championPoints": 507000, "championPointsSinceLastLevel": 2522, "championPointsUntilNextLevel": 8478, "highestGrade": "", "lastPlayTime": 1787443812000, "markRequiredForNextLevel": 2, "tokensEarned": 1, "puuid": "me-puuid"},
          {"championId": 18, "championLevel": 45, "championPoints": 465000, "championPointsSinceLastLevel": 1000, "championPointsUntilNextLevel": 10000, "highestGrade": "S+", "lastPlayTime": 1792678355528, "markRequiredForNextLevel": 2, "tokensEarned": 16, "puuid": "me-puuid"},
          {"championId": 236, "championLevel": 30, "championPoints": 320000, "championPointsSinceLastLevel": 500, "championPointsUntilNextLevel": 10500, "highestGrade": "", "lastPlayTime": 1780000000000, "markRequiredForNextLevel": 2, "tokensEarned": 1, "puuid": "me-puuid"},
          {"championId": 222, "championLevel": 21, "championPoints": 198000, "championPointsSinceLastLevel": 4000, "championPointsUntilNextLevel": 7000, "highestGrade": "A", "lastPlayTime": 1785000000000, "markRequiredForNextLevel": 2, "tokensEarned": 11, "puuid": "me-puuid"},
          {"championId": 96, "championLevel": 19, "championPoints": 175000, "championPointsSinceLastLevel": 3000, "championPointsUntilNextLevel": 8000, "highestGrade": "", "lastPlayTime": 1770000000000, "markRequiredForNextLevel": 2, "tokensEarned": 0, "puuid": "me-puuid"},
          {"championId": 81, "championLevel": 13, "championPoints": 117000, "championPointsSinceLastLevel": 6000, "championPointsUntilNextLevel": 5000, "highestGrade": "", "lastPlayTime": 1771000000000, "markRequiredForNextLevel": 2, "tokensEarned": 8, "puuid": "me-puuid"},
          {"championId": 22, "championLevel": 10, "championPoints": 83000, "championPointsSinceLastLevel": 8000, "championPointsUntilNextLevel": 3000, "highestGrade": "", "lastPlayTime": 1772000000000, "markRequiredForNextLevel": 2, "tokensEarned": 16, "puuid": "me-puuid"},
          {"championId": 145, "championLevel": 10, "championPoints": 79500, "championPointsSinceLastLevel": 4500, "championPointsUntilNextLevel": 6500, "highestGrade": "", "lastPlayTime": 1773000000000, "markRequiredForNextLevel": 2, "tokensEarned": 0, "puuid": "me-puuid"},
          {"championId": 804, "championLevel": 8, "championPoints": 63000, "championPointsSinceLastLevel": 5000, "championPointsUntilNextLevel": 6000, "highestGrade": "", "lastPlayTime": 1774000000000, "markRequiredForNextLevel": 1, "tokensEarned": 12, "puuid": "me-puuid"},
          {"championId": 157, "championLevel": 8, "championPoints": 57000, "championPointsSinceLastLevel": 2000, "championPointsUntilNextLevel": 9000, "highestGrade": "", "lastPlayTime": 1775000000000, "markRequiredForNextLevel": 1, "tokensEarned": 0, "puuid": "me-puuid"},
          {"championId": 5, "championLevel": 7, "championPoints": 47000, "championPointsSinceLastLevel": 3000, "championPointsUntilNextLevel": 8000, "highestGrade": "", "lastPlayTime": 1776000000000, "markRequiredForNextLevel": 1, "tokensEarned": 1, "puuid": "me-puuid"}
        ]
        """;

        /// <summary>
        /// /lol-champion-mastery/v1/local-player/champion-mastery-score: a bare number (the sum of all champion levels; changed).
        /// </summary>
        public const string MasteryScore = "540";

        /// <summary>
        /// /lol-match-history/v1/products/lol/current-summoner/matches (4 of the client's 21 games, newest first, trimmed). Each
        /// game carries only the local player in <c>participants</c> / <c>participantIdentities</c>, but both teams' objective
        /// counts and bans. The client ignored <c>begIndex</c> / <c>endIndex</c> (always games 0–20). Recorded: a ranked loss on
        /// blue side, a 31-second Practice Tool game (one team, "Win"), a custom game that aborted
        /// (<c>endOfGameResult</c> "Abort_TooFewPlayers", both teams "Fail") and a ranked win on red side. A remake sets
        /// <c>stats.gameEndedInEarlySurrender</c> (none recorded; vary with <c>.Replace</c>).
        /// </summary>
        public const string Matches = """
        {
          "accountId": 1001, "platformId": "EUN1",
          "games": {
            "gameCount": 4, "gameIndexBegin": 0, "gameIndexEnd": 3,
            "games": [
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1792699895462, "gameDuration": 1644, "gameId": 4000000004,
                "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "mapId": 11, "queueId": 420,
                "participantIdentities": [
                  {"participantId": 4, "player": {"accountId": 1001, "gameName": "Me", "tagLine": "EUNE", "profileIcon": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 51, "participantId": 4, "spell1Id": 4, "spell2Id": 21, "teamId": 100, "timeline": {"lane": "BOTTOM", "role": "CARRY"},
                    "stats": {"assists": 4, "champLevel": 13, "deaths": 8, "gameEndedInEarlySurrender": false, "goldEarned": 12103, "item0": 1086, "item1": 6676, "item2": 3031, "item3": 3033, "item4": 0, "item5": 0, "item6": 3363, "kills": 10, "neutralMinionsKilled": 4, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 27730, "totalMinionsKilled": 158, "visionScore": 5, "win": false} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 0, "hordeKills": 1, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 4, "bans": [{"championId": 35, "pickTurn": 1},{"championId": 360, "pickTurn": 2},{"championId": 122, "pickTurn": 3},{"championId": 38, "pickTurn": 4},{"championId": 8, "pickTurn": 5}]},
                  {"teamId": 200, "win": "Win", "baronKills": 1, "dragonKills": 3, "hordeKills": 2, "inhibitorKills": 1, "riftHeraldKills": 1, "towerKills": 9, "bans": [{"championId": 55, "pickTurn": 6},{"championId": 39, "pickTurn": 7},{"championId": 75, "pickTurn": 8},{"championId": 200, "pickTurn": 9},{"championId": 800, "pickTurn": 10}]}
                ]
              },
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1792698196237, "gameDuration": 31, "gameId": 4000000003,
                "gameMode": "PRACTICETOOL", "gameType": "CUSTOM_GAME", "mapId": 11, "queueId": 3140,
                "participantIdentities": [
                  {"participantId": 1, "player": {"accountId": 1001, "gameName": "Me", "tagLine": "EUNE", "profileIcon": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 51, "participantId": 1, "spell1Id": 4, "spell2Id": 21, "teamId": 100, "timeline": {"lane": "NONE", "role": "NONE"},
                    "stats": {"assists": 0, "champLevel": 1, "deaths": 0, "gameEndedInEarlySurrender": false, "goldEarned": 550, "item0": 0, "item1": 0, "item2": 0, "item3": 0, "item4": 0, "item5": 0, "item6": 0, "kills": 0, "neutralMinionsKilled": 0, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 0, "totalMinionsKilled": 0, "visionScore": 0, "win": true} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Win", "baronKills": 0, "dragonKills": 0, "hordeKills": 0, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 0, "bans": []}
                ]
              },
              {
                "endOfGameResult": "Abort_TooFewPlayers", "gameCreation": 1792697736677, "gameDuration": 52, "gameId": 4000000002,
                "gameMode": "CLASSIC", "gameType": "CUSTOM_GAME", "mapId": 11, "queueId": 3110,
                "participantIdentities": [
                  {"participantId": 1, "player": {"accountId": 1001, "gameName": "Me", "tagLine": "EUNE", "profileIcon": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 1, "participantId": 1, "spell1Id": 4, "spell2Id": 21, "teamId": 100, "timeline": {"lane": "NONE", "role": "NONE"},
                    "stats": {"assists": 0, "champLevel": 1, "deaths": 0, "gameEndedInEarlySurrender": false, "goldEarned": 500, "item0": 1056, "item1": 2003, "item2": 0, "item3": 0, "item4": 0, "item5": 0, "item6": 3340, "kills": 0, "neutralMinionsKilled": 0, "perk0": 8112, "perkPrimaryStyle": 8100, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 0, "totalMinionsKilled": 0, "visionScore": 0, "win": false} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 0, "hordeKills": 0, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 0, "bans": [{"championId": 268, "pickTurn": 1}]},
                  {"teamId": 200, "win": "Fail", "baronKills": 0, "dragonKills": 0, "hordeKills": 0, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 0, "bans": []}
                ]
              },
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1792678355528, "gameDuration": 1807, "gameId": 4000000001,
                "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "mapId": 11, "queueId": 420,
                "participantIdentities": [
                  {"participantId": 9, "player": {"accountId": 1001, "gameName": "Me", "tagLine": "EUNE", "profileIcon": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 18, "participantId": 9, "spell1Id": 4, "spell2Id": 21, "teamId": 200, "timeline": {"lane": "BOTTOM", "role": "CARRY"},
                    "stats": {"assists": 2, "champLevel": 16, "deaths": 3, "gameEndedInEarlySurrender": false, "goldEarned": 15624, "item0": 1086, "item1": 6676, "item2": 3031, "item3": 6675, "item4": 3035, "item5": 1018, "item6": 3363, "kills": 12, "neutralMinionsKilled": 4, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8300, "subteamPlacement": 0, "totalDamageDealtToChampions": 28433, "totalMinionsKilled": 196, "visionScore": 18, "win": true} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 1, "hordeKills": 1, "inhibitorKills": 0, "riftHeraldKills": 1, "towerKills": 3, "bans": [{"championId": 11, "pickTurn": 1},{"championId": 122, "pickTurn": 2},{"championId": 89, "pickTurn": 3},{"championId": 238, "pickTurn": 4},{"championId": 202, "pickTurn": 5}]},
                  {"teamId": 200, "win": "Win", "baronKills": 1, "dragonKills": 3, "hordeKills": 2, "inhibitorKills": 3, "riftHeraldKills": 0, "towerKills": 12, "bans": [{"championId": 17, "pickTurn": 6},{"championId": 25, "pickTurn": 7},{"championId": 234, "pickTurn": 8},{"championId": 360, "pickTurn": 9},{"championId": 245, "pickTurn": 10}]}
                ]
              }
            ]
          }
        }
        """;

        /// <summary>
        /// /lol-match-history/v1/games/4000000004: the ranked loss in full — 10 participants, all with Riot IDs and puuids in
        /// <c>participantIdentities</c> (the mapper must drop the enemies'). Participants are in role order (top, jungle, mid,
        /// bottom, support per team); <c>timeline.lane</c> is League's guess and calls the top laner (participant 1) JUNGLE.
        /// </summary>
        public const string Game = """
        {
          "endOfGameResult": "GameComplete", "gameCreation": 1792699895462, "gameDuration": 1644, "gameId": 4000000004,
          "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "mapId": 11, "queueId": 420,
          "participantIdentities": [
            {"participantId": 1, "player": {"accountId": 1002, "gameName": "Ally1", "tagLine": "TAG2", "profileIcon": 29, "puuid": "ally1-puuid", "summonerId": 1002, "summonerName": ""}},
            {"participantId": 2, "player": {"accountId": 1003, "gameName": "Ally2", "tagLine": "TAG3", "profileIcon": 29, "puuid": "ally2-puuid", "summonerId": 1003, "summonerName": ""}},
            {"participantId": 3, "player": {"accountId": 1004, "gameName": "Ally3", "tagLine": "TAG4", "profileIcon": 29, "puuid": "ally3-puuid", "summonerId": 1004, "summonerName": ""}},
            {"participantId": 4, "player": {"accountId": 1001, "gameName": "Me", "tagLine": "EUNE", "profileIcon": 29, "puuid": "me-puuid", "summonerId": 1001, "summonerName": ""}},
            {"participantId": 5, "player": {"accountId": 1005, "gameName": "Ally4", "tagLine": "TAG5", "profileIcon": 29, "puuid": "ally4-puuid", "summonerId": 1005, "summonerName": ""}},
            {"participantId": 6, "player": {"accountId": 2001, "gameName": "Enemy1", "tagLine": "TAG1", "profileIcon": 29, "puuid": "enemy1-puuid", "summonerId": 2001, "summonerName": ""}},
            {"participantId": 7, "player": {"accountId": 2002, "gameName": "Enemy2", "tagLine": "TAG2", "profileIcon": 29, "puuid": "enemy2-puuid", "summonerId": 2002, "summonerName": ""}},
            {"participantId": 8, "player": {"accountId": 2003, "gameName": "Enemy3", "tagLine": "TAG3", "profileIcon": 29, "puuid": "enemy3-puuid", "summonerId": 2003, "summonerName": ""}},
            {"participantId": 9, "player": {"accountId": 2004, "gameName": "Enemy4", "tagLine": "TAG4", "profileIcon": 29, "puuid": "enemy4-puuid", "summonerId": 2004, "summonerName": ""}},
            {"participantId": 10, "player": {"accountId": 2005, "gameName": "Enemy5", "tagLine": "TAG5", "profileIcon": 29, "puuid": "enemy5-puuid", "summonerId": 2005, "summonerName": ""}}
          ],
          "participants": [
            {"championId": 82, "participantId": 1, "spell1Id": 14, "spell2Id": 4, "teamId": 100, "timeline": {"lane": "JUNGLE", "role": "NONE"},
              "stats": {"assists": 1, "champLevel": 15, "deaths": 11, "gameEndedInEarlySurrender": false, "goldEarned": 9578, "item0": 3116, "item1": 3111, "item2": 3075, "item3": 2508, "item4": 3147, "item5": 0, "item6": 3363, "kills": 2, "neutralMinionsKilled": 16, "perk0": 8010, "perkPrimaryStyle": 8000, "perkSubStyle": 8400, "subteamPlacement": 0, "totalDamageDealtToChampions": 15556, "totalMinionsKilled": 155, "visionScore": 29, "win": false} },
            {"championId": 234, "participantId": 2, "spell1Id": 4, "spell2Id": 11, "teamId": 100, "timeline": {"lane": "JUNGLE", "role": "NONE"},
              "stats": {"assists": 7, "champLevel": 14, "deaths": 6, "gameEndedInEarlySurrender": false, "goldEarned": 10494, "item0": 3111, "item1": 6672, "item2": 6676, "item3": 3123, "item4": 1037, "item5": 6670, "item6": 3340, "kills": 6, "neutralMinionsKilled": 114, "perk0": 8010, "perkPrimaryStyle": 8000, "perkSubStyle": 8300, "subteamPlacement": 0, "totalDamageDealtToChampions": 16981, "totalMinionsKilled": 34, "visionScore": 21, "win": false} },
            {"championId": 103, "participantId": 3, "spell1Id": 14, "spell2Id": 4, "teamId": 100, "timeline": {"lane": "MIDDLE", "role": "SOLO"},
              "stats": {"assists": 6, "champLevel": 15, "deaths": 7, "gameEndedInEarlySurrender": false, "goldEarned": 12174, "item0": 3157, "item1": 1052, "item2": 3175, "item3": 4645, "item4": 2503, "item5": 3916, "item6": 3340, "kills": 9, "neutralMinionsKilled": 0, "perk0": 8992, "perkPrimaryStyle": 8200, "perkSubStyle": 8100, "subteamPlacement": 0, "totalDamageDealtToChampions": 29071, "totalMinionsKilled": 175, "visionScore": 16, "win": false} },
            {"championId": 51, "participantId": 4, "spell1Id": 4, "spell2Id": 21, "teamId": 100, "timeline": {"lane": "BOTTOM", "role": "CARRY"},
              "stats": {"assists": 4, "champLevel": 13, "deaths": 8, "gameEndedInEarlySurrender": false, "goldEarned": 12103, "item0": 1086, "item1": 6676, "item2": 3031, "item3": 3033, "item4": 0, "item5": 0, "item6": 3363, "kills": 10, "neutralMinionsKilled": 4, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 27730, "totalMinionsKilled": 158, "visionScore": 5, "win": false} },
            {"championId": 154, "participantId": 5, "spell1Id": 3, "spell2Id": 4, "teamId": 100, "timeline": {"lane": "BOTTOM", "role": "SUPPORT"},
              "stats": {"assists": 10, "champLevel": 12, "deaths": 6, "gameEndedInEarlySurrender": false, "goldEarned": 7692, "item0": 3876, "item1": 3068, "item2": 3111, "item3": 0, "item4": 2525, "item5": 0, "item6": 3364, "kills": 2, "neutralMinionsKilled": 0, "perk0": 8439, "perkPrimaryStyle": 8400, "perkSubStyle": 8000, "subteamPlacement": 0, "totalDamageDealtToChampions": 11570, "totalMinionsKilled": 26, "visionScore": 60, "win": false} },
            {"championId": 48, "participantId": 6, "spell1Id": 14, "spell2Id": 4, "teamId": 200, "timeline": {"lane": "JUNGLE", "role": "NONE"},
              "stats": {"assists": 8, "champLevel": 18, "deaths": 4, "gameEndedInEarlySurrender": false, "goldEarned": 17424, "item0": 3074, "item1": 3111, "item2": 3078, "item3": 3065, "item4": 6333, "item5": 2021, "item6": 3340, "kills": 17, "neutralMinionsKilled": 8, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8400, "subteamPlacement": 0, "totalDamageDealtToChampions": 36024, "totalMinionsKilled": 201, "visionScore": 18, "win": true} },
            {"championId": 76, "participantId": 7, "spell1Id": 4, "spell2Id": 11, "teamId": 200, "timeline": {"lane": "JUNGLE", "role": "NONE"},
              "stats": {"assists": 9, "champLevel": 15, "deaths": 5, "gameEndedInEarlySurrender": false, "goldEarned": 11263, "item0": 3135, "item1": 3100, "item2": 3020, "item3": 3145, "item4": 1058, "item5": 0, "item6": 3364, "kills": 4, "neutralMinionsKilled": 158, "perk0": 8128, "perkPrimaryStyle": 8100, "perkSubStyle": 8000, "subteamPlacement": 0, "totalDamageDealtToChampions": 12628, "totalMinionsKilled": 15, "visionScore": 13, "win": true} },
            {"championId": 63, "participantId": 8, "spell1Id": 4, "spell2Id": 12, "teamId": 200, "timeline": {"lane": "MIDDLE", "role": "SOLO"},
              "stats": {"assists": 8, "champLevel": 15, "deaths": 9, "gameEndedInEarlySurrender": false, "goldEarned": 10495, "item0": 1056, "item1": 3145, "item2": 3175, "item3": 6653, "item4": 2503, "item5": 1058, "item6": 3340, "kills": 4, "neutralMinionsKilled": 4, "perk0": 8128, "perkPrimaryStyle": 8100, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 21329, "totalMinionsKilled": 175, "visionScore": 18, "win": true} },
            {"championId": 145, "participantId": 9, "spell1Id": 4, "spell2Id": 21, "teamId": 200, "timeline": {"lane": "BOTTOM", "role": "CARRY"},
              "stats": {"assists": 7, "champLevel": 14, "deaths": 4, "gameEndedInEarlySurrender": false, "goldEarned": 13427, "item0": 1086, "item1": 3087, "item2": 1036, "item3": 6672, "item4": 3115, "item5": 1036, "item6": 3340, "kills": 12, "neutralMinionsKilled": 4, "perk0": 8008, "perkPrimaryStyle": 8000, "perkSubStyle": 8200, "subteamPlacement": 0, "totalDamageDealtToChampions": 21089, "totalMinionsKilled": 151, "visionScore": 15, "win": true} },
            {"championId": 412, "participantId": 10, "spell1Id": 4, "spell2Id": 3, "teamId": 200, "timeline": {"lane": "BOTTOM", "role": "SUPPORT"},
              "stats": {"assists": 15, "champLevel": 13, "deaths": 7, "gameEndedInEarlySurrender": false, "goldEarned": 8278, "item0": 3110, "item1": 3143, "item2": 3876, "item3": 3111, "item4": 2022, "item5": 0, "item6": 3340, "kills": 1, "neutralMinionsKilled": 0, "perk0": 8465, "perkPrimaryStyle": 8400, "perkSubStyle": 8300, "subteamPlacement": 0, "totalDamageDealtToChampions": 11120, "totalMinionsKilled": 46, "visionScore": 47, "win": true} }
          ],
          "teams": [
            {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 0, "hordeKills": 1, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 4, "bans": [{"championId": 35, "pickTurn": 1},{"championId": 360, "pickTurn": 2},{"championId": 122, "pickTurn": 3},{"championId": 38, "pickTurn": 4},{"championId": 8, "pickTurn": 5}]},
            {"teamId": 200, "win": "Win", "baronKills": 1, "dragonKills": 3, "hordeKills": 2, "inhibitorKills": 1, "riftHeraldKills": 1, "towerKills": 9, "bans": [{"championId": 55, "pickTurn": 6},{"championId": 39, "pickTurn": 7},{"championId": 75, "pickTurn": 8},{"championId": 200, "pickTurn": 9},{"championId": 800, "pickTurn": 10}]}
          ]
        }
        """;

        /// <summary>
        /// /lol-match-history/v1/games/{gameId} of an Arena game, <b>constructed, not recorded</b> (no Arena game in the
        /// recorded history yet): the shape of <see cref="Game"/> with game mode "CHERRY" on map 30, four of the duos, two per LCU
        /// team (teamId 100/200), each player's duo in <c>stats.playerSubteamId</c> and its place in <c>subteamPlacement</c>.
        /// The local player (Me) plays duo 3 with Ally1 and places 3rd; duo 2 won, duo 4 placed 2nd, duo 1 6th. Each player's
        /// kills are unique so a test can <c>.Replace</c> one player's duo id away.
        /// </summary>
        public const string ArenaGame = """
        {
          "endOfGameResult": "GameComplete", "gameCreation": 1792690000000, "gameDuration": 1100, "gameId": 4000000010,
          "gameMode": "CHERRY", "gameType": "MATCHED_GAME", "mapId": 30, "queueId": 1700,
          "participantIdentities": [
            {"participantId": 1, "player": {"gameName": "Me", "tagLine": "EUNE", "puuid": "me-puuid", "summonerId": 1001}},
            {"participantId": 2, "player": {"gameName": "Ally1", "tagLine": "TAG2", "puuid": "ally1-puuid", "summonerId": 1002}},
            {"participantId": 3, "player": {"gameName": "Enemy1", "tagLine": "TAG1", "puuid": "enemy1-puuid", "summonerId": 2001}},
            {"participantId": 4, "player": {"gameName": "Enemy2", "tagLine": "TAG2", "puuid": "enemy2-puuid", "summonerId": 2002}},
            {"participantId": 5, "player": {"gameName": "Enemy3", "tagLine": "TAG3", "puuid": "enemy3-puuid", "summonerId": 2003}},
            {"participantId": 6, "player": {"gameName": "Enemy4", "tagLine": "TAG4", "puuid": "enemy4-puuid", "summonerId": 2004}},
            {"participantId": 7, "player": {"gameName": "Enemy5", "tagLine": "TAG5", "puuid": "enemy5-puuid", "summonerId": 2005}},
            {"participantId": 8, "player": {"gameName": "Enemy6", "tagLine": "TAG6", "puuid": "enemy6-puuid", "summonerId": 2006}}
          ],
          "participants": [
            {"championId": 103, "participantId": 1, "spell1Id": 2201, "spell2Id": 2202, "teamId": 100, "stats": {"kills": 7, "playerSubteamId": 3, "subteamPlacement": 3, "win": true, "deaths": 4, "assists": 5, "goldEarned": 9000, "champLevel": 15}},
            {"championId": 51, "participantId": 2, "spell1Id": 2201, "spell2Id": 2202, "teamId": 100, "stats": {"kills": 5, "playerSubteamId": 3, "subteamPlacement": 3, "win": true, "deaths": 5, "assists": 6, "goldEarned": 8500, "champLevel": 15}},
            {"championId": 157, "participantId": 3, "spell1Id": 2201, "spell2Id": 2202, "teamId": 100, "stats": {"kills": 3, "playerSubteamId": 1, "subteamPlacement": 6, "win": false, "deaths": 6, "assists": 2, "goldEarned": 6000, "champLevel": 12}},
            {"championId": 777, "participantId": 4, "spell1Id": 2201, "spell2Id": 2202, "teamId": 100, "stats": {"kills": 2, "playerSubteamId": 1, "subteamPlacement": 6, "win": false, "deaths": 6, "assists": 3, "goldEarned": 5800, "champLevel": 12}},
            {"championId": 92, "participantId": 5, "spell1Id": 2201, "spell2Id": 2202, "teamId": 200, "stats": {"kills": 9, "playerSubteamId": 2, "subteamPlacement": 1, "win": true, "deaths": 2, "assists": 7, "goldEarned": 11000, "champLevel": 18}},
            {"championId": 875, "participantId": 6, "spell1Id": 2201, "spell2Id": 2202, "teamId": 200, "stats": {"kills": 8, "playerSubteamId": 2, "subteamPlacement": 1, "win": true, "deaths": 3, "assists": 8, "goldEarned": 10500, "champLevel": 18}},
            {"championId": 38, "participantId": 7, "spell1Id": 2201, "spell2Id": 2202, "teamId": 200, "stats": {"kills": 6, "playerSubteamId": 4, "subteamPlacement": 2, "win": true, "deaths": 3, "assists": 4, "goldEarned": 10000, "champLevel": 17}},
            {"championId": 69, "participantId": 8, "spell1Id": 2201, "spell2Id": 2202, "teamId": 200, "stats": {"kills": 4, "playerSubteamId": 4, "subteamPlacement": 2, "win": true, "deaths": 4, "assists": 6, "goldEarned": 9500, "champLevel": 17}}
          ],
          "teams": [
            {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 0, "towerKills": 0, "bans": [{"championId": 10, "pickTurn": 1}]},
            {"teamId": 200, "win": "Win", "baronKills": 0, "dragonKills": 0, "towerKills": 0, "bans": [{"championId": 20, "pickTurn": 2}]}
          ]
        }
        """;
    }

    /// <summary>
    /// Another player's profile endpoints as the client answered them for a friend. The friend is <see cref="FriendPuuid"/>
    /// (the first entry of <see cref="FriendsSamples.Friends"/>, named "Friend#EUW" here); the ranked endpoint answers the
    /// same shape as the local player's, so <see cref="Own.RankedStats"/> is reused where the numbers don't matter
    /// (<see cref="RankedStats"/> has what the client really sends for someone else).
    /// </summary>
    public static class Player
    {
        public const string FriendPuuid = "aaaa0001-0000-0000-0000-000000000001";

        public const string SummonerPath = "/lol-summoner/v2/summoners/puuid/" + FriendPuuid;
        public const string RankedPath = "/lol-ranked/v1/ranked-stats/" + FriendPuuid;
        public const string MasteryPath = "/lol-champion-mastery/v1/" + FriendPuuid + "/champion-mastery";
        public const string MatchesPath = "/lol-match-history/v1/products/lol/" + FriendPuuid + "/matches";

        /// <summary>The GETs of one player profile read, in the order the connector sends them.</summary>
        public static readonly IReadOnlyList<string> Reads =
            [$"GET {SummonerPath}", $"GET {RankedPath}", $"GET {MasteryPath}", $"GET {MatchesPath}"];

        /// <summary>
        /// /lol-ranked/v1/ranked-stats/{puuid} for a friend, trimmed. Recorded read-only against a live client (Nov 2026):
        /// real wins, but <c>losses</c> is always 0 for another player, here and in <c>league-ladders</c> and
        /// <c>social-leaderboard-ranked-queue-stats-for-puuids</c>. The local player's own read has real losses.
        /// </summary>
        public const string RankedStats = """
        {"queues": [
          {"queueType": "RANKED_FLEX_SR", "tier": "EMERALD", "division": "II", "leaguePoints": 47, "wins": 104, "losses": 0, "isProvisional": false, "provisionalGamesRemaining": 0, "miniSeriesProgress": ""},
          {"queueType": "RANKED_SOLO_5x5", "tier": "PLATINUM", "division": "I", "leaguePoints": 34, "wins": 92, "losses": 0, "isProvisional": false, "provisionalGamesRemaining": 0, "miniSeriesProgress": ""}
        ]}
        """;

        /// <summary>The Riot ID lookup's path (the Riot ID rides the query string, which <see cref="FakeLcu"/> records).</summary>
        public const string AliasLookupPath = "/lol-summoner/v1/alias/lookup";

        /// <summary>
        /// /lol-summoner/v1/alias/lookup?gameName=…&amp;tagLine=… for a Riot ID that exists (recorded read-only for the local
        /// player's own Riot ID, Oct 23 2026, patch 16.21; asked in lower case, answered in League's spelling). Here it is the
        /// friend's: <c>LolSummonerAliasLookupResponse</c> in <c>/help</c>.
        /// </summary>
        public const string AliasLookup = """
        { "alias": { "gameName": "Friend", "tagLine": "EUW" }, "puuid": "aaaa0001-0000-0000-0000-000000000001" }
        """;

        /// <summary>The same lookup for a Riot ID nobody has: still HTTP 200, everything empty (recorded the same day).</summary>
        public const string AliasLookupNotFound = """
        { "alias": { "gameName": "", "tagLine": "" }, "puuid": "" }
        """;

        /// <summary>The friend's newest game, also served in full by <see cref="Handler"/> (<see cref="Own.Game"/>).</summary>
        public const long GameId = 4100000003;

        /// <summary>
        /// A fake LCU with the local player's profile (<see cref="Own.Handler"/>), the friends list, and the friend's
        /// profile (paths for <see cref="FriendPuuid"/>, or for the given puuid when the sample stands in for someone else).
        /// </summary>
        public static FakeLcu Handler(string puuid = FriendPuuid)
        {
            var handler = Own.Handler();
            handler.Routes["/lol-chat/v1/friends"] = FriendsSamples.Friends;
            handler.Routes[SummonerPath.Replace(FriendPuuid, puuid)] = Summoner;
            handler.Routes[RankedPath.Replace(FriendPuuid, puuid)] = Own.RankedStats;
            handler.Routes[MasteryPath.Replace(FriendPuuid, puuid)] = Mastery;
            handler.Routes[MatchesPath.Replace(FriendPuuid, puuid)] = Matches.Replace(FriendPuuid, puuid);
            return handler;
        }

        /// <summary>/lol-summoner/v2/summoners/puuid/{puuid}: the current-summoner shape, XP included, for anyone.</summary>
        public const string Summoner = """
        {
          "accountId": 3001, "displayName": "", "gameName": "Friend", "internalName": "", "nameChangeFlag": false,
          "percentCompleteForNextLevel": 40, "privacy": "PUBLIC", "profileIconId": 7210, "puuid": "aaaa0001-0000-0000-0000-000000000001",
          "summonerId": 3001, "summonerLevel": 456, "tagLine": "EUW", "unnamed": true, "xpSinceLastLevel": 1152, "xpUntilNextLevel": 2880
        }
        """;

        /// <summary>
        /// /lol-champion-mastery/v1/{puuid}/champion-mastery (3 of the list's 170 entries): the local list's entries, sorted by
        /// points. There is no score endpoint for other players.
        /// </summary>
        public const string Mastery = """
        [
          {"championId": 222, "championLevel": 40, "championPoints": 450000, "championPointsSinceLastLevel": 1000, "championPointsUntilNextLevel": 10000, "highestGrade": "S", "lastPlayTime": 1792500000000, "puuid": "aaaa0001-0000-0000-0000-000000000001"},
          {"championId": 412, "championLevel": 22, "championPoints": 210000, "championPointsSinceLastLevel": 3000, "championPointsUntilNextLevel": 8000, "highestGrade": "", "lastPlayTime": 1791000000000, "puuid": "aaaa0001-0000-0000-0000-000000000001"},
          {"championId": 1, "championLevel": 3, "championPoints": 9000, "championPointsSinceLastLevel": 1200, "championPointsUntilNextLevel": 1400, "highestGrade": "", "lastPlayTime": 1780000000000, "puuid": "aaaa0001-0000-0000-0000-000000000001"}
        ]
        """;

        /// <summary>
        /// /lol-match-history/v1/products/lol/{puuid}/matches (3 of the client's 20 games, newest first): the current-summoner
        /// list's shape, only the friend's participant per game. <c>begIndex</c> / <c>endIndex</c> were ignored (always the
        /// newest 20; ~2 s for a player the client hadn't loaded). Recorded kinds: a ranked loss as top, a ranked win as a
        /// support whose lane was NONE (role SUPPORT), and an Arena game (CHERRY, queue 1750).
        /// </summary>
        public const string Matches = """
        {
          "accountId": 3001, "platformId": "EUN1",
          "games": {
            "gameCount": 3, "gameIndexBegin": 0, "gameIndexEnd": 2,
            "games": [
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1792699895462, "gameDuration": 1644, "gameId": 4100000003,
                "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "mapId": 11, "queueId": 420,
                "participantIdentities": [
                  {"participantId": 1, "player": {"accountId": 0, "gameName": "Friend", "tagLine": "EUW", "profileIcon": 7210, "puuid": "aaaa0001-0000-0000-0000-000000000001", "summonerId": 3001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 82, "participantId": 1, "spell1Id": 14, "spell2Id": 4, "teamId": 100, "timeline": {"lane": "TOP", "role": "SOLO"},
                    "stats": {"assists": 1, "champLevel": 15, "deaths": 11, "gameEndedInEarlySurrender": false, "goldEarned": 9578, "item0": 3116, "item1": 3111, "item2": 3075, "item3": 2508, "item4": 3147, "item5": 0, "item6": 3363, "kills": 2, "neutralMinionsKilled": 16, "perk0": 8010, "perkPrimaryStyle": 8000, "perkSubStyle": 8400, "playerSubteamId": 0, "subteamPlacement": 0, "totalDamageDealtToChampions": 15556, "totalMinionsKilled": 155, "visionScore": 29, "win": false} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 0, "hordeKills": 1, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 4, "bans": []},
                  {"teamId": 200, "win": "Win", "baronKills": 1, "dragonKills": 3, "hordeKills": 2, "inhibitorKills": 1, "riftHeraldKills": 1, "towerKills": 9, "bans": []}
                ]
              },
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1792600000000, "gameDuration": 1900, "gameId": 4100000002,
                "gameMode": "CLASSIC", "gameType": "MATCHED_GAME", "mapId": 11, "queueId": 420,
                "participantIdentities": [
                  {"participantId": 10, "player": {"accountId": 0, "gameName": "Friend", "tagLine": "EUW", "profileIcon": 7210, "puuid": "aaaa0001-0000-0000-0000-000000000001", "summonerId": 3001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 412, "participantId": 10, "spell1Id": 4, "spell2Id": 3, "teamId": 200, "timeline": {"lane": "NONE", "role": "SUPPORT"},
                    "stats": {"assists": 15, "champLevel": 13, "deaths": 7, "gameEndedInEarlySurrender": false, "goldEarned": 8278, "item0": 3110, "item1": 3143, "item2": 3876, "item3": 3111, "item4": 2022, "item5": 0, "item6": 3364, "kills": 1, "neutralMinionsKilled": 0, "perk0": 8465, "perkPrimaryStyle": 8400, "perkSubStyle": 8300, "playerSubteamId": 0, "subteamPlacement": 0, "totalDamageDealtToChampions": 11120, "totalMinionsKilled": 46, "visionScore": 47, "win": true} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "baronKills": 0, "dragonKills": 1, "hordeKills": 0, "inhibitorKills": 0, "riftHeraldKills": 0, "towerKills": 2, "bans": []},
                  {"teamId": 200, "win": "Win", "baronKills": 1, "dragonKills": 4, "hordeKills": 3, "inhibitorKills": 2, "riftHeraldKills": 1, "towerKills": 10, "bans": []}
                ]
              },
              {
                "endOfGameResult": "GameComplete", "gameCreation": 1782000000000, "gameDuration": 1100, "gameId": 4100000001,
                "gameMode": "CHERRY", "gameType": "MATCHED_GAME", "mapId": 30, "queueId": 1750,
                "participantIdentities": [
                  {"participantId": 3, "player": {"accountId": 0, "gameName": "Friend", "tagLine": "EUW", "profileIcon": 7210, "puuid": "aaaa0001-0000-0000-0000-000000000001", "summonerId": 3001, "summonerName": ""}}
                ],
                "participants": [
                  {"championId": 222, "participantId": 3, "spell1Id": 2201, "spell2Id": 2202, "teamId": 100, "timeline": {"lane": "MIDDLE", "role": "SUPPORT"},
                    "stats": {"assists": 6, "champLevel": 16, "deaths": 5, "gameEndedInEarlySurrender": false, "goldEarned": 9100, "item0": 0, "item1": 0, "item2": 0, "item3": 0, "item4": 0, "item5": 0, "item6": 0, "kills": 8, "neutralMinionsKilled": 0, "perk0": 0, "perkPrimaryStyle": 0, "perkSubStyle": 0, "playerSubteamId": 2, "subteamPlacement": 5, "totalDamageDealtToChampions": 30000, "totalMinionsKilled": 0, "visionScore": 0, "win": false} }
                ],
                "teams": [
                  {"teamId": 100, "win": "Fail", "bans": []},
                  {"teamId": 200, "win": "Fail", "bans": []}
                ]
              }
            ]
          }
        }
        """;
    }
}
