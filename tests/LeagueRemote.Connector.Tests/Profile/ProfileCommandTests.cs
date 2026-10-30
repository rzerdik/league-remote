using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>The local player's profile commands: <see cref="CommandType.LoadMatchDetails"/> and <see cref="CommandType.RefreshProfile"/>.</summary>
public sealed class ProfileCommandTests
{
    [Fact]
    public async Task LoadMatchDetails_reads_the_game_once_and_publishes_it()
    {
        using var c = new Commands();
        await c.ReadProfileAsync();
        var version = c.Store.Snapshot().Version;

        var result = await c.Run(CommandType.LoadMatchDetails, ProfileSamples.Own.RankedLossGameId);

        Assert.True(result.Success, result.Error);
        Assert.Equal([$"GET {ProfileSamples.Own.SummonerPath}", $"GET {ProfileSamples.Own.GamePath}"], c.Lcu.Requests);
        Assert.Equal([ProfileSamples.Own.RankedLossGameId], c.DetailIds);
        Assert.True(c.Store.Snapshot().Version > version);
        Assert.Equal(0, c.Session.Refreshes); // the game state isn't rebuilt for it
    }

    [Fact]
    public async Task LoadMatchDetails_of_a_loaded_game_moves_it_to_the_front_without_a_read()
    {
        using var c = new Commands();
        c.Lcu.Routes["/lol-match-history/v1/games/4000000001"] = ProfileSamples.Own.Game.Replace("4000000004", "4000000001");
        await c.ReadProfileAsync();
        await c.Run(CommandType.LoadMatchDetails, ProfileSamples.Own.RankedLossGameId);
        await c.Run(CommandType.LoadMatchDetails, 4000000001);
        Assert.Equal([4000000001L, ProfileSamples.Own.RankedLossGameId], c.DetailIds);
        c.Lcu.Requests.Clear();

        var result = await c.Run(CommandType.LoadMatchDetails, ProfileSamples.Own.RankedLossGameId);

        Assert.True(result.Success, result.Error);
        Assert.Empty(c.Lcu.Requests);
        Assert.Equal([ProfileSamples.Own.RankedLossGameId, 4000000001L], c.DetailIds);
    }

    [Fact]
    public async Task LoadMatchDetails_refuses_a_game_that_is_not_in_the_match_history()
    {
        using var c = new Commands();
        await c.ReadProfileAsync();

        var result = await c.Run(CommandType.LoadMatchDetails, 4000000099);

        Assert.Equal((false, "That game isn't in your match history."), (result.Success, result.Error));
        Assert.Empty(c.Lcu.Requests);
        Assert.Empty(c.DetailIds);
    }

    [Fact]
    public async Task LoadMatchDetails_says_so_when_league_has_no_such_game()
    {
        using var c = new Commands();
        await c.ReadProfileAsync();

        var result = await c.Run(CommandType.LoadMatchDetails, 4000000003); // listed, but the client answers 404

        Assert.Equal((false, "League couldn't load that game."), (result.Success, result.Error));
        Assert.Empty(c.DetailIds);
    }

    [Fact]
    public async Task RefreshProfile_rereads_at_most_every_thirty_seconds_and_republishes_meanwhile()
    {
        using var c = new Commands();

        Assert.True((await c.Run(CommandType.RefreshProfile)).Success);
        Assert.Equal(ProfileSamples.Own.ProfileReads, c.Lcu.Requests);
        Assert.Equal("Me", c.Store.Snapshot().Profile.Summoner!.GameName);

        c.Lcu.Requests.Clear();
        c.Time.Advance(ProfileState.RefreshInterval - TimeSpan.FromSeconds(1));
        var version = c.Store.Snapshot().Version;
        Assert.True((await c.Run(CommandType.RefreshProfile)).Success);
        Assert.Empty(c.Lcu.Requests);
        Assert.Equal(version + 1, c.Store.Snapshot().Version); // republished as it was

        c.Time.Advance(TimeSpan.FromSeconds(1));
        version = c.Store.Snapshot().Version;
        Assert.True((await c.Run(CommandType.RefreshProfile)).Success);
        Assert.Equal(ProfileSamples.Own.ProfileReads, c.Lcu.Requests);
        Assert.Equal(version + 1, c.Store.Snapshot().Version); // nothing changed, still republished
        Assert.Equal(0, c.Session.Refreshes);
    }

    [Fact]
    public async Task RefreshProfile_without_a_client_republishes_the_empty_profile()
    {
        using var c = new Commands();
        c.Session.Client = null;

        var result = await c.Run(CommandType.RefreshProfile);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, c.Store.Snapshot().Version);
    }

    [Fact]
    public async Task RefreshState_republishes_the_profile_without_reading_it()
    {
        using var c = new Commands();
        await c.ReadProfileAsync();
        var version = c.Store.Snapshot().Version;

        Assert.True((await c.Run(CommandType.RefreshState)).Success);

        Assert.Equal(version + 1, c.Store.Snapshot().Version);
        Assert.Empty(c.Lcu.Requests);
    }

    private sealed class Commands : IDisposable
    {
        private readonly HttpClient _http;

        public Commands()
        {
            _http = Lcu.CreateClient();
            Session = new FakeLcuSession(new LcuClient(_http));
        }

        public FakeTimeProvider Time { get; } = new(DateTimeOffset.Parse("2026-10-23T18:00:00Z"));

        public FakeLcu Lcu { get; } = ProfileSamples.Own.Handler();

        public ProfileStore Store { get; } = new();

        public FakeLcuSession Session { get; }

        public IEnumerable<long> DetailIds => Store.Snapshot().Profile.Details.Select(d => d.GameId);

        /// <summary>Reads the profile the way the refresh loop does, then forgets those requests.</summary>
        public async Task ReadProfileAsync()
        {
            await ProfileReader.ReadAsync(Session.Client!, Store, Time, CancellationToken.None);
            Lcu.Requests.Clear();
        }

        public Task<CommandResult> Run(CommandType type, long? gameId = null) =>
            Session.Executor(Store, Time).ExecuteAsync(new RemoteCommand("r1", type, GameId: gameId), CancellationToken.None);

        public void Dispose() => _http.Dispose();
    }
}
