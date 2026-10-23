using Microsoft.AspNetCore.Server.Kestrel.Core;

Console.Error.WriteLine("CLROS_STATIC_FILES_APP_ENTRY v1");
try
{
    if (!StaticFileApplication.SelectProfile(Environment.GetEnvironmentVariable("CLROS_STATIC_FILES_PROFILE")))
        throw new ArgumentException("Explicit CLROS static-file profile is required.");
    Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
    var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = args, WebRootPath = "/app/wwwroot" });
    builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
    var app = builder.Build();
    StaticFileApplication.Configure(app);
    app.MapGet("/healthz", () => TypedResults.Text("OS_MINIMAL_API_OK", "text/plain"));
    app.MapGet("/config", () => TypedResults.Json(new StaticFileConfigurationResponse("minimal.api.static-files", "v1", "/app/wwwroot"),
        StaticFileJsonContext.Default.StaticFileConfigurationResponse));
    app.Run();
    Console.Error.WriteLine("CLROS_STATIC_FILES_APP_END v1");
}
catch
{
    Console.Error.WriteLine("CLROS_STATIC_FILES_APP_FAIL v1");
    throw;
}
