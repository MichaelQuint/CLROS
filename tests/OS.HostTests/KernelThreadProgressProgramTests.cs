using OS.Kernel.Threading;
using P = OS.Kernel.Threading.KernelThreadProgram;
using W = OS.Kernel.Threading.KernelThreadProgressProgram;
using S = OS.Kernel.Threading.ThreadContextSwitch;

internal static unsafe class KernelThreadProgressProgramTests
{
    private static ulong Target(byte* instruction) => unchecked((ulong)instruction + 5 + (ulong)(long)*(int*)(instruction + 1));
    private static void Bytes(byte* actual, ReadOnlySpan<byte> expected) =>
        HostCheck.True(new ReadOnlySpan<byte>(actual, expected.Length).SequenceEqual(expected));

    [HostTest("kernel.thread progress has one stop check increment and transfer per fixed worker iteration")]
    private static void Flow()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = KernelThreadProgramTests.Plan(m);
        ulong progress = p.Stub.CallbackContext + 64, stop = progress + 8;
        byte[] before = m.Snapshot();
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, progress, stop));
        HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, progress, stop));
        HostCheck.True(before.AsSpan(0, 0x10000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory, 0x10000)));
        HostCheck.True(before.AsSpan(0x12000).SequenceEqual(new ReadOnlySpan<byte>(m.Memory + 0x12000, KernelThreadTableTests.Model.Bytes - 0x12000)));
        HostCheck.Equal(0x10U, W.Loop);
        HostCheck.Equal(0x3aU, W.YieldResume);
        HostCheck.Equal(0x40U, W.WorkerReturn);
        foreach (uint offset in new[] { P.WorkerA, P.WorkerB, P.ReusedWorker })
        {
            byte* worker = (byte*)m.Code + offset;
            // Eight Win64 callee saves plus40 bytes align the sole CALL and reserve shadow space.
            Bytes(worker, [0x53, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xec, 0x28]);
            Bytes(worker + 0x10, [0x48, 0xb8]);
            HostCheck.Equal(stop, *(ulong*)(worker + 0x12));
            Bytes(worker + 0x1a, [0x83, 0x38, 0x00, 0x0f, 0x85]);
            HostCheck.Equal((ulong)worker + W.WorkerReturn, unchecked((ulong)worker + 0x23 + (ulong)(long)*(int*)(worker + 0x1f)));
            Bytes(worker + 0x23, [0x48, 0xb8]);
            HostCheck.Equal(progress, *(ulong*)(worker + 0x25));
            Bytes(worker + 0x2d, [0x48, 0xff, 0x00, 0xba, 0, 0, 0, 0, 0xe8]);
            HostCheck.Equal(m.Code + (offset == P.WorkerB ? P.FromB : P.FromA), Target(worker + 0x35));
            HostCheck.Equal((ulong)worker + W.YieldResume, (ulong)worker + 0x35 + 5);
            HostCheck.Equal((byte)0xe9, worker[W.YieldResume]);
            HostCheck.Equal((ulong)worker + W.Loop, Target(worker + W.YieldResume));
            HostCheck.Equal((byte)0xcc, worker[0x3f]);
            Bytes(worker + W.WorkerReturn, [0x48, 0x83, 0xc4, 0x28, 0x41, 0x5f, 0x41, 0x5e, 0x41, 0x5d, 0x41, 0x5c, 0x5f, 0x5e, 0x5d, 0x5b, 0xb8, 17, 0, 0, 0, 0xc3]);
            HostCheck.True(new ReadOnlySpan<byte>(worker + 0x56, 256 - 0x56).IndexOfAnyExcept((byte)0xcc) < 0);
        }
        // Counter/stop values are runtime control, not emitter input values.
        *(ulong*)progress = ulong.MaxValue;
        *(uint*)stop = 1;
        HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, progress, stop));
    }

    [HostTest("kernel.thread progress invalid control extents aliases defaults and short output preserve all bytes")]
    private static void Rejections()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var good = KernelThreadProgramTests.Plan(m);
        ulong progress = good.Stub.CallbackContext + 64, stop = progress + 8;
        foreach ((ulong counter, ulong flag) in new[] {
            (0UL, stop), (progress, 0UL), (progress + 1, stop), (progress, stop + 1),
            (good.Stub.CallbackContext - 8, stop), (progress, good.Stub.CallbackContext - 4),
            (good.Stub.CallbackContext + good.Stub.CallbackBytes, stop),
            (progress, good.Stub.CallbackContext + good.Stub.CallbackBytes),
            (ulong.MaxValue - 7, stop), (progress, ulong.MaxValue - 3),
            (progress, progress), (progress, progress + 4), (stop, stop) })
        {
            byte[] before = m.Snapshot();
            HostCheck.True(!W.Emit((byte*)m.Code, W.Bytes, good, counter, flag));
            HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, good, counter, flag));
            m.Same(before);
        }
        for (int change = 0; change < 8; change++)
        {
            var p = good;
            byte* output = (byte*)m.Code;
            uint capacity = W.Bytes;
            if (change == 0)
                p = default;
            if (change == 1)
                output = null;
            if (change == 2)
                output++;
            if (change == 3)
                capacity--;
            if (change == 4)
                capacity = 0;
            if (change == 5)
                p.Witness = p.A.Context;
            if (change == 6)
                p.First.ExitStub = m.Code;
            if (change == 7)
                output = (byte*)p.Stub.CallbackContext;
            byte[] before = m.Snapshot();
            HostCheck.True(!W.Emit(output, capacity, p, progress, stop));
            HostCheck.True(!W.Verify(output, capacity, p, progress, stop));
            m.Same(before);
        }
        // Adjacent fields and the final complete field in control storage are valid in either order.
        foreach ((ulong counter, ulong flag) in new[] {
            (good.Stub.CallbackContext, good.Stub.CallbackContext + 8),
            (good.Stub.CallbackContext + 8, good.Stub.CallbackContext + 4),
            (good.Stub.CallbackContext + good.Stub.CallbackBytes - 8, good.Stub.CallbackContext) })
        {
            HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, good, counter, flag));
            HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, good, counter, flag));
        }
    }

    [HostTest("kernel.thread progress mutations and changed addresses reject while IRQ bytes remain independent")]
    private static void Comparisons()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = KernelThreadProgramTests.Plan(m);
        ulong progress = p.Stub.CallbackContext + 64, stop = progress + 16;
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, progress, stop));
        foreach (uint offset in new[] { P.DriverA + 21, P.EntryA + 10, P.WorkerA + 0x1d, P.WorkerA + 0x2d,
            P.WorkerB + 0x35, P.ReusedWorker + W.YieldResume, P.ReusedWorker + W.WorkerReturn + 17,
            P.WorkerA + 0x56, P.ToA + 50, P.Recovery + 20, P.IrqSaveDisable })
        {
            ((byte*)m.Code)[offset] ^= 1;
            byte[] before = m.Snapshot();
            HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, progress, stop));
            m.Same(before);
            ((byte*)m.Code)[offset] ^= 1;
        }
        HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, progress + 8, stop));
        HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, progress, stop + 4));
        ((byte*)m.Code)[P.TimerHandler] = 0x90;
        HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, progress, stop));
    }

    [HostTest("kernel.thread progress verifies captured yield and normal exit without reemitting live contexts")]
    private static void Captures()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = KernelThreadProgramTests.Plan(m);
        ulong progress = p.Stub.CallbackContext + 64, stop = progress + 8;
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, progress, stop));
        foreach (uint continuation in new[] { P.WorkerA + W.YieldResume, P.EntryA + KernelThreadEntry.ExitResumeOffset })
        {
            p.A.Kind = S.ResumeKind.CapturedCall;
            p.A.ResumeRip = m.Code + continuation;
            p.A.Context->Rip = p.A.ResumeRip;
            p.A.Context->Rsp = p.A.StackGuardStart + 8192 - 192;
            p.First.Initial = p.A;
            HostCheck.True(W.Verify((byte*)m.Code, W.Bytes, p, progress, stop));
            byte[] before = m.Snapshot();
            HostCheck.True(!W.Emit((byte*)m.Code, W.Bytes, p, progress, stop));
            m.Same(before);
        }
    }

    [HostTest("kernel.thread progress entry rejection leaves worker stack before recovery while original program stays unchanged")]
    private static void RecoveryStack()
    {
        using KernelThreadTableTests.Model m = new(initialize: false);
        var p = KernelThreadProgramTests.Plan(m);
        byte* recovery = (byte*)m.Code + P.Recovery;
        HostCheck.True(P.Emit((byte*)m.Code, P.Bytes, p) && P.Verify((byte*)m.Code, P.Bytes, p));
        HostCheck.Equal((byte)0xfc, recovery[0]);
        byte[] originalPrefix = new ReadOnlySpan<byte>(recovery, 24).ToArray();
        HostCheck.Equal(p.Stub.HelperFx, unchecked((ulong)recovery + 9 + (ulong)(long)*(int*)(recovery + 5)));
        HostCheck.True(W.Emit((byte*)m.Code, W.Bytes, p, p.Stub.CallbackContext + 64, p.Stub.CallbackContext + 72));
        Bytes(recovery, [0x48, 0xbc]); // MOV RSP, imm64 before any recovery callback.
        ulong top = p.Owner.SwitchStackGuardStart + p.Owner.SwitchStackBytes - S.Page - 64;
        HostCheck.Equal(top, *(ulong*)(recovery + 2));
        HostCheck.Equal(0UL, top & 15);
        HostCheck.True(originalPrefix.AsSpan(0, 5).SequenceEqual(new ReadOnlySpan<byte>(recovery + 10, 5)));
        HostCheck.Equal(p.Stub.HelperFx, unchecked((ulong)recovery + 19 + (ulong)(long)*(int*)(recovery + 15)));
        HostCheck.True(originalPrefix.AsSpan(9).SequenceEqual(new ReadOnlySpan<byte>(recovery + 19, 15)));
        recovery[2] ^= 16;
        HostCheck.True(!W.Verify((byte*)m.Code, W.Bytes, p, p.Stub.CallbackContext + 64, p.Stub.CallbackContext + 72));
    }
}
