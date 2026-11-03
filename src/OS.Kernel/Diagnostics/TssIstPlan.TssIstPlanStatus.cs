using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public enum TssIstPlanStatus
{
    Ok = 0,
    OutputBufferTooSmall = 1,
    InvalidIstIndex = 2,
    NullDoubleFaultIstStackTop = 3,
    UnalignedDoubleFaultIstStackTop = 4,
    InvalidStackSize = 5,
    InvalidGuardSize = 6
}
