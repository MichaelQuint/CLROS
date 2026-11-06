namespace OS.Kernel.Memory;
/// <summary>
/// Caller-supplied modeled outcome. It intentionally carries no live-evidence
/// bit; IsLiveEvidence is always false.
/// </summary>

public readonly struct X64ProtectionProbeObservation
{
    public const ulong StructureSize = 56;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeTerminalKind _terminalKind;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _invocationCount;
    private readonly uint _expectedReturnValue;
    private readonly uint _observedReturnValue;
    private readonly ulong _canaryBefore;
    private readonly ulong _canaryAfter;
    private readonly ulong _reserved;
    private X64ProtectionProbeObservation(X64ProtectionProbeTerminalKind terminalKind, OS.Boot.VirtualAddress entryAddress, ulong invocationCount, uint expectedReturnValue, uint observedReturnValue, ulong canaryBefore, ulong canaryAfter)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _terminalKind = terminalKind;
        _entryAddress = entryAddress;
        _invocationCount = invocationCount;
        _expectedReturnValue = expectedReturnValue;
        _observedReturnValue = observedReturnValue;
        _canaryBefore = canaryBefore;
        _canaryAfter = canaryAfter;
        _reserved = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public X64ProtectionProbeTerminalKind TerminalKind => _terminalKind;
    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;
    public ulong InvocationCount => _invocationCount;
    public uint ExpectedReturnValue => _expectedReturnValue;
    public uint ObservedReturnValue => _observedReturnValue;
    public ulong CanaryBefore => _canaryBefore;
    public ulong CanaryAfter => _canaryAfter;
    public bool IsLiveEvidence => false;

    public static X64ProtectionProbeObservation CreateModeled(X64ProtectionProbeTerminalKind terminalKind, OS.Boot.VirtualAddress entryAddress, ulong invocationCount, uint observedReturnValue, ulong canaryBefore, ulong canaryAfter)
    {
        return new X64ProtectionProbeObservation(terminalKind, entryAddress, invocationCount, X64ProtectionProbeStubPlan.ExpectedReturnValue, observedReturnValue, canaryBefore, canaryAfter);
    }

    public X64ProtectionProbeStatus Validate()
    {
        if (!IsInitialized || !_entryAddress.IsValid)
        {
            return X64ProtectionProbeStatus.InvalidObservation;
        }

        if (_terminalKind != X64ProtectionProbeTerminalKind.ModeledReturn)
        {
            return X64ProtectionProbeStatus.InvalidTerminalKind;
        }

        if (_invocationCount != 1)
        {
            return X64ProtectionProbeStatus.InvocationCountMustBeOne;
        }

        if (_expectedReturnValue != X64ProtectionProbeStubPlan.ExpectedReturnValue || _observedReturnValue != _expectedReturnValue)
        {
            return X64ProtectionProbeStatus.UnexpectedReturnValue;
        }

        if (_canaryBefore == 0 || _canaryAfter != _canaryBefore)
        {
            return X64ProtectionProbeStatus.CanaryMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }
}
