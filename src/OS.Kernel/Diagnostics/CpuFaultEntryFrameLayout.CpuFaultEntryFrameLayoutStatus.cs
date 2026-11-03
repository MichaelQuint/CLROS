using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum CpuFaultEntryFrameLayoutStatus
{
    Ok = 0,
    BufferTooSmall = 1,
    UnknownVector = 2,
    InvalidHasErrorCodeFlag = 3,
    InconsistentErrorCodeMetadata = 4
}
