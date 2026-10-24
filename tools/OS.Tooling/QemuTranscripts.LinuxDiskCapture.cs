using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied association between one positive file read and its exact
    /// original call/completion positions. Sectors are immutable logical-order
    /// physical LBAs; constructing this value grants no execution authority.</summary>
    public readonly record struct DiskReadWindow(ulong Ordinal, int CallRecordIndex, int CompletionRecordIndex,
        string Path, ulong Offset, uint Bytes, IReadOnlyList<ulong> Sectors);

    /// <summary>Copied value joins only. Parent values, canonical leaf, independent
    /// image inventory, every observed sector byte, read windows, timing period
    /// and exact instruction boundaries are checked. This is not CPU instruction
    /// emulation, user-buffer replay, firmware framing, original-runner, packet,
    /// source/native provenance, device identity or complete-guest authority.</summary>
    public sealed class LinuxDiskCaptureInspection
    {
        public WebParentReport Parent { get; }
        public LinuxDiskFileLeafReport Leaf { get; }
        public NativeDiskReadObservations.Inspection Device { get; }
        public string ImageSha256 { get; }
        public int LeafStartIndex { get; }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary { get; }
        public IReadOnlyList<int> IoRecordIndices { get; }
        public IReadOnlyList<DiskReadWindow> ReadWindows { get; }
        public int InstructionIrqs { get; }
        public bool CheckedRunnerNativeFirmwarePackets => false;
        public bool ReplayedArbitraryInstructionsOrUserBuffers => false;
        internal LinuxDiskCaptureInspection(WebParentReport parent, LinuxDiskFileLeafReport leaf,
            NativeDiskReadObservations.Inspection device, string hash, int first,
            LinuxDiskAuxiliary[] auxiliary, int[] indexes, DiskReadWindow[] windows, int irqs)
        { Parent = parent; Leaf = leaf; Device = device; ImageSha256 = hash; LeafStartIndex = first;
          Auxiliary = Array.AsReadOnly(auxiliary); IoRecordIndices = Array.AsReadOnly(indexes);
          ReadWindows = Array.AsReadOnly(windows); InstructionIrqs = irqs; }
    }

    /// <summary>Synchronously borrow complete already-framed rows, canonical
    /// package, immutable raw image and externally verified fixture receipt.
    /// The caller holds originalImagePath unchanged; independent ReadBack must
    /// agree with supplied bytes and receipt. Output retains no mutable caller
    /// arrays, image aliases, receipt arrays or filesystem ownership. Every row
    /// is accounted by the exact parent/envelope or indexed leaf, never filtered
    /// into a guessed accepted transcript. Invalid observations throw ToolFailure.
    /// Caller must independently authenticate FirmwareProtocol and its original
    /// files/process/native closure, then NIC packets and final held-input checks.
    /// Only mechanical025 is supported; no Blazor, HTTP, writes or negative guest.
    /// </summary>
    public static unsafe LinuxDiskCaptureInspection InspectLinuxDiskCaptureValues(
        IReadOnlyList<string> supplied, byte[] package, ReadOnlySpan<byte> rawImage,
        WebRootImageFixture.Receipt receipt, string originalImagePath)
    {
        int count = supplied?.Count ?? 0;
        if (receipt is null) throw new ToolFailure("disk_capture_input");
        Need(count is >0 and <=131072 && package is not null, "disk_capture_input");
        string[] rows = new string[count];
        for (int i = 0; i < count; i++)
        { string? row = supplied![i]; Need(row is { Length: >0 and <=1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_capture_row"); rows[i] = row!; }
        LinuxDiskFileProofFixture.RequirePackage(package!);
        var image = ValidateLinuxDiskCaptureImage(rawImage, receipt, originalImagePath);
        string imageHash = image.Sha256;
        var fragmented = image.Files.Single(f => f.Path == "/app/wwwroot/fixture/fragmented.bin");
        var prepared = ValidateLinuxDiskCaptureParents(rows, package!);
        var parent = prepared.Parent; var clock = prepared.Clock;
        var network = prepared.Network;
        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && rows.Count(r=>r == "TEST_START m16-009") == 1 && rows.Count(r=>r == "TEST_PASS m16-009") == 1, "disk_capture_leaf_extent");
        string[] leafRows = rows[first..(last + 1)];
        var leaf = InspectLinuxDiskCaptureLeafForTests(rows, package!);
        var auxiliary = leaf.Auxiliary.Select(a=>new LinuxDiskAuxiliary(first + a.RecordIndex, a.Text)).ToArray();
        var binding = ValidateLinuxDiskCaptureBinding(rows, prepared, rawImage.Length);
        var disk = binding.Disk; var budget = binding.Budget; var end = binding.End;

        var ioRows = auxiliary.Where(a=>a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)).ToArray();
        var device = NativeDiskReadObservations.Inspect(ioRows.Select(a=>a.Text).ToArray(), rawImage,
            new(disk["owner"],disk["epoch"],disk["bootstrapCompleted"],disk["bootstrapCopied"],end["submitted"],end["completed"],end["copied"]));
        Need(device.Cancelled == 0, "disk_capture_positive_no_cancellations");
        var completions = leaf.Completions.ToDictionary(c=>c.Ordinal);
        for (int i = 0; i < ioRows.Length; i++)
        {
            Need(device.Rows[i].Clock >= disk["clock"] && device.Rows[i].Clock <= network.End["end"], "disk_capture_io_lifetime");
            Need(device.Rows[i].Sector >= image.PartitionFirstLba && device.Rows[i].Sector <= image.PartitionLastLba, "disk_capture_io_partition");
            Need(leaf.Completions.Count(c=>first+c.CallRecordIndex < ioRows[i].RecordIndex && ioRows[i].RecordIndex < first+c.CompletionRecordIndex) == 1, "disk_capture_io_owned_window");
        }
        // IO clocks are fresh flush samples. CALL/COMPLETE clocks are the kernel
        // table's last observation. Never compare them as one serial clock stream.
        decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * clock["period"] / 1_000_000);
        foreach (var a in auxiliary)
        {
            if (a.Text.StartsWith("M16_008_SYSCALL_TIME ",StringComparison.Ordinal))
            {
                var t = ReadRecord(a.Text,"M16_008_SYSCALL_TIME","identity:dec","ordinal:dec","nr:dec","entered:dec","admitted:dec","dispatched:dec","prepared:dec","periodFs:dec");
                Need(t["periodFs"] == clock["period"] && Nanos(t["entered"]) >= budget["started"] &&
                    Nanos(t["prepared"]) <= network.End["end"], "disk_capture_syscall_timing_lifetime");
            }
            else if (a.Text.StartsWith("M16_008_TIMING ",StringComparison.Ordinal))
            {
                var t = ReadRecord(a.Text,"M16_008_TIMING","identity:dec","epoch:dec","start:dec","arm:dec","capture:dec","ready:dec","periodFs:dec");
                Need(t["periodFs"] == clock["period"] && Nanos(t["start"]) >= budget["started"] &&
                    Nanos(t["ready"]) <= network.End["end"], "disk_capture_irq_timing_lifetime");
                var irqRows = auxiliary.Where(v=>v.RecordIndex < a.RecordIndex && v.Text.StartsWith("M16_009_IRQ ",StringComparison.Ordinal)).ToArray();
                Need(irqRows.Length != 0,"disk_capture_timing_irq_missing");
                var irq = ReadRecord(irqRows[^1].Text,"M16_009_IRQ","identity:dec","epoch:dec","nanos:dec","rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","valueBits:dec");
                Need(irq["epoch"] == t["epoch"] && Nanos(t["capture"]) <= irq["nanos"] && irq["nanos"] <= Nanos(t["ready"]), "disk_capture_irq_timing_capture");
            }
        }

        var expected = DiskFileCalls();
        Dictionary<uint, DiskCaptureDescription> fds = []; List<DiskReadWindow> windows = [];
        var packageView = OS.Manifest.LinuxElfPackage.Validate(package!);
        string PathAt(ulong address)
        {
            Need(address >= LinuxDiskFileProofFixture.ImageBase && address - LinuxDiskFileProofFixture.ImageBase < LinuxDiskFileProofFixture.ElfBytes, "disk_capture_path_address");
            int at = checked((int)(packageView.PayloadOffset + address - LinuxDiskFileProofFixture.ImageBase));
            int length = Array.IndexOf(package!, (byte)0, at, Math.Min(256, package!.Length-at)) - at;
            Need(length is >0 and <=255, "disk_capture_path_terminator"); return System.Text.Encoding.ASCII.GetString(package!,at,length);
        }
        for (int i = 0; i < expected.Length; i++)
        {
            var c = expected[i]; ulong ordinal = (ulong)i + 1;
            if (c.Number is 2 or 257 && c.Value >= 0)
            {
                string path = PathAt(c.Number == 2 ? c.A0 : c.A1);
                var file = image.Files.Single(f=>f.Path == path); fds.Add((uint)c.Value,new(file));
            }
            else if (c.Number == 72 && c.Value >= 0) fds.Add((uint)c.Value,fds[(uint)c.A0]);
            else if (c.Number == 3 && c.Value == 0) Need(fds.Remove((uint)c.A0), "disk_capture_close_model");
            else if (c.Number == 8 && c.Value >= 0) fds[(uint)c.A0].Position = (ulong)c.Value;
            else if (c.Number is 0 or 17 && c.Value > 0)
            {
                var d = fds[(uint)c.A0]; ulong offset = c.Number == 17 ? c.A3 : d.Position;
                Need(d.File.Path == fragmented.Path && offset <= d.File.Bytes && (ulong)c.Value <= d.File.Bytes-offset, "disk_capture_read_extent");
                var allSectors = DiskCaptureFileSectors(d.File,rawImage.Length);
                int startSector = (int)(offset/512), sectors = (int)((offset%512+(ulong)c.Value+511)/512);
                ulong[] wanted = allSectors.Skip(startSector).Take(sectors).ToArray();
                var completion = completions[ordinal];
                ulong[] observed = ioRows.Select((a,n)=>(a,n)).Where(v=>device.Rows[v.n].Kind == NativeDiskReadObservations.Kind.Copied &&
                    v.a.RecordIndex > first+completion.CallRecordIndex && v.a.RecordIndex < first+completion.CompletionRecordIndex)
                    .Select(v=>device.Rows[v.n].Sector).ToArray();
                // Demand data reads have no whole-file/data cache; each touched
                // file sector is one ordered block copy in this exact read window.
                Need(observed.SequenceEqual(wanted), "disk_capture_read_sector_window");
                windows.Add(new(ordinal,first+completion.CallRecordIndex,first+completion.CompletionRecordIndex,d.File.Path,offset,(uint)c.Value,Array.AsReadOnly(wanted)));
                if (c.Number == 0) d.Position += (ulong)c.Value;
            }
        }
        Need(fds.Count == 0 && windows.Count == expected.Count(c=>c.Number is 0 or 17 && c.Value > 0), "disk_capture_fd_read_inventory");

        ulong[] rips = LinuxDiskFileProofFixture.ExpectedReturnRips().ToArray();
        var starts = LinuxDiskFileProofFixture.MainInstructionRips().ToHashSet();
        int callCount = 0, irqs = 0; ulong lower = LinuxDiskFileProofFixture.ImageBase+LinuxDiskFileProofFixture.EntryOffset;
        for (int i = 0; i < leafRows.Length; i++)
        {
            string row = leafRows[i];
            if (row.StartsWith("M16_008_CALL ",StringComparison.Ordinal))
            {
                var c = expected[callCount++];
                if (!c.Pending) lower = rips[callCount-1];
            }
            else if (row.StartsWith("M16_008_RESUME ",StringComparison.Ordinal)) lower = rips[callCount-1];
            else if (row.StartsWith("M16_009_IRQ ",StringComparison.Ordinal))
            {
                var irq = ReadRecord(row,"M16_009_IRQ","identity:dec","epoch:dec","nanos:dec","rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","valueBits:dec");
                Need(callCount < expected.Length && starts.Contains(irq["rip"]) && irq["rip"] >= lower && irq["rip"] <= rips[callCount]-2, "disk_capture_irq_interval");
                lower = irq["rip"]; irqs++;
            }
        }
        Need(callCount == expected.Length && leaf.Epochs >= (ulong)irqs, "disk_capture_irq_inventory");
        return new(parent,leaf,device,imageHash,first,auxiliary,ioRows.Select(a=>a.RecordIndex).ToArray(),windows.ToArray(),irqs);
    }

    // Shared strict image/parent checks. Optional pending values originate only
    // from the exact same canonical ExitPending rows/image. No call projection,
    // record filtering or fabricated drained terminal is used for that branch.
    private static WebRootImageFixture.ImageInventory ValidateLinuxDiskCaptureImage(
        ReadOnlySpan<byte> rawImage, WebRootImageFixture.Receipt receipt, string originalImagePath)
    {
        Need(rawImage.Length is >=34*1024*1024 and <=WebRootImageFixture.MaximumImageBytes && rawImage.Length % 512 == 0, "disk_capture_image_bound");
        string imageHash = Convert.ToHexString(SHA256.HashData(rawImage));
        var image = WebRootImageFixture.ReadBack(originalImagePath); // Caller already holds this exact plain file.
        Need(image.Sha256 == imageHash && imageHash == receipt.FinalSha256 &&
            image.PartitionFirstLba == receipt.PartitionFirstLba && image.PartitionLastLba == receipt.PartitionLastLba &&
            receipt.Variant is "DiskA" or "DiskB", "disk_capture_image_identity");
        var expectedFiles = receipt.PreservedFiles.Concat(receipt.WebRootFiles).Append(receipt.Sentinel).ToArray();
        Need(expectedFiles.Length == image.Files.Length && expectedFiles.Length is >0 and <=4096 &&
            expectedFiles.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == expectedFiles.Length, "disk_capture_image_inventory");
        foreach (var f in expectedFiles)
        {
            var actual = image.Files.SingleOrDefault(x => x.Path == f.Path);
            Need(actual is not null && actual.Bytes == f.Bytes && actual.Sha256 == f.Sha256 && actual.Attributes == f.Attributes &&
                actual.FatDate == f.FatDate && actual.FatTime == f.FatTime && actual.Extents.SequenceEqual(f.Extents), "disk_capture_image_file");
        }
        int bpb = checked((int)(image.PartitionFirstLba * 512));
        Need(bpb <= rawImage.Length - 512 && BinaryPrimitives.ReadUInt16LittleEndian(rawImage.Slice(bpb + 11, 2)) == 512 &&
            rawImage[bpb + 13] == 1, "disk_capture_fixture_sector_clusters"); // Exact fixture geometry, not a general FS restriction.
        var fragmented = image.Files.Single(f => f.Path == "/app/wwwroot/fixture/fragmented.bin");
        Need(fragmented.Bytes == WebRootImageFixture.FragmentedFixtureBytes && fragmented.Extents.Length >=2 &&
            image.Files.Single(f => f.Path == "/app/wwwroot/fixture/empty.bin").Bytes == 0 &&
            image.Files.Any(f => f.Path == "/app/wwwroot/fixture/nested/info.txt"), "disk_capture_fixture_assets");
        var fragmentSectors = DiskCaptureFileSectors(fragmented, rawImage.Length);
        for (uint offset = 0; offset < fragmented.Bytes; offset++)
        {
            ulong sector = fragmentSectors[(int)(offset / 512)];
            byte actual = rawImage[checked((int)(sector * 512 + offset % 512))];
            byte wanted = unchecked((byte)((offset * 73 + 19) ^ (offset >> 9) ^ (offset >> 17)));
            Need(actual == wanted, "disk_capture_fragmented_pattern");
        }

        return image;
    }

    private sealed record DiskCaptureParents(WebParentReport Parent, SocketNetworkProof Network,
        Dictionary<string,ulong> Clock, ulong[] OldPages, ulong[] NicBinding);

    private static DiskCaptureParents ValidateLinuxDiskCaptureParents(string[] rows, byte[] package,
        LinuxDiskExitPendingDeviceValues? pending = null)
    {
        var network = SocketNetworkEnvelope(rows, true, mechanical: true, diskFiles: true, diskExitPending: pending);
        int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
        int parentsEnd = owned + 7 + M16LinuxStartupMarkers.Count + M16ServiceLoopMarkers.Count;
        int apicEnd = parentsEnd + QemuContracts.M05ApicSetupMarkers.Count - owned;
        int networkStart = apicEnd + QemuContracts.M07ArpReceiveMarkers.Count - QemuContracts.M05PciEnumerationMarkers.Count;
        string[] original = ValidateServiceLoopTail(rows[..parentsEnd], true);
        original = ValidateLinuxStartupTail(original, package, diskFiles: true, diskExitPending: pending is not null);
        original = ValidateLinuxElfTail(original, package!);
        string[] apic = [..original, ..rows[parentsEnd..apicEnd]];
        int[] varying = [3,9,10,19,20,21,22,23,24,..Enumerable.Range(66,10),77,78,82,83,87,88,89,93,94,
            ..QemuContracts.M05ApicSetupMarkers.Select((s,i)=>(s,i)).Where(x=>x.s.StartsWith("M05_APIC ", StringComparison.Ordinal)).Select(x=>x.i)];
        ExactInventory(apic, QemuContracts.M05ApicSetupMarkers, varying); ValidateAllocator(apic);
        AssertM03OwnedSubsystemRecords(original, AssertCopiedAcpiRecords(original));
        AssertM05PciEnumerationRecords(apic[..QemuContracts.M05PciEnumerationMarkers.Count]); AssertM05ApicSetupRecords(apic);
        string[] nic = [..apic[..QemuContracts.M05PciEnumerationMarkers.Count], ..rows[apicEnd..networkStart]];
        var arp = AssertM07ArpReceiveRecords(nic);
        var tables = ReadRecord(rows[owned + 10], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        const string restored = " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        var kernel = ReadRecord(rows[owned + 11][..^restored.Length], "M16_004_READBACK", "old:dec");
        const string clockSuffix = " source=owned-hpet";
        var clock = ReadRecord(rows[owned + 15][..^clockSuffix.Length], "M16_003_CLOCK", "frequency:dec", "period:dec", "recoveries:dec");
        const string stopSuffix = " firmware=revoked providers=1 shutdown=owned-controller";
        var service = ReadRecord(rows[parentsEnd - 3][..^stopSuffix.Length], "M16_003_STOP", "iterations:dec", "reads:dec", "first:dec", "last:dec", "interval:dec");
        Need(network.Proof.Peer["start"] >= decimal.Floor((decimal)service["last"] * clock["period"] / 1_000_000), "disk_capture_service_peer_clock");
        ulong[] anchor = PrefixRow(nic, "M07_NET D", 4), oldPages = PrefixRow(nic, "M07_ARP 2", 4), nicBinding = PrefixRow(nic, "M07_ARP 0", 4);
        Need(anchor[0] <= uint.MaxValue, "disk_capture_anchor");
        var liveDma = ReadRecord(rows[networkStart + 1], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
        var parent = new WebParentReport(networkStart + 3, tables["root"], kernel["old"], clock["frequency"], clock["period"],
            liveDma["a"], network.Proof.Peer["start"], network.Proof.Peer["end"], (uint)anchor[0], arp);

        return new(parent, network.Proof, clock, oldPages, nicBinding);
    }

    private sealed record DiskCaptureBinding(Dictionary<string,ulong> Disk, Dictionary<string,ulong> Budget,
        Dictionary<string,ulong> BudgetEnd, Dictionary<string,ulong> End);

    private static unsafe DiskCaptureBinding ValidateLinuxDiskCaptureBinding(string[] rows,
        DiskCaptureParents prepared, int imageBytes, LinuxDiskExitPendingDeviceValues? pending = null)
    {
        var parent=prepared.Parent; var network=prepared.Network;
        var oldPages=prepared.OldPages; var nicBinding=prepared.NicBinding;
        string Unique(string prefix) { var found = rows.Where(r=>r.StartsWith(prefix, StringComparison.Ordinal)).ToArray(); Need(found.Length == 1, "disk_capture_unique_" + prefix); return found[0]; }
        const string diskSuffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        string diskLine = Unique("M16_023_BOUND ");
        var disk = ReadRecord(diskLine[..^diskSuffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        var end = ReadDiskFileEnd(Unique("M16_025_END "), pending);
        Need(disk["epoch"] == 0x4d31365f30323301 && disk["capacity"] == (ulong)imageBytes / 512 &&
            disk["bootstrapCompleted"] == 37 && disk["bootstrapCopied"] == 37 &&
            end["bootstrapCompleted"] == disk["bootstrapCompleted"] && end["bootstrapCopied"] == disk["bootstrapCopied"], "disk_capture_binding_counters");
        Need(disk["pages"] == 90 && disk["owner"] <= 0x100000000UL - (ulong)sizeof(Block.State), "disk_capture_measured_layout");
        string threadSuffix = pending is null ? " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack" : " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string threadLine = Unique("M16_008_BOUND ");
        var thread = ReadRecord(threadLine[..^threadSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        var budget = ReadRecord(Unique("M16_008_BUDGET "), "M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        var budgetEnd = ReadRecord(Unique("M16_008_BUDGET_END "), "M16_008_BUDGET_END", "nanos:dec");
        Need(disk["clock"] >= parent.PeerEnd && budget["started"] >= disk["clock"] && budgetEnd["nanos"] <= network.End["end"], "disk_capture_lifetime");
        DistinctExtents([(parent.UserRoot,4096),(parent.KernelRoot,4096),(parent.Dma,32768),
            (thread["pool"],LinuxThreadProgram.SignalPoolPages*4096UL),(oldPages[0],8192),(nicBinding[3],16384),
            (disk["arena"],disk["pages"]*4096),(disk["bar"],16384),(disk["owner"],(ulong)sizeof(Block.State))], "disk_capture_live_extents");

        return new(disk,budget,budgetEnd,end);
    }

    private sealed class DiskCaptureDescription(WebRootImageFixture.DiskFile file)
    { internal readonly WebRootImageFixture.DiskFile File = file; internal ulong Position; }
    private static ulong[] DiskCaptureFileSectors(WebRootImageFixture.DiskFile file, int imageBytes)
    {
        List<ulong> sectors = [];
        foreach (var e in file.Extents)
        {
            Need(e.Clusters != 0 && e.FirstLba < (ulong)imageBytes/512 && e.Clusters <= (ulong)imageBytes/512-e.FirstLba, "disk_capture_file_sectors");
            for (uint n = 0; n < e.Clusters; n++) sectors.Add(e.FirstLba+n);
        }
        Need((ulong)sectors.Count == ((ulong)file.Bytes+511)/512 && sectors.Distinct().Count() == sectors.Count, "disk_capture_file_allocation");
        return sectors.ToArray();
    }
}
