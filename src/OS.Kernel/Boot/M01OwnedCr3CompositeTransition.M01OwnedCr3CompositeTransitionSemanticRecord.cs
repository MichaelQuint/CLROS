namespace OS.Boot;
/// <summary>
/// One stable semantic interval in the relocation-free byte plan.
/// </summary>

public readonly struct M01OwnedCr3CompositeTransitionSemanticRecord
{
    public const ulong StructureSize = 24;
    public const uint NoBranchTarget = ~0U;
    private readonly M01OwnedCr3CompositeTransitionOperation _operation;
    private readonly M01OwnedCr3CompositeTransitionRecoveryClass _recoveryClass;
    private readonly ushort _reserved;
    private readonly uint _byteOffset;
    private readonly uint _byteLength;
    private readonly uint _branchTargetOffset;
    private readonly ulong _stateOffsetOrConstant;
    internal M01OwnedCr3CompositeTransitionSemanticRecord(M01OwnedCr3CompositeTransitionOperation operation, M01OwnedCr3CompositeTransitionRecoveryClass recoveryClass, uint byteOffset, uint byteLength, uint branchTargetOffset, ulong stateOffsetOrConstant)
    {
        _operation = operation;
        _recoveryClass = recoveryClass;
        _reserved = 0;
        _byteOffset = byteOffset;
        _byteLength = byteLength;
        _branchTargetOffset = branchTargetOffset;
        _stateOffsetOrConstant = stateOffsetOrConstant;
    }

    public M01OwnedCr3CompositeTransitionOperation Operation => _operation;
    public M01OwnedCr3CompositeTransitionRecoveryClass RecoveryClass => _recoveryClass;
    public uint ByteOffset => _byteOffset;
    public uint ByteLength => _byteLength;
    public uint BranchTargetOffset => _branchTargetOffset;
    public ulong StateOffsetOrConstant => _stateOffsetOrConstant;
    internal bool ReservedFieldIsZero => _reserved == 0;
}
