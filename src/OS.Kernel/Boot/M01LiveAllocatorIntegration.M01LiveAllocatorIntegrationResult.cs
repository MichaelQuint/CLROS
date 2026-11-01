namespace OS.Boot;
/// <summary>
/// Immutable host audit result. Success requires the complete immediate
/// allocate/free cycle and deterministic double-free rejection.
/// </summary>

public readonly struct M01LiveAllocatorIntegrationResult
{
    private readonly M01LiveAllocatorStatus _status;
    private readonly M01LiveAllocatorCheckpoint _checkpoint;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly M01LiveAllocatorEvidence _evidence;
    private readonly M01LiveAllocatorStaticState _staticState;
    private M01LiveAllocatorIntegrationResult(M01LiveAllocatorStatus status, M01LiveAllocatorCheckpoint checkpoint, M01LiveAllocatorEvidence evidence, M01LiveAllocatorStaticState staticState)
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

    public M01LiveAllocatorStatus Status => _status;
    public M01LiveAllocatorCheckpoint LastCompletedCheckpoint => _checkpoint;
    public M01LiveAllocatorEvidence Evidence => _evidence;
    public M01LiveAllocatorStaticState StaticState => _staticState;
    public bool Succeeded => _status == M01LiveAllocatorStatus.Success && _checkpoint == M01LiveAllocatorCheckpoint.DoubleFreeRejected && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _evidence.AllocationEvidenceValue.IsWellFormed && _evidence.MapLedgerEvidenceValue.IsWellFormedFor(_evidence.AllocationEvidenceValue) && _evidence.ArenaStatusValue == M01LiveAllocatorArenaStatus.Success && _evidence.ReservationValidationValue.Succeeded && _evidence.ArenaReservationIndexValue != M01LiveAllocatorIntegration.NoIndex && _evidence.ArenaReservationValue.IsValid && _evidence.ArenaReservationValue.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && _evidence.SourceRangeStatusValue == OS.Kernel.Memory.BootReservationSourceRangeStatus.Success && _evidence.SubtractionValue.Succeeded && _evidence.SubtractionValue.ReservationCount == 0 && _evidence.SubtractionValue.RemovedPages == 0 && _evidence.SubtractionValue.RemovedBytes == 0 && _evidence.StateMaterializationValue.Succeeded && _evidence.AllocationValue.Succeeded && _evidence.FreeValue.Succeeded && _evidence.DoubleFreeValue.IsInitialized && !_evidence.DoubleFreeValue.Succeeded && _evidence.DoubleFreeValue.Status == OS.Kernel.Memory.BootPageAllocatorMutationStatus.FreeRangeNotAllocated && _staticState.IsCompleted;

    internal static M01LiveAllocatorIntegrationResult Success(M01LiveAllocatorEvidence evidence, M01LiveAllocatorStaticState staticState)
    {
        return new M01LiveAllocatorIntegrationResult(M01LiveAllocatorStatus.Success, M01LiveAllocatorCheckpoint.DoubleFreeRejected, evidence, staticState);
    }

    internal static M01LiveAllocatorIntegrationResult Fail(M01LiveAllocatorStatus status, M01LiveAllocatorCheckpoint checkpoint, M01LiveAllocatorEvidence evidence)
    {
        return new M01LiveAllocatorIntegrationResult(status, checkpoint, evidence, default);
    }
}
