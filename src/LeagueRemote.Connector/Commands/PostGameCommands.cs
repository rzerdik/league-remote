using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>The post-game screen's Play Again and Leave.</summary>
internal sealed class PostGameCommands : CommandHandler
{
    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.PlayAgain] = RefreshSlice.State,
        [CommandType.LeavePostGame] = RefreshSlice.State,
    };

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        if (!Commands.ContainsKey(command.Type))
            return await base.ExecuteAsync(client, command, ct);
        if (await client.GetGameflowPhaseAsync(ct) != GameflowPhases.EndOfGame)
            return "Not on the post-game screen.";

        if (command.Type == CommandType.PlayAgain)
        {
            // The client's own Play Again button is disabled without this (rcp-fe-lol-postgame).
            if (!PostGameMapper.CanPlayAgain(await client.GetGameflowPlayerStatusAsync(ct)))
                return "Play Again isn't available for this game.";
            await client.PlayAgainAsync(ct);
        }
        else
        {
            await client.DismissStatsAsync(ct);
        }

        return null;
    }
}
