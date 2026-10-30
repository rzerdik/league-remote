namespace LeagueRemote.Connector.Lcu;

/// <summary>The values of <c>/lol-gameflow/v1/gameflow-phase</c> the connector acts on.</summary>
internal static class GameflowPhases
{
    public const string None = "None";
    public const string Lobby = "Lobby";
    public const string Matchmaking = "Matchmaking";
    public const string ReadyCheck = "ReadyCheck";
    public const string ChampSelect = "ChampSelect";
    public const string GameStart = "GameStart";
    public const string InProgress = "InProgress";
    public const string Reconnect = "Reconnect";
    public const string WaitingForStats = "WaitingForStats";
    public const string PreEndOfGame = "PreEndOfGame";
    public const string EndOfGame = "EndOfGame";

    /// <summary>The game process runs (or is being rejoined): the Live Client Data API can answer.</summary>
    public static bool IsGameRunning(string? phase) => phase is GameStart or InProgress or Reconnect;

    /// <summary>
    /// The game is being played or its result is on its way. Leaving these phases (other than for Reconnect) means a game
    /// just finished.
    /// </summary>
    public static bool IsGameOrStats(string? phase) => phase is InProgress or WaitingForStats or PreEndOfGame;

    /// <summary>
    /// A game is over when the phase leaves <see cref="IsGameOrStats"/> for anything but Reconnect, or reaches EndOfGame
    /// (also when the connector only connected then).
    /// </summary>
    public static bool GameJustEnded(string? previous, string phase) =>
        phase == EndOfGame
            ? previous != EndOfGame
            : IsGameOrStats(previous) && !IsGameOrStats(phase) && phase != Reconnect;
}
