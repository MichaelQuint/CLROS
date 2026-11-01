namespace OS.Drivers;
public readonly struct VirtioPciBindingPlanResult
{
    private VirtioPciBindingPlanResult(VirtioPciBindingPlanStatus status, PciConfigurationHeaderStatus headerStatus, VirtioPciCapabilityListParseResult capabilityParse, VirtioPciDeviceDiscoveryResult discovery, PciBarDecodeResult barDecode, VirtioPciBarApertureValidationResult apertureValidation, VirtioPciBindingMetadata binding, bool isInitialized)
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

    public static VirtioPciBindingPlanResult Success(VirtioPciCapabilityListParseResult capabilityParse, VirtioPciDeviceDiscoveryResult discovery, PciBarDecodeResult barDecode, VirtioPciBarApertureValidationResult apertureValidation, VirtioPciBindingMetadata binding)
    {
        return new VirtioPciBindingPlanResult(VirtioPciBindingPlanStatus.Success, PciConfigurationHeaderStatus.Success, capabilityParse, discovery, barDecode, apertureValidation, binding, isInitialized: true);
    }

    public static VirtioPciBindingPlanResult Fail(VirtioPciBindingPlanStatus status, PciConfigurationHeaderStatus headerStatus = PciConfigurationHeaderStatus.Success, VirtioPciCapabilityListParseResult capabilityParse = default, VirtioPciDeviceDiscoveryResult discovery = default, PciBarDecodeResult barDecode = default, VirtioPciBarApertureValidationResult apertureValidation = default)
    {
        return new VirtioPciBindingPlanResult(status, headerStatus, capabilityParse, discovery, barDecode, apertureValidation, default, isInitialized: true);
    }
}
