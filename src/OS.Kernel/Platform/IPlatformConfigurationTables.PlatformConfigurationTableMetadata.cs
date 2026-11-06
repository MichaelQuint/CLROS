using OS.Boot;

namespace OS.Kernel.Platform;

public readonly struct PlatformConfigurationTableMetadata
{
    public PlatformConfigurationTableMetadata(ulong tableAddress, ulong tableCount)
    {
        TableAddress = tableAddress;
        TableCount = tableCount;
    }

    public ulong TableAddress
    {
        get;
    }
    public ulong TableCount
    {
        get;
    }
}
