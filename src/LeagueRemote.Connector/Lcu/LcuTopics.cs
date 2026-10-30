namespace LeagueRemote.Connector.Lcu;

/// <summary>
/// The WAMP topics the connector subscribes to: "OnJsonApiEvent" + the endpoint path with "/" replaced by "_". Each topic
/// only says that something changed; the refresh loops re-read what they need.
/// </summary>
internal static class LcuTopics
{
    public const string GameflowPhase = "OnJsonApiEvent_lol-gameflow_v1_gameflow-phase";

    /// <summary>Holds the post-game's Play Again state (player-status lives below it).</summary>
    public const string GameflowMetadata = "OnJsonApiEvent_lol-gameflow_v1_gameflow-metadata";

    public const string ReadyCheck = "OnJsonApiEvent_lol-matchmaking_v1_ready-check";
    public const string MatchmakingSearch = "OnJsonApiEvent_lol-matchmaking_v1_search";
    public const string Lobby = "OnJsonApiEvent_lol-lobby_v2_lobby";
    public const string LobbySearchState = "OnJsonApiEvent_lol-lobby_v2_lobby_matchmaking_search-state";
    public const string ReceivedInvitations = "OnJsonApiEvent_lol-lobby_v2_received-invitations";
    public const string ChampSelectSession = "OnJsonApiEvent_lol-champ-select_v1_session";
    public const string PickableChampionIds = "OnJsonApiEvent_lol-champ-select_v1_pickable-champion-ids";
    public const string BannableChampionIds = "OnJsonApiEvent_lol-champ-select_v1_bannable-champion-ids";

    /// <summary>Filled shortly after the local player locks in, and again after a trade or reroll.</summary>
    public const string SkinCarousel = "OnJsonApiEvent_lol-champ-select_v1_skin-carousel-skins";

    public const string RunePages = "OnJsonApiEvent_lol-perks_v1_pages";
    public const string CurrentRunePage = "OnJsonApiEvent_lol-perks_v1_currentpage";

    /// <summary>Conversations and everything below them (messages, participants).</summary>
    public const string Chat = "OnJsonApiEvent_lol-chat_v1_conversations";

    /// <summary>Friends and their presence (one event per friend that changed).</summary>
    public const string Friends = "OnJsonApiEvent_lol-chat_v1_friends";

    public const string FriendGroups = "OnJsonApiEvent_lol-chat_v1_friend-groups";

    /// <summary>Pending friend requests both ways (<c>/lol-chat/v2/friend-requests</c>).</summary>
    public const string FriendRequests = "OnJsonApiEvent_lol-chat_v2_friend-requests";

    /// <summary>The local player's ranked queues (LP, wins, tier after a ranked game).</summary>
    public const string RankedStats = "OnJsonApiEvent_lol-ranked_v1_current-ranked-stats";

    /// <summary>Champion mastery list and score (<c>/lol-champion-mastery/v1/local-player/…</c>).</summary>
    public const string Mastery = "OnJsonApiEvent_lol-champion-mastery_v1_local-player";

    /// <summary>Level, XP, icon and Riot ID of the local player.</summary>
    public const string CurrentSummoner = "OnJsonApiEvent_lol-summoner_v1_current-summoner";

    /// <summary>The post-game stats screen's data: the state's post-game summary, and the profile's LP and XP.</summary>
    public const string EndOfGameStats = "OnJsonApiEvent_lol-end-of-game_v1_eog-stats-block";

    public const string ChampionMasteryUpdates = "OnJsonApiEvent_lol-end-of-game_v1_champion-mastery-updates";
    public const string LpChangeNotification = "OnJsonApiEvent_lol-ranked_v1_current-lp-change-notification";

    /// <summary>Everything <see cref="LcuEventSocket"/> subscribes to.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        GameflowPhase,
        ReadyCheck,
        MatchmakingSearch,
        Lobby,
        LobbySearchState,
        ReceivedInvitations,
        ChampSelectSession,
        PickableChampionIds,
        BannableChampionIds,
        SkinCarousel,
        RunePages,
        CurrentRunePage,
        Chat,
        Friends,
        FriendGroups,
        FriendRequests,
        RankedStats,
        Mastery,
        CurrentSummoner,
        EndOfGameStats,
        ChampionMasteryUpdates,
        LpChangeNotification,
        GameflowMetadata,
    ];
}
