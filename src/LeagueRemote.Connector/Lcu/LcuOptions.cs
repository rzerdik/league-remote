namespace LeagueRemote.Connector.Lcu;

internal sealed class LcuOptions
{
    public const string Section = "Lcu";

    /// <summary>League install folder; the lockfile lives directly inside it.</summary>
    public string InstallPath { get; set; } = @"C:\Riot Games\League of Legends";

    public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromSeconds(2.5);
}
