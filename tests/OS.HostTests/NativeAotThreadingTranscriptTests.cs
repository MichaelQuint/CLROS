using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static partial class NativeAotThreadingTranscriptTests
{
    private const ulong TailRip = 0x400287;
    // These are independent synthetic observations, never guest captures.
    private const string Suspension = "TEST_START nativeaot.suspension\nNATIVEAOT_SUSPENSION_GC epoch=1 before=1 after=2 progressBefore=10 progressReturned=12 progressAfter=13\n" + "NATIVEAOT_SUSPENSION_GC epoch=2 before=2 after=3 progressBefore=13 progressReturned=15 progressAfter=16\n" + "NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true\nTEST_PASS nativeaot.suspension\n";
    private const string Pool = "TEST_START nativeaot.threadpool\nNATIVEAOT_THREADPOOL_WORK tasks=4 completed=4 checksum=30 workers=3 threadPool=true\n" + "NATIVEAOT_THREADPOOL_CONTINUATIONS before=1 race=1 after=1 count=3 value=43 publicationInline=0\n" + "NATIVEAOT_THREADPOOL_CANCELLATION before=2 during=1 callbacks=0\n" + "NATIVEAOT_THREADPOOL_DELAY requestedMs=50 before=1000 after=1050 frequency=1000 minimumMs=25\n" + "NATIVEAOT_THREADPOOL_TIMER dueMs=50 before=1100 fired=1150 frequency=1000 callbacks=1 stale=0 disposed=true\n" + "NATIVEAOT_THREADPOOL_DRAINED tasks=4 continuations=3 callbacks=1 stale=0\nTEST_PASS nativeaot.threadpool\n";
    private static Vector SuspensionVector(bool earlyHandlerIrq = false, bool selfExit = false, bool tailIrq = false, bool requeue = false, bool restart = false, bool imageNoop = false, bool entropyFallback = false)
    {
        Vector v = new Vector(false).Start();
        Thread final = v.Clone(2, 2), signal = v.Clone(3, 3), worker = v.Clone(4, 4);
        v.Irq(10, 4, 4);
        v.Select(final);
        v.Wait(0x500200);
        v.Drain(20);
        v.Supervisor(3, 3, 4);
        v.Select(signal);
        v.Call(0, 0, [3, 0x500700, 1], 3, 2);
        v.Drain(30);
        v.Supervisor(3, 2, 4);
        v.Select(worker);
        v.Irq(40, 2, 4);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            v.Select(v.Main);
            if (restart && cycle == 1)
                v.Resume(0);
            if (imageNoop && cycle == 0)
            {
                v.Call(10, 0, [0x500000, 4096, 3]);
                v.Call(10, -12, [0x500000, 4096, 1]);
            }

            if (entropyFallback && cycle == 0)
            {
                v.Call(318, -38, [0x500600, 32, 0]);
                v.Call(318, -38, [0x500600, 32, 0]);
                v.Call(318, 16, [0x500600, 32, 1]);
                v.Call(318, -11, [0x500600, 8, 1]);
                v.Call(318, -38, [0x500600, 8, 0]);
            }

            v.Call(200, 0, [4, 35]);
            v.Wait(0x500204);
            v.Drain((ulong)(50 + 40 * cycle));
            v.Supervisor(3, 1, 4);
            v.Select(worker);
            worker.Frame = worker.Top - 1160;
            worker.Depth = 1;
            worker.Mask = 1UL << 34;
            worker.Rsp = worker.Frame - 64;
            if (earlyHandlerIrq && cycle == 0)
                v.Irq(55, 1, 4);
            v.Call(186, 4);
            if (requeue && cycle == 0)
            {
                v.Call(202, 1, [0x500200, 131, 0, 1, 0x500204]); // Final waiter moves behind Main.
                v.Call(202, 0, [0x500200, 129, 1]);
                v.Call(202, 2, [0x500204, 131, 0, int.MaxValue, 0x500204]); // Same-key does not reorder.
            }

            v.Wake(0x500204);
            v.Wait(0x500208);
            v.Drain((ulong)(60 + 40 * cycle));
            v.Supervisor(3, 1, 4);
            v.Select(v.Main);
            v.Resume(0);
            if (requeue && cycle == 0)
                v.Call(202, 1, [0x500204, 131, 0, 1, 0x500200]);
            v.Wake(0x500208);
            v.Irq((ulong)(70 + 40 * cycle), 2, 4);
            v.Select(worker);
            v.Resume(0);
            worker.Rsp = worker.Frame + 8;
            worker.Depth = worker.Frame = worker.Mask = 0;
            v.Call(15, 0, action: 5, returnedRsp: worker.Top - 64);
            worker.Rsp = worker.Top - 64;
            if (cycle == 0)
            {
                v.Irq(80, 2, 4);
                if (restart)
                {
                    v.Select(v.Main);
                    v.Irq(81, 2, 4);
                    v.Select(worker);
                    v.Wait(0x500240, 85);
                    v.Drain(82);
                    v.Supervisor(3, 1, 4);
                    v.Select(v.Main);
                    v.Call(202, 1, [0x500240, 131, 0, 1, 0x500244]);
                    v.Call(200, 0, [4, 35]);
                    v.Wait(0x500204);
                    v.Drain(83);
                    v.Supervisor(3, 1, 4);
                    v.Select(worker);
                    v.Resume(-4);
                    worker.Frame = worker.Top - 1160;
                    worker.Depth = 1;
                    worker.Mask = 1UL << 34;
                    worker.Rsp = worker.Frame - 64;
                    v.Call(186, 4);
                    v.Call(219, 0, [ulong.MaxValue, ulong.MaxValue, ulong.MaxValue], 3, 1, 85);
                    v.Drain(84);
                    v.Supervisor(3, 0, 4);
                    v.Tick(85, 1);
                    v.Select(worker);
                    v.Resume(-110);
                    v.Call(219, -4);
                    v.Wake(0x500204);
                    worker.Rsp = worker.Frame + 8;
                    worker.Depth = worker.Frame = worker.Mask = 0;
                    v.Call(15, -4, action: 5, returnedRsp: worker.Top - 64);
                    worker.Rsp = worker.Top - 64;
                    v.Irq(85, 2, 4);
                }
            }
        }

        if (selfExit)
        {
            v.Call(218, worker.Tid, [worker.Fs + 32]);
            worker.Mask = ~((1UL << 8) | (1UL << 18));
            v.Call(14, 0, [2, 0x500400, 0, 8]);
            v.Call(11, 0, [worker.Bottom, 0x20000], instructionRip: TailRip);
            if (tailIrq)
            {
                v.Irq(120, 2, 4, TailRip + 3, 0x246, direct: true);
            }
        }

        v.Call(60, 0, [0], 4, instructionRip: selfExit ? TailRip + 10 : null, flags: selfExit ? 0x246UL : 514);
        if (!tailIrq)
            v.Drain(120);
        v.Exit(4, 1, 4, selfExit ? -14 : 0);
        v.Select(v.Main);
        v.Output();
        v.Call(231, 0, [0], 2);
        v.Drain(120);
        v.Exit(2, 2, 3);
        v.Select(final);
        v.Exit(4, 1, 2);
        v.Select(signal);
        v.Exit(4, 0, 1);
        v.End(4);
        return v;
    }

    private static Vector PoolVector(bool reuse = false, bool workerClone = false, int retiredSignal = 0, bool requeue = false, bool yielded = false)
    {
        Vector v = new Vector(true).Start();
        Thread final = v.Clone(2, 2), signal = v.Clone(3, 3), first = v.Clone(4, 4), second = v.Clone(5, 5), gate = v.Clone(6, 6);
        v.Irq(10, 6, 6);
        v.Select(final);
        v.Wait(0x500200);
        v.Drain(20);
        v.Supervisor(3, 5, 6);
        v.Select(signal);
        v.Call(0, 0, [3, 0x500700, 1], 3, 2);
        v.Drain(30);
        v.Supervisor(3, 4, 6);
        v.Select(first);
        v.Wait(0x500210, 100);
        v.Drain(40);
        v.Supervisor(3, 3, 6);
        v.Select(second);
        v.Wait(0x500214, 100);
        v.Drain(50);
        v.Supervisor(3, 2, 6);
        v.Select(gate);
        if (yielded)
        {
            v.Call(24, 0, action: 6);
            v.Drain(55);
            v.Supervisor(6, 2, 6);
            v.Select(v.Main);
            v.Irq(56, 2, 6);
            v.Select(gate);
        }

        if (requeue)
            v.Call(202, 1, [0x500210, 131, 0, 1, 0x500214]); // Address FIFO changes; equal-deadline scheduler FIFO must not.
        if (retiredSignal == 1)
        {
            gate.Mask = 1UL << 34;
            v.Call(14, 0, [2, 0x500400, 0, 8]);
        }

        v.Wait(0x500230);
        v.Drain(60);
        v.Supervisor(3, 1, 6);
        v.Select(v.Main);
        v.Wait(0x500218, 100);
        v.Drain(70);
        v.Supervisor(3, 0, 6);
        v.Tick(99, 0);
        v.Tick(100, 3);
        v.Select(first);
        v.Resume(-110);
        v.Wake(0x500230);
        Thread? extra = workerClone ? v.Clone(7, 7) : null;
        int count = workerClone ? 7 : 6;
        v.Call(60, 0, [0], 4);
        v.Drain(100);
        v.Exit(4, workerClone ? 4 : 3, count);
        v.Select(second);
        v.Resume(-110);
        v.Call(60, 0, [0], 4);
        v.Drain(100);
        v.Exit(4, workerClone ? 3 : 2, --count);
        count--;
        v.Select(v.Main);
        v.Resume(-110);
        if (reuse)
        {
            extra = v.Clone(4, 7, 2);
            count++;
        }

        if (retiredSignal != 0)
            v.Call(200, 0, [6, 35]);
        if (retiredSignal == 2)
        {
            v.Irq(100, 2, count);
            v.Select(gate);
            v.Resume(0);
            gate.Frame = gate.Top - 1160;
            gate.Depth = 1;
            gate.Mask = 1UL << 34;
            gate.Rsp = gate.Frame - 64;
            v.Call(186, 6);
            v.Wait(0x500238);
            v.Drain(100);
            v.Supervisor(3, 1, count);
            v.Select(v.Main);
        }

        v.Output();
        v.Call(231, 0, [0], 2);
        v.Drain(100);
        v.Exit(2, extra == null ? 3 : 4, count--);
        if (retiredSignal == 2)
        {
            // Gate is parked again; group cancellation appends fresh waiters
            // by physical slot order, unlike an already-ready gate completion.
            v.Select(final);
            v.Exit(4, 2, count--);
            v.Select(signal);
            v.Exit(4, 1, count--);
            v.Select(gate);
            v.Exit(4, 0, count);
            v.End(6);
            return v;
        }

        v.Select(gate);
        v.Exit(4, extra == null ? 2 : 3, count--);
        if (extra != null)
        {
            v.Select(extra);
            v.Exit(4, 2, count--);
        }

        v.Select(final);
        v.Exit(4, 1, count--);
        v.Select(signal);
        v.Exit(4, 0, count);
        v.End(extra == null ? 6 : 7);
        return v;
    }

    internal static byte[] Elf(bool tail = false)
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(136), 0x500000);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(160), 0x40000);
        if (tail)
            new byte[]
            {
                0xb8,
                11,
                0,
                0,
                0,
                0x0f,
                5,
                0x48,
                0x31,
                0xff,
                0xb8,
                60,
                0,
                0,
                0,
                0x0f,
                5
            }.CopyTo(elf, 0x280);
        return elf;
    }

    private static QemuTranscripts.ThreadingProtocolReport Inspect(Vector v, string[]? rows = null, Func<ulong, bool>? code = null, bool tail = false, byte[]? image = null) => QemuTranscripts.InspectNativeAotThreadingProtocolForTests(v.Pool ? "NativeAotThreadPoolSmoke" : "NativeAotSuspensionSmoke", rows ?? v.Rows.ToArray(), image ?? Elf(tail), code ?? (r => r == 0x400202 || tail && (r == TailRip - 7 || r == TailRip || r == TailRip + 3 || r == TailRip + 8 || r == TailRip + 10)), r => r == 0x400202 || tail && (r == TailRip || r == TailRip + 10), v.Lengths, 0x2000, 0x3000);
    private static string[] Change(Vector v, string selector, string field, ulong value)
    {
        var rows = v.Rows.ToArray();
        int at = Array.FindIndex(rows, r => r.Contains(selector, StringComparison.Ordinal));
        HostCheck.True(at >= 0);
        rows[at] = Regex.Replace(rows[at], @"\b" + field + "=[0-9]+", field + "=" + value.ToString(CultureInfo.InvariantCulture));
        return rows;
    }

    private static void Reject(Vector v, string[] rows, bool tail = false, byte[]? image = null)
    {
        bool rejected = false;
        try
        {
            Inspect(v, rows, tail: tail, image: image);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }

        HostCheck.True(rejected, "runtime synthetic mutation admitted");
    }

    [HostTest("nativeaot.threading requeue moves address FIFO and borrows without inventing ready workers or changing deadline FIFO")]
    private static void Requeue()
    {
        var v = SuspensionVector(requeue: true);
        HostCheck.True(Inspect(v).SignalReturns == 2);
        var timed = PoolVector(requeue: true);
        HostCheck.True(Inspect(timed).Reaped == 6);
        string move = "a0=5243392 a1=131";
        Reject(v, Change(v, move, "valueBits", 2));
        Reject(v, Change(v, move, "a2", 1));
        Reject(v, Change(v, move, "a3", 0));
        Reject(v, Change(v, move, "a3", uint.MaxValue));
        Reject(v, Change(v, move, "a4", 0x500208));
        Reject(v, Change(v, move, "a4", 0x500205));
        Reject(v, Change(v, move, "a1", 3));
        Reject(v, Change(v, "a0=5243396 a1=131", "valueBits", 1));
        Reject(timed, Change(timed, "a0=5243408 a1=131", "valueBits", 0));
    }

    [HostTest("nativeaot.threading timed restart consumes the original requeued wait deadline once inside a real handler")]
    private static void Restart()
    {
        var v = SuspensionVector(restart: true);
        HostCheck.Equal(3, Inspect(v).SignalReturns);
        Reject(v, Change(v, "nr=219 ", "waitDeadline", 86));
        Reject(v, Change(v, "nr=219 ", "waitSequence", 1));
        Reject(v, Change(v, "nr=219 ", "valueBits", unchecked((ulong)-110L)));
        Reject(v, Change(v, "nr=219 ", "waitSource", 2));
        Reject(v, Change(v, "nr=219 ", "nr", 202));
        Reject(v, Change(v, "nr=219 ", "clock", 86));
    }

    [HostTest("nativeaot.threading self-unmap tail requires exact ELF instructions masked owner IRQ continuation and EFAULT clear-TID")]
    private static void ExitTail()
    {
        Vector plain = SuspensionVector(selfExit: true), irq = SuspensionVector(selfExit: true, tailIrq: true);
        HostCheck.True(Inspect(plain, tail: true).Reaped == 4 && Inspect(irq, tail: true).Transfers == 30);
        Reject(plain, Change(plain, "nr=11 ", "rip", 0x400202), true);
        Reject(plain, Change(plain, "nr=11 ", "a1", 0x18000), true);
        Reject(plain, Change(plain, "nr=11 ", "a1", 0x20001), true);
        Reject(plain, Change(plain, "nr=11 ", "blockedSignals", 0), true);
        Reject(plain, Change(plain, "nr=60 ", "nr", 39), true);
        Reject(plain, Change(plain, "nr=60 ", "flags", 0x202), true);
        Reject(plain, Change(plain, "clearTidResult=", "clearTidResult", 0), true);
        Reject(irq, Change(irq, $"rip={TailRip + 3} ", "rip", TailRip + 4), true);
        Reject(irq, Change(irq, $"rip={TailRip + 3} ", "flags", 0x202), true);
        var afterMov = Change(irq, $"rip={TailRip + 3} ", "rip", TailRip + 8);
        int afterMovIrq = Array.FindIndex(afterMov, r => r.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) && r.Contains($"rip={TailRip + 8} ", StringComparison.Ordinal));
        Reject(irq, afterMov, true);
        afterMov[afterMovIrq] = Regex.Replace(afterMov[afterMovIrq], @"\bvalueBits=[0-9]+", "valueBits=60");
        HostCheck.Equal(30, Inspect(irq, afterMov, tail: true).Transfers);
        var yielded = irq.Rows.ToList();
        int tailAt = yielded.FindIndex(r => r.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) && r.Contains($"rip={TailRip + 3} ", StringComparison.Ordinal));
        yielded.Insert(tailAt + 1, "M16_008_SUPERVISOR identity=4294967300 action=1 transfers=26 ready=2 active=4");
        Reject(irq, yielded.ToArray(), true);
        int unmap = irq.Rows.FindIndex(r => r.Contains("nr=11 ", StringComparison.Ordinal));
        ulong fs = ulong.Parse(Regex.Match(irq.Rows[unmap], @"\bfs=([0-9]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var forged = irq.Rows.Select((r, i) => i < unmap ? r : Regex.Replace(r, @"\b(fs|returnFs)=" + fs + @"\b", m => m.Groups[1].Value + "=" + (fs + 8).ToString(CultureInfo.InvariantCulture))).ToArray();
        Reject(irq, forged, true);
        byte[] changed = Elf(true);
        changed[0x28b] = 231;
        Reject(plain, plain.Rows.ToArray(), true, changed);
        bool rejected = false;
        try
        {
            Inspect(plain, code: r => r == 0x400202 || r == TailRip - 7 || r == TailRip || r == TailRip + 8 || r == TailRip + 10, tail: true);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }

        HostCheck.True(rejected, "partial instruction index cannot admit tail");
    }

    [HostTest("nativeaot.threading complete synthetic lifetimes bind two signal frames and ordered timed idle wakes")]
    private static void Complete()
    {
        var suspension = Inspect(SuspensionVector());
        HostCheck.True(suspension.Clones == 3 && suspension.Reaped == 4 && suspension.SignalReturns == 2 && suspension.Transfers == 30);
        HostCheck.True(Inspect(SuspensionVector(earlyHandlerIrq: true)).Transfers == 32);
        var pool = Inspect(PoolVector());
        HostCheck.True(pool.Clones == 5 && pool.Reaped == 6 && pool.Ticks == 2 && pool.Transfers == 26);
        HostCheck.True(Inspect(PoolVector(reuse: true)).Reaped == 7 && Inspect(PoolVector(workerClone: true)).Clones == 6);
        HostCheck.True(Inspect(PoolVector(retiredSignal: 1)).SignalReturns == 0 && Inspect(PoolVector(retiredSignal: 2)).SignalReturns == 0);
    }

    [HostTest("nativeaot.threading sched_yield requires actual captured FIFO drain and cannot fabricate a wait or return-only success")]
    private static void YieldProtocol()
    {
        var v = PoolVector(yielded: true);
        var report = Inspect(v);
        HostCheck.True(report.Transfers == 30 && report.Irqs == 2);
        Reject(v, Change(v, "nr=24 ", "action", 1));
        Reject(v, Change(v, "nr=24 ", "waitSource", 1));
        Reject(v, Change(v, "action=6 transfers=", "ready", 1));
        int at = v.Rows.FindIndex(r => r.Contains("nr=24 ", StringComparison.Ordinal));
        var rows = v.Rows.ToArray();
        rows[at] = Regex.Replace(rows[at], @"\b(valueBits|returnValueBits)=[0-9]+", m => m.Groups[1].Value + "=" + unchecked((ulong)-38L).ToString(CultureInfo.InvariantCulture));
        Reject(v, rows);
        Reject(v, v.Rows.Where((_, i) => i != at + 1).ToArray());
    }

    [HostTest("nativeaot.threading optional HPET boundary diagnostics bind only to the completed IRQ owner and ordered samples")]
    private static void TimingDiagnostics()
    {
        var v = PoolVector();
        int at = v.Rows.FindIndex(r => r.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal));
        const string timing = "M16_008_TIMING identity=4294967297 epoch=1 start=100 arm=200 capture=300 ready=400 periodFs=10000000";
        v.Rows.Insert(at + 1, timing);
        HostCheck.True(Inspect(v).Reaped == 6);
        Reject(v, Change(v, "M16_008_TIMING ", "identity", 4294967298));
        Reject(v, Change(v, "M16_008_TIMING ", "epoch", 2));
        Reject(v, Change(v, "M16_008_TIMING ", "arm", 99));
        Reject(v, Change(v, "M16_008_TIMING ", "capture", 199));
        Reject(v, Change(v, "M16_008_TIMING ", "ready", 299));
        Reject(v, Change(v, "M16_008_TIMING ", "periodFs", 0));
        Reject(v, Change(v, "M16_008_TIMING ", "periodFs", 1_000_000_001));
        v.Rows.RemoveAt(at + 1);
        int blocked = v.Rows.FindIndex(r => r.Contains("action=3 transfers=", StringComparison.Ordinal));
        v.Rows.Insert(blocked + 1, timing);
        Reject(v, v.Rows.ToArray());
    }

    [HostTest("nativeaot.threading syscall timing diagnostics bind to the actual generation ordinal and syscall without replacing evidence")]
    private static void SyscallTimingDiagnostics()
    {
        var v = PoolVector();
        int at = v.Rows.FindIndex(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal));
        string nr = Regex.Match(v.Rows[at], @"\bnr=([0-9]+)").Groups[1].Value;
        string timing = $"M16_008_SYSCALL_TIME identity=4294967297 ordinal=1 nr={nr} entered=100 admitted=200 dispatched=300 prepared=400 periodFs=10000000";
        v.Rows.Insert(at + 1, timing);
        HostCheck.True(Inspect(v).Reaped == 6);
        foreach (var mutation in new[]
        {
            Change(v, "M16_008_SYSCALL_TIME ", "identity", 4294967298),
            Change(v, "M16_008_SYSCALL_TIME ", "ordinal", 2),
            Change(v, "M16_008_SYSCALL_TIME ", "nr", 999),
            Change(v, "M16_008_SYSCALL_TIME ", "admitted", 99),
            Change(v, "M16_008_SYSCALL_TIME ", "dispatched", 199),
            Change(v, "M16_008_SYSCALL_TIME ", "prepared", 299),
            Change(v, "M16_008_SYSCALL_TIME ", "periodFs", 0),
            v.Rows.Where((_, i) => i != at).ToArray()
        }

        )
            Reject(v, mutation);
        v.Rows.Insert(at + 2, timing);
        Reject(v, v.Rows.ToArray());
    }

    [HostTest("nativeaot.threading synthetic scheduler rejects premature deadlines wrong FIFO resumes and forged owner cleanup")]
    private static void SchedulerFailures()
    {
        var v = PoolVector();
        _ = Inspect(v);
        foreach (var rows in new[]
        {
            Change(v, "M16_008_TICK epoch=8 ", "ready", 1),
            Change(v, "M16_008_TICK epoch=9 ", "nanos", 99),
            Change(v, "M16_008_RESUME ", "valueBits", 0),
            Change(v, "waitDeadline=100 ", "waitDeadline", 30),
            Change(v, "M16_008_SUPERVISOR ", "ready", 5),
            Change(v, "M16_008_REAP ", "next", 0x100000004),
            Change(v, "M16_008_OWNER_END ", "reaped", 5),
            Change(v, "nr=56 ", "valueBits", 3),
            v.Rows.Select(r => r.Replace("initialStack=zero", "initialStack=dirty", StringComparison.Ordinal)).ToArray(),
            v.Rows.Concat(new[] { "extra" }).ToArray()
        }

        )
            Reject(v, rows);
        for (int i = 0; i < v.Rows.Count; i++)
            if (v.Rows[i].StartsWith("M16_008_RESUME ", StringComparison.Ordinal) || v.Rows[i].StartsWith("M16_008_REAP ", StringComparison.Ordinal))
                Reject(v, v.Rows.Where((_, at) => at != i).ToArray());
    }

    [HostTest("nativeaot.threading small application profiles require real bounded time without changing worker capacity")]
    private static void ApplicationBudget()
    {
        foreach (var v in new[]
        {
            SuspensionVector(),
            PoolVector()
        }

        )
        {
            _ = Inspect(v);
            foreach (var prefix in new[]
            {
                "M16_008_BUDGET ",
                "M16_008_BUDGET_END "
            }

            )
                Reject(v, v.Rows.Where(r => !r.StartsWith(prefix, StringComparison.Ordinal)).ToArray());
            Reject(v, Change(v, "M16_008_BUDGET ", "started", 11));
            Reject(v, Change(v, "M16_008_BUDGET ", "epochs", 128));
            Reject(v, Change(v, "M16_008_BUDGET ", "steps", 512));
            Reject(v, Change(v, "M16_008_BUDGET_END ", "nanos", 300_000_000_000));
            Reject(v, Change(v, "M16_008_BUDGET_END ", "nanos", v.Clock - 1));
            Reject(v, v.Rows.Select(r => r.Replace("poolPages=8192", "poolPages=4096", StringComparison.Ordinal)).ToArray());
        }

        var borrowed = SuspensionVector(imageNoop: true);
        _ = Inspect(borrowed);
        Reject(borrowed, Change(borrowed, "nr=10 a0=5242880 a1=4096 a2=3 ", "valueBits", unchecked((ulong)-12L)));
        Reject(borrowed, Change(borrowed, "nr=10 a0=5242880 a1=4096 a2=1 ", "valueBits", 0));
    }

    [HostTest("nativeaot.threading finite entropy refusals preserve remaining bytes and partial reads cannot fabricate success or refill")]
    private static void EntropyFallback()
    {
        var v = SuspensionVector(entropyFallback: true);
        _ = Inspect(v);
        const string random = "nr=318 a0=5244416 ";
        void Refuse(string selector, ulong result)
        {
            var rows = v.Rows.ToArray();
            int at = Array.FindIndex(rows, r => r.Contains(selector, StringComparison.Ordinal));
            HostCheck.True(at >= 0, "entropy mutation selector");
            rows[at] = Regex.Replace(rows[at], @"\b(valueBits|returnValueBits)=[0-9]+", m => m.Groups[1].Value + "=" + result.ToString(CultureInfo.InvariantCulture));
            bool rejected = false;
            try
            {
                Inspect(v, rows);
            }
            catch (ToolFailure failure)
            {
                rejected = failure.Reason == "threading_entropy_result";
            }

            HostCheck.True(rejected, "entropy semantics rejected by wrong control");
        }

        Refuse(random + "a1=32 a2=0 ", 32);
        Reject(v, Change(v, random + "a1=32 a2=0 ", "a2", 1));
        Refuse(random + "a1=32 a2=1 ", 32);
        Refuse(random + "a1=8 a2=1 ", 8);
        Refuse(random + "a1=8 a2=0 ", unchecked((ulong)-11L));
    }

    [HostTest("nativeaot.threading synthetic signals require actual frame ownership restored context and pending target generation")]
    private static void SignalFailures()
    {
        var v = SuspensionVector();
        _ = Inspect(v);
        foreach (var rows in new[]
        {
            Change(v, "nr=200 ", "a0", 3),
            Change(v, "nr=186 ", "signalFrame", 0x500008),
            Change(v, "nr=15 ", "returnRsp", 0x6ffffff0),
            Change(v, "nr=15 ", "returnFs", 0),
            Change(v, "nr=15 ", "returnFlags", 2),
            Change(v, "nr=15 ", "signalDepth", 1),
            Change(v, "nr=15 ", "rsp", 0x500008),
            Change(v, "nr=15 ", "returnValueBits", 1),
            Change(v, "nr=200 ", "blockedSignals", 1UL << 8),
            Change(v, " a1=129 ", "valueBits", 0)
        }

        )
            Reject(v, rows);
        bool denied = false;
        try
        {
            Inspect(v, code: _ => false);
        }
        catch (ToolFailure)
        {
            denied = true;
        }

        HostCheck.True(denied);
    }

    [HostTest("nativeaot.threading synthetic call admission rejects missing current witnesses output pairing and alias geometry")]
    private static void InputFailures()
    {
        var v = PoolVector();
        _ = Inspect(v);
        foreach (var rows in new[]
        {
            Change(v, "nr=1 ", "a2", 1),
            Change(v, "nr=1 ", "valueBits", 0),
            Change(v, "M16_008_BOUND ", "pool", 0xfff98000),
            Change(v, "M16_008_BOUND ", "pool", 0x3000),
            Change(v, "M16_008_BOUND ", "workers", 4),
            Change(v, "M16_009_IRQ ", "fs", 0),
            Change(v, "M16_009_IRQ ", "epoch", 2),
            Change(v, "nr=56 ", "returnRsp", 0x600000),
            v.Rows.Select(r => Regex.Replace(r, " signalDepth=.*$", "")).ToArray(),
            v.Rows.Select(r => Regex.Replace(r, " waitSource=.*$", "")).ToArray()
        }

        )
            Reject(v, rows);
        foreach (int at in Enumerable.Range(0, v.Rows.Count).Where(i => v.Rows[i].StartsWith("M16_005_WRITE", StringComparison.Ordinal)))
            Reject(v, v.Rows.Where((_, i) => i != at).ToArray());
    }
}
