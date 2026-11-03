using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Tooling;
using OS.Tooling.Native;

// Copied protocol controls only; no QEMU, firmware, guest or native acceptance.
internal static partial class WhpxPrelaunchTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<JoinPendingForTest>k__BackingField")]
    private static extern ref Func<BoundedJobProcessRunner.OwnershipJoinObservation, bool>? JoinProbe(ProcessRequest request);
    private const string Hash = "47D57A6072E0BB3BD98F87926EB129EB1736DFE818C67B3B81EF7CE4EDD0B3CD";
    private const string Machine = "q35,accel=whpx,kernel-irqchip=on";
    private const string Executable = "C:\\QEMU\\qemu-system-x86_64.exe";
    private static byte[] Warning => Encoding.UTF8.GetBytes(Executable + ": warning: Ignoring request for interrupt vector 0\r\n");
    private static Type Target => Assembly.LoadFrom(HostTools.ManagedTool(HostToolProcess.FindRepositoryRoot(), "OS.QemuRunner"))
        .GetType("OS.Tools.QemuRunner.WhpxPrelaunch", throwOnError: true)!;
    private static string? Prefix(string hash, string machine, string status, bool running, bool resumed, byte[] bytes) =>
        (string?)Target.GetMethod("ValidatePrelaunch", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, [hash, machine, Executable, status, running, resumed, bytes]);
    private static string? Seal(byte[] prefix, byte[] actual, string key = "original", bool resumed = true) =>
        (string?)Target.GetMethod("ValidateSeal", BindingFlags.Static | BindingFlags.Public)!
        .Invoke(null, [prefix, actual, "original", key, resumed]);

    [HostTest("WHPX prelaunch admits only empty or one exact initialization prefix on the pinned recipe")]
    private static void ExactPrefix()
    {
        HostCheck.True(Prefix(Hash, Machine, "prelaunch", false, false, []) is null);
        HostCheck.True(Prefix(Hash, Machine, "prelaunch", false, false, Warning) is null);
        HostCheck.True(Seal(Warning, Warning) is null && Seal([], []) is null);
    }

    [HostTest("WHPX prelaunch refuses wrong binary controller and nonprelaunch or early resume states")]
    private static void IdentityAndPhase()
    {
        HostCheck.Equal("whpx_prelaunch_identity", Prefix(new string('0', 64), Machine, "prelaunch", false, false, Warning));
        foreach (string machine in new[] { "q35,accel=whpx", "q35,accel=whpx,kernel-irqchip=off", Machine + ",pic=off", "q35,accel=tcg" })
            HostCheck.Equal("whpx_prelaunch_identity", Prefix(Hash, machine, "prelaunch", false, false, Warning));
        foreach (string status in new[] { "", "running", "paused", "prelaunch " })
            HostCheck.Equal("whpx_prelaunch_phase", Prefix(Hash, Machine, status, false, false, Warning));
        HostCheck.Equal("whpx_prelaunch_phase", Prefix(Hash, Machine, "prelaunch", true, false, Warning));
        HostCheck.Equal("whpx_prelaunch_phase", Prefix(Hash, Machine, "prelaunch", false, true, Warning));
        HostCheck.Equal("whpx_prelaunch_seal_changed", Seal(Warning, Warning, resumed: false));
    }

    [HostTest("WHPX prelaunch refuses unexpected repeated malformed and partial startup bytes")]
    private static void UnexpectedPrefix()
    {
        foreach (byte[] bytes in new[] { Encoding.UTF8.GetBytes("unexpected\r\n"), Warning.Concat(Warning).ToArray(),
            Warning.Concat(new byte[] { 0 }).ToArray(), Encoding.UTF8.GetBytes("other.exe: warning: Ignoring request for interrupt vector 0\r\n") })
            HostCheck.Equal("whpx_prelaunch_stderr_prefix", Prefix(Hash, Machine, "prelaunch", false, false, bytes));
        for (int length = 1; length < Warning.Length; length++)
            HostCheck.Equal("whpx_prelaunch_stderr_prefix", Prefix(Hash, Machine, "prelaunch", false, false, Warning[..length]));
    }

    [HostTest("WHPX prelaunch rejects identical late warning truncation replacement and sealed byte mutation")]
    private static void LateAndMutatedBytes()
    {
        foreach (byte[] bytes in new[] { Warning.Concat(Warning).ToArray(), Warning[..^1], [], Warning.Concat(new byte[] { 10 }).ToArray() })
            HostCheck.Equal("whpx_prelaunch_seal_changed", Seal(Warning, bytes));
        HostCheck.Equal("whpx_prelaunch_seal_changed", Seal([], Warning));
        HostCheck.Equal("whpx_prelaunch_seal_changed", Seal(Warning, Warning, key: "replacement"));
        for (int offset = 0; offset < Warning.Length; offset++)
        {
            byte[] changed = Warning;
            changed[offset] ^= 1;
            HostCheck.Equal("whpx_prelaunch_seal_changed", Seal(Warning, changed));
        }
    }

    [HostTest("WHPX prelaunch requires reset instruction entry selector and control register evidence")]
    private static void ResetState()
    {
        var validate = Target.GetMethod("ResetRegisters", BindingFlags.Static | BindingFlags.Public)!.CreateDelegate<Func<string, bool>>();
        string text = "CPU#0\r\nEIP=0000fff0 EFL=00000046\r\nCS =f000 ffff0000\r\nCR0=60000010\r\n";
        HostCheck.True(validate(text));
        foreach (string field in new[] { "EIP=0000fff0", "CS =f000 ffff0000", "CR0=60000010" })
            HostCheck.True(!validate(text.Replace(field, "missing", StringComparison.Ordinal)));
        HostCheck.True(!validate(text.Replace("EIP=0000fff0", "EIP=0000fff1", StringComparison.Ordinal)));
    }

    private static string ReadSharedText(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(input, Encoding.ASCII);
        return reader.ReadToEnd();
    }

    [HostTest("Windows retained external stderr preserves independent read position and launch failure ownership")]
    private static async Task DirectFileLifecycle()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string root = Path.Combine(Path.GetTempPath(), "clros-whpx-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string raw = Path.Combine(root, "stderr.raw");
        using var writer = File.OpenHandle(raw, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        string host = HostTools.FindExecutable(null, "DOTNET_HOST_PATH", "dotnet") ?? throw new FileNotFoundException("dotnet host missing");
        async Task<ProcessResult> Emit(string text)
        {
            var request = new ProcessRequest(host, [typeof(HostTestRunner).Assembly.Location, "--runner-control", "streams"], root)
            {
                Timeout = TimeSpan.FromSeconds(10),
                OutputCharacterLimit = 4096,
                RetainOwnershipUntilJoined = true,
                StandardErrorHandle = writer
            };
            string? previous = null;
            JoinProbe(request) = observation =>
            {
                string row = $"WHPX_FILE_JOIN value={text} active={observation.ActiveProcesses} total={observation.TotalProcesses} captured={observation.CapturedProcesses} complete={observation.CaptureComplete} root={observation.RootSignaled} members={observation.MembersSignaled} assigned={observation.Assigned} expired={observation.InitialWaitExpired}";
                if (row != previous)
                {
                    Console.WriteLine(row);
                    previous = row;
                }
                return false; // Observe only; never fabricate or bypass an original join.
            };
            Console.WriteLine("WHPX_FILE_STAGE emit-start=" + text);
            var result = await ProcessRunner.RunAsync(request);
            Console.WriteLine("WHPX_FILE_STAGE emit-joined=" + text);
            return result;
        }
        var first = await Emit("A");
        HostCheck.True(first.Succeeded && first.StandardErrorExternallyOwned && first.StandardError.Length == 0);
        HostCheck.Equal("stderr-ok", ReadSharedText(raw));
        HostCheck.Equal("stdout-ok", first.StandardOutput);
        var second = await Emit("B");
        HostCheck.True(second.Succeeded && second.TerminationConfirmed && second.StandardErrorExternallyOwned && second.StandardError.Length == 0);
        HostCheck.Equal("stdout-ok", second.StandardOutput);
        HostCheck.Equal("stderr-okstderr-ok", ReadSharedText(raw)); // A reader must not reset the inherited writer's file position.
        Console.WriteLine("WHPX_FILE_STAGE missing-launch-start");
        var missing = await ProcessRunner.RunAsync(new(Path.Combine(root, "missing.exe"), [], root)
        {
            Timeout = TimeSpan.FromSeconds(10),
            RetainOwnershipUntilJoined = true,
            StandardErrorHandle = writer
        });
        HostCheck.True(!missing.Succeeded && missing.TerminationConfirmed && !writer.IsClosed);
        Console.WriteLine("WHPX_FILE_STAGE missing-launch-returned");
        HostCheck.Equal("stderr-okstderr-ok", ReadSharedText(raw));
        bool protectedFile = false;
        try
        {
            using var other = new FileStream(raw, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            protectedFile = true;
        }
        HostCheck.True(protectedFile, "surviving original writer still denies a second writer");
        bool refused = false;
        try
        {
            await ProcessRunner.RunAsync(new(host, [typeof(HostTestRunner).Assembly.Location, "--runner-control", "streams"], root)
            {
                StandardErrorHandle = writer
            });
        }
        catch (ArgumentException error)
        {
            refused = error.Message == "process_external_stderr_requires_live_handle_and_retained_windows_job";
        }
        HostCheck.True(refused, "external handle cannot release ownership on a bounded but unconfirmed join");
    }

    [HostTest("Windows external stderr remains protected and byte exact after inner owner cancellation")]
    private static async Task DirectFileCancellation()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string root = Path.Combine(Path.GetTempPath(), "clros-whpx-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string raw = Path.Combine(root, "stderr.raw");
        using var writer = File.OpenHandle(raw, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var stop = new CancellationTokenSource();
        Task<ProcessResult> process = ProcessRunner.RunAsync(new(
            (HostTools.FindExecutable(null, "DOTNET_HOST_PATH", "dotnet") ?? throw new FileNotFoundException("dotnet host missing")),
            [typeof(HostTestRunner).Assembly.Location, "--runner-control", "streams-wait"], root)
        {
            Timeout = TimeSpan.FromSeconds(10),
            OutputCharacterLimit = 4096,
            RetainOwnershipUntilJoined = true,
            StandardErrorHandle = writer
        }, stop.Token);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!process.IsCompleted && ReadSharedText(raw) != "stderr-ok" && clock.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        stop.Cancel();
        ProcessResult joined = await process;
        HostCheck.True(joined.Cancelled && joined.TerminationConfirmed && !joined.TimedOut && joined.StandardErrorExternallyOwned);
        HostCheck.Equal("stderr-ok", ReadSharedText(raw));
        HostCheck.True(!writer.IsClosed);
        bool protectedFile = false;
        try
        {
            File.WriteAllBytes(raw, []);
        }
        catch (IOException)
        {
            protectedFile = true;
        }
        HostCheck.True(protectedFile, "original lease must survive the complete cancelled process-tree join");
        HostCheck.Equal("whpx_prelaunch_seal_changed", Seal([], Encoding.ASCII.GetBytes(ReadSharedText(raw))));
    }

    [HostTest("WHPX joined-file controls bind original phase anchors and refuse late bytes and incomplete ownership")]
    private static void JoinedFileControls()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string root = Path.Combine(Path.GetTempPath(), "clros-whpx-joined-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var validate = typeof(QemuTranscripts).GetMethod("AssertWhpxJoinedFile", BindingFlags.Static | BindingFlags.NonPublic)!;
        static string Digest(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        static byte[] Json(object value) => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
        int index = 0;
        void Check(string mutation, bool expected)
        {
            string serial = Path.Combine(root, (++index).ToString() + ".serial.log"), directory = serial + ".whpx-prelaunch";
            Directory.CreateDirectory(directory);
            byte[] warning = Warning, raw = mutation == "late" ? [.. warning, .. warning] : mutation == "empty" ? [] : warning;
            string rawPath = Path.Combine(directory, "stderr.raw");
            File.WriteAllBytes(rawPath, raw);
            using var writer = File.OpenHandle(rawPath, FileMode.Open, FileAccess.Write, FileShare.Read);
            string key = OS.Tooling.Native.NativeFile.GetIdentity(writer).Key;
            byte[] owner = Json(new
            {
                Schema = "clros.whpx.outer-file-owner.v2",
                Mode = "surviving-original-owner",
                RawIdentity = key,
                Qemu = Executable,
                Serial = serial,
                Raw = rawPath,
                QemuSha256 = Hash,
                Machine
            });
            string ownerHash = Digest(owner), start = "2026-11-01T00:00:00.0000000Z";
            var status = new
            {
                status = "prelaunch",
                running = false
            };
            var cpus = new[] { new Dictionary<string, object> { ["cpu-index"] = 0 } };
            string registers = "EIP=0000fff0 CS =f000 ffff0000 CR0=60000010";
            object[] values = [new { }, status, cpus, registers, status, new { }];
            string[] commands = ["qmp_capabilities", "query-status", "query-cpus-fast", "human-monitor-command", "query-status", "cont"];
            var qmp = new StringBuilder("{\"QMP\":{}}\n");
            for (int i = 0; i < commands.Length; i++)
            {
                qmp.Append("REQUEST ").Append(System.Text.Json.JsonSerializer.Serialize(new
                {
                    execute = commands[i],
                    arguments = i == 3 ? new Dictionary<string, string> { ["command-line"] = "info registers" } : new(),
                    id = i + 1
                })).Append('\n');
                qmp.Append(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { ["return"] = values[i], ["id"] = i + 1 })).Append('\n');
            }
            byte[] qmpBytes = Encoding.UTF8.GetBytes(qmp.ToString());
            byte[] prefix = mutation == "empty" ? [] : warning;
            byte[] seal = Json(new
            {
                Schema = "clros.whpx.prelaunch-seal.v2",
                Transport = "inherited-standard-error-file",
                OwnerSha256 = ownerHash,
                QemuSha256 = Hash,
                Executable,
                RawIdentity = key,
                ContCommandsBeforeSeal = 0,
                ProcessId = 123,
                StartUtc = start,
                PrefixBytes = prefix.Length,
                PrefixSha256 = Digest(prefix),
                Before = status,
                After = status,
                Cpus = cpus,
                Registers = registers,
                Arguments = new[] { "-machine", Machine, "-cpu", "Nehalem", "-S" }
            });
            byte[] resume = Json(new
            {
                Schema = "clros.whpx.prelaunch-resume.v2",
                OwnerSha256 = ownerHash,
                ContCommands = 1,
                ContAcknowledged = true,
                ProcessId = 123,
                SealSha256 = Digest(seal),
                PrefixSha256 = Digest(prefix),
                PrefixBytes = prefix.Length,
                QmpSha256 = Digest(qmpBytes)
            });
            string child = "QEMU_WHPX_CHILD " + Encoding.UTF8.GetString(Json(new
            {
                OwnerSha256 = ownerHash,
                ProcessId = 123,
                StartUtc = start
            }));
            string resumed = "QEMU_WHPX_RESUME " + Encoding.UTF8.GetString(Json(new
            {
                OwnerSha256 = ownerHash,
                ProcessId = mutation == "wrong-child" ? 124 : 123,
                StartUtc = start,
                SealSha256 = Digest(seal),
                ResumeSha256 = Digest(resume),
                QmpSha256 = Digest(qmpBytes)
            }));
            string stdout = child + "\r\n" + resumed + "\r\n";
            if (mutation == "missing-anchor")
                stdout = child + "\r\n";
            if (mutation == "duplicate-anchor")
                stdout += resumed + "\r\n";
            if (mutation == "reordered-anchor")
                stdout = resumed + "\r\n" + child + "\r\n";
            if (mutation == "sidecar-rewrite")
            {
                // Mutually consistent files cannot replace the earlier original stdout anchor.
                qmpBytes = [.. qmpBytes, 10];
                var node = System.Text.Json.Nodes.JsonNode.Parse(resume)!;
                node["QmpSha256"] = Digest(qmpBytes);
                resume = Encoding.UTF8.GetBytes(node.ToJsonString());
            }
            if (mutation == "duplicate-json")
                seal = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(seal).Replace("\"ProcessId\":123", "\"ProcessId\":123,\"ProcessId\":123", StringComparison.Ordinal));
            foreach (var file in new[] { ("ownership.json", owner), ("seal.json", seal), ("resume.json", resume), ("qmp.log", qmpBytes) })
                File.WriteAllBytes(Path.Combine(directory, file.Item1), file.Item2);
            if (mutation == "missing-resume")
                File.Delete(Path.Combine(directory, "resume.json"));
            var original = new ProcessResult(0, stdout, "", mutation == "timeout", false, mutation == "cancelled", TimeSpan.Zero)
            {
                StandardErrorExternallyOwned = mutation != "pipe",
                TerminationConfirmed = mutation != "unjoined"
            };
            bool accepted = false;
            try
            {
                validate.Invoke(null, [root, serial, Executable, key, ownerHash, original, mutation == "quota" ? 1 : 65536]);
                accepted = true;
            }
            catch (TargetInvocationException error) when (!expected && error.InnerException is ToolFailure or IOException or System.Text.Json.JsonException)
            {
            }
            HostCheck.Equal(expected, accepted, "copied joined-file control " + mutation);
            HostCheck.Equal(expected, File.Exists(Path.Combine(directory, "outer-final.json")), "only accepted copied controls emit a final file");
        }
        foreach (string mutation in new[] { "exact", "empty", "cancelled" })
            Check(mutation, true);
        foreach (string mutation in new[] { "late", "missing-anchor", "duplicate-anchor", "reordered-anchor", "wrong-child",
            "sidecar-rewrite", "duplicate-json", "missing-resume", "timeout", "pipe", "unjoined", "quota" })
            Check(mutation, false);
    }

    [HostTest("WHPX rejected original retains captured stdout and process facts before phase validation")]
    private static void RejectedOriginalPreserved()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string qemu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "qemu", "qemu-system-x86_64.exe");
        if (!File.Exists(qemu))
            return;
        string root = Path.Combine(Path.GetTempPath(), "clros-whpx-rejected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string serial = Path.Combine(root, "serial.log");
        var type = typeof(QemuTranscripts).Assembly.GetType("OS.Tooling.WhpxLaunchSession", true)!;
        using var owner = (IDisposable?)type.GetMethod("Create")!.Invoke(null, [root, serial, qemu, "whpx"]);
        if (owner is null)
            return; // This compatibility policy applies to one exact installed binary.
        var rejected = new ProcessResult(1, "QEMU_WHPX_CHILD incomplete-original\r\n", "", false, false, false, TimeSpan.Zero)
        {
            StandardErrorExternallyOwned = true
        };
        bool failed = false;
        try
        {
            type.GetMethod("VerifyJoined")!.Invoke(owner, [rejected, 65536]);
        }
        catch (TargetInvocationException error) when (error.InnerException is IOException or ToolFailure)
        {
            failed = true;
        }
        HostCheck.True(failed && !File.Exists(serial + ".whpx-prelaunch/outer-final.json"));
        HostCheck.Equal(rejected.StandardOutput, File.ReadAllText(serial + ".whpx-prelaunch/stdout.original.log"));
        using var facts = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(serial + ".whpx-prelaunch/process.original.json"));
        HostCheck.Equal(1, facts.RootElement.GetProperty("ExitCode").GetInt32());
        HostCheck.Equal(rejected.StandardOutput, facts.RootElement.GetProperty("StandardOutput").GetString());
    }

}
