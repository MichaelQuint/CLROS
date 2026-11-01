namespace OS.Kernel.Diagnostics;
public readonly struct ExistingIn8Out8StubPageState
{
    public const ulong StructureSize = 48;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly ExistingIn8Out8StubPagePhase _phase;
    private readonly ulong _transitionVersion;
    private readonly ulong _publicationEpoch;
    private readonly ulong _modeledPageAddress;
    private readonly OS.Kernel.Memory.PageProtection _currentProtection;
    private readonly byte _finalizationCount;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly ulong _reserved2;
    private ExistingIn8Out8StubPageState(ExistingIn8Out8StubPagePhase phase, ulong transitionVersion, ulong publicationEpoch, ulong modeledPageAddress, OS.Kernel.Memory.PageProtection currentProtection, byte finalizationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _phase = phase;
        _transitionVersion = transitionVersion;
        _publicationEpoch = publicationEpoch;
        _modeledPageAddress = modeledPageAddress;
        _currentProtection = currentProtection;
        _finalizationCount = finalizationCount;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0;
    public ExistingIn8Out8StubPagePhase Phase => _phase;
    public ulong TransitionVersion => _transitionVersion;
    public ulong PublicationEpoch => _publicationEpoch;
    public ulong ModeledPageAddress => _modeledPageAddress;
    public OS.Kernel.Memory.PageProtection CurrentProtection => _currentProtection;
    public byte FinalizationCount => _finalizationCount;

    public bool IsValid
    {
        get
        {
            if (!IsInitialized)
            {
                return false;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.Uninitialized)
            {
                return _transitionVersion == 0 && _publicationEpoch == 0 && _modeledPageAddress == 0 && _currentProtection == OS.Kernel.Memory.PageProtection.None && _finalizationCount == 0;
            }

            if (_publicationEpoch == 0 || !ExistingIn8Out8StubBytesRWPagesFinalize.IsValidModeledPageAddress(_modeledPageAddress))
            {
                return false;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.Materialized)
            {
                return _transitionVersion == 1 && _currentProtection == ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection && _finalizationCount == 0;
            }

            if (_phase == ExistingIn8Out8StubPagePhase.ReadBackVerified)
            {
                return _transitionVersion == 2 && _currentProtection == ExistingIn8Out8StubBytesRWPagesFinalize.ReadWriteProtection && _finalizationCount == 0;
            }

            return _phase == ExistingIn8Out8StubPagePhase.FinalizationAudited && _transitionVersion == 3 && _currentProtection == ExistingIn8Out8StubBytesRWPagesFinalize.ReadExecuteProtection && _finalizationCount == 1;
        }
    }

    public static ExistingIn8Out8StubPageState Create()
    {
        return new ExistingIn8Out8StubPageState(ExistingIn8Out8StubPagePhase.Uninitialized, transitionVersion: 0, publicationEpoch: 0, modeledPageAddress: 0, OS.Kernel.Memory.PageProtection.None, finalizationCount: 0);
    }

    internal static ExistingIn8Out8StubPageState Advance(ExistingIn8Out8StubPagePhase phase, ulong transitionVersion, ulong publicationEpoch, ulong modeledPageAddress, OS.Kernel.Memory.PageProtection currentProtection, byte finalizationCount)
    {
        return new ExistingIn8Out8StubPageState(phase, transitionVersion, publicationEpoch, modeledPageAddress, currentProtection, finalizationCount);
    }
}
#if !NOSTDLIB_EFI
#endif
