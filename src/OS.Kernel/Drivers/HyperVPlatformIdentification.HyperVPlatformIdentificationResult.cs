namespace OS.Drivers;

public readonly struct HyperVPlatformIdentificationResult
{
    private HyperVPlatformIdentificationResult(bool isInitialized, HyperVPlatformIdentificationStatus status, bool hypervisorPresent, HyperVVendorSignature vendorSignature, uint maxHypervisorLeaf, bool hasFeatureIdentificationLeaf, CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        IsInitialized = isInitialized;
        Status = status;
        HypervisorPresent = hypervisorPresent;
        VendorSignature = vendorSignature;
        MaxHypervisorLeaf = maxHypervisorLeaf;
        HasFeatureIdentificationLeaf = hasFeatureIdentificationLeaf;
        FeatureIdentificationLeaf = featureIdentificationLeaf;
    }

    public HyperVPlatformIdentificationStatus Status
    {
        get;
    }
    public bool HypervisorPresent
    {
        get;
    }
    public HyperVVendorSignature VendorSignature
    {
        get;
    }
    public uint MaxHypervisorLeaf
    {
        get;
    }
    public bool HasFeatureIdentificationLeaf
    {
        get;
    }
    public CpuidRegisterSnapshot FeatureIdentificationLeaf
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == HyperVPlatformIdentificationStatus.Success;
    public bool IsHyperVCompatible => Succeeded;

    public static HyperVPlatformIdentificationResult Create(HyperVPlatformIdentificationStatus status, bool hypervisorPresent, HyperVVendorSignature vendorSignature, uint maxHypervisorLeaf, bool hasFeatureIdentificationLeaf, CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        return new HyperVPlatformIdentificationResult(isInitialized: true, status, hypervisorPresent, vendorSignature, maxHypervisorLeaf, hasFeatureIdentificationLeaf, featureIdentificationLeaf);
    }
}
