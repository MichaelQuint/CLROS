namespace OS.Drivers;

using OS.Storage;

public readonly struct VirtioDeviceDescriptor
{
    internal VirtioDeviceDescriptor(int sourceIndex, VirtioPciDeviceKind deviceKind, VirtioPciBindingPlanResult bindingPlan, VirtioBlockDeviceConfigResult blockConfiguration, VirtioNetDeviceConfigParseResult networkConfiguration, VirtioDeviceInitializationProfileResult initializationProfile)
    {
        SourceIndex = sourceIndex;
        DeviceKind = deviceKind;
        BindingPlan = bindingPlan;
        BlockConfiguration = blockConfiguration;
        NetworkConfiguration = networkConfiguration;
        InitializationProfile = initializationProfile;
    }

    public int SourceIndex
    {
        get;
    }
    public VirtioPciDeviceKind DeviceKind
    {
        get;
    }
    public VirtioPciBindingPlanResult BindingPlan
    {
        get;
    }
    public VirtioPciBindingMetadata Binding => BindingPlan.Binding;
    public VirtioBlockDeviceConfigResult BlockConfiguration
    {
        get;
    }
    public VirtioNetDeviceConfigParseResult NetworkConfiguration
    {
        get;
    }
    public VirtioDeviceInitializationProfileResult InitializationProfile
    {
        get;
    }
    public bool HasBlockDeviceInfo => DeviceKind == VirtioPciDeviceKind.Block && BlockConfiguration.Succeeded;
    public BlockDeviceInfo BlockDeviceInfo => BlockConfiguration.BlockDeviceInfo;
    public bool HasNetworkConfiguration => DeviceKind == VirtioPciDeviceKind.Network && NetworkConfiguration.Succeeded;
    public VirtioNetDeviceConfig NetworkDeviceConfiguration => NetworkConfiguration.Config;
}
