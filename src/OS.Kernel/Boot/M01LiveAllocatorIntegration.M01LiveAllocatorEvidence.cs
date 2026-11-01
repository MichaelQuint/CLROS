namespace OS.Boot;
/// <summary>
/// Value-only audit evidence accumulated by the stage-two coordinator.
/// </summary>

public struct M01LiveAllocatorEvidence
{
    internal ulong StageOneStateAddressValue;
    internal ulong StageOneStateCapacityValue;
    internal ulong StaticStateAddressValue;
    internal ulong StaticStateCapacityValue;
    internal M01LiveAllocatorAllocationEvidence AllocationEvidenceValue;
    internal M01LiveAllocatorMapLedgerEvidence MapLedgerEvidenceValue;
    internal BootMemoryRegionRecord ArenaAllocationValue;
    internal M01LiveAllocatorArenaStatus ArenaStatusValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidationValue;
    internal ulong ArenaReservationIndexValue;
    internal OS.Kernel.Memory.BootReservedRange ArenaReservationValue;
    internal OS.Kernel.Memory.BootReservationSourceRangeStatus SourceRangeStatusValue;
    internal OS.Kernel.Memory.BootReservationSubtractionResult SubtractionValue;
    internal OS.Kernel.Memory.BootPageAllocatorStateMaterializationResult StateMaterializationValue;
    internal OS.Kernel.Memory.BootPageAllocatorOnePageResult AllocationValue;
    internal OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult FreeValue;
    internal OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult DoubleFreeValue;
    public ulong StageOneStateAddress => StageOneStateAddressValue;
    public ulong StageOneStateCapacity => StageOneStateCapacityValue;
    public ulong StaticStateAddress => StaticStateAddressValue;
    public ulong StaticStateCapacity => StaticStateCapacityValue;
    public M01LiveAllocatorAllocationEvidence AllocationEvidence => AllocationEvidenceValue;
    public M01LiveAllocatorMapLedgerEvidence MapLedgerEvidence => MapLedgerEvidenceValue;
    public BootMemoryRegionRecord ArenaAllocation => ArenaAllocationValue;
    public M01LiveAllocatorArenaStatus ArenaStatus => ArenaStatusValue;
    public OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidation => ReservationValidationValue;
    public ulong ArenaReservationIndex => ArenaReservationIndexValue;
    public OS.Kernel.Memory.BootReservedRange ArenaReservation => ArenaReservationValue;
    public OS.Kernel.Memory.BootReservationSourceRangeStatus SourceRangeStatus => SourceRangeStatusValue;
    public OS.Kernel.Memory.BootReservationSubtractionResult Subtraction => SubtractionValue;
    public OS.Kernel.Memory.BootPageAllocatorStateMaterializationResult StateMaterialization => StateMaterializationValue;
    public OS.Kernel.Memory.BootPageAllocatorOnePageResult Allocation => AllocationValue;
    public OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult Free => FreeValue;
    public OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult DoubleFree => DoubleFreeValue;
}
