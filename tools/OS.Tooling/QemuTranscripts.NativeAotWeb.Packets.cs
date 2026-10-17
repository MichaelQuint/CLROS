using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal static AuthorityFiles HoldWebEvidence(string root,QemuSmokeOptions options)
    {
        AuthorityFiles files=new();
        try
        {
            var serial=files.Open(root,Path.GetFullPath(options.SerialLog,root),"web serial","web_evidence_serial",8*1024*1024,singleLink:true);
            var summary=files.Open(root,Path.GetFullPath(options.SummaryLog,root),"web summary","web_evidence_summary",16*1024*1024,singleLink:true);
            var packets=files.Open(root,Path.GetFullPath(options.SerialLog+".pcap",root),"web PCAP","web_evidence_packets",4*1024*1024,singleLink:true);
            AuthorityFiles.RequireDistinct(serial,summary,packets);return files;
        }
        catch{files.Dispose();throw;}
    }

    private static WebPacketInspection ValidateWebPackets(byte[] package,byte[] raw,byte[] packets,
        WebParentReport parent,WebReplayReport leaf,int forward)
    {
        int at=raw.AsSpan().IndexOf("TEST_START m10.004\r\n"u8);Need(at>=0,"web_packet_leaf");
        var framed=NativeAotThreadingOutput.ReadWeb(raw.AsSpan(at));
        string complete=framed.NormalizedTail[..(framed.NormalizedTail.Length-leaf.Observations.TrailingFragment.Length)];
        var lifetimes=SocketLifetimes(complete[..^2].Split("\r\n",StringSplitOptions.None),continuingWeb:true);
        var configuration=NativeAotWebProgram.DescribeConfiguration(package);
        var report=InspectNativeAotWebPacketsForTests(packets,parent.FrameAnchor,parent.Arp,
            lifetimes.Select(l=>l.RemotePort).ToArray(),forward,configuration.Body,configuration.Json,configuration.LogQuota);
        Need(report.Connections==leaf.SocketAccepts && (ulong)report.Received==leaf.Received &&
            (ulong)report.Sent==leaf.Sent,"web_packet_leaf_join");
        Need(report.TimeWaitPorts.All(port=>lifetimes.Any(l=>l.RemotePort==port && l.Waits>0)),"web_packet_time_wait_join");
        return report;
    }
    /// <summary>Observed anchored HTTP streams and TCP close directions.
    /// A FIN or FIN ACK supplies no guest descriptor release, worker reap,
    /// device stop, TIME_WAIT drain or complete runner/source qualification.</summary>
    public readonly record struct WebPacketInspection(int Connections,int TcpRecords,int Received,int Sent,
        int ClientFins,int ServerFins,int ClientFinAcks,int ServerFinAcks,string HealthSha256,string ConfigurationSha256,
        IReadOnlyList<uint> TimeWaitPorts);

    private sealed class WebWireFlow(uint port,uint client,byte[] request)
    {
        internal readonly uint Port=port,Client=unchecked(client+1);
        internal readonly byte[] Request=request,Response=new byte[8192];
        internal readonly bool[] RequestSeen=new bool[request.Length],ResponseSeen=new bool[8192];
        internal uint Server;
        internal int ClientSeen,ServerSeen,ResponseExtent;
        internal bool SynAck,ClientReady,ClientFin,ServerFin,ClientFinAcked,ServerFinAcked,ServerFinFirst;
    }

    /// <summary>Inspect bounded original QEMU PCAP bytes against borrowed
    /// anchor/ARP, ordered remote ports and exact selected HTTP expectations.
    /// Checks headers, checksums, sequence/ACK bounds, retransmission equality,
    /// complete request/response bytes and EOF-delimited config completion.
    /// Caller values grant no execution, parent, native or runner authority.
    /// Malformed, unsupported or incomplete traffic throws; inputs are not kept.</summary>
    public static WebPacketInspection InspectNativeAotWebPacketsForTests(byte[] bytes,uint anchor,QemuArpReceiveReceipt arp,
        IReadOnlyList<uint> ports,int forward,string body,string configuration,bool logQuota=false)
    {
        Need(ports.Count==5 && ports.Distinct().Count()==5 && ports.All(p=>p is >0 and <=65535) && forward is >0 and <=65535 &&
            body.Length>0 && Encoding.UTF8.GetByteCount(body)<=1024 && configuration.Length>0 &&
            Encoding.UTF8.GetByteCount(configuration)<=4096,"web_wire_expectations");
        _=QemuEthernetCapture.Inspect(bytes,anchor);
        int offset=24,prefixStage=0,prefixEnd=0,resolutionSent=0,tcpRecords=0;ulong timestamp=0;
        bool resolved=false;List<WebWireFlow> flows=[];
        while(offset<bytes.Length)
        {
            ulong now=(ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset))*1_000_000+
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset+4));
            Need(now>=timestamp,"web_wire_clock");timestamp=now;
            int size=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset+8)));offset+=16;
            var frame=bytes.AsSpan(offset,size);offset+=size;
            if(prefixStage<3)
            {
                if(prefixStage==0)
                {
                    if(frame.Length==60 && frame.Slice(14,9).SequenceEqual("CLROS-M07"u8) && OS.Std.Hash.Crc32.Compute(frame)==anchor)prefixStage=1;
                    continue;
                }
                Need(OS.Std.Hash.Crc32.Compute(frame)==(prefixStage==1?arp.RequestCrc32:arp.ReceiveCrc32) &&
                    frame.Length==(prefixStage==1?60:arp.ReceiveBytes),"web_wire_parent_arp");
                prefixStage++;prefixEnd=offset;continue;
            }
            Need(frame.Length>=14,"web_wire_ethernet");if(SocketIgnoredRouterAdvertisement(frame))continue;
            bool outgoing=MatchNetworkMac(frame[6..],0x020000000001),incoming=MatchNetworkMac(frame[6..],arp.PeerMac);
            Need(outgoing!=incoming && (outgoing?MatchNetworkMac(frame,arp.PeerMac)||frame[..6].IndexOfAnyExcept((byte)255)<0:
                MatchNetworkMac(frame,0x020000000001)||frame[..6].IndexOfAnyExcept((byte)255)<0),"web_wire_mac");
            ushort type=BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if(!resolved || type==0x0806)
            {
                Need(type==0x0806,"web_wire_resolution_type");CheckSocketArp(frame,outgoing,arp.PeerMac,out ushort operation);
                if(!resolved)
                {
                    if(outgoing)Need(operation==1 && ++resolutionSent==1,"web_wire_resolution_request");
                    else if(operation==2){Need(resolutionSent==1,"web_wire_resolution_order");resolved=true;}
                }
                continue;
            }
            if(!ReadSocketTcpFrame(frame,outgoing,arp.PeerMac,out var packet))continue;tcpRecords++;
            uint remote=packet.Remote,sequence=packet.Sequence,ack=packet.Ack;byte flags=packet.Flags;var payload=packet.Payload;
            var flow=flows.SingleOrDefault(f=>f.Port==remote);
            if(incoming && flags==2)
            {
                Need(payload.Length==0 && ack==0,"web_wire_syn");
                if(flow is null)
                {
                    Need(flows.Count<5 && remote==ports[flows.Count],"web_wire_original_tuple");
                    if(flows.Count!=0)
                    {
                        var previous=flows[^1];
                        // The raw reader waits for response EOF before the next
                        // probe. Remaining teardown ACKs may still overlap.
                        Need(previous.ServerFin && previous.ResponseExtent>0 && previous.ServerSeen==previous.ResponseExtent,
                            "web_wire_probe_order");
                    }
                    string path=flows.Count switch { 1=>"/config",2 or 3=>"/__clros/logexercise",_=>"/healthz" };
                    bool post=flows.Count is 2 or 3;
                    byte[] request=Encoding.ASCII.GetBytes($"{(post?"POST":"GET")} {path} HTTP/1.0\r\nHost: 127.0.0.1:{forward}\r\nConnection: close\r\nAccept: */*\r\n"+
                        (post?"Content-Length: 0\r\n":"")+"\r\n");
                    flow=new(remote,sequence,request);flows.Add(flow);
                }
                else Need(unchecked(sequence+1)==flow.Client && !flow.ClientFin && !flow.ServerFin,"web_wire_syn_retry");
                continue;
            }
            Need(flow is not null,"web_wire_unowned_tuple");
            if(outgoing && flags==0x12)
            {
                Need(payload.Length==0 && ack==flow!.Client && (!flow.SynAck || flow.Server==unchecked(sequence+1)),"web_wire_syn_ack");
                flow!.SynAck=true;flow.Server=unchecked(sequence+1);continue;
            }
            Need(flow!.SynAck && flags is 0x10 or 0x18 or 0x11 or 0x19,"web_wire_established");
            uint relative=unchecked(sequence-(outgoing?flow.Server:flow.Client));
            uint acknowledge=unchecked(ack-(outgoing?flow.Client:flow.Server));
            Need(acknowledge<=(uint)(outgoing?flow.ClientSeen:flow.ServerSeen)+(outgoing?flow.ClientFin?1U:0:flow.ServerFin?1U:0),"web_wire_acknowledgement");
            if(incoming)flow.ClientReady=true;else Need(flow.ClientReady,"web_wire_handshake_order");
            if(outgoing && flow.ClientFin && acknowledge==flow.Request.Length+1)flow.ClientFinAcked=true;
            if(incoming && flow.ServerFin && acknowledge==flow.ResponseExtent+1)flow.ServerFinAcked=true;
            byte[] data=outgoing?flow.Response:flow.Request;bool[] seen=outgoing?flow.ResponseSeen:flow.RequestSeen;
            int contiguous=outgoing?flow.ServerSeen:flow.ClientSeen;
            bool fin=outgoing?flow.ServerFin:flow.ClientFin;
            int extent=outgoing?flow.ResponseExtent:flow.Request.Length;
            Need((payload.Length==0?relative<=(uint)contiguous+(fin?1U:0):
                    relative<(uint)data.Length && (ulong)relative+(uint)payload.Length<=(uint)data.Length) &&
                (!fin || (ulong)relative+(uint)payload.Length<=(uint)extent+(payload.Length==0?1U:0)),"web_wire_sequence");
            for(int i=0;i<payload.Length;i++)
            {
                int at=(int)relative+i;
                Need(outgoing?!seen[at] || data[at]==payload[i]:data[at]==payload[i],"web_wire_literal_payload");
                if(outgoing)data[at]=payload[i];seen[at]=true;
            }
            if(outgoing && payload.Length!=0)flow.ResponseExtent=Math.Max(flow.ResponseExtent,checked((int)relative+payload.Length));
            while(contiguous<seen.Length && seen[contiguous])contiguous++;
            if(outgoing)flow.ServerSeen=contiguous;else flow.ClientSeen=contiguous;
            if((flags&1)!=0)
            {
                int end=outgoing?flow.ResponseExtent:flow.Request.Length;
                Need(end>0 && relative+payload.Length==end && contiguous==end,"web_wire_fin_sequence");
                if(outgoing){if(!flow.ServerFin)flow.ServerFinFirst=!flow.ClientFin;flow.ServerFin=true;}else flow.ClientFin=true;
            }
        }
        Need(prefixStage==3 && resolved && resolutionSent==1 && flows.Count==5,"web_wire_complete");
        _=QemuEthernetCapture.InspectArp(bytes.AsSpan(0,prefixEnd),arp);
        for(int i=0;i<flows.Count;i++)
        {
            var flow=flows[i];Need(flow.ClientReady && flow.ClientSeen==flow.Request.Length &&
                flow.ServerSeen==flow.ResponseExtent && flow.ResponseExtent>0 && (i==0 || flow.ServerFin),"web_wire_stream_complete");
            WebHttpResponse(flow.Response.AsSpan(0,flow.ResponseExtent),i switch { 1=>configuration,
                2=>logQuota?"":"CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9",3=>"",_=>body },i,logQuota);
        }
        return new(5,tcpRecords,flows.Sum(f=>f.Request.Length),flows.Sum(f=>f.ResponseExtent),flows.Count(f=>f.ClientFin),
            flows.Count(f=>f.ServerFin),flows.Count(f=>f.ClientFinAcked),flows.Count(f=>f.ServerFinAcked),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration))),
            Array.AsReadOnly(flows.Where(f=>f.ServerFinFirst && f.ClientFinAcked && f.ServerFinAcked).Select(f=>f.Port).ToArray()));
    }

    private static void WebHttpResponse(ReadOnlySpan<byte> bytes,string expected,int phase,bool logQuota)
    {
        int end=bytes.IndexOf("\r\n\r\n"u8);Need(end is >0 and <=2048,"web_wire_http_header");
        string[] lines=DecodeUtf8(bytes[..end].ToArray()).Split("\r\n",StringSplitOptions.None);
        bool quotaFailure=logQuota && phase==2;
        Need(lines[0]==(phase==3?"HTTP/1.1 409 Conflict":quotaFailure?"HTTP/1.1 500 Internal Server Error":"HTTP/1.1 200 OK"),"web_wire_http_status");Dictionary<string,string> headers=new(StringComparer.OrdinalIgnoreCase);
        foreach(string line in lines.Skip(1))
        {
            int colon=line.IndexOf(": ",StringComparison.Ordinal);
            Need(colon>0 && !Controls.IsMatch(line) && headers.TryAdd(line[..colon],line[(colon+2)..]),"web_wire_http_headers");
        }
        bool length=phase!=1,contentType=phase!=3 && !quotaFailure;
        Need(headers.Count==(phase==1 || !contentType?4:5) && headers.Keys.All(k=>k is "Connection" or "Date" or "Server" ||
                contentType && k=="Content-Type" || length && k=="Content-Length") &&
            headers.GetValueOrDefault("Connection")=="close" && headers.GetValueOrDefault("Server")=="Kestrel" &&
            headers.GetValueOrDefault("Content-Type")== (phase==1?"application/json; charset=utf-8":contentType?"text/plain":null) &&
            DateTimeOffset.TryParseExact(headers.GetValueOrDefault("Date"),"r",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out _),"web_wire_http_headers");
        byte[] body=Encoding.UTF8.GetBytes(expected);
        Need((!length || headers.GetValueOrDefault("Content-Length")==body.Length.ToString(CultureInfo.InvariantCulture)) &&
            bytes[(end+4)..].SequenceEqual(body),"web_wire_http_body");
    }
}
