using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum GdtDescriptorPlanStatus
{
    Ok = 0,
    OutputBufferTooSmall = 1,
    NullTssBaseAddress = 2,
    InvalidTssLimit = 3
}
