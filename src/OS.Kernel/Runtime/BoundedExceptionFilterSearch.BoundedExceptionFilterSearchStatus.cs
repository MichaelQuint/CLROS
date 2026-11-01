using System;

namespace OS.Kernel.Runtime;
public enum BoundedExceptionFilterSearchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    FilterRequired = 2,
    Empty = 3,
    CapacityExceeded = 4,
    InvalidClause = 5,
    UnsupportedClause = 6,
    InvalidTopology = 7,
    InvalidReceipt = 8,
    NoMatch = 9
}
