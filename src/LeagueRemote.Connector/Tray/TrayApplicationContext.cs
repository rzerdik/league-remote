using System.Diagnostics;

namespace LeagueRemote.Connector.Tray;

/// <summary>
/// Notification-area icon: status, phones, current pairing code, phone app / setup URLs (click opens the browser), pairing QR
/// window, Unpair All Phones, Open Log Folder, Exit; an error balloon when the phone server cannot start. The QR window opens on start and
/// whenever the exe is launched again (<see cref="ShowPairingWindow"/>). Runs on the UI (STA) thread.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ConnectorStatus _status;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _lcuItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _phonesItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _codeItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _appUrlItem = new() { Visible = false, ToolTipText = "Open in browser" };
    private readonly ToolStripMenuItem _setupUrlItem = new() { Visible = false, ToolTipText = "Open in browser. Open it on a new phone (same Wi-Fi) to install the certificate." };
    private readonly ToolStripMenuItem _qrItem = new("Show pairing QR…");
    private readonly ToolStripMenuItem _unpairItem = new("Unpair All Phones");
    private PairingQrForm? _qrForm;
    private readonly SynchronizationContext _ui;
    private string? _lastBalloonCode;
    private string? _lastBalloonError;

    public TrayApplicationContext(ConnectorStatus status)
    {
        _status = status;

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _lcuItem, _phonesItem, _codeItem, _appUrlItem, _setupUrlItem, new ToolStripSeparator(),
            _qrItem, _unpairItem, new ToolStripMenuItem("Open Log Folder", null, (_, _) => OpenLogFolder()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);
        _appUrlItem.Click += (_, _) => AppShell.OpenInBrowser(_status.AppUrl);
        _setupUrlItem.Click += (_, _) => AppShell.OpenInBrowser(_status.SetupUrl);
        _unpairItem.Click += (_, _) => Task.Run(_status.RequestUnpair);
        _qrItem.Click += (_, _) => ShowQr();

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
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or System.ComponentModel.InvalidAsynchronousStateException)
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

    private static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder.Logs);
            Process.Start(new ProcessStartInfo(AppDataFolder.Logs) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or System.ComponentModel.Win32Exception or InvalidOperationException)
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
