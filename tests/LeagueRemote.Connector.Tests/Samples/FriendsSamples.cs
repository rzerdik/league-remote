namespace LeagueRemote.Connector.Tests;

/// <summary>
/// /lol-chat/v1/friends and /lol-chat/v1/friend-groups in the shape a live client sent them (Oct 22 2026, patch 16.21),
/// with every name, puuid and id replaced by an obvious placeholder. The first two entries keep every field the client
/// sends; the rest are trimmed to what the mapper reads. The <c>lol</c> map's values are all strings.
/// </summary>
internal static class FriendsSamples
{
    public const string InGameId = "aaaa0001-0000-0000-0000-000000000001@eu2.pvp.net";
    public const string InGamePath = "/lol-chat/v1/friends/aaaa0001-0000-0000-0000-000000000001%40eu2.pvp.net";

    public const string Friends = """
    [
      {
        "availability": "dnd", "discordId": "", "discordInfo": null, "discordOnlineStatus": "", "displayGroupId": 0,
        "displayGroupName": "**Default", "gameName": "Friend1", "gameTag": "TAG1", "groupId": 0, "groupName": "**Default",
        "icon": 1001, "id": "aaaa0001-0000-0000-0000-000000000001@eu2.pvp.net", "isP2PConversationMuted": false,
        "lastSeenOnlineTimestamp": null,
        "lol": {
          "bannerIdSelected": "", "challengeCrystalLevel": "PLATINUM", "challengePoints": "1000", "championId": "16",
          "companionId": "1", "damageSkinId": "1", "gameId": "1000000001", "gameMode": "CLASSIC", "gameQueueType": "RANKED_SOLO_5x5",
          "gameStatus": "inGame", "iconOverride": "", "isObservable": "ALL", "legendaryMasteryScore": "0", "level": "100",
          "mapId": "11", "mapSkinId": "55", "playerTitleSelected": "", "profileIcon": "1001", "pty": "",
          "puuid": "aaaa0001-0000-0000-0000-000000000001", "queueId": "420", "rankedLeagueDivision": "I",
          "rankedLeagueQueue": "RANKED_SOLO_5x5", "rankedLeagueTier": "EMERALD", "rankedLosses": "0",
          "rankedPrevSeasonDivision": "II", "rankedPrevSeasonTier": "EMERALD", "rankedWins": "60", "regalia": "{}",
          "skinVariant": "", "skinname": "Soraka", "spectatorKey": "", "timeStamp": "1792693207373"
        },
        "name": "", "note": "note", "patchline": "live", "pid": "aaaa0001-0000-0000-0000-000000000001@eu2.pvp.net",
        "platformId": "EUN1", "product": "league_of_legends", "productName": "League of Legends",
        "puuid": "aaaa0001-0000-0000-0000-000000000001", "relationshipOnRiot": "friend", "statusMessage": "", "summary": "",
        "summonerId": 1001, "time": 1792693211466
      },
      {
        "availability": "offline", "discordId": "", "discordInfo": null, "discordOnlineStatus": "", "displayGroupId": 0,
        "displayGroupName": "**Default", "gameName": "Zażółć", "gameTag": "TAG2", "groupId": 0, "groupName": "**Default",
        "icon": 1002, "id": "aaaa0002-0000-0000-0000-000000000002@eu2.pvp.net", "isP2PConversationMuted": false,
        "lastSeenOnlineTimestamp": null, "lol": {}, "name": "", "note": "", "patchline": "", "pid": "aaaa0002-0000-0000-0000-000000000002@eu2.pvp.net",
        "platformId": "EUN1", "product": "", "productName": "", "puuid": "aaaa0002-0000-0000-0000-000000000002",
        "relationshipOnRiot": "friend", "statusMessage": "", "summary": "", "summonerId": 1002, "time": 1792675347460
      },
      { "availability": "away", "gameName": "Friend3", "gameTag": "TAG3", "groupId": 0, "icon": 1003,
        "id": "aaaa0003@eu2.pvp.net", "puuid": "aaaa0003", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": ":D", "note": "", "summonerId": 1003,
        "lol": { "gameStatus": "outOfGame", "gameQueueType": "", "queueId": "", "championId": "", "mapId": "",
                 "rankedLeagueTier": "GOLD", "rankedLeagueDivision": "II", "rankedLeagueQueue": "RANKED_SOLO_5x5", "timeStamp": "1792692110744" } },
      { "availability": "chat", "gameName": "Friend4", "gameTag": "TAG4", "groupId": 0, "icon": 1004,
        "id": "aaaa0004@eu2.pvp.net", "puuid": "aaaa0004", "product": "riot_client", "productName": "Riot Client",
        "statusMessage": "", "note": "", "summonerId": 1004, "lol": {} },
      { "availability": "chat", "gameName": "Friend5", "gameTag": "TAG5", "groupId": 7, "icon": 1005,
        "id": "aaaa0005@eu2.pvp.net", "puuid": "aaaa0005", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": "", "note": "", "summonerId": 1005,
        "lol": { "gameStatus": "outOfGame", "gameQueueType": "", "queueId": "2400", "championId": "", "gameMode": "KIWI",
                 "timeStamp": "1792689868266", "rankedLeagueTier": "EMERALD", "rankedLeagueDivision": "I", "rankedLeagueQueue": "RANKED_FLEX_SR",
                 "pty": "{\"isPartyOpen\":true,\"maxPlayers\":5,\"partyId\":\"00000000-0000-0000-0000-000000000001\",\"queueId\":2400,\"summoners\":[1,2,1005]}" } },
      { "availability": "dnd", "gameName": "Friend6", "gameTag": "TAG6", "groupId": 0, "icon": 1006,
        "id": "aaaa0006@eu2.pvp.net", "puuid": "aaaa0006", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": "", "note": "", "summonerId": 1006,
        "lol": { "gameStatus": "championSelect", "gameQueueType": "NORMAL", "queueId": "400", "championId": "", "timeStamp": "1792693000000",
                 "rankedLeagueTier": "MASTER", "rankedLeagueDivision": "I", "rankedLeagueQueue": "RANKED_SOLO_5x5" } },
      { "availability": "dnd", "gameName": "Friend7", "gameTag": "TAG7", "groupId": 0, "icon": 1007,
        "id": "aaaa0007@eu2.pvp.net", "puuid": "aaaa0007", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": "", "note": "", "summonerId": 1007,
        "lol": { "gameStatus": "inQueue", "queueId": "450", "timeStamp": "1792693100000", "rankedLeagueTier": "", "rankedLeagueDivision": "NA" } },
      { "availability": "chat", "gameName": "host", "gameTag": "EUW", "groupId": 0, "icon": 1008,
        "id": "aaaa0008@eu2.pvp.net", "puuid": "aaaa0008", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": "", "note": "", "summonerId": 1008,
        "lol": { "gameStatus": "hosting_RANKED_FLEX_SR", "queueId": "440", "timeStamp": "1792693150000" } },
      { "availability": "mobile", "gameName": "Friend9", "gameTag": "TAG9", "groupId": 0, "icon": 1009,
        "id": "aaaa0009@eu2.pvp.net", "puuid": "aaaa0009", "product": "league_of_legends", "productName": "League of Legends",
        "statusMessage": "", "note": "", "summonerId": 1009, "lol": {} },
      { "availability": "offline", "gameName": "", "name": "OldName", "gameTag": "", "groupId": 7, "icon": 1010,
        "id": "aaaa0010@eu2.pvp.net", "puuid": "aaaa0010", "product": "", "productName": "",
        "statusMessage": "", "note": "", "summonerId": 0, "lastSeenOnlineTimestamp": "1792600000000", "lol": {} }
    ]
    """;

    /// <summary>The live client only had "**Default"; "Duo" (a user group) and a meta group are added to cover them.</summary>
    public const string FriendGroups = """
    [
      {"collapsed":false,"id":0,"isLocalized":false,"isMetaGroup":false,"name":"**Default","priority":3},
      {"collapsed":false,"id":7,"isLocalized":false,"isMetaGroup":false,"name":"Duo","priority":1},
      {"collapsed":true,"id":9,"isLocalized":true,"isMetaGroup":true,"name":"**Offline","priority":2}
    ]
    """;

    /// <summary>
    /// /lol-chat/v2/friend-requests as a live client (patch 16.21) sends it, names and puuids replaced: outgoing requests
    /// carry icon -1 and summonerId 0, every id is "0" (requests are keyed by puuid). One incoming and one "both" added.
    /// </summary>
    public const string FriendRequests = """
    [
      {"direction":"out","gameName":"Zażółć Test","icon":-1,"id":"0","name":"Zażółć Test","note":"","pid":"","puuid":"bbbb0001-0000-0000-0000-000000000001","summonerId":0,"tagLine":"TAG1"},
      {"direction":"out","gameName":"Alpha Test","icon":-1,"id":"0","name":"Alpha Test","note":"","pid":"","puuid":"bbbb0002-0000-0000-0000-000000000002","summonerId":0,"tagLine":"TAG2"},
      {"direction":"in","gameName":"Newcomer","icon":1003,"id":"0","name":"Newcomer","note":"","pid":"","puuid":"bbbb0003-0000-0000-0000-000000000003","summonerId":4242,"tagLine":"TAG3"},
      {"direction":"both","gameName":"JustAccepted","icon":1004,"id":"0","name":"JustAccepted","note":"","pid":"","puuid":"bbbb0004-0000-0000-0000-000000000004","summonerId":0,"tagLine":"TAG4"}
    ]
    """;

    public static FakeLcu Handler()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-chat/v1/friends"] = Friends;
        handler.Routes["/lol-chat/v1/friend-groups"] = FriendGroups;
        return handler;
    }
}
