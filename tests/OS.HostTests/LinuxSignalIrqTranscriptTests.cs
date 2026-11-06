using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using Fixture = OS.Tooling.LinuxSignalProofFixture;
using Controls = LinuxSignalTranscriptTests;

internal static unsafe class LinuxSignalIrqTranscriptTests
{
    [HostTest("m16-009 synthetic activation schedules require real spin capture signal calls exits and draining across varied preemption")]
    private static void Schedules()
    {
        foreach (bool early in new[] { false, true })
        {
            string[] rows = Synthetic(early);
            QemuTranscripts.ValidateLinuxSignalTail(rows, Fixture.CreatePackage("Activation"), 0x200000, 0x100000);
            HostCheck.Equal(18, rows.Count(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal)));
            HostCheck.Equal(2, rows.Count(r => r.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal)));
            HostCheck.True(rows.Length == (early ? 51 : 39));
        }
    }

    [HostTest("m16-009 activation rejects forged IRQ epoch instruction flags context and ending-checkpoint ownership")]
    private static void IrqFields()
    {
        string[] rows = Synthetic(true);
        byte[] package = Fixture.CreatePackage("Activation");
        QemuTranscripts.ValidateLinuxSignalTail(rows, package, 0x200000, 0x100000);
        int loop = Array.FindIndex(rows, r => r.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) && Controls.Field(r, "rip") == Fixture.ActivationLoopRip);
        foreach (string field in new[] { "identity", "epoch", "rip", "rsp", "fs", "gs", "valueBits" })
            Controls.Reject(Controls.Change(rows, loop, field, Controls.Field(rows[loop], field) + 1), package);
        foreach (ulong flags in new[] { 0UL, 2UL, 0x202UL, 0x3602UL, 0x10602UL })
            Controls.Reject(Controls.Change(rows, loop, "flags", flags), package);
        Controls.Reject(Controls.Change(rows, loop, "nanos", ulong.MaxValue), package);
        int drain = Array.FindIndex(rows, r => r.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal));
        foreach (string field in new[] { "identity", "epoch" })
            Controls.Reject(Controls.Change(rows, drain, field, Controls.Field(rows[drain], field) + 1), package);
        int final = Array.FindLastIndex(rows, r => r.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal));
        Controls.Reject(Controls.Change(rows, final, "nanos", Controls.Field(rows[final], "nanos") + 1), package);
        // Same executable address at the wrong completed-call stage is stale.
        int initial = Array.FindIndex(rows, r => r.StartsWith("M16_009_IRQ ", StringComparison.Ordinal));
        Controls.Reject(Controls.Change(rows, initial, "rip", Fixture.ActivationLoopRip), package);
    }

    [HostTest("m16-009 activation rejects wrong FIFO selection call vectors signal bypass transfer and reap accounting")]
    private static void OwnershipAndCalls()
    {
        string[] rows = Synthetic();
        byte[] package = Fixture.CreatePackage("Activation");
        QemuTranscripts.ValidateLinuxSignalTail(rows, package, 0x200000, 0x100000);
        int call = Array.FindIndex(rows, r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && Controls.Field(r, "nr") == 234);
        foreach (string field in new[] { "identity", "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "rsp", "fs", "gs", "clock", "valueBits", "action", "status" })
            Controls.Reject(Controls.Change(rows, call, field, Controls.Field(rows[call], field) + 1), package);
        int confirm = Array.FindIndex(rows, r => r.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal));
        foreach (string field in new[] { "identity", "action", "transfers", "ready", "active" })
            Controls.Reject(Controls.Change(rows, confirm, field, Controls.Field(rows[confirm], field) + 1), package);
        int reap = Array.FindIndex(rows, r => r.StartsWith("M16_008_REAP ", StringComparison.Ordinal));
        Controls.Reject(Controls.Change(rows, reap, "next", 0x100000002), package);
        int end = Array.FindIndex(rows, r => r.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal));
        foreach (string field in new[] { "reaped", "transfers", "irqs", "exit", "userReserved", "userBacked" })
            Controls.Reject(Controls.Change(rows, end, field, Controls.Field(rows[end], field) + 1), package);
        string[] liveStack = (string[])rows.Clone();
        liveStack[end] = liveStack[end].Replace("initialStack=zero", "initialStack=dirty", StringComparison.Ordinal);
        Controls.Reject(liveStack, package);
        Controls.Reject(rows, Fixture.CreatePackage("AlternateStack"));
    }

    [HostTest("m16-009 activation requires every witness IRQ handoff exit reap and restoration row in order")]
    private static void RecordOrder()
    {
        string[] rows = Synthetic();
        byte[] package = Fixture.CreatePackage("Activation");
        QemuTranscripts.ValidateLinuxSignalTail(rows, package, 0x200000, 0x100000);
        for (int i = 0; i < rows.Length; i++)
        {
            Controls.Reject(rows.Where((_, j) => j != i).ToArray(), package);
            if (i + 1 < rows.Length)
            {
                string[] swap = rows.ToArray();
                (swap[i], swap[i + 1]) = (swap[i + 1], swap[i]);
                Controls.Reject(swap, package);
            }
        }
        Controls.Reject([.. rows, rows[^1]], package);
        string[] disabled = rows.ToArray();
        disabled[4] = disabled[4].Replace("timer=user-irq-and-idle", "timer=idle-only", StringComparison.Ordinal);
        Controls.Reject(disabled, package);
        string[] extra = rows.ToArray();
        extra[Array.FindIndex(rows, r => r.StartsWith("M16_009_IRQ ", StringComparison.Ordinal))] += " rip=4194304";
        Controls.Reject(extra, package);
    }

    // Parser controls only: these rows never execute a thread, signal or IRQ.
    internal static string[] Synthetic(bool early = false)
    {
        byte[] package = Fixture.CreatePackage("Activation");
        var points = Fixture.ExpectedIrqPoints();
        List<string> rows = Controls.Synthetic("AlternateStack").Take(5).ToList();
        rows[4] = rows[4].Replace("timer=idle-only", "timer=user-irq-and-idle", StringComparison.Ordinal);
        int[] counts = [0, 0];
        ulong clock = 0, epoch = 0, transfers = 0;
        int active = 1;
        Queue<int> ready = new();
        ready.Enqueue(0);
        (uint Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4)[] parent = [(39,0,0,0,0,0),(186,0,0,0,0,0),(158,0x1002,0x402000,0,0,0),
            (158,0x1001,0x402100,0,0,0),(13,35,0x401100,0,8,0),(131,0x401120,0,0,0,0),(56,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000),
            (186,0,0,0,0,0),(14,2,0,0x401158,8,0),(1,1,0x4001a0,15,0,0),(15,1,0x4001a0,15,0,0),(1,1,0x400180,15,0,0),(231,42,0,0,0,0)];
        (uint Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4)[] child = [(158, 0x1001, 0x403100, 0, 0, 0), (39, 0, 0, 0, 0, 0), (186, 0, 0, 0, 0, 0), (234, 1, 1, 35, 0, 0), (60, 7, 0, 0, 0, 0)];
        void Segment(int which, int calls, bool exit, ulong? rip = null)
        {
            HostCheck.Equal(which, ready.Dequeue());
            ulong identity = 0x100000001UL + (uint)which;
            for (int j = 0; j < calls; j++)
            {
                int i = counts[which];
                var c = (which == 0 ? parent : child)[i];
                var instruction = Fixture.ExpectedIrqInvocations(which == 1)[i];
                if (c.Nr == 1)
                    rows.Add("M16_005_WRITE fd=1 bytes=15 data=" + (i == 9 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
                ulong value = which == 1 ? i == 1 ? 1UL : i == 2 ? 2UL : 0 : i is 0 or 1 or 7 ? 1UL : i == 6 ? 2UL : c.Nr == 1 ? 15UL : c.Nr == 15 ? 0x12345678UL : 0;
                bool terminal = i == (which == 0 ? 12 : 4);
                ulong action = terminal ? which == 0 ? 2UL : 4UL : c.Nr == 15 ? 5UL : 1UL;
                ulong rsp = which == 1 ? 0x406000UL : i == 10 ? 0x406c40UL : i is >= 7 and <= 9 ? 0x406c38UL : 0x405000UL;
                ulong fs = which == 1 ? 0x403000UL : i < 2 ? 0UL : 0x402000UL, gs = which == 1 ? 0x403100UL : i < 3 ? 0UL : 0x402100UL;
                rows.Add(FormattableString.Invariant($"M16_008_CALL identity={identity} nr={c.Nr} a0={c.A0} a1={c.A1} a2={c.A2} a3={c.A3} a4={c.A4} a5=0 rip={instruction.ReturnRip} rsp={rsp} flags=582 fs={fs} gs={gs} clock={clock} valueBits={value} action={action} status={(terminal ? 2 : 1)}"));
                if (which == 0 && i == 6)
                {
                    ready.Enqueue(1);
                    active++;
                }
                counts[which]++;
            }
            epoch++;
            if (!exit)
            {
                var point = points.Single(p => p.Child == (which == 1) && p.Rip == rip!.Value);
                ulong rsp = point.Rsp == 0 ? InitialStack(package) : point.Rsp;
                clock += 100;
                ulong value = point.Loop ? 0x1122334400000000UL : 0;
                rows.Add(FormattableString.Invariant($"M16_009_IRQ identity={identity} epoch={epoch} nanos={clock} rip={point.Rip} rsp={rsp} flags={(point.Direction ? 0x646 : 0x246)} fs={point.Fs} gs={point.Gs} valueBits={value}"));
                ready.Enqueue(which);
            }
            else
            {
                if (which == 1)
                    clock += 100;
                rows.Add(FormattableString.Invariant($"M16_009_DRAIN identity={identity} epoch={epoch} nanos={clock}"));
                rows.Add(FormattableString.Invariant($"M16_008_EXIT identity={identity} clearTidResult=0"));
            }
            transfers += 2;
            rows.Add(FormattableString.Invariant($"M16_008_SUPERVISOR identity={identity} action={(exit ? which == 0 ? 2 : 4 : 1)} transfers={transfers} ready={ready.Count} active={active}"));
            if (exit)
            {
                rows.Add(FormattableString.Invariant($"M16_008_REAP old={identity} next={identity + (1UL << 32)} context=zero stack=zero slot=zero"));
                active--;
            }
        }
        if (early)
            Segment(0, 0, false, 0x400200);
        Segment(0, 7, false, Fixture.ActivationLoopRip);
        if (early)
        {
            var childWait = points.First(p => p.Child && p.Calls == 3);
            Segment(1, 3, false, childWait.Rip);
            Segment(0, 0, false, Fixture.ActivationLoopRip);
            var childAfterSend = points.First(p => p.Child && p.Calls == 4);
            Segment(1, 1, false, childAfterSend.Rip);
            var handler = points.First(p => p.Handler && p.Calls == 9);
            Segment(0, 2, false, handler.Rip);
            Segment(1, 1, true);
            Segment(0, 2, false, Fixture.ActivationResumeRip);
            Segment(0, 2, true);
        }
        else
        {
            Segment(1, 5, true);
            Segment(0, 6, true);
        }
        rows.Add(FormattableString.Invariant($"M16_008_OWNER_END reaped=2 transfers={transfers} irqs={epoch} exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained"));
        rows.AddRange(["M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        return LinuxCallOrdinalTests.Annotate(rows);
    }
    private static ulong InitialStack(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        byte[] seed = Enumerable.Repeat((byte)1, 32).ToArray();
        byte* s = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try
        {
            HostCheck.True(s != null);
            fixed (byte* p = package)
            fixed (byte* r = seed)
            {
                HostCheck.Equal(LinuxElfStartupStatus.Ready, LinuxElfStartup.Build(p + view.ManifestOffset, view.ManifestBytes, p + view.PayloadOffset, view.PayloadBytes, r, 32, s, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var plan));
                return plan.StackPointer;
            }
        }
        finally
        {
            NativeMemory.AlignedFree(s);
        }
    }
}
