using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>The slice whose refresh loop a successful command wakes.</summary>
internal enum RefreshSlice
{
    /// <summary>Nothing to rebuild: the handler publishes its own store, or nothing in League changed.</summary>
    None,
    State,
    Chat,
    Friends,
}

/// <summary>
/// One feature's commands. <see cref="CommandExecutor"/> validates, checks that a League client is connected, runs the
/// handler and turns its answer into a result; the handler checks the feature's preconditions against what League lists
/// right now and makes the typed <see cref="LcuClient"/> calls.
/// </summary>
internal abstract class CommandHandler
{
    /// <summary>The commands this handler runs, each with the slice to rebuild once it succeeded.</summary>
    public abstract IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; }

    /// <summary>
    /// Runs <paramref name="command"/> if it works without a League client (settings kept on the PC, republishing a store);
    /// false when it needs one.
    /// </summary>
    /// <param name="error">Null on success, otherwise a short error for the phone.</param>
    public virtual bool TryRunWithoutClient(RemoteCommand command, out string? error)
    {
        error = null;
        return false;
    }

    /// <returns>Null on success, otherwise a short error for the phone.</returns>
    public virtual Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct) =>
        Task.FromResult<string?>($"Command '{command.Type}' is not supported.");
}
