using System;

namespace OS.Std.Memory;

public enum FileSectorCopyStatus : byte
{
    Uninitialized = 0,
    Success,
    InvalidLength,
    IncompleteRead,
    InvalidOffset,
    Overlap
}
