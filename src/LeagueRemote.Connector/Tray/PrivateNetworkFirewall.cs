using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LeagueRemote.Connector.Tray;

/// <summary>One Windows Firewall rule, as far as <see cref="PrivateNetworkFirewall"/> needs it.</summary>
/// <param name="Program">The rule's program path, or null/empty when it applies to any program.</param>
internal sealed record FirewallRule(bool Enabled, bool Inbound, bool Allow, int Protocol, string? LocalPorts, int Profiles, string? Program);

/// <summary>
/// Tray › Allow on Private Networks: whether phones on a private network can reach the server's ports, and the one elevated
/// <c>netsh</c> command that opens them. Reading the rules needs no admin (the firewall's COM API); only adding one does.
/// </summary>
internal static class PrivateNetworkFirewall
{
    public const string RuleName = AppShell.Name;

    private const int Tcp = 6;
    private const int AnyProtocol = 256;
    private const int PrivateProfile = 2;
    private const int InboundDirection = 1;
    private const int AllowAction = 1;

    /// <summary>
    /// True when enabled inbound allow rules for TCP on the private profile cover every port, either for any program or for
    /// <paramref name="exePath"/> (what Windows' own "Allow access" prompt creates). Block rules are not considered.
    /// </summary>
    public static bool Allows(IEnumerable<FirewallRule> rules, IReadOnlyCollection<int> ports, string? exePath)
    {
        var open = rules
            .Where(r => r.Enabled && r.Inbound && r.Allow && (r.Protocol is Tcp or AnyProtocol) && (r.Profiles & PrivateProfile) != 0)
            .Where(r => string.IsNullOrEmpty(r.Program)
                        || string.Equals(Environment.ExpandEnvironmentVariables(r.Program), exePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return ports.All(port => open.Any(r => Covers(r.LocalPorts, port)));
    }

    /// <summary>Whether a rule's local ports (<c>*</c>, <c>5080,5443</c>, <c>5000-6000</c>) include <paramref name="port"/>.</summary>
    public static bool Covers(string? localPorts, int port)
    {
        if (string.IsNullOrWhiteSpace(localPorts) || localPorts.Trim() == "*")
            return true;

        foreach (var part in localPorts.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var dash = part.IndexOf('-', StringComparison.Ordinal);
            var match = dash < 0
                ? int.TryParse(part, out var single) && single == port
                : int.TryParse(part[..dash], out var from) && int.TryParse(part[(dash + 1)..], out var to) && port >= from && port <= to;
            if (match)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds rule <see cref="RuleName"/>: inbound, TCP, <paramref name="ports"/>, private profile only. Runs elevated (UAC
    /// prompt); netsh from the system folder, not whatever is on PATH.
    /// </summary>
    public static ProcessStartInfo AddRuleCommand(IEnumerable<int> ports) =>
        new(Path.Combine(Environment.SystemDirectory, "netsh.exe"))
        {
            Arguments = $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP "
                        + $"localport={string.Join(',', ports.Where(p => p > 0).Distinct().Order())} profile=private",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

    /// <summary>The current rules (read-only, no admin), or null when the firewall can't be read.</summary>
    public static IReadOnlyList<FirewallRule>? ReadRules()
    {
        if (Type.GetTypeFromProgID("HNetCfg.FwPolicy2") is not { } policyType)
            return null;

        object? policy = null;
        try
        {
            policy = Activator.CreateInstance(policyType);
            dynamic rules = ((dynamic)policy!).Rules;
            var result = new List<FirewallRule>();
            foreach (var rule in rules)
            {
                result.Add(new FirewallRule(
                    (bool)rule.Enabled,
                    (int)rule.Direction == InboundDirection,
                    (int)rule.Action == AllowAction,
                    (int)rule.Protocol,
                    (string?)rule.LocalPorts,
                    (int)rule.Profiles,
                    (string?)rule.ApplicationName));
                Marshal.FinalReleaseComObject(rule);
            }

            Marshal.FinalReleaseComObject(rules);
            return result;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException
                                       or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return null;
        }
        finally
        {
            if (policy is not null)
                Marshal.FinalReleaseComObject(policy);
        }
    }
}
