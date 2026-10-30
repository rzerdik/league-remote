using Microsoft.Extensions.Logging;

namespace LeagueRemote.Connector.Lcu;

/// <summary>
/// Logging for LCU work that failed. League's own error text can name a player or a champion pick, so it goes to Debug; the
/// Warning carries only the HTTP status and League's error code. Other failures (no answer, malformed JSON) carry nothing
/// of League's, so their message stays at Warning.
/// </summary>
internal static class LcuFailureLog
{
    public static void LogLcuFailure(this ILogger logger, string what, Exception ex)
    {
        if (ex is LcuRequestException lcu)
        {
            // League doesn't always send an error code or a message: an empty value, not "(null)".
            logger.LogWarning("{What} failed: HTTP {Status} {ErrorCode}", what, (int)lcu.StatusCode, lcu.ErrorCode ?? "");
            logger.LogDebug("{What} failure: {Message}", what, lcu.LcuMessage ?? "");
        }
        else
        {
            logger.LogWarning("{What} failed: {Message}", what, ex.Message);
        }
    }
}
