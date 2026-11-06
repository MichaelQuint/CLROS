using OS.Kernel.Platform;

namespace OS.Boot;

public struct FirmwareAdapterMemoryMapInfo
{
    public ulong Bytes, MapKey, DescriptorBytes;
    public uint DescriptorVersion, Reserved;
}
