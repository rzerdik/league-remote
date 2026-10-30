using System.Text.Json;
using System.Text.RegularExpressions;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.ChampSelect;

/// <summary>
/// Pure mapping of the champ-select skin carousel (<c>/lol-champ-select/v1/skin-carousel-skins</c>) to
/// <see cref="SkinSelection"/>. Mirrors what the client's own carousel (rcp-fe-lol-champ-select, patch 16.21) offers:
/// it drops <c>disabled</c> skins and only equips <c>unlocked</c> ones, and it only shows the carousel once the local
/// player's champion is locked in. The carousel is the local player's own collection; nothing about other players is read.
/// </summary>
internal static partial class SkinMapper
{
    /// <summary>
    /// The champion whose skins the local player can choose now: their champion once it is theirs
    /// (<see cref="ChampSelectRules.LocalLockedChampionId"/>), when the mode allows skins and the game isn't starting yet (the
    /// client locks its carousel then); 0 when none.
    /// </summary>
    public static int SkinChampionId(ChampSelectSession session) =>
        session.AllowSkinSelection && session.Timer.Phase != ChampSelectTimer.GameStarting
            ? ChampSelectRules.LocalLockedChampionId(session)
            : 0;

    /// <summary>The local player's equipped skin or chroma (<c>myTeam[].selectedSkinId</c>); 0 when unknown.</summary>
    public static int SelectedSkinId(ChampSelectSession session) => session.Me?.SelectedSkinId is > 0 and var id ? id : 0;

    /// <summary>
    /// The skins for the state: null until the local player can choose (see <see cref="SkinChampionId"/>) or when the carousel
    /// has nothing they can equip; cut to the <see cref="SkinSelection"/> limits (see <see cref="Bound"/>).
    /// </summary>
    public static SkinSelection? Map(ChampSelectSession session, JsonElement? carousel)
    {
        var championId = SkinChampionId(session);
        if (championId <= 0)
            return null;

        var skins = Bound(Equippable(carousel, championId));
        return skins.Count == 0 ? null : new SkinSelection(championId, SelectedSkinId(session), skins);
    }

    /// <summary>True when <paramref name="skinId"/> is a skin or chroma of <paramref name="championId"/> the carousel lets the local player equip.</summary>
    public static bool IsEquippable(JsonElement? carousel, int championId, int skinId) =>
        skinId > 0 && Equippable(carousel, championId).Any(s => s.Id == skinId || s.Chromas.Any(c => c.Id == skinId));

    /// <summary>
    /// Every equippable skin of <paramref name="championId"/> in the carousel (not disabled, unlocked, a positive id), the
    /// default skin first, with all its equippable chromas; no size limits. A skin of another champion (a carousel that
    /// hasn't caught up with a trade yet) is left out.
    /// </summary>
    public static IReadOnlyList<ChampionSkin> Equippable(JsonElement? carousel, int championId)
    {
        var seen = new HashSet<int>();
        return carousel.Objects()
            .Where(s => IsEquippable(s, championId) && seen.Add(s.Int("id")))
            .Select(s => new ChampionSkin(
                s.Int("id"),
                Name(s.Str("name")),
                s.Bool("isBase"),
                s.Arr("childSkins")
                    .Where(c => c.ValueKind == JsonValueKind.Object && IsEquippable(c, championId) && seen.Add(c.Int("id")))
                    .Select(c => new SkinChroma(c.Int("id"), Name(c.Str("name"), c.Str("shortName")), Colors(c)))
                    .ToList()))
            .OrderByDescending(s => s.IsBase) // stable: the carousel's order otherwise
            .ToList();
    }

    /// <summary>
    /// Cuts the list to the <see cref="SkinSelection"/> limits, keeping the carousel's order: first the skins themselves
    /// (≤ <see cref="SkinSelection.MaxSkins"/>), then their chromas skin by skin (≤ <see cref="SkinSelection.MaxChromas"/> each,
    /// ≤ <see cref="SkinSelection.MaxTotalChromas"/> in all), as long as the estimated JSON stays within
    /// <see cref="SkinSelection.MaxJsonBytes"/>. A skin without its chromas still lets you equip the skin.
    /// </summary>
    public static IReadOnlyList<ChampionSkin> Bound(IReadOnlyList<ChampionSkin> skins)
    {
        var budget = SkinSelection.MaxJsonBytes;
        var kept = new List<ChampionSkin>();
        foreach (var skin in skins.Take(SkinSelection.MaxSkins))
        {
            var cost = EntryJsonBytes + JsonBytes(skin.Name);
            if (cost > budget)
                break;
            budget -= cost;
            kept.Add(skin with { Chromas = [] });
        }

        var chromasLeft = SkinSelection.MaxTotalChromas;
        for (var i = 0; i < kept.Count && chromasLeft > 0; i++)
        {
            var chromas = new List<SkinChroma>();
            foreach (var chroma in skins[i].Chromas.Take(Math.Min(SkinSelection.MaxChromas, chromasLeft)))
            {
                var cost = EntryJsonBytes + JsonBytes(chroma.Name) + chroma.Colors.Sum(c => c.Length + 3);
                if (cost > budget)
                    break;
                budget -= cost;
                chromas.Add(chroma);
            }

            chromasLeft -= chromas.Count;
            kept[i] = kept[i] with { Chromas = chromas };
        }

        return kept;
    }

    /// <summary>
    /// The JSON around one skin or chroma besides its name and colours: <c>{"id":…,"name":"","isBase":false,"chromas":[]},</c>
    /// with a ten-digit id is 55 bytes; rounded up.
    /// </summary>
    private const int EntryJsonBytes = 64;

    /// <summary>
    /// What a string takes in SignalR's JSON at most: the default encoder writes printable ASCII as is and escapes the rest
    /// (non-ASCII, quotes, <c>&lt; &gt; &amp; ' + `</c>, backslash) as <c>\uXXXX</c>, 6 bytes per UTF-16 unit.
    /// </summary>
    internal static int JsonBytes(string text) =>
        text.Sum(c => c is >= ' ' and <= '~' && c is not ('"' or '\\' or '<' or '>' or '&' or '\'' or '+' or '`') ? 1 : 6);

    /// <summary>
    /// The skin belongs to <paramref name="championId"/>: its <c>championId</c>, or, when the client leaves that out, its id
    /// (skin ids are champion id × 1000 + the skin's number, chromas included).
    /// </summary>
    private static bool IsEquippable(JsonElement skin, int championId)
    {
        var id = skin.Int("id");
        var owner = skin.Int("championId");
        return id > 0 && skin.Bool("unlocked") && !skin.Bool("disabled") && (owner > 0 ? owner : id / 1000) == championId;
    }

    /// <summary>The name trimmed and cut to <see cref="SkinSelection.MaxNameLength"/>; <paramref name="fallback"/> when empty.</summary>
    private static string Name(string? name, string? fallback = null) =>
        Text.Clip((string.IsNullOrWhiteSpace(name) ? fallback : name)?.Trim(), SkinSelection.MaxNameLength);

    /// <summary>The chroma's swatch colours: only <c>#RRGGBB</c> values (the phone puts them in a style), at most two.</summary>
    private static IReadOnlyList<string> Colors(JsonElement chroma) =>
        chroma.Arr("colors")
            .Where(c => c.ValueKind == JsonValueKind.String && HexColor().IsMatch(c.GetString()!))
            .Select(c => c.GetString()!.ToUpperInvariant())
            .Take(2)
            .ToList();

    [GeneratedRegex(@"\A#[0-9A-Fa-f]{6}\z")]
    private static partial Regex HexColor();
}
