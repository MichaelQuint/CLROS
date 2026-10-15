using OS.Tooling;

internal static class NativeAotRuntimeIntegrationTests
{
    private static readonly string[] Apps = ["NativeAotSuspensionStress", "NativeAotThreadPoolStress", "NativeAotRuntimeMatrix"];
    private static QemuSmokeOptions Options(string app, params string[] extra) => QemuSmokeOptions.Parse(
        ["-BuildEfi", "-" + NativeAotRuntimeSelection.Flag(app), "-LinuxThreadingDisassembly", "artifacts/input.dump", .. extra]);

    [HostTest("nativeaot.runtime profiles require exact applications full signal prerequisites and distinct outer markers")]
    private static void Profiles()
    {
        HostCheck.True(EfiContracts.ProbeSymbols.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(EfiContracts.DiagnosticProbes), "every diagnostic probe has one reviewed compile symbol");
        HostCheck.Equal("M09_011_PROBE", EfiContracts.ProbeSymbols["EnableM09011Probe"]);
        HostCheck.Equal("M09_012_PROBE", EfiContracts.ProbeSymbols["EnableM09012Probe"]);
        HostCheck.Equal("M16_010_PROBE", EfiContracts.ProbeSymbols["EnableM16010Probe"]);
        HostCheck.True(Apps.All(app => !EfiProfile.Create(null, new Dictionary<string, bool>()).IsEnabled(NativeAotRuntimeSelection.Flag(app))));
        foreach (string app in Apps)
        {
            var options = Options(app, "-DryRun"); var plan = QemuProfiles.Create(options);
            HostCheck.Equal(app, options.LinuxThreadingApplication); HostCheck.Equal("Diagnostic", plan.BootPolicy);
            foreach (string flag in new[] { "EnableM16002Probe", "EnableM16003Probe", "EnableM16004Probe", "EnableM16005Probe",
                "EnableM16006Probe", "EnableM16007Probe", "EnableM16008Probe", "EnableM16009Probe", "EnableM05ApicSetupProbe",
                "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe" }) HostCheck.True(plan.IsEnabled(flag));
            var m = NativeAotRuntimeSelection.Markers(app);
            foreach (string marker in new[] { m.Start, m.Ready, m.Pass })
                HostCheck.True(plan.Required.Contains(marker) && plan.Unique.Contains(marker));
            var ordered = plan.Ordered.ToList();
            HostCheck.True(ordered.IndexOf(m.Start) < ordered.IndexOf("TEST_START m16-009") &&
                ordered.IndexOf("TEST_PASS m16-009") < ordered.IndexOf(m.Ready) && ordered.IndexOf(m.Ready) < ordered.IndexOf(m.Pass));
            foreach (string other in Apps.Where(other => other != app))
                HostCheck.True(!plan.IsEnabled(NativeAotRuntimeSelection.Flag(other)) && !plan.Required.Contains(NativeAotRuntimeSelection.Markers(other).Ready));
            HostCheck.Equal(600, options.TimeoutSeconds);
            var profile = EfiOptions.Parse(["-" + NativeAotRuntimeSelection.Flag(app)], true).Profile();
            string[] parents = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16009Probe"] = true })
                .Flags.Where(p => p.Value).Select(p => p.Key).ToArray();
            HostCheck.True(parents.All(profile.IsEnabled) && profile.Flags.Count(p => p.Value) == parents.Length + 1);
            foreach (string parent in parents)
            {
                bool rejected = false;
                try { EfiOptions.Parse(["-" + NativeAotRuntimeSelection.Flag(app), "-" + parent + "=false"], true).Profile(); }
                catch (ToolFailure) { rejected = true; }
                HostCheck.True(rejected, "explicit disabled parent " + parent);
            }
        }
        var old = QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16009Probe"); QemuProfiles.Create(old); HostCheck.Equal(30, old.TimeoutSeconds);
    }

    [HostTest("nativeaot.runtime dry run forwards exact outer markers and selected timeout without claiming prepared execution")]
    private static void DryRun()
    {
        foreach (string app in Apps)
        {
            var options = Options(app, "-DryRun"); int children = 0;
            int code = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, TextWriter.Null, (name, args) =>
            {
                children++; HostCheck.Equal("OS.QemuRunner", name);
                HostCheck.True(args.Contains("--dry-run") && args.Contains(NativeAotRuntimeSelection.Markers(app).Pass));
                var list = args.ToList(); int at = list.IndexOf("--timeout");
                HostCheck.True(at >= 0 && list[at + 1] == "600");
                return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
            }).GetAwaiter().GetResult();
            HostCheck.Equal(0, code); HostCheck.Equal(1, children);
        }
    }

    [HostTest("nativeaot.runtime original RuntimeSmoke requires explicit M16-010 selection and retains distinct sealed preparation")]
    private static void OriginalRuntimeSmoke()
    {
        const string app = "NativeAotRuntimeSmoke";
        var options = Options("NativeAotRuntimeMatrix", "-LinuxThreadingApplication", app, "-DryRun");
        var plan = QemuProfiles.Create(options);
        HostCheck.Equal(app, NativeAotRuntimeSelection.Application(plan.Flags, options.LinuxThreadingApplication));
        HostCheck.Equal("NativeAotRuntimeMatrix", NativeAotRuntimeSelection.Application(plan.Flags));
        HostCheck.Equal("EnableM16010Probe", NativeAotRuntimeSelection.Flag(app));
        HostCheck.True(plan.Required.Contains("TEST_START m16-010") && plan.Unique.Contains("TEST_PASS m16-010"));
        HostCheck.Equal(600, options.TimeoutSeconds);
        bool rejected = false;
        try { QemuTranscripts.ValidateRun(HostToolProcess.FindRepositoryRoot(), options, plan,
            new(0, "", "", false, false, false, TimeSpan.Zero), TextWriter.Null); }
        catch (ToolFailure error) { rejected = error.Reason == "runtime_prepared_program_required"; }
        HostCheck.True(rejected);
        foreach (string owner in new[] { "NativeAotSuspensionStress", "NativeAotThreadPoolStress" })
            Reject(Options(owner, "-LinuxThreadingApplication", app), "runtime_profile_application");
        Reject(QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16009Probe", "-LinuxThreadingApplication", app,
            "-LinuxThreadingDisassembly", "input.dump"), "threading_application");
        Reject(Options("NativeAotRuntimeMatrix", "-LinuxThreadingApplication", "nativeaotruntimesmoke"), "runtime_profile_application");
        int children = 0;
        int code = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, TextWriter.Null, (name, args) =>
        {
            children++; HostCheck.Equal("OS.QemuRunner", name);
            HostCheck.True(args.Contains("--dry-run") && args.Contains("TEST_PASS m16-010"));
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        }).GetAwaiter().GetResult();
        HostCheck.Equal(0, code); HostCheck.Equal(1, children);
    }

    [HostTest("nativeaot.runtime invalid selections disabled prerequisites and timeouts fail before any child")]
    private static void Selection()
    {
        foreach (string app in Apps)
        {
            Reject(Options(app, "-LinuxThreadingApplication="), "runtime_profile_application");
            Reject(Options(app, "-LinuxThreadingApplication", "NativeAotSuspensionSmoke"), "runtime_profile_application");
            Reject(Options(app, "-LinuxThreadingDisassembly="), "threading_disassembly_required");
            Reject(Options(app, "-LinuxHelloDisassembly", "hello.dump"), "threading_hello_conflict");
            Reject(Options(app, "-EnableM16009Probe=false"), "runtime_explicit_false_prerequisite");
            Reject(Options(app, "-TimeoutSeconds", "901"), "qemu_timeout_out_of_range");
            Reject(Options(app, "-TimeoutSeconds", "0"), "qemu_timeout_out_of_range");
            foreach (int timeout in new[] { 60, 900 })
            {
                var options = Options(app, "-TimeoutSeconds", timeout.ToString(System.Globalization.CultureInfo.InvariantCulture));
                QemuProfiles.Create(options); HostCheck.Equal(timeout, options.TimeoutSeconds);
            }
            foreach (string other in Apps.Where(other => other != app))
            {
                Reject(Options(app, "-" + NativeAotRuntimeSelection.Flag(other)), "runtime_profile_conflict");
                Reject(Options(app, "-LinuxThreadingApplication", other), "runtime_profile_application");
            }
            Reject(QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16009Probe", "-LinuxThreadingApplication", app,
                "-LinuxThreadingDisassembly", "input.dump"), "threading_application");
        }
        Reject(QemuSmokeOptions.Parse("-TimeoutSeconds", "301"), "qemu_timeout_out_of_range");
    }

    [HostTest("nativeaot.runtime complete caller requires sealed matching preparation before opening any transcript")]
    private static void PreparedBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string app in Apps)
        {
            var options = Options(app); var plan = QemuProfiles.Create(options); bool rejected = false;
            try { QemuTranscripts.ValidateRun(root, options, plan, new(0, "", "", false, false, false, TimeSpan.Zero), TextWriter.Null); }
            catch (ToolFailure error) { rejected = error.Reason == "runtime_prepared_program_required"; }
            HostCheck.True(rejected);
        }
        foreach (string name in new[] { "QemuSmoke.cs", "QemuTranscripts.Run.cs", "QemuTranscripts.M03.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, "tools/OS.Tooling", name));
            HostCheck.True(!source.Contains("InspectNativeAotRuntimeProtocolForTests", StringComparison.Ordinal) &&
                !source.Contains("InspectNativeAotRuntimeEnvelopeForTests", StringComparison.Ordinal));
        }
    }

    [HostTest("nativeaot.runtime wrapper selects canonical runtime admission before any child and releases rejected input locks")]
    private static void Inputs()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "runtime-profile-inputs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string package = Path.Combine(directory, "mechanical.pkg"), dump = Path.Combine(directory, "input.dump");
        byte[] bytes = LinuxSignalProofFixture.CreatePackage("AlternateStack");
        try
        {
            File.WriteAllBytes(package, bytes); File.WriteAllText(dump, "headers only");
            foreach (string app in Apps)
            {
                var options = Options(app); options.LinuxElfPackage = package; options.LinuxThreadingDisassembly = dump;
                Reject(options, "runtime_package_identity", false);
                options.Image = dump; Reject(options, "threading_input_output_alias", false);
                options.Image = "artifacts/images/os.raw"; options.LinuxThreadingDisassembly = package;
                Reject(options, "threading_input_alias", false);
                using var p = new FileStream(package, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var d = new FileStream(dump, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                HostCheck.Equal((long)bytes.Length, p.Length);
            }
        }
        finally { File.Delete(package); File.Delete(dump); Directory.Delete(directory); }
    }

    private static void Reject(QemuSmokeOptions options, string reason, bool dryRun = true)
    {
        int children = 0; using StringWriter output = new(); options.DryRun = dryRun;
        int result = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (_, _) =>
        { children++; return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero)); }).GetAwaiter().GetResult();
        HostCheck.True(result != 0); HostCheck.Equal(0, children); HostCheck.True(output.ToString().Contains(reason, StringComparison.Ordinal), reason + " actual=" + output);
    }
}
