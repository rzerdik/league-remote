using System.Text.Json;
using System.Threading.Channels;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.AutoPick;

/// <summary>One state rebuild's raw LCU data, as the automation got it from the refresh loop.</summary>
internal sealed record AutoPickObservation(LcuClient Client, LcuSnapshot Snapshot);

/// <summary>
/// Automatic ban, pick, summoner spells and rune page in champ select (the rules are in the <see cref="AutoPickState"/> doc).
/// No polling of its own: it follows the state rebuilds (<see cref="Observe"/>) and wakes when a pending action is due, but
/// re-reads the session right before every champion write, because a rebuild can be stale and the user may have acted.
/// The phone's commands only change state under the lock; a step re-checks it before each write. Never logs player names.
/// </summary>
internal sealed class AutoPickService : BackgroundService
{
    /// <summary>Wait after a step failed (the client didn't answer a read) before trying again.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>What the automation did and decided in one champ select (keyed by its game id).</summary>
    private sealed class Run(long gameId, long id)
    {
        public long GameId { get; set; } = gameId;

        /// <summary><see cref="AutoPickRun.Id"/>.</summary>
        public readonly long Id = id;

        public string QueueGroup = "";
        public string Role = "";

        /// <summary><see cref="AutoPickRunStatus"/> from the session and presets; disabled / paused are applied on top.</summary>
        public string BaseStatus = AutoPickRunStatus.Unsupported;

        public AutoPickPending? Pending;
        public string PendingName = "";

        /// <summary>
        /// Champions auto-hovered on <see cref="Pending"/>'s action. A rebuild showing one of them there is a rebuild read
        /// before our latest hover landed, not the user's doing; the fresh reads before a write don't get that leeway.
        /// </summary>
        public readonly HashSet<int> AutoHovered = [];

        public readonly List<AutoPickLogEntry> Log = [];

        /// <summary>Actions already handled (hovered, left to the user, skipped): each is touched at most once.</summary>
        public readonly HashSet<long> Handled = [];

        /// <summary>The role the spells were last considered for; null until the first time.</summary>
        public string? SpellsRole;

        /// <summary>The spells as the automation last set or found them: different later means the user changed them.</summary>
        public (int, int)? SpellsBaseline;

        public bool RunePageDone;
    }

    private readonly AutoPickSettingsStore _settings;
    private readonly AutoPickStore _store;
    private readonly ChampSelectLock _champSelectLock;
    private readonly TimeProvider _time;
    private readonly ILogger<AutoPickService> _logger;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private AutoPickObservation? _latest;
    private Run? _run;
    private bool _paused;

    /// <summary>The gameflow phase of the previous step (null after connecting): the pause ends on a transition into "None".</summary>
    private string? _lastPhase;

    /// <summary>A pending action's due time a step already ran at or after: the loop doesn't wake for it again.</summary>
    private DateTimeOffset? _dueHandled;

    private int _steps;

    /// <summary>The last <see cref="AutoPickRun.Id"/> handed out (see <see cref="NextRunIdLocked"/>).</summary>
    private long _lastRunId;

    /// <summary>When saving the rune page names last failed (only used inside a step); null when it didn't.</summary>
    private DateTimeOffset? _namesFailedAt;

    /// <summary>Wait after the settings file couldn't take the rune page names before trying again.</summary>
    internal static readonly TimeSpan NameWriteRetryDelay = TimeSpan.FromMinutes(1);

    // Only used inside a step (one at a time): a catalog per LCU connection.
    private LcuClient? _catalogClient;
    private ChampionCatalog? _catalog;

    public AutoPickService(
        AutoPickSettingsStore settings, AutoPickStore store, ChampSelectLock champSelectLock, TimeProvider time, ILogger<AutoPickService> logger)
    {
        _settings = settings;
        _store = store;
        _champSelectLock = champSelectLock;
        _time = time;
        _logger = logger;
        // The loaded presets reach the phone even before the League client is found.
        lock (_lock)
            PublishLocked();
    }

    /// <summary>Steps run so far (tests check the loop doesn't spin).</summary>
    internal int Steps => Volatile.Read(ref _steps);

    // --- Inputs ---------------------------------------------------------------------------

    /// <summary>A state rebuild of the connected client (any phase); the loop processes the newest one.</summary>
    public void Observe(LcuClient client, LcuSnapshot snapshot)
    {
        lock (_lock)
            _latest = new AutoPickObservation(client, snapshot);
        Wake();
    }

    /// <summary>The League client went away: the run ends (the settings stay) and the pause is over.</summary>
    public void OnDisconnected()
    {
        lock (_lock)
        {
            _latest = null;
            _run = null;
            _paused = false;
            _lastPhase = null;
            PublishLocked();
        }
    }

    // --- Commands (from the phone, via CommandExecutor) -------------------------------------
    // The settings file is written outside the lock; a step that claims a lock-in meanwhile checks the saved settings.

    /// <returns>Null on success, otherwise a short error for the phone.</returns>
    public string? SetEnabled(bool enabled)
    {
        if (!_settings.Update(s => s with { Enabled = enabled }))
            return "Couldn't save the setting on the PC.";
        lock (_lock)
        {
            if (!enabled)
                CancelPendingLocked();
            PublishLocked();
        }

        return null;
    }

    /// <summary>Saves and publishes <see cref="AutoPickSettings.AutoAccept"/>; <see cref="AutoAcceptService"/> follows the published state.</summary>
    public string? SetAutoAccept(bool enabled)
    {
        if (!_settings.Update(s => s with { AutoAccept = enabled }))
            return "Couldn't save the setting on the PC.";
        lock (_lock)
            PublishLocked();
        return null;
    }

    /// <summary>
    /// Replaces one preset (an empty one removes it); a running champ select uses it for the steps still to come. A pending
    /// action whose champion the new preset no longer lists is cancelled by the next step.
    /// </summary>
    public string? SetPreset(AutoPickPreset preset)
    {
        JsonElement? pages;
        lock (_lock)
            pages = _latest?.Snapshot.RunePages;
        if (!_settings.Update(s => AutoPickSettingsStore.WithPreset(s, preset with { RunePageName = AutoPickRules.RunePageName(s, preset.RunePageId, pages) })))
            return "Couldn't save the preset on the PC.";
        lock (_lock)
            PublishLocked();
        return null;
    }

    public string? Pause()
    {
        lock (_lock)
        {
            _paused = true;
            CancelPendingLocked();
            PublishLocked();
        }

        return null;
    }

    public string? Resume()
    {
        lock (_lock)
        {
            _paused = false;
            PublishLocked();
        }

        return null;
    }

    /// <summary>Cancels the pending completion (the hover stays and the action is left to the user).</summary>
    public string? Cancel(long? actionId)
    {
        lock (_lock)
        {
            if (_run?.Pending is not { } pending)
                return "Nothing to cancel.";
            if ((actionId is { } id && id != pending.ActionId) || !pending.Cancellable)
                return "That auto pick is already done.";
            CancelPendingLocked();
            PublishLocked();
        }

        return null;
    }

    // --- Loop -----------------------------------------------------------------------------

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await StepAsync(ct);
                await WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A read failed (the client is busy or going away): keep the run and look again shortly.
                _logger.LogLcuFailure("Auto pick step", ex);
                await Task.Delay(RetryDelay, _time, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    /// <summary>
    /// Until the next rebuild or command, or until the pending action is due. A due time a step already handled is not
    /// waited for again, so a pending action that somehow stays can't make the loop spin.
    /// </summary>
    private async Task WaitAsync(CancellationToken ct)
    {
        DateTimeOffset? due;
        lock (_lock)
        {
            due = _run?.Pending is { Cancellable: true } pending ? pending.CompletesAt : null;
            if (due is { } at && at == _dueHandled && at <= _time.GetUtcNow())
                due = null;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var wake = _wake.Reader.WaitToReadAsync(cts.Token).AsTask();
        if (due is { } dueAt)
        {
            var delay = dueAt - _time.GetUtcNow();
            if (delay > TimeSpan.Zero)
                await Task.WhenAny(wake, Task.Delay(delay, _time, cts.Token));
        }
        else
        {
            await wake;
        }

        await cts.CancelAsync();
        while (_wake.Reader.TryRead(out _)) { }
    }

    private void Wake() => _wake.Writer.TryWrite(true);

    /// <summary>
    /// Processes the newest rebuild: ends or starts the run, applies spells and rune page, handles the local player's
    /// in-progress ban / pick and completes a pending one when it is due. Called by the loop; tests call it directly.
    /// </summary>
    internal async Task StepAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Interlocked.Increment(ref _steps);
            lock (_lock)
            {
                if (_run?.Pending is { Cancellable: true } pending && pending.CompletesAt <= _time.GetUtcNow())
                    _dueHandled = pending.CompletesAt;
            }

            await StepCoreAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StepCoreAsync(CancellationToken ct)
    {
        AutoPickObservation? observation;
        lock (_lock)
            observation = _latest;
        if (observation is null)
            return;

        var snapshot = observation.Snapshot;
        RememberRunePageNames(snapshot.RunePages);
        var phase = snapshot.GameflowPhase;
        if (phase != GameflowPhases.ChampSelect)
        {
            lock (_lock)
            {
                // Champ select ended (the game starts or someone dodged): the pause was for that one. Leaving for the home
                // screen ("None") ends it too, but not staying there: a pause sent in "None" still covers the next champ select.
                var intoNone = phase == GameflowPhases.None && _lastPhase is not null && _lastPhase != GameflowPhases.None;
                if (_run is not null || intoNone)
                    _paused = false;
                _lastPhase = phase;
                _run = null;
                PublishLocked();
            }

            return;
        }

        // The session can 404 for a moment while champ select starts or ends. Nothing is written then, and a pending
        // action that came due can't be confirmed: it is dropped (the hover stays) rather than left waiting.
        if (ChampSelectSession.Parse(snapshot.ChampSelectSession) is not { } session)
        {
            lock (_lock)
            {
                _lastPhase = phase;
                if (_run is { Pending: { Cancellable: true } due } current && due.CompletesAt <= _time.GetUtcNow())
                {
                    current.Pending = null;
                    PublishLocked();
                }
            }

            return;
        }

        var facts = AutoPickRules.Read(session);
        Run run;
        AutoPickPreset preset;
        lock (_lock)
        {
            _lastPhase = phase;
            if (_run is { } previous && previous.GameId != facts.GameId)
            {
                if (previous.GameId > 0 && facts.GameId > 0)
                {
                    // Re-entry without a phase change in between (dodge, then a new champ select): a fresh run.
                    _run = null;
                    _paused = false;
                }
                else
                {
                    previous.GameId = Math.Max(previous.GameId, facts.GameId);
                }
            }

            run = _run ??= new Run(facts.GameId, NextRunIdLocked());
            var group = facts.QueueGroup;
            var resolved = group is null ? null : _settings.Current.Resolve(group, facts.Role);
            run.QueueGroup = group ?? "";
            run.Role = group is null ? "" : facts.Role;
            run.BaseStatus = group is null ? AutoPickRunStatus.Unsupported
                : resolved is null ? AutoPickRunStatus.NoPreset
                : AutoPickRunStatus.Active;
            var active = IsActiveLocked(run);
            if (!active)
                CancelPendingLocked(); // the preset went away, a swap to a role without one, disabled, paused
            PublishLocked();
            if (!active)
                return;
            preset = resolved!;
        }

        await ApplySpellsAsync(observation, run, facts, preset, ct);
        await ApplyRunePageAsync(observation, run, preset, ct);
        if (AutoPickQueueGroups.HasChampionLists(run.QueueGroup) && !facts.BenchEnabled)
            await HandleActionAsync(observation, run, facts, preset, ct);
    }

    // --- Ban / pick -----------------------------------------------------------------------

    private async Task HandleActionAsync(AutoPickObservation observation, Run run, ChampSelectFacts facts, AutoPickPreset preset, CancellationToken ct)
    {
        AutoPickPending? pending;
        lock (_lock)
            pending = run.Pending;
        if (pending is not null)
        {
            await ReviewPendingAsync(observation, run, facts, preset, pending, ct);
            return;
        }

        if (facts.MyAction is not { } action)
            return;
        var kind = action.Type;
        lock (_lock)
        {
            if (!IsActiveLocked(run) || !run.Handled.Add(action.Id))
                return;
            if (action.ChampionId > 0)
            {
                // Hovered, or declared as pick intent during planning, before the turn started: the user's choice.
                LogLocked(run, AutoPickLogKinds.Manual, action.ChampionId, AutoPickRules.Manual(kind));
                PublishLocked();
                return;
            }

            if (AutoPickRules.Choices(kind, preset).Count == 0)
                return;
            run.AutoHovered.Clear();
        }

        await HoverNextAsync(observation.Client, run, preset, kind, action.Id, previous: null, IdsFor(kind, observation.Snapshot), ct);
    }

    /// <summary>
    /// The pending action against the newest rebuild: over → dropped; another champion there → the user's (manual); no
    /// longer in the preset → cancelled; our champion no longer valid → the next valid choice; due → re-read the session
    /// and, if it still shows exactly our champion and all holds, complete it.
    /// </summary>
    private async Task ReviewPendingAsync(
        AutoPickObservation observation, Run run, ChampSelectFacts facts, AutoPickPreset preset, AutoPickPending pending, CancellationToken ct)
    {
        var client = observation.Client;
        var ids = IdsFor(pending.Kind, observation.Snapshot);
        var reread = false;
        while (true)
        {
            if (facts.MyAction is not { } action || action.Id != pending.ActionId)
            {
                // Its turn is over (League completed it, the phase moved on, champ select ended).
                Record(run, pending, null, null);
                return;
            }

            bool ours;
            lock (_lock)
            {
                // A rebuild can predate our latest hover; the fresh read right before the lock can't, so it must show exactly ours.
                ours = reread
                    ? action.ChampionId == pending.ChampionId
                    : action.ChampionId <= 0 || action.ChampionId == pending.ChampionId || run.AutoHovered.Contains(action.ChampionId);
            }

            if (!ours)
            {
                Record(run, pending, AutoPickLogKinds.Manual, AutoPickRules.Manual(pending.Kind), action.ChampionId);
                return;
            }

            if (!AutoPickRules.Choices(pending.Kind, preset).Contains(pending.ChampionId))
            {
                // The user edited the preset and dropped this champion: don't lock what they no longer want.
                lock (_lock)
                {
                    if (_run == run && run.Pending == pending)
                    {
                        CancelPendingLocked();
                        PublishLocked();
                    }
                }

                return;
            }

            if (!AutoPickRules.IsValid(pending.Kind, pending.ChampionId, facts, preset, ids))
            {
                // Banned, taken by a teammate or locked by an enemy meanwhile: the next valid choice, with a new grace period.
                lock (_lock)
                {
                    if (_run != run || run.Pending != pending)
                        return;
                    run.Pending = null;
                }

                await HoverNextAsync(client, run, preset, pending.Kind, pending.ActionId, pending.ChampionId, ids, ct);
                return;
            }

            if (_time.GetUtcNow() < pending.CompletesAt || reread)
                break;

            // Due: decide on what League shows now, not on the last rebuild.
            if (ChampSelectSession.Parse(await client.GetChampSelectSessionAsync(ct)) is not { } freshSession)
            {
                Record(run, pending, null, null);
                return;
            }

            facts = AutoPickRules.Read(freshSession);
            ids = pending.Kind == AutoPickActionKinds.Ban
                ? await client.GetBannableChampionIdsAsync(ct)
                : await client.GetPickableChampionIdsAsync(ct);
            reread = true;
        }

        if (reread)
            await CompleteAsync(client, run, pending, ct);
    }

    /// <summary>
    /// Reads the session again and hovers the first valid choice on it, unless the action's turn is over or it carries a
    /// champion that isn't <paramref name="previous"/> (our previous pending champion when moving on to the next choice;
    /// null for the first hover): then it's the user's.
    /// </summary>
    private async Task HoverNextAsync(
        LcuClient client, Run run, AutoPickPreset preset, string kind, long actionId, int? previous, IReadOnlyList<int>? ids, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!IsActiveLocked(run))
                return;
        }

        var fresh = await client.GetChampSelectSessionAsync(ct);
        var readAt = _time.GetUtcNow();
        if (ChampSelectSession.Parse(fresh) is not { } freshSession)
        {
            Record(run, null, null, null); // champ select is over
            return;
        }

        var facts = AutoPickRules.Read(freshSession);
        if (facts.MyAction is not { } action || action.Id != actionId)
        {
            Record(run, null, null, null); // the turn is over
            return;
        }

        if (action.ChampionId > 0 && action.ChampionId != previous)
        {
            Record(run, null, AutoPickLogKinds.Manual, AutoPickRules.Manual(kind), action.ChampionId);
            return;
        }

        if (AutoPickRules.FirstValid(kind, facts, preset, ids) is not { } championId)
        {
            Record(run, null, AutoPickLogKinds.Skipped, AutoPickRules.NoneAvailable(kind));
            return;
        }

        lock (_lock)
        {
            if (!IsActiveLocked(run))
                return;
            run.AutoHovered.Add(championId);
        }

        try
        {
            await client.PatchActionChampionAsync(actionId, championId, ct);
        }
        catch (LcuRequestException ex)
        {
            _logger.LogLcuFailure($"Auto pick: the {kind} hover of champion {championId}", ex);
            Record(run, null, AutoPickLogKinds.Failed, AutoPickRules.HoverFailed(kind), championId);
            return;
        }

        var name = await NameAsync(client, championId, ct);
        var completesAt = AutoPickRules.CompletesAt(_time.GetUtcNow(), readAt, facts);
        lock (_lock)
        {
            if (_run != run)
                return;
            if (completesAt is not { } due)
                LogLocked(run, AutoPickLogKinds.Hovered, championId, AutoPickRules.HoveredOnly(name));
            else if (!IsActiveLocked(run))
                LogLocked(run, AutoPickLogKinds.Cancelled, championId, AutoPickRules.Cancelled(kind, name)); // paused while hovering
            else
            {
                run.Pending = new AutoPickPending(kind, actionId, championId, due);
                run.PendingName = name;
            }

            PublishLocked();
        }
    }

    /// <summary>Locks in / bans like the phone's LockChampion, unless the phone cancelled or the run stopped meanwhile.</summary>
    private async Task CompleteAsync(LcuClient client, Run run, AutoPickPending pending, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_run != run || run.Pending != pending)
                return;
            if (!IsActiveLocked(run))
            {
                CancelPendingLocked();
                PublishLocked();
                return;
            }

            pending = pending with { Cancellable = false };
            run.Pending = pending;
            PublishLocked();
        }

        var result = default(LockResult);
        try
        {
            result = await _champSelectLock.LockAsync(client, pending.ActionId, pending.ChampionId, ct);
        }
        catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
        {
            _logger.LogLcuFailure($"Auto pick: completing the {pending.Kind} of champion {pending.ChampionId}", ex);
        }
        finally
        {
            string name;
            lock (_lock)
                name = run.PendingName;
            if (!result.Completed)
            {
                Record(run, pending, AutoPickLogKinds.Failed, AutoPickRules.CompleteFailed(pending.Kind), pending.ChampionId);
            }
            else if (result.ChampionId > 0 && result.ChampionId != pending.ChampionId)
            {
                // The user locked in something else on the PC a moment before us.
                Record(run, pending, AutoPickLogKinds.Manual, AutoPickRules.Manual(pending.Kind), result.ChampionId);
            }
            else
            {
                var kind = pending.Kind == AutoPickActionKinds.Ban ? AutoPickLogKinds.Banned : AutoPickLogKinds.Locked;
                Record(run, pending, kind, AutoPickRules.Completed(pending.Kind, name), pending.ChampionId);
            }
        }
    }

    /// <summary>
    /// Clears <paramref name="pending"/> (only while it is still the run's; null = nothing to clear), adds the log entry
    /// (none when <paramref name="logKind"/> is null) and publishes. Nothing when the run is over.
    /// </summary>
    private void Record(Run run, AutoPickPending? pending, string? logKind, string? text, int championId = 0)
    {
        lock (_lock)
        {
            if (_run != run)
                return;
            if (pending is not null)
            {
                if (run.Pending != pending)
                    return;
                run.Pending = null;
            }

            if (logKind is not null)
                LogLocked(run, logKind, championId, text!);
            PublishLocked();
        }
    }

    private static IReadOnlyList<int>? IdsFor(string kind, LcuSnapshot snapshot) =>
        kind == AutoPickActionKinds.Ban ? snapshot.BannableChampionIds : snapshot.PickableChampionIds;

    // --- Spells and rune page -------------------------------------------------------------

    /// <summary>
    /// Once per champ select (first time the automation applies), and again after a position swap when the spells are still
    /// what the automation last set or found: PATCH my-selection when the preset's differ.
    /// </summary>
    private async Task ApplySpellsAsync(AutoPickObservation observation, Run run, ChampSelectFacts facts, AutoPickPreset preset, CancellationToken ct)
    {
        if (facts.MySpells is not { } current)
            return;
        var target = (preset.Spell1Id, preset.Spell2Id);
        lock (_lock)
        {
            // Not active (paused meanwhile): left for later, so a Resume still applies them.
            if (run.SpellsRole == facts.Role || !IsActiveLocked(run))
                return;
            var swapped = run.SpellsRole is not null;
            run.SpellsRole = facts.Role;
            if (swapped && run.SpellsBaseline != current)
                return; // the user changed them since: theirs
            if (preset.Spell1Id <= 0 || preset.Spell2Id <= 0 || current == target)
            {
                run.SpellsBaseline = current;
                return;
            }
        }

        try
        {
            await observation.Client.SetSummonerSpellsAsync(target.Spell1Id, target.Spell2Id, ct);
            lock (_lock)
                run.SpellsBaseline = target;
            Record(run, null, AutoPickLogKinds.Spells, AutoPickRules.SpellsSet);
        }
        catch (LcuRequestException ex)
        {
            _logger.LogLcuFailure($"Auto pick: summoner spells {target.Spell1Id}/{target.Spell2Id}", ex);
            lock (_lock)
                run.SpellsBaseline = current;
            Record(run, null, AutoPickLogKinds.Failed, AutoPickRules.SpellsFailed);
        }
    }

    /// <summary>
    /// Keeps the presets' <see cref="AutoPickPreset.RunePageName"/>s in step with the client's pages (a page renamed on the
    /// PC, a preset saved while League was closed), so the phone can name them once League is
    /// closed. Writes the settings file only when a name changed; a page that is gone keeps its last name.
    /// </summary>
    private void RememberRunePageNames(JsonElement? pages)
    {
        if (AutoPickRules.WithRunePageNames(_settings.Current, pages) is null)
            return;
        if (_namesFailedAt is { } failed && _time.GetUtcNow() - failed < NameWriteRetryDelay)
            return;
        if (!_settings.Update(s => AutoPickRules.WithRunePageNames(s, pages) ?? s))
        {
            _namesFailedAt = _time.GetUtcNow(); // logged by the store; not again with every rebuild
            return;
        }

        _namesFailedAt = null;
        lock (_lock)
            PublishLocked();
    }

    /// <summary>Once per champ select: switch to the preset's rune page when it exists and isn't current.</summary>
    private async Task ApplyRunePageAsync(AutoPickObservation observation, Run run, AutoPickPreset preset, CancellationToken ct)
    {
        lock (_lock)
        {
            if (run.RunePageDone)
                return;
            if (preset.RunePageId <= 0)
            {
                run.RunePageDone = true;
                return;
            }
        }

        // Not read in this rebuild (a hiccup): the next one has it.
        if (observation.Snapshot.RunePages is not { ValueKind: JsonValueKind.Array } pages)
            return;

        var page = pages.EnumerateArray().FirstOrDefault(p => p.ValueKind == JsonValueKind.Object && p.Long("id") == preset.RunePageId);
        if (page.ValueKind != JsonValueKind.Object)
        {
            lock (_lock)
                run.RunePageDone = true;
            Record(run, null, AutoPickLogKinds.Skipped, AutoPickRules.RunePageMissing);
            return;
        }

        lock (_lock)
        {
            // Not active (paused meanwhile): left for later, so a Resume still applies it.
            if (!IsActiveLocked(run))
                return;
            run.RunePageDone = true;
        }

        if (page.Bool("current"))
            return;

        try
        {
            await observation.Client.SetCurrentRunePageAsync(preset.RunePageId, ct);
            Record(run, null, AutoPickLogKinds.RunePage, AutoPickRules.RunePageSet);
        }
        catch (LcuRequestException ex)
        {
            _logger.LogLcuFailure("Auto pick: the rune page", ex);
            Record(run, null, AutoPickLogKinds.Failed, AutoPickRules.RunePageFailed);
        }
    }

    // --- State ----------------------------------------------------------------------------

    private string StatusLocked(Run run) =>
        !_settings.Current.Enabled ? AutoPickRunStatus.Disabled
        : _paused ? AutoPickRunStatus.Paused
        : run.BaseStatus;

    /// <summary>Still the current run, enabled, not paused, supported queue with a preset: writes allowed.</summary>
    private bool IsActiveLocked(Run run) => _run == run && StatusLocked(run) == AutoPickRunStatus.Active;

    /// <summary>Cancels a pending completion that hasn't started yet; the hover stays and the action isn't touched again.</summary>
    private void CancelPendingLocked()
    {
        if (_run is not { Pending: { Cancellable: true } pending } run)
            return;
        run.Pending = null;
        LogLocked(run, AutoPickLogKinds.Cancelled, pending.ChampionId, AutoPickRules.Cancelled(pending.Kind, run.PendingName));
    }

    private void LogLocked(Run run, string kind, int championId, string text)
    {
        run.Log.Add(new AutoPickLogEntry(_time.GetUtcNow(), kind, championId, Text.Clip(text, AutoPickState.MaxLogTextLength, ellipsis: true)));
        if (run.Log.Count > AutoPickState.MaxLogEntries)
            run.Log.RemoveRange(0, run.Log.Count - AutoPickState.MaxLogEntries);
        _logger.LogInformation("Auto pick: {Kind} (champion {ChampionId})", kind, championId);
    }

    private void PublishLocked() =>
        _store.Set(new AutoPickState(
            _settings.Current,
            _paused,
            _run is { } run ? new AutoPickRun(run.QueueGroup, run.Role, StatusLocked(run), run.Pending, [.. run.Log], run.Id) : null,
            _time.GetUtcNow()));

    /// <summary>
    /// A run id larger than any before, also across connector restarts: the connector clock in ms, or one more than the
    /// last id when two runs start within the same millisecond (or the clock went back).
    /// </summary>
    private long NextRunIdLocked() => _lastRunId = Math.Max(_lastRunId + 1, _time.GetUtcNow().ToUnixTimeMilliseconds());

    private async Task<string> NameAsync(LcuClient client, int championId, CancellationToken ct)
    {
        if (!ReferenceEquals(_catalogClient, client))
        {
            _catalogClient = client;
            _catalog = new ChampionCatalog(client);
        }

        try
        {
            return AutoPickRules.ChampionLabel(await _catalog!.GetNameAsync(championId, ct), championId);
        }
        catch (Exception ex) when (LcuErrors.IsTransient(ex, ct))
        {
            return AutoPickRules.ChampionLabel(null, championId);
        }
    }
}
