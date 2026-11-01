namespace OS.Kernel.Memory;
/// <summary>
/// Immutable value evidence for one exact page-aligned virtual range whose
/// translations must be invalidated after a successful page-table mutation.
/// It does not perform an architectural invalidation.
/// </summary>

public readonly struct X64TlbInvalidationRange
{
    public const ulong StructureSize = 40;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _byteLength;
    private readonly ulong _pageCount;
    internal X64TlbInvalidationRange(OS.Boot.VirtualAddress virtualStart, OS.Boot.VirtualAddress virtualEndExclusive, ulong byteLength, ulong pageCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _byteLength = byteLength;
        _pageCount = pageCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public OS.Boot.VirtualAddress VirtualStart => _virtualStart;
    public OS.Boot.VirtualAddress VirtualEndExclusive => _virtualEndExclusive;
    public ulong ByteLength => _byteLength;
    public ulong PageCount => _pageCount;

    public X64TlbInvalidationStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64TlbInvalidationStatus.InvalidPlanState;
        }

        return X64TlbInvalidationPlan.ValidateRangeFields(_virtualStart, _virtualEndExclusive, _byteLength, _pageCount);
    }
}
