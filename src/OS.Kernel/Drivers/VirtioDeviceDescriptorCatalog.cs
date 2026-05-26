namespace OS.Drivers;

using OS.Storage;

public enum VirtioDeviceDescriptorCatalogStatus
{
    Success = 0,
    MissingDeviceConfiguration = 1,
    UnsupportedDeviceKind = 2,
    ConfigParseFailed = 3,
    InitializationProfileFailed = 4,
    DestinationTooSmall = 5,
    BindingPlanFailed = 6
}

public readonly struct VirtioDeviceDescriptorSource
{
    public VirtioDeviceDescriptorSource(
        int sourceIndex,
        VirtioPciBindingPlanResult bindingPlan,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted)
        : this(
            sourceIndex,
            bindingPlan,
            bindingPlan.Succeeded ? bindingPlan.Binding.DeviceKind : VirtioPciDeviceKind.Unknown,
            deviceFeatures,
            availableQueueCount,
            featuresOkAccepted)
    {
    }

    public VirtioDeviceDescriptorSource(
        int sourceIndex,
        VirtioPciBindingPlanResult bindingPlan,
        VirtioPciDeviceKind deviceKind,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        BindingPlan = bindingPlan;
        DeviceKind = deviceKind;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex { get; }

    public VirtioPciBindingPlanResult BindingPlan { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public ulong DeviceFeatures { get; }

    public ushort AvailableQueueCount { get; }

    public bool FeaturesOkAccepted { get; }
}

public readonly struct VirtioDeviceDescriptorDerivedBindingSource
{
    public VirtioDeviceDescriptorDerivedBindingSource(
        int sourceIndex,
        VirtioPciBindingDerivedBarSizePlanResult derivedBindingPlan,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted)
        : this(
            sourceIndex,
            derivedBindingPlan,
            derivedBindingPlan.Succeeded ? derivedBindingPlan.Binding.DeviceKind : VirtioPciDeviceKind.Unknown,
            deviceFeatures,
            availableQueueCount,
            featuresOkAccepted)
    {
    }

    public VirtioDeviceDescriptorDerivedBindingSource(
        int sourceIndex,
        VirtioPciBindingDerivedBarSizePlanResult derivedBindingPlan,
        VirtioPciDeviceKind deviceKind,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted)
    {
        SourceIndex = sourceIndex;
        DerivedBindingPlan = derivedBindingPlan;
        DeviceKind = deviceKind;
        DeviceFeatures = deviceFeatures;
        AvailableQueueCount = availableQueueCount;
        FeaturesOkAccepted = featuresOkAccepted;
    }

    public int SourceIndex { get; }

    public VirtioPciBindingDerivedBarSizePlanResult DerivedBindingPlan { get; }

    public VirtioPciBindingPlanResult BindingPlan => DerivedBindingPlan.BindingPlan;

    public VirtioPciDeviceKind DeviceKind { get; }

    public ulong DeviceFeatures { get; }

    public ushort AvailableQueueCount { get; }

    public bool FeaturesOkAccepted { get; }
}

public readonly struct VirtioDeviceConfigurationSource
{
    public VirtioDeviceConfigurationSource(
        int sourceIndex,
        ReadOnlyMemory<byte> configuration)
    {
        SourceIndex = sourceIndex;
        Configuration = configuration;
    }

    public int SourceIndex { get; }

    public ReadOnlyMemory<byte> Configuration { get; }
}

public readonly struct VirtioDeviceDescriptor
{
    internal VirtioDeviceDescriptor(
        int sourceIndex,
        VirtioPciDeviceKind deviceKind,
        VirtioPciBindingPlanResult bindingPlan,
        VirtioBlockDeviceConfigResult blockConfiguration,
        VirtioNetDeviceConfigParseResult networkConfiguration,
        VirtioDeviceInitializationProfileResult initializationProfile)
    {
        SourceIndex = sourceIndex;
        DeviceKind = deviceKind;
        BindingPlan = bindingPlan;
        BlockConfiguration = blockConfiguration;
        NetworkConfiguration = networkConfiguration;
        InitializationProfile = initializationProfile;
    }

    public int SourceIndex { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public VirtioPciBindingPlanResult BindingPlan { get; }

    public VirtioPciBindingMetadata Binding => BindingPlan.Binding;

    public VirtioBlockDeviceConfigResult BlockConfiguration { get; }

    public VirtioNetDeviceConfigParseResult NetworkConfiguration { get; }

    public VirtioDeviceInitializationProfileResult InitializationProfile { get; }

    public bool HasBlockDeviceInfo =>
        DeviceKind == VirtioPciDeviceKind.Block && BlockConfiguration.Succeeded;

    public BlockDeviceInfo BlockDeviceInfo => BlockConfiguration.BlockDeviceInfo;

    public bool HasNetworkConfiguration =>
        DeviceKind == VirtioPciDeviceKind.Network && NetworkConfiguration.Succeeded;

    public VirtioNetDeviceConfig NetworkDeviceConfiguration => NetworkConfiguration.Config;
}

public readonly struct VirtioDeviceDescriptorCatalogResult
{
    private VirtioDeviceDescriptorCatalogResult(
        VirtioDeviceDescriptorCatalogStatus status,
        int inspectedSourceCount,
        int descriptorCount,
        int requiredDescriptorCapacity,
        int failingSourceIndex,
        int failingInputIndex,
        VirtioPciDeviceKind failingDeviceKind,
        VirtioPciBindingPlanResult failingBindingPlan,
        VirtioBlockDeviceConfigResult failingBlockConfiguration,
        VirtioNetDeviceConfigParseResult failingNetworkConfiguration,
        VirtioDeviceInitializationProfileResult failingInitializationProfile,
        VirtioPciBindingDerivedBarSizePlanResult failingDerivedBindingPlan,
        bool isInitialized)
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

    public VirtioDeviceDescriptorCatalogStatus Status { get; }

    public int InspectedSourceCount { get; }

    public int DescriptorCount { get; }

    public int RequiredDescriptorCapacity { get; }

    public int FailingSourceIndex { get; }

    public int FailingInputIndex { get; }

    public VirtioPciDeviceKind FailingDeviceKind { get; }

    public VirtioPciBindingPlanResult FailingBindingPlan { get; }

    public VirtioPciBindingDerivedBarSizePlanResult FailingDerivedBindingPlan { get; }

    public VirtioBlockDeviceConfigResult FailingBlockConfiguration { get; }

    public VirtioNetDeviceConfigParseResult FailingNetworkConfiguration { get; }

    public VirtioDeviceInitializationProfileResult FailingInitializationProfile { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioDeviceDescriptorCatalogStatus.Success;

    public static VirtioDeviceDescriptorCatalogResult Success(
        int inspectedSourceCount,
        int descriptorCount)
    {
        return new VirtioDeviceDescriptorCatalogResult(
            VirtioDeviceDescriptorCatalogStatus.Success,
            inspectedSourceCount,
            descriptorCount,
            descriptorCount,
            NoFailingSourceIndex,
            NoFailingInputIndex,
            default,
            default,
            default,
            default,
            default,
            default,
            isInitialized: true);
    }

    public static VirtioDeviceDescriptorCatalogResult Fail(
        VirtioDeviceDescriptorCatalogStatus status,
        int inspectedSourceCount,
        int descriptorCount,
        int requiredDescriptorCapacity,
        int failingSourceIndex,
        int failingInputIndex,
        VirtioPciDeviceKind failingDeviceKind,
        VirtioPciBindingPlanResult failingBindingPlan = default,
        VirtioBlockDeviceConfigResult failingBlockConfiguration = default,
        VirtioNetDeviceConfigParseResult failingNetworkConfiguration = default,
        VirtioDeviceInitializationProfileResult failingInitializationProfile = default,
        VirtioPciBindingDerivedBarSizePlanResult failingDerivedBindingPlan = default)
    {
        return new VirtioDeviceDescriptorCatalogResult(
            status,
            inspectedSourceCount,
            descriptorCount,
            requiredDescriptorCapacity,
            failingSourceIndex,
            failingInputIndex,
            failingDeviceKind,
            failingBindingPlan,
            failingBlockConfiguration,
            failingNetworkConfiguration,
            failingInitializationProfile,
            failingDerivedBindingPlan,
            isInitialized: true);
    }
}

public static class VirtioDeviceDescriptorCatalog
{
    public const bool RequiresCallerProvidedDescriptorSources = true;
    public const bool RequiresCallerProvidedDeviceConfiguration = true;
    public const bool RequiresCallerProvidedFeatureMetadata = true;
    public const bool UsesVirtioDeviceInitializationProfile = true;
    public const bool PerformsLiveConfigReads = false;
    public const bool PerformsLiveConfigWrites = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool ConfiguresQueues = false;
    public const bool BindsLiveDriver = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;

    public static VirtioDeviceDescriptorCatalogResult Build(
        ReadOnlySpan<VirtioDeviceDescriptorSource> sources,
        ReadOnlySpan<VirtioDeviceConfigurationSource> configurationSources,
        Span<VirtioDeviceDescriptor> destination)
    {
        int descriptorCount = 0;
        int requiredDescriptorCapacity = 0;
        int firstUnwrittenInputIndex = VirtioDeviceDescriptorCatalogResult.NoFailingInputIndex;
        int firstUnwrittenSourceIndex = VirtioDeviceDescriptorCatalogResult.NoFailingSourceIndex;

        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            VirtioDeviceDescriptorSource source = sources[sourceIndex];
            requiredDescriptorCapacity++;

            if (!source.BindingPlan.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.BindingPlanFailed,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: source.BindingPlan);
            }

            if (!IsSupportedKind(source.DeviceKind))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.UnsupportedDeviceKind,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: source.BindingPlan);
            }

            if (!TryFindConfiguration(
                    configurationSources,
                    source.SourceIndex,
                    out ReadOnlyMemory<byte> configuration))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: source.BindingPlan);
            }

            VirtioBlockDeviceConfigResult blockConfiguration = default;
            VirtioNetDeviceConfigParseResult networkConfiguration = default;

            if (source.DeviceKind == VirtioPciDeviceKind.Block)
            {
                blockConfiguration = VirtioBlockDeviceConfig.Parse(configuration.Span);
                if (!blockConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(
                        VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed,
                        inspectedSourceCount: sourceIndex + 1,
                        descriptorCount,
                        requiredDescriptorCapacity,
                        source.SourceIndex,
                        sourceIndex,
                        source.DeviceKind,
                        failingBindingPlan: source.BindingPlan,
                        failingBlockConfiguration: blockConfiguration);
                }
            }
            else
            {
                networkConfiguration = VirtioNetDeviceConfig.TryParse(configuration.Span);
                if (!networkConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(
                        VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed,
                        inspectedSourceCount: sourceIndex + 1,
                        descriptorCount,
                        requiredDescriptorCapacity,
                        source.SourceIndex,
                        sourceIndex,
                        source.DeviceKind,
                        failingBindingPlan: source.BindingPlan,
                        failingNetworkConfiguration: networkConfiguration);
                }
            }

            VirtioDeviceInitializationProfileResult initializationProfile =
                VirtioDeviceInitializationProfile.Create(
                    source.DeviceKind,
                    source.DeviceFeatures,
                    source.AvailableQueueCount,
                    source.FeaturesOkAccepted);
            if (!initializationProfile.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: source.BindingPlan,
                    failingBlockConfiguration: blockConfiguration,
                    failingNetworkConfiguration: networkConfiguration,
                    failingInitializationProfile: initializationProfile);
            }

            if (descriptorCount >= destination.Length)
            {
                if (firstUnwrittenInputIndex == VirtioDeviceDescriptorCatalogResult.NoFailingInputIndex)
                {
                    firstUnwrittenInputIndex = sourceIndex;
                    firstUnwrittenSourceIndex = source.SourceIndex;
                }

                continue;
            }

            destination[descriptorCount] = new VirtioDeviceDescriptor(
                source.SourceIndex,
                source.DeviceKind,
                source.BindingPlan,
                blockConfiguration,
                networkConfiguration,
                initializationProfile);
            descriptorCount++;
        }

        if (requiredDescriptorCapacity > destination.Length)
        {
            return VirtioDeviceDescriptorCatalogResult.Fail(
                VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall,
                inspectedSourceCount: sources.Length,
                descriptorCount,
                requiredDescriptorCapacity,
                firstUnwrittenSourceIndex,
                firstUnwrittenInputIndex,
                firstUnwrittenInputIndex >= 0
                    ? sources[firstUnwrittenInputIndex].DeviceKind
                    : default);
        }

        return VirtioDeviceDescriptorCatalogResult.Success(
            sources.Length,
            descriptorCount);
    }

    public static VirtioDeviceDescriptorCatalogResult Build(
        ReadOnlySpan<VirtioDeviceDescriptorDerivedBindingSource> sources,
        ReadOnlySpan<VirtioDeviceConfigurationSource> configurationSources,
        Span<VirtioDeviceDescriptor> destination)
    {
        int descriptorCount = 0;
        int requiredDescriptorCapacity = 0;
        int firstUnwrittenInputIndex = VirtioDeviceDescriptorCatalogResult.NoFailingInputIndex;
        int firstUnwrittenSourceIndex = VirtioDeviceDescriptorCatalogResult.NoFailingSourceIndex;

        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            VirtioDeviceDescriptorDerivedBindingSource source = sources[sourceIndex];
            requiredDescriptorCapacity++;

            if (!source.DerivedBindingPlan.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.BindingPlanFailed,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: source.BindingPlan,
                    failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            VirtioPciBindingPlanResult bindingPlan = source.DerivedBindingPlan.BindingPlan;
            if (!IsSupportedKind(source.DeviceKind))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.UnsupportedDeviceKind,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: bindingPlan,
                    failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            if (!TryFindConfiguration(
                    configurationSources,
                    source.SourceIndex,
                    out ReadOnlyMemory<byte> configuration))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: bindingPlan,
                    failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            VirtioBlockDeviceConfigResult blockConfiguration = default;
            VirtioNetDeviceConfigParseResult networkConfiguration = default;

            if (source.DeviceKind == VirtioPciDeviceKind.Block)
            {
                blockConfiguration = VirtioBlockDeviceConfig.Parse(configuration.Span);
                if (!blockConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(
                        VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed,
                        inspectedSourceCount: sourceIndex + 1,
                        descriptorCount,
                        requiredDescriptorCapacity,
                        source.SourceIndex,
                        sourceIndex,
                        source.DeviceKind,
                        failingBindingPlan: bindingPlan,
                        failingBlockConfiguration: blockConfiguration,
                        failingDerivedBindingPlan: source.DerivedBindingPlan);
                }
            }
            else
            {
                networkConfiguration = VirtioNetDeviceConfig.TryParse(configuration.Span);
                if (!networkConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(
                        VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed,
                        inspectedSourceCount: sourceIndex + 1,
                        descriptorCount,
                        requiredDescriptorCapacity,
                        source.SourceIndex,
                        sourceIndex,
                        source.DeviceKind,
                        failingBindingPlan: bindingPlan,
                        failingNetworkConfiguration: networkConfiguration,
                        failingDerivedBindingPlan: source.DerivedBindingPlan);
                }
            }

            VirtioDeviceInitializationProfileResult initializationProfile =
                VirtioDeviceInitializationProfile.Create(
                    source.DeviceKind,
                    source.DeviceFeatures,
                    source.AvailableQueueCount,
                    source.FeaturesOkAccepted);
            if (!initializationProfile.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(
                    VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed,
                    inspectedSourceCount: sourceIndex + 1,
                    descriptorCount,
                    requiredDescriptorCapacity,
                    source.SourceIndex,
                    sourceIndex,
                    source.DeviceKind,
                    failingBindingPlan: bindingPlan,
                    failingBlockConfiguration: blockConfiguration,
                    failingNetworkConfiguration: networkConfiguration,
                    failingInitializationProfile: initializationProfile,
                    failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            if (descriptorCount >= destination.Length)
            {
                if (firstUnwrittenInputIndex == VirtioDeviceDescriptorCatalogResult.NoFailingInputIndex)
                {
                    firstUnwrittenInputIndex = sourceIndex;
                    firstUnwrittenSourceIndex = source.SourceIndex;
                }

                continue;
            }

            destination[descriptorCount] = new VirtioDeviceDescriptor(
                source.SourceIndex,
                source.DeviceKind,
                bindingPlan,
                blockConfiguration,
                networkConfiguration,
                initializationProfile);
            descriptorCount++;
        }

        if (requiredDescriptorCapacity > destination.Length)
        {
            return VirtioDeviceDescriptorCatalogResult.Fail(
                VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall,
                inspectedSourceCount: sources.Length,
                descriptorCount,
                requiredDescriptorCapacity,
                firstUnwrittenSourceIndex,
                firstUnwrittenInputIndex,
                firstUnwrittenInputIndex >= 0
                    ? sources[firstUnwrittenInputIndex].DeviceKind
                    : default,
                failingDerivedBindingPlan: firstUnwrittenInputIndex >= 0
                    ? sources[firstUnwrittenInputIndex].DerivedBindingPlan
                    : default);
        }

        return VirtioDeviceDescriptorCatalogResult.Success(
            sources.Length,
            descriptorCount);
    }

    private static bool IsSupportedKind(VirtioPciDeviceKind deviceKind)
    {
        return deviceKind == VirtioPciDeviceKind.Block ||
            deviceKind == VirtioPciDeviceKind.Network;
    }

    private static bool TryFindConfiguration(
        ReadOnlySpan<VirtioDeviceConfigurationSource> sources,
        int sourceIndex,
        out ReadOnlyMemory<byte> configuration)
    {
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index].SourceIndex == sourceIndex)
            {
                configuration = sources[index].Configuration;
                return true;
            }
        }

        configuration = default;
        return false;
    }
}
