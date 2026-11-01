namespace OS.Kernel.Diagnostics;
public readonly struct SeparatePageFaultTransitionResult
{
    internal SeparatePageFaultTransitionResult(SeparatePageFaultTransitionOperation operation, SeparatePageFaultTransitionStatus status, int written, int compared, int mismatch, ulong contextFingerprint, ulong streamFingerprint)
    {
        Operation = operation;
        Status = status;
        BytesWritten = written;
        BytesCompared = compared;
        FirstMismatchOffset = mismatch;
        ContextFingerprint = contextFingerprint;
        StreamFingerprint = streamFingerprint;
    }

    public SeparatePageFaultTransitionOperation Operation { get; }
    public SeparatePageFaultTransitionStatus Status { get; }
    public int BytesWritten { get; }
    public int BytesCompared { get; }
    public int FirstMismatchOffset { get; }
    public ulong ContextFingerprint { get; }
    public ulong StreamFingerprint { get; }
    public bool Succeeded => Operation != SeparatePageFaultTransitionOperation.None && Status == SeparatePageFaultTransitionStatus.Success && FirstMismatchOffset == -1;
    public bool ContextPrepared => Succeeded && Operation == SeparatePageFaultTransitionOperation.PrepareContext;
    public bool StreamEmitted => Succeeded && Operation == SeparatePageFaultTransitionOperation.EmitStream;
    public bool ReadBackValidated => Succeeded && Operation == SeparatePageFaultTransitionOperation.ValidateStreamReadBack;
    public bool GuestExecution => false;
    public bool HardwareStateCaptured => false;
    public bool FaultTriggered => false;
    public bool PostExitBootServices => false;
}
