namespace OS.Boot;

public readonly struct BootMemoryRegionRecord
{
    public const ulong PageSize = 4096;
    public BootMemoryRegionRecord(uint uefiType, ulong physicalStart, ulong virtualStart, ulong pageCount, ulong attribute, ulong byteLength, ulong physicalEndExclusive)
    {
        UefiType = uefiType;
        PhysicalStart = physicalStart;
        VirtualStart = virtualStart;
        PageCount = pageCount;
        Attribute = attribute;
        ByteLength = byteLength;
        PhysicalEndExclusive = physicalEndExclusive;
    }

    public uint UefiType
    {
        get;
    }
    public ulong PhysicalStart
    {
        get;
    }
    public ulong VirtualStart
    {
        get;
    }
    public ulong PageCount
    {
        get;
    }
    public ulong Attribute
    {
        get;
    }
    public ulong ByteLength
    {
        get;
    }
    public ulong PhysicalEndExclusive
    {
        get;
    }
}
#if !NOSTDLIB_EFI
#endif
