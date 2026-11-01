namespace OS.Kernel.Diagnostics;
public readonly struct FaultEntryStubPlanResult
{
    private FaultEntryStubPlanResult(bool succeeded, FaultEntryStubPlanStatus status, byte vector, bool pushesErrorCode, bool pushesSyntheticErrorCode, int bytesWritten, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Vector = vector;
        PushesErrorCode = pushesErrorCode;
        PushesSyntheticErrorCode = pushesSyntheticErrorCode;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }
    public FaultEntryStubPlanStatus Status { get; }
    public byte Vector { get; }
    public bool PushesErrorCode { get; }
    public bool PushesSyntheticErrorCode { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }

    public static FaultEntryStubPlanResult Success(byte vector, bool pushesErrorCode, bool pushesSyntheticErrorCode, int bytesWritten)
    {
        return new FaultEntryStubPlanResult(true, FaultEntryStubPlanStatus.Ok, vector, pushesErrorCode, pushesSyntheticErrorCode, bytesWritten, bytesWritten);
    }

    public static FaultEntryStubPlanResult Fail(FaultEntryStubPlanStatus status, byte vector, bool pushesErrorCode, bool pushesSyntheticErrorCode, int requiredBytes)
    {
        return new FaultEntryStubPlanResult(false, status, vector, pushesErrorCode, pushesSyntheticErrorCode, 0, requiredBytes);
    }
}
