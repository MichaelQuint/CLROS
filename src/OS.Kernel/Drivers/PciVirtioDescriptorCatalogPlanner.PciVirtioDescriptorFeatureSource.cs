namespace OS.Drivers;

public readonly struct PciVirtioDescriptorFeatureSource
{
    public PciVirtioDescriptorFeatureSource(int sourceIndex, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex
    {
        get;
    }
    public ulong DeviceFeatures
    {
        get;
    }
    public ushort AvailableQueueCount
    {
        get;
    }
    public bool FeaturesOkAccepted
    {
        get;
    }
}
