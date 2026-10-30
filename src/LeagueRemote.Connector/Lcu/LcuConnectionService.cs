using System.Collections.Concurrent;
using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.State;
using LeagueRemote.Connector.Tray;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Lcu;

/// <summary>Access to the currently connected League client, if any.</summary>
internal interface ILcuSession
{
    /// <summary>Null while no client is connected.</summary>
    LcuClient? Client { get; }

    /// <summary>
    /// Raised (on a refresh thread) when the published gameflow phase changes, also to "no client" on a disconnect. Read the
    /// phase from <see cref="StateStore"/>.
    /// </summary>
    event Action? GameflowPhaseChanged;

    /// <summary>Schedules a (debounced) state rebuild.</summary>
    void RequestRefresh();

    /// <summary>Schedules a (debounced) chat rebuild.</summary>
    void RequestChatRefresh();

    /// <summary>Schedules a (throttled) friend list rebuild.</summary>
    void RequestFriendsRefresh();

    /// <summary>
    /// Lists this direct conversation in chat even when it is not among the most recent ones (the phone just opened it),
    /// until the client reconnects, and schedules a chat rebuild.
    /// </summary>
    void KeepConversation(string conversationId);
}

/// <summary>
/// Discovery → connect → subscribe to events → rebuild slices on change; when the client goes away (exit or restart change
/// port and password) it publishes empty slices and starts over. Each slice has its own <see cref="RefreshLoop"/>, woken
/// only by its own events (<see cref="RefreshesFor"/>), so chatty presence or champ-select events don't rebuild the rest.
/// </summary>
internal sealed class LcuConnectionService(
    LcuDiscovery discovery,
    LcuEventSocket eventSocket,
    StateStore stateStore,
    ChatStore chatStore,
    FriendsStore friendsStore,
    ProfileStore profileStore,
    AutoPickService autoPick,
    ConnectorStatus status,
    TimeProvider time,
    ILogger<LcuConnectionService> logger) : BackgroundService, ILcuSession
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2.5);

    /// <summary>Presence churns (every friend's game, queue and status change is an event): one rebuild per interval.</summary>
    internal static readonly TimeSpan FriendsInterval = TimeSpan.FromSeconds(3);

    /// <summary>A game's end fires ranked, mastery, summoner and end-of-game events within a few seconds: one reread.</summary>
    internal static readonly TimeSpan ProfileDebounce = TimeSpan.FromSeconds(2);

    /// <summary>The refresh loops of the current connection; null while no client is connected.</summary>
    private sealed record Loops(RefreshLoop State, RefreshLoop Chat, RefreshLoop Friends, RefreshLoop Profile, RefreshLoop PostGame);

    private volatile Loops? _loops;
    private volatile LcuClient? _client;
    private readonly ConcurrentDictionary<string, byte> _keptConversations = new();

    /// <summary>The gameflow phase of the previous state rebuild (null right after connecting).</summary>
    private string? _lastPhase;

    /// <summary>The last stale champ-select timer anchor logged (<see cref="ChampSelectTimer.StaleAnchor"/>): once per anchor.</summary>
    private double _loggedStaleTimerAnchor;

    public LcuClient? Client => _client;

    public event Action? GameflowPhaseChanged;

    public void RequestRefresh() => _loops?.State.Request();

    public void RequestChatRefresh() => _loops?.Chat.Request();

    public void RequestFriendsRefresh() => _loops?.Friends.Request();

    public void KeepConversation(string conversationId)
    {
        _keptConversations[conversationId] = 0;
        RequestChatRefresh();
    }

    /// <summary>
    /// Which rebuilds an LCU event needs: chat events only touch chat, friend events only friends, profile events (ranked,
    /// mastery, summoner) only the profile; the end-of-game stats both the state (post-game summary) and the profile; lobby
    /// and phase changes move the party / draft rooms. Everything else (the post-game LP and mastery updates too) is state.
    /// </summary>
    internal static (bool State, bool Chat, bool Friends, bool Profile) RefreshesFor(string topic) => topic switch
    {
        LcuTopics.Chat => (false, true, false, false),
        LcuTopics.Friends or LcuTopics.FriendGroups or LcuTopics.FriendRequests => (false, false, true, false),
        LcuTopics.RankedStats or LcuTopics.Mastery or LcuTopics.CurrentSummoner => (false, false, false, true),
        LcuTopics.EndOfGameStats => (true, false, false, true),
        LcuTopics.GameflowPhase or LcuTopics.Lobby => (true, true, false, false),
        _ => (true, false, false, false),
    };

    private void OnEvent(string topic)
    {
        if (_loops is not { } loops)
            return;
        var (state, chat, friends, profile) = RefreshesFor(topic);
        if (state)
            loops.State.Request();
        if (chat)
            loops.Chat.Request();
        if (friends)
            loops.Friends.Request();
        if (profile)
            loops.Profile.Request();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            status.SetLcuStatus("Waiting for League client…");
            var credentials = await discovery.WaitForClientAsync(ct);

            using var http = LcuHttp.CreateClient(credentials);
            var client = new LcuClient(http);
            try
            {
                await client.GetGameflowPhaseAsync(ct); // stale lockfile → fails here
            }
            catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
            {
                logger.LogDebug(ex, "League client at {Credentials} not reachable yet", credentials);
                await Task.Delay(ReconnectDelay, time, ct);
                continue;
            }

            logger.LogInformation("Connected to League client {Credentials}", credentials);
            status.SetLcuStatus("League client connected");
            _client = client;
            try
            {
                await RunConnectedAsync(credentials, client, ct);
            }
            finally
            {
                _loops = null;
                _client = null;
                stateStore.Set(ClientState.Disconnected());
                chatStore.Set(ChatState.Empty());
                friendsStore.Set(FriendsState.Empty());
                profileStore.Set(ProfileState.Empty(), readAt: null);
                autoPick.OnDisconnected();
                _lastPhase = null;
                GameflowPhaseChanged?.Invoke();
            }

            if (!ct.IsCancellationRequested)
            {
                logger.LogInformation("League client connection lost; rediscovering");
                await Task.Delay(ReconnectDelay, time, ct);
            }
        }
    }

    private async Task RunConnectedAsync(LcuCredentials credentials, LcuClient client, CancellationToken ct)
    {
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = connectionCts.Token;
        _lastPhase = null;
        _keptConversations.Clear();

        var gameState = new GameStateReader(client, new PlayerNameResolver(client, logger, time), new ChampionCatalog(client));
        var chat = new ChatReader(client, time, () => _keptConversations.Keys.ToHashSet());
        Loops? loops = null;
        loops = new Loops(
            new RefreshLoop("State refresh", Debounce, TimeSpan.Zero, t => RebuildStateAsync(client, gameState, loops!, t), time, logger),
            new RefreshLoop("Chat refresh", Debounce, TimeSpan.Zero, async t => chatStore.Set(await chat.BuildAsync(t)), time, logger),
            new RefreshLoop("Friends refresh", Debounce, FriendsInterval,
                async t => friendsStore.Set(await FriendsReader.ReadAsync(client, time.GetUtcNow(), t)), time, logger),
            new RefreshLoop("Profile refresh", ProfileDebounce, TimeSpan.Zero, t => ProfileReader.ReadAsync(client, profileStore, time, t), time, logger),
            // Requested once per finished game (GameJustEnded); handles its own LCU errors.
            new RefreshLoop("Post-game watch", TimeSpan.Zero, TimeSpan.Zero,
                t => ProfileReader.WatchForNewGameAsync(client, profileStore, time, logger, t), time, logger));
        _loops = loops;

        Task[] tasks =
        [
            eventSocket.RunAsync(credentials, OnEvent, token),
            loops.State.RunAsync(token),
            loops.Chat.RunAsync(token),
            loops.Friends.RunAsync(token),
            loops.Profile.RunAsync(token),
            loops.PostGame.RunAsync(token),
        ];
        loops.State.Request();
        loops.Chat.Request();
        loops.Friends.Request();
        loops.Profile.Request();

        var finished = await Task.WhenAny(tasks);
        if (finished.IsFaulted && !ct.IsCancellationRequested)
            logger.LogInformation("LCU connection ended: {Reason}", finished.Exception?.GetBaseException().Message);

        await connectionCts.CancelAsync();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Already logged above; we are reconnecting anyway.
        }
    }

    /// <summary>Reads and publishes the state, hands the raw reads to the auto pick and starts the post-game watch.</summary>
    private async Task RebuildStateAsync(LcuClient client, GameStateReader reader, Loops loops, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(ct);
        var now = time.GetUtcNow();
        var state = GameStateMapper.Build(snapshot, now);
        if (ChampSelectMapper.StaleTimerAnchor(snapshot.ChampSelectSession, now) is { } anchor && anchor != _loggedStaleTimerAnchor)
        {
            _loggedStaleTimerAnchor = anchor;
            logger.LogDebug(
                "Champ select timer anchor is {AgeSeconds:F1} s old, longer than the whole phase: PC clock and League's timer disagree, the phone shows no timer and auto-lock only hovers",
                (now.ToUnixTimeMilliseconds() - anchor) / 1000);
        }

        stateStore.Set(state);
        autoPick.Observe(client, snapshot);
        if (GameflowPhases.GameJustEnded(_lastPhase, state.GameflowPhase))
            loops.PostGame.Request();
        var phaseChanged = _lastPhase != state.GameflowPhase;
        _lastPhase = state.GameflowPhase;
        if (phaseChanged)
            GameflowPhaseChanged?.Invoke();
    }
}
