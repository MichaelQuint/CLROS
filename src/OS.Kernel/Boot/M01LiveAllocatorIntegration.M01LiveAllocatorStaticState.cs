namespace OS.Boot;
/// <summary>
/// Caller-owned durable state produced only after the complete allocate,
/// containment, free, and double-free-rejection cycle succeeds.
/// </summary>

public struct M01LiveAllocatorStaticState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;
    public const ulong RequiredStorageBytes = 312;
    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LiveAllocatorStatus CoordinatorStatus;
    internal M01LiveAllocatorCheckpoint CoordinatorCheckpoint;
    internal byte Reserved0;
    internal byte Reserved1;
    internal byte Reserved2;
    internal byte Reserved3;
    internal byte Reserved4;
    internal byte Reserved5;
    internal OS.Kernel.Memory.BootPageAllocatorMutationStatus DoubleFreeStatusValue;
    internal uint ReservedWord;
    internal ulong StageOneStateAddressValue;
    internal ulong StageOneStateCapacityValue;
    internal ulong RefreshedMapKeyValue;
    internal ulong RefreshedMapGenerationValue;
    internal OS.Kernel.Memory.BootReservedRange ArenaReservationValue;
    internal OS.Kernel.Memory.BootReservationSourceRange ArenaSourceRangeValue;
    internal OS.Kernel.Memory.BootReservationSourceRange FreeRangeValue;
    internal OS.Kernel.Memory.BootPageFreeListNode FreeNodeValue;
    internal OS.Kernel.Memory.BootPageAllocatorState AllocatorStateValue;
    internal PhysicalAddress AllocatedPageStartValue;
    internal PhysicalAddress AllocatedPageEndExclusiveValue;
    internal ulong InitialFreePagesValue;
    internal ulong InitialFreeBytesValue;
    public uint Size => HeaderSize;
    public uint Version => HeaderVersion;
    public M01LiveAllocatorStatus Status => CoordinatorStatus;
    public M01LiveAllocatorCheckpoint Checkpoint => CoordinatorCheckpoint;
    public OS.Kernel.Memory.BootPageAllocatorMutationStatus DoubleFreeStatus => DoubleFreeStatusValue;
    public ulong StageOneStateAddress => StageOneStateAddressValue;
    public ulong StageOneStateCapacity => StageOneStateCapacityValue;
    public ulong RefreshedMapKey => RefreshedMapKeyValue;
    public ulong RefreshedMapGeneration => RefreshedMapGenerationValue;
    public OS.Kernel.Memory.BootReservedRange ArenaReservation => ArenaReservationValue;
    public OS.Kernel.Memory.BootReservationSourceRange ArenaSourceRange => ArenaSourceRangeValue;
    public OS.Kernel.Memory.BootReservationSourceRange FreeRange => FreeRangeValue;
    public OS.Kernel.Memory.BootPageFreeListNode FreeNode => FreeNodeValue;
    public OS.Kernel.Memory.BootPageAllocatorState AllocatorState => AllocatorStateValue;
    public PhysicalAddress AllocatedPageStart => AllocatedPageStartValue;
    public PhysicalAddress AllocatedPageEndExclusive => AllocatedPageEndExclusiveValue;
    public ulong InitialFreePages => InitialFreePagesValue;
    public ulong InitialFreeBytes => InitialFreeBytesValue;
    public bool IsCompleted => M01LiveAllocatorIntegration.IsCompletedState(this);
}
