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
            inner->Rbp != originalTransfer + 672)
            return false;
        // The measured inner catch changes no nonvolatile GPR or XMM register.
        // Only RIP/SP differ from the retained original handling context.
        for (int word = 2; word < sizeof(X64UnwindContext) / 8; word++)
            if (((ulong*)captured)[word] != ((ulong*)inner)[word])
                return false;
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
            actual->Rbx != exception || sizeof(X64UnwindContext) != 240)
            return false;
        // This compiler catch changes only RIP and RBX. Preserve its real
        // result rather than replacing it with the pre-catch nonvolatiles.
        for (int word = 1; word < sizeof(X64UnwindContext) / 8; word++)
            if (word != 2 && ((ulong*)actual)[word] != ((ulong*)selected)[word])
                return false;
        return true;
    }

    private static bool MethodFits(ulong method, uint bytes) => method != 0 && method <= ulong.MaxValue - bytes;
}
#endif
