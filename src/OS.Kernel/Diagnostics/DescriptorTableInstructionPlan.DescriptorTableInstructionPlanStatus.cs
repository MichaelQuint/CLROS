using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public enum DescriptorTableInstructionPlanStatus
{
    Ok = 0,
    InvalidOperationKind = 1,
    OutputBufferTooSmall = 2
}
