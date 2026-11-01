namespace OS.Kernel.Memory;
/// <summary>
/// Identity-mapped, caller-owned input and readback state consumed by the
/// emitted Windows-x64 ABI stub. This is an unmanaged POD. The observed-state
/// factory exists so host tests and a runner can audit evidence without ever
/// invoking privileged bytes.
/// </summary>

public struct X64ReversibleCr3TransitionRuntimeState
{
    public const uint StructureSize = 184;
    public const ushort StructureVersion = 1;
    private uint _structureSize;
    private ushort _structureVersion;
    private byte _isInitialized;
    private byte _reserved0;
    private ulong _ownedCr3Raw;
    private ulong _stackStart;
    private ulong _stackEndExclusive;
    private ulong _canaryAddress;
    private ulong _expectedCanary;
    private ulong _replacementCanary;
    private ulong _originalRflags;
    private ulong _originalCr3Raw;
    private ulong _originalCr4Raw;
    private ulong _clearedCr4Readback;
    private ulong _ownedCr3Readback;
    private ulong _canaryBefore;
    private ulong _canaryAfter;
    private ulong _firmwareCr3Readback;
    private ulong _restoredCr4Readback;
    private ulong _cliRflagsReadback;
    private ulong _restoredRflagsReadback;
    private ulong _entryRsp;
    private ulong _exitRsp;
    private ulong _postRestoreCanaryReadback;
    private uint _firmwareCallbackCount;
    private X64ReversibleCr3TransitionRuntimeStatus _runtimeStatus;
    private X64ReversibleCr3TransitionCheckpoint _checkpoint;
    private ushort _reserved1;
    private ulong _reserved2;
    public X64ReversibleCr3TransitionRuntimeState(ulong ownedCr3Raw, ulong stackStart, ulong stackEndExclusive, ulong canaryAddress, ulong expectedCanary, ulong replacementCanary)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _ownedCr3Raw = ownedCr3Raw;
        _stackStart = stackStart;
        _stackEndExclusive = stackEndExclusive;
        _canaryAddress = canaryAddress;
        _expectedCanary = expectedCanary;
        _replacementCanary = replacementCanary;
        _originalRflags = 0;
        _originalCr3Raw = 0;
        _originalCr4Raw = 0;
        _clearedCr4Readback = 0;
        _ownedCr3Readback = 0;
        _canaryBefore = 0;
        _canaryAfter = 0;
        _firmwareCr3Readback = 0;
        _restoredCr4Readback = 0;
        _cliRflagsReadback = 0;
        _restoredRflagsReadback = 0;
        _entryRsp = 0;
        _exitRsp = 0;
        _postRestoreCanaryReadback = 0;
        _firmwareCallbackCount = 0;
        _runtimeStatus = X64ReversibleCr3TransitionRuntimeStatus.Uninitialized;
        _checkpoint = X64ReversibleCr3TransitionCheckpoint.Uninitialized;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    private X64ReversibleCr3TransitionRuntimeState(ulong ownedCr3Raw, ulong stackStart, ulong stackEndExclusive, ulong canaryAddress, ulong expectedCanary, ulong replacementCanary, ulong originalRflags, ulong originalCr3Raw, ulong originalCr4Raw, ulong clearedCr4Readback, ulong ownedCr3Readback, ulong canaryBefore, ulong canaryAfter, ulong firmwareCr3Readback, ulong restoredCr4Readback, ulong cliRflagsReadback, ulong restoredRflagsReadback, ulong entryRsp, ulong exitRsp, ulong postRestoreCanaryReadback, uint firmwareCallbackCount, X64ReversibleCr3TransitionRuntimeStatus runtimeStatus, X64ReversibleCr3TransitionCheckpoint checkpoint)
    {
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _ownedCr3Raw = ownedCr3Raw;
        _stackStart = stackStart;
        _stackEndExclusive = stackEndExclusive;
        _canaryAddress = canaryAddress;
        _expectedCanary = expectedCanary;
        _replacementCanary = replacementCanary;
        _originalRflags = originalRflags;
        _originalCr3Raw = originalCr3Raw;
        _originalCr4Raw = originalCr4Raw;
        _clearedCr4Readback = clearedCr4Readback;
        _ownedCr3Readback = ownedCr3Readback;
        _canaryBefore = canaryBefore;
        _canaryAfter = canaryAfter;
        _firmwareCr3Readback = firmwareCr3Readback;
        _restoredCr4Readback = restoredCr4Readback;
        _cliRflagsReadback = cliRflagsReadback;
        _restoredRflagsReadback = restoredRflagsReadback;
        _entryRsp = entryRsp;
        _exitRsp = exitRsp;
        _postRestoreCanaryReadback = postRestoreCanaryReadback;
        _firmwareCallbackCount = firmwareCallbackCount;
        _runtimeStatus = runtimeStatus;
        _checkpoint = checkpoint;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && (byte)_runtimeStatus <= (byte)X64ReversibleCr3TransitionRuntimeStatus.PostRestoreCanaryMismatch && (byte)_checkpoint <= (byte)X64ReversibleCr3TransitionCheckpoint.Returned;
    public ulong OwnedCr3Raw => _ownedCr3Raw;
    public ulong StackStart => _stackStart;
    public ulong StackEndExclusive => _stackEndExclusive;
    public ulong CanaryAddress => _canaryAddress;
    public ulong ExpectedCanary => _expectedCanary;
    public ulong ReplacementCanary => _replacementCanary;
    public ulong OriginalRflags => _originalRflags;
    public ulong OriginalCr3Raw => _originalCr3Raw;
    public ulong OriginalCr4Raw => _originalCr4Raw;
    public ulong ClearedCr4Readback => _clearedCr4Readback;
    public ulong OwnedCr3Readback => _ownedCr3Readback;
    public ulong CanaryBefore => _canaryBefore;
    public ulong CanaryAfter => _canaryAfter;
    public ulong FirmwareCr3Readback => _firmwareCr3Readback;
    public ulong RestoredCr4Readback => _restoredCr4Readback;
    public ulong CliRflagsReadback => _cliRflagsReadback;
    public ulong RestoredRflagsReadback => _restoredRflagsReadback;
    public ulong EntryRsp => _entryRsp;
    public ulong ExitRsp => _exitRsp;
    public ulong PostRestoreCanaryReadback => _postRestoreCanaryReadback;
    public uint FirmwareCallbackCount => _firmwareCallbackCount;
    public X64ReversibleCr3TransitionRuntimeStatus RuntimeStatus => _runtimeStatus;
    public X64ReversibleCr3TransitionCheckpoint Checkpoint => _checkpoint;

    public static X64ReversibleCr3TransitionRuntimeState CreateObserved(ulong ownedCr3Raw, ulong stackStart, ulong stackEndExclusive, ulong canaryAddress, ulong expectedCanary, ulong replacementCanary, ulong originalRflags, ulong originalCr3Raw, ulong originalCr4Raw, ulong clearedCr4Readback, ulong ownedCr3Readback, ulong canaryBefore, ulong canaryAfter, ulong firmwareCr3Readback, ulong restoredCr4Readback, ulong cliRflagsReadback, ulong restoredRflagsReadback, ulong entryRsp, ulong exitRsp, ulong postRestoreCanaryReadback, uint firmwareCallbackCount, X64ReversibleCr3TransitionRuntimeStatus runtimeStatus, X64ReversibleCr3TransitionCheckpoint checkpoint)
    {
        return new X64ReversibleCr3TransitionRuntimeState(ownedCr3Raw, stackStart, stackEndExclusive, canaryAddress, expectedCanary, replacementCanary, originalRflags, originalCr3Raw, originalCr4Raw, clearedCr4Readback, ownedCr3Readback, canaryBefore, canaryAfter, firmwareCr3Readback, restoredCr4Readback, cliRflagsReadback, restoredRflagsReadback, entryRsp, exitRsp, postRestoreCanaryReadback, firmwareCallbackCount, runtimeStatus, checkpoint);
    }
}
