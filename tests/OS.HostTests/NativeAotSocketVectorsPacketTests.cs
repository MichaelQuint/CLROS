using OS.Tooling;
using Wire=NativeAotSocketIntegrationTests;

internal static class NativeAotSocketVectorsPacketTests
{
    [HostTest("vector packet proof requires all four binary streams reset and acknowledged shutdown lifetimes")]
    private static void Complete()
    {
        var fixture=Wire.Packets(vectors:true);
        var report=QemuTranscripts.InspectSocketVectorPacketsForTests(Wire.Capture(fixture.Frames),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]);
        HostCheck.True(report.Connections==4&&report.Received==256&&report.Sent==524352&&report.Resets==1&&report.ClientFins==3&&report.ServerFins==3);
        int payload=fixture.Frames.FindIndex(x=>x.Length>1000);var duplicate=fixture.Frames.ToList();duplicate.Insert(payload+1,duplicate[payload]);
        HostCheck.Equal(524352,QemuTranscripts.InspectSocketVectorPacketsForTests(Wire.Capture(duplicate),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]).Sent);
        var missing=fixture.Frames.ToList();missing.RemoveAt(missing.FindLastIndex(x=>x.Length>=54&&x[12]==8&&x[13]==0&&x[23]==6));
        Reject(missing,"socket_wire_complete");
        var corrupt=fixture.Frames.Select(x=>(byte[])x.Clone()).ToList();corrupt[payload][54]^=1;
        Reject(corrupt,"socket_wire_tcp_checksum");
        // Recompute checksums around a wrong request to exercise literal validation.
        var changed=fixture.Frames.ToList();byte[] request=new byte[64];for(int i=0;i<64;i++)request[i]=(byte)(i*11%251);request[19]^=1;
        int at=changed.FindIndex(x=>x.Length==118);changed[at]=Wire.Tcp(false,40000,1001,9001,0x18,request);
        Reject(changed,"socket_wire_literal_payload");
        void Reject(List<byte[]> frames,string reason)
        {NativeAotSocketVectorsPacketTests.Reject(()=>QemuTranscripts.InspectSocketVectorPacketsForTests(Wire.Capture(frames),fixture.Anchor,fixture.Arp,[40000,40001,40002,40003]),reason);}
    }

    [HostTest("vector wire proof cannot substitute the original socket payload contract or tuple order")]
    private static void Separate()
    {
        var vectors=Wire.Packets(true);var original=Wire.Packets();
        Reject(()=>QemuTranscripts.InspectSocketPacketsForTests(Wire.Capture(vectors.Frames),vectors.Anchor,vectors.Arp,[40000,40001,40002,40003]),"socket_wire_payload_extent");
        Reject(()=>QemuTranscripts.InspectSocketVectorPacketsForTests(Wire.Capture(original.Frames),original.Anchor,original.Arp,[40000,40001,40002,40003]),"socket_wire_literal_payload");
        Reject(()=>QemuTranscripts.InspectSocketVectorPacketsForTests(Wire.Capture(vectors.Frames),vectors.Anchor,vectors.Arp,[40001,40000,40002,40003]),"socket_wire_original_tuple");
    }

    private static void Reject(Action action,string reason)
    {
        string? observed=null;try{action();}catch(ToolFailure error){observed=error.Reason;}
        HostCheck.Equal(reason,observed??"accepted");
    }
}
