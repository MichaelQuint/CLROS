using System.Globalization;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using B = OS.Storage.WritableBlockSession;
using Fixture = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Check copied mechanical NIC/envelope values and the exact027
    /// cleanup tail. The leaf, images, firmware, actual process/native inputs,
    /// independent packets and storage observations remain separate obligations.
    /// Borrowed input is never modified; malformed or extra rows throw.</summary>

    public static void InspectWritableDiskNetworkValues(string[] rows)
    {
        Need(rows is { Length: > 0 and <= 131072 } && rows.All(row => row is { Length: > 0 and <= 2048 } && row.All(c => c is >= ' ' and <= '~')), "writable_parent_input");
        _ = SocketNetworkEnvelope(rows, true, mechanical: true, diskFiles: true, writableDisk: true);
    }

    // Only the original production caller can supply these retained leases.
    // Replays/value-only reports cannot establish this joined invocation.
    internal static WritableDiskValueReport InspectOriginalWritableDiskRun(string root, QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, AuthorityFile image, AuthorityFile snapshot, AuthorityFile efi, AuthorityFile package, CombinedWebDataImageFixture.WorkingImage working, CombinedWebDataImageFixture.Receipt receipt, CombinedWebDataImageFixture.Inspection extracted, string qemu, string ovmf, Action verifyOwnedInputs)
    {
        RequireCompleteProcess(runner, "writable_runner_incomplete");
        Need(runner.Succeeded && runner.StandardError.Length == 0 && NativeAotDiskSelection.WritableSelected(plan.Flags) && options.WritableDiskFixture is "Mechanical30" or "LogUpload40" && !options.HasDiskRaceSelection && options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" && options.LinuxElfImageVariant == "Intact" && !options.SkipImageBuild && plan.BuildEfi, "writable_runner_selection");
        working.VerifyOriginalsAndWorkingIdentity();
        snapshot.VerifyUnchanged();
        using AuthorityFiles captured = new();
        AuthorityFile Hold(string path, int maximum) => captured.Open(root, Path.GetFullPath(path, root), "original writable capture", "writable_capture", maximum, singleLink: true);
        var serial = Hold(options.SerialLog, 8 * 1024 * 1024);
        var summary = Hold(options.SummaryLog, 16 * 1024 * 1024);
        var packets = Hold(options.SerialLog + ".pcap", 4 * 1024 * 1024);
        AuthorityFiles.RequireDistinct(image, snapshot, efi, package, serial, summary, packets);
        string[] stdout = runner.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (stdout[^1] == "")
            stdout = stdout[..^1];
        Need(stdout.Length == 22 && stdout[0] == $"QEMU_DISCOVERY result={qemu} checked={qemu}" && stdout[1] == $"OVMF_DISCOVERY result={ovmf} checked={ovmf}", "writable_runner_discovery");
        AssertStandardRunnerResult(root, options, plan, stdout, writableData: new(working.Path, image.InitialIdentity.Key, image.Length, image.Sha256.ToUpperInvariant(), working.IdentityKey, working.Length, working.InitialSha256));
        string[] rows = FirmwareProtocol(serial.Path, "TEST_PASS m16-012\r\n", true, false, "writable_capture", linuxThreads: true);
        byte[] packageBytes = package.ReadBytes(65536, "writable_package");
        bool logUpload = options.WritableDiskFixture == "LogUpload40";
        var parents = ValidateLinuxDiskCaptureParents(rows, packageBytes, writableDisk: true, logUpload: logUpload);
        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && Array.LastIndexOf(rows, "TEST_START m16-009") == first && Array.LastIndexOf(rows, "TEST_PASS m16-009") == last, "writable_capture_leaf_extent");
        var selected = rows.Select((text, index) => new WritableDiskRow(index, text)).Where(row => row.Text.StartsWith("M16_027_", StringComparison.Ordinal)).ToArray();
        var result = InspectWritableDiskValues(rows[first..(last + 1)], first, rows.Length, selected, packageBytes, image.ReadBytes(CombinedWebDataImageFixture.ImageBytes, "writable_initial_image"), snapshot.ReadBytes(CombinedWebDataImageFixture.ImageBytes, "writable_final_image"), receipt.Image, extracted, logUpload);
        ValidateWritableCaptureBinding(rows, parents, selected, first, last);
        AssertSocketCapture(packets.Path, parents.Network, mechanical: true);
        ValidateUnselectedRecords(serial.ReadUtf8(8 * 1024 * 1024, "writable_serial"), plan);
        captured.VerifyUnchanged();
        snapshot.VerifyUnchanged();
        working.VerifyOriginalsAndWorkingIdentity();
        verifyOwnedInputs();
        captured.VerifyUnchanged();
        snapshot.VerifyUnchanged();
        return result;
    }

    private static void ValidateWritableCaptureBinding(string[] rows, DiskCaptureParents parents, IReadOnlyList<WritableDiskRow> selected, int first, int last)
    {
        string Unique(string prefix)
        {
            var matches = rows.Where(row => row.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(matches.Length == 1, "writable_capture_unique_" + prefix);
            return matches[0];
        }

        var bindingRow = WdSingle(selected, "M16_027_BOUND ");
        const string bindingSuffix = " pci=0:7:0 queue=0 access=readwrite flush=negotiated reservation=retained";
        var b = WdRecord(bindingRow.Text[..^bindingSuffix.Length], "M16_027_BOUND", "owner epoch arena pages bar capacity nativeOwner nativeOwnerBytes boot bootBytes carrier carrierBytes observation observationBytes engine engineEpoch port portEpoch mount mountEpoch provider providerEpoch readAttachment writeAttachment dataFirst dataLast webFirst webLast pid clock");
        const string threadSuffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string threadRow = Unique("M16_008_BOUND ");
        Need(threadRow.EndsWith(threadSuffix, StringComparison.Ordinal), "writable_capture_thread_policy");
        var thread = WdRecord(threadRow[..^threadSuffix.Length], "M16_008_BOUND", "root pool workers code metadata userImage");
        var arm = LinuxSyscallArm(Unique("M16_005_ARMED "));
        var budget = WdRecord(Unique("M16_008_BUDGET "), "M16_008_BUDGET", "started epochs steps nanos");
        var finish = WdRecord(Unique("M16_008_BUDGET_END "), "M16_008_BUDGET_END", "nanos");
        Need(arm["root"] == parents.Parent.UserRoot && thread["root"] == parents.Parent.KernelRoot && b["clock"] >= parents.Parent.PeerEnd && budget["started"] >= b["clock"] && finish["nanos"] <= parents.Network.End["end"] && bindingRow.RecordIndex > first && bindingRow.RecordIndex < last, "writable_capture_lifetime_roots");
        DistinctExtents([(parents.Parent.UserRoot, 4096), (parents.Parent.KernelRoot, 4096), (parents.Parent.Dma, 32768), (thread["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL), (parents.OldPages[0], 8192), (parents.NicBinding[3], 16384), (b["arena"], b["pages"] * 4096), (b["bar"], 16384), (b["boot"], b["bootBytes"]), (b["nativeOwner"], b["nativeOwnerBytes"])], "writable_capture_live_extents");
    }

    public const int MaximumWritableDiskRows = 65536;
    /// <summary>Inspect every selected IO/STATE row in original strictly increasing
    /// index order. Inputs are synchronously borrowed; output owns immutable values.
    /// The supplied initial boundary is drained. Cancellation, failed completion,
    /// dirty frontiers and an unresolved terminal request remain explicit facts.
    /// This grants no source, device, reset, process, native or persistence authority.
    /// Malformed, conflicting, missing or reordered stages throw ToolFailure.</summary>

    public static WritableBlockValueReport InspectWritableBlockValues(IReadOnlyList<WritableDiskRow> rows, Fixture.BlockBoundary before, Fixture.BlockBoundary after)
    {
        WritableRows(rows, int.MaxValue);
        WritableBoundary(before);
        WritableBoundary(after);
        WdNeed(before.Owner == after.Owner && before.Epoch == after.Epoch && before.SectorCount == after.SectorCount && before.Submitted == before.Completed && before.SubmittedBytes == before.CompletedBytes, "boundary");
        Fixture.BlockBoundary baseline = before;
        WritableDiskIo? prior = null;
        Dictionary<string, ulong>? state = null;
        ulong clock = 0;
        bool bound = false, retired = false, failed = false;
        List<WritableDiskIo> io = [];
        List<WritableDiskRow> states = [];
        foreach (var row in rows)
        {
            if (row.Text.StartsWith("M16_027_STATE ", StringComparison.Ordinal))
            {
                var s = WdRecord(row.Text, "M16_027_STATE", "owner epoch bound status running retired dirty retry submitted completed submittedBytes completedBytes flushAcknowledgedBytes clock");
                WdNeed(s["owner"] == before.Owner && s["epoch"] == before.Epoch && s["bound"] <= 1 && s["running"] <= 1 && s["retired"] <= 1 && s["dirty"] <= 1 && s["retry"] <= 1 && s["status"] <= (ulong)B.Status.Ready && (!bound || s["bound"] == 1) && (!retired || s["retired"] == 1) && (s["running"] == 0 || s["bound"] == 1 && s["retired"] == 0) && (s["retired"] == 0 || s["bound"] == 1 && s["running"] == 0), "state");
                var counters = WdBoundary(s, before);
                WritableBoundary(counters);
                WdNeed(s["dirty"] == (counters.SubmittedBytes != counters.FlushAcknowledgedBytes ? 1UL : 0UL), "state_dirty");
                if (s["bound"] == 0)
                    WdNeed(s["status"] == 0 && s["running"] == 0 && s["retry"] == 0 && s["submitted"] == 0 && s["completed"] == 0 && s["submittedBytes"] == 0, "state_unbound");
                if (s["running"] != 0)
                    WdNeed(s["status"] == (ulong)B.Status.Success && s["retry"] == 0, "state_running");
                if (state is not null)
                {
                    WdNeed(s["submitted"] >= state["submitted"] && s["submitted"] - state["submitted"] <= 1 && s["completed"] >= state["completed"] && s["completed"] - state["completed"] <= 1 && s["submittedBytes"] >= state["submittedBytes"] && s["completedBytes"] >= state["completedBytes"] && s["flushAcknowledgedBytes"] >= state["flushAcknowledgedBytes"] && (state["retry"] == 0 || s["retry"] == 1) && (!failed || s["status"] == state["status"]) && s.Any(p => p.Key != "clock" && p.Value != state[p.Key]), "state_progress");
                }
                else
                    WdNeed(counters == before, "state_initial");
                bound |= s["bound"] != 0;
                retired |= s["retired"] != 0;
                failed |= s["status"] is not ((ulong)B.Status.Uninitialized or (ulong)B.Status.Success);
                WdClock(s["clock"], ref clock);
                state = s;
                states.Add(row);
                continue;
            }

            WdNeed(row.Text.StartsWith("M16_027_IO ", StringComparison.Ordinal), "block_unknown");
            string text = row.Text, hex = "";
            int dataAt = text.IndexOf(" data=", StringComparison.Ordinal);
            if (dataAt >= 0)
            {
                hex = text[(dataAt + 6)..];
                text = text[..dataAt];
                WdNeed(hex.Length == 1024 && hex.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "hex");
            }

            var r = WdRecord(text, "M16_027_IO", "owner epoch generation kind sector flags status cancelled submitted completed submittedBytes completedBytes flushTargetBytes flushAcknowledgedBytes available consumed completionBytes payloadBytes clock");
            WdNeed(r["kind"] <= 2 && r["flags"] is 1 or 3 or 7 && r["cancelled"] <= 1 && r["available"] <= ushort.MaxValue && r["consumed"] <= ushort.MaxValue && r["status"] <= (ulong)B.Status.Ready && r["completionBytes"] <= 512 && r["payloadBytes"] is 0 or 512, "io_wire");
            var current = new WritableDiskIo(row.RecordIndex, r["owner"], r["epoch"], r["generation"], (B.Operation)r["kind"], r["sector"], (byte)r["flags"], (B.Status)r["status"], r["cancelled"] != 0, r["submitted"], r["completed"], r["submittedBytes"], r["completedBytes"], r["flushTargetBytes"], r["flushAcknowledgedBytes"], (ushort)r["available"], (ushort)r["consumed"], (uint)r["completionBytes"], (uint)r["payloadBytes"], r["clock"], hex);
            bool next = prior is null || current.Generation != prior.Generation;
            if (next)
            {
                if (prior is not null)
                {
                    WdNeed(WdResolved(prior) && prior.Status == B.Status.Success, "unresolved");
                    baseline = WdBoundary(prior, before);
                }

                WdNeed(current.Flags == 1 && !current.Cancelled, "publication");
            }
            else
            {
                WdNeed(current.Owner == prior!.Owner && current.Epoch == prior.Epoch && current.Kind == prior.Kind && current.Sector == prior.Sector && (current.Flags & prior.Flags) == prior.Flags && (!prior.Cancelled || current.Cancelled) && (!prior.IsComplete || current.Status == prior.Status) && (!current.Cancelled || prior.Cancelled || !prior.IsCopied && (!prior.IsComplete || prior.Kind == B.Operation.Read && prior.Status == B.Status.Success)) && (prior.PayloadBytes == 0 || current.DataHex == prior.DataHex) && (current with { RecordIndex = prior.RecordIndex, Clock = prior.Clock }) != prior, "accumulation");
            }

            WdSnapshot(current, baseline);
            WdNeed(state is not null && state["bound"] == 1 && state["retired"] == 0 && WdBoundary(state, before) == WdBoundary(current, before), "io_state");
            if (current.IsComplete && current.Status != B.Status.Success)
                WdNeed(state["status"] == (ulong)current.Status && state["running"] == 0 && state["retry"] == 1, "error_state");
            WdClock(current.Clock, ref clock);
            io.Add(current);
            prior = current;
        }

        WdNeed((prior is null ? before : WdBoundary(prior, before)) == after && (state is null ? before == after && rows.Count == 0 : WdBoundary(state, before) == after), "final_boundary");
        return new(before, after, io, states, failed);
    }

    private static void WdSnapshot(WritableDiskIo r, Fixture.BlockBoundary before)
    {
        bool success = r.IsComplete && r.Status == B.Status.Success;
        WdNeed(before.Submitted != ulong.MaxValue && r.Owner == before.Owner && r.Epoch == before.Epoch && r.Generation == before.Submitted + 1 && r.Submitted == r.Generation && (r.Kind == B.Operation.Flush ? r.Sector == 0 : r.Sector < before.SectorCount) && (r.IsComplete ? r.Status is B.Status.Success or B.Status.DeviceIoError or B.Status.UnsupportedRequest : r.Status == B.Status.Uninitialized) && (!r.IsCopied || r.Kind == B.Operation.Read && success && !r.Cancelled), "io_request");
        WdNeed(r.Kind != B.Operation.Write || before.SubmittedBytes <= ulong.MaxValue - 512, "overflow");
        ulong submitted = before.SubmittedBytes + (r.Kind == B.Operation.Write ? 512UL : 0);
        ulong completed = before.Completed + (r.IsComplete ? 1UL : 0);
        ulong bytes = before.CompletedBytes + (success && r.Kind == B.Operation.Write ? 512UL : 0);
        ulong target = r.Kind == B.Operation.Flush ? before.CompletedBytes : 0;
        WdNeed(r.Completed == completed && r.SubmittedBytes == submitted && r.CompletedBytes == bytes && r.FlushTargetBytes == target && r.FlushAcknowledgedBytes == (success && r.Kind == B.Operation.Flush ? target : before.FlushAcknowledgedBytes) && r.Available == unchecked((ushort)r.Submitted) && r.Consumed == unchecked((ushort)completed) && r.CompletionBytes == (success && r.Kind != B.Operation.Flush ? 512U : 0U) && r.PayloadBytes == (r.Kind == B.Operation.Write || r.IsCopied ? 512U : 0U) && r.DataHex.Length == r.PayloadBytes * 2, "io_frontier");
    }

    private static bool WdResolved(WritableDiskIo r) => r.IsComplete && (r.Kind != B.Operation.Read || r.Status != B.Status.Success || r.Cancelled || r.IsCopied);
    private static Fixture.BlockBoundary WdBoundary(WritableDiskIo r, Fixture.BlockBoundary b) => new(b.Owner, b.Epoch, b.SectorCount, r.Submitted, r.Completed, r.SubmittedBytes, r.CompletedBytes, r.FlushAcknowledgedBytes);
    private static Fixture.BlockBoundary WdBoundary(IReadOnlyDictionary<string, ulong> r, Fixture.BlockBoundary b) => new(b.Owner, b.Epoch, b.SectorCount, r["submitted"], r["completed"], r["submittedBytes"], r["completedBytes"], r["flushAcknowledgedBytes"]);
    private static void WritableBoundary(Fixture.BlockBoundary b) => WdNeed(b.Owner >= 4096 && b.Owner < 0x0000800000000000 && b.Owner % 8 == 0 && b.Epoch != 0 && b.SectorCount is> 0 and <= ulong.MaxValue / 512 && b.Completed <= b.Submitted && b.Submitted - b.Completed <= 1 && b.SubmittedBytes % 512 == 0 && b.CompletedBytes % 512 == 0 && b.FlushAcknowledgedBytes % 512 == 0 && b.CompletedBytes <= b.SubmittedBytes && b.FlushAcknowledgedBytes <= b.CompletedBytes && b.SubmittedBytes / 512 <= b.Submitted && b.CompletedBytes / 512 <= b.Completed, "boundary");
    private static Dictionary<string, ulong> WdRecord(string text, string prefix, string keys) => ReadRecord(text, prefix, keys.Split(' ').Select(k => k + ":dec").ToArray());
    private static void WdClock(ulong now, ref ulong last)
    {
        WdNeed(now >= last && now <= long.MaxValue, "clock");
        last = now;
    }

    private static void WdNeed([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("writable_transcript_" + reason);
    }

    private static void WritableRows(IReadOnlyList<WritableDiskRow> rows, int originalCount)
    {
        WdNeed(rows is not null && rows.Count <= MaximumWritableDiskRows && originalCount > 0, "input");
        int last = -1;
        foreach (var row in rows!)
        {
            WdNeed(row.RecordIndex > last && row.RecordIndex < originalCount && row.Text is { Length: > 0 and <= 2048 } && row.Text.All(c => c is >= ' ' and <= '~'), "row");
            last = row.RecordIndex;
        }
    }

    /// <summary>Inspect the complete m16-009 leaf plus every indexed M16_027
    /// observation from the original capture. Every in-leaf writable row must
    /// match the supplied census byte-for-byte; unknown leaf/selected rows reject.
    /// Index gaps outside the leaf are explicitly the original parent's obligation.
    /// Canonical30/40 packages and their exact call/RIP inventories are required.
    /// This overload checks values without granting image or execution authority.</summary>

    public static WritableDiskValueReport InspectWritableDiskLeafValues(IReadOnlyList<string> leaf, int leafFirstIndex, int originalRowCount, IReadOnlyList<WritableDiskRow> writableRows, byte[] package, bool logUpload = false) => WdInspect(leaf, leafFirstIndex, originalRowCount, writableRows, package, logUpload, false);
    /// <summary>Additionally borrow exact128MiB initial/final image bytes and
    /// independently extracted Combined inspections. Require SHA equality,
    /// unchanged non-data bytes, guard files and canonical final data. Compare
    /// each IN against a bounded copied image view advanced only by successful
    /// OUT completion; require its exact final bytes. This is protocol/image
    /// consistency, never evidence that physical writes persisted or a guest ran.
    /// Caller retains unchanged original files/receipts and joins actual children.</summary>

    public static WritableDiskValueReport InspectWritableDiskValues(IReadOnlyList<string> leaf, int leafFirstIndex, int originalRowCount, IReadOnlyList<WritableDiskRow> writableRows, byte[] package, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, CombinedWebDataImageFixture.Inspection initial, CombinedWebDataImageFixture.Inspection final, bool logUpload = false)
    {
        var report = WdInspect(leaf, leafFirstIndex, originalRowCount, writableRows, package, logUpload, true);
        WdImages(report.Block, initialBytes, finalBytes, initial, final, logUpload);
        return report;
    }

    private static WritableDiskValueReport WdInspect(IReadOnlyList<string> leaf, int first, int originalCount, IReadOnlyList<WritableDiskRow> selected, byte[] package, bool logUpload, bool images)
    {
        WdNeed(leaf is not null && leaf.Count is> 0 and <= MaximumWritableDiskRows && first >= 0 && first <= originalCount - leaf.Count, "leaf_extent");
        WritableRows(selected, originalCount);
        if (logUpload)
            Fixture.RequireLogUploadPackage(package);
        else
            Fixture.RequirePackage(package);
        var calls = logUpload ? Fixture.LogUploadCalls() : Fixture.Calls();
        var instructions = logUpload ? Fixture.LogUploadInstructionRips() : Fixture.InstructionRips();
        WdNeed(calls.Count == (logUpload ? 40 : 30) && calls.All(c => instructions.Contains(c.ReturnRip)), "fixture_inventory");
        var rows = leaf!.Select((text, i) => new WritableDiskRow(first + i, text)).ToArray();
        WritableRows(rows, originalCount);
        var census = selected.ToDictionary(x => x.RecordIndex);
        List<WritableDiskRow> auxiliary = selected.ToList();
        foreach (var row in rows.Where(x => x.Text.StartsWith("M16_027_", StringComparison.Ordinal)))
            WdNeed(census.TryGetValue(row.RecordIndex, out var original) && original.Text == row.Text, "census_missing");
        foreach (var row in selected.Where(x => x.RecordIndex >= first && x.RecordIndex < first + rows.Length))
            WdNeed(rows[row.RecordIndex - first].Text == row.Text, "census_alias");
        var bindingRow = WdSingle(selected, "M16_027_BOUND ");
        const string bindingSuffix = " pci=0:7:0 queue=0 access=readwrite flush=negotiated reservation=retained";
        WdNeed(bindingRow.Text.EndsWith(bindingSuffix, StringComparison.Ordinal), "binding_policy");
        var binding = WdRecord(bindingRow.Text[..^bindingSuffix.Length], "M16_027_BOUND", "owner epoch arena pages bar capacity nativeOwner nativeOwnerBytes boot bootBytes carrier carrierBytes observation observationBytes engine engineEpoch port portEpoch mount mountEpoch provider providerEpoch readAttachment writeAttachment dataFirst dataLast webFirst webLast pid clock");
        WdBinding(binding);
        var stopRow = WdSingle(selected, "M16_027_STOP ");
        const string stopSuffix = " dma=zero reservation=retained";
        WdNeed(stopRow.Text.EndsWith(stopSuffix, StringComparison.Ordinal), "stop_policy");
        var stop = WdRecord(stopRow.Text[..^stopSuffix.Length], "M16_027_STOP", "submitted completed submittedBytes completedBytes flushAcknowledgedBytes rows clock");
        var endRow = WdSingle(selected, "M16_027_END ");
        const string endSuffix = " process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=retired dma=zero reservation=retained";
        WdNeed(endRow.Text.EndsWith(endSuffix, StringComparison.Ordinal), "end_policy");
        var end = WdRecord(endRow.Text[..^endSuffix.Length], "M16_027_END", "owner epoch reaped transfers irqs submitted completed submittedBytes completedBytes flushAcknowledgedBytes clock");
        var before = new Fixture.BlockBoundary(binding["owner"], binding["epoch"], binding["capacity"], 0, 0, 0, 0, 0);
        var after = WdBoundary(end, before);
        WdNeed(end["owner"] == before.Owner && end["epoch"] == before.Epoch && WdBoundary(stop, before) == after && bindingRow.RecordIndex < stopRow.RecordIndex && stopRow.RecordIndex < endRow.RecordIndex && endRow.RecordIndex >= first + rows.Length, "terminal_order");
        var blockRows = selected.Where(x => x.Text.StartsWith("M16_027_IO ", StringComparison.Ordinal) || x.Text.StartsWith("M16_027_STATE ", StringComparison.Ordinal)).ToArray();
        var block = InspectWritableBlockValues(blockRows, before, after);
        WdNeed(!block.HasCancelled && !block.HasDeviceError && !block.HasHeldRead && !block.HasUncompletedPublication && !block.Dirty && block.Io.Count != 0 && blockRows.All(x => x.RecordIndex < stopRow.RecordIndex) && stop["rows"] == (ulong)blockRows.Length && stop["rows"] <= 16384, "positive_drain");
        foreach (var row in block.States)
        {
            var s = WdRecord(row.Text, "M16_027_STATE", "owner epoch bound status running retired dirty retry submitted completed submittedBytes completedBytes flushAcknowledgedBytes clock");
            WdNeed(s["status"] is (ulong)B.Status.Uninitialized or (ulong)B.Status.Success && s["retry"] == 0, "positive_state");
        }

        var lastState = WdRecord(block.States[^1].Text, "M16_027_STATE", "owner epoch bound status running retired dirty retry submitted completed submittedBytes completedBytes flushAcknowledgedBytes clock");
        WdNeed(lastState["retired"] == 1 && lastState["running"] == 0 && lastState["dirty"] == 0, "retired_state");
        ulong ioClock = 0;
        foreach (var row in selected)
        {
            string prefix = row.Text.Split(' ', 2)[0];
            WdNeed(prefix is "M16_027_IO" or "M16_027_STATE" or "M16_027_BOUND" or "M16_027_STOP" or "M16_027_END" or "M16_027_BEGIN" or "M16_027_WAKE" or "M16_027_COMPLETE" or "M16_027_REPARK", "selected_unknown");
            if (prefix is "M16_027_BEGIN" or "M16_027_WAKE" or "M16_027_COMPLETE" or "M16_027_REPARK")
                WdNeed(row.RecordIndex >= first && row.RecordIndex < first + rows.Length, "wait_outside_leaf");
            else
            {
                int clockAt = row.Text.IndexOf(" clock=", StringComparison.Ordinal);
                WdNeed(clockAt >= 0, "clock_missing");
                int endAt = row.Text.IndexOf(' ', clockAt + 7);
                string number = endAt < 0 ? row.Text[(clockAt + 7)..] : row.Text[(clockAt + 7)..endAt];
                WdClock(Number(number), ref ioClock);
            }
        }

        ulong stack = DiskFileStack(package), identity = 0x100000001, tableClock = 0, epoch = 0, sequence = 0, request = 0, providerRequest = 0, fdOwner = 0;
        int at = 0, call = 0, transfers = 0, pendingAt = -1, beginAt = -1, supervisor = 0, lastReturn = -1, activeWindow = -1;
        bool reaped = false, exited = false, exitCall = false, witness = false;
        Dictionary<string, ulong>? pending = null;
        Fixture.CallSpec active = default;
        bool woke = false;
        Dictionary<ulong, (ulong Generation, ulong Description, ulong Binding)> fds = [];
        Dictionary<ulong, ulong> fdGenerations = [];
        List<WritableDiskWait> waits = [];
        void SkipDevice()
        {
            while (at < rows.Length && rows[at].Text.StartsWith("M16_027_", StringComparison.Ordinal) && !rows[at].Text.StartsWith("M16_027_BEGIN ", StringComparison.Ordinal) && !rows[at].Text.StartsWith("M16_027_WAKE ", StringComparison.Ordinal) && !rows[at].Text.StartsWith("M16_027_COMPLETE ", StringComparison.Ordinal) && !rows[at].Text.StartsWith("M16_027_REPARK ", StringComparison.Ordinal))
            {
                at++;
            }
        }

        string Take()
        {
            SkipDevice();
            WdNeed(at < rows.Length, "truncated");
            return rows[at++].Text;
        }

        void Exact(string text) => WdNeed(Take() == text, "leaf_order");
        Exact("TEST_START m16-009");
        LinuxMemoryPolicy(Take());
        // Native package views expose the ELF payload, not its envelope.
        uint payloadBytes = OS.Manifest.LinuxElfPackage.Validate(package).PayloadBytes;
        Exact("M16_007_POLICY descriptors=32 pipes=2 capacity=4096 scratch=8192 packageBytes=" + payloadBytes.ToString(CultureInfo.InvariantCulture) + " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        int networkAt = at;
        Exact("M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        auxiliary.Add(rows[networkAt]);
        ulong userRoot = LinuxSyscallArm(Take())["root"];
        int startAt = at;
        Exact("TEST_START m16.027");
        auxiliary.Add(rows[startAt]);
        WdNeed(selected.All(x => x.RecordIndex > first + startAt) && bindingRow.RecordIndex >= first + at, "bootstrap_order");
        const string boundSuffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string nativeBound = Take();
        WdNeed(nativeBound.EndsWith(boundSuffix, StringComparison.Ordinal), "user_irq_policy");
        var native = WdRecord(nativeBound[..^boundSuffix.Length], "M16_008_BOUND", "root pool workers code metadata userImage");
        WdNeed(native["workers"] == 2 && native["code"] == LinuxThreadProgram.Bytes && native["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && native["userImage"] == 728 && native["pool"] >= 4096 && native["pool"] % 4096 == 0 && bindingRow.RecordIndex < first + at, "native_bound");
        DistinctExtents([(userRoot, 4096), (native["root"], 4096), (native["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL)], "writable_roots");
        var budget = WdRecord(Take(), "M16_008_BUDGET", "started epochs steps nanos");
        WdNeed(budget["epochs"] == 8192 && budget["steps"] == 16384 && budget["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds && budget["started"] <= long.MaxValue, "budget");
        lastReturn = first + at - 1;
        while (!reaped)
        {
            SkipDevice();
            WdNeed(at < rows.Length, "truncated");
            int index = at;
            string text = rows[at].Text;
            if (text.StartsWith("M16_012_POLL ", StringComparison.Ordinal))
            {
                var p = WdRecord(Take(), "M16_012_POLL", "identity clock ready idle");
                WdNeed(p["identity"] is 0 or 0x100000001 && p["ready"] <= 1 && p["idle"] <= 1 && (p["idle"] == 0 || p["identity"] == 0), "poll");
                WdClock(p["clock"], ref tableClock);
                auxiliary.Add(rows[index]);
                continue;
            }

            if (text.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal) || text.StartsWith("M16_008_TICK ", StringComparison.Ordinal))
            {
                bool drain = text.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal);
                var t = WdRecord(Take(), drain ? "M16_009_DRAIN" : "M16_008_TICK", drain ? "identity epoch nanos" : "epoch nanos ready");
                WdNeed(pending is not null && supervisor == 0 && t["epoch"] == ++epoch && epoch < budget["epochs"] && (drain ? t["identity"] == identity : t["ready"] <= 1), "timer");
                WdClock(t["nanos"], ref tableClock);
                auxiliary.Add(rows[index]);
                continue;
            }

            if (text.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
            {
                WdNeed(call != 0 && (rows[index - 1].Text.StartsWith("M16_008_CALL ", StringComparison.Ordinal) || rows[index - 1].Text.StartsWith("M16_027_BEGIN ", StringComparison.Ordinal)), "syscall_timing_order");
                _ = LinuxSyscallTiming(Take(), identity, (ulong)call, calls[call - 1].Number);
                auxiliary.Add(rows[index]);
                continue;
            }

            if (text.StartsWith("M16_008_TIMING ", StringComparison.Ordinal))
            {
                WdNeed(index != 0 && rows[index - 1].Text.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal), "timing_order");
                LinuxTiming(Take(), identity, epoch);
                auxiliary.Add(rows[index]);
                continue;
            }

            if (text.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                WdNeed(call == calls.Count - 2 && pending is null && !witness, "witness_order");
                string value = logUpload ? Fixture.LogUploadWitness : Fixture.Witness;
                Exact("M16_005_WRITE fd=1 bytes=" + value.Length.ToString(CultureInfo.InvariantCulture) + " data=" + value);
                witness = true;
                continue;
            }

            if (text.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
            {
                WdNeed(call < calls.Count && pending is null && supervisor == 0 && !exitCall, "call_order");
                active = calls[call];
                activeWindow = lastReturn;
                var c = WdRecord(Take(), "M16_008_CALL", "identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status ordinal waitSource waitDeadline waitSequence signalDepth signalFrame blockedSignals returnRip returnRsp returnFlags returnFs returnGs returnValueBits");
                WdNeed(c["identity"] == identity && c["ordinal"] == (ulong)++call && c["nr"] == active.Number && c["a0"] == active.A0 && c["a1"] == active.A1 && c["a2"] == active.A2 && c["a3"] == active.A3 && c["a4"] == active.A4 && c["a5"] == active.A5 && c["rip"] == active.ReturnRip && c["rsp"] == stack && c["fs"] == 0 && c["gs"] == 0 && LinuxSyscallGateway.ValidateUserFlags(c["flags"], false) && (c["flags"] & 0x400) == 0 && c["signalDepth"] == 0 && c["signalFrame"] == 0 && c["blockedSignals"] == 0 && c["returnRip"] == c["rip"] && c["returnRsp"] == stack && c["returnFlags"] == c["flags"] && c["returnFs"] == 0 && c["returnGs"] == 0 && c["returnValueBits"] == c["valueBits"], "call");
                WdClock(c["clock"], ref tableClock);
                bool rootOpen = WdRootOpen(active);
                bool async = active.Number is 0 or 17 or 74 or 328 or 87 || active.Number == 2 && active.Role != "open.outside" && !rootOpen;
                if (active.Role == "read.closed")
                    async = false;
                if (async)
                {
                    WdNeed(c["action"] == 3 && c["status"] == 9 && c["valueBits"] == 0 && c["waitSource"] == 5 && c["waitDeadline"] == 0 && c["waitSequence"] == ++sequence, "call_wait");
                    pendingAt = first + index;
                    beginAt = first + at;
                    pending = WdRecord(Take(), "M16_027_BEGIN", "identity ordinal sequence requestOwner requestEpoch request nr rip outputPrefix fdOwner fd fdGeneration description bindingGeneration reservedFd reservedDescription reservationGeneration providerOwner providerEpoch providerRequest operation phase clock");
                    WdNeed(beginAt == pendingAt + 1 && pending["identity"] == identity && pending["ordinal"] == (ulong)call && pending["sequence"] == sequence && pending["requestOwner"] == binding["writeAttachment"] && pending["requestEpoch"] == binding["providerEpoch"] && pending["request"] == ++request && pending["nr"] == active.Number && pending["rip"] == active.ReturnRip && pending["outputPrefix"] == active.OutputBytes && pending["phase"] is 1 or 2 && pending["operation"] == WdOperation(active.Number), "begin");
                    WdBeginBinding(pending, binding, active, fds, fdGenerations, ref fdOwner, ref providerRequest);
                    WdClock(pending["clock"], ref tableClock);
                    supervisor = 3;
                    woke = false;
                }
                else
                {
                    bool exit = active.Number == 231;
                    WdNeed(c["waitSource"] == 0 && c["waitDeadline"] == 0 && c["waitSequence"] == 0 && c["valueBits"] == unchecked((ulong)active.Result) && c["action"] == (exit ? 2UL : 1UL) && c["status"] == (exit ? 2UL : 1UL), "immediate");
                    if (rootOpen)
                    {
                        // Mounted-root open publishes immediately after consuming
                        // one descriptor request, without a provider wait/token.
                        ulong free = 3;
                        while (fds.ContainsKey(free))
                            free++;
                        ulong description = 3;
                        while (fds.Values.Any(x => x.Description == description))
                            description++;
                        fdGenerations.TryGetValue(free, out ulong generation);
                        WdNeed(free < 32 && description < 32 && active.Result >= 3 && (ulong)active.Result == free && generation < uint.MaxValue, "root_open_publish");
                        fdGenerations[free] = generation + 1;
                        fds.Add(free, (generation + 1, description, ++request));
                    }

                    if (active.Number == 3)
                        WdNeed(fds.Remove(active.A0), "close_binding");
                    if (exit)
                    {
                        WdNeed(witness && fds.Count == 0, "exit_fds");
                        exitCall = true;
                        supervisor = 2;
                    }

                    lastReturn = first + index;
                }

                continue;
            }

            if (text.StartsWith("M16_027_WAKE ", StringComparison.Ordinal))
            {
                WdNeed(pending is not null && !woke, "wake_order");
                var w = WdRecord(Take(), "M16_027_WAKE", "identity sequence source requestOwner requestEpoch request outputPrefix completion state clock");
                WdNeed(w["identity"] == identity && w["sequence"] == sequence && w["source"] == 5 && w["requestOwner"] == pending!["requestOwner"] && w["requestEpoch"] == pending["requestEpoch"] && w["request"] == pending["request"] && w["outputPrefix"] == pending["outputPrefix"] && w["completion"] == (ulong)KernelThreadTable.Completion.Woken && w["state"] == (ulong)KernelThreadTable.State.Ready, "wake");
                WdClock(w["clock"], ref tableClock);
                woke = true;
                continue;
            }

            if (text.StartsWith("M16_027_COMPLETE ", StringComparison.Ordinal))
            {
                WdNeed(pending is not null && supervisor == 0 && woke, "completion_order");
                var c = WdRecord(Take(), "M16_027_COMPLETE", "identity sequence requestOwner requestEpoch request nr rip outputBytes completion action valueBits clock");
                WdNeed(c["identity"] == identity && c["sequence"] == sequence && c["requestOwner"] == pending!["requestOwner"] && c["requestEpoch"] == pending["requestEpoch"] && c["request"] == pending["request"] && c["nr"] == active.Number && c["rip"] == active.ReturnRip && c["outputBytes"] == active.OutputBytes && c["completion"] == (ulong)KernelThreadTable.Completion.Woken, "completion");
                WdClock(c["clock"], ref tableClock);
                if (c["action"] == 3)
                {
                    WdNeed(c["valueBits"] == 0, "repark_value");
                    var p = WdRecord(Take(), "M16_027_REPARK", "identity previous sequence request clock");
                    WdNeed(p["identity"] == identity && p["previous"] == sequence && p["sequence"] == ++sequence && p["request"] == pending["request"], "repark");
                    WdClock(p["clock"], ref tableClock);
                    supervisor = 3;
                    woke = false;
                    continue;
                }

                WdNeed(c["action"] == 1 && c["valueBits"] == unchecked((ulong)active.Result), "completion_value");
                waits.Add(new(activeWindow, pendingAt, beginAt, first + index, identity, (ulong)call, sequence, pending["requestOwner"], pending["requestEpoch"], pending["request"], (uint)pending["outputPrefix"], active.Result));
                if (active.Number == 2 && active.Result >= 0)
                {
                    ulong fd = (ulong)active.Result;
                    WdNeed(fd == pending["reservedFd"] && !fds.ContainsKey(fd) && pending["reservationGeneration"] < uint.MaxValue, "open_publish");
                    fdGenerations[fd] = pending["reservationGeneration"] + 1;
                    fds.Add(fd, (fdGenerations[fd], pending["reservedDescription"], request));
                }

                var resume = WdRecord(Take(), "M16_008_RESUME", "identity valueBits status");
                WdNeed(resume["identity"] == identity && resume["valueBits"] == c["valueBits"] && resume["status"] == 1, "resume");
                lastReturn = first + at - 1;
                pending = null;
                continue;
            }

            if (text.StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
            {
                WdNeed(exitCall && !exited && supervisor == 2, "exit_order");
                Exact("M16_008_EXIT identity=4294967297 clearTidResult=0");
                exited = true;
                continue;
            }

            if (text.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal))
            {
                var s = WdRecord(Take(), "M16_008_SUPERVISOR", "identity action transfers ready active");
                transfers += 2;
                WdNeed(supervisor != 0 && (supervisor != 2 || exited) && s["identity"] == identity && s["action"] == (ulong)supervisor && s["transfers"] == (ulong)transfers && s["active"] == 1 && s["ready"] <= 1 && (supervisor != 2 || s["ready"] == 0), "supervisor");
                supervisor = 0;
                continue;
            }

            if (text.StartsWith("M16_008_REAP ", StringComparison.Ordinal))
            {
                WdNeed(exited && supervisor == 0 && pending is null && call == calls.Count, "reap_order");
                Exact("M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero");
                reaped = true;
                continue;
            }

            throw new ToolFailure("writable_transcript_leaf_unknown", "index=" + (first + index).ToString(CultureInfo.InvariantCulture));
        }

        int reapIndex = first + at - 1;
        for (int fd = 0; fd < 3; fd++)
        {
            SkipDevice();
            int index = at;
            Exact("M16_011_CLOSE fd=" + fd.ToString(CultureInfo.InvariantCulture) + " valueBits=0");
            auxiliary.Add(rows[index]);
        }

        var finish = WdRecord(Take(), "M16_008_BUDGET_END", "nanos");
        WdNeed(finish["nanos"] >= tableClock && finish["nanos"] >= budget["started"] && finish["nanos"] <= long.MaxValue && finish["nanos"] - budget["started"] < budget["nanos"], "budget_end");
        const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        string ownerText = Take();
        WdNeed(ownerText.EndsWith(cleanup, StringComparison.Ordinal), "cleanup");
        var owner = WdRecord(ownerText[..^cleanup.Length], "M16_008_OWNER_END", "reaped transfers irqs exit");
        WdNeed(owner["reaped"] == 1 && owner["transfers"] == (ulong)transfers && owner["irqs"] == epoch && owner["exit"] == 42 && end["reaped"] == 1 && end["transfers"] == (ulong)transfers && end["irqs"] == epoch && stopRow.RecordIndex > reapIndex && stopRow.RecordIndex < first + at && waits.Count != 0, "owner");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        Exact("CLROS_M16_009_READY");
        Exact("TEST_PASS m16-009");
        WdNeed(at == rows.Length && transfers / 2 < (int)budget["steps"], "leaf_tail");
        WdIoWindows(block, waits, calls, bindingRow.RecordIndex);
        return new(block, waits, auxiliary, call, transfers, epoch, images);
    }

    // A dispatch can touch B before its CALL/BEGIN rows are serialized. Use
    // the previous completed call boundary, not BEGIN, as the causal lower bound.
    // This is only the finite single-worker serialized-value association.
    private static void WdIoWindows(WritableBlockValueReport block, IReadOnlyList<WritableDiskWait> waits, IReadOnlyList<Fixture.CallSpec> calls, int boundIndex)
    {
        var groups = block.Io.GroupBy(x => x.Generation).Select(g => g.ToArray()).ToArray();
        var assigned = waits.ToDictionary(w => w.Ordinal, _ => new List<WritableDiskIo>());
        int bootstrap = 0;
        foreach (var group in groups)
        {
            if (group[^1].RecordIndex < boundIndex)
            {
                WdNeed(group.All(r => r.Kind == B.Operation.Read) && group[^1].IsCopied, "bootstrap_io");
                bootstrap++;
                continue;
            }

            var candidates = waits.Where(w => group[0].RecordIndex > w.WindowFirstRecordIndex && group[^1].RecordIndex < w.CompleteRecordIndex).ToArray();
            WdNeed(candidates.Length == 1, "io_window");
            assigned[candidates[0].Ordinal].Add(group[^1]);
        }

        WdNeed(bootstrap >= OS.Storage.ContinuingFat32Bootstrap.MaximumReads, "bootstrap_reads");
        foreach (var wait in waits)
        {
            var call = calls[checked((int)wait.Ordinal - 1)];
            var io = assigned[wait.Ordinal];
            bool mutation = call.Number is 328 or 87 || call.Number == 2 && (call.A1 & 0x40) != 0;
            WdNeed(mutation || io.All(x => x.Kind != B.Operation.Write), "unexpected_out");
            if (mutation)
            {
                var writes = io.Where(x => x.Kind == B.Operation.Write).ToArray();
                WdNeed(writes.Length != 0 && io.Any(x => x.Kind == B.Operation.Flush && x.FlushAcknowledgedBytes >= writes.Max(w => w.SubmittedBytes)), "mutation_flush");
            }

            if (call.Number == 74)
                WdNeed(io.Any(x => x.Kind == B.Operation.Flush), "sync_flush");
            if (call.Number is 0 or 17 && call.Result > 0)
                WdNeed(io.Any(x => x.Kind == B.Operation.Read && x.IsCopied), "read_copy");
        }
    }

    private static WritableDiskRow WdSingle(IReadOnlyList<WritableDiskRow> rows, string prefix)
    {
        var matches = rows.Where(x => x.Text.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        WdNeed(matches.Length == 1, "unique_" + prefix.Trim());
        return matches[0];
    }

    private static ulong WdOperation(ulong nr) => nr switch
    {
        2 => 1,
        0 or 17 => 2,
        328 => 3,
        74 => 4,
        87 => 5,
        _ => 0
    };
    private static void WdBinding(IReadOnlyDictionary<string, ulong> b)
    {
        // The boot arena stays below4GiB; retained supervisor MMIO uses its
        // separate supported mapping envelope, including decoded Memory64 BARs.
        WdNeed(b["pages"] is> 0 and <= 256 && b["arena"] >= 4096 && b["arena"] % 4096 == 0 && b["arena"] <= 0x100000000UL - b["pages"] * 4096 && b["bar"] >= 0x100000 && b["bar"] % 16384 == 0 && b["bar"] <= OS.Kernel.Memory.X64UserAddressSpace.UserEnd - 16384 && b["capacity"] == CombinedWebDataImageFixture.Sectors && b["dataFirst"] == CombinedWebDataImageFixture.DataFirstLba && b["dataLast"] == CombinedWebDataImageFixture.DataLastLba && b["webFirst"] == CombinedWebDataImageFixture.WebFirstLba && b["webLast"] == CombinedWebDataImageFixture.WebLastLba && b["pid"] == 1 && b["epoch"] is> 0 and <= ulong.MaxValue - 4 && b["engineEpoch"] == b["epoch"] + 2 && b["providerEpoch"] == b["engineEpoch"] && b["portEpoch"] == b["epoch"] + 3 && b["mountEpoch"] == b["epoch"] + 4 && b["clock"] <= long.MaxValue && b["nativeOwnerBytes"] is> 0 and <= 4096 && b["bootBytes"] is> 0 and <= 1024 * 1024 && b["carrierBytes"] is> 0 and <= 4096 && b["observationBytes"] is >= 512 and <= 4096, "binding");
        ulong arena = b["arena"], length = b["pages"] * 4096;
        List<(ulong, ulong)> inside = [];
        foreach (string name in new[]
        {
            "owner",
            "engine",
            "port",
            "mount",
            "provider",
            "readAttachment",
            "writeAttachment",
            "carrier",
            "observation"
        }

        )
        {
            ulong bytes = name == "carrier" ? b["carrierBytes"] : name == "observation" ? b["observationBytes"] : 8;
            WdNeed(b[name] >= arena + 4096 && b[name] % 8 == 0 && b[name] - arena <= length - bytes, "binding_pointer");
            inside.Add((b[name], bytes));
        }

        DistinctExtents(inside, "writable_binding_internal");
        DistinctExtents([(arena, length), (b["bar"], 16384), (b["boot"], b["bootBytes"]), (b["nativeOwner"], b["nativeOwnerBytes"])], "writable_binding_external");
    }

    private static bool WdRootOpen(Fixture.CallSpec call) => call.Number == 2 && (call.Role is "sync.parent-open" or "sync.deleted-parent-open" or "parent.open") && call.A0 == Fixture.RootName && call.A1 == Fixture.RootFlags && call.A2 == 0 && call.A3 == 0 && call.A4 == 0 && call.A5 == 0 && call.OutputBytes == 0;
    private static void WdBeginBinding(Dictionary<string, ulong> p, IReadOnlyDictionary<string, ulong> b, Fixture.CallSpec call, Dictionary<ulong, (ulong Generation, ulong Description, ulong Binding)> fds, Dictionary<ulong, ulong> generations, ref ulong owner, ref ulong providerRequest)
    {
        WdNeed(new[] { "fd", "fdGeneration", "description", "reservedFd", "reservedDescription", "reservationGeneration" }.All(k => p[k] <= uint.MaxValue) && p["phase"] <= byte.MaxValue && p["operation"] <= byte.MaxValue && p["description"] < 32 && p["reservedFd"] < 32 && p["reservedDescription"] < 32 && p["outputPrefix"] <= 8192, "begin_width");
        if (p["providerOwner"] == 0)
            WdNeed(p["providerEpoch"] == 0 && p["providerRequest"] == 0, "provider_zero");
        else
        {
            WdNeed(p["providerOwner"] == b["provider"] && p["providerEpoch"] == b["providerEpoch"] && p["providerRequest"] > providerRequest, "provider_token");
            providerRequest = p["providerRequest"];
        }

        if (call.Number is 2 or 87)
        {
            WdNeed(p["fdOwner"] == 0 && p["fd"] == 0 && p["fdGeneration"] == 0 && p["description"] == 0 && p["bindingGeneration"] == 0, "path_fd");
            if (call.Number == 2)
            {
                ulong free = 3;
                while (fds.ContainsKey(free))
                    free++;
                ulong description = 3;
                while (fds.Values.Any(x => x.Description == description))
                    description++;
                WdNeed(p["reservedFd"] == free && p["reservedFd"] >= 3 && p["reservedDescription"] == description, "reservation");
                generations.TryGetValue(free, out ulong generation);
                WdNeed(p["reservationGeneration"] == generation, "reservation_generation");
            }
            else
                WdNeed(p["reservedFd"] == 0 && p["reservedDescription"] == 0 && p["reservationGeneration"] == 0, "unlink_reservation");
        }
        else
        {
            WdNeed(fds.TryGetValue(call.A0, out var fd) && p["fd"] == call.A0 && p["fdGeneration"] == fd.Generation && p["description"] == fd.Description && p["bindingGeneration"] == fd.Binding && p["fdOwner"] >= b["boot"] && p["fdOwner"] % 8 == 0 && p["fdOwner"] - b["boot"] < b["bootBytes"] && (owner == 0 || owner == p["fdOwner"]) && p["reservedFd"] == 0 && p["reservedDescription"] == 0 && p["reservationGeneration"] == 0, "fd_binding");
            owner = p["fdOwner"];
        }
    }

    private static void WdImages(WritableBlockValueReport block, ReadOnlySpan<byte> initialBytes, ReadOnlySpan<byte> finalBytes, CombinedWebDataImageFixture.Inspection initial, CombinedWebDataImageFixture.Inspection final, bool logUpload)
    {
        WdNeed(initial is not null && final is not null && initialBytes.Length == CombinedWebDataImageFixture.ImageBytes && finalBytes.Length == initialBytes.Length && Convert.ToHexString(SHA256.HashData(initialBytes)).Equals(initial!.RawSha256, StringComparison.OrdinalIgnoreCase) && Convert.ToHexString(SHA256.HashData(finalBytes)).Equals(final!.RawSha256, StringComparison.OrdinalIgnoreCase), "image_hash");
        WdNeed(initial!.CapacitySectors == block.Before.SectorCount && final!.CapacitySectors == block.Before.SectorCount && initial.DiskGuid == final.DiskGuid && initial.WebPartitionGuid == final.WebPartitionGuid && initial.DataPartitionGuid == final.DataPartitionGuid && initial.WebFirst == final.WebFirst && initial.WebLast == final.WebLast && initial.DataFirst == final.DataFirst && initial.DataLast == final.DataLast && initial.WebPartitionSha256 == final.WebPartitionSha256, "image_geometry");
        if (logUpload)
            WdNeed(initial.Data.Find("LOG.TXT")is { Name: "LOG.TXT", Bytes: 0 } && initial.Data.Find("UPLOAD.BIN")is { Name: "UPLOAD.BIN", Bytes: 0 } && final.Data.Find("LOG.TXT")is not null && final.Data.Find("UPLOAD.BIN")is not null, "named_seeds");
        int first = checked((int)(initial.DataFirst * 512)), end = checked((int)((initial.DataLast + 1) * 512));
        WdNeed(initialBytes[..first].SequenceEqual(finalBytes[..first]) && initialBytes[end..].SequenceEqual(finalBytes[end..]), "image_outside_data");
        WdNeed(initialBytes[first + 13] == 1 && finalBytes[first + 13] == 1 && initialBytes.Slice(first, 512).SequenceEqual(finalBytes.Slice(first, 512)) && initialBytes.Slice(first + 6 * 512, 512).SequenceEqual(finalBytes.Slice(first + 6 * 512, 512)), "image_bpb");
        HashSet<ulong> protectedSectors = [];
        foreach (var file in initial.Data.Files)
        {
            if (logUpload && file.Name is "LOG.TXT" or "UPLOAD.BIN")
                continue;
            foreach (var extent in file.Extents)
                for (uint i = 0; i < extent.Clusters; i++)
                    protectedSectors.Add(extent.FirstLba + i);
        }

        Dictionary<ulong, byte[]> changed = [];
        HashSet<ulong> completed = [];
        foreach (var row in block.Io)
        {
            int offset = checked((int)(row.Sector * 512));
            if (row.Kind == B.Operation.Write && row.IsComplete && completed.Add(row.Generation))
            {
                WdNeed(row.Sector >= initial.DataFirst && row.Sector <= initial.DataLast && (changed.ContainsKey(row.Sector) || changed.Count < 4096), "write_extent");
                WdNeed(!protectedSectors.Contains(row.Sector) && (row.Sector >= initial.Data.FirstFatSector || row.Sector == initial.DataFirst + 1 || row.Sector == initial.DataFirst + 7), "write_guard");
                changed[row.Sector] = row.CopyPayload();
            }
            else if (row.Kind == B.Operation.Read && row.IsCopied)
            {
                ReadOnlySpan<byte> expected = changed.TryGetValue(row.Sector, out var bytes) ? bytes : initialBytes.Slice(offset, 512);
                WdNeed(row.CopyPayload().AsSpan().SequenceEqual(expected), "read_bytes");
            }
        }

        for (ulong lba = initial.DataFirst; lba <= initial.DataLast; lba++)
        {
            int offset = checked((int)(lba * 512));
            ReadOnlySpan<byte> expected = changed.TryGetValue(lba, out var bytes) ? bytes : initialBytes.Slice(offset, 512);
            WdNeed(finalBytes.Slice(offset, 512).SequenceEqual(expected), "final_bytes");
        }

        WdNeed(initial.Data.Files.Count == final.Data.Files.Count && initial.Data.Find(WritableDataImageFixture.ExclusiveCreateName)is null && final.Data.Find(WritableDataImageFixture.ExclusiveCreateName)is null, "file_inventory");
        foreach (var file in initial.Data.Files)
        {
            var after = final.Data.Find(file.Name);
            WdNeed(after is not null && after.Name == file.Name && after.ShortAlias == file.ShortAlias && after.ShortEntryOffset == file.ShortEntryOffset && after.Attributes == file.Attributes && after.FatDate == file.FatDate && after.FatTime == file.FatTime, "file_identity");
            bool target = logUpload && file.Name is "LOG.TXT" or "UPLOAD.BIN";
            if (target)
            {
                WdNeed(file.Bytes == 0, "seed_not_empty");
                var bytes = file.Name == "LOG.TXT" ? NativeAotLogUploadContract.ExpectedLog() : NativeAotLogUploadContract.ExpectedUpload();
                WdNeed(after!.CopyBytes().AsSpan().SequenceEqual(bytes), "target_bytes");
            }
            else
                WdNeed(after!.Sha256 == file.Sha256 && after.Clusters.SequenceEqual(file.Clusters), "guard_file");
        }
    }
}
