using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// The prepared service package must seal this exact argument/configuration.
// This separate diagnostic fixture is never selected by the ordinary web leaf.
bool quotaFailure = args.Length == 4 && args[0] == "--clros-service=v3-quota";
bool lifecycle = args.Length == 4 && (args[0] == "--clros-service=v2" || quotaFailure);
string contract = quotaFailure ? "v3-quota" : lifecycle ? "v2" : "v1";
if (args.Length != 4 || (args[0] != "--clros-service=v1" && !lifecycle) || args[1] != "--urls=http://0.0.0.0:8080" || args[2] != "--contentRoot" || string.IsNullOrEmpty(args[3]))
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
    if ((builder.Configuration["CLROS_SERVICE_CONTRACT"] != contract || quotaFailure && builder.Configuration["CLROS_MEMORY_EXHAUSTION"] != "terminate"))
    {
        Console.Error.WriteLine("CLROS_SERVICE_REJECT configuration");
        return 66;
    }

    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
    builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ServiceJsonContext.Default));
    var app = builder.Build();
    Console.Error.WriteLine("CLROS_APP_BUILT minimal.api.service");
    if (lifecycle)
        Console.Error.WriteLine("CLROS_SERVICE_GC_BEGIN " + ServiceLifecycleExercise.MeasureGcBudget());
    if (lifecycle && !quotaFailure)
        Console.Error.WriteLine("CLROS_SERVICE_NATIVE_BEGIN " + ServiceLifecycleExercise.MeasureNativeBudget());
    using var exercise = new ServiceExercise(app.Lifetime.StopApplication);
    var lifecycleExercise = lifecycle ? new ServiceLifecycleExercise() : null;
    var quotaExercise = quotaFailure ? new ServiceQuotaExercise() : null;
    using var stopping = app.Lifetime.ApplicationStopping.Register(exercise.RequestStop);
    using var started = app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.Error.WriteLine("CLROS_SERVICE_LISTEN contract=" + contract + " endpoint=0.0.0.0:8080");
        if (lifecycle)
            Console.Error.WriteLine("CLROS_M10_011_READY");
    });
    app.MapGet("/healthz", () => TypedResults.Text("CLROS_SERVICE_HEALTH_OK", "text/plain"));
    if (lifecycleExercise is not null)
    {
        app.MapGet("/__clros/service/lifecycle/status", () => Results.Text(lifecycleExercise.Read(), "text/plain"));
        app.MapPost("/__clros/service/lifecycle/echo", async (HttpContext context) =>
        {
            if (quotaFailure)
            {
                // This diagnostic holds an incomplete body through bounded pressure.
                var rate = context.Features.Get<Microsoft.AspNetCore.Server.Kestrel.Core.Features.IHttpMinRequestBodyDataRateFeature>() ?? throw new InvalidOperationException("CLROS_QUOTA_REJECT echo_rate_feature");
                rate.MinDataRate = null;
            }

            var result = await lifecycleExercise.ReadEchoAsync(context.Request.Body, context.Request.ContentLength, context.RequestAborted);
            return Results.Text(result.Text, "text/plain", statusCode: result.Accepted ? 200 : 400);
        });
        app.MapGet("/__clros/service/lifecycle/stream", async (HttpContext context) =>
        {
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = ServiceLifecycleExercise.StreamBytes;
            await lifecycleExercise.WriteStreamAsync(context.Response.Body, context.RequestAborted);
        });
        app.MapGet("/__clros/service/lifecycle/cancel", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/plain";
            await context.Response.StartAsync(context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await lifecycleExercise.WaitForCancellationAsync(context.RequestAborted);
        });
    }

    if (quotaExercise is not null)
    {
        app.MapGet("/__clros/service/quota/status", () => Results.Text(quotaExercise.Read(), "text/plain"));
        app.MapPost("/__clros/service/quota", async (HttpContext context) =>
        {
            if (!quotaExercise.TryStart())
                return Results.StatusCode(409);
            await quotaExercise.ExhaustAsync(context.RequestAborted);
            return Results.StatusCode(500);
        });
    }

    app.MapGet("/__clros/service/status", () => TypedResults.Json(exercise.Read(), ServiceJsonContext.Default.ServiceSnapshot));
    app.MapPost("/__clros/service/start", () => exercise.Start() ? Results.Text("CLROS_SERVICE_STARTED", "text/plain") : Results.StatusCode(409));
    app.MapPost("/__clros/service/stop", (HttpContext context) =>
    {
        if (lifecycleExercise is { IsDrained: false } || !exercise.TryComplete())
            return Results.StatusCode(409);
        // Let the stop response complete before Kestrel closes its real listener.
        context.Response.OnCompleted(() =>
        {
            app.Lifetime.StopApplication();
            return Task.CompletedTask;
        });
        return Results.Text("CLROS_SERVICE_STOPPED", "text/plain");
    });
    app.Run();
    started.Dispose();
    stopping.Dispose(); // No callback may touch the disposed worker event.
    exercise.Dispose(); // Join the dedicated worker before allowing process exit.
    var final = exercise.Read();
    if (!exercise.Completed)
    {
        Console.Error.WriteLine("CLROS_SERVICE_FAIL shutdown");
        return 70;
    }

    if (lifecycleExercise is not null)
    {
        if (!lifecycleExercise.IsDrained)
        {
            Console.Error.WriteLine("CLROS_SERVICE_FAIL lifecycle_drain");
            return 70;
        }

        Console.Error.WriteLine("CLROS_SERVICE_LIFECYCLE_END " + lifecycleExercise.Read());
        Console.Error.WriteLine("CLROS_SERVICE_GC_END " + ServiceLifecycleExercise.MeasureGcBudget());
        if (!quotaFailure)
            Console.Error.WriteLine("CLROS_SERVICE_NATIVE_END " + ServiceLifecycleExercise.MeasureNativeBudget());
    }

    Console.Error.WriteLine($"CLROS_SERVICE_APP_END calls={final.Calls} randomRequests={final.RandomRequests} randomBytes={final.RandomBytes} iterations={final.Iterations} elapsedMillis={final.ElapsedMilliseconds} worker=joined");
    GC.KeepAlive(witness);
    return 0;
}
catch
{
    Console.Error.WriteLine("CLROS_APP_FAIL minimal.api.service");
    throw;
}
