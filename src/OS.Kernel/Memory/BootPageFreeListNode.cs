namespace OS.Kernel.Memory;

public readonly struct BootPageFreeListNode
{
    public const ulong StructureSize = 48;
    // This typed POD is not wire-compatible with the legacy EFI six-ulong
    // node-slot table. A future live adapter must transcode both provenance
    // indexes and the signed terminal link instead of reinterpreting bytes.
    public BootPageFreeListNode(int sourceFreeSegmentIndex, int sourceUsableRangeIndex, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, int nextNodeIndex)
    {
        SourceFreeSegmentIndex = sourceFreeSegmentIndex;
        SourceUsableRangeIndex = sourceUsableRangeIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        NextNodeIndex = nextNodeIndex;
    }

    public int SourceFreeSegmentIndex
    {
        get;
    }
    public int SourceUsableRangeIndex
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
    public int NextNodeIndex
    {
        get;
    }
}
