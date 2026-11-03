using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public enum RhpNewFastStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    StorageOverlap = 3,
    AlreadyInitialized = 4,
    NotInitialized = 5,
    InvalidState = 6,
    HeapAuthorityRejected = 7,
    HeapBindingMismatch = 8,
    InitialPageNotPristine = 9,
    UnsupportedProfile = 10,
    InvalidMethodTableToken = 11,
    InvalidMethodTableFlags = 12,
    UnsupportedFixedType = 13,
    InvalidBaseSize = 14,
    Exhausted = 15,
    UnsupportedArrayType = 16,
    InvalidComponentSize = 17,
    InvalidLength = 18,
    UnsupportedStringType = 19,
    UnsupportedStaticType = 20,
    InvalidReceipt = 21,
    ReceiptContentsChanged = 22,
    CollectionNotEnabled = 23,
    InvalidAllocationLedger = 24,
    AllocationIdentityExhausted = 25
}
