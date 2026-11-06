namespace OS.Kernel.Diagnostics;
/// <summary>
/// Versioned, unmanaged result for request validation or a synthetic
/// CpuFaultFrame comparison. ExpectedValue and ObservedValue describe the
/// first rejected field and remain zero when no field was rejected.
/// </summary>

public readonly struct DisabledDefaultUd2VectorStateResult
{
    public const uint StructureSize = 72;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly DisabledDefaultUd2VectorStateStatus _status;
    private readonly DisabledDefaultUd2VectorStateOperation _operation;
    private readonly DisabledDefaultUd2VectorField _firstMismatchField;
    private readonly ushort _comparedFieldCount;
    private readonly uint _reserved0;
    private readonly DisabledDefaultUd2VectorStateRequest _request;
    private readonly ulong _expectedValue;
    private readonly ulong _observedValue;
    private readonly ulong _reserved1;
    private DisabledDefaultUd2VectorStateResult(DisabledDefaultUd2VectorStateOperation operation, DisabledDefaultUd2VectorStateStatus status, DisabledDefaultUd2VectorStateRequest request, DisabledDefaultUd2VectorField firstMismatchField, ushort comparedFieldCount, ulong expectedValue, ulong observedValue)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _firstMismatchField = firstMismatchField;
        _comparedFieldCount = comparedFieldCount;
        _reserved0 = 0;
        _request = request;
        _expectedValue = expectedValue;
        _observedValue = observedValue;
        _reserved1 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _status <= DisabledDefaultUd2VectorStateStatus.RegisterStateMismatch && _operation <= DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation && _firstMismatchField <= DisabledDefaultUd2VectorField.R15 && _comparedFieldCount <= DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount && _reserved0 == 0 && _reserved1 == 0;
    public DisabledDefaultUd2VectorStateStatus Status => _status;
    public DisabledDefaultUd2VectorStateOperation Operation => _operation;
    public DisabledDefaultUd2VectorStateRequest Request => _request;
    public DisabledDefaultUd2VectorField FirstMismatchField => _firstMismatchField;
    public ushort ComparedFieldCount => _comparedFieldCount;
    public ulong ExpectedValue => _expectedValue;
    public ulong ObservedValue => _observedValue;
    public bool ReadyDisabled => IsInitialized && _operation == DisabledDefaultUd2VectorStateOperation.Plan && _status == DisabledDefaultUd2VectorStateStatus.ReadyDisabled && _firstMismatchField == DisabledDefaultUd2VectorField.None && _comparedFieldCount == 0 && _expectedValue == 0 && _observedValue == 0 && DisabledDefaultUd2VectorState.IsReadyDisabledRequest(_request);
    public bool ObservationAccepted => IsInitialized && _operation == DisabledDefaultUd2VectorStateOperation.AuditSyntheticObservation && _status == DisabledDefaultUd2VectorStateStatus.ObservationAccepted && _request.IsInitialized && _firstMismatchField == DisabledDefaultUd2VectorField.None && _comparedFieldCount == DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount && _expectedValue == 0 && _observedValue == 0 && DisabledDefaultUd2VectorState.IsReadyDisabledRequest(_request);
    public bool Succeeded => ReadyDisabled || ObservationAccepted;
    public bool IsGuestEvidence => false;

    internal static DisabledDefaultUd2VectorStateResult Create(DisabledDefaultUd2VectorStateOperation operation, DisabledDefaultUd2VectorStateStatus status, DisabledDefaultUd2VectorStateRequest request, DisabledDefaultUd2VectorField firstMismatchField = DisabledDefaultUd2VectorField.None, ushort comparedFieldCount = 0, ulong expectedValue = 0, ulong observedValue = 0)
    {
        return new DisabledDefaultUd2VectorStateResult(operation, status, request, firstMismatchField, comparedFieldCount, expectedValue, observedValue);
    }
}
