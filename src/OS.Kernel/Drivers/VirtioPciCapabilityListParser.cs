using OS.Std.Binary;

namespace OS.Drivers;

public static class VirtioPciCapabilityListParser
{
    public const byte VendorSpecificCapabilityId = 0x09;
    public const byte ModernVirtioCapabilityLength = 16;
    public const byte ModernVirtioNotifyCapabilityLength = 20;
    public const byte RequiredCapabilityCount = 4;
    private const byte CapabilityHeaderLength = 2;
    private const byte CapabilityLengthOffset = 2;
    private const byte CapabilityTypeOffset = 3;
    private const byte CapabilityBarOffset = 4;
    private const byte CapabilityRegionOffsetOffset = 8;
    private const byte CapabilityRegionLengthOffset = 12;
    private const byte NotifyOffsetMultiplierOffset = 16;
    public static VirtioPciCapabilityListParseResult Parse(ReadOnlySpan<byte> pciConfigurationSpace, byte capabilityStartOffset)
    {
        VirtioPciCapabilityMetadata commonConfiguration = default;
        VirtioPciCapabilityMetadata notifyConfiguration = default;
        VirtioPciCapabilityMetadata isrStatus = default;
        VirtioPciCapabilityMetadata deviceConfiguration = default;
        bool hasCommonConfiguration = false;
        bool hasNotifyConfiguration = false;
        bool hasIsrStatus = false;
        bool hasDeviceConfiguration = false;
        byte parsedVirtioCapabilityCount = 0;
        byte capabilityOffset = capabilityStartOffset;
        ulong visitedOffsets0 = 0;
        ulong visitedOffsets1 = 0;
        ulong visitedOffsets2 = 0;
        ulong visitedOffsets3 = 0;
        while (capabilityOffset != 0)
        {
            if (!IsCapabilityOffsetAligned(capabilityOffset))
            {
                return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.CapabilityOffsetUnaligned, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
            }

            if (!HasBytes(pciConfigurationSpace, capabilityOffset, CapabilityHeaderLength))
            {
                return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.CapabilityHeaderTruncated, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
            }

            if (HasVisited(capabilityOffset, visitedOffsets0, visitedOffsets1, visitedOffsets2, visitedOffsets3))
            {
                return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.CapabilityLoopDetected, capabilityOffset, pciConfigurationSpace[capabilityOffset], parsedVirtioCapabilityCount);
            }

            MarkVisited(capabilityOffset, ref visitedOffsets0, ref visitedOffsets1, ref visitedOffsets2, ref visitedOffsets3);
            byte capabilityId = pciConfigurationSpace[capabilityOffset];
            byte nextCapabilityOffset = pciConfigurationSpace[capabilityOffset + 1];
            if (capabilityId == VendorSpecificCapabilityId)
            {
                VirtioPciCapabilityListParseStatus status = TryParseVirtioCapability(pciConfigurationSpace, capabilityOffset, out VirtioPciCapabilityMetadata capability);
                if (status != VirtioPciCapabilityListParseStatus.Success)
                {
                    return VirtioPciCapabilityListParseResult.Fail(status, capabilityOffset, capabilityId, parsedVirtioCapabilityCount);
                }

                if (TryRecordRequiredCapability(capability, ref commonConfiguration, ref notifyConfiguration, ref isrStatus, ref deviceConfiguration, ref hasCommonConfiguration, ref hasNotifyConfiguration, ref hasIsrStatus, ref hasDeviceConfiguration))
                {
                    parsedVirtioCapabilityCount++;
                }
            }

            capabilityOffset = nextCapabilityOffset;
        }

        if (!hasCommonConfiguration)
        {
            return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.MissingCommonConfigurationCapability, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
        }

        if (!hasNotifyConfiguration)
        {
            return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.MissingNotifyCapability, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
        }

        if (!hasIsrStatus)
        {
            return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.MissingIsrStatusCapability, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
        }

        if (!hasDeviceConfiguration)
        {
            return VirtioPciCapabilityListParseResult.Fail(VirtioPciCapabilityListParseStatus.MissingDeviceConfigurationCapability, capabilityOffset, failingCapabilityId: 0, parsedVirtioCapabilityCount);
        }

        return VirtioPciCapabilityListParseResult.Success(VirtioPciCapabilitySet.Create(commonConfiguration, notifyConfiguration, isrStatus, deviceConfiguration), parsedVirtioCapabilityCount);
    }

    private static VirtioPciCapabilityListParseStatus TryParseVirtioCapability(ReadOnlySpan<byte> pciConfigurationSpace, byte capabilityOffset, out VirtioPciCapabilityMetadata capability)
    {
        capability = default;
        if (!HasBytes(pciConfigurationSpace, capabilityOffset, ModernVirtioCapabilityLength))
        {
            return VirtioPciCapabilityListParseStatus.VendorCapabilityTooShort;
        }

        byte capabilityLength = pciConfigurationSpace[capabilityOffset + CapabilityLengthOffset];
        if (capabilityLength < ModernVirtioCapabilityLength)
        {
            return VirtioPciCapabilityListParseStatus.VendorCapabilityTooShort;
        }

        if (!HasBytes(pciConfigurationSpace, capabilityOffset, capabilityLength))
        {
            return VirtioPciCapabilityListParseStatus.VendorCapabilityRecordOutOfRange;
        }

        byte capabilityTypeValue = pciConfigurationSpace[capabilityOffset + CapabilityTypeOffset];
        if (!IsSupportedVirtioCapabilityType(capabilityTypeValue))
        {
            return VirtioPciCapabilityListParseStatus.UnsupportedVendorCapability;
        }

        VirtioPciCapabilityType capabilityType = (VirtioPciCapabilityType)capabilityTypeValue;
        if (capabilityType == VirtioPciCapabilityType.NotifyConfiguration && capabilityLength < ModernVirtioNotifyCapabilityLength)
        {
            return VirtioPciCapabilityListParseStatus.NotifyCapabilityTooShort;
        }

        byte bar = pciConfigurationSpace[capabilityOffset + CapabilityBarOffset];
        if (bar > VirtioPciDeviceDiscovery.MaximumPciBar)
        {
            return VirtioPciCapabilityListParseStatus.InvalidBar;
        }

        if (!TryReadUInt32LittleEndian(pciConfigurationSpace, capabilityOffset + CapabilityRegionOffsetOffset, out uint regionOffset) || !TryReadUInt32LittleEndian(pciConfigurationSpace, capabilityOffset + CapabilityRegionLengthOffset, out uint regionLength))
        {
            return VirtioPciCapabilityListParseStatus.VendorCapabilityRecordOutOfRange;
        }

        if (regionOffset > uint.MaxValue - regionLength)
        {
            return VirtioPciCapabilityListParseStatus.CapabilityRegionOverflow;
        }

        uint notifyOffsetMultiplier = 0;
        if (capabilityType == VirtioPciCapabilityType.NotifyConfiguration)
        {
            if (!TryReadUInt32LittleEndian(pciConfigurationSpace, capabilityOffset + NotifyOffsetMultiplierOffset, out notifyOffsetMultiplier))
            {
                return VirtioPciCapabilityListParseStatus.VendorCapabilityRecordOutOfRange;
            }
        }

        capability = new VirtioPciCapabilityMetadata(capabilityType, bar, regionOffset, regionLength, notifyOffsetMultiplier);
        return VirtioPciCapabilityListParseStatus.Success;
    }

    private static bool TryRecordRequiredCapability(VirtioPciCapabilityMetadata capability, ref VirtioPciCapabilityMetadata commonConfiguration, ref VirtioPciCapabilityMetadata notifyConfiguration, ref VirtioPciCapabilityMetadata isrStatus, ref VirtioPciCapabilityMetadata deviceConfiguration, ref bool hasCommonConfiguration, ref bool hasNotifyConfiguration, ref bool hasIsrStatus, ref bool hasDeviceConfiguration)
    {
        switch (capability.Type)
        {
            case VirtioPciCapabilityType.CommonConfiguration:
                if (!hasCommonConfiguration)
                {
                    commonConfiguration = capability;
                    hasCommonConfiguration = true;
                    return true;
                }

                return false;
            case VirtioPciCapabilityType.NotifyConfiguration:
                if (!hasNotifyConfiguration)
                {
                    notifyConfiguration = capability;
                    hasNotifyConfiguration = true;
                    return true;
                }

                return false;
            case VirtioPciCapabilityType.IsrStatus:
                if (!hasIsrStatus)
                {
                    isrStatus = capability;
                    hasIsrStatus = true;
                    return true;
                }

                return false;
            case VirtioPciCapabilityType.DeviceConfiguration:
                if (!hasDeviceConfiguration)
                {
                    deviceConfiguration = capability;
                    hasDeviceConfiguration = true;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static bool IsSupportedVirtioCapabilityType(byte capabilityType)
    {
        return capabilityType == (byte)VirtioPciCapabilityType.CommonConfiguration || capabilityType == (byte)VirtioPciCapabilityType.NotifyConfiguration || capabilityType == (byte)VirtioPciCapabilityType.IsrStatus || capabilityType == (byte)VirtioPciCapabilityType.DeviceConfiguration || capabilityType == (byte)VirtioPciCapabilityType.PciConfiguration;
    }

    private static bool IsCapabilityOffsetAligned(byte capabilityOffset)
    {
        return (capabilityOffset & 0x03) == 0;
    }

    private static bool HasBytes(ReadOnlySpan<byte> source, int offset, int requiredLength)
    {
        return offset >= 0 && requiredLength >= 0 && offset <= source.Length && requiredLength <= source.Length - offset;
    }

    private static bool TryReadUInt32LittleEndian(ReadOnlySpan<byte> source, int offset, out uint value)
    {
        if (!HasBytes(source, offset, sizeof(uint)))
        {
            value = 0;
            return false;
        }

        value = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        return true;
    }

    private static bool HasVisited(byte offset, ulong visitedOffsets0, ulong visitedOffsets1, ulong visitedOffsets2, ulong visitedOffsets3)
    {
        ulong mask = 1UL << (offset & 63);
        return (offset >> 6) switch
        {
            0 => (visitedOffsets0 & mask) != 0,
            1 => (visitedOffsets1 & mask) != 0,
            2 => (visitedOffsets2 & mask) != 0,
            _ => (visitedOffsets3 & mask) != 0
        };
    }

    private static void MarkVisited(byte offset, ref ulong visitedOffsets0, ref ulong visitedOffsets1, ref ulong visitedOffsets2, ref ulong visitedOffsets3)
    {
        ulong mask = 1UL << (offset & 63);
        switch (offset >> 6)
        {
            case 0:
                visitedOffsets0 |= mask;
                break;
            case 1:
                visitedOffsets1 |= mask;
                break;
            case 2:
                visitedOffsets2 |= mask;
                break;
            default:
                visitedOffsets3 |= mask;
                break;
        }
    }
}
