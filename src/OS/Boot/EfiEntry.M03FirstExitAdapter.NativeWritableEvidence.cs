#if M16_027_PROBE
using B = OS.Storage.WritableBlockSession;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {


        // Retained ordinary RAM, separate from B's exclusive observation slot.
        // Root samples before and immediately after EVERY possible B-touching
        // dispatch/commit/complete/readiness/cancel/exit/detach/end call. No
        // aggregation across two publications and no callback inside B.
        private struct NativeWritableEvidence
        {
            internal B.Observation Last, Current;
            internal ulong Rows, LastClock;
            internal B.Status LastBlockFailure;
            internal ulong StateSubmitted, StateCompleted, StateSubmittedBytes, StateCompletedBytes, StateFlushedBytes;
            internal bool Failed, HeaderCopied, HadState, LastBound, LastRunning, LastRetired, LastDirty, LastRetry;
#if M16_027_TIMING_PROBE
            internal byte TimingPhase; //0 unarmed,1 first source5 BEGIN,2 measuring,3 report emitted.
            internal OS.Kernel.Diagnostics.WritableStorageTiming.State Timing;
#endif
        }


    }
}
#endif
