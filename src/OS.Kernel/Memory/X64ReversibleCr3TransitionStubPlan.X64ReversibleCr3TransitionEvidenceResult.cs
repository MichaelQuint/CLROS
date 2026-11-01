namespace OS.Kernel.Memory;
/// <summary>
/// Host-safe audit result. A complete value snapshot is retained so a
/// composite plan can independently cross-check every raw register and
/// canary observation.
/// </summary>

public readonly struct X64ReversibleCr3TransitionEvidenceResult
{
    public const ulong StructureSize = 216;
    private readonly byte _isInitialized;
    private readonly X64ReversibleCr3TransitionStubPlanStatus _status;
    private readonly X64ReversibleCr3TransitionRuntimeStatus _runtimeStatus;
    private readonly X64ReversibleCr3TransitionCheckpoint _checkpoint;
    private readonly X64ReversibleCr3TransitionTerminalKind _terminalKind;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _runtimeStateAddress;
    private readonly X64ReversibleCr3TransitionRuntimeState _state;
    internal X64ReversibleCr3TransitionEvidenceResult(X64ReversibleCr3TransitionStubPlanStatus status, X64ReversibleCr3TransitionTerminalKind terminalKind, ulong runtimeStateAddress, X64ReversibleCr3TransitionRuntimeState state, ulong expectedValue, ulong observedValue)
    {
        _isInitialized = 1;
        _status = status;
        _runtimeStatus = state.RuntimeStatus;
        _checkpoint = state.Checkpoint;
        _terminalKind = terminalKind;
        _reserved0 = 0;
        _reserved1 = 0;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _runtimeStateAddress = runtimeStateAddress;
        _state = state;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && (byte)_status <= (byte)X64ReversibleCr3TransitionStubPlanStatus.PostRestoreCanaryMismatch && _status != X64ReversibleCr3TransitionStubPlanStatus.Uninitialized && (byte)_runtimeStatus <= (byte)X64ReversibleCr3TransitionRuntimeStatus.PostRestoreCanaryMismatch && (byte)_checkpoint <= (byte)X64ReversibleCr3TransitionCheckpoint.Returned && (byte)_terminalKind <= (byte)X64ReversibleCr3TransitionTerminalKind.TripleFault;
    public bool Succeeded => IsInitialized && _status == X64ReversibleCr3TransitionStubPlanStatus.Success && _runtimeStatus == X64ReversibleCr3TransitionRuntimeStatus.Success && _checkpoint == X64ReversibleCr3TransitionCheckpoint.Returned && _terminalKind == X64ReversibleCr3TransitionTerminalKind.Returned && _expectedValue == 0 && _observedValue == 0 && _state.IsInitialized && _runtimeStatus == _state.RuntimeStatus && _checkpoint == _state.Checkpoint && _state.FirmwareCallbackCount == 0 && X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStorageLayout(_runtimeStateAddress, _state.CanaryAddress, _state.StackStart, _state.StackEndExclusive) && X64ReversibleCr3TransitionStubPlan.IsValidOwnedCr3(_state.OwnedCr3Raw) && X64ReversibleCr3TransitionStubPlan.IsValidFirmwareRawCr3(_state.OriginalCr3Raw) && (_state.OriginalCr3Raw & X64ReversibleCr3TransitionStubPlan.Cr3ArchitecturalBaseMask) != _state.OwnedCr3Raw && (_state.OriginalCr4Raw & (X64ReversibleCr3TransitionStubPlan.Cr4PcideBit | X64ReversibleCr3TransitionStubPlan.Cr4FiveLevelPagingBit)) == 0 && _state.ClearedCr4Readback == (_state.OriginalCr4Raw & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit) && _state.OwnedCr3Readback == _state.OwnedCr3Raw && _state.FirmwareCr3Readback == _state.OriginalCr3Raw && _state.RestoredCr4Readback == _state.OriginalCr4Raw && (_state.OriginalRflags & X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit) == X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit && (_state.CliRflagsReadback & (X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit | X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit)) == X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit && _state.RestoredRflagsReadback == _state.OriginalRflags && _state.CanaryAddress != 0 && (_state.CanaryAddress & 7) == 0 && _state.ExpectedCanary != 0 && _state.ReplacementCanary != 0 && _state.ExpectedCanary != _state.ReplacementCanary && _state.CanaryBefore == _state.ExpectedCanary && _state.CanaryAfter == _state.ReplacementCanary && _state.PostRestoreCanaryReadback == _state.ReplacementCanary && _state.StackStart != 0 && _state.StackEndExclusive > _state.StackStart && _state.StackEndExclusive - _state.StackStart >= 32 && (_state.StackStart & 0xf) == 0 && (_state.StackEndExclusive & 0xf) == 0 && _state.EntryRsp == _state.ExitRsp && _state.StackStart + X64ReversibleCr3TransitionStubPlan.MaximumSimultaneousPushBytes >= _state.StackStart && _state.EntryRsp >= _state.StackStart + X64ReversibleCr3TransitionStubPlan.MaximumSimultaneousPushBytes && _state.EntryRsp < _state.StackEndExclusive && (_state.EntryRsp & 0xf) == 8;
    public X64ReversibleCr3TransitionStubPlanStatus Status => _status;
    public X64ReversibleCr3TransitionRuntimeStatus RuntimeStatus => _runtimeStatus;
    public X64ReversibleCr3TransitionCheckpoint Checkpoint => _checkpoint;
    public X64ReversibleCr3TransitionTerminalKind TerminalKind => _terminalKind;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public ulong RuntimeStateAddress => _runtimeStateAddress;
    public X64ReversibleCr3TransitionRuntimeState State => _state;

    internal X64ReversibleCr3TransitionEvidenceResult WithRuntimeStateAddress(ulong runtimeStateAddress)
    {
        return new X64ReversibleCr3TransitionEvidenceResult(_status, _terminalKind, runtimeStateAddress, _state, _expectedValue, _observedValue);
    }
}
