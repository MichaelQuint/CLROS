namespace OS.Kernel.Platform;

public readonly struct PlatformPhysicalMemorySourceResult
{
    private PlatformPhysicalMemorySourceResult(bool succeeded, PlatformPhysicalMemorySourceStatus status, int bytesRead, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        BytesRead = bytesRead;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformPhysicalMemorySourceStatus Status
    {
        get;
    }
    public int BytesRead
    {
        get;
    }
    public ulong FirmwareStatus
    {
        get;
    }

    public static PlatformPhysicalMemorySourceResult Read(int bytesRead)
    {
        if (bytesRead <= 0)
        {
            return Fault(0);
        }

        return new PlatformPhysicalMemorySourceResult(true, PlatformPhysicalMemorySourceStatus.Success, bytesRead, 0);
    }

    public static PlatformPhysicalMemorySourceResult Unavailable()
    {
        return new PlatformPhysicalMemorySourceResult(false, PlatformPhysicalMemorySourceStatus.Unavailable, 0, 0);
    }

    public static PlatformPhysicalMemorySourceResult Fault(ulong firmwareStatus)
    {
        return new PlatformPhysicalMemorySourceResult(false, PlatformPhysicalMemorySourceStatus.Fault, 0, firmwareStatus);
    }
}
