using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static class LinuxDiskRaceTranscriptTests
{
    [HostTest("disk race leaf joins two exact workers overlapping requests and close reuse without granting auxiliary authority")]
    private static void Positive()
    {
        foreach (string variant in F.Variants)
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
        foreach (string variant in F.Variants)
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
        foreach (string variant in F.Variants)
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
        foreach (string variant in F.Variants)
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
