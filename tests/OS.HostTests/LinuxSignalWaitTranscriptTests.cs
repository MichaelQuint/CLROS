using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;
using T = LinuxSignalTranscriptTests;

internal static class LinuxSignalWaitTranscriptTests
{
    [HostTest("m16-009 interrupted pipe futex restart and partial write tails require complete three dispatch lifetimes")]
    private static void Positive()
    {
        int[] counts = [44, 44, 40, 41, 45];
        for (int i = 0; i < F.WaitVariants.Count; i++)
        {
            string variant = F.WaitVariants[i];
            string[] rows = Synthetic(variant);
            QemuTranscripts.ValidateLinuxSignalTail(rows, F.CreatePackage(variant), 0x200000, 0x100000);
            HostCheck.Equal(counts[i], rows.Length);
            HostCheck.Equal(3, rows.Count(r => r.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal)));
            HostCheck.Equal(2, rows.Count(r => r.StartsWith("M16_008_REAP ", StringComparison.Ordinal)));
        }
    }

    [HostTest("m16-009 wait tails reject forged interrupted results replay bindings clocks and handler identity")]
    private static void CallBinding()
    {
        foreach (string variant in F.WaitVariants)
        {
            string[] rows = Synthetic(variant);
            byte[] package = F.CreatePackage(variant);
            foreach (int at in Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith("M16_008_CALL ", StringComparison.Ordinal)))
            {
                foreach (string key in new[] { "identity", "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "rsp", "fs", "gs", "valueBits", "action", "status", "ordinal" })
                    T.Reject(T.Change(rows, at, key, unchecked(T.Field(rows[at], key) + 1)), package);
                T.Reject(T.Change(rows, at, "clock", ulong.MaxValue), package);
                foreach (ulong flags in new[] { 0UL, 0x246UL, 0x446UL, 0x3046UL })
                    T.Reject(T.Change(rows, at, "flags", flags), package);
            }
            int resume = Array.FindIndex(rows, r => r.StartsWith("M16_008_RESUME ", StringComparison.Ordinal));
            foreach (string key in new[] { "identity", "valueBits", "status" })
                T.Reject(T.Change(rows, resume, key, 0), package);
            // A valid restarted read consumes one byte; a futex replay sees the
            // child's changed word and returns EAGAIN, never a fabricated wake.
            if (variant.EndsWith("Restart", StringComparison.Ordinal))
            {
                int replay = Array.FindIndex(rows, resume + 1, r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal) &&
                    T.Field(r, "nr") == (variant == "SignalFutexRestart" ? 202UL : 0UL));
                HostCheck.True(replay > resume);
                T.Reject(T.Change(rows, replay, "valueBits", 0), package);
                T.Reject(T.Change(rows, replay, "rip", T.Field(rows[replay], "rip") + 2), package);
                T.Reject(T.Change(rows, replay, "clock", T.Field(rows[replay], "clock") + 1), package);
            }
        }
    }

    [HostTest("m16-009 wait tails reject missing reordered duplicate calls and incomplete completion reap or cleanup")]
    private static void OrderAndCleanup()
    {
        foreach (string variant in F.WaitVariants)
        {
            string[] rows = Synthetic(variant);
            byte[] package = F.CreatePackage(variant);
            for (int i = 0; i < rows.Length; i++)
            {
                T.Reject(rows.Where((_, at) => at != i).ToArray(), package);
                string[] changed = (string[])rows.Clone();
                changed[i] += " extra=1";
                T.Reject(changed, package);
                if (i + 1 < rows.Length)
                {
                    changed = (string[])rows.Clone();
                    (changed[i], changed[i + 1]) = (changed[i + 1], changed[i]);
                    T.Reject(changed, package);
                }
            }
            foreach (int at in Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal)))
                foreach (string key in new[] { "identity", "action", "transfers", "ready", "active" })
                    T.Reject(T.Change(rows, at, key, T.Field(rows[at], key) + 1), package);
            int end = Array.FindIndex(rows, r => r.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal));
            foreach (string key in new[] { "reaped", "transfers", "irqs", "exit", "userReserved", "userBacked" })
                T.Reject(T.Change(rows, end, key, T.Field(rows[end], key) + 1), package);
            T.Reject([.. rows, rows[^1]], package);
            T.Reject(rows, F.CreatePackage(variant == "SignalPipePartial" ? "SignalPipeRestart" : "SignalPipePartial"));
        }
    }

    internal static string[] Synthetic(string variant)
    {
        bool futex = variant.StartsWith("SignalFutex", StringComparison.Ordinal), partial = variant == "SignalPipePartial", restart = variant.EndsWith("Restart", StringComparison.Ordinal);
        bool stolen = variant == F.StolenWaitVariant;
        List<string> rows = T.Synthetic("AlternateStack").Take(5).Select(r => r.Replace("packageBytes=28672", "packageBytes=40960", StringComparison.Ordinal)).ToList();
        var instructions = F.ExpectedWaitInvocations(variant);
        int call = 0;
        ulong clock = 0;
        bool child = false, handler = false;
        void Emit(ulong nr, long value = 0, ulong a = 0, ulong b = 0, ulong c = 0, ulong d = 0, ulong e = 0, ulong action = 1, ulong status = 1)
        {
            if (nr == 1 && a == 1)
                rows.Add("M16_005_WRITE fd=1 bytes=15 data=" + (handler ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
            var instruction = instructions[call++];
            if (futex && (nr == 202 && b == 128 && action == 3 || child && nr == 234))
                clock += 3;
            ulong rsp = child ? 0x406000UL : handler ? nr == 15 ? 0x406c40UL : 0x406c38UL : 0x405000UL;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={(child ? 4294967298UL : 4294967297UL)} nr={nr} a0={a} a1={b} a2={c} a3={d} a4={e} a5=0 rip={instruction.ReturnRip} rsp={rsp} flags=70 fs={(child ? 0x403000UL : call <= 2 ? 0UL : 0x402000UL)} gs=0 clock={clock} valueBits={unchecked((ulong)value)} action={action} status={status}"));
        }
        void Supervisor(ulong identity, uint action, uint transfers, uint ready, uint active) => rows.Add(FormattableString.Invariant($"M16_008_SUPERVISOR identity={identity} action={action} transfers={transfers} ready={ready} active={active}"));
        void ExitReap(ulong identity, uint action, uint transfers, uint ready, uint active)
        {
            rows.Add(FormattableString.Invariant($"M16_008_EXIT identity={identity} clearTidResult=0"));
            Supervisor(identity, action, transfers, ready, active);
            rows.Add(FormattableString.Invariant($"M16_008_REAP old={identity} next={identity + (1UL << 32)} context=zero stack=zero slot=zero"));
        }
        Emit(39, 1);
        Emit(186, 1);
        Emit(158, 0, 0x1002, 0x402000);
        Emit(13, 0, 10, 0x401100, 0, 8);
        Emit(131, 0, 0x401120);
        if (!futex)
            Emit(293, 0, 0x4011c0);
        Emit(56, 2, 0x7d0f00, 0x406000, 0x4011b8, 0x4011bc, 0x403000);
        if (futex)
            Emit(202, 0, 0x4011d0, 128, action: 3, status: 9);
        else
            Emit(partial ? 1UL : 0UL, 0, partial ? 4UL : 3UL, partial ? 0x407000UL : 0x409000UL, partial ? 4100UL : 1UL, action: 3, status: 9);
        Supervisor(4294967297, 3, 2, 1, 2);
        child = true;
        Emit(39, 1);
        Emit(186, 2);
        if (stolen)
            Emit(1, 1, 4, 0x407000, 1);
        Emit(234, 0, 1, 1, 10);
        if (futex)
            Emit(202, 0, 0x4011d0, 129, 1);
        else if (stolen)
            Emit(0, 1, 3, 0x409000, 1);
        else if (!partial)
            Emit(1, 1, 4, 0x407000, 1);
        Emit(60, 0, 7, action: 4, status: 2);
        ExitReap(4294967298, 4, 4, 1, 2);
        child = false;
        if (stolen)
            Supervisor(4294967297, 3, 6, 1, 1);
        rows.Add(FormattableString.Invariant($"M16_008_RESUME identity=4294967297 valueBits={unchecked((ulong)(partial ? 4096L : -4L))} status=1"));
        handler = true;
        Emit(186, 1);
        Emit(14, 0, 2, 0, 0x401158, 8);
        Emit(131, 0, 0, 0x4011a0);
        Emit(1, 15, 1, 0x4001a0, 15);
        Emit(15, partial ? 4096 : restart ? futex ? 202 : 0 : -4, 1, 0x4001a0, 15, action: 5);
        handler = false;
        if (stolen)
            Emit(1, 1, 4, 0x407000, 1);
        if (restart)
        {
            if (futex)
                Emit(202, -11, 0x4011d0, 128);
            else
                Emit(0, 1, 3, 0x409000, 1);
        }
        else if (!futex && !partial)
            Emit(0, 1, 3, 0x409000, 1);
        if (partial)
        {
            Emit(0, 4096, 3, 0x409000, 4096);
            Emit(1, 4, 4, 0x408000, 4);
            Emit(0, 4, 3, 0x409000, 4);
        }
        Emit(14, 0, 2, 0, 0x401150, 8);
        Emit(131, 0, 0, 0x401180);
        if (!futex)
        {
            Emit(3, 0, 3);
            Emit(3, 0, 4);
        }
        Emit(1, 15, 1, 0x400180, 15);
        Emit(231, 0, 42, action: 2, status: 2);
        ExitReap(4294967297, 2, stolen ? 8U : 6U, 0, 1);
        rows.AddRange(["M16_008_OWNER_END reaped=2 transfers="+(stolen?8:6)+" irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true","CLROS_M16_009_READY","TEST_PASS m16-009"]);
        HostCheck.Equal(instructions.Count, call);
        return LinuxCallOrdinalTests.Annotate(rows);
    }
}
