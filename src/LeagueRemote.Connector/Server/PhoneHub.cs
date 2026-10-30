using LeagueRemote.Connector.Commands;
using LeagueRemote.Connector.Pairing;
using LeagueRemote.Connector.Push;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace LeagueRemote.Connector.Server;

/// <summary>
/// The SignalR hub phones talk to, at <see cref="HubMethods.Path"/>; method names match <see cref="HubMethods.Server"/>. Hubs
/// are transient: per-connection state lives in <see cref="PhoneConnections"/> or <c>Context.Items</c>, and only
/// <see cref="PhoneBroadcaster"/> sends state slices, so a replay can't overtake a newer update.
/// </summary>
internal sealed class PhoneHub(
    PhonePairing pairing,
    PhoneConnections connections,
    PhoneBroadcaster broadcaster,
    ICommandRunner commands,
    RedeemThrottle throttle,
    GlobalRedeemGuard guard,
    PushSubscriptionStore subscriptions,
    PushNotifier notifier,
    PushForegroundTracker foregroundTracker,
    VapidKeys vapid,
    IHostApplicationLifetime lifetime,
    ILogger<PhoneHub> logger) : Hub
{
    /// <summary>Group of the signed-in connections.</summary>
    public const string Phones = "phones";

    private const string FailedRedeemsKey = "failedRedeems";

    /// <summary>How long <see cref="SendCommand"/> waits for the League client. Tests shorten it.</summary>
    internal static TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public async Task<PairingGrant> RedeemPairingCode(string code)
    {
        // Brute-force layers (threat model on RedeemThrottle): this connection, its address, everyone. Only guesses count
        // towards the shared ones.
        var ip = RedeemThrottle.ClientKey(Context.GetHttpContext()?.Connection.RemoteIpAddress);
        var failures = Context.Items.TryGetValue(FailedRedeemsKey, out var value) && value is int n ? n : 0;
        if (failures >= RedeemThrottle.MaxFailuresPerConnection || throttle.IsBlocked(ip))
            throw new HubException("Too many attempts. Try again later.");

        PhoneRedemption? redemption;
        try
        {
            redemption = pairing.Redeem(code ?? "", connections.ConnectedTokenKeys());
        }
        catch (PairingStoreException ex)
        {
            logger.LogError(ex, "Pairing could not be saved");
            throw new HubException("The PC could not save the pairing. Try again.");
        }
        catch (PairingFullException ex)
        {
            // The code was right: not a failed attempt.
            logger.LogWarning("Pairing refused for {Ip}: {Reason}", ip, ex.Message);
            throw new HubException(ex.Message);
        }

        if (redemption is null)
        {
            Context.Items[FailedRedeemsKey] = failures + 1;
            if (pairing.IsRetiredCode(code ?? ""))
            {
                // A stale QR (the code was rotated): not a guess, so it doesn't use up the address's or everyone's budget.
                logger.LogInformation("Pairing attempt with a retired code from {Ip}", ip);
                throw new HubException("That pairing code is no longer valid. Use the code the PC shows now.");
            }

            throttle.RecordFailure(ip);
            logger.LogWarning("Failed pairing attempt from {Ip}", ip);
            if (guard.RecordFailure(ip))
            {
                pairing.RotateCode();
                logger.LogWarning("More than {Max} failed pairing attempts within {Window}: the pairing code was rotated",
                    GlobalRedeemGuard.MaxFailures, GlobalRedeemGuard.Window);
            }

            throw new HubException("Invalid or expired pairing code.");
        }

        if (redemption.EvictedTokenKey is { } evicted)
        {
            subscriptions.RemoveOwner(evicted);
            logger.LogInformation("Unpaired the least recently seen phone to make room (at most {Max})", PhonePairing.MaxPhones);
        }

        await SignInAsync(redemption.TokenKey);
        logger.LogInformation("A phone paired ({Count} paired)", pairing.PhoneCount);
        return redemption.Grant;
    }

    public async Task<PairingGrant> Authenticate(string token)
    {
        var tokenKey = pairing.Authenticate(token) ?? throw new HubException("Unknown or revoked token.");
        await SignInAsync(tokenKey);
        pairing.MarkSeen(tokenKey);
        return new PairingGrant(pairing.PairingId, token);
    }

    /// <summary>
    /// <see cref="HubMethods.Server.Unpair"/>: the phone revokes its own token. Its other connections are closed, so they can't
    /// keep receiving state; the caller is signed out but stays connected.
    /// </summary>
    public async Task<bool> Unpair()
    {
        var tokenKey = RequireSignedIn();
        bool revoked;
        try
        {
            revoked = pairing.Revoke(tokenKey);
        }
        catch (PairingStoreException ex)
        {
            logger.LogError(ex, "Could not unpair a phone");
            throw new HubException("The PC could not save the change. Try again.");
        }

        connections.SignOut(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Phones);
        if (!revoked)
            return false;

        subscriptions.RemoveOwner(tokenKey);
        // After the revocation: a sign-in racing it either signed in before (closed here) or finds its token gone.
        connections.Abort(key => key == tokenKey, exceptConnectionId: Context.ConnectionId);
        logger.LogInformation("A phone unpaired itself");
        return true;
    }

    public async Task<CommandResult> SendCommand(RemoteCommand command)
    {
        RequireSignedIn();

        // Runs on the app's lifetime, not the connection's: a socket blip mid "Accept" must not abort the LCU write halfway.
        var run = commands.ExecuteAsync(command, lifetime.ApplicationStopping);
        CommandResult result;
        try
        {
            result = await run.WaitAsync(CommandTimeout, Context.ConnectionAborted);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Command {Type} ({RequestId}) timed out", command?.Type, command?.RequestId);
            return new CommandResult(command?.RequestId ?? "", false, "The PC took too long to answer.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The runner should not throw; if it does, still answer so the phone doesn't wait for a timeout.
            logger.LogError(ex, "Command {RequestId} threw", command?.RequestId);
            return new CommandResult(command?.RequestId ?? "", false, "Something went wrong on the PC while running the command.");
        }

        logger.LogInformation("Command {Type} ({RequestId}): {Outcome}",
            command?.Type, result.RequestId, result.Success ? "ok" : result.Error);
        return result;
    }

    public string GetPushPublicKey()
    {
        RequireSignedIn();
        return vapid.PublicKey;
    }

    public void RegisterPushSubscription(string endpoint, string p256dh, string auth)
    {
        var tokenKey = RequireSignedIn();
        if (PushSubscriptionStore.Validate(endpoint, p256dh, auth) is { } error)
            throw new HubException(error);
        subscriptions.Register(endpoint, p256dh, auth, tokenKey);
        logger.LogInformation("Push subscription registered");
    }

    // The methods below act only on subscriptions the calling phone registered (its token key is the owner): phones of
    // one owner trust each other, but a subscription endpoint is a capability URL and shouldn't be steerable from another.

    public void UnregisterPushSubscription(string endpoint)
    {
        var tokenKey = RequireSignedIn();
        if (subscriptions.Unregister(endpoint ?? "", tokenKey))
            logger.LogInformation("Push subscription removed");
    }

    public void SetPushForeground(string endpoint, bool foreground)
    {
        var tokenKey = RequireSignedIn();
        RequireEndpoint(endpoint);
        RequireOwnSubscription(endpoint, tokenKey);
        foregroundTracker.Set(Context.ConnectionId, endpoint, foreground);
    }

    public bool SetPushPreferences(string endpoint, PushPreferences preferences)
    {
        var tokenKey = RequireSignedIn();
        RequireEndpoint(endpoint);
        if (preferences is null)
            throw new HubException("Preferences are required.");
        return subscriptions.SetPreferences(endpoint, preferences, tokenKey);
    }

    public async Task<bool> SendTestPush(string endpoint)
    {
        var tokenKey = RequireSignedIn();
        RequireEndpoint(endpoint);
        if (await notifier.SendTestAsync(endpoint, tokenKey) is { } error)
            throw new HubException(error);
        return true;
    }

    public override async Task OnConnectedAsync()
    {
        connections.Connected(Context.ConnectionId, Context.Abort);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foregroundTracker.ClearConnection(Context.ConnectionId);
        if (connections.Disconnected(Context.ConnectionId) is { } tokenKey)
            pairing.MarkSeen(tokenKey);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Signs this connection in, then re-checks the token: a revocation closes only the connections signed in before it, so
    /// a sign-in racing it must find its token gone and sign itself out. Then: the phones group, a full replay.
    /// </summary>
    private async Task SignInAsync(string tokenKey)
    {
        if (!connections.TrySignIn(Context.ConnectionId, tokenKey))
            throw new HubException("Connection is closing.");
        if (!pairing.IsPaired(tokenKey))
        {
            connections.SignOut(Context.ConnectionId);
            throw new HubException("Unknown or revoked token.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Phones);
        broadcaster.Replay(Context.ConnectionId);
    }

    private string RequireSignedIn() =>
        connections.TokenKeyOf(Context.ConnectionId) ?? throw new HubException("Sign in first.");

    private static void RequireEndpoint(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > HubMethods.Server.MaxPushEndpointLength)
            throw new HubException($"Endpoint is required (max {HubMethods.Server.MaxPushEndpointLength} chars).");
    }

    /// <summary>Not registered, or registered by another phone: the phone reads the same answer either way.</summary>
    private void RequireOwnSubscription(string endpoint, string tokenKey)
    {
        if (subscriptions.Find(endpoint, tokenKey) is null)
            throw new HubException(PushNotifier.NotRegistered);
    }
}
