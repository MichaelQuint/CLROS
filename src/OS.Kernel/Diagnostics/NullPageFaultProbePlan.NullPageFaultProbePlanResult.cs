namespace OS.Kernel.Diagnostics;
/// <summary>
/// Fixed architectural intent for a future supervisor read of address zero.
/// It is ordinary data, never executable code or live-fault evidence.
/// </summary>

public readonly struct NullPageFaultProbePlanResult
{
    public const ulong StructureSize = 184;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly NullPageFaultProbePlanStatus _status;
    private readonly NullPageFaultProbeRequest _request;
    private readonly NullPageFaultAccessKind _accessKind;
    private readonly byte _hasHardwareErrorCode;
    private readonly byte _expectedPrivilegeLevel;
    private readonly byte _isEnabled;
    private readonly ushort _expectedVector;
    private readonly ushort _reserved0;
    private readonly uint _expectedErrorCode;
    private readonly uint _checkedErrorCodeMask;
    private readonly ulong _expectedCr2Raw;
    private readonly ulong _reserved1;
    private NullPageFaultProbePlanResult(NullPageFaultProbePlanStatus status, NullPageFaultProbeRequest request)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _request = request;
        _accessKind = NullPageFaultAccessKind.Read;
        _hasHardwareErrorCode = 1;
        _expectedPrivilegeLevel = 0;
        _isEnabled = 0;
        _expectedVector = NullPageFaultProbePlan.ExpectedPageFaultVector;
        _reserved0 = 0;
        _expectedErrorCode = NullPageFaultProbePlan.ExpectedErrorCode;
        _checkedErrorCodeMask = NullPageFaultProbePlan.CheckedErrorCodeMask;
        _expectedCr2Raw = NullPageFaultProbePlan.ExpectedCr2Raw;
        _reserved1 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public NullPageFaultProbePlanStatus Status => _status;
    public NullPageFaultProbeRequest Request => _request;
    public NullPageFaultAccessKind AccessKind => _accessKind;
    public bool HasHardwareErrorCode => _hasHardwareErrorCode == 1;
    public byte ExpectedPrivilegeLevel => _expectedPrivilegeLevel;
    public bool IsEnabled => _isEnabled == 1;
    public ushort ExpectedVector => _expectedVector;
    public uint ExpectedErrorCode => _expectedErrorCode;
    public uint CheckedErrorCodeMask => _checkedErrorCodeMask;
    public ulong ExpectedCr2Raw => _expectedCr2Raw;
    public bool Succeeded => IsInitialized && _status == NullPageFaultProbePlanStatus.ReadyDisabled && _request.IsInitialized && _request.PageZeroPolicy.ProvesPageZeroAbsentInSuppliedImage && !_request.EnabledByDefault && !_request.ExecutionRequested && !_request.ExecutionAttempted && _request.OwnedDiagnosticsReady && _request.PageFaultHandlerReady && _accessKind == NullPageFaultAccessKind.Read && _hasHardwareErrorCode == 1 && _expectedPrivilegeLevel == 0 && _isEnabled == 0 && _expectedVector == NullPageFaultProbePlan.ExpectedPageFaultVector && _expectedErrorCode == NullPageFaultProbePlan.ExpectedErrorCode && _checkedErrorCodeMask == NullPageFaultProbePlan.CheckedErrorCodeMask && _expectedCr2Raw == NullPageFaultProbePlan.ExpectedCr2Raw;

    internal static NullPageFaultProbePlanResult Create(NullPageFaultProbePlanStatus status, NullPageFaultProbeRequest request)
    {
        return new NullPageFaultProbePlanResult(status, request);
    }
}
