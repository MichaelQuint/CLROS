#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;
































































internal static unsafe partial class RhpNewFastAllocation
{



    private static CctorStorage s_cctorStorage;
    private static CctorCounters s_cctorCounters;
    private static CctorActiveContexts s_cctorActive;
    private static ulong s_cctorPhase, s_cctorSeed, s_cctorGcObject;
    private static ulong s_cctorDirectPartial, s_cctorCyclePartial;
    private static ulong s_cctorNonGcCalls, s_cctorGcCalls, s_cctorLastContext, s_cctorLastBase;
    private static uint s_cctorDepth;
    private static ulong s_cctorDepthOneReentries, s_cctorDepthTwoReentries;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ulong CctorSeed() => s_cctorSeed;

    // Crossing a no-inline method outside the declaring type keeps the compiler
    // static-access guard on the direct-recursion callsite observable.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ulong CctorDirectRead() => CctorDirectFixture.Read();

    internal static void CctorEntered(uint index)
    {
        if (s_cctorPhase != 1 || index >= 8) RejectCctor(1);
        fixed (CctorCounters* counts = &s_cctorCounters)
            if (++counts->Values[index] != 1) RejectCctor(2);
    }

    internal static void CctorPartial(uint kind, ulong value)
    {
        if (kind == 0) s_cctorDirectPartial = value;
        else if (kind == 1) s_cctorCyclePartial = value;
        else RejectCctor(3);
    }

    internal static void EnsureCompilerClassConstructor(ulong context)
    {
        if (s_cctorPhase != 1) RejectCctor(4);
        fixed (CctorStorage* storage = &s_cctorStorage)
        fixed (CctorActiveContexts* active = &s_cctorActive)
        {
            uint depth = s_cctorDepth;
            if (depth >= 16) RejectCctor(18);
            bool reentry = false;
            for (uint i = 0; i < depth; i++) if (active->Values[i] == context) reentry = true;
            active->Values[depth] = context; s_cctorDepth = depth + 1;
            if (CompilerClassConstructorExecution.EnsureLazy((byte*)storage, (ulong)sizeof(CctorStorage), context)
                != CompilerClassConstructorStatus.Success) RejectCctor(5);
            if (s_cctorDepth != depth + 1 || active->Values[depth] != context) RejectCctor(19);
            if (reentry)
            {
                // Success from an active context preserves the original callable;
                // only the outer successful return may zero the compiler word.
                if (*(ulong*)context == 0) RejectCctor(20);
                if (depth == 1) s_cctorDepthOneReentries++;
                else if (depth == 2) s_cctorDepthTwoReentries++;
                else RejectCctor(21);
            }
            active->Values[depth] = 0; s_cctorDepth = depth;
        }
    }

    internal static void ObserveCompilerNonGcBase(ulong context, ulong address)
    {
        if (context > ulong.MaxValue - 8 || address != context + 8) RejectCctor(6);
        s_cctorNonGcCalls++;
        s_cctorLastContext = context; s_cctorLastBase = address;
    }

    internal static void ObserveCompilerGcBase(ulong context, object value)
    {
        if (Bits(value) != s_cctorGcObject || context == 0) RejectCctor(7);
        s_cctorGcCalls++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BindAndRunCompilerClassConstructors(ulong gcObject)
    {
        if (!OS.Kernel.Diagnostics.Probes.EnableM04ClassConstructorExecutionProbe ||
            s_cctorPhase != 0 || gcObject == 0 || s_gcHandle == 0) RejectCctor(8);
        s_cctorSeed = s_gcHandle ^ (ulong)s_access.Backing;
        s_cctorGcObject = gcObject;
        CctorText(0); // TEST_START m04.024
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (CctorStorage* storage = &s_cctorStorage)
        {
            if (CompilerClassConstructorExecution.RequiredStorage(4, 16) > (ulong)sizeof(CctorStorage) ||
                CompilerClassConstructorExecution.Bind(s_gcCapture, s_gcMapping, (byte*)type,
                    (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, (ulong)sizeof(CctorStorage), 4, 16)
                    != CompilerClassConstructorStatus.Success) RejectCctor(9);
            s_cctorPhase = 1;
            CctorLine(0, s_gcHandle, (ulong)storage, (ulong)sizeof(CctorStorage), s_cctorSeed);
            if (BootSequence.RunCompilerClassConstructors((byte*)storage, (ulong)sizeof(CctorStorage))
                    != CompilerClassConstructorStatus.Success) RejectCctor(10);
            if (CompilerClassConstructorExecution.Observe((byte*)storage, (ulong)sizeof(CctorStorage), out var eager)
                    != CompilerClassConstructorStatus.Success ||
                eager.EagerCount != 1 || eager.EagerCompleted != 1 || !eager.EagerFinished ||
                CctorEagerFixture.Read() != s_cctorSeed + 11) RejectCctor(10);
            CctorLine(1, eager.EagerCount, eager.EagerCompleted, CctorEagerFixture.Read(), s_cctorSeed + 11);

            ulong lazy = CctorLazyFixture.Read(), direct = CctorDirectFixture.Read();
            ulong cycle = CctorCycleA.Read(), nested = CctorNestedOuter.Read();
            if (lazy != s_cctorSeed + 21 || CctorLazyFixture.Read() != lazy) RejectCctor(11);
            if (direct != s_cctorSeed + 32 || s_cctorDirectPartial != s_cctorSeed + 31 ||
                CctorDirectFixture.Read() != direct) RejectCctor(12);
            if (cycle != s_cctorSeed + 43 || CctorCycleB.Read() != s_cctorSeed + 42 ||
                s_cctorCyclePartial != s_cctorSeed + 41 || CctorCycleA.Read() != cycle) RejectCctor(13);
            if (nested != s_cctorSeed + 52 || CctorNestedInner.Read() != s_cctorSeed + 51 ||
                CctorNestedOuter.Read() != nested) RejectCctor(14);
            // The existing one-cell fixture keeps its null payload. Its real
            // compiler GC-base helper executes its constructor once before use.
            if (Bits(GcStaticFixture.Get()) != 0 || Bits(GcStaticFixture.Get()) != 0 ||
                s_cctorGcCalls == 0 || s_cctorNonGcCalls == 0 ||
                s_cctorLastBase != s_cctorLastContext + 8) RejectCctor(15);
            if (BootSequence.RunCompilerClassConstructors((byte*)storage, (ulong)sizeof(CctorStorage))
                    != CompilerClassConstructorStatus.Success) RejectCctor(16);
            if (CompilerClassConstructorExecution.Observe((byte*)storage, (ulong)sizeof(CctorStorage), out var done)
                    != CompilerClassConstructorStatus.Success ||
                done.EagerCount != 1 || done.EagerCompleted != 1 || !done.EagerFinished ||
                done.LazyCount != 7 || done.LazyCompleted != 7 || done.ActiveLazyCount != 0) RejectCctor(16);
            fixed (CctorCounters* counts = &s_cctorCounters)
                for (uint i = 0; i < 8; i++) if (counts->Values[i] != 1) RejectCctor(17);
            // ILC also guards stores in the active constructors themselves.
            // Count all verified depth-one/depth-two helper entries, separately
            // from the eight constructor invocation counters above.
            if (s_cctorDepthOneReentries != 5 || s_cctorDepthTwoReentries != 3 || s_cctorDepth != 0) RejectCctor(22);
            CctorLine(2, lazy, s_cctorSeed + 21, 1, 1);
            CctorLine(3, s_cctorDirectPartial, direct, s_cctorSeed + 31, 1);
            CctorLine(4, s_cctorCyclePartial, CctorCycleB.Read(), cycle, 2);
            CctorLine(5, CctorNestedInner.Read(), nested, s_cctorSeed + 51, 2);
            CctorLine(6, s_cctorNonGcCalls, s_cctorGcCalls, s_cctorLastContext, s_cctorLastBase);
            CctorLine(7, done.EagerCompleted, done.LazyCompleted, done.ActiveLazyCount, 8);
            CctorLine(8, s_cctorDepthOneReentries, s_cctorDepthTwoReentries, s_cctorDepth, s_cctorGcObject);
            CctorText(1); CctorText(2);
            // Keep admission live for later compiled accesses. Completed context
            // words now let ILC take its zero-word fast path without rerunning.
        }
    }

    private static void CctorLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        Write((byte)'M'); Write((byte)'0'); Write((byte)'4'); Write((byte)'_');
        Write((byte)'C'); Write((byte)'C'); Write((byte)'T'); Write((byte)'O'); Write((byte)'R'); Write(32);
        Decimal(stage); Write(32); Hex(a); Write(32); Hex(b); Write(32); Hex(c); Write(32); Hex(d); End();
    }

    private static void CctorText(uint kind)
    {
        ulong a, b, c; uint bytes;
        if (kind == 0) { a = 0x4154535f54534554; b = 0x302e34306d205452; c = 0x000000000a0d3432; bytes = 20; }
        else if (kind == 1) { a = 0x304d5f534f524c43; b = 0x45525f3432305f34; c = 0x0000000a0d594441; bytes = 21; }
        else { a = 0x5341505f54534554; b = 0x32302e34306d2053; c = 0x00000000000a0d34; bytes = 19; }
        for (uint i = 0; i < bytes; i++) Write((byte)((i < 8 ? a : i < 16 ? b : c) >> ((int)(i & 7) * 8)));
    }

    private static void RejectCctor(ulong reason)
    {
        CctorLine(255, reason, s_cctorPhase, s_cctorNonGcCalls, s_cctorGcCalls);
        while (true) System.RuntimeFailFast.Halt();
    }
}
#endif
