using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#if ENTROPY_CAPTURE_HOST
using OS.Boot;
using Core = OS.Boot.EfiEntry.FirmwareEntropyCapture;
using Reservoir = OS.Boot.EfiEntry.FirmwareEntropyReservoir;

#else
internal static partial class M03FirmwareEntropySeedEfiIntegrationTests
{
    private const string Flag = "EnableM03FirmwareEntropySeedProbe";
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static string Root() => HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag)
    {
        string path = Path.Combine(Root(), "artifacts/host-tests/m03-entropy-" + tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Result Process(string directory, string file, params string[] args)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        DateTime begin = DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new
        {
            File = file,
            Arguments = args,
            WorkingDirectory = Root(),
            StartedUtc = begin
        }));
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        bool timeout = !process.WaitForExit(900000);
        if (timeout)
        {
            process.Kill(true);
            process.WaitForExit();
        }

        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
        {
            NativeExit = process.ExitCode,
            TimedOut = timeout,
            StartedUtc = begin,
            EndedUtc = DateTime.UtcNow
        }));
        return new(process.ExitCode, timeout, output, error);
    }

    private static Result Tool(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, false, result.Output, result.Error);
    }

    private static void Passed(Result r) => Need(r.Exit == 0 && !r.Timeout && r.Error.Length == 0, r.Output + r.Error);
    [HostTest("m03.003.efi conditional source and original firmware authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = Root();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" && (string?)defaults[0].Attribute("Condition") == "'$(" + Flag + ")' == ''", "one false default");
        var define = project.Descendants("DefineConstants").Single(x => x.Value.Contains("M03_FIRMWARE_ENTROPY_SEED_PROBE", StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition") == "'$(" + Flag + ")' == 'true'" && define.Value == "$(DefineConstants);M03_FIRMWARE_ENTROPY_SEED_PROBE", "exact enabled symbol");
        var source = EntropySources();
        var admission = OS.Tooling.EfiProjectAdmission.InspectStatic(root);
        Need(admission.Succeeded, admission.Reason + " " + admission.Detail);
        Need(source.Type("OS.Kernel.Diagnostics.Probes").GetMembers("EnableFirmwareEntropySeedProbe").Single()
            is Microsoft.CodeAnalysis.IFieldSymbol
        {
            IsStatic: true, IsConst: true, ConstantValue: false,
            Type.SpecialType: Microsoft.CodeAnalysis.SpecialType.System_Boolean,
            DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public
        }, "disabled compile-time authority");
        string[] owners = ["OS.Boot.EfiEntry+FirmwareEntropyCapture", "OS.Boot.EfiEntry+FirmwareEntropyReservoir"];
        string core = HostSourceText.Documents(source, owners);
        string code = HostSourceText.Declarations(source, owners);
        var calls = HostSourceOperations.CallsTo(source, "OS.Boot.EfiEntry+FirmwareEntropyCapture", "Run");
        Need(calls.Length == 1, "one original entropy entry call");
        var entryMethod = calls.Single().Ancestors()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().First();
        Need(source.Type("OS.Boot.EfiEntry").DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree == entryMethod.SyntaxTree && reference.Span == entryMethod.Parent!.Span),
            "entropy caller remains owned by the EFI entry type");
        _ = source.MethodInvocations(entryMethod);
        string entry = HostSourceText.Active(entryMethod);
        foreach (string forbidden in new[]
        {
            "new byte[",
            "stackalloc",
            "throw ",
            "new object",
            "string ",
            "GetHash",
            "Console.",
            "AllocatePool(",
            "AllocatePages("
        }

        )
            Need(!core.Contains(forbidden, StringComparison.Ordinal), "finite native core " + forbidden);
        Need(HostSourceText.Contains(code, "s->Locate(&guid, null, (void**)&s->Rng)") && HostSourceText.Contains(code, "get(rng, null, (nuint)RequestedBytes, s->Scratch->Data)"), "original pointer call shapes");
        Need(HostSourceText.Contains(code, "WriteUnsignedDecimal(console, serial, ClearedBytes(s))") && !core.Contains("Zero(scratch->Data, 0, 32) ?", StringComparison.Ordinal), "no diagnostic over uncleared provider bytes");
        Need(HostSourceText.Contains(code, "MethodImplOptions.NoInlining") && HostSourceText.Contains(code, "s->PublishedBytes = RequestedBytes; s->Phase = 3"), "private real loads and publication last");
        Need(HostSourceText.Contains(entry, "if (!FirmwareEntropyCapture.Run(systemTable, console, serial))") && HostSourceText.Contains(entry, "while (true) { }"), "failed guest terminal leaves success path intact");
        Need(EntropyHostOracleMatches(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj")), "reviewed complete host oracle family");
    }

    [HostTest("m03.003.efi actual numeric core mapped ABI guards and private retention", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root = Root(), directory = Scratch("actual-core");
        string[] sources = ["src/OS/Boot/EfiEntry.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.cs",
            "src/OS/Boot/EfiEntry.EfiTableHeader.cs",
            "src/OS/Boot/EfiEntry.EfiSimpleTextOutputProtocol.cs",
            "src/OS/Boot/EfiEntry.EfiBootServices.cs",
            "src/OS/Boot/EfiEntry.EfiSystemTable.cs",
            "src/OS/Boot/EfiEntry.EfiMemoryMapSnapshot.cs",
            "src/OS/Boot/EfiEntry.OwnedMemoryMapBuffer.cs",
            "src/OS/Boot/EfiEntry.OwnedMemoryRegionBuffer.cs",
            "src/OS/Boot/EfiEntry.BootPageAllocatorNodeBuffer.cs",
            "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessStatus.cs",
            "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessResult.cs",
            "src/OS/Boot/EfiEntry.EfiGuid.cs",
            "src/OS/Boot/EfiEntry.EfiSerialIoProtocol.cs",
            "src/OS/Boot/EfiEntry.EfiConsoleCharacter.cs",
            "src/OS/Boot/EfiEntry.M04ManagedHeapAdapter.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.Storage.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.State.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Buffer.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Protocol.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.State.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.EfiLoadedImageProtocol.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneMemoryMapBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAllocationRecordBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.AcpiBootstrapWorkspace.cs",
            "src/OS/Boot/EfiEntry.AcpiBootstrapOwnedBuffer.cs",
            "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.cs",
            "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.Authority.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.CooperativeControl.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.TimerControl.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxLoadedImage.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFileSystem.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFile.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.AdapterHandles.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Observation.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Storage.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Snapshot.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Attempt.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Case.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Snapshot.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAllocationRecordBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeFixedRequirementBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRequirementBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRangeBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageImageBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageEntryBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeBindingBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeSemanticBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeStateSnapshotBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePlannerStackBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePreparationCallContext.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M02OwnedX64GdtEncodingStackWorkspace.cs",
            "src/OS/Boot/EfiEntry.M02PageFaultAdapter.cs",
            "src/OS/Boot/EfiEntry.M02DoubleFaultIstAdapter.cs",
            "src/OS/Boot/EfiEntry.M02IdtrContinuedExecutionStackWorkspace.cs",
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.cs",
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.Observation.cs", "src/OS/Boot/EfiConsoleAdapter.cs", "tests/OS.HostTests/M03FirmwareEntropySeedEfiIntegrationTests.cs",
            "tests/OS.HostTests/M03FirmwareEntropySeedEfiIntegrationTests.Result.cs",
            "tests/OS.HostTests/M03FirmwareEntropySeedEfiIntegrationTests.FirmwareEntropyHost.cs"];
        string attribute = "        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root, "src/OS/Runtime/RuntimeExportAttribute.cs")).Replace("\r\n", "\n", StringComparison.Ordinal).Contains(attribute, StringComparison.Ordinal), "unchanged host attribute projection");
        File.WriteAllText(Path.Combine(directory, "Attribute.cs"), "namespace System.Runtime {\n" + attribute + "\n}");
        string items = string.Join('\n', sources.Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string project = Path.Combine(directory, "Capture.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>FirmwareEntropyHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ENTROPY_CAPTURE_HOST;M03_FIRMWARE_ENTROPY_SEED_PROBE</DefineConstants></PropertyGroup><ItemGroup>" + items + "<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        string[] inputs = [.. sources.Select(x => Path.Combine(root, x)), Path.Combine(directory, "Attribute.cs"), project];
        var identities = inputs.Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "inputs.json"), JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory, "build"), "dotnet", "build", project, "-c", "Release", "--nologo", "-m:1", "--disable-build-servers", "-p:UseSharedCompilation=false"));
        Result run = Process(Path.Combine(directory, "run"), "dotnet", Path.Combine(directory, "bin/Release/net11.0/Capture.dll"));
        Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.entropy.host.tests", StringComparison.Ordinal) && run.Output.Split('\n').Count(x => x.StartsWith("TEST_START m03.entropy.host ", StringComparison.Ordinal)) == 8, "complete eight-group actual core child");
        foreach (var input in identities)
            Need(Hash(input.Path) == input.Sha256, "unchanged child input " + input.Path);
    }

    [HostTest("m03.003.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string directory = Scratch("transcript");
        byte[] good = FirmwareTranscriptControls.Bytes(ProtocolLines);
        var control = new FirmwareTranscriptControls(directory, path => OS.Tooling.QemuTranscripts.AssertM03FirmwareEntropyTranscript(path));
        control.Check(good, true);
        for (int i = 0; i < ProtocolLines.Length; i++)
        {
            foreach (string replacement in new[]
            {
                "",
                ProtocolLines[i] + " ",
                " " + ProtocolLines[i],
                ProtocolLines[i] + " extra=1",
                ProtocolLines[i] + "\0"
            }

            )
            {
                string[] copy = [.. ProtocolLines];
                copy[i] = replacement;
                control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            }

            string[] lower = [.. ProtocolLines];
            lower[i] = lower[i].ToLowerInvariant();
            if (lower[i] != ProtocolLines[i])
                control.Check(FirmwareTranscriptControls.Bytes(lower), false);
            control.Check(FirmwareTranscriptControls.Bytes(ProtocolLines.Take(i + 1).Concat(ProtocolLines.Skip(i))), false);
        }

        foreach (var pair in new (string Old, string New)[]
        {
            ("requested=32", "requested=31"),
            ("bytes=32", "bytes=31"),
            ("scratchCleared=32", "scratchCleared=0"),
            ("guards=64", "guards=63"),
            ("tails=64", "tails=63"),
            ("published=1", "published=0"),
            ("status=0 calls=1 found=1", "status=9223372036854775815 calls=1 found=1"),
            ("regions=4", "regions=3"),
            ("freeBytes=81920", "freeBytes=81921")
        }

        )
        {
            string text = Encoding.ASCII.GetString(good), changed = text.Replace(pair.Old, pair.New);
            Need(text != changed, "mutation changed input");
            control.Check(Encoding.ASCII.GetBytes(changed), false);
        }

        string[] swap = [.. ProtocolLines];
        swap[15] = ProtocolLines[14];
        control.Check(FirmwareTranscriptControls.Bytes(swap), false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n", "\n")), false);
        control.Check(good[..^1], false);
        control.Check([.. good, 0], false);
        control.Check([.. good, 255], false);
        control.Check(good, false, [255]);
        control.Check(good, false, Encoding.ASCII.GetBytes("arbitrary"));
        control.Check(good, false, post: [.. FirmwareTranscriptControls.Suffix, 65]);
        control.Check(good, true, post: FirmwareTranscriptControls.ShortSuffix);
        control.Check(good, false, post: [.. FirmwareTranscriptControls.ShortSuffix, 65]);
        control.Check(good, false, post: []);
        foreach (byte[] suffix in new[]
        {
            FirmwareTranscriptControls.Suffix,
            FirmwareTranscriptControls.ShortSuffix
        }

        )
        {
            foreach (int index in new[]
            {
                0,
                suffix.Length - 1
            }

            )
            {
                byte[] changed = [.. suffix];
                changed[index] ^= 1;
                control.Check(good, false, post: changed);
            }

            control.Check(good, false, post: [.. suffix, 65]);
            control.Check(good, false, post: [.. suffix, 13, 10]);
            control.Check(good, false, post: suffix[..^1]);
        }

        int firstCr = Array.IndexOf(FirmwareTranscriptControls.ShortSuffix, (byte)13);
        foreach (int length in new[]
        {
            1,
            firstCr,
            firstCr + 2,
            FirmwareTranscriptControls.ShortSuffix.Length - 1,
            FirmwareTranscriptControls.ShortSuffix.Length + 1,
            FirmwareTranscriptControls.Suffix.Length - 1
        }

        )
            control.Check(good, false, post: FirmwareTranscriptControls.Suffix[..length]);
        string[] missing = [.. ProtocolLines[..14], "M03_ENTROPY_DISCOVERY status=9223372036854775822 calls=1 found=0", "M03_ENTROPY_ACQUIRE status=0 calls=0 requested=32", "M03_ENTROPY_RETAIN bytes=0 scratchCleared=0 guards=64 tails=64 published=0", "M03_ENTROPY_UNAVAILABLE retainedUnchanged=1 published=0 rngCalls=0", "TEST_FAIL m03.003.efi source_unavailable"];
        var absent = new FirmwareTranscriptControls(directory, path => OS.Tooling.QemuTranscripts.AssertM03FirmwareEntropyTranscript(path, true));
        void Missing(byte[] bytes, bool expected) => absent.Check(bytes, expected, post: []);
        Missing(FirmwareTranscriptControls.Bytes(missing), true);
        for (int i = 0; i < missing.Length; i++)
        {
            string[] changed = [.. missing];
            changed[i] += " extra=1";
            Missing(FirmwareTranscriptControls.Bytes(changed), false);
            changed = [.. missing];
            changed[i] = "";
            Missing(FirmwareTranscriptControls.Bytes(changed), false);
        }

        foreach (var pair in new[]
        {
            ("rngCalls=0", "rngCalls=1"),
            ("scratchCleared=0", "scratchCleared=32"),
            ("retainedUnchanged=1", "retainedUnchanged=0"),
            ("9223372036854775822", "9223372036854775815"),
            ("published=0", "published=1")
        }

        )
            Missing(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(FirmwareTranscriptControls.Bytes(missing)).Replace(pair.Item1, pair.Item2)), false);
        Missing([.. FirmwareTranscriptControls.Bytes(missing), 65], false);
        Missing([.. FirmwareTranscriptControls.Bytes(missing), .. FirmwareTranscriptControls.Suffix], false);
        control.Check(good, true);
        string[] ordered = ProtocolLines.Select(line => line.StartsWith("UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", StringComparison.Ordinal) || line.StartsWith("BOOT_MEMORY_REGION_COUNT ", StringComparison.Ordinal) || line.StartsWith("BOOT_PAGE_ALLOCATOR_READY ", StringComparison.Ordinal) || line.StartsWith("M03_ENTROPY_", StringComparison.Ordinal) ? line.Split(' ')[0] : line).ToArray();
        var runner = new StandardFirmwareRunnerControls(directory, ProtocolLines, ordered, ["OS_SMOKE_FAIL", "TEST_FAIL"], Flag, true);
        runner.CompleteMatrix();
        Result dry = Tool("qemu", "smoke", "-BootPolicy", "Diagnostic", "-BuildEfi", "-" + Flag, "-FirmwareRngSource", "virtio", "-DryRun");
        Passed(dry);
        string[] Vector(string prefix) => dry.Output.Split('\n').Single(x => x.StartsWith(prefix + " ", StringComparison.Ordinal))[(prefix.Length + 1)..].TrimEnd('\r').Split(';').Select(x => x.Trim()).ToArray();
        foreach (bool shortened in new[]
        {
            false,
            true
        }

        )
        {
            byte[] framed = runner.Frame(shortened);
            File.WriteAllBytes(Path.Combine(directory, shortened ? "short-framed.serial.bin" : "full-framed.serial.bin"), framed);
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(Vector("REQUIRED_MARKERS"), Vector("FORBIDDEN_MARKERS"), orderedMarkers: Vector("ORDERED_MARKERS"), uniqueMarkers: Vector("UNIQUE_MARKERS"));
            Need(state.Feed(framed).Succeeded, "complete framed Feed");
            var actual = state.Complete();
            Need(actual.Passed && actual.TotalBytes == framed.Length && actual.LinesProcessed == ProtocolLines.Length + (shortened ? 4 : 5) && actual.InvalidByteCount == (shortened ? 15 : 207) && actual.InvalidNoiseLineCount == (shortened ? 1 : 2) && actual.OrderedSeenCount == ProtocolLines.Length && actual.UniqueSeenCount == ProtocolLines.Length, "complete framed accounting");
        }

        Console.WriteLine("TRANSCRIPT_CONTROLS m03.entropy cases=" + (control.Cases + runner.Cases) + " stderrWhitespaceRejected=true");
    }

    private static readonly string[] ProtocolLines = ["TEST_START efi.main", "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 4", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "TEST_START m03.003.efi", "M03_ENTROPY_DISCOVERY status=0 calls=1 found=1", "M03_ENTROPY_ACQUIRE status=0 calls=1 requested=32", "M03_ENTROPY_RETAIN bytes=32 scratchCleared=32 guards=64 tails=64 published=1", "CLROS_M03_003_READY bytes=32", "TEST_PASS m03.003.efi", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
    [HostTest("m03.003.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root = Root(), directory = Scratch("profile"), script = Path.Combine(root, "tools/OS.Tooling/QemuProfiles.cs"), before = Hash(script);
        Result Dry(params string[] args) => Tool(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", .. args]);
        string[] positive = ["-BuildEfi", "-" + Flag, "-FirmwareRngSource", "virtio"];
        Result result = Dry(positive);
        Passed(result);
        Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal), "actual dry-run terminal");
        string[] Vector(string prefix) => result.Output.Split('\n').Single(x => x.StartsWith(prefix + " ", StringComparison.Ordinal))[(prefix.Length + 1)..].TrimEnd('\r').Split(';').Select(x => x.Trim()).ToArray();
        var required = Vector("REQUIRED_MARKERS");
        var ordered = Vector("ORDERED_MARKERS");
        var unique = Vector("UNIQUE_MARKERS");
        var forbidden = Vector("FORBIDDEN_MARKERS");
        foreach (var vector in new[]
        {
            required,
            ordered,
            unique
        }

        )
            Need(vector.Length == ProtocolLines.Length && vector.Distinct(StringComparer.Ordinal).Count() == ProtocolLines.Length, "complete canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal), StringComparer.Ordinal) && ordered.SequenceEqual(unique, StringComparer.Ordinal), "actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines)
        {
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", lines) + "\r\n")).Succeeded, "actual streamer Feed");
            return state.Complete();
        }

        var accepted = Stream(ProtocolLines);
        Need(accepted.Passed && accepted.LinesProcessed == ProtocolLines.Length && accepted.OrderedSeenCount == ProtocolLines.Length && accepted.UniqueSeenCount == ProtocolLines.Length, "genuine full-token streamer success");
        foreach (int index in new[]
        {
            14,
            15,
            16,
            17
        }

        )
        {
            var missing = Stream(ProtocolLines.Where((_, i) => i != index).ToArray());
            Need(!missing.Passed && missing.MissingRequiredMarkers!.Count > 0, "missing entropy row");
            var duplicate = Stream(ProtocolLines.Take(index + 1).Concat(ProtocolLines.Skip(index)).ToArray());
            Need(!duplicate.Passed && duplicate.DuplicateUniqueOccurrenceCount == 1, "duplicate entropy row");
            string[] swapped = [.. ProtocolLines];
            (swapped[index], swapped[index + 1]) = (swapped[index + 1], swapped[index]);
            Need(Stream(swapped).OutOfOrderOccurrenceCount > 0, "reordered entropy row");
        }

        void Reject(string[] args, string reason)
        {
            Result failure = Dry(args);
            Need(failure.Exit != 0 && !failure.Timeout && (failure.Output + failure.Error).Contains(reason, StringComparison.Ordinal), "profile rejection " + reason);
        }

        string project = File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj"));
        string[] flags = Regex.Matches(project, @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "complete profile flags");
        foreach (string other in flags.Where(x => x != Flag))
            Reject([.. positive, "-" + other], EfiProfileTestControls.MissingParentReason([Flag, other], ordinarySmoke: true) ?? (other == "EnableM03FirstRealExitBootServicesProbe" ? "m03_first_exit_requires property=EnableM01LoadedImageStackProbe" : other == "EnableM04GcStaticRootMaterializationProbe" ? "m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe" : other == "EnableM03TypedExitBootServicesCallProbe" ? "m03_typed_ebs_profile_conflict property=EnableM03FirmwareEntropySeedProbe" : other == "EnableM04TypeManagerModuleRegistrationProbe" ? "m04_type_manager_requires_original_loaded_image" : "m03_firmware_entropy_profile_conflict property=" + other));
        foreach (string value in new[]
        {
            "VIRTIO",
            "Missing",
            "unknown"
        }

        )
            Reject(["-BuildEfi", "-" + Flag, "-FirmwareRngSource", value], "m03_firmware_entropy_requires_exact_source");
        Reject(["-FirmwareRngSource", "virtio"], "firmware_rng_source_requires_entropy_profile");
        Reject(["-FirmwareRngSource", ""], "firmware_rng_source_requires_entropy_profile");
        Reject(["-BuildEfi", "-" + Flag], "m03_firmware_entropy_requires_exact_source");
        Reject(["-" + Flag, "-FirmwareRngSource", "virtio"], "m03_firmware_entropy_requires_build_and_original_capture");
        Reject([.. positive, "-SkipImageBuild"], "m03_firmware_entropy_requires_build_and_original_capture");
        Reject([.. positive, "-MarkerPreset", "acpi"], "m03_firmware_entropy_forbids_override");
        Passed(Dry("-BuildEfi", "-" + Flag, "-FirmwareRngSource", "missing"));
        Need(Hash(script) == before, "unchanged profile input");
    }

    [HostTest("m03.003.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root = Root();
        foreach (bool enabled in new[]
        {
            false,
            true
        }

        )
        {
            string directory = Scratch(enabled ? "enabled" : "default"), artifact = Path.Combine(root, "artifacts/efi/issue541-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result result = Tool(["efi", "build", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), .. (enabled ? new[] { "-EnableM03FirmwareEntropySeedProbe" } : Array.Empty<string>())]);
            Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), "actual producer/verifier");
            foreach (string suffix in new[]
            {
                "",
                ".link.json",
                ".link.map",
                ".link.rsp",
                ".serializer.obj"
            }

            )
                File.Copy(artifact + suffix, Path.Combine(directory, "image.EFI" + suffix));
            foreach (var pair in new[]
            {
                (OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"), "OS.dll"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.obj", "OS.obj"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml", "OS.map.xml")
            }

            )
                File.Copy(Path.Combine(root, pair.Item1), Path.Combine(directory, pair.Item2));
            using var json = JsonDocument.Parse(File.ReadAllText(artifact + ".link.json"));
            var evaluation = json.RootElement.GetProperty("postRestoreEvaluation");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create(null, new Dictionary<string, bool> { [Flag] = enabled }));
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
                Need(string.Equals(Hash(Path.Combine(root, input.GetProperty("path").GetString()!)), input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual raw source identity");
            foreach (var property in evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)))
                Need(property.Value.GetString() == (enabled && property.Name == Flag ? "true" : "false"), "actual profile " + property.Name);
            using var stream = File.OpenRead(Path.Combine(directory, "OS.dll"));
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Need(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
            var types = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t => metadata.GetString(t.Name) == "FirmwareEntropyCapture").ToArray();
            Need(types.Length == (enabled ? 1 : 0), "conditional entropy type absent/default and present/enabled");
            if (enabled)
                foreach (string name in new[]
                {
                    "Begin",
                    "Acquire",
                    "Retain",
                    "ReadByte",
                    "ClearedBytes",
                    "Run"
                }

                )
                {
                    var methods = types[0].GetMethods().Where(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == name).ToArray();
                    Need(methods.Length == 1 && metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress != 0, "actual entropy method " + name);
                }

            string sourceDirectory = Path.Combine(directory, "source-inputs");
            Directory.CreateDirectory(sourceDirectory);
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
            {
                string relative = input.GetProperty("path").GetString()!, destination = Path.Combine(sourceDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(root, relative), destination);
            }

            File.WriteAllText(Path.Combine(directory, "artifact-identities.json"), JsonSerializer.Serialize(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).ToArray()));
        }
    }
}
#endif
