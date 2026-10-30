using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

public sealed class PostGameTests
{
    private const string Phase = "/lol-gameflow/v1/gameflow-phase";
    private const string StatsBlock = "/lol-end-of-game/v1/eog-stats-block";
    private const string LpChange = "/lol-ranked/v1/current-lp-change-notification";
    private const string Mastery = "/lol-end-of-game/v1/champion-mastery-updates";
    private const string Session = "/lol-gameflow/v1/session";
    private const string PlayerStatus = "/lol-gameflow/v1/gameflow-metadata/player-status";

    private static readonly JsonSerializerOptions HubJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static PostGameSummary Ranked() => PostGameMapper.Map(
        Json(PostGameSamples.RankedWin), Json(PostGameSamples.LpPromoted), Json(PostGameSamples.MasteryUpdate),
        Json(PostGameSamples.Session), Json(PostGameSamples.PlayerStatus))!;

    // --- Mapping ------------------------------------------------------------------------------

    [Fact]
    public void The_summary_has_the_result_queue_length_and_your_line()
    {
        var summary = Ranked();

        Assert.Equal(4000000020, summary.GameId);
        Assert.Equal(420, summary.QueueId);
        Assert.Equal(11, summary.MapId);
        Assert.Equal("CLASSIC", summary.GameMode);
        Assert.Equal(1512, summary.DurationSeconds);
        Assert.Equal(MatchResults.Win, summary.Result);
        Assert.True(summary.IsRanked);
        Assert.True(summary.CanPlayAgain);

        var me = Assert.Single(summary.Teams.SelectMany(t => t.Participants), p => p.IsMe);
        Assert.Equal(103, me.ChampionId);
        Assert.Equal(("Me", "EUW"), (me.GameName, me.TagLine));
        Assert.Equal((18, 3, 9), (me.Kills, me.Deaths, me.Assists));
        Assert.Equal(213, me.Cs); // minions + monsters
        Assert.Equal((17, 30000, 38200, 21), (me.ChampLevel, me.Gold, me.Damage, me.VisionScore));
        Assert.Equal([6655, 3020, 4645, 3089, 3157, 0, 3363], me.Items);
        Assert.Equal((4, 14), (me.Spell1Id, me.Spell2Id));
        Assert.Equal((8112, 8100, 8200), (me.Keystone, me.PrimaryStyle, me.SubStyle));
    }

    [Fact]
    public void Your_team_comes_first_with_its_totals_and_short_item_lists_are_padded()
    {
        var summary = Ranked();

        Assert.Equal([100, 200], summary.Teams.Select(t => t.TeamId));
        var mine = summary.Teams[0];
        Assert.True(mine.IsMyTeam);
        Assert.True(mine.Win);
        Assert.Equal((25, 8, 15, 52000), (mine.Kills, mine.Deaths, mine.Assists, mine.Gold));
        Assert.Equal([86, 103], mine.Participants.Select(p => p.ChampionId)); // the client's order

        var enemy = summary.Teams[1];
        Assert.False(enemy.IsMyTeam);
        Assert.False(enemy.Win);
        Assert.Equal([6692, 3111, 0, 0, 0, 0, 0], enemy.Participants[1].Items);
        Assert.Equal(13, enemy.Participants[1].ChampLevel); // no LEVEL stat: the player's level
        Assert.Equal(160, enemy.Participants[1].Cs);
    }

    [Fact]
    public void Enemy_names_and_everyones_ids_never_reach_the_phone()
    {
        var summary = Ranked();

        Assert.All(summary.Teams[1].Participants, p => Assert.Equal(("", ""), (p.GameName, p.TagLine)));
        Assert.Equal(["Ally1", "Me"], summary.Teams[0].Participants.Select(p => p.GameName));

        var json = JsonSerializer.Serialize(summary, HubJson);
        foreach (var secret in new[] { "Enemy1", "Enemy2", "E1", "enemy1-puuid", "enemy2-puuid", "me-puuid", "summonerId", "post-game-room", "profileIcon" })
            Assert.DoesNotContain(secret, json);
        Assert.Equal(["ally1-puuid", ""], summary.Teams[0].Participants.Select(p => p.Puuid)); // a teammate's, to open their profile
    }

    [Fact]
    public void A_remake_is_a_remake_whoever_won()
    {
        var block = PostGameSamples.RankedWin.Replace("\"gameEndedInEarlySurrender\": false", "\"gameEndedInEarlySurrender\": true");

        Assert.Equal(MatchResults.Remake, PostGameMapper.Map(Json(block))!.Result);
    }

    [Fact]
    public void A_loss_is_your_team_not_winning()
    {
        var block = PostGameSamples.RankedWin
            .Replace("\"isWinningTeam\": true", "\"isWinningTeam\": WIN")
            .Replace("\"isWinningTeam\": false", "\"isWinningTeam\": true")
            .Replace("\"isWinningTeam\": WIN", "\"isWinningTeam\": false");

        Assert.Equal(MatchResults.Loss, PostGameMapper.Map(Json(block))!.Result);
    }

    [Fact]
    public void You_are_found_by_puuid_when_the_block_does_not_flag_you()
    {
        var block = PostGameSamples.RankedWin.Replace("\"isLocalPlayer\": true", "\"isLocalPlayer\": false");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.True(summary.Teams[0].IsMyTeam);
        Assert.Equal(103, Assert.Single(summary.Teams[0].Participants, p => p.IsMe).ChampionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{ "gameId": 1, "teams": [] }""")]
    [InlineData("""{ "gameId": 1, "teams": [ { "teamId": 100, "players": [] } ] }""")]
    public void No_summary_without_a_stats_block_or_players(string? block) =>
        Assert.Null(PostGameMapper.Map(block is null ? null : Json(block)));

    [Fact]
    public void The_lp_change_and_a_promotion_are_shown_for_this_game()
    {
        var rank = Ranked().Rank;

        Assert.NotNull(rank);
        Assert.Equal(RankedQueueTypes.Solo, rank.QueueType);
        Assert.Equal(("GOLD", "IV", 0, 21), (rank.Tier, rank.Division, rank.LeaguePoints, rank.LeaguePointsDelta));
        Assert.Equal(PostGameRankChanges.Promoted, rank.Change);
    }

    [Fact]
    public void A_demotion_and_an_apex_tier_without_division()
    {
        var notification = PostGameSamples.LpPromoted
            .Replace("LEAGUE_PROMOTED", "LEAGUE_DEMOTED").Replace("\"GOLD\"", "\"MASTER\"").Replace("\"leaguePointsDelta\": 21", "\"leaguePointsDelta\": -18");

        var rank = PostGameMapper.Rank(Json(notification), 4000000020, ranked: true)!;

        Assert.Equal(("MASTER", "", -18, PostGameRankChanges.Demoted), (rank.Tier, rank.Division, rank.LeaguePointsDelta, rank.Change));
    }

    [Fact]
    public void An_lp_change_of_an_earlier_game_is_ignored()
    {
        var stale = PostGameSamples.LpPromoted.Replace("4000000020", "3999999999");

        Assert.Null(PostGameMapper.Rank(Json(stale), 4000000020, ranked: true));
    }

    [Fact]
    public void An_lp_change_without_a_game_counts_only_for_a_ranked_game()
    {
        var anonymous = Json(PostGameSamples.LpPromoted.Replace("4000000020", "0"));

        Assert.NotNull(PostGameMapper.Rank(anonymous, 4000000020, ranked: true));
        Assert.Null(PostGameMapper.Rank(anonymous, 4000000020, ranked: false));
        Assert.Null(PostGameMapper.Rank(null, 4000000020, ranked: true)); // 204: no notification
    }

    [Fact]
    public void Mastery_gained_with_this_game_only()
    {
        var mastery = Ranked().Mastery;

        Assert.Equal(new PostGameMastery(103, 1240, 12, true, "S", 9760), mastery);
        Assert.Null(PostGameMapper.Mastery(Json(PostGameSamples.MasteryUpdate.Replace("4000000020", "3999999999")), 4000000020));
    }

    [Fact]
    public void Play_again_follows_the_clients_own_check()
    {
        Assert.True(PostGameMapper.CanPlayAgain(Json(PostGameSamples.PlayerStatus)));
        Assert.False(PostGameMapper.CanPlayAgain(Json(PostGameSamples.PlayerStatus.Replace("\"allowedPlayAgain\": true", "\"allowedPlayAgain\": false"))));
        Assert.False(PostGameMapper.CanPlayAgain(null));
    }

    [Fact]
    public void Without_a_session_the_queue_comes_from_the_last_queued_lobby()
    {
        var summary = PostGameMapper.Map(Json(PostGameSamples.RankedWin), playerStatus: Json(PostGameSamples.PlayerStatus))!;

        Assert.Equal(420, summary.QueueId);
        Assert.Equal(0, summary.MapId);
        Assert.Null(summary.Rank);
        Assert.Null(summary.Mastery);
    }

    // --- Arena --------------------------------------------------------------------------------

    [Fact]
    public void Arena_is_one_team_per_duo_yours_first_then_by_placement()
    {
        var summary = PostGameMapper.Map(Json(PostGameSamples.Arena))!;

        Assert.Equal([3, 1, 7], summary.Teams.Select(t => t.TeamId));
        Assert.Equal([true, false, false], summary.Teams.Select(t => t.IsMyTeam));
        Assert.Equal([true, true, false], summary.Teams.Select(t => t.Win)); // 2nd, 1st, 8th
        Assert.Equal(MatchResults.Win, summary.Result);
        Assert.Equal(["Me", "Duo"], summary.Teams[0].Participants.Select(p => p.GameName));
        Assert.All(summary.Teams.Skip(1).SelectMany(t => t.Participants), p => Assert.Equal("", p.GameName));
        Assert.Equal(13, summary.Teams[0].Kills);
        Assert.Equal(2, summary.Teams[0].Participants[0].Placement);
    }

    [Fact]
    public void Arena_without_every_players_duo_names_only_you()
    {
        var block = PostGameSamples.Arena.Replace("\"PLAYER_SUBTEAM\": 7,", "");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.All(summary.Teams, t => Assert.False(t.IsMyTeam));
        Assert.Equal(["Me"], summary.Teams.SelectMany(t => t.Participants).Select(p => p.GameName).Where(n => n != ""));
        Assert.Equal(MatchResults.Win, summary.Result); // from your placement (2nd)
    }

    [Fact]
    public void Arena_is_recognised_by_its_duos_when_the_mode_is_missing()
    {
        var block = PostGameSamples.Arena.Replace("\"gameMode\": \"CHERRY\", ", "");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.Equal(["Me", "Duo"], summary.Teams.SelectMany(t => t.Participants).Select(p => p.GameName).Where(n => n != ""));
        Assert.Equal(PostGameMapper.ArenaMapId, summary.MapId);
    }

    [Fact]
    public void Without_team_ids_only_you_are_named()
    {
        var block = PostGameSamples.RankedWin.Replace("\"teamId\": 100, ", "").Replace("\"teamId\": 200, ", "");

        var summary = PostGameMapper.Map(Json(block))!;

        Assert.Equal(["Me"], summary.Teams.SelectMany(t => t.Participants).Select(p => p.GameName).Where(n => n != ""));
        Assert.All(summary.Teams, t => Assert.False(t.IsMyTeam));
    }

    [Fact]
    public void Mastery_of_another_champion_is_ignored()
    {
        var other = Json(PostGameSamples.MasteryUpdate.Replace("\"gameId\": 4000000020, ", "").Replace("\"championId\": 103,", "\"championId\": 64,"));

        Assert.Null(PostGameMapper.Mastery(other, 4000000020, myChampionId: 103));
        Assert.NotNull(PostGameMapper.Mastery(other, 4000000020));
    }

    [Fact]
    public void The_summary_stays_small_with_sixteen_players_and_the_longest_names()
    {
        var longName = new string('W', 40);
        var players = string.Join(",", Enumerable.Range(0, 20).Select(i => $$"""
            { "puuid": "{{$"p{i:D2}".PadRight(RiotIds.MaxPuuidLength, 'a')}}", "riotIdGameName": "{{longName}}", "riotIdTagLine": "{{longName}}", "championId": {{900 + i}},
              "isLocalPlayer": {{(i == 0 ? "true" : "false")}}, "spell1Id": 4, "spell2Id": 14, "items": [999999, 999999, 999999, 999999, 999999, 999999, 999999, 999999],
              "stats": { "CHAMPIONS_KILLED": 99, "NUM_DEATHS": 99, "ASSISTS": 99, "MINIONS_KILLED": 999, "GOLD_EARNED": 99999,
                         "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 999999, "VISION_SCORE": 999, "PLAYER_SUBTEAM": {{i / 2 + 1}},
                         "PLAYER_SUBTEAM_PLACEMENT": {{i / 2 + 1}}, "PERK0": 9999, "PERK_PRIMARY_STYLE": 9999, "PERK_SUB_STYLE": 9999 } }
            """));
        var block = $$"""{ "gameId": 1, "gameMode": "CHERRY", "teams": [ { "teamId": 100, "players": [ {{players}} ] } ] }""";

        var summary = PostGameMapper.Map(Json(block), Json(PostGameSamples.LpPromoted.Replace("4000000020", "1")),
            Json(PostGameSamples.MasteryUpdate.Replace("4000000020", "1")), Json(PostGameSamples.Session), Json(PostGameSamples.PlayerStatus))!;

        Assert.Equal(PostGameSummary.MaxPlayers, summary.Teams.Sum(t => t.Participants.Count));
        Assert.True(summary.Teams.Count <= PostGameSummary.MaxTeams);
        Assert.Equal(PostGameMapper.MaxNameLength, summary.Teams[0].Participants[0].GameName.Length);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(summary, HubJson).Length;
        Assert.True(bytes <= PostGameSummary.MaxJsonBytes, $"{bytes} bytes");
    }

    // --- Reading it with the state ------------------------------------------------------------

    private static FakeLcu EndOfGameHandler()
    {
        var handler = new FakeLcu();
        handler.Routes[Phase] = "\"EndOfGame\"";
        handler.Routes["/lol-perks/v1/pages"] = "[]";
        handler.Routes[StatsBlock] = PostGameSamples.RankedWin;
        handler.Routes[LpChange] = PostGameSamples.LpPromoted;
        handler.Routes[Mastery] = PostGameSamples.MasteryUpdate;
        handler.Routes[Session] = PostGameSamples.Session;
        handler.Routes[PlayerStatus] = PostGameSamples.PlayerStatus;
        return handler;
    }

    [Fact]
    public async Task The_post_game_screen_is_read_with_the_state()
    {
        var handler = EndOfGameHandler();

        var state = await TestLcu.BuildStateAsync(handler);

        Assert.Equal("EndOfGame", state.GameflowPhase);
        Assert.NotNull(state.PostGame);
        Assert.Equal(21, state.PostGame.Rank!.LeaguePointsDelta);
        Assert.Equal(
            [
                "GET /lol-gameflow/v1/gameflow-phase",
                "GET /lol-perks/v1/pages",
                "GET /lol-game-queues/v1/queues",
                $"GET {StatsBlock}",
                $"GET {LpChange}",
                $"GET {Mastery}",
                $"GET {Session}",
                $"GET {PlayerStatus}",
                "GET /lol-lobby/v2/received-invitations",
            ],
            handler.Requests);
    }

    [Fact]
    public async Task A_service_that_fails_only_leaves_its_part_out()
    {
        // A League service restarting.
        var handler = EndOfGameHandler();
        handler.Failures[$"GET {LpChange}"] = (HttpStatusCode.InternalServerError, "busy");
        handler.Failures[$"GET {Mastery}"] = (HttpStatusCode.InternalServerError, "busy");

        var state = await TestLcu.BuildStateAsync(handler);

        Assert.NotNull(state.PostGame);
        Assert.Null(state.PostGame.Rank);
        Assert.Null(state.PostGame.Mastery);
        Assert.Equal(MatchResults.Win, state.PostGame.Result);
    }

    [Fact]
    public async Task Before_the_stats_arrive_there_is_no_summary_yet()
    {
        var handler = EndOfGameHandler();
        handler.Routes.Remove(StatsBlock);

        var state = await TestLcu.BuildStateAsync(handler);

        Assert.Equal("EndOfGame", state.GameflowPhase);
        Assert.Null(state.PostGame);
    }

    [Theory]
    [InlineData("PreEndOfGame")]
    [InlineData("WaitingForStats")]
    [InlineData("Lobby")]
    [InlineData("None")]
    public async Task Other_phases_do_not_read_the_post_game_screen(string phase)
    {
        var handler = EndOfGameHandler();
        handler.Routes[Phase] = $"\"{phase}\"";

        var state = await TestLcu.BuildStateAsync(handler);

        Assert.Null(state.PostGame);
        Assert.DoesNotContain(handler.Requests, r => r.Contains("end-of-game") || r.Contains("lp-change") || r.Contains("gameflow/v1/session"));
    }

    [Fact]
    public void The_post_game_events_rebuild_the_state()
    {
        Assert.Equal((true, false, false, true), LcuConnectionService.RefreshesFor(LcuTopics.EndOfGameStats));
        foreach (var topic in new[]
                 {
                     LcuTopics.EndOfGameStats,
                     "OnJsonApiEvent_lol-end-of-game_v1_champion-mastery-updates",
                     "OnJsonApiEvent_lol-ranked_v1_current-lp-change-notification",
                     "OnJsonApiEvent_lol-gameflow_v1_gameflow-metadata",
                 })
        {
            Assert.Contains(topic, LcuTopics.All);
            Assert.True(LcuConnectionService.RefreshesFor(topic).State);
        }
    }

    // --- Commands -----------------------------------------------------------------------------

    [Fact]
    public async Task PlayAgain_sends_what_the_clients_post_game_sends()
    {
        var handler = EndOfGameHandler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.PlayAgain));

        Assert.True(result.Success, result.Error);
        Assert.Equal([$"GET {Phase}", $"GET {PlayerStatus}", "POST /lol-lobby/v2/play-again"], handler.Requests);
    }

    [Theory]
    [InlineData(CommandType.PlayAgain)]
    [InlineData(CommandType.LeavePostGame)]
    public async Task The_post_game_commands_are_refused_elsewhere(CommandType type)
    {
        var handler = EndOfGameHandler();
        handler.Routes[Phase] = "\"PreEndOfGame\"";

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", type));

        Assert.False(result.Success);
        Assert.Equal("Not on the post-game screen.", result.Error);
        Assert.Equal([$"GET {Phase}"], handler.Requests);
    }

    [Theory]
    [InlineData("""{ "lastQueuedLobbyStatus": { "allowedPlayAgain": false } }""")]
    [InlineData(null)]
    public async Task PlayAgain_is_refused_when_the_client_does_not_offer_it(string? playerStatus)
    {
        var handler = EndOfGameHandler();
        if (playerStatus is null)
            handler.Routes.Remove(PlayerStatus);
        else
            handler.Routes[PlayerStatus] = playerStatus;

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.PlayAgain));

        Assert.False(result.Success);
        Assert.Equal("Play Again isn't available for this game.", result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlayAgain_refused_by_league_gives_a_short_sentence()
    {
        var handler = EndOfGameHandler();
        handler.Failures["POST /lol-lobby/v2/play-again"] = (HttpStatusCode.InternalServerError, "LOBBY_NOT_FOUND");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.PlayAgain));

        Assert.False(result.Success);
        Assert.Equal("League couldn't do that. Try again.", result.Error);
    }

    [Fact]
    public async Task LeavePostGame_closes_the_post_game_screen_like_the_client()
    {
        var handler = EndOfGameHandler();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("r1", CommandType.LeavePostGame));

        Assert.True(result.Success, result.Error);
        // The League scoreboard's close button: no play-again-decline (only TFT's Go Home sends that).
        Assert.Equal([$"GET {Phase}", "POST /lol-end-of-game/v1/state/dismiss-stats"], handler.Requests);
    }
}
