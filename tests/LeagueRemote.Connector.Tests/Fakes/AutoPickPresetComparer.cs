using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Tests;

/// <summary>Records compare lists by reference; presets are equal when their values are.</summary>
internal sealed class AutoPickPresetComparer : IEqualityComparer<AutoPickPreset>
{
    public static readonly AutoPickPresetComparer Instance = new();

    public bool Equals(AutoPickPreset? x, AutoPickPreset? y) =>
        x is not null && y is not null
        && (x.QueueGroup, x.Role, x.Spell1Id, x.Spell2Id, x.RunePageId, x.RunePageName)
            == (y.QueueGroup, y.Role, y.Spell1Id, y.Spell2Id, y.RunePageId, y.RunePageName)
        && x.BanChampionIds.SequenceEqual(y.BanChampionIds) && x.PickChampionIds.SequenceEqual(y.PickChampionIds);

    public int GetHashCode(AutoPickPreset obj) => HashCode.Combine(obj.QueueGroup, obj.Role);
}
