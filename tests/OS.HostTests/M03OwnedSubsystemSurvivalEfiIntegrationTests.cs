using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class M03OwnedSubsystemSurvivalEfiIntegrationTests
{
    private static string root = null!, scratch = null!;
    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        root = Environment.GetEnvironmentVariable("CLROS_TEST_ROOT") ?? Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "CLROS.slnx")))
            root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("CLROS root missing");
        scratch = Path.Combine(root, "artifacts", "host-tests", "m03-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Group("profile", Profile);
        Group("panic_capture", PanicCapture);
        Group("whole_transcript", WholeTranscript);
    }
    private static void Group(string name, Action test)
    {
        string marker = "m03.010.integration." + name;
        HostCheck.Run("" + marker, test);
    }
    private static void Need(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
    private static string Read(string path) => File.ReadAllText(Path.Combine(root, path));

    private static void Profile()
    {
        Need(!Probes.EnableM03OwnedSubsystemSurvivalProbe && !Probes.EnableM03OwnedSubsystemPanicProbe,
            "both dangerous children default off");
        OwnedAllocatorRoundTripState state = default;
        FirstExitDiagnosticState diagnostic = default;
        FirstExitOwnedArenaState arena = default;
        Need(BootSequence.RunOwnedSubsystemAllocatorRoundTrip(ref state, ref diagnostic, ref arena, default,
            (M01LiveAllocatorStaticState*)1) == OwnedAllocatorRoundTripStatus.AdmissionFailed &&
            state.Status == default && diagnostic.Status == default, "disabled boundary never accesses invalid owner");
        XDocument project = XDocument.Parse(Read("src/OS/OS.csproj"));
        var flags = project.Descendants().Where(e => e.Name.LocalName.StartsWith("Enable", StringComparison.Ordinal) &&
            e.Name.LocalName.EndsWith("Probe", StringComparison.Ordinal) && e.Attribute("Condition") != null).ToArray();
        int vectorCount = System.Text.RegularExpressions.Regex.Matches(project.ToString(), @"\$\((Enable\w+Probe)\)")
            .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Count();
        Need(vectorCount == OS.Tooling.EfiContracts.DiagnosticProbes.Count && flags.All(e => e.Value == "false"), "reviewed diagnostic inventory and literal disabled defaults");
        Need(project.Descendants("Compile").Count() == OS.Tooling.EfiContracts.CompileItems.Count, "actual explicit 122-file compile closure including the normal policy");
        foreach (string flag in new[] { "EnableM03OwnedSubsystemSurvivalProbe", "EnableM03OwnedSubsystemPanicProbe" })
        {
            Need(flags.Single(e => e.Name.LocalName == flag).Value == "false", "literal child default " + flag);
            string[] chain = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableAcpiPhysicalDiscoveryProbe",
            "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe"];
            string[] selected = flag.EndsWith("PanicProbe", StringComparison.Ordinal) ? [.. chain, "--" + flag] : chain;
            foreach (bool build in new[] { true, false })
                Need(OS.Tooling.EfiOptions.Parse(selected, build).Profile().IsEnabled(flag), "parsed and propagated EFI child " + flag);
            Need(OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. selected])).Flags[flag], "parsed and propagated guest child " + flag);
        }
        var closure = OS.Tooling.EfiEvidence.SourceClosurePaths(root);
        Need(closure.Distinct(StringComparer.OrdinalIgnoreCase).Count() == closure.Count, "source closure has no duplicate identities");
        Need(OS.Tooling.EfiContracts.SourceClosureExplicitPaths.All(closure.Contains), "publisher and verifier bind explicit build authorities");
        foreach (string name in OS.Tooling.EfiContracts.CompileItems.Select(item => item.Split('|')[0]))
            Need(closure.Contains(Path.GetRelativePath(root, Path.GetFullPath(name, Path.Combine(root, "src/OS"))).Replace('\\', '/')), "original compile authority " + name);
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
            ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableAcpiPhysicalDiscoveryProbe",
             "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe",
             "--EnableM03OwnedSubsystemPanicProbe"], true).Profile());
        string operation = System.Text.RegularExpressions.Regex.Replace(source.Method(
            "OS.Boot.EfiEntry+M03FirstExitAdapter", "ReportOwnedSubsystems").ToFullString(),
            @"\s+", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Need(!operation.Contains("FirstExitPostExitDiagnostics.Report", StringComparison.Ordinal) &&
            System.Text.RegularExpressions.Regex.Matches(operation, "BootSequence\\.RunOwnedSubsystemAllocatorRoundTrip").Count == 1,
            "allocator wrapper is the sole original diagnostic invocation");
        Need(operation.Contains("ReadBackAcpiBootstrap(workspace,copied->Bytes", StringComparison.Ordinal) &&
            operation.Contains("ReadOwnedGuard((ulong*)state->PanicGuard)", StringComparison.Ordinal), "original readback and actual C# guard load");
        string runnerOptions = HostSourceText.Documents(HostSourceCatalog.Project(
            "tools/OS.QemuRunner/OS.QemuRunner.csproj"), "RunnerOptions");
        Need(runnerOptions.Contains("--expect-owned-subsystem-panic", StringComparison.Ordinal), "explicit terminal-control mode");
    }

    private const string Firmware = "G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==";
    internal static string Frame(string[] lines) => Encoding.ASCII.GetString(Convert.FromBase64String(Firmware)) + string.Join("\r\n", lines) + "\r\n";
    private static string Row(int stage, params ulong[] values) => "M03_OWNED " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
    internal static string[] Capture(bool panic)
    {
        // Independent synthetic original-parent transcript, with distinct static,
        // copied, allocator, table, code and arena owners. This is not guest proof.
        List<string> lines = [.. Parent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd('\r'))];
        ulong[] physical = [805306368, 805310464, 805314560, 805318656, 805322752];
        ulong[] lengths = [36, 60, 60, 44, 56];
        ulong[] signatures = [2329016660419433298, 1413763928, 1195787085, 1128878145, 1413828680];
        ulong[] indices = [ulong.MaxValue, ulong.MaxValue, 2, 0, 1];
        lines.Add("TEST_START m03.010");
        lines.Add(Row(0, 0x41000000, 0x50000000, 0x50000000, 2, 2, 8192, 0, 0));
        ulong offset = 0;
        for (int i = 0; i < 5; i++)
        {
            lines.Add(Row(i + 1, 0x42000000, 0x40000010, physical[i], offset, lengths[i], signatures[i], indices[i], lengths[i]));
            offset += lengths[i];
        }
        const ulong arena = 0x60000000, tables = 0x61000000, code = 0x62000000;
        lines.Add(Row(6, arena, tables, code, arena + 4096, arena + 8192, arena + 12288, 24, arena + 300008));
        if (!panic)
        {
            lines.Add("CLROS_M03_010_READY");
            lines.Add("TEST_PASS m03.010");
            return [.. lines];
        }
        lines.Add(Row(7, arena + 86016, tables, arena + 1024, code, arena, arena + 90120, arena + 352240, arena + 401392));
        lines.Add(Row(8, arena + 86016, tables, arena + 1024, arena + 401000, arena + 385032, arena + 401392, 14, 0));
        string fault = "OS_SMOKE_FAIL subsystem=boot reason=exit-fault vector=14 errorCodePresent=true errorCode=0x0000000000000000";
        string[] names = ["rip", "rsp", "rbp", "rflags", "cr2", "cr3", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
        ulong[] values = [0x70001000, arena + 300000, 0, 2, arena + 86016, tables, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
        for (int i = 0; i < names.Length; i++)
            fault += " " + names[i] + "=0x" + values[i].ToString("X16");
        lines.Add(fault);
        return [.. lines];
    }
    private static void PanicCapture()
    {
        string[] lines = Capture(true);
        string text = Frame(lines);
        Need(lines.Length == 77 && text.Count(c => c == '\n') == 79, "77 application rows / 79 newline observations include firmware preamble");
        Need(OwnedSubsystemPanicCapture.Evaluate(text, out string prefix) == 1 && prefix.Split("\r\n").Length == 77 &&
            !prefix.Contains("OS_SMOKE_FAIL", StringComparison.Ordinal), "finite prerequisite prefix plus exact terminal control");
        Need(OwnedSubsystemPanicCapture.Evaluate(text[..^2], out _) == 0, "incomplete final record cannot complete");
        Need(OwnedSubsystemPanicCapture.Evaluate(text + "TEST_PASS m03.010\r\n", out _) == -1, "no post-panic success");
        foreach (var change in new[] {
            (67, "0x0000000000002000", "0x0000000000001000"),
            (68, "0x0000000030000000", "0x0000000030000001"),
            (69, "0x0000000000000024", "0x0000000000000025"),
            (73, "0x0000000060003000", "0x0000000060004000"),
            (75, "0x0000000060061E68", "0x0000000060063000"),
            (76, "cr2=0x0000000060015000", "cr2=0x0000000060015008"),
            (76, "cr3=0x0000000061000000", "cr3=0x0000000061001000"),
            (76, "rflags=0x0000000000000002", "rflags=0x0000000000000202") })
        {
            string[] bad = (string[])lines.Clone();
            Need(bad[change.Item1].Contains(change.Item2), "mutation target exists");
            bad[change.Item1] = bad[change.Item1].Replace(change.Item2, change.Item3);
            Need(OwnedSubsystemPanicCapture.Evaluate(Frame(bad), out _) == -1, "reject association mutation row " + change.Item1);
        }
        Need(OwnedSubsystemPanicCapture.Evaluate("X" + text[1..], out _) == -1, "exact original firmware framing");
        Need(OwnedSubsystemPanicCapture.Evaluate(text.Replace("0x0000000041000000", "0x41000000"), out _) == -1, "fixed-width fields");
        File.WriteAllText(Path.Combine(scratch, "panic.log"), text, new UTF8Encoding(false));
        string[] positive = Capture(false);
        Need(positive.Length == 76 && Frame(positive).Count(c => c == '\n') == 78, "positive application/stream counts");
        File.WriteAllText(Path.Combine(scratch, "positive.log"), Frame(positive), new UTF8Encoding(false));
        string[] normal = [..positive.Take(52), ..positive.Skip(55),
            "TEST_START m03.011", "CLROS_M03_011_READY", "TEST_PASS m03.011", ..positive.Skip(52).Take(3)];
        Need(normal.Length == 79 && normal[52] == "TEST_START m03.007" && normal[63] == "TEST_START m03.010" &&
            normal[78] == "OS_SMOKE_PASS", "normal success follows real-exit and positive-owned records");
        File.WriteAllText(Path.Combine(scratch, "normal.log"), Frame(normal), new UTF8Encoding(false));
    }
    private static void WholeTranscript()
    {
        string positive = File.ReadAllText(Path.Combine(scratch, "positive.log"));
        string panic = File.ReadAllText(Path.Combine(scratch, "panic.log"));
        int cases = 0;
        void Check(string text, bool control, bool expected, bool normal = false)
        {
            string path = Path.Combine(scratch, "case-" + cases++ + ".log");
            File.WriteAllText(path, text, new UTF8Encoding(false));
            bool accepted = true;
            try
            {
                OS.Tooling.QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, ownedPanic: control, normalBoot: normal);
            }
            catch (OS.Tooling.ToolFailure)
            {
                accepted = false;
            }
            Need(accepted == expected, "whole transcript case=" + (cases - 1) + " expected=" + expected);
        }
        Check(positive, false, true);
        Check(panic, true, true);
        Check(positive.Replace("0x0000000041000000", "0x0000000040000000", StringComparison.Ordinal), false, false);
        Check(positive.Replace("0x0000000030000000", "0x0000000030000001", StringComparison.Ordinal), false, false);
        Check(positive.Replace("BOOT_MEMORY_REGION_COUNT 4", "BOOT_MEMORY_REGION_COUNT 5", StringComparison.Ordinal), false, false);
        Check(positive.Replace("mismatches=0", "mismatches=1", StringComparison.Ordinal), false, false);
        Check(positive.Replace("CLROS_M03_010_READY", "CLROS_M03_010_READY extra=1", StringComparison.Ordinal), false, false);
        Check(panic.Replace("M03_OWNED 8", "M03_OWNED 7", StringComparison.Ordinal), true, false);
        Check(panic.Replace("cr2=0x0000000060015000", "cr2=0x0000000060015008", StringComparison.Ordinal), true, false);
        Check(panic + "TEST_PASS m03.010\r\n", true, false);
        Check(panic.Replace("\r\n", "\n", StringComparison.Ordinal), true, false);
        Check("X" + panic[1..], true, false);
        int ownedCases = cases;
        string normal = File.ReadAllText(Path.Combine(scratch, "normal.log"));
        Check(normal, false, true, true);
        Check(positive, false, false, true);
        Check(normal, false, false);
        Check(normal, true, false, true);
        int prefixBytes = Convert.FromBase64String(Firmware).Length;
        string prefix = normal[..prefixBytes];
        string[] rows = normal[prefixBytes..].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Need(rows.Length == OS.Tooling.QemuContracts.M03NormalVerifiedExitMarkers.Count, "complete normal inventory");
        for (int i = 0; i < rows.Length; i++)
        {
            Check(prefix + string.Join("\r\n", rows.Where((_, index) => index != i)) + "\r\n", false, false, true);
            Check(prefix + string.Join("\r\n", rows.Take(i).Concat([rows[i]]).Concat(rows.Skip(i))) + "\r\n", false, false, true);
            if (i + 1 < rows.Length)
            {
                string[] swapped = (string[])rows.Clone();
                (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
                Check(prefix + string.Join("\r\n", swapped) + "\r\n", false, false, true);
            }
        }
        foreach (string bad in new[] { normal.Replace("\r\n", "\n", StringComparison.Ordinal), normal[..^1], normal + "EXTRA\r\n",
        normal.Replace("0x0000000041000000", "0x0000000040000000", StringComparison.Ordinal), normal.Replace("M03_OWNED 0", "M03_OWNED 1", StringComparison.Ordinal),
        normal.Replace("mismatches=0", "mismatches=1", StringComparison.Ordinal), normal.Replace("OS_BOOT_OK", "OS_SMOKE_FAIL", StringComparison.Ordinal),
        normal.Replace("CLROS_M03_011_READY", "CLROS_M03_011_READY extra=1", StringComparison.Ordinal) })
            Check(bad, false, false, true);
        File.WriteAllText(Path.Combine(scratch, "parser-controls.json"), JsonSerializer.Serialize(new
        {
            OwnedControls = ownedCases,
            NormalControls = cases - ownedCases
        }));
    }

    private const string Parent = """
        TEST_START efi.main
        EFI_CONSOLE_READY
        UEFI_MEMORY_MAP_READY
        UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4
        UEFI_MEMORY_MAP_OWNED
        BOOT_HANDOFF_READY
        UEFI_MEMORY_MAP_NORMALIZED
        BOOT_MEMORY_MAP_ACCEPTED
        BOOT_MEMORY_REGIONS_READY
        BOOT_MEMORY_REGION_COUNT 4
        BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0
        BOOT_ACPI_RSDP_HANDOFF_READY
        UEFI_ACPI_RSDP_READY
        ACPI_RSDP_SNAPSHOT_READY
        ACPI_RSDP_VALIDATED
        ACPI_ROOT_SDT_SNAPSHOT_READY
        ACPI_ROOT_SDT_VALIDATED
        ACPI_MCFG_DISCOVERY_READY
        TEST_START m03.002
        CLROS_M03_002_TABLE role=rsdp physical=805306368 owned=1073741840 offset=0 length=36 signature=2329016660419433298 rootIndex=18446744073709551615 compared=36 mismatches=0
        CLROS_M03_002_TABLE role=root physical=805310464 owned=1073741876 offset=36 length=60 signature=1413763928 rootIndex=18446744073709551615 compared=60 mismatches=0
        CLROS_M03_002_TABLE role=mcfg physical=805314560 owned=1073741936 offset=96 length=60 signature=1195787085 rootIndex=2 compared=60 mismatches=0
        CLROS_M03_002_TABLE role=apic physical=805318656 owned=1073741996 offset=156 length=44 signature=1128878145 rootIndex=0 compared=44 mismatches=0
        CLROS_M03_002_TABLE role=hpet physical=805322752 owned=1073742040 offset=200 length=56 signature=1413828680 rootIndex=1 compared=56 mismatches=0
        CLROS_M03_002_RETAIN total=256 descriptors=5 guards=32 tail=265984 mismatches=0
        CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false
        TEST_PASS m03.002
        TEST_START m01.stage1
        M01_LOADED_IMAGE_CAPTURED
        M01_STACK_SWITCH_STUB_READY
        M01_STACK_ARENA_ALLOCATED
        M01_STACK_GUARD_RESERVED
        M01_STAGE1_MAP_RECAPTURED
        M01_STAGE1_RESERVATIONS_VALIDATED
        M01_OWNED_STACK_READY
        M01_OWNED_STACK_SWITCHED
        M01_RSP_CONTAINED
        TEST_PASS m01.stage1
        TEST_START m01.stage2
        M01_ALLOCATOR_ARENA_ALLOCATED
        M01_STAGE2_MAP_RECAPTURED
        M01_RESERVATIONS_APPLIED
        M01_LIVE_ALLOCATED_4K
        M01_LIVE_PAGE_WITHIN_ARENA
        M01_LIVE_FREED_4K
        M01_DOUBLE_FREE_REJECTED
        TEST_PASS m01.stage2
        TEST_START boot.sequence
        BOOT_PHASE PhaseA.Audit START
        BOOT_PHASE PhaseA.Audit PASS
        BOOT_PHASE PhaseA.Diagnostics START
        BOOT_PHASE PhaseA.Diagnostics PASS
        OS_BOOT_OK
        TEST_PASS boot.sequence
        OS_SMOKE_PASS
        TEST_START m03.007
        M03_EBS_RETURNED_SUCCESS
        M03_PERMANENT_CR3_ADOPTED
        M03_POST_EBS_OWNED_DIAGNOSTIC
        CLROS_M03_007_READY
        TEST_PASS m03.007
        TEST_START m03.009
        UEFI_EBS_SUCCESS
        POST_FIRMWARE_DIAGNOSTICS_READY
        CLROS_M03_009_READY
        TEST_PASS m03.009
        """;
}
