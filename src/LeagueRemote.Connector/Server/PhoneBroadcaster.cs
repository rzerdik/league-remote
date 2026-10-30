using System.Threading.Channels;
using LeagueRemote.Connector.Push;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace LeagueRemote.Connector.Server;

/// <summary>
/// The only sender of state slices to phones: on every store change it sends each slice whose version moved to the signed-in
/// phones, feeds <see cref="ClientState"/> and <see cref="FriendsState"/> to push, and replays every slice to a phone that just
/// signed in. One loop, so everything a connection receives goes out in the order it was read (a replay can never overtake
/// a newer fan-out), and bursts coalesce: only the latest value per wake is sent.
/// </summary>
internal sealed class PhoneBroadcaster : BackgroundService
{
    private sealed class Slice(string method, Func<(object Value, long Version)> snapshot, Action<object>? onNew = null)
    {
        public string Method { get; } = method;
        public Func<(object Value, long Version)> Snapshot { get; } = snapshot;
        public Action<object>? OnNew { get; } = onNew;
        public long Sent { get; set; } = -1;
    }

    private readonly IHubContext<PhoneHub> _hub;
    private readonly StateStore _state;
    private readonly ChatStore _chat;
    private readonly FriendsStore _friends;
    private readonly ProfileStore _profile;
    private readonly LiveGameStore _liveGame;
    private readonly AutoPickStore _autoPick;
    private readonly ILogger<PhoneBroadcaster> _logger;
    private readonly IReadOnlyList<Slice> _slices;

    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Channel<string> _replays = Channel.CreateUnbounded<string>();

    public PhoneBroadcaster(
        IHubContext<PhoneHub> hub,
        StateStore state,
        ChatStore chat,
        FriendsStore friends,
        ProfileStore profile,
        LiveGameStore liveGame,
        AutoPickStore autoPick,
        PushNotifier push,
        ILogger<PhoneBroadcaster> logger)
    {
        _hub = hub;
        _state = state;
        _chat = chat;
        _friends = friends;
        _profile = profile;
        _liveGame = liveGame;
        _autoPick = autoPick;
        _logger = logger;
        // Push sees every new state whether or not a phone is connected: that is the point of push.
        _slices =
        [
            new Slice(HubMethods.Client.StateUpdated, () => state.Snapshot(), v => _ = push.OnStatePublished((ClientState)v)),
            new Slice(HubMethods.Client.ChatUpdated, () => chat.Snapshot()),
            new Slice(HubMethods.Client.FriendsUpdated, () => friends.Snapshot(), v => _ = push.OnFriendsPublished((FriendsState)v)),
            new Slice(HubMethods.Client.ProfileUpdated, () => profile.Snapshot()),
            new Slice(HubMethods.Client.LiveGameUpdated, () => liveGame.Snapshot()),
            new Slice(HubMethods.Client.AutoPickUpdated, () => autoPick.Snapshot()),
        ];
    }

    /// <summary>Queues a replay of every slice to one connection (it just signed in). Safe from any thread.</summary>
    public void Replay(string connectionId)
    {
        _replays.Writer.TryWrite(connectionId);
        Wake();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _state.Changed += Wake;
        _chat.Changed += Wake;
        _friends.Changed += Wake;
        _profile.Changed += Wake;
        _liveGame.Changed += Wake;
        _autoPick.Changed += Wake;
        try
        {
            Wake(); // the current values are the push baseline
            while (await _wake.Reader.WaitToReadAsync(ct))
            {
                _wake.Reader.TryRead(out _);
                await SendChangesAsync(ct);
                while (_replays.Reader.TryRead(out var connectionId))
                    await ReplayAsync(connectionId, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _state.Changed -= Wake;
            _chat.Changed -= Wake;
            _friends.Changed -= Wake;
            _profile.Changed -= Wake;
            _liveGame.Changed -= Wake;
            _autoPick.Changed -= Wake;
        }
    }

    /// <summary>Store events fire on the LCU and poller threads: only signal here, never send.</summary>
    private void Wake() => _wake.Writer.TryWrite(true);

    private async Task SendChangesAsync(CancellationToken ct)
    {
        foreach (var slice in _slices)
        {
            var (value, version) = slice.Snapshot();
            if (version == slice.Sent)
                continue;
            slice.Sent = version;
            slice.OnNew?.Invoke(value);
            await SendAsync(_hub.Clients.Group(PhoneHub.Phones), slice.Method, value, ct);
        }
    }

    private async Task ReplayAsync(string connectionId, CancellationToken ct)
    {
        var client = _hub.Clients.Client(connectionId);
        foreach (var slice in _slices)
            await SendAsync(client, slice.Method, slice.Snapshot().Value, ct);
    }

    /// <summary>A failed send (a phone's socket closing) must never end the loop: a faulted background service stops the app.</summary>
    private async Task SendAsync(IClientProxy clients, string method, object value, CancellationToken ct)
    {
        try
        {
            await clients.SendAsync(method, value, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Could not send {Method} to phones: {Message}", method, ex.Message);
        }
    }
}
