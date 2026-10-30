using LeagueRemote.Connector.Pairing;
using LeagueRemote.Connector.Server;
using LeagueRemote.Connector.State;
using LeagueRemote.Connector.Tray;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace LeagueRemote.Connector.Tests;

public sealed class PhoneHubTests : IAsyncLifetime
{
    private PhoneServerHost _server = null!;

    public async Task InitializeAsync() => _server = await PhoneServerHost.StartAsync();

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task A_paired_phone_runs_commands_and_gets_the_runners_result()
    {
        _server.Commands.Handler = (c, _) => Task.FromResult(new CommandResult(c.RequestId, false, "Not in a lobby."));
        var (phone, _) = await _server.PairPhoneAsync();
        await using var _phone = phone;

        var result = await phone.InvokeAsync<CommandResult>(HubMethods.Server.SendCommand, new RemoteCommand("r1", CommandType.StartMatchmaking));

        Assert.Equal(new CommandResult("r1", false, "Not in a lobby."), result);
        Assert.Equal(CommandType.StartMatchmaking, Assert.Single(_server.Commands.Received).Type);
    }

    [Fact]
    public async Task A_command_the_pc_does_not_answer_in_time_fails_with_a_timeout()
    {
        var never = new TaskCompletionSource<CommandResult>();
        _server.Commands.Handler = (_, _) => never.Task;
        var previous = PhoneHub.CommandTimeout;
        PhoneHub.CommandTimeout = TimeSpan.FromMilliseconds(100);
        try
        {
            var (phone, _) = await _server.PairPhoneAsync();
            await using var _phone = phone;

            var result = await phone.InvokeAsync<CommandResult>(HubMethods.Server.SendCommand, new RemoteCommand("slow", CommandType.AcceptReadyCheck));

            Assert.Equal(new CommandResult("slow", false, "The PC took too long to answer."), result);
        }
        finally
        {
            PhoneHub.CommandTimeout = previous;
        }
    }

    [Fact]
    public async Task A_connection_that_did_not_sign_in_cannot_send_commands()
    {
        await using var stranger = await _server.StartPhoneAsync();

        var error = await Assert.ThrowsAsync<HubException>(() =>
            stranger.InvokeAsync<CommandResult>(HubMethods.Server.SendCommand, new RemoteCommand("r1", CommandType.AcceptReadyCheck)));

        Assert.Contains("Sign in first", error.Message);
        Assert.Empty(_server.Commands.Received);
    }

    public static TheoryData<string> SliceMethods() =>
    [
        HubMethods.Client.StateUpdated, HubMethods.Client.ChatUpdated, HubMethods.Client.FriendsUpdated,
        HubMethods.Client.ProfileUpdated, HubMethods.Client.LiveGameUpdated, HubMethods.Client.AutoPickUpdated,
    ];

    [Theory, MemberData(nameof(SliceMethods))]
    public async Task A_store_change_reaches_signed_in_phones_and_a_late_phone_gets_it_replayed(string method)
    {
        await using var phone = await _server.StartPhoneAsync();
        var updates = Messages(phone, method);
        var grant = await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, _server.Pairing.Code);
        await using var stranger = await _server.StartPhoneAsync();
        var strangerGot = false;
        stranger.On<object>(method, _ => strangerGot = true);

        // The sign-in replay, then the change (a phone's RefreshState touches every store).
        Touch(method);
        for (var i = 0; i < 2; i++)
            await updates.Reader.ReadAsync().AsTask().WaitAsync(PhoneServerHost.Timeout);

        // A phone signing in later gets the current value.
        await using var late = await _server.StartPhoneAsync();
        var replayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        late.On<object>(method, _ => replayed.TrySetResult());
        await late.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, grant.Token);
        await replayed.Task.WaitAsync(PhoneServerHost.Timeout);

        Assert.False(strangerGot);
    }

    [Fact]
    public async Task A_phone_that_unpairs_itself_closes_its_other_connections_and_its_token_stops_working()
    {
        var (phone, grant) = await _server.PairPhoneAsync();
        await using var _phone = phone;
        await using var other = await _server.StartPhoneAsync();
        await other.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, grant.Token);
        var otherClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        other.Closed += _ =>
        {
            otherClosed.TrySetResult();
            return Task.CompletedTask;
        };

        Assert.True(await phone.InvokeAsync<bool>(HubMethods.Server.Unpair));

        await otherClosed.Task.WaitAsync(PhoneServerHost.Timeout);
        Assert.Equal(0, _server.Pairing.PhoneCount);
        var refused = await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, grant.Token));
        Assert.Contains("Unknown or revoked token.", refused.Message);
    }

    [Fact]
    public async Task Unpair_all_in_the_tray_disconnects_every_phone_and_their_tokens_stop_working()
    {
        var (a, grantA) = await _server.PairPhoneAsync();
        var (b, grantB) = await _server.PairPhoneAsync();
        await using var _a = a;
        await using var _b = b;
        var status = _server.Get<ConnectorStatus>();
        await PhoneServerHost.EventuallyAsync(() => status is { PairedPhones: 2, ConnectedPhones: 2 });
        var code = _server.Pairing.Code;

        status.RequestUnpair();

        await PhoneServerHost.EventuallyAsync(() => a.State == HubConnectionState.Disconnected && b.State == HubConnectionState.Disconnected);
        Assert.False(status.IsPaired);
        Assert.NotEqual(code, status.PairingCode);
        await using var again = await _server.StartPhoneAsync();
        foreach (var token in (string[])[grantA.Token, grantB.Token])
        {
            var refused = await Assert.ThrowsAsync<HubException>(() => again.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, token));
            Assert.Contains("Unknown or revoked token.", refused.Message);
        }

        var repaired = await again.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, _server.Pairing.Code);
        Assert.NotEqual(grantA.PairingId, repaired.PairingId);
    }

    [Fact]
    public async Task A_sign_in_racing_a_revocation_is_refused()
    {
        var (phone, grant) = await _server.PairPhoneAsync();
        await using var _phone = phone;
        var tokenKey = _server.Pairing.Authenticate(grant.Token)!;

        // The revocation lands between the token lookup and the sign-in: the re-check after signing in must catch it.
        var connections = _server.Get<PhoneConnections>();
        await using var racer = await _server.StartPhoneAsync();
        connections.Changed += RevokeOnce;
        void RevokeOnce()
        {
            connections.Changed -= RevokeOnce;
            _server.Pairing.Revoke(tokenKey);
        }

        var refused = await Assert.ThrowsAsync<HubException>(() => racer.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, grant.Token));
        Assert.Contains("Unknown or revoked token.", refused.Message);
        var error = await Assert.ThrowsAsync<HubException>(() =>
            racer.InvokeAsync<CommandResult>(HubMethods.Server.SendCommand, new RemoteCommand("r1", CommandType.AcceptReadyCheck)));
        Assert.Contains("Sign in first", error.Message);
    }

    private static System.Threading.Channels.Channel<object> Messages(HubConnection connection, string method)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<object>();
        connection.On<object>(method, value => channel.Writer.TryWrite(value));
        return channel;
    }

    private void Touch(string method)
    {
        switch (method)
        {
            case HubMethods.Client.StateUpdated: _server.Get<StateStore>().Touch(); break;
            case HubMethods.Client.ChatUpdated: _server.Get<ChatStore>().Touch(); break;
            case HubMethods.Client.FriendsUpdated: _server.Get<FriendsStore>().Touch(); break;
            case HubMethods.Client.ProfileUpdated: _server.Get<ProfileStore>().Touch(); break;
            case HubMethods.Client.LiveGameUpdated: _server.Get<LiveGameStore>().Touch(); break;
            case HubMethods.Client.AutoPickUpdated: _server.Get<AutoPickStore>().Touch(); break;
        }
    }
}
