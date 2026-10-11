#if NOSTDLIB_EFI && M04_FILTERED_CATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{
    // Closed until original ILC filter/catch code, ABI, metadata and complete
    // callback stack have been independently measured and admitted.
    private const bool FilterTransferAdmitted = false;
    private const uint FilterMethodBytes = 295, FilterTableBytes = 11;
    private const uint FilterReturnOffset = 35, FilterContinuationOffset = 36;
    private const uint FilterFirstOffset = 60, FilterSecondOffset = 178;
    private const uint FilterFirstHandlerOffset = 127, FilterSecondHandlerOffset = 245;
    private static ulong s_filterEntries, s_filterLocal, s_filterFirst, s_filterSecond;
    private static EhControl s_filterControl;

    [System.Runtime.RuntimeImport("*", "CLROS_Filter_Clauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong FilterClauses();
    [System.Runtime.RuntimeImport("*", "CLROS_Filter_Invoke")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern int InvokeFilter(ulong target, ulong exception, X64UnwindContext* parent);
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool FilterGeometryReady() => FilterTransferAdmitted;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchFilteredExceptionType(ulong targetType, object value)
    {
        ulong reference = Bits(value);
        if (reference == 0) return false;
        if ((s_ehPhase != 4 && s_ehPhase != 5) || reference != s_ehReceipt.ObjectReference ||
            targetType != EhBaseType() || !EhObjectIntact() || !CheckEhStack()) { RejectEh(40); return false; }
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, reference, targetType);
            if (!match.Succeeded || !match.Matches || !EhObjectIntact()) { RejectEh(41); return false; }
            return match.Matches;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunFilteredCatch()
    {
        if (!BootSequence.IsFilteredCatchEnabled() || s_ehPhase != 0 || s_filterEntries != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes || sizeof(X64UnwindContext) != 240 ||
                sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32 || !CheckEhStack()) RejectEh(2);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        RhpNewFastResult root = s_ehReceipt;
        var collected = CollectGcGraph(&root, 1);
        if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
            Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact() || !ReadGcRoot().Succeeded ||
            !CollectorReceiptValid(5)) RejectEh(3);
        EhLine(1, Bits(exception), s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        s_ehPhase = 3;
        ulong result = FilterCatcher(exception);
        if (s_ehPhase != 10 || s_filterEntries != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 1 ||
            result != s_ehSeed + 1 || !EhObjectIntact() || !CheckEhStack()) RejectEh(4);
        EhLine(13, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        EhLine(14, Bits(exception), s_ehReceipt.MethodTableToken, s_filterEntries, s_ehCatchEntries);
        s_ehPhase = 11; s_ehReceipt = default;
        EhText(1); EhText(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterThrower(System.Exception value) { throw value; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong FilterCatcher(SimpleCatchBase value)
    {
        ulong stamp = 0, result = 0;
        try { FilterThrower(value); }
        catch (SimpleCatchBase caught) when (FilterDecision(caught, ref stamp, 1))
        {
            FilterCatchEntered(caught, stamp, 1);
            result = caught.Payload + 2;
        }
        catch (SimpleCatchBase caught) when (FilterDecision(caught, ref stamp, 2))
        {
            FilterCatchEntered(caught, stamp, 2);
            result = caught.Payload + 1;
        }
        FilterContinued(value, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FilterDecision(SimpleCatchBase value, ref ulong stamp, uint index)
    {
        fixed (ulong* local = &stamp)
        {
            if (index is < 1 or > 2 || s_ehPhase != 3 + index || s_filterEntries != index - 1 ||
                s_ehCatchEntries != 0 || s_ehContinuations != 0 || Bits(value) != s_ehReceipt.ObjectReference ||
                !StackSpan((ulong)local, 8) || (ulong)local != s_ehCaller.Rbp - 8 ||
                stamp != (index == 1 ? 0 : s_ehSeed + 1) ||
                (index == 2 && (ulong)local != s_filterLocal) || !EhObjectIntact() || !CheckEhStack()) RejectEh(5);
            s_filterLocal = (ulong)local; stamp = s_ehSeed + index;
            s_filterEntries = index; s_ehPhase = 4 + index;
            EhLine(index == 1 ? 5UL : 7UL, Bits(value), (ulong)local, stamp, index == 2 ? 1UL : 0UL);
            return index == 2;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterCatchEntered(SimpleCatchBase value, ulong stamp, uint index)
    {
        if (index != 2 || s_ehPhase != 7 || s_filterEntries != 2 || s_ehCatchEntries != 0 ||
            Bits(value) != s_ehReceipt.ObjectReference || stamp != s_ehSeed + 2 || !StackSpan(s_filterLocal, 8) ||
            GcReadWord(s_filterLocal) != stamp || !EhObjectIntact() || !CheckEhStack()) RejectEh(6);
        s_ehCatchEntries = 1; s_ehPhase = 8;
        EhLine(10, Bits(value), stamp, index, s_ehCatchEntries);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FilterContinued(SimpleCatchBase value, ulong result)
    {
        if (s_ehPhase != 9 || s_filterEntries != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            Bits(value) != s_ehReceipt.ObjectReference || result != s_ehSeed + 1 ||
            !EhObjectIntact() || !CheckEhStack()) RejectEh(7);
        s_ehContinuations = 1; s_ehPhase = 10;
        EhLine(12, Bits(value), value.Payload, result, s_ehContinuations);
    }

    [System.Runtime.RuntimeExport("CLROS_Filter_Select")]
    private static uint SelectFilteredCatch(ulong exception, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || exception != s_ehReceipt.ObjectReference || !CheckEhStack() || !EhObjectIntact() ||
            !BridgeLayout(captured, transfer, control) || !ControlAdmitted(control)) return RejectEh(12);
        if (!FilterGeometryReady()) return RejectEh(13);
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&FilterThrower;
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&FilterCatcher;
        if (!CaptureAdmitted((ulong)captured, captured->Rip, captured->Rsp, thrower)) return RejectEh(14);
        ulong invalidRip = captured->Rip ^ 1UL;
        bool denied = CaptureAdmitted((ulong)captured, invalidRip, captured->Rsp, thrower);
        if (denied || s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 || s_ehSelected != 0 ||
            s_ehTransferAddress != 0 || s_filterEntries != 0 || !EhObjectIntact()) return RejectEh(15);
        EhLine(2, captured->Rip, invalidRip, captured->Rsp, denied ? 1UL : 0UL);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), 10, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo) return RejectEh(16);
            var unwound = BootSequence.RunFilteredCatchUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!unwound.Succeeded || unwound.InEpilog || unwound.Caller.Rip != catcher + FilterReturnOffset ||
                unwound.Caller.Rsp != captured->Rsp + 48) return RejectEh(17);
            var metadata = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup((byte*)storage, EhStorageBytes, unwound.Caller.Rip);
            if (!metadata.Succeeded || metadata.ChainDepth != 0 || metadata.FrameRegister != 0 ||
                metadata.FrameOffset != 0 || metadata.FixedAllocationBytes != 64 || metadata.StackBytes != 72 ||
                metadata.PrologBytes != 5 || metadata.BeginRva != catcher - s_gcImage.Start ||
                metadata.EndRva != catcher - s_gcImage.Start + FilterFirstOffset ||
                unwound.Caller.Rbp != unwound.Caller.Rsp + 64 || !StackSpan(unwound.Caller.Rsp, 88) ||
                GcReadWord(unwound.Caller.Rbp + 16) != exception || GcReadWord(unwound.Caller.Rbp - 8) != 0 ||
                GcReadWord(unwound.Caller.Rbp - 16) != 0) return RejectEh(18);
            ulong table = FilterClauses();
            if (table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF) return RejectEh(19);
            var bounds = new NativeAotEhMethodBounds((uint)(catcher - s_gcImage.Start), FilterMethodBytes,
                (uint)(table - s_gcImage.Start), FilterTableBytes);
            NativeAotEhClauseResult* rows = stackalloc NativeAotEhClauseResult[2];
            BoundedExceptionFilterReceipt* receipts = stackalloc BoundedExceptionFilterReceipt[2];
            receipts[0] = default; receipts[1] = default;
            for (uint index = 0; index < 2; index++)
            {
                rows[index] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                    s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                    (byte*)storage, EhStorageBytes, bounds, index);
                if (!rows[index].Succeeded || rows[index].Index != index || rows[index].Count != 2 ||
                    rows[index].IsTryGroupBoundary || rows[index].Kind != NativeAotEhClauseKind.Filter ||
                    rows[index].TypeAddress != 0 || rows[index].TryStartOffset != 31 || rows[index].TryEndOffset != 37 ||
                    rows[index].FilterAddress != catcher + (index == 0 ? FilterFirstOffset : FilterSecondOffset) ||
                    rows[index].HandlerAddress != catcher + (index == 0 ? FilterFirstHandlerOffset : FilterSecondHandlerOffset)) return RejectEh(21);
            }
            s_ehCaller = unwound.Caller; s_filterControl = *control;
            s_filterFirst = rows[0].FilterAddress; s_filterSecond = rows[1].FilterAddress;
            EhLine(3, captured->Rip, captured->Rsp, s_ehCaller.Rip, s_ehCaller.Rsp);
            EhLine(4, s_filterFirst, s_filterSecond, rows[0].HandlerAddress, rows[1].HandlerAddress);
            X64UnwindContext parent = s_ehCaller;
            var clauses = new System.ReadOnlySpan<NativeAotEhClauseResult>(rows, 2);
            var decisions = new System.ReadOnlySpan<BoundedExceptionFilterReceipt>(receipts, 2);
            uint offset = (uint)(parent.Rip - 1 - catcher);
            for (uint index = 0; index < 2; index++)
            {
                var next = BoundedExceptionFilterSearch.Select(clauses, decisions, offset);
                if (!next.RequiresFilter || next.ClauseIndex != index || next.FilterAddress != rows[index].FilterAddress ||
                    !FilterContextMatches(&parent, false) || !EhObjectIntact() || !CheckEhStack()) return RejectEh(22);
                if (index == 0) s_ehPhase = 4;
                int returned = InvokeFilter(next.FilterAddress, exception, &parent);
                if (returned != (index == 0 ? 0 : 1) || s_ehPhase != 5 + index || s_filterEntries != index + 1 ||
                    s_ehCatchEntries != 0 || s_ehContinuations != 0 || !FilterContextMatches(&parent, false) ||
                    (ulong)captured + 816 != captured->Rsp || captured->Rip != thrower + 9 ||
                    !StackSpan(s_filterLocal, 8) || GcReadWord(s_filterLocal) != s_ehSeed + index + 1 ||
                    !EhObjectIntact() || !CheckEhStack()) return RejectEh(23);
                receipts[index] = new(index, next.FilterAddress, returned);
                EhLine(index == 0 ? 6UL : 8UL, next.FilterAddress, (ulong)(uint)returned, s_filterEntries, parent.Rsp);
            }
            var selected = BoundedExceptionFilterSearch.Select(clauses, decisions, offset);
            if (!selected.Succeeded || selected.ClauseIndex != 1 || selected.HandlerAddress != catcher + FilterSecondHandlerOffset ||
                selected.FilterAddress != 0 || !FilterContextMatches(&parent, false) || !EhObjectIntact()) return RejectEh(24);
            s_ehSelected = selected.HandlerAddress; s_ehTransferAddress = (ulong)transfer;
            *transfer = new EhTransfer { Funclet = selected.HandlerAddress, Caller = parent };
            s_ehPhase = 7;
            EhLine(9, selected.HandlerAddress, selected.ClauseIndex, parent.Rbp, s_filterLocal);
            return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Filter_ValidateContinuation")]
    private static uint ValidateFilteredContinuation(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* controls)
    {
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&FilterCatcher;
        if (s_ehPhase != 8 || s_filterEntries != 2 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            !EhObjectIntact() || !CheckEhStack() || (ulong)transfer != s_ehTransferAddress ||
            (ulong)post != (ulong)transfer + 256 || (ulong)controls != (ulong)transfer + 504 ||
            !StackSpan((ulong)transfer - 272, 840) || !ControlAdmitted(controls) || !ControlAdmitted(controls + 1) ||
            controls->Cr4 != s_filterControl.Cr4 || controls[1].Cr4 != s_filterControl.Cr4 ||
            controls->X87 != s_filterControl.X87 || controls[1].X87 != s_filterControl.X87 ||
            (controls->Mxcsr & 0xFFC0) != (s_filterControl.Mxcsr & 0xFFC0) ||
            (controls[1].Mxcsr & 0xFFC0) != (s_filterControl.Mxcsr & 0xFFC0) ||
            rip != catcher + FilterContinuationOffset || post->Rip != rip || !FilterContextMatches(post, true) ||
            transfer->Funclet != s_ehSelected || transfer->Reserved != 0 || !FilterContextMatches(&transfer->Caller, false) ||
            !StackSpan(s_filterLocal, 8) || s_filterLocal != s_ehCaller.Rbp - 8 ||
            GcReadWord(s_filterLocal) != s_ehSeed + 2 || GcReadWord(s_ehCaller.Rbp - 16) != s_ehSeed + 1) return RejectEh(20);
        EhLine(11, rip, post->Rsp, post->Rbp, s_ehCatchEntries);
        s_ehPhase = 9; return 1;
    }

    private static bool FilterContextMatches(X64UnwindContext* actual, bool afterCatch)
    {
        if (actual->Rsp != s_ehCaller.Rsp || actual->Rbp != s_ehCaller.Rbp ||
            (!afterCatch && actual->Rip != s_ehCaller.Rip)) return false;
        if (afterCatch) return true; // Catch-produced nonvolatiles survive the bridge's validated resume.
        fixed (X64UnwindContext* saved = &s_ehCaller)
        {
            ulong* left = (ulong*)actual, right = (ulong*)saved;
            for (uint word = 0; word < 30; word++) if (left[word] != right[word]) return false;
        }
        return true;
    }
}
#endif
