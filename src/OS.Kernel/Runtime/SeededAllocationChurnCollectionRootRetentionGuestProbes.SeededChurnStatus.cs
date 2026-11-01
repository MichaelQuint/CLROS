using OS.Std;

namespace OS.Kernel.Runtime;
public enum SeededChurnStatus : byte
{
    Uninitialized,
    Success,
    InvalidRounds,
    TypeRejected,
    AllocationRejected,
    CollectionRejected,
    IdentityMismatch,
    RetentionMismatch,
    ReclamationMismatch
}
