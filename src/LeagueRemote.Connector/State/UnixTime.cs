using System.Globalization;

namespace LeagueRemote.Connector.State;

/// <summary>League's unix-millisecond timestamps, read leniently.</summary>
internal static class UnixTime
{
    /// <summary>Year 2000 to 9999: anything else is a missing or broken timestamp, not a date to show.</summary>
    public static DateTimeOffset? FromMs(long ms) =>
        ms is > 946_684_800_000 and < 253_402_300_799_000 ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    /// <summary>The same for a timestamp League sends as a string (friends' presence).</summary>
    public static DateTimeOffset? FromMs(string? ms) =>
        long.TryParse(ms, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? FromMs(value) : null;
}
