namespace OS.Boot;

public readonly struct UefiMemoryMapCopyPlan
{
    public const ulong MinimumDescriptorSize = 40;
    public const ulong DefaultMaximumDescriptorCount = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;
    private UefiMemoryMapCopyPlan(ulong sourceAddress, ulong sourceBufferSize, ulong sourceMapSize, ulong destinationCapacity, ulong requiredDestinationBytes, ulong mapKey, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount)
    {
        SourceAddress = sourceAddress;
        SourceBufferSize = sourceBufferSize;
        SourceMapSize = sourceMapSize;
        DestinationCapacity = destinationCapacity;
        RequiredDestinationBytes = requiredDestinationBytes;
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
    }

    public ulong SourceAddress
    {
        get;
    }
    public ulong SourceBufferSize
    {
        get;
    }
    public ulong SourceMapSize
    {
        get;
    }
    public ulong DestinationCapacity
    {
        get;
    }
    public ulong RequiredDestinationBytes
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

    public static UefiMemoryMapCopyPlan CreateUnchecked(ulong sourceAddress, ulong sourceBufferSize, ulong sourceMapSize, ulong destinationCapacity, ulong mapKey, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount)
    {
        return new UefiMemoryMapCopyPlan(sourceAddress, sourceBufferSize, sourceMapSize, destinationCapacity, sourceMapSize, mapKey, descriptorSize, descriptorVersion, descriptorCount);
    }
}
