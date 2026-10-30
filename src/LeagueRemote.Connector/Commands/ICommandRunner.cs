using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>Runs a phone command against the League client. Never throws (the <see cref="CommandExecutor"/> contract).</summary>
internal interface ICommandRunner
{
    Task<CommandResult> ExecuteAsync(RemoteCommand command, CancellationToken ct);
}
