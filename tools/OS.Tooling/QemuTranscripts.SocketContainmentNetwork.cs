using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal static IReadOnlyList<string> SocketContainmentMarkers()
    {
        var markers=SocketMarkers(false).ToList();
        markers.InsertRange(markers.IndexOf("TEST_START m16-011")+1,["TEST_START m07.027","TEST_START m07.028"]);
        markers.AddRange(["CLROS_M07_028_READY","TEST_PASS m07.028","CLROS_M07_027_READY","TEST_PASS m07.027"]);
        return markers.AsReadOnly();
    }

    private static string NormalizeSocketContainmentWrites(string text)
    {
        byte[] raw=Encoding.UTF8.GetBytes(text);var leaf=SocketRawLeaf(raw);int start=raw.AsSpan().IndexOf(leaf);
        var capture=NativeAotSocketContainmentGuest.Read(leaf);
        return DecodeUtf8(raw.AsSpan(0,start).ToArray())+capture.Scheduling.Output.NormalizedTail+DecodeUtf8(raw.AsSpan(start+leaf.Length).ToArray());
    }

    private static (string[] Parent,SocketNetworkProof Proof) ValidateSocketContainmentNetworkTail(string[] lines,byte[] raw,
        byte[] package,NativeAotSocketContainmentProgram program)
    {
        var envelope=SocketNetworkEnvelope(lines,false,true);
        var tables=ReadRecord(lines[QemuContracts.M03OwnedSubsystemMarkers.Count+10],"M16_004_TABLES","root:dec","used:dec","leaves:dec","ranges:dec");
        const string restored=" restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        string readback=lines[QemuContracts.M03OwnedSubsystemMarkers.Count+11];Need(readback.EndsWith(restored,StringComparison.Ordinal),"socket_parent_readback");
        var kernel=ReadRecord(readback[..^restored.Length],"M16_004_READBACK","old:dec");
        ValidateNativeAotSocketContainmentTail(program.SourceRoot,package,program,SocketRawLeaf(raw),tables["root"],kernel["old"]);
        string[] parent=ValidateServiceLoopTail(lines[..envelope.ParentsEnd],true);
        parent=ValidateLinuxStartupTail(parent,package);parent=ValidateLinuxElfTail(parent,package);
        return ([..parent,..envelope.Apic.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count)],envelope.Proof);
    }

    /// <summary>Value-only containment wrapper control. Checks parent APIC/NIC,
    /// transport and cleanup associations; supplied rows grant no package,
    /// firmware, peer-process, packet or guest execution authority.</summary>
    public static SocketContainmentTransport InspectSocketContainmentNetworkForTests(string[] rows)
        =>SocketNetworkEnvelope(rows,false,true).Proof.Containment!;

    private static void AssertSocketContainmentCapture(string path,SocketNetworkProof proof,SocketContainmentPeerResult peer)
        =>_=JoinSocketContainmentCapture(File.ReadAllBytes(path),proof,peer);

    /// <summary>Value-only independent packet/parent/peer counter join for
    /// synthetic controls. Borrow inputs without retaining them; no execution,
    /// sealed program, source or physical guest authority is granted.</summary>
    public static SocketContainmentPacketInspection InspectSocketContainmentEvidenceForTests(string[] rows,byte[] capture,string peerResult)
        =>JoinSocketContainmentCapture(capture,SocketNetworkEnvelope(rows,false,true).Proof,ReadSocketContainmentPeerResult(peerResult));

    private static SocketContainmentPacketInspection JoinSocketContainmentCapture(byte[] capture,SocketNetworkProof proof,SocketContainmentPeerResult peer)
    {
        ulong[] anchor=PrefixRow(proof.Nic,"M07_NET D",4);Need(anchor[0]<=uint.MaxValue,"socket_capture_anchor_width");
        var control=ReadSocketContainmentPackets(capture,(uint)anchor[0],proof.Arp);var wire=control.Totals;
        // The device recycles the unsupported VLAN below the delivered-frame
        // counters. PCAP sees that extra incoming frame; the exact drop row and
        // available-ring publication account for it independently.
        Need(proof.Containment is not null && wire.PeerReceived==proof.Peer["received"] && wire.PumpReceived==proof.Wire["received"]+1 &&
            wire.PumpArp==proof.Wire["arp"] && wire.PumpIgnored==proof.Wire["ignored"]+1 &&
            wire.PumpReceived-wire.PumpArp-wire.PumpIgnored==proof.Wire["accepted"] &&
            wire.PumpArpCopied==proof.Wire["arpCopied"] && wire.PumpTcpCopied==proof.Wire["tcpCopied"] &&
            peer.HeadTransmissions==(ulong)control.HeadTransmissions && peer.HealthyReplies==30 && peer.VlanFrames==1 &&
            peer.ArpReplies==2+wire.PumpArp,"socket_containment_capture_serial_peer_join");
        return control;
    }

    private readonly record struct SocketContainmentPeerResult(ulong ArpReplies,ulong HeadTransmissions,ulong HealthyReplies,ulong VlanFrames);

    private static SocketContainmentPeerResult ReadSocketContainmentPeerResult(string line)
    {
        const string prefix="SOCKET_CONTAINMENT_PEER_RESULT complete=True stopped=True";
        Need(line.StartsWith(prefix+" ",StringComparison.Ordinal),"socket_containment_peer_cleanup");
        var row=ReadRecord("SOCKET_CONTAINMENT_PEER_RESULT"+line[prefix.Length..],"SOCKET_CONTAINMENT_PEER_RESULT",
            "arpReplies:dec","headTransmissions:dec","healthyReplies:dec","vlanFrames:dec");
        Need(row["arpReplies"] is >=2 and <=1024 && row["headTransmissions"] is >=6 and <=4096 && row["healthyReplies"]==30 && row["vlanFrames"]==1,
            "socket_containment_peer_result");
        return new(row["arpReplies"],row["headTransmissions"],row["healthyReplies"],row["vlanFrames"]);
    }

    internal static AuthorityFiles HoldSocketContainmentEvidence(string root,QemuSmokeOptions options)
    {
        AuthorityFiles files=new();
        try
        {
            var serial=files.Open(root,Path.GetFullPath(options.SerialLog,root),"containment serial","socket_containment_serial",8*1024*1024,singleLink:true);
            var summary=files.Open(root,Path.GetFullPath(options.SummaryLog,root),"containment summary","socket_containment_summary",16*1024*1024,singleLink:true);
            var capture=files.Open(root,Path.GetFullPath(options.SerialLog+".pcap",root),"containment PCAP","socket_containment_capture",4*1024*1024,singleLink:true);
            AuthorityFiles.RequireDistinct(serial,summary,capture);return files;
        }
        catch{files.Dispose();throw;}
    }
}
