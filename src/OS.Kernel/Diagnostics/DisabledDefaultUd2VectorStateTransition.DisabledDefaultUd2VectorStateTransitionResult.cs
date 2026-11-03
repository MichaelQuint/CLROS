namespace OS.Kernel.Diagnostics;

public readonly struct DisabledDefaultUd2VectorStateTransitionResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;
    private readonly int _firstMismatchOffset;
    private DisabledDefaultUd2VectorStateTransitionResult(DisabledDefaultUd2VectorStateTransitionOperation operation, DisabledDefaultUd2VectorStateTransitionStatus status, ulong modeledContextAddress, ulong modeledStreamAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong streamCapacity, int contextBytesWritten, int contextBytesCompared, int streamBytesWritten, int streamBytesCompared, int firstMismatchOffset, ulong contextFingerprint, ulong streamFingerprint, bool destinationMutated, bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        _firstMismatchOffset = firstMismatchOffset;
        Operation = operation;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        ModeledStreamAddress = modeledStreamAddress;
        OwnedIdtAddress = ownedIdtAddress;
        ContextCapacity = contextCapacity;
        StreamCapacity = streamCapacity;
        ContextBytesWritten = contextBytesWritten;
        ContextBytesCompared = contextBytesCompared;
        StreamBytesWritten = streamBytesWritten;
        StreamBytesCompared = streamBytesCompared;
        ContextFingerprint = contextFingerprint;
        StreamFingerprint = streamFingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;
    public DisabledDefaultUd2VectorStateTransitionOperation Operation
    {
        get;
    }
    public DisabledDefaultUd2VectorStateTransitionStatus Status
    {
        get;
    }
    public ulong ModeledContextAddress
    {
        get;
    }
    public ulong ModeledStreamAddress
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
    public ulong StreamCapacity
    {
        get;
    }
    public int ContextBytesWritten
    {
        get;
    }
    public int ContextBytesCompared
    {
        get;
    }
    public int StreamBytesWritten
    {
        get;
    }
    public int StreamBytesCompared
    {
        get;
    }
    public int FirstMismatchOffset => IsInitialized ? _firstMismatchOffset : -1;
    public ulong ContextFingerprint
    {
        get;
    }
    public ulong StreamFingerprint
    {
        get;
    }
    public ulong ExpectedStreamFingerprint => ModeledContextAddress == DisabledDefaultUd2VectorStateTransition.CanonicalModeledContextAddress ? DisabledDefaultUd2VectorStateTransition.StreamFnv1A64 : StreamFingerprint;
    public bool DestinationMutated => _destinationMutated == 1;
    public bool ReadBackPerformed => _readBackPerformed == 1;
    public bool ContextPrepared => IsInitialized && Operation == DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext && Status == DisabledDefaultUd2VectorStateTransitionStatus.Success && ContextBytesWritten == DisabledDefaultUd2VectorStateTransition.ContextByteCount && ContextBytesCompared == DisabledDefaultUd2VectorStateTransition.ContextByteCount && StreamBytesWritten == 0 && StreamBytesCompared == 0 && FirstMismatchOffset == -1 && ContextFingerprint != 0 && DestinationMutated && ReadBackPerformed;
    public bool StreamEmitted => IsInitialized && Operation == DisabledDefaultUd2VectorStateTransitionOperation.EmitStream && Status == DisabledDefaultUd2VectorStateTransitionStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == DisabledDefaultUd2VectorStateTransition.ContextByteCount && StreamBytesWritten == DisabledDefaultUd2VectorStateTransition.StreamByteCount && StreamBytesCompared == DisabledDefaultUd2VectorStateTransition.StreamByteCount && FirstMismatchOffset == -1 && StreamFingerprint == ExpectedStreamFingerprint && DestinationMutated && ReadBackPerformed;
    public bool ReadBackValidated => IsInitialized && Operation == DisabledDefaultUd2VectorStateTransitionOperation.ValidateStreamReadBack && Status == DisabledDefaultUd2VectorStateTransitionStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == DisabledDefaultUd2VectorStateTransition.ContextByteCount && StreamBytesWritten == 0 && StreamBytesCompared == DisabledDefaultUd2VectorStateTransition.StreamByteCount && FirstMismatchOffset == -1 && StreamFingerprint == ExpectedStreamFingerprint && !DestinationMutated && ReadBackPerformed;
    public bool Succeeded => ContextPrepared || StreamEmitted || ReadBackValidated;

    internal static DisabledDefaultUd2VectorStateTransitionResult Pass(DisabledDefaultUd2VectorStateTransitionOperation operation, ulong modeledContextAddress, ulong modeledStreamAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong streamCapacity, int contextBytesWritten, int streamBytesWritten, ulong contextFingerprint, ulong streamFingerprint, bool destinationMutated)
    {
        return new DisabledDefaultUd2VectorStateTransitionResult(operation, DisabledDefaultUd2VectorStateTransitionStatus.Success, modeledContextAddress, modeledStreamAddress, ownedIdtAddress, contextCapacity, streamCapacity, contextBytesWritten, DisabledDefaultUd2VectorStateTransition.ContextByteCount, streamBytesWritten, operation == DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext ? 0 : DisabledDefaultUd2VectorStateTransition.StreamByteCount, -1, contextFingerprint, streamFingerprint, destinationMutated, true);
    }

    internal static DisabledDefaultUd2VectorStateTransitionResult Fail(DisabledDefaultUd2VectorStateTransitionOperation operation, DisabledDefaultUd2VectorStateTransitionStatus status, ulong modeledContextAddress, ulong modeledStreamAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong streamCapacity, int contextBytesCompared = 0, int streamBytesWritten = 0, int streamBytesCompared = 0, int firstMismatchOffset = -1, ulong contextFingerprint = 0, ulong streamFingerprint = 0, bool destinationMutated = false, bool readBackPerformed = false)
    {
        return new DisabledDefaultUd2VectorStateTransitionResult(operation, status, modeledContextAddress, modeledStreamAddress, ownedIdtAddress, contextCapacity, streamCapacity, 0, contextBytesCompared, streamBytesWritten, streamBytesCompared, firstMismatchOffset, contextFingerprint, streamFingerprint, destinationMutated, readBackPerformed);
    }
}
#if !NOSTDLIB_EFI
#endif
