using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// The prepared service package must seal this exact argument/configuration.
// This separate diagnostic fixture is never selected by the ordinary web leaf.
if (args.Length != 4 || args[0] != "--clros-service=v1" || args[1] != "--urls=http://0.0.0.0:8080" ||
    args[2] != "--contentRoot" || string.IsNullOrEmpty(args[3]))
{
    Console.Error.WriteLine("CLROS_SERVICE_REJECT arguments");
    return 64;
}
if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("CLROS_SERVICE_REJECT platform");
    return 65;
}

Console.Error.WriteLine("CLROS_APP_ENTRY minimal.api.service");
try
{
    var witness = new ServiceStartupWitness();
    Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
    var builder = WebApplication.CreateSlimBuilder(args);
    if (builder.Configuration["CLROS_SERVICE_CONTRACT"] != "v1")
    {
        Console.Error.WriteLine("CLROS_SERVICE_REJECT configuration");
        return 66;
    }
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
    builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ServiceJsonContext.Default));
    var app = builder.Build();
    Console.Error.WriteLine("CLROS_APP_BUILT minimal.api.service");
    using var exercise = new ServiceExercise(app.Lifetime.StopApplication);
    using var stopping = app.Lifetime.ApplicationStopping.Register(exercise.RequestStop);
    using var started = app.Lifetime.ApplicationStarted.Register(() => Console.Error.WriteLine("CLROS_SERVICE_LISTEN contract=v1 endpoint=0.0.0.0:8080"));

    app.MapGet("/healthz", () => TypedResults.Text("CLROS_SERVICE_HEALTH_OK", "text/plain"));
    app.MapGet("/__clros/service/status", () => TypedResults.Json(exercise.Read(), ServiceJsonContext.Default.ServiceSnapshot));
    app.MapPost("/__clros/service/start", () => exercise.Start()
        ? Results.Text("CLROS_SERVICE_STARTED", "text/plain") : Results.StatusCode(409));
    app.MapPost("/__clros/service/stop", (HttpContext context) =>
    {
        if (!exercise.TryComplete()) return Results.StatusCode(409);
        // Let the stop response complete before Kestrel closes its real listener.
        context.Response.OnCompleted(() => { app.Lifetime.StopApplication(); return Task.CompletedTask; });
        return Results.Text("CLROS_SERVICE_STOPPED", "text/plain");
    });

    app.Run();
    started.Dispose(); stopping.Dispose(); // No callback may touch the disposed worker event.
    exercise.Dispose(); // Join the dedicated worker before allowing process exit.
    var final = exercise.Read();
    if (!exercise.Completed) { Console.Error.WriteLine("CLROS_SERVICE_FAIL shutdown"); return 70; }
    Console.Error.WriteLine($"CLROS_SERVICE_APP_END calls={final.Calls} randomRequests={final.RandomRequests} randomBytes={final.RandomBytes} iterations={final.Iterations} elapsedMillis={final.ElapsedMilliseconds} worker=joined");
    GC.KeepAlive(witness);
    return 0;
}
catch
{
    Console.Error.WriteLine("CLROS_APP_FAIL minimal.api.service");
    throw;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceSnapshot))]
internal sealed partial class ServiceJsonContext : JsonSerializerContext;

internal sealed class ServiceStartupWitness
{
    static ServiceStartupWitness() => Console.Error.WriteLine("CLROS_APP_CCTOR minimal.api.service");
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ServiceStartupWitness() => Console.Error.WriteLine("CLROS_APP_CTOR minimal.api.service");
}
