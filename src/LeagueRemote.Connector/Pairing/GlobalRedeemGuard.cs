namespace LeagueRemote.Connector.Pairing;

/// <summary>
/// Brute-force guard for 6-digit codes across all addresses, on top of the per-connection and per-IP
/// <see cref="RedeemThrottle"/> (which an attacker can sidestep with many connections/addresses): more than
/// <see cref="MaxFailures"/> failed redeems within a sliding <see cref="Window"/> trips it. The hub then rotates the pairing
/// code (the tray and QR show the new one), so guesses collected so far are worthless. Paired phones are unaffected:
/// <c>Authenticate</c> is never throttled (tokens are 256-bit). Tripping resets the window.
/// <para>
/// Each source address counts at most <see cref="MaxFailuresPerSource"/> times per window. A single address is already
/// bounded by <see cref="RedeemThrottle"/>; without this, one noisy client could rotate the code with 11 wrong guesses.
/// Tripping it takes at least three addresses, which is exactly the distributed attack the guard is for. Guesses at codes
/// that were real recently (<see cref="PhonePairing.IsRetiredCode"/>) don't count at all.
/// </para>
/// </summary>
internal sealed class GlobalRedeemGuard(TimeProvider time)
{
    public const int MaxFailures = 10;
    public const int MaxFailuresPerSource = RedeemThrottle.MaxFailuresPerConnection;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private readonly Queue<(DateTimeOffset At, string Source)> _failures = new();
    private readonly Dictionary<string, int> _perSource = new();

    /// <summary>
    /// Records a failed redeem from <paramref name="source"/> (a <see cref="RedeemThrottle.ClientKey"/>). Returns true when
    /// this failure tripped the guard (the code must be rotated now).
    /// </summary>
    public bool RecordFailure(string source)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            while (_failures.TryPeek(out var oldest) && oldest.At + Window <= now)
            {
                _failures.Dequeue();
                if (--_perSource[oldest.Source] == 0)
                    _perSource.Remove(oldest.Source);
            }

            var fromSource = _perSource.GetValueOrDefault(source);
            if (fromSource >= MaxFailuresPerSource)
                return false;

            _failures.Enqueue((now, source));
            _perSource[source] = fromSource + 1;
            if (_failures.Count <= MaxFailures)
                return false;

            _failures.Clear();
            _perSource.Clear();
            return true;
        }
    }
}
