using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    // This entry is reachable only from the original QemuSmoke runner owner.
    // All generated app/image/native/source inputs were held before launch;
    // captures and peer outputs remain held through both final callbacks.
    internal static DiskStaticOriginalInspection InspectDiskStaticRun(string root, QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, NativeAotStaticFileProgram program, NativeAotDiskStaticPeer.Completion peer, AuthorityFile image, AuthorityFile efi, AuthorityFile package, AuthorityFile imageReceipt, WebRootImageFixture.Receipt receipt, WebRunnerContext context, Action verifyOriginalInputs, Func<IReadOnlyList<StaticFileHttpWireMessage>, AuthorityFiles, IReadOnlyList<AuthorityFile>>? inspectOriginalBrowser = null, WebRootDiskInventory.VerifiedTruncated? truncated = null)
    {
        RequireCompleteProcess(runner, "disk_static_runner_incomplete");
        Need((options.WebRootFault == "Truncated") == (truncated is not null) && peer.Truncated == (truncated is not null), "disk_static_original_fault_scope");
        truncated?.VerifyUnchanged();
        bool full = NativeAotDiskSelection.FullStaticSelected(plan.Flags);
        Need(peer.FullProfile == full && (inspectOriginalBrowser is not null) == full, "disk_static_original_full_owner");
        int serialMaximum = full ? DiskStaticCapturePolicy.SerialBytes : 8 * 1024 * 1024, summaryMaximum = full ? DiskStaticCapturePolicy.SummaryBytes : 16 * 1024 * 1024, packetMaximum = full ? DiskStaticCapturePolicy.PacketBytes : 4 * 1024 * 1024, httpMaximum = full ? 524288 : 65536, bodyMaximum = full ? DiskStaticHttpContract.MaximumAssetBytes : 65536;
        Need(runner.Succeeded && runner.StandardError.Length == 0 && ReferenceEquals(runner, peer.Runner) && peer.Port == context.ForwardPort && NativeAotDiskSelection.StaticSelected(plan.Flags) && !program.Staged && plan.BuildEfi && !options.DryRun && !options.SkipImageBuild && options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" && options.LinuxElfImageVariant == "Intact" && options.WebRootFixture is "DiskA" or "DiskB" && options.LinuxThreadingApplication == NativeAotStaticFilePublisher.Application && string.Equals(Path.GetFullPath(root), program.SourceRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "disk_static_original_selection");
        using AuthorityFiles captured = new();
        AuthorityFile Hold(string path, int maximum, bool empty = false) => captured.Open(root, Path.GetFullPath(path, root), "original disk static evidence", "disk_static_capture", maximum, allowEmpty: empty, singleLink: true);
        Need(Path.GetFullPath(peer.Directory) == Path.GetFullPath(options.SerialLog + ".disk-http", root), "disk_static_peer_directory");
        var serial = Hold(options.SerialLog, serialMaximum);
        var summary = Hold(options.SummaryLog, summaryMaximum);
        var pcap = Hold(options.SerialLog + ".pcap", packetMaximum);
        var http = Hold(Path.Combine(peer.Directory, "http.json"), httpMaximum);
        var stopped = Hold(Path.Combine(peer.Directory, "stopped.json"), 4096);
        InspectDiskStaticReceiptIdentityValues(http.Sha256, peer.ReceiptSha256, imageReceipt.ReadUtf8(16 * 1024 * 1024, "disk_static_image_receipt"), JsonSerializer.Serialize(receipt));
        byte[] raw = serial.ReadBytes(serialMaximum, "disk_static_serial"), packageBytes = package.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "disk_static_package"), imageBytes = image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_static_image");
        var packageView = NativeAotStaticFileProgram.RequirePackage(packageBytes, false);
        _ = LinuxElfImageFixture.Inspect(imageBytes, efi.ReadBytes(16 * 1024 * 1024, "disk_static_efi"), packageBytes);
        var inventory = truncated?.Baseline ?? WebRootDiskInventory.Inspect(image.Path, imageBytes, receipt);
        truncated?.RequireValues(inventory, imageBytes);
        int bodies = full ? 4 + DiskStaticHttpContract.CreateAssetPlan(inventory).Requests.Count + 2 : 5;
        var bodyFiles = Enumerable.Range(0, bodies).Select(i => Hold(Path.Combine(peer.Directory, i.ToString("D3", CultureInfo.InvariantCulture) + ".body"), bodyMaximum, true)).ToArray();
        AuthorityFiles.RequireDistinct([image, efi, package, imageReceipt, serial, summary, pcap, http, stopped, ..bodyFiles]);
        byte[] summaryBytes = summary.ReadBytes(summaryMaximum, "disk_static_summary");
        Need(summaryBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()), "disk_static_summary_bom");
        string[] child = DecodeUtf8(summaryBytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).TakeLast(StaticFileProcessEvidence.SummaryCount).ToArray();
        var process = InspectNativeAotStaticFileChildForTests(child, context, options.TimeoutSeconds, full && !NativeAotDiskSelection.TimingSelected(plan.Flags));
        Need(runner.Duration.TotalMilliseconds >= process.LifetimeMilliseconds && runner.Duration.TotalSeconds <= options.TimeoutSeconds + 60, "disk_static_runner_lifetime");
        string[] stdout = runner.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (stdout[^1] == "")
            stdout = stdout[..^1];
        Need(stdout.Length == 21 && stdout[2] == $"QEMU_DISCOVERY result={context.Qemu} checked={context.Qemu}" && stdout[3] == $"OVMF_DISCOVERY result={context.Ovmf} checked={context.Ovmf}", "disk_static_runner_discovery");
        AssertStandardRunnerResult(root, options, plan, stdout, staticProgram: program, staticForwardPort: peer.Port, staticChildRows: child);
        int at = raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8);
        Need(at > FirmwarePrefixBytes, "disk_static_parent_start");
        var parent = InspectNativeAotWebParentForTests(packageBytes, raw.AsSpan(0, at), diskStatic: true);
        var(text, rows) = DiskStaticParentRows(raw, full);
        var replay = InspectNativeAotDiskStaticProtocolForTests(packageBytes.AsSpan((int)packageView.PayloadOffset, (int)packageView.PayloadBytes).ToArray(), SocketRawLeaf(raw), program.IsInstruction, program.IsSyscallReturn, parent.UserRoot, parent.KernelRoot, inventory, full, truncated);
        int connections = full ? replay.Static.Connections.Count : 1;
        Need(connections is> 0 and <= 32 && (full || replay.Static.Connections.Count == 1), "disk_static_connections");
        var disk = ReadDiskStaticBound(rows, replay);
        Need(disk["capacity"] == (ulong)imageBytes.Length / 512, "disk_static_image_device_geometry");
        string endRow = rows.Single(r => r.StartsWith("M16_025_END ", StringComparison.Ordinal));
        var end = full ? ReadDiskFileEndCommon(endRow) : ReadDiskFileEnd(endRow);
        Need(end["bootstrapCompleted"] == disk["bootstrapCompleted"] && end["bootstrapCopied"] == disk["bootstrapCopied"], "disk_static_bootstrap_join");
        var final = new NativeDiskReadObservations.Boundary(replay.Owner, replay.Epoch, replay.BootstrapCompleted, replay.BootstrapCopied, end["submitted"], end["completed"], end["copied"]);
        var sectors = InspectNativeAotDiskStaticSectorValues(replay, inventory, imageBytes, final, truncated);
        if (full)
            InspectNativeAotDiskStaticEndValues(endRow, final, sectors.Device);
        var envelope = SocketNetworkEnvelope(rows, true, staticFiles: true, diskFiles: true, diskStatic: true, diskStaticConnections: connections, diskStaticFullDevice: full ? sectors.Device : null).Proof;
        Need(replay.Static.ReadyNanoseconds >= parent.PeerEnd && replay.Static.ExitedNanoseconds <= envelope.End["end"] && replay.Static.FileObservations > 0 && replay.Static.PathObservations > 0 && replay.Static.Signal15Returns == 1, "disk_static_parent_leaf");
        foreach (string row in rows.Where(r => r.StartsWith("M16_008_TIMING ", StringComparison.Ordinal) || r.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)))
            Need(row.EndsWith(" periodFs=" + parent.Period.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), "disk_static_timing_period");
        var bound = ReadRecord(rows.Single(r => r.StartsWith("M16_008_BOUND ", StringComparison.Ordinal))[..^" timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack".Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        InspectDiskStaticLiveBoundaryValues(parent, bound["pool"], PrefixRow(rows, "M07_ARP 2", 4)[0], PrefixRow(rows, "M07_ARP 0", 4)[3], disk["arena"], disk["pages"], disk["bar"], disk["owner"], disk["clock"], envelope.End["end"], sectors.Device.Rows);
        var budget = ReadRecord(rows.Single(r => r.StartsWith("M16_008_BUDGET ", StringComparison.Ordinal)), "M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        var budgetEnd = ReadRecord(rows.Single(r => r.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal)), "M16_008_BUDGET_END", "nanos:dec");
        Need(budget["started"] >= disk["clock"] && budgetEnd["nanos"] <= envelope.End["end"], "disk_static_budget_lifetime");
        byte[] packetBytes = pcap.ReadBytes(packetMaximum, "disk_static_packets");
        var bodyBytes = bodyFiles.ToDictionary(b => Path.GetFileName(b.Path), b => b.ReadBytes(bodyMaximum, "disk_static_body"));
        DiskStaticHttpInspection wire;
        if (full)
        {
            var inspected = InspectNativeAotDiskStaticFullHttpValues(packetBytes, parent.FrameAnchor, parent.Arp, envelope.Lifetimes.Select(l => l.RemotePort).ToArray(), http.ReadBytes(httpMaximum, "disk_static_http"), bodyBytes, inventory, replay, truncated);
            var browserFiles = inspectOriginalBrowser!(inspected.BrowserMessages, captured);
            Need(browserFiles.Count is> 0 and <= 272, "disk_static_browser_artifact_bound");
            AuthorityFiles.RequireDistinct([image, efi, package, imageReceipt, serial, summary, pcap, http, stopped, ..bodyFiles, ..browserFiles]);
            wire = inspected.Http;
        }
        else
            wire = InspectNativeAotDiskStaticHttpValues(packetBytes, parent.FrameAnchor, parent.Arp, envelope.Lifetimes.Select(l => l.RemotePort).ToArray(), http.ReadBytes(httpMaximum, "disk_static_http"), bodyBytes, inventory, replay);
        using (var stoppedDocument = JsonDocument.Parse(stopped.ReadBytes(4096, "disk_static_stopped")))
        {
            var value = stoppedDocument.RootElement;
            StaticObject(value, "Port", "EndpointRefused", "RunnerExit");
            Need(value.GetProperty("Port").GetInt32() == peer.Port && value.GetProperty("EndpointRefused").GetBoolean() && value.GetProperty("RunnerExit").GetInt32() == 0, "disk_static_endpoint_stopped");
        }

        var w = envelope.Wire;
        var e = envelope.End;
        _ = ReadServiceWireTotals(packetBytes, parent, new(w["received"], w["arp"], w["accepted"], w["ignored"], w["capacity"], w["arpCopied"], w["tcpCopied"], e["polls"], e["fresh"], e["retired"], e["start"], e["end"], e["maxDeadline"]), raw, out _);
        Need((ulong)wire.TcpRecords == w["accepted"] + w["tcpCopied"], "disk_static_wire_totals");
        ValidateUnselectedRecords(text, plan);
        captured.VerifyUnchanged();
        verifyOriginalInputs();
        truncated?.VerifyUnchanged();
        captured.VerifyUnchanged();
        return new(replay, sectors, wire);
    }

    private static (string Text, string[] Rows) DiskStaticParentRows(ReadOnlySpan<byte> raw, bool full)
    {
        Need(raw.Length > FirmwarePrefixBytes && raw.Length <= (full ? DiskStaticCapturePolicy.SerialBytes : 8 * 1024 * 1024), "disk_static_serial_bounds");
        string text = NormalizeStaticFileWrites(DecodeUtf8(raw[FirmwarePrefixBytes..].ToArray()), full);
        Need(text.EndsWith("\r\n", StringComparison.Ordinal), "disk_static_serial_terminal");
        string[] rows = text[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.All(r => r.Length > 0 && !Controls.IsMatch(r)), "disk_static_serial_rows");
        return (text, rows);
    }

    private static Dictionary<string, ulong> ReadDiskStaticBound(string[] rows, DiskStaticReplayReport replay)
    {
        var disk = ReadRecord(rows.Single(r => r.StartsWith("M16_023_BOUND ", StringComparison.Ordinal))[..^" pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained".Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(disk["owner"] == replay.Owner && disk["epoch"] == replay.Epoch, "disk_static_image_device_geometry");
        Need(!replay.FullProfile || disk["bootstrapCompleted"] == replay.BootstrapCompleted && disk["bootstrapCopied"] == replay.BootstrapCopied, "disk_static_bootstrap_join");
        return disk;
    }

    /// <summary>Synchronously borrow a complete bounded Full serial capture and
    /// its already inspected replay/physical-sector values. Run the production
    /// parent envelope and END sector-counter joins with those exact identities
    /// and bootstrap boundaries. This copies no process/source/native/packet or
    /// whole-postprocessor authority and cannot accept a failed original.</summary>

    public static void InspectNativeAotDiskStaticParentValues(ReadOnlySpan<byte> raw, DiskStaticReplayReport replay, DiskStaticSectorValues sectors) => _ = InspectNativeAotDiskStaticParentPortValues(raw, replay, sectors);
    /// <summary>Run the same synchronous Full parent, END and envelope checks
    /// and return an owned copy of their validated remote ports for packet-value
    /// inspection. Borrowed captures are not retained. Failure throws; copied
    /// ports grant no source, process, browser, native or original authority.</summary>

    public static IReadOnlyList<uint> InspectNativeAotDiskStaticParentPortValues(ReadOnlySpan<byte> raw, DiskStaticReplayReport replay, DiskStaticSectorValues sectors)
    {
        Need(replay is not null && replay.FullProfile && sectors is not null, "disk_static_full_parent_context");
        var(_, rows) = DiskStaticParentRows(raw, true);
        _ = ReadDiskStaticBound(rows, replay!);
        var b = sectors!.Device.Counters;
        var expected = new NativeDiskReadObservations.Boundary(replay!.Owner, replay.Epoch, replay.BootstrapCompleted, replay.BootstrapCopied, b.Submitted, b.Completed, b.Copied);
        InspectNativeAotDiskStaticEndValues(rows.Single(r => r.StartsWith("M16_025_END ", StringComparison.Ordinal)), expected, sectors.Device);
        var envelope = SocketNetworkEnvelope(rows, true, staticFiles: true, diskFiles: true, diskStatic: true, diskStaticConnections: replay.Static.Connections.Count, diskStaticFullDevice: sectors.Device).Proof;
        return envelope.Lifetimes.Select(l => l.RemotePort).ToArray();
    }

    /// <summary>Synchronously borrow copied live addresses and IO clocks. Reject
    /// overlapping root, worker, NIC and disk extents, an unmeasured disk layout
    /// or IO outside the parent/device lifetime. This grants no native, process
    /// or original execution authority; the original owner supplies those joins.</summary>

    public static unsafe void InspectDiskStaticLiveBoundaryValues(WebParentReport parent, ulong pool, ulong priorNicDma, ulong nicBar, ulong arena, ulong pages, ulong diskBar, ulong owner, ulong boundClock, ulong endClock, IReadOnlyList<NativeDiskReadObservations.Observation> io)
    {
        Need(pages == 90 && owner >= 4096 && owner <= 0x100000000UL - (ulong)sizeof(Block.State), "disk_static_measured_layout");
        DistinctExtents([(parent.UserRoot, 4096), (parent.KernelRoot, 4096), (parent.Dma, 32768), (pool, WebThreadPoolPages * 4096UL), (priorNicDma, 8192), (nicBar, 16384), (arena, pages * 4096), (diskBar, 16384), (owner, (ulong)sizeof(Block.State))], "disk_static_live_extents");
        Need(boundClock >= parent.PeerEnd && endClock >= boundClock && io.Count > 0 && io.All(row => row.Clock >= boundClock && row.Clock <= endClock), "disk_static_io_lifetime");
    }

    /// <summary>Compare borrowed receipt identities synchronously. SHA256 text
    /// may use either hexadecimal case; image receipt text must match exactly.
    /// Malformed, oversized or divergent values throw. No file, process, native
    /// or original-run authority is granted by these supplied strings.</summary>

    public static void InspectDiskStaticReceiptIdentityValues(string heldHttpHash, string peerHttpHash, string heldImageReceipt, string originalImageReceipt)
    {
        static bool Digest(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
        Need(Digest(heldHttpHash) && Digest(peerHttpHash) && string.Equals(heldHttpHash, peerHttpHash, StringComparison.OrdinalIgnoreCase), "disk_static_http_receipt_identity");
        Need(heldImageReceipt is { Length: > 0 and <= 16 * 1024 * 1024 } && string.Equals(heldImageReceipt, originalImageReceipt, StringComparison.Ordinal), "disk_static_image_receipt_identity");
    }
}
