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

    // Independent synthetic scheduler vectors. They do not execute a native
    // app or construct original image, runner, packet or acceptance authority.
    private static List<string> Vector(WebRootDiskInventory.Inspection inventory,byte[] image,bool repark=false,bool shortPrefix=false)
    {
        var file=inventory.Find(WebRootDiskInventory.Root+"/index.html")!;
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=4096",StringComparison.Ordinal)
            .Replace("descriptors=16","descriptors=32",StringComparison.Ordinal).Replace("packageBytes=1320176","packageBytes=8641616",StringComparison.Ordinal)
            .Replace("workers=4","workers=16",StringComparison.Ordinal).Replace("epochs=8192 steps=16384","epochs=1000000 steps=4000000",StringComparison.Ordinal);
        v.Rows.Insert(3,"M16_011_BOUND connections=16 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        int bound=v.Rows.FindIndex(r=>r.StartsWith("M16_008_BOUND ",StringComparison.Ordinal));
        v.Rows.InsertRange(bound,["TEST_START m16.023","TEST_START m16.024","TEST_START m16.025",
            "M16_023_BOUND owner=16384 epoch=3 arena=9437184 pages=90 bar=34359771136 capacity=131072 bootstrapCompleted=37 bootstrapCopied=37 clock=0 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained"]);
        int budget=v.Rows.FindIndex(r=>r.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal));v.Rows.Insert(budget+1,"TEST_START m10.018");
        v.Call(13,0,[15,0x500400,0,8]);Write(2,"CLROS_STATIC_FILES_APP_ENTRY v1\n");
        v.Call(291,5,[0x80000]);v.Call(41,6,[2,0x80001,6]);v.Call(49,0,[6,0x500200,16]);
        v.Rows.Add($"M10_018_DISK_READY submitted=37 completed=37 copied=37 clock={Ready}");
        v.Rows.Add($"M10_018_LISTENING main={v.Main.Id} writer={v.Main.Id} fd=6 nanos={Ready} http=external cleanup=pending");
        v.Call(50,0,[6,512]);v.Call(233,0,[5,1,6,0x500300]);v.Clock=Ready;
        ulong request=0;
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
        v.Rows.Add($"M10_018_TERMINATE main={v.Main.Id} signal=15 origin=128 nanos={Requested} tableClock={Requested} owner=supervisor completion=pending");
        v.Clock=Requested;v.Resume(-4);
        ulong saved=v.Main.Rsp,fp=(saved-128-512)&~63UL,frame=((fp-440)&~15UL)-8;
        v.Main.Frame=frame;v.Main.Depth=1;v.Main.Mask=1UL<<14;v.Main.Rsp=frame-64;
        v.Call(186,1);v.Call(1,1,[4,0x500400,1]);v.Main.Rsp=frame+8;v.Main.Frame=v.Main.Depth=v.Main.Mask=0;
        v.Call(15,-4,action:5,returnedRsp:saved);v.Main.Rsp=saved;v.Call(3,0,[6]);
        Write(1,"info: independent disk static vector\n");Write(2,"CLROS_STATIC_FILES_APP_END v1\n");
        v.Rows.Add($"M10_018_APP_EXIT main={v.Main.Id} nanos={Exited} exit=0 cleanup=pending");
        v.Call(231,0,[0],2);v.Drain(Requested);v.Exit(2,2,3);v.Select(first);v.Exit(4,1,2);v.Select(second);v.Exit(4,0,1);
        for(int fd=0;fd<=5;fd++)v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.Clock=Exited;v.End(3);int owner=v.Rows.FindIndex(r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
        v.Rows.Insert(owner+1,$"M10_018_OWNER_END main={v.Main.Id} ready={Ready} requested={Requested} exited={Exited} reaped=3 app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending");
        return v.Rows;

        void Write(uint channel,string text)
        { int bytes=Encoding.UTF8.GetByteCount(text);v.Rows.Add($"M16_005_WRITE fd={channel} bytes={bytes} data={text}");v.Call(1,bytes,[channel,0x500400,(uint)bytes]); }
        void Wake()=>v.Rows.Add($"M16_025_WAKE identity={v.Main.Id} sequence={v.Main.Sequence} request={request} completion=1 state=1 clock={v.Clock}");
        void Begin(ulong number,ulong[] arguments,bool copy=false)
        {
            request++;v.Call(number,0,arguments,action:3,source:4);v.Drain(v.Clock+10);v.Supervisor(3,0,1);
            if(copy)
            {
                ulong lba=file.Sectors[0].Lba;string data=Convert.ToHexString(image.AsSpan((int)lba*512,512)).ToLowerInvariant();
                foreach(int kind in new[]{0,1,2})v.Rows.Add($"M16_023_IO kind={kind} owner=16384 epoch=3 generation=38 sector={lba} sequence=38 bytes=512 available=38 consumed={(kind==0?37:38)} clock={v.Clock}"+(kind==2?" copied=committed data="+data:""));
            }
            Wake();
        }
        void Path(ulong number,long value)=>v.Rows.Add($"M10_018_PATH identity={v.Main.Id} ordinal={v.Main.Ordinal} nr={number} pathHex={Convert.ToHexString(Encoding.ASCII.GetBytes(file.Path))} valueBits={unchecked((ulong)value)} kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} mode=33060");
        void File(ulong number,ulong before,ulong after)=>v.Rows.Add($"M10_018_FILE identity={v.Main.Id} ordinal={v.Main.Ordinal+(number is 3 or 5?1UL:0)} nr={number} fd=7 generation=1 kind=5 view=4294967295 inode={file.Inode} bytes={file.Bytes} position={before} afterPosition={after}");
        void Complete(long result,uint prefix)
        { v.Rows.Add($"M16_025_COMPLETE identity={v.Main.Id} sequence={v.Main.Sequence} request={request} outputBytes={prefix} completion=1 valueBits={unchecked((ulong)result)} clock={v.Clock}");v.Resume(result); }
    }

    private static QemuTranscripts.DiskStaticReplayReport Inspect(IEnumerable<string> rows,WebRootDiskInventory.Inspection inventory)=>
        QemuTranscripts.InspectNativeAotDiskStaticProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n"),r=>r==0x400202,r=>r==0x400202,8192,12288,inventory);
    private static string[] Change(IEnumerable<string> rows,string selector,string key,ulong value)
    {
        string[] result=rows.ToArray();int at=Array.FindIndex(result,r=>r.Contains(selector,StringComparison.Ordinal));HostCheck.True(at>=0);
        result[at]=Regex.Replace(result[at],@"\b"+key+"=[0-9]+",key+"="+value.ToString(CultureInfo.InvariantCulture));return result;
    }
    private static void Reject(IEnumerable<string> rows,WebRootDiskInventory.Inspection inventory,string reason)
    {
        string? actual=null;try{Inspect(rows,inventory);}catch(ToolFailure e){actual=e.Reason;}
        HostCheck.True(actual is not null && actual.Contains(reason,StringComparison.Ordinal),"disk replay expected "+reason+" got "+actual);
    }

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
