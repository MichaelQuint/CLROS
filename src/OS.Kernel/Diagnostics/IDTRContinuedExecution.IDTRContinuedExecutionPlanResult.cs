namespace OS.Kernel.Diagnostics;
/// <summary>
/// Status-coded result for context preparation and exact stub byte planning.
/// This is an unmanaged value contract and retains no caller-owned pointer.
/// </summary>

public readonly struct IDTRContinuedExecutionPlanResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;
    private readonly int _firstMismatchOffset;
    private IDTRContinuedExecutionPlanResult(IDTRContinuedExecutionOperation operation, IDTRContinuedExecutionStatus status, ulong modeledContextAddress, ulong modeledStubAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong stubCapacity, int contextBytesWritten, int contextBytesCompared, int stubBytesWritten, int stubBytesCompared, int firstMismatchOffset, ulong contextFingerprint, ulong stubFingerprint, bool destinationMutated, bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        _firstMismatchOffset = firstMismatchOffset;
        Operation = operation;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        ModeledStubAddress = modeledStubAddress;
        OwnedIdtAddress = ownedIdtAddress;
        ContextCapacity = contextCapacity;
        StubCapacity = stubCapacity;
        ContextBytesWritten = contextBytesWritten;
        ContextBytesCompared = contextBytesCompared;
        StubBytesWritten = stubBytesWritten;
        StubBytesCompared = stubBytesCompared;
        ContextFingerprint = contextFingerprint;
        StubFingerprint = stubFingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;
    public IDTRContinuedExecutionOperation Operation
    {
        get;
    }
    public IDTRContinuedExecutionStatus Status
    {
        get;
    }
    public ulong ModeledContextAddress
    {
        get;
    }
    public ulong ModeledStubAddress
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
    public ulong StubCapacity
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
    public int StubBytesWritten
    {
        get;
    }
    public int StubBytesCompared
    {
        get;
    }
    public int FirstMismatchOffset => IsInitialized ? _firstMismatchOffset : -1;
    public ulong ContextFingerprint
    {
        get;
    }
    public ulong StubFingerprint
    {
        get;
    }
    public ulong ExpectedStubFingerprint => IDTRContinuedExecution.StubFnv1A64;
    public bool DestinationMutated => _destinationMutated == 1;
    public bool ReadBackPerformed => _readBackPerformed == 1;
    public bool ContextPrepared => IsInitialized && Operation == IDTRContinuedExecutionOperation.PrepareContext && Status == IDTRContinuedExecutionStatus.Success && ContextBytesWritten == IDTRContinuedExecution.ContextByteCount && ContextBytesCompared == IDTRContinuedExecution.ContextByteCount && StubBytesWritten == 0 && StubBytesCompared == 0 && FirstMismatchOffset == -1 && DestinationMutated && ReadBackPerformed;
    public bool StubEmitted => IsInitialized && Operation == IDTRContinuedExecutionOperation.EmitStub && Status == IDTRContinuedExecutionStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == IDTRContinuedExecution.ContextByteCount && StubBytesWritten == IDTRContinuedExecution.StubByteCount && StubBytesCompared == IDTRContinuedExecution.StubByteCount && FirstMismatchOffset == -1 && StubFingerprint == ExpectedStubFingerprint && DestinationMutated && ReadBackPerformed;
    public bool ReadBackValidated => IsInitialized && Operation == IDTRContinuedExecutionOperation.ValidateStubReadBack && Status == IDTRContinuedExecutionStatus.Success && ContextBytesWritten == 0 && ContextBytesCompared == IDTRContinuedExecution.ContextByteCount && StubBytesWritten == 0 && StubBytesCompared == IDTRContinuedExecution.StubByteCount && FirstMismatchOffset == -1 && StubFingerprint == ExpectedStubFingerprint && !DestinationMutated && ReadBackPerformed;
    public bool Succeeded => ContextPrepared || StubEmitted || ReadBackValidated;

    internal static IDTRContinuedExecutionPlanResult Pass(IDTRContinuedExecutionOperation operation, ulong modeledContextAddress, ulong modeledStubAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong stubCapacity, int contextBytesWritten, int stubBytesWritten, ulong contextFingerprint, ulong stubFingerprint, bool destinationMutated)
    {
        return new IDTRContinuedExecutionPlanResult(operation, IDTRContinuedExecutionStatus.Success, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, stubCapacity, contextBytesWritten, IDTRContinuedExecution.ContextByteCount, stubBytesWritten, operation == IDTRContinuedExecutionOperation.PrepareContext ? 0 : IDTRContinuedExecution.StubByteCount, -1, contextFingerprint, stubFingerprint, destinationMutated, true);
    }

    internal static IDTRContinuedExecutionPlanResult Fail(IDTRContinuedExecutionOperation operation, IDTRContinuedExecutionStatus status, ulong modeledContextAddress, ulong modeledStubAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong stubCapacity, int contextBytesWritten = 0, int contextBytesCompared = 0, int stubBytesWritten = 0, int stubBytesCompared = 0, int firstMismatchOffset = -1, ulong contextFingerprint = 0, ulong stubFingerprint = 0, bool destinationMutated = false, bool readBackPerformed = false)
    {
        return new IDTRContinuedExecutionPlanResult(operation, status, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, stubCapacity, contextBytesWritten, contextBytesCompared, stubBytesWritten, stubBytesCompared, firstMismatchOffset, contextFingerprint, stubFingerprint, destinationMutated, readBackPerformed);
    }
}
