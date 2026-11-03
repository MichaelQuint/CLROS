namespace OS.Storage;

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

    public GptPartitionSelectorStatus Status
    {
        get;
    }

    public GptPartitionSelectorKind SelectorKind
    {
        get;
    }

    public GptPartitionTableStatus ParseStatus
    {
        get;
    }

    public GptPartitionDescriptor Descriptor
    {
        get;
    }

    public int DescriptorSpanLength
    {
        get;
    }

    public int PartitionsScanned
    {
        get;
    }

    public int SelectedDescriptorIndex
    {
        get;
    }

    public int MatchCount
    {
        get;
    }

    public int DuplicateCount
    {
        get;
    }

    public int FailingDescriptorIndex
    {
        get;
    }

    public Guid RequestedGuid
    {
        get;
    }

    public uint Flags
    {
        get;
    }

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
