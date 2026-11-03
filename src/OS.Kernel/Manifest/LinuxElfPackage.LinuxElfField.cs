using OS.Std.Hash;

namespace OS.Manifest;

public struct LinuxElfField
{
    public uint Offset, Length;
}
#if !NOSTDLIB_EFI
#endif
