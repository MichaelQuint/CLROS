namespace OS.Kernel.Platform;

public interface IPlatformPhysicalMemory
{
    PlatformPhysicalMemoryResult ReadBytes(
        ulong physicalAddress,
        int byteCount,
        Span<byte> destination);
}

public interface IPlatformPhysicalMemorySource
{
    PlatformPhysicalMemorySourceResult ReadBytes(
        ulong physicalAddress,
        int byteCount,
        Span<byte> destination);
}

public static class PlatformPhysicalMemoryOperations
{
    public const string ReadBytes = "PhysicalMemory.ReadBytes";
}

public enum PlatformPhysicalMemoryStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidRange = 3,
    InvalidDestination = 4,
    DestinationTooSmall = 5,
    SourceUnavailable = 6,
    SourceFailed = 7,
    ShortRead = 8
}

public enum PlatformPhysicalMemorySourceStatus
{
    Unknown = 0,
    Success = 1,
    Unavailable = 2,
    Fault = 3
}

public readonly struct PlatformPhysicalMemorySourceResult
{
    private PlatformPhysicalMemorySourceResult(
        bool succeeded,
        PlatformPhysicalMemorySourceStatus status,
        int bytesRead,
        ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        BytesRead = bytesRead;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }

    public PlatformPhysicalMemorySourceStatus Status { get; }

    public int BytesRead { get; }

    public ulong FirmwareStatus { get; }

    public static PlatformPhysicalMemorySourceResult Read(int bytesRead)
    {
        if (bytesRead <= 0)
        {
            return Fault(0);
        }

        return new PlatformPhysicalMemorySourceResult(
            true,
            PlatformPhysicalMemorySourceStatus.Success,
            bytesRead,
            0);
    }

    public static PlatformPhysicalMemorySourceResult Unavailable()
    {
        return new PlatformPhysicalMemorySourceResult(
            false,
            PlatformPhysicalMemorySourceStatus.Unavailable,
            0,
            0);
    }

    public static PlatformPhysicalMemorySourceResult Fault(ulong firmwareStatus)
    {
        return new PlatformPhysicalMemorySourceResult(
            false,
            PlatformPhysicalMemorySourceStatus.Fault,
            0,
            firmwareStatus);
    }
}

public readonly struct PlatformPhysicalMemoryResult
{
    private PlatformPhysicalMemoryResult(
        bool succeeded,
        PlatformPhysicalMemoryStatus status,
        FirmwareServicesMode mode,
        ulong physicalAddress,
        int requestedBytes,
        int bytesRead,
        int requiredBytes,
        ulong firmwareStatus)
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

    public static PlatformPhysicalMemoryResult Read(
        FirmwareCallResult guardResult,
        PlatformPhysicalMemorySourceResult sourceResult,
        ulong physicalAddress,
        int requestedBytes)
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
            return new PlatformPhysicalMemoryResult(
                false,
                PlatformPhysicalMemoryStatus.ShortRead,
                guardResult.Mode,
                physicalAddress,
                requestedBytes,
                sourceResult.BytesRead,
                requestedBytes,
                sourceResult.FirmwareStatus);
        }

        return new PlatformPhysicalMemoryResult(
            true,
            PlatformPhysicalMemoryStatus.Success,
            guardResult.Mode,
            physicalAddress,
            requestedBytes,
            sourceResult.BytesRead,
            requestedBytes,
            sourceResult.FirmwareStatus);
    }

    public static PlatformPhysicalMemoryResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformPhysicalMemoryResult(
            false,
            PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable,
            guardResult.Mode,
            0,
            0,
            0,
            0,
            0);
    }

    public static PlatformPhysicalMemoryResult InvalidRange(
        FirmwareCallResult guardResult,
        ulong physicalAddress,
        int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(
            false,
            PlatformPhysicalMemoryStatus.InvalidRange,
            guardResult.Mode,
            physicalAddress,
            requestedBytes,
            0,
            requestedBytes > 0 ? requestedBytes : 0,
            0);
    }

    public static PlatformPhysicalMemoryResult InvalidDestination(
        FirmwareCallResult guardResult,
        ulong physicalAddress,
        int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(
            false,
            PlatformPhysicalMemoryStatus.InvalidDestination,
            guardResult.Mode,
            physicalAddress,
            requestedBytes,
            0,
            requestedBytes > 0 ? requestedBytes : 0,
            0);
    }

    public static PlatformPhysicalMemoryResult DestinationTooSmall(
        FirmwareCallResult guardResult,
        ulong physicalAddress,
        int requestedBytes)
    {
        return new PlatformPhysicalMemoryResult(
            false,
            PlatformPhysicalMemoryStatus.DestinationTooSmall,
            guardResult.Mode,
            physicalAddress,
            requestedBytes,
            0,
            requestedBytes,
            0);
    }

    private static PlatformPhysicalMemoryResult SourceFailure(
        FirmwareCallResult guardResult,
        PlatformPhysicalMemorySourceResult sourceResult,
        ulong physicalAddress,
        int requestedBytes)
    {
        PlatformPhysicalMemoryStatus status = sourceResult.Status == PlatformPhysicalMemorySourceStatus.Unavailable
            ? PlatformPhysicalMemoryStatus.SourceUnavailable
            : PlatformPhysicalMemoryStatus.SourceFailed;

        return new PlatformPhysicalMemoryResult(
            false,
            status,
            guardResult.Mode,
            physicalAddress,
            requestedBytes,
            sourceResult.BytesRead,
            requestedBytes,
            sourceResult.FirmwareStatus);
    }
}
