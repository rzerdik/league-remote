using System.Text.Json;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Time.Testing;
using static LeagueRemote.Connector.Tests.AutoPickHarness;

namespace LeagueRemote.Connector.Tests;

/// <summary><see cref="AutoPickSettingsStore"/> (the settings file) and <see cref="AutoPickStore"/> (what the phone gets).</summary>
public sealed class AutoPickSettingsStoreTests
{
    // --- Settings store -------------------------------------------------------------------

    [Fact]
    public void Settings_round_trip_through_a_versioned_file()
    {
        using var h = new AutoPickHarness();
        var settings = new AutoPickSettings(true,
            [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238], [103, 99], 4, 14, 1234567890123)]);

        Assert.True(h.Settings.Update(_ => settings));

        var path = Path.Combine(h.Folder, AutoPickSettingsStore.FileName);
        using (var file = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal(1, file.RootElement.GetProperty("version").GetInt32());
            Assert.Equal(1234567890123, file.RootElement.GetProperty("settings").GetProperty("presets")[0].GetProperty("runePageId").GetInt64());
        }

        var loaded = new AutoPickSettingsStore(h.Folder).Current;
        Assert.True(loaded.Enabled);
        Assert.Equal(settings.Presets[0], loaded.Presets[0], AutoPickPresetComparer.Instance);
        Assert.Equal([AutoPickSettingsStore.FileName], Directory.GetFiles(h.Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void A_failed_write_leaves_the_previous_file_and_settings()
    {
        using var h = new AutoPickHarness(Draft());
        Directory.CreateDirectory(Path.Combine(h.Folder, AutoPickSettingsStore.FileName + ".tmp")); // the temp file can't be written

        Assert.False(h.Settings.Update(s => s with { Enabled = false }));

        Assert.True(h.Settings.Current.Enabled);
        Assert.True(new AutoPickSettingsStore(h.Folder).Current.Enabled);
    }

    [Fact]
    public void Reading_settings_never_creates_the_folder()
    {
        using var h = new AutoPickHarness();

        Assert.Equal(AutoPickSettings.Default, h.Settings.Current);
        Assert.False(Directory.Exists(h.Folder));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("""{ "version": 2, "settings": { "enabled": true, "presets": [] } }""")]
    [InlineData("""{ "settings": { "enabled": true, "presets": [] } }""")]
    public void A_corrupt_or_newer_file_is_moved_aside_and_the_defaults_are_used(string content)
    {
        using var h = new AutoPickHarness();
        Directory.CreateDirectory(h.Folder);
        var path = Path.Combine(h.Folder, AutoPickSettingsStore.FileName);
        File.WriteAllText(path, content);

        var store = new AutoPickSettingsStore(h.Folder);

        Assert.False(store.Current.Enabled);
        Assert.Empty(store.Current.Presets);
        Assert.False(File.Exists(path));
        var aside = Assert.Single(Directory.GetFiles(h.Folder, AutoPickSettingsStore.FileName + ".corrupt-*"));
        Assert.Equal(content, File.ReadAllText(aside));
    }

    [Fact]
    public void Invalid_presets_are_dropped_on_load_and_the_rest_are_sorted()
    {
        using var h = new AutoPickHarness();
        Directory.CreateDirectory(h.Folder);
        File.WriteAllText(Path.Combine(h.Folder, AutoPickSettingsStore.FileName), """
        {
          "version": 1,
          "settings": {
            "enabled": true,
            "presets": [
              { "queueGroup": "draft", "role": "UTILITY", "banChampionIds": [], "pickChampionIds": [412] },
              { "queueGroup": "aram", "role": "ANY", "banChampionIds": [], "pickChampionIds": [103] },
              { "queueGroup": "arena", "role": "ANY", "banChampionIds": [], "pickChampionIds": [103] },
              { "queueGroup": "draft", "role": "FILL", "banChampionIds": [], "pickChampionIds": [103] },
              { "queueGroup": "draft", "role": "TOP", "banChampionIds": [1, 1], "pickChampionIds": [] },
              { "queueGroup": "draft", "role": "JUNGLE", "spell1Id": 11 },
              { "queueGroup": "blind", "role": "ANY", "banChampionIds": null, "pickChampionIds": [86], "spell1Id": 4, "spell2Id": 12 },
              { "queueGroup": "draft", "role": "ANY", "banChampionIds": [238], "pickChampionIds": [103] },
              { "queueGroup": "draft", "role": "UTILITY", "banChampionIds": [], "pickChampionIds": [40] },
              { "queueGroup": "draft", "role": "MIDDLE", "banChampionIds": [], "pickChampionIds": [] },
              null
            ]
          }
        }
        """);

        var loaded = new AutoPickSettingsStore(h.Folder).Current;

        Assert.True(loaded.Enabled);
        Assert.Equal(
            [("draft", "ANY"), ("draft", "UTILITY"), ("blind", "ANY")],
            loaded.Presets.Select(p => (p.QueueGroup, p.Role)));
        Assert.Equal([412], loaded.Presets[1].PickChampionIds); // the first one per key
        Assert.Empty(loaded.Presets[2].BanChampionIds);
    }

    [Fact]
    public void Setting_a_preset_replaces_the_same_key_and_an_empty_one_removes_it()
    {
        var settings = Draft(true, new AutoPickPreset(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], [86]));

        var replaced = AutoPickSettingsStore.WithPreset(settings, new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [], [64]));
        var removed = AutoPickSettingsStore.WithPreset(replaced, new(AutoPickQueueGroups.Blind, AutoPickRoles.Any, [], []));

        Assert.Equal([64], AutoPickSettingsStore.Normalize(replaced).Presets.Single(p => p.QueueGroup == AutoPickQueueGroups.Draft).PickChampionIds);
        Assert.Equal(2, replaced.Presets.Count);
        Assert.Equal(AutoPickQueueGroups.Draft, Assert.Single(removed.Presets).QueueGroup);
    }

    // --- Store ----------------------------------------------------------------------------

    [Fact]
    public void Store_republishes_only_when_more_than_the_time_changed_and_touch_restamps()
    {
        var time = new FakeTimeProvider(Start);
        var store = new AutoPickStore(time);
        var changes = 0;
        store.Changed += () => changes++;
        var state = new AutoPickState(new AutoPickSettings(true, []), false, null, Start);

        Assert.True(store.Set(state));
        Assert.False(store.Set(state with { UpdatedAt = Start.AddSeconds(5) }));
        Assert.True(store.Set(state with { PausedForThisGame = true }));
        Assert.Equal((2, 2L), (changes, store.Snapshot().Version));

        time.Advance(TimeSpan.FromSeconds(7));
        store.Touch();
        Assert.Equal((3L, Start.AddSeconds(7)), (store.Snapshot().Version, store.Snapshot().Value.UpdatedAt));
    }
}
