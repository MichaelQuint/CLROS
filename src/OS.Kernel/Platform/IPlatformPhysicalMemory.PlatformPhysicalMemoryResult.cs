namespace OS.Kernel.Platform;
public readonly struct PlatformPhysicalMemoryResult
{
    private PlatformPhysicalMemoryResult(bool succeeded, PlatformPhysicalMemoryStatus status, FirmwareServicesMode mode, ulong physicalAddress, int requestedBytes, int bytesRead, int requiredBytes, ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        PhysicalAddress = physicalAddress;
        RequestedBytes = requestedBytes;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }
    public PlatformPhysicalMemoryStatus Status { get; }
    public FirmwareServicesMode Mode { get; }
    public string Operation => PlatformPhysicalMemoryOperations.ReadBytes;
    public ulong PhysicalAddress { get; }
    public int RequestedBytes { get; }
    public int BytesRead { get; }
    public int RequiredBytes { get; }
    public ulong FirmwareStatus { get; }

    public static PlatformPhysicalMemoryResult Read(FirmwareCallResult guardResult, PlatformPhysicalMemorySourceResult sourceResult, ulong physicalAddress, int requestedBytes)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!sourceResult.Succeeded)
        {
            return SourceFailure(guardResult, sourceResult, physicalAddress, requestedBytes);
        }

        if (sourceResult.BytesRead != requestedBytes)
        {
            return new PlatformPhysicalMemoryResult(false, PlatformPhysicalMemoryStatus.ShortRead, guardResult.Mode, physicalAddress, requestedBytes, sourceResult.BytesRead, requestedBytes, sourceResult.FirmwareStatus);
        }

        return new PlatformPhysicalMemoryResult(true, PlatformPhysicalMemoryStatus.Success, guardResult.Mode, physicalAddress, requestedBytes, sourceResult.BytesRead, requestedBytes, sourceResult.FirmwareStatus);
    }

    public static PlatformPhysicalMemoryResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformPhysicalMemoryResult(false, PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable, guardResult.Mode, 0, 0, 0, 0, 0);
    }

    public static PlatformPhysicalMemoryResult InvalidRange(FirmwareCallResult guardResult, ulong physicalAddress, int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(false, PlatformPhysicalMemoryStatus.InvalidRange, guardResult.Mode, physicalAddress, requestedBytes, 0, requestedBytes > 0 ? requestedBytes : 0, 0);
    }

    public static PlatformPhysicalMemoryResult InvalidDestination(FirmwareCallResult guardResult, ulong physicalAddress, int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(false, PlatformPhysicalMemoryStatus.InvalidDestination, guardResult.Mode, physicalAddress, requestedBytes, 0, requestedBytes > 0 ? requestedBytes : 0, 0);
    }

    public static PlatformPhysicalMemoryResult DestinationTooSmall(FirmwareCallResult guardResult, ulong physicalAddress, int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(false, PlatformPhysicalMemoryStatus.DestinationTooSmall, guardResult.Mode, physicalAddress, requestedBytes, 0, requestedBytes, 0);
    }

    private static PlatformPhysicalMemoryResult SourceFailure(FirmwareCallResult guardResult, PlatformPhysicalMemorySourceResult sourceResult, ulong physicalAddress, int requestedBytes)
    {
        PlatformPhysicalMemoryStatus status = sourceResult.Status == PlatformPhysicalMemorySourceStatus.Unavailable ? PlatformPhysicalMemoryStatus.SourceUnavailable : PlatformPhysicalMemoryStatus.SourceFailed;
        return new PlatformPhysicalMemoryResult(false, status, guardResult.Mode, physicalAddress, requestedBytes, sourceResult.BytesRead, requestedBytes, sourceResult.FirmwareStatus);
    }
}
