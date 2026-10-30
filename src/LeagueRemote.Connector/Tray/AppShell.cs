using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LeagueRemote.Connector.Tray;

/// <summary>App name, icon, colours and small shell helpers shared by the tray and the QR window.</summary>
internal static class AppShell
{
    public const string Name = "League Remote";

    // The background is the icon's (web/scripts/gen-icons.mjs); the gold accent is the PWA's dark `accent-text` and its
    // Increase Contrast variant (web/design/tokens.json), since it colours link text on the dark window.
    public static readonly Color Background = Color.FromArgb(0x0E, 0x10, 0x13);
    public static readonly Color Surface = Color.FromArgb(0x17, 0x1A, 0x1F);
    public static readonly Color Text = Color.FromArgb(0xE6, 0xE9, 0xEF);
    public static readonly Color MutedText = Color.FromArgb(0x8B, 0x93, 0xA1);
    public static readonly Color Accent = Color.FromArgb(0xED, 0xC5, 0x72);
    public static readonly Color AccentHover = Color.FromArgb(0xF5, 0xD4, 0x8A);

    private const string IconResource = "LeagueRemote.Connector.app.ico";

    /// <summary>The multi-size app icon (Assets/app.ico, embedded). Callers own the returned instance.</summary>
    public static Icon LoadIcon(Size? size = null)
    {
        using var stream = typeof(AppShell).Assembly.GetManifestResourceStream(IconResource)
            ?? throw new InvalidOperationException($"Missing embedded resource {IconResource}");
        using var icon = new Icon(stream);
        return size is { } s ? new Icon(icon, s) : (Icon)icon.Clone();
    }

    /// <summary>Opens an http(s) URL in the default browser. Anything else is ignored.</summary>
    public static void OpenInBrowser(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default browser registered; nothing sensible to do from a tray app.
        }
    }

    /// <summary>Dark title bar on Windows 10 20H1+ / 11 (no-op elsewhere).</summary>
    public static void UseDarkTitleBar(Form form)
    {
        var enabled = 1;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    /// <summary>
    /// Activates <paramref name="form"/> and puts it in the foreground. Windows' foreground lock can refuse a plain
    /// Activate() from a background (tray) process, so the window is briefly made topmost as well; the relaunched
    /// process also calls AllowSetForegroundWindow before signalling us.
    /// </summary>
    public static void BringToFront(Form form)
    {
        form.TopMost = true;
        form.TopMost = false;
        form.Activate();
        _ = SetForegroundWindow(form.Handle);
    }

    private const int DwmwaUseImmersiveDarkMode = 20;

#pragma warning disable SYSLIB1054 // LibraryImport would need AllowUnsafeBlocks; not worth it for two calls.
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
#pragma warning restore SYSLIB1054
}
