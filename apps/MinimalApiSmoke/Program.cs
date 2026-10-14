using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;

const string AppId = "minimal.api.smoke";
const string ContractId = "kestrel.smoke.package.v0";
const string DefaultExpectedBody = "OS_MINIMAL_API_OK";
const string ExpectedBodyKey = "OS_SMOKE_EXPECTED_BODY";
const string DefaultListenEndpoint = "http://0.0.0.0:8080";

// The qualification profile uses immutable configuration and HTTP/1.1 only.
// Set this before default configuration providers are created, so no watcher starts.
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, KestrelSmokeJsonContext.Default);
});

var app = builder.Build();

app.MapGet("/", (IConfiguration configuration) =>
    TypedResults.Text(GetExpectedBody(configuration), "text/plain"))
    .WithName("KestrelSmokeRoot");

app.MapGet("/healthz", (IConfiguration configuration) =>
    TypedResults.Text(GetExpectedBody(configuration), "text/plain"))
    .WithName("KestrelSmokeHealth");

app.MapGet("/config", (IConfiguration configuration) =>
    TypedResults.Json(CreateConfigResponse(configuration), KestrelSmokeJsonContext.Default.KestrelSmokeConfigResponse))
    .WithName("KestrelSmokeConfig");

app.Run();

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
