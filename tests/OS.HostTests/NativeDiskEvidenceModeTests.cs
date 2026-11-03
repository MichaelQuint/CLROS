using OS.Tooling;

internal static class NativeDiskEvidenceModeTests
{
    private const string Trace = "EnableM10018EvidenceTraceProbe";
    private const string Summary = "EnableM10018EvidenceSummaryProbe";

    [HostTest("disk evidence modes remain off in normal and original timing profiles")]
    private static void Defaults()
    {
        foreach (var selected in new[]
        {
            new Dictionary<string, bool>(),
            new Dictionary<string, bool> { ["EnableM10018TimingProbe"] = true }
        })
        {
            var profile = EfiProfile.Create("", selected);
            HostCheck.True(!profile.IsEnabled(Trace) && !profile.IsEnabled(Summary));
            HostCheck.True(!profile.CommandArguments().Contains("--" + Trace) && !profile.CommandArguments().Contains("--" + Summary));
        }
    }

    [HostTest("disk evidence modes select one timing-only symbol and retain exact parent requirements")]
    private static void Modes()
    {
        foreach (var (mode, symbol) in new[] { (Trace, "CLROS_DISK_EVIDENCE_TRACE"), (Summary, "CLROS_DISK_EVIDENCE_SUMMARY") })
        {
            var selected = new Dictionary<string, bool> { [mode] = true };
            var profile = EfiProfile.Create("", selected);
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            foreach (string parent in new[] { "EnableM10018TimingProbe", "EnableM10018FullProbe", "EnableM10018Probe", "EnableM16025Probe" })
                HostCheck.True(profile.IsEnabled(parent));
            HostCheck.True(profile.IsEnabled(mode) && !profile.IsEnabled(mode == Trace ? Summary : Trace));
            HostCheck.Equal(symbol, EfiContracts.ProbeSymbols[mode]);
            HostCheck.True(selected.Count == 1 && selected[mode]);
            var parsed = EfiOptions.Parse(["--" + mode], build: true).Profile();
            HostCheck.True(parsed.CommandArguments().SequenceEqual(profile.CommandArguments()));
        }
    }

    [HostTest("disk evidence modes reject mixed modes false parents normal boot and incompatible profiles")]
    private static void Rejections()
    {
        Reject(new() { [Trace] = true, [Summary] = true });
        foreach (string mode in new[] { Trace, Summary })
        {
            foreach (string parent in new[] { "EnableM10018TimingProbe", "EnableM10018FullProbe", "EnableM10018Probe", "EnableM16025Probe" })
                Reject(new() { [mode] = true, [parent] = false });
            foreach (string conflict in new[] { "EnableM16027Probe", "EnableM10017Probe", "EnableM16014Probe" })
                Reject(new() { [mode] = true, [conflict] = true });
            Reject(new() { [mode] = true }, "VerifiedExit");
        }
        Reject(new() { ["EnableM10018EvidenceUnknownProbe"] = true });
    }

    [HostTest("disk evidence controls preserve timing capture resources through actual QEMU planning")]
    private static void Plans()
    {
        foreach (string mode in new[] { Trace, Summary })
        {
            var options = QemuSmokeOptions.Parse("-BuildEfi", "-" + mode, "-LinuxElfPackage", "artifacts/disk-static.pkg",
                "-LinuxThreadingDisassembly", "artifacts/disk-static.dump", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/webroot");
            var plan = QemuProfiles.Create(options);
            HostCheck.True(NativeAotDiskSelection.TimingSelected(plan.Flags) && plan.IsEnabled(mode));
            HostCheck.Equal(600, options.TimeoutSeconds);
            HostCheck.True(options.MemoryMiB is null);
            HostCheck.Equal("MinimalApiStaticFiles", options.LinuxThreadingApplication);
        }
    }

    private static void Reject(Dictionary<string, bool> selected, string policy = "")
    {
        bool rejected = false;
        try
        {
            _ = EfiProfile.Create(policy, selected);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
