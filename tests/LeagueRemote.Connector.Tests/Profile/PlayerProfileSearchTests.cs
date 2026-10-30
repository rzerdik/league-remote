using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.PlayerProfileHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="CommandType.SearchPlayer"/>: the Riot ID lookup and how long a searched player stays open.</summary>
public sealed class PlayerProfileSearchTests
{
    private const string LookupPath = ProfileSamples.Player.AliasLookupPath;

    [Fact]
    public async Task SearchPlayer_looks_the_riot_id_up_then_reads_and_publishes_that_player()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookup;

        var result = await c.Search("friend", "euw");

        Assert.True(result.Success, result.Error);
        Assert.Equal([$"GET {LookupPath}?gameName=friend&tagLine=euw", MeRead, .. ProfileSamples.Player.Reads], c.Lcu.Requests);
        var player = Assert.Single(c.Players);
        Assert.Equal((FriendPuuid, "Friend", "EUW"), (player.Puuid, player.Summoner.GameName, player.Summoner.TagLine)); // League's spelling
        Assert.True(c.Store.KeepsAccess(FriendPuuid, c.Time.GetUtcNow()));
        Assert.Equal(0, c.Session.Refreshes);
    }

    [Fact]
    public async Task SearchPlayer_sends_the_riot_id_escaped()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookupNotFound;

        await c.Search("Zażółć Test", "TAG1");
        await c.Search("a&tagLine=x", "ÄÖÜ"); // allowed in a game name, so it must not end the parameter

        Assert.Equal(
            [
                $"GET {LookupPath}?gameName=Za%C5%BC%C3%B3%C5%82%C4%87%20Test&tagLine=TAG1",
                $"GET {LookupPath}?gameName=a%26tagLine%3Dx&tagLine=%C3%84%C3%96%C3%9C",
            ],
            c.Lcu.Requests);
    }

    [Fact]
    public async Task SearchPlayer_says_so_when_nobody_has_that_riot_id()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookupNotFound;

        var result = await c.Search("Nobody", "0000");

        Assert.Equal((false, "No player with that Riot ID."), (result.Success, result.Error));
        Assert.Equal([$"GET {LookupPath}?gameName=Nobody&tagLine=0000"], c.Lcu.Requests);
        Assert.Empty(c.Players);
    }

    [Fact]
    public async Task SearchPlayer_with_the_local_players_own_riot_id_reads_nothing_about_them()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookup.Replace(FriendPuuid, "ME-PUUID");

        var result = await c.Search("myself", "eune"); // League answers with the local player's puuid, in another case

        Assert.Equal((false, "That's your own Riot ID."), (result.Success, result.Error));
        Assert.Equal([$"GET {LookupPath}?gameName=myself&tagLine=eune", MeRead], c.Lcu.Requests);
        Assert.Empty(c.Players);
    }

    [Theory]
    [InlineData("ab", "EUW")]
    [InlineData("Friend#EUW", "EUW")]
    [InlineData("Friend", "E/W")]
    [InlineData("Friend", "")]
    public async Task SearchPlayer_refuses_a_malformed_riot_id_before_asking_league(string gameName, string tagLine)
    {
        using var c = new PlayerProfileHarness();

        var result = await c.Search(gameName, tagLine);

        Assert.False(result.Success);
        Assert.Empty(c.Lcu.Requests);
    }

    [Fact]
    public async Task A_searched_player_and_their_games_open_again_without_a_list_naming_them()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookup;
        c.Lcu.Routes["/lol-chat/v1/friends"] = "[]"; // a stranger, not a friend
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid");
        Assert.True((await c.Search("Friend", "EUW")).Success);
        c.Lcu.Requests.Clear();

        Assert.True((await c.Load()).Success); // the player view asks again when it opens
        Assert.Empty(c.Lcu.Requests); // read a moment ago: only republished
        Assert.True((await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId)).Success);
        Assert.Equal([MeRead, $"GET {FriendGamePath}"], c.Lcu.Requests);
    }

    [Fact]
    public async Task A_searched_player_is_refused_fifteen_minutes_after_the_search_even_without_a_profile_read()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookup;
        c.Lcu.Routes["/lol-chat/v1/friends"] = "[]"; // a stranger
        c.Lcu.Routes[FriendGamePath] = FriendGame("ally1-puuid");
        Assert.True((await c.Search("Friend", "EUW")).Success);

        // Opened again near the end: reread (older than 30 s), which must not renew the search's grant.
        c.Time.Advance(ProfileState.PlayerLifetime - TimeSpan.FromSeconds(10));
        Assert.True((await c.Load()).Success);
        c.Lcu.Requests.Clear();
        c.Time.Advance(TimeSpan.FromSeconds(10)); // no Touch, no profile read in between

        var profile = await c.Load();
        var game = await c.Run(CommandType.LoadMatchDetails, FriendPuuid, ProfileSamples.Player.GameId);

        Assert.Equal((false, "You can only view friends, your party and your team."), (profile.Success, profile.Error));
        Assert.Equal((false, "You can only view friends, your party and your team."), (game.Success, game.Error));
        Assert.Equal([FriendsRead, LobbyRead, SessionRead, FriendsRead, LobbyRead, SessionRead], c.Lcu.Requests);
    }

    [Fact]
    public async Task A_searched_player_gone_from_the_cache_needs_a_new_search()
    {
        using var c = new PlayerProfileHarness();
        c.Lcu.Routes[LookupPath] = ProfileSamples.Player.AliasLookup;
        c.Lcu.Routes["/lol-chat/v1/friends"] = "[]";
        Assert.True((await c.Search("Friend", "EUW")).Success);
        c.Time.Advance(ProfileState.PlayerLifetime);
        c.Store.Touch(c.Time.GetUtcNow()); // the Profile tab opened: the player is dropped

        var result = await c.Load();

        Assert.Equal((false, "You can only view friends, your party and your team."), (result.Success, result.Error));
    }
}
