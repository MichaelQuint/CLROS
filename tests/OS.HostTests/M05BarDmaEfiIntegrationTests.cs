using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05BarDmaEfiIntegrationTests
{
    internal static readonly string[] Flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableAcpiPhysicalDiscoveryProbe",
        "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe", "--EnableM05PciEnumerationProbe", "--EnableM05BarDmaProbe"];

    [HostTest("m05.bar_dma.efi default off and transitively requires owned PCI")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05BarDmaProbe && !BootSequence.RunOwnedBarDma(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M05BarDma"));
            foreach (string required in Flags[..^1]) Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M05BarDmaMarkers.All(plan.Required.Contains));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.bar_dma.efi complete transcript binds restoration and owned allocator lifetime")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-bar-dma-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows) { File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(rows)); QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true, barDma: true); }
        try
        {
            Check(lines);
            string[] alreadyDisabled = [.. lines];
            alreadyDisabled[98] = alreadyDisabled[98].Replace("0x0000000000000007", "0x0000000000000003", StringComparison.Ordinal);
            Check(alreadyDisabled);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true));
            int[] rowIndices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M05_BAR_DMA ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int,int,ulong)[]
            { (0,0,0x700), (0,1,0x12341af4), (0,2,0x10000), (0,3,0xe0031000), (1,0,0), (1,1,4095), (1,3,1),
              (2,0,0xfffffffffffff000), (2,2,2), (2,3,0), (3,0,7), (3,1,7), (3,1,0), (3,3,0), (4,1,0), (5,0,1), (6,0,0),
              (6,2,0x400000), (7,1,0x101000), (8,1,0), (9,2,2), (10,2,1), (11,0,1), (12,1,1), (12,2,1) })
            {
                string[] changed = [.. lines]; string[] parts = changed[rowIndices[row]].Split(' '); parts[col + 2] = "0x" + wrong.ToString("X16");
                changed[rowIndices[row]] = string.Join(' ', parts); Reject(() => Check(changed));
            }
            string[] swapped = [.. lines]; (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]); Reject(() => Check(swapped));
            Reject(() => Check([.. lines, "extra"]));
            string[] badPrefix = [.. lines]; badPrefix[67] = badPrefix[67].Replace("M03_OWNED 0", "M03_OWNED 1", StringComparison.Ordinal); Reject(() => Check(badPrefix));
        }
        finally { File.Delete(path); }
    }

    private static string[] Capture()
    {
        string[] lines = QemuContracts.M05BarDmaMarkers.ToArray();
        string[] prefix = M03OwnedSubsystemSurvivalEfiIntegrationTests.Capture(false); prefix.CopyTo(lines, 0);
        string Row(string name, int n, ulong[] values) => name + " " + n + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        ulong[][] pci = [[0xe0000000,0xe0000000,0x100000,0],[6,4,3,1],[0xe0000000,0x29c08086,0,0x06000000],[1,1,1,4096],
            [8,39,0x1f07,0],[8,39,1,0],[10,1,2,0],[0x29228086,0x200,2,2],[9,8,7,1]];
        int[] pciIndices = [77,78,82,83,87,88,89,93,94];
        for (int i = 0; i < pci.Length; i++) lines[pciIndices[i]] = Row("M05_PCI", i, pci[i]);
        string[] owner = prefix[67].Split(' '); ulong page = Convert.ToUInt64(owner[4][2..], 16);
        ulong[][] values = [[0x600,0x00051b36,7,0xe0030000],[0x80000000,4096,2,0],[0x100000000,0x200000,3,1],[3,3,1,1],
            [0,1,2,3],[7,7,8,9],[page,page,page,4096],[0x101000,0x201000,0x301000,4096],[0,7,4,0],
            [page,2,1,1],[1,1,13,1],[12,12,9,1],[2,2,0,1]];
        int stage = 0;
        for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("M05_BAR_DMA ", StringComparison.Ordinal)) { lines[i] = Row("M05_BAR_DMA", stage, values[stage]); stage++; }
        return lines;
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid profile or evidence must be rejected");
    }
}
