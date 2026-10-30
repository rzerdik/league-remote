using System.Text.Json;

// Kept in the State namespace: every mapper already imports it.
namespace LeagueRemote.Connector.State;

/// <summary>
/// Lenient accessors for LCU and Live Client JSON: missing or wrongly typed properties yield defaults instead of throwing,
/// because the shapes change between patches.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>An empty object, for chaining lenient reads through a missing child (<c>e.Obj("x") ?? EmptyObject</c>).</summary>
    public static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static string? Str(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool Bool(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static double Dbl(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    public static long Long(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : 0;

    public static int Int(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    public static IEnumerable<JsonElement> Arr(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    /// <summary>A child object; null when <paramref name="e"/> isn't an object or the child is missing or not an object.</summary>
    public static JsonElement? Obj(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    /// <summary>An integer that may also arrive as a float (<c>80.0</c>), rounded; 0 when missing or not finite.</summary>
    public static int IntLenient(this JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number)
            return 0;
        if (v.TryGetInt32(out var i))
            return i;
        var d = v.GetDouble();
        return double.IsFinite(d) ? (int)Math.Clamp(Math.Round(d), int.MinValue, int.MaxValue) : 0;
    }

    /// <summary>A flag sent as a JSON boolean or as the string "True" (the Live Client Data API does the latter).</summary>
    public static bool BoolLenient(this JsonElement e, string name) =>
        e.Bool(name) || string.Equals(e.Str(name), "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>The objects of a JSON array (an LCU list); nothing when it is missing (404) or not an array.</summary>
    public static IEnumerable<JsonElement> Objects(this JsonElement? e) =>
        e is { ValueKind: JsonValueKind.Array } array ? array.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object) : [];

    /// <summary>The Riot ID game name, or the pre-Riot-ID <c>name</c> that some old entries still only have.</summary>
    public static string GameNameOrName(this JsonElement e) =>
        e.Str("gameName") is { Length: > 0 } gameName ? gameName : e.Str("name") ?? "";
}
