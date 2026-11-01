using OS.Std.Hash;

namespace OS.Manifest;
public static unsafe partial class LinuxElfPackage
{
    /// <summary>Borrowed offsets into one validated, retained immutable package.
    /// No pointers, allocation or independent lifetime are granted.</summary>

    public struct DataFile
    {
        public uint PathOffset, PathBytes, DataOffset, DataBytes;
    }
#if !NOSTDLIB_EFI
#endif
}
