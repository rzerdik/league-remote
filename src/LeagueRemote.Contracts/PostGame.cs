namespace LeagueRemote.Contracts;

/// <summary>
/// The client's post-game screen (gameflow phase EndOfGame) as the phone shows it: the result, your stats, the LP and
/// mastery the game gave, and both teams. Rides <see cref="ClientState.PostGame"/>, set only in the EndOfGame phase, so it
/// comes and goes with the screen it describes; answer it with <see cref="CommandType.PlayAgain"/> or
/// <see cref="CommandType.LeavePostGame"/>. Read from <c>GET /lol-end-of-game/v1/eog-stats-block</c>,
/// <c>/lol-ranked/v1/current-lp-change-notification</c>, <c>/lol-end-of-game/v1/champion-mastery-updates</c>,
/// <c>/lol-gameflow/v1/session</c> and <c>/lol-gameflow/v1/gameflow-metadata/player-status</c>.
/// Privacy as in <see cref="MatchDetails"/>: names for the own team only (Arena: the own duo), never an enemy's name; a
/// puuid only beside a teammate's name (<see cref="MatchParticipant.Puuid"/>, to open their profile), no summoner ids. Bounded (<see cref="MaxTeams"/>, <see cref="MaxPlayers"/>) so the state message stays
/// small: well under <see cref="MaxJsonBytes"/> even for Arena with the longest Riot IDs.
/// </summary>
/// <param name="GameId">The game (<c>gameId</c> of the stats block).</param>
/// <param name="QueueId">LCU queue id of the game (gameflow session <c>gameData.queue.id</c>); 0 when unknown.</param>
/// <param name="GameMode">LCU game mode ("CLASSIC", "ARAM", "CHERRY" (Arena), …).</param>
/// <param name="MapId">11 Summoner's Rift, 12 Howling Abyss, 30 Arena, …; 0 when unknown.</param>
/// <param name="DurationSeconds">Game length in seconds (<c>gameLength</c>).</param>
/// <param name="Result">
/// <see cref="MatchResults.Remake"/> (the game ended in an early surrender vote), <see cref="MatchResults.Win"/> or
/// <see cref="MatchResults.Loss"/> (the local player's team; Arena: a top-four placement wins).
/// </param>
/// <param name="IsRanked">The game counted for a ranked queue (<c>ranked</c>).</param>
/// <param name="Rank">The LP change of this game; null for unranked games or until the ranked service reports it.</param>
/// <param name="Mastery">The mastery the local player's champion earned; null until (or unless) the client reports it.</param>
/// <param name="Teams">The local player's team first, then the others by team id; Arena: one per duo, the own duo first, then by placement.</param>
/// <param name="CanPlayAgain">
/// The client offers Play Again for this game (<c>lastQueuedLobbyStatus.allowedPlayAgain</c>, what its own button checks).
/// </param>
public sealed record PostGameSummary(
    long GameId,
    int QueueId,
    string GameMode,
    int MapId,
    int DurationSeconds,
    string Result,
    bool IsRanked,
    PostGameRank? Rank,
    PostGameMastery? Mastery,
    IReadOnlyList<PostGameTeam> Teams,
    bool CanPlayAgain)
{
    /// <summary>Teams kept (Arena has eight duos).</summary>
    public const int MaxTeams = 8;

    /// <summary>Players kept in all (Arena has sixteen).</summary>
    public const int MaxPlayers = 16;

    /// <summary>The summary's JSON stays below this (checked by a test with Arena and the longest Riot IDs).</summary>
    public const int MaxJsonBytes = 16 * 1024;
}

/// <summary>One team of the post-game scoreboard (<c>teams[]</c>), or one duo in Arena.</summary>
/// <param name="TeamId">100 (blue side) or 200 (red side); in Arena the duo's number (no sides).</param>
/// <param name="IsMyTeam">The local player played on it (Arena: their duo).</param>
/// <param name="Win">LCU <c>isWinningTeam</c>; Arena duos: a top-four placement.</param>
/// <param name="Kills">The players' kills added up (like the client's team header).</param>
/// <param name="Gold">The players' gold earned added up.</param>
/// <param name="Participants">
/// In the client's order. <see cref="MatchParticipant"/> as in match details: names (and teammates' puuids) on the own
/// team only; <see cref="MatchParticipant.Keystone"/> and the rune trees are 0 when the stats block doesn't carry them.
/// </param>
public sealed record PostGameTeam(
    int TeamId,
    bool IsMyTeam,
    bool Win,
    int Kills,
    int Deaths,
    int Assists,
    int Gold,
    IReadOnlyList<MatchParticipant> Participants);

/// <summary>What the client's post-game shows about LP (<c>GET /lol-ranked/v1/current-lp-change-notification</c>).</summary>
/// <param name="QueueType">See <see cref="RankedQueueTypes"/>.</param>
/// <param name="Tier">The tier after the game ("IRON" … "CHALLENGER"); empty while unranked.</param>
/// <param name="Division">"I"–"IV" after the game; empty for apex tiers and while unranked.</param>
/// <param name="LeaguePoints">LP after the game.</param>
/// <param name="LeaguePointsDelta">LP this game gave (negative for a loss); 0 when it didn't count.</param>
/// <param name="ProvisionalGamesRemaining">Placement games left; 0 when placed.</param>
/// <param name="Change">See <see cref="PostGameRankChanges"/>.</param>
public sealed record PostGameRank(
    string QueueType,
    string Tier,
    string Division,
    int LeaguePoints,
    int LeaguePointsDelta,
    int ProvisionalGamesRemaining,
    string Change);

/// <summary>
/// <see cref="PostGameRank.Change"/>, from the notification's <c>notifyReason</c> as the client's post-game reads it
/// (rcp-fe-lol-postgame, patch 16.21: "LEAGUE_PROMOTED" / "LEAGUE_DEMOTED").
/// </summary>
public static class PostGameRankChanges
{
    public const string None = "";

    public const string Promoted = "promoted";

    public const string Demoted = "demoted";
}

/// <summary>The local player's champion mastery from this game (<c>GET /lol-end-of-game/v1/champion-mastery-updates</c>).</summary>
/// <param name="PointsGained">Mastery points this game gave (<c>pointsGained</c>).</param>
/// <param name="Level">Mastery level after the game.</param>
/// <param name="LeveledUp">The game raised the level (<c>hasLeveledUp</c>).</param>
/// <param name="Grade">The game's grade ("S+", "A-", …); empty when none.</param>
/// <param name="PointsUntilNextLevel">Points still missing for the next level after the game.</param>
public sealed record PostGameMastery(
    int ChampionId,
    int PointsGained,
    int Level,
    bool LeveledUp,
    string Grade,
    int PointsUntilNextLevel);
