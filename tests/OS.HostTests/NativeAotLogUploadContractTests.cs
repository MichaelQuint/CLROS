using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadContract;

internal static class NativeAotLogUploadContractTests
{
    [HostTest("log upload copied file contract checks exact UTF8 records sector boundaries upload chunks and EOF without granting persistence")]
    private static void FileValues()
    {
        byte[] log = F.ExpectedLog(), upload = F.ExpectedUpload();
        string text = new UTF8Encoding(false, true).GetString(log);
        HostCheck.True(log.Length > 512 && log.Length <= 4096 && text.Split('\n').Length == 13 &&
            text.StartsWith("CLROS_LOG v1 sequence=01 message=CLROS ready — café\n", StringComparison.Ordinal) &&
            text.EndsWith("sequence=12 message=CLROS ready — café\n", StringComparison.Ordinal));
        HostCheck.True(upload.Length == 5126 && upload[0] == 19 && upload[4093] != upload[4094] && upload[4095] != upload[4096]);
        var values = F.InspectFileValues(log, upload);
        HostCheck.True(values.LogBytes == log.Length && values.UploadBytes == 5126 && !values.GrantsPersistenceOrGuestAuthority);
        foreach (int at in new[] { 0, 511, 512, 4093, 4094, 4095, 4096, 5125 })
        { byte[] altered = upload.ToArray(); altered[at] ^= 1; Reject(() => F.InspectFileValues(log, altered), "upload_bytes"); }
        Reject(() => F.InspectFileValues(log.AsSpan(1), upload), "log_bytes");
        Reject(() => F.InspectFileValues(log.Concat(new byte[] { 0 }).ToArray(), upload), "log_bytes");
        Reject(() => F.InspectFileValues(log, upload.AsSpan(0, upload.Length - 1)), "upload_bytes");
        Reject(() => F.InspectFileValues(log, upload.Concat(new byte[] { 0xa5 }).ToArray()), "upload_bytes");
    }

    [HostTest("log upload copied transcript requires exact phase policy hashes markers and bounded complete output")]
    private static void TranscriptValues()
    {
        var values = F.InspectFileValues(F.ExpectedLog(), F.ExpectedUpload());
        string Write(string phase) => "TEST_START m10.027\nLOG_UPLOAD_PHASE " + phase + "\n" +
            "LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false") + "\n" +
            $"LOG_UPLOAD_FILES logBytes={values.LogBytes} logSha256={values.LogSha256} uploadBytes={values.UploadBytes} uploadSha256={values.UploadSha256}\nTEST_PASS m10.027\n";
        foreach (string phase in new[] { "write", "verify" })
        {
            string stdout = Write(phase); var transcript = F.InspectTranscriptValues(stdout, phase);
            HostCheck.True(transcript.Phase == phase && transcript.Files == values && !transcript.GrantsOriginalProcessOrFlushAuthority);
            HostCheck.Equal(transcript, F.InspectTranscriptValues(stdout.Replace("\n", "\r\n", StringComparison.Ordinal), phase));
            foreach (string bad in new[] { stdout + "unexpected\n", stdout[..^1], stdout.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal),
                stdout.Replace(values.LogSha256, new string('0', 64), StringComparison.Ordinal), stdout.Replace("buffering=0", "buffering=4096", StringComparison.Ordinal),
                stdout.Replace("logBytes=", "logBytes=0", StringComparison.Ordinal), stdout.Replace("LOG_UPLOAD_PHASE " + phase, "LOG_UPLOAD_PHASE foreign", StringComparison.Ordinal) })
                Reject(() => F.InspectTranscriptValues(bad, phase), "transcript");
        }
        Reject(() => F.InspectTranscriptValues(new string('x', F.MaximumTranscriptBytes + 1), "write"), "transcript");
        Reject(() => F.InspectTranscriptValues(Write("write"), "unknown"), "phase");
    }

    [HostTest("log upload project source inventory and native publication plan preserve original musl recipe and exclude SQLite")]
    private static void PublicationPlan()
    {
        string root = HostToolProcess.FindRepositoryRoot(); F.RequireCurrentSources(root);
        string destination = Path.Combine(root, "artifacts/log-upload-plan");
        string[] prior = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        prior[1] = "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj";
        string recipe = JsonSerializer.Serialize(new[] { new { executable = "dotnet", arguments = prior } });
        string[] plan = F.PublishArguments(recipe, destination);
        HostCheck.True(plan[1] == "apps/NativeAotLogUploadContract/NativeAotLogUploadContract.csproj" &&
            plan.Contains("-p:StaticExecutable=true") && plan.Contains("-p:PositionIndependentExecutable=false") &&
            plan.Contains("-p:RuntimeFrameworkVersion=" + NativeAotPayload.RuntimeVersion));
        string project = File.ReadAllText(Path.Combine(root, "apps", F.Application, F.Application + ".csproj"));
        HostCheck.True(project.Contains("<LangVersion>14.0</LangVersion>", StringComparison.Ordinal) &&
            project.Contains("<TargetFramework>net11.0</TargetFramework>", StringComparison.Ordinal) &&
            !project.Contains("PackageReference", StringComparison.Ordinal) && !project.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        bool rejected = false;
        try { F.PublishArguments(recipe.Replace("-p:StaticExecutable=true", "-p:StaticExecutable=false", StringComparison.Ordinal), destination); }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("log upload owned Windows managed processes write reopen verify and retain exact files while preserving nonempty rejection", HostLane.Native)]
    private static async Task ManagedReference()
    {
        string root = HostToolProcess.FindRepositoryRoot(), parent = Path.Combine(root, "artifacts/log-upload-managed-tests"); Directory.CreateDirectory(parent);
        string output = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var value = await F.RunManagedReferenceAsync(root, output);
        HostCheck.True(!value.GrantsLinuxNativeAotGuestOrPersistenceAuthority && value.Write.Phase == "write" && value.Verify.Phase == "verify" && value.Write.Files == value.Verify.Files);
        byte[] log = File.ReadAllBytes(Path.Combine(output, "data", F.LogName)), upload = File.ReadAllBytes(Path.Combine(output, "data", F.UploadName));
        HostCheck.Equal(value.Write.Files, F.InspectFileValues(log, upload));
        HostCheck.True(value.Artifacts.All(File.Exists) && File.Exists(Path.Combine(output, "nonempty.stdout.log")));
        HostCheck.True(File.Exists(Path.Combine(output, "relative-root.process.json")) &&
            File.Exists(Path.Combine(output, "missing-upload.process.json")) && File.Exists(Path.Combine(output, "corrupt-upload.process.json")));
        Reject(() => F.RequireFreshOutput(root, output), "fresh_output_required");
        string cancelledOutput = Path.Combine(parent, Guid.NewGuid().ToString("N")); bool cancelled = false;
        try { await F.RunManagedReferenceAsync(root, cancelledOutput, new CancellationToken(canceled: true)); }
        catch (OperationCanceledException) { cancelled = true; }
        HostCheck.True(cancelled && File.Exists(Path.Combine(cancelledOutput, "receipt.json")));
        using (var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(cancelledOutput, "receipt.json"))))
            HostCheck.True(!receipt.RootElement.GetProperty("Succeeded").GetBoolean() && !receipt.RootElement.GetProperty("FlushObserved").GetBoolean());
        Console.WriteLine("LOG_UPLOAD_MANAGED_REFERENCE " + output);
    }

    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try { action(); } catch (ToolFailure failure) when (failure.Reason == "log_upload_" + reason) { rejected = true; }
        HostCheck.True(rejected);
    }
}
