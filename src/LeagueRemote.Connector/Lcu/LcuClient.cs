using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Lcu;

/// <summary>What a champ-select swap request does: <see cref="LcuClient.SwapAsync"/>.</summary>
internal enum SwapVerb { Request, Accept, Decline, Cancel }

/// <summary>
/// Typed access to the whitelisted LCU endpoints only. This is deliberately not a proxy:
/// there is no public method that sends an arbitrary request.
/// GETs return the raw JSON (or null on 404, which the LCU uses for "not in that phase").
/// </summary>
internal sealed class LcuClient(HttpClient http)
{
    // --- Gameflow ---------------------------------------------------------------------------

    public async Task<string> GetGameflowPhaseAsync(CancellationToken ct) =>
        (await GetAsync("/lol-gameflow/v1/gameflow-phase", ct)) is { ValueKind: JsonValueKind.String } phase
            ? phase.GetString() ?? "None"
            : "None";

    /// <summary>
    /// The current game flow session (<c>GetLolGameflowV1Session</c>). Read-only; only <c>gameData.queue</c> (id, map, mode)
    /// is used, on the post-game screen: its players' names are never read.
    /// </summary>
    public Task<JsonElement?> GetGameflowSessionAsync(CancellationToken ct) => GetAsync("/lol-gameflow/v1/session", ct);

    /// <summary>
    /// <c>GetLolGameflowV1GameflowMetadataPlayerStatus</c>: <c>lastQueuedLobbyStatus.allowedPlayAgain</c> is what the client's
    /// post-game enables its Play Again with (rcp-fe-lol-postgame, patch 16.21). Read-only; verified live (Oct 2026).
    /// </summary>
    public Task<JsonElement?> GetGameflowPlayerStatusAsync(CancellationToken ct) =>
        GetAsync("/lol-gameflow/v1/gameflow-metadata/player-status", ct);

    // --- End of game ------------------------------------------------------------------------
    // Verified in the client's /help (Oct 2026). Outside the post-game screen the GETs answer 404 ("No end of game stats
    // available.") or, for the LP notification, 204; their shapes on the post-game screen are not recorded yet.

    /// <summary>The post-game scoreboard (<c>GetLolEndOfGameV1EogStatsBlock</c>, LolEndOfGameEndOfGameStats). Read-only.</summary>
    public Task<JsonElement?> GetEndOfGameStatsAsync(CancellationToken ct) => GetAsync("/lol-end-of-game/v1/eog-stats-block", ct);

    /// <summary>The local player's mastery from the last game (<c>GetLolEndOfGameV1ChampionMasteryUpdates</c>). Read-only.</summary>
    public Task<JsonElement?> GetChampionMasteryUpdatesAsync(CancellationToken ct) =>
        GetAsync("/lol-end-of-game/v1/champion-mastery-updates", ct);

    /// <summary>The last ranked game's LP change (<c>GetLolRankedV1CurrentLpChangeNotification</c>); null (204) when none. Read-only.</summary>
    public Task<JsonElement?> GetLpChangeNotificationAsync(CancellationToken ct) =>
        GetAsync("/lol-ranked/v1/current-lp-change-notification", ct);

    /// <summary>
    /// The post-game screen's Play Again (<c>PostLolLobbyV2PlayAgain</c>, no body): what rcp-fe-lol-postgame's
    /// <c>parties.playAgain</c> sends (patch 16.21). The client then rebuilds the lobby of the game with its party.
    /// </summary>
    public Task PlayAgainAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-lobby/v2/play-again", null, ct);

    /// <summary>
    /// Closes the post-game screen (<c>PostLolEndOfGameV1StateDismissStats</c>, no body): what the League scoreboard's
    /// <c>exitPostgame</c> / <c>_leavePostgame</c> send in rcp-fe-lol-postgame (patch 16.21). Only TFT's Go Home also posts
    /// <c>/lol-lobby/v2/play-again-decline</c>; League's close button doesn't, so neither do we.
    /// </summary>
    public Task DismissStatsAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-end-of-game/v1/state/dismiss-stats", null, ct);

    // --- Ready check ------------------------------------------------------------------------

    public Task<JsonElement?> GetReadyCheckAsync(CancellationToken ct) => GetAsync("/lol-matchmaking/v1/ready-check", ct);

    public Task AcceptReadyCheckAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-matchmaking/v1/ready-check/accept", null, ct);

    public Task DeclineReadyCheckAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-matchmaking/v1/ready-check/decline", null, ct);

    /// <summary>Read-only; only used for time-in-queue / estimated queue time.</summary>
    public Task<JsonElement?> GetMatchmakingSearchAsync(CancellationToken ct) => GetAsync("/lol-matchmaking/v1/search", ct);

    // --- Lobby ------------------------------------------------------------------------------

    public Task<JsonElement?> GetLobbyAsync(CancellationToken ct) => GetAsync("/lol-lobby/v2/lobby", ct);

    public Task CreateLobbyAsync(int queueId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-lobby/v2/lobby", JsonContent.Create(new { queueId }), ct);

    /// <summary>
    /// Creates a private Practice Tool lobby: what the client's own Practice Tool button sends (patch 16.21,
    /// <c>_generatePracticeToolPayload</c> in rcp-fe-lol-parties). A bare <c>{ queueId }</c> is rejected with 400.
    /// </summary>
    public Task CreatePracticeToolLobbyAsync(int queueId, string lobbyName, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-lobby/v2/lobby", JsonContent.Create(new
        {
            queueId,
            customGameLobby = new
            {
                configuration = new
                {
                    gameMode = "PRACTICETOOL",
                    gameMutator = "",
                    gameServerRegion = "",
                    mapId = 11,
                    mutators = new { id = 1 },
                    spectatorDelayEnabled = false,
                    spectatorPolicy = "AllAllowed",
                    teamSize = 5,
                    hidePublicly = true,
                },
                lobbyName,
                hidePublicly = true,
                lobbyPassword = (string?)null,
            },
        }), ct);

    /// <summary>Every queue the client knows, including the custom-game ones (e.g. 3140 Practice Tool). Read-only.</summary>
    public Task<JsonElement?> GetQueuesAsync(CancellationToken ct) => GetAsync("/lol-game-queues/v1/queues", ct);

    /// <summary>
    /// Legacy custom lobbies (no queue id) have no matchmaking: the leader starts champ select directly. Current
    /// clients give custom games their own queues, which start through matchmaking like any other queue.
    /// </summary>
    public Task StartCustomGameAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-lobby/v1/lobby/custom/start-champ-select", null, ct);

    public Task<JsonElement?> GetLobbySearchStateAsync(CancellationToken ct) => GetAsync("/lol-lobby/v2/lobby/matchmaking/search-state", ct);

    public Task StartMatchmakingAsync(CancellationToken ct) => SendAsync(HttpMethod.Post, "/lol-lobby/v2/lobby/matchmaking/search", null, ct);

    public Task CancelMatchmakingAsync(CancellationToken ct) => SendAsync(HttpMethod.Delete, "/lol-lobby/v2/lobby/matchmaking/search", null, ct);

    /// <summary>Leaves the lobby (custom and Practice Tool lobbies too): what the client's ✕ next to Find Match sends.</summary>
    public Task LeaveLobbyAsync(CancellationToken ct) => SendAsync(HttpMethod.Delete, "/lol-lobby/v2/lobby", null, ct);

    /// <summary>Sets the local player's two role preferences (what the lobby's position selector sends).</summary>
    public Task SetPositionPreferencesAsync(string firstPreference, string secondPreference, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Put,
            "/lol-lobby/v2/lobby/members/localMember/position-preferences",
            JsonContent.Create(new { firstPreference, secondPreference }),
            ct);

    // --- Invitations --------------------------------------------------------------------------
    // Verified in the client's /help (Oct 2026) and against rcp-fe-lol-parties, which sends the same calls.

    /// <summary>Invitations the local player received (LolLobbyReceivedInvitationDto[]). Read-only.</summary>
    public Task<JsonElement?> GetReceivedInvitationsAsync(CancellationToken ct) => GetAsync("/lol-lobby/v2/received-invitations", ct);

    /// <summary>Joins the inviting party (the client's Accept).</summary>
    public Task AcceptInvitationAsync(string invitationId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/lol-lobby/v2/received-invitations/{Uri.EscapeDataString(invitationId)}/accept", null, ct);

    public Task DeclineInvitationAsync(string invitationId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/lol-lobby/v2/received-invitations/{Uri.EscapeDataString(invitationId)}/decline", null, ct);

    /// <summary>Invites one player to the current lobby: <c>[{ toSummonerId }]</c>, what the client's <c>invitePlayer</c> sends.</summary>
    public Task SendInvitationAsync(long toSummonerId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-lobby/v2/lobby/invitations", JsonContent.Create(new[] { new { toSummonerId } }), ct);

    // --- Summoners (read-only; used to resolve player names) --------------------------------

    public Task<JsonElement?> GetCurrentSummonerAsync(CancellationToken ct) => GetAsync("/lol-summoner/v1/current-summoner", ct);

    public Task<JsonElement?> GetSummonerByPuuidAsync(string puuid, CancellationToken ct) =>
        GetAsync($"/lol-summoner/v2/summoners/puuid/{Uri.EscapeDataString(puuid)}", ct);

    public Task<JsonElement?> GetSummonerByIdAsync(long summonerId, CancellationToken ct) =>
        GetAsync($"/lol-summoner/v1/summoners/{summonerId}", ct);

    // --- Champ select -----------------------------------------------------------------------

    public Task<JsonElement?> GetChampSelectSessionAsync(CancellationToken ct) => GetAsync("/lol-champ-select/v1/session", ct);

    public Task PatchActionChampionAsync(long actionId, int championId, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, $"/lol-champ-select/v1/session/actions/{actionId}", JsonContent.Create(new { championId }), ct);

    /// <summary>Hover + lock in one call: PATCH the action with <c>completed: true</c> (what the client itself does).</summary>
    public Task LockActionAsync(long actionId, int championId, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, $"/lol-champ-select/v1/session/actions/{actionId}", JsonContent.Create(new { championId, completed = true }), ct);

    public Task CompleteActionAsync(long actionId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/lol-champ-select/v1/session/actions/{actionId}/complete", null, ct);

    public Task SetSummonerSpellsAsync(int spell1Id, int spell2Id, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, "/lol-champ-select/v1/session/my-selection", JsonContent.Create(new { spell1Id, spell2Id }), ct);

    /// <summary>
    /// The skin carousel for the local player's champion (<c>GetLolChampSelectV1SkinCarouselSkins</c>, an array of
    /// <c>TeamBuilderDirect-SkinSelectorSkin</c>: <c>id</c>, <c>name</c>, <c>championId</c>, <c>isBase</c>, <c>disabled</c>,
    /// <c>unlocked</c>, <c>ownership</c>, <c>childSkins</c> with <c>colors</c>, …); <c>[]</c> outside champ select. Read-only.
    /// </summary>
    public Task<JsonElement?> GetSkinCarouselAsync(CancellationToken ct) => GetAsync("/lol-champ-select/v1/skin-carousel-skins", ct);

    /// <summary>
    /// Equips a skin or chroma: what the client's skin carousel sends (rcp-fe-lol-champ-select <c>requestSetSkin</c>, patch
    /// 16.21: <c>PATCH /lol-champ-select/v1/session/my-selection { selectedSkinId }</c>, only for an <c>unlocked</c> skin).
    /// </summary>
    public Task SelectSkinAsync(int selectedSkinId, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, "/lol-champ-select/v1/session/my-selection", JsonContent.Create(new { selectedSkinId }), ct);

    public Task<int[]> GetPickableChampionIdsAsync(CancellationToken ct) => GetIdsAsync("/lol-champ-select/v1/pickable-champion-ids", ct);

    public Task<int[]> GetBannableChampionIdsAsync(CancellationToken ct) => GetIdsAsync("/lol-champ-select/v1/bannable-champion-ids", ct);

    /// <summary>
    /// Every champion in the game (read-only game data: <c>id</c>, localized <c>name</c>, internal <c>alias</c> such as
    /// "MonkeyKing" for Wukong, …; includes a "None" entry with id -1). Parsed by <see cref="State.ChampionCatalog"/>.
    /// </summary>
    public Task<JsonElement?> GetChampionSummaryAsync(CancellationToken ct) => GetAsync("/lol-game-data/assets/v1/champion-summary.json", ct);

    /// <summary>
    /// The lanes League recommends per champion: <c>{ "103": { "recommendedPositions": ["MIDDLE"] }, … }</c>. Mapped by
    /// <see cref="State.ChampionLanesMapper"/>.
    /// </summary>
    public Task<JsonElement?> GetRecommendedChampionPositionsAsync(CancellationToken ct) => GetAsync("/lol-perks/v1/recommended-champion-positions", ct);

    /// <summary>
    /// <c>POST /lol-champ-select/v1/session/{pick-order-swaps|position-swaps|trades}/{id}/{request|accept|decline|cancel}</c>.
    /// </summary>
    public Task SwapAsync(SwapKind kind, long id, SwapVerb verb, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/lol-champ-select/v1/session/{SwapSegment(kind)}/{id}/{SwapVerbSegment(verb)}", null, ct);

    private static string SwapSegment(SwapKind kind) => kind switch
    {
        SwapKind.PickOrder => "pick-order-swaps",
        SwapKind.Position => "position-swaps",
        SwapKind.Trade => "trades",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string SwapVerbSegment(SwapVerb verb) => verb switch
    {
        SwapVerb.Request => "request",
        SwapVerb.Accept => "accept",
        SwapVerb.Decline => "decline",
        SwapVerb.Cancel => "cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, null),
    };

    /// <summary>
    /// <c>POST /lol-champ-select/v1/session/bench/swap/{championId}</c> (<c>PostLolChampSelectV1SessionBenchSwapByChampionId</c>),
    /// no body: what the client's bench sends when a champion on it is clicked (<c>championClicked</c> in the
    /// champion-bench component of rcp-fe-lol-champ-select, patch 16.21). The local player's champion goes to the bench.
    /// </summary>
    public Task BenchSwapAsync(int championId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/lol-champ-select/v1/session/bench/swap/{championId}", null, ct);

    /// <summary>
    /// <c>POST /lol-champ-select/v1/session/my-selection/reroll</c> (<c>PostLolChampSelectV1SessionMySelectionReroll</c>), no
    /// body: what the client's Reroll button sends (<c>_reroll</c> in the reroll-button component, patch 16.21).
    /// </summary>
    public Task RerollAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-champ-select/v1/session/my-selection/reroll", null, ct);

    /// <summary>
    /// <c>POST /lol-lobby-team-builder/champ-select/v1/session/quit</c> (<c>PostLolLobbyTeamBuilderChampSelectV1SessionQuit</c>,
    /// no arguments, in the client's <c>/help</c> as of Oct 2026, patch 16.21): the local player quits the matchmade champ
    /// select, a dodge with League's penalties. No body.
    /// </summary>
    public Task QuitChampSelectAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-lobby-team-builder/champ-select/v1/session/quit", null, ct);

    // --- Runes ------------------------------------------------------------------------------

    public Task<JsonElement?> GetRunePagesAsync(CancellationToken ct) => GetAsync("/lol-perks/v1/pages", ct);

    /// <summary>Body is the bare page id (JSON number).</summary>
    public Task SetCurrentRunePageAsync(long pageId, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, "/lol-perks/v1/currentpage", JsonContent.Create(pageId), ct);

    // --- Chat ---------------------------------------------------------------------------------
    // Conversation ids contain "@" (e.g. "{puuid}@eu2.pvp.net"); the client escapes them with encodeURIComponent.

    /// <summary>The local player's chat identity (<c>puuid</c>, <c>gameName</c>). Read-only.</summary>
    public Task<JsonElement?> GetChatMeAsync(CancellationToken ct) => GetAsync("/lol-chat/v1/me", ct);

    public Task<JsonElement?> GetConversationsAsync(CancellationToken ct) => GetAsync("/lol-chat/v1/conversations", ct);

    public Task<JsonElement?> GetConversationMessagesAsync(string conversationId, CancellationToken ct) =>
        GetAsync($"/lol-chat/v1/conversations/{Uri.EscapeDataString(conversationId)}/messages", ct);

    /// <summary>Who is in a room (party / draft), with their Riot ID game names as the client shows them.</summary>
    public Task<JsonElement?> GetConversationParticipantsAsync(string conversationId, CancellationToken ct) =>
        GetAsync($"/lol-chat/v1/conversations/{Uri.EscapeDataString(conversationId)}/participants", ct);

    /// <summary>What the client's chat input sends (rcp-fe-lol-social <c>sendMessage</c>).</summary>
    public Task SendChatMessageAsync(string conversationId, string body, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Post,
            $"/lol-chat/v1/conversations/{Uri.EscapeDataString(conversationId)}/messages",
            JsonContent.Create(new { body, type = "chat" }),
            ct);

    /// <summary>
    /// Opens a direct conversation with a friend, as the client's "Message" does (rcp-fe-lol-social <c>createConversation</c>:
    /// <c>{ id: friend.id, type: "chat" }</c>). Sends nothing to the friend.
    /// </summary>
    public Task CreateDirectConversationAsync(string friendChatId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-chat/v1/conversations", JsonContent.Create(new { id = friendChatId, type = "chat" }), ct);

    /// <summary>The conversation open in the client's chat window, if any (404 when none).</summary>
    public Task<JsonElement?> GetActiveConversationAsync(CancellationToken ct) => GetAsync("/lol-chat/v1/conversations/active", ct);

    /// <summary>Opening a conversation is how the client marks it read (rcp-fe-lol-social <c>setActive</c>).</summary>
    public Task SetActiveConversationAsync(string conversationId, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, "/lol-chat/v1/conversations/active", JsonContent.Create(new { id = conversationId }), ct);

    public Task ClearActiveConversationAsync(CancellationToken ct) => SendAsync(HttpMethod.Delete, "/lol-chat/v1/conversations/active", null, ct);

    // --- Friends ------------------------------------------------------------------------------
    // Friend ids are chat ids ("{puuid}@eu2.pvp.net"), escaped like conversation ids.

    /// <summary>Every friend with presence (<c>availability</c>, <c>lol</c> map). Read-only.</summary>
    public Task<JsonElement?> GetFriendsAsync(CancellationToken ct) => GetAsync("/lol-chat/v1/friends", ct);

    /// <summary>The user's friend groups (<c>**Default</c> plus their own). Read-only.</summary>
    public Task<JsonElement?> GetFriendGroupsAsync(CancellationToken ct) => GetAsync("/lol-chat/v1/friend-groups", ct);

    /// <summary>Removes a friend (<c>DeleteLolChatV1FriendsById</c> in the client's <c>/help</c>).</summary>
    public Task RemoveFriendAsync(string friendId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"/lol-chat/v1/friends/{Uri.EscapeDataString(friendId)}", null, ct);

    // --- Friend requests ----------------------------------------------------------------------
    // What the client's own social plugin sends (rcp-fe-lol-social friendRequests service, patch 16.21). Requests are keyed
    // by puuid; /lol-chat/v1/friend-requests is gone (404).

    public Task<JsonElement?> GetFriendRequestsAsync(CancellationToken ct) => GetAsync("/lol-chat/v2/friend-requests", ct);

    /// <summary>Add Friend by Riot ID (the client's Add Friend box posts <c>{ gameName, tagLine }</c>).</summary>
    public Task SendFriendRequestAsync(string gameName, string tagLine, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "/lol-chat/v2/friend-requests", JsonContent.Create(new { gameName, tagLine }), ct);

    /// <summary>Accepting is a PUT of <c>{ direction: "both" }</c> on the request.</summary>
    public Task AcceptFriendRequestAsync(string puuid, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"/lol-chat/v2/friend-requests/{Uri.EscapeDataString(puuid)}", JsonContent.Create(new { direction = "both" }), ct);

    /// <summary>Declines an incoming request or withdraws an outgoing one (the client uses the same DELETE for both).</summary>
    public Task DeleteFriendRequestAsync(string puuid, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"/lol-chat/v2/friend-requests/{Uri.EscapeDataString(puuid)}", null, ct);

    // --- Profile (read-only) -----------------------------------------------------------------
    // Verified read-only against a live client, Oct 2026 (patch 16.21); the local player's summoner is
    // GetCurrentSummonerAsync above. Never the mastery POST endpoints.

    /// <summary>The local player's ranked queues (<c>queues[]</c>, League and TFT).</summary>
    public Task<JsonElement?> GetCurrentRankedStatsAsync(CancellationToken ct) => GetAsync("/lol-ranked/v1/current-ranked-stats", ct);

    /// <summary>
    /// The local player's newest games (<c>games.games[]</c>, only their own participant each). The client ignores
    /// <c>begIndex</c> / <c>endIndex</c> and always answers the newest 21, so none are sent.
    /// </summary>
    public Task<JsonElement?> GetMatchHistoryAsync(CancellationToken ct) =>
        GetAsync("/lol-match-history/v1/products/lol/current-summoner/matches", ct);

    /// <summary>One finished game with all participants (<c>GetLolMatchHistoryV1GamesByGameId</c>, uint64 id).</summary>
    public Task<JsonElement?> GetMatchAsync(long gameId, CancellationToken ct) =>
        GetAsync($"/lol-match-history/v1/games/{(ulong)gameId}", ct);

    /// <summary>Every champion the local player has mastery on, sorted by points.</summary>
    public Task<JsonElement?> GetChampionMasteryAsync(CancellationToken ct) =>
        GetAsync("/lol-champion-mastery/v1/local-player/champion-mastery", ct);

    /// <summary>The local player's total mastery score (a bare number).</summary>
    public Task<JsonElement?> GetChampionMasteryScoreAsync(CancellationToken ct) =>
        GetAsync("/lol-champion-mastery/v1/local-player/champion-mastery-score", ct);

    // Other players (friends, party, own team, teammates named in a game, a searched Riot ID; the executor checks who before
    // calling these). Verified read-only against a live client, Oct 23 2026 (patch 16.21), with the local player's and a
    // friend's puuid. Their summoner is GetSummonerByPuuidAsync above (same shape as the current summoner, XP included).

    /// <summary>A player's ranked queues: the same <c>queues[]</c> as the current player's (<c>GetLolRankedV1RankedStatsByPuuid</c>).</summary>
    public Task<JsonElement?> GetRankedStatsAsync(string puuid, CancellationToken ct) =>
        GetAsync($"/lol-ranked/v1/ranked-stats/{Uri.EscapeDataString(puuid)}", ct);

    /// <summary>
    /// A player's newest games (<c>GetLolMatchHistoryV1ProductsLolByPuuidMatches</c>): the newest 20, only their own
    /// participant each; <c>begIndex</c> / <c>endIndex</c> are ignored, so none are sent. Takes ~2 s for a player the client
    /// hasn't loaded yet.
    /// </summary>
    public Task<JsonElement?> GetMatchHistoryAsync(string puuid, CancellationToken ct) =>
        GetAsync($"/lol-match-history/v1/products/lol/{Uri.EscapeDataString(puuid)}/matches", ct);

    /// <summary>Every champion a player has mastery on, sorted by points (<c>GetLolChampionMasteryV1ByPuuidChampionMastery</c>).</summary>
    public Task<JsonElement?> GetChampionMasteryAsync(string puuid, CancellationToken ct) =>
        GetAsync($"/lol-champion-mastery/v1/{Uri.EscapeDataString(puuid)}/champion-mastery", ct);

    /// <summary>
    /// The puuid of a Riot ID (<c>GetLolSummonerV1AliasLookup</c>, <c>LolSummonerAliasLookupResponse</c>
    /// <c>{ alias: { gameName, tagLine }, puuid }</c>), matched case-insensitively and answered with League's spelling.
    /// Verified read-only against a live client, Oct 23 2026 (patch 16.21): nobody with that Riot ID is still 200, with an
    /// empty puuid and alias; a malformed one is 400.
    /// </summary>
    public Task<JsonElement?> LookupRiotIdAsync(string gameName, string tagLine, CancellationToken ct) =>
        GetAsync($"/lol-summoner/v1/alias/lookup?gameName={Uri.EscapeDataString(gameName)}&tagLine={Uri.EscapeDataString(tagLine)}", ct);

    // --- Game data audio (read-only) ----------------------------------------------------------

    public const string GameDataAssetsPrefix = "/lol-game-data/assets/";

    /// <summary>Largest game-data audio file accepted (the biggest map track is ~5 MB).</summary>
    private const int MaxGameDataAudioBytes = 16 * 1024 * 1024;

    /// <summary>
    /// The only paths <see cref="GetGameDataAudioAsync"/> fetches: <c>.ogg</c> files under <see cref="GameDataAssetsPrefix"/>,
    /// no <c>..</c>, so the method can't be turned into a reader of other endpoints.
    /// </summary>
    public static bool IsGameDataAudioPath(string path) =>
        path.StartsWith(GameDataAssetsPrefix, StringComparison.Ordinal)
        && path.Length > GameDataAssetsPrefix.Length
        && !path.Contains("..", StringComparison.Ordinal)
        && path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A map music / ambience file the client serves from <c>lol-game-data</c>, e.g.
    /// <c>/lol-game-data/assets/content/src/LeagueClient/GameModeAssets/Shared/sound/music-cs-blindpick-default.ogg</c>,
    /// loaded the way the client's own front-end does (<c>rcp-fe-lol-parties</c> <c>getMapSoundUrl</c>:
    /// <c>/${map.assets[key]}</c> from <c>/lol-maps/v2/maps</c>). Null on 404 or when the file is too large. Verified
    /// read-only against a live client (HTTP 200, Ogg data), Oct 2026.
    /// </summary>
    public async Task<byte[]?> GetGameDataAudioAsync(string assetPath, CancellationToken ct)
    {
        if (!IsGameDataAudioPath(assetPath))
            throw new ArgumentException("Not a game-data audio path.", nameof(assetPath));

        using var response = await http.GetAsync(assetPath, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        if (response.Content.Headers.ContentLength > MaxGameDataAudioBytes)
            return null;

        var data = await response.Content.ReadAsByteArrayAsync(ct);
        return data.Length > MaxGameDataAudioBytes ? null : data;
    }

    // --- Plumbing ---------------------------------------------------------------------------

    private async Task<int[]> GetIdsAsync(string path, CancellationToken ct) =>
        (await GetAsync(path, ct)) is { ValueKind: JsonValueKind.Array } array
            ? [.. array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32())]
            : [];

    private async Task<JsonElement?> GetAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private async Task SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? message = null;
        string? errorCode = null;
        try
        {
            // LCU errors look like {"errorCode":"RPC_ERROR","httpStatus":404,"message":"..."}.
            var body = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                message = document.RootElement.Str("message");
                errorCode = document.RootElement.Str("errorCode");
            }
        }
        catch (JsonException)
        {
            // Not JSON; status code alone will do.
        }

        throw new LcuRequestException(response.StatusCode, message, errorCode);
    }
}
