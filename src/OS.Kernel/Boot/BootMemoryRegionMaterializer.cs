namespace OS.Boot;

public enum BootMemoryRegionMaterializationStatus
{
    Success = 0,
    MissingSource = 1,
    MissingDestination = 2,
    InvalidDescriptorSize = 3,
    InvalidDescriptorVersion = 4,
    InvalidDescriptorCount = 5,
    SizeOverflow = 6,
    SourceTooSmall = 7,
    DestinationTooSmall = 8,
    InvalidPageCount = 9,
    PageByteCountOverflow = 10,
    AddressRangeOverflow = 11,
    TotalPageCountOverflow = 12,
    TotalByteCountOverflow = 13
}

public readonly struct BootMemoryRegionRecord
{
    public const ulong PageSize = 4096;

    public BootMemoryRegionRecord(
        uint uefiType,
        ulong physicalStart,
        ulong virtualStart,
        ulong pageCount,
        ulong attribute,
        ulong byteLength,
        ulong physicalEndExclusive)
    {
        UefiType = uefiType;
        PhysicalStart = physicalStart;
        VirtualStart = virtualStart;
        PageCount = pageCount;
        Attribute = attribute;
        ByteLength = byteLength;
        PhysicalEndExclusive = physicalEndExclusive;
    }

    public uint UefiType { get; }

    public ulong PhysicalStart { get; }

    public ulong VirtualStart { get; }

    public ulong PageCount { get; }

    public ulong Attribute { get; }

    public ulong ByteLength { get; }

    public ulong PhysicalEndExclusive { get; }
}

public readonly struct BootMemoryRegionMaterializationResult
{
    private BootMemoryRegionMaterializationResult(
        BootMemoryRegionMaterializationStatus status,
        ulong descriptorCount,
        ulong regionCount,
        ulong requiredRegionCapacity,
        ulong availableRegionCapacity,
        ulong invalidDescriptorIndex,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes)
    {
        Status = status;
        DescriptorCount = descriptorCount;
        RegionCount = regionCount;
        RequiredRegionCapacity = requiredRegionCapacity;
        AvailableRegionCapacity = availableRegionCapacity;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalMemoryPages = conventionalMemoryPages;
        ConventionalMemoryBytes = conventionalMemoryBytes;
    }

    public BootMemoryRegionMaterializationStatus Status { get; }

    public bool Succeeded => Status == BootMemoryRegionMaterializationStatus.Success;

    public ulong DescriptorCount { get; }

    public ulong RegionCount { get; }

    public ulong RequiredRegionCapacity { get; }

    public ulong AvailableRegionCapacity { get; }

    public ulong InvalidDescriptorIndex { get; }

    public ulong TotalPages { get; }

    public ulong TotalBytes { get; }

    public ulong ConventionalMemoryPages { get; }

    public ulong ConventionalMemoryBytes { get; }

    public static BootMemoryRegionMaterializationResult Success(
        ulong descriptorCount,
        ulong availableRegionCapacity,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes)
    {
        return new BootMemoryRegionMaterializationResult(
            BootMemoryRegionMaterializationStatus.Success,
            descriptorCount,
            descriptorCount,
            descriptorCount,
            availableRegionCapacity,
            invalidDescriptorIndex: 0,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes);
    }

    public static BootMemoryRegionMaterializationResult Fail(
        BootMemoryRegionMaterializationStatus status,
        ulong descriptorCount = 0,
        ulong requiredRegionCapacity = 0,
        ulong availableRegionCapacity = 0,
        ulong invalidDescriptorIndex = 0,
        ulong totalPages = 0,
        ulong totalBytes = 0,
        ulong conventionalMemoryPages = 0,
        ulong conventionalMemoryBytes = 0)
    {
        return new BootMemoryRegionMaterializationResult(
            status,
            descriptorCount,
            regionCount: 0,
            requiredRegionCapacity,
            availableRegionCapacity,
            invalidDescriptorIndex,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes);
    }
}

public static unsafe class BootMemoryRegionMaterializer
{
    public const ulong DescriptorStride40 = 40;
    public const ulong DescriptorStride48 = 48;
    public const ulong MaximumDescriptorCount = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;

    private const uint UefiConventionalMemoryType = 7;

#if !NOSTDLIB_EFI
    public static BootMemoryRegionMaterializationResult Materialize(
        ReadOnlySpan<byte> source,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount,
        Span<BootMemoryRegionRecord> destination)
    {
        fixed (byte* sourcePointer = source)
        fixed (BootMemoryRegionRecord* destinationPointer = destination)
        {
            return Materialize(
                sourcePointer,
                (ulong)source.Length,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                destinationPointer,
                (ulong)destination.Length);
        }
    }
#endif

    public static BootMemoryRegionMaterializationResult Materialize(
        void* source,
        ulong sourceByteCount,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount,
        BootMemoryRegionRecord* destination,
        ulong destinationRegionCapacity)
    {
        BootMemoryRegionMaterializationResult validation = ValidateMetadata(
            source,
            sourceByteCount,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            destination,
            destinationRegionCapacity);
        if (!validation.Succeeded)
        {
            return validation;
        }

        BootMemoryRegionMaterializationResult summary = Summarize(
            (byte*)source,
            descriptorSize,
            descriptorCount,
            destinationRegionCapacity);
        if (!summary.Succeeded)
        {
            return summary;
        }

        WriteRegionsUnchecked(
            (byte*)source,
            descriptorSize,
            descriptorCount,
            destination);

        return BootMemoryRegionMaterializationResult.Success(
            descriptorCount,
            destinationRegionCapacity,
            summary.TotalPages,
            summary.TotalBytes,
            summary.ConventionalMemoryPages,
            summary.ConventionalMemoryBytes);
    }

    private static BootMemoryRegionMaterializationResult ValidateMetadata(
        void* source,
        ulong sourceByteCount,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount,
        BootMemoryRegionRecord* destination,
        ulong destinationRegionCapacity)
    {
        if (source == null || sourceByteCount == 0)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.MissingSource,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (destination == null || destinationRegionCapacity == 0)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.MissingDestination,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (descriptorSize != DescriptorStride40 && descriptorSize != DescriptorStride48)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.InvalidDescriptorSize,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (descriptorVersion == 0)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.InvalidDescriptorVersion,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (descriptorCount == 0 || descriptorCount > MaximumDescriptorCount)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.InvalidDescriptorCount,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (descriptorCount > MaximumUnsignedLong / descriptorSize)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.SizeOverflow,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        ulong requiredSourceBytes = descriptorCount * descriptorSize;
        if (sourceByteCount < requiredSourceBytes)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.SourceTooSmall,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        if (destinationRegionCapacity < descriptorCount)
        {
            return BootMemoryRegionMaterializationResult.Fail(
                BootMemoryRegionMaterializationStatus.DestinationTooSmall,
                descriptorCount,
                descriptorCount,
                destinationRegionCapacity);
        }

        return BootMemoryRegionMaterializationResult.Success(
            descriptorCount,
            destinationRegionCapacity,
            totalPages: 0,
            totalBytes: 0,
            conventionalMemoryPages: 0,
            conventionalMemoryBytes: 0);
    }

    private static BootMemoryRegionMaterializationResult Summarize(
        byte* source,
        ulong descriptorSize,
        ulong descriptorCount,
        ulong destinationRegionCapacity)
    {
        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong conventionalMemoryPages = 0;
        ulong conventionalMemoryBytes = 0;

        for (ulong descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            byte* descriptor = source + (descriptorIndex * descriptorSize);
            uint uefiType = ReadUInt32LittleEndian(descriptor);
            ulong physicalStart = ReadUInt64LittleEndian(descriptor + 8);
            ulong pageCount = ReadUInt64LittleEndian(descriptor + 24);

            if (pageCount == 0)
            {
                return BootMemoryRegionMaterializationResult.Fail(
                    BootMemoryRegionMaterializationStatus.InvalidPageCount,
                    descriptorCount,
                    descriptorCount,
                    destinationRegionCapacity,
                    descriptorIndex,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            if (pageCount > MaximumUnsignedLong / BootMemoryRegionRecord.PageSize)
            {
                return BootMemoryRegionMaterializationResult.Fail(
                    BootMemoryRegionMaterializationStatus.PageByteCountOverflow,
                    descriptorCount,
                    descriptorCount,
                    destinationRegionCapacity,
                    descriptorIndex,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            ulong byteLength = pageCount * BootMemoryRegionRecord.PageSize;
            if (physicalStart > MaximumUnsignedLong - byteLength)
            {
                return BootMemoryRegionMaterializationResult.Fail(
                    BootMemoryRegionMaterializationStatus.AddressRangeOverflow,
                    descriptorCount,
                    descriptorCount,
                    destinationRegionCapacity,
                    descriptorIndex,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            if (totalPages > MaximumUnsignedLong - pageCount)
            {
                return BootMemoryRegionMaterializationResult.Fail(
                    BootMemoryRegionMaterializationStatus.TotalPageCountOverflow,
                    descriptorCount,
                    descriptorCount,
                    destinationRegionCapacity,
                    descriptorIndex,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            if (totalBytes > MaximumUnsignedLong - byteLength)
            {
                return BootMemoryRegionMaterializationResult.Fail(
                    BootMemoryRegionMaterializationStatus.TotalByteCountOverflow,
                    descriptorCount,
                    descriptorCount,
                    destinationRegionCapacity,
                    descriptorIndex,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            totalPages += pageCount;
            totalBytes += byteLength;

            if (uefiType == UefiConventionalMemoryType)
            {
                if (conventionalMemoryPages > MaximumUnsignedLong - pageCount)
                {
                    return BootMemoryRegionMaterializationResult.Fail(
                        BootMemoryRegionMaterializationStatus.TotalPageCountOverflow,
                        descriptorCount,
                        descriptorCount,
                        destinationRegionCapacity,
                        descriptorIndex,
                        totalPages,
                        totalBytes,
                        conventionalMemoryPages,
                        conventionalMemoryBytes);
                }

                if (conventionalMemoryBytes > MaximumUnsignedLong - byteLength)
                {
                    return BootMemoryRegionMaterializationResult.Fail(
                        BootMemoryRegionMaterializationStatus.TotalByteCountOverflow,
                        descriptorCount,
                        descriptorCount,
                        destinationRegionCapacity,
                        descriptorIndex,
                        totalPages,
                        totalBytes,
                        conventionalMemoryPages,
                        conventionalMemoryBytes);
                }

                conventionalMemoryPages += pageCount;
                conventionalMemoryBytes += byteLength;
            }
        }

        return BootMemoryRegionMaterializationResult.Success(
            descriptorCount,
            destinationRegionCapacity,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes);
    }

    private static void WriteRegionsUnchecked(
        byte* source,
        ulong descriptorSize,
        ulong descriptorCount,
        BootMemoryRegionRecord* destination)
    {
        for (ulong descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            byte* descriptor = source + (descriptorIndex * descriptorSize);
            uint uefiType = ReadUInt32LittleEndian(descriptor);
            ulong physicalStart = ReadUInt64LittleEndian(descriptor + 8);
            ulong virtualStart = ReadUInt64LittleEndian(descriptor + 16);
            ulong pageCount = ReadUInt64LittleEndian(descriptor + 24);
            ulong attribute = ReadUInt64LittleEndian(descriptor + 32);
            ulong byteLength = pageCount * BootMemoryRegionRecord.PageSize;

            destination[descriptorIndex] = new BootMemoryRegionRecord(
                uefiType,
                physicalStart,
                virtualStart,
                pageCount,
                attribute,
                byteLength,
                physicalStart + byteLength);
        }
    }

    private static uint ReadUInt32LittleEndian(byte* source)
    {
        return source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    private static ulong ReadUInt64LittleEndian(byte* source)
    {
        return (ulong)source[0]
            | ((ulong)source[1] << 8)
            | ((ulong)source[2] << 16)
            | ((ulong)source[3] << 24)
            | ((ulong)source[4] << 32)
            | ((ulong)source[5] << 40)
            | ((ulong)source[6] << 48)
            | ((ulong)source[7] << 56);
    }
}
