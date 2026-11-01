namespace OS.Kernel.Boot;
/// <summary>
/// Complete value-only outcome of one bounded planning attempt.
/// </summary>

public readonly struct X64BootIdentityMappingPlanResult
{
    public const ulong StructureSize = 176;
    public const ulong NoIndex = ~0UL;
    private readonly byte _isInitialized;
    private readonly X64BootIdentityMappingStatus _status;
    private readonly OS.Boot.AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus _pageTableEntryStatus;
    private readonly uint _reserved;
    private readonly ulong _sourceCount;
    private readonly ulong _validatedSourceCount;
    private readonly ulong _sourceCapacity;
    private readonly ulong _destinationCapacity;
    private readonly ulong _requiredDestinationCapacity;
    private readonly ulong _outputRangeCount;
    private readonly ulong _availableTablePageCapacity;
    private readonly ulong _requiredPurposeMask;
    private readonly ulong _observedPurposeMask;
    private readonly ulong _missingPurposeMask;
    private readonly ulong _invalidSourceIndex;
    private readonly ulong _conflictingSourceIndex;
    private readonly ulong _guardSourceIndex;
    private readonly ulong _unionPageCount;
    private readonly ulong _unionByteLength;
    private readonly ulong _pml4PageCount;
    private readonly ulong _pdptPageCount;
    private readonly ulong _pdPageCount;
    private readonly ulong _ptPageCount;
    private readonly ulong _totalTablePageCount;
    private readonly ulong _totalTableByteLength;
    internal X64BootIdentityMappingPlanResult(X64BootIdentityMappingStatus status, OS.Boot.AddressValueStatus addressStatus, OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus, ulong sourceCount, ulong validatedSourceCount, ulong sourceCapacity, ulong destinationCapacity, ulong requiredDestinationCapacity, ulong outputRangeCount, ulong availableTablePageCapacity, ulong requiredPurposeMask, ulong observedPurposeMask, ulong missingPurposeMask, ulong invalidSourceIndex, ulong conflictingSourceIndex, ulong guardSourceIndex, ulong unionPageCount, ulong unionByteLength, ulong pml4PageCount, ulong pdptPageCount, ulong pdPageCount, ulong ptPageCount, ulong totalTablePageCount, ulong totalTableByteLength)
    {
        _isInitialized = 1;
        _status = status;
        _addressStatus = addressStatus;
        _pageTableEntryStatus = pageTableEntryStatus;
        _reserved = 0;
        _sourceCount = sourceCount;
        _validatedSourceCount = validatedSourceCount;
        _sourceCapacity = sourceCapacity;
        _destinationCapacity = destinationCapacity;
        _requiredDestinationCapacity = requiredDestinationCapacity;
        _outputRangeCount = outputRangeCount;
        _availableTablePageCapacity = availableTablePageCapacity;
        _requiredPurposeMask = requiredPurposeMask;
        _observedPurposeMask = observedPurposeMask;
        _missingPurposeMask = missingPurposeMask;
        _invalidSourceIndex = invalidSourceIndex;
        _conflictingSourceIndex = conflictingSourceIndex;
        _guardSourceIndex = guardSourceIndex;
        _unionPageCount = unionPageCount;
        _unionByteLength = unionByteLength;
        _pml4PageCount = pml4PageCount;
        _pdptPageCount = pdptPageCount;
        _pdPageCount = pdPageCount;
        _ptPageCount = ptPageCount;
        _totalTablePageCount = totalTablePageCount;
        _totalTableByteLength = totalTableByteLength;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved == 0;
    public X64BootIdentityMappingStatus Status => _status;
    public bool Succeeded => IsInitialized && _status == X64BootIdentityMappingStatus.Success;
    public OS.Boot.AddressValueStatus AddressStatus => _addressStatus;
    public OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus => _pageTableEntryStatus;
    public ulong SourceCount => _sourceCount;
    public ulong ValidatedSourceCount => _validatedSourceCount;
    public ulong SourceCapacity => _sourceCapacity;
    public ulong DestinationCapacity => _destinationCapacity;
    public ulong RequiredDestinationCapacity => _requiredDestinationCapacity;
    public ulong OutputRangeCount => _outputRangeCount;
    public ulong AvailableTablePageCapacity => _availableTablePageCapacity;
    public ulong RequiredPurposeMask => _requiredPurposeMask;
    public ulong ObservedPurposeMask => _observedPurposeMask;
    public ulong MissingPurposeMask => _missingPurposeMask;
    public ulong InvalidSourceIndex => _invalidSourceIndex;
    public ulong ConflictingSourceIndex => _conflictingSourceIndex;
    public ulong GuardSourceIndex => _guardSourceIndex;
    public ulong UnionPageCount => _unionPageCount;
    public ulong UnionByteLength => _unionByteLength;
    public ulong Pml4PageCount => _pml4PageCount;
    public ulong PdptPageCount => _pdptPageCount;
    public ulong PdPageCount => _pdPageCount;
    public ulong PtPageCount => _ptPageCount;
    public ulong TotalTablePageCount => _totalTablePageCount;
    public ulong TotalTableByteLength => _totalTableByteLength;
    internal bool HasCanonicalState => IsInitialized;
}
