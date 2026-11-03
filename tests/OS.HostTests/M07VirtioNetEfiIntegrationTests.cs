using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M07VirtioNetEfiIntegrationTests
{
    private static readonly string[] Flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
        "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe",
        "--EnableM05PciEnumerationProbe", "--EnableM05VirtioNegotiationProbe", "--EnableM05VirtqueuePollingProbe", "--EnableM07VirtioNetProbe"];

    [HostTest("m07.net.efi requires original polling and defaults off", HostLane.Fast, 0)]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM07VirtioNetProbe && !BootSequence.RunOwnedVirtioNet(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M07VirtioNet"));
            Reject(() => EfiOptions.Parse(Flags.Where(f => f != "--EnableM05VirtqueuePollingProbe").ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string conflict in new[] { "M05ApicSetup", "M05VirtqueueInterrupt", "M06VirtioBlockQueueSetup", "M06BlockMsix" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + conflict + "Probe"], build).Profile());
        }
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--RequireMarker", "CLROS_M07_005_READY")));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m07.net.efi hands strict markers and fresh Ethernet capture to the runner", HostLane.Fast, 1)]
    private static void Runner()
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--DryRun", "--BuildEfi", .. Flags]);
        HostCheck.True(result.ExitCode == 0, result.Error + result.Output);
        string markers = string.Join(';', QemuContracts.M07VirtioNetMarkers);
        foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
            HostCheck.True(result.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
        HostCheck.True(result.Output.Contains("filter-dump,id=clroscapture,netdev=n0,file=", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("qemu-smoke.log.pcap,maxlen=65536", StringComparison.Ordinal));
        HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
        string directory = Path.Combine(Path.GetTempPath(), "clros-m07-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string serial = Path.Combine(directory, "serial.log");
            File.WriteAllBytes(serial + ".pcap", [1]);
            var stale = HostToolProcess.RunTool(["qemu", "smoke", "--DryRun", "--BuildEfi", "--SerialLog", serial, .. Flags]);
            HostCheck.True(stale.ExitCode != 0 && (stale.Output + stale.Error).Contains("fresh file path", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [HostTest("m07.net.efi validates every child row and preserves strict parent replay", HostLane.Fast, 2)]
    private static void Transcript()
    {
        string[] parent = M05VirtqueuePollingEfiIntegrationTests.Capture();
        ulong[] Parent(string prefix) => parent.Single(l => l.StartsWith(prefix + " ", StringComparison.Ordinal))
            .Split(' ')[2..].Select(v => Convert.ToUInt64(v[2..], 16)).ToArray();
        ulong page = Parent("M05_QUEUE 2")[0], command = Parent("M05_VIRTIO 9")[1];
        ulong bar = Parent("M05_VIRTIO 0")[2], notify = Parent("M05_VIRTIO 1")[1];
        ulong[][] rows = [
            [0x10411af4,0x600,command,bar], [Parent("M05_VIRTIO 7")[0],0x100000000,11,1],
            [0,256,8,0],[1,256,8,1],[page,page,page,4096],
            [page,page+128,page+148,page+256], [page+2048,page+2176,page+2196,page+2304],
            [1526,2,1,1], [notify,notify+4,4,72], [1,0,0,1], [1,7,1,3],
            [1,1,1,command], Parent("M05_QUEUE 6"), [0xd24c49dd,60,0,12]
        ];
        string[] lines = [.. parent, .. QemuContracts.M07VirtioNetMarkers.Skip(parent.Length)];
        for (int i = 0; i < rows.Length; i++)
        {
            string prefix = "M07_NET " + i.ToString("X");
            int index = Array.FindIndex(lines, l => l == prefix);
            lines[index] = prefix + string.Concat(rows[i].Select(v => " 0x" + v.ToString("X16")));
        }
        HostCheck.Equal(0xd24c49ddU, QemuTranscripts.AssertM07VirtioNetRecords(lines));
        HostCheck.True(QemuTranscripts.NormalizeM07VirtioNetRecords(lines).SequenceEqual(parent));
        string directory = Path.Combine(Path.GetTempPath(), "clros-m07-net-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string serial = Path.Combine(directory, "serial.log");
            File.WriteAllText(serial, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(lines));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(serial, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioNegotiation: true, virtqueuePolling: true, virtioNet: true);
            for (int stage = 0; stage < rows.Length; stage++)
            {
                int field = stage is 2 or 3 ? 2 : stage == 10 ? 1 : 0;
                string[] changed = (string[])lines.Clone();
                string prefix = "M07_NET " + stage.ToString("X");
                int index = Array.FindIndex(changed, l => l.StartsWith(prefix + " ", StringComparison.Ordinal));
                ulong[] values = (ulong[])rows[stage].Clone();
                values[field] ^= 1;
                changed[index] = prefix + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
                Reject(() => QemuTranscripts.AssertM07VirtioNetRecords(changed));
            }
            Reject(() => QemuTranscripts.AssertM07VirtioNetRecords(lines[..^1]));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
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
        HostCheck.True(rejected);
    }
}
