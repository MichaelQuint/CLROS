namespace OS.Boot;
#if !NOSTDLIB_EFI
#endif
/// <summary>
/// Pointer-free result for call-gate preparation, combined-page
/// materialization/readback, or modeled post-return observation audit.
/// </summary>

public readonly struct M01OwnedCr3CompositeCallGateResult
{
    public const ulong StructureSize = 776;
    public const ulong NoIndex = ~0UL;
    private readonly M01OwnedCr3CompositeCallGateStatus _status;
    private readonly M01OwnedCr3CompositeCallGatePhase _phase;
    private readonly M01OwnedCr3CompositeTransitionTerminalKind _terminalKind;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly M01OwnedCr3CompositeCallGateObservation _observation;
    private readonly M01OwnedCr3CompositeTransitionRuntimeState _preparedState;
    private readonly ulong _codeBase;
    private readonly ulong _codeCapacity;
    private readonly ulong _compositeOffset;
    private readonly ulong _compositeLength;
    private readonly ulong _gateOffset;
    private readonly ulong _gateLength;
    private readonly ulong _gateEntryAddress;
    private readonly ulong _callInstructionOffset;
    private readonly ulong _returnSiteOffset;
    private readonly ulong _returnSiteAddress;
    private readonly ulong _fullPageByteCount;
    private readonly ulong _instructionCount;
    private readonly ulong _invalidIndex;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _fnv1a64;
    internal M01OwnedCr3CompositeCallGateResult(M01OwnedCr3CompositeCallGateStatus status, M01OwnedCr3CompositeCallGatePhase phase, M01OwnedCr3CompositeTransitionTerminalKind terminalKind, M01OwnedCr3CompositeCallGateObservation observation, M01OwnedCr3CompositeTransitionRuntimeState preparedState, ulong codeBase, ulong codeCapacity, ulong compositeOffset, ulong compositeLength, ulong gateOffset, ulong gateLength, ulong gateEntryAddress, ulong callInstructionOffset, ulong returnSiteOffset, ulong returnSiteAddress, ulong fullPageByteCount, ulong instructionCount, ulong invalidIndex, ulong expectedValue, ulong observedValue, ulong fnv1a64)
    {
        _status = status;
        _phase = phase;
        _terminalKind = terminalKind;
        _reserved0 = 0;
        _reserved1 = 0;
        _observation = observation;
        _preparedState = preparedState;
        _codeBase = codeBase;
        _codeCapacity = codeCapacity;
        _compositeOffset = compositeOffset;
        _compositeLength = compositeLength;
        _gateOffset = gateOffset;
        _gateLength = gateLength;
        _gateEntryAddress = gateEntryAddress;
        _callInstructionOffset = callInstructionOffset;
        _returnSiteOffset = returnSiteOffset;
        _returnSiteAddress = returnSiteAddress;
        _fullPageByteCount = fullPageByteCount;
        _instructionCount = instructionCount;
        _invalidIndex = invalidIndex;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _fnv1a64 = fnv1a64;
    }

    public M01OwnedCr3CompositeCallGateStatus Status => _status;
    public M01OwnedCr3CompositeCallGatePhase Phase => _phase;
    public M01OwnedCr3CompositeTransitionTerminalKind TerminalKind => _terminalKind;
    public bool IsSuccess => _status == M01OwnedCr3CompositeCallGateStatus.Success && _reserved0 == 0 && _reserved1 == 0;
    public bool IsPrepared => IsSuccess && _phase == M01OwnedCr3CompositeCallGatePhase.Prepared;
    public bool IsMaterialized => IsSuccess && _phase == M01OwnedCr3CompositeCallGatePhase.Materialized;
    public bool IsReadbackVerified => IsSuccess && _phase == M01OwnedCr3CompositeCallGatePhase.ReadbackVerified;
    public bool IsObservedAudit => IsSuccess && _phase == M01OwnedCr3CompositeCallGatePhase.ObservedAudit;
    public M01OwnedCr3CompositeCallGateObservation Observation => _observation;
    public M01OwnedCr3CompositeTransitionRuntimeState PreparedState => _preparedState;
    public ulong CodeBase => _codeBase;
    public ulong CodeCapacity => _codeCapacity;
    public ulong CompositeOffset => _compositeOffset;
    public ulong CompositeLength => _compositeLength;
    public ulong GateOffset => _gateOffset;
    public ulong GateLength => _gateLength;
    public ulong GateEntryAddress => _gateEntryAddress;
    public ulong CallInstructionOffset => _callInstructionOffset;
    public ulong ReturnSiteOffset => _returnSiteOffset;
    public ulong ReturnSiteAddress => _returnSiteAddress;
    public ulong FullPageByteCount => _fullPageByteCount;
    public ulong InstructionCount => _instructionCount;
    public ulong InvalidIndex => _invalidIndex;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public ulong Fnv1a64 => _fnv1a64;
    public bool UsesWindowsX64Abi => true;
    public bool HasExternalRelocations => false;
    public bool InvokesFirmwareCallbacks => false;
    public bool InvokesManagedCallbacks => false;
    public bool TouchesXmm6ThroughXmm15 => false;
    public bool ExecutesGeneratedCode => false;
    public bool PerformsLiveCr3Load => false;
    public bool PerformsLiveControlRegisterRead => false;
    public bool IsGuestEvidence => false;
}
