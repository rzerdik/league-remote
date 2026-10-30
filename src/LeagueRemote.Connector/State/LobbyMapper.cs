using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>Pure mapping of <c>/lol-lobby/v2/lobby</c> (own party only) and the lobby rules the commands check.</summary>
internal static class LobbyMapper
{
    public static LobbyState? Map(
        JsonElement? lobbyJson,
        JsonElement? searchStateJson,
        JsonElement? searchJson,
        IReadOnlyDictionary<string, string>? playerNames = null)
    {
        if (lobbyJson is not { ValueKind: JsonValueKind.Object } lobby)
            return null;

        var isSearching = IsSearching(searchStateJson);
        double? timeInQueue = null, estimated = null;
        if (isSearching && searchJson is { ValueKind: JsonValueKind.Object } search)
        {
            timeInQueue = search.Dbl("timeInQueue");
            estimated = search.Dbl("estimatedQueueTime");
        }

        var local = lobby.Obj("localMember");
        var members = lobby.Arr("members")
            .Select(m => new LobbyMember(
                RiotId(m, playerNames),
                m.Bool("isLeader"),
                Text.NullIfBlank(m.Str("firstPositionPreference")),
                Text.NullIfBlank(m.Str("secondPositionPreference")),
                local is { } l && IsSameMember(m, l),
                m.Str("puuid") ?? ""))
            .ToList();

        var gameConfig = GameConfig(lobby);
        var invitations = InvitationMapper.MapSent(lobby, playerNames);
        return new LobbyState(
            gameConfig?.Int("queueId") ?? 0,
            local?.Bool("isLeader") ?? false,
            lobby.Bool("canStartActivity"),
            isSearching,
            timeInQueue,
            estimated,
            members,
            gameConfig is { } gc ? Text.NullIfBlank(gc.Str("gameMode")) : null,
            IsCustomLobby(lobby),
            HasPositionSelector(lobby),
            CanInvite(lobby),
            invitations.Count > 0 ? invitations : null);
    }

    /// <summary>
    /// Matchmaking is searching or has found a match (<c>/lol-lobby/v2/lobby/matchmaking/search-state</c>); the client's
    /// Cancel replaces its other lobby actions then.
    /// </summary>
    public static bool IsSearching(JsonElement? searchState) =>
        searchState is { ValueKind: JsonValueKind.Object } s && s.Str("searchState") is "Searching" or "Found";

    /// <summary>
    /// The local member may invite (<c>allowedInviteOthers</c>: the leader, or everyone once the leader opened invites) and
    /// the party has room. What the client checks before it offers Invite.
    /// </summary>
    public static bool CanInvite(JsonElement lobby) => lobby.Obj("localMember")?.Bool("allowedInviteOthers") == true && !IsPartyFull(lobby);

    /// <summary><c>gameConfig.isLobbyFull</c>, or as many members as <c>gameConfig.maxLobbySize</c> allows.</summary>
    public static bool IsPartyFull(JsonElement lobby)
    {
        if (GameConfig(lobby) is not { } gc)
            return false;
        var max = gc.Int("maxLobbySize");
        return gc.Bool("isLobbyFull") || (max > 0 && lobby.Arr("members").Count() >= max);
    }

    /// <summary>True when the player is already a member of <paramref name="lobby"/>.</summary>
    public static bool IsMember(JsonElement lobby, string puuid) =>
        lobby.Arr("members").Any(m => m.ValueKind == JsonValueKind.Object
                                      && string.Equals(m.Str("puuid"), puuid, StringComparison.OrdinalIgnoreCase));

    /// <summary>Draft-style queues where everyone picks two roles before queueing.</summary>
    private static readonly int[] PositionSelectorQueueIds = [400, 420, 440];

    /// <summary>
    /// True when the lobby has the two-role selector (Draft Pick, Ranked Solo/Duo, Ranked Flex). Uses the client's
    /// <c>gameConfig.showPositionSelector</c>; older builds without it fall back to the queue id.
    /// </summary>
    public static bool HasPositionSelector(JsonElement lobby)
    {
        if (GameConfig(lobby) is not { } gc || gc.Bool("isCustom"))
            return false;
        if (gc.TryGetProperty("showPositionSelector", out var shown) && shown.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return shown.GetBoolean();
        return PositionSelectorQueueIds.Contains(gc.Int("queueId"));
    }

    /// <summary>True for custom lobbies (incl. Practice Tool), which are started with custom/start-champ-select instead of matchmaking.</summary>
    public static bool IsCustomLobby(JsonElement lobby) => GameConfig(lobby) is { } gc && gc.Bool("isCustom");

    /// <summary>A custom lobby without its own queue (older clients): started with custom/start-champ-select, not matchmaking.</summary>
    public static bool IsLegacyCustomLobby(JsonElement lobby) => IsCustomLobby(lobby) && GameConfig(lobby)?.Int("queueId") is not > 0;

    /// <summary>Lobby members without a Riot ID in their JSON, plus the players the lobby invited (names are empty there).</summary>
    public static IEnumerable<PlayerRef> PlayersToResolve(JsonElement? lobbyJson)
    {
        if (lobbyJson is not { ValueKind: JsonValueKind.Object } lobby)
            return [];
        return lobby.Arr("members")
            .Where(m => m.ValueKind == JsonValueKind.Object && RiotIdNames.OwnRiotId(m) is null)
            .Select(PlayerRef.Of)
            .Concat(InvitationMapper.InviteesToResolve(lobby));
    }

    private static JsonElement? GameConfig(JsonElement lobby) => lobby.Obj("gameConfig");

    /// <summary>Matches a lobby member to <c>localMember</c> by puuid, or by summoner id when the puuid is missing.</summary>
    private static bool IsSameMember(JsonElement member, JsonElement local)
    {
        var puuid = local.Str("puuid");
        if (!string.IsNullOrEmpty(puuid))
            return member.Str("puuid") == puuid;
        var summonerId = local.Long("summonerId");
        return summonerId > 0 && member.Long("summonerId") == summonerId;
    }

    /// <summary>
    /// Riot ID from the member itself, else a name resolved via the summoner API, else the legacy summoner name. Current LCU
    /// builds often leave gameName (and the deprecated summonerName) empty in the lobby, hence the resolved names.
    /// </summary>
    private static string RiotId(JsonElement member, IReadOnlyDictionary<string, string>? playerNames) =>
        RiotIdNames.OwnRiotId(member)
        ?? RiotIdNames.Resolved(PlayerRef.Of(member), playerNames)
        ?? Text.NullIfBlank(member.Str("summonerName"))
        ?? "Unknown";
}
