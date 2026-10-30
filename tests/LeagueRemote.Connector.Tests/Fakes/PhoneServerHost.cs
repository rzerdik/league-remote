using System.Net;
using LeagueRemote.Connector.ClientSounds;
using LeagueRemote.Connector.Commands;
using LeagueRemote.Connector.Pairing;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.Server;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeagueRemote.Connector.Tests;

/// <summary>Answers every command with success unless a test sets <see cref="Handler"/>; records what it was asked.</summary>
internal sealed class FakeCommandRunner : ICommandRunner
{
    public List<RemoteCommand> Received { get; } = [];

    public Func<RemoteCommand, CancellationToken, Task<CommandResult>> Handler { get; set; } =
        (command, _) => Task.FromResult(new CommandResult(command.RequestId, true));

    public Task<CommandResult> ExecuteAsync(RemoteCommand command, CancellationToken ct)
    {
        lock (Received)
            Received.Add(command);
        return Handler(command, ct);
    }
}

/// <summary>
/// The connector's phone server as the app builds it (<see cref="PhoneServer.AddPhoneServer"/>), with real stores, a
/// <see cref="FakeCommandRunner"/> and a <see cref="FakePushSender"/>: real Kestrel on loopback, ports chosen by the OS
/// (never the app's 5080/5443), data in a temp folder. Clients may claim an address with <see cref="ClientIpHeader"/>
/// (all connections come from loopback otherwise).
/// </summary>
internal sealed class PhoneServerHost : IAsyncDisposable
{
    public const string ClientIpHeader = "X-Test-Client-Ip";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private PhoneServerHost(string dataFolder, WebApplication app)
    {
        DataFolder = dataFolder;
        App = app;
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        Http = new Uri(addresses.Single(a => a.StartsWith("http://", StringComparison.Ordinal)));
        Https = new Uri(addresses.Single(a => a.StartsWith("https://", StringComparison.Ordinal)));
    }

    public string DataFolder { get; }
    public WebApplication App { get; }
    public Uri Http { get; }
    public Uri Https { get; }

    public T Get<T>() where T : notnull => App.Services.GetRequiredService<T>();

    public FakeCommandRunner Commands => (FakeCommandRunner)Get<ICommandRunner>();
    public FakePushSender Push => (FakePushSender)Get<IPushSender>();
    public PhonePairing Pairing => Get<PhonePairing>();

    /// <param name="dataFolder">Reuse a folder (a restart); a new temp folder otherwise. Deleted on dispose either way.</param>
    /// <param name="sounds">Served at <c>/client-sounds</c>; none by default.</param>
    /// <param name="services">Replacements, registered before the server's own (which only fill gaps).</param>
    public static async Task<PhoneServerHost> StartAsync(
        string? dataFolder = null, ClientSoundLibrary? sounds = null, Action<IServiceCollection>? services = null)
    {
        dataFolder ??= Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.Sources.Clear(); // nothing from the machine's environment or a settings file
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ServerOptions.Section}:{nameof(ServerOptions.HttpPort)}"] = "0",
            [$"{ServerOptions.Section}:{nameof(ServerOptions.HttpsPort)}"] = "0",
        });
        builder.Logging.ClearProviders();

        var s = builder.Services;
        s.AddSingleton<StateStore>();
        s.AddSingleton<ChatStore>();
        s.AddSingleton<FriendsStore>();
        s.AddSingleton<ProfileStore>();
        s.AddSingleton<LiveGameStore>();
        s.AddSingleton(new AutoPickStore(TimeProvider.System));
        s.AddSingleton<ICommandRunner, FakeCommandRunner>();
        s.AddSingleton<IPushSender, FakePushSender>();
        if (sounds is not null)
            s.AddSingleton(sounds);
        s.AddSingleton<IStartupFilter, FakeClientIp>();
        services?.Invoke(s);
        builder.AddPhoneServer(new PhoneServerSettings(dataFolder, IPAddress.Loopback));

        var app = builder.Build();
        app.MapPhoneServer();
        await app.StartAsync();
        return new PhoneServerHost(dataFolder, app);
    }

    /// <summary>
    /// A hub connection over HTTPS as a phone makes it (a claimed LAN address over plain HTTP would be redirected), optionally
    /// from <paramref name="clientIp"/>. Loopback test server: any certificate is accepted.
    /// </summary>
    public HubConnection Connect(string? clientIp = null) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Https, HubMethods.Path), o =>
            {
                o.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                };
                o.WebSocketConfiguration = ws => ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                if (clientIp is not null)
                    o.Headers[ClientIpHeader] = clientIp;
            })
            .Build();

    public async Task<HubConnection> StartPhoneAsync(string? clientIp = null)
    {
        var connection = Connect(clientIp);
        await connection.StartAsync();
        return connection;
    }

    /// <summary>A started phone that redeemed the current code.</summary>
    public async Task<(HubConnection Phone, PairingGrant Grant)> PairPhoneAsync(string? clientIp = null)
    {
        var phone = await StartPhoneAsync(clientIp);
        var grant = await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, Pairing.Code);
        return (phone, grant);
    }

    /// <summary>Waits (bounded by <see cref="Timeout"/>) until <paramref name="condition"/> holds, e.g. a disconnect was processed.</summary>
    public static async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition did not hold in time.");
            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        if (Directory.Exists(DataFolder))
            Directory.Delete(DataFolder, recursive: true);
    }

    /// <summary>Kestrel connections all come from loopback here: take the client address from a test header.</summary>
    private sealed class FakeClientIp : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(ClientIpHeader, out var ip) && IPAddress.TryParse(ip, out var address))
                    context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
