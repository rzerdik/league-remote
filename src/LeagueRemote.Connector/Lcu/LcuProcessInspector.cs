using System.Diagnostics;
using System.Management;

namespace LeagueRemote.Connector.Lcu;

internal sealed record ProcessCommandLine(int Pid, string CommandLine);

/// <summary>Reads the LeagueClientUx command line (fallback when the lockfile is not where we expect it).</summary>
internal interface ILcuProcessInspector
{
    ProcessCommandLine? FindLeagueClientUx();
}

/// <summary>Best effort: cheap process-name check first, then WMI <c>Win32_Process.CommandLine</c>.</summary>
internal sealed class WmiLcuProcessInspector(ILogger<WmiLcuProcessInspector> logger) : ILcuProcessInspector
{
    private const string ProcessName = "LeagueClientUx";

    public ProcessCommandLine? FindLeagueClientUx()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            if (processes.Length == 0)
                return null;
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = '{ProcessName}.exe'");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using (item)
                {
                    if (item["CommandLine"] is string commandLine && item["ProcessId"] is uint pid)
                        return new ProcessCommandLine((int)pid, commandLine);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            logger.LogDebug(ex, "WMI query for {Process} failed", ProcessName);
        }

        return null;
    }
}
