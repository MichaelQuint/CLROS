using OS.Std.Hash;

namespace OS.Manifest;
public struct LinuxElfPackageView
{
    public LinuxElfStatus Status;
    public uint ByteCount, ManifestOffset, ManifestBytes, PayloadOffset, PayloadBytes;
    public uint DataTableOffset, DataFileCount;
    public LinuxElfManifest Manifest;
    public readonly bool Succeeded => Status == LinuxElfStatus.Success;
}
#if !NOSTDLIB_EFI
#endif
