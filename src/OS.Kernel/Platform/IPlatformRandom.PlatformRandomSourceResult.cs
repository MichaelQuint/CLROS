namespace OS.Kernel.Platform;
public readonly struct PlatformRandomSourceResult
{
    private PlatformRandomSourceResult(bool succeeded, PlatformRandomSourceStatus status, int bytesWritten, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        BytesWritten = bytesWritten;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }
    public PlatformRandomSourceStatus Status { get; }
    public int BytesWritten { get; }
    public ulong FirmwareStatus { get; }

    public static PlatformRandomSourceResult Filled(int bytesWritten)
    {
        if (bytesWritten <= 0)
        {
            return new PlatformRandomSourceResult(false, PlatformRandomSourceStatus.FirmwareError, 0, 0);
        }

        return new PlatformRandomSourceResult(true, PlatformRandomSourceStatus.Success, bytesWritten, 0);
    }

    public static PlatformRandomSourceResult Unavailable()
    {
        return new PlatformRandomSourceResult(false, PlatformRandomSourceStatus.Unavailable, 0, 0);
    }

    public static PlatformRandomSourceResult FirmwareError(ulong firmwareStatus)
    {
        return new PlatformRandomSourceResult(false, PlatformRandomSourceStatus.FirmwareError, 0, firmwareStatus);
    }
}
