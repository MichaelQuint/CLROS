using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;
using static M05ApicSetupEfiIntegrationTests;

internal static unsafe class M05VirtqueueInterruptEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M05VirtioNegotiationEfiIntegrationTests.Flags, "--EnableM05ApicSetupProbe", "--EnableM05VirtqueueInterruptProbe"];

    [HostTest("m05.irq.efi defaults off and requires APIC plus negotiation without polling")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05VirtqueueInterruptProbe && !BootSequence.RunOwnedVirtqueueInterrupt(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M05VirtqueueInterrupt"));
            foreach (string required in Flags[..^1]) Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05BarDma", "M05VirtqueuePolling", "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M05VirtqueueInterruptMarkers.All(plan.Required.Contains));
        _ = new OS.Tools.QemuRunner.SerialMarkerStateMachine(QemuContracts.M05VirtqueueInterruptMarkers.ToArray(), []);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.irq.efi transcript requires handler EOI bounded IF window and restored IDT")]
    private static void Transcript()
    {
        string[] lines = QemuContracts.M05VirtqueueInterruptMarkers.ToArray();
        string[] apic = M05ApicSetupEfiIntegrationTests.Capture(); apic.CopyTo(lines, 0);
        string[] virtio = M05VirtioNegotiationEfiIntegrationTests.Capture();
        virtio[QemuContracts.M05PciEnumerationMarkers.Count..].CopyTo(lines, apic.Length);
        ulong[] owner = lines[67].Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        Fill(lines, "M05_QUEUE", [[1,256,8,2],[0,1,8,11],[owner[2],owner[2],owner[2],4096],
            [0xc000003004,1,4,72],[1,1,0,0],[1,5,1,1],[owner[4],owner[4],owner[6],1],[5,1,1,0x403]]);
        Fill(lines, "M05_IRQ", [[0xfe000000,0xfe000800,3,0x98],[0xfee00000,0xe0,0xc002,0xffff],
            [0x8012b0,0x810000,0x820000,0xfee000b0],[1,0,1,0],[0x8012e6,0x202,2,2],
            [1,1000000,1,5],[4095,0x810000,4095,0x810000],[2,1,1,1]]);
        string path = Path.Combine(Path.GetTempPath(), "clros-irq-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioNegotiation: true, apicSetup: true, virtqueueInterrupt: true);
        }
        try
        {
            Check(lines);
            Mutations(lines, "M05_QUEUE ", [(5,0,2)], Check);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioNegotiation: true, virtqueuePolling: true));
            Mutations(lines, "M05_IRQ ", [(0,0,0),(0,1,0xfe000008),(0,1,0xfe001000),(0,2,0),(0,2,2048),(0,3,0xf8),
                (1,0,0xfee01000),(1,1,0xdf),(1,2,0x8002),(1,3,0),(2,0,0x8012b1),(2,1,0x820000),(2,2,0x8012b0),(2,3,0),
                (3,0,0),(3,0,2),(3,1,1),(3,2,0),(3,3,1),(4,0,0x8012e5),(4,0,0x8012f7),(4,1,2),(4,2,0x202),(4,3,0x202),
                (5,0,0),(5,0,1000001),(5,1,999999),(5,2,2),(5,3,4),(6,0,4094),(6,1,0x820000),(6,2,4094),(6,3,0x820000),
                (7,0,0x8002),(7,1,0),(7,2,0),(7,3,0)], Check);
        }
        finally { File.Delete(path); }
    }
}
