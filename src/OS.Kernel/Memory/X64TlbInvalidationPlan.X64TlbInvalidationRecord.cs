namespace OS.Kernel.Memory;
/// <summary>
/// One exact target for a future checked INVLPG invocation.
/// </summary>

public readonly struct X64TlbInvalidationRecord
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _target;
    private readonly ulong _ordinal;
    internal X64TlbInvalidationRecord(OS.Boot.VirtualAddress target, ulong ordinal)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _target = target;
        _ordinal = ordinal;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public OS.Boot.VirtualAddress Target => _target;
    public ulong Ordinal => _ordinal;

    public X64TlbInvalidationStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        if (!_target.IsValid || !_target.IsAligned(X64TlbInvalidationPlan.PageSize))
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        if (_target.TryGetRawValueForHardwareRegisterBoundary(out ulong rawTarget) != OS.Boot.AddressValueStatus.Success || rawTarget == 0 || rawTarget >= X64TlbInvalidationPlan.LowCanonicalEndExclusive)
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        return X64TlbInvalidationStatus.Success;
    }
}
