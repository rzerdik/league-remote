using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// The user's <see cref="AutoPickSettings"/>, persisted as <see cref="FileName"/> in the connector's data folder
/// (<c>%LOCALAPPDATA%\league-remote</c>; tests pass a temp folder) as <c>{ "version": 1, "settings": { … } }</c>.
/// Loaded once when constructed: a missing file is <see cref="AutoPickSettings.Default"/>; unreadable JSON or a version
/// this build doesn't know is moved aside (<c>auto-pick.json.corrupt-yyyyMMddHHmmss</c>) and the defaults are used. Every
/// preset is re-validated with the whitelist's rules on load (<see cref="Normalize"/>). Writes are atomic (temp file +
/// rename) and serialized; a failed write leaves the previous file and the previous settings.
/// </summary>
internal sealed class AutoPickSettingsStore
{
    public const string FileName = "auto-pick.json";

    /// <summary>Bumped only for incompatible changes; new optional fields don't need it.</summary>
    internal const int CurrentVersion = 1;

    private sealed record StoredFile(int Version, AutoPickSettings? Settings);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Lock _lock = new();
    private readonly string _folder;
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;
    private AutoPickSettings _current = AutoPickSettings.Default;

    public AutoPickSettingsStore(string folder, ILogger<AutoPickSettingsStore>? logger = null, TimeProvider? time = null)
    {
        _folder = folder;
        _path = Path.Combine(folder, FileName);
        _logger = logger;
        _time = time ?? TimeProvider.System;
        Load();
    }

    public AutoPickSettings Current
    {
        get
        {
            lock (_lock)
                return _current;
        }
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the current settings, normalizes the result and saves it. Returns false (and
    /// keeps the previous settings) when the file couldn't be written.
    /// </summary>
    public bool Update(Func<AutoPickSettings, AutoPickSettings> change)
    {
        lock (_lock)
        {
            var next = Normalize(change(_current));
            var temp = _path + ".tmp";
            try
            {
                // Created on the first save only, so merely reading the settings never creates the folder.
                Directory.CreateDirectory(_folder);
                File.WriteAllText(temp, JsonSerializer.Serialize(new StoredFile(CurrentVersion, next), Json));
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(ex, "Could not write {Path}", _path);
                TryDelete(temp);
                return false;
            }

            _current = next;
            return true;
        }
    }

    /// <summary>
    /// The settings with <paramref name="preset"/> in place of the preset with the same group and role; an empty preset
    /// (<see cref="AutoPickPreset.IsEmpty"/>) removes it.
    /// </summary>
    public static AutoPickSettings WithPreset(AutoPickSettings settings, AutoPickPreset preset) =>
        settings with
        {
            Presets =
            [
                .. settings.Presets.Where(p => p.QueueGroup != preset.QueueGroup || p.Role != preset.Role),
                .. preset.IsEmpty ? [] : new[] { preset },
            ],
        };

    /// <summary>
    /// Drops presets the whitelist would refuse (<see cref="CommandWhitelist.Validate"/> of the matching
    /// <see cref="CommandType.SetAutoPickPreset"/>) and empty ones, keeps the first per (group, role), sorts by group then
    /// role (<see cref="AutoPickQueueGroups.All"/>, <see cref="AutoPickRoles.All"/>). Tolerates nulls from a hand-edited file.
    /// </summary>
    public static AutoPickSettings Normalize(AutoPickSettings? settings)
    {
        if (settings is null)
            return AutoPickSettings.Default;

        var presets = (settings.Presets ?? [])
            .Where(p => p is not null)
            .Select(p => p with
            {
                BanChampionIds = p.BanChampionIds ?? [],
                PickChampionIds = p.PickChampionIds ?? [],
                Spell1Id = Math.Max(0, p.Spell1Id),
                Spell2Id = Math.Max(0, p.Spell2Id),
                RunePageName = p.RunePageId > 0
                    ? Text.Clip(p.RunePageName, AutoPickSettings.MaxRunePageNameLength)
                    : "",
            })
            .Where(p => !p.IsEmpty && CommandWhitelist.Validate(AsCommand(p)) is null)
            .DistinctBy(p => (p.QueueGroup, p.Role))
            .OrderBy(p => IndexOf(AutoPickQueueGroups.All, p.QueueGroup))
            .ThenBy(p => IndexOf(AutoPickRoles.All, p.Role))
            .Take(AutoPickSettings.MaxPresets)
            .ToList();
        return new AutoPickSettings(settings.Enabled, presets, settings.AutoAccept);
    }

    private static RemoteCommand AsCommand(AutoPickPreset p) => new(
        "load",
        CommandType.SetAutoPickPreset,
        Spell1Id: p.Spell1Id > 0 || p.Spell2Id > 0 ? p.Spell1Id : null,
        Spell2Id: p.Spell1Id > 0 || p.Spell2Id > 0 ? p.Spell2Id : null,
        RunePageId: p.RunePageId != 0 ? p.RunePageId : null,
        QueueGroup: p.QueueGroup,
        Role: p.Role,
        BanChampionIds: p.BanChampionIds,
        PickChampionIds: p.PickChampionIds);

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return i;
        }

        return list.Count;
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        StoredFile? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredFile>(File.ReadAllText(_path), Json);
        }
        catch (JsonException ex)
        {
            MoveAside(ex, "unreadable");
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked or unreadable right now (antivirus): keep the file, use the defaults for this run.
            _logger?.LogError(ex, "Could not read {Path}; auto pick starts with no presets", _path);
            return;
        }

        if (stored is not { Version: CurrentVersion })
        {
            // A newer connector wrote it (or it isn't ours): keep it for inspection rather than overwrite it later.
            MoveAside(null, $"version {stored?.Version} is not supported");
            return;
        }

        _current = Normalize(stored.Settings);
        _logger?.LogInformation("Loaded {Count} auto pick presets from {Path} (enabled: {Enabled})",
            _current.Presets.Count, _path, _current.Enabled);
    }

    private void MoveAside(Exception? ex, string reason)
    {
        var aside = $"{_path}.corrupt-{_time.GetUtcNow():yyyyMMddHHmmss}";
        _logger?.LogError(ex, "Auto pick settings {Path} are {Reason}; moved to {Aside}, starting with no presets", _path, reason, aside);
        try
        {
            File.Move(_path, aside, overwrite: true);
        }
        catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(moveEx, "Could not move {Path} aside", _path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
