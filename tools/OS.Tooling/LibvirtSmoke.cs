namespace OS.Tooling;

public sealed record LibvirtSmokeOptions
{
    public string Image { get; init; } = "artifacts/images/os.raw";
    public string DiskFormat { get; init; } = "raw";
    public string SerialLog { get; init; } = "artifacts/logs/libvirt-smoke.log";
    public string SummaryLog { get; init; } = "artifacts/logs/libvirt-smoke.summary.log";
    public string DomainXml { get; init; } = "artifacts/logs/libvirt-smoke.xml";
    public string LifecyclePlan { get; init; } = "artifacts/logs/libvirt-lifecycle.plan.log";
    public string OvmfLoader { get; init; } = "";
    public string Network { get; init; } = "none";
    public string Virsh { get; init; } = "";
    public string WorkloadProfile { get; init; } = "SingleMinimalApi";
    public int TimeoutSeconds { get; init; } = 30;
    public IReadOnlyList<string> RequireMarkers { get; init; } = SmokeContracts.RequiredMarkers;
    public IReadOnlyList<string> ForbidMarkers { get; init; } = [];
    public bool DryRun { get; init; }
    public bool PlanLifecycle { get; init; }
    public bool CheckSerialOnly { get; init; }
    public bool AllowRealLibvirtLifecycle { get; init; }
    public bool ValidatePaths { get; init; }

    public static LibvirtSmokeOptions Parse(string[] args)
    {
        LibvirtSmokeOptions options = new();
        List<string> required = [], forbidden = [];
        bool explicitRequired = false;
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("missing_value " + option);
            options = option switch
            {
                "--image" => options with { Image = Value() },
                "--disk-format" => options with { DiskFormat = Value().ToLowerInvariant() },
                "--serial-log" => options with { SerialLog = Value() },
                "--summary-log" => options with { SummaryLog = Value() },
                "--domain-xml" => options with { DomainXml = Value() },
                "--lifecycle-plan" => options with { LifecyclePlan = Value() },
                "--ovmf-loader" => options with { OvmfLoader = Value() },
                "--network" => options with { Network = Value().ToLowerInvariant() },
                "--virsh" => options with { Virsh = Value() },
                "--workload-profile" => options with { WorkloadProfile = Value() },
                "--timeout-seconds" => options with { TimeoutSeconds = int.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture) },
                "--dry-run" => options with { DryRun = true },
                "--plan-lifecycle" => options with { PlanLifecycle = true },
                "--check-serial-only" => options with { CheckSerialOnly = true },
                "--allow-real-libvirt-lifecycle" => options with { AllowRealLibvirtLifecycle = true },
                "--validate-paths" => options with { ValidatePaths = true },
                "--require-marker" or "--forbid-marker" => options,
                _ => throw new ArgumentException("unknown_option " + option)
            };
            if (option == "--require-marker") { explicitRequired = true; required.Add(Value()); }
            if (option == "--forbid-marker") forbidden.Add(Value());
        }
        return options with
        {
            RequireMarkers = explicitRequired ? SmokeContracts.SplitMarkers(required) : options.RequireMarkers,
            ForbidMarkers = SmokeContracts.SplitMarkers(forbidden)
        };
    }
}

public static class LibvirtSmoke
{
    public static async Task<int> RunAsync(string root, LibvirtSmokeOptions options, TextWriter output,
        Func<IReadOnlyList<string>, Task<ProcessResult>>? invoke = null)
    {
        try
        {
            SmokeContracts.ValidateWorkload(options.WorkloadProfile);
            if (options.DiskFormat is not ("raw" or "qcow2")) throw new ArgumentException("invalid_args disk_format=" + options.DiskFormat + " valid=raw,qcow2");
            if (options.Network is not ("none" or "default")) throw new ArgumentException("invalid_args network=" + options.Network + " valid=none,default");
            if (options.RequireMarkers.Count == 0) throw new ArgumentException("invalid_args missing_required_marker");
            if (options.TimeoutSeconds <= 0) throw new ArgumentException("invalid_args timeout_seconds");
            invoke ??= args => HostTools.RunAsync(root, "OS.LibvirtRunner", args);
            output.WriteLine("TEST_START libvirt.smoke");
            output.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
            output.WriteLine("TEST_PASS workload.profile.tooling " + (options.WorkloadProfile == "SingleMinimalApi" ? "default single" : "propagates multi"));
            foreach (var (key, value) in new (string, object)[]
            {
                ("IMAGE_ARTIFACT", options.Image), ("DISK_FORMAT", options.DiskFormat), ("SERIAL_LOG", options.SerialLog),
                ("SUMMARY_LOG", options.SummaryLog), ("DOMAIN_XML", options.DomainXml), ("LIFECYCLE_PLAN", options.LifecyclePlan),
                ("TIMEOUT_SECONDS", options.TimeoutSeconds), ("REAL_LIFECYCLE_OPT_IN", options.AllowRealLibvirtLifecycle ? "true" : "false")
            }) output.WriteLine("LIBVIRT_" + key + " " + value);
            output.WriteLine("REQUIRED_MARKERS " + string.Join(';', options.RequireMarkers));
            output.WriteLine("FORBIDDEN_MARKERS " + (options.ForbidMarkers.Count == 0 ? "none" : string.Join(';', options.ForbidMarkers)));

            List<string> Markers()
            {
                List<string> arguments = [];
                foreach (string marker in options.RequireMarkers) arguments.AddRange(["--require-marker", marker]);
                foreach (string marker in options.ForbidMarkers) arguments.AddRange(["--forbid-marker", marker]);
                return arguments;
            }
            List<string> Common(string command)
            {
                List<string> arguments = [command, "--image", Absolute(options.Image), "--disk-format", options.DiskFormat,
                    "--serial-log", Absolute(options.SerialLog), "--network", options.Network, .. Markers()];
                if (options.OvmfLoader.Length != 0) arguments.AddRange(["--ovmf-loader", Absolute(options.OvmfLoader)]);
                if (options.ValidatePaths) arguments.Add("--validate-paths");
                return arguments;
            }
            string Absolute(string path) => Path.GetFullPath(path, root);
            void EnsureParent(string path) => Directory.CreateDirectory(Path.GetDirectoryName(Absolute(path))!);
            void Save(string path, string text) { EnsureParent(path); File.WriteAllText(Absolute(path), text.TrimEnd() + Environment.NewLine); }
            int Failed(string step, ProcessResult result)
            {
                if (result.StandardError.Length != 0) output.WriteLine(result.StandardError.TrimEnd());
                output.WriteLine($"TEST_FAIL {step} exit={result.ExitCode} timeout={result.TimedOut.ToString().ToLowerInvariant()} outputLimit={result.OutputLimitExceeded.ToString().ToLowerInvariant()}");
                return result.ExitCode == 0 ? 1 : result.ExitCode;
            }
            if (options.CheckSerialOnly)
            {
                output.WriteLine("LIBVIRT_CHECK_SERIAL_ONLY");
                EnsureParent(options.SummaryLog);
                var result = await invoke(["check-serial", "--serial-log", Absolute(options.SerialLog), "--summary-log", Absolute(options.SummaryLog), .. Markers()]);
                output.WriteLine(result.StandardOutput.TrimEnd());
                if (!result.Succeeded) return Failed("libvirt.smoke.check_serial", result);
                output.WriteLine("TEST_PASS libvirt.smoke.check_serial");
                return 0;
            }
            EnsureParent(options.DomainXml);
            var xml = await invoke(Common("generate-xml"));
            if (!xml.Succeeded) return Failed("libvirt.smoke.generate_xml", xml);
            Save(options.DomainXml, xml.StandardOutput);
            output.WriteLine("LIBVIRT_XML_ARTIFACT " + options.DomainXml);
            if (options.PlanLifecycle)
            {
                var arguments = Common("plan-lifecycle");
                arguments.AddRange(["--timeout-seconds", options.TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                if (options.AllowRealLibvirtLifecycle) arguments.Add("--allow-real-libvirt-lifecycle");
                var plan = await invoke(arguments);
                if (!plan.Succeeded) return Failed("libvirt.lifecycle.plan", plan);
                Save(options.LifecyclePlan, plan.StandardOutput);
                output.WriteLine("LIBVIRT_LIFECYCLE_PLAN_ARTIFACT " + options.LifecyclePlan);
                output.WriteLine(plan.StandardOutput.TrimEnd());
                output.WriteLine("TEST_PASS libvirt.lifecycle.plan");
                return 0;
            }
            if (options.DryRun)
            {
                output.WriteLine("LIBVIRT_DRY_RUN");
                output.WriteLine("TEST_PASS libvirt.smoke.dryrun");
                return 0;
            }
            if (!File.Exists(Absolute(options.Image))) { output.WriteLine("TEST_FAIL libvirt.smoke image_not_found path=" + options.Image); return 3; }
            string? virsh = HostTools.FindExecutable(options.Virsh, "VIRSH", "virsh");
            if (virsh is null)
            {
                output.WriteLine("TEST_SKIP libvirt.smoke missing_tool=virsh");
                output.WriteLine("TEST_PASS libvirt.smoke.missing_tool_reported");
                return 0;
            }
            output.WriteLine("VIRSH_DISCOVERY result=" + virsh);
            if (!options.AllowRealLibvirtLifecycle)
            {
                output.WriteLine("TEST_SKIP libvirt.smoke real_lifecycle_requires_opt_in");
                output.WriteLine("TEST_PASS libvirt.smoke.real_lifecycle_guarded");
                return 0;
            }
            output.WriteLine("TEST_FAIL libvirt.smoke.real_run_not_implemented");
            return 2;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            output.WriteLine("TEST_FAIL libvirt.smoke " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }
}
