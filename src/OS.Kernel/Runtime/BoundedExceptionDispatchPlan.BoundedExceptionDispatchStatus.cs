using System;

namespace OS.Kernel.Runtime;
public enum BoundedExceptionDispatchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    Empty = 2,
    CapacityExceeded = 3,
    InvalidClause = 4,
    UnsupportedClause = 5,
    InvalidTypeMatch = 6,
    InvalidNesting = 7,
    NoMatchingCatch = 8,
    InvalidStart = 9
}
