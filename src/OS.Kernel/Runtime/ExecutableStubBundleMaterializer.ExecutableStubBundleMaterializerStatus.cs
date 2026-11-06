using OS.Kernel.Diagnostics;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public enum ExecutableStubBundleMaterializerStatus
{
    Success = 0,
    InvalidBundlePlan = 1,
    InvalidMemoryPlan = 2,
    InconsistentMemoryPlan = 3,
    DestinationTooSmall = 4,
    MissingSlot = 5,
    SourceTooSmall = 6,
    SlotOutOfBounds = 7
}
