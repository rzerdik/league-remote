namespace LeagueRemote.Connector.Server;

/// <summary>
/// The live hub connections, which phone token each signed in with, and how to close them. Hubs are transient, so this is
/// where per-connection sign-in lives. Thread-safe; abort callbacks and <see cref="Changed"/> run outside the lock.
/// </summary>
internal sealed class PhoneConnections
{
    private sealed record Entry(Action Abort, string? TokenKey);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _connections = new();

    /// <summary>A phone signed in or out, or a signed-in connection closed (the tray shows how many are connected).</summary>
    public event Action? Changed;

    public int SignedInCount
    {
        get
        {
            lock (_lock)
                return _connections.Values.Count(e => e.TokenKey is not null);
        }
    }

    public void Connected(string connectionId, Action abort)
    {
        lock (_lock)
            _connections[connectionId] = new Entry(abort, null);
    }

    /// <summary>
    /// Signs the connection in with <paramref name="tokenKey"/> (switching from another token, if any). False when the
    /// connection already closed.
    /// </summary>
    public bool TrySignIn(string connectionId, string tokenKey)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out var entry))
                return false;
            _connections[connectionId] = entry with { TokenKey = tokenKey };
        }

        Changed?.Invoke();
        return true;
    }

    public string? TokenKeyOf(string connectionId)
    {
        lock (_lock)
            return _connections.GetValueOrDefault(connectionId)?.TokenKey;
    }

    public void SignOut(string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out var entry) || entry.TokenKey is null)
                return;
            _connections[connectionId] = entry with { TokenKey = null };
        }

        Changed?.Invoke();
    }

    /// <summary>The connection closed. Returns the token key it was signed in with, if any.</summary>
    public string? Disconnected(string connectionId)
    {
        string? tokenKey;
        lock (_lock)
        {
            if (!_connections.Remove(connectionId, out var entry))
                return null;
            tokenKey = entry.TokenKey;
        }

        if (tokenKey is not null)
            Changed?.Invoke();
        return tokenKey;
    }

    /// <summary>Token keys of the phones connected right now (never evicted to make room for a new phone).</summary>
    public IReadOnlySet<string> ConnectedTokenKeys()
    {
        lock (_lock)
            return _connections.Values.Select(e => e.TokenKey).OfType<string>().ToHashSet();
    }

    /// <summary>
    /// Signs out and closes every connection signed in with a token key that <paramref name="tokenKeyMatches"/>, except
    /// <paramref name="exceptConnectionId"/>: their token was just revoked, so they must not keep receiving state.
    /// </summary>
    public void Abort(Func<string, bool> tokenKeyMatches, string? exceptConnectionId = null)
    {
        List<Action> aborts = [];
        lock (_lock)
        {
            foreach (var (id, entry) in _connections.ToList())
            {
                if (id == exceptConnectionId || entry.TokenKey is not { } key || !tokenKeyMatches(key))
                    continue;
                _connections[id] = entry with { TokenKey = null };
                aborts.Add(entry.Abort);
            }
        }

        foreach (var abort in aborts)
            abort();
        if (aborts.Count > 0)
            Changed?.Invoke();
    }
}
