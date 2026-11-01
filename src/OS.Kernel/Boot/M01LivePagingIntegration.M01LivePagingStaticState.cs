namespace OS.Boot;
/// <summary>
/// Durable caller-owned state written only after every stage-three host audit
/// succeeds. Its contents remain value-only and no pointer is retained.
/// </summary>

public unsafe struct M01LivePagingStaticState
{
    public const uint CurrentVersion = 1;
    public const ulong RequiredStorageAlignment = 8;
    internal uint HeaderSize;
    internal uint HeaderVersion;
    internal M01LivePagingStatus CoordinatorStatus;
    internal M01LivePagingCheckpoint CoordinatorCheckpoint;
    internal byte Reserved0;
    internal byte Reserved1;
    internal byte Reserved2;
    internal byte Reserved3;
    internal byte Reserved4;
    internal byte Reserved5;
    internal ulong StageTwoStateAddressValue;
    internal ulong StageTwoStateCapacityValue;
    internal ulong RefreshedMapKeyValue;
    internal ulong RefreshedMapGenerationValue;
    internal OS.Kernel.Memory.BootReservedRange Cr3CodeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange ProtectionProbeReservationValue;
    internal OS.Kernel.Memory.BootReservedRange TableStateReservationValue;
    internal PhysicalAddress OwnedRootValue;
    internal ulong RawFirmwareCr3Value;
    internal ulong RawCr4Value;
    internal ulong OriginalRflagsValue;
    internal ulong FinalCanaryValue;
    internal ulong Cr3ElapsedTicksValue;
    internal ulong Cr3EventCountValue;
    internal ulong ProtectionEventCountValue;
    internal ulong TablePageCountValue;
    internal ulong MappedPageCountValue;
    internal byte PageZeroAbsentValue;
    internal byte Reserved6;
    internal ushort Reserved7;
    internal uint Reserved8;
    public static ulong RequiredStorageBytes => (ulong)sizeof(M01LivePagingStaticState);
    public uint Size => HeaderSize;
    public uint Version => HeaderVersion;
    public M01LivePagingStatus Status => CoordinatorStatus;
    public M01LivePagingCheckpoint Checkpoint => CoordinatorCheckpoint;
    public ulong StageTwoStateAddress => StageTwoStateAddressValue;
    public ulong StageTwoStateCapacity => StageTwoStateCapacityValue;
    public ulong RefreshedMapKey => RefreshedMapKeyValue;
    public ulong RefreshedMapGeneration => RefreshedMapGenerationValue;
    public OS.Kernel.Memory.BootReservedRange Cr3CodeReservation => Cr3CodeReservationValue;
    public OS.Kernel.Memory.BootReservedRange ProtectionProbeReservation => ProtectionProbeReservationValue;
    public OS.Kernel.Memory.BootReservedRange TableStateReservation => TableStateReservationValue;
    public PhysicalAddress OwnedRoot => OwnedRootValue;
    public ulong RawFirmwareCr3 => RawFirmwareCr3Value;
    public ulong RawCr4 => RawCr4Value;
    public ulong OriginalRflags => OriginalRflagsValue;
    public ulong FinalCanary => FinalCanaryValue;
    public ulong Cr3ElapsedTicks => Cr3ElapsedTicksValue;
    public ulong Cr3EventCount => Cr3EventCountValue;
    public ulong ProtectionEventCount => ProtectionEventCountValue;
    public ulong TablePageCount => TablePageCountValue;
    public ulong MappedPageCount => MappedPageCountValue;
    public bool PageZeroAbsent => PageZeroAbsentValue == 1;
    public bool IsCompleted => M01LivePagingIntegration.IsCompletedState(this);
}
