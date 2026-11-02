using System.Text.RegularExpressions;

namespace LeagueRemote.Connector.ClientSounds;

/// <summary>
/// The League client's own rank emblems for the phone, read from the user's installation like the client sounds: Data
/// Dragon doesn't have them and nothing of Riot's is copied into the repository. <c>/fe/lol-static-assets/images/ranked-emblem/emblem-{tier}.png</c>
/// sits raw in the plugin's <c>assets.wad</c> (all ten tiers checked in the 2026-11-03 install; the LCU's HTTPS API answers
/// <c>/fe/...</c> with 404). Only the listed tiers can be asked for. A read is kept in memory until the WAD changes (client
/// patch); nothing is cached on disk, so without an install the phone gets a 404 and shows its own symbol.
/// Served by the phone server: <c>GET /rank-emblems/{tier}.png</c>, tier in lower case.
/// </summary>
internal sealed partial class RankEmblemLibrary(string installPath, ILogger? logger = null)
{
    public const string RoutePrefix = "/rank-emblems";

    private const string Plugin = "rcp-fe-lol-static-assets";

    private static readonly HashSet<string> Tiers =
        ["iron", "bronze", "silver", "gold", "platinum", "emerald", "diamond", "master", "grandmaster", "challenger"];

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (long Length, long Ticks, byte[] Data)> _cache = new(StringComparer.Ordinal);

    /// <summary>The PNG for a request like <c>gold.png</c>, or null (unknown tier, bad name, not in this install).</summary>
    public byte[]? Get(string fileName)
    {
        if (FileNameRegex().Match(fileName) is not { Success: true } match || !Tiers.Contains(match.Groups["tier"].Value))
            return null;
        var tier = match.Groups["tier"].Value;
        var wad = new FileInfo(Path.Combine(installPath, "Plugins", Plugin, "assets.wad"));
        if (!wad.Exists)
            return null;
        var (length, ticks) = (wad.Length, wad.LastWriteTimeUtc.Ticks);
        lock (_lock)
        {
            if (_cache.TryGetValue(tier, out var cached) && cached.Length == length && cached.Ticks == ticks)
                return cached.Data;
        }

        try
        {
            var data = WadArchive.TryRead(wad.FullName, $"plugins/{Plugin}/global/default/images/ranked-emblem/emblem-{tier}.png");
            if (data is null || !data.AsSpan().StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
                return null;
            lock (_lock)
                _cache[tier] = (length, ticks, data);
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger?.LogDebug(ex, "Could not read the {Tier} rank emblem", tier);
            return null;
        }
    }

    [GeneratedRegex(@"^(?<tier>[a-z]{1,20})\.png$")]
    private static partial Regex FileNameRegex();
}
