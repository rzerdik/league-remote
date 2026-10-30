using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LeagueRemote.Connector.LiveClient;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="LiveGameMapper"/> on <see cref="LiveGameSamples"/>, the live-game store and the champion catalog's name map.</summary>
public sealed class LiveGameTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-23T19:14:00Z", CultureInfo.InvariantCulture);

    private static readonly IReadOnlyDictionary<string, int> Champions = ChampionCatalog.IdsByName(Json(LiveGameSamples.ChampionSummary));

    private static LiveGameState Map(string json, IReadOnlyDictionary<string, int>? champions = null) =>
        LiveGameMapper.Map(Json(json), champions ?? Champions, Now);

    private static LivePlayer Player(LiveGameState game, string championName) =>
        game.Teams.SelectMany(t => t.Players).Single(p => p.ChampionName == championName);

    private static LiveTeam Team(LiveGameState game, string side) => game.Teams.Single(t => t.Side == side);

    /// <summary>The sample with a change made to its JSON tree.</summary>
    private static string With(string json, Action<JsonNode> change)
    {
        var root = JsonNode.Parse(json)!;
        change(root);
        return root.ToJsonString();
    }

    /// <summary>The 14:00 sample's players with only these events, at this clock.</summary>
    private static string Game(double gameTime, params string[] events) => With(LiveGameSamples.AllGameData, root =>
    {
        root["gameData"]!["gameTime"] = gameTime;
        root["events"]!["Events"] = new JsonArray([.. events.Select(e => JsonNode.Parse(e))]);
    });

    private static string Drake(int id, double time, string type, string killer) =>
        $$"""{ "EventID": {{id}}, "EventName": "DragonKill", "EventTime": {{time.ToString(CultureInfo.InvariantCulture)}}, "DragonType": "{{type}}", "Stolen": "False", "KillerName": "{{killer}}", "Assisters": [] }""";

    private static string Kill(int id, double time, string killer, string victim) =>
        $$"""{ "EventID": {{id}}, "EventName": "ChampionKill", "EventTime": {{time.ToString(CultureInfo.InvariantCulture)}}, "KillerName": "{{killer}}", "VictimName": "{{victim}}", "Assisters": [] }""";

    private static string Serialized(LiveGameState game) =>
        JsonSerializer.Serialize(game, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } });

    // --- Players and teams -----------------------------------------------------------------

    [Fact]
    public void The_scoreboard_lists_the_own_team_first_with_the_local_player()
    {
        var game = Map(LiveGameSamples.AllGameData);

        Assert.True(game.Available);
        Assert.Equal((840.2, "CLASSIC", 11, Now), (game.GameTime, game.GameMode, game.MapNumber, game.UpdatedAt));
        Assert.Equal([LiveTeams.Order, LiveTeams.Chaos], game.Teams.Select(t => t.Side));
        Assert.Equal([true, false], game.Teams.Select(t => t.IsMyTeam));
        Assert.Equal(["Ahri", "Garen", "Lee Sin", "Thresh", "Jinx"], game.Teams[0].Players.Select(p => p.ChampionName));
        Assert.Equal((3, 1), (game.Teams[0].Kills, game.Teams[1].Kills));

        var me = game.Me!;
        Assert.Same(me.Player, Assert.Single(game.Teams.SelectMany(t => t.Players), p => p.IsMe));
        Assert.Equal((103, "Me", "TAG0", Positions.Middle), (me.Player.ChampionId, me.Player.GameName, me.Player.TagLine, me.Player.Position));
        Assert.Equal(1234.5677490234375, me.CurrentGold);
        Assert.Equal((9, 3, 0, 0, 80, 4.5), (me.Player.Level, me.Player.Kills, me.Player.Deaths, me.Player.Assists, me.Player.CreepScore, me.Player.WardScore));
    }

    [Fact]
    public void A_team_from_the_local_players_side_is_listed_first_also_on_red_side()
    {
        var game = Map(With(LiveGameSamples.AllGameData, root =>
        {
            foreach (var p in root["allPlayers"]!.AsArray())
                p!["team"] = p["team"]!.GetValue<string>() == LiveTeams.Order ? LiveTeams.Chaos : LiveTeams.Order;
        }));

        Assert.Equal([LiveTeams.Chaos, LiveTeams.Order], game.Teams.Select(t => t.Side));
        Assert.True(game.Teams[0].IsMyTeam);
    }

    [Fact]
    public void Own_team_keeps_riot_ids_and_enemies_are_champions_only_everywhere()
    {
        foreach (var sample in new[] { LiveGameSamples.AllGameData, LiveGameSamples.AllGameDataLate })
        {
            var game = Map(sample);

            Assert.Equal(["Me", "Ally1", "Ally2", "Ally3", "Ally4"], Team(game, LiveTeams.Order).Players.Select(p => p.GameName));
            Assert.Equal(["TAG0", "TAG1", "TAG2", "TAG3", "TAG4"], Team(game, LiveTeams.Order).Players.Select(p => p.TagLine));
            Assert.All(Team(game, LiveTeams.Chaos).Players, p => Assert.Equal(("", ""), (p.GameName, p.TagLine)));
            // Not in players, events or assists: enemies are never named.
            Assert.DoesNotContain("Enemy", Serialized(game), StringComparison.Ordinal);
            Assert.DoesNotContain("TAG5", Serialized(game), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_unmatched_local_player_names_nobody()
    {
        var game = Map(With(LiveGameSamples.AllGameData, root =>
            root["activePlayer"] = JsonNode.Parse("""{ "currentGold": 5, "riotId": "Someone#ELSE", "riotIdGameName": "Someone", "riotIdTagLine": "ELSE", "summonerName": "Someone#ELSE" }""")));

        Assert.Null(game.Me);
        Assert.All(game.Teams, t => Assert.False(t.IsMyTeam));
        Assert.All(game.Teams.SelectMany(t => t.Players), p => Assert.Equal(("", "", false), (p.GameName, p.TagLine, p.IsMe)));
        Assert.All(game.Events, e => Assert.Equal(("", "", false), (e.ActorGameName, e.VictimGameName, e.IsMyTeam)));
        Assert.Equal([LiveTeams.Order, LiveTeams.Chaos], game.Teams.Select(t => t.Side));
    }

    [Fact]
    public void A_spectator_without_an_active_player_sees_champions_only()
    {
        var spectating = With(LiveGameSamples.AllGameDataLate, root =>
            root["activePlayer"] = JsonNode.Parse("""{ "error": "Spectator mode doesn't currently support this feature" }"""));
        var withoutActivePlayer = With(LiveGameSamples.AllGameDataLate, root => root.AsObject().Remove("activePlayer"));

        foreach (var game in new[] { Map(spectating), Map(withoutActivePlayer) })
        {
            Assert.Null(game.Me);
            Assert.All(game.Teams.SelectMany(t => t.Players), p => Assert.Equal("", p.GameName));
            Assert.DoesNotContain("Ally", Serialized(game), StringComparison.Ordinal);
            Assert.DoesNotContain("Enemy", Serialized(game), StringComparison.Ordinal);
            Assert.Equal(1, Team(game, LiveTeams.Order).Barons); // the counts don't need the local player
        }
    }

    [Fact]
    public void An_own_team_of_more_than_five_names_only_the_local_player()
    {
        // An unexpected mode putting everyone on one side must not name enemies.
        var game = Map(With(LiveGameSamples.AllGameData, root =>
        {
            foreach (var p in root["allPlayers"]!.AsArray())
                p!["team"] = LiveTeams.Order;
        }));

        var team = Assert.Single(game.Teams);
        Assert.Equal(10, team.Players.Count);
        Assert.Equal("Me", Assert.Single(team.Players, p => p.GameName != "").GameName);
        Assert.DoesNotContain("Ally", Serialized(game), StringComparison.Ordinal);
        Assert.DoesNotContain("Enemy", Serialized(game), StringComparison.Ordinal);
    }

    [Fact]
    public void A_local_player_matched_twice_is_nobody()
    {
        var game = Map(With(LiveGameSamples.AllGameData, root => root["allPlayers"]![1]!["riotId"] = "Me#TAG0"));

        Assert.Null(game.Me);
        Assert.All(game.Teams.SelectMany(t => t.Players), p => Assert.Equal("", p.GameName));
    }

    [Fact]
    public void Champion_ids_come_from_the_internal_name_with_the_display_name_as_fallback()
    {
        var game = Map(LiveGameSamples.AllGameData);

        Assert.Equal((62, "Wukong"), (Player(game, "Wukong").ChampionId, Player(game, "Wukong").ChampionName));
        Assert.Equal(145, Player(game, "Kai'Sa").ChampionId);
        Assert.Equal(64, Player(game, "Lee Sin").ChampionId);
        Assert.Equal([103, 86, 64, 412, 222, 122, 62, 134, 145, 111], game.Teams.SelectMany(t => t.Players).Select(p => p.ChampionId));

        var noRawName = Map(With(LiveGameSamples.AllGameData, root => root["allPlayers"]![6]!.AsObject().Remove("rawChampionName")));
        Assert.Equal(62, Player(noRawName, "Wukong").ChampionId);

        // Without the LCU's game data the names are still there.
        var noCatalog = LiveGameMapper.Map(Json(LiveGameSamples.AllGameData), null, Now);
        Assert.All(noCatalog.Teams.SelectMany(t => t.Players), p => Assert.Equal(0, p.ChampionId));
        Assert.Equal("Wukong", Player(noCatalog, "Wukong").ChampionName);
    }

    [Fact]
    public void Summoner_spells_items_and_runes_are_read_per_player()
    {
        var game = Map(LiveGameSamples.AllGameData);
        var ahri = Player(game, "Ahri");

        Assert.Equal(new LiveSummonerSpell("SummonerFlash", "Flash"), ahri.Spell1);
        Assert.Equal(new LiveSummonerSpell("SummonerDot", "Ignite"), ahri.Spell2);
        Assert.Equal(new LiveSummonerSpell("SummonerHaste", "Ghost"), Player(game, "Darius").Spell2);
        Assert.Equal(new LiveSummonerSpell("SummonerSmite", "Smite"), Player(game, "Lee Sin").Spell1);
        Assert.Equal([new LiveItem(1056, 0, 1), new LiveItem(3020, 1, 1), new LiveItem(2003, 2, 1), new LiveItem(3340, 6, 1)], ahri.Items);
        Assert.Equal(new LiveItem(2003, 1, 2), Player(game, "Kai'Sa").Items[1]);
        Assert.Equal((8112, 8100, 8200), (ahri.Keystone, ahri.PrimaryStyle, ahri.SubStyle));
        Assert.Equal(Positions.Utility, Player(game, "Nautilus").Position);
    }

    [Fact]
    public void Unknown_spell_names_and_positions_degrade_to_empty()
    {
        var game = Map(With(LiveGameSamples.AllGameData, root =>
        {
            root["allPlayers"]![0]!["summonerSpells"]!["summonerSpellOne"]!["rawDisplayName"] = "SomethingElse";
            root["allPlayers"]![0]!["position"] = "NONE";
            root["allPlayers"]![0]!["items"] = JsonNode.Parse("""[ { "itemID": 0, "slot": 3 }, { "itemID": 1001, "slot": 1, "count": 1 } ]""");
        }));
        var ahri = Player(game, "Ahri");

        Assert.Equal(new LiveSummonerSpell("", "Flash"), ahri.Spell1);
        Assert.Equal("", ahri.Position);
        Assert.Equal([new LiveItem(1001, 1, 1)], ahri.Items);
    }

    [Fact]
    public void Dead_players_carry_their_respawn_countdown()
    {
        var game = Map(LiveGameSamples.AllGameData);

        Assert.Equal((true, 14.3), (Player(game, "Darius").IsDead, Player(game, "Darius").RespawnSeconds));
        Assert.Equal((false, 0.0), (Player(game, "Ahri").IsDead, Player(game, "Ahri").RespawnSeconds));
    }

    // --- Objective counts ------------------------------------------------------------------

    [Fact]
    public void Team_objectives_are_counted_from_the_events()
    {
        var game = Map(LiveGameSamples.AllGameData);
        var order = Team(game, LiveTeams.Order);
        var chaos = Team(game, LiveTeams.Chaos);

        Assert.Equal((1, 0, 0, 0, 2), (order.Towers, order.Inhibitors, order.Barons, order.Heralds, order.Grubs));
        Assert.Equal([LiveDragonTypes.Fire], order.Dragons);
        // The stolen drake is the thief's.
        Assert.Equal([LiveDragonTypes.Water], chaos.Dragons);
        Assert.Equal((0, 1), (chaos.Towers, chaos.Grubs));

        var late = Map(LiveGameSamples.AllGameDataLate);
        var lateOrder = Team(late, LiveTeams.Order);
        Assert.Equal((1, 1, 1, 1, 2), (lateOrder.Towers, lateOrder.Inhibitors, lateOrder.Barons, lateOrder.Heralds, lateOrder.Grubs));
        Assert.Equal([LiveDragonTypes.Fire, LiveDragonTypes.Hextech], lateOrder.Dragons);
        Assert.Equal(0, Team(late, LiveTeams.Chaos).Inhibitors);
    }

    [Fact]
    public void A_turret_that_falls_to_minions_counts_for_the_other_team()
    {
        var game = Map(Game(900,
            """{ "EventID": 1, "EventName": "TurretKilled", "EventTime": 880.5, "TurretKilled": "Turret_T1_C_05_A", "KillerName": "Minion_T200L1S04N0006", "Assisters": [] }""",
            """{ "EventID": 2, "EventName": "FirstBrick", "EventTime": 880.5, "KillerName": "Minion_T200L1S04N0006" }"""));

        Assert.Equal((0, 1), (Team(game, LiveTeams.Order).Towers, Team(game, LiveTeams.Chaos).Towers));
        var turret = game.Events[0];
        Assert.Equal((LiveEventKinds.Turret, LiveTeams.Chaos, false, LiveLanes.Mid, 0, ""),
            (turret.Kind, turret.Side, turret.IsMyTeam, turret.Lane, turret.ActorChampionId, turret.ActorChampionName));
        Assert.Equal((LiveEventKinds.FirstTower, LiveTeams.Chaos), (game.Events[1].Kind, game.Events[1].Side));
    }

    // --- Feed --------------------------------------------------------------------------------

    [Fact]
    public void The_feed_maps_each_event_to_champions()
    {
        var game = Map(LiveGameSamples.AllGameData);

        Assert.Equal(
        [
            LiveEventKinds.Kill, LiveEventKinds.FirstBlood, LiveEventKinds.Dragon, LiveEventKinds.Grub, LiveEventKinds.Grub,
            LiveEventKinds.Grub, LiveEventKinds.Kill, LiveEventKinds.Kill, LiveEventKinds.Kill, LiveEventKinds.Multikill,
            LiveEventKinds.Turret, LiveEventKinds.FirstTower, LiveEventKinds.Dragon, LiveEventKinds.Kill,
        ], game.Events.Select(e => e.Kind));
        LiveEvent ById(int id) => game.Events.Single(e => e.Id == id);

        var firstKill = ById(2);
        Assert.Equal((190.5, LiveTeams.Order, true), (firstKill.Time, firstKill.Side, firstKill.IsMyTeam));
        Assert.Equal((103, "Ahri", "Me"), (firstKill.ActorChampionId, firstKill.ActorChampionName, firstKill.ActorGameName));
        Assert.Equal((134, "Syndra", ""), (firstKill.VictimChampionId, firstKill.VictimChampionName, firstKill.VictimGameName));
        Assert.Equal([64], firstKill.AssistChampionIds);
        Assert.Equal((103, LiveTeams.Order), (ById(3).ActorChampionId, ById(3).Side));

        var enemyKill = ById(8);
        Assert.Equal((LiveTeams.Chaos, false, 145, ""), (enemyKill.Side, enemyKill.IsMyTeam, enemyKill.ActorChampionId, enemyKill.ActorGameName));
        Assert.Equal((412, "Ally3"), (enemyKill.VictimChampionId, enemyKill.VictimGameName));
        Assert.Equal([111], enemyKill.AssistChampionIds);

        Assert.Equal((2, 103), (ById(11).KillStreak, ById(11).ActorChampionId));
        Assert.Equal((LiveDragonTypes.Fire, false, 64, "Ally2"), (ById(4).DragonType, ById(4).Stolen, ById(4).ActorChampionId, ById(4).ActorGameName));
        Assert.Equal([103], ById(4).AssistChampionIds);
        Assert.Equal((LiveTeams.Chaos, 62, ""), (ById(7).Side, ById(7).ActorChampionId, ById(7).ActorGameName));

        var stolen = ById(14);
        Assert.Equal((LiveDragonTypes.Water, true, LiveTeams.Chaos, 62), (stolen.DragonType, stolen.Stolen, stolen.Side, stolen.ActorChampionId));

        var turret = ById(12);
        Assert.Equal((LiveTeams.Order, LiveLanes.Bot, 222, "Ally4"), (turret.Side, turret.Lane, turret.ActorChampionId, turret.ActorGameName));
        Assert.Equal([412], turret.AssistChampionIds);

        // Executed by a turret: no champion did it, the victim's enemies scored.
        var execution = ById(15);
        Assert.Equal((0, "", 122, "", LiveTeams.Order), (execution.ActorChampionId, execution.ActorChampionName, execution.VictimChampionId, execution.VictimGameName, execution.Side));
        Assert.Equal("", game.Result);
    }

    [Fact]
    public void The_late_feed_has_objectives_an_ace_the_inhibitor_and_the_result()
    {
        var game = Map(LiveGameSamples.AllGameDataLate);
        LiveEvent ById(int id) => game.Events.Single(e => e.Id == id);

        Assert.Equal((LiveEventKinds.Herald, 64), (ById(16).Kind, ById(16).ActorChampionId));
        Assert.Equal((LiveEventKinds.Dragon, LiveDragonTypes.Hextech, 103), (ById(17).Kind, ById(17).DragonType, ById(17).ActorChampionId));
        Assert.Equal((LiveEventKinds.Inhibitor, LiveTeams.Order, LiveLanes.Bot), (ById(18).Kind, ById(18).Side, ById(18).Lane));
        Assert.Equal(LiveEventKinds.Baron, ById(19).Kind);
        Assert.Equal([103, 86, 412, 222], ById(19).AssistChampionIds);
        Assert.Equal((LiveEventKinds.Ace, LiveTeams.Order, 103, "Me"), (ById(20).Kind, ById(20).Side, ById(20).ActorChampionId, ById(20).ActorGameName));
        Assert.Equal((LiveEventKinds.InhibitorRespawned, LiveTeams.Chaos, LiveLanes.Bot, false), (ById(21).Kind, ById(21).Side, ById(21).Lane, ById(21).IsMyTeam));
        Assert.Equal((LiveEventKinds.GameEnd, MatchResults.Win, ""), (ById(22).Kind, ById(22).Result, ById(22).Side));
        Assert.Equal(MatchResults.Win, game.Result);
        Assert.Equal(21, game.Events.Count); // GameStart and MinionsSpawning are not in the feed
    }

    [Fact]
    public void A_lost_game_reports_a_loss()
    {
        var game = Map(LiveGameSamples.AllGameDataLate.Replace("\"Result\": \"Win\"", "\"Result\": \"Lose\""));
        Assert.Equal(MatchResults.Loss, game.Result);
    }

    [Fact]
    public void The_feed_keeps_the_newest_events_but_counts_all_of_them()
    {
        var events = Enumerable.Range(1, 40).Select(i => Kill(i, 100 + i, i % 2 == 0 ? "Me" : "Ally1", "Enemy3")).ToArray();
        var game = Map(Game(200, events));

        Assert.Equal(LiveGameState.MaxEvents, game.Events.Count);
        Assert.Equal(Enumerable.Range(11, 30), game.Events.Select(e => e.Id));
        // Kills are the scoreboard's, the events only feed the list.
        Assert.Equal(3, Team(game, LiveTeams.Order).Kills);
    }

    [Fact]
    public void A_name_two_players_share_resolves_to_nobody()
    {
        var game = Map(With(LiveGameSamples.AllGameData, root => root["allPlayers"]![1]!["riotIdGameName"] = "Enemy3"));

        var kill = game.Events.Single(e => e.Id == 2);
        Assert.Equal((0, ""), (kill.VictimChampionId, kill.VictimChampionName));
        Assert.Equal(103, kill.ActorChampionId);
    }

    [Fact]
    public void Events_resolve_names_by_riot_id_and_summoner_name_too()
    {
        var game = Map(Game(300,
            Kill(1, 200, "Me#TAG0", "Enemy3#TAG7"),
            """{ "EventID": 2, "EventName": "ChampionKill", "EventTime": 210, "KillerName": "Nobody", "VictimName": "Ally1", "Assisters": ["Enemy4#TAG8", "Ghost"] }"""));

        Assert.Equal((103, 134), (game.Events[0].ActorChampionId, game.Events[0].VictimChampionId));
        Assert.Equal((0, 86, LiveTeams.Chaos), (game.Events[1].ActorChampionId, game.Events[1].VictimChampionId, game.Events[1].Side));
        Assert.Equal([145], game.Events[1].AssistChampionIds);
    }

    // --- Timers ------------------------------------------------------------------------------

    [Fact]
    public void Timers_at_fourteen_minutes()
    {
        var timers = Map(LiveGameSamples.AllGameData).Timers!;

        Assert.Equal(LiveObjectiveKinds.Dragon, timers.Dragon.Kind);
        Assert.Equal(1112.6, timers.Dragon.SpawnsAt!.Value, 3);
        Assert.Equal("", timers.Dragon.DragonType); // two drakes: the rift's element isn't known yet
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Baron, 1200, null, ""), timers.Baron);
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Herald, 900, 1185, ""), timers.Herald);
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Grubs, null, 885, ""), timers.Grubs);
        Assert.Empty(timers.Inhibitors);
    }

    [Fact]
    public void Timers_after_the_third_drake_herald_baron_and_an_inhibitor()
    {
        var timers = Map(LiveGameSamples.AllGameDataLate).Timers!;

        Assert.Equal(1450.3, timers.Dragon.SpawnsAt!.Value, 3);
        Assert.Equal(LiveDragonTypes.Hextech, timers.Dragon.DragonType);
        Assert.Equal(1865.2, timers.Baron.SpawnsAt!.Value, 3);
        Assert.Null(timers.Herald.SpawnsAt);
        Assert.Empty(timers.Inhibitors); // it respawned
    }

    [Fact]
    public void Before_any_kill_the_first_spawns_apply()
    {
        var timers = Map(Game(120, """{ "EventID": 0, "EventName": "GameStart", "EventTime": 0.03 }""")).Timers!;

        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Dragon, 300, null, ""), timers.Dragon);
        Assert.Equal(1200, timers.Baron.SpawnsAt);
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Herald, 900, 1185, ""), timers.Herald);
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Grubs, 480, 885, ""), timers.Grubs);
    }

    [Fact]
    public void The_third_drake_reveals_the_rift_type()
    {
        var two = Map(Game(800, Drake(1, 320, "Fire", "Ally2"), Drake(2, 700, "Earth", "Enemy2"))).Timers!;
        Assert.Equal((1000.0, ""), (two.Dragon.SpawnsAt!.Value, two.Dragon.DragonType));

        var three = Map(Game(1100, Drake(1, 320, "Fire", "Ally2"), Drake(2, 700, "Earth", "Enemy2"), Drake(3, 1020, "Chemtech", "Ally2"))).Timers!;
        Assert.Equal((1320.0, LiveDragonTypes.Chemtech), (three.Dragon.SpawnsAt!.Value, three.Dragon.DragonType));
    }

    [Fact]
    public void A_fourth_drake_for_one_team_brings_the_elder()
    {
        string[] split = [Drake(1, 300, "Fire", "Ally2"), Drake(2, 600, "Water", "Enemy2"), Drake(3, 900, "Water", "Ally2"), Drake(4, 1200, "Water", "Enemy2"), Drake(5, 1500, "Water", "Ally2")];
        var noSoul = Map(Game(1600, split)).Timers!;
        Assert.Equal((LiveObjectiveKinds.Dragon, 1800.0, LiveDragonTypes.Water), (noSoul.Dragon.Kind, noSoul.Dragon.SpawnsAt!.Value, noSoul.Dragon.DragonType));

        var soul = Map(Game(1900, [.. split, Drake(6, 1800, "Water", "Ally2")]));
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Elder, 2160, null, LiveDragonTypes.Elder), soul.Timers!.Dragon);
        Assert.Equal(4, Team(soul, LiveTeams.Order).Dragons.Count);

        var elder = Map(Game(2300, [.. split, Drake(6, 1800, "Water", "Ally2"), Drake(7, 2200, "Elder", "Enemy2")]));
        Assert.Equal(new LiveObjectiveTimer(LiveObjectiveKinds.Elder, 2560, null, LiveDragonTypes.Elder), elder.Timers!.Dragon);
        Assert.Equal((1, 2), (Team(elder, LiveTeams.Chaos).Elders, Team(elder, LiveTeams.Chaos).Dragons.Count));
        Assert.Equal(LiveDragonTypes.Elder, elder.Events[^1].DragonType);
    }

    [Fact]
    public void Grubs_and_herald_leave_when_their_window_closes()
    {
        string grub = """{ "EventID": 1, "EventName": "HordeKill", "EventTime": 500, "Stolen": "False", "KillerName": "Ally2", "Assisters": [] }""";

        var open = Map(Game(880, grub)).Timers!;
        Assert.Equal((480.0, 885.0), (open.Grubs.SpawnsAt!.Value, open.Grubs.DespawnsAt!.Value));
        Assert.Null(Map(Game(885, grub)).Timers!.Grubs.SpawnsAt);

        Assert.Equal(900, Map(Game(1184)).Timers!.Herald.SpawnsAt);
        Assert.Null(Map(Game(1185)).Timers!.Herald.SpawnsAt);
        Assert.Null(Map(Game(1000, """{ "EventID": 1, "EventName": "HeraldKill", "EventTime": 950, "Stolen": "True", "KillerName": "Enemy2", "Assisters": [] }""")).Timers!.Herald.SpawnsAt);
    }

    [Fact]
    public void Barons_respawn_six_minutes_after_each_kill()
    {
        var timers = Map(Game(1700,
            """{ "EventID": 1, "EventName": "BaronKill", "EventTime": 1250, "Stolen": "False", "KillerName": "Ally2", "Assisters": [] }""",
            """{ "EventID": 2, "EventName": "BaronKill", "EventTime": 1650, "Stolen": "True", "KillerName": "Enemy2", "Assisters": [] }""")).Timers!;

        Assert.Equal(2010, timers.Baron.SpawnsAt);
    }

    [Fact]
    public void Destroyed_inhibitors_count_down_until_they_respawn()
    {
        string[] events =
        [
            """{ "EventID": 1, "EventName": "InhibKilled", "EventTime": 1400, "InhibKilled": "Barracks_T2_R1", "KillerName": "Ally4", "Assisters": [] }""",
            """{ "EventID": 2, "EventName": "InhibKilled", "EventTime": 1350, "InhibKilled": "Barracks_T1_L1", "KillerName": "Minion_T200L0S30N0001", "Assisters": [] }""",
        ];

        var timers = Map(Game(1500, events)).Timers!;
        Assert.Equal([new LiveInhibitorTimer(LiveTeams.Order, LiveLanes.Top, 1650), new LiveInhibitorTimer(LiveTeams.Chaos, LiveLanes.Bot, 1700)], timers.Inhibitors);

        // Respawned by the event or by the clock.
        string respawned = """{ "EventID": 3, "EventName": "InhibRespawned", "EventTime": 1660, "InhibRespawned": "Barracks_T1_L1" }""";
        Assert.Equal([new LiveInhibitorTimer(LiveTeams.Chaos, LiveLanes.Bot, 1700)], Map(Game(1660, [.. events, respawned])).Timers!.Inhibitors);
        Assert.Empty(Map(Game(1700, events)).Timers!.Inhibitors);
    }

    [Theory]
    [InlineData("ARAM", 12)]
    [InlineData("CHERRY", 30)]
    [InlineData("PRACTICETOOL", 11)]
    [InlineData("URF", 11)]
    public void Other_modes_have_no_timers(string gameMode, int mapNumber)
    {
        var game = Map(With(LiveGameSamples.AllGameData, root =>
        {
            root["gameData"]!["gameMode"] = gameMode;
            root["gameData"]!["mapNumber"] = mapNumber;
        }));

        Assert.Null(game.Timers);
        Assert.Equal(2, game.Teams.Count);
    }

    // --- Leniency and size -----------------------------------------------------------------

    [Theory]
    [InlineData("""{ "gameData": { "gameTime": 5.5 } }""")]
    [InlineData("""{ "allPlayers": "x", "events": [], "activePlayer": 3, "gameData": [] }""")]
    [InlineData("""{ "allPlayers": [ 1, { "team": 7, "scores": "x", "items": {}, "summonerSpells": [] } ], "events": { "Events": [ 1, { "EventName": "TurretKilled" } ] } }""")]
    public void Odd_shapes_map_without_throwing(string json)
    {
        var game = Map(json);
        Assert.True(game.Available);
        Assert.Null(game.Me);
    }

    [Fact]
    public void A_non_object_answer_is_unavailable()
    {
        Assert.False(Map("[]").Available);
        Assert.Null(LiveGameMapper.GameTime(Json("[]")));
        Assert.Equal(840.2, LiveGameMapper.GameTime(Json(LiveGameSamples.AllGameData)));
    }

    [Fact]
    public void A_full_feed_stays_small()
    {
        var events = Enumerable.Range(1, 200).Select(i => Kill(i, 100 + i, "Me", "Enemy3")).ToArray();
        var json = Serialized(Map(Game(1800, events)));

        // Keep a full feed well under 32 KB so a state message stays cheap over Wi-Fi.
        Assert.True(json.Length < 32 * 1024, $"{json.Length} bytes");
    }

    // --- Store and catalog ------------------------------------------------------------------

    [Fact]
    public void The_store_republishes_only_when_more_than_the_clock_changed()
    {
        var store = new LiveGameStore();
        var changes = 0;
        store.Changed += () => changes++;
        var game = Map(LiveGameSamples.AllGameData);

        Assert.True(store.Set(game));
        var (_, version) = store.Snapshot();

        var ticked = game with { GameTime = 841.7, UpdatedAt = Now.AddSeconds(1.5) };
        Assert.False(store.Set(ticked));
        Assert.Equal((ticked, version), store.Snapshot()); // the newest clock is kept for the next publish

        Assert.True(store.Set(ticked with { Me = game.Me! with { CurrentGold = 1300 } }));
        Assert.Equal(version + 1, store.Snapshot().Version);

        store.Touch();
        Assert.Equal(version + 2, store.Snapshot().Version);
        Assert.Equal(1300, store.Snapshot().Value.Me!.CurrentGold);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void The_champion_name_map_prefers_aliases_and_the_regular_champion()
    {
        Assert.Equal(62, Champions["MonkeyKing"]);
        Assert.Equal(62, Champions["wukong"]);
        Assert.Equal(145, Champions["Kai'Sa"]);
        Assert.Equal(1, Champions["Annie"]); // not Jade_Annie's 60001
        Assert.Equal(60001, Champions["Jade_Annie"]);
        Assert.False(Champions.ContainsKey("None"));
        Assert.Equal([1, 62, 64, 86, 103, 111, 122, 134, 145, 222, 412, 60001], ChampionCatalog.Ids(Json(LiveGameSamples.ChampionSummary)));
    }
}
