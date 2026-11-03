using System.Text.Json;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Raw LCU responses gathered for one state rebuild. Null = endpoint not fetched or 404.
/// <see cref="PlayerNames"/> holds names resolved via the summoner API, keyed by <see cref="RiotIdNames.NameKeys"/>.
/// <see cref="AllChampionIds"/> (every champion in the game) is only fetched when the bannable list is unusable.
/// </summary>
internal sealed record LcuSnapshot(
    string GameflowPhase,
    JsonElement? ReadyCheck = null,
    JsonElement? Lobby = null,
    JsonElement? LobbySearchState = null,
    JsonElement? MatchmakingSearch = null,
    JsonElement? ChampSelectSession = null,
    IReadOnlyList<int>? PickableChampionIds = null,
    IReadOnlyList<int>? BannableChampionIds = null,
    IReadOnlyList<int>? AllChampionIds = null,
    JsonElement? RunePages = null,
    IReadOnlyDictionary<string, string>? PlayerNames = null,
    JsonElement? Queues = null,
    JsonElement? ReceivedInvitations = null,
    JsonElement? SkinCarousel = null,
    PostGameReads? PostGame = null,
    JsonElement? ChampionPositions = null);

/// <summary>The post-game screen's reads (EndOfGame phase only), mapped by <see cref="PostGameMapper"/>.</summary>
internal sealed record PostGameReads(
    JsonElement? Stats,
    JsonElement? LpChange = null,
    JsonElement? MasteryUpdate = null,
    JsonElement? Session = null,
    JsonElement? PlayerStatus = null);
