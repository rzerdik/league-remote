namespace LeagueRemote.Connector.Server;

/// <summary>Configuration section <c>Server</c>: where phones reach this PC. Keep the defaults in sync with appsettings.json.</summary>
internal sealed class ServerOptions
{
    public const string Section = "Server";

    /// <summary>Plain HTTP: the first-time setup page and CA download for phones, a redirect to HTTPS for everything else.</summary>
    public int HttpPort { get; set; } = 5080;

    /// <summary>HTTPS: the PWA and the hub for phones.</summary>
    public int HttpsPort { get; set; } = 5443;

    /// <summary>URL the phone opens (encoded in the pairing QR). Empty: <c>https://{LAN IP}:{HttpsPort}</c>.</summary>
    public string? PwaUrl { get; set; }
}
