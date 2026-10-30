using System.Text.Json;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// Party invitations, friends and friend requests. Each acts only on an invitation, friend or request the client lists
/// right now, never on an id just because the phone sent it.
/// </summary>
internal sealed class SocialCommands : CommandHandler
{
    private const string InviteGone = "That invite is gone.";

    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        // Invitations are part of the game state (lobby and received invitations).
        [CommandType.AcceptInvitation] = RefreshSlice.State,
        [CommandType.DeclineInvitation] = RefreshSlice.State,
        [CommandType.SendInvitation] = RefreshSlice.State,
        [CommandType.RemoveFriend] = RefreshSlice.Friends,
        [CommandType.SendFriendRequest] = RefreshSlice.Friends,
        [CommandType.AcceptFriendRequest] = RefreshSlice.Friends,
        [CommandType.DeclineFriendRequest] = RefreshSlice.Friends,
        [CommandType.CancelFriendRequest] = RefreshSlice.Friends,
    };

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        switch (command.Type)
        {
            case CommandType.AcceptInvitation:
            {
                if (InvitationMapper.FindReceived(await client.GetReceivedInvitationsAsync(ct), command.InvitationId!) is not { } invitation)
                    return InviteGone;
                if (!invitation.Bool("canAcceptInvitation"))
                    return "League won't let you join that game.";
                var phase = await client.GetGameflowPhaseAsync(ct);
                if (phase == GameflowPhases.ChampSelect || GameflowPhases.IsGameRunning(phase))
                    return "You can't join a party right now.";
                if (await LobbyCommands.IsSearchingAsync(client, ct))
                    return "Cancel the search first.";
                await client.AcceptInvitationAsync(command.InvitationId!, ct);
                return null;
            }

            case CommandType.DeclineInvitation:
                if (InvitationMapper.FindReceived(await client.GetReceivedInvitationsAsync(ct), command.InvitationId!) is null)
                    return InviteGone;
                await client.DeclineInvitationAsync(command.InvitationId!, ct);
                return null;

            case CommandType.SendInvitation:
                return await SendInvitationAsync(client, command.Puuid!, ct);

            case CommandType.RemoveFriend:
            {
                if (FriendsMapper.FindById(await client.GetFriendsAsync(ct), command.FriendId!) is not { } friend)
                    return "That friend is no longer on your list.";
                await client.RemoveFriendAsync(friend.Str("id")!, ct);
                return null;
            }

            case CommandType.SendFriendRequest:
                try
                {
                    await client.SendFriendRequestAsync(command.GameName!, command.TagLine!, ct);
                    return null;
                }
                catch (LcuRequestException ex)
                {
                    return LcuRefusals.FriendRequestError(ex);
                }

            case CommandType.AcceptFriendRequest:
            case CommandType.DeclineFriendRequest:
            case CommandType.CancelFriendRequest:
            {
                // In the direction the command expects, so a stale tap can't cancel a request the user just received (or
                // accept one they sent).
                var direction = command.Type == CommandType.CancelFriendRequest ? FriendRequestDirections.Outgoing : FriendRequestDirections.Incoming;
                if (FriendRequestMapper.Find(await client.GetFriendRequestsAsync(ct), command.Puuid!, direction) is not { } request)
                    return "That friend request is gone.";

                if (command.Type == CommandType.AcceptFriendRequest)
                    await client.AcceptFriendRequestAsync(request.Puuid, ct);
                else
                    await client.DeleteFriendRequestAsync(request.Puuid, ct);
                return null;
            }

            default:
                return await base.ExecuteAsync(client, command, ct);
        }
    }

    /// <summary>Friends only: the phone picks from the friends list, and that is all it may invite.</summary>
    private static async Task<string?> SendInvitationAsync(LcuClient client, string puuid, CancellationToken ct)
    {
        if (await client.GetLobbyAsync(ct) is not { } lobby)
            return "Not in a lobby.";
        if (LobbyMapper.IsMember(lobby, puuid))
            return "Already in your party.";
        if (LobbyMapper.IsPartyFull(lobby))
            return "Your party is full.";
        if (!LobbyMapper.CanInvite(lobby))
            return "Only the party leader can invite.";
        if (await LobbyCommands.IsSearchingAsync(client, ct))
            return "Cancel the search first.";

        if (FriendsMapper.FindByPuuid(await client.GetFriendsAsync(ct), puuid) is not { } friend)
            return "Only friends can be invited.";
        var summonerId = friend.Long("summonerId");
        if (summonerId <= 0 && await client.GetSummonerByPuuidAsync(puuid, ct) is { ValueKind: JsonValueKind.Object } summoner)
            summonerId = summoner.Long("summonerId");
        if (summonerId <= 0)
            return "League doesn't know that player right now.";

        await client.SendInvitationAsync(summonerId, ct);
        return null;
    }
}
