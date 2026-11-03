using OS.Kernel.Diagnostics;
using OS.Tooling;
using System.Xml.Linq;

internal static class NativeAotSocketVectorsProfileTests
{
    private static string[] Arguments => ["--BuildEfi","--EnableM16013Probe","--LinuxThreadingDisassembly","original-vector.llvm.txt",
        "--LinuxElfPackage","original-vector.pkg","--DryRun"];

    [HostTest("vector EFI leaf defaults off and resolves only its complete socket runtime parents")]
    private static void Profile()
    {
        HostCheck.True(!Probes.EnableM16013Probe && !EfiProfile.Create(null, new Dictionary<string, bool>()).IsEnabled("M16013"));
        HostCheck.Equal("M16_013_PROBE", EfiContracts.ProbeSymbols["EnableM16013Probe"]);
        HostCheck.True(EfiContracts.ProjectDefaultProbes.Count(x => x == "EnableM16013Probe") == 1 &&
            EfiContracts.FirstExitDependentProbes.Count(x => x == "EnableM16013Probe") == 1);
        var project = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        HostCheck.Equal("false", project.Descendants("EnableM16013Probe").Single().Value);
        HostCheck.Equal("$(DefineConstants);M16_013_PROBE", project.Descendants("DefineConstants").Single(x =>
            x.Parent?.Attribute("Condition")?.Value == "'$(EnableM16013Probe)' == 'true'").Value);
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(["--EnableM16013Probe"], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.Equal(23, profile.Flags.Count(x => x.Value));
            foreach (string parent in profile.Flags.Where(x => x.Value && x.Key != "EnableM16013Probe").Select(x => x.Key))
                Reject(() => EfiOptions.Parse(["--EnableM16013Probe", "--" + parent, "false"], build).Profile());
            foreach (string conflict in new[] { "M07027", "M07028", "M10004", "M09011", "M09012", "M16010", "M05VirtqueueInterrupt", "M09006" })
                Reject(() => EfiOptions.Parse(["--EnableM16013Probe", "--Enable" + conflict + "Probe"], build).Profile());
            HostCheck.Equal("", NativeAotSocketSelection.Application(profile.Flags));
        }
    }

    [HostTest("vector network envelope joins parent cleanup and rejects missing duplicated or foreign terminal markers")]
    private static void NetworkEnvelope()
    {
        var rows = NativeAotSocketIntegrationTests.Envelope(true, true);
        HostCheck.Equal(4, QemuTranscripts.InspectSocketVectorNetworkForTests(rows).Retired);
        Reject(() => QemuTranscripts.InspectSocketNetworkForTests(rows, true));
        Reject(() => QemuTranscripts.InspectSocketVectorNetworkForTests(NativeAotSocketIntegrationTests.Envelope(true)));
        foreach (string missing in new[] { "TEST_START m16-013", "TEST_PASS m16-012", "CLROS_M16_013_READY", "TEST_PASS m16-013" })
            Reject(() => QemuTranscripts.InspectSocketVectorNetworkForTests(rows.Where(x => x != missing).ToArray()));
        Reject(() => QemuTranscripts.InspectSocketVectorNetworkForTests([.. rows, "TEST_PASS m16-013"]));
        Reject(() => QemuTranscripts.InspectSocketVectorNetworkForTests(rows.Select(x => x.Replace("stage=5 a=8 b=0", "stage=5 a=8 b=1", StringComparison.Ordinal)).ToArray()));
    }

    [HostTest("vector runner uses dedicated terminal original inputs bounded forwarded peer and runtime drain")]
    private static void Runner()
    {
        var options = QemuSmokeOptions.Parse(Arguments);
        var plan = QemuProfiles.Create(options);
        HostCheck.Equal("NativeAotSocketVectors", options.LinuxThreadingApplication);
        HostCheck.Equal(600, options.TimeoutSeconds);
        HostCheck.Equal("virtio", options.FirmwareRngSource);
        HostCheck.Equal("TEST_PASS m16-013", plan.Ordered[^1]);
        HostCheck.True(plan.Ordered.Contains("TEST_START m16-012") && plan.Ordered.Contains("TEST_START m16-013") && plan.Unique.Contains("CLROS_M16_013_READY"));
        List<string>? runner = null;
        int result = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, TextWriter.Null, (name, args) =>
        {
            HostCheck.Equal("OS.QemuRunner", name);
            runner = args.ToList();
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        }).GetAwaiter().GetResult();
        HostCheck.True(result == 0 && runner is not null && !runner.Contains("--continuing-network-peer") && !runner.Contains("--socket-containment-peer") && runner.Contains("--ethernet-capture"));
        int port = runner!.IndexOf("--socket-forward-port");
        HostCheck.True(port >= 0 && runner[port + 1] == "20080");
        HostCheck.True(runner.Sum(x => x.Length + 3) < 30000, "vector runner command line remains bounded");
        foreach (string[] extra in new string[][]{["--LinuxThreadingApplication","NativeAotSocketSmokeAsync"],["--TimeoutSeconds","599"],
            ["--LinuxElfImageVariant","DigestFault"],["--LinuxHelloDisassembly","hello.txt"],["--RequireMarker","CLROS_M07_026_READY"]})
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. Arguments, .. extra])));
        foreach (string flag in new[] { "EnableM16011Probe", "EnableM16012Probe", "EnableM10004Probe" })
        {
            var original = EfiOptions.Parse(["--" + flag], true).Profile();
            var originalPlan = QemuProfiles.Create(QemuSmokeOptions.Parse([.. Arguments.Where(x => x != "--EnableM16013Probe"), "--" + flag]));
            QemuTranscripts.ValidateUnselectedRecords("M16_013_MESSAGE n=1\r\nM16_013_VECTOR n=1\r\n", originalPlan);
            Reject(() => QemuTranscripts.ValidateUnselectedRecords("CLROS_M16_013_READY\r\n", originalPlan));
            HostCheck.True(!original.IsEnabled("M16013"));
        }
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (ToolFailure) { return; }
        throw new InvalidOperationException("vector_profile_invalid_accepted");
    }
}
