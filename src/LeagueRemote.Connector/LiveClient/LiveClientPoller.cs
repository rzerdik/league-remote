using System.Net.Security;
using System.Text.Json;
using System.Threading.Channels;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.LiveClient;

/// <summary>
/// The game's loaded flag and clock (<see cref="InGameState"/>) and the live game (<see cref="LiveGameState"/>), from Riot's
/// official Live Client Data API: one <c>GET allgamedata</c> on 127.0.0.1:2999 per poll, only while the game runs, and
/// nothing is ever sent to the game. Loaded means the clock advances (the API can answer with a frozen clock during the
/// loading screen); a hiccup after that keeps the loaded state, so phones and push never see loaded → loading → loaded.
/// Enemies appear only by champion; Riot IDs are kept for the own team and never logged.
/// </summary>
internal sealed class LiveClientPoller : BackgroundService
{
    public const string Host = "127.0.0.1";
    public const int Port = 2999;
    public static readonly Uri AllGameDataUri = new($"https://{Host}:{Port}/liveclientdata/allgamedata");

    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>Republish interval of the clock while nothing else changed (the phone interpolates in between).</summary>
    public static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(5);

    /// <summary>Republish interval of the live game when only gold, CS, vision or respawn countdowns moved.</summary>
    public static readonly TimeSpan LiveGamePublishInterval = TimeSpan.FromSeconds(3);

    /// <summary>The API not answering this long while in game (game crashed, Reconnect phase) clears the live game.</summary>
    public static readonly TimeSpan LiveGameStaleAfter = TimeSpan.FromSeconds(10);

    /// <summary>Wait before asking the LCU for the champion summary again after it wasn't available.</summary>
    internal static readonly TimeSpan CatalogRetry = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    private readonly StateStore _store;
    private readonly LiveGameStore _liveStore;
    private readonly ILcuSession _session;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    /// <summary>Set when the gameflow phase changes: wakes the idle poller instead of a timer.</summary>
    private readonly Channel<bool> _phaseChanged = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>The live-game mapping; a seam so tests can make it fail.</summary>
    internal Func<JsonElement, IReadOnlyDictionary<string, int>?, DateTimeOffset, LiveGameState> MapLiveGame { get; init; } = LiveGameMapper.Map;

    /// <summary>A live-game failure was logged and nothing has succeeded since (log once per streak).</summary>
    private bool _liveFailureLogged;

    private InGameState? _current;
    private double? _lastGameTime;
    private InGameState? _published;
    private DateTimeOffset _publishedAt;

    /// <summary><see cref="LiveGameKey"/> of the last published live game; null after Empty.</summary>
    private string? _liveKey;
    private DateTimeOffset _livePublishedAt;
    private DateTimeOffset? _liveReadAt;

    private LcuClient? _catalogClient;
    private ChampionCatalog? _catalog;
    private DateTimeOffset _catalogRetryAt;
    private IReadOnlyDictionary<string, int>? _championIds;

    /// <param name="handler">The API's transport; by default one that trusts only the game's certificate (<see cref="IsTrustedCertificate"/>).</param>
    public LiveClientPoller(
        StateStore store,
        LiveGameStore liveStore,
        ILcuSession session,
        TimeProvider time,
        ILogger<LiveClientPoller> logger,
        HttpMessageHandler? handler = null)
    {
        _store = store;
        _liveStore = liveStore;
        _session = session;
        _time = time;
        _logger = logger;
        _http = new HttpClient(handler ?? CreateHandler()) { Timeout = RequestTimeout };
        _session.GameflowPhaseChanged += OnPhaseChanged;
    }

    /// <summary>The game serves a certificate from Riot's own root, which Windows does not trust: accepted only for 127.0.0.1:2999.</summary>
    internal static bool IsTrustedCertificate(Uri? requestUri, SslPolicyErrors errors) =>
        errors == SslPolicyErrors.None
        || (requestUri is not null && string.Equals(requestUri.Host, Host, StringComparison.Ordinal) && requestUri.Port == Port);

    private static HttpMessageHandler CreateHandler() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, _, _, errors) => IsTrustedCertificate(message.RequestUri, errors),
        UseProxy = false,
    };

    private void OnPhaseChanged() => _phaseChanged.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var inGame = true;
            try
            {
                inGame = await TickAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Never let one bad poll stop the connector (a faulted BackgroundService stops the host).
                _logger.LogWarning("Live client poll failed ({Error})", ex.GetType().Name);
            }

            // Out of game there is nothing to poll: sleep until the phase changes (a change during the tick is kept).
            if (inGame)
                await Task.Delay(PollInterval, _time, ct);
            else
                await _phaseChanged.Reader.ReadAsync(ct);
        }
    }

    /// <summary>One poll: reads the phase, queries the API when in game, stores the result when it should be published.</summary>
    /// <returns>True while the game runs.</returns>
    internal async Task<bool> TickAsync(CancellationToken ct)
    {
        var phase = _store.GameflowPhase;
        if (!GameflowPhases.IsGameRunning(phase))
        {
            _current = null;
            _lastGameTime = null;
            Publish(null);
            ClearLiveGame();
            return false;
        }

        using var allGameData = await TryGetAllGameDataAsync(ct);
        var gameTime = allGameData is null ? null : LiveGameMapper.GameTime(allGameData.RootElement);
        var now = _time.GetUtcNow();
        var previous = _lastGameTime;
        _lastGameTime = gameTime ?? _lastGameTime;
        if (gameTime is { } t && _current is { Loaded: true } loaded)
        {
            // The clock jumping back means the game (re)started from zero, e.g. the loading screen's clock was
            // already counting: restart the game clock with it.
            var restarted = t < loaded.GameTime - RestartTolerance;
            if (restarted)
                _logger.LogInformation("Game clock restarted ({Previous:0.0}s → {GameTime:0.0}s)", loaded.GameTime, t);
            _current = new InGameState(true, t, restarted ? now : loaded.StartedAt);
        }
        else if (gameTime is { } t2 && previous is { } p && t2 > p)
        {
            // Loaded only once the clock runs: during the loading screen the API can already answer with a
            // frozen gameTime, which the phone would otherwise count up from and then reset.
            _logger.LogInformation("Game loaded (game time {GameTime:0.0}s)", t2);
            _current = new InGameState(true, t2, now);
        }
        else if (_current is not { Loaded: true })
        {
            _current = new InGameState(false, 0, null);
        }

        // else: transient failure after the game loaded; keep the last loaded state.
        if (_published?.Loaded != _current.Loaded || _published?.StartedAt != _current.StartedAt || now - _publishedAt >= PublishInterval)
            Publish(_current);

        try
        {
            await UpdateLiveGameAsync(gameTime is null ? null : allGameData!.RootElement, now, ct);
            _liveFailureLogged = false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Keep the last live game (until it is stale) and the in-game state; type only, the data may hold Riot IDs.
            if (!_liveFailureLogged)
                _logger.LogWarning("Live game update failed ({Error}); keeping the last one", ex.GetType().Name);
            _liveFailureLogged = true;
            if (_liveReadAt is { } readAt && now - readAt >= LiveGameStaleAfter)
                ClearLiveGame();
        }

        return true;
    }

    // --- Live game ----------------------------------------------------------------------------

    private async Task UpdateLiveGameAsync(JsonElement? allGameData, DateTimeOffset now, CancellationToken ct)
    {
        if (_current is not { Loaded: true })
        {
            ClearLiveGame();
            return;
        }

        if (allGameData is not { } root)
        {
            // A hiccup keeps the last scoreboard; a game that stopped answering doesn't.
            if (_liveReadAt is { } readAt && now - readAt >= LiveGameStaleAfter)
                ClearLiveGame();
            return;
        }

        var game = MapLiveGame(root, await ChampionIdsAsync(now, ct), now);
        _liveReadAt = now;
        var key = LiveGameKey(game);
        if (key == _liveKey && now - _livePublishedAt < LiveGamePublishInterval)
            return;
        _liveKey = key;
        _livePublishedAt = now;
        // The store ignores the clock; when nothing else moved, a new version still carries the fresh clock out.
        if (!_liveStore.Set(game) && game.Available)
            _liveStore.Touch();
    }

    private void ClearLiveGame()
    {
        _liveKey = null;
        _liveReadAt = null;
        _liveStore.Set(LiveGameState.Empty() with { UpdatedAt = _time.GetUtcNow() });
    }

    /// <summary>
    /// What the phone should see at once: the feed, the result, and each player's champion, level, KDA, death and items.
    /// Gold, CS, vision and respawn countdowns wait for <see cref="LiveGamePublishInterval"/>.
    /// </summary>
    internal static string LiveGameKey(LiveGameState game)
    {
        string[] header = [game.Available ? "1" : "0", game.Result, game.Events.Count > 0 ? $"{game.Events.Count}:{game.Events[^1].Id}" : ""];
        var players = game.Teams.SelectMany(t => t.Players).Select(p =>
        {
            int[] values =
            [
                p.ChampionId, p.Level, p.Kills, p.Deaths, p.Assists, p.IsDead ? 1 : 0,
                .. p.Items.SelectMany(i => new[] { i.Slot, i.ItemId, i.Count }),
            ];
            return string.Join(',', values);
        });
        return string.Join('|', header.Concat(players));
    }

    /// <summary>
    /// Champion ids by name from the LCU's champion summary, read once per LCU connection; the last map is kept while the
    /// LCU is away or the read failed (retried after <see cref="CatalogRetry"/>). Null until the first read succeeded.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>?> ChampionIdsAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_session.Client is not { } client)
            return _championIds;
        if (!ReferenceEquals(client, _catalogClient))
        {
            _catalogClient = client;
            _catalog = new ChampionCatalog(client);
            _catalogRetryAt = default;
        }

        if (_catalog is null || now < _catalogRetryAt)
            return _championIds; // loaded for this connection, or waiting to retry

        try
        {
            if (await _catalog.GetIdsByNameAsync(ct) is { } ids)
            {
                _championIds = ids;
                _catalog = null;
            }
            else
            {
                _catalogRetryAt = now + CatalogRetry;
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // The ids are optional (names are sent too): any failure, also the LCU's HttpClient disposed by a
            // disconnect, just means trying again later.
            _catalogRetryAt = now + CatalogRetry;
        }

        return _championIds;
    }

    /// <summary>A reported clock this far below the last one is a restart, not jitter.</summary>
    private const double RestartTolerance = 2;

    private void Publish(InGameState? state)
    {
        _published = state;
        _publishedAt = _time.GetUtcNow();
        _store.SetInGame(state);
    }

    /// <summary>The API's whole answer, or null when it is not answering (yet). The caller disposes it.</summary>
    private async Task<JsonDocument?> TryGetAllGameDataAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(AllGameDataUri, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(body, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or IOException && !ct.IsCancellationRequested)
        {
            return null; // not loaded yet (connection refused while the game process starts, loading screen, ...)
        }
    }

    public override void Dispose()
    {
        _session.GameflowPhaseChanged -= OnPhaseChanged;
        _http.Dispose();
        base.Dispose();
    }
}
