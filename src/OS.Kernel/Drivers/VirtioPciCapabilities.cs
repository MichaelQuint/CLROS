namespace OS.Drivers;

public enum VirtioPciCapabilityType : byte
{
    CommonConfiguration = 1,
    NotifyConfiguration = 2,
    IsrStatus = 3,
    DeviceConfiguration = 4,
    PciConfiguration = 5
}

public readonly struct VirtioPciCapabilityMetadata
{
    public VirtioPciCapabilityMetadata(
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier)
    {
        Type = type;
        Bar = bar;
        Offset = offset;
        Length = length;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
    }

    public VirtioPciCapabilityType Type { get; }

    public byte Bar { get; }

    public uint Offset { get; }

    public uint Length { get; }

    public uint NotifyOffsetMultiplier { get; }
}

public readonly struct VirtioPciCapabilitySet
{
    private VirtioPciCapabilitySet(
        VirtioPciCapabilityMetadata commonConfiguration,
        VirtioPciCapabilityMetadata notifyConfiguration,
        VirtioPciCapabilityMetadata isrStatus,
        VirtioPciCapabilityMetadata deviceConfiguration,
        bool hasCommonConfiguration,
        bool hasNotifyConfiguration,
        bool hasIsrStatus,
        bool hasDeviceConfiguration)
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

    public VirtioPciCapabilityMetadata CommonConfiguration { get; }

    public VirtioPciCapabilityMetadata NotifyConfiguration { get; }

    public VirtioPciCapabilityMetadata IsrStatus { get; }

    public VirtioPciCapabilityMetadata DeviceConfiguration { get; }

    public bool HasCommonConfiguration { get; }

    public bool HasNotifyConfiguration { get; }

    public bool HasIsrStatus { get; }

    public bool HasDeviceConfiguration { get; }

    public static VirtioPciCapabilitySet Create(
        VirtioPciCapabilityMetadata commonConfiguration,
        VirtioPciCapabilityMetadata notifyConfiguration,
        VirtioPciCapabilityMetadata isrStatus,
        VirtioPciCapabilityMetadata deviceConfiguration)
    {
        return new VirtioPciCapabilitySet(
            commonConfiguration,
            notifyConfiguration,
            isrStatus,
            deviceConfiguration,
            hasCommonConfiguration: true,
            hasNotifyConfiguration: true,
            hasIsrStatus: true,
            hasDeviceConfiguration: true);
    }
}
