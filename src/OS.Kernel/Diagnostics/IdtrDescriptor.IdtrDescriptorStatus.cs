using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public enum IdtrDescriptorStatus
{
    Ok = 0,
    NullBaseAddress = 1,
    TableTooSmall = 2,
    TableTooLarge = 3,
    OutputBufferTooSmall = 4
}
