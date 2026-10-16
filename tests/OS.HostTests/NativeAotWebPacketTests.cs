using System.Buffers.Binary;
using System.Text;
using OS.Tooling;

internal static class NativeAotWebPacketTests
{
    private const string Body="SELECTED",Config="{\"value\":\"SELECTED\"}";
    private static (List<byte[]> Frames,uint Anchor,QemuArpReceiveReceipt Arp) Vector(bool finalAck=false,bool clientHalfClose=false)
    {
        var original=NativeAotSocketIntegrationTests.Packets();var frames=original.Frames.Take(8).ToList();
        for(int i=0;i<2;i++)
        {
            ushort port=(ushort)(41000+i);uint client=(uint)(1000+i*10000),server=(uint)(2000+i*10000);
            void Add(bool outgoing,uint seq,uint ack,byte flags,ReadOnlySpan<byte> payload=default)=>
                frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing,port,seq,ack,flags,payload));
            Add(false,client,0,2);Add(true,server,client+1,0x12);client++;server++;Add(false,client,server,0x10);
            byte[] request=Encoding.ASCII.GetBytes($"GET {(i==0?"/healthz":"/config")} HTTP/1.0\r\nHost: 127.0.0.1:22080\r\nConnection: close\r\nAccept: */*\r\n\r\n");
            Add(false,client,server,0x18,request);client+=(uint)request.Length;Add(true,server,client,0x10);
            bool early=clientHalfClose && i==0;
            if(early){Add(false,client,server,0x11);client++;Add(true,server,client,0x10);}
            string content=i==0?Body:Config;
            byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"+(i==0?$"Content-Length: {content.Length}\r\n":"")+
                $"Connection: close\r\nContent-Type: {(i==0?"text/plain":"application/json; charset=utf-8")}\r\nDate: Thu, 01 Jan 1970 00:00:00 GMT\r\nServer: Kestrel\r\n\r\n{content}");
            Add(true,server,client,0x18,response.AsSpan(0,5));server+=5;
            Add(true,server,client,0x18,response.AsSpan(5));server+=(uint)response.Length-5;Add(false,client,server,0x10);
            Add(true,server,client,0x11);server++;Add(false,client,server,0x10);
            if(!early){Add(false,client,server,0x11);client++;}
            if(i==0 || finalAck)Add(true,server,client,0x10);
        }
        return(frames,original.Anchor,original.Arp);
    }
    private static QemuTranscripts.WebPacketInspection Inspect(List<byte[]> frames,uint anchor,QemuArpReceiveReceipt arp,
        string body=Body,int forward=22080)=>QemuTranscripts.InspectNativeAotWebPacketsForTests(
            NativeAotSocketIntegrationTests.Capture(frames),anchor,arp,[41000,41001],forward,body,Config);
    private static void Reject(Action action)
    {bool rejected=false;try{action();}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
    private static List<byte[]> Change(List<byte[]> frames,int index,Func<byte[],byte[]> change)
    {var result=frames.ToList();result[index]=change((byte[])result[index].Clone());return result;}

    [HostTest("web packets retain incomplete final close while proving exact selected HTTP streams")]
    private static void Positive()
    {
        var v=Vector();var r=Inspect(v.Frames,v.Anchor,v.Arp);
        HostCheck.True(r.Connections==2 && r.Received==159 && r.Sent>Body.Length+Config.Length &&
            r.ClientFins==2 && r.ServerFins==2 && r.ServerFinAcks==2 && r.ClientFinAcks==1);
        HostCheck.True(r.TimeWaitPorts.SequenceEqual(new uint[]{41000}));
        HostCheck.True(Inspect(Vector(clientHalfClose:true).Frames,v.Anchor,v.Arp).TimeWaitPorts.Count==0);
        HostCheck.True(Inspect(Vector(true).Frames,v.Anchor,v.Arp).ClientFinAcks==2);
        int at=v.Frames.FindIndex(f=>f.Length>100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34))==8080);
        var duplicate=v.Frames.ToList();duplicate.Insert(at+1,duplicate[at]);HostCheck.Equal(r.Sent,Inspect(duplicate,v.Anchor,v.Arp).Sent);
        var reordered=v.Frames.ToList();(reordered[at-1],reordered[at])=(reordered[at],reordered[at-1]);
        HostCheck.Equal(r.Sent,Inspect(reordered,v.Anchor,v.Arp).Sent);
    }

    [HostTest("web packets reject checksum foreign tuple ACK bounds gaps and conflicting retransmissions")]
    private static void Transport()
    {
        var v=Vector();int at=v.Frames.FindIndex(f=>f.Length>100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34))==8080);
        Reject(()=>Inspect(Change(v.Frames,at,f=>{f[^1]^=1;return f;}),v.Anchor,v.Arp));
        var gap=v.Frames.ToList();gap.RemoveAt(at-1);Reject(()=>Inspect(gap,v.Anchor,v.Arp));
        byte[] original=v.Frames[at];uint sequence=BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38));
        uint ack=BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(42));byte flags=original[47];
        byte[] payload=original[54..];payload[^1]^=1;
        var conflict=v.Frames.ToList();conflict.Insert(at+1,NativeAotSocketIntegrationTests.Tcp(true,41000,sequence,ack,flags,payload));
        Reject(()=>Inspect(conflict,v.Anchor,v.Arp));
        Reject(()=>Inspect(Change(v.Frames,at,_=>NativeAotSocketIntegrationTests.Tcp(true,41002,sequence,ack,flags,original.AsSpan(54))),v.Anchor,v.Arp));
        Reject(()=>Inspect(Change(v.Frames,at,_=>NativeAotSocketIntegrationTests.Tcp(true,41000,sequence,ack+100000,flags,original.AsSpan(54))),v.Anchor,v.Arp));
    }

    [HostTest("web packets reject wrong HTTP body host status headers and missing EOF completion")]
    private static void Http()
    {
        var v=Vector();Reject(()=>Inspect(v.Frames,v.Anchor,v.Arp,body:"OTHER"));Reject(()=>Inspect(v.Frames,v.Anchor,v.Arp,forward:22081));
        int at=v.Frames.FindIndex(f=>f.Length>100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34))==8080);
        byte[] original=v.Frames[at];uint sequence=BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38));
        uint ack=BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(42));
        foreach((string from,string to) in new[]{("200 OK","500 OK"),("Server: Kestrel","Server: Invalid"),("Content-Length: 8","Content-Length: 9")})
            Reject(()=>Inspect(Change(v.Frames,at,_=>NativeAotSocketIntegrationTests.Tcp(true,41000,sequence,ack,0x18,
                Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(original,54,original.Length-54).Replace(from,to,StringComparison.Ordinal)))),v.Anchor,v.Arp));
        var missing=v.Frames.Where(f=>f.Length<54 || BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34))!=8080 ||
            BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36))!=41001 || (f[47]&1)==0).ToList();
        Reject(()=>Inspect(missing,v.Anchor,v.Arp));
    }
}
