using System;
using OS.Boot;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionCallerWindowStatus : byte
{
    Uninitialized,
    Success,
    CapacityExceeded,
    OutputTooSmall,
    StorageOverlap,
    InvalidFrame,
    ModuleRejected,
    SnapshotRejected,
    CatalogRejected,
    ClauseRejected,
    UnwindRejected
}
