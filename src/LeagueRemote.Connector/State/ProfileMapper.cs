using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Pure mapping from the profile endpoints (current summoner, ranked stats, champion mastery, match history, one full game)
/// to <see cref="ProfileState"/>, verified read-only against a live client (Oct 2026, patch 16.21; samples in
/// <c>ProfileSamples</c>). Privacy: everything is about the local player — or, for a <see cref="PlayerProfile"/>, about a
/// player the executor checked; in a full game only the own team keeps its Riot IDs, enemies' names are dropped, and a
/// puuid leaves this mapper only beside a name (<see cref="MatchParticipant.Puuid"/>: a named teammate the phone may open;
/// a player profile also carries the puuid it was asked for). Never a summoner id.
/// </summary>
internal static class ProfileMapper
{
    /// <summary>Summoner fields as the phone shows them; null when there is no logged-in summoner (404) yet.</summary>
    public static ProfileSummoner? Summoner(JsonElement? summoner) =>
        summoner is { ValueKind: JsonValueKind.Object } s
            ? new ProfileSummoner(
                s.GameNameOrName(),
                s.Str("tagLine") ?? "",
                s.Int("summonerLevel"),
                s.Int("xpSinceLastLevel"),
                Math.Max(0, s.Int("xpUntilNextLevel")),
                s.Int("profileIconId"))
            : null;

    /// <summary>
    /// The puuid a Riot ID lookup found (<c>/lol-summoner/v1/alias/lookup</c>); null when nobody has that Riot ID (League
    /// answers an empty puuid) or the answer isn't a usable puuid.
    /// </summary>
    public static string? LookedUpPuuid(JsonElement? lookup) =>
        lookup is { ValueKind: JsonValueKind.Object } l && RiotIds.IsPuuid(l.Str("puuid")) ? l.Str("puuid") : null;

    /// <summary>The local player's puuid (only used to find them in games; never published).</summary>
    public static string? MyPuuid(JsonElement? summoner) =>
        summoner is { ValueKind: JsonValueKind.Object } s && s.Str("puuid") is { Length: > 0 } puuid ? puuid : null;

    /// <summary>
    /// Solo/Duo, then Flex (always, unranked when the client has no entry), then other League queues with a tier in the
    /// client's order. TFT queues are left out. Empty when the ranked service didn't answer.
    /// </summary>
    public static IReadOnlyList<RankedQueueStats> Ranks(JsonElement? rankedStats)
    {
        if (rankedStats is not { ValueKind: JsonValueKind.Object } stats)
            return [];

        var queues = stats.Arr("queues")
            .Where(q => q.ValueKind == JsonValueKind.Object && !string.IsNullOrEmpty(q.Str("queueType")))
            .Where(q => !q.Str("queueType")!.Contains(RankedQueueTypes.TftMarker, StringComparison.OrdinalIgnoreCase))
            .Select(MapQueue)
            .DistinctBy(q => q.QueueType)
            .ToList();

        RankedQueueStats Fixed(string type) => queues.FirstOrDefault(q => q.QueueType == type) ?? Unranked(type);
        return
        [
            Fixed(RankedQueueTypes.Solo),
            Fixed(RankedQueueTypes.Flex),
            .. queues.Where(q => q.QueueType is not (RankedQueueTypes.Solo or RankedQueueTypes.Flex) && q.Tier != ""),
        ];
    }

    /// <summary>Score from the bare number, champion count from the list, top champions by points.</summary>
    public static ProfileMastery Mastery(JsonElement? masteryList, JsonElement? masteryScore)
    {
        var all = masteryList.Objects().Where(m => m.Int("championId") > 0).ToList();
        var top = all
            .Select(m => new ChampionMastery(
                m.Int("championId"),
                m.Int("championLevel"),
                m.Int("championPoints"),
                m.Int("championPointsSinceLastLevel"),
                Math.Max(0, m.Int("championPointsUntilNextLevel")),
                m.Str("highestGrade") ?? "",
                UnixTime.FromMs(m.Long("lastPlayTime"))))
            .OrderByDescending(m => m.Points)
            .ThenBy(m => m.ChampionId)
            .Take(ProfileState.MaxTopChampions)
            .ToList();
        var score = masteryScore is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var s) ? s : 0;
        return new ProfileMastery(score, all.Count, top);
    }

    /// <summary>
    /// Another player's mastery (<c>/lol-champion-mastery/v1/{puuid}/champion-mastery</c>, the same entries as the local
    /// list). There is no score endpoint for other players; the score is what the client's is: the sum of champion levels.
    /// </summary>
    public static ProfileMastery PlayerMastery(JsonElement? masteryList) =>
        Mastery(masteryList, null) with
        {
            Score = masteryList.Objects().Where(m => m.Int("championId") > 0).Sum(m => Math.Max(0, m.Int("championLevel"))),
        };

    /// <summary>
    /// The local player's games (or, with another player's puuid, theirs: the by-puuid list has the same shape), newest
    /// first, at most <see cref="ProfileState.MaxMatches"/>. Each list entry carries only that player, found by
    /// <paramref name="myPuuid"/> (or the entry's only participant when the puuid is unknown).
    /// </summary>
    public static IReadOnlyList<MatchSummary> Matches(JsonElement? matchHistory, string? myPuuid)
    {
        if (matchHistory?.Obj("games") is not { } games)
            return [];

        return
        [
            .. games.Arr("games")
                .Where(g => g.ValueKind == JsonValueKind.Object && g.Long("gameId") > 0)
                .Select(g => Summary(g, myPuuid))
                .OfType<MatchSummary>()
                .DistinctBy(m => m.GameId)
                .OrderByDescending(m => m.StartedAt)
                .Take(ProfileState.MaxMatches),
        ];
    }

    /// <summary>
    /// A full game: the local player's team first, then the others by team id (Arena: by placement). Null when the client
    /// didn't send a game. Names only for the local player's team; enemies keep champion and stats only.
    /// </summary>
    public static MatchDetails? Details(JsonElement? game, string? myPuuid)
    {
        if (game is not { ValueKind: JsonValueKind.Object } g || g.Long("gameId") <= 0)
            return null;

        var identities = Identities(g);
        var participants = g.Arr("participants")
            .Where(p => p.ValueKind == JsonValueKind.Object)
            .OrderBy(p => p.Int("participantId"))
            .ToList();
        bool Me(JsonElement p) => IsMe(identities, p, myPuuid);

        if (g.Str("gameMode") != LeagueRanks.ArenaGameMode)
            return new MatchDetails(g.Long("gameId"), SidedTeams(g, participants, identities, Me, onlyMeNamed: false));

        // Arena: the LCU puts four duos on each of its two teams; the own team is the duo (stats.playerSubteamId). Without
        // every player's duo known it can't tell allies from enemies, so it fails closed: the LCU teams, only the local
        // player named, no team marked as the own one.
        return participants.All(p => StatInt(p, "playerSubteamId") > 0)
            ? new MatchDetails(g.Long("gameId"), ArenaTeams(participants, identities, Me))
            : new MatchDetails(g.Long("gameId"), SidedTeams(g, participants, identities, Me, onlyMeNamed: true));
    }

    /// <summary>
    /// Another player's game from their side: their team first and named. If the local player was on the other side, or
    /// their puuid is unknown, only the player keeps a name (their teammates were the local player's enemies). The local
    /// player's own line never carries a puuid.
    /// </summary>
    public static MatchDetails? PlayerDetails(JsonElement? game, string playerPuuid, string? localPuuid)
    {
        if (Details(game, playerPuuid) is not { } raw || game is not { } g)
            return null;
        if (localPuuid is not null && string.Equals(localPuuid, playerPuuid, StringComparison.OrdinalIgnoreCase))
            return raw;
        var details = localPuuid is null ? raw : raw with
        {
            Teams =
            [
                .. raw.Teams.Select(t => t with
                {
                    Participants =
                    [
                        .. t.Participants.Select(p =>
                            string.Equals(p.Puuid, localPuuid, StringComparison.OrdinalIgnoreCase) ? p with { Puuid = "" } : p),
                    ],
                }),
            ],
        };

        // Without the local player's puuid we can't tell whether they played against these names: fail closed.
        if (localPuuid is not null)
        {
            var identities = Identities(g);
            var participants = g.Arr("participants").Where(p => p.ValueKind == JsonValueKind.Object).ToList();
            var player = participants.FirstOrDefault(p => IsMe(identities, p, playerPuuid));
            var local = participants.FirstOrDefault(p => IsMe(identities, p, localPuuid));
            if (local.ValueKind != JsonValueKind.Object)
                return details; // the local player wasn't in it: nobody here is their enemy

            var arena = g.Str("gameMode") == LeagueRanks.ArenaGameMode;
            int Side(JsonElement p) => arena ? StatInt(p, "playerSubteamId") : p.Int("teamId");
            if (player.ValueKind == JsonValueKind.Object && Side(player) > 0 && Side(player) == Side(local))
                return details; // the same team: the usual own-team names
        }

        return details with
        {
            Teams =
            [
                .. details.Teams.Select(t => t with
                {
                    Participants = [.. t.Participants.Select(p => p.IsMe ? p : p with { GameName = "", TagLine = "", Puuid = "" })],
                }),
            ],
        };
    }

    /// <summary>
    /// One <see cref="MatchTeam"/> per Arena duo, <see cref="MatchTeam.TeamId"/> = <c>playerSubteamId</c>: the own duo
    /// first, then by placement (<c>subteamPlacement</c>). <see cref="MatchTeam.Win"/> as the LCU says per player
    /// (<c>stats.win</c>: a top-half placement); no objectives or bans (the LCU keeps those per side, not per duo).
    /// Not verified live: no Arena game recorded yet.
    /// </summary>
    private static List<MatchTeam> ArenaTeams(List<JsonElement> participants, Dictionary<int, JsonElement> identities, Func<JsonElement, bool> isMe)
    {
        var mySubteam = participants.Where(isMe).Select(p => StatInt(p, "playerSubteamId")).FirstOrDefault();
        return
        [
            .. participants
                .GroupBy(p => StatInt(p, "playerSubteamId"))
                .Select(duo =>
                {
                    var mine = duo.Key == mySubteam;
                    var members = duo.Select(p => Participant(p, isMe(p), mine ? Identity(identities, p) : null)).ToList();
                    return new MatchTeam(
                        duo.Key, mine, duo.Any(p => Stats(p) is { ValueKind: JsonValueKind.Object } s && s.Bool("win")),
                        members.Sum(m => m.Kills), members.Sum(m => m.Gold), 0, 0, 0, 0, 0, 0, [], members);
                })
                .OrderByDescending(t => t.IsMyTeam)
                .ThenBy(t => t.Participants.Select(p => p.Placement).FirstOrDefault(p => p > 0) is > 0 and var placement ? placement : int.MaxValue)
                .ThenBy(t => t.TeamId),
        ];
    }

    /// <summary>
    /// The LCU's teams (100 blue, 200 red) with their objectives and bans. <paramref name="onlyMeNamed"/>: the own team is
    /// unknown (Arena without duo ids), so only the local player keeps a name and no team is marked as theirs.
    /// </summary>
    private static List<MatchTeam> SidedTeams(
        JsonElement g, List<JsonElement> participants, Dictionary<int, JsonElement> identities, Func<JsonElement, bool> isMe, bool onlyMeNamed)
    {
        var me = participants.FirstOrDefault(isMe);
        var myTeamId = me.ValueKind == JsonValueKind.Object && !onlyMeNamed ? me.Int("teamId") : (int?)null;

        bool Named(JsonElement p) => onlyMeNamed ? isMe(p) : p.Int("teamId") == myTeamId;

        var teamJson = g.Arr("teams").Where(t => t.ValueKind == JsonValueKind.Object).ToList();
        var teamIds = teamJson.Select(t => t.Int("teamId")).Concat(participants.Select(p => p.Int("teamId"))).Distinct();

        return
        [
            .. teamIds.Select(teamId =>
            {
                var team = teamJson.FirstOrDefault(t => t.Int("teamId") == teamId);
                var hasTeam = team.ValueKind == JsonValueKind.Object;
                var members = participants
                    .Where(p => p.Int("teamId") == teamId)
                    .Select(p => Participant(p, isMe(p), Named(p) ? Identity(identities, p) : null))
                    .ToList();
                return new MatchTeam(
                    teamId,
                    IsMyTeam: teamId == myTeamId,
                    Win: hasTeam && team.Str("win") == "Win",
                    Kills: members.Sum(m => m.Kills),
                    Gold: members.Sum(m => m.Gold),
                    Towers: hasTeam ? team.Int("towerKills") : 0,
                    Inhibitors: hasTeam ? team.Int("inhibitorKills") : 0,
                    Dragons: hasTeam ? team.Int("dragonKills") : 0,
                    Barons: hasTeam ? team.Int("baronKills") : 0,
                    Heralds: hasTeam ? team.Int("riftHeraldKills") : 0,
                    Grubs: hasTeam ? team.Int("hordeKills") : 0,
                    Bans: hasTeam ? Bans(team) : [],
                    Participants: members);
            })
            .OrderByDescending(t => t.IsMyTeam || t.Participants.Any(p => p.IsMe))
            .ThenBy(t => t.TeamId),
        ];
    }

    /// <summary>Whether <paramref name="gameId"/> is one of the profile's matches (what LoadMatchDetails may load).</summary>
    public static bool HasMatch(ProfileState profile, long gameId) => profile.Matches.Any(m => m.GameId == gameId);

    /// <summary>Whether <paramref name="gameId"/> is one of that player's matches (LoadMatchDetails with a puuid).</summary>
    public static bool HasMatch(PlayerProfile player, long gameId) => player.Matches.Any(m => m.GameId == gameId);

    /// <summary>
    /// Whether one of <paramref name="lines"/> names the player with <paramref name="puuid"/>: a puuid is only ever published
    /// beside a teammate's name (<see cref="MatchParticipant.Puuid"/>), so this is "the connector showed them as a teammate".
    /// </summary>
    public static bool Names(IEnumerable<MatchParticipant> lines, string puuid) =>
        lines.Any(p => p.Puuid.Length > 0 && p.GameName.Length > 0 && string.Equals(p.Puuid, puuid, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <see cref="MatchResults"/>: aborted (<c>endOfGameResult</c> "Abort_…"), remake (early surrender), win, loss.
    /// </summary>
    internal static string Result(string? endOfGameResult, JsonElement stats) =>
        endOfGameResult is not null && endOfGameResult.StartsWith("Abort", StringComparison.OrdinalIgnoreCase) ? MatchResults.Aborted
        : stats.ValueKind == JsonValueKind.Object && stats.Bool("gameEndedInEarlySurrender") ? MatchResults.Remake
        : stats.ValueKind == JsonValueKind.Object && stats.Bool("win") ? MatchResults.Win
        : MatchResults.Loss;

    /// <summary>
    /// League's post-game role guess (<c>timeline.lane</c> + <c>role</c>) as a <see cref="Positions"/> value. A support who
    /// roamed comes as lane NONE + role SUPPORT (seen live, Oct 2026): still the support.
    /// </summary>
    internal static string Position(string? lane, string? role)
    {
        var support = role?.ToUpperInvariant() is "SUPPORT" or "DUO_SUPPORT";
        return lane?.ToUpperInvariant() switch
        {
            "TOP" => Positions.Top,
            "JUNGLE" => Positions.Jungle,
            "MIDDLE" or "MID" => Positions.Middle,
            "BOTTOM" or "BOT" when support => Positions.Utility,
            "BOTTOM" or "BOT" => Positions.Bottom,
            "NONE" or "" or null when support => Positions.Utility,
            _ => "",
        };
    }

    private static MatchSummary? Summary(JsonElement game, string? myPuuid)
    {
        var identities = Identities(game);
        var participants = game.Arr("participants").Where(p => p.ValueKind == JsonValueKind.Object).ToList();
        var me = participants.FirstOrDefault(p => IsMe(identities, p, myPuuid));
        if (me.ValueKind != JsonValueKind.Object)
        {
            // The list carries only the local player, so a single entry is them even when the puuid didn't match.
            if (participants.Count != 1)
                return null;
            me = participants[0];
        }

        var timeline = me.Obj("timeline");
        return new MatchSummary(
            game.Long("gameId"),
            Math.Max(0, game.Int("queueId")),
            game.Str("gameMode") ?? "",
            game.Str("gameType") ?? "",
            game.Int("mapId"),
            UnixTime.FromMs(game.Long("gameCreation")) ?? DateTimeOffset.UnixEpoch,
            game.Int("gameDuration"),
            Result(game.Str("endOfGameResult"), Stats(me)),
            timeline is { } t ? Position(t.Str("lane"), t.Str("role")) : "",
            Participant(me, isMe: true, Identity(identities, me)));
    }

    /// <param name="player">The line's identity when it may be named (own team), else null: no name, no puuid.</param>
    private static MatchParticipant Participant(JsonElement p, bool isMe, JsonElement? player)
    {
        int S(string name) => StatInt(p, name);
        var gameName = player is { } named ? named.GameNameOrName() : "";
        return new MatchParticipant(
            ChampionId: p.Int("championId"),
            IsMe: isMe,
            GameName: gameName,
            TagLine: player is { } tagged ? tagged.Str("tagLine") ?? "" : "",
            ChampLevel: S("champLevel"),
            Kills: S("kills"),
            Deaths: S("deaths"),
            Assists: S("assists"),
            Cs: S("totalMinionsKilled") + S("neutralMinionsKilled"),
            Gold: S("goldEarned"),
            Damage: S("totalDamageDealtToChampions"),
            VisionScore: S("visionScore"),
            Items: [.. Enumerable.Range(0, 7).Select(i => Math.Max(0, S($"item{i}")))],
            Spell1Id: p.Int("spell1Id"),
            Spell2Id: p.Int("spell2Id"),
            Keystone: S("perk0"),
            PrimaryStyle: S("perkPrimaryStyle"),
            SubStyle: S("perkSubStyle"),
            Placement: Math.Max(0, S("subteamPlacement")),
            Puuid: !isMe && gameName.Length > 0 ? TeammatePuuid(player?.Str("puuid")) : "");
    }

    /// <summary>A named teammate's puuid as published (<see cref="MatchParticipant.Puuid"/>); "" when it isn't a usable puuid.</summary>
    internal static string TeammatePuuid(string? puuid) => RiotIds.IsPuuid(puuid) ? puuid! : "";

    private static RankedQueueStats MapQueue(JsonElement q)
    {
        var tier = LeagueRanks.Tier(q.Str("tier"));
        var highestTier = LeagueRanks.Tier(q.Str("highestTier"));
        var previousTier = LeagueRanks.Tier(q.Str("previousSeasonEndTier"));
        return new RankedQueueStats(
            q.Str("queueType")!,
            tier,
            LeagueRanks.Division(tier, q.Str("division")),
            tier == "" ? 0 : q.Int("leaguePoints"),
            q.Int("wins"),
            q.Int("losses"),
            q.Bool("isProvisional"),
            Math.Max(0, q.Int("provisionalGamesRemaining")),
            q.Str("miniSeriesProgress") ?? "",
            highestTier,
            LeagueRanks.Division(highestTier, q.Str("highestDivision")),
            previousTier,
            LeagueRanks.Division(previousTier, q.Str("previousSeasonEndDivision")));
    }

    private static RankedQueueStats Unranked(string queueType) =>
        new(queueType, "", "", 0, 0, 0, false, 0, "", "", "", "", "");

    private static IReadOnlyList<int> Bans(JsonElement team) =>
    [
        .. team.Arr("bans")
            .Where(b => b.ValueKind == JsonValueKind.Object)
            .OrderBy(b => b.Int("pickTurn"))
            .Select(b => Math.Max(0, b.Int("championId"))),
    ];

    private static Dictionary<int, JsonElement> Identities(JsonElement game) =>
        game.Arr("participantIdentities")
            .Where(i => i.Obj("player") is not null)
            .DistinctBy(i => i.Int("participantId"))
            .ToDictionary(i => i.Int("participantId"), i => i.GetProperty("player"));

    private static JsonElement? Identity(Dictionary<int, JsonElement> identities, JsonElement participant) =>
        identities.TryGetValue(participant.Int("participantId"), out var player) ? player : null;

    private static bool IsMe(Dictionary<int, JsonElement> identities, JsonElement participant, string? myPuuid) =>
        myPuuid is not null
        && Identity(identities, participant) is { } player
        && string.Equals(player.Str("puuid"), myPuuid, StringComparison.OrdinalIgnoreCase);

    /// <summary>The participant's <c>stats</c> object; <c>default</c> (Undefined) when missing.</summary>
    private static JsonElement Stats(JsonElement participant) => participant.Obj("stats") ?? default;

    private static int StatInt(JsonElement participant, string name) => participant.Obj("stats")?.Int(name) ?? 0;
}
