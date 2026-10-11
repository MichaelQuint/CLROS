#if NOSTDLIB_EFI && M04_UNHANDLED_EXCEPTION_PROBE
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{
    // Only the profile's original caller-owned allocation/root and retained
    // module may reach this export. This does not install general cast services
    // or extend any lifetime; dispatch contains no allocation or collection.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool MatchRetainedExceptionType(ulong targetType, object value)
    {
        ulong reference = Bits(value);
        if (reference == 0) return false;
        if ((s_ehPhase != 2 && s_ehPhase != 3) ||
            !UnhandledRootRetained(reference) || !EhObjectIntact())
        {
            RejectUnhandled(100);
            return false;
        }

        fixed (GcTypeStorage* type = &s_gcType)
        fixed (GcCollectingStorage* owner = &s_gcCollecting)
        {
            var match = ExceptionTypeMatch.Match(NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
                s_gcCapture, s_gcMapping, (byte*)type, (ulong)sizeof(GcTypeStorage), s_gcHandle,
                s_access, &owner->State, (ulong)sizeof(GcCollectingStorage),
                s_ehReceipt, reference, targetType);
            if (!match.Succeeded || !UnhandledRootRetained(reference) || !EhObjectIntact())
            {
                RejectUnhandled(101 + (ulong)match.Status);
                return false;
            }
            return match.Matches;
        }
    }
}
#endif
