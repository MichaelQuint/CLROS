namespace OS.Drivers;
public static class VirtioPciBindingPlan
{
    public static VirtioPciBindingPlanResult Create(PciEcamScannedDevice device, ReadOnlySpan<byte> pciConfigurationSpace, ReadOnlySpan<ulong> barSizes)
    {
        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(pciConfigurationSpace);
        if (!headerResult.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.HeaderParseFailed, headerResult.Status);
        }

        PciType0ConfigurationHeader header = headerResult.Header;
        VirtioPciCapabilityListParseResult capabilityParse = VirtioPciCapabilityListParser.Parse(pciConfigurationSpace, header.CapabilityPointer);
        if (!capabilityParse.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.CapabilityParseFailed, capabilityParse: capabilityParse);
        }

        Span<VirtioPciCapabilityMetadata> capabilities = stackalloc VirtioPciCapabilityMetadata[VirtioPciCapabilityListParser.RequiredCapabilityCount];
        if (!capabilityParse.TryCopyRequiredCapabilities(capabilities))
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.CapabilityParseFailed, capabilityParse: capabilityParse);
        }

        VirtioPciDeviceDiscoveryResult discovery = VirtioPciDeviceDiscovery.Discover(new PciDeviceIdentity(header.VendorId, header.DeviceId), capabilities);
        if (!discovery.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.DiscoveryFailed, capabilityParse: capabilityParse, discovery: discovery);
        }

        PciBarDecodeResult barDecode = PciBarDecoder.Decode(header);
        if (!barDecode.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.BarDecodeFailed, capabilityParse: capabilityParse, discovery: discovery, barDecode: barDecode);
        }

        VirtioPciBarApertureValidationResult apertureValidation = VirtioPciBarApertureValidator.Validate(discovery, barDecode.Bars, barSizes);
        if (!apertureValidation.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(VirtioPciBindingPlanStatus.ApertureValidationFailed, capabilityParse: capabilityParse, discovery: discovery, barDecode: barDecode, apertureValidation: apertureValidation);
        }

        VirtioPciBindingMetadata binding = new VirtioPciBindingMetadata(device.SegmentIndex, device.Segment, device.Bus, device.DeviceNumber, device.Function, header.CapabilityPointer, discovery.Identity, discovery.DeviceKind, discovery.VirtioDeviceId, discovery.Capabilities, barDecode.Bars, apertureValidation.Apertures);
        return VirtioPciBindingPlanResult.Success(capabilityParse, discovery, barDecode, apertureValidation, binding);
    }
}
