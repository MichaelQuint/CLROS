namespace OS.Boot;

public enum UefiMemoryMapCopyPlanStatus
{
    Success = 0,
    MissingSource = 1,
    InvalidMapSize = 2,
    InvalidDescriptorSize = 3,
    InvalidDescriptorVersion = 4,
    InvalidDescriptorCount = 5,
    MapSizeDescriptorMismatch = 6,
    DescriptorCountLimitExceeded = 7,
    SizeOverflow = 8,
    DestinationTooSmall = 9
}

public readonly struct UefiMemoryMapCopyPlan
{
    public const ulong MinimumDescriptorSize = 40;
    public const ulong DefaultMaximumDescriptorCount = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;

    private UefiMemoryMapCopyPlan(
        ulong sourceAddress,
        ulong sourceBufferSize,
        ulong sourceMapSize,
        ulong destinationCapacity,
        ulong requiredDestinationBytes,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount)
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

    public ulong SourceAddress { get; }

    public ulong SourceBufferSize { get; }

    public ulong SourceMapSize { get; }

    public ulong DestinationCapacity { get; }

    public ulong RequiredDestinationBytes { get; }

    public ulong MapKey { get; }

    public ulong DescriptorSize { get; }

    public uint DescriptorVersion { get; }

    public ulong DescriptorCount { get; }

    public static UefiMemoryMapCopyPlan CreateUnchecked(
        ulong sourceAddress,
        ulong sourceBufferSize,
        ulong sourceMapSize,
        ulong destinationCapacity,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount)
    {
        return new UefiMemoryMapCopyPlan(
            sourceAddress,
            sourceBufferSize,
            sourceMapSize,
            destinationCapacity,
            sourceMapSize,
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount);
    }
}

public readonly struct UefiMemoryMapCopyPlanResult
{
    private UefiMemoryMapCopyPlanResult(
        bool succeeded,
        UefiMemoryMapCopyPlanStatus status,
        UefiMemoryMapCopyPlan plan)
    {
        Succeeded = succeeded;
        Status = status;
        Plan = plan;
    }

    public bool Succeeded { get; }

    public UefiMemoryMapCopyPlanStatus Status { get; }

    public UefiMemoryMapCopyPlan Plan { get; }

    public static UefiMemoryMapCopyPlanResult Create(
        FirmwareMemoryMapRecord metadata,
        ulong destinationCapacity)
    {
        return Create(
            metadata,
            destinationCapacity,
            UefiMemoryMapCopyPlan.DefaultMaximumDescriptorCount);
    }

    public static UefiMemoryMapCopyPlanResult Create(
        FirmwareMemoryMapRecord metadata,
        ulong destinationCapacity,
        ulong maximumDescriptorCount)
    {
        if (metadata.BufferAddress == 0 || metadata.BufferSize == 0 || metadata.MapSize == 0)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.MissingSource);
        }

        if (metadata.MapSize > metadata.BufferSize)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.InvalidMapSize);
        }

        if (metadata.DescriptorSize < UefiMemoryMapCopyPlan.MinimumDescriptorSize)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.InvalidDescriptorSize);
        }

        if (metadata.DescriptorVersion == 0)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.InvalidDescriptorVersion);
        }

        if (metadata.MapSize % metadata.DescriptorSize != 0)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.MapSizeDescriptorMismatch);
        }

        ulong computedDescriptorCount = metadata.MapSize / metadata.DescriptorSize;
        if (computedDescriptorCount == 0 || metadata.DescriptorCount == 0)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount);
        }

        if (metadata.DescriptorCount > maximumDescriptorCount || computedDescriptorCount > maximumDescriptorCount)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.DescriptorCountLimitExceeded);
        }

        if (metadata.DescriptorCount > UefiMemoryMapCopyPlan.MaximumUnsignedLong / metadata.DescriptorSize)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.SizeOverflow);
        }

        ulong declaredByteCount = metadata.DescriptorCount * metadata.DescriptorSize;
        if (declaredByteCount != metadata.MapSize || computedDescriptorCount != metadata.DescriptorCount)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount);
        }

        if (destinationCapacity < metadata.MapSize)
        {
            return Fail(UefiMemoryMapCopyPlanStatus.DestinationTooSmall);
        }

        return new UefiMemoryMapCopyPlanResult(
            true,
            UefiMemoryMapCopyPlanStatus.Success,
            UefiMemoryMapCopyPlan.CreateUnchecked(
                metadata.BufferAddress,
                metadata.BufferSize,
                metadata.MapSize,
                destinationCapacity,
                metadata.MapKey,
                metadata.DescriptorSize,
                metadata.DescriptorVersion,
                metadata.DescriptorCount));
    }

    private static UefiMemoryMapCopyPlanResult Fail(UefiMemoryMapCopyPlanStatus status)
    {
        return new UefiMemoryMapCopyPlanResult(false, status, default);
    }
}
