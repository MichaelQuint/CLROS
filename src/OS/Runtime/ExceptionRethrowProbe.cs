#if (NOSTDLIB_EFI && M04_RETHROW_PROBE) || RETHROW_CORE_TEST
using OS.Kernel.Runtime;
#if NOSTDLIB_EFI && M04_RETHROW_PROBE
using OS.Boot;
using System.Runtime.CompilerServices;
#endif

namespace OS.Runtime;

// Pure predicates for the measured step1096 compiler shape. They never authorize
// a transfer by themselves: the EFI caller must retain and revalidate the actual
// module, allocation/root, stack mapping, bridge records and control snapshots.
// Actual linked geometry and stack review are required before enabling transfer.
internal static unsafe class RethrowAdmission
{
    internal const uint ThrowBytes = 10, ThrowReturn = 9;
    internal const uint InnerBytes = 88, InnerReturn = 25, Cleanup = 46, InnerCatch = 69, RethrowReturn = 87;
    internal const uint OuterBytes = 89, OuterReturn = 26, OuterContinuation = 27, OuterCatch = 51;

    internal static bool InitialFramesMatch(ulong throwingSp, ulong innerSp, ulong innerBp) =>
        throwingSp != 0 && throwingSp <= ulong.MaxValue - 96 &&
        innerSp == throwingSp + 48 && innerBp == innerSp + 48;

    internal static bool OuterFramesMatch(ulong innerSp, ulong outerSp, ulong outerBp) =>
        innerSp != 0 && innerSp <= ulong.MaxValue - 112 &&
        outerSp == innerSp + 64 && outerBp == outerSp + 48;

    internal static bool ActiveStateMatches(ulong phase, ulong exception, ulong retainedException,
        ulong finallyEntries, ulong innerEntries, ulong outerEntries, ulong continuations,
        ulong incomingArgument) =>
        phase == 7 && exception != 0 && exception == retainedException &&
        finallyEntries == 1 && innerEntries == 1 && outerEntries == 0 && continuations == 0 &&
        incomingArgument == 0;

    internal static bool RethrowFrameMatches(ulong originalTransfer, ulong capturedAddress, ulong innerMethod,
        X64UnwindContext* captured, X64UnwindContext* inner)
    {
        // Original bridge base B: transfer B+272; inner SP B+896; inner BP
        // B+944. Inner catch is called at B-8 and allocates 40. Its rethrow
        // creates a second 840-byte bridge; capture B-864, genuine SP B-48.
        if (captured == null || inner == null || originalTransfer < 1168 ||
            originalTransfer > ulong.MaxValue - 672 || (originalTransfer & 15) != 0 ||
            innerMethod == 0 || innerMethod > ulong.MaxValue - InnerBytes ||
            capturedAddress != originalTransfer - 1136 ||
            captured->Rip != innerMethod + RethrowReturn || captured->Rsp != originalTransfer - 320 ||
            inner->Rip != innerMethod + InnerReturn || inner->Rsp != originalTransfer + 624 ||
            inner->Rbp != originalTransfer + 672) return false;
        // The measured inner catch changes no nonvolatile GPR or XMM register.
        // Only RIP/SP differ from the retained original handling context.
        for (int word = 2; word < sizeof(X64UnwindContext) / 8; word++)
            if (((ulong*)captured)[word] != ((ulong*)inner)[word]) return false;
        return sizeof(X64UnwindContext) == 240;
    }

    internal static bool AcceptInnerCatch(NativeAotEhClauseResult caught, ulong method, ulong type, uint pc) =>
        MethodFits(method, InnerBytes) && type != 0 && pc == InnerReturn - 1 &&
        caught.Succeeded && caught.Index == 1 && caught.Count == 2 && !caught.IsTryGroupBoundary &&
        caught.Kind == NativeAotEhClauseKind.Typed && caught.FilterAddress == 0 &&
        caught.TypeAddress == type && caught.TryStartOffset == 20 && caught.TryEndOffset == 40 &&
        caught.HandlerAddress == method + InnerCatch && caught.ContainsOffset(pc);

    internal static bool AcceptInnerCleanup(NativeAotEhClauseResult cleanup, NativeAotEhClauseResult caught,
        ulong method, ulong type, uint pc) =>
        AcceptInnerCatch(caught, method, type, pc) && cleanup.Succeeded &&
        cleanup.Index == 0 && cleanup.Count == 2 && !cleanup.IsTryGroupBoundary &&
        cleanup.Kind == NativeAotEhClauseKind.FaultOrFinally && cleanup.FilterAddress == 0 && cleanup.TypeAddress == 0 &&
        cleanup.TryStartOffset == 20 && cleanup.TryEndOffset == 26 &&
        cleanup.HandlerAddress == method + Cleanup && cleanup.ContainsOffset(pc);

    internal static bool AcceptOuterCatch(NativeAotEhClauseResult caught, ulong method, ulong type, uint pc) =>
        MethodFits(method, OuterBytes) && type != 0 && pc == OuterReturn - 1 &&
        caught.Succeeded && caught.Index == 0 && caught.Count == 1 && !caught.IsTryGroupBoundary &&
        caught.Kind == NativeAotEhClauseKind.Typed && caught.FilterAddress == 0 &&
        caught.TypeAddress == type && caught.TryStartOffset == 21 && caught.TryEndOffset == 27 &&
        caught.HandlerAddress == method + OuterCatch && caught.ContainsOffset(pc);

    internal static bool OuterContinuationMatches(ulong method, ulong exception,
        X64UnwindContext* actual, X64UnwindContext* selected)
    {
        if (!MethodFits(method, OuterBytes) || exception == 0 || actual == null || selected == null ||
            selected->Rip != method + OuterReturn || actual->Rip != method + OuterContinuation ||
            actual->Rbx != exception || sizeof(X64UnwindContext) != 240) return false;
        // This compiler catch changes only RIP and RBX. Preserve its real
        // result rather than replacing it with the pre-catch nonvolatiles.
        for (int word = 1; word < sizeof(X64UnwindContext) / 8; word++)
            if (word != 2 && ((ulong*)actual)[word] != ((ulong*)selected)[word]) return false;
        return true;
    }

    private static bool MethodFits(ulong method, uint bytes) => method != 0 && method <= ulong.MaxValue - bytes;
}
#if NOSTDLIB_EFI && M04_RETHROW_PROBE
internal static unsafe partial class RhpNewFastAllocation
{
    // Step1100 reviews the linked bridges, compiler frames, clauses, callback
    // stack and register preservation. Admit only this bounded probe fixture.
    private const bool RethrowTransferAdmitted = true;
    private static X64UnwindContext s_rethrowOuter;
    private static EhControl s_rethrowOuterControl;
    private static ulong s_rethrowException, s_rethrowOuterTransfer, s_rethrowOuterHandler, s_rethrowOuterEntries;

    [System.Runtime.RuntimeImport("*", "CLROS_Rethrow_InnerClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong RethrowInnerClauses();
    [System.Runtime.RuntimeImport("*", "CLROS_Rethrow_OuterClauses")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong RethrowOuterClauses();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool RethrowGeometryReady() => RethrowTransferAdmitted;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunRethrow()
    {
        if (!BootSequence.IsRethrowEnabled() || s_ehPhase != 0 || s_rethrowException != 0 ||
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
                sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32) RejectEh(3);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference || !EhObjectIntact()) RejectEh(4);
        RhpNewFastResult root = s_ehReceipt;
        var collected = CollectGcGraph(&root, 1);
        if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
            !RethrowOwnerIntact()) RejectEh(5);
        EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken, s_ehReceipt.AllocationCount, collected.LiveObjects);
        s_ehPhase = 3;
        ulong result = RethrowOuter(exception);
        if (s_ehPhase != 11 || s_ehFinallyEntries != 1 || s_ehCatchEntries != 1 || s_rethrowOuterEntries != 1 ||
            s_ehContinuations != 1 || result != s_ehSeed + 1 || !RethrowOwnerIntact() || !CheckEhStack()) RejectEh(6);
        EhLine(8, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        // No collection runs after the explicit rooted collection above. The
        // original receipt remains retained until the real continuation returns.
        s_ehPhase = 12; s_ehReceipt = default; s_rethrowException = 0;
        s_ehTransferAddress = 0; s_rethrowOuterTransfer = 0;
        s_ehCaller = default; s_rethrowOuter = default;
        EhText(1); EhText(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowThrower(System.Exception value) { throw value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowInner(SimpleCatchBase value)
    {
        ulong cleanupStamp = 0;
        try
        {
            try { RethrowThrower(value); }
            finally { RethrowFinallyEntered(value, ref cleanupStamp); }
        }
        catch (SimpleCatchBase caught)
        {
            RethrowEntered(caught, cleanupStamp);
            throw;
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong RethrowOuter(SimpleCatchBase value)
    {
        ulong result = 0;
        try { RethrowInner(value); }
        catch (SimpleCatchBase caught)
        {
            RethrowOuterEntered(caught);
            result = caught.Payload + 1;
        }
        RethrowContinued(value, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowFinallyEntered(SimpleCatchBase value, ref ulong stamp)
    {
        fixed (ulong* local = &stamp)
        {
            if (s_ehPhase != 4 || s_ehFinallyEntries != 0 || s_ehCatchEntries != 0 || s_rethrowOuterEntries != 0 ||
                s_ehContinuations != 0 || stamp != 0 || !StackSpan((ulong)local, 8) ||
                (ulong)local != s_ehCaller.Rbp - 8 || Bits(value) != s_rethrowException ||
                value.Payload != s_ehSeed || !RethrowOwnerIntact() || !CheckEhStack()) RejectEh(23);
            s_ehFinallyLocal = (ulong)local; stamp = s_ehSeed + 2;
            s_ehFinallyEntries = 1; s_ehPhase = 5;
            EhLine(9, Bits(value), (ulong)local, stamp, s_ehFinallyEntries);
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowEntered(SimpleCatchBase value, ulong stamp)
    {
        if (s_ehPhase != 6 || s_ehFinallyEntries != 1 || stamp != s_ehSeed + 2 || !RethrowStampIntact() ||
            s_ehCatchEntries != 0 || Bits(value) != s_rethrowException || !RethrowOwnerIntact() || !CheckEhStack()) RejectEh(24);
        s_ehCatchEntries = 1; s_ehPhase = 7;
        EhLine(5, Bits(value), value.Payload, stamp, s_ehCatchEntries);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowOuterEntered(SimpleCatchBase value)
    {
        if (s_ehPhase != 8 || s_rethrowOuterEntries != 0 || s_ehCatchEntries != 1 || s_ehFinallyEntries != 1 ||
            s_ehContinuations != 0 || Bits(value) != s_rethrowException || !RethrowStampIntact() ||
            !RethrowOwnerIntact() || !CheckEhStack()) RejectEh(25);
        s_rethrowOuterEntries = 1; s_ehPhase = 9;
        EhLine(13, Bits(value), value.Payload, s_rethrowOuterHandler, s_rethrowOuterEntries);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowContinued(SimpleCatchBase value, ulong result)
    {
        // The original bridge/inner stack is no longer live after the jump.
        // Do not dereference either discarded frame from this continuation.
        if (s_ehPhase != 10 || s_ehFinallyEntries != 1 || s_ehCatchEntries != 1 || s_rethrowOuterEntries != 1 ||
            s_ehContinuations != 0 || Bits(value) != s_rethrowException || result != s_ehSeed + 1 ||
            !RethrowOwnerIntact() || !CheckEhStack()) RejectEh(26);
        s_ehContinuations = 1; s_ehPhase = 11;
        EhLine(7, Bits(value), value.Payload, result, s_ehContinuations);
    }

    [System.Runtime.RuntimeExport("CLROS_Rethrow_SelectInner")]
    private static uint RethrowSelectInner(ulong exception, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || !CheckEhStack() || !RethrowOwnerIntact() || exception != s_ehReceipt.ObjectReference ||
            !BridgeLayout(captured, transfer, control) || !ControlAdmitted(control) || !RethrowGeometryReady()) return RejectEh(12);
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, void>)&RethrowInner;
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&RethrowThrower;
        if (!CaptureAdmitted((ulong)captured, captured->Rip, captured->Rsp, thrower) ||
            s_ehSelected != 0 || s_ehSelectedFinally != 0 || s_ehTransferAddress != 0 || s_rethrowException != 0 ||
            s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 || s_ehFinallyEntries != 0 || s_ehCatchEntries != 0 ||
            s_rethrowOuterEntries != 0 || s_ehContinuations != 0) return RejectEh(14);
        ulong originalRip = captured->Rip, invalidRip = originalRip ^ 1UL;
        if (CaptureAdmitted((ulong)captured, invalidRip, captured->Rsp, thrower) || captured->Rip != originalRip) return RejectEh(21);
        EhLine(2, originalRip, invalidRip, captured->Rsp, 0);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), RethrowAdmission.ThrowBytes, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo) return RejectEh(15);
            var unwind = BootSequence.RunRethrowUnwind(s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage),
                s_gcHandle, (byte*)storage, EhStorageBytes, (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!unwind.Succeeded || unwind.InEpilog || unwind.Caller.Rip != method + RethrowAdmission.InnerReturn ||
                !RethrowAdmission.InitialFramesMatch(captured->Rsp, unwind.Caller.Rsp, unwind.Caller.Rbp) ||
                !RethrowFunction((byte*)storage, method, 0, RethrowAdmission.Cleanup, 48, 56, 5) ||
                !StackSpan(unwind.Caller.Rsp, 72) || GcReadWord(unwind.Caller.Rbp + 16) != exception ||
                GcReadWord(unwind.Caller.Rbp - 8) != 0) return RejectEh(16);
            ulong table = RethrowInnerClauses();
            if (!RethrowTableRva(table)) return RejectEh(18);
            NativeAotEhMethodBounds bounds = new((uint)(method - s_gcImage.Start), RethrowAdmission.InnerBytes,
                (uint)(table - s_gcImage.Start), 11);
            var caught = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes, bounds, 1);
            var cleanup = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes, bounds, 0);
            if (!RethrowAdmission.AcceptInnerCleanup(cleanup, caught, method, EhBaseType(), (uint)(unwind.Caller.Rip - 1 - method)) ||
                !RethrowFunction((byte*)storage, method, RethrowAdmission.Cleanup, RethrowAdmission.InnerCatch, 40, 40, 4) ||
                !RethrowFunction((byte*)storage, method, RethrowAdmission.InnerCatch, RethrowAdmission.InnerBytes, 40, 40, 4)) return RejectEh(19);
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, caught.TypeAddress);
            if (!match.Succeeded || !match.Matches || !RethrowOwnerIntact()) return RejectEh(20);
            s_ehCaller = unwind.Caller; s_ehSelected = caught.HandlerAddress; s_ehSelectedFinally = cleanup.HandlerAddress;
            s_ehTransferAddress = (ulong)transfer; s_ehEntryControl = RethrowControlCopy(control); s_rethrowException = exception;
            *transfer = new EhTransfer { Funclet = caught.HandlerAddress, Reserved = cleanup.HandlerAddress, Caller = unwind.Caller };
            EhLine(3, captured->Rip, captured->Rsp, unwind.Caller.Rip, unwind.Caller.Rsp);
            EhLine(4, caught.TypeAddress, caught.HandlerAddress, unwind.Caller.Rbp, unwind.ReturnSlot);
            s_ehPhase = 4; return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Rethrow_ValidateFinally")]
    private static uint RethrowValidateFinally(EhTransfer* transfer, EhControl* entry, EhControl* post)
    {
        if (s_ehPhase != 5 || s_ehFinallyEntries != 1 || s_ehCatchEntries != 0 || s_rethrowOuterEntries != 0 ||
            s_ehContinuations != 0 || (ulong)transfer != s_ehTransferAddress || !RethrowInnerTransferIntact() ||
            (ulong)entry != (ulong)transfer + 504 || (ulong)post != (ulong)transfer + 536 ||
            !RethrowControls(entry, post, s_ehEntryControl) || !RethrowStampIntact() || !RethrowOwnerIntact() ||
            !CheckEhStack()) return RejectEh(27);
        EhLine(10, s_ehSelectedFinally, s_ehSelected, s_ehFinallyLocal, GcReadWord(s_ehFinallyLocal));
        s_ehPhase = 6; return 1;
    }

    [System.Runtime.RuntimeExport("CLROS_Rethrow_RejectInnerReturn")]
    private static uint RethrowRejectInnerReturn(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* controls) => RejectEh(28);

    [System.Runtime.RuntimeExport("CLROS_Rethrow_SelectOuter")]
    private static uint RethrowSelectOuter(ulong incoming, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (!RethrowAdmission.ActiveStateMatches(s_ehPhase, s_rethrowException, s_ehReceipt.ObjectReference,
            s_ehFinallyEntries, s_ehCatchEntries, s_rethrowOuterEntries, s_ehContinuations, incoming) ||
            !RethrowGeometryReady() || !RethrowOwnerIntact() || !CheckEhStack() ||
            !BridgeLayout(captured, transfer, control) || !ControlAdmitted(control) ||
            !RethrowInnerTransferIntact() || !RethrowStampIntact() || s_rethrowOuterTransfer != 0 ||
            s_rethrowOuterHandler != 0 || s_rethrowOuter.Rip != 0 || s_rethrowOuter.Rsp != 0) return RejectEh(30);
        ulong inner = (ulong)(delegate* managed<SimpleCatchBase, void>)&RethrowInner;
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&RethrowOuter;
        fixed (X64UnwindContext* original = &s_ehCaller)
            if (!RethrowAdmission.RethrowFrameMatches(s_ehTransferAddress, (ulong)captured, inner, captured, original)) return RejectEh(31);
        EhControl* originalControl = (EhControl*)(s_ehTransferAddress + 504);
        if (!RethrowControls(originalControl, control, s_ehEntryControl)) return RejectEh(32);
        // Local invalid active-state admission is observed before selection;
        // neither the actual capture nor the original active frame is rewritten.
        if (RethrowAdmission.ActiveStateMatches(8, s_rethrowException, s_ehReceipt.ObjectReference,
            s_ehFinallyEntries, s_ehCatchEntries, s_rethrowOuterEntries, s_ehContinuations, incoming)) return RejectEh(33);
        EhLine(14, captured->Rip, captured->Rsp, s_ehTransferAddress, s_rethrowException);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            // Unwind the retained original parent context, not the synthetic
            // callback stack and not a fabricated return address at the rethrow.
            var unwind = BootSequence.RunRethrowUnwind(s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage),
                s_gcHandle, (byte*)storage, EhStorageBytes, (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, s_ehCaller);
            if (!unwind.Succeeded || unwind.InEpilog || unwind.Caller.Rip != method + RethrowAdmission.OuterReturn ||
                !RethrowAdmission.OuterFramesMatch(s_ehCaller.Rsp, unwind.Caller.Rsp, unwind.Caller.Rbp) ||
                !RethrowFunction((byte*)storage, method, 0, RethrowAdmission.OuterCatch, 40, 56, 6) ||
                !StackSpan(unwind.Caller.Rsp, 72) || GcReadWord(unwind.Caller.Rbp + 16) != s_rethrowException ||
                GcReadWord(unwind.Caller.Rbp - 16) != 0) return RejectEh(34);
            ulong table = RethrowOuterClauses();
            if (!RethrowTableRva(table)) return RejectEh(35);
            NativeAotEhMethodBounds bounds = new((uint)(method - s_gcImage.Start), RethrowAdmission.OuterBytes,
                (uint)(table - s_gcImage.Start), 8);
            var caught = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes, bounds, 0);
            if (!RethrowAdmission.AcceptOuterCatch(caught, method, EhBaseType(), (uint)(unwind.Caller.Rip - 1 - method)) ||
                !RethrowFunction((byte*)storage, method, RethrowAdmission.OuterCatch, RethrowAdmission.OuterBytes, 40, 40, 4)) return RejectEh(36);
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), s_ehReceipt, s_rethrowException, caught.TypeAddress);
            if (!match.Succeeded || !match.Matches || !RethrowOwnerIntact()) return RejectEh(37);
            s_rethrowOuter = unwind.Caller; s_rethrowOuterTransfer = (ulong)transfer;
            s_rethrowOuterHandler = caught.HandlerAddress; s_rethrowOuterControl = RethrowControlCopy(control);
            *transfer = new EhTransfer { Funclet = caught.HandlerAddress, Reserved = s_rethrowException, Caller = unwind.Caller };
            EhLine(11, s_ehCaller.Rip, s_ehCaller.Rsp, unwind.Caller.Rip, unwind.Caller.Rsp);
            EhLine(12, caught.TypeAddress, caught.HandlerAddress, unwind.Caller.Rbp, unwind.ReturnSlot);
            s_ehPhase = 8; return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Rethrow_ValidateOuter")]
    private static uint RethrowValidateOuter(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* controls)
    {
        ulong method = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&RethrowOuter;
        if (s_ehPhase != 9 || s_ehFinallyEntries != 1 || s_ehCatchEntries != 1 || s_rethrowOuterEntries != 1 ||
            s_ehContinuations != 0 || (ulong)transfer != s_rethrowOuterTransfer || (ulong)transfer < 272 ||
            !StackSpan((ulong)transfer - 272, 840) || (ulong)post != (ulong)transfer + 256 ||
            (ulong)controls != (ulong)transfer + 504 || !RethrowControls(controls, controls + 1, s_rethrowOuterControl) ||
            transfer->Funclet != s_rethrowOuterHandler || transfer->Reserved != s_rethrowException ||
            !RethrowInnerTransferIntact() || !RethrowStampIntact() || !RethrowOwnerIntact() || !CheckEhStack() ||
            post->Rip != rip || post->Rsp <= (ulong)transfer + 568 || post->Rsp > s_gcStack.InitialRsp ||
            !StackSpan(s_rethrowOuter.Rbp - 16, 8) || GcReadWord(s_rethrowOuter.Rbp - 16) != s_ehSeed + 1) return RejectEh(38);
        fixed (X64UnwindContext* selected = &s_rethrowOuter)
            if (!SingleFinallyAdmission.ContextsMatch(&transfer->Caller, selected, false) ||
                !RethrowAdmission.OuterContinuationMatches(method, s_rethrowException, post, selected)) return RejectEh(39);
        EhLine(6, rip, post->Rsp, post->Rbp, s_rethrowOuterEntries); s_ehPhase = 10; return 1;
    }

    private static bool RethrowOwnerIntact() => EhObjectIntact() && ReadGcRoot().Succeeded && CollectorReceiptValid(5);
    private static bool RethrowTableRva(ulong table) => table > s_gcImage.Start && table - s_gcImage.Start <= 0x7FFFFFFF;
    private static bool RethrowFunction(byte* storage, ulong method, uint begin, uint end, uint allocation, uint stack, byte prolog)
    {
        var row = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(storage, EhStorageBytes, method + begin);
        return row.Succeeded && row.ChainDepth == 0 && row.FrameRegister == 0 && row.FrameOffset == 0 &&
            row.FixedAllocationBytes == allocation && row.StackBytes == stack && row.PrologBytes == prolog &&
            row.BeginRva == method - s_gcImage.Start + begin && row.EndRva == method - s_gcImage.Start + end;
    }
    private static bool RethrowInnerTransferIntact()
    {
        if (s_ehTransferAddress < 272 || (s_ehTransferAddress & 15) != 0 || !StackSpan(s_ehTransferAddress - 272, 840)) return false;
        EhTransfer* transfer = (EhTransfer*)s_ehTransferAddress;
        fixed (X64UnwindContext* saved = &s_ehCaller)
            return transfer->Funclet == s_ehSelected && transfer->Reserved == s_ehSelectedFinally &&
                SingleFinallyAdmission.ContextsMatch(&transfer->Caller, saved, false);
    }
    private static bool RethrowStampIntact() => s_ehFinallyLocal == s_ehCaller.Rbp - 8 &&
        StackSpan(s_ehFinallyLocal, 8) && GcReadWord(s_ehFinallyLocal) == s_ehSeed + 2 &&
        StackSpan(s_ehCaller.Rbp + 16, 8) && GcReadWord(s_ehCaller.Rbp + 16) == s_rethrowException;
    private static EhControl RethrowControlCopy(EhControl* value) =>
        new() { Flags = value->Flags, Cr4 = value->Cr4, Mxcsr = value->Mxcsr, X87 = value->X87 };
    private static bool RethrowControls(EhControl* entry, EhControl* post, EhControl saved) =>
        SingleFinallyAdmission.ControlsMatch(new(entry->Flags, entry->Cr4, entry->Mxcsr, entry->X87),
            new(post->Flags, post->Cr4, post->Mxcsr, post->X87), new(saved.Flags, saved.Cr4, saved.Mxcsr, saved.X87));
}
#endif
#endif
