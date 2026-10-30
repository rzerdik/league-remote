using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace LeagueRemote.Connector.Server;

/// <summary>The built PWA (web/dist), embedded in the connector assembly under <c>pwa/</c> by the csproj.</summary>
internal static class EmbeddedPwa
{
    public const string Root = "pwa";

    /// <summary>Embedded files, or an empty provider when the connector was built without the PWA.</summary>
    public static IFileProvider FileProvider { get; } = Create();

    public static FileExtensionContentTypeProvider ContentTypes { get; } = CreateContentTypes();

    /// <summary>True when <c>index.html</c> is embedded.</summary>
    public static bool IsAvailable => FileProvider.GetFileInfo("index.html").Exists;

    private static IFileProvider Create()
    {
        try
        {
            return new ManifestEmbeddedFileProvider(typeof(EmbeddedPwa).Assembly, Root);
        }
        catch (InvalidOperationException)
        {
            return new NullFileProvider(); // no embedded files manifest (built with -p:SkipWebBuild=true and no web/dist)
        }
    }

    private static FileExtensionContentTypeProvider CreateContentTypes()
    {
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"] = "application/manifest+json";
        types.Mappings[".wasm"] = "application/wasm";
        return types;
    }
}
