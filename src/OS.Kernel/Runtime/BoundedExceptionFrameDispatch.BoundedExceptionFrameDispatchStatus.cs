using System;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionFrameDispatchStatus : byte
{
    Uninitialized,
    Success,
    SearchRejected,
    UnsupportedPlan
}
