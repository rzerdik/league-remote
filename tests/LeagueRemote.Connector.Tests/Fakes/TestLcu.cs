using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeagueRemote.Connector.Tests;

/// <summary>Helpers for tests that hand LCU JSON to mappers or a fake LCU (<c>using static</c>).</summary>
internal static class TestLcu
{
    /// <summary>Parses a sample into an element that outlives its (disposed) document.</summary>
    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>An <see cref="HttpClient"/> for an <c>LcuClient</c> talking to a fake LCU handler.</summary>
    public static HttpClient LcuHttp(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://127.0.0.1:50000/") };

    /// <summary>A name resolver as the connection uses it, with the system clock.</summary>
    public static PlayerNameResolver Names(LcuClient client, TimeProvider? time = null) =>
        new(client, NullLogger.Instance, time ?? TimeProvider.System);

    /// <summary>A state reader as one LCU connection uses it.</summary>
    public static GameStateReader StateReader(LcuClient client) => new(client, Names(client), new ChampionCatalog(client));

    /// <summary>One state rebuild as the refresh loop makes it: the reads, then the mapping.</summary>
    public static async Task<ClientState> BuildStateAsync(LcuClient client, GameStateReader? reader = null) =>
        GameStateMapper.Build(await (reader ?? StateReader(client)).ReadAsync(CancellationToken.None), DateTimeOffset.UtcNow);

    /// <summary>One state rebuild against a fake LCU, on a connection of its own.</summary>
    public static async Task<ClientState> BuildStateAsync(HttpMessageHandler lcu)
    {
        using var http = LcuHttp(lcu);
        return await BuildStateAsync(new LcuClient(http));
    }

    /// <summary>One read of the local player's profile against a fake LCU, as the refresh loop makes it.</summary>
    public static async Task<ProfileState> ReadProfileAsync(HttpMessageHandler lcu)
    {
        using var http = LcuHttp(lcu);
        return await ProfileReader.BuildAsync(new LcuClient(http), DateTimeOffset.UtcNow, CancellationToken.None);
    }
}
