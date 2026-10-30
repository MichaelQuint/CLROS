using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static unsafe class DiskStaticExecutionPolicyTests
{
    [HostTest("disk full live serial shares finite policy while ordinary and service bounds remain")]
    private static void LiveCapturePolicy()
    {
        HostCheck.Equal(128*1024*1024,DiskStaticExecutionPolicy.MaximumSerialBytes);
        HostCheck.Equal(DiskStaticExecutionPolicy.MaximumSerialBytes,DiskStaticCapturePolicy.SerialBytes);
        string source=File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(),"tools/OS.QemuRunner/Program.cs"));
        string normalized=string.Concat(source.Where(c=>!char.IsWhiteSpace(c)));
        HostCheck.True(normalized.Contains("SerialByteCapacity=>FullDiskStaticFiles?OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.MaximumSerialBytes:ContinuingService?SerialMarkerStateMachine.ContinuingServiceByteCapacity:SerialMarkerStateMachine.DefaultByteCapacity;",StringComparison.Ordinal));
        var full=new OS.Tools.QemuRunner.SerialMarkerStateMachine([],[],byteCapacity:DiskStaticCapturePolicy.SerialBytes);
        var ordinary=new OS.Tools.QemuRunner.SerialMarkerStateMachine([],[]);
        byte[] chunk=new byte[4096];Array.Fill(chunk,(byte)'x');chunk[^1]=(byte)'\n';
        for(long bytes=0;bytes<DiskStaticCapturePolicy.SerialBytes;bytes+=chunk.Length)
        {
            HostCheck.True(full.Feed(chunk).Succeeded);
            if(bytes<OS.Tools.QemuRunner.SerialMarkerStateMachine.DefaultByteCapacity)HostCheck.True(ordinary.Feed(chunk).Succeeded);
        }
        HostCheck.Equal(OS.Tools.QemuRunner.SerialMarkerFeedStatus.TotalCapacityExceeded,ordinary.Feed("x"u8).Status);
        HostCheck.Equal(OS.Tools.QemuRunner.SerialMarkerFeedStatus.TotalCapacityExceeded,full.Feed("x"u8).Status);
        HostCheck.Equal(64L*1024*1024,OS.Tools.QemuRunner.SerialMarkerStateMachine.ContinuingServiceByteCapacity);
    }

    [HostTest("disk full UTC admission uses a finite preentry anchor allowance without changing ordinary or continuing clocks")]
    private static void RealtimeAdmission()
    {
        HostCheck.Equal((ulong)DiskStaticCapturePolicy.MaximumTimeoutSeconds,DiskStaticExecutionPolicy.RealtimeMaximumElapsedSeconds);
        HostCheck.True(DiskStaticExecutionPolicy.RealtimeMaximumElapsedSeconds*1_000_000_000UL>DiskStaticExecutionPolicy.RuntimeNanoseconds);
        string root=HostToolProcess.FindRepositoryRoot();
        string source=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.Realtime.cs"));
        string normalized=string.Concat(source.Where(c=>!char.IsWhiteSpace(c)));
        HostCheck.True(normalized.Contains("maximumElapsedSeconds:Probes.EnableM16014Probe?0UL:Probes.EnableM10018FullProbe?DiskStaticExecutionPolicy.RealtimeMaximumElapsedSeconds:600UL,continuing:Probes.EnableM16014Probe",StringComparison.Ordinal));
        var admission=EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(admission.Succeeded,admission.Reason+" "+admission.Detail);
    }

    [HostTest("disk full selection rejects mixed and short policy while development stays unchanged")]
    private static void Selection()
    {
        string[] args=["-EnableM10018FullProbe","-BuildEfi","-LinuxElfPackage","artifacts/full.pkg",
            "-LinuxThreadingDisassembly","artifacts/full.dump","-WebRootFixture","DiskA","-WebRootAssets","artifacts/full-root"];
        var options=QemuSmokeOptions.Parse(args);var plan=QemuProfiles.Create(options);
        HostCheck.True(NativeAotDiskSelection.FullStaticSelected(plan.Flags));HostCheck.Equal(7200,options.TimeoutSeconds);
        HostCheck.Equal(3600,DiskStaticCapturePolicy.BrowserTimeoutSeconds);
        HostCheck.True(plan.IsEnabled("EnableM10018Probe") && plan.IsEnabled("EnableM16025Probe") && !plan.IsEnabled("EnableM16014Probe"));
        foreach(string[] suffix in new[]{new[]{"-EnableM10018Probe:false"},new[]{"-EnableM10017Probe"},new[]{"-EnableM16014Probe"},
            new[]{"-TimeoutSeconds","6599"},new[]{"-TimeoutSeconds","7201"},new[]{"-LinuxDiskRaceVariant","Progress"}})
        {
            bool denied=false;try{_=QemuProfiles.Create(QemuSmokeOptions.Parse([..args,..suffix]));}catch(ToolFailure){denied=true;}
            HostCheck.True(denied,string.Join(' ',suffix));
        }
        args[0]="-EnableM10018Probe";options=QemuSmokeOptions.Parse(args);plan=QemuProfiles.Create(options);
        HostCheck.Equal(825,options.TimeoutSeconds);HostCheck.True(!NativeAotDiskSelection.FullStaticSelected(plan.Flags));
        HostCheck.True(!OS.Kernel.Diagnostics.Probes.EnableM10018FullProbe);
        HostCheck.Equal("M10_018_FULL_PROBE",EfiContracts.ProbeSymbols["EnableM10018FullProbe"]);
        var admission=EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(admission.Succeeded,admission.Reason+" "+admission.Detail);
    }

    [HostTest("disk full timeout boundaries retain finite shutdown and parent cancellation")]
    private static void TimeoutBoundaries()
    {
        foreach(int seconds in new[]{6600,7200})
        {
            var options=QemuSmokeOptions.Parse(["-EnableM10018FullProbe","-BuildEfi","-LinuxElfPackage","artifacts/full.pkg",
                "-LinuxThreadingDisassembly","artifacts/full.dump","-WebRootFixture","DiskA","-WebRootAssets","artifacts/full-root",
                "-TimeoutSeconds",seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            _=QemuProfiles.Create(options);HostCheck.Equal(seconds,options.TimeoutSeconds);
        }
        HostCheck.Equal(6_000_000_000_000UL,DiskStaticExecutionPolicy.RequestDelayNanoseconds);
        HostCheck.Equal(120_000_000_000UL,DiskStaticExecutionPolicy.GraceNanoseconds);
        HostCheck.True(DiskStaticExecutionPolicy.RuntimeNanoseconds>DiskStaticExecutionPolicy.RequestDelayNanoseconds+DiskStaticExecutionPolicy.GraceNanoseconds);
        string root=HostToolProcess.FindRepositoryRoot();
        string source=File.ReadAllText(Path.Combine(root,"tools/OS.Tooling/NativeAotDiskStaticPeer.cs"));
        string normalized=string.Concat(source.Where(c=>!char.IsWhiteSpace(c)));
        HostCheck.True(normalized.Contains("awaitbrowser!(port,Path.Combine(directory,\"browser\"),owned.Token);",StringComparison.Ordinal));
        HostCheck.True(normalized.Contains("matrix.CancelAfter(TimeSpan.FromSeconds(full?1700:DiskStaticHttpContract.MatrixTimeoutSeconds));",StringComparison.Ordinal));
        HostCheck.True(normalized.Contains("owned.Cancel();if(peerisnotnull)try{awaitpeer;}catch{}",StringComparison.Ordinal));
    }

    [HostTest("disk full retains elapsed ceiling and disallows service quota renewal")]
    private static void Budget()
    {
        LinuxThreadExecutionBudget b=default;HostCheck.True(!b.Begin(16,100,diskStaticFull:true));
        HostCheck.True(!b.Begin(16,100,true,true,true,true));HostCheck.True(b.Begin(16,100,true,true,diskStaticFull:true));
        HostCheck.True(b.HasElapsedLimit && !b.IsContinuingService && !b.RequestCancellation());
        HostCheck.Equal(DiskStaticExecutionPolicy.RuntimeNanoseconds,b.ElapsedLimitNanoseconds);
        HostCheck.True(b.Continue(300_000_000_100,1,1));HostCheck.True(b.Observe(100+b.ElapsedLimitNanoseconds-1));
        HostCheck.True(!b.Observe(100+b.ElapsedLimitNanoseconds));
        b=default;HostCheck.True(b.Begin(16,100,true,true));HostCheck.Equal(300_000_000_000UL,b.ElapsedLimitNanoseconds);
        HostCheck.True(!b.Observe(300_000_000_100));
    }

    [HostTest("disk full lifecycle uses selected one shot termination and grace deadlines")]
    private static void Lifecycle()
    {
        LinuxStaticFileObservation o=default;HostCheck.True(o.Begin(7,diskStaticFull:true));
        byte[] entry="CLROS_STATIC_FILES_APP_ENTRY v1\n"u8.ToArray();
        fixed(byte* p=entry)HostCheck.True(o.ObserveOutput(7,2,p,(uint)entry.Length));
        HostCheck.True(o.RecordBind(7,3,true) && o.RecordListen(7,3,true,100));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Waiting,o.Poll(100+LinuxStaticFileObservation.RequestDelayNanoseconds));
        ulong request=100+DiskStaticExecutionPolicy.RequestDelayNanoseconds;
        HostCheck.Equal(LinuxStaticFileObservation.Progress.RequestTermination,o.Poll(request));HostCheck.True(o.RecordRequest(7,request));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.Draining,o.Poll(request+DiskStaticExecutionPolicy.GraceNanoseconds-1));
        HostCheck.Equal(LinuxStaticFileObservation.Progress.GraceExpired,o.Poll(request+DiskStaticExecutionPolicy.GraceNanoseconds));
        HostCheck.True(o.Failed);
    }

    [HostTest("disk full gateway counts32768 maximum completions without uint byte wrap")]
    private static void Gateway()
    {
        byte* scratch=stackalloc byte[4096];int context=0;
        LinuxSyscallAdmission a=new(){UserRoot=0x200000,MaximumCalls=32768,MaximumWrite=4096,Scratch=scratch,ScratchBytes=4096,
            FirmwareExited=true,OwnedBoundaryValidated=true,EnableStartup=true,EnableDescriptors=true,EnableThreads=true,DiskStaticFull=true};
        LinuxSyscallAccess access=new(){Context=&context,UserInterrupts=true,PageAccess=&Permissions,CopyFromUser=&Copy,Write=&Write,
            Startup=&Noop,Descriptors=&Noop,Threads=&Thread};LinuxSyscallGateState state=default;
        a.ContinuingService=true;HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission,LinuxSyscallGateway.Begin(ref state,a,access));
        a.ContinuingService=false;a.DiskStaticFull=false;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission,LinuxSyscallGateway.Begin(ref state,a,access));
        a.DiskStaticFull=true;HostCheck.Equal(LinuxSyscallGateStatus.Active,LinuxSyscallGateway.Begin(ref state,a,access));
        HostCheck.True(!LinuxSyscallGateway.RenewTurn(ref state,1));
        LinuxSyscallFrame f=new(){Number=20,Argument0=3,Argument1=0x402000,Argument2=1,Rip=0x400100,Rsp=0x70001000,Rflags=0x202};
        for(uint i=0;i<32768;i++)HostCheck.Equal(LinuxSyscallAction.Return,LinuxSyscallGateway.Dispatch(ref state,ref f).Action);
        HostCheck.Equal(32768U,state.Calls);HostCheck.Equal(2_147_483_648U,state.BytesWritten);HostCheck.Equal(2_147_483_648UL,state.TotalBytesWritten);
        _=LinuxSyscallGateway.Dispatch(ref state,ref f);HostCheck.Equal(LinuxSyscallGateStatus.CallLimit,state.Status);
        HostCheck.Equal(2_147_483_648U,state.BytesWritten);
    }

    [HostTest("disk full serial enlargement requires explicit copied input selection")]
    private static void Capture()
    {
        byte[] bytes=new byte[9*1024*1024];Array.Fill(bytes,(byte)'A');bytes[^1]=(byte)'\n';
        byte[] suffix="M16_005_WRITE fd=1 bytes=1 data=x\r\n"u8.ToArray();byte[] raw=[..bytes,..suffix];
        bool denied=false;try{_=NativeAotThreadingOutput.ReadWeb(raw);}catch(ToolFailure){denied=true;}
        HostCheck.True(denied);HostCheck.Equal("x",NativeAotThreadingOutput.ReadWeb(raw,diskStaticFull:true).Stdout);
        HostCheck.True((ulong)DiskStaticExecutionPolicy.MaximumCalls*LinuxDescriptorServices.MaximumIo<=uint.MaxValue);
    }

    [HostTest("disk full multi read bodies require every queued prefix after its supplying completion")]
    private static void BodyReads()
    {
        NativeDiskStaticReadWindows.ReadWindow R(ulong ordinal,ulong offset,uint bytes,int begin,int end)=>
            new(1,ordinal,17,2,ordinal,begin,end,99,offset,16384,8192,bytes){Descriptor=8};
        NativeDiskStaticReadWindows.ReadWindow[] reads=[R(1,0,8192,10,20),R(2,8192,8192,41,50),R(3,16384,1616,61,70)];
        QemuTranscripts.DiskStaticSocketWrite[] writes=[new(0,29,0,100),new(0,40,100,8292),new(0,60,8292,16484),new(0,80,16484,18100)];
        var joined=QemuTranscripts.InspectDiskStaticBodyReadValues(99,0,18000,1,5,100,18100,reads,writes);
        HostCheck.Equal(3,joined.Count);HostCheck.True(joined[1].BeginRecordIndex>writes[1].CallRecordIndex);
        foreach(int variant in Enumerable.Range(0,6))
        {
            var r=reads.ToArray();var w=writes.ToArray();int prior=5;
            if(variant==0)w[2]=w[2] with{CallRecordIndex=45};
            if(variant==1)r[1]=r[1] with{Offset=8193};
            if(variant==2)r=[..r,..r.Select(x=>x with{Descriptor=9})];
            if(variant==3)w=w[..^1];
            if(variant==4)prior=15;
            if(variant==5)r[0]=r[0] with{Descriptor=0};
            bool denied=false;try{_=QemuTranscripts.InspectDiskStaticBodyReadValues(99,0,18000,1,prior,100,18100,r,w);}catch(ToolFailure){denied=true;}
            HostCheck.True(denied,"body join mutation"+variant);
        }
    }

    [HostTest("disk full new connections exclude earlier phase reads only with a serial boundary")]
    private static void EarlierPhaseReads()
    {
        NativeDiskStaticReadWindows.ReadWindow R(uint descriptor,ulong generation,int begin,int end)=>
            new(1,generation,17,generation,generation,begin,end,99,0,8192,8192,8192){Descriptor=descriptor};
        NativeDiskStaticReadWindows.ReadWindow[] reads=[R(3,1,10,20),R(4,2,100,110)];
        QemuTranscripts.DiskStaticSocketWrite[] writes=[new(1,119,0,100),new(1,120,100,8292)];
        bool denied=false;
        try{_=QemuTranscripts.InspectDiskStaticBodyReadValues(99,0,8192,1,-1,100,8292,reads,writes);}
        catch(ToolFailure){denied=true;}
        HostCheck.True(denied,"earlier identical inode reads remain ambiguous without an authenticated phase boundary");
        var joined=QemuTranscripts.InspectDiskStaticBodyReadValues(99,0,8192,1,90,100,8292,reads,writes);
        HostCheck.Equal(1,joined.Count);HostCheck.Equal(4U,joined[0].Descriptor);HostCheck.Equal(2UL,joined[0].DescriptorGeneration);
        denied=false;
        try{_=QemuTranscripts.InspectDiskStaticBodyReadValues(99,0,8192,1,90,100,8292,[..reads,R(5,3,101,111)],writes);}
        catch(ToolFailure){denied=true;}
        HostCheck.True(denied,"indistinguishable reads within the same phase still fail closed");
    }

    [HostTest("disk full child dry run admits only the explicit finite readonly parent")]
    private static void FullDryRun()
    {
        var run=HostToolProcess.RunTool(["qemu","smoke","-DryRun","-Qemu","qemu-test","-OvmfCode","ovmf-test",
            "-BuildEfi","-EnableM10018FullProbe","-LinuxElfPackage","artifacts/full.pkg","-LinuxThreadingDisassembly","artifacts/full.dump",
            "-WebRootFixture","DiskA","-WebRootAssets","artifacts/full-root"]);
        HostCheck.True(run.ExitCode==0,run.Output);
        HostCheck.True(run.Output.Contains("readonly=on",StringComparison.Ordinal) && run.Output.Contains("CLROS_M10_018_READY",StringComparison.Ordinal));
    }

    [UnmanagedCallersOnly] private static byte Permissions(void* c,ulong a)=>(byte)(a==0x400000?5:3);
    [UnmanagedCallersOnly] private static byte Copy(void* c,ulong a,byte* d,uint n)=>1;
    [UnmanagedCallersOnly] private static byte Write(void* c,uint fd,byte* s,uint n)=>1;
    [UnmanagedCallersOnly] private static long Noop(void* c,LinuxSyscallFrame* f)=>-38;
    [UnmanagedCallersOnly] private static LinuxSyscallResult Thread(void* c,LinuxSyscallFrame* f)=>new(LinuxSyscallAction.Return,65536);
}
