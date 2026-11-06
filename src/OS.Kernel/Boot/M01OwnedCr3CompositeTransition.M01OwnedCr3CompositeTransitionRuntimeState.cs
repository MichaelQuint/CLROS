namespace OS.Boot;
/// <summary>
/// Identity-mapped, caller-owned input and readback POD consumed by the
/// emitted Windows-x64 ABI stream. Raw addresses exist here only at the
/// register boundary.
/// </summary>

public struct M01OwnedCr3CompositeTransitionRuntimeState
{
    public const uint StructureSize = 480;
    public const ushort StructureVersion = 1;
    private uint _structureSize;
    private ushort _structureVersion;
    private byte _isInitialized;
    private byte _reserved0;
    private ulong _ownedCr3Raw;
    private ulong _tablePageArenaStart;
    private ulong _tablePageArenaEndExclusive;
    private ulong _tableStateArenaStart;
    private ulong _tableStateArenaEndExclusive;
    private ulong _compositeCodeStart;
    private ulong _compositeCodeEndExclusive;
    private ulong _continuationPageStart;
    private ulong _continuationPageEndExclusive;
    private ulong _expectedReturnAddress;
    private ulong _stackStart;
    private ulong _stackEndExclusive;
    private ulong _probePageStart;
    private ulong _probePageEndExclusive;
    private ulong _probeEntry;
    private ulong _expectedProbePteAddress;
    private ulong _initialProbePteRaw;
    private ulong _finalProbePteRaw;
    private ulong _canaryAddress;
    private ulong _expectedCanary;
    private ulong _replacementCanary;
    private uint _expectedProbeReturn;
    private uint _prohibitedFirmwareCallbackCount;
    private ulong _expectedSelfAddress;
    // The low bits carry the audited critical-page count. The remaining
    // permitted bits bind the exact PDPT, PD, and PT page ordinals followed
    // by the prepared probe walk. Keeping this as one immutable word
    // preserves the v1 POD size while preventing an arena-valid alias chain
    // from being promoted as the prepared hierarchy.
    private ulong _expectedProbeHierarchyIdentity;
    private ulong _originalRflags;
    private ulong _originalCr0;
    private ulong _originalCr3Raw;
    private ulong _originalCr4Raw;
    private ulong _originalEfer;
    private ulong _physicalAddressMask;
    private ulong _entryRsp;
    private ulong _observedReturnAddress;
    private ulong _observedRip;
    private ulong _cliRflagsReadback;
    private ulong _clearedCr4Readback;
    private ulong _ownedCr3Readback;
    private ulong _canaryBefore;
    private ulong _canaryAfter;
    private ulong _probePml4e;
    private ulong _probePdpte;
    private ulong _probePde;
    private ulong _probePteBefore;
    private ulong _probePteAfter;
    private ulong _invlpgTarget;
    private ulong _probeReturn;
    private ulong _pageZeroPml4e;
    private ulong _pageZeroPdpte;
    private ulong _pageZeroPde;
    private ulong _pageZeroPte;
    private ulong _committedProbePteReadback;
    private ulong _firmwareCr3Readback;
    private ulong _postRestoreCanaryReadback;
    private ulong _restoredCr4Readback;
    private ulong _restoredRflagsIntent;
    private ulong _exitRsp;
    private ulong _invlpgCount;
    private ulong _probeInvocationCount;
    private M01OwnedCr3CompositeTransitionRuntimeStatus _runtimeStatus;
    private M01OwnedCr3CompositeTransitionCheckpoint _checkpoint;
    private M01OwnedCr3CompositeTransitionPageZeroLevel _pageZeroLevel;
    private byte _mutationCommitted;
    private byte _pgeWasCleared;
    private byte _ownedRootWasLoaded;
    private byte _probeReturned;
    private M01OwnedCr3CompositeTransitionCheckpoint _progressCheckpoint;
    private ulong _reservedOutput1;
    internal M01OwnedCr3CompositeTransitionRuntimeState(ulong ownedCr3Raw, ulong tablePageArenaStart, ulong tablePageArenaEndExclusive, ulong tableStateArenaStart, ulong tableStateArenaEndExclusive, ulong compositeCodeStart, ulong compositeCodeEndExclusive, ulong continuationPageStart, ulong continuationPageEndExclusive, ulong expectedReturnAddress, ulong stackStart, ulong stackEndExclusive, ulong probePageStart, ulong probePageEndExclusive, ulong probeEntry, ulong expectedProbePteAddress, ulong initialProbePteRaw, ulong finalProbePteRaw, ulong canaryAddress, ulong expectedCanary, ulong replacementCanary, uint expectedProbeReturn, ulong expectedSelfAddress, ulong expectedProbeHierarchyIdentity)
    {
        this = default;
        _structureSize = StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _ownedCr3Raw = ownedCr3Raw;
        _tablePageArenaStart = tablePageArenaStart;
        _tablePageArenaEndExclusive = tablePageArenaEndExclusive;
        _tableStateArenaStart = tableStateArenaStart;
        _tableStateArenaEndExclusive = tableStateArenaEndExclusive;
        _compositeCodeStart = compositeCodeStart;
        _compositeCodeEndExclusive = compositeCodeEndExclusive;
        _continuationPageStart = continuationPageStart;
        _continuationPageEndExclusive = continuationPageEndExclusive;
        _expectedReturnAddress = expectedReturnAddress;
        _stackStart = stackStart;
        _stackEndExclusive = stackEndExclusive;
        _probePageStart = probePageStart;
        _probePageEndExclusive = probePageEndExclusive;
        _probeEntry = probeEntry;
        _expectedProbePteAddress = expectedProbePteAddress;
        _initialProbePteRaw = initialProbePteRaw;
        _finalProbePteRaw = finalProbePteRaw;
        _canaryAddress = canaryAddress;
        _expectedCanary = expectedCanary;
        _replacementCanary = replacementCanary;
        _expectedProbeReturn = expectedProbeReturn;
        _prohibitedFirmwareCallbackCount = 0;
        _expectedSelfAddress = expectedSelfAddress;
        _expectedProbeHierarchyIdentity = expectedProbeHierarchyIdentity;
        _progressCheckpoint = M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized;
        _reservedOutput1 = 0;
    }

    public uint Size => _structureSize;
    public ushort Version => _structureVersion;
    public bool IsInitialized => _isInitialized == 1;
    public ulong OwnedCr3Raw => _ownedCr3Raw;
    public ulong TablePageArenaStart => _tablePageArenaStart;
    public ulong TablePageArenaEndExclusive => _tablePageArenaEndExclusive;
    public ulong TableStateArenaStart => _tableStateArenaStart;
    public ulong TableStateArenaEndExclusive => _tableStateArenaEndExclusive;
    public ulong CompositeCodeStart => _compositeCodeStart;
    public ulong CompositeCodeEndExclusive => _compositeCodeEndExclusive;
    public ulong CompositeCodeAndCallGatePageStart => _compositeCodeStart;
    public ulong CompositeCodeAndCallGatePageEndExclusive => _compositeCodeEndExclusive;
    public ulong ContinuationPageStart => _continuationPageStart;
    public ulong ContinuationPageEndExclusive => _continuationPageEndExclusive;
    public ulong ExpectedReturnAddress => _expectedReturnAddress;
    public ulong ExactPostCallReturnAddress => _expectedReturnAddress;
    public ulong StackStart => _stackStart;
    public ulong StackEndExclusive => _stackEndExclusive;
    public ulong ProbePageStart => _probePageStart;
    public ulong ProbePageEndExclusive => _probePageEndExclusive;
    public ulong ProbeEntry => _probeEntry;
    public ulong ExpectedProbePteAddress => _expectedProbePteAddress;
    public ulong InitialProbePteRaw => _initialProbePteRaw;
    public ulong FinalProbePteRaw => _finalProbePteRaw;
    public ulong CanaryAddress => _canaryAddress;
    public ulong ExpectedCanary => _expectedCanary;
    public ulong ReplacementCanary => _replacementCanary;
    public uint ExpectedProbeReturn => _expectedProbeReturn;
    public uint ProhibitedFirmwareCallbackCount => _prohibitedFirmwareCallbackCount;
    public ulong ExpectedSelfAddress => _expectedSelfAddress;
    public ulong ExpectedProbeHierarchyIdentity => _expectedProbeHierarchyIdentity;
    public ulong ExpectedCriticalMappingPageCount => _expectedProbeHierarchyIdentity & M01OwnedCr3CompositeTransition.CriticalMappingPageCountMask;
    public ulong ExpectedProbePdptPageOrdinal => (_expectedProbeHierarchyIdentity >> M01OwnedCr3CompositeTransition.ProbePdptPageOrdinalShift) & M01OwnedCr3CompositeTransition.ProbeHierarchyPageOrdinalMask;
    public ulong ExpectedProbePdPageOrdinal => (_expectedProbeHierarchyIdentity >> M01OwnedCr3CompositeTransition.ProbePdPageOrdinalShift) & M01OwnedCr3CompositeTransition.ProbeHierarchyPageOrdinalMask;
    public ulong ExpectedProbePtPageOrdinal => (_expectedProbeHierarchyIdentity >> M01OwnedCr3CompositeTransition.ProbePtPageOrdinalShift) & M01OwnedCr3CompositeTransition.ProbeHierarchyPageOrdinalMask;
    public ulong OriginalRflags => _originalRflags;
    public ulong OriginalCr0 => _originalCr0;
    public ulong OriginalCr3Raw => _originalCr3Raw;
    public ulong OriginalCr4Raw => _originalCr4Raw;
    public ulong OriginalEfer => _originalEfer;
    public ulong PhysicalAddressMask => _physicalAddressMask;
    public ulong EntryRsp => _entryRsp;
    public ulong ObservedReturnAddress => _observedReturnAddress;
    public ulong ObservedRip => _observedRip;
    public ulong CliRflagsReadback => _cliRflagsReadback;
    public ulong ClearedCr4Readback => _clearedCr4Readback;
    public ulong OwnedCr3Readback => _ownedCr3Readback;
    public ulong CanaryBefore => _canaryBefore;
    public ulong CanaryAfter => _canaryAfter;
    public ulong ProbePml4e => _probePml4e;
    public ulong ProbePdpte => _probePdpte;
    public ulong ProbePde => _probePde;
    public ulong ProbePteBefore => _probePteBefore;
    public ulong ProbePteAfter => _probePteAfter;
    public ulong InvlpgTarget => _invlpgTarget;
    public ulong ProbeReturn => _probeReturn;
    public ulong PageZeroPml4e => _pageZeroPml4e;
    public ulong PageZeroPdpte => _pageZeroPdpte;
    public ulong PageZeroPde => _pageZeroPde;
    public ulong PageZeroPte => _pageZeroPte;
    public ulong CommittedProbePteReadback => _committedProbePteReadback;
    public ulong FirmwareCr3Readback => _firmwareCr3Readback;
    public ulong PostRestoreCanaryReadback => _postRestoreCanaryReadback;
    public ulong RestoredCr4Readback => _restoredCr4Readback;
    public ulong RestoredRflagsIntent => _restoredRflagsIntent;
    public ulong ExitRsp => _exitRsp;
    public ulong InvlpgCount => _invlpgCount;
    public ulong ProbeInvocationCount => _probeInvocationCount;
    public M01OwnedCr3CompositeTransitionRuntimeStatus RuntimeStatus => _runtimeStatus;
    public M01OwnedCr3CompositeTransitionCheckpoint Checkpoint => _checkpoint;
    public M01OwnedCr3CompositeTransitionPageZeroLevel PageZeroLevel => _pageZeroLevel;
    public bool MutationCommitted => _mutationCommitted == 1;
    public bool OriginalPgeWasSet => _pgeWasCleared == 1;
    // Compatibility alias retained for the initial host-test contract.
    public bool PgeWasCleared => _pgeWasCleared == 1;
    public bool OwnedRootWasLoaded => _ownedRootWasLoaded == 1;
    public bool ProbeReturned => _probeReturned == 1;
    public M01OwnedCr3CompositeTransitionCheckpoint ProgressCheckpoint => _progressCheckpoint;

#if !NOSTDLIB_EFI
    public static M01OwnedCr3CompositeTransitionRuntimeState CreateObserved(M01OwnedCr3CompositeTransitionRuntimeState initializedState, ulong originalRflags, ulong originalCr0, ulong originalCr3Raw, ulong originalCr4Raw, ulong originalEfer, ulong physicalAddressMask, ulong entryRsp, ulong observedReturnAddress, ulong observedRip, ulong cliRflagsReadback, ulong clearedCr4Readback, ulong ownedCr3Readback, ulong canaryBefore, ulong canaryAfter, ulong probePml4e, ulong probePdpte, ulong probePde, ulong probePteBefore, ulong probePteAfter, ulong invlpgTarget, ulong probeReturn, ulong pageZeroPml4e, ulong pageZeroPdpte, ulong pageZeroPde, ulong pageZeroPte, ulong committedProbePteReadback, ulong firmwareCr3Readback, ulong postRestoreCanaryReadback, ulong restoredCr4Readback, ulong restoredRflagsIntent, ulong exitRsp, ulong invlpgCount, ulong probeInvocationCount, uint firmwareCallbackCount, M01OwnedCr3CompositeTransitionRuntimeStatus runtimeStatus, M01OwnedCr3CompositeTransitionCheckpoint checkpoint, M01OwnedCr3CompositeTransitionPageZeroLevel pageZeroLevel, bool mutationCommitted, bool pgeWasCleared, bool ownedRootWasLoaded, bool probeReturned, M01OwnedCr3CompositeTransitionCheckpoint progressCheckpoint = M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized)
    {
        initializedState._originalRflags = originalRflags;
        initializedState._originalCr0 = originalCr0;
        initializedState._originalCr3Raw = originalCr3Raw;
        initializedState._originalCr4Raw = originalCr4Raw;
        initializedState._originalEfer = originalEfer;
        initializedState._physicalAddressMask = physicalAddressMask;
        initializedState._entryRsp = entryRsp;
        initializedState._observedReturnAddress = observedReturnAddress;
        initializedState._observedRip = observedRip;
        initializedState._cliRflagsReadback = cliRflagsReadback;
        initializedState._clearedCr4Readback = clearedCr4Readback;
        initializedState._ownedCr3Readback = ownedCr3Readback;
        initializedState._canaryBefore = canaryBefore;
        initializedState._canaryAfter = canaryAfter;
        initializedState._probePml4e = probePml4e;
        initializedState._probePdpte = probePdpte;
        initializedState._probePde = probePde;
        initializedState._probePteBefore = probePteBefore;
        initializedState._probePteAfter = probePteAfter;
        initializedState._invlpgTarget = invlpgTarget;
        initializedState._probeReturn = probeReturn;
        initializedState._pageZeroPml4e = pageZeroPml4e;
        initializedState._pageZeroPdpte = pageZeroPdpte;
        initializedState._pageZeroPde = pageZeroPde;
        initializedState._pageZeroPte = pageZeroPte;
        initializedState._committedProbePteReadback = committedProbePteReadback;
        initializedState._firmwareCr3Readback = firmwareCr3Readback;
        initializedState._postRestoreCanaryReadback = postRestoreCanaryReadback;
        initializedState._restoredCr4Readback = restoredCr4Readback;
        initializedState._restoredRflagsIntent = restoredRflagsIntent;
        initializedState._exitRsp = exitRsp;
        initializedState._invlpgCount = invlpgCount;
        initializedState._probeInvocationCount = probeInvocationCount;
        initializedState._prohibitedFirmwareCallbackCount = firmwareCallbackCount;
        initializedState._runtimeStatus = runtimeStatus;
        initializedState._checkpoint = checkpoint;
        initializedState._pageZeroLevel = pageZeroLevel;
        initializedState._mutationCommitted = mutationCommitted ? (byte)1 : (byte)0;
        initializedState._pgeWasCleared = pgeWasCleared ? (byte)1 : (byte)0;
        initializedState._ownedRootWasLoaded = ownedRootWasLoaded ? (byte)1 : (byte)0;
        initializedState._probeReturned = probeReturned ? (byte)1 : (byte)0;
        initializedState._progressCheckpoint = progressCheckpoint;
        return initializedState;
    }

#endif
    internal bool ReservedFieldsAreZero => _reserved0 == 0 && _reservedOutput1 == 0;
}
