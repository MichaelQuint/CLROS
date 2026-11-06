using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct BootMemoryUsableRange
{
    public BootMemoryUsableRange(int sourceRegionIndex, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, ulong attribute)
    {
        SourceRegionIndex = sourceRegionIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        Attribute = attribute;
    }

    public int SourceRegionIndex
    {
        get;
    }
    public ulong PhysicalStart
    {
        get;
    }
    public ulong PhysicalEndExclusive
    {
        get;
    }
    public ulong PageCount
    {
        get;
    }
    public ulong ByteLength
    {
        get;
    }
    public ulong Attribute
    {
        get;
    }
}
