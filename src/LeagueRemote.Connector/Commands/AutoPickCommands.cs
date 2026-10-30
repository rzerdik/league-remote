using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// The champ-select automation's settings and run. They work without a League client (presets and pause live on the PC,
/// the settings screen is used outside games) and never touch the LCU directly: <see cref="AutoPickService"/> persists,
/// republishes and does the LCU writes itself.
/// </summary>
internal sealed class AutoPickCommands(ILcuSession session, AutoPickService autoPick) : CommandHandler
{
    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.SetAutoPickEnabled] = RefreshSlice.None,
        [CommandType.SetAutoPickPreset] = RefreshSlice.None,
        [CommandType.PauseAutoPick] = RefreshSlice.None,
        [CommandType.ResumeAutoPick] = RefreshSlice.None,
        [CommandType.CancelAutoPickAction] = RefreshSlice.None,
    };

    public override bool TryRunWithoutClient(RemoteCommand command, out string? error)
    {
        error = command.Type switch
        {
            CommandType.SetAutoPickEnabled => autoPick.SetEnabled(command.Enabled!.Value),
            CommandType.SetAutoPickPreset => autoPick.SetPreset(new AutoPickPreset(
                command.QueueGroup!,
                command.Role!,
                command.BanChampionIds ?? [],
                command.PickChampionIds ?? [],
                command.Spell1Id ?? 0,
                command.Spell2Id ?? 0,
                command.RunePageId ?? 0)),
            CommandType.PauseAutoPick => autoPick.Pause(),
            CommandType.ResumeAutoPick => autoPick.Resume(),
            _ => autoPick.Cancel(command.ActionId),
        };

        // Something may be automated now: let the automation look at a fresh rebuild rather than the last one it saw.
        if (error is null && session.Client is not null
            && command.Type is CommandType.SetAutoPickEnabled or CommandType.SetAutoPickPreset or CommandType.ResumeAutoPick)
            session.RequestRefresh();
        return true;
    }
}
