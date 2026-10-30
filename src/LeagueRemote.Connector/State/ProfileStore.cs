using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Latest <see cref="ProfileState"/> for phones (published through a <see cref="SliceStore{T}"/>). Also the cache
/// of full games the phone opened (<see cref="ProfileState.Details"/>): a finished game never changes, so each is read from
/// the LCU once, kept while it is in <see cref="ProfileState.Matches"/>, at most <see cref="ProfileState.MaxDetails"/>, the
/// most recently requested first. And the cache of other players' profiles (<see cref="ProfileState.Players"/>, with their
/// own opened games), kept for <see cref="ProfileState.PlayerLifetime"/>, and which of them were opened from a game or a
/// Riot ID search (<see cref="KeepsAccess"/>): those aren't on any League list to re-check, so they stay viewable for
/// <see cref="ProfileState.PlayerLifetime"/> from that grant (a reread doesn't extend it) while they are cached.
/// </summary>
internal sealed class ProfileStore(TimeProvider? time = null)
{
    /// <summary>Guards the caches; the slice is only written under it, so publishes keep the caches' order.</summary>
    private readonly Lock _lock = new();
    private readonly SliceStore<ProfileState> _slice = new(
        ProfileState.Empty(),
        profile => profile with { UpdatedAt = default },
        (profile, now) => profile with { UpdatedAt = now },
        time ?? TimeProvider.System);
    private List<MatchDetails> _details = [];
    private readonly List<PlayerProfile> _players = [];
    /// <summary>Puuid → when a game or a search last gave access (see <see cref="KeepsAccess"/>).</summary>
    private readonly Dictionary<string, DateTimeOffset> _access = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _readAt;

    /// <summary>Raised on the writer's thread when a new version is available: handlers only signal.</summary>
    public event Action? Changed
    {
        add => _slice.Changed += value;
        remove => _slice.Changed -= value;
    }

    public (ProfileState Profile, long Version) Snapshot() => _slice.Snapshot();

    private ProfileState Current => _slice.Snapshot().Value;

    /// <summary>When the LCU was last read in full (<see cref="Set"/> with a time); null after a disconnect.</summary>
    public DateTimeOffset? ReadAt
    {
        get
        {
            lock (_lock)
                return _readAt;
        }
    }

    /// <summary>
    /// Stores a fresh read (its <see cref="ProfileState.Details"/> and <see cref="ProfileState.Players"/> are ignored: the
    /// cache's details of games still in <see cref="ProfileState.Matches"/> are kept, the others dropped; other players'
    /// profiles stay until <see cref="ProfileState.PlayerLifetime"/> after they were read). <paramref name="readAt"/> is when
    /// the LCU was read (null for <see cref="ProfileState.Empty"/> on disconnect, so the next refresh reads at once; that also
    /// drops every player). Returns whether a new version was published (false when nothing but timestamps changed).
    /// </summary>
    public bool Set(ProfileState profile, DateTimeOffset? readAt)
    {
        lock (_lock)
        {
            _readAt = readAt;
            var inMatches = profile.Matches.Select(m => m.GameId).ToHashSet();
            _details = [.. _details.Where(d => inMatches.Contains(d.GameId))];
            if (readAt is { } now)
                _players.RemoveAll(p => now - p.LoadedAt >= ProfileState.PlayerLifetime);
            else
                _players.Clear();
            PruneAccessLocked();
            return PublishLocked(profile);
        }
    }

    /// <summary>
    /// Moves an already loaded game to the front of <see cref="ProfileState.Details"/>. False when it isn't cached (the
    /// caller reads it from the LCU and calls <see cref="AddDetails"/>).
    /// </summary>
    public bool ShowDetails(long gameId)
    {
        lock (_lock)
        {
            var index = _details.FindIndex(d => d.GameId == gameId);
            if (index > 0)
            {
                var details = _details[index];
                _details.RemoveAt(index);
                _details.Insert(0, details);
                PublishLocked(Current);
            }

            return index >= 0;
        }
    }

    /// <summary>
    /// Adds a game read from the LCU at the front, dropping the one requested longest ago beyond
    /// <see cref="ProfileState.MaxDetails"/>. False (nothing stored) when the game is no longer in the matches.
    /// </summary>
    public bool AddDetails(MatchDetails details)
    {
        lock (_lock)
        {
            var current = Current;
            if (!current.Matches.Any(m => m.GameId == details.GameId))
                return false;
            _details.RemoveAll(d => d.GameId == details.GameId);
            _details.Insert(0, details);
            if (_details.Count > ProfileState.MaxDetails)
                _details.RemoveRange(ProfileState.MaxDetails, _details.Count - ProfileState.MaxDetails);
            PublishLocked(current);
            return true;
        }
    }

    // --- Other players ------------------------------------------------------------------------

    /// <summary>The cached profile of <paramref name="puuid"/> (as <see cref="ProfileState.Players"/> has it), or null.</summary>
    public PlayerProfile? Player(string puuid)
    {
        lock (_lock)
            return _players.FirstOrDefault(p => SamePuuid(p.Puuid, puuid));
    }

    /// <summary>
    /// Whether <paramref name="puuid"/> is cached and was opened from a game the connector named them in or by a Riot ID
    /// search less than <see cref="ProfileState.PlayerLifetime"/> before <paramref name="now"/> (<see cref="AddPlayer"/> /
    /// <see cref="ShowPlayer"/> with <c>grantedAt</c>): their profile and games stay viewable without a League list naming
    /// them. Rereading the profile doesn't extend it; only a new grant (named in a game again, searched again) does. Expired
    /// grants count as none even before a profile read prunes them.
    /// </summary>
    public bool KeepsAccess(string puuid, DateTimeOffset now)
    {
        lock (_lock)
            return _access.TryGetValue(puuid, out var grantedAt) && now - grantedAt < ProfileState.PlayerLifetime
                   && _players.Any(p => SamePuuid(p.Puuid, puuid));
    }

    /// <summary>
    /// Whether a game the connector published names <paramref name="puuid"/> as a teammate (<see cref="MatchParticipant.Puuid"/>):
    /// one of the local player's opened games, or an opened game of a player read less than
    /// <see cref="ProfileState.PlayerLifetime"/> before <paramref name="now"/> (an expired player's games name nobody).
    /// </summary>
    public bool NamesTeammate(string puuid, DateTimeOffset now)
    {
        lock (_lock)
        {
            var lines = _details
                .Concat(_players.Where(p => now - p.LoadedAt < ProfileState.PlayerLifetime).SelectMany(p => p.Details))
                .SelectMany(d => d.Teams)
                .SelectMany(t => t.Participants);
            return ProfileMapper.Names(lines, puuid);
        }
    }

    /// <summary>
    /// Stores a player's profile read from the LCU at the front of <see cref="ProfileState.Players"/>, replacing an older read
    /// (its loaded games still in the new match list are kept) and dropping the one opened longest ago beyond
    /// <see cref="ProfileState.MaxPlayers"/>. <paramref name="grantedAt"/>: opened from a game or a search just now, see
    /// <see cref="KeepsAccess"/> (null: no new grant; an earlier one keeps its own time).
    /// </summary>
    public void AddPlayer(PlayerProfile player, DateTimeOffset? grantedAt = null)
    {
        lock (_lock)
        {
            var previous = _players.FirstOrDefault(p => SamePuuid(p.Puuid, player.Puuid));
            var inMatches = player.Matches.Select(m => m.GameId).ToHashSet();
            var details = previous?.Details.Where(d => inMatches.Contains(d.GameId)).ToList() ?? [];
            _players.RemoveAll(p => SamePuuid(p.Puuid, player.Puuid));
            _players.Insert(0, player with { Details = details });
            if (_players.Count > ProfileState.MaxPlayers)
                _players.RemoveRange(ProfileState.MaxPlayers, _players.Count - ProfileState.MaxPlayers);
            if (grantedAt is { } at)
                _access[player.Puuid] = at;
            PruneAccessLocked();
            PublishLocked(Current);
        }
    }

    /// <summary>
    /// Moves a cached player to the front of <see cref="ProfileState.Players"/>; false when not cached.
    /// <paramref name="grantedAt"/> as in <see cref="AddPlayer"/>.
    /// </summary>
    public bool ShowPlayer(string puuid, DateTimeOffset? grantedAt = null)
    {
        lock (_lock)
        {
            var index = _players.FindIndex(p => SamePuuid(p.Puuid, puuid));
            if (index >= 0 && grantedAt is { } at)
                _access[_players[index].Puuid] = at;
            if (index > 0)
            {
                var player = _players[index];
                _players.RemoveAt(index);
                _players.Insert(0, player);
                PublishLocked(Current);
            }

            return index >= 0;
        }
    }

    /// <summary>
    /// Moves an already loaded game of a cached player to the front of their <see cref="PlayerProfile.Details"/>. False when
    /// it isn't loaded (the caller reads it and calls <see cref="AddPlayerDetails"/>).
    /// </summary>
    public bool ShowPlayerDetails(string puuid, long gameId)
    {
        lock (_lock)
        {
            var index = _players.FindIndex(p => SamePuuid(p.Puuid, puuid));
            var player = index >= 0 ? _players[index] : null;
            var at = player?.Details.ToList().FindIndex(d => d.GameId == gameId) ?? -1;
            if (at > 0)
            {
                var details = player!.Details.ToList();
                var game = details[at];
                details.RemoveAt(at);
                details.Insert(0, game);
                _players[index] = player with { Details = details };
                PublishLocked(Current);
            }

            return at >= 0;
        }
    }

    /// <summary>
    /// Adds a game of a cached player at the front of their details (at most <see cref="ProfileState.MaxPlayerDetails"/>).
    /// False (nothing stored) when the player is no longer cached or the game isn't in their matches.
    /// </summary>
    public bool AddPlayerDetails(string puuid, MatchDetails game)
    {
        lock (_lock)
        {
            var index = _players.FindIndex(p => SamePuuid(p.Puuid, puuid));
            if (index < 0 || !ProfileMapper.HasMatch(_players[index], game.GameId))
                return false;
            var player = _players[index];
            List<MatchDetails> details = [game, .. player.Details.Where(d => d.GameId != game.GameId)];
            _players[index] = player with { Details = [.. details.Take(ProfileState.MaxPlayerDetails)] };
            PublishLocked(Current);
            return true;
        }
    }

    /// <summary>
    /// Forces a republish of the current profile; with <paramref name="now"/> it first drops players read more than
    /// <see cref="ProfileState.PlayerLifetime"/> ago (a Profile tab opened within the refresh interval publishes without a read).
    /// </summary>
    public void Touch(DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            if (now is { } at && _players.RemoveAll(p => at - p.LoadedAt >= ProfileState.PlayerLifetime) > 0)
            {
                PruneAccessLocked();
                PublishLocked(Current);
            }

            _slice.Touch();
        }
    }

    private static bool SamePuuid(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>A player who left the cache (cap, lifetime, disconnect) loses the access a game or a search gave them.</summary>
    private void PruneAccessLocked()
    {
        foreach (var puuid in _access.Keys.Where(puuid => !_players.Any(p => SamePuuid(p.Puuid, puuid))).ToList())
            _access.Remove(puuid);
    }

    /// <summary>
    /// Composes <paramref name="profile"/> with the cached details and players; bumps the version when that changed anything.
    /// </summary>
    private bool PublishLocked(ProfileState profile) =>
        _slice.Set(profile with { Details = [.. _details], Players = [.. _players] });
}
