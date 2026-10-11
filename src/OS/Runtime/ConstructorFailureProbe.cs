#if NOSTDLIB_EFI && M04_CONSTRUCTOR_FAILURE_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static class ConstructorFailureFixture
{
    private static ulong s_value;
    static ConstructorFailureFixture()
    {
        RhpNewFastAllocation.ConstructorFailureBodyEntered();
        s_value = RhpNewFastAllocation.ConstructorFailureSeed();
        throw RhpNewFastAllocation.ConstructorFailureOriginal();
    }

    [MethodImpl(MethodImplOptions.NoInlining)] internal static ulong Read() => s_value;
}

internal static unsafe partial class RhpNewFastAllocation
{
    // Original compiler geometry, EH records, callback stack and both real
    // transfers must be reviewed before this gate can be enabled.
    private const bool ConstructorFailureTransferAdmitted = false;
    private struct ConstructorFailureStorage { internal fixed ulong Words[1024]; }
    private struct ConstructorFailureRoots { internal RhpNewFastResult Original, Wrapper; }
    private struct ConstructorFailureScratch { internal fixed ulong Words[210]; }
    private static ConstructorFailureStorage s_constructorFailureStorage;
    private static ConstructorFailureRoots s_constructorFailureRoots;
    private static ConstructorFailureScratch s_constructorFailureScratch;
    private static CompilerClassConstructorInvocation s_constructorFailureInvocation;
    private static ulong s_constructorFailureContext, s_constructorFailureBodyCount;
    private static ulong s_constructorFailureInnerEntries, s_constructorFailureOuterEntries;
    private static ulong s_constructorFailureReentries;
    private static ulong s_constructorFailureReferenceStores;
    private static ulong s_constructorFailureNonGcBase;
    private static X64UnwindContext s_constructorFailureFirstCapture, s_constructorFailureInvokeContext, s_constructorFailureOuterContext;
    private static EhControl s_constructorFailureControl;
    private static ulong s_constructorFailureFirstTransfer;
    // Provisional helper geometry remains behind the closed gate until the
    // final compiler output and complete callback stack are independently read.
    private const uint ConstructorFailureEnsureBytes = 505, ConstructorFailureEnsureReturn = 494;
    private const uint ConstructorFailureEnsureAllocation = 128, ConstructorFailureEnsureStack = 136;

    [System.Runtime.RuntimeImport("*", "CLROS_CctorFailure_InvokeClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong ConstructorFailureInvokeClauses();
    [System.Runtime.RuntimeImport("*", "CLROS_CctorFailure_OuterClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)] private static extern ulong ConstructorFailureOuterClauses();
    [System.Runtime.RuntimeImport("*", "RhTypeCast_IsInstanceOfException")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern bool ConstructorFailureRuntimeMatch(Internal.Runtime.MethodTable* target, object value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ConstructorFailureGeometryReady() => ConstructorFailureTransferAdmitted;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ulong ConstructorFailureSeed() => s_ehSeed;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static System.Exception ConstructorFailureOriginal() =>
        ConstructorFailureObject(s_constructorFailureRoots.Original.ObjectReference);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ConstructorFailureWrapperType() =>
        (ulong)Internal.Runtime.MethodTable.Of<System.TypeInitializationException>();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ConstructorFailureBaseType() => (ulong)Internal.Runtime.MethodTable.Of<System.Exception>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunConstructorFailure()
    {
        if (!BootSequence.IsConstructorFailureEnabled() || s_ehPhase != 0 ||
            s_constructorFailureRoots.Original.ObjectReference != 0 || s_constructorFailureBodyCount != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* metadata = &s_ehStorage)
        fixed (ConstructorFailureStorage* storage = &s_constructorFailureStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)metadata, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes || sizeof(X64UnwindContext) != 240 ||
                sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32 || !CheckEhStack()) RejectEh(2);
            var constructors = CompilerClassConstructorExecution.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage,
                (ulong)sizeof(ConstructorFailureStorage), 4, 4);
            if (constructors != CompilerClassConstructorStatus.Success ||
                CompilerClassConstructorExecution.RunEager((byte*)storage, (ulong)sizeof(ConstructorFailureStorage))
                    != CompilerClassConstructorStatus.Success) RejectEh(3);
            EhLine(0, s_gcHandle, (ulong)metadata, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived original = NewCatchException(s_ehSeed);
        s_constructorFailureRoots.Original = s_ehReceipt;
        fixed (ConstructorFailureRoots* roots = &s_constructorFailureRoots)
        {
            var collected = CollectGcGraph(&roots->Original, 1);
            if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
                !ConstructorFailureRootRetained(Bits(original)) || !EhObjectIntact()) RejectEh(4);
            EhLine(1, Bits(original), s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        }
        bool same = ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)EhDerivedType(), original);
        bool baseMatch = ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)ConstructorFailureBaseType(), original);
        bool wrapped = ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)ConstructorFailureWrapperType(), original);
        bool nil = ConstructorFailureRuntimeMatch(null, null);
        if (!same || !baseMatch || wrapped || nil) RejectEh(5);
        EhLine(2, same ? 1UL : 0UL, baseMatch ? 1UL : 0UL, wrapped ? 1UL : 0UL, nil ? 1UL : 0UL);
        s_ehPhase = 3;
        ulong result = CtorFailureOuter();
        if (s_ehPhase != 13 || s_constructorFailureBodyCount != 1 || s_constructorFailureInnerEntries != 1 ||
            s_constructorFailureOuterEntries != 1 || s_constructorFailureReentries != 1 || s_ehContinuations != 1 || result != s_ehSeed + 1 ||
            !ConstructorFailureWrapperIntact() || !EhObjectIntact() || !CheckEhStack()) RejectEh(6);
        fixed (ConstructorFailureStorage* storage = &s_constructorFailureStorage)
        {
            var repeated = CompilerClassConstructorExecution.BeginLazy((byte*)storage,
                (ulong)sizeof(ConstructorFailureStorage), s_constructorFailureContext, out var denied);
            var observed = CompilerClassConstructorExecution.Observe((byte*)storage,
                (ulong)sizeof(ConstructorFailureStorage), out var progress);
            if (repeated != CompilerClassConstructorStatus.InitializationFailed || denied.RequiresInvocation ||
                observed != CompilerClassConstructorStatus.Success || progress.LazyCount != 1 ||
                progress.ActiveLazyCount != 0 || progress.LazyCompleted != 0 ||
                GcReadWord(s_constructorFailureContext) != s_constructorFailureInvocation.Target) RejectEh(7);
            // This is a noninvoking host-operation rejection, not a second real
            // compiler static access or cached-exception rethrow qualification.
            EhLine(16, (ulong)repeated, GcReadWord(s_constructorFailureContext), progress.LazyCount, progress.ActiveLazyCount);
        }
        EhLine(17, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        EhLine(18, s_constructorFailureRoots.Original.ObjectReference, s_constructorFailureRoots.Wrapper.ObjectReference,
            s_constructorFailureRoots.Original.AllocationCount, s_constructorFailureRoots.Wrapper.AllocationCount);
        s_ehPhase = 14; s_constructorFailureRoots = default; s_ehReceipt = default;
        EhText(1); EhText(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnsureCompilerClassConstructor(ulong context)
    {
        if (s_ehPhase == 5 && context == s_constructorFailureContext && s_constructorFailureReentries == 0)
        {
            fixed (ConstructorFailureStorage* storage = &s_constructorFailureStorage)
            {
                var reentry = CompilerClassConstructorExecution.BeginLazy((byte*)storage,
                    (ulong)sizeof(ConstructorFailureStorage), context, out var ticket);
                if (reentry != CompilerClassConstructorStatus.Success || ticket.RequiresInvocation ||
                    GcReadWord(context) != s_constructorFailureInvocation.Target) RejectEh(30);
                s_constructorFailureReentries = 1; return;
            }
        }
        if (s_ehPhase != 3 || s_constructorFailureContext != 0 || s_constructorFailureInvocation.RequiresInvocation)
            RejectEh(8);
        fixed (ConstructorFailureStorage* storage = &s_constructorFailureStorage)
        {
            var begin = CompilerClassConstructorExecution.BeginLazy((byte*)storage,
                (ulong)sizeof(ConstructorFailureStorage), context, out var invocation);
            if (begin != CompilerClassConstructorStatus.Success || !invocation.RequiresInvocation ||
                invocation.Context != context || invocation.RecordIndex != 0 || invocation.ParentIndex != 0xFFFFFFFFU)
                RejectEh(9);
            s_constructorFailureInvocation = invocation; s_constructorFailureContext = context;
            s_constructorFailureNonGcBase = context + 8; s_ehPhase = 4;
            EhLine(3, context, invocation.Target, invocation.RecordIndex, invocation.ParentIndex);
            CtorFailureInvoke(invocation.Target);
            // The selected constructor always throws. Normal return is denied;
            // completion and failure are recorded only by their real paths.
            RejectEh(10);
        }
    }

    internal static void ObserveCompilerNonGcBase(ulong context, ulong address)
    {
        if (context != s_constructorFailureContext || address != s_constructorFailureNonGcBase) RejectEh(11);
    }
    internal static void ObserveCompilerGcBase(ulong context, object value) { RejectEh(12); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ConstructorFailureBodyEntered()
    {
        if (s_ehPhase != 4 || s_constructorFailureBodyCount != 0 || !EhObjectIntact() ||
            GcReadWord(s_constructorFailureContext) != s_constructorFailureInvocation.Target) RejectEh(13);
        s_constructorFailureBodyCount = 1; s_ehPhase = 5;
        EhLine(4, s_constructorFailureContext, s_constructorFailureInvocation.Target, s_constructorFailureBodyCount, s_ehSeed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CtorFailureInvoke(ulong target)
    {
        try { ((delegate* managed<void>)target)(); }
        catch (System.Exception original)
        {
            System.TypeInitializationException wrapper = ConstructorFailureWrap(original);
            throw wrapper;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong CtorFailureOuter()
    {
        ulong result = 0;
        try { result = ConstructorFailureFixture.Read(); }
        catch (System.TypeInitializationException wrapper)
        {
            ConstructorFailureOuterEntered(wrapper);
            result = GcReadWord(Bits(wrapper.InnerException) + 8) + 1;
        }
        ConstructorFailureContinued(result);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static System.TypeInitializationException ConstructorFailureWrap(System.Exception original)
    {
        if (s_ehPhase != 6 || s_constructorFailureInnerEntries != 0 || s_constructorFailureReentries != 1 || Bits(original) != s_ehReceipt.ObjectReference ||
            !ConstructorFailureRootRetained(Bits(original)) || !EhObjectIntact()) RejectEh(14);
        s_constructorFailureInnerEntries = 1; s_ehPhase = 7;
        EhLine(8, Bits(original), s_constructorFailureContext, s_constructorFailureInnerEntries, s_constructorFailureBodyCount);
        fixed (ConstructorFailureStorage* storage = &s_constructorFailureStorage)
        {
            var failed = CompilerClassConstructorExecution.FinishLazy((byte*)storage,
                (ulong)sizeof(ConstructorFailureStorage), s_constructorFailureInvocation, returnedNormally: false);
            if (failed != CompilerClassConstructorStatus.Success ||
                GcReadWord(s_constructorFailureContext) != s_constructorFailureInvocation.Target) RejectEh(15);
            EhLine(9, (ulong)failed, s_constructorFailureContext, GcReadWord(s_constructorFailureContext), s_constructorFailureBodyCount);
        }
        s_ehPhase = 8;
        var wrapper = new System.TypeInitializationException(original, s_constructorFailureContext);
        if (Bits(wrapper) != s_constructorFailureRoots.Wrapper.ObjectReference || !ConstructorFailureWrapperIntact()) RejectEh(16);
        fixed (ConstructorFailureRoots* roots = &s_constructorFailureRoots)
        {
            var collected = CollectGcGraph(&roots->Original, 2);
            if (!collected.Succeeded || collected.LiveObjects != 4 || collected.ReclaimedObjects != 0 ||
                !ConstructorFailureWrapperIntact() || !EhObjectIntact()) RejectEh(17);
            EhLine(10, Bits(wrapper), Bits(wrapper.InnerException), wrapper.ConstructorContext, collected.LiveObjects);
        }
        s_ehPhase = 9;
        bool same = ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)ConstructorFailureWrapperType(), wrapper);
        bool baseMatch = ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)ConstructorFailureBaseType(), wrapper);
        if (!same || !baseMatch || wrapper.ConstructorContext != s_constructorFailureContext) RejectEh(18);
        EhLine(11, Bits(wrapper), same ? 1UL : 0UL, baseMatch ? 1UL : 0UL, s_constructorFailureReferenceStores);
        return wrapper;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConstructorFailureOuterEntered(System.TypeInitializationException wrapper)
    {
        if (s_ehPhase != 10 || s_constructorFailureOuterEntries != 0 || !ConstructorFailureWrapperIntact() || !EhObjectIntact() ||
            Bits(wrapper) != s_constructorFailureRoots.Wrapper.ObjectReference) RejectEh(19);
        s_constructorFailureOuterEntries = 1; s_ehPhase = 11;
        EhLine(13, Bits(wrapper), Bits(wrapper.InnerException), wrapper.ConstructorContext, s_constructorFailureOuterEntries);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConstructorFailureContinued(ulong result)
    {
        if (s_ehPhase != 12 || result != s_ehSeed + 1 || s_ehContinuations != 0 ||
            !ConstructorFailureWrapperIntact() || !EhObjectIntact()) RejectEh(20);
        s_ehContinuations = 1; s_ehPhase = 13;
        EhLine(15, s_constructorFailureRoots.Wrapper.ObjectReference, s_constructorFailureRoots.Original.ObjectReference,
            result, s_ehContinuations);
    }

    private static ulong AllocateConstructorFailureWrapper(ulong token)
    {
        if (s_ehPhase != 8 || token != ConstructorFailureWrapperType() ||
            s_constructorFailureRoots.Wrapper.ObjectReference != 0 || !EhObjectIntact()) RejectEh(21);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(
                NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, token);
            if (!map.Succeeded || map.SeriesCount != 1 || map.Prefix.BaseSize != 32 ||
                !map.TryGetSeries(0, out var series) || series.OffsetFromReference != 8 || series.Bytes != 8) RejectEh(22);
            var allocated = GuestTestRhpNewFast.TryAllocate(s_access, &owner->State, (ulong)sizeof(GcCollectingStorage),
                NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, map.Prefix, token);
            if (!allocated.AllocationSucceeded || allocated.AllocationCount != 10 || allocated.BytesConsumed != 32 ||
                GcReadWord(allocated.ObjectReference + 8) != 0 || GcReadWord(allocated.ObjectReference + 16) != 0) RejectEh(23);
            s_constructorFailureRoots.Wrapper = allocated; return allocated.ObjectReference;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ConstructorFailureWrapperIntact()
    {
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
            return GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), s_constructorFailureRoots.Wrapper) == RhpNewFastStatus.Success &&
                s_constructorFailureRoots.Wrapper.MethodTableToken == ConstructorFailureWrapperType() &&
                s_constructorFailureReferenceStores == 1 &&
                GcReadWord(s_constructorFailureRoots.Wrapper.ObjectReference + 8) == s_ehReceipt.ObjectReference &&
                GcReadWord(s_constructorFailureRoots.Wrapper.ObjectReference + 16) == s_constructorFailureContext;
    }

    private static uint ApplyConstructorFailureReference(ulong operation, ulong destination, ulong operand, ulong bytes)
    {
        if (s_gcPhase != 5 || s_ehPhase != 8 || operation != 1 || bytes != 8 ||
            s_constructorFailureReferenceStores != 0 || s_constructorFailureInnerEntries != 1 ||
            s_constructorFailureRoots.Wrapper.ObjectReference == 0 ||
            destination != s_constructorFailureRoots.Wrapper.ObjectReference + 8 ||
            operand != s_constructorFailureRoots.Original.ObjectReference || !ConstructorFailureOriginalRootIntact()) return 0;
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            if (GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), s_constructorFailureRoots.Wrapper) != RhpNewFastStatus.Success ||
                s_constructorFailureRoots.Wrapper.MethodTableToken != ConstructorFailureWrapperType() ||
                GcReadWord(destination) != 0 || GcReadWord(destination + 8) != 0) return 0;
            var stored = CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.TryStore(s_access,
                CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage, destination - (ulong)s_access.Backing, operand);
            if (!stored.Succeeded || stored.BytesWritten != 8 || GcReadWord(destination) != operand) return 0;
            s_constructorFailureReferenceStores = 1; return 1;
        }
    }
    private static bool ConstructorFailureRootRetained(ulong reference) =>
        reference != 0 && (reference == s_constructorFailureRoots.Original.ObjectReference && EhObjectIntact() ||
            reference == s_constructorFailureRoots.Wrapper.ObjectReference && ConstructorFailureWrapperIntact());
    private static System.Exception ConstructorFailureObject(ulong reference)
    {
        nuint address = (nuint)reference; return Unsafe.As<nuint, System.Exception>(ref address);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchConstructorFailureExceptionType(ulong targetType, object value)
    {
        ulong reference = Bits(value);
        if (reference == 0) return false;
        if ((s_ehPhase != 2 && s_ehPhase != 5 && s_ehPhase != 9) || !ConstructorFailureRootRetained(reference))
            { RejectEh(24); return false; }
        RhpNewFastResult receipt = reference == s_ehReceipt.ObjectReference ?
            s_constructorFailureRoots.Original : s_constructorFailureRoots.Wrapper;
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), receipt, reference, targetType);
            if (!match.Succeeded || !ConstructorFailureRootRetained(reference)) { RejectEh(25); return false; }
            return match.Matches;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_CctorFailure_Select")]
    private static uint ConstructorFailureSelect(ulong exception, X64UnwindContext* captured,
        EhTransfer* transfer, EhControl* control)
    {
        if (!BridgeLayout(captured, transfer, control) || !ControlAdmitted(control) ||
            !CheckEhStack() || !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) return RejectEh(31);
        *transfer = default;
        if (!ConstructorFailureGeometryReady()) return RejectEh(26);
        if (captured->Rsp != (ulong)captured + 816 || !StackSpan(captured->Rsp, 8) ||
            !ConstructorFailureRootRetained(exception)) return RejectEh(32);
        if (s_ehPhase == 5) return ConstructorFailureSelectOriginal(exception, captured, transfer, control);
        if (s_ehPhase == 9) return ConstructorFailureSelectWrapper(exception, captured, transfer, control);
        return RejectEh(27);
    }

    private static uint ConstructorFailureSelectOriginal(ulong exception, X64UnwindContext* captured,
        EhTransfer* transfer, EhControl* control)
    {
        ulong constructor = s_constructorFailureInvocation.Target;
        ulong invoke = (ulong)(delegate* managed<ulong, void>)&CtorFailureInvoke;
        ulong ensure = (ulong)(delegate* managed<ulong, void>)&EnsureCompilerClassConstructor;
        ulong runner = (ulong)(delegate* managed<StaticClassConstructionContext*, void>)&ClassConstructorRunner.EnsureClassConstructorRun;
        ulong helper = (ulong)(delegate* managed<StaticClassConstructionContext*, nint, nint>)&ClassConstructorRunner.CheckStaticClassConstructionReturnNonGCStaticBase;
        ulong read = (ulong)(delegate* managed<ulong>)&ConstructorFailureFixture.Read;
        ulong outer = (ulong)(delegate* managed<ulong>)&CtorFailureOuter;
        if (exception != s_ehReceipt.ObjectReference || captured->Rip != constructor + 54 ||
            s_constructorFailureReentries != 1 || s_constructorFailureInnerEntries != 0 || s_constructorFailureOuterEntries != 0 ||
            s_constructorFailureFirstTransfer != 0 || s_ehTransferAddress != 0 || s_ehContinuations != 0 ||
            GcReadWord(s_constructorFailureContext) != constructor || !ConstructorFailureOriginalRootIntact()) return RejectEh(33);
        // A rejected scalar candidate cannot change the genuine capture or
        // expose a selected target. The raw instruction geometry is separately
        // admitted by the original-image verifier.
        bool bad = (captured->Rip ^ 1UL) == constructor + 54;
        if (bad || transfer->Funclet != 0 || transfer->Reserved != 0) return RejectEh(34);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* metadata = &s_ehStorage)
        fixed (ConstructorFailureScratch* scratch = &s_constructorFailureScratch)
        {
            if (TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (ulong)scratch,
                (ulong)sizeof(ConstructorFailureScratch), TypeManagerRegisteredRangeKind.MutableData) != TypeManagerModuleStatus.Success)
                return RejectEh(35);
            X64UnwindContext* frames = (X64UnwindContext*)scratch;
            frames[0] = *captured;
            if (!ConstructorFailureFunction((byte*)metadata, constructor, 0, 55, 40, 56, 6) ||
                !ConstructorFailureFunction((byte*)metadata, invoke, 0, 35, 32, 40, 5) ||
                !ConstructorFailureFunction((byte*)metadata, invoke, 35, 53, 40, 40, 4) ||
                !ConstructorFailureFunction((byte*)metadata, ensure, 0, ConstructorFailureEnsureBytes, ConstructorFailureEnsureAllocation, ConstructorFailureEnsureStack, 8) ||
                !ConstructorFailureFunction((byte*)metadata, runner, 0, 15, 40, 40, 4) ||
                !ConstructorFailureFunction((byte*)metadata, helper, 0, 41, 40, 56, 6) ||
                !ConstructorFailureFunction((byte*)metadata, read, 0, 35, 32, 40, 5) ||
                !ConstructorFailureFunction((byte*)metadata, outer, 0, 40, 40, 56, 6) ||
                !ConstructorFailureFunction((byte*)metadata, outer, 40, 101, 40, 40, 4)) return RejectEh(36);
            if (!ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames, frames + 1, invoke + 28, 64) ||
                frames[1].Rbp != frames[1].Rsp + 32 ||
                !ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames + 1, frames + 2, ensure + ConstructorFailureEnsureReturn, 48) ||
                !ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames + 2, frames + 3, runner + 9, ConstructorFailureEnsureStack + 8) ||
                !ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames + 3, frames + 4, helper + 20, 48) ||
                !ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames + 4, frames + 5, read + 33, 64) ||
                !ConstructorFailureUnwind((byte*)type, (byte*)metadata, frames + 5, frames + 6, outer + 16, 48) ||
                frames[6].Rbp != frames[6].Rsp + 48 || !StackSpan(frames[6].Rsp, 72)) return RejectEh(37);
            // ILC removes the source initializer because both normal and catch
            // paths assign the result. This slot is read only after the actual
            // outer funclet has written it, never as initial-state authority.
            if (!ConstructorFailureNoEh((byte*)type, (byte*)metadata, constructor, 55) ||
                !ConstructorFailureNoEh((byte*)type, (byte*)metadata, ensure, ConstructorFailureEnsureBytes) ||
                !ConstructorFailureNoEh((byte*)type, (byte*)metadata, runner, 15) ||
                !ConstructorFailureNoEh((byte*)type, (byte*)metadata, helper, 41) ||
                !ConstructorFailureNoEh((byte*)type, (byte*)metadata, read, 35)) return RejectEh(38);
            var inner = ConstructorFailureReadRow((byte*)type, (byte*)metadata, invoke, 53, ConstructorFailureInvokeClauses());
            var outerRow = ConstructorFailureReadRow((byte*)type, (byte*)metadata, outer, 101, ConstructorFailureOuterClauses());
            if (!ConstructorFailureClause(inner, invoke + 35, ConstructorFailureBaseType(), 27) ||
                !ConstructorFailureClause(outerRow, outer + 40, ConstructorFailureWrapperType(), 15) ||
                !ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)inner.TypeAddress, ConstructorFailureObject(exception)) ||
                !ConstructorFailureOriginalRootIntact() || !CheckEhStack()) return RejectEh(39);
            s_constructorFailureFirstCapture = *captured;
            s_constructorFailureInvokeContext = frames[1]; s_constructorFailureOuterContext = frames[6];
            s_constructorFailureFirstTransfer = (ulong)transfer;
            s_constructorFailureControl = new EhControl { Flags = control->Flags, Cr4 = control->Cr4, Mxcsr = control->Mxcsr, X87 = control->X87 };
            *transfer = new EhTransfer { Funclet = inner.HandlerAddress, Caller = frames[1] };
            EhLine(5, captured->Rip, captured->Rsp, frames[1].Rip, frames[1].Rsp);
            EhLine(6, frames[6].Rip, frames[6].Rsp, frames[6].Rbp, s_constructorFailureContext);
            EhLine(7, inner.HandlerAddress, outerRow.HandlerAddress, inner.TypeAddress, outerRow.TypeAddress);
            s_ehPhase = 6; return 1;
        }
    }

    private static uint ConstructorFailureSelectWrapper(ulong exception, X64UnwindContext* captured,
        EhTransfer* transfer, EhControl* control)
    {
        ulong invoke = (ulong)(delegate* managed<ulong, void>)&CtorFailureInvoke;
        ulong outer = (ulong)(delegate* managed<ulong>)&CtorFailureOuter;
        if (exception != s_constructorFailureRoots.Wrapper.ObjectReference || s_constructorFailureInnerEntries != 1 ||
            s_constructorFailureOuterEntries != 0 || s_ehContinuations != 0 || s_ehTransferAddress != 0 ||
            s_constructorFailureFirstTransfer < 320 || captured->Rip != invoke + 52 ||
            captured->Rsp != s_constructorFailureFirstTransfer - 320 ||
            !ConstructorFailureFirstBridgeIntact() || !ConstructorFailureContextEqual(captured, ref s_constructorFailureInvokeContext, 2, false) ||
            !ConstructorFailureControlEqual(control) || !ConstructorFailureWrapperIntact() ||
            GcReadWord(s_constructorFailureContext) != s_constructorFailureInvocation.Target) return RejectEh(40);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* metadata = &s_ehStorage)
        {
            var row = ConstructorFailureReadRow((byte*)type, (byte*)metadata, outer, 101, ConstructorFailureOuterClauses());
            if (!ConstructorFailureClause(row, outer + 40, ConstructorFailureWrapperType(), 15) ||
                !ConstructorFailureRuntimeMatch((Internal.Runtime.MethodTable*)row.TypeAddress, ConstructorFailureObject(exception)) ||
                !ConstructorFailureFirstBridgeIntact() || !ConstructorFailureWrapperIntact() || !CheckEhStack()) return RejectEh(41);
            s_ehSelected = row.HandlerAddress; s_ehTransferAddress = (ulong)transfer;
            *transfer = new EhTransfer { Funclet = row.HandlerAddress, Caller = s_constructorFailureOuterContext };
            EhLine(12, captured->Rip, captured->Rsp, row.HandlerAddress, s_constructorFailureOuterContext.Rsp);
            s_ehPhase = 10; return 1;
        }
    }
    [System.Runtime.RuntimeExport("CLROS_CctorFailure_ValidateContinuation")]
    private static uint ConstructorFailureValidateContinuation(ulong continuation, X64UnwindContext* actual,
        EhTransfer* transfer, EhControl* control)
    {
        if (!ConstructorFailureGeometryReady()) return RejectEh(28);
        ulong outer = (ulong)(delegate* managed<ulong>)&CtorFailureOuter;
        if (s_ehPhase != 11 || s_constructorFailureInnerEntries != 1 || s_constructorFailureOuterEntries != 1 ||
            s_ehContinuations != 0 || (ulong)transfer != s_ehTransferAddress || (ulong)transfer < 272 ||
            !StackSpan((ulong)transfer - 272, 848) || (ulong)actual != (ulong)transfer + 256 ||
            (ulong)control != (ulong)transfer + 504 || !ConstructorFailureControlEqual(control) ||
            !ConstructorFailureControlEqual(control + 1) || !ConstructorFailureFirstBridgeIntact() ||
            transfer->Funclet != s_ehSelected || transfer->Reserved != 0 || s_ehSelected != outer + 40 ||
            !ConstructorFailureContextEqual(&transfer->Caller, ref s_constructorFailureOuterContext, 0, false) ||
            continuation != outer + 20 || actual->Rip != continuation ||
            !ConstructorFailureContextEqual(actual, ref s_constructorFailureOuterContext, 1, true) ||
            actual->Rbx != s_constructorFailureRoots.Wrapper.ObjectReference || actual->Rsp <= (ulong)transfer + 576 ||
            !ConstructorFailureWrapperIntact() || !ConstructorFailureOriginalRootIntact() || !CheckEhStack() ||
            GcReadWord(s_constructorFailureOuterContext.Rbp - 16) != s_ehSeed + 1) return RejectEh(42);
        EhLine(14, continuation, actual->Rsp, actual->Rbp, s_constructorFailureOuterEntries);
        s_ehPhase = 12; return 1;
    }

    private static bool ConstructorFailureFunction(byte* metadata, ulong method, uint begin, uint end,
        uint allocation, uint stack, uint prolog)
    {
        if (method < s_gcImage.Start || end <= begin || method - s_gcImage.Start > 0x7FFFFFFF) return false;
        var row = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(metadata, EhStorageBytes, method + begin);
        return row.Succeeded && row.ImageBase == s_gcImage.Start && row.ModuleHandle == s_gcHandle &&
            row.ChainDepth == 0 && row.FrameRegister == 0 && row.FrameOffset == 0 &&
            row.FixedAllocationBytes == allocation && row.StackBytes == stack && row.PrologBytes == prolog &&
            row.BeginRva == method - s_gcImage.Start + begin && row.EndRva == method - s_gcImage.Start + end;
    }
    private static bool ConstructorFailureUnwind(byte* type, byte* metadata, X64UnwindContext* input,
        X64UnwindContext* output, ulong rip, uint consumed)
    {
        var result = BootSequence.RunConstructorFailureUnwind(s_gcCapture, s_gcMapping,
            type, (ulong)sizeof(GcTypeStorage), s_gcHandle, metadata, EhStorageBytes,
            (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *input);
        if (!result.Succeeded || result.InEpilog || result.Caller.Rip != rip ||
            result.Caller.Rsp != input->Rsp + consumed || !StackSpan(result.Caller.Rsp, 8)) return false;
        *output = result.Caller; return true;
    }
    private static bool ConstructorFailureNoEh(byte* type, byte* metadata, ulong method, uint bytes)
    {
        if (method < s_gcImage.Start || method - s_gcImage.Start > 0x7FFFFFFF) return false;
        var row = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
            s_gcCapture, s_gcMapping, type, (ulong)sizeof(GcTypeStorage), s_gcHandle, metadata, EhStorageBytes,
            new((uint)(method - s_gcImage.Start), bytes, 0, 0), 0);
        return row.Status == NativeAotEhClauseStatus.NoEhInfo;
    }
    private static NativeAotEhClauseResult ConstructorFailureReadRow(byte* type, byte* metadata,
        ulong method, uint bytes, ulong table)
    {
        if (method < s_gcImage.Start || method - s_gcImage.Start > 0x7FFFFFFF ||
            table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF) return default;
        return NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
            s_gcCapture, s_gcMapping, type, (ulong)sizeof(GcTypeStorage), s_gcHandle, metadata, EhStorageBytes,
            new((uint)(method - s_gcImage.Start), bytes, (uint)(table - s_gcImage.Start), 8), 0);
    }
    private static bool ConstructorFailureClause(NativeAotEhClauseResult row, ulong handler, ulong type, uint pc) =>
        row.Succeeded && row.Count == 1 && row.Kind == NativeAotEhClauseKind.Typed &&
        row.HandlerAddress == handler && row.TypeAddress == type && row.ContainsOffset(pc);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ConstructorFailureOriginalRootIntact()
    {
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
            return s_constructorFailureRoots.Original.ObjectReference == s_ehReceipt.ObjectReference &&
                s_constructorFailureRoots.Original.MethodTableToken == EhDerivedType() &&
                GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &owner->State,
                    (ulong)sizeof(GcCollectingStorage), s_constructorFailureRoots.Original) == RhpNewFastStatus.Success && EhObjectIntact();
    }
    private static bool ConstructorFailureContextEqual(X64UnwindContext* actual, ref X64UnwindContext expected,
        int first, bool rbxChanged)
    {
        fixed (X64UnwindContext* saved = &expected)
            for (int word = first; word < 30; word++)
                if (!(rbxChanged && word == 2) && ((ulong*)actual)[word] != ((ulong*)saved)[word]) return false;
        return true;
    }
    private static bool ConstructorFailureControlEqual(EhControl* control) =>
        ControlAdmitted(control) && control->Cr4 == s_constructorFailureControl.Cr4 &&
        control->Mxcsr == s_constructorFailureControl.Mxcsr && control->X87 == s_constructorFailureControl.X87 &&
        ((control->Flags ^ s_constructorFailureControl.Flags) & 0x600) == 0;
    private static bool ConstructorFailureFirstBridgeIntact()
    {
        ulong invoke = (ulong)(delegate* managed<ulong, void>)&CtorFailureInvoke;
        ulong address = s_constructorFailureFirstTransfer;
        if (address < 272 || !StackSpan(address - 272, 848)) return false;
        EhTransfer* transfer = (EhTransfer*)address;
        return transfer->Funclet == invoke + 35 && transfer->Reserved == 0 &&
            ConstructorFailureContextEqual(&transfer->Caller, ref s_constructorFailureInvokeContext, 0, false) &&
            ConstructorFailureContextEqual((X64UnwindContext*)(address - 240), ref s_constructorFailureFirstCapture, 0, false) &&
            GcReadWord(address + 496) == s_constructorFailureRoots.Original.ObjectReference &&
            ConstructorFailureControlEqual((EhControl*)(address + 504)) && ConstructorFailureOriginalRootIntact();
    }
}
#endif
