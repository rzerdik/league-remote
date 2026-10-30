using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// The local player's profile and match details, and other players' profiles (friends, the own party and team, a Riot ID
/// search). They publish <see cref="ProfileStore"/> themselves; the game state isn't rebuilt for them.
/// </summary>
internal sealed class ProfileCommands(ILcuSession session, StateStore stateStore, ProfileStore profileStore, TimeProvider time)
    : CommandHandler
{
    private const string CannotViewPlayer = "You can only view friends, your party and your team.";

    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.RefreshProfile] = RefreshSlice.None,
        [CommandType.LoadPlayerProfile] = RefreshSlice.None,
        [CommandType.SearchPlayer] = RefreshSlice.None,
        [CommandType.LoadMatchDetails] = RefreshSlice.None,
    };

    /// <summary>The phone sends RefreshProfile whenever the Profile tab opens: without a client it gets the (empty) profile again.</summary>
    public override bool TryRunWithoutClient(RemoteCommand command, out string? error)
    {
        error = null;
        if (command.Type != CommandType.RefreshProfile || session.Client is not null)
            return false;
        profileStore.Touch();
        return true;
    }

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        switch (command.Type)
        {
            case CommandType.RefreshProfile:
            {
                // Throttled: the phone asks on every tab open, a full read is five requests.
                var fresh = profileStore.ReadAt is { } readAt && time.GetUtcNow() - readAt < ProfileState.RefreshInterval;
                if (fresh || !await ProfileReader.ReadAsync(client, profileStore, time, ct))
                    profileStore.Touch(time.GetUtcNow()); // nothing new: republish so the phone still hears back
                return null;
            }

            case CommandType.LoadPlayerProfile:
            {
                var puuid = command.Puuid!;
                // A teammate this connector names in a game it published (a new grant), a player a game or a search opened
                // less than PlayerLifetime ago (that grant, not renewed), else friends, the own party and the own team as
                // League lists them right now; never an enemy or a stranger.
                var now = time.GetUtcNow();
                var named = IsNamedTeammate(puuid, now);
                if (!named && !profileStore.KeepsAccess(puuid, now) && !await CanViewPlayerAsync(client, puuid, ct))
                    return CannotViewPlayer;
                return await OpenPlayerAsync(client, puuid, named ? now : null, ct);
            }

            case CommandType.SearchPlayer:
            {
                // The user typed this Riot ID: League finds the player, so showing them is what was asked for.
                var found = ProfileMapper.LookedUpPuuid(await client.LookupRiotIdAsync(command.GameName!, command.TagLine!, ct));
                if (found is null)
                    return "No player with that Riot ID.";
                if (ProfileMapper.MyPuuid(await client.GetCurrentSummonerAsync(ct)) is { } mine
                    && string.Equals(mine, found, StringComparison.OrdinalIgnoreCase))
                    return "That's your own Riot ID.";
                return await OpenPlayerAsync(client, found, time.GetUtcNow(), ct);
            }

            case CommandType.LoadMatchDetails when !string.IsNullOrEmpty(command.Puuid):
                return await LoadPlayerMatchAsync(client, command.Puuid, command.GameId!.Value, ct);

            case CommandType.LoadMatchDetails:
                return await LoadOwnMatchAsync(client, command.GameId!.Value, ct);

            default:
                return await base.ExecuteAsync(client, command, ct);
        }
    }

    /// <summary>Only a game the profile lists (the local player's own history), never an arbitrary id.</summary>
    private async Task<string?> LoadOwnMatchAsync(LcuClient client, long gameId, CancellationToken ct)
    {
        if (!ProfileMapper.HasMatch(profileStore.Snapshot().Profile, gameId))
            return "That game isn't in your match history.";
        // A finished game never changes: one read per game.
        if (profileStore.ShowDetails(gameId))
            return null;

        var myPuuid = ProfileMapper.MyPuuid(await client.GetCurrentSummonerAsync(ct));
        if (ProfileMapper.Details(await client.GetMatchAsync(gameId, ct), myPuuid) is not { } details || details.GameId != gameId)
            return "League couldn't load that game.";
        return profileStore.AddDetails(details) ? null : "That game isn't in your match history.";
    }

    /// <summary>A game of a player the phone opened, re-checked like LoadPlayerProfile (they may have left the party).</summary>
    private async Task<string?> LoadPlayerMatchAsync(LcuClient client, string puuid, long gameId, CancellationToken ct)
    {
        if (profileStore.Player(puuid) is not { } player || !ProfileMapper.HasMatch(player, gameId))
            return "That game isn't in their match history.";
        if (!profileStore.KeepsAccess(puuid, time.GetUtcNow()) && !await CanViewPlayerAsync(client, puuid, ct))
            return CannotViewPlayer;
        if (profileStore.ShowPlayerDetails(puuid, gameId))
            return null;

        var localPuuid = ProfileMapper.MyPuuid(await client.GetCurrentSummonerAsync(ct));
        if (ProfileMapper.PlayerDetails(await client.GetMatchAsync(gameId, ct), puuid, localPuuid) is not { } details
            || details.GameId != gameId)
            return "League couldn't load that game.";
        return profileStore.AddPlayerDetails(puuid, details) ? null : "That game isn't in their match history.";
    }

    /// <summary>
    /// Reads <paramref name="puuid"/>'s profile into <see cref="ProfileState.Players"/> (first), or moves a profile read less
    /// than <see cref="ProfileState.RefreshInterval"/> ago to the front. The caller checked who it is; <paramref name="grantedAt"/>
    /// when a game or a search gave access just now (<see cref="ProfileStore.KeepsAccess"/>).
    /// </summary>
    private async Task<string?> OpenPlayerAsync(LcuClient client, string puuid, DateTimeOffset? grantedAt, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (profileStore.Player(puuid) is { } cached && now - cached.LoadedAt < ProfileState.RefreshInterval)
        {
            if (!profileStore.ShowPlayer(puuid, grantedAt))
                return "League couldn't load that profile.";
            profileStore.Touch(now); // already first: republish so the phone still hears back
            return null;
        }

        if (await ProfileReader.BuildPlayerAsync(client, puuid, now, ct) is not { } player)
            return "League couldn't load that profile.";
        profileStore.AddPlayer(player, grantedAt);
        return null;
    }

    /// <summary>
    /// Whether this connector itself shows <paramref name="puuid"/> as a named teammate right now: in a game it published (the
    /// local player's or a still-fresh opened player's game details, the post-game screen — a puuid only ever goes out
    /// beside a teammate's name, see <see cref="MatchParticipant.Puuid"/>). Checked against the connector's own stores, never
    /// against anything the phone sent.
    /// </summary>
    private bool IsNamedTeammate(string puuid, DateTimeOffset now) =>
        profileStore.NamesTeammate(puuid, now)
        || (stateStore.Snapshot().State.PostGame is { } postGame && ProfileMapper.Names(postGame.Teams.SelectMany(t => t.Participants), puuid));

    /// <summary>
    /// Whether the local player may open <paramref name="puuid"/>'s profile: a friend, a member of their lobby, or a teammate
    /// in champ select whose name League shows (not an anonymous draft). Read fresh from the LCU, cheapest first; enemies
    /// are never on any of these lists.
    /// </summary>
    private static async Task<bool> CanViewPlayerAsync(LcuClient client, string puuid, CancellationToken ct) =>
        FriendsMapper.FindByPuuid(await client.GetFriendsAsync(ct), puuid) is not null
        || (await client.GetLobbyAsync(ct) is { } lobby && LobbyMapper.IsMember(lobby, puuid))
        || ChampSelectSession.Parse(await client.GetChampSelectSessionAsync(ct))?.IsVisibleTeammate(puuid) == true;
}
