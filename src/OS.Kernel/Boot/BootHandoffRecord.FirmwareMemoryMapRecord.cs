namespace OS.Boot;
public struct FirmwareMemoryMapRecord
{
    public const uint FlagOwnedCopy = 1;
    public ulong BufferAddress;
    public ulong BufferSize;
    public ulong SourceBufferAddress;
    public ulong SourceBufferSize;
    public ulong MapSize;
    public ulong MapKey;
    public ulong DescriptorSize;
    public uint DescriptorVersion;
    public ulong DescriptorCount;
    public uint Flags;
    public bool HasMap()
    {
        return BufferAddress != 0 && BufferSize != 0 && MapSize != 0;
    }

    public bool HasOwnedCopy()
    {
        return HasMap() && (Flags & FlagOwnedCopy) != 0;
    }
}
