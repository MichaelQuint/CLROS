using System.Buffers.Binary;
using OS.Tooling;

internal static class NativeAotWebRacePacketTests
{
    private const uint Client=1001,Server=2001;
    private const ushort First=43000,Second=43001;
    private const int Cap=4*1024*1024;
    private sealed record Vector(NativeAotWebPacketTests.ControlPackets Original,List<byte[]> Frames,uint[] Ports,int OrdinaryCount);
    private static Vector Start(params uint[] ports)
    {
        var original=NativeAotWebPacketTests.RaceControls();
        return new(original,original.Frames.Select(frame=>frame.ToArray()).ToList(),[..original.Ports,..ports],original.Frames.Count);
    }
    private static void Add(Vector v,bool outgoing,ushort port,uint sequence,uint ack,byte flags,ReadOnlySpan<byte> payload=default)=>
        v.Frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing,port,sequence,ack,flags,payload));
    private static void Handshake(Vector v,ushort port,uint client=Client,uint server=Server)
    {Add(v,false,port,client-1,0,2);Add(v,true,port,server-1,client,0x12);Add(v,false,port,client,server,0x10);}
    private static QemuTranscripts.WebRacePacketInspection Read(Vector v,byte[]? raw=null)=>
        QemuTranscripts.InspectNativeAotWebRacePacketsForTests(raw??NativeAotSocketIntegrationTests.Capture(v.Frames),v.Original.Anchor,
            v.Original.Arp,v.Ports,22080,"SELECTED","{\"value\":\"SELECTED\"}",v.Original.Windows,v.Original.Controls);
    private static Vector Concurrent()
    {
        var v=Start(First,Second);Handshake(v,First);Handshake(v,Second,100001,200001);
        Add(v,false,First,Client,Server,0x18,"abc"u8);
        Add(v,false,Second,100001,200001,0x18,"1234"u8);
        Add(v,true,Second,200001,100005,0x18,"XY"u8);
        Add(v,true,First,Server,Client+3,0x18,"hello"u8);
        return v;
    }
    private static void Reject(Action action)
    {bool rejected=false;try{action();}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}

    [HostTest("web race whole PCAP preserves exact ordinary controls and original concurrent stream record identities")]
    private static void WholeCapture()
    {
        var v=Concurrent();byte[] raw=NativeAotSocketIntegrationTests.Capture(v.Frames),before=raw.ToArray();var result=Read(v,raw);
        HostCheck.True(result.Ordinary.Connections==8&&result.Ordinary.Responses.Count==9&&result.Ordinary.ControlsChecked&&result.Ordinary.UtcChecked);
        HostCheck.True(result.Race.Streams.Count==2&&result.Race.QuietSyns==0&&result.Race.TcpRecords==10&&result.Race.AggregateExtent==14);
        HostCheck.Equal(v.OrdinaryCount+1,result.FirstRaceRecord);
        HostCheck.True(result.OrdinaryRecords.SequenceEqual(Enumerable.Range(9,v.OrdinaryCount-8))&&result.OrdinaryRecords.Contains(v.Original.VlanAt+1));
        var first=result.Race.Streams[0];var second=result.Race.Streams[1];
        HostCheck.True(first.RemotePort==First&&second.RemotePort==Second&&first.Facts.SynRecord==v.OrdinaryCount+1&&second.Facts.SynRecord==v.OrdinaryCount+4);
        HostCheck.True(first.Requests.KnownPrefixBase64=="YWJj"&&first.Responses.KnownPrefixBase64=="aGVsbG8="&&second.Requests.KnownPrefixBase64=="MTIzNA=="&&second.Responses.KnownPrefixBase64=="WFk=");
        HostCheck.Equal(v.OrdinaryCount+7,first.Requests.FirstRanges.Single().First.Record);
        foreach(var range in result.Race.Streams.SelectMany(flow=>flow.Requests.FirstRanges.Concat(flow.Responses.FirstRanges)))
            HostCheck.True(range.First.Seconds==1&&range.First.Microseconds==range.First.Record-1&&!result.OrdinaryRecords.Contains(range.First.Record));
        HostCheck.True(raw.AsSpan().SequenceEqual(before));Array.Clear(raw);v.Frames.Clear();
        HostCheck.Equal("YWJj",first.Requests.KnownPrefixBase64);
        bool immutable=false;try{((IList<int>)result.OrdinaryRecords)[0]=0;}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
    }

    [HostTest("web race partition refuses changed VLAN leaked ACK premature additional SYN foreign ports and conflicting retry")]
    private static void PartitionRefusals()
    {
        var changed=Concurrent();changed.Frames[changed.Original.VlanAt][18]=1;Reject(()=>Read(changed));
        var duplicate=Concurrent();duplicate.Frames.Insert(duplicate.Original.VlanAt,duplicate.Frames[duplicate.Original.VlanAt].ToArray());Reject(()=>Read(duplicate));
        var missing=Concurrent();missing.Frames.RemoveAt(missing.Original.VlanAt);Reject(()=>Read(missing));
        var leaked=Concurrent();leaked.Frames.Insert(leaked.OrdinaryCount,Convert.FromHexString(leaked.Original.Controls.Frames.DroppedFrames[0].Hex));Reject(()=>Read(leaked));
        var early=Concurrent();byte[] syn=early.Frames[early.OrdinaryCount];early.Frames.RemoveAt(early.OrdinaryCount);early.Frames.Insert(early.Original.VlanAt,syn);Reject(()=>Read(early));
        var beforeEof=Concurrent();byte[] ninth=beforeEof.Frames[beforeEof.OrdinaryCount];beforeEof.Frames.RemoveAt(beforeEof.OrdinaryCount);
        int fin=beforeEof.Frames.FindIndex(frame=>frame.Length>=54&&frame[12]==8&&frame[13]==0&&frame[23]==6&&frame[47]==0x11&&
            BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(34))==8080&&BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(36))==beforeEof.Original.Ports[^1]);
        HostCheck.True(fin>beforeEof.Original.VlanAt);beforeEof.Frames.Insert(fin,ninth);
        string? boundary=null;try{_=Read(beforeEof);}catch(ToolFailure error){boundary=error.Reason;}
        HostCheck.Equal("web_race_wire_before_controls",boundary);
        foreach(int at in new[]{8,Concurrent().OrdinaryCount})
        {var foreign=Concurrent();foreign.Frames.Insert(at,NativeAotSocketIntegrationTests.Tcp(false,44000,55,0,2,[]));Reject(()=>Read(foreign));}
        var retry=Concurrent();Add(retry,false,First,Client,Server,0x18,"abX"u8);Reject(()=>Read(retry));
        var ports=Concurrent();Reject(()=>Read(ports with{Ports=[..ports.Ports,First]}));
        var absent=Concurrent();Reject(()=>Read(absent with{Ports=[..absent.Ports,44001]}));
    }

    [HostTest("web race first ranges retain islands retries partial streams empty FIN and bounded client reset facts")]
    private static void PartialFacts()
    {
        var v=Start(First,Second);Handshake(v,First);
        Add(v,false,First,Client+4,Server,0x18,"ef"u8);Add(v,false,First,Client,Server,0x18,"abcd"u8);
        Add(v,false,First,Client,Server,0x18,"abcdef"u8);Add(v,true,First,Server,Client+6,0x18,"XYZ"u8);
        Handshake(v,Second,100001,200001);Add(v,false,Second,100001,200001,0x11);Add(v,true,Second,200001,100002,0x10);
        Add(v,true,Second,200001,100002,0x11);Add(v,false,Second,100002,200002,0x10);
        var result=Read(v);var first=result.Race.Streams[0];var empty=result.Race.Streams[1];
        HostCheck.True(first.Requests.KnownPrefixBase64=="YWJjZGVm"&&first.Requests.KnownBytes==6&&first.Requests.FirstRanges.Count==2&&!first.Facts.ServerFin);
        HostCheck.True(first.Requests.FirstRanges[0].First is {StreamOffset:4,ByteCount:2}&&first.Requests.FirstRanges[0].First.Record==v.OrdinaryCount+4&&
            first.Requests.FirstRanges[1].First.Record==v.OrdinaryCount+5);
        HostCheck.True(empty.Requests.Extent==0&&empty.Responses.Extent==0&&empty.Facts.ClientFin&&empty.Facts.ServerFin&&empty.Facts.ClientFinAcked&&empty.Facts.ServerFinAcked&&!empty.Facts.ServerFinFirst);
        var reset=Start(First);Handshake(reset,First);Add(reset,false,First,Client,Server,0x18,"A"u8);Add(reset,false,First,Client+1,Server,0x14);
        HostCheck.Equal(reset.Frames.Count,Read(reset).Race.Streams.Single().Facts.ResetRecord);
        byte[] generic=NativeAotSocketIntegrationTests.Capture(v.Frames.Take(8).Concat(v.Frames.Skip(v.OrdinaryCount+7)));
        // Keep ordinary generic mode's pre-existing empty-FIN refusal. The
        // prefix plus second flow is a separate independent packet value.
        Reject(()=>QemuTranscripts.InspectNativeAotServiceQuotaPacketStreamsForTests(generic,v.Original.Anchor,v.Original.Arp,[Second]));
    }

    [HostTest("web race delegated streams keep directional aggregate hole budgets and ordinary default packet contract")]
    private static void BoundsAndDefaults()
    {
        var v=Start(First);Handshake(v,First);uint half=Cap/2;
        Add(v,false,First,Client+half-1,Server,0x18,"A"u8);Add(v,true,First,Server+half-1,Client,0x18,"B"u8);
        var result=Read(v);HostCheck.True(result.Race.AggregateExtent==Cap&&result.Race.Streams.Single().Requests.ContiguousBytes==0&&result.Race.Streams.Single().Responses.ContiguousBytes==0);
        Add(v,false,First,Client+half,Server,0x18,"C"u8);Reject(()=>Read(v));
        var direction=Start(First);Handshake(direction,First);Add(direction,false,First,Client+Cap,Server,0x18,"X"u8);Reject(()=>Read(direction));
        var ordinary=Start();Reject(()=>Read(ordinary));
        var original=ordinary.Original;
        var unchanged=QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(original.Frames),original.Anchor,original.Arp,
            original.Ports,22080,"SELECTED","{\"value\":\"SELECTED\"}",utcWindows:original.Windows,controls:original.Controls);
        HostCheck.True(unchanged.Connections==8&&unchanged.ControlsChecked&&unchanged.Responses.Count==9);
        var extra=Concurrent();Reject(()=>QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(extra.Frames),original.Anchor,original.Arp,
            extra.Ports,22080,"SELECTED","{\"value\":\"SELECTED\"}",utcWindows:original.Windows,controls:original.Controls));
    }
}
