using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.State;

/// <summary>
/// The raw LCU reads of one state rebuild (<see cref="LcuSnapshot"/>): what <see cref="GameStateMapper.Build"/> maps and the
/// auto pick reads. Each phase reads only what it shows. One instance per LCU connection (its name and champion caches
/// live as long); not thread-safe, the state refresh loop is sequential.
/// </summary>
internal sealed class GameStateReader(LcuClient client, PlayerNameResolver names, ChampionCatalog champions)
{
    public async Task<LcuSnapshot> ReadAsync(CancellationToken ct)
    {
        var phase = await client.GetGameflowPhaseAsync(ct);
        var snapshot = new LcuSnapshot(phase, RunePages: await client.GetRunePagesAsync(ct));

        // Where the phone offers game modes (Play, Lobby › Game Mode): the queues the client has enabled right now
        // (Quickplay, Arena and rotating modes come and go).
        if (phase is GameflowPhases.None or GameflowPhases.Lobby or GameflowPhases.EndOfGame)
            snapshot = snapshot with { Queues = await client.GetQueuesAsync(ct) };

        // The post-game screen. Every part is optional: a service that doesn't answer leaves its part out, never the rebuild.
        if (phase == GameflowPhases.EndOfGame)
        {
            snapshot = snapshot with
            {
                PostGame = new PostGameReads(
                    (await OptionalRead.TryAsync(client.GetEndOfGameStatsAsync, ct)).Json,
                    (await OptionalRead.TryAsync(client.GetLpChangeNotificationAsync, ct)).Json,
                    (await OptionalRead.TryAsync(client.GetChampionMasteryUpdatesAsync, ct)).Json,
                    (await OptionalRead.TryAsync(client.GetGameflowSessionAsync, ct)).Json,
                    (await OptionalRead.TryAsync(client.GetGameflowPlayerStatusAsync, ct)).Json),
            };
        }

        if (phase is GameflowPhases.Lobby or GameflowPhases.Matchmaking or GameflowPhases.ReadyCheck)
        {
            var lobby = await client.GetLobbyAsync(ct);
            snapshot = snapshot with
            {
                Lobby = lobby,
                LobbySearchState = await client.GetLobbySearchStateAsync(ct),
                PlayerNames = await names.ResolveLobbyAsync(lobby, ct),
            };
        }

        // Invitations arrive in any phase; the phone offers them outside champ select and the game (the client can't join
        // a party then anyway).
        if (phase != GameflowPhases.ChampSelect && !GameflowPhases.IsGameRunning(phase))
        {
            var received = await client.GetReceivedInvitationsAsync(ct);
            if (InvitationMapper.OpenReceived(received).Any())
            {
                var inviters = await names.ResolveInvitersAsync(received, ct);
                snapshot = snapshot with
                {
                    ReceivedInvitations = received,
                    // Queue names for "X invited you to Ranked Flex".
                    Queues = snapshot.Queues ?? await client.GetQueuesAsync(ct),
                    PlayerNames = snapshot.PlayerNames is { } lobbyNames ? lobbyNames.Concat(inviters).DistinctBy(p => p.Key).ToDictionary() : inviters,
                };
            }
        }

        if (phase is GameflowPhases.Matchmaking or GameflowPhases.ReadyCheck)
        {
            snapshot = snapshot with
            {
                MatchmakingSearch = await client.GetMatchmakingSearchAsync(ct),
                ReadyCheck = await client.GetReadyCheckAsync(ct),
            };
        }

        if (phase == GameflowPhases.ChampSelect)
        {
            // Re-fetched on every rebuild (session / id-list events), so the lists follow the ban and pick phases.
            var session = await client.GetChampSelectSessionAsync(ct);
            var pickable = await client.GetPickableChampionIdsAsync(ct);
            var bannable = await client.GetBannableChampionIdsAsync(ct);
            snapshot = snapshot with
            {
                ChampSelectSession = session,
                PickableChampionIds = pickable,
                BannableChampionIds = bannable,
                // League sometimes answers [-1] (or nothing) here; the mapper then offers every champion not yet taken.
                AllChampionIds = ChampSelectRules.IsUnusableIdList(bannable) ? await champions.GetAllAsync(ct) : null,
                // Only once the champion is yours (the client shows its carousel then too). A carousel the client can't
                // answer just means no skin picker, never a failed rebuild.
                SkinCarousel = ChampSelectSession.Parse(session) is { } s && SkinMapper.SkinChampionId(s) > 0
                    ? (await OptionalRead.TryAsync(client.GetSkinCarouselAsync, ct)).Json
                    : null,
            };
        }

        return snapshot;
    }
}
