namespace OS.Boot;
public static unsafe partial class M01LivePagingIntegration
{
    private struct ArenaPlanState
    {
        internal AddressValueStatus AddressStatus;
        internal OS.Kernel.Memory.BootReservationTableStatus ReservationStatus;
        internal OS.Kernel.Boot.X64BootIdentityMappingStatus MappingStatus;
        internal OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus;
        internal OS.Kernel.Memory.X64PageTableEncodingPreconditions PageTablePreconditions;
        internal BootMemoryRegionRecord AllocationRecord;
        internal OS.Kernel.Boot.X64BootIdentityMappingPlanResult MappingPlan;
        internal PhysicalAddress CandidatePhysicalBase;
        internal PhysicalAddress RootTable;
        internal PhysicalAddress StatePage;
        internal PhysicalAddress ArenaEndExclusive;
        internal ulong IterationCount;
        internal ulong IterationLimit;
        internal ulong TablePageCount;
        internal ulong TotalPageCount;
        internal ulong TableByteLength;
        internal ulong TotalByteLength;
        internal ulong AvailablePageCountFromCandidate;
        internal ulong RequiredRangeWorkspaceCount;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement BootStackGuardRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement CodeAndCallGateRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement OwnedStackRequirement;
        internal OS.Kernel.Boot.X64BootIdentityMappingRequirement ProtectionProbeRequirement;
    }
}
