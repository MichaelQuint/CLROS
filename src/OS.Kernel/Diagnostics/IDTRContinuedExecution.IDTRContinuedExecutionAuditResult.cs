namespace OS.Kernel.Diagnostics;
/// <summary>
/// Host-safe audit of caller-supplied execution observations. Constructing an
/// audit never executes the emitted privileged bytes.
/// </summary>

public readonly struct IDTRContinuedExecutionAuditResult
{
    private readonly byte _isInitialized;
    private readonly int _firstMismatchOffset;
    private IDTRContinuedExecutionAuditResult(IDTRContinuedExecutionStatus status, ulong modeledContextAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong observedCookie, int contextBytesCompared, int firstMismatchOffset, ushort previousLimit, ulong previousBase, ushort loadedLimit, ulong loadedBase, ushort restoredLimit, ulong restoredBase, ulong rflagsBefore, ulong rflagsAfter)
    {
        _isInitialized = 1;
        _firstMismatchOffset = firstMismatchOffset;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        OwnedIdtAddress = ownedIdtAddress;
        ContextCapacity = contextCapacity;
        ObservedCookie = observedCookie;
        ContextBytesCompared = contextBytesCompared;
        PreviousLimit = previousLimit;
        PreviousBase = previousBase;
        LoadedLimit = loadedLimit;
        LoadedBase = loadedBase;
        RestoredLimit = restoredLimit;
        RestoredBase = restoredBase;
        RflagsBefore = rflagsBefore;
        RflagsAfter = rflagsAfter;
    }

    public bool IsInitialized => _isInitialized == 1;
    public IDTRContinuedExecutionOperation Operation => IsInitialized ? IDTRContinuedExecutionOperation.AuditExecution : IDTRContinuedExecutionOperation.None;
    public IDTRContinuedExecutionStatus Status
    {
        get;
    }
    public ulong ModeledContextAddress
    {
        get;
    }
    public ulong OwnedIdtAddress
    {
        get;
    }
    public ulong ContextCapacity
    {
        get;
    }
    public ulong ExpectedCookie => IDTRContinuedExecution.ExecutionCookie;
    public ulong ObservedCookie
    {
        get;
    }
    public int ContextBytesCompared
    {
        get;
    }
    public int FirstMismatchOffset => IsInitialized ? _firstMismatchOffset : -1;
    public ushort PreviousLimit
    {
        get;
    }
    public ulong PreviousBase
    {
        get;
    }
    public ushort LoadedLimit
    {
        get;
    }
    public ulong LoadedBase
    {
        get;
    }
    public ushort RestoredLimit
    {
        get;
    }
    public ulong RestoredBase
    {
        get;
    }
    public ulong RflagsBefore
    {
        get;
    }
    public ulong RflagsAfter
    {
        get;
    }
    public bool TransitionObserved => IsInitialized && !SameIdtr(PreviousLimit, PreviousBase, IDTRContinuedExecution.IdtrLimit, OwnedIdtAddress);
    public bool OwnedIdtrLoaded => IsInitialized && LoadedLimit == IDTRContinuedExecution.IdtrLimit && LoadedBase == OwnedIdtAddress;
    public bool FirmwareIdtrRestored => IsInitialized && RestoredLimit == PreviousLimit && RestoredBase == PreviousBase;
    public bool InterruptFlagRestored => IsInitialized && ((RflagsBefore ^ RflagsAfter) & IDTRContinuedExecution.RflagsInterruptEnableBit) == 0;
    public bool ContinuedExecution => IsInitialized && ObservedCookie == ExpectedCookie;
    public bool Succeeded => IsInitialized && Status == IDTRContinuedExecutionStatus.Success && ObservedCookie == ExpectedCookie && ContextBytesCompared == IDTRContinuedExecution.ContextByteCount && FirstMismatchOffset == -1 && TransitionObserved && OwnedIdtrLoaded && FirmwareIdtrRestored && InterruptFlagRestored && ContinuedExecution;

    internal static IDTRContinuedExecutionAuditResult Create(IDTRContinuedExecutionStatus status, ulong modeledContextAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong observedCookie, int contextBytesCompared, int firstMismatchOffset, ushort previousLimit, ulong previousBase, ushort loadedLimit, ulong loadedBase, ushort restoredLimit, ulong restoredBase, ulong rflagsBefore, ulong rflagsAfter)
    {
        return new IDTRContinuedExecutionAuditResult(status, modeledContextAddress, ownedIdtAddress, contextCapacity, observedCookie, contextBytesCompared, firstMismatchOffset, previousLimit, previousBase, loadedLimit, loadedBase, restoredLimit, restoredBase, rflagsBefore, rflagsAfter);
    }

    private static bool SameIdtr(ushort firstLimit, ulong firstBase, ushort secondLimit, ulong secondBase)
    {
        return firstLimit == secondLimit && firstBase == secondBase;
    }
}
#if !NOSTDLIB_EFI
#endif
