using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static class LinuxHelloTranscriptTests
{
    [HostTest("nativeaot.hello complete pool envelope rejects old geometry physical overflow and late pool root overlap")]
    private static void PoolEnvelope()
    {
        var rows = Build();
        _ = Inspect(Change(rows, "M16_008_BOUND ", "pool", 0xfff1e000));
        Reject(Change(rows, "M16_008_BOUND ", "pool", 0xfff1f000));
        Reject(Change(rows, "M16_008_BOUND ", "pool", 0xfff5e000));
        Reject(Change(rows, "M16_008_BOUND ", "code", 16384));
        Reject(Change(rows, "M16_008_BOUND ", "metadata", 45056));
        var overlap = Change(Change(rows, "M16_008_BOUND ", "root", 0x300000), "M16_008_BOUND ", "pool", 0x29c000);
        bool rejected = false;
        try
        {
            _ = QemuTranscripts.InspectLinuxHelloProtocolForTests(overlap, Elf(), r => r == 0x400202, r => r == 0x400202, 0x2000, 0x300000);
        }
        catch (ToolFailure failure)
        {
            rejected = failure.Reason == "hello_roots_pool_alias";
        }
        HostCheck.True(rejected);
    }

    [HostTest("nativeaot.hello protocol models genuine child waits FIFO IRQ wake exit-group and three off-stack reaps")]
    private static void CompleteProtocol()
    {
        var rows = Build();
        var report = Inspect(rows);
        HostCheck.True(report.Calls > 30 && report.Irqs == 1 && report.Drains == 4 && report.Transfers == 14);
        // Intentionally nonzero unused registers must not be treated as ABI inputs.
        HostCheck.True(rows.Any(r => r.Contains("nr=39 a0=4660", StringComparison.Ordinal)));
        var wakeFirst = Inspect(Build(wakeBeforeWait: true));
        HostCheck.True(wakeFirst.Calls == report.Calls && wakeFirst.Irqs == 2 && wakeFirst.Drains == 3 && wakeFirst.Transfers == 14);
        var uncontended = Build(uncontended: true);
        var direct = Inspect(uncontended);
        HostCheck.True(direct.Calls == report.Calls - 2 && direct.Irqs == 2 && direct.Drains == 3 && direct.Transfers == 14);
        Reject(uncontended.Where(r => !r.Contains("identity=4294967298 nr=202 ", StringComparison.Ordinal)).ToArray());
        Reject(uncontended.Where(r => !r.Contains("identity=4294967299 nr=0 ", StringComparison.Ordinal)).ToArray());
    }

    [HostTest("nativeaot.hello stdout can precede the signal child timeslice but exit requires its actual mask and pipe wait")]
    private static void LateSignalChild()
    {
        foreach (int early in new[] { 1, 2, 3 })
        {
            var rows = Build(earlyWrites: early);
            var report = Inspect(rows);
            HostCheck.Equal(14, report.Transfers);
            Reject(rows.Where(r => !r.Contains("identity=4294967299 nr=14 ", StringComparison.Ordinal)).ToArray());
            Reject(rows.Where(r => !r.Contains("identity=4294967299 nr=0 ", StringComparison.Ordinal)).ToArray());
        }
        Reject(Build(earlyWrites: 3, prematureExit: true), reason: "hello_runtime_children_and_startup");
    }

    [HostTest("nativeaot.hello optional syscall timings require the actual complete call and ordered raw samples")]
    private static void SyscallTimingDiagnostics()
    {
        var rows = Build().ToList();
        int at = rows.FindIndex(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal));
        string nr = Regex.Match(rows[at], @"\bnr=([0-9]+)").Groups[1].Value;
        string timing = $"M16_008_SYSCALL_TIME identity=4294967297 ordinal=1 nr={nr} entered=100 admitted=200 dispatched=300 prepared=400 periodFs=10000000";
        rows.Insert(at + 1, timing);
        _ = Inspect(rows.ToArray());
        foreach (var mutation in new[]{Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","identity",4294967298),
            Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","ordinal",2),Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","nr",999),
            Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","admitted",99),Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","dispatched",199),
            Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","prepared",299),Change(rows.ToArray(),"M16_008_SYSCALL_TIME ","periodFs",0),
            rows.Where((_,i)=>i!=at).ToArray()})
            Reject(mutation);
        rows.Insert(at + 2, timing);
        Reject(rows.ToArray());
    }

    [HostTest("nativeaot.hello optional timing rows cannot stand in for the required IRQ or alter its identity")]
    private static void TimingDiagnostics()
    {
        var rows = Build(wakeBeforeWait: true).ToList();
        int at = rows.FindIndex(r => r.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal));
        rows.Insert(at + 1, "M16_008_TIMING identity=4294967297 epoch=1 start=100 arm=200 capture=300 ready=400 periodFs=10000000");
        _ = Inspect(rows.ToArray());
        Reject(Change(rows.ToArray(), "M16_008_TIMING ", "epoch", 2));
        rows.RemoveAt(at - 1);
        Reject(rows.ToArray());
    }

    [HostTest("nativeaot.hello application budget requires exact bounds fresh clocks and both lifetime records")]
    private static void BudgetRecords()
    {
        var rows = Build();
        _ = Inspect(Change(rows, "M16_008_BUDGET_END ", "nanos", 300000000000)); // One ns before the deadline.
        foreach (var pair in new[] { ("epochs",128UL), ("epochs",8191UL), ("epochs",8193UL),
            ("steps",512UL), ("steps",16383UL), ("steps",16385UL),
            ("nanos",299999999999UL), ("nanos",300000000001UL), ("started",2UL), ("started",ulong.MaxValue) })
            Reject(Change(rows, "M16_008_BUDGET ", pair.Item1, pair.Item2));
        foreach (ulong nanos in new[] { 0UL, 5UL, 300000000001UL, ulong.MaxValue })
            Reject(Change(rows, "M16_008_BUDGET_END ", "nanos", nanos));
        Reject(Change(rows, "M16_009_IRQ ", "nanos", 300000000001));
        Reject(Change(Build(wakeBeforeWait: true), "M16_009_IRQ ", "nanos", 0));
        Reject(Change(rows, "M16_006_POLICY ", "poolPages", 4096));
        foreach (string prefix in new[] { "M16_008_BUDGET ", "M16_008_BUDGET_END " })
        {
            Reject(rows.Where(r => !r.StartsWith(prefix, StringComparison.Ordinal)).ToArray());
            var duplicate = rows.ToList();
            int at = duplicate.FindIndex(r => r.StartsWith(prefix, StringComparison.Ordinal));
            duplicate.Insert(at, duplicate[at]);
            Reject(duplicate.ToArray());
        }
    }

    [HostTest("nativeaot.hello four-worker application accepts observed IRQs beyond the mechanical ceiling")]
    private static void ApplicationIrqs()
    {
        const int extra = 128;
        var rows = Build().ToList();
        string Add(string row, string field, ulong amount) => Regex.Replace(row, @"\b" + field + "=([0-9]+)",
            m => field + "=" + (ulong.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + amount).ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < rows.Count; i++)
        {
            string row = rows[i];
            if (row.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
                row = Add(row, "clock", extra);
            if (row.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) || row.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal))
                row = Add(Add(row, "epoch", extra), "nanos", extra);
            if (row.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal) || row.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal))
                row = Add(row, "transfers", extra * 2);
            if (row.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal))
                row = Add(row, "irqs", extra);
            if (row.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal))
                row = Add(row, "nanos", extra);
            rows[i] = row;
        }
        List<string> initial = [];
        for (int i = 1; i <= extra; i++)
        {
            initial.Add($"M16_009_IRQ identity=4294967297 epoch={i} nanos={i} rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 valueBits=0");
            initial.Add($"M16_008_SUPERVISOR identity=4294967297 action=1 transfers={i * 2} ready=1 active=1");
        }
        rows.InsertRange(rows.FindIndex(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal)), initial);
        var report = Inspect(rows.ToArray());
        HostCheck.True(report.Irqs == 129 && report.Drains == 4 && report.Transfers == 270);
    }

    [HostTest("nativeaot.hello image permissions preserve original ELF data and reject code stack gaps and competing windows")]
    private static void ImagePermissions()
    {
        static ulong Bits(long value) => unchecked((ulong)value);
        var rows = Build(extraCalls: [
            (228,Bits(-14),[6,0x533000]),
            (10,0,[0x533000,1,3]), (228,0,[6,0x533000]), (10,0,[0x533000,4096,3]),
            (10,Bits(-22),[0x400000,4096,3]), // Original executable code.
            (10,Bits(-22),[0x6fe80000,4096,1]), // Initial stack is not ELF data.
            (10,Bits(-22),[0x480000,4096,3]), // Image envelope gap.
            (10,Bits(-22),[0x532000,8192,3]), // Two original ELF rows.
            (10,Bits(-22),[0x533000,4096,7]), // W+X.
            (10,Bits(-12),[0x501000,4096,1]), // Competing valid data window.
            (10,0,[0x533000,4096,1]), (228,Bits(-14),[6,0x533000]),
            (10,0,[0x501000,8192,1]), (228,Bits(-14),[1,0x501000]),
            (10,Bits(-12),[0x501000,4096,3]), (10,Bits(-12),[0x502000,8192,3]),
            (10,0,[0x501000,8192,3]), (228,0,[1,0x501000]),
            (10,0,[0x533000,4096,3]), (10,0,[0x533000,4096,1]), (228,Bits(-14),[6,0x533000])]);
        _ = Inspect(rows);
        foreach (string selector in new[] { "nr=10 a0=4194304 ","nr=10 a0=1877475328 ","nr=10 a0=4718592 ",
            "nr=10 a0=5447680 ","nr=10 a0=5451776 a1=4096 a2=7 ","nr=10 a0=5246976 a1=4096 ",
            "nr=10 a0=5251072 a1=8192 ","nr=228 a0=6 a1=5451776 " })
            Reject(Change(rows, selector, "valueBits", 0));
        Reject(Change(rows, "nr=10 a0=5451776 a1=1 ", "valueBits", Bits(-22)));
        // Restoring the wrong original protection cannot authorize later writes.
        Reject(Change(rows, "nr=10 a0=5451776 a1=4096 a2=1 ", "a2", 3));
    }

    [HostTest("nativeaot.hello monotonic and coarse clocks require real result codes and writable output")]
    private static void ClockResults()
    {
        var rows = Build(extraCalls: [(228,0,[1,0x500100]),(228,0,[6,0x500100]),
            (228,unchecked((ulong)-22L),[0,0x500100]),(228,unchecked((ulong)-22L),[7,0x500100])]);
        _ = Inspect(rows);
        Reject(Change(rows, "nr=228 a0=6 ", "valueBits", unchecked((ulong)-22L)));
        Reject(Change(rows, "nr=228 a0=6 ", "a1", 0x400000));
        Reject(Change(rows, "nr=228 a0=0 ", "valueBits", 0));
        Reject(Change(rows, "nr=228 a0=7 ", "valueBits", 0));
    }

    [HostTest("nativeaot.hello protocol rejects invented wait success wrong wake duplicate child and counterfeit scheduler accounting")]
    private static void ScheduleNegatives()
    {
        string[] rows = Build();
        Reject(Change(rows, "nr=202 ", "action", 1));
        Reject(Change(rows, "nr=202 ", "valueBits", unchecked((ulong)-11L)));
        Reject(Change(rows, " a1=129 ", "valueBits", 0));
        Reject(Change(rows, "nr=56 ", "valueBits", 3));
        Reject(Change(rows, "M16_008_RESUME ", "valueBits", unchecked((ulong)-11L)));
        Reject(Change(rows, "M16_008_SUPERVISOR ", "ready", 2));
        Reject(Change(rows, "M16_008_SUPERVISOR ", "transfers", 4));
        Reject(Change(rows, "M16_008_REAP ", "next", 0x100000001));
        var lateResume = rows.ToList();
        int cancel = lateResume.FindLastIndex(r => r.StartsWith("M16_008_EXIT ", StringComparison.Ordinal));
        lateResume.Insert(cancel, "M16_008_RESUME identity=4294967299 valueBits=1 status=1");
        Reject(lateResume.ToArray());
    }

    [HostTest("nativeaot.hello protocol rejects ABI memory FS instruction raw output pairing and incomplete cleanup mutations")]
    private static void ProtocolNegatives()
    {
        string[] rows = Build();
        foreach (var mutation in new[] {
            Change(rows,"nr=158 ","a0",0x1001), Change(rows,"nr=218 ","a0",0x53a374),
            Change(rows,"nr=56 ","a0",0x7d0f01), Change(rows,"nr=56 ","a4",0x100000120),
            Change(rows,"nr=293 ","a1",0), Change(rows,"nr=1 ","a2",26), Change(rows,"nr=231 ","a0",42),
            Change(rows,"nr=318 ","valueBits",0), Change(rows,"nr=324 ","valueBits",0),
            Change(rows,"M16_009_IRQ ","fs",0), Change(rows,"M16_009_IRQ ","flags",2),
            Change(rows,"M16_009_IRQ ","epoch",7), Change(rows,"M16_008_OWNER_END ","reaped",2),
            rows.Select(r=>r.Replace("initialStack=zero","initialStack=dirty",StringComparison.Ordinal)).ToArray(),
            rows.Select(r=>r.Replace("userBacked=0","userBacked=1",StringComparison.Ordinal)).ToArray(),
            rows.Where(r=>!r.StartsWith("M16_005_WRITE fd=1 bytes=19",StringComparison.Ordinal)).ToArray(),
            rows.Concat(new[]{"CLROS_M16_009_READY"}).ToArray() })
            Reject(mutation);
        Reject(rows, instruction: _ => false);
        Reject(rows, syscall: _ => false);
    }

    private static QemuTranscripts.HelloProtocolReport Inspect(string[] rows, Func<ulong, bool>? instruction = null, Func<ulong, bool>? syscall = null) =>
        QemuTranscripts.InspectLinuxHelloProtocolForTests(rows, Elf(), instruction ?? (r => r == 0x400202), syscall ?? (r => r == 0x400202), 0x2000, 0x3000);

    private static void Reject(string[] rows, Func<ulong, bool>? instruction = null, Func<ulong, bool>? syscall = null, string? reason = null)
    {
        bool rejected = false;
        try
        {
            Inspect(rows, instruction, syscall);
        }
        catch (ToolFailure failure)
        {
            rejected = reason is null || failure.Reason == reason;
        }
        HostCheck.True(rejected, "hello synthetic mutation accepted or wrong rejection");
    }

    private static string[] Change(string[] rows, string selector, string key, ulong value)
    {
        string[] copy = rows.ToArray();
        int at = Array.FindIndex(copy, r => r.Contains(selector, StringComparison.Ordinal));
        HostCheck.True(at >= 0, "mutation selector");
        copy[at] = Regex.Replace(copy[at], @"\b" + key + "=[0-9]+", key + "=" + value.ToString(CultureInfo.InvariantCulture));
        return copy;
    }

    private static byte[] Elf()
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(136), 0x500000); // Independent enlarged static RW/BSS range.
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(160), 0x33000);
        // Preserve the RX load, split data around an original readonly cookie
        // page, and retain RW clear-TID/futex storage above it.
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(56), 5);
        void Load(int at, uint flags, ulong address, ulong bytes)
        {
            elf.AsSpan(at, 56).Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at + 4), flags);
            BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at + 16), address);
            BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at + 24), address);
            BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at + 40), bytes);
            BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at + 48), 4096);
        }
        Load(176, 4, 0x533000, 4096);
        Load(232, 6, 0x534000, 0xc000);
        elf.AsSpan(288, 56).Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(288), 0x6474e551);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(292), 6);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(336), 16);
        return elf;
    }

    private static string[] Build(bool wakeBeforeWait = false, (ulong Nr, ulong Value, ulong[] Args)[]? extraCalls = null, bool uncontended = false, int earlyWrites = 0, bool prematureExit = false)
    {
        const ulong tc = 0x100000000, one = 0x100010000, two = 0x100300000;
        const ulong fs1 = one + 0x183000 - 0x4c8, fs2 = two + 0x183000 - 0x4c8;
        const ulong top1 = one + 0x183000 - 0x618, top2 = two + 0x183000 - 0x618;
        List<string> rows = ["TEST_START m16-009",
            "M16_006_POLICY physical=268435456 available=134217728 poolPages=8192 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=1241768 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_005_ARMED root=8192 cs=59 ss=51 kernelStack=1048576 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "M16_008_BOUND root=12288 pool=2097152 workers=4 code=53248 metadata=114688 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
            "M16_008_BUDGET started=1 epochs=8192 steps=16384 nanos=300000000000"];
        int tid = 1;
        ulong clock = 0, fs = 0, rsp = 0x6ffffff0;
        void Call(ulong nr, ulong value, ulong[] args, ulong action = 1, ulong status = 1)
        {
            ulong[] a = Enumerable.Repeat(0x1234UL, 6).ToArray();
            args.CopyTo(a, 0);
            rows.Add($"M16_008_CALL identity={0x100000000UL + (uint)tid} nr={nr} a0={a[0]} a1={a[1]} a2={a[2]} a3={a[3]} a4={a[4]} a5={a[5]} rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 clock={clock} valueBits={value} action={action} status={status}");
        }
        void Drain(int epoch, ulong nanos)
        {
            clock = nanos;
            rows.Add($"M16_009_DRAIN identity={0x100000000UL + (uint)tid} epoch={epoch} nanos={clock}");
        }
        void Supervisor(int action, int transfers, int ready, int active) => rows.Add($"M16_008_SUPERVISOR identity={0x100000000UL + (uint)tid} action={action} transfers={transfers} ready={ready} active={active}");
        void Exit() => rows.Add($"M16_008_EXIT identity={0x100000000UL + (uint)tid} clearTidResult=0");
        void Reap() => rows.Add($"M16_008_REAP old={0x100000000UL + (uint)tid} next={0x200000000UL + (uint)tid} context=zero stack=zero slot=zero");
        var output = new[] { (27UL, "TEST_START nativeaot.hello"), (19UL, "NATIVEAOT_HELLO_OK"), (26UL, "TEST_PASS nativeaot.hello") };
        void Write((ulong Bytes, string Text) write)
        {
            rows.Add($"M16_005_WRITE fd=1 bytes={write.Bytes} data={write.Text}");
            Call(1, write.Bytes, [1, 0x500100, write.Bytes]);
        }
        Call(9, tc, [0, 488, 3, 0x22, ulong.MaxValue, 0]);
        fs = tc + 0x120;
        Call(158, 0, [0x1002, fs]);
        Call(218, 1, [0x53a370]);
        if (extraCalls is not null)
            foreach (var call in extraCalls)
                Call(call.Nr, call.Value, call.Args);
        foreach (ulong signal in new ulong[] { 11, 8, 35, 2, 3, 18 })
        {
            Call(13, 0, [signal, 0, 0x500000, 8]);
            Call(13, 0, [signal, 0x500020, 0x500000, 8]);
        }
        Call(13, 0, [13, 0x500020, 0x500000, 8]);
        Call(324, 0x18, [0, 0, 0]);
        Call(324, 0, [16, 0, 0]);
        Call(318, 8, [0x500100, 8, 0]);
        Call(9, one, [0, 0x183000, 0, 0x22, ulong.MaxValue, 0]);
        Call(10, 0, [one + 8192, 0x181000, 3]);
        Call(56, 2, [0x7d0f00, top1, 0x500200, 0x53a370, fs1]);
        if (wakeBeforeWait || uncontended)
        {
            clock = 1;
            rows.Add($"M16_009_IRQ identity=4294967297 epoch=1 nanos=1 rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 valueBits=2");
            Supervisor(1, 2, 2, 2);
        }
        else
        {
            clock = 1;
            Call(202, 0, [0x5394c0, 128, 0x80000002, 0], 3, 9);
            Drain(1, 2);
            Supervisor(3, 2, 1, 2);
        }
        tid = 2;
        fs = fs1;
        rsp = top1 - 64;
        Call(14, 0, [2, 0x500100, 0, 8]);
        Call(186, 2, []);
        Call(39, 1, []);
        Call(157, unchecked((ulong)-38L), [15, 0x500100]);
        Call(9, 0x100200000, [0, 16384, 3, 0x22, ulong.MaxValue, 0]);
        if (!uncontended)
            Call(202, wakeBeforeWait ? 0UL : 1UL, [0x5394c0, 129, 1]);
        clock = wakeBeforeWait || uncontended ? 2UL : 3UL;
        Call(202, 0, [fs1 + 0x40, 128, 2, 0], 3, 9);
        Drain(2, wakeBeforeWait || uncontended ? 3UL : 4UL);
        Supervisor(3, 4, 1, 2);
        tid = 1;
        fs = tc + 0x120;
        rsp = 0x6ffffff0;
        if (wakeBeforeWait)
        {
            clock = 4;
            Call(202, unchecked((ulong)-11L), [0x5394c0, 128, 0x80000002, 0]);
        }
        else if (!uncontended)
            rows.Add("M16_008_RESUME identity=4294967297 valueBits=0 status=1");
        Call(25, unchecked((ulong)-12L), [0x6fe80000, 4096, 8192, 0]);
        Call(25, unchecked((ulong)-14L), [0x6fe7f000, 4096, 8192, 0]);
        Call(293, 0, [0x500400, 0x80000]);
        Call(9, two, [0, 0x183000, 0, 0x22, ulong.MaxValue, 0]);
        Call(10, 0, [two + 8192, 0x181000, 3]);
        Call(56, 3, [0x7d0f00, top2, 0x500200, 0x53a370, fs2]);
        foreach (var write in output.Take(earlyWrites))
            Write(write);
        if (prematureExit)
            Call(231, 0, [0], 2, 2);
        clock = 5;
        rows.Add($"M16_009_IRQ identity=4294967297 epoch=3 nanos=5 rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 valueBits=3");
        Supervisor(1, 6, 2, 3);
        tid = 3;
        fs = fs2;
        rsp = top2 - 64;
        Call(14, 0, [2, 0x500100, 0, 8]);
        Call(157, unchecked((ulong)-38L), [15, 0x500100]);
        Call(0, 0, [3, rsp - 64, 1], 3, 9);
        Drain(4, 6);
        Supervisor(3, 8, 1, 3);
        tid = 1;
        fs = tc + 0x120;
        rsp = 0x6ffffff0;
        foreach (var write in output.Skip(earlyWrites))
            Write(write);
        Call(231, 0, [0], 2, 2);
        Drain(5, 6);
        Exit();
        Supervisor(2, 10, 2, 3);
        Reap();
        tid = 2;
        Exit();
        Supervisor(4, 12, 1, 2);
        Reap();
        tid = 3;
        Exit();
        Supervisor(4, 14, 0, 1);
        Reap();
        rows.AddRange(["M16_008_BUDGET_END nanos=6",
            "M16_008_OWNER_END reaped=3 transfers=14 irqs=5 exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true","CLROS_M16_009_READY","TEST_PASS m16-009"]);
        return LinuxCallOrdinalTests.Annotate(rows);
    }
}
