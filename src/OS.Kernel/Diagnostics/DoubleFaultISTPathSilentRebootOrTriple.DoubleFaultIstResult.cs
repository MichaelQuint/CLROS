namespace OS.Kernel.Diagnostics;

public readonly struct DoubleFaultIstResult
{
    internal DoubleFaultIstResult(DoubleFaultIstOperation operation, DoubleFaultIstStatus status, int written, int compared, int mismatch, ulong contextFingerprint, ulong streamFingerprint, ulong tableFingerprint = 0, ulong slotFingerprint = 0)
    {
        Operation = operation;
        Status = status;
        BytesWritten = written;
        BytesCompared = compared;
        FirstMismatchOffset = mismatch;
        ContextFingerprint = contextFingerprint;
        StreamFingerprint = streamFingerprint;
        TableFingerprint = tableFingerprint;
        SlotFingerprint = slotFingerprint;
    }

    public DoubleFaultIstOperation Operation
    {
        get;
    }
    public DoubleFaultIstStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    /// <summary>Primary comparison extent; excludes prerequisite rechecks and repeated reads.</summary>

    public int BytesCompared
    {
        get;
    }
    public int FirstMismatchOffset
    {
        get;
    }
    public ulong TableFingerprint
    {
        get;
    }
    public ulong SlotFingerprint
    {
        get;
    }
    public ulong ContextFingerprint
    {
        get;
    }
    public ulong StreamFingerprint
    {
        get;
    }
    public bool Succeeded => Operation != DoubleFaultIstOperation.None && Status == DoubleFaultIstStatus.Success && FirstMismatchOffset == -1;
    public bool ContextPrepared => Succeeded && Operation == DoubleFaultIstOperation.PrepareContext;
    public bool StreamEmitted => Succeeded && Operation == DoubleFaultIstOperation.EmitStream;
    public bool ReadBackValidated => Succeeded && Operation == DoubleFaultIstOperation.ValidateStreamReadBack;
    public bool GuestExecution => false;
    public bool HardwareStateCaptured => false;
    public bool FaultTriggered => false;
    public bool PostExitBootServices => false;
}
