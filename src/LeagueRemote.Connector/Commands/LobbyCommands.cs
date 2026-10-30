using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>Ready check, lobby, matchmaking and roles.</summary>
internal sealed class LobbyCommands : CommandHandler
{
    internal const string PracticeToolLobbyName = "League Remote Practice";

    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.AcceptReadyCheck] = RefreshSlice.State,
        [CommandType.DeclineReadyCheck] = RefreshSlice.State,
        [CommandType.CreateLobby] = RefreshSlice.State,
        [CommandType.CreatePracticeTool] = RefreshSlice.State,
        [CommandType.StartMatchmaking] = RefreshSlice.State,
        [CommandType.CancelMatchmaking] = RefreshSlice.State,
        [CommandType.LeaveLobby] = RefreshSlice.State,
        [CommandType.SetPositionPreferences] = RefreshSlice.State,
    };

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        switch (command.Type)
        {
            case CommandType.AcceptReadyCheck:
                await client.AcceptReadyCheckAsync(ct);
                return null;

            case CommandType.DeclineReadyCheck:
                await client.DeclineReadyCheckAsync(ct);
                return null;

            case CommandType.CreateLobby:
                await client.CreateLobbyAsync(command.QueueId!.Value, ct);
                return null;

            case CommandType.CreatePracticeTool:
            {
                // Practice Tool has its own queue now; look its id up in case Riot renumbers it.
                var queueId = Queues.PracticeToolId(await client.GetQueuesAsync(ct)) ?? Queues.DefaultPracticeToolQueueId;
                await client.CreatePracticeToolLobbyAsync(queueId, PracticeToolLobbyName, ct);
                return null;
            }

            case CommandType.StartMatchmaking:
                // Custom games now have their own queues (Practice Tool = 3140) and start through matchmaking, as the
                // client does; only a legacy custom lobby without a queue id is started via start-champ-select.
                if (await client.GetLobbyAsync(ct) is { } lobby && LobbyMapper.IsLegacyCustomLobby(lobby))
                    await client.StartCustomGameAsync(ct);
                else
                    await client.StartMatchmakingAsync(ct);
                return null;

            case CommandType.CancelMatchmaking:
                await client.CancelMatchmakingAsync(ct);
                return null;

            case CommandType.LeaveLobby:
                if (await client.GetLobbyAsync(ct) is null)
                    return "Not in a lobby.";
                if (await IsSearchingAsync(client, ct))
                    return "Cancel the search first.";
                await client.LeaveLobbyAsync(ct);
                return null;

            case CommandType.SetPositionPreferences:
            {
                if (await client.GetLobbyAsync(ct) is not { } currentLobby)
                    return "Not in a lobby.";
                if (!LobbyMapper.HasPositionSelector(currentLobby))
                    return "This mode doesn't use role selection.";
                if (await IsSearchingAsync(client, ct))
                    return "Cancel the search to change your roles.";

                var first = command.FirstPosition!;
                var second = first == Positions.Fill ? Positions.Unselected : command.SecondPosition!;
                await client.SetPositionPreferencesAsync(first, second, ct);
                return null;
            }

            default:
                return await base.ExecuteAsync(client, command, ct);
        }
    }

    /// <summary>Matchmaking is searching or has found a match (see <see cref="LobbyMapper.IsSearching"/>).</summary>
    internal static async Task<bool> IsSearchingAsync(LcuClient client, CancellationToken ct) =>
        LobbyMapper.IsSearching(await client.GetLobbySearchStateAsync(ct));
}
