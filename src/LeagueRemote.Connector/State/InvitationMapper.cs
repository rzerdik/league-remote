using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.State;

/// <summary>Pure mapping of the party invitations: the ones the lobby sent and the ones the local player received.</summary>
internal static class InvitationMapper
{
    /// <summary>Sent-invitation states worth showing: still open, or turned down / failed. Accepted ones become members.</summary>
    private static readonly string[] OpenSentInvitationStates = ["Requested", "Pending", "OnHold", "Declined", "Error"];

    /// <summary>
    /// <c>lobby.invitations</c> (the same list as <c>/lol-lobby/v2/lobby/invitations</c>) without the local player's own entry
    /// (state Accepted) and without players who already joined.
    /// </summary>
    public static IReadOnlyList<SentInvitation> MapSent(JsonElement lobby, IReadOnlyDictionary<string, string>? playerNames) =>
        OpenSent(lobby)
            .Select(i => new SentInvitation(
                i.Str("toPuuid")!,
                RiotIdNames.Resolved(InviteeRef(i), playerNames) ?? Text.NullIfBlank(i.Str("toSummonerName")) ?? "Unknown",
                i.Str("state")!))
            .ToList();

    /// <summary>The players the lobby invited (names are empty in invitations).</summary>
    public static IEnumerable<PlayerRef> InviteesToResolve(JsonElement lobby) => OpenSent(lobby).Select(InviteeRef);

    private static IEnumerable<JsonElement> OpenSent(JsonElement lobby)
    {
        var memberPuuids = lobby.Arr("members").Select(m => m.Str("puuid")).Where(p => !string.IsNullOrEmpty(p)).ToHashSet();
        return lobby.Arr("invitations").Where(i =>
            i.ValueKind == JsonValueKind.Object && OpenSentInvitationStates.Contains(i.Str("state"))
            && i.Str("toPuuid") is { Length: > 0 } puuid && !memberPuuids.Contains(puuid));
    }

    /// <summary>
    /// <c>/lol-lobby/v2/received-invitations</c>: only Pending ones (the client hides declined ones) and never self-invites,
    /// which the client accepts on its own. Null when there are none.
    /// </summary>
    public static IReadOnlyList<ReceivedInvitation>? MapReceived(
        JsonElement? json,
        JsonElement? queues,
        IReadOnlyDictionary<string, string>? playerNames)
    {
        var list = OpenReceived(json)
            .Select(i =>
            {
                var gc = i.Obj("gameConfig");
                var queueId = gc?.Int("queueId") ?? 0;
                return new ReceivedInvitation(
                    i.Str("invitationId")!,
                    RiotIdNames.Resolved(InviterRef(i), playerNames) ?? Text.NullIfBlank(i.Str("fromSummonerName")) ?? "Someone",
                    queueId,
                    Queues.Name(queues, queueId),
                    gc is { } c ? Text.NullIfBlank(c.Str("gameMode")) : null,
                    i.Bool("canAcceptInvitation"));
            })
            .ToList();
        return list.Count > 0 ? list : null;
    }

    /// <summary>Received invitations waiting for an answer: state Pending, not a self-invite, with an id.</summary>
    public static IEnumerable<JsonElement> OpenReceived(JsonElement? json) =>
        json.Objects().Where(i =>
            i.Str("state") == "Pending" && !i.Bool("isSelfInvite") && !string.IsNullOrEmpty(i.Str("invitationId")));

    /// <summary>The received invitation with this id, if it is still waiting for an answer.</summary>
    public static JsonElement? FindReceived(JsonElement? json, string invitationId) =>
        OpenReceived(json).Where(i => i.Str("invitationId") == invitationId).Cast<JsonElement?>().FirstOrDefault();

    /// <summary>Senders of the received invitations that wait for an answer.</summary>
    public static IEnumerable<PlayerRef> InvitersToResolve(JsonElement? receivedInvitations) =>
        OpenReceived(receivedInvitations).Select(InviterRef);

    private static PlayerRef InviterRef(JsonElement invitation) => new(invitation.Str("fromPuuid"), invitation.Long("fromSummonerId"));

    private static PlayerRef InviteeRef(JsonElement invitation) => new(invitation.Str("toPuuid"), invitation.Long("toSummonerId"));
}
