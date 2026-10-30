using LeagueRemote.Connector.Pairing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class PhonePairingTests : IDisposable
{
    private static readonly IReadOnlySet<string> NoneConnected = new HashSet<string>();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-30T12:00:00Z"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private PhonePairing Load() => new(_dir, _time, NullLogger<PhonePairing>.Instance);

    [Fact]
    public void The_code_pairs_any_number_of_phones_under_one_stable_pairing_id()
    {
        var pairing = Load();
        var first = pairing.Redeem(pairing.Code, NoneConnected)!;
        var second = pairing.Redeem($" {pairing.Code} ", NoneConnected)!;

        Assert.Equal(pairing.PairingId, first.Grant.PairingId);
        Assert.Equal(first.Grant.PairingId, second.Grant.PairingId);
        Assert.NotEqual(first.Grant.Token, second.Grant.Token);
        Assert.Equal(first.TokenKey, pairing.Authenticate(first.Grant.Token));
        Assert.Null(pairing.Authenticate("not-a-token"));
        Assert.Equal(2, pairing.PhoneCount);
    }

    [Fact]
    public void A_wrong_code_is_a_guess_and_a_rotated_one_is_retired()
    {
        var pairing = Load();
        var old = pairing.Code;
        Assert.Null(pairing.Redeem(GlobalRedeemGuardTests.Guess(old), NoneConnected));
        Assert.False(pairing.IsRetiredCode(GlobalRedeemGuardTests.Guess(old)));

        pairing.RotateCode();
        Assert.NotEqual(old, pairing.Code);
        Assert.Null(pairing.Redeem(old, NoneConnected));
        Assert.True(pairing.IsRetiredCode(old));
        Assert.False(pairing.IsRetiredCode(pairing.Code));

        _time.Advance(PhonePairing.RetiredCodeMemory);
        Assert.False(pairing.IsRetiredCode(old));
    }

    [Fact]
    public void Phones_survive_a_restart_but_the_code_does_not()
    {
        var pairing = Load();
        var grant = pairing.Redeem(pairing.Code, NoneConnected)!.Grant;

        var reloaded = Load();
        Assert.Equal(pairing.PairingId, reloaded.PairingId);
        Assert.NotNull(reloaded.Authenticate(grant.Token));
        Assert.NotEqual(pairing.Code, reloaded.Code);
        Assert.DoesNotContain(grant.Token, File.ReadAllText(Path.Combine(_dir, PhonePairing.FileName)));
    }

    [Fact]
    public void An_unreadable_file_is_moved_aside()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, PhonePairing.FileName), "{ not json");

        Assert.Equal(0, Load().PhoneCount);
        Assert.Single(Directory.GetFiles(_dir, PhonePairing.FileName + ".corrupt-*"));
    }

    [Fact]
    public void A_phone_unpairs_itself_and_unpair_all_starts_a_new_pairing()
    {
        var pairing = Load();
        var a = pairing.Redeem(pairing.Code, NoneConnected)!;
        var b = pairing.Redeem(pairing.Code, NoneConnected)!;

        Assert.True(pairing.Revoke(a.TokenKey));
        Assert.False(pairing.Revoke(a.TokenKey));
        Assert.Null(pairing.Authenticate(a.Grant.Token));
        Assert.NotNull(pairing.Authenticate(b.Grant.Token));

        var (id, code) = (pairing.PairingId, pairing.Code);
        Assert.Equal([b.TokenKey], pairing.RevokeAll());
        Assert.Null(pairing.Authenticate(b.Grant.Token));
        Assert.NotEqual(id, pairing.PairingId);
        Assert.NotEqual(code, pairing.Code);
        Assert.True(pairing.IsRetiredCode(code));
        Assert.Equal(pairing.PairingId, Load().PairingId);
    }

    [Fact]
    public void Beyond_the_cap_the_least_recently_seen_disconnected_phone_goes_and_all_connected_refuses()
    {
        var pairing = Load();
        var phones = new List<PhoneRedemption>();
        for (var i = 0; i < PhonePairing.MaxPhones; i++)
        {
            phones.Add(pairing.Redeem(pairing.Code, NoneConnected)!);
            _time.Advance(PhonePairing.LastSeenGranularity);
        }

        pairing.MarkSeen(phones[0].TokenKey); // the oldest was used recently: phones[1] is the least recently seen
        var connected = phones.Skip(2).Select(p => p.TokenKey).ToHashSet();
        var next = pairing.Redeem(pairing.Code, connected)!;
        Assert.Equal(phones[1].TokenKey, next.EvictedTokenKey);
        Assert.Equal(PhonePairing.MaxPhones, pairing.PhoneCount);

        var all = phones.Where(p => p != phones[1]).Select(p => p.TokenKey).Append(next.TokenKey).ToHashSet();
        Assert.Throws<PairingFullException>(() => pairing.Redeem(pairing.Code, all));
        Assert.Equal(PhonePairing.MaxPhones, pairing.PhoneCount);
    }

    [Fact]
    public void A_failed_write_rolls_the_new_phone_back()
    {
        var pairing = Load();
        var first = pairing.Redeem(pairing.Code, NoneConnected)!;

        // The temp file can't be created while a directory of that name is in the way.
        Directory.CreateDirectory(Path.Combine(_dir, PhonePairing.FileName + ".tmp"));
        Assert.Throws<PairingStoreException>(() => pairing.Redeem(pairing.Code, NoneConnected));

        Assert.Equal(1, pairing.PhoneCount);
        Assert.NotNull(pairing.Authenticate(first.Grant.Token));
    }
}
