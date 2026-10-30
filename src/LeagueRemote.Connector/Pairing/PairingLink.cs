using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LeagueRemote.Connector.Https;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Pairing;

/// <summary>Builds the PWA base URL and the pairing deep link (<c>{pwaUrl}/?pair=123456</c>) encoded in the QR. Pure.</summary>
internal static class PairingLink
{
    /// <summary>
    /// <paramref name="pwaUrl"/> when set; otherwise <paramref name="serverUrl"/>, with a localhost/loopback host replaced by
    /// <paramref name="lanIpv4"/>() (if it finds one) so a phone on the same network can reach it. No trailing slash.
    /// </summary>
    public static string ResolvePwaBase(string? pwaUrl, string serverUrl, Func<string?> lanIpv4)
    {
        var explicitUrl = !string.IsNullOrWhiteSpace(pwaUrl);
        var uri = new Uri(explicitUrl ? pwaUrl!.Trim() : serverUrl.Trim(), UriKind.Absolute);
        var host = uri.Host;
        if (!explicitUrl && IsLoopback(host) && lanIpv4() is { Length: > 0 } ip)
            host = ip;

        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"{uri.Scheme}://{host}{port}{uri.AbsolutePath.TrimEnd('/')}";
    }

    public static string DeepLink(string pwaBase, string code) =>
        $"{pwaBase.TrimEnd('/')}/?{HubMethods.Server.PairQueryParameter}={Uri.EscapeDataString(code)}";

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}

/// <summary>
/// Finds this PC's primary LAN IPv4: first up, non-loopback, non-virtual interface that has a default gateway. Addresses the
/// local name-constrained CA may vouch for (private ranges) win over others, so the QR link points at an address
/// the HTTPS certificate can actually cover.
/// </summary>
internal static class LanAddress
{
    private static readonly string[] VirtualHints = ["virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "wsl", "docker", "vpn", "tap", "tunnel", "loopback"];

    public static string? GetPrimaryIPv4()
    {
        IPAddress? fallback = null;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
                    || VirtualHints.Any(h => nic.Name.Contains(h, StringComparison.OrdinalIgnoreCase)
                                             || nic.Description.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var props = nic.GetIPProperties();
                var hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                if (!hasGateway)
                    continue;

                var addresses = props.UnicastAddresses
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .ToList();
                if (PickPreferred(addresses) is { } preferred)
                    return preferred.ToString();
                fallback ??= addresses.FirstOrDefault();
            }
        }
        catch (NetworkInformationException)
        {
        }

        return fallback?.ToString();
    }

    /// <summary>First address the local CA may vouch for (<see cref="LocalCertificateAuthority.IsPermitted"/>), or null.</summary>
    internal static IPAddress? PickPreferred(IEnumerable<IPAddress> addresses) =>
        addresses.FirstOrDefault(LocalCertificateAuthority.IsPermitted);

    /// <summary>Every IPv4 address of every up, non-loopback interface (virtual ones included): the HTTPS certificate's SANs.</summary>
    public static IReadOnlyList<IPAddress> GetAllIPv4()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }
}
