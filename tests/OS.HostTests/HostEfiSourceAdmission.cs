using OS.Tooling;

/// <summary>Checks a selected source through the same family-pin and restricted
/// source policies used by project admission. Runtime conditional sources use
/// their reviewed family identity; the restricted kernel parser is not a general
/// EFI preprocessor. An unknown source never supplies admission authority.</summary>
internal static class HostEfiSourceAdmission
{
    internal static bool Accepts(string include, string source)
    {
        bool family = EfiContracts.SourceFamilyIdentities.ContainsKey(include);
        bool restricted = include.StartsWith("..\\OS.Kernel\\", StringComparison.Ordinal)
            || EfiContracts.SourceAdmissionReasons.ContainsKey(include);
        if (!family && !restricted)
            return false;
        try
        {
            if (family)
                EfiProjectAdmission.VerifySourceFamilyPin(include, source);
            return !restricted || EfiSourceAdmission.Inspect(source, include).Succeeded;
        }
        catch (ToolFailure)
        {
            return false;
        }
    }
}
