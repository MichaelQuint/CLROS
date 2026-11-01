using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
public enum NativeAotHeapBootstrapPlanStatus
{
    Success = 0,
    AllocatorStateNotInitialized = 1,
    InvalidPageSize = 2,
    NoFreePages = 3,
    AllocatorFreeByteCountOverflow = 4,
    AllocatorFreeByteCountMismatch = 5,
    RequestedHeapPagesZero = 6,
    RequestedHeapPagesExceedFreePages = 7,
    InvalidObjectAlignment = 8,
    ObjectAlignmentTooSmall = 9,
    ObjectAlignmentExceedsPageSize = 10
}
