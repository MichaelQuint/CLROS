namespace OS.Boot;
/// <summary>
/// Value-only M01-029 evidence. It deliberately omits transition runtime
/// state, CR3 events, protection observations/events, and CR3 policy. The
/// included leaf results describe preparation and caller-owned model
/// mutation only.
/// </summary>

public struct M01LivePagingPreparationEvidence
{
    internal M01LivePagingAllocationEvidence AllocationEvidenceValue;
    internal M01LivePagingMapLedgerEvidence MapLedgerEvidenceValue;
    internal OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidationValue;
    internal ulong Cr3CodeReservationIndexValue;
    internal ulong ProtectionProbeReservationIndexValue;
    internal ulong TableStateReservationIndexValue;
    internal OS.Kernel.Boot.X64BootIdentityMappingPlanResult MappingPlanValue;
    internal M01LivePagingTableBuildResult TableBuildValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult PreflightMaterializationValue;
    internal OS.Kernel.Memory.X64PagingModePreflightStubResult PreflightReadbackValue;
    internal OS.Kernel.Memory.X64PagingModePreflightResult PreflightValue;
    internal OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlanResult TransitionMaterializationValue;
    internal OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlanResult TransitionReadbackValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgMaterializationValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadbackValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeMaterializationValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeReadbackValue;
    internal M01LivePagingProtectionPreparation ProtectionPreparationValue;
    internal OS.Kernel.Memory.PageZeroMappingPolicyResult PageZeroAuditValue;
    internal M01LivePagingCleanupPlan CleanupPlanValue;
    public M01LivePagingAllocationEvidence AllocationEvidence => AllocationEvidenceValue;
    public M01LivePagingMapLedgerEvidence MapLedgerEvidence => MapLedgerEvidenceValue;
    public OS.Kernel.Memory.BootReservationTableValidationResult ReservationValidation => ReservationValidationValue;
    public ulong Cr3CodeReservationIndex => Cr3CodeReservationIndexValue;
    public ulong ProtectionProbeReservationIndex => ProtectionProbeReservationIndexValue;
    public ulong TableStateReservationIndex => TableStateReservationIndexValue;
    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult MappingPlan => MappingPlanValue;
    public M01LivePagingTableBuildResult TableBuild => TableBuildValue;
    public OS.Kernel.Memory.X64PagingModePreflightStubResult PreflightMaterialization => PreflightMaterializationValue;
    public OS.Kernel.Memory.X64PagingModePreflightStubResult PreflightReadback => PreflightReadbackValue;
    public OS.Kernel.Memory.X64PagingModePreflightResult Preflight => PreflightValue;
    public OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlanResult TransitionMaterialization => TransitionMaterializationValue;
    public OS.Kernel.Memory.X64ReversibleCr3TransitionStubPlanResult TransitionReadback => TransitionReadbackValue;
    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgMaterialization => InvlpgMaterializationValue;
    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadback => InvlpgReadbackValue;
    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeMaterialization => ProbeMaterializationValue;
    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeReadback => ProbeReadbackValue;
    public M01LivePagingProtectionPreparation ProtectionPreparation => ProtectionPreparationValue;
    public OS.Kernel.Memory.X64PageMapResult ProtectionMutation => ProtectionPreparationValue.Mutation;
    public OS.Kernel.Memory.X64InvlpgInvocationPlanResult InvlpgInvocation => ProtectionPreparationValue.InvlpgInvocation;
    public OS.Kernel.Memory.PageZeroMappingPolicyResult PageZeroAudit => PageZeroAuditValue;
    public M01LivePagingCleanupPlan CleanupPlan => CleanupPlanValue;
    public bool Succeeded => ReservationValidationValue.Succeeded && MappingPlanValue.Succeeded && TableBuildValue.Succeeded && PreflightMaterializationValue.Succeeded && PreflightReadbackValue.Succeeded && PreflightReadbackValue.ExactBytes && PreflightValue.Succeeded && TransitionMaterializationValue.Succeeded && TransitionReadbackValue.Succeeded && InvlpgMaterializationValue.Succeeded && InvlpgReadbackValue.ReadBackVerified && ProbeMaterializationValue.Succeeded && ProbeReadbackValue.ReadBackVerified && ProtectionPreparationValue.Succeeded && PageZeroAuditValue.ProvesPageZeroAbsentInSuppliedImage && CleanupPlanValue.Succeeded;
}
