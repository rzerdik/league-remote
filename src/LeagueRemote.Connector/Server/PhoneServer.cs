using System.Net;
using LeagueRemote.Connector.ClientSounds;
using LeagueRemote.Connector.Https;
using LeagueRemote.Connector.Pairing;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.Tray;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LeagueRemote.Connector.Server;

/// <summary>Where the phone server keeps its files and which interface it listens on.</summary>
/// <param name="DataFolder">Pairing, push subscriptions, VAPID keys and certificates (<c>%LOCALAPPDATA%\league-remote</c>; tests use a temp folder).</param>
/// <param name="ListenAddress"><see cref="IPAddress.Any"/> in the app (LAN); tests use loopback, which also limits the certificate to loopback.</param>
internal sealed record PhoneServerSettings(string DataFolder, IPAddress ListenAddress);

/// <summary>
/// The connector's web side, in the same host as the LCU services: Kestrel on <c>Server:HttpPort</c> (setup page, CA, redirect)
/// and <c>Server:HttpsPort</c> (PWA + <see cref="PhoneHub"/>), pairing, Web Push and the <see cref="PhoneBroadcaster"/>.
/// The connector registers the state stores, an <see cref="Commands.ICommandRunner"/> and (optionally) a
/// <see cref="ClientSoundLibrary"/>.
/// </summary>
internal static class PhoneServer
{
    public static WebApplicationBuilder AddPhoneServer(this WebApplicationBuilder builder, PhoneServerSettings settings)
    {
        var services = builder.Services;
        services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ConnectorStatus>();

        // Picked per handshake, so the certificate follows address changes without a restart.
        services.AddSingleton(sp => CreateCertificates(settings, sp.GetRequiredService<ILogger<ServerCertificateProvider>>()));
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            var options = kestrel.ApplicationServices.GetRequiredService<IOptions<ServerOptions>>().Value;
            kestrel.AddServerHeader = false;
            kestrel.Listen(settings.ListenAddress, options.HttpPort);
            kestrel.Listen(settings.ListenAddress, options.HttpsPort, listen => listen.UseHttps(new HttpsConnectionAdapterOptions
            {
                ServerCertificateSelector = (_, _) => kestrel.ApplicationServices.GetRequiredService<ServerCertificateProvider>().GetCertificate(),
            }));
        });

        services.AddSingleton(sp => new PhonePairing(settings.DataFolder, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<PhonePairing>>()));
        services.AddSingleton<PhoneConnections>();
        services.AddSingleton<RedeemThrottle>();
        services.AddSingleton<GlobalRedeemGuard>();

        services.AddSingleton(sp => VapidKeys.LoadOrCreate(settings.DataFolder, sp.GetRequiredService<ILogger<VapidKeys>>()));
        services.AddSingleton(sp => new PushSubscriptionStore(
            sp.GetRequiredService<TimeProvider>(), settings.DataFolder, sp.GetRequiredService<ILogger<PushSubscriptionStore>>()));
        services.AddSingleton<PushTriggerDetector>();
        services.AddSingleton<PushForegroundTracker>();
        services.TryAddSingleton<IPushSender, WebPushSender>();
        services.AddSingleton<PushNotifier>();

        // Lets a phone fire e.g. RefreshState while a slow SendCommand (LoadMatchDetails) is still running.
        services.AddSignalR(o => o.MaximumParallelInvocationsPerClient = 4);
        services.AddSingleton<PhoneBroadcaster>();
        services.AddHostedService(sp => sp.GetRequiredService<PhoneBroadcaster>());
        services.AddHostedService<PairingStatusService>();
        return builder;
    }

    /// <summary>Security headers, the LAN front, the embedded PWA, <c>/hub</c>, <c>/healthz</c>, <c>/licenses.txt</c>, <c>/client-sounds</c> and the SPA fallback.</summary>
    public static WebApplication MapPhoneServer(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var certificates = app.Services.GetRequiredService<ServerCertificateProvider>();
        app.UseSecurityHeaders();
        app.UseMiddleware<LanHttpMiddleware>(certificates.CaCertificateDer, HttpsPortResolver(app, options.HttpsPort));

        var pwa = EmbeddedPwa.FileProvider;
        var staticFiles = new StaticFileOptions
        {
            FileProvider = pwa,
            ContentTypeProvider = EmbeddedPwa.ContentTypes,
            OnPrepareResponse = ctx =>
            {
                // Hashed /assets/* can be cached; the shell and the service worker must revalidate so updates roll out.
                if (!ctx.Context.Request.Path.StartsWithSegments("/assets"))
                    ctx.Context.Response.Headers.CacheControl = "no-cache";
            },
        };
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = pwa });
        app.UseStaticFiles(staticFiles);

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
        app.MapLicenses();
        app.MapHub<PhoneHub>(HubMethods.Path);
        app.MapClientSounds(app.Services.GetService<ClientSoundLibrary>());
        app.MapFallbackToFile("index.html", staticFiles); // SPA routes (and the /?pair= deep link) → index.html
        return app;
    }

    /// <summary>A message for the tray and the log when the server could not start: a busy port says which one.</summary>
    public static string StartFailure(Exception ex, ServerOptions options) =>
        PortInUse(ex, options) is { } port
            ? $"Port {port} is in use by another program. Set Server:{(port == options.HttpsPort ? "HttpsPort" : "HttpPort")} to a free port in {ConnectorSettings.FileName} (tray menu: Open Settings File)."
            : $"The phone server could not start: {ex.Message}";

    /// <summary>The configured port Kestrel couldn't bind, or null when the failure was something else.</summary>
    internal static int? PortInUse(Exception ex, ServerOptions options)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is not AddressInUseException && e is not IOException { InnerException: AddressInUseException })
                continue;
            var https = options.HttpsPort;
            return ex.Message.Contains($":{https}", StringComparison.Ordinal) || e.Message.Contains($":{https}", StringComparison.Ordinal)
                ? https
                : options.HttpPort;
        }

        return null;
    }

    private static ServerCertificateProvider CreateCertificates(PhoneServerSettings settings, ILogger logger)
    {
        var store = new CertificateStore(Path.Combine(settings.DataFolder, "certs"), TimeProvider.System, logger);
        if (IPAddress.IsLoopback(settings.ListenAddress))
            return new ServerCertificateProvider(store, () => [IPAddress.Loopback], ["localhost"], TimeProvider.System, logger, watchNetwork: false);

        // Addresses are re-read whenever the network changes (Wi-Fi up after start, new DHCP lease), so the certificate keeps
        // matching the IP in the pairing QR. Only addresses the name-constrained CA may vouch for: anything else would make
        // phones reject the whole chain.
        return new ServerCertificateProvider(
            store,
            () => [.. LanAddress.GetAllIPv4().Where(LocalCertificateAuthority.IsPermitted), IPAddress.Loopback],
            LocalCertificateAuthority.PermittedDnsNames,
            TimeProvider.System,
            logger);
    }

    /// <summary>The configured HTTPS port, or (port 0) the one Kestrel actually bound, looked up once the server runs.</summary>
    private static Func<int> HttpsPortResolver(WebApplication app, int configured)
    {
        if (configured != 0)
            return () => configured;

        var resolved = 0;
        return () =>
        {
            if (resolved != 0)
                return resolved;
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
            var https = addresses.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            return https is null ? 0 : resolved = new Uri(https).Port;
        };
    }
}
