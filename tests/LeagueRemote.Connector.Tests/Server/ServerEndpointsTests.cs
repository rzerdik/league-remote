using LeagueRemote.Connector.Server;

namespace LeagueRemote.Connector.Tests;

public sealed class ServerEndpointsTests
{
    private static string? Lan() => "192.168.1.20";

    [Fact]
    public void Phones_open_https_and_the_setup_page_on_the_lan_ip()
    {
        var endpoints = ServerEndpoints.Resolve(new ServerOptions(), Lan);

        Assert.Equal("https://192.168.1.20:5443", endpoints.PwaBase);
        Assert.Equal("http://192.168.1.20:5080/setup", endpoints.SetupUrl);
    }

    [Fact]
    public void Ports_and_the_pwa_url_override_are_honoured()
    {
        var endpoints = ServerEndpoints.Resolve(new ServerOptions { HttpPort = 8080, HttpsPort = 8443, PwaUrl = "https://pc.lan:9443/" }, Lan);

        Assert.Equal("https://pc.lan:9443", endpoints.PwaBase);
        Assert.Equal("http://192.168.1.20:8080/setup", endpoints.SetupUrl);
    }
}
