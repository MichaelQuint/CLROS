namespace OS.Boot;
public readonly struct M01LivePagingIntegrationResult
{
    private readonly M01LivePagingStatus _status;
    private readonly M01LivePagingCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly M01LivePagingEvidence _evidence;
    private readonly M01LivePagingStaticState _staticState;
    private M01LivePagingIntegrationResult(M01LivePagingStatus status, M01LivePagingCheckpoint checkpoint, M01LivePagingEvidence evidence, M01LivePagingStaticState staticState)
    {
        _status = status;
        _checkpoint = checkpoint;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _evidence = evidence;
        _staticState = staticState;
    }

    public M01LivePagingStatus Status => _status;
    public M01LivePagingCheckpoint LastCompletedCheckpoint => _checkpoint;
    public M01LivePagingEvidence Evidence => _evidence;
    public M01LivePagingStaticState StaticState => _staticState;
    public M01LivePagingExecutionMode ExecutionMode => M01LivePagingExecutionMode.ModelOnlyLegacy;
    public bool IsModelOnly => true;
    public bool IsExecutionEvidence => false;
    public bool PreparedForCompositeTransition => false;
    public bool Succeeded => _status == M01LivePagingStatus.Success && _checkpoint == M01LivePagingCheckpoint.Completed && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _evidence.ReservationValidationValue.Succeeded && _evidence.MappingPlanValue.Succeeded && _evidence.TableBuildValue.Succeeded && _evidence.PreflightMaterializationValue.Succeeded && _evidence.PreflightReadbackValue.Succeeded && _evidence.PreflightValue.Succeeded && _evidence.TransitionMaterializationValue.Succeeded && _evidence.TransitionReadbackValue.Succeeded && _evidence.TransitionEvidenceValue.Succeeded && _evidence.Cr3PlanValue.Succeeded && _evidence.Cr3AuditValue.Succeeded && _evidence.InvlpgMaterializationValue.Succeeded && _evidence.InvlpgReadbackValue.ReadBackVerified && _evidence.ProbeMaterializationValue.Succeeded && _evidence.ProbeReadbackValue.ReadBackVerified && _evidence.ProtectionMutationValue.Succeeded && _evidence.ProtectionPlanValue.Succeeded && _evidence.PageZeroAuditValue.ProvesPageZeroAbsentInSuppliedImage && _staticState.IsCompleted;

    internal static M01LivePagingIntegrationResult Success(M01LivePagingEvidence evidence, M01LivePagingStaticState staticState)
    {
        return new M01LivePagingIntegrationResult(M01LivePagingStatus.Success, M01LivePagingCheckpoint.Completed, evidence, staticState);
    }

    internal static M01LivePagingIntegrationResult Fail(M01LivePagingStatus status, M01LivePagingCheckpoint checkpoint, M01LivePagingEvidence evidence)
    {
        return new M01LivePagingIntegrationResult(status, checkpoint, evidence, default);
    }
}
