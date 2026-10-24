using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public readonly record struct LinuxDiskExitPendingRecord(int RecordIndex, IReadOnlyDictionary<string, ulong> Values);

    /// <summary>Immutable copied stage values only. ResetAbortedRequest means
    /// the selected counters report reset retirement rather than a used-ring
    /// completion. No native stop, user-memory observation, scheduler, original
    /// artifact, image or guest authority follows from inspecting these values.</summary>
    public sealed class LinuxDiskExitPendingValues
    {
        public IReadOnlyList<LinuxDiskExitPendingRecord> Records { get; }
        public bool ResetAbortedRequest { get; }
        public bool CheckedOriginalExecution => false;
        public bool CheckedNativeStopOrUserMemory => false;
        internal LinuxDiskExitPendingValues(LinuxDiskExitPendingRecord[] records, bool resetAborted)
        { Records = Array.AsReadOnly(records); ResetAbortedRequest = resetAborted; }
    }

    private static readonly string[] DiskExitPendingFields =
    [ "stage", "main", "child", "ordinal", "rip", "sequence", "owner", "epoch", "generation", "index", "fd", "fdGeneration",
      "binding", "bindingGeneration", "description", "position", "fileGeneration", "fileIndex", "inode", "fsOwner", "fsEpoch",
      "fsGeneration", "fsIndex", "blockOwner", "blockEpoch", "blockGeneration", "sector", "outputBytes", "submitted", "completed",
      "copied", "cancelled", "available", "consumed", "resetReads", "reset", "busMasterOff", "dmaReleased", "poolMask",
      "sentinelBytes", "userReserved", "userBacked", "ioGeneration", "ioSector", "blockPhase", "fsPhase", "ioActive", "fsIoIndex",
      "fsIoGeneration", "requestPhase", "requestWorker", "slotActive", "slotBytes", "cursor", "clock" ];

    /// <summary>Borrow at most32768 printable rows of at most1536 characters
    /// and the entire canonical ExitPending package. Require eight ordered,
    /// generation-bound scalar stages, unchanged copy/submit counts and actual
    /// reset-aborted terminal counters. Unknown fields, altered identities,
    /// missing/duplicate stages or divergent counters throw ToolFailure. Returns
    /// owned immutable values; separate full protocol/native/device/image and
    /// original-runner joins remain mandatory before any qualification.</summary>
    public static LinuxDiskExitPendingValues InspectLinuxDiskExitPendingValues(IReadOnlyList<string> supplied, byte[] package)
    {
        Need(supplied is { Count: > 0 and <= 32768 } && package is not null &&
            Race.PackageVariant(package) == "ExitPending", "disk_exit_pending_input");
        List<LinuxDiskExitPendingRecord> records = [];
        for (int i = 0; i < supplied!.Count; i++)
        {
            string row = supplied[i];
            Need(row is { Length: > 0 and <= 1536 } && row.All(c => c is >= ' ' and <= '~'), "disk_exit_pending_row");
            if (!row.StartsWith("M16_025_EXIT_PENDING ", StringComparison.Ordinal)) continue;
            Need(records.Count < 8, "disk_exit_pending_stage_count");
            var fields = ReadRecord(row, "M16_025_EXIT_PENDING", DiskExitPendingFields.Select(f => f + ":dec").ToArray());
            Need(fields["stage"] == (ulong)records.Count + 1, "disk_exit_pending_stage_order");
            records.Add(new(i, new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(fields)));
        }
        Need(records.Count == 8, "disk_exit_pending_stage_count");
        var first = records[0].Values;
        var call = Race.Calls("ExitPending").Single(c => c.Role == "main.read");
        ulong ordinal = (ulong)Race.Calls("ExitPending").TakeWhile(c => c.Role != "main.read").Count() + 1;
        Need(first["main"] == Race.MainIdentity && first["child"] == Race.ChildIdentity && first["ordinal"] == ordinal &&
            first["rip"] == call.ReturnRip && first["sequence"] > 0 && first["generation"] > 0 &&
            first["index"] < LinuxDescriptorServices.DiskSlots && first["fd"] == Race.OriginalFd && first["fdGeneration"] > 0 &&
            first["binding"] < LinuxDescriptorServices.DiskSlots && first["bindingGeneration"] > 0 && first["description"] < 16 &&
            first["position"] == 0 && first["fileGeneration"] > 0 && first["fileIndex"] < 8 && first["inode"] > 0 &&
            first["fsGeneration"] > 0 && first["fsIndex"] < 8 && first["epoch"] != 0 && first["fsEpoch"] == first["epoch"] &&
            first["blockEpoch"] == first["epoch"] && first["blockGeneration"] == first["submitted"] &&
            first["outputBytes"] == Race.ParentOutputBytes && first["submitted"] > 0 &&
            first["completed"] < ulong.MaxValue && first["submitted"] == first["completed"] + 1 &&
            first["copied"] <= first["completed"] && first["cancelled"] <= first["completed"], "disk_exit_pending_identity");
        foreach (string owner in new[] { "owner", "fsOwner", "blockOwner" })
            Need(first[owner] is >= 4096 and <= 0xfffffff8 && first[owner] % 8 == 0, "disk_exit_pending_owner");
        Need(first["owner"] != first["fsOwner"] && first["owner"] != first["blockOwner"] &&
            first["fsOwner"] != first["blockOwner"], "disk_exit_pending_owner");
        ulong clock = 0;
        for (int i = 0; i < records.Count; i++)
        {
            var v = records[i].Values;
            foreach (string field in DiskExitPendingFields.Skip(1).Take(28))
                Need(v[field] == first[field], "disk_exit_pending_binding");
            Need(v["copied"] == first["copied"] && v["completed"] == first["completed"] && v["cancelled"] == first["cancelled"] &&
                v["available"] == unchecked((ushort)v["submitted"]) && v["consumed"] == unchecked((ushort)v["completed"]) &&
                v["reset"] <= 1 && v["busMasterOff"] <= 1 && v["dmaReleased"] <= 1 && v["resetReads"] <= 2048 &&
                v["clock"] >= clock && v["clock"] <= long.MaxValue &&
                v["sentinelBytes"] == (i is 0 or 5 ? Race.ParentOutputBytes : 0), "disk_exit_pending_counters");
            Need(v["ioGeneration"] == first["blockGeneration"] && v["ioSector"] == first["sector"] &&
                v["blockPhase"] == (i >= 4 ? 6UL : 3UL) && v["fsPhase"] == (i >= 5 ? 4UL : 2UL) &&
                v["ioActive"] == (i >= 5 ? 0UL : 1UL) && v["fsIoIndex"] == first["fsIndex"] &&
                v["fsIoGeneration"] == first["fsGeneration"] && v["requestPhase"] == (i < 2 ? 2UL : 4UL) &&
                v["requestWorker"] == (i < 2 ? first["main"] : 0) && v["slotActive"] == (i < 2 ? 1UL : 0UL) &&
                v["slotBytes"] == (i < 2 ? first["outputBytes"] : 0) && v["cursor"] == first["position"], "disk_exit_pending_cancel_boundary");
            if (i >= 4)
                Need(v["completed"] == records[3].Values["completed"] && v["cancelled"] == records[3].Values["cancelled"] &&
                    v["reset"] == 1 && v["busMasterOff"] == 1 && v["dmaReleased"] == 1 && v["poolMask"] == 0 &&
                    v["resetReads"] > records[3].Values["resetReads"], "disk_exit_pending_stop");
            else Need(v["poolMask"] == 1, "disk_exit_pending_dma_owner");
            if (i >= 5) Need(v["resetReads"] == records[4].Values["resetReads"], "disk_exit_pending_stop");
            clock = v["clock"];
        }
        Need(records[7].Values["userReserved"] == 0 && records[7].Values["userBacked"] == 0, "disk_exit_pending_memory_restored");
        return new(records.ToArray(), records[7].Values["completed"] == first["completed"]);
    }

    public readonly record struct LinuxDiskRaceCall(int RecordIndex, ulong Identity, ulong Ordinal, Race.CallSpec Spec);
    public readonly record struct LinuxDiskRaceWindow(int CallRecordIndex, int CompletionRecordIndex,
        ulong Identity, ulong Ordinal, ulong Sequence, ulong RequestGeneration, uint Source, long Value, uint OutputBytes, uint Completion = 1);
    public readonly record struct LinuxDiskRaceAbandoned(int CallRecordIndex, int GroupExitRecordIndex,
        ulong Identity, ulong Ordinal, ulong Sequence, ulong RequestGeneration, uint OutputBytes);

    /// <summary>Copied scalar checks of the exact two-worker mechanical leaf.
    /// Pending windows and all known auxiliary rows retain original indexes for
    /// parent/sector/timing joins. This inspector checks reported transaction
    /// ordering, not an instruction interpreter, scheduler replay, sentinel/data
    /// memory inspection, original firmware, native device or whole-guest proof.</summary>
    public sealed class LinuxDiskRaceLeafReport
    {
        public string Variant { get; }
        public int Transfers { get; }
        public ulong Epochs { get; }
        public bool CheckedCalls => true;
        public bool CheckedWaitCompletions => true;
        public bool CheckedLifecycleRecords => true;
        public bool CheckedCrossWorkerOrdering => true;
        public bool CheckedSchedulerReplay => false;
        public bool CheckedUserMemoryOrAuxiliaryJoins => false;
        public bool CheckedSignalReturnEnvelopes => Variant is "InterruptWins" or "CompletionWins";
        public bool CheckedPostDeathSentinelOrOrphanDrain => false;
        public bool CompletionWinnerProvesBackendDataRace => false;
        public IReadOnlyList<LinuxDiskRaceCall> Calls { get; }
        public IReadOnlyList<LinuxDiskRaceWindow> Windows { get; }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary { get; }
        public IReadOnlyList<LinuxDiskRaceAbandoned> Abandoned { get; }
        public Race.MemoryLayout Memory => Race.Memory;
        public int MainReadCallIndex { get; }
        public int MainReadCompletionIndex { get; }
        public int ChildCloseIndex { get; }
        public int ChildSignalIndex { get; }
        internal LinuxDiskRaceLeafReport(string variant, int transfers, ulong epochs, List<LinuxDiskRaceCall> calls,
            List<LinuxDiskRaceWindow> windows, List<LinuxDiskAuxiliary> auxiliary, int read, int complete, int close,
            List<LinuxDiskRaceAbandoned> abandoned, int signal)
        {
            Variant = variant; Transfers = transfers; Epochs = epochs;
            Calls = Array.AsReadOnly(calls.ToArray()); Windows = Array.AsReadOnly(windows.ToArray());
            Auxiliary = Array.AsReadOnly(auxiliary.ToArray()); MainReadCallIndex = read;
            MainReadCompletionIndex = complete; ChildCloseIndex = close;
            Abandoned = Array.AsReadOnly(abandoned.ToArray()); ChildSignalIndex = signal;
        }
    }

    /// <summary>Synchronously borrow a complete m16-009 leaf and canonical race
    /// package. Inputs are not retained or mutated. At most32768 printable ASCII
    /// rows/1536 chars each are admitted. Unknown, missing, duplicated or invalid
    /// values throw ToolFailure. Root arguments are value joins only; authority
    /// and every indexed auxiliary observation require independent validation.</summary>
    public static LinuxDiskRaceLeafReport InspectLinuxDiskRaceLeaf(IReadOnlyList<string> supplied,
        byte[] package, ulong userRoot, ulong kernelRoot)
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
        string variant = Race.PackageVariant(package!); bool close = variant == "CloseReuse";
        bool signalRace = variant is "InterruptWins" or "CompletionWins", exitPending = variant == "ExitPending";
        var manifest = LinuxElfPackage.Validate(package!).Manifest;
        var observations = ReadLinuxDiskCallObservations(input);
        var waits = observations.Waits.ToDictionary(v => v.RecordIndex);
        var returns = observations.Returns.ToDictionary(v => v.RecordIndex);
        string[] rows = ValidateLinuxDiskCallOrdinals(input);
        RaceWorker[] workers = [new(Race.Calls(variant), Race.MainIdentity, DiskFileStack(package!), 0),
            new(Race.Calls(variant, child: true), Race.ChildIdentity, Race.ChildStackTop, Race.ChildTls)];
        workers[0].Created = true;
        Need(userRoot is >= 4096 and <= 0xfffff000 && userRoot % 4096 == 0 && kernelRoot is >= 4096 and <= 0xfffff000 &&
            kernelRoot % 4096 == 0 && userRoot != kernelRoot, "disk_race_roots");
        int at = 0, selected = -1, awaiting = -1, reaped = 0, transfers = 0;
        uint action = 0; bool exitMarker = false, witness = false;
        ulong clock = 0, epoch = 0, request = 0;
        int mainRead = -1, mainComplete = -1, childClose = -1, childDuplicate = -1, notify = -1;
        int childSignal = -1; bool groupExit = false;
        var handler = Race.HandlerCalls(variant); int handlerCalls = 0; bool inHandler = false, handlerReturned = false;
        ulong signalFrame = 0, interruptedFlags = 0;
        HashSet<string> progress = [];
        List<LinuxDiskRaceCall> calls = []; List<LinuxDiskRaceWindow> windows = []; List<LinuxDiskAuxiliary> auxiliary = [];
        List<LinuxDiskRaceAbandoned> abandoned = [];
        string Take() { Need(at < rows.Length, "disk_race_truncated"); return rows[at++]; }
        void Exact(string value) => Need(Take() == value, "disk_race_order_" + value);
        Dictionary<string, ulong> Row(string name, params string[] fields) => ReadRecord(Take(), name, fields);
        void Clock(ulong value) { Need(value >= clock && value <= long.MaxValue, "disk_race_clock"); clock = value; }
        void Aux(int index) => auxiliary.Add(new(index, new string(input[index].AsSpan())));
        int Worker(ulong identity)
        { Need(identity is Race.MainIdentity or Race.ChildIdentity, "disk_race_identity"); return identity == Race.MainIdentity ? 0 : 1; }
        void Select(int index)
        {
            Need(awaiting < 0 && workers[index].Created && !workers[index].Exited && !workers[index].Reaped &&
                (selected < 0 || selected == index), "disk_race_selection"); selected = index;
        }
        bool MainPending() => workers[0].Pending is { } p && p.Spec.Role == "main.read";
        void Finish(int w, int index, long value, uint output, uint completion = 1)
        {
            var pending = workers[w].Pending!;
            if (pending.Spec.Role == "main.read")
            {
                if (signalRace)
                {
                    Need(childSignal > mainRead && childSignal < index && workers[1].Exited && !inHandler && !handlerReturned,
                        "disk_race_signal_overlap");
                    inHandler = true;
                    ulong fp = (workers[0].Stack - 128 - LinuxSignalServices.FpBytes) & ~63UL;
                    signalFrame = ((fp - LinuxSignalServices.FrameBytes) & ~15UL) - 8;
                }
                else Need(!exitPending && progress.SetEquals(new[] { "child.pin", "child.disjoint-readonly", "child.disjoint-writable" }) &&
                    (close ? childClose > mainRead : notify > mainRead && childDuplicate > mainRead), "disk_race_overlap_missing");
                mainComplete = index;
            }
            if (pending.Spec.Role == "child.duplicate") Need(mainComplete >= 0 && mainComplete < index, "disk_race_cursor_lease");
            if (pending.Spec.Wait == Race.WaitPolicy.PipeRead) Need(notify >= 0 && notify < index, "disk_race_pipe_wake");
            if (pending.Spec.Wait == Race.WaitPolicy.Join) Need(workers[1].Exited, "disk_race_join_exit");
            windows.Add(new(pending.Index, index, workers[w].Identity, pending.Ordinal, pending.Sequence,
                pending.Request, pending.Source, value, output, completion)); workers[w].Pending = null;
        }

        Exact("TEST_START m16-009"); LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        int network = at; Exact("M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live"); Aux(network);
        Need(LinuxSyscallArm(Take())["root"] == userRoot, "disk_race_user_root");
        foreach (string start in new[] { "TEST_START m16.023", "TEST_START m16.024", "TEST_START m16.025" })
        { int index = at; Exact(start); Aux(index); }
        int binding = at; RaceDiskBound(Take()); Aux(binding);
        const string boundSuffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string boundLine = Take(); Need(boundLine.EndsWith(boundSuffix, StringComparison.Ordinal), "disk_race_preemption_policy");
        var bound = ReadRecord(boundLine[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(bound["root"] == kernelRoot && bound["workers"] == 2 && manifest.Threads == 2 &&
            bound["pool"] >= 4096 && bound["pool"] % 4096 == 0 && bound["pool"] <= 0x100000000UL - LinuxThreadProgram.SignalPoolPages * 4096UL &&
            bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728, "disk_race_bound");
        DistinctExtents([(userRoot, 4096), (kernelRoot, 4096), (bound["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL)], "disk_race_extents");
        var budget = Row("M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        Need(budget["started"] <= long.MaxValue && budget["epochs"] == 8192 && budget["steps"] == 16384 &&
            budget["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_race_budget");
        while (reaped < 2)
        {
            Need(at < rows.Length, "disk_race_truncated"); int index = at; string line = rows[at];
            if (line.StartsWith("M16_012_POLL ", StringComparison.Ordinal))
            {
                var p = Row("M16_012_POLL", "identity:dec", "clock:dec", "ready:dec", "idle:dec");
                Need(p["ready"] <= 2 && p["idle"] <= 1 && (p["idle"] == 0 || p["identity"] == 0), "disk_race_poll");
                if (p["identity"] != 0) Select(Worker(p["identity"]));
                else Need(selected < 0 && awaiting < 0, "disk_race_poll_owner");
                Clock(p["clock"]); Aux(index); continue;
            }
            if (line.StartsWith("M16_023_IO ", StringComparison.Ordinal))
            { RaceIoShape(Take()); Aux(index); continue; }
            if (line.StartsWith("M16_008_TICK ", StringComparison.Ordinal))
            {
                var t = Row("M16_008_TICK", "epoch:dec", "nanos:dec", "ready:dec");
                Need(selected < 0 && awaiting < 0 && workers.Any(w => w.Pending is not null) &&
                    t["epoch"] == ++epoch && epoch < budget["epochs"] && t["ready"] <= 2, "disk_race_tick");
                Clock(t["nanos"]); Aux(index); continue;
            }
            if (line.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal))
            {
                var d = Row("M16_009_DRAIN", "identity:dec", "epoch:dec", "nanos:dec");
                Need(awaiting >= 0 && d["identity"] == workers[awaiting].Identity && d["epoch"] == ++epoch && epoch < budget["epochs"], "disk_race_drain");
                Clock(d["nanos"]); Aux(index); continue;
            }
            if (line.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
            {
                Need(index > 0 && rows[index - 1].StartsWith("M16_008_CALL ", StringComparison.Ordinal), "disk_race_timing_order");
                var c = calls[^1]; _ = LinuxSyscallTiming(Take(), c.Identity, c.Ordinal, c.Spec.Number); Aux(index); continue;
            }
            if (line.StartsWith("M16_008_TIMING ", StringComparison.Ordinal))
            {
                Need(index > 0 && rows[index - 1].StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal), "disk_race_timing_order");
                var s = ReadRecord(rows[index - 1], "M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                LinuxTiming(Take(), s["identity"], epoch); Aux(index); continue;
            }
            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                int w = exitPending ? 1 : 0; Select(w);
                Need(!witness && (exitPending ? MainPending() : workers[0].Pending is null && workers[1].Reaped) &&
                    workers[w].Calls < workers[w].Plan.Count && workers[w].Plan[workers[w].Calls].Role == (exitPending ? "child.witness" : "main.witness"), "disk_race_witness_order");
                Exact("M16_005_WRITE fd=1 bytes=" + Race.Witness(variant).Length + " data=" + Race.Witness(variant)); witness = true; continue;
            }
            if (line.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
            {
                var c = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                    "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
                int w = Worker(c["identity"]); Select(w); RaceWorker owner = workers[w];
                bool handlerCall = w == 0 && inHandler;
                Need(owner.Pending is null && (handlerCall ? handlerCalls < handler.Count : owner.Calls < owner.Plan.Count), "disk_race_call_order");
                var e = handlerCall ? handler[handlerCalls] : owner.Plan[owner.Calls];
                bool signalReturn = handlerCall && e.Number == 15;
                ulong stack = handlerCall ? signalFrame + (signalReturn ? 8UL : 0UL) : owner.Stack;
                Need(c["nr"] == e.Number && c["a0"] == e.A0 && c["a1"] == e.A1 && c["a2"] == e.A2 && c["a3"] == e.A3 &&
                    c["a4"] == e.A4 && c["a5"] == e.A5 && c["rip"] == e.ReturnRip && c["rsp"] == stack && c["fs"] == owner.Fs && c["gs"] == 0 &&
                    LinuxSyscallGateway.ValidateUserFlags(c["flags"], false) && (c["flags"] & 0x400) == 0, "disk_race_call_binding");
                Clock(c["clock"]); var wait = waits[index]; var ret = returns[index]; owner.Ordinal++;
                if (handlerCall) handlerCalls++; else owner.Calls++;
                Need(wait.Identity == owner.Identity && wait.Ordinal == (ulong)owner.Ordinal && wait.Deadline == 0 && ret.Identity == owner.Identity && ret.Ordinal == wait.Ordinal &&
                    ret.SignalDepth == (handlerCall && !signalReturn ? 1UL : 0UL) && ret.SignalFrame == (handlerCall && !signalReturn ? signalFrame : 0) &&
                    ret.BlockedSignals == (handlerCall && !signalReturn ? 0x200UL : 0) &&
                    ret.Rip == (signalReturn ? owner.Plan.Single(p => p.Role == "main.read").ReturnRip : c["rip"]) &&
                    ret.Rsp == (signalReturn ? owner.Stack : stack) && ret.Flags == (signalReturn ? interruptedFlags : c["flags"]) &&
                    ret.Fs == owner.Fs && ret.Gs == 0 && ret.ValueBits == c["valueBits"], "disk_race_return");
                calls.Add(new(index, owner.Identity, (ulong)owner.Ordinal, e));
                if (e.Role == "child.signal") { Need(signalRace && MainPending() && owner.Calls == 1, "disk_race_signal_owner"); childSignal = index; }
                if (e.Role == "prep.clone") { Need(!workers[1].Created, "disk_race_duplicate_clone"); workers[1].Created = true; }
                if (w == 1 && e.Role is "child.pin" or "child.disjoint-readonly" or "child.disjoint-writable")
                { Need(MainPending(), "disk_race_progress_outside_read"); progress.Add(e.Role); }
                if (e.Role == "child.tid") Need(MainPending(), "disk_race_child_early");
                if (e.Role == "child.notify") { if (!close) Need(MainPending(), "disk_race_notify_late"); notify = index; }
                if (e.Role == "child.duplicate") { Need(MainPending(), "disk_race_duplicate_late"); childDuplicate = index; }
                if (e.Role == "child.close") { Need(close && MainPending(), "disk_race_close_late"); childClose = index; }
                bool blocked = c["action"] == (ulong)LinuxSyscallAction.Blocked;
                Need(e.Wait == Race.WaitPolicy.Disk ? blocked : e.Wait == Race.WaitPolicy.None ? !blocked : true, "disk_race_wait_policy");
                if (blocked)
                {
                    uint source = e.Wait == Race.WaitPolicy.Disk ? 4U : e.Wait == Race.WaitPolicy.PipeRead ? 2U : 1U;
                    Need(c["status"] == (ulong)LinuxSyscallGateStatus.Blocked && c["valueBits"] == 0 && wait.Source == source &&
                        wait.Sequence == ++owner.Sequence, "disk_race_wait");
                    if (e.Wait == Race.WaitPolicy.Join) Need(!workers[1].Exited, "disk_race_join_after_exit");
                    if (e.Wait == Race.WaitPolicy.PipeRead) Need(notify < 0, "disk_race_pipe_already_buffered");
                    if (e.Wait == Race.WaitPolicy.Disk) request++;
                    owner.Pending = new(e, index, (ulong)owner.Ordinal, owner.Sequence, e.Wait == Race.WaitPolicy.Disk ? request : 0, source);
                    if (e.Role == "main.read") { mainRead = index; interruptedFlags = c["flags"]; }
                    awaiting = w; action = (uint)LinuxSyscallAction.Blocked;
                }
                else
                {
                    bool exit = e.Number is 60 or 231;
                    long value = e.Wait == Race.WaitPolicy.Join ? -11 : e.Result;
                    Need(wait.Source == 0 && wait.Sequence == 0 && c["valueBits"] == unchecked((ulong)value) &&
                        c["action"] == (signalReturn ? 5UL : exit ? e.Number == 60 ? 4UL : 2UL : 1UL) && c["status"] == (exit ? 2UL : 1UL), "disk_race_outcome");
                    if (signalReturn) { Need(handlerCalls == handler.Count, "disk_race_signal_return_count"); inHandler = false; handlerReturned = true; }
                    if (e.Wait == Race.WaitPolicy.Join) Need(workers[1].Exited, "disk_race_join_early_eagain");
                    if (e.Wait == Race.WaitPolicy.PipeRead) Need(notify >= 0, "disk_race_pipe_empty_return");
                    if (exit)
                    {
                        if (exitPending)
                        {
                            Need(w == 1 && e.Role == "child.group-exit" && MainPending() && witness && !groupExit, "disk_race_pending_group_exit");
                            var p = workers[0].Pending!;
                            abandoned.Add(new(p.Index, index, workers[0].Identity, p.Ordinal, p.Sequence, p.Request, p.Spec.OutputBytes));
                            workers[0].Pending = null; groupExit = true;
                        }
                        else Need(w == 1 || workers[1].Reaped && witness && (!signalRace || handlerReturned), "disk_race_exit_order");
                        awaiting = w; action = (uint)c["action"]; exitMarker = false;
                    }
                }
                continue;
            }
            if (line.StartsWith("M16_025_COMPLETE ", StringComparison.Ordinal))
            {
                var c = Row("M16_025_COMPLETE", "identity:dec", "sequence:dec", "request:dec", "outputBytes:dec", "completion:dec", "valueBits:dec", "clock:dec");
                int w = Worker(c["identity"]); Select(w); var p = workers[w].Pending;
                Need(p is not null && p.Source == 4 && c["sequence"] == p.Sequence && c["request"] == p.Request && c["outputBytes"] == p.Spec.OutputBytes &&
                    c["completion"] == (ulong)(p.Spec.Role == "main.read" && variant == "InterruptWins" ? KernelThreadTable.Completion.Interrupted : KernelThreadTable.Completion.Woken) &&
                    c["valueBits"] == unchecked((ulong)p.Spec.Result), "disk_race_completion");
                Clock(c["clock"]); long value = p!.Spec.Result; uint output = p.Spec.OutputBytes;
                var resumed = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                Need(resumed["identity"] == c["identity"] && resumed["valueBits"] == c["valueBits"] && resumed["status"] == 1, "disk_race_resume");
                Finish(w, index, value, output, (uint)c["completion"]); continue;
            }
            if (line.StartsWith("M16_008_RESUME ", StringComparison.Ordinal))
            {
                var r = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec"); int w = Worker(r["identity"]); Select(w);
                var p = workers[w].Pending; Need(p is not null && p.Source is 1 or 2 && r["valueBits"] == unchecked((ulong)p.Spec.Result) && r["status"] == 1, "disk_race_nondisk_resume");
                Finish(w, index, p!.Spec.Result, 0); continue;
            }
            if (line.StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
            {
                var e = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec"); int w = Worker(e["identity"]);
                if (exitPending && groupExit && w == 0 && awaiting < 0 && selected < 0)
                { awaiting = 0; action = 4; exitMarker = false; }
                Need(awaiting == w && action is 2 or 4 && !exitMarker && !workers[w].Exited && e["clearTidResult"] == 0, "disk_race_exit");
                exitMarker = true; workers[w].Exited = true; continue;
            }
            if (line.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal))
            {
                var s = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                Need(awaiting >= 0 && s["identity"] == workers[awaiting].Identity && s["action"] == action &&
                    (action == 3 || exitMarker), "disk_race_supervisor_order");
                transfers += 2;
                Need(transfers / 2 < (int)budget["steps"] && s["transfers"] == (ulong)transfers && s["active"] == (ulong)workers.Count(w => w.Created && !w.Reaped) &&
                    s["ready"] <= (ulong)workers.Count(w => w.Created && !w.Exited && !w.Reaped), "disk_race_supervisor");
                selected = awaiting = -1; action = 0; continue;
            }
            if (line.StartsWith("M16_008_REAP ", StringComparison.Ordinal))
            {
                const string suffix = " context=zero stack=zero slot=zero";
                string reap = Take(); Need(reap.EndsWith(suffix, StringComparison.Ordinal), "disk_race_scrub");
                var r = ReadRecord(reap[..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec"); int w = Worker(r["old"]);
                Need(selected < 0 && awaiting < 0 && workers[w].Exited && !workers[w].Reaped && workers[w].Pending is null &&
                    workers[w].Calls == workers[w].Plan.Count && r["next"] == r["old"] + (1UL << 32), "disk_race_reap");
                workers[w].Reaped = true; reaped++; continue;
            }
            throw new ToolFailure("disk_race_unknown_record", "index=" + index);
        }
        for (uint fd = 0; fd < (exitPending ? 8U : 3U); fd++) { int index = at; Exact("M16_011_CLOSE fd=" + fd + " valueBits=0"); Aux(index); }
        var finished = Row("M16_008_BUDGET_END", "nanos:dec");
        Need(finished["nanos"] >= clock && finished["nanos"] >= budget["started"] && finished["nanos"] <= long.MaxValue &&
            finished["nanos"] - budget["started"] < LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_race_budget_end");
        const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        string ownerEnd = Take(); Need(ownerEnd.EndsWith(cleanup, StringComparison.Ordinal), "disk_race_cleanup");
        var end = ReadRecord(ownerEnd[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(end["reaped"] == 2 && end["transfers"] == (ulong)transfers && end["irqs"] == epoch && end["exit"] == 42, "disk_race_owner_end");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true"); Exact("CLROS_M16_009_READY"); Exact("TEST_PASS m16-009");
        Need(at == rows.Length && waits.Count == calls.Count && returns.Count == calls.Count && witness && mainRead >= 0 &&
            (exitPending ? mainComplete == -1 && groupExit && abandoned.Count == 1 : mainComplete > mainRead &&
                (close ? childClose > mainRead && childClose < mainComplete : signalRace ? childSignal > mainRead && childSignal < mainComplete && handlerReturned :
                 childDuplicate > mainRead && childDuplicate < mainComplete)),
            "disk_race_terminal");
        return new(variant, transfers, epoch, calls, windows, auxiliary, mainRead, mainComplete, childClose, abandoned, childSignal);
    }

    private sealed record RacePending(Race.CallSpec Spec, int Index, ulong Ordinal, ulong Sequence, ulong Request, uint Source);
    private sealed class RaceWorker(IReadOnlyList<Race.CallSpec> plan, ulong identity, ulong stack, ulong fs)
    {
        internal readonly IReadOnlyList<Race.CallSpec> Plan = plan;
        internal readonly ulong Identity = identity, Stack = stack, Fs = fs;
        internal bool Created, Exited, Reaped;
        internal int Calls, Ordinal;
        internal ulong Sequence;
        internal RacePending? Pending;
    }

    private static void RaceDiskBound(string line)
    {
        const string suffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        Need(line.EndsWith(suffix, StringComparison.Ordinal), "disk_race_disk_policy");
        var d = ReadRecord(line[..^suffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec", "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(d["owner"] is >= 4096 and < 0x0000800000000000 && d["owner"] % 8 == 0 && d["epoch"] != 0 &&
            d["arena"] >= 4096 && d["arena"] % 4096 == 0 && d["pages"] is > 0 and <= 256 && d["arena"] <= 0x100000000UL - d["pages"] * 4096 &&
            d["bar"] >= 0x100000 && d["bar"] % 16384 == 0 && d["bar"] < 0x0000800000000000 - 16384 &&
            d["capacity"] is >= 68 and <= (ulong)long.MaxValue / 512 && d["bootstrapCompleted"] > 0 && d["bootstrapCopied"] <= d["bootstrapCompleted"] && d["clock"] <= long.MaxValue, "disk_race_disk_geometry");
    }

    private static void RaceIoShape(string line)
    {
        const string number = "(0|[1-9][0-9]*)";
        string pattern = "\\AM16_023_IO kind=([012]) owner=" + number + " epoch=" + number + " generation=" + number + " sector=" + number +
            " sequence=" + number + " bytes=" + number + " available=" + number + " consumed=" + number + " clock=" + number + "( copied=committed data=[0-9a-f]{1024})?\\z";
        var match = Regex.Match(line, pattern, RegexOptions.CultureInvariant);
        Need(match.Success && (match.Groups[1].Value == "2") == match.Groups[11].Success, "disk_race_io_shape");
        for (int i = 2; i <= 10; i++) Need(ulong.TryParse(match.Groups[i].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "disk_race_io_number");
    }
}
