using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static class VirtioDeviceDescriptorCatalogTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ulong UnsupportedDeviceFeature = 1UL << 42;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtio.descriptor.catalog creates block descriptor", TestCreatesBlockDescriptor, ref failures);
        Run("drivers.virtio.descriptor.catalog creates network descriptor", TestCreatesNetworkDescriptor, ref failures);
        Run("drivers.virtio.descriptor.catalog reports missing device config", TestReportsMissingDeviceConfig, ref failures);
        Run("drivers.virtio.descriptor.catalog reports unsupported kind", TestReportsUnsupportedKind, ref failures);
        Run("drivers.virtio.descriptor.catalog reports config parse failure", TestReportsConfigParseFailure, ref failures);
        Run("drivers.virtio.descriptor.catalog reports initialization failure", TestReportsInitializationFailure, ref failures);
        Run("drivers.virtio.descriptor.catalog reports destination exhaustion", TestReportsDestinationExhaustion, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive creates block descriptor", TestDerivedCreatesBlockDescriptor, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive creates network descriptor", TestDerivedCreatesNetworkDescriptor, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive reports derived binding failure", TestDerivedReportsBindingFailure, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive reports missing device config", TestDerivedReportsMissingDeviceConfig, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive reports config parse failure", TestDerivedReportsConfigParseFailure, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive reports initialization failure", TestDerivedReportsInitializationFailure, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive reports destination exhaustion", TestDerivedReportsDestinationExhaustion, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("drivers.virtio.descriptor.catalog.derive preserves no-live-device boundary", TestDerivedPreservesNoLiveDeviceBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio device descriptor catalog host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestCreatesBlockDescriptor()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 4,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(4, CreateBlockConfiguration(capacitySectors: 64))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        VirtioDeviceDescriptor descriptor = destination[0];

        return result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.Success
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 1
            && result.RequiredDescriptorCapacity == 1
            && descriptor.SourceIndex == 4
            && descriptor.DeviceKind == VirtioPciDeviceKind.Block
            && descriptor.BindingPlan.Succeeded
            && descriptor.Binding.DeviceKind == VirtioPciDeviceKind.Block
            && descriptor.HasBlockDeviceInfo
            && descriptor.BlockConfiguration.Status == VirtioBlockDeviceConfigStatus.Success
            && descriptor.BlockDeviceInfo.BlockSize == VirtioBlockDeviceConfig.SectorSizeBytes
            && descriptor.BlockDeviceInfo.BlockCount == 64
            && descriptor.BlockDeviceInfo.CapacityBytes == 64UL * VirtioBlockDeviceConfig.SectorSizeBytes
            && !descriptor.HasNetworkConfiguration
            && descriptor.InitializationProfile.Succeeded
            && descriptor.InitializationProfile.RequiredQueueCount
                == VirtioDeviceInitializationProfile.BlockRequiredQueueCount
            && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask;
    }

    private static bool TestCreatesNetworkDescriptor()
    {
        MacAddress macAddress = new MacAddress(0x02, 0x10, 0x20, 0x30, 0x40, 0x50);
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 6,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 2)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                6,
                CreateNetworkConfiguration(
                    macAddress,
                    VirtioNetDeviceConfig.StatusLinkUp | VirtioNetDeviceConfig.StatusAnnounce))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        VirtioDeviceDescriptor descriptor = destination[0];

        return result.Succeeded
            && descriptor.SourceIndex == 6
            && descriptor.DeviceKind == VirtioPciDeviceKind.Network
            && descriptor.BindingPlan.Succeeded
            && descriptor.HasNetworkConfiguration
            && descriptor.NetworkConfiguration.Status == VirtioNetDeviceConfigStatus.Success
            && descriptor.NetworkDeviceConfiguration.MacAddress == macAddress
            && descriptor.NetworkDeviceConfiguration.HasStatus
            && descriptor.NetworkDeviceConfiguration.IsLinkUp
            && descriptor.NetworkDeviceConfiguration.HasAnnounce
            && !descriptor.HasBlockDeviceInfo
            && descriptor.InitializationProfile.Succeeded
            && descriptor.InitializationProfile.RequiredQueueCount
                == VirtioDeviceInitializationProfile.NetworkRequiredQueueCount
            && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask;
    }

    private static bool TestReportsMissingDeviceConfig()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 8,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            Array.Empty<VirtioDeviceConfigurationSource>(),
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 0
            && result.RequiredDescriptorCapacity == 1
            && result.FailingSourceIndex == 8
            && result.FailingInputIndex == 0
            && result.FailingDeviceKind == VirtioPciDeviceKind.Block
            && result.FailingBindingPlan.Succeeded
            && result.FailingBlockConfiguration.Status == default
            && result.FailingNetworkConfiguration.Status == default
            && result.FailingInitializationProfile.Status == default;
    }

    private static bool TestReportsUnsupportedKind()
    {
        VirtioDeviceDescriptorSource source = CreateSource(
            sourceIndex: 9,
            VirtioPciDeviceDiscovery.BlockDeviceId,
            VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
            availableQueueCount: 1);
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                source.SourceIndex,
                source.BindingPlan,
                (VirtioPciDeviceKind)99,
                source.DeviceFeatures,
                source.AvailableQueueCount,
                source.FeaturesOkAccepted)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(9, CreateBlockConfiguration(capacitySectors: 2))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.UnsupportedDeviceKind
            && result.FailingSourceIndex == 9
            && result.FailingInputIndex == 0
            && result.FailingDeviceKind == (VirtioPciDeviceKind)99
            && result.FailingBindingPlan.Succeeded
            && result.FailingBindingPlan.Binding.DeviceKind == VirtioPciDeviceKind.Block;
    }

    private static bool TestReportsConfigParseFailure()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 10,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                10,
                new byte[VirtioBlockDeviceConfig.CapacityLength - 1])
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 0
            && result.RequiredDescriptorCapacity == 1
            && result.FailingSourceIndex == 10
            && result.FailingInputIndex == 0
            && result.FailingDeviceKind == VirtioPciDeviceKind.Block
            && result.FailingBlockConfiguration.Status == VirtioBlockDeviceConfigStatus.ConfigTooSmall
            && result.FailingNetworkConfiguration.Status == default
            && result.FailingInitializationProfile.Status == default;
    }

    private static bool TestReportsInitializationFailure()
    {
        MacAddress macAddress = new MacAddress(0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE);
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 12,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(12, CreateNetworkConfiguration(macAddress, statusBits: 0))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 0
            && result.RequiredDescriptorCapacity == 1
            && result.FailingSourceIndex == 12
            && result.FailingInputIndex == 0
            && result.FailingDeviceKind == VirtioPciDeviceKind.Network
            && result.FailingNetworkConfiguration.Succeeded
            && result.FailingNetworkConfiguration.Config.MacAddress == macAddress
            && result.FailingInitializationProfile.Status
                == VirtioDeviceInitializationProfileStatus.InitializationPlanFailed
            && result.FailingInitializationProfile.InitializationPlan.Status
                == VirtioDeviceInitializationPlanStatus.InsufficientQueueCount;
    }

    private static bool TestReportsDestinationExhaustion()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            CreateSource(
                sourceIndex: 20,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1),
            CreateSource(
                sourceIndex: 21,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 2)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(20, CreateBlockConfiguration(capacitySectors: 16)),
            new VirtioDeviceConfigurationSource(
                21,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x01, 0x02, 0x03, 0x04, 0x05),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall
            && result.InspectedSourceCount == 2
            && result.DescriptorCount == 1
            && result.RequiredDescriptorCapacity == 2
            && result.FailingInputIndex == 1
            && result.FailingSourceIndex == 21
            && result.FailingDeviceKind == VirtioPciDeviceKind.Network
            && destination[0].SourceIndex == 20
            && destination[0].DeviceKind == VirtioPciDeviceKind.Block
            && destination[0].BlockDeviceInfo.BlockCount == 16;
    }

    private static bool TestDerivedCreatesBlockDescriptor()
    {
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 24,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(24, CreateBlockConfiguration(capacitySectors: 128))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        VirtioDeviceDescriptor descriptor = destination[0];

        return result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.Success
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 1
            && result.FailingDerivedBindingPlan.Status == default
            && descriptor.SourceIndex == 24
            && descriptor.DeviceKind == VirtioPciDeviceKind.Block
            && descriptor.BindingPlan.Succeeded
            && descriptor.HasBlockDeviceInfo
            && descriptor.BlockDeviceInfo.BlockCount == 128
            && descriptor.InitializationProfile.Succeeded
            && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask;
    }

    private static bool TestDerivedCreatesNetworkDescriptor()
    {
        MacAddress macAddress = new MacAddress(0x02, 0x11, 0x22, 0x33, 0x44, 0x55);
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 26,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 2)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                26,
                CreateNetworkConfiguration(macAddress, VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        VirtioDeviceDescriptor descriptor = destination[0];

        return result.Succeeded
            && descriptor.SourceIndex == 26
            && descriptor.DeviceKind == VirtioPciDeviceKind.Network
            && descriptor.BindingPlan.Succeeded
            && descriptor.HasNetworkConfiguration
            && descriptor.NetworkDeviceConfiguration.MacAddress == macAddress
            && descriptor.NetworkDeviceConfiguration.IsLinkUp
            && descriptor.InitializationProfile.Succeeded
            && descriptor.InitializationProfile.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask;
    }

    private static bool TestDerivedReportsBindingFailure()
    {
        FakePciConfigDwordAccessor accessor = CreateSuccessfulAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF008);
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            new VirtioDeviceDescriptorDerivedBindingSource(
                sourceIndex: 28,
                CreateDerivedBindingPlan(VirtioPciDeviceDiscovery.BlockDeviceId, accessor),
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(28, CreateBlockConfiguration(capacitySectors: 2))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.BindingPlanFailed
            && result.InspectedSourceCount == 1
            && result.DescriptorCount == 0
            && result.FailingSourceIndex == 28
            && result.FailingInputIndex == 0
            && result.FailingDerivedBindingPlan.Status == VirtioPciBindingDerivedBarSizePlanStatus.DerivedSizeFailed
            && result.FailingDerivedBindingPlan.SizingStatus == PciBarSizingPlanStatus.MalformedReadback
            && result.FailingBindingPlan.Status == default;
    }

    private static bool TestDerivedReportsMissingDeviceConfig()
    {
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 30,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            Array.Empty<VirtioDeviceConfigurationSource>(),
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.MissingDeviceConfiguration
            && result.FailingSourceIndex == 30
            && result.FailingInputIndex == 0
            && result.FailingDerivedBindingPlan.Succeeded
            && result.FailingBindingPlan.Succeeded
            && result.FailingBlockConfiguration.Status == default;
    }

    private static bool TestDerivedReportsConfigParseFailure()
    {
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 32,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                32,
                new byte[VirtioBlockDeviceConfig.CapacityLength - 1])
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.ConfigParseFailed
            && result.FailingSourceIndex == 32
            && result.FailingDerivedBindingPlan.Succeeded
            && result.FailingBlockConfiguration.Status == VirtioBlockDeviceConfigStatus.ConfigTooSmall
            && result.FailingInitializationProfile.Status == default;
    }

    private static bool TestDerivedReportsInitializationFailure()
    {
        MacAddress macAddress = new MacAddress(0x02, 0x66, 0x77, 0x88, 0x99, 0xAA);
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 34,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 1)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(34, CreateNetworkConfiguration(macAddress, statusBits: 0))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.InitializationProfileFailed
            && result.FailingSourceIndex == 34
            && result.FailingDeviceKind == VirtioPciDeviceKind.Network
            && result.FailingDerivedBindingPlan.Succeeded
            && result.FailingNetworkConfiguration.Succeeded
            && result.FailingInitializationProfile.Status
                == VirtioDeviceInitializationProfileStatus.InitializationPlanFailed
            && result.FailingInitializationProfile.InitializationPlan.Status
                == VirtioDeviceInitializationPlanStatus.InsufficientQueueCount;
    }

    private static bool TestDerivedReportsDestinationExhaustion()
    {
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 40,
                VirtioPciDeviceDiscovery.BlockDeviceId,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1),
            CreateDerivedSource(
                sourceIndex: 41,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 2)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(40, CreateBlockConfiguration(capacitySectors: 16)),
            new VirtioDeviceConfigurationSource(
                41,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x07, 0x08, 0x09, 0x0A, 0x0B),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.DestinationTooSmall
            && result.InspectedSourceCount == 2
            && result.DescriptorCount == 1
            && result.RequiredDescriptorCapacity == 2
            && result.FailingInputIndex == 1
            && result.FailingSourceIndex == 41
            && result.FailingDeviceKind == VirtioPciDeviceKind.Network
            && result.FailingDerivedBindingPlan.Succeeded
            && destination[0].SourceIndex == 40
            && destination[0].DeviceKind == VirtioPciDeviceKind.Block;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioDeviceDescriptorCatalogResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioDeviceDescriptorCatalogStatus.Success
            && result.InspectedSourceCount == 0
            && result.DescriptorCount == 0
            && result.RequiredDescriptorCapacity == 0
            && result.FailingSourceIndex == 0
            && result.FailingInputIndex == 0
            && result.FailingBindingPlan.Status == default
            && result.FailingDerivedBindingPlan.Status == default
            && result.FailingInitializationProfile.Status == default;
    }

    private static bool TestDerivedPreservesNoLiveDeviceBoundary()
    {
        VirtioDeviceDescriptorDerivedBindingSource[] sources =
        [
            CreateDerivedSource(
                sourceIndex: 44,
                VirtioPciDeviceDiscovery.NetworkDeviceId,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 2)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                44,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x20, 0x21, 0x22, 0x23, 0x24),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);

        return result.Succeeded
            && VirtioDeviceDescriptorCatalog.RequiresCallerProvidedDescriptorSources
            && VirtioDeviceDescriptorCatalog.RequiresCallerProvidedDeviceConfiguration
            && VirtioDeviceDescriptorCatalog.RequiresCallerProvidedFeatureMetadata
            && VirtioDeviceDescriptorCatalog.UsesVirtioDeviceInitializationProfile
            && !VirtioDeviceDescriptorCatalog.PerformsLiveConfigReads
            && !VirtioDeviceDescriptorCatalog.PerformsLiveConfigWrites
            && !VirtioDeviceDescriptorCatalog.MapsBars
            && !VirtioDeviceDescriptorCatalog.PerformsMmio
            && !VirtioDeviceDescriptorCatalog.ContainsVolatileAccess
            && !VirtioDeviceDescriptorCatalog.AllocatesDmaMemory
            && !VirtioDeviceDescriptorCatalog.PerformsDmaMapping
            && !VirtioDeviceDescriptorCatalog.ConfiguresQueues
            && !VirtioDeviceDescriptorCatalog.BindsLiveDriver
            && !VirtioDeviceDescriptorCatalog.HandlesInterrupts
            && !VirtioDeviceDescriptorCatalog.PerformsPacketIo
            && !VirtioDeviceDescriptorCatalog.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioDeviceDescriptorDerivedBindingSource>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioDeviceDescriptorCatalogResult>();
    }

    private static VirtioDeviceDescriptorSource CreateSource(
        int sourceIndex,
        byte virtioDeviceId,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted = true)
    {
        return new VirtioDeviceDescriptorSource(
            sourceIndex,
            CreateBindingPlan(virtioDeviceId),
            deviceFeatures,
            availableQueueCount,
            featuresOkAccepted);
    }

    private static VirtioDeviceDescriptorDerivedBindingSource CreateDerivedSource(
        int sourceIndex,
        byte virtioDeviceId,
        ulong deviceFeatures,
        ushort availableQueueCount,
        bool featuresOkAccepted = true)
    {
        return new VirtioDeviceDescriptorDerivedBindingSource(
            sourceIndex,
            CreateDerivedBindingPlan(virtioDeviceId, CreateSuccessfulAccessor()),
            deviceFeatures,
            availableQueueCount,
            featuresOkAccepted);
    }

    private static VirtioPciBindingDerivedBarSizePlanResult CreateDerivedBindingPlan(
        byte virtioDeviceId,
        IPciConfigDwordAccessor accessor)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength
            : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;
        PciBarSizingProbeAddressCommand[] commands =
            new PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        uint[] readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];
        ulong[] sizes = new ulong[PciBarDecoder.BarCount];

        return VirtioPciBindingDerivedBarSizePlanner.Plan(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId, deviceConfigurationLength),
            accessor,
            commands,
            readbacks,
            sizes);
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan(byte virtioDeviceId)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength
            : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId, deviceConfigurationLength),
            CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 1,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 7,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 2,
                device: 4,
                function: 1,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(
        ushort deviceId,
        uint deviceConfigurationLength)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;

        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            offset: 0x20,
            VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offset: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: 4,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offset: 0,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offset: 0x40,
            deviceConfigurationLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return new ulong[]
        {
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        };
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static byte[] CreateNetworkConfiguration(
        MacAddress macAddress,
        ushort statusBits)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.StatusConfigLength];
        macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength),
            statusBits);
        return config;
    }

    private static FakePciConfigDwordAccessor CreateSuccessfulAccessor()
    {
        FakePciConfigDwordAccessor accessor = new FakePciConfigDwordAccessor();
        accessor.SetReadback(registerOffset: 0x10, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x14, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x18, value: 0xFFFFF000);
        accessor.SetReadback(registerOffset: 0x1C, value: 0xFFFFF000);
        return accessor;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte capabilityOffset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[capabilityOffset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[capabilityOffset + 1] = nextOffset;
        config[capabilityOffset + 2] = capabilityLength;
        config[capabilityOffset + 3] = (byte)type;
        config[capabilityOffset + 4] = bar;
        WriteUInt32(config, capabilityOffset + 8, offset);
        WriteUInt32(config, capabilityOffset + 12, length);

        if (capabilityLength >= VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength)
        {
            WriteUInt32(config, capabilityOffset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private sealed class FakePciConfigDwordAccessor : IPciConfigDwordAccessor
    {
        private readonly uint[] _readbacks = new uint[PciBarSizingProbeExecutor.RequiredReadbackCount];

        public void SetReadback(
            ushort registerOffset,
            uint value)
        {
            byte barIndex = (byte)((registerOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            _readbacks[barIndex] = value;
        }

        public PciConfigDwordAccessStatus Write32(
            PciEcamConfigurationAddressResult address,
            uint value)
        {
            return PciConfigDwordAccessStatus.Success;
        }

        public PciConfigDwordReadResult Read32(
            PciEcamConfigurationAddressResult address)
        {
            byte barIndex = (byte)((address.RegisterOffset - PciBarSizingProbePlanner.FirstBarRegisterOffset) /
                PciBarSizingProbePlanner.BarRegisterStride);
            return new PciConfigDwordReadResult(
                PciConfigDwordAccessStatus.Success,
                _readbacks[barIndex]);
        }
    }
}
