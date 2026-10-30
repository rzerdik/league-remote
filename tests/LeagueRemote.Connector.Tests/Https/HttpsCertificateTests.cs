using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LeagueRemote.Connector.Https;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class HttpsCertificateTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");
    private static readonly IPAddress[] Ips = [Lan, IPAddress.Loopback];
    private static readonly string[] Names = ["localhost"];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Root_ca_is_a_ten_year_self_signed_ca_for_signing_certificates()
    {
        using var ca = LocalCertificateAuthority.CreateRootCa(Now);

        Assert.True(ca.HasPrivateKey);
        Assert.Equal(ca.SubjectName.Name, ca.IssuerName.Name);
        Assert.Contains(LocalCertificateAuthority.CaCommonName, ca.Subject);
        var constraints = Assert.Single(ca.Extensions.OfType<X509BasicConstraintsExtension>());
        Assert.True(constraints.CertificateAuthority);
        Assert.True(constraints.Critical);
        var usage = Assert.Single(ca.Extensions.OfType<X509KeyUsageExtension>());
        Assert.True(usage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign));
        Assert.InRange((ca.NotAfter - ca.NotBefore).TotalDays, 3649, 3651);
        Assert.Equal(2048, ca.GetRSAPublicKey()!.KeySize);
    }

    [Fact]
    public void Server_certificate_chains_to_the_ca_and_covers_lan_addresses_for_server_auth()
    {
        using var ca = LocalCertificateAuthority.CreateRootCa(Now);
        using var server = LocalCertificateAuthority.IssueServerCertificate(ca, Ips, Names, Now);

        Assert.True(server.HasPrivateKey);
        Assert.True(LocalCertificateAuthority.ChainsTo(server, ca));
        Assert.Equal(ca.SubjectName.Name, server.IssuerName.Name);

        var san = Assert.Single(server.Extensions.OfType<X509SubjectAlternativeNameExtension>());
        Assert.Equal(Ips.OrderBy(i => i.ToString()), san.EnumerateIPAddresses().OrderBy(i => i.ToString()));
        Assert.Equal(Names.Order(), san.EnumerateDnsNames().Order());

        var eku = Assert.Single(server.Extensions.OfType<X509EnhancedKeyUsageExtension>());
        Assert.Contains(eku.EnhancedKeyUsages.Cast<Oid>(), o => o.Value == LocalCertificateAuthority.ServerAuthOid);
        Assert.False(Assert.Single(server.Extensions.OfType<X509BasicConstraintsExtension>()).CertificateAuthority);
        Assert.True((server.NotAfter - server.NotBefore).TotalDays <= 397);

        // Unrelated CA: no chain.
        using var otherCa = LocalCertificateAuthority.CreateRootCa(Now);
        Assert.False(LocalCertificateAuthority.ChainsTo(server, otherCa));
    }

    [Fact]
    public void Root_ca_is_name_constrained_to_private_addresses_and_localhost()
    {
        using var ca = LocalCertificateAuthority.CreateRootCa(Now);
        Assert.True(LocalCertificateAuthority.HasNameConstraints(ca));

        Assert.True(LocalCertificateAuthority.IsPermitted(IPAddress.Parse("192.168.1.120")));
        Assert.True(LocalCertificateAuthority.IsPermitted(IPAddress.Parse("172.31.255.1")));
        Assert.False(LocalCertificateAuthority.IsPermitted(IPAddress.Parse("172.32.0.1")));
        Assert.False(LocalCertificateAuthority.IsPermitted(IPAddress.Parse("8.8.8.8")));
        Assert.False(LocalCertificateAuthority.IsPermitted(IPAddress.IPv6Loopback));

        // The chain engine enforces the constraint: a certificate for a public name/address does not validate.
        using var forPublicSite = LocalCertificateAuthority.IssueServerCertificate(ca, [IPAddress.Parse("8.8.8.8")], ["example.com"], Now);
        Assert.False(LocalCertificateAuthority.ChainsTo(forPublicSite, ca));
    }

    [Fact]
    public void NeedsReissue_when_missing_expiring_foreign_or_not_covering_current_addresses()
    {
        using var ca = LocalCertificateAuthority.CreateRootCa(Now);
        using var otherCa = LocalCertificateAuthority.CreateRootCa(Now);
        using var server = LocalCertificateAuthority.IssueServerCertificate(ca, Ips, Names, Now);

        Assert.False(LocalCertificateAuthority.NeedsReissue(server, ca, Ips, Names, Now));
        Assert.False(LocalCertificateAuthority.NeedsReissue(server, ca, [Lan], ["localhost"], Now)); // subset is fine
        Assert.True(LocalCertificateAuthority.NeedsReissue(null, ca, Ips, Names, Now));
        Assert.True(LocalCertificateAuthority.NeedsReissue(server, otherCa, Ips, Names, Now));
        Assert.True(LocalCertificateAuthority.NeedsReissue(server, ca, [.. Ips, IPAddress.Parse("10.0.0.5")], Names, Now));
        Assert.True(LocalCertificateAuthority.NeedsReissue(server, ca, Ips, [.. Names, "NEW-NAME"], Now));
        Assert.False(LocalCertificateAuthority.NeedsReissue(server, ca, Ips, Names, Now.AddDays(360)));
        Assert.True(LocalCertificateAuthority.NeedsReissue(server, ca, Ips, Names, Now.AddDays(370))); // < 30 days left
    }

    [Fact]
    public void Store_creates_once_reuses_and_reissues_only_the_server_certificate_when_addresses_change()
    {
        var time = new FakeTimeProvider(Now);
        var store = new CertificateStore(_dir, time);

        byte[] caDer;
        string serverThumbprint;
        using (var first = store.EnsureCertificates(Ips, Names))
        {
            caDer = first.CaCertificateDer;
            serverThumbprint = first.Server.Thumbprint;
            Assert.True(first.Server.HasPrivateKey);
        }

        Assert.True(File.Exists(Path.Combine(_dir, CertificateStore.CaFileName)));
        Assert.True(File.Exists(Path.Combine(_dir, CertificateStore.ServerFileName)));

        using (var second = new CertificateStore(_dir, time).EnsureCertificates(Ips, Names))
        {
            Assert.Equal(caDer, second.CaCertificateDer);
            Assert.Equal(serverThumbprint, second.Server.Thumbprint);
        }

        using (var moved = new CertificateStore(_dir, time).EnsureCertificates([IPAddress.Parse("10.0.0.5"), IPAddress.Loopback], Names))
        {
            Assert.Equal(caDer, moved.CaCertificateDer); // phones keep trusting the same CA
            Assert.NotEqual(serverThumbprint, moved.Server.Thumbprint);
            using var ca = X509CertificateLoader.LoadCertificate(caDer);
            Assert.True(LocalCertificateAuthority.ChainsTo(moved.Server, ca));
        }
    }

    [Fact]
    public void Store_never_exposes_the_ca_key_and_keeps_files_dpapi_protected()
    {
        using var certificates = new CertificateStore(_dir, new FakeTimeProvider(Now)).EnsureCertificates(Ips, Names);

        using var publicCa = X509CertificateLoader.LoadCertificate(certificates.CaCertificateDer);
        Assert.False(publicCa.HasPrivateKey);
        Assert.Contains(LocalCertificateAuthority.CaCommonName, publicCa.Subject);

        // The files on disk are DPAPI blobs, not loadable PKCS#12.
        var raw = File.ReadAllBytes(Path.Combine(_dir, CertificateStore.CaFileName));
        Assert.ThrowsAny<CryptographicException>(() => X509CertificateLoader.LoadPkcs12(raw, null));
    }
}
