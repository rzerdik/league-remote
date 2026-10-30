using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.PlayerProfileHarness;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Other players' profiles (<see cref="ProfileState.Players"/>) as they are read and mapped: their games named from their
/// side, which teammates carry a puuid, the message size, and what the game state tells the phone about players.
/// </summary>
public sealed class PlayerProfileMapperTests
{
    private static async Task<PlayerProfile?> Read(FakeLcu handler, string puuid = FriendPuuid)
    {
        using var http = handler.CreateClient();
        return await ProfileReader.BuildPlayerAsync(new LcuClient(http), puuid, Now, CancellationToken.None);
    }

    private static MatchDetails PlayerGame(string playerPuuid, string? localPuuid, string? json = null) =>
        ProfileMapper.PlayerDetails(Json(json ?? ProfileSamples.Own.Game), playerPuuid, localPuuid)!;

    // --- Reading a profile --------------------------------------------------------------------

    [Fact]
    public async Task A_players_profile_has_their_summoner_ranks_mastery_and_games()
    {
        var handler = ProfileSamples.Player.Handler();

        var player = (await Read(handler))!;

        Assert.Equal(ProfileSamples.Player.Reads, handler.Requests);
        Assert.Equal(FriendPuuid, player.Puuid);
        Assert.Equal(("Friend", "EUW", 456, 7210), (player.Summoner.GameName, player.Summoner.TagLine, player.Summoner.Level, player.Summoner.ProfileIconId));
        Assert.Equal([RankedQueueTypes.Solo, RankedQueueTypes.Flex], player.Ranks.Select(r => r.QueueType));
        Assert.Equal((65, 3), (player.Mastery.Score, player.Mastery.ChampionCount)); // the sum of champion levels
        Assert.Equal([222, 412, 1], player.Mastery.TopChampions.Select(m => m.ChampionId));
        Assert.Equal([4100000003L, 4100000002L, 4100000001L], player.Matches.Select(m => m.GameId));
        Assert.All(player.Matches, m => Assert.Equal(("Friend", true), (m.Me.GameName, m.Me.IsMe)));
        Assert.Equal([MatchResults.Loss, MatchResults.Win, MatchResults.Loss], player.Matches.Select(m => m.Result));
        // A roaming support comes as lane NONE + role SUPPORT; Arena's lane guess is passed on as it is.
        Assert.Equal([Positions.Top, Positions.Utility, Positions.Middle], player.Matches.Select(m => m.Position));
        Assert.Equal(5, player.Matches[2].Me.Placement);
        Assert.Empty(player.Details);
        Assert.Equal(Now, player.LoadedAt);
    }

    [Fact]
    public async Task No_summoner_for_that_puuid_means_no_profile_and_no_further_reads()
    {
        var handler = ProfileSamples.Player.Handler();
        handler.Routes.Remove(ProfileSamples.Player.SummonerPath);

        Assert.Null(await Read(handler));
        Assert.Equal([$"GET {ProfileSamples.Player.SummonerPath}"], handler.Requests);
    }

    [Fact]
    public async Task A_part_the_client_does_not_answer_is_empty()
    {
        var handler = ProfileSamples.Player.Handler();
        handler.Routes.Remove(ProfileSamples.Player.MasteryPath);
        handler.Routes.Remove(ProfileSamples.Player.MatchesPath);

        var player = (await Read(handler))!;

        Assert.Equal((0, 0, 0), (player.Mastery.Score, player.Mastery.ChampionCount, player.Mastery.TopChampions.Count));
        Assert.Empty(player.Matches);
        Assert.NotEmpty(player.Ranks);
    }

    // --- Their games: names from their side, never the local player's enemies -----------------

    [Fact]
    public void A_game_the_local_player_was_not_in_names_the_players_team_only()
    {
        // The sample's "Enemy1" is the player here; the local player (me-puuid) isn't in this game.
        var details = PlayerGame("enemy1-puuid", "someone-else", ProfileSamples.Own.Game.Replace("me-puuid", "stranger-puuid"));

        Assert.Equal([(200, true), (100, false)], details.Teams.Select(t => (t.TeamId, t.IsMyTeam)));
        Assert.Equal(["Enemy1", "Enemy2", "Enemy3", "Enemy4", "Enemy5"], Named(details));
        Assert.Equal("Enemy1", Assert.Single(details.Teams.SelectMany(t => t.Participants), p => p.IsMe).GameName);
    }

    [Fact]
    public void A_game_the_local_player_played_on_the_players_team_names_that_team()
    {
        var details = PlayerGame("ally1-puuid", ProfileSamples.Own.MyPuuid);

        Assert.Equal(["Ally1", "Ally2", "Ally3", "Me", "Ally4"], Named(details));
        Assert.Equal("Ally1", Assert.Single(details.Teams.SelectMany(t => t.Participants), p => p.IsMe).GameName);
    }

    [Fact]
    public void A_game_the_local_player_played_against_the_player_names_nobody_but_them()
    {
        // The player's teammates were the local player's enemies in that game.
        var details = PlayerGame("enemy1-puuid", ProfileSamples.Own.MyPuuid);

        Assert.Equal(["Enemy1"], Named(details));
        Assert.Equal([(200, true), (100, false)], details.Teams.Select(t => (t.TeamId, t.IsMyTeam)));
        Assert.All(details.Teams.SelectMany(t => t.Participants), p => Assert.Equal(p.IsMe ? "TAG1" : "", p.TagLine));
    }

    [Fact]
    public void Without_the_local_players_puuid_nobody_but_the_player_is_named()
    {
        // The local player may have been on the other side: fail closed.
        Assert.Equal(["Enemy1"], Named(PlayerGame("enemy1-puuid", null)));
    }

    [Theory]
    [InlineData("ally1-puuid", new[] { "Ally1", "Me" })] // the local player's duo
    [InlineData("enemy1-puuid", new[] { "Enemy1" })] // same LCU team as the local player, another duo
    [InlineData("enemy3-puuid", new[] { "Enemy3" })]
    public void Arena_names_the_players_duo_only_when_it_is_the_local_players(string playerPuuid, string[] named)
    {
        var details = PlayerGame(playerPuuid, ProfileSamples.Own.MyPuuid, ProfileSamples.Own.ArenaGame);

        Assert.Equal(named.Order(), Named(details).Order());
    }

    // --- Teammates the connector names in a game ----------------------------------------------

    private static readonly IReadOnlyList<string> AllyPuuids = ["ally1-puuid", "ally2-puuid", "ally3-puuid", "ally4-puuid"];

    [Fact]
    public void An_own_game_carries_the_puuids_of_named_teammates_only()
    {
        var details = ProfileMapper.Details(Json(ProfileSamples.Own.Game), ProfileSamples.Own.MyPuuid)!;
        var lines = details.Teams.SelectMany(t => t.Participants).ToList();

        Assert.Equal(AllyPuuids, Puuids(lines));
        Assert.All(lines, p => Assert.Equal(p.Puuid != "", p.GameName != "" && !p.IsMe)); // a puuid only beside a name, never yours
        Assert.All(details.Teams.Where(t => !t.IsMyTeam).SelectMany(t => t.Participants), p => Assert.Equal(("", ""), (p.GameName, p.Puuid)));
    }

    [Fact]
    public void A_players_game_carries_their_teammates_puuids_but_not_theirs_nor_the_local_players()
    {
        var withYou = PlayerGame("ally1-puuid", ProfileSamples.Own.MyPuuid);
        var againstYou = PlayerGame("enemy1-puuid", ProfileSamples.Own.MyPuuid);
        var unknownYou = PlayerGame("ally1-puuid", null);

        Assert.Equal(["ally2-puuid", "ally3-puuid", "ally4-puuid"], Puuids(withYou.Teams.SelectMany(t => t.Participants)));
        Assert.Empty(Puuids(againstYou.Teams.SelectMany(t => t.Participants))); // their teammates were your enemies
        Assert.Empty(Puuids(unknownYou.Teams.SelectMany(t => t.Participants)));
    }

    [Fact]
    public void An_arena_game_carries_only_the_duo_partners_puuid()
    {
        var details = ProfileMapper.Details(Json(ProfileSamples.Own.ArenaGame), ProfileSamples.Own.MyPuuid)!;

        Assert.Equal(["ally1-puuid"], Puuids(details.Teams.SelectMany(t => t.Participants)));
    }

    [Fact]
    public void The_post_game_screen_carries_the_puuids_of_named_teammates_only()
    {
        var summary = PostGameMapper.Map(Json(PostGameSamples.RankedWin))!;

        Assert.Equal(["ally1-puuid"], Puuids(summary.Teams.SelectMany(t => t.Participants)));
        var json = JsonSerializer.Serialize(summary);
        foreach (var leak in new[] { "enemy1-puuid", "enemy2-puuid", "me-puuid" })
            Assert.DoesNotContain(leak, json);
    }

    [Fact]
    public void The_post_game_screen_carries_no_puuid_when_it_cannot_tell_which_line_is_yours()
    {
        // Neither flagged nor matched by puuid: nobody is known to be a teammate.
        var block = PostGameSamples.RankedWin.Replace("\"isLocalPlayer\": true", "\"isLocalPlayer\": false")
            .Replace("\"localPlayer\": { \"puuid\": \"me-puuid\"", "\"localPlayer\": { \"puuid\": \"someone-else\"");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.DoesNotContain(summary.Teams.SelectMany(t => t.Participants), p => p.IsMe);
        Assert.Empty(Puuids(summary.Teams.SelectMany(t => t.Participants)));
    }

    [Fact]
    public void The_post_game_screen_never_carries_the_local_players_puuid()
    {
        // The local player's line is flagged, but an ally's line carries their puuid too (odd data).
        var block = PostGameSamples.RankedWin.Replace("\"puuid\": \"ally1-puuid\"", "\"puuid\": \"ME-PUUID\"");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.Empty(Puuids(summary.Teams.SelectMany(t => t.Participants)));
    }

    // --- Size ---------------------------------------------------------------------------------

    [Fact]
    public void A_full_profile_with_three_players_stays_below_its_message_budget()
    {
        // Every list at its cap, every Riot ID at its longest and escaped by the serializer (non-ASCII), every number large,
        // and every line named, with a puuid beside every name but yours (the connector names only one team; this is the
        // bound, not a real message). Puuids at the longest the whitelist accepts (live ones are 36 characters).
        var name = new string('Ü', RiotIds.MaxGameNameLength);
        var tag = new string('Ü', RiotIds.MaxTagLineLength);
        var puuid = new string('a', RiotIds.MaxPuuidLength);
        MatchParticipant Line(bool me) =>
            new(999, me, name, tag, 18, 99, 99, 99, 999, 99_999, 999_999, 999, [9999, 9999, 9999, 9999, 9999, 9999, 9999], 9999, 9999, 9999, 9999, 9999, 8,
                me ? "" : puuid);
        MatchSummary Summary(long id) =>
            new(4_000_000_000 + id, 9999, "PRACTICETOOL", "MATCHED_GAME", 30, Now, 99_999, MatchResults.Remake, Positions.Utility, Line(true));
        // Arena is the biggest game: 8 duos = 16 lines (a Summoner's Rift game has 10), every one named here.
        MatchDetails Full(long id) =>
            new(4_000_000_000 + id, [.. Enumerable.Range(0, 8).Select(t => new MatchTeam(100, t == 0, true, 999, 999_999, 11, 3, 9, 9, 9, 9, [999, 999, 999, 999, 999],
                [.. Enumerable.Range(0, 2).Select(i => Line(i == 0))]))]);
        var ranks = Enumerable.Range(0, 4).Select(_ => new RankedQueueStats("JADE_RANKED_SOLO_5x5", "GRANDMASTER", "IV", 9999, 9999, 9999, true, 5, "WLNNN", "GRANDMASTER", "IV", "GRANDMASTER", "IV")).ToList();
        var mastery = new ProfileMastery(9999, 999, [.. Enumerable.Range(0, ProfileState.MaxTopChampions).Select(_ => new ChampionMastery(999, 999, 9_999_999, 99_999, 99_999, "S+", Now))]);
        var summoner = new ProfileSummoner(name, tag, 9999, 99_999, 99_999, 99_999);
        var matches = Enumerable.Range(0, ProfileState.MaxMatches).Select(i => Summary(i)).ToList();
        var players = Enumerable.Range(0, ProfileState.MaxPlayers)
            .Select(i => new PlayerProfile(puuid, summoner, ranks, mastery, matches,
                [.. Enumerable.Range(0, ProfileState.MaxPlayerDetails).Select(j => Full(j))], Now))
            .ToList();
        var profile = new ProfileState(summoner, ranks, mastery, matches, [.. Enumerable.Range(0, ProfileState.MaxDetails).Select(j => Full(j))], Now, players);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length;

        Assert.True(bytes <= ProfileState.MaxJsonBytes, $"profile: {bytes} bytes");
    }

    // --- What the phone learns about players in the game state -------------------------------

    [Fact]
    public void Lobby_members_carry_their_puuid()
    {
        var lobby = LobbyMapper.Map(Json(ChatSamples.Lobby), null, null)!;

        Assert.Equal(["me-puuid", "partner-puuid"], lobby.Members.Select(m => m.Puuid));
    }

    [Fact]
    public void Teammates_carry_a_puuid_only_while_league_names_them_and_enemies_never()
    {
        var cs = ChampSelectMapper.Map(Json(ChatSamples.AnonymousChampSelectSession
            .Replace("\"cellId\": 4, \"puuid\": \"\"", "\"cellId\": 4, \"puuid\": \"hidden-puuid\"")), [], [])!;

        Assert.Equal([(1L, "ally-puuid"), (2L, "me-puuid"), (3L, ""), (4L, "")], cs.MyTeam.Select(m => (m.CellId, m.Puuid)));
        var json = JsonSerializer.Serialize(cs);
        foreach (var leak in new[] { "enemy-puuid", "obf-", "hidden-puuid", "Leaked", "Enemy" })
            Assert.DoesNotContain(leak, json);
    }

    [Fact]
    public void Teammates_are_named_only_while_league_names_them_and_enemies_never()
    {
        // Cell 4 is hidden although a name slipped into its entry; a teammate without a tag line keeps the game name.
        var cs = ChampSelectMapper.Map(Json(ChatSamples.AnonymousChampSelectSession
            .Replace("\"gameName\": \"Ally\", \"tagLine\": \"EUW\"", "\"gameName\": \"Ally\", \"tagLine\": \"\"")), [], [])!;

        Assert.Equal([(1L, "Ally"), (2L, "Me#EUNE"), (3L, ""), (4L, "")], cs.MyTeam.Select(m => (m.CellId, m.RiotId)));
        var json = JsonSerializer.Serialize(cs);
        foreach (var leak in new[] { "Leaked", "Enemy" })
            Assert.DoesNotContain(leak, json);
    }

    [Fact]
    public void A_visible_teammate_is_named_by_riot_id()
    {
        var cs = ChampSelectMapper.Map(Json(ChatSamples.AnonymousChampSelectSession), [], [])!;

        Assert.Equal("Ally#EUW", cs.MyTeam.Single(m => m.CellId == 1).RiotId);
    }
}
