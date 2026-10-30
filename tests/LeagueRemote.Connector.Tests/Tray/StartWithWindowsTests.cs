using LeagueRemote.Connector.Tray;
using Microsoft.Win32;

namespace LeagueRemote.Connector.Tests;

/// <summary>Against a scratch key under HKCU\Software\LeagueRemote.Tests (deleted afterwards), never the real Run key.</summary>
public sealed class StartWithWindowsTests : IDisposable
{
    private const string Parent = @"Software\LeagueRemote.Tests";
    private readonly string _key = $@"{Parent}\{Guid.NewGuid():N}";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(Parent);
        if (parent is { SubKeyCount: 0, ValueCount: 0 })
            Registry.CurrentUser.DeleteSubKey(Parent, throwOnMissingSubKey: false);
    }

    private string? Value()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_key);
        return key?.GetValue("League Remote") as string;
    }

    [Fact]
    public void Enable_writes_the_quoted_exe_path_and_Disable_removes_it()
    {
        var startup = new StartWithWindows(_key);
        Assert.False(startup.IsEnabled);

        startup.Enable(@"C:\Games\League Remote\LeagueRemote.exe");
        Assert.True(startup.IsEnabled);
        Assert.Equal("\"C:\\Games\\League Remote\\LeagueRemote.exe\"", Value());

        startup.Disable();
        Assert.False(startup.IsEnabled);
        Assert.Null(Value());
        startup.Disable(); // already off: no error
    }

    [Fact]
    public void Repoint_follows_a_moved_exe_only_while_it_is_on()
    {
        var startup = new StartWithWindows(_key);
        Assert.False(startup.Repoint(@"C:\New\LeagueRemote.exe"));
        Assert.Null(Value());

        startup.Enable(@"C:\Old\LeagueRemote.exe");
        Assert.True(startup.Repoint(@"C:\New\LeagueRemote.exe"));
        Assert.Equal("\"C:\\New\\LeagueRemote.exe\"", Value());
        Assert.False(startup.Repoint(@"c:\new\LeagueRemote.exe"));
    }
}
