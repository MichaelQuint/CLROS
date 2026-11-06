using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05VirtioNegotiationEfiIntegrationTests
{
    internal static readonly string[] Flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableAcpiPhysicalDiscoveryProbe",
        "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe", "--EnableM05PciEnumerationProbe", "--EnableM05VirtioNegotiationProbe"];

    [HostTest("m05.virtio.efi defaults off and requires exclusive owned PCI profile")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05VirtioNegotiationProbe && !BootSequence.RunOwnedVirtioNegotiation(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M05VirtioNegotiation"));
            foreach (string required in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05BarDma", "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M05VirtioNegotiationMarkers.All(plan.Required.Contains));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m05.virtio.efi transcript binds high BAR mappings negotiation and terminal reset")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-virtio-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true, virtioNegotiation: true);
        }
        try
        {
            Check(lines);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true));
            int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M05_VIRTIO ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int, int, ulong)[]
            { (0,0,0x700), (0,1,0x10001af4), (0,2,0), (0,2,0xc000000001), (0,3,3), (1,1,0xc000001000),
              (2,0,0x10007), (2,0,5), (2,1,7), (2,2,1), (2,3,0x8000), (3,0,0), (3,1,0), (3,2,55),
              (4,3,0), (5,2,0), (6,1,0), (7,0,0xffffffff), (7,1,0x100000001), (7,2,3), (7,2,15), (7,3,0),
              (8,0,1), (8,1,1), (8,2,1), (8,3,1), (9,0,11), (9,1,7), (9,2,0), (9,3,1) })
            {
                string[] changed = [.. lines];
                string[] parts = changed[indices[row]].Split(' ');
                parts[col + 2] = "0x" + wrong.ToString("X16");
                changed[indices[row]] = string.Join(' ', parts);
                Reject(() => Check(changed));
            }
            string[] swapped = [.. lines];
            (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]);
            Reject(() => Check(swapped));
            Reject(() => Check([.. lines, "extra"]));
            Reject(() => Check(lines[..^1]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static string[] Capture()
    {
        string[] lines = QemuContracts.M05VirtioNegotiationMarkers.ToArray();
        M03OwnedSubsystemSurvivalEfiIntegrationTests.Capture(false).CopyTo(lines, 0);
        string Row(string name, int n, ulong[] values) => name + " " + n + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        ulong[][] pci = [[0xe0000000,0xe0000000,0x100000,0],[6,4,3,1],[0xe0000000,0x29c08086,0,0x06000000],[1,1,1,4096],
            [8,39,0x1f07,0],[8,39,1,0],[10,1,2,0],[0x29228086,0x200,2,2],[9,8,7,1]];
        int[] pciIndices = [77, 78, 82, 83, 87, 88, 89, 93, 94];
        for (int i = 0; i < pci.Length; i++)
            lines[pciIndices[i]] = Row("M05_PCI", i, pci[i]);
        ulong[][] values = [[0x600,0x10411af4,0xc000000000,4],[0xc000000000,0xc000003000,0xc000001000,0xc000002000],
            [7,0x403,0,0x4000],[0xc000000000,0xc000000000,56,1],[0xc000003000,0xc000003000,2,1],
            [0xc000001000,0xc000001000,1,1],[0xc000002000,0xc000002000,1,1],[0x8000000100000001,0x100000000,11,1],
            [2,3,5,6],[0,0x403,1,0]];
        int stage = 0;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("M05_VIRTIO ", StringComparison.Ordinal))
            {
                lines[i] = Row("M05_VIRTIO", stage, values[stage]);
                stage++;
            }
        return lines;
    }
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "invalid profile or evidence must be rejected");
    }
}
