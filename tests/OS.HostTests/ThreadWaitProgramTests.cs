using OS.Kernel.Threading;
using P = OS.Kernel.Threading.KernelThreadProgram;
using W = OS.Kernel.Threading.ThreadWaitProgram;

internal static unsafe class ThreadWaitProgramTests
{
    private static P.Plan Plan(KernelThreadTableTests.Model m, bool tls)
    {
        var p = KernelThreadProgramTests.Plan(m);
        if (tls)
        {
            p.A.Context->FsBase = p.Owner.ContextArena + 0x7000; p.A.Context->UserGsBase = p.Owner.ContextArena + 0x7200;
            p.B.Context->FsBase = p.Owner.ContextArena + 0x7400; p.B.Context->UserGsBase = p.Owner.ContextArena + 0x7600;
        }
        return p;
    }
    private static ulong Target(byte* instruction) => unchecked((ulong)instruction + 5 + (ulong)(long)*(int*)(instruction + 1));

    [HostTest("thread.wait program independent FS GS loads and exact four action continuations")]
    private static void Flow()
    {
        foreach (bool tls in new[] { false, true })
        {
            using KernelThreadTableTests.Model m = new(initialize: false); var p = Plan(m, tls);
            HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, tls)); HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, tls));
            byte[] loads = [0x64, 0x4c, 0x8b, 0x24, 0x25, 0, 0, 0, 0, 0x65, 0x4c, 0x8b, 0x2c, 0x25, 0, 0, 0, 0];
            foreach (uint worker in new[] { P.WorkerA, P.WorkerB, P.ReusedWorker })
            {
                for (uint action = 0; action < 4; action++)
                {
                    byte* first = (byte*)m.Code + worker + 0x10 + action * 0x30;
                    for (int b = 0; b < 18; b++) HostCheck.Equal(tls ? loads[b] : (byte)0x90, first[b]);
                    HostCheck.Equal((byte)0xba, first[18]); HostCheck.Equal(action, *(uint*)(first + 19));
                    HostCheck.Equal((byte)0xe8, first[23]); HostCheck.Equal(m.Code + (worker == P.WorkerB ? P.FromB : P.FromA), Target(first + 23));
                    HostCheck.Equal((ulong)first + 28, m.Code + worker + W.ActionResume + action * W.ActionStride);
                    HostCheck.Equal((byte)0xe9, first[28]); HostCheck.Equal((ulong)first + 48, Target(first + 28));
                }
                byte* end = (byte*)m.Code + worker + 0xd0;
                HostCheck.Equal(0x28c48348U, *(uint*)end); HostCheck.Equal((byte)0xb8, end[16]);
                HostCheck.Equal(worker == P.WorkerB ? 23U : 17U, *(uint*)(end + 17)); HostCheck.Equal((byte)0xc3, end[21]);
            }
            foreach (uint driver in new[] { P.DriverA, P.DriverB })
            { byte* call = (byte*)m.Code + driver + 21; HostCheck.Equal((byte)0xe8, *call); HostCheck.Equal(m.Code + (driver == P.DriverA ? P.ToA : P.ToB), Target(call)); }
        }
    }

    [HostTest("thread.wait program bad TLS or binding plans preserve complete output and resources")]
    private static void Rejections()
    {
        using KernelThreadTableTests.Model m = new(initialize: false); var good = Plan(m, true);
        for (int change = 0; change < 6; change++)
        {
            var p = good; ulong oldFs = p.A.Context->FsBase;
            if (change == 0) p.Witness = p.A.Context;
            if (change == 1) p.First.ExitStub = m.Code;
            if (change == 2) p.Stub.HelperFx = (ulong)p.Witness;
            if (change == 3) p.A.Context->FsBase = 0;
            if (change == 4) p.Second.Entry = p.First.Entry;
            if (change == 5) p.Owner.CurrentRoot++;
            byte[] before = m.Snapshot(); HostCheck.True(!W.Emit((byte*)m.Code, W.Bytes, p, true)); m.Same(before);
            p.A.Context->FsBase = oldFs;
        }
    }

    [HostTest("thread.wait program checks worker bytes and shared infrastructure while preserving IRQ ownership")]
    private static void Comparisons()
    {
        using KernelThreadTableTests.Model m = new(initialize: false); var p = Plan(m, true);
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, true));
        foreach (uint offset in new[] { P.WorkerA + 0x10, P.WorkerB + 0x20, P.ReusedWorker + 0x31, P.ToA + 50, P.Recovery + 20, P.IrqSaveDisable })
        { ((byte*)m.Code)[offset] ^= 1; HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, true)); ((byte*)m.Code)[offset] ^= 1; }
        ((byte*)m.Code)[P.TimerHandler] = 0x90; HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, true));
        HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, false));
    }

    [HostTest("thread.wait program captures retain admitted descriptors and original byte vectors")]
    private static void Captures()
    {
        using KernelThreadTableTests.Model m = new(initialize: false); var p = Plan(m, true);
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, true));
        p.A.Kind = ThreadContextSwitch.ResumeKind.CapturedCall; p.A.ResumeRip = m.Code + P.WorkerA + W.ActionResume;
        p.A.Context->Rip = p.A.ResumeRip; p.A.Context->Rsp = p.A.StackGuardStart + 8192 - 192;
        p.First.Initial = p.A; HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, true));
        p.A.Context->FsBase = p.Owner.ContextArena - 8; HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, true));
    }
}
