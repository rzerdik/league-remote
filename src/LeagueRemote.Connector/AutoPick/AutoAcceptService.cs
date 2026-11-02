using System.Threading.Channels;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.AutoPick;

/// <summary>
/// Auto Accept (<see cref="AutoPickSettings.AutoAccept"/>): accepts the ready check on the PC, so it works while the phone
/// sleeps. No polling and no LCU events of its own: it follows the published <see cref="ClientState.ReadyCheck"/> and the
/// setting, and wakes when the wait is over.
/// <list type="bullet">
/// <item>A ready check that appears in progress, unanswered and with the setting on waits
/// <see cref="AutoPickState.AutoAcceptDelay"/>; then the check is read again from the client (the published state can be a
/// moment old) and accepted only if it is still in progress, unanswered and the setting is still on.</item>
/// <item>Each ready check gets one decision. A check first seen with the setting off, or answered, or whose wait was cancelled
/// by turning the setting off, is left to the user even if the setting is turned on again; a failed accept isn't retried.
/// It is a new ready check once the published state has shown no check in progress (someone declined, requeue).</item>
/// </list>
/// </summary>
internal sealed class AutoAcceptService(
    StateStore state,
    AutoPickStore autoPick,
    AutoPickSettingsStore settings,
    ILcuSession session,
    TimeProvider time,
    ILogger<AutoAcceptService> logger) : BackgroundService
{
    /// <summary>The LCU's <c>state</c> of a ready check waiting for answers.</summary>
    private const string InProgress = "InProgress";

    /// <summary>The LCU's <c>playerResponse</c> before the local player answers.</summary>
    private const string Unanswered = "None";

    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>This ready check had its one decision (see the class doc). Only used by the loop.</summary>
    private bool _decided;

    /// <summary>When the waiting accept is due; null without one. Only used by the loop.</summary>
    private DateTimeOffset? _due;

    private int _steps;

    /// <summary>Steps finished so far (tests wait for the loop with it).</summary>
    internal int Steps => Volatile.Read(ref _steps);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        state.Changed += Wake;
        autoPick.Changed += Wake;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await StepAsync(ct);
                await WaitAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            state.Changed -= Wake;
            autoPick.Changed -= Wake;
        }
    }

    /// <summary>Store events fire on LCU threads and in command handlers: only signal here.</summary>
    private void Wake() => _wake.Writer.TryWrite(true);

    /// <summary>Until the state or the setting changes, or the waiting accept is due.</summary>
    private async Task WaitAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var wake = _wake.Reader.WaitToReadAsync(cts.Token).AsTask();
        if (_due is { } due)
        {
            var delay = due - time.GetUtcNow();
            if (delay > TimeSpan.Zero)
                await Task.WhenAny(wake, Task.Delay(delay, time, cts.Token));
        }
        else
        {
            await wake;
        }

        await cts.CancelAsync();
        while (_wake.Reader.TryRead(out _)) { }
    }

    /// <summary>Looks at the published ready check and acts on it. Called by the loop; tests call it directly.</summary>
    internal async Task StepAsync(CancellationToken ct)
    {
        try
        {
            await StepCoreAsync(ct);
        }
        finally
        {
            Interlocked.Increment(ref _steps);
        }
    }

    private async Task StepCoreAsync(CancellationToken ct)
    {
        var check = state.Snapshot().State.ReadyCheck;
        if (check?.State != InProgress)
        {
            _decided = false;
            _due = null;
            return;
        }

        if (_decided)
            return;

        if (check.PlayerResponse != Unanswered || !settings.Current.AutoAccept)
        {
            _decided = true;
            _due = null;
            return;
        }

        var now = time.GetUtcNow();
        if (_due is not { } due)
        {
            _due = now + AutoPickState.AutoAcceptDelay;
            return;
        }

        if (now < due)
            return;

        _decided = true;
        _due = null;
        await AcceptAsync(ct);
    }

    /// <summary>The one attempt for this ready check. A failure is logged and left to the user.</summary>
    private async Task AcceptAsync(CancellationToken ct)
    {
        if (session.Client is not { } client)
            return;

        try
        {
            var check = GameStateMapper.MapReadyCheck(await client.GetReadyCheckAsync(ct));
            if (check is not { State: InProgress, PlayerResponse: Unanswered } || !settings.Current.AutoAccept)
                return;

            await client.AcceptReadyCheckAsync(ct);
            logger.LogInformation("Auto accepted the ready check");
            session.RequestRefresh();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogLcuFailure("Auto accept", ex);
        }
    }
}
