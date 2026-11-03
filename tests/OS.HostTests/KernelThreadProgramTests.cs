using OS.Kernel.Threading;
using P = OS.Kernel.Threading.KernelThreadProgram;
using S = OS.Kernel.Threading.ThreadContextSwitch;

internal static unsafe class KernelThreadProgramTests
{
    internal static P.Plan Plan(KernelThreadTableTests.Model m)
    {
        var o = m.Owner;
        o.CodeBytes = P.Bytes;
        o.GuardsVerified = false;
        o.MappingsVerified = false;
        var supervisor = m.Supervisor;
        supervisor.ResumeRip = m.Code + P.DriverResume;
        supervisor.Context->Rip = supervisor.ResumeRip;
        S.Descriptor Worker(int index, uint entry) => new()
        {
            Identity = 0x100000001UL + (uint)index,
            CurrentIdentity = 0x100000001UL + (uint)index,
            Context = m.Context(index),
            ContextBytes = 688,
            StackGuardStart = m.Stack(index),
            StackBytes = 0x3000,
            ResumeRip = m.Code + entry,
            Kind = S.ResumeKind.Entry
        };
        var a = Worker(0, P.EntryA);
        var b = Worker(1, P.EntryB);
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(a.Context, 688, a.ResumeRip, a.StackGuardStart + 8192 - 40, 0xffbf));
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(b.Context, 688, b.ResumeRip, b.StackGuardStart + 8192 - 40, 0xffbf));
        ulong control = o.ContextArena + 0x5000, helper = o.ContextArena + 0x4000;
        new Span<byte>((void*)helper, 512).Clear();
        *(ushort*)helper = 0x027f;
        *(uint*)(helper + 24) = 0x1f80;
        *(uint*)(helper + 28) = 0xffbf;
        KernelThreadEntry.Plan Entry(S.Descriptor d, uint offset, uint body, uint back) => new()
        {
            Owner = o,
            Initial = d,
            CodeOffset = offset,
            Entry = m.Code + body,
            Argument = control,
            Start = 0x200000,
            Control = control,
            ControlBytes = 256,
            ExitStub = m.Code + back,
            RejectRip = m.Code + P.Recovery
        };
        return new()
        {
            Owner = o,
            Supervisor = supervisor,
            A = a,
            B = b,
            First = Entry(a, P.EntryA, P.WorkerA, P.FromA),
            Second = Entry(b, P.EntryB, P.WorkerB, P.FromB),
            Witness = (X64ThreadContext*)(o.ContextArena + 0x3000),
            RecoverCallback = 0x200010,
            TerminalCallback = 0x200020,
            Stub = new()
            {
                AdmissionCallback = 0x200030,
                CallbackContext = control,
                CallbackBytes = 256,
                HelperFx = helper,
                JumpSlot = o.ContextArena + 0x6000,
                RejectRip = m.Code + P.Recovery
            }
        };
    }
    private static ulong Target(byte* instruction) => unchecked((ulong)instruction + 5 + (ulong)(long)*(int*)(instruction + 1));

    [HostTest("kernel.thread program has distinct supervisor workers and exact CALL continuations")]
    private static void ProgramFlow()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = Plan(m);
        byte[] before = new ReadOnlySpan<byte>(m.Memory, KernelThreadTableTests.Model.Bytes).ToArray();
        HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p));
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(before.AsSpan(0, 0x10000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory, 0x10000)));
        HostCheck.True(before.AsSpan(0x12000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory + 0x12000, KernelThreadTableTests.Model.Bytes - 0x12000)));
        foreach (uint driver in new[] { P.DriverA, P.DriverB })
        {
            byte* call = (byte*)m.Code + driver + P.DriverResume - 5;
            HostCheck.Equal((byte)0xe8, *call);
            HostCheck.Equal(m.Code + (driver == P.DriverA ? P.ToA : P.ToB), Target(call));
        }
        foreach ((uint worker, uint back, uint[] values) in new[] {
            (P.WorkerA, P.FromA, new uint[] { 0, 2, 0, 2, 0 }),
            (P.WorkerB, P.FromB, new uint[] { 0, 3, 0, 2, 23 }),
            (P.ReusedWorker, P.FromA, new uint[] { 0, 0, 1 }) })
            for (uint i = 0; i < values.Length; i++)
            {
                byte* action = (byte*)m.Code + worker + 0x10 + i * 0x10;
                HostCheck.Equal((byte)0xba, action[0]);
                HostCheck.Equal(values[i], *(uint*)(action + 1));
                HostCheck.Equal((byte)0xe8, action[5]);
                HostCheck.Equal(m.Code + back, Target(action + 5));
                HostCheck.Equal((byte)0xe9, action[10]);
                HostCheck.Equal((ulong)action + 16, Target(action + 10));
            }
        // Only the owned idle helper enables IF: STI's shadow covers HLT, then CLI.
        byte* idle = (byte*)m.Code + P.Idle;
        HostCheck.True(idle[0] == 0xfb && idle[1] == 0xf4 && idle[2] == 0xfa);
        HostCheck.Equal((byte)0x51, *((byte*)m.Code + P.IrqRestore));
        HostCheck.Equal((byte)0x9d, *((byte*)m.Code + P.IrqRestore + 1));
        ((byte*)m.Code)[P.TimerHandler] = 0x90;
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        ((byte*)m.Code)[P.FromB + 40] ^= 1;
        HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p));
    }

    [HostTest("kernel.thread program rejects mismatched binding aliases and callbacks before writes")]
    private static void BadPlans()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var good = Plan(m);
        byte[] before = new ReadOnlySpan<byte>(m.Memory, KernelThreadTableTests.Model.Bytes).ToArray();
        for (int change = 0; change < 10; change++)
        {
            var p = good;
            if (change == 0)
                p.Witness = p.A.Context;
            if (change == 1)
                p.Stub.CallbackContext = (ulong)p.Witness;
            if (change == 2)
                p.First.Owner.CurrentRoot++;
            if (change == 3)
                p.First.Initial.Identity++;
            if (change == 4)
                p.A.Identity = p.B.Identity;
            if (change == 5)
                p.RecoverCallback = m.Code;
            if (change == 6)
                p.TerminalCallback = 0;
            if (change == 7)
                p.First.Entry = m.Code + P.WorkerB;
            if (change == 8)
                p.Supervisor.ResumeRip++;
            if (change == 9)
                p.Second.ExitStub = m.Code + P.FromA;
            HostCheck.True(!P.Emit((byte*)m.Code, P.Bytes, p));
            HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Memory, KernelThreadTableTests.Model.Bytes)));
        }
    }

    [HostTest("kernel.thread program verifies captures and one republished generation without stale entry identity")]
    private static void CaptureAndRepublish()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = Plan(m);
        HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p));
        p.A.Kind = p.B.Kind = S.ResumeKind.CapturedCall;
        p.A.ResumeRip = m.Code + P.WorkerA + P.Sleep1Resume;
        p.B.ResumeRip = m.Code + P.WorkerB + P.Yield1Resume;
        p.A.Context->Rip = p.A.ResumeRip;
        p.B.Context->Rip = p.B.ResumeRip;
        p.A.Context->Rsp -= 152;
        p.B.Context->Rsp -= 152;
        p.Supervisor.ResumeRip = m.Code + P.DriverB + P.DriverResume;
        p.Supervisor.Context->Rip = p.Supervisor.ResumeRip;
        p.First.Initial = p.A;
        p.Second.Initial = p.B;
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(!P.Emit((byte*)m.Code, P.Bytes, p));
        p.A.Identity += 1UL << 32;
        p.A.CurrentIdentity = p.A.Identity;
        p.A.Kind = S.ResumeKind.Entry;
        p.A.ResumeRip = m.Code + P.EntryA;
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(p.A.Context, 688, p.A.ResumeRip, p.A.StackGuardStart + 8192 - 40, 0xffbf));
        p.First.Initial = p.A;
        p.First.Entry = m.Code + P.ReusedWorker;
        HostCheck.True(!P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.True(KernelThreadEntry.Emit((byte*)m.Code + P.EntryA, 256, p.First));
        HostCheck.True(P.Verify((byte*)m.Code, P.Bytes, p));
    }
}
