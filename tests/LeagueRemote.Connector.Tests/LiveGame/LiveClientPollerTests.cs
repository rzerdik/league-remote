using System.Net;
using System.Net.Security;
using System.Text;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.LiveClient;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class LiveClientPollerTests : IDisposable
{
    /// <summary>Answers each request with the next queued response (or throws it); never touches the network.</summary>
    private sealed class FakeLiveClient : HttpMessageHandler
    {
        public readonly Queue<Func<HttpResponseMessage>> Responses = new();
        public readonly List<Uri> Requests = [];

        /// <summary>An <c>allgamedata</c> answer with nothing but the clock (as during the loading screen).</summary>
        public void Ok(double gameTime) =>
            Json($$$"""{"gameData":{"gameMode":"CLASSIC","gameTime":{{{Invariant(gameTime)}}},"mapName":"Map11","mapNumber":11}}""");

        /// <summary>The 14:00 sample (optionally changed) with its clock set to <paramref name="gameTime"/>.</summary>
        public void Sample(double gameTime, Func<string, string>? change = null) =>
            Json((change ?? (s => s))(LiveGameSamples.AllGameData).Replace("\"gameTime\": 840.2", $"\"gameTime\": {Invariant(gameTime)}"));

        public void Json(string json) => Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        private static string Invariant(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void Refused() => Responses.Enqueue(() => throw new HttpRequestException("Connection refused"));

        public void Status(HttpStatusCode status) => Responses.Enqueue(() => new HttpResponseMessage(status));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(Responses.Dequeue()());
        }
    }

    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-22T18:00:00Z"));
    private readonly FakeLiveClient _api = new();
    private readonly StateStore _store = new();
    private readonly LiveGameStore _live = new();
    private readonly FakeLcu _lcu = new();
    private readonly HttpClient _lcuHttp;
    private readonly LiveClientPoller _poller;

    public LiveClientPollerTests()
    {
        _lcu.Routes["/lol-game-data/assets/v1/champion-summary.json"] = LiveGameSamples.ChampionSummary;
        _lcuHttp = _lcu.CreateClient();
        _poller = Poller(new FakeLcuSession(new LcuClient(_lcuHttp)));
    }

    private LiveClientPoller Poller(FakeLcuSession session) =>
        new(_store, _live, session, _time, NullLogger<LiveClientPoller>.Instance, _api);

    public void Dispose()
    {
        _poller.Dispose();
        _lcuHttp.Dispose();
    }

    private void Phase(string phase) =>
        _store.Set(new ClientState(true, phase, null, null, null, [], DateTimeOffset.UnixEpoch));

    private InGameState? InGame => _store.Snapshot().State.InGame;

    [Fact]
    public async Task Outside_a_game_it_does_not_poll_and_reports_nothing()
    {
        Phase("ChampSelect");
        Assert.False(await _poller.TickAsync(CancellationToken.None));
        Assert.Empty(_api.Requests);
        Assert.Null(InGame);
    }

    [Fact]
    public async Task Out_of_game_it_sleeps_until_the_phase_changes()
    {
        var session = new FakeLcuSession(null);
        using var poller = Poller(session);
        Phase("Lobby");
        await poller.StartAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(_api.Requests);

        _api.Ok(1);
        Phase("InProgress");
        session.RaiseGameflowPhaseChanged();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (_api.Requests.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Single(_api.Requests);
        await poller.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Loading_until_the_api_answers_then_loaded_with_the_game_clock()
    {
        Phase("InProgress");

        _api.Refused();
        Assert.True(await _poller.TickAsync(CancellationToken.None));
        Assert.Equal(new InGameState(false, 0, null), InGame);
        Assert.Equal(LiveClientPoller.AllGameDataUri, Assert.Single(_api.Requests));

        _api.Status(HttpStatusCode.NotFound);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(false, 0, null), InGame);

        // The first answer only arms the check: the clock has to be seen running.
        _api.Ok(0.25);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(false, 0, null), InGame);

        _time.Advance(LiveClientPoller.PollInterval);
        var loadedAt = _time.GetUtcNow();
        _api.Ok(1.25);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(true, 1.25, loadedAt), InGame);
    }

    [Fact]
    public async Task A_frozen_clock_on_the_loading_screen_is_still_loading()
    {
        Phase("InProgress");
        for (var i = 0; i < 5; i++)
        {
            _api.Ok(0);
            await _poller.TickAsync(CancellationToken.None);
            _time.Advance(LiveClientPoller.PollInterval);
            Assert.Equal(new InGameState(false, 0, null), InGame);
        }

        var loadedAt = _time.GetUtcNow();
        _api.Ok(0.8);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(true, 0.8, loadedAt), InGame);
    }

    [Fact]
    public async Task A_clock_jumping_back_to_zero_restarts_the_game_clock_at_once()
    {
        Phase("InProgress");
        _api.Ok(10);
        await _poller.TickAsync(CancellationToken.None);
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Ok(11.5);
        await _poller.TickAsync(CancellationToken.None);
        Assert.True(InGame!.Loaded);

        _time.Advance(LiveClientPoller.PollInterval);
        var restartedAt = _time.GetUtcNow();
        _api.Ok(0.3);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(true, 0.3, restartedAt), InGame);
    }

    [Fact]
    public async Task Clock_is_republished_every_few_seconds_and_the_loaded_flip_at_once()
    {
        Phase("InProgress");
        _api.Status(HttpStatusCode.ServiceUnavailable);
        await _poller.TickAsync(CancellationToken.None);
        var (_, loadingVersion) = _store.Snapshot();

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Ok(0.1);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(loadingVersion, _store.Snapshot().Version);

        // The flip is published immediately.
        _time.Advance(LiveClientPoller.PollInterval);
        var startedAt = _time.GetUtcNow();
        _api.Ok(0.5);
        await _poller.TickAsync(CancellationToken.None);
        var (state, loadedVersion) = _store.Snapshot();
        Assert.True(loadedVersion > loadingVersion);
        Assert.Equal(0.5, state.InGame!.GameTime);

        // Clock only: held back until PublishInterval has passed.
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Ok(2.0);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(loadedVersion, _store.Snapshot().Version);

        _time.Advance(LiveClientPoller.PublishInterval);
        _api.Ok(7.0);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(true, 7.0, startedAt), InGame);
    }

    [Fact]
    public async Task A_hiccup_after_loading_keeps_the_game_loaded_and_leaving_the_game_clears_it()
    {
        Phase("InProgress");
        _api.Ok(29);
        await _poller.TickAsync(CancellationToken.None);
        _api.Ok(30);
        await _poller.TickAsync(CancellationToken.None);
        var loaded = InGame;
        Assert.True(loaded!.Loaded);

        _time.Advance(LiveClientPoller.PublishInterval);
        _api.Refused();
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(loaded, InGame);

        Phase("EndOfGame");
        Assert.False(await _poller.TickAsync(CancellationToken.None));
        Assert.Null(InGame);

        // Next game: loading again, with a new start time.
        Phase("GameStart");
        _api.Refused();
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(new InGameState(false, 0, null), InGame);
    }

    [Fact]
    public void Lcu_state_updates_keep_the_in_game_part()
    {
        _store.SetInGame(new InGameState(true, 5, DateTimeOffset.UnixEpoch));
        Phase("InProgress");
        Assert.Equal(new InGameState(true, 5, DateTimeOffset.UnixEpoch), InGame);
    }

    // --- Live game ------------------------------------------------------------------------

    private const string LastEarlyEvent = "\"KillerName\": \"Turret_T1_L_03_A\", \"VictimName\": \"Enemy1\", \"Assisters\": [] }";
    private const string Gold = "\"currentGold\": 1234.5677490234375";
    private const string HeraldEvent = ", { \"EventID\": 16, \"EventName\": \"HeraldKill\", \"EventTime\": 843.5, \"Stolen\": \"False\", \"KillerName\": \"Ally2\", \"Assisters\": [] }";

    private LiveGameState Live => _live.Snapshot().Value;

    private long LiveVersion => _live.Snapshot().Version;

    /// <summary>Two polls of the sample with a running clock: the game is loaded and the live game published.</summary>
    private async Task LoadSampleAsync()
    {
        Phase("InProgress");
        _api.Sample(840.2);
        await _poller.TickAsync(CancellationToken.None);
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(841.7);
        await _poller.TickAsync(CancellationToken.None);
    }

    [Fact]
    public async Task One_request_per_poll_feeds_both_the_clock_and_the_live_game()
    {
        Phase("InProgress");
        _api.Sample(840.2);
        await _poller.TickAsync(CancellationToken.None);
        Assert.False(Live.Available); // the clock has to be seen running first

        _time.Advance(LiveClientPoller.PollInterval);
        var loadedAt = _time.GetUtcNow();
        _api.Sample(841.7);
        await _poller.TickAsync(CancellationToken.None);

        Assert.Equal(new InGameState(true, 841.7, loadedAt), InGame);
        Assert.True(Live.Available);
        Assert.Equal((841.7, loadedAt), (Live.GameTime, Live.UpdatedAt));
        Assert.Equal(103, Live.Me!.Player.ChampionId); // champion ids from the LCU's champion summary
        Assert.Equal(62, Live.Teams[1].Players.Single(p => p.ChampionName == "Wukong").ChampionId);
        Assert.Equal([LiveClientPoller.AllGameDataUri, LiveClientPoller.AllGameDataUri], _api.Requests);
        Assert.Equal(["GET /lol-game-data/assets/v1/champion-summary.json"], _lcu.Requests); // read once
    }

    [Fact]
    public async Task Gold_and_cs_wait_for_the_publish_interval_but_events_go_out_at_once()
    {
        await LoadSampleAsync();
        var version = LiveVersion;

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(843.2, s => s.Replace(Gold, "\"currentGold\": 1260.0"));
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version, LiveVersion);
        Assert.Equal(841.7, Live.GameTime);

        _time.Advance(TimeSpan.FromSeconds(0.5));
        _api.Sample(843.7, s => s.Replace(LastEarlyEvent, LastEarlyEvent + HeraldEvent));
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 1, LiveVersion);
        Assert.Equal((16, 843.7), (Live.Events[^1].Id, Live.GameTime));

        // Only the gold moved again: held back until the interval has passed since that publish.
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(845.2, s => s.Replace(LastEarlyEvent, LastEarlyEvent + HeraldEvent).Replace(Gold, "\"currentGold\": 1300.0"));
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 1, LiveVersion);

        _time.Advance(LiveClientPoller.LiveGamePublishInterval);
        _api.Sample(848.2, s => s.Replace(LastEarlyEvent, LastEarlyEvent + HeraldEvent).Replace(Gold, "\"currentGold\": 1310.0"));
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 2, LiveVersion);
        Assert.Equal((1310.0, 848.2), (Live.Me!.CurrentGold, Live.GameTime));
    }

    [Fact]
    public async Task A_clock_only_change_is_republished_every_interval_but_not_faster()
    {
        await LoadSampleAsync();
        var version = LiveVersion;

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(843.2);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version, LiveVersion);

        // Nothing but the clock moved, yet the phone gets a fresh base for its clock and countdowns.
        _time.Advance(LiveClientPoller.LiveGamePublishInterval - LiveClientPoller.PollInterval);
        _api.Sample(844.7);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 1, LiveVersion);
        Assert.Equal((844.7, _time.GetUtcNow()), (Live.GameTime, Live.UpdatedAt));

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(846.2);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 1, LiveVersion);

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(847.7);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 2, LiveVersion);
    }

    [Fact]
    public async Task A_failing_live_game_update_keeps_polling_and_the_game_clock()
    {
        var fail = false;
        using var poller = new LiveClientPoller(_store, _live, new FakeLcuSession(null), _time, NullLogger<LiveClientPoller>.Instance, _api)
        {
            MapLiveGame = (root, ids, now) => fail ? throw new InvalidOperationException("mapper bug") : LiveGameMapper.Map(root, ids, now),
        };
        Phase("InProgress");
        _api.Sample(840.2);
        await poller.TickAsync(CancellationToken.None);
        _time.Advance(LiveClientPoller.PollInterval);
        var loadedAt = _time.GetUtcNow();
        _api.Sample(841.7);
        await poller.TickAsync(CancellationToken.None);
        var game = Live;
        Assert.True(game.Available);

        fail = true;
        _time.Advance(LiveClientPoller.PublishInterval);
        _api.Sample(846.7);
        Assert.True(await poller.TickAsync(CancellationToken.None));
        Assert.Equal(new InGameState(true, 846.7, loadedAt), InGame); // the clock keeps going
        Assert.Same(game, Live); // the last live game is kept

        _time.Advance(LiveClientPoller.LiveGameStaleAfter);
        _api.Sample(856.7);
        await poller.TickAsync(CancellationToken.None);
        Assert.False(Live.Available); // ... until it is stale

        fail = false;
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(858.2);
        await poller.TickAsync(CancellationToken.None);
        Assert.True(Live.Available);
    }

    [Fact]
    public async Task A_disposed_lcu_connection_only_costs_the_champion_ids()
    {
        var http = _lcu.CreateClient();
        http.Dispose(); // what LcuConnectionService does on a disconnect
        using var poller = Poller(new FakeLcuSession(new LcuClient(http)));
        Phase("InProgress");
        _api.Sample(840.2);
        await poller.TickAsync(CancellationToken.None);
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(841.7);
        await poller.TickAsync(CancellationToken.None);

        Assert.True(Live.Available);
        Assert.Equal(0, Live.Me!.Player.ChampionId);
        Assert.True(InGame!.Loaded);
    }

    [Fact]
    public async Task A_new_item_goes_out_at_once()
    {
        await LoadSampleAsync();
        var version = LiveVersion;

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(843.2, s => s.Replace("{ \"itemID\": 2003, \"slot\": 2, \"count\": 1 }, ", ""));
        await _poller.TickAsync(CancellationToken.None);

        Assert.Equal(version + 1, LiveVersion);
        Assert.Equal(3, Live.Me!.Player.Items.Count);
    }

    [Fact]
    public async Task The_live_game_is_cleared_when_the_game_ends()
    {
        await LoadSampleAsync();
        Assert.True(Live.Available);
        var version = LiveVersion;

        Phase("EndOfGame");
        await _poller.TickAsync(CancellationToken.None);

        Assert.False(Live.Available);
        Assert.Equal(version + 1, LiveVersion);
        Assert.Empty(Live.Teams);

        // Idle polls don't republish the empty game.
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(version + 1, LiveVersion);
    }

    [Fact]
    public async Task A_hiccup_keeps_the_live_game_and_a_silent_game_clears_it()
    {
        await LoadSampleAsync();
        var game = Live;

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Refused();
        await _poller.TickAsync(CancellationToken.None);
        Assert.Same(game, Live);

        _time.Advance(LiveClientPoller.LiveGameStaleAfter);
        _api.Status(HttpStatusCode.ServiceUnavailable);
        await _poller.TickAsync(CancellationToken.None);
        Assert.False(Live.Available);
        Assert.True(InGame!.Loaded); // the game still counts as loaded for the clock and the push trigger

        // It answers again: published at once.
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(860);
        await _poller.TickAsync(CancellationToken.None);
        Assert.True(Live.Available);
    }

    [Fact]
    public async Task Without_the_league_client_champion_ids_stay_unknown()
    {
        using var poller = Poller(new FakeLcuSession(null));
        Phase("InProgress");
        _api.Sample(840.2);
        await poller.TickAsync(CancellationToken.None);
        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(841.7);
        await poller.TickAsync(CancellationToken.None);

        Assert.True(Live.Available);
        Assert.All(Live.Teams.SelectMany(t => t.Players), p => Assert.Equal(0, p.ChampionId));
        Assert.Equal("Wukong", Live.Teams[1].Players[1].ChampionName);
    }

    [Fact]
    public async Task A_missing_champion_summary_is_retried_after_a_while_not_every_poll()
    {
        _lcu.Routes.Remove("/lol-game-data/assets/v1/champion-summary.json");
        await LoadSampleAsync();
        Assert.Single(_lcu.Requests);

        _time.Advance(LiveClientPoller.PollInterval);
        _api.Sample(843.2);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Single(_lcu.Requests);

        _lcu.Routes["/lol-game-data/assets/v1/champion-summary.json"] = LiveGameSamples.ChampionSummary;
        _time.Advance(LiveClientPoller.CatalogRetry);
        _api.Sample(875);
        await _poller.TickAsync(CancellationToken.None);
        Assert.Equal(2, _lcu.Requests.Count);
        Assert.Equal(103, Live.Me!.Player.ChampionId); // the ids changing publishes at once
    }

    [Fact]
    public void Untrusted_certificates_are_accepted_only_from_the_local_game()
    {
        Assert.True(LiveClientPoller.IsTrustedCertificate(LiveClientPoller.AllGameDataUri, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(LiveClientPoller.IsTrustedCertificate(new Uri("https://127.0.0.1:2998/"), SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(LiveClientPoller.IsTrustedCertificate(new Uri("https://example.com:2999/"), SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.True(LiveClientPoller.IsTrustedCertificate(new Uri("https://example.com/"), SslPolicyErrors.None));
    }
}
