using OS.Manifest;

namespace OS.Kernel.Runtime;
public enum NativeAppExecutableMemoryPlanStatus
{
    Success = 0,
    MappingPlanFailed = 1,
    InvalidPayloadMappingPlan = 2,
    ZeroExecutableContent = 3,
    InvalidPageSize = 4,
    InvalidAlignment = 5,
    AlignmentGreaterThanPageSize = 6,
    UnalignedExecutableOffset = 7,
    AccidentalWritableExecutable = 8,
    UnsafeProtectionState = 9,
    InvalidEntryOffset = 10,
    RangeOverflow = 11
}
