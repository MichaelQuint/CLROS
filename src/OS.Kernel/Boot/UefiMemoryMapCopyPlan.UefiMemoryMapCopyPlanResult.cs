namespace OS.Boot;
public readonly struct UefiMemoryMapCopyPlanResult
{
    private UefiMemoryMapCopyPlanResult(bool succeeded, UefiMemoryMapCopyPlanStatus status, UefiMemoryMapCopyPlan plan)
    {
        Succeeded = succeeded;
        Status = status;
        Plan = plan;
    }

    public bool Succeeded { get; }
    public UefiMemoryMapCopyPlanStatus Status { get; }
    public UefiMemoryMapCopyPlan Plan { get; }

    public static UefiMemoryMapCopyPlanResult Create(FirmwareMemoryMapRecord metadata, ulong destinationCapacity)
    {
        return Create(metadata, destinationCapacity, UefiMemoryMapCopyPlan.DefaultMaximumDescriptorCount);
    }

    public static UefiMemoryMapCopyPlanResult Create(FirmwareMemoryMapRecord metadata, ulong destinationCapacity, ulong maximumDescriptorCount)
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

        return new UefiMemoryMapCopyPlanResult(true, UefiMemoryMapCopyPlanStatus.Success, UefiMemoryMapCopyPlan.CreateUnchecked(metadata.BufferAddress, metadata.BufferSize, metadata.MapSize, destinationCapacity, metadata.MapKey, metadata.DescriptorSize, metadata.DescriptorVersion, metadata.DescriptorCount));
    }

    private static UefiMemoryMapCopyPlanResult Fail(UefiMemoryMapCopyPlanStatus status)
    {
        return new UefiMemoryMapCopyPlanResult(false, status, default);
    }
}
