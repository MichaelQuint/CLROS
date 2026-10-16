using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal static IReadOnlyList<string> WebMarkers() => [
        ..SocketMarkers(true).TakeWhile(x => x != "TEST_START m16-011"),
        "TEST_START m10.004",..M16LinuxSignalMarkers.Take(3),"M16_011_BOUND ","M16_005_ARMED","M16_008_BOUND","M16_008_BUDGET",
        "M10_004_ENTER","M10_004_CONFIG","M10_004_LISTEN","M10_004_LIVE","CLROS_M10_004_READY","TEST_PASS m10.004"];

    // This consumer checks borrowed observations only. QemuSmoke owns current
    // native/source/image admission, the actual runner and the final success.
    internal static int InspectWebRun(string root,QemuSmokeOptions options,QemuSmokePlan plan,ProcessResult runner,
        TextWriter output,NativeAotWebProgram program,WebRunnerContext context)
    {
        RequireCompleteProcess(runner,"web_runner_incomplete");
        if(runner.ExitCode!=0)return runner.ExitCode;
        Need(plan.IsEnabled("EnableM10004Probe") && program is not null &&
            string.Equals(Path.GetFullPath(root),program.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
            "web_prepared_owner");
        byte[] raw=File.ReadAllBytes(Path.GetFullPath(options.SerialLog,root));
        byte[] package=File.ReadAllBytes(Path.GetFullPath(options.LinuxElfPackage,root));
        var capture=ValidateNativeAotWebCapturePrefix(root,package,program!,raw);
        var replay=capture.Leaf;
        var packets=ValidateWebPackets(package,raw,File.ReadAllBytes(Path.GetFullPath(options.SerialLog+".pcap",root)),capture.Parent,replay,context.ForwardPort);
        var configuration=NativeAotWebProgram.DescribeConfiguration(package);
        var runnerReport=InspectNativeAotWebRunnerForTests(options,plan,runner,raw,
            File.ReadAllBytes(Path.GetFullPath(options.SummaryLog,root)),context,configuration.Body,configuration.Json);
        var observed=replay.Observations;
        output.WriteLine($"WEB_PREFIX_PARENT records={capture.Parent.Records} userRoot={capture.Parent.UserRoot} kernelRoot={capture.Parent.KernelRoot} periodFs={capture.Parent.Period} dma={capture.Parent.Dma} acceptance=pending");
        output.WriteLine($"WEB_PREFIX_OBSERVATIONS calls={observed.Calls} writes={observed.Writes} stdout={observed.StdoutBytes} stderr={observed.StderrBytes} pendingEpoll={observed.PendingEpollWaits} fragmentBytes={observed.TrailingFragment.Length} acceptance=pending");
        output.WriteLine($"WEB_PREFIX_REPLAY clones={replay.Protocol.Clones} irqs={replay.Protocol.Irqs} drains={replay.Protocol.Drains} transfers={replay.Protocol.Transfers} active={replay.Active} ready={replay.Ready} selected={replay.Current} returning={replay.ReturningToSupervisor} entropyRemaining={replay.EntropyRemaining} accepts={replay.SocketAccepts} sent={replay.Sent} received={replay.Received} acceptance=pending");
        output.WriteLine($"WEB_PREFIX_PACKETS connections={packets.Connections} tcpRecords={packets.TcpRecords} received={packets.Received} sent={packets.Sent} clientFins={packets.ClientFins} serverFins={packets.ServerFins} clientFinAcks={packets.ClientFinAcks} serverFinAcks={packets.ServerFinAcks} acceptance=pending");
        output.WriteLine($"WEB_PREFIX_RUNNER pid={runnerReport.ProcessId} lifetimeMs={runnerReport.LifetimeMilliseconds} healthMs={runnerReport.HealthMilliseconds} configMs={runnerReport.ConfigurationMilliseconds} acceptance=pending");
        // A continuing prefix supplies no guest exit/reap or resource release.
        // Never send it to the finite parser or manufacture completion records.
        // The caller must still recheck every held current input before success.
        return 0;
    }

    /// <summary>Value-only observations of a complete web-leaf prefix and its
    /// final incomplete kernel row. Hashes retain the original raw bytes; the
    /// fragment is never replayed as a completed syscall or invented cleanup.
    /// Call shapes/ordinals/returns, readiness bindings, write lengths/channels
    /// and M10 startup observations are checked. This is not full syscall,
    /// scheduler, packet, native, source, runner or guest acceptance.</summary>
    public readonly record struct WebObservationReport(int Calls,int Writes,int StdoutBytes,int StderrBytes,
        int PendingEpollWaits,string TrailingFragment,string RawSha256,string CompletePrefixSha256);

    /// <summary>Inspect borrowed synthetic raw leaf bytes with explicit expected
    /// startup counts. Supplied text grants no sealed instruction or execution
    /// authority. Malformed framing, observations or incomplete-row shape throw;
    /// no input is retained or mutated.</summary>
    public static WebObservationReport InspectNativeAotWebObservationsForTests(ReadOnlySpan<byte> rawTail,
        uint arguments,uint environment,uint settingsBytes)=>ReadWebObservations(rawTail,arguments,environment,settingsBytes,null,null);

    /// <summary>Join web prefix observations to an exact original package and
    /// its sealed prepared instruction lookup. Every completed syscall/return
    /// and user IRQ binds to original instruction starts. Inputs are synchronous
    /// borrows; stale/mismatched package, owner or observations throw. This leaf
    /// still grants no full syscall/scheduler/native/packet/runner acceptance.</summary>
    public static WebObservationReport ValidateNativeAotWebPrefix(string root,byte[] package,NativeAotWebProgram program,ReadOnlySpan<byte> rawTail)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root),program.SourceRoot,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),"web_prepared_owner");
        var configuration=NativeAotWebProgram.DescribeConfiguration(package);
        return ReadWebObservations(rawTail,configuration.Arguments,configuration.Environment,configuration.SettingsBytes,
            program!.IsSyscallReturn,program!.IsInstruction);
    }

    /// <summary>Replayed complete prefix and retained in-flight owner state.
    /// Transfers count only completed supervisor observations. An unfinished
    /// syscall/IRQ/return is never counted as completed. Parent/native/packet/
    /// runner/source qualification remains outside this leaf report.</summary>
    public readonly record struct WebReplayReport(WebObservationReport Observations,ThreadingProtocolReport Protocol,
        int Active,int Ready,ulong Current,string Boundary,bool ReturningToSupervisor,uint EntropyRemaining,int SocketAccepts,ulong Sent,ulong Received);

    /// <summary>Replay original web memory, syscall, FIFO/thread, signal, wait,
    /// descriptor and socket observations through the complete captured prefix.
    /// Exact original package/prepared lookup and caller-supplied parent roots
    /// are borrowed, not executed. Unsupported or inconsistent observations
    /// throw. A live owner at host capture end supplies no guest cleanup or
    /// complete QEMU/native/packet/runner/source acceptance.</summary>
    public static WebReplayReport ValidateNativeAotWebContinuingPrefix(string root,byte[] package,NativeAotWebProgram program,
        ReadOnlySpan<byte> rawTail,ulong userRoot,ulong kernelRoot)
    {
        var observed=ValidateNativeAotWebPrefix(root,package,program,rawTail);
        var configuration=NativeAotWebProgram.DescribeConfiguration(package);
        var framed=NativeAotThreadingOutput.ReadWeb(rawTail);
        string complete=framed.NormalizedTail[..(framed.NormalizedTail.Length-observed.TrailingFragment.Length)];
        string[] original=complete[..^2].Split("\r\n",StringSplitOptions.None);
        const string suffix=" source=immutable-package copied=user";
        var config=ReadRecord(original.Single(r=>r.StartsWith("M10_004_CONFIG ",StringComparison.Ordinal))[..^suffix.Length],"M10_004_CONFIG","fd:dec","bytes:dec");
        var view=OS.Manifest.LinuxElfPackage.Validate(package);
        return ReplayWeb(framed,observed,package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes).ToArray(),
            program.IsInstruction,program.IsSyscallReturn,userRoot,kernelRoot,configuration.SettingsBytes,checked((int)config["fd"]));
    }

    /// <summary>Value-only synthetic control seam for the same continuing
    /// semantic model. Caller ELF, roots, settings counts and instruction
    /// predicates grant no prepared-program, app, source or guest authority.
    /// Borrowed raw rows are framed and replayed; invalid observations throw.</summary>
    public static WebReplayReport InspectNativeAotWebReplayForTests(byte[] elf,ReadOnlySpan<byte> raw,
        Func<ulong,bool> instruction,Func<ulong,bool> syscall,ulong userRoot,ulong kernelRoot,uint settingsBytes,int settingsFd)
    {
        var framed=NativeAotThreadingOutput.ReadWeb(raw);
        int last=framed.NormalizedTail.LastIndexOf("\r\n",StringComparison.Ordinal);Need(last>=0,"web_replay_records");
        string fragment=framed.NormalizedTail[(last+2)..];if(fragment.Length!=0)WebFragment(fragment);
        string[] rows=framed.NormalizedTail[..last].Split("\r\n",StringSplitOptions.None);
        var observations=new WebObservationReport(ReadLinuxCallWaits(rows,allowEpoll:true).Count,framed.WriteLengths.Count,
            Encoding.UTF8.GetByteCount(framed.Stdout),Encoding.UTF8.GetByteCount(framed.Stderr),0,fragment,
            Convert.ToHexString(SHA256.HashData(raw)),Convert.ToHexString(SHA256.HashData(raw[..(raw.Length-fragment.Length)])));
        return ReplayWeb(framed,observations,elf,instruction,syscall,userRoot,kernelRoot,settingsBytes,settingsFd);
    }

    private static WebReplayReport ReplayWeb(NativeAotThreadingOutput.Capture framed,WebObservationReport observed,byte[] elf,
        Func<ulong,bool> instruction,Func<ulong,bool> syscall,ulong userRoot,ulong kernelRoot,uint settingsBytes,int settingsFd)
    {
        string complete=framed.NormalizedTail[..(framed.NormalizedTail.Length-observed.TrailingFragment.Length)];
        string[] original=complete[..^2].Split("\r\n",StringSplitOptions.None);
        int[] configRows=Enumerable.Range(0,original.Length).Where(i=>original[i].StartsWith("M10_004_CONFIG ",StringComparison.Ordinal)).ToArray();
        Need(configRows.Length==1 && configRows[0]+1<original.Length,"web_replay_settings_call");
        var settingsCall=ReadLinuxCallWaits(original,allowEpoll:true).SingleOrDefault(c=>c.RecordIndex==configRows[0]+1);
        Need(settingsCall.Identity!=0 && original[configRows[0]+1].Contains(" nr=17 ",StringComparison.Ordinal),"web_replay_settings_call");
        string[] rows=original.Where(r=>!r.StartsWith("M10_004_",StringComparison.Ordinal) && r is not
            ("TEST_START m10.004" or "CLROS_M10_004_READY" or "TEST_PASS m10.004")).ToArray();
        var capture=NativeAotSocketGuest.ReadReadiness(new(framed.Stdout,string.Join("\r\n",rows)+"\r\n",
            framed.WriteLengths.ToArray(),framed.WriteChannels.ToArray(),framed.Stderr));
        observed=observed with { PendingEpollWaits=capture.PendingWaits };
        return new ThreadingProtocol("MinimalApiSmoke",Rows(capture.Output),elf,instruction,syscall,capture.Output.WriteLengths,
            userRoot,kernelRoot,socketCapture:capture,webSettingsBytes:settingsBytes,webSettingsFd:settingsFd,
            webSettingsCall:(settingsCall.Identity,settingsCall.Ordinal)).ValidateWeb(observed);
    }

    private sealed partial class ThreadingProtocol
    {
        internal WebReplayReport ValidateWeb(WebObservationReport observations)
        {
            Need(web,"web_replay_profile");
            try { _=Validate();throw new ToolFailure("web_replay_unexpected_finite_return"); }
            catch(WebBoundary boundary)
            {
                Need(position==rows.Length && active>0 && !stopping && calls==observations.Calls && writes==writeLengths.Count &&
                    irqs>0 && clones>0 && entropyBytes>0 && entropyBytes<=entropyLimit && barrierRegistered && signalPipeCreated && stackProbe &&
                    new ulong[]{2,3,8,11,13,18,35}.All(actions.Contains),"web_replay_incomplete_startup");
                Need(history.Count(t=>!t.Reaped)==active && ready.All(t=>!t.Reaped && (t!=socketSelected || webReturningToSupervisor)) &&
                    socketDescriptors.Values.Any(d=>!d.Port && d.Phase==2) && socketAccepts>=2 && socketSent>0 && socketReceived>0,"web_replay_live_owner");
                ulong current=socketSelected?.Identity??0;
                WebReplayFragment(observations.TrailingFragment,boundary.Expected);
                int completedTransfers=rows.Count(r=>r.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal))*2;
                return new(observations,new(calls,clones,reaped,irqs,drains,ticks,completedTransfers,signalReturns),
                    active,ready.Count,current,boundary.Expected,webReturningToSupervisor,checked(entropyLimit-(uint)entropyBytes),socketAccepts,socketSent,socketReceived);
            }
        }

        private void WebReplayFragment(string fragment,string expected)
        {
            if(fragment.Length==0)return;
            string[] tokens=fragment.Split(' ');string kind=tokens[0];
            bool network=kind is "M16_012_POLL" or "M16_012_WAKE";
            bool allowed=expected switch
            {
                "M16_008_SUPERVISOR"=>kind==expected && webReturningToSupervisor,
                "M16_009_IRQ"=>network || kind is "M16_008_CALL" or "M16_009_IRQ" or "M16_008_SYSCALL_TIME",
                "M16_009_DRAIN"=>network || kind is "M16_009_DRAIN" or "M16_008_SYSCALL_TIME",
                "dispatch_or_idle"=>network || ready.Count>0 && kind is "M16_008_CALL" or "M16_009_IRQ" or "M16_008_RESUME",
                _=>kind==expected
            };
            Need(allowed,"web_replay_fragment_stage");
            // The final numeric token may itself be incomplete. Only a field
            // followed by a delimiter supplies a complete identity observation.
            if(tokens.Length<3)return;
            ulong identity=ulong.Parse(tokens[1][9..],CultureInfo.InvariantCulture);
            if(network)
                Need(kind=="M16_012_POLL"?identity==(socketSelected?.Identity??0):
                    history.Any(t=>!t.Reaped && t.Identity==identity && t.WaitSource==3),"web_replay_fragment_owner");
            else
            {
                ulong owner=socketSelected?.Identity??(ready.TryPeek(out var next)?next.Identity:0);
                Need(owner!=0 && identity==owner,"web_replay_fragment_owner");
            }
        }
    }

    private static readonly string[] WebCallFields="identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status".Split(' ').Select(x=>x+":dec").ToArray();
    private static readonly string[] WebCallSuffix="ordinal waitSource waitDeadline waitSequence signalDepth signalFrame blockedSignals returnRip returnRsp returnFlags returnFs returnGs returnValueBits".Split(' ');

    private static WebObservationReport ReadWebObservations(ReadOnlySpan<byte> raw,uint arguments,uint environment,uint settingsBytes,
        Func<ulong,bool>? syscall,Func<ulong,bool>? instruction)
    {
        Need(arguments==5 && environment is >=2 and <=3 && settingsBytes is >0 and <=65536,"web_observation_policy");
        var framed=NativeAotThreadingOutput.ReadWeb(raw);
        int last=framed.NormalizedTail.LastIndexOf("\r\n",StringComparison.Ordinal);
        Need(last>=0,"web_observation_records");
        string fragment=framed.NormalizedTail[(last+2)..];
        if(fragment.Length!=0)WebFragment(fragment);
        string complete=framed.NormalizedTail[..(last+2)];
        string[] rows=complete[..^2].Split("\r\n",StringSplitOptions.None);
        Need(rows.Length is >=12 and <=100000 && rows.All(r=>r.Length!=0 && r.All(c=>c is >= ' ' and <= '~')) &&
            rows[0]=="TEST_START m10.004" && rows[1]=="TEST_START m16-009" && rows.Count(r=>r==rows[0])==1,"web_observation_records");
        Need(!rows.Any(r=>r.StartsWith("TEST_FAIL",StringComparison.Ordinal) || r.StartsWith("OS_SMOKE_FAIL",StringComparison.Ordinal) ||
            r.StartsWith("M16_008_OWNER_END",StringComparison.Ordinal) || r.StartsWith("M16_009_RESTORE",StringComparison.Ordinal) ||
            r.StartsWith("M16_011_END",StringComparison.Ordinal) || r=="CLROS_M16_009_READY"),"web_observation_failure_or_cleanup");
        string[] markers=["M10_004_ENTER ","M10_004_CONFIG ","M10_004_LISTEN ","M10_004_LIVE ","CLROS_M10_004_READY","TEST_PASS m10.004"];
        int[] positions=new int[markers.Length]; int previous=1;
        for(int i=0;i<markers.Length;i++)
        {
            int[] found=Enumerable.Range(0,rows.Length).Where(n=>i<4?rows[n].StartsWith(markers[i],StringComparison.Ordinal):rows[n]==markers[i]).ToArray();
            Need(found.Length==1 && found[0]>previous,"web_observation_marker_order"); positions[i]=previous=found[0];
        }
        Need(!rows.Any(r=>r.StartsWith("M10_004_",StringComparison.Ordinal) && !markers.Take(4).Any(m=>r.StartsWith(m,StringComparison.Ordinal))),"web_observation_unknown_marker");
        var waits=ReadLinuxCallWaits(rows,allowEpoll:true).ToDictionary(x=>x.RecordIndex);
        var returned=ReadLinuxCallReturns(rows,allowEpoll:true).ToDictionary(x=>x.RecordIndex);
        string[] calls=ValidateLinuxCallOrdinals(rows,allowEpoll:true);
        Dictionary<int,Dictionary<string,ulong>> parsed=[];
        foreach(var call in waits)
        {
            var r=ReadRecord(calls[call.Key],"M16_008_CALL",WebCallFields); var result=returned[call.Key];
            Need((uint)r["identity"] is >=1 and <=16 && r["identity"]>>32!=0 &&
                result.Identity==r["identity"] && result.ValueBits==r["valueBits"] &&
                (syscall is null || syscall(r["rip"])) && (instruction is null || instruction(result.Rip)),"web_observation_call_binding");
            parsed.Add(call.Key,r);
        }
        Need(parsed.Count>=4,"web_observation_calls");
        if(instruction is not null)foreach(string row in rows.Where(r=>r.StartsWith("M16_009_IRQ ",StringComparison.Ordinal)))
        {
            var irq=ReadRecord(row,"M16_009_IRQ","identity:dec","epoch:dec","nanos:dec","rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","valueBits:dec");
            Need(instruction(irq["rip"]),"web_observation_irq_instruction");
        }
        var entry=ReadRecord(rows[positions[0]],"M10_004_ENTER","identity:dec","rip:dec","argc:dec","envc:dec","configBytes:dec");
        var first=parsed.OrderBy(x=>x.Key).First();
        Need(first.Key==positions[0]+1 && waits[first.Key].Ordinal==1 && entry["identity"]==first.Value["identity"] &&
            entry["rip"]==first.Value["rip"] && entry["argc"]==arguments && entry["envc"]==environment && entry["configBytes"]==settingsBytes,"web_observation_entry");
        const string configSuffix=" source=immutable-package copied=user";
        Need(rows[positions[1]].EndsWith(configSuffix,StringComparison.Ordinal),"web_observation_config");
        var config=ReadRecord(rows[positions[1]][..^configSuffix.Length],"M10_004_CONFIG","fd:dec","bytes:dec");
        Need(parsed.TryGetValue(positions[1]+1,out var read) && read["nr"] is 0 or 17 or 19 && read["a0"]==config["fd"] &&
            read["valueBits"]>0 && read["valueBits"]<=settingsBytes && config["bytes"]==settingsBytes,"web_observation_config");
        const string listenSuffix=" address=0 port=8080 owner=live";
        Need(rows[positions[2]].EndsWith(listenSuffix,StringComparison.Ordinal),"web_observation_listen");
        var listen=ReadRecord(rows[positions[2]][..^listenSuffix.Length],"M10_004_LISTEN","fd:dec");
        Need(parsed.TryGetValue(positions[2]+1,out var listenCall) && listenCall["nr"]==50 && listenCall["a0"]==listen["fd"] && listenCall["valueBits"]==0,"web_observation_listen");
        const string liveSuffix=" owner=retained externalHttp=required";
        Need(rows[positions[3]].EndsWith(liveSuffix,StringComparison.Ordinal) && positions[4]==positions[3]+1 && positions[5]==positions[4]+1,"web_observation_live");
        var live=ReadRecord(rows[positions[3]][..^liveSuffix.Length],"M10_004_LIVE","argc:dec","envc:dec","configBytes:dec","bindFd:dec","stdoutBytes:dec","stderrBytes:dec","active:dec","clock:dec");
        Need(live["argc"]==arguments && live["envc"]==environment && live["configBytes"]==settingsBytes && live["bindFd"]==listen["fd"] &&
            live["active"] is >0 and <=16 && live["clock"]>0,"web_observation_live");
        int writes=0,stdout=0,stderr=0,pending=0; uint channel=0;
        for(int i=0;i<rows.Length;i++)
        {
            if(rows[i].StartsWith("M16_005_WRITE ",StringComparison.Ordinal))
            {
                Need(writes<framed.WriteLengths.Count,"web_observation_write");
                uint fd=framed.WriteChannels[writes];int bytes=framed.WriteLengths[writes++];
                Need(rows[i]==$"M16_005_WRITE fd={fd} bytes={bytes} data=" && (pending==0 || channel==fd),"web_observation_write");
                channel=fd;pending+=bytes;if(fd==1)stdout+=bytes;else stderr+=bytes;
            }
            if(i==positions[3])Need(live["stdoutBytes"]==(ulong)stdout && live["stderrBytes"]==(ulong)stderr,"web_observation_output_counters");
            if(parsed.TryGetValue(i,out var call))
            {
                bool output=call["nr"] is 1 or 20 && call["a0"] is 1 or 2 && unchecked((long)call["valueBits"])>0;
                Need(pending==0?!output:output && call["a0"]==channel && call["valueBits"]==(ulong)pending,"web_observation_write_call");
                if(output)Need(call["nr"]==1?
                    call["a2"] is >0 and <=OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.MaximumIo && call["valueBits"]<=call["a2"]:
                    call["a2"] is >0 and <=OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.MaximumVectors,"web_observation_write_request");
                pending=0;
            }
        }
        Need(pending==0 && writes==framed.WriteLengths.Count,"web_observation_orphan_write");
        var observations=NativeAotSocketGuest.ReadReadiness(new(framed.Stdout,complete,framed.WriteLengths.ToArray(),framed.WriteChannels.ToArray(),framed.Stderr));
        return new(parsed.Count,writes,stdout,stderr,observations.PendingWaits,fragment,
            Convert.ToHexString(SHA256.HashData(raw)),Convert.ToHexString(SHA256.HashData(raw[..(raw.Length-fragment.Length)])));
    }

    // An owned host stop can cut a kernel record. Retain a bounded canonical
    // prefix rather than synthesizing its absent fields or a guest terminal.
    private static void WebFragment(string fragment)
    {
        Need(fragment.Length<=2048 && fragment.All(c=>c is >= ' ' and <= '~'),"web_fragment_shape");
        string[] tokens=fragment.Split(' ');
        string[] fields=tokens[0] switch
        {
            "M16_008_CALL"=>[..WebCallFields.Select(f=>f[..^4]),..WebCallSuffix],
            "M16_008_SYSCALL_TIME"=>"identity ordinal nr entered admitted dispatched prepared periodFs".Split(' '),
            "M16_012_POLL"=>"identity clock ready idle".Split(' '),
            "M16_012_WAKE"=>"identity sequence completion state clock".Split(' '),
            "M16_012_COMPLETE"=>"identity sequence completion valueBits clock".Split(' '),
            "M16_012_REPARK"=>"identity previous sequence deadline clock".Split(' '),
            "M16_012_EXPIRE"=>"identity ordinal deadline clock".Split(' '),
            "M16_008_RESUME"=>"identity valueBits status".Split(' '),
            "M16_008_SUPERVISOR"=>"identity action transfers ready active".Split(' '),
            "M16_009_DRAIN"=>"identity epoch nanos".Split(' '),
            "M16_009_IRQ"=>"identity epoch nanos rip rsp flags fs gs valueBits".Split(' '),
            "M16_008_TIMING"=>"identity epoch start arm capture ready periodFs".Split(' '),
            _=>throw new ToolFailure("web_fragment_record")
        };
        Need(tokens.Length<=fields.Length+1,"web_fragment_fields");
        for(int i=1;i<tokens.Length;i++)
        {
            string prefix=fields[i-1]+"=";bool final=i==tokens.Length-1;
            if(final && prefix.StartsWith(tokens[i],StringComparison.Ordinal))continue;
            Need(tokens[i].StartsWith(prefix,StringComparison.Ordinal),"web_fragment_fields");
            string number=tokens[i][prefix.Length..];
            Need(number.Length!=0 && (number.Length==1 || number[0]!='0') && number.All(c=>c is >= '0' and <= '9') &&
                ulong.TryParse(number,out _),"web_fragment_number");
        }
    }
}
