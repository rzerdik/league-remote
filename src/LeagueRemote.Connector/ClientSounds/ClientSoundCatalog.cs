namespace LeagueRemote.Connector.ClientSounds;

/// <summary>
/// Which League client sound or music track the phone plays for which event. ADJUST HERE after a client patch renames a file.
///
/// <para><b>Where the files come from.</b> Two places, both in the user's own installation; nothing is ever copied into
/// the repository (extracted files are cached in the connector's local data folder):</para>
/// <list type="bullet">
/// <item>Front-end URLs <c>/fe/{plugin}/{rest}</c>, e.g. <c>/fe/lol-champ-select/sounds/sfx-cs-draft-notif-yourpick.ogg</c>.
/// Those are only resolved inside the client's own browser process: the LCU's HTTPS API answers <c>/fe/...</c> with 404
/// "Invalid URI format" (checked 2026-10-22). So the connector reads them from
/// <c>{InstallPath}\Plugins\rcp-fe-{plugin}\assets.wad</c> under <c>plugins/rcp-fe-{plugin}/global/default/{rest}</c>
/// (lower case, xxHash64-hashed; see <see cref="WadArchive"/>).</item>
/// <item>Game-data assets <c>/lol-game-data/assets/...</c> (per-map music and ambience: the <c>assets</c> of
/// <c>GET /lol-maps/v2/maps</c>, e.g. <c>champ-select-background-sound</c>, <c>music-inqueue-loop-sound</c>). Those sit
/// zstd-compressed in <c>Plugins/rcp-be-lol-game-data/default-assets2.wad</c>, which .NET can't inflate, but the LCU
/// serves them itself (<c>GET /lol-game-data/assets/content/src/LeagueClient/GameModeAssets/...</c> → 200, Ogg data;
/// verified read-only against a live client, 2026-10-22), so they are fetched once through
/// <c>LcuClient.GetGameDataAudioAsync</c> while the client runs and then served from the cache.</item>
/// </list>
///
/// <para><b>How the events and timings were found</b> (2026-10-22, client 16.21): unpack the JS bundles in the plugin WADs
/// and read where each <c>.ogg</c> is played:</para>
/// <list type="bullet">
/// <item><b>Champ select</b> (<c>rcp-fe-lol-champ-select</c>): <c>soundOptionsByEventType</c> maps the LCU's
/// <c>/lol-champ-select/v1/sfx-notifications</c> events to files ("my-turn-to-pick", "my-turn-to-ban",
/// "ten-bans-my-ban-completed", "ten-bans-ally-ban-completed", "serial-bans-ally/enemy-ban-completed",
/// "draft-pick-locked-in-my/their-team-single", "draft-my/their-team-first-pick-notif", "draft-my-pick-sniped"). The
/// <c>timer-status</c> component plays <c>playActionTypeTransitionSounds</c> (pick-intro when the active action becomes a
/// pick, 10ban-intro when it becomes someone else's ban) and its <c>timerTickObserver</c> plays the countdown ticks on every
/// change of <c>timer.timeRemaining</c> (= <c>Math.floor(ms / 1000)</c>):
/// <c>sfx-cs-timer-tick.ogg</c> while it is <b>your own</b> pick or ban in BAN_PICK and the value is ≤
/// <c>DURATIONS.actionSoundThreshold</c> (10; draft modes read <c>LcuChampionSelect.DraftActionTickSoundThreshold</c>,
/// which is 10 on the live platform config) — the same sound for pick and ban, once per second from "10" down to "0", in
/// step with the timer's <c>timer-pulse</c> class; <c>sfx-cs-timer-tick-small.ogg</c> for everyone at ≤ 5 in PLANNING,
/// ≤ 10 in FINALIZATION, and ≤ 5 in BAN_PICK when <c>allowSubsetChampionPicks</c>. There is no separate expiry sound.
/// The swap/trade dialogs play <c>sfx-cs-notif-*swaprequest-*</c>; bench modes play <c>sfx-champ-select-bench-update</c>
/// when a teammate takes from or adds to the bench and <c>sfx-cs-button-reroll-click</c> on Reroll.</item>
/// <item><b>Champ select music</b> (same plugin, the music mixin): draft-like modes (draft, blind with bans, random with
/// bans) layer <c>music-cs-draft-*</c> tracks by phase and by how many bans / picks are done (see
/// <c>web/src/sound/draftMusic.ts</c>); other modes loop the map's <c>champ-select-background-sound</c>.</item>
/// <item><b>Ready check</b> (<c>rcp-fe-lol-navigation</c>): intro → <c>sfx-readycheck-lowspec-intro</c>; in progress →
/// <c>sfx-readycheck-ringmagic-countdown</c> plus the map's <c>ready-check-background-sound</c> portal; accepted → the
/// countdown fades into <c>sfx-readycheck-ringmagic-accepted-loop</c>; success / failure outro →
/// <c>sfx-readycheck-lowspec-outro-accepted</c> / <c>sfx-readycheck-outro-declined</c>.</item>
/// <item><b>Lobby</b> (<c>rcp-fe-lol-parties</c>): find match / quit buttons, the party leader grant
/// (<c>sfx-lobby-notif-captain-granted</c>), a sent invite (<c>sfx-parties-member-banner-invited</c>), a member arriving
/// (their banner intro, <c>sfx-parties-member-banner-intro_0{partySize}</c>, all the same file), the lobby ambience
/// (<c>sfx-ambience-pregame-loop-sound</c>) and the in-queue music (<c>music-inqueue-loop-sound</c>, Matchmaking and
/// ReadyCheck; stopped in champ select).</item>
/// <item><b>Social</b> (<c>rcp-fe-lol-social</c>, files under <c>/fe/lol-social/sounds</c>): <c>chatMessage</c>
/// (sfx-soc-notif-chat-rcvd), <c>friendRequest</c>, <c>gameInvite</c>, and <c>leftChatRoom</c> (a member leaving the
/// lobby's chat room; joining has no sound).</item>
/// <item><b>Home</b> (<c>rcp-fe-lol-navigation</c>, <c>CLIENT_AMBIENCE.AC_MAIN</c>): <c>music-ac-home-main.ogg</c>, looped.</item>
/// </list>
///
/// <para><b>No client sound</b> (the phone synthesizes these, see <c>web/src/sound/synth.ts</c>): the game finishing
/// loading (<see cref="Sounds"/>' empty <c>gameStart</c>) and sending a chat message (the client is silent there; the phone
/// keeps a very quiet confirmation).</para>
///
/// <para>Each key lists candidates in order of preference; the first one found is used. Every path was confirmed to exist
/// (Ogg data) in the 2026-10-22 install.</para>
/// </summary>
internal static class ClientSoundCatalog
{
    private const string ChampSelect = "/fe/lol-champ-select/sounds/";
    private const string StaticAssets = "/fe/lol-static-assets/sounds/";
    private const string Social = "/fe/lol-social/sounds/";
    /// <summary>Game-data assets the LCU serves itself (map music, ambience, ready check portals).</summary>
    private const string MapAssets = Lcu.LcuClient.GameDataAssetsPrefix + "content/src/LeagueClient/GameModeAssets/";

    /// <summary>Short UI sounds: the phone downloads and decodes all of them up front.</summary>
    public static IReadOnlyDictionary<string, string[]> Sounds { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // --- Ready check (rcp-fe-lol-navigation ready-check component) ---
        // "intro" state (match found); low-spec intro first, as the client plays it on the notifications channel.
        ["matchFound"] = [StaticAssets + "sfx-readycheck-lowspec-intro.ogg"],
        // "in-progress" state: the ring countdown (11.6 s, the whole ready check) and the map's portal sound.
        ["readyCountdown"] = [StaticAssets + "sfx-readycheck-ringmagic-countdown.ogg"],
        ["readyPortal"] = [MapAssets + "Classic_SRU/sound/sfx-readycheck-sr-portal.ogg"],
        ["readyPortalAram"] = [MapAssets + "ARAM/sound/sfx-readycheck-ha-portal.ogg"],
        // "Accept" / "Decline" buttons; "accepted" state loops until everyone answered.
        ["readyAccept"] = [StaticAssets + "sfx-readycheck-accept-button-click.ogg"],
        ["readyDecline"] = [StaticAssets + "sfx-readycheck-decline-button-click.ogg"],
        ["readyAcceptedLoop"] = [StaticAssets + "sfx-readycheck-ringmagic-accepted-loop.ogg"],
        // "success-outro" (everyone accepted) / "failure-outro" (someone declined or timed out).
        ["readySuccess"] = [StaticAssets + "sfx-readycheck-lowspec-outro-accepted.ogg"],
        ["readyFailed"] = [StaticAssets + "sfx-readycheck-outro-declined.ogg"],

        // --- Lobby (rcp-fe-lol-parties) ---
        // "Find match" button / leaving the queue (the navigation bar's circle-X button).
        ["queueStart"] = ["/fe/lol-parties/sfx-lobby-button-find-match-click.ogg"],
        ["queueCancel"] = ["/fe/lol-uikit/sfx-uikit-button-circlex-click.ogg", "/fe/lol-parties/sfx-lobby-button-quit-click.ogg"],
        // A member's banner intro when they join (sfx-parties-member-banner-intro_0{2..5}: one file under five names).
        ["memberJoined"] = [StaticAssets + "sfx-parties-member-banner-intro_05.ogg", StaticAssets + "sfx-parties-player-banner-intro.ogg"],
        // The lobby chat room's "left_room" message (rcp-fe-lol-social leftChatRoom).
        ["memberLeft"] = [Social + "sfx-soc-notif-player-leave-lobby.ogg"],
        // A new pending invitation from this lobby (pendingInvitesChanged).
        ["inviteSent"] = [StaticAssets + "sfx-parties-member-banner-invited.ogg"],
        // You became the party leader (_handlePartyLeaderGranted).
        ["leaderGranted"] = ["/fe/lol-parties/sfx-lobby-notif-captain-granted.ogg"],

        // --- Social (rcp-fe-lol-social aliases, under /fe/lol-social/sounds) ---
        ["inviteReceived"] = [Social + "sfx-soc-notif-gameinvite-rcvd.ogg"],
        ["chatMessage"] = [Social + "sfx-soc-notif-chat-rcvd.ogg"],
        ["friendRequest"] = [Social + "sfx-soc-notif-friendreq-rcvd.ogg"],

        // --- Champ select (rcp-fe-lol-champ-select) ---
        // playActionTypeTransitionSounds: the active action becomes someone else's ban / a pick.
        ["banPhase"] = [ChampSelect + "sfx-cs-draft-10ban-intro.ogg", ChampSelect + "sfx-cs-draft-ban-ring-intro.ogg"],
        ["pickPhase"] = [ChampSelect + "sfx-cs-draft-pick-intro.ogg"],
        // "my-turn-to-pick" / "my-turn-to-ban".
        ["yourTurn"] = [ChampSelect + "sfx-cs-draft-notif-yourpick.ogg"],
        ["yourBan"] = [ChampSelect + "sfx-cs-draft-notif-yourban.ogg"],
        // timerTickObserver: your own turn from "10" to "0" / everyone's small tick (planning, finalization, subset picks).
        ["timerTick"] = [ChampSelect + "sfx-cs-timer-tick.ogg"],
        ["timerTickSmall"] = [ChampSelect + "sfx-cs-timer-tick-small.ogg"],
        // Champion grid hover; "Lock in" button.
        ["hover"] = [ChampSelect + "sfx-uikit-grid-hover.ogg", StaticAssets + "sfx-uikit-grid-hover.ogg"],
        ["lockIn"] = [ChampSelect + "sfx-cs-lockin-button-click.ogg"],
        // "ten-bans-my-ban-completed" / "ten-bans-ally-ban-completed" (simultaneous bans).
        ["ban"] = [ChampSelect + "sfx-cs-draft-10ban-your-ban.ogg", ChampSelect + "sfx-cs-draft-ban-button-click.ogg"],
        ["allyBan"] = [ChampSelect + "sfx-cs-draft-10ban-ally-ban.ogg"],
        // "serial-bans-ally-ban-completed" / "serial-bans-enemy-ban-completed" (one ban after another, e.g. tournament draft).
        ["serialAllyBan"] = [ChampSelect + "sfx-cs-draft-ban-your-team.ogg"],
        ["serialEnemyBan"] = [ChampSelect + "sfx-cs-draft-ban-enemy-team.ogg"],
        // "draft-pick-locked-in-my-team-single" / "-their-team-single"; "draft-my-team-first-pick-notif" / "-their-team-".
        ["allyPick"] = [ChampSelect + "sfx-cs-draft-left-pick-single.ogg"],
        ["enemyPick"] = [ChampSelect + "sfx-cs-draft-right-pick-single.ogg"],
        ["allyFirstPick"] = [ChampSelect + "sfx-cs-draft-left-first-pick.ogg"],
        ["enemyFirstPick"] = [ChampSelect + "sfx-cs-draft-right-first-pick.ogg"],
        // "draft-my-pick-sniped": the champion you declared got banned.
        ["pickSniped"] = [ChampSelect + "sfx-cs-draft-10ban-your-pick-denied.ogg"],
        // Swap / trade request received (position swaps have their own), accepted, declined.
        ["swapRequest"] = [ChampSelect + "sfx-cs-notif-swaprequest-rcvd.ogg"],
        ["positionSwapRequest"] = [ChampSelect + "sfx-cs-notif-roleswaprequest-rcvd.ogg", ChampSelect + "sfx-cs-notif-swaprequest-rcvd.ogg"],
        ["swapAccepted"] = [ChampSelect + "sfx-cs-notif-swaprequest-accepted.ogg"],
        ["swapDeclined"] = [ChampSelect + "sfx-cs-notif-swaprequest-declined.ogg"],
        // Bench modes (ARAM): the champion-bench component's playSoundOnAllySwap (the bench changed, your champion didn't;
        // at most every CHAMPION_BENCH_SOUND_COOLDOWN_MS = 3 s) and the reroll button's click.
        ["benchUpdate"] = [ChampSelect + "sfx-champ-select-bench-update.ogg"],
        ["reroll"] = [ChampSelect + "sfx-cs-button-reroll-click.ogg"],

        // --- Elsewhere ---
        // No "game loaded" UI sound exists in the client plugins: the phone synthesizes one.
        ["gameStart"] = [],
        // Generic small click.
        ["click"] = ["/fe/lol-uikit/sfx-uikit-generic-click-small.ogg", StaticAssets + "sfx-uikit-button-generic-click.ogg"],
    };

    /// <summary>
    /// Music and ambience loops (1–5 MB each): the phone streams them only while music is on. Draft keys follow the champ
    /// select music mixin's <c>processDraftMusicChange</c>; map keys are Summoner's Rift (<c>CLASSIC</c>) and Howling Abyss
    /// (<c>ARAM</c>) from <c>/lol-maps/v2/maps</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Music { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // Home (CLIENT_AMBIENCE.AC_MAIN, 57 s loop).
        ["musicHome"] = ["/fe/lol-navigation/music-ac-home-main.ogg"],
        // Lobby: the map's sfx-ambience-pregame-loop-sound.
        ["ambienceLobby"] = [MapAssets + "Classic_SRU/sound/sfx-ambience-loop-summonersrift.ogg"],
        ["ambienceLobbyAram"] = [MapAssets + "ARAM/sound/sfx-ambience-loop-howlingabyss.ogg"],
        // Matchmaking / ready check: the map's music-inqueue-loop-sound.
        ["musicQueue"] = [MapAssets + "Classic_SRU/sound/music-inqueue-loop-summonersrift.ogg"],
        ["musicQueueAram"] = [MapAssets + "ARAM/sound/music-inqueue-loop-howlingabyss.ogg"],
        // Non-draft champ select: the map's champ-select-background-sound (ARAM's ends in sync with champ select), and
        // ARAM's champ-select-banphase-background-sound for "random with bans".
        ["musicBlindPick"] = [MapAssets + "Shared/sound/music-cs-blindpick-default.ogg"],
        ["musicAram"] = [MapAssets + "ARAM/sound/music-cs-allrandom-howlingabyss.ogg"],
        ["musicAramBan"] = [MapAssets + "ARAM/sound/music-cs-allrandom-banphase-howlingabyss.ogg"],
        // Draft champ select: planning, its transition into bans, ban layers, the ban → pick transition, pick layers,
        // and the finalization track (62 s, not looped, joined at 60 − seconds left).
        ["musicDraftIntent"] = [ChampSelect + "music-cs-draft-pickintent-01.ogg"],
        ["musicDraftIntentToBan"] = [ChampSelect + "music-cs-draft-pickintent-to-ban-trans-01.ogg"],
        ["musicDraftBan"] = [ChampSelect + "music-cs-draft-ban-base-layer-01.ogg"],
        ["musicDraftBanLayer1"] = [ChampSelect + "music-cs-draft-ban-intensity-layer-01.ogg"],
        ["musicDraftBanLayer2"] = [ChampSelect + "music-cs-draft-ban-intensity-layer-02.ogg"],
        ["musicDraftBanToPick"] = [ChampSelect + "music-cs-draft-ban-to-pick-trans-01.ogg"],
        ["musicDraftPick"] = [ChampSelect + "music-cs-draft-pick-base-layer-01.ogg"],
        ["musicDraftPickLayer1"] = [ChampSelect + "music-cs-draft-pick-intensity-layer-01.ogg"],
        ["musicDraftPickLayer2"] = [ChampSelect + "music-cs-draft-pick-intensity-layer-02.ogg"],
        ["musicDraftPickLayer3"] = [ChampSelect + "music-cs-draft-pick-intensity-layer-03.ogg"],
        ["musicDraftPickLayer4"] = [ChampSelect + "music-cs-draft-pick-intensity-layer-04.ogg"],
        ["musicDraftFinal"] = [ChampSelect + "music-cs-draft-finalization-60sec-01.ogg"],
    };

    /// <summary>Every key, sounds and music alike (keys are unique across both).</summary>
    public static IEnumerable<KeyValuePair<string, string[]>> All => Sounds.Concat(Music);

    public static bool Contains(string key) => Sounds.ContainsKey(key) || Music.ContainsKey(key);

    public static bool IsMusic(string key) => Music.ContainsKey(key);

    /// <summary>A source fetched from the running client rather than read from a WAD.</summary>
    public static bool IsGameDataAsset(string url) => Lcu.LcuClient.IsGameDataAudioPath(url);

    /// <summary>
    /// Maps a front-end URL to its WAD: <c>/fe/{plugin}/{rest}</c> → (<c>Plugins/rcp-fe-{plugin}/assets.wad</c>,
    /// <c>plugins/rcp-fe-{plugin}/global/default/{rest}</c>). Null when the URL doesn't have that shape.
    /// </summary>
    public static (string WadRelativePath, string EntryPath)? Locate(string frontEndUrl)
    {
        const string prefix = "/fe/";
        if (!frontEndUrl.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var rest = frontEndUrl[prefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1 || rest.Contains("..", StringComparison.Ordinal))
            return null;
        var plugin = "rcp-fe-" + rest[..slash];
        return (Path.Combine("Plugins", plugin, "assets.wad"), $"plugins/{plugin}/global/default/{rest[(slash + 1)..]}");
    }
}
