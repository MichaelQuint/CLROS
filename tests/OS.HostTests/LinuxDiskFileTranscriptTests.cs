using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskFileProofFixture;

internal static class LinuxDiskFileTranscriptTests
{
    [HostTest("disk file leaf joins exact fixture calls request gaps output ceilings and immutable indexed facts")]
    private static void Positive()
    {
        string[] rows = Synthetic(), before = (string[])rows.Clone(); byte[] package = F.CreatePackage(), original = (byte[])package.Clone();
        var report = Inspect(rows, package);
        HostCheck.True(report.CheckedCalls && report.CheckedWaitCompletions && report.CheckedLifecycleRecords && !report.CheckedAuxiliaryJoins);
        HostCheck.Equal((int)F.ExpectedCalls, report.Calls); HostCheck.True(report.Completions.Count > 20);
        HostCheck.True(rows.SequenceEqual(before) && package.SequenceEqual(original));
        foreach (var completion in report.Completions)
        {
            HostCheck.True(rows[completion.CallRecordIndex].StartsWith("M16_008_CALL ", StringComparison.Ordinal));
            HostCheck.True(rows[completion.CompletionRecordIndex].StartsWith("M16_025_COMPLETE ", StringComparison.Ordinal));
            HostCheck.True(completion.RequestGeneration >= completion.Sequence);
        }
        HostCheck.True(report.Completions.Any(c => c.RequestGeneration > c.Sequence));
        HostCheck.True(report.Completions.Any(c => c.OutputBytes == 0 && c.Value == -14));
        HostCheck.True(report.Completions.Any(c => c.OutputBytes == 1 && c.Value == -14));
        HostCheck.True(report.Completions.Any(c => c.OutputBytes == 1 && c.Value == 1));
        HostCheck.True(report.Completions.Any(c => c.OutputBytes == 0 && c.Value == 0));
        HostCheck.True(report.Auxiliary.All(v => v.Text == rows[v.RecordIndex]));
        string retained = report.Auxiliary[0].Text; Array.Fill(rows, "changed"); Array.Clear(package);
        HostCheck.Equal(retained, report.Auxiliary[0].Text);
        bool immutable = false; try { ((IList<QemuTranscripts.LinuxDiskAuxiliary>)report.Auxiliary)[0] = default; } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    [HostTest("disk file leaf rejects forged syscall arguments exact rips outcomes ordinals and return envelopes")]
    private static void Calls()
    {
        string[] rows = Synthetic(); int[] calls = Indexes(rows, "M16_008_CALL ");
        foreach (int at in new[] { calls[0], calls[4], calls[8], calls[^2], calls[^1] })
            foreach (string name in new[] { "identity", "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "rsp", "fs", "gs", "valueBits", "action", "status", "ordinal" })
                Reject(Change(rows, at, name, Field(rows[at], name) + 1));
        foreach (string name in new[] { "signalDepth", "signalFrame", "blockedSignals", "returnRip", "returnRsp", "returnFlags", "returnFs", "returnGs", "returnValueBits" })
            Reject(Change(rows, calls[0], name, Field(rows[calls[0]], name) + 1));
        Reject(Change(rows, calls[0], "flags", 0)); Reject(Change(rows, calls[0], "clock", ulong.MaxValue));
        Reject(Change(rows, calls[^1], "a0", 99)); Reject(Change(rows, calls[^1], "rip", F.FailureExitRip));
    }

    [HostTest("disk file leaf authenticates each pending sequence request generation and original copy ceiling before resume")]
    private static void Completions()
    {
        string[] rows = Synthetic();
        foreach (int at in Indexes(rows, "M16_025_COMPLETE "))
        {
            foreach (string name in new[] { "identity", "sequence", "request", "outputBytes", "completion", "valueBits" })
                Reject(Change(rows, at, name, Field(rows[at], name) + 1));
            Reject(rows.Where((_, i) => i != at).ToArray()); Reject([.. rows[..at], rows[at], .. rows[at..]]);
        }
        int call = Indexes(rows, "M16_008_CALL ")[0];
        foreach (string name in new[] { "waitSource", "waitDeadline", "waitSequence" }) Reject(Change(rows, call, name, Field(rows[call], name) + 1));
        int complete = Indexes(rows, "M16_025_COMPLETE ")[0];
        foreach (string name in new[] { "identity", "valueBits", "status" }) Reject(Change(rows, complete + 1, name, Field(rows[complete + 1], name) + 1));
        string[] reordered = (string[])rows.Clone(); (reordered[complete], reordered[complete + 1]) = (reordered[complete + 1], reordered[complete]); Reject(reordered);
        Reject(rows.Where((_, i) => i != complete + 1).ToArray());
    }

    [HostTest("disk file leaf rejects omitted duplicated reordered lifecycle records and failed restoration")]
    private static void Lifecycle()
    {
        string[] rows = Synthetic();
        foreach (string prefix in new[] { "M16_008_SUPERVISOR ", "M16_008_EXIT ", "M16_008_REAP ", "M16_008_BUDGET_END ", "M16_008_OWNER_END ", "M16_009_RESTORE ", "M16_011_CLOSE " })
        {
            int at = Indexes(rows, prefix)[0]; Reject(rows.Where((_, i) => i != at).ToArray()); Reject([.. rows[..at], rows[at], .. rows[at..]]);
        }
        int owner = Indexes(rows, "M16_008_OWNER_END ")[0];
        foreach (string name in new[] { "reaped", "transfers", "irqs", "exit", "userReserved", "userBacked" }) Reject(Change(rows, owner, name, Field(rows[owner], name) + 1));
        int supervisor = Indexes(rows, "M16_008_SUPERVISOR ")[0];
        foreach (string name in new[] { "identity", "action", "transfers", "ready", "active" }) Reject(Change(rows, supervisor, name, name == "ready" ? 2UL : Field(rows[supervisor], name) + 1));
        int reap = Indexes(rows, "M16_008_REAP ")[0]; Reject(Change(rows, reap, "next", 4294967298));
        Reject(Replace(rows, reap, rows[reap].Replace("stack=zero", "stack=live", StringComparison.Ordinal)));
        Reject(Replace(rows, rows.Length - 3, rows[^3].Replace("root=true", "root=false", StringComparison.Ordinal)));
        int write = Indexes(rows, "M16_005_WRITE ")[0]; Reject(Replace(rows, write, rows[write] + "!"));
        Reject(rows.Where((_, i) => i != write).ToArray()); Reject([.. rows, rows[^1]]);
    }

    [HostTest("disk file leaf retains bounded auxiliary sector timing tick and irq records for mandatory independent joins")]
    private static void Auxiliary()
    {
        string[] rows = Synthetic(auxiliary: true); var report = Inspect(rows);
        HostCheck.True(!report.CheckedAuxiliaryJoins && report.Epochs == 2);
        foreach (string prefix in new[] { "M16_023_IO ", "M16_008_TICK ", "M16_009_IRQ ", "M16_008_SYSCALL_TIME ", "M16_008_TIMING " })
            HostCheck.True(report.Auxiliary.Any(v => v.Text.StartsWith(prefix, StringComparison.Ordinal) && v.Text == rows[v.RecordIndex]));
        int io = Indexes(rows, "M16_023_IO ")[0];
        Reject(Replace(rows, io, rows[io] + " extra=1")); Reject(Replace(rows, io, rows[io].Replace("kind=0", "kind=3", StringComparison.Ordinal)));
        Reject(Replace(rows, io, rows[io].Replace("owner=1", "owner=18446744073709551616", StringComparison.Ordinal)));
        int poll = Indexes(rows, "M16_012_POLL ")[0]; Reject(Change(rows, poll, "identity", 4294967298)); Reject(Change(rows, poll, "ready", 2));
        int irq = Indexes(rows, "M16_009_IRQ ")[0];
        foreach (string name in new[] { "identity", "epoch", "rsp", "fs", "gs" }) Reject(Change(rows, irq, name, Field(rows[irq], name) + 1));
        Reject(Change(rows, irq, "rip", F.ImageBase + F.FailureOffset));
        int tick = Indexes(rows, "M16_008_TICK ")[0]; Reject(Change(rows, tick, "epoch", 9));
        Reject([.. rows[..io], "M16_023_UNKNOWN kind=0", .. rows[io..]]);
    }

    [HostTest("disk file leaf rejects stale package root budget and bounded framing inputs")]
    private static void Bounds()
    {
        string[] rows = Synthetic(); byte[] package = F.CreatePackage(); package[^1] ^= 1;
        bool rejected = false; try { Inspect(rows, package); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected);
        rejected = false; try { QemuTranscripts.InspectLinuxDiskFileLeafForTests(rows, F.CreatePackage(), 0x300000, 0x100000); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected);
        int budget = Indexes(rows, "M16_008_BUDGET ")[0]; foreach (string name in new[] { "epochs", "steps", "nanos" }) Reject(Change(rows, budget, name, Field(rows[budget], name) + 1));
        Reject(Change(rows, Indexes(rows, "M16_008_BUDGET_END ")[0], "nanos", ulong.MaxValue));
        Reject([]); Reject([.. rows[..3], "unknown", .. rows[3..]]); Reject(Replace(rows, 0, rows[0] + "\r\n"));
        Reject(Replace(rows, 0, new string('a', 1537))); Reject(Replace(rows, 0, null!));
        bool bound = false; try { QemuTranscripts.InspectLinuxDiskFileLeafForTests(new Oversize(), F.CreatePackage(), 0x200000, 0x100000); } catch (ToolFailure) { bound = true; } HostCheck.True(bound);
    }

    [HostTest("disk file leaf retains bootstrap starts and bounded binding at the actual post-arm position")]
    private static void Bootstrap()
    {
        string[] rows = Synthetic(); var report = Inspect(rows);
        int armed = Indexes(rows, "M16_005_ARMED ")[0], bound = Indexes(rows, "M16_023_BOUND ")[0];
        HostCheck.Equal(armed + 4, bound);
        for (int at = armed + 1; at <= bound; at++)
        {
            HostCheck.True(report.Auxiliary.Any(v => v.RecordIndex == at && v.Text == rows[at]));
            Reject(rows.Where((_, i) => i != at).ToArray()); Reject([.. rows[..at], rows[at], .. rows[at..]]);
        }
        string[] moved = (string[])rows.Clone(); (moved[armed], moved[armed + 1]) = (moved[armed + 1], moved[armed]); Reject(moved);
        foreach (string name in new[] { "owner", "epoch", "arena", "pages", "bar", "capacity", "bootstrapCompleted" }) Reject(Change(rows, bound, name, 0));
        Reject(Change(rows, bound, "owner", 0x0000800000000000)); Reject(Change(rows, bound, "owner", 4097));
        Reject(Change(rows, bound, "pages", 257)); Reject(Change(rows, bound, "arena", 0xfffff000));
        Reject(Change(rows, bound, "bar", 0x100001)); Reject(Change(rows, bound, "capacity", ulong.MaxValue));
        Reject(Change(rows, bound, "bootstrapCopied", 41)); Reject(Change(rows, bound, "clock", ulong.MaxValue));
        Reject(Replace(rows, bound, rows[bound].Replace("pci=0:7:0", "pci=0:6:0", StringComparison.Ordinal)));
        Reject(Replace(rows, bound, rows[bound] + " extra=1"));
        Reject(Replace(rows, bound, rows[bound].Replace("owner=8388608", "owner=18446744073709551616", StringComparison.Ordinal)));
    }

    // Expected arguments and outcomes are independently decoded from the
    // canonical mechanical machine-code calls, not the inspector's plan.
    private static unsafe string[] Synthetic(bool auxiliary = false)
    {
        byte[] package = F.CreatePackage(), elf = F.CreateElf(); var view = LinuxElfPackage.Validate(package);
        ulong stack; byte* buffer = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096); byte* seed = stackalloc byte[32];
        for (int i = 0; i < 32; i++) seed[i] = 1;
        try { fixed (byte* p = package) { HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes,
            p + view.PayloadOffset, view.PayloadBytes, seed, 32, buffer, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var initial)); stack = initial.StackPointer; } }
        finally { NativeMemory.AlignedFree(buffer); }
        const ulong identity = 4294967297; ulong clock = 100, sequence = 0, request = 0, epochs = 0; int ordinal = 0, transfers = 0;
        List<string> rows = ["TEST_START m16-009",
            "M16_006_POLICY physical=536870912 available=268435456 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live",
            "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=3145728 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "TEST_START m16.023", "TEST_START m16.024", "TEST_START m16.025",
            "M16_023_BOUND owner=8388608 epoch=1 arena=9437184 pages=96 bar=4026531840 capacity=131072 bootstrapCompleted=40 bootstrapCopied=40 clock=1 pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained",
            $"M16_008_BOUND root=1048576 pool=4194304 workers=2 code={LinuxThreadProgram.Bytes} metadata={LinuxThreadProgram.MetadataPages * 4096} userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
            "M16_008_BUDGET started=1 epochs=8192 steps=16384 nanos=300000000000"];
        void Poll(ulong id) { rows.Add(FormattableString.Invariant($"M16_012_POLL identity={id} clock={clock++} ready={(id == 0 ? 1 : 0)} idle=0")); }
        void Supervisor(uint action) { transfers += 2; rows.Add(FormattableString.Invariant($"M16_008_SUPERVISOR identity={identity} action={action} transfers={transfers} ready={(action == 1 ? 1 : 0)} active=1")); }
        foreach (ulong rip in F.ExpectedReturnRips())
        {
            int end = checked((int)(rip - F.ImageBase)), cursor = end - 2; ulong a3 = 0;
            if (elf[cursor - 6] == 0x41 && elf[cursor - 5] == 0xba) { a3 = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(cursor - 4)); cursor -= 6; }
            uint Immediate(byte opcode) { HostCheck.Equal(opcode, elf[cursor - 5]); uint v = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(cursor - 4)); cursor -= 5; return v; }
            ulong a2 = Immediate(0xba), a1 = Immediate(0xbe), a0 = Immediate(0xbf), nr = Immediate(0xb8);
            bool exit = nr == 231; int compare = end + (nr == 17 ? 6 : 0);
            long value = exit ? 0 : BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(compare + 2));
            if (!exit) HostCheck.True(elf[compare] == 0x48 && elf[compare + 1] == 0x3d);
            bool pending = nr is 4 or 6 or 257 || nr is 0 or 8 or 17 && value is not (-9 or -22) || nr == 2 && value is not (-30 or -13 or -23);
            if (pending || nr == 2 && value is -13 or -23) request++;
            if (pending) sequence++; ordinal++; Poll(identity); Poll(identity);
            if (nr == 1) rows.Add($"M16_005_WRITE fd=1 bytes={F.Witness.Length} data={F.Witness}");
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={identity} nr={nr} a0={a0} a1={a1} a2={a2} a3={a3} a4=0 a5=0 rip={rip} rsp={stack} flags=582 fs=0 gs=0 clock={clock - 1} valueBits={unchecked((ulong)(pending ? 0 : value))} action={(pending ? 3 : exit ? 2 : 1)} status={(pending ? 9 : exit ? 2 : 1)} ordinal={ordinal} waitSource={(pending ? 4 : 0)} waitDeadline=0 waitSequence={(pending ? sequence : 0)} signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp={stack} returnFlags=582 returnFs=0 returnGs=0 returnValueBits={unchecked((ulong)(pending ? 0 : value))}"));
            if (auxiliary) rows.Add(FormattableString.Invariant($"M16_008_SYSCALL_TIME identity={identity} ordinal={ordinal} nr={nr} entered=1 admitted=2 dispatched=3 prepared=4 periodFs=1000000"));
            if (pending)
            {
                Supervisor(3);
                if (auxiliary && sequence == 1)
                {
                    rows.Add(FormattableString.Invariant($"M16_008_TICK epoch={++epochs} nanos={clock++} ready=0"));
                    rows.Add("M16_023_IO kind=0 owner=1 epoch=2 generation=3 sector=4 sequence=3 bytes=512 available=3 consumed=2 clock=100");
                }
                Poll(0); Poll(0);
                uint output = nr is 4 or 6 ? 144U : nr is 0 or 17 && a1 >= F.DataBase && a1 < F.ImageBase + F.ElfBytes ?
                    (uint)Math.Min(Math.Min(a2, 8192), F.ImageBase + F.ElfBytes - a1) : 0;
                rows.Add(FormattableString.Invariant($"M16_025_COMPLETE identity={identity} sequence={sequence} request={request} outputBytes={output} completion=1 valueBits={unchecked((ulong)value)} clock={clock++}"));
                rows.Add(FormattableString.Invariant($"M16_008_RESUME identity={identity} valueBits={unchecked((ulong)value)} status=1"));
            }
            if (auxiliary && ordinal == 4)
            {
                rows.Add(FormattableString.Invariant($"M16_009_IRQ identity={identity} epoch={++epochs} nanos={clock++} rip={rip} rsp={stack} flags=582 fs=0 gs=0 valueBits=0"));
                Supervisor(1); rows.Add(FormattableString.Invariant($"M16_008_TIMING identity={identity} epoch={epochs} start=1 arm=2 capture=3 ready=4 periodFs=1000000"));
            }
            if (exit) { rows.Add($"M16_008_EXIT identity={identity} clearTidResult=0"); Supervisor(2); }
        }
        rows.Add("M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero");
        for (uint fd = 0; fd < 3; fd++) rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        rows.Add(FormattableString.Invariant($"M16_008_BUDGET_END nanos={clock}"));
        rows.Add(FormattableString.Invariant($"M16_008_OWNER_END reaped=1 transfers={transfers} irqs={epochs} exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained"));
        rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        return rows.ToArray();
    }
    private static QemuTranscripts.LinuxDiskFileLeafReport Inspect(string[] rows, byte[]? package = null) =>
        QemuTranscripts.InspectLinuxDiskFileLeafForTests(rows, package ?? F.CreatePackage(), 0x200000, 0x100000);
    private static int[] Indexes(string[] rows, string prefix) => Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static ulong Field(string row, string key) => ulong.Parse(row.Split(' ').Single(f => f.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..], CultureInfo.InvariantCulture);
    private static string[] Change(string[] rows, int at, string key, ulong value)
    { string[] copy = (string[])rows.Clone(); copy[at] = string.Join(' ', copy[at].Split(' ').Select(f => f.StartsWith(key + "=", StringComparison.Ordinal) ? key + "=" + value.ToString(CultureInfo.InvariantCulture) : f)); return copy; }
    private static string[] Replace(string[] rows, int at, string value) { string[] copy = (string[])rows.Clone(); copy[at] = value; return copy; }
    private static void Reject(string[] rows) { bool rejected = false; try { Inspect(rows); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
    private sealed class Oversize : IReadOnlyList<string>
    {
        public int Count => 131073;
        public string this[int index] => throw new InvalidOperationException("must reject count first");
        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("must reject count first");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
