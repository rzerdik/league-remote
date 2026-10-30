using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;

namespace LeagueRemote.Connector.Https;

/// <summary>
/// The phone server's live HTTPS server certificate. Kestrel asks <see cref="GetCertificate"/> on every handshake (its
/// <c>ServerCertificateSelector</c>); that returns the cached certificate unless it was flagged stale, in which case the PC's
/// current addresses are re-read and the certificate is re-issued (via <see cref="CertificateStore"/>) when they aren't all
/// covered or it is about to expire. It is flagged stale on <see cref="NetworkChange.NetworkAddressChanged"/> (refreshed in the
/// background right away) and when the renewal date passes. So an exe started before Wi-Fi got its address, or a PC whose
/// address changed, serves a certificate matching the address in the pairing QR without a restart.
/// </summary>
internal sealed class ServerCertificateProvider : IDisposable
{
    private readonly CertificateStore _store;
    private readonly Func<IReadOnlyCollection<IPAddress>> _addresses;
    private readonly IReadOnlyCollection<string> _dnsNames;
    private readonly TimeProvider _time;
    private readonly ILogger? _logger;
    private readonly bool _watchNetwork;

    private readonly Lock _refreshLock = new();
    // Replaced certificates stay alive until disposal: a handshake may still be using one.
    private readonly List<HttpsCertificates> _retired = [];
    private volatile HttpsCertificates _current;
    private volatile int _stale;
    private DateTimeOffset _renewAt;
    private bool _disposed;

    /// <param name="store">Persists the CA and server certificate.</param>
    /// <param name="addresses">This PC's current addresses to cover (only ones the name-constrained CA may vouch for).</param>
    /// <param name="dnsNames">DNS names to cover.</param>
    /// <param name="time">Clock.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="watchNetwork">Subscribe to <see cref="NetworkChange.NetworkAddressChanged"/> (off in tests).</param>
    public ServerCertificateProvider(
        CertificateStore store,
        Func<IReadOnlyCollection<IPAddress>> addresses,
        IReadOnlyCollection<string> dnsNames,
        TimeProvider time,
        ILogger? logger = null,
        bool watchNetwork = true)
    {
        _store = store;
        _addresses = addresses;
        _dnsNames = dnsNames;
        _time = time;
        _logger = logger;
        _watchNetwork = watchNetwork;

        _current = store.EnsureCertificates(addresses(), dnsNames);
        _renewAt = RenewAt(_current.Server);
        if (watchNetwork)
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    /// <summary>The certificate currently served.</summary>
    public X509Certificate2 Server => _current.Server;

    /// <summary>The local CA's public certificate (DER). Never changes while the CA is valid.</summary>
    public byte[] CaCertificateDer => _current.CaCertificateDer;

    /// <summary>Kestrel's certificate selector: cheap unless the certificate was flagged stale.</summary>
    public X509Certificate2 GetCertificate()
    {
        if (_stale != 0 || _time.GetUtcNow() >= _renewAt)
        {
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                // Never fail the handshake over a refresh problem: keep serving what we have.
                _logger?.LogWarning(ex, "Could not refresh the HTTPS certificate");
            }
        }

        return _current.Server;
    }

    /// <summary>Flags the certificate stale: the next <see cref="GetCertificate"/> re-checks the addresses.</summary>
    public void Invalidate() => _stale = 1;

    /// <summary>
    /// Re-reads the addresses and re-issues the certificate if it doesn't cover them all or is due for renewal. Thread-safe.
    /// Returns true when the served certificate changed.
    /// </summary>
    public bool Refresh()
    {
        lock (_refreshLock)
        {
            if (_disposed)
                return false;

            // Cleared before reading the addresses: a change that lands while we work flags it again.
            _stale = 0;
            var addresses = _addresses();
            var current = _current;
            if (_time.GetUtcNow() < _renewAt && LocalCertificateAuthority.Covers(current.Server, addresses, _dnsNames))
                return false;

            var next = _store.EnsureCertificates(addresses, _dnsNames);
            if (next.Server.Thumbprint == current.Server.Thumbprint)
            {
                next.Dispose();
                // Nothing new was issued: don't retry on every handshake.
                var retry = _time.GetUtcNow() + TimeSpan.FromHours(1);
                _renewAt = RenewAt(current.Server) > retry ? RenewAt(current.Server) : retry;
                return false;
            }

            _retired.Add(current);
            _current = next;
            _renewAt = RenewAt(next.Server);
            _logger?.LogInformation("HTTPS certificate now covers {Addresses}", string.Join(", ", addresses));
            return true;
        }
    }

    public void Dispose()
    {
        if (_watchNetwork)
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

        lock (_refreshLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var retired in _retired)
                retired.Dispose();
            _retired.Clear();
            _current.Dispose();
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Invalidate();
        // Re-issue now (RSA key generation) rather than during a phone's handshake; the selector still re-checks lazily.
        _ = Task.Run(() =>
        {
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not refresh the HTTPS certificate after a network change");
            }
        });
    }

    private static DateTimeOffset RenewAt(X509Certificate2 server) =>
        new DateTimeOffset(server.NotAfter.ToUniversalTime(), TimeSpan.Zero) - LocalCertificateAuthority.RenewBefore;
}
