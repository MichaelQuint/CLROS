namespace OS.Storage;

public enum GptPartitionSelectorStatus
{
    Success = 0,
    ParseResultNotInitialized = 1,
    ParseFailed = 2,
    DescriptorSpanEmpty = 3,
    DescriptorCountMismatch = 4,
    MissingGuid = 5,
    NotFound = 6,
    InvalidDescriptorMetadata = 7
}

public enum GptPartitionSelectorKind
{
    Unknown = 0,
    PartitionTypeGuid = 1,
    UniquePartitionGuid = 2
}

public readonly struct GptPartitionSelectorResult
{
    private const uint InitializedFlag = 1;

    private GptPartitionSelectorResult(
        GptPartitionSelectorStatus status,
        GptPartitionSelectorKind selectorKind,
        GptPartitionTableStatus parseStatus,
        GptPartitionDescriptor descriptor,
        int descriptorSpanLength,
        int partitionsScanned,
        int selectedDescriptorIndex,
        int matchCount,
        int duplicateCount,
        int failingDescriptorIndex,
        Guid requestedGuid,
        uint flags)
    {
        Status = status;
        SelectorKind = selectorKind;
        ParseStatus = parseStatus;
        Descriptor = descriptor;
        DescriptorSpanLength = descriptorSpanLength;
        PartitionsScanned = partitionsScanned;
        SelectedDescriptorIndex = selectedDescriptorIndex;
        MatchCount = matchCount;
        DuplicateCount = duplicateCount;
        FailingDescriptorIndex = failingDescriptorIndex;
        RequestedGuid = requestedGuid;
        Flags = flags;
    }

    public GptPartitionSelectorStatus Status { get; }

    public GptPartitionSelectorKind SelectorKind { get; }

    public GptPartitionTableStatus ParseStatus { get; }

    public GptPartitionDescriptor Descriptor { get; }

    public int DescriptorSpanLength { get; }

    public int PartitionsScanned { get; }

    public int SelectedDescriptorIndex { get; }

    public int MatchCount { get; }

    public int DuplicateCount { get; }

    public int FailingDescriptorIndex { get; }

    public Guid RequestedGuid { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == GptPartitionSelectorStatus.Success &&
        MatchCount > 0 &&
        SelectedDescriptorIndex >= 0 &&
        PartitionsScanned > 0 &&
        Descriptor.FirstLba <= Descriptor.LastLba;

    public uint SelectedEntryIndex => Descriptor.EntryIndex;

    public int SelectedEntryByteOffset => Descriptor.EntryByteOffset;

    public ulong FirstLba => Descriptor.FirstLba;

    public ulong LastLba => Descriptor.LastLba;

    public ulong BlockCount => Succeeded ? Descriptor.LastLba - Descriptor.FirstLba + 1UL : 0UL;

    public int NameByteOffset => Descriptor.NameByteOffset;

    public int NameByteLength => Succeeded ? Descriptor.NameByteLength : 0;

    public int NameUtf16CodeUnitLength => Descriptor.NameUtf16CodeUnitLength;

    public static GptPartitionSelectorResult Success(
        GptPartitionSelectorKind selectorKind,
        GptPartitionTableStatus parseStatus,
        GptPartitionDescriptor descriptor,
        int descriptorSpanLength,
        int partitionsScanned,
        int selectedDescriptorIndex,
        int matchCount,
        Guid requestedGuid)
    {
        return new GptPartitionSelectorResult(
            GptPartitionSelectorStatus.Success,
            selectorKind,
            parseStatus,
            descriptor,
            descriptorSpanLength,
            partitionsScanned,
            selectedDescriptorIndex,
            matchCount,
            matchCount > 0 ? matchCount - 1 : 0,
            failingDescriptorIndex: -1,
            requestedGuid,
            InitializedFlag);
    }

    public static GptPartitionSelectorResult Fail(
        GptPartitionSelectorStatus status,
        GptPartitionSelectorKind selectorKind,
        GptPartitionTableStatus parseStatus,
        int descriptorSpanLength,
        int partitionsScanned,
        Guid requestedGuid,
        int matchCount = 0,
        int failingDescriptorIndex = -1)
    {
        return new GptPartitionSelectorResult(
            status,
            selectorKind,
            parseStatus,
            default,
            descriptorSpanLength,
            partitionsScanned,
            selectedDescriptorIndex: -1,
            matchCount,
            duplicateCount: matchCount > 0 ? matchCount - 1 : 0,
            failingDescriptorIndex,
            requestedGuid,
            InitializedFlag);
    }
}

public static class GptPartitionSelector
{
    public const bool UsesCallerOwnedDescriptors = true;
    public const bool ReparsesGptBytes = false;
    public const bool AllocatesManagedStorage = false;
    public const bool ReadsBlockDevice = false;
    public const bool LoadsPackage = false;

    public static GptPartitionSelectorStatus SelectByPartitionTypeGuid(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid partitionTypeGuid,
        out GptPartitionSelectorResult result)
    {
        return Select(
            parse,
            descriptors,
            partitionTypeGuid,
            GptPartitionSelectorKind.PartitionTypeGuid,
            out result);
    }

    public static GptPartitionSelectorStatus SelectByUniquePartitionGuid(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid uniquePartitionGuid,
        out GptPartitionSelectorResult result)
    {
        return Select(
            parse,
            descriptors,
            uniquePartitionGuid,
            GptPartitionSelectorKind.UniquePartitionGuid,
            out result);
    }

    private static GptPartitionSelectorStatus Select(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid requestedGuid,
        GptPartitionSelectorKind selectorKind,
        out GptPartitionSelectorResult result)
    {
        result = default;

        if (!parse.IsInitialized)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.ParseResultNotInitialized,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (!parse.Succeeded)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.ParseFailed,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (requestedGuid == Guid.Empty)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.MissingGuid,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (parse.PartitionsWritten <= 0 || descriptors.Length == 0)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.DescriptorSpanEmpty,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (parse.PartitionsWritten > descriptors.Length)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.DescriptorCountMismatch,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        int selectedIndex = -1;
        int matchCount = 0;
        GptPartitionDescriptor selected = default;
        for (int index = 0; index < parse.PartitionsWritten; index++)
        {
            GptPartitionDescriptor descriptor = descriptors[index];
            if (!IsDescriptorMetadataValid(parse.Header, descriptor))
            {
                result = GptPartitionSelectorResult.Fail(
                    GptPartitionSelectorStatus.InvalidDescriptorMetadata,
                    selectorKind,
                    parse.Status,
                    descriptors.Length,
                    index + 1,
                    requestedGuid,
                    matchCount,
                    index);
                return result.Status;
            }

            if (!Matches(descriptor, requestedGuid, selectorKind))
            {
                continue;
            }

            matchCount++;
            if (selectedIndex < 0)
            {
                selectedIndex = index;
                selected = descriptor;
            }
        }

        if (selectedIndex < 0)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.NotFound,
                selectorKind,
                parse.Status,
                descriptors.Length,
                parse.PartitionsWritten,
                requestedGuid);
            return result.Status;
        }

        result = GptPartitionSelectorResult.Success(
            selectorKind,
            parse.Status,
            selected,
            descriptors.Length,
            parse.PartitionsWritten,
            selectedIndex,
            matchCount,
            requestedGuid);
        return result.Status;
    }

    private static bool Matches(
        GptPartitionDescriptor descriptor,
        Guid requestedGuid,
        GptPartitionSelectorKind selectorKind)
    {
        return selectorKind == GptPartitionSelectorKind.PartitionTypeGuid
            ? descriptor.PartitionTypeGuid == requestedGuid
            : descriptor.UniquePartitionGuid == requestedGuid;
    }

    private static bool IsDescriptorMetadataValid(
        GptHeader header,
        GptPartitionDescriptor descriptor)
    {
        if (descriptor.EntryIndex >= header.PartitionEntryCount ||
            descriptor.EntryByteOffset < 0 ||
            descriptor.FirstLba < header.FirstUsableLba ||
            descriptor.LastLba > header.LastUsableLba ||
            descriptor.FirstLba > descriptor.LastLba ||
            descriptor.PartitionTypeGuid == Guid.Empty ||
            descriptor.UniquePartitionGuid == Guid.Empty)
        {
            return false;
        }

        int expectedNameOffset =
            descriptor.EntryByteOffset + GptPartitionDescriptor.NameOffsetWithinEntryBytes;
        return descriptor.NameByteOffset == expectedNameOffset &&
            descriptor.NameUtf16CodeUnitLength >= 0 &&
            descriptor.NameUtf16CodeUnitLength <= GptPartitionDescriptor.MaximumNameUtf16CodeUnits &&
            descriptor.NameByteLength == GptPartitionDescriptor.NameFieldLengthBytes;
    }
}
