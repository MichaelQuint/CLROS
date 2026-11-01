using System;

namespace OS.Kernel.Runtime;
public enum BoundedExceptionFrameSearchStatus : byte
{
    Uninitialized,
    Success,
    Empty,
    CapacityExceeded,
    OutputTooSmall,
    InvalidFrame,
    InvalidSlice,
    InvalidClauseRange,
    DispatchRejected,
    DuplicateCleanup,
    NoMatchingCatch
}
