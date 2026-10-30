using System.Net;
using LeagueRemote.Contracts;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace LeagueRemote.Connector.Push;

internal enum PushDelivery
{
    Sent,

    /// <summary>The push service answered 404/410: the subscription expired or was revoked and must be forgotten.</summary>
    Gone,

    Failed,
}

/// <summary>Delivers one notification to one subscription.</summary>
internal interface IPushSender
{
    Task<PushDelivery> SendAsync(StoredPushSubscription subscription, PushNotification notification, CancellationToken ct);
}

/// <summary>
/// Web Push via <c>Lib.Net.Http.WebPush</c> (RFC 8030 delivery, RFC 8291 aes128gcm payload encryption, RFC 8292 VAPID).
/// The push services (FCM, Mozilla autopush, Apple) are on the internet, so this needs outbound HTTPS from the PC.
/// </summary>
internal sealed class WebPushSender : IPushSender, IDisposable
{
    /// <summary>
    /// VAPID <c>sub</c> claim (RFC 8292): where a push service can reach whoever sends the messages. The project page (an
    /// <c>https:</c> URI is allowed next to <c>mailto:</c>) is a real contact, unlike a placeholder mailbox; every PC sends
    /// its own few notifications. Apple's push service rejects tokens without a well-formed subject.
    /// </summary>
    private const string Subject = "https://github.com/rzerdik/league-remote";

    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly PushServiceClient _client;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(VapidKeys keys, ILogger<WebPushSender> logger)
    {
        _logger = logger;
        _client = new PushServiceClient(_http)
        {
            DefaultAuthentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = Subject },
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
        };
    }

    public async Task<PushDelivery> SendAsync(StoredPushSubscription subscription, PushNotification notification, CancellationToken ct)
    {
        var target = new PushSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);
        var message = new PushMessage(notification.ToPayload())
        {
            Topic = notification.Tag, // a newer message with the same topic replaces an undelivered older one
            TimeToLive = (int)notification.TimeToLive.TotalSeconds,
            Urgency = PushMessageUrgency.High,
        };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(target, message, ct);
            return PushDelivery.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushDelivery.Gone;
        }
        catch (PushServiceClientException ex)
        {
            _logger.LogWarning("Push service {Host} rejected a notification: {Status} {Body}",
                SafeHost(subscription.Endpoint), (int)ex.StatusCode, ex.Body);
            return PushDelivery.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Could not reach push service {Host}: {Message}", SafeHost(subscription.Endpoint), ex.Message);
            return PushDelivery.Failed;
        }
    }

    /// <summary>Log only the host: the endpoint path is a capability URL.</summary>
    private static string SafeHost(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host : "?";

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Glue between the state stores and Web Push: runs <see cref="PushTriggerDetector"/> on every new state and friend list
/// and sends the resulting notifications in the background to the subscriptions whose <see cref="PushPreferences"/> allow
/// that event, dropping subscriptions the push service reports as gone. Phones that have the app open
/// (<see cref="PushForegroundTracker"/>) are skipped: the detector still sees every state, so debouncing is the same
/// whether or not anyone gets the push.
/// </summary>
internal sealed class PushNotifier(
    PushSubscriptionStore subscriptions,
    PushTriggerDetector detector,
    PushForegroundTracker foreground,
    IPushSender sender,
    TimeProvider time,
    ILogger<PushNotifier> logger)
{
    private readonly Lock _testLock = new();
    private readonly Dictionary<string, DateTimeOffset> _lastTest = new();

    /// <summary>Every phone was unpaired: whoever pairs next starts from a fresh baseline.</summary>
    public void Forget() => detector.Forget();

    /// <summary>Returns the background send (completed when there was nothing to send); callers need not await it.</summary>
    public Task OnStatePublished(ClientState state) => Dispatch(detector.Observe(state));

    /// <summary>Like <see cref="OnStatePublished"/>, for new incoming friend requests.</summary>
    public Task OnFriendsPublished(FriendsState friends) => Dispatch(detector.ObserveFriends(friends));

    /// <summary>The phone asked about a subscription it didn't register (or none at all).</summary>
    public const string NotRegistered = "Turn notifications on first.";

    /// <summary>
    /// Sends <see cref="PushNotification.Test"/> to one subscription now, ignoring foreground and preferences. Null when it
    /// was sent; otherwise why not (not registered by <paramref name="ownerKey"/>, asked again within
    /// <see cref="HubMethods.Server.TestPushInterval"/>, the push service refused).
    /// </summary>
    public async Task<string?> SendTestAsync(string endpoint, string ownerKey)
    {
        if (subscriptions.Find(endpoint, ownerKey) is not { } target)
            return NotRegistered;
        lock (_testLock)
        {
            var now = time.GetUtcNow();
            if (_lastTest.TryGetValue(endpoint, out var last) && now - last < HubMethods.Server.TestPushInterval)
                return "Wait a few seconds before sending another test.";
            foreach (var stale in _lastTest.Where(e => now - e.Value >= HubMethods.Server.TestPushInterval).Select(e => e.Key).ToList())
                _lastTest.Remove(stale);
            _lastTest[endpoint] = now;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var delivery = await SendOneAsync(target, PushNotification.Test(), timeout.Token);
        logger.LogInformation("Test push: {Delivery}", delivery);
        if (delivery == PushDelivery.Gone && subscriptions.Remove(endpoint))
            return "This phone's notifications expired. Turn them off and on again.";
        return delivery == PushDelivery.Sent ? null : "The notification service didn't take the test. Try again later.";
    }

    private Task Dispatch(IReadOnlyList<PushNotification> notifications)
    {
        if (notifications.Count == 0)
            return Task.CompletedTask;
        var all = subscriptions.GetAll();
        var awake = all.Where(s => !foreground.IsForeground(s.Endpoint)).ToList();
        if (awake.Count < all.Count)
            logger.LogInformation("Push: skipped {Count} subscription(s) with the app in the foreground", all.Count - awake.Count);

        // Each subscription gets only the events its preferences allow.
        var plan = notifications
            .Select(n => new PlannedPush(n, [.. awake.Where(s => n.Event.AllowedBy(s.Preferences))]))
            .Where(p => p.Targets.Count > 0)
            .ToList();
        if (plan.Count == 0)
            return Task.CompletedTask;
        return Task.Run(() => SendAsync(plan));
    }

    private sealed record PlannedPush(PushNotification Notification, IReadOnlyList<StoredPushSubscription> Targets);

    private async Task<PushDelivery> SendOneAsync(StoredPushSubscription target, PushNotification notification, CancellationToken ct)
    {
        try
        {
            return await sender.SendAsync(target, notification, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push delivery failed");
            return PushDelivery.Failed;
        }
    }

    private async Task SendAsync(IReadOnlyList<PlannedPush> plan)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var (notification, targets) in plan)
        {
            var results = await Task.WhenAll(targets.Select(async target => (target, await SendOneAsync(target, notification, timeout.Token))));

            foreach (var (target, delivery) in results)
            {
                if (delivery == PushDelivery.Gone && subscriptions.Remove(target.Endpoint))
                    logger.LogInformation("Removed an expired push subscription");
            }

            logger.LogInformation("Push \"{Tag}\": {Sent}/{Total} delivered",
                notification.Tag, results.Count(r => r.Item2 == PushDelivery.Sent), results.Length);
        }
    }
}
