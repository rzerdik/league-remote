using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace LeagueRemote.Connector.Pairing;

/// <summary>
/// Brute-force guard for 6-digit codes: a fixed-window limit of failed redeem attempts per client address.
/// (The hub additionally caps failures per connection, and <see cref="GlobalRedeemGuard"/> watches all of them.)
/// <para>
/// Threat model: anyone on the network who can reach the hub may guess codes; the three layers bound how many guesses one
/// connection, one address and everyone together get before codes stop being worth guessing. The address is the phone's
/// own LAN address (<c>HttpContext.Connection.RemoteIpAddress</c>; there is no proxy in front). Only guesses count: a code
/// that was real recently (<see cref="PhonePairing.IsRetiredCode"/>) is a user with a stale QR, not an attacker, and
/// doesn't use up the address's budget. IPv6 clients are grouped by /64, the block a single host usually gets (the server
/// listens on IPv4 only today; this keeps the guard right should it ever go dual-stack).
/// </para>
/// </summary>
internal sealed class RedeemThrottle(TimeProvider time)
{
    public const int MaxFailuresPerConnection = 5;
    public const int MaxFailuresPerIp = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>Above this many tracked addresses, expired buckets are dropped (so a scan can't grow memory forever).</summary>
    private const int CleanupThreshold = 1024;

    private sealed record Bucket(DateTimeOffset WindowStart, int Failures);

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    /// <summary>
    /// The throttle key for a client address: IPv4 as is (also when mapped into IPv6), IPv6 by its /64 prefix,
    /// <c>"unknown"</c> without an address.
    /// </summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null)
            return "unknown";
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || IPAddress.IsLoopback(address))
            return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    public bool IsBlocked(string key) =>
        _buckets.TryGetValue(key, out var bucket)
        && bucket.WindowStart + Window > time.GetUtcNow()
        && bucket.Failures >= MaxFailuresPerIp;

    public void RecordFailure(string key)
    {
        var now = time.GetUtcNow();
        _buckets.AddOrUpdate(
            key,
            _ => new Bucket(now, 1),
            (_, b) => b.WindowStart + Window > now ? b with { Failures = b.Failures + 1 } : new Bucket(now, 1));

        if (_buckets.Count > CleanupThreshold)
        {
            foreach (var (expiredKey, bucket) in _buckets)
            {
                if (bucket.WindowStart + Window <= now)
                    _buckets.TryRemove(new KeyValuePair<string, Bucket>(expiredKey, bucket));
            }
        }
    }

    /// <summary>Addresses currently tracked (tests).</summary>
    internal int TrackedCount => _buckets.Count;
}
