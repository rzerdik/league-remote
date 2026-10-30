namespace LeagueRemote.Connector.Tray;

/// <summary>Shared, observable status for the tray UI (and logs). Events fire on background threads.</summary>
internal sealed class ConnectorStatus
{
    private readonly Lock _lock = new();

    public string LcuStatus { get; private set; } = "Waiting for League client…";

    /// <summary>Phones paired with this PC.</summary>
    public int PairedPhones { get; private set; }

    /// <summary>Paired phones signed in right now.</summary>
    public int ConnectedPhones { get; private set; }

    public bool IsPaired => PairedPhones > 0;

    /// <summary>"No phone paired", "1 phone paired, connected", "2 phones paired, 1 connected".</summary>
    public string PhonesStatus => (PairedPhones, ConnectedPhones) switch
    {
        (0, _) => "No phone paired",
        (1, > 0) => "1 phone paired, connected",
        (1, _) => "1 phone paired",
        (var paired, var connected) => $"{paired} phones paired, {connected} connected",
    };

    /// <summary>The pairing code (it pairs the first phone and adds more); null until the server started.</summary>
    public string? PairingCode { get; private set; }

    /// <summary>Deep link encoded in the pairing QR (<c>{pwaUrl}/?pair={code}</c>); null without a code.</summary>
    public string? PairingLink { get; private set; }

    /// <summary>URL phones open (the PWA base, e.g. <c>https://192.168.1.20:5443</c>); null until known.</summary>
    public string? AppUrl { get; private set; }

    /// <summary>First-time phone setup page (<c>http://{ip}:5080/setup</c>); null until known.</summary>
    public string? SetupUrl { get; private set; }

    /// <summary>SHA-256 fingerprint of the local CA, to compare with what the phone shows before trusting it.</summary>
    public string? CaFingerprint { get; private set; }

    /// <summary>Fatal problem of the phone server (e.g. port in use); null when fine.</summary>
    public string? ServerError { get; private set; }

    public event Action? Changed;

    /// <summary>Raised when the user picks "Unpair All Phones" in the tray.</summary>
    public event Action? UnpairRequested;

    public void SetLcuStatus(string text) => Update(() => LcuStatus = text);

    public void SetPhones(int paired, int connected) => Update(() =>
    {
        PairedPhones = paired;
        ConnectedPhones = connected;
    });

    public void SetPairingCode(string? code, string? link) => Update(() =>
    {
        PairingCode = code;
        PairingLink = code is null ? null : link;
    });

    public void SetEndpoints(string? appUrl, string? setupUrl) => Update(() =>
    {
        AppUrl = appUrl;
        SetupUrl = setupUrl;
    });

    public void SetCaFingerprint(string? fingerprint) => Update(() => CaFingerprint = fingerprint);

    public void SetServerError(string? error) => Update(() => ServerError = error);

    public void RequestUnpair() => UnpairRequested?.Invoke();

    private void Update(Action change)
    {
        lock (_lock)
            change();
        Changed?.Invoke();
    }
}
