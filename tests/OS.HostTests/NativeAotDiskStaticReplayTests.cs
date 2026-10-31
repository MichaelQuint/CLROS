using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static class NativeAotDiskStaticReplayTests
{
    private const ulong Ready=1000,Requested=Ready+LinuxStaticFileObservation.RequestDelayNanoseconds,Exited=Requested+100;
    private static void WithInventory(Action<WebRootDiskInventory.Inspection,byte[]> action)=>WebRootImageFixtureTests.WithImage((root,dir,image,source)=>
    {
        string path=Path.Combine(dir,"replay.raw");
        var receipt=WebRootImageFixture.Apply(root,image,source,WebRootImageFixture.SnapshotPublishedRoot(source),path);
        byte[] raw=File.ReadAllBytes(path);action(WebRootDiskInventory.Inspect(path,raw,receipt),raw);
    });
    private static void WithTruncated(Action<WebRootDiskInventory.VerifiedTruncated,byte[]> action)=>WebRootImageFixtureTests.WithImage((root,dir,image,source)=>
    {
        string positive=Path.Combine(dir,"positive.raw"),faultPath=Path.Combine(dir,"truncated.raw");
        var receipt=WebRootImageFixture.Apply(root,image,source,WebRootImageFixture.SnapshotPublishedRoot(source),positive);
        var fault=WebRootImageFixture.CreateFaultVariant(root,positive,receipt,faultPath,"Truncated");
        action(WebRootDiskInventory.VerifiedTruncated.Verify(root,positive,receipt,faultPath,fault),File.ReadAllBytes(faultPath));
    });

    // Independent synthetic scheduler vectors. They do not execute a native
    // app or construct original image, runner, packet or acceptance authority.
    private static List<string> Vector(WebRootDiskInventory.Inspection inventory,byte[] image,bool repark=false,bool shortPrefix=false,
        bool full=false,WebRootDiskInventory.VerifiedTruncated? truncated=null,int combinedOutputBytes=0,bool idlePoll=false)
    {
        var file=inventory.Find(WebRootDiskInventory.Root+"/index.html")!;
        ulong requested=full?Ready+DiskStaticExecutionPolicy.RequestDelayNanoseconds:Requested,exited=full?requested+100:Exited;
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)
        {
            v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=4096",StringComparison.Ordinal)
                .Replace("descriptors=16","descriptors=32",StringComparison.Ordinal).Replace("packageBytes=1320176","packageBytes=8641616",StringComparison.Ordinal)
                .Replace("workers=4","workers=16",StringComparison.Ordinal).Replace("epochs=8192 steps=16384","epochs=1000000 steps=4000000",StringComparison.Ordinal);
            if(full && v.Rows[i].StartsWith("M16_008_BUDGET ",StringComparison.Ordinal))
                v.Rows[i]=Regex.Replace(v.Rows[i],@"\bnanos=[0-9]+","nanos="+DiskStaticExecutionPolicy.RuntimeNanoseconds.ToString(CultureInfo.InvariantCulture));
        }
        v.Rows.Insert(3,"M16_011_BOUND connections=16 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        int bound=v.Rows.FindIndex(r=>r.StartsWith("M16_008_BOUND ",StringComparison.Ordinal));
        v.Rows.InsertRange(bound,["TEST_START m16.023","TEST_START m16.024","TEST_START m16.025",
            "M16_023_BOUND owner=16384 epoch=3 arena=9437184 pages=90 bar=34359771136 capacity=131072 bootstrapCompleted=37 bootstrapCopied=37 clock=0 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained"]);
        int budget=v.Rows.FindIndex(r=>r.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal));v.Rows.Insert(budget+1,"TEST_START m10.018");
        if(full)v.Rows.Insert(budget+2,"M10_018_POLICY profile=DiskStaticFullV1 version="+DiskStaticExecutionPolicy.Version+
            " calls="+DiskStaticExecutionPolicy.MaximumCalls+" requestNanos="+DiskStaticExecutionPolicy.RequestDelayNanoseconds+
            " graceNanos="+DiskStaticExecutionPolicy.GraceNanoseconds+" runtimeNanos="+DiskStaticExecutionPolicy.RuntimeNanoseconds+" renewal=none");
        v.Call(13,0,[15,0x500400,0,8]);Write(2,"CLROS_STATIC_FILES_APP_ENTRY v1\n");
        v.Call(291,5,[0x80000]);v.Call(41,6,[2,0x80001,6]);v.Call(49,0,[6,0x500200,16]);
        v.Rows.Add($"M10_018_DISK_READY submitted=37 completed=37 copied=37 clock={Ready}");
        v.Rows.Add($"M10_018_LISTENING main={v.Main.Id} writer={v.Main.Id} fd=6 nanos={Ready} http=external cleanup=pending");
        v.Call(50,0,[6,512]);v.Call(233,0,[5,1,6,0x500300]);v.Clock=Ready;
        ulong request=0,device=37;
        if(truncated is not null)
            foreach(ulong number in new[]{4UL,6UL})
            {
                Begin(number,[0x500200,0x500800]);
                v.Rows.Add($"M10_018_PATH identity={v.Main.Id} ordinal={v.Main.Ordinal} nr={number} pathHex={Convert.ToHexString(Encoding.ASCII.GetBytes(WebRootDiskInventory.VerifiedTruncated.Target))} valueBits={unchecked((ulong)-5L)} kind=0 view=4294967295 inode=0 bytes=0 mode=0");
                Complete(-5,144);
            }
        Begin(2,[0x500200,0x80100,0]);
        if(repark)
        {
            ulong old=v.Main.Sequence;v.Rows.Add($"M16_025_REPARK identity={v.Main.Id} previous={old} sequence={++v.Main.Sequence} request={request} outputBytes=0 clock={v.Clock}");
            v.Supervisor(3,0,1);Wake();
        }
        Path(2,7);File(2,0,0);Complete(7,0);
        if(shortPrefix)
        { Begin(0,[7,0x53fff0,8192]);File(0,0,0);Complete(-14,16); }
        Begin(0,[7,0x500800,8192],copy:true);File(0,0,file.Bytes);Complete(file.Bytes,8192);
        Begin(0,[7,1,8192]);File(0,file.Bytes,file.Bytes);Complete(0,0); // EOF does not require mapped output.
        File(5,file.Bytes,file.Bytes);v.Call(5,0,[7,0x500800]);
        File(3,file.Bytes,file.Bytes);v.Call(3,0,[7]);
        var first=v.Clone(2,2);var second=v.Clone(3,3);
        v.Irq(2000,3,3);v.Select(first);v.Wait(0x500208);v.Drain(2010);v.Supervisor(3,2,3);
        v.Select(second);v.Wait(0x50020c);v.Drain(2020);v.Supervisor(3,1,3);v.Select(v.Main);
        v.Call(288,7,[6,0x500700,0x500780,0x80000]);v.Call(45,1,[7,0x500800,1,0,0,0]);v.Call(44,1,[7,0x500000,1,0,0,0]);v.Call(3,0,[7]);
        v.Wait(0x500204);v.Drain(2030);v.Supervisor(3,0,3);
        v.Rows.Add($"M10_018_TERMINATE main={v.Main.Id} signal=15 origin=128 nanos={requested} tableClock={requested} owner=supervisor completion=pending");
        v.Clock=requested;v.Resume(-4);
        ulong saved=v.Main.Rsp,fp=(saved-128-512)&~63UL,frame=((fp-440)&~15UL)-8;
        v.Main.Frame=frame;v.Main.Depth=1;v.Main.Mask=1UL<<14;v.Main.Rsp=frame-64;
        v.Call(186,1);v.Call(1,1,[4,0x500400,1]);v.Main.Rsp=frame+8;v.Main.Frame=v.Main.Depth=v.Main.Mask=0;
        v.Call(15,-4,action:5,returnedRsp:saved);v.Main.Rsp=saved;v.Call(3,0,[6]);
        if(combinedOutputBytes!=0)
        {
            int remaining=combinedOutputBytes-Encoding.UTF8.GetByteCount("CLROS_STATIC_FILES_APP_ENTRY v1\n"+
                "info: independent disk static vector\n"+"CLROS_STATIC_FILES_APP_END v1\n");
            HostCheck.True(remaining>=0);
            while(remaining>0){int bytes=Math.Min(512,remaining);Write(1,new string('x',bytes));remaining-=bytes;}
        }
        Write(1,"info: independent disk static vector\n");Write(2,"CLROS_STATIC_FILES_APP_END v1\n");
        v.Rows.Add($"M10_018_APP_EXIT main={v.Main.Id} nanos={exited} exit=0 cleanup=pending");
        v.Call(231,0,[0],2);v.Drain(requested);v.Exit(2,2,3);v.Select(first);v.Exit(4,1,2);v.Select(second);v.Exit(4,0,1);
        for(int fd=0;fd<=5;fd++)v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.Clock=exited;v.End(3);int owner=v.Rows.FindIndex(r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
        v.Rows.Insert(owner+1,$"M10_018_OWNER_END main={v.Main.Id} ready={Ready} requested={requested} exited={exited} reaped=3 app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending");
        return v.Rows;

        void Write(uint channel,string text)
        { int bytes=Encoding.UTF8.GetByteCount(text);v.Rows.Add($"M16_005_WRITE fd={channel} bytes={bytes} data={text}");v.Call(1,bytes,[channel,0x500400,(uint)bytes]); }
        void Wake()=>v.Rows.Add($"M16_025_WAKE identity={v.Main.Id} sequence={v.Main.Sequence} request={request} completion=1 state=1 clock={v.Clock}");
        void Begin(ulong number,ulong[] arguments,bool copy=false)
        {
            request++;v.Call(number,0,arguments,action:3,source:4);v.Drain(v.Clock+10);v.Supervisor(3,0,1);
            if(truncated is not null && number==4)
                foreach(ulong lba in new[]{truncated.Fault.Fat0WordOffset/512,truncated.Fault.Fat1WordOffset/512})Io(lba);
            if(copy)Io(file.Sectors[0].Lba);
            if(idlePoll && request==1 && number==2)
            {
                // The observed supervisor poll returned empty. The later wake
                // belongs to the existing observe:false idle turn, not that batch.
                v.Rows.Add($"M16_012_POLL identity=0 clock={v.Clock} ready=0 idle=0");
                v.Rows.Add($"M16_012_POLL identity=0 clock={v.Clock} ready=0 idle=0");
                v.Clock++;
                Wake();
                v.Rows.Add($"M16_012_POLL identity=0 clock={v.Clock} ready=1 idle=1");
            }
            else Wake();
        }
        void Io(ulong lba)
        {
            ulong previous=device++;string data=Convert.ToHexString(image.AsSpan(checked((int)lba*512),512)).ToLowerInvariant();
            foreach(int kind in new[]{0,1,2})v.Rows.Add($"M16_023_IO kind={kind} owner=16384 epoch=3 generation={device} sector={lba} sequence={device} bytes=512 available={device} consumed={(kind==0?previous:device)} clock={v.Clock}"+(kind==2?" copied=committed data="+data:""));
        }
        void Path(ulong number,long value)=>v.Rows.Add($"M10_018_PATH identity={v.Main.Id} ordinal={v.Main.Ordinal} nr={number} pathHex={Convert.ToHexString(Encoding.ASCII.GetBytes(file.Path))} valueBits={unchecked((ulong)value)} kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} mode=33060");
        void File(ulong number,ulong before,ulong after)=>v.Rows.Add($"M10_018_FILE identity={v.Main.Id} ordinal={v.Main.Ordinal+(number is 3 or 5?1UL:0)} nr={number} fd=7 generation=1 kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} position={before} afterPosition={after}");
        void Complete(long result,uint prefix)
        { v.Rows.Add($"M16_025_COMPLETE identity={v.Main.Id} sequence={v.Main.Sequence} request={request} outputBytes={prefix} completion=1 valueBits={unchecked((ulong)result)} clock={v.Clock}");v.Resume(result); }
    }

    private static QemuTranscripts.DiskStaticReplayReport Inspect(IEnumerable<string> rows,WebRootDiskInventory.Inspection inventory,
        bool full=false,WebRootDiskInventory.VerifiedTruncated? truncated=null)=>
        QemuTranscripts.InspectNativeAotDiskStaticProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n"),r=>r==0x400202,r=>r==0x400202,8192,12288,inventory,full,truncated);
    private static string[] Change(IEnumerable<string> rows,string selector,string key,ulong value)
    {
        string[] result=rows.ToArray();int at=Array.FindIndex(result,r=>r.Contains(selector,StringComparison.Ordinal));HostCheck.True(at>=0);
        result[at]=Regex.Replace(result[at],@"\b"+key+"=[0-9]+",key+"="+value.ToString(CultureInfo.InvariantCulture));return result;
    }
    private static void Reject(IEnumerable<string> rows,WebRootDiskInventory.Inspection inventory,string? reason,
        bool full=false,WebRootDiskInventory.VerifiedTruncated? truncated=null)
    {
        string? actual=null;try{Inspect(rows,inventory,full,truncated);}catch(ToolFailure e){actual=e.Reason;}
        HostCheck.True(actual is not null && (reason is null || actual.Contains(reason,StringComparison.Ordinal)),"disk replay expected "+reason+" got "+actual);
    }

    [HostTest("verified truncated Full replay binds ready source4 stat failures mirrored cache fill healthy read and signal cleanup",HostLane.Native)]
    private static void TruncatedReadyCompletion()=>WithTruncated((truncated,image)=>
    {
        var inventory=truncated.Baseline;
        var rows=Vector(inventory,image,full:true,truncated:truncated);
        var report=Inspect(rows,inventory,true,truncated);
        HostCheck.True(report.FullProfile && report.Truncated && report.Faults.Count==2 && report.Operations.Count==5 &&
            report.Faults.Select(f=>f.Operation.Number).SequenceEqual(new[]{4UL,6UL}) &&
            report.Faults.All(f=>f.Operation.Value==-5 && f.Operation.OutputPrefix==144) &&
            report.Reads.Count==1 && report.Static.Protocol.Reaped==3 && report.Static.Signal15Returns==1 &&
            !report.CheckedDeviceBytesOrHttp && !report.GrantsOriginalExecutionAuthority);
        var sectors=QemuTranscripts.InspectNativeAotDiskStaticSectorValues(report,inventory,image,new(16384,3,37,37,40,40,40),truncated);
        HostCheck.True(sectors.Metadata.Count==2 && sectors.Metadata[0].Kind==WebRootDiskInventory.MetadataKind.Fat0 &&
            sectors.Metadata[1].Kind==WebRootDiskInventory.MetadataKind.Fat1 && !sectors.GrantsOriginalNativeProcessOrHttpAuthority);
        Reject(Change(rows," nr=4 ","waitSource",1),inventory,null,true,truncated);
        var interrupt=Change(Change(rows,"M16_025_WAKE ","completion",5),"M16_025_COMPLETE ","completion",5);
        Reject(interrupt,inventory,"interrupt_winner",true,truncated);
        Reject(rows,inventory,null,false,truncated);
        Reject(rows,inventory,"path_result",true);
        truncated.VerifyUnchanged();
    });

    [HostTest("disk static full synthetic replay joins deferred opens reads EOF and actual signal cleanup",HostLane.Native)]
    private static void Complete()=>WithInventory((inventory,image)=>
    {
        foreach(bool repark in new[]{false,true})
        {
            var rows=Vector(inventory,image,repark);var report=Inspect(rows,inventory);
            HostCheck.True(report.Static.Protocol.Reaped==3 && report.Static.Signal15Returns==1 && report.Static.Connections.Count==1);
            HostCheck.True(report.Reads.Count==1 && report.DeviceRows.Count==3 && !report.CheckedDeviceBytesOrHttp && !report.GrantsOriginalExecutionAuthority);
            var device=NativeDiskReadObservations.Inspect(report.DeviceRows.Select(r=>r.Text).ToArray(),image,new(16384,3,37,37,38,38,38));
            var join=NativeDiskStaticReadWindows.Inspect(inventory,device,report.DeviceRows.Select(r=>r.RecordIndex).ToArray(),report.Reads);
            HostCheck.True(join.Assigned.Count==1 && join.Residual.Count==0 && join.Assigned[0].Path.EndsWith("/index.html",StringComparison.Ordinal));
            var complete=QemuTranscripts.InspectNativeAotDiskStaticSectorValues(report,inventory,image,new(16384,3,37,37,38,38,38));
            HostCheck.True(complete.Metadata.Count==0 && !complete.GrantsOriginalNativeProcessOrHttpAuthority && !complete.ReconstructsFileSystemCache);
        }
        _=Inspect(Vector(inventory,image,shortPrefix:true),inventory);
    });


    // Actual receipt order is slot order; post-Advance mutation order is disk
    // readiness then descriptor readiness. These remain synthetic vectors only.
    private static List<string> NativePollBatchVector(WebRootDiskInventory.Inspection inventory,byte[] image,
        bool deadline=false,bool existingPrefix=false,bool reversedDispatch=false)
    {
        var file=inventory.Find(WebRootDiskInventory.Root+"/index.html")!;
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)
            v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=4096",StringComparison.Ordinal)
                .Replace("descriptors=16","descriptors=32",StringComparison.Ordinal)
                .Replace("packageBytes=1320176","packageBytes=8641616",StringComparison.Ordinal)
                .Replace("workers=4","workers=16",StringComparison.Ordinal)
                .Replace("epochs=8192 steps=16384","epochs=1000000 steps=4000000",StringComparison.Ordinal);
        int at=v.Rows.FindIndex(x=>x.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal));
        v.Rows[at]=Regex.Replace(v.Rows[at],@"\bnanos=[0-9]+","nanos="+DiskStaticExecutionPolicy.RuntimeNanoseconds.ToString(CultureInfo.InvariantCulture));
        v.Rows.Insert(3,"M16_011_BOUND connections=16 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        at=v.Rows.FindIndex(x=>x.StartsWith("M16_008_BOUND ",StringComparison.Ordinal));
        v.Rows.InsertRange(at,["TEST_START m16.023","TEST_START m16.024","TEST_START m16.025",
            "M16_023_BOUND owner=16384 epoch=3 arena=9437184 pages=90 bar=34359771136 capacity=131072 bootstrapCompleted=37 bootstrapCopied=37 clock=0 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained"]);
        at=v.Rows.FindIndex(x=>x.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal));
        v.Rows.InsertRange(at+1,["TEST_START m10.018",
            "M10_018_POLICY profile=DiskStaticFullV1 version="+DiskStaticExecutionPolicy.Version+
            " calls="+DiskStaticExecutionPolicy.MaximumCalls+" requestNanos="+DiskStaticExecutionPolicy.RequestDelayNanoseconds+
            " graceNanos="+DiskStaticExecutionPolicy.GraceNanoseconds+" runtimeNanos="+DiskStaticExecutionPolicy.RuntimeNanoseconds+" renewal=none"]);
        v.Call(13,0,[15,0x500400,0,8]);Write(2,"CLROS_STATIC_FILES_APP_ENTRY v1\n");
        v.Call(291,5,[0x80000]);v.Call(41,6,[2,0x80001,6]);v.Call(49,0,[6,0x500200,16]);
        v.Rows.Add($"M10_018_DISK_READY submitted=37 completed=37 copied=37 clock={Ready}");
        v.Rows.Add($"M10_018_LISTENING main={v.Main.Id} writer={v.Main.Id} fd=6 nanos={Ready} http=external cleanup=pending");
        v.Call(50,0,[6,512]);v.Call(233,0,[5,1,6,0x500300]);v.Clock=Ready;

        // Open on main before the workers exist; fd7 is then genuinely shared.
        v.Call(2,0,[0x500200,0x80100,0],3,4);v.Drain(1010);v.Supervisor(3,0,1);
        v.Rows.Add($"M16_025_WAKE identity={v.Main.Id} sequence={v.Main.Sequence} request=1 completion=1 state=1 clock={v.Clock}");
        v.Rows.Add($"M10_018_PATH identity={v.Main.Id} ordinal={v.Main.Ordinal} nr=2 pathHex={Convert.ToHexString(Encoding.ASCII.GetBytes(file.Path))} valueBits=7 kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} mode=33060");
        File(2,0,0);Complete(1,7,0);v.Resume(7);

        var socket=v.Clone(2,2);var disk=v.Clone(3,3);
        NativeAotThreadingTranscriptTests.Thread? prefix=existingPrefix?v.Clone(4,4):null;
        int active=existingPrefix?4:3;
        v.Irq(1020,active,active);
        v.Select(socket);v.Call(281,0,[5,0x500600,16,deadline?1UL:uint.MaxValue,0,8],3,3,deadline?1060UL:0);
        v.Drain(1030);v.Supervisor(3,active-1,active);
        v.Select(disk);v.Call(17,0,[7,0x500800,8192,0],3,4);
        v.Drain(1040);v.Supervisor(3,active-2,active);
        if(prefix is not null){v.Select(prefix);v.Irq(1041,2,active);}
        v.Select(v.Main);
        int readyPrefix=existingPrefix?1:0;
        v.Rows.Add($"M16_012_POLL identity={v.Main.Id} clock={v.Clock} ready={readyPrefix} idle=0");
        v.Clock=1070;
        v.Rows.Add($"M16_012_POLL identity={v.Main.Id} clock={v.Clock} ready={readyPrefix+(deadline?1:0)} idle=0");
        Io(file.Sectors[0].Lba);
        // The lower slot is serialized first, even when disk was readied first.
        v.Rows.Add($"M16_012_WAKE identity={socket.Id} sequence={socket.Sequence} completion={(deadline?2:1)} state=1 clock={v.Clock}");
        v.Rows.Add($"M16_025_WAKE identity={disk.Id} sequence={disk.Sequence} request=2 completion=1 state=1 clock={v.Clock}");
        v.Call(186,1);
        v.Irq(1071,active,active);
        if(prefix is not null)
        {
            v.Select(prefix);v.Call(186,4);v.Wait(0x500210);
            v.Drain(1072);v.Supervisor(3,3,active);
        }
        bool socketFirst=deadline!=reversedDispatch;
        if(socketFirst){FinishSocket(2);FinishDisk(1);}
        else {FinishDisk(2);FinishSocket(1);}
        v.Select(v.Main);File(3,0,0);v.Call(3,0,[7]);
        // Preserve the existing static socket/lifecycle requirements.
        v.Call(288,7,[6,0x500700,0x500780,0x80000]);
        v.Call(45,1,[7,0x500800,1,0,0,0]);v.Call(44,1,[7,0x500000,1,0,0,0]);v.Call(3,0,[7]);
        v.Wait(0x500204);v.Drain(v.Clock+1);v.Supervisor(3,0,active);
        ulong requested=Ready+DiskStaticExecutionPolicy.RequestDelayNanoseconds,exited=requested+100;
        v.Rows.Add($"M10_018_TERMINATE main={v.Main.Id} signal=15 origin=128 nanos={requested} tableClock={requested} owner=supervisor completion=pending");
        v.Clock=requested;v.Resume(-4);
        ulong saved=v.Main.Rsp,fp=(saved-128-512)&~63UL,frame=((fp-440)&~15UL)-8;
        v.Main.Frame=frame;v.Main.Depth=1;v.Main.Mask=1UL<<14;v.Main.Rsp=frame-64;
        v.Call(186,1);v.Call(1,1,[4,0x500400,1]);v.Main.Rsp=frame+8;v.Main.Frame=v.Main.Depth=v.Main.Mask=0;
        v.Call(15,-4,action:5,returnedRsp:saved);v.Main.Rsp=saved;v.Call(3,0,[6]);
        Write(1,"info: independent disk static vector\n");Write(2,"CLROS_STATIC_FILES_APP_END v1\n");
        v.Rows.Add($"M10_018_APP_EXIT main={v.Main.Id} nanos={exited} exit=0 cleanup=pending");
        v.Call(231,0,[0],2);v.Drain(requested);v.Exit(2,prefix is null?0:1,active);
        if(prefix is not null){v.Select(prefix);v.Exit(4,0,1);}
        for(int fd=0;fd<=5;fd++)v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.Clock=exited;v.End(existingPrefix?4:3);
        at=v.Rows.FindIndex(x=>x.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
        v.Rows.Insert(at+1,$"M10_018_OWNER_END main={v.Main.Id} ready={Ready} requested={requested} exited={exited} reaped={(existingPrefix?4:3)} app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending");
        return v.Rows;

        void Write(uint channel,string text)
        {int bytes=Encoding.UTF8.GetByteCount(text);v.Rows.Add($"M16_005_WRITE fd={channel} bytes={bytes} data={text}");v.Call(1,bytes,[channel,0x500400,(uint)bytes]);}
        void File(ulong number,ulong before,ulong after)
        {var t=v.Current;v.Rows.Add($"M10_018_FILE identity={t.Id} ordinal={t.Ordinal+(number is 3 or 5?1UL:0)} nr={number} fd=7 generation=1 kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} position={before} afterPosition={after}");}
        void Complete(ulong request,long result,uint output)
        {var t=v.Current;v.Rows.Add($"M16_025_COMPLETE identity={t.Id} sequence={t.Sequence} request={request} outputBytes={output} completion=1 valueBits={unchecked((ulong)result)} clock={v.Clock}");}
        void Io(ulong lba)
        {
            string data=Convert.ToHexString(image.AsSpan(checked((int)lba*512),512)).ToLowerInvariant();
            for(int kind=0;kind<3;kind++)v.Rows.Add($"M16_023_IO kind={kind} owner=16384 epoch=3 generation=38 sector={lba} sequence=38 bytes=512 available=38 consumed={(kind==0?37:38)} clock={v.Clock}"+(kind==2?" copied=committed data="+data:""));
        }
        void FinishDisk(int remainingReady)
        {
            v.Select(disk);File(17,0,0);Complete(2,checked((long)file.Bytes),8192);v.Resume(checked((long)file.Bytes));
            v.Call(60,0,[0],4);v.Drain(v.Clock+1);v.Exit(4,remainingReady,active--);
        }
        void FinishSocket(int remainingReady)
        {
            v.Select(socket);v.Rows.Add($"M16_012_COMPLETE identity={socket.Id} sequence={socket.Sequence} completion={(deadline?2:1)} valueBits={(deadline?0:1)} clock={v.Clock}");
            v.Resume(deadline?0:1);v.Call(60,0,[0],4);v.Drain(v.Clock+1);v.Exit(4,remainingReady,active--);
        }
    }

    [HostTest("empty supervisor poll pair preserves a later unobserved-idle disk wake",HostLane.Native)]
    private static void EmptySupervisorPollPair()=>WithInventory((inventory,image)=>
    {
        var rows=Vector(inventory,image,full:true,idlePoll:true);
        int first=rows.FindIndex(x=>x.StartsWith("M16_012_POLL identity=0 ",StringComparison.Ordinal));
        HostCheck.True(first>=0 && rows[first]==rows[first+1] &&
            rows[first].EndsWith(" ready=0 idle=0",StringComparison.Ordinal) &&
            rows[first+2].StartsWith("M16_025_WAKE ",StringComparison.Ordinal) &&
            rows[first+3].StartsWith("M16_012_POLL identity=0 ",StringComparison.Ordinal) &&
            rows[first+3].EndsWith(" ready=1 idle=1",StringComparison.Ordinal));
        var report=Inspect(rows,inventory,true);
        HostCheck.True(report.FullProfile && report.Static.Protocol.Reaped==3 &&
            report.Reads.Count==1 && !report.GrantsOriginalExecutionAuthority);
    });

    [HostTest("disk poll mixed wake receipts preserve native phase ordering and prior ready winners",HostLane.Native)]
    private static void NativePollBatchOrdering()=>WithInventory((inventory,image)=>
    {
        foreach(var mode in new[]{(Deadline:false,Prefix:false),(Deadline:true,Prefix:false),(Deadline:false,Prefix:true)})
        {
            var rows=NativePollBatchVector(inventory,image,mode.Deadline,mode.Prefix);
            var report=Inspect(rows,inventory,true);
            HostCheck.True(report.FullProfile && report.Static.Protocol.Reaped==(mode.Prefix?4:3) && !report.GrantsOriginalExecutionAuthority);
        }
        Reject(NativePollBatchVector(inventory,image,reversedDispatch:true),inventory,null,true);
        Reject(NativePollBatchVector(inventory,image,deadline:true,reversedDispatch:true),inventory,null,true);
        var valid=NativePollBatchVector(inventory,image);
        Reject(Change(valid,"M16_012_WAKE ","identity",0x200000002),inventory,null,true);
        Reject(Change(valid,"M16_025_WAKE identity=4294967299 ","clock",1071),inventory,null,true);
        int first=valid.FindIndex(x=>x.StartsWith("M16_012_WAKE ",StringComparison.Ordinal));
        var swapped=valid.ToArray();(swapped[first],swapped[first+1])=(swapped[first+1],swapped[first]);
        Reject(swapped,inventory,null,true);
        Reject(valid.Where(x=>!x.StartsWith("M16_012_POLL identity=4294967297 clock=1070 ",StringComparison.Ordinal)),inventory,null,true);
        var split=valid.ToList();split.Insert(first+1,"M16_012_POLL identity=4294967297 clock=1070 ready=1 idle=0");
        Reject(split,inventory,null,true);
    });

    [HostTest("disk replay shares explicit Full decoded-output allowance and preserves ordinary bound",HostLane.Native)]
    private static void CombinedOutputBoundaries()=>WithInventory((inventory,image)=>
    {
        foreach(var policy in new[]{(Full:false,Bytes:65536),(Full:true,Bytes:131072)})
        {
            var rows=Vector(inventory,image,full:policy.Full,combinedOutputBytes:policy.Bytes);
            var report=Inspect(rows,inventory,policy.Full);
            HostCheck.Equal(policy.Full,report.FullProfile);
            HostCheck.True(report.Static.Protocol.Reaped==3 && !report.GrantsOriginalExecutionAuthority);
            Reject(Vector(inventory,image,full:policy.Full,combinedOutputBytes:policy.Bytes+1),inventory,"raw_size",policy.Full);
        }
        _=Inspect(Vector(inventory,image,full:true,combinedOutputBytes:65537),inventory,true);
    });

    [HostTest("disk static replay rejects forged wake completion and original writable prefix",HostLane.Native)]
    private static void WaitOwnership()=>WithInventory((inventory,image)=>
    {
        var rows=Vector(inventory,image);_=Inspect(rows,inventory);
        Reject(Change(rows,"M16_025_WAKE ","identity",0x100000002),inventory,"wake_owner");
        Reject(Change(rows,"M16_025_WAKE ","sequence",2),inventory,"wake_owner");
        Reject(Change(rows,"M16_025_WAKE ","state",3),inventory,"wake_owner");
        Reject(Change(rows,"M16_025_COMPLETE ","request",2),inventory,"complete_owner");
        Reject(Change(rows,"outputBytes=8192 completion=1","outputBytes",8191),inventory,"complete_owner");
        Reject(Change(rows," nr=2 ","a1",1),inventory,"blocked_immediate_admission");
        Reject(rows.Where(r=>!r.StartsWith("M16_025_WAKE ",StringComparison.Ordinal)),inventory,"socket_no_unowned_idle");
    });

    [HostTest("disk static replay binds physical inode descriptor generation and deferred original ordinal",HostLane.Native)]
    private static void PhysicalOwnership()=>WithInventory((inventory,image)=>
    {
        var rows=Vector(inventory,image);_=Inspect(rows,inventory);
        Reject(Change(rows,"M10_018_PATH ","inode",17),inventory,"path_metadata");
        Reject(Change(rows,"M10_018_FILE ","generation",2),inventory,"file_generation");
        Reject(Change(rows,"M10_018_PATH ","ordinal",1),inventory,"path_binding");
        string original=Convert.ToHexString(Encoding.ASCII.GetBytes(WebRootDiskInventory.Root+"/index.html"));
        Reject(rows.Select(r=>r.Replace("pathHex="+original,"pathHex="+Convert.ToHexString("/proc/meminfo"u8),StringComparison.Ordinal)),inventory,"pending_path_route");
        Reject(rows.Where(r=>!r.StartsWith("M10_018_FILE ",StringComparison.Ordinal)),inventory,"file_binding");
        Reject(Change(rows,"M10_018_DISK_READY ","copied",36),inventory,"drained_readiness");
    });

    [HostTest("disk static replay reparks same request with a fresh wait token and keeps old staged reader strict",HostLane.Native)]
    private static void ReparkOwnership()=>WithInventory((inventory,image)=>
    {
        var rows=Vector(inventory,image,repark:true);_=Inspect(rows,inventory);
        Reject(Change(rows,"M16_025_REPARK ","sequence",1),inventory,"repark_owner");
        Reject(Change(rows,"M16_025_REPARK ","request",2),inventory,"repark_owner");
        bool rejected=false;try{QemuTranscripts.InspectNativeAotStaticFileProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n"),_=>true,_=>true,8192,12288);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected);
    });

    [HostTest("disk static sector join confines residual metadata to actual pending intervals and exact image",HostLane.Native)]
    private static void MetadataIntervals()=>WithInventory((inventory,image)=>
    {
        var rows=Vector(inventory,image);
        for(int i=0;i<rows.Count;i++)if(rows[i].StartsWith("M16_023_IO ",StringComparison.Ordinal))
            rows[i]=Regex.Replace(rows[i],@"\b(generation|sequence|available|consumed)=([0-9]+)",m=>m.Groups[1].Value+"="+(ulong.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)+1));
        int firstWake=rows.FindIndex(r=>r.StartsWith("M16_025_WAKE ",StringComparison.Ordinal));
        ulong lba=inventory.DirectorySectors[0].Lba;string data=Convert.ToHexString(image.AsSpan((int)lba*512,512)).ToLowerInvariant();
        string[] metadata=Enumerable.Range(0,3).Select(kind=>$"M16_023_IO kind={kind} owner=16384 epoch=3 generation=38 sector={lba} sequence=38 bytes=512 available=38 consumed={(kind==0?37:38)} clock=1010"+(kind==2?" copied=committed data="+data:"")).ToArray();
        rows.InsertRange(firstWake,metadata);
        var report=Inspect(rows,inventory);
        var joined=QemuTranscripts.InspectNativeAotDiskStaticSectorValues(report,inventory,image,new(16384,3,37,37,39,39,39));
        HostCheck.True(joined.Metadata.Count==1 && joined.Metadata[0].Kind==WebRootDiskInventory.MetadataKind.Directory);
        var bad=report with {Operations=report.Operations.Select(o=>o.Number==2?o with {BeginRecordIndex=o.BeginRecordIndex+5}:o).ToArray()};
        bool interval=false;try{QemuTranscripts.InspectNativeAotDiskStaticSectorValues(bad,inventory,image,new(16384,3,37,37,39,39,39));}
        catch(ToolFailure e){interval=e.Reason=="disk_static_metadata_pending_interval";}HostCheck.True(interval);
        byte[] changed=image.ToArray();changed[^1]^=1;
        bool identity=false;try{QemuTranscripts.InspectNativeAotDiskStaticSectorValues(report,inventory,changed,new(16384,3,37,37,39,39,39));}
        catch(ToolFailure e){identity=e.Reason=="webroot_disk_inventory_metadata_image";}HostCheck.True(identity);
        var early=Vector(inventory,image);
        int wake=early.FindIndex(r=>r.StartsWith("M16_025_WAKE ",StringComparison.Ordinal) && r.Contains(" request=2 ",StringComparison.Ordinal));
        string wakeRow=early[wake];early.RemoveAt(wake);int io=early.FindIndex(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal));early.Insert(io,wakeRow);
        var earlyReport=Inspect(early,inventory);bool ready=false;
        try{QemuTranscripts.InspectNativeAotDiskStaticSectorValues(earlyReport,inventory,image,new(16384,3,37,37,38,38,38));}
        catch(ToolFailure e){ready=e.Reason=="disk_static_data_ready_interval";}HostCheck.True(ready);
    });
}
