using System.Text.Json;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.PlayerProfileHarness;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// <see cref="CommandType.LoadPlayerProfile"/> and <see cref="CommandType.LoadMatchDetails"/> with a puuid: who may be
/// opened (friends, the party, the team, teammates named in an opened game), and their games.
/// </summary>
public sealed class PlayerProfileCommandTests
{
    [Fact]
    public async Task LoadPlayerProfile_of_a_friend_checks_the_friends_list_then_reads_and_publishes_them()
    {
        using var c = new PlayerProfileHarness();
        var version = c.Store.Snapshot().Version;

        var result = await c.Load();

        Assert.True(result.Success, result.Error);
        Assert.Equal([FriendsRead, .. ProfileSamples.Player.Reads], c.Lcu.Requests);
        Assert.Equal(FriendPuuid, Assert.Single(c.Players).Puuid);
        Assert.True(c.Store.Snapshot().Version > version);
        Assert.Equal(0, c.Session.Refreshes); // the game state isn't rebuilt for it
    }

    [Fact]
    public async Task LoadPlayerProfile_of_a_party_member_checks_the_lobby()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes["/lol-lobby/v2/lobby"] = ChatSamples.Lobby;
        c.Serve("partner-puuid");

        var result = await c.Load("partner-puuid");

        Assert.True(result.Success, result.Error);
        Assert.Equal([FriendsRead, LobbyRead, .. ProfileSamples.Player.Reads.Select(r => r.Replace(FriendPuuid, "partner-puuid"))], c.Lcu.Requests);
        Assert.Equal("partner-puuid", Assert.Single(c.Players).Puuid);
    }

    [Fact]
    public async Task A_party_member_is_found_whatever_the_case_of_the_puuid()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes["/lol-lobby/v2/lobby"] = ChatSamples.Lobby;
        c.Serve("PARTNER-PUUID");

        Assert.True((await c.Load("PARTNER-PUUID")).Success);
    }

    [Fact]
    public async Task LoadPlayerProfile_of_a_teammate_league_names_checks_the_champ_select_team()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes["/lol-champ-select/v1/session"] = ChatSamples.AnonymousChampSelectSession;
        c.Serve("ally-puuid");

        var result = await c.Load("ally-puuid");

        Assert.True(result.Success, result.Error);
        Assert.Equal([FriendsRead, LobbyRead, SessionRead, .. ProfileSamples.Player.Reads.Select(r => r.Replace(FriendPuuid, "ally-puuid"))], c.Lcu.Requests);
    }

    [Theory]
    [InlineData("enemy-puuid")] // on theirTeam of the session
    [InlineData("obf-1")] // a hidden teammate's obfuscated id
    [InlineData("hidden-puuid")] // a hidden teammate whose real puuid the client left in
    [InlineData("stranger-puuid")]
    [InlineData("AAAA0001-0000-0000-0000-00000000000X")] // close to a friend's, but not one
    public async Task LoadPlayerProfile_refuses_enemies_hidden_teammates_and_strangers_without_reading_them(string puuid)
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes["/lol-lobby/v2/lobby"] = ChatSamples.Lobby;
        c.Lcu.Routes["/lol-champ-select/v1/session"] = ChatSamples.AnonymousChampSelectSession
            .Replace("\"cellId\": 4, \"puuid\": \"\"", "\"cellId\": 4, \"puuid\": \"hidden-puuid\"");
        c.Serve(puuid);

        var result = await c.Load(puuid);

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
        Assert.Empty(c.Players);
    }

    [Fact]
    public async Task A_friend_puuid_matches_in_any_case_like_chat_ids()
    {
        using var c = new PlayerProfileHarness();
        c.Serve(FriendPuuid.ToUpperInvariant());

        Assert.True((await c.Load(FriendPuuid.ToUpperInvariant())).Success);
    }

    [Fact]
    public async Task LoadPlayerProfile_says_so_when_league_has_no_such_summoner()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes.Remove(ProfileSamples.Player.SummonerPath);

        var result = await c.Load();

        Assert.Equal((false, "League couldn't load that profile."), (result.Success, result.Error));
        Assert.Empty(c.Players);
    }

    [Fact]
    public async Task LoadPlayerProfile_again_within_thirty_seconds_rechecks_but_does_not_reread()
    {
        using var c = new PlayerProfileHarness();
        await c.Load();
        c.Lcu.Requests.Clear();
        c.Time.Advance(ProfileState.RefreshInterval - TimeSpan.FromSeconds(1));
        var version = c.Store.Snapshot().Version;

        Assert.True((await c.Load()).Success);
        Assert.Equal([FriendsRead], c.Lcu.Requests);
        Assert.True(c.Store.Snapshot().Version > version); // republished, so the phone hears back

        c.Lcu.Requests.Clear();
        c.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await c.Load()).Success);
        Assert.Equal([FriendsRead, .. ProfileSamples.Player.Reads], c.Lcu.Requests);
        Assert.Equal(Now + ProfileState.RefreshInterval, Assert.Single(c.Players).LoadedAt);
    }

    [Fact]
    public async Task A_friend_removed_meanwhile_is_refused_even_though_cached()
    {
        using var c = new PlayerProfileHarness();
        await c.Load();
        c.Lcu.Routes["/lol-chat/v1/friends"] = "[]";

        var result = await c.Load();

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
    }

    // --- Their games --------------------------------------------------------------------------

    [Fact]
    public async Task LoadMatchDetails_with_a_puuid_reads_their_game_from_their_side()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid"); // the friend played with the local player
        await c.Load();
        c.Lcu.Requests.Clear();

        var result = await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId);

        Assert.True(result.Success, result.Error);
        Assert.Equal([FriendsRead, MeRead, $"GET {FriendGamePath}"], c.Lcu.Requests);
        var details = Assert.Single(Assert.Single(c.Players).Details);
        Assert.Equal(["Ally1", "Ally2", "Ally3", "Me", "Ally4"], Named(details));
        Assert.Empty(c.Store.Snapshot().Profile.Details); // not the local player's own games
        Assert.Equal(0, c.Session.Refreshes);

        // Loaded once: opening it again only rechecks the friend.
        c.Lcu.Requests.Clear();
        Assert.True((await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);
        Assert.Equal([FriendsRead], c.Lcu.Requests);
    }

    [Fact]
    public async Task A_friends_game_against_the_local_player_names_only_the_friend()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[FriendGamePath] = FriendGame("enemy1-puuid");
        await c.Load();

        Assert.True((await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);

        var json = JsonSerializer.Serialize(c.Store.Snapshot().Profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(["Enemy1"], Named(Assert.Single(c.Players[0].Details))); // the friend's in-game name
        foreach (var leak in new[] { "Enemy2", "Enemy5", "\"Me\"", "Ally", "-puuid" })
            Assert.DoesNotContain(leak, json);
    }

    [Fact]
    public async Task LoadMatchDetails_with_a_puuid_refuses_a_game_outside_their_history_and_unopened_players()
    {
        using var c = new PlayerProfileHarness();
        await c.Load();
        c.Lcu.Requests.Clear();

        var notTheirs = await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Own.RankedLossGameId);
        var notOpened = await c.Run(CommandType.LoadMatchDetails, "aaaa0003", ProfileSamples.Player.GameId);

        Assert.Equal((false, "That game isn't in their match history."), (notTheirs.Success, notTheirs.Error));
        Assert.Equal((false, "That game isn't in their match history."), (notOpened.Success, notOpened.Error));
        Assert.Empty(c.Lcu.Requests);
    }

    [Fact]
    public async Task LoadMatchDetails_with_a_puuid_rechecks_the_player()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid");
        await c.Load();
        c.Lcu.Routes["/lol-chat/v1/friends"] = "[]";
        c.Lcu.Requests.Clear();

        var result = await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId);

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
    }

    [Fact]
    public async Task LoadMatchDetails_with_an_empty_puuid_is_the_local_players_own_game()
    {
        using var c = new PlayerProfileHarness();
        await ProfileReader.ReadAsync(c.Session.Client!, c.Store, c.Time, CancellationToken.None);
        c.Lcu.Requests.Clear();

        var result = await c.Run(CommandType.LoadMatchDetails, "", ProfileSamples.Own.RankedLossGameId);

        Assert.True(result.Success, result.Error);
        Assert.Equal([MeRead, $"GET {ProfileSamples.Own.GamePath}"], c.Lcu.Requests);
        Assert.Single(c.Store.Snapshot().Profile.Details);
    }

    // --- Teammates the connector named in a game ----------------------------------------------

    [Fact]
    public async Task A_teammate_named_in_an_opened_game_opens_without_asking_league_who_they_are()
    {
        using var c = new PlayerProfileHarness();
        await c.OpenOwnGameAsync();
        c.Serve("ally2-puuid");

        var result = await c.Load("ally2-puuid");

        Assert.True(result.Success, result.Error);
        // No friends / lobby / champ-select reads: the connector named them itself.
        Assert.Equal([.. ProfileSamples.Player.Reads.Select(r => r.Replace(FriendPuuid, "ally2-puuid"))], c.Lcu.Requests);
        Assert.Equal("ally2-puuid", Assert.Single(c.Players).Puuid);
        Assert.True(c.Store.KeepsAccess("ALLY2-PUUID", c.Time.GetUtcNow()));
    }

    [Theory]
    [InlineData("enemy1-puuid")] // in that game, on the other team
    [InlineData("enemy5-puuid")]
    [InlineData("me-puuid-x")]
    public async Task An_enemy_of_an_opened_game_is_still_refused(string puuid)
    {
        using var c = new PlayerProfileHarness();
        await c.OpenOwnGameAsync();
        c.Serve(puuid);

        var result = await c.Load(puuid);

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
        Assert.Empty(c.Players);
    }

    [Fact]
    public async Task A_teammate_on_the_post_game_screen_opens_and_an_enemy_there_does_not()
    {
        using var c = new PlayerProfileHarness();
        c.State.Set(ClientState.Disconnected() with { PostGame = PostGameMapper.Map(Json(PostGameSamples.RankedWin)) });
        c.Serve("ally1-puuid");
        c.Serve("enemy1-puuid");

        var ally = await c.Load("ally1-puuid");
        var allyReads = c.Lcu.Requests.ToList();
        c.Lcu.Requests.Clear();
        var enemy = await c.Load("enemy1-puuid");

        Assert.True(ally.Success, ally.Error);
        Assert.Equal([.. ProfileSamples.Player.Reads.Select(r => r.Replace(FriendPuuid, "ally1-puuid"))], allyReads);
        Assert.Equal((false, "You can only view friends, your party and your team."), (enemy.Success, enemy.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
    }

    [Fact]
    public async Task A_teammate_named_in_a_players_game_opens_too_but_not_once_that_game_named_nobody()
    {
        using var c = new PlayerProfileHarness();
        // The friend played with the local player: their teammates are named (and openable).
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid");
        await c.Load();
        Assert.True((await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);
        c.Serve("ally3-puuid");
        c.Lcu.Requests.Clear();

        Assert.True((await c.Load("ally3-puuid")).Success);
        Assert.DoesNotContain(FriendsRead, c.Lcu.Requests);

        // The friend played against the local player: nobody but the friend is named, so nobody else opens.
        using var against = new PlayerProfileHarness();
        against.Lcu.Routes[FriendGamePath] = FriendGame("enemy1-puuid");
        await against.Load();
        Assert.True((await against.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);
        against.Serve("enemy2-puuid");
        Assert.False((await against.Load("enemy2-puuid")).Success);
    }

    [Fact]
    public async Task A_teammate_opened_from_a_game_keeps_their_games_while_cached()
    {
        using var c = new PlayerProfileHarness();
        await c.OpenOwnGameAsync();
        c.Serve("ally2-puuid");
        Assert.True((await c.Load("ally2-puuid")).Success);
        // Their newest game is the recorded game, where they are Ally2 beside the local player.
        c.Lcu.Routes[FriendGamePath] = ProfileSamples.Own.Game.Replace("4000000004", "4100000003");
        c.Lcu.Requests.Clear();

        var result = await c.Run(CommandType.LoadMatchDetails, "ally2-puuid", ProfileSamples.Player.GameId);

        Assert.True(result.Success, result.Error);
        Assert.Equal([MeRead, $"GET {FriendGamePath}"], c.Lcu.Requests); // no list re-check
        var game = Assert.Single(c.Players[0].Details);
        Assert.Equal(["ally1-puuid", "ally3-puuid", "ally4-puuid"], Puuids(game.Teams.SelectMany(t => t.Participants)));
    }

    [Fact]
    public async Task A_teammate_from_a_game_is_refused_once_that_grant_and_the_game_are_gone()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid");
        await c.Load(); // the friend
        Assert.True((await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);
        c.Serve("ally3-puuid");
        Assert.True((await c.Load("ally3-puuid")).Success); // named in the friend's game
        c.Lcu.Requests.Clear();
        c.Time.Advance(ProfileState.PlayerLifetime); // the friend's profile (and its game) and the grant expire together

        var result = await c.Load("ally3-puuid");

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
    }
}
