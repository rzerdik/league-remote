namespace LeagueRemote.Connector;

/// <summary>
/// The connector's per-user data folder, <c>%LOCALAPPDATA%\league-remote</c>: paired phones, push subscriptions and keys,
/// certificates, auto pick presets, cached client sounds, logs.
/// </summary>
internal static class AppDataFolder
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "league-remote");

    /// <summary>The connector's log files (<see cref="Logging.FileLoggerProvider"/>).</summary>
    public static string Logs { get; } = Path.Combine(Root, "logs");

    public static string Combine(string name) => Path.Combine(Root, name);
}
