using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original indexed observations requiring separate sector, NIC,
    /// timing or instruction joins. Text is copied; a value is never execution authority.</summary>
    public readonly record struct LinuxDiskAuxiliary(int RecordIndex, string Text);
    public readonly record struct LinuxDiskCompletion(int CallRecordIndex, int CompletionRecordIndex,
        ulong Ordinal, ulong Sequence, ulong RequestGeneration, uint OutputBytes, long Value);

    /// <summary>Immutable value inspection of the exact mechanical fixture.
    /// Checked flags cover only exact calls, wait/completion associations and
    /// reported exit/reap/restore accounting. Auxiliary rows remain mandatory
    /// independent joins; no firmware, native, device, disk bytes, arbitrary
    /// instruction execution or whole-guest acceptance is established.</summary>
    public sealed class LinuxDiskFileLeafReport
    {
        public bool CheckedCalls => true;
        public bool CheckedWaitCompletions => true;
        public bool CheckedLifecycleRecords => true;
        public bool CheckedAuxiliaryJoins => false;
        public int Calls { get; }
        public int Transfers { get; }
        public ulong Epochs { get; }
        public IReadOnlyList<LinuxDiskCompletion> Completions { get; }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary { get; }
        internal LinuxDiskFileLeafReport(int calls, int transfers, ulong epochs,
            List<LinuxDiskCompletion> completions, List<LinuxDiskAuxiliary> auxiliary)
        { Calls = calls; Transfers = transfers; Epochs = epochs;
            Completions = Array.AsReadOnly(completions.ToArray()); Auxiliary = Array.AsReadOnly(auxiliary.ToArray()); }
    }

    /// <summary>Synchronously borrow the complete m16-009 leaf (start through
    /// final pass) and exact LinuxDiskFileProofFixture package. Neither input is
    /// retained or mutated. Accept at most131072 ASCII rows/1536 characters each.
    /// Known auxiliary observations are checked structurally and returned with
    /// their original indexes for the caller's independent joins; unknown rows
    /// are rejected. The package's asserted memory comparisons are not replayed.
    /// Exceptions report malformed, unsupported or incomplete value evidence.</summary>
    public static LinuxDiskFileLeafReport InspectLinuxDiskFileLeafForTests(IReadOnlyList<string> lines,
        byte[] package, ulong userRoot, ulong kernelRoot)
    {
        int rowCount = lines?.Count ?? 0;
        Need(package is not null && rowCount is > 0 and <= 131072, "disk_leaf_input");
        string[] input = new string[rowCount];
        for (int i = 0; i < rowCount; i++) input[i] = lines![i];
        Need(input.All(r => r is { Length: > 0 and <= 1536 } && r.All(c => c is >= ' ' and <= '~')), "disk_leaf_row_bound");
        LinuxDiskFileProofFixture.RequirePackage(package!);
        var manifest = LinuxElfPackage.Validate(package!).Manifest;
        var facts = ReadLinuxDiskCallObservations(input);
        var waits = facts.Waits.ToDictionary(v => v.RecordIndex);
        var returns = facts.Returns.ToDictionary(v => v.RecordIndex);
        string[] rows = ValidateLinuxDiskCallOrdinals(input);
        var expected = DiskFileCalls(); var rips = LinuxDiskFileProofFixture.ExpectedReturnRips();
        Need(expected.Length == rips.Count && expected.Length == LinuxDiskFileProofFixture.ExpectedCalls, "disk_leaf_fixture_inventory");
        Need(userRoot is >= 4096 and <= 0xfffff000 && userRoot % 4096 == 0 && kernelRoot is >= 4096 and <= 0xfffff000 &&
            kernelRoot % 4096 == 0 && kernelRoot != userRoot, "disk_leaf_roots");
        ulong stack = DiskFileStack(package!); int at = 0, call = 0, transfers = 0; ulong clock = 0, epoch = 0, request = 0, sequence = 0;
        const ulong identity = 0x100000001;
        bool exitCall = false, exitRow = false, reaped = false, witness = false;
        uint supervisor = 0; DiskFileCall? pending = null; int pendingIndex = -1; ulong pendingOrdinal = 0, pendingRequest = 0;
        List<LinuxDiskCompletion> completions = []; List<LinuxDiskAuxiliary> auxiliary = [];
        string Take() { Need(at < rows.Length, "disk_leaf_truncated"); return rows[at++]; }
        void Exact(string value) => Need(Take() == value, "disk_leaf_order_" + value);
        Dictionary<string, ulong> Row(string name, params string[] keys) => ReadRecord(Take(), name, keys);
        void Clock(ulong value) { Need(value >= clock && value <= long.MaxValue, "disk_leaf_clock"); clock = value; }
        void Aux(int index) => auxiliary.Add(new(index, new string(input[index].AsSpan())));
        Exact("TEST_START m16-009"); LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        int binding = at; Exact("M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live"); Aux(binding);
        Need(LinuxSyscallArm(Take())["root"] == userRoot, "disk_leaf_user_root");
        foreach (string start in new[] { "TEST_START m16.023", "TEST_START m16.024", "TEST_START m16.025" })
        { int original = at; Exact(start); Aux(original); }
        int diskBinding = at; string diskLine = Take();
        const string diskSuffix = " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained";
        Need(diskLine.EndsWith(diskSuffix, StringComparison.Ordinal), "disk_leaf_disk_policy");
        var disk = ReadRecord(diskLine[..^diskSuffix.Length], "M16_023_BOUND", "owner:dec", "epoch:dec", "arena:dec", "pages:dec", "bar:dec", "capacity:dec",
            "bootstrapCompleted:dec", "bootstrapCopied:dec", "clock:dec");
        Need(disk["owner"] is >= 4096 and < 0x0000800000000000 && disk["owner"] % 8 == 0 && disk["epoch"] != 0 &&
            disk["pages"] is > 0 and <= 256 && disk["arena"] >= 4096 && disk["arena"] % 4096 == 0 && disk["arena"] <= 0x100000000UL - disk["pages"] * 4096 &&
            disk["bar"] >= 0x100000 && disk["bar"] % 16384 == 0 && disk["bar"] < 0x0000800000000000 - 16384 &&
            disk["capacity"] is >= 68 and <= (ulong)long.MaxValue / 512 && disk["bootstrapCompleted"] > 0 &&
            disk["bootstrapCopied"] <= disk["bootstrapCompleted"] && disk["clock"] <= long.MaxValue, "disk_leaf_disk_geometry");
        Aux(diskBinding); // Exact native/image/counter ownership remains a required independent join.
        const string boundSuffix = " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
        string boundLine = Take(); Need(boundLine.EndsWith(boundSuffix, StringComparison.Ordinal), "disk_leaf_bound_policy");
        var bound = ReadRecord(boundLine[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(bound["root"] == kernelRoot && bound["workers"] == manifest.Threads && manifest.Threads == 2 &&
            bound["pool"] >= 4096 && bound["pool"] % 4096 == 0 && bound["pool"] <= 0x100000000UL - LinuxThreadProgram.SignalPoolPages * 4096UL &&
            bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728, "disk_leaf_geometry");
        DistinctExtents([(userRoot, 4096), (kernelRoot, 4096), (bound["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL)], "disk_leaf_extents");
        var budget = Row("M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
        Need(budget["started"] <= long.MaxValue && budget["epochs"] == 8192 && budget["steps"] == 16384 &&
            budget["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_leaf_budget");
        while (!reaped)
        {
            Need(at < rows.Length, "disk_leaf_truncated"); string line = rows[at]; int index = at;
            if (line.StartsWith("M16_012_POLL ", StringComparison.Ordinal))
            {
                var p = Row("M16_012_POLL", "identity:dec", "clock:dec", "ready:dec", "idle:dec");
                Need(p["identity"] is 0 or identity && p["ready"] <= 1 && p["idle"] <= 1 &&
                    (p["idle"] == 0 || p["identity"] == 0), "disk_leaf_poll"); Clock(p["clock"]); Aux(index); continue;
            }
            if (line.StartsWith("M16_023_IO ", StringComparison.Ordinal))
            {
                const string dec = "(0|[1-9][0-9]*)";
                string pattern = "\\AM16_023_IO kind=([012]) owner=" + dec + " epoch=" + dec + " generation=" + dec + " sector=" + dec +
                    " sequence=" + dec + " bytes=" + dec + " available=" + dec + " consumed=" + dec + " clock=" + dec + "( copied=committed data=[0-9a-f]{1024})?\\z";
                var m = Regex.Match(line, pattern, RegexOptions.CultureInvariant);
                Need(m.Success && (m.Groups[1].Value == "2") == m.Groups[11].Success, "disk_leaf_io_shape");
                for (int j = 2; j <= 10; j++) Need(ulong.TryParse(m.Groups[j].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "disk_leaf_io_number");
                at++; Aux(index); continue; // Exact device/LBA/sector bytes belong to NativeDiskReadObservations.
            }
            if (line.StartsWith("M16_008_TICK ", StringComparison.Ordinal))
            {
                var t = Row("M16_008_TICK", "epoch:dec", "nanos:dec", "ready:dec");
                Need(supervisor == 0 && pending.HasValue && t["epoch"] == ++epoch && epoch < budget["epochs"] && t["ready"] <= 1, "disk_leaf_tick");
                Clock(t["nanos"]); Aux(index); continue;
            }
            if (line.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) || line.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal))
            {
                bool drain = line.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal);
                var t = Row(drain ? "M16_009_DRAIN" : "M16_009_IRQ", drain ? ["identity:dec", "epoch:dec", "nanos:dec"] :
                    ["identity:dec", "epoch:dec", "nanos:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "valueBits:dec"]);
                Need(t["identity"] == identity && t["epoch"] == ++epoch && epoch < budget["epochs"] &&
                    (!drain ? supervisor == 0 && !pending.HasValue && !exitCall : supervisor != 0), "disk_leaf_irq");
                if (!drain) { Need(t["rip"] >= LinuxDiskFileProofFixture.ImageBase + LinuxDiskFileProofFixture.EntryOffset &&
                    t["rip"] < LinuxDiskFileProofFixture.ImageBase + LinuxDiskFileProofFixture.FailureOffset && t["rsp"] == stack && t["fs"] == 0 && t["gs"] == 0 &&
                    LinuxSyscallGateway.ValidateUserFlags(t["flags"], true), "disk_leaf_irq_envelope"); supervisor = 1; }
                Clock(t["nanos"]); Aux(index); continue; // Instruction boundary/value still require the parent's instruction join.
            }
            if (line.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal))
            {
                Need(call != 0 && index > 0 && rows[index - 1].StartsWith("M16_008_CALL ", StringComparison.Ordinal), "disk_leaf_timing_order");
                _ = LinuxSyscallTiming(Take(), identity, (ulong)call, expected[call - 1].Number); Aux(index); continue;
            }
            if (line.StartsWith("M16_008_TIMING ", StringComparison.Ordinal))
            {
                Need(index > 0 && rows[index - 1].StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal), "disk_leaf_timing_order");
                LinuxTiming(Take(), identity, epoch); Aux(index); continue;
            }
            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                Need(call == expected.Length - 2 && !witness && pending is null && supervisor == 0, "disk_leaf_write_order");
                Exact("M16_005_WRITE fd=1 bytes=" + LinuxDiskFileProofFixture.Witness.Length + " data=" + LinuxDiskFileProofFixture.Witness); witness = true; continue;
            }
            if (line.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
            {
                Need(call < expected.Length && pending is null && supervisor == 0 && !exitCall, "disk_leaf_call_order");
                var e = expected[call]; var c = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                    "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
                Need(c["identity"] == identity && c["nr"] == e.Number && c["a0"] == e.A0 && c["a1"] == e.A1 && c["a2"] == e.A2 && c["a3"] == e.A3 &&
                    c["a4"] == 0 && c["a5"] == 0 && c["rip"] == rips[call] && c["rsp"] == stack && c["fs"] == 0 && c["gs"] == 0 &&
                    LinuxSyscallGateway.ValidateUserFlags(c["flags"], true) && (c["flags"] & 0x400) == 0, "disk_leaf_call_binding");
                Clock(c["clock"]); var wait = waits[index]; var returned = returns[index]; call++;
                Need(wait.Identity == identity && wait.Ordinal == (ulong)call && wait.Deadline == 0 && returned.Identity == identity && returned.Ordinal == wait.Ordinal &&
                    returned.SignalDepth == 0 && returned.SignalFrame == 0 && returned.BlockedSignals == 0 && returned.Rip == c["rip"] && returned.Rsp == stack &&
                    returned.Flags == c["flags"] && returned.Fs == 0 && returned.Gs == 0 && returned.ValueBits == c["valueBits"], "disk_leaf_return");
                if (e.Request) request++;
                if (e.Pending)
                {
                    Need(c["action"] == 3 && c["status"] == 9 && c["valueBits"] == 0 && wait.Source == 4 && wait.Sequence == ++sequence, "disk_leaf_wait");
                    pending = e; pendingIndex = index; pendingOrdinal = (ulong)call; pendingRequest = request; supervisor = 3;
                }
                else
                {
                    bool end = e.Number == 231;
                    Need(wait.Source == 0 && wait.Sequence == 0 && c["valueBits"] == unchecked((ulong)e.Value) && c["action"] == (end ? 2UL : 1UL) &&
                        c["status"] == (end ? 2UL : 1UL), "disk_leaf_outcome");
                    if (end) { Need(witness, "disk_leaf_witness_missing"); exitCall = true; supervisor = 2; }
                }
                continue;
            }
            if (line.StartsWith("M16_025_COMPLETE ", StringComparison.Ordinal))
            {
                Need(pending.HasValue && supervisor == 0, "disk_leaf_completion_order"); var e = pending!.Value;
                var c = Row("M16_025_COMPLETE", "identity:dec", "sequence:dec", "request:dec", "outputBytes:dec", "completion:dec", "valueBits:dec", "clock:dec");
                Need(c["identity"] == identity && c["sequence"] == sequence && c["request"] == pendingRequest && c["outputBytes"] == e.OutputBytes &&
                    c["completion"] == (ulong)KernelThreadTable.Completion.Woken && c["valueBits"] == unchecked((ulong)e.Value), "disk_leaf_completion"); Clock(c["clock"]);
                completions.Add(new(pendingIndex, index, pendingOrdinal, sequence, pendingRequest, e.OutputBytes, e.Value));
                var resume = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                Need(resume["identity"] == identity && resume["valueBits"] == c["valueBits"] && resume["status"] == 1, "disk_leaf_resume"); pending = null; continue;
            }
            if (line.StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
            {
                Need(exitCall && !exitRow && supervisor == 2, "disk_leaf_exit_order"); var e = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                Need(e["identity"] == identity && e["clearTidResult"] == 0, "disk_leaf_exit"); exitRow = true; continue;
            }
            if (line.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal))
            {
                Need(supervisor != 0 && (supervisor != 2 || exitRow), "disk_leaf_supervisor_order");
                var s = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec"); transfers += 2;
                Need(transfers / 2 < (int)budget["steps"] && s["identity"] == identity && s["action"] == supervisor && s["transfers"] == (ulong)transfers &&
                    s["active"] == 1 && (supervisor == 1 ? s["ready"] == 1 : supervisor == 2 ? s["ready"] == 0 : s["ready"] <= 1), "disk_leaf_supervisor");
                supervisor = 0; continue;
            }
            if (line.StartsWith("M16_008_REAP ", StringComparison.Ordinal))
            {
                Need(exitRow && supervisor == 0 && pending is null && call == expected.Length, "disk_leaf_reap_order");
                Exact("M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero"); reaped = true; continue;
            }
            throw new ToolFailure("disk_leaf_unknown_record", "index=" + index);
        }
        for (uint fd = 0; fd < 3; fd++)
        {
            int close = at; Exact("M16_011_CLOSE fd=" + fd.ToString(CultureInfo.InvariantCulture) + " valueBits=0"); Aux(close);
        }
        var finished = Row("M16_008_BUDGET_END", "nanos:dec");
        Need(finished["nanos"] >= clock && finished["nanos"] >= budget["started"] && finished["nanos"] <= long.MaxValue &&
            finished["nanos"] - budget["started"] < LinuxThreadExecutionBudget.RuntimeNanoseconds, "disk_leaf_budget_end");
        const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        string owner = Take(); Need(owner.EndsWith(cleanup, StringComparison.Ordinal), "disk_leaf_cleanup");
        var endOwner = ReadRecord(owner[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(endOwner["reaped"] == 1 && endOwner["transfers"] == (ulong)transfers && endOwner["irqs"] == epoch && endOwner["exit"] == 42 && completions.Count != 0, "disk_leaf_owner");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true"); Exact("CLROS_M16_009_READY"); Exact("TEST_PASS m16-009");
        Need(at == rows.Length && waits.Count == expected.Length && returns.Count == expected.Length, "disk_leaf_extra_record");
        return new(call, transfers, epoch, completions, auxiliary);
    }

    private readonly record struct DiskFileCall(ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, long Value, bool Pending, bool Request, uint OutputBytes);
    private static DiskFileCall[] DiskFileCalls()
    {
        const ulong b = LinuxDiskFileProofFixture.ImageBase, output = b + 0x4000, path = b + 0x1000, stat = b + 0x3000;
        const uint length = WebRootImageFixture.FragmentedFixtureBytes; List<DiskFileCall> calls = [];
        void Add(ulong n, ulong a, ulong c, ulong d, long value, bool pending = false, ulong offset = 0, bool allocated = false)
        {
            uint bytes = n is 4 or 6 ? 144U : n is 0 or 17 ? c is >= b + 0x1000 and < b + LinuxDiskFileProofFixture.ElfBytes ?
                (uint)Math.Min(Math.Min(d, LinuxDescriptorServices.DiskCompletionBytes), b + LinuxDiskFileProofFixture.ElfBytes - c) : 0 : 0;
            calls.Add(new(n, a, c, d, offset, value, pending, pending || allocated, bytes));
        }
        Add(4, path, stat, 0, 0, true); Add(6, b + 0x1220, stat, 0, 0, true); Add(257, 0xffffff9c, path, 0, 3, true); Add(5, 3, stat, 0, 0);
        Add(0, 3, output, 16384, 8192, true); Add(8, 3, 0, 1, 8192, true); Add(72, 3, 0, 6, 6); Add(0, 6, output + 4094, 8, 8, true);
        Add(17, 3, output, 4, 4, true, 65535); Add(8, 3, 0, 1, 8200, true);
        for (int i = 0; i < 2; i++) { Add(2, path, 0, 0, 4, true); Add(0, 4, output, 1, 1, true); Add(3, 4, 0, 0, 0); }
        Add(8, 3, length - 2, 0, length - 2, true); Add(0, 3, output, 8, 2, true); Add(0, 3, 0, 8, 0, true);
        Add(8, 3, 0, 0, 0, true); Add(0, 3, 0, 1, -14, true); Add(8, 3, 0, 1, 0, true);
        Add(0, 3, b + 0x200, 1, -14, true); Add(8, 3, 0, 1, 0, true);
        Add(0, 3, b + LinuxDiskFileProofFixture.ElfBytes - 1, 4, -14, true); Add(8, 3, 0, 1, 0, true);
        Add(8, 3, length - 1, 0, length - 1, true); Add(0, 3, b + LinuxDiskFileProofFixture.ElfBytes - 1, 4, 1, true);
        Add(8, 3, 0, 99, -22); Add(0, 3, output, 65537, -22); Add(2, path, 1, 0, -30); Add(2, path, 0x40, 0, -30);
        Add(2, b + 0x1060, 0, 0, -2, true); Add(2, b + 0x10c0, 0, 0, -13, allocated: true); Add(2, b + 0x1140, 0, 0, -20, true);
        uint[] more = [4, 5, 7, 8, 9, 10, 11]; foreach (uint fd in more) Add(2, path, 0, 0, fd, true);
        Add(2, path, 0, 0, -23, allocated: true); foreach (uint fd in more) Add(3, fd, 0, 0, 0);
        Add(3, 3, 0, 0, 0); Add(0, 3, output, 1, -9); Add(3, 6, 0, 0, 0); Add(2, b + 0x11c0, 0, 0, 3, true);
        Add(5, 3, stat, 0, 0); Add(0, 3, 0, 1, 0, true); Add(3, 3, 0, 0, 0);
        Add(1, 1, b + LinuxDiskFileProofFixture.WitnessOffset, (ulong)LinuxDiskFileProofFixture.Witness.Length, LinuxDiskFileProofFixture.Witness.Length); Add(231, 42, 0, 0, 0);
        return calls.ToArray();
    }

    private static unsafe ulong DiskFileStack(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package); byte* seed = stackalloc byte[32]; for (int i = 0; i < 32; i++) seed[i] = 1;
        byte* storage = (byte*)System.Runtime.InteropServices.NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        Need(storage != null, "disk_leaf_stack_allocation");
        try { fixed (byte* p = package) { Need(LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes, p + view.PayloadOffset, view.PayloadBytes,
            seed, 32, storage, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var initial) == LinuxElfStartupStatus.Ready, "disk_leaf_stack"); return initial.StackPointer; } }
        finally { System.Runtime.InteropServices.NativeMemory.AlignedFree(storage); }
    }
}
