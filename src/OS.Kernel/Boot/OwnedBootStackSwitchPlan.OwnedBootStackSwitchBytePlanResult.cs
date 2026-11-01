namespace OS.Boot;
/// <summary>
/// Preserves exact byte-plan emission or readback evidence.
/// </summary>

public readonly struct OwnedBootStackSwitchBytePlanResult
{
    private readonly OwnedBootStackSwitchStatus _status;
    private readonly OwnedBootStackSwitchByteOperation _operation;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly ulong _codePageBufferAddress;
    private readonly ulong _materializedPageByteLength;
    private readonly ulong _availablePageCapacity;
    private readonly ulong _stubEntryOffset;
    private readonly ulong _materializedStubByteLength;
    private readonly ulong _bytesProcessed;
    private readonly ulong _mismatchByteOffset;
    private readonly OwnedBootStackSwitchPlan _plan;
    private OwnedBootStackSwitchBytePlanResult(OwnedBootStackSwitchStatus status, OwnedBootStackSwitchByteOperation operation, ulong codePageBufferAddress, ulong materializedPageByteLength, ulong availablePageCapacity, ulong stubEntryOffset, ulong materializedStubByteLength, ulong bytesProcessed, ulong mismatchByteOffset, OwnedBootStackSwitchPlan plan)
    {
        _status = status;
        _operation = operation;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _codePageBufferAddress = codePageBufferAddress;
        _materializedPageByteLength = materializedPageByteLength;
        _availablePageCapacity = availablePageCapacity;
        _stubEntryOffset = stubEntryOffset;
        _materializedStubByteLength = materializedStubByteLength;
        _bytesProcessed = bytesProcessed;
        _mismatchByteOffset = mismatchByteOffset;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;
    public OwnedBootStackSwitchByteOperation Operation => _operation;
    public ulong CodePageBufferAddress => _codePageBufferAddress;
    public ulong MaterializedPageByteLength => _materializedPageByteLength;
    public ulong AvailablePageCapacity => _availablePageCapacity;
    public ulong StubEntryOffset => _stubEntryOffset;
    public ulong MaterializedStubByteLength => _materializedStubByteLength;
    public ulong BytesProcessed => _bytesProcessed;
    public ulong MismatchByteOffset => _mismatchByteOffset;
    public OwnedBootStackSwitchPlan Plan => _plan;
    public bool Succeeded => _status == OwnedBootStackSwitchStatus.Success && (_operation == OwnedBootStackSwitchByteOperation.Emit || _operation == OwnedBootStackSwitchByteOperation.Verify) && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _reserved5 == 0 && _plan.IsValid && _codePageBufferAddress != 0 && _codePageBufferAddress == _plan.CodeAllocation.VirtualStart && _materializedPageByteLength == _plan.CodeAllocation.ByteLength && _availablePageCapacity >= _materializedPageByteLength && _stubEntryOffset == _plan.StubEntryOffset && _materializedStubByteLength == OwnedBootStackSwitchPlan.RequiredStubByteLength && _bytesProcessed == _materializedStubByteLength && _mismatchByteOffset == OwnedBootStackSwitchPlan.NoIndex;

    internal static OwnedBootStackSwitchBytePlanResult Success(OwnedBootStackSwitchByteOperation operation, ulong codePageBufferAddress, ulong availablePageCapacity, ulong materializedPageByteLength, OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchBytePlanResult(OwnedBootStackSwitchStatus.Success, operation, codePageBufferAddress, materializedPageByteLength, availablePageCapacity, plan.StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, OwnedBootStackSwitchPlan.RequiredStubByteLength, OwnedBootStackSwitchPlan.NoIndex, plan);
    }

    internal static OwnedBootStackSwitchBytePlanResult Fail(OwnedBootStackSwitchStatus status, OwnedBootStackSwitchByteOperation operation, ulong codePageBufferAddress, ulong availablePageCapacity, ulong materializedPageByteLength, OwnedBootStackSwitchPlan plan, ulong mismatchByteOffset = OwnedBootStackSwitchPlan.NoIndex)
    {
        return new OwnedBootStackSwitchBytePlanResult(status, operation, codePageBufferAddress, materializedPageByteLength, availablePageCapacity, plan.StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, 0, mismatchByteOffset, plan);
    }
}
