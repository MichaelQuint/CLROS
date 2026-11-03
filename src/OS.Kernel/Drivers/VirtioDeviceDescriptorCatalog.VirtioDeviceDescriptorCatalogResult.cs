namespace OS.Drivers;

using OS.Storage;

public readonly struct VirtioDeviceDescriptorCatalogResult
{
    private VirtioDeviceDescriptorCatalogResult(VirtioDeviceDescriptorCatalogStatus status, int inspectedSourceCount, int descriptorCount, int requiredDescriptorCapacity, int failingSourceIndex, int failingInputIndex, VirtioPciDeviceKind failingDeviceKind, VirtioPciBindingPlanResult failingBindingPlan, VirtioBlockDeviceConfigResult failingBlockConfiguration, VirtioNetDeviceConfigParseResult failingNetworkConfiguration, VirtioDeviceInitializationProfileResult failingInitializationProfile, VirtioPciBindingDerivedBarSizePlanResult failingDerivedBindingPlan, bool isInitialized)
    {
        Status = status;
        InspectedSourceCount = inspectedSourceCount;
        DescriptorCount = descriptorCount;
        RequiredDescriptorCapacity = requiredDescriptorCapacity;
        FailingSourceIndex = failingSourceIndex;
        FailingInputIndex = failingInputIndex;
        FailingDeviceKind = failingDeviceKind;
        FailingBindingPlan = failingBindingPlan;
        FailingBlockConfiguration = failingBlockConfiguration;
        FailingNetworkConfiguration = failingNetworkConfiguration;
        FailingInitializationProfile = failingInitializationProfile;
        FailingDerivedBindingPlan = failingDerivedBindingPlan;
        IsInitialized = isInitialized;
    }

    public const int NoFailingSourceIndex = -1;
    public const int NoFailingInputIndex = -1;
    public VirtioDeviceDescriptorCatalogStatus Status
    {
        get;
    }
    public int InspectedSourceCount
    {
        get;
    }
    public int DescriptorCount
    {
        get;
    }
    public int RequiredDescriptorCapacity
    {
        get;
    }
    public int FailingSourceIndex
    {
        get;
    }
    public int FailingInputIndex
    {
        get;
    }
    public VirtioPciDeviceKind FailingDeviceKind
    {
        get;
    }
    public VirtioPciBindingPlanResult FailingBindingPlan
    {
        get;
    }
    public VirtioPciBindingDerivedBarSizePlanResult FailingDerivedBindingPlan
    {
        get;
    }
    public VirtioBlockDeviceConfigResult FailingBlockConfiguration
    {
        get;
    }
    public VirtioNetDeviceConfigParseResult FailingNetworkConfiguration
    {
        get;
    }
    public VirtioDeviceInitializationProfileResult FailingInitializationProfile
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioDeviceDescriptorCatalogStatus.Success;

    public static VirtioDeviceDescriptorCatalogResult Success(int inspectedSourceCount, int descriptorCount)
    {
        return new VirtioDeviceDescriptorCatalogResult(VirtioDeviceDescriptorCatalogStatus.Success, inspectedSourceCount, descriptorCount, descriptorCount, NoFailingSourceIndex, NoFailingInputIndex, default, default, default, default, default, default, isInitialized: true);
    }

    public static VirtioDeviceDescriptorCatalogResult Fail(VirtioDeviceDescriptorCatalogStatus status, int inspectedSourceCount, int descriptorCount, int requiredDescriptorCapacity, int failingSourceIndex, int failingInputIndex, VirtioPciDeviceKind failingDeviceKind, VirtioPciBindingPlanResult failingBindingPlan = default, VirtioBlockDeviceConfigResult failingBlockConfiguration = default, VirtioNetDeviceConfigParseResult failingNetworkConfiguration = default, VirtioDeviceInitializationProfileResult failingInitializationProfile = default, VirtioPciBindingDerivedBarSizePlanResult failingDerivedBindingPlan = default)
    {
        return new VirtioDeviceDescriptorCatalogResult(status, inspectedSourceCount, descriptorCount, requiredDescriptorCapacity, failingSourceIndex, failingInputIndex, failingDeviceKind, failingBindingPlan, failingBlockConfiguration, failingNetworkConfiguration, failingInitializationProfile, failingDerivedBindingPlan, isInitialized: true);
    }
}
