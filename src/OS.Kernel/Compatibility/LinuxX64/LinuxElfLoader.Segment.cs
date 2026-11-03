using OS.Std.Binary;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxElfLoader
{
    private struct Segment
    {
        internal uint Type, Flags;
        internal ulong Offset, Address, FileBytes, MemoryBytes, Alignment;
    }
}
