using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>Which LCU events reread the profile, and the watch that rereads it after a game until the game shows up.</summary>
public sealed class ProfilePostGameWatchTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>The newest game is now 4000000005: the game that just ended.</summary>
    private static readonly string MatchesWithNewGame =
        ProfileSamples.Own.Matches.Replace("4000000004", "4000000005").Replace("1792699895462", "1792800000000");

    // --- Events -------------------------------------------------------------------------------

    [Theory]
    [InlineData(LcuTopics.RankedStats)]
    [InlineData(LcuTopics.Mastery)]
    [InlineData(LcuTopics.CurrentSummoner)]
    public void Profile_events_only_reread_the_profile(string topic)
    {
        Assert.Equal((false, false, false, true), LcuConnectionService.RefreshesFor(topic));
        Assert.Contains(topic, LcuTopics.All);
    }

    [Fact]
    public void Game_flow_events_do_not_reread_the_profile()
    {
        Assert.False(LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-gameflow_v1_gameflow-phase").Profile);
        Assert.False(LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-champ-select_v1_session").Profile);
        Assert.False(LcuConnectionService.RefreshesFor(LcuTopics.Friends).Profile);
    }

    [Theory]
    [InlineData("PreEndOfGame", "EndOfGame", true)]
    [InlineData("WaitingForStats", "EndOfGame", true)]
    [InlineData("InProgress", "None", true)] // Practice Tool closed straight to the home screen
    [InlineData("WaitingForStats", "Lobby", true)]
    [InlineData(null, "EndOfGame", true)] // the connector connected on the stats screen
    [InlineData("InProgress", "WaitingForStats", false)]
    [InlineData("WaitingForStats", "PreEndOfGame", false)]
    [InlineData("EndOfGame", "EndOfGame", false)]
    [InlineData("EndOfGame", "Lobby", false)]
    [InlineData("InProgress", "Reconnect", false)]
    [InlineData("ChampSelect", "InProgress", false)]
    [InlineData(null, "InProgress", false)]
    public void A_game_ends_when_the_phase_leaves_the_game_or_reaches_end_of_game(string? previous, string phase, bool ended) =>
        Assert.Equal(ended, GameflowPhases.GameJustEnded(previous, phase));

    // --- The post-game watch ------------------------------------------------------------------

    [Fact]
    public async Task After_a_game_the_profile_is_reread_with_backoff_until_the_game_shows_up()
    {
        using var watch = new Watch();
        var task = await watch.StartAsync();

        watch.Time.Advance(TimeSpan.FromSeconds(4.9));
        Assert.Empty(watch.Lcu.Requests);

        watch.Time.Advance(TimeSpan.FromSeconds(0.1)); // 5 s: first reread, the game isn't listed yet
        await watch.Answers.WaitFor(ProfileSamples.Own.ProfileReads.Count);
        Assert.Equal(ProfileSamples.Own.ProfileReads, watch.Lcu.Requests);
        Assert.False(task.IsCompleted);

        watch.Lcu.Routes[ProfileSamples.Own.MatchesPath] = MatchesWithNewGame;
        watch.Time.Advance(TimeSpan.FromSeconds(14.9));
        Assert.Equal(ProfileSamples.Own.ProfileReads.Count, watch.Lcu.Requests.Count);

        watch.Time.Advance(TimeSpan.FromSeconds(0.1)); // 20 s: there it is
        await task.WaitAsync(Timeout);

        Assert.Equal([.. ProfileSamples.Own.ProfileReads, .. ProfileSamples.Own.ProfileReads], watch.Lcu.Requests);
        Assert.Equal(4000000005L, watch.Store.Snapshot().Profile.Matches[0].GameId);
    }

    [Fact]
    public async Task The_post_game_watch_gives_up_after_four_rereads()
    {
        using var watch = new Watch();
        var task = await watch.StartAsync();

        foreach (var gap in ProfileReader.PostGameBackoff)
        {
            watch.Time.Advance(gap);
            await watch.Answers.WaitFor(ProfileSamples.Own.ProfileReads.Count);
        }

        await task.WaitAsync(Timeout);
        Assert.Equal(4 * ProfileSamples.Own.ProfileReads.Count, watch.Lcu.Requests.Count);
        Assert.Equal(
            [5, 15, 45, 120],
            ProfileReader.PostGameBackoff.Select(g => (int)g.TotalSeconds));
    }

    [Fact]
    public async Task The_post_game_watch_stops_without_a_read_when_the_regular_refresh_found_the_game()
    {
        using var watch = new Watch();
        var task = await watch.StartAsync();

        var withGame = ProfileSamples.Own.Handler();
        withGame.Routes[ProfileSamples.Own.MatchesPath] = MatchesWithNewGame;
        watch.Store.Set(await ReadProfileAsync(withGame), watch.Time.GetUtcNow());
        watch.Time.Advance(TimeSpan.FromSeconds(5));
        await task.WaitAsync(Timeout);

        Assert.Empty(watch.Lcu.Requests);
    }

    [Fact]
    public async Task Connected_on_the_stats_screen_the_first_regular_read_is_the_baseline()
    {
        using var watch = new Watch();
        var task = watch.StartUnread(); // GameJustEnded(null, "EndOfGame"): the store is still empty

        // The regular refresh after connecting lists the history from before the game.
        watch.Store.Set(await ReadProfileAsync(ProfileSamples.Own.Handler()), watch.Time.GetUtcNow());
        watch.Time.Advance(TimeSpan.FromSeconds(5));
        await watch.Answers.WaitFor(ProfileSamples.Own.ProfileReads.Count);
        Assert.False(task.IsCompleted); // old history isn't the game that just ended

        watch.Lcu.Routes[ProfileSamples.Own.MatchesPath] = MatchesWithNewGame;
        watch.Time.Advance(TimeSpan.FromSeconds(15));
        await task.WaitAsync(Timeout);

        Assert.Equal(2 * ProfileSamples.Own.ProfileReads.Count, watch.Lcu.Requests.Count);
        Assert.Equal(4000000005L, watch.Store.Snapshot().Profile.Matches[0].GameId);
    }

    [Fact]
    public async Task Connected_on_the_stats_screen_the_watchs_own_first_read_is_the_baseline()
    {
        using var watch = new Watch();
        var task = watch.StartUnread();

        watch.Time.Advance(TimeSpan.FromSeconds(5)); // no regular read yet: the watch's own read sets the baseline
        await watch.Answers.WaitFor(ProfileSamples.Own.ProfileReads.Count);
        Assert.False(task.IsCompleted);

        watch.Lcu.Routes[ProfileSamples.Own.MatchesPath] = MatchesWithNewGame;
        watch.Time.Advance(TimeSpan.FromSeconds(15));
        await task.WaitAsync(Timeout);

        Assert.Equal(2 * ProfileSamples.Own.ProfileReads.Count, watch.Lcu.Requests.Count);
    }

    /// <summary>Counts answered LCU requests so a test can wait for a read the watch makes on another thread.</summary>
    private sealed class Answered(FakeLcu inner) : DelegatingHandler(inner)
    {
        private readonly SemaphoreSlim _answered = new(0);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            _answered.Release();
            return response;
        }

        public async Task WaitFor(int requests)
        {
            for (var i = 0; i < requests; i++)
                Assert.True(await _answered.WaitAsync(Timeout), "The LCU was not read in time.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _answered.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class Watch : IDisposable
    {
        private readonly HttpClient _http;

        public Watch()
        {
            Answers = new Answered(Lcu);
            _http = LcuHttp(Answers);
        }

        public FakeTimeProvider Time { get; } = new(DateTimeOffset.Parse("2026-10-23T18:00:00Z"));

        public FakeLcu Lcu { get; } = ProfileSamples.Own.Handler();

        public ProfileStore Store { get; } = new();

        public Answered Answers { get; }

        public LcuClient Client => new(_http);

        public async Task<Task> StartAsync()
        {
            Store.Set(await ReadProfileAsync(ProfileSamples.Own.Handler()), Time.GetUtcNow());
            return StartUnread();
        }

        /// <summary>Starts the watch on a store that wasn't read yet (connected on the stats screen).</summary>
        public Task StartUnread() =>
            ProfileReader.WatchForNewGameAsync(Client, Store, Time, NullLogger.Instance, CancellationToken.None);

        public void Dispose() => _http.Dispose();
    }
}
