using System.Net;
using LeagueRemote.Connector.Pairing;
using LeagueRemote.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class GlobalRedeemGuardTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

    [Fact]
    public void Trips_on_the_eleventh_failure_within_the_window_and_then_resets()
    {
        var guard = new GlobalRedeemGuard(_time);

        for (var i = 0; i < GlobalRedeemGuard.MaxFailures; i++)
            Assert.False(guard.RecordFailure($"10.0.0.{i}"));
        Assert.True(guard.RecordFailure("10.0.0.99"));

        // Reset after tripping: the next failures start a new count.
        Assert.False(guard.RecordFailure("10.0.0.99"));
    }

    [Fact]
    public void Failures_older_than_the_window_do_not_count()
    {
        var guard = new GlobalRedeemGuard(_time);

        for (var i = 0; i < GlobalRedeemGuard.MaxFailures; i++)
            Assert.False(guard.RecordFailure($"10.0.0.{i}"));
        _time.Advance(GlobalRedeemGuard.Window);

        Assert.False(guard.RecordFailure("10.0.0.99"));
    }

    [Fact]
    public void One_source_alone_can_never_trip_it()
    {
        var guard = new GlobalRedeemGuard(_time);

        // A single address is bounded by the per-IP throttle instead.
        for (var i = 0; i < 100; i++)
            Assert.False(guard.RecordFailure("192.168.1.66"));
    }

    [Fact]
    public void Three_sources_trip_it_and_a_source_counts_again_once_its_failures_left_the_window()
    {
        var guard = new GlobalRedeemGuard(_time);
        for (var i = 0; i < GlobalRedeemGuard.MaxFailuresPerSource; i++)
            guard.RecordFailure("a");
        _time.Advance(TimeSpan.FromMinutes(6));
        for (var i = 0; i < GlobalRedeemGuard.MaxFailuresPerSource; i++)
            guard.RecordFailure("b");
        Assert.False(guard.RecordFailure("a")); // "a" is still at its cap: ignored

        _time.Advance(TimeSpan.FromMinutes(5)); // "a"'s failures expired, "b"'s are still in the window
        for (var i = 0; i < GlobalRedeemGuard.MaxFailuresPerSource; i++)
            Assert.False(guard.RecordFailure("a"));
        Assert.True(guard.RecordFailure("c"));
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:3::1", "2001:db8:1:3::/64")]
    [InlineData("::1", "::1")]
    public void Throttle_keys_ipv4_as_is_and_ipv6_by_its_64_prefix(string? address, string key)
    {
        Assert.Equal(key, RedeemThrottle.ClientKey(address is null ? null : IPAddress.Parse(address)));
    }

    [Fact]
    public void Throttle_blocks_an_address_after_its_failures_until_the_window_ends()
    {
        var throttle = new RedeemThrottle(_time);
        for (var i = 0; i < RedeemThrottle.MaxFailuresPerIp - 1; i++)
            throttle.RecordFailure("a");
        Assert.False(throttle.IsBlocked("a"));

        throttle.RecordFailure("a");
        Assert.True(throttle.IsBlocked("a"));
        Assert.False(throttle.IsBlocked("b"));

        _time.Advance(RedeemThrottle.Window);
        Assert.False(throttle.IsBlocked("a"));
    }

    [Fact]
    public void Throttle_forgets_expired_addresses_once_it_tracks_many()
    {
        var throttle = new RedeemThrottle(_time);
        for (var i = 0; i < 1000; i++)
            throttle.RecordFailure($"old-{i}");
        _time.Advance(RedeemThrottle.Window);
        for (var i = 0; i < 100; i++)
            throttle.RecordFailure($"new-{i}");

        Assert.True(throttle.TrackedCount <= 100, $"{throttle.TrackedCount} addresses still tracked");
    }

    [Fact]
    public async Task Too_many_failures_from_several_addresses_rotate_the_code_and_paired_phones_stay()
    {
        await using var server = await PhoneServerHost.StartAsync();
        var code = server.Pairing.Code;
        var (paired, grant) = await server.PairPhoneAsync();
        await using var _paired = paired;

        // 11 wrong guesses from 3 addresses (each counts at most 5 times, like each connection).
        var wrong = Guess(code);
        var attackers = new List<HubConnection>();
        try
        {
            for (var i = 0; i < 3; i++)
                attackers.Add(await server.StartPhoneAsync($"198.51.100.{i + 1}"));
            for (var i = 0; i <= GlobalRedeemGuard.MaxFailures; i++)
            {
                await Assert.ThrowsAsync<HubException>(() =>
                    attackers[i / GlobalRedeemGuard.MaxFailuresPerSource].InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, wrong));
            }
        }
        finally
        {
            foreach (var attacker in attackers)
                await attacker.DisposeAsync();
        }

        // The old code is dead (and known as such); the new one works and joins the same pairing.
        Assert.NotEqual(code, server.Pairing.Code);
        await using var newPhone = await server.StartPhoneAsync();
        var stale = await Assert.ThrowsAsync<HubException>(() => newPhone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, code));
        Assert.Contains("no longer valid", stale.Message);
        var joined = await newPhone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, server.Pairing.Code);
        Assert.Equal(grant.PairingId, joined.PairingId);

        // Paired phones are unaffected.
        await using var reconnected = await server.StartPhoneAsync();
        Assert.Equal(grant.PairingId, (await reconnected.InvokeAsync<PairingGrant>(HubMethods.Server.Authenticate, grant.Token)).PairingId);
    }

    [Fact]
    public async Task One_noisy_client_cannot_rotate_the_code()
    {
        await using var server = await PhoneServerHost.StartAsync();
        var code = server.Pairing.Code;

        // 19 guesses over 4 connections from one address stay below its block.
        const string noisyAddress = "192.168.1.66";
        var wrong = Guess(code);
        for (var c = 0; c < 4; c++)
        {
            await using var noisy = await server.StartPhoneAsync(noisyAddress);
            for (var i = 0; i < RedeemThrottle.MaxFailuresPerConnection && c * 5 + i < RedeemThrottle.MaxFailuresPerIp - 1; i++)
                await Assert.ThrowsAsync<HubException>(() => noisy.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, wrong));
        }

        // No rotation, and a real user at the same address still pairs with the code it sees.
        Assert.Equal(code, server.Pairing.Code);
        await using var phone = await server.StartPhoneAsync(noisyAddress);
        await phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, code);
    }

    [Fact]
    public async Task Retired_codes_do_not_use_up_the_address_budget()
    {
        await using var server = await PhoneServerHost.StartAsync();
        var old = server.Pairing.Code;
        server.Pairing.RotateCode();

        // Someone keeps scanning the old QR: far more attempts than the address may fail, none of them guesses.
        for (var c = 0; c < 6; c++)
        {
            await using var phone = await server.StartPhoneAsync("203.0.113.9");
            for (var i = 0; i < RedeemThrottle.MaxFailuresPerConnection; i++)
            {
                var error = await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, old));
                Assert.Contains("no longer valid", error.Message);
            }

            // The connection's own cap still applies.
            var capped = await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, old));
            Assert.Contains("Too many attempts", capped.Message);
        }

        await using var fresh = await server.StartPhoneAsync("203.0.113.9");
        await fresh.InvokeAsync<PairingGrant>(HubMethods.Server.RedeemPairingCode, server.Pairing.Code);
    }

    /// <summary>A 6-digit code that is none of the given ones.</summary>
    internal static string Guess(params string[] taken) =>
        Enumerable.Range(0, 1_000_000).Select(i => i.ToString("D6")).First(c => !taken.Contains(c));
}
