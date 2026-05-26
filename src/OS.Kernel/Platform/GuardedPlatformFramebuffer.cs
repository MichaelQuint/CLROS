namespace OS.Kernel.Platform;

public sealed class GuardedPlatformFramebuffer : IPlatformFramebuffer
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformFramebufferSource _source;

    public GuardedPlatformFramebuffer(
        IPlatformFirmware firmware,
        IPlatformFramebufferSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformFramebufferResult CaptureSnapshot()
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformFramebufferOperations.CaptureSnapshot);

        if (!guardResult.Succeeded)
        {
            return PlatformFramebufferResult.FirmwareServicesUnavailable(guardResult);
        }

        return PlatformFramebufferResult.Capture(
            guardResult,
            _source.CaptureSnapshot());
    }
}
