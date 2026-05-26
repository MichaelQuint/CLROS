namespace OS.Drivers;

public enum VirtioPciBarApertureValidationStatus
{
    Success = 0,
    DiscoveryFailed = 1,
    MissingRequiredCapability = 2,
    BarIndexOutOfRange = 3,
    BarSizeUnavailable = 4,
    BarAbsent = 5,
    BarIsIo = 6,
    BarConsumedByPrevious64BitBar = 7,
    BarMalformed = 8,
    CapabilityRegionOverflow = 9,
    CapabilityRegionBeyondBarSize = 10
}

public readonly struct VirtioPciBarAperture
{
    internal VirtioPciBarAperture(
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        PciBarKind barKind,
        ulong barBaseAddressBits,
        uint offset,
        uint length,
        ulong endOffset,
        ulong barSize,
        uint notifyOffsetMultiplier)
    {
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        BarKind = barKind;
        BarBaseAddressBits = barBaseAddressBits;
        Offset = offset;
        Length = length;
        EndOffset = endOffset;
        BarSize = barSize;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
    }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public PciBarKind BarKind { get; }

    public ulong BarBaseAddressBits { get; }

    public uint Offset { get; }

    public uint Length { get; }

    public ulong EndOffset { get; }

    public ulong BarSize { get; }

    public uint NotifyOffsetMultiplier { get; }
}

public readonly struct VirtioPciBarApertureSet
{
    internal VirtioPciBarApertureSet(
        VirtioPciBarAperture commonConfiguration,
        VirtioPciBarAperture notifyConfiguration,
        VirtioPciBarAperture isrStatus,
        VirtioPciBarAperture deviceConfiguration)
    {
        CommonConfiguration = commonConfiguration;
        NotifyConfiguration = notifyConfiguration;
        IsrStatus = isrStatus;
        DeviceConfiguration = deviceConfiguration;
    }

    public VirtioPciBarAperture CommonConfiguration { get; }

    public VirtioPciBarAperture NotifyConfiguration { get; }

    public VirtioPciBarAperture IsrStatus { get; }

    public VirtioPciBarAperture DeviceConfiguration { get; }
}

public readonly struct VirtioPciBarApertureValidationResult
{
    private VirtioPciBarApertureValidationResult(
        VirtioPciBarApertureValidationStatus status,
        VirtioPciDeviceDiscoveryStatus discoveryStatus,
        VirtioPciCapabilityType failingCapabilityType,
        byte failingBarIndex,
        PciBarKind failingBarKind,
        ulong failingBarSize,
        ulong failingRegionEndOffset,
        VirtioPciBarApertureSet apertures,
        bool isInitialized)
    {
        Status = status;
        DiscoveryStatus = discoveryStatus;
        FailingCapabilityType = failingCapabilityType;
        FailingBarIndex = failingBarIndex;
        FailingBarKind = failingBarKind;
        FailingBarSize = failingBarSize;
        FailingRegionEndOffset = failingRegionEndOffset;
        Apertures = apertures;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarApertureValidationStatus Status { get; }

    public VirtioPciDeviceDiscoveryStatus DiscoveryStatus { get; }

    public VirtioPciCapabilityType FailingCapabilityType { get; }

    public byte FailingBarIndex { get; }

    public PciBarKind FailingBarKind { get; }

    public ulong FailingBarSize { get; }

    public ulong FailingRegionEndOffset { get; }

    public VirtioPciBarApertureSet Apertures { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciBarApertureValidationStatus.Success;

    public static VirtioPciBarApertureValidationResult Success(
        VirtioPciBarApertureSet apertures)
    {
        return new VirtioPciBarApertureValidationResult(
            VirtioPciBarApertureValidationStatus.Success,
            VirtioPciDeviceDiscoveryStatus.Success,
            default,
            PciBarDecoder.NoBarIndex,
            default,
            failingBarSize: 0,
            failingRegionEndOffset: 0,
            apertures,
            isInitialized: true);
    }

    public static VirtioPciBarApertureValidationResult Fail(
        VirtioPciBarApertureValidationStatus status,
        VirtioPciDeviceDiscoveryStatus discoveryStatus,
        VirtioPciCapabilityType failingCapabilityType,
        byte failingBarIndex,
        PciBarKind failingBarKind = default,
        ulong failingBarSize = 0,
        ulong failingRegionEndOffset = 0)
    {
        return new VirtioPciBarApertureValidationResult(
            status,
            discoveryStatus,
            failingCapabilityType,
            failingBarIndex,
            failingBarKind,
            failingBarSize,
            failingRegionEndOffset,
            default,
            isInitialized: true);
    }
}

public static class VirtioPciBarApertureValidator
{
    public const byte RequiredBarSizeCount = PciBarDecoder.BarCount;

    public static VirtioPciBarApertureValidationResult Validate(
        VirtioPciDeviceDiscoveryResult discovery,
        PciBarDescriptorSet bars,
        ReadOnlySpan<ulong> barSizes)
    {
        if (!discovery.Succeeded)
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.DiscoveryFailed,
                discovery.Status,
                default,
                PciBarDecoder.NoBarIndex);
        }

        VirtioPciCapabilitySet capabilities = discovery.Capabilities;
        if (!capabilities.HasCommonConfiguration ||
            !capabilities.HasNotifyConfiguration ||
            !capabilities.HasIsrStatus ||
            !capabilities.HasDeviceConfiguration)
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.MissingRequiredCapability,
                discovery.Status,
                default,
                PciBarDecoder.NoBarIndex);
        }

        VirtioPciBarApertureValidationResult commonResult = ValidateCapability(
            capabilities.CommonConfiguration,
            bars,
            barSizes,
            out VirtioPciBarAperture commonConfiguration);
        if (!commonResult.Succeeded)
        {
            return commonResult;
        }

        VirtioPciBarApertureValidationResult notifyResult = ValidateCapability(
            capabilities.NotifyConfiguration,
            bars,
            barSizes,
            out VirtioPciBarAperture notifyConfiguration);
        if (!notifyResult.Succeeded)
        {
            return notifyResult;
        }

        VirtioPciBarApertureValidationResult isrResult = ValidateCapability(
            capabilities.IsrStatus,
            bars,
            barSizes,
            out VirtioPciBarAperture isrStatus);
        if (!isrResult.Succeeded)
        {
            return isrResult;
        }

        VirtioPciBarApertureValidationResult deviceResult = ValidateCapability(
            capabilities.DeviceConfiguration,
            bars,
            barSizes,
            out VirtioPciBarAperture deviceConfiguration);
        if (!deviceResult.Succeeded)
        {
            return deviceResult;
        }

        return VirtioPciBarApertureValidationResult.Success(
            new VirtioPciBarApertureSet(
                commonConfiguration,
                notifyConfiguration,
                isrStatus,
                deviceConfiguration));
    }

    private static VirtioPciBarApertureValidationResult ValidateCapability(
        VirtioPciCapabilityMetadata capability,
        PciBarDescriptorSet bars,
        ReadOnlySpan<ulong> barSizes,
        out VirtioPciBarAperture aperture)
    {
        aperture = default;

        if (!bars.TryGet(capability.Bar, out PciBarDescriptor bar))
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.BarIndexOutOfRange,
                VirtioPciDeviceDiscoveryStatus.Success,
                capability.Type,
                capability.Bar);
        }

        switch (bar.Kind)
        {
            case PciBarKind.Memory32:
            case PciBarKind.Memory64:
                break;

            case PciBarKind.Absent:
                return VirtioPciBarApertureValidationResult.Fail(
                    VirtioPciBarApertureValidationStatus.BarAbsent,
                    VirtioPciDeviceDiscoveryStatus.Success,
                    capability.Type,
                    capability.Bar,
                    bar.Kind);

            case PciBarKind.Io:
                return VirtioPciBarApertureValidationResult.Fail(
                    VirtioPciBarApertureValidationStatus.BarIsIo,
                    VirtioPciDeviceDiscoveryStatus.Success,
                    capability.Type,
                    capability.Bar,
                    bar.Kind);

            case PciBarKind.ConsumedByPrevious64BitBar:
                return VirtioPciBarApertureValidationResult.Fail(
                    VirtioPciBarApertureValidationStatus.BarConsumedByPrevious64BitBar,
                    VirtioPciDeviceDiscoveryStatus.Success,
                    capability.Type,
                    capability.Bar,
                    bar.Kind);

            default:
                return VirtioPciBarApertureValidationResult.Fail(
                    VirtioPciBarApertureValidationStatus.BarMalformed,
                    VirtioPciDeviceDiscoveryStatus.Success,
                    capability.Type,
                    capability.Bar,
                    bar.Kind);
        }

        if (capability.Bar >= barSizes.Length)
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.BarSizeUnavailable,
                VirtioPciDeviceDiscoveryStatus.Success,
                capability.Type,
                capability.Bar,
                bar.Kind);
        }

        if (capability.Offset > uint.MaxValue - capability.Length)
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.CapabilityRegionOverflow,
                VirtioPciDeviceDiscoveryStatus.Success,
                capability.Type,
                capability.Bar,
                bar.Kind);
        }

        ulong regionEndOffset = (ulong)capability.Offset + capability.Length;
        ulong barSize = barSizes[capability.Bar];
        if (regionEndOffset > barSize)
        {
            return VirtioPciBarApertureValidationResult.Fail(
                VirtioPciBarApertureValidationStatus.CapabilityRegionBeyondBarSize,
                VirtioPciDeviceDiscoveryStatus.Success,
                capability.Type,
                capability.Bar,
                bar.Kind,
                barSize,
                regionEndOffset);
        }

        aperture = new VirtioPciBarAperture(
            capability.Type,
            capability.Bar,
            bar.Kind,
            bar.BaseAddressBits,
            capability.Offset,
            capability.Length,
            regionEndOffset,
            barSize,
            capability.NotifyOffsetMultiplier);

        return VirtioPciBarApertureValidationResult.Success(default);
    }
}
