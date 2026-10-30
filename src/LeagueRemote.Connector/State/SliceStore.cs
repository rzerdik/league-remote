using System.Text.Json;

namespace LeagueRemote.Connector.State;

/// <summary>
/// The latest value of one state slice and a version that moves only when something phones see changed: the fingerprint
/// (JSON of the fingerprint view) leaves out what changes on every read (timestamps, the game clock), so a rebuild that
/// changed nothing isn't sent again. The stored value is always the newest one, so whatever goes out next carries fresh
/// timestamps. Thread-safe.
/// </summary>
internal class SliceStore<T>
{
    private readonly Lock _lock = new();
    private readonly Func<T, T> _fingerprintView;
    private readonly Func<T, DateTimeOffset, T> _stamp;
    private readonly TimeProvider _time;
    private T _current;
    private string _fingerprint;
    private long _version;

    /// <param name="empty">The value before the first <see cref="Set"/>.</param>
    /// <param name="fingerprintView">The value without the parts that must not count as a change.</param>
    /// <param name="stamp">Restamps a value for <see cref="Touch"/> (identity for slices whose time must stay as read).</param>
    /// <param name="time">Clock for <see cref="Touch"/>.</param>
    public SliceStore(T empty, Func<T, T> fingerprintView, Func<T, DateTimeOffset, T> stamp, TimeProvider time)
    {
        _fingerprintView = fingerprintView;
        _stamp = stamp;
        _time = time;
        _current = empty;
        _fingerprint = Fingerprint(empty);
    }

    /// <summary>Raised on the writer's thread (LCU refresh, poller) when a new version is available: handlers only signal.</summary>
    public event Action? Changed;

    public (T Value, long Version) Snapshot()
    {
        lock (_lock)
            return (_current, _version);
    }

    /// <summary>Stores <paramref name="value"/>; returns whether a new version was published.</summary>
    public bool Set(T value) => Update(_ => value);

    /// <summary>Like <see cref="Set"/>, with the new value computed from the current one under the store's lock.</summary>
    public bool Update(Func<T, T> change)
    {
        bool changed;
        lock (_lock)
        {
            var next = change(_current);
            var fingerprint = Fingerprint(next);
            _current = next;
            changed = fingerprint != _fingerprint;
            if (changed)
            {
                _fingerprint = fingerprint;
                _version++;
            }
        }

        if (changed)
            Changed?.Invoke();
        return changed;
    }

    /// <summary>Forces a republish of the current value (a phone asked for a refresh), restamped now.</summary>
    public void Touch()
    {
        lock (_lock)
        {
            _current = _stamp(_current, _time.GetUtcNow());
            _version++;
        }

        Changed?.Invoke();
    }

    private string Fingerprint(T value) => JsonSerializer.Serialize(_fingerprintView(value));
}
