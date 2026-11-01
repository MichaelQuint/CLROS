namespace OS.Kernel.Memory;
/// <summary>
/// Status-coded materialization evidence. The zero/default value is never
/// successful.
/// </summary>

public readonly struct X64TlbInvalidationPlanResult
{
    public const ulong StructureSize = 88;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64TlbInvalidationStatus _status;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly X64TlbInvalidationRange _range;
    private readonly ulong _destinationCapacity;
    private readonly ulong _requiredRecordCount;
    private readonly ulong _writtenRecordCount;
    private readonly ulong _invalidRecordIndex;
    private X64TlbInvalidationPlanResult(X64TlbInvalidationStatus status, X64TlbInvalidationRange range, ulong destinationCapacity, ulong requiredRecordCount, ulong writtenRecordCount, ulong invalidRecordIndex)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _range = range;
        _destinationCapacity = destinationCapacity;
        _requiredRecordCount = requiredRecordCount;
        _writtenRecordCount = writtenRecordCount;
        _invalidRecordIndex = invalidRecordIndex;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0;
    public X64TlbInvalidationStatus Status => _status;
    public X64TlbInvalidationRange Range => _range;
    public ulong DestinationCapacity => _destinationCapacity;
    public ulong RequiredRecordCount => _requiredRecordCount;
    public ulong WrittenRecordCount => _writtenRecordCount;
    public ulong InvalidRecordIndex => _invalidRecordIndex;
    public bool Succeeded => IsInitialized && _status == X64TlbInvalidationStatus.Success && _range.Validate() == X64TlbInvalidationStatus.Success && _requiredRecordCount == _range.PageCount && _writtenRecordCount == _requiredRecordCount && _destinationCapacity >= _requiredRecordCount && _destinationCapacity <= X64TlbInvalidationPlan.MaximumPageCount && _invalidRecordIndex == NoIndex;

    internal static X64TlbInvalidationPlanResult Success(X64TlbInvalidationRange range, ulong destinationCapacity)
    {
        return new X64TlbInvalidationPlanResult(X64TlbInvalidationStatus.Success, range, destinationCapacity, range.PageCount, range.PageCount, NoIndex);
    }

    internal static X64TlbInvalidationPlanResult Fail(X64TlbInvalidationStatus status, X64TlbInvalidationRange range = default, ulong destinationCapacity = 0, ulong requiredRecordCount = 0, ulong invalidRecordIndex = NoIndex)
    {
        return new X64TlbInvalidationPlanResult(status, range, destinationCapacity, requiredRecordCount, 0, invalidRecordIndex);
    }
}
