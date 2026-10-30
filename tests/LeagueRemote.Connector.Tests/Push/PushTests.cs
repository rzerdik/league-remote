using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>Records instead of sending: these tests never contact a real push service.</summary>
internal sealed class FakePushSender : IPushSender
{
    public readonly List<(StoredPushSubscription Subscription, PushNotification Notification)> Sent = [];
    public Func<StoredPushSubscription, PushDelivery> Result { get; set; } = _ => PushDelivery.Sent;
    public TaskCompletionSource FirstSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<PushDelivery> SendAsync(StoredPushSubscription subscription, PushNotification notification, CancellationToken ct)
    {
        lock (Sent)
            Sent.Add((subscription, notification));
        FirstSend.TrySetResult();
        return Task.FromResult(Result(subscription));
    }
}

internal static class PushSamples
{
    public const string Owner = "owner-key";

    public static string P256dh()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = ecdh.ExportParameters(false).Q;
        return Base64Url.EncodeToString([0x04, .. q.X!, .. q.Y!]);
    }

    public static string Auth() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    public static string Endpoint(int n = 1) => $"https://fcm.googleapis.com/fcm/send/test-{n}";

    public static void Register(PushSubscriptionStore store, int n, string owner = Owner) =>
        store.Register(Endpoint(n), P256dh(), Auth(), owner);

    public static ClientState State(string phase, bool connected = true, InGameState? inGame = null, PlayerAction? myAction = null) =>
        new(connected, phase, null, null,
            myAction is null
                ? null
                : new ChampSelectState(1, "BAN_PICK", 30000, [], myAction, [], [], [], ChampSelectBans.None, [], [], "blue",
                    IsTimerInfinite: false, AllowSubsetChampionPicks: false, BenchEnabled: false, BenchChampionIds: null,
                    CanBenchSwap: false, AllowRerolling: false, RerollsRemaining: 0, CanReroll: false, Skins: null, CanDodge: true),
            [], DateTimeOffset.UnixEpoch, inGame);
}

public sealed class PushTriggerDetectorTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-22T12:00:00Z"));
    private readonly PushTriggerDetector _detector;

    public PushTriggerDetectorTests() => _detector = new PushTriggerDetector(_time);

    private IReadOnlyList<string> Observe(ClientState state) => [.. _detector.Observe(state).Select(n => n.Tag)];

    private static ClientState WithInvites(string phase, params ReceivedInvitation[] invitations) =>
        PushSamples.State(phase) with { Invitations = invitations.Length > 0 ? invitations : null };

    private static readonly ReceivedInvitation Flex = new("inv-1", "Friend#EUNE", 440, "Ranked Flex", "CLASSIC", true);
    private static readonly ReceivedInvitation Aram = new("inv-2", "Other#NA1", 450, null, "ARAM", true);

    [Fact]
    public void A_new_invitation_notifies_once_with_name_and_queue_only()
    {
        Assert.Empty(Observe(WithInvites("None")));

        var sent = _detector.Observe(WithInvites("None", Flex));
        var notification = Assert.Single(sent);
        Assert.Equal(PushNotification.LobbyInviteTag, notification.Tag);
        Assert.Equal("Friend#EUNE invited you to Ranked Flex", notification.Body);

        // Replays and the same invite still listed: nothing; a second one: notifies (no queue name: no "to ...").
        Assert.Empty(Observe(WithInvites("None", Flex)));
        var second = Assert.Single(_detector.Observe(WithInvites("None", Flex, Aram)));
        Assert.Equal("Other#NA1 invited you", second.Body);
    }

    [Fact]
    public void Invitations_open_when_the_connector_starts_do_not_notify()
    {
        Assert.Empty(Observe(WithInvites("None", Flex)));
        Assert.Empty(Observe(WithInvites("Lobby", Flex)));
    }

    [Fact]
    public void A_phase_without_the_invitation_list_forgets_nothing()
    {
        Observe(WithInvites("None"));
        Observe(WithInvites("None", Flex));

        // Champ select doesn't read invitations (null), then they are listed again: not new.
        Assert.DoesNotContain(PushNotification.LobbyInviteTag, Observe(PushSamples.State("ChampSelect")));
        Assert.Empty(Observe(WithInvites("EndOfGame", Flex)));

        // Declined (gone), then the same friend invites again with a new id: notifies.
        Assert.Empty(Observe(WithInvites("None")));
        Assert.Equal([PushNotification.LobbyInviteTag], Observe(WithInvites("None", Flex with { Id = "inv-3" })));
    }

    [Fact]
    public void After_unpairing_all_phones_the_next_state_is_a_fresh_baseline()
    {
        Assert.Empty(Observe(PushSamples.State("Lobby")));
        _detector.Forget();

        Assert.Empty(Observe(PushSamples.State("InProgress", inGame: new InGameState(true, 600, _time.GetUtcNow()))));
    }

    [Fact]
    public void Entering_ready_check_notifies_once_and_replays_do_not()
    {
        Assert.Empty(Observe(PushSamples.State("Matchmaking")));
        Assert.Equal([PushNotification.ReadyCheckTag], Observe(PushSamples.State("ReadyCheck")));

        // Same state again (RefreshState).
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Empty(Observe(PushSamples.State("ReadyCheck")));

        // Someone declined, back to queue, a new match pops: notifies again.
        Assert.Empty(Observe(PushSamples.State("Matchmaking")));
        Assert.Equal([PushNotification.ReadyCheckTag], Observe(PushSamples.State("ReadyCheck")));
    }

    [Fact]
    public void Ready_check_flapping_within_the_debounce_window_notifies_once()
    {
        Observe(PushSamples.State("Matchmaking"));
        Assert.NotEmpty(Observe(PushSamples.State("ReadyCheck")));
        _time.Advance(TimeSpan.FromSeconds(1));
        Observe(PushSamples.State("Matchmaking"));
        Assert.Empty(Observe(PushSamples.State("ReadyCheck")));
    }

    [Fact]
    public void First_state_after_start_or_client_reconnect_never_notifies()
    {
        Assert.Empty(Observe(PushSamples.State("ReadyCheck")));
        Assert.Empty(new PushTriggerDetector(_time).Observe(PushSamples.State("InProgress", inGame: new InGameState(true, 10, DateTimeOffset.UnixEpoch))));

        // League client restarted: Disconnected → ReadyCheck is a replay of what the connector found, not a transition.
        Observe(PushSamples.State("None", connected: false));
        Assert.Empty(Observe(PushSamples.State("ReadyCheck")));
    }

    [Fact]
    public void Game_loaded_flip_notifies_once()
    {
        Observe(PushSamples.State("ChampSelect"));
        Assert.Empty(Observe(PushSamples.State("InProgress")));
        Assert.Empty(Observe(PushSamples.State("InProgress", inGame: new InGameState(false, 0, null))));

        var loaded = new InGameState(true, 1.2, _time.GetUtcNow());
        Assert.Equal([PushNotification.GameStartTag], Observe(PushSamples.State("InProgress", inGame: loaded)));
        Assert.Empty(Observe(PushSamples.State("InProgress", inGame: loaded with { GameTime = 6 })));

        // A League client restart mid-game: Disconnected, then loaded again. No new notification.
        Observe(PushSamples.State("None", connected: false));
        Assert.Empty(Observe(PushSamples.State("InProgress", inGame: loaded with { GameTime = 60 })));
    }

    [Fact]
    public void Your_turn_notifies_once_per_action()
    {
        Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(5, "ban", 0, false, false)));
        var ban = Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(5, "ban", 0, true, false)));
        Assert.Equal([PushNotification.ChampSelectTurnTag], ban);
        Assert.Empty(Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(5, "ban", 99, true, false))));

        var pick = _detector.Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(9, "pick", 0, true, false)));
        Assert.Equal("Your turn to pick", Assert.Single(pick).Title);
        Assert.Empty(Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(9, "pick", 0, false, true))));
    }

    [Fact]
    public void Payload_has_the_documented_shape()
    {
        using var json = JsonDocument.Parse(PushNotification.MatchFound().ToPayload());
        var root = json.RootElement;
        Assert.Equal("Match found", root.GetProperty("title").GetString());
        Assert.Equal("Accept on your phone", root.GetProperty("body").GetString());
        Assert.Equal("ready-check", root.GetProperty("tag").GetString());
        Assert.Equal("/", root.GetProperty("url").GetString());
        Assert.True(root.GetProperty("renotify").GetBoolean());
        Assert.True(root.GetProperty("requireInteraction").GetBoolean());

        using var gameStart = JsonDocument.Parse(PushNotification.GameStarted().ToPayload());
        Assert.False(gameStart.RootElement.TryGetProperty("requireInteraction", out _));
    }
}

public sealed class PushSubscriptionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/x")]
    [InlineData("https://192.168.1.10/push")]
    [InlineData("https://localhost/push")]
    [InlineData("https://printer.local/push")]
    [InlineData("https://user:pw@fcm.googleapis.com/x")]
    [InlineData("https://internal.example.com/push")]
    [InlineData("https://evil.com/.push.apple.com")]
    [InlineData("https://fcm.googleapis.com.evil.com/x")]
    [InlineData("https://fcm.googleapis.com:8443/x")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Rejects_endpoints_that_are_not_public_https_urls(string endpoint) =>
        Assert.NotNull(PushSubscriptionStore.Validate(endpoint, PushSamples.P256dh(), PushSamples.Auth()));

    [Fact]
    public void Rejects_overlong_endpoints_and_malformed_keys()
    {
        var tooLong = "https://fcm.googleapis.com/" + new string('a', HubMethods.Server.MaxPushEndpointLength);
        Assert.NotNull(PushSubscriptionStore.Validate(tooLong, PushSamples.P256dh(), PushSamples.Auth()));
        Assert.Null(PushSubscriptionStore.Validate("https://web.push.apple.com/QGx", PushSamples.P256dh(), PushSamples.Auth()));
        Assert.Null(PushSubscriptionStore.Validate("https://updates.push.services.mozilla.com/wpush/v2/x", PushSamples.P256dh(), PushSamples.Auth()));
        Assert.NotNull(PushSubscriptionStore.Validate(PushSamples.Endpoint(), "AAAA", PushSamples.Auth()));
        Assert.NotNull(PushSubscriptionStore.Validate(PushSamples.Endpoint(), PushSamples.P256dh(), "not base64!"));
        Assert.NotNull(PushSubscriptionStore.Validate(PushSamples.Endpoint(), PushSamples.P256dh(), Base64Url.EncodeToString(new byte[8])));
        Assert.Null(PushSubscriptionStore.Validate(PushSamples.Endpoint(), PushSamples.P256dh(), PushSamples.Auth()));

        var store = new PushSubscriptionStore(_time);
        Assert.Throws<ArgumentException>(() => store.Register("http://x.example/y", PushSamples.P256dh(), PushSamples.Auth(), PushSamples.Owner));
    }

    [Fact]
    public void Caps_subscriptions_dropping_the_oldest()
    {
        var store = new PushSubscriptionStore(_time);
        for (var i = 1; i <= HubMethods.Server.MaxPushSubscriptionsPerPairing + 2; i++)
        {
            PushSamples.Register(store, i);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var all = store.GetAll();
        Assert.Equal(HubMethods.Server.MaxPushSubscriptionsPerPairing, all.Count);
        Assert.DoesNotContain(all, s => s.Endpoint == PushSamples.Endpoint(1) || s.Endpoint == PushSamples.Endpoint(2));
    }

    [Fact]
    public void An_unpaired_phone_takes_its_subscriptions_along_and_unpairing_all_clears_them()
    {
        var store = new PushSubscriptionStore(_time, _dir);
        PushSamples.Register(store, 1, "phone-a");
        PushSamples.Register(store, 2, "phone-b");

        store.RemoveOwner("phone-a");
        Assert.Equal([PushSamples.Endpoint(2)], store.GetAll().Select(s => s.Endpoint));

        store.Clear();
        Assert.Empty(store.GetAll());
        Assert.Empty(new PushSubscriptionStore(_time, _dir).GetAll());
    }

    [Fact]
    public void Persists_to_the_data_path()
    {
        var p256dh = PushSamples.P256dh();
        var auth = PushSamples.Auth();
        new PushSubscriptionStore(_time, _dir).Register(PushSamples.Endpoint(), p256dh, auth, PushSamples.Owner);

        var reloaded = new PushSubscriptionStore(_time, _dir);
        var s = Assert.Single(reloaded.GetAll());
        Assert.Equal((PushSamples.Endpoint(), p256dh, auth, PushSamples.Owner), (s.Endpoint, s.P256dh, s.Auth, s.OwnerKey));

        reloaded.Remove(PushSamples.Endpoint());
        Assert.Empty(new PushSubscriptionStore(_time, _dir).GetAll());
    }

    [Fact]
    public void A_subscription_can_be_changed_or_removed_only_by_the_phone_that_registered_it()
    {
        var store = new PushSubscriptionStore(_time);
        PushSamples.Register(store, 1, "phone-a");
        var muted = new PushPreferences { MatchFound = false };

        Assert.Null(store.Find(PushSamples.Endpoint(1), "phone-b"));
        Assert.False(store.SetPreferences(PushSamples.Endpoint(1), muted, "phone-b"));
        Assert.False(store.Unregister(PushSamples.Endpoint(1), "phone-b"));
        Assert.Null(Assert.Single(store.GetAll()).Preferences);

        Assert.NotNull(store.Find(PushSamples.Endpoint(1), "phone-a"));
        Assert.True(store.SetPreferences(PushSamples.Endpoint(1), muted, "phone-a"));
        Assert.Equal(muted, Assert.Single(store.GetAll()).Preferences);

        // The same browser registers again after pairing with a new token: the new token owns it (preferences kept).
        PushSamples.Register(store, 1, "phone-b");
        Assert.Equal(muted, store.Find(PushSamples.Endpoint(1), "phone-b")?.Preferences);
        Assert.True(store.Unregister(PushSamples.Endpoint(1), "phone-b"));
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public async Task Notifier_sends_to_every_subscription_and_drops_gone_ones()
    {
        var store = new PushSubscriptionStore(_time, _dir);
        PushSamples.Register(store, 1);
        PushSamples.Register(store, 2);
        var sender = new FakePushSender { Result = s => s.Endpoint == PushSamples.Endpoint(2) ? PushDelivery.Gone : PushDelivery.Sent };
        var notifier = new PushNotifier(store, new PushTriggerDetector(_time), new PushForegroundTracker(_time), sender, _time, NullLogger<PushNotifier>.Instance);

        await notifier.OnStatePublished(PushSamples.State("Matchmaking"));
        await notifier.OnStatePublished(PushSamples.State("ReadyCheck"));

        Assert.Equal(2, sender.Sent.Count);
        Assert.All(sender.Sent, s => Assert.Equal(PushNotification.ReadyCheckTag, s.Notification.Tag));
        Assert.Equal([PushSamples.Endpoint(1)], store.GetAll().Select(s => s.Endpoint));
        Assert.Equal([PushSamples.Endpoint(1)], new PushSubscriptionStore(_time, _dir).GetAll().Select(s => s.Endpoint));
    }

    [Fact]
    public async Task Notifier_skips_phones_with_the_app_in_the_foreground_without_changing_debouncing()
    {
        var store = new PushSubscriptionStore(_time);
        PushSamples.Register(store, 1);
        PushSamples.Register(store, 2);
        var foreground = new PushForegroundTracker(_time);
        var sender = new FakePushSender();
        var notifier = new PushNotifier(store, new PushTriggerDetector(_time), foreground, sender, _time, NullLogger<PushNotifier>.Instance);

        foreground.Set("conn-1", PushSamples.Endpoint(1), foreground: true);
        await notifier.OnStatePublished(PushSamples.State("Matchmaking"));
        await notifier.OnStatePublished(PushSamples.State("ReadyCheck"));
        Assert.Equal([PushSamples.Endpoint(2)], sender.Sent.Select(s => s.Subscription.Endpoint));

        // Both phones in the foreground: nothing is sent, but the trigger still counts (a replay stays silent later).
        sender.Sent.Clear();
        foreground.Set("conn-2", PushSamples.Endpoint(2), foreground: true);
        await notifier.OnStatePublished(PushSamples.State("Matchmaking"));
        await notifier.OnStatePublished(PushSamples.State("ReadyCheck"));
        Assert.Empty(sender.Sent);
        foreground.ClearConnection("conn-1");
        foreground.ClearConnection("conn-2");
        await notifier.OnStatePublished(PushSamples.State("ReadyCheck"));
        Assert.Empty(sender.Sent);
    }
}

public sealed class PushForegroundTrackerTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-22T12:00:00Z"));
    private readonly PushForegroundTracker _tracker;

    public PushForegroundTrackerTests() => _tracker = new PushForegroundTracker(_time);

    [Fact]
    public void Foreground_lasts_until_hidden_or_the_connection_closes()
    {
        Assert.False(_tracker.IsForeground(PushSamples.Endpoint()));

        _tracker.Set("c1", PushSamples.Endpoint(), foreground: true);
        Assert.True(_tracker.IsForeground(PushSamples.Endpoint()));
        Assert.False(_tracker.IsForeground(PushSamples.Endpoint(2)));

        _tracker.Set("c1", PushSamples.Endpoint(), foreground: false);
        Assert.False(_tracker.IsForeground(PushSamples.Endpoint()));

        _tracker.Set("c1", PushSamples.Endpoint(), foreground: true);
        _tracker.ClearConnection("c1");
        Assert.False(_tracker.IsForeground(PushSamples.Endpoint()));
    }

    [Fact]
    public void A_foreground_report_expires_unless_repeated()
    {
        _tracker.Set("c1", PushSamples.Endpoint(), foreground: true);
        _time.Advance(PushForegroundTracker.StaleAfter - TimeSpan.FromSeconds(1));
        Assert.True(_tracker.IsForeground(PushSamples.Endpoint()));

        _tracker.Set("c1", PushSamples.Endpoint(), foreground: true); // heartbeat
        _time.Advance(PushForegroundTracker.StaleAfter - TimeSpan.FromSeconds(1));
        Assert.True(_tracker.IsForeground(PushSamples.Endpoint()));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_tracker.IsForeground(PushSamples.Endpoint()));
    }

    [Fact]
    public void One_connection_closing_does_not_unmute_another_connection_of_the_same_phone()
    {
        // A reconnect briefly overlaps the old connection: the new one's report must survive the old one closing.
        _tracker.Set("old", PushSamples.Endpoint(), foreground: true);
        _tracker.Set("new", PushSamples.Endpoint(), foreground: true);
        _tracker.ClearConnection("old");
        Assert.True(_tracker.IsForeground(PushSamples.Endpoint()));
    }
}

public sealed class VapidKeysTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Generated_keys_are_a_valid_p256_pair_in_base64url()
    {
        var keys = VapidKeys.Generate();
        Assert.True(keys.IsValid());
        Assert.Equal(65, Base64Url.DecodeFromChars(keys.PublicKey).Length);
        Assert.Equal(32, Base64Url.DecodeFromChars(keys.PrivateKey).Length);
        Assert.DoesNotContain('=', keys.PublicKey);
        Assert.False((keys with { PublicKey = VapidKeys.Generate().PublicKey }).IsValid());
    }

    [Fact]
    public void Keys_persist_in_the_data_path_protected_and_a_corrupt_file_is_replaced()
    {
        var first = VapidKeys.LoadOrCreate(_dir);
        var path = Path.Combine(_dir, VapidKeys.FileName);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(_dir, VapidKeys.LegacyFileName)));
        Assert.Equal(first, VapidKeys.LoadOrCreate(_dir));

        // DPAPI-protected: neither key is readable from the file.
        var stored = File.ReadAllText(path);
        Assert.DoesNotContain(first.PrivateKey, stored);
        Assert.DoesNotContain(first.PublicKey, stored);

        File.WriteAllText(path, "{ not a dpapi blob");
        var replaced = VapidKeys.LoadOrCreate(_dir);
        Assert.NotEqual(first, replaced);
        Assert.True(replaced.IsValid());
        Assert.Equal(replaced, VapidKeys.LoadOrCreate(_dir));
    }

    [Fact]
    public void A_plaintext_key_file_of_an_older_version_is_adopted_protected_and_removed()
    {
        var existing = VapidKeys.Generate();
        Directory.CreateDirectory(_dir);
        var legacyPath = Path.Combine(_dir, VapidKeys.LegacyFileName);
        File.WriteAllText(legacyPath, $$$"""{ "publicKey": "{{{existing.PublicKey}}}", "privateKey": "{{{existing.PrivateKey}}}" }""");

        // Same keys, so the phones' subscriptions stay valid; the plaintext is gone.
        Assert.Equal(existing, VapidKeys.LoadOrCreate(_dir));
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(Path.Combine(_dir, VapidKeys.FileName)));
        Assert.Equal(existing, VapidKeys.LoadOrCreate(_dir));

        // An unreadable plaintext file is not adopted.
        File.Delete(Path.Combine(_dir, VapidKeys.FileName));
        File.WriteAllText(legacyPath, "{ not json");
        var fresh = VapidKeys.LoadOrCreate(_dir);
        Assert.NotEqual(existing, fresh);
        Assert.True(fresh.IsValid());
    }

    [Fact]
    public void Without_a_data_path_keys_are_in_memory_only() =>
        Assert.NotEqual(VapidKeys.LoadOrCreate(null), VapidKeys.LoadOrCreate(""));
}

public sealed class PushHubTests
{
    [Fact]
    public async Task Phone_registers_a_subscription_and_a_ready_check_is_pushed_to_it()
    {
        await using var server = await PhoneServerHost.StartAsync();
        await using var phone = await server.StartPhoneAsync();

        // Not signed in: rejected.
        await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<string>(HubMethods.Server.GetPushPublicKey));

        await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, server.Pairing.Code);
        var publicKey = await phone.InvokeAsync<string>(HubMethods.Server.GetPushPublicKey);
        Assert.Equal(65, Base64Url.DecodeFromChars(publicKey).Length);
        await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync(
            HubMethods.Server.RegisterPushSubscription, "http://insecure.example/x", PushSamples.P256dh(), PushSamples.Auth()));

        await phone.InvokeAsync(HubMethods.Server.RegisterPushSubscription, PushSamples.Endpoint(42), PushSamples.P256dh(), PushSamples.Auth());

        await StatesAsync(server.Get<StateStore>(), phone, "Matchmaking", "ReadyCheck");
        await server.Push.FirstSend.Task.WaitAsync(PhoneServerHost.Timeout);

        lock (server.Push.Sent)
        {
            var (subscription, notification) = Assert.Single(server.Push.Sent);
            Assert.Equal(PushSamples.Endpoint(42), subscription.Endpoint);
            Assert.Equal("Match found", notification.Title);
        }

        await phone.InvokeAsync(HubMethods.Server.UnregisterPushSubscription, PushSamples.Endpoint(42));
        Assert.Empty(server.Get<PushSubscriptionStore>().GetAll());
    }

    [Fact]
    public async Task A_phone_with_the_app_in_the_foreground_gets_no_push_while_another_phone_does()
    {
        await using var server = await PhoneServerHost.StartAsync();
        await using var open = await server.StartPhoneAsync();
        await Assert.ThrowsAsync<HubException>(() => open.InvokeAsync(HubMethods.Server.SetPushForeground, PushSamples.Endpoint(1), true));

        await open.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, server.Pairing.Code);
        var (pocket, _) = await server.PairPhoneAsync();
        await using var _pocket = pocket;

        await open.InvokeAsync(HubMethods.Server.RegisterPushSubscription, PushSamples.Endpoint(1), PushSamples.P256dh(), PushSamples.Auth());
        await pocket.InvokeAsync(HubMethods.Server.RegisterPushSubscription, PushSamples.Endpoint(2), PushSamples.P256dh(), PushSamples.Auth());
        await open.InvokeAsync(HubMethods.Server.SetPushForeground, PushSamples.Endpoint(1), true);

        await StatesAsync(server.Get<StateStore>(), pocket, "Matchmaking", "ReadyCheck");
        await server.Push.FirstSend.Task.WaitAsync(PhoneServerHost.Timeout);
        lock (server.Push.Sent)
            Assert.Equal(PushSamples.Endpoint(2), Assert.Single(server.Push.Sent).Subscription.Endpoint);

        // The open phone's connection drops (e.g. the app was closed): it gets pushes again.
        var foreground = server.Get<PushForegroundTracker>();
        Assert.True(foreground.IsForeground(PushSamples.Endpoint(1)));
        await open.StopAsync();
        await PhoneServerHost.EventuallyAsync(() => !foreground.IsForeground(PushSamples.Endpoint(1)));
    }

    [Fact]
    public async Task A_phone_can_change_remove_or_test_only_the_subscriptions_it_registered()
    {
        await using var server = await PhoneServerHost.StartAsync();
        var (mine, _) = await server.PairPhoneAsync();
        var (other, _) = await server.PairPhoneAsync();
        await using var _mine = mine;
        await using var _other = other;
        var store = server.Get<PushSubscriptionStore>();
        var muted = new PushPreferences { MatchFound = false };
        await mine.InvokeAsync(HubMethods.Server.RegisterPushSubscription, PushSamples.Endpoint(1), PushSamples.P256dh(), PushSamples.Auth());

        // Another paired phone: the subscription reads as not registered.
        Assert.False(await other.InvokeAsync<bool>(HubMethods.Server.SetPushPreferences, PushSamples.Endpoint(1), muted));
        await other.InvokeAsync(HubMethods.Server.UnregisterPushSubscription, PushSamples.Endpoint(1));
        var refused = await Assert.ThrowsAsync<HubException>(() => other.InvokeAsync<bool>(HubMethods.Server.SendTestPush, PushSamples.Endpoint(1)));
        Assert.Contains(PushNotifier.NotRegistered, refused.Message);
        await Assert.ThrowsAsync<HubException>(() => other.InvokeAsync(HubMethods.Server.SetPushForeground, PushSamples.Endpoint(1), true));
        Assert.False(server.Get<PushForegroundTracker>().IsForeground(PushSamples.Endpoint(1)));
        Assert.Null(Assert.Single(store.GetAll()).Preferences);
        Assert.Empty(server.Push.Sent);

        // The phone that registered it: everything works.
        Assert.True(await mine.InvokeAsync<bool>(HubMethods.Server.SetPushPreferences, PushSamples.Endpoint(1), muted));
        Assert.Equal(muted, Assert.Single(store.GetAll()).Preferences);
        await mine.InvokeAsync(HubMethods.Server.SetPushForeground, PushSamples.Endpoint(1), true);
        Assert.True(server.Get<PushForegroundTracker>().IsForeground(PushSamples.Endpoint(1)));
        Assert.True(await mine.InvokeAsync<bool>(HubMethods.Server.SendTestPush, PushSamples.Endpoint(1)));
        lock (server.Push.Sent)
            Assert.Equal(PushSamples.Endpoint(1), Assert.Single(server.Push.Sent).Subscription.Endpoint);
        await mine.InvokeAsync(HubMethods.Server.UnregisterPushSubscription, PushSamples.Endpoint(1));
        Assert.Empty(store.GetAll());
    }

    /// <summary>
    /// The League client moves through <paramref name="phases"/>; each step waits until <paramref name="phone"/> received it,
    /// so push saw every state (the broadcaster only sends the latest one per wake).
    /// </summary>
    internal static async Task StatesAsync(StateStore store, HubConnection phone, params string[] phases)
    {
        foreach (var phase in phases)
        {
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = phone.On<ClientState>(HubMethods.Client.StateUpdated, s =>
            {
                if (s.GameflowPhase == phase)
                    received.TrySetResult();
            });
            store.Set(PushSamples.State(phase));
            await received.Task.WaitAsync(PhoneServerHost.Timeout);
        }
    }
}
