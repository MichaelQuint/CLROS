using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M07ArpReceiveEfiIntegrationTests
{
    internal static readonly string[] Flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
        "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe", "--EnableM03OwnedSubsystemSurvivalProbe",
        "--EnableM05PciEnumerationProbe", "--EnableM05VirtioNegotiationProbe", "--EnableM05VirtqueuePollingProbe",
        "--EnableM07VirtioNetProbe", "--EnableM07ArpReceiveProbe"];

    [HostTest("m07.ARP EFI requires original net chain and defaults off", HostLane.Fast, 0)]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM07ArpReceiveProbe && !BootSequence.RunOwnedArpReceive(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M07ArpReceive"));
            foreach (string missing in new[] { "M07VirtioNet", "M05VirtqueuePolling", "M05VirtioNegotiation", "M05PciEnumeration" })
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != "--Enable" + missing + "Probe").ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--RequireMarker", "CLROS_M07_006_READY")));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--RequireMarker", "M07_ARP 8")));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m07.ARP EFI runner selects generated MAC and complete strict protocol", HostLane.Fast, 1)]
    private static void Runner()
    {
        var r = HostToolProcess.RunTool(["qemu", "smoke", "--DryRun", "--BuildEfi", .. Flags]);
        HostCheck.True(r.ExitCode == 0, r.Error + r.Output);
        string markers = string.Join(';', QemuContracts.M07ArpReceiveMarkers);
        foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
            HostCheck.True(r.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
        HostCheck.True(r.Output.Contains(",mac=02:00:00:00:00:01", StringComparison.Ordinal) &&
            r.Output.Contains("filter-dump,id=clroscapture,netdev=n0,file=", StringComparison.Ordinal));
    }

    [HostTest("m07.ARP EFI transcript guards every row marker and parent replay", HostLane.Fast, 2)]
    private static void Transcript()
    {
        string[] lines = Capture();
        var receipt = QemuTranscripts.AssertM07ArpReceiveRecords(lines);
        HostCheck.True(receipt.ReceiveBytes == 42 && receipt.PeerMac == 0x52550a000202);
        string[] padded = (string[])lines.Clone();
        int usedRow = Array.FindIndex(padded, l => l.StartsWith("M07_ARP 5 ", StringComparison.Ordinal));
        int crcRow = Array.FindIndex(padded, l => l.StartsWith("M07_ARP 8 ", StringComparison.Ordinal));
        padded[usedRow] = Row("M07_ARP 5", [1,0,76,1]);
        padded[crcRow] = Row("M07_ARP 8", [receipt.ReceiveCrc32,64,receipt.RequestCrc32,60]);
        HostCheck.True(QemuTranscripts.AssertM07ArpReceiveRecords(padded).ReceiveBytes == 64);
        padded[usedRow] = Row("M07_ARP 5", [1,0,77,1]);
        padded[crcRow] = Row("M07_ARP 8", [receipt.ReceiveCrc32,65,receipt.RequestCrc32,60]);
        Reject(() => QemuTranscripts.AssertM07ArpReceiveRecords(padded));
        string directory = Path.Combine(Path.GetTempPath(), "clros-m07-arp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "serial.log");
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(lines));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit:true, ownedSurvival:true,
                pciEnumeration:true, virtioNegotiation:true, virtqueuePolling:true, virtioNet:true, arpReceive:true);
            for (int stage = 0; stage < 14; stage++)
            {
                // CRC fields are independently associated by PCAP admission;
                // mutate the length here. Peer MAC's cache shape is bounded;
                // mutate count here. Poll count may vary; mutate fence count.
                int field = stage == 8 ? 1 : stage == 11 ? 1 : 0;
                string prefix = "M07_ARP " + stage.ToString("X");
                string[] changed = (string[])lines.Clone();
                int index = Array.FindIndex(changed, l => l.StartsWith(prefix + " ", StringComparison.Ordinal));
                ulong[] values = changed[index].Split(' ')[2..].Select(v => Convert.ToUInt64(v[2..],16)).ToArray();
                values[field] ^= 1;
                changed[index] = Row(prefix, values);
                Reject(() => QemuTranscripts.AssertM07ArpReceiveRecords(changed));
            }
            Reject(() => QemuTranscripts.AssertM07ArpReceiveRecords(lines[..^1]));
            string[] duplicate = [.. lines, lines[^1]];
            Reject(() => QemuTranscripts.AssertM07ArpReceiveRecords(duplicate));
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static string[] Capture()
    {
        string[] polling = M05VirtqueuePollingEfiIntegrationTests.Capture();
        ulong[] Parent(string prefix) => polling.Single(l => l.StartsWith(prefix + " ", StringComparison.Ordinal))
            .Split(' ')[2..].Select(v => Convert.ToUInt64(v[2..],16)).ToArray();
        // Offer the receive feature while the original parent still selects
        // VERSION_1 only. Keep both negotiated receipts independent.
        ulong[] offered = Parent("M05_VIRTIO 7"); offered[0] |= 1UL << 15;
        polling[Array.FindIndex(polling, l => l.StartsWith("M05_VIRTIO 7 ", StringComparison.Ordinal))] = Row("M05_VIRTIO 7", offered);
        ulong page = Parent("M05_QUEUE 2")[0], command = Parent("M05_VIRTIO 9")[1];
        ulong bar = Parent("M05_VIRTIO 0")[2], notify = Parent("M05_VIRTIO 1")[1];
        ulong[][] netRows = [
            [0x10411af4,0x600,command,bar],[Parent("M05_VIRTIO 7")[0],0x100000000,11,1],
            [0,256,8,0],[1,256,8,1],[page,page,page,4096],
            [page,page+128,page+148,page+256],[page+2048,page+2176,page+2196,page+2304],
            [1526,2,1,1],[notify,notify+4,4,72],[1,0,0,1],[1,7,1,3],
            [1,1,1,command],Parent("M05_QUEUE 6"),[0xd24c49dd,60,0,12]
        ];
        ulong[][] arpRows = [netRows[0],[netRows[1][0],QemuContracts.M07ArpReceiveFeatures,11,1],netRows[4],netRows[5],netRows[6],
            [1,0,54,1],[12,1,0,QemuContracts.M07ArpReceiveFeatures],[1,2,0x52550a000202,100],
            [0x12345678,42,0x87654321,60],[0x7f,2,42,1],netRows[12],[1,8,1,3],netRows[11],
            [15,QemuContracts.M07ArpReceiveFeatures,0x0a000202,0x0a00020f]];
        string[] lines = [.. polling, .. QemuContracts.M07ArpReceiveMarkers.Skip(polling.Length)];
        foreach ((string prefix, ulong[][] rows) in new[] { ("M07_NET ",netRows), ("M07_ARP ",arpRows) })
            for (int stage = 0; stage < rows.Length; stage++)
            {
                string key = prefix + stage.ToString("X"); int index = Array.FindIndex(lines,l => l == key);
                lines[index] = Row(key,rows[stage]);
            }
        return lines;
    }
    private static string Row(string prefix, ulong[] values) => prefix + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
