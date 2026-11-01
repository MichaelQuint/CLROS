namespace OS.Kernel.Diagnostics;
public readonly struct UnexpectedVectorPanicResult
{
    internal UnexpectedVectorPanicResult(UnexpectedVectorPanicStatus status, int bytesWritten = 0, int bytesCompared = 0, int firstMismatch = -1, int firstDrain = 0, int secondDrain = 0, int metadataPolls = 0, int metadataWritten = 0, Com1OwnedPortIoOperationResult lastOperation = default, PanicSafeOwnedCom1ReportResult panic = default)
    {
        Status = status;
        BytesWritten = bytesWritten;
        BytesCompared = bytesCompared;
        FirstMismatchOffset = firstMismatch;
        FirstDrainAttempts = firstDrain;
        SecondDrainAttempts = secondDrain;
        MetadataPolls = metadataPolls;
        MetadataBytesWritten = metadataWritten;
        LastOperation = lastOperation;
        Panic = panic;
    }

    public UnexpectedVectorPanicStatus Status { get; }
    public bool Succeeded => Status == UnexpectedVectorPanicStatus.Success;
    /// <summary>Distinct caller-buffer bytes written by preparation, emission or formatting; serial writes have separate counters.</summary>

    public int BytesWritten { get; }
    public bool BuffersMutated => BytesWritten != 0;
    public int BytesCompared { get; }
    public int FirstMismatchOffset { get; }
    public int FirstDrainAttempts { get; }
    public int SecondDrainAttempts { get; }
    public int MetadataPolls { get; }
    public int MetadataBytesWritten { get; }
    public Com1OwnedPortIoOperationResult LastOperation { get; }
    public PanicSafeOwnedCom1ReportResult Panic { get; }
}
