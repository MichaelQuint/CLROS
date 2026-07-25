using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioPciDeviceDiscoveryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtiopci identifies virtio block device", TestIdentifiesVirtioBlockDevice, ref failures);
        Run("drivers.virtiopci identifies virtio net device", TestIdentifiesVirtioNetDevice, ref failures);
        Run("drivers.virtiopci rejects unsupported device", TestRejectsUnsupportedDevice, ref failures);
        Run("drivers.virtiopci records required capabilities", TestRecordsRequiredCapabilities, ref failures);
        Run("drivers.virtiopci default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio PCI discovery host tests failed: " + failures);
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

    private static bool TestIdentifiesVirtioBlockDevice()
    {
        VirtioPciDeviceDiscoveryResult result = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.BlockDeviceId),
            CreateBlockCapabilities());

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.Identity.VendorId == VirtioPciDeviceDiscovery.VirtioVendorId
            && result.Identity.DeviceId == 0x1042
            && result.VirtioDeviceId == VirtioPciDeviceDiscovery.BlockDeviceId
            && result.DeviceKind == VirtioPciDeviceKind.Block
            && result.Capabilities.HasCommonConfiguration
            && result.Capabilities.HasNotifyConfiguration
            && result.Capabilities.HasIsrStatus
            && result.Capabilities.HasDeviceConfiguration
            && result.Capabilities.DeviceConfiguration.Length
                == VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;
    }

    private static bool TestIdentifiesVirtioNetDevice()
    {
        VirtioPciDeviceDiscoveryResult result = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId),
            CreateNetworkCapabilities());

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.Identity.DeviceId == 0x1041
            && result.VirtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            && result.DeviceKind == VirtioPciDeviceKind.Network
            && result.Capabilities.DeviceConfiguration.Length
                == VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength;
    }

    private static bool TestRejectsUnsupportedDevice()
    {
        VirtioPciDeviceDiscoveryResult wrongVendor = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(0x1234, 0x1042),
            CreateBlockCapabilities());
        if (wrongVendor.Succeeded ||
            !wrongVendor.IsInitialized ||
            wrongVendor.Status != VirtioPciDeviceDiscoveryStatus.UnsupportedVendor)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult legacyDevice = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(VirtioPciDeviceDiscovery.VirtioVendorId, 0x1001),
            CreateBlockCapabilities());
        if (legacyDevice.Succeeded ||
            !legacyDevice.IsInitialized ||
            legacyDevice.Status != VirtioPciDeviceDiscoveryStatus.UnsupportedDeviceId)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult unsupportedModernDevice = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(VirtioPciDeviceDiscovery.VirtioVendorId, 0x1045),
            CreateBlockCapabilities());

        return !unsupportedModernDevice.Succeeded
            && unsupportedModernDevice.IsInitialized
            && unsupportedModernDevice.Status == VirtioPciDeviceDiscoveryStatus.UnsupportedDeviceKind
            && unsupportedModernDevice.VirtioDeviceId == 5
            && unsupportedModernDevice.DeviceKind == VirtioPciDeviceKind.Unknown;
    }

    private static bool TestRecordsRequiredCapabilities()
    {
        VirtioPciDeviceDiscoveryResult result = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.BlockDeviceId),
            CreateBlockCapabilities());
        if (!result.Succeeded)
        {
            return false;
        }

        VirtioPciCapabilitySet capabilities = result.Capabilities;
        if (!capabilities.HasCommonConfiguration ||
            !capabilities.HasNotifyConfiguration ||
            !capabilities.HasIsrStatus ||
            !capabilities.HasDeviceConfiguration ||
            capabilities.CommonConfiguration.Bar != 0 ||
            capabilities.CommonConfiguration.Offset != 0x1000 ||
            capabilities.CommonConfiguration.Length != VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength ||
            capabilities.NotifyConfiguration.Bar != 1 ||
            capabilities.NotifyConfiguration.Offset != 0x2000 ||
            capabilities.NotifyConfiguration.Length != 0x100 ||
            capabilities.NotifyConfiguration.NotifyOffsetMultiplier != 4 ||
            capabilities.IsrStatus.Bar != 2 ||
            capabilities.IsrStatus.Offset != 0x3000 ||
            capabilities.IsrStatus.Length != VirtioPciDeviceDiscovery.IsrStatusMinimumLength ||
            capabilities.DeviceConfiguration.Bar != 3 ||
            capabilities.DeviceConfiguration.Offset != 0x4000 ||
            capabilities.DeviceConfiguration.Length != VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult missingDeviceConfiguration = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042),
            CreateCapabilitiesWithoutDeviceConfiguration());
        if (missingDeviceConfiguration.Succeeded ||
            missingDeviceConfiguration.Status
                != VirtioPciDeviceDiscoveryStatus.MissingDeviceConfigurationCapability)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult invalidNotify = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(VirtioPciDeviceDiscovery.VirtioVendorId, 0x1042),
            CreateCapabilitiesWithInvalidNotifyLength());

        return !invalidNotify.Succeeded
            && invalidNotify.IsInitialized
            && invalidNotify.Status == VirtioPciDeviceDiscoveryStatus.InvalidNotifyCapability;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciDeviceDiscoveryResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciDeviceDiscoveryStatus.Success
            && result.DeviceKind == VirtioPciDeviceKind.Unknown
            && !result.Capabilities.HasCommonConfiguration
            && !result.Capabilities.HasNotifyConfiguration
            && !result.Capabilities.HasIsrStatus
            && !result.Capabilities.HasDeviceConfiguration
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioPciDeviceDiscoveryResult>();
    }

    private static VirtioPciCapabilityMetadata[] CreateBlockCapabilities()
    {
        return new VirtioPciCapabilityMetadata[]
        {
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.CommonConfiguration,
                bar: 0,
                offset: 0x1000,
                length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.NotifyConfiguration,
                bar: 1,
                offset: 0x2000,
                length: 0x100,
                notifyOffsetMultiplier: 4),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.IsrStatus,
                bar: 2,
                offset: 0x3000,
                length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.DeviceConfiguration,
                bar: 3,
                offset: 0x4000,
                length: VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
                notifyOffsetMultiplier: 0)
        };
    }

    private static VirtioPciCapabilityMetadata[] CreateNetworkCapabilities()
    {
        return new VirtioPciCapabilityMetadata[]
        {
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.CommonConfiguration,
                bar: 0,
                offset: 0x1000,
                length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.NotifyConfiguration,
                bar: 1,
                offset: 0x2000,
                length: 0x100,
                notifyOffsetMultiplier: 4),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.IsrStatus,
                bar: 2,
                offset: 0x3000,
                length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.DeviceConfiguration,
                bar: 3,
                offset: 0x4000,
                length: VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength,
                notifyOffsetMultiplier: 0)
        };
    }

    private static VirtioPciCapabilityMetadata[] CreateCapabilitiesWithoutDeviceConfiguration()
    {
        return new VirtioPciCapabilityMetadata[]
        {
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.CommonConfiguration,
                bar: 0,
                offset: 0x1000,
                length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.NotifyConfiguration,
                bar: 1,
                offset: 0x2000,
                length: 0x100,
                notifyOffsetMultiplier: 4),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.IsrStatus,
                bar: 2,
                offset: 0x3000,
                length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
                notifyOffsetMultiplier: 0)
        };
    }

    private static VirtioPciCapabilityMetadata[] CreateCapabilitiesWithInvalidNotifyLength()
    {
        return new VirtioPciCapabilityMetadata[]
        {
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.CommonConfiguration,
                bar: 0,
                offset: 0x1000,
                length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.NotifyConfiguration,
                bar: 1,
                offset: 0x2000,
                length: 1,
                notifyOffsetMultiplier: 4),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.IsrStatus,
                bar: 2,
                offset: 0x3000,
                length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
                notifyOffsetMultiplier: 0),
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.DeviceConfiguration,
                bar: 3,
                offset: 0x4000,
                length: VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
                notifyOffsetMultiplier: 0)
        };
    }
}
