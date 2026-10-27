using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadContract;

internal static class NativeAotLogUploadContractTests
{
    private delegate byte[] PackageBuilder(ReadOnlySpan<byte> elf, string phase, string digest);
    private delegate F.ClrosPackageValues PackageInspector(ReadOnlySpan<byte> package, string phase, string digest);
    // Exact typed bindings keep the synthetic identity seam internal to tooling.
    private static readonly PackageBuilder CreateCopiedPackage = typeof(F).GetMethod("CreateClrosPackageCore", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<PackageBuilder>();
    private static readonly PackageInspector InspectCopiedPackage = typeof(F).GetMethod("InspectClrosPackageCore", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<PackageInspector>();

    [HostTest("log upload copied CLROS packages bind explicit write verify arguments four loads TLS and runtime policy without admitting instructions")]
    private static void ClrosPackagePhases()
    {
        byte[] elf = SyntheticClrosElf(), before = elf.ToArray(); string digest = Sha(elf);
        foreach (string phase in new[] { "write", "verify" })
        {
            byte[] package = CreateCopiedPackage(elf, phase, digest);
            var value = InspectCopiedPackage(package, phase, digest);
            string manifest = "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
                "app.path=apps/NativeAotLogUploadContract\nargs=--" + phase + " --root /data\ndigest=" + digest.ToLowerInvariant() +
                "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=32\nlisten=0.0.0.0:8080\n";
            var parsed = OS.Manifest.LinuxElfPackage.Validate(package);
            HostCheck.True(parsed.Succeeded && parsed.DataFileCount == 0 && parsed.Manifest.MemoryBytes == 67_108_864 &&
                parsed.Manifest.StackBytes == 1_572_864 && parsed.Manifest.Threads == 16 && parsed.Manifest.Descriptors == 32 &&
                value.Phase == phase && value.Manifest == manifest && value.PackageSha256 == Sha(package) && value.PackageBytes == package.Length &&
                value.Arguments.SequenceEqual(new[] { "--" + phase, "--root", "/data" }) && value.Image.Entry == 4_231_031 &&
                value.Image.ProgramHeaderCount == 11 && value.Image.Loads.Count == 4 && value.Image.Tls is { FileSize: 24, MemorySize: 264, Alignment: 8 } &&
                !value.Manifest.Contains("CLROS_USER_IRQS", StringComparison.Ordinal) && !value.InstructionIndexAdmitted &&
                !value.ExecutionAuthority && !value.GrantsOriginalPublicationGuestOrPersistenceAuthority);
            HostCheck.True(package.AsSpan((int)parsed.PayloadOffset).SequenceEqual(elf) && elf.AsSpan().SequenceEqual(before) &&
                TestPackage(elf, manifest).AsSpan().SequenceEqual(package));
            Reject(() => InspectCopiedPackage(package, phase == "write" ? "verify" : "write", digest), "clros_package");
        }
    }

    [HostTest("log upload public CLROS package admission never accepts synthetic identities default unknown or differently cased phases")]
    private static void ClrosPackagePublicIdentity()
    {
        byte[] elf = SyntheticClrosElf(); string digest = Sha(elf);
        HostCheck.True(digest != F.ClrosElfSha256);
        byte[] synthetic = CreateCopiedPackage(elf, "write", digest);
        Reject(() => F.CreateClrosPackage(elf, "write"), "clros_elf_identity");
        Reject(() => F.InspectClrosPackage(synthetic, "write"), "clros_package");
        foreach (string phase in new[] { "", "Write", "Verify", "default", "recovery", "write\n", "verify\0", null! })
        {
            Reject(() => F.CreateClrosPackage(elf, phase), "phase");
            Reject(() => F.InspectClrosPackage(synthetic, phase), "phase");
        }
        Reject(() => F.CreateClrosPackage([], "write"), "clros_elf_length");
        Reject(() => F.InspectClrosPackage([], "verify"), "clros_package");
        // Matching a caller-supplied synthetic digest is explicitly only the
        // internal pure-value test seam. Public identity remains the B8E4 pin.
        elf[4096] ^= 1;
        Reject(() => CreateCopiedPackage(elf, "write", digest), "clros_elf_identity");
        Reject(() => F.CreateClrosPackage(elf, "verify"), "clros_elf_identity");
    }

    [HostTest("log upload copied CLROS geometry rejects altered valid loads TLS entry and program headers even with matching synthetic digest")]
    private static void ClrosPackageGeometry()
    {
        foreach (Action<byte[]> mutate in new Action<byte[]>[]
        {
            bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 10),
            bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), 0),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64 + 56 + 4), 7),
            bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(64 + 2 * 56 + 16), 4_198_400),
            bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(64 + 3 * 56 + 40), 189_329),
            bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(64 + 4 * 56 + 32), 25),
            bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64 + 4 * 56), 0)
        })
        {
            byte[] elf = SyntheticClrosElf(); mutate(elf);
            Reject(() => CreateCopiedPackage(elf, "write", Sha(elf)), "clros_elf_geometry");
        }
        byte[] valid = SyntheticClrosElf();
        Reject(() => CreateCopiedPackage(valid.AsSpan(1), "write", Sha(valid)), "clros_elf_length");
        Reject(() => CreateCopiedPackage([.. valid, 0], "verify", Sha(valid)), "clros_elf_length");
    }

    [HostTest("log upload copied CLROS manifests reject alternate roots permissions counts IRQ optin app paths duplicate keys and phase widening")]
    private static void ClrosPackagePolicy()
    {
        byte[] elf = SyntheticClrosElf(); string digest = Sha(elf);
        byte[] package = CreateCopiedPackage(elf, "write", digest);
        string manifest = InspectCopiedPackage(package, "write", digest).Manifest;
        foreach (string altered in new[]
        {
            manifest.Replace("--root /data", "--root /data/", StringComparison.Ordinal),
            manifest.Replace("--write --root", "--verify --root", StringComparison.Ordinal),
            manifest.Replace("--write --root", "--write --extra --root", StringComparison.Ordinal),
            manifest.Replace("apps/NativeAotLogUploadContract", "apps/m16logupload.elf", StringComparison.Ordinal),
            manifest.Replace("stack=1572864", "stack=65536", StringComparison.Ordinal),
            manifest.Replace("threads=16", "threads=2", StringComparison.Ordinal),
            manifest.Replace("descriptors=32", "descriptors=16", StringComparison.Ordinal),
            manifest.Replace("memory=67108864", "memory=134217728", StringComparison.Ordinal),
            manifest.Replace("listen=0.0.0.0:8080", "listen=127.0.0.1:8080", StringComparison.Ordinal),
            manifest + "env.CLROS_USER_IRQS=1\n",
            manifest + "args=--write --root /data\n",
            manifest.Replace("\n", "\r\n", StringComparison.Ordinal)
        })
            Reject(() => InspectCopiedPackage(TestPackage(elf, altered), "write", digest), "clros_package");
    }

    [HostTest("log upload copied CLROS package header payload bounds and bytes reject truncation extra data and foreign pins")]
    private static void ClrosPackageTamper()
    {
        byte[] elf = SyntheticClrosElf(); string digest = Sha(elf);
        byte[] package = CreateCopiedPackage(elf, "verify", digest);
        foreach (int offset in new[] { 0, 8, 12, 16, 20, 24, 32, 40, 63, 64 })
        {
            byte[] altered = package.ToArray(); altered[offset] ^= 1;
            Reject(() => InspectCopiedPackage(altered, "verify", digest), "clros_package");
        }
        Reject(() => InspectCopiedPackage(package.AsSpan(0, package.Length - 1), "verify", digest), "clros_package");
        Reject(() => InspectCopiedPackage([.. package, 0], "verify", digest), "clros_package");
        int payload = (int)BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(20)) + 64;
        foreach (int offset in new[] { 4096, 8192, F.ClrosElfBytes - 1 })
        {
            byte[] altered = package.ToArray(); altered[payload + offset] ^= 1;
            Reject(() => InspectCopiedPackage(altered, "verify", digest), "clros_elf_identity");
        }
        Reject(() => InspectCopiedPackage(package, "verify", F.ClrosElfSha256), "clros_package");
    }

    [HostTest("log upload copied CLROS reports and packages own immutable metadata without retaining or changing borrowed buffers")]
    private static void ClrosPackageCopiedValues()
    {
        byte[] elf = SyntheticClrosElf(); string digest = Sha(elf);
        byte[] package = CreateCopiedPackage(elf, "write", digest), before = package.ToArray();
        var value = InspectCopiedPackage(package, "write", digest);
        HostCheck.True(package.AsSpan().SequenceEqual(before)); elf[8192] ^= 1;
        HostCheck.True(package.AsSpan().SequenceEqual(before));
        package.AsSpan().Clear();
        HostCheck.True(value.PackageSha256 == Sha(before) && value.Image.Sha256 == digest && value.Image.Loads[1].FileSize == 893_584 &&
            value.Image.Tls!.MemorySize == 264 && value.Arguments.SequenceEqual(new[] { "--write", "--root", "/data" }));
        bool argumentsReadonly = false, loadsReadonly = false;
        try { ((IList<string>)value.Arguments)[0] = "--verify"; } catch (NotSupportedException) { argumentsReadonly = true; }
        try { ((IList<NativeAotPayload.Segment>)value.Image.Loads).Clear(); } catch (NotSupportedException) { loadsReadonly = true; }
        HostCheck.True(argumentsReadonly && loadsReadonly && !value.InstructionIndexAdmitted && !value.ExecutionAuthority);
    }

    // Independent ELF framing only: its four PT_LOAD/TLS numeric extents match
    // the retained footprint, but it is not a NativeAOT program or native input.
    private static byte[] SyntheticClrosElf()
    {
        byte[] elf = new byte[F.ClrosElfBytes]; elf[0] = 0x7f; elf[1] = (byte)'E'; elf[2] = (byte)'L'; elf[3] = (byte)'F';
        elf[4] = 2; elf[5] = 1; elf[6] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(16), 2); BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(18), 62);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(20), 1); BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(24), 4_231_031);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(32), 64); BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(52), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(54), 56); BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(56), 11);
        Segment(0, 1, 4, 0, 4_194_304, 748, 748, 4096);
        Segment(1, 1, 5, 4096, 4_198_400, 893_584, 893_584, 4096);
        Segment(2, 1, 4, 901_120, 5_095_424, 442_088, 442_088, 4096);
        Segment(3, 1, 6, 1_345_320, 5_539_624, 9616, 189_328, 4096);
        Segment(4, 7, 4, 1_345_320, 5_539_624, 24, 264, 8); elf[4096] = 0xc3;
        return elf;
        void Segment(int index, uint type, uint flags, ulong offset, ulong address, ulong file, ulong memory, ulong alignment)
        {
            Span<byte> row = elf.AsSpan(64 + index * 56, 56);
            BinaryPrimitives.WriteUInt32LittleEndian(row, type); BinaryPrimitives.WriteUInt32LittleEndian(row[4..], flags);
            BinaryPrimitives.WriteUInt64LittleEndian(row[8..], offset); BinaryPrimitives.WriteUInt64LittleEndian(row[16..], address);
            BinaryPrimitives.WriteUInt64LittleEndian(row[32..], file); BinaryPrimitives.WriteUInt64LittleEndian(row[40..], memory);
            BinaryPrimitives.WriteUInt64LittleEndian(row[48..], alignment);
        }
    }
    private static byte[] TestPackage(byte[] elf, string text)
    {
        byte[] manifest = Encoding.ASCII.GetBytes(text), package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, 0x3130464c45524c43); // Independent CLRELF01 header.
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length); return package;
    }
    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

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

    [HostTest("log upload copied transcript requires expected before work verified after readback exact phase policy and no legacy crypto fields")]
    private static void TranscriptValues()
    {
        var values = F.InspectFileValues(F.ExpectedLog(), F.ExpectedUpload());
        string Write(string phase) => "TEST_START m10.027\nLOG_UPLOAD_PHASE " + phase + $"\nLOG_UPLOAD_EXPECTED logBytes={values.LogBytes} uploadBytes={values.UploadBytes}\n" +
            "LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false") + "\n" +
            $"LOG_UPLOAD_VERIFIED logBytes={values.LogBytes} uploadBytes={values.UploadBytes}\nTEST_PASS m10.027\n";
        foreach (string phase in new[] { "write", "verify" })
        {
            string stdout = Write(phase); var transcript = F.InspectTranscriptValues(stdout, phase);
            HostCheck.True(transcript.Phase == phase && transcript.ExpectedFiles == values && !transcript.GrantsOriginalProcessOrFlushAuthority);
            HostCheck.Equal(transcript, F.InspectTranscriptValues(stdout.Replace("\n", "\r\n", StringComparison.Ordinal), phase));
            foreach (string bad in new[] { stdout + "unexpected\n", stdout[..^1], stdout.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal),
                stdout.Replace("LOG_UPLOAD_VERIFIED", "LOG_UPLOAD_FILES", StringComparison.Ordinal), stdout.Replace("buffering=0", "buffering=4096", StringComparison.Ordinal),
                stdout.Replace("logBytes=", "logBytes=0", StringComparison.Ordinal), stdout.Replace("LOG_UPLOAD_PHASE " + phase, "LOG_UPLOAD_PHASE foreign", StringComparison.Ordinal),
                stdout.Replace("LOG_UPLOAD_VERIFIED ", "LOG_UPLOAD_VERIFIED logSha256=" + values.LogSha256 + " ", StringComparison.Ordinal),
                stdout.Replace("LOG_UPLOAD_EXPECTED", "LOG_UPLOAD_VERIFIED", StringComparison.Ordinal),
                stdout.Replace("LOG_UPLOAD_VERIFIED", "LOG_UPLOAD_EXPECTED", StringComparison.Ordinal),
                stdout.Replace($"LOG_UPLOAD_VERIFIED logBytes={values.LogBytes} uploadBytes={values.UploadBytes}\n", "", StringComparison.Ordinal),
                stdout.Replace($"LOG_UPLOAD_EXPECTED logBytes={values.LogBytes} uploadBytes={values.UploadBytes}\n", "", StringComparison.Ordinal) })
                Reject(() => F.InspectTranscriptValues(bad, phase), "transcript");
            string legacy = "TEST_START m10.027\nLOG_UPLOAD_PHASE " + phase + "\n" +
                "LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false") + "\n" +
                $"LOG_UPLOAD_FILES logBytes={values.LogBytes} logSha256={values.LogSha256} uploadBytes={values.UploadBytes} uploadSha256={values.UploadSha256}\nTEST_PASS m10.027\n";
            Reject(() => F.InspectTranscriptValues(legacy, phase), "transcript");
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
        string program = File.ReadAllText(Path.Combine(root, "apps", F.Application, "Program.cs"));
        HostCheck.True(project.Contains("<LangVersion>14.0</LangVersion>", StringComparison.Ordinal) &&
            project.Contains("<TargetFramework>net11.0</TargetFramework>", StringComparison.Ordinal) &&
            !project.Contains("PackageReference", StringComparison.Ordinal) && !project.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) &&
            !program.Contains("Security.Cryptography", StringComparison.Ordinal) && !program.Contains("HashData", StringComparison.Ordinal) &&
            !program.Contains("LOG_UPLOAD_FILES", StringComparison.Ordinal));
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
        HostCheck.True(value.Files == 2 && value.ExitedFiles == 2 && value.UnjoinedRows == 2 && value.UnreturnedRows == 2 &&
            value.TerminalUnreturnedRows == 1 && value.NonterminalUnreturnedRows == 1 &&
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

    [HostTest("log upload complete unknown result syntax preserves read epoll futex and terminal exit evidence without closing projection")]
    private static void LinuxUnknownResults()
    {
        const string trace = """
            1792919189.087207 read(3<pipe:[20592]> <unfinished ...>) = ?
            1792919190.669175 epoll_pwait(5<anon_inode:[eventpoll]> <unfinished ...>) = ?
            1792919190.685000 futex(0x7f00, FUTEX_WAIT_PRIVATE, 2, NULL) = ?
            1792919190.657872 exit(0)               = ?
            1792919190.685288 exit_group(0)         = ?
            1792919190.697636 +++ exited with 0 +++
            """;
        var value = F.InspectTraceValues(new Dictionary<string, string> { ["write-trace.7"] = trace + "\n" }, "write");
        HostCheck.True(value.Rows.Select(row => row.Raw).SequenceEqual(trace.Split('\n')) &&
            value.Rows.Select(row => row.Line).SequenceEqual(Enumerable.Range(1, 6)) &&
            value.Rows.All(row => row.Source == "write-trace.7"));
        HostCheck.True(value.Files == 1 && value.ExitedFiles == 1 && value.UnjoinedRows == 0 && value.UnclassifiedRows == 0 &&
            value.UnreturnedRows == 5 && value.TerminalUnreturnedRows == 2 && value.NonterminalUnreturnedRows == 3 &&
            !value.CompleteTraceProjection && !value.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
        HostCheck.True(value.Rows.Take(5).All(row => row.Kind == F.TraceKind.UnreturnedCall && row.Result == "?") &&
            value.Rows[0].Syscall == "read" && value.Rows[0].Arguments == "3<pipe:[20592]> <unfinished ...>" &&
            value.Rows[1].Syscall == "epoll_pwait" && value.Rows[1].Arguments == "5<anon_inode:[eventpoll]> <unfinished ...>" &&
            value.Rows[2].Syscall == "futex" && value.Rows[2].Arguments == "0x7f00, FUTEX_WAIT_PRIVATE, 2, NULL" &&
            value.Rows[3].Arguments == "0" && value.Rows[4].Arguments == "0");
        string terminalOnly = "1.000001 exit_group(0) = ?\n1.000002 +++ exited with 0 +++\n";
        var terminal = F.InspectTraceValues(new Dictionary<string, string> { ["verify-trace.9"] = terminalOnly }, "verify");
        HostCheck.True(terminal.TerminalUnreturnedRows == 1 && terminal.NonterminalUnreturnedRows == 0 &&
            terminal.UnreturnedRows == 1 && !terminal.CompleteTraceProjection && !terminal.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
    }

    [HostTest("log upload literal marker arguments remain complete calls while real cross PID fragments remain unjoined")]
    private static void LinuxLiteralMarkersAndFragments()
    {
        const string literals = """
            1.000001 write(3, "<unfinished ...>", 16) = 16
            1.000002 write(3, "<... read resumed>", 18) = -1 EIO (Input/output error)
            1.000003 +++ exited with 0 +++
            """;
        var complete = F.InspectTraceValues(new Dictionary<string, string> { ["write-trace.7"] = literals + "\n" }, "write");
        HostCheck.True(complete.CompleteTraceProjection && complete.UnjoinedRows == 0 && complete.UnreturnedRows == 0 &&
            complete.Rows.Take(2).All(row => row.Kind == F.TraceKind.CompletedCall && row.Syscall == "write") &&
            complete.Rows[0].Arguments == "3, \"<unfinished ...>\", 16" && complete.Rows[0].Result == "16" &&
            complete.Rows[1].Arguments == "3, \"<... read resumed>\", 18" && complete.Rows[1].Result == "-1 EIO (Input/output error)" &&
            complete.Rows.Select(row => row.Raw).SequenceEqual(literals.Split('\n')) && !complete.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
        var split = F.InspectTraceValues(new Dictionary<string, string>
        {
            ["write-trace.7"] = "1.000001 read(3, <unfinished ...>\n1.000003 +++ exited with 0 +++\n",
            ["write-trace.8"] = "1.000002 <... read resumed>\"x\", 1) = 1\n1.000004 +++ exited with 0 +++\n"
        }, "write");
        HostCheck.True(split.ExitedFiles == 2 && split.UnjoinedRows == 2 && split.UnreturnedRows == 0 &&
            split.Rows[0].Kind == F.TraceKind.UnfinishedCall && split.Rows[0].Result == "" &&
            split.Rows[2].Kind == F.TraceKind.ResumedCall && split.Rows[2].Syscall == "" && split.Rows[2].Result == "" &&
            !split.CompleteTraceProjection && !split.GrantsSyscallClosureLockFlushOrPersistenceAuthority);
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
        HostCheck.True(!value.GrantsLinuxNativeAotGuestOrPersistenceAuthority && value.Write.Phase == "write" && value.Verify.Phase == "verify" && value.Write.ExpectedFiles == value.Verify.ExpectedFiles);
        byte[] log = File.ReadAllBytes(Path.Combine(output, "data", F.LogName)), upload = File.ReadAllBytes(Path.Combine(output, "data", F.UploadName));
        HostCheck.Equal(value.Write.ExpectedFiles, F.InspectFileValues(log, upload));
        using (var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "receipt.json"))))
            HostCheck.True(receipt.RootElement.GetProperty("Schema").GetString() == "CLROS_LOG_UPLOAD_MANAGED_REFERENCE_V2" &&
                receipt.RootElement.GetProperty("Write").TryGetProperty("ExpectedFiles", out _) &&
                !receipt.RootElement.GetProperty("Write").TryGetProperty("Files", out _));
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
