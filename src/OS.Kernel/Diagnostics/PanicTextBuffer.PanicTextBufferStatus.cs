using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum PanicTextBufferStatus
{
    Ok = 0,
    Overflow = 1,
    InvalidAscii = 2
}
