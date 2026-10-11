#if NOSTDLIB_EFI && M04_NESTED_FINALLY_CATCH_PROBE
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
    // Measured at nested-eh-native-layout-r01/r03 and checked by strict PE
    // code/clause/unwind controls. Direct callback paths including the bridge
    // use at most 10,056 bytes below entry; the existing 16,400-byte stack floor
    // also retains the inherited diagnostic firmware writer margin. Only this
    // exact four-row fixture may transfer; default profiles compile it out.
    private const bool NestedTransferAdmitted = true;
    private const uint NestedCodeBytes = 222, NestedTableBytes = 26, NestedReturn = 32, NestedContinuation = 73;
    private const uint NestedFirstCleanup = 101, NestedSecondCleanup = 130, NestedWrongCatch = 159, NestedCatch = 180;
    private const uint NestedTryStart = 27, NestedInnerEnd = 33, NestedOuterEnd = 53, NestedCatchEnd = 73;
    private const uint NestedRootAllocation = 56, NestedRootStack = 72, NestedRootProlog = 6, NestedBpFromSp = 64;
    private const uint NestedStampOffset = 24, NestedResultOffset = 16;
#pragma warning disable CS0649
    private struct NestedTransfer
    {
        internal ulong Funclet, FirstCleanup, SecondCleanup, Reserved;
        internal X64UnwindContext Caller;
    }
#pragma warning restore CS0649
    private static EhControl s_nestedEntryControl;
    private static ulong s_nestedFirst, s_nestedSecond, s_nestedCleanups, s_nestedLocal;

    [System.Runtime.RuntimeImport("*", "CLROS_Nested_Clauses")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong NestedClauses();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool NestedGeometryReady() => NestedTransferAdmitted;
    [MethodImpl(MethodImplOptions.NoInlining)] private static uint NestedCodeExtent() => NestedCodeBytes;
    [MethodImpl(MethodImplOptions.NoInlining)] private static uint NestedTableExtent() => NestedTableBytes;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong NestedSiblingType() => (ulong)Internal.Runtime.MethodTable.Of<SimpleCatchSibling>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunNestedFinallyCatch()
    {
        if (!BootSequence.IsNestedFinallyCatchEnabled() || s_ehPhase != 0 || s_nestedCleanups != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        if (!CheckEhStack()) RejectEh(2);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes || sizeof(X64UnwindContext) != 240 ||
                sizeof(NestedTransfer) != 272 || sizeof(EhControl) != 32) RejectEh(3);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact()) RejectEh(4);
        RhpNewFastResult root = s_ehReceipt;
        var collected = CollectGcGraph(&root, 1);
        if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
            !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(5);
        EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        s_ehPhase = 3;
        ulong result = NestedCatcher(exception);
        if (s_ehPhase != 10 || s_nestedCleanups != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 1 ||
            result != s_ehSeed + 1 || !EhObjectIntact() || !CheckEhStack()) RejectEh(6);
        // NestedContinued checks the parent locals while its frame is live.
        // After return, use the completed phase/receipt rather than expired slots.
        EhLine(13, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        EhLine(15, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_gcHandle, s_ehReceipt.AllocationCount);
        // Explicit root lifetime ends after the real compiled continuation.
        s_ehPhase = 11; s_ehReceipt = default;
        EhText(1); EhText(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NestedThrower(System.Exception value) { throw value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong NestedCatcher(SimpleCatchBase value)
    {
        ulong result = 0, stamp = 0;
        try
        {
            try
            {
                try { NestedThrower(value); }
                finally { NestedCleanupEntered(value, ref stamp, 1); }
            }
            finally { NestedCleanupEntered(value, ref stamp, 2); }
        }
        catch (SimpleCatchSibling wrong) { NestedUnexpectedCatch(wrong); }
        catch (SimpleCatchBase caught)
        {
            NestedCatchEntered(caught, stamp);
            result = caught.Payload + 1;
        }
        NestedContinued(value, result, stamp);
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NestedUnexpectedCatch(SimpleCatchSibling value) { RejectEh(40); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NestedCleanupEntered(SimpleCatchBase value, ref ulong stamp, uint ordinal)
    {
        fixed (ulong* local = &stamp)
        {
            if (ordinal < 1 || ordinal > 2 || s_ehPhase != (ordinal == 1 ? 4UL : 6UL) ||
                s_nestedCleanups != ordinal - 1 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
                !StackSpan((ulong)local, 8) || (ulong)local != s_ehCaller.Rbp - NestedStampOffset ||
                stamp != (ordinal == 1 ? 0UL : s_ehSeed + 2) || Bits(value) != s_ehReceipt.ObjectReference ||
                value.Payload != s_ehSeed || !EhObjectIntact() || !CheckEhStack()) RejectEh(23);
            s_nestedLocal = (ulong)local; stamp = s_ehSeed + 1 + ordinal;
            s_nestedCleanups = ordinal; s_ehPhase = ordinal == 1 ? 5UL : 7UL;
            EhLine(ordinal == 1 ? 6U : 8U, Bits(value), (ulong)local, stamp, ordinal);
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NestedCatchEntered(SimpleCatchBase value, ulong stamp)
    {
        if (s_ehPhase != 8 || s_nestedCleanups != 2 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
            stamp != s_ehSeed + 3 || !NestedStampIntact(2) || Bits(value) != s_ehReceipt.ObjectReference ||
            value.Payload != s_ehSeed || !EhObjectIntact() || !CheckEhStack()) RejectEh(24);
        s_ehCatchEntries = 1; s_ehPhase = 9;
        EhLine(10, Bits(value), value.Payload, stamp, s_ehCatchEntries);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NestedContinued(SimpleCatchBase value, ulong result, ulong stamp)
    {
        if (s_ehPhase != 10 || s_nestedCleanups != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            result != s_ehSeed + 1 || stamp != s_ehSeed + 3 || Bits(value) != s_ehReceipt.ObjectReference ||
            !NestedStampIntact(2) || !EhObjectIntact() || !CheckEhStack()) RejectEh(25);
        s_ehContinuations = 1;
        EhLine(12, Bits(value), value.Payload, result, s_ehContinuations);
    }

    [System.Runtime.RuntimeExport("CLROS_Nested_Select")]
    private static uint NestedSelect(ulong exception, X64UnwindContext* captured, NestedTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || !CheckEhStack() || !EhObjectIntact() || exception != s_ehReceipt.ObjectReference ||
            !NestedBridgeLayout(captured, transfer, control) || !ControlAdmitted(control)) return RejectEh(12);
        if (!NestedGeometryReady()) return RejectEh(13);
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NestedCatcher;
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&NestedThrower;
        ulong originalRip = captured->Rip, originalRsp = captured->Rsp;
        if (!NestedCaptureAdmitted((ulong)captured, originalRip, originalRsp, thrower) ||
            s_ehSelected != 0 || s_ehTransferAddress != 0 || s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 ||
            s_nestedFirst != 0 || s_nestedSecond != 0 || s_nestedLocal != 0 || s_nestedCleanups != 0 ||
            s_ehCatchEntries != 0 || s_ehContinuations != 0) return RejectEh(14);
        ulong invalidRip = originalRip ^ 1UL;
        if (NestedCaptureAdmitted((ulong)captured, invalidRip, originalRsp, thrower) ||
            captured->Rip != originalRip || captured->Rsp != originalRsp) return RejectEh(21);
        EhLine(2, originalRip, invalidRip, originalRsp, 0);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), 10, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo) return RejectEh(15);
            var unwind = BootSequence.RunNestedFinallyCatchUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!unwind.Succeeded || unwind.InEpilog || unwind.Caller.Rip != method + NestedReturn ||
                unwind.Caller.Rsp != captured->Rsp + 48 || unwind.Caller.Rbp != unwind.Caller.Rsp + NestedBpFromSp ||
                !StackSpan(unwind.Caller.Rsp, NestedRootStack + 16) ||
                !NestedFunction((byte*)storage, method, 0, NestedFirstCleanup, NestedRootAllocation, NestedRootStack, NestedRootProlog) ||
                GcReadWord(unwind.Caller.Rbp + 16) != exception ||
                GcReadWord(unwind.Caller.Rbp - NestedStampOffset) != 0 ||
                GcReadWord(unwind.Caller.Rbp - NestedResultOffset) != 0) return RejectEh(16);
            ulong table = NestedClauses();
            uint codeBytes = NestedCodeExtent(), tableBytes = NestedTableExtent();
            if (method < s_gcImage.Start || method - s_gcImage.Start > 0x7FFFFFFF ||
                table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF ||
                codeBytes == 0 || tableBytes == 0) return RejectEh(18);
            NativeAotEhMethodBounds bounds = new((uint)(method - s_gcImage.Start), codeBytes,
                (uint)(table - s_gcImage.Start), tableBytes);
            NativeAotEhClauseResult* rows = stackalloc NativeAotEhClauseResult[4];
            ExceptionTypeMatchResult* matches = stackalloc ExceptionTypeMatchResult[4];
            for (uint i = 0; i < 4; i++)
            {
                rows[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                    s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                    (byte*)storage, EhStorageBytes, bounds, i);
                matches[i] = default;
                if (rows[i].Succeeded && rows[i].Kind == NativeAotEhClauseKind.Typed)
                    matches[i] = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                        s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                        s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, rows[i].TypeAddress);
            }
            uint pc = (uint)(unwind.Caller.Rip - 1 - method);
            var plan = BoundedExceptionDispatchPlan.Select(new(rows, 4), new(matches, 4), pc);
            if (!plan.Succeeded || plan.CatchIndex != 3 || plan.CleanupCount != 2 ||
                plan.FirstCleanup != method + NestedFirstCleanup || plan.SecondCleanup != method + NestedSecondCleanup ||
                plan.CatchHandler != method + NestedCatch || !NestedRowsMatch(rows, method) ||
                !matches[2].Succeeded || matches[2].Matches || !matches[3].Succeeded || !matches[3].Matches ||
                !EhObjectIntact()) return RejectEh(19);
            // Only a local decoded row is changed. Original metadata/capture/context stay intact.
            var saved = rows[3]; rows[3] = default;
            var rejected = BoundedExceptionDispatchPlan.Select(new(rows, 4), new(matches, 4), pc);
            rows[3] = saved;
            if (rejected.Status != BoundedExceptionDispatchStatus.InvalidClause || rejected.Succeeded ||
                rejected.CatchHandler != 0 || rejected.FirstCleanup != 0 || rejected.SecondCleanup != 0) return RejectEh(22);
            EhLine(14, (ulong)rejected.Status, rejected.CatchHandler, rejected.FirstCleanup, rejected.SecondCleanup);
            if (!NestedFunction((byte*)storage, method, NestedFirstCleanup, NestedSecondCleanup, 40, 40, 4) ||
                !NestedFunction((byte*)storage, method, NestedSecondCleanup, NestedWrongCatch, 40, 40, 4) ||
                !NestedFunction((byte*)storage, method, NestedWrongCatch, NestedCatch, 40, 40, 4) ||
                !NestedFunction((byte*)storage, method, NestedCatch, NestedCodeBytes, 40, 40, 4)) return RejectEh(26);
            s_ehCaller = unwind.Caller; s_ehSelected = plan.CatchHandler; s_ehTransferAddress = (ulong)transfer;
            s_nestedFirst = plan.FirstCleanup; s_nestedSecond = plan.SecondCleanup;
            s_nestedEntryControl = new EhControl { Flags = control->Flags, Cr4 = control->Cr4, Mxcsr = control->Mxcsr, X87 = control->X87 };
            *transfer = new NestedTransfer { Funclet = plan.CatchHandler, FirstCleanup = plan.FirstCleanup,
                SecondCleanup = plan.SecondCleanup, Caller = unwind.Caller };
            EhLine(3, captured->Rip, captured->Rsp, unwind.Caller.Rip, unwind.Caller.Rsp);
            EhLine(4, (ulong)matches[2].Status, matches[2].Matches ? 1UL : 0UL, (ulong)matches[3].Status, matches[3].Matches ? 1UL : 0UL);
            EhLine(5, plan.FirstCleanup, plan.SecondCleanup, plan.CatchHandler, unwind.Caller.Rbp - NestedStampOffset);
            s_ehPhase = 4; return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Nested_ValidateCleanup")]
    private static uint NestedValidateCleanup(NestedTransfer* transfer, EhControl* entry, EhControl* post)
    {
        if (s_nestedCleanups < 1 || s_nestedCleanups > 2 || s_ehPhase != (s_nestedCleanups == 1 ? 5UL : 7UL) ||
            s_ehCatchEntries != 0 || s_ehContinuations != 0 || !NestedBridgeIntact(transfer, entry, post) ||
            !NestedTransferIntact(transfer) || !NestedControlsIntact(entry, post) || !NestedStampIntact(s_nestedCleanups) ||
            !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) || !CheckEhStack()) return RejectEh(27);
        EhLine(s_nestedCleanups == 1 ? 7U : 9U, s_nestedFirst, s_nestedSecond, s_nestedLocal, GcReadWord(s_nestedLocal));
        s_ehPhase = s_nestedCleanups == 1 ? 6UL : 8UL; return 1;
    }
    [System.Runtime.RuntimeExport("CLROS_Nested_ValidateContinuation")]
    private static uint NestedValidateContinuation(ulong rip, X64UnwindContext* post, NestedTransfer* transfer, EhControl* entry)
    {
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NestedCatcher;
        if (s_ehPhase != 9 || s_nestedCleanups != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            !NestedBridgeIntact(transfer, entry, entry + 1) || (ulong)post != (ulong)transfer + 272 ||
            !NestedTransferIntact(transfer) || !NestedControlsIntact(entry, entry + 1) || !NestedStampIntact(2) ||
            !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) || !CheckEhStack() ||
            rip != method + NestedContinuation || post->Rip != rip || post->Rbx != s_ehReceipt.ObjectReference ||
            !NestedContextIntact(post, true) || post->Rsp <= (ulong)transfer + 592 || post->Rsp > s_gcStack.InitialRsp ||
            GcReadWord(s_ehCaller.Rbp - NestedResultOffset) != s_ehSeed + 1) return RejectEh(28);
        EhLine(11, rip, post->Rsp, post->Rbp, s_ehCatchEntries); s_ehPhase = 10; return 1;
    }

    private static bool NestedRowsMatch(NativeAotEhClauseResult* rows, ulong method) =>
        rows[0].TryStartOffset == NestedTryStart && rows[0].TryEndOffset == NestedInnerEnd &&
        rows[1].TryStartOffset == NestedTryStart && rows[1].TryEndOffset == NestedOuterEnd &&
        rows[2].TryStartOffset == NestedTryStart && rows[2].TryEndOffset == NestedCatchEnd &&
        rows[3].TryStartOffset == NestedTryStart && rows[3].TryEndOffset == NestedCatchEnd &&
        rows[2].HandlerAddress == method + NestedWrongCatch && rows[2].TypeAddress == NestedSiblingType() &&
        rows[3].TypeAddress == EhBaseType();
    private static bool NestedFunction(byte* storage, ulong method, uint begin, uint end, uint allocation, uint stack, uint prolog)
    {
        var row = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(storage, EhStorageBytes, method + begin);
        return row.Succeeded && row.ChainDepth == 0 && row.FrameRegister == 0 && row.FrameOffset == 0 &&
            row.FixedAllocationBytes == allocation && row.StackBytes == stack && row.PrologBytes == prolog &&
            row.BeginRva == method - s_gcImage.Start + begin && row.EndRva == method - s_gcImage.Start + end;
    }
    private static bool NestedCaptureAdmitted(ulong address, ulong rip, ulong rsp, ulong thrower) =>
        rip == thrower + 9 && rsp == address + 832 && rip != 0 &&
        rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;
    private static bool NestedBridgeLayout(X64UnwindContext* input, NestedTransfer* output, EhControl* controls) =>
        (ulong)input >= 32 && ((ulong)input & 15) == 0 && StackSpan((ulong)input - 32, 864) &&
        (ulong)output == (ulong)input + 240 && (ulong)controls == (ulong)input + 760;
    private static bool NestedBridgeIntact(NestedTransfer* transfer, EhControl* entry, EhControl* post) =>
        s_ehTransferAddress != 0 && (ulong)transfer == s_ehTransferAddress && ((ulong)transfer & 15) == 0 &&
        (ulong)transfer >= 272 && StackSpan((ulong)transfer - 272, 864) &&
        (ulong)entry == (ulong)transfer + 520 && (ulong)post == (ulong)transfer + 552;
    private static bool NestedTransferIntact(NestedTransfer* transfer) =>
        transfer->Funclet == s_ehSelected && transfer->FirstCleanup == s_nestedFirst &&
        transfer->SecondCleanup == s_nestedSecond && transfer->Reserved == 0 && NestedContextIntact(&transfer->Caller, false);
    private static bool NestedContextIntact(X64UnwindContext* actual, bool afterCatch)
    {
        fixed (X64UnwindContext* saved = &s_ehCaller)
            for (int word = afterCatch ? 1 : 0; word < 30; word++)
                if (!(afterCatch && word == 2) && ((ulong*)actual)[word] != ((ulong*)saved)[word]) return false;
        return true;
    }
    private static bool NestedControlsIntact(EhControl* entry, EhControl* post) =>
        ControlAdmitted(entry) && ControlAdmitted(post) && entry->Cr4 == s_nestedEntryControl.Cr4 &&
        entry->Mxcsr == s_nestedEntryControl.Mxcsr && entry->X87 == s_nestedEntryControl.X87 &&
        ((entry->Flags ^ s_nestedEntryControl.Flags) & 0x600) == 0 &&
        post->Cr4 == entry->Cr4 && post->Mxcsr == entry->Mxcsr && post->X87 == entry->X87 &&
        ((post->Flags ^ entry->Flags) & 0x600) == 0;
    private static bool NestedStampIntact(ulong ordinal) =>
        s_nestedLocal == s_ehCaller.Rbp - NestedStampOffset && StackSpan(s_nestedLocal, 8) &&
        GcReadWord(s_nestedLocal) == s_ehSeed + 1 + ordinal &&
        GcReadWord(s_ehCaller.Rbp + 16) == s_ehReceipt.ObjectReference;
}
#endif
