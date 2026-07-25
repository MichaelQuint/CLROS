using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class HyperVPlatformIdentificationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "drivers.hyperv identification rejects missing hypervisor bit",
            TestRejectsMissingHypervisorBit,
            ref failures);
        Run(
            "drivers.hyperv identification rejects unsupported vendor",
            TestRejectsUnsupportedVendor,
            ref failures);
        Run(
            "drivers.hyperv identification rejects insufficient max leaf",
            TestRejectsInsufficientMaxLeaf,
            ref failures);
        Run(
            "drivers.hyperv identification requires feature leaf snapshot",
            TestRequiresFeatureLeafSnapshot,
            ref failures);
        Run(
            "drivers.hyperv identification accepts compatible snapshot",
            TestAcceptsCompatibleSnapshot,
            ref failures);
        Run(
            "drivers.hyperv identification default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Hyper-V platform identification host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestRejectsMissingHypervisorBit()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(leaf1Ecx: 0));

        return !result.Succeeded
            && !result.HypervisorPresent
            && result.Status == HyperVPlatformIdentificationStatus.NoHypervisorPresentBit
            && result.MaxHypervisorLeaf == HyperVPlatformIdentification.FeatureIdentificationLeafNumber;
    }

    private static bool TestRejectsUnsupportedVendor()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(
                vendorEbx: 0x4B4D564B,
                vendorEcx: 0x564B4D56,
                vendorEdx: 0x0000004D));

        return !result.Succeeded
            && result.HypervisorPresent
            && result.Status == HyperVPlatformIdentificationStatus.UnsupportedVendor
            && !result.VendorSignature.IsMicrosoftHyperV;
    }

    private static bool TestRejectsInsufficientMaxLeaf()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(maxHypervisorLeaf: HyperVPlatformIdentification.FeatureIdentificationLeafNumber - 1));

        return !result.Succeeded
            && result.HypervisorPresent
            && result.VendorSignature.IsMicrosoftHyperV
            && result.MaxHypervisorLeaf == HyperVPlatformIdentification.FeatureIdentificationLeafNumber - 1
            && result.Status == HyperVPlatformIdentificationStatus.InsufficientMaxLeaf;
    }

    private static bool TestRequiresFeatureLeafSnapshot()
    {
        HyperVPlatformIdentificationResult result = HyperVPlatformIdentification.Identify(
            CreateSnapshot(hasFeatureLeaf: false));

        return !result.Succeeded
            && result.HypervisorPresent
            && result.VendorSignature.IsMicrosoftHyperV
            && result.MaxHypervisorLeaf >= HyperVPlatformIdentification.FeatureIdentificationLeafNumber
            && !result.HasFeatureIdentificationLeaf
            && result.Status == HyperVPlatformIdentificationStatus.MissingFeatureIdentificationLeaf;
    }

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

        return result.Succeeded
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
            && result.FeatureIdentificationLeaf.Edx == 0x00000004;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        HyperVPlatformIdentificationResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && !result.IsHyperVCompatible
            && result.Status == HyperVPlatformIdentificationStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<HyperVPlatformIdentificationResult>();
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
