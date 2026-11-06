namespace OS.Boot;
/// <summary>
/// Complete value-only result for materialization, range binding, byte
/// readback, or observed-state evaluation.
/// </summary>

public readonly struct M01OwnedCr3CompositeTransitionResult
{
    public const ulong NoIndex = ~0UL;
    private readonly M01OwnedCr3CompositeTransitionStatus _status;
    private readonly M01OwnedCr3CompositeTransitionResultPhase _phase;
    private readonly M01OwnedCr3CompositeTransitionTerminalKind _terminalKind;
    private readonly M01OwnedCr3CompositeTransitionRuntimeState _state;
    private readonly ulong _entryAddress;
    private readonly ulong _byteCount;
    private readonly ulong _byteCapacity;
    private readonly ulong _semanticCount;
    private readonly ulong _semanticCapacity;
    private readonly ulong _invalidIndex;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _fnv1a64;
    internal M01OwnedCr3CompositeTransitionResult(M01OwnedCr3CompositeTransitionStatus status, M01OwnedCr3CompositeTransitionResultPhase phase, M01OwnedCr3CompositeTransitionTerminalKind terminalKind, M01OwnedCr3CompositeTransitionRuntimeState state, ulong entryAddress, ulong byteCount, ulong byteCapacity, ulong semanticCount, ulong semanticCapacity, ulong invalidIndex, ulong expectedValue, ulong observedValue, ulong fnv1a64)
    {
        _status = status;
        _phase = phase;
        _terminalKind = terminalKind;
        _state = state;
        _entryAddress = entryAddress;
        _byteCount = byteCount;
        _byteCapacity = byteCapacity;
        _semanticCount = semanticCount;
        _semanticCapacity = semanticCapacity;
        _invalidIndex = invalidIndex;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _fnv1a64 = fnv1a64;
    }

    public M01OwnedCr3CompositeTransitionStatus Status => _status;
    public bool IsSuccess => _status == M01OwnedCr3CompositeTransitionStatus.Success;
    public M01OwnedCr3CompositeTransitionResultPhase Phase => _phase;
    public bool IsStatePrepared => IsSuccess && _phase == M01OwnedCr3CompositeTransitionResultPhase.StatePrepared;
    public bool IsMaterialized => IsSuccess && _phase == M01OwnedCr3CompositeTransitionResultPhase.Materialized;
    public bool IsReadbackVerified => IsSuccess && _phase == M01OwnedCr3CompositeTransitionResultPhase.ReadbackVerified;
    public bool IsObservedAudit => IsSuccess && _phase == M01OwnedCr3CompositeTransitionResultPhase.ObservedAudit;
    public bool ProvesCompositeEncodingReadback => IsReadbackVerified;
    public M01OwnedCr3CompositeTransitionTerminalKind TerminalKind => _terminalKind;
    public M01OwnedCr3CompositeTransitionRuntimeState State => _state;
    public ulong EntryAddress => _entryAddress;
    public ulong ByteCount => _byteCount;
    public ulong ByteCapacity => _byteCapacity;
    public ulong RequiredByteCount => M01OwnedCr3CompositeTransition.MaterializedPageByteLength;
    public ulong RequiredInstructionByteLength => M01OwnedCr3CompositeTransition.RequiredByteLength;
    public ulong RequiredMaterializedPageByteLength => M01OwnedCr3CompositeTransition.MaterializedPageByteLength;
    public ulong SemanticCount => _semanticCount;
    public ulong SemanticCapacity => _semanticCapacity;
    public ulong RequiredSemanticCount => M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount;
    public ulong InvalidIndex => _invalidIndex;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public ulong Fnv1a64 => _fnv1a64;
    public bool DefinesSingleCompositeOwnedCr3Interval => M01OwnedCr3CompositeTransition.DefinesSingleCompositeOwnedCr3Interval;
    public bool ExecutesCompositeOwnedCr3Interval => M01OwnedCr3CompositeTransition.ExecutesCompositeOwnedCr3Interval;
    public bool IsGuestEvidence => M01OwnedCr3CompositeTransition.IsGuestEvidence;
}
