using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Kernel.Diagnostics;
public enum DescriptorTableInstallStubPlanStatus
{
    Ok = 0,
    InvalidOperationKind = 1,
    InvalidDescriptorPayloadLength = 2,
    InvalidSelector = 3,
    OutputBufferTooSmall = 4
}
