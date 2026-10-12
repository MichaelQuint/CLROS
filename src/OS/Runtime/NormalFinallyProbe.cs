#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal sealed class NormalCatchSibling : SimpleCatchBase
{
    internal NormalCatchSibling(ulong payload) : base(payload) { }
}

internal static unsafe partial class RhpNewFastAllocation
{
    // Geometry from step1190 and scoped native/identity controls in #674–#676.
    // The reviewed six callback roots fit 14,592 first-party bytes below the
    // captured thrower, within the retained 16,400-byte floor. Republish and
    // review each changed image before guest execution; general EH is absent.
    private const bool NormalTransferAdmitted = true;
    private const uint NormalCodeBytes = 173, NormalTableBytes = 11;
    private const uint NormalFinallyOffset = 89, NormalCatchOffset = 121;
    private const uint NormalThrowReturn = 9, NormalFinallyReturn = 115;
    private const uint NormalParentReturn = 61, NormalContinuation = 62;
    private static ulong s_normalFinallyEntries, s_normalCatchEntries, s_normalContinuations;
    private static ulong s_normalWitness, s_normalLocal, s_normalSelections;
    private static RhpNewFastResult s_normalRoot;
    private static EhControl s_normalEntryControl;

    [System.Runtime.RuntimeImport("*", "CLROS_Eh_CatalogStart")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong NormalCatalogStart();
    [System.Runtime.RuntimeImport("*", "CLROS_Eh_CatalogEnd")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong NormalCatalogEnd();
    [System.Runtime.RuntimeImport("*", "CLROS_NormalFinally_Clauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong NormalClauses();
    [System.Runtime.RuntimeImport("*", "RhTypeCast_IsInstanceOfException")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern bool NormalRuntimeMatch(Internal.Runtime.MethodTable* target, object value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool NormalGeometryReady() => NormalTransferAdmitted;

    [System.Runtime.RuntimeExport("CLROS_NormalFinally_CompilerAddress")]
    private static ulong NormalCompilerAddress() =>
        (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NormalCatcher;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunNormalFinally()
    {
        if (!BootSequence.IsNormalFinallyEnabled() || s_ehPhase != 0 || s_normalRoot.ObjectReference != 0 ||
            s_normalFinallyEntries != 0 || s_normalCatchEntries != 0 || s_normalContinuations != 0 ||
            s_normalLocal != 0 || s_normalSelections != 0 || s_gcPhase != 5 || s_collectorPhase != 7 ||
            s_gcStack.Checks != 10 || s_control.ConstructorCount != 7 ||
            !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        if (!CheckEhStack()) RejectEh(2);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes || sizeof(X64UnwindContext) != 240 ||
                sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32) RejectEh(3);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact()) RejectEh(4);
        s_normalRoot = s_ehReceipt;
        fixed (RhpNewFastResult* root = &s_normalRoot)
        {
            var collected = CollectGcGraph(root, 1);
            if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
                !NormalRootRetained(Bits(exception)) || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(5);
            EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        }
        bool same = NormalRuntimeMatch((Internal.Runtime.MethodTable*)EhDerivedType(), exception);
        bool ancestor = NormalRuntimeMatch((Internal.Runtime.MethodTable*)EhBaseType(), exception);
        bool sibling = NormalRuntimeMatch(Internal.Runtime.MethodTable.Of<NormalCatchSibling>(), exception);
        bool nil = NormalRuntimeMatch(null, null);
        if (!same || !ancestor || sibling || nil || !NormalRootRetained(Bits(exception))) RejectEh(6);
        EhLine(2, same ? 1UL : 0UL, ancestor ? 1UL : 0UL, sibling ? 1UL : 0UL, nil ? 1UL : 0UL);
        s_ehPhase = 3;
        ulong result = NormalCatcher(exception);
        if (s_ehPhase != 8 || s_normalFinallyEntries != 1 || s_normalSelections != 1 ||
            s_normalCatchEntries != 1 || s_normalContinuations != 1 || result != s_ehSeed + 1 ||
            !NormalRootRetained(Bits(exception)) || !CheckEhStack()) RejectEh(7);
        // The root method has returned: do not read its expired parent-local slots.
        EhLine(13, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        EhLine(14, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_gcHandle, s_ehReceipt.AllocationCount);
        s_ehPhase = 9; s_normalRoot = default; s_ehReceipt = default;
        EhText(1); EhText(2);
    }

    // CLR MethodImpl NoOptimization=0x40; the minimal enum has no named member.
    // Without it ILC clones the normal finally into this root, leaving no
    // normal-call funclet frame for RecoverNormalFinallyParent to recover.
    [MethodImpl(MethodImplOptions.NoInlining | (MethodImplOptions)0x40)]
    private static ulong NormalCatcher(SimpleCatchBase value)
    {
        ulong stamp = 0, result = 0;
        try
        {
            try { stamp = NormalBody(value); }
            finally
            {
                NormalFinallyEntered(value, ref stamp);
                NormalThrower(value);
            }
        }
        catch (SimpleCatchBase caught)
        {
            NormalCatchEntered(caught, stamp);
            result = caught.Payload + 1;
        }
        NormalContinued(value, result, stamp);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong NormalBody(SimpleCatchBase value) => value.Payload + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalThrower(System.Exception value) { throw value; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalFinallyEntered(SimpleCatchBase value, ref ulong stamp)
    {
        fixed (ulong* local = &stamp)
        {
            if (s_ehPhase != 3 || s_normalFinallyEntries != 0 || s_normalCatchEntries != 0 ||
                s_normalContinuations != 0 || stamp != s_ehSeed + 1 || value.Payload != s_ehSeed ||
                !StackSpan((ulong)local, 8) || !NormalRootRetained(Bits(value)) || !CheckEhStack()) RejectEh(61);
            stamp = value.Payload + 2;
            s_normalFinallyEntries = 1; s_normalWitness = stamp; s_normalLocal = (ulong)local;
            s_ehPhase = 4; EhLine(3, Bits(value), (ulong)local, stamp, s_normalFinallyEntries);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalCatchEntered(SimpleCatchBase value, ulong stamp)
    {
        if (s_ehPhase != 5 || s_normalFinallyEntries != 1 || s_normalCatchEntries != 0 ||
            s_normalContinuations != 0 || stamp != s_ehSeed + 2 || !NormalLocalsIntact(false) ||
            !NormalRootRetained(Bits(value)) || value.Payload != s_ehSeed || !CheckEhStack()) RejectEh(62);
        s_normalCatchEntries = 1; s_ehPhase = 6;
        EhLine(9, Bits(value), value.Payload, stamp, s_normalCatchEntries);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NormalContinued(SimpleCatchBase value, ulong result, ulong stamp)
    {
        if (s_ehPhase != 7 || s_normalFinallyEntries != 1 || s_normalCatchEntries != 1 ||
            s_normalContinuations != 0 || result != s_ehSeed + 1 || stamp != s_ehSeed + 2 ||
            !NormalLocalsIntact(true) || !NormalRootRetained(Bits(value)) || !CheckEhStack()) RejectEh(63);
        s_normalContinuations = 1; s_ehPhase = 8;
        EhLine(11, Bits(value), value.Payload, result, s_normalContinuations);
        EhLine(12, s_normalLocal, stamp, s_normalFinallyEntries, s_normalSelections);
    }

    [System.Runtime.RuntimeExport("CLROS_NormalFinally_Select")]
    private static uint NormalSelect(ulong exception, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 4 || s_normalFinallyEntries != 1 || s_normalSelections != 0 ||
            s_normalCatchEntries != 0 || s_normalContinuations != 0 || !BridgeLayout(captured, transfer, control) ||
            !ControlAdmitted(control) || !NormalRootRetained(exception) || !CheckEhStack()) return RejectEh(12);
        if (!NormalGeometryReady()) return RejectEh(13);
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NormalCatcher;
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&NormalThrower;
        ulong originalRip = captured->Rip, originalRsp = captured->Rsp;
        if (!NormalCaptureAdmitted((ulong)captured, originalRip, originalRsp, thrower) ||
            s_ehSelected != 0 || s_ehTransferAddress != 0 || s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0) return RejectEh(14);
        ulong invalidRip = originalRip ^ 1UL;
        if (NormalCaptureAdmitted((ulong)captured, invalidRip, originalRsp, thrower) ||
            captured->Rip != originalRip || captured->Rsp != originalRsp) return RejectEh(15);
        EhLine(4, originalRip, invalidRip, originalRsp, 0);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), 10, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo) return RejectEh(16);
            var own = BootSequence.RunNormalFinallyUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!own.Succeeded || own.InEpilog || own.Caller.Rip != method + NormalFinallyReturn ||
                own.Caller.Rsp != captured->Rsp + 48) return RejectEh(17);
            ulong catalog = NormalCatalogStart(), catalogEnd = NormalCatalogEnd();
            if (catalog < s_gcImage.Start || catalog - s_gcImage.Start > 0x7FFFFFFF ||
                catalogEnd < catalog || catalogEnd - catalog != 32) return RejectEh(18);
            var recovered = NativeAotEhClauseMetadata.RecoverNormalFinallyParent(
                NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (uint)(catalog - s_gcImage.Start), 32, (byte*)(s_gcStack.Start + 16),
                s_gcStack.End - s_gcStack.Start - 16, own.Caller);
            if (!recovered.Succeeded || recovered.ExitedClauseIndex != 0 || recovered.ProtectedOffset != 60 ||
                recovered.Parent.Rip != method + NormalParentReturn || recovered.Parent.Rsp != captured->Rsp + 96 ||
                recovered.Parent.Rbp != recovered.Parent.Rsp + 64 || !StackSpan(recovered.Parent.Rsp, 88) ||
                recovered.Bounds.RootMethodRva != method - s_gcImage.Start ||
                recovered.Bounds.CodeBytes != NormalCodeBytes || recovered.Bounds.EhBytes != NormalTableBytes ||
                recovered.Bounds.EhRva != NormalClauses() - s_gcImage.Start ||
                s_normalLocal != recovered.Parent.Rbp - 8 || GcReadWord(s_normalLocal) != s_ehSeed + 2 ||
                GcReadWord(recovered.Parent.Rbp + 16) != exception ||
                GcReadWord(recovered.Parent.Rbp - 16) != 0 || GcReadWord(recovered.Parent.Rbp - 24) != 0 ||
                !NormalFrame((byte*)storage, method, 0, NormalFinallyOffset, 64, 72, 5) ||
                !NormalFrame((byte*)storage, method, NormalFinallyOffset, NormalCatchOffset, 40, 40, 4) ||
                !NormalFrame((byte*)storage, method, NormalCatchOffset, NormalCodeBytes, 40, 40, 4)) return RejectEh(19);
            NativeAotEhClauseResult* rows = stackalloc NativeAotEhClauseResult[2];
            ExceptionTypeMatchResult* matches = stackalloc ExceptionTypeMatchResult[2];
            for (uint i = 0; i < 2; i++)
            {
                rows[i] = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                    s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                    (byte*)storage, EhStorageBytes, recovered.Bounds, i);
                matches[i] = default;
                if (rows[i].Succeeded && rows[i].Kind == NativeAotEhClauseKind.Typed)
                    matches[i] = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                        s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                        s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, rows[i].TypeAddress);
            }
            var plan = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(new(rows, 2), new(matches, 2),
                recovered.ProtectedOffset, recovered.ExitedClauseIndex);
            if (!plan.Succeeded || plan.CatchIndex != 1 || plan.CleanupCount != 0 ||
                plan.FirstCleanup != 0 || plan.SecondCleanup != 0 || plan.CatchHandler != method + NormalCatchOffset ||
                !NormalRows(rows, method) || !matches[1].Succeeded || !matches[1].Matches ||
                !NormalRootRetained(exception)) return RejectEh(20);
            var saved = rows[1]; rows[1] = default;
            var rejected = BoundedExceptionDispatchPlan.SelectAfterExitedFinally(new(rows, 2), new(matches, 2), 60, 0);
            rows[1] = saved;
            if (rejected.Succeeded || rejected.CatchHandler != 0 || rejected.CleanupCount != 0) return RejectEh(21);
            s_ehCaller = recovered.Parent; s_ehSelected = plan.CatchHandler; s_ehTransferAddress = (ulong)transfer;
            s_normalEntryControl = new EhControl { Flags = control->Flags, Cr4 = control->Cr4, Mxcsr = control->Mxcsr, X87 = control->X87 };
            *transfer = new EhTransfer { Funclet = plan.CatchHandler, Reserved = 0, Caller = recovered.Parent };
            s_normalSelections = 1;
            EhLine(5, captured->Rip, captured->Rsp, own.Caller.Rip, own.Caller.Rsp);
            EhLine(6, own.Caller.Rip, own.Caller.Rsp, recovered.Parent.Rip, recovered.Parent.Rsp);
            EhLine(7, method, NormalCodeBytes, NormalClauses(), NormalTableBytes);
            EhLine(8, rows[1].TypeAddress, plan.CatchHandler, recovered.Parent.Rbp, recovered.ExitedClauseIndex);
            s_ehPhase = 5; return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_NormalFinally_ValidateContinuation")]
    private static uint NormalValidateContinuation(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* controls)
    {
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&NormalCatcher;
        if (s_ehPhase != 6 || s_normalFinallyEntries != 1 || s_normalSelections != 1 || s_normalCatchEntries != 1 ||
            s_normalContinuations != 0 || !NormalBridgeIntact(transfer, controls, post) ||
            !NormalTransferIntact(transfer) || !NormalContextIntact(post, method + NormalContinuation) ||
            !NormalControlsIntact(controls, controls + 1) || rip != method + NormalContinuation ||
            !NormalLocalsIntact(true) || !NormalRootRetained(s_ehReceipt.ObjectReference) ||
            !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) || !CheckEhStack()) return RejectEh(22);
        EhLine(10, rip, post->Rsp, post->Rbp, s_normalCatchEntries); s_ehPhase = 7; return 1;
    }

    private static bool NormalCaptureAdmitted(ulong address, ulong rip, ulong rsp, ulong thrower) =>
        address != 0 && address <= ulong.MaxValue - 816 && rip == thrower + NormalThrowReturn &&
        rsp == address + 816 && rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;
    private static bool NormalFrame(byte* storage, ulong method, uint begin, uint end, uint allocation, uint stack, uint prolog)
    {
        var row = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(storage, EhStorageBytes, method + begin);
        return row.Succeeded && row.ChainDepth == 0 && row.Flags == 0 && row.FrameRegister == 0 && row.FrameOffset == 0 &&
            row.FixedAllocationBytes == allocation && row.StackBytes == stack && row.PrologBytes == prolog &&
            row.BeginRva == method - s_gcImage.Start + begin && row.EndRva == method - s_gcImage.Start + end;
    }
    private static bool NormalRows(NativeAotEhClauseResult* rows, ulong method) =>
        rows[0].Succeeded && rows[0].Index == 0 && rows[0].Count == 2 && !rows[0].IsTryGroupBoundary &&
        rows[0].TryStartOffset == 43 && rows[0].TryEndOffset == 56 && rows[0].Kind == NativeAotEhClauseKind.FaultOrFinally &&
        rows[0].HandlerAddress == method + NormalFinallyOffset && rows[0].TypeAddress == 0 && rows[0].FilterAddress == 0 &&
        rows[1].Succeeded && rows[1].Index == 1 && rows[1].Count == 2 && !rows[1].IsTryGroupBoundary &&
        rows[1].TryStartOffset == 43 && rows[1].TryEndOffset == 62 && rows[1].Kind == NativeAotEhClauseKind.Typed &&
        rows[1].HandlerAddress == method + NormalCatchOffset && rows[1].TypeAddress == EhBaseType() && rows[1].FilterAddress == 0;
    private static bool NormalBridgeIntact(EhTransfer* transfer, EhControl* controls, X64UnwindContext* post) =>
        s_ehTransferAddress != 0 && (ulong)transfer == s_ehTransferAddress && ((ulong)transfer & 15) == 0 &&
        (ulong)transfer >= 272 && StackSpan((ulong)transfer - 272, 848) &&
        (ulong)controls == (ulong)transfer + 504 && (ulong)post == (ulong)transfer + 256;
    private static bool NormalTransferIntact(EhTransfer* transfer) =>
        transfer->Funclet == s_ehSelected && transfer->Reserved == 0 && NormalContextIntact(&transfer->Caller, s_ehCaller.Rip);
    private static bool NormalContextIntact(X64UnwindContext* actual, ulong rip)
    {
        if (actual->Rip != rip || actual->Rsp != s_ehCaller.Rsp || actual->Rsp <= s_ehTransferAddress + 576 ||
            actual->Rsp > s_gcStack.InitialRsp) return false;
        for (byte reg = 3; reg <= 15; reg++)
            if ((reg == 3 || reg == 5 || reg == 6 || reg == 7 || reg >= 12) &&
                actual->GetInteger(reg) != s_ehCaller.GetInteger(reg)) return false;
        for (byte reg = 6; reg <= 15; reg++)
        {
            var a = actual->GetVector(reg); var b = s_ehCaller.GetVector(reg);
            if (a.Low != b.Low || a.High != b.High) return false;
        }
        return true;
    }
    private static bool NormalControlsIntact(EhControl* entry, EhControl* post) =>
        ControlAdmitted(entry) && ControlAdmitted(post) && entry->Flags == s_normalEntryControl.Flags &&
        entry->Cr4 == s_normalEntryControl.Cr4 && entry->Cr4 == post->Cr4 &&
        entry->X87 == s_normalEntryControl.X87 && entry->X87 == post->X87 &&
        entry->Mxcsr == s_normalEntryControl.Mxcsr && (entry->Mxcsr & 0xFFC0) == (post->Mxcsr & 0xFFC0);
    private static bool NormalLocalsIntact(bool afterCatch) =>
        s_normalLocal == s_ehCaller.Rbp - 8 && StackSpan(s_ehCaller.Rsp, 88) &&
        GcReadWord(s_normalLocal) == s_normalWitness && s_normalWitness == s_ehSeed + 2 &&
        GcReadWord(s_ehCaller.Rbp + 16) == s_ehReceipt.ObjectReference &&
        GcReadWord(s_ehCaller.Rbp - 16) == (afterCatch ? s_ehSeed + 1 : 0UL) &&
        GcReadWord(s_ehCaller.Rbp - 24) == s_ehReceipt.ObjectReference;
    private static bool NormalRootRetained(ulong reference) => reference != 0 &&
        s_normalRoot.AllocationSucceeded && s_ehReceipt.AllocationSucceeded &&
        s_normalRoot.ObjectReference == reference && s_normalRoot.ObjectReference == s_ehReceipt.ObjectReference &&
        s_normalRoot.MethodTableToken == s_ehReceipt.MethodTableToken &&
        s_normalRoot.AllocationCount == s_ehReceipt.AllocationCount && EhObjectIntact();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchNormalExceptionType(ulong targetType, object value)
    {
        ulong reference = Bits(value); if (reference == 0) return false;
        if ((s_ehPhase != 2 && s_ehPhase != 4) || !NormalRootRetained(reference)) { RejectEh(100); return false; }
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, reference, targetType);
            if (!match.Succeeded || !NormalRootRetained(reference)) { RejectEh(101 + (ulong)match.Status); return false; }
            return match.Matches;
        }
    }
}
#endif
