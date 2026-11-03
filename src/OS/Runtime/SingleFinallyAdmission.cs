#if NOSTDLIB_EFI && (M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE)
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;


#pragma warning disable CS0649

#pragma warning restore CS0649






#endif
#if SINGLE_FINALLY_CORE_TEST
using OS.Kernel.Runtime;
namespace OS.Runtime;
#endif
#if (NOSTDLIB_EFI && (M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE)) || SINGLE_FINALLY_CORE_TEST
// Pure admission predicates shared by the real EFI callbacks and an isolated
// host fixture compiling this same source. They do not authenticate a module,
// dereference guest addresses, search types or execute/restore a context.
internal static unsafe partial class SingleFinallyAdmission
{
    internal const uint CatchCodeBytes = 136, CatchReturnOffset = 32, CleanupOffset = 71, HandlerOffset = 94;
    // Actual Thrower is SUB RSP,40; CALL RhpThrowEx; INT3. Unwind consumes
    // its 40-byte allocation and the caller return slot before restoring Catcher.
    internal static bool ParentFrameMatches(ulong capturedRsp, ulong callerRsp, ulong callerRbp) =>
        capturedRsp != 0 && capturedRsp <= ulong.MaxValue - 112 &&
        callerRsp == capturedRsp + 48 && callerRbp == callerRsp + 64;

    internal static bool AcceptCatch(NativeAotEhClauseResult caught, ulong catcher, ulong baseType, uint offset) =>
        catcher != 0 && catcher <= ulong.MaxValue - CatchCodeBytes && baseType != 0 && offset == CatchReturnOffset - 1 &&
        caught.Succeeded && caught.Index == 1 && caught.Count == 2 && !caught.IsTryGroupBoundary &&
        caught.Kind == NativeAotEhClauseKind.Typed && caught.FilterAddress == 0 &&
        caught.TryStartOffset == 27 && caught.TryEndOffset == 47 && caught.TypeAddress == baseType &&
        caught.HandlerAddress == catcher + HandlerOffset && caught.ContainsOffset(offset);
    internal static bool AcceptCleanup(NativeAotEhClauseResult cleanup, NativeAotEhClauseResult caught,
        ulong catcher, ulong baseType, uint offset) =>
        AcceptCatch(caught, catcher, baseType, offset) && cleanup.Succeeded && cleanup.Index == 0 && cleanup.Count == 2 &&
        !cleanup.IsTryGroupBoundary && cleanup.Kind == NativeAotEhClauseKind.FaultOrFinally &&
        cleanup.FilterAddress == 0 && cleanup.TypeAddress == 0 && cleanup.TryStartOffset == caught.TryStartOffset &&
        cleanup.TryEndOffset == 33 && cleanup.TryEndOffset < caught.TryEndOffset && cleanup.ContainsOffset(offset) &&
        cleanup.HandlerAddress == catcher + CleanupOffset;
    internal static bool ContextsMatch(X64UnwindContext* actual, X64UnwindContext* saved, bool afterCatch)
    {
        if (actual == null || saved == null || sizeof(X64UnwindContext) != 240)
            return false;
        // The measured catch changes only RIP and RBX. The caller separately
        // checks their exact resulting values; every other word must survive.
        for (int i = 0; i < sizeof(X64UnwindContext) / 8; i++)
            if (!(afterCatch && (i == 0 || i == 2)) && ((ulong*)actual)[i] != ((ulong*)saved)[i])
                return false;
        return true;
    }
    internal static bool ControlsMatch(Control entry, Control post, Control saved) =>
        (entry.Flags & 0x400) == 0 && (post.Flags & 0x400) == 0 &&
        (entry.Cr4 & (1UL << 23)) == 0 && (post.Cr4 & (1UL << 23)) == 0 &&
        (entry.Mxcsr & 0xFFFF0000) == 0 && (post.Mxcsr & 0xFFFF0000) == 0 &&
        entry.Flags == saved.Flags && entry.Cr4 == saved.Cr4 && entry.X87 == saved.X87 && entry.Mxcsr == saved.Mxcsr &&
        entry.Cr4 == post.Cr4 && entry.X87 == post.X87 && (entry.Mxcsr & 0xFFC0) == (post.Mxcsr & 0xFFC0);
}
#endif
