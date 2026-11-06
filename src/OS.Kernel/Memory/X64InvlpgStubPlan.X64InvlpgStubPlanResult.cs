namespace OS.Kernel.Memory;
/// <summary>
/// Exact byte-plan and readback result. It is ordinary-data evidence and
/// contains no claim that INVLPG executed.
/// </summary>

public readonly struct X64InvlpgStubPlanResult
{
    public const ulong StructureSize = 104;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64InvlpgStubPlanStatus _status;
    private readonly X64InvlpgStubPhase _phase;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly OS.Boot.VirtualAddress _codePageStart;
    private readonly OS.Boot.VirtualAddress _codePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _entryOffset;
    private readonly X64TlbInvalidationRecord _target;
    private readonly ulong _availableCapacity;
    private readonly ulong _requiredByteCount;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly ulong _instructionCount;
    private X64InvlpgStubPlanResult(X64InvlpgStubPlanStatus status, X64InvlpgStubPhase phase, OS.Boot.VirtualAddress codePageStart, OS.Boot.VirtualAddress codePageEndExclusive, OS.Boot.VirtualAddress entryAddress, ulong entryOffset, X64TlbInvalidationRecord target, ulong availableCapacity, ulong requiredByteCount, ulong bytesWrittenOrVerified, ulong instructionCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _codePageStart = codePageStart;
        _codePageEndExclusive = codePageEndExclusive;
        _entryAddress = entryAddress;
        _entryOffset = entryOffset;
        _target = target;
        _availableCapacity = availableCapacity;
        _requiredByteCount = requiredByteCount;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _instructionCount = instructionCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public X64InvlpgStubPlanStatus Status => _status;
    public X64InvlpgStubPhase Phase => _phase;
    public OS.Boot.VirtualAddress CodePageStart => _codePageStart;
    public OS.Boot.VirtualAddress CodePageEndExclusive => _codePageEndExclusive;
    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;
    public ulong EntryOffset => _entryOffset;
    public X64TlbInvalidationRecord Target => _target;
    public ulong AvailableCapacity => _availableCapacity;
    public ulong RequiredByteCount => _requiredByteCount;
    public ulong BytesWrittenOrVerified => _bytesWrittenOrVerified;
    public ulong InstructionCount => _instructionCount;
    public bool ReadBackVerified => Succeeded && _phase == X64InvlpgStubPhase.ReadBackVerified;
    public bool Succeeded => IsInitialized && _status == X64InvlpgStubPlanStatus.Success && (_phase == X64InvlpgStubPhase.Materialized || _phase == X64InvlpgStubPhase.ReadBackVerified) && _codePageStart.IsValid && _codePageStart.IsAligned(X64InvlpgStubPlan.PageSize) && _codePageStart.TryGetEndExclusive(X64InvlpgStubPlan.PageSize, out OS.Boot.VirtualAddress expectedEnd) == OS.Boot.AddressValueStatus.Success && expectedEnd.Equals(_codePageEndExclusive) && _entryAddress.IsValid && _entryAddress.IsAligned(X64InvlpgStubPlan.EntryAlignment) && _entryOffset < X64InvlpgStubPlan.PageSize && _entryOffset <= X64InvlpgStubPlan.PageSize - X64InvlpgStubPlan.RequiredByteLength && _codePageStart.TryAdd(_entryOffset, out OS.Boot.VirtualAddress expectedEntry) == OS.Boot.AddressValueStatus.Success && expectedEntry.Equals(_entryAddress) && _target.Validate() == X64TlbInvalidationStatus.Success && _target.Ordinal == 0 && _availableCapacity == X64InvlpgStubPlan.PageSize && _requiredByteCount == X64InvlpgStubPlan.RequiredByteLength && _bytesWrittenOrVerified == _requiredByteCount && _instructionCount == X64InvlpgStubPlan.AuditedInstructionCount;

    internal static X64InvlpgStubPlanResult Success(X64InvlpgStubPhase phase, OS.Boot.VirtualAddress codePageStart, OS.Boot.VirtualAddress codePageEndExclusive, OS.Boot.VirtualAddress entryAddress, ulong entryOffset, X64TlbInvalidationRecord target, ulong availableCapacity)
    {
        return new X64InvlpgStubPlanResult(X64InvlpgStubPlanStatus.Success, phase, codePageStart, codePageEndExclusive, entryAddress, entryOffset, target, availableCapacity, X64InvlpgStubPlan.RequiredByteLength, X64InvlpgStubPlan.RequiredByteLength, X64InvlpgStubPlan.AuditedInstructionCount);
    }

    internal static X64InvlpgStubPlanResult Fail(X64InvlpgStubPlanStatus status, X64TlbInvalidationRecord target = default, ulong availableCapacity = 0)
    {
        return new X64InvlpgStubPlanResult(status, X64InvlpgStubPhase.Uninitialized, default, default, default, 0, target, availableCapacity, X64InvlpgStubPlan.RequiredByteLength, 0, X64InvlpgStubPlan.AuditedInstructionCount);
    }
}
