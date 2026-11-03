using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow bounded already-framed ASCII rows, the
    /// exact canonical Progress package and immutable original image/receipt.
    /// Copy rows before strict startup, idle-only, parent, device and leaf joins.
    /// Assign every whole submit/validate/copy transaction to exactly one request
    /// using its complete interval and independent sector coverage; reject
    /// ambiguous ownership instead of guessing FIFO. Shared cursor data starts
    /// only after parent commit. Metadata is confined to preparation opens.
    /// Malformed/divergent/incomplete observations throw ToolFailure. Results
    /// own immutable values; original firmware/process/native/packets and held
    /// input verification remain mandatory outside this value-only inspector.</summary>

    public static LinuxDiskProgressCaptureInspection InspectLinuxDiskProgressCaptureValues(IReadOnlyList<string> supplied, byte[] package, byte[] rawImage, WebRootImageFixture.Receipt receipt, string originalImagePath)
    {
        int count = supplied?.Count ?? 0;
        Need(count is > 0 and <= 32768 && package is not null && rawImage is not null && receipt is not null, "disk_progress_capture_input");
        string[] rows = new string[count];
        for (int i = 0; i < count; i++)
        {
            string? row = supplied![i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_progress_capture_row");
            rows[i] = row!;
        }

        Need(Race.PackageVariant(package!) == "Progress", "disk_progress_capture_package");
        var image = ValidateLinuxDiskCaptureImage(rawImage!, receipt!, originalImagePath);
        var inventory = WebRootDiskInventory.Inspect(originalImagePath, rawImage!, receipt!);
        var parents = ValidateLinuxDiskCaptureParents(rows, package!, diskProgress: true);
        var binding = ValidateLinuxDiskCaptureBinding(rows, parents, rawImage!.Length, diskProgress: true);
        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && Array.LastIndexOf(rows, "TEST_START m16-009") == first && Array.LastIndexOf(rows, "TEST_PASS m16-009") == last, "disk_progress_capture_leaf_extent");
        var leaf = InspectLinuxDiskRaceLeaf(rows[first..(last + 1)], package!, parents.Parent.UserRoot, parents.Parent.KernelRoot);
        var io = leaf.Auxiliary.Where(a => a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)).ToArray();
        Need(io.Select(a => first + a.RecordIndex).SequenceEqual(rows.Select((row, index) => (row, index)).Where(v => v.row.StartsWith("M16_023_IO ", StringComparison.Ordinal)).Select(v => v.index)), "disk_progress_io_inventory");
        var b = binding.Disk;
        var end = binding.End;
        var device = NativeDiskReadObservations.Inspect(io.Select(a => a.Text).ToArray(), rawImage!, new(b["owner"], b["epoch"], b["bootstrapCompleted"], b["bootstrapCopied"], end["submitted"], end["completed"], end["copied"]));
        Need(device.Cancelled == 0, "disk_progress_no_cancellations");
        var file = inventory.Find("/app/wwwroot/fixture/fragmented.bin");
        Need(file is not null && !file.IsDirectory && file.Bytes == WebRootImageFixture.FragmentedFixtureBytes, "disk_progress_fragmented");
        var main = leaf.Calls.Single(c => c.Spec.Role == "main.read");
        List<ProgressRequest> requests = [];
        foreach (var window in leaf.Windows.Where(w => w.Source == 4))
        {
            var call = leaf.Calls.Single(c => c.RecordIndex == window.CallRecordIndex);
            bool metadata = call.Spec.Role is "prep.original" or "prep.independent";
            bool read = call.Spec.Number is 0 or 17;
            Need(metadata || read || call.Spec.Number == 8, "disk_progress_request_kind");
            ulong offset = call.Spec.Number == 17 ? call.Spec.A3 : call.Spec.Role == "child.duplicate" ? (ulong)main.Spec.Result : 0;
            uint bytes = read ? checked((uint)call.Spec.Result) : 0;
            Need(!read || bytes > 0 && offset <= file!.Bytes && bytes <= file.Bytes - offset, "disk_progress_read_extent");
            ulong[] sectors = read ? file!.Cover(offset, bytes).Select(s => s.Lba).ToArray() : [];
            requests.Add(new(window, call, metadata, offset, bytes, sectors));
        }

        Need(requests.Count(r => r.Metadata) == 2 && requests.Count(r => r.Bytes > 0) == 4, "disk_progress_request_inventory");
        List<(ProgressRequest Owner, NativeDiskReadObservations.Observation Submit, NativeDiskReadObservations.Observation Validated)> parentIntervals = [];
        for (int i = 0; i < device.Rows.Count; i += 3)
        {
            Need(i + 2 < device.Rows.Count && device.Rows[i].Kind == NativeDiskReadObservations.Kind.Submitted && device.Rows[i + 1].Kind == NativeDiskReadObservations.Kind.CompletionValidated && device.Rows[i + 2].Kind == NativeDiskReadObservations.Kind.Copied, "disk_progress_transaction");
            var transaction = device.Rows[i];
            int submit = io[i].RecordIndex, validated = io[i + 1].RecordIndex, copy = io[i + 2].RecordIndex;
            for (int n = i; n < i + 3; n++)
                Need(device.Rows[n].Clock >= b["clock"] && device.Rows[n].Clock <= parents.Network.End["end"] && device.Rows[n].Sector >= image.PartitionFirstLba && device.Rows[n].Sector <= image.PartitionLastLba, "disk_progress_io_lifetime");
            var owners = requests.Where(r => r.Window.CallRecordIndex < submit && copy < r.Window.CompletionRecordIndex && (r.Metadata || r.Sectors.Contains(transaction.Sector))).ToArray();
            Need(owners.Length == 1, "disk_progress_transaction_owner");
            var owner = owners[0];
            Need(owner.Call.Spec.Role != "child.duplicate" || submit > leaf.MainReadCompletionIndex, "disk_progress_shared_cursor_commit");
            owner.Observed.Add(transaction.Sector);
            owner.Generations.Add(transaction.Generation);
            if (owner.Call.Spec.Role == "main.read")
                parentIntervals.Add((owner, transaction, device.Rows[i + 1]));
        }

        foreach (var request in requests)
        {
            if (request.Metadata)
                _ = WebRootDiskInventory.ClassifyMetadata(inventory, rawImage!, request.Observed);
            else
                Need(request.Observed.SequenceEqual(request.Sectors), "disk_progress_read_sector_window");
        }

        decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * parents.Clock["period"] / 1_000_000);
        var callsByIndex = leaf.Calls.ToDictionary(c => c.RecordIndex);
        Dictionary<int, decimal> entered = [];
        foreach (var auxiliary in leaf.Auxiliary)
        {
            Need(!auxiliary.Text.StartsWith("M16_008_TIMING ", StringComparison.Ordinal), "disk_progress_irq_timing");
            if (!auxiliary.Text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
                continue;
            Need(callsByIndex.TryGetValue(auxiliary.RecordIndex - 1, out var call), "disk_progress_timing_owner");
            var t = LinuxSyscallTiming(auxiliary.Text, call.Identity, call.Ordinal, call.Spec.Number);
            Need(t["periodFs"] == parents.Clock["period"] && Nanos(t["entered"]) >= binding.Budget["started"] && Nanos(t["prepared"]) <= parents.Network.End["end"] && entered.TryAdd(call.RecordIndex, Nanos(t["entered"])), "disk_progress_syscall_timing");
        }

        // CALL/TIME serialization follows syscall-prelude polling. Only the
        // adjacent, identity-bound entry sample locates the other worker in
        // physical request time; neither row position nor duration overlap does.
        var progress = leaf.Calls.Where(c => c.Spec.Role is "child.tid" or "child.pin" or "child.disjoint-readonly" or "child.disjoint-writable" or "child.notify" or "child.duplicate").Where(c => entered.TryGetValue(c.RecordIndex, out decimal entry) && parentIntervals.Count(p => p.Submit.Clock < entry && entry < p.Validated.Clock) == 1).ToArray();
        Need(progress.Length > 0, "disk_progress_device_overlap");
        var owned = requests.Select(r => new LinuxDiskProgressRequest(r.Call.Identity, r.Call.Ordinal, r.Window.Sequence, r.Window.RequestGeneration, r.Call.Spec.Role, r.Offset, r.Bytes, Array.AsReadOnly(r.Sectors.ToArray()), Array.AsReadOnly(r.Generations.ToArray()))).ToArray();
        return new(parents.Parent, leaf, device, image.Sha256, owned, progress);
    }

    /// <summary>Synchronously borrow unchanged bounded rows, complete canonical
    /// ExitPending package, immutable raw image and verified fixture receipt.
    /// Caller holds originalImagePath unchanged through final acceptance. Every
    /// IO row must belong to an exact prep-open or abandoned main-read window;
    /// metadata LBAs and the ordered data prefix come from independent image
    /// extraction. The sole uncompleted submit must be the next data sector.
    /// Invalid or divergent joins throw ToolFailure. Original firmware framing,
    /// emitted native/source, packets and process ownership remain caller duties.</summary>

    public static LinuxDiskExitPendingCaptureInspection InspectLinuxDiskExitPendingCaptureValues(IReadOnlyList<string> supplied, byte[] package, byte[] rawImage, WebRootImageFixture.Receipt receipt, string originalImagePath)
    {
        int count = supplied?.Count ?? 0;
        Need(count is > 0 and <= 32768 && package is not null && rawImage is not null && receipt is not null, "disk_exit_capture_input");
        string[] rows = new string[count];
        for (int i = 0; i < count; i++)
        {
            string? row = supplied![i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_exit_capture_row");
            rows[i] = row!;
        }

        Need(Race.PackageVariant(package!) == "ExitPending", "disk_exit_capture_package");
        var image = ValidateLinuxDiskCaptureImage(rawImage!, receipt!, originalImagePath);
        var inventory = WebRootDiskInventory.Inspect(originalImagePath, rawImage!, receipt!);
        var pending = InspectLinuxDiskExitPendingDeviceValues(rows, package!, rawImage!);
        var parents = ValidateLinuxDiskCaptureParents(rows, package!, pending);
        var binding = ValidateLinuxDiskCaptureBinding(rows, parents, rawImage!.Length, pending);
        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && Array.LastIndexOf(rows, "TEST_START m16-009") == first && Array.LastIndexOf(rows, "TEST_PASS m16-009") == last, "disk_exit_capture_leaf_extent");
        var leaf = InspectLinuxDiskExitPendingLeaf(rows[first..(last + 1)], package!, parents.Parent.UserRoot, parents.Parent.KernelRoot);
        var stages = leaf.ExitPendingStages!;
        for (int i = 0; i < 8; i++)
            Need(first + stages.Records[i].RecordIndex == pending.Stages.Records[i].RecordIndex && DiskExitPendingFields.All(k => stages.Records[i].Values[k] == pending.Stages.Records[i].Values[k]), "disk_exit_capture_stage_identity");
        Need(leaf.Auxiliary.Where(a => a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)).Select(a => first + a.RecordIndex).SequenceEqual(pending.IoRecordIndices), "disk_exit_capture_io_inventory");
        Need(first + leaf.Auxiliary.Single(a => a.Text.StartsWith("M16_023_BOUND ", StringComparison.Ordinal)).RecordIndex == pending.BindingRecordIndex, "disk_exit_capture_binding_identity");
        var file = inventory.Find("/app/wwwroot/fixture/fragmented.bin");
        Need(file is not null && !file.IsDirectory && file.Bytes == WebRootImageFixture.FragmentedFixtureBytes, "disk_exit_capture_fragmented");
        var stage = pending.Stages.Records[0];
        Need(stage.Values["inode"] == file!.Inode && stage.Values["position"] == 0, "disk_exit_capture_inode");
        var abandoned = leaf.Abandoned.Single();
        Need(first + abandoned.CallRecordIndex < stage.RecordIndex && first + abandoned.GroupExitRecordIndex > stage.RecordIndex, "disk_exit_capture_abandoned_window");
        var opens = leaf.Windows.Where(w => w.Source == 4).ToArray();
        Need(opens.Length == 2 && opens.All(w => leaf.Calls.Single(c => c.RecordIndex == w.CallRecordIndex).Spec.Role is "prep.original" or "prep.independent"), "disk_exit_capture_open_windows");
        List<ulong> metadata = [], copied = [];
        for (int i = 0; i < pending.Device.Rows.Count; i++)
        {
            var io = pending.Device.Rows[i];
            int index = pending.IoRecordIndices[i];
            Need(io.Clock >= binding.Disk["clock"] && io.Clock <= parents.Network.End["end"] && io.Sector >= image.PartitionFirstLba && io.Sector <= image.PartitionLastLba, "disk_exit_capture_io_lifetime");
            int openCount = opens.Count(w => first + w.CallRecordIndex < index && index < first + w.CompletionRecordIndex);
            bool reading = first + abandoned.CallRecordIndex < index && index < stage.RecordIndex;
            Need(openCount + (reading ? 1 : 0) == 1, "disk_exit_capture_io_window");
            if (openCount == 1)
                metadata.Add(io.Sector);
            else if (io.Kind == NativeDiskReadObservations.Kind.Copied)
                copied.Add(io.Sector);
        }

        _ = WebRootDiskInventory.ClassifyMetadata(inventory, rawImage!, metadata);
        var expected = file.Cover(0, Race.ParentOutputBytes).Select(s => s.Lba).ToArray();
        Need(expected.Length == 16 && copied.Count < expected.Length && copied.SequenceEqual(expected.Take(copied.Count)) && pending.Device.Rows[^1].Sector == expected[copied.Count] && pending.Device.Rows[^1].Kind == NativeDiskReadObservations.Kind.Submitted, "disk_exit_capture_data_prefix");
        var terminal = leaf.ExitCleanupEnd!.Value;
        var terminals = rows.Select((row, index) => (row, index)).Where(x => x.row.StartsWith("M16_025_EXIT_CLEANUP_END ", StringComparison.Ordinal)).ToArray();
        Need(terminals.Length == 1 && terminals[0].index == first + terminal.RecordIndex, "disk_exit_capture_cleanup_identity");
        var fullTerminal = ReadRecord(terminals[0].row, "M16_025_EXIT_CLEANUP_END", "owner:dec", "epoch:dec", "clock:dec");
        Need(fullTerminal.All(v => terminal.Values[v.Key] == v.Value), "disk_exit_capture_cleanup_identity");
        var budgetEnds = rows.Where(r => r.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal)).ToArray();
        Need(budgetEnds.Length == 1, "disk_exit_capture_budget_end");
        ulong activeEnd = ReadRecord(budgetEnds[0], "M16_008_BUDGET_END", "nanos:dec")["nanos"];
        for (int i = 0; i < 8; i++)
        {
            ulong value = pending.Stages.Records[i].Values["clock"];
            ulong lower = i < 3 ? binding.Disk["clock"] : i < 7 ? activeEnd : parents.Network.End["end"];
            ulong upper = i < 3 ? activeEnd : i < 7 ? parents.Network.End["end"] : terminal.Values["clock"];
            Need(value >= lower && value <= upper, "disk_exit_capture_stage_lifetime");
        }

        foreach (var a in leaf.Auxiliary)
        {
            if (!a.Text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
                continue;
            var t = ReadRecord(a.Text, "M16_008_SYSCALL_TIME", "identity:dec", "ordinal:dec", "nr:dec", "entered:dec", "admitted:dec", "dispatched:dec", "prepared:dec", "periodFs:dec");
            decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * parents.Clock["period"] / 1_000_000);
            Need(t["periodFs"] == parents.Clock["period"] && Nanos(t["entered"]) >= binding.Budget["started"] && Nanos(t["prepared"]) <= parents.Network.End["end"], "disk_exit_capture_syscall_timing");
        }

        return new(parents.Parent, leaf, pending, image.Sha256, first, copied.ToArray());
    }

    /// <summary>Synchronously borrow bounded unchanged rows, the canonical
    /// ExitPending package and sector-aligned raw image. Join the unique binding,
    /// every ordered IO observation and the eight copied exit stages. The current
    /// source performs no software pump after stage1: the last real submit must
    /// precede it and remain uncompleted through actual reset. Reject extra,
    /// missing, reordered or divergent observations with ToolFailure. No input
    /// arrays or image aliases are retained and no qualification authority is
    /// granted by this copied-value inspection.</summary>

    public static LinuxDiskExitPendingDeviceValues InspectLinuxDiskExitPendingDeviceValues(IReadOnlyList<string> supplied, byte[] package, ReadOnlySpan<byte> rawImage)
    {
        var stages = InspectLinuxDiskExitPendingValues(supplied, package);
        int binding = -1;
        Dictionary<string, ulong>? bound = null;
        List<int> indexes = [];
        List<string> io = [];
        const string suffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        for (int i = 0; i < supplied.Count; i++)
        {
            string row = supplied[i];
            if (row.StartsWith("M16_023_BOUND ", StringComparison.Ordinal))
            {
                Need(binding < 0 && i < stages.Records[0].RecordIndex, "disk_exit_pending_device_binding");
                RaceDiskBound(row);
                binding = i;
                bound = ReadRecord(row[..^suffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
            }
            else if (row.StartsWith("M16_023_IO ", StringComparison.Ordinal))
            {
                Need(binding >= 0 && i > binding && i < stages.Records[0].RecordIndex, "disk_exit_pending_device_io_order");
                indexes.Add(i);
                io.Add(row);
            }
        }

        Need(bound is not null && indexes.Count > 0 && rawImage.Length % 512 == 0 && bound["capacity"] == (ulong)rawImage.Length / 512, "disk_exit_pending_device_image");
        var first = stages.Records[0].Values;
        var beforeStop = stages.Records[3].Values;
        var stopped = stages.Records[4].Values;
        Need(bound!["owner"] == first["blockOwner"] && bound["epoch"] == first["blockEpoch"] && bound["clock"] <= first["clock"], "disk_exit_pending_device_owner");
        var boundary = new NativeDiskReadObservations.Boundary(bound["owner"], bound["epoch"], bound["bootstrapCompleted"], bound["bootstrapCopied"], beforeStop["submitted"], beforeStop["completed"], beforeStop["copied"]);
        var reset = new NativeDiskReadObservations.PendingResetRetirement(bound["owner"], bound["epoch"], first["blockGeneration"], first["sector"], beforeStop["submitted"], beforeStop["completed"], beforeStop["copied"], beforeStop["cancelled"], OS.Storage.ContinuingReadOnlyBlockService.Status.Success, checked((uint)stopped["resetReads"]), stopped["reset"] == 1, stopped["busMasterOff"] == 1, stopped["dmaReleased"] == 1, stopped["poolMask"]);
        var device = NativeDiskReadObservations.InspectResetRetired(io, rawImage, boundary, reset);
        foreach (var observation in device.Rows)
            Need(observation.Clock >= bound["clock"] && observation.Clock <= first["clock"], "disk_exit_pending_device_clock");
        Need(device.Rows[^1].Generation == first["blockGeneration"] && device.Rows[^1].Sector == first["sector"], "disk_exit_pending_device_pending_request");
        return new(stages, device, binding, indexes.ToArray());
    }

    private static readonly string[] DiskExitPendingFields = ["stage", "main", "child", "ordinal", "rip", "sequence", "owner", "epoch", "generation", "index", "fd", "fdGeneration", "binding", "bindingGeneration", "description", "position", "fileGeneration", "fileIndex", "inode", "fsOwner", "fsEpoch", "fsGeneration", "fsIndex", "blockOwner", "blockEpoch", "blockGeneration", "sector", "outputBytes", "submitted", "completed", "copied", "cancelled", "available", "consumed", "resetReads", "reset", "busMasterOff", "dmaReleased", "poolMask", "sentinelBytes", "userReserved", "userBacked", "ioGeneration", "ioSector", "blockPhase", "fsPhase", "ioActive", "fsIoIndex", "fsIoGeneration", "requestPhase", "requestWorker", "slotActive", "slotBytes", "cursor", "clock"];
    /// <summary>Borrow at most32768 printable rows of at most1536 characters
    /// and the entire canonical ExitPending package. Require eight ordered,
    /// generation-bound scalar stages, unchanged copy/submit counts and actual
    /// reset-aborted terminal counters. Unknown fields, altered identities,
    /// missing/duplicate stages or divergent counters throw ToolFailure. Returns
    /// owned immutable values; separate full protocol/native/device/image and
    /// original-runner joins remain mandatory before any qualification.</summary>

    public static LinuxDiskExitPendingValues InspectLinuxDiskExitPendingValues(IReadOnlyList<string> supplied, byte[] package)
    {
        Need(supplied is { Count: > 0 and <= 32768 } && package is not null && Race.PackageVariant(package) == "ExitPending", "disk_exit_pending_input");
        List<LinuxDiskExitPendingRecord> records = [];
        for (int i = 0; i < supplied!.Count; i++)
        {
            string row = supplied[i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_exit_pending_row");
            if (!row.StartsWith("M16_025_EXIT_PENDING ", StringComparison.Ordinal))
                continue;
            Need(records.Count < 8, "disk_exit_pending_stage_count");
            var fields = ReadRecord(row, "M16_025_EXIT_PENDING", DiskExitPendingFields.Select(f => f + ":dec").ToArray());
            Need(fields["stage"] == (ulong)records.Count + 1, "disk_exit_pending_stage_order");
            records.Add(new(i, new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(fields)));
        }

        Need(records.Count == 8, "disk_exit_pending_stage_count");
        var first = records[0].Values;
        var call = Race.Calls("ExitPending").Single(c => c.Role == "main.read");
        ulong ordinal = (ulong)Race.Calls("ExitPending").TakeWhile(c => c.Role != "main.read").Count() + 1;
        Need(first["main"] == Race.MainIdentity && first["child"] == Race.ChildIdentity && first["ordinal"] == ordinal && first["rip"] == call.ReturnRip && first["sequence"] > 0 && first["generation"] > 0 && first["index"] < LinuxDescriptorServices.DiskSlots && first["fd"] == Race.OriginalFd && first["fdGeneration"] > 0 && first["binding"] < LinuxDescriptorServices.DiskSlots && first["bindingGeneration"] > 0 && first["description"] < 16 && first["position"] == 0 && first["fileGeneration"] > 0 && first["fileIndex"] < 8 && first["inode"] > 0 && first["fsGeneration"] > 0 && first["fsIndex"] < 8 && first["epoch"] != 0 && first["fsEpoch"] == first["epoch"] && first["blockEpoch"] == first["epoch"] && first["blockGeneration"] == first["submitted"] && first["outputBytes"] == Race.ParentOutputBytes && first["submitted"] > 0 && first["completed"] < ulong.MaxValue && first["submitted"] == first["completed"] + 1 && first["copied"] <= first["completed"] && first["cancelled"] <= first["completed"], "disk_exit_pending_identity");
        foreach (string owner in new[]
        {
            "owner",
            "fsOwner",
            "blockOwner"
        }

        )
            Need(first[owner] is >= 4096 and <= 0xfffffff8 && first[owner] % 8 == 0, "disk_exit_pending_owner");
        Need(first["owner"] != first["fsOwner"] && first["owner"] != first["blockOwner"] && first["fsOwner"] != first["blockOwner"], "disk_exit_pending_owner");
        ulong clock = 0;
        for (int i = 0; i < records.Count; i++)
        {
            var v = records[i].Values;
            foreach (string field in DiskExitPendingFields.Skip(1).Take(28))
                Need(v[field] == first[field], "disk_exit_pending_binding");
            Need(v["copied"] == first["copied"] && v["completed"] == first["completed"] && v["cancelled"] == first["cancelled"] && v["available"] == unchecked((ushort)v["submitted"]) && v["consumed"] == unchecked((ushort)v["completed"]) && v["reset"] <= 1 && v["busMasterOff"] <= 1 && v["dmaReleased"] <= 1 && v["resetReads"] <= 2048 && v["clock"] >= clock && v["clock"] <= long.MaxValue && v["sentinelBytes"] == (i is 0 or 5 ? Race.ParentOutputBytes : 0), "disk_exit_pending_counters");
            Need(v["ioGeneration"] == first["blockGeneration"] && v["ioSector"] == first["sector"] && v["blockPhase"] == (i >= 4 ? 6UL : 3UL) && v["fsPhase"] == (i >= 5 ? 4UL : 2UL) && v["ioActive"] == (i >= 5 ? 0UL : 1UL) && v["fsIoIndex"] == first["fsIndex"] && v["fsIoGeneration"] == first["fsGeneration"] && v["requestPhase"] == (i < 2 ? 2UL : 4UL) && v["requestWorker"] == (i < 2 ? first["main"] : 0) && v["slotActive"] == (i < 2 ? 1UL : 0UL) && v["slotBytes"] == (i < 2 ? first["outputBytes"] : 0) && v["cursor"] == first["position"], "disk_exit_pending_cancel_boundary");
            if (i >= 4)
                Need(v["completed"] == records[3].Values["completed"] && v["cancelled"] == records[3].Values["cancelled"] && v["reset"] == 1 && v["busMasterOff"] == 1 && v["dmaReleased"] == 1 && v["poolMask"] == 0 && v["resetReads"] > records[3].Values["resetReads"], "disk_exit_pending_stop");
            else
                Need(v["poolMask"] == 1, "disk_exit_pending_dma_owner");
            if (i >= 5)
                Need(v["resetReads"] == records[4].Values["resetReads"], "disk_exit_pending_stop");
            clock = v["clock"];
        }

        Need(records[7].Values["userReserved"] == 0 && records[7].Values["userBacked"] == 0, "disk_exit_pending_memory_restored");
        return new(records.ToArray(), records[7].Values["completed"] == first["completed"]);
    }

    /// <summary>Synchronously borrow a complete m16-009 leaf and canonical race
    /// package. Inputs are not retained or mutated. At most32768 printable ASCII
    /// rows/1536 chars each are admitted. Unknown, missing, duplicated or invalid
    /// values throw ToolFailure. Root arguments are value joins only; authority
    /// and every indexed auxiliary observation require independent validation.</summary>

    public static LinuxDiskRaceLeafReport InspectLinuxDiskRaceLeaf(IReadOnlyList<string> supplied, byte[] package, ulong userRoot, ulong kernelRoot) => InspectLinuxDiskRaceLeafCore(supplied, package, userRoot, kernelRoot, null);
    /// <summary>Inspect the canonical ExitPending leaf with all eight original
    /// stage positions and a finite two-slot FIFO/lifecycle replay. Retain the
    /// original parked disk capability through group shutdown until the parent's
    /// real post-CommitExit stage. Require exact ready/active counts and off-stack
    /// confirmation/reap order. This is copied scheduler consistency, not native,
    /// user-memory, device, complete-parent or original-execution authority.</summary>

    public static LinuxDiskRaceLeafReport InspectLinuxDiskExitPendingLeaf(IReadOnlyList<string> supplied, byte[] package, ulong userRoot, ulong kernelRoot) => InspectLinuxDiskRaceLeafCore(supplied, package, userRoot, kernelRoot, InspectLinuxDiskExitPendingValues(supplied, package));
    private static LinuxDiskRaceLeafReport InspectLinuxDiskRaceLeafCore(IReadOnlyList<string> supplied, byte[] package, ulong userRoot, ulong kernelRoot, LinuxDiskExitPendingValues? stages)
    {
        int count = supplied?.Count ?? 0;
        Need(count is > 0 and <= 32768 && package is not null, "disk_race_input");
        string[] input = new string[count];
        for (int i = 0; i < count; i++)
        {
            string row = supplied![i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_race_row_bound");
            input[i] = row;
        }

        string variant = Race.PackageVariant(package!);
        bool close = variant == "CloseReuse";
        bool signalRace = variant is "InterruptWins" or "CompletionWins", exitPending = variant == "ExitPending";
        Need(stages is null || exitPending, "disk_race_stage_variant");
        ExitPendingFifo? fifo = stages is null ? null : new();
        int exitStage = 0;
        var manifest = LinuxElfPackage.Validate(package!).Manifest;
        var observations = ReadLinuxDiskCallObservations(input);
        var waits = observations.Waits.ToDictionary(v => v.RecordIndex);
        var returns = observations.Returns.ToDictionary(v => v.RecordIndex);
        string[] rows = ValidateLinuxDiskCallOrdinals(input);
        RaceWorker[] workers = [new(Race.Calls(variant), Race.MainIdentity, DiskFileStack(package!), 0), new(Race.Calls(variant, child: true), Race.ChildIdentity, Race.ChildStackTop, Race.ChildTls)];
        workers[0].Created = true;
        Need(userRoot is >= 4096 and <= 0xfffff000 && userRoot % 4096 == 0 && kernelRoot is >= 4096 and <= 0xfffff000 && kernelRoot % 4096 == 0 && userRoot != kernelRoot, "disk_race_roots");
        int at = 0, selected = -1, awaiting = -1, reaped = 0, transfers = 0;
        uint action = 0;
        bool exitMarker = false, witness = false;
        ulong clock = 0, epoch = 0, request = 0;
        int mainRead = -1, mainComplete = -1, childClose = -1, childDuplicate = -1, notify = -1;
        int childSignal = -1;
        bool groupExit = false;
        var handler = Race.HandlerCalls(variant);
        int handlerCalls = 0;
        bool inHandler = false, handlerReturned = false;
        ulong signalFrame = 0, interruptedFlags = 0;
        HashSet<string> progress = [];
        List<LinuxDiskRaceCall> calls = [];
        List<LinuxDiskRaceWindow> windows = [];
        List<LinuxDiskAuxiliary> auxiliary = [];
        List<LinuxDiskRaceAbandoned> abandoned = [];
        string Take()
        {
            Need(at < rows.Length, "disk_race_truncated");
            return rows[at++];
        }

        void Exact(string value) => Need(Take() == value, "disk_race_order_" + value);
        Dictionary<string, ulong> Row(string name, params string[] fields) => ReadRecord(Take(), name, fields);
        void Clock(ulong value)
        {
            Need(value >= clock && value <= long.MaxValue, "disk_race_clock");
            clock = value;
        }

        void Aux(int index) => auxiliary.Add(new(index, new string(input[index].AsSpan())));
        int Worker(ulong identity)
        {
            Need(identity is Race.MainIdentity or Race.ChildIdentity, "disk_race_identity");
            return identity == Race.MainIdentity ? 0 : 1;
        }

        void Select(int index)
        {
            Need(awaiting < 0 && workers[index].Created && !workers[index].Exited && !workers[index].Reaped && (selected < 0 || selected == index), "disk_race_selection");
            selected = index;
        }

        bool MainPending() => workers[0].Pending is { } p && p.Spec.Role == "main.read";
        void Finish(int w, int index, long value, uint output, uint completion = 1)
        {
            var pending = workers[w].Pending!;
            if (pending.Spec.Role == "main.read")
            {
                if (signalRace)
                {
                    Need(childSignal > mainRead && childSignal < index && workers[1].Exited && !inHandler && !handlerReturned, "disk_race_signal_overlap");
                    inHandler = true;
                    ulong fp = (workers[0].Stack - 128 - LinuxSignalServices.FpBytes) & ~63UL;
                    signalFrame = ((fp - LinuxSignalServices.FrameBytes) & ~15UL) - 8;
                }
                else
                    Need(!exitPending && progress.SetEquals(new[] { "child.pin", "child.disjoint-readonly", "child.disjoint-writable" }) && (close ? childClose > mainRead : notify > mainRead && childDuplicate > mainRead), "disk_race_overlap_missing");
                mainComplete = index;
            }

            if (pending.Spec.Role == "child.duplicate")
                Need(mainComplete >= 0 && mainComplete < index, "disk_race_cursor_lease");
            if (pending.Spec.Wait == Race.WaitPolicy.PipeRead)
                Need(notify >= 0 && notify < index, "disk_race_pipe_wake");
            if (pending.Spec.Wait == Race.WaitPolicy.Join)
                Need(workers[1].Exited, "disk_race_join_exit");
            windows.Add(new(pending.Index, index, workers[w].Identity, pending.Ordinal, pending.Sequence, pending.Request, pending.Source, value, output, completion));
            workers[w].Pending = null;
        }

        Exact("TEST_START m16-009");
        LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        int network = at;
        Exact("M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        Aux(network);
        Need(LinuxSyscallArm(Take())["root"] == userRoot, "disk_race_user_root");
        foreach (string start in new[]
        {
            "TEST_START m16.023",
            "TEST_START m16.024",
            "TEST_START m16.025"
        }

        )
        {
            int index = at;
            Exact(start);
            Aux(index);
        }

        int binding = at;
        RaceDiskBound(Take());
        Aux(binding);
        const string boundSuffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string boundLine = Take();
        Need(boundLine.EndsWith(boundSuffix, StringComparison.Ordinal), "disk_race_preemption_policy");
        var bound = ReadRecord(boundLine[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(bound["root"] == kernelRoot && bound["workers"] == 2 && manifest.Threads == 2 && bound["pool"] >= 4096 && bound["pool"] % 4096 == 0 && bound["pool"] <= 0x100000000UL - LinuxThreadProgram.SignalPoolPages * 4096UL && bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728, "disk_race_bound");
        DistinctExtents([(userRoot, 4096), (kernelRoot, 4096), (bound["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL)], "disk_race_extents");
        var budget = Row("M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        Need(budget["started"] <= long.MaxValue && budget["epochs"] == 8192 && budget["steps"] == 16384 && budget["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_race_budget");
        while (reaped < 2)
        {
            Need(at < rows.Length, "disk_race_truncated");
            int index = at;
            string line = rows[at];
            if (line.StartsWith("M16_025_EXIT_PENDING ", StringComparison.Ordinal))
            {
                Need(stages is not null && exitStage < 3 && stages.Records[exitStage].RecordIndex == index, "disk_race_exit_stage_position");
                var v = stages!.Records[exitStage].Values;
                if (exitStage == 0)
                {
                    var p = workers[0].Pending;
                    Need(MainPending() && selected is -1 or 1 && !witness && !groupExit && workers[1].Calls == 0 && workers[1].Calls < workers[1].Plan.Count && workers[1].Plan[workers[1].Calls].Role == "child.group-exit" && p is not null && v["ordinal"] == p.Ordinal && v["sequence"] == p.Sequence && v["generation"] == p.Request && v["rip"] == p.Spec.ReturnRip && v["outputBytes"] == p.Spec.OutputBytes && index + 1 < rows.Length && rows[index + 1].StartsWith("M16_008_CALL ", StringComparison.Ordinal), "disk_race_exit_stage_pending");
                    // The first child syscall can precede any optional child
                    // POLL record. The immediately following call must bind the
                    // complete child exit frame; replay its sole FIFO dispatch
                    // here so the preceding pending snapshot has the same owner.
                    Select(1);
                    fifo!.Running(1);
                    fifo!.BeforeShutdown();
                }
                else
                {
                    Need(groupExit && MainPending() && workers[1].Reaped && !workers[0].Exited && selected < 0 && awaiting < 0, "disk_race_exit_stage_commit");
                    if (exitStage == 1)
                    {
                        Need(stages.Records[2].RecordIndex == index + 1, "disk_race_exit_stage_adjacent");
                        fifo!.BeforeParentCommit();
                    }
                    else
                    {
                        Need(index + 1 < rows.Length && rows[index + 1].StartsWith("M16_008_EXIT ", StringComparison.Ordinal), "disk_race_exit_stage_adjacent");
                        fifo!.AfterParentCommit();
                        workers[0].Pending = null;
                    }
                }

                Take();
                Aux(index);
                exitStage++;
                continue;
            }

            if (line.StartsWith("M16_012_POLL ", StringComparison.Ordinal))
            {
                var p = Row("M16_012_POLL", "identity:dec", "clock:dec", "ready:dec", "idle:dec");
                Need(p["ready"] <= 2 && p["idle"] <= 1 && (p["idle"] == 0 || p["identity"] == 0), "disk_race_poll");
                if (p["identity"] != 0)
                    Select(Worker(p["identity"]));
                else
                    Need(selected < 0 && awaiting < 0, "disk_race_poll_owner");
                fifo?.Poll(p["identity"] == 0 ? -1 : Worker(p["identity"]), p["ready"]);
                Clock(p["clock"]);
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_023_IO ", StringComparison.Ordinal))
            {
                Need(fifo is null || exitStage == 0, "disk_race_post_shutdown_io");
                RaceIoShape(Take());
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_025_COMPLETE_TIME ", StringComparison.Ordinal))
            {
                var time = Row("M16_025_COMPLETE_TIME", "identity:dec", "sequence:dec", "request:dec", "clock:dec");
                Need(variant == "CloseReuse" && time["clock"] is > 0 and <= long.MaxValue, "disk_race_completion_time");
                // This is a physical wall-clock witness, outside the unchanged
                // logical CALL/POLL/COMPLETE observation stream.
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_008_TICK ", StringComparison.Ordinal))
            {
                // This explicit socket-backed leaf idles cooperatively through
                // IdleLinuxSocketNetwork, never the supervisor timer wait path.
                Need(fifo is null, "disk_exit_fifo_unexpected_tick");
                var t = Row("M16_008_TICK", "epoch:dec", "nanos:dec", "ready:dec");
                Need(selected < 0 && awaiting < 0 && workers.Any(w => w.Pending is not null) && t["epoch"] == ++epoch && epoch < budget["epochs"] && t["ready"] <= 2, "disk_race_tick");
                Clock(t["nanos"]);
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal))
            {
                var d = Row("M16_009_DRAIN", "identity:dec", "epoch:dec", "nanos:dec");
                Need(awaiting >= 0 && d["identity"] == workers[awaiting].Identity && d["epoch"] == ++epoch && epoch < budget["epochs"], "disk_race_drain");
                Clock(d["nanos"]);
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
            {
                Need(index > 0 && rows[index - 1].StartsWith("M16_008_CALL ", StringComparison.Ordinal), "disk_race_timing_order");
                var c = calls[^1];
                _ = LinuxSyscallTiming(Take(), c.Identity, c.Ordinal, c.Spec.Number);
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_008_TIMING ", StringComparison.Ordinal))
            {
                Need(fifo is null, "disk_exit_fifo_unexpected_irq_timing");
                Need(index > 0 && rows[index - 1].StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal), "disk_race_timing_order");
                var s = ReadRecord(rows[index - 1], "M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                LinuxTiming(Take(), s["identity"], epoch);
                Aux(index);
                continue;
            }

            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                Need(!exitPending, "disk_race_exit_unexpected_stdout");
                int w = 0;
                Select(w);
                fifo?.Running(w);
                Need(!witness && workers[0].Pending is null && workers[1].Reaped && workers[w].Calls < workers[w].Plan.Count && workers[w].Plan[workers[w].Calls].Role == "main.witness", "disk_race_witness_order");
                Exact("M16_005_WRITE fd=1 bytes=" + Race.Witness(variant).Length + " data=" + Race.Witness(variant));
                witness = true;
                continue;
            }

            if (line.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
            {
                var c = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
                int w = Worker(c["identity"]);
                Select(w);
                RaceWorker owner = workers[w];
                fifo?.Running(w);
                bool handlerCall = w == 0 && inHandler;
                Need(owner.Pending is null && (handlerCall ? handlerCalls < handler.Count : owner.Calls < owner.Plan.Count), "disk_race_call_order");
                var e = handlerCall ? handler[handlerCalls] : owner.Plan[owner.Calls];
                bool signalReturn = handlerCall && e.Number == 15;
                ulong stack = handlerCall ? signalFrame + (signalReturn ? 8UL : 0UL) : owner.Stack;
                Need(c["nr"] == e.Number && c["a0"] == e.A0 && c["a1"] == e.A1 && c["a2"] == e.A2 && c["a3"] == e.A3 && c["a4"] == e.A4 && c["a5"] == e.A5 && c["rip"] == e.ReturnRip && c["rsp"] == stack && c["fs"] == owner.Fs && c["gs"] == 0 && LinuxSyscallGateway.ValidateUserFlags(c["flags"], false) && (c["flags"] & 0x400) == 0, "disk_race_call_binding");
                Clock(c["clock"]);
                var wait = waits[index];
                var ret = returns[index];
                owner.Ordinal++;
                if (handlerCall)
                    handlerCalls++;
                else
                    owner.Calls++;
                Need(wait.Identity == owner.Identity && wait.Ordinal == (ulong)owner.Ordinal && wait.Deadline == 0 && ret.Identity == owner.Identity && ret.Ordinal == wait.Ordinal && ret.SignalDepth == (handlerCall && !signalReturn ? 1UL : 0UL) && ret.SignalFrame == (handlerCall && !signalReturn ? signalFrame : 0) && ret.BlockedSignals == (handlerCall && !signalReturn ? 0x200UL : 0) && ret.Rip == (signalReturn ? owner.Plan.Single(p => p.Role == "main.read").ReturnRip : c["rip"]) && ret.Rsp == (signalReturn ? owner.Stack : stack) && ret.Flags == (signalReturn ? interruptedFlags : c["flags"]) && ret.Fs == owner.Fs && ret.Gs == 0 && ret.ValueBits == c["valueBits"], "disk_race_return");
                calls.Add(new(index, owner.Identity, (ulong)owner.Ordinal, e));
                if (e.Role == "child.signal")
                {
                    Need(signalRace && MainPending() && owner.Calls == 1, "disk_race_signal_owner");
                    childSignal = index;
                }

                if (e.Role == "prep.clone")
                {
                    Need(!workers[1].Created, "disk_race_duplicate_clone");
                    workers[1].Created = true;
                    fifo?.CreateChild();
                }

                if (w == 1 && e.Role is "child.pin" or "child.disjoint-readonly" or "child.disjoint-writable")
                {
                    Need(MainPending(), "disk_race_progress_outside_read");
                    progress.Add(e.Role);
                }

                if (e.Role == "child.tid")
                    Need(MainPending(), "disk_race_child_early");
                if (e.Role == "child.notify")
                {
                    if (!close)
                        Need(MainPending(), "disk_race_notify_late");
                    notify = index;
                }

                if (e.Role == "child.duplicate")
                {
                    Need(MainPending(), "disk_race_duplicate_late");
                    childDuplicate = index;
                }

                if (e.Role == "child.close")
                {
                    Need(close && MainPending(), "disk_race_close_late");
                    childClose = index;
                }

                bool blocked = c["action"] == (ulong)LinuxSyscallAction.Blocked;
                Need(e.Wait == Race.WaitPolicy.Disk ? blocked : e.Wait == Race.WaitPolicy.None ? !blocked : true, "disk_race_wait_policy");
                if (blocked)
                {
                    uint source = e.Wait == Race.WaitPolicy.Disk ? 4U : e.Wait == Race.WaitPolicy.PipeRead ? 2U : 1U;
                    Need(c["status"] == (ulong)LinuxSyscallGateStatus.Blocked && c["valueBits"] == 0 && wait.Source == source && wait.Sequence == ++owner.Sequence, "disk_race_wait");
                    if (e.Wait == Race.WaitPolicy.Join)
                        Need(!workers[1].Exited, "disk_race_join_after_exit");
                    if (e.Wait == Race.WaitPolicy.PipeRead)
                        Need(notify < 0, "disk_race_pipe_already_buffered");
                    if (e.Wait == Race.WaitPolicy.Disk)
                        request++;
                    owner.Pending = new(e, index, (ulong)owner.Ordinal, owner.Sequence, e.Wait == Race.WaitPolicy.Disk ? request : 0, source);
                    fifo?.BeginWait(w, e.Role == "main.read");
                    if (e.Role == "main.read")
                    {
                        mainRead = index;
                        interruptedFlags = c["flags"];
                    }

                    awaiting = w;
                    action = (uint)LinuxSyscallAction.Blocked;
                }
                else
                {
                    bool exit = e.Number is 60 or 231;
                    long value = e.Wait == Race.WaitPolicy.Join ? -11 : e.Result;
                    Need(wait.Source == 0 && wait.Sequence == 0 && c["valueBits"] == unchecked((ulong)value) && c["action"] == (signalReturn ? 5UL : exit ? e.Number == 60 ? 4UL : 2UL : 1UL) && c["status"] == (exit ? 2UL : 1UL), "disk_race_outcome");
                    if (signalReturn)
                    {
                        Need(handlerCalls == handler.Count, "disk_race_signal_return_count");
                        inHandler = false;
                        handlerReturned = true;
                    }

                    if (e.Wait == Race.WaitPolicy.Join)
                        Need(workers[1].Exited, "disk_race_join_early_eagain");
                    if (e.Wait == Race.WaitPolicy.PipeRead)
                        Need(notify >= 0, "disk_race_pipe_empty_return");
                    if (exit)
                    {
                        if (exitPending)
                        {
                            Need(w == 1 && e.Role == "child.group-exit" && owner.Calls == 1 && owner.Ordinal == 1 && MainPending() && !witness && !groupExit, "disk_race_pending_group_exit");
                            var p = workers[0].Pending!;
                            abandoned.Add(new(p.Index, index, workers[0].Identity, p.Ordinal, p.Sequence, p.Request, p.Spec.OutputBytes));
                            if (fifo is null)
                                workers[0].Pending = null;
                            else
                            {
                                Need(exitStage == 1, "disk_race_shutdown_before_stage");
                                fifo.Shutdown();
                            }

                            groupExit = true;
                        }
                        else
                            Need(w == 1 || workers[1].Reaped && witness && (!signalRace || handlerReturned), "disk_race_exit_order");
                        awaiting = w;
                        action = (uint)c["action"];
                        exitMarker = false;
                    }
                }

                continue;
            }

            if (line.StartsWith("M16_025_COMPLETE ", StringComparison.Ordinal))
            {
                var c = Row("M16_025_COMPLETE", "identity:dec", "sequence:dec", "request:dec", "outputBytes:dec", "completion:dec", "valueBits:dec", "clock:dec");
                int w = Worker(c["identity"]);
                Select(w);
                var p = workers[w].Pending;
                fifo?.PreparationCompleted(w);
                Need(p is not null && p.Source == 4 && c["sequence"] == p.Sequence && c["request"] == p.Request && c["outputBytes"] == p.Spec.OutputBytes && c["completion"] == (ulong)(p.Spec.Role == "main.read" && variant == "InterruptWins" ? KernelThreadTable.Completion.Interrupted : KernelThreadTable.Completion.Woken) && c["valueBits"] == unchecked((ulong)p.Spec.Result), "disk_race_completion");
                Clock(c["clock"]);
                long value = p!.Spec.Result;
                uint output = p.Spec.OutputBytes;
                var resumed = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                Need(resumed["identity"] == c["identity"] && resumed["valueBits"] == c["valueBits"] && resumed["status"] == 1, "disk_race_resume");
                Finish(w, index, value, output, (uint)c["completion"]);
                continue;
            }

            if (line.StartsWith("M16_008_RESUME ", StringComparison.Ordinal))
            {
                var r = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                int w = Worker(r["identity"]);
                Select(w);
                var p = workers[w].Pending;
                Need(p is not null && p.Source is 1 or 2 && r["valueBits"] == unchecked((ulong)p.Spec.Result) && r["status"] == 1, "disk_race_nondisk_resume");
                Finish(w, index, p!.Spec.Result, 0);
                continue;
            }

            if (line.StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
            {
                var e = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                int w = Worker(e["identity"]);
                if (exitPending && groupExit && w == 0 && awaiting < 0 && selected < 0)
                {
                    awaiting = 0;
                    action = 4;
                    exitMarker = false;
                }

                Need(awaiting == w && action is 2 or 4 && !exitMarker && !workers[w].Exited && e["clearTidResult"] == 0, "disk_race_exit");
                fifo?.ExitRecord(w);
                exitMarker = true;
                workers[w].Exited = true;
                continue;
            }

            if (line.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal))
            {
                var s = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                Need(awaiting >= 0 && s["identity"] == workers[awaiting].Identity && s["action"] == action && (action == 3 || exitMarker), "disk_race_supervisor_order");
                transfers += 2;
                fifo?.Supervisor(awaiting, action, s["ready"], s["active"]);
                Need(transfers / 2 < (int)budget["steps"] && s["transfers"] == (ulong)transfers && s["active"] == (ulong)workers.Count(w => w.Created && !w.Reaped) && s["ready"] <= (ulong)workers.Count(w => w.Created && !w.Exited && !w.Reaped), "disk_race_supervisor");
                selected = awaiting = -1;
                action = 0;
                continue;
            }

            if (line.StartsWith("M16_008_REAP ", StringComparison.Ordinal))
            {
                const string suffix = " context=zero stack=zero slot=zero";
                string reap = Take();
                Need(reap.EndsWith(suffix, StringComparison.Ordinal), "disk_race_scrub");
                var r = ReadRecord(reap[..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec");
                int w = Worker(r["old"]);
                Need(selected < 0 && awaiting < 0 && workers[w].Exited && !workers[w].Reaped && workers[w].Pending is null && workers[w].Calls == workers[w].Plan.Count && r["next"] == r["old"] + (1UL << 32), "disk_race_reap");
                fifo?.Reap(w);
                workers[w].Reaped = true;
                reaped++;
                continue;
            }

            throw new ToolFailure("disk_race_unknown_record", "index=" + index);
        }

        void ExitTailStage(int expected)
        {
            Need(stages is not null && exitStage == expected - 1 && at == stages.Records[exitStage].RecordIndex, "disk_race_exit_stage_teardown");
            int index = at;
            Take();
            Aux(index);
            exitStage++;
        }

        if (stages is not null)
        {
            fifo!.Drained();
            for (int stage = 4; stage <= 7; stage++)
                ExitTailStage(stage);
        }

        for (uint fd = 0; fd < (exitPending ? 8U : 3U); fd++)
        {
            int index = at;
            Exact("M16_011_CLOSE fd=" + fd + " valueBits=0");
            Aux(index);
        }

        if (stages is not null)
            ExitTailStage(8);
        LinuxDiskExitPendingRecord? cleanupEnd = null;
        if (stages is not null)
        {
            int index = at;
            var terminal = Row("M16_025_EXIT_CLEANUP_END", "owner:dec", "epoch:dec", "clock:dec");
            Need(terminal["owner"] == stages.Records[0].Values["owner"] && terminal["epoch"] == stages.Records[0].Values["epoch"] && terminal["clock"] >= stages.Records[7].Values["clock"] && terminal["clock"] >= budget["started"] && terminal["clock"] <= long.MaxValue && terminal["clock"] - budget["started"] < LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_exit_cleanup_terminal");
            cleanupEnd = new(index, new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(terminal));
            Aux(index);
        }

        var finished = Row("M16_008_BUDGET_END", "nanos:dec");
        Need(finished["nanos"] >= clock && finished["nanos"] >= budget["started"] && finished["nanos"] <= long.MaxValue && finished["nanos"] - budget["started"] < LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_race_budget_end");
        if (stages is not null)
            Need(stages.Records.Take(3).All(s => s.Values["clock"] <= finished["nanos"]) && stages.Records.Skip(3).Take(4).All(s => s.Values["clock"] >= finished["nanos"]), "disk_exit_active_cleanup_boundary");
        const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        string ownerEnd = Take();
        Need(ownerEnd.EndsWith(cleanup, StringComparison.Ordinal), "disk_race_cleanup");
        var end = ReadRecord(ownerEnd[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(end["reaped"] == 2 && end["transfers"] == (ulong)transfers && end["irqs"] == epoch && end["exit"] == 42, "disk_race_owner_end");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        Exact("CLROS_M16_009_READY");
        Exact("TEST_PASS m16-009");
        Need(at == rows.Length && waits.Count == calls.Count && returns.Count == calls.Count && mainRead >= 0 && (exitPending ? !witness && workers[1].Calls == 1 && mainComplete == -1 && groupExit && abandoned.Count == 1 : witness && mainComplete > mainRead && (close ? childClose > mainRead && childClose < mainComplete : signalRace ? childSignal > mainRead && childSignal < mainComplete && handlerReturned : childDuplicate > mainRead && childDuplicate < mainComplete)), "disk_race_terminal");
        return new(variant, transfers, epoch, calls, windows, auxiliary, mainRead, mainComplete, childClose, abandoned, childSignal, stages, cleanupEnd);
    }

    private static void RaceDiskBound(string line)
    {
        const string suffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        Need(line.EndsWith(suffix, StringComparison.Ordinal), "disk_race_disk_policy");
        var d = ReadRecord(line[..^suffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(d["owner"] is >= 4096 and < 0x0000800000000000 && d["owner"] % 8 == 0 && d["epoch"] != 0 && d["arena"] >= 4096 && d["arena"] % 4096 == 0 && d["pages"] is > 0 and <= 256 && d["arena"] <= 0x100000000UL - d["pages"] * 4096 && d["bar"] >= 0x100000 && d["bar"] % 16384 == 0 && d["bar"] < 0x0000800000000000 - 16384 && d["capacity"] is >= 68 and <= (ulong)long.MaxValue / 512 && d["bootstrapCompleted"] > 0 && d["bootstrapCopied"] <= d["bootstrapCompleted"] && d["clock"] <= long.MaxValue, "disk_race_disk_geometry");
    }

    private static void RaceIoShape(string line)
    {
        const string number = "(0|[1-9][0-9]*)";
        string pattern = "\\AM16_023_IO kind=([012]) owner=" + number + " epoch=" + number + " generation=" + number + " sector=" + number + " sequence=" + number + " bytes=" + number + " available=" + number + " consumed=" + number + " clock=" + number + "( copied=committed data=[0-9a-f]{1024})?\\z";
        var match = Regex.Match(line, pattern, RegexOptions.CultureInvariant);
        Need(match.Success && (match.Groups[1].Value == "2") == match.Groups[11].Success, "disk_race_io_shape");
        for (int i = 2; i <= 10; i++)
            Need(ulong.TryParse(match.Groups[i].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "disk_race_io_number");
    }

    private static ulong LinuxDiskCloseReuseCompletionTime(IReadOnlyList<string> rows, LinuxDiskRaceLeafReport leaf, LinuxDiskRaceWindow window)
    {
        var times = leaf.Auxiliary.Where(a => a.Text.StartsWith("M16_025_COMPLETE_TIME ", StringComparison.Ordinal)).ToArray();
        Need(times.Length == leaf.Windows.Count(w => w.Source == 4), "disk_close_reuse_completion_time_witness");
        var ends = rows.Where(r => r.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal)).ToArray();
        Need(ends.Length == 1, "disk_close_reuse_completion_time_witness");
        ulong ended = ReadRecord(ends[0], "M16_008_BUDGET_END", "nanos:dec")["nanos"];
        ulong last = 0;
        foreach (var time in times)
        {
            var value = ReadRecord(time.Text, "M16_025_COMPLETE_TIME", "identity:dec", "sequence:dec", "request:dec", "clock:dec");
            Need(value["clock"] >= last && value["clock"] <= ended && value["clock"] <= long.MaxValue, "disk_close_reuse_completion_time_witness");
            last = value["clock"];
        }

        var matching = times.Where(t => t.RecordIndex == window.CompletionRecordIndex - 1).ToArray();
        Need(matching.Length == 1, "disk_close_reuse_completion_time_witness");
        var wall = ReadRecord(matching[0].Text, "M16_025_COMPLETE_TIME", "identity:dec", "sequence:dec", "request:dec", "clock:dec");
        var logical = ReadRecord(rows[window.CompletionRecordIndex], "M16_025_COMPLETE", "identity:dec", "sequence:dec", "request:dec", "outputBytes:dec", "completion:dec", "valueBits:dec", "clock:dec");
        Need(wall["identity"] == logical["identity"] && wall["sequence"] == logical["sequence"] && wall["request"] == logical["request"] && wall["clock"] >= logical["clock"], "disk_close_reuse_completion_time_witness");
        return wall["clock"];
    }

    /// <summary>Borrow copied canonical leaf/image values synchronously. Check
    /// the close dispatch inside one real pending physical token and one later
    /// data transaction. Keep every auxiliary row at its original leaf index.
    /// No aliases are retained or inputs mutated. Invalid values throw ToolFailure.
    /// The caller must independently join dataSectors to the fragmented-file
    /// inventory, all remaining transaction/request ownership, parent/native/
    /// original input lifetimes, actual canonical RX/control flow and cleanup.
    /// Returning the existing leaf report adds no sentinel, drain, user-memory,
    /// original execution, firmware, device or scheduler authority.</summary>

    private static LinuxDiskRaceLeafReport InspectLinuxDiskCloseReuseCandidateValues(IReadOnlyList<string> rows, byte[] package, ulong userRoot, ulong kernelRoot, byte[] rawImage, NativeDiskReadObservations.Boundary boundary, IReadOnlyList<ulong> dataSectors, ulong periodFs)
    {
        var leaf = InspectLinuxDiskRaceLeaf(rows, package, userRoot, kernelRoot);
        Need(leaf.Variant == "CloseReuse" && rawImage is not null && dataSectors is { Count: 16 } && dataSectors.Distinct().Count() == 16 && dataSectors.All(s => s < (ulong)rawImage.Length / 512) && periodFs is > 0 and <= 1_000_000_000, "disk_close_reuse_input");
        LinuxDiskRaceCall Call(string role) => leaf.Calls.Single(c => c.Spec.Role == role);
        LinuxDiskRaceWindow Window(LinuxDiskRaceCall call) => leaf.Windows.Single(w => w.CallRecordIndex == call.RecordIndex);
        var main = Call("main.read");
        var old = Window(main);
        foreach (var window in leaf.Windows.Where(w => w.Source == 4))
            _ = LinuxDiskCloseReuseCompletionTime(rows, leaf, window);
        var close = Call("child.close");
        var reopen = Call("child.reopen");
        var opened = Window(reopen);
        var later = Call("main.duplicate");
        var read = Window(later);
        var replacementCursor = Call("main.replacement-cursor");
        Need(old.Source == 4 && old.Value == -9 && old.OutputBytes == 8192 && close.Spec.Number == 3 && close.Spec.A0 == 3 && close.Spec.Result == 0 && reopen.Spec.Number == 2 && reopen.Spec.Result == 3 && old.CallRecordIndex < close.RecordIndex && close.RecordIndex < old.CompletionRecordIndex && close.RecordIndex < reopen.RecordIndex && opened.CompletionRecordIndex < later.RecordIndex && old.CompletionRecordIndex < later.RecordIndex && read.Source == 4 && read.Value == 8 && later.Spec.A0 == 7 && later.Spec.A1 == LinuxDiskRaceProofFixture.ChildOutput && replacementCursor.Spec.Number == 8 && replacementCursor.Spec.A0 == 3 && replacementCursor.Spec.A1 == 0 && replacementCursor.Spec.A2 == 1 && replacementCursor.Spec.Result == 0 && later.RecordIndex < replacementCursor.RecordIndex, "disk_close_reuse_lifetime");
        var timing = leaf.Auxiliary.Where(a => a.RecordIndex == close.RecordIndex + 1 && a.Text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)).ToArray();
        Need(timing.Length == 1, "disk_close_reuse_close_time");
        var t = LinuxSyscallTiming(timing[0].Text, close.Identity, close.Ordinal, close.Spec.Number);
        Need(t["periodFs"] == periodFs, "disk_close_reuse_period");
        decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * periodFs / 1_000_000);
        decimal admitted = Nanos(t["admitted"]), dispatched = Nanos(t["dispatched"]);
        // Inspect EVERY original indexed IO row against unchanged raw image
        // bytes. Successful copies and zero-byte cancellations are distinct.
        var io = leaf.Auxiliary.Where(a => a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)).ToArray();
        string bound = leaf.Auxiliary.Single(a => a.Text.StartsWith("M16_023_BOUND ", StringComparison.Ordinal)).Text;
        var b = ReadRecord(bound[..bound.IndexOf(" pci=", StringComparison.Ordinal)], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(boundary.Owner == b["owner"] && boundary.Epoch == b["epoch"] && boundary.BootstrapCompleted == b["bootstrapCompleted"] && boundary.BootstrapCopied == b["bootstrapCopied"], "disk_close_reuse_device_boundary");
        var device = NativeDiskReadObservations.Inspect(io.Select(a => a.Text).ToArray(), rawImage, boundary);
        List<int> pending = [], subsequent = [];
        for (int i = 0; i < device.Rows.Count; i++)
        {
            var s = device.Rows[i];
            if (s.Kind != NativeDiskReadObservations.Kind.Submitted)
                continue;
            var v = device.Rows[i + 1];
            Need(s.Clock >= b["clock"], "disk_close_reuse_device_lifetime");
            if (main.RecordIndex < io[i].RecordIndex && io[i].RecordIndex < close.RecordIndex && close.RecordIndex < io[i + 1].RecordIndex && dataSectors.Contains(s.Sector) && s.Clock < admitted && dispatched < v.Clock)
                pending.Add(i);
            if (later.RecordIndex < io[i].RecordIndex && s.Sector == dataSectors[0] && v.Bytes == 512 && io[i + 2].RecordIndex < read.CompletionRecordIndex)
                subsequent.Add(i);
        }

        Need(pending.Count == 1, "disk_close_reuse_pending_interval");
        Need(subsequent.Count == 1, "disk_close_reuse_later_data");
        int first = pending[0], next = subsequent[0];
        int terminal = device.Rows[first + 1].Bytes == 0 ? first + 1 : first + 2;
        var oldToken = device.Rows[first];
        var newToken = device.Rows[next];
        ulong finished = LinuxDiskCloseReuseCompletionTime(rows, leaf, read);
        Need(terminal < next && io[terminal].RecordIndex < io[next].RecordIndex && device.Rows[terminal].Clock <= newToken.Clock && oldToken.Owner == newToken.Owner && oldToken.Epoch == newToken.Epoch && oldToken.Generation < newToken.Generation && device.Rows[next + 2].Kind == NativeDiskReadObservations.Kind.Copied, "disk_close_reuse_token_order");
        Need(device.Rows[next + 2].Clock <= finished, "disk_close_reuse_completion_time");
        // This is the old PHYSICAL token's terminal boundary. It is not a
        // sampled fd/FS-operation generation or a user-output observation.
        // The original owner still needs the pinned Cancel/Pump source join:
        // -9 starts/releases Cancelling; it cannot schedule later old reads;
        // the single active device is consumed before another submission.
        // Other IO remains in leaf.Auxiliary for full ownership/census joining.
        return leaf;
    }

    /// <summary>Synchronously borrow bounded copied rows, new canonical
    /// CloseReuse bytes, stable original image/receipt and copied boundaries.
    /// Independently inspect image sectors; assign EVERY whole physical IO
    /// transaction to exactly one canonical wait/request/call, including the
    /// selected old token's detached drain. Preserve original leaf indexes and
    /// every auxiliary row. Missing, ambiguous or unsupported ownership throws
    /// ToolFailure; no event, completion, generation or parent is fabricated.
    /// Returned existing values/tuples grant no original/native/device/process,
    /// whole-user-memory, sentinel execution or persistence authority. Caller
    /// still owns original file holds, complete parent/native/RX/lifecycle joins,
    /// cancellation-effect lifetime and canonical successful sentinel path.</summary>

    private static (LinuxDiskRaceLeafReport Leaf, NativeDiskReadObservations.Inspection Device, IReadOnlyList<(LinuxDiskRaceCall Call, LinuxDiskRaceWindow Window, IReadOnlyList<ulong> DeviceGenerations)> Requests) InspectLinuxDiskCloseReuseTransactionsCandidateValues(IReadOnlyList<string> supplied, byte[] package, ulong userRoot, ulong kernelRoot, byte[] rawImage, WebRootImageFixture.Receipt receipt, string originalImagePath, NativeDiskReadObservations.Boundary boundary, ulong periodFs)
    {
        int count = supplied?.Count ?? 0;
        Need(count is > 0 and <= 32768 && package is not null && rawImage is not null && receipt is not null, "disk_close_reuse_capture_input");
        string[] rows = new string[count];
        for (int i = 0; i < count; i++)
        {
            string? row = supplied![i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_close_reuse_capture_row");
            rows[i] = row!;
        }

        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && Array.LastIndexOf(rows, "TEST_START m16-009") == first && Array.LastIndexOf(rows, "TEST_PASS m16-009") == last, "disk_close_reuse_leaf_extent");
        var allIo = rows.Select((text, index) => (text, index)).Where(r => r.text.StartsWith("M16_023_IO", StringComparison.Ordinal)).ToArray();
        Need(allIo.All(r => r.index > first && r.index < last && r.text.StartsWith("M16_023_IO ", StringComparison.Ordinal)), "disk_close_reuse_io_census");
        // Preflight BEFORE R01's Single/slice: duplicates, malformed family
        // prefixes and missing/repeated pci delimiters always fail ToolFailure.
        var bounds = rows.Select((text, index) => (text, index)).Where(r => r.text.StartsWith("M16_023_BOUND", StringComparison.Ordinal)).ToArray();
        Need(bounds.Length == 1 && bounds[0].index > first && bounds[0].index < last, "disk_close_reuse_bound_count");
        const string suffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        string bound = bounds[0].text;
        Need(bound.StartsWith("M16_023_BOUND ", StringComparison.Ordinal) && bound.Length > suffix.Length && bound.EndsWith(suffix, StringComparison.Ordinal) && bound.IndexOf(" pci=", StringComparison.Ordinal) == bound.Length - suffix.Length && bound.LastIndexOf(" pci=", StringComparison.Ordinal) == bound.Length - suffix.Length, "disk_close_reuse_bound_shape");
        RaceDiskBound(bound);
        var b = ReadRecord(bound[..^suffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(b["capacity"] == (ulong)rawImage!.Length / 512, "disk_close_reuse_image_capacity");
        var image = ValidateLinuxDiskCaptureImage(rawImage, receipt!, originalImagePath);
        var inventory = WebRootDiskInventory.Inspect(originalImagePath, rawImage, receipt!);
        var file = inventory.Find("/app/wwwroot/fixture/fragmented.bin");
        var empty = inventory.Find("/app/wwwroot/fixture/empty.bin");
        Need(file is not null && !file.IsDirectory && file.Bytes == WebRootImageFixture.FragmentedFixtureBytes && empty is not null && !empty.IsDirectory && empty.Bytes == 0, "disk_close_reuse_files");
        ulong[] coverage = file!.Cover(0, 8192).Select(s => s.Lba).ToArray();
        string[] local = rows[first..(last + 1)];
        var leaf = InspectLinuxDiskCloseReuseCandidateValues(local, package!, userRoot, kernelRoot, rawImage, boundary, coverage, periodFs);
        var io = leaf.Auxiliary.Where(a => a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)).ToArray();
        Need(io.Select(a => first + a.RecordIndex).SequenceEqual(allIo.Select(r => r.index)), "disk_close_reuse_io_census");
        var device = NativeDiskReadObservations.Inspect(io.Select(a => a.Text).ToArray(), rawImage, boundary);
        var calls = leaf.Calls.ToDictionary(c => c.RecordIndex);
        var main = leaf.Calls.Single(c => c.Spec.Role == "main.read");
        var close = leaf.Calls.Single(c => c.Spec.Role == "child.close");
        var t = LinuxSyscallTiming(local[close.RecordIndex + 1], close.Identity, close.Ordinal, close.Spec.Number);
        decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * periodFs / 1_000_000);
        var mainWindow = leaf.Windows.Single(w => w.CallRecordIndex == main.RecordIndex);
        ulong failed = LinuxDiskCloseReuseCompletionTime(local, leaf, mainWindow);
        Need(Nanos(t["dispatched"]) <= failed, "disk_close_reuse_close_before_error");
        List<(int Start, int End)> transactions = [];
        for (int i = 0; i < device.Rows.Count;)
        {
            Need(i + 1 < device.Rows.Count && device.Rows[i].Kind == NativeDiskReadObservations.Kind.Submitted && device.Rows[i + 1].Kind == NativeDiskReadObservations.Kind.CompletionValidated, "disk_close_reuse_transaction");
            int end = device.Rows[i + 1].Bytes == 0 ? i + 1 : i + 2;
            Need(end < device.Rows.Count && (end == i + 1 || device.Rows[end].Kind == NativeDiskReadObservations.Kind.Copied), "disk_close_reuse_transaction");
            transactions.Add((i, end));
            i = end + 1;
        }

        var selected = transactions.Where(p => main.RecordIndex < io[p.Start].RecordIndex && io[p.Start].RecordIndex < close.RecordIndex && close.RecordIndex < io[p.Start + 1].RecordIndex && device.Rows[p.Start].Clock < Nanos(t["admitted"]) && Nanos(t["dispatched"]) < device.Rows[p.Start + 1].Clock).ToArray();
        Need(selected.Length == 1, "disk_close_reuse_pending_interval");
        ulong oldLast = device.Rows[selected[0].Start].Generation;
        List<ProgressRequest> requests = [];
        foreach (var window in leaf.Windows.Where(w => w.Source == 4))
        {
            Need(calls.TryGetValue(window.CallRecordIndex, out var call), "disk_close_reuse_request_call");
            bool metadata = call.Spec.Role is "prep.original" or "prep.independent" or "child.reopen";
            bool old = call.Spec.Role == "main.read", later = call.Spec.Role == "main.duplicate";
            Need(metadata || old || later || call.Spec.Role is "child.empty" or "main.replacement" or "main.replacement-cursor" or "main.cursor" or "main.independent-cursor", "disk_close_reuse_unsupported_request");
            requests.Add(new(window, call, metadata, 0, old ? 8192U : later ? 8U : 0U, old ? coverage : later ? coverage[..1] : []));
        }

        var table = requests.Select(r => (r.Call, r.Window)).ToArray();
        // Classify all non-fragmented-data sectors in ONE independent pass.
        // ReadStep uses the validated handle cluster array and DataIo only;
        // arbitrary metadata inside a read does not receive guessed ownership.
        var metadataTransactions = transactions.Where(p => !coverage.Contains(device.Rows[p.Start].Sector)).ToArray();
        var classified = WebRootDiskInventory.ClassifyMetadata(inventory, rawImage, metadataTransactions.Select(p => device.Rows[p.Start].Sector).ToArray());
        var metadataByStart = metadataTransactions.Select((p, i) => (p.Start, Sector: classified[i])).ToDictionary(p => p.Start, p => p.Sector);
        var requestMetadata = requests.Select(_ => new List<WebRootDiskInventory.MetadataSector>()).ToArray();
        foreach (var p in transactions)
        {
            var submit = device.Rows[p.Start];
            bool metadata = metadataByStart.TryGetValue(p.Start, out var sector);
            int owner = CloseReuseUniqueTransactionOwner(table, metadata, submit.Sector, submit.Generation, io[p.Start].RecordIndex, io[p.End].RecordIndex, oldLast, coverage);
            var request = requests[owner];
            var time = leaf.Auxiliary.Where(a => a.RecordIndex == request.Call.RecordIndex + 1 && a.Text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)).ToArray();
            Need(time.Length == 1, "disk_close_reuse_request_time");
            var started = LinuxSyscallTiming(time[0].Text, request.Call.Identity, request.Call.Ordinal, request.Call.Spec.Number);
            Need(started["periodFs"] == periodFs && Nanos(started["dispatched"]) <= submit.Clock && submit.Clock >= b["clock"] && submit.Sector >= image.PartitionFirstLba && submit.Sector <= image.PartitionLastLba, "disk_close_reuse_request_lifetime");
            Need(request.Call.Spec.Role != "child.reopen" || Nanos(started["dispatched"]) >= Nanos(t["dispatched"]), "disk_close_reuse_reopen_time");
            ulong completed = LinuxDiskCloseReuseCompletionTime(local, leaf, request.Window);
            // Cancellation is terminal for this read: only the selected last
            // token may drain without data. Earlier prefix transactions must
            // have completed their real kernel copy before a following token.
            if (request.Call.Spec.Role == "main.read" && submit.Generation != oldLast)
                Need(device.Rows[p.Start + 1].Bytes == 512 && p.End == p.Start + 2 && device.Rows[p.End].Kind == NativeDiskReadObservations.Kind.Copied, "disk_close_reuse_early_cancel");
            bool detached = request.Call.Spec.Role == "main.read" && submit.Generation == oldLast;
            Need(detached || device.Rows[p.End].Clock <= completed, "disk_close_reuse_completion_time");
            if (metadata)
            {
                Need(device.Rows[p.Start + 1].Bytes == 512 && (sector.Kind != WebRootDiskInventory.MetadataKind.Directory || sector.DirectoryPath is "/app/wwwroot" or "/app/wwwroot/fixture"), "disk_close_reuse_metadata_route");
                requestMetadata[owner].Add(sector);
            }

            request.Observed.Add(submit.Sector);
            request.Generations.Add(submit.Generation);
        }

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (request.Metadata)
                Need(requestMetadata[i].Any(s => s.Kind == WebRootDiskInventory.MetadataKind.Directory && s.DirectoryPath == "/app/wwwroot") && requestMetadata[i].Any(s => s.Kind == WebRootDiskInventory.MetadataKind.Directory && s.DirectoryPath == "/app/wwwroot/fixture"), "disk_close_reuse_metadata_route");
            else if (request.Call.Spec.Role == "main.read")
                Need(request.Observed.Count is > 0 and <= 16 && request.Observed.SequenceEqual(coverage.Take(request.Observed.Count)) && request.Generations[^1] == oldLast, "disk_close_reuse_old_data_prefix");
            else
                Need(request.Observed.SequenceEqual(request.Sectors), "disk_close_reuse_data_window");
        }

        var result = requests.Select(r => (r.Call, r.Window, DeviceGenerations: (IReadOnlyList<ulong>)Array.AsReadOnly(r.Generations.ToArray()))).ToArray();
        return (leaf, device, Array.AsReadOnly(result));
    }

    // Pure copied-value cardinality seam for independent ambiguity controls.
    // The real entry builds this table ONLY from the canonical sealed leaf.
    // Forging a private test table cannot create public capture authority.
    private static int CloseReuseUniqueTransactionOwner(IReadOnlyList<(LinuxDiskRaceCall Call, LinuxDiskRaceWindow Window)> requests, bool metadata, ulong sector, ulong generation, int submitted, int terminal, ulong oldLastGeneration, IReadOnlyList<ulong> coverage)
    {
        Need(requests is { Count: > 0 and <= 32 } && coverage is { Count: 16 } && generation != 0 && oldLastGeneration != 0 && submitted >= 0 && terminal > submitted, "disk_close_reuse_owner_input");
        int owner = -1;
        for (int i = 0; i < requests.Count; i++)
        {
            var (call, window) = requests[i];
            Need(window.Source == 4 && window.CallRecordIndex == call.RecordIndex && window.Identity == call.Identity && window.Ordinal == call.Ordinal && window.Sequence != 0 && window.RequestGeneration != 0 && window.CompletionRecordIndex > call.RecordIndex, "disk_close_reuse_owner_table");
            bool eligible = call.Spec.Role switch
            {
                "prep.original" or "prep.independent" or "child.reopen" => metadata,
                "main.read" => !metadata && coverage.Contains(sector) && generation <= oldLastGeneration,
                "main.duplicate" => !metadata && sector == coverage[0],
                _ => false
            };
            bool detached = call.Spec.Role == "main.read" && generation == oldLastGeneration;
            if (!eligible || call.RecordIndex >= submitted || !detached && terminal >= window.CompletionRecordIndex)
                continue;
            Need(owner < 0, "disk_close_reuse_ambiguous_owner");
            owner = i;
        }

        Need(owner >= 0, "disk_close_reuse_unowned_transaction");
        return owner;
    }

    // Copied-value component, not an original-owner entrypoint. All returned
    // leaf/user-memory/native authority cuts remain those of the existing types.
    private static (WebParentReport Parent, LinuxDiskRaceLeafReport Leaf, NativeDiskReadObservations.Inspection Device, IReadOnlyList<(LinuxDiskRaceCall Call, LinuxDiskRaceWindow Window, IReadOnlyList<ulong> DeviceGenerations)> Requests) InspectLinuxDiskCloseReuseCaptureValues(IReadOnlyList<string> supplied, byte[] package, byte[] rawImage, WebRootImageFixture.Receipt receipt, string originalImagePath)
    {
        int count = supplied?.Count ?? 0;
        Need(count is > 0 and <= 32768 && package is not null && rawImage is not null && receipt is not null, "disk_close_reuse_original_input");
        string[] rows = new string[count];
        for (int i = 0; i < count; i++)
        {
            string? row = supplied![i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_close_reuse_original_row");
            rows[i] = row!;
        }

        Need(LinuxDiskRaceProofFixture.PackageVariant(package!) == "CloseReuse", "disk_close_reuse_package");
        var prelude = ReadCloseReuseCapturePrelude(rows);
        var values = InspectLinuxDiskCloseReuseTransactionsCandidateValues(rows, package!, prelude.UserRoot, prelude.KernelRoot, rawImage!, receipt!, originalImagePath, prelude.Boundary, prelude.Period);
        var old = values.Requests.Single(r => r.Call.Spec.Role == "main.read");
        ulong oldLast = old.DeviceGenerations[^1];
        var cancelled = values.Device.Rows.Where(r => r.Kind == NativeDiskReadObservations.Kind.CompletionValidated && r.Bytes == 0).ToArray();
        Need(values.Device.ResetRetirement is null && cancelled.Length <= 1 && (ulong)cancelled.Length == values.Device.Cancelled && cancelled.All(r => r.Generation == oldLast), "disk_close_reuse_cancel_scope");
        var parents = ValidateLinuxDiskCaptureParents(rows, package!, closeReuse: values.Device);
        var binding = ValidateLinuxDiskCaptureBinding(rows, parents, rawImage!.Length, closeReuse: values.Device);
        Need(parents.Parent.UserRoot == prelude.UserRoot && parents.Parent.KernelRoot == prelude.KernelRoot && parents.Clock["period"] == prelude.Period, "disk_close_reuse_parent_roots_clock");
        foreach (var row in values.Device.Rows)
            Need(row.Clock >= binding.Disk["clock"] && row.Clock <= parents.Network.End["end"], "disk_close_reuse_parent_io_lifetime");
        var calls = values.Leaf.Calls.ToDictionary(c => c.RecordIndex);
        HashSet<int> timed = [];
        foreach (var row in values.Leaf.Auxiliary)
        {
            Need(!row.Text.StartsWith("M16_008_TIMING ", StringComparison.Ordinal), "disk_close_reuse_no_user_irq_timing");
            if (!row.Text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
                continue;
            Need(calls.TryGetValue(row.RecordIndex - 1, out var call), "disk_close_reuse_time_call");
            var time = LinuxSyscallTiming(row.Text, call.Identity, call.Ordinal, call.Spec.Number);
            decimal Nanos(ulong ticks) => decimal.Floor((decimal)ticks * prelude.Period / 1_000_000);
            Need(time["periodFs"] == prelude.Period && Nanos(time["entered"]) >= binding.Budget["started"] && Nanos(time["prepared"]) <= parents.Network.End["end"] && timed.Add(call.RecordIndex), "disk_close_reuse_parent_time_lifetime");
        }

        return (parents.Parent, values.Leaf, values.Device, values.Requests);
    }

    // Raw fixed-schema prelude copies ONLY. It is not parent acceptance. The
    // full parent/binding below must independently accept these exact values.
    private static (ulong UserRoot, ulong KernelRoot, ulong Period, NativeDiskReadObservations.Boundary Boundary) ReadCloseReuseCapturePrelude(string[] rows)
    {
        string One(string prefix)
        {
            var found = rows.Where(r => r.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(found.Length == 1, "disk_close_reuse_prelude_unique");
            return found[0];
        }

        const string diskSuffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        string disk = One("M16_023_BOUND");
        Need(disk.StartsWith("M16_023_BOUND ", StringComparison.Ordinal) && disk.Length > diskSuffix.Length && disk.EndsWith(diskSuffix, StringComparison.Ordinal) && disk.IndexOf(" pci=", StringComparison.Ordinal) == disk.Length - diskSuffix.Length && disk.LastIndexOf(" pci=", StringComparison.Ordinal) == disk.Length - diskSuffix.Length, "disk_close_reuse_bound_shape");
        RaceDiskBound(disk);
        var b = ReadRecord(disk[..^diskSuffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        const string endSuffix = " device=retired fs=retired dma=zero descriptors=closed reservation=retained";
        string end = One("M16_025_END ");
        Need(end.EndsWith(endSuffix, StringComparison.Ordinal), "disk_close_reuse_end_shape");
        var e = ReadRecord(end[..^endSuffix.Length], "M16_025_END", "submitted:dec", "completed:dec", "copied:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "fsReads:dec", "cacheHits:dec", "cacheMisses:dec");
        var table = ReadRecord(One("M16_004_TABLES "), "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        const string restored = " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        string kernel = One("M16_004_READBACK ");
        Need(kernel.EndsWith(restored, StringComparison.Ordinal), "disk_close_reuse_kernel_shape");
        var k = ReadRecord(kernel[..^restored.Length], "M16_004_READBACK", "old:dec");
        const string clockSuffix = " source=owned-hpet";
        string clock = One("M16_003_CLOCK ");
        Need(clock.EndsWith(clockSuffix, StringComparison.Ordinal), "disk_close_reuse_clock_shape");
        var c = ReadRecord(clock[..^clockSuffix.Length], "M16_003_CLOCK", "frequency:dec", "period:dec", "recoveries:dec");
        Need(c["period"] is > 0 and <= 1_000_000_000, "disk_close_reuse_clock_period");
        return (table["root"], k["old"], c["period"], new(b["owner"], b["epoch"], b["bootstrapCompleted"], b["bootstrapCopied"], e["submitted"], e["completed"], e["copied"]));
    }
}
