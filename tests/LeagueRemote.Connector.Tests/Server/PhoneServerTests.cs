using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using LeagueRemote.Connector.Https;
using LeagueRemote.Connector.Pairing;
using LeagueRemote.Connector.Server;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Smoke test of the phone server on real Kestrel sockets: loopback only and ports chosen by the OS (never the app's
/// 5080/5443), certificates and pairings in a temp directory.
/// </summary>
public sealed class PhoneServerTests : IAsyncLifetime
{
    private PhoneServerHost _host = null!;
    private ServerCertificateProvider _certificates = null!;
    private X509Certificate2 _ca = null!;
    private readonly List<X509Certificate2> _presented = [];

    public async Task InitializeAsync()
    {
        _host = await PhoneServerHost.StartAsync();
        _certificates = _host.Get<ServerCertificateProvider>();
        _ca = X509CertificateLoader.LoadCertificate(_certificates.CaCertificateDer);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        _ca.Dispose();
        foreach (var cert in _presented)
            cert.Dispose();
    }

    /// <summary>Trusts only our local CA (as a phone with the CA installed would) and records what the server presented.</summary>
    private HttpClientHandler TrustingLocalCa() => new()
    {
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
        {
            if (cert is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
                return false;
            lock (_presented)
                _presented.Add(new X509Certificate2(cert));
            return LocalCertificateAuthority.ChainsTo(cert, _ca);
        },
    };

    [Fact]
    public async Task Https_handshake_uses_the_generated_certificate_and_serves_the_embedded_pwa()
    {
        using var client = new HttpClient(TrustingLocalCa());

        using var index = await client.GetAsync(new Uri(_host.Https, "/"));
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal("text/html", index.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"app\"", await index.Content.ReadAsStringAsync());

        lock (_presented)
            Assert.Equal(_certificates.Server.Thumbprint, Assert.Single(_presented.DistinctBy(c => c.Thumbprint)).Thumbprint);

        // SPA fallback (deep link / client routes) → index.html.
        using var deepLink = await client.GetAsync(new Uri(_host.Https, "/some/route?pair=123456"));
        Assert.Equal(HttpStatusCode.OK, deepLink.StatusCode);
        Assert.Equal("text/html", deepLink.Content.Headers.ContentType?.MediaType);

        using var manifest = await client.GetAsync(new Uri(_host.Https, "/manifest.webmanifest"));
        Assert.Equal("application/manifest+json", manifest.Content.Headers.ContentType?.MediaType);

        var wasm = EmbeddedPwa.FileProvider.GetDirectoryContents("assets").Single(f => f.Name.EndsWith(".wasm", StringComparison.Ordinal));
        using var wasmResponse = await client.GetAsync(new Uri(_host.Https, "/assets/" + wasm.Name));
        Assert.Equal(HttpStatusCode.OK, wasmResponse.StatusCode);
        Assert.Equal("application/wasm", wasmResponse.Content.Headers.ContentType?.MediaType);

        using var health = await client.GetAsync(new Uri(_host.Https, "/healthz"));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Licenses_are_served_as_plain_text_from_the_exe()
    {
        using var client = new HttpClient(TrustingLocalCa());

        using var licenses = await client.GetAsync(new Uri(_host.Https, Licenses.Path));

        Assert.Equal(HttpStatusCode.OK, licenses.StatusCode);
        Assert.Equal("text/plain", licenses.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Header(licenses, "Content-Security-Policy"));
        var text = await licenses.Content.ReadAsStringAsync();
        Assert.StartsWith("MIT License", text);
        Assert.Contains("# Third-party notices", text);
        Assert.Contains("SIL OPEN FONT LICENSE", text);
    }

    [Fact]
    public async Task Http_port_serves_setup_and_ca_and_lets_loopback_through()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        using var setup = await client.GetAsync(new Uri(_host.Http, "/setup"));
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Contains($"https://127.0.0.1:{_host.Https.Port}/", await setup.Content.ReadAsStringAsync());

        using var ca = await client.GetAsync(new Uri(_host.Http, "/league-remote-ca.crt"));
        Assert.Equal("application/x-x509-ca-cert", ca.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_certificates.CaCertificateDer, await ca.Content.ReadAsByteArrayAsync());

        // Loopback (a browser on this PC, the Vite dev proxy) is not redirected.
        using var index = await client.GetAsync(new Uri(_host.Http, "/"));
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
    }

    [Fact]
    public async Task Every_response_carries_the_security_headers()
    {
        using var https = new HttpClient(TrustingLocalCa());
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        http.DefaultRequestHeaders.Add(PhoneServerHost.ClientIpHeader, "192.168.1.50");

        using var index = await https.GetAsync(new Uri(_host.Https, "/"));
        using var health = await https.GetAsync(new Uri(_host.Https, "/healthz"));
        using var setup = await http.GetAsync(new Uri(_host.Http, "/setup"));
        using var redirect = await http.GetAsync(new Uri(_host.Http, "/"));
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);

        foreach (var response in (HttpResponseMessage[])[index, health, setup, redirect])
        {
            Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Header(response, "Content-Security-Policy"));
            Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
            Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
            Assert.Equal("DENY", Header(response, "X-Frame-Options"));
            Assert.StartsWith("camera=(self)", Header(response, "Permissions-Policy"));
            Assert.False(response.Headers.Contains("Server"));
        }

        // What the PWA needs: its own scripts and the QR decoder's wasm, Data Dragon art and JSON, the hub's WebSocket,
        // the generated silent audio clip. Nothing inline but styles, nothing framed.
        var csp = SecurityHeaders.ContentSecurityPolicy;
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval';", csp);
        Assert.Contains("img-src 'self' blob: https://ddragon.leagueoflegends.com;", csp);
        Assert.Contains("connect-src 'self' wss: https://ddragon.leagueoflegends.com;", csp);
        Assert.Contains("media-src 'self' blob:;", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-eval'", csp.Replace("wasm-unsafe-eval", ""));
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues(name));

    [Fact]
    public async Task A_phone_pairs_over_https_trusting_only_the_local_ca()
    {
        await using var phone = new HubConnectionBuilder()
            .WithUrl(new Uri(_host.Https, HubMethods.Path), o =>
            {
                o.HttpMessageHandlerFactory = _ => TrustingLocalCa();
                o.WebSocketConfiguration = ws => ws.RemoteCertificateValidationCallback =
                    (_, cert, _, errors) => cert is X509Certificate2 leaf && LocalCertificateAuthority.ChainsTo(leaf, _ca)
                                            && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None;
            })
            .Build();
        await phone.StartAsync();

        var grant = await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, _host.Pairing.Code);

        Assert.Equal(_host.Pairing.PairingId, grant.PairingId);
        Assert.True(File.Exists(Path.Combine(_host.DataFolder, PhonePairing.FileName)));
    }
}

public class EmbeddedPwaTests
{
    [Fact]
    public void Connector_assembly_embeds_the_built_pwa()
    {
        var resources = typeof(EmbeddedPwa).Assembly.GetManifestResourceNames();
        Assert.Contains("Microsoft.Extensions.FileProviders.Embedded.Manifest.xml", resources);

        Assert.True(EmbeddedPwa.IsAvailable);
        foreach (var file in (string[])["index.html", "manifest.webmanifest", "sw.js", "icon-192.png"])
            Assert.True(EmbeddedPwa.FileProvider.GetFileInfo(file).Exists, file);
        var assets = EmbeddedPwa.FileProvider.GetDirectoryContents("assets").Select(f => f.Name).ToList();
        Assert.Contains(assets, n => n.EndsWith(".js", StringComparison.Ordinal));
        Assert.Contains(assets, n => n.EndsWith(".wasm", StringComparison.Ordinal));
    }
}
