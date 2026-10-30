using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LeagueRemote.Connector.Server;

/// <summary>
/// Browser hardening headers on every response of both ports (PWA, hub, setup page, CA download). The page keeps a
/// bearer token in <c>localStorage</c> and shows third-party images, so the policy says exactly what it may load.
/// </summary>
internal static class SecurityHeaders
{
    /// <summary>
    /// What the PWA really loads (checked against the built <c>web/dist</c>):
    /// <list type="bullet">
    /// <item><c>script-src</c>: own bundles only, no inline scripts; <c>'wasm-unsafe-eval'</c> for the self-hosted ZXing QR decoder.</item>
    /// <item><c>style-src 'unsafe-inline'</c>: Vue's <c>:style</c> bindings and the setup page's own stylesheet.</item>
    /// <item><c>img-src</c>: Data Dragon art; <c>blob:</c> for the QR decoder's image fallback.</item>
    /// <item><c>media-src blob:</c>: the generated silent clip that keeps iOS audio alive in the background.</item>
    /// <item><c>connect-src</c>: the hub (<c>wss:</c> spelled out: older Safari didn't count WebSockets as <c>'self'</c>)
    /// and Data Dragon JSON.</item>
    /// <item>No frames, plugins, foreign form targets or <c>&lt;base&gt;</c> tricks.</item>
    /// </list>
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; "
        + "script-src 'self' 'wasm-unsafe-eval'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' blob: https://ddragon.leagueoflegends.com; "
        + "media-src 'self' blob:; "
        + "font-src 'self'; "
        + "connect-src 'self' wss: https://ddragon.leagueoflegends.com; "
        + "worker-src 'self'; "
        + "manifest-src 'self'; "
        + "object-src 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'";

    /// <summary>The QR scanner needs the camera; nothing else on the page needs a powerful feature.</summary>
    public const string PermissionsPolicy = "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY"; // for browsers without frame-ancestors
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;
            return next(context);
        });
}
