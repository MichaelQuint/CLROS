using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static readonly string[] SocketLifetimeFields="slot owner epoch generation index local localPort remote remotePort clock deadline duration".Split(' ').Select(s=>s+":dec").ToArray();
    private static bool IsSocketTransport(string row)=>row.StartsWith("M16_011_SLOT ",StringComparison.Ordinal)||row.StartsWith("M16_011_TIMEWAIT ",StringComparison.Ordinal);
    private static Dictionary<string,ulong> SocketTransport(string row)=>row.StartsWith("M16_011_SLOT ",StringComparison.Ordinal)?
        ReadRecord(row,"M16_011_SLOT",["event:dec",..SocketLifetimeFields]):ReadRecord(row,"M16_011_TIMEWAIT",[..SocketLifetimeFields,"drain:dec"]);

    private sealed class SocketLifetime(Dictionary<string,ulong> first)
    {
        internal readonly Dictionary<string,ulong> First=first;
        internal ulong Retired,MaximumDeadline;
        internal int Waits;internal bool Drain;
        internal uint RemotePort=checked((uint)first["remotePort"]);
    }
    private sealed record SocketNetworkProof(string[] Nic,QemuArpReceiveReceipt Arp,Dictionary<string,ulong> Peer,
        Dictionary<string,ulong> Wire,Dictionary<string,ulong> End,Dictionary<string,ulong> Stop,List<SocketLifetime> Lifetimes);

    /// <summary>Value-only transport observation accounting for synthetic
    /// controls. It grants no firmware, packet, process or guest authority.</summary>
    public readonly record struct SocketTransportInspection(int Fresh,int Retired,int TimeWaitLifetimes,ulong MaximumDeadline);
    /// <summary>Check original handle/tuple lifetimes and actual TIME-WAIT
    /// deadline retention in supplied rows. Inputs are borrowed; malformed,
    /// stale, duplicate, early or incomplete releases throw. Parent and wire
    /// evidence are deliberately outside this synthetic inspection seam.</summary>
    public static SocketTransportInspection InspectSocketTransportForTests(string[] rows)
    {
        var result=SocketLifetimes(rows);return new(result.Count,result.Count(x=>x.Retired!=0),result.Count(x=>x.Waits!=0),result.Max(x=>x.MaximumDeadline));
    }
    private static List<SocketLifetime> SocketLifetimes(string[] rows,bool continuingWeb=false)
    {
        List<SocketLifetime> all=[];Dictionary<string,SocketLifetime> known=[];Dictionary<ulong,SocketLifetime> active=[];
        Dictionary<ulong,ulong> generations=[];ulong clock=0,owner=0,epoch=0;
        foreach(string row in rows.Where(IsSocketTransport))
        {
            var r=SocketTransport(row);bool slot=row.StartsWith("M16_011_SLOT ",StringComparison.Ordinal);
            Need(r["slot"]<8 && r["index"]<8 && r["owner"]>=4096 && r["owner"]%8==0 && r["epoch"]!=0 && r["generation"]!=0 &&
                r["local"]==0x0a00020f && r["localPort"]==8080 && r["remote"]==0x0a000202 && r["remotePort"] is >0 and <=65535 &&
                r["clock"]>=clock && r["clock"]<=long.MaxValue,"socket_transport_identity");clock=r["clock"];
            if(owner==0){owner=r["owner"];epoch=r["epoch"];}Need(owner==r["owner"] && epoch==r["epoch"],"socket_transport_owner");
            string key=string.Join(":",new[]{r["owner"],r["epoch"],r["generation"],r["index"]});
            if(slot && r["event"]==0)
            {
                Need(!known.ContainsKey(key) && !active.ContainsKey(r["slot"]) && active.Values.All(v=>v.First["index"]!=r["index"]) &&
                    r["generation"]==generations.GetValueOrDefault(r["index"])+1 && r["deadline"]==0 && r["duration"]==0,"socket_transport_fresh");
                var value=new SocketLifetime(r);known.Add(key,value);active.Add(r["slot"],value);all.Add(value);continue;
            }
            Need(known.TryGetValue(key,out var lifetime) && active.GetValueOrDefault(r["slot"])==lifetime && lifetime!.Retired==0 &&
                new[]{"slot","index","local","localPort","remote","remotePort"}.All(field=>r[field]==lifetime.First[field]),"socket_transport_generation");
            if(slot)
            {
                Need(r["event"]==1 && r["deadline"]>=lifetime!.MaximumDeadline && clock>=r["deadline"] &&
                    (r["deadline"]==0?r["duration"]==0:r["duration"]==240_000_000_000 && lifetime.Waits!=0),"socket_transport_retire");
                lifetime!.MaximumDeadline=r["deadline"];lifetime.Retired=clock;active.Remove(r["slot"]);generations[r["index"]]=r["generation"];
            }
            else
            {
                Need(r["drain"]<=1 && lifetime!.Waits<2 && !lifetime.Drain && (lifetime.Waits==0 || r["drain"]==1) &&
                    r["duration"]==240_000_000_000 && r["deadline"]>=r["duration"] && clock>=r["deadline"]-r["duration"] &&
                    r["deadline"]>=lifetime.MaximumDeadline,"socket_transport_time_wait");
                lifetime!.MaximumDeadline=r["deadline"];lifetime.Waits++;lifetime.Drain=r["drain"]==1;
            }
        }
        Need(continuingWeb?all.Count==2 && active.Count>0 && all.Select(v=>v.RemotePort).Distinct().Count()==2:
            all.Count==4 && active.Count==0 && all.All(v=>v.Retired!=0) && all.Any(v=>v.Waits!=0),"socket_transport_complete");return all;
    }

    internal static ReadOnlySpan<byte> SocketRawLeaf(ReadOnlySpan<byte> raw)
    {
        ReadOnlySpan<byte> start="TEST_START m16-009\r\n"u8,end="CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n"u8;
        int at=raw.IndexOf(start);Need(at>=0 && raw[(at+start.Length)..].IndexOf(start)<0,"socket_raw_leaf_start");
        int last=raw[at..].IndexOf(end);Need(last>=0,"socket_raw_leaf_end");last+=at+end.Length;
        Need(raw[last..].StartsWith("M16_011_WIRE "u8),"socket_raw_leaf_boundary");return raw[at..last];
    }
    private static string NormalizeSocketWrites(string text,bool asynchronous)
    {
        byte[] raw=Encoding.UTF8.GetBytes(text);var leaf=SocketRawLeaf(raw);int start=raw.AsSpan().IndexOf(leaf);
        var capture=NativeAotSocketGuest.Read(asynchronous,leaf);
        return DecodeUtf8(raw.AsSpan(0,start).ToArray())+capture.Output.NormalizedTail+DecodeUtf8(raw.AsSpan(start+leaf.Length).ToArray());
    }

    internal static IReadOnlyList<string> SocketMarkers(bool asynchronous)=>[
        ..QemuContracts.M03OwnedSubsystemMarkers,"TEST_START m16-002","M16_002_STAGED","M16_002_DIGEST","M16_002_PACKAGE_DIGEST",
        "M16_002_OWNED","CLROS_M16_002_READY","TEST_PASS m16-002",..M16LinuxStartupMarkers,..M16ServiceLoopMarkers,
        ..QemuContracts.M05ApicSetupMarkers.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count),
        ..QemuContracts.M07ArpReceiveMarkers.Skip(QemuContracts.M05PciEnumerationMarkers.Count),
        "TEST_START m07-026","M07_026_ROW stage=0 ","M16_011_PEER ","TEST_START m16-011",
        ..asynchronous?new[]{"TEST_START m16-012"}:Array.Empty<string>(),..M16LinuxSignalMarkers.Take(3),
        "M16_011_BOUND ",..M16LinuxSignalMarkers.Skip(3),"M16_011_WIRE ","M16_011_END ",
        "M07_026_ROW stage=4 ","M07_026_ROW stage=5 ","M07_026_ROW stage=6 ","CLROS_M07_026_READY","TEST_PASS m07-026",
        "CLROS_M16_011_READY","TEST_PASS m16-011",..asynchronous?new[]{"CLROS_M16_012_READY","TEST_PASS m16-012"}:Array.Empty<string>()];

    private static (string[] Parent,SocketNetworkProof Proof) ValidateSocketNetworkTail(string[] lines,byte[] raw,byte[] package,NativeAotSocketProgram program)
    {
        var envelope=SocketNetworkEnvelope(lines,program.Asynchronous);
        var tables=ReadRecord(lines[QemuContracts.M03OwnedSubsystemMarkers.Count+10],"M16_004_TABLES","root:dec","used:dec","leaves:dec","ranges:dec");
        const string restored=" restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        string readback=lines[QemuContracts.M03OwnedSubsystemMarkers.Count+11];Need(readback.EndsWith(restored,StringComparison.Ordinal),"socket_parent_readback");
        var kernel=ReadRecord(readback[..^restored.Length],"M16_004_READBACK","old:dec");
        ValidateNativeAotSocketTail(program.SourceRoot,package,program,SocketRawLeaf(raw),tables["root"],kernel["old"]);
        string[] parent=ValidateServiceLoopTail(lines[..envelope.ParentsEnd],true);
        parent=ValidateLinuxStartupTail(parent,package);parent=ValidateLinuxElfTail(parent,package);
        return ([..parent,..envelope.Apic.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count)],envelope.Proof);
    }

    /// <summary>Value-only combined APIC/NIC wrapper inspection for synthetic
    /// controls. Validates observed transport/cleanup associations but does not
    /// validate or authorize firmware, package/startup, application execution,
    /// independent peer process or packet evidence.</summary>
    public static SocketTransportInspection InspectSocketNetworkForTests(string[] rows,bool asynchronous)
    {
        var result=SocketNetworkEnvelope(rows,asynchronous).Proof.Lifetimes;
        return new(result.Count,result.Count(x=>x.Retired!=0),result.Count(x=>x.Waits!=0),result.Max(x=>x.MaximumDeadline));
    }
    private static (int ParentsEnd,string[] Apic,SocketNetworkProof Proof) SocketNetworkEnvelope(string[] lines,bool asynchronous)
    {
        int parentsEnd=QemuContracts.M03OwnedSubsystemMarkers.Count+7+M16LinuxStartupMarkers.Count+M16ServiceLoopMarkers.Count;
        int apicEnd=parentsEnd+QemuContracts.M05ApicSetupMarkers.Count-QemuContracts.M03OwnedSubsystemMarkers.Count;
        int networkStart=apicEnd+QemuContracts.M07ArpReceiveMarkers.Count-QemuContracts.M05PciEnumerationMarkers.Count;
        int cursor=networkStart;
        void Exact(string expected){Need(cursor<lines.Length&&lines[cursor++]==expected,"socket_parent_order_"+expected);}
        Exact("TEST_START m07-026");var dma=ReadRecord(lines[cursor++],"M07_026_ROW","stage:dec","a:dec","b:dec","c:dec","d:dec");
        var peer=ReadRecord(lines[cursor++],"M16_011_PEER","mac:dec","sent:dec","received:dec","start:dec","end:dec");
        Exact("TEST_START m16-011");if(asynchronous)Exact("TEST_START m16-012");
        int leafStart=cursor;Need(lines[leafStart]=="TEST_START m16-009","socket_parent_leaf");
        int leafEnd=Array.IndexOf(lines,"TEST_PASS m16-009",leafStart)+1;Need(leafEnd>leafStart,"socket_parent_leaf_end");
        var lifetimes=SocketLifetimes(lines[leafStart..leafEnd]);cursor=leafEnd;
        var wire=ReadRecord(lines[cursor++],"M16_011_WIRE","received:dec","arp:dec","accepted:dec","ignored:dec","capacity:dec","arpCopied:dec","tcpCopied:dec");
        const string cleanup=" retained=0 buffers=scrubbed descriptors=closed workers=reaped";string endLine=lines[cursor++];
        Need(endLine.EndsWith(cleanup,StringComparison.Ordinal),"socket_network_cleanup");
        var end=ReadRecord(endLine[..^cleanup.Length],"M16_011_END","polls:dec","received:dec","copied:dec","fresh:dec","retired:dec","start:dec","end:dec","maxDeadline:dec");
        Dictionary<string,ulong> Row(uint stage){var value=ReadRecord(lines[cursor++],"M07_026_ROW","stage:dec","a:dec","b:dec","c:dec","d:dec");Need(value["stage"]==stage,"socket_network_stage");return value;}
        var stop=Row(4);var pool=Row(5);var allocator=Row(6);
        Exact("CLROS_M07_026_READY");Exact("TEST_PASS m07-026");Exact("CLROS_M16_011_READY");Exact("TEST_PASS m16-011");
        if(asynchronous){Exact("CLROS_M16_012_READY");Exact("TEST_PASS m16-012");}Need(cursor==lines.Length,"socket_parent_extra_record");
        string[] apic=[..lines[..QemuContracts.M03OwnedSubsystemMarkers.Count],..lines[parentsEnd..apicEnd]];
        Need(apic.Length==QemuContracts.M05ApicSetupMarkers.Count,"socket_apic_extent");AssertM05ApicSetupRecords(apic);
        string[] nic=[..apic[..QemuContracts.M05PciEnumerationMarkers.Count],..lines[apicEnd..networkStart]];
        var arp=AssertM07ArpReceiveRecords(nic);
        Need(peer["mac"]==arp.PeerMac && peer["sent"]==1 && peer["received"]>=1 && peer["start"]>=end["start"] &&
            peer["end"]>=peer["start"] && peer["end"]<=lifetimes.Min(v=>v.First["clock"]),"socket_peer_parent");
        Need(end["fresh"]==4 && end["retired"]==4 && end["polls"]>0 && end["received"]==wire["received"] &&
            end["copied"]==wire["tcpCopied"] && wire["received"]==wire["arp"]+wire["accepted"]+wire["ignored"]+wire["capacity"] &&
            wire["capacity"]==0 && end["maxDeadline"]==lifetimes.Max(v=>v.MaximumDeadline) && end["end"]>=end["maxDeadline"] &&
            end["end"]>=lifetimes.Max(v=>v.Retired),"socket_network_accounting");
        Need(dma["stage"]==0 && dma["a"]>=4096 && dma["a"]%4096==0 && dma["a"]<=0x100000000UL-32768 && dma["b"]==32768 &&
            dma["c"]==QemuContracts.M07ArpReceiveFeatures && dma["d"]==8 && stop["a"]==peer["received"]+wire["received"] &&
            stop["b"]==peer["sent"]+wire["arpCopied"]+wire["tcpCopied"] && stop["c"] is >=1 and <=1024 && stop["d"]==2 &&
            pool["a"]==8 && pool["b"]==0 && pool["c"]==8 && pool["d"]==1,"socket_nic_cleanup");
        ulong[] baseline=PrefixRow(nic,"M07_ARP A",4),binding=PrefixRow(nic,"M07_ARP 0",4),oldPages=PrefixRow(nic,"M07_ARP 2",4);
        Need(allocator["a"]==baseline[0] && allocator["b"]==baseline[1] && allocator["c"]==baseline[2] && allocator["d"]==binding[2] &&
            (dma["a"]+32768<=oldPages[0]||oldPages[0]+8192<=dma["a"]) &&
            (dma["a"]+32768<=binding[3]||binding[3]+16384<=dma["a"]),"socket_allocator_association");
        return (parentsEnd,apic,new(nic,arp,peer,wire,end,stop,lifetimes));
    }

    private sealed class SocketWireFlow(uint port,int index,uint sequence)
    {
        internal readonly uint Port=port,Client=sequence+1;
        internal readonly int Index=index;
        internal uint Server;internal bool SynAck,ClientFin,ServerFin,ClientFinAcked,ServerFinAcked,Reset;
        internal int ClientSeen,ServerSeen;
        internal readonly bool[] Request=new bool[index==1?1:20],Reply=new bool[index==0?65536:index==1?0:18];
    }
    /// <summary>Value-only original socket wire totals; no execution authority.</summary>
    public readonly record struct SocketPacketInspection(int Connections,int Received,int Sent,int Resets,int ClientFins,int ServerFins,
        ulong PeerReceived,ulong PumpReceived,ulong PumpArp,ulong PumpArpCopied,ulong PumpTcpCopied,ulong PumpIgnored);

    /// <summary>Inspect a bounded original PCAP with its independent NIC/ARP
    /// anchors and four retained original remote ports. Checks checksums,
    /// sequence-relative literal stream bytes, retransmission equality, actual
    /// reset/half-close and complete FINs. Supplied values grant no source,
    /// firmware, peer-child or prepared guest authority.</summary>
    public static SocketPacketInspection InspectSocketPacketsForTests(byte[] capture,uint anchor,QemuArpReceiveReceipt arp,IReadOnlyList<uint> ports)
        =>SocketPackets(capture,anchor,arp,ports);

    private static SocketPacketInspection SocketPackets(byte[] bytes,uint anchor,QemuArpReceiveReceipt arp,IReadOnlyList<uint> ports)
    {
        Need(ports.Count==4 && ports.All(p=>p is >0 and <=65535),"socket_wire_ports");
        _=QemuEthernetCapture.Inspect(bytes,anchor);
        int offset=24,records=0,prefixEnd=0,prefixStage=0;ulong timestamp=0,peerReceived=0,pumpReceived=0,arpReceived=0,arpCopied=0,tcpCopied=0,ignored=0;
        bool resolved=false;int resolutionSent=0;List<SocketWireFlow> flows=[];Dictionary<uint,SocketWireFlow> current=[];
        while(offset<bytes.Length)
        {
            ulong now=(ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset))*1_000_000+BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset+4));
            Need(now>=timestamp,"socket_wire_clock");timestamp=now;
            int size=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset+8)));offset+=16;
            ReadOnlySpan<byte> frame=bytes.AsSpan(offset,size);offset+=size;records++;
            if(prefixStage<3)
            {
                if(prefixStage==0)
                {
                    if(frame.Length==60 && frame.Slice(14,9).SequenceEqual("CLROS-M07"u8) && OS.Std.Hash.Crc32.Compute(frame)==anchor)prefixStage=1;
                    continue; // Original firmware traffic precedes the owned anchor.
                }
                Need(OS.Std.Hash.Crc32.Compute(frame)==(prefixStage==1?arp.RequestCrc32:arp.ReceiveCrc32) &&
                    frame.Length==(prefixStage==1?60:arp.ReceiveBytes),"socket_wire_parent_arp");
                prefixStage++;prefixEnd=offset;continue;
            }
            Need(frame.Length>=14,"socket_wire_ethernet");
            if(SocketIgnoredRouterAdvertisement(frame))
            {if(resolved){pumpReceived++;ignored++;}else peerReceived++;continue;}
            bool outgoing=MatchNetworkMac(frame[6..],0x020000000001),incoming=MatchNetworkMac(frame[6..],arp.PeerMac);
            Need(outgoing!=incoming && (outgoing?MatchNetworkMac(frame,arp.PeerMac)||frame[..6].IndexOfAnyExcept((byte)255)<0:
                MatchNetworkMac(frame,0x020000000001)||frame[..6].IndexOfAnyExcept((byte)255)<0),"socket_wire_mac");
            ushort type=BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if(!resolved)
            {
                Need(type==0x0806,"socket_wire_resolution_type");
                CheckSocketArp(frame,outgoing,arp.PeerMac,out ushort operation);
                if(outgoing){Need(operation==1 && ++resolutionSent==1,"socket_wire_resolution_request");}
                else {peerReceived++;if(operation==2){Need(resolutionSent==1,"socket_wire_resolution_order");resolved=true;}}
                continue;
            }
            if(incoming)pumpReceived++;
            if(type==0x0806)
            {
                CheckSocketArp(frame,outgoing,arp.PeerMac,out _);if(outgoing)arpCopied++;else arpReceived++;continue;
            }
            if(!ReadSocketTcpFrame(frame,outgoing,arp.PeerMac,out var packet)){ignored++;continue;}
            uint remote=packet.Remote,sequence=packet.Sequence,ack=packet.Ack;byte flags=packet.Flags;var payload=packet.Payload;
            if(outgoing)tcpCopied++;
            current.TryGetValue(remote,out var flow);
            if(incoming && (flags&0x12)==2)
            {
                Need(payload.Length==0 && (flags&5)==0,"socket_wire_syn");
                if(flow is not null && !flow.Reset && !(flow.ClientFin&&flow.ServerFin))Need(sequence+1==flow.Client,"socket_wire_syn_retry");
                else
                {
                    Need(flows.Count<4 && remote==ports[flows.Count],"socket_wire_original_tuple");
                    flow=new(remote,flows.Count,sequence);flows.Add(flow);current[remote]=flow;
                }
                continue;
            }
            Need(flow is not null,"socket_wire_unowned_tuple");
            if(outgoing && (flags&0x12)==0x12)
            {
                Need(flags==0x12 && payload.Length==0 && ack==flow!.Client && (!flow.SynAck || flow.Server==sequence+1),"socket_wire_syn_ack");
                flow!.SynAck=true;flow.Server=sequence+1;continue;
            }
            Need(flow!.SynAck && (flags&2)==0 && (flags&0x10)!=0,"socket_wire_established");
            uint relative=unchecked(sequence-(outgoing?flow.Server:flow.Client));
            bool[] received=outgoing?flow.Reply:flow.Request;
            Need(relative<=(uint)received.Length+(outgoing?flow.ServerFin?1U:0:flow.ClientFin?1U:0),"socket_wire_sequence");
            uint acknowledge=unchecked(ack-(outgoing?flow.Client:flow.Server));
            Need(acknowledge<=(uint)(outgoing?flow.ClientSeen:flow.ServerSeen)+(outgoing?flow.ClientFin?1U:0:flow.ServerFin?1U:0),"socket_wire_acknowledgement");
            if(outgoing && flow.ClientFin && acknowledge==flow.Request.Length+1)flow.ClientFinAcked=true;
            if(incoming && flow.ServerFin && acknowledge==flow.Reply.Length+1)flow.ServerFinAcked=true;
            if((flags&4)!=0)
            {Need(flags==0x14 && incoming && flow.Index==1 && payload.Length==0 && flow.Request.All(v=>v),"socket_wire_reset");flow.Reset=true;continue;}
            Need(!flow.Reset && (payload.Length==0 || relative+payload.Length<=received.Length),"socket_wire_payload_extent");
            for(int i=0;i<payload.Length;i++)
            {
                int at=checked((int)relative+i);byte expected=outgoing?flow.Index==0?(byte)(at%251):"CLROS_SOCKET_REPLY"u8[at]:
                    flow.Index==1?(byte)0x52:"CLROS_SOCKET_REQUEST"u8[at];
                Need(payload[i]==expected,"socket_wire_literal_payload");received[at]=true;
            }
            if(outgoing)
            {
                Need(flow.Index<2 || payload.Length==0 || flow.ClientFin,"socket_wire_half_close_order");
                while(flow.ServerSeen<received.Length&&received[flow.ServerSeen])flow.ServerSeen++;
            }
            else while(flow.ClientSeen<received.Length&&received[flow.ClientSeen])flow.ClientSeen++;
            if((flags&1)!=0)
            {
                Need(flow.Index!=1 && relative+payload.Length==received.Length && received.All(v=>v),"socket_wire_fin_sequence");
                if(outgoing)flow.ServerFin=true;else {Need(flow.Index!=0 || flow.ServerFin,"socket_wire_active_close_order");flow.ClientFin=true;}
            }
        }
        Need(prefixStage==3,"socket_wire_parent_prefix");_=QemuEthernetCapture.InspectArp(bytes.AsSpan(0,prefixEnd),arp);
        Need(resolved && resolutionSent==1 && flows.Count==4 && flows.All(f=>f.Request.All(v=>v)&&f.Reply.All(v=>v)&&
            (f.Index==1?f.Reset&&!f.ClientFin&&!f.ServerFin:!f.Reset&&f.ClientFin&&f.ServerFin&&f.ClientFinAcked&&f.ServerFinAcked)),"socket_wire_complete");
        return new(4,flows.Sum(f=>f.Request.Length),flows.Sum(f=>f.Reply.Length),flows.Count(f=>f.Reset),flows.Count(f=>f.ClientFin),flows.Count(f=>f.ServerFin),
            peerReceived,pumpReceived,arpReceived,arpCopied,tcpCopied,ignored);
    }
    private readonly ref struct SocketWireFrame(uint remote,uint sequence,uint ack,byte flags,ReadOnlySpan<byte> payload)
    {
        internal readonly uint Remote=remote,Sequence=sequence,Ack=ack;
        internal readonly byte Flags=flags;
        internal readonly ReadOnlySpan<byte> Payload=payload;
    }
    // Shared measured IPv4/TCP envelope. Callers own Ethernet direction,
    // anchored ARP, flow/lifetime accounting and literal application streams.
    private static bool ReadSocketTcpFrame(ReadOnlySpan<byte> frame,bool outgoing,ulong peer,out SocketWireFrame value)
    {
        value=default;ushort type=BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
        Need(type==0x0800 && frame.Length>=34 && MatchNetworkMac(frame,outgoing?peer:0x020000000001),"socket_wire_ipv4");
        var ip=frame[14..];int ihl=(ip[0]&15)*4,total=BinaryPrimitives.ReadUInt16BigEndian(ip[2..]);
        Need((ip[0]>>4)==4 && ihl>=20 && ihl<=60 && total>=ihl && total<=1500 && total<=ip.Length &&
            (BinaryPrimitives.ReadUInt16BigEndian(ip[6..])&0x3fff)==0 && ip[8]!=0 && NetworkChecksum(ip[..ihl]) &&
            BinaryPrimitives.ReadUInt32BigEndian(ip[12..])==(outgoing?0x0a00020fU:0x0a000202U) &&
            BinaryPrimitives.ReadUInt32BigEndian(ip[16..])==(outgoing?0x0a000202U:0x0a00020fU),"socket_wire_ip_envelope");
        if(ip[9]!=6){Need(!outgoing && ip[9]==17,"socket_wire_non_tcp");return false;}
        var tcp=ip.Slice(ihl,total-ihl);Need(tcp.Length>=20,"socket_wire_tcp_extent");int header=(tcp[12]>>4)*4;
        Need(header>=20 && header<=tcp.Length,"socket_wire_tcp_header");
        byte[] checksum=new byte[12+tcp.Length];ip.Slice(12,8).CopyTo(checksum);checksum[9]=6;
        BinaryPrimitives.WriteUInt16BigEndian(checksum.AsSpan(10),checked((ushort)tcp.Length));tcp.CopyTo(checksum.AsSpan(12));
        Need(NetworkChecksum(checksum),"socket_wire_tcp_checksum");
        uint source=BinaryPrimitives.ReadUInt16BigEndian(tcp),destination=BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]);
        Need((outgoing?source:destination)==8080,"socket_wire_port");uint remote=outgoing?destination:source;
        uint sequence=BinaryPrimitives.ReadUInt32BigEndian(tcp[4..]),ack=BinaryPrimitives.ReadUInt32BigEndian(tcp[8..]);byte flags=tcp[13];
        Need((flags&0xc0)==0 && (flags&0x20)==0,"socket_wire_tcp_flags");var payload=tcp[header..];
        value=new(remote,sequence,ack,flags,payload);return true;
    }

    // The default QEMU usernet emits this IPv6 router advertisement even though
    // the admitted application/transport is IPv4-only. Count its checked bytes
    // as ignored; it grants no IPv6 transport or application authority.
    private static bool SocketIgnoredRouterAdvertisement(ReadOnlySpan<byte> frame)
    {
        if(BinaryPrimitives.ReadUInt16BigEndian(frame[12..])!=0x86dd)return false;
        Need(frame.Length==110 && MatchNetworkMac(frame,0x333300000001) && MatchNetworkMac(frame[6..],0x525600000002),"socket_wire_ignored_ipv6");
        var ip=frame[14..];
        Need(ip[..8].SequenceEqual(Convert.FromHexString("6000000000383AFF")) &&
            ip.Slice(8,16).SequenceEqual(Convert.FromHexString("FE800000000000000000000000000002")) &&
            ip.Slice(24,16).SequenceEqual(Convert.FromHexString("FF020000000000000000000000000001")) &&
            ip[40]==134 && ip[41]==0 && ip[44..].SequenceEqual(Convert.FromHexString(
                "4000070800000000000000000101525600000002030440C0000151800000384000000000FEC00000000000000000000000000000")),"socket_wire_ignored_ipv6");
        byte[] checksum=new byte[96];ip.Slice(8,32).CopyTo(checksum);BinaryPrimitives.WriteUInt32BigEndian(checksum.AsSpan(32),56);
        checksum[39]=58;ip[40..].CopyTo(checksum.AsSpan(40));Need(NetworkChecksum(checksum),"socket_wire_ignored_ipv6_checksum");return true;
    }
    private static void CheckSocketArp(ReadOnlySpan<byte> frame,bool outgoing,ulong peer,out ushort operation)
    {
        Need(frame.Length>=42,"socket_wire_arp_extent");var packet=frame[14..];operation=BinaryPrimitives.ReadUInt16BigEndian(packet[6..]);
        Need(BinaryPrimitives.ReadUInt16BigEndian(packet)==1 && BinaryPrimitives.ReadUInt16BigEndian(packet[2..])==0x0800 && packet[4]==6 && packet[5]==4 &&
            operation is 1 or 2 && MatchNetworkMac(packet[8..],outgoing?0x020000000001:peer) &&
            BinaryPrimitives.ReadUInt32BigEndian(packet[14..])==(outgoing?0x0a00020fU:0x0a000202U) &&
            BinaryPrimitives.ReadUInt32BigEndian(packet[24..])==(outgoing?0x0a000202U:0x0a00020fU) &&
            (operation!=2 || MatchNetworkMac(packet[18..],outgoing?peer:0x020000000001)),"socket_wire_arp_envelope");
    }

    private static void AssertSocketCapture(string path,SocketNetworkProof proof)
    {
        ulong[] anchor=PrefixRow(proof.Nic,"M07_NET D",4);Need(anchor[0]<=uint.MaxValue,"socket_capture_anchor_width");
        var report=SocketPackets(File.ReadAllBytes(path),(uint)anchor[0],proof.Arp,proof.Lifetimes.Select(v=>v.RemotePort).ToArray());
        Need(report.Connections==4 && report.Received==61 && report.Sent==65572 && report.Resets==1 && report.ClientFins==3 && report.ServerFins==3 &&
            report.PeerReceived==proof.Peer["received"] && report.PumpReceived==proof.Wire["received"] && report.PumpArp==proof.Wire["arp"] &&
            report.PumpIgnored==proof.Wire["ignored"] && report.PumpReceived-report.PumpArp-report.PumpIgnored==proof.Wire["accepted"] &&
            report.PumpArpCopied==proof.Wire["arpCopied"] && report.PumpTcpCopied==proof.Wire["tcpCopied"],"socket_capture_serial_join");
    }

    internal static AuthorityFiles HoldSocketEvidence(string root,QemuSmokeOptions options,NativeAotSocketPeer.Completion peer)
    {
        AuthorityFiles files=new();
        try
        {
            var serial=files.Open(root,Path.GetFullPath(options.SerialLog,root),"socket serial","socket_serial",8*1024*1024,singleLink:true);
            var summary=files.Open(root,Path.GetFullPath(options.SummaryLog,root),"socket runner summary","socket_summary",16*1024*1024,singleLink:true);
            var capture=files.Open(root,Path.GetFullPath(options.SerialLog+".pcap",root),"socket packet capture","socket_capture",4*1024*1024,singleLink:true);
            peer.Verify(files);var literal=files.Open(root,peer.Path,"socket peer","socket_peer_log",65536,singleLink:true);
            AuthorityFiles.RequireDistinct(serial,summary,capture,literal);return files;
        }
        catch{files.Dispose();throw;}
    }
}
