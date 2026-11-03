using OS.Kernel.Platform;

namespace OS.Boot;

public readonly struct FirmwareMemoryMapMetadata
{
    public FirmwareMemoryMapMetadata(ulong bufferAddress, ulong bufferSize, ulong mapSize, ulong mapKey, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount)
    {
        BufferAddress = bufferAddress;
        BufferSize = bufferSize;
        MapSize = mapSize;
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
    }

    public ulong BufferAddress
    {
        get;
    }
    public ulong BufferSize
    {
        get;
    }
    public ulong MapSize
    {
        get;
    }
    public ulong MapKey
    {
        get;
    }
    public ulong DescriptorSize
    {
        get;
    }
    public uint DescriptorVersion
    {
        get;
    }
    public ulong DescriptorCount
    {
        get;
    }
    public bool HasMap => BufferAddress != 0 && BufferSize != 0 && MapSize != 0;
}
