namespace OS.Kernel.Platform;

public sealed class GuardedPlatformFileSystem : IPlatformFileSystem
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformFileSystemSource _source;

    public GuardedPlatformFileSystem(IPlatformFirmware firmware, IPlatformFileSystemSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformFileSystemReadResult ReadFile(
        ReadOnlySpan<byte> relativePath,
        Span<byte> destination)
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformFileSystemOperations.ReadFile);

        if (!guardResult.Succeeded)
        {
            return PlatformFileSystemReadResult.FirmwareServicesUnavailable(guardResult);
        }

        if (!PlatformFileSystemReadResult.IsSafeRelativeAsciiPath(relativePath))
        {
            return PlatformFileSystemReadResult.InvalidPath(
                guardResult,
                relativePath.Length,
                destination.Length);
        }

        PlatformFileSystemSourceReadResult sourceResult =
            _source.ReadFile(relativePath, destination);

        return PlatformFileSystemReadResult.Read(
            guardResult,
            relativePath,
            sourceResult,
            destination.Length);
    }
}
