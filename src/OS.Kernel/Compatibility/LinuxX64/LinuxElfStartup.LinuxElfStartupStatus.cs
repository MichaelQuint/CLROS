using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxElfStartupStatus : byte
{
    Uninitialized,
    Ready,
    InvalidManifest,
    InvalidElf,
    InvalidStorage,
    InvalidEntropy,
    OverlappingStorage,
    InsufficientStack,
    ReadBackFailed
}
