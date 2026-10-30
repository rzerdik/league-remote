using System.Text.Json;
using System.Text.Json.Nodes;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>The local player's profile as it is read and mapped: summoner, ranks, mastery, match history and game details.</summary>
public sealed class ProfileMapperTests
{
    private static Task<ProfileState> Read(FakeLcu handler) => ReadProfileAsync(handler);

    private static async Task<ProfileState> ReadWith(string path, string json)
    {
        var handler = ProfileSamples.Own.Handler();
        handler.Routes[path] = json;
        return await Read(handler);
    }

    private static RankedQueueStats Rank(ProfileState profile, string queueType) => profile.Ranks.Single(r => r.QueueType == queueType);

    // --- Summoner, ranks, mastery -------------------------------------------------------------

    [Fact]
    public async Task Summoner_has_riot_id_level_icon_and_the_levels_total_xp()
    {
        var summoner = (await Read(ProfileSamples.Own.Handler())).Summoner!;

        Assert.Equal(new ProfileSummoner("Me", "EUNE", 123, 668, 3648, 29), summoner);
    }

    [Fact]
    public async Task No_logged_in_summoner_means_no_summoner()
    {
        var handler = ProfileSamples.Own.Handler();
        handler.Routes.Remove(ProfileSamples.Own.SummonerPath);

        Assert.Null((await Read(handler)).Summoner);
    }

    [Fact]
    public async Task Ranks_are_solo_then_flex_without_tft_or_unranked_other_queues()
    {
        var profile = await Read(ProfileSamples.Own.Handler());

        Assert.Equal([RankedQueueTypes.Solo, RankedQueueTypes.Flex], profile.Ranks.Select(r => r.QueueType));
        Assert.Equal(
            new RankedQueueStats(RankedQueueTypes.Solo, "PLATINUM", "IV", 52, 230, 236, false, 0, "", "EMERALD", "III", "EMERALD", "IV"),
            profile.Ranks[0]);
        Assert.Equal(("PLATINUM", "I", 81, 12, 9), (profile.Ranks[1].Tier, profile.Ranks[1].Division, profile.Ranks[1].LeaguePoints, profile.Ranks[1].Wins, profile.Ranks[1].Losses));
    }

    [Fact]
    public async Task Unranked_solo_is_still_listed_with_empty_tier_and_division()
    {
        var profile = await ReadWith(ProfileSamples.Own.RankedPath, ProfileSamples.Own.RankedStats.Replace(
            "\"queueType\": \"RANKED_SOLO_5x5\", \"tier\": \"PLATINUM\", \"division\": \"IV\"",
            "\"queueType\": \"RANKED_SOLO_5x5\", \"tier\": \"\", \"division\": \"NA\""));

        var solo = profile.Ranks[0];
        Assert.Equal((RankedQueueTypes.Solo, "", "", 0), (solo.QueueType, solo.Tier, solo.Division, solo.LeaguePoints));
    }

    [Fact]
    public async Task Solo_and_flex_are_listed_even_when_the_client_leaves_them_out()
    {
        var profile = await ReadWith(ProfileSamples.Own.RankedPath, """{"queues": []}""");

        Assert.Equal([RankedQueueTypes.Solo, RankedQueueTypes.Flex], profile.Ranks.Select(r => r.QueueType));
        Assert.All(profile.Ranks, r => Assert.Equal(("", "", 0, 0), (r.Tier, r.Division, r.Wins, r.Losses)));
    }

    [Fact]
    public async Task Another_league_queue_with_a_tier_follows_flex_but_tft_never_shows()
    {
        var ranked = ProfileSamples.Own.RankedStats
            .Replace("\"queueType\": \"JADE_RANKED_SOLO_5x5\", \"tier\": \"\", \"division\": \"NA\"",
                "\"queueType\": \"JADE_RANKED_SOLO_5x5\", \"tier\": \"GOLD\", \"division\": \"II\"")
            .Replace("\"queueType\": \"RANKED_TFT\", \"tier\": \"\"", "\"queueType\": \"RANKED_TFT\", \"tier\": \"DIAMOND\"");

        var profile = await ReadWith(ProfileSamples.Own.RankedPath, ranked);

        Assert.Equal([RankedQueueTypes.Solo, RankedQueueTypes.Flex, "JADE_RANKED_SOLO_5x5"], profile.Ranks.Select(r => r.QueueType));
        Assert.Equal(("GOLD", "II"), (profile.Ranks[2].Tier, profile.Ranks[2].Division));
    }

    [Fact]
    public async Task Apex_tiers_have_no_division()
    {
        var profile = await ReadWith(ProfileSamples.Own.RankedPath, ProfileSamples.Own.RankedStats.Replace(
            "\"tier\": \"PLATINUM\", \"division\": \"IV\"", "\"tier\": \"MASTER\", \"division\": \"I\""));

        Assert.Equal(("MASTER", ""), (Rank(profile, RankedQueueTypes.Solo).Tier, Rank(profile, RankedQueueTypes.Solo).Division));
    }

    [Fact]
    public async Task Ranks_are_empty_when_the_ranked_service_does_not_answer()
    {
        var handler = ProfileSamples.Own.Handler();
        handler.Routes.Remove(ProfileSamples.Own.RankedPath);

        Assert.Empty((await Read(handler)).Ranks);
    }

    [Fact]
    public async Task Mastery_has_the_score_the_champion_count_and_the_top_ten_by_points()
    {
        var mastery = (await Read(ProfileSamples.Own.Handler())).Mastery;

        Assert.Equal((540, 12), (mastery.Score, mastery.ChampionCount));
        Assert.Equal([51, 67, 18, 236, 222, 96, 81, 22, 145, 804], mastery.TopChampions.Select(c => c.ChampionId));
        Assert.Equal(
            new ChampionMastery(51, 57, 590000, 2107, 8893, "S+", DateTimeOffset.FromUnixTimeMilliseconds(1792621129000)),
            mastery.TopChampions[0]);
        Assert.Equal("", mastery.TopChampions[1].HighestGrade);
    }

    [Fact]
    public void Mastery_is_sorted_by_points_whatever_order_the_client_sends()
    {
        var reversed = JsonSerializer.Serialize(Json(ProfileSamples.Own.Mastery).EnumerateArray().Reverse());

        var mastery = ProfileMapper.Mastery(Json(reversed), Json(ProfileSamples.Own.MasteryScore));

        Assert.Equal(51, mastery.TopChampions[0].ChampionId);
        Assert.Equal(ProfileState.MaxTopChampions, mastery.TopChampions.Count);
    }

    [Fact]
    public async Task A_part_the_client_refuses_keeps_its_previous_value()
    {
        var good = await Read(ProfileSamples.Own.Handler());
        var handler = ProfileSamples.Own.Handler();
        handler.Routes.Remove(ProfileSamples.Own.MasteryScorePath);
        handler.Routes[ProfileSamples.Own.RankedPath] = "not json";

        using var http = handler.CreateClient();
        var profile = await ProfileReader.BuildAsync(new LcuClient(http), DateTimeOffset.UtcNow, CancellationToken.None, good);

        Assert.Equal(good.Ranks, profile.Ranks); // broken answer: the last ranks stay
        Assert.Equal(0, profile.Mastery.Score); // 404: the client says there is none
    }

    // --- Match history ------------------------------------------------------------------------

    [Fact]
    public async Task Matches_are_newest_first_with_how_each_game_ended()
    {
        var matches = (await Read(ProfileSamples.Own.Handler())).Matches;

        Assert.Equal([4000000004L, 4000000003, 4000000002, 4000000001], matches.Select(m => m.GameId));
        Assert.Equal([MatchResults.Loss, MatchResults.Win, MatchResults.Aborted, MatchResults.Win], matches.Select(m => m.Result));

        var ranked = matches[0];
        Assert.Equal((420, "CLASSIC", "MATCHED_GAME", 11, 1644), (ranked.QueueId, ranked.GameMode, ranked.GameType, ranked.MapId, ranked.DurationSeconds));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792699895462), ranked.StartedAt);
        Assert.Equal((3140, "PRACTICETOOL", "CUSTOM_GAME"), (matches[1].QueueId, matches[1].GameMode, matches[1].GameType));
    }

    [Fact]
    public async Task A_summary_carries_the_local_players_line()
    {
        var me = (await Read(ProfileSamples.Own.Handler())).Matches[0].Me;

        var noItems = Array.Empty<int>(); // records compare lists by reference
        Assert.Equal(
            new MatchParticipant(51, true, "Me", "EUNE", 13, 10, 8, 4, 162, 12103, 27730, 5, noItems, 4, 21, 8008, 8000, 8200, 0, ""),
            me with { Items = noItems });
        Assert.Equal([1086, 6676, 3031, 3033, 0, 0, 3363], me.Items);
    }

    [Fact]
    public async Task An_early_surrender_is_a_remake()
    {
        var profile = await ReadWith(ProfileSamples.Own.MatchesPath, ProfileSamples.Own.Matches.Replace(
            "\"deaths\": 8, \"gameEndedInEarlySurrender\": false", "\"deaths\": 8, \"gameEndedInEarlySurrender\": true"));

        Assert.Equal(MatchResults.Remake, profile.Matches[0].Result);
    }

    [Fact]
    public async Task Position_is_leagues_role_guess_and_empty_without_one()
    {
        var matches = (await Read(ProfileSamples.Own.Handler())).Matches;

        Assert.Equal([Positions.Bottom, "", "", Positions.Bottom], matches.Select(m => m.Position));
    }

    [Theory]
    [InlineData("TOP", "SOLO", Positions.Top)]
    [InlineData("JUNGLE", "NONE", Positions.Jungle)]
    [InlineData("MIDDLE", "SOLO", Positions.Middle)]
    [InlineData("BOTTOM", "CARRY", Positions.Bottom)]
    [InlineData("BOTTOM", "SUPPORT", Positions.Utility)]
    [InlineData("NONE", "SUPPORT", Positions.Utility)] // a roaming support (seen live)
    [InlineData("NONE", "NONE", "")]
    [InlineData(null, null, "")]
    public void Lane_and_role_become_a_position(string? lane, string? role, string position) =>
        Assert.Equal(position, ProfileMapper.Position(lane, role));

    [Fact]
    public async Task Only_the_newest_twenty_games_are_kept()
    {
        var history = JsonNode.Parse(ProfileSamples.Own.Matches)!;
        var template = history["games"]!["games"]![0]!;
        var games = new JsonArray();
        for (var i = 0; i < 21; i++) // oldest first, to check the connector sorts
        {
            var game = template.DeepClone();
            game["gameId"] = 5000000000L + i;
            game["gameCreation"] = 1792600000000L + i * 3_600_000L;
            games.Add(game);
        }

        history["games"]!["games"] = games;

        var matches = (await ReadWith(ProfileSamples.Own.MatchesPath, history.ToJsonString())).Matches;

        Assert.Equal(ProfileState.MaxMatches, matches.Count);
        Assert.Equal(5000000020L, matches[0].GameId);
        Assert.DoesNotContain(matches, m => m.GameId == 5000000000L);
    }

    // --- Match details ------------------------------------------------------------------------

    private static MatchDetails Details(string? myPuuid = ProfileSamples.Own.MyPuuid) =>
        ProfileMapper.Details(Json(ProfileSamples.Own.Game), myPuuid)!;

    [Fact]
    public void Details_list_the_own_team_first_with_names_and_enemies_without()
    {
        var details = Details();

        Assert.Equal(ProfileSamples.Own.RankedLossGameId, details.GameId);
        Assert.Equal([(100, true), (200, false)], details.Teams.Select(t => (t.TeamId, t.IsMyTeam)));
        Assert.Equal(["Ally1", "Ally2", "Ally3", "Me", "Ally4"], details.Teams[0].Participants.Select(p => p.GameName));
        Assert.Equal(["TAG2", "TAG3", "TAG4", "EUNE", "TAG5"], details.Teams[0].Participants.Select(p => p.TagLine));
        Assert.All(details.Teams[1].Participants, p => Assert.Equal(("", ""), (p.GameName, p.TagLine)));
        Assert.Equal([48, 76, 63, 145, 412], details.Teams[1].Participants.Select(p => p.ChampionId));
    }

    [Fact]
    public void IsMe_is_matched_by_puuid()
    {
        var everyone = Details().Teams.SelectMany(t => t.Participants).ToList();

        var me = Assert.Single(everyone, p => p.IsMe);
        Assert.Equal((51, "Me", 162), (me.ChampionId, me.GameName, me.Cs));
    }

    [Fact]
    public void On_red_side_the_red_team_comes_first()
    {
        // Seen from the player who was Enemy1: their team (200) is the own team now.
        var details = Details("enemy1-puuid");

        Assert.Equal([(200, true), (100, false)], details.Teams.Select(t => (t.TeamId, t.IsMyTeam)));
        Assert.Equal("Enemy1", details.Teams[0].Participants[0].GameName);
        Assert.All(details.Teams[1].Participants, p => Assert.Equal("", p.GameName));
    }

    [Fact]
    public void Without_knowing_the_local_player_nobody_has_a_name()
    {
        var details = Details(null);

        Assert.All(details.Teams, t => Assert.False(t.IsMyTeam));
        Assert.All(details.Teams.SelectMany(t => t.Participants), p => Assert.Equal(("", false), (p.GameName, p.IsMe)));
    }

    [Fact]
    public void Teams_carry_totals_objectives_and_bans()
    {
        var (blue, red) = (Details().Teams[0], Details().Teams[1]);

        Assert.Equal((false, 29, 52041), (blue.Win, blue.Kills, blue.Gold));
        Assert.Equal((4, 0, 0, 0, 0, 1), (blue.Towers, blue.Inhibitors, blue.Dragons, blue.Barons, blue.Heralds, blue.Grubs));
        Assert.Equal([35, 360, 122, 38, 8], blue.Bans);
        Assert.Equal((true, 38, 60887), (red.Win, red.Kills, red.Gold));
        Assert.Equal((9, 1, 3, 1, 1, 2), (red.Towers, red.Inhibitors, red.Dragons, red.Barons, red.Heralds, red.Grubs));
        Assert.Equal([55, 39, 75, 200, 800], red.Bans);
    }

    private static MatchDetails Arena(string? json = null) =>
        ProfileMapper.Details(Json(json ?? ProfileSamples.Own.ArenaGame), ProfileSamples.Own.MyPuuid)!;

    [Fact]
    public void Arena_details_are_one_team_per_duo_own_duo_first_then_by_placement()
    {
        var teams = Arena().Teams;

        Assert.Equal([(3, true), (2, false), (4, false), (1, false)], teams.Select(t => (t.TeamId, t.IsMyTeam)));
        Assert.All(teams, t => Assert.Equal(2, t.Participants.Count));
        Assert.Equal([3, 1, 2, 6], teams.Select(t => t.Participants[0].Placement));
        Assert.Equal([true, true, true, false], teams.Select(t => t.Win)); // stats.win: a top-half place
        Assert.Equal([103, 51], teams[0].Participants.Select(p => p.ChampionId));
        Assert.Equal((12, 17500), (teams[0].Kills, teams[0].Gold));
        Assert.All(teams, t => Assert.Equal((0, 0, 0), (t.Towers, t.Dragons, t.Bans.Count))); // per side in the LCU, not per duo
    }

    [Fact]
    public void Arena_names_only_the_own_duo()
    {
        var teams = Arena().Teams;

        Assert.Equal([("Me", "EUNE", true), ("Ally1", "TAG2", false)], teams[0].Participants.Select(p => (p.GameName, p.TagLine, p.IsMe)));
        // Enemy1 and Enemy2 share the local player's LCU team (100) but are another duo.
        Assert.All(teams.Skip(1).SelectMany(t => t.Participants), p => Assert.Equal(("", "", false), (p.GameName, p.TagLine, p.IsMe)));
    }

    [Theory]
    [InlineData("\"kills\": 7, \"playerSubteamId\": 3", "\"kills\": 7")] // the local player's duo is unknown
    [InlineData("\"kills\": 9, \"playerSubteamId\": 2", "\"kills\": 9")] // another player's duo is unknown
    [InlineData("\"kills\": 3, \"playerSubteamId\": 1", "\"kills\": 3, \"playerSubteamId\": 0")]
    public void Arena_without_every_duo_known_names_only_the_local_player(string from, string to)
    {
        var details = Arena(ProfileSamples.Own.ArenaGame.Replace(from, to));

        Assert.Equal([100, 200], details.Teams.Select(t => t.TeamId)); // the LCU's teams, the local player's first
        Assert.All(details.Teams, t => Assert.False(t.IsMyTeam));
        var named = Assert.Single(details.Teams.SelectMany(t => t.Participants), p => p.GameName != "");
        Assert.Equal(("Me", true), (named.GameName, named.IsMe));
    }

    [Fact]
    public async Task No_enemy_name_or_puuid_and_not_your_own_puuid_ever_reaches_the_phone()
    {
        var store = new ProfileStore();
        store.Set(await Read(ProfileSamples.Own.Handler()), DateTimeOffset.UnixEpoch);
        store.AddDetails(Details());

        var json = JsonSerializer.Serialize(store.Snapshot().Profile);

        Assert.DoesNotContain("Enemy", json, StringComparison.OrdinalIgnoreCase); // "enemy1-puuid" included
        Assert.DoesNotContain(ProfileSamples.Own.MyPuuid, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summonerId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ally1", json);
        Assert.Contains("ally1-puuid", json); // beside the name: what opens their profile
    }
}
