using OS.Kernel.Memory;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum ManagedHeapPageRangeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    StorageOverlap = 3,
    AlreadyInitialized = 4,
    InvalidReservationTable = 5,
    InvalidParent = 6,
    InvalidAllocator = 7,
    RetainedRangeIsFree = 8,
    AllocationRejected = 9,
    AllocationResultRejected = 10,
    InitializationReadbackFailed = 11,
    IdentityMismatch = 12,
    NotReady = 13
}
