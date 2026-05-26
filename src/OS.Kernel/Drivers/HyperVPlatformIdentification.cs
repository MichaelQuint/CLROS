namespace OS.Drivers;

public enum HyperVPlatformIdentificationStatus
{
    Success = 0,
    NoHypervisorPresentBit = 1,
    UnsupportedVendor = 2,
    InsufficientMaxLeaf = 3,
    MissingFeatureIdentificationLeaf = 4
}

public readonly struct CpuidRegisterSnapshot
{
    public CpuidRegisterSnapshot(
        uint eax,
        uint ebx,
        uint ecx,
        uint edx)
    {
        Eax = eax;
        Ebx = ebx;
        Ecx = ecx;
        Edx = edx;
    }

    public uint Eax { get; }

    public uint Ebx { get; }

    public uint Ecx { get; }

    public uint Edx { get; }
}

public readonly struct HyperVVendorSignature
{
    public HyperVVendorSignature(
        uint ebx,
        uint ecx,
        uint edx)
    {
        Ebx = ebx;
        Ecx = ecx;
        Edx = edx;
    }

    public uint Ebx { get; }

    public uint Ecx { get; }

    public uint Edx { get; }

    public bool IsMicrosoftHyperV =>
        Ebx == HyperVPlatformIdentification.MicrosoftHyperVVendorEbx &&
        Ecx == HyperVPlatformIdentification.MicrosoftHyperVVendorEcx &&
        Edx == HyperVPlatformIdentification.MicrosoftHyperVVendorEdx;

    public bool Equals(HyperVVendorSignature other)
    {
        return Ebx == other.Ebx &&
            Ecx == other.Ecx &&
            Edx == other.Edx;
    }
}

public readonly struct HyperVPlatformCpuidSnapshot
{
    public HyperVPlatformCpuidSnapshot(
        CpuidRegisterSnapshot processorFeatureLeaf,
        CpuidRegisterSnapshot hypervisorVendorLeaf,
        bool hasFeatureIdentificationLeaf,
        CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        ProcessorFeatureLeaf = processorFeatureLeaf;
        HypervisorVendorLeaf = hypervisorVendorLeaf;
        HasFeatureIdentificationLeaf = hasFeatureIdentificationLeaf;
        FeatureIdentificationLeaf = featureIdentificationLeaf;
    }

    public CpuidRegisterSnapshot ProcessorFeatureLeaf { get; }

    public CpuidRegisterSnapshot HypervisorVendorLeaf { get; }

    public bool HasFeatureIdentificationLeaf { get; }

    public CpuidRegisterSnapshot FeatureIdentificationLeaf { get; }
}

public readonly struct HyperVPlatformIdentificationResult
{
    private HyperVPlatformIdentificationResult(
        bool isInitialized,
        HyperVPlatformIdentificationStatus status,
        bool hypervisorPresent,
        HyperVVendorSignature vendorSignature,
        uint maxHypervisorLeaf,
        bool hasFeatureIdentificationLeaf,
        CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        IsInitialized = isInitialized;
        Status = status;
        HypervisorPresent = hypervisorPresent;
        VendorSignature = vendorSignature;
        MaxHypervisorLeaf = maxHypervisorLeaf;
        HasFeatureIdentificationLeaf = hasFeatureIdentificationLeaf;
        FeatureIdentificationLeaf = featureIdentificationLeaf;
    }

    public HyperVPlatformIdentificationStatus Status { get; }

    public bool HypervisorPresent { get; }

    public HyperVVendorSignature VendorSignature { get; }

    public uint MaxHypervisorLeaf { get; }

    public bool HasFeatureIdentificationLeaf { get; }

    public CpuidRegisterSnapshot FeatureIdentificationLeaf { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == HyperVPlatformIdentificationStatus.Success;

    public bool IsHyperVCompatible => Succeeded;

    public static HyperVPlatformIdentificationResult Create(
        HyperVPlatformIdentificationStatus status,
        bool hypervisorPresent,
        HyperVVendorSignature vendorSignature,
        uint maxHypervisorLeaf,
        bool hasFeatureIdentificationLeaf,
        CpuidRegisterSnapshot featureIdentificationLeaf)
    {
        return new HyperVPlatformIdentificationResult(
            isInitialized: true,
            status,
            hypervisorPresent,
            vendorSignature,
            maxHypervisorLeaf,
            hasFeatureIdentificationLeaf,
            featureIdentificationLeaf);
    }
}

public static class HyperVPlatformIdentification
{
    public const uint HypervisorVendorLeafNumber = 0x40000000;
    public const uint FeatureIdentificationLeafNumber = 0x40000003;
    public const int HypervisorPresentEcxBit = 31;
    public const uint HypervisorPresentEcxMask = 1u << HypervisorPresentEcxBit;
    public const uint MicrosoftHyperVVendorEbx = 0x7263694D;
    public const uint MicrosoftHyperVVendorEcx = 0x666F736F;
    public const uint MicrosoftHyperVVendorEdx = 0x76482074;

    public static HyperVPlatformIdentificationResult Identify(
        HyperVPlatformCpuidSnapshot snapshot)
    {
        bool hypervisorPresent = HasHypervisorPresentBit(snapshot.ProcessorFeatureLeaf);
        HyperVVendorSignature vendorSignature = GetVendorSignature(snapshot.HypervisorVendorLeaf);
        uint maxHypervisorLeaf = snapshot.HypervisorVendorLeaf.Eax;

        if (!hypervisorPresent)
        {
            return CreateResult(
                HyperVPlatformIdentificationStatus.NoHypervisorPresentBit,
                hypervisorPresent,
                vendorSignature,
                maxHypervisorLeaf,
                snapshot);
        }

        if (!vendorSignature.IsMicrosoftHyperV)
        {
            return CreateResult(
                HyperVPlatformIdentificationStatus.UnsupportedVendor,
                hypervisorPresent,
                vendorSignature,
                maxHypervisorLeaf,
                snapshot);
        }

        if (maxHypervisorLeaf < FeatureIdentificationLeafNumber)
        {
            return CreateResult(
                HyperVPlatformIdentificationStatus.InsufficientMaxLeaf,
                hypervisorPresent,
                vendorSignature,
                maxHypervisorLeaf,
                snapshot);
        }

        if (!snapshot.HasFeatureIdentificationLeaf)
        {
            return CreateResult(
                HyperVPlatformIdentificationStatus.MissingFeatureIdentificationLeaf,
                hypervisorPresent,
                vendorSignature,
                maxHypervisorLeaf,
                snapshot);
        }

        return CreateResult(
            HyperVPlatformIdentificationStatus.Success,
            hypervisorPresent,
            vendorSignature,
            maxHypervisorLeaf,
            snapshot);
    }

    public static bool HasHypervisorPresentBit(CpuidRegisterSnapshot processorFeatureLeaf)
    {
        return (processorFeatureLeaf.Ecx & HypervisorPresentEcxMask) != 0;
    }

    public static HyperVVendorSignature GetVendorSignature(
        CpuidRegisterSnapshot hypervisorVendorLeaf)
    {
        return new HyperVVendorSignature(
            hypervisorVendorLeaf.Ebx,
            hypervisorVendorLeaf.Ecx,
            hypervisorVendorLeaf.Edx);
    }

    private static HyperVPlatformIdentificationResult CreateResult(
        HyperVPlatformIdentificationStatus status,
        bool hypervisorPresent,
        HyperVVendorSignature vendorSignature,
        uint maxHypervisorLeaf,
        HyperVPlatformCpuidSnapshot snapshot)
    {
        return HyperVPlatformIdentificationResult.Create(
            status,
            hypervisorPresent,
            vendorSignature,
            maxHypervisorLeaf,
            snapshot.HasFeatureIdentificationLeaf,
            snapshot.FeatureIdentificationLeaf);
    }
}
