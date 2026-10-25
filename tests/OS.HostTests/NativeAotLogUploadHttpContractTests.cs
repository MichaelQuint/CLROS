using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
}
