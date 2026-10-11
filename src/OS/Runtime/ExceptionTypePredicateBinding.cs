#if NOSTDLIB_EFI && (M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE)
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{
    // Only the profile's original caller-owned allocation/root and retained
    // module may reach this export. This does not install general cast services
    // or extend any lifetime; dispatch contains no allocation or collection.
#if M04_CONSTRUCTOR_FAILURE_PROBE
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool MatchRetainedExceptionType(ulong targetType, object value) =>
        MatchConstructorFailureExceptionType(targetType, value);
#else
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool MatchRetainedExceptionType(ulong targetType, object value)
    {
        ulong reference = Bits(value);
        if (reference == 0) return false;
        if ((s_ehPhase != 2 && s_ehPhase != 3) ||
#if M04_FRAME_DISPATCH_PROBE
            !FrameRootRetained(reference) || !EhObjectIntact())
#else
            !UnhandledRootRetained(reference) || !EhObjectIntact())
#endif
        {
#if M04_FRAME_DISPATCH_PROBE
            RejectFrame(100);
#else
            RejectUnhandled(100);
#endif
            return false;
        }

        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage),
                s_ehReceipt, reference, targetType);
#if M04_FRAME_DISPATCH_PROBE
            if (!match.Succeeded || !FrameRootRetained(reference) || !EhObjectIntact())
#else
            if (!match.Succeeded || !UnhandledRootRetained(reference) || !EhObjectIntact())
#endif
            {
#if M04_FRAME_DISPATCH_PROBE
                RejectFrame(101 + (ulong)match.Status);
#else
                RejectUnhandled(101 + (ulong)match.Status);
#endif
                return false;
            }
            return match.Matches;
        }
    }
#endif
}
#endif
