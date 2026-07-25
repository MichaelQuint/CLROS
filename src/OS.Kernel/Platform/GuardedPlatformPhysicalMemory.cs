namespace OS.Kernel.Platform;

public sealed class GuardedPlatformPhysicalMemory : IPlatformPhysicalMemory
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformPhysicalMemorySource _source;

    public GuardedPlatformPhysicalMemory(
        IPlatformFirmware firmware,
        IPlatformPhysicalMemorySource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformPhysicalMemoryResult ReadBytes(
        ulong physicalAddress,
        int byteCount,
        Span<byte> destination)
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformPhysicalMemoryOperations.ReadBytes);

        if (!guardResult.Succeeded)
        {
            return PlatformPhysicalMemoryResult.FirmwareServicesUnavailable(guardResult);
        }

        if (!IsValidRange(physicalAddress, byteCount))
        {
            return PlatformPhysicalMemoryResult.InvalidRange(
                guardResult,
                physicalAddress,
                byteCount);
        }

        if (destination.IsEmpty)
        {
            return PlatformPhysicalMemoryResult.InvalidDestination(
                guardResult,
                physicalAddress,
                byteCount);
        }

        if (destination.Length < byteCount)
        {
            return PlatformPhysicalMemoryResult.DestinationTooSmall(
                guardResult,
                physicalAddress,
                byteCount);
        }

        PlatformPhysicalMemorySourceResult sourceResult =
            _source.ReadBytes(physicalAddress, byteCount, destination.Slice(0, byteCount));

        return PlatformPhysicalMemoryResult.Read(
            guardResult,
            sourceResult,
            physicalAddress,
            byteCount);
    }

    private static bool IsValidRange(ulong physicalAddress, int byteCount)
    {
        if (physicalAddress == 0 || byteCount <= 0)
        {
            return false;
        }

        ulong bytes = (ulong)byteCount;
        ulong exclusiveEnd = physicalAddress + bytes;
        return exclusiveEnd > physicalAddress;
    }
}
