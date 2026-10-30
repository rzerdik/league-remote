using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// A command executor on <see cref="ProfileSamples.Player.Handler"/> (the local player's profile, the friends list and a
/// friend's profile) with a <see cref="FakeTimeProvider"/>, plus what the player-profile tests share (<c>using static</c>).
/// </summary>
internal sealed class PlayerProfileHarness : IDisposable
{
    public const string FriendPuuid = ProfileSamples.Player.FriendPuuid;
    public const string FriendsRead = "GET /lol-chat/v1/friends";
    public const string LobbyRead = "GET /lol-lobby/v2/lobby";
    public const string SessionRead = "GET /lol-champ-select/v1/session";
    public const string MeRead = $"GET {ProfileSamples.Own.SummonerPath}";

    /// <summary>The friend's newest game (<see cref="ProfileSamples.Player.GameId"/>) in full.</summary>
    public const string FriendGamePath = "/lol-match-history/v1/games/4100000003";

    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-23T18:00:00Z");

    private readonly HttpClient _http;

    public PlayerProfileHarness()
    {
        _http = Lcu.CreateClient();
        Session = new FakeLcuSession(new LcuClient(_http));
    }

    public FakeTimeProvider Time { get; } = new(Now);

    public FakeLcu Lcu { get; } = ProfileSamples.Player.Handler();

    public ProfileStore Store { get; } = new();

    public StateStore State { get; } = new();

    public FakeLcuSession Session { get; }

    public IReadOnlyList<PlayerProfile> Players => Store.Snapshot().Profile.Players ?? [];

    public Task<CommandResult> Run(CommandType type, string? puuid, long? gameId = null) =>
        Run(new RemoteCommand("r1", type, Puuid: puuid, GameId: gameId));

    public Task<CommandResult> Run(RemoteCommand command) =>
        Session.Executor(Store, Time, state: State).ExecuteAsync(command, CancellationToken.None);

    public Task<CommandResult> Load(string puuid = FriendPuuid) => Run(CommandType.LoadPlayerProfile, puuid);

    public Task<CommandResult> Search(string gameName, string tagLine) =>
        Run(new RemoteCommand("r1", CommandType.SearchPlayer, GameName: gameName, TagLine: tagLine));

    /// <summary>The local player's profile read and their recorded ranked loss opened, as the Profile tab does.</summary>
    public async Task OpenOwnGameAsync()
    {
        await ProfileReader.ReadAsync(Session.Client!, Store, Time, CancellationToken.None);
        Assert.True((await Run(CommandType.LoadMatchDetails, null, ProfileSamples.Own.RankedLossGameId)).Success);
        Lcu.Requests.Clear();
    }

    /// <summary>Serves <paramref name="puuid"/>'s profile with the friend sample's data.</summary>
    public void Serve(string puuid)
    {
        foreach (var (path, json) in ProfileSamples.Player.Handler(puuid).Routes)
            Lcu.Routes[path] = json;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>The recorded full game as the friend's newest game, with the friend in <paramref name="seat"/>'s place.</summary>
    public static string FriendGame(string seat) =>
        ProfileSamples.Own.Game.Replace("4000000004", "4100000003").Replace(seat, FriendPuuid);

    /// <summary>The game names of everyone named in <paramref name="details"/>.</summary>
    public static List<string> Named(MatchDetails details) =>
        [.. details.Teams.SelectMany(t => t.Participants).Where(p => p.GameName != "").Select(p => p.GameName)];

    /// <summary>The puuids the lines carry (empty ones left out).</summary>
    public static List<string> Puuids(IEnumerable<MatchParticipant> lines) => [.. lines.Where(p => p.Puuid != "").Select(p => p.Puuid)];
}
