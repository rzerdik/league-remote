namespace LeagueRemote.Connector.Push;

/// <summary>
/// Which push subscriptions belong to a phone that has the app open and visible right now: the user is looking at the app,
/// so "Match found" / "Your turn" pushes to that phone are just noise. The phone reports it per SignalR connection
/// (<see cref="Contracts.HubMethods.Server.SetPushForeground"/>); a connection has at most one entry (a browser has one
/// subscription), dropped when the connection closes. A foreground report expires after <see cref="StaleAfter"/> unless
/// the phone repeats it, so a frozen tab (iOS suspends without a "hidden" event reaching the PC) can't mute itself forever.
/// Thread-safe.
/// </summary>
internal sealed class PushForegroundTracker(TimeProvider time)
{
    /// <summary>The PWA repeats its foreground report every 30 s while visible; this leaves room for one missed beat.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(75);

    private sealed record Entry(string Endpoint, DateTimeOffset ReportedAt);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _byConnection = new();

    /// <summary>The connection's phone (subscription <paramref name="endpoint"/>) is now visible or hidden.</summary>
    public void Set(string connectionId, string endpoint, bool foreground)
    {
        lock (_lock)
        {
            if (foreground)
                _byConnection[connectionId] = new Entry(endpoint, time.GetUtcNow());
            else
                _byConnection.Remove(connectionId);
        }
    }

    public void ClearConnection(string connectionId)
    {
        lock (_lock)
            _byConnection.Remove(connectionId);
    }

    /// <summary>True when some live connection reported <paramref name="endpoint"/> visible within <see cref="StaleAfter"/>.</summary>
    public bool IsForeground(string endpoint)
    {
        var cutoff = time.GetUtcNow() - StaleAfter;
        lock (_lock)
            return _byConnection.Values.Any(e => e.Endpoint == endpoint && e.ReportedAt > cutoff);
    }
}
