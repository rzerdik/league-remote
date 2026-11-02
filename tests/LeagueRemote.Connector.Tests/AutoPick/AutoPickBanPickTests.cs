using System.Net;
using System.Text.Json;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>Auto pick's bans and picks in draft: when it writes nothing, what it hovers and when it locks.</summary>
public sealed class AutoPickBanPickTests
{
    // --- No writes ------------------------------------------------------------------------

    [Fact]
    public async Task Nothing_is_written_while_auto_pick_is_disabled()
    {
        using var h = new AutoPickHarness(Draft(enabled: false));

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal((AutoPickRunStatus.Disabled, AutoPickQueueGroups.Draft, Positions.Middle), (h.Run.Status, h.Run.QueueGroup, h.Run.Role));
    }

    [Fact]
    public async Task Nothing_is_written_while_paused_and_the_pause_set_before_champ_select_covers_it()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync("Lobby");
        h.Service.Pause();
        await h.ObserveAsync("Lobby");
        Assert.True(h.State.PausedForThisGame);

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal(AutoPickRunStatus.Paused, h.Run.Status);
        Assert.True(h.State.PausedForThisGame);
    }

    [Theory]
    [InlineData(1700, false)] // Arena
    [InlineData(1090, false)] // TFT
    [InlineData(3140, true)] // Practice Tool
    [InlineData(3120, true)] // SR All Random custom
    [InlineData(3200, true)] // Howling Abyss Blind custom
    [InlineData(3210, true)] // Howling Abyss Draft custom
    [InlineData(3150, true)] // URF custom
    public async Task Nothing_is_written_in_an_unsupported_queue_or_custom_game(int queueId, bool custom)
    {
        var settings = Draft() with
        {
            Presets = [.. Draft().Presets, new(AutoPickQueueGroups.Aram, AutoPickRoles.Any, [], [], 32, 4)],
        };
        using var h = new AutoPickHarness(settings);
        h.Lcu.Session["queueId"] = queueId;
        h.Lcu.Session["isCustomGame"] = custom;

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal((AutoPickRunStatus.Unsupported, "", ""), (h.Run.Status, h.Run.QueueGroup, h.Run.Role));
    }

    [Fact]
    public async Task Nothing_is_written_without_a_preset_for_the_group_and_role()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [103]),
            new(AutoPickQueueGroups.Draft, Positions.Top, [238], [86]),
        ]));

        await h.ObserveAsync();

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal(AutoPickRunStatus.NoPreset, h.Run.Status);
    }

    [Fact]
    public async Task A_ban_marked_in_progress_during_planning_is_left_alone()
    {
        using var h = new AutoPickHarness(Draft());
        h.Lcu.SetTimer("PLANNING", 25000);

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal(AutoPickRunStatus.Active, h.Run.Status);
        Assert.Empty(h.Run.Log);
    }

    [Fact]
    public async Task Run_is_null_outside_champ_select()
    {
        using var h = new AutoPickHarness(Draft());

        await h.ObserveAsync("Lobby");
        Assert.Null(h.State.Run);

        await h.ObserveAsync();
        Assert.NotNull(h.State.Run);

        await h.ObserveAsync("InProgress");
        Assert.Null(h.State.Run);
    }

    // --- Ban ------------------------------------------------------------------------------

    [Fact]
    public async Task Ban_hovers_the_first_valid_choice_and_completes_it_in_the_last_second_of_the_turn()
    {
        using var h = new AutoPickHarness(Draft());

        await h.ObserveAsync();

        Assert.Equal([GetSession, Hover11Zed, Summary], h.Lcu.Requests);
        Assert.Equal(new AutoPickPending(AutoPickActionKinds.Ban, 11, 238, Start + TimeSpan.FromSeconds(3)), h.Run.Pending);
        Assert.Equal(AutoPickRunStatus.Active, h.Run.Status);

        await h.AdvanceAsync(2.9);
        Assert.Equal(3, h.Lcu.Requests.Count); // not yet

        await h.AdvanceAsync(0.1);

        Assert.Equal(
            [
                GetSession,
                Hover11Zed,
                Summary,
                GetSession,
                "GET /lol-champ-select/v1/bannable-champion-ids",
                "PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":238,\"completed\":true}",
                GetSession,
            ],
            h.Lcu.Requests);
        Assert.Null(h.Run.Pending);
        var entry = Assert.Single(h.Run.Log);
        Assert.Equal((AutoPickLogKinds.Banned, 238, "Auto-banned Zed"), (entry.Kind, entry.ChampionId, entry.Text));

        // Champions only: nobody's name or id is in what the phone gets.
        var json = JsonSerializer.Serialize(h.State, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var leak in new[] { "Enemy", "Ally", "\"Me\"", "puuid", "9006", "Page A" })
            Assert.DoesNotContain(leak, json);
    }

    [Fact]
    public async Task Ban_skips_bans_teammate_choices_and_the_own_pick_list()
    {
        // 103 is in my own pick list, 238 banned by an enemy, 157 on a teammate's ban, 81 a teammate's intent.
        using var h = new AutoPickHarness(new AutoPickSettings(true,
            [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [103, 238, 157, 81, 55], [103, 99])]));
        h.Lcu.Action(13)["championId"] = 238;
        h.Lcu.Action(13)["completed"] = true;
        h.Lcu.Action(10)["championId"] = 157;
        h.Lcu.Member(3)["championPickIntent"] = 81;

        await h.ObserveAsync();

        Assert.Equal("PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":55}", h.Writes.Single());
        Assert.Equal(55, h.Run.Pending!.ChampionId);
    }

    [Fact]
    public async Task Ban_never_takes_the_local_players_own_pick_intent()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [7, 157], [])]));
        h.Lcu.Member(2)["championPickIntent"] = 7;

        await h.ObserveAsync();

        Assert.Equal("PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":157}", h.Writes.Single());
    }

    [Fact]
    public async Task Nothing_valid_in_the_list_is_logged_as_skipped_and_writes_nothing()
    {
        using var h = new AutoPickHarness(new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [61, 64], [])]));

        await h.ObserveAsync();
        await h.ObserveAsync();

        Assert.Equal([GetSession], h.Lcu.Requests);
        var entry = Assert.Single(h.Run.Log);
        Assert.Equal((AutoPickLogKinds.Skipped, "No champion from your ban list is available"), (entry.Kind, entry.Text));
    }

    // --- Pick -----------------------------------------------------------------------------

    [Fact]
    public async Task Pick_falls_back_past_banned_taken_locked_and_unavailable_champions()
    {
        // 55 banned by my team, 64 banned by theirs, 103 locked and 99 hovered by teammates, 81 a teammate's intent,
        // 61 locked by an enemy, 4 not pickable: 245 it is.
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [55, 64, 103, 99, 81, 61, 4, 245])]),
            AutoPickSamples.DraftPickTurn);
        h.Lcu.Pickable = [55, 64, 103, 99, 81, 61, 245, 7];

        await h.ObserveAsync();
        await h.AdvanceAsync(3);

        Assert.Equal(
            [
                GetSession,
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}",
                Summary,
                GetSession,
                "GET /lol-champ-select/v1/pickable-champion-ids",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245,\"completed\":true}",
                GetSession,
            ],
            h.Lcu.Requests);
        Assert.Equal((AutoPickLogKinds.Locked, "Auto-locked Ekko"), (h.Run.Log[^1].Kind, h.Run.Log[^1].Text));
    }

    [Fact]
    public async Task A_champion_the_user_chose_before_the_turn_is_left_alone()
    {
        using var h = new AutoPickHarness(Draft(), AutoPickSamples.DraftPickTurn);
        h.Lcu.Action(21)["championId"] = 157; // hovered, or declared as intent during planning

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        var entry = Assert.Single(h.Run.Log);
        Assert.Equal((AutoPickLogKinds.Manual, 157, "You chose yourself, auto-pick stepped aside"), (entry.Kind, entry.ChampionId, entry.Text));
    }

    [Fact]
    public async Task User_changing_the_champion_during_the_grace_period_wins_and_nothing_is_locked()
    {
        using var h = new AutoPickHarness(Draft(), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();
        Assert.Equal(245, h.Run.Pending!.ChampionId);

        h.Time.Advance(TimeSpan.FromSeconds(1));
        h.Lcu.Action(21)["championId"] = 157;
        await h.ObserveAsync();
        await h.AdvanceAsync(5);
        await h.ObserveAsync();

        Assert.Equal(["PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}"], h.Writes);
        Assert.Null(h.Run.Pending);
        Assert.Equal(AutoPickLogKinds.Manual, Assert.Single(h.Run.Log).Kind);
    }

    [Fact]
    public async Task User_changing_the_champion_right_before_the_lock_is_seen_in_the_re_read()
    {
        using var h = new AutoPickHarness(Draft(), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();

        h.Lcu.Action(21)["championId"] = 157; // no rebuild reached the automation yet
        await h.AdvanceAsync(3);

        Assert.Equal(["PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}"], h.Writes);
        Assert.Equal(AutoPickLogKinds.Manual, Assert.Single(h.Run.Log).Kind);
    }

    [Fact]
    public async Task Our_champion_taken_during_the_grace_period_moves_to_the_next_choice_with_a_new_grace_period()
    {
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [245, 7])]), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();

        h.Time.Advance(TimeSpan.FromSeconds(2));
        h.Lcu.Action(24)["championId"] = 245; // an enemy locks it
        h.Lcu.Action(24)["completed"] = true;
        await h.ObserveAsync();

        Assert.Equal(new AutoPickPending(AutoPickActionKinds.Pick, 21, 7, Start + TimeSpan.FromSeconds(5)), h.Run.Pending);
        await h.AdvanceAsync(1);
        Assert.Equal(2, h.Writes.Count); // the new grace period isn't over

        await h.AdvanceAsync(2);
        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":7}",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":7,\"completed\":true}",
            ],
            h.Writes);
        Assert.Equal("Auto-locked LeBlanc", h.Run.Log[^1].Text);
    }

    [Fact]
    public async Task Our_champion_taken_by_a_teammate_right_before_the_lock_is_seen_in_the_re_read()
    {
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [245, 7])]), AutoPickSamples.DraftPickTurn);
        await h.ObserveAsync();

        h.Lcu.Action(23)["championId"] = 245; // a teammate hovers it
        await h.AdvanceAsync(3);

        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":7}",
            ],
            h.Writes);
        Assert.Equal((7, Start + TimeSpan.FromSeconds(6)), (h.Run.Pending!.ChampionId, h.Run.Pending.CompletesAt));
    }

    [Fact]
    public async Task An_action_that_ended_during_the_grace_period_is_not_touched()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();

        h.Lcu.Action(11)["completed"] = true; // the timer ran out and League banned the hover itself
        h.Lcu.Action(11)["isInProgress"] = false;
        await h.AdvanceAsync(3);

        Assert.Equal([Hover11Zed], h.Writes);
        Assert.Null(h.Run.Pending);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("pause")]
    [InlineData("disable")]
    public async Task Cancel_pause_or_disable_during_the_grace_period_stops_the_completion_and_keeps_the_hover(string how)
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();

        var error = how switch
        {
            "cancel" => h.Service.Cancel(11),
            "pause" => h.Service.Pause(),
            _ => h.Service.SetEnabled(false),
        };
        Assert.Null(error);
        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickLogKinds.Cancelled, "Cancelled auto-ban of Zed"), (h.Run.Log[^1].Kind, h.Run.Log[^1].Text));

        await h.AdvanceAsync(5);
        await h.ObserveAsync();

        Assert.Equal([Hover11Zed], h.Writes);
    }

    [Fact]
    public async Task Too_little_time_left_hovers_only()
    {
        using var h = new AutoPickHarness(Draft());
        h.Lcu.SetTimer("BAN_PICK", 900);

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Equal([Hover11Zed], h.Writes);
        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickLogKinds.Hovered, "Hovered Zed (too little time to lock in)"), (h.Run.Log[^1].Kind, h.Run.Log[^1].Text));
    }

    [Theory]
    [InlineData(4000, false, 3)]
    [InlineData(30000, false, 29)] // a full turn: the whole of it stays yours
    [InlineData(1500, false, 0.5)]
    [InlineData(0, true, 3)] // infinite timer: no last second, a short wait instead
    public async Task Completion_is_one_second_before_the_phase_end(double timeLeftMs, bool infinite, double seconds)
    {
        using var h = new AutoPickHarness(Draft());
        h.Lcu.SetTimer("BAN_PICK", timeLeftMs, infinite);

        await h.ObserveAsync();
        Assert.Equal(Start + TimeSpan.FromSeconds(seconds), h.Run.Pending!.CompletesAt);

        await h.AdvanceAsync(seconds);
        Assert.Equal(AutoPickLogKinds.Banned, h.Run.Log[^1].Kind);
    }

    [Fact]
    public async Task Phase_end_counts_from_leagues_timer_anchor_not_from_the_read()
    {
        // League's 6 s were left 3 s before the read: the phase ends 3 s after it, so the lock comes at 2 s, not 5 s.
        using var h = new AutoPickHarness(Draft());
        h.Lcu.SetTimer("BAN_PICK", 6000);
        h.Lcu.Session["timer"]!["internalNowInEpochMs"] = Start.ToUnixTimeMilliseconds() - 3000;

        await h.ObserveAsync();

        Assert.Equal(Start + TimeSpan.FromSeconds(2), h.Run.Pending!.CompletesAt);
    }

    [Fact]
    public async Task League_refusing_the_lock_is_logged_as_failed()
    {
        using var h = new AutoPickHarness(Draft());
        h.Lcu.AcceptsLock = false;
        await h.ObserveAsync();

        await h.AdvanceAsync(3);

        Assert.Contains("POST /lol-champ-select/v1/session/actions/11/complete", h.Writes);
        Assert.Null(h.Run.Pending);
        Assert.Equal((AutoPickLogKinds.Failed, "League didn't accept the auto-ban"), (h.Run.Log[^1].Kind, h.Run.Log[^1].Text));
    }

    [Fact]
    public async Task League_refusing_the_hover_is_logged_as_failed_and_not_retried()
    {
        using var h = new AutoPickHarness(Draft());
        h.Lcu.Failures["PATCH /lol-champ-select/v1/session/actions/11"] = (HttpStatusCode.BadRequest, "refused");

        await h.ObserveAsync();
        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Equal([Hover11Zed], h.Writes);
        Assert.Equal(AutoPickLogKinds.Failed, Assert.Single(h.Run.Log).Kind);
    }

    [Fact]
    public async Task Each_action_is_handled_once_and_the_pick_follows_the_ban()
    {
        using var h = new AutoPickHarness(Draft());
        await h.ObserveAsync();
        await h.AdvanceAsync(3);
        await h.ObserveAsync();

        h.Lcu.Session = System.Text.Json.Nodes.JsonNode.Parse(AutoPickSamples.DraftPickTurn)!;
        await h.ObserveAsync();
        await h.ObserveAsync();

        Assert.Equal(
            [
                Hover11Zed,
                "PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":238,\"completed\":true}",
                "PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":245}",
            ],
            h.Writes);
    }

    [Fact]
    public async Task Resume_automates_a_step_the_user_left_untouched()
    {
        using var h = new AutoPickHarness(Draft());
        h.Service.Pause();
        await h.ObserveAsync();
        Assert.Empty(h.Lcu.Requests);

        h.Service.Resume();
        await h.ObserveAsync();

        Assert.Equal([Hover11Zed], h.Writes);
    }
}
