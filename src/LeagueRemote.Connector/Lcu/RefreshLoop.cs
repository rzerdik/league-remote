using System.Threading.Channels;

namespace LeagueRemote.Connector.Lcu;

/// <summary>
/// Rebuilds one slice on request: requests that arrive while it waits or works collapse into one pending rebuild. A
/// transient LCU error keeps the last published slice and retries after <see cref="RetryDelay"/>; anything else ends the
/// loop (and with it the connection, which reconnects).
/// </summary>
/// <param name="name">For the log ("State refresh").</param>
/// <param name="debounce">Wait after a request before rebuilding, so a burst of events costs one rebuild.</param>
/// <param name="throttle">Minimum gap after a rebuild, for slices whose events churn (presence).</param>
internal sealed class RefreshLoop(
    string name,
    TimeSpan debounce,
    TimeSpan throttle,
    Func<CancellationToken, Task> rebuild,
    TimeProvider time,
    ILogger logger)
{
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2.5);

    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Request() => _requests.Writer.TryWrite(true);

    /// <summary>Runs until <paramref name="ct"/> is cancelled or a rebuild fails with a non-transient error.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var _ in _requests.Reader.ReadAllAsync(ct))
        {
            if (debounce > TimeSpan.Zero)
                await Task.Delay(debounce, time, ct);
            while (_requests.Reader.TryRead(out var __)) { }

            var pause = throttle;
            try
            {
                await rebuild(ct);
            }
            catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
            {
                // Mid phase transition, a service restarting, a slow reply while the game launches: try again even if no
                // further event comes. A client that is really gone ends the event socket, which reconnects.
                logger.LogLcuFailure(name, ex);
                Request();
                pause = pause > RetryDelay ? pause : RetryDelay;
            }

            if (pause > TimeSpan.Zero)
                await Task.Delay(pause, time, ct);
        }
    }
}
