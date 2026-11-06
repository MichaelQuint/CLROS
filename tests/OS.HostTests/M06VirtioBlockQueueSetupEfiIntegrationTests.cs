using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M06VirtioBlockQueueSetupEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M05VirtioNegotiationEfiIntegrationTests.Flags[..^1], "--EnableM06VirtioBlockQueueSetupProbe"];

    [HostTest("m06.block.efi requires exclusive owned PCI diagnostic profile and defaults off")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06VirtioBlockQueueSetupProbe &&
            !BootSequence.RunOwnedVirtioBlockQueueSetup(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06VirtioBlockQueueSetup"));
            foreach (string required in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05BarDma", "M05VirtioNegotiation", "M05ApicSetup", "M05VirtqueuePolling", "M05VirtqueueInterrupt",
                "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M06VirtioBlockQueueSetupMarkers.All(plan.Required.Contains));
        _ = new OS.Tools.QemuRunner.SerialMarkerStateMachine(QemuContracts.M06VirtioBlockQueueSetupMarkers.ToArray(), []);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m06.block.efi transcript binds identity features owned DMA queue readback and reset cleanup")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-block-queue-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, BlockFrame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true);
        }
        try
        {
            Check(lines);
            string valid = File.ReadAllText(path);
            File.WriteAllText(path, valid.Replace("Pci(0x6,0x0)", "Pci(0x7,0x0)", StringComparison.Ordinal));
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true));
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(lines));
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true));
            Check(lines);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true));
            int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M06_BLK ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int, int, ulong)[]
            {
                (0,0,0),(0,1,0x10411af4),(0,2,0),(0,2,0x100001),(0,3,0),
                (1,0,0x407),(1,1,256),(1,2,1),(1,3,0x8000),(1,3,0x800),
                (2,0,8192),(2,1,3),(2,2,55),(2,3,1),
                (3,0,0x100000000),(3,1,0x100000000),(3,1,0x100000021),(3,2,15),(3,3,0),
                (4,0,0),(4,1,0),(4,2,0),(4,3,1),
                (5,0,0),(5,0,1),(5,1,0),(5,2,0),(5,3,8192),
                (6,0,16),(6,1,130),(6,2,150),(6,3,16),(7,0,0),(7,1,0),(7,2,0),(7,3,0),
                (8,0,1),(8,1,0),(8,1,3),(8,1,4),(8,1,65536),(8,2,16),(8,3,0),
                (9,0,0),(9,1,0),(9,2,0),(9,3,1),(10,0,0),(10,1,0),(10,2,0),(10,3,1),
                (11,0,0),(11,1,0),(11,2,999),(11,3,0),(12,0,0x407),(12,1,11),(12,2,0x8000),(12,3,0)
            })
            {
                string[] changed = [.. lines];
                string[] parts = changed[indices[row]].Split(' ');
                parts[col + 2] = "0x" + wrong.ToString("X16");
                changed[indices[row]] = string.Join(' ', parts);
                Reject(() => Check(changed));
            }
            // Consistent address mutations must still be tied to the original allocator.
            string[] outside = [.. lines];
            outside[indices[5]] = "M06_BLK 5 0x0000000000100000 0x0000000000100000 0x0000000000100000 0x0000000000001000";
            outside[indices[9]] = "M06_BLK 9 0x0000000000100000 0x0000000000100080 0x0000000000100094 0x0000000000000000";
            Reject(() => Check(outside));
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

    internal static string BlockFrame(string[] lines)
    {
        const string screen = "\u001b[2J\u001b[01;01H\u001b[=3h\u001b[2J\u001b[01;01H";
        string prefix = screen + screen + screen +
            "BdsDxe: loading Boot0001 \"UEFI Misc Device\" from PciRoot(0x0)/Pci(0x6,0x0)\r\n" +
            "BdsDxe: starting Boot0001 \"UEFI Misc Device\" from PciRoot(0x0)/Pci(0x6,0x0)\r\n";
        HostCheck.Equal(QemuContracts.BlockFirmwarePrefixBytes, prefix.Length);
        return prefix + string.Join("\r\n", lines) + "\r\n";
    }

    internal static string[] Capture()
    {
        string[] lines = QemuContracts.M06VirtioBlockQueueSetupMarkers.ToArray();
        M05VirtioNegotiationEfiIntegrationTests.Capture()[..QemuContracts.M05PciEnumerationMarkers.Count].CopyTo(lines, 0);
        ulong[] owner = lines[67].Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        ulong page = owner[2];
        ulong[][] values = [[0x600,0x10421af4,0xc000000000,0x407],[0x403,15,0,2],[0x4000,4,56,8],
            [0x100000020,0x100000020,11,1],[3,5,6,0],[page,page,page,4096],[0,128,148,8],[256,272,512,512],
            [0,256,8,1],[page,page+128,page+148,0],[1,1,1,2],[owner[4],owner[4],owner[6],1],[0x403,0,2,1]];
        int stage = 0;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("M06_BLK ", StringComparison.Ordinal))
            {
                lines[i] = "M06_BLK " + stage.ToString("X") + string.Concat(values[stage].Select(v => " 0x" + v.ToString("X16")));
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
        HostCheck.True(rejected, "invalid block profile or evidence must be rejected");
    }
}
