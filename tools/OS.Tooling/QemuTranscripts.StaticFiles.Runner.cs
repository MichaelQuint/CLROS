using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied lifetime of the configured emulator/capture child. A
    /// wrapper ID is not the underlying emulator ID; original capture authority
    /// remains separately mandatory. No guest execution authority is granted.</summary>
    public readonly record struct StaticFileChildReport(int ProcessId,long LifetimeMilliseconds);

    /// <summary>Check twelve bounded canonical child rows against the actual
    /// caller invocation interval. Reject missing death, spontaneous exit,
    /// cleanup failure, implausible lifetime or an endpoint left open. Caller
    /// values alone supply no process, native, packet or guest authority.</summary>
    public static StaticFileChildReport InspectNativeAotStaticFileChildForTests(IReadOnlyList<string> rows,
        WebRunnerContext context,int timeoutSeconds)
    {
        Need(rows is {Count:StaticFileProcessEvidence.SummaryCount} && rows.All(row=>row is {Length:>0 and <=256} &&
            row.All(value=>value is >= ' ' and <= '~')),"static_child_rows");
        Need(context.ForwardPort is >0 and <=65535 && context.StartedUtc.Offset==TimeSpan.Zero &&
            context.CompletedUtc.Offset==TimeSpan.Zero && context.StartedUtc>DateTimeOffset.UnixEpoch &&
            context.CompletedUtc>=context.StartedUtc && timeoutSeconds is >=600 and <=825,"static_child_context");
        string Value(int index,string prefix)
        { Need(rows[index].StartsWith(prefix+" ",StringComparison.Ordinal),"static_child_field");return rows[index][(prefix.Length+1)..]; }
        ulong Number(int index,string prefix)
        {
            string value=Value(index,prefix);
            Need(ulong.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out ulong result) &&
                result.ToString(CultureInfo.InvariantCulture)==value,"static_child_number");return result;
        }
        DateTimeOffset Utc(int index,string prefix)
        {
            string value=Value(index,prefix);
            Need(DateTime.TryParseExact(value,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var date) &&
                date.Kind==DateTimeKind.Utc && date.ToString("O",CultureInfo.InvariantCulture)==value,"static_child_utc");return new(date);
        }
        ulong id=Number(0,"STATIC_CHILD_ID"),lifetime=Number(3,"STATIC_CHILD_LIFETIME_MS");
        var started=Utc(1,"STATIC_CHILD_START_UTC");var ended=Utc(2,"STATIC_CHILD_END_UTC");
        Need(id is >0 and <=int.MaxValue && ended>=started && started>=context.StartedUtc && ended<=context.CompletedUtc &&
            lifetime==(ulong)(long)(ended-started).TotalMilliseconds && lifetime>0 && lifetime<=(ulong)(timeoutSeconds+30)*1000,
            "static_child_lifetime");
        string[] expected=["STATIC_ENDPOINT_PRELAUNCH_AVAILABLE true","STATIC_ENDPOINT_STOPPED_REFUSED true",
            "STATIC_CHILD_TERMINATION_REQUESTED true","STATIC_CHILD_DEATH_CONFIRMED true","STATIC_CHILD_SPONTANEOUS false",
            "STATIC_CHILD_CLEANUP_FAILED false"];
        Need(rows.Skip(4).Take(6).SequenceEqual(expected) && Value(11,"STATIC_CHILD_FAILURE")=="none","static_child_completion");
        string exit=Value(10,"STATIC_CHILD_EXIT_CODE");
        Need(int.TryParse(exit,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int exitCode) &&
            exitCode.ToString(CultureInfo.InvariantCulture)==exit,"static_child_exit");
        return new((int)id,(long)lifetime);
    }

    private static string NormalizeStaticFileWrites(string text,bool diskStaticFull=false)
    {
        byte[] raw=Encoding.UTF8.GetBytes(text);var leaf=SocketRawLeaf(raw);int start=raw.AsSpan().IndexOf(leaf);
        var output=NativeAotThreadingOutput.ReadWeb(leaf,diskStaticFull);
        return DecodeUtf8(raw.AsSpan(0,start).ToArray())+output.NormalizedTail+DecodeUtf8(raw.AsSpan(start+leaf.Length).ToArray());
    }

    internal static AuthorityFiles HoldStaticFileEvidence(string root,QemuSmokeOptions options,NativeAotStagedStaticPeer.Completion peer)
    {
        AuthorityFiles files=new();
        try
        {
            List<AuthorityFile> all=[];
            void Open(string path,int maximum,bool empty=false)=>all.Add(files.Open(root,Path.GetFullPath(path,root),
                "static original evidence","static_evidence",maximum,allowEmpty:empty,singleLink:true));
            Open(options.SerialLog,8*1024*1024);Open(options.SummaryLog,16*1024*1024);Open(options.SerialLog+".pcap",4*1024*1024);
            Need(Path.GetFullPath(peer.Directory)==Path.GetFullPath(options.SerialLog+".static",root),"static_peer_directory");
            Open(Path.Combine(peer.Directory,"http.json"),256*1024);
            Need(all[^1].Sha256.Equals(peer.ReceiptSha256,StringComparison.OrdinalIgnoreCase),"static_peer_completion_receipt");
            Open(Path.Combine(peer.Directory,"abort.json"),4096);Open(Path.Combine(peer.Directory,"stopped.json"),4096);
            for(int i=0;i<26;i++)Open(Path.Combine(peer.Directory,i.ToString("D3",CultureInfo.InvariantCulture)+".body"),65536,true);
            AuthorityFiles.RequireDistinct(all.ToArray());return files;
        }
        catch{files.Dispose();throw;}
    }

    /// <summary>Join copied scheduler send/receive counts to unique captured
    /// streams in original accept/SYN order. The serialized accept owner uses
    /// transport FIFO order. Only a reset flow permits queued but unsent bytes,
    /// bounded by its original 32-KiB transmit queue. Inputs supply no authority.</summary>
    public static void InspectNativeAotStaticFileIoJoinForTests(IReadOnlyList<StaticFileSocketIo> calls,
        IReadOnlyList<StaticFileHttpFlow> flows)
    {
        Need(calls is {Count:3} && flows is {Count:3} && flows.Select(flow=>flow.RemotePort).Distinct().Count()==3 &&
            flows.Count(flow=>flow.Reset)==1,"static_io_inventory");
        for(int i=0;i<3;i++)
        {
            var call=calls[i];var flow=flows[i];
            Need(call.Connection==i && flow.RemotePort is >0 and <=65535 && flow.RequestBytes>0 && flow.ResponseBytes>0 &&
                call.Received==(ulong)flow.RequestBytes && call.Reset is >=0 and <=1 &&
                (flow.Reset?call.Sent>=(ulong)flow.ResponseBytes && call.Sent-(ulong)flow.ResponseBytes<=32768:
                    call.Sent==(ulong)flow.ResponseBytes && call.Reset==0),"static_io_stream_join");
        }
    }

    internal static int InspectStaticFileRun(string root,QemuSmokeOptions options,QemuSmokePlan plan,ProcessResult runner,
        TextWriter output,NativeAotStaticFileProgram program,NativeAotStagedStaticPeer.Completion peer,WebRunnerContext context)
    {
        RequireCompleteProcess(runner,"static_runner_incomplete");
        Need(runner.Succeeded && runner.StandardError.Length==0 && ReferenceEquals(runner,peer.Runner) && peer.Port==context.ForwardPort &&
            program.Staged && plan.IsEnabled("EnableM10017Probe") && options.LinuxThreadingApplication==NativeAotStaticFilePublisher.Application &&
            options.WorkloadProfile=="SingleMinimalApi" && options.FirmwareRngSource=="virtio" &&
            string.Equals(Path.GetFullPath(root),program.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
            "static_prepared_owner");
        byte[] raw=File.ReadAllBytes(Path.GetFullPath(options.SerialLog,root));
        byte[] package=File.ReadAllBytes(Path.GetFullPath(options.LinuxElfPackage,root));
        var packageView=NativeAotStaticFileProgram.RequirePackage(package,true);
        byte[] summary=File.ReadAllBytes(Path.GetFullPath(options.SummaryLog,root));
        Need(raw.Length is >FirmwarePrefixBytes and <=8*1024*1024 && summary.Length is >3 and <=16*1024*1024 &&
            summary.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()),"static_runner_extent");
        string[] child=DecodeUtf8(summary[3..]).Split("\r\n",StringSplitOptions.RemoveEmptyEntries).TakeLast(StaticFileProcessEvidence.SummaryCount).ToArray();
        var process=InspectNativeAotStaticFileChildForTests(child,context,options.TimeoutSeconds);
        Need(runner.Duration.TotalMilliseconds>=process.LifetimeMilliseconds && runner.Duration.TotalSeconds<=options.TimeoutSeconds+60,
            "static_runner_lifetime");
        string[] stdout=runner.StandardOutput.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n');
        if(stdout[^1]=="")stdout=stdout[..^1];
        Need(stdout.Length==19 && stdout[0]==$"QEMU_DISCOVERY result={context.Qemu} checked={context.Qemu}" &&
            stdout[1]==$"OVMF_DISCOVERY result={context.Ovmf} checked={context.Ovmf}","static_runner_discovery");
        AssertStandardRunnerResult(root,options,plan,stdout,staticProgram:program,staticForwardPort:peer.Port,staticChildRows:child);
        int at=raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8);Need(at>FirmwarePrefixBytes,"static_parent_start");
        var parent=InspectNativeAotWebParentForTests(package,raw.AsSpan(0,at));
        string text=NormalizeStaticFileWrites(DecodeUtf8(raw[FirmwarePrefixBytes..]));
        Need(text.EndsWith("\r\n",StringComparison.Ordinal),"static_capture_end");
        string[] rows=text[..^2].Split("\r\n",StringSplitOptions.None);
        Need(rows.All(row=>row.Length>0 && !Controls.IsMatch(row)),"static_capture_rows");
        var envelope=SocketNetworkEnvelope(rows,true,staticFiles:true).Proof;
        var replay=ReplayNativeAotStaticFileTail(package.AsSpan((int)packageView.PayloadOffset,(int)packageView.PayloadBytes).ToArray(),
            SocketRawLeaf(raw),program.IsInstruction,program.IsSyscallReturn,parent.UserRoot,parent.KernelRoot);
        Need(replay.ReadyNanoseconds>=parent.PeerEnd && replay.ExitedNanoseconds<=envelope.End["end"] &&
            replay.FileObservations>0 && replay.PathObservations>0 && replay.Signal15Returns==1,"static_parent_leaf_join");
        foreach(string row in rows.Where(row=>row.StartsWith("M16_008_TIMING ",StringComparison.Ordinal) || row.StartsWith("M16_008_SYSCALL_TIME ",StringComparison.Ordinal)))
            Need(row.EndsWith(" periodFs="+parent.Period.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal),"static_parent_timing_period");
        var bound=ReadRecord(rows.Single(row=>row.StartsWith("M16_008_BOUND ",StringComparison.Ordinal))
            [..^" timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack".Length],"M16_008_BOUND",
            "root:dec","pool:dec","workers:dec","code:dec","metadata:dec","userImage:dec");
        DistinctExtents([(parent.UserRoot,4096),(parent.KernelRoot,4096),(parent.Dma,32768),(bound["pool"],WebThreadPoolPages*4096UL)],"static_parent_live_extents");
        byte[] packets=File.ReadAllBytes(Path.GetFullPath(options.SerialLog+".pcap",root));
        var bodies=Enumerable.Range(0,26).ToDictionary(i=>i.ToString("D3",CultureInfo.InvariantCulture)+".body",
            i=>File.ReadAllBytes(Path.Combine(peer.Directory,i.ToString("D3",CultureInfo.InvariantCulture)+".body")));
        byte[] receipt=File.ReadAllBytes(Path.Combine(peer.Directory,"http.json"));
        Need(Convert.ToHexString(SHA256.HashData(receipt))==peer.ReceiptSha256,"static_completion_receipt");
        var http=InspectNativeAotStaticFileHttpPackets(packets,parent.FrameAnchor,parent.Arp,
            envelope.Lifetimes.Select(value=>value.RemotePort).ToArray(),receipt,bodies,File.ReadAllBytes(Path.Combine(peer.Directory,"abort.json")));
        using(var stopped=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(peer.Directory,"stopped.json"))))
        {
            var value=stopped.RootElement;StaticObject(value,"Port","EndpointRefused","RunnerExit");
            Need(value.GetProperty("Port").GetInt32()==peer.Port && value.GetProperty("EndpointRefused").GetBoolean() &&
                value.GetProperty("RunnerExit").GetInt32()==0,"static_endpoint_stopped");
        }
        InspectNativeAotStaticFileIoJoinForTests(replay.Connections,http.Flows);
        var wire=envelope.Wire;var end=envelope.End;
        _=ReadServiceWireTotals(packets,parent,new(wire["received"],wire["arp"],wire["accepted"],wire["ignored"],wire["capacity"],
            wire["arpCopied"],wire["tcpCopied"],end["polls"],end["fresh"],end["retired"],end["start"],end["end"],end["maxDeadline"]),raw,out _);
        Need((ulong)http.TcpRecords==wire["accepted"]+wire["tcpCopied"],"static_packet_tcp_join");
        ValidateUnselectedRecords(text,plan);
        output.WriteLine($"STATIC_COMPLETE_REPLAY calls={replay.Protocol.Calls} fileObservations={replay.FileObservations} pathObservations={replay.PathObservations} signalReturns={replay.Signal15Returns} acceptance=pending");
        output.WriteLine($"STATIC_COMPLETE_HTTP connections={http.Connections} responses={http.Responses.Count} tcpRecords={http.TcpRecords} requestBytes={http.RequestBytes} responseBytes={http.ResponseBytes} acceptance=pending");
        output.WriteLine($"STATIC_COMPLETE_CHILD id={process.ProcessId} lifetimeMs={process.LifetimeMilliseconds} role=configured-emulator-or-capture-child acceptance=pending");
        return 0;
    }
}
