namespace OS.Drivers;

public static class HyperVPlatformIdentification
{
    public const uint HypervisorVendorLeafNumber = 0x40000000;
    public const uint FeatureIdentificationLeafNumber = 0x40000003;
    public const int HypervisorPresentEcxBit = 31;
    public const uint HypervisorPresentEcxMask = 1u << HypervisorPresentEcxBit;
    public const uint MicrosoftHyperVVendorEbx = 0x7263694D;
    public const uint MicrosoftHyperVVendorEcx = 0x666F736F;
    public const uint MicrosoftHyperVVendorEdx = 0x76482074;
    public static HyperVPlatformIdentificationResult Identify(HyperVPlatformCpuidSnapshot snapshot)
    {
        bool hypervisorPresent = HasHypervisorPresentBit(snapshot.ProcessorFeatureLeaf);
        HyperVVendorSignature vendorSignature = GetVendorSignature(snapshot.HypervisorVendorLeaf);
        uint maxHypervisorLeaf = snapshot.HypervisorVendorLeaf.Eax;
        if (!hypervisorPresent)
        {
            return CreateResult(HyperVPlatformIdentificationStatus.NoHypervisorPresentBit, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot);
        }

        if (!vendorSignature.IsMicrosoftHyperV)
        {
            return CreateResult(HyperVPlatformIdentificationStatus.UnsupportedVendor, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot);
        }

        if (maxHypervisorLeaf < FeatureIdentificationLeafNumber)
        {
            return CreateResult(HyperVPlatformIdentificationStatus.InsufficientMaxLeaf, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot);
        }

        if (!snapshot.HasFeatureIdentificationLeaf)
        {
            return CreateResult(HyperVPlatformIdentificationStatus.MissingFeatureIdentificationLeaf, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot);
        }

        return CreateResult(HyperVPlatformIdentificationStatus.Success, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot);
    }

    public static bool HasHypervisorPresentBit(CpuidRegisterSnapshot processorFeatureLeaf)
    {
        return (processorFeatureLeaf.Ecx & HypervisorPresentEcxMask) != 0;
    }

    public static HyperVVendorSignature GetVendorSignature(CpuidRegisterSnapshot hypervisorVendorLeaf)
    {
        return new HyperVVendorSignature(hypervisorVendorLeaf.Ebx, hypervisorVendorLeaf.Ecx, hypervisorVendorLeaf.Edx);
    }

    private static HyperVPlatformIdentificationResult CreateResult(HyperVPlatformIdentificationStatus status, bool hypervisorPresent, HyperVVendorSignature vendorSignature, uint maxHypervisorLeaf, HyperVPlatformCpuidSnapshot snapshot)
    {
        return HyperVPlatformIdentificationResult.Create(status, hypervisorPresent, vendorSignature, maxHypervisorLeaf, snapshot.HasFeatureIdentificationLeaf, snapshot.FeatureIdentificationLeaf);
    }
}
