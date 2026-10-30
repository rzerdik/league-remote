using System.Net;
using System.Text.Json;
using LeagueRemote.Connector.ClientSounds;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class ClientSoundsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();

    private string Install => Path.Combine(_dir, "League of Legends");
    private string Cache => Path.Combine(_dir, "cache");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private void InstallChampSelectWad(byte[] yourPick)
    {
        FakeWad.Write(Path.Combine(Install, "Plugins", "rcp-fe-lol-champ-select", "assets.wad"),
            ("plugins/rcp-fe-lol-champ-select/global/default/sounds/sfx-cs-draft-notif-yourpick.ogg", yourPick, false),
            ("plugins/rcp-fe-lol-champ-select/global/default/sounds/sfx-cs-lockin-button-click.ogg", FakeWad.Ogg, true));
    }

    [Fact]
    public void Front_end_urls_map_to_the_plugin_wad()
    {
        Assert.Equal(
            (Path.Combine("Plugins", "rcp-fe-lol-static-assets", "assets.wad"), "plugins/rcp-fe-lol-static-assets/global/default/sounds/x.ogg"),
            ClientSoundCatalog.Locate("/fe/lol-static-assets/sounds/x.ogg"));
        Assert.Null(ClientSoundCatalog.Locate("/lol-game-data/assets/x.ogg"));
        Assert.Null(ClientSoundCatalog.Locate("/fe/lol-uikit/../../x.ogg"));
        Assert.Null(ClientSoundCatalog.Locate("/fe/lol-uikit"));
    }

    [Fact]
    public void Catalog_keys_are_the_ones_the_phone_knows()
    {
        // web/src/contracts.ts ClientSoundKey / ClientMusicKey.
        string[] sounds =
        [
            "matchFound", "readyCountdown", "readyPortal", "readyPortalAram", "readyAccept", "readyDecline", "readyAcceptedLoop",
            "readySuccess", "readyFailed", "queueStart", "queueCancel", "memberJoined", "memberLeft", "inviteSent", "leaderGranted",
            "inviteReceived", "chatMessage", "friendRequest", "banPhase", "pickPhase", "yourTurn", "yourBan", "timerTick",
            "timerTickSmall", "hover", "lockIn", "ban", "allyBan", "serialAllyBan", "serialEnemyBan", "allyPick", "enemyPick",
            "allyFirstPick", "enemyFirstPick", "pickSniped", "swapRequest", "positionSwapRequest", "swapAccepted", "swapDeclined",
            "benchUpdate", "reroll", "gameStart", "click",
        ];
        string[] music =
        [
            "musicHome", "ambienceLobby", "ambienceLobbyAram", "musicQueue", "musicQueueAram", "musicBlindPick", "musicAram",
            "musicAramBan", "musicDraftIntent", "musicDraftIntentToBan", "musicDraftBan", "musicDraftBanLayer1", "musicDraftBanLayer2",
            "musicDraftBanToPick", "musicDraftPick", "musicDraftPickLayer1", "musicDraftPickLayer2", "musicDraftPickLayer3",
            "musicDraftPickLayer4", "musicDraftFinal",
        ];
        Assert.Equal(sounds.Order(), ClientSoundCatalog.Sounds.Keys.Order());
        Assert.Equal(music.Order(), ClientSoundCatalog.Music.Keys.Order());
        Assert.All(ClientSoundCatalog.All.SelectMany(e => e.Value),
            url => Assert.True(ClientSoundCatalog.Locate(url) is not null || ClientSoundCatalog.IsGameDataAsset(url), url));
    }

    [Fact]
    public void Countdown_ticks_come_from_the_client_not_a_synth()
    {
        Assert.Equal(["/fe/lol-champ-select/sounds/sfx-cs-timer-tick.ogg"], ClientSoundCatalog.Sounds["timerTick"]);
        Assert.Equal(["/fe/lol-champ-select/sounds/sfx-cs-timer-tick-small.ogg"], ClientSoundCatalog.Sounds["timerTickSmall"]);
        Assert.Empty(ClientSoundCatalog.Sounds["gameStart"]); // the only cue the client has no sound for
    }

    [Theory]
    [InlineData("/lol-game-data/assets/content/src/LeagueClient/GameModeAssets/Shared/sound/x.ogg", true)]
    [InlineData("/lol-game-data/assets/../lol-chat/v1/me.ogg", false)]
    [InlineData("/lol-game-data/assets/v1/champions.json", false)]
    [InlineData("/lol-chat/v1/x.ogg", false)]
    [InlineData("/lol-game-data/assets/", false)]
    public void Only_game_data_audio_paths_are_fetched_from_the_client(string url, bool expected) =>
        Assert.Equal(expected, ClientSoundCatalog.IsGameDataAsset(url));

    private const string QueueMusic = "/lol-game-data/assets/content/src/LeagueClient/GameModeAssets/Classic_SRU/sound/music-inqueue-loop-summonersrift.ogg";

    [Fact]
    public async Task Map_music_is_fetched_from_the_running_client_once_and_cached()
    {
        var fetched = new List<string>();
        var running = true;
        Task<byte[]?> Fetch(string path, CancellationToken ct)
        {
            fetched.Add(path);
            return Task.FromResult(running && path == QueueMusic ? FakeWad.Ogg : null);
        }

        var library = new ClientSoundLibrary(Install, Cache, _time, gameData: Fetch);
        var manifest = await library.GetManifestAsync(CancellationToken.None);
        Assert.Equal("/client-sounds/musicQueue.ogg", manifest.Music["musicQueue"]);
        Assert.DoesNotContain("musicQueue", manifest.Sounds.Keys);
        Assert.Equal(FakeWad.Ogg, await File.ReadAllBytesAsync((await library.GetFileAsync("musicQueue.ogg", CancellationToken.None))!));
        Assert.Contains(QueueMusic, fetched);

        // Other game-data files were missing (client "not running" for them): retried a minute later, the cached one isn't.
        fetched.Clear();
        running = false;
        _time.Advance(ClientSoundLibrary.RetryInterval);
        manifest = await library.GetManifestAsync(CancellationToken.None);
        Assert.DoesNotContain(QueueMusic, fetched);
        Assert.NotEmpty(fetched);
        Assert.Equal("/client-sounds/musicQueue.ogg", manifest.Music["musicQueue"]);
    }

    [Fact]
    public async Task Game_data_answers_that_are_not_audio_are_ignored()
    {
        var library = new ClientSoundLibrary(Install, Cache, _time,
            gameData: (_, _) => Task.FromResult<byte[]?>("""{"errorCode":"RPC_ERROR"}"""u8.ToArray()));
        var manifest = await library.GetManifestAsync(CancellationToken.None);
        Assert.Empty(manifest.Music);
        Assert.Empty(manifest.Sounds);
    }

    [Fact]
    public async Task A_failing_client_fetch_falls_back_to_the_cache()
    {
        var library = new ClientSoundLibrary(Install, Cache, _time, gameData: (p, _) => Task.FromResult<byte[]?>(p == QueueMusic ? FakeWad.Ogg : null));
        await library.GetManifestAsync(CancellationToken.None);

        var broken = new ClientSoundLibrary(Install, Cache, _time,
            gameData: (_, _) => throw new HttpRequestException("connection refused"));
        Assert.Equal("/client-sounds/musicQueue.ogg", (await broken.GetManifestAsync(CancellationToken.None)).Music["musicQueue"]);
    }

    [Fact]
    public async Task Music_keys_with_digits_are_served_from_the_plugin_wad()
    {
        FakeWad.Write(Path.Combine(Install, "Plugins", "rcp-fe-lol-champ-select", "assets.wad"),
            ("plugins/rcp-fe-lol-champ-select/global/default/sounds/music-cs-draft-pick-intensity-layer-01.ogg", FakeWad.Ogg, false));
        var library = new ClientSoundLibrary(Install, Cache, _time);

        Assert.Equal("/client-sounds/musicDraftPickLayer1.ogg", (await library.GetManifestAsync(CancellationToken.None)).Music["musicDraftPickLayer1"]);
        Assert.NotNull(await library.GetFileAsync("musicDraftPickLayer1.ogg", CancellationToken.None));
        Assert.Null(await library.GetFileAsync("1musicDraftPickLayer1.ogg", CancellationToken.None));
    }

    [Fact]
    public async Task Lcu_client_fetches_only_game_data_audio()
    {
        var handler = new FakeLcu();
        handler.Routes[QueueMusic] = "OggS fake";
        using var http = handler.CreateClient();
        var lcu = new Lcu.LcuClient(http);

        Assert.Equal("OggS fake"u8.ToArray(), await lcu.GetGameDataAudioAsync(QueueMusic, CancellationToken.None));
        Assert.Null(await lcu.GetGameDataAudioAsync(QueueMusic.Replace("summonersrift", "nowhere"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => lcu.GetGameDataAudioAsync("/lol-chat/v1/me", CancellationToken.None));
        Assert.Equal([$"GET {QueueMusic}", $"GET {QueueMusic.Replace("summonersrift", "nowhere")}"], handler.Requests);
    }

    [Fact]
    public void Wad_reader_finds_raw_and_gzip_entries_by_path_hash()
    {
        var wad = Path.Combine(_dir, "a.wad");
        FakeWad.Write(wad, ("Some/Path.OGG", FakeWad.Ogg, false), ("other.ogg", [1, 2, 3], true));

        Assert.Equal(FakeWad.Ogg, WadArchive.TryRead(wad, "some/path.ogg"));
        Assert.Equal([1, 2, 3], WadArchive.TryRead(wad, "other.ogg"));
        Assert.Null(WadArchive.TryRead(wad, "missing.ogg"));

        File.WriteAllBytes(wad, new byte[400]);
        Assert.Throws<InvalidDataException>(() => WadArchive.TryRead(wad, "x"));
    }

    [Fact]
    public async Task Extracts_lazily_into_the_cache_and_keeps_serving_it_without_the_client()
    {
        var library = new ClientSoundLibrary(Install, Cache, _time);
        var empty = await library.GetManifestAsync(CancellationToken.None);
        Assert.Empty(empty.Sounds);
        Assert.Empty(empty.Music);
        Assert.False(Directory.Exists(Cache));

        InstallChampSelectWad(FakeWad.Ogg);
        library = new ClientSoundLibrary(Install, Cache, _time);
        var manifest = (await library.GetManifestAsync(CancellationToken.None)).Sounds;
        Assert.Equal("/client-sounds/yourTurn.ogg", manifest["yourTurn"]);
        Assert.Equal("/client-sounds/lockIn.ogg", manifest["lockIn"]);
        Assert.Equal(2, manifest.Count);
        Assert.Equal(FakeWad.Ogg, await File.ReadAllBytesAsync((await library.GetFileAsync("lockIn.ogg", CancellationToken.None))!));

        Assert.Null(await library.GetFileAsync("matchFound.ogg", CancellationToken.None));
        Assert.Null(await library.GetFileAsync("..\\index.json", CancellationToken.None));
        Assert.Null(await library.GetFileAsync("lockIn.mp3", CancellationToken.None));

        // League uninstalled / moved: the cache still serves.
        Directory.Delete(Install, recursive: true);
        var offline = new ClientSoundLibrary(Install, Cache, _time);
        Assert.Equal(manifest, (await offline.GetManifestAsync(CancellationToken.None)).Sounds);
    }

    [Fact]
    public async Task A_client_patch_is_picked_up_on_the_next_refresh()
    {
        InstallChampSelectWad(FakeWad.Ogg);
        var library = new ClientSoundLibrary(Install, Cache, _time);
        await library.GetManifestAsync(CancellationToken.None);

        byte[] patched = [.. "OggS"u8, 9, 9, 9, 9, 9];
        InstallChampSelectWad(patched);
        File.SetLastWriteTimeUtc(Path.Combine(Install, "Plugins", "rcp-fe-lol-champ-select", "assets.wad"), DateTime.UtcNow.AddMinutes(1));
        _time.Advance(ClientSoundLibrary.RefreshInterval);

        var path = await library.GetFileAsync("yourTurn.ogg", CancellationToken.None);
        Assert.Equal(patched, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task Phone_server_serves_the_manifest_and_the_files()
    {
        InstallChampSelectWad(FakeWad.Ogg);
        var server = await PhoneServerHost.StartAsync(Path.Combine(_dir, "server"), new ClientSoundLibrary(Install, Cache, _time));
        await using (server)
        {
            using var client = new HttpClient { BaseAddress = server.Http }; // loopback: plain HTTP passes the LAN middleware

            using var manifest = await client.GetAsync("/client-sounds/manifest.json");
            Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
            Assert.Equal("application/json", manifest.Content.Headers.ContentType?.MediaType);
            using var json = JsonDocument.Parse(await manifest.Content.ReadAsStringAsync());
            Assert.Equal("/client-sounds/yourTurn.ogg", json.RootElement.GetProperty("sounds").GetProperty("yourTurn").GetString());
            Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("music").ValueKind);

            using var sound = await client.GetAsync("/client-sounds/yourTurn.ogg");
            Assert.Equal(HttpStatusCode.OK, sound.StatusCode);
            Assert.Equal("audio/ogg", sound.Content.Headers.ContentType?.MediaType);
            Assert.Contains("max-age", sound.Headers.CacheControl?.ToString());
            Assert.Equal(FakeWad.Ogg, await sound.Content.ReadAsByteArrayAsync());

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/client-sounds/matchFound.ogg")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/client-sounds/nope.ogg")).StatusCode);
        }
    }

    [Fact]
    public async Task Phone_server_without_sounds_returns_an_empty_manifest()
    {
        await using var server = await PhoneServerHost.StartAsync(Path.Combine(_dir, "server"));
        using var client = new HttpClient { BaseAddress = server.Http };
        Assert.Equal("""{"sounds":{},"music":{}}""", await client.GetStringAsync("/client-sounds/manifest.json"));
    }

    /// <summary>
    /// Re-checks <see cref="ClientSoundCatalog"/> against a real League installation after a client patch:
    /// <c>set LEAGUEREMOTE_REAL_INSTALL=C:\Riot Games\League of Legends</c> and run this test. Read-only on the install; does
    /// nothing when the variable is not set.
    /// </summary>
    [Fact]
    public async Task Catalog_matches_a_real_installation_when_one_is_configured()
    {
        if (Environment.GetEnvironmentVariable("LEAGUEREMOTE_REAL_INSTALL") is not { Length: > 0 } install)
            return;

        // Plugin-WAD files only: game-data music needs the running client (GetGameDataAudioAsync).
        var manifest = await new ClientSoundLibrary(install, Cache, _time).GetManifestAsync(CancellationToken.None);
        var expected = ClientSoundCatalog.All.Where(s => s.Value.Any(u => ClientSoundCatalog.Locate(u) is not null)).Select(s => s.Key).Order();
        Assert.Equal(expected, manifest.Sounds.Keys.Concat(manifest.Music.Keys).Order());
        foreach (var file in Directory.GetFiles(Cache, "*.ogg"))
            Assert.Equal("OggS"u8.ToArray(), (await File.ReadAllBytesAsync(file)).Take(4));
    }
}
