using Microsoft.AspNetCore.StaticFiles;

/// <summary>The explicitly selected, immutable physical-webroot qualification
/// profile. Ordinary smoke startup and its root response remain independent.</summary>
public static class StaticFileApplication
{
    public const string Profile = "v1";

    /// <summary>Reject unknown requested profiles before the application starts.</summary>
    public static bool SelectProfile(string? value) => value switch
    {
        null or "" => false,
        Profile => true,
        _ => throw new ArgumentException("Unsupported CLROS static-file profile.")
    };

    /// <summary>Map generated JSON and upstream middleware over the configured
    /// physical webroot. Watchers, directory listing and unknown types stay off.</summary>
    public static void Configure(WebApplication app)
    {
        var types = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "text/javascript",
            [".mjs"] = "text/javascript",
            [".json"] = "application/json",
            [".txt"] = "text/plain",
            [".wasm"] = "application/wasm",
            [".dat"] = "application/octet-stream",
            [".bin"] = "application/octet-stream"
        });
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = types,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store"
        });
        app.MapGet("/api/status", () => TypedResults.Json(
            new StaticFileStatusResponse(Profile, true, "CLROS ready — café"), StaticFileJsonContext.Default.StaticFileStatusResponse));
        app.MapGet("/api/items", (HttpContext context) => TypedResults.Json(
            new StaticFileItemsResponse(context.Request.Query["empty"] == "true" ? [] :
                [new(1, "Blazor", "Standalone WebAssembly"), new(2, "FAT32", null)]),
            StaticFileJsonContext.Default.StaticFileItemsResponse));
    }
}
