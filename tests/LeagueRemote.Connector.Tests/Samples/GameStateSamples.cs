namespace LeagueRemote.Connector.Tests;

/// <summary>Game-state JSON: lobbies, champ-select sessions, invitations and queues (names and ids are placeholders).</summary>
internal static class GameStateSamples
{
    /// <summary>
    /// Trimmed /lol-lobby/v2/lobby as current LCU builds send it: the local player (leader) has an empty gameName and
    /// summonerName; one member still carries a Riot ID (tag as "gameTag"); one has only a summonerId.
    /// </summary>
    public const string Lobby = """
    {
      "canStartActivity": true,
      "gameConfig": { "queueId": 420, "gameMode": "CLASSIC", "isCustom": false },
      "localMember": { "puuid": "me-puuid", "summonerId": 1001, "isLeader": true },
      "members": [
        { "puuid": "me-puuid", "summonerId": 1001, "gameName": "", "gameTag": "", "summonerName": "", "isLeader": true, "firstPositionPreference": "MIDDLE", "secondPositionPreference": "", "summonerIconId": 5132 },
        { "puuid": "friend-puuid", "summonerId": 1002, "gameName": "Friend", "gameTag": "EUNE", "summonerName": "", "isLeader": false },
        { "puuid": "", "summonerId": 1003, "gameName": "", "summonerName": "", "isLeader": false }
      ]
    }
    """;

    /// <summary>/lol-lobby/v2/lobby for a Practice Tool custom lobby (queueId -1).</summary>
    public const string PracticeToolLobby = """
    {
      "canStartActivity": true,
      "gameConfig": { "queueId": -1, "gameMode": "PRACTICETOOL", "isCustom": true },
      "localMember": { "puuid": "me-puuid", "summonerId": 1001, "isLeader": true },
      "members": [ { "puuid": "me-puuid", "summonerId": 1001, "gameName": "Me", "gameTag": "EUW", "isLeader": true } ]
    }
    """;

    /// <summary>/lol-summoner/v1/current-summoner.</summary>
    public const string CurrentSummoner = """
    { "puuid": "me-puuid", "summonerId": 1001, "gameName": "Me", "tagLine": "EUW", "displayName": "Me" }
    """;

    /// <summary>
    /// Trimmed champ-select session. Local player is cell 2; their ban is done, their pick is in progress.
    /// theirTeam contains a hovered champion (777); the enemy's name, puuid and summoner id must never leak.
    /// </summary>
    public const string ChampSelectSession = """
    {
      "localPlayerCellId": 2,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 27500 },
      "myTeam": [
        { "cellId": 1, "championId": 103, "championPickIntent": 0, "assignedPosition": "middle", "spell1Id": 4, "spell2Id": 14, "gameName": "Ally", "tagLine": "EUW" },
        { "cellId": 2, "championId": 0, "championPickIntent": 222, "assignedPosition": "bottom", "spell1Id": 4, "spell2Id": 7, "gameName": "Me", "tagLine": "EUW" }
      ],
      "theirTeam": [
        { "cellId": 5, "championId": 777, "championPickIntent": 0, "assignedPosition": "", "spell1Id": 4, "spell2Id": 12, "gameName": "Enemy", "tagLine": "XXX", "puuid": "enemy-puuid", "summonerId": 9005 }
      ],
      "actions": [
        [
          { "id": 10, "actorCellId": 2, "championId": 55, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 11, "actorCellId": 5, "championId": 1, "type": "ban", "completed": true, "isAllyAction": false, "isInProgress": false }
        ],
        [
          { "id": 20, "actorCellId": 1, "championId": 103, "type": "pick", "completed": true, "isAllyAction": true, "isInProgress": false }
        ],
        [
          { "id": 21, "actorCellId": 2, "championId": 222, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 22, "actorCellId": 5, "championId": 777, "type": "pick", "completed": false, "isAllyAction": false, "isInProgress": true }
        ]
      ]
    }
    """;

    /// <summary>
    /// Ban phase, the local player's (cell 2) ban is in progress. The enemy (cells 5, 6) has one completed ban and one
    /// known position; my team's earlier ban is only listed in <c>bans.myTeamBans</c>. Swaps/trades offered with cell 1.
    /// </summary>
    public const string BanPhaseSession = """
    {
      "localPlayerCellId": 2,
      "timer": { "phase": "BAN_PICK", "adjustedTimeLeftInPhase": 20000 },
      "bans": { "myTeamBans": [ 64 ], "theirTeamBans": [], "numBans": 10 },
      "myTeam": [
        { "cellId": 1, "championId": 0, "championPickIntent": 0, "assignedPosition": "middle", "spell1Id": 4, "spell2Id": 14 },
        { "cellId": 2, "championId": 0, "championPickIntent": 0, "assignedPosition": "bottom", "spell1Id": 4, "spell2Id": 7 }
      ],
      "theirTeam": [
        { "cellId": 5, "championId": 0, "championPickIntent": 0, "assignedPosition": "top", "gameName": "Enemy", "puuid": "enemy-puuid" },
        { "cellId": 6, "championId": -1, "championPickIntent": 0, "assignedPosition": "", "gameName": "Enemy2", "puuid": "enemy2-puuid" }
      ],
      "actions": [
        [
          { "id": 10, "actorCellId": 2, "championId": 0, "type": "ban", "completed": false, "isAllyAction": true, "isInProgress": true },
          { "id": 11, "actorCellId": 5, "championId": 99, "type": "ban", "completed": true, "isAllyAction": false, "isInProgress": false },
          { "id": 12, "actorCellId": 6, "championId": 0, "type": "ban", "completed": false, "isAllyAction": false, "isInProgress": true }
        ],
        [
          { "id": 20, "actorCellId": 2, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": false }
        ]
      ],
      "pickOrderSwaps": [ { "id": 31, "cellId": 1, "state": "AVAILABLE" } ],
      "positionSwaps": [ { "id": 32, "cellId": 1, "state": "RECEIVED" } ],
      "trades": [ { "id": 33, "cellId": 1, "state": "INVALID" }, { "id": 0, "cellId": 6, "state": "AVAILABLE" } ]
    }
    """;

    /// <summary>
    /// /lol-lobby/v2/received-invitations (LolLobbyReceivedInvitationDto[], fields as the client's /help lists them, Oct 2026;
    /// the live list was empty when recorded, so the values are made up). A Ranked Flex invite whose sender's name is empty
    /// (current builds), one League won't let the player accept, an already declined one and a self-invite: the last two are
    /// never shown.
    /// </summary>
    public const string ReceivedInvitations = """
    [
      {
        "invitationId": "inv-flex", "invitationType": "lobby", "state": "Pending", "timestamp": "1761157200000",
        "fromPuuid": "friend-puuid", "fromSummonerId": 1002, "fromSummonerName": "", "isSelfInvite": false,
        "canAcceptInvitation": true, "restrictions": [],
        "gameConfig": { "gameMode": "CLASSIC", "queueId": 440, "mapId": 11, "inviteGameType": "RANKED_GAME" }
      },
      {
        "invitationId": "inv-arena", "invitationType": "lobby", "state": "Pending", "timestamp": "1761157210000",
        "fromPuuid": "other-puuid", "fromSummonerId": 1003, "fromSummonerName": "Old Name", "isSelfInvite": false,
        "canAcceptInvitation": false,
        "restrictions": [ { "restrictionCode": "QueueDisabled", "restrictionArgs": {}, "summonerIds": [1001], "puuids": ["me-puuid"] } ],
        "gameConfig": { "gameMode": "CHERRY", "queueId": 1700, "mapId": 30, "inviteGameType": "NORMAL_GAME" }
      },
      {
        "invitationId": "inv-old", "invitationType": "lobby", "state": "Declined", "timestamp": "1761157000000",
        "fromPuuid": "friend-puuid", "fromSummonerId": 1002, "fromSummonerName": "", "isSelfInvite": false,
        "canAcceptInvitation": true, "restrictions": [],
        "gameConfig": { "gameMode": "ARAM", "queueId": 450, "mapId": 12, "inviteGameType": "NORMAL_GAME" }
      },
      {
        "invitationId": "inv-self", "invitationType": "lobby", "state": "Pending", "timestamp": "1761157220000",
        "fromPuuid": "me-puuid", "fromSummonerId": 1001, "fromSummonerName": "", "isSelfInvite": true,
        "canAcceptInvitation": true, "restrictions": [],
        "gameConfig": { "gameMode": "CLASSIC", "queueId": 420, "mapId": 11, "inviteGameType": "RANKED_GAME" }
      }
    ]
    """;

    /// <summary>/lol-game-queues/v1/queues, trimmed (live, Oct 2026: <c>description</c> is the name the client shows).</summary>
    public const string Queues = """
    [
      { "id": 400, "name": "Normal", "description": "Draft Pick", "shortName": "Normal", "gameMode": "CLASSIC", "queueAvailability": "Available" },
      { "id": 440, "name": "Ranked Flex", "description": "Ranked Flex", "shortName": "Ranked Flex", "gameMode": "CLASSIC", "queueAvailability": "Available" },
      { "id": 1700, "name": "Arena", "description": "Arena", "shortName": "Arena", "gameMode": "CHERRY", "queueAvailability": "PlatformDisabled" }
    ]
    """;

    /// <summary>
    /// /lol-lobby/v2/lobby with sent invitations. The local player's own entry (state Accepted, invitationType "invalid",
    /// empty name) is what the live client sends for a solo lobby (Oct 2026, also in /lol-lobby/v2/lobby/invitations); the
    /// pending, declined and joined ones are made up in the same shape. The leader may invite; the party has room.
    /// </summary>
    public const string LobbyWithInvitations = """
    {
      "canStartActivity": true,
      "gameConfig": { "queueId": 440, "gameMode": "CLASSIC", "isCustom": false, "isLobbyFull": false, "maxLobbySize": 5, "showPositionSelector": true },
      "localMember": { "puuid": "me-puuid", "summonerId": 1001, "isLeader": true, "allowedInviteOthers": true },
      "members": [
        { "puuid": "me-puuid", "summonerId": 1001, "gameName": "Me", "gameTag": "EUW", "isLeader": true },
        { "puuid": "joined-puuid", "summonerId": 1004, "gameName": "Joined", "gameTag": "EUW", "isLeader": false }
      ],
      "invitations": [
        { "invitationId": "", "invitationType": "invalid", "state": "Accepted", "timestamp": "0", "toPuuid": "me-puuid", "toSummonerId": 1001, "toSummonerName": "" },
        { "invitationId": "", "invitationType": "lobby", "state": "Pending", "timestamp": "1761157300000", "toPuuid": "friend-puuid", "toSummonerId": 1002, "toSummonerName": "" },
        { "invitationId": "", "invitationType": "lobby", "state": "Declined", "timestamp": "1761157310000", "toPuuid": "other-puuid", "toSummonerId": 1003, "toSummonerName": "" },
        { "invitationId": "", "invitationType": "lobby", "state": "Joined", "timestamp": "1761157320000", "toPuuid": "joined-puuid", "toSummonerId": 1004, "toSummonerName": "" }
      ]
    }
    """;

    /// <summary>/lol-chat/v1/friends, trimmed (live shape, Oct 2026; ids made up).</summary>
    public const string Friends = """
    [
      { "puuid": "friend-puuid", "summonerId": 1002, "gameName": "Friend", "gameTag": "EUNE", "availability": "chat", "id": "friend-puuid@eu2.pvp.net", "pid": "friend-puuid@eu2.pvp.net" },
      { "puuid": "no-id-puuid", "summonerId": 0, "gameName": "NoId", "gameTag": "EUW", "availability": "chat", "id": "no-id-puuid@eu2.pvp.net", "pid": "no-id-puuid@eu2.pvp.net" }
    ]
    """;
}
