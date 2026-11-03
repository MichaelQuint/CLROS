namespace OS.Boot;
public readonly struct M02SafeStubPageIntegrationResult
{
    public const ulong StructureSize = 96;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly M02SafeStubPageIntegrationStatus _status;
    private readonly M02SafeStubPageIntegrationPhase _phase;
    private readonly byte _portIoExecuted;
    private readonly byte _privilegedStubTargeted;
    private readonly byte _reserved0;
    private readonly OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus _sourceStatus;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _observedReturn;
    private readonly ulong _modeledPageAddress;
    private readonly ulong _pageByteCount;
    private readonly ulong _safeOffset;
    private readonly ulong _out8Offset;
    private readonly ulong _in8Offset;
    private readonly ulong _fnv1a64;
    private readonly ulong _publicationEpoch;
    private readonly ulong _transitionVersion;
    private readonly ulong _probeInvocationCount;
    private M02SafeStubPageIntegrationResult(M02SafeStubPageIntegrationStatus status, M02SafeStubPageIntegrationPhase phase, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus sourceStatus, ulong modeledPageAddress, ulong publicationEpoch, ulong transitionVersion, uint observedReturn, ulong probeInvocationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _portIoExecuted = 0;
        _privilegedStubTargeted = 0;
        _reserved0 = 0;
        _sourceStatus = sourceStatus;
        _reserved1 = 0;
        _reserved2 = 0;
        _observedReturn = observedReturn;
        _modeledPageAddress = modeledPageAddress;
        _pageByteCount = M02SafeStubPageIntegration.PageSize;
        _safeOffset = M02SafeStubPageIntegration.SafeOffset;
        _out8Offset = M02SafeStubPageIntegration.Out8Offset;
        _in8Offset = M02SafeStubPageIntegration.In8Offset;
        _fnv1a64 = M02SafeStubPageIntegration.ExpectedFnv1a64;
        _publicationEpoch = publicationEpoch;
        _transitionVersion = transitionVersion;
        _probeInvocationCount = probeInvocationCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public M02SafeStubPageIntegrationStatus Status => _status;
    public M02SafeStubPageIntegrationPhase Phase => _phase;
    public OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus SourceStatus => _sourceStatus;
    public ulong ModeledPageAddress => _modeledPageAddress;
    public ulong PageByteCount => _pageByteCount;
    public ulong SafeOffset => _safeOffset;
    public ulong Out8Offset => _out8Offset;
    public ulong In8Offset => _in8Offset;
    public ulong Fnv1a64 => _fnv1a64;
    public ulong PublicationEpoch => _publicationEpoch;
    public ulong TransitionVersion => _transitionVersion;
    public uint ObservedReturn => _observedReturn;
    public ulong ProbeInvocationCount => _probeInvocationCount;
    public bool PortIoExecuted => _portIoExecuted != 0;
    public bool PrivilegedStubTargeted => _privilegedStubTargeted != 0;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized || _status != M02SafeStubPageIntegrationStatus.Success || _phase < M02SafeStubPageIntegrationPhase.Materialized || _phase > M02SafeStubPageIntegrationPhase.ExecutionAudited || _sourceStatus != OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus.Success || !M02SafeStubPageIntegration.IsValidPageAddress(_modeledPageAddress) || _pageByteCount != M02SafeStubPageIntegration.PageSize || _safeOffset != M02SafeStubPageIntegration.SafeOffset || _out8Offset != M02SafeStubPageIntegration.Out8Offset || _in8Offset != M02SafeStubPageIntegration.In8Offset || _fnv1a64 != M02SafeStubPageIntegration.ExpectedFnv1a64 || _publicationEpoch != M02SafeStubPageIntegration.RequiredPublicationEpoch || PortIoExecuted || PrivilegedStubTargeted)
            {
                return false;
            }

            if (_phase == M02SafeStubPageIntegrationPhase.Materialized)
            {
                return _transitionVersion == 1 && _observedReturn == 0 && _probeInvocationCount == 0;
            }

            if (_phase == M02SafeStubPageIntegrationPhase.ReadBackVerified)
            {
                return _transitionVersion == 2 && _observedReturn == 0 && _probeInvocationCount == 0;
            }

            if (_phase == M02SafeStubPageIntegrationPhase.FinalizationAudited)
            {
                return _transitionVersion == 3 && _observedReturn == 0 && _probeInvocationCount == 0;
            }

            return _transitionVersion == 3 && _observedReturn == M02SafeStubPageIntegration.SafeReturnValue && _probeInvocationCount == M02SafeStubPageIntegration.ExactProbeInvocationCount;
        }
    }

    internal static M02SafeStubPageIntegrationResult Success(M02SafeStubPageIntegrationPhase phase, ulong modeledPageAddress, ulong transitionVersion, uint observedReturn, ulong probeInvocationCount)
    {
        return new M02SafeStubPageIntegrationResult(M02SafeStubPageIntegrationStatus.Success, phase, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus.Success, modeledPageAddress, M02SafeStubPageIntegration.RequiredPublicationEpoch, transitionVersion, observedReturn, probeInvocationCount);
    }

    internal static M02SafeStubPageIntegrationResult Fail(M02SafeStubPageIntegrationStatus status, OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus sourceStatus = OS.Kernel.Diagnostics.ExistingIn8Out8StubPageStatus.Uninitialized)
    {
        return new M02SafeStubPageIntegrationResult(status, M02SafeStubPageIntegrationPhase.Uninitialized, sourceStatus, modeledPageAddress: 0, publicationEpoch: 0, transitionVersion: 0, observedReturn: 0, probeInvocationCount: 0);
    }
}
