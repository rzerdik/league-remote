namespace LeagueRemote.Contracts;

/// <summary>
/// The local player's profile as the phone shows it on the Profile tab: Riot ID and level, ranks, champion mastery and
/// recent match history, plus the full details of the games the phone opened. Published as its own hub message
/// (<see cref="HubMethods.Client.ProfileUpdated"/>) and never pushed. About the local player, plus the profiles of the friends,
/// party members and champ-select teammates the phone opened (<see cref="Players"/>). Other players appear otherwise only
/// inside game details, and there only the own team carries names (never an enemy's name or puuid).
/// </summary>
/// <param name="Summoner">Null until the client has a logged-in summoner (or the read failed).</param>
/// <param name="Ranks">
/// League (not TFT) ranked queues from <c>GET /lol-ranked/v1/current-ranked-stats</c>: always
/// <see cref="RankedQueueTypes.Solo"/> then <see cref="RankedQueueTypes.Flex"/> (also when unranked, so the phone can show
/// "Unranked"), then any other non-TFT queue the client lists only while it has a tier, in the client's order. Empty when the
/// ranked service didn't answer.
/// </param>
/// <param name="Mastery">Champion mastery; <see cref="ProfileMastery.Empty"/> when the client didn't answer.</param>
/// <param name="Matches">The local player's last games, newest first, at most <see cref="MaxMatches"/>.</param>
/// <param name="Details">
/// Full games the phone asked for with <see cref="CommandType.LoadMatchDetails"/>, the most recently requested first, at
/// most <see cref="MaxDetails"/>. Only games that are in <see cref="Matches"/> (a game that drops out of the list is dropped
/// here too), so the phone takes the header (queue, time, duration, result) from the matching <see cref="MatchSummary"/>.
/// </param>
/// <param name="Players">
/// Other players' profiles the phone opened with <see cref="CommandType.LoadPlayerProfile"/> — only friends, party members
/// and the own team in champ select (re-checked by the connector on every read), teammates the connector named in a game
/// (<see cref="MatchParticipant.Puuid"/>) and players found with <see cref="CommandType.SearchPlayer"/> — the most recently
/// opened first, at most
/// <see cref="MaxPlayers"/>, dropped by the first profile read or Profile tab open after <see cref="PlayerLifetime"/>.
/// </param>
public sealed record ProfileState(
    ProfileSummoner? Summoner,
    IReadOnlyList<RankedQueueStats> Ranks,
    ProfileMastery Mastery,
    IReadOnlyList<MatchSummary> Matches,
    IReadOnlyList<MatchDetails> Details,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PlayerProfile> Players)
{
    public static ProfileState Empty() => new(null, [], ProfileMastery.Empty, [], [], DateTimeOffset.UtcNow, []);

    /// <summary>
    /// Games in <see cref="Matches"/>: what the client's own match history shows. The LCU's list ignores <c>begIndex</c> /
    /// <c>endIndex</c> (patch 16.21) and returns the newest 21; the connector keeps the first 20.
    /// </summary>
    public const int MaxMatches = 20;

    /// <summary>
    /// Full games kept in <see cref="Details"/>; loading another drops the one requested longest ago (it is one LCU read
    /// to open it again). Three, not more, to keep the message within <see cref="MaxJsonBytes"/>.
    /// </summary>
    public const int MaxDetails = 3;

    /// <summary>Champions in <see cref="ProfileMastery.TopChampions"/>.</summary>
    public const int MaxTopChampions = 10;

    /// <summary>
    /// <see cref="CommandType.RefreshProfile"/> rereads the LCU at most this often; sooner, the connector just republishes
    /// what it has (the phone sends it every time the Profile tab opens).
    /// </summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    /// <summary>Other players' profiles kept in <see cref="Players"/>; opening another drops the one opened longest ago.</summary>
    public const int MaxPlayers = 3;

    /// <summary>
    /// Full games kept per player (<see cref="PlayerProfile.Details"/>): the one open on the phone; opening another of
    /// theirs replaces it (one LCU read). Keeps the message within <see cref="MaxJsonBytes"/>.
    /// </summary>
    public const int MaxPlayerDetails = 1;

    /// <summary>
    /// How long another player's profile stays in <see cref="Players"/> after it was read (it goes with the next profile
    /// read or Profile tab open after that, and with a disconnect): a champ-select teammate is a stranger once the game
    /// is over, so their profile shouldn't linger in the connector's state. Opening it again rereads it.
    /// </summary>
    public static readonly TimeSpan PlayerLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Most JSON a whole profile message may take as SignalR writes it, so it stays a small download on a phone: every list
    /// above is capped, so even with every cached game an Arena game (16 lines), every line named with the longest
    /// escaped Riot ID and a 128-character puuid (the longest accepted) beside every name but the player's own, it stays
    /// below this (124,303 bytes measured); real ones are far smaller (enemy lines carry no names or puuids).
    /// </summary>
    public const int MaxJsonBytes = 128 * 1024;
}

/// <summary>
/// Another player's profile (<see cref="ProfileState.Players"/>), read by puuid like the client's own profile view of a
/// friend: <c>GET /lol-summoner/v2/summoners/puuid/{puuid}</c>, <c>/lol-ranked/v1/ranked-stats/{puuid}</c>,
/// <c>/lol-champion-mastery/v1/{puuid}/champion-mastery</c> and <c>/lol-match-history/v1/products/lol/{puuid}/matches</c>.
/// Per-champion, per-role and recent-form stats are left to the phone (computed from <see cref="Matches"/>).
/// </summary>
/// <param name="Puuid">
/// The player's puuid, as the command sent it (a friend's, a party member's or a teammate's) or as the Riot ID search
/// found it.
/// </param>
/// <param name="Summoner">Their Riot ID, level and icon (XP fields as the client sends them for anyone).</param>
/// <param name="Ranks">Same rules as <see cref="ProfileState.Ranks"/>.</param>
/// <param name="Mastery">
/// Same as <see cref="ProfileState.Mastery"/>; <see cref="ProfileMastery.Score"/> is the sum of their champion levels (what the
/// client's score is: there is no score endpoint for other players).
/// </param>
/// <param name="Matches">
/// Their last games, newest first, at most <see cref="ProfileState.MaxMatches"/> (the client answers the newest 20 and ignores
/// paging). <see cref="MatchSummary.Me"/> is this player's line (<see cref="MatchParticipant.IsMe"/> means "this player" here).
/// </param>
/// <param name="Details">
/// Their games the phone opened (<see cref="CommandType.LoadMatchDetails"/> with <see cref="RemoteCommand.Puuid"/>), most
/// recently requested first, at most <see cref="ProfileState.MaxPlayerDetails"/>, from this player's side:
/// <see cref="MatchTeam.IsMyTeam"/> / <see cref="MatchParticipant.IsMe"/> mark their team and them. Names follow the own rule
/// from their side (their team named, the other team never), and when the local player was in that game on the other team,
/// nobody but this player is named — so no enemy of the local player ever is.
/// </param>
/// <param name="LoadedAt">When the connector read it (see <see cref="ProfileState.PlayerLifetime"/>).</param>
public sealed record PlayerProfile(
    string Puuid,
    ProfileSummoner Summoner,
    IReadOnlyList<RankedQueueStats> Ranks,
    ProfileMastery Mastery,
    IReadOnlyList<MatchSummary> Matches,
    IReadOnlyList<MatchDetails> Details,
    DateTimeOffset LoadedAt);

/// <summary>From <c>GET /lol-summoner/v1/current-summoner</c>.</summary>
/// <param name="GameName">Riot ID game name.</param>
/// <param name="TagLine">Riot ID tag line, without '#'.</param>
/// <param name="Level">Summoner level (<c>summonerLevel</c>).</param>
/// <param name="XpSinceLastLevel">XP earned in the current level (<c>xpSinceLastLevel</c>).</param>
/// <param name="XpForNextLevel">
/// XP the current level needs in total (LCU <c>xpUntilNextLevel</c> — despite the name not what's left: the client's
/// <c>percentCompleteForNextLevel</c> is <see cref="XpSinceLastLevel"/> / this). 0 when unknown.
/// </param>
/// <param name="ProfileIconId">Community Dragon profile icon id.</param>
public sealed record ProfileSummoner(
    string GameName,
    string TagLine,
    int Level,
    int XpSinceLastLevel,
    int XpForNextLevel,
    int ProfileIconId);

/// <summary>Ranked queue types as the LCU names them (<c>queueType</c>).</summary>
public static class RankedQueueTypes
{
    /// <summary>Ranked Solo/Duo.</summary>
    public const string Solo = "RANKED_SOLO_5x5";

    /// <summary>Ranked Flex.</summary>
    public const string Flex = "RANKED_FLEX_SR";

    /// <summary>TFT queues (<c>RANKED_TFT</c>, <c>RANKED_TFT_DOUBLE_UP</c>, …) contain this and are left out.</summary>
    public const string TftMarker = "TFT";
}

/// <summary>One entry of <c>queues</c> in <c>GET /lol-ranked/v1/current-ranked-stats</c>.</summary>
/// <param name="QueueType">See <see cref="RankedQueueTypes"/>; other strings are passed through (the client also lists e.g.
/// <c>RANKED_PREMADE_5x5</c> and <c>JADE_RANKED_SOLO_5x5</c>, shown only while they have a tier).</param>
/// <param name="Tier">"IRON" … "CHALLENGER"; empty when unranked (LCU "" or "NONE").</param>
/// <param name="Division">"I"–"IV"; empty when unranked or for apex tiers (LCU sends "NA").</param>
/// <param name="LeaguePoints">LP in the current division.</param>
/// <param name="IsProvisional">Still in placement games.</param>
/// <param name="ProvisionalGamesRemaining">Placement games left; 0 when placed.</param>
/// <param name="Promos">
/// Series progress as the LCU sends it (<c>miniSeriesProgress</c>): one character per game, 'W' won, 'L' lost, 'N' not
/// played yet. Empty when not in a series (always, as of patch 16.21).
/// </param>
/// <param name="HighestTier">Highest tier reached this season in this queue; empty when none.</param>
/// <param name="HighestDivision">Its division; empty when none or apex.</param>
/// <param name="PreviousSeasonEndTier">Where the previous season ended in this queue; empty when unranked then.</param>
/// <param name="PreviousSeasonEndDivision">Its division; empty when none or apex.</param>
public sealed record RankedQueueStats(
    string QueueType,
    string Tier,
    string Division,
    int LeaguePoints,
    int Wins,
    int Losses,
    bool IsProvisional,
    int ProvisionalGamesRemaining,
    string Promos,
    string HighestTier,
    string HighestDivision,
    string PreviousSeasonEndTier,
    string PreviousSeasonEndDivision);

/// <summary>From <c>GET /lol-champion-mastery/v1/local-player/champion-mastery</c> (+ <c>…/champion-mastery-score</c>).</summary>
/// <param name="Score">Total mastery score (the client's number: the sum of all champion levels).</param>
/// <param name="ChampionCount">Champions with any mastery (the length of the client's list).</param>
/// <param name="TopChampions">By points, highest first, at most <see cref="ProfileState.MaxTopChampions"/>.</param>
public sealed record ProfileMastery(int Score, int ChampionCount, IReadOnlyList<ChampionMastery> TopChampions)
{
    public static readonly ProfileMastery Empty = new(0, 0, []);
}

/// <param name="Level">Mastery level (unbounded since the 2024 mastery rework).</param>
/// <param name="Points">Total mastery points.</param>
/// <param name="PointsSinceLastLevel">Points earned in the current level.</param>
/// <param name="PointsUntilNextLevel">Points still missing for the next level (here the LCU name is accurate).</param>
/// <param name="HighestGrade">Best grade this season ("S+", "A-", …); empty when none.</param>
/// <param name="LastPlayed">When the champion was last played; null when unknown.</param>
public sealed record ChampionMastery(
    int ChampionId,
    int Level,
    int Points,
    int PointsSinceLastLevel,
    int PointsUntilNextLevel,
    string HighestGrade,
    DateTimeOffset? LastPlayed);

/// <summary>How a game ended for the local player (<see cref="MatchSummary.Result"/>), checked in this order.</summary>
public static class MatchResults
{
    /// <summary>The game never really started (LCU <c>endOfGameResult</c> "Abort_…", e.g. a custom game with too few players).</summary>
    public const string Aborted = "aborted";

    /// <summary>Ended by an early surrender vote, a remake (<c>stats.gameEndedInEarlySurrender</c>). Doesn't count for LP.</summary>
    public const string Remake = "remake";

    public const string Win = "win";

    public const string Loss = "loss";
}

/// <summary>
/// One game of <c>GET /lol-match-history/v1/products/lol/current-summoner/matches</c>. The list carries only the local
/// player's participant, so that is all a summary has (like the client's match history list); both teams come with
/// <see cref="MatchDetails"/>.
/// </summary>
/// <param name="GameId">LCU game id; what <see cref="CommandType.LoadMatchDetails"/> takes.</param>
/// <param name="QueueId">LCU queue id (420 Solo/Duo, 440 Flex, 3140 Practice Tool, …); 0 when none.</param>
/// <param name="GameMode">LCU game mode ("CLASSIC", "ARAM", "CHERRY" (Arena), "PRACTICETOOL", …).</param>
/// <param name="GameType">"MATCHED_GAME" or "CUSTOM_GAME".</param>
/// <param name="MapId">11 Summoner's Rift, 12 Howling Abyss, 30 Arena, …</param>
/// <param name="StartedAt">When the game was created (<c>gameCreation</c>).</param>
/// <param name="DurationSeconds">Game length in seconds (<c>gameDuration</c>).</param>
/// <param name="Result">See <see cref="MatchResults"/>.</param>
/// <param name="Position">
/// The role League guessed after the game, as a <see cref="Positions"/> value (TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY), from
/// <c>timeline.lane</c> + <c>timeline.role</c> (BOTTOM + SUPPORT = UTILITY); empty when NONE. A heuristic: in a recorded
/// game it called the top laner JUNGLE, so show it as a hint at most.
/// </param>
/// <param name="Me">The local player's line (<see cref="MatchParticipant.IsMe"/> true, with their own Riot ID).</param>
public sealed record MatchSummary(
    long GameId,
    int QueueId,
    string GameMode,
    string GameType,
    int MapId,
    DateTimeOffset StartedAt,
    int DurationSeconds,
    string Result,
    string Position,
    MatchParticipant Me);

/// <summary>
/// A full game from <c>GET /lol-match-history/v1/games/{gameId}</c>, as the client's match details show it.
/// </summary>
/// <param name="GameId">See <see cref="MatchSummary.GameId"/>.</param>
/// <param name="Teams">
/// The local player's team first, then the others by LCU team id (100 blue, 200 red). Arena (game mode "CHERRY"): one team
/// per duo, the own duo first, then by placement. If the client doesn't say every player's duo (<c>playerSubteamId</c>),
/// Arena falls back to the two LCU teams with no team marked <see cref="MatchTeam.IsMyTeam"/> and only the local player named.
/// </param>
public sealed record MatchDetails(long GameId, IReadOnlyList<MatchTeam> Teams);

/// <summary>One team (<c>teams[]</c>) of a game, or one duo in Arena.</summary>
/// <param name="TeamId">100 (blue side) or 200 (red side); in Arena the duo's <c>playerSubteamId</c> (no sides).</param>
/// <param name="IsMyTeam">The local player played on it (in Arena: their duo).</param>
/// <param name="Win">
/// LCU <c>win</c> "Win" (both teams show "Fail" in aborted games). Arena duos: the players' <c>stats.win</c>, which the
/// LCU sets for a top-half placement; the duo's <see cref="MatchParticipant.Placement"/> is the finer result.
/// </param>
/// <param name="Kills">Sum of the participants' kills.</param>
/// <param name="Gold">Sum of the participants' gold earned.</param>
/// <param name="Towers">Turrets destroyed (<c>towerKills</c>).</param>
/// <param name="Inhibitors"><c>inhibitorKills</c>.</param>
/// <param name="Dragons"><c>dragonKills</c>.</param>
/// <param name="Barons"><c>baronKills</c>.</param>
/// <param name="Heralds">Rift Heralds (<c>riftHeraldKills</c>).</param>
/// <param name="Grubs">Voidgrubs (<c>hordeKills</c>).</param>
/// <param name="Bans">
/// Champions this team banned, in pick-turn order (0 = no ban in that turn); empty without bans. Arena duos have no
/// objectives or bans (the LCU keeps those per side): 0 and empty.
/// </param>
/// <param name="Participants">
/// In the game's order (participantId; in matchmade games that is top, jungle, mid, bottom, support).
/// </param>
public sealed record MatchTeam(
    int TeamId,
    bool IsMyTeam,
    bool Win,
    int Kills,
    int Gold,
    int Towers,
    int Inhibitors,
    int Dragons,
    int Barons,
    int Heralds,
    int Grubs,
    IReadOnlyList<int> Bans,
    IReadOnlyList<MatchParticipant> Participants);

/// <summary>
/// One player's line in a game (<c>participants[]</c> + <c>stats</c>). Privacy: <see cref="GameName"/> and
/// <see cref="TagLine"/> are set only for the local player's team and are always empty for enemies (the LCU sends them, the
/// connector drops them). <see cref="Puuid"/> only ever comes with a name, so the phone can open exactly the players it can
/// name; there is never a summoner id.
/// </summary>
/// <param name="ChampionId">Champion played.</param>
/// <param name="IsMe">The local player (matched by puuid with the current summoner).</param>
/// <param name="GameName">Riot ID game name; own team only, empty for enemies.</param>
/// <param name="TagLine">Riot ID tag line without '#'; own team only, empty for enemies.</param>
/// <param name="ChampLevel">Champion level at the end (<c>champLevel</c>).</param>
/// <param name="Cs">Creep score: <c>totalMinionsKilled</c> + <c>neutralMinionsKilled</c>.</param>
/// <param name="Gold"><c>goldEarned</c>.</param>
/// <param name="Damage">Damage to champions (<c>totalDamageDealtToChampions</c>).</param>
/// <param name="VisionScore"><c>visionScore</c>.</param>
/// <param name="Items">Always 7 item ids, <c>item0</c>–<c>item6</c> (the last is the trinket); 0 = empty slot.</param>
/// <param name="Spell1Id">Summoner spell (D).</param>
/// <param name="Spell2Id">Summoner spell (F).</param>
/// <param name="Keystone">Keystone rune id (<c>perk0</c>, e.g. 8008 Lethal Tempo); 0 when none.</param>
/// <param name="PrimaryStyle">Primary rune tree (<c>perkPrimaryStyle</c>, e.g. 8000 Precision); 0 when none.</param>
/// <param name="SubStyle">Secondary rune tree (<c>perkSubStyle</c>); 0 when none.</param>
/// <param name="Placement">Arena placement 1–8 (<c>subteamPlacement</c>); 0 outside Arena.</param>
/// <param name="Puuid">
/// A named teammate's puuid, what <see cref="CommandType.LoadPlayerProfile"/> takes to open their profile (the connector
/// re-checks that it named them). Empty for every line without a name (enemies, anonymous players), for the local player
/// and, in another player's game, for that player (their puuid is <see cref="PlayerProfile.Puuid"/>).
/// </param>
public sealed record MatchParticipant(
    int ChampionId,
    bool IsMe,
    string GameName,
    string TagLine,
    int ChampLevel,
    int Kills,
    int Deaths,
    int Assists,
    int Cs,
    int Gold,
    int Damage,
    int VisionScore,
    IReadOnlyList<int> Items,
    int Spell1Id,
    int Spell2Id,
    int Keystone,
    int PrimaryStyle,
    int SubStyle,
    int Placement,
    string Puuid);
