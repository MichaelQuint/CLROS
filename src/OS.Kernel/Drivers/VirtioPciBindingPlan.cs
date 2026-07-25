namespace OS.Drivers;

public enum VirtioPciBindingPlanStatus
{
    Success = 0,
    HeaderParseFailed = 1,
    CapabilityParseFailed = 2,
    DiscoveryFailed = 3,
    BarDecodeFailed = 4,
    ApertureValidationFailed = 5
}

public readonly struct VirtioPciBindingMetadata
{
    internal VirtioPciBindingMetadata(
        int segmentIndex,
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        byte capabilityPointer,
        PciDeviceIdentity identity,
        VirtioPciDeviceKind deviceKind,
        byte virtioDeviceId,
        VirtioPciCapabilitySet capabilities,
        PciBarDescriptorSet bars,
        VirtioPciBarApertureSet apertures)
    {
        SegmentIndex = segmentIndex;
        Segment = segment;
        Bus = bus;
        Device = device;
        Function = function;
        CapabilityPointer = capabilityPointer;
        Identity = identity;
        DeviceKind = deviceKind;
        VirtioDeviceId = virtioDeviceId;
        Capabilities = capabilities;
        Bars = bars;
        Apertures = apertures;
    }

    public int SegmentIndex { get; }

    public PciEcamSegment Segment { get; }

    public ulong EcamBasePhysicalAddress => Segment.BasePhysicalAddress;

    public ushort SegmentGroup => Segment.SegmentGroup;

    public byte SegmentStartBus => Segment.StartBus;

    public byte SegmentEndBus => Segment.EndBus;

    public byte Bus { get; }

    public byte Device { get; }

    public byte Function { get; }

    public byte CapabilityPointer { get; }

    public PciDeviceIdentity Identity { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public byte VirtioDeviceId { get; }

    public VirtioPciCapabilitySet Capabilities { get; }

    public PciBarDescriptorSet Bars { get; }

    public VirtioPciBarApertureSet Apertures { get; }
}

public readonly struct VirtioPciBindingPlanResult
{
    private VirtioPciBindingPlanResult(
        VirtioPciBindingPlanStatus status,
        PciConfigurationHeaderStatus headerStatus,
        VirtioPciCapabilityListParseResult capabilityParse,
        VirtioPciDeviceDiscoveryResult discovery,
        PciBarDecodeResult barDecode,
        VirtioPciBarApertureValidationResult apertureValidation,
        VirtioPciBindingMetadata binding,
        bool isInitialized)
    {
        Status = status;
        HeaderStatus = headerStatus;
        CapabilityParse = capabilityParse;
        Discovery = discovery;
        BarDecode = barDecode;
        ApertureValidation = apertureValidation;
        Binding = binding;
        IsInitialized = isInitialized;
    }

    public VirtioPciBindingPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public PciConfigurationHeaderStatus HeaderStatus { get; }

    public VirtioPciCapabilityListParseResult CapabilityParse { get; }

    public VirtioPciDeviceDiscoveryResult Discovery { get; }

    public PciBarDecodeResult BarDecode { get; }

    public VirtioPciBarApertureValidationResult ApertureValidation { get; }

    public VirtioPciBindingMetadata Binding { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciBindingPlanStatus.Success;

    public static VirtioPciBindingPlanResult Success(
        VirtioPciCapabilityListParseResult capabilityParse,
        VirtioPciDeviceDiscoveryResult discovery,
        PciBarDecodeResult barDecode,
        VirtioPciBarApertureValidationResult apertureValidation,
        VirtioPciBindingMetadata binding)
    {
        return new VirtioPciBindingPlanResult(
            VirtioPciBindingPlanStatus.Success,
            PciConfigurationHeaderStatus.Success,
            capabilityParse,
            discovery,
            barDecode,
            apertureValidation,
            binding,
            isInitialized: true);
    }

    public static VirtioPciBindingPlanResult Fail(
        VirtioPciBindingPlanStatus status,
        PciConfigurationHeaderStatus headerStatus = PciConfigurationHeaderStatus.Success,
        VirtioPciCapabilityListParseResult capabilityParse = default,
        VirtioPciDeviceDiscoveryResult discovery = default,
        PciBarDecodeResult barDecode = default,
        VirtioPciBarApertureValidationResult apertureValidation = default)
    {
        return new VirtioPciBindingPlanResult(
            status,
            headerStatus,
            capabilityParse,
            discovery,
            barDecode,
            apertureValidation,
            default,
            isInitialized: true);
    }
}

public static class VirtioPciBindingPlan
{
    public static VirtioPciBindingPlanResult Create(
        PciEcamScannedDevice device,
        ReadOnlySpan<byte> pciConfigurationSpace,
        ReadOnlySpan<ulong> barSizes)
    {
        PciConfigurationHeaderResult headerResult =
            PciConfigurationHeader.ParseType0(pciConfigurationSpace);
        if (!headerResult.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.HeaderParseFailed,
                headerResult.Status);
        }

        PciType0ConfigurationHeader header = headerResult.Header;
        VirtioPciCapabilityListParseResult capabilityParse =
            VirtioPciCapabilityListParser.Parse(
                pciConfigurationSpace,
                header.CapabilityPointer);
        if (!capabilityParse.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.CapabilityParseFailed,
                capabilityParse: capabilityParse);
        }

        Span<VirtioPciCapabilityMetadata> capabilities =
            stackalloc VirtioPciCapabilityMetadata[VirtioPciCapabilityListParser.RequiredCapabilityCount];
        if (!capabilityParse.TryCopyRequiredCapabilities(capabilities))
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.CapabilityParseFailed,
                capabilityParse: capabilityParse);
        }

        VirtioPciDeviceDiscoveryResult discovery =
            VirtioPciDeviceDiscovery.Discover(
                new PciDeviceIdentity(header.VendorId, header.DeviceId),
                capabilities);
        if (!discovery.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.DiscoveryFailed,
                capabilityParse: capabilityParse,
                discovery: discovery);
        }

        PciBarDecodeResult barDecode = PciBarDecoder.Decode(header);
        if (!barDecode.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.BarDecodeFailed,
                capabilityParse: capabilityParse,
                discovery: discovery,
                barDecode: barDecode);
        }

        VirtioPciBarApertureValidationResult apertureValidation =
            VirtioPciBarApertureValidator.Validate(
                discovery,
                barDecode.Bars,
                barSizes);
        if (!apertureValidation.Succeeded)
        {
            return VirtioPciBindingPlanResult.Fail(
                VirtioPciBindingPlanStatus.ApertureValidationFailed,
                capabilityParse: capabilityParse,
                discovery: discovery,
                barDecode: barDecode,
                apertureValidation: apertureValidation);
        }

        VirtioPciBindingMetadata binding = new VirtioPciBindingMetadata(
            device.SegmentIndex,
            device.Segment,
            device.Bus,
            device.DeviceNumber,
            device.Function,
            header.CapabilityPointer,
            discovery.Identity,
            discovery.DeviceKind,
            discovery.VirtioDeviceId,
            discovery.Capabilities,
            barDecode.Bars,
            apertureValidation.Apertures);

        return VirtioPciBindingPlanResult.Success(
            capabilityParse,
            discovery,
            barDecode,
            apertureValidation,
            binding);
    }
}
