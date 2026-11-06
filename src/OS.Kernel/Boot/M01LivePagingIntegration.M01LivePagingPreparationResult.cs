namespace OS.Boot;
/// <summary>
/// Result of M01-029 preparation. Success is preparation evidence only and
/// cannot be interpreted as generated-code, privileged, EFI, or guest
/// execution.
/// </summary>

public readonly struct M01LivePagingPreparationResult
{
    private readonly M01LivePagingStatus _status;
    private readonly M01LivePagingCheckpoint _checkpoint;
    private readonly M01LivePagingExecutionMode _executionMode;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly M01LivePagingPreparationEvidence _evidence;
    private readonly M01LivePagingPreparationState _state;
    private M01LivePagingPreparationResult(M01LivePagingStatus status, M01LivePagingCheckpoint checkpoint, M01LivePagingExecutionMode executionMode, M01LivePagingPreparationEvidence evidence, M01LivePagingPreparationState state)
    {
        _status = status;
        _checkpoint = checkpoint;
        _executionMode = executionMode;
        _reserved0 = 0;
        _reserved1 = 0;
        _evidence = evidence;
        _state = state;
    }

    public M01LivePagingStatus Status => _status;
    public M01LivePagingCheckpoint LastCompletedCheckpoint => _checkpoint;
    public M01LivePagingExecutionMode ExecutionMode => _executionMode;
    public M01LivePagingPreparationEvidence Evidence => _evidence;
    public M01LivePagingPreparationState State => _state;
    public bool IsPreparationOnly => _executionMode == M01LivePagingExecutionMode.PreparationOnly;
    public bool IsModelOnly => false;
    public bool IsExecutionEvidence => false;
    public bool Succeeded => _status == M01LivePagingStatus.Success && _checkpoint == M01LivePagingCheckpoint.PreparedForCompositeTransition && _executionMode == M01LivePagingExecutionMode.PreparationOnly && _reserved0 == 0 && _reserved1 == 0 && _evidence.Succeeded && _state.IsPrepared;
    public bool PreparedForCompositeTransition => Succeeded;
    public bool UsesExactLiveIntervalBindings => Succeeded && _state.MappingRequirementCount == M01LivePagingIntegration.RequiredLiveIntervalMappingRequirementCount && _evidence.MappingPlan.RequiredPurposeMask == OS.Kernel.Boot.X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask;

    internal static M01LivePagingPreparationResult Success(M01LivePagingPreparationEvidence evidence, M01LivePagingPreparationState state)
    {
        return new M01LivePagingPreparationResult(M01LivePagingStatus.Success, M01LivePagingCheckpoint.PreparedForCompositeTransition, M01LivePagingExecutionMode.PreparationOnly, evidence, state);
    }

    internal static M01LivePagingPreparationResult Fail(M01LivePagingStatus status, M01LivePagingCheckpoint checkpoint, M01LivePagingPreparationEvidence evidence)
    {
        return new M01LivePagingPreparationResult(status, checkpoint, M01LivePagingExecutionMode.PreparationOnly, evidence, default);
    }
}
