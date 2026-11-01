#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe partial struct LinuxThreadServices
{
    /// <summary>Copied frozen phase partition, without callback, lifetime or
    /// dispatch-return authority. A default value represents no observation.</summary>

    public readonly struct DiskDispatchTotals
    {
        public readonly ulong PhaseNanoseconds, PollNanoseconds, DispatchNanoseconds, NestedPollNanoseconds;
        public readonly ulong CompletedEnvelopes, ClippedEnvelopes;
        public readonly bool PhysicalDispatchActiveAtFreeze;
        public ulong DispatchNonPollNanoseconds => DispatchNanoseconds - NestedPollNanoseconds;
        public ulong SupervisorNonPollNanoseconds => PhaseNanoseconds - PollNanoseconds - DispatchNonPollNanoseconds;

        internal DiskDispatchTotals(ulong phase, ulong poll, ulong dispatch, ulong nested, ulong completed, ulong clipped, bool physicalActive)
        {
            PhaseNanoseconds = phase;
            PollNanoseconds = poll;
            DispatchNanoseconds = dispatch;
            NestedPollNanoseconds = nested;
            CompletedEnvelopes = completed;
            ClippedEnvelopes = clipped;
            PhysicalDispatchActiveAtFreeze = physicalActive;
        }
    }
}
#endif
