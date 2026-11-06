namespace OS.Kernel.Platform;

public readonly struct PlatformRandomResult
{
    private PlatformRandomResult(bool succeeded, PlatformRandomStatus status, FirmwareServicesMode mode, int requestedBytes, int bytesWritten, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        RequestedBytes = requestedBytes;
        BytesWritten = bytesWritten;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformRandomStatus Status
    {
        get;
    }
    public FirmwareServicesMode Mode
    {
        get;
    }
    public string Operation => PlatformRandomOperations.FillBytes;
    public int RequestedBytes
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public ulong FirmwareStatus
    {
        get;
    }

    public static PlatformRandomResult Fill(FirmwareCallResult guardResult, PlatformRandomSourceResult sourceResult, int requestedBytes)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (requestedBytes <= 0)
        {
            return InvalidDestination(guardResult);
        }

        if (!sourceResult.Succeeded)
        {
            return SourceFailure(guardResult, sourceResult, requestedBytes);
        }

        if (sourceResult.BytesWritten != requestedBytes)
        {
            return new PlatformRandomResult(false, PlatformRandomStatus.ShortRead, guardResult.Mode, requestedBytes, sourceResult.BytesWritten, sourceResult.FirmwareStatus);
        }

        return new PlatformRandomResult(true, PlatformRandomStatus.Success, guardResult.Mode, requestedBytes, sourceResult.BytesWritten, sourceResult.FirmwareStatus);
    }

    public static PlatformRandomResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformRandomResult(false, PlatformRandomStatus.FirmwareServicesUnavailable, guardResult.Mode, 0, 0, 0);
    }

    public static PlatformRandomResult InvalidDestination(FirmwareCallResult guardResult)
    {
        return new PlatformRandomResult(false, PlatformRandomStatus.InvalidDestination, guardResult.Mode, 0, 0, 0);
    }

    private static PlatformRandomResult SourceFailure(FirmwareCallResult guardResult, PlatformRandomSourceResult sourceResult, int requestedBytes)
    {
        PlatformRandomStatus status = sourceResult.Status == PlatformRandomSourceStatus.Unavailable ? PlatformRandomStatus.SourceUnavailable : PlatformRandomStatus.SourceFailed;
        return new PlatformRandomResult(false, status, guardResult.Mode, requestedBytes, sourceResult.BytesWritten, sourceResult.FirmwareStatus);
    }
}
