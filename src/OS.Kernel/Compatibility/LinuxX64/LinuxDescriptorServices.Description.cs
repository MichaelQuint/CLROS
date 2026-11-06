using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Caller-zeroed open descriptions. Duplicates share position,
    /// status flags and endpoint ownership. Only the last close releases them.</summary>

    public struct Description
    {
        internal uint Type, References, Flags, Pipe, Borrowers;
        internal byte* Data;
        internal ulong Bytes, Position, Inode;
        internal uint Channel;
    }
}
