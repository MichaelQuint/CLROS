#if NOSTDLIB_EFI && M04_UNHANDLED_EXCEPTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal sealed class SimpleCatchSibling : SimpleCatchBase
{
    internal SimpleCatchSibling(ulong payload) : base(payload) { }
}

internal static unsafe partial class RhpNewFastAllocation
{
    // Exact original compiler method/EH/unwind contributions are admitted by
    // the native verifier. Callback stack review still precedes every guest run.
    private const bool UnhandledSearchAdmitted = true;
    private const uint UnhandledMiddleBytes = 46, UnhandledMiddleReturn = 35;
    private const uint UnhandledCatchBytes = 56, UnhandledCatchReturn = 15, UnhandledTableBytes = 8;
    private static RhpNewFastResult s_unhandledRoot;
    private static ulong s_unhandledMiddleEntries;

    [System.Runtime.RuntimeImport("*", "CLROS_Eh_GetClauseTable")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong UnhandledClauseTable();

    [System.Runtime.RuntimeImport("*", "RhTypeCast_IsInstanceOfException")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern bool RuntimeExceptionMatch(Internal.Runtime.MethodTable* target, object value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunUnhandledException()
    {
        if (!BootSequence.IsUnhandledExceptionEnabled() || s_ehPhase != 0 ||
            s_unhandledRoot.ObjectReference != 0 || s_unhandledMiddleEntries != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectUnhandled(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        if (!CheckEhStack()) RejectUnhandled(2);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes ||
                sizeof(X64UnwindContext) != 240 || sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32) RejectUnhandled(3);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact()) RejectUnhandled(4);
        s_unhandledRoot = s_ehReceipt;
        fixed (RhpNewFastResult* root = &s_unhandledRoot)
        {
            var collected = CollectGcGraph(root, 1);
            if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
                !UnhandledRootRetained(Bits(exception)) || !EhObjectIntact() ||
                !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectUnhandled(5);
            EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        }
        bool same = RuntimeExceptionMatch((Internal.Runtime.MethodTable*)EhDerivedType(), exception);
        bool ancestor = RuntimeExceptionMatch((Internal.Runtime.MethodTable*)EhBaseType(), exception);
        bool sibling = RuntimeExceptionMatch((Internal.Runtime.MethodTable*)UnhandledSiblingType(), exception);
        bool nil = RuntimeExceptionMatch(null, null);
        if (!same || !ancestor || sibling || nil || !UnhandledRootRetained(Bits(exception)) || !EhObjectIntact()) RejectUnhandled(6);
        EhLine(2, same ? 1UL : 0UL, ancestor ? 1UL : 0UL, sibling ? 1UL : 0UL, nil ? 1UL : 0UL);
        s_ehPhase = 3;
        Catcher(exception);
        RejectUnhandled(7);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong UnhandledSiblingType() => (ulong)Internal.Runtime.MethodTable.Of<SimpleCatchSibling>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Thrower(System.Exception value) { throw value; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong UnhandledMiddle(SimpleCatchBase value)
    {
        ulong stamp = value.Payload;
        UnhandledMarkFrame(ref stamp);
        Thrower(value);
        return stamp;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnhandledMarkFrame(ref ulong stamp)
    {
        if (s_ehPhase != 3 || s_unhandledMiddleEntries != 0 || stamp != s_ehSeed ||
            !CheckEhStack() || !EhObjectIntact()) RejectUnhandled(8);
        s_unhandledMiddleEntries = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong Catcher(SimpleCatchBase value)
    {
        try { return UnhandledMiddle(value); }
        catch (SimpleCatchSibling sibling)
        {
            UnhandledUnexpectedCatch(sibling);
            return 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnhandledUnexpectedCatch(SimpleCatchSibling value) { RejectUnhandled(9); }

    private static bool UnhandledRootRetained(ulong reference) => reference != 0 &&
        s_unhandledRoot.AllocationSucceeded && s_ehReceipt.AllocationSucceeded &&
        s_unhandledRoot.ObjectReference == reference && s_ehReceipt.ObjectReference == reference &&
        s_unhandledRoot.MethodTableToken == s_ehReceipt.MethodTableToken &&
        s_unhandledRoot.BytesConsumed == s_ehReceipt.BytesConsumed &&
        s_unhandledRoot.AllocationCount == s_ehReceipt.AllocationCount &&
        s_unhandledRoot.UsedBefore == s_ehReceipt.UsedBefore && s_unhandledRoot.UsedAfter == s_ehReceipt.UsedAfter &&
        s_unhandledRoot.MappedAllocationStart == s_ehReceipt.MappedAllocationStart &&
        s_unhandledRoot.MappedAllocationEndExclusive == s_ehReceipt.MappedAllocationEndExclusive &&
        s_unhandledRoot.PhysicalAllocationStart == s_ehReceipt.PhysicalAllocationStart &&
        s_unhandledRoot.PhysicalAllocationEndExclusive == s_ehReceipt.PhysicalAllocationEndExclusive;

    [System.Runtime.RuntimeExport("CLROS_Eh_Select")]
    private static uint SelectUnhandled(ulong exception, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || s_unhandledMiddleEntries != 1 || !CheckEhStack() || !EhObjectIntact() ||
            !UnhandledRootRetained(exception) || !BridgeLayout(captured, transfer, control) || !ControlAdmitted(control))
            return RejectUnhandled(10);
        // No handler/context is ever exposed to the existing bridge on this path.
        *transfer = default;
        if (!UnhandledSearchAdmitted) return RejectUnhandled(11);
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&Thrower;
        ulong middle = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&UnhandledMiddle;
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&Catcher;
        if (!CaptureAdmitted((ulong)captured, captured->Rip, captured->Rsp, thrower) ||
            s_ehCatchEntries != 0 || s_ehContinuations != 0 || s_ehSelected != 0 || s_ehTransferAddress != 0)
            return RejectUnhandled(12);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var first = BootSequence.RunUnhandledExceptionUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!first.Succeeded || first.InEpilog || first.Caller.Rip != middle + UnhandledMiddleReturn)
                return RejectUnhandled(13);
            var second = BootSequence.RunUnhandledExceptionUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, first.Caller);
            if (!second.Succeeded || second.InEpilog || second.Caller.Rip != catcher + UnhandledCatchReturn)
                return RejectUnhandled(14);
            EhLine(3, captured->Rip, captured->Rsp, first.Caller.Rip, first.Caller.Rsp);
            EhLine(4, first.Caller.Rip, first.Caller.Rsp, second.Caller.Rip, second.Caller.Rsp);
            var noThrow = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), EhThrowCodeBytes, 0, 0), 0);
            var noMiddle = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(middle - s_gcImage.Start), UnhandledMiddleBytes, 0, 0), 0);
            ulong table = UnhandledClauseTable();
            if (noThrow.Status != NativeAotEhClauseStatus.NoEhInfo || noMiddle.Status != NativeAotEhClauseStatus.NoEhInfo ||
                table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF ||
                catcher < s_gcImage.Start || catcher - s_gcImage.Start > 0x7FFFFFFF) return RejectUnhandled(15);
            NativeAotEhClauseResult* rows = stackalloc NativeAotEhClauseResult[1];
            ExceptionTypeMatchResult* matches = stackalloc ExceptionTypeMatchResult[1];
            rows[0] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(catcher - s_gcImage.Start), UnhandledCatchBytes,
                    (uint)(table - s_gcImage.Start), UnhandledTableBytes), 0);
            if (!rows[0].Succeeded || rows[0].TypeAddress != UnhandledSiblingType()) return RejectUnhandled(16);
            matches[0] = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, rows[0].TypeAddress);
            bool exported = RuntimeExceptionMatch((Internal.Runtime.MethodTable*)rows[0].TypeAddress, UnhandledObject(exception));
            if (!matches[0].Succeeded || matches[0].Matches || exported) return RejectUnhandled(17);
            BoundedExceptionSearchFrame* frames = stackalloc BoundedExceptionSearchFrame[3];
            frames[0] = new(*captured, EhThrowReturnOffset - 1, EhThrowCodeBytes, 0, 0);
            frames[1] = new(first.Caller, UnhandledMiddleReturn - 1, UnhandledMiddleBytes, 0, 0);
            frames[2] = new(second.Caller, UnhandledCatchReturn - 1, UnhandledCatchBytes, 0, 1);
            BoundedExceptionCleanupEntry* cleanups = stackalloc BoundedExceptionCleanupEntry[8];
            var plan = BoundedExceptionFrameSearch.Select(new(frames, 3), new(rows, 1), new(matches, 1), new(cleanups, 8));
            if (!UnhandledNoMatch(plan, cleanups)) return RejectUnhandled(18);
            var original = frames[2]; frames[2] = new(original.Context, original.CodeBytes, original.CodeBytes, 0, 1);
            var bad = BoundedExceptionFrameSearch.Select(new(frames, 3), new(rows, 1), new(matches, 1), new(cleanups, 8));
            frames[2] = original;
            if (bad.Status != BoundedExceptionFrameSearchStatus.InvalidFrame || !UnhandledEmptyPlan(bad, cleanups))
                return RejectUnhandled(19);
            var originalRow = rows[0]; rows[0] = default;
            bad = BoundedExceptionFrameSearch.Select(new(frames, 3), new(rows, 1), new(matches, 1), new(cleanups, 8));
            rows[0] = originalRow;
            if (bad.Status != BoundedExceptionFrameSearchStatus.DispatchRejected || !UnhandledEmptyPlan(bad, cleanups))
                return RejectUnhandled(20);
            plan = BoundedExceptionFrameSearch.Select(new(frames, 3), new(rows, 1), new(matches, 1), new(cleanups, 8));
            if (!UnhandledNoMatch(plan, cleanups) || !UnhandledRootRetained(exception) || !EhObjectIntact() ||
                !CheckEhStack() || s_ehCatchEntries != 0 || s_ehContinuations != 0) return RejectUnhandled(21);
            EhLine(5, rows[0].TypeAddress, rows[0].HandlerAddress, second.ReturnSlot, 0);
            EhLine(6, 3, (ulong)plan.Status, plan.CatchHandler, plan.CleanupCount);
            EhLine(7, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        }
        s_ehPhase = 4;
        UnhandledText("TEST_FAIL m04.021.unhandled unhandled-exception\r\n");
        UnhandledText("OS_SMOKE_FAIL subsystem=runtime reason=unhandled-exception\r\n");
        // The reviewed existing bridge takes its zero-selector rejection edge
        // into the terminal loop; no second-pass cleanup/handler runs.
        return 0;
    }

    [System.Runtime.RuntimeExport("CLROS_Eh_ValidateContinuation")]
    private static uint UnhandledContinuation(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* control) => RejectUnhandled(22);

    private static bool UnhandledNoMatch(BoundedExceptionFrameSearchResult plan, BoundedExceptionCleanupEntry* cleanups) =>
        plan.Status == BoundedExceptionFrameSearchStatus.NoMatchingCatch && UnhandledEmptyPlan(plan, cleanups);

    private static bool UnhandledEmptyPlan(BoundedExceptionFrameSearchResult plan, BoundedExceptionCleanupEntry* cleanups)
    {
        if (plan.Succeeded || plan.CatchFrameIndex != 0 || plan.CatchClauseIndex != 0 || plan.CatchHandler != 0 ||
            !UnhandledContextEmpty(plan.CatchContext) || plan.CleanupCount != 0) return false;
        for (int i = 0; i < 8; i++) if (cleanups[i].Handler != 0 || cleanups[i].FrameIndex != 0 ||
            !UnhandledContextEmpty(cleanups[i].Context)) return false;
        return true;
    }

    private static bool UnhandledContextEmpty(X64UnwindContext context)
    {
        for (int i = 0; i < sizeof(X64UnwindContext) / 8; i++) if (((ulong*)&context)[i] != 0) return false;
        return true;
    }

    // The original retained allocation was checked before this bit-preserving
    // local alias; it cannot manufacture a new object or extend its root lifetime.
    private static object UnhandledObject(ulong reference)
    {
        nuint native = (nuint)reference;
        return Unsafe.As<nuint, object>(ref native);
    }

    private static void UnhandledText(string text)
    {
        for (int i = 0; i < text.Length; i++) Write((byte)text[i]);
    }

    private static uint RejectUnhandled(ulong reason)
    {
        EhText(3); Decimal(reason); End();
        UnhandledText("OS_SMOKE_FAIL subsystem=runtime reason=unhandled-policy-rejected\r\n");
        while (true) System.RuntimeFailFast.Halt();
    }
}
#endif
