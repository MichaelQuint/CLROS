namespace OS.Boot;
/// <summary>
/// Preserves one-shot state-record transition and RSP-audit evidence.
/// The caller owns serialization of the bound mutable state storage.
/// </summary>

public readonly struct OwnedBootStackSwitchStateResult
{
    private readonly OwnedBootStackSwitchStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OwnedBootStackSwitchState _previousState;
    private readonly OwnedBootStackSwitchState _requestedState;
    private readonly OwnedBootStackSwitchState _currentState;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly VirtualAddress _observedRsp;
    private readonly ulong _stateStorageAddress;
    private readonly ulong _transitionCount;
    private readonly OwnedBootStackSwitchPlan _plan;
    private OwnedBootStackSwitchStateResult(OwnedBootStackSwitchStatus status, AddressValueStatus addressStatus, OwnedBootStackSwitchState previousState, OwnedBootStackSwitchState requestedState, OwnedBootStackSwitchState currentState, VirtualAddress observedRsp, ulong stateStorageAddress, ulong transitionCount, OwnedBootStackSwitchPlan plan)
    {
        _status = status;
        _addressStatus = addressStatus;
        _previousState = previousState;
        _requestedState = requestedState;
        _currentState = currentState;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _observedRsp = observedRsp;
        _stateStorageAddress = stateStorageAddress;
        _transitionCount = transitionCount;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;
    public AddressValueStatus AddressStatus => _addressStatus;
    public OwnedBootStackSwitchState PreviousState => _previousState;
    public OwnedBootStackSwitchState RequestedState => _requestedState;
    public OwnedBootStackSwitchState CurrentState => _currentState;
    public VirtualAddress ObservedRsp => _observedRsp;
    public ulong StateStorageAddress => _stateStorageAddress;
    public ulong TransitionCount => _transitionCount;
    public OwnedBootStackSwitchPlan Plan => _plan;
    public bool Succeeded => _status == OwnedBootStackSwitchStatus.Success && _addressStatus == AddressValueStatus.Success && _requestedState == _currentState && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _plan.IsValid && _stateStorageAddress != 0 && (_stateStorageAddress & (OwnedBootStackSwitchStateRecord.RequiredStorageAlignment - 1)) == 0 && _stateStorageAddress <= ~0UL - OwnedBootStackSwitchStateRecord.StructureSize && IsSuccessfulTransition(_previousState, _currentState, _observedRsp, _transitionCount, _plan);

    internal static OwnedBootStackSwitchStateResult Success(OwnedBootStackSwitchState previousState, OwnedBootStackSwitchState currentState, VirtualAddress observedRsp, ulong stateStorageAddress, ulong transitionCount, OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateResult(OwnedBootStackSwitchStatus.Success, AddressValueStatus.Success, previousState, currentState, currentState, observedRsp, stateStorageAddress, transitionCount, plan);
    }

    internal static OwnedBootStackSwitchStateResult Fail(OwnedBootStackSwitchStatus status, AddressValueStatus addressStatus, OwnedBootStackSwitchState previousState, OwnedBootStackSwitchState requestedState, OwnedBootStackSwitchState currentState, VirtualAddress observedRsp, ulong stateStorageAddress, ulong transitionCount, OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateResult(status, addressStatus, previousState, requestedState, currentState, observedRsp, stateStorageAddress, transitionCount, plan);
    }

    private static bool IsSuccessfulTransition(OwnedBootStackSwitchState previousState, OwnedBootStackSwitchState currentState, VirtualAddress observedRsp, ulong transitionCount, OwnedBootStackSwitchPlan plan)
    {
        if (previousState == OwnedBootStackSwitchState.Uninitialized && currentState == OwnedBootStackSwitchState.Planned)
        {
            return transitionCount == 0 && !observedRsp.IsValid;
        }

        if (previousState == OwnedBootStackSwitchState.Planned && currentState == OwnedBootStackSwitchState.Armed)
        {
            return transitionCount == 1 && !observedRsp.IsValid;
        }

        if (previousState == OwnedBootStackSwitchState.Armed && currentState == OwnedBootStackSwitchState.TransferRecorded)
        {
            return transitionCount == 2 && !observedRsp.IsValid;
        }

        return previousState == OwnedBootStackSwitchState.TransferRecorded && currentState == OwnedBootStackSwitchState.ContinuationObserved && transitionCount == 3 && observedRsp.IsValid && observedRsp.Equals(plan.ExpectedContinuationStackPointer);
    }
}
