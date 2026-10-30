using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.Server;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace LeagueRemote.Connector;

/// <summary>
/// Where the connector's configuration comes from, lowest priority first: the defaults in code (the options classes and
/// <see cref="LogLevels"/>), the user's optional <c>settings.json</c> in the data folder, environment variables, the
/// command line. Nothing is read from the exe's folder: the exe is downloaded, moved and replaced as a single file.
/// </summary>
internal static class ConnectorSettings
{
    public const string FileName = "settings.json";

    /// <summary>
    /// Quiet framework categories. Kept as configuration rather than logging filters in code, so a <c>Logging</c> section
    /// in settings.json can still turn any of them up (code filters would win over configuration).
    /// </summary>
    public static IReadOnlyDictionary<string, string?> LogLevels { get; } = new Dictionary<string, string?>
    {
        ["Logging:LogLevel:Default"] = "Information",
        ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Information",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
        ["Logging:LogLevel:Microsoft.AspNetCore.SignalR"] = "Warning",
        ["Logging:LogLevel:Microsoft.AspNetCore.Http.Connections"] = "Warning",
        ["Logging:LogLevel:System.Net.Http.HttpClient"] = "Warning",
    };

    /// <summary>
    /// Replaces the host's <c>appsettings*.json</c> sources with <see cref="LogLevels"/> and <paramref name="settingsFile"/>,
    /// in the same place: after the host's own settings, before environment variables and the command line.
    /// </summary>
    /// <returns>A short problem description when the file exists but can't be used (it is skipped then), else null.</returns>
    public static string? Apply(IConfigurationBuilder configuration, string settingsFile)
    {
        var sources = configuration.Sources;
        var index = -1;
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is not JsonConfigurationSource)
                continue;
            sources.RemoveAt(i);
            index = i;
        }

        // Without appsettings sources (a host built differently), go before the first environment or command-line source.
        if (index < 0)
        {
            index = 0;
            while (index < sources.Count && sources[index] is MemoryConfigurationSource or ChainedConfigurationSource)
                index++;
        }

        sources.Insert(index, new MemoryConfigurationSource { InitialData = LogLevels });
        var problem = Check(settingsFile);
        if (problem is not null)
            return problem;

        var file = new JsonConfigurationSource { Path = settingsFile, Optional = true, ReloadOnChange = false };
        file.ResolveFileProvider(); // an absolute path outside the content root
        sources.Insert(index + 1, file);
        return null;
    }

    /// <summary>Writes <see cref="DefaultJson"/> to <paramref name="settingsFile"/> unless it exists.</summary>
    public static void CreateIfMissing(string settingsFile)
    {
        if (File.Exists(settingsFile))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        File.WriteAllText(settingsFile, DefaultJson());
    }

    /// <summary>Every setting with its built-in default, for the user to edit (JSON has no comments to leave them out).</summary>
    public static string DefaultJson()
    {
        var server = new ServerOptions();
        var lcu = new LcuOptions();
        return JsonSerializer.Serialize(new
        {
            Server = new { server.HttpPort, server.HttpsPort, PwaUrl = server.PwaUrl ?? "" },
            Lcu = new { lcu.InstallPath },
            Connector = new { Headless = false },
            Logging = new { LogLevel = new { Default = "Information" } },
        }, IndentedJson) + Environment.NewLine;
    }

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    /// <summary>Null when the file is missing or reads as JSON configuration; a broken file must not stop the app.</summary>
    private static string? Check(string settingsFile)
    {
        try
        {
            new ConfigurationBuilder().AddJsonFile(settingsFile, optional: true, reloadOnChange: false).Build();
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or IOException or UnauthorizedAccessException)
        {
            return $"{FileName} has an error and was ignored: {ex.InnerException?.Message ?? ex.Message}";
        }
    }
}
