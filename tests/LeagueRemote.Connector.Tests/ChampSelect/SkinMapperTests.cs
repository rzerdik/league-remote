using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>The skin picker: <see cref="SkinMapper"/>, reading the carousel with the state, and <see cref="CommandType.SelectSkin"/>.</summary>
public sealed class SkinMapperTests
{
    private const string Carousel = "/lol-champ-select/v1/skin-carousel-skins";
    private const string Session = "/lol-champ-select/v1/session";

    private static ChampSelectSession LockedSession => ChampSelectSession.Parse(Json(SkinSamples.LockedSession))!;

    // --- Mapping ------------------------------------------------------------------------------

    [Fact]
    public void Only_skins_and_chromas_league_lets_you_equip_reach_the_phone_default_first()
    {
        var skins = ChampSelectMapper.Map(Json(SkinSamples.LockedSession), [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!.Skins;

        Assert.NotNull(skins);
        Assert.Equal(103, skins.ChampionId);
        Assert.Equal(103001, skins.SelectedSkinId);
        Assert.Equal([103000, 103001, 103015], skins.Skins.Select(s => s.Id));
        Assert.True(skins.Skins[0].IsBase);
        Assert.Equal("Ahri", skins.Skins[0].Name);

        var starGuardian = skins.Skins[2];
        Assert.Equal("Star Guardian Ahri", starGuardian.Name);
        Assert.Equal([103016, 103019], starGuardian.Chromas.Select(c => c.Id));
        Assert.Equal("Star Guardian Ahri (Rose Quartz)", starGuardian.Chromas[0].Name);
        Assert.Equal(["#F0A6DE", "#5BB9E8"], starGuardian.Chromas[0].Colors);
        // A tiered form: no name of its own (its short name instead) and a colour that isn't one, dropped.
        Assert.Equal("Form 2", starGuardian.Chromas[1].Name);
        Assert.Empty(starGuardian.Chromas[1].Colors);
    }

    [Fact]
    public void No_skins_while_your_pick_is_still_to_make()
    {
        var cs = ChampSelectMapper.Map(Json(GameStateSamples.ChampSelectSession), [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!;

        Assert.Null(cs.Skins);
    }

    [Fact]
    public void No_skins_when_the_mode_does_not_allow_choosing_one()
    {
        var session = SkinSamples.LockedSession.Replace("\"allowSkinSelection\": true", "\"allowSkinSelection\": false");

        var cs = ChampSelectMapper.Map(Json(session), [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!;

        Assert.Null(cs.Skins);
    }

    [Fact]
    public void A_dealt_bench_champion_has_skins_too()
    {
        // ARAM: no pick actions, the champion is dealt (TeamMember.IsLocked's bench rule).
        var session = Json("""
        { "localPlayerCellId": 0, "benchEnabled": true, "timer": { "phase": "FINALIZATION" }, "actions": [],
          "myTeam": [ { "cellId": 0, "championId": 103, "selectedSkinId": 0 } ] }
        """);

        var cs = ChampSelectMapper.Map(session, [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!;

        Assert.True(cs.MyTeam[0].IsLocked);
        Assert.NotNull(cs.Skins);
        Assert.Equal(0, cs.Skins.SelectedSkinId);
        Assert.Equal(3, cs.Skins.Skins.Count);
    }

    [Theory]
    // A bench mode with subset picks: the dealt champion is only a hover while the pick is open.
    [InlineData("""{ "localPlayerCellId": 0, "benchEnabled": true, "actions": [[ { "id": 1, "actorCellId": 0, "type": "pick", "completed": false } ]], "myTeam": [ { "cellId": 0, "championId": 103 } ] }""")]
    // No bench and no pick action: nothing says the champion is yours (TeamMember.IsLocked is false too).
    [InlineData("""{ "localPlayerCellId": 0, "actions": [], "myTeam": [ { "cellId": 0, "championId": 103 } ] }""")]
    public void No_skins_for_a_champion_that_is_not_locked(string sessionJson)
    {
        var cs = ChampSelectMapper.Map(Json(sessionJson), [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!;

        Assert.False(cs.MyTeam[0].IsLocked);
        Assert.Null(cs.Skins);
    }

    [Fact]
    public void An_empty_carousel_means_no_skin_picker()
    {
        Assert.Null(SkinMapper.Map(LockedSession, Json("[]")));
        Assert.Null(SkinMapper.Map(LockedSession, null));
    }

    [Fact]
    public void A_huge_carousel_is_cut_to_the_count_limits_in_the_carousel_order()
    {
        var mapped = SkinMapper.Map(LockedSession, Json(HugeCarousel(skins: 150, chromasPerSkin: 30, name: "Skin", repeat: 1)))!;

        Assert.Equal(SkinSelection.MaxSkins, mapped.Skins.Count);
        Assert.All(mapped.Skins, s => Assert.True(s.Chromas.Count <= SkinSelection.MaxChromas));
        Assert.Equal(SkinSelection.MaxTotalChromas, mapped.Skins.Sum(s => s.Chromas.Count));
        // Chromas go to the first skins: the later ones keep only the skin itself.
        Assert.Equal(SkinSelection.MaxChromas, mapped.Skins[0].Chromas.Count);
        Assert.Empty(mapped.Skins[^1].Chromas);
    }

    /// <summary>
    /// The state is republished to every phone on each champ-select change: a worst-case carousel must keep the whole
    /// <see cref="ClientState"/> small, also with names SignalR escapes (non-ASCII is written as \uXXXX, six bytes a character).
    /// </summary>
    [Theory]
    [InlineData("x")]
    [InlineData("별")]
    [InlineData("<&>")]
    public void A_worst_case_carousel_keeps_the_state_message_small(string nameChars)
    {
        var carousel = Json(HugeCarousel(skins: 250, chromasPerSkin: 60, name: nameChars));
        var cs = ChampSelectMapper.Map(Json(SkinSamples.LockedSession), [], [], skinCarousel: carousel)!;
        var state = new ClientState(true, "ChampSelect", null, null, cs, [], DateTimeOffset.UtcNow);

        // As SignalR's JSON protocol writes it: camelCase, enums as strings, the default (escaping) encoder.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var skinsBytes = JsonSerializer.SerializeToUtf8Bytes(cs.Skins, options).Length;
        var stateBytes = JsonSerializer.SerializeToUtf8Bytes(state, options).Length;

        Assert.NotEmpty(cs.Skins!.Skins);
        Assert.Equal(SkinSelection.MaxNameLength, cs.Skins.Skins[0].Name.Length);
        Assert.True(skinsBytes <= SkinSelection.MaxJsonBytes, $"skins: {skinsBytes} bytes");
        Assert.True(stateBytes < 64 * 1024, $"state: {stateBytes} bytes");
    }

    [Fact]
    public void A_skin_without_a_champion_id_belongs_to_the_champion_its_id_says()
    {
        var carousel = Json("""
        [
          { "id": 103000, "name": "Ahri", "isBase": true, "unlocked": true,
            "childSkins": [ { "id": 103016, "name": "Rose Quartz", "unlocked": true }, { "id": 99016, "name": "Not Ahri's", "unlocked": true } ] },
          { "id": 99001, "name": "Sorceress Lux", "unlocked": true }
        ]
        """);

        var skins = SkinMapper.Map(LockedSession, carousel)!;

        Assert.Equal([103000], skins.Skins.Select(s => s.Id));
        Assert.Equal([103016], skins.Skins[0].Chromas.Select(c => c.Id));
        Assert.False(SkinMapper.IsEquippable(carousel, 103, 99001));
    }

    [Fact]
    public void No_skins_once_the_game_is_starting()
    {
        var session = SkinSamples.LockedSession.Replace("\"FINALIZATION\"", "\"GAME_STARTING\"");

        var cs = ChampSelectMapper.Map(Json(session), [], [], skinCarousel: Json(SkinSamples.AhriCarousel))!;

        Assert.Null(cs.Skins);
    }

    /// <summary>A carousel of Ahri skins, each with chromas, every name <paramref name="repeat"/> times <paramref name="name"/>.</summary>
    private static string HugeCarousel(int skins, int chromasPerSkin, string name, int repeat = 200)
    {
        var longName = JsonSerializer.Serialize(string.Concat(Enumerable.Repeat(name, repeat)));
        return "[" + string.Join(",", Enumerable.Range(0, skins).Select(i =>
        {
            var chromas = string.Join(",", Enumerable.Range(1, chromasPerSkin).Select(c =>
                $$"""{ "id": {{103000 + 300 + i * chromasPerSkin + c}}, "championId": 103, "name": {{longName}}, "colors": ["#112233", "#445566"], "unlocked": true }"""));
            return $$"""{ "id": {{103000 + i}}, "championId": 103, "name": {{longName}}, "isBase": {{(i == 0 ? "true" : "false")}}, "unlocked": true, "childSkins": [{{chromas}}] }""";
        })) + "]";
    }

    // --- State rebuild ------------------------------------------------------------------------

    [Fact]
    public async Task The_carousel_is_only_read_once_the_champion_is_locked_in()
    {
        var picking = ChampSelectHandler(GameStateSamples.ChampSelectSession);
        var locked = ChampSelectHandler(SkinSamples.LockedSession);

        var before = await TestLcu.BuildStateAsync(picking);
        var after = await TestLcu.BuildStateAsync(locked);

        Assert.DoesNotContain($"GET {Carousel}", picking.Requests);
        Assert.Null(before.ChampSelect!.Skins);
        Assert.Contains($"GET {Carousel}", locked.Requests);
        Assert.Equal([103000, 103001, 103015], after.ChampSelect!.Skins!.Skins.Select(s => s.Id));
    }

    [Fact]
    public async Task A_carousel_the_client_cannot_answer_does_not_fail_the_rebuild()
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession);
        handler.Failures[$"GET {Carousel}"] = (HttpStatusCode.InternalServerError, "busy");

        var state = await TestLcu.BuildStateAsync(handler);

        Assert.NotNull(state.ChampSelect);
        Assert.Null(state.ChampSelect.Skins);
    }

    [Fact]
    public void Carousel_changes_rebuild_the_game_state()
    {
        Assert.Contains("OnJsonApiEvent_lol-champ-select_v1_skin-carousel-skins", LcuTopics.All);
        Assert.True(LcuConnectionService.RefreshesFor("OnJsonApiEvent_lol-champ-select_v1_skin-carousel-skins").State);
    }

    // --- SelectSkin -----------------------------------------------------------------------------

    [Theory]
    [InlineData(103015)] // a skin
    [InlineData(103016)] // an owned chroma
    [InlineData(103000)] // back to the default
    public async Task SelectSkin_equips_a_skin_or_chroma_from_the_carousel(int skinId)
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: skinId));

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            [
                $"GET {Session}",
                $"GET {Carousel}",
                $"PATCH {Session}/my-selection {{\"selectedSkinId\":{skinId}}}",
            ],
            handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_of_the_skin_you_already_wear_sends_nothing()
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 103001));

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("PATCH", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(103014)] // not owned
    [InlineData(103017)] // a chroma not owned
    [InlineData(103018)] // a disabled chroma
    [InlineData(103020)] // a disabled skin
    [InlineData(99001)] // another champion's skin
    [InlineData(266001)] // not in the carousel at all
    public async Task SelectSkin_refuses_what_the_carousel_does_not_let_you_equip(int skinId)
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: skinId));

        Assert.False(result.Success);
        Assert.Equal("That skin isn't available.", result.Error);
        Assert.Equal([$"GET {Session}", $"GET {Carousel}"], handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_refuses_outside_champ_select()
    {
        var handler = new FakeLcu();

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 103015));

        Assert.Equal("Not in champ select.", result.Error);
        Assert.Equal([$"GET {Session}"], handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_refuses_before_you_lock_in()
    {
        var handler = ChampSelectHandler(GameStateSamples.ChampSelectSession);

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 222001));

        Assert.Equal("Lock in a champion first.", result.Error);
        Assert.Equal([$"GET {Session}"], handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_refuses_when_the_mode_has_no_skin_choice()
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession.Replace("\"allowSkinSelection\": true", "\"allowSkinSelection\": false"));

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 103015));

        Assert.Equal("This mode doesn't let you choose a skin.", result.Error);
        Assert.Equal([$"GET {Session}"], handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_refuses_once_the_game_is_starting()
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession.Replace("\"FINALIZATION\"", "\"GAME_STARTING\""));

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 103015));

        Assert.Equal("The game is starting.", result.Error);
        Assert.Equal([$"GET {Session}"], handler.Requests);
    }

    [Fact]
    public async Task SelectSkin_reports_league_refusing_the_skin()
    {
        var handler = ChampSelectHandler(SkinSamples.LockedSession);
        handler.Failures[$"PATCH {Session}/my-selection"] = (HttpStatusCode.BadRequest, "Invalid skin selection");

        var result = await FakeLcuSession.ExecuteAsync(handler, new RemoteCommand("s1", CommandType.SelectSkin, SkinId: 103015));

        Assert.False(result.Success);
        Assert.Equal("League refused that.", result.Error);
    }

    // --- Helpers --------------------------------------------------------------------------------

    private static FakeLcu ChampSelectHandler(string session)
    {
        var handler = new FakeLcu();
        handler.Routes["/lol-gameflow/v1/gameflow-phase"] = "\"ChampSelect\"";
        handler.Routes["/lol-perks/v1/pages"] = "[]";
        handler.Routes[Session] = session;
        handler.Routes["/lol-champ-select/v1/pickable-champion-ids"] = "[64, 103, 222]";
        handler.Routes["/lol-champ-select/v1/bannable-champion-ids"] = "[64, 103, 222]";
        handler.Routes[Carousel] = SkinSamples.AhriCarousel;
        return handler;
    }
}
