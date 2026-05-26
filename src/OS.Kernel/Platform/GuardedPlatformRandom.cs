namespace OS.Kernel.Platform;

public sealed class GuardedPlatformRandom : IPlatformRandom
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformRandomSource _source;

    public GuardedPlatformRandom(IPlatformFirmware firmware, IPlatformRandomSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformRandomResult FillBytes(Span<byte> destination)
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformRandomOperations.FillBytes);

        if (!guardResult.Succeeded)
        {
            return PlatformRandomResult.FirmwareServicesUnavailable(guardResult);
        }

        if (destination.IsEmpty)
        {
            return PlatformRandomResult.InvalidDestination(guardResult);
        }

        PlatformRandomSourceResult sourceResult = _source.FillBytes(destination);
        return PlatformRandomResult.Fill(guardResult, sourceResult, destination.Length);
    }
}
