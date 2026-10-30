using System.Net.NetworkInformation;
using LeagueRemote.Connector.Https;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.Server;
using LeagueRemote.Connector.Tray;
using Microsoft.Extensions.Options;

namespace LeagueRemote.Connector.Pairing;

/// <summary>
/// Keeps the tray's pairing view current (code, QR link, phone app and setup URLs, how many phones are paired and connected,
/// the CA fingerprint) and carries out the tray's "Unpair All Phones".
/// </summary>
internal sealed class PairingStatusService(
    PhonePairing pairing,
    PhoneConnections connections,
    PushSubscriptionStore subscriptions,
    PushNotifier notifier,
    ServerCertificateProvider certificates,
    IOptions<ServerOptions> options,
    ConnectorStatus status,
    ILogger<PairingStatusService> logger,
    ILoggerFactory loggerFactory) : IHostedService
{
    /// <summary>
    /// Own category for the line with the pairing code, so the log file can leave it out (the file outlives the code and
    /// ends up in bug reports) while a headless console still shows it.
    /// </summary>
    public const string CodeCategory = "LeagueRemote.Connector.Pairing.PairingCode";

    private readonly ILogger _codeLogger = loggerFactory.CreateLogger(CodeCategory);
    private readonly Lock _lock = new();
    private string? _loggedCode;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        status.SetCaFingerprint(LocalCertificateAuthority.Fingerprint(certificates.CaCertificateDer));
        pairing.Changed += Refresh;
        connections.Changed += RefreshPhones;
        status.UnpairRequested += UnpairAll;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        Refresh();

        var endpoints = ResolveEndpoints();
        logger.LogInformation("Phones open {AppUrl}; first time on a phone, set it up at {SetupUrl}", endpoints.PwaBase, endpoints.SetupUrl);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        status.UnpairRequested -= UnpairAll;
        connections.Changed -= RefreshPhones;
        pairing.Changed -= Refresh;
        return Task.CompletedTask;
    }

    /// <summary>The LAN address may have changed (Wi-Fi up after start, new DHCP lease): the QR link must follow it.</summary>
    private void OnNetworkAddressChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var endpoints = ResolveEndpoints();
        var code = pairing.Code;
        var link = PairingLink.DeepLink(endpoints.PwaBase, code);
        status.SetEndpoints(endpoints.PwaBase, endpoints.SetupUrl);
        status.SetPairingCode(code, link);
        RefreshPhones();

        lock (_lock)
        {
            if (code == _loggedCode)
                return;
            _loggedCode = code;
        }

        _codeLogger.LogInformation("Pairing code {Code} — open {Link} on your phone", code, link);
    }

    private void RefreshPhones() => status.SetPhones(pairing.PhoneCount, connections.SignedInCount);

    private ServerEndpoints ResolveEndpoints() => ServerEndpoints.Resolve(options.Value, LanAddress.GetPrimaryIPv4);

    /// <summary>
    /// Every phone token goes (with a new pairing id and code), then their push subscriptions, and every phone is
    /// disconnected; its next Authenticate gets "Unknown or revoked token." and the phone asks to pair again.
    /// </summary>
    private void UnpairAll()
    {
        try
        {
            var removed = pairing.RevokeAll();
            subscriptions.Clear();
            notifier.Forget();
            connections.Abort(_ => true);
            logger.LogInformation("Unpaired all phones ({Count})", removed.Count);
        }
        catch (PairingStoreException ex)
        {
            logger.LogError(ex, "Could not unpair the phones");
        }
    }
}
