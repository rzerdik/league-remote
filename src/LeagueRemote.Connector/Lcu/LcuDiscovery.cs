using Microsoft.Extensions.Options;

namespace LeagueRemote.Connector.Lcu;

/// <summary>Finds the running League client: lockfile first, LeagueClientUx command line as fallback.</summary>
internal sealed class LcuDiscovery(
    IOptions<LcuOptions> options,
    ILcuProcessInspector processInspector,
    ILogger<LcuDiscovery> logger)
{
    public async Task<LcuCredentials> WaitForClientAsync(CancellationToken ct)
    {
        while (true)
        {
            var credentials = TryDiscover();
            if (credentials is not null)
                return credentials;

            await Task.Delay(options.Value.DiscoveryInterval, ct);
        }
    }

    public LcuCredentials? TryDiscover() => TryReadLockfile() ?? TryReadProcessArgs();

    private LcuCredentials? TryReadLockfile()
    {
        var path = Path.Combine(options.Value.InstallPath, "lockfile");
        if (!File.Exists(path))
            return null;

        try
        {
            // The client keeps the lockfile open, so we must allow sharing.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var credentials = LockfileParser.Parse(reader.ReadToEnd());
            if (credentials is null)
                logger.LogWarning("Lockfile at {Path} is malformed", path);
            return credentials;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read lockfile at {Path}", path);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Access denied reading lockfile at {Path}", path);
            return null;
        }
    }

    private LcuCredentials? TryReadProcessArgs()
    {
        var process = processInspector.FindLeagueClientUx();
        if (process is null)
            return null;

        return ProcessArgsParser.Parse(process.CommandLine) is { } credentials
            ? credentials with { Pid = process.Pid }
            : null;
    }
}
