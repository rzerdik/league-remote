using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

public sealed class AutoPickWhitelistTests
{
    private static readonly int[] TenPicks = [103, 99, 61, 4, 7, 245, 134, 517, 38, 1];

    public static TheoryData<RemoteCommand> Valid() =>
    [
        new("a1", CommandType.SetAutoPickEnabled, Enabled: true),
        new("a2", CommandType.SetAutoPickEnabled, Enabled: false),
        new("a3", CommandType.PauseAutoPick),
        new("a4", CommandType.ResumeAutoPick),
        new("a5", CommandType.CancelAutoPickAction),
        new("a6", CommandType.CancelAutoPickAction, ActionId: 7),
        new("a6", CommandType.CancelAutoPickAction, ActionId: 0),
        new("a7", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "MIDDLE",
            BanChampionIds: [238, 157, 777, 84, 105], PickChampionIds: TenPicks, Spell1Id: 4, Spell2Id: 14, RunePageId: 1234567890123),
        new("a8", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "ANY", PickChampionIds: [103]),
        new("a9", CommandType.SetAutoPickPreset, QueueGroup: "blind", Role: "UTILITY", Spell1Id: 4, Spell2Id: 3),
        new("a10", CommandType.SetAutoPickPreset, QueueGroup: "aram", Role: "ANY", Spell1Id: 32, Spell2Id: 4, BanChampionIds: [], PickChampionIds: []),
        new("a11", CommandType.SetAutoPickPreset, QueueGroup: "aram", Role: "ANY", RunePageId: 5),
        // Nothing set: removes the preset.
        new("a12", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP"),
        new("a13", CommandType.SetAutoAcceptEnabled, Enabled: true),
        new("a14", CommandType.SetAutoAcceptEnabled, Enabled: false),
    ];

    public static TheoryData<RemoteCommand> Invalid() =>
    [
        new("a1", CommandType.SetAutoPickEnabled),
        new("a3", CommandType.CancelAutoPickAction, ActionId: -3),
        new("a4", CommandType.SetAutoPickPreset),
        new("a5", CommandType.SetAutoPickPreset, Role: "MIDDLE"),
        new("a6", CommandType.SetAutoPickPreset, QueueGroup: "arena", Role: "ANY"),
        new("a7", CommandType.SetAutoPickPreset, QueueGroup: "DRAFT", Role: "ANY"),
        new("a8", CommandType.SetAutoPickPreset, QueueGroup: "draft"),
        new("a9", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "FILL"),
        new("a10", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "middle"),
        new("a11", CommandType.SetAutoPickPreset, QueueGroup: "aram", Role: "MIDDLE"),
        new("a12", CommandType.SetAutoPickPreset, QueueGroup: "aram", Role: "ANY", PickChampionIds: [103]),
        new("a13", CommandType.SetAutoPickPreset, QueueGroup: "aram", Role: "ANY", BanChampionIds: [238]),
        new("a14", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", BanChampionIds: [1, 2, 3, 4, 5, 6]),
        new("a15", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", PickChampionIds: [.. TenPicks, 2]),
        new("a16", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", PickChampionIds: [103, 0]),
        new("a17", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", BanChampionIds: [-1]),
        new("a18", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", PickChampionIds: [103, 99, 103]),
        new("a19", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", Spell1Id: 4),
        new("a20", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", Spell2Id: 4),
        new("a21", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", Spell1Id: 4, Spell2Id: 4),
        new("a22", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", Spell1Id: 0, Spell2Id: 0),
        new("a23", CommandType.SetAutoPickPreset, QueueGroup: "draft", Role: "TOP", RunePageId: 0),
        new("a24", CommandType.SetAutoAcceptEnabled),
    ];

    [Theory, MemberData(nameof(Valid))]
    public void Accepts_valid_commands(RemoteCommand command) => Assert.Null(CommandWhitelist.Validate(command));

    [Theory, MemberData(nameof(Invalid))]
    public void Rejects_invalid_commands(RemoteCommand command) => Assert.NotNull(CommandWhitelist.Validate(command));

    [Fact]
    public void Queues_map_to_their_groups_and_others_are_not_automated()
    {
        Assert.Equal(AutoPickQueueGroups.Draft, AutoPickQueueGroups.ForQueue(420));
        Assert.Equal(AutoPickQueueGroups.Draft, AutoPickQueueGroups.ForQueue(440));
        Assert.Equal(AutoPickQueueGroups.Blind, AutoPickQueueGroups.ForQueue(480));
        Assert.Equal(AutoPickQueueGroups.Aram, AutoPickQueueGroups.ForQueue(450));
        Assert.Null(AutoPickQueueGroups.ForQueue(1750)); // Arena
        Assert.Equal(AutoPickQueueGroups.Blind, AutoPickQueueGroups.ForQueue(3100)); // SR Blind Pick custom
        Assert.Equal(AutoPickQueueGroups.Draft, AutoPickQueueGroups.ForQueue(3110)); // SR Draft Pick custom
        Assert.Equal(AutoPickQueueGroups.Draft, AutoPickQueueGroups.ForQueue(3130)); // SR Tournament Draft custom
        Assert.Null(AutoPickQueueGroups.ForQueue(3140)); // Practice Tool
        Assert.Null(AutoPickQueueGroups.ForQueue(3120)); // SR All Random custom
        Assert.Null(AutoPickQueueGroups.ForQueue(3200)); // Howling Abyss Blind custom
        Assert.Null(AutoPickQueueGroups.ForQueue(-1));
    }

    [Theory]
    [InlineData("middle", "MIDDLE")]
    [InlineData("utility", "UTILITY")]
    [InlineData("", "ANY")]
    [InlineData(null, "ANY")]
    [InlineData("fill", "ANY")]
    [InlineData("any", "ANY")]
    public void Assigned_position_becomes_a_role(string? assigned, string role) =>
        Assert.Equal(role, AutoPickRoles.FromAssignedPosition(assigned));

    [Fact]
    public void Role_preset_overrides_only_what_it_sets_and_falls_back_to_any()
    {
        var settings = new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [103, 99], 4, 14, 11),
            new(AutoPickQueueGroups.Draft, Positions.Jungle, [], [64], 4, 11),
            new(AutoPickQueueGroups.Blind, Positions.Top, [], [86]),
        ]);

        var jungle = settings.Resolve(AutoPickQueueGroups.Draft, Positions.Jungle)!;
        Assert.Equal([238], jungle.BanChampionIds);
        Assert.Equal([64], jungle.PickChampionIds);
        Assert.Equal((4, 11), (jungle.Spell1Id, jungle.Spell2Id));
        Assert.Equal(11, jungle.RunePageId);

        Assert.Equal([103, 99], settings.Resolve(AutoPickQueueGroups.Draft, Positions.Top)!.PickChampionIds);
        Assert.Equal([86], settings.Resolve(AutoPickQueueGroups.Blind, Positions.Top)!.PickChampionIds);
        Assert.Null(settings.Resolve(AutoPickQueueGroups.Blind, AutoPickRoles.Any));
        Assert.Null(settings.Resolve(AutoPickQueueGroups.Aram, AutoPickRoles.Any));
    }

    [Fact]
    public void A_rune_page_name_falls_back_with_its_page()
    {
        var settings = new AutoPickSettings(true,
        [
            new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [103], RunePageId: 11, RunePageName: "Mid Electrocute"),
            new(AutoPickQueueGroups.Draft, Positions.Jungle, [], [64]),
            new(AutoPickQueueGroups.Draft, Positions.Top, [], [86], RunePageId: 12, RunePageName: "Top Grasp"),
        ]);

        var jungle = settings.Resolve(AutoPickQueueGroups.Draft, Positions.Jungle)!;
        Assert.Equal((11L, "Mid Electrocute"), (jungle.RunePageId, jungle.RunePageName));
        var top = settings.Resolve(AutoPickQueueGroups.Draft, Positions.Top)!;
        Assert.Equal((12L, "Top Grasp"), (top.RunePageId, top.RunePageName));
    }

    [Fact]
    public void Older_auto_pick_json_without_the_new_fields_still_reads()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var preset = JsonSerializer.Deserialize<AutoPickPreset>(
            """{"queueGroup":"draft","role":"ANY","banChampionIds":[],"pickChampionIds":[103],"spell1Id":0,"spell2Id":0,"runePageId":11}""", web)!;
        var run = JsonSerializer.Deserialize<AutoPickRun>(
            """{"queueGroup":"draft","role":"ANY","status":"active","pending":null,"log":[]}""", web)!;

        Assert.Equal("", preset.RunePageName);
        Assert.Equal(0, run.Id);
    }

    [Fact]
    public void A_preset_with_nothing_set_is_empty() =>
        Assert.True(new AutoPickPreset(AutoPickQueueGroups.Draft, Positions.Top, [], []).IsEmpty);

    [Fact]
    public void Preset_command_reads_id_lists_from_the_phone_json_and_the_state_has_no_computed_fields()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var command = JsonSerializer.Deserialize<RemoteCommand>(
            """{"requestId":"a1","type":"SetAutoPickPreset","queueGroup":"draft","role":"TOP","pickChampionIds":[86,122],"spell1Id":4,"spell2Id":12}""",
            web)!;
        Assert.Equal(CommandType.SetAutoPickPreset, command.Type);
        Assert.Equal([86, 122], command.PickChampionIds);
        Assert.Null(command.BanChampionIds);
        Assert.Null(CommandWhitelist.Validate(command));

        var json = JsonSerializer.Serialize(
            new AutoPickSettings(true, [new(AutoPickQueueGroups.Draft, Positions.Top, [], [86], 4, 12)]), web);
        Assert.Contains("\"pickChampionIds\":[86]", json);
        Assert.DoesNotContain("isEmpty", json);
    }
}
