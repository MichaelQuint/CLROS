namespace OS.Drivers;

using OS.Storage;

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
    public static VirtioDeviceDescriptorCatalogResult Build(ReadOnlySpan<VirtioDeviceDescriptorSource> sources, ReadOnlySpan<VirtioDeviceConfigurationSource> configurationSources, Span<VirtioDeviceDescriptor> destination)
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
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.BindingPlanFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan);
            }

            if (!IsSupportedKind(source.DeviceKind))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.UnsupportedDeviceKind, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan);
            }

            if (!TryFindConfiguration(configurationSources, source.SourceIndex, out ReadOnlyMemory<byte> configuration))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan);
            }

            VirtioBlockDeviceConfigResult blockConfiguration = default;
            VirtioNetDeviceConfigParseResult networkConfiguration = default;
            if (source.DeviceKind == VirtioPciDeviceKind.Block)
            {
                blockConfiguration = VirtioBlockDeviceConfig.Parse(configuration.Span);
                if (!blockConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan, failingBlockConfiguration: blockConfiguration);
                }
            }
            else
            {
                networkConfiguration = VirtioNetDeviceConfig.TryParse(configuration.Span);
                if (!networkConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan, failingNetworkConfiguration: networkConfiguration);
                }
            }

            VirtioDeviceInitializationProfileResult initializationProfile = VirtioDeviceInitializationProfile.Create(source.DeviceKind, source.DeviceFeatures, source.AvailableQueueCount, source.FeaturesOkAccepted);
            if (!initializationProfile.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan, failingBlockConfiguration: blockConfiguration, failingNetworkConfiguration: networkConfiguration, failingInitializationProfile: initializationProfile);
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

            destination[descriptorCount] = new VirtioDeviceDescriptor(source.SourceIndex, source.DeviceKind, source.BindingPlan, blockConfiguration, networkConfiguration, initializationProfile);
            descriptorCount++;
        }

        if (requiredDescriptorCapacity > destination.Length)
        {
            return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall, inspectedSourceCount: sources.Length, descriptorCount, requiredDescriptorCapacity, firstUnwrittenSourceIndex, firstUnwrittenInputIndex, firstUnwrittenInputIndex >= 0 ? sources[firstUnwrittenInputIndex].DeviceKind : default);
        }

        return VirtioDeviceDescriptorCatalogResult.Success(sources.Length, descriptorCount);
    }

    public static VirtioDeviceDescriptorCatalogResult Build(ReadOnlySpan<VirtioDeviceDescriptorDerivedBindingSource> sources, ReadOnlySpan<VirtioDeviceConfigurationSource> configurationSources, Span<VirtioDeviceDescriptor> destination)
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
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.BindingPlanFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: source.BindingPlan, failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            VirtioPciBindingPlanResult bindingPlan = source.DerivedBindingPlan.BindingPlan;
            if (!IsSupportedKind(source.DeviceKind))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.UnsupportedDeviceKind, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: bindingPlan, failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            if (!TryFindConfiguration(configurationSources, source.SourceIndex, out ReadOnlyMemory<byte> configuration))
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: bindingPlan, failingDerivedBindingPlan: source.DerivedBindingPlan);
            }

            VirtioBlockDeviceConfigResult blockConfiguration = default;
            VirtioNetDeviceConfigParseResult networkConfiguration = default;
            if (source.DeviceKind == VirtioPciDeviceKind.Block)
            {
                blockConfiguration = VirtioBlockDeviceConfig.Parse(configuration.Span);
                if (!blockConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: bindingPlan, failingBlockConfiguration: blockConfiguration, failingDerivedBindingPlan: source.DerivedBindingPlan);
                }
            }
            else
            {
                networkConfiguration = VirtioNetDeviceConfig.TryParse(configuration.Span);
                if (!networkConfiguration.Succeeded)
                {
                    return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: bindingPlan, failingNetworkConfiguration: networkConfiguration, failingDerivedBindingPlan: source.DerivedBindingPlan);
                }
            }

            VirtioDeviceInitializationProfileResult initializationProfile = VirtioDeviceInitializationProfile.Create(source.DeviceKind, source.DeviceFeatures, source.AvailableQueueCount, source.FeaturesOkAccepted);
            if (!initializationProfile.Succeeded)
            {
                return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed, inspectedSourceCount: sourceIndex + 1, descriptorCount, requiredDescriptorCapacity, source.SourceIndex, sourceIndex, source.DeviceKind, failingBindingPlan: bindingPlan, failingBlockConfiguration: blockConfiguration, failingNetworkConfiguration: networkConfiguration, failingInitializationProfile: initializationProfile, failingDerivedBindingPlan: source.DerivedBindingPlan);
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

            destination[descriptorCount] = new VirtioDeviceDescriptor(source.SourceIndex, source.DeviceKind, bindingPlan, blockConfiguration, networkConfiguration, initializationProfile);
            descriptorCount++;
        }

        if (requiredDescriptorCapacity > destination.Length)
        {
            return VirtioDeviceDescriptorCatalogResult.Fail(VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall, inspectedSourceCount: sources.Length, descriptorCount, requiredDescriptorCapacity, firstUnwrittenSourceIndex, firstUnwrittenInputIndex, firstUnwrittenInputIndex >= 0 ? sources[firstUnwrittenInputIndex].DeviceKind : default, failingDerivedBindingPlan: firstUnwrittenInputIndex >= 0 ? sources[firstUnwrittenInputIndex].DerivedBindingPlan : default);
        }

        return VirtioDeviceDescriptorCatalogResult.Success(sources.Length, descriptorCount);
    }

    private static bool IsSupportedKind(VirtioPciDeviceKind deviceKind)
    {
        return deviceKind == VirtioPciDeviceKind.Block || deviceKind == VirtioPciDeviceKind.Network;
    }

    private static bool TryFindConfiguration(ReadOnlySpan<VirtioDeviceConfigurationSource> sources, int sourceIndex, out ReadOnlyMemory<byte> configuration)
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
