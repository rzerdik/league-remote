using System.Text.Json;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>Which events push to which phone (PushPreferences), the opt-in events, and the test notification.</summary>
public sealed class PushPreferencesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-22T12:00:00Z"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static FriendsState Friends(params FriendRequest[] requests) =>
        new FriendsState([new FriendGroup(0, "", true, 0)], [], DateTimeOffset.UnixEpoch) { Requests = requests };

    private static FriendRequest Incoming(string puuid, string name) => new(puuid, FriendRequestDirections.Incoming, name, "EUW", 1, "a note");

    private (PushSubscriptionStore Store, FakePushSender Sender, PushNotifier Notifier) Notifier()
    {
        var store = new PushSubscriptionStore(_time);
        var sender = new FakePushSender();
        return (store, sender, new PushNotifier(store, new PushTriggerDetector(_time), new PushForegroundTracker(_time), sender, _time,
            NullLogger<PushNotifier>.Instance));
    }

    [Fact]
    public void Defaults_push_the_classic_events_and_new_events_are_opt_in()
    {
        Assert.All([PushEvent.MatchFound, PushEvent.YourTurn, PushEvent.GameStarted, PushEvent.LobbyInvite, PushEvent.Test],
            e => Assert.True(e.AllowedBy(null)));
        Assert.False(PushEvent.ChampSelectStarted.AllowedBy(null));
        Assert.False(PushEvent.FriendRequest.AllowedBy(null));

        // A phone that sends only some fields keeps the defaults for the rest.
        var partial = JsonSerializer.Deserialize<PushPreferences>("""{ "matchFound": false, "somethingNew": true }""", JsonSerializerOptions.Web)!;
        Assert.False(partial.MatchFound);
        Assert.True(partial.YourTurn);
        Assert.False(partial.FriendRequest);
    }

    [Fact]
    public void Entering_champ_select_notifies_unless_the_turn_starts_at_once()
    {
        var detector = new PushTriggerDetector(_time);
        detector.Observe(PushSamples.State("ReadyCheck"));
        var started = Assert.Single(detector.Observe(PushSamples.State("ChampSelect")));
        Assert.Equal((PushEvent.ChampSelectStarted, PushNotification.ChampSelectTurnTag), (started.Event, started.Tag));
        Assert.Empty(detector.Observe(PushSamples.State("ChampSelect")));

        // Blind pick: the turn is there from the start; "Your turn" says more, so it's the only one.
        detector.Observe(PushSamples.State("Lobby"));
        var blind = detector.Observe(PushSamples.State("ChampSelect", myAction: new PlayerAction(1, "pick", 0, true, false)));
        Assert.Equal([PushEvent.YourTurn], blind.Select(n => n.Event));
    }

    [Fact]
    public void A_new_incoming_friend_request_notifies_once_with_the_game_name_only()
    {
        var detector = new PushTriggerDetector(_time);
        var open = Incoming("puuid-1", "Friend");
        Assert.Empty(detector.ObserveFriends(Friends(open))); // baseline: already open when the connector first saw the list

        var outgoing = new FriendRequest("puuid-out", FriendRequestDirections.Outgoing, "Someone", "NA1", 0, "");
        var newcomer = Incoming("puuid-2", "Newcomer");
        var n = Assert.Single(detector.ObserveFriends(Friends(open, newcomer, outgoing)));
        Assert.Equal((PushEvent.FriendRequest, "Newcomer wants to be friends"), (n.Event, n.Body));
        Assert.DoesNotContain("note", n.ToPayload());
        Assert.DoesNotContain("puuid", n.ToPayload());

        // Dropping out of one update and coming back doesn't notify again; two at once make one notification.
        Assert.Empty(detector.ObserveFriends(Friends(open)));
        Assert.Empty(detector.ObserveFriends(Friends(open, newcomer)));
        var two = Assert.Single(detector.ObserveFriends(Friends(Incoming("puuid-3", "A"), Incoming("puuid-4", "B"))));
        Assert.Equal("2 new friend requests", two.Body);
    }

    [Fact]
    public void Friend_requests_open_when_the_league_client_comes_back_do_not_notify()
    {
        var detector = new PushTriggerDetector(_time);
        detector.ObserveFriends(Friends());
        Assert.Empty(detector.ObserveFriends(FriendsState.Empty())); // the client closed: the connector publishes an empty list
        Assert.Empty(detector.ObserveFriends(Friends(Incoming("puuid-1", "Friend"))));

        detector.Forget(); // every phone unpaired
        Assert.Empty(detector.ObserveFriends(Friends(Incoming("puuid-1", "Friend"), Incoming("puuid-2", "Other"))));
    }

    [Fact]
    public async Task Each_subscription_gets_only_the_events_its_preferences_allow()
    {
        var (store, sender, notifier) = Notifier();
        PushSamples.Register(store, 1); // no preferences: defaults
        PushSamples.Register(store, 2);
        Assert.True(store.SetPreferences(PushSamples.Endpoint(2),
            new PushPreferences { MatchFound = false, ChampSelectStarted = true, FriendRequest = true }, PushSamples.Owner));

        await notifier.OnStatePublished(PushSamples.State("Matchmaking"));
        await notifier.OnStatePublished(PushSamples.State("ReadyCheck"));
        await notifier.OnStatePublished(PushSamples.State("ChampSelect"));
        await notifier.OnFriendsPublished(Friends());
        await notifier.OnFriendsPublished(Friends(Incoming("puuid-1", "Friend")));

        Assert.Equal(
            [
                (PushSamples.Endpoint(1), PushEvent.MatchFound),
                (PushSamples.Endpoint(2), PushEvent.ChampSelectStarted),
                (PushSamples.Endpoint(2), PushEvent.FriendRequest),
            ],
            sender.Sent.Select(s => (s.Subscription.Endpoint, s.Notification.Event)));
    }

    [Fact]
    public void Preferences_persist_and_survive_re_registration()
    {
        var store = new PushSubscriptionStore(_time, _dir);
        PushSamples.Register(store, 1);
        var prefs = new PushPreferences { GameStarted = false, FriendRequest = true };
        Assert.False(store.SetPreferences(PushSamples.Endpoint(9), prefs, PushSamples.Owner));
        Assert.True(store.SetPreferences(PushSamples.Endpoint(1), prefs, PushSamples.Owner));

        // The phone registers again on every start (with new keys after a re-subscribe, too).
        PushSamples.Register(store, 1);
        Assert.Equal(prefs, Assert.Single(store.GetAll()).Preferences);
        Assert.Equal(prefs, Assert.Single(new PushSubscriptionStore(_time, _dir).GetAll()).Preferences);
    }

    [Fact]
    public async Task A_test_push_goes_to_that_subscription_at_most_once_per_interval()
    {
        var (store, sender, notifier) = Notifier();
        Assert.NotNull(await notifier.SendTestAsync(PushSamples.Endpoint(1), PushSamples.Owner)); // not registered
        PushSamples.Register(store, 1);
        PushSamples.Register(store, 2);

        Assert.Null(await notifier.SendTestAsync(PushSamples.Endpoint(1), PushSamples.Owner));
        Assert.NotNull(await notifier.SendTestAsync(PushSamples.Endpoint(1), PushSamples.Owner));
        _time.Advance(HubMethods.Server.TestPushInterval);
        Assert.Null(await notifier.SendTestAsync(PushSamples.Endpoint(1), PushSamples.Owner));

        Assert.Equal(2, sender.Sent.Count);
        Assert.All(sender.Sent, s =>
            Assert.Equal((PushSamples.Endpoint(1), PushNotification.TestTag), (s.Subscription.Endpoint, s.Notification.Tag)));
    }
}

public sealed class PushPreferencesHubTests
{
    [Fact]
    public async Task A_phone_that_turned_match_found_off_gets_no_push_for_it_and_can_send_itself_a_test()
    {
        await using var server = await PhoneServerHost.StartAsync();
        await using var phone = await server.StartPhoneAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            phone.InvokeAsync<bool>(HubMethods.Server.SetPushPreferences, PushSamples.Endpoint(7), new PushPreferences()));

        await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, server.Pairing.Code);

        Assert.False(await phone.InvokeAsync<bool>(HubMethods.Server.SetPushPreferences, PushSamples.Endpoint(7), new PushPreferences()));
        await phone.InvokeAsync(HubMethods.Server.RegisterPushSubscription, PushSamples.Endpoint(7), PushSamples.P256dh(), PushSamples.Auth());
        Assert.True(await phone.InvokeAsync<bool>(HubMethods.Server.SetPushPreferences, PushSamples.Endpoint(7),
            new PushPreferences { MatchFound = false, ChampSelectStarted = true }));

        await PushHubTests.StatesAsync(server.Get<StateStore>(), phone, "Matchmaking", "ReadyCheck", "ChampSelect");
        await server.Push.FirstSend.Task.WaitAsync(PhoneServerHost.Timeout);
        lock (server.Push.Sent)
            Assert.Equal(PushEvent.ChampSelectStarted, Assert.Single(server.Push.Sent).Notification.Event);

        Assert.True(await phone.InvokeAsync<bool>(HubMethods.Server.SendTestPush, PushSamples.Endpoint(7)));
        var throttled = await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<bool>(HubMethods.Server.SendTestPush, PushSamples.Endpoint(7)));
        Assert.Contains("Wait", throttled.Message);
        lock (server.Push.Sent)
            Assert.Equal(PushNotification.TestTag, server.Push.Sent[^1].Notification.Tag);
    }
}
