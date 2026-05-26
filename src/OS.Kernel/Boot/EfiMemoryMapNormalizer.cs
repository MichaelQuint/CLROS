namespace OS.Boot;

public enum EfiMemoryMapNormalizeStatus
{
    Success = 0,
    MissingSource = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    SizeOverflow = 5,
    SourceTooSmall = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    AddressRangeOverflow = 9,
    TotalPageCountOverflow = 10,
    TotalByteCountOverflow = 11
}

public readonly struct EfiMemoryMapSummary
{
    public EfiMemoryMapSummary(
        ulong descriptorCount,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalPages,
        ulong lowestPhysicalAddress,
        ulong highestPhysicalEndExclusive)
    {
        DescriptorCount = descriptorCount;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalPages = conventionalPages;
        LowestPhysicalAddress = lowestPhysicalAddress;
        HighestPhysicalEndExclusive = highestPhysicalEndExclusive;
    }

    public ulong DescriptorCount { get; }

    public ulong TotalPages { get; }

    public ulong TotalBytes { get; }

    public ulong ConventionalPages { get; }

    public ulong LowestPhysicalAddress { get; }

    public ulong HighestPhysicalEndExclusive { get; }
}

public readonly struct EfiMemoryMapNormalizeResult
{
    private EfiMemoryMapNormalizeResult(
        bool succeeded,
        EfiMemoryMapNormalizeStatus status,
        EfiMemoryMapSummary summary,
        ulong invalidDescriptorIndex,
        ulong requiredSourceBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Summary = summary;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        RequiredSourceBytes = requiredSourceBytes;
    }

    public bool Succeeded { get; }

    public EfiMemoryMapNormalizeStatus Status { get; }

    public EfiMemoryMapSummary Summary { get; }

    public ulong InvalidDescriptorIndex { get; }

    public ulong RequiredSourceBytes { get; }

    public static EfiMemoryMapNormalizeResult Success(EfiMemoryMapSummary summary, ulong requiredSourceBytes)
    {
        return new EfiMemoryMapNormalizeResult(
            true,
            EfiMemoryMapNormalizeStatus.Success,
            summary,
            0,
            requiredSourceBytes);
    }

    public static EfiMemoryMapNormalizeResult Fail(
        EfiMemoryMapNormalizeStatus status,
        ulong invalidDescriptorIndex = 0,
        ulong requiredSourceBytes = 0)
    {
        return new EfiMemoryMapNormalizeResult(
            false,
            status,
            default,
            invalidDescriptorIndex,
            requiredSourceBytes);
    }
}

public static unsafe class EfiMemoryMapNormalizer
{
    public const ulong PageSize = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;
    private const uint UefiConventionalMemoryType = 7;

    public static EfiMemoryMapNormalizeResult Normalize(
        void* source,
        ulong sourceByteCount,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount)
    {
        if (source == null || sourceByteCount == 0)
        {
            return EfiMemoryMapNormalizeResult.Fail(EfiMemoryMapNormalizeStatus.MissingSource);
        }

        if (descriptorSize != EfiBootHandoffValidator.DescriptorStride40
            && descriptorSize != EfiBootHandoffValidator.DescriptorStride48)
        {
            return EfiMemoryMapNormalizeResult.Fail(EfiMemoryMapNormalizeStatus.InvalidDescriptorSize);
        }

        if (descriptorVersion == 0)
        {
            return EfiMemoryMapNormalizeResult.Fail(EfiMemoryMapNormalizeStatus.InvalidDescriptorVersion);
        }

        if (descriptorCount == 0
            || descriptorCount > EfiBootHandoffValidator.MaximumDescriptorCount)
        {
            return EfiMemoryMapNormalizeResult.Fail(EfiMemoryMapNormalizeStatus.InvalidDescriptorCount);
        }

        if (descriptorCount > MaximumUnsignedLong / descriptorSize)
        {
            return EfiMemoryMapNormalizeResult.Fail(EfiMemoryMapNormalizeStatus.SizeOverflow);
        }

        ulong requiredSourceBytes = descriptorCount * descriptorSize;
        if (sourceByteCount < requiredSourceBytes)
        {
            return EfiMemoryMapNormalizeResult.Fail(
                EfiMemoryMapNormalizeStatus.SourceTooSmall,
                requiredSourceBytes: requiredSourceBytes);
        }

        byte* sourceBytes = (byte*)source;
        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong conventionalPages = 0;
        ulong lowestPhysicalAddress = MaximumUnsignedLong;
        ulong highestPhysicalEndExclusive = 0;

        for (ulong descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            byte* descriptor = sourceBytes + (descriptorIndex * descriptorSize);
            uint type = ReadUInt32LittleEndian(descriptor);
            ulong physicalStart = ReadUInt64LittleEndian(descriptor + 8);
            ulong numberOfPages = ReadUInt64LittleEndian(descriptor + 24);

            if (numberOfPages == 0)
            {
                return EfiMemoryMapNormalizeResult.Fail(
                    EfiMemoryMapNormalizeStatus.InvalidPageCount,
                    descriptorIndex);
            }

            if (numberOfPages > MaximumUnsignedLong / PageSize)
            {
                return EfiMemoryMapNormalizeResult.Fail(
                    EfiMemoryMapNormalizeStatus.PageByteCountOverflow,
                    descriptorIndex);
            }

            ulong byteLength = numberOfPages * PageSize;
            if (physicalStart > MaximumUnsignedLong - byteLength)
            {
                return EfiMemoryMapNormalizeResult.Fail(
                    EfiMemoryMapNormalizeStatus.AddressRangeOverflow,
                    descriptorIndex);
            }

            if (totalPages > MaximumUnsignedLong - numberOfPages)
            {
                return EfiMemoryMapNormalizeResult.Fail(
                    EfiMemoryMapNormalizeStatus.TotalPageCountOverflow,
                    descriptorIndex);
            }

            if (totalBytes > MaximumUnsignedLong - byteLength)
            {
                return EfiMemoryMapNormalizeResult.Fail(
                    EfiMemoryMapNormalizeStatus.TotalByteCountOverflow,
                    descriptorIndex);
            }

            ulong physicalEndExclusive = physicalStart + byteLength;
            totalPages += numberOfPages;
            totalBytes += byteLength;

            if (type == UefiConventionalMemoryType)
            {
                if (conventionalPages > MaximumUnsignedLong - numberOfPages)
                {
                    return EfiMemoryMapNormalizeResult.Fail(
                        EfiMemoryMapNormalizeStatus.TotalPageCountOverflow,
                        descriptorIndex);
                }

                conventionalPages += numberOfPages;
            }

            if (physicalStart < lowestPhysicalAddress)
            {
                lowestPhysicalAddress = physicalStart;
            }

            if (physicalEndExclusive > highestPhysicalEndExclusive)
            {
                highestPhysicalEndExclusive = physicalEndExclusive;
            }
        }

        EfiMemoryMapSummary summary = new EfiMemoryMapSummary(
            descriptorCount,
            totalPages,
            totalBytes,
            conventionalPages,
            lowestPhysicalAddress,
            highestPhysicalEndExclusive);

        return EfiMemoryMapNormalizeResult.Success(summary, requiredSourceBytes);
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
