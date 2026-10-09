using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioPciModernRegisterMapTests
{

    [HostTest("drivers.virtiopci.registermap common offsets", HostLane.Fast, 0)]
    private static bool TestCommonOffsets()
    {
        return HostCheck.Result(VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureSelectOffset == 0x00
            && VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureSelectWidth == 4
            && VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureOffset == 0x04
            && VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureWidth == 4
            && VirtioPciModernRegisterMap.CommonConfiguration.DriverFeatureSelectOffset == 0x08
            && VirtioPciModernRegisterMap.CommonConfiguration.DriverFeatureSelectWidth == 4
            && VirtioPciModernRegisterMap.CommonConfiguration.DriverFeatureOffset == 0x0C
            && VirtioPciModernRegisterMap.CommonConfiguration.DriverFeatureWidth == 4
            && VirtioPciModernRegisterMap.CommonConfiguration.ConfigMsixVectorOffset == 0x10
            && VirtioPciModernRegisterMap.CommonConfiguration.ConfigMsixVectorWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.NumQueuesOffset == 0x12
            && VirtioPciModernRegisterMap.CommonConfiguration.NumQueuesWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.DeviceStatusOffset == 0x14
            && VirtioPciModernRegisterMap.CommonConfiguration.DeviceStatusWidth == 1
            && VirtioPciModernRegisterMap.CommonConfiguration.ConfigGenerationOffset == 0x15
            && VirtioPciModernRegisterMap.CommonConfiguration.ConfigGenerationWidth == 1
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset == 0x16
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset == 0x18
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueMsixVectorOffset == 0x1A
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueMsixVectorWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableOffset == 0x1C
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueEnableWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset == 0x1E
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffWidth == 2
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset == 0x20
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDescWidth == 8
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverOffset == 0x28
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDriverWidth == 8
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceOffset == 0x30
            && VirtioPciModernRegisterMap.CommonConfiguration.QueueDeviceWidth == 8
            && VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength == 0x38);
    }

    [HostTest("drivers.virtiopci.registermap status bits", HostLane.Fast, 1)]
    private static bool TestStatusBits()
    {
        return HostCheck.Result(VirtioPciModernRegisterMap.DeviceStatus.Acknowledge == 0x01
            && VirtioPciModernRegisterMap.DeviceStatus.Driver == 0x02
            && VirtioPciModernRegisterMap.DeviceStatus.DriverOk == 0x04
            && VirtioPciModernRegisterMap.DeviceStatus.FeaturesOk == 0x08
            && VirtioPciModernRegisterMap.DeviceStatus.DeviceNeedsReset == 0x40
            && VirtioPciModernRegisterMap.DeviceStatus.Failed == 0x80);
    }

    [HostTest("drivers.virtiopci.registermap isr and notify bits", HostLane.Fast, 2)]
    private static bool TestIsrAndNotifyBits()
    {
        return HostCheck.Result(VirtioPciModernRegisterMap.IsrStatus.Offset == 0x00
            && VirtioPciModernRegisterMap.IsrStatus.Width == 1
            && VirtioPciModernRegisterMap.IsrStatus.QueueInterruptBit == 0
            && VirtioPciModernRegisterMap.IsrStatus.QueueInterrupt == 0x01
            && VirtioPciModernRegisterMap.IsrStatus.DeviceConfigurationChangeBit == 1
            && VirtioPciModernRegisterMap.IsrStatus.DeviceConfigurationChange == 0x02
            && VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBits == 16
            && VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes == 2);
    }

    [HostTest("drivers.virtiopci.registermap validates first live common length", HostLane.Fast, 3)]
    private static bool TestValidatesFirstLiveCommonLength()
    {
        uint requiredLength = VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength;
        if (VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength != requiredLength ||
            requiredLength != 0x38)
        {
            return HostCheck.Result(false);
        }

        VirtioPciDeviceDiscoveryResult tooShort = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId),
            CreateNetworkCapabilities(requiredLength - 1));
        if (tooShort.Succeeded ||
            tooShort.Status != VirtioPciDeviceDiscoveryStatus.InvalidCommonConfigurationCapability)
        {
            return HostCheck.Result(false);
        }

        VirtioPciDeviceDiscoveryResult exactLength = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId),
            CreateNetworkCapabilities(requiredLength));

        return HostCheck.Result(exactLength.Succeeded
            && exactLength.Status == VirtioPciDeviceDiscoveryStatus.Success
            && exactLength.Capabilities.CommonConfiguration.Length == requiredLength);
    }

    [HostTest("drivers.virtiopci.registermap net config offsets", HostLane.Fast, 4)]
    private static bool TestNetConfigOffsets()
    {
        return HostCheck.Result(VirtioPciModernRegisterMap.NetDeviceConfiguration.MacAddressOffset == 0x00
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.MacAddressLength == 6
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusOffset == 0x06
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusWidth == 2
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusConfigLength == 8
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.MacAddressOffset
                == VirtioNetDeviceConfig.MacAddressOffset
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.MacAddressLength
                == VirtioNetDeviceConfig.MacAddressLength
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusOffset
                == VirtioNetDeviceConfig.StatusOffset
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusWidth
                == VirtioNetDeviceConfig.StatusLength
            && VirtioPciModernRegisterMap.NetDeviceConfiguration.StatusConfigLength
                == VirtioNetDeviceConfig.StatusConfigLength);
    }

    private static VirtioPciCapabilityMetadata[] CreateNetworkCapabilities(uint commonConfigurationLength)
    {
        return new VirtioPciCapabilityMetadata[]
        {
            new VirtioPciCapabilityMetadata(
                VirtioPciCapabilityType.CommonConfiguration,
                bar: 0,
                offset: 0x1000,
                length: commonConfigurationLength,
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
}
