using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// Champ select: hover and lock, spells, runes, skins, bench and reroll, swaps and dodge. Every command re-reads the session
/// and acts only on what it lists right now.
/// </summary>
internal sealed class ChampSelectCommands(ChampSelectLock champSelectLock) : CommandHandler
{
    private const string NotInChampSelect = "Not in champ select.";

    public override IReadOnlyDictionary<CommandType, RefreshSlice> Commands { get; } = new Dictionary<CommandType, RefreshSlice>
    {
        [CommandType.HoverChampion] = RefreshSlice.State,
        [CommandType.LockChampion] = RefreshSlice.State,
        [CommandType.SetSummonerSpells] = RefreshSlice.State,
        [CommandType.SelectRunePage] = RefreshSlice.State,
        [CommandType.SelectSkin] = RefreshSlice.State,
        [CommandType.BenchSwap] = RefreshSlice.State,
        [CommandType.Reroll] = RefreshSlice.State,
        [CommandType.DodgeChampSelect] = RefreshSlice.State,
        [CommandType.RequestSwap] = RefreshSlice.State,
        [CommandType.AcceptSwap] = RefreshSlice.State,
        [CommandType.DeclineSwap] = RefreshSlice.State,
        [CommandType.CancelSwap] = RefreshSlice.State,
    };

    public override async Task<string?> ExecuteAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        // Rune pages aren't tied to the session: the client switches the current page in any phase.
        if (command.Type == CommandType.SelectRunePage)
        {
            await client.SetCurrentRunePageAsync(command.RunePageId!.Value, ct);
            return null;
        }

        if (ChampSelectSession.Parse(await client.GetChampSelectSessionAsync(ct)) is not { } session)
            return NotInChampSelect;

        return command.Type switch
        {
            CommandType.HoverChampion or CommandType.LockChampion => await HoverOrLockAsync(client, session, command, ct),
            CommandType.SetSummonerSpells => await SetSpellsAsync(client, command, ct),
            CommandType.SelectSkin => await SelectSkinAsync(client, session, command.SkinId!.Value, ct),
            CommandType.BenchSwap => await BenchSwapAsync(client, session, command.ChampionId!.Value, ct),
            CommandType.Reroll => await RerollAsync(client, session, ct),
            CommandType.DodgeChampSelect => await DodgeAsync(client, session, ct),
            CommandType.RequestSwap or CommandType.AcceptSwap or CommandType.DeclineSwap or CommandType.CancelSwap =>
                await SwapAsync(client, session, command, ct),
            _ => await base.ExecuteAsync(client, command, ct),
        };
    }

    /// <summary>
    /// Applies to the local player's current action whatever its type ("pick" or "ban"), and only when it is the action the
    /// phone named. Hover is allowed ahead of the turn; locking needs the action to be in progress.
    /// </summary>
    private async Task<string?> HoverOrLockAsync(LcuClient client, ChampSelectSession session, RemoteCommand command, CancellationToken ct)
    {
        var action = session.FindMyAction();
        if (action is null || action.Completed)
            return "No pick or ban action pending.";
        if (command.ActionId != action.Id)
            return "That turn is over.";
        if (command.Type == CommandType.LockChampion && !action.IsInProgress)
            return "It is not your turn yet.";

        var championId = command.ChampionId!.Value;
        if (command.Type == CommandType.HoverChampion)
        {
            await client.PatchActionChampionAsync(action.Id, championId, ct);
            return null;
        }

        return (await champSelectLock.LockAsync(client, action.Id, championId, ct)).Completed
            ? null
            : "League didn't accept the lock-in. Is the champion still available?";
    }

    private static async Task<string?> SetSpellsAsync(LcuClient client, RemoteCommand command, CancellationToken ct)
    {
        await client.SetSummonerSpellsAsync(command.Spell1Id!.Value, command.Spell2Id!.Value, ct);
        return null;
    }

    private static async Task<string?> SelectSkinAsync(LcuClient client, ChampSelectSession session, int skinId, CancellationToken ct)
    {
        if (!session.AllowSkinSelection)
            return "This mode doesn't let you choose a skin.";
        var championId = ChampSelectRules.LocalLockedChampionId(session);
        if (championId <= 0)
            return "Lock in a champion first.";
        // The client locks its carousel once the game is starting.
        if (session.Timer.Phase == ChampSelectTimer.GameStarting)
            return "The game is starting.";

        // Only a skin or chroma the client's carousel lets you equip right now, never an arbitrary id.
        if (!SkinMapper.IsEquippable(await client.GetSkinCarouselAsync(ct), championId, skinId))
            return "That skin isn't available.";
        if (SkinMapper.SelectedSkinId(session) == skinId)
            return null; // already equipped: the client doesn't send it again either
        await client.SelectSkinAsync(skinId, ct);
        return null;
    }

    /// <summary>Only a champion the bench lists right now, never an arbitrary id.</summary>
    private static async Task<string?> BenchSwapAsync(LcuClient client, ChampSelectSession session, int championId, CancellationToken ct)
    {
        if (ChampSelectRules.BenchSwapRefusal(session, championId) is { } refusal)
            return refusal;
        // The client's bench greys out champions you can't play (not in the pickable set).
        var pickable = await client.GetPickableChampionIdsAsync(ct);
        if (!ChampSelectRules.IsUnusableIdList(pickable) && !pickable.Contains(championId))
            return "You can't play that champion.";

        await client.BenchSwapAsync(championId, ct);
        return null;
    }

    private static async Task<string?> RerollAsync(LcuClient client, ChampSelectSession session, CancellationToken ct)
    {
        if (ChampSelectRules.RerollRefusal(session) is { } refusal)
            return refusal;
        await client.RerollAsync(ct);
        return null;
    }

    private static async Task<string?> DodgeAsync(LcuClient client, ChampSelectSession session, CancellationToken ct)
    {
        if (ChampSelectRules.DodgeRefusal(session) is { } refusal)
            return refusal;
        await client.QuitChampSelectAsync(ct);
        return null;
    }

    /// <summary>Only acts on what League currently offers, so a stale tap cannot hit an unrelated swap.</summary>
    private static async Task<string?> SwapAsync(LcuClient client, ChampSelectSession session, RemoteCommand command, CancellationToken ct)
    {
        var kind = command.SwapKind!.Value;
        var (verb, requiredState) = command.Type switch
        {
            CommandType.RequestSwap => (SwapVerb.Request, "AVAILABLE"),
            CommandType.AcceptSwap => (SwapVerb.Accept, "RECEIVED"),
            CommandType.DeclineSwap => (SwapVerb.Decline, "RECEIVED"),
            _ => (SwapVerb.Cancel, "SENT"),
        };

        var swap = session.FindSwap(kind, command.SwapId!.Value);
        if (swap is null || swap.State != requiredState)
            return $"That {SwapLabel(kind)} is no longer available.";

        await client.SwapAsync(kind, swap.Id, verb, ct);
        return null;
    }

    private static string SwapLabel(SwapKind kind) => kind switch
    {
        SwapKind.PickOrder => "pick order swap",
        SwapKind.Position => "role swap",
        _ => "champion trade",
    };
}
