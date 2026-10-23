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
    bool staticFiles = StaticFileApplication.SelectProfile(Environment.GetEnvironmentVariable("CLROS_STATIC_FILES_PROFILE"));
    var builder = staticFiles
        ? WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = args, WebRootPath = "/app/wwwroot" })
        : WebApplication.CreateSlimBuilder(args);
    builder.WebHost.ConfigureKestrel(options =>
        options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, KestrelSmokeJsonContext.Default);
    });

    var app = builder.Build();
    Console.Error.WriteLine("CLROS_APP_BUILT minimal.api.smoke");

    if (staticFiles) StaticFileApplication.Configure(app);
    else app.MapGet("/", (IConfiguration configuration) =>
            TypedResults.Text(GetExpectedBody(configuration), "text/plain"))
            .WithName("KestrelSmokeRoot");

    app.MapGet("/healthz", (IConfiguration configuration) =>
        TypedResults.Text(GetExpectedBody(configuration), "text/plain"))
        .WithName("KestrelSmokeHealth");

    app.MapGet("/config", (IConfiguration configuration) =>
        TypedResults.Json(CreateConfigResponse(configuration), KestrelSmokeJsonContext.Default.KestrelSmokeConfigResponse))
        .WithName("KestrelSmokeConfig");

    // This explicit diagnostic is supported only by the pinned Linux application
    // profile. Normal configuration and HTTP endpoints retain their portable path.
    app.MapPost("/__clros/logexercise", LinuxLoggingExercise.Run);
    app.MapGet("/__clros/vectors", KestrelVectorExercise.WriteAsync);

    // Churn starts only on this explicit versioned empty request. Kestrel owns
    // cancellation; the helper awaits its work and releases its active slot.
    var churn = new KestrelChurnExercise();
    app.MapPost("/__clros/churn", async (HttpContext context) =>
    {
        if (context.Request.ContentLength != 0 || context.Request.QueryString.HasValue ||
            context.Request.Headers["X-CLROS-Churn"].Count != 1 ||
            context.Request.Headers["X-CLROS-Churn"][0] is not ("v1" or "v2") ||
            context.Request.Headers.ContainsKey("Transfer-Encoding"))
            return TypedResults.Text("CLROS_CHURN_BAD_REQUEST", "text/plain", statusCode: 400);
        string? result = context.Request.Headers["X-CLROS-Churn"][0] == "v2" ?
            await churn.RunUntilCanceledAsync(context.RequestAborted) : await churn.RunAsync(context.RequestAborted);
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
        string? result = eventOwned ? await signalProbe.RunEventOwnedRaceAsync(context.RequestAborted) :
            await signalProbe.RunRaceAsync(context.RequestAborted);
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
        return TypedResults.Text(accepted ? "CLROS_SIGNAL_RACE_START_REQUESTED" : "CLROS_SIGNAL_RACE_START_REFUSED",
            "text/plain", statusCode: accepted ? 200 : 409);
    });
    app.MapPost("/__clros/signalrace/recover", (HttpContext context) =>
    {
        bool eventOwned = IsSignalRequest(context, "v3");
        if (!eventOwned && !IsSignalRequest(context))
            return TypedResults.Text("CLROS_SIGNAL_RACE_BAD_REQUEST", "text/plain", statusCode: 400);
        if (!KestrelSignalProbeAbi.Supported)
            return TypedResults.Text("CLROS_SIGNAL_RACE_UNSUPPORTED", "text/plain", statusCode: 501);
        bool accepted = signalProbe.RequestRecovery(eventOwned);
        return TypedResults.Text(accepted ? "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED" : "CLROS_SIGNAL_RACE_RECOVERY_REFUSED",
            "text/plain", statusCode: accepted ? 200 : 409);
    });

    app.Run();
    if (staticFiles) Console.Error.WriteLine("CLROS_STATIC_FILES_APP_END v1");
    GC.KeepAlive(startupWitness);
}
catch
{
    Console.Error.WriteLine("CLROS_APP_FAIL minimal.api.smoke");
    throw;
}

static bool IsSignalVersion(HttpContext context, string version) => context.Request.Headers["X-CLROS-Signal"].Count == 1 &&
    context.Request.Headers["X-CLROS-Signal"][0] == version;
static bool IsSignalRequest(HttpContext context, string version = "v1") => context.Request.ContentLength == 0 &&
    !context.Request.QueryString.HasValue && IsSignalVersion(context, version) && !context.Request.Headers.ContainsKey("Transfer-Encoding");

static string GetExpectedBody(IConfiguration configuration)
{
    string? configured = configuration[ExpectedBodyKey];
    return string.IsNullOrEmpty(configured) ? DefaultExpectedBody : configured;
}

static KestrelSmokeConfigResponse CreateConfigResponse(IConfiguration configuration)
{
    string expectedBody = GetExpectedBody(configuration);
    string? listenEndpoint = configuration["ASPNETCORE_URLS"];

    return new KestrelSmokeConfigResponse(
        AppId,
        ContractId,
        ExpectedBodyKey,
        expectedBody,
        Encoding.UTF8.GetByteCount(expectedBody),
        string.IsNullOrWhiteSpace(listenEndpoint) ? DefaultListenEndpoint : listenEndpoint,
        "/healthz");
}

internal sealed record KestrelSmokeConfigResponse(
    string AppId,
    string ContractId,
    string ExpectedBodyKey,
    string ExpectedBody,
    int ExpectedBodyBytes,
    string ListenEndpoint,
    string HealthPath);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(KestrelSmokeConfigResponse))]
internal sealed partial class KestrelSmokeJsonContext : JsonSerializerContext;

// Explicit constructors give qualification an observation of real managed
// initialization. The caller retains this object until application shutdown.
internal sealed class AppStartupWitness
{
    static AppStartupWitness() => Console.Error.WriteLine("CLROS_APP_CCTOR minimal.api.smoke");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AppStartupWitness() => Console.Error.WriteLine("CLROS_APP_CTOR minimal.api.smoke");
}

// A one-shot bounded diagnostic, never run by ordinary HTTP requests. Eight
// owned threads run in two groups of four so runtime threads retain capacity;
// each group rendezvous before writing and joins before the next group starts.
// The Linux-only native call observes the real writev result and errno rather
// than translating a managed stream error into an assumed descriptor result.
internal static class LinuxLoggingExercise
{
    private static int started;

    internal static IResult Run()
    {
        if (!OperatingSystem.IsLinux()) return Results.StatusCode(501);
        if (Interlocked.Exchange(ref started, 1) != 0) return Results.StatusCode(409);
        int failed = 0;
        const int groupSize = 4;
        for (int first = 0; first < 8; first += groupSize)
        {
            using var ready = new CountdownEvent(groupSize);
            using var release = new ManualResetEventSlim(false);
            var threads = new Thread[groupSize];
            int startedThreads = 0;
            bool allReady;
            try
            {
                for (int worker = 0; worker < threads.Length; worker++)
                {
                    int identity = first + worker;
                    threads[worker] = new Thread(() =>
                    {
                        try
                        {
                            ready.Signal();
                            if (!release.Wait(TimeSpan.FromSeconds(30))) { Interlocked.Exchange(ref failed, 1); return; }
                            for (int record = 0; record < 4; record++)
                            {
                                Console.Out.WriteLine($"CLROS_LOG fd=1 worker={identity} record={record}");
                                if (!WriteRecord(identity, record)) Interlocked.Exchange(ref failed, 1);
                            }
                        }
                        catch { Interlocked.Exchange(ref failed, 1); }
                    }, 256 * 1024) { IsBackground = true };
                    threads[worker].Start();
                    startedThreads++;
                }
                allReady = ready.Wait(TimeSpan.FromSeconds(30));
            }
            finally
            {
                release.Set();
                for (int worker = 0; worker < startedThreads; worker++)
                    // A stuck writer must not outlive this diagnostic or its events.
                    // Terminate the owned fixture process rather than return HTTP.
                    if (!threads[worker].Join(TimeSpan.FromSeconds(30))) Environment.Exit(70);
            }
            if (!allReady || failed != 0) return Results.StatusCode(500);
        }
        if (!InvalidDescriptor()) return Results.StatusCode(500);
        return Results.Text("CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9", "text/plain");
    }

    private static unsafe bool WriteRecord(int worker, int record)
    {
        byte[] bytes = Encoding.ASCII.GetBytes($"CLROS_LOG fd=2 worker={worker} record={record}");
        byte newline = (byte)'\n';
        fixed (byte* body = bytes)
        {
            Vector* vectors = stackalloc Vector[2];
            vectors[0] = new Vector { Address = body, Length = (nuint)bytes.Length };
            vectors[1] = new Vector { Address = &newline, Length = 1 };
            return WriteVector(2, vectors, 2) == bytes.Length + 1;
        }
    }

    private static unsafe bool InvalidDescriptor()
    {
        byte value = (byte)'!';
        var vector = new Vector { Address = &value, Length = 1 };
        return WriteVector(1234, &vector, 1) == -1 && Marshal.GetLastPInvokeError() == 9;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Vector { internal byte* Address; internal nuint Length; }

    [DllImport("libc", EntryPoint = "writev", SetLastError = true)]
    private static extern unsafe nint WriteVector(int descriptor, Vector* vectors, int count);
}
