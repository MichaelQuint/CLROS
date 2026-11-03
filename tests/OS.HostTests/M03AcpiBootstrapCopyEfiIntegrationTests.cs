using System.Diagnostics;
using System.Reflection;
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

#if ACPI_BOOTSTRAP_HOST
using OS.Boot;














































































































































































































#else
internal static partial class M03AcpiBootstrapCopyEfiIntegrationTests
{
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static string Root()
    {
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "CLROS.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag)
    {
        string path = Path.Combine(Root(), "artifacts/host-tests/m03-acpi-" + tag + "-" + Guid.NewGuid().ToString("N"));
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
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new { File = file, Arguments = args, WorkingDirectory = Root(), StartedUtc = begin }));
        using var p = System.Diagnostics.Process.Start(start)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        bool timeout = !p.WaitForExit(900000);
        if (timeout)
        {
            p.Kill(true);
            p.WaitForExit();
        }

        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { NativeExit = p.ExitCode, TimedOut = timeout, StartedUtc = begin, EndedUtc = DateTime.UtcNow }));
        return new(p.ExitCode, timeout, output, error);
    }

    private static Result Tool(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, false, result.Output, result.Error);
    }

    private static void Passed(Result result) => Need(result.Exit == 0 && !result.Timeout && result.Error.Length == 0, result.Output + result.Error);
    [HostTest("m03.002.efi conditional source and unchanged profile", HostLane.Native, 0)]
    private static void TestSource()
    {
        string root = Root();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        Need(!project.Descendants("EnableAcpiPhysicalDiscoveryProbe").Any(), "existing property unset by default");
        var symbol = project.Descendants("DefineConstants").Single(x => x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE", StringComparison.Ordinal));
        Need(string.Equals((string? )symbol.Parent?.Attribute("Condition"), "'$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'", StringComparison.Ordinal), "existing symbol requires explicit true");
        Need(project.Descendants("DefineConstants").Count(x => x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE", StringComparison.Ordinal)) == 1, "one existing symbol");
        Need(string.Equals(Hash(Path.Combine(root, "src/OS.Kernel/Boot/RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.cs")), OS.Tooling.EfiContracts.AcpiBootstrapHostOracleSha256, StringComparison.OrdinalIgnoreCase), "frozen host oracle");
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs"));
        string core = source[source.IndexOf("    private static bool WriteAcpiPhysicalDiscoveryProbe(", StringComparison.Ordinal)..source.IndexOf("    private static bool TryParseMcfgSegments(", StringComparison.Ordinal)];
        foreach (string forbidden in new[]
        {
            "new byte[",
            "stackalloc",
            "throw ",
            "AllocatePool(",
            "AllocatePages(",
            "ExitBootServices(",
            "ReadOnlySpan",
            "Console."
        }

        )
            Need(!core.Contains(forbidden, StringComparison.Ordinal), "finite EFI core " + forbidden);
        Need(core.IndexOf("if (!AcpiFinalAdmission(", StringComparison.Ordinal) < core.IndexOf("owned[i] =", StringComparison.Ordinal), "all admission before destination writes");
        TestScriptProfile();
    }

    [HostTest("m03.002.efi actual five-copy EFI methods and independent host parity", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root = Root(), directory = Scratch("actual-methods");
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
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.Observation.cs", "src/OS/Boot/EfiConsoleAdapter.cs", "tests/OS.HostTests/M03AcpiBootstrapCopyEfiIntegrationTests.cs",
            "tests/OS.HostTests/M03AcpiBootstrapCopyEfiIntegrationTests.Result.cs",
            "tests/OS.HostTests/M03AcpiBootstrapCopyEfiIntegrationTests.AcpiBootstrapHost.cs"];
        string attribute = "        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root, "src/OS/Runtime/RuntimeExportAttribute.cs")).Replace("\r\n", "\n", StringComparison.Ordinal).Contains(attribute, StringComparison.Ordinal), "unchanged ordinary-host attribute projection");
        File.WriteAllText(Path.Combine(directory, "Attribute.cs"), "namespace System.Runtime {\n" + attribute + "\n}");
        string items = string.Join('\n', sources.Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string project = Path.Combine(directory, "Capture.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>AcpiBootstrapHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ACPI_BOOTSTRAP_HOST;ACPI_PHYSICAL_DISCOVERY_PROBE</DefineConstants></PropertyGroup><ItemGroup>" + items + "<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        string[] inputs = [..sources.Select(x => Path.Combine(root, x)), Path.Combine(directory, "Attribute.cs"), project];
        var identities = inputs.Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "inputs.json"), JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory, "build"), "dotnet", "build", project, "-c", "Release", "--nologo", "-m:1", "--disable-build-servers", "-p:UseSharedCompilation=false"));
        Result run = Process(Path.Combine(directory, "run"), "dotnet", Path.Combine(directory, "bin/Release/net11.0/Capture.dll"));
        Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.acpi.host.tests", StringComparison.Ordinal) && run.Output.Split('\n').Count(x => x.StartsWith("TEST_START m03.acpi.host ", StringComparison.Ordinal)) == 5, "complete actual child");
        foreach (var input in identities)
            Need(Hash(input.Path) == input.Sha256, "unchanged input " + input.Path);
    }

    [HostTest("m03.002.efi literal transcript and complete finite fields", HostLane.Fast, 2)]
    private static void TestLiterals()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "src/OS/Boot/EfiEntry.cs"));
        string formatter = source[source.IndexOf("    private static void WriteAcpiBootstrapTranscript(", StringComparison.Ordinal)..source.IndexOf("    private static bool TryParseMcfgSegments(", StringComparison.Ordinal)];
        var literals = Regex.Matches(formatter, @"WriteAcpiBootstrapAscii\(console, serial, 0x([0-9A-F]{16}), ([1-8])\);");
        var decoded = new StringBuilder();
        foreach (Match m in literals)
        {
            ulong value = Convert.ToUInt64(m.Groups[1].Value, 16);
            for (int i = 0; i < int.Parse(m.Groups[2].Value); i++)
                decoded.Append((char)(byte)(value >> (8 * i)));
        }

        const string expected = "TEST_START m03.002\r\nCLROS_M03_002_TABLE role=rsdprootmcfgapichpet physical= owned= offset= length= signature= rootIndex= compared= mismatches=CLROS_M03_002_RETAIN total= descriptors=5 guards=32 tail= mismatches=0\r\nCLROS_M03_002_READY firmwareAvailable=true exitBootServices=false\r\nTEST_PASS m03.002\r\n";
        Need(decoded.ToString() == expected, "exact source-emitted literal grammar independent of future captures");
        Need(Regex.Matches(formatter, "WriteUnsignedDecimal\\(console, serial,").Count == 10 && formatter.Contains("role < 5", StringComparison.Ordinal), "eight row fields plus total/tail");
        TestTranscriptControls();
    }

    private static void TestScriptProfile()
    {
        string root = Root(), directory = Scratch("script-profile"), script = Path.Combine(root, "tools/OS.Tooling/QemuProfiles.cs");
        string before = Hash(script);
        var buildProfile = OS.Tooling.EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableAcpiPhysicalDiscoveryProbe"] = true });
        Need(buildProfile.CommandArguments().Contains("--EnableAcpiPhysicalDiscoveryProbe"), "conditional build forwarding");
        Result Dry(params string[] args) => Tool(["qemu", "smoke", "-DryRun", ..args]);
        void Reject(params string[] args)
        {
            Result r = Dry(args);
            Need(r.Exit != 0 && !r.Timeout && (r.Output + r.Error).Contains("TEST_FAIL", StringComparison.Ordinal), "profile refusal " + string.Join(' ', args) + " " + r.Output + r.Error);
        }

        const string flag = "-EnableAcpiPhysicalDiscoveryProbe";
        string[] positive = ["-BuildEfi", flag, "-MarkerPreset", "acpi"];
        foreach (string preset in new[]
        {
            "acpi",
            " ACPI "
        }

        )
        {
            Result run = Dry("-BuildEfi", flag, "-MarkerPreset", preset);
            Passed(run);
            string[] Vector(string prefix) => run.Output.Split('\n').Single(x => x.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..].TrimEnd('\r').Split(';');
            string[] ordered = Vector("ORDERED_MARKERS "), unique = Vector("UNIQUE_MARKERS "), required = Vector("REQUIRED_MARKERS ");
            Need(ordered.Length == OS.Tooling.QemuContracts.M03AcpiBootstrapRequiredMarkers.Count && unique.Length == ordered.Length && required.Length == ordered.Length, "complete record inventory required");
            Need(ordered.SequenceEqual(unique, StringComparer.Ordinal) && ordered.ToHashSet(StringComparer.Ordinal).SetEquals(required), "ordered/unique sequence and required set");
            Need(ordered.Distinct(StringComparer.Ordinal).Count() == ordered.Length && ordered.All(x => x.Length > 0 && x.Trim() == x), "canonical unique markers");
            Need(ordered[13] == "ACPI_RSDP_SNAPSHOT_READY" && ordered[17] == "ACPI_MCFG_DISCOVERY_READY" && ordered[18] == "TEST_START m03.002" && ordered[26] == "TEST_PASS m03.002" && ordered[^1] == "OS_SMOKE_PASS", "old ACPI and complete new sequence");
            Need(run.Output.Split('\n').Count(x => string.Equals(x.TrimEnd('\r'), "TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)) == 1, "exact dryrun terminal");
        }

        Reject(flag, "-MarkerPreset", "acpi");
        Reject("-BuildEfi", flag);
        Reject("-BuildEfi", flag, "-MarkerPreset", "baseline");
        Reject("-BuildEfi", flag, "-MarkerPreset", "acpi,baseline");
        Reject([..positive, "-SkipImageBuild"]);
        Reject([..positive, "-RequireNativeAppMarkers"]);
        Reject("-RequireMarker", "CLROS_M03_002_READY");
        foreach (string option in new[]
        {
            "-RequireMarker",
            "-RequireOrderedMarker",
            "-RequireUniqueMarker",
            "-ForbidMarker",
            "-M01RawObservationLog",
            "--http-probe",
            "--extra-arg"
        }

        )
            Reject([..positive, option, "extra"]);
        var flags = OS.Tooling.EfiContracts.DiagnosticProbes.ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "actual current requested profile vector");
        foreach (string other in flags.Where(x => x != "EnableAcpiPhysicalDiscoveryProbe"))
            Reject([..positive, "-" + other]);
        Result markerOnly = Dry("-BootPolicy", "Diagnostic", "-MarkerPreset", "acpi");
        Passed(markerOnly);
        Need(!markerOnly.Output.Contains("CLROS_M03_002_", StringComparison.Ordinal), "historical marker-only preset remains separate");
        Need(Hash(script) == before, "unchanged actual smoke source");
    }

    private static void TestTranscriptControls()
    {
        string directory = Scratch("transcript");
        string[] lines = ["TEST_START efi.main", "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 4", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "ACPI_RSDP_SNAPSHOT_READY", "ACPI_RSDP_VALIDATED", "ACPI_ROOT_SDT_SNAPSHOT_READY", "ACPI_ROOT_SDT_VALIDATED", "ACPI_MCFG_DISCOVERY_READY", "TEST_START m03.002", "CLROS_M03_002_TABLE role=rsdp physical=805306368 owned=1073741840 offset=0 length=36 signature=2329016660419433298 rootIndex=18446744073709551615 compared=36 mismatches=0", "CLROS_M03_002_TABLE role=root physical=805310464 owned=1073741876 offset=36 length=60 signature=1413763928 rootIndex=18446744073709551615 compared=60 mismatches=0", "CLROS_M03_002_TABLE role=mcfg physical=805314560 owned=1073741936 offset=96 length=60 signature=1195787085 rootIndex=2 compared=60 mismatches=0", "CLROS_M03_002_TABLE role=apic physical=805318656 owned=1073741996 offset=156 length=44 signature=1128878145 rootIndex=0 compared=44 mismatches=0", "CLROS_M03_002_TABLE role=hpet physical=805322752 owned=1073742040 offset=200 length=56 signature=1413828680 rootIndex=1 compared=56 mismatches=0", "CLROS_M03_002_RETAIN total=256 descriptors=5 guards=32 tail=265984 mismatches=0", "CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false", "TEST_PASS m03.002", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
        var control = new FirmwareTranscriptControls(directory, path => OS.Tooling.QemuTranscripts.AssertM03AcpiBootstrapTranscript(path));
        byte[] good = FirmwareTranscriptControls.Bytes(lines);
        control.Check(good, true);
        string[] rsdt = [..lines];
        rsdt[19] = rsdt[19].Replace("length=36", "length=20").Replace("compared=36", "compared=20");
        rsdt[20] = rsdt[20].Replace("owned=1073741876 offset=36 length=60 signature=1413763928", "owned=1073741860 offset=20 length=48 signature=1413763922").Replace("compared=60", "compared=48");
        rsdt[21] = rsdt[21].Replace("owned=1073741936 offset=96", "owned=1073741908 offset=68");
        rsdt[22] = rsdt[22].Replace("owned=1073741996 offset=156", "owned=1073741968 offset=128");
        rsdt[23] = rsdt[23].Replace("owned=1073742040 offset=200", "owned=1073742012 offset=172");
        rsdt[24] = rsdt[24].Replace("total=256", "total=228").Replace("tail=265984", "tail=266012");
        control.Check(FirmwareTranscriptControls.Bytes(rsdt), true);
        string[] touch = [..rsdt];
        touch[20] = touch[20].Replace("physical=805310464", "physical=805306388");
        control.Check(FirmwareTranscriptControls.Bytes(touch), true);
        for (int i = 0; i < lines.Length; i++)
        {
            string[] copy = [..lines];
            copy[i] += " extra=1";
            control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            copy = [..lines];
            copy[i] = "";
            control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            copy = [..lines];
            copy[i] = copy[i].ToLowerInvariant();
            if (copy[i] != lines[i])
                control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            control.Check(FirmwareTranscriptControls.Bytes(lines.Take(i + 1).Concat(lines.Skip(i))), false);
        }

        for (int i = 19; i <= 23; i++)
        {
            foreach (string field in new[]
            {
                "physical",
                "owned",
                "length"
            }

            )
                foreach (string value in new[]
                {
                    "0",
                    "18446744073709551615",
                    "18446744073709551616",
                    "01",
                    "+1",
                    "-1"
                }

                )
                {
                    string[] copy = [..lines];
                    copy[i] = Regex.Replace(copy[i], " " + field + "=[0-9]+", " " + field + "=" + value);
                    control.Check(FirmwareTranscriptControls.Bytes(copy), false);
                }

            foreach (string field in new[]
            {
                "offset",
                "signature",
                "compared",
                "mismatches"
            }

            )
            {
                string[] copy = [..lines];
                copy[i] = Regex.Replace(copy[i], " " + field + "=[0-9]+", " " + field + "=1");
                control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            }

            string[] overlap = [..lines];
            overlap[i] = Regex.Replace(overlap[i], " physical=[0-9]+", " physical=1073741840");
            control.Check(FirmwareTranscriptControls.Bytes(overlap), false);
            string[] index = [..lines];
            index[i] = Regex.Replace(index[i], " rootIndex=[0-9]+", " rootIndex=" + (i < 21 ? "0" : "3"));
            control.Check(FirmwareTranscriptControls.Bytes(index), false);
        }

        foreach (var pair in new (string Old, string New)[]
        {
            ("physical=805310464", "physical=805306369"),
            ("rootIndex=2", "rootIndex=0"),
            ("signature=1413763928", "signature=1413763927"),
            ("descriptors=5", "descriptors=4"),
            ("guards=32", "guards=31"),
            ("tail=265984", "tail=265983"),
            ("total=256", "total=255"),
            ("regions=4", "regions=3"),
            ("freeBytes=81920", "freeBytes=81921"),
            ("firmwareAvailable=true", "firmwareAvailable=false"),
            ("exitBootServices=false", "exitBootServices=true")
        }

        )
            control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace(pair.Old, pair.New)), false);
        string[] swap = [..lines];
        (swap[21], swap[22]) = (swap[22], swap[21]);
        control.Check(FirmwareTranscriptControls.Bytes(swap), false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n", "\n")), false);
        control.Check(good[..^1], false);
        control.Check([..good, 0], false);
        control.Check([..good, 255], false);
        control.Check(good, false, [255]);
        control.Check(good, false, Encoding.ASCII.GetBytes("arbitrary"));
        control.Check(good, false, post: [..FirmwareTranscriptControls.Suffix, 65]);
        control.Check(good, true);
        string[] ordered = lines.Select(line => line.StartsWith("UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ", StringComparison.Ordinal) || line.StartsWith("BOOT_MEMORY_REGION_COUNT ", StringComparison.Ordinal) || line.StartsWith("BOOT_PAGE_ALLOCATOR_READY ", StringComparison.Ordinal) || line.StartsWith("CLROS_M03_002_RETAIN ", StringComparison.Ordinal) ? line.Split(' ')[0] : line.StartsWith("CLROS_M03_002_TABLE ", StringComparison.Ordinal) ? string.Join(' ', line.Split(' ').Take(2)) : line).ToArray();
        var runner = new StandardFirmwareRunnerControls(directory, lines, ordered, ["OS_SMOKE_FAIL", "TEST_FAIL", "ACPI_DISCOVERY_FAIL"], "EnableAcpiPhysicalDiscoveryProbe", preset: "acpi");
        runner.CompleteMatrix(false);
        Console.WriteLine("TRANSCRIPT_CONTROLS m03.acpi cases=" + (control.Cases + runner.Cases) + " stderrWhitespaceRejected=true");
    }

    [HostTest("m03.002.efi default and enabled no-stdlib artifacts", HostLane.Native, 3)]
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
            string directory = Scratch(enabled ? "enabled" : "default"), artifact = Path.Combine(root, "artifacts/efi/issue539-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result result = Tool(["efi", "build", "-BootPolicy", "Diagnostic", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), ..(enabled ? new[] { "-EnableAcpiPhysicalDiscoveryProbe" } : Array.Empty<string>())]);
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
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableAcpiPhysicalDiscoveryProbe"] = enabled }));
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
                Need(string.Equals(Hash(Path.Combine(root, input.GetProperty("path").GetString()!)), input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual raw source identity");
            foreach (var property in evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)))
                Need(property.Value.GetString() == (enabled && property.Name == "EnableAcpiPhysicalDiscoveryProbe" ? "true" : "false"), "actual profile " + property.Name);
            using var stream = File.OpenRead(Path.Combine(directory, "OS.dll"));
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Need(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
            var entry = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t => metadata.GetString(t.Name) == "EfiEntry");
            foreach (string name in new[]
            {
                "TryCaptureAcpiBootstrap",
                "TryReadAndValidateAcpiRsdp",
                "TryReadAndValidateRootSdt",
                "TryDiscoverSelectedAcpiTables",
                "AcpiFinalAdmission",
                "TryPublishAcpiBootstrap",
                "ReadBackAcpiBootstrap"
            }

            )
            {
                var methods = entry.GetMethods().Where(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == name).ToArray();
                Need(methods.Length == (enabled ? 1 : 0), "conditional numeric method " + name);
                if (enabled)
                    Need(metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress != 0, "actual method body " + name);
            }
        }
    }
}
#endif
