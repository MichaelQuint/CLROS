namespace OS.Drivers;
public readonly struct VirtioPciBarApertureValidationResult
{
    private VirtioPciBarApertureValidationResult(VirtioPciBarApertureValidationStatus status, VirtioPciDeviceDiscoveryStatus discoveryStatus, VirtioPciCapabilityType failingCapabilityType, byte failingBarIndex, PciBarKind failingBarKind, ulong failingBarSize, ulong failingRegionEndOffset, VirtioPciBarApertureSet apertures, bool isInitialized)
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

    public static VirtioPciBarApertureValidationResult Success(VirtioPciBarApertureSet apertures)
    {
        return new VirtioPciBarApertureValidationResult(VirtioPciBarApertureValidationStatus.Success, VirtioPciDeviceDiscoveryStatus.Success, default, PciBarDecoder.NoBarIndex, default, failingBarSize: 0, failingRegionEndOffset: 0, apertures, isInitialized: true);
    }

    public static VirtioPciBarApertureValidationResult Fail(VirtioPciBarApertureValidationStatus status, VirtioPciDeviceDiscoveryStatus discoveryStatus, VirtioPciCapabilityType failingCapabilityType, byte failingBarIndex, PciBarKind failingBarKind = default, ulong failingBarSize = 0, ulong failingRegionEndOffset = 0)
    {
        return new VirtioPciBarApertureValidationResult(status, discoveryStatus, failingCapabilityType, failingBarIndex, failingBarKind, failingBarSize, failingRegionEndOffset, default, isInitialized: true);
    }
}
