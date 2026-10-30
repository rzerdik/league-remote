using LeagueRemote.Connector.Pairing;

namespace LeagueRemote.Connector.Server;

/// <summary>What the phone opens: the PWA (<see cref="PwaBase"/>) and the first-time setup page. Pure: the LAN address is passed in.</summary>
internal sealed record ServerEndpoints(string PwaBase, string SetupUrl)
{
    public static ServerEndpoints Resolve(ServerOptions options, Func<string?> lanIpv4) =>
        new(
            PairingLink.ResolvePwaBase(options.PwaUrl, $"https://localhost:{options.HttpsPort}", lanIpv4),
            PairingLink.ResolvePwaBase(null, $"http://localhost:{options.HttpPort}", lanIpv4) + LanHttpMiddleware.SetupPath);
}
