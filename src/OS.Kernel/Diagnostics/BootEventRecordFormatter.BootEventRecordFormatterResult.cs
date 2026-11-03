namespace OS.Kernel.Diagnostics;

public readonly struct BootEventRecordFormatterResult
{
    private BootEventRecordFormatterResult(bool isInitialized, BootEventRecordFormatterStatus status, PanicTextBufferStatus textStatus, int recordsRequested, int recordsWritten, int bytesWritten, int requiredBytes, int invalidRecordIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        TextStatus = textStatus;
        RecordsRequested = recordsRequested;
        RecordsWritten = recordsWritten;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        InvalidRecordIndex = invalidRecordIndex;
    }

    public bool IsInitialized
    {
        get;
    }
    public BootEventRecordFormatterStatus Status
    {
        get;
    }
    public PanicTextBufferStatus TextStatus
    {
        get;
    }
    public int RecordsRequested
    {
        get;
    }
    public int RecordsWritten
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int InvalidRecordIndex
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == BootEventRecordFormatterStatus.Success && TextStatus == PanicTextBufferStatus.Ok && RecordsRequested > 0 && RecordsWritten == RecordsRequested && BytesWritten > 0 && RequiredBytes == BytesWritten;

    public static BootEventRecordFormatterResult Success(int recordsRequested, int recordsWritten, int bytesWritten, int requiredBytes)
    {
        return new BootEventRecordFormatterResult(true, BootEventRecordFormatterStatus.Success, PanicTextBufferStatus.Ok, recordsRequested, recordsWritten, bytesWritten, requiredBytes, -1);
    }

    public static BootEventRecordFormatterResult Fail(BootEventRecordFormatterStatus status, PanicTextBufferStatus textStatus, int recordsRequested, int recordsWritten, int bytesWritten, int requiredBytes, int invalidRecordIndex)
    {
        return new BootEventRecordFormatterResult(true, status, textStatus, recordsRequested, recordsWritten, bytesWritten, requiredBytes, invalidRecordIndex);
    }
}
