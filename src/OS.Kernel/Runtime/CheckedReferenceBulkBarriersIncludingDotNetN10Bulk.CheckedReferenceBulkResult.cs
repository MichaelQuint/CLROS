namespace OS.Kernel.Runtime;

public readonly struct CheckedReferenceBulkResult
{
    internal CheckedReferenceBulkResult(CheckedReferenceBulkStatus status, ManagedHeapPageRangeStatus heapStatus = default, ulong bytesWritten = 0)
    {
        Status = status;
        HeapStatus = heapStatus;
        BytesWritten = bytesWritten;
    }

    public CheckedReferenceBulkStatus Status
    {
        get;
    }
    public ManagedHeapPageRangeStatus HeapStatus
    {
        get;
    }
    public ulong BytesWritten
    {
        get;
    }
    public bool Succeeded => Status == CheckedReferenceBulkStatus.Success;
}
