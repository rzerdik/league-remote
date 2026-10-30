using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Auto pick in custom games. Custom lobbies are queues of their own (3100 SR Blind, 3110 SR Draft, 3130 SR Tournament
/// Draft, …) and the session says <c>isCustomGame</c>.
/// </summary>
public sealed class AutoPickCustomGameTests
{
    [Fact]
    public async Task Blind_custom_game_hovers_and_locks_the_first_pick_with_the_blind_preset()
    {
        using var h = new AutoPickHarness(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [103, 99])]),
            AutoPickSamples.CustomBlindSession);

        await h.ObserveAsync();

        Assert.Equal((AutoPickRunStatus.Active, AutoPickQueueGroups.Blind, AutoPickRoles.Any), (h.Run.Status, h.Run.QueueGroup, h.Run.Role));
        Assert.Equal(["PATCH /lol-champ-select/v1/session/actions/2 {\"championId\":103}"], h.Writes);
        Assert.Equal(new AutoPickPending(AutoPickActionKinds.Pick, 2, 103, Start + AutoPickState.AutoLockDelay), h.Run.Pending);

        await h.AdvanceAsync(3);

        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/actions/2 {\"championId\":103}",
                "PATCH /lol-champ-select/v1/session/actions/2 {\"championId\":103,\"completed\":true}",
            ],
            h.Writes);
        Assert.Equal((AutoPickLogKinds.Locked, "Auto-locked Ahri"), (h.Run.Log.Single().Kind, h.Run.Log.Single().Text));
    }

    [Theory]
    [InlineData(3110)] // SR Draft Pick custom
    [InlineData(3130)] // SR Tournament Draft custom
    public async Task Draft_custom_game_bans_with_the_draft_preset(int queueId)
    {
        using var h = new AutoPickHarness(Draft(), AutoPickSamples.CustomDraftBanTurn);
        h.Lcu.Session["queueId"] = queueId;

        await h.ObserveAsync();
        await h.AdvanceAsync(3);

        Assert.Equal((AutoPickRunStatus.Active, AutoPickQueueGroups.Draft, AutoPickRoles.Any), (h.Run.Status, h.Run.QueueGroup, h.Run.Role));
        Assert.Equal(
            [
                "PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":238}",
                "PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":238,\"completed\":true}",
            ],
            h.Writes);
        Assert.Equal("Auto-banned Zed", h.Run.Log.Single().Text);
    }

    [Fact]
    public async Task Draft_custom_game_picks_when_the_local_players_turn_comes()
    {
        using var h = new AutoPickHarness(Draft(), AutoPickSamples.CustomDraftBanTurn);
        foreach (var id in new long[] { 10, 11, 15, 20 })
            h.Lcu.Action(id)["completed"] = true;
        foreach (var id in new long[] { 10, 11, 15 })
            h.Lcu.Action(id)["isInProgress"] = false;
        h.Lcu.Action(21)["isInProgress"] = true;
        h.Lcu.Action(25)["isInProgress"] = true;

        await h.ObserveAsync();

        Assert.Equal(["PATCH /lol-champ-select/v1/session/actions/21 {\"championId\":103}"], h.Writes);
    }

    // A custom session without a queue id can't be told apart: Practice Tool, Howling Abyss and Summoner's Rift customs
    // look the same in the session, and the champ-select rebuild reads no lobby (gameMode / mapId). None is automated.

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Practice_tool_session_without_a_queue_id_writes_nothing(int queueId)
    {
        // Practice Tool: one player, Simul Pick, no bans.
        using var h = new AutoPickHarness(BlindAndDraft(), AutoPickSamples.CustomBlindSession);
        h.Lcu.Session["queueId"] = queueId;
        h.Lcu.Session["myTeam"]!.AsArray().RemoveAt(0);
        h.Lcu.Session["theirTeam"]!.AsArray().Clear();

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal(AutoPickRunStatus.Unsupported, h.Run.Status);
    }

    [Theory]
    [InlineData(false)] // Howling Abyss Blind custom
    [InlineData(true)] // Howling Abyss Draft / Tournament Draft custom
    public async Task Howling_abyss_custom_without_a_queue_id_writes_nothing(bool draft)
    {
        using var h = new AutoPickHarness(BlindAndDraft(), draft ? AutoPickSamples.CustomDraftBanTurn : AutoPickSamples.CustomBlindSession);
        h.Lcu.Session["queueId"] = -1;

        await h.ObserveAsync();
        await h.AdvanceAsync(5);

        Assert.Empty(h.Lcu.Requests);
        Assert.Equal(AutoPickRunStatus.Unsupported, h.Run.Status);
    }

    [Theory]
    [InlineData(3100, AutoPickQueueGroups.Blind)]
    [InlineData(3110, AutoPickQueueGroups.Draft)]
    public async Task Summoners_rift_customs_are_automated_by_their_queue_id(int queueId, string group)
    {
        using var h = new AutoPickHarness(
            BlindAndDraft(),
            group == AutoPickQueueGroups.Blind ? AutoPickSamples.CustomBlindSession : AutoPickSamples.CustomDraftBanTurn);
        h.Lcu.Session["queueId"] = queueId;

        await h.ObserveAsync();

        Assert.Equal((AutoPickRunStatus.Active, group), (h.Run.Status, h.Run.QueueGroup));
        Assert.Single(h.Writes);
    }

    private static AutoPickSettings BlindAndDraft() =>
        Draft(true, new AutoPickPreset(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [99]));
}
