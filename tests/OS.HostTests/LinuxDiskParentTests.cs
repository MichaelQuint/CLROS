using OS.Tooling;
using OS.Manifest;
using OS.Kernel.Compatibility.LinuxX64;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

internal static class LinuxDiskParentTests
{
    private delegate byte[] BclPackageBuilder(ReadOnlySpan<byte> elf,string phase,string digest);
    private delegate NativeAotLogUploadContract.ClrosPackageValues BclPackageInspector(ReadOnlySpan<byte> package,string phase,string digest);
    private delegate string[] StartupValueConsumer(string[] lines,byte[] package,bool diskFiles,bool diskStatic,bool exitPending,bool writable,bool logUpload,NativeAotLogUploadContract.ClrosPackageValues? values);
    private static readonly BclPackageBuilder BuildBclPackage=typeof(NativeAotLogUploadContract).GetMethod("CreateClrosPackageCore",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<BclPackageBuilder>();
    private static readonly BclPackageInspector InspectBclPackage=typeof(NativeAotLogUploadContract).GetMethod("InspectClrosPackageCore",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<BclPackageInspector>();
    private static readonly StartupValueConsumer InspectStartupValues=typeof(QemuTranscripts).GetMethod("ValidateLinuxStartupTailCore",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<StartupValueConsumer>();

    [HostTest("writable copied full parents preserve canonical30 and40 staging startup HPET roots and zero TCP without qualifying their leaf")]
    private static void WritableFullParents()
    {
        foreach(bool logUpload in new[]{false,true})
        {
            byte[] package=logUpload?LinuxWritableDiskProofFixture.CreateLogUploadPackage():LinuxWritableDiskProofFixture.CreatePackage();
            string[] rows=WritableParentRows(package),before=rows.ToArray();byte[] original=package.ToArray();
            var report=QemuTranscripts.InspectWritableDiskParentValues(rows,package,logUpload);
            HostCheck.Equal(16_777_216UL,report.Parent.UserRoot);HostCheck.Equal(33_554_432UL,report.Parent.KernelRoot);
            HostCheck.Equal(1_000_000UL,report.Parent.Frequency);HostCheck.Equal(1_000_000_000UL,report.Parent.Period);
            HostCheck.Equal(268_435_456UL,report.Parent.Dma);HostCheck.Equal(4_500_000UL,report.Parent.PeerStart);HostCheck.Equal(5_000_000UL,report.Parent.PeerEnd);
            HostCheck.True(report.Parent.Records>QemuContracts.M03OwnedSubsystemMarkers.Count && report.Parent.Arp.ReceiveBytes>0 &&
                report.Phase=="" && !report.HasPreparedBclProgram && !report.CheckedLeaf && !report.CheckedFirmwareFilesOrOriginalProcess &&
                !report.GrantsSourceSchedulerGuestOrPersistenceAuthority && report.PackageSha256==Convert.ToHexString(SHA256.HashData(package)));
            HostCheck.True(rows.SequenceEqual(before) && package.AsSpan().SequenceEqual(original));
            Array.Fill(rows,"modified");package.AsSpan().Clear();
            HostCheck.Equal(Convert.ToHexString(SHA256.HashData(original)),report.PackageSha256);
        }
    }

    [HostTest("writable copied full parents reject late cleanup HPET staging root and TCP substitutions after a valid prefix")]
    private static void WritableFullParentFailures()
    {
        byte[] package=LinuxWritableDiskProofFixture.CreateLogUploadPackage();string[] rows=WritableParentRows(package);
        _=QemuTranscripts.InspectWritableDiskParentValues(rows,package,logUpload:true);
        foreach(var change in new[]{("handlesClosed=2","handlesClosed=1"),("reads=1","reads=0"),
            ("ranges=65","ranges=67"),("source=owned-hpet","source=firmware"),("frequency=1000000","frequency=2000000"),
            ("start=4500000","start=4000000"),("nic=retired","nic=live"),("flushAcknowledgedBytes=512","flushAcknowledgedBytes=0"),
            ("stage=5 a=8","stage=5 a=7")})
        {
            string[] changed=rows.Select(r=>r.Replace(change.Item1,change.Item2,StringComparison.Ordinal)).ToArray();
            HostCheck.True(!rows.SequenceEqual(changed),change.Item1);
            DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(changed,package,logUpload:true));
        }
        int leaf=Array.IndexOf(rows,"TEST_START m16-009");
        DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues([..rows[..(leaf+1)],"M16_011_SLOT event=0",..rows[(leaf+1)..]],package,true),"signal_epoll_no_tcp_lifetimes");
        DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues([..rows,"unexpected tail"],package,true),"socket_parent_extra_record");
        DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(rows,package,logUpload:false));
    }

    [HostTest("writable copied full parents require the prepared BCL discriminator and reject malformed bounded rows")]
    private static void WritableParentSelectionAndBounds()
    {
        foreach(string phase in new[]{"write","verify"})
        {
            var (package,_)=CopiedBclPackage(phase);string[] rows=WritableParentRows(package);
            foreach(bool logUpload in new[]{false,true})
                DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(rows,package,logUpload));
        }
        byte[] ordinary=LinuxWritableDiskProofFixture.CreatePackage();string[] good=WritableParentRows(ordinary);
        foreach(string[] input in new string[][]{[],["bad\0row"],["nonascii µ"],[new string('x',2049)],new string[131073],null!})
            DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(input,ordinary),"writable_parent_input");
        DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(good,null!),"writable_parent_input");
        DenyStartup(()=>QemuTranscripts.InspectWritableDiskParentValues(good,[]));
    }

    // Full copied parents with an intentionally unvalidated placeholder leaf.
    // Reused by the ignored original-B8 prepared-input readback; no guest runs.
    internal static string[] WritableParentRows(byte[] package)
    {
        var view=LinuxElfPackage.Validate(package);HostCheck.True(view.Succeeded);
        string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string[] staged=["TEST_START m16-002",FormattableString.Invariant($"M16_002_STAGED bytes={package.Length} payload={view.PayloadBytes} pages={(package.Length+4095)/4096} handlesClosed=2 reads={(package.Length+65535)/65536}"),
            "M16_002_DIGEST "+Hash(package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes)),"M16_002_PACKAGE_DIGEST "+Hash(package),
            "M16_002_OWNED firmware=exited package=read-only-nx ledger=valid fileBytes=64 entry=false","CLROS_M16_002_READY","TEST_PASS m16-002"];
        string[] startup=MappedStartupRows(package,view.PayloadBytes==NativeAotLogUploadContract.ClrosElfBytes?67UL:65UL)[^7..];
        string[] service=["TEST_START m16-003","M16_003_CLOCK frequency=1000000 period=1000000000 recoveries=0 source=owned-hpet",
            ..Enumerable.Range(1,4).Select(i=>FormattableString.Invariant($"M16_003_ITERATION index={i} ticks={i*1000+1}")),
            ..Enumerable.Range(0,8).Select(i=>"M16_003_FIRMWARE_DENIED index="+i+" status=ServicesUnavailable providers=1 outputs=unchanged"),
            "M16_003_STOP iterations=4 reads=5 first=1 last=4001 interval=1000 firmware=revoked providers=1 shutdown=owned-controller","CLROS_M16_003_READY","TEST_PASS m16-003"];
        var rows=NativeAotSocketIntegrationTests.Envelope(true).ToList();int owned=QemuContracts.M03OwnedSubsystemMarkers.Count;
        rows.RemoveRange(owned,31);rows.InsertRange(owned,[..staged,..startup,..service]);
        int first=rows.IndexOf("TEST_START m16-009"),last=rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(first+1,last-first-1);rows.Insert(first+1,"parent-only leaf deliberately unchecked");
        void Set(string prefix,string value)=>rows[rows.FindIndex(r=>r.StartsWith(prefix,StringComparison.Ordinal))]=value;
        Set("M16_011_PEER ","M16_011_PEER mac=90525193470466 sent=1 received=1 start=4500000 end=5000000");
        Set("M16_011_WIRE ","M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Set("M16_011_END ","M16_011_END polls=1 received=0 copied=0 fresh=0 retired=0 start=4400000 end=500000000 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Set("M07_026_ROW stage=4 ","M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        int terminal=rows.IndexOf("CLROS_M07_026_READY");
        rows.InsertRange(terminal,["M16_027_END owner=16781312 epoch=272 reaped=1 transfers=60 irqs=0 submitted=100 completed=100 submittedBytes=512 completedBytes=512 flushAcknowledgedBytes=512 clock=500000001 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=retired dma=zero reservation=retained",
            "CLROS_M16_027_READY","TEST_PASS m16.027"]);
        return rows.ToArray();
    }

    [HostTest("disk startup copied BCL write and verify geometry requires four loads TLS and67 ranges without preparing a program")]
    private static void BclStartupValues()
    {
        foreach(string phase in new[]{"write","verify"})
        {
            var (package,values)=CopiedBclPackage(phase);string[] rows=MappedStartupRows(package,67),before=rows.ToArray();byte[] original=package.ToArray();
            string[] parent=InspectStartupValues(rows,package,true,false,false,true,true,values);
            HostCheck.True(parent.SequenceEqual(rows[..^7]) && rows.SequenceEqual(before) && package.AsSpan().SequenceEqual(original) &&
                values.Phase==phase && values.Image.Loads.Count==4 && values.Image.Tls is {FileSize:24,MemorySize:264} &&
                !values.InstructionIndexAdmitted && !values.ExecutionAuthority && !values.GrantsOriginalPublicationGuestOrPersistenceAuthority);
            DenyStartup(()=>QemuTranscripts.ValidateLinuxStartupTail(rows,package,diskFiles:true,writableDisk:true,logUpload:true));
            DenyStartup(()=>QemuTranscripts.InspectLinuxStartupRangeValues(67,4,diskFiles:true,writableDisk:true));
        }
    }

    [HostTest("disk startup BCL copied admission rejects wrong selections phase package and mutated bytes before consuming rows")]
    private static void BclStartupAdmission()
    {
        var (package,values)=CopiedBclPackage("write");var (verify,verification)=CopiedBclPackage("verify");string[] rows=MappedStartupRows(package,67);
        foreach(var flags in new (bool Disk,bool Static,bool Exit,bool Writable,bool Log)[]{(false,false,false,true,true),
            (true,true,false,true,true),(true,false,true,true,true),(true,false,false,false,true),(true,false,false,true,false)})
            DenyStartup(()=>InspectStartupValues(rows,package,flags.Disk,flags.Static,flags.Exit,flags.Writable,flags.Log,values),"m16_004_disk_selection");
        DenyStartup(()=>InspectStartupValues(rows,package,true,false,false,true,true,verification),"m16_004_log_upload_package");
        DenyStartup(()=>InspectStartupValues(MappedStartupRows(verify,67),verify,true,false,false,true,true,values),"m16_004_log_upload_package");
        byte[] changed=package.ToArray();changed[^1]^=1;
        DenyStartup(()=>InspectStartupValues(rows,changed,true,false,false,true,true,values),"m16_004_log_upload_package");
        byte[] mechanical=LinuxWritableDiskProofFixture.CreatePackage();
        DenyStartup(()=>InspectStartupValues(MappedStartupRows(mechanical,65),mechanical,true,false,false,true,true,values),"m16_004_log_upload_package");
    }

    [HostTest("disk startup BCL binds actual TLS image and stack records and rejects neighbouring retained range counts")]
    private static void BclStartupRecordRejection()
    {
        var (package,values)=CopiedBclPackage("write");string[] rows=MappedStartupRows(package,67);
        foreach(int count in new[]{0,64,65,66,68,135})
            DenyStartup(()=>InspectStartupValues(rows.Select(r=>r.Replace("ranges=67","ranges="+count,StringComparison.Ordinal)).ToArray(),package,true,false,false,true,true,values),"m16_004_table_geometry");
        foreach(var change in new[]{("loads=4","loads=2"),("tlsFile=24","tlsFile=0"),("tlsMemory=264","tlsMemory=0"),
            ("entry=4231031","entry=4231032"),("argc=4","argc=3"),("envc=0","envc=1"),
            ("seedCleared=32","seedCleared=0"),("restored=true","restored=false"),("entry=false","entry=true")})
        {
            HostCheck.True(rows.Any(r=>r.Contains(change.Item1,StringComparison.Ordinal)),"mutation must change its intended row");
            DenyStartup(()=>InspectStartupValues(rows.Select(r=>r.Replace(change.Item1,change.Item2,StringComparison.Ordinal)).ToArray(),package,true,false,false,true,true,values));
        }
        HostCheck.True(rows.Length==QemuContracts.M03OwnedSubsystemMarkers.Count+14);
    }

    [HostTest("disk startup null prepared program preserves exact mechanical30 and logupload40 two-load65 selection")]
    private static void WritableStartupLegacy()
    {
        foreach(bool log in new[]{false,true})
        {
            byte[] package=log?LinuxWritableDiskProofFixture.CreateLogUploadPackage():LinuxWritableDiskProofFixture.CreatePackage();
            string[] rows=MappedStartupRows(package,65);
            HostCheck.Equal(rows.Length-7,QemuTranscripts.ValidateLinuxStartupTail(rows,package,diskFiles:true,writableDisk:true,logUpload:log).Length);
            foreach(int count in new[]{64,66,67,135})
                DenyStartup(()=>QemuTranscripts.ValidateLinuxStartupTail(MappedStartupRows(package,(ulong)count),package,diskFiles:true,writableDisk:true,logUpload:log));
            DenyStartup(()=>QemuTranscripts.ValidateLinuxStartupTail(rows,package,diskFiles:true,writableDisk:true,logUpload:!log));
        }
    }

    // These synthetic bytes exercise only the copied-value consumer. They
    // cannot pass the public fixed-ELF/prepared-program factory.
    private static (byte[] Package,NativeAotLogUploadContract.ClrosPackageValues Values) CopiedBclPackage(string phase)
    {
        byte[] elf=new byte[NativeAotLogUploadContract.ClrosElfBytes];new byte[]{0x7f,69,76,70,2,1,1}.CopyTo(elf,0);
        void W16(int at,ushort v)=>BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at),v);
        void W32(int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at),v);
        void W64(int at,ulong v)=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at),v);
        W16(16,2);W16(18,62);W32(20,1);W64(24,4231031);W64(32,64);W16(52,64);W16(54,56);W16(56,11);
        void Segment(int at,uint type,uint flags,ulong offset,ulong address,ulong file,ulong memory,ulong alignment)
        {W32(at,type);W32(at+4,flags);W64(at+8,offset);W64(at+16,address);W64(at+32,file);W64(at+40,memory);W64(at+48,alignment);}
        Segment(64,1,4,0,4194304,748,748,4096);Segment(120,1,5,4096,4198400,893584,893584,4096);
        Segment(176,1,4,901120,5095424,442088,442088,4096);Segment(232,1,6,1345320,5539624,9616,189328,4096);
        Segment(288,7,4,1345320,5539624,24,264,8);
        string digest=Convert.ToHexString(SHA256.HashData(elf));HostCheck.True(digest!=NativeAotLogUploadContract.ClrosElfSha256);
        byte[] package=BuildBclPackage(elf,phase,digest);return(package,InspectBclPackage(package,phase,digest));
    }

    internal static unsafe string[] MappedStartupRows(byte[] package,ulong ranges)
    {
        var view=LinuxElfPackage.Validate(package);HostCheck.True(view.Succeeded);
        byte* entropy=stackalloc byte[32];new Span<byte>(entropy,32).Fill(1);
        byte* stack=(byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes,4096);
        try
        {
            LinuxElfStartupPlan startup;LinuxElfLoadPlan image;
            fixed(byte* p=package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready,LinuxElfLoader.Validate(p+view.PayloadOffset,view.PayloadBytes,view.Manifest.MemoryBytes,view.Manifest.StackBytes,out image));
                HostCheck.Equal(LinuxElfStartupStatus.Ready,LinuxElfStartup.Build(p+view.ManifestOffset,view.ManifestBytes,p+view.PayloadOffset,view.PayloadBytes,
                    entropy,32,stack,view.Manifest.StackBytes,0x70000000UL-view.Manifest.StackBytes,0,out startup));
            }
            ulong loadPages=0;for(uint i=0;i<image.LoadCount;i++){uint n=i*5;loadPages+=(((image.Loads[n]+image.Loads[n+3]+4095)&~4095UL)-(image.Loads[n]&~4095UL))/4096;}
            return [..Enumerable.Repeat("parents checked separately",QemuContracts.M03OwnedSubsystemMarkers.Count+7),"TEST_START m16-004",
                FormattableString.Invariant($"M16_004_IMAGE base={image.ImageStart} bytes={image.ImageBytes} loads={image.LoadCount} entry={image.Entry} phdr={image.ProgramHeaders} tlsFile={image.TlsFileBytes} tlsMemory={image.TlsMemoryBytes}"),
                FormattableString.Invariant($"M16_004_STACK start={startup.StackStart} bytes={startup.StackBytes} sp={startup.StackPointer} argc={startup.ArgumentCount} envc={startup.EnvironmentCount} random={startup.RandomAddress} entropyBytes=32 seedCleared=32"),
                FormattableString.Invariant($"M16_004_TABLES root=16777216 used=17 leaves={loadPages+view.Manifest.StackBytes/4096+128} ranges={ranges}"),
                "M16_004_READBACK old=33554432 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false",
                "CLROS_M16_004_READY","TEST_PASS m16-004"];
        }
        finally{NativeMemory.AlignedFree(stack);}
    }

    private static void DenyStartup(Action action,string? reason=null)
    {
        string? failure=null;
        try{action();}catch(Exception error)when(error is ToolFailure or InvalidDataException or ArgumentException){failure=error.Message;}
        HostCheck.True(failure is not null && (reason is null || failure.Contains(reason,StringComparison.Ordinal)),"startup must reject: "+reason+" actual="+failure);
    }

    [HostTest("disk parent admits only the exact typed pending-reset terminal and preserves ordinary drained rejection")]
    private static void PendingReset()
    {
        var (device, image) = LinuxDiskRaceTranscriptTests.ExitPendingDeviceFixture();
        var rows = Envelope().ToList(); rows.InsertRange(rows.IndexOf("TEST_START m16-009") + 1, device);
        int end = rows.IndexOf(End); rows[end] = End.Replace("completed=40 copied=40", "completed=39 copied=39", StringComparison.Ordinal);
        int networkEnd = rows.FindIndex(r => r.StartsWith("M16_011_END ", StringComparison.Ordinal));
        rows[networkEnd] = rows[networkEnd].Replace(" end=1 ", " end=1000 ", StringComparison.Ordinal);
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage("ExitPending"); string[] before = rows.ToArray();
        var inspected = QemuTranscripts.InspectLinuxDiskExitPendingNetworkForTests(before, package, image);
        HostCheck.True(inspected.Device.Counters.Submitted == 40 && inspected.Device.Counters.Completed == 39 &&
            inspected.Device.Counters.Copied == 39 && inspected.Device.Cancelled == 0 && rows.SequenceEqual(before));
        Reject(before, "disk_parent_counters"); // Ordinary and018 use this same drained default.
        void Deny(string[] changed)
        {
            bool rejected = false;
            try { _ = QemuTranscripts.InspectLinuxDiskExitPendingNetworkForTests(changed, package, image); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        Deny(rows.Select(r => r == rows[end] ? r.Replace("copied=39", "copied=38", StringComparison.Ordinal) : r).ToArray());
        Deny(rows.Select(r => r == rows[end] ? r.Replace("bootstrapCompleted=37", "bootstrapCompleted=36", StringComparison.Ordinal) : r).ToArray());
        Deny(rows.Select(r => r == rows[end] ? r.Replace("fsReads=6", "fsReads=5", StringComparison.Ordinal) : r).ToArray());
        Deny(rows.Select(r => r.StartsWith("M16_023_IO kind=0 ", StringComparison.Ordinal) && r.Contains("generation=40 ", StringComparison.Ordinal) ?
            r.Replace("sector=1000 ", "sector=999 ", StringComparison.Ordinal) : r).ToArray());
        Deny(rows.Select(r => r.StartsWith("M16_025_EXIT_PENDING stage=5 ", StringComparison.Ordinal) ?
            r.Replace(" reset=1 ", " reset=0 ", StringComparison.Ordinal) : r).ToArray());
        Deny(rows.Select(r => r == rows[networkEnd] ? r.Replace(" end=1000 ", " end=1 ", StringComparison.Ordinal) : r).ToArray());
    }

    private const string End = "M16_025_END submitted=40 completed=40 copied=40 bootstrapCompleted=37 bootstrapCopied=37 fsReads=6 cacheHits=20 cacheMisses=2 device=retired fs=retired dma=zero descriptors=closed reservation=retained";
    private static string[] Envelope()
    {
        var rows = NativeAotSocketIntegrationTests.Envelope(true).ToList();
        int first = rows.IndexOf("TEST_START m16-009"), last = rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(first + 1, last - first - 1);
        void Set(string prefix, string value) => rows[rows.FindIndex(row => row.StartsWith(prefix, StringComparison.Ordinal))] = value;
        Set("M16_011_WIRE ", "M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Set("M16_011_END ", "M16_011_END polls=1 received=0 copied=0 fresh=0 retired=0 start=0 end=1 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Set("M07_026_ROW stage=4 ", "M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        rows.InsertRange(rows.IndexOf("CLROS_M07_026_READY"), [End, "CLROS_M16_023_READY", "TEST_PASS m16.023",
            "CLROS_M16_024_READY", "TEST_PASS m16.024", "CLROS_M16_025_READY", "TEST_PASS m16.025"]);
        return rows.ToArray();
    }
    private static void Reject(string[] rows, string reason)
    {
        string? failure = null;
        try { QemuTranscripts.InspectLinuxDiskNetworkForTests(rows); } catch (ToolFailure error) { failure = error.Message; }
        HostCheck.True(failure?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + failure);
    }

    [HostTest("disk parent envelope requires drained disk counts and original NIC teardown without TCP")]
    private static void Complete()
    {
        string[] rows = Envelope(), before = rows.ToArray();
        QemuTranscripts.InspectLinuxDiskNetworkForTests(rows); HostCheck.True(rows.SequenceEqual(before));
        // The old mechanical envelope must still reject the disk-specific tail.
        bool rejected = false;
        try { QemuTranscripts.InspectSignalEpollNetworkForTests(rows); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("disk parent envelope rejects missing duplicate moved or retained disk cleanup")]
    private static void Cleanup()
    {
        string[] rows = Envelope(); int at = Array.IndexOf(rows, End);
        for (int i = at; i < at + 7; i++)
        {
            Reject(rows.Where((_, index) => index != i).ToArray(), i == at ? "disk_parent_cleanup" : "socket_parent_order_");
            Reject([.. rows[..i], rows[i], .. rows[i..]], i == at ? "socket_parent_order_" : "socket_parent_order_");
        }
        Reject(rows.Select(row => row.Replace("device=retired fs=retired", "device=retained fs=retired", StringComparison.Ordinal)).ToArray(), "disk_parent_cleanup");
        Reject([.. rows, End], "socket_parent_extra_record");
    }

    [HostTest("disk parent envelope rejects mismatched bootstrap final copy and filesystem counters")]
    private static void Counters()
    {
        foreach (var change in new[] { ("submitted=40", "submitted=41"), ("completed=40", "completed=39"),
            ("copied=40", "copied=39"), ("bootstrapCompleted=37", "bootstrapCompleted=40"),
            ("bootstrapCopied=37", "bootstrapCopied=36"), ("fsReads=6", "fsReads=5"), ("cacheMisses=2", "cacheMisses=7") })
            Reject(Envelope().Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray(), "disk_parent_counters");
        Reject(Envelope().Select(row => row.Replace("tcpCopied=0", "tcpCopied=1", StringComparison.Ordinal)).ToArray(), "socket_network_accounting");
        Reject(Envelope().Select(row => row.Replace("stage=5 a=8 b=0", "stage=5 a=8 b=1", StringComparison.Ordinal)).ToArray(), "socket_nic_cleanup");
    }

    [HostTest("disk parent envelope rejects excessive nonascii empty and unknown input")]
    private static void Bounds()
    {
        Reject([], "disk_parent_input"); Reject(["bad\0row"], "disk_parent_input");
        Reject([new string('x', 1537)], "disk_parent_input"); Reject(new string[131073], "disk_parent_input");
        Reject([.. Envelope(), "unknown"], "socket_parent_extra_record");
    }

    [HostTest("disk startup admits exact Progress65 only through its explicit copied-value selection")]
    private static void ProgressStartup()
    {
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage("Progress"), original = package.ToArray();
        string[] rows = MappedStartupRows(package, 65), before = rows.ToArray();
        // Admission of copied rows does not execute the package or qualify a guest.
        string[] parent = QemuTranscripts.ValidateLinuxStartupTail(rows, package, diskFiles: true, diskProgress: true);
        HostCheck.True(parent.SequenceEqual(before[..^7]));
        HostCheck.True(rows.SequenceEqual(before) && package.SequenceEqual(original));
    }

    [HostTest("disk Progress startup rejects conflicting selections other canonical packages and changed bytes")]
    private static void ProgressStartupAdmission()
    {
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage("Progress");
        string[] rows = MappedStartupRows(package, 65);
        DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows, package, diskFiles: true));
        foreach (var flags in new[]
        {
            (Disk: false, Static: false, Exit: false, Writable: false, Log: false),
            (Disk: true, Static: true, Exit: false, Writable: false, Log: false),
            (Disk: true, Static: false, Exit: true, Writable: false, Log: false),
            (Disk: true, Static: false, Exit: false, Writable: true, Log: false),
            (Disk: true, Static: false, Exit: false, Writable: false, Log: true),
            (Disk: true, Static: false, Exit: false, Writable: true, Log: true)
        })
            DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows, package,
                diskFiles: flags.Disk, diskStatic: flags.Static, diskExitPending: flags.Exit,
                writableDisk: flags.Writable, logUpload: flags.Log, diskProgress: true), "m16_004_disk_selection");
        foreach (string variant in LinuxDiskRaceProofFixture.Variants.Where(v => v != "Progress"))
            DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows,
                LinuxDiskRaceProofFixture.CreatePackage(variant), diskFiles: true, diskProgress: true));
        foreach (byte[] other in new[] { LinuxDiskFileProofFixture.CreatePackage(),
            LinuxWritableDiskProofFixture.CreatePackage(), LinuxWritableDiskProofFixture.CreateLogUploadPackage() })
            DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows, other, diskFiles: true, diskProgress: true));
        byte[] changed = package.ToArray(); changed[^1] ^= 1;
        DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows, changed, diskFiles: true, diskProgress: true));
        DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(rows, package[..^1], diskFiles: true, diskProgress: true));
    }

    [HostTest("disk Progress startup retains exact65 and all image stack and table geometry guards")]
    private static void ProgressStartupGeometry()
    {
        byte[] package = LinuxDiskRaceProofFixture.CreatePackage("Progress");
        string[] rows = MappedStartupRows(package, 65), before = rows.ToArray();
        foreach (int ranges in new[] { 64, 66, 67, 135 })
            DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(MappedStartupRows(package, (ulong)ranges),
                package, diskFiles: true, diskProgress: true), "m16_004_table_geometry");
        foreach (var change in new[]
        {
            (From: "loads=2", To: "loads=3", Reason: "m16_004_image_geometry"),
            (From: "tlsFile=0 tlsMemory=0", To: "tlsFile=1 tlsMemory=1", Reason: "m16_004_image_geometry"),
            (From: "root=16777216", To: "root=0", Reason: "m16_004_table_geometry"),
            (From: "argc=2", To: "argc=0", Reason: "m16_004_stack_geometry")
        })
        {
            string[] malformed = rows.Select(r => r.Replace(change.From, change.To, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!malformed.SequenceEqual(rows), "Progress geometry control must change its selected field");
            DenyStartup(() => QemuTranscripts.ValidateLinuxStartupTail(malformed, package,
                diskFiles: true, diskProgress: true), change.Reason);
        }
        HostCheck.True(rows.SequenceEqual(before));
    }

    [HostTest("disk startup admits exact ExitPending bytes only through its explicit mechanical branch")]
    private static void ExitPendingStartup()
    {
        byte[] package=LinuxDiskRaceProofFixture.CreatePackage("ExitPending");
        string[] rows=StartupRows(package),before=rows.ToArray();
        HostCheck.Equal(rows.Length-7,QemuTranscripts.ValidateLinuxStartupTail(rows,package,diskFiles:true,diskExitPending:true).Length);
        HostCheck.True(rows.SequenceEqual(before));
        void Deny(byte[] candidate,bool disk=true,bool web=false,bool pending=true,string[]? input=null)
        {
            bool denied=false;try{_=QemuTranscripts.ValidateLinuxStartupTail(input??rows,candidate,disk,web,pending);}
            catch(ToolFailure){denied=true;}HostCheck.True(denied);
        }
        Deny(package,pending:false);Deny(package,disk:false);Deny(package,web:true);
        Deny(LinuxDiskFileProofFixture.CreatePackage());
        foreach(string variant in LinuxDiskRaceProofFixture.Variants.Where(v=>v!="ExitPending"))Deny(LinuxDiskRaceProofFixture.CreatePackage(variant));
        byte[] corrupt=package.ToArray();corrupt[^1]^=1;Deny(corrupt);
        foreach(int ranges in new[]{64,66,135})Deny(package,input:rows.Select(r=>r.Replace("ranges=65","ranges="+ranges,StringComparison.Ordinal)).ToArray());
    }

    [HostTest("disk startup accepts exactly65 ranges only for the canonical mechanical package")]
    private static void StartupRanges()
    {
        byte[] package=LinuxDiskFileProofFixture.CreatePackage();string[] rows=StartupRows(package);
        HostCheck.Equal(rows.Length-7,QemuTranscripts.ValidateLinuxStartupTail(rows,package,diskFiles:true).Length);
        void Denied(string[] input,byte[] candidate,bool disk)
        {
            bool rejected=false;try{_=QemuTranscripts.ValidateLinuxStartupTail(input,candidate,diskFiles:disk);}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected);
        }
        Denied(rows,package,false);
        foreach(int count in new[]{64,66,135})Denied(rows.Select(r=>r.Replace("ranges=65","ranges="+count,StringComparison.Ordinal)).ToArray(),package,true);
        Denied(rows,LinuxThreadProofFixture.CreatePackage("Threads"),true);
    }

    internal static unsafe string[] StartupRows(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        byte* stack = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try
        {
            LinuxElfStartupPlan startup;
            fixed (byte* bytes = package)
                HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(bytes + view.ManifestOffset, view.ManifestBytes,
                    bytes + view.PayloadOffset, view.PayloadBytes, seed, 32, stack, view.Manifest.StackBytes,
                    0x70000000UL - view.Manifest.StackBytes, 0, out startup));
            string stackRow = string.Create(CultureInfo.InvariantCulture,
                $"M16_004_STACK start={startup.StackStart} bytes={startup.StackBytes} sp={startup.StackPointer} argc={startup.ArgumentCount} envc={startup.EnvironmentCount} random={startup.RandomAddress} entropyBytes=32 seedCleared=32");
            return [.. Enumerable.Repeat("parents checked separately", QemuContracts.M03OwnedSubsystemMarkers.Count + 7),
                "TEST_START m16-004", "M16_004_IMAGE base=4194304 bytes=28672 loads=2 entry=4194816 phdr=4194368 tlsFile=0 tlsMemory=0",
                stackRow, "M16_004_TABLES root=16777216 used=17 leaves=1737 ranges=65",
                "M16_004_READBACK old=33554432 restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false",
                "CLROS_M16_004_READY", "TEST_PASS m16-004"];
        }
        finally { NativeMemory.AlignedFree(stack); }
    }
}
