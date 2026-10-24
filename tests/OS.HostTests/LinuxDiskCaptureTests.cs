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
                row=Set(Set(Set(Set(row,"inode",file.Inode),"sector",sectors[copied].Lba),"ioSector",sectors[copied].Lba),"clock",20_000_000+Field(row,"stage")*1000);
            }
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
}
