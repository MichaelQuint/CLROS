namespace OS.Boot;

public readonly struct EfiMemoryMapSummary
{
    public EfiMemoryMapSummary(ulong descriptorCount, ulong totalPages, ulong totalBytes, ulong conventionalPages, ulong lowestPhysicalAddress, ulong highestPhysicalEndExclusive)
    {
        DescriptorCount = descriptorCount;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalPages = conventionalPages;
        LowestPhysicalAddress = lowestPhysicalAddress;
        HighestPhysicalEndExclusive = highestPhysicalEndExclusive;
    }

    public ulong DescriptorCount
    {
        get;
    }
    public ulong TotalPages
    {
        get;
    }
    public ulong TotalBytes
    {
        get;
    }
    public ulong ConventionalPages
    {
        get;
    }
    public ulong LowestPhysicalAddress
    {
        get;
    }
    public ulong HighestPhysicalEndExclusive
    {
        get;
    }
}
