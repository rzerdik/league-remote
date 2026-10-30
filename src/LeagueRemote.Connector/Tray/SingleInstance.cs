using System.Runtime.InteropServices;

namespace LeagueRemote.Connector.Tray;

/// <summary>
/// One connector per Windows session (<c>Local\</c> kernel objects, i.e. per signed-in user). The first process owns a
/// named mutex and waits on a named event; a later process sets the event (so the first one brings its window to the
/// front) and exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\LeagueRemote.Connector.Instance";
    private const string ActivateEventName = @"Local\LeagueRemote.Connector.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>
    /// The instance guard when this is the first process; otherwise signals the running one and returns null
    /// (the caller should exit).
    /// </summary>
    public static SingleInstance? TryAcquire()
    {
        // Create the event first: a second process that starts while we are still booting then opens the same event,
        // and its signal stays pending until we start listening.
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex, activate);

        mutex.Dispose();
        // Windows only lets the foreground process hand focus on. We were just launched by the user, so we may.
        _ = AllowSetForegroundWindow(AsfwAny);
        activate.Set();
        activate.Dispose();
        return null;
    }

    /// <summary>Invokes <paramref name="onActivate"/> (on a thread-pool thread) whenever another launch is attempted.</summary>
    public void ListenForActivation(Action onActivate) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _activate.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread (should not happen); disposing still frees it.
        }

        _mutex.Dispose();
    }

    private const int AsfwAny = -1;

#pragma warning disable SYSLIB1054 // LibraryImport would need AllowUnsafeBlocks; not worth it for one call.
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
#pragma warning restore SYSLIB1054
}
