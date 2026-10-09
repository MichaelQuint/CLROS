using OS.Tooling;

internal static class EfiProfileTestControls
{
    // Independent expectations shared by cross-profile rejection fixtures.
    internal static string? MissingParentReason(IReadOnlyCollection<string> selected)
    {
        bool Has(string name) => selected.Contains("Enable" + name + "Probe", StringComparer.Ordinal);
        foreach (var (child, parent, reason) in new (string, string, string)[]
        {
            ("M03FirmwareAdapterRevocation", "M03FirstRealExitBootServices", "m03_revocation_requires_first_exit"),
            ("M03OwnedSubsystemSurvival", "M03FirstRealExitBootServices", "m03_owned_requires_first_exit"),
            ("M03OwnedSubsystemPanic", "M03OwnedSubsystemSurvival", "m03_owned_panic_requires_survival"),
            ("M05PhysicalMapping", "M03FirstRealExitBootServices", "m05_mapping_requires_owned_first_exit"),
            ("M05PhysicalMapping", "M03OwnedSubsystemSurvival", "m05_mapping_requires_owned_first_exit"),
            ("M05OwnedAcpiSelection", "M03FirstRealExitBootServices", "m05_acpi_requires_owned_first_exit"),
            ("M05OwnedAcpiSelection", "M03OwnedSubsystemSurvival", "m05_acpi_requires_owned_first_exit"),
            ("M04ClassConstructorExecution", "M04GcStaticRootMaterialization", "m04_cctor_requires_gc_profile"),
            ("M04InterfaceDispatch", "M04ClassConstructorExecution", "m04_dispatch_requires_cctor_profile"),
            ("M04X64UnwindMetadata", "M04TypeManagerModuleRegistration", "m04_x64_unwind_metadata_requires_original_module"),
            ("M04SimpleThrowCatch", "M04GcStaticRootMaterialization", "m04_simple_catch_requires_gc_profile"),
            ("M04SingleFinallyCatch", "M04GcStaticRootMaterialization", "m04_single_finally_requires_gc_profile"),
            ("M04Rethrow", "M04GcStaticRootMaterialization", "m04_rethrow_requires_gc_profile"),
            ("M04SeededCollectorChurn", "M04GcStaticRootMaterialization", "m04_churn_requires_gc_profile")
        }) if (Has(child) && !Has(parent)) return reason;
        return null;
    }

    // The reviewed prerequisite order of the returning M01/M02 profiles.
    private static readonly string[] ReturningChain =
    [
        "M01LoadedImageStack", "M01LiveAllocator", "M01OwnedPaging", "M02SafeStub",
        "M02CheckedPortIoBinding", "M02Com1OwnedPortIo", "M02OwnedCom1Ready",
        "M02PanicSafeOwnedCom1", "M02OwnedX64GdtEncoding", "M02GuardedIstStackAllocation"
    ];

    internal static bool Returning(string profileName, IReadOnlyList<string> required, IReadOnlyList<string> forbidden,
        string ready, string failure)
    {
        int at = Array.IndexOf(ReturningChain, profileName);
        HostCheck.True(at >= 0, "reviewed returning profile");
        string[] flags = ReturningChain[..(at + 1)].Select(n => "Enable" + n + "Probe").ToArray();
        string[] arguments = flags.Select(n => "--" + n).ToArray();
        foreach (bool build in new[] { true, false })
        {
            var profile = EfiOptions.Parse(arguments, build).Profile();
            HostCheck.True(flags.All(profile.IsEnabled), "complete explicit profile reaches build and verifier");
            foreach (string prerequisite in arguments[..^1])
            {
                HostCheck.True(HostToolProcess.ValidateEfiProfile(build, arguments.Where(a => a != prerequisite).ToArray()).ExitCode != 0,
                    "missing prerequisite refused: " + prerequisite);
                HostCheck.True(HostToolProcess.ValidateEfiProfile(build, arguments.Select(a => a == prerequisite ? a + ":false" : a).ToArray()).ExitCode != 0,
                    "explicit false prerequisite refused: " + prerequisite);
            }
        }
        QemuSmokePlan plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. arguments]));
        HostCheck.True(flags.All(plan.IsEnabled), "profile reaches QEMU build request");
        string[] missingRequired = required.Where(marker => !plan.Required.Contains(marker)).ToArray();
        string[] missingForbidden = forbidden.Where(marker => !plan.Forbidden.Contains(marker)).ToArray();
        HostCheck.True(missingRequired.Length == 0 && missingForbidden.Length == 0,
            "whole selected marker inventories profile=" + profileName +
            " missingRequired=[" + string.Join(" | ", missingRequired) + "]" +
            " missingForbidden=[" + string.Join(" | ", missingForbidden) + "]");
        HostCheck.True(plan.Required.Contains(ready) && plan.Forbidden.Contains(failure), "independent ready and failure records");
        HostCheck.Equal(plan.Required.Count, plan.Required.Distinct(StringComparer.Ordinal).Count());
        HostCheck.Equal(plan.Forbidden.Count, plan.Forbidden.Distinct(StringComparer.Ordinal).Count());
        var baseline = QemuProfiles.Create(QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--dry-run"));
        HostCheck.True(!baseline.Required.Contains(ready), "default marker isolation");
        foreach (string[] rejected in new string[][] { ["--dry-run", .. arguments], ["--dry-run", "--build-efi", "--skip-image-build", .. arguments] })
        {
            bool accepted = true;
            try { QemuProfiles.Create(QemuSmokeOptions.Parse(rejected)); }
            catch (ToolFailure) { accepted = false; }
            HostCheck.True(!accepted, "profile needs fresh built image");
        }
        return true;
    }
}
