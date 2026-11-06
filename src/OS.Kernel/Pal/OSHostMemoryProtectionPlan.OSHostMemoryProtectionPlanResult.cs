namespace OS.Pal;

public readonly struct OSHostMemoryProtectionPlanResult
{
    private OSHostMemoryProtectionPlanResult(OSHostMemoryProtectionPlanStatus status, OSHostStatus hostStatus, ulong baseAddress, ulong byteLength, ulong endAddress, ulong pageSizeBytes, ulong pageCount, OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection, bool isInitialized)
    {
        Status = status;
        HostStatus = hostStatus;
        BaseAddress = baseAddress;
        ByteLength = byteLength;
        EndAddress = endAddress;
        PageSizeBytes = pageSizeBytes;
        PageCount = pageCount;
        CurrentProtection = currentProtection;
        RequestedProtection = requestedProtection;
        IsInitialized = isInitialized;
    }

    public OSHostMemoryProtectionPlanStatus Status
    {
        get;
    }
    public OSHostStatus HostStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == OSHostMemoryProtectionPlanStatus.Success;
    public ulong BaseAddress
    {
        get;
    }
    public ulong ByteLength
    {
        get;
    }
    public ulong EndAddress
    {
        get;
    }
    public ulong PageSizeBytes
    {
        get;
    }
    public ulong PageCount
    {
        get;
    }
    public OSHostPageProtection CurrentProtection
    {
        get;
    }
    public OSHostPageProtection RequestedProtection
    {
        get;
    }

    public static OSHostMemoryProtectionPlanResult Success(ulong baseAddress, ulong byteLength, ulong endAddress, ulong pageSizeBytes, ulong pageCount, OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection)
    {
        return new OSHostMemoryProtectionPlanResult(OSHostMemoryProtectionPlanStatus.Success, OSHostStatus.Ok, baseAddress, byteLength, endAddress, pageSizeBytes, pageCount, currentProtection, requestedProtection, isInitialized: true);
    }

    public static OSHostMemoryProtectionPlanResult Fail(OSHostMemoryProtectionPlanStatus status, ulong baseAddress, ulong byteLength, ulong pageSizeBytes, OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection)
    {
        return new OSHostMemoryProtectionPlanResult(status, OSHostMemoryProtectionPlan.ToHostStatus(status), baseAddress, byteLength, 0, pageSizeBytes, 0, currentProtection, requestedProtection, isInitialized: true);
    }
}
