using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary>Keeps <see cref="LiveGameSamples"/> valid JSON and consistent with what its summaries promise the mapper tests.</summary>
public sealed class LiveGameSamplesTests
{
    [Fact]
    public void All_game_data_has_two_teams_of_five_and_the_local_player()
    {
        using var doc = JsonDocument.Parse(LiveGameSamples.AllGameData);
        var root = doc.RootElement;
        var players = root.GetProperty("allPlayers").EnumerateArray().ToList();

        Assert.Equal(5, players.Count(p => p.GetProperty("team").GetString() == LiveTeams.Order));
        Assert.Equal(5, players.Count(p => p.GetProperty("team").GetString() == LiveTeams.Chaos));
        var me = root.GetProperty("activePlayer").GetProperty("riotId").GetString();
        Assert.Single(players, p => p.GetProperty("riotId").GetString() == me);
        Assert.Equal(840.2, root.GetProperty("gameData").GetProperty("gameTime").GetDouble());
    }

    [Fact]
    public void Event_killers_and_victims_are_players_game_names_or_structures()
    {
        using var doc = JsonDocument.Parse(LiveGameSamples.AllGameDataLate);
        var root = doc.RootElement;
        var names = root.GetProperty("allPlayers").EnumerateArray().Select(p => p.GetProperty("riotIdGameName").GetString()).ToHashSet();
        var events = root.GetProperty("events").GetProperty("Events").EnumerateArray().ToList();

        Assert.Equal(1815.0, root.GetProperty("gameData").GetProperty("gameTime").GetDouble());
        Assert.Equal(Enumerable.Range(0, events.Count), events.Select(e => e.GetProperty("EventID").GetInt32()));
        foreach (var e in events)
        {
            foreach (var field in new[] { "KillerName", "VictimName", "Recipient", "Acer" })
            {
                if (e.TryGetProperty(field, out var name))
                    Assert.True(names.Contains(name.GetString()) || name.GetString()!.StartsWith("Turret_", StringComparison.Ordinal), name.GetString());
            }
        }
    }

    [Fact]
    public void Kill_events_match_the_scoreboard()
    {
        using var doc = JsonDocument.Parse(LiveGameSamples.AllGameData);
        var root = doc.RootElement;
        var kills = root.GetProperty("events").GetProperty("Events").EnumerateArray()
            .Where(e => e.GetProperty("EventName").GetString() == "ChampionKill").ToList();

        foreach (var p in root.GetProperty("allPlayers").EnumerateArray())
        {
            var name = p.GetProperty("riotIdGameName").GetString();
            var scores = p.GetProperty("scores");
            Assert.Equal(scores.GetProperty("kills").GetInt32(), kills.Count(k => k.GetProperty("KillerName").GetString() == name));
            Assert.Equal(scores.GetProperty("deaths").GetInt32(), kills.Count(k => k.GetProperty("VictimName").GetString() == name));
            Assert.Equal(scores.GetProperty("assists").GetInt32(),
                kills.Count(k => k.GetProperty("Assisters").EnumerateArray().Any(a => a.GetString() == name)));
        }
    }
}
