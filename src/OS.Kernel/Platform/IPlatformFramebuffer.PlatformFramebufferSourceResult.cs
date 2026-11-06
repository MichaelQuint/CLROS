namespace OS.Kernel.Platform;

public readonly struct PlatformFramebufferSourceResult
{
    private PlatformFramebufferSourceResult(bool succeeded, PlatformFramebufferSourceStatus status, PlatformFramebufferMetadata metadata, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Metadata = metadata;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformFramebufferSourceStatus Status
    {
        get;
    }
    public PlatformFramebufferMetadata Metadata
    {
        get;
    }
    public ulong FirmwareStatus
    {
        get;
    }

    public static PlatformFramebufferSourceResult Captured(PlatformFramebufferMetadata metadata)
    {
        return new PlatformFramebufferSourceResult(true, PlatformFramebufferSourceStatus.Success, metadata, 0);
    }

    public static PlatformFramebufferSourceResult Unavailable()
    {
        return new PlatformFramebufferSourceResult(false, PlatformFramebufferSourceStatus.Unavailable, default, 0);
    }

    public static PlatformFramebufferSourceResult FirmwareError(ulong firmwareStatus)
    {
        return new PlatformFramebufferSourceResult(false, PlatformFramebufferSourceStatus.FirmwareError, default, firmwareStatus);
    }
}
