using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static class LinuxDiskRaceTranscriptTests
{
    [HostTest("exit pending device joins every real sector and the last submit to reset retirement")]
    private static void ExitPendingDevice()
    {
        var (rows, image) = ExitPendingDeviceFixture(); string[] before = rows.ToArray();
        byte[] package = F.CreatePackage("ExitPending");
        var result = QemuTranscripts.InspectLinuxDiskExitPendingDeviceValues(rows, package, image);
        HostCheck.True(!result.CheckedOriginalExecution && !result.CheckedNativeStopOrUserMemory &&
            result.BindingRecordIndex == 0 && result.IoRecordIndices.SequenceEqual(Enumerable.Range(1, 7)) && rows.SequenceEqual(before));
        HostCheck.True(result.Device.Counters.Submitted == 40 && result.Device.Counters.Completed == 39 &&
            result.Device.Counters.Copied == 39 && result.Device.Cancelled == 0 && result.Device.ResetRetirement is not null &&
            result.Device.Rows[^1].Kind == NativeDiskReadObservations.Kind.Submitted && result.Device.Rows[^1].Sector == 1000 &&
            result.Device.Rows.Count(r => r.Kind == NativeDiskReadObservations.Kind.Copied) == 2);
        string retained = result.Device.Rows[2].DataHex; Array.Fill(rows, "changed"); Array.Clear(image); Array.Clear(package);
        HostCheck.Equal(retained, result.Device.Rows[2].DataHex);
        bool denied = false; try { ((IList<int>)result.IoRecordIndices)[0] = 0; } catch (NotSupportedException) { denied = true; }
        HostCheck.True(denied);
    }

    [HostTest("exit pending device rejects unjoined bindings missing bytes post-death pumps and invented completions")]
    private static void ExitPendingDeviceRejections()
    {
        var (rows, image) = ExitPendingDeviceFixture();
        void Deny(string[] changed, byte[]? bytes = null)
        {
            bool rejected = false;
            try { _ = QemuTranscripts.InspectLinuxDiskExitPendingDeviceValues(changed, F.CreatePackage("ExitPending"), bytes ?? image); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        foreach (string key in new[] { "owner", "epoch", "capacity", "bootstrapCompleted", "bootstrapCopied" })
            Deny(Change(rows, 0, key, Field(rows[0], key) + 1));
        Deny(Change(rows, 0, "clock", 101)); Deny(Change(rows, 1, "clock", 0)); Deny(Change(rows, 7, "clock", 101));
        for (int i = 0; i <= 7; i++) Deny(rows.Where((_, n) => n != i).ToArray());
        Deny([rows[1], rows[0], ..rows[2..]]); Deny([rows[0], rows[0], ..rows[1..]]);
        Deny([..rows, rows[7]]); // Even a copied-valid last submit may not appear after stage1.
        Deny([..rows[..7], ..rows[8..], rows[7]]);
        Deny(Change(rows, 7, "generation", 39)); Deny(Change(rows, 7, "sector", 999));
        Deny([..rows[..7], rows[7], rows[7].Replace("kind=0", "kind=1", StringComparison.Ordinal), ..rows[8..]]);
        Deny(rows, image[..^512]); byte[] wrong = image.ToArray(); wrong[3 * 512] ^= 1; Deny(rows, wrong);
        // A changed terminal generation and sector across every stage still must
        // match the actual last observed request, rather than self-consistency.
        string[] stagesChanged = rows.ToArray();
        for (int i = 8; i < rows.Length; i++)
        { stagesChanged = Change(stagesChanged, i, "sector", 999); stagesChanged = Change(stagesChanged, i, "ioSector", 999); }
        Deny(stagesChanged);
    }

    private static (string[] Rows, byte[] Image) ExitPendingDeviceFixture()
    {
        byte[] image = new byte[1001 * 512];
        for (int i = 0; i < image.Length; i++) image[i] = unchecked((byte)(i * 13 + (i >> 9)));
        const ulong owner = 0x120000, epoch = 0x4d31365f30323301;
        List<string> rows = [$"M16_023_BOUND owner={owner} epoch={epoch} arena=9437184 pages=90 bar=4026531840 capacity=1001 bootstrapCompleted=37 bootstrapCopied=37 clock=1 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained"];
        ulong clock = 10;
        for (ulong generation = 38; generation <= 40; generation++)
        {
            ulong sector = generation == 40 ? 1000 : generation - 35;
            rows.Add($"M16_023_IO kind=0 owner={owner} epoch={epoch} generation={generation} sector={sector} sequence={generation} bytes=512 available={generation} consumed={generation - 1} clock={clock++}");
            if (generation == 40) break;
            rows.Add($"M16_023_IO kind=1 owner={owner} epoch={epoch} generation={generation} sector={sector} sequence={generation} bytes=512 available={generation} consumed={generation} clock={clock++}");
            rows.Add($"M16_023_IO kind=2 owner={owner} epoch={epoch} generation={generation} sector={sector} sequence={generation} bytes=512 available={generation} consumed={generation} clock={clock++} copied=committed data={Convert.ToHexString(image.AsSpan((int)sector * 512, 512)).ToLowerInvariant()}");
        }
        rows.AddRange(ExitPendingStages(false)); return (rows.ToArray(), image);
    }

    [HostTest("exit pending copied stages preserve reset-aborted counters without granting guest authority")]
    private static void ExitPendingCopiedStages()
    {
        foreach (bool drained in new[] { false })
        {
            string[] rows = ExitPendingStages(drained), before = rows.ToArray(); byte[] package = F.CreatePackage("ExitPending");
            var values = QemuTranscripts.InspectLinuxDiskExitPendingValues(rows, package);
            HostCheck.True(values.ResetAbortedRequest == !drained && !values.CheckedOriginalExecution && !values.CheckedNativeStopOrUserMemory);
            HostCheck.True(values.Records.Select(r => r.RecordIndex).SequenceEqual(Enumerable.Range(0, 8)) && rows.SequenceEqual(before));
            string retained = values.Records[0].Values["main"].ToString(CultureInfo.InvariantCulture);
            Array.Fill(rows, "changed"); Array.Clear(package);
            HostCheck.Equal(F.MainIdentity.ToString(CultureInfo.InvariantCulture), retained);
            bool denied = false;
            try { ((IDictionary<string, ulong>)values.Records[0].Values)["main"] = 0; } catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
    }

    [HostTest("exit pending copied stages reject altered owners identities counters reset sentinel and teardown ordering")]
    private static void ExitPendingCopiedRejections()
    {
        string[] rows = ExitPendingStages(false);
        void Deny(string[] changed, byte[]? package = null)
        {
            bool rejected = false;
            try { _ = QemuTranscripts.InspectLinuxDiskExitPendingValues(changed, package ?? F.CreatePackage("ExitPending")); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        foreach (string key in new[] { "main", "child", "ordinal", "rip", "sequence", "owner", "epoch", "generation", "index", "fd", "fdGeneration",
            "binding", "bindingGeneration", "description", "position", "fileGeneration", "fileIndex", "inode", "fsOwner", "fsEpoch", "fsGeneration",
            "fsIndex", "blockOwner", "blockEpoch", "blockGeneration", "sector", "outputBytes", "submitted", "copied" })
            Deny(Change(rows, 1, key, Field(rows[1], key) + 1));
        foreach (int index in Enumerable.Range(0, 8))
        {
            Deny(Change(rows, index, "stage", index == 0 ? 2UL : 1UL));
            Deny(Change(rows, index, "available", 0)); Deny(Change(rows, index, "consumed", 0));
            Deny(Change(rows, index, "sentinelBytes", Field(rows[index], "sentinelBytes") + 1));
            Deny(Change(rows, index, "completed", Field(rows[index], "completed") + 1));
            Deny(Change(rows, index, "cancelled", Field(rows[index], "cancelled") + 1));
            Deny(rows.Where((_, i) => i != index).ToArray());
            Deny([..rows[..index], rows[index], ..rows[index..]]);
        }
        foreach (string key in new[] { "reset", "busMasterOff", "dmaReleased", "resetReads" }) Deny(Change(rows, 4, key, 0));
        Deny(Change(rows, 4, "poolMask", 1)); Deny(Change(rows, 0, "poolMask", 0));
        Deny(Change(rows, 7, "userReserved", 4096)); Deny(Change(rows, 7, "userBacked", 1));
        Deny(Change(rows, 3, "clock", 0)); Deny([..rows, rows[7]]);
        Deny(ExitPendingStages(true));
        foreach (string key in new[] { "ioGeneration", "ioSector", "blockPhase", "fsPhase", "ioActive", "fsIoIndex", "fsIoGeneration",
            "requestPhase", "requestWorker", "slotActive", "slotBytes", "cursor" })
            Deny(Change(rows, 2, key, Field(rows[2], key) + 1));
        Deny(rows, F.CreatePackage("Progress")); byte[] corrupted = F.CreatePackage("ExitPending"); corrupted[^1] ^= 1; Deny(rows, corrupted);
        Deny([..rows[..7], rows[7] + " extra=0"]); Deny([..rows[..7], rows[7].Replace("clock=107", "clock=0107", StringComparison.Ordinal)]);
    }

    private static string[] ExitPendingStages(bool drained)
    {
        string[] fields = ("stage main child ordinal rip sequence owner epoch generation index fd fdGeneration binding bindingGeneration description position " +
            "fileGeneration fileIndex inode fsOwner fsEpoch fsGeneration fsIndex blockOwner blockEpoch blockGeneration sector outputBytes submitted completed " +
            "copied cancelled available consumed resetReads reset busMasterOff dmaReleased poolMask sentinelBytes userReserved userBacked " +
            "ioGeneration ioSector blockPhase fsPhase ioActive fsIoIndex fsIoGeneration requestPhase requestWorker slotActive slotBytes cursor clock").Split(' ');
        var call = F.Calls("ExitPending").Single(c => c.Role == "main.read");
        ulong ordinal = (ulong)F.Calls("ExitPending").TakeWhile(c => c.Role != "main.read").Count() + 1;
        Dictionary<string, ulong> v = fields.ToDictionary(k => k, _ => 0UL, StringComparer.Ordinal);
        v["main"] = F.MainIdentity; v["child"] = F.ChildIdentity; v["ordinal"] = ordinal; v["rip"] = call.ReturnRip; v["sequence"] = 3;
        v["owner"] = 0x100000; v["epoch"] = v["fsEpoch"] = v["blockEpoch"] = 0x4d31365f30323301;
        v["generation"] = 3; v["fd"] = 3; v["fdGeneration"] = v["bindingGeneration"] = v["fileGeneration"] = v["fsGeneration"] = 1;
        v["description"] = 3; v["inode"] = 1234; v["fsOwner"] = 0x110000; v["blockOwner"] = 0x120000;
        v["blockGeneration"] = v["submitted"] = v["available"] = 40; v["completed"] = v["consumed"] = v["copied"] = 39;
        v["sector"] = 1000; v["outputBytes"] = 8192;
        List<string> rows = [];
        for (int i = 0; i < 8; i++)
        {
            v["stage"] = (ulong)i + 1; v["clock"] = (ulong)i + 100; v["sentinelBytes"] = i is 0 or 5 ? 8192UL : 0;
            v["poolMask"] = i < 4 ? 1UL : 0; v["resetReads"] = i < 4 ? 0UL : 1;
            v["reset"] = v["busMasterOff"] = v["dmaReleased"] = i < 4 ? 0UL : 1;
            v["ioGeneration"] = 40; v["ioSector"] = 1000; v["blockPhase"] = i < 4 ? 3UL : 6;
            v["fsPhase"] = i < 5 ? 2UL : 4; v["ioActive"] = i < 5 ? 1UL : 0; v["fsIoGeneration"] = 1;
            v["requestPhase"] = i < 2 ? 2UL : 4; v["requestWorker"] = i < 2 ? F.MainIdentity : 0;
            v["slotActive"] = i < 2 ? 1UL : 0; v["slotBytes"] = i < 2 ? 8192UL : 0;
            if (drained && i >= 2) { v["completed"] = v["consumed"] = 40; v["cancelled"] = 1; }
            rows.Add("M16_025_EXIT_PENDING " + string.Join(' ', fields.Select(f => f + "=" + v[f].ToString(CultureInfo.InvariantCulture))));
        }
        return rows.ToArray();
    }

    [HostTest("disk race leaf joins two exact workers overlapping requests and close reuse without granting auxiliary authority")]
    private static void Positive()
    {
        foreach (string variant in F.OriginalVariants)
        {
            string[] rows = Synthetic(variant), before = (string[])rows.Clone(); byte[] package = F.CreatePackage(variant), saved = (byte[])package.Clone();
            var report = Inspect(rows, package);
            HostCheck.True(report.CheckedCalls && report.CheckedWaitCompletions && report.CheckedLifecycleRecords &&
                report.CheckedCrossWorkerOrdering && !report.CheckedSchedulerReplay && !report.CheckedUserMemoryOrAuxiliaryJoins);
            HostCheck.Equal(variant, report.Variant); HostCheck.Equal(F.Calls(variant).Count + F.Calls(variant, true).Count, report.Calls.Count);
            HostCheck.True(rows.SequenceEqual(before) && package.SequenceEqual(saved));
            HostCheck.True(report.Windows.All(w => w.CallRecordIndex < w.CompletionRecordIndex &&
                rows[w.CallRecordIndex].StartsWith("M16_008_CALL ", StringComparison.Ordinal)));
            var main = report.Windows.Single(w => w.CallRecordIndex == report.MainReadCallIndex);
            HostCheck.True(main.CompletionRecordIndex == report.MainReadCompletionIndex && main.OutputBytes == 8192 && main.Identity == F.MainIdentity);
            if (variant == "CloseReuse")
            {
                HostCheck.True(report.ChildCloseIndex > main.CallRecordIndex && report.ChildCloseIndex < main.CompletionRecordIndex);
                HostCheck.Equal(-9L, main.Value); HostCheck.True(report.Windows.Any(w => w.Source == 2));
            }
            else
            {
                var child = report.Calls.Single(c => c.Spec.Role == "child.duplicate");
                var window = report.Windows.Single(w => w.CallRecordIndex == child.RecordIndex);
                HostCheck.True(main.CallRecordIndex < child.RecordIndex && child.RecordIndex < main.CompletionRecordIndex && main.CompletionRecordIndex < window.CompletionRecordIndex);
                HostCheck.True(report.Windows.Any(w => w.Source == 1));
            }
            HostCheck.True(report.Auxiliary.All(a => a.Text == rows[a.RecordIndex]));
            string retained = report.Auxiliary[0].Text; Array.Fill(rows, "changed"); Array.Clear(package);
            HostCheck.Equal(retained, report.Auxiliary[0].Text); bool denied = false;
            try { ((IList<QemuTranscripts.LinuxDiskRaceWindow>)report.Windows)[0] = default; } catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied && report.Memory == F.Memory);
        }
    }

    [HostTest("disk race leaf rejects mutated arguments clone generations exact continuations and return envelopes")]
    private static void Calls()
    {
        foreach (string variant in F.OriginalVariants)
        {
            string[] rows = Synthetic(variant); var report = Inspect(rows);
            foreach (int at in report.Calls.Where(c => c.Spec.Role is "prep.clone" or "main.read" or "child.pin" or "child.tid" or "main.exit").Select(c => c.RecordIndex))
                foreach (string field in new[] { "identity", "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "rsp", "fs", "gs", "valueBits", "action", "status", "ordinal" })
                    Reject(Change(rows, at, field, Field(rows[at], field) + 1), variant);
            int first = report.Calls[0].RecordIndex;
            foreach (string field in new[] { "signalDepth", "signalFrame", "blockedSignals", "returnRip", "returnRsp", "returnFlags", "returnFs", "returnGs", "returnValueBits" })
                Reject(Change(rows, first, field, Field(rows[first], field) + 1), variant);
            Reject(Change(rows, first, "flags", 0x202), variant); Reject(Change(rows, first, "clock", ulong.MaxValue), variant);
            int clone = report.Calls.Single(c => c.Spec.Role == "prep.clone").RecordIndex;
            Reject(Change(rows, clone, "valueBits", 0), variant);
        }
    }

    [HostTest("disk race waits bind original worker sequences global requests copy ceilings and exact resumes")]
    private static void Waits()
    {
        foreach (string variant in F.OriginalVariants)
        {
            string[] rows = Synthetic(variant); var report = Inspect(rows);
            foreach (var window in report.Windows)
            {
                foreach (string key in new[] { "waitSource", "waitDeadline", "waitSequence" })
                    Reject(Change(rows, window.CallRecordIndex, key, Field(rows[window.CallRecordIndex], key) + 1), variant);
                if (window.Source != 4) continue;
                foreach (string key in new[] { "identity", "sequence", "request", "outputBytes", "completion", "valueBits" })
                    Reject(Change(rows, window.CompletionRecordIndex, key, Field(rows[window.CompletionRecordIndex], key) + 1), variant);
                Reject(rows.Where((_, i) => i != window.CompletionRecordIndex).ToArray(), variant);
                Reject([.. rows[..window.CompletionRecordIndex], rows[window.CompletionRecordIndex], .. rows[window.CompletionRecordIndex..]], variant);
                foreach (string key in new[] { "identity", "valueBits", "status" })
                    Reject(Change(rows, window.CompletionRecordIndex + 1, key, Field(rows[window.CompletionRecordIndex + 1], key) + 1), variant);
            }
            var two = report.Windows.Where(w => w.Source == 4).Take(2).ToArray();
            Reject(Change(rows, two[1].CompletionRecordIndex, "request", two[0].RequestGeneration), variant);
            int first = report.MainReadCompletionIndex;
            string[] swapped = (string[])rows.Clone(); (swapped[first], swapped[first + 1]) = (swapped[first + 1], swapped[first]); Reject(swapped, variant);
        }
    }

    [HostTest("disk race ownership rejects early completion and duplicated cursor completion before original commit")]
    private static void RaceOrdering()
    {
        string[] rows = Synthetic("Progress"); var report = Inspect(rows);
        int firstChild = Index(rows, "M16_012_POLL identity=4294967298 ");
        // Move only the completion+resume pair, keeping all canonical calls
        // and per-worker ordinal sequences unchanged. No child progress exists
        // at this earlier legal supervisor boundary.
        Reject(MovePair(rows, report.MainReadCompletionIndex, firstChild), "Progress", "disk_race_overlap_missing");
        var child = report.Calls.Single(c => c.Spec.Role == "child.duplicate");
        var childWindow = report.Windows.Single(w => w.CallRecordIndex == child.RecordIndex);
        Reject(MovePair(rows, childWindow.CompletionRecordIndex, report.MainReadCompletionIndex), "Progress", "disk_race_cursor_lease");
        string[] close = Synthetic("CloseReuse"); var closed = Inspect(close);
        Reject(Change(close, closed.MainReadCompletionIndex, "valueBits", 8192), "CloseReuse");
        int reopened = closed.Calls.Single(c => c.Spec.Role == "child.reopen").RecordIndex;
        Reject(Change(close, reopened, "a0", F.Path), "CloseReuse");
        var replacement = closed.Calls.Single(c => c.Spec.Role == "main.replacement");
        var replacementWindow = closed.Windows.Single(w => w.CallRecordIndex == replacement.RecordIndex);
        Reject(Change(close, replacementWindow.CompletionRecordIndex, "valueBits", 1), "CloseReuse");
    }

    [HostTest("disk race lifecycle rejects missing extra reordered exits scrubs and inconsistent owner counters")]
    private static void Lifecycle()
    {
        foreach (string variant in F.OriginalVariants)
        {
            string[] rows = Synthetic(variant);
            foreach (string prefix in new[] { "M16_008_SUPERVISOR ", "M16_008_EXIT ", "M16_008_REAP ", "M16_005_WRITE ", "M16_008_OWNER_END ", "M16_009_RESTORE ", "M16_011_CLOSE " })
            {
                int at = Index(rows, prefix); Reject(rows.Where((_, i) => i != at).ToArray(), variant);
                Reject([.. rows[..at], rows[at], .. rows[at..]], variant);
            }
            int owner = Index(rows, "M16_008_OWNER_END ");
            foreach (string key in new[] { "reaped", "transfers", "irqs", "exit", "userReserved", "userBacked" }) Reject(Change(rows, owner, key, Field(rows[owner], key) + 1), variant);
            int reap = Index(rows, "M16_008_REAP "); Reject(Change(rows, reap, "next", Field(rows[reap], "old")), variant);
            Reject(Replace(rows, reap, rows[reap].Replace("stack=zero", "stack=live", StringComparison.Ordinal)), variant);
            int supervisor = Index(rows, "M16_008_SUPERVISOR ");
            foreach (string key in new[] { "identity", "action", "transfers", "active" }) Reject(Change(rows, supervisor, key, Field(rows[supervisor], key) + 1), variant);
            Reject(Change(rows, supervisor, "ready", 3), variant);
            Reject([.. rows, "TEST_PASS unexpected"], variant); Reject([.. rows[..^1]], variant);
        }
    }

    [HostTest("disk race bounded auxiliary values remain indexed unqualified and reject malformed or unknown rows")]
    private static void Auxiliary()
    {
        string[] rows = Synthetic("Progress", auxiliary: true); var report = Inspect(rows);
        HostCheck.True(report.Auxiliary.Any(a => a.Text.StartsWith("M16_023_IO ", StringComparison.Ordinal)) && !report.CheckedUserMemoryOrAuxiliaryJoins);
        int io = Index(rows, "M16_023_IO "); Reject(Replace(rows, io, rows[io] + " extra=1"), "Progress");
        Reject(Replace(rows, io, rows[io].Replace("generation=1", "generation=18446744073709551616", StringComparison.Ordinal)), "Progress");
        Reject(Replace(rows, io, "M16_009_IRQ identity=4294967297 epoch=1 nanos=1 rip=4194816 rsp=1 flags=514 fs=0 gs=0 valueBits=0"), "Progress");
        Reject(Replace(rows, io, "UNKNOWN_DISK_RECORD"), "Progress");
        int bound = Index(rows, "M16_008_BOUND "); Reject(Replace(rows, bound, rows[bound].Replace("timer=idle-only", "timer=user-irq-and-idle", StringComparison.Ordinal)), "Progress");
        Reject(Replace(rows, 0, new string('x', 1537)), "Progress"); Reject(Replace(rows, 0, "TEST_START m16-009\n"), "Progress");
        bool refused = false; try { QemuTranscripts.InspectLinuxDiskRaceLeaf(new Oversized(), F.CreatePackage("Progress"), 0x200000, 0x100000); } catch (ToolFailure) { refused = true; }
        HostCheck.True(refused);
        refused = false; try { Inspect(rows, F.CreatePackage("CloseReuse")); } catch (ToolFailure) { refused = true; } HostCheck.True(refused);
    }

    [HostTest("disk signal race leaves distinguish interrupted read and zero-I/O EOF winner through real handler return envelopes")]
    private static void SignalLifetimes()
    {
        foreach (string variant in new[] { "InterruptWins", "CompletionWins" })
        {
            var rows = SyntheticLifecycle(variant); var report = Inspect(rows, F.CreatePackage(variant));
            var read = report.Windows.Single(w => w.CallRecordIndex == report.MainReadCallIndex);
            HostCheck.True(report.ChildSignalIndex > read.CallRecordIndex && report.ChildSignalIndex < read.CompletionRecordIndex &&
                read.Completion == (variant == "InterruptWins" ? 5U : 1U) && read.Value == (variant == "InterruptWins" ? -4 : 0));
            HostCheck.True(report.CheckedSignalReturnEnvelopes && !report.CheckedPostDeathSentinelOrOrphanDrain &&
                !report.CompletionWinnerProvesBackendDataRace && report.Abandoned.Count == 0);
            HostCheck.Equal(F.Calls(variant).Count + F.Calls(variant, true).Count + F.HandlerCalls(variant).Count, report.Calls.Count);
            HostCheck.True(report.Calls.Where(c => c.Spec.Role.StartsWith("handler.", StringComparison.Ordinal)).Select(c => c.Spec).SequenceEqual(F.HandlerCalls(variant)));
            HostCheck.True(report.Calls.Single(c => c.Spec.Role == "main.drain-probe").RecordIndex > report.MainReadCompletionIndex);
        }
    }

    [HostTest("disk signal races reject wrong winner target prefix pending result and forged sigreturn frame mask or restoration")]
    private static void SignalMutations()
    {
        foreach (string variant in new[] { "InterruptWins", "CompletionWins" })
        {
            string[] rows = SyntheticLifecycle(variant); var report = Inspect(rows, F.CreatePackage(variant));
            int complete = report.MainReadCompletionIndex, signal = report.ChildSignalIndex;
            foreach (string field in new[] { "completion", "sequence", "request", "outputBytes", "valueBits", "identity" })
                Reject(Change(rows, complete, field, Field(rows[complete], field) + 1), variant);
            foreach (string field in new[] { "a0", "a1", "a2" }) Reject(Change(rows, signal, field, Field(rows[signal], field) + 1), variant);
            foreach (var call in report.Calls.Where(c => c.Spec.Role.StartsWith("handler.", StringComparison.Ordinal)))
                foreach (string field in new[] { "rsp", "signalDepth", "signalFrame", "blockedSignals", "returnRip", "returnRsp", "returnValueBits", "action" })
                    Reject(Change(rows, call.RecordIndex, field, Field(rows[call.RecordIndex], field) + 1), variant);
            var restored = report.Calls.Single(c => c.Spec.Role == "handler.return");
            Reject(rows.Where((_, i) => i != restored.RecordIndex).ToArray(), variant);
            Reject(Replace(rows, signal, rows[signal].Replace("nr=234", "nr=200", StringComparison.Ordinal)), variant);
            Reject(MovePair(rows, complete, signal), variant);
        }
    }

    [HostTest("disk pending process exit has one abandoned wait and no invented completion return or post-death sentinel authority")]
    private static void PendingExit()
    {
        const string variant = "ExitPending"; var rows = SyntheticLifecycle(variant); var report = Inspect(rows, F.CreatePackage(variant));
        HostCheck.True(report.MainReadCompletionIndex == -1 && report.Abandoned.Count == 1 && !report.CheckedPostDeathSentinelOrOrphanDrain &&
            report.Windows.All(w => w.CallRecordIndex != report.MainReadCallIndex));
        var pending = report.Abandoned[0]; HostCheck.True(pending.CallRecordIndex == report.MainReadCallIndex && pending.OutputBytes == 8192 &&
            pending.Identity == F.MainIdentity && pending.GroupExitRecordIndex > pending.CallRecordIndex);
        HostCheck.True(report.Calls.Single(c => c.Spec.Role == "child.group-exit").RecordIndex == pending.GroupExitRecordIndex);
        foreach (string field in new[] { "a0", "identity", "nr", "action", "valueBits" })
            Reject(Change(rows, pending.GroupExitRecordIndex, field, Field(rows[pending.GroupExitRecordIndex], field) + 1), variant);
        string completion = $"M16_025_COMPLETE identity={F.MainIdentity} sequence={pending.Sequence} request={pending.RequestGeneration} outputBytes=8192 completion=1 valueBits=8192 clock=999";
        Reject([.. rows[..pending.GroupExitRecordIndex], completion, $"M16_008_RESUME identity={F.MainIdentity} valueBits=8192 status=1", .. rows[pending.GroupExitRecordIndex..]], variant);
        int reap = Array.FindIndex(rows, r => r.StartsWith("M16_008_REAP old=" + F.MainIdentity, StringComparison.Ordinal));
        Reject(rows.Where((_, i) => i != reap).ToArray(), variant);
        int close = Array.FindIndex(rows, r => r == "M16_011_CLOSE fd=7 valueBits=0");
        Reject(rows.Where((_, i) => i != close).ToArray(), variant);
        bool immutable = false;
        try { ((IList<QemuTranscripts.LinuxDiskRaceAbandoned>)report.Abandoned)[0] = default; } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    private static unsafe string[] SyntheticLifecycle(string variant)
    {
        byte[] package = F.CreatePackage(variant); var view = LinuxElfPackage.Validate(package);
        byte* memory = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096); byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        ulong stack;
        try { fixed (byte* p = package) { HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes,
            p + view.PayloadOffset, view.PayloadBytes, seed, 32, memory, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var initial)); stack = initial.StackPointer; } }
        finally { NativeMemory.AlignedFree(memory); }
        var plans = new[] { F.Calls(variant), F.Calls(variant, true) }; var handler = F.HandlerCalls(variant);
        int[] ordinal = [0, 0], next = [0, 0]; ulong[] sequences = [0, 0]; Pending?[] pending = new Pending?[2];
        int active = 1, transfers = 0; ulong clock = 100, request = 0, epochs = 0;
        ulong fp = (stack - 128 - LinuxSignalServices.FpBytes) & ~63UL, frame = ((fp - LinuxSignalServices.FrameBytes) & ~15UL) - 8;
        List<string> rows = Synthetic("Progress").TakeWhile(r => !r.StartsWith("M16_012_POLL ", StringComparison.Ordinal)).ToList();
        ulong Identity(int w) => w == 0 ? F.MainIdentity : F.ChildIdentity;
        ulong Stack(int w) => w == 0 ? stack : F.ChildStackTop;
        void Call(int w, F.CallSpec e, bool handlerCall = false)
        {
            bool block = e.Wait == F.WaitPolicy.Disk;
            uint source = block ? 4U : 0;
            if (block) pending[w] = new(e, ++sequences[w], ++request, source);
            ulong value = unchecked((ulong)(block ? 0 : e.Wait == F.WaitPolicy.Join ? -11 : e.Result));
            bool restored = handlerCall && e.Number == 15;
            uint action = block ? 3U : restored ? 5U : e.Number == 231 ? 2U : e.Number == 60 ? 4U : 1U;
            ulong inputStack = handlerCall ? frame + (restored ? 8UL : 0UL) : Stack(w);
            rows.Add($"M16_012_POLL identity={Identity(w)} clock={clock++} ready=0 idle=0");
            if (e.Role is "main.witness" or "child.witness") rows.Add($"M16_005_WRITE fd=1 bytes={F.Witness(variant).Length} data={F.Witness(variant)}");
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={Identity(w)} nr={e.Number} a0={e.A0} a1={e.A1} a2={e.A2} a3={e.A3} a4={e.A4} a5={e.A5} rip={e.ReturnRip} rsp={inputStack} flags=70 fs={(w == 0 ? 0UL : F.ChildTls)} gs=0 clock={clock - 1} valueBits={value} action={action} status={(block ? 9 : action is 2 or 4 ? 2 : 1)} ordinal={++ordinal[w]} waitSource={source} waitDeadline=0 waitSequence={(block ? sequences[w] : 0)} signalDepth={(handlerCall && !restored ? 1 : 0)} signalFrame={(handlerCall && !restored ? frame : 0)} blockedSignals={(handlerCall && !restored ? 512 : 0)} returnRip={(restored ? plans[0].Single(c => c.Role == "main.read").ReturnRip : e.ReturnRip)} returnRsp={(restored ? stack : inputStack)} returnFlags=70 returnFs={(w == 0 ? 0UL : F.ChildTls)} returnGs=0 returnValueBits={value}"));
            if (e.Role == "prep.clone") active = 2;
        }
        void Planned(int w)
        { var e = plans[w][next[w]++]; Call(w, e); }
        void Supervisor(int w, uint action, uint ready)
            => rows.Add($"M16_008_SUPERVISOR identity={Identity(w)} action={action} transfers={transfers += 2} ready={ready} active={active}");
        void Drain(int w) => rows.Add($"M16_009_DRAIN identity={Identity(w)} epoch={++epochs} nanos={clock++}");
        void Complete(int w)
        {
            var p = pending[w]!; uint winner = variant == "InterruptWins" && p.Spec.Role == "main.read" ? 5U : 1U;
            rows.Add($"M16_025_COMPLETE identity={Identity(w)} sequence={p.Sequence} request={p.Request} outputBytes={p.Spec.OutputBytes} completion={winner} valueBits={unchecked((ulong)p.Spec.Result)} clock={clock++}");
            rows.Add($"M16_008_RESUME identity={Identity(w)} valueBits={unchecked((ulong)p.Spec.Result)} status=1"); pending[w] = null;
        }
        void Reap(int w)
        { rows.Add($"M16_008_REAP old={Identity(w)} next={Identity(w) + (1UL << 32)} context=zero stack=zero slot=zero"); active--; }
        while (plans[0][next[0]].Role != "main.read")
        {
            Planned(0); if (pending[0] is not null) { Drain(0); Supervisor(0, 3, 0); Complete(0); }
        }
        Planned(0); Drain(0); Supervisor(0, 3, 1);
        if (variant == "ExitPending")
        {
            Planned(1); Planned(1); Drain(1); rows.Add($"M16_008_EXIT identity={F.ChildIdentity} clearTidResult=0"); Supervisor(1, 2, 1); Reap(1);
            pending[0] = null; rows.Add($"M16_008_EXIT identity={F.MainIdentity} clearTidResult=0"); Supervisor(0, 4, 0); Reap(0);
        }
        else
        {
            Planned(1); Planned(1); Drain(1); rows.Add($"M16_008_EXIT identity={F.ChildIdentity} clearTidResult=0"); Supervisor(1, 4, 1); Reap(1);
            Complete(0); foreach (var e in handler) Call(0, e, handlerCall: true);
            while (next[0] < plans[0].Count)
            {
                var e = plans[0][next[0]]; Planned(0);
                if (pending[0] is not null) { Drain(0); Supervisor(0, 3, 0); Complete(0); }
                if (e.Number == 231) { Drain(0); rows.Add($"M16_008_EXIT identity={F.MainIdentity} clearTidResult=0"); Supervisor(0, 2, 0); Reap(0); }
            }
        }
        for (uint fd = 0; fd < (variant == "ExitPending" ? 8U : 3U); fd++) rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        rows.Add($"M16_008_BUDGET_END nanos={clock}");
        rows.Add($"M16_008_OWNER_END reaped=2 transfers={transfers} irqs={epochs} exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        HostCheck.True(next[0] == plans[0].Count && next[1] == plans[1].Count);
        return rows.ToArray();
    }

    internal static unsafe string[] Synthetic(string variant, bool auxiliary = false)
    {
        byte[] package = F.CreatePackage(variant); var view = LinuxElfPackage.Validate(package);
        byte* memory = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096); byte* seed = stackalloc byte[32]; new Span<byte>(seed, 32).Fill(1);
        ulong stack;
        try { fixed (byte* p = package) { HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes,
            p + view.PayloadOffset, view.PayloadBytes, seed, 32, memory, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var initial)); stack = initial.StackPointer; } }
        finally { NativeMemory.AlignedFree(memory); }
        var plans = new[] { F.Calls(variant), F.Calls(variant, true) };
        var decoded = new[] { LinuxDiskRaceProofFixtureTests.Calls(variant), LinuxDiskRaceProofFixtureTests.Calls(variant, true) };
        int[] ordinal = new int[2]; ulong[] sequences = new ulong[2]; Pending?[] pending = new Pending?[2];
        int active = 1, transfers = 0; ulong clock = 100; ulong request = 0;
        List<string> rows = ["TEST_START m16-009",
            "M16_006_POLICY physical=536870912 available=268435456 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live",
            "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=3145728 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "TEST_START m16.023", "TEST_START m16.024", "TEST_START m16.025",
            "M16_023_BOUND owner=8388608 epoch=1 arena=9437184 pages=90 bar=4026531840 capacity=131072 bootstrapCompleted=37 bootstrapCopied=37 clock=1 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained",
            $"M16_008_BOUND root=1048576 pool=4194304 workers=2 code={LinuxThreadProgram.Bytes} metadata={LinuxThreadProgram.MetadataPages * 4096} userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack",
            "M16_008_BUDGET started=1 epochs=8192 steps=16384 nanos=300000000000"];
        ulong Identity(int w) => w == 0 ? F.MainIdentity : F.ChildIdentity;
        ulong Stack(int w) => w == 0 ? stack : F.ChildStackTop;
        void Poll(int w) => rows.Add($"M16_012_POLL identity={Identity(w)} clock={clock++} ready=0 idle=0");
        void Call(int w, string role, bool block = false)
        {
            int n = ordinal[w]; var e = plans[w][n]; var code = decoded[w][n]; HostCheck.Equal(role, e.Role);
            HostCheck.True(e.ReturnRip == code.Rip && e.Number == code.Number);
            block |= e.Wait == F.WaitPolicy.Disk;
            uint source = block ? e.Wait == F.WaitPolicy.Disk ? 4U : e.Wait == F.WaitPolicy.PipeRead ? 2U : 1U : 0;
            if (block) pending[w] = new(e, ++sequences[w], source == 4 ? ++request : 0, source);
            ulong value = unchecked((ulong)(block ? 0 : e.Wait == F.WaitPolicy.Join ? -11 : e.Result));
            uint action = block ? 3U : e.Number == 231 ? 2U : e.Number == 60 ? 4U : 1U;
            Poll(w); if (role == "main.witness") rows.Add($"M16_005_WRITE fd=1 bytes={F.Witness(variant).Length} data={F.Witness(variant)}");
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={Identity(w)} nr={code.Number} a0={code.A0} a1={code.A1} a2={code.A2} a3={code.A3} a4={code.A4} a5={code.A5} rip={code.Rip} rsp={Stack(w)} flags=70 fs={(w == 0 ? 0UL : F.ChildTls)} gs=0 clock={clock - 1} valueBits={value} action={action} status={(block ? 9 : action is 2 or 4 ? 2 : 1)} ordinal={++ordinal[w]} waitSource={source} waitDeadline=0 waitSequence={(block ? sequences[w] : 0)} signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={code.Rip} returnRsp={Stack(w)} returnFlags=70 returnFs={(w == 0 ? 0UL : F.ChildTls)} returnGs=0 returnValueBits={value}"));
            if (role == "prep.clone") active = 2;
        }
        void Supervisor(int w, uint action, uint ready = 0) => rows.Add($"M16_008_SUPERVISOR identity={Identity(w)} action={action} transfers={transfers += 2} ready={ready} active={active}");
        void Complete(int w)
        {
            var p = pending[w]!;
            if (p.Source == 4) rows.Add(FormattableString.Invariant($"M16_025_COMPLETE identity={Identity(w)} sequence={p.Sequence} request={p.Request} outputBytes={p.Spec.OutputBytes} completion=1 valueBits={unchecked((ulong)p.Spec.Result)} clock={clock++}"));
            rows.Add(FormattableString.Invariant($"M16_008_RESUME identity={Identity(w)} valueBits={unchecked((ulong)p.Spec.Result)} status=1")); pending[w] = null;
        }
        void Disk(int w, string role) { Call(w, role); Supervisor(w, 3); Complete(w); }
        void Exit(int w)
        {
            Call(w, w == 0 ? "main.exit" : "child.exit"); rows.Add($"M16_008_EXIT identity={Identity(w)} clearTidResult=0");
            Supervisor(w, w == 0 ? 2U : 4U, w == 0 ? 0U : 1U);
            rows.Add($"M16_008_REAP old={Identity(w)} next={Identity(w) + (1UL << 32)} context=zero stack=zero slot=zero"); active--;
        }
        Disk(0, "prep.original"); Disk(0, "prep.independent"); Call(0, "prep.pipe"); Call(0, "prep.duplicate"); Call(0, "prep.clone");
        Call(0, "main.read"); Supervisor(0, 3, 1);
        if (auxiliary) rows.Add("M16_023_IO kind=0 owner=8388608 epoch=1 generation=1 sector=4096 sequence=1 bytes=512 available=1 consumed=0 clock=100");
        foreach (string role in new[] { "child.tid", "child.pin", "child.disjoint-readonly", "child.disjoint-writable" }) Call(1, role);
        if (variant == "Progress")
        {
            Call(1, "child.notify"); Call(1, "child.duplicate"); Supervisor(1, 3);
            Complete(0); Call(0, "main.pipe"); Call(0, "main.pread"); Supervisor(0, 3);
            Complete(1); Call(1, "child.pread"); Supervisor(1, 3);
            Complete(0); Call(0, "main.join", block: true); Supervisor(0, 3);
            Complete(1); Disk(1, "child.independent-cursor"); Exit(1); Complete(0);
        }
        else
        {
            Call(1, "child.close"); Call(1, "child.reopen"); Supervisor(1, 3);
            Complete(0); Call(0, "main.pipe", block: true); Supervisor(0, 3);
            Complete(1); Disk(1, "child.empty"); Call(1, "child.notify"); Exit(1);
            Complete(0); Call(0, "main.join"); Disk(0, "main.replacement"); Disk(0, "main.duplicate"); Call(0, "main.replacement-stat");
        }
        Disk(0, "main.cursor"); Disk(0, "main.independent-cursor");
        foreach (uint fd in new uint[] { 3, 4, 7, 5, 6 }) Call(0, "main.close" + fd);
        Call(0, "main.witness"); Exit(0);
        for (uint fd = 0; fd < 3; fd++) rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        rows.Add($"M16_008_BUDGET_END nanos={clock}");
        rows.Add($"M16_008_OWNER_END reaped=2 transfers={transfers} irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        HostCheck.True(ordinal[0] == plans[0].Count && ordinal[1] == plans[1].Count);
        return rows.ToArray();
    }
    private sealed record Pending(F.CallSpec Spec, ulong Sequence, ulong Request, uint Source);
    private static QemuTranscripts.LinuxDiskRaceLeafReport Inspect(string[] rows, byte[]? package = null) =>
        QemuTranscripts.InspectLinuxDiskRaceLeaf(rows, package ?? F.CreatePackage(rows.Any(r => r.Contains("data=M16_DISK_CLOSE_REUSE_OK!", StringComparison.Ordinal)) ? "CloseReuse" : "Progress"), 0x200000, 0x100000);
    private static int Index(string[] rows, string prefix) => Array.FindIndex(rows, r => r.StartsWith(prefix, StringComparison.Ordinal));
    private static ulong Field(string row, string key) => ulong.Parse(row.Split(' ').Single(f => f.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..], CultureInfo.InvariantCulture);
    private static string[] Change(string[] rows, int at, string key, ulong value) => Replace(rows, at,
        string.Join(' ', rows[at].Split(' ').Select(f => f.StartsWith(key + "=", StringComparison.Ordinal) ? key + "=" + value.ToString(CultureInfo.InvariantCulture) : f)));
    private static string[] Replace(string[] rows, int at, string row) { string[] copy = (string[])rows.Clone(); copy[at] = row; return copy; }
    private static string[] MovePair(string[] rows, int from, int to)
    {
        var copy = rows.ToList(); var pair = copy.GetRange(from, 2); copy.RemoveRange(from, 2); copy.InsertRange(to > from ? to - 2 : to, pair); return copy.ToArray();
    }
    private static void Reject(string[] rows, string variant, string? reason = null)
    {
        bool rejected = false;
        try { Inspect(rows, F.CreatePackage(variant)); }
        catch (ToolFailure error) { rejected = reason is null || error.Message.StartsWith(reason, StringComparison.Ordinal); }
        HostCheck.True(rejected, reason ?? "expected ToolFailure");
    }
    private sealed class Oversized : IReadOnlyList<string>
    {
        public int Count => 32769;
        public string this[int index] => throw new InvalidOperationException("must reject bounds first");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("must reject bounds first");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
