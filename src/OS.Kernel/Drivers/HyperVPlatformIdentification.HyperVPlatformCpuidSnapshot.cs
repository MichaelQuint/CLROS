namespace OS.Drivers;

public readonly struct HyperVPlatformCpuidSnapshot
{
    public HyperVPlatformCpuidSnapshot(CpuidRegisterSnapshot processorFeatureLeaf, CpuidRegisterSnapshot hypervisorVendorLeaf, bool hasFeatureIdentificationLeaf, CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        ProcessorFeatureLeaf = processorFeatureLeaf;
        HypervisorVendorLeaf = hypervisorVendorLeaf;
        HasFeatureIdentificationLeaf = hasFeatureIdentificationLeaf;
        FeatureIdentificationLeaf = featureIdentificationLeaf;
    }

    public CpuidRegisterSnapshot ProcessorFeatureLeaf
    {
        get;
    }
    public CpuidRegisterSnapshot HypervisorVendorLeaf
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
}
