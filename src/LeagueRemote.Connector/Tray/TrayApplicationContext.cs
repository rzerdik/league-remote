using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using LeagueRemote.Connector.Server;

namespace LeagueRemote.Connector.Tray;

/// <summary>
/// Notification-area icon: status, phones, current pairing code, phone app / setup URLs (click opens the browser), pairing QR
/// window, Unpair All Phones, Allow on Private Networks (only while the firewall would block phones), Start with Windows,
/// Open Settings File, Open Log Folder, Exit; an error balloon when the phone server cannot start. The QR window opens on
/// start and whenever the exe is launched again (<see cref="ShowPairingWindow"/>). Runs on the UI (STA) thread.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    /// <summary>ERROR_CANCELLED: the user said no to the UAC prompt.</summary>
    private const int ErrorCancelled = 1223;

    private readonly ConnectorStatus _status;
    private readonly int[] _ports;
    private readonly StartWithWindows _startWithWindows = new();
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _lcuItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _phonesItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _codeItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _appUrlItem = new() { Visible = false, ToolTipText = "Open in browser" };
    private readonly ToolStripMenuItem _setupUrlItem = new() { Visible = false, ToolTipText = "Open in browser. Open it on a new phone (same Wi-Fi) to install the certificate." };
    private readonly ToolStripMenuItem _qrItem = new("Show pairing QR…");
    private readonly ToolStripMenuItem _unpairItem = new("Unpair All Phones");
    private readonly ToolStripMenuItem _firewallItem = new("Allow on Private Networks") { Visible = false, ToolTipText = "Let phones on your home Wi-Fi reach this PC. Asks for admin rights." };
    private readonly ToolStripMenuItem _startItem = new("Start with Windows");
    private PairingQrForm? _qrForm;
    private readonly SynchronizationContext _ui;
    private string? _lastBalloonCode;
    private string? _lastBalloonError;

    /// <param name="server">The ports the firewall item opens.</param>
    /// <param name="settingsProblem">Why settings.json was ignored, shown once in a balloon.</param>
    public TrayApplicationContext(ConnectorStatus status, ServerOptions server, string? settingsProblem = null)
    {
        _status = status;
        _ports = [.. new[] { server.HttpPort, server.HttpsPort }.Where(p => p > 0).Distinct()];

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _lcuItem, _phonesItem, _codeItem, _appUrlItem, _setupUrlItem, new ToolStripSeparator(),
            _qrItem, _unpairItem, new ToolStripSeparator(),
            _firewallItem, _startItem, new ToolStripMenuItem("Open Settings File", null, (_, _) => OpenSettingsFile()),
            new ToolStripMenuItem("Open Log Folder", null, (_, _) => OpenLogFolder()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);
        _appUrlItem.Click += (_, _) => AppShell.OpenInBrowser(_status.AppUrl);
        _setupUrlItem.Click += (_, _) => AppShell.OpenInBrowser(_status.SetupUrl);
        _unpairItem.Click += (_, _) => Task.Run(_status.RequestUnpair);
        _qrItem.Click += (_, _) => ShowQr();
        _firewallItem.Click += (_, _) => AllowOnPrivateNetworks();
        _startItem.Click += (_, _) => ToggleStartWithWindows();

        // Creating the menu (a Control) installs the WinForms synchronization context.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _icon = new NotifyIcon
        {
            Icon = AppShell.LoadIcon(SystemInformation.SmallIconSize),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => ShowQr();

        _status.Changed += OnStatusChanged;
        Refresh();
        ShowQr();
        InitStartWithWindows();
        CheckFirewall();
        if (settingsProblem is not null)
            ShowBalloon(settingsProblem, ToolTipIcon.Warning);
    }

    /// <summary>Safe to call from any thread.</summary>
    public void RequestExit() => _ui.Post(_ => ExitThread(), null);

    /// <summary>Opens (or restores) the QR window and brings it to the front. Safe to call from any thread.</summary>
    public void ShowPairingWindow()
    {
        try
        {
            _ui.Post(_ => ShowQr(), null);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or InvalidAsynchronousStateException)
        {
            // The UI loop already ended (shutting down); nothing to show.
        }
    }

    private void OnStatusChanged() => _ui.Post(_ => Refresh(), null);

    private void Refresh()
    {
        var code = _status.PairingCode;
        _lcuItem.Text = _status.LcuStatus;
        _phonesItem.Text = _status.PhonesStatus;
        _codeItem.Text = (_status.IsPaired, code) switch
        {
            (_, null) => "Pairing code: …",
            (true, _) => $"Another phone: code {code}",
            (false, _) => $"Pairing code: {code}",
        };
        _unpairItem.Enabled = _status.IsPaired;
        _appUrlItem.Text = $"Phone app: {_status.AppUrl}";
        _appUrlItem.Visible = _status.AppUrl is not null && _status.ServerError is null;
        _setupUrlItem.Text = $"First-time phone setup: {_status.SetupUrl}";
        _setupUrlItem.Visible = _status.SetupUrl is not null && _status.ServerError is null;

        if (_status.ServerError is { } error && error != _lastBalloonError)
        {
            _lastBalloonError = error;
            _icon.ShowBalloonTip(15_000, $"{AppShell.Name} cannot start", error, ToolTipIcon.Error);
            return;
        }

        var tooltip = _status.ServerError is { } serverError ? $"{AppShell.Name}: {serverError}"
            : code is null || _status.IsPaired ? $"{AppShell.Name}: {_status.LcuStatus}"
            : $"{AppShell.Name}: pair with code {code}";
        _icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        // Balloon only while unpaired: once paired the code is only shown in the menu and the QR window.
        if (code is not null && !_status.IsPaired && code != _lastBalloonCode)
        {
            _lastBalloonCode = code;
            _icon.ShowBalloonTip(10_000, $"{AppShell.Name} pairing code", $"Enter {code} on your phone.", ToolTipIcon.Info);
        }
    }

    private void ShowQr()
    {
        if (_qrForm is null || _qrForm.IsDisposed)
        {
            _qrForm = new PairingQrForm(_status);
            _qrForm.FormClosed += (_, _) => _qrForm = null;
        }

        if (_qrForm.WindowState == FormWindowState.Minimized)
            _qrForm.WindowState = FormWindowState.Normal;
        _qrForm.Show();
        AppShell.BringToFront(_qrForm);
    }

    private void ShowBalloon(string text, ToolTipIcon icon = ToolTipIcon.Info) => _icon.ShowBalloonTip(8_000, AppShell.Name, text, icon);

    // --- Start with Windows ------------------------------------------------

    /// <summary>Shows the current state and, when it is on, points it at this exe (it may have been moved or replaced).</summary>
    private void InitStartWithWindows()
    {
        try
        {
            if (Environment.ProcessPath is { } exe)
                _startWithWindows.Repoint(exe);
            _startItem.Checked = _startWithWindows.IsEnabled;
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            _startItem.Enabled = false;
        }
    }

    private void ToggleStartWithWindows()
    {
        try
        {
            if (_startWithWindows.IsEnabled)
                _startWithWindows.Disable();
            else if (Environment.ProcessPath is { } exe)
                _startWithWindows.Enable(exe);
            _startItem.Checked = _startWithWindows.IsEnabled;
        }
        catch (Exception ex) when (IsRegistryError(ex))
        {
            ShowBalloon("Couldn't change Start with Windows.", ToolTipIcon.Warning);
        }
    }

    private static bool IsRegistryError(Exception ex) => ex is UnauthorizedAccessException or SecurityException or IOException;

    // --- Firewall ----------------------------------------------------------

    /// <summary>Shows Allow on Private Networks unless the firewall already lets phones in (or can't be read: offer it then).</summary>
    private void CheckFirewall() => Task.Run(() =>
    {
        var rules = PrivateNetworkFirewall.ReadRules();
        var allowed = rules is not null && PrivateNetworkFirewall.Allows(rules, _ports, Environment.ProcessPath);
        OnUi(() => _firewallItem.Visible = !allowed);
    });

    /// <summary>Adds the firewall rule through one UAC prompt; saying no there is fine.</summary>
    private async void AllowOnPrivateNetworks()
    {
        _firewallItem.Enabled = false;
        try
        {
            using var netsh = Process.Start(PrivateNetworkFirewall.AddRuleCommand(_ports));
            if (netsh is not null)
                await netsh.WaitForExitAsync();
            if (netsh is { ExitCode: not 0 })
                ShowBalloon("Couldn't change the firewall.", ToolTipIcon.Warning);
            else
                ShowBalloon("Phones on your private network can now connect.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            ShowBalloon("The firewall wasn't changed.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ShowBalloon("Couldn't change the firewall.", ToolTipIcon.Warning);
        }
        finally
        {
            _firewallItem.Enabled = true;
            CheckFirewall();
        }
    }

    // --- Files -------------------------------------------------------------

    /// <summary>Opens settings.json (written with the defaults first if missing) in the default editor, else Notepad.</summary>
    private void OpenSettingsFile()
    {
        var path = AppDataFolder.Settings;
        try
        {
            ConnectorSettings.CreateIfMissing(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowBalloon("Couldn't create the settings file.", ToolTipIcon.Warning);
            return;
        }

        var notepad = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { ArgumentList = { path } };
        if (TryStart(new ProcessStartInfo(path) { UseShellExecute = true }) || TryStart(notepad))
            ShowBalloon("Restart League Remote after you save changes.");
    }

    private static bool TryStart(ProcessStartInfo start)
    {
        try
        {
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return false; // e.g. no app registered for .json
        }
    }

    private void OnUi(Action action)
    {
        try
        {
            _ui.Post(_ => action(), null);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or InvalidAsynchronousStateException)
        {
            // The UI loop already ended (shutting down).
        }
    }

    private static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder.Logs);
            Process.Start(new ProcessStartInfo(AppDataFolder.Logs) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            // Folder can't be created or Explorer can't open it; nothing sensible to do from a tray app.
        }
    }

    protected override void ExitThreadCore()
    {
        _qrForm?.Close();
        _status.Changed -= OnStatusChanged;
        _icon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.ContextMenuStrip?.Dispose();
            _icon.Icon?.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
