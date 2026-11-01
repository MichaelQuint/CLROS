using OS.Std.Text;
using OS.Kernel.Memory;

namespace OS.Manifest;
public static unsafe partial class LinuxAppConfiguration
{
    public enum Status : byte
    {
        Uninitialized,
        Unconfigured,
        Success,
        InvalidManifest,
        MissingLimit,
        UnsupportedOverride,
        InvalidLimit,
        InvalidEntitlement
    }
}
