using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeagueRemote.Contracts;

namespace LeagueRemote.Connector.Pairing;

/// <summary>A phone that just redeemed the code: its grant, its token key, and the token it pushed out (if any).</summary>
internal sealed record PhoneRedemption(PairingGrant Grant, string TokenKey, string? EvictedTokenKey);

/// <summary>The pairing file could not be written; the change was rolled back.</summary>
internal sealed class PairingStoreException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>Every one of the <see cref="PhonePairing.MaxPhones"/> phones is connected right now: none may be evicted.</summary>
internal sealed class PairingFullException(string message) : Exception(message);

/// <summary>
/// This PC's one pairing: a stable pairing id, one reusable 6-digit code (shown in the tray and QR, valid until rotated: at
/// startup, on tray Unpair and when the <see cref="GlobalRedeemGuard"/> trips) and the phones' tokens, kept only as SHA-256
/// hashes ("token keys") in <c>{data}/phones.json</c>. Writes are atomic (temp file + rename); a failed write rolls the
/// change back. Thread-safe; <see cref="Changed"/> fires outside the lock.
/// </summary>
internal sealed class PhonePairing
{
    public const string FileName = "phones.json";
    public const int MaxPhones = 10;

    /// <summary>How long (and how many) rotated codes are remembered for <see cref="IsRetiredCode"/>.</summary>
    public static readonly TimeSpan RetiredCodeMemory = TimeSpan.FromHours(24);
    public const int MaxRetiredCodes = 1000;

    /// <summary>"Last seen" is only advanced (and written) when it is at least this old: a reconnect needs no file write.</summary>
    public static readonly TimeSpan LastSeenGranularity = TimeSpan.FromHours(1);

    private const int CurrentVersion = 1;
    private const int TokenBytes = 32;

    private sealed record Phone(string Hash, DateTimeOffset CreatedAt, DateTimeOffset? LastSeen)
    {
        public DateTimeOffset LastActivity => LastSeen ?? CreatedAt;
    }

    private sealed record StoredFile(int Version, string PairingId, List<Phone> Phones);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Lock _lock = new();
    private readonly string _folder;
    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly List<Phone> _phones = [];
    private readonly Dictionary<string, DateTimeOffset> _retiredCodes = new();
    private readonly Queue<(string Code, DateTimeOffset At)> _retiredOrder = new();
    private string _pairingId = NewPairingId();
    private string _code = "";

    public PhonePairing(string dataFolder, TimeProvider time, ILogger<PhonePairing> logger)
    {
        _folder = dataFolder;
        _path = Path.Combine(dataFolder, FileName);
        _time = time;
        _logger = logger;
        Load();
        _code = NewCode();
    }

    /// <summary>The code rotated or a phone was added or removed.</summary>
    public event Action? Changed;

    public string PairingId
    {
        get
        {
            lock (_lock)
                return _pairingId;
        }
    }

    /// <summary>The current code, valid until it is rotated.</summary>
    public string Code
    {
        get
        {
            lock (_lock)
                return _code;
        }
    }

    public int PhoneCount
    {
        get
        {
            lock (_lock)
                return _phones.Count;
        }
    }

    /// <summary>
    /// Pairs a phone with <paramref name="code"/>: null for a wrong code. Beyond <see cref="MaxPhones"/> the least recently
    /// seen phone that isn't in <paramref name="connectedTokenKeys"/> is unpaired. Throws <see cref="PairingFullException"/>
    /// when all of them are connected, and <see cref="PairingStoreException"/> (nothing changed) when the file can't be written.
    /// </summary>
    public PhoneRedemption? Redeem(string code, IReadOnlySet<string> connectedTokenKeys)
    {
        PhoneRedemption redemption;
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim() != _code)
                return null;

            Phone? evicted = null;
            if (_phones.Count >= MaxPhones)
            {
                evicted = _phones.Where(p => !connectedTokenKeys.Contains(p.Hash))
                    .OrderBy(p => p.LastActivity).ThenBy(p => p.CreatedAt).ThenBy(p => p.Hash, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (evicted is null)
                {
                    throw new PairingFullException(
                        $"This PC already has {MaxPhones} phones paired. Unpair one of them first.");
                }
            }

            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
            var phone = new Phone(TokenKey(token), _time.GetUtcNow(), null);
            List<Phone> before = [.. _phones];
            if (evicted is not null)
                _phones.Remove(evicted);
            _phones.Add(phone);
            SaveOrRestore(before, "The pairing could not be saved.");
            redemption = new PhoneRedemption(new PairingGrant(_pairingId, token), phone.Hash, evicted?.Hash);
        }

        Changed?.Invoke();
        return redemption;
    }

    /// <summary>
    /// Whether <paramref name="code"/> was the code until a rotation within <see cref="RetiredCodeMemory"/> (a stale QR, not a
    /// guess). At most <see cref="MaxRetiredCodes"/> are remembered.
    /// </summary>
    public bool IsRetiredCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        lock (_lock)
        {
            PurgeRetiredCodes(_time.GetUtcNow());
            return _retiredCodes.ContainsKey(code.Trim());
        }
    }

    /// <summary>The token's key when it belongs to a paired phone, otherwise null.</summary>
    public string? Authenticate(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        var key = TokenKey(token);
        return IsPaired(key) ? key : null;
    }

    /// <summary>Whether the phone with this token key is (still) paired.</summary>
    public bool IsPaired(string tokenKey)
    {
        lock (_lock)
            return _phones.Any(p => p.Hash == tokenKey);
    }

    /// <summary>The phone signed in or disconnected: it was seen now. Best effort; a failed write is only logged.</summary>
    public void MarkSeen(string tokenKey)
    {
        lock (_lock)
        {
            var index = _phones.FindIndex(p => p.Hash == tokenKey);
            var now = _time.GetUtcNow();
            if (index < 0 || (_phones[index].LastSeen is { } seen && now - seen < LastSeenGranularity))
                return;

            _phones[index] = _phones[index] with { LastSeen = now };
            try
            {
                Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Only bookkeeping: signing in must not fail because of it. Written with the next change.
                _logger.LogWarning("Could not save when a phone was last seen ({Message})", ex.Message);
            }
        }
    }

    /// <summary>A phone unpairs itself. False when the token was already gone. Throws <see cref="PairingStoreException"/>.</summary>
    public bool Revoke(string tokenKey)
    {
        lock (_lock)
        {
            List<Phone> before = [.. _phones];
            if (_phones.RemoveAll(p => p.Hash == tokenKey) == 0)
                return false;
            SaveOrRestore(before, "The phone could not be unpaired.");
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Tray "Unpair All Phones": every token goes, the pairing gets a new id (phones take their "pair again" path) and a new
    /// code (whoever saw the old QR can't pair again). Returns the removed token keys. Throws <see cref="PairingStoreException"/>.
    /// </summary>
    public IReadOnlyList<string> RevokeAll()
    {
        List<string> removed;
        lock (_lock)
        {
            List<Phone> before = [.. _phones];
            var previousId = _pairingId;
            removed = [.. _phones.Select(p => p.Hash)];
            _phones.Clear();
            _pairingId = NewPairingId();
            try
            {
                SaveOrRestore(before, "The phones could not be unpaired.");
            }
            catch (PairingStoreException)
            {
                _pairingId = previousId;
                throw;
            }

            RotateCodeLocked();
        }

        Changed?.Invoke();
        return removed;
    }

    /// <summary>A new code; the old one is remembered as retired.</summary>
    public void RotateCode()
    {
        lock (_lock)
            RotateCodeLocked();
        Changed?.Invoke();
    }

    private void RotateCodeLocked()
    {
        var now = _time.GetUtcNow();
        _retiredCodes[_code] = now;
        _retiredOrder.Enqueue((_code, now));
        PurgeRetiredCodes(now);
        _code = NewCode();
    }

    private string NewCode()
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            if (code != _code && !_retiredCodes.ContainsKey(code))
                return code;
        }
    }

    private void PurgeRetiredCodes(DateTimeOffset now)
    {
        while (_retiredOrder.TryPeek(out var oldest)
               && (oldest.At + RetiredCodeMemory <= now || _retiredOrder.Count > MaxRetiredCodes))
        {
            _retiredOrder.Dequeue();
            if (_retiredCodes.TryGetValue(oldest.Code, out var at) && at == oldest.At)
                _retiredCodes.Remove(oldest.Code);
        }
    }

    private void SaveOrRestore(List<Phone> before, string message)
    {
        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _phones.Clear();
            _phones.AddRange(before);
            throw new PairingStoreException(message, ex);
        }
    }

    private void Save()
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(temp, JsonSerializer.Serialize(new StoredFile(CurrentVersion, _pairingId, _phones), Json));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not write {Path}", _path);
            TryDelete(temp);
            throw;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        StoredFile? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredFile>(File.ReadAllText(_path), Json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            MoveAside(ex);
            return;
        }

        if (stored is not { Version: CurrentVersion, PairingId.Length: > 0 })
        {
            MoveAside(null);
            return;
        }

        _pairingId = stored.PairingId;
        _phones.AddRange((stored.Phones ?? []).Where(p => !string.IsNullOrEmpty(p?.Hash)));
        _logger.LogInformation("Loaded {Count} paired phone(s) from {Path}", _phones.Count, _path);
    }

    /// <summary>An unreadable file must not stop the PC from starting: keep it for inspection, phones pair again.</summary>
    private void MoveAside(Exception? ex)
    {
        var aside = $"{_path}.corrupt-{_time.GetUtcNow():yyyyMMddHHmmss}";
        _logger.LogError(ex, "Pairing file {Path} is unreadable; moved to {Aside}, starting with no phones", _path, aside);
        try
        {
            File.Move(_path, aside, overwrite: true);
        }
        catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(moveEx, "Could not move {Path} aside", _path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NewPairingId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    private static string TokenKey(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
