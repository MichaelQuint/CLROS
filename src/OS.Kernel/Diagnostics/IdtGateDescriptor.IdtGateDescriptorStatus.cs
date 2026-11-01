using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public enum IdtGateDescriptorStatus
{
    Ok = 0,
    InvalidVector = 1,
    InvalidIstIndex = 2,
    InvalidSelector = 3,
    InvalidGateType = 4,
    NullHandlerAddress = 5,
    InvalidDescriptorPrivilegeLevel = 6
}
