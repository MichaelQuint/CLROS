namespace OS.Drivers;
public static class VirtioPciDeviceDiscovery
{
    public const ushort VirtioVendorId = 0x1AF4;
    public const ushort ModernDeviceIdStart = 0x1040;
    public const ushort ModernDeviceIdEnd = 0x107F;
    public const byte NetworkDeviceId = 1;
    public const byte BlockDeviceId = 2;
    public const byte MaximumPciBar = 5;
    public const uint CommonConfigurationMinimumLength = VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength;
    public const uint NotifyConfigurationMinimumLength = 2;
    public const uint IsrStatusMinimumLength = 1;
    public const uint NetworkDeviceConfigurationMinimumLength = 6;
    public const uint BlockDeviceConfigurationMinimumLength = 8;
    public static VirtioPciDeviceDiscoveryResult Discover(PciDeviceIdentity identity, ReadOnlySpan<VirtioPciCapabilityMetadata> capabilities)
    {
        if (identity.VendorId != VirtioVendorId)
        {
            return VirtioPciDeviceDiscoveryResult.Fail(VirtioPciDeviceDiscoveryStatus.UnsupportedVendor, identity, virtioDeviceId: 0, VirtioPciDeviceKind.Unknown);
        }

        if (identity.DeviceId < ModernDeviceIdStart || identity.DeviceId > ModernDeviceIdEnd)
        {
            return VirtioPciDeviceDiscoveryResult.Fail(VirtioPciDeviceDiscoveryStatus.UnsupportedDeviceId, identity, virtioDeviceId: 0, VirtioPciDeviceKind.Unknown);
        }

        byte virtioDeviceId = (byte)(identity.DeviceId - ModernDeviceIdStart);
        VirtioPciDeviceKind deviceKind = GetDeviceKind(virtioDeviceId);
        if (deviceKind == VirtioPciDeviceKind.Unknown)
        {
            return VirtioPciDeviceDiscoveryResult.Fail(VirtioPciDeviceDiscoveryStatus.UnsupportedDeviceKind, identity, virtioDeviceId, deviceKind);
        }

        VirtioPciDeviceDiscoveryStatus capabilityStatus = TryCollectRequiredCapabilities(capabilities, deviceKind, out VirtioPciCapabilitySet requiredCapabilities);
        if (capabilityStatus != VirtioPciDeviceDiscoveryStatus.Success)
        {
            return VirtioPciDeviceDiscoveryResult.Fail(capabilityStatus, identity, virtioDeviceId, deviceKind);
        }

        return VirtioPciDeviceDiscoveryResult.Success(identity, deviceKind, virtioDeviceId, requiredCapabilities);
    }

    private static VirtioPciDeviceKind GetDeviceKind(byte virtioDeviceId)
    {
        if (virtioDeviceId == NetworkDeviceId)
        {
            return VirtioPciDeviceKind.Network;
        }

        if (virtioDeviceId == BlockDeviceId)
        {
            return VirtioPciDeviceKind.Block;
        }

        return VirtioPciDeviceKind.Unknown;
    }

    private static VirtioPciDeviceDiscoveryStatus TryCollectRequiredCapabilities(ReadOnlySpan<VirtioPciCapabilityMetadata> capabilities, VirtioPciDeviceKind deviceKind, out VirtioPciCapabilitySet requiredCapabilities)
    {
        VirtioPciCapabilityMetadata commonConfiguration = default;
        VirtioPciCapabilityMetadata notifyConfiguration = default;
        VirtioPciCapabilityMetadata isrStatus = default;
        VirtioPciCapabilityMetadata deviceConfiguration = default;
        bool hasCommonConfiguration = false;
        bool hasNotifyConfiguration = false;
        bool hasIsrStatus = false;
        bool hasDeviceConfiguration = false;
        for (int index = 0; index < capabilities.Length; index++)
        {
            VirtioPciCapabilityMetadata capability = capabilities[index];
            switch (capability.Type)
            {
                case VirtioPciCapabilityType.CommonConfiguration:
                    if (!IsCapabilityRegionValid(capability, CommonConfigurationMinimumLength))
                    {
                        requiredCapabilities = default;
                        return VirtioPciDeviceDiscoveryStatus.InvalidCommonConfigurationCapability;
                    }

                    if (!hasCommonConfiguration)
                    {
                        commonConfiguration = capability;
                        hasCommonConfiguration = true;
                    }

                    break;
                case VirtioPciCapabilityType.NotifyConfiguration:
                    if (!IsCapabilityRegionValid(capability, NotifyConfigurationMinimumLength))
                    {
                        requiredCapabilities = default;
                        return VirtioPciDeviceDiscoveryStatus.InvalidNotifyCapability;
                    }

                    if (!hasNotifyConfiguration)
                    {
                        notifyConfiguration = capability;
                        hasNotifyConfiguration = true;
                    }

                    break;
                case VirtioPciCapabilityType.IsrStatus:
                    if (!IsCapabilityRegionValid(capability, IsrStatusMinimumLength))
                    {
                        requiredCapabilities = default;
                        return VirtioPciDeviceDiscoveryStatus.InvalidIsrStatusCapability;
                    }

                    if (!hasIsrStatus)
                    {
                        isrStatus = capability;
                        hasIsrStatus = true;
                    }

                    break;
                case VirtioPciCapabilityType.DeviceConfiguration:
                    if (!IsCapabilityRegionValid(capability, GetDeviceConfigurationMinimumLength(deviceKind)))
                    {
                        requiredCapabilities = default;
                        return VirtioPciDeviceDiscoveryStatus.InvalidDeviceConfigurationCapability;
                    }

                    if (!hasDeviceConfiguration)
                    {
                        deviceConfiguration = capability;
                        hasDeviceConfiguration = true;
                    }

                    break;
            }
        }

        if (!hasCommonConfiguration)
        {
            requiredCapabilities = default;
            return VirtioPciDeviceDiscoveryStatus.MissingCommonConfigurationCapability;
        }

        if (!hasNotifyConfiguration)
        {
            requiredCapabilities = default;
            return VirtioPciDeviceDiscoveryStatus.MissingNotifyCapability;
        }

        if (!hasIsrStatus)
        {
            requiredCapabilities = default;
            return VirtioPciDeviceDiscoveryStatus.MissingIsrStatusCapability;
        }

        if (!hasDeviceConfiguration)
        {
            requiredCapabilities = default;
            return VirtioPciDeviceDiscoveryStatus.MissingDeviceConfigurationCapability;
        }

        requiredCapabilities = VirtioPciCapabilitySet.Create(commonConfiguration, notifyConfiguration, isrStatus, deviceConfiguration);
        return VirtioPciDeviceDiscoveryStatus.Success;
    }

    private static uint GetDeviceConfigurationMinimumLength(VirtioPciDeviceKind deviceKind)
    {
        return deviceKind == VirtioPciDeviceKind.Block ? BlockDeviceConfigurationMinimumLength : NetworkDeviceConfigurationMinimumLength;
    }

    private static bool IsCapabilityRegionValid(VirtioPciCapabilityMetadata capability, uint minimumLength)
    {
        if (capability.Bar > MaximumPciBar)
        {
            return false;
        }

        if (capability.Length < minimumLength)
        {
            return false;
        }

        return capability.Offset <= uint.MaxValue - capability.Length;
    }
}
