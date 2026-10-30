using System.Text.Json;
using System.Text.RegularExpressions;

namespace LeagueRemote.Connector.ClientSounds;

/// <summary>The available client sounds and music: key → URL path (<c>/client-sounds/{key}.ogg</c>).</summary>
internal sealed record ClientSoundManifest(IReadOnlyDictionary<string, string> Sounds, IReadOnlyDictionary<string, string> Music);

/// <summary>
/// The League client's own sounds and music for the phone, taken from the user's installation (see
/// <see cref="ClientSoundCatalog"/>) on first use and cached in <see cref="CacheDirectory"/> (the connector's local data
/// folder; never the repository). The cache is re-checked at most every <see cref="RefreshInterval"/> (every
/// <see cref="RetryInterval"/> while a game-data file is still missing because the client wasn't running): a file is
/// re-extracted when its source changed (client patch); cached files stay available while the client is not installed at
/// the configured path or not running. About 50 MB once everything is cached (the music is most of it).
/// Served by the phone server:<c>GET /client-sounds/manifest.json</c> and <c>GET /client-sounds/{key}.{ext}</c>.
/// </summary>
/// <param name="gameData">
/// Fetches a <c>/lol-game-data/assets/...</c> file from the running client (null when no client is connected or the file
/// is missing); without it only the plugin WADs are read.
/// </param>
internal sealed partial class ClientSoundLibrary(
    string installPath,
    string cacheDirectory,
    TimeProvider time,
    ILogger? logger = null,
    Func<string, CancellationToken, Task<byte[]?>>? gameData = null)
{
    public const string RoutePrefix = "/client-sounds";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

    public static string DefaultCacheDirectory { get; } = AppDataFolder.Combine("client-sounds");

    private const string IndexFile = "index.json";
    private const string GameDataPlugin = "rcp-be-lol-game-data";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Where a cached file came from, so a client patch triggers a re-extract: the WAD's size and time, or for game-data
    /// files those of the game-data plugin's WADs (the LCU serves them from there).
    /// </summary>
    private sealed record CachedSound(string Source, string File, long WadLength, long WadWriteTicks);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyDictionary<string, string> _files = new Dictionary<string, string>();
    private DateTimeOffset _refreshedAt = DateTimeOffset.MinValue;
    private TimeSpan _nextRefresh = TimeSpan.Zero;

    public string CacheDirectory => cacheDirectory;

    /// <summary>Every available sound and music track, by key.</summary>
    public async Task<ClientSoundManifest> GetManifestAsync(CancellationToken ct)
    {
        var files = await EnsureFreshAsync(ct);
        Dictionary<string, string> sounds = new(StringComparer.Ordinal), music = new(StringComparer.Ordinal);
        foreach (var (key, path) in files)
            (ClientSoundCatalog.IsMusic(key) ? music : sounds)[key] = $"{RoutePrefix}/{Path.GetFileName(path)}";
        return new ClientSoundManifest(sounds, music);
    }

    /// <summary>The cached file for a request like <c>matchFound.ogg</c>, or null (unknown key, not available, bad name).</summary>
    public async Task<string?> GetFileAsync(string fileName, CancellationToken ct)
    {
        if (FileNameRegex().Match(fileName) is not { Success: true } match || !ClientSoundCatalog.Contains(match.Groups["key"].Value))
            return null;
        var files = await EnsureFreshAsync(ct);
        return files.TryGetValue(match.Groups["key"].Value, out var path)
               && string.Equals(Path.GetFileName(path), fileName, StringComparison.Ordinal)
               && File.Exists(path)
            ? path
            : null;
    }

    public static string ContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".ogg" => "audio/ogg",
        ".webm" => "audio/webm",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        _ => "application/octet-stream",
    };

    private async Task<IReadOnlyDictionary<string, string>> EnsureFreshAsync(CancellationToken ct)
    {
        if (time.GetUtcNow() - _refreshedAt < _nextRefresh)
            return _files;

        await _gate.WaitAsync(ct);
        try
        {
            if (time.GetUtcNow() - _refreshedAt >= _nextRefresh)
            {
                var (files, complete) = await RefreshAsync(ct);
                _files = files;
                _refreshedAt = time.GetUtcNow();
                _nextRefresh = complete ? RefreshInterval : RetryInterval;
            }

            return _files;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <returns>The files, and false when a game-data file could not be fetched (no client) and is worth retrying soon.</returns>
    private async Task<(Dictionary<string, string> Files, bool Complete)> RefreshAsync(CancellationToken ct)
    {
        var index = LoadIndex();
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var complete = true;
        var gameDataStamp = GameDataStamp();
        // Several sounds come from the same plugin WAD: its table of contents is read once per refresh (null: unreadable).
        var tocs = new Dictionary<string, IReadOnlyDictionary<ulong, WadArchive.Entry>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, candidates) in ClientSoundCatalog.All)
        {
            var found = false;
            foreach (var source in candidates)
            {
                (long Length, long Ticks) stamp;
                Func<Task<byte[]?>> read;
                if (ClientSoundCatalog.Locate(source) is { } location)
                {
                    var wad = new FileInfo(Path.Combine(installPath, location.WadRelativePath));
                    if (!wad.Exists)
                        continue;
                    stamp = (wad.Length, wad.LastWriteTimeUtc.Ticks);
                    read = () => Task.Run(() => ReadWad(wad.FullName, location.EntryPath, tocs), ct);
                }
                else if (ClientSoundCatalog.IsGameDataAsset(source) && gameData is not null)
                {
                    stamp = gameDataStamp;
                    read = () => FetchGameData(source, ct);
                }
                else
                {
                    continue;
                }

                var fileName = key + Path.GetExtension(source).ToLowerInvariant();
                var target = Path.Combine(cacheDirectory, fileName);
                if (index.TryGetValue(key, out var cached) && cached.Source == source && cached.WadLength == stamp.Length
                    && cached.WadWriteTicks == stamp.Ticks && File.Exists(target))
                {
                    files[key] = target;
                    found = true;
                    break;
                }

                if (await read() is not { } data)
                {
                    if (!ClientSoundCatalog.IsGameDataAsset(source))
                        continue;
                    // The client isn't running (or doesn't have it): retry soon, meanwhile serve a cached copy below.
                    complete = false;
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(cacheDirectory);
                    var temp = target + ".tmp";
                    await File.WriteAllBytesAsync(temp, data, ct);
                    File.Move(temp, target, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger?.LogWarning(ex, "Could not cache client sound {Key}", key);
                    continue;
                }

                index[key] = new CachedSound(source, fileName, stamp.Length, stamp.Ticks);
                files[key] = target;
                found = true;
                break;
            }

            // Client not found at the install path / not running (or the file moved in a patch): keep serving what is cached.
            if (!found && index.TryGetValue(key, out var stale) && File.Exists(Path.Combine(cacheDirectory, stale.File)))
                files[key] = Path.Combine(cacheDirectory, stale.File);
        }

        SaveIndex(index);
        logger?.LogInformation("Client sounds available: {Count}/{Total}", files.Count, ClientSoundCatalog.Sounds.Count + ClientSoundCatalog.Music.Count);
        return (files, complete);
    }

    /// <summary>Not thread-safe over <paramref name="tocs"/>: the refresh reads one file at a time.</summary>
    private byte[]? ReadWad(string wadPath, string entryPath, Dictionary<string, IReadOnlyDictionary<ulong, WadArchive.Entry>?> tocs)
    {
        try
        {
            if (!tocs.TryGetValue(wadPath, out var toc))
            {
                tocs[wadPath] = null; // stays null when unreadable: not retried within this refresh
                tocs[wadPath] = toc = WadArchive.ReadToc(wadPath);
            }

            return toc is null ? null : WadArchive.TryRead(wadPath, toc, entryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger?.LogDebug(ex, "Could not read {Wad}", wadPath);
            return null;
        }
    }

    private async Task<byte[]?> FetchGameData(string source, CancellationToken ct)
    {
        try
        {
            var data = await gameData!(source, ct);
            // Only audio is kept (the LCU answers unknown paths with JSON errors).
            return data is { Length: > 4 } && data.AsSpan(0, 4).SequenceEqual("OggS"u8) ? data : null;
        }
        catch (Exception ex) when (ex is IOException && !ct.IsCancellationRequested || Lcu.LcuErrors.IsTransient(ex, ct))
        {
            logger?.LogDebug(ex, "Could not fetch {Source} from the League client", source);
            return null;
        }
    }

    /// <summary>Size and newest write time of the game-data plugin's WADs: changes with a client patch.</summary>
    private (long Length, long Ticks) GameDataStamp()
    {
        try
        {
            var dir = new DirectoryInfo(Path.Combine(installPath, "Plugins", GameDataPlugin));
            if (!dir.Exists)
                return (0, 0);
            var wads = dir.GetFiles("*.wad");
            return (wads.Sum(w => w.Length), wads.Length == 0 ? 0 : wads.Max(w => w.LastWriteTimeUtc.Ticks));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (0, 0);
        }
    }

    private Dictionary<string, CachedSound> LoadIndex()
    {
        try
        {
            var path = Path.Combine(cacheDirectory, IndexFile);
            if (File.Exists(path) && JsonSerializer.Deserialize<Dictionary<string, CachedSound>>(File.ReadAllText(path), Json) is { } index)
            {
                // Only plain file names inside the cache directory are trusted.
                return index.Where(e => FileNameRegex().IsMatch(e.Value.File) && ClientSoundCatalog.Contains(e.Key))
                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger?.LogDebug(ex, "Ignoring the client sound cache index");
        }

        return new Dictionary<string, CachedSound>(StringComparer.Ordinal);
    }

    private void SaveIndex(Dictionary<string, CachedSound> index)
    {
        if (index.Count == 0)
            return;
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllText(Path.Combine(cacheDirectory, IndexFile), JsonSerializer.Serialize(index, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(ex, "Could not write the client sound cache index");
        }
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z][A-Za-z0-9]{0,39})\.(?:ogg|webm|mp3|wav)$")]
    private static partial Regex FileNameRegex();
}
