using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal sealed record DiskStaticOriginalInspection(DiskStaticReplayReport Replay,DiskStaticSectorValues Sectors,
        DiskStaticHttpInspection Http);

    // This entry is reachable only from the original QemuSmoke runner owner.
    // All generated app/image/native/source inputs were held before launch;
    // captures and peer outputs remain held through both final callbacks.
    internal static DiskStaticOriginalInspection InspectDiskStaticRun(string root,QemuSmokeOptions options,QemuSmokePlan plan,
        ProcessResult runner,NativeAotStaticFileProgram program,NativeAotDiskStaticPeer.Completion peer,
        AuthorityFile image,AuthorityFile efi,AuthorityFile package,AuthorityFile imageReceipt,
        WebRootImageFixture.Receipt receipt,WebRunnerContext context,Action verifyOriginalInputs)
    {
        RequireCompleteProcess(runner,"disk_static_runner_incomplete");
        Need(runner.Succeeded && runner.StandardError.Length==0 && ReferenceEquals(runner,peer.Runner) && peer.Port==context.ForwardPort &&
            NativeAotDiskSelection.StaticSelected(plan.Flags) && !program.Staged && plan.BuildEfi && !options.DryRun && !options.SkipImageBuild &&
            options.WorkloadProfile=="SingleMinimalApi" && options.FirmwareRngSource=="virtio" && options.LinuxElfImageVariant=="Intact" &&
            options.WebRootFixture is "DiskA" or "DiskB" && options.LinuxThreadingApplication==NativeAotStaticFilePublisher.Application &&
            string.Equals(Path.GetFullPath(root),program.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
            "disk_static_original_selection");
        using AuthorityFiles captured=new();
        AuthorityFile Hold(string path,int maximum,bool empty=false)=>captured.Open(root,Path.GetFullPath(path,root),
            "original disk static evidence","disk_static_capture",maximum,allowEmpty:empty,singleLink:true);
        Need(Path.GetFullPath(peer.Directory)==Path.GetFullPath(options.SerialLog+".disk-http",root),"disk_static_peer_directory");
        var serial=Hold(options.SerialLog,8*1024*1024);var summary=Hold(options.SummaryLog,16*1024*1024);
        var pcap=Hold(options.SerialLog+".pcap",4*1024*1024);var http=Hold(Path.Combine(peer.Directory,"http.json"),65536);
        var stopped=Hold(Path.Combine(peer.Directory,"stopped.json"),4096);
        var bodyFiles=Enumerable.Range(0,5).Select(i=>Hold(Path.Combine(peer.Directory,i.ToString("D3",CultureInfo.InvariantCulture)+".body"),65536,true)).ToArray();
        AuthorityFiles.RequireDistinct([image,efi,package,imageReceipt,serial,summary,pcap,http,stopped,..bodyFiles]);
        Need(http.Sha256==peer.ReceiptSha256 && imageReceipt.ReadUtf8(16*1024*1024,"disk_static_image_receipt")==JsonSerializer.Serialize(receipt),
            "disk_static_receipt_identity");
        byte[] raw=serial.ReadBytes(8*1024*1024,"disk_static_serial"),packageBytes=package.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes,"disk_static_package"),
            imageBytes=image.ReadBytes(WebRootImageFixture.MaximumImageBytes,"disk_static_image");
        var packageView=NativeAotStaticFileProgram.RequirePackage(packageBytes,false);
        _=LinuxElfImageFixture.Inspect(imageBytes,efi.ReadBytes(16*1024*1024,"disk_static_efi"),packageBytes);
        var inventory=WebRootDiskInventory.Inspect(image.Path,imageBytes,receipt);
        byte[] summaryBytes=summary.ReadBytes(16*1024*1024,"disk_static_summary");
        Need(summaryBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()),"disk_static_summary_bom");
        string[] child=DecodeUtf8(summaryBytes[3..]).Split("\r\n",StringSplitOptions.RemoveEmptyEntries).TakeLast(StaticFileProcessEvidence.SummaryCount).ToArray();
        var process=InspectNativeAotStaticFileChildForTests(child,context,options.TimeoutSeconds);
        Need(runner.Duration.TotalMilliseconds>=process.LifetimeMilliseconds && runner.Duration.TotalSeconds<=options.TimeoutSeconds+60,
            "disk_static_runner_lifetime");
        string[] stdout=runner.StandardOutput.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n');if(stdout[^1]=="")stdout=stdout[..^1];
        Need(stdout.Length==19 && stdout[0]==$"QEMU_DISCOVERY result={context.Qemu} checked={context.Qemu}" &&
            stdout[1]==$"OVMF_DISCOVERY result={context.Ovmf} checked={context.Ovmf}","disk_static_runner_discovery");
        AssertStandardRunnerResult(root,options,plan,stdout,staticProgram:program,staticForwardPort:peer.Port,staticChildRows:child);
        int at=raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8);Need(at>FirmwarePrefixBytes,"disk_static_parent_start");
        var parent=InspectNativeAotWebParentForTests(packageBytes,raw.AsSpan(0,at));
        string text=NormalizeStaticFileWrites(DecodeUtf8(raw[FirmwarePrefixBytes..]));
        Need(text.EndsWith("\r\n",StringComparison.Ordinal),"disk_static_serial_terminal");
        string[] rows=text[..^2].Split("\r\n",StringSplitOptions.None);
        Need(rows.All(r=>r.Length>0 && !Controls.IsMatch(r)),"disk_static_serial_rows");
        var replay=InspectNativeAotDiskStaticProtocolForTests(packageBytes.AsSpan((int)packageView.PayloadOffset,(int)packageView.PayloadBytes).ToArray(),
            SocketRawLeaf(raw),program.IsInstruction,program.IsSyscallReturn,parent.UserRoot,parent.KernelRoot,inventory);
        Need(replay.Static.Connections.Count==1,"disk_static_development_connections");
        var envelope=SocketNetworkEnvelope(rows,true,staticFiles:true,diskFiles:true,diskStatic:true,diskStaticConnections:1).Proof;
        Need(replay.Static.ReadyNanoseconds>=parent.PeerEnd && replay.Static.ExitedNanoseconds<=envelope.End["end"] &&
            replay.Static.FileObservations>0 && replay.Static.PathObservations>0 && replay.Static.Signal15Returns==1,"disk_static_parent_leaf");
        foreach(string row in rows.Where(r=>r.StartsWith("M16_008_TIMING ",StringComparison.Ordinal) || r.StartsWith("M16_008_SYSCALL_TIME ",StringComparison.Ordinal)))
            Need(row.EndsWith(" periodFs="+parent.Period.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal),"disk_static_timing_period");
        var bound=ReadRecord(rows.Single(r=>r.StartsWith("M16_008_BOUND ",StringComparison.Ordinal))
            [..^" timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack".Length],"M16_008_BOUND",
            "root:dec","pool:dec","workers:dec","code:dec","metadata:dec","userImage:dec");
        var disk=ReadRecord(rows.Single(r=>r.StartsWith("M16_023_BOUND ",StringComparison.Ordinal))
            [..^" pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained".Length],"M16_023_BOUND",
            "owner:dec","epoch:dec","arena:dec","pages:dec","bar:dec","capacity:dec","bootstrapCompleted:dec","bootstrapCopied:dec","clock:dec");
        Need(disk["owner"]==replay.Owner && disk["epoch"]==replay.Epoch && disk["capacity"]==(ulong)imageBytes.Length/512,
            "disk_static_image_device_geometry");
        var end=ReadDiskFileEnd(rows.Single(r=>r.StartsWith("M16_025_END ",StringComparison.Ordinal)));
        Need(end["bootstrapCompleted"]==disk["bootstrapCompleted"] && end["bootstrapCopied"]==disk["bootstrapCopied"],"disk_static_bootstrap_join");
        var sectors=InspectNativeAotDiskStaticSectorValues(replay,inventory,imageBytes,new(replay.Owner,replay.Epoch,
            replay.BootstrapCompleted,replay.BootstrapCopied,end["submitted"],end["completed"],end["copied"]));
        InspectDiskStaticLiveBoundaryValues(parent,bound["pool"],PrefixRow(rows,"M07_ARP 2",4)[0],PrefixRow(rows,"M07_ARP 0",4)[3],
            disk["arena"],disk["pages"],disk["bar"],disk["owner"],disk["clock"],envelope.End["end"],sectors.Device.Rows);
        var budget=ReadRecord(rows.Single(r=>r.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal)),"M16_008_BUDGET",
            "started:dec","epochs:dec","steps:dec","nanos:dec");
        var budgetEnd=ReadRecord(rows.Single(r=>r.StartsWith("M16_008_BUDGET_END ",StringComparison.Ordinal)),"M16_008_BUDGET_END","nanos:dec");
        Need(budget["started"]>=disk["clock"] && budgetEnd["nanos"]<=envelope.End["end"],"disk_static_budget_lifetime");
        byte[] packetBytes=pcap.ReadBytes(4*1024*1024,"disk_static_packets");
        var bodyBytes=bodyFiles.ToDictionary(b=>Path.GetFileName(b.Path),b=>b.ReadBytes(65536,"disk_static_body"));
        var wire=InspectNativeAotDiskStaticHttpValues(packetBytes,parent.FrameAnchor,parent.Arp,
            envelope.Lifetimes.Select(l=>l.RemotePort).ToArray(),http.ReadBytes(65536,"disk_static_http"),bodyBytes,inventory,replay);
        using(var stoppedDocument=JsonDocument.Parse(stopped.ReadBytes(4096,"disk_static_stopped")))
        {
            var value=stoppedDocument.RootElement;StaticObject(value,"Port","EndpointRefused","RunnerExit");
            Need(value.GetProperty("Port").GetInt32()==peer.Port && value.GetProperty("EndpointRefused").GetBoolean() &&
                value.GetProperty("RunnerExit").GetInt32()==0,"disk_static_endpoint_stopped");
        }
        var w=envelope.Wire;var e=envelope.End;
        _=ReadServiceWireTotals(packetBytes,parent,new(w["received"],w["arp"],w["accepted"],w["ignored"],w["capacity"],w["arpCopied"],w["tcpCopied"],
            e["polls"],e["fresh"],e["retired"],e["start"],e["end"],e["maxDeadline"]),raw,out _);
        Need((ulong)wire.TcpRecords==w["accepted"]+w["tcpCopied"],"disk_static_wire_totals");
        ValidateUnselectedRecords(text,plan);
        captured.VerifyUnchanged();verifyOriginalInputs();captured.VerifyUnchanged();
        return new(replay,sectors,wire);
    }

    /// <summary>Synchronously borrow copied live addresses and IO clocks. Reject
    /// overlapping root, worker, NIC and disk extents, an unmeasured disk layout
    /// or IO outside the parent/device lifetime. This grants no native, process
    /// or original execution authority; the original owner supplies those joins.</summary>
    public static unsafe void InspectDiskStaticLiveBoundaryValues(WebParentReport parent,ulong pool,ulong priorNicDma,ulong nicBar,
        ulong arena,ulong pages,ulong diskBar,ulong owner,ulong boundClock,ulong endClock,
        IReadOnlyList<NativeDiskReadObservations.Observation> io)
    {
        Need(pages==90 && owner>=4096 && owner<=0x100000000UL-(ulong)sizeof(Block.State),"disk_static_measured_layout");
        DistinctExtents([(parent.UserRoot,4096),(parent.KernelRoot,4096),(parent.Dma,32768),(pool,WebThreadPoolPages*4096UL),
            (priorNicDma,8192),(nicBar,16384),(arena,pages*4096),(diskBar,16384),(owner,(ulong)sizeof(Block.State))],"disk_static_live_extents");
        Need(boundClock>=parent.PeerEnd && endClock>=boundClock && io.Count>0 &&
            io.All(row=>row.Clock>=boundClock && row.Clock<=endClock),"disk_static_io_lifetime");
    }
}
