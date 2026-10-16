using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal static IReadOnlyList<string> WebMarkers() => [
        ..SocketMarkers(true).TakeWhile(x => x != "TEST_START m16-011"),
        "TEST_START m10.004",..M16LinuxSignalMarkers.Take(3),"M16_011_BOUND ","M16_005_ARMED","M16_008_BOUND","M16_008_BUDGET",
        "M10_004_ENTER","M10_004_CONFIG","M10_004_LISTEN","M10_004_LIVE","CLROS_M10_004_READY","TEST_PASS m10.004"];

    internal static int ValidateWebRun(string root,QemuSmokeOptions options,QemuSmokePlan plan,ProcessResult runner,
        TextWriter output,NativeAotWebProgram program)
    {
        RequireCompleteProcess(runner,"web_runner_incomplete");
        if(runner.ExitCode!=0)return runner.ExitCode;
        Need(plan.IsEnabled("EnableM10004Probe") && program is not null &&
            string.Equals(Path.GetFullPath(root),program.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
            "web_prepared_owner");
        byte[] raw=File.ReadAllBytes(Path.GetFullPath(options.SerialLog,root));
        ReadOnlySpan<byte> start="TEST_START m10.004\r\n"u8;
        int at=raw.AsSpan().IndexOf(start);
        Need(at>=2 && raw[at-2]==13 && raw[at-1]==10 && raw.AsSpan(at+start.Length).IndexOf(start)<0,"web_raw_start");
        var observed=ValidateNativeAotWebPrefix(root,File.ReadAllBytes(Path.GetFullPath(options.LinuxElfPackage,root)),program!,raw.AsSpan(at));
        output.WriteLine($"WEB_PREFIX_OBSERVATIONS calls={observed.Calls} writes={observed.Writes} stdout={observed.StdoutBytes} stderr={observed.StderrBytes} pendingEpoll={observed.PendingEpollWaits} fragmentBytes={observed.TrailingFragment.Length} acceptance=pending");
        // A live HTTP exchange alone cannot pass the full smoke gate. The finite
        // runtime parser requires guest exit/reap and must never be given invented
        // completion rows for this continuing profile. Keep acceptance closed
        // until the independent continuing transcript/native/packet join exists.
        output.WriteLine("TEST_FAIL qemu.smoke web_continuing_evidence_admission_pending");
        return 1;
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
