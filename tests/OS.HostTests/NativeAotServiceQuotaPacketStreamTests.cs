using OS.Tooling;

internal static class NativeAotServiceQuotaPacketStreamTests
{
    private const uint Client=1001,Server=2001;
    private const int Cap=4*1024*1024;
    private sealed record Vector(List<byte[]> Frames,uint Anchor,QemuArpReceiveReceipt Arp,ushort Port);
    private static Vector Start()
    {
        var original=NativeAotSocketIntegrationTests.Packets();
        return new(original.Frames.Take(8).Select(frame=>frame.ToArray()).ToList(),original.Anchor,original.Arp,43000);
    }
    private static void Add(Vector v,bool outgoing,uint sequence,uint ack,byte flags,ReadOnlySpan<byte> payload=default,ushort? port=null) =>
        v.Frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing,port??v.Port,sequence,ack,flags,payload));
    private static void Handshake(Vector v,uint client=Client,uint server=Server,ushort? port=null)
    {
        Add(v,false,client-1,0,2,port:port);Add(v,true,server-1,client,0x12,port:port);Add(v,false,client,server,0x10,port:port);
    }
    private static byte[] Capture(Vector v)=>NativeAotSocketIntegrationTests.Capture(v.Frames);
    private static QemuTranscripts.ServiceQuotaPacketStreams Read(Vector v,byte[]? raw=null,bool lifecycle=false,IReadOnlyList<uint>? ports=null) =>
        QemuTranscripts.InspectNativeAotServiceQuotaPacketStreamsForTests(raw??Capture(v),v.Anchor,v.Arp,ports??new uint[]{v.Port},lifecycle);
    private static Vector Large(int request=40000,int response=600000)
    {
        var v=Start();Handshake(v);
        byte[] data=Enumerable.Range(0,Math.Max(request,response)).Select(i=>(byte)(i%251)).ToArray();
        for(int at=0;at<request;at+=1024)Add(v,false,Client+(uint)at,Server,0x18,data.AsSpan(at,Math.Min(1024,request-at)));
        Add(v,true,Server,Client+(uint)request,0x10);
        for(int at=0;at<response;at+=1024)
        {
            int count=Math.Min(1024,response-at);Add(v,true,Server+(uint)at,Client+(uint)request,0x18,data.AsSpan(at,count));
            Add(v,false,Client+(uint)request,Server+(uint)(at+count),0x10);
        }
        Add(v,false,Client+(uint)request,Server+(uint)response,0x10);return v;
    }
    private static Vector Witnesses(bool changed=false,bool reset=false,bool close=false)
    {
        var v=Start();Handshake(v);
        Add(v,false,Client+4,Server,0x18,"ef"u8);Add(v,false,Client,Server,0x18,"abcd"u8);
        Add(v,false,Client,Server,0x18,changed?"abcdeX"u8:"abcdef"u8);
        Add(v,true,Server,Client+6,0x18,"XYZ"u8);Add(v,false,Client+6,Server+3,0x10);
        if(reset)Add(v,false,Client+6,Server+3,0x14);
        if(close){Add(v,true,Server+3,Client+6,0x11);Add(v,false,Client+6,Server+4,0x11);Add(v,true,Server+4,Client+7,0x10);}
        return v;
    }
    private static Vector Holes(bool overflow=false)
    {
        var v=Start();Handshake(v);uint half=Cap/2;
        Add(v,false,Client+half-1,Server,0x18,"A"u8);Add(v,true,Server+half-1,Client,0x18,"B"u8);
        if(overflow)Add(v,false,Client+half,Server,0x18,"C"u8);return v;
    }

    [HostTest("quota packet streams permit bounded persistent bytes without changing ordinary or lifecycle caps")]
    private static void PersistentCaps()
    {
        var v=Large();var result=Read(v);var flow=result.Streams.Single();
        HostCheck.True(result.AggregateExtent==640000 && flow.Requests.Extent==40000 && flow.Responses.Extent==600000 &&
            flow.Requests.ContiguousBytes==40000 && flow.Responses.ContiguousBytes==600000);
        HostCheck.True(Convert.FromBase64String(flow.Requests.KnownPrefixBase64).SequenceEqual(
            Enumerable.Range(0,40000).Select(i=>(byte)(i%251))));
        HostCheck.True(flow.Facts.SynRecord==9 && flow.Facts.SynAck && flow.Facts.ClientReady &&
            !flow.Facts.ClientFin && !flow.Facts.ServerFin && flow.Facts.ResetRecord==0);
        Reject(()=>QemuTranscripts.InspectNativeAotServiceWireRangesForTests(Capture(v),v.Anchor,v.Arp,[v.Port]),"service_wire_payload_extent");
        Reject(()=>QemuTranscripts.InspectNativeAotServiceWireRangesForTests(Capture(v),v.Anchor,v.Arp,[v.Port],lifecycle:true),"service_wire_payload_extent");
        var response=Large(1,600000);
        Reject(()=>QemuTranscripts.InspectNativeAotServiceWireRangesForTests(Capture(response),response.Anchor,response.Arp,[response.Port],lifecycle:true),
            "service_wire_payload_extent");
    }

    [HostTest("quota packet streams copy first-witness islands and retain existing reset FIN and retry rules")]
    private static void FirstWitnesses()
    {
        var v=Witnesses();byte[] raw=Capture(v),before=raw.ToArray();
        var original=QemuTranscripts.InspectNativeAotServiceWireRangesForTests(raw,v.Anchor,v.Arp,[v.Port]);
        var result=Read(v,raw);var flow=result.Streams.Single();
        HostCheck.True(flow.Requests.KnownPrefixBase64=="YWJjZGVm" && flow.Requests.KnownBytes==6 && flow.Requests.Extent==6);
        HostCheck.True(flow.Requests.FirstRanges.Select(r=>r.First).Concat(flow.Responses.FirstRanges.Select(r=>r.First)).SequenceEqual(original));
        HostCheck.True(flow.Requests.FirstRanges[0].First is {StreamOffset:4,ByteCount:2,Record:12} &&
            flow.Requests.FirstRanges[0].DataBase64=="ZWY=" && flow.Requests.FirstRanges[1].First is {StreamOffset:0,ByteCount:4,Record:13});
        HostCheck.True(raw.AsSpan().SequenceEqual(before));Array.Clear(raw);v.Frames.Clear();
        HostCheck.Equal("YWJjZGVm",flow.Requests.KnownPrefixBase64);
        bool streams=false,ranges=false;
        try{((IList<QemuTranscripts.ServiceQuotaPacketStream>)result.Streams)[0]=flow;}
        catch(NotSupportedException){streams=true;}
        try{((IList<QemuTranscripts.ServiceQuotaPacketRange>)flow.Requests.FirstRanges)[0]=flow.Requests.FirstRanges[0];}
        catch(NotSupportedException){ranges=true;}
        HostCheck.True(streams && ranges);
        Reject(()=>Read(Witnesses(changed:true)),"service_wire_retransmission");
        var reset=Witnesses(reset:true);Reject(()=>Read(reset),"service_wire_established");
        HostCheck.Equal(17,Read(reset,lifecycle:true).Streams.Single().Facts.ResetRecord);
        var closed=Read(Witnesses(close:true)).Streams.Single().Facts;
        HostCheck.True(closed.ClientFin && closed.ServerFin && closed.ClientFinAcked && closed.ServerFinAcked &&
            closed.ServerFinFirst && closed.ServerFinRecord==17 && closed.ClientFinRecord==18);
    }

    [HostTest("quota packet stream budgets charge holes and aggregate directions before allocation growth")]
    private static void ExtentBudgets()
    {
        var valid=Read(Holes());var flow=valid.Streams.Single();
        HostCheck.True(valid.AggregateExtent==Cap && flow.Requests.Extent==Cap/2 && flow.Responses.Extent==Cap/2 &&
            flow.Requests.ContiguousBytes==0 && flow.Responses.ContiguousBytes==0 &&
            flow.Requests.KnownBytes==1 && flow.Responses.KnownBytes==1 && flow.Requests.KnownPrefixBase64=="");
        var over=Holes(overflow:true);byte[] raw=Capture(over);
        _=Read(Witnesses()); // Warm common parsing/report types before allocation observation.
        long start=GC.GetAllocatedBytesForCurrentThread();
        Reject(()=>Read(over,raw),"service_quota_wire_aggregate");
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
        // Two2MiB byte+seen buffers cost about8MiB. Growing either one again
        // before refusing would allocate another8MiB;12MiB separates those paths.
        HostCheck.True(allocated<12L*1024*1024);
        var direction=Start();Handshake(direction);Add(direction,false,Client+Cap,Server,0x18,"A"u8);
        Reject(()=>Read(direction),"service_quota_wire_extent");
        var across=Start();Handshake(across);Add(across,false,Client+Cap-1,Server,0x18,"A"u8);
        Handshake(across,100001,200001,43001);Add(across,false,100001,200001,0x18,"B"u8,43001);
        Reject(()=>Read(across,ports:[43000,43001]),"service_quota_wire_aggregate");
        var plain=Witnesses();Reject(()=>Read(plain,ports:[plain.Port,plain.Port]),"service_wire_ports");
    }

    private static void Reject(Action action,string reason)
    {
        try{action();}catch(ToolFailure error){HostCheck.Equal(reason,error.Reason);return;}
        throw new InvalidOperationException("expected rejection "+reason);
    }
}
