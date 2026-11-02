using System.Text.Json;
using System.Text.RegularExpressions;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using static LeagueRemote.Connector.State.JsonElementExtensions;

namespace LeagueRemote.Connector.LiveClient;

/// <summary>
/// Pure mapping from the Live Client Data API's <c>GET /liveclientdata/allgamedata</c> to <see cref="LiveGameState"/>
/// (the contract's XML docs are the spec). Shapes follow Riot's documented samples (<c>LiveGameSamples</c>).
/// <para>
/// Privacy: enemies only by champion. Riot IDs are copied only for the local player's team, and only when the local player
/// was matched and that team has at most <see cref="LiveGameState.MaxTeamSize"/> players; otherwise only the local player
/// keeps a name. Events name players by game name; they are resolved to scoreboard lines here and leave as champions (plus
/// the game name for the own team only). Nothing here logs.
/// </para>
/// </summary>
internal static partial class LiveGameMapper
{
    private const string RawChampionPrefix = "game_character_displayname_";

    private static readonly HashSet<string> KnownPositions =
        [Positions.Top, Positions.Jungle, Positions.Middle, Positions.Bottom, Positions.Utility];

    /// <summary><c>gameData.gameTime</c>, or null when the answer has none (not a game yet).</summary>
    public static double? GameTime(JsonElement allGameData) =>
        allGameData.Obj("gameData") is { } gameData
        && gameData.TryGetProperty("gameTime", out var t)
        && t.ValueKind == JsonValueKind.Number
            ? t.GetDouble()
            : null;

    /// <param name="allGameData">The API's answer.</param>
    /// <param name="championIds">
    /// Champion id by internal name and display name (<see cref="ChampionCatalog.GetIdsByNameAsync"/>); null while the LCU's
    /// game data isn't available (ids are 0 then).
    /// </param>
    /// <param name="now">Becomes <see cref="LiveGameState.UpdatedAt"/>.</param>
    public static LiveGameState Map(JsonElement allGameData, IReadOnlyDictionary<string, int>? championIds, DateTimeOffset now)
    {
        if (allGameData.ValueKind != JsonValueKind.Object)
            return LiveGameState.Empty() with { UpdatedAt = now };

        var gameData = allGameData.Obj("gameData") ?? EmptyObject;
        var gameMode = gameData.Str("gameMode") ?? "";
        var mapNumber = gameData.IntLenient("mapNumber");
        var gameTime = gameData.Dbl("gameTime");

        var roster = Roster.Build(allGameData, championIds ?? new Dictionary<string, int>());
        var events = (allGameData.Obj("events") ?? EmptyObject).Arr("Events")
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .OrderBy(e => e.Dbl("EventTime")) // stable: FirstBlood / FirstBrick stay right after their event
            .ToList();

        var feed = events.Select(e => MapEvent(e, roster)).OfType<LiveEvent>().ToList();
        var teams = Teams(roster, events);
        var me = roster.MeIndex >= 0
            ? new LiveActivePlayer(roster.Lines[roster.MeIndex].Player, allGameData.Obj("activePlayer")?.Dbl("currentGold") ?? 0)
            : null;
        var timers = LiveGameRules.HasObjectiveTimers(gameMode, mapNumber) ? Timers(events, roster, gameTime) : null;
        var result = feed.LastOrDefault(e => e.Kind == LiveEventKinds.GameEnd)?.Result ?? "";

        return new LiveGameState(
            true,
            gameTime,
            gameMode,
            mapNumber,
            me,
            teams,
            timers,
            [.. feed.TakeLast(LiveGameState.MaxEvents)],
            result,
            now);
    }

    // --- Players --------------------------------------------------------------------------

    /// <summary>One <c>allPlayers</c> line with the names events use to refer to it.</summary>
    private sealed record Line(string Team, string GameName, string RiotId, string SummonerName, LivePlayer Player);

    /// <summary>The scoreboard plus who is the local player and who may be named.</summary>
    private sealed class Roster
    {
        public required IReadOnlyList<Line> Lines { get; init; }

        /// <summary>Index of the local player in <see cref="Lines"/>; -1 when unmatched.</summary>
        public required int MeIndex { get; init; }

        /// <summary>The local player's team; null when unmatched.</summary>
        public required string? MyTeam { get; init; }

        public static Roster Build(JsonElement root, IReadOnlyDictionary<string, int> championIds)
        {
            var raw = root.Arr("allPlayers").Where(p => p.ValueKind == JsonValueKind.Object).ToList();
            var meIndex = FindMe(root.Obj("activePlayer") ?? EmptyObject, raw);
            var myTeam = meIndex >= 0 ? raw[meIndex].Str("team") ?? "" : null;
            var nameTeam = myTeam is { Length: > 0 }
                           && raw.Count(p => (p.Str("team") ?? "") == myTeam) <= LiveGameState.MaxTeamSize;

            var lines = raw.Select((p, i) =>
            {
                var team = p.Str("team") ?? "";
                var named = i == meIndex || (nameTeam && team == myTeam);
                return new Line(team, p.Str("riotIdGameName") ?? "", p.Str("riotId") ?? "", p.Str("summonerName") ?? "",
                    MapPlayer(p, i == meIndex, named, championIds));
            }).ToList();
            return new Roster { Lines = lines, MeIndex = meIndex, MyTeam = myTeam };
        }

        /// <summary>
        /// The <c>allPlayers</c> line an event name refers to: by <c>riotIdGameName</c>, then <c>riotId</c>, then
        /// <c>summonerName</c>. A name two lines share is nobody (never guess who scored), and so is a structure or monster.
        /// </summary>
        public Line? Find(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var key in new Func<Line, string>[] { l => l.GameName, l => l.RiotId, l => l.SummonerName })
            {
                var matches = Lines.Where(l => string.Equals(key(l), name, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
                if (matches.Count > 0)
                    return matches.Count == 1 ? matches[0] : null;
            }

            return null;
        }

        public bool IsMine(string side) => side.Length > 0 && side == MyTeam;
    }

    /// <summary>The active player's line, by Riot ID, then game name + tag, then summoner name; -1 when none or ambiguous.</summary>
    private static int FindMe(JsonElement active, List<JsonElement> players)
    {
        var riotId = active.Str("riotId");
        var gameName = active.Str("riotIdGameName");
        var tagLine = active.Str("riotIdTagLine");
        var summonerName = active.Str("summonerName");
        var tests = new List<Func<JsonElement, bool>>();
        if (!string.IsNullOrEmpty(riotId))
            tests.Add(p => Same(p.Str("riotId"), riotId));
        if (!string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(tagLine))
            tests.Add(p => Same(p.Str("riotIdGameName"), gameName) && Same(p.Str("riotIdTagLine"), tagLine));
        if (!string.IsNullOrEmpty(summonerName))
            tests.Add(p => Same(p.Str("summonerName"), summonerName));

        foreach (var test in tests)
        {
            var matches = players.Select((p, i) => (p, i)).Where(x => test(x.p)).Select(x => x.i).Take(2).ToList();
            if (matches.Count > 0)
                return matches.Count == 1 ? matches[0] : -1;
        }

        return -1;

        static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static LivePlayer MapPlayer(JsonElement p, bool isMe, bool named, IReadOnlyDictionary<string, int> championIds)
    {
        var scores = p.Obj("scores") ?? EmptyObject;
        var spells = p.Obj("summonerSpells") ?? EmptyObject;
        var runes = p.Obj("runes") ?? EmptyObject;
        var position = p.Str("position") ?? "";
        var isDead = p.Bool("isDead");
        var (gameName, tagLine) = named ? RiotId(p) : ("", "");
        return new LivePlayer(
            ChampionId(p, championIds),
            p.Str("championName") ?? "",
            isMe,
            p.Bool("isBot"),
            gameName,
            tagLine,
            KnownPositions.Contains(position) ? position : "",
            p.IntLenient("level"),
            scores.IntLenient("kills"),
            scores.IntLenient("deaths"),
            scores.IntLenient("assists"),
            scores.IntLenient("creepScore"),
            scores.Dbl("wardScore"),
            [
                .. p.Arr("items").Where(i => i.ValueKind == JsonValueKind.Object && i.IntLenient("itemID") > 0)
                    .Select(i => new LiveItem(i.IntLenient("itemID"), i.IntLenient("slot"), Math.Max(1, i.IntLenient("count"))))
                    .OrderBy(i => i.Slot),
            ],
            Spell(spells.Obj("summonerSpellOne") ?? EmptyObject),
            Spell(spells.Obj("summonerSpellTwo") ?? EmptyObject),
            runes.Obj("keystone")?.IntLenient("id") ?? 0,
            runes.Obj("primaryRuneTree")?.IntLenient("id") ?? 0,
            runes.Obj("secondaryRuneTree")?.IntLenient("id") ?? 0,
            isDead,
            isDead ? Math.Max(0, p.Dbl("respawnTimer")) : 0);
    }

    /// <summary><c>riotIdGameName</c> / <c>riotIdTagLine</c>, else split from <c>riotId</c> or a "Name#TAG" <c>summonerName</c>.</summary>
    private static (string GameName, string TagLine) RiotId(JsonElement p)
    {
        if (p.Str("riotIdGameName") is { Length: > 0 } gameName)
            return (gameName, p.Str("riotIdTagLine") ?? "");
        var full = p.Str("riotId") is { Length: > 0 } riotId ? riotId : p.Str("summonerName") ?? "";
        var hash = full.LastIndexOf('#');
        return hash >= 0 ? (full[..hash], full[(hash + 1)..]) : (full, "");
    }

    /// <summary>The internal name from <c>rawChampionName</c> against the LCU aliases, else the display name; 0 when unknown.</summary>
    private static int ChampionId(JsonElement p, IReadOnlyDictionary<string, int> championIds)
    {
        var raw = p.Str("rawChampionName") ?? "";
        var alias = raw.StartsWith(RawChampionPrefix, StringComparison.OrdinalIgnoreCase) ? raw[RawChampionPrefix.Length..] : "";
        if (alias.Length > 0 && championIds.TryGetValue(alias, out var id))
            return id;
        return p.Str("championName") is { Length: > 0 } name && championIds.TryGetValue(name, out var byName) ? byName : 0;
    }

    private static LiveSummonerSpell Spell(JsonElement spell)
    {
        var match = SpellPattern().Match(spell.Str("rawDisplayName") ?? "");
        var name = spell.Str("displayName") ?? "";
        return match.Success || name.Length > 0 ? new LiveSummonerSpell(match.Success ? match.Groups[1].Value : "", name) : LiveSummonerSpell.None;
    }

    [GeneratedRegex(@"^GeneratedTip_SummonerSpell_(\w+)_DisplayName$")]
    private static partial Regex SpellPattern();

    // --- Teams ----------------------------------------------------------------------------

    /// <summary>Per-team objective counts while walking the event list.</summary>
    private sealed class Tally
    {
        public int Towers, Inhibitors, Elders, Barons, Heralds, Grubs;
        public readonly List<string> Dragons = [];
    }

    /// <summary>The local player's team first, then ORDER, CHAOS and anything else; counts from the whole event list.</summary>
    private static IReadOnlyList<LiveTeam> Teams(Roster roster, List<JsonElement> events)
    {
        var tallies = new Dictionary<string, Tally>();
        Tally For(string side) => tallies.TryGetValue(side, out var t) ? t : tallies[side] = new Tally();

        foreach (var e in events)
        {
            switch (e.Str("EventName"))
            {
                case "TurretKilled" when StructureOwner(e.Str("TurretKilled")) is { } owner && Opposite(owner) is { Length: > 0 } side:
                    For(side).Towers++;
                    break;
                case "InhibKilled" when StructureOwner(e.Str("InhibKilled")) is { } owner && Opposite(owner) is { Length: > 0 } side:
                    For(side).Inhibitors++;
                    break;
                case "DragonKill" when ObjectiveSide(e, roster) is { Length: > 0 } side:
                    if (IsElder(e.Str("DragonType")))
                        For(side).Elders++;
                    else
                        For(side).Dragons.Add(e.Str("DragonType") ?? "");
                    break;
                case "BaronKill" when ObjectiveSide(e, roster) is { Length: > 0 } side:
                    For(side).Barons++;
                    break;
                case "HeraldKill" when ObjectiveSide(e, roster) is { Length: > 0 } side:
                    For(side).Heralds++;
                    break;
                case "HordeKill" when ObjectiveSide(e, roster) is { Length: > 0 } side:
                    For(side).Grubs++;
                    break;
            }
        }

        return
        [
            .. roster.Lines.Select(l => l.Team).Distinct()
                .OrderBy(side => roster.IsMine(side) ? 0 : 1)
                .ThenBy(side => side switch { LiveTeams.Order => 0, LiveTeams.Chaos => 1, _ => 2 })
                .ThenBy(side => side, StringComparer.Ordinal)
                .Select(side =>
                {
                    var players = roster.Lines.Where(l => l.Team == side).Select(l => l.Player).ToList();
                    var t = tallies.GetValueOrDefault(side) ?? new Tally();
                    return new LiveTeam(side, roster.IsMine(side), players.Sum(p => p.Kills), t.Towers, t.Inhibitors,
                        [.. t.Dragons], t.Elders, t.Barons, t.Heralds, t.Grubs, players);
                }),
        ];
    }

    // --- Events ---------------------------------------------------------------------------

    /// <summary>One feed line, or null for events that aren't in the feed (GameStart, MinionsSpawning, unknown).</summary>
    private static LiveEvent? MapEvent(JsonElement e, Roster roster)
    {
        var killerName = e.Str("KillerName");
        var killer = roster.Find(killerName);
        switch (e.Str("EventName"))
        {
            case "ChampionKill":
            {
                var victim = roster.Find(e.Str("VictimName"));
                // A kill by a turret, minion or monster is the victim's enemies'.
                var side = killer?.Team ?? (victim is not null ? Opposite(victim.Team) : NameSide(killerName));
                return Event(e, roster, LiveEventKinds.Kill, side, killer, victim, assists: true);
            }
            case "FirstBlood":
            {
                var recipient = roster.Find(e.Str("Recipient"));
                return Event(e, roster, LiveEventKinds.FirstBlood, recipient?.Team ?? "", recipient);
            }
            case "Multikill":
                return Event(e, roster, LiveEventKinds.Multikill, killer?.Team ?? "", killer) with { KillStreak = e.IntLenient("KillStreak") };
            case "Ace":
            {
                var acer = roster.Find(e.Str("Acer"));
                var side = e.Str("AcingTeam") is { Length: > 0 } acing ? acing : acer?.Team ?? "";
                return Event(e, roster, LiveEventKinds.Ace, side, acer);
            }
            case "DragonKill":
            {
                var type = e.Str("DragonType") ?? "";
                return Event(e, roster, LiveEventKinds.Dragon, ObjectiveSide(e, roster), killer, assists: true) with
                {
                    DragonType = IsElder(type) ? LiveDragonTypes.Elder : type,
                    Stolen = e.BoolLenient("Stolen"),
                };
            }
            case "BaronKill":
                return Event(e, roster, LiveEventKinds.Baron, ObjectiveSide(e, roster), killer, assists: true) with { Stolen = e.BoolLenient("Stolen") };
            case "HeraldKill":
                return Event(e, roster, LiveEventKinds.Herald, ObjectiveSide(e, roster), killer, assists: true) with { Stolen = e.BoolLenient("Stolen") };
            case "HordeKill":
                return Event(e, roster, LiveEventKinds.Grub, ObjectiveSide(e, roster), killer, assists: true) with { Stolen = e.BoolLenient("Stolen") };
            case "TurretKilled":
                return Structure(e, roster, LiveEventKinds.Turret, e.Str("TurretKilled"), killer);
            case "InhibKilled":
                return Structure(e, roster, LiveEventKinds.Inhibitor, e.Str("InhibKilled"), killer);
            case "FirstBrick":
                return Event(e, roster, LiveEventKinds.FirstTower, killer?.Team ?? NameSide(killerName), killer);
            case "InhibRespawned":
            {
                var name = e.Str("InhibRespawned");
                return Event(e, roster, LiveEventKinds.InhibitorRespawned, StructureOwner(name) ?? "", null) with { Lane = StructureLane(name) };
            }
            case "GameEnd":
                return Event(e, roster, LiveEventKinds.GameEnd, "", null) with
                {
                    Result = e.Str("Result") switch
                    {
                        "Win" => MatchResults.Win,
                        "Lose" or "Loss" => MatchResults.Loss,
                        _ => "",
                    },
                };
            default:
                return null;
        }
    }

    /// <summary>A destroyed structure: scored by the opposite of its owner (also when minions took it).</summary>
    private static LiveEvent Structure(JsonElement e, Roster roster, string kind, string? structure, Line? killer)
    {
        var side = StructureOwner(structure) is { } owner ? Opposite(owner) : killer?.Team ?? NameSide(e.Str("KillerName"));
        return Event(e, roster, kind, side, killer, assists: true) with { Lane = StructureLane(structure) };
    }

    private static LiveEvent Event(JsonElement e, Roster roster, string kind, string side, Line? actor, Line? victim = null, bool assists = false) =>
        new(
            e.IntLenient("EventID"),
            e.Dbl("EventTime"),
            kind,
            side,
            roster.IsMine(side),
            actor?.Player.ChampionId ?? 0,
            actor?.Player.ChampionName ?? "",
            actor?.Player.GameName ?? "", // empty unless the actor may be named
            victim?.Player.ChampionId ?? 0,
            victim?.Player.ChampionName ?? "",
            victim?.Player.GameName ?? "",
            assists
                ? [.. e.Arr("Assisters").Where(a => a.ValueKind == JsonValueKind.String).Select(a => roster.Find(a.GetString()))
                    .OfType<Line>().Select(l => l.Player.ChampionId)]
                : [],
            0,
            "",
            false,
            "",
            "");

    /// <summary>The team that took an epic monster: the killer's, else from a non-champion killer's name, else the first resolvable assister's.</summary>
    private static string ObjectiveSide(JsonElement e, Roster roster)
    {
        var killerName = e.Str("KillerName");
        if (roster.Find(killerName) is { } killer)
            return killer.Team;
        if (NameSide(killerName) is { Length: > 0 } side)
            return side;
        return e.Arr("Assisters").Where(a => a.ValueKind == JsonValueKind.String)
            .Select(a => roster.Find(a.GetString())).OfType<Line>().FirstOrDefault()?.Team ?? "";
    }

    // --- Timers ---------------------------------------------------------------------------

    /// <summary>Next spawns per <see cref="LiveGameRules"/>, recomputed from the whole event list.</summary>
    private static LiveObjectiveTimers Timers(List<JsonElement> events, Roster roster, double gameTime)
    {
        double? lastDrake = null, soulAt = null, lastElder = null, lastBaron = null;
        var elementalTypes = new List<string>();
        var drakesByTeam = new Dictionary<string, int>();
        bool heraldTaken = false;
        var grubs = 0;
        var inhibitors = new Dictionary<string, LiveInhibitorTimer>();

        foreach (var e in events)
        {
            var time = e.Dbl("EventTime");
            switch (e.Str("EventName"))
            {
                case "DragonKill" when IsElder(e.Str("DragonType")):
                    lastElder = time;
                    break;
                case "DragonKill":
                    lastDrake = time;
                    elementalTypes.Add(e.Str("DragonType") ?? "");
                    if (ObjectiveSide(e, roster) is { Length: > 0 } side
                        && (drakesByTeam[side] = drakesByTeam.GetValueOrDefault(side) + 1) >= LiveGameRules.DragonSoulDrakes)
                        soulAt ??= time;
                    break;
                case "BaronKill":
                    lastBaron = time;
                    break;
                case "HeraldKill":
                    heraldTaken = true;
                    break;
                case "HordeKill":
                    grubs++;
                    break;
                case "InhibKilled" when e.Str("InhibKilled") is { Length: > 0 } inhib && StructureOwner(inhib) is { } owner:
                    inhibitors[inhib] = new LiveInhibitorTimer(owner, StructureLane(inhib), time + LiveGameRules.InhibitorRespawn);
                    break;
                case "InhibRespawned" when e.Str("InhibRespawned") is { Length: > 0 } inhib:
                    inhibitors.Remove(inhib);
                    break;
            }
        }

        var dragon = soulAt is { } soul
            ? new LiveObjectiveTimer(LiveObjectiveKinds.Elder, Math.Max(soul, lastElder ?? soul) + LiveGameRules.ElderSpawnDelay, null, LiveDragonTypes.Elder)
            : new LiveObjectiveTimer(
                LiveObjectiveKinds.Dragon,
                lastDrake is { } last ? last + LiveGameRules.DragonRespawn : LiveGameRules.DragonFirstSpawn,
                null,
                elementalTypes.Count >= 3 ? elementalTypes[2] : ""); // the third drake shows the Elemental Rift's element
        var baron = new LiveObjectiveTimer(
            LiveObjectiveKinds.Baron,
            lastBaron is { } b ? b + LiveGameRules.BaronRespawn : LiveGameRules.BaronSpawn,
            null,
            "");
        var herald = new LiveObjectiveTimer(
            LiveObjectiveKinds.Herald,
            heraldTaken || gameTime >= LiveGameRules.HeraldDespawn ? null : LiveGameRules.HeraldSpawn,
            LiveGameRules.HeraldDespawn,
            "");
        var grubCamp = new LiveObjectiveTimer(
            LiveObjectiveKinds.Grubs,
            grubs >= LiveGameRules.GrubsPerCamp || gameTime >= LiveGameRules.GrubsDespawn ? null : LiveGameRules.GrubsSpawn,
            LiveGameRules.GrubsDespawn,
            "");
        return new LiveObjectiveTimers(dragon, baron, herald, grubCamp,
            [.. inhibitors.Values.Where(i => i.RespawnsAt > gameTime).OrderBy(i => i.RespawnsAt)]);
    }

    // --- Names of structures and units ------------------------------------------------------

    [GeneratedRegex(@"^(?:Turret|Barracks)_T([12])_([LCR])")]
    private static partial Regex StructurePattern();

    [GeneratedRegex(@"^(?:Turret|Barracks|Minion)_T(100|200|1|2)")]
    private static partial Regex UnitSidePattern();

    /// <summary>The team whose base a structure is in (<c>Turret_T1_…</c> ORDER, <c>Barracks_T2_…</c> CHAOS); null when it doesn't parse.</summary>
    private static string? StructureOwner(string? name) =>
        StructurePattern().Match(name ?? "") is { Success: true } m ? m.Groups[1].Value == "1" ? LiveTeams.Order : LiveTeams.Chaos : null;

    private static string StructureLane(string? name) =>
        StructurePattern().Match(name ?? "") is { Success: true } m
            ? m.Groups[2].Value switch { "L" => LiveLanes.Top, "C" => LiveLanes.Mid, _ => LiveLanes.Bot }
            : "";

    /// <summary>The side of a non-champion killer from its name (turrets T1/T2, minions T100/T200); empty for monsters and players.</summary>
    private static string NameSide(string? name) =>
        UnitSidePattern().Match(name ?? "") is { Success: true } m
            ? m.Groups[1].Value is "1" or "100" ? LiveTeams.Order : LiveTeams.Chaos
            : "";

    private static string Opposite(string side) => side switch
    {
        LiveTeams.Order => LiveTeams.Chaos,
        LiveTeams.Chaos => LiveTeams.Order,
        _ => "",
    };

    private static bool IsElder(string? dragonType) => string.Equals(dragonType, LiveDragonTypes.Elder, StringComparison.OrdinalIgnoreCase);
}
