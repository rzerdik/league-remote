using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace LeagueRemote.Connector.Server;

/// <summary>
/// Front of the phone server's pipeline:
/// <list type="bullet">
/// <item><c>/setup</c> → first-time phone setup page (install the local CA, then open the HTTPS app);</item>
/// <item><c>/league-remote-ca.crt</c> → the CA's public certificate (DER), never its key;</item>
/// <item>any other plain-HTTP request from a non-loopback client → <c>307</c> to <c>https://{same host}:{HttpsPort}{path}{query}</c>
/// (no HSTS: the HTTP port must keep serving the setup page; the host is checked, see <see cref="RequestHost"/>);</item>
/// <item>HTTPS and loopback requests (a browser on this PC, the Vite dev server's proxy) pass through.</item>
/// </list>
/// </summary>
internal sealed partial class LanHttpMiddleware(RequestDelegate next, byte[] caCertificateDer, Func<int> httpsPort)
{
    public const string SetupPath = "/setup";
    public const string CaCertificatePath = "/league-remote-ca.crt";
    public const string CaCertificateContentType = "application/x-x509-ca-cert";

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);

        if (isRead && path.Equals(SetupPath, StringComparison.OrdinalIgnoreCase))
            return WriteSetupPageAsync(context);

        if (isRead && path.Equals(CaCertificatePath, StringComparison.OrdinalIgnoreCase))
            return WriteCaCertificateAsync(context);

        if (context.Request.IsHttps || IsLoopback(context.Connection.RemoteIpAddress))
            return next(context);

        if (HttpsOrigin(context) is not { } origin)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        var location = origin + context.Request.PathBase.Add(path).ToUriComponent() + context.Request.QueryString.ToUriComponent();
        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = location;
        context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    }

    private Task WriteSetupPageAsync(HttpContext context)
    {
        if (HttpsOrigin(context) is not { } origin)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        var pair = context.Request.Query[Contracts.HubMethods.Server.PairQueryParameter].ToString();
        var appUrl = origin + "/" + (PairCodeRegex().IsMatch(pair) ? $"?{Contracts.HubMethods.Server.PairQueryParameter}={pair}" : "");

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(SetupPage.Render(appUrl, CaCertificatePath, CaFingerprint(), CaName()));
    }

    private Task WriteCaCertificateAsync(HttpContext context)
    {
        context.Response.ContentType = CaCertificateContentType;
        context.Response.Headers.ContentDisposition = "inline; filename=\"league-remote-ca.crt\"";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentLength = caCertificateDer.Length;
        return HttpMethods.IsHead(context.Request.Method)
            ? Task.CompletedTask
            : context.Response.Body.WriteAsync(caCertificateDer).AsTask();
    }

    /// <summary>SHA-256 of the CA certificate, colon-separated hex, so the user can compare it on the phone.</summary>
    private string CaFingerprint() => Https.LocalCertificateAuthority.Fingerprint(caCertificateDer);

    /// <summary>The CA's common name as it is in the certificate (an existing CA may carry an older name than <see cref="Https.LocalCertificateAuthority.CaCommonName"/>).</summary>
    private string CaName()
    {
        try
        {
            using var ca = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(caCertificateDer);
            return ca.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, forIssuer: false);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Https.LocalCertificateAuthority.CaCommonName;
        }
    }

    /// <summary><c>https://{host the client used}:{HttpsPort}</c>, or null when the Host header isn't a valid host.</summary>
    private string? HttpsOrigin(HttpContext context)
    {
        try
        {
            return new UriBuilder(Uri.UriSchemeHttps, RequestHost(context), httpsPort()).Uri.GetLeftPart(UriPartial.Authority);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The host for the HTTPS link, without port: the Host header when it is an address the certificate can cover (a private
    /// or loopback IP, <c>localhost</c>), else the address this request arrived on (what the phone used to reach us), else this
    /// machine's name. The Host header is the client's to choose (the host filter allows any), so it is never reflected as is.
    /// </summary>
    private static string RequestHost(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (IPAddress.TryParse(host, out var ip) && IsCoveredByCertificate(ip))
            return host;
        if (Https.LocalCertificateAuthority.PermittedDnsNames.Contains(host, StringComparer.OrdinalIgnoreCase))
            return host;

        var local = context.Connection.LocalIpAddress;
        if (local is { IsIPv4MappedToIPv6: true })
            local = local.MapToIPv4();
        return local is not null && IsCoveredByCertificate(local) ? local.ToString() : Environment.MachineName;
    }

    private static bool IsCoveredByCertificate(IPAddress address) =>
        IsLoopback(address) || Https.LocalCertificateAuthority.IsPermitted(address);

    private static bool IsLoopback(IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex PairCodeRegex();
}
