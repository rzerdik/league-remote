using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="ProfileStore"/>: the published profile, the opened games and the cached other players.</summary>
public sealed class ProfileStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-23T18:00:00Z");

    private static MatchSummary Summary(long gameId) =>
        new(gameId, 420, "CLASSIC", "MATCHED_GAME", 11, DateTimeOffset.UnixEpoch, 1500, MatchResults.Win, "",
            new MatchParticipant(1, true, "Me", "EUNE", 18, 1, 2, 3, 200, 10000, 20000, 10, [0, 0, 0, 0, 0, 0, 0], 4, 14, 0, 0, 0, 0, ""));

    private static ProfileState WithMatches(params long[] gameIds) =>
        ProfileState.Empty() with { Matches = [.. gameIds.Select(Summary)] };

    private static PlayerProfile Player(string puuid, DateTimeOffset? loadedAt = null, params long[] gameIds) =>
        new(puuid, new ProfileSummoner(puuid, "TAG", 30, 0, 0, 1), [], ProfileMastery.Empty,
            [.. gameIds.Select(id => new MatchSummary(id, 420, "CLASSIC", "MATCHED_GAME", 11, Now, 1500, MatchResults.Win, "",
                new MatchParticipant(1, true, puuid, "TAG", 18, 1, 2, 3, 200, 10000, 20000, 10, [0, 0, 0, 0, 0, 0, 0], 4, 14, 0, 0, 0, 0, "")))],
            [], loadedAt ?? Now);

    private static MatchDetails Game(long gameId) => new(gameId, []);

    // --- The local player's profile -----------------------------------------------------------

    [Fact]
    public void Store_ignores_a_reread_that_only_changed_the_timestamp()
    {
        var store = new ProfileStore();
        var changes = 0;
        store.Changed += () => changes++;

        Assert.True(store.Set(WithMatches(1, 2) with { UpdatedAt = DateTimeOffset.UnixEpoch }, DateTimeOffset.UnixEpoch));
        var (_, version) = store.Snapshot();
        Assert.False(store.Set(WithMatches(1, 2) with { UpdatedAt = DateTimeOffset.UtcNow }, DateTimeOffset.UtcNow));

        Assert.Equal(version, store.Snapshot().Version);
        Assert.Equal(1, changes);

        store.Touch();
        Assert.Equal(version + 1, store.Snapshot().Version);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Store_keeps_three_details_the_most_recently_requested_first()
    {
        var store = new ProfileStore();
        store.Set(WithMatches(1, 2, 3, 4, 5, 6), DateTimeOffset.UnixEpoch);

        foreach (var id in new long[] { 1, 2, 3, 4 })
            Assert.True(store.AddDetails(new MatchDetails(id, [])));

        Assert.Equal([4L, 3, 2], store.Snapshot().Profile.Details.Select(d => d.GameId));

        Assert.True(store.ShowDetails(2));
        Assert.False(store.ShowDetails(1)); // dropped as the one requested longest ago
        Assert.Equal([2L, 4, 3], store.Snapshot().Profile.Details.Select(d => d.GameId));
    }

    [Fact]
    public void Store_drops_details_of_games_that_left_the_match_history()
    {
        var store = new ProfileStore();
        store.Set(WithMatches(1, 2), DateTimeOffset.UnixEpoch);
        store.AddDetails(new MatchDetails(1, []));
        store.AddDetails(new MatchDetails(2, []));

        store.Set(WithMatches(3, 2), DateTimeOffset.UnixEpoch);

        Assert.Equal([2L], store.Snapshot().Profile.Details.Select(d => d.GameId));
        Assert.False(store.AddDetails(new MatchDetails(1, []))); // not in the matches any more
        store.Set(ProfileState.Empty(), readAt: null); // the client went away
        Assert.Empty(store.Snapshot().Profile.Details);
        Assert.Null(store.ReadAt);
    }

    // --- Other players ------------------------------------------------------------------------

    [Fact]
    public void Store_keeps_three_players_the_most_recently_opened_first()
    {
        var store = new ProfileStore();
        foreach (var puuid in new[] { "p1", "p2", "p3", "p4" })
            store.AddPlayer(Player(puuid));

        Assert.Equal(["p4", "p3", "p2"], store.Snapshot().Profile.Players!.Select(p => p.Puuid));

        Assert.True(store.ShowPlayer("p2"));
        Assert.False(store.ShowPlayer("p1"));
        Assert.Equal(["p2", "p4", "p3"], store.Snapshot().Profile.Players!.Select(p => p.Puuid));
    }

    [Fact]
    public void Store_keeps_one_game_per_player_and_drops_it_when_it_leaves_their_history()
    {
        var store = new ProfileStore();
        store.AddPlayer(Player("p1", null, 1, 2, 3, 4));
        Assert.True(store.AddPlayerDetails("p1", Game(1)));
        Assert.True(store.AddPlayerDetails("p1", Game(4))); // replaces game 1
        Assert.False(store.AddPlayerDetails("p1", Game(99))); // not in their history
        Assert.False(store.AddPlayerDetails("p9", Game(1))); // not opened

        Assert.Equal([4L], store.Player("p1")!.Details.Select(d => d.GameId));
        Assert.True(store.ShowPlayerDetails("p1", 4));
        Assert.False(store.ShowPlayerDetails("p1", 1)); // dropped: it is read again

        store.AddPlayer(Player("p1", null, 4, 5, 6)); // reread: game 4 is still in their history
        Assert.Equal([4L], store.Player("p1")!.Details.Select(d => d.GameId));
        store.AddPlayer(Player("p1", null, 5, 6, 7)); // and now it isn't
        Assert.Empty(store.Player("p1")!.Details);
    }

    [Fact]
    public void Access_from_a_game_or_a_search_goes_with_the_cached_profile()
    {
        var store = new ProfileStore();
        store.AddPlayer(Player("p1"), grantedAt: Now);
        store.AddPlayer(Player("p2"));
        Assert.True(store.KeepsAccess("P1", Now));
        Assert.False(store.KeepsAccess("p2", Now));

        store.AddPlayer(Player("p1")); // a reread keeps it
        Assert.True(store.KeepsAccess("p1", Now));
        Assert.True(store.ShowPlayer("p2", grantedAt: Now)); // opened from a game while cached
        Assert.True(store.KeepsAccess("p2", Now));

        foreach (var puuid in new[] { "p3", "p4", "p5" })
            store.AddPlayer(Player(puuid));
        Assert.False(store.KeepsAccess("p1", Now)); // dropped by the cap
        store.AddPlayer(Player("p1"));
        Assert.False(store.KeepsAccess("p1", Now)); // back as a friend, say: re-checked again

        store.AddPlayer(Player("p6"), grantedAt: Now);
        store.Set(ProfileState.Empty(), readAt: null); // disconnect
        Assert.False(store.KeepsAccess("p6", Now));
    }

    [Fact]
    public void Access_lasts_fifteen_minutes_from_the_grant_whatever_rereads_happen()
    {
        var store = new ProfileStore();
        store.AddPlayer(Player("p1"), grantedAt: Now);
        var almost = Now + ProfileState.PlayerLifetime - TimeSpan.FromSeconds(1);
        store.AddPlayer(Player("p1", almost)); // reread just before the end, no new grant

        Assert.True(store.KeepsAccess("p1", almost));
        Assert.False(store.KeepsAccess("p1", Now + ProfileState.PlayerLifetime)); // without any Touch or Set

        store.ShowPlayer("p1", grantedAt: Now + ProfileState.PlayerLifetime); // named in a game again: a new grant
        Assert.True(store.KeepsAccess("p1", Now + ProfileState.PlayerLifetime));
    }

    [Fact]
    public void An_expired_players_games_name_nobody()
    {
        var store = new ProfileStore();
        var line = new MatchParticipant(1, false, "Mate", "TAG", 18, 1, 2, 3, 200, 10000, 20000, 10, [0, 0, 0, 0, 0, 0, 0], 4, 14, 0, 0, 0, 0, "mate-puuid");
        store.AddPlayer(Player("p1", Now, 7));
        Assert.True(store.AddPlayerDetails("p1", new MatchDetails(7, [new MatchTeam(100, true, true, 0, 0, 0, 0, 0, 0, 0, 0, [], [line])])));

        Assert.True(store.NamesTeammate("mate-puuid", Now));
        Assert.False(store.NamesTeammate("mate-puuid", Now + ProfileState.PlayerLifetime));
    }

    [Fact]
    public void Players_are_dropped_fifteen_minutes_after_they_were_read_and_on_disconnect()
    {
        var store = new ProfileStore();
        store.AddPlayer(Player("old", Now - ProfileState.PlayerLifetime));
        store.AddPlayer(Player("new", Now - ProfileState.PlayerLifetime + TimeSpan.FromSeconds(1)));

        store.Set(ProfileState.Empty(), Now);
        Assert.Equal(["new"], store.Snapshot().Profile.Players!.Select(p => p.Puuid));

        store.Touch(Now + TimeSpan.FromSeconds(1)); // the Profile tab opened without a reread
        Assert.Empty(store.Snapshot().Profile.Players!);

        store.AddPlayer(Player("p1"));
        store.Set(ProfileState.Empty(), readAt: null); // the client went away
        Assert.Empty(store.Snapshot().Profile.Players!);
    }
}
