using System.Buffers.Text;
using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Push;

/// <summary>A browser's Web Push subscription (what <c>PushSubscription.toJSON()</c> returns).</summary>
/// <param name="Preferences">Which events push to it; null (never set) = <see cref="PushPreferences.Default"/>.</param>
/// <param name="OwnerKey">Key of the phone token that registered it, so it goes when that phone is unpaired or evicted.</param>
internal sealed record StoredPushSubscription(
    string Endpoint,
    string P256dh,
    string Auth,
    DateTimeOffset CreatedAt,
    string OwnerKey,
    PushPreferences? Preferences = null);

/// <summary>
/// This PC's Web Push subscriptions, at most <see cref="HubMethods.Server.MaxPushSubscriptionsPerPairing"/> (the oldest is
/// dropped). Re-registering an endpoint keeps its <see cref="StoredPushSubscription.Preferences"/>: they belong to the phone.
/// With a data path the set is persisted to <c>{dataPath}/push-subscriptions.json</c> (atomic write).
/// </summary>
internal sealed class PushSubscriptionStore
{
    public const string FileName = "push-subscriptions.json";

    private sealed record StoredFile(int Version, List<StoredPushSubscription> Subscriptions);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Lock _lock = new();
    private readonly List<StoredPushSubscription> _subscriptions = [];
    private readonly TimeProvider _time;
    private readonly string? _path;
    private readonly ILogger? _logger;

    public PushSubscriptionStore(TimeProvider time, string? dataPath = null, ILogger<PushSubscriptionStore>? logger = null)
    {
        _time = time;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(dataPath))
        {
            Directory.CreateDirectory(dataPath);
            _path = Path.Combine(dataPath, FileName);
            Load();
        }
    }

    /// <summary>
    /// Null when the subscription is acceptable, otherwise why not: the endpoint must be an absolute https URL without user info
    /// (at most <see cref="HubMethods.Server.MaxPushEndpointLength"/> chars), <paramref name="p256dh"/> a base64url uncompressed
    /// P-256 point (65 bytes) and <paramref name="auth"/> a base64url 16-byte secret.
    /// </summary>
    public static string? Validate(string? endpoint, string? p256dh, string? auth)
    {
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > HubMethods.Server.MaxPushEndpointLength)
            return $"Endpoint is required (max {HubMethods.Server.MaxPushEndpointLength} chars).";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            // The PC POSTs to this URL, so only known browser push services are accepted (no SSRF into the LAN).
            || uri.HostNameType is not UriHostNameType.Dns || !uri.IsDefaultPort || !IsKnownPushHost(uri.Host))
            return "Endpoint must be an https URL of a push service.";
        if (DecodedLength(p256dh, 100) is not 65 || Base64Url.DecodeFromChars(p256dh)[0] != 0x04)
            return "p256dh must be a base64url P-256 public key.";
        if (DecodedLength(auth, 32) is not 16)
            return "auth must be a base64url 16-byte secret.";
        return null;
    }

    /// <summary>Exact hosts and dot-prefixed suffixes of the push services Chrome/Edge/Firefox/Safari use.</summary>
    private static readonly string[] KnownPushHosts =
        ["fcm.googleapis.com", "android.googleapis.com", ".push.services.mozilla.com", ".push.apple.com", ".notify.windows.com"];

    private static bool IsKnownPushHost(string host) =>
        KnownPushHosts.Any(h => h.StartsWith('.')
            ? host.EndsWith(h, StringComparison.OrdinalIgnoreCase)
            : host.Equals(h, StringComparison.OrdinalIgnoreCase));

    private static int? DecodedLength(string? value, int maxChars) =>
        !string.IsNullOrEmpty(value) && value.Length <= maxChars && Base64Url.IsValid(value, out var length) ? length : null;

    /// <summary>Adds or refreshes the subscription. Throws <see cref="ArgumentException"/> when <see cref="Validate"/> rejects it.</summary>
    public void Register(string endpoint, string p256dh, string auth, string ownerKey)
    {
        if (Validate(endpoint, p256dh, auth) is { } error)
            throw new ArgumentException(error);

        lock (_lock)
        {
            // The phone registers again on every start: keep the preferences it set.
            var preferences = _subscriptions.FirstOrDefault(s => s.Endpoint == endpoint)?.Preferences;
            _subscriptions.RemoveAll(s => s.Endpoint == endpoint);
            _subscriptions.Add(new StoredPushSubscription(endpoint, p256dh, auth, _time.GetUtcNow(), ownerKey, preferences));
            var excess = _subscriptions.Count - HubMethods.Server.MaxPushSubscriptionsPerPairing;
            if (excess > 0)
                _subscriptions.RemoveRange(0, excess);
            Save();
        }
    }

    // A phone may only touch the subscriptions it registered (ownerKey = its token key): the endpoint is a capability URL
    // another phone shouldn't be able to mute, re-configure or test-push. The owner-less Remove is for the push service's
    // own verdict (gone) and for unpairing.

    /// <summary>Removes the endpoint when <paramref name="ownerKey"/> registered it. False when it isn't registered, or by another phone.</summary>
    public bool Unregister(string endpoint, string ownerKey)
    {
        lock (_lock)
        {
            if (_subscriptions.RemoveAll(s => s.Endpoint == endpoint && s.OwnerKey == ownerKey) == 0)
                return false;
            Save();
            return true;
        }
    }

    /// <summary>
    /// Sets which events push to <paramref name="endpoint"/>. False when it isn't registered, or not by <paramref name="ownerKey"/>.
    /// </summary>
    public bool SetPreferences(string endpoint, PushPreferences preferences, string ownerKey)
    {
        lock (_lock)
        {
            var index = _subscriptions.FindIndex(s => s.Endpoint == endpoint && s.OwnerKey == ownerKey);
            if (index < 0)
                return false;
            if (_subscriptions[index].Preferences != preferences)
            {
                _subscriptions[index] = _subscriptions[index] with { Preferences = preferences };
                Save();
            }

            return true;
        }
    }

    /// <summary>The subscription <paramref name="endpoint"/> registered by <paramref name="ownerKey"/>, or null.</summary>
    public StoredPushSubscription? Find(string endpoint, string ownerKey)
    {
        lock (_lock)
            return _subscriptions.FirstOrDefault(s => s.Endpoint == endpoint && s.OwnerKey == ownerKey);
    }

    /// <summary>The push service said the subscription is gone (404/410): forget it, whoever registered it.</summary>
    public bool Remove(string endpoint)
    {
        lock (_lock)
        {
            if (_subscriptions.RemoveAll(s => s.Endpoint == endpoint) == 0)
                return false;
            Save();
            return true;
        }
    }

    /// <summary>A phone's token was revoked or evicted: drop the subscriptions it registered.</summary>
    public void RemoveOwner(string ownerKey)
    {
        lock (_lock)
        {
            if (_subscriptions.RemoveAll(s => s.OwnerKey == ownerKey) > 0)
                Save();
        }
    }

    /// <summary>Every phone was unpaired.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_subscriptions.Count == 0)
                return;
            _subscriptions.Clear();
            Save();
        }
    }

    public IReadOnlyList<StoredPushSubscription> GetAll()
    {
        lock (_lock)
            return [.. _subscriptions];
    }

    private void Save()
    {
        if (_path is null)
            return;
        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new StoredFile(1, _subscriptions), Json));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: the subscription works until the next restart, and the phone re-registers on every start.
            _logger?.LogError(ex, "Could not write {Path}", _path);
        }
    }

    private void Load()
    {
        if (_path is null || !File.Exists(_path))
            return;
        try
        {
            var stored = JsonSerializer.Deserialize<StoredFile>(File.ReadAllText(_path), Json);
            foreach (var s in stored?.Subscriptions ?? [])
            {
                if (Validate(s.Endpoint, s.P256dh, s.Auth) is null && !string.IsNullOrEmpty(s.OwnerKey))
                    _subscriptions.Add(s);
            }

            _logger?.LogInformation("Loaded {Count} push subscriptions from {Path}", _subscriptions.Count, _path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Could not read {Path}; starting without push subscriptions", _path);
        }
    }
}
