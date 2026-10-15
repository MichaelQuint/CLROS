using System.Buffers.Binary;
using OS.Net;
using OS.Tooling;

internal static class ContinuingNetworkTranscriptTests
{
    [HostTest("m07-026 transcript rejects missing reordered forged and mismatched parent rows", HostLane.Fast, 0)]
    private static void Transcript()
    {
        string[] rows = Records();
        HostCheck.Equal(rows.Length - 10, QemuTranscripts.ValidateNetworkServiceTail(rows).Length);
        foreach (int index in Enumerable.Range(rows.Length - 10, 10))
        {
            string[] bad = (string[])rows.Clone(); bad[index] += " extra";
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
        Reject(() => QemuTranscripts.ValidateNetworkServiceTail(rows[..^1]));
        Reject(() => QemuTranscripts.ValidateNetworkServiceTail([.. rows, rows[^1]]));
        for (int stage = 0; stage < 7; stage++)
        {
            string[] bad = (string[])rows.Clone();
            bad[rows.Length - 9 + stage] = bad[rows.Length - 9 + stage].Replace("stage=" + stage, "stage=" + ((stage + 1) % 7));
            Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
        }
        foreach ((string from, string to) in new[] { ("a=20", "a=19"), ("b=32", "b=31"), ("d=90525193470466", "d=90525193470467"), (" c=0 d=", " c=1 d="), ("a=8 b=0 c=8", "a=8 b=1 c=8") })
        {
            string[] bad = rows.Select(x => x.Replace(from, to, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!bad.SequenceEqual(rows)); Reject(() => QemuTranscripts.ValidateNetworkServiceTail(bad));
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
            void Inspect(List<byte[]> values) { File.WriteAllBytes(capture, Pcap(values)); QemuTranscripts.AssertM07026Capture(serial, capture); }
            Inspect(frames);
            Reject(() => Inspect(frames.Skip(1).ToList()));
            Reject(() => Inspect(frames.Take(frames.Count - 1).ToList()));
            var duplicate = frames.ToList(); duplicate.Add(frames[^1]); Reject(() => Inspect(duplicate));
            foreach (int offset in new[] { 0, 6, 24, 36, 38, 40, 42 })
            {
                var bad = frames.Select(x => (byte[])x.Clone()).ToList(); bad[^1][offset] ^= 1; Reject(() => Inspect(bad));
            }
            var swapped = frames.ToList(); (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]); Reject(() => Inspect(swapped));
        }
        finally { Directory.Delete(directory, true); }
    }

    [HostTest("m07-026 full parser preserves exact package live-clock and original network parents", HostLane.Fast, 2)]
    private static void FullTranscript()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-net-parents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] package = LinuxElfPackageTests.CapturePackage(); var view = OS.Manifest.LinuxElfPackage.Validate(package);
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
            string[] rows = Records(); int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
            staged.CopyTo(rows, owned); service.CopyTo(rows, owned + staged.Length);
            string serial = Path.Combine(directory, "serial.log"), packagePath = Path.Combine(directory, "app.pkg");
            File.WriteAllBytes(packagePath, package);
            void Inspect(string[] values)
            {
                File.WriteAllText(serial, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(values));
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(serial, firstRealExit:true, ownedSurvival:true, pciEnumeration:true,
                    virtioNegotiation:true, virtqueuePolling:true, virtioNet:true, arpReceive:true,
                    linuxElfPackagePath:packagePath, workloadServiceLoop:true, networkService:true);
            }
            Inspect(rows);
            foreach (int index in new[] { owned + 2, owned + 7 + 1, owned + 7 + 6 })
            {
                string[] bad = (string[])rows.Clone(); bad[index] += " extra"; Reject(() => Inspect(bad));
            }
            package[^1] ^= 1; File.WriteAllBytes(packagePath, package); Reject(() => Inspect(rows));
        }
        finally { Directory.Delete(directory, true); }
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
            "TEST_START m07-026", "M07_026_ROW stage=0 a=2097152 b=32768 c=4295000064 d=8",
            "M07_026_ROW stage=1 a=8 b=8 c=1 d=0", "M07_026_ROW stage=2 a=20 b=20 c=20 d=90525193470466",
            "M07_026_ROW stage=3 a=12 b=32 c=32 d=2000000050", "M07_026_ROW stage=4 a=32 b=32 c=1 d=2",
            "M07_026_ROW stage=5 a=8 b=0 c=8 d=1", $"M07_026_ROW stage=6 a={allocator[0]} b={allocator[1]} c={allocator[2]} d={command}",
            "CLROS_M07_026_READY", "TEST_PASS m07-026"];
    }

    private static List<byte[]> Frames()
    {
        byte[] anchor = new byte[60]; Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor, 0);
        "CLROS-M07"u8.CopyTo(anchor.AsSpan(14)); var frames = new List<byte[]> { anchor };
        var localMac = new MacAddress(2, 0, 0, 0, 0, 1); var peerMac = new MacAddress(0x52, 0x55, 10, 0, 2, 2);
        var local = new Ipv4Address(10, 0, 2, 15); var peer = new Ipv4Address(10, 0, 2, 2);
        for (int i = 0; i < 21; i++)
        {
            byte[] request = new byte[60], reply = new byte[60];
            EthernetHeader.TryWrite(request, new(new MacAddress(255, 255, 255, 255, 255, 255), localMac, 0x806));
            EthernetHeader.TryWrite(reply, new(localMac, peerMac, 0x806));
            ArpPacket.TryWrite(request.AsSpan(14), new(1, localMac, local, default, peer), out _);
            ArpPacket.TryWrite(reply.AsSpan(14), new(2, peerMac, peer, localMac, local), out _);
            frames.Add(request); frames.Add(reply);
        }
        for (ushort i = 0; i < 12; i++)
        {
            byte[] request = new byte[66], reply = new byte[66];
            Ipv4IcmpEchoFrame.TryWriteEchoRequest(request, new(peerMac, localMac, local, peer), 226, i, "CLROS continuing network"u8, out int a);
            Ipv4IcmpEchoFrame.TryWriteEchoReply(reply, new(localMac, peerMac, peer, local), 226, i, "CLROS continuing network"u8, out int b);
            HostCheck.True(a == 66 && b == 66); frames.Add(request); frames.Add(reply);
        }
        return frames;
    }
    private static byte[] Pcap(List<byte[]> frames)
    {
        using var stream = new MemoryStream();
        stream.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        Span<byte> header = stackalloc byte[16];
        foreach (byte[] frame in frames)
        {
            header.Clear(); BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)frame.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header[12..], (uint)frame.Length); stream.Write(header); stream.Write(frame);
        }
        return stream.ToArray();
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (ToolFailure) { return; } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("m07_026_invalid_evidence_accepted");
    }
}
