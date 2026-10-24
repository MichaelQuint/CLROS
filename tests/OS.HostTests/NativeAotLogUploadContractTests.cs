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

    [HostTest("log upload Linux trace plan keeps literal native arguments independent phases and complete unfiltered syscall capture")]
    private static void LinuxTracePlan()
    {
        const string executable = "/work/published app/NativeAotLogUploadContract", data = "/work/reference data", prefix = "/work/write-trace";
        string[] write = F.LinuxTraceArguments(executable, data, prefix, "write");
        string[] verify = F.LinuxTraceArguments(executable, data, "/work/verify-trace", "verify");
        HostCheck.True(write[0] == "-ff" && write.Contains("-yy") && write.Contains("-ttt") &&
            write[Array.IndexOf(write, "-o") + 1] == prefix && write[Array.IndexOf(write, "--root") + 1] == data &&
            write.Contains(executable) && write.Contains("--write") && !write.Contains("--verify") &&
            verify.Contains("--verify") && !verify.Contains("--write") && !write.Any(value => value.StartsWith("-e", StringComparison.Ordinal)));
        HostCheck.True(write.Count(value => value == executable) == 1 && write.Count(value => value == data) == 1 &&
            write.Length == verify.Length && !write.Any(value => value.Contains("sh -c", StringComparison.Ordinal)));
        foreach (string bad in new[] { "relative", "/work/../data", "/work/./data", "/work//data", "/work/data/", "/work/data\n", @"C:\work\data", "/work\\data" })
            Reject(() => F.LinuxTraceArguments(executable, bad, prefix, "write"), "linux_path");
        Reject(() => F.LinuxTraceArguments(executable, executable, prefix, "write"), "linux_path");
        Reject(() => F.LinuxTraceArguments(executable, data, prefix, "recovery"), "phase");
    }

    [HostTest("log upload copied Linux trace preserves signal pipe unfinished resumed exiting error and unknown rows without inferred returns")]
    private static void LinuxTraceRows()
    {
        const string main = """
            1792836276.000001 execve("/work/app", ["/work/app", "--write"], 0x1234 /* 4 vars */) = 0
            1792836276.000002 openat(AT_FDCWD, "/work/data/LOG.TXT", O_WRONLY|O_CREAT|O_CLOEXEC, 0600) = 3</work/data/LOG.TXT>
            1792836276.000003 fstatfs(3</work/data/LOG.TXT>, {f_type=EXT4_SUPER_MAGIC}) = 0
            1792836276.000004 flock(3</work/data/LOG.TXT>, LOCK_SH|LOCK_NB) = -1 ENOSYS (Function not implemented)
            1792836276.000005 pwritev2(3</work/data/LOG.TXT>, [{iov_base="CLROS", iov_len=5}], 1, 0, RWF_NOAPPEND) = 5
            1792836276.000006 fsync(3</work/data/LOG.TXT>) = -1 EROFS (Read-only file system)
            1792836276.000007 close(3</work/data/LOG.TXT>) = 0
            1792836276.000008 exit_group(0) = ?
            1792836276.000009 +++ exited with 0 +++
            """;
        const string signalPipe = """
            1792836276.000001 read(3<pipe:[6103]>, <unfinished ...>
            1792836276.000002 <... read resumed>"x", 1) = 1
            1792836276.000003 read(3<pipe:[6103]> <unfinished ...>) = ?
            1792836276.000004 --- SIGRTMIN {si_signo=SIGRTMIN, si_code=SI_TKILL, si_pid=17, si_uid=0} ---
            1792836276.000005 unsupported original row
            1792836276.000009 +++ exited with 0 +++
            """;
        var files = new Dictionary<string, string> { ["write-trace.17"] = main + "\n", ["write-trace.18"] = signalPipe + "\n" };
        var value = F.InspectTraceValues(files, "write");
        HostCheck.True(value.Files == 2 && value.ExitedFiles == 2 && value.UnjoinedRows == 3 && value.UnreturnedRows == 1 &&
            value.UnclassifiedRows == 1 && !value.CompleteTraceProjection && !value.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
        HostCheck.Equal(main.Split('\n').Length + signalPipe.Split('\n').Length, value.Rows.Count);
        HostCheck.True(value.Rows.Where(row => row.Source == "write-trace.17").Select(row => row.Raw).SequenceEqual(main.Split('\n')) &&
            value.Rows.Where(row => row.Source == "write-trace.18").Select(row => row.Raw).SequenceEqual(signalPipe.Split('\n')));
        HostCheck.True(value.Rows.Single(row => row.Syscall == "flock").Result.StartsWith("-1 ENOSYS", StringComparison.Ordinal) &&
            value.Rows.Single(row => row.Syscall == "fsync").Result.StartsWith("-1 EROFS", StringComparison.Ordinal) &&
            value.Rows.Single(row => row.Syscall == "exit_group").Kind == F.TraceKind.UnreturnedCall &&
            value.Rows.Any(row => row.Kind == F.TraceKind.UnfinishedCall && row.Raw.Contains("6103", StringComparison.Ordinal)) &&
            value.Rows.Any(row => row.Kind == F.TraceKind.ResumedCall) && value.Rows.Any(row => row.Kind == F.TraceKind.Signal));
        string complete = "1.000001 close(3</work/data/UPLOAD.BIN>) = 0\n1.000002 +++ exited with 0 +++\n";
        var copied = F.InspectTraceValues(new Dictionary<string, string> { ["verify-trace.44"] = complete }, "verify");
        HostCheck.True(copied.CompleteTraceProjection && !copied.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
        var killed = F.InspectTraceValues(new Dictionary<string, string> { ["verify-trace.44"] = complete.Replace("exited with 0", "killed by SIGKILL", StringComparison.Ordinal) }, "verify");
        HostCheck.True(killed.ExitedFiles == 0 && !killed.CompleteTraceProjection && killed.Rows[^1].Kind == F.TraceKind.ProcessExit);
    }

    [HostTest("log upload trace bounds reject partial bytes foreign phase PID aliases and capacity before Linux execution")]
    private static async Task LinuxTraceBounds()
    {
        const string valid = "1.000001 close(3) = 0\n1.000002 +++ exited with 0 +++\n";
        Dictionary<string, string> One(string name, string text) => new() { [name] = text };
        foreach (string name in new[] { "write-trace.0", "write-trace.01", "write-trace.-1", "write-trace.2147483648", "write-trace.", "../write-trace.1", "verify-trace.1" })
            Reject(() => F.InspectTraceValues(One(name, valid), "write"), "trace_identity");
        Reject(() => F.InspectTraceValues(One("write-trace.1", valid[..^1]), "write"), "trace_truncated");
        Reject(() => F.InspectTraceValues(One("write-trace.1", "1.000001 read(3, \0, 0) = 0\n"), "write"), "trace_line");
        Reject(() => F.InspectTraceValues(One("write-trace.1", new string('x', 8193) + "\n"), "write"), "trace_line");
        Reject(() => F.InspectTraceValues(One("write-trace.1", new string('é', 2 * 1024 * 1024) + "\n"), "write"), "trace_capacity");
        Reject(() => F.InspectTraceValues(Enumerable.Range(1, 65).ToDictionary(i => "write-trace." + i, _ => valid), "write"), "trace_inventory");
        Reject(() => F.InspectTraceValues(new Dictionary<string, string>(), "write"), "trace_inventory");
        if (!OperatingSystem.IsLinux())
        {
            string root = HostToolProcess.FindRepositoryRoot(), destination = Path.Combine(root, "artifacts", "log-upload-linux-forbidden-" + Guid.NewGuid().ToString("N"));
            bool rejected = false;
            try { using var reference = await F.RunLinuxReferenceAsync(root, root, destination, TextWriter.Null); }
            catch (ToolFailure failure) when (failure.Reason == "log_upload_linux_builder_required") { rejected = true; }
            HostCheck.True(rejected && !Path.Exists(destination));
        }
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
