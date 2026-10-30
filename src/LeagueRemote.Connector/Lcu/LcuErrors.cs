using System.Text.Json;

namespace LeagueRemote.Connector.Lcu;

internal static class LcuErrors
{
    /// <summary>
    /// A failure worth retrying rather than dropping the connection: League answered with an error (mid phase transition, a
    /// service restarting), didn't answer in time, or sent a body that isn't JSON (a starting client). False once
    /// <paramref name="ct"/> is cancelled, so shutdown and reconnects aren't swallowed.
    /// </summary>
    public static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex is LcuRequestException or HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested;
}
