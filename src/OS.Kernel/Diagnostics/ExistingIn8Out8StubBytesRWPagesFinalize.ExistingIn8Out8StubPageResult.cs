namespace OS.Kernel.Diagnostics;
public readonly struct ExistingIn8Out8StubPageResult
{
    public const ulong StructureSize = 112;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly ExistingIn8Out8StubPageStatus _status;
    private readonly ExistingIn8Out8StubPagePhase _phase;
    private readonly byte _liveProtection;
    private readonly byte _liveExecution;
    private readonly byte _finalizationCount;
    private readonly OS.Kernel.Memory.PageProtection _initialProtection;
    private readonly OS.Kernel.Memory.PageProtection _finalProtection;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ulong _modeledPageAddress;
    private readonly ulong _pageByteCount;
    private readonly ulong _safeOffset;
    private readonly ulong _out8Offset;
    private readonly ulong _in8Offset;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly ulong _instructionByteCount;
    private readonly ulong _occupiedExtent;
    private readonly ulong _fnv1a64;
    private readonly ulong _publicationEpoch;
    private readonly ulong _transitionVersion;
    private readonly ulong _reserved2;
    private ExistingIn8Out8StubPageResult(ExistingIn8Out8StubPageStatus status, ExistingIn8Out8StubPagePhase phase, ulong modeledPageAddress, ulong bytesWrittenOrVerified, ulong publicationEpoch, ulong transitionVersion, byte finalizationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _liveProtection = 0;
        _liveExecution = 0;
        _finalizationCount = finalizationCount;
        _initialProtection = ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection;
        _finalProtection = ExistingIn8Out8StubBytesRWPagesFinalize.ReadExecuteProtection;
        _reserved0 = 0;
        _reserved1 = 0;
        _modeledPageAddress = modeledPageAddress;
        _pageByteCount = ExistingIn8Out8StubBytesRWPagesFinalize.PageSize;
        _safeOffset = ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset;
        _out8Offset = ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset;
        _in8Offset = ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _instructionByteCount = ExistingIn8Out8StubBytesRWPagesFinalize.InstructionByteCount;
        _occupiedExtent = ExistingIn8Out8StubBytesRWPagesFinalize.OccupiedExtent;
        _fnv1a64 = ExistingIn8Out8StubBytesRWPagesFinalize.ExpectedFnv1a64;
        _publicationEpoch = publicationEpoch;
        _transitionVersion = transitionVersion;
        _reserved2 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public ExistingIn8Out8StubPageStatus Status => _status;
    public ExistingIn8Out8StubPagePhase Phase => _phase;
    public ulong ModeledPageAddress => _modeledPageAddress;
    public ulong PageByteCount => _pageByteCount;
    public ulong SafeOffset => _safeOffset;
    public ulong Out8Offset => _out8Offset;
    public ulong In8Offset => _in8Offset;
    public ulong BytesWrittenOrVerified => _bytesWrittenOrVerified;
    public ulong InstructionByteCount => _instructionByteCount;
    public ulong OccupiedExtent => _occupiedExtent;
    public ulong Fnv1a64 => _fnv1a64;
    public ulong PublicationEpoch => _publicationEpoch;
    public ulong TransitionVersion => _transitionVersion;
    public OS.Kernel.Memory.PageProtection InitialProtection => _initialProtection;
    public OS.Kernel.Memory.PageProtection FinalProtection => _finalProtection;
    public byte FinalizationCount => _finalizationCount;
    public bool LiveProtection => _liveProtection != 0;
    public bool LiveExecution => _liveExecution != 0;
    public bool ReadBackVerified => Succeeded && (_phase == ExistingIn8Out8StubPagePhase.ReadBackVerified || _phase == ExistingIn8Out8StubPagePhase.FinalizationAudited);
    public bool FinalizationAudited => Succeeded && _phase == ExistingIn8Out8StubPagePhase.FinalizationAudited;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized || _status != ExistingIn8Out8StubPageStatus.Success || _phase < ExistingIn8Out8StubPagePhase.Materialized || _phase > ExistingIn8Out8StubPagePhase.FinalizationAudited || !ExistingIn8Out8StubBytesRWPagesFinalize.IsValidModeledPageAddress(_modeledPageAddress) || _pageByteCount != ExistingIn8Out8StubBytesRWPagesFinalize.PageSize || _safeOffset != ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset || _out8Offset != ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset || _in8Offset != ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset || _bytesWrittenOrVerified != ExistingIn8Out8StubBytesRWPagesFinalize.PageSize || _instructionByteCount != ExistingIn8Out8StubBytesRWPagesFinalize.InstructionByteCount || _occupiedExtent != ExistingIn8Out8StubBytesRWPagesFinalize.OccupiedExtent || _fnv1a64 != ExistingIn8Out8StubBytesRWPagesFinalize.ExpectedFnv1a64 || _publicationEpoch == 0 || _initialProtection != ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection || _finalProtection != ExistingIn8Out8StubBytesRWPagesFinalize.ReadExecuteProtection || LiveProtection || LiveExecution)
            {
                return false;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.Materialized)
            {
                return _transitionVersion == 1 && _finalizationCount == 0;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.ReadBackVerified)
            {
                return _transitionVersion == 2 && _finalizationCount == 0;
            }

            return _transitionVersion == 3 && _finalizationCount == 1;
        }
    }

    internal static ExistingIn8Out8StubPageResult Success(ExistingIn8Out8StubPagePhase phase, ulong modeledPageAddress, ulong publicationEpoch, ulong transitionVersion, byte finalizationCount)
    {
        return new ExistingIn8Out8StubPageResult(ExistingIn8Out8StubPageStatus.Success, phase, modeledPageAddress, ExistingIn8Out8StubBytesRWPagesFinalize.PageSize, publicationEpoch, transitionVersion, finalizationCount);
    }

    internal static ExistingIn8Out8StubPageResult Fail(ExistingIn8Out8StubPageStatus status)
    {
        return new ExistingIn8Out8StubPageResult(status, ExistingIn8Out8StubPagePhase.Uninitialized, modeledPageAddress: 0, bytesWrittenOrVerified: 0, publicationEpoch: 0, transitionVersion: 0, finalizationCount: 0);
    }
}
#if !NOSTDLIB_EFI
#endif
