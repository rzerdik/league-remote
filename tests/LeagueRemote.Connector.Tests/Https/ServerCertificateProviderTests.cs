using System.Net;
using System.Security.Cryptography.X509Certificates;
using LeagueRemote.Connector.Https;
using LeagueRemote.Connector.Pairing;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class ServerCertificateProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static IPAddress[] Covered(X509Certificate2 cert) =>
        [.. cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single().EnumerateIPAddresses()];

    [Fact]
    public void Address_change_reissues_the_certificate_on_the_next_handshake()
    {
        // Started before Wi-Fi had an address: only loopback.
        IPAddress[] current = [IPAddress.Loopback];
        var time = new FakeTimeProvider(Now);
        using var provider = new ServerCertificateProvider(
            new CertificateStore(_dir, time), () => current, ["localhost"], time, watchNetwork: false);

        var initial = provider.GetCertificate();
        Assert.Equal([IPAddress.Loopback], Covered(initial));

        // Cheap path: nothing flagged, the cached certificate is returned without re-reading the addresses.
        current = [IPAddress.Parse("192.168.1.20"), IPAddress.Loopback];
        Assert.Same(initial, provider.GetCertificate());

        // NetworkAddressChanged → Invalidate: the next handshake gets a certificate for the new address, same CA.
        var caDer = provider.CaCertificateDer;
        provider.Invalidate();
        var reissued = provider.GetCertificate();
        Assert.NotEqual(initial.Thumbprint, reissued.Thumbprint);
        Assert.Contains(IPAddress.Parse("192.168.1.20"), Covered(reissued));
        Assert.Equal(caDer, provider.CaCertificateDer);
        using var ca = X509CertificateLoader.LoadCertificate(caDer);
        Assert.True(LocalCertificateAuthority.ChainsTo(reissued, ca));

        // A change that is still covered keeps the certificate.
        current = [IPAddress.Parse("192.168.1.20")];
        provider.Invalidate();
        Assert.Same(reissued, provider.GetCertificate());
    }

    [Fact]
    public void Certificate_is_renewed_when_it_nears_expiry_without_a_network_change()
    {
        var time = new FakeTimeProvider(Now);
        using var provider = new ServerCertificateProvider(
            new CertificateStore(_dir, time), () => [IPAddress.Loopback], ["localhost"], time, watchNetwork: false);

        var initial = provider.GetCertificate();
        time.Advance(TimeSpan.FromDays(370)); // < 30 days left of 397
        var renewed = provider.GetCertificate();

        Assert.NotEqual(initial.Thumbprint, renewed.Thumbprint);
        Assert.True(renewed.NotAfter > initial.NotAfter);
    }

    [Fact]
    public void Primary_address_prefers_ranges_the_local_ca_may_vouch_for()
    {
        Assert.Equal(IPAddress.Parse("192.168.1.20"),
            LanAddress.PickPreferred([IPAddress.Parse("100.64.3.4"), IPAddress.Parse("192.168.1.20")]));
        Assert.Null(LanAddress.PickPreferred([IPAddress.Parse("100.64.3.4")]));
    }
}
