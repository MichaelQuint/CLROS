using OS.Tooling;

internal static class EfiProfileTestControls
{
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
