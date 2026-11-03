namespace OS.Drivers;

using OS.Storage;

public readonly struct VirtioDeviceConfigurationSource
{
    public VirtioDeviceConfigurationSource(int sourceIndex, ReadOnlyMemory<byte> configuration)
    {
        SourceIndex = sourceIndex;
        Configuration = configuration;
    }

    public int SourceIndex
    {
        get;
    }
    public ReadOnlyMemory<byte> Configuration
    {
        get;
    }
}
