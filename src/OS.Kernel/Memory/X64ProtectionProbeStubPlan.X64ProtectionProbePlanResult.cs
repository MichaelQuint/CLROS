namespace OS.Kernel.Memory;
/// <summary>
/// Aggregate caller-owned audit result for the future protection probe.
/// </summary>

public readonly struct X64ProtectionProbePlanResult
{
    public const ulong StructureSize = 744;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeStatus _status;
    private readonly X64ProtectionProbeStatus _stubStatus;
    private readonly X64PageMapStatus _pageMapStatus;
    private readonly X64InvlpgStubPlanStatus _invlpgStatus;
    private readonly X64ProtectionProbeTerminalKind _terminalKind;
    private readonly uint _reserved;
    private readonly OS.Boot.BootMemoryRegionRecord _probeAllocation;
    private readonly BootReservedRange _probeReservation;
    private readonly X64ProtectionProbeStubPlanResult _probeStub;
    private readonly X64PageMapResult _protectionMutation;
    private readonly X64InvlpgStubPlanResult _invlpgStub;
    private readonly X64InvlpgInvocationPlanResult _invlpgInvocation;
    private readonly X64ProtectionProbeObservation _observation;
    private readonly ulong _eventCount;
    private readonly ulong _eventCapacity;
    private readonly ulong _validatedEventCount;
    private readonly ulong _invalidEventIndex;
    private readonly ulong _leafReadbackRaw;
    private X64ProtectionProbePlanResult(X64ProtectionProbeStatus status, X64ProtectionProbeStatus stubStatus, X64PageMapStatus pageMapStatus, X64InvlpgStubPlanStatus invlpgStatus, X64ProtectionProbeTerminalKind terminalKind, OS.Boot.BootMemoryRegionRecord probeAllocation, BootReservedRange probeReservation, X64ProtectionProbeStubPlanResult probeStub, X64PageMapResult protectionMutation, X64InvlpgStubPlanResult invlpgStub, X64InvlpgInvocationPlanResult invlpgInvocation, X64ProtectionProbeObservation observation, ulong eventCount, ulong eventCapacity, ulong validatedEventCount, ulong invalidEventIndex, ulong leafReadbackRaw)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _stubStatus = stubStatus;
        _pageMapStatus = pageMapStatus;
        _invlpgStatus = invlpgStatus;
        _terminalKind = terminalKind;
        _reserved = 0;
        _probeAllocation = probeAllocation;
        _probeReservation = probeReservation;
        _probeStub = probeStub;
        _protectionMutation = protectionMutation;
        _invlpgStub = invlpgStub;
        _invlpgInvocation = invlpgInvocation;
        _observation = observation;
        _eventCount = eventCount;
        _eventCapacity = eventCapacity;
        _validatedEventCount = validatedEventCount;
        _invalidEventIndex = invalidEventIndex;
        _leafReadbackRaw = leafReadbackRaw;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved == 0;
    public X64ProtectionProbeStatus Status => _status;
    public X64ProtectionProbeStatus StubStatus => _stubStatus;
    public X64PageMapStatus PageMapStatus => _pageMapStatus;
    public X64InvlpgStubPlanStatus InvlpgStatus => _invlpgStatus;
    public X64ProtectionProbeTerminalKind TerminalKind => _terminalKind;
    public OS.Boot.BootMemoryRegionRecord ProbeAllocation => _probeAllocation;
    public BootReservedRange ProbeReservation => _probeReservation;
    public X64ProtectionProbeStubPlanResult ProbeStub => _probeStub;
    public X64PageMapResult ProtectionMutation => _protectionMutation;
    public X64InvlpgStubPlanResult InvlpgStub => _invlpgStub;
    public X64InvlpgInvocationPlanResult InvlpgInvocation => _invlpgInvocation;
    public X64ProtectionProbeObservation Observation => _observation;
    public ulong EventCount => _eventCount;
    public ulong EventCapacity => _eventCapacity;
    public ulong ValidatedEventCount => _validatedEventCount;
    public ulong InvalidEventIndex => _invalidEventIndex;
    public ulong LeafReadbackRaw => _leafReadbackRaw;
    public bool Succeeded => IsInitialized && _status == X64ProtectionProbeStatus.Success && _stubStatus == X64ProtectionProbeStatus.Success && _pageMapStatus == X64PageMapStatus.Success && _invlpgStatus == X64InvlpgStubPlanStatus.Success && _terminalKind == X64ProtectionProbeTerminalKind.ModeledReturn && _probeStub.ReadBackVerified && _protectionMutation.Succeeded && _invlpgStub.ReadBackVerified && _invlpgInvocation.Succeeded && _observation.Validate() == X64ProtectionProbeStatus.Success && _eventCount == X64ProtectionProbeStubPlan.RequiredEventCount && _eventCapacity >= _eventCount && _eventCapacity <= X64ProtectionProbeStubPlan.RequiredEventCount && _validatedEventCount == _eventCount && _invalidEventIndex == NoIndex && _leafReadbackRaw == _protectionMutation.ReplacementEntry.RawValue;

    internal static X64ProtectionProbePlanResult Success(OS.Boot.BootMemoryRegionRecord probeAllocation, BootReservedRange probeReservation, X64ProtectionProbeStubPlanResult probeStub, X64PageMapResult protectionMutation, X64InvlpgStubPlanResult invlpgStub, X64InvlpgInvocationPlanResult invlpgInvocation, X64ProtectionProbeObservation observation, ulong eventCount, ulong eventCapacity, ulong leafReadbackRaw)
    {
        return new X64ProtectionProbePlanResult(X64ProtectionProbeStatus.Success, X64ProtectionProbeStatus.Success, X64PageMapStatus.Success, X64InvlpgStubPlanStatus.Success, observation.TerminalKind, probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, eventCount, NoIndex, leafReadbackRaw);
    }

    internal static X64ProtectionProbePlanResult Fail(X64ProtectionProbeStatus status, OS.Boot.BootMemoryRegionRecord probeAllocation = default, BootReservedRange probeReservation = default, X64ProtectionProbeStubPlanResult probeStub = default, X64PageMapResult protectionMutation = default, X64InvlpgStubPlanResult invlpgStub = default, X64InvlpgInvocationPlanResult invlpgInvocation = default, X64ProtectionProbeObservation observation = default, ulong eventCount = 0, ulong eventCapacity = 0, ulong validatedEventCount = 0, ulong invalidEventIndex = NoIndex, ulong leafReadbackRaw = 0)
    {
        X64InvlpgStubPlanStatus invlpgStatus = invlpgStub.Status != X64InvlpgStubPlanStatus.Success ? invlpgStub.Status : invlpgInvocation.Status;
        return new X64ProtectionProbePlanResult(status, probeStub.Status, protectionMutation.Status, invlpgStatus, observation.TerminalKind, probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, validatedEventCount, invalidEventIndex, leafReadbackRaw);
    }
}
