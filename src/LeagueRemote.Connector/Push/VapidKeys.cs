using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace LeagueRemote.Connector.Push;

/// <summary>
/// This PC's VAPID (RFC 8292) key pair: P-256, base64url. <see cref="PublicKey"/> is the uncompressed point (65 bytes) the
/// PWA passes to <c>PushManager.subscribe({ applicationServerKey })</c>; <see cref="PrivateKey"/> is the scalar (32 bytes).
/// Push services bind a subscription to the public key, so it must stay the same across restarts:
/// <see cref="LoadOrCreate"/> persists it to <c>{dataPath}/vapid.dpapi</c>, DPAPI-protected for the current user like the
/// certificates in <see cref="Https.CertificateStore"/> (in memory only without a data path).
/// </summary>
internal sealed record VapidKeys(string PublicKey, string PrivateKey)
{
    public const string FileName = "vapid.dpapi";

    /// <summary>Plain JSON written by versions before the key was protected; adopted and deleted on the first start.</summary>
    public const string LegacyFileName = "vapid.json";

    private static readonly byte[] Entropy = "league-remote.vapid.v1"u8.ToArray();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static VapidKeys Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        var point = new byte[65];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return new VapidKeys(Base64Url.EncodeToString(point), Base64Url.EncodeToString(parameters.D!));
    }

    /// <summary>True when both keys decode to a valid P-256 key pair whose public point matches the private scalar.</summary>
    public bool IsValid()
    {
        try
        {
            var point = Base64Url.DecodeFromChars(PublicKey);
            var d = Base64Url.DecodeFromChars(PrivateKey);
            if (point.Length != 65 || point[0] != 0x04 || d.Length != 32)
                return false;
            using var ecdsa = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = d });
            var q = ecdsa.ExportParameters(false).Q;
            return q.X!.AsSpan().SequenceEqual(point.AsSpan(1, 32)) && q.Y!.AsSpan().SequenceEqual(point.AsSpan(33, 32));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads <c>{dataPath}/vapid.dpapi</c>; failing that, adopts a plaintext <c>vapid.json</c> of an older version (protected
    /// from now on, the plaintext deleted) so existing subscriptions keep working; failing that, generates a pair. Writes are
    /// atomic. <paramref name="dataPath"/> null/empty → a fresh in-memory pair (subscriptions then don't survive a restart).
    /// A pair that can't be read is replaced: phones notice the new public key and subscribe again on their own.
    /// </summary>
    public static VapidKeys LoadOrCreate(string? dataPath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
            return Generate();

        var path = Path.Combine(dataPath, FileName);
        if (ReadProtected(path, logger) is { } stored)
            return stored;

        var legacyPath = Path.Combine(dataPath, LegacyFileName);
        var legacy = ReadLegacy(legacyPath, logger);
        var keys = legacy ?? Generate();
        if (!Save(keys, dataPath, path, logger))
            return keys; // the plaintext file (if any) stays, so the keys at least survive the next start

        if (legacy is not null)
        {
            TryDelete(legacyPath, logger);
            logger?.LogInformation("Moved the VAPID key pair from {Legacy} to the protected {Path}", legacyPath, path);
        }
        else
        {
            logger?.LogInformation("Generated a new VAPID key pair in {Path}", path);
        }

        return keys;
    }

    private static VapidKeys? ReadProtected(string path, ILogger? logger)
    {
        if (!File.Exists(path))
            return null;
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            var stored = JsonSerializer.Deserialize<VapidKeys>(plain, Json);
            if (stored is { } && stored.IsValid())
                return stored;
            logger?.LogWarning("{Path} does not hold a valid key pair; generating a new one", path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            logger?.LogWarning(ex, "Could not read {Path}; generating a new VAPID key pair", path);
        }
        finally
        {
            if (plain is not null)
                CryptographicOperations.ZeroMemory(plain);
        }

        return null;
    }

    private static VapidKeys? ReadLegacy(string path, ILogger? logger)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var stored = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(path), Json);
            return stored is { } && stored.IsValid() ? stored : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not read {Path}; generating a new VAPID key pair", path);
            return null;
        }
    }

    private static bool Save(VapidKeys keys, string dataPath, string path, ILogger? logger)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(keys, Json);
        try
        {
            Directory.CreateDirectory(dataPath);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            logger?.LogError(ex, "Could not save {Path}; push subscriptions will not survive a restart", path);
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static void TryDelete(string path, ILogger? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not delete the plaintext {Path}; delete it by hand", path);
        }
    }
}
