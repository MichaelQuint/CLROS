namespace OS.Kernel.Platform;

public readonly struct PlatformFileSystemSourceReadResult
{
    private PlatformFileSystemSourceReadResult(bool succeeded, PlatformFileSystemSourceStatus status, int bytesRead, int requiredBytes, int availableBytes, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformFileSystemSourceStatus Status
    {
        get;
    }
    public int BytesRead
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int AvailableBytes
    {
        get;
    }
    public ulong FirmwareStatus
    {
        get;
    }

    public static PlatformFileSystemSourceReadResult Read(int bytesRead, int availableBytes)
    {
        if (bytesRead <= 0 || availableBytes < bytesRead)
        {
            return FirmwareError(0);
        }

        return new PlatformFileSystemSourceReadResult(true, PlatformFileSystemSourceStatus.Success, bytesRead, bytesRead, availableBytes, 0);
    }

    public static PlatformFileSystemSourceReadResult DestinationTooSmall(int requiredBytes, int availableBytes)
    {
        return new PlatformFileSystemSourceReadResult(false, PlatformFileSystemSourceStatus.DestinationTooSmall, 0, requiredBytes, availableBytes, 0);
    }

    public static PlatformFileSystemSourceReadResult Unavailable()
    {
        return new PlatformFileSystemSourceReadResult(false, PlatformFileSystemSourceStatus.Unavailable, 0, 0, 0, 0);
    }

    public static PlatformFileSystemSourceReadResult FirmwareError(ulong firmwareStatus)
    {
        return new PlatformFileSystemSourceReadResult(false, PlatformFileSystemSourceStatus.FirmwareError, 0, 0, 0, firmwareStatus);
    }

    public static PlatformFileSystemSourceReadResult Missing()
    {
        return new PlatformFileSystemSourceReadResult(false, PlatformFileSystemSourceStatus.NotFound, 0, 0, 0, 0);
    }
}
