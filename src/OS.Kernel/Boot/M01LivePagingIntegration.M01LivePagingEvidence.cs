namespace OS.Boot;
/// <summary>
/// Value-only leaf results retained for diagnosis. Pointer inputs are never
/// persisted in this record.
/// </summary>

public struct M01LivePagingEvidence
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
    internal OS.Kernel.Memory.X64ReversibleCr3TransitionEvidenceResult TransitionEvidenceValue;
    internal OS.Kernel.Memory.X64Cr3LoadStubPlanResult Cr3PlanValue;
    internal OS.Kernel.Memory.X64Cr3ProbeAuditResult Cr3AuditValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgMaterializationValue;
    internal OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadbackValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeMaterializationValue;
    internal OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeReadbackValue;
    internal OS.Kernel.Memory.X64PageMapResult ProtectionMutationValue;
    internal OS.Kernel.Memory.X64ProtectionProbePlanResult ProtectionPlanValue;
    internal OS.Kernel.Memory.PageZeroMappingPolicyResult PageZeroAuditValue;
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
    public OS.Kernel.Memory.X64ReversibleCr3TransitionEvidenceResult TransitionEvidence => TransitionEvidenceValue;
    public OS.Kernel.Memory.X64Cr3LoadStubPlanResult Cr3Plan => Cr3PlanValue;
    public OS.Kernel.Memory.X64Cr3ProbeAuditResult Cr3Audit => Cr3AuditValue;
    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgMaterialization => InvlpgMaterializationValue;
    public OS.Kernel.Memory.X64InvlpgStubPlanResult InvlpgReadback => InvlpgReadbackValue;
    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeMaterialization => ProbeMaterializationValue;
    public OS.Kernel.Memory.X64ProtectionProbeStubPlanResult ProbeReadback => ProbeReadbackValue;
    public OS.Kernel.Memory.X64PageMapResult ProtectionMutation => ProtectionMutationValue;
    public OS.Kernel.Memory.X64ProtectionProbePlanResult ProtectionPlan => ProtectionPlanValue;
    public OS.Kernel.Memory.PageZeroMappingPolicyResult PageZeroAudit => PageZeroAuditValue;
}
