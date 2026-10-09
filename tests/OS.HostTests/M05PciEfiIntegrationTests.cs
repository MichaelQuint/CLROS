using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05PciEfiIntegrationTests
{
    [HostTest("m05.pci.efi default off and profile requires original ownership")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05PciEnumerationProbe && !BootSequence.RunOwnedPciEnumeration(true, &calls, &Execute) && calls == 0);
        string[] flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableAcpiPhysicalDiscoveryProbe",
            "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe", "--EnableM05PciEnumerationProbe"];
        foreach (bool build in new[] { true, false })
        {
            HostCheck.True(EfiOptions.Parse(flags, build).Profile().IsEnabled("M05PciEnumeration"));
            foreach (string required in flags[..^1]) Reject(() => EfiOptions.Parse(flags.Where(f => f != required).ToArray(), build).Profile());
            foreach (string conflict in new[] { "M05PhysicalMapping", "M05OwnedAcpiSelection", "M05MonotonicClock", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. flags, "--Enable" + conflict + "Probe"], build).Profile());
            Reject(() => EfiOptions.Parse([.. flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. flags]));
        HostCheck.True(QemuContracts.M05PciEnumerationMarkers.All(plan.Required.Contains) && plan.Forbidden.Contains("TEST_FAIL"));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. flags])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.pci.efi records bind aperture header scan and malformed controls")]
    private static void Transcript()
    {
        string Row(int stage, params ulong[] values) => "M05_PCI " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        string[] lines = QemuContracts.M05PciEnumerationMarkers.ToArray();
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        int[] offsets = [1, 2, 6, 7, 11, 12, 13, 17, 18];
        ulong[][] rows = [[0xe0000000, 0xe0800000, 0x100000, 2], [6, 4, 3, 1],
            [0xe0800000, 0x29c08086, 0, 0x06000000], [1, 1, 1, 4096],
            [8, 46, 0x02081f07, 0], [8, 46, 1, 0], [10, 1, 2, 0],
            [0x12348086, 0x02081f02, 4, 2], [9, 8, 7, 1]];
        for (int i = 0; i < 9; i++) lines[start + offsets[i]] = Row(i, rows[i]);
        QemuTranscripts.AssertM05PciEnumerationRecords(lines);
        foreach ((int stage, int column, ulong value) in new (int, int, ulong)[]
        {
            (0, 0, 0), (0, 1, 0xe0800001), (0, 2, 4096), (0, 3, 65536),
            (1, 0, 0), (2, 0, 0xe0000000), (2, 1, 0xffffffff), (2, 2, 2), (3, 1, 0),
            (4, 0, 17), (4, 1, 33), (4, 2, 0x02071f07), (4, 2, 0x02081f01),
            (5, 0, 7), (5, 1, 39), (5, 2, 0), (6, 0, 1), (6, 2, 1), (6, 3, 1),
            (7, 1, 0x02082000), (7, 2, 49), (7, 3, 1), (8, 0, 1), (8, 3, 0)
        })
        {
            string[] changed = [.. lines]; ulong[] values = [.. rows[stage]]; values[column] = value;
            changed[start + offsets[stage]] = Row(stage, values); Reject(() => QemuTranscripts.AssertM05PciEnumerationRecords(changed));
        }
        string[] swapped = [.. lines]; (swapped[start + 3], swapped[start + 4]) = (swapped[start + 4], swapped[start + 3]);
        Reject(() => QemuTranscripts.AssertM05PciEnumerationRecords(swapped));
        Reject(() => QemuTranscripts.AssertM05PciEnumerationRecords([.. lines, "extra"]));
        // Exercise the complete parser, not only the PCI suffix. The shared M03
        // ownership check must receive exactly its original 76-record prefix.
        string[] whole = [.. M03OwnedSubsystemSurvivalEfiIntegrationTests.Capture(false), .. lines.Skip(start)];
        string path = Path.Combine(Path.GetTempPath(), "clros-pci-transcript-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(whole));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true));
            whole[67] = whole[67].Replace("M03_OWNED 0", "M03_OWNED 1", StringComparison.Ordinal);
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(whole));
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true));
        }
        finally { File.Delete(path); }
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid profile or evidence must be rejected");
    }
}
