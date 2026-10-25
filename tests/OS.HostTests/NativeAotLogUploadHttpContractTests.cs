using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadHttpContract;

// Managed loopback/process/FileStream evidence only. No Linux NativeAOT,
// device FLUSH, CLROS, reboot or physical-persistence authority is inferred.
internal static class NativeAotLogUploadHttpContractTests
{
    private const string Transcript = "TEST_START m10.027.http\n" +
        "LOG_UPLOAD_HTTP_POLICY serve=once bodyBytes=5126 logRows=6+6 chunkBytes=4094+1032 flushRequested=true\n" +
        "LOG_UPLOAD_HTTP_READY startupLogBytes=330 uploadBytes=0\n" +
        "LOG_UPLOAD_HTTP_ACCEPTED logBytes=660 uploadBytes=5126\nTEST_PASS m10.027.http\n";

    [HostTest("HTTP log upload copied vectors preserve the actual console candidate bytes and carry no execution authority")]
    private static void Vectors()
    {
        byte[] log = NativeAotLogUploadContract.ExpectedLog(), upload = NativeAotLogUploadContract.ExpectedUpload();
        HostCheck.True(log.Length == 660 && upload.Length == 5126 && F.StartupLogBytes == 330);
        HostCheck.Equal("C6A15763F0A6422AE19B2B3BA77FCEC15BF177D87EA471988415D5C24FC2673F", Convert.ToHexString(SHA256.HashData(log)));
        HostCheck.Equal("E9B33668A772A5C2B5B20CBAEC9AC688B3EE7BBEC76DD17857DBF2BFB7428EBE", Convert.ToHexString(SHA256.HashData(upload)));
        foreach (var row in Responses())
        {
            var fact = F.InspectResponseValues(row.Kind, row.Method, row.Target, row.Status, row.Type, row.Body);
            string retained = fact.Sha256; if (row.Body.Length != 0) row.Body[0] ^= 1;
            HostCheck.True(fact.Sha256 == retained && !fact.GrantsOriginalProcessOrStorageAuthority);
        }
        F.InspectServerTranscriptValues(Transcript, ""); F.InspectServerTranscriptValues(Transcript.Replace("\n", "\r\n", StringComparison.Ordinal), "");
    }

    [HostTest("HTTP log upload response values reject wrong route method status type payload and unbounded bodies")]
    private static void ResponseMutations()
    {
        foreach (var row in Responses())
        {
            Reject(() => F.InspectResponseValues(row.Kind, row.Method == "GET" ? "HEAD" : "PUT", row.Target, row.Status, row.Type, row.Body), "response");
            Reject(() => F.InspectResponseValues(row.Kind, row.Method, row.Target + "?path=other", row.Status, row.Type, row.Body), "response");
            Reject(() => F.InspectResponseValues(row.Kind, row.Method, row.Target, row.Status + 1, row.Type, row.Body), "response");
            Reject(() => F.InspectResponseValues(row.Kind, row.Method, row.Target, row.Status, "application/json", row.Body), "response");
            byte[] mutated = row.Body.Length == 0 ? [0x01] : (byte[])row.Body.Clone(); if (row.Body.Length != 0) mutated[^1] ^= 1;
            Reject(() => F.InspectResponseValues(row.Kind, row.Method, row.Target, row.Status, row.Type, mutated), "response");
        }
        Reject(() => F.InspectResponseValues(F.ResponseKind.Upload, "GET", "/upload", 200, "application/octet-stream", new byte[5127]), "response_capacity");
        Reject(() => F.InspectResponseValues(F.ResponseKind.Health, "GET", "/healthz", 200, "text/plain\nforged: yes", []), "response_capacity");
        Reject(() => F.InspectResponseValues((F.ResponseKind)99, "GET", "/healthz", 200, "", []), "response_kind");
    }

    [HostTest("HTTP log upload transcript rejects missing startup readback partial output reordered effects and fabricated success")]
    private static void TranscriptMutations()
    {
        foreach (string mutation in new[]
        {
            Transcript[..^1], Transcript + "extra\n", Transcript.Replace(F.Ready, "", StringComparison.Ordinal),
            Transcript.Replace("startupLogBytes=330", "startupLogBytes=0", StringComparison.Ordinal),
            Transcript.Replace("flushRequested=true", "flushObserved=true", StringComparison.Ordinal),
            Transcript.Replace("LOG_UPLOAD_HTTP_ACCEPTED logBytes=660 uploadBytes=5126\n", "", StringComparison.Ordinal),
            Transcript.Replace(F.Ready, F.Ready + F.Ready, StringComparison.Ordinal),
            "TEST_PASS m10.027.http\n", Transcript.Replace("\nTEST_PASS", "\nTEST_FAIL x\nTEST_PASS", StringComparison.Ordinal),
            new string('x', F.MaximumTranscriptBytes + 1), new string('\u00e9', F.MaximumTranscriptBytes / 2 + 1)
        }) Reject(() => F.InspectServerTranscriptValues(mutation, ""), "transcript");
        Reject(() => F.InspectServerTranscriptValues(Transcript, "warning"), "transcript");
    }

    [HostTest("HTTP log upload requires a fresh confined artifact owner before any launch or replacement")]
    private static void FreshOutput()
    {
        string root = HostToolProcess.FindRepositoryRoot(), parent = Path.Combine(root, "artifacts", "log-upload-http-fresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent); string fresh = Path.Combine(parent, "fresh"); F.RequireFreshOutput(root, fresh); HostCheck.True(!Path.Exists(fresh));
        Directory.CreateDirectory(fresh); File.WriteAllText(Path.Combine(fresh, "owner.txt"), "original");
        Reject(() => F.RequireFreshOutput(root, fresh), "fresh_output_required");
        Reject(() => F.RequireFreshOutput(root, Path.Combine(root, "not-artifacts-" + Guid.NewGuid().ToString("N"))), "fresh_output_required");
        Reject(() => F.RequireFreshOutput(root, Path.Combine(root, "artifacts", "..", "outside")), "fresh_output_required");
        HostCheck.Equal("original", File.ReadAllText(Path.Combine(fresh, "owner.txt")));
    }

    [HostTest("HTTP log upload prelaunch cancellation retains a valid incomplete receipt without launching a child")]
    private static async Task CancelBeforeLaunch()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = HostToolProcess.FindRepositoryRoot(), parent = Path.Combine(root, "artifacts", "log-upload-http-cancel-tests"); Directory.CreateDirectory(parent);
        string output = Path.Combine(parent, Guid.NewGuid().ToString("N")); bool cancelled = false;
        try { await F.RunManagedReferenceAsync(root, output, new CancellationToken(canceled: true)); }
        catch (OperationCanceledException) { cancelled = true; }
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "receipt.json")));
        using var commands = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "commands.json")));
        HostCheck.True(cancelled && !receipt.RootElement.GetProperty("Complete").GetBoolean() &&
            !receipt.RootElement.GetProperty("DeviceFlushObserved").GetBoolean() && commands.RootElement.GetArrayLength() == 0);
    }

    [HostTest("HTTP Linux publication plan preserves the original static musl recipe and rejects compiler recipe changes")]
    private static void LinuxPublicationPlan()
    {
        string root = HostToolProcess.FindRepositoryRoot(), destination = Path.Combine(root, "artifacts", "http-value-only-publish");
        string[] original = ["publish", "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj", "-c", "Release", "-f", "net11.0", "-r", "linux-musl-x64",
            "--self-contained", "true", "-o", "original-output", "--nologo", "-p:PublishAot=true", "-p:StaticExecutable=true", "-p:PositionIndependentExecutable=false",
            "-p:InvariantGlobalization=true", "-p:ServerGarbageCollection=false", "-p:EventSourceSupport=false", "-p:EnableDiagnostics=false", "-p:DebuggerSupport=false",
            "-p:RuntimeFrameworkVersion=11.0.0-rc.2.26504.105", "-p:TargetLatestRuntimePatch=false", "-p:UseSharedCompilation=false", "-nr:false"];
        string Recipe(params string[][] rows) => JsonSerializer.Serialize(rows.Select(arguments => new { executable = "dotnet", arguments }));
        string[] planned = F.PublishArguments(Recipe(original), destination), expected = (string[])original.Clone();
        expected[1] = "apps/MinimalApiLogUploadContract/MinimalApiLogUploadContract.csproj"; expected[11] = Path.GetFullPath(destination);
        HostCheck.True(planned.SequenceEqual(expected) && original[1].Contains("NativeAotSocketSmoke", StringComparison.Ordinal));
        planned[1] = "mutated"; HostCheck.True(F.PublishArguments(Recipe(original), destination).SequenceEqual(expected) && !Path.Exists(destination));
        foreach (string flag in new[] { "-p:StaticExecutable=true", "-p:PositionIndependentExecutable=false", "-r", "-p:RuntimeFrameworkVersion=11.0.0-rc.2.26504.105" })
        {
            string[] changed = (string[])original.Clone(); int index = Array.IndexOf(changed, flag); changed[index] += "-changed";
            RejectAny(() => F.PublishArguments(Recipe(changed), destination));
        }
        RejectAny(() => F.PublishArguments(Recipe(original, original), destination)); RejectAny(() => F.PublishArguments("[]", destination));
    }

    [HostTest("HTTP Linux source admission pins only the two unchanged app files and rejects extra modified or cached publication inputs")]
    private static void LinuxSourcePins()
    {
        string root = HostToolProcess.FindRepositoryRoot(), parent = Path.Combine(root, "artifacts", "http-source-pins-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(parent, "apps", F.Application); Directory.CreateDirectory(app);
        foreach (string name in new[] { "Program.cs", F.Application + ".csproj" }) File.Copy(Path.Combine(root, "apps", F.Application, name), Path.Combine(app, name));
        F.RequireCurrentSources(parent, fresh: true); F.RequireCurrentSources(parent);
        string program = Path.Combine(app, "Program.cs"), saved = File.ReadAllText(program);
        File.AppendAllText(program, "\n// unreviewed source\n"); RejectAny(() => F.RequireCurrentSources(parent)); File.WriteAllText(program, saved);
        // Restore exact bytes rather than applying a text/newline projection.
        File.Copy(Path.Combine(root, "apps", F.Application, "Program.cs"), program, overwrite: true);
        string extra = Path.Combine(app, "Other.cs"); File.WriteAllText(extra, "// unreviewed\n"); RejectAny(() => F.RequireCurrentSources(parent)); File.Delete(extra);
        string config = Path.Combine(parent, "NuGet.Config"); File.WriteAllText(config, "<configuration/>"); RejectAny(() => F.RequireCurrentSources(parent)); File.Delete(config);
        Directory.CreateDirectory(Path.Combine(app, "obj")); RejectAny(() => F.RequireCurrentSources(parent, fresh: true)); F.RequireCurrentSources(parent);
    }

    [HostTest("HTTP Linux trace argv is literal bounded serve-once and cannot select a remote URL shell or old write phase")]
    private static void LinuxArguments()
    {
        string[] actual = F.LinuxTraceArguments("/root/http/app", "/root/http/data", "/root/http/write-trace", 18881);
        string[] expected = ["-ff", "-yy", "-ttt", "-s", "256", "-o", "/root/http/write-trace", "/root/http/app", "--serve-once", "--root", "/root/http/data", "--urls", "http://127.0.0.1:18881"];
        HostCheck.True(actual.SequenceEqual(expected) && !actual.Contains("--write", StringComparer.Ordinal)); actual[8] = "--write";
        HostCheck.True(F.LinuxTraceArguments("/root/http/app", "/root/http/data", "/root/http/write-trace", 18881).SequenceEqual(expected));
        foreach (int port in new[] { -1, 0, 65536 }) Reject(() => F.LinuxTraceArguments("/a", "/b", "/c", port), "linux_port");
        foreach (string path in new[] { "relative", "/", "/a/../b", "/a/./b", "/a//b", "/a/", "/a\\b", "/a\nb", "/a\"b", "/a\u00e9" })
            Reject(() => F.LinuxTraceArguments(path, "/b", "/c", 1), "linux_path");
        RejectAny(() => F.LinuxTraceArguments("/a", "/a", "/c", 1));
        HostCheck.True(NativeAotLogUploadContract.LinuxTraceArguments("/root/console/app", "/root/http/data", "/root/http/verify-trace", "verify")
            .SequenceEqual(new[] { "-ff", "-yy", "-ttt", "-s", "256", "-o", "/root/http/verify-trace", "/root/console/app", "--verify", "--root", "/root/http/data" }));
    }

    [HostTest("HTTP Linux copied publication values reject foreign failed duplicate redirected and mismatched identities without granting authority")]
    private static void LinuxPublicationValues()
    {
        var sources = new Dictionary<string, string> { ["apps/MinimalApiLogUploadContract/Program.cs"] = new('A', 64), ["global.json"] = new('B', 64) };
        JsonObject Receipt() => new()
        {
            ["schema"] = "clros.nativeaot.socket-log-upload-http.publication.v1", ["sdk"] = NativeAotPayload.SdkVersion, ["runtime"] = NativeAotPayload.RuntimeVersion,
            ["application"] = F.Application, ["succeeded"] = true, ["failure"] = null, ["appExecuted"] = false, ["guestExecuted"] = false,
            ["sources"] = JsonSerializer.SerializeToNode(sources), ["native"] = new JsonObject { ["/usr/bin/strace"] = new string('D', 64) },
            ["outputs"] = new JsonObject { [F.Application + "/" + F.Application] = new string('C', 64) }, ["image"] = new JsonObject { ["Sha256"] = new string('C', 64) }
        };
        var valid = Receipt(); var value = F.InspectLinuxPublicationValues(valid.ToJsonString(), sources);
        HostCheck.True(!value.GrantsOriginalPublicationOrExecutionAuthority && value.Sources == 2 && value.Outputs == 1 && value.NativeInputs == 1);
        foreach ((string field, JsonNode? changed) in new (string, JsonNode?)[]
        {
            ("schema", JsonValue.Create("other")), ("application", JsonValue.Create("MinimalApiStaticFiles")), ("sdk", JsonValue.Create("11.0.100")),
            ("runtime", JsonValue.Create("11.0.0")), ("succeeded", JsonValue.Create(false)), ("failure", JsonValue.Create("failed")),
            ("appExecuted", JsonValue.Create(true)), ("guestExecuted", JsonValue.Create(true))
        }) { var receipt = Receipt(); receipt[field] = changed; Reject(() => F.InspectLinuxPublicationValues(receipt.ToJsonString(), sources), "publication_policy"); }
        var mutation = Receipt(); mutation["sources"]!["global.json"] = new string('E', 64); Reject(() => F.InspectLinuxPublicationValues(mutation.ToJsonString(), sources), "publication_sources");
        mutation = Receipt(); mutation["outputs"] = new JsonObject { ["../escape"] = new string('C', 64) }; Reject(() => F.InspectLinuxPublicationValues(mutation.ToJsonString(), sources), "publication_outputs");
        mutation = Receipt(); mutation["native"] = new JsonObject { ["relative-tool"] = new string('D', 64) }; Reject(() => F.InspectLinuxPublicationValues(mutation.ToJsonString(), sources), "publication_native");
        mutation = Receipt(); mutation["image"]!["Sha256"] = new string('F', 64); Reject(() => F.InspectLinuxPublicationValues(mutation.ToJsonString(), sources), "publication_image");
        mutation = Receipt(); var tooMany = new JsonObject(); for (int i = 0; i <= 512; i++) tooMany["file" + i] = new string('C', 64); mutation["outputs"] = tooMany;
        Reject(() => F.InspectLinuxPublicationValues(mutation.ToJsonString(), sources), "publication_outputs");
        string duplicate = valid.ToJsonString().Insert(1, "\"sdk\":\"forged\","); Reject(() => F.InspectLinuxPublicationValues(duplicate, sources), "publication_policy");
        string duplicateSource = valid.ToJsonString().Replace("\"global.json\":", "\"global.json\":\"" + new string('B', 64) + "\",\"global.json\":", StringComparison.Ordinal);
        Reject(() => F.InspectLinuxPublicationValues(duplicateSource, sources), "publication_sources");
        sources.Clear(); HostCheck.True(value.Sources == 2 && value.ImageSha256 == new string('C', 64));
    }

    [HostTest("HTTP Linux exact copied execve binds argv while retaining unknown unfinished and resumed raw trace rows")]
    private static void LinuxExecveValues()
    {
        const string exec = "1.000001 execve(\"/root/http/app\", [\"/root/http/app\", \"--serve-once\", \"--root\", \"/root/http/data\", \"--urls\", \"http://127.0.0.1:18881\"], 0x123 /* 20 vars */) = 0\n";
        const string tail = "1.000002 unknown_unclassified_row\n1.000003 futex(0x10, FUTEX_WAIT, 0 <unfinished ...>\n1.000004 <... futex resumed>) = 0\n1.000005 exit_group(0) = ?\n1.000006 +++ exited with 0 +++\n";
        string[] args = ["--serve-once", "--root", "/root/http/data", "--urls", "http://127.0.0.1:18881"];
        var raw = new Dictionary<string, string> { ["write-trace.88"] = exec + tail }; var trace = NativeAotLogUploadContract.InspectTraceValues(raw, "write");
        F.InspectLinuxExecveValues(trace, "/root/http/app", args); raw.Clear();
        HostCheck.True(trace.Rows.Count == 6 && trace.UnclassifiedRows == 1 && trace.UnjoinedRows == 2 && !trace.CompleteTraceProjection &&
            trace.Rows.All(row => row.Source == "write-trace.88") && trace.Rows[0].Raw == exec.TrimEnd('\n'));
        foreach (string changed in new[] { exec.Replace("/root/http/app", "/root/other/app", StringComparison.Ordinal), exec.Replace("--serve-once", "--write", StringComparison.Ordinal),
            exec.Replace("18881", "18882", StringComparison.Ordinal), exec.Replace("= 0", "= -1 EACCES (Permission denied)", StringComparison.Ordinal), exec + exec, "" })
        {
            var bad = NativeAotLogUploadContract.InspectTraceValues(new Dictionary<string, string> { ["write-trace.88"] = changed + tail }, "write");
            Reject(() => F.InspectLinuxExecveValues(bad, "/root/http/app", args), "execve_identity");
        }
        Reject(() => F.InspectLinuxExecveValues(trace, "/root/http/app", Enumerable.Repeat("x", 17).ToArray()), "execve_arguments");
        Reject(() => F.InspectLinuxExecveValues(trace, "/root/http/app", ["bad\narg"]), "execve_arguments");
    }

    [HostTest("HTTP Linux owner rejects a nonbuilder before source caches outputs or child execution")]
    private static async Task LinuxNoLaunch()
    {
        if (OperatingSystem.IsLinux()) return;
        string root = HostToolProcess.FindRepositoryRoot(), output = Path.Combine(root, "artifacts", "http-linux-forbidden-" + Guid.NewGuid().ToString("N")); bool rejected = false;
        try { using var reference = await F.RunLinuxReferenceAsync(root, root, output, TextWriter.Null); }
        catch (ToolFailure error) when (error.Reason == "log_upload_http_linux_builder_required") { rejected = true; }
        HostCheck.True(rejected && !Path.Exists(output));
    }

    [HostTest("HTTP log upload real managed owner joins finite Kestrel FileStream writes failures cancellation and distinct console verification", HostLane.Native)]
    private static async Task ManagedReference()
    {
        string root = HostToolProcess.FindRepositoryRoot(), parent = Path.Combine(root, "artifacts", "log-upload-http-managed-tests"); Directory.CreateDirectory(parent);
        string output = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var value = await F.RunManagedReferenceAsync(root, output);
        HostCheck.True(!value.GrantsLinuxNativeGuestOrPhysicalPersistenceAuthority && value.Files.LogBytes == 660 && value.Files.UploadBytes == 5126 &&
            value.Responses.Count == 10 && value.Controls.Count == 7 && value.Controls.All(row => row.Joined && row.EndpointRefused));
        HostCheck.True(value.Controls.Single(row => row.Name == "positive") is { ExpectedFailure: false, Cancelled: false, ExitCode: 0 });
        HostCheck.True(value.Controls.Single(row => row.Name == "cancel-body") is { ExpectedFailure: true, Cancelled: true });
        foreach (var control in value.Controls)
        {
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, control.Name, "http.json")));
            HostCheck.True(receipt.RootElement.GetProperty("Complete").GetBoolean() == (control.Name == "positive") &&
                receipt.RootElement.GetProperty("Joined").GetBoolean() && receipt.RootElement.GetProperty("EndpointRefused").GetBoolean() &&
                !receipt.RootElement.GetProperty("GuestQualified").GetBoolean());
        }
        using (var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "receipt.json"))))
            HostCheck.True(receipt.RootElement.GetProperty("Complete").GetBoolean() && !receipt.RootElement.GetProperty("PhysicalPersistenceQualified").GetBoolean());
        NativeAotLogUploadContract.InspectTranscriptValues(File.ReadAllText(Path.Combine(output, "independent-console-verify.stdout.log")), "verify");
        HostCheck.Equal(value.Files, NativeAotLogUploadContract.InspectFileValues(
            File.ReadAllBytes(Path.Combine(output, "positive/data/LOG.TXT")), File.ReadAllBytes(Path.Combine(output, "positive/data/UPLOAD.BIN"))));
        F.InspectServerTranscriptValues(File.ReadAllText(Path.Combine(output, "positive/server.stdout.log")), File.ReadAllText(Path.Combine(output, "positive/server.stderr.log")));
        foreach (var control in new[] { "cancel-body", "io-open-denied" })
        {
            HostCheck.True(File.ReadAllBytes(Path.Combine(output, control, "data/LOG.TXT")).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog().AsSpan(0, 330)) &&
                new FileInfo(Path.Combine(output, control, "data/UPLOAD.BIN")).Length == 0);
        }
        HostCheck.True(File.ReadAllBytes(Path.Combine(output, "log-append-denied/data/LOG.TXT")).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog().AsSpan(0, 330)) &&
            File.ReadAllBytes(Path.Combine(output, "log-append-denied/data/UPLOAD.BIN")).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedUpload()));
        HostCheck.True(File.Exists(Path.Combine(output, "positive/pre-request.json")) && File.Exists(Path.Combine(output, "inputs.json")));
        Console.WriteLine("LOG_UPLOAD_HTTP_MANAGED_REFERENCE " + output);
    }

    private static (F.ResponseKind Kind, string Method, string Target, int Status, string Type, byte[] Body)[] Responses() =>
    [
        (F.ResponseKind.Health, "GET", "/healthz", 200, "text/plain; charset=utf-8", Encoding.ASCII.GetBytes("LOG_UPLOAD_HTTP_OK\n")),
        (F.ResponseKind.StartupLog, "GET", "/log", 200, "text/plain; charset=utf-8", NativeAotLogUploadContract.ExpectedLog()[..330]),
        (F.ResponseKind.WrongType, "POST", "/upload", 415, "", []), (F.ResponseKind.WrongLength, "POST", "/upload", 400, "", []),
        (F.ResponseKind.Oversized, "POST", "/upload", 413, "", []), (F.ResponseKind.Busy, "POST", "/upload", 409, "", []),
        (F.ResponseKind.Accepted, "POST", "/upload", 201, "text/plain; charset=utf-8", Encoding.ASCII.GetBytes("LOG_UPLOAD_HTTP_ACCEPTED\n")),
        (F.ResponseKind.Log, "GET", "/log", 200, "text/plain; charset=utf-8", NativeAotLogUploadContract.ExpectedLog()),
        (F.ResponseKind.Upload, "GET", "/upload", 200, "application/octet-stream", NativeAotLogUploadContract.ExpectedUpload())
    ];
    private static void Reject(Action action, string reason)
    { bool rejected = false; try { action(); } catch (ToolFailure error) when (error.Reason == "log_upload_http_" + reason) { rejected = true; } HostCheck.True(rejected); }
    private static void RejectAny(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
