using System.Net;
using LeagueRemote.Connector.ClientSounds;
using Microsoft.Extensions.DependencyInjection;

namespace LeagueRemote.Connector.Tests;

public sealed class RankEmblemTests : IDisposable
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 1, 2, 3];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));

    private string Install => Path.Combine(_dir, "League of Legends");

    private string Wad => Path.Combine(Install, "Plugins", "rcp-fe-lol-static-assets", "assets.wad");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static string Entry(string tier) => $"plugins/rcp-fe-lol-static-assets/global/default/images/ranked-emblem/emblem-{tier}.png";

    [Fact]
    public void A_listed_tier_comes_from_the_plugin_wad()
    {
        FakeWad.Write(Wad, (Entry("gold"), Png, false));

        Assert.Equal(Png, new RankEmblemLibrary(Install).Get("gold.png"));
    }

    [Theory]
    [InlineData("silver.png")] // not in this install
    [InlineData("nope.png")] // not a tier, even with such an entry
    [InlineData("GOLD.png")]
    [InlineData("gold.jpg")]
    [InlineData("..gold.png")]
    public void Anything_else_is_not_served(string file)
    {
        FakeWad.Write(Wad, (Entry("gold"), Png, false), (Entry("nope"), Png, false));

        Assert.Null(new RankEmblemLibrary(Install).Get(file));
    }

    [Fact]
    public void An_entry_that_is_not_a_png_is_not_served()
    {
        FakeWad.Write(Wad, (Entry("gold"), FakeWad.Ogg, false));

        Assert.Null(new RankEmblemLibrary(Install).Get("gold.png"));
    }

    [Fact]
    public void No_install_means_no_emblem()
    {
        Assert.Null(new RankEmblemLibrary(Install).Get("gold.png"));
    }

    [Fact]
    public void A_client_patch_replaces_the_cached_emblem()
    {
        FakeWad.Write(Wad, (Entry("gold"), Png, false));
        var library = new RankEmblemLibrary(Install);
        Assert.Equal(Png, library.Get("gold.png"));

        byte[] patched = [.. Png, 4, 5];
        FakeWad.Write(Wad, (Entry("gold"), patched, false));

        Assert.Equal(patched, library.Get("gold.png"));
    }

    [Fact]
    public async Task Phone_server_serves_the_emblems()
    {
        FakeWad.Write(Wad, (Entry("gold"), Png, false));
        await using var server = await PhoneServerHost.StartAsync(
            Path.Combine(_dir, "server"), services: s => s.AddSingleton(new RankEmblemLibrary(Install)));
        using var client = new HttpClient { BaseAddress = server.Http }; // loopback: plain HTTP passes the LAN middleware

        using var emblem = await client.GetAsync("/rank-emblems/gold.png");

        Assert.Equal(HttpStatusCode.OK, emblem.StatusCode);
        Assert.Equal("image/png", emblem.Content.Headers.ContentType?.MediaType);
        Assert.Contains("max-age", emblem.Headers.CacheControl?.ToString());
        Assert.Equal(Png, await emblem.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/rank-emblems/silver.png")).StatusCode);
    }
}
