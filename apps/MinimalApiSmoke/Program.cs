using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

const string AppId = "minimal.api.smoke";
const string ContractId = "kestrel.smoke.package.v0";
const string DefaultExpectedBody = "OS_MINIMAL_API_OK";
const string ExpectedBodyKey = "OS_SMOKE_EXPECTED_BODY";
const string DefaultListenEndpoint = "http://0.0.0.0:8080";
Console.Error.WriteLine("CLROS_APP_ENTRY minimal.api.smoke");
try
{
    var startupWitness = new AppStartupWitness();
    // The qualification profile uses immutable configuration and HTTP/1.1 only.
    // Set this before default configuration providers are created, so no watcher starts.
    Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
    var builder = WebApplication.CreateSlimBuilder(args);
    builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, KestrelSmokeJsonContext.Default);
    });
    var app = builder.Build();
    Console.Error.WriteLine("CLROS_APP_BUILT minimal.api.smoke");
    app.MapGet("/", (IConfiguration configuration) => TypedResults.Text(GetExpectedBody(configuration), "text/plain")).WithName("KestrelSmokeRoot");
    app.MapGet("/healthz", (IConfiguration configuration) => TypedResults.Text(GetExpectedBody(configuration), "text/plain")).WithName("KestrelSmokeHealth");
    app.MapGet("/config", (IConfiguration configuration) => TypedResults.Json(CreateConfigResponse(configuration), KestrelSmokeJsonContext.Default.KestrelSmokeConfigResponse)).WithName("KestrelSmokeConfig");
    // This explicit diagnostic is supported only by the pinned Linux application
    // profile. Normal configuration and HTTP endpoints retain their portable path.
    app.MapPost("/__clros/logexercise", LinuxLoggingExercise.Run);
    app.MapGet("/__clros/vectors", KestrelVectorExercise.WriteAsync);
    // Churn starts only on this explicit versioned empty request. Kestrel owns
    // cancellation; the helper awaits its work and releases its active slot.
    var churn = new KestrelChurnExercise();
    app.MapPost("/__clros/churn", async (HttpContext context) =>
    {
        if (context.Request.ContentLength != 0 || context.Request.QueryString.HasValue || context.Request.Headers["X-CLROS-Churn"].Count != 1 || context.Request.Headers["X-CLROS-Churn"][0] is not ("v1" or "v2") || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            return TypedResults.Text("CLROS_CHURN_BAD_REQUEST", "text/plain", statusCode: 400);
        string? result = context.Request.Headers["X-CLROS-Churn"][0] == "v2" ? await churn.RunUntilCanceledAsync(context.RequestAborted) : await churn.RunAsync(context.RequestAborted);
        return TypedResults.Text(result ?? "CLROS_CHURN_BUSY", "text/plain", statusCode: result is null ? 429 : 200);
    });
    app.MapGet("/__clros/churn/status", () => TypedResults.Text(churn.Read(), "text/plain"));
    var signalProbe = new KestrelSignalProbe();
    app.MapPost("/__clros/signalprobe", async (HttpContext context) =>
    {
        if (!IsSignalRequest(context))
            return TypedResults.Text("CLROS_SIGNAL_PROBE_BAD_REQUEST", "text/plain", statusCode: 400);
        if (!KestrelSignalProbeAbi.Supported)
            return TypedResults.Text("CLROS_SIGNAL_PROBE_UNSUPPORTED", "text/plain", statusCode: 501);
        string? result = await signalProbe.RunAsync(context.RequestAborted);
        return TypedResults.Text(result ?? "CLROS_SIGNAL_PROBE_BUSY", "text/plain", statusCode: result is null ? 409 : 200);
    });
    app.MapPost("/__clros/signalrace", async (HttpContext context) =>
    {
        bool eventOwned = IsSignalRequest(context, "v3");
        if (!eventOwned && !IsSignalRequest(context))
            return TypedResults.Text("CLROS_SIGNAL_RACE_BAD_REQUEST", "text/plain", statusCode: 400);
        if (!KestrelSignalProbeAbi.Supported)
            return TypedResults.Text("CLROS_SIGNAL_RACE_UNSUPPORTED", "text/plain", statusCode: 501);
        string? result = eventOwned ? await signalProbe.RunEventOwnedRaceAsync(context.RequestAborted) : await signalProbe.RunRaceAsync(context.RequestAborted);
        return TypedResults.Text(result ?? "CLROS_SIGNAL_RACE_BUSY", "text/plain", statusCode: result is null ? 409 : 200);
    });
    app.MapGet("/__clros/signalrace/status", (HttpContext context) =>
    {
        bool selected = signalProbe.TryReadRace(IsSignalVersion(context, "v3"), out string value);
        return TypedResults.Text(selected ? value : "CLROS_SIGNAL_RACE_PROTOCOL_REFUSED", "text/plain", statusCode: selected ? 200 : 409);
    });
    app.MapPost("/__clros/signalrace/start", (HttpContext context) =>
    {
        bool eventOwned = IsSignalRequest(context, "v3");
        if (!eventOwned && !IsSignalRequest(context))
            return TypedResults.Text("CLROS_SIGNAL_RACE_BAD_REQUEST", "text/plain", statusCode: 400);
        if (!KestrelSignalProbeAbi.Supported)
            return TypedResults.Text("CLROS_SIGNAL_RACE_UNSUPPORTED", "text/plain", statusCode: 501);
        bool accepted = signalProbe.RequestStart(eventOwned);
        return TypedResults.Text(accepted ? "CLROS_SIGNAL_RACE_START_REQUESTED" : "CLROS_SIGNAL_RACE_START_REFUSED", "text/plain", statusCode: accepted ? 200 : 409);
    });
    app.MapPost("/__clros/signalrace/recover", (HttpContext context) =>
    {
        bool eventOwned = IsSignalRequest(context, "v3");
        if (!eventOwned && !IsSignalRequest(context))
            return TypedResults.Text("CLROS_SIGNAL_RACE_BAD_REQUEST", "text/plain", statusCode: 400);
        if (!KestrelSignalProbeAbi.Supported)
            return TypedResults.Text("CLROS_SIGNAL_RACE_UNSUPPORTED", "text/plain", statusCode: 501);
        bool accepted = signalProbe.RequestRecovery(eventOwned);
        return TypedResults.Text(accepted ? "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED" : "CLROS_SIGNAL_RACE_RECOVERY_REFUSED", "text/plain", statusCode: accepted ? 200 : 409);
    });
    app.Run();
    GC.KeepAlive(startupWitness);
}
catch
{
    Console.Error.WriteLine("CLROS_APP_FAIL minimal.api.smoke");
    throw;
}

static bool IsSignalVersion(HttpContext context, string version) => context.Request.Headers["X-CLROS-Signal"].Count == 1 && context.Request.Headers["X-CLROS-Signal"][0] == version;
static bool IsSignalRequest(HttpContext context, string version = "v1") => context.Request.ContentLength == 0 && !context.Request.QueryString.HasValue && IsSignalVersion(context, version) && !context.Request.Headers.ContainsKey("Transfer-Encoding");
static string GetExpectedBody(IConfiguration configuration)
{
    string? configured = configuration[ExpectedBodyKey];
    return string.IsNullOrEmpty(configured) ? DefaultExpectedBody : configured;
}

static KestrelSmokeConfigResponse CreateConfigResponse(IConfiguration configuration)
{
    string expectedBody = GetExpectedBody(configuration);
    string? listenEndpoint = configuration["ASPNETCORE_URLS"];
    return new KestrelSmokeConfigResponse(AppId, ContractId, ExpectedBodyKey, expectedBody, Encoding.UTF8.GetByteCount(expectedBody), string.IsNullOrWhiteSpace(listenEndpoint) ? DefaultListenEndpoint : listenEndpoint, "/healthz");
}
