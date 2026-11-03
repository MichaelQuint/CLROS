#if !NOSTDLIB_EFI
using OS.Kernel.Runtime;

#endif
namespace OS.Kernel.Diagnostics;

public enum CheckedPortIoFunctionPointerBindingStatus
{
    Ready = 0,
    BindingPlanNotReady = 1,
    InvalidConfiguredBasePort = 2,
    ConfiguredPortRangeOverflow = 3,
    InvalidPayloadRange = 4,
    StubBytesNotReady = 5,
    Out8TargetMismatch = 6,
    In8TargetMismatch = 7,
    Out8TargetOutsidePayload = 8,
    In8TargetOutsidePayload = 9,
    CallableRangesOverlap = 10
}
