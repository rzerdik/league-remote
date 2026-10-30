using System.Text.Json;
using System.Text.Json.Serialization;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Push;

/// <summary>The moment a notification is about; <see cref="PushEventExtensions.AllowedBy"/> checks it against a phone's preferences.</summary>
internal enum PushEvent
{
    MatchFound,
    ChampSelectStarted,
    YourTurn,
    GameStarted,
    LobbyInvite,
    FriendRequest,

    /// <summary>Asked for from Settings (<see cref="HubMethods.Server.SendTestPush"/>): always allowed.</summary>
    Test,
}

internal static class PushEventExtensions
{
    /// <summary>Whether <paramref name="preferences"/> (null = <see cref="PushPreferences.Default"/>) let this event push.</summary>
    public static bool AllowedBy(this PushEvent e, PushPreferences? preferences)
    {
        var p = preferences ?? PushPreferences.Default;
        return e switch
        {
            PushEvent.MatchFound => p.MatchFound,
            PushEvent.ChampSelectStarted => p.ChampSelectStarted,
            PushEvent.YourTurn => p.YourTurn,
            PushEvent.GameStarted => p.GameStarted,
            PushEvent.LobbyInvite => p.LobbyInvite,
            PushEvent.FriendRequest => p.FriendRequest,
            PushEvent.Test => true,
            _ => false,
        };
    }
}

/// <summary>A notification for the PWA's service worker. <see cref="ToPayload"/> is what gets encrypted and pushed.</summary>
/// <param name="TimeToLive">How long the push service keeps it for an offline phone; a stale "Match found" is useless.</param>
internal sealed record PushNotification(
    PushEvent Event,
    string Title,
    string Body,
    string Tag,
    TimeSpan TimeToLive,
    bool RequireInteraction = false,
    bool Renotify = true,
    string Url = "/")
{
    public const string ReadyCheckTag = "ready-check";
    public const string GameStartTag = "game-start";

    /// <summary>Champ select started and your turn share it, so the turn replaces "Champion select" on the phone.</summary>
    public const string ChampSelectTurnTag = "cs-turn";
    public const string LobbyInviteTag = "lobby-invite";
    public const string FriendRequestTag = "friend-request";
    public const string TestTag = "test";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Payload(
        string Title,
        string Body,
        string Tag,
        string Url,
        bool Renotify,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RequireInteraction);

    /// <summary><c>{ "title", "body", "tag", "url", "renotify", "requireInteraction"? }</c></summary>
    public string ToPayload() => JsonSerializer.Serialize(new Payload(Title, Body, Tag, Url, Renotify, RequireInteraction), Json);

    public static PushNotification MatchFound() =>
        new(PushEvent.MatchFound, "Match found", "Accept on your phone", ReadyCheckTag, TimeSpan.FromSeconds(15), RequireInteraction: true);

    public static PushNotification ChampSelectStarted() =>
        new(PushEvent.ChampSelectStarted, "Champion select", "Champion select has started", ChampSelectTurnTag, TimeSpan.FromSeconds(30));

    public static PushNotification GameStarted() =>
        new(PushEvent.GameStarted, "Game started", "The game has loaded", GameStartTag, TimeSpan.FromMinutes(2));

    public static PushNotification YourTurn(bool ban) => ban
        ? new(PushEvent.YourTurn, "Your turn to ban", "Pick a champion to ban", ChampSelectTurnTag, TimeSpan.FromSeconds(30))
        : new(PushEvent.YourTurn, "Your turn to pick", "Lock in your champion", ChampSelectTurnTag, TimeSpan.FromSeconds(30));

    /// <summary>"X invited you to Ranked Flex". Only the inviter's name and the queue: nothing a player typed.</summary>
    public static PushNotification LobbyInvite(ReceivedInvitation invitation) =>
        new(
            PushEvent.LobbyInvite,
            "Game invite",
            invitation.QueueName is { Length: > 0 } queue ? $"{invitation.FromRiotId} invited you to {queue}" : $"{invitation.FromRiotId} invited you",
            LobbyInviteTag,
            TimeSpan.FromMinutes(5));

    /// <summary>"X wants to be friends" (game name only, never the request's note), or a count for several at once.</summary>
    public static PushNotification FriendRequests(IReadOnlyList<FriendRequest> requests) =>
        new(
            PushEvent.FriendRequest,
            "Friend request",
            requests.Count == 1 ? $"{(string.IsNullOrWhiteSpace(requests[0].GameName) ? "Someone" : requests[0].GameName)} wants to be friends" : $"{requests.Count} new friend requests",
            FriendRequestTag,
            TimeSpan.FromMinutes(10));

    public static PushNotification Test() =>
        new(PushEvent.Test, "Notifications are on", "This is how League Remote will alert you", TestTag, TimeSpan.FromMinutes(1));
}

/// <summary>
/// Turns the stream of states the connector publishes into notifications. Only real transitions between two connected
/// states count, so replays never re-fire: the first state after the connector starts, a state right after the League
/// client (re)connected, and republishing the same state (RefreshState) all produce nothing.
/// <list type="bullet">
/// <item><b>Match found</b>: the gameflow phase enters <c>ReadyCheck</c> (at most once per <see cref="ReadyCheckDebounce"/>).</item>
/// <item><b>Champion select</b>: the gameflow phase enters <c>ChampSelect</c>, unless the local player's turn starts at once (blind
/// pick): then only "Your turn".</item>
/// <item><b>Game started</b>: <see cref="ClientState.InGame"/>.Loaded goes false/null → true (at most once per <see cref="GameStartDebounce"/>).</item>
/// <item><b>Your turn</b>: the local player's pick/ban action becomes in progress (once per action id per champ select).</item>
/// <item><b>Game invite</b>: an invitation id appears in <see cref="ClientState.Invitations"/> (once per id).</item>
/// <item><b>Friend request</b> (<see cref="ObserveFriends"/>): an incoming request from a puuid not seen since the friend list's
/// baseline (the first non-empty <see cref="FriendsState"/> after the connector or League client came back).</item>
/// </list>
/// Thread-safe.
/// </summary>
internal sealed class PushTriggerDetector(TimeProvider time)
{
    public static readonly TimeSpan ReadyCheckDebounce = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan GameStartDebounce = TimeSpan.FromSeconds(60);

    private sealed class Memory
    {
        public ClientState? Last;
        public DateTimeOffset LastReadyCheck = DateTimeOffset.MinValue;
        public DateTimeOffset LastGameStart = DateTimeOffset.MinValue;
        public long? LastTurnActionId;
        public HashSet<string> SeenInvitationIds = [];

        /// <summary>Incoming friend request puuids seen since the baseline; null until there is one.</summary>
        public HashSet<string>? SeenRequestPuuids;
    }

    private readonly Lock _lock = new();
    private Memory _memory = new();

    /// <summary>Every phone was unpaired: the next state is a fresh baseline for whoever pairs next.</summary>
    public void Forget()
    {
        lock (_lock)
            _memory = new Memory();
    }

    public IReadOnlyList<PushNotification> Observe(ClientState state)
    {
        lock (_lock)
        {
            var memory = _memory;
            var previous = memory.Last;
            memory.Last = state;
            var now = time.GetUtcNow();
            var turn = CurrentTurn(state);
            if (state.GameflowPhase != "ChampSelect")
                memory.LastTurnActionId = null;

            var invitations = state.Invitations ?? [];
            var newInvitations = invitations.Where(i => !memory.SeenInvitationIds.Contains(i.Id)).ToList();
            // Only ids still listed are remembered, so the set stays small (and a state without the list, e.g. in champ
            // select, forgets nothing that is still open in the client).
            if (state.Invitations is not null || !state.LcuConnected)
                memory.SeenInvitationIds = invitations.Select(i => i.Id).ToHashSet();

            if (previous is not { LcuConnected: true } || !state.LcuConnected)
            {
                // No baseline to compare with: remember, don't notify.
                memory.LastTurnActionId = turn?.Id ?? memory.LastTurnActionId;
                return [];
            }

            var notifications = new List<PushNotification>();

            if (state.GameflowPhase == "ReadyCheck" && previous.GameflowPhase != "ReadyCheck" && now - memory.LastReadyCheck >= ReadyCheckDebounce)
            {
                memory.LastReadyCheck = now;
                notifications.Add(PushNotification.MatchFound());
            }

            if (state.InGame is { Loaded: true } && previous.InGame is not { Loaded: true } && now - memory.LastGameStart >= GameStartDebounce)
            {
                memory.LastGameStart = now;
                notifications.Add(PushNotification.GameStarted());
            }

            var turnStarts = turn is not null && turn.Id != memory.LastTurnActionId;
            if (state.GameflowPhase == "ChampSelect" && previous.GameflowPhase != "ChampSelect" && !turnStarts)
                notifications.Add(PushNotification.ChampSelectStarted());

            if (turnStarts)
            {
                memory.LastTurnActionId = turn!.Id;
                notifications.Add(PushNotification.YourTurn(ban: turn.Type == "ban"));
            }

            notifications.AddRange(newInvitations.Select(PushNotification.LobbyInvite));

            return notifications;
        }
    }

    /// <summary>
    /// New incoming friend requests in a published friend list. An empty list (no friends and no groups: what the connector
    /// publishes while the League client is gone) drops the baseline, so requests that were already open when the client
    /// comes back don't notify. Puuids are remembered until <see cref="Forget"/>, so a request that drops out of one update
    /// and comes back never notifies twice.
    /// </summary>
    public IReadOnlyList<PushNotification> ObserveFriends(FriendsState friends)
    {
        lock (_lock)
        {
            var memory = _memory;
            if (friends.Friends.Count == 0 && friends.Groups.Count == 0)
            {
                memory.SeenRequestPuuids = null;
                return [];
            }

            var incoming = (friends.Requests ?? []).Where(r => r.Direction == FriendRequestDirections.Incoming).ToList();
            if (memory.SeenRequestPuuids is null)
            {
                memory.SeenRequestPuuids = incoming.Select(r => r.Puuid).ToHashSet();
                return [];
            }

            var fresh = incoming.Where(r => memory.SeenRequestPuuids.Add(r.Puuid)).ToList();
            return fresh.Count == 0 ? [] : [PushNotification.FriendRequests(fresh)];
        }
    }

    /// <summary>The local player's pick or ban action that is in progress right now, if any.</summary>
    private static PlayerAction? CurrentTurn(ClientState state) =>
        state is { GameflowPhase: "ChampSelect", ChampSelect.MyAction: { IsInProgress: true, Completed: false, Type: "pick" or "ban" } action }
            ? action
            : null;
}
