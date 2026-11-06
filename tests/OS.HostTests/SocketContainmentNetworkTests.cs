using System.Globalization;
using OS.Tooling;

internal static class SocketContainmentNetworkTests
{
    private const string Peer = "SOCKET_CONTAINMENT_PEER_RESULT complete=True stopped=True arpReplies=2 headTransmissions=8 healthyReplies=30 vlanFrames=1";
    private static string Fields(int role, ulong clock) => FormattableString.Invariant(
        $"slot={role} owner=4096 epoch=1 generation=1 index={role} local=167772687 localPort=8080 remote=167772674 remotePort={41000 + role} clock={clock}");
    private static string[] Rows(int startupRetries = 0)
    {
        var reference = SocketContainmentPacketTests.ReferenceCapture(startupRetries);
        var rows = NativeAotSocketIntegrationTests.Envelope(false).ToList();
        void HexRow(string prefix, params ulong[] fields)
        {
            int at = rows.FindIndex(row => row.StartsWith(prefix + " ", StringComparison.Ordinal));
            rows[at] = prefix + string.Concat(fields.Select(value => " 0x" + value.ToString("X16", CultureInfo.InvariantCulture)));
        }
        HexRow("M07_ARP 7", 1, 2, reference.Arp.PeerMac, 100);
        HexRow("M07_ARP 5", 1, 0, 72, 1);
        HexRow("M07_ARP 8", reference.Arp.ReceiveCrc32, 60, reference.Arp.RequestCrc32, 60);
        rows[rows.FindIndex(row => row.StartsWith("M16_011_PEER ", StringComparison.Ordinal))] = "M16_011_PEER mac=2199023255554 sent=1 received=1 start=0 end=0";
        int start = rows.IndexOf("TEST_START m16-009"), end = rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(start + 1, end - start - 1);
        rows.InsertRange(start + 1, [
            "M16_011_SLOT event=0 "+Fields(0,1000)+" deadline=0 duration=0",
            "M16_011_SLOT event=0 "+Fields(1,2000)+" deadline=0 duration=0",
            "M16_011_SLOT event=0 "+Fields(2,3000)+" deadline=0 duration=0",
            "M07_028_DROP before=0 after=1 availableBefore=58 availableAfter=60 delivered=1 clock=35000000000 running=1",
            "M07_027_TIMEOUT "+Fields(0,63_000_001_000)+" sharedFailed=0",
            "M16_011_SLOT event=1 "+Fields(0,63_000_001_000)+" deadline=0 duration=0",
            "M16_011_SLOT event=1 "+Fields(1,70_001_000_000)+" deadline=0 duration=0",
            "M16_011_SLOT event=1 "+Fields(2,70_001_000_000)+" deadline=0 duration=0"]);
        rows.InsertRange(rows.IndexOf("TEST_START m16-011") + 1, ["TEST_START m07.027", "TEST_START m07.028"]);
        rows[rows.FindIndex(row => row.StartsWith("M16_011_WIRE ", StringComparison.Ordinal))] =
            $"M16_011_WIRE received={71 + startupRetries} arp=0 accepted=71 ignored={startupRetries} capacity=0 arpCopied=0 tcpCopied=44";
        rows[rows.FindIndex(row => row.StartsWith("M16_011_END ", StringComparison.Ordinal))] =
            $"M16_011_END polls=100 received={71 + startupRetries} copied=44 fresh=3 retired=3 start=0 end=70001000000 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped";
        rows[rows.FindIndex(row => row.StartsWith("M07_026_ROW stage=4 ", StringComparison.Ordinal))] = $"M07_026_ROW stage=4 a={72 + startupRetries} b=45 c=1 d=2";
        rows.AddRange(["CLROS_M07_028_READY", "TEST_PASS m07.028", "CLROS_M07_027_READY", "TEST_PASS m07.027"]);
        return rows.ToArray();
    }
    [HostTest("containment parent joins observed unanswered startup SYNs without assigning individual RX outcomes")]
    private static void StartupHandshake()
    {
        var rows = Rows(12);
        var reference = SocketContainmentPacketTests.ReferenceCapture(12);
        var value = QemuTranscripts.InspectSocketContainmentEvidenceForTests(rows, reference.Capture, Peer);
        HostCheck.Equal(12, value.UnansweredStartupSyns);
        foreach (string pair in new[] { "accepted=72 ignored=11", "accepted=70 ignored=13" })
        {
            bool rejected = false;
            try
            {
                _ = QemuTranscripts.InspectSocketContainmentEvidenceForTests(rows.Select(row => row.Replace("accepted=71 ignored=12", pair, StringComparison.Ordinal)).ToArray(), reference.Capture, Peer);
            }
            catch (ToolFailure error)
            {
                rejected = error.Reason == "socket_containment_capture_serial_peer_join";
            }
            HostCheck.True(rejected);
        }
    }
    private static void Reject(string[] rows, string reason, string peer = Peer)
    {
        string? actual = null;
        try
        {
            _ = QemuTranscripts.InspectSocketContainmentEvidenceForTests(rows, SocketContainmentPacketTests.ReferenceCapture().Capture, peer);
        }
        catch (ToolFailure error)
        {
            actual = error.Message;
        }
        HostCheck.True(actual?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + actual);
    }

    [HostTest("containment parent joins actual packet totals peer counters and separately recycled VLAN")]
    private static void Complete()
    {
        var rows = Rows();
        var reference = SocketContainmentPacketTests.ReferenceCapture();
        var value = QemuTranscripts.InspectSocketContainmentEvidenceForTests(rows, reference.Capture, Peer);
        HostCheck.Equal(72UL, value.Totals.PumpReceived);
        HostCheck.Equal(44UL, value.Totals.PumpTcpCopied);
        HostCheck.Equal(1UL, QemuTranscripts.InspectSocketContainmentNetworkForTests(rows).DeliveredAfterDrop);
        var separate = rows.Select(row => row.Replace("availableAfter=60 delivered=1", "availableAfter=59 delivered=0", StringComparison.Ordinal)).ToArray();
        HostCheck.Equal(3, QemuTranscripts.InspectSocketContainmentNetworkForTests(separate).Lifetimes.Count);
    }
    [HostTest("containment parent rejects changed NIC cleanup peer lifetime and retained resources")]
    private static void Parents()
    {
        foreach (var change in new[]{("stage=4 a=72","stage=4 a=71","socket_nic_cleanup"),("fresh=3","fresh=4","socket_network_accounting"),
            ("buffers=scrubbed","buffers=retained","socket_network_cleanup"),("end=0","end=2000","socket_peer_parent"),
            ("stage=5 a=8 b=0","stage=5 a=8 b=1","socket_nic_cleanup")})
            Reject(Rows().Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray(), change.Item3);
        Reject([.. Rows(), "extra"], "socket_parent_extra_record");
    }
    [HostTest("containment parent rejects invented peer completion and packet counter disagreement")]
    private static void Independent()
    {
        foreach (string peer in new[] { Peer.Replace("complete=True", "complete=False", StringComparison.Ordinal), Peer.Replace("stopped=True", "stopped=False", StringComparison.Ordinal) })
            Reject(Rows(), "socket_containment_peer_cleanup", peer);
        Reject(Rows(), "socket_containment_capture_serial_peer_join", Peer.Replace("headTransmissions=8", "headTransmissions=7", StringComparison.Ordinal));
        Reject(Rows(), "socket_containment_capture_serial_peer_join", Peer.Replace("arpReplies=2", "arpReplies=3", StringComparison.Ordinal));
        Reject(Rows().Select(row => row.Replace("accepted=71 ignored=0", "accepted=70 ignored=1", StringComparison.Ordinal)).ToArray(), "socket_containment_capture_serial_peer_join");
    }
}
