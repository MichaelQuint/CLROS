using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;

public unsafe struct FirstExitTransitionState
{
    internal FirstExitPhase PhaseValue;
    internal FirmwareServicesMode ModeValue;
    internal BoundedExitRetryState Retry;
    internal BoundedExitRetryResult AttemptValue;
    public FirstExitPhase Phase => PhaseValue;
    public FirmwareServicesMode Mode => ModeValue;
    public BoundedExitRetryResult Attempt => AttemptValue;
    public bool RetainedFirmwareCallbacksCleared => Retry.First.Exit.Function == null && Retry.First.Exit.Handle == null && Retry.Retry.Exit.Function == null && Retry.Retry.Exit.Handle == null;
}
