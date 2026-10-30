using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.ClientSounds;
using LeagueRemote.Connector.Commands;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.LiveClient;
using LeagueRemote.Connector.Logging;
using LeagueRemote.Connector.Server;
using LeagueRemote.Connector.State;
using LeagueRemote.Connector.Tray;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace LeagueRemote.Connector;

internal static class Program
{
    private const string ConsoleFlag = "--console";

    [STAThread]
    private static int Main(string[] args)
    {
        var headless = args.Contains(ConsoleFlag, StringComparer.OrdinalIgnoreCase);
        var hostArgs = args.Where(a => !string.Equals(a, ConsoleFlag, StringComparison.OrdinalIgnoreCase)).ToArray();

        // WinExe has no console of its own: reuse the parent terminal's, or create one for headless runs.
        if (AttachConsole(AttachParentProcess) || (headless && AllocConsole()))
            UseUtf8Console();

        // A second launch (e.g. double-clicking the exe again) just brings the running instance's window forward.
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            Console.WriteLine($"{AppShell.Name} is already running; showing its window.");
            return 0;
        }

        // Nothing is read from the exe's folder or the working directory: defaults are in code, the user's own settings in
        // the data folder (ConnectorSettings). Always Production: the server faces the LAN.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = hostArgs,
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
        var settingsProblem = ConnectorSettings.Apply(builder.Configuration, AppDataFolder.Settings);
        headless |= builder.Configuration.GetValue<bool>("Connector:Headless");

        // Console for headless runs, plus a rolling file in the data folder so a tray run leaves a log for bug reports.
        // Disposed after the app (declared first), so the shutdown lines reach the file.
        using var fileLog = new FileLoggerProvider(AppDataFolder.Logs);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
        builder.Logging.AddFileLog(fileLog);

        var services = builder.Services;
        services.Configure<LcuOptions>(builder.Configuration.GetSection(LcuOptions.Section));

        services.AddSingleton<ConnectorStatus>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StateStore>();
        services.AddSingleton<ChatStore>();
        services.AddSingleton<FriendsStore>();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<LiveGameStore>();
        // Champ-select automation: presets in auto-pick.json in the data folder (AppDataFolder), fed by the state rebuilds.
        services.AddSingleton(sp => new AutoPickSettingsStore(AppDataFolder.Root, sp.GetRequiredService<ILogger<AutoPickSettingsStore>>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<AutoPickStore>();
        services.AddSingleton<ChampSelect.ChampSelectLock>();
        services.AddSingleton<AutoPickService>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoPickService>());
        services.AddSingleton<ILcuProcessInspector, WmiLcuProcessInspector>();
        services.AddSingleton<LcuDiscovery>();
        services.AddSingleton<LcuEventSocket>();
        services.AddSingleton<LcuConnectionService>();
        services.AddSingleton<ILcuSession>(sp => sp.GetRequiredService<LcuConnectionService>());
        services.AddHostedService(sp => sp.GetRequiredService<LcuConnectionService>());
        // Loaded flag, game clock and the live game (LiveGameStore) from Riot's Live Client Data API, only while a game runs.
        services.AddHostedService<LiveClientPoller>();
        services.AddSingleton<CommandExecutor>();
        services.AddSingleton<ICommandRunner>(sp => sp.GetRequiredService<CommandExecutor>());
        // The client's own UI sounds and map music for the phone; music comes from the running client's lol-game-data.
        services.AddSingleton(sp =>
        {
            var session = sp.GetRequiredService<ILcuSession>();
            return new ClientSoundLibrary(
                sp.GetRequiredService<IOptions<LcuOptions>>().Value.InstallPath, ClientSoundLibrary.DefaultCacheDirectory, TimeProvider.System,
                sp.GetRequiredService<ILogger<ClientSoundLibrary>>(),
                (path, ct) => session.Client is { } client ? client.GetGameDataAudioAsync(path, ct) : Task.FromResult<byte[]?>(null));
        });
        builder.AddPhoneServer(new PhoneServerSettings(AppDataFolder.Root, IPAddress.Any));

        using var app = builder.Build();
        var status = app.Services.GetRequiredService<ConnectorStatus>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));
        var started = false;
        logger.LogInformation("{Name} {Version} starting ({Mode})", AppShell.Name, Version(), headless ? "headless" : "tray");
        if (settingsProblem is not null)
            logger.LogWarning("{Problem} ({Path})", settingsProblem, AppDataFolder.Settings);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            // The process is about to end: get the crash into the file first.
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");
            fileLog.Dispose();
        };
        try
        {
            app.MapPhoneServer();
            app.Start();
            started = true;
            if (!EmbeddedPwa.IsAvailable)
                logger.LogWarning("This build has no embedded PWA: only /setup is served");
        }
        catch (Exception ex)
        {
            // Typically a busy port. The tray stays up to say so; headless runs end with an error code.
            var message = PhoneServer.StartFailure(ex, app.Services.GetRequiredService<IOptions<ServerOptions>>().Value);
            logger.LogError(ex, "{Message}", message);
            status.SetServerError(message);
            if (headless)
                return 1;
        }

        if (headless)
        {
            app.WaitForShutdown();
            return 0;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
        using (var tray = new TrayApplicationContext(status, serverOptions, settingsProblem))
        {
            instance.ListenForActivation(tray.ShowPairingWindow);
            using var stopping = app.Lifetime.ApplicationStopping.Register(tray.RequestExit);
            Application.Run(tray);
        }

        try
        {
            // Also after a failed start: services that did start (the LCU loops) stop cleanly.
            app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (!started)
        {
            logger.LogDebug(ex, "Stopping after a failed start");
        }

        return 0;
    }

    private static string Version() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";

    private static void UseUtf8Console()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Redirected/odd console: keep the default encoding.
        }
    }

    private const int AttachParentProcess = -1;

#pragma warning disable SYSLIB1054 // LibraryImport would need AllowUnsafeBlocks; not worth it for two calls.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
#pragma warning restore SYSLIB1054
}
