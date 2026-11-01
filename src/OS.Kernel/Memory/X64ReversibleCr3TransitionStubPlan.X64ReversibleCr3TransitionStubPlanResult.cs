namespace OS.Kernel.Memory;
/// <summary>
/// Result of exact byte emission or readback validation.
/// </summary>

public readonly struct X64ReversibleCr3TransitionStubPlanResult
{
    public const ulong StructureSize = 72;
    public const ulong NoIndex = ~0UL;
    private readonly byte _isInitialized;
    private readonly X64ReversibleCr3TransitionStubPlanStatus _status;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly ulong _bytesWritten;
    private readonly ulong _requiredBytes;
    private readonly ulong _entryAddress;
    private readonly ulong _invalidByteIndex;
    private readonly ulong _sourceCount;
    private readonly ulong _sourceCapacity;
    private readonly ulong _externalPatchCount;
    private readonly ulong _instructionCount;
    internal X64ReversibleCr3TransitionStubPlanResult(X64ReversibleCr3TransitionStubPlanStatus status, ulong bytesWritten, ulong requiredBytes, ulong entryAddress, ulong invalidByteIndex, ulong sourceCount, ulong sourceCapacity, ulong instructionCount)
    {
        _isInitialized = 1;
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _bytesWritten = bytesWritten;
        _requiredBytes = requiredBytes;
        _entryAddress = entryAddress;
        _invalidByteIndex = invalidByteIndex;
        _sourceCount = sourceCount;
        _sourceCapacity = sourceCapacity;
        _externalPatchCount = 0;
        _instructionCount = instructionCount;
    }

    public bool IsInitialized => _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && (byte)_status <= (byte)X64ReversibleCr3TransitionStubPlanStatus.PostRestoreCanaryMismatch && _status != X64ReversibleCr3TransitionStubPlanStatus.Uninitialized && _externalPatchCount == 0;
    public bool Succeeded => IsInitialized && _status == X64ReversibleCr3TransitionStubPlanStatus.Success && _bytesWritten == X64ReversibleCr3TransitionStubPlan.RequiredByteLength && _requiredBytes == X64ReversibleCr3TransitionStubPlan.RequiredByteLength && _entryAddress != 0 && (_entryAddress & (X64ReversibleCr3TransitionStubPlan.EntryAlignment - 1)) == 0 && _entryAddress + _requiredBytes >= _entryAddress && _invalidByteIndex == NoIndex && _sourceCount == _requiredBytes && _sourceCapacity >= _sourceCount && _sourceCapacity <= X64ReversibleCr3TransitionStubPlan.MaximumByteCapacity && _externalPatchCount == 0 && _instructionCount == X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount;
    public X64ReversibleCr3TransitionStubPlanStatus Status => _status;
    public ulong BytesWritten => _bytesWritten;
    public ulong RequiredBytes => _requiredBytes;
    public ulong EntryAddress => _entryAddress;
    public ulong InvalidByteIndex => _invalidByteIndex;
    public ulong SourceCount => _sourceCount;
    public ulong SourceCapacity => _sourceCapacity;
    public ulong ExternalPatchCount => _externalPatchCount;
    public ulong InstructionCount => _instructionCount;
    public bool HasExternalRelocations => false;
}
