namespace OS.Drivers;

public readonly struct VirtioPciCapabilitySet
{
    private VirtioPciCapabilitySet(VirtioPciCapabilityMetadata commonConfiguration, VirtioPciCapabilityMetadata notifyConfiguration, VirtioPciCapabilityMetadata isrStatus, VirtioPciCapabilityMetadata deviceConfiguration, bool hasCommonConfiguration, bool hasNotifyConfiguration, bool hasIsrStatus, bool hasDeviceConfiguration)
    {
        CommonConfiguration = commonConfiguration;
        NotifyConfiguration = notifyConfiguration;
        IsrStatus = isrStatus;
        DeviceConfiguration = deviceConfiguration;
        HasCommonConfiguration = hasCommonConfiguration;
        HasNotifyConfiguration = hasNotifyConfiguration;
        HasIsrStatus = hasIsrStatus;
        HasDeviceConfiguration = hasDeviceConfiguration;
    }

    public VirtioPciCapabilityMetadata CommonConfiguration
    {
        get;
    }
    public VirtioPciCapabilityMetadata NotifyConfiguration
    {
        get;
    }
    public VirtioPciCapabilityMetadata IsrStatus
    {
        get;
    }
    public VirtioPciCapabilityMetadata DeviceConfiguration
    {
        get;
    }
    public bool HasCommonConfiguration
    {
        get;
    }
    public bool HasNotifyConfiguration
    {
        get;
    }
    public bool HasIsrStatus
    {
        get;
    }
    public bool HasDeviceConfiguration
    {
        get;
    }

    public static VirtioPciCapabilitySet Create(VirtioPciCapabilityMetadata commonConfiguration, VirtioPciCapabilityMetadata notifyConfiguration, VirtioPciCapabilityMetadata isrStatus, VirtioPciCapabilityMetadata deviceConfiguration)
    {
        return new VirtioPciCapabilitySet(commonConfiguration, notifyConfiguration, isrStatus, deviceConfiguration, hasCommonConfiguration: true, hasNotifyConfiguration: true, hasIsrStatus: true, hasDeviceConfiguration: true);
    }
}
