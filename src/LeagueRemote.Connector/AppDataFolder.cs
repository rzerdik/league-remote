namespace LeagueRemote.Connector;

/// <summary>
/// The connector's per-user data folder, <c>%LOCALAPPDATA%\league-remote</c>: paired phones, push subscriptions and keys,
/// certificates, auto pick presets, cached client sounds, logs, and the optional <c>settings.json</c>. Nothing lives next to
/// the exe, so replacing or moving it keeps all of this.
/// </summary>
internal static class AppDataFolder
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "league-remote");

    /// <summary>The connector's log files (<see cref="Logging.FileLoggerProvider"/>).</summary>
    public static string Logs { get; } = Path.Combine(Root, "logs");

    /// <summary>The user's optional settings (<see cref="ConnectorSettings"/>).</summary>
    public static string Settings { get; } = Path.Combine(Root, ConnectorSettings.FileName);

    public static string Combine(string name) => Path.Combine(Root, name);
}
