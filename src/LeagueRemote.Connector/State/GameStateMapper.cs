using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>
/// Pure mapping from one rebuild's LCU JSON to <see cref="ClientState"/>.
/// POLICY: own lobby and own team; from "theirTeam" only what the League client shows anyway (champion, hover,
/// assigned position, bans). Enemy names, puuids and summoner ids are never read. Own lobby members and named teammates
/// carry their puuid (for View Profile); a teammate an anonymous draft hides doesn't. No in-game data here: the only
/// in-game information (loaded flag + game clock) comes from Riot's official Live Client Data API, see
/// <see cref="LiveClient.LiveClientPoller"/>.
/// </summary>
internal static class GameStateMapper
{
    public static ClientState Build(LcuSnapshot s, DateTimeOffset now) => new(
        LcuConnected: true,
        GameflowPhase: string.IsNullOrEmpty(s.GameflowPhase) ? GameflowPhases.None : s.GameflowPhase,
        ReadyCheck: MapReadyCheck(s.ReadyCheck),
        Lobby: LobbyMapper.Map(s.Lobby, s.LobbySearchState, s.MatchmakingSearch, s.PlayerNames),
        ChampSelect: ChampSelectMapper.Map(s.ChampSelectSession, s.PickableChampionIds ?? [], s.BannableChampionIds ?? [], s.AllChampionIds, s.SkinCarousel, now),
        RunePages: MapRunePages(s.RunePages),
        UpdatedAt: now,
        AvailableQueueIds: Queues.AvailableIds(s.Queues),
        Invitations: InvitationMapper.MapReceived(s.ReceivedInvitations, s.Queues, s.PlayerNames),
        PostGame: s.GameflowPhase == GameflowPhases.EndOfGame && s.PostGame is { } p
            ? PostGameMapper.Map(p.Stats, p.LpChange, p.MasteryUpdate, p.Session, p.PlayerStatus)
            : null,
        ChampionLanes: ChampionLanesMapper.Map(s.ChampionPositions));

    public static ReadyCheckState? MapReadyCheck(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } e)
            return null;

        var state = e.Str("state") ?? "Invalid";
        if (state == "Invalid")
            return null;

        return new ReadyCheckState(state, e.Str("playerResponse") ?? "None", e.Dbl("timer"));
    }

    public static IReadOnlyList<RunePage> MapRunePages(JsonElement? json) =>
        json.Objects()
            .Select(p => new RunePage(p.Long("id"), p.Str("name") ?? "", p.Bool("current"), p.Bool("isEditable")))
            .ToList();
}
