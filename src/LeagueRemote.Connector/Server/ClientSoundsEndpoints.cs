using LeagueRemote.Connector.ClientSounds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LeagueRemote.Connector.Server;

/// <summary>The League client's own UI sounds and music for the phone, from the local install.</summary>
internal static class ClientSoundsEndpoints
{
    /// <summary>
    /// <c>GET /client-sounds/manifest.json</c> → <c>{ "sounds": { "matchFound": "/client-sounds/matchFound.ogg", ... },
    /// "music": { "musicHome": "/client-sounds/musicHome.ogg", ... } }</c> (only what is available; both empty without a
    /// League install or cache) and <c>GET /client-sounds/{key}.{ext}</c> → the audio file (cacheable for a day).
    /// </summary>
    public static IEndpointRouteBuilder MapClientSounds(this IEndpointRouteBuilder endpoints, ClientSoundLibrary? sounds)
    {
        endpoints.MapGet(ClientSoundLibrary.RoutePrefix + "/manifest.json", async (HttpContext context) =>
        {
            var manifest = sounds is null
                ? new ClientSoundManifest(new Dictionary<string, string>(), new Dictionary<string, string>())
                : await sounds.GetManifestAsync(context.RequestAborted);
            context.Response.Headers.CacheControl = "no-cache";
            return Results.Json(new { sounds = manifest.Sounds, music = manifest.Music });
        });

        endpoints.MapGet(ClientSoundLibrary.RoutePrefix + "/{file}", async (string file, HttpContext context) =>
        {
            if (sounds is null || await sounds.GetFileAsync(file, context.RequestAborted) is not { } path)
                return Results.NotFound();
            context.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(path, ClientSoundLibrary.ContentType(file), enableRangeProcessing: true);
        });

        return endpoints;
    }

    /// <summary><c>GET /rank-emblems/{tier}.png</c> → the client's emblem for that tier (cacheable for a day), else 404.</summary>
    public static IEndpointRouteBuilder MapRankEmblems(this IEndpointRouteBuilder endpoints, RankEmblemLibrary? emblems)
    {
        endpoints.MapGet(RankEmblemLibrary.RoutePrefix + "/{file}", (string file, HttpContext context) =>
        {
            if (emblems?.Get(file) is not { } data)
                return Results.NotFound();
            context.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(data, "image/png");
        });

        return endpoints;
    }
}
