using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LeagueRemote.Connector.Https;

/// <summary>
/// Keeps the local CA and the server certificate as DPAPI-protected (current user) PKCS#12 files in
/// <c>%LOCALAPPDATA%\league-remote\certs</c>. The CA is created once (10 years); the server certificate is re-issued when it
/// expires within 30 days or when this PC's current addresses/names aren't all in its SANs.
/// </summary>
internal sealed class CertificateStore(string directory, TimeProvider time, ILogger? logger = null)
{
    public const string CaFileName = "ca.pfx.dpapi";
    public const string ServerFileName = "server.pfx.dpapi";

    // Kept stable: changing the entropy would make every existing install's DPAPI-protected files unreadable (a new CA,
    // and every phone would have to install it again).
    private static readonly byte[] Entropy = "lol-remote.certs.v1"u8.ToArray();

    public static string DefaultDirectory { get; } = AppDataFolder.Combine("certs");

    public string Directory { get; } = directory;

    /// <summary>Loads or creates the CA and a server certificate covering <paramref name="ipAddresses"/> and <paramref name="dnsNames"/>.</summary>
    public HttpsCertificates EnsureCertificates(IReadOnlyCollection<IPAddress> ipAddresses, IReadOnlyCollection<string> dnsNames)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var now = time.GetUtcNow();

        using var ca = LoadOrCreateCa(now);
        var server = Load(ServerFileName, X509KeyStorageFlags.UserKeySet);
        if (LocalCertificateAuthority.NeedsReissue(server, ca, ipAddresses, dnsNames, now))
        {
            server?.Dispose();
            using var issued = LocalCertificateAuthority.IssueServerCertificate(ca, ipAddresses, dnsNames, now);
            // Saving and re-loading the PKCS#12 also gives the key a real key container, which SChannel requires.
            server = Save(ServerFileName, issued, X509KeyStorageFlags.UserKeySet);
            logger?.LogInformation("Issued HTTPS certificate for {Names} (valid until {NotAfter:d})",
                string.Join(", ", ipAddresses.Select(a => a.ToString()).Concat(dnsNames)), server.NotAfter);
        }

        return new HttpsCertificates(server!, ca.Export(X509ContentType.Cert));
    }

    private X509Certificate2 LoadOrCreateCa(DateTimeOffset now)
    {
        var ca = Load(CaFileName, X509KeyStorageFlags.EphemeralKeySet);
        if (ca is not null && ca.HasPrivateKey && LocalCertificateAuthority.HasNameConstraints(ca)
            && ca.NotAfter.ToUniversalTime() > now.UtcDateTime + LocalCertificateAuthority.ServerValidity)
            return ca;

        ca?.Dispose();
        // A new CA invalidates the server certificate signed by the old one (NeedsReissue checks the chain).
        using var created = LocalCertificateAuthority.CreateRootCa(now);
        logger?.LogInformation("Created local certificate authority \"{Name}\" in {Directory}", LocalCertificateAuthority.CaCommonName, Directory);
        return Save(CaFileName, created, X509KeyStorageFlags.EphemeralKeySet);
    }

    private X509Certificate2? Load(string fileName, X509KeyStorageFlags flags)
    {
        var path = Path.Combine(Directory, fileName);
        if (!File.Exists(path))
            return null;

        byte[]? pfx = null;
        try
        {
            pfx = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return X509CertificateLoader.LoadPkcs12(pfx, null, flags);
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "{Path} is unreadable; replacing it", path);
            return null;
        }
        finally
        {
            if (pfx is not null)
                CryptographicOperations.ZeroMemory(pfx);
        }
    }

    private X509Certificate2 Save(string fileName, X509Certificate2 certificate, X509KeyStorageFlags flags)
    {
        var pfx = certificate.Export(X509ContentType.Pkcs12);
        try
        {
            var path = Path.Combine(Directory, fileName);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, ProtectedData.Protect(pfx, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temp, path, overwrite: true);
            return X509CertificateLoader.LoadPkcs12(pfx, null, flags);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }
}
