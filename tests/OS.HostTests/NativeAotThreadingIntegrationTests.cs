using OS.Tooling;

internal static class NativeAotThreadingIntegrationTests
{
    [HostTest("nativeaot.threading wrapper requires paired exact application options and the signal profile before any child")]
    private static void Selection()
    {
        var parsed = QemuSmokeOptions.Parse("--linux-threading-application", "NativeAotSuspensionSmoke", "--linux-threading-disassembly", "artifacts/input.dump");
        HostCheck.Equal("NativeAotSuspensionSmoke", parsed.LinuxThreadingApplication);
        HostCheck.Equal("artifacts/input.dump", parsed.LinuxThreadingDisassembly);
        foreach (var (arguments, reason) in new (string[], string)[]
        {
            (["-LinuxThreadingApplication","nativeaotsuspensionsmoke","-LinuxThreadingDisassembly","a"],"threading_application"),
            (["-LinuxThreadingApplication="],"threading_application"),
            (["-LinuxThreadingDisassembly","a"],"threading_application"),
            (["-LinuxThreadingApplication","NativeAotSuspensionSmoke"],"threading_disassembly_required"),
            (["-LinuxThreadingApplication","NativeAotThreadPoolSmoke","-LinuxThreadingDisassembly="],"threading_disassembly_required"),
            (["-LinuxThreadingApplication","NativeAotSuspensionSmoke","-LinuxThreadingDisassembly","a"],"threading_profile_requires_m16_009"),
            (["-BuildEfi","-EnableM16009Probe","-LinuxThreadingApplication","NativeAotThreadPoolSmoke","-LinuxThreadingDisassembly","a","-LinuxHelloDisassembly","b"],"threading_hello_conflict")
        })
            RejectBeforeChild(QemuSmokeOptions.Parse([.. arguments, "-DryRun"]), reason);
    }

    [HostTest("nativeaot.threading wrapper rejects substituted packages and every input output alias then releases input locks")]
    private static void Inputs()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "threading-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string package = Path.Combine(directory, "mechanical.pkg"), dump = Path.Combine(directory, "input.dump");
            byte[] original = LinuxSignalProofFixture.CreatePackage("AlternateStack");
            File.WriteAllBytes(package, original);
            File.WriteAllText(dump, "headers only");
            foreach (string input in new[] { package, dump })
                foreach (int alias in Enumerable.Range(0, 8))
                {
                    var options = Options(package, dump);
                    switch (alias)
                    {
                        case 0:
                            options.Image = input;
                            break;
                        case 1:
                            options.EfiArtifact = input;
                            break;
                        case 2:
                            options.SerialLog = input;
                            break;
                        case 3:
                            options.SummaryLog = input;
                            break;
                        case 4:
                            options.M01RawObservationLog = input;
                            break;
                        case 5:
                            options.SummaryLog = input[..^4];
                            File.Copy(input, options.SummaryLog + ".stderr.log");
                            break;
                        case 6:
                            options.SerialLog = input[..^4];
                            File.Copy(input, options.SerialLog + ".pcap");
                            break;
                        case 7:
                            options.LinuxThreadingDisassembly = package;
                            break;
                    }
                    // Derived stderr/pcap aliases use an input whose actual path is
                    // exactly the output spelling; all other cases use originals.
                    if (alias == 5)
                    {
                        if (input == package)
                            options.LinuxElfPackage = options.SummaryLog + ".stderr.log";
                        else
                            options.LinuxThreadingDisassembly = options.SummaryLog + ".stderr.log";
                    }
                    if (alias == 6)
                    {
                        if (input == package)
                            options.LinuxElfPackage = options.SerialLog + ".pcap";
                        else
                            options.LinuxThreadingDisassembly = options.SerialLog + ".pcap";
                    }
                    RejectBeforeChild(options, alias == 4 ? "m16_002_requires_build_and_original_capture" :
                        alias == 7 ? "threading_input_alias" : "threading_input_output_alias");
                    if (alias is 5 or 6)
                        File.Delete(alias == 5 ? options.SummaryLog + ".stderr.log" : options.SerialLog + ".pcap");
                }
            RejectBeforeChild(Options(package, dump), "threading_package_identity");
            HostCheck.True(File.ReadAllBytes(package).AsSpan().SequenceEqual(original));
            HostCheck.Equal("headers only", File.ReadAllText(dump));
            using FileStream exclusivePackage = new(package, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using FileStream exclusiveDump = new(dump, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(directory, true); }
    }

    [HostTest("nativeaot.threading dry run preserves signal profile command construction without prepared execution authority")]
    private static void DryRun()
    {
        foreach (string app in new[] { "NativeAotSuspensionSmoke", "NativeAotThreadPoolSmoke" })
        {
            var options = Options("artifacts/absent.pkg", "artifacts/absent.dump");
            options.DryRun = true;
            options.LinuxThreadingApplication = app;
            int children = 0;
            using StringWriter output = new();
            int code = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (name, args) =>
            {
                children++;
                HostCheck.Equal("OS.QemuRunner", name);
                HostCheck.True(args.Contains("--dry-run"));
                HostCheck.True(args.Contains("TEST_PASS m16-009"));
                return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
            }).GetAwaiter().GetResult();
            HostCheck.Equal(0, code);
            HostCheck.Equal(1, children);
        }
    }

    [HostTest("nativeaot.threading full caller requires sealed preparation before transcript files and excludes synthetic inspection")]
    private static void PreparedBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var options = Options("artifacts/absent.pkg", "artifacts/absent.dump");
        var plan = QemuProfiles.Create(options);
        bool rejected = false;
        try
        {
            QemuTranscripts.ValidateRun(root, options, plan, new(0, "", "", false, false, false, TimeSpan.Zero), TextWriter.Null);
        }
        catch (ToolFailure error) { rejected = error.Reason == "threading_prepared_program_required"; }
        HostCheck.True(rejected);
        foreach (string name in new[] { "QemuSmoke.cs", "QemuTranscripts.Run.cs", "QemuTranscripts.M03.cs" })
            HostCheck.True(!File.ReadAllText(Path.Combine(root, "tools/OS.Tooling", name)).Contains("InspectNativeAotThreadingProtocolForTests", StringComparison.Ordinal));
    }

    private static QemuSmokeOptions Options(string package, string dump) => QemuSmokeOptions.Parse("-BuildEfi", "-EnableM16009Probe",
        "-LinuxElfPackage", package, "-LinuxThreadingApplication", "NativeAotSuspensionSmoke", "-LinuxThreadingDisassembly", dump);

    private static void RejectBeforeChild(QemuSmokeOptions options, string reason)
    {
        int children = 0;
        using StringWriter output = new();
        int code = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (_, _) =>
        {
            children++;
            throw new InvalidOperationException("unexpected child");
        }).GetAwaiter().GetResult();
        HostCheck.True(code != 0 && children == 0 && output.ToString().Contains(reason, StringComparison.Ordinal), reason + " actual=" + output);
    }
}
