using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.ChampSelect;

/// <summary>What League shows after a lock attempt.</summary>
/// <param name="Completed">The action is completed (by this lock, or by someone else first).</param>
/// <param name="ChampionId">The champion on the completed action; 0 when unknown (champ select already ended, the game is starting).</param>
internal readonly record struct LockResult(bool Completed, int ChampionId);

/// <summary>
/// Locks in (or bans with) a champ-select action and confirms it against the session: the phone's
/// <c>LockChampion</c> and the automatic ban / pick share it. The LCU can answer 2xx without locking (champion
/// unavailable) and applies a lock asynchronously, so a 2xx alone proves nothing.
/// </summary>
internal sealed class ChampSelectLock(TimeProvider time)
{
    /// <summary>Session re-reads after a failed /complete call: about 2 s at the default <see cref="ConfirmDelay"/>.</summary>
    public const int LateLockAttempts = 14;

    /// <summary>Gap between the session re-reads that confirm a lock.</summary>
    public TimeSpan ConfirmDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// PATCH the action with <c>completed: true</c> (what the client itself does), confirm; otherwise POST <c>/complete</c>
    /// and confirm again. A refused PATCH on an action that is completed already (the user locked in on the PC a moment
    /// earlier) is no failure: the result says what League shows.
    /// </summary>
    /// <exception cref="LcuRequestException">League refused the PATCH or /complete and the action stayed open.</exception>
    public async Task<LockResult> LockAsync(LcuClient client, long actionId, int championId, CancellationToken ct)
    {
        try
        {
            await client.LockActionAsync(actionId, championId, ct);
        }
        catch (LcuRequestException)
        {
            if (await CompletedAsync(client, actionId, ct, attempts: 1) is { Completed: true } done)
                return done;
            throw;
        }

        if (await CompletedAsync(client, actionId, ct) is { Completed: true } locked)
            return locked;

        try
        {
            await client.CompleteActionAsync(actionId, ct);
        }
        catch (LcuRequestException)
        {
            // League may have applied the PATCH lock late, so /complete errors on an already-completed
            // action. Give the session a moment to catch up before reporting the error.
            if (await CompletedAsync(client, actionId, ct, LateLockAttempts) is { Completed: true } late)
                return late;
            throw;
        }

        return await CompletedAsync(client, actionId, ct);
    }

    /// <summary>Re-reads the session a few times (the client applies the change asynchronously) until the action is completed.</summary>
    private async Task<LockResult> CompletedAsync(LcuClient client, long actionId, CancellationToken ct, int attempts = 4)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(ConfirmDelay, time, ct);
            // Champ select can end the moment the last lock lands (Practice Tool, blind pick): a game that is
            // starting means the lock went through.
            if (await client.GetChampSelectSessionAsync(ct) is not { } session)
                return new LockResult(await client.GetGameflowPhaseAsync(ct) is GameflowPhases.GameStart or GameflowPhases.InProgress, 0);
            if (ChampSelectSession.Parse(session)?.FindMyAction(actionId) is { Completed: true } action)
                return new LockResult(true, action.ChampionId);
        }

        return new LockResult(false, 0);
    }
}
