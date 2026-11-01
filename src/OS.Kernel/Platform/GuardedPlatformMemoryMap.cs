using OS.Boot;

namespace OS.Kernel.Platform;
public sealed class GuardedPlatformMemoryMap : IPlatformMemoryMap
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformMemoryMapSource _source;
    public GuardedPlatformMemoryMap(IPlatformFirmware firmware, IPlatformMemoryMapSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformMemoryMapResult CopySnapshot()
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(PlatformMemoryMapOperations.GetMemoryMap);
        if (!guardResult.Succeeded)
        {
            return PlatformMemoryMapResult.FirmwareServicesUnavailable(guardResult);
        }

        return PlatformMemoryMapResult.Copied(guardResult, _source.CaptureSnapshot());
    }
}
