namespace OS.Kernel.Platform;

public readonly struct PlatformSerialTransportResult
{
    private PlatformSerialTransportResult(PlatformSerialTransportStatus status, int bytesWritten, ulong backendStatus)
    {
        Status = status;
        BytesWritten = bytesWritten;
        BackendStatus = backendStatus;
    }

    public PlatformSerialTransportStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public ulong BackendStatus
    {
        get;
    }
    public bool Succeeded => Status == PlatformSerialTransportStatus.Success;

    public static PlatformSerialTransportResult Written(int bytesWritten)
    {
        return Written(bytesWritten, 0);
    }

    public static PlatformSerialTransportResult Written(int bytesWritten, ulong backendStatus)
    {
        if (bytesWritten < 0)
        {
            return BackendFailure(backendStatus);
        }

        return new PlatformSerialTransportResult(PlatformSerialTransportStatus.Success, bytesWritten, backendStatus);
    }

    public static PlatformSerialTransportResult Unavailable()
    {
        return new PlatformSerialTransportResult(PlatformSerialTransportStatus.Unavailable, 0, 0);
    }

    public static PlatformSerialTransportResult BackendFailure(ulong backendStatus)
    {
        return new PlatformSerialTransportResult(PlatformSerialTransportStatus.BackendFailure, 0, backendStatus);
    }
}
