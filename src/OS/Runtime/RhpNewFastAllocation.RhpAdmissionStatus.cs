using OS.Kernel.Runtime;

namespace OS.Runtime;
// Issue-specific POD core. Host callers supply readable buffers, never CLR object bits.
internal enum RhpAdmissionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidStorage = 2,
    StorageOverlap = 3,
    AlreadyPrepared = 4,
    NotPrepared = 5,
    Reentry = 6,
    UnknownToken = 7,
    InvalidImage = 8,
    InvalidPrefixExtent = 9,
    PrefixChanged = 10,
    AllocationRejected = 11,
    ObservationRejected = 12,
    PriorObjectChanged = 13,
    RetainedRejected = 14,
    InvalidFixture = 15
}
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE

#pragma warning disable 649

#pragma warning restore 649
#endif
