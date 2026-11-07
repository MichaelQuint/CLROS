using System.Buffers.Binary;
using OS.Net;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class ContinuingNetworkTranscriptTests
{
    [HostTest("m07-026 zero-window corpus requires the retained octet and rejects a full retry", HostLane.Fast, 3)]
    private static void ZeroWindowCorpus()
    {
        ContinuingTcpScenario.Packet[] corpus = [.. ContinuingTcpScenario.Packets];
        int index = Array.FindIndex(corpus, packet => packet.Tag == "data.retry");
        HostCheck.True(index > 0 && !corpus[index - 1].FromGuest && corpus[index - 1].Window == 0);
        var first = corpus.Single(packet => packet.Tag == "data.first");
        var probe = corpus[index];
        HostCheck.True(first.Payload == "ABCD" && probe.Payload == "A" && probe.Sequence == first.Sequence && probe.Ack == first.Ack);

        // Literal original3bdc Network frames104/108. These independently pin
        // the retained octet instead of deriving both sides from the corpus.
        byte[] original = Convert.FromHexString("02000000000202000000000108004500002C00000000400662BC0A00020F0A0002021F909C40000003E900002721501800042C53000041424344");
        byte[] retained = Convert.FromHexString("02000000000202000000000108004500002900000000400662BF0A00020F0A0002021F909C40000003E900002721501800046FDC000041");
        ContinuingTcpScenario.Validate(original, first, 0x020000000002);
        ContinuingTcpScenario.Validate(retained, probe, 0x020000000002);
        Reject(() => ContinuingTcpScenario.Validate(original, probe, 0x020000000002));
        Reject(() => ContinuingTcpScenario.Validate(ContinuingTcpScenario.Write(probe with { Payload = "B" }, 0x020000000002), probe, 0x020000000002));
    }

    [HostTest("m07-026 transcript rejects missing reordered forged and mismatched parent rows", HostLane.Fast, 0)]
    private static void Transcript()
    {
        string[] rows = Records();
        int tail = 10 + QemuTranscripts.M07026TcpRecords + QemuTranscripts.M07026SchedulerStages, start = rows.Length - tail;
        HostCheck.Equal(start, QemuTranscripts.ValidateNetworkServiceTail(rows).Length);
        foreach (int index in Enumerable.Range(start, tail))
        {
            string[] bad = (string[])rows.Clone();
            bad[index] += " extra";
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
        Reject(() => QemuTranscripts.ValidateNetworkServiceTail(rows[..^1]));
        Reject(() => QemuTranscripts.ValidateNetworkServiceTail([.. rows, rows[^1]]));
        for (int stage = 0; stage < 7; stage++)
        {
            string[] bad = (string[])rows.Clone();
            int index = start + 2 + stage + (stage >= 4 ? QemuTranscripts.M07026TcpRecords : 0);
            bad[index] = bad[index].Replace("stage=" + stage, "stage=" + ((stage + 1) % 7));
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
        foreach ((string from, string to) in new[] { ("a=20", "a=19"), ("b=32", "b=31"), ("d=90525193470466", "d=90525193470467"), (" c=0 d=", " c=1 d="), ("a=8 b=0 c=8", "a=8 b=1 c=8") })
        {
            string[] bad = rows.Select(x => x.Replace(from, to, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!bad.SequenceEqual(rows));
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
        foreach ((string from, string to) in new[] { ("a=4194304 b=32", "a=2097152 b=32"),
            ("a=2400 b=200 c=2200 d=100000000", "a=2400 b=200 c=0 d=100000000"),
            ("a=2400 b=200 c=2200 d=100000000", "a=2399 b=200 c=2200 d=100000000"),
            ("d=100000000", "d=1000000001"), ("a=4802 b=1 c=0 d=32", "a=4800 b=1 c=0 d=32"),
            ("a=4802 b=1 c=0 d=32", "a=4802 b=0 c=0 d=32") })
        {
            string[] bad = rows.Select(x => x.Replace(from, to, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!bad.SequenceEqual(rows));
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
    }

    [HostTest("m07-026 independent pcap requires all ARP and tagged checksum-valid ordered echo pairs", HostLane.Fast, 1)]
    private static void Capture()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-continuing-net-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string serial = Path.Combine(directory, "serial.log"), capture = Path.Combine(directory, "ethernet.pcap");
            File.WriteAllLines(serial, Records());
            var frames = Frames();
            void Inspect(List<byte[]> values)
            {
                File.WriteAllBytes(capture, Pcap(values));
                QemuTranscripts.AssertM07026Capture(serial, capture);
            }
            Inspect(frames);
            Reject(() => Inspect(frames.Skip(1).ToList()));
            Reject(() => Inspect(frames.Where((_, i) => i != 1).ToList()));
            var badPrefix = frames.Select(x => (byte[])x.Clone()).ToList();
            badPrefix[0][14] = 1;
            Reject(() => Inspect(badPrefix));
            var duplicatePrefix = frames.ToList();
            duplicatePrefix.Insert(1, frames[0]);
            Reject(() => Inspect(duplicatePrefix));
            Reject(() => Inspect(frames.Take(frames.Count - 1).ToList()));
            var duplicate = frames.ToList();
            duplicate.Add(frames[^1]);
            Reject(() => Inspect(duplicate));
            foreach (int offset in new[] { 0, 6, 24, 36, 38, 40, 42 })
            {
                var bad = frames.Select(x => (byte[])x.Clone()).ToList();
                bad[^1][offset] ^= 1;
                Reject(() => Inspect(bad));
            }
            var swapped = frames.ToList();
            (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]);
            Reject(() => Inspect(swapped));
            File.WriteAllBytes(capture, Pcap(frames, compressTimers: true));
            Reject(() => QemuTranscripts.AssertM07026Capture(serial, capture));
            int firstTcp = frames.Count - ContinuingTcpScenario.Packets.Count - 2;
            foreach (int offset in new[] { 34, 38, 42, 46, 47, 48, 50, 54 })
            {
                var bad = frames.Select(x => (byte[])x.Clone()).ToList();
                bad[firstTcp + 1][offset] ^= 1;
                Reject(() => Inspect(bad));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [HostTest("m07-026 full parser preserves exact package live-clock and original network parents", HostLane.Fast, 2)]
    private static void FullTranscript()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-net-parents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] package = LinuxElfPackageTests.CapturePackage();
            var view = OS.Manifest.LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded);
            string Digest(ReadOnlySpan<byte> data) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
            string[] staged = ["TEST_START m16-002", $"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length + 4095) / 4096} handlesClosed=2 reads=1",
                "M16_002_DIGEST " + Digest(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes)), "M16_002_PACKAGE_DIGEST " + Digest(package),
                "M16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false", "CLROS_M16_002_READY", "TEST_PASS m16-002"];
            string[] service = ["TEST_START m16-003", "M16_003_CLOCK frequency=10000000 period=100000000 recoveries=0 source=owned-hpet",
                .. Enumerable.Range(1, 4).Select(i => $"M16_003_ITERATION index={i} ticks={i * 10000 + 50}"),
                .. Enumerable.Range(0, 8).Select(i => $"M16_003_FIRMWARE_DENIED index={i} status=ServicesUnavailable providers=1 outputs=unchanged"),
                "M16_003_STOP iterations=4 reads=65 first=50 last=40050 interval=10000 firmware=revoked providers=1 shutdown=owned-controller",
                "CLROS_M16_003_READY", "TEST_PASS m16-003"];
            string[] rows = Records();
            int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
            staged.CopyTo(rows, owned);
            service.CopyTo(rows, owned + staged.Length);
            string serial = Path.Combine(directory, "serial.log"), packagePath = Path.Combine(directory, "app.pkg");
            File.WriteAllBytes(packagePath, package);
            void Inspect(string[] values)
            {
                File.WriteAllText(serial, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(values));
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(serial, firstRealExit: true, ownedSurvival: true, pciEnumeration: true,
                    virtioNegotiation: true, virtqueuePolling: true, virtioNet: true, arpReceive: true,
                    linuxElfPackagePath: packagePath, workloadServiceLoop: true, networkService: true);
            }
            Inspect(rows);
            string pcap = Path.Combine(directory, "ethernet.pcap");
            File.WriteAllBytes(pcap, Pcap(Frames()));
            var replay = HostToolProcess.RunTool("qemu", "validate-network", serial, pcap, packagePath);
            HostCheck.True(replay.ExitCode == 0 && replay.Output.Contains("TEST_PASS qemu.network.capture", StringComparison.Ordinal), replay.Error + replay.Output);
            File.Copy(pcap, serial + ".pcap");
            ContinuingNetworkRunnerTranscriptControls.Run(directory, serial, rows, packagePath);
            foreach (int index in new[] { owned + 2, owned + 7 + 1, owned + 7 + 6 })
            {
                string[] bad = (string[])rows.Clone();
                bad[index] += " extra";
                Reject(() => Inspect(bad));
            }
            package[^1] ^= 1;
            File.WriteAllBytes(packagePath, package);
            Reject(() => Inspect(rows));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string[] Records()
    {
        string[] parent = M07ArpReceiveEfiIntegrationTests.Capture();
        ulong[] allocator = parent.Single(x => x.StartsWith("M07_ARP A ", StringComparison.Ordinal)).Split(' ')[2..].Select(x => Convert.ToUInt64(x[2..], 16)).ToArray();
        ulong command = Convert.ToUInt64(parent.Single(x => x.StartsWith("M07_ARP 0 ", StringComparison.Ordinal)).Split(' ')[4][2..], 16);
        int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
        // This fixture checks only the new leaf and existing ARP parent. Real
        // package/service records are checked separately by the full M03 parser.
        return [.. parent.Take(owned), .. Enumerable.Repeat("parent validated separately", 24), .. parent.Skip(owned),
            "TEST_START m07-026", "M07_026_SCHED stage=0 a=4194304 b=32 c=1 d=0", "M07_026_ROW stage=0 a=2097152 b=32768 c=4295000064 d=8",
            "M07_026_ROW stage=1 a=8 b=8 c=1 d=0", "M07_026_ROW stage=2 a=20 b=20 c=20 d=90525193470466",
            "M07_026_ROW stage=3 a=12 b=32 c=32 d=2000000050", .. QemuTranscripts.M07026TcpStarts,
            "M07_026_TCP stage=0 a=2 b=1 c=4 d=2", "M07_026_TCP stage=1 a=2 b=1 c=1 d=6",
            "M07_026_TCP stage=2 a=241000000000 b=241001000000 c=1001000000 d=240000000000",
            "M07_026_TCP stage=3 a=11 b=4 c=10017 d=4", "M07_026_TCP stage=4 a=20 b=1021 c=1 d=23",
            "M07_026_TCP stage=5 a=20 b=15 c=1 d=1", "M07_026_TCP stage=6 a=2 b=3 c=4 d=8",
            "M07_026_TCP stage=7 a=2 b=6000000000 c=13000000000 d=1",
            "M07_026_TCP stage=8 a=3 b=24000000000 c=24000000000 d=1",
            "M07_026_TCP stage=9 a=241001000000 b=241002000000 c=0 d=5",
            $"M07_026_TCP stage=10 a=31 b={ContinuingTcpScenario.Packets.Count(x => x.FromGuest)} c={33 + ContinuingTcpScenario.Packets.Count(x => !x.FromGuest)} d={33 + ContinuingTcpScenario.Packets.Count(x => x.FromGuest)}",
            .. QemuTranscripts.M07026TcpEnds,
            $"M07_026_ROW stage=4 a={33 + ContinuingTcpScenario.Packets.Count(x => !x.FromGuest)} b={33 + ContinuingTcpScenario.Packets.Count(x => x.FromGuest)} c=1 d=2",
            "M07_026_ROW stage=5 a=8 b=0 c=8 d=1", $"M07_026_ROW stage=6 a={allocator[0]} b={allocator[1]} c={allocator[2]} d={command}",
            "M07_026_SCHED stage=1 a=2400 b=200 c=2200 d=100000000", "M07_026_SCHED stage=2 a=4802 b=1 c=0 d=32",
            "CLROS_M07_026_READY", "TEST_PASS m07-026"];
    }

    private static List<byte[]> Frames()
    {
        byte[] anchor = new byte[60];
        Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor, 0);
        "CLROS-M07"u8.CopyTo(anchor.AsSpan(14));
        byte[] warmup = (byte[])anchor.Clone();
        warmup.AsSpan(14).Clear();
        var frames = new List<byte[]> { warmup, anchor };
        var localMac = new MacAddress(2, 0, 0, 0, 0, 1);
        var peerMac = new MacAddress(0x52, 0x55, 10, 0, 2, 2);
        var local = new Ipv4Address(10, 0, 2, 15);
        var peer = new Ipv4Address(10, 0, 2, 2);
        for (int i = 0; i < 21; i++)
        {
            byte[] request = new byte[60], reply = new byte[60];
            EthernetHeader.TryWrite(request, new(new MacAddress(255, 255, 255, 255, 255, 255), localMac, 0x806));
            EthernetHeader.TryWrite(reply, new(localMac, peerMac, 0x806));
            ArpPacket.TryWrite(request.AsSpan(14), new(1, localMac, local, default, peer), out _);
            ArpPacket.TryWrite(reply.AsSpan(14), new(2, peerMac, peer, localMac, local), out _);
            frames.Add(request);
            frames.Add(reply);
        }
        for (ushort i = 0; i < 12; i++)
        {
            byte[] request = new byte[66], reply = new byte[66];
            Ipv4IcmpEchoFrame.TryWriteEchoRequest(request, new(peerMac, localMac, local, peer), 226, i, "CLROS continuing network"u8, out int a);
            Ipv4IcmpEchoFrame.TryWriteEchoReply(reply, new(localMac, peerMac, peer, local), 226, i, "CLROS continuing network"u8, out int b);
            HostCheck.True(a == 66 && b == 66);
            frames.Add(request);
            frames.Add(reply);
        }
        const ulong peerAddress = 0x52550a000202;
        foreach (var packet in ContinuingTcpScenario.Packets)
            frames.Add(ContinuingTcpScenario.Write(packet, peerAddress));
        byte[] finalRequest = new byte[60], finalReply = new byte[60];
        Ipv4IcmpEchoFrame.TryWriteEchoRequest(finalRequest, new(peerMac, localMac, local, peer), 227, 0, "CLROS TCP closed"u8, out _);
        Ipv4IcmpEchoFrame.TryWriteEchoReply(finalReply, new(localMac, peerMac, peer, local), 227, 0, "CLROS TCP closed"u8, out _);
        frames.Add(finalRequest);
        frames.Add(finalReply);
        return frames;
    }
    private static byte[] Pcap(List<byte[]> frames, bool compressTimers = false)
    {
        using var stream = new MemoryStream();
        stream.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        Span<byte> header = stackalloc byte[16];
        ulong timestamp = 0, waitFin = 0;
        int tcpStart = frames.Count - ContinuingTcpScenario.Packets.Count - 2;
        for (int index = 0; index < frames.Count; index++)
        {
            byte[] frame = frames[index];
            timestamp += 100;
            if (!compressTimers && index >= tcpStart && index < frames.Count - 2)
            {
                var packet = ContinuingTcpScenario.Packets[index - tcpStart];
                timestamp += (ulong)packet.DelayMilliseconds * 1000;
                timestamp += packet.Tag switch
                {
                    "syn.retry" or "loss.retry1" or "pending.retry1" or "pending.retry2" => 1000000UL,
                    "data.retry" => 3000000UL,
                    "loss.retry2" => 2000000UL,
                    _ => 0
                };
                if (packet.Tag == "wait.fin")
                    waitFin = timestamp;
            }
            if (!compressTimers && index == frames.Count - 2)
                timestamp = Math.Max(timestamp, waitFin + 240005000);
            header.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)(timestamp / 1000000));
            BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(timestamp % 1000000));
            BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)frame.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header[12..], (uint)frame.Length);
            stream.Write(header);
            stream.Write(frame);
        }
        return stream.ToArray();
    }
    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            return;
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("m07_026_invalid_evidence_accepted");
    }
}
