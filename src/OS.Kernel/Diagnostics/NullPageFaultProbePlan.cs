namespace OS.Kernel.Diagnostics;

/// <summary>
/// Stable status contract for the disabled null-page-fault probe plan.
/// Values are append-only. ReadyDisabled is metadata readiness only.
/// </summary>
public enum NullPageFaultProbePlanStatus : byte
{
    Uninitialized = 0,
    ReadyDisabled = 1,
    InvalidRequestState = 2,
    PageZeroPolicyUnavailable = 3,
    EnabledByDefaultRejected = 4,
    ExecutionAttemptedRejected = 5,
    ExecutionRequestedRejected = 6,
    OwnedDiagnosticsUnavailable = 7,
    PageFaultHandlerUnavailable = 8
}

/// <summary>
/// The future architectural access modeled by the disabled plan.
/// </summary>
public enum NullPageFaultAccessKind : byte
{
    Uninitialized = 0,
    Read = 1
}

/// <summary>
/// Versioned, unmanaged caller input for planning only. Diagnostics fields are
/// caller attestations; they are not observations of loaded guest state.
/// </summary>
public readonly struct NullPageFaultProbeRequest
{
    public const ulong StructureSize = 144;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _ownedDiagnosticsReady;
    private readonly byte _pageFaultHandlerReady;
    private readonly byte _executionRequested;
    private readonly byte _executionAttempted;
    private readonly uint _reserved;
    private readonly OS.Kernel.Memory.PageZeroMappingPolicyResult
        _pageZeroPolicy;

    private NullPageFaultProbeRequest(
        OS.Kernel.Memory.PageZeroMappingPolicyResult
            pageZeroPolicy,
        bool enabledByDefault,
        bool ownedDiagnosticsReady,
        bool pageFaultHandlerReady,
        bool executionRequested,
        bool executionAttempted)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _enabledByDefault =
            enabledByDefault ? (byte)1 : (byte)0;
        _ownedDiagnosticsReady =
            ownedDiagnosticsReady ? (byte)1 : (byte)0;
        _pageFaultHandlerReady =
            pageFaultHandlerReady ? (byte)1 : (byte)0;
        _executionRequested =
            executionRequested ? (byte)1 : (byte)0;
        _executionAttempted =
            executionAttempted ? (byte)1 : (byte)0;
        _reserved = 0;
        _pageZeroPolicy = pageZeroPolicy;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _enabledByDefault <= 1
        && _ownedDiagnosticsReady <= 1
        && _pageFaultHandlerReady <= 1
        && _executionRequested <= 1
        && _executionAttempted <= 1
        && _reserved == 0;

    public bool EnabledByDefault => _enabledByDefault == 1;

    public bool OwnedDiagnosticsReady =>
        _ownedDiagnosticsReady == 1;

    public bool PageFaultHandlerReady =>
        _pageFaultHandlerReady == 1;

    public bool ExecutionRequested => _executionRequested == 1;

    public bool ExecutionAttempted => _executionAttempted == 1;

    public OS.Kernel.Memory.PageZeroMappingPolicyResult
        PageZeroPolicy => _pageZeroPolicy;

    public static NullPageFaultProbeRequest CreateDisabled(
        OS.Kernel.Memory.PageZeroMappingPolicyResult
            pageZeroPolicy,
        bool callerAttestsOwnedDiagnosticsReady,
        bool callerAttestsPageFaultHandlerReady)
    {
        return new NullPageFaultProbeRequest(
            pageZeroPolicy,
            enabledByDefault: false,
            callerAttestsOwnedDiagnosticsReady,
            callerAttestsPageFaultHandlerReady,
            executionRequested: false,
            executionAttempted: false);
    }

    public static NullPageFaultProbeRequest CreateForValidation(
        OS.Kernel.Memory.PageZeroMappingPolicyResult
            pageZeroPolicy,
        bool enabledByDefault,
        bool callerAttestsOwnedDiagnosticsReady,
        bool callerAttestsPageFaultHandlerReady,
        bool executionRequested,
        bool executionAttempted)
    {
        return new NullPageFaultProbeRequest(
            pageZeroPolicy,
            enabledByDefault,
            callerAttestsOwnedDiagnosticsReady,
            callerAttestsPageFaultHandlerReady,
            executionRequested,
            executionAttempted);
    }
}

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

    private NullPageFaultProbePlanResult(
        NullPageFaultProbePlanStatus status,
        NullPageFaultProbeRequest request)
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
        _expectedVector =
            NullPageFaultProbePlan.ExpectedPageFaultVector;
        _reserved0 = 0;
        _expectedErrorCode =
            NullPageFaultProbePlan.ExpectedErrorCode;
        _checkedErrorCodeMask =
            NullPageFaultProbePlan.CheckedErrorCodeMask;
        _expectedCr2Raw =
            NullPageFaultProbePlan.ExpectedCr2Raw;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public NullPageFaultProbePlanStatus Status => _status;

    public NullPageFaultProbeRequest Request => _request;

    public NullPageFaultAccessKind AccessKind => _accessKind;

    public bool HasHardwareErrorCode => _hasHardwareErrorCode == 1;

    public byte ExpectedPrivilegeLevel =>
        _expectedPrivilegeLevel;

    public bool IsEnabled => _isEnabled == 1;

    public ushort ExpectedVector => _expectedVector;

    public uint ExpectedErrorCode => _expectedErrorCode;

    public uint CheckedErrorCodeMask => _checkedErrorCodeMask;

    public ulong ExpectedCr2Raw => _expectedCr2Raw;

    public bool Succeeded =>
        IsInitialized
        && _status == NullPageFaultProbePlanStatus.ReadyDisabled
        && _request.IsInitialized
        && _request.PageZeroPolicy
            .ProvesPageZeroAbsentInSuppliedImage
        && !_request.EnabledByDefault
        && !_request.ExecutionRequested
        && !_request.ExecutionAttempted
        && _request.OwnedDiagnosticsReady
        && _request.PageFaultHandlerReady
        && _accessKind == NullPageFaultAccessKind.Read
        && _hasHardwareErrorCode == 1
        && _expectedPrivilegeLevel == 0
        && _isEnabled == 0
        && _expectedVector ==
            NullPageFaultProbePlan.ExpectedPageFaultVector
        && _expectedErrorCode ==
            NullPageFaultProbePlan.ExpectedErrorCode
        && _checkedErrorCodeMask ==
            NullPageFaultProbePlan.CheckedErrorCodeMask
        && _expectedCr2Raw ==
            NullPageFaultProbePlan.ExpectedCr2Raw;

    internal static NullPageFaultProbePlanResult Create(
        NullPageFaultProbePlanStatus status,
        NullPageFaultProbeRequest request)
    {
        return new NullPageFaultProbePlanResult(status, request);
    }
}

/// <summary>
/// Produces host-testable metadata for a disabled future #PF probe. It emits
/// no bytes, performs no access, and cannot invoke a probe.
/// </summary>
public static class NullPageFaultProbePlan
{
    public const ushort ExpectedPageFaultVector = 14;
    public const uint ExpectedErrorCode = 0;
    public const uint CheckedErrorCodeMask = 0x1f;
    public const ulong ExpectedCr2Raw = 0;
    public const bool EnabledByDefault = false;
    public const bool PerformsLiveFault = false;
    public const bool DereferencesPageZero = false;
    public const bool EmitsExecutableBytes = false;
    public const bool ExecutesProbe = false;
    public const bool IsGuestEvidence = false;
    public const bool ExecutionDeferredToM02014 = true;

    public static NullPageFaultProbePlanResult Plan(
        NullPageFaultProbeRequest request)
    {
        if (!request.IsInitialized)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus.InvalidRequestState,
                request);
        }

        if (!request.PageZeroPolicy
            .ProvesPageZeroAbsentInSuppliedImage)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .PageZeroPolicyUnavailable,
                request);
        }

        if (request.EnabledByDefault)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .EnabledByDefaultRejected,
                request);
        }

        if (request.ExecutionAttempted)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .ExecutionAttemptedRejected,
                request);
        }

        if (request.ExecutionRequested)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .ExecutionRequestedRejected,
                request);
        }

        if (!request.OwnedDiagnosticsReady)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .OwnedDiagnosticsUnavailable,
                request);
        }

        if (!request.PageFaultHandlerReady)
        {
            return NullPageFaultProbePlanResult.Create(
                NullPageFaultProbePlanStatus
                    .PageFaultHandlerUnavailable,
                request);
        }

        return NullPageFaultProbePlanResult.Create(
            NullPageFaultProbePlanStatus.ReadyDisabled,
            request);
    }
}
