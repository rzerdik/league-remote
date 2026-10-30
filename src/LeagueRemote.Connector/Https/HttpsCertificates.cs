using System.Security.Cryptography.X509Certificates;

namespace LeagueRemote.Connector.Https;

/// <summary>The phone server's TLS material: the server certificate for Kestrel and the CA's public certificate (DER).</summary>
internal sealed class HttpsCertificates(X509Certificate2 server, byte[] caCertificateDer) : IDisposable
{
    /// <summary>Server certificate with a persisted (non-ephemeral) private key, usable by Kestrel/SChannel.</summary>
    public X509Certificate2 Server { get; } = server;

    /// <summary>The local CA's public certificate only (DER), served at <c>/league-remote-ca.crt</c>. Never contains the key.</summary>
    public byte[] CaCertificateDer { get; } = caCertificateDer;

    public void Dispose() => Server.Dispose();
}
