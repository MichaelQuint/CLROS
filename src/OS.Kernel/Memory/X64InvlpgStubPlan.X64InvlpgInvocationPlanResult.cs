namespace OS.Kernel.Memory;
/// <summary>
/// One-shot checked-call plan. FunctionPointerAddress is an integer value for
/// later explicit unmanaged binding; no managed callable object or reference crosses
/// the boundary.
/// </summary>

public readonly struct X64InvlpgInvocationPlanResult
{
    public const ulong StructureSize = 88;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64InvlpgStubPlanStatus _status;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly X64InvlpgInvocationRequest _request;
    private readonly ulong _functionPointerAddress;
    private readonly ulong _plannedInvocationCount;
    private X64InvlpgInvocationPlanResult(X64InvlpgStubPlanStatus status, X64InvlpgInvocationRequest request, ulong functionPointerAddress, ulong plannedInvocationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _request = request;
        _functionPointerAddress = functionPointerAddress;
        _plannedInvocationCount = plannedInvocationCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0;
    public X64InvlpgStubPlanStatus Status => _status;
    public X64InvlpgInvocationRequest Request => _request;
    public ulong FunctionPointerAddress => _functionPointerAddress;
    public ulong PlannedInvocationCount => _plannedInvocationCount;
    public bool Succeeded => IsInitialized && _status == X64InvlpgStubPlanStatus.Success && _request.Validate() == X64InvlpgStubPlanStatus.Success && _request.EntryAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEntry) == OS.Boot.AddressValueStatus.Success && _functionPointerAddress == rawEntry && _plannedInvocationCount == 1;

    internal static X64InvlpgInvocationPlanResult Success(X64InvlpgInvocationRequest request, ulong functionPointerAddress)
    {
        return new X64InvlpgInvocationPlanResult(X64InvlpgStubPlanStatus.Success, request, functionPointerAddress, 1);
    }

    internal static X64InvlpgInvocationPlanResult Fail(X64InvlpgStubPlanStatus status, X64InvlpgInvocationRequest request = default)
    {
        return new X64InvlpgInvocationPlanResult(status, request, 0, 0);
    }
}
