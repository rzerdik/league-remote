using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>The LCU reads of the profile slice: the local player's profile, another player's, and the post-game watch.</summary>
internal static class ProfileReader
{
    /// <summary>
    /// Gaps between the post-game rereads (at 5 s, 20 s, 65 s and 185 s after the game ended), until the new game is the
    /// newest in the match history. How long the client takes to list a finished game is not measured yet.
    /// </summary>
    internal static readonly IReadOnlyList<TimeSpan> PostGameBackoff =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(120)];

    /// <summary>Reads the whole profile and stores it, stamped as a fresh read; true when that changed what phones see.</summary>
    public static async Task<bool> ReadAsync(LcuClient client, ProfileStore store, TimeProvider time, CancellationToken ct)
    {
        var profile = await BuildAsync(client, time.GetUtcNow(), ct, store.Snapshot().Profile);
        return store.Set(profile, time.GetUtcNow());
    }

    /// <summary>
    /// Summoner, ranks, mastery and match history. A part the client answers with an error (one of its services restarting)
    /// keeps its value from <paramref name="previous"/>, so one failing service doesn't blank the others.
    /// <see cref="ProfileState.Details"/> and <see cref="ProfileState.Players"/> are left to the <see cref="ProfileStore"/>.
    /// </summary>
    public static async Task<ProfileState> BuildAsync(LcuClient c, DateTimeOffset now, CancellationToken ct, ProfileState? previous = null)
    {
        var summoner = await OptionalRead.TryAsync(c.GetCurrentSummonerAsync, ct);
        var ranked = await OptionalRead.TryAsync(c.GetCurrentRankedStatsAsync, ct);
        var mastery = await OptionalRead.TryAsync(c.GetChampionMasteryAsync, ct);
        var masteryScore = await OptionalRead.TryAsync(c.GetChampionMasteryScoreAsync, ct);
        var matches = await OptionalRead.TryAsync(c.GetMatchHistoryAsync, ct);

        return new ProfileState(
            summoner.Ok ? ProfileMapper.Summoner(summoner.Json) : previous?.Summoner,
            ranked.Ok ? ProfileMapper.Ranks(ranked.Json) : previous?.Ranks ?? [],
            mastery.Ok && masteryScore.Ok ? ProfileMapper.Mastery(mastery.Json, masteryScore.Json) : previous?.Mastery ?? ProfileMastery.Empty,
            matches.Ok ? ProfileMapper.Matches(matches.Json, ProfileMapper.MyPuuid(summoner.Json)) : previous?.Matches ?? [],
            [],
            now,
            []);
    }

    /// <summary>
    /// Another player's profile by puuid (the caller checked they are a friend, in the party or on the own team). Null when
    /// the client has no summoner for that puuid; a part the client refuses (ranked, mastery, history) comes back empty.
    /// </summary>
    public static async Task<PlayerProfile?> BuildPlayerAsync(LcuClient c, string puuid, DateTimeOffset now, CancellationToken ct)
    {
        var summoner = await OptionalRead.TryAsync(t => c.GetSummonerByPuuidAsync(puuid, t), ct);
        if (ProfileMapper.Summoner(summoner.Json) is not { } player)
            return null;
        var ranked = await OptionalRead.TryAsync(t => c.GetRankedStatsAsync(puuid, t), ct);
        var mastery = await OptionalRead.TryAsync(t => c.GetChampionMasteryAsync(puuid, t), ct);
        var matches = await OptionalRead.TryAsync(t => c.GetMatchHistoryAsync(puuid, t), ct);
        return new PlayerProfile(
            puuid,
            player,
            ProfileMapper.PlayerRanks(ranked.Json),
            ProfileMapper.PlayerMastery(mastery.Json),
            ProfileMapper.Matches(matches.Json, puuid),
            [],
            now);
    }

    /// <summary>
    /// After a game, match history has no event: reread the profile at <see cref="PostGameBackoff"/> (measured from the
    /// start) until its newest game differs from the baseline, the newest game when the watch started or, when the profile
    /// wasn't read yet, of the first read. A regular refresh finding the game first ends the watch too.
    /// </summary>
    public static async Task WatchForNewGameAsync(LcuClient client, ProfileStore store, TimeProvider time, ILogger logger, CancellationToken ct)
    {
        var hasBaseline = store.ReadAt is not null;
        var baseline = hasBaseline ? NewestGameId(store) : null;
        bool Arrived()
        {
            if (!hasBaseline)
            {
                if (store.ReadAt is null)
                    return false;
                (hasBaseline, baseline) = (true, NewestGameId(store));
                return false;
            }

            return NewestGameId(store) is { } newest && newest != baseline;
        }

        var due = time.GetUtcNow();
        foreach (var gap in PostGameBackoff)
        {
            due += gap;
            var wait = due - time.GetUtcNow();
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, time, ct);
            if (Arrived())
                return;

            try
            {
                await ReadAsync(client, store, time, ct);
            }
            catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
            {
                logger.LogLcuFailure("Post-game profile refresh", ex);
                continue;
            }

            if (Arrived())
                return;
        }

        logger.LogInformation("The finished game isn't in the match history yet; it shows up with the next profile refresh");
    }

    private static long? NewestGameId(ProfileStore store) =>
        store.Snapshot().Profile.Matches is [var newest, ..] ? newest.GameId : null;
}
