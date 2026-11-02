using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LeagueRemote.Connector.Server;

/// <summary>
/// The app's license and the third-party notices (repo root <c>LICENSE</c> and <c>THIRD_PARTY_NOTICES.md</c>, embedded by
/// the csproj), as plain text for the phone's Settings › About › Licenses: the exe is downloaded alone, so they travel inside it.
/// </summary>
internal static class Licenses
{
    public const string Path = "/licenses.txt";

    private static readonly Lazy<string> Content = new(() =>
        string.Join("\n\n\n", Read("LeagueRemote.Connector.LICENSE"), Read("LeagueRemote.Connector.THIRD_PARTY_NOTICES.md")).Trim() + "\n");

    public static string Text => Content.Value;

    /// <summary><c>GET /licenses.txt</c> → <see cref="Text"/> (<c>text/plain</c>).</summary>
    public static IEndpointRouteBuilder MapLicenses(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            return Results.Text(Text, "text/plain; charset=utf-8");
        });
        return endpoints;
    }

    private static string Read(string resource)
    {
        using var stream = typeof(Licenses).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
            return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").Trim();
    }
}
