using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class HyperVPlatformIdentificationTests
{

    [HostTest("drivers.hyperv identification rejects missing hypervisor bit", HostLane.Fast, 0)]
    private static bool TestRejectsMissingHypervisorBit()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(leaf1Ecx: 0));

        return HostCheck.Result(!result.Succeeded
            && !result.HypervisorPresent
            && result.Status == HyperVPlatformIdentificationStatus.NoHypervisorPresentBit
            && result.MaxHypervisorLeaf == HyperVPlatformIdentification.FeatureIdentificationLeafNumber);
    }

    [HostTest("drivers.hyperv identification rejects unsupported vendor", HostLane.Fast, 1)]
    private static bool TestRejectsUnsupportedVendor()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(
                vendorEbx: 0x4B4D564B,
                vendorEcx: 0x564B4D56,
                vendorEdx: 0x0000004D));

        return HostCheck.Result(!result.Succeeded
            && result.HypervisorPresent
            && result.Status == HyperVPlatformIdentificationStatus.UnsupportedVendor
            && !result.VendorSignature.IsMicrosoftHyperV);
    }

    [HostTest("drivers.hyperv identification rejects insufficient max leaf", HostLane.Fast, 2)]
    private static bool TestRejectsInsufficientMaxLeaf()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(maxHypervisorLeaf: HyperVPlatformIdentification.FeatureIdentificationLeafNumber - 1));

        return HostCheck.Result(!result.Succeeded
            && result.HypervisorPresent
            && result.VendorSignature.IsMicrosoftHyperV
            && result.MaxHypervisorLeaf == HyperVPlatformIdentification.FeatureIdentificationLeafNumber - 1
            && result.Status == HyperVPlatformIdentificationStatus.InsufficientMaxLeaf);
    }

    [HostTest("drivers.hyperv identification requires feature leaf snapshot", HostLane.Fast, 3)]
    private static bool TestRequiresFeatureLeafSnapshot()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(hasFeatureLeaf: false));

        return HostCheck.Result(!result.Succeeded
            && result.HypervisorPresent
            && result.VendorSignature.IsMicrosoftHyperV
            && result.MaxHypervisorLeaf >= HyperVPlatformIdentification.FeatureIdentificationLeafNumber
            && !result.HasFeatureIdentificationLeaf
            && result.Status == HyperVPlatformIdentificationStatus.MissingFeatureIdentificationLeaf);
    }

    [HostTest("drivers.hyperv identification accepts compatible snapshot", HostLane.Fast, 4)]
    private static bool TestAcceptsCompatibleSnapshot()
    {
        CpuidRegisterSnapshot featureLeaf = new CpuidRegisterSnapshot(
            eax: 0x0000002F,
            ebx: 0x00000001,
            ecx: 0x00000002,
            edx: 0x00000004);
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(
                maxHypervisorLeaf: 0x40000005,
                hasFeatureLeaf: true,
                featureLeaf: featureLeaf));

        return HostCheck.Result(result.Succeeded
            && result.IsHyperVCompatible
            && result.IsInitialized
            && result.HypervisorPresent
            && result.Status == HyperVPlatformIdentificationStatus.Success
            && result.MaxHypervisorLeaf == 0x40000005
            && result.VendorSignature.Equals(
                new HyperVVendorSignature(
                    HyperVPlatformIdentification.MicrosoftHyperVVendorEbx,
                    HyperVPlatformIdentification.MicrosoftHyperVVendorEcx,
                    HyperVPlatformIdentification.MicrosoftHyperVVendorEdx))
            && result.HasFeatureIdentificationLeaf
            && result.FeatureIdentificationLeaf.Eax == 0x0000002F
            && result.FeatureIdentificationLeaf.Ebx == 0x00000001
            && result.FeatureIdentificationLeaf.Ecx == 0x00000002
            && result.FeatureIdentificationLeaf.Edx == 0x00000004);
    }

    [HostTest("drivers.hyperv identification default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        HyperVPlatformIdentificationResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && !result.IsHyperVCompatible
            && result.Status == HyperVPlatformIdentificationStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HyperVPlatformIdentificationResult>());
    }

    private static HyperVPlatformCpuidSnapshot CreateSnapshot(
        uint leaf1Ecx = HyperVPlatformIdentification.HypervisorPresentEcxMask,
        uint maxHypervisorLeaf = HyperVPlatformIdentification.FeatureIdentificationLeafNumber,
        uint vendorEbx = HyperVPlatformIdentification.MicrosoftHyperVVendorEbx,
        uint vendorEcx = HyperVPlatformIdentification.MicrosoftHyperVVendorEcx,
        uint vendorEdx = HyperVPlatformIdentification.MicrosoftHyperVVendorEdx,
        bool hasFeatureLeaf = true,
        CpuidRegisterSnapshot featureLeaf = default)
    {
        return new HyperVPlatformCpuidSnapshot(
            new CpuidRegisterSnapshot(eax: 0, ebx: 0, ecx: leaf1Ecx, edx: 0),
            new CpuidRegisterSnapshot(
                eax: maxHypervisorLeaf,
                ebx: vendorEbx,
                ecx: vendorEcx,
                edx: vendorEdx),
            hasFeatureLeaf,
            featureLeaf);
    }
}
