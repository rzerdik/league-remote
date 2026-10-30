using System.Globalization;
using System.Text.RegularExpressions;

namespace LeagueRemote.Connector.Lcu;

internal static partial class ProcessArgsParser
{
    /// <summary>
    /// Extracts <c>--app-port=</c> and <c>--remoting-auth-token=</c> from a LeagueClientUx command line.
    /// Returns null when either is missing. Pid is 0; the caller fills it in.
    /// </summary>
    public static LcuCredentials? Parse(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        var port = PortRegex().Match(commandLine);
        var token = TokenRegex().Match(commandLine);
        if (!port.Success || !token.Success)
            return null;

        if (!int.TryParse(port.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber) ||
            portNumber is < 1 or > 65535)
        {
            return null;
        }

        return new LcuCredentials(portNumber, token.Groups[1].Value, "https", 0);
    }

    [GeneratedRegex(@"--app-port=(\d+)")]
    private static partial Regex PortRegex();

    [GeneratedRegex(@"--remoting-auth-token=([^\s""]+)")]
    private static partial Regex TokenRegex();
}
