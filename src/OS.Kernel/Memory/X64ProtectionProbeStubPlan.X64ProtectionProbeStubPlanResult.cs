namespace OS.Kernel.Memory;
/// <summary>
/// Exact result of writing or validating the six-byte constant-return stream.
/// </summary>

public readonly struct X64ProtectionProbeStubPlanResult
{
    public const ulong StructureSize = 80;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64ProtectionProbeStatus _status;
    private readonly X64ProtectionProbeStubPhase _phase;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly OS.Boot.VirtualAddress _probePageStart;
    private readonly OS.Boot.VirtualAddress _probePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _entryOffset;
    private readonly ulong _availableCapacity;
    private readonly ulong _requiredByteCount;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly uint _returnValue;
    private readonly uint _instructionCount;
    private X64ProtectionProbeStubPlanResult(X64ProtectionProbeStatus status, X64ProtectionProbeStubPhase phase, OS.Boot.VirtualAddress probePageStart, OS.Boot.VirtualAddress probePageEndExclusive, OS.Boot.VirtualAddress entryAddress, ulong entryOffset, ulong availableCapacity, ulong requiredByteCount, ulong bytesWrittenOrVerified, uint returnValue, uint instructionCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _probePageStart = probePageStart;
        _probePageEndExclusive = probePageEndExclusive;
        _entryAddress = entryAddress;
        _entryOffset = entryOffset;
        _availableCapacity = availableCapacity;
        _requiredByteCount = requiredByteCount;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _returnValue = returnValue;
        _instructionCount = instructionCount;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public X64ProtectionProbeStatus Status => _status;
    public X64ProtectionProbeStubPhase Phase => _phase;
    public OS.Boot.VirtualAddress ProbePageStart => _probePageStart;
    public OS.Boot.VirtualAddress ProbePageEndExclusive => _probePageEndExclusive;
    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;
    public ulong EntryOffset => _entryOffset;
    public ulong AvailableCapacity => _availableCapacity;
    public ulong RequiredByteCount => _requiredByteCount;
    public ulong BytesWrittenOrVerified => _bytesWrittenOrVerified;
    public uint ReturnValue => _returnValue;
    public uint InstructionCount => _instructionCount;
    public bool ReadBackVerified => Succeeded && _phase == X64ProtectionProbeStubPhase.ReadBackVerified;
    public bool Succeeded => IsInitialized && _status == X64ProtectionProbeStatus.Success && (_phase == X64ProtectionProbeStubPhase.Materialized || _phase == X64ProtectionProbeStubPhase.ReadBackVerified) && _probePageStart.IsValid && _probePageStart.IsAligned(X64ProtectionProbeStubPlan.PageSize) && _probePageStart.TryGetEndExclusive(X64ProtectionProbeStubPlan.PageSize, out OS.Boot.VirtualAddress expectedEnd) == OS.Boot.AddressValueStatus.Success && expectedEnd.Equals(_probePageEndExclusive) && _entryOffset <= X64ProtectionProbeStubPlan.PageSize - X64ProtectionProbeStubPlan.RequiredByteLength && (_entryOffset & (X64ProtectionProbeStubPlan.EntryAlignment - 1)) == 0 && _probePageStart.TryAdd(_entryOffset, out OS.Boot.VirtualAddress expectedEntry) == OS.Boot.AddressValueStatus.Success && expectedEntry.Equals(_entryAddress) && _entryAddress.IsAligned(X64ProtectionProbeStubPlan.EntryAlignment) && _availableCapacity == X64ProtectionProbeStubPlan.PageSize && _requiredByteCount == X64ProtectionProbeStubPlan.RequiredByteLength && _bytesWrittenOrVerified == _requiredByteCount && _returnValue == X64ProtectionProbeStubPlan.ExpectedReturnValue && _instructionCount == X64ProtectionProbeStubPlan.AuditedInstructionCount;

    internal static X64ProtectionProbeStubPlanResult Success(X64ProtectionProbeStubPhase phase, OS.Boot.VirtualAddress probePageStart, OS.Boot.VirtualAddress probePageEndExclusive, OS.Boot.VirtualAddress entryAddress, ulong entryOffset, ulong availableCapacity)
    {
        return new X64ProtectionProbeStubPlanResult(X64ProtectionProbeStatus.Success, phase, probePageStart, probePageEndExclusive, entryAddress, entryOffset, availableCapacity, X64ProtectionProbeStubPlan.RequiredByteLength, X64ProtectionProbeStubPlan.RequiredByteLength, X64ProtectionProbeStubPlan.ExpectedReturnValue, (uint)X64ProtectionProbeStubPlan.AuditedInstructionCount);
    }

    internal static X64ProtectionProbeStubPlanResult Fail(X64ProtectionProbeStatus status, ulong availableCapacity = 0)
    {
        return new X64ProtectionProbeStubPlanResult(status, X64ProtectionProbeStubPhase.Uninitialized, default, default, default, 0, availableCapacity, X64ProtectionProbeStubPlan.RequiredByteLength, 0, X64ProtectionProbeStubPlan.ExpectedReturnValue, (uint)X64ProtectionProbeStubPlan.AuditedInstructionCount);
    }
}
