using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

namespace OS.Boot;

public enum OwnedAllocatorRoundTripStatus : uint
{
    Empty,
    AdmissionFailed,
    AlreadyConsumed,
    DiagnosticsFailed,
    OwnerRejected,
    AllocationFailed,
    PageRejected,
    ReadbackFailed,
    FreeFailed,
    AccountingFailed,
    Completed
}
