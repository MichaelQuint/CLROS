namespace OS.Boot;
/// <summary>
/// Cross-binds one runtime-dereferenced range to existing allocation,
/// reservation, and normalized identity-mapping ledgers.
/// </summary>

public readonly struct M01OwnedCr3CompositeTransitionRangeBinding
{
    public const ulong StructureSize = 80;
    public const ulong NoIndex = ~0UL;
    private readonly M01OwnedCr3CompositeTransitionRangeRole _role;
    private readonly M01OwnedCr3CompositeTransitionRangeProvenance _provenance;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly PhysicalAddress _physicalStart;
    private readonly PhysicalAddress _physicalEndExclusive;
    private readonly VirtualAddress _virtualStart;
    private readonly VirtualAddress _virtualEndExclusive;
    private readonly ulong _allocationIndex;
    private readonly ulong _reservationIndex;
    private readonly ulong _identityMappingIndex;
    private readonly ulong _reserved2;
    private readonly ulong _reserved3;
    public M01OwnedCr3CompositeTransitionRangeBinding(M01OwnedCr3CompositeTransitionRangeRole role, M01OwnedCr3CompositeTransitionRangeProvenance provenance, PhysicalAddress physicalStart, PhysicalAddress physicalEndExclusive, VirtualAddress virtualStart, VirtualAddress virtualEndExclusive, ulong allocationIndex, ulong reservationIndex, ulong identityMappingIndex)
    {
        _role = role;
        _provenance = provenance;
        _reserved0 = 0;
        _reserved1 = 0;
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _allocationIndex = allocationIndex;
        _reservationIndex = reservationIndex;
        _identityMappingIndex = identityMappingIndex;
        _reserved2 = 0;
        _reserved3 = 0;
    }

    public M01OwnedCr3CompositeTransitionRangeRole Role => _role;
    public M01OwnedCr3CompositeTransitionRangeProvenance Provenance => _provenance;
    public PhysicalAddress PhysicalStart => _physicalStart;
    public PhysicalAddress PhysicalEndExclusive => _physicalEndExclusive;
    public VirtualAddress VirtualStart => _virtualStart;
    public VirtualAddress VirtualEndExclusive => _virtualEndExclusive;
    public ulong AllocationIndex => _allocationIndex;
    public ulong ReservationIndex => _reservationIndex;
    public ulong IdentityMappingIndex => _identityMappingIndex;
    internal bool ReservedFieldsAreZero => _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0;
}
