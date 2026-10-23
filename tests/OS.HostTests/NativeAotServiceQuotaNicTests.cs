using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaNicTests
{
    private sealed record Vector(List<byte[]> Frames,QemuTranscripts.WebParentReport Parent,
        QemuTranscripts.ServiceEnvelopeInspection Envelope);
    private static Vector Fixture()
    {
        var original=NativeAotSocketIntegrationTests.Packets();
        // Original owned parent anchor+ARP, one ignored runtime RA before ARP
        // resolution, then a no-FIN service prefix with independent TCP bytes.
        var frames=original.Frames.Take(6).Select(frame=>frame.ToArray()).ToList();
        frames.Add(original.Frames[0].ToArray());frames.AddRange(original.Frames.Skip(6).Take(2).Select(frame=>frame.ToArray()));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,41000,50,0,2,[]));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,41000,50,0,2,[]));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,43000,1000,0,2,[]));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(true,43000,2000,1001,0x12,[]));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(true,43000,2000,1001,0x12,[])); // Exact SYNACK retry counts as copied.
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,43000,1001,2001,0x10,[]));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,43000,1001,2001,0x18,"P"u8));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(true,43000,2001,1002,0x18,"Q"u8));
        frames.Add(NativeAotSocketIntegrationTests.Tcp(false,43000,1002,2002,0x10,[]));
        frames.Add(original.Frames[0].ToArray()); // Received and ignored after resolution.
        frames.Add(original.Frames[6].ToArray());frames.Add(original.Frames[7].ToArray()); // Runtime ARP copied/received.
        var parent=new QemuTranscripts.WebParentReport(0,0x200000,0x300000,100_000_000,10_000_000,
            0x10000000,20,21,original.Anchor,original.Arp);
        var envelope=new QemuTranscripts.ServiceEnvelopeInspection(8,1,4,3,0,1,3,1,1,0,20,25,0);
        return new(frames,parent,envelope);
    }

    // These exact peer/wire rows isolate the counter seam. They deliberately
    // are not a complete parent/quota capture or a prepared/owned program.
    private static byte[] Raw(QemuTranscripts.WebParentReport parent,QemuTranscripts.ServiceEnvelopeInspection envelope,
        ulong peerReceived=2)=>Encoding.UTF8.GetBytes(
        $"M16_011_PEER mac={parent.Arp.PeerMac} sent=1 received={peerReceived} start={parent.PeerStart} end={parent.PeerEnd}\r\n"+
        $"M16_011_WIRE received={envelope.Received} arp={envelope.Arp} accepted={envelope.Accepted} ignored={envelope.Ignored} "+
        $"capacity={envelope.Capacity} arpCopied={envelope.ArpCopied} tcpCopied={envelope.TcpCopied}\r\n");
    private static QemuTranscripts.ServiceNicAccountingInspection Read(Vector v,
        QemuTranscripts.ServiceEnvelopeInspection? envelope=null,byte[]? raw=null,byte[]? pcap=null)=>
        QemuTranscripts.InspectNativeAotServiceQuotaNicForTests(pcap??NativeAotSocketIntegrationTests.Capture(v.Frames),
            v.Parent,envelope??v.Envelope,raw??Raw(v.Parent,envelope??v.Envelope));

    [HostTest("quota NIC accounting joins original ARP TCP ignored and quiet SYN records without graceful FIN")]
    private static void Exact()
    {
        var v=Fixture();byte[] packets=NativeAotSocketIntegrationTests.Capture(v.Frames),before=packets.ToArray();
        var result=Read(v,pcap:packets);
        HostCheck.True(result is {PeerReceived:2,Received:8,Arp:1,Accepted:4,Ignored:3,ArpCopied:1,TcpCopied:3,
            QuietSyns:2,FirstSynAckRecord:13});
        HostCheck.True(packets.AsSpan().SequenceEqual(before));
        Array.Clear(packets);v.Frames.Clear();HostCheck.Equal(3UL,result.TcpCopied);
    }

    [HostTest("quota NIC accounting rejects drift in every original counter parent and raw envelope association")]
    private static void Counters()
    {
        var v=Fixture();var e=v.Envelope;
        // Keep each received arithmetic identity valid so packet comparison,
        // rather than preflight arithmetic alone, must reject these variants.
        foreach(var bad in new[]{
            e with{Received=9,Accepted=5},e with{Arp=2,Accepted=3},
            e with{Accepted=5,Ignored=2},e with{Ignored=4,Accepted=3},
            e with{ArpCopied=2},e with{TcpCopied=4}})
            Reject(()=>Read(v,envelope:bad),"service_packet_nic_join");
        Reject(()=>Read(v,envelope:e with{Capacity=1}),"service_quota_nic_envelope");
        Reject(()=>Read(v,raw:Raw(v.Parent,e,3)),"service_packet_nic_join");
        string raw=Encoding.UTF8.GetString(Raw(v.Parent,e));
        foreach(var mutation in new[]{("sent=1","sent=2"),("mac="+v.Parent.Arp.PeerMac,"mac=1"),
            ("start=20","start=19"),("end=21","end=22")})
            Reject(()=>Read(v,raw:Encoding.UTF8.GetBytes(raw.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal))),
                "service_quota_nic_parent");
        Reject(()=>Read(v,raw:Encoding.UTF8.GetBytes(raw.Replace("tcpCopied=3","tcpCopied=4",StringComparison.Ordinal))),
            "service_quota_nic_wire");
        string[] rows=raw.Split("\r\n",StringSplitOptions.RemoveEmptyEntries);
        foreach(int selected in new[]{0,1})
        {
            Reject(()=>Read(v,raw:Encoding.UTF8.GetBytes(rows[1-selected]+"\r\n")),"service_quota_nic_row_once");
            Reject(()=>Read(v,raw:Encoding.UTF8.GetBytes(raw+rows[selected]+"\r\n")),"service_quota_nic_row_once");
        }
    }

    [HostTest("quota NIC accounting preserves SYNACK source quiet boundary and bounded original capture rejection")]
    private static void Boundaries()
    {
        var late=Fixture();late.Frames.Add(NativeAotSocketIntegrationTests.Tcp(false,42000,900,0,2,[]));
        Reject(()=>Read(late),"service_packet_quiet_syn_lifetime");
        var orphan=Fixture();orphan.Frames.RemoveAt(11); // Remove the answered original SYN, not either quiet SYN.
        Reject(()=>Read(orphan),"service_packet_syn_ack_source");
        var retry=Fixture();retry.Frames[13]=NativeAotSocketIntegrationTests.Tcp(true,43000,2001,1001,0x12,[]);
        Reject(()=>Read(retry),"service_packet_syn_ack_source");
        var excessive=Fixture();
        excessive.Frames.InsertRange(9,Enumerable.Range(0,1599).Select(_=>
            NativeAotSocketIntegrationTests.Tcp(false,41000,50,0,2,[])));
        Reject(()=>Read(excessive),"service_packet_quiet_syn_lifetime");
        var shortFrame=Fixture();shortFrame.Frames.Add(new byte[1]);
        Reject(()=>Read(shortFrame),"service_quota_nic_frame");
        var v=Fixture();byte[] packets=NativeAotSocketIntegrationTests.Capture(v.Frames);
        Reject(()=>Read(v,pcap:packets[..^1]));Reject(()=>Read(v,pcap:new byte[4*1024*1024+1]));
        Reject(()=>Read(v,raw:[]),"service_quota_nic_input");
        Reject(()=>QemuTranscripts.InspectNativeAotServiceQuotaNicForTests(packets,v.Parent with{FrameAnchor=1},
            v.Envelope,Raw(v.Parent,v.Envelope)));
        Reject(()=>QemuTranscripts.InspectNativeAotServiceQuotaNicForTests(packets,v.Parent with{
            Arp=v.Parent.Arp with{ReceiveCrc32=v.Parent.Arp.ReceiveCrc32^1}},v.Envelope,Raw(v.Parent,v.Envelope)));
    }

    private static void Reject(Action action,string? reason=null)
    {
        try{action();}
        catch(ToolFailure error) when(reason is null || error.Reason==reason){return;}
        catch(InvalidDataException) when(reason is null){return;}
        throw new InvalidOperationException("expected rejection "+reason);
    }
}
