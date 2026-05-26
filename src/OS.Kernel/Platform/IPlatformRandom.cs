namespace OS.Kernel.Platform;

public interface IPlatformRandom
{
    PlatformRandomResult FillBytes(Span<byte> destination);
}

public interface IPlatformRandomSource
{
    PlatformRandomSourceResult FillBytes(Span<byte> destination);
}

public static class PlatformRandomOperations
{
    public const string FillBytes = "Random.FillBytes";
}

public enum PlatformRandomStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidDestination = 3,
    SourceUnavailable = 4,
    SourceFailed = 5,
    ShortRead = 6
}

public enum PlatformRandomSourceStatus
{
    Unknown = 0,
    Success = 1,
    Unavailable = 2,
    FirmwareError = 3
}

public readonly struct PlatformRandomSourceResult
{
    private PlatformRandomSourceResult(
        bool succeeded,
        PlatformRandomSourceStatus status,
        int bytesWritten,
        ulong firmwareStatus)
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
            return new PlatformRandomSourceResult(
                false,
                PlatformRandomSourceStatus.FirmwareError,
                0,
                0);
        }

        return new PlatformRandomSourceResult(
            true,
            PlatformRandomSourceStatus.Success,
            bytesWritten,
            0);
    }

    public static PlatformRandomSourceResult Unavailable()
    {
        return new PlatformRandomSourceResult(
            false,
            PlatformRandomSourceStatus.Unavailable,
            0,
            0);
    }

    public static PlatformRandomSourceResult FirmwareError(ulong firmwareStatus)
    {
        return new PlatformRandomSourceResult(
            false,
            PlatformRandomSourceStatus.FirmwareError,
            0,
            firmwareStatus);
    }
}

public readonly struct PlatformRandomResult
{
    private PlatformRandomResult(
        bool succeeded,
        PlatformRandomStatus status,
        FirmwareServicesMode mode,
        int requestedBytes,
        int bytesWritten,
        ulong firmwareStatus)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        RequestedBytes = requestedBytes;
        BytesWritten = bytesWritten;
        FirmwareStatus = firmwareStatus;
    }

    public bool Succeeded { get; }

    public PlatformRandomStatus Status { get; }

    public FirmwareServicesMode Mode { get; }

    public string Operation => PlatformRandomOperations.FillBytes;

    public int RequestedBytes { get; }

    public int BytesWritten { get; }

    public ulong FirmwareStatus { get; }

    public static PlatformRandomResult Fill(
        FirmwareCallResult guardResult,
        PlatformRandomSourceResult sourceResult,
        int requestedBytes)
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
            return new PlatformRandomResult(
                false,
                PlatformRandomStatus.ShortRead,
                guardResult.Mode,
                requestedBytes,
                sourceResult.BytesWritten,
                sourceResult.FirmwareStatus);
        }

        return new PlatformRandomResult(
            true,
            PlatformRandomStatus.Success,
            guardResult.Mode,
            requestedBytes,
            sourceResult.BytesWritten,
            sourceResult.FirmwareStatus);
    }

    public static PlatformRandomResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformRandomResult(
            false,
            PlatformRandomStatus.FirmwareServicesUnavailable,
            guardResult.Mode,
            0,
            0,
            0);
    }

    public static PlatformRandomResult InvalidDestination(FirmwareCallResult guardResult)
    {
        return new PlatformRandomResult(
            false,
            PlatformRandomStatus.InvalidDestination,
            guardResult.Mode,
            0,
            0,
            0);
    }

    private static PlatformRandomResult SourceFailure(
        FirmwareCallResult guardResult,
        PlatformRandomSourceResult sourceResult,
        int requestedBytes)
    {
        PlatformRandomStatus status = sourceResult.Status == PlatformRandomSourceStatus.Unavailable
            ? PlatformRandomStatus.SourceUnavailable
            : PlatformRandomStatus.SourceFailed;

        return new PlatformRandomResult(
            false,
            status,
            guardResult.Mode,
            requestedBytes,
            sourceResult.BytesWritten,
            sourceResult.FirmwareStatus);
    }
}
