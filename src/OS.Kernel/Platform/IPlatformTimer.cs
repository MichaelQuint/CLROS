namespace OS.Kernel.Platform;

public interface IPlatformTimer
{
    PlatformTimerResult ReadMonotonic();
}

public interface IPlatformTimerSource
{
    PlatformTimerMetadata ReadMonotonic();
}

public static class PlatformTimerOperations
{
    public const string ReadMonotonic = "Timer.ReadMonotonic";
}

public enum PlatformTimerStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidMetadata = 3
}

public readonly struct PlatformTimerMetadata
{
    public PlatformTimerMetadata(ulong monotonicTicks, ulong frequencyHertz)
    {
        MonotonicTicks = monotonicTicks;
        FrequencyHertz = frequencyHertz;
    }

    public ulong MonotonicTicks { get; }

    public ulong FrequencyHertz { get; }

    public bool IsValid => FrequencyHertz != 0;
}

public readonly struct PlatformTimerResult
{
    private PlatformTimerResult(
        bool succeeded,
        PlatformTimerStatus status,
        FirmwareServicesMode mode,
        PlatformTimerMetadata metadata)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        Metadata = metadata;
    }

    public bool Succeeded { get; }

    public PlatformTimerStatus Status { get; }

    public FirmwareServicesMode Mode { get; }

    public string Operation => PlatformTimerOperations.ReadMonotonic;

    public PlatformTimerMetadata Metadata { get; }

    public ulong MonotonicTicks => Metadata.MonotonicTicks;

    public ulong FrequencyHertz => Metadata.FrequencyHertz;

    public static PlatformTimerResult Read(
        FirmwareCallResult guardResult,
        PlatformTimerMetadata metadata)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!metadata.IsValid)
        {
            return InvalidMetadata(guardResult);
        }

        return new PlatformTimerResult(
            true,
            PlatformTimerStatus.Success,
            guardResult.Mode,
            metadata);
    }

    public static PlatformTimerResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformTimerResult(
            false,
            PlatformTimerStatus.FirmwareServicesUnavailable,
            guardResult.Mode,
            default);
    }

    public static PlatformTimerResult InvalidMetadata(FirmwareCallResult guardResult)
    {
        return new PlatformTimerResult(
            false,
            PlatformTimerStatus.InvalidMetadata,
            guardResult.Mode,
            default);
    }
}
