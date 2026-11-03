using OS.Manifest;

namespace OS.Kernel.Runtime;

[Flags]
public enum NativeAppExecutableMemoryProtection : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
