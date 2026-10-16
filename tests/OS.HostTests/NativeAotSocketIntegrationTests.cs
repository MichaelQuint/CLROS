using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using OS.Tooling;

internal static class NativeAotSocketIntegrationTests
{
    private const string Peer="TEST_START nativeaot.socket.peer\n"+
        "SOCKET_PEER_CONNECTION index=0 sent=20 received=65536 close=eof\n"+
        "SOCKET_PEER_CONNECTION index=1 sent=1 close=reset\n"+
        "SOCKET_PEER_CONNECTION index=2 sent=20 received=18 close=eof\n"+
        "SOCKET_PEER_CONNECTION index=3 sent=20 received=18 close=eof\nTEST_PASS nativeaot.socket.peer\n";
    private static void Reject(Action action,string reason)
    {
        string? failure=null;try{action();}catch(ToolFailure error){failure=error.Message;}
        HostCheck.True(failure?.Contains(reason,StringComparison.Ordinal)==true,reason+" actual="+failure);
    }
    [HostTest("socket selection preserves the exact sync async application and rejects broader runtime substitution")]
    private static void Selection()
    {
        Dictionary<string,bool> flags=[];HostCheck.Equal("",NativeAotSocketSelection.Application(flags));
        flags["EnableM16011Probe"]=true;HostCheck.Equal("NativeAotSocketSmoke",NativeAotSocketSelection.Application(flags));
        Reject(()=>NativeAotSocketSelection.Application(flags,"NativeAotSocketSmokeAsync"),"socket_profile_application");
        flags["EnableM16012Probe"]=true;HostCheck.Equal("NativeAotSocketSmokeAsync",NativeAotSocketSelection.Application(flags));
        foreach(string name in new[]{"M16010","M09011","M09012"})
        {flags["Enable"+name+"Probe"]=true;Reject(()=>NativeAotSocketSelection.Application(flags),"socket_runtime_conflict");flags.Remove("Enable"+name+"Probe");}
    }
    [HostTest("socket peer output requires all original literal bytes and EOF reset results")]
    private static void Output()
    {
        NativeAotSocketPeer.InspectOutput(Peer);NativeAotSocketPeer.InspectOutput(Peer.Replace("\n","\r\n",StringComparison.Ordinal));
        foreach(string text in new[]{Peer.Replace("65536","65535",StringComparison.Ordinal),Peer.Replace("close=reset","close=eof",StringComparison.Ordinal),
            Peer+"extra\n",Peer.Replace("index=3","index=2",StringComparison.Ordinal),Peer.TrimEnd('\n')})
            Reject(()=>NativeAotSocketPeer.InspectOutput(text),"socket_peer_output");
    }

    // This is a host stub with no sealed program/NIC/firmware evidence. It tests
    // the actual separate peer child and owned process/log lifecycle only.
    private static async Task<ProcessResult> Stub(int port,CancellationToken token)
    {
        using TcpListener listener=new(IPAddress.Loopback,port);listener.Start();
        for(int index=0;index<4;index++)
        {
            using Socket client=await listener.AcceptSocketAsync(token);byte[] expected=index==1?[0x52]:"CLROS_SOCKET_REQUEST"u8.ToArray();
            byte[] request=new byte[expected.Length];int read=0;
            while(read<request.Length)
            {int count=await client.ReceiveAsync(request.AsMemory(read),SocketFlags.None,token);HostCheck.True(count>0);read+=count;}
            HostCheck.True(request.AsSpan().SequenceEqual(expected));
            if(index==1)
            {
                try{HostCheck.Equal(0,await client.ReceiveAsync(new byte[1],SocketFlags.None,token));}
                catch(SocketException error)when(error.SocketErrorCode==SocketError.ConnectionReset){}
                continue;
            }
            if(index>=2)HostCheck.Equal(0,await client.ReceiveAsync(new byte[1],SocketFlags.None,token));
            byte[] payload=index==0?Enumerable.Range(0,65536).Select(i=>(byte)(i%251)).ToArray():"CLROS_SOCKET_REPLY"u8.ToArray();
            int sent=0;while(sent<payload.Length){int count=await client.SendAsync(payload.AsMemory(sent),SocketFlags.None,token);HostCheck.True(count>0);sent+=count;}
            client.Shutdown(SocketShutdown.Send);
        }
        return new(0,"","",false,false,false,TimeSpan.Zero);
    }
    [HostTest("socket orchestration owns the unchanged separate peer and preserves its fresh exact output")]
    private static void OwnedPeer()=>OwnedPeerCore().GetAwaiter().GetResult();
    private static async Task OwnedPeerCore()
    {
        string root=HostToolProcess.FindRepositoryRoot();string log=Path.Combine(root,"artifacts","tests","socket-peer-"+Guid.NewGuid().ToString("N"),"peer.log");
        var result=await NativeAotSocketPeer.RunAsync(root,log,Stub);
        HostCheck.True(result.Port is >0 and <=65535 && result.Runner.Succeeded);
        byte[] raw=File.ReadAllBytes(log);HostCheck.Equal(Peer,Encoding.UTF8.GetString(raw).Replace("\r\n","\n",StringComparison.Ordinal));
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(raw)),result.Sha256);
        bool rejected=false;try{_=await NativeAotSocketPeer.RunAsync(root,log,Stub);}catch(IOException){rejected=true;}HostCheck.True(rejected);
    }
    [HostTest("socket orchestration cancels and awaits its owned peer when the runner fails")]
    private static void FailedRunner()=>FailedRunnerCore().GetAwaiter().GetResult();
    private static async Task FailedRunnerCore()
    {
        string root=HostToolProcess.FindRepositoryRoot();string log=Path.Combine(root,"artifacts","tests","socket-peer-"+Guid.NewGuid().ToString("N"),"failed.log");
        string? failure=null;
        try{_=await NativeAotSocketPeer.RunAsync(root,log,(_,_)=>Task.FromResult(new ProcessResult(2,"","",false,false,false,TimeSpan.Zero)));}
        catch(ToolFailure error){failure=error.Message;}
        HostCheck.True(failure?.StartsWith("socket_peer_",StringComparison.Ordinal)==true);HostCheck.True(File.Exists(log));
        string early=log+".early";failure=null;
        try{_=await NativeAotSocketPeer.RunAsync(root,early,(_,_)=>Task.FromResult(new ProcessResult(2,"","",false,false,false,TimeSpan.Zero)),
            beforePeer:token=>Task.Delay(Timeout.Infinite,token));}
        catch(ToolFailure error){failure=error.Message;}
        HostCheck.True(failure?.StartsWith("socket_runner_exited_before_peer exit=2 terminationConfirmed=True",StringComparison.Ordinal)==true);
        HostCheck.True(File.Exists(early)&&new FileInfo(early).Length==0);
    }

    private static List<string> Transport()
    {
        List<string> rows=[];
        string Fields(int i,ulong clock,ulong deadline=0,ulong duration=0)=>$"slot={i} owner=4096 epoch=1 generation=1 index={i} local=167772687 localPort=8080 remote=167772674 remotePort={40000+i} clock={clock} deadline={deadline} duration={duration}";
        for(int i=0;i<4;i++)rows.Add("M16_011_SLOT event=0 "+Fields(i,(uint)i+1));
        rows.Add("M16_011_TIMEWAIT "+Fields(0,5,240000000005,240000000000)+" drain=0");
        for(int i=1;i<4;i++)rows.Add("M16_011_SLOT event=1 "+Fields(i,(uint)i+5));
        rows.Add("M16_011_TIMEWAIT "+Fields(0,9,240000000005,240000000000)+" drain=1");
        rows.Add("M16_011_SLOT event=1 "+Fields(0,240000000005,240000000005,240000000000));return rows;
    }
    [HostTest("socket transport joins original tuple generations and full actual TIME WAIT before release")]
    private static void Lifetimes()
    {
        var rows=Transport();var report=QemuTranscripts.InspectSocketTransportForTests(rows.ToArray());
        HostCheck.True(report.Fresh==4&&report.Retired==4&&report.TimeWaitLifetimes==1&&report.MaximumDeadline==240000000005);
        Reject(()=>QemuTranscripts.InspectSocketTransportForTests(rows.Select(row=>row.Replace("clock=240000000005","clock=240000000004",StringComparison.Ordinal)).ToArray()),"socket_transport_retire");
        Reject(()=>QemuTranscripts.InspectSocketTransportForTests(rows.Where(row=>!row.StartsWith("M16_011_TIMEWAIT",StringComparison.Ordinal)).ToArray()),"socket_transport_retire");
        Reject(()=>QemuTranscripts.InspectSocketTransportForTests(rows.Select(row=>row.Contains("event=1 slot=1",StringComparison.Ordinal)?row.Replace("generation=1","generation=2",StringComparison.Ordinal):row).ToArray()),"socket_transport_generation");
        Reject(()=>QemuTranscripts.InspectSocketTransportForTests(rows[..^1].ToArray()),"socket_transport_complete");
    }

    // The existing independent PCI/APIC and NIC/ARP vectors are composed here.
    // Placeholder startup and leaf rows deliberately grant no app authority.
    private static string[] Envelope(bool asynchronous)
    {
        string[] nic=M07ArpReceiveEfiIntegrationTests.Capture(),apic=M05ApicSetupEfiIntegrationTests.Capture();
        ulong[] Parent(string prefix)=>nic.Single(row=>row.StartsWith(prefix+" ",StringComparison.Ordinal)).Split(' ')[2..].Select(v=>Convert.ToUInt64(v[2..],16)).ToArray();
        ulong[] allocator=Parent("M07_ARP A"),binding=Parent("M07_ARP 0");
        return [..apic.Take(QemuContracts.M03OwnedSubsystemMarkers.Count),
            ..Enumerable.Repeat("startup/package/service validated separately",7+7+17),
            ..apic.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count),..nic.Skip(QemuContracts.M05PciEnumerationMarkers.Count),
            "TEST_START m07-026",$"M07_026_ROW stage=0 a=268435456 b=32768 c={QemuContracts.M07ArpReceiveFeatures} d=8",
            "M16_011_PEER mac=90525193470466 sent=1 received=1 start=0 end=0","TEST_START m16-011",
            ..asynchronous?new[]{"TEST_START m16-012"}:Array.Empty<string>(),"TEST_START m16-009",..Transport(),"TEST_PASS m16-009",
            "M16_011_WIRE received=10 arp=0 accepted=10 ignored=0 capacity=0 arpCopied=0 tcpCopied=20",
            "M16_011_END polls=100 received=10 copied=20 fresh=4 retired=4 start=0 end=240000000005 maxDeadline=240000000005 retained=0 buffers=scrubbed descriptors=closed workers=reaped",
            "M07_026_ROW stage=4 a=11 b=21 c=1 d=2","M07_026_ROW stage=5 a=8 b=0 c=8 d=1",
            $"M07_026_ROW stage=6 a={allocator[0]} b={allocator[1]} c={allocator[2]} d={binding[2]}",
            "CLROS_M07_026_READY","TEST_PASS m07-026","CLROS_M16_011_READY","TEST_PASS m16-011",
            ..asynchronous?new[]{"CLROS_M16_012_READY","TEST_PASS m16-012"}:Array.Empty<string>()];
    }
    [HostTest("socket wrapper joins original APIC NIC cleanup DMA and transport observations in both modes")]
    private static void Parents()
    {
        foreach(bool asynchronous in new[]{false,true})
        {
            string[] rows=Envelope(asynchronous);HostCheck.Equal(4,QemuTranscripts.InspectSocketNetworkForTests(rows,asynchronous).Retired);
            foreach((string old,string replacement,string reason) in new[]{
                ("stage=4 a=11","stage=4 a=10","socket_nic_cleanup"),
                ("stage=5 a=8 b=0","stage=5 a=8 b=1","socket_nic_cleanup"),
                ("end=240000000005 maxDeadline","end=240000000004 maxDeadline","socket_network_accounting"),
                ("end=0","end=2","socket_peer_parent"),
                ("buffers=scrubbed","buffers=retained","socket_network_cleanup")})
                Reject(()=>QemuTranscripts.InspectSocketNetworkForTests(rows.Select(row=>row.Replace(old,replacement,StringComparison.Ordinal)).ToArray(),asynchronous),reason);
            Reject(()=>QemuTranscripts.InspectSocketNetworkForTests([..rows,"extra"],asynchronous),"socket_parent_extra_record");
            Reject(()=>QemuTranscripts.InspectSocketNetworkForTests(rows,!asynchronous),"socket_parent_");
        }
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum=0;for(int i=0;i<bytes.Length;i+=2)sum+=(uint)bytes[i]*256+(i+1<bytes.Length?bytes[i+1]:0U);
        while(sum>65535)sum=(sum&65535)+(sum>>16);return (ushort)~sum;
    }
    private static byte[] Tcp(bool outgoing,ushort port,uint sequence,uint ack,byte flags,ReadOnlySpan<byte> payload)
    {
        byte[] frame=new byte[54+payload.Length];
        Convert.FromHexString(outgoing?"52550A0002020200000000010800":"02000000000152550A0002020800").CopyTo(frame,0);
        Span<byte> ip=frame.AsSpan(14),tcp=frame.AsSpan(34);ip[0]=0x45;ip[8]=64;ip[9]=6;
        BinaryPrimitives.WriteUInt16BigEndian(ip[2..],checked((ushort)(40+payload.Length)));
        BinaryPrimitives.WriteUInt32BigEndian(ip[12..],outgoing?0x0a00020fU:0x0a000202U);
        BinaryPrimitives.WriteUInt32BigEndian(ip[16..],outgoing?0x0a000202U:0x0a00020fU);
        BinaryPrimitives.WriteUInt16BigEndian(ip[10..],Checksum(ip[..20]));
        BinaryPrimitives.WriteUInt16BigEndian(tcp,outgoing?(ushort)8080:port);BinaryPrimitives.WriteUInt16BigEndian(tcp[2..],outgoing?port:(ushort)8080);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[4..],sequence);BinaryPrimitives.WriteUInt32BigEndian(tcp[8..],ack);
        tcp[12]=0x50;tcp[13]=flags;tcp[14]=tcp[15]=255;payload.CopyTo(tcp[20..]);
        byte[] pseudo=new byte[12+tcp.Length];ip.Slice(12,8).CopyTo(pseudo);pseudo[9]=6;
        BinaryPrimitives.WriteUInt16BigEndian(pseudo.AsSpan(10),checked((ushort)tcp.Length));tcp.CopyTo(pseudo.AsSpan(12));
        BinaryPrimitives.WriteUInt16BigEndian(tcp[16..],Checksum(pseudo));return frame;
    }
    private static byte[] Capture(IEnumerable<byte[]> frames)
    {
        using MemoryStream bytes=new();bytes.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        using BinaryWriter writer=new(bytes,Encoding.UTF8,true);uint time=0;
        foreach(byte[] frame in frames){writer.Write(1U);writer.Write(time++);writer.Write((uint)frame.Length);writer.Write((uint)frame.Length);writer.Write(frame);}
        writer.Flush();return bytes.ToArray();
    }
    // Independent QEMU usernet reference frame retained from the original r12
    // capture. This background packet grants no guest IPv6 capability.
    private static byte[] RouterAdvertisement()=>Convert.FromHexString(
        "33330000000152560000000286DD6000000000383AFFFE800000000000000000000000000002FF0200000000000000000000000000018600155E4000070800000000000000000101525600000002030440C0000151800000384000000000FEC00000000000000000000000000000");
    private static (List<byte[]> Frames,uint Anchor,QemuArpReceiveReceipt Arp) Packets()
    {
        byte[] anchor=new byte[60],warmup=new byte[60];Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor,0);anchor.CopyTo(warmup,0);
        "CLROS-M07"u8.CopyTo(anchor.AsSpan(14));byte[] request=EthernetFrameItExistingClassifierTests.Request.ToArray();
        byte[] reply=EthernetFrameItExistingClassifierTests.Reply().AsSpan(12,42).ToArray();
        uint Crc(byte[] data)=>OS.Std.Hash.Crc32.Compute(data);
        List<byte[]> frames=[RouterAdvertisement(),RouterAdvertisement(),warmup,anchor,request,reply,request,reply];
        for(int index=0;index<4;index++)
        {
            ushort port=(ushort)(40000+index);uint client=(uint)(1000+index*100000),server=(uint)(9000+index*100000);
            void Add(bool outgoing,uint seq,uint ack,byte flags,ReadOnlySpan<byte> payload=default)=>frames.Add(Tcp(outgoing,port,seq,ack,flags,payload));
            Add(false,client,0,2);Add(true,server,client+1,0x12);client++;server++;Add(false,client,server,0x10);
            byte[] sent=index==1?[0x52]:"CLROS_SOCKET_REQUEST"u8.ToArray();
            Add(false,client,server,0x18,sent);client+=(uint)sent.Length;Add(true,server,client,0x10);
            if(index==1){Add(false,client,server,0x14);continue;}
            if(index>=2){Add(false,client,server,0x11);client++;Add(true,server,client,0x10);}
            byte[] payload=index==0?Enumerable.Range(0,65536).Select(i=>(byte)(i%251)).ToArray():"CLROS_SOCKET_REPLY"u8.ToArray();
            for(int offset=0;offset<payload.Length;)
            {
                int count=Math.Min(1024,payload.Length-offset);Add(true,server,client,0x18,payload.AsSpan(offset,count));server+=(uint)count;offset+=count;Add(false,client,server,0x10);
            }
            Add(true,server,client,0x11);server++;
            if(index==0){Add(false,client,server,0x11);client++;Add(true,server,client,0x10);}
            else Add(false,client,server,0x10);
        }
        frames.Add(RouterAdvertisement());return(frames,Crc(anchor),new(Crc(reply),42,Crc(request),0x52550a000202));
    }
    [HostTest("socket packet proof checks literal stream bytes retransmissions reset and both acknowledged FIN directions")]
    private static void Wire()
    {
        var fixture=Packets();var report=QemuTranscripts.InspectSocketPacketsForTests(Capture(fixture.Frames),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]);
        HostCheck.True(report.Connections==4&&report.Received==61&&report.Sent==65572&&report.Resets==1&&report.ClientFins==3&&report.ServerFins==3&&report.PumpIgnored==1);
        var duplicate=fixture.Frames.ToList();int payload=duplicate.FindIndex(frame=>frame.Length>1000);duplicate.Insert(payload+1,duplicate[payload]);
        HostCheck.Equal(65572,QemuTranscripts.InspectSocketPacketsForTests(Capture(duplicate),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]).Sent);
        var corrupt=fixture.Frames.Select(frame=>(byte[])frame.Clone()).ToList();corrupt[payload][54]^=1;
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(corrupt),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_tcp_checksum");
        var hole=fixture.Frames.ToList();(hole[payload],hole[payload+2])=(hole[payload+2],hole[payload]);
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(hole),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_acknowledgement");
        foreach((byte old,byte flags,string reason) in new[]{((byte)0x12,(byte)0x13,"socket_wire_syn_ack"),((byte)0x14,(byte)0x15,"socket_wire_reset")})
        {
            var changed=fixture.Frames.ToList();int at=changed.FindIndex(frame=>frame.Length>=54&&frame[47]==old);byte[] original=changed[at];
            bool outgoing=original[6]==2;ushort port=BinaryPrimitives.ReadUInt16BigEndian(original.AsSpan(outgoing?36:34));
            changed[at]=Tcp(outgoing,port,BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38)),
                BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(42)),flags,original.AsSpan(54));
            Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(changed),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),reason);
        }
        var foreignArp=fixture.Frames.Select(frame=>(byte[])frame.Clone()).ToList();
        int resolution=foreignArp.FindLastIndex(frame=>frame.Length>=42&&frame[12]==8&&frame[13]==6&&frame[21]==2);foreignArp[resolution][32]^=1;
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(foreignArp),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_arp_envelope");
        var badV6=fixture.Frames.Select(frame=>(byte[])frame.Clone()).ToList();badV6[^1][56]^=1;
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(badV6),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_ignored_ipv6_checksum");
        var badParent=fixture.Frames.ToList();badParent.RemoveAt(badParent.FindIndex(frame=>frame.Length>=42&&frame[12]==8&&frame[13]==6));
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(badParent),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_parent_arp");
        var missing=fixture.Frames.ToList();missing.RemoveAt(missing.FindLastIndex(frame=>frame.Length>=54&&frame[12]==8&&frame[13]==0&&frame[23]==6));
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(missing),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),"socket_wire_complete");
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Capture(fixture.Frames),fixture.Anchor,fixture.Arp,[40001,40000,40002,40003]),"socket_wire_original_tuple");
    }
}
