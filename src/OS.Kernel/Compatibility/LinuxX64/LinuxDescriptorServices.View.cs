using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Retained immutable package data, with an exact absolute ASCII
    /// path below /app/ and a nonzero owner-assigned inode at least 256. Paths
    /// contain no dot segments. The package owner verifies provenance and denies mutation;
    /// this adapter neither acquires files nor opens executable images.</summary>

    public struct View
    {
        public byte* Path;
        public uint PathBytes;
        public byte* Data;
        public ulong Bytes, Inode;
    }
}
