using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Complete inner-leaf accounting, without parent firmware, native
    /// ABI, NIC, packet, peer, runner or guest-qualification authority.</summary>
    public readonly record struct ServiceReplayReport(ThreadingProtocolReport Protocol,ServiceStartupInspection Startup,
        ServiceRandomInspection Random,ServiceTransportInspection Transport,int SocketAccepts=0,ulong SocketSent=0,ulong SocketReceived=0,
        ServiceGcInspection? Gc=null,ServiceMemoryInspection? Memory=null);

    /// <summary>Borrow an original package, sealed prepared service program and
    /// bounded complete raw leaf (including preceding seed/bound observations).
    /// Hold current source files while replaying startup, all syscall/memory/
    /// scheduler/wait/signal generations, random/compact ownership and genuine
    /// shutdown/reaps. Invalid or incomplete inputs throw. Parent provenance,
    /// independent packets/HTTP/control witnesses and original native ABI review
    /// remain mandatory; this method alone never accepts a guest run.</summary>
    public static ServiceReplayReport ValidateNativeAotServiceTail(string root,byte[] package,NativeAotServiceProgram program,
        ReadOnlySpan<byte> rawTail,ulong userRoot,ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root),program.SourceRoot,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),"service_program_owner");
        using AuthorityFiles sources=new();NativeAotServiceProgram.HoldSources(root,sources);
        var view=NativeAotServiceProgram.RequirePackage(package,program!.Lifecycle);
        var report=ReplayService(NativeAotThreadingOutput.ReadService(rawTail),package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes).ToArray(),
            program.IsInstruction,program.IsSyscallReturn,userRoot,kernelRoot,program.Lifecycle,view.Manifest.MemoryBytes);
        _=NativeAotServiceProgram.RequirePackage(package,program.Lifecycle);
        NativeAotServiceProgram.HoldSources(root,sources);sources.VerifyUnchanged();return report;
    }

    private static ServiceReplayReport ReplayService(NativeAotThreadingOutput.Capture framed,byte[] elf,
        Func<ulong,bool> instruction,Func<ulong,bool> syscallReturn,ulong userRoot,ulong kernelRoot,bool lifecycle,ulong entitlementBytes)
    {
        Need(framed.NormalizedTail.EndsWith("\r\n",StringComparison.Ordinal),"service_complete_terminal");
        string[] original=framed.NormalizedTail[..^2].Split("\r\n",StringSplitOptions.None);
        var startup=ReadServiceStartup(framed,out var metadata,lifecycle);
        var gc=ReadServiceGc(framed,entitlementBytes,lifecycle,out var gcBudgetPosition,out var gcMemoryPosition);
        if(gcBudgetPosition.HasValue)metadata.Add(gcBudgetPosition.Value);
        if(gcMemoryPosition.HasValue)metadata.Add(gcMemoryPosition.Value);
        var memory=ReadServiceMemory(framed,lifecycle,gc,out var memoryPositions);metadata.UnionWith(memoryPositions);
        if(lifecycle)Need(memory.HasValue && memory.Value.GcArena.HasValue && memory.Value.NativeReserveFits &&
            memory.Value.ConservativeHeadroomFits,"service_memory_headroom");
        var random=ReadServiceRandom(original);var transport=ReadServiceTransport(original,out var network);
        metadata.UnionWith(network);ServiceRuntimeMetadata(original,startup,metadata);
        for(int i=0;i<original.Length;i++)
            if(original[i].StartsWith("M11_012_",StringComparison.Ordinal) ||
                original[i]=="M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none")metadata.Add(i);
        int[] start=Enumerable.Range(0,original.Length).Where(index=>original[index]=="TEST_START m16-009").ToArray();
        Need(start.Length==1,"service_complete_start");
        string[] rows=original.Where((_,index)=>index>=start[0] && !metadata.Contains(index)).ToArray();
        var capture=NativeAotSocketGuest.ReadServiceReadiness(new(framed.Stdout,string.Join("\r\n",rows)+"\r\n",
            framed.WriteLengths.ToArray(),framed.WriteChannels.ToArray(),framed.Stderr));
        var replay=new ThreadingProtocol(NativeAotServicePackage.Application,rows,elf,instruction,syscallReturn,
            framed.WriteLengths,userRoot,kernelRoot,socketCapture:capture,webSettingsBytes:startup.SettingsBytes,
            webSettingsFd:startup.SettingsDescriptor,webSettingsCall:(startup.SettingsIdentity,startup.SettingsOrdinal));
        var protocol=replay.Validate();var counts=replay.ServiceSocketCounts();
        Need((ulong)protocol.Calls>=random.Requests && protocol.Reaped==protocol.Clones+1 && transport.Connections>=10,
            "service_complete_joins");
        return new(protocol,startup,random,transport,counts.Accepts,counts.Sent,counts.Received,gc,memory);
    }

    private sealed partial class ThreadingProtocol
    {
        internal (int Accepts,ulong Sent,ulong Received) ServiceSocketCounts() => (socketAccepts,socketSent,socketReceived);
    }

    private static void ServiceRuntimeMetadata(string[] rows,ServiceStartupInspection startup,HashSet<int> metadata)
    {
        var calls=ValidateLinuxServiceCallOrdinals(rows);var returns=ReadLinuxServiceCallReturns(rows).ToDictionary(row=>row.RecordIndex);
        int One(string prefix)
        {
            int[] found=Enumerable.Range(0,rows.Length).Where(index=>rows[index].StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            Need(found.Length==1,"service_runtime_metadata_once");return found[0];
        }
        int progress=One("M16_014_PROGRESS "),closed=One("M16_014_LISTENER_CLOSE "),randomEnd=One("M11_012_END "),
            sequenceEnd=One("M07_033_END "),transportEnd=One("M07_030_END "),budgetEnd=One("M16_008_BUDGET_END ");
        const string suffix=" process=live cleanup=pending";
        Need(rows[closed].EndsWith(suffix,StringComparison.Ordinal) && closed>progress && closed+1<rows.Length &&
            returns.ContainsKey(closed+1),"service_listener_close_order");
        var close=ReadRecord(rows[closed][..^suffix.Length],"M16_014_LISTENER_CLOSE","identity:dec","fd:dec");
        var call=ReadRecord(calls[closed+1],"M16_008_CALL",WebCallFields);
        Need(close["fd"]==(ulong)startup.ListenerDescriptor && close["identity"]==call["identity"] && call["nr"]==3 &&
            call["a0"]==close["fd"] && call["valueBits"]==0 && call["action"]==1 && call["status"]==1,"service_listener_close_call");
        metadata.Add(closed);
        int[] groups=returns.Keys.Where(index=>ReadRecord(calls[index],"M16_008_CALL",WebCallFields)["nr"]==231).ToArray();
        Need(groups.Length==1,"service_group_exit_once");int group=groups[0];
        Need(group>closed && transportEnd>group && randomEnd>transportEnd && sequenceEnd==randomEnd+1 && budgetEnd>sequenceEnd &&
            Enumerable.Range(group+1,randomEnd-group-1).Any(index=>rows[index].StartsWith("M16_008_REAP ",StringComparison.Ordinal)) &&
            !returns.Keys.Any(index=>index>group),"service_runtime_cleanup_order");
    }

    private sealed partial class ThreadingProtocol
    {
        private bool serviceReady;
        private ulong servicePeakIdentity, servicePeakCalls, serviceBudgetFinished;

        private void ServiceBegin()
        {
            Exact("TEST_START m16.014");
            const string policy=" elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
            string line=Take(); Need(line.EndsWith(policy,StringComparison.Ordinal),"service_budget_policy");
            var row=ReadRecord(line[..^policy.Length],"M16_014_POLICY","started:dec","callsPerTurn:dec","progressNanos:dec");
            Need(row["callsPerTurn"]==LinuxSyscallGateway.MaximumThreadCalls &&
                row["progressNanos"]==LinuxThreadExecutionBudget.ServiceProgressNanoseconds &&
                budget.Begin((uint)capacity,row["started"],application:true,continuingWeb:true,continuingService:true),"service_budget_policy");
            // The initial network snapshot predates its first clock read. It
            // is an exact bootstrap sentinel, not a reading before this anchor.
            Exact("M16_012_POLL identity=0 clock=0 ready=1 idle=0");
        }

        private void ServiceClock(ulong now,ref LinuxThreadExecutionBudget observed)
        {
            // Cancellation ends operational admission. Actual exit/drain/reap
            // records remain mandatory and must not revive this watchdog.
            if(observed.CancellationRequested)return;
            bool Runnable(RuntimeThread thread)=>!thread.Exiting &&
                (ready.Contains(thread) || thread==socketSelected && !thread.Parking);
            if(!observed.ObserveProgress(now,(ulong)calls,history.Any(thread=>!thread.Reaped && Runnable(thread))))
                throw new ToolFailure("service_progress_watchdog",$"position={position} now={now} calls={calls} ready={ready.Count} selected={socketSelected?.Identity??0} row={rows[Math.Min(position,rows.Length-1)]}");
            foreach(var thread in history.Where(thread=>!thread.Reaped))
                Need(observed.ObserveWorker(now,(uint)thread.Identity-1,thread.TotalCalls,Runnable(thread)),"service_worker_watchdog");
        }

        private void ServiceCall(RuntimeThread thread,LinuxCallWaitObservation witness)
        {
            Need(!budget.CancellationRequested && thread.TotalCalls<ulong.MaxValue && witness.Ordinal==thread.TotalCalls+1,
                "service_worker_call_counter");
            ulong turn=(ulong)epochs+1;
            if(thread.TurnEpoch!=turn){thread.TurnEpoch=turn;thread.TurnCalls=0;}
            Need(thread.TurnCalls<LinuxSyscallGateway.MaximumThreadCalls,"service_turn_quota");
            thread.TurnCalls++;thread.TotalCalls=witness.Ordinal;
            if(thread.TotalCalls>servicePeakCalls){servicePeakCalls=thread.TotalCalls;servicePeakIdentity=thread.Identity;}
        }

        private void ServiceProgress()
        {
            if(!Starts("M16_014_PROGRESS "))return;
            const string suffix=" owner=live cleanup=pending"; string line=Take();
            Need(!serviceReady && line.EndsWith(suffix,StringComparison.Ordinal),"service_ready_once");
            var row=ReadRecord(line[..^suffix.Length],"M16_014_PROGRESS","identity:dec","calls:dec","started:dec","nanos:dec","epochs:dec","active:dec");
            Need(row["identity"]==servicePeakIdentity && row["calls"]==servicePeakCalls && servicePeakCalls>4096 &&
                history.Any(thread=>!thread.Reaped && thread.Identity==servicePeakIdentity && thread.TotalCalls==servicePeakCalls) &&
                row["started"]==budget.StartedNanoseconds && row["nanos"]>=clock &&
                row["nanos"]>=row["started"] && row["nanos"]-row["started"]>LinuxThreadExecutionBudget.RuntimeNanoseconds &&
                row["epochs"]==(ulong)epochs && epochs>0 && row["active"]==(ulong)active,"service_ready_accounting");
            // This supervisor snapshot is emitted before the pending network
            // poll publishes its older clock sample. Validate it against a copy
            // of the same watchdog; do not reorder the actual poll/call stream.
            var observed=budget;ServiceClock(row["nanos"],ref observed);
            Exact("CLROS_M16_014_READY"); serviceReady=true;
        }

        private void ServiceFinish()
        {
            const string suffix=" process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
            string line=Take(); Need(serviceReady && budget.CancellationRequested && line.EndsWith(suffix,StringComparison.Ordinal),"service_owner_cleanup");
            var row=ReadRecord(line[..^suffix.Length],"M16_014_END","identity:dec","calls:dec","started:dec","nanos:dec","reaped:dec");
            Need(row["identity"]==servicePeakIdentity && row["calls"]==servicePeakCalls && servicePeakCalls>4096 &&
                row["started"]==budget.StartedNanoseconds && row["nanos"]>=Math.Max(clock,serviceBudgetFinished) && row["nanos"]<=long.MaxValue &&
                row["nanos"]-row["started"]>LinuxThreadExecutionBudget.RuntimeNanoseconds && row["reaped"]==(ulong)reaped &&
                history.All(thread=>thread.Reaped),"service_owner_accounting");
            Exact("TEST_PASS m16.014");
        }
    }

    /// <summary>Value-only complete scheduler/syscall replay seam. Caller ELF,
    /// predicates, settings and framed observations confer no source, program,
    /// application-output, transport, runner or guest authority. Unsupported or
    /// incomplete lifetimes throw; inputs are borrowed without modification.</summary>
    public static ThreadingProtocolReport InspectNativeAotServiceProtocolForTests(byte[] elf,string[] rows,
        IReadOnlyList<int> lengths,Func<ulong,bool> instruction,Func<ulong,bool> syscallReturn,
        ulong userRoot,ulong kernelRoot,uint settingsBytes,int settingsFd,ulong settingsIdentity,ulong settingsOrdinal)
    {
        var framed=new NativeAotThreadingOutput.Capture("",string.Join("\r\n",rows)+"\r\n",lengths.ToArray(),
            Enumerable.Repeat(1U,lengths.Count).ToArray(),"");
        var capture=NativeAotSocketGuest.ReadServiceReadiness(framed);
        return new ThreadingProtocol(NativeAotServicePackage.Application,Rows(capture.Output),elf,instruction,syscallReturn,
            capture.Output.WriteLengths,userRoot,kernelRoot,socketCapture:capture,webSettingsBytes:settingsBytes,webSettingsFd:settingsFd,
            webSettingsCall:(settingsIdentity,settingsOrdinal)).Validate();
    }
}
