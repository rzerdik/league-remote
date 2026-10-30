using System.Net;
using LeagueRemote.Connector.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace LeagueRemote.Connector.Tests;

public sealed class LanHttpMiddlewareTests : IAsyncLifetime
{
    private static readonly byte[] CaDer = [0x30, 0x82, 0x01, 0x02, 0x03];
    private static readonly IPAddress Phone = IPAddress.Parse("192.168.1.50");

    private IHost _host = null!;
    private TestServer _server = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseMiddleware<LanHttpMiddleware>(CaDer, (Func<int>)(() => 5443));
                    app.Run(ctx => ctx.Response.WriteAsync("app"));
                }))
            .StartAsync();
        _server = _host.GetTestServer();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private Task<HttpContext> Send(string path, IPAddress? remote, string scheme = "http", string query = "", string method = "GET",
        string host = "192.168.1.20:5080", IPAddress? local = null) =>
        _server.SendAsync(ctx =>
        {
            ctx.Request.Method = method;
            ctx.Request.Scheme = scheme;
            ctx.Request.Host = new HostString(host);
            ctx.Request.Path = path;
            ctx.Request.QueryString = new QueryString(query);
            ctx.Connection.RemoteIpAddress = remote;
            ctx.Connection.LocalIpAddress = local;
        });

    private static string Body(HttpContext ctx) => new StreamReader(ctx.Response.Body).ReadToEnd();

    [Theory]
    [InlineData("/", "", "https://192.168.1.20:5443/")]
    [InlineData("/", "?pair=123456", "https://192.168.1.20:5443/?pair=123456")]
    [InlineData("/assets/app.js", "?v=1&x=%20y", "https://192.168.1.20:5443/assets/app.js?v=1&x=%20y")]
    [InlineData("/hub/negotiate", "?negotiateVersion=1", "https://192.168.1.20:5443/hub/negotiate?negotiateVersion=1")]
    public async Task Plain_http_from_the_lan_is_redirected_to_https_on_the_same_host_keeping_path_and_query(
        string path, string query, string expected)
    {
        var ctx = await Send(path, Phone, query: query);

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, ctx.Response.StatusCode);
        Assert.Equal(expected, ctx.Response.Headers.Location.ToString());
        Assert.False(ctx.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData("attacker.example")] // a forged Host header is not reflected
    [InlineData("8.8.8.8:5080")] // a public address the certificate could never cover
    [InlineData("")]
    public async Task A_host_the_certificate_cannot_cover_is_replaced_by_the_address_the_request_came_in_on(string host)
    {
        var ctx = await Send("/", Phone, host: host, local: IPAddress.Parse("192.168.1.20"));

        Assert.Equal("https://192.168.1.20:5443/", ctx.Response.Headers.Location.ToString());

        var setup = Body(await Send("/setup", Phone, host: host, local: IPAddress.Parse("::ffff:10.0.0.7")));
        Assert.Contains("href=\"https://10.0.0.7:5443/\"", setup);
    }

    [Theory]
    [InlineData("localhost:5080", "https://localhost:5443/")]
    [InlineData("10.1.2.3", "https://10.1.2.3:5443/")]
    [InlineData("[::1]:5080", "https://[::1]:5443/")]
    public async Task A_private_address_or_localhost_in_the_host_header_is_kept(string host, string expected)
    {
        var ctx = await Send("/", Phone, host: host);

        Assert.Equal(expected, ctx.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Without_a_usable_host_or_local_address_the_machine_name_is_used()
    {
        var ctx = await Send("/", Phone, host: "attacker.example");

        Assert.Equal($"https://{Environment.MachineName}:5443/", ctx.Response.Headers.Location.ToString(), ignoreCase: true);
    }

    [Fact]
    public async Task Redirect_keeps_the_method_for_posts() // 307, not 302
    {
        var ctx = await Send("/hub/negotiate", Phone, method: "POST");
        Assert.Equal(StatusCodes.Status307TemporaryRedirect, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Loopback_is_not_redirected(string remote)
    {
        var ctx = await Send("/hub", IPAddress.Parse(remote));

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal("app", Body(ctx));
    }

    [Fact]
    public async Task Https_requests_pass_through()
    {
        var ctx = await Send("/", Phone, scheme: "https");

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal("app", Body(ctx));
    }

    [Fact]
    public async Task Setup_page_is_served_over_http_and_links_to_the_https_app()
    {
        var ctx = await Send("/setup", Phone, query: "?pair=012345");

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.StartsWith("text/html", ctx.Response.ContentType);
        var html = Body(ctx);
        Assert.Contains("href=\"https://192.168.1.20:5443/?pair=012345\"", html);
        Assert.Contains("href=\"/league-remote-ca.crt\"", html);
        Assert.Contains("Certificate Trust Settings", html);
        Assert.Contains("Install a certificate", html);
        Assert.Contains("not endorsed by Riot Games", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task Setup_page_ignores_a_pair_parameter_that_is_not_a_code()
    {
        var html = Body(await Send("/setup", Phone, query: "?pair=%22%3E%3Cscript%3E"));

        Assert.Contains("href=\"https://192.168.1.20:5443/\"", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task Ca_certificate_is_served_as_der_and_not_redirected()
    {
        var ctx = await Send(LanHttpMiddleware.CaCertificatePath, Phone);

        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.Equal("application/x-x509-ca-cert", ctx.Response.ContentType);
        var body = new MemoryStream();
        await ctx.Response.Body.CopyToAsync(body);
        Assert.Equal(CaDer, body.ToArray());
    }
}
