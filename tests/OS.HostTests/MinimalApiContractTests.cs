using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling;

internal static class MinimalApiContractTests
{
    private const string Body = "OS_MINIMAL_API_OK";

    [HostTest("minimalapi.contract real Kestrel keeps immutable AOT application policy")]
    private static void ApplicationPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "apps/MinimalApiSmoke/MinimalApiSmoke.csproj"));
        HostCheck.Equal("Microsoft.NET.Sdk.Web", project.Root!.Attribute("Sdk")!.Value);
        foreach ((string key, string value) in new[] { ("TargetFramework", "net11.0"), ("LangVersion", "14.0"),
            ("InvariantGlobalization", "true"), ("ServerGarbageCollection", "false"), ("EventSourceSupport", "false"),
            ("EnableDiagnostics", "false"), ("DebuggerSupport", "false") })
            HostCheck.Equal(value, project.Descendants(key).Single().Value);
        string source = File.ReadAllText(Path.Combine(root, "apps/MinimalApiSmoke/Program.cs"));
        HostCheck.True(source.Contains("WebApplication.CreateSlimBuilder(args)", StringComparison.Ordinal));
        HostCheck.True(source.Contains("ConfigureKestrel", StringComparison.Ordinal));
        HostCheck.True(source.IndexOf("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", StringComparison.Ordinal) <
            source.IndexOf("WebApplication.CreateSlimBuilder", StringComparison.Ordinal));
        HostCheck.True(source.Contains("HttpProtocols.Http1", StringComparison.Ordinal));
        HostCheck.True(source.Contains("app.MapGet(\"/healthz\"", StringComparison.Ordinal));
    }

    [HostTest("minimalapi.contract validates exact default and configured HTTP observations")]
    private static void ObservedHttp()
    {
        NativeAotPayload.ValidateHttpEvidence(Evidence(Body).ToJsonString(), Body);
        const string configured = "configured-\u00e9";
        NativeAotPayload.ValidateHttpEvidence(Evidence(configured).ToJsonString(), configured);
    }

    [HostTest("minimalapi.contract rejects malformed missing and incorrect HTTP observations")]
    private static void RejectedHttp()
    {
        Reject(() => NativeAotPayload.ValidateHttpEvidence("{", Body));
        foreach ((string key, JsonNode? replacement) in new (string, JsonNode?)[] {
            ("endpoint", JsonValue.Create("http://10.0.2.15:8080")), ("endpoint", JsonValue.Create("http://127.0.0.1:8080/x")),
            ("paths", new JsonArray("/healthz")), ("status", JsonValue.Create(500)),
            ("body", JsonValue.Create(Body + "\n")), ("configBody", JsonValue.Create("stale")),
            ("appId", JsonValue.Create("mock.api")), ("bodyBytes", JsonValue.Create(16)) })
        {
            JsonObject row = Evidence(Body);
            row[key] = replacement;
            Reject(() => NativeAotPayload.ValidateHttpEvidence(row.ToJsonString(), Body));
        }
        foreach (string key in Evidence(Body).Select(row => row.Key))
        {
            JsonObject row = Evidence(Body); row.Remove(key);
            Reject(() => NativeAotPayload.ValidateHttpEvidence(row.ToJsonString(), Body));
        }
    }

    [HostTest("minimalapi.contract finite app transcripts reject missing failure duplicate and reversed markers")]
    private static void Markers()
    {
        foreach ((string app, string marker) in new[] { ("NativeAotHello", "nativeaot.hello"),
            ("NativeAotRuntimeSmoke", "nativeaot.runtime") })
        {
            string start = "TEST_START " + marker + "\n", pass = "TEST_PASS " + marker + "\n";
            NativeAotPayload.ValidateRunOutput(app, start + "observed output\n" + pass);
            foreach (string text in new[] { "", pass, start, pass + start, start + pass + pass,
                start + start + pass, start + "TEST_FAIL unrelated reason\n" + pass })
                Reject(() => NativeAotPayload.ValidateRunOutput(app, text));
        }
        Reject(() => NativeAotPayload.ValidateRunOutput("FakeApp", "TEST_PASS nativeaot.hello\n"));
    }

    private static JsonObject Evidence(string body) => new()
    {
        ["endpoint"] = "http://127.0.0.1:8080", ["paths"] = new JsonArray("/", "/healthz", "/config"),
        ["status"] = 200, ["body"] = body, ["appId"] = "minimal.api.smoke", ["configBody"] = body,
        ["bodyBytes"] = System.Text.Encoding.UTF8.GetByteCount(body)
    };

    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
        { rejected = true; }
        HostCheck.True(rejected);
    }
}
