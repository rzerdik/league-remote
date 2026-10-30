using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Pure mapping of the client's post-game data to <see cref="PostGameSummary"/>: the stats block
/// (<c>/lol-end-of-game/v1/eog-stats-block</c>, keys as rcp-fe-lol-postgame reads them in patch 16.21: <c>teams[].players[]</c>
/// with <c>stats.CHAMPIONS_KILLED</c>, <c>NUM_DEATHS</c>, <c>ASSISTS</c>, <c>MINIONS_KILLED</c>, <c>NEUTRAL_MINIONS_KILLED</c>,
/// <c>GOLD_EARNED</c>, <c>TOTAL_DAMAGE_DEALT_TO_CHAMPIONS</c>, <c>VISION_SCORE</c>, <c>items</c>, <c>spell1Id</c>…), the LP
/// change, the mastery update, the game flow session (queue) and player status (Play Again).
/// POLICY as in match details (<see cref="ProfileMapper.Details"/>): Riot IDs for the own team only, in Arena for the own
/// duo only (only the local player when the block doesn't say every player's duo); enemies by champion and stats; a puuid
/// only beside a teammate's name (<see cref="MatchParticipant.Puuid"/>, to open their profile), never the local player's;
/// summoner ids and profile icons are never copied.
/// </summary>
internal static class PostGameMapper
{
    /// <summary>Arena's map (Rings of Wrath); Arena has one team per duo (<c>stats.PLAYER_SUBTEAM</c>), placements 1–8.</summary>
    public const int ArenaMapId = 30;

    /// <summary>Longest name kept (Riot game names have at most 16 characters; this only bounds odd data).</summary>
    internal const int MaxNameLength = 32;

    private const int ItemSlots = 7;

    /// <summary>
    /// The summary, or null when the stats block isn't there (404 before the post-game screen) or lists no players.
    /// <paramref name="lpChange"/> and <paramref name="masteryUpdate"/> are used only when they are about this game. When
    /// the local player isn't found in the block, <see cref="PostGameSummary.Result"/> falls back to a loss and no team is
    /// yours (the phone then titles the screen neutrally).
    /// </summary>
    public static PostGameSummary? Map(
        JsonElement? stats, JsonElement? lpChange = null, JsonElement? masteryUpdate = null, JsonElement? session = null,
        JsonElement? playerStatus = null)
    {
        if (stats is not { ValueKind: JsonValueKind.Object } eog)
            return null;

        var players = eog.Arr("teams")
            .Where(t => t.ValueKind == JsonValueKind.Object)
            .SelectMany(t => t.Arr("players").Where(p => p.ValueKind == JsonValueKind.Object).Select(p => (Team: t, Player: p)))
            .Take(PostGameSummary.MaxPlayers)
            .ToList();
        if (players.Count == 0)
            return null;

        var gameId = eog.Long("gameId");
        var queue = Queue(session);
        var gameMode = eog.Str("gameMode") is { Length: > 0 } mode ? mode : queue?.Str("gameMode") ?? "";
        var myPuuid = eog.Obj("localPlayer")?.Str("puuid");
        bool IsMe(JsonElement p) =>
            p.Bool("isLocalPlayer") || (!string.IsNullOrEmpty(myPuuid) && string.Equals(p.Str("puuid"), myPuuid, StringComparison.OrdinalIgnoreCase));

        // Arena however it shows: by mode, by map, or by players carrying a duo. Privacy depends on it (the LCU puts four
        // duos on each of its two teams), so any sign is enough; ArenaDuos fails closed.
        var mapId = queue?.Int("mapId") ?? 0;
        var arena = gameMode == LeagueRanks.ArenaGameMode || mapId == ArenaMapId || players.Any(x => Stat(x.Player, "PLAYER_SUBTEAM") > 0);
        var teams = WithoutStrayPuuids(arena ? ArenaDuos(players, IsMe) : Teams(players, IsMe), myPuuid);
        var me = teams.SelectMany(t => t.Participants).FirstOrDefault(p => p.IsMe);
        var myTeam = teams.FirstOrDefault(t => t.IsMyTeam);
        var ranked = eog.Bool("ranked");

        var result = eog.Bool("gameEndedInEarlySurrender") ? MatchResults.Remake
            : myTeam is not null ? (myTeam.Win ? MatchResults.Win : MatchResults.Loss)
            : me is { Placement: > 0 } ? (me.Placement <= 4 ? MatchResults.Win : MatchResults.Loss)
            : MatchResults.Loss;

        var lastQueued = playerStatus?.Obj("lastQueuedLobbyStatus");

        return new PostGameSummary(
            gameId,
            queue?.Int("id") is > 0 and var queueId ? queueId : lastQueued?.Int("queueId") is > 0 and var lastId ? lastId : 0,
            gameMode,
            mapId == 0 && arena ? ArenaMapId : mapId,
            Math.Max(0, eog.Int("gameLength")),
            result,
            ranked,
            Rank(lpChange, gameId, ranked),
            Mastery(masteryUpdate, gameId, me?.ChampionId ?? 0),
            teams,
            CanPlayAgain(playerStatus));
    }

    /// <summary>
    /// Teammates' puuids only when the block says which line is the local player's (without that nobody is known to be a
    /// teammate, so none is published), and never the local player's own puuid.
    /// </summary>
    private static List<PostGameTeam> WithoutStrayPuuids(List<PostGameTeam> teams, string? myPuuid)
    {
        var meFound = teams.Any(t => t.Participants.Any(p => p.IsMe));
        bool Keep(MatchParticipant p) =>
            meFound && p.Puuid.Length > 0 && !(myPuuid is { Length: > 0 } && string.Equals(p.Puuid, myPuuid, StringComparison.OrdinalIgnoreCase));
        return [.. teams.Select(t => t with { Participants = [.. t.Participants.Select(p => Keep(p) ? p : p with { Puuid = "" })] })];
    }

    /// <summary>The client's own Play Again check: <c>lastQueuedLobbyStatus.allowedPlayAgain</c> of the player status.</summary>
    public static bool CanPlayAgain(JsonElement? playerStatus) =>
        playerStatus?.Obj("lastQueuedLobbyStatus")?.Bool("allowedPlayAgain") == true;

    /// <summary>
    /// The LP change when it belongs to this game: the notification names this game, or names none while the stats block
    /// says the game was ranked. Null for anything else (a notification left over from an earlier game).
    /// </summary>
    internal static PostGameRank? Rank(JsonElement? notification, long gameId, bool ranked)
    {
        if (notification is not { ValueKind: JsonValueKind.Object } n || string.IsNullOrEmpty(n.Str("queueType")))
            return null;
        var notificationGame = n.Long("gameId");
        if (notificationGame != 0 ? notificationGame != gameId : !ranked)
            return null;

        var tier = LeagueRanks.Tier(n.Str("tier"));
        return new PostGameRank(
            n.Str("queueType")!,
            tier,
            LeagueRanks.Division(tier, n.Str("division")),
            tier == "" ? 0 : n.Int("leaguePoints"),
            n.Int("leaguePointsDelta"),
            Math.Max(0, n.Int("provisionalGamesRemaining")),
            n.Str("notifyReason") switch
            {
                "LEAGUE_PROMOTED" => PostGameRankChanges.Promoted,
                "LEAGUE_DEMOTED" => PostGameRankChanges.Demoted,
                _ => PostGameRankChanges.None,
            });
    }

    /// <summary>
    /// The mastery update when it is about this game (or names no game) and the local player's champion (when known): a
    /// leftover update of an earlier game never shows another champion's points.
    /// </summary>
    internal static PostGameMastery? Mastery(JsonElement? update, long gameId, int myChampionId = 0)
    {
        if (update is not { ValueKind: JsonValueKind.Object } u || u.Int("championId") <= 0)
            return null;
        if (myChampionId > 0 && u.Int("championId") != myChampionId)
            return null;
        var updateGame = u.Long("gameId");
        if (updateGame != 0 && updateGame != gameId)
            return null;

        return new PostGameMastery(
            u.Int("championId"),
            Clamp(u.Long("pointsGained")),
            Clamp(u.Long("level")),
            u.Bool("hasLeveledUp"),
            u.Str("grade") ?? "",
            Clamp(u.Long("pointsUntilNextLevelAfterGame")));
    }

    /// <summary>
    /// The block's teams, the local player's first (then by team id); names on the own team only. Players without a team
    /// id (0) are never anyone's team: there only the local player is named, so a block without team ids can't name enemies.
    /// </summary>
    private static List<PostGameTeam> Teams(List<(JsonElement Team, JsonElement Player)> players, Func<JsonElement, bool> isMe)
    {
        var myTeamId = players.Where(x => isMe(x.Player)).Select(x => TeamId(x.Team, x.Player)).FirstOrDefault(-1);
        return players
            .GroupBy(x => TeamId(x.Team, x.Player))
            .Select(g =>
            {
                var team = g.First().Team;
                var mine = g.Key != 0 && (g.Key == myTeamId || (myTeamId == -1 && team.Bool("isPlayerTeam")));
                return MakeTeam(g.Key, mine, team.Bool("isWinningTeam"),
                    [.. g.Select(x => Participant(x.Player, isMe(x.Player), named: mine || isMe(x.Player)))]);
            })
            .OrderByDescending(t => t.IsMyTeam)
            .ThenBy(t => t.TeamId)
            .Take(PostGameSummary.MaxTeams)
            .ToList();
    }

    /// <summary>
    /// Arena: one team per duo (<c>stats.PLAYER_SUBTEAM</c>), the own duo first, then by placement. When a player has no
    /// duo, the block's teams instead, with none marked as yours and only the local player named.
    /// </summary>
    private static List<PostGameTeam> ArenaDuos(List<(JsonElement Team, JsonElement Player)> players, Func<JsonElement, bool> isMe)
    {
        if (players.Any(x => Stat(x.Player, "PLAYER_SUBTEAM") <= 0))
        {
            return
            [
                .. players
                    .GroupBy(x => TeamId(x.Team, x.Player))
                    .OrderBy(g => g.Key)
                    .Take(PostGameSummary.MaxTeams)
                    .Select(g => MakeTeam(g.Key, false, g.First().Team.Bool("isWinningTeam"),
                        [.. g.Select(x => Participant(x.Player, isMe(x.Player), named: isMe(x.Player)))])),
            ];
        }

        var mySubteam = players.Where(x => isMe(x.Player)).Select(x => Stat(x.Player, "PLAYER_SUBTEAM")).FirstOrDefault();
        return players
            .GroupBy(x => Stat(x.Player, "PLAYER_SUBTEAM"))
            .Select(g =>
            {
                var mine = g.Key == mySubteam;
                var list = g.Select(x => Participant(x.Player, isMe(x.Player), named: mine)).ToList();
                var placement = list.Select(p => p.Placement).FirstOrDefault(p => p > 0);
                return (Placement: placement, Team: MakeTeam(g.Key, mine, placement is > 0 and <= 4, list));
            })
            .OrderByDescending(x => x.Team.IsMyTeam)
            .ThenBy(x => x.Placement is > 0 and var p ? p : int.MaxValue)
            .ThenBy(x => x.Team.TeamId)
            .Take(PostGameSummary.MaxTeams)
            .Select(x => x.Team)
            .ToList();
    }

    private static PostGameTeam MakeTeam(int teamId, bool mine, bool win, IReadOnlyList<MatchParticipant> players) => new(
        teamId, mine, win, players.Sum(p => p.Kills), players.Sum(p => p.Deaths), players.Sum(p => p.Assists),
        players.Sum(p => p.Gold), players);

    /// <summary>The team's <c>teamId</c>, else the player's.</summary>
    private static int TeamId(JsonElement team, JsonElement player) =>
        team.Int("teamId") is not 0 and var id ? id : player.Int("teamId");

    private static MatchParticipant Participant(JsonElement p, bool isMe, bool named)
    {
        int S(string name) => Stat(p, name);
        var items = p.Arr("items").Where(i => i.ValueKind == JsonValueKind.Number && i.TryGetInt32(out _)).Select(i => Math.Max(0, i.GetInt32())).ToList();
        if (items.Count == 0)
            items = [.. Enumerable.Range(0, ItemSlots).Select(i => Math.Max(0, S($"ITEM{i}")))];
        var name = named ? Text.Clip(p.Str("riotIdGameName") is { Length: > 0 } gameName ? gameName : p.Str("summonerName"), MaxNameLength) : "";
        return new MatchParticipant(
            ChampionId: p.Int("championId"),
            IsMe: isMe,
            GameName: name,
            TagLine: named ? Text.Clip(p.Str("riotIdTagLine"), MaxNameLength) : "",
            ChampLevel: S("LEVEL") is > 0 and var level ? level : Math.Max(0, p.Int("level")),
            Kills: S("CHAMPIONS_KILLED"),
            Deaths: S("NUM_DEATHS"),
            Assists: S("ASSISTS"),
            Cs: S("MINIONS_KILLED") + S("NEUTRAL_MINIONS_KILLED"),
            Gold: S("GOLD_EARNED"),
            Damage: S("TOTAL_DAMAGE_DEALT_TO_CHAMPIONS"),
            VisionScore: S("VISION_SCORE"),
            Items: [.. items.Concat(Enumerable.Repeat(0, ItemSlots)).Take(ItemSlots)],
            Spell1Id: p.Int("spell1Id"),
            Spell2Id: p.Int("spell2Id"),
            // Rune keys as the older stats block named them; not seen in the 16.21 front-end, so often 0.
            Keystone: S("PERK0"),
            PrimaryStyle: S("PERK_PRIMARY_STYLE"),
            SubStyle: S("PERK_SUB_STYLE"),
            Placement: Math.Max(0, S("PLAYER_SUBTEAM_PLACEMENT")),
            Puuid: !isMe && name.Length > 0 ? ProfileMapper.TeammatePuuid(p.Str("puuid")) : "");
    }

    /// <summary>A whole-number stat of <c>stats</c> (the block sends them as numbers); 0 when missing or negative.</summary>
    private static int Stat(JsonElement player, string name) => Math.Max(0, player.Obj("stats")?.IntLenient(name) ?? 0);

    private static JsonElement? Queue(JsonElement? session) => session?.Obj("gameData")?.Obj("queue");

    private static int Clamp(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}
