using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LeagueRemote.Connector.Https;

/// <summary>
/// Pure certificate factory for the phone server's HTTPS: a long-lived local root CA (installed once on the phone) and
/// short-lived server certificates it signs for this PC's LAN addresses. No I/O; persistence is <see cref="CertificateStore"/>.
/// </summary>
internal static class LocalCertificateAuthority
{
    public const string CaCommonName = "League Remote local CA";
    public const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    public static readonly TimeSpan CaValidity = TimeSpan.FromDays(3650);

    /// <summary>397 days: what Apple accepts for TLS server certificates (and well under the 825-day cap for private roots).</summary>
    public static readonly TimeSpan ServerValidity = TimeSpan.FromDays(397);

    /// <summary>Re-issue the server certificate when it expires within this window.</summary>
    public static readonly TimeSpan RenewBefore = TimeSpan.FromDays(30);

    /// <summary>Backdating, so a phone whose clock is slightly behind still accepts a freshly issued certificate.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromHours(1);

    public const string NameConstraintsOid = "2.5.29.30";

    /// <summary>
    /// IPv4 ranges the CA may vouch for (RFC 1918 private networks, loopback, link-local). Together with
    /// <see cref="PermittedDnsNames"/> this is a critical nameConstraints extension: even if the CA key leaked, a phone that
    /// trusts the CA would reject certificates it signed for any public site.
    /// </summary>
    public static readonly IReadOnlyList<(IPAddress Network, int PrefixLength)> PermittedIpRanges =
    [
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
    ];

    public static readonly IReadOnlyList<string> PermittedDnsNames = ["localhost"];

    /// <summary>Root CA: RSA 2048, SHA-256, 10 years, basicConstraints CA:true (pathlen 0), keyCertSign + cRLSign, name-constrained.</summary>
    public static X509Certificate2 CreateRootCa(DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={CaCommonName}, O=League Remote"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509Extension(NameConstraintsOid, EncodeNameConstraints(), critical: true));

        var notBefore = now - ClockSkew;
        return request.CreateSelfSigned(notBefore, notBefore + CaValidity);
    }

    /// <summary>
    /// Server certificate signed by <paramref name="ca"/> (which must have its private key): RSA 2048, SHA-256, EKU serverAuth,
    /// SANs = <paramref name="ipAddresses"/> + <paramref name="dnsNames"/>, valid <see cref="ServerValidity"/> (clamped to the CA).
    /// </summary>
    public static X509Certificate2 IssueServerCertificate(
        X509Certificate2 ca, IEnumerable<IPAddress> ipAddresses, IEnumerable<string> dnsNames, DateTimeOffset now)
    {
        var ips = ipAddresses.Distinct().ToList();
        var names = dnsNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ips.Count == 0 && names.Count == 0)
            throw new ArgumentException("At least one IP address or DNS name is required.");

        using var key = RSA.Create(2048);
        var commonName = names.FirstOrDefault() ?? ips[0].ToString();
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}, O=League Remote"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthOid)], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));

        var san = new SubjectAlternativeNameBuilder();
        foreach (var ip in ips)
            san.AddIpAddress(ip);
        foreach (var name in names)
            san.AddDnsName(name);
        request.CertificateExtensions.Add(san.Build());

        var notBefore = now - ClockSkew;
        var notAfter = notBefore + ServerValidity;
        if (notAfter > ca.NotAfter)
            notAfter = ca.NotAfter;

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F; // positive
        using var publicOnly = request.Create(ca, notBefore, notAfter, serial);
        return publicOnly.CopyWithPrivateKey(key);
    }

    /// <summary>
    /// True when the server certificate must be (re-)issued: missing, not signed by <paramref name="ca"/>, expiring within
    /// <see cref="RenewBefore"/>, or not covering every current address/name.
    /// </summary>
    public static bool NeedsReissue(
        X509Certificate2? server, X509Certificate2 ca, IEnumerable<IPAddress> ipAddresses, IEnumerable<string> dnsNames, DateTimeOffset now)
    {
        if (server is null || !server.HasPrivateKey)
            return true;
        if (server.NotAfter.ToUniversalTime() - RenewBefore <= now.UtcDateTime || server.NotBefore.ToUniversalTime() > now.UtcDateTime)
            return true;
        return !ChainsTo(server, ca) || !Covers(server, ipAddresses, dnsNames);
    }

    /// <summary>True when the certificate's SANs list every one of <paramref name="ipAddresses"/> and <paramref name="dnsNames"/>.</summary>
    public static bool Covers(X509Certificate2 server, IEnumerable<IPAddress> ipAddresses, IEnumerable<string> dnsNames)
    {
        var san = server.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (san is null)
            return false;

        var coveredIps = san.EnumerateIPAddresses().ToHashSet();
        var coveredNames = san.EnumerateDnsNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ipAddresses.All(coveredIps.Contains)
               && dnsNames.Where(n => !string.IsNullOrWhiteSpace(n)).All(coveredNames.Contains);
    }

    /// <summary>SHA-256 of a DER certificate as colon-separated hex — what phones show in the certificate details.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> der) =>
        string.Join(":", SHA256.HashData(der).ToArray().Select(b => b.ToString("X2")));

    /// <summary>True when <paramref name="ca"/> carries a critical nameConstraints extension; a CA without one is replaced.</summary>
    public static bool HasNameConstraints(X509Certificate2 ca) =>
        ca.Extensions[NameConstraintsOid] is { Critical: true };

    /// <summary>True when <paramref name="address"/> is an IPv4 address inside <see cref="PermittedIpRanges"/>.</summary>
    public static bool IsPermitted(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && PermittedIpRanges.Any(r => new IPNetwork(r.Network, r.PrefixLength).Contains(address));

    /// <summary>DER of NameConstraints { permittedSubtrees [0] { iPAddress (address + mask)…, dNSName… } }.</summary>
    private static byte[] EncodeNameConstraints()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            foreach (var (network, prefixLength) in PermittedIpRanges)
            {
                var mask = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(prefixLength == 0 ? 0 : -1 << (32 - prefixLength)));
                using (writer.PushSequence())
                    writer.WriteOctetString([.. network.GetAddressBytes(), .. mask], new Asn1Tag(TagClass.ContextSpecific, 7));
            }

            foreach (var name in PermittedDnsNames)
            {
                using (writer.PushSequence())
                    writer.WriteCharacterString(UniversalTagNumber.IA5String, name, new Asn1Tag(TagClass.ContextSpecific, 2));
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// Builds the chain of <paramref name="leaf"/> with <paramref name="ca"/> as the only trust anchor (no revocation, dates
    /// ignored: <see cref="NeedsReissue"/> checks them itself).
    /// </summary>
    public static bool ChainsTo(X509Certificate2 leaf, X509Certificate2 ca)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
        return chain.Build(leaf)
               && chain.ChainElements.Count == 2
               && chain.ChainElements[^1].Certificate.RawDataMemory.Span.SequenceEqual(ca.RawDataMemory.Span);
    }
}
