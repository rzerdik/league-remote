using LeagueRemote.Connector.Pairing;

namespace LeagueRemote.Connector.Tray;

/// <summary>
/// Small dark window, top to bottom: the 6-digit code (click copies it), the pairing QR, the link (click opens it in the
/// browser), and a "first time on this phone" link to the setup page. Follows the code when it changes. UI thread only.
/// </summary>
internal sealed class PairingQrForm : Form
{
    private const string CodeCaption = "PAIRING CODE";

    private readonly ConnectorStatus _status;
    private readonly SynchronizationContext _ui;
    private readonly Label _caption = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        Text = CodeCaption,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = AppShell.MutedText,
        Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
        Margin = Padding.Empty,
    };
    private readonly Label _code = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = AppShell.Text,
        Font = new Font("Consolas", 26f, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 8),
        Cursor = Cursors.Hand,
    };
    /// <summary>Puts the caption back after it said the code was copied.</summary>
    private readonly System.Windows.Forms.Timer _captionReset = new() { Interval = 1500 };
    // QR codes need a light quiet zone, so the code sits on a white card inside the dark window.
    private readonly PictureBox _qr = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.White,
        Padding = new Padding(8),
        Margin = Padding.Empty,
    };
    private readonly LinkLabel _link = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(0, 10, 0, 0),
    };
    private readonly LinkLabel _setup = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(0, 10, 0, 0),
        Visible = false,
    };
    private readonly ToolTip _toolTip = new();
    private string? _shownLink;
    private string? _shownSetupUrl;

    public PairingQrForm(ConnectorStatus status)
    {
        _status = status;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppShell.Name;
        Icon = AppShell.LoadIcon();
        ClientSize = new Size(340, 536);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppShell.Background;
        ForeColor = AppShell.Text;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);

        foreach (var link in (LinkLabel[])[_link, _setup])
        {
            link.LinkColor = AppShell.Accent;
            link.ActiveLinkColor = AppShell.AccentHover;
            link.VisitedLinkColor = AppShell.Accent;
            link.LinkBehavior = LinkBehavior.HoverUnderline;
        }
        _setup.ForeColor = AppShell.MutedText;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = AppShell.Background };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_caption, 0, 0);
        layout.Controls.Add(_code, 0, 1);
        layout.Controls.Add(_qr, 0, 2);
        layout.Controls.Add(_link, 0, 3);
        layout.Controls.Add(_setup, 0, 4);
        Controls.Add(layout);
        ResumeLayout(performLayout: true);

        _code.Click += (_, _) => CopyCode();
        _captionReset.Tick += (_, _) => ShowCaption(CodeCaption);
        _toolTip.SetToolTip(_code, "Click to copy");
        _link.LinkClicked += (_, _) => AppShell.OpenInBrowser(_status.PairingLink);
        _setup.LinkClicked += (_, _) => AppShell.OpenInBrowser(_status.SetupUrl);
        _toolTip.SetToolTip(_link, "Open in browser. On the phone: scan the QR code above.");
        _toolTip.SetToolTip(_setup, "Open this on the phone (same Wi-Fi) once, to install the certificate.");

        _status.Changed += OnStatusChanged;
        UpdateView();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppShell.UseDarkTitleBar(this);
    }

    private void OnStatusChanged() => _ui.Post(_ => UpdateView(), null);

    private void CopyCode()
    {
        if (_status.PairingCode is not { } code)
            return;
        try
        {
            Clipboard.SetText(code);
            ShowCaption("COPIED");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another program holds the clipboard open.
            ShowCaption("COULDN'T COPY");
        }

        _captionReset.Stop();
        _captionReset.Start();
    }

    private void ShowCaption(string text)
    {
        _captionReset.Stop();
        _caption.Text = text;
    }

    private void UpdateView()
    {
        if (IsDisposed)
            return;

        var code = _status.PairingCode;
        var link = _status.PairingLink;
        _code.Text = code ?? "······";
        _link.Text = link ?? "Starting…";
        _link.LinkArea = new LinkArea(0, link?.Length ?? 0);
        UpdateSetupLine(_status.SetupUrl);

        if (link == _shownLink)
            return;
        _shownLink = link;

        var old = _qr.Image;
        if (link is null)
        {
            _qr.Image = null;
        }
        else
        {
            using var stream = new MemoryStream(PairingQr.ToPng(link));
            _qr.Image = Image.FromStream(stream);
        }

        old?.Dispose();
    }

    private void UpdateSetupLine(string? setupUrl)
    {
        if (setupUrl == _shownSetupUrl)
            return;
        _shownSetupUrl = setupUrl;

        _setup.Visible = setupUrl is not null;
        if (setupUrl is null)
            return;

        const string prefix = "First time on this phone? Set it up:\n";
        _setup.Text = prefix + setupUrl;
        _setup.LinkArea = new LinkArea(prefix.Length, setupUrl.Length);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _status.Changed -= OnStatusChanged;
            _captionReset.Dispose();
            _toolTip.Dispose();
            _qr.Image?.Dispose();
            _caption.Font.Dispose();
            _code.Font.Dispose();
            Icon?.Dispose();
        }

        base.Dispose(disposing);
    }
}
