using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskFileProofFixture;

internal static class LinuxDiskCaptureTests
{
    [HostTest("Progress copied full capture joins independent sectors and owned child syscall entry inside a pending main device read",HostLane.Native)]
    private static void ProgressCapture()=>WithCapture(c=>
    {
        byte[] package=LinuxDiskRaceProofFixture.CreatePackage("Progress"),saved=package.ToArray();
        string[] rows=BuildProgressRows(c),before=rows.ToArray();
        var report=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
        var file=WebRootDiskInventory.Inspect(c.Path,c.Image,c.Receipt).Find("/app/wwwroot/fixture/fragmented.bin")!;
        foreach(var expected in new[]{(Role:"main.read",Offset:0UL,Bytes:8192U),(Role:"child.duplicate",Offset:8192UL,Bytes:8U),
            (Role:"main.pread",Offset:131000UL,Bytes:16U),(Role:"child.pread",Offset:65535UL,Bytes:512U)})
        {
            var request=report.Requests.Single(r=>r.Role==expected.Role);
            HostCheck.Equal(expected.Offset,request.Offset);HostCheck.Equal(expected.Bytes,request.Bytes);
            HostCheck.True(request.Sectors.SequenceEqual(file.Cover(expected.Offset,expected.Bytes).Select(s=>s.Lba)));
            HostCheck.Equal(request.Sectors.Count,request.DeviceGenerations.Count);
        }
        HostCheck.Equal(LinuxDiskRaceProofFixture.Calls("Progress").Concat(LinuxDiskRaceProofFixture.Calls("Progress",true))
            .Count(call=>call.Wait==LinuxDiskRaceProofFixture.WaitPolicy.Disk),report.Requests.Count);
        ulong[] generations=report.Requests.SelectMany(r=>r.DeviceGenerations).ToArray();
        HostCheck.Equal(report.Device.Rows.Count/3,generations.Length);HostCheck.Equal(generations.Length,generations.Distinct().Count());
        HostCheck.Equal(rows.Count(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)),report.Device.Rows.Count);
        var main=report.Requests.Single(r=>r.Role=="main.read");int first=Array.IndexOf(rows,"TEST_START m16-009");
        int submit=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=0 ",StringComparison.Ordinal)&&Field(r,"generation")==main.DeviceGenerations[0])-first;
        int validated=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=1 ",StringComparison.Ordinal)&&Field(r,"generation")==main.DeviceGenerations[0])-first;
        var child=report.DevicePendingProgress.Single(call=>call.Spec.Role=="child.tid");
        ulong entry=Field(rows[first+child.RecordIndex+1],"entered")*1000;
        HostCheck.True(Field(rows[first+submit],"clock")<entry&&entry<Field(rows[first+validated],"clock"));
        var duplicate=report.Requests.Single(r=>r.Role=="child.duplicate");
        HostCheck.True(Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=0 ",StringComparison.Ordinal)&&
            Field(r,"generation")==duplicate.DeviceGenerations[0])-first>report.Leaf.MainReadCompletionIndex);
        HostCheck.True(!report.CheckedOriginalExecution&&!report.CheckedSchedulerReplay&&!report.ReplayedArbitraryUserMemory);
        HostCheck.Equal(c.Receipt.FinalSha256,report.ImageSha256);HostCheck.True(rows.SequenceEqual(before)&&package.SequenceEqual(saved));
        bool immutable=false;try{((IList<ulong>)main.DeviceGenerations)[0]=0;}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
        Array.Fill(rows,"changed");Array.Clear(package);HostCheck.True(main.DeviceGenerations[0]!=0&&child.Spec.Role=="child.tid");
    });

    [HostTest("Progress entry timing survives delayed serialization but requires strict main request clock boundaries",HostLane.Native)]
    private static void ProgressEntryClock()=>WithCapture(c=>
    {
        string[] rows=BuildProgressRows(c);byte[] package=LinuxDiskRaceProofFixture.CreatePackage("Progress");
        var report=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
        ulong generation=report.Requests.Single(r=>r.Role=="main.read").DeviceGenerations[0];
        int submit=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=0 ",StringComparison.Ordinal)&&Field(r,"generation")==generation);
        int validated=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=1 ",StringComparison.Ordinal)&&Field(r,"generation")==generation);
        int timing=Index(rows,"M16_008_SYSCALL_TIME ");
        var delayed=rows.ToList();string[] completion=delayed.GetRange(validated,2).ToArray();delayed.RemoveRange(validated,2);
        delayed.InsertRange(timing-1,completion);
        var observed=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(delayed.ToArray(),package,c.Image,c.Receipt,c.Path);
        HostCheck.True(observed.DevicePendingProgress.Single().RecordIndex>
            delayed.FindIndex(r=>r==completion[0])-delayed.IndexOf("TEST_START m16-009"));

        // Align the physical endpoints to the fixture HPET quantum so exact
        // equality, as well as either side, is tested without rounding aliases.
        ulong low=(Field(rows[submit],"clock")/1000+1)*1000;
        ulong high=Field(rows[validated],"clock")/1000*1000;
        string[] aligned=Change(Change(rows,submit,"clock",low),validated,"clock",high);
        foreach(ulong entry in new[]{low-1000,low,high,high+1000})
        {
            string[] boundary=aligned.ToArray();boundary[timing]=TimingAt(boundary[timing],entry/1000);
            RejectProgress(c,boundary,"disk_progress_device_overlap");
        }
        // A long prelude crossing a transaction is not an entry witness.
        string[] duration=aligned.ToArray();duration[timing]=TimingAt(duration[timing],low/1000-1);
        foreach(string field in new[]{"admitted","dispatched","prepared"})duration[timing]=Set(duration[timing],field,high/1000+1);
        RejectProgress(c,duration,"disk_progress_device_overlap");

        // Entry inside another request's physical transaction cannot stand in
        // for the uniquely owned main.read interval, even with intact sectors.
        ulong foreign=report.Requests.Single(r=>r.Role=="main.pread").DeviceGenerations[0];
        int foreignSubmit=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=0 ",StringComparison.Ordinal)&&Field(r,"generation")==foreign);
        ulong foreignClock=Field(rows[foreignSubmit],"clock");string[] other=rows.ToArray();
        for(int i=foreignSubmit;i<other.Length;i++)
            if(other[i].StartsWith("M16_023_IO ",StringComparison.Ordinal))
                other[i]=Set(other[i],"clock",Field(other[i],"clock")+(i==foreignSubmit?0UL:i==foreignSubmit+1?1000UL:i==foreignSubmit+2?1001UL:2000UL));
        other[timing]=TimingAt(other[timing],foreignClock/1000+1);
        NativeDiskReadObservations.Inspect(other.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)).ToArray(),c.Image,report.Device.Counters);
        RejectProgress(c,other,"disk_progress_device_overlap");
    });

    [HostTest("Progress rejects absent malformed detached duplicated and foreign syscall timing even after a valid witness",HostLane.Native)]
    private static void ProgressEntryIdentity()=>WithCapture(c=>
    {
        string[] rows=BuildProgressRows(c);int timing=Index(rows,"M16_008_SYSCALL_TIME ");
        RejectProgress(c,rows.Where((_,i)=>i!=timing).ToArray(),"disk_progress_device_overlap");
        foreach(string field in new[]{"identity","ordinal","nr","periodFs"})
            RejectProgress(c,Change(rows,timing,field,Field(rows[timing],field)+1));
        RejectProgress(c,[..rows[..timing],rows[timing],..rows[timing..]],"disk_race_timing_order");
        RejectProgress(c,Change(rows,timing,"prepared",Field(rows[timing],"entered")-1),"linux_syscall_timing_observation");
        RejectProgress(c,Change(rows,timing,"entered",0),"disk_progress_syscall_timing");
        string[] late=rows.ToArray();late[timing]=Set(late[timing],"prepared",ulong.MaxValue);
        RejectProgress(c,late,"disk_progress_syscall_timing");
        int later=Array.FindIndex(rows,timing+1,r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal));
        RejectProgress(c,[..rows[..(later+1)],rows[timing],..rows[(later+1)..]],"linux_syscall_timing_observation");
    });

    private static string TimingAt(string row,ulong ticks)
    {
        foreach(string field in new[]{"entered","admitted","dispatched","prepared"})row=Set(row,field,ticks);
        return row;
    }

    [HostTest("Progress copied capture rejects shared cursor submission before main commit despite valid whole device transactions",HostLane.Native)]
    private static void ProgressSharedCursor()=>WithCapture(c=>
    {
        string[] rows=BuildProgressRows(c);byte[] package=LinuxDiskRaceProofFixture.CreatePackage("Progress");
        var report=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
        ulong generation=report.Requests.Single(r=>r.Role=="child.duplicate").DeviceGenerations.Single();
        var earlier=rows.ToList();var transaction=earlier.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)&&Field(r,"generation")==generation).ToArray();
        HostCheck.Equal(3,transaction.Length);earlier.RemoveAll(r=>transaction.Contains(r,StringComparer.Ordinal));
        int completion=Array.IndexOf(rows,"TEST_START m16-009")+report.Leaf.MainReadCompletionIndex;
        string mainComplete=rows[completion];int at=earlier.IndexOf(mainComplete);ulong clock=Field(mainComplete,"clock");
        // Keep the device sequence and bytes valid while moving all three rows
        // before the parent cursor commit; CALL/COMPLETE order is unchanged.
        earlier.InsertRange(at,transaction.Select(r=>Set(r,"clock",clock)));
        NativeDiskReadObservations.Inspect(earlier.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)).ToArray(),c.Image,report.Device.Counters);
        RejectProgress(c,earlier.ToArray(),"disk_progress_shared_cursor_commit");
    });

    [HostTest("Progress copied capture rejects corrupt bytes misassigned whole requests missing rows and unowned metadata",HostLane.Native)]
    private static void ProgressTransactions()=>WithCapture(c=>
    {
        string[] rows=BuildProgressRows(c);byte[] package=LinuxDiskRaceProofFixture.CreatePackage("Progress");
        var report=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
        ulong generation=report.Requests.Single(r=>r.Role=="main.read").DeviceGenerations[0];
        int copy=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=2 ",StringComparison.Ordinal)&&Field(r,"generation")==generation);
        var corrupt=rows.ToArray();int data=corrupt[copy].IndexOf(" data=",StringComparison.Ordinal)+6;
        corrupt[copy]=corrupt[copy][..data]+(corrupt[copy][data]=='0'?'1':'0')+corrupt[copy][(data+1)..];RejectProgress(c,corrupt,"image_bytes");
        var inventory=WebRootDiskInventory.Inspect(c.Path,c.Image,c.Receipt);
        string[] wrong=rows.Select(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)&&Field(r,"generation")==generation?
            IoSector(r,inventory.DirectorySectors[0].Lba,c.Image):r).ToArray();
        NativeDiskReadObservations.Inspect(wrong.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)).ToArray(),c.Image,report.Device.Counters);
        RejectProgress(c,wrong,"disk_progress_transaction_owner");
        int submit=Array.FindIndex(rows,r=>r.StartsWith("M16_023_IO kind=0 ",StringComparison.Ordinal)&&Field(r,"generation")==generation);
        RejectProgress(c,rows.Where((_,i)=>i!=submit).ToArray(),"request_identity");
        var noProgress=rows.ToList();string delayed=noProgress[submit];noProgress.RemoveAt(submit);
        int validation=noProgress.FindIndex(r=>r.StartsWith("M16_023_IO kind=1 ",StringComparison.Ordinal)&&Field(r,"generation")==generation);
        noProgress.Insert(validation,Set(delayed,"clock",Field(noProgress[validation],"clock")));
        NativeDiskReadObservations.Inspect(noProgress.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)).ToArray(),c.Image,report.Device.Counters);
        RejectProgress(c,noProgress.ToArray(),"disk_progress_device_overlap");
        ulong metadata=report.Requests.Single(r=>r.Role=="prep.original").DeviceGenerations.Single();
        ulong asset=inventory.Find("/app/wwwroot/fixture/fragmented.bin")!.Cover(0,1)[0].Lba;
        string[] unowned=rows.Select(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)&&Field(r,"generation")==metadata?IoSector(r,asset,c.Image):r).ToArray();
        NativeDiskReadObservations.Inspect(unowned.Where(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)).ToArray(),c.Image,report.Device.Counters);
        RejectProgress(c,unowned,"metadata_asset");
    });

    [HostTest("Progress copied full capture rejects wrong package startup IRQ policy and detached original rows",HostLane.Native)]
    private static void ProgressAdmission()=>WithCapture(c=>
    {
        string[] rows=BuildProgressRows(c);QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,
            LinuxDiskRaceProofFixture.CreatePackage("Progress"),c.Image,c.Receipt,c.Path);
        RejectProgress(c,rows,"disk_progress_capture_package",LinuxDiskRaceProofFixture.CreatePackage("CloseReuse"));
        byte[] changed=LinuxDiskRaceProofFixture.CreatePackage("Progress");changed[^1]^=1;RejectProgress(c,rows,"disk_race_package_identity",changed);
        RejectProgress(c,Change(rows,Index(rows,"M16_004_TABLES "),"ranges",64),"m16_004_table_geometry");
        RejectProgress(c,rows.Select(r=>r.Replace("timer=idle-only","timer=user-irq-and-idle",StringComparison.Ordinal)).ToArray());
        int first=Array.IndexOf(rows,"TEST_START m16-009"),io=Index(rows,"M16_023_IO ");
        RejectProgress(c,[..rows[..first],rows[io],..rows[first..]]);
        RejectProgress(c,[..rows,"unowned metadata"]);
    });

    private static void RejectProgress(Capture c,string[] rows,string? reason=null,byte[]? package=null)
    {
        string? actual=null;try{_=QemuTranscripts.InspectLinuxDiskProgressCaptureValues(rows,
            package??LinuxDiskRaceProofFixture.CreatePackage("Progress"),c.Image,c.Receipt,c.Path);}
        catch(ToolFailure error){actual=error.Reason;}
        HostCheck.True(actual is not null&&(reason is null||actual.Contains(reason,StringComparison.Ordinal)),reason+" actual="+actual);
    }

    // Reuse the complete parent and machine-code-derived Progress leaf. Only
    // copied observation values are supplied; no device or guest is executed.
    private static string[] BuildProgressRows(Capture c)
    {
        var inventory=WebRootDiskInventory.Inspect(c.Path,c.Image,c.Receipt);
        var file=inventory.Find("/app/wwwroot/fixture/fragmented.bin")!;
        byte[] package=LinuxDiskRaceProofFixture.CreatePackage("Progress");var view=LinuxElfPackage.Validate(package);
        var shell=c.Rows.ToList();int first=shell.IndexOf("TEST_START m16-009"),last=shell.IndexOf("TEST_PASS m16-009");shell.RemoveRange(first,last-first+1);
        void Replace(string prefix,string value)=>shell[shell.FindIndex(r=>r.StartsWith(prefix,StringComparison.Ordinal))]=value;
        Replace("M16_002_STAGED ",$"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length+4095)/4096} handlesClosed=2 reads=1");
        Replace("M16_002_DIGEST ","M16_002_DIGEST "+Convert.ToHexString(SHA256.HashData(package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes))).ToLowerInvariant());
        Replace("M16_002_PACKAGE_DIGEST ","M16_002_PACKAGE_DIGEST "+Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant());
        string[] startup=LinuxDiskParentTests.MappedStartupRows(package,65)[^7..];startup[3]=Set(startup[3],"root",2097152);startup[4]=Set(startup[4],"old",1048576);
        int startupAt=shell.IndexOf("TEST_START m16-004");shell.RemoveRange(startupAt,7);shell.InsertRange(startupAt,startup);
        var pending=new Dictionary<(ulong Identity,ulong Sequence),LinuxDiskRaceProofFixture.CallSpec>();
        ulong generation=37,mainGeneration=0,duplicateClock=0;bool duplicateCopied=false;List<string> leaf=[];
        var main=LinuxDiskRaceProofFixture.Calls("Progress").Single(call=>call.Role=="main.read");
        var duplicate=LinuxDiskRaceProofFixture.Calls("Progress",true).Single(call=>call.Role=="child.duplicate");
        var mainSectors=file.Cover(0,(uint)main.Result);
        foreach(string original in LinuxDiskRaceTranscriptTests.Synthetic("Progress"))
        {
            string row=Regex.Replace(original,@"\b(clock|nanos|started)=([0-9]+)",m=>m.Groups[1].Value+"="+(10_000_000UL+ulong.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*1000).ToString(CultureInfo.InvariantCulture));
            if(original.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal))row=Set(row,"nanos",Field(original,"nanos"));
            if(row.StartsWith("M16_023_BOUND ",StringComparison.Ordinal))row=$"M16_023_BOUND owner=8388608 epoch=5562286797237662465 arena=9437184 pages=90 bar=4026531840 capacity={c.Image.Length/512} bootstrapCompleted=37 bootstrapCopied=37 clock=10001000 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
            if(row.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&Field(row,"waitSource")==4)
            {
                ulong identity=Field(row,"identity");var spec=LinuxDiskRaceProofFixture.Calls("Progress",identity==LinuxDiskRaceProofFixture.ChildIdentity)[checked((int)Field(row,"ordinal")-1)];
                pending.Add((identity,Field(row,"waitSequence")),spec);leaf.Add(row);
                if(spec.Role=="main.read")Submit(mainSectors[0].Lba,Field(row,"clock")+1);
                continue;
            }
            if(row.StartsWith("M16_025_COMPLETE ",StringComparison.Ordinal))
            {
                var spec=pending[(Field(row,"identity"),Field(row,"sequence"))];ulong clock=Field(row,"clock");
                if(spec.Role is "prep.original" or "prep.independent")Emit(inventory.DirectorySectors[0].Lba,clock-3);
                else if(spec.Role=="main.read")
                {
                    ulong at=clock-(ulong)(2+3*(mainSectors.Count-1));Finish(mainGeneration,mainSectors[0].Lba,at);at+=2;
                    foreach(var sector in mainSectors.Skip(1)){Emit(sector.Lba,at);at+=3;}
                    leaf.Add(row);duplicateClock=clock+1;continue;
                }
                else if(spec.Role=="child.duplicate")HostCheck.True(duplicateCopied);
                else if(spec.Number==17)
                {
                    var sectors=file.Cover(spec.A3,(uint)spec.Result);ulong at=clock-(ulong)sectors.Count*3;
                    foreach(var sector in sectors){Emit(sector.Lba,at);at+=3;}
                }
            }
            leaf.Add(row);
            if(row.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&
                Field(row,"identity")==LinuxDiskRaceProofFixture.ChildIdentity&&Field(row,"ordinal")==1)
            {
                ulong ticks=Field(row,"clock")/1000;
                leaf.Add($"M16_008_SYSCALL_TIME identity={Field(row,"identity")} ordinal=1 nr=186 entered={ticks} admitted={ticks} dispatched={ticks} prepared={ticks} periodFs=1000000000");
            }
            if(row.StartsWith("M16_008_RESUME ",StringComparison.Ordinal)&&duplicateClock!=0)
            {
                Emit(file.Cover((ulong)main.Result,(uint)duplicate.Result)[0].Lba,duplicateClock);
                duplicateCopied=true;duplicateClock=0;
            }
        }
        shell.InsertRange(first,leaf);
        Replace("M16_025_END ",$"M16_025_END submitted={generation} completed={generation} copied={generation} bootstrapCompleted=37 bootstrapCopied=37 fsReads={generation-34} cacheHits=20 cacheMisses=1 device=retired fs=retired dma=zero descriptors=closed reservation=retained");
        return shell.ToArray();
        string Common(ulong gen,ulong sector)=>$"owner=8388608 epoch=5562286797237662465 generation={gen} sector={sector} sequence={gen} bytes=512 available={gen}";
        void Submit(ulong sector,ulong clock){mainGeneration=++generation;leaf.Add($"M16_023_IO kind=0 {Common(generation,sector)} consumed={generation-1} clock={clock}");}
        void Finish(ulong gen,ulong sector,ulong clock)
        {
            leaf.Add($"M16_023_IO kind=1 {Common(gen,sector)} consumed={gen} clock={clock}");
            leaf.Add($"M16_023_IO kind=2 {Common(gen,sector)} consumed={gen} clock={clock+1} copied=committed data={Convert.ToHexString(c.Image.AsSpan(checked((int)sector*512),512)).ToLowerInvariant()}");
        }
        void Emit(ulong sector,ulong clock){ulong gen=++generation;leaf.Add($"M16_023_IO kind=0 {Common(gen,sector)} consumed={gen-1} clock={clock}");Finish(gen,sector,clock+1);}
    }

    [HostTest("exit pending capture joins full parents independent inode metadata and exact abandoned data prefix",HostLane.Native)]
    private static void ExitPendingCapture()=>WithCapture(c=>
    {
        foreach(int copied in new[]{0,2,15})
        {
            string[] rows=BuildExitRows(c,copied),before=rows.ToArray();
            byte[] package=LinuxDiskRaceProofFixture.CreatePackage("ExitPending");
            var report=QemuTranscripts.InspectLinuxDiskExitPendingCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
            HostCheck.True(!report.CheckedOriginalExecution && report.Leaf.CheckedSchedulerReplay && rows.SequenceEqual(before));
            HostCheck.Equal(copied,report.CopiedDataSectors.Count);
            HostCheck.Equal(c.Receipt.FinalSha256,report.ImageSha256);
            HostCheck.True(report.Pending.Stages.Records[7].Values["clock"]>Field(rows[Index(rows,"M16_008_BUDGET_END ")],"nanos"));
            HostCheck.True(report.Pending.Stages.Records[7].Values["clock"]>Field(rows[Index(rows,"M16_011_END ")],"end") &&
                report.Leaf.ExitCleanupEnd!.Value.Values["clock"]>=report.Pending.Stages.Records[7].Values["clock"]);
            ulong retained=report.Pending.Stages.Records[0].Values["inode"];Array.Fill(rows,"changed");Array.Clear(package);
            HostCheck.Equal(retained,report.Pending.Stages.Records[0].Values["inode"]);
        }
    });

    [HostTest("exit pending capture rejects coherent wrong inode later sector metadata substitution and detached original rows",HostLane.Native)]
    private static void ExitPendingCaptureRejections()=>WithCapture(c=>
    {
        byte[] package=LinuxDiskRaceProofFixture.CreatePackage("ExitPending");string[] rows=BuildExitRows(c,2);
        QemuTranscripts.InspectLinuxDiskExitPendingCaptureValues(rows,package,c.Image,c.Receipt,c.Path);
        void Deny(string[] changed,string reason)
        {
            string? actual=null;try{_=QemuTranscripts.InspectLinuxDiskExitPendingCaptureValues(changed,package,c.Image,c.Receipt,c.Path);}
            catch(ToolFailure error){actual=error.Reason;}HostCheck.True(actual?.Contains(reason,StringComparison.Ordinal)==true,reason+" actual="+actual);
        }
        Deny(rows.Select(r=>r.StartsWith("M16_025_EXIT_PENDING ",StringComparison.Ordinal)?Set(r,"inode",Field(r,"inode")+1):r).ToArray(),"disk_exit_capture_inode");
        var inventory=WebRootDiskInventory.Inspect(c.Path,c.Image,c.Receipt);
        var data=inventory.Find("/app/wwwroot/fixture/fragmented.bin")!.Cover(0,8192);
        ulong lastGeneration=Field(rows[Index(rows,"M16_025_EXIT_PENDING stage=1 ")],"blockGeneration");
        string[] later=rows.Select(r=>r.StartsWith("M16_025_EXIT_PENDING ",StringComparison.Ordinal)?Set(Set(r,"sector",data[3].Lba),"ioSector",data[3].Lba):
            r.StartsWith("M16_023_IO ",StringComparison.Ordinal)&&Field(r,"generation")==lastGeneration?Set(r,"sector",data[3].Lba):r).ToArray();
        QemuTranscripts.InspectLinuxDiskExitPendingDeviceValues(later,package,c.Image);Deny(later,"disk_exit_capture_data_prefix");
        string[] metadata=rows.Select(r=>r.StartsWith("M16_023_IO ",StringComparison.Ordinal)&&Field(r,"generation")==38?IoSector(r,data[0].Lba,c.Image):r).ToArray();
        QemuTranscripts.InspectLinuxDiskExitPendingDeviceValues(metadata,package,c.Image);Deny(metadata,"metadata_asset");
        int bound=Index(rows,"M16_023_BOUND ");Deny(Change(rows,bound,"pages",89),"disk_capture_measured_layout");
        int supervisor=Index(rows,"M16_008_SUPERVISOR ");
        Deny([..rows[..(supervisor+1)],"M16_008_TIMING identity=4294967297 epoch=1 start=1 arm=2 capture=3 ready=4 periodFs=1000000000",..rows[(supervisor+1)..]],"unexpected_irq_timing");
        int io=Index(rows,"M16_023_IO ");Deny([..rows[..io],..rows[(io+1)..]],"request_identity");
        Deny([..rows,rows[io]],"io_order");
    });

    private static string IoSector(string row,ulong sector,byte[] image)
    {
        row=Set(row,"sector",sector);int data=row.IndexOf(" data=",StringComparison.Ordinal);
        return data<0?row:row[..(data+6)]+Convert.ToHexString(image.AsSpan(checked((int)sector*512),512)).ToLowerInvariant();
    }

    private static string[] BuildExitRows(Capture c,int copied)
    {
        var inventory=WebRootDiskInventory.Inspect(c.Path,c.Image,c.Receipt);
        var file=inventory.Find("/app/wwwroot/fixture/fragmented.bin")!;var sectors=file.Cover(0,8192);
        byte[] package=LinuxDiskRaceProofFixture.CreatePackage("ExitPending");var view=LinuxElfPackage.Validate(package);
        var shell=c.Rows.ToList();int first=shell.IndexOf("TEST_START m16-009"),last=shell.IndexOf("TEST_PASS m16-009");shell.RemoveRange(first,last-first+1);
        void Replace(string prefix,string value)=>shell[shell.FindIndex(r=>r.StartsWith(prefix,StringComparison.Ordinal))]=value;
        Replace("M16_002_STAGED ",$"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length+4095)/4096} handlesClosed=2 reads=1");
        Replace("M16_002_DIGEST ","M16_002_DIGEST "+Convert.ToHexString(SHA256.HashData(package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes))).ToLowerInvariant());
        Replace("M16_002_PACKAGE_DIGEST ","M16_002_PACKAGE_DIGEST "+Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant());
        string[] startup=LinuxDiskParentTests.StartupRows(package)[^7..];
        startup[3]="M16_004_TABLES root=2097152 used=17 leaves=1737 ranges=65";
        startup[4]="M16_004_READBACK old=1048576 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        int startupAt=shell.IndexOf("TEST_START m16-004");shell.RemoveRange(startupAt,7);shell.InsertRange(startupAt,startup);
        ulong generation=37;List<string> leaf=[];
        Replace("M16_011_END ",Set(shell.Single(r=>r.StartsWith("M16_011_END ",StringComparison.Ordinal)),"end",20_009_000));
        foreach(string original in LinuxDiskRaceTranscriptTests.ExitPendingStagedLeaf())
        {
            string row=Regex.Replace(original,@"\b(clock|nanos|started)=([0-9]+)",m=>m.Groups[1].Value+"="+(10_000_000UL+ulong.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*1000).ToString(CultureInfo.InvariantCulture));
            if(original.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal))row=Set(row,"nanos",Field(original,"nanos"));
            if(row.StartsWith("M16_023_BOUND ",StringComparison.Ordinal))row=$"M16_023_BOUND owner=1179648 epoch=5562286797237662465 arena=9437184 pages=90 bar=4026531840 capacity={c.Image.Length/512} bootstrapCompleted=37 bootstrapCopied=37 clock=10001000 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
            if(row.StartsWith("M16_025_COMPLETE ",StringComparison.Ordinal))Emit(inventory.DirectorySectors[0].Lba,Field(row,"clock")-3,true);
            if(row.StartsWith("M16_025_EXIT_PENDING stage=1 ",StringComparison.Ordinal))
            {
                for(int i=0;i<copied;i++)Emit(sectors[i].Lba,19_000_000UL+(ulong)i*3,true);
                Emit(sectors[copied].Lba,19_001_000,false);
            }
            if(row.StartsWith("M16_025_EXIT_PENDING ",StringComparison.Ordinal))
            {
                foreach(string field in new[]{"blockGeneration","submitted","available","ioGeneration"})row=Set(row,field,generation);
                foreach(string field in new[]{"completed","consumed","copied"})row=Set(row,field,generation-1);
                ulong stage=Field(row,"stage"),clock=stage<4?20_000_000+stage*1000:stage<8?20_001_000+stage*1000:20_010_000;
                row=Set(Set(Set(Set(row,"inode",file.Inode),"sector",sectors[copied].Lba),"ioSector",sectors[copied].Lba),"clock",clock);
            }
            if(row.StartsWith("M16_008_BUDGET_END ",StringComparison.Ordinal))row=Set(row,"nanos",20_004_000);
            if(row.StartsWith("M16_025_EXIT_CLEANUP_END ",StringComparison.Ordinal))row=Set(row,"clock",20_011_000);
            leaf.Add(row);
        }
        HostCheck.Equal(40UL+(ulong)copied,generation);shell.InsertRange(first,leaf);
        Replace("M16_025_END ",$"M16_025_END submitted={generation} completed={generation-1} copied={generation-1} bootstrapCompleted=37 bootstrapCopied=37 fsReads={generation-34} cacheHits=20 cacheMisses=1 device=retired fs=retired dma=zero descriptors=closed reservation=retained");
        return shell.ToArray();
        void Emit(ulong sector,ulong clock,bool complete)
        {
            generation++;string common=$"owner=1179648 epoch=5562286797237662465 generation={generation} sector={sector} sequence={generation} bytes=512 available={generation}";
            leaf.Add($"M16_023_IO kind=0 {common} consumed={generation-1} clock={clock}");
            if(!complete)return;
            leaf.Add($"M16_023_IO kind=1 {common} consumed={generation} clock={clock+1}");
            leaf.Add($"M16_023_IO kind=2 {common} consumed={generation} clock={clock+2} copied=committed data={Convert.ToHexString(c.Image.AsSpan(checked((int)sector*512),512)).ToLowerInvariant()}");
        }
    }

    [HostTest("disk capture joins portable real image sectors indexed waits timing and IRQ boundaries", HostLane.Native)]
    private static void PortablePositive() => WithCapture(c =>
    {
        string[] saved = c.Rows.ToArray(); byte[] package = c.Package.ToArray();
        var report = c.Inspect();
        HostCheck.True(report.Leaf.Calls == F.ExpectedCalls && report.ReadWindows.Count == 7 && report.InstructionIrqs == 1);
        HostCheck.True(report.Leaf.Epochs == 37 && report.Leaf.Transfers == 74 && report.Leaf.Completions.Count == 35);
        HostCheck.True(!report.CheckedRunnerNativeFirmwarePackets && !report.ReplayedArbitraryInstructionsOrUserBuffers);
        HostCheck.True(report.Device.Rows.Count == report.IoRecordIndices.Count && report.Device.Cancelled == 0);
        HostCheck.True(report.ReadWindows.Any(w => w.Offset == 65535 && w.Bytes == 4 && w.Sectors.Count == 2));
        HostCheck.True(report.ReadWindows.Any(w => w.Offset == 0 && w.Bytes == 8192 && w.Sectors.Count == 16));
        foreach (var w in report.ReadWindows)
        {
            HostCheck.True(c.Rows[w.CallRecordIndex].StartsWith("M16_008_CALL ", StringComparison.Ordinal));
            HostCheck.True(c.Rows[w.CompletionRecordIndex].StartsWith("M16_025_COMPLETE ", StringComparison.Ordinal));
            HostCheck.True(report.IoRecordIndices.Any(i => i > w.CallRecordIndex && i < w.CompletionRecordIndex));
        }
        HostCheck.True(c.Rows.SequenceEqual(saved) && c.Package.SequenceEqual(package));
        string held = report.Auxiliary[0].Text; Array.Fill(c.Rows, "changed"); Array.Clear(c.Package); Array.Clear(c.Image);
        HostCheck.Equal(held, report.Auxiliary[0].Text); HostCheck.True(report.ReadWindows[0].Sectors.Count != 0);
        bool immutable = false;
        try { ((IList<ulong>)report.ReadWindows[0].Sectors)[0] = 0; } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    });

    [HostTest("disk capture rejects valid sector bytes in the wrong original file window and detached IO", HostLane.Native)]
    private static void SectorWindows() => WithCapture(c =>
    {
        var accepted = c.Inspect(); var first = accepted.ReadWindows[0];
        int copy = accepted.IoRecordIndices.First(i => i > first.CallRecordIndex && i < first.CompletionRecordIndex && Field(c.Rows[i], "kind") == 2);
        ulong generation = Field(c.Rows[copy], "generation"), otherSector = c.Receipt.PartitionFirstLba;
        string[] movedBytes = c.Rows.ToArray();
        for (int i = 0; i < movedBytes.Length; i++)
            if (movedBytes[i].StartsWith("M16_023_IO ", StringComparison.Ordinal) && Field(movedBytes[i], "generation") == generation)
            {
                movedBytes[i] = Set(movedBytes[i], "sector", otherSector);
                if (Field(movedBytes[i], "kind") == 2) movedBytes[i] = movedBytes[i][..movedBytes[i].IndexOf(" data=", StringComparison.Ordinal)] +
                    " data=" + Convert.ToHexString(c.Image.AsSpan(checked((int)otherSector * 512), 512)).ToLowerInvariant();
            }
        // The altered triplet remains a complete valid read of real image bytes;
        // only its association with the owning fragmented-file range is wrong.
        NativeDiskReadObservations.Inspect(movedBytes.Where(r => r.StartsWith("M16_023_IO ", StringComparison.Ordinal)).ToArray(),
            c.Image, accepted.Device.Counters);
        c.Reject(movedBytes, "disk_capture_read_sector_window");
        string[] wrongBytes = c.Rows.ToArray(); int data = wrongBytes[copy].IndexOf(" data=", StringComparison.Ordinal) + 6;
        char replacement = wrongBytes[copy][data] == '0' ? '1' : '0';
        wrongBytes[copy] = wrongBytes[copy][..data] + replacement + wrongBytes[copy][(data + 1)..];
        c.Reject(wrongBytes, "image_bytes");
        int submitted = accepted.IoRecordIndices.First();
        string[] detached = c.Rows.ToList().Where((_, i) => i != submitted).ToArray();
        c.Reject(detached, "request_identity");
        c.Reject([..c.Rows, c.Rows[submitted]], "socket_parent_extra_record");
    });

    [HostTest("disk capture joins exact instruction intervals and measured HPET timing with independent table clocks", HostLane.Native)]
    private static void IrqAndTiming() => WithCapture(c =>
    {
        c.Inspect(); int irq = Index(c.Rows, "M16_009_IRQ "), timing = Index(c.Rows, "M16_008_TIMING ");
        c.Reject(Change(c.Rows, irq, "rip", Field(c.Rows[irq], "rip") + 1), "disk_capture_irq_interval");
        // An authentic instruction start from a later call is still outside
        // this IRQ's current completed-return / next-syscall interval.
        c.Reject(Change(c.Rows, irq, "rip", F.ExpectedReturnRips()[^2]), "disk_capture_irq_interval");
        c.Reject(Change(c.Rows, timing, "periodFs", 100000000), "disk_capture_irq_timing_lifetime");
        string[] outside = c.Rows.ToArray();
        foreach (string name in new[] { "start", "arm", "capture", "ready" }) outside[timing] = Set(outside[timing], name, 0);
        c.Reject(outside, "disk_capture_irq_timing_lifetime");
        string[] earlier = c.Rows.ToArray(); ulong capture = Field(earlier[timing], "capture");
        earlier[timing] = Set(earlier[timing], "ready", capture);
        c.Reject(earlier, "disk_capture_irq_timing_capture");
        int callTiming = Index(c.Rows, "M16_008_SYSCALL_TIME ");
        c.Reject(Change(c.Rows, callTiming, "periodFs", 100000000), "disk_capture_syscall_timing_lifetime");
        int io = Index(c.Rows, "M16_023_IO ");
        var original = c.Inspect(); ulong bindClock = Field(c.Rows[Index(c.Rows, "M16_023_BOUND ")], "clock");
        HostCheck.True(Field(c.Rows[io], "clock") > bindClock);
        c.Reject(c.Rows.Select(r => r.StartsWith("M16_023_IO ", StringComparison.Ordinal) ? Set(r, "clock", bindClock - 1) : r).ToArray(), "disk_capture_io_lifetime");
        HostCheck.True(original.Device.Rows.Count != 0); // No equality with stale CALL clocks was needed.
    });

    [HostTest("disk capture binds exact parent roots layout capacity and final cumulative boundaries", HostLane.Native)]
    private static void ParentBindings() => WithCapture(c =>
    {
        c.Inspect(); int bound = Index(c.Rows, "M16_023_BOUND "), end = Index(c.Rows, "M16_025_END ");
        foreach (var change in new[] { ("capacity", (ulong)c.Image.Length / 512 + 1), ("epoch", 1UL), ("bootstrapCompleted", 38UL), ("bootstrapCopied", 36UL) })
            c.Reject(Change(c.Rows, bound, change.Item1, change.Item2), "disk_capture_binding_counters");
        c.Reject(Change(c.Rows, bound, "pages", 89), "disk_capture_measured_layout");
        c.Reject(Change(c.Rows, bound, "arena", 0x10000000), "disk_capture_live_extents"); // Existing live NIC arena.
        c.Reject(Change(c.Rows, end, "fsReads", Field(c.Rows[end], "fsReads") + 1), "disk_parent_counters");
        c.Reject(Change(c.Rows, end, "cacheMisses", ulong.MaxValue), "disk_parent_counters");
        int tables = Index(c.Rows, "M16_004_TABLES "); c.Reject(Change(c.Rows, tables, "ranges", 64), "m16_004_table_geometry");
        c.Reject([..c.Rows, "unowned"], "socket_parent_extra_record");
    });

    [HostTest("disk capture independently rejects divergent held image receipt inventory and caller bounds", HostLane.Native)]
    private static void ImageIdentity() => WithCapture(c =>
    {
        c.Inspect(); byte[] changed = c.Image.ToArray(); changed[0] ^= 1;
        c.Reject(c.Rows, "disk_capture_image_identity", image: changed);
        c.Reject(c.Rows, "disk_capture_image_identity", receipt: c.Receipt with { FinalSha256 = new string('0', 64) });
        var files = c.Receipt.WebRootFiles.ToArray(); int at = Array.FindIndex(files, f => f.Path.EndsWith("/fragmented.bin", StringComparison.Ordinal));
        var extents = files[at].Extents.ToArray(); extents[0] = extents[0] with { FirstLba = extents[0].FirstLba + 1 };
        files[at] = files[at] with { Extents = extents };
        c.Reject(c.Rows, "disk_capture_image_file", receipt: c.Receipt with { WebRootFiles = files });
        c.Reject([], "disk_capture_input"); c.Reject(["bad\0record"], "disk_capture_row");
        c.Reject([new string('x', 1537)], "disk_capture_row"); c.Reject(new string[131073], "disk_capture_input");
    });

    private sealed class Capture(string[] rows, byte[] image, string path, WebRootImageFixture.Receipt receipt)
    {
        internal readonly string Path=path;
        internal readonly string[] Rows = rows;
        internal readonly byte[] Package = F.CreatePackage(), Image = image;
        internal readonly WebRootImageFixture.Receipt Receipt = receipt;
        internal QemuTranscripts.LinuxDiskCaptureInspection Inspect() => QemuTranscripts.InspectLinuxDiskCaptureValues(Rows, Package, Image, Receipt, Path);
        internal void Reject(string[] candidate, string reason, byte[]? image = null, WebRootImageFixture.Receipt? receipt = null)
        {
            string? actual = null;
            try { _ = QemuTranscripts.InspectLinuxDiskCaptureValues(candidate, Package, image ?? Image, receipt ?? Receipt, Path); }
            catch (ToolFailure error) { actual = error.Reason; }
            HostCheck.True(actual?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + actual);
        }
    }
    private sealed class Description(string path) { internal readonly string Path = path; internal ulong Position; }
    private static void WithCapture(Action<Capture> action) => WebRootImageFixtureTests.WithImage((root, directory, original, source) =>
    {
        string path = Path.Combine(directory, "capture.raw");
        var receipt = WebRootImageFixture.Apply(root, original, source, WebRootImageFixture.SnapshotPublishedRoot(source), path);
        byte[] image = File.ReadAllBytes(path);
        action(new(BuildRows(image, receipt), image, path, receipt));
    });

    // Independent source fixtures provide original firmware/APIC/NIC values and
    // machine-code-decoded calls. This builder supplies synthetic disk events,
    // not a replayed/native guest or an emulated filesystem implementation.
    private static unsafe string[] BuildRows(byte[] image, WebRootImageFixture.Receipt receipt)
    {
        byte[] package = F.CreatePackage(); var view = LinuxElfPackage.Validate(package);
        string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string[] staged = ["TEST_START m16-002", $"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length+4095)/4096} handlesClosed=2 reads=1",
            "M16_002_DIGEST " + Digest(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes)), "M16_002_PACKAGE_DIGEST " + Digest(package),
            "M16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false", "CLROS_M16_002_READY", "TEST_PASS m16-002"];
        byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        byte* stack = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096); LinuxElfStartupPlan startup;
        try { fixed (byte* bytes = package) HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(bytes+view.ManifestOffset, view.ManifestBytes,
            bytes+view.PayloadOffset, view.PayloadBytes, seed, 32, stack, view.Manifest.StackBytes, 0x70000000UL-view.Manifest.StackBytes, 0, out startup)); }
        finally { NativeMemory.AlignedFree(stack); }
        string[] startupRows = ["TEST_START m16-004", "M16_004_IMAGE base=4194304 bytes=28672 loads=2 entry=4194816 phdr=4194368 tlsFile=0 tlsMemory=0",
            $"M16_004_STACK start={startup.StackStart} bytes={startup.StackBytes} sp={startup.StackPointer} argc={startup.ArgumentCount} envc={startup.EnvironmentCount} random={startup.RandomAddress} entropyBytes=32 seedCleared=32",
            "M16_004_TABLES root=2097152 used=17 leaves=1737 ranges=65", "M16_004_READBACK old=1048576 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false",
            "CLROS_M16_004_READY", "TEST_PASS m16-004"];
        string[] service = ["TEST_START m16-003", "M16_003_CLOCK frequency=1000000 period=1000000000 recoveries=0 source=owned-hpet",
            ..Enumerable.Range(1,4).Select(i=>$"M16_003_ITERATION index={i} ticks={i*1000+1}"),
            ..Enumerable.Range(0,8).Select(i=>$"M16_003_FIRMWARE_DENIED index={i} status=ServicesUnavailable providers=1 outputs=unchanged"),
            "M16_003_STOP iterations=4 reads=5 first=1 last=4001 interval=1000 firmware=revoked providers=1 shutdown=owned-controller", "CLROS_M16_003_READY", "TEST_PASS m16-003"];
        var shell = NativeAotSocketIntegrationTests.Envelope(true).ToList(); int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
        shell.RemoveRange(owned, 31); shell.InsertRange(owned, staged.Concat(startupRows).Concat(service));
        int first = shell.IndexOf("TEST_START m16-009"), last = shell.IndexOf("TEST_PASS m16-009"); shell.RemoveRange(first, last-first+1);
        void Replace(string prefix, string replacement) => shell[shell.FindIndex(r=>r.StartsWith(prefix,StringComparison.Ordinal))] = replacement;
        Replace("M16_011_PEER ", "M16_011_PEER mac=90525193470466 sent=1 received=1 start=4500000 end=5000000");
        Replace("M16_011_WIRE ", "M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Replace("M16_011_END ", "M16_011_END polls=100 received=0 copied=0 fresh=0 retired=0 start=4400000 end=500000000 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Replace("M07_026_ROW stage=4 ", "M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        List<string> leaf = []; Dictionary<uint,Description> fds = []; ulong generation=37, epoch=0; int transfers=0;
        string? currentCall=null; bool irq=false;
        string[] originalLeaf = LinuxDiskFileTranscriptTests.Synthetic();
        foreach (string sourceRow in originalLeaf)
        {
            string row = Regex.Replace(sourceRow, @"\b(clock|nanos|started)=([0-9]+)", m => m.Groups[1].Value+"="+(ulong.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*1000+10_000_000).ToString(CultureInfo.InvariantCulture));
            // The budget's nanos field is a duration policy, unlike observed
            // nanos samples. Preserve it while translating the started sample.
            if (sourceRow.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal)) row=Set(row,"nanos",Field(sourceRow,"nanos"));
            if (row.StartsWith("M16_023_BOUND ",StringComparison.Ordinal))
                row=$"M16_023_BOUND owner=8388608 epoch=5562286797237662465 arena=9437184 pages=90 bar=4026531840 capacity={image.Length/512} bootstrapCompleted=37 bootstrapCopied=37 clock=10001000 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
            if (row.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) currentCall=row;
            if (row.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal))
            {
                if (Field(row,"action")==3) leaf.Add($"M16_009_DRAIN identity=4294967297 epoch={++epoch} nanos={Field(currentCall!,"clock")}");
                row=Set(row,"transfers",(ulong)(transfers+=2));
            }
            if (row.StartsWith("M16_025_COMPLETE ",StringComparison.Ordinal))
            {
                EmitIo(currentCall!,unchecked((long)Field(row,"valueBits")),Field(row,"clock")-1);
                Apply(currentCall!,unchecked((long)Field(row,"valueBits")));
            }
            if (row.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal)) { row=Set(row,"transfers",(ulong)transfers); row=Set(row,"irqs",epoch); }
            leaf.Add(row);
            if (row.StartsWith("M16_008_CALL ",StringComparison.Ordinal))
            {
                ulong nr=Field(row,"nr"), ordinal=Field(row,"ordinal"), ticks=Field(row,"clock")/1000;
                leaf.Add($"M16_008_SYSCALL_TIME identity=4294967297 ordinal={ordinal} nr={nr} entered={ticks} admitted={ticks} dispatched={ticks+1} prepared={ticks+1} periodFs=1000000000");
                if (Field(row,"waitSource")==0) Apply(row,unchecked((long)Field(row,"valueBits")));
                if (!irq && nr==5)
                {
                    irq=true; ulong now=Field(row,"clock")+1;
                    leaf.Add($"M16_009_IRQ identity=4294967297 epoch={++epoch} nanos={now} rip={Field(row,"rip")} rsp={Field(row,"rsp")} flags=582 fs=0 gs=0 valueBits=0");
                    leaf.Add($"M16_008_SUPERVISOR identity=4294967297 action=1 transfers={transfers+=2} ready=1 active=1");
                    leaf.Add($"M16_008_TIMING identity=4294967297 epoch={epoch} start={now/1000-1} arm={now/1000-1} capture={now/1000} ready={now/1000+1} periodFs=1000000000");
                }
                if (nr==231) leaf.Add($"M16_009_DRAIN identity=4294967297 epoch={++epoch} nanos={Field(row,"clock")}");
            }
        }
        HostCheck.True(fds.Count==0); shell.InsertRange(first,leaf);
        shell.InsertRange(shell.IndexOf("CLROS_M07_026_READY"),[$"M16_025_END submitted={generation} completed={generation} copied={generation} bootstrapCompleted=37 bootstrapCopied=37 fsReads={generation-34} cacheHits=20 cacheMisses=1 device=retired fs=retired dma=zero descriptors=closed reservation=retained",
            "CLROS_M16_023_READY","TEST_PASS m16.023","CLROS_M16_024_READY","TEST_PASS m16.024","CLROS_M16_025_READY","TEST_PASS m16.025"]);
        return shell.ToArray();

        void Apply(string call,long value)
        {
            ulong nr=Field(call,"nr"), fd=Field(call,"a0");
            if (nr is 2 or 257 && value>=0) fds.Add((uint)value,new(Field(call,nr==2?"a0":"a1")==F.ImageBase+0x11c0?"empty.bin":"fragmented.bin"));
            else if (nr==72 && value>=0) fds.Add((uint)value,fds[(uint)fd]);
            else if (nr==3 && value==0) HostCheck.True(fds.Remove((uint)fd));
            else if (nr==8 && value>=0) fds[(uint)fd].Position=(ulong)value;
            else if (nr==0 && value>0) fds[(uint)fd].Position+=(ulong)value;
        }
        void EmitIo(string call,long value,ulong clock)
        {
            ulong nr=Field(call,"nr"); List<ulong> sectors=[];
            if (Field(call,"ordinal")==1) sectors.Add(receipt.PartitionFirstLba);
            if (nr is 0 or 17 && value!=0)
            {
                var d=fds[(uint)Field(call,"a0")]; HostCheck.Equal("fragmented.bin",d.Path);
                ulong offset=nr==17?Field(call,"a3"):d.Position;
                ulong bytes=(ulong)(value>0?value:(long)Math.Min(Field(call,"a2"),8192));
                var file=receipt.WebRootFiles.Single(f=>f.Path.EndsWith("/fragmented.bin",StringComparison.Ordinal));
                ulong[] chain=file.Extents.SelectMany(e=>Enumerable.Range(0,(int)e.Clusters).Select(n=>e.FirstLba+(uint)n)).ToArray();
                for (ulong i=offset/512;i<=(offset+bytes-1)/512;i++) sectors.Add(chain[(int)i]);
            }
            foreach (ulong sector in sectors)
            {
                ulong next=++generation;
                string Prefix(uint kind,ulong consumed)=>$"M16_023_IO kind={kind} owner=8388608 epoch=5562286797237662465 generation={next} sector={sector} sequence={next} bytes=512 available={unchecked((ushort)next)} consumed={unchecked((ushort)consumed)} clock={clock}";
                leaf.Add(Prefix(0,next-1)); leaf.Add(Prefix(1,next));
                leaf.Add(Prefix(2,next)+" copied=committed data="+Convert.ToHexString(image.AsSpan(checked((int)sector*512),512)).ToLowerInvariant());
            }
        }
    }
    private static int Index(string[] rows,string prefix)=>Array.FindIndex(rows,r=>r.StartsWith(prefix,StringComparison.Ordinal));
    private static ulong Field(string row,string key)=>ulong.Parse(row.Split(' ').Single(f=>f.StartsWith(key+"=",StringComparison.Ordinal))[(key.Length+1)..],CultureInfo.InvariantCulture);
    private static string Set(string row,string key,ulong value)=>string.Join(' ',row.Split(' ').Select(f=>f.StartsWith(key+"=",StringComparison.Ordinal)?key+"="+value.ToString(CultureInfo.InvariantCulture):f));
    private static string[] Change(string[] rows,int at,string key,ulong value) { var copy=rows.ToArray(); copy[at]=Set(copy[at],key,value); return copy; }

    [HostTest("CloseReuse all IO owners separate detached data from overlapping reopen metadata", HostLane.Native)]
    private static void CloseReuseTransactionOwners() => WithCapture(c =>
    {
        foreach (var mode in new[] { (Detached: true, Staging: false), (Detached: true, Staging: true), (Detached: false, Staging: false) })
        {
            string[] rows = CloseReuseWholeRows(c, mode.Detached, mode.Staging), before = rows.ToArray();
            var report = CloseReuseWhole(c, rows);
            HostCheck.True(rows.SequenceEqual(before) && report.Leaf.Variant == "CloseReuse" &&
                !report.Leaf.CheckedUserMemoryOrAuxiliaryJoins && !report.Leaf.CheckedPostDeathSentinelOrOrphanDrain);
            ulong[] assigned = report.Requests.SelectMany(r => r.DeviceGenerations).ToArray();
            HostCheck.True(assigned.Distinct().Count() == assigned.Length &&
                assigned.Order().SequenceEqual(report.Device.Rows.Where(r => r.Kind == NativeDiskReadObservations.Kind.Submitted)
                    .Select(r => r.Generation).Order()));
            var old = report.Requests.Single(r => r.Call.Spec.Role == "main.read");
            var reopened = report.Requests.Single(r => r.Call.Spec.Role == "child.reopen");
            HostCheck.True(old.Window.Value == -9 && old.Window.OutputBytes == 8192 &&
                old.DeviceGenerations.Count == 1 && reopened.DeviceGenerations.Count == 2 &&
                report.Requests.Where(r => r.Call.Spec.Role is "child.empty" or "main.replacement" or
                    "main.replacement-cursor" or "main.cursor" or "main.independent-cursor")
                    .All(r => r.DeviceGenerations.Count == 0));
            int oldTerminal = report.Leaf.Auxiliary.Single(a => a.Text.StartsWith("M16_023_IO kind=1 ", StringComparison.Ordinal) &&
                Field(a.Text, "generation") == old.DeviceGenerations[0]).RecordIndex;
            HostCheck.True(mode.Detached ? oldTerminal > old.Window.CompletionRecordIndex :
                oldTerminal < old.Window.CompletionRecordIndex &&
                report.Leaf.Auxiliary.Any(a => a.Text.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) &&
                    Field(a.Text, "generation") == reopened.DeviceGenerations[0] && a.RecordIndex < old.Window.CompletionRecordIndex));
            HostCheck.Equal(mode.Staging ? 0UL : 1UL, report.Device.Cancelled);
        }
    });

    [HostTest("CloseReuse rejects missing reassigned orphan stale and out-of-leaf physical owners", HostLane.Native)]
    private static void CloseReuseTransactionRejections() => WithCapture(c =>
    {
        string[] rows = CloseReuseWholeRows(c); var report = CloseReuseWhole(c, rows);
        var initial = report.Requests.Single(r => r.Call.Spec.Role == "prep.original");
        var old = report.Requests.Single(r => r.Call.Spec.Role == "main.read");
        int submit = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) &&
            Field(r, "generation") == initial.DeviceGenerations[0]);
        // Remove a WHOLE metadata transaction and repair only synthetic device
        // counters/ring numbering: reject the missing directory route, rather
        // than merely failing a sequence gap in the observation parser.
        var missing = rows.ToList(); missing.RemoveRange(submit, 3);
        RejectCloseReuseWhole(c, RenumberCloseReuseIo(missing.ToArray()), "disk_close_reuse_metadata_route");
        var reopened = report.Requests.Single(r => r.Call.Spec.Role == "child.reopen");
        string[] reassigned = MoveCloseReuseTransaction(rows, submit, reopened.Window.CompletionRecordIndex,
            Field(rows[reopened.Window.CompletionRecordIndex], "clock") - 100);
        RejectCloseReuseWhole(c, reassigned, "disk_close_reuse_metadata_route");
        // Reassign the same real sector bytes into a read-only interval before
        // the old data submit. Its raw shape/hash/counters are valid, but a read
        // has no directory/FAT ownership route.
        string[] moved = MoveCloseReuseTransaction(rows, submit, old.Call.RecordIndex + 2,
            Field(rows[old.Call.RecordIndex], "clock") + 50);
        RejectCloseReuseWhole(c, moved, "disk_close_reuse_unowned_transaction");
        int oldSubmit = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) &&
            Field(r, "generation") == old.DeviceGenerations[0]);
        RejectCloseReuseWhole(c, Change(rows, oldSubmit, "owner", 8388616), "disk_observation_association");
        RejectCloseReuseWhole(c, Change(rows, oldSubmit, "epoch", 2), "disk_observation_association");
        int terminal = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=1 ", StringComparison.Ordinal) &&
            Field(r, "generation") == old.DeviceGenerations[0]);
        RejectCloseReuseWhole(c, Change(rows, terminal, "generation", old.DeviceGenerations[0] + 1), "disk_observation_request_identity");
        // A complete additional image-matching transaction after the last disk
        // wait cannot be projected away or attached to the earlier detached token.
        var later = report.Requests.Single(r => r.Call.Spec.Role == "main.duplicate");
        int data = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) &&
            Field(r, "generation") == later.DeviceGenerations[0]);
        string[] extra = rows.Skip(data).Take(3).ToArray();
        int after = report.Requests.Max(r => r.Window.CompletionRecordIndex) + 2;
        ulong clock = Field(rows[report.Requests.Max(r => r.Window.CompletionRecordIndex)], "clock") + 100;
        for (int i = 0; i < extra.Length; i++) extra[i] = Set(extra[i], "clock", clock + (ulong)i);
        var orphan = rows.ToList(); orphan.InsertRange(after, extra);
        RejectCloseReuseWhole(c, RenumberCloseReuseIo(orphan.ToArray()), "disk_close_reuse_unowned_transaction");
        var outside = rows.Concat(extra).ToArray();
        RejectCloseReuseWhole(c, RenumberCloseReuseIo(outside), "disk_close_reuse_io_census");
        // A different fragmented sector is real image data but not the required
        // ordered prefix of this cancelled read.
        var file = WebRootDiskInventory.Inspect(c.Path, c.Image, c.Receipt).Find("/app/wwwroot/fixture/fragmented.bin")!;
        ulong wrong = file.Cover(0, 8192)[3].Lba;
        var skipped = Change(rows, oldSubmit, "sector", wrong); skipped = Change(skipped, terminal, "sector", wrong);
        RejectCloseReuseWhole(c, skipped, "disk_close_reuse_old_data_prefix");
    });

    [HostTest("CloseReuse metadata ambiguity uses copied table values without forging a sealed capture", HostLane.Native)]
    private static void CloseReuseAmbiguousMetadata() => WithCapture(c =>
    {
        var report = CloseReuseWhole(c, CloseReuseWholeRows(c));
        var one = report.Requests.Single(r => r.Call.Spec.Role == "prep.original");
        var two = report.Requests.Single(r => r.Call.Spec.Role == "child.reopen");
        // Independent forged WINDOW VALUES only: distinct calls, both spans cover
        // one metadata transaction. The real entry never accepts this table from
        // its caller; it constructs it from the canonical leaf.
        int submitted = two.Call.RecordIndex + 2, terminal = submitted + 2;
        var requests = new[] { (one.Call, one.Window with { CompletionRecordIndex = terminal + 10 }),
            (two.Call, two.Window with { CompletionRecordIndex = terminal + 10 }) };
        bool rejected = false;
        try { CloseReuseInvoke("CloseReuseUniqueTransactionOwner", [requests, true, 6270UL, 42UL,
            submitted, terminal, 42UL, Enumerable.Range(0, 16).Select(i => (ulong)i).ToArray()]); }
        catch (ToolFailure error) { rejected = error.Reason == "disk_close_reuse_ambiguous_owner"; }
        HostCheck.True(rejected);
    });

    [HostTest("CloseReuse malformed duplicate and repeated-pci bounds always throw ToolFailure", HostLane.Native)]
    private static void CloseReuseBoundRejections() => WithCapture(c =>
    {
        string[] rows = CloseReuseWholeRows(c); int bound = Index(rows, "M16_023_BOUND ");
        var duplicated = rows.ToList(); duplicated.Insert(bound, rows[bound]);
        RejectCloseReuseWhole(c, duplicated.ToArray(), "disk_close_reuse_bound_count");
        foreach (string value in new[] { "M16_023_BOUND owner=1", rows[bound].Replace(" pci=", " pciMissing=", StringComparison.Ordinal),
            rows[bound].Replace(" pci=", " pci=0:7:0 pci=", StringComparison.Ordinal),
            rows[bound].Replace(" pci=0:7:0", " pci=0:8:0", StringComparison.Ordinal), "M16_023_BOUND_extra" })
        {
            string[] changed = rows.ToArray(); changed[bound] = value;
            RejectCloseReuseWhole(c, changed, "disk_close_reuse_bound_shape");
        }
    });

    private static (QemuTranscripts.LinuxDiskRaceLeafReport Leaf, NativeDiskReadObservations.Inspection Device,
        IReadOnlyList<(QemuTranscripts.LinuxDiskRaceCall Call, QemuTranscripts.LinuxDiskRaceWindow Window,
            IReadOnlyList<ulong> DeviceGenerations)> Requests) CloseReuseWhole(Capture c, string[] rows) =>
        ((QemuTranscripts.LinuxDiskRaceLeafReport, NativeDiskReadObservations.Inspection,
          IReadOnlyList<(QemuTranscripts.LinuxDiskRaceCall, QemuTranscripts.LinuxDiskRaceWindow, IReadOnlyList<ulong>)>))
        CloseReuseInvoke("InspectLinuxDiskCloseReuseTransactionsCandidateValues", [rows,
            LinuxDiskRaceProofFixture.CreatePackage("CloseReuse"), 0x200000UL, 0x100000UL, c.Image, c.Receipt, c.Path,
            new NativeDiskReadObservations.Boundary(8388608, 1, 37, 37,
                37UL + (ulong)rows.Count(r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal)),
                37UL + (ulong)rows.Count(r => r.StartsWith("M16_023_IO kind=1 ", StringComparison.Ordinal)),
                37UL + (ulong)rows.Count(r => r.StartsWith("M16_023_IO kind=2 ", StringComparison.Ordinal))), 1_000_000UL]);

    private static object CloseReuseInvoke(string name, object[] args)
    {
        var method = typeof(QemuTranscripts).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        try { return method.Invoke(null, args)!; }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void RejectCloseReuseWhole(Capture c, string[] rows, string reason)
    {
        bool rejected = false; try { CloseReuseWhole(c, rows); }
        catch (ToolFailure error) { rejected = error.Reason == reason; }
        HostCheck.True(rejected, reason);
    }

    private static string[] CloseReuseWholeRows(Capture c, bool detached = true, bool staging = false)
    {
        var inventory = WebRootDiskInventory.Inspect(c.Path, c.Image, c.Receipt);
        ulong root = inventory.DirectorySectors.First(s => s.Path == "/app/wwwroot").Lba;
        ulong directory = inventory.DirectorySectors.First(s => s.Path == "/app/wwwroot/fixture").Lba;
        ulong data = inventory.Find("/app/wwwroot/fixture/fragmented.bin")!.Cover(0, 8192)[0].Lba;
        string[] rows = LinuxDiskRaceTranscriptTests.Synthetic("CloseReuse").Select(original =>
        {
            string row = Regex.Replace(original, @"\b(clock|nanos|started)=([0-9]+)", m => m.Groups[1].Value + "=" +
                (ulong.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 10000).ToString(CultureInfo.InvariantCulture));
            if (original.StartsWith("M16_008_BUDGET ", StringComparison.Ordinal)) row = Set(row, "nanos", Field(original, "nanos"));
            if (row.StartsWith("M16_023_BOUND ", StringComparison.Ordinal)) row = Set(row, "capacity", (ulong)c.Image.Length / 512);
            return row;
        }).ToArray();
        var leaf = QemuTranscripts.InspectLinuxDiskRaceLeaf(rows, LinuxDiskRaceProofFixture.CreatePackage("CloseReuse"), 0x200000, 0x100000);
        var calls = leaf.Calls.ToDictionary(call => call.Spec.Role);
        QemuTranscripts.LinuxDiskRaceWindow Window(string role) => leaf.Windows.Single(w => w.CallRecordIndex == calls[role].RecordIndex);
        Dictionary<int, List<string>> insert = [];
        void Add(int at, IEnumerable<string> values) { if (!insert.TryGetValue(at, out var list)) insert[at] = list = []; list.AddRange(values); }
        ulong generation = 37, copied = 37;
        string Io(uint kind, ulong gen, ulong sector, ulong sequence, uint bytes, ulong available, ulong consumed, ulong clock) =>
            $"M16_023_IO kind={kind} owner=8388608 epoch=1 generation={gen} sector={sector} sequence={sequence} bytes={bytes} available={available} consumed={consumed} clock={clock}";
        string[] Emit(ulong sector, ulong clock)
        {
            ulong gen = ++generation;
            return [Io(0, gen, sector, gen, 512, gen, gen - 1, clock),
                Io(1, gen, sector, gen, 512, gen, gen, clock + 10),
                Io(2, gen, sector, ++copied, 512, gen, gen, clock + 20) + " copied=committed data=" +
                    Convert.ToHexString(c.Image.AsSpan(checked((int)sector * 512), 512)).ToLowerInvariant()];
        }
        foreach (var call in calls.Values.Where(call => call.Spec.Wait == LinuxDiskRaceProofFixture.WaitPolicy.Disk || call.Spec.Role == "child.close"))
        {
            ulong clock = Field(rows[call.RecordIndex], "clock");
            Add(call.RecordIndex + 1, [$"M16_008_SYSCALL_TIME identity={call.Identity} ordinal={call.Ordinal} nr={call.Spec.Number} entered={clock - 10} admitted={clock + 10} dispatched={clock + 20} prepared={clock + 30} periodFs=1000000"]);
        }
        foreach (string role in new[] { "prep.original", "prep.independent" })
        {
            var window = Window(role); ulong clock = Field(rows[window.CompletionRecordIndex], "clock") - 800;
            Add(window.CompletionRecordIndex, Emit(root, clock)); Add(window.CompletionRecordIndex, Emit(directory, clock + 100));
        }
        var main = Window("main.read"); ulong old = ++generation;
        Add(calls["child.tid"].RecordIndex - 1, [Io(0, old, data, old, 512, old, old - 1,
            Field(rows[main.CallRecordIndex], "clock") + 100)]);
        int retirementAt = detached ? main.CompletionRecordIndex + 2 : main.CompletionRecordIndex;
        ulong retirementClock = Field(rows[main.CompletionRecordIndex], "clock") + (detached ? 100UL : 0UL) - (detached ? 0UL : 1000UL);
        Add(retirementAt, [Io(1, old, data, old, staging ? 512U : 0U, old, old, retirementClock)]);
        if (staging) Add(retirementAt, [Io(2, old, data, ++copied, 512, old, old, retirementClock + 10) + " copied=committed data=" +
            Convert.ToHexString(c.Image.AsSpan(checked((int)data * 512), 512)).ToLowerInvariant()]);
        int reopenedAt = detached ? Window("child.reopen").CompletionRecordIndex : main.CompletionRecordIndex;
        ulong reopenedClock = detached ? Field(rows[Window("child.reopen").CompletionRecordIndex], "clock") - 800 : retirementClock + 100;
        Add(reopenedAt, Emit(root, reopenedClock)); Add(reopenedAt, Emit(directory, reopenedClock + 100));
        var duplicate = Window("main.duplicate"); Add(duplicate.CompletionRecordIndex,
            Emit(data, Field(rows[duplicate.CompletionRecordIndex], "clock") - 300));
        List<string> result = [];
        for (int i = 0; i < rows.Length; i++) { if (insert.TryGetValue(i, out var values)) result.AddRange(values); result.Add(rows[i]); }
        return result.ToArray();
    }

    private static string[] MoveCloseReuseTransaction(string[] rows, int from, int to, ulong clock)
    {
        var copy = rows.ToList(); string[] tx = copy.Skip(from).Take(3).Select((row, i) => Set(row, "clock", clock + (ulong)i)).ToArray();
        copy.RemoveRange(from, 3); copy.InsertRange(to > from ? to - 3 : to, tx);
        return RenumberCloseReuseIo(copy.ToArray());
    }
    private static string[] RenumberCloseReuseIo(string[] rows)
    {
        // ONLY independent synthetic mutations. Never normalize original evidence.
        ulong submitted = 37, completed = 37, copied = 37, generation = 0;
        return rows.Select(row =>
        {
            if (!row.StartsWith("M16_023_IO ", StringComparison.Ordinal)) return row;
            ulong kind = Field(row, "kind"), sequence;
            if (kind == 0) sequence = generation = ++submitted;
            else sequence = kind == 1 ? ++completed : ++copied;
            row = Set(Set(Set(row, "generation", generation), "sequence", sequence), "available", unchecked((ushort)submitted));
            return Set(row, "consumed", unchecked((ushort)completed));
        }).ToArray();
    }

    [HostTest("CloseReuse rejects a cancelled earlier data prefix before the actual close token", HostLane.Native)]
    private static void CloseReuseEarlyCancelledPrefix() => WithCapture(c =>
    {
        string[] rows = CloseReuseWholeRows(c);
        var baseline = CloseReuseWhole(c, rows);
        var old = baseline.Requests.Single(r => r.Call.Spec.Role == "main.read");
        ulong generation = old.DeviceGenerations.Single();
        var file = WebRootDiskInventory.Inspect(c.Path, c.Image, c.Receipt)
            .Find("/app/wwwroot/fixture/fragmented.bin")!;
        var coverage = file.Cover(0, 8192);
        int submitted = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) &&
            Field(r, "generation") == generation);
        int validated = Array.FindIndex(rows, r => r.StartsWith("M16_023_IO kind=1 ", StringComparison.Ordinal) &&
            Field(r, "generation") == generation);
        HostCheck.True(submitted >= 0 && validated > submitted);
        HostCheck.Equal(0UL, Field(rows[validated], "bytes"));
        HostCheck.Equal(coverage[0].Lba, Field(rows[submitted], "sector"));
        ulong clock = Field(rows[submitted], "clock");
        string time = rows[old.Call.RecordIndex + 1];
        HostCheck.Equal(1_000_000UL, Field(time, "periodFs")); // This vector's ticks equal nanoseconds.
        HostCheck.True(clock >= 30 && Field(time, "dispatched") <= clock - 30);

        // Both new rows precede the selected pending token and child.close.
        // Their counter/generation placeholders are repaired only by the
        // existing synthetic helper; no original evidence is normalized.
        string earlierSubmit = Set(rows[submitted], "clock", clock - 30);
        string earlierCancel = Set(rows[validated], "clock", clock - 20);
        string[] changed = rows.ToArray();
        changed[submitted] = Set(changed[submitted], "sector", coverage[1].Lba);
        changed[validated] = Set(changed[validated], "sector", coverage[1].Lba);
        var copy = changed.ToList();
        copy.InsertRange(submitted, [earlierSubmit, earlierCancel]);

        // Metadata, real later fd7 data, final sentinel/cursor recipe and the
        // last-token close overlap remain unchanged. The old data sectors now
        // form the correct prefix, but cancellation cannot precede another
        // submission by that same read without an actual producer explanation.
        RejectCloseReuseWhole(c, RenumberCloseReuseIo(copy.ToArray()), "disk_close_reuse_early_cancel");
    });

    [HostTest("CloseReuse full copied parent joins exact cancellation counters and preserves default equality", HostLane.Native)]
    private static void CloseReuseFullParent() => WithCapture(c =>
    {
        foreach (var mode in new[] { (Detached: true, Staging: false), (Detached: true, Staging: true), (Detached: false, Staging: false) })
        {
            string[] rows = BuildCloseReuseFullRows(c, mode.Detached, mode.Staging), before = rows.ToArray();
            var report = InspectCloseReuseFull(c, rows);
            HostCheck.True(rows.SequenceEqual(before) && report.Parent.UserRoot == 0x200000 &&
                report.Parent.KernelRoot == 0x100000 && report.Leaf.Variant == "CloseReuse" &&
                !report.Leaf.CheckedUserMemoryOrAuxiliaryJoins && !report.Leaf.CheckedPostDeathSentinelOrOrphanDrain);
            HostCheck.Equal(mode.Staging ? 0UL : 1UL, report.Device.Cancelled);
            int end = Index(rows, "M16_025_END ");
            HostCheck.Equal(report.Device.Counters.Submitted, Field(rows[end], "submitted"));
            HostCheck.Equal(report.Device.Counters.Copied, Field(rows[end], "copied"));
            HostCheck.Equal(report.Device.Cancelled, Field(rows[end], "completed") - Field(rows[end], "copied"));
            if (!mode.Staging)
            {
                // The ordinary copied network entry never receives a typed
                // CloseReuse inspection and must retain copied==completed.
                bool rejected = false;
                try { QemuTranscripts.InspectLinuxDiskNetworkForTests(rows); }
                catch (ToolFailure error) { rejected = error.Reason == "disk_parent_counters"; }
                HostCheck.True(rejected);
            }
            Array.Fill(rows, "changed");
            HostCheck.True(report.Leaf.Calls.Count != 0 && report.Device.Rows.Count != 0);
        }
    });

    [HostTest("CloseReuse full copied parent rejects mismatched roots counters clock layout and selection", HostLane.Native)]
    private static void CloseReuseFullParentRejections() => WithCapture(c =>
    {
        string[] rows = BuildCloseReuseFullRows(c);
        InspectCloseReuseFull(c, rows);
        void Deny(string[] changed, string? reason = null, byte[]? package = null)
        {
            string? actual = null;
            try { InspectCloseReuseFull(c, changed, package); }
            catch (ToolFailure error) { actual = error.Reason; }
            HostCheck.True(actual is not null && (reason is null || actual.Contains(reason, StringComparison.Ordinal)),
                (reason ?? "ToolFailure") + " actual=" + actual);
        }
        int bound = Index(rows, "M16_023_BOUND "), end = Index(rows, "M16_025_END ");
        Deny(Change(rows, bound, "pages", 89), "disk_capture_measured_layout");
        Deny(Change(rows, bound, "arena", 0x10000000), "disk_capture_live_extents");
        foreach (string field in new[] { "submitted", "completed", "copied", "fsReads" })
            Deny(Change(rows, end, field, Field(rows[end], field) + 1));
        Deny(Change(rows, end, "cacheMisses", ulong.MaxValue));
        int tables = Index(rows, "M16_004_TABLES ");
        Deny(Change(rows, tables, "root", Field(rows[tables], "root") + 4096));
        int clock = Index(rows, "M16_003_CLOCK ");
        Deny(Change(rows, clock, "period", 1000000));
        string[] epoch = rows.Select(row => row.StartsWith("M16_023_BOUND ", StringComparison.Ordinal) ||
            row.StartsWith("M16_023_IO ", StringComparison.Ordinal) ? Set(row, "epoch", 1) : row).ToArray();
        Deny(epoch, "disk_capture_binding_counters");
        Deny(rows.Where(row => !row.StartsWith("M16_025_END ", StringComparison.Ordinal)).ToArray());
        Deny([..rows, rows[bound]]);
        Deny(rows.Select(row => row.StartsWith("M16_008_BOUND ", StringComparison.Ordinal)
            ? row.Replace("timer=idle-only", "timer=user-irq-and-idle", StringComparison.Ordinal) : row).ToArray());
        Deny(rows, package: LinuxDiskRaceProofFixture.CreatePackage("Progress"));
        Deny([null!]);
    });

    private static (QemuTranscripts.WebParentReport Parent, QemuTranscripts.LinuxDiskRaceLeafReport Leaf,
        NativeDiskReadObservations.Inspection Device,
        IReadOnlyList<(QemuTranscripts.LinuxDiskRaceCall Call, QemuTranscripts.LinuxDiskRaceWindow Window,
            IReadOnlyList<ulong> DeviceGenerations)> Requests) InspectCloseReuseFull(Capture c, string[] rows, byte[]? package = null) =>
        ((QemuTranscripts.WebParentReport, QemuTranscripts.LinuxDiskRaceLeafReport, NativeDiskReadObservations.Inspection,
          IReadOnlyList<(QemuTranscripts.LinuxDiskRaceCall, QemuTranscripts.LinuxDiskRaceWindow, IReadOnlyList<ulong>)>))
        CloseReuseInvoke("InspectLinuxDiskCloseReuseCaptureValues",
            [rows, package ?? LinuxDiskRaceProofFixture.CreatePackage("CloseReuse"), c.Image, c.Receipt, c.Path]);

    private static string[] BuildCloseReuseFullRows(Capture c, bool detached = true, bool staging = false)
    {
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage("CloseReuse");
        var view = LinuxElfPackage.Validate(package);
        string[] copied = CloseReuseWholeRows(c, detached, staging);
        var device = CloseReuseWhole(c, copied).Device;
        var shell = c.Rows.ToList();
        int first = shell.IndexOf("TEST_START m16-009"), last = shell.IndexOf("TEST_PASS m16-009");
        shell.RemoveRange(first, last - first + 1);
        void Replace(string prefix, string value) =>
            shell[shell.FindIndex(row => row.StartsWith(prefix, StringComparison.Ordinal))] = value;
        Replace("M16_002_STAGED ", $"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length + 4095) / 4096} handlesClosed=2 reads=1");
        Replace("M16_002_DIGEST ", "M16_002_DIGEST " +
            Convert.ToHexString(SHA256.HashData(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes))).ToLowerInvariant());
        Replace("M16_002_PACKAGE_DIGEST ", "M16_002_PACKAGE_DIGEST " + Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant());
        string[] startup = LinuxDiskParentTests.MappedStartupRows(package, 65)[^7..];
        startup[3] = Set(startup[3], "root", 2097152);
        startup[4] = Set(startup[4], "old", 1048576);
        int at = shell.IndexOf("TEST_START m16-004");
        shell.RemoveRange(at, 7);
        shell.InsertRange(at, startup);
        List<string> leaf = [];
        foreach (string original in copied)
        {
            string row = Regex.Replace(original, @"\b(clock|nanos|started)=([0-9]+)", match =>
                match.Groups[1].Value + "=" + (10_000_000UL +
                    ulong.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture));
            if (original.StartsWith("M16_008_BUDGET ", StringComparison.Ordinal))
                row = Set(row, "nanos", Field(original, "nanos"));
            if (row.StartsWith("M16_023_BOUND ", StringComparison.Ordinal))
                row = $"M16_023_BOUND owner=8388608 epoch=5562286797237662465 arena=9437184 pages=90 bar=4026531840 capacity={c.Image.Length / 512} bootstrapCompleted=37 bootstrapCopied=37 clock={Field(row, "clock")} pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
            if (row.StartsWith("M16_023_IO ", StringComparison.Ordinal))
                row = Set(row, "epoch", 5562286797237662465);
            if (row.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
            {
                foreach (string field in new[] { "entered", "admitted", "dispatched", "prepared" })
                    row = Set(row, field, (Field(original, field) + 10_000_000) / 1000);
                row = Set(row, "periodFs", 1000000000);
            }
            leaf.Add(row);
        }
        shell.InsertRange(first, leaf);
        var counters = device.Counters;
        Replace("M16_025_END ", $"M16_025_END submitted={counters.Submitted} completed={counters.Completed} copied={counters.Copied} bootstrapCompleted=37 bootstrapCopied=37 fsReads={counters.Submitted - 34} cacheHits=20 cacheMisses=1 device=retired fs=retired dma=zero descriptors=closed reservation=retained");
        return shell.ToArray();
    }
}
