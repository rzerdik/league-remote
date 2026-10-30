using LeagueRemote.Connector.Pairing;

namespace LeagueRemote.Connector.Tests;

public sealed class PairingLinkTests
{
    private static string? Lan() => "192.168.1.20";

    [Theory]
    [InlineData(null, "http://localhost:5080", "http://192.168.1.20:5080")]
    [InlineData("", "http://127.0.0.1:5080/", "http://192.168.1.20:5080")]
    [InlineData(null, "https://remote.example.com", "https://remote.example.com")]
    [InlineData("https://192.168.1.20:5443/", "http://localhost:5080", "https://192.168.1.20:5443")]
    [InlineData("http://localhost:5173", "http://localhost:5080", "http://localhost:5173")] // explicit PwaUrl is kept as is
    [InlineData("https://example.com/app/", "http://localhost:5080", "https://example.com/app")]
    public void ResolvePwaBase(string? pwaUrl, string serverUrl, string expected) =>
        Assert.Equal(expected, PairingLink.ResolvePwaBase(pwaUrl, serverUrl, Lan));

    [Fact]
    public void ResolvePwaBase_keeps_localhost_when_no_lan_address() =>
        Assert.Equal("http://localhost:5080", PairingLink.ResolvePwaBase(null, "http://localhost:5080", () => null));

    [Fact]
    public void DeepLink_carries_code_in_pair_query_parameter() =>
        Assert.Equal("https://192.168.1.20:5443/?pair=012345", PairingLink.DeepLink("https://192.168.1.20:5443/", "012345"));

    [Fact]
    public void Qr_png_starts_with_png_signature() =>
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, PairingQr.ToPng("http://192.168.1.20:5080/?pair=012345")[..4]);
}
