namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Chat JSON as the League client sends it (shapes recorded from a live client, Oct 2026; names, ids and texts replaced).
/// The local player is <c>me-puuid</c>. Direct conversations, messages, participants and the lobby's chat fields are
/// recorded; the party room id format ({multiUserChatId}@lol-pre-game.{region}.pvp.net, type "customGame" in a custom
/// lobby) was seen live; the draft room's suffix is made up (matching only relies on the id starting with the session's
/// <c>multiUserChatId</c>).
/// </summary>
internal static class ChatSamples
{
    public const string PartyMuc = "cccc0001-0000-0000-0000-000000000001";
    public const string PartyId = PartyMuc + "@lol-pre-game.eu2.pvp.net";
    public const string DraftMuc = "c1~4f2b7e0a9d";
    public const string DraftId = DraftMuc + "@champ-select.eu2.pvp.net";
    public const string FriendId = "friend-puuid@eu2.pvp.net";
    public const string OldFriendId = "old-friend-puuid@eu1.pvp.net";

    /// <summary>The same paths escaped the way the connector (and the client, encodeURIComponent) sends them.</summary>
    public const string PartyPath = "/lol-chat/v1/conversations/" + PartyMuc + "%40lol-pre-game.eu2.pvp.net";
    public const string DraftPath = "/lol-chat/v1/conversations/" + DraftMuc + "%40champ-select.eu2.pvp.net";
    public const string FriendPath = "/lol-chat/v1/conversations/friend-puuid%40eu2.pvp.net";
    public const string OldFriendPath = "/lol-chat/v1/conversations/old-friend-puuid%40eu1.pvp.net";

    /// <summary>/lol-chat/v1/me (trimmed).</summary>
    public const string Me = """
    { "gameName": "Me", "gameTag": "EUNE", "id": "me-puuid@eu2.pvp.net", "pid": "me-puuid@eu2.pvp.net", "puuid": "me-puuid", "summonerId": 1001 }
    """;

    /// <summary>
    /// /lol-chat/v1/conversations: the party room, the draft room, a stale champ-select room of an earlier game, two DMs
    /// (the older one first, as the client lists them) and a club room League Remote ignores.
    /// </summary>
    public const string Conversations = """
    [
      { "gameName": "OldFriend", "gameTag": "EUW", "id": "old-friend-puuid@eu1.pvp.net", "pid": "old-friend-puuid@eu1.pvp.net",
        "isMuted": false, "type": "chat", "unreadMessageCount": 0, "targetRegion": "eu1", "name": "", "password": "", "inviterId": "",
        "mucJwtDto": { "channelClaim": "", "domain": "", "jwt": "", "targetRegion": "" },
        "lastMessage": { "body": "gg", "fromId": "old-friend-puuid@eu1.pvp.net", "fromPid": "old-friend-puuid@eu1.pvp.net", "fromPuuid": "old-friend-puuid",
          "fromSummonerId": 0, "id": "1790696129435:1", "isHistorical": true, "timestamp": "2026-09-29T15:35:29.383Z", "type": "chat" } },
      { "gameName": "Friend", "gameTag": "EUNE", "id": "friend-puuid@eu2.pvp.net", "pid": "friend-puuid@eu2.pvp.net",
        "isMuted": false, "type": "chat", "unreadMessageCount": 2, "targetRegion": "eu2", "name": "", "password": "", "inviterId": "",
        "mucJwtDto": { "channelClaim": "", "domain": "", "jwt": "", "targetRegion": "" },
        "lastMessage": { "body": "want to play?", "fromId": "friend-puuid@eu2.pvp.net", "fromPid": "friend-puuid@eu2.pvp.net", "fromPuuid": "friend-puuid",
          "fromSummonerId": 0, "id": "1792632130896:85", "isHistorical": true, "timestamp": "2026-10-22T01:22:12.690Z", "type": "chat" } },
      { "gameName": "", "gameTag": "", "id": "c1~0000000000@champ-select.eu2.pvp.net", "pid": "c1~0000000000@champ-select.eu2.pvp.net",
        "isMuted": false, "type": "championSelect", "unreadMessageCount": 0, "name": "", "password": "x", "inviterId": "", "targetRegion": "eu2" },
      { "gameName": "", "gameTag": "", "id": "cccc0001-0000-0000-0000-000000000001@lol-pre-game.eu2.pvp.net",
        "pid": "cccc0001-0000-0000-0000-000000000001@lol-pre-game.eu2.pvp.net", "isMuted": false, "type": "teamBuilder",
        "unreadMessageCount": 1, "name": "", "password": "x", "inviterId": "", "targetRegion": "eu2",
        "lastMessage": { "body": "inv me", "fromId": "partner-puuid", "fromPid": "partner-puuid@eu2.pvp.net", "fromPuuid": "partner-puuid",
          "fromSummonerId": 1002, "id": "m_0b1", "isHistorical": false, "timestamp": "2026-10-22T18:01:05.000Z", "type": "chat" } },
      { "gameName": "", "gameTag": "", "id": "c1~4f2b7e0a9d@champ-select.eu2.pvp.net", "pid": "c1~4f2b7e0a9d@champ-select.eu2.pvp.net",
        "isMuted": false, "type": "championSelect", "unreadMessageCount": 3, "name": "", "password": "x", "inviterId": "", "targetRegion": "eu2" },
      { "gameName": "", "gameTag": "", "id": "club-1@clubs.eu2.pvp.net", "isMuted": false, "type": "club", "unreadMessageCount": 9 }
    ]
    """;

    /// <summary>Trimmed /lol-lobby/v2/lobby of a two-player party (Draft Pick).</summary>
    public const string Lobby = """
    {
      "gameConfig": { "queueId": 400, "gameMode": "CLASSIC", "isCustom": false, "isTeamBuilderManaged": true },
      "localMember": { "puuid": "me-puuid", "summonerId": 1001, "isLeader": true },
      "members": [
        { "puuid": "me-puuid", "summonerId": 1001, "gameName": "", "isLeader": true },
        { "puuid": "partner-puuid", "summonerId": 1002, "gameName": "Partner", "gameTag": "EUNE", "isLeader": false }
      ],
      "multiUserChatId": "cccc0001-0000-0000-0000-000000000001",
      "multiUserChatPassword": "x",
      "mucJwtDto": { "channelClaim": "cccc0001-0000-0000-0000-000000000001", "domain": "lol-pre-game", "jwt": "jwt", "targetRegion": "eu2" },
      "partyId": "cccc0001-0000-0000-0000-000000000001"
    }
    """;

    /// <summary>/lol-champ-select/v1/session, only what chat needs.</summary>
    public const string ChampSelectSession = """
    {
      "localPlayerCellId": 2,
      "chatDetails": { "multiUserChatId": "c1~4f2b7e0a9d", "multiUserChatPassword": "x",
        "mucJwtDto": { "channelClaim": "", "domain": "champ-select", "jwt": "jwt", "targetRegion": "eu2" } },
      "myTeam": [], "theirTeam": [], "actions": []
    }
    """;

    /// <summary>
    /// /lol-champ-select/v1/session of an anonymous (ranked) draft, trimmed to what chat reads. Shape from the client's
    /// <c>/help</c> (<c>ChampSelectPlayerSelection</c>: <c>puuid</c>, <c>summonerId</c>, <c>gameName</c>,
    /// <c>nameVisibilityType</c>, <c>obfuscatedPuuid</c>, <c>obfuscatedSummonerId</c>), not yet recorded in a live ranked
    /// draft: the local player (cell 2) and a teammate League names (cell 1) are visible; cells 3 and 4 are hidden, with
    /// only obfuscated ids. Ids and names are placeholders.
    /// </summary>
    public const string AnonymousChampSelectSession = """
    {
      "localPlayerCellId": 2,
      "chatDetails": { "multiUserChatId": "c1~4f2b7e0a9d", "multiUserChatPassword": "x",
        "mucJwtDto": { "channelClaim": "", "domain": "champ-select", "jwt": "jwt", "targetRegion": "eu2" } },
      "myTeam": [
        { "cellId": 1, "puuid": "ally-puuid", "summonerId": 1002, "gameName": "Ally", "tagLine": "EUW", "nameVisibilityType": "VISIBLE",
          "obfuscatedPuuid": "", "obfuscatedSummonerId": 0, "assignedPosition": "top", "championId": 0, "championPickIntent": 0 },
        { "cellId": 2, "puuid": "me-puuid", "summonerId": 1001, "gameName": "Me", "tagLine": "EUNE", "nameVisibilityType": "VISIBLE",
          "obfuscatedPuuid": "", "obfuscatedSummonerId": 0, "assignedPosition": "bottom", "championId": 0, "championPickIntent": 0 },
        { "cellId": 3, "puuid": "", "summonerId": 0, "gameName": "", "tagLine": "", "nameVisibilityType": "HIDDEN",
          "obfuscatedPuuid": "obf-1", "obfuscatedSummonerId": 7001, "assignedPosition": "middle", "championId": 0, "championPickIntent": 103 },
        { "cellId": 4, "puuid": "", "summonerId": 0, "gameName": "Leaked", "tagLine": "", "nameVisibilityType": "HIDDEN",
          "obfuscatedPuuid": "obf-2", "obfuscatedSummonerId": 7002, "assignedPosition": "", "championId": 0, "championPickIntent": 0 }
      ],
      "theirTeam": [ { "cellId": 7, "puuid": "enemy-puuid", "summonerId": 9007, "gameName": "Enemy", "obfuscatedPuuid": "obf-enemy" } ],
      "actions": []
    }
    """;

    /// <summary>
    /// Draft room of the anonymous draft above (message fields as listed in <c>/help</c>,
    /// <c>LolChatConversationMessageResource</c>): my line, the named teammate's, then hidden teammates known only by their
    /// obfuscated puuid (cell 3) or obfuscated summoner id (cell 4), and a sender not on the team (an enemy id never maps).
    /// </summary>
    public const string AnonymousDraftMessages = """
    [
      { "body": "adc", "fromId": "me-puuid", "fromPid": "me-puuid@eu2.pvp.net", "fromPuuid": "me-puuid", "fromSummonerId": 1001,
        "fromObfuscatedPuuid": "", "fromObfuscatedSummonerId": 0, "id": "a_01", "timestamp": "2026-10-22T18:05:00.000Z", "type": "groupchat" },
      { "body": "top", "fromId": "ally-puuid", "fromPid": "ally-puuid@eu2.pvp.net", "fromPuuid": "ally-puuid", "fromSummonerId": 1002,
        "fromObfuscatedPuuid": "", "fromObfuscatedSummonerId": 0, "id": "a_02", "timestamp": "2026-10-22T18:05:05.000Z", "type": "groupchat" },
      { "body": "mid", "fromId": "", "fromPid": "", "fromPuuid": "", "fromSummonerId": 0,
        "fromObfuscatedPuuid": "obf-1", "fromObfuscatedSummonerId": 0, "id": "a_03", "timestamp": "2026-10-22T18:05:10.000Z", "type": "groupchat" },
      { "body": "can I swap?", "fromId": "", "fromPid": "", "fromPuuid": "", "fromSummonerId": 0,
        "fromObfuscatedPuuid": "", "fromObfuscatedSummonerId": 7002, "id": "a_04", "timestamp": "2026-10-22T18:05:20.000Z", "type": "groupchat" },
      { "body": "hi", "fromId": "", "fromPid": "", "fromPuuid": "", "fromSummonerId": 0,
        "fromObfuscatedPuuid": "obf-enemy", "fromObfuscatedSummonerId": 0, "id": "a_05", "timestamp": "2026-10-22T18:05:30.000Z", "type": "groupchat" }
    ]
    """;

    /// <summary>
    /// Party room history: both joined, a message each way, the partner left. As recorded live: room lines are type
    /// "groupchat", <c>fromId</c> comes with or without the domain.
    /// </summary>
    public const string PartyMessages = """
    [
      { "body": "joined_room", "fromId": "me-puuid", "fromPid": "me-puuid@eu2.pvp.net", "fromPuuid": "me-puuid", "fromSummonerId": 1001,
        "id": "m_01", "isHistorical": false, "timestamp": "2026-10-22T18:00:00.000Z", "type": "system" },
      { "body": "joined_room", "fromId": "partner-puuid", "fromPid": "partner-puuid@eu2.pvp.net", "fromPuuid": "partner-puuid", "fromSummonerId": 1002,
        "id": "m_02", "isHistorical": false, "timestamp": "2026-10-22T18:00:01.000Z", "type": "system" },
      { "body": "hey", "fromId": "me-puuid@eu2.pvp.net", "fromPid": "me-puuid@eu2.pvp.net", "fromPuuid": "me-puuid", "fromSummonerId": 1001,
        "id": "m_03", "isHistorical": false, "timestamp": "2026-10-22T18:00:30.000Z", "type": "groupchat" },
      { "body": "inv me", "fromId": "partner-puuid", "fromPid": "partner-puuid@eu2.pvp.net", "fromPuuid": "partner-puuid", "fromSummonerId": 1002,
        "id": "m_0b1", "isHistorical": false, "timestamp": "2026-10-22T18:01:05.000Z", "type": "groupchat" },
      { "body": "left_room", "fromId": "partner-puuid", "fromPid": "partner-puuid@eu2.pvp.net", "fromPuuid": "partner-puuid", "fromSummonerId": 1002,
        "id": "m_05", "isHistorical": false, "timestamp": "2026-10-22T18:01:30.000Z", "type": "system" }
    ]
    """;

    /// <summary>/lol-chat/v1/conversations/{party}/participants (trimmed).</summary>
    public const string PartyParticipants = """
    [
      { "gameName": "Me", "gameTag": "EUNE", "id": "me-puuid@eu2.pvp.net", "puuid": "me-puuid", "summonerId": 1001 },
      { "gameName": "Partner", "gameTag": "EUNE", "id": "partner-puuid@eu2.pvp.net", "puuid": "partner-puuid", "summonerId": 1002 }
    ]
    """;

    /// <summary>
    /// Draft room: joins (not shown in drafts), a celebration line (dropped), a teammate League names and one it hides
    /// (anonymous ranked draft: no puuid, obfuscated id only).
    /// </summary>
    public const string DraftMessages = """
    [
      { "body": "joined_room", "fromId": "ally-puuid", "fromPid": "ally-puuid@eu2.pvp.net", "fromPuuid": "ally-puuid", "fromSummonerId": 0,
        "id": "d_01", "isHistorical": false, "timestamp": "2026-10-22T18:05:00.000Z", "type": "system" },
      { "body": "Partner bought a boost", "fromId": "", "fromPid": "", "fromPuuid": "", "fromSummonerId": 0,
        "id": "d_02", "isHistorical": false, "timestamp": "2026-10-22T18:05:01.000Z", "type": "celebration" },
      { "body": "mid", "fromId": "ally-puuid", "fromPid": "ally-puuid@eu2.pvp.net", "fromPuuid": "ally-puuid", "fromSummonerId": 0,
        "id": "d_03", "isHistorical": false, "timestamp": "2026-10-22T18:05:10.000Z", "type": "groupchat" },
      { "body": "can I swap?", "fromId": "", "fromPid": "", "fromPuuid": "", "fromObfuscatedPuuid": "obf-1", "fromSummonerId": 0,
        "id": "d_04", "isHistorical": false, "timestamp": "2026-10-22T18:05:20.000Z", "type": "groupchat" }
    ]
    """;

    public const string DraftParticipants = """
    [ { "gameName": "Ally", "gameTag": "EUW", "id": "ally-puuid@eu2.pvp.net", "puuid": "ally-puuid" },
      { "gameName": "", "gameTag": "", "id": "", "puuid": "", "obfuscatedPuuid": "obf-1" } ]
    """;

    public const string FriendMessages = """
    [
      { "body": "hi", "fromId": "me-puuid", "fromPid": "me-puuid@eu2.pvp.net", "fromPuuid": "me-puuid", "fromSummonerId": 1001,
        "id": "1792632120000:84", "isHistorical": true, "timestamp": "2026-10-22T01:20:00.000Z", "type": "chat" },
      { "body": "want to play?", "fromId": "friend-puuid", "fromPid": "friend-puuid@eu2.pvp.net", "fromPuuid": "friend-puuid", "fromSummonerId": 0,
        "id": "1792632130896:85", "isHistorical": true, "timestamp": "2026-10-22T01:22:12.690Z", "type": "chat" },
      { "body": "joined_room", "fromId": "me-puuid", "fromPid": "me-puuid@eu2.pvp.net", "fromPuuid": "me-puuid", "fromSummonerId": 1001,
        "id": "m_ca7f", "isHistorical": false, "timestamp": "2026-10-22T13:22:28.088Z", "type": "system" }
    ]
    """;

    public const string OldFriendMessages = """
    [ { "body": "gg", "fromId": "old-friend-puuid", "fromPid": "old-friend-puuid@eu1.pvp.net", "fromPuuid": "old-friend-puuid",
        "id": "1790696129435:1", "isHistorical": true, "timestamp": "2026-09-29T15:35:29.383Z", "type": "chat" } ]
    """;

    /// <summary>
    /// A direct conversation as it looks once opened but never written in (recorded live): <c>lastMessage</c> is null and
    /// its history only holds the room's <c>joined_room</c> lines (type "system", <c>fromId</c> without the domain).
    /// </summary>
    public const string OpenedConversation = """
    { "gameName": "Quiet", "gameTag": "EUNE", "id": "quiet-puuid@eu2.pvp.net", "pid": "quiet-puuid@eu2.pvp.net",
      "isMuted": false, "type": "chat", "unreadMessageCount": 0, "targetRegion": "eu2", "name": "", "password": "", "inviterId": "",
      "mucJwtDto": { "channelClaim": "", "domain": "", "jwt": "", "targetRegion": "" }, "lastMessage": null }
    """;

    public const string QuietId = "quiet-puuid@eu2.pvp.net";
    public const string QuietPath = "/lol-chat/v1/conversations/quiet-puuid%40eu2.pvp.net";

    public const string OpenedMessages = """
    [
      { "body": "joined_room", "fromId": "me-puuid", "fromObfuscatedPuuid": "", "fromObfuscatedSummonerId": 0, "fromPid": "me-puuid@eu2.pvp.net",
        "fromPuuid": "me-puuid", "fromSummonerId": 1001, "id": "m_cd8aef4613f41514", "isHistorical": false,
        "timestamp": "2026-10-22T13:22:28.086Z", "type": "system" },
      { "body": "joined_room", "fromId": "quiet-puuid", "fromObfuscatedPuuid": "", "fromObfuscatedSummonerId": 0, "fromPid": "quiet-puuid@eu2.pvp.net",
        "fromPuuid": "quiet-puuid", "fromSummonerId": 1003, "id": "m_a67af4ef1817094f", "isHistorical": false,
        "timestamp": "2026-10-22T13:22:28.087Z", "type": "system" }
    ]
    """;

    /// <summary>
    /// /lol-chat/v1/friends (recorded, trimmed to what League Remote reads plus a few neighbours): <c>id</c> and <c>pid</c> are
    /// the friend's chat id, the same as the direct conversation's id.
    /// </summary>
    public const string Friends = """
    [
      { "availability": "chat", "gameName": "Friend", "gameTag": "EUNE", "groupId": 0, "groupName": "**Default", "icon": 1001,
        "id": "friend-puuid@eu2.pvp.net", "pid": "friend-puuid@eu2.pvp.net", "puuid": "friend-puuid", "isP2PConversationMuted": false,
        "lastSeenOnlineTimestamp": null, "lol": {}, "name": "", "note": "", "statusMessage": "", "summonerId": 1004, "time": 1792675347460 },
      { "availability": "offline", "gameName": "Newbie", "gameTag": "EUNE", "groupId": 0, "groupName": "**Default", "icon": 29,
        "id": "new-friend-puuid@eu2.pvp.net", "pid": "new-friend-puuid@eu2.pvp.net", "puuid": "new-friend-puuid", "isP2PConversationMuted": false,
        "lastSeenOnlineTimestamp": null, "lol": {}, "name": "", "note": "", "statusMessage": "", "summonerId": 1005, "time": 1792675347460 }
    ]
    """;

    /// <summary>A fake LCU with the whole chat above.</summary>
    public static FakeLcu Handler()
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-chat/v1/me"] = Me;
        handler.Routes["/lol-chat/v1/conversations"] = Conversations;
        handler.Routes["/lol-lobby/v2/lobby"] = Lobby;
        handler.Routes["/lol-champ-select/v1/session"] = ChampSelectSession;
        handler.Routes[PartyPath + "/messages"] = PartyMessages;
        handler.Routes[PartyPath + "/participants"] = PartyParticipants;
        handler.Routes[DraftPath + "/messages"] = DraftMessages;
        handler.Routes[DraftPath + "/participants"] = DraftParticipants;
        handler.Routes[FriendPath + "/messages"] = FriendMessages;
        handler.Routes[OldFriendPath + "/messages"] = OldFriendMessages;
        handler.Routes["/lol-chat/v1/friends"] = Friends;
        return handler;
    }
}
