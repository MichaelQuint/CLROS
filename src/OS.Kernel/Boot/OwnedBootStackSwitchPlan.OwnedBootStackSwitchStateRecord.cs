namespace OS.Boot;
/// <summary>
/// Caller-owned mutable one-shot state for one exact stack-switch plan.
/// The stored address binds the state bytes to their initialized location so
/// copying them elsewhere cannot replay the transition.
/// </summary>

public struct OwnedBootStackSwitchStateRecord
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 536;
    public const ulong RequiredStorageAlignment = 8;
    private uint _size;
    private uint _version;
    private ulong _storageAddress;
    private ulong _transitionCount;
    private VirtualAddress _observedRsp;
    private OwnedBootStackSwitchPlan _plan;
    private OwnedBootStackSwitchState _state;
    private OwnedBootStackSwitchTerminalCause _terminalCause;
    private OwnedBootStackSwitchStatus _terminalStatus;
    private byte _reserved0;
    private byte _reserved1;
    private byte _reserved2;
    private byte _reserved3;
    private byte _reserved4;
    private OwnedBootStackSwitchStateRecord(ulong storageAddress, OwnedBootStackSwitchPlan plan)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _storageAddress = storageAddress;
        _transitionCount = 0;
        _observedRsp = default;
        _plan = plan;
        _state = OwnedBootStackSwitchState.Planned;
        _terminalCause = OwnedBootStackSwitchTerminalCause.Uninitialized;
        _terminalStatus = OwnedBootStackSwitchStatus.Uninitialized;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
    }

    public uint Size => _size;
    public uint Version => _version;
    public ulong StorageAddress => _storageAddress;
    public ulong TransitionCount => _transitionCount;
    public VirtualAddress ObservedRsp => _observedRsp;
    public OwnedBootStackSwitchPlan Plan => _plan;
    public OwnedBootStackSwitchState State => _state;
    public OwnedBootStackSwitchTerminalCause TerminalCause => _terminalCause;
    public OwnedBootStackSwitchStatus TerminalStatus => _terminalStatus;

    public bool IsValid
    {
        get
        {
            if (_size != StructureSize || _version != CurrentVersion || _storageAddress == 0 || (_storageAddress & (RequiredStorageAlignment - 1)) != 0 || _storageAddress > ~0UL - StructureSize || !_plan.IsValid || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0)
            {
                return false;
            }

            if (_plan.StateStorageStart.TryGetRawValueForUefiBoundary(out ulong plannedStorageAddress) != AddressValueStatus.Success || plannedStorageAddress != _storageAddress || _plan.StateStorageByteLength != StructureSize)
            {
                return false;
            }

            if (_state == OwnedBootStackSwitchState.Planned)
            {
                return _transitionCount == 0 && !_observedRsp.IsValid && HasNoTerminalCause();
            }

            if (_state == OwnedBootStackSwitchState.Armed)
            {
                return _transitionCount == 1 && !_observedRsp.IsValid && HasNoTerminalCause();
            }

            if (_state == OwnedBootStackSwitchState.TransferRecorded)
            {
                return _transitionCount == 2 && !_observedRsp.IsValid && HasNoTerminalCause();
            }

            if (_state == OwnedBootStackSwitchState.ContinuationObserved)
            {
                return _transitionCount == 3 && _observedRsp.IsValid && _observedRsp.Equals(_plan.ExpectedContinuationStackPointer) && HasNoTerminalCause();
            }

            if (_state != OwnedBootStackSwitchState.Failed || _transitionCount != 3)
            {
                return false;
            }

            if (_terminalCause == OwnedBootStackSwitchTerminalCause.UnexpectedReturnReported)
            {
                return _terminalStatus == OwnedBootStackSwitchStatus.UnexpectedReturn && !_observedRsp.IsValid;
            }

            return _terminalCause == OwnedBootStackSwitchTerminalCause.ContinuationRspAuditFailed && _plan.ValidateObservedRsp(_observedRsp, out AddressValueStatus terminalAddressStatus) == _terminalStatus && terminalAddressStatus != AddressValueStatus.Success;
        }
    }

    internal static OwnedBootStackSwitchStateRecord Create(ulong storageAddress, OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateRecord(storageAddress, plan);
    }

    internal void Advance(OwnedBootStackSwitchState state, ulong transitionCount, VirtualAddress observedRsp, OwnedBootStackSwitchTerminalCause terminalCause, OwnedBootStackSwitchStatus terminalStatus)
    {
        _state = state;
        _transitionCount = transitionCount;
        _observedRsp = observedRsp;
        _terminalCause = terminalCause;
        _terminalStatus = terminalStatus;
    }

    private bool HasNoTerminalCause()
    {
        return _terminalCause == OwnedBootStackSwitchTerminalCause.Uninitialized && _terminalStatus == OwnedBootStackSwitchStatus.Uninitialized;
    }
}
