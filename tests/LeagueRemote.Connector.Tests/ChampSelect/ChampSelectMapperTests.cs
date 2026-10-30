using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>The champ-select session as the phone gets it: own team, enemies, bans, swaps, turns and the action to act on.</summary>
public sealed class ChampSelectMapperTests
{
    [Fact]
    public void Champ_select_maps_own_team_and_picks_my_in_progress_action()
    {
        var session = Json(GameStateSamples.ChampSelectSession);

        var state = GameStateMapper.Build(
            new LcuSnapshot("ChampSelect", ChampSelectSession: session, PickableChampionIds: [1, 222], BannableChampionIds: [3]),
            DateTimeOffset.UnixEpoch);

        var cs = Assert.IsType<ChampSelectState>(state.ChampSelect);
        Assert.Equal(2, cs.LocalPlayerCellId);
        Assert.Equal("BAN_PICK", cs.TimerPhase);
        Assert.Equal([1L, 2L], cs.MyTeam.Select(m => m.CellId));
        Assert.True(cs.MyTeam.Single(m => m.CellId == 2).IsLocalPlayer);
        Assert.Equal(222, cs.MyTeam.Single(m => m.CellId == 2).ChampionPickIntent);
        Assert.Equal([1, 222], cs.PickableChampionIds);
        Assert.Equal([3], cs.BannableChampionIds);

        // My action: the in-progress pick (id 21), not the completed ban or the enemy's pick.
        Assert.NotNull(cs.MyAction);
        Assert.Equal(21, cs.MyAction.Id);
        Assert.Equal("pick", cs.MyAction.Type);
        Assert.True(cs.MyAction.IsInProgress);
    }

    [Fact]
    public void Enemy_team_exposes_champions_and_positions_but_never_names_or_account_ids()
    {
        var session = Json(GameStateSamples.ChampSelectSession);

        var state = GameStateMapper.Build(new LcuSnapshot("ChampSelect", ChampSelectSession: session), DateTimeOffset.UnixEpoch);

        var enemy = Assert.Single(state.ChampSelect!.TheirTeam);
        Assert.Equal(new EnemyMember(5, 777, 0, "", IsLocked: false, IsActing: true, PickOrder: 3), enemy);

        var json = JsonSerializer.Serialize(state);
        Assert.Contains("777", json);
        Assert.DoesNotContain("Enemy\"", json);
        Assert.DoesNotContain("XXX", json);
        Assert.DoesNotContain("enemy-puuid", json);
        Assert.DoesNotContain("9005", json);
    }

    [Fact]
    public void Enemy_lock_state_follows_their_completed_pick_action()
    {
        var session = Json(GameStateSamples.ChampSelectSession.Replace(
            "\"id\": 22, \"actorCellId\": 5, \"championId\": 777, \"type\": \"pick\", \"completed\": false",
            "\"id\": 22, \"actorCellId\": 5, \"championId\": 777, \"type\": \"pick\", \"completed\": true"));

        var cs = ChampSelectMapper.Map(session, [], [])!;

        Assert.True(cs.TheirTeam.Single().IsLocked);
    }

    [Fact]
    public void Bans_come_from_completed_ban_actions_of_both_teams()
    {
        var session = Json(GameStateSamples.ChampSelectSession);

        var cs = ChampSelectMapper.Map(session, [], [])!;

        Assert.Equal([55], cs.Bans.MyTeamBans);
        Assert.Equal([1], cs.Bans.TheirTeamBans);
    }

    [Fact]
    public void Ban_phase_maps_bans_positions_and_my_in_progress_ban()
    {
        var session = Json(GameStateSamples.BanPhaseSession);

        var cs = ChampSelectMapper.Map(session, [], [1, 64, 99, 103])!;

        Assert.Equal(new PlayerAction(10, "ban", 0, IsInProgress: true, Completed: false), cs.MyAction);
        // No completed ally ban action yet, so my team's bans come from session.bans; theirs from the completed action.
        Assert.Equal([64], cs.Bans.MyTeamBans);
        Assert.Equal([99], cs.Bans.TheirTeamBans);
        Assert.Equal(["top", ""], cs.TheirTeam.Select(e => e.AssignedPosition));
        Assert.All(cs.TheirTeam, e => Assert.Equal(0, e.ChampionId)); // -1 normalised to 0
        Assert.Equal([1, 64, 99, 103], cs.BannableChampionIds); // a usable LCU list is passed through
    }

    [Theory]
    [InlineData(new[] { -1 })]
    [InlineData(new int[0])]
    [InlineData(new[] { 0, -1 })]
    public void Unusable_bannable_ids_fall_back_to_all_champions_not_yet_banned_or_picked(int[] bannable)
    {
        // Taken: 64 (my team's ban), 99 (their ban), 7 (my completed pick).
        var session = Json(GameStateSamples.BanPhaseSession.Replace(
            "\"id\": 20, \"actorCellId\": 2, \"championId\": 0, \"type\": \"pick\", \"completed\": false",
            "\"id\": 20, \"actorCellId\": 2, \"championId\": 7, \"type\": \"pick\", \"completed\": true"));

        var state = GameStateMapper.Build(
            new LcuSnapshot("ChampSelect", ChampSelectSession: session, BannableChampionIds: bannable, AllChampionIds: [-1, 1, 7, 64, 99, 103]),
            DateTimeOffset.UnixEpoch);

        Assert.Equal([1, 103], state.ChampSelect!.BannableChampionIds);
    }

    [Fact]
    public void Invalid_ids_are_dropped_even_without_a_fallback_list()
    {
        var session = Json(GameStateSamples.BanPhaseSession);

        var cs = ChampSelectMapper.Map(session, [-1, 0, 5, 5], [-1])!;

        Assert.Equal([5], cs.PickableChampionIds);
        Assert.Empty(cs.BannableChampionIds);
    }

    [Fact]
    public void Swaps_and_trades_are_mapped_per_teammate_and_invalid_ids_dropped()
    {
        var session = Json(GameStateSamples.BanPhaseSession);

        var cs = ChampSelectMapper.Map(session, [], [])!;

        Assert.Equal(
            [
                new SwapOffer(SwapKind.PickOrder, 31, 1, "AVAILABLE"),
                new SwapOffer(SwapKind.Position, 32, 1, "RECEIVED"),
                new SwapOffer(SwapKind.Trade, 33, 1, "INVALID"),
            ],
            cs.Swaps);
        Assert.Equal(new SwapOffer(SwapKind.Position, 32, 1, "RECEIVED"), ChampSelectSession.Parse(session)!.FindSwap(SwapKind.Position, 32));
        Assert.Null(ChampSelectSession.Parse(session)!.FindSwap(SwapKind.Trade, 32));
    }

    [Fact]
    public void Swap_kind_serializes_as_a_string()
    {
        var json = JsonSerializer.Serialize(new SwapOffer(SwapKind.PickOrder, 1, 2, "SENT"), JsonSerializerOptions.Web);

        Assert.Equal("""{"kind":"PickOrder","id":1,"cellId":2,"state":"SENT"}""", json);
    }

    [Fact]
    public void Planning_phase_targets_the_pick_action_even_when_a_ban_comes_first()
    {
        var session = Json(GameStateSamples.BanPhaseSession
            .Replace("\"phase\": \"BAN_PICK\"", "\"phase\": \"PLANNING\"")
            .Replace(
                "\"type\": \"ban\", \"completed\": false, \"isAllyAction\": true, \"isInProgress\": true",
                "\"type\": \"ban\", \"completed\": false, \"isAllyAction\": true, \"isInProgress\": false"));

        Assert.Equal(20, ChampSelectSession.Parse(session)!.FindMyAction()!.Id);
    }

    [Fact]
    public void Planning_phase_targets_the_pick_action_even_while_the_ban_is_already_in_progress()
    {
        // League flags the first ban as in progress during PLANNING; hovering then must declare pick intent.
        var session = Json(GameStateSamples.BanPhaseSession.Replace("\"phase\": \"BAN_PICK\"", "\"phase\": \"PLANNING\""));

        var action = ChampSelectSession.Parse(session)!.FindMyAction()!;

        Assert.Equal(20, action.Id);
        Assert.Equal("pick", action.Type);
    }

    [Fact]
    public void Session_without_enemy_bans_or_swaps_maps_to_empty_lists()
    {
        var session = Json("""{ "localPlayerCellId": 0, "myTeam": [], "actions": [] }""");

        var cs = ChampSelectMapper.Map(session, [], [])!;

        Assert.Empty(cs.TheirTeam);
        Assert.Empty(cs.Bans.MyTeamBans);
        Assert.Empty(cs.Bans.TheirTeamBans);
        Assert.Empty(cs.Swaps);
    }

    /// <summary>Ranked draft, local player (blue, cell 2) is in the second blue pick turn with cell 3.</summary>
    private static JsonElement DraftSession(string phase, string pickGroup2Progress = "true") => Json($$"""
    {
      "localPlayerCellId": 2,
      "timer": { "phase": "{{phase}}", "adjustedTimeLeftInPhase": 30000, "isInfinite": false },
      "myTeam": [
        { "cellId": 0, "team": 1, "championId": 86, "championPickIntent": 0, "assignedPosition": "top" },
        { "cellId": 2, "team": 1, "championId": 103, "championPickIntent": 103, "assignedPosition": "middle" },
        { "cellId": 3, "team": 1, "championId": 0, "championPickIntent": 51, "assignedPosition": "bottom" }
      ],
      "theirTeam": [
        { "cellId": 5, "team": 2, "championId": 238, "assignedPosition": "" },
        { "cellId": 6, "team": 2, "championId": 254, "assignedPosition": "" }
      ],
      "actions": [
        [
          { "id": 1, "actorCellId": 0, "championId": 157, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 2, "actorCellId": 2, "championId": 777, "type": "ban", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 3, "actorCellId": 5, "championId": 11, "type": "ban", "completed": true, "isAllyAction": false, "isInProgress": false }
        ],
        [ { "id": 4, "actorCellId": -1, "championId": 0, "type": "ten_bans_reveal", "completed": true, "isAllyAction": false, "isInProgress": false } ],
        [ { "id": 5, "actorCellId": 0, "championId": 86, "type": "pick", "completed": true, "isAllyAction": true, "isInProgress": false } ],
        [
          { "id": 6, "actorCellId": 5, "championId": 238, "type": "pick", "completed": true, "isAllyAction": false, "isInProgress": false },
          { "id": 7, "actorCellId": 6, "championId": 254, "type": "pick", "completed": true, "isAllyAction": false, "isInProgress": false }
        ],
        [
          { "id": 8, "actorCellId": 2, "championId": 103, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": {{pickGroup2Progress}} },
          { "id": 9, "actorCellId": 3, "championId": 0, "type": "pick", "completed": false, "isAllyAction": true, "isInProgress": {{pickGroup2Progress}} }
        ]
      ]
    }
    """);

    [Fact]
    public void Draft_turns_follow_the_action_groups_with_team_and_progress()
    {
        var cs = ChampSelectMapper.Map(DraftSession("BAN_PICK"), [], [])!;

        Assert.Equal("blue", cs.MySide);
        Assert.False(cs.IsTimerInfinite);
        Assert.Equal(
            [
                new DraftTurn("ban", "both", [0, 2, 5], false, true),
                new DraftTurn("reveal", "none", [], false, true),
                new DraftTurn("pick", "ally", [0], false, true),
                new DraftTurn("pick", "enemy", [5, 6], false, true),
                new DraftTurn("pick", "ally", [2, 3], true, false),
            ],
            cs.Turns!,
            new DraftTurnComparer());
        Assert.False(cs.AllowSubsetChampionPicks);
    }

    [Fact]
    public void Subset_champion_picks_are_flagged_for_the_soft_timer_ticks()
    {
        var json = DraftSession("BAN_PICK").GetRawText().Replace("\"localPlayerCellId\": 2,", "\"localPlayerCellId\": 2, \"allowSubsetChampionPicks\": true,");
        var cs = ChampSelectMapper.Map(Json(json), [], [])!;

        Assert.True(cs.AllowSubsetChampionPicks);
    }

    [Fact]
    public void Team_members_show_who_is_acting_their_pick_order_lock_and_ban()
    {
        var cs = ChampSelectMapper.Map(DraftSession("BAN_PICK"), [], [])!;

        var top = cs.MyTeam.Single(m => m.CellId == 0);
        Assert.True(top.IsLocked);
        Assert.False(top.IsActing);
        Assert.Equal(1, top.PickOrder);
        Assert.Equal(157, top.BanChampionId);
        Assert.True(top.HasBanned);

        // Hovering Ahri during the turn: championId is the hover, not a lock.
        var me = cs.MyTeam.Single(m => m.IsLocalPlayer);
        Assert.Equal(103, me.ChampionId);
        Assert.False(me.IsLocked);
        Assert.True(me.IsActing);
        Assert.Equal(4, me.PickOrder);

        Assert.True(cs.MyTeam.Single(m => m.CellId == 3).IsActing);
        Assert.All(cs.TheirTeam, e => Assert.True(e.IsLocked));
        Assert.Equal([2, 3], cs.TheirTeam.Select(e => e.PickOrder));
    }

    [Fact]
    public void Nobody_is_acting_while_players_declare_intent()
    {
        var cs = ChampSelectMapper.Map(DraftSession("PLANNING"), [], [])!;

        Assert.DoesNotContain(cs.Turns!, t => t.IsInProgress);
        Assert.DoesNotContain(cs.MyTeam, m => m.IsActing);
    }

    private sealed class DraftTurnComparer : IEqualityComparer<DraftTurn>
    {
        public bool Equals(DraftTurn? x, DraftTurn? y) =>
            x is not null && y is not null && x.Type == y.Type && x.Team == y.Team && x.CellIds.SequenceEqual(y.CellIds)
            && x.IsInProgress == y.IsInProgress && x.Completed == y.Completed;

        public int GetHashCode(DraftTurn obj) => obj.Type.GetHashCode();
    }
}
