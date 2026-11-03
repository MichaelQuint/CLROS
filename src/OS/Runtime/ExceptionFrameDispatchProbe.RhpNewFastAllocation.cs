#if NOSTDLIB_EFI && M04_FRAME_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{
    // Exact compiler code/EH/unwind measured in frame-native-geometry-r01.
    // Transfer remains closed until complete callback stack review.
    // The separate bridge, compiled frame geometry and complete constant-body
    // callback stack paths were reviewed before admitting this bounded profile.
    private const bool FrameTransferAdmitted = true;
#if M04_EH_CATALOG_PROBE
    // Caller-window metadata, scratch lifetime and complete callback stack
    // review is recorded in caller-window-transfer-admission-r01 before enabling.
    private const bool CatalogTransferAdmitted = true;
    [System.Runtime.RuntimeImport("*", "CLROS_Eh_CatalogStart")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong FrameCatalogStart();
    [System.Runtime.RuntimeImport("*", "CLROS_Eh_CatalogEnd")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong FrameCatalogEnd();
#endif
    private const uint FrameMiddleBytes = 72, FrameMiddleReturn = 25, FrameMiddleCleanup = 49, FrameMiddleTableBytes = 4;
    private const uint FrameCatchBytes = 138, FrameCatchReturn = 46, FrameWrongCatch = 75, FrameCatchHandler = 96;
    private const uint FrameContinuation = 47, FrameCatchTableBytes = 15;
    private const uint FrameMiddleAllocation = 48, FrameMiddleStack = 56, FrameMiddleProlog = 5, FrameMiddleBp = 48;
    private const uint FrameCatchAllocation = 56, FrameCatchStack = 72, FrameCatchProlog = 6, FrameCatchBp = 64;
    private const uint FrameMiddleStampOffset = 8, FrameOuterStampOffset = 24, FrameResultOffset = 16;
    private static RhpNewFastResult s_frameRoot;

#if M04_EH_CATALOG_PROBE

#else

#endif
    private static FrameScratch s_frameScratch;
    private static X64UnwindContext s_frameMiddle;
    private static EhControl s_frameEntryControl;
    private static ulong s_frameCleanup, s_frameCleanupEntries, s_frameMiddleLocal, s_frameOuterLocal;

    [System.Runtime.RuntimeImport("*", "CLROS_Frame_MiddleClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong FrameMiddleClauses();
    [System.Runtime.RuntimeImport("*", "CLROS_Frame_CatcherClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong FrameCatcherClauses();
    [System.Runtime.RuntimeImport("*", "RhTypeCast_IsInstanceOfException")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern bool FrameRuntimeMatch(Internal.Runtime.MethodTable* target, object value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool FrameGeometryReady() =>
#if M04_EH_CATALOG_PROBE
        CatalogTransferAdmitted;
#else
        FrameTransferAdmitted;
#endif
    [MethodImpl(MethodImplOptions.NoInlining)] private static uint FrameMiddleExtent() => FrameMiddleBytes;
    [MethodImpl(MethodImplOptions.NoInlining)] private static uint FrameCatchExtent() => FrameCatchBytes;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong FrameSiblingType() => (ulong)Internal.Runtime.MethodTable.Of<SimpleCatchSibling>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunFrameDispatch()
    {
        if (!BootSequence.IsFrameDispatchEnabled() || s_ehPhase != 0 || s_frameRoot.ObjectReference != 0 ||
            s_frameCleanupEntries != 0 || s_frameOuterLocal != 0 || s_frameMiddleLocal != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5))
            RejectFrame(1);
        EhText(0);
        s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        if (!CheckEhStack())
            RejectFrame(2);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes || sizeof(X64UnwindContext) != 240 ||
                sizeof(BoundedExceptionFrameTransfer) != 496 || sizeof(EhControl) != 32 ||
#if M04_EH_CATALOG_PROBE
                sizeof(FrameScratch) != 16 * sizeof(BoundedExceptionCleanupEntry))
                RejectFrame(3);
#else
                sizeof(FrameScratch) != 8 * sizeof(BoundedExceptionCleanupEntry))
                RejectFrame(3);
#endif
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact())
            RejectFrame(4);
        s_frameRoot = s_ehReceipt;
        fixed (RhpNewFastResult* root = &s_frameRoot)
        {
            var collected = CollectGcGraph(root, 1);
            if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
                !FrameRootRetained(Bits(exception)) || !EhObjectIntact() ||
                !ReadGcRoot().Succeeded || !CollectorReceiptValid(5))
                RejectFrame(5);
            EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        }
        bool same = FrameRuntimeMatch((Internal.Runtime.MethodTable*)EhDerivedType(), exception);
        bool ancestor = FrameRuntimeMatch((Internal.Runtime.MethodTable*)EhBaseType(), exception);
        bool sibling = FrameRuntimeMatch((Internal.Runtime.MethodTable*)FrameSiblingType(), exception);
        bool nil = FrameRuntimeMatch(null, null);
        if (!same || !ancestor || sibling || nil || !FrameRootRetained(Bits(exception)) || !EhObjectIntact())
            RejectFrame(6);
        EhLine(2, same ? 1UL : 0UL, ancestor ? 1UL : 0UL, sibling ? 1UL : 0UL, nil ? 1UL : 0UL);
        s_ehPhase = 3;
        ulong result = FrameCatcher(exception);
        if (s_ehPhase != 8 || s_frameCleanupEntries != 1 || s_ehCatchEntries != 1 || s_ehContinuations != 1 ||
            result != s_ehSeed + 1 || !FrameRootRetained(Bits(exception)) || !EhObjectIntact() || !CheckEhStack())
            RejectFrame(7);
        // Parent locals were checked inside the real continuation. Their frame
        // has now returned: only retained phase/receipt evidence is used here.
        EhLine(13, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        EhLine(14, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_gcHandle, s_ehReceipt.AllocationCount);
        s_ehPhase = 9;
        s_frameRoot = default;
        s_ehReceipt = default;
        EhText(1);
        EhText(2);
#if M04_EH_CATALOG_PROBE
        FrameCatalogText(true);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameThrower(System.Exception value)
    {
        throw value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong FrameMiddle(SimpleCatchBase value)
    {
        ulong stamp = 0;
        try
        {
            FrameThrower(value);
        }
        finally { FrameCleanupEntered(value, ref stamp); }
        return stamp;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong FrameCatcher(SimpleCatchBase value)
    {
        ulong result = 0, stamp = 0;
        FrameMarkOuter(value, ref stamp);
        try
        {
            FrameMiddle(value);
        }
        catch (SimpleCatchSibling wrong) { FrameUnexpectedCatch(wrong); }
        catch (SimpleCatchBase caught)
        {
            FrameCatchEntered(caught, ref stamp);
            result = caught.Payload + 1;
        }
        FrameContinued(value, result, stamp);
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameUnexpectedCatch(SimpleCatchSibling value)
    {
        RejectFrame(40);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameMarkOuter(SimpleCatchBase value, ref ulong stamp)
    {
        fixed (ulong* local = &stamp)
        {
            if (s_ehPhase != 3 || s_frameOuterLocal != 0 || stamp != 0 ||
                !StackSpan((ulong)local, 8) || !FrameRootRetained(Bits(value)) || !EhObjectIntact() || !CheckEhStack())
                RejectFrame(8);
            s_frameOuterLocal = (ulong)local;
            stamp = s_ehSeed + 2;
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameCleanupEntered(SimpleCatchBase value, ref ulong stamp)
    {
        fixed (ulong* local = &stamp)
        {
            if (s_ehPhase != 4 || s_frameCleanupEntries != 0 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
                (ulong)local != s_frameMiddleLocal || !FrameMiddleStampIntact(0) || stamp != 0 ||
                !FrameOuterStampIntact(2) || !FrameRootRetained(Bits(value)) || value.Payload != s_ehSeed ||
                !EhObjectIntact() || !CheckEhStack())
                RejectFrame(9);
            stamp = s_ehSeed + 4;
            s_frameCleanupEntries = 1;
            s_ehPhase = 5;
            EhLine(8, Bits(value), (ulong)local, stamp, s_frameCleanupEntries);
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameCatchEntered(SimpleCatchBase value, ref ulong stamp)
    {
        fixed (ulong* local = &stamp)
        {
            if (s_ehPhase != 6 || s_frameCleanupEntries != 1 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
                (ulong)local != s_frameOuterLocal || !FrameOuterStampIntact(2) || stamp != s_ehSeed + 2 ||
                !FrameRootRetained(Bits(value)) || value.Payload != s_ehSeed || !EhObjectIntact() || !CheckEhStack())
                RejectFrame(10);
            stamp = s_ehSeed + 3;
            s_ehCatchEntries = 1;
            s_ehPhase = 7;
            EhLine(10, Bits(value), value.Payload, stamp, s_ehCatchEntries);
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FrameContinued(SimpleCatchBase value, ulong result, ulong stamp)
    {
        if (s_ehPhase != 8 || s_frameCleanupEntries != 1 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            result != s_ehSeed + 1 || stamp != s_ehSeed + 3 || !FrameOuterStampIntact(3) ||
            !FrameRootRetained(Bits(value)) || !EhObjectIntact() || !CheckEhStack())
            RejectFrame(11);
        s_ehContinuations = 1;
        EhLine(12, Bits(value), result, stamp, s_ehContinuations);
    }

    [System.Runtime.RuntimeExport("CLROS_Frame_Select")]
    private static uint FrameSelect(ulong exception, X64UnwindContext* captured, BoundedExceptionFrameTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || !CheckEhStack() || !EhObjectIntact() || !FrameRootRetained(exception) ||
            !FrameBridgeLayout(captured, transfer, control) || !ControlAdmitted(control))
            return RejectFrame(12);
        *transfer = default;
        if (!FrameGeometryReady())
            return RejectFrame(13);
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&FrameThrower;
        ulong middle = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&FrameMiddle;
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&FrameCatcher;
        if (!FrameCaptureAdmitted((ulong)captured, captured->Rip, captured->Rsp, thrower) ||
            FrameCaptureAdmitted((ulong)captured, captured->Rip ^ 1UL, captured->Rsp, thrower) ||
            s_frameCleanup != 0 || s_frameMiddleLocal != 0 || s_ehSelected != 0 || s_ehTransferAddress != 0 ||
            s_frameCleanupEntries != 0 || s_ehCatchEntries != 0 || s_ehContinuations != 0)
            return RejectFrame(14);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        fixed (EhStorage* storage = &s_ehStorage)
        fixed (FrameScratch* scratchOwner = &s_frameScratch)
        {
            ulong scratchAddress = (ulong)scratchOwner;
            if ((scratchAddress & 7) != 0 || scratchAddress < s_gcImage.Start ||
                scratchAddress - s_gcImage.Start > s_gcImage.Bytes ||
                (ulong)sizeof(FrameScratch) > s_gcImage.Bytes - (scratchAddress - s_gcImage.Start) ||
                TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(s_gcCapture, s_gcMapping,
                    (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, scratchAddress,
                    (ulong)sizeof(FrameScratch), TypeManagerRegisteredRangeKind.MutableData) != TypeManagerModuleStatus.Success)
                return RejectFrame(25);
#if M04_EH_CATALOG_PROBE
            ulong rowsAddress = scratchAddress + (ulong)sizeof(BoundedExceptionCleanupEntry) * 8;
            ulong matchesAddress = (rowsAddress + (ulong)sizeof(NativeAotEhClauseResult) * 3 + 7) & ~7UL;
            ulong framesAddress = (matchesAddress + (ulong)sizeof(ExceptionTypeMatchResult) * 3 + 7) & ~7UL;
            if (framesAddress + (ulong)sizeof(BoundedExceptionSearchFrame) * 3 > scratchAddress + (ulong)sizeof(FrameScratch))
                return RejectFrame(28);
            NativeAotEhClauseResult* rows = (NativeAotEhClauseResult*)rowsAddress;
            ExceptionTypeMatchResult* matches = (ExceptionTypeMatchResult*)matchesAddress;
            BoundedExceptionSearchFrame* frames = (BoundedExceptionSearchFrame*)framesAddress;
            ulong catalogStart = FrameCatalogStart(), catalogEnd = FrameCatalogEnd();
            if (!BootSequence.IsEhCatalogEnabled() || catalogStart < s_gcImage.Start || catalogEnd <= catalogStart ||
                catalogStart - s_gcImage.Start > 0x7FFFFFFFUL || catalogEnd - catalogStart > 0xFFFFFFFFUL)
                return RejectFrame(26);
            var window = BoundedExceptionCallerWindow.Build(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, (uint)(catalogStart - s_gcImage.Start), (uint)(catalogEnd - catalogStart),
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured, 3,
                new(frames, 3), new(rows, 3));
            if (!window.Succeeded || window.FrameCount != 3 || window.RowCount != 3 ||
                frames[0].Context.Rip != captured->Rip || frames[0].Context.Rsp != captured->Rsp ||
                frames[0].ProtectedOffset != 8 || frames[0].RowOffset != 0 || frames[0].RowCount != 0 ||
                frames[1].ProtectedOffset != FrameMiddleReturn - 1 || frames[1].RowOffset != 0 || frames[1].RowCount != 1 ||
                frames[2].ProtectedOffset != FrameCatchReturn - 1 || frames[2].RowOffset != 1 || frames[2].RowCount != 2)
                return RejectFrame(29);
            var firstCaller = frames[1].Context;
            var secondCaller = frames[2].Context;
#else
            var first = BootSequence.RunFrameDispatchUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!first.Succeeded || first.InEpilog)
                return RejectFrame(15);
            var firstCaller = first.Caller;
            var second = BootSequence.RunFrameDispatchUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, firstCaller);
            if (!second.Succeeded || second.InEpilog)
                return RejectFrame(16);
            var secondCaller = second.Caller;
#endif
            if (firstCaller.Rip != middle + FrameMiddleReturn ||
                firstCaller.Rsp != captured->Rsp + 48 || firstCaller.Rbp != firstCaller.Rsp + FrameMiddleBp ||
                !StackSpan(firstCaller.Rsp, FrameMiddleStack + 16) ||
                !FrameFunction((byte*)storage, middle, 0, FrameMiddleCleanup, FrameMiddleAllocation, FrameMiddleStack, FrameMiddleProlog))
                return RejectFrame(15);
            if (secondCaller.Rip != catcher + FrameCatchReturn ||
                secondCaller.Rsp != firstCaller.Rsp + FrameMiddleStack + 8 ||
                secondCaller.Rbp != secondCaller.Rsp + FrameCatchBp || secondCaller.Rbp == firstCaller.Rbp ||
                !StackSpan(secondCaller.Rsp, FrameCatchStack + 16) ||
                !FrameFunction((byte*)storage, catcher, 0, FrameWrongCatch, FrameCatchAllocation, FrameCatchStack, FrameCatchProlog) ||
                GcReadWord(firstCaller.Rbp + 16) != exception || GcReadWord(secondCaller.Rbp + 16) != exception ||
                GcReadWord(firstCaller.Rbp - FrameMiddleStampOffset) != 0 ||
                s_frameOuterLocal != secondCaller.Rbp - FrameOuterStampOffset ||
                GcReadWord(s_frameOuterLocal) != s_ehSeed + 2 ||
                GcReadWord(secondCaller.Rbp - FrameResultOffset) != 0)
                return RejectFrame(16);
            EhLine(3, captured->Rip, captured->Rsp, firstCaller.Rip, firstCaller.Rsp);
            EhLine(4, firstCaller.Rip, firstCaller.Rsp, secondCaller.Rip, secondCaller.Rsp);
#if !M04_EH_CATALOG_PROBE
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), 10, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo)
                return RejectFrame(17);
            NativeAotEhClauseResult* rows = stackalloc NativeAotEhClauseResult[3];
            ExceptionTypeMatchResult* matches = stackalloc ExceptionTypeMatchResult[3];
#endif
            matches[0] = default;
#if M04_EH_CATALOG_PROBE
            var middleContribution = FrameCatalogLookup((byte*)type, (byte*)storage,
                (uint)(catalogStart - s_gcImage.Start), (uint)(catalogEnd - catalogStart), firstCaller.Rip - 1);
            var catchContribution = FrameCatalogLookup((byte*)type, (byte*)storage,
                (uint)(catalogStart - s_gcImage.Start), (uint)(catalogEnd - catalogStart), secondCaller.Rip - 1);
            if (!middleContribution.Succeeded || !catchContribution.Succeeded ||
                s_gcImage.Start + middleContribution.Bounds.RootMethodRva != middle || middleContribution.ClauseCount != 1 ||
                s_gcImage.Start + catchContribution.Bounds.RootMethodRva != catcher || catchContribution.ClauseCount != 2)
                return RejectFrame(27);
            uint middleBytes = middleContribution.Bounds.CodeBytes, catchBytes = catchContribution.Bounds.CodeBytes;
            FrameCatalogLine(0, catalogStart, catalogEnd, (uint)GcReadWord(catalogStart + 8), s_gcHandle);
            FrameCatalogLine(1, middle, middleBytes, s_gcImage.Start + middleContribution.Bounds.EhRva, middleContribution.Bounds.EhBytes);
            FrameCatalogLine(2, catcher, catchBytes, s_gcImage.Start + catchContribution.Bounds.EhRva, catchContribution.Bounds.EhBytes);
            if (frames[1].CodeBytes != middleBytes || frames[2].CodeBytes != catchBytes)
                return RejectFrame(30);
            for (uint i = 0; i < 3; i++)
                FrameCallerWindowLine(i, frames[i].Context.Rip, frames[i].Context.Rsp, frames[i].ProtectedOffset,
                    ((ulong)frames[i].RowOffset << 32) | frames[i].RowCount);
#else
            uint middleBytes = FrameMiddleBytes, catchBytes = FrameCatchBytes;
            rows[0] = FrameReadRow((byte*)type, (byte*)storage, middle, FrameMiddleExtent(), FrameMiddleClauses(), FrameMiddleTableBytes, 0);
            rows[1] = FrameReadRow((byte*)type, (byte*)storage, catcher, FrameCatchExtent(), FrameCatcherClauses(), FrameCatchTableBytes, 0);
            rows[2] = FrameReadRow((byte*)type, (byte*)storage, catcher, FrameCatchExtent(), FrameCatcherClauses(), FrameCatchTableBytes, 1);
#endif
            if (!rows[0].Succeeded || rows[0].Count != 1 || rows[0].Kind != NativeAotEhClauseKind.FaultOrFinally ||
                rows[0].HandlerAddress != middle + FrameMiddleCleanup || !rows[0].ContainsOffset(FrameMiddleReturn - 1) ||
                !rows[1].Succeeded || !rows[2].Succeeded || rows[1].Count != 2 || rows[2].Count != 2 ||
                rows[1].TypeAddress != FrameSiblingType() || rows[2].TypeAddress != EhBaseType() ||
                rows[1].HandlerAddress != catcher + FrameWrongCatch || rows[2].HandlerAddress != catcher + FrameCatchHandler ||
                !rows[1].ContainsOffset(FrameCatchReturn - 1) || !rows[2].ContainsOffset(FrameCatchReturn - 1))
                return RejectFrame(18);
            for (uint i = 1; i < 3; i++)
            {
                matches[i] = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
                    s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                    s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, rows[i].TypeAddress);
                if (!matches[i].Succeeded || matches[i].Matches != (i == 2) ||
                    FrameRuntimeMatch((Internal.Runtime.MethodTable*)rows[i].TypeAddress, FrameObject(exception)) != (i == 2))
                    return RejectFrame(19);
            }
#if !M04_EH_CATALOG_PROBE
            BoundedExceptionSearchFrame* frames = stackalloc BoundedExceptionSearchFrame[3];
            frames[0] = new(*captured, 8, 10, 0, 0);
            frames[1] = new(firstCaller, FrameMiddleReturn - 1, middleBytes, 0, 1);
            frames[2] = new(secondCaller, FrameCatchReturn - 1, catchBytes, 1, 2);
#endif
            BoundedExceptionCleanupEntry* scratch = (BoundedExceptionCleanupEntry*)scratchOwner;
            var savedFrame = frames[2];
            frames[2] = new(savedFrame.Context, savedFrame.CodeBytes, savedFrame.CodeBytes, 1, 2);
            var badPc = BoundedExceptionFrameDispatch.Select(new(frames, 3), new(rows, 3), new(matches, 3), new(scratch, 8), out var denied);
            frames[2] = savedFrame;
            if (badPc.Status != BoundedExceptionFrameDispatchStatus.SearchRejected ||
                badPc.SearchStatus != BoundedExceptionFrameSearchStatus.InvalidFrame || !FrameOutputsEmpty(denied, scratch))
                return RejectFrame(20);
            var savedRow = rows[2];
            rows[2] = default;
            var badRow = BoundedExceptionFrameDispatch.Select(new(frames, 3), new(rows, 3), new(matches, 3), new(scratch, 8), out denied);
            rows[2] = savedRow;
            if (badRow.Status != BoundedExceptionFrameDispatchStatus.SearchRejected ||
                badRow.SearchStatus != BoundedExceptionFrameSearchStatus.DispatchRejected ||
                badRow.DispatchStatus != BoundedExceptionDispatchStatus.InvalidClause || !FrameOutputsEmpty(denied, scratch))
                return RejectFrame(21);
            var plan = BoundedExceptionFrameDispatch.Select(new(frames, 3), new(rows, 3), new(matches, 3), new(scratch, 8), out var selected);
            if (!plan.Succeeded || plan.CleanupFrameIndex != 1 || plan.CatchFrameIndex != 2 ||
                selected.CleanupHandler != middle + FrameMiddleCleanup || selected.CatchHandler != catcher + FrameCatchHandler ||
                selected.CleanupCaller.Rip != firstCaller.Rip || selected.CatchCaller.Rip != secondCaller.Rip ||
                !FrameFunction((byte*)storage, middle, FrameMiddleCleanup, FrameMiddleBytes, 40, 40, 4) ||
                !FrameFunction((byte*)storage, catcher, FrameWrongCatch, FrameCatchHandler, 40, 40, 4) ||
                !FrameFunction((byte*)storage, catcher, FrameCatchHandler, FrameCatchBytes, 40, 40, 4) ||
                !FrameRootRetained(exception) || !EhObjectIntact() || !CheckEhStack())
                return RejectFrame(22);
            EhLine(5, (ulong)badPc.SearchStatus, (ulong)badRow.SearchStatus, (ulong)badRow.DispatchStatus, 0);
            EhLine(6, (ulong)matches[1].Status, matches[1].Matches ? 1UL : 0UL, (ulong)matches[2].Status, matches[2].Matches ? 1UL : 0UL);
            s_frameMiddle = firstCaller;
            s_ehCaller = secondCaller;
            s_frameCleanup = selected.CleanupHandler;
            s_ehSelected = selected.CatchHandler;
            s_frameMiddleLocal = firstCaller.Rbp - FrameMiddleStampOffset;
            s_ehTransferAddress = (ulong)transfer;
            s_frameEntryControl = new EhControl { Flags = control->Flags, Cr4 = control->Cr4, Mxcsr = control->Mxcsr, X87 = control->X87 };
            *transfer = selected;
            EhLine(7, selected.CleanupHandler, selected.CatchHandler, s_frameMiddleLocal, s_frameOuterLocal);
            s_ehPhase = 4;
            return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Frame_ValidateCleanup")]
    private static uint FrameValidateCleanup(BoundedExceptionFrameTransfer* transfer, EhControl* entry, EhControl* post)
    {
        if (s_ehPhase != 5 || s_frameCleanupEntries != 1 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
            !FrameBridgeIntact(transfer, entry, post) || !FrameTransferIntact(transfer) || !FrameControlsIntact(entry, post) ||
            !FrameMiddleStampIntact(4) || !FrameOuterStampIntact(2) || !FrameRootRetained(s_ehReceipt.ObjectReference) ||
            !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5) || !CheckEhStack())
            return RejectFrame(23);
        EhLine(9, s_frameCleanup, s_frameMiddle.Rsp, s_ehCaller.Rsp, s_frameRoot.ObjectReference);
        s_ehPhase = 6;
        return 1;
    }
    [System.Runtime.RuntimeExport("CLROS_Frame_ValidateContinuation")]
    private static uint FrameValidateContinuation(ulong rip, X64UnwindContext* post, BoundedExceptionFrameTransfer* transfer, EhControl* entry)
    {
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&FrameCatcher;
        if (s_ehPhase != 7 || s_frameCleanupEntries != 1 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            !FrameBridgeIntact(transfer, entry, entry + 1) || (ulong)post != (ulong)transfer + 496 ||
            !FrameTransferIntact(transfer) || !FrameControlsIntact(entry, entry + 1) || !FrameOuterStampIntact(3) ||
            !FrameRootRetained(s_ehReceipt.ObjectReference) || !EhObjectIntact() || !ReadGcRoot().Succeeded ||
            !CollectorReceiptValid(5) || !CheckEhStack() || rip != method + FrameContinuation || post->Rip != rip ||
            post->Rbx != s_ehReceipt.ObjectReference || !FrameContextIntact(post, true, false) ||
            post->Rsp <= (ulong)transfer + 808 || post->Rsp > s_gcStack.InitialRsp ||
            GcReadWord(s_ehCaller.Rbp - FrameResultOffset) != s_ehSeed + 1)
            return RejectFrame(24);
        EhLine(11, rip, post->Rsp, post->Rbp, s_ehCatchEntries);
        s_ehPhase = 8;
        return 1;
    }

    private static NativeAotEhClauseResult FrameReadRow(byte* type, byte* storage, ulong method, uint codeBytes, ulong table, uint tableBytes, uint index)
    {
        if (method < s_gcImage.Start || method - s_gcImage.Start > 0x7FFFFFFF ||
            table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF || codeBytes == 0 || tableBytes == 0)
            return default;
        return NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
            s_gcCapture, s_gcMapping, type, (ulong)sizeof(GcTypeStorage), s_gcHandle, storage, EhStorageBytes,
            new((uint)(method - s_gcImage.Start), codeBytes, (uint)(table - s_gcImage.Start), tableBytes), index);
    }
#if M04_EH_CATALOG_PROBE
    // Keep large by-value authority copies in one returning helper frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static NativeAotEhMethodLookupResult FrameCatalogLookup(byte* type, byte* storage, uint catalogRva, uint catalogBytes, ulong instruction) =>
        NativeAotEhClauseMetadata.LookupMethod(NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian,
            s_gcCapture, s_gcMapping, type, (ulong)sizeof(GcTypeStorage), s_gcHandle, storage, EhStorageBytes, catalogRva, catalogBytes, instruction);
    private static void FrameCallerWindowLine(ulong stage, ulong rip, ulong rsp, ulong offset, ulong slice)
    {
        string text = "M04_CALLER_WINDOW ";
        for (int i = 0; i < text.Length; i++)
            Write((byte)text[i]);
        Decimal(stage);
        Write(32);
        Hex(rip);
        Write(32);
        Hex(rsp);
        Write(32);
        Hex(offset);
        Write(32);
        Hex(slice);
        End();
    }
    private static void FrameCatalogText(bool ready)
    {
        string text = ready ? "CLROS_M04_EH_CATALOG_READY\r\n" : "M04_EH_CATALOG ";
        for (int i = 0; i < text.Length; i++)
            Write((byte)text[i]);
    }
    private static void FrameCatalogLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        FrameCatalogText(false);
        Decimal(stage);
        Write(32);
        Hex(a);
        Write(32);
        Hex(b);
        Write(32);
        Hex(c);
        Write(32);
        Hex(d);
        End();
    }
#endif
    private static bool FrameFunction(byte* storage, ulong method, uint begin, uint end, uint allocation, uint stack, uint prolog)
    {
        var row = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(storage, EhStorageBytes, method + begin);
        return row.Succeeded && row.ChainDepth == 0 && row.FrameRegister == 0 && row.FrameOffset == 0 &&
            row.FixedAllocationBytes == allocation && row.StackBytes == stack && row.PrologBytes == prolog &&
            row.BeginRva == method - s_gcImage.Start + begin && row.EndRva == method - s_gcImage.Start + end;
    }
    private static bool FrameRootRetained(ulong reference) => reference != 0 &&
        s_frameRoot.AllocationSucceeded && s_ehReceipt.AllocationSucceeded &&
        s_frameRoot.ObjectReference == reference && s_ehReceipt.ObjectReference == reference &&
        s_frameRoot.MethodTableToken == s_ehReceipt.MethodTableToken &&
        s_frameRoot.BytesConsumed == s_ehReceipt.BytesConsumed && s_frameRoot.AllocationCount == s_ehReceipt.AllocationCount &&
        s_frameRoot.UsedBefore == s_ehReceipt.UsedBefore && s_frameRoot.UsedAfter == s_ehReceipt.UsedAfter &&
        s_frameRoot.MappedAllocationStart == s_ehReceipt.MappedAllocationStart &&
        s_frameRoot.MappedAllocationEndExclusive == s_ehReceipt.MappedAllocationEndExclusive &&
        s_frameRoot.PhysicalAllocationStart == s_ehReceipt.PhysicalAllocationStart &&
        s_frameRoot.PhysicalAllocationEndExclusive == s_ehReceipt.PhysicalAllocationEndExclusive;
    private static bool FrameCaptureAdmitted(ulong address, ulong rip, ulong rsp, ulong thrower) =>
        rip == thrower + 9 && rsp == address + 1056 && rip != 0 &&
        rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;
    private static bool FrameBridgeLayout(X64UnwindContext* input, BoundedExceptionFrameTransfer* output, EhControl* entry) =>
        (ulong)input >= 32 && ((ulong)input & 15) == 0 && StackSpan((ulong)input - 32, 1088) &&
        (ulong)output == (ulong)input + 240 && (ulong)entry == (ulong)input + 984;
    private static bool FrameBridgeIntact(BoundedExceptionFrameTransfer* transfer, EhControl* entry, EhControl* post) =>
        s_ehTransferAddress != 0 && (ulong)transfer == s_ehTransferAddress && ((ulong)transfer & 15) == 0 &&
        (ulong)transfer >= 272 && StackSpan((ulong)transfer - 272, 1088) &&
        (ulong)entry == (ulong)transfer + 744 && (ulong)post == (ulong)transfer + 776;
    private static bool FrameTransferIntact(BoundedExceptionFrameTransfer* transfer) =>
        transfer->CatchHandler == s_ehSelected && transfer->CleanupHandler == s_frameCleanup &&
        FrameContextIntact((X64UnwindContext*)((byte*)transfer + 16), false, false) &&
        FrameContextIntact((X64UnwindContext*)((byte*)transfer + 256), false, true);
    private static bool FrameContextIntact(X64UnwindContext* actual, bool afterCatch, bool middle)
    {
        fixed (X64UnwindContext* catchSaved = &s_ehCaller)
        fixed (X64UnwindContext* middleSaved = &s_frameMiddle)
        {
            ulong* saved = (ulong*)(middle ? middleSaved : catchSaved);
            for (int word = afterCatch ? 1 : 0; word < 30; word++)
                if (!(afterCatch && word == 2) && ((ulong*)actual)[word] != saved[word])
                    return false;
        }
        return true;
    }
    private static bool FrameControlsIntact(EhControl* entry, EhControl* post) =>
        ControlAdmitted(entry) && ControlAdmitted(post) && entry->Cr4 == s_frameEntryControl.Cr4 &&
        entry->Mxcsr == s_frameEntryControl.Mxcsr && entry->X87 == s_frameEntryControl.X87 &&
        ((entry->Flags ^ s_frameEntryControl.Flags) & 0x600) == 0 &&
        post->Cr4 == entry->Cr4 && post->Mxcsr == entry->Mxcsr && post->X87 == entry->X87 &&
        ((post->Flags ^ entry->Flags) & 0x600) == 0;
    private static bool FrameMiddleStampIntact(ulong delta) =>
        s_frameMiddleLocal == s_frameMiddle.Rbp - FrameMiddleStampOffset && StackSpan(s_frameMiddleLocal, 8) &&
        GcReadWord(s_frameMiddleLocal) == (delta == 0 ? 0UL : s_ehSeed + delta) &&
        GcReadWord(s_frameMiddle.Rbp + 16) == s_ehReceipt.ObjectReference;
    private static bool FrameOuterStampIntact(ulong delta) =>
        s_frameOuterLocal == s_ehCaller.Rbp - FrameOuterStampOffset && StackSpan(s_frameOuterLocal, 8) &&
        GcReadWord(s_frameOuterLocal) == s_ehSeed + delta && GcReadWord(s_ehCaller.Rbp + 16) == s_ehReceipt.ObjectReference;
    private static bool FrameOutputsEmpty(BoundedExceptionFrameTransfer transfer, BoundedExceptionCleanupEntry* scratch)
    {
        for (int i = 0; i < 62; i++)
            if (((ulong*)&transfer)[i] != 0)
                return false;
        for (int entry = 0; entry < 8; entry++)
            for (int word = 0; word < sizeof(BoundedExceptionCleanupEntry) / 8; word++)
                if (((ulong*)(scratch + entry))[word] != 0)
                    return false;
        return true;
    }
    private static object FrameObject(ulong reference)
    {
        nuint native = (nuint)reference;
        return Unsafe.As<nuint, object>(ref native);
    }
    private static uint RejectFrame(ulong reason)
    {
        EhText(3);
        Decimal(reason);
        End();
        while (true)
            System.RuntimeFailFast.Halt();
    }
}
#endif
