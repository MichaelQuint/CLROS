using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static uint WebThreadPoolPages => LinuxThreadProgram.CodePages + LinuxThreadProgram.MetadataPages + LinuxThreadProgram.SwitchPages + LinuxThreadProgram.MaximumWorkers * 18 + LinuxThreadProgram.IrqPages + 1;

    /// <summary>Join the complete original parent prefix to continuing web
    /// semantic replay. Borrowed package/prepared program/raw serial are not
    /// executed or mutated. Invalid framing, firmware bytes, parent records,
    /// package/startup geometry, roots, clocks or live DMA associations throw.
    /// Supplied controls add original-position transport/descriptor joins;
    /// omission retains the historical leaf scope, whose check flag is false.
    /// Production always supplies its mandatory checked runner reports.
    /// Independent native/source/packet/runner admission remains mandatory.
    /// Explicit eventOwned selects the leaf's exact named-wait checks and
    /// requires a race selection; omitted mode retains historical scope.</summary>

    public static (WebParentReport Parent, WebReplayReport Leaf) ValidateNativeAotWebCapturePrefix(string root, byte[] package, NativeAotWebProgram program, ReadOnlySpan<byte> raw, WebControlInspection? controls = null, WebSignalRaceSelection? signalRace = null, bool eventOwned = false)
    {
        Need(raw.Length is > FirmwarePrefixBytes and <= 8 * 1024 * 1024, "web_capture_extent");
        ReadOnlySpan<byte> start = "TEST_START m10.004\r\n"u8;
        int at = raw.IndexOf(start);
        Need(at >= FirmwarePrefixBytes && raw[at - 2] == 13 && raw[at - 1] == 10 && raw[(at + start.Length)..].IndexOf(start) < 0, "web_raw_start");
        var parent = InspectNativeAotWebParentForTests(package, raw[..at]);
        var leaf = ValidateNativeAotWebContinuingPrefix(root, package, program, raw[at..], parent.UserRoot, parent.KernelRoot, controls, signalRace, eventOwned);
        var framed = NativeAotThreadingOutput.ReadWeb(raw[at..]);
        string complete = framed.NormalizedTail[..(framed.NormalizedTail.Length - leaf.Observations.TrailingFragment.Length)];
        var rows = complete[..^2].Split("\r\n", StringSplitOptions.None);
        var budget = ReadRecord(rows.Single(r => r.StartsWith("M16_008_BUDGET ", StringComparison.Ordinal)), "M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        Need(budget["started"] >= parent.PeerEnd, "web_parent_clock_join");
        foreach (string row in rows.Where(r => r.StartsWith("M16_008_TIMING ", StringComparison.Ordinal) || r.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)))
            Need(row.EndsWith(" periodFs=" + parent.Period.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), "web_parent_timing_period");
        var bound = ReadRecord(rows.Single(r => r.StartsWith("M16_008_BOUND ", StringComparison.Ordinal))[..^" timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack".Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        DistinctExtents([(parent.UserRoot, 4096), (parent.KernelRoot, 4096), (parent.Dma, 32768), (bound["pool"], WebThreadPoolPages * 4096UL)], "web_parent_live_extents");
        return (parent, leaf);
    }

    /// <summary>Inspect value-only parent observations for focused controls.
    /// Exact raw firmware framing and supplied package geometry are checked;
    /// caller bytes grant no sealed-program or execution authority. Inputs are
    /// synchronous borrows. Explicit diskStatic requires the canonical unstaged
    /// package and its separate67-range startup. Default callers keep64 ranges.
    /// Invalid or unsupported observations throw.</summary>

    public static WebParentReport InspectNativeAotWebParentForTests(byte[] package, ReadOnlySpan<byte> raw, bool diskStatic = false)
    {
        Need(raw.Length is > FirmwarePrefixBytes and <= 1024 * 1024 && Convert.ToHexString(SHA256.HashData(raw[..FirmwarePrefixBytes])) == FirmwarePrefixSha256, "web_parent_firmware_prefix");
        string text = DecodeUtf8(raw[FirmwarePrefixBytes..].ToArray());
        Need(text.EndsWith("\r\n", StringComparison.Ordinal), "web_parent_record_end");
        string[] lines = text[..^2].Split("\r\n", StringSplitOptions.None);
        Need(lines.All(r => r.Length != 0 && !Controls.IsMatch(r)), "web_parent_record_bytes");
        int owned = QemuContracts.M03OwnedSubsystemMarkers.Count;
        int parentsEnd = owned + 7 + M16LinuxStartupMarkers.Count + M16ServiceLoopMarkers.Count;
        int apicEnd = parentsEnd + QemuContracts.M05ApicSetupMarkers.Count - owned;
        int networkEnd = apicEnd + QemuContracts.M07ArpReceiveMarkers.Count - QemuContracts.M05PciEnumerationMarkers.Count;
        Need(lines.Length == networkEnd + 3 && lines[networkEnd] == "TEST_START m07-026", "web_parent_record_count");
        string[] original = ValidateServiceLoopTail(lines[..parentsEnd], true);
        original = ValidateLinuxStartupTail(original, package, diskStatic: diskStatic);
        original = ValidateLinuxElfTail(original, package);
        string[] apic = [.. original, .. lines[parentsEnd..apicEnd]];
        int[] varying = [3, 9, 10, 19, 20, 21, 22, 23, 24, .. Enumerable.Range(66, 10), 77, 78, 82, 83, 87, 88, 89, 93, 94, .. QemuContracts.M05ApicSetupMarkers.Select((s, i) => (s, i)).Where(x => x.s.StartsWith("M05_APIC ", StringComparison.Ordinal)).Select(x => x.i)];
        ExactInventory(apic, QemuContracts.M05ApicSetupMarkers, varying);
        ValidateAllocator(apic);
        AssertM03OwnedSubsystemRecords(original, AssertCopiedAcpiRecords(original));
        AssertM05PciEnumerationRecords(apic[..QemuContracts.M05PciEnumerationMarkers.Count]);
        AssertM05ApicSetupRecords(apic);
        string[] nic = [.. apic[..QemuContracts.M05PciEnumerationMarkers.Count], .. lines[apicEnd..networkEnd]];
        var arp = AssertM07ArpReceiveRecords(nic);
        var dma = ReadRecord(lines[networkEnd + 1], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
        var peer = ReadRecord(lines[networkEnd + 2], "M16_011_PEER", "mac:dec", "sent:dec", "received:dec", "start:dec", "end:dec");
        Need(dma["stage"] == 0 && dma["a"] >= 4096 && dma["a"] % 4096 == 0 && dma["a"] <= 0x100000000UL - 32768 && dma["b"] == 32768 && dma["c"] == QemuContracts.M07ArpReceiveFeatures && dma["d"] == 8, "web_parent_live_dma");
        Need(peer["mac"] == arp.PeerMac && peer["sent"] == 1 && peer["received"] >= 1 && peer["start"] > 0 && peer["end"] >= peer["start"], "web_parent_live_peer");
        ulong[] binding = PrefixRow(nic, "M07_ARP 0", 4), oldPages = PrefixRow(nic, "M07_ARP 2", 4), anchor = PrefixRow(nic, "M07_NET D", 4);
        DistinctExtents([(dma["a"], 32768), (oldPages[0], 8192), (binding[3], 16384)], "web_parent_dma_association");
        var tables = ReadRecord(lines[owned + 10], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        const string restored = " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        var kernel = ReadRecord(lines[owned + 11][..^restored.Length], "M16_004_READBACK", "old:dec");
        const string clockSuffix = " source=owned-hpet";
        var clock = ReadRecord(lines[owned + 14 + 1][..^clockSuffix.Length], "M16_003_CLOCK", "frequency:dec", "period:dec", "recoveries:dec");
        const string stopSuffix = " firmware=revoked providers=1 shutdown=owned-controller";
        var stop = ReadRecord(lines[parentsEnd - 3][..^stopSuffix.Length], "M16_003_STOP", "iterations:dec", "reads:dec", "first:dec", "last:dec", "interval:dec");
        Need((decimal)peer["start"] >= decimal.Floor((decimal)stop["last"] * clock["period"] / 1_000_000), "web_parent_service_peer_clock");
        return new(lines.Length, tables["root"], kernel["old"], clock["frequency"], clock["period"], dma["a"], peer["start"], peer["end"], checked((uint)anchor[0]), arp);
    }
}
