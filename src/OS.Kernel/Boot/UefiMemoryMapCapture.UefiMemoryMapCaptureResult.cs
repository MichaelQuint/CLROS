namespace OS.Boot;
public readonly struct UefiMemoryMapCaptureResult
{
    private UefiMemoryMapCaptureResult(bool succeeded, UefiMemoryMapCaptureStatus status, UefiMemoryMapCopyPlanStatus copyPlanStatus, FirmwareMemoryMapRecord sourceRecord, FirmwareMemoryMapRecord ownedRecord, UefiMemoryMapCopyPlan copyPlan, ulong firmwareStatus, ulong requiredBufferSize, ulong allocationSize)
    {
        Succeeded = succeeded;
        Status = status;
        CopyPlanStatus = copyPlanStatus;
        SourceRecord = sourceRecord;
        OwnedRecord = ownedRecord;
        CopyPlan = copyPlan;
        FirmwareStatus = firmwareStatus;
        RequiredBufferSize = requiredBufferSize;
        AllocationSize = allocationSize;
    }

    public bool Succeeded { get; }
    public UefiMemoryMapCaptureStatus Status { get; }
    public UefiMemoryMapCopyPlanStatus CopyPlanStatus { get; }
    public FirmwareMemoryMapRecord SourceRecord { get; }
    public FirmwareMemoryMapRecord OwnedRecord { get; }
    public UefiMemoryMapCopyPlan CopyPlan { get; }
    public ulong FirmwareStatus { get; }
    public ulong RequiredBufferSize { get; }
    public ulong AllocationSize { get; }

    public static UefiMemoryMapCaptureResult Success(FirmwareMemoryMapRecord sourceRecord, FirmwareMemoryMapRecord ownedRecord, UefiMemoryMapCopyPlan copyPlan, ulong allocationSize)
    {
        return new UefiMemoryMapCaptureResult(true, UefiMemoryMapCaptureStatus.Success, UefiMemoryMapCopyPlanStatus.Success, sourceRecord, ownedRecord, copyPlan, 0, 0, allocationSize);
    }

    public static UefiMemoryMapCaptureResult Fail(UefiMemoryMapCaptureStatus status, UefiMemoryMapCopyPlanStatus copyPlanStatus = UefiMemoryMapCopyPlanStatus.Success, FirmwareMemoryMapRecord sourceRecord = default, ulong firmwareStatus = 0, ulong requiredBufferSize = 0, ulong allocationSize = 0)
    {
        return new UefiMemoryMapCaptureResult(false, status, copyPlanStatus, sourceRecord, default, default, firmwareStatus, requiredBufferSize, allocationSize);
    }
}
