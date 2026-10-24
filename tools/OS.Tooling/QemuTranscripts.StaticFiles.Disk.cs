using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied complete scheduler/file replay facts. Sector rows retain
    /// their original positions for the separate device and read-window joins.
    /// This value inspection grants no original native, process, packet, HTTP
    /// or guest authority; a later inspection cannot rescue a failed original.</summary>
    public sealed record DiskStaticReplayReport(StaticFileReplayReport Static,
        IReadOnlyList<NativeDiskStaticReadWindows.ReadWindow> Reads,
        IReadOnlyList<DiskStaticOperation> Operations, IReadOnlyList<LinuxDiskAuxiliary> DeviceRows, ulong Owner, ulong Epoch,
        ulong BootstrapCompleted, ulong BootstrapCopied, int ReadinessRecordIndex)
    {
        public bool CheckedDeviceBytesOrHttp => false;
        public bool GrantsOriginalExecutionAuthority => false;
    }

    /// <summary>Actual captured call interval through its consumed completion.
    /// Metadata copies must fit an original pending interval; overlap grants
    /// no unique request attribution. Values do not reconstruct the FS cache.</summary>
    public readonly record struct DiskStaticOperation(ulong Identity,ulong Ordinal,ulong Number,ulong Request,
        int BeginRecordIndex,int WakeRecordIndex,int CompleteRecordIndex,uint OutputPrefix,long Value);

    public sealed record DiskStaticSectorValues(NativeDiskReadObservations.Inspection Device,
        NativeDiskStaticReadWindows.Inspection Data,IReadOnlyList<WebRootDiskInventory.MetadataSector> Metadata)
    {
        public bool GrantsOriginalNativeProcessOrHttpAuthority=>false;
        public bool ReconstructsFileSystemCache=>false;
    }

    /// <summary>Join copied scheduler/file intervals to every original indexed
    /// sector observation using the same held image and independent inventory.
    /// Require exact one-request device lifecycle/bytes, unique positive data
    /// coverage, and physical FAT/directory-only residuals inside actual pending
    /// operations. This is a synchronous value join, not a process owner or an
    /// FS-cache replay. The caller retains original source/native/packet authority.</summary>
    public static DiskStaticSectorValues InspectNativeAotDiskStaticSectorValues(DiskStaticReplayReport replay,
        WebRootDiskInventory.Inspection inventory,byte[] heldImage,NativeDiskReadObservations.Boundary final)
    {
        Need(replay is not null && inventory is not null && replay.Operations is {Count:>0 and <=4096} &&
            final.Owner==replay.Owner && final.Epoch==replay.Epoch && final.BootstrapCompleted==replay.BootstrapCompleted &&
            final.BootstrapCopied==replay.BootstrapCopied,"disk_static_sector_boundary");
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(inventory);
        var device=NativeDiskReadObservations.Inspect(replay.DeviceRows.Select(r=>r.Text).ToArray(),heldImage,final);
        var data=NativeDiskStaticReadWindows.Inspect(inventory,device,replay.DeviceRows.Select(r=>r.RecordIndex).ToArray(),replay.Reads);
        var metadata=WebRootDiskInventory.ClassifyMetadata(inventory,heldImage,data.Residual.Select(c=>c.Observation.Sector).ToArray());
        var operations=replay.Operations.OrderBy(o=>o.BeginRecordIndex).ToArray();
        Need(operations.Select(o=>o.Request).Distinct().Count()==operations.Length,"disk_static_operation_requests");
        var byRequest=operations.ToDictionary(o=>o.Request);
        foreach(var assigned in data.Assigned)
        {
            var window=assigned.Window;
            Need(byRequest.TryGetValue(window.RequestGeneration,out var op) && op.Identity==window.Identity &&
                op.Ordinal==window.Ordinal && op.Number==window.Number && op.BeginRecordIndex==window.BeginRecordIndex &&
                op.CompleteRecordIndex==window.CompleteRecordIndex && op.OutputPrefix==window.OutputPrefix &&
                op.Value==window.ReturnedBytes && assigned.Copy.CopyRecordIndex<op.WakeRecordIndex,
                "disk_static_data_ready_interval");
        }
        int[] begins=new int[operations.Length],ends=new int[operations.Length];
        for(int i=0;i<operations.Length;i++)
        {
            var op=operations[i];
            Need(op.BeginRecordIndex>=0 && op.WakeRecordIndex>op.BeginRecordIndex && op.CompleteRecordIndex>op.WakeRecordIndex &&
                (i==0 || op.BeginRecordIndex>begins[i-1]),"disk_static_operation_intervals");
            begins[i]=op.BeginRecordIndex;ends[i]=Math.Max(op.WakeRecordIndex,i==0?-1:ends[i-1]);
        }
        foreach(var copy in data.Residual)
        {
            int at=Array.BinarySearch(begins,copy.SubmitRecordIndex);at=at>=0?at-1:~at-1;
            Need(at>=0 && ends[at]>copy.CopyRecordIndex,"disk_static_metadata_pending_interval");
        }
        return new(device,data,metadata);
    }

    /// <summary>Borrow one complete inner m16-009 tail, original ELF, instruction
    /// predicates and independent physical inventory. Replay actual source4
    /// waits in the existing full memory/FIFO/epoll/signal model. Descriptor,
    /// request and block generation domains remain separate. Missing, unsupported
    /// or inconsistent rows reject. Inputs supply copied values only; enclosing
    /// original image/native/process/packet/HTTP owners remain mandatory.</summary>
    public static DiskStaticReplayReport InspectNativeAotDiskStaticProtocolForTests(byte[] elf,
        ReadOnlySpan<byte> raw, Func<ulong,bool> instruction, Func<ulong,bool> syscallReturn,
        ulong userRoot, ulong kernelRoot, WebRootDiskInventory.Inspection inventory)
    {
        Need(inventory is not null,"disk_static_inventory_missing");
        var output=NativeAotThreadingOutput.ReadWeb(raw);
        var capture=NativeAotSocketGuest.ReadDiskStaticReadiness(output);
        var replay=new ThreadingProtocol(NativeAotStaticFilePublisher.Application,Rows(output),elf,instruction,
            syscallReturn,output.WriteLengths,userRoot,kernelRoot,socketCapture:capture,diskInventory:inventory);
        return replay.DiskStaticValidate();
    }

    private sealed class DiskStaticPending(Dictionary<string,ulong> call,LinuxCallWaitObservation witness,
        uint outputPrefix,uint pathPrefix)
    {
        internal readonly Dictionary<string,ulong> Call=new(call);
        internal readonly LinuxCallWaitObservation Witness=witness;
        internal readonly int BeginIndex=witness.RecordIndex;
        internal readonly uint OutputPrefix=outputPrefix,PathPrefix=pathPrefix;
        internal ulong Request;
        internal uint Winner;
        internal bool Observed;
        internal int WakeIndex;
        internal uint CapturedDescriptorGeneration;
    }

    private sealed partial class ThreadingProtocol
    {
        private readonly WebRootDiskInventory.Inspection? diskInventory;
        private string StaticTag=>diskInventory is null?"M10_017":"M10_018";
        private readonly List<LinuxDiskAuxiliary> diskIo=[];
        private readonly List<NativeDiskStaticReadWindows.ReadWindow> diskReadWindows=[];
        private readonly List<DiskStaticOperation> diskOperations=[];
        private readonly HashSet<ulong> diskRequests=[];
        private DiskStaticPending? diskCompleting;
        private ulong diskOwner,diskEpoch,diskBootstrapCompleted,diskBootstrapCopied;
        private int diskReadyIndex=-1;

        internal DiskStaticReplayReport DiskStaticValidate()
        {
            try { return DiskStaticReport(Validate()); }
            catch(ToolFailure failure)
            { throw new ToolFailure(failure.Reason,$"position={position} row={rows[Math.Min(position,rows.Length-1)]} detail={failure.Message}"); }
        }

        internal DiskStaticReplayReport DiskStaticReport(ThreadingProtocolReport protocol)
        {
            Need(diskReadyIndex>=0 && diskIo.Count>0 && diskReadWindows.Count>0 && history.All(t=>t.Disk is null) &&
                diskRequests.Count==diskOperations.Count,
                "disk_static_complete_observations");
            return new(StaticReport(protocol),Array.AsReadOnly(diskReadWindows.ToArray()),Array.AsReadOnly(diskOperations.ToArray()),Array.AsReadOnly(diskIo.ToArray()),
                diskOwner,diskEpoch,diskBootstrapCompleted,diskBootstrapCopied,diskReadyIndex);
        }

        private void DiskStaticBegin()
        {
            Exact("TEST_START m16.023");Exact("TEST_START m16.024");Exact("TEST_START m16.025");
            const string suffix=" pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
            string line=Take();Need(line.EndsWith(suffix,StringComparison.Ordinal),"disk_static_device_policy");
            var bound=ReadRecord(line[..^suffix.Length],"M16_023_BOUND","owner:dec","epoch:dec","arena:dec","pages:dec",
                "bar:dec","capacity:dec","bootstrapCompleted:dec","bootstrapCopied:dec","clock:dec");
            Need(bound["owner"] is >=4096 and <0x0000800000000000 && bound["owner"]%8==0 && bound["epoch"]!=0 &&
                bound["pages"] is >0 and <=256 && bound["arena"]>=4096 && bound["arena"]%4096==0 &&
                bound["arena"]<=0x100000000UL-bound["pages"]*4096 && bound["bar"]>=0x100000 &&
                bound["bar"]%16384==0 && bound["capacity"]>diskInventory!.PartitionLastLba &&
                bound["bootstrapCompleted"]>0 && bound["bootstrapCopied"]==bound["bootstrapCompleted"] &&
                bound["clock"]<=long.MaxValue,"disk_static_device_geometry");
            diskOwner=bound["owner"];diskEpoch=bound["epoch"];
            diskBootstrapCompleted=bound["bootstrapCompleted"];diskBootstrapCopied=bound["bootstrapCopied"];
        }

        private void DiskStaticReady()
        {
            int index=position;
            var row=Row("M10_018_DISK_READY","submitted:dec","completed:dec","copied:dec","clock:dec");
            // This finite consumer requires a drained readiness boundary. A
            // pre-arming live device request needs a separately reviewed join.
            Need(diskReadyIndex<0 && diskIo.Count==0 && row["submitted"]==row["completed"] &&
                row["completed"]==row["copied"] && row["completed"]>=diskBootstrapCompleted &&
                row["clock"]>=clock,"disk_static_drained_readiness");
            diskBootstrapCompleted=row["completed"];diskBootstrapCopied=row["copied"];diskReadyIndex=index;
            Need(Starts("M10_018_LISTENING "),"disk_static_readiness_listen_join");
            var listen=ReadRecord(rows[position][..^" http=external cleanup=pending".Length],"M10_018_LISTENING",
                "main:dec","writer:dec","fd:dec","nanos:dec");
            Need(listen["nanos"]==row["clock"],"disk_static_readiness_clock");
        }

        private uint DiskStaticPrefix(ulong address,uint maximum,uint access)
        {
            uint prefix=0;
            while(prefix<maximum)
            {
                if(address<4096 || address>=LinuxSyscallGateway.UserEnd)break;
                uint count=Math.Min(maximum-prefix,4096U-(uint)(address&4095));
                if(!memory.Covered(address,count,access))break;
                prefix+=count;address+=count;
            }
            return prefix;
        }

        private void DiskStaticStage(RuntimeThread thread,Dictionary<string,ulong> call,LinuxCallWaitObservation witness)
        {
            ulong nr=call["nr"];
            Need(thread.Disk is null && witness.Source==4 && witness.Deadline==0 && call["valueBits"]==0 &&
                nr is 0 or 2 or 4 or 6 or 8 or 17 or 257 && staticFileWitness is null && staticPathWitness is null,
                "disk_static_pending_call");
            Need(diskOperations.Count+history.Count(t=>t.Disk is not null)<4096,"disk_static_operation_bound");
            if(nr is 2 or 257)
            {
                ulong flags=call[nr==257?"a2":"a1"];
                Need((flags&(3UL|0x40|0x200|0x400))==0 && (flags&~0xa8900UL)==0 && LowestFd()>=0,
                    "disk_static_blocked_immediate_admission");
            }
            uint maximum=nr is 4 or 6?144U:nr is 0 or 17?(uint)Math.Min(call["a2"],8192UL):0;
            uint prefix=DiskStaticPrefix(call["a1"],maximum,3);
            uint pathPrefix=nr is 2 or 4 or 6 or 257?DiskStaticPrefix(call[nr==257?"a1":"a0"],256,1):0;
            var pending=new DiskStaticPending(call,witness,prefix,pathPrefix);
            if(nr is 0 or 8 or 17)
            {
                Need(call["a0"]<(ulong)descriptorCapacity && staticFilesByFd.TryGetValue((int)call["a0"],out var file) &&
                    file.Kind==5,"disk_static_pending_descriptor");
                pending.CapturedDescriptorGeneration=descriptorGenerations[(int)call["a0"]];
            }
            thread.Disk=pending;Stage(thread,witness,4,0,0,0);
        }

        // Device clocks are serial observation times, while table clocks are
        // sampled by scheduler mutations. Do not compare those distinct streams.
        private bool DiskStaticObservations()
        {
            if(diskInventory is null)return false;
            if(Starts("M16_023_IO "))
            {
                Need(diskReadyIndex>=0 && diskIo.Count<NativeDiskReadObservations.MaximumRows,"disk_static_io_scope");
                diskIo.Add(new(position,Take()));return true;
            }
            if(!Starts("M16_025_WAKE "))return false;
            int index=position;
            var row=Row("M16_025_WAKE","identity:dec","sequence:dec","request:dec","completion:dec","state:dec","clock:dec");
            var thread=history.SingleOrDefault(t=>!t.Reaped && t.Identity==row["identity"]);
            Need(thread is {WaitSource:4,Exiting:false,Disk:not null} && thread.LastSequence==row["sequence"] &&
                !thread.Disk.Observed && row["state"]==(thread.Parking?3UL:1UL) &&
                row["completion"] is 1 or 5 && row["request"]!=0,"disk_static_wake_owner");
            var pending=thread!.Disk!;
            if(pending.Request==0)
            { Need(diskRequests.Add(row["request"]),"disk_static_request_reuse");pending.Request=row["request"]; }
            else Need(pending.Request==row["request"],"disk_static_request_change");
            Clock(row["clock"]);
            if(row["completion"]==1)
            { Need(thread.Completion is null && pending.Winner==0,"disk_static_wake_winner");pending.Winner=1;Complete(thread,long.MinValue); }
            else Need(pending.Winner==5 && thread.Completion==-4,"disk_static_interrupt_winner");
            pending.Observed=true;pending.WakeIndex=index;return true;
        }

        private bool DiskStaticPrepare(RuntimeThread thread)
        {
            Need(diskInventory is not null && thread.Disk is {Observed:true} && thread.Completion.HasValue,"disk_static_prepare_owner");
            var pending=thread.Disk!;
            if(Starts("M16_025_REPARK "))
            {
                var row=Row("M16_025_REPARK","identity:dec","previous:dec","sequence:dec","request:dec","outputBytes:dec","clock:dec");
                Need(pending.Winner==1 && row["identity"]==thread.Identity && row["previous"]==thread.LastSequence &&
                    row["sequence"]>thread.LastSequence && row["request"]==pending.Request && row["outputBytes"]==pending.OutputPrefix,
                    "disk_static_repark_owner");
                Clock(row["clock"]);thread.LastSequence=row["sequence"];thread.WaitOrder=++waitOrder;thread.AddressOrder=++addressOrder;
                thread.Completion=null;thread.Parking=true;pending.Winner=0;pending.Observed=false;
                InterruptPendingWait(thread);SocketObservations();return true;
            }
            StaticBeforeCall(thread,deferred:true);
            var completed=Row("M16_025_COMPLETE","identity:dec","sequence:dec","request:dec","outputBytes:dec","completion:dec","valueBits:dec","clock:dec");
            Need(completed["identity"]==thread.Identity && completed["sequence"]==thread.LastSequence &&
                completed["request"]==pending.Request && completed["outputBytes"]==pending.OutputPrefix &&
                completed["completion"]==pending.Winner,"disk_static_complete_owner");
            Clock(completed["clock"]);
            diskOperations.Add(new(thread.Identity,pending.Witness.Ordinal,pending.Call["nr"],pending.Request,
                pending.BeginIndex,pending.WakeIndex,position-1,pending.OutputPrefix,unchecked((long)completed["valueBits"])));
            if(pending.CapturedDescriptorGeneration!=0)
                Need(descriptorGenerations[(int)pending.Call["a0"]]==pending.CapturedDescriptorGeneration &&
                    staticFilesByFd.ContainsKey((int)pending.Call["a0"]),"disk_static_complete_descriptor");
            var call=new Dictionary<string,ulong>(pending.Call)
            { ["valueBits"]=completed["valueBits"],["action"]=1,["status"]=1 };
            diskCompleting=pending;
            try
            {
                if(pending.Winner==5)
                { Need(unchecked((long)completed["valueBits"])==-4,"disk_static_interrupted_result");throw new ToolFailure("disk_static_interrupted_file_observation_unqualified"); }
                Need(StaticFileCall(call,pending.Witness),"disk_static_complete_file_call");
                Need(staticFileWitness is null && staticPathWitness is null,"disk_static_complete_metadata_consumed");
            }
            finally { diskCompleting=null; }
            thread.Completion=unchecked((long)completed["valueBits"]);SocketObservations();return false;
        }

        private Dictionary<string,ulong> DiskStaticPath(Dictionary<string,ulong> call,Dictionary<string,ulong> row,
            string text,string relative,bool open,bool trailing)
        {
            string path="/"+relative;
            string[] parts=relative.Split('/');
            bool valid=path.Length<256 && parts.Length<=11 && parts.All(p=>p.Length>0 && p is not ("." or "..") &&
                p[^1] is not ('.' or ' ') && p.All(c=>c is >=(char)33 and <=(char)126 && !"\\:*?\"<>|".Contains(c)));
            Need(diskCompleting is null || valid,"disk_static_blocked_immediate_path");
            var entry=diskInventory!.Find(path);
            long expected;ulong kind=0,inode=0,size=0,mode=0;
            if(!valid)expected=-13;
            else if(open && (call[call["nr"]==257?"a2":"a1"]&(3UL|0x40|0x200|0x400))!=0)expected=-30;
            else if(open && (call[call["nr"]==257?"a2":"a1"]&~0xa8900UL)!=0)expected=-22;
            else if(entry is null)
            {
                expected=-2;
                string parent=path;
                while(parent.Length>WebRootDiskInventory.Root.Length)
                { parent=parent[..parent.LastIndexOf('/')];if(diskInventory.Find(parent) is {IsDirectory:false}){expected=-20;break;} }
            }
            else if(trailing && !entry.IsDirectory)expected=-20;
            else if(open && entry.IsDirectory)expected=-38;
            else if(open)expected=LowestFd()<0?-24:LowestFd();
            else expected=diskCompleting is not null && diskCompleting.OutputPrefix<144 || !memory.Covered(call["a1"],144,3)?-14:0;
            if(valid && (!open || (call[call["nr"]==257?"a2":"a1"]&~0xa8900UL)==0 &&
                (call[call["nr"]==257?"a2":"a1"]&(3UL|0x40|0x200|0x400))==0))
                Need(diskCompleting is not null,"disk_static_missing_pending_path");
            if(expected>=0 && entry is not null)
            { kind=entry.IsDirectory?3UL:5UL;inode=entry.Inode;size=entry.Bytes;mode=entry.IsDirectory?0x416dUL:0x8124UL; }
            Need(call["valueBits"]==Bits(expected),"disk_static_path_result");
            Need(row["kind"]==kind && row["view"]==uint.MaxValue && row["inode"]==inode && row["bytes"]==size && row["mode"]==mode,
                "disk_static_path_metadata");
            if(open && call["nr"]==257 && !text.StartsWith('/'))Need(call["a0"]==Bits(-100),"disk_static_relative_openat");
            staticPathCount++;return row;
        }
    }
}
