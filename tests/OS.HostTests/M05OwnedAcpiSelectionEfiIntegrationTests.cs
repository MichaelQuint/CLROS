using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;
using OS.Tooling;

internal static unsafe class M05OwnedAcpiSelectionEfiIntegrationTests
{
    [HostTest("m05.002.efi disabled boot gate")]
    private static void Disabled()
    {
        int invocations = 0;
        HostCheck.True(!Probes.EnableM05OwnedAcpiSelectionProbe &&
            !BootSequence.RunOwnedAcpiSelection(true, &invocations, &Execute) &&
            !BootSequence.RunOwnedAcpiSelection(false, &invocations, &Execute) &&
            !BootSequence.RunOwnedAcpiSelection(true, null, &Execute) &&
            !BootSequence.RunOwnedAcpiSelection(true, &invocations, null) && invocations == 0);
    }

    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.002.efi unaligned pointer endian vectors")]
    private static void PointerEndian()
    {
        byte* bytes = stackalloc byte[] { 0xcc, 0xef, 0xcd, 0xab, 0x89, 0x67, 0x45, 0x23, 0x01, 0xdd };
        HostCheck.Equal(0x89abcdefU, EndianPrimitives.ReadUInt32LittleEndian(bytes + 1));
        HostCheck.Equal(0x0123456789abcdefUL, EndianPrimitives.ReadUInt64LittleEndian(bytes + 1));
        HostCheck.True(bytes[0] == 0xcc && bytes[9] == 0xdd);
    }

    [HostTest("m05.002.efi profile admission and fresh image")]
    private static void Profile()
    {
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        string[] guards = project.Descendants("Error").Select(x => (string?)x.Attribute("Condition") ?? "").ToArray();
        HostCheck.Equal(1, guards.Count(x => x == "'$(EnableM05OwnedAcpiSelectionProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')"));
        HostCheck.Equal(1, guards.Count(x => x == "'$(EnableM05OwnedAcpiSelectionProbe)' == 'true' and ('$(EnableM03OwnedSubsystemPanicProbe)' == 'true' or '$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' or '$(EnableM05PhysicalMappingProbe)' == 'true')"));
        string[] flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
            "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe",
            "--EnableM03OwnedSubsystemSurvivalProbe", "--EnableM05OwnedAcpiSelectionProbe"];
        foreach (bool build in new[] { true, false })
        {
            HostCheck.True(EfiOptions.Parse(flags, build).Profile().IsEnabled("M05OwnedAcpiSelection"));
            foreach (string required in flags[..^1])
                Reject(() => EfiOptions.Parse(flags.Where(f => f != required).ToArray(), build).Profile());
            foreach (string conflict in new[] { "M05PhysicalMapping", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. flags, "--Enable" + conflict + "Probe"], build).Profile());
            Reject(() => EfiOptions.Parse([.. flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. flags]));
        HostCheck.True(QemuContracts.M05OwnedAcpiSelectionMarkers.All(plan.Required.Contains));
        HostCheck.True(plan.Forbidden.Contains("TEST_FAIL"));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. flags])));
    }

    [HostTest("m05.002.efi retained selection transcript associations")]
    private static void Transcript()
    {
        string Row(string prefix, params ulong[] values) => prefix + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        string[] lines = Enumerable.Repeat("fixture", QemuContracts.M05OwnedAcpiSelectionMarkers.Count).ToArray();
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        lines[24] = "CLROS_M03_002_RETAIN total=300 descriptors=5 guards=32 tail=265940 mismatches=0";
        lines[start] = "TEST_START m05.002";
        for (int i = 0; i < 3; i++)
        {
            ulong physical = 0x100000UL + (ulong)i * 0x1000, offset = 120UL + (ulong)i * 60;
            lines[70 + i] = Row("M03_OWNED " + (i + 3), 0x800000, 0x900000, physical, offset, 60, 0, (ulong)i, 60);
            lines[start + 1 + i] = Row("M05_ACPI " + i, physical, offset, 60, (ulong)i);
        }
        lines[start + 4] = Row("M05_ACPI 3", 3, 299, 300, 1);
        lines[^2] = "CLROS_M05_002_READY"; lines[^1] = "TEST_PASS m05.002";
        QemuTranscripts.AssertM05OwnedAcpiSelectionRecords(lines);
        for (int i = 0; i < 3; i++)
        {
            string[] changed = [.. lines]; changed[start + 1 + i] = Row("M05_ACPI " + i, 0xdead, 120, 60, (ulong)i);
            Reject(() => QemuTranscripts.AssertM05OwnedAcpiSelectionRecords(changed));
        }
        foreach (ulong[] invalid in new ulong[][] { [1, 299, 300, 1], [2, 299, 300, 1], [3, 298, 300, 1], [3, 299, 300, 0] })
        {
            string[] changed = [.. lines]; changed[start + 4] = Row("M05_ACPI 3", invalid);
            Reject(() => QemuTranscripts.AssertM05OwnedAcpiSelectionRecords(changed));
        }
        Reject(() => QemuTranscripts.AssertM05OwnedAcpiSelectionRecords([.. lines, "extra"]));
    }

    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid profile or evidence must be rejected");
    }
}
