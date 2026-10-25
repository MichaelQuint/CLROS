using OS.Tooling;

internal static class NativeAotWritableDiskSelectionTests
{
    private static string[] Arguments(string fixture) => ["-BuildEfi", "-EnableM16027Probe", "-WritableDiskFixture", fixture,
        "-LinuxElfPackage", "artifacts/writable.pkg", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/published-webroot"];

    [HostTest("writable profile binds explicit canonical30 and40 packages", HostLane.Fast, 0)]
    private static void CanonicalPackages()
    {
        foreach (string fixture in new[] { "Mechanical30", "LogUpload40" })
        {
            var options = QemuSmokeOptions.Parse(Arguments(fixture));
            var plan = QemuProfiles.Create(options);
            HostCheck.True(NativeAotDiskSelection.WritableSelected(plan.Flags));
            HostCheck.Equal(180, options.TimeoutSeconds);
            HostCheck.True(plan.Required.Contains("CLROS_M16_027_READY") && plan.Required.Contains("M16_027_STOP"));
            HostCheck.True(!plan.Required.Contains("CLROS_M16_025_READY") && !plan.Required.Contains("CLROS_M10_018_READY"));
            foreach (string parent in new[] { "M16025", "M16024", "M16023", "M16012", "M16011", "M16008", "M07026" })
                HostCheck.True(plan.IsEnabled("Enable" + parent + "Probe"));
            byte[] package = fixture == "Mechanical30" ? LinuxWritableDiskProofFixture.CreatePackage() : LinuxWritableDiskProofFixture.CreateLogUploadPackage();
            NativeAotDiskSelection.RequireWritablePackage(package, fixture);
            Reject(() => NativeAotDiskSelection.RequireWritablePackage(package, fixture == "Mechanical30" ? "LogUpload40" : "Mechanical30"));
            package[^1] ^= 1;
            Reject(() => NativeAotDiskSelection.RequireWritablePackage(package, fixture));
        }
        Reject(() => NativeAotDiskSelection.RequireWritablePackage(LinuxDiskFileProofFixture.CreatePackage(), "Mechanical30"));
    }

    [HostTest("writable selection rejects missing explicit fixtures and incompatible profiles", HostLane.Fast, 1)]
    private static void Rejections()
    {
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM16027Probe"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-WritableDiskFixture", "Mechanical30"])));
        foreach (string parent in new[] { "M16025", "M16024", "M16023", "M16012", "M16011", "M16008" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-Enable" + parent + "Probe:false"])));
        foreach (string conflict in new[] { "M10018", "M10017", "M10004", "M16014", "M16013" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-Enable" + conflict + "Probe"])));
        foreach (string[] extra in new[] {
            new[] { "-TimeoutSeconds", "119" }, new[] { "-TimeoutSeconds", "221" },
            new[] { "-LinuxDiskRaceVariant", "ExitPending" }, new[] { "-LinuxThreadingApplication", "NativeAotSocketSmoke" },
            new[] { "-GptImageVariant", "PrimaryHeaderCrcFault" }, new[] { "-DiskFormat", "qcow2" },
            new[] { "-SkipImageBuild" }, new[] { "-MarkerPreset", "unexpected-vector-panic" },
            new[] { "-RequireMarker", "CLROS_M16_025_READY" }, new[] { "-WritableDiskFixture", "LogUpload40" } })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), ..extra])));
        Reject(() => QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-WritableDiskFixture", "unknown"]));
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is ToolFailure or ArgumentException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
