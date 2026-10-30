// Mirror of src/LeagueRemote.Contracts/Hub.cs.

/** HubMethods: the hub's route and method names. */
export const HubMethods = {
  path: '/hub',
  /** HubMethods.Server.PairQueryParameter — QR / deep link: `{pwaUrl}/?pair=123456`. */
  pairQueryParameter: 'pair',
  server: {
    /** (code) → PairingGrant — pairs with the code the PC shows and signs in like authenticate. */
    redeemPairingCode: 'RedeemPairingCode',
    /** (token) → PairingGrant — signs in: every state slice, then every update. */
    authenticate: 'Authenticate',
    /**
     * Signed in: () → boolean — revokes this phone's token on the PC (other phones stay paired). Other connections using
     * it are disconnected (their next authenticate gets "Unknown or revoked token", as after the PC's Unpair All Phones).
     */
    unpair: 'Unpair',
    sendCommand: 'SendCommand',
    /** () → string — the PC's VAPID public key (base64url) for PushManager.subscribe. */
    getPushPublicKey: 'GetPushPublicKey',
    /** (endpoint, p256dh, auth) → void — store this browser's Web Push subscription on the PC. */
    registerPushSubscription: 'RegisterPushSubscription',
    /** (endpoint) → void. */
    unregisterPushSubscription: 'UnregisterPushSubscription',
    /**
     * (endpoint, foreground) → void — while true the PC sends no pushes to that subscription. Tied to the connection and
     * expires 75 s after the last `true`, so repeat it (every 30 s) while visible.
     */
    setPushForeground: 'SetPushForeground',
    /**
     * (endpoint, PushPreferences) → boolean — which events push to that subscription; stored with it and checked before
     * every push. False when the endpoint isn't registered (register first). Send after every registration.
     */
    setPushPreferences: 'SetPushPreferences',
    /** (endpoint) → boolean — a test notification now, even with the app open; at most one per 10 s (throws otherwise). */
    sendTestPush: 'SendTestPush',
  },
  /** At most this many push subscriptions on the PC (it drops the oldest). */
  maxPushSubscriptionsPerPairing: 5,
  maxPushEndpointLength: 1024,
  client: {
    /** To phone: (ClientState). */
    stateUpdated: 'StateUpdated',
    /** To phone: (ChatState). */
    chatUpdated: 'ChatUpdated',
    /** To phone: (FriendsState). */
    friendsUpdated: 'FriendsUpdated',
    /** To phone: (ProfileState). */
    profileUpdated: 'ProfileUpdated',
    /** To phone: (LiveGameState). */
    liveGameUpdated: 'LiveGameUpdated',
    /** To phone: (AutoPickState). */
    autoPickUpdated: 'AutoPickUpdated',
  },
} as const
