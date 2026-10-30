using System.Text.Json;

namespace LeagueRemote.Connector.Lcu;

/// <summary>An LCU read whose failure only leaves its part out (a profile part, a post-game panel, the skin carousel).</summary>
internal static class OptionalRead
{
    /// <summary>An LCU error or a body that isn't JSON is "no answer" (Ok false); network errors propagate.</summary>
    public static async Task<(bool Ok, JsonElement? Json)> TryAsync(Func<CancellationToken, Task<JsonElement?>> read, CancellationToken ct)
    {
        try
        {
            return (true, await read(ct));
        }
        catch (Exception ex) when (ex is LcuRequestException or JsonException)
        {
            return (false, null);
        }
    }
}
