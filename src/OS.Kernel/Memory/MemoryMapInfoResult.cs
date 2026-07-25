using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct MemoryMapInfoResult
{
    public const ulong MinimumUefiDescriptorSize = 40;

    private readonly string? _message;

    private MemoryMapInfoResult(
        bool succeeded,
        MemoryMapInfoStatus status,
        MemoryMapInfo info,
        int invalidDescriptorIndex,
        string message)
    {
        Succeeded = succeeded;
        Status = status;
        Info = info;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        _message = message;
    }

    public bool Succeeded { get; }

    public MemoryMapInfoStatus Status { get; }

    public MemoryMapInfo Info { get; }

    public int InvalidDescriptorIndex { get; }

    public string Message => _message ?? string.Empty;

    public static MemoryMapInfoResult FromUefiSnapshot(UefiMemoryMapSnapshot snapshot)
    {
        return FromUefiDescriptors(
            snapshot.MapKey,
            snapshot.DescriptorSize,
            snapshot.DescriptorVersion,
            snapshot.DescriptorCount,
            snapshot.DescriptorEntries);
    }

    public static MemoryMapInfoResult FromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        UefiMemoryDescriptor[]? descriptorEntries)
    {
        if (descriptorEntries is null)
        {
            return ValidateMetadata(
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                descriptorEntriesLength: 0,
                entriesAreMissing: true);
        }

        MemoryMapInfoResult metadataResult = ValidateMetadata(
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries.Length,
            entriesAreMissing: false);

        if (!metadataResult.Succeeded)
        {
            return metadataResult;
        }

        return Build(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries);
    }

    public static MemoryMapInfoResult FromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries)
    {
        MemoryMapInfoResult metadataResult = ValidateMetadata(
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries.Length,
            entriesAreMissing: false);

        if (!metadataResult.Succeeded)
        {
            return metadataResult;
        }

        return Build(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiSnapshot(
        UefiMemoryMapSnapshot snapshot,
        Span<MemoryRegion> destination)
    {
        return TryWriteRegionsFromUefiDescriptors(
            snapshot.MapKey,
            snapshot.DescriptorSize,
            snapshot.DescriptorVersion,
            snapshot.DescriptorCount,
            snapshot.DescriptorEntries,
            destination);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        UefiMemoryDescriptor[]? descriptorEntries,
        Span<MemoryRegion> destination)
    {
        MemoryMapInfoRegionWriteResult metadataResult = ValidateRegionWriteMetadata(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntriesLength: descriptorEntries is null ? 0 : descriptorEntries.Length,
            entriesAreMissing: descriptorEntries is null,
            availableRegionCapacity: destination.Length);

        if (!metadataResult.Succeeded)
        {
            return metadataResult;
        }

        return TryWriteRegionsFromUefiDescriptors(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries.AsSpan(),
            destination);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries,
        Span<MemoryRegion> destination)
    {
        MemoryMapInfoRegionWriteResult metadataResult = ValidateRegionWriteMetadata(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries.Length,
            entriesAreMissing: false,
            destination.Length);

        if (!metadataResult.Succeeded)
        {
            return metadataResult;
        }

        if (destination.Length < descriptorCount)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.DestinationTooSmall,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity: descriptorCount,
                availableRegionCapacity: destination.Length,
                message: "Caller-owned memory region destination is too small.");
        }

        RegionSummaryResult summary = CalculateRegionSummary(
            descriptorCount,
            descriptorEntries);

        if (!summary.Succeeded)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                ToRegionWriteStatus(summary.Status),
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                summary.InvalidDescriptorIndex,
                descriptorCount,
                destination.Length,
                summary.TotalPages,
                summary.TotalBytes,
                summary.ConventionalMemoryPages,
                summary.ConventionalMemoryBytes,
                summary.Message);
        }

        WriteRegionsUnchecked(
            descriptorCount,
            descriptorEntries,
            destination);

        return MemoryMapInfoRegionWriteResult.Success(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorCount,
            descriptorCount,
            destination.Length,
            summary.TotalPages,
            summary.TotalBytes,
            summary.ConventionalMemoryPages,
            summary.ConventionalMemoryBytes);
    }

    private static MemoryMapInfoResult ValidateMetadata(
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int descriptorEntriesLength,
        bool entriesAreMissing)
    {
        if (descriptorSize < MinimumUefiDescriptorSize)
        {
            return Invalid(
                MemoryMapInfoStatus.InvalidDescriptorSize,
                -1,
                "UEFI memory map descriptor size is smaller than the required descriptor header.");
        }

        if (descriptorVersion == 0)
        {
            return Invalid(
                MemoryMapInfoStatus.InvalidDescriptorVersion,
                -1,
                "UEFI memory map descriptor version is required.");
        }

        if (descriptorCount <= 0)
        {
            return Invalid(
                MemoryMapInfoStatus.InvalidDescriptorCount,
                -1,
                "UEFI memory map descriptor count is required.");
        }

        if (entriesAreMissing)
        {
            return Invalid(
                MemoryMapInfoStatus.InvalidDescriptorEntries,
                -1,
                "UEFI memory map descriptor entries are required.");
        }

        if (descriptorEntriesLength != descriptorCount)
        {
            return Invalid(
                MemoryMapInfoStatus.InvalidDescriptorCount,
                -1,
                "UEFI memory map descriptor count does not match descriptor entries.");
        }

        return new MemoryMapInfoResult(
            true,
            MemoryMapInfoStatus.Success,
            default,
            -1,
            string.Empty);
    }

    private static MemoryMapInfoResult Build(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries)
    {
        RegionSummaryResult summary = CalculateRegionSummary(
            descriptorCount,
            descriptorEntries);

        if (!summary.Succeeded)
        {
            return Invalid(
                summary.Status,
                summary.InvalidDescriptorIndex,
                summary.Message);
        }

        MemoryRegion[] regions = new MemoryRegion[descriptorCount];
        WriteRegionsUnchecked(
            descriptorCount,
            descriptorEntries,
            regions);

        MemoryMapInfo info = MemoryMapInfo.CreateUnchecked(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            summary.TotalPages,
            summary.TotalBytes,
            summary.ConventionalMemoryPages,
            summary.ConventionalMemoryBytes,
            regions);

        return new MemoryMapInfoResult(
            true,
            MemoryMapInfoStatus.Success,
            info,
            -1,
            string.Empty);
    }

    private static MemoryMapInfoResult Invalid(
        MemoryMapInfoStatus status,
        int invalidDescriptorIndex,
        string message)
    {
        return new MemoryMapInfoResult(
            false,
            status,
            default,
            invalidDescriptorIndex,
            message);
    }

    private static MemoryMapInfoRegionWriteResult ValidateRegionWriteMetadata(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int descriptorEntriesLength,
        bool entriesAreMissing,
        int availableRegionCapacity)
    {
        int requiredRegionCapacity = descriptorCount > 0 ? descriptorCount : 0;

        if (descriptorSize < MinimumUefiDescriptorSize)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.InvalidDescriptorSize,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity,
                availableRegionCapacity,
                message: "UEFI memory map descriptor size is smaller than the required descriptor header.");
        }

        if (descriptorVersion == 0)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.InvalidDescriptorVersion,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity,
                availableRegionCapacity,
                message: "UEFI memory map descriptor version is required.");
        }

        if (descriptorCount <= 0)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.InvalidDescriptorCount,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity: 0,
                availableRegionCapacity,
                message: "UEFI memory map descriptor count is required.");
        }

        if (entriesAreMissing)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.InvalidDescriptorEntries,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity,
                availableRegionCapacity,
                message: "UEFI memory map descriptor entries are required.");
        }

        if (descriptorEntriesLength != descriptorCount)
        {
            return MemoryMapInfoRegionWriteResult.Fail(
                MemoryMapInfoRegionWriteStatus.InvalidDescriptorCount,
                mapKey,
                descriptorSize,
                descriptorVersion,
                descriptorCount,
                invalidDescriptorIndex: -1,
                requiredRegionCapacity,
                availableRegionCapacity,
                message: "UEFI memory map descriptor count does not match descriptor entries.");
        }

        return MemoryMapInfoRegionWriteResult.Success(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            regionCount: 0,
            requiredRegionCapacity,
            availableRegionCapacity,
            totalPages: 0,
            totalBytes: 0,
            conventionalMemoryPages: 0,
            conventionalMemoryBytes: 0);
    }

    private static RegionSummaryResult CalculateRegionSummary(
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries)
    {
        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong conventionalMemoryPages = 0;
        ulong conventionalMemoryBytes = 0;

        for (int descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            MemoryRegionResult regionResult = MemoryRegion.FromUefiDescriptor(descriptorEntries[descriptorIndex]);
            if (!regionResult.Succeeded)
            {
                return RegionSummaryResult.Invalid(
                    regionResult.Status,
                    descriptorIndex,
                    regionResult.Message,
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            MemoryRegion region = regionResult.Region;
            if (totalPages > ulong.MaxValue - region.PageCount)
            {
                return RegionSummaryResult.Invalid(
                    MemoryMapInfoStatus.TotalPageCountOverflow,
                    descriptorIndex,
                    "UEFI memory map total page count overflows.",
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            if (totalBytes > ulong.MaxValue - region.ByteLength)
            {
                return RegionSummaryResult.Invalid(
                    MemoryMapInfoStatus.TotalByteCountOverflow,
                    descriptorIndex,
                    "UEFI memory map total byte length overflows.",
                    totalPages,
                    totalBytes,
                    conventionalMemoryPages,
                    conventionalMemoryBytes);
            }

            totalPages += region.PageCount;
            totalBytes += region.ByteLength;

            if (region.Type == MemoryRegionType.ConventionalMemory)
            {
                conventionalMemoryPages += region.PageCount;
                conventionalMemoryBytes += region.ByteLength;
            }
        }

        return RegionSummaryResult.Valid(
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes);
    }

    private static void WriteRegionsUnchecked(
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries,
        Span<MemoryRegion> destination)
    {
        for (int descriptorIndex = 0; descriptorIndex < descriptorCount; descriptorIndex++)
        {
            MemoryRegionResult regionResult = MemoryRegion.FromUefiDescriptor(descriptorEntries[descriptorIndex]);
            destination[descriptorIndex] = regionResult.Region;
        }
    }

    private static MemoryMapInfoRegionWriteStatus ToRegionWriteStatus(MemoryMapInfoStatus status)
    {
        return status switch
        {
            MemoryMapInfoStatus.Success => MemoryMapInfoRegionWriteStatus.Success,
            MemoryMapInfoStatus.InvalidDescriptorSize => MemoryMapInfoRegionWriteStatus.InvalidDescriptorSize,
            MemoryMapInfoStatus.InvalidDescriptorVersion => MemoryMapInfoRegionWriteStatus.InvalidDescriptorVersion,
            MemoryMapInfoStatus.InvalidDescriptorCount => MemoryMapInfoRegionWriteStatus.InvalidDescriptorCount,
            MemoryMapInfoStatus.InvalidDescriptorEntries => MemoryMapInfoRegionWriteStatus.InvalidDescriptorEntries,
            MemoryMapInfoStatus.InvalidPageCount => MemoryMapInfoRegionWriteStatus.InvalidPageCount,
            MemoryMapInfoStatus.PageByteCountOverflow => MemoryMapInfoRegionWriteStatus.PageByteCountOverflow,
            MemoryMapInfoStatus.AddressRangeOverflow => MemoryMapInfoRegionWriteStatus.AddressRangeOverflow,
            MemoryMapInfoStatus.TotalPageCountOverflow => MemoryMapInfoRegionWriteStatus.TotalPageCountOverflow,
            MemoryMapInfoStatus.TotalByteCountOverflow => MemoryMapInfoRegionWriteStatus.TotalByteCountOverflow,
            _ => MemoryMapInfoRegionWriteStatus.Unknown
        };
    }

    private readonly struct RegionSummaryResult
    {
        private readonly string? _message;

        private RegionSummaryResult(
            bool succeeded,
            MemoryMapInfoStatus status,
            int invalidDescriptorIndex,
            ulong totalPages,
            ulong totalBytes,
            ulong conventionalMemoryPages,
            ulong conventionalMemoryBytes,
            string message)
        {
            Succeeded = succeeded;
            Status = status;
            InvalidDescriptorIndex = invalidDescriptorIndex;
            TotalPages = totalPages;
            TotalBytes = totalBytes;
            ConventionalMemoryPages = conventionalMemoryPages;
            ConventionalMemoryBytes = conventionalMemoryBytes;
            _message = message;
        }

        public bool Succeeded { get; }

        public MemoryMapInfoStatus Status { get; }

        public int InvalidDescriptorIndex { get; }

        public ulong TotalPages { get; }

        public ulong TotalBytes { get; }

        public ulong ConventionalMemoryPages { get; }

        public ulong ConventionalMemoryBytes { get; }

        public string Message => _message ?? string.Empty;

        public static RegionSummaryResult Valid(
            ulong totalPages,
            ulong totalBytes,
            ulong conventionalMemoryPages,
            ulong conventionalMemoryBytes)
        {
            return new RegionSummaryResult(
                true,
                MemoryMapInfoStatus.Success,
                invalidDescriptorIndex: -1,
                totalPages,
                totalBytes,
                conventionalMemoryPages,
                conventionalMemoryBytes,
                string.Empty);
        }

        public static RegionSummaryResult Invalid(
            MemoryMapInfoStatus status,
            int invalidDescriptorIndex,
            string message,
            ulong totalPages,
            ulong totalBytes,
            ulong conventionalMemoryPages,
            ulong conventionalMemoryBytes)
        {
            return new RegionSummaryResult(
                false,
                status,
                invalidDescriptorIndex,
                totalPages,
                totalBytes,
                conventionalMemoryPages,
                conventionalMemoryBytes,
                message);
        }
    }
}

public enum MemoryMapInfoRegionWriteStatus
{
    Unknown = 0,
    Success = 1,
    InvalidDescriptorSize = 2,
    InvalidDescriptorVersion = 3,
    InvalidDescriptorCount = 4,
    InvalidDescriptorEntries = 5,
    DestinationTooSmall = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    AddressRangeOverflow = 9,
    TotalPageCountOverflow = 10,
    TotalByteCountOverflow = 11
}

public readonly struct MemoryMapInfoRegionWriteResult
{
    private readonly string? _message;

    private MemoryMapInfoRegionWriteResult(
        MemoryMapInfoRegionWriteStatus status,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int regionCount,
        int requiredRegionCapacity,
        int availableRegionCapacity,
        int invalidDescriptorIndex,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes,
        string message)
    {
        Status = status;
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
        RegionCount = regionCount;
        RequiredRegionCapacity = requiredRegionCapacity;
        AvailableRegionCapacity = availableRegionCapacity;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalMemoryPages = conventionalMemoryPages;
        ConventionalMemoryBytes = conventionalMemoryBytes;
        _message = message;
    }

    public MemoryMapInfoRegionWriteStatus Status { get; }

    public bool Succeeded => Status == MemoryMapInfoRegionWriteStatus.Success;

    public ulong MapKey { get; }

    public ulong DescriptorSize { get; }

    public uint DescriptorVersion { get; }

    public int DescriptorCount { get; }

    public int RegionCount { get; }

    public int RequiredRegionCapacity { get; }

    public int AvailableRegionCapacity { get; }

    public int InvalidDescriptorIndex { get; }

    public ulong TotalPages { get; }

    public ulong TotalBytes { get; }

    public ulong ConventionalMemoryPages { get; }

    public ulong ConventionalMemoryBytes { get; }

    public string Message => _message ?? string.Empty;

    internal static MemoryMapInfoRegionWriteResult Success(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int regionCount,
        int requiredRegionCapacity,
        int availableRegionCapacity,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes)
    {
        return new MemoryMapInfoRegionWriteResult(
            MemoryMapInfoRegionWriteStatus.Success,
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            regionCount,
            requiredRegionCapacity,
            availableRegionCapacity,
            invalidDescriptorIndex: -1,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes,
            string.Empty);
    }

    internal static MemoryMapInfoRegionWriteResult Fail(
        MemoryMapInfoRegionWriteStatus status,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int invalidDescriptorIndex,
        int requiredRegionCapacity,
        int availableRegionCapacity,
        string message)
    {
        return Fail(
            status,
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            invalidDescriptorIndex,
            requiredRegionCapacity,
            availableRegionCapacity,
            totalPages: 0,
            totalBytes: 0,
            conventionalMemoryPages: 0,
            conventionalMemoryBytes: 0,
            message);
    }

    internal static MemoryMapInfoRegionWriteResult Fail(
        MemoryMapInfoRegionWriteStatus status,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        int invalidDescriptorIndex,
        int requiredRegionCapacity,
        int availableRegionCapacity,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes,
        string message)
    {
        return new MemoryMapInfoRegionWriteResult(
            status,
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            regionCount: 0,
            requiredRegionCapacity,
            availableRegionCapacity,
            invalidDescriptorIndex,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes,
            message);
    }
}
