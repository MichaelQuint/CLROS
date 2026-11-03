namespace OS.Drivers;

public static class VirtioPciBarApertureValidator
{
    public const byte RequiredBarSizeCount = PciBarDecoder.BarCount;
    public static VirtioPciBarApertureValidationResult Validate(VirtioPciDeviceDiscoveryResult discovery, PciBarDescriptorSet bars, ReadOnlySpan<ulong> barSizes)
    {
        if (!discovery.Succeeded)
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.DiscoveryFailed, discovery.Status, default, PciBarDecoder.NoBarIndex);
        }

        VirtioPciCapabilitySet capabilities = discovery.Capabilities;
        if (!capabilities.HasCommonConfiguration || !capabilities.HasNotifyConfiguration || !capabilities.HasIsrStatus || !capabilities.HasDeviceConfiguration)
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.MissingRequiredCapability, discovery.Status, default, PciBarDecoder.NoBarIndex);
        }

        VirtioPciBarApertureValidationResult commonResult = ValidateCapability(capabilities.CommonConfiguration, bars, barSizes, out VirtioPciBarAperture commonConfiguration);
        if (!commonResult.Succeeded)
        {
            return commonResult;
        }

        VirtioPciBarApertureValidationResult notifyResult = ValidateCapability(capabilities.NotifyConfiguration, bars, barSizes, out VirtioPciBarAperture notifyConfiguration);
        if (!notifyResult.Succeeded)
        {
            return notifyResult;
        }

        VirtioPciBarApertureValidationResult isrResult = ValidateCapability(capabilities.IsrStatus, bars, barSizes, out VirtioPciBarAperture isrStatus);
        if (!isrResult.Succeeded)
        {
            return isrResult;
        }

        VirtioPciBarApertureValidationResult deviceResult = ValidateCapability(capabilities.DeviceConfiguration, bars, barSizes, out VirtioPciBarAperture deviceConfiguration);
        if (!deviceResult.Succeeded)
        {
            return deviceResult;
        }

        return VirtioPciBarApertureValidationResult.Success(new VirtioPciBarApertureSet(commonConfiguration, notifyConfiguration, isrStatus, deviceConfiguration));
    }

    private static VirtioPciBarApertureValidationResult ValidateCapability(VirtioPciCapabilityMetadata capability, PciBarDescriptorSet bars, ReadOnlySpan<ulong> barSizes, out VirtioPciBarAperture aperture)
    {
        aperture = default;
        if (!bars.TryGet(capability.Bar, out PciBarDescriptor bar))
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarIndexOutOfRange, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar);
        }

        switch (bar.Kind)
        {
            case PciBarKind.Memory32:
            case PciBarKind.Memory64:
                break;
            case PciBarKind.Absent:
                return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarAbsent, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
            case PciBarKind.Io:
                return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarIsIo, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
            case PciBarKind.ConsumedByPrevious64BitBar:
                return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarConsumedByPrevious64BitBar, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
            default:
                return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarMalformed, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
        }

        if (capability.Bar >= barSizes.Length)
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.BarSizeUnavailable, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
        }

        if (capability.Offset > uint.MaxValue - capability.Length)
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.CapabilityRegionOverflow, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind);
        }

        ulong regionEndOffset = (ulong)capability.Offset + capability.Length;
        ulong barSize = barSizes[capability.Bar];
        if (regionEndOffset > barSize)
        {
            return VirtioPciBarApertureValidationResult.Fail(VirtioPciBarApertureValidationStatus.CapabilityRegionBeyondBarSize, VirtioPciDeviceDiscoveryStatus.Success, capability.Type, capability.Bar, bar.Kind, barSize, regionEndOffset);
        }

        aperture = new VirtioPciBarAperture(capability.Type, capability.Bar, bar.Kind, bar.BaseAddressBits, capability.Offset, capability.Length, regionEndOffset, barSize, capability.NotifyOffsetMultiplier);
        return VirtioPciBarApertureValidationResult.Success(default);
    }
}
