using Microsoft.Win32;

namespace LeagueRemote.Connector.Tray;

/// <summary>
/// Tray › Start with Windows: a value under the user's own Run key (no admin) that starts the exe at sign-in. The key is a
/// parameter so tests can use a scratch key instead of the real one.
/// </summary>
internal sealed class StartWithWindows(string keyPath = StartWithWindows.RunKeyPath, string valueName = AppShell.Name)
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string;
        }
    }

    public void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(valueName, Command(exePath));
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Points an existing entry at <paramref name="exePath"/>, so a moved or re-downloaded exe still starts with Windows.
    /// Does nothing while it is off. True when the entry changed.
    /// </summary>
    public bool Repoint(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key?.GetValue(valueName) is not string current || string.Equals(current, Command(exePath), StringComparison.OrdinalIgnoreCase))
            return false;
        key.SetValue(valueName, Command(exePath));
        return true;
    }

    /// <summary>The Run value: the quoted path (it may contain spaces).</summary>
    public static string Command(string exePath) => $"\"{exePath}\"";
}
