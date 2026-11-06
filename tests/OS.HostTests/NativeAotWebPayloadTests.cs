using System.Security.Cryptography;
using System.Text.Json;
using OS.Tooling;

internal static class NativeAotWebPayloadTests
{
    [HostTest("nativeaot.web receipt rejects stale source missing altered and unexpected output")]
    private static void StaleArtifacts()
    {
        foreach (string mutation in new[] { "source", "missing", "altered", "extra", "pin", "schema", "old_schema" })
        {
            string root = Path.Combine(Path.GetTempPath(), "clros-web-receipt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var sources = new Dictionary<string, string>();
                foreach (string app in new[] { "NativeAotHello", "NativeAotRuntimeSmoke", "MinimalApiSmoke" })
                {
                    string relative = "apps/" + app + "/Program.cs";
                    string path = Path.Combine(root, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, app);
                    sources.Add(relative, Hash(path));
                }
                string evidence = Path.Combine(root, "artifacts/evidence");
                Directory.CreateDirectory(evidence);
                string output = Path.Combine(evidence, "observed.log");
                File.WriteAllText(output, "original");
                var outputs = new Dictionary<string, string> { ["observed.log"] = Hash(output) };
                string sdk = mutation == "pin" ? "10.0.100" : NativeAotPayload.SdkVersion;
                var receipt = new NativeAotWebPayload.QualificationReceipt(mutation == "schema" ? "clros.nativeaot.web.v1" : mutation == "old_schema" ? "clros.nativeaot.web.v3" : "clros.nativeaot.web.v4", sdk,
                    NativeAotPayload.RuntimeVersion, sources, outputs);
                File.WriteAllText(Path.Combine(evidence, "web-receipt.json"), JsonSerializer.Serialize(receipt));
                if (mutation == "source")
                    File.AppendAllText(Path.Combine(root, sources.Keys.First()), "changed");
                if (mutation == "missing")
                    File.Delete(output);
                if (mutation == "altered")
                {
                    DateTime timestamp = File.GetLastWriteTimeUtc(output);
                    File.WriteAllText(output, "modified");
                    File.SetLastWriteTimeUtc(output, timestamp);
                }
                if (mutation == "extra")
                    File.WriteAllText(Path.Combine(evidence, "unexpected.log"), "extra");
                string? failure = null;
                try
                {
                    NativeAotWebPayload.ValidateQualification(root, evidence);
                }
                catch (InvalidDataException error)
                {
                    failure = error.Message;
                }
                HostCheck.Equal(mutation switch
                {
                    "pin" or "schema" or "old_schema" => "native_receipt_pin",
                    "source" => "source_inputs_changed",
                    _ => "native_evidence_changed"
                }, failure!);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [HostTest("nativeaot.web borrowed logging grammar joins both streams native writer identities and invalid fd")]
    private static void LoggingGrammar()
    {
        foreach (string mutation in new[] { "none", "missing_stdout", "duplicate_stderr", "startup_order", "missing_vector",
            "duplicate_vector", "short_vector", "same_native_thread", "wrong_errno", "extra_stderr", "unterminated_stderr",
            "unterminated_stdout", "stdout_failure", "missing_stage", "duplicate_stage", "wrong_stage", "early_stage" })
        {
            var (stdout, stderr, traces) = LoggingFixture();
            switch (mutation)
            {
                case "missing_stdout":
                    stdout = stdout[(stdout.IndexOf('\n') + 1)..];
                    break;
                case "duplicate_stderr":
                    stderr += "CLROS_LOG fd=2 worker=0 record=0\n";
                    break;
                case "startup_order":
                    stderr = stderr.Replace("CLROS_APP_CCTOR", "CLROS_APP_OTHER", StringComparison.Ordinal);
                    break;
                case "missing_vector":
                    traces["fixture.2000"] = "";
                    break;
                case "duplicate_vector":
                    traces["fixture.2000"] += traces["fixture.2000"].Split('\n')[0] + "\n";
                    break;
                case "short_vector":
                    traces["fixture.2000"] = traces["fixture.2000"].Replace(" = 33", " = 1", StringComparison.Ordinal);
                    break;
                case "same_native_thread":
                    traces["fixture.2000"] += traces["fixture.2001"];
                    traces.Remove("fixture.2001");
                    break;
                case "wrong_errno":
                    traces["fixture.1"] = traces["fixture.1"].Replace("EBADF", "EIO", StringComparison.Ordinal);
                    break;
                case "extra_stderr":
                    stderr += "unexpected\n";
                    break;
                case "unterminated_stderr":
                    stderr = stderr[..^1];
                    break;
                case "unterminated_stdout":
                    stdout = stdout[..^1];
                    break;
                case "stdout_failure":
                    stdout += "TEST_FAIL logging.fixture\n";
                    break;
                case "missing_stage":
                    stderr = stderr.Replace("CLROS_VECTOR_STAGED bytes=49152 blocks=3\n", "", StringComparison.Ordinal);
                    break;
                case "duplicate_stage":
                    stderr += "CLROS_VECTOR_STAGED bytes=49152 blocks=3\n";
                    break;
                case "wrong_stage":
                    stderr = stderr.Replace("blocks=3", "blocks=2", StringComparison.Ordinal);
                    break;
                case "early_stage":
                    stderr = stderr.Replace("CLROS_VECTOR_STAGED bytes=49152 blocks=3\n", "", StringComparison.Ordinal)
                    .Replace("CLROS_APP_BUILT minimal.api.smoke\n", "CLROS_APP_BUILT minimal.api.smoke\nCLROS_VECTOR_STAGED bytes=49152 blocks=3\n", StringComparison.Ordinal);
                    break;
            }
            bool rejected = false;
            try
            {
                NativeAotWebPayload.ValidateLoggingEvidence(stdout, stderr, traces);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            HostCheck.Equal(mutation != "none", rejected, mutation);
        }
    }

    private static (string Stdout, string Stderr, Dictionary<string, string> Traces) LoggingFixture()
    {
        string Line(int fd, int worker, int record) => $"CLROS_LOG fd={fd} worker={worker} record={record}";
        string stdout = string.Join('\n', Enumerable.Range(0, 8).SelectMany(w => Enumerable.Range(0, 4).Select(r => Line(1, w, r)))) + "\n";
        string stderr = "CLROS_APP_ENTRY minimal.api.smoke\nCLROS_APP_CCTOR minimal.api.smoke\nCLROS_APP_CTOR minimal.api.smoke\nCLROS_APP_BUILT minimal.api.smoke\n" +
            string.Join('\n', Enumerable.Range(0, 8).SelectMany(w => Enumerable.Range(0, 4).Select(r => Line(2, w, r)))) + "\nCLROS_VECTOR_STAGED bytes=49152 blocks=3\n";
        var traces = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fixture.1"] = "writev(1234, [{iov_base=\"!\", iov_len=1}], 1) = -1 EBADF (Bad file descriptor)\n"
        };
        for (int worker = 0; worker < 8; worker++)
        {
            traces["fixture." + (2000 + worker)] = string.Concat(Enumerable.Range(0, 4).Select(record =>
            {
                string body = Line(2, worker, record);
                return $"writev(2, [{{iov_base=\"{body}\", iov_len={body.Length}}}, {{iov_base=\"\\n\", iov_len=1}}], 2) = {body.Length + 1}\n";
            }));
        }
        return (stdout, stderr, traces);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
