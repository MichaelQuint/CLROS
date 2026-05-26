namespace OS.Kernel.Platform;

public sealed class GuardedPlatformTimer : IPlatformTimer
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformTimerSource _source;

    public GuardedPlatformTimer(IPlatformFirmware firmware, IPlatformTimerSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformTimerResult ReadMonotonic()
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformTimerOperations.ReadMonotonic);

        if (!guardResult.Succeeded)
        {
            return PlatformTimerResult.FirmwareServicesUnavailable(guardResult);
        }

        return PlatformTimerResult.Read(guardResult, _source.ReadMonotonic());
    }
}
