using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// Runs whitelisted <see cref="RemoteCommand"/>s: validates, checks for a League client, hands the command to its feature's
/// <see cref="CommandHandler"/> and wakes the slice the handler names. Never throws; failures become a
/// <see cref="CommandResult"/>.
/// </summary>
internal sealed class CommandExecutor : ICommandRunner
{
    private readonly ILcuSession _session;
    private readonly StateStore _stateStore;
    private readonly ChatStore _chatStore;
    private readonly FriendsStore _friendsStore;
    private readonly ProfileStore _profileStore;
    private readonly LiveGameStore _liveGameStore;
    private readonly AutoPickStore _autoPickStore;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly Dictionary<CommandType, CommandHandler> _handlers = [];

    public CommandExecutor(
        ILcuSession session,
        StateStore stateStore,
        ChatStore chatStore,
        FriendsStore friendsStore,
        ProfileStore profileStore,
        LiveGameStore liveGameStore,
        AutoPickStore autoPickStore,
        AutoPickService autoPick,
        ChampSelectLock champSelectLock,
        TimeProvider time,
        ILogger<CommandExecutor> logger)
    {
        _session = session;
        _stateStore = stateStore;
        _chatStore = chatStore;
        _friendsStore = friendsStore;
        _profileStore = profileStore;
        _liveGameStore = liveGameStore;
        _autoPickStore = autoPickStore;
        _logger = logger;

        CommandHandler[] handlers =
        [
            new LobbyCommands(),
            new ChampSelectCommands(champSelectLock),
            new PostGameCommands(),
            new ChatCommands(session),
            new SocialCommands(),
            new ProfileCommands(session, stateStore, profileStore, time),
            new AutoPickCommands(session, autoPick),
        ];
        foreach (var handler in handlers)
        {
            foreach (var type in handler.Commands.Keys)
                _handlers.Add(type, handler); // throws on a command claimed twice
        }
    }

    public async Task<CommandResult> ExecuteAsync(RemoteCommand command, CancellationToken ct)
    {
        // The only whitelist check on the PC: everything the phone sends comes through here.
        if (CommandWhitelist.Validate(command) is { } validationError)
            return new CommandResult(command?.RequestId ?? "", false, validationError);

        if (command.Type == CommandType.RefreshState)
        {
            RepublishEverything();
            return Ok(command);
        }

        _handlers.TryGetValue(command.Type, out var handler);
        try
        {
            if (handler is not null && handler.TryRunWithoutClient(command, out var offlineError))
                return offlineError is null ? Ok(command) : Fail(command, offlineError);

            if (_session.Client is not { } client)
                return Fail(command, "League client is not running.");
            if (handler is null)
                return Fail(command, $"Command '{command.Type}' is not supported.");

            var error = await handler.ExecuteAsync(client, command, ct);
            if (error is null)
                Refresh(handler.Commands[command.Type]);
            return error is null ? Ok(command) : Fail(command, error);
        }
        catch (LcuRequestException ex)
        {
            // League's own text stays on the PC (Debug: it may name a player); the phone gets a fixed sentence.
            _logger.LogLcuFailure($"Command {command.Type}", ex);
            return Fail(command, LcuRefusals.Message(ex));
        }
        catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
        {
            _logger.LogWarning(ex, "Command {Type} could not reach the League client", command.Type);
            return Fail(command, "Could not reach the League client.");
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            // Anything else must still produce a result, or the phone waits for a timeout.
            _logger.LogError(ex, "Command {Type} failed unexpectedly", command.Type);
            return Fail(command, "Something went wrong on the PC while running the command.");
        }
    }

    /// <summary>RefreshState: rebuild what the LCU feeds and republish every slice, so the phone hears back on each.</summary>
    private void RepublishEverything()
    {
        _session.RequestRefresh();
        _session.RequestChatRefresh();
        _session.RequestFriendsRefresh();
        _stateStore.Touch();
        _chatStore.Touch();
        _friendsStore.Touch();
        _profileStore.Touch();
        _liveGameStore.Touch();
        _autoPickStore.Touch();
    }

    private void Refresh(RefreshSlice slice)
    {
        switch (slice)
        {
            case RefreshSlice.State:
                _session.RequestRefresh();
                break;
            case RefreshSlice.Chat:
                _session.RequestChatRefresh();
                break;
            case RefreshSlice.Friends:
                _session.RequestFriendsRefresh();
                break;
        }
    }

    private static CommandResult Ok(RemoteCommand c) => new(c.RequestId, true);

    private static CommandResult Fail(RemoteCommand c, string error) => new(c.RequestId, false, error);
}
