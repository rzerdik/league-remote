using System.Globalization;

namespace LeagueRemote.Connector.Lcu;

internal static class LockfileParser
{
    /// <summary>Parses <c>name:pid:port:password:protocol</c>. Returns null when the content is malformed.</summary>
    public static LcuCredentials? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var parts = content.Trim().Split(':');
        if (parts.Length != 5)
            return null;

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(parts[3]) ||
            string.IsNullOrWhiteSpace(parts[4]))
        {
            return null;
        }

        return new LcuCredentials(port, parts[3], parts[4], pid);
    }
}
