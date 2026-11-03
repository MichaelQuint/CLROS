namespace OS.Drivers;

public readonly struct VirtioPciConfigurationSource
{
    public VirtioPciConfigurationSource(int sourceIndex, ReadOnlyMemory<byte> configurationSpace)
    {
        SourceIndex = sourceIndex;
        ConfigurationSpace = configurationSpace;
    }

    public int SourceIndex
    {
        get;
    }
    public ReadOnlyMemory<byte> ConfigurationSpace
    {
        get;
    }
}
