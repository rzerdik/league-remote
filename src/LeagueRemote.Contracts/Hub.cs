namespace LeagueRemote.Contracts;

/// <summary>SignalR hub route and method names shared by the connector and the PWA.</summary>
public static class HubMethods
{
    public const string Path = "/hub";

    /// <summary>Methods phones invoke on the PC.</summary>
    public static class Server
    {
        /// <summary>
        /// (string code) → <see cref="PairingGrant"/>. Pairs this phone with the code the PC shows (tray, QR). The code is
        /// reusable until the PC rotates it (restart, tray "Unpair All Phones", too many wrong guesses), so more phones can
        /// pair with it. Signs the connection in like <see cref="Authenticate"/>. Failed attempts are rate-limited.
        /// </summary>
        public const string RedeemPairingCode = nameof(RedeemPairingCode);

        /// <summary>Query parameter carrying the pairing code in the QR / deep link: <c>{pwaUrl}/?pair=123456</c>.</summary>
        public const string PairQueryParameter = "pair";

        /// <summary>
        /// (string token) → <see cref="PairingGrant"/>. Signs the connection in: the caller gets every state slice, then
        /// every update.
        /// </summary>
        public const string Authenticate = nameof(Authenticate);

        /// <summary>
        /// Signed in: () → bool. Revokes the token this connection signed in with (this phone only; other phones stay paired).
        /// Every other connection signed in with it is disconnected; its next <see cref="Authenticate"/> fails with "Unknown or
        /// revoked token", which is how phones learn they were unpaired (also after the tray's "Unpair All Phones"). The caller
        /// is signed out but stays connected. False when the token was already gone.
        /// </summary>
        public const string Unpair = nameof(Unpair);

        /// <summary>Signed in: (<see cref="RemoteCommand"/>) → <see cref="CommandResult"/>. Whitelist-validated and run on the PC.</summary>
        public const string SendCommand = nameof(SendCommand);

        /// <summary>Signed in: () → string. The PC's VAPID public key (base64url, uncompressed P-256 point) for <c>PushManager.subscribe</c>.</summary>
        public const string GetPushPublicKey = nameof(GetPushPublicKey);

        /// <summary>
        /// Signed in: (string endpoint, string p256dh, string auth). Stores a Web Push subscription (idempotent per endpoint; at
        /// most <see cref="MaxPushSubscriptionsPerPairing"/>, the oldest is dropped). The PC then sends "Match found" / "Game
        /// started" / "Your turn" / … notifications to it, as its <see cref="PushPreferences"/> allow
        /// (<see cref="SetPushPreferences"/>). It goes when the phone that registered it is unpaired.
        /// </summary>
        public const string RegisterPushSubscription = nameof(RegisterPushSubscription);

        /// <summary>
        /// Signed in: (string endpoint). Removes that subscription, if this phone (the token it signed in with) registered
        /// it. The methods below are bound the same way: a subscription registered by another phone is treated as not
        /// registered. (Registering again with a new token takes it over.)
        /// </summary>
        public const string UnregisterPushSubscription = nameof(UnregisterPushSubscription);

        /// <summary>
        /// Signed in: (string endpoint, bool foreground). Whether this phone has the app open and visible; while it does, the PC
        /// sends no pushes to that subscription (the user is looking at the app). Tied to this connection (forgotten when it
        /// closes) and expires 75 s after the last <c>true</c>, so the phone repeats <c>true</c> every 30 s while visible.
        /// Throws when this phone hasn't registered the endpoint (register first, which the app does after every sign-in).
        /// </summary>
        public const string SetPushForeground = nameof(SetPushForeground);

        /// <summary>
        /// Signed in: (string endpoint, <see cref="PushPreferences"/>) → bool. Which events push to that subscription; stored
        /// with it (kept when it is registered again) and checked before every push. False when this phone hasn't registered
        /// the endpoint (register first). Subscriptions without preferences use <see cref="PushPreferences.Default"/>.
        /// </summary>
        public const string SetPushPreferences = nameof(SetPushPreferences);

        /// <summary>
        /// Signed in: (string endpoint) → bool. Sends a test notification to that subscription right away, even with the app
        /// open (the user asked for it). True when the push service accepted it. Throws when this phone hasn't registered the
        /// endpoint or it is asked again within <see cref="TestPushInterval"/>.
        /// </summary>
        public const string SendTestPush = nameof(SendTestPush);

        /// <summary>At most one test notification per subscription this often.</summary>
        public static readonly TimeSpan TestPushInterval = TimeSpan.FromSeconds(10);

        public const int MaxPushSubscriptionsPerPairing = 5;

        public const int MaxPushEndpointLength = 1024;
    }

    /// <summary>Methods the PC invokes on signed-in phones.</summary>
    public static class Client
    {
        /// <summary>(<see cref="ClientState"/>).</summary>
        public const string StateUpdated = nameof(StateUpdated);

        /// <summary>(<see cref="ChatState"/>). Never pushed.</summary>
        public const string ChatUpdated = nameof(ChatUpdated);

        /// <summary>
        /// (<see cref="FriendsState"/>). Throttled by the PC (presence churns). Only a new incoming friend request may push
        /// (<see cref="PushPreferences.FriendRequest"/>, opt-in, the sender's game name only).
        /// </summary>
        public const string FriendsUpdated = nameof(FriendsUpdated);

        /// <summary>
        /// (<see cref="ProfileState"/>). Sent after the local player's games, rank or mastery change and on
        /// <see cref="CommandType.RefreshProfile"/> / <see cref="CommandType.LoadMatchDetails"/>. Never pushed.
        /// </summary>
        public const string ProfileUpdated = nameof(ProfileUpdated);

        /// <summary>
        /// (<see cref="LiveGameState"/>). Every few seconds during a game and once as <see cref="LiveGameState.Empty"/> when the
        /// game ends. Never pushed.
        /// </summary>
        public const string LiveGameUpdated = nameof(LiveGameUpdated);

        /// <summary>
        /// (<see cref="AutoPickState"/>). Sent when the presets change and a few times per champ select (pending auto action,
        /// log). Never pushed.
        /// </summary>
        public const string AutoPickUpdated = nameof(AutoPickUpdated);
    }
}
