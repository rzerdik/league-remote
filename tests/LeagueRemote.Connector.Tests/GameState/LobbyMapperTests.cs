using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.Tests.TestLcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>The lobby as the phone gets it: members, names, the local player and the position selector.</summary>
public sealed class LobbyMapperTests
{
    [Fact]
    public void Lobby_member_names_use_gameTag_then_resolved_names_then_Unknown()
    {
        var names = new Dictionary<string, string> { ["me-puuid"] = "Me#EUW" };

        var lobby = Assert.IsType<LobbyState>(LobbyMapper.Map(Json(GameStateSamples.Lobby), null, null, names));

        Assert.Equal(["Me#EUW", "Friend#EUNE", "Unknown"], lobby.Members.Select(m => m.RiotId));
        Assert.Equal("MIDDLE", lobby.Members[0].FirstPositionPreference);
        Assert.Null(lobby.Members[0].SecondPositionPreference);
        Assert.Equal([5132, 0, 0], lobby.Members.Select(m => m.ProfileIconId));
        Assert.Equal(420, lobby.QueueId);
        Assert.Equal("CLASSIC", lobby.GameMode);
        Assert.False(lobby.IsCustom);
        Assert.True(lobby.IsLeader);
    }

    [Fact]
    public void Lobby_marks_the_local_member_and_the_position_selector()
    {
        var ranked = LobbyMapper.Map(Json(GameStateSamples.Lobby), null, null)!;
        var practice = LobbyMapper.Map(Json(GameStateSamples.PracticeToolLobby), null, null)!;

        Assert.Equal([true, false, false], ranked.Members.Select(m => m.IsLocalPlayer));
        Assert.True(ranked.ShowPositionSelector); // no showPositionSelector field: queue 420 decides
        Assert.False(practice.ShowPositionSelector);
        Assert.True(practice.Members[0].IsLocalPlayer);
    }

    [Theory]
    [InlineData("\"showPositionSelector\": false", 420, false)]
    [InlineData("\"showPositionSelector\": true", 400, true)]
    [InlineData("\"pickType\": \"AllRandomPickStrategy\"", 450, false)]
    public void Position_selector_prefers_the_client_flag_over_the_queue_id(string extra, int queueId, bool expected)
    {
        var json = GameStateSamples.Lobby.Replace(
            "\"gameConfig\": { \"queueId\": 420,",
            $"\"gameConfig\": {{ {extra}, \"queueId\": {queueId},");

        Assert.Equal(expected, LobbyMapper.Map(Json(json), null, null)!.ShowPositionSelector);
    }

    [Fact]
    public void Local_member_falls_back_to_the_summoner_id_without_a_puuid()
    {
        var json = GameStateSamples.Lobby.Replace(
            "\"localMember\": { \"puuid\": \"me-puuid\", \"summonerId\": 1001",
            "\"localMember\": { \"puuid\": \"\", \"summonerId\": 1003");

        Assert.Equal([false, false, true], LobbyMapper.Map(Json(json), null, null)!.Members.Select(m => m.IsLocalPlayer));
    }

    [Fact]
    public void Lobby_member_name_resolves_by_summoner_id_and_falls_back_to_summonerName()
    {
        var json = GameStateSamples.Lobby.Replace(
            "{ \"puuid\": \"\", \"summonerId\": 1003, \"gameName\": \"\", \"summonerName\": \"\"",
            "{ \"puuid\": \"\", \"summonerId\": 1003, \"gameName\": \"\", \"summonerName\": \"OldName\"");

        var unresolved = LobbyMapper.Map(Json(json), null, null)!;
        var resolved = LobbyMapper.Map(Json(json), null, null, new Dictionary<string, string> { ["id:1003"] = "Third#NA1" })!;

        Assert.Equal("OldName", unresolved.Members[2].RiotId);
        Assert.Equal("Third#NA1", resolved.Members[2].RiotId);
    }

    [Fact]
    public void Practice_tool_lobby_is_custom_with_its_game_mode()
    {
        var lobby = Assert.IsType<LobbyState>(LobbyMapper.Map(Json(GameStateSamples.PracticeToolLobby), null, null));

        Assert.True(lobby.IsCustom);
        Assert.Equal("PRACTICETOOL", lobby.GameMode);
        Assert.Equal(-1, lobby.QueueId);
        Assert.Equal("Me#EUW", lobby.Members.Single().RiotId);
    }

}
