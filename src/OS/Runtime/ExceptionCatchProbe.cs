#if NOSTDLIB_EFI && M04_SIMPLE_THROW_CATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal class SimpleCatchBase : System.Exception
{
    internal readonly ulong Payload;
    internal SimpleCatchBase(ulong payload) { Payload = payload; }
}

internal sealed class SimpleCatchDerived : SimpleCatchBase
{
    internal SimpleCatchDerived(ulong payload) : base(payload) { }
}

internal static unsafe partial class RhpNewFastAllocation
{
    // Measured from publish-2's original ILC/COFF contributions and complete
    // linked bytes. Enabled development publication now requires independent
    // reachable-path review before guest execution. No inferred tail bounds.
    private const bool EhTransferAdmitted = true;
    private const uint EhThrowCodeBytes = 10, EhCatchCodeBytes = 89;
    private const uint EhTableBytes = 8;
    private const uint EhThrowReturnOffset = 9, EhCatchReturnOffset = 26;
    private const uint EhContinuationOffset = 27, EhHandlerOffset = 51;
    private const uint EhCatchFrameBytes = 40;
    private const ulong EhStorageBytes = 65536;
    private struct EhStorage { internal fixed ulong Words[8192]; }
    // These POD fields are written by the generated native bridge.
#pragma warning disable CS0649
    private struct EhTransfer
    {
        internal ulong Funclet, Reserved;
        internal X64UnwindContext Caller;
    }
    private struct EhControl
    {
        internal ulong Flags, Cr4;
        internal uint Mxcsr;
        internal ushort X87, Reserved;
        internal ulong Reserved2;
    }
#pragma warning restore CS0649
    private static EhStorage s_ehStorage;
    private static RhpNewFastResult s_ehReceipt;
    private static X64UnwindContext s_ehCaller;
    private static ulong s_ehPhase, s_ehSeed, s_ehCatchEntries, s_ehContinuations;
    private static ulong s_ehSelected, s_ehTransferAddress, s_ehChecks, s_ehMinimum;

    [System.Runtime.RuntimeImport("*", "CLROS_Eh_GetClauseTable")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    private static extern ulong EhClauseTable();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunSimpleThrowCatch()
    {
        if (!BootSequence.IsSimpleThrowCatchEnabled() || s_ehPhase != 0 ||
            s_gcPhase != 5 || s_collectorPhase != 7 || s_gcStack.Checks != 10 ||
            s_control.ConstructorCount != 7 || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5))
            RejectEh(1);
        EhText(0); s_ehPhase = 1;
        s_ehSeed = (s_gcHandle ^ (ulong)s_access.Backing) & 0xFFFF;
        if (!CheckEhStack()) RejectEh(2);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var bound = X64PERuntimeFunctionUnwindMetadataHostileInput.Bind(s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes);
            if (!bound.Succeeded || bound.RequiredStorage >= EhStorageBytes ||
                sizeof(X64UnwindContext) != 240 || sizeof(EhTransfer) != 256 || sizeof(EhControl) != 32)
                RejectEh(3);
            EhLine(0, s_gcHandle, (ulong)storage, bound.RequiredStorage, bound.FunctionCount);
        }
        SimpleCatchDerived exception = NewCatchException(s_ehSeed);
        if (s_ehPhase != 2 || Bits(exception) != s_ehReceipt.ObjectReference ||
            exception.Payload != s_ehSeed || !EhObjectIntact()) RejectEh(4);
        RhpNewFastResult root = s_ehReceipt;
        // This is the real original receipt passed into collection, not a
        // managed local pretending to be an automatically scanned root.
        BoundedCollectionResult collected = CollectGcGraph(&root, 1);
        if (!collected.Succeeded || collected.LiveObjects != 3 || collected.ReclaimedObjects != 0 ||
            !EhObjectIntact() || !ReadGcRoot().Succeeded || !CollectorReceiptValid(5)) RejectEh(5);
        EhLine(1, s_ehReceipt.ObjectReference, s_ehReceipt.MethodTableToken,
            s_ehReceipt.AllocationCount, collected.LiveObjects);
        s_ehPhase = 3;
        ulong result = Catcher(exception);
        if (s_ehPhase != 6 || s_ehCatchEntries != 1 || s_ehContinuations != 1 ||
            result != s_ehSeed + 1 || !EhObjectIntact() || !CheckEhStack()) RejectEh(6);
        EhLine(8, s_ehChecks, s_ehMinimum, GcReadWord(s_gcStack.Start), GcReadWord(s_gcStack.Start + 8));
        // No collection occurs between the explicit rooted collection and the
        // actual compiled continuation. Root lifetime ends only here.
        s_ehPhase = 7; s_ehReceipt = default;
        EhText(1); EhText(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SimpleCatchDerived NewCatchException(ulong payload) => new(payload);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Thrower(System.Exception value) { throw value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong Catcher(SimpleCatchBase value)
    {
        ulong result = 0;
        try { Thrower(value); }
        catch (SimpleCatchBase caught)
        {
            CatchEntered(caught);
            result = caught.Payload + 1;
        }
        CatchContinued(value, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CatchEntered(SimpleCatchBase value)
    {
        if (s_ehPhase != 4 || s_ehCatchEntries != 0 || Bits(value) != s_ehReceipt.ObjectReference ||
            value.Payload != s_ehSeed || !EhObjectIntact() || !CheckEhStack()) RejectEh(7);
        s_ehCatchEntries++;
        EhLine(5, Bits(value), value.Payload, s_ehSelected, s_ehCatchEntries);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CatchContinued(SimpleCatchBase value, ulong result)
    {
        if (s_ehPhase != 5 || s_ehCatchEntries != 1 || s_ehContinuations != 0 ||
            Bits(value) != s_ehReceipt.ObjectReference || result != s_ehSeed + 1 ||
            !EhObjectIntact() || !CheckEhStack()) RejectEh(8);
        s_ehContinuations++; s_ehPhase = 6;
        EhLine(7, Bits(value), value.Payload, result, s_ehContinuations);
    }

    private static ulong AllocateCatchException(ulong token)
    {
        if (s_ehPhase != 1 || s_ehReceipt.ObjectReference != 0 || token != EhDerivedType() ||
            !ReadGcRoot().Succeeded) RejectEh(9);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var map = BoundedNonMovingCollectorExplicitRootEnumeration.InspectType(
                NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, s_gcCapture, s_gcMapping,
                (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle, s_gcImage, token);
            if (!map.Succeeded || map.SeriesCount != 0 || map.Prefix.BaseSize != 24) RejectEh(10);
            var allocated = GuestTestRhpNewFast.TryAllocate(s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                map.Prefix, token);
            if (!allocated.AllocationSucceeded || allocated.AllocationCount != 9 ||
                allocated.BytesConsumed != 24 || GcReadWord(allocated.ObjectReference + 8) != 0)
                RejectEh(11);
            s_ehReceipt = allocated; s_ehPhase = 2; return allocated.ObjectReference;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EhObjectIntact()
    {
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
            return GuestTestRhpNewFast.TryValidateRetainedAllocation(s_access, &owner->State,
                (ulong)sizeof(GcCollectingStorage), s_ehReceipt) == RhpNewFastStatus.Success &&
                s_ehReceipt.MethodTableToken == EhDerivedType() &&
                GcReadWord(s_ehReceipt.ObjectReference + 8) == s_ehSeed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static ulong EhDerivedType() => (ulong)Internal.Runtime.MethodTable.Of<SimpleCatchDerived>();
    [MethodImpl(MethodImplOptions.NoInlining)] private static ulong EhBaseType() => (ulong)Internal.Runtime.MethodTable.Of<SimpleCatchBase>();
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool EhGeometryReady() => EhTransferAdmitted;

    [System.Runtime.RuntimeExport("CLROS_Eh_Select")]
    private static uint SelectCatch(ulong exception, X64UnwindContext* captured, EhTransfer* transfer, EhControl* control)
    {
        if (s_ehPhase != 3 || !CheckEhStack() || !EhObjectIntact() || exception != s_ehReceipt.ObjectReference ||
            !BridgeLayout(captured, transfer, control) || !ControlAdmitted(control)) return RejectEh(12);
        if (!EhGeometryReady()) return RejectEh(13); // retain the bounded admission switch
        ulong thrower = (ulong)(delegate* managed<System.Exception, void>)&Thrower;
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&Catcher;
        ulong originalRip = captured->Rip, originalRsp = captured->Rsp;
        if (!CaptureAdmitted((ulong)captured, originalRip, originalRsp, thrower)) return RejectEh(14);
        // Exercise the same admission predicate with local scalar data only.
        // The live bridge capture, transfer buffer and CPU state are untouched;
        // this is admission-rejection evidence, not a terminal-halt control.
        ulong invalidRip = originalRip ^ 1UL;
        ulong originalType = s_ehReceipt.MethodTableToken;
        bool invalidAdmitted = CaptureAdmitted((ulong)captured, invalidRip, originalRsp, thrower);
        if (invalidAdmitted || captured->Rip != originalRip || captured->Rsp != originalRsp ||
            s_ehPhase != 3 || s_ehSelected != 0 || s_ehTransferAddress != 0 ||
            s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 || s_ehCatchEntries != 0 || s_ehContinuations != 0 ||
            s_ehReceipt.ObjectReference != exception || s_ehReceipt.MethodTableToken != originalType ||
            s_ehReceipt.AllocationCount != 9 || !EhObjectIntact()) return RejectEh(21);
        EhLine(2, originalRip, invalidRip, originalRsp, invalidAdmitted ? 1UL : 0UL);
        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        fixed (EhStorage* storage = &s_ehStorage)
        {
            var noEh = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(thrower - s_gcImage.Start), EhThrowCodeBytes, 0, 0), 0);
            if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo) return RejectEh(15);
            var unwound = BootSequence.RunSimpleThrowCatchUnwind(s_gcCapture, s_gcMapping, (byte*)type,
                (ulong)sizeof(GcTypeStorage), s_gcHandle, (byte*)storage, EhStorageBytes,
                (byte*)(s_gcStack.Start + 16), s_gcStack.End - s_gcStack.Start - 16, *captured);
            if (!unwound.Succeeded || unwound.InEpilog || unwound.Caller.Rip != catcher + EhCatchReturnOffset ||
                unwound.Caller.Rsp <= captured->Rsp) return RejectEh(16);
            var metadata = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup((byte*)storage, EhStorageBytes, unwound.Caller.Rip);
            // NativeAOT sets RBP in code without an UWOP_SET_FPREG: preserve
            // actual xdata0/0 and bind the independently inspected body shape.
            if (!metadata.Succeeded || metadata.ChainDepth != 0 || metadata.FrameRegister != 0 ||
                metadata.FrameOffset != 0 || metadata.FixedAllocationBytes != EhCatchFrameBytes ||
                metadata.StackBytes != 56 || metadata.PrologBytes != 6 ||
                metadata.BeginRva != catcher - s_gcImage.Start || metadata.EndRva != catcher - s_gcImage.Start + EhHandlerOffset ||
                unwound.Caller.Rip - catcher < 11 || unwound.Caller.Rbp != unwound.Caller.Rsp + 48 ||
                !StackSpan(unwound.Caller.Rsp, 72) ||
                GcReadWord(unwound.Caller.Rbp + 16) != exception || GcReadWord(unwound.Caller.Rbp - 16) != 0)
                return RejectEh(17);
            ulong table = EhClauseTable();
            if (table < s_gcImage.Start || table - s_gcImage.Start > 0x7FFFFFFF) return RejectEh(18);
            var clause = NativeAotEhClauseMetadata.ReadClause(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                (byte*)storage, EhStorageBytes, new((uint)(catcher - s_gcImage.Start), EhCatchCodeBytes, (uint)(table - s_gcImage.Start), EhTableBytes), 0);
            if (!clause.Succeeded || clause.Count != 1 || clause.IsTryGroupBoundary ||
                clause.Kind != NativeAotEhClauseKind.Typed || clause.FilterAddress != 0 ||
                clause.TypeAddress != EhBaseType() || clause.HandlerAddress != catcher + EhHandlerOffset ||
                !clause.ContainsOffset((uint)(unwound.Caller.Rip - 1 - catcher))) return RejectEh(18);
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage), s_ehReceipt, exception, clause.TypeAddress);
            if (!match.Succeeded || !match.Matches || !EhObjectIntact()) return RejectEh(19);
            s_ehCaller = unwound.Caller; s_ehSelected = clause.HandlerAddress; s_ehTransferAddress = (ulong)transfer;
            *transfer = new EhTransfer { Funclet = clause.HandlerAddress, Caller = unwound.Caller };
            EhLine(3, captured->Rip, captured->Rsp, unwound.Caller.Rip, unwound.Caller.Rsp);
            EhLine(4, clause.TypeAddress, clause.HandlerAddress, unwound.Caller.Rbp, unwound.ReturnSlot);
            s_ehPhase = 4; return 1;
        }
    }

    [System.Runtime.RuntimeExport("CLROS_Eh_ValidateContinuation")]
    private static uint ValidateCatchContinuation(ulong rip, X64UnwindContext* post, EhTransfer* transfer, EhControl* controls)
    {
        ulong catcher = (ulong)(delegate* managed<SimpleCatchBase, ulong>)&Catcher;
        if (s_ehPhase != 4 || s_ehCatchEntries != 1 || !CheckEhStack() || !EhObjectIntact() ||
            (ulong)transfer != s_ehTransferAddress || (ulong)post != (ulong)transfer + 256 ||
            (ulong)controls != (ulong)transfer + 504 || !StackSpan((ulong)transfer - 272, 840) ||
            !ControlAdmitted(controls) || !ControlAdmitted(controls + 1) ||
            controls->Cr4 != controls[1].Cr4 || controls->X87 != controls[1].X87 ||
            (controls->Mxcsr & 0xFFC0) != (controls[1].Mxcsr & 0xFFC0) ||
            rip != catcher + EhContinuationOffset || post->Rip != rip || post->Rsp != s_ehCaller.Rsp ||
            post->Rbp != s_ehCaller.Rbp ||
            transfer->Funclet != s_ehSelected || transfer->Caller.Rsp != s_ehCaller.Rsp ||
            post->Rsp <= (ulong)post + 240 || post->Rsp > s_gcStack.InitialRsp)
            return RejectEh(20);
        EhLine(6, rip, post->Rsp, post->Rbp, s_ehCatchEntries); s_ehPhase = 5; return 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CaptureAdmitted(ulong capturedAddress, ulong rip, ulong rsp, ulong thrower) =>
        rip == thrower + EhThrowReturnOffset && rsp == capturedAddress + 816 &&
        rip != 0 && rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;

    private static bool BridgeLayout(X64UnwindContext* input, EhTransfer* output, EhControl* controls) =>
        (ulong)input >= 32 && StackSpan((ulong)input - 32, 848) &&
        ((ulong)input & 15) == 0 && (ulong)output == (ulong)input + 240 &&
        (ulong)controls == (ulong)input + 744;
    private static bool ControlAdmitted(EhControl* control) =>
        (control->Flags & 0x400) == 0 && (control->Cr4 & (1UL << 23)) == 0 &&
        (control->Mxcsr & 0xFFFF0000) == 0;
    private static bool StackSpan(ulong at, ulong bytes) => at >= s_gcStack.Start + 16400 &&
        at <= s_gcStack.InitialRsp && bytes <= s_gcStack.InitialRsp - at;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckEhStack()
    {
        ulong witness = 0, address = (ulong)&witness;
        if (!GcStackContext(address, out ulong authority, out ulong start, out ulong end, out ulong rsp) ||
            authority != s_gcStack.Authority || start != s_gcStack.Start || end != s_gcStack.End ||
            rsp != s_gcStack.InitialRsp || GcReadWord(start) != GcStackWord0 || GcReadWord(start + 8) != GcStackWord1)
            return false;
        if (s_ehMinimum == 0 || address < s_ehMinimum) s_ehMinimum = address;
        s_ehChecks++; return true;
    }
    private static uint RejectEh(ulong reason)
    {
        EhText(3); Decimal(reason); End();
        while (true) System.RuntimeFailFast.Halt();
    }
    private static void EhLine(ulong stage, ulong a, ulong b, ulong c, ulong d)
    {
        EhText(4); Decimal(stage); Write(32); Hex(a); Write(32); Hex(b); Write(32); Hex(c); Write(32); Hex(d); End();
    }
    private static void EhText(uint kind)
    {
        // Constant string access is already supported by the admitted shell.
        string text = kind == 0 ? "TEST_START m04.021.simple-catch\r\n" :
            kind == 1 ? "TEST_PASS m04.021.simple-catch\r\n" :
            kind == 2 ? "CLROS_M04_SIMPLE_CATCH_READY\r\n" :
            kind == 3 ? "TEST_FAIL m04.021.simple-catch " : "M04_CATCH ";
        for (int i = 0; i < text.Length; i++) Write((byte)text[i]);
    }
}
#endif
