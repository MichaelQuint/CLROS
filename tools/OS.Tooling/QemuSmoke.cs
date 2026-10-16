using System.Globalization;

namespace OS.Tooling;

public static class QemuSmoke
{
    public static async Task<int> RunAsync(string root, QemuSmokeOptions options, TextWriter output,
        Func<string, IReadOnlyList<string>, Task<ProcessResult>>? invoke = null, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        invoke ??= (name, args) => ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, name), .. args], root)
        { Timeout = name == "OS.QemuRunner" && NativeAotRuntimeSelection.Application(options.Flags).Length != 0
            ? TimeSpan.FromSeconds(options.TimeoutSeconds + 60) : TimeSpan.FromMinutes(name == "OS.Tooling" ? 30 : 10),
            OutputCharacterLimit = 16 * 1024 * 1024 }, cancellation);
        async Task<ProcessResult> Run(string name, IReadOnlyList<string> args)
        {
            var result = await invoke(name, args);
            output.Write(result.StandardOutput);
            if (result.StandardError.Length != 0) output.Write(result.StandardError);
            QemuTranscripts.RequireCompleteProcess(result, "qemu_child_incomplete tool=" + name);
            return result;
        }
        QemuCaptureSession? capture = null;
        FileStream? linuxInput = null;
        FileStream? helloDisassemblyInput = null;
        FileStream? threadingDisassemblyInput = null;
        NativeAotHelloProgram? helloProgram = null;
        NativeAotThreadingProgram? threadingProgram = null;
        NativeAotRuntimeProgram? runtimeProgram = null;
        try
        {
            QemuSmokePlan plan = QemuProfiles.Create(options, output.WriteLine);
            if (options.LinuxHelloDisassembly.Length != 0 && !plan.IsEnabled("EnableM16009Probe"))
                throw new ToolFailure("hello_profile_requires_m16_009");
            if (plan.IsEnabled("EnableM16002Probe") && !options.DryRun)
            {
                linuxInput = new FileStream(Path.GetFullPath(options.LinuxElfPackage, root), FileMode.Open, FileAccess.Read, FileShare.Read);
                if (linuxInput.Length < 64 || linuxInput.Length > OS.Manifest.LinuxElfPackage.MaximumBytes) throw new ToolFailure("m16_002_package_input_size");
                if (plan.IsEnabled("EnableM16009Probe"))
                {
                    if(options.HasThreadingSelection)
                    {
                        string package=Path.GetFullPath(options.LinuxElfPackage,root), disassembly=Path.GetFullPath(options.LinuxThreadingDisassembly,root);
                        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
                        foreach(string path in new[]{options.Image,options.EfiArtifact,options.SerialLog,options.SummaryLog,
                            options.SummaryLog+".stderr.log",options.SerialLog+".pcap",options.M01RawObservationLog})
                            if(path.Length!=0 && (string.Equals(package,Path.GetFullPath(path,root),comparison) ||
                                string.Equals(disassembly,Path.GetFullPath(path,root),comparison)))throw new ToolFailure("threading_input_output_alias");
                        if(string.Equals(package,disassembly,comparison))throw new ToolFailure("threading_input_alias");
                        threadingDisassemblyInput=new FileStream(disassembly,FileMode.Open,FileAccess.Read,FileShare.Read);
                        if (NativeAotRuntimeSelection.Application(plan.Flags).Length != 0)
                            runtimeProgram=NativeAotRuntimeProgram.Read(root,options.LinuxThreadingApplication,package,disassembly);
                        else threadingProgram=NativeAotThreadingProgram.Read(root,options.LinuxThreadingApplication,package,disassembly);
                    }
                    else if (options.LinuxHelloDisassembly.Length != 0)
                    {
                        string disassembly = Path.GetFullPath(options.LinuxHelloDisassembly, root);
                        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                        foreach (string path in new[] { options.Image, options.EfiArtifact, options.SerialLog, options.SummaryLog,
                            options.SummaryLog + ".stderr.log", options.M01RawObservationLog, options.LinuxElfPackage })
                            if (path.Length != 0 && string.Equals(disassembly, Path.GetFullPath(path, root), comparison))
                                throw new ToolFailure("hello_disassembly_output_alias");
                        helloDisassemblyInput = new FileStream(disassembly, FileMode.Open, FileAccess.Read, FileShare.Read);
                        helloProgram = NativeAotHelloProgram.Read(root, Path.GetFullPath(options.LinuxElfPackage, root),
                            disassembly);
                    }
                    else LinuxSignalProofFixture.RequirePackage(linuxInput);
                }
                else if (plan.IsEnabled("EnableM16008Probe")) LinuxThreadProofFixture.RequirePackage(linuxInput);
            }
            output.WriteLine("QEMU_SCRIPT_BOOT_POLICY policy=" + plan.BootPolicy);
            output.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
            output.WriteLine("TEST_PASS workload.profile.tooling " + (options.WorkloadProfile == "SingleMinimalApi" ? "default single" : "propagates multi"));
            output.WriteLine("QEMU_SCRIPT_PROFILE required=" + plan.Required.Count + " ordered=" + plan.Ordered.Count + " unique=" + plan.Unique.Count + " forbidden=" + plan.Forbidden.Count);
            if (plan.Presets.Count != 0) output.WriteLine("QEMU_MARKER_PRESETS " + string.Join(';', plan.Presets));
            string gptBootImage = options.GptImageVariant == "PrimaryHeaderCrcFault" ? options.Image + ".boot.raw" : "";
            if (gptBootImage.Length != 0)
            {
                string boot = Path.GetFullPath(gptBootImage, root);
                var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                foreach (string path in new[] { options.Image, options.EfiArtifact, options.SerialLog, options.SummaryLog,
                    options.SummaryLog + ".stderr.log", options.Qemu, options.OvmfCode, options.M01RawObservationLog })
                    if (path.Length != 0 && comparer.Equals(boot, Path.GetFullPath(path, root)))
                        throw new ToolFailure("gpt_boot_image_path_alias");
            }
            string qemu = options.Qemu, ovmf = options.OvmfCode;
            if (!options.DryRun)
            {
                output.WriteLine("TEST_START qemu.smoke.preflight");
                qemu = ResolveExecutable(options.Qemu) ?? "";
                ovmf = ResolveOvmf(options.OvmfCode, qemu) ?? "";
                output.WriteLine("QEMU_SCRIPT_PREFLIGHT_QEMU requested=" + options.Qemu + " result=" + (qemu.Length == 0 ? "not_found" : qemu));
                output.WriteLine("QEMU_SCRIPT_PREFLIGHT_OVMF requested=" + options.OvmfCode + " result=" + (ovmf.Length == 0 ? "not_found" : ovmf));
                if (qemu.Length == 0 || ovmf.Length == 0)
                {
                    string reason = string.Join(' ', new[] { qemu.Length == 0 ? "qemu_not_found" : "", ovmf.Length == 0 ? "ovmf_not_found" : "" }.Where(x => x.Length != 0));
                    output.WriteLine("TEST_FAIL qemu.smoke.preflight " + reason);
                    output.WriteLine("TEST_FAIL qemu.smoke " + reason);
                    return qemu.Length == 0 ? 3 : 6;
                }
                output.WriteLine("TEST_PASS qemu.smoke.preflight");
                if (plan.BuildEfi)
                {
                    var build = await Run("OS.Tooling", ["efi", "build", "--boot-policy", plan.BootPolicy, "--efi-output-path", options.EfiArtifact,
                        .. plan.Flags.Where(p => p.Value).Select(p => "-" + p.Key)]);
                    if (build.ExitCode != 0) return build.ExitCode;
                }
                if (!options.SkipImageBuild)
                {
                    string[] package = plan.IsEnabled("EnableM16002Probe") ? ["--package", options.LinuxElfPackage] : [];
                    var image = await Run("OS.ImageBuilder", ["create-raw", "--workload-profile", options.WorkloadProfile, "--efi", options.EfiArtifact, "--output", options.Image, .. package]);
                    if (image.ExitCode != 0) return image.ExitCode;
                }
                if (plan.IsEnabled("EnableM06FatReadProbe"))
                {
                    var receipt = FatImageFixture.Apply(Path.GetFullPath(options.Image, root), Path.GetFullPath(options.EfiArtifact, root));
                    output.WriteLine("M06_FAT_IMAGE_FIXTURE " + System.Text.Json.JsonSerializer.Serialize(receipt));
                }
                if (plan.IsEnabled("EnableM16002Probe") && options.LinuxElfImageVariant != "Intact")
                {
                    var receipt = LinuxElfImageFixture.Apply(Path.GetFullPath(options.Image, root), Path.GetFullPath(options.EfiArtifact, root), Path.GetFullPath(options.LinuxElfPackage, root), options.LinuxElfImageVariant);
                    output.WriteLine("M16_002_IMAGE_FIXTURE " + System.Text.Json.JsonSerializer.Serialize(receipt));
                }
                if (plan.IsEnabled("EnableM06PackageReadProbe"))
                {
                    var receipt = PackageImageFixture.Apply(Path.GetFullPath(options.Image, root), Path.GetFullPath(options.EfiArtifact, root), options.PackageImageVariant);
                    output.WriteLine("M06_PACKAGE_IMAGE_FIXTURE " + System.Text.Json.JsonSerializer.Serialize(receipt));
                }
                if (options.GptImageVariant == "PrimaryHeaderCrcFault")
                {
                    File.Copy(Path.GetFullPath(options.Image, root), Path.GetFullPath(gptBootImage, root), true);
                    var receipt = GptImageFixture.ApplyPrimaryHeaderCrcFault(Path.GetFullPath(options.Image, root));
                    using var boot = File.OpenRead(Path.GetFullPath(gptBootImage, root));
                    if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(boot)) != receipt.OriginalSha256)
                        throw new ToolFailure("gpt_boot_image_identity");
                    output.WriteLine("M06_GPT_BOOT_IMAGE path=" + gptBootImage + " sha256=" + receipt.OriginalSha256);
                    output.WriteLine("M06_GPT_IMAGE_FAULT " + System.Text.Json.JsonSerializer.Serialize(receipt));
                }
                int preflight = ValidateImage(Path.GetFullPath(options.Image, root), options.DiskFormat, output, options.Image);
                if (preflight != 0) return preflight;
            }
            int timeout = options.TimeoutSeconds;
            bool rawCapture = plan.IsEnabled("EnableM01OwnedPagingProbe") && !new[] { "M02OwnedGdtTssTerminal", "M02Ud2VectorState", "M02PageFaultCr2ErrorCode", "M02DoubleFaultIst", "M02UnexpectedVectorPanic" }.Any(x => plan.IsEnabled("Enable" + x + "Probe"));
            if (rawCapture) output.WriteLine("M01_RAW_OBSERVATION_CONFIG enabled=true active=" + (!options.DryRun).ToString().ToLowerInvariant() + " dryRun=" + options.DryRun.ToString().ToLowerInvariant());
            if (!options.DryRun && rawCapture)
            {
                output.WriteLine("TEST_START m01.026.raw-observations");
                capture = QemuCaptureSession.Create(root, qemu, options.M01RawObservationLog, options.SerialLog, options.SummaryLog, options.TimeoutSeconds, plan.IsEnabled("EnableM02SafeStubProbe"));
                qemu = capture.Executable; timeout = capture.RunnerTimeoutSeconds;
                output.WriteLine("M01_RAW_OBSERVATION_PATH " + capture.ObservationPath);
                output.WriteLine("M01_RAW_OBSERVATION_SUPPORT " + capture.SupportDirectory);
                output.WriteLine("M01_RAW_OBSERVATION_TIMEOUTS guestSeconds=" + capture.GuestTimeoutSeconds + " captureSeconds=" + QemuCaptureSession.CaptureTimeoutSeconds + " runnerSeconds=" + capture.RunnerTimeoutSeconds);
                output.WriteLine("M01_RAW_OBSERVATION_IDENTITIES " + string.Join(' ', capture.Identities.Select(p => p.Key + "=" + p.Value)));
            }
            List<string> arguments = ["--image", options.Image, "--disk-format", options.DiskFormat, "--workload-profile", options.WorkloadProfile,
                "--serial-log", options.SerialLog, "--summary-log", options.SummaryLog, "--timeout", timeout.ToString(CultureInfo.InvariantCulture)];
            arguments.AddRange(["--startup-timeout", options.StartupTimeoutSeconds.ToString(CultureInfo.InvariantCulture)]);
            if (plan.ReplaceDefaultRequiredMarkers) arguments.Add("--replace-default-required-markers");
            if (plan.IsEnabled("EnableM02UnexpectedVectorPanicProbe")) arguments.Add("--expect-unexpected-vector-panic");
            if (plan.IsEnabled("EnableM03OwnedSubsystemPanicProbe")) arguments.Add("--expect-owned-subsystem-panic");
            if (plan.IsEnabled("EnableM04UnhandledExceptionProbe")) arguments.Add("--expect-unhandled-exception");
            if (plan.IsEnabled("EnableM03FirmwareEntropySeedProbe") || plan.IsEnabled("EnableM16004Probe")) arguments.AddRange(["--firmware-rng-source", options.FirmwareRngSource]);
            if (plan.IsEnabled("EnableM05BarDmaProbe")) arguments.Add("--pci-bar-test-device");
            if (plan.IsEnabled("EnableM05VirtioNegotiationProbe")) arguments.Add("--virtio-negotiation-device");
            if (plan.IsEnabled("EnableM07VirtioNetProbe")) arguments.AddRange(["--ethernet-capture", Path.GetFullPath(options.SerialLog + ".pcap", root)]);
            if (plan.IsEnabled("EnableM07ArpReceiveProbe")) arguments.Add("--arp-receive-fixture");
            if (plan.IsEnabled("EnableM07026Probe")) arguments.Add("--continuing-network-peer");
            if (plan.IsEnabled("EnableM06VirtioBlockQueueSetupProbe")) arguments.Add("--virtio-block-queue-device");
            if (gptBootImage.Length != 0) arguments.AddRange(["--virtio-block-boot-image", gptBootImage]);
            // The runner adds every ordered marker to its required list. Avoid
            // sending an identical list twice: long profiles otherwise exceed
            // Windows' command-line limit without adding a requirement.
            IReadOnlyList<string> required = plan.Required.SequenceEqual(plan.Ordered) ? [] : plan.Required;
            foreach (var (name, values) in new[] { ("--require-marker", required), ("--require-ordered-marker", plan.Ordered), ("--require-unique-marker", plan.Unique), ("--forbid-marker", plan.Forbidden), ("--marker-preset", plan.Presets) })
                foreach (string value in values) arguments.AddRange([name, value]);
            if (qemu.Length != 0) arguments.AddRange(["--qemu", qemu]);
            if (ovmf.Length != 0) arguments.AddRange(["--ovmf-code", ovmf]);
            if (options.DryRun) arguments.Add("--dry-run");
            ProcessResult runner;
            if (capture is null) runner = await Run("OS.QemuRunner", arguments);
            else
            {
                runner = await ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. arguments], root)
                    { Timeout = TimeSpan.FromSeconds(timeout + 30), EnvironmentOverrides = capture.Environment }, cancellation);
                output.Write(runner.StandardOutput); output.Write(runner.StandardError);
                QemuTranscripts.RequireCompleteProcess(runner, "qemu_capture_runner_incomplete");
            }
            if (options.DryRun) return runner.ExitCode;
            if (runner.ExitCode != 0 && capture is not null) return runner.ExitCode;
            // Hold the exact public evidence before the capture hash check and
            // through every subsequent parent/leaf/runner read.
            using AuthorityFiles? threadingEvidence = threadingProgram is null && runtimeProgram is null ? null : QemuTranscripts.HoldThreadingEvidence(root, options);
            if (capture is not null)
            {
                capture.Validate(); output.WriteLine("TEST_PASS m01.026.raw-observations");
            }
            int validated = QemuTranscripts.ValidateRun(root, options, plan, runner, output, helloProgram, threadingProgram, runtimeProgram);
            threadingEvidence?.VerifyUnchanged();
            return validated;
        }
        catch (Exception error) when (error is ToolFailure or ArgumentException or IOException or InvalidDataException or InvalidOperationException)
        { output.WriteLine("TEST_FAIL qemu.smoke " + error.Message); return 1; }
        finally { capture?.Dispose(); linuxInput?.Dispose(); helloDisassemblyInput?.Dispose(); threadingDisassemblyInput?.Dispose(); }
    }

    public static string? ResolveExecutable(string? requested)
    {
        string name = string.IsNullOrWhiteSpace(requested) ? Environment.GetEnvironmentVariable("QEMU") ?? "qemu-system-x86_64" : requested;
        if (string.IsNullOrWhiteSpace(name)) name = "qemu-system-x86_64";
        if (Path.IsPathRooted(name) || name.Contains('\\') || name.Contains('/'))
            return File.Exists(name) ? Path.GetFullPath(name) : null;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            foreach (string filename in new[] { name, name + ".exe", name + ".cmd", name + ".bat" })
            {
                string candidate = Path.Combine(directory.Trim('"'), filename);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        return new[] { @"C:\Program Files\qemu\qemu-system-x86_64.exe", @"C:\Program Files (x86)\qemu\qemu-system-x86_64.exe",
            @"C:\ProgramData\chocolatey\bin\qemu-system-x86_64.exe", @"C:\msys64\mingw64\bin\qemu-system-x86_64.exe" }.FirstOrDefault(File.Exists);
    }

    public static string? ResolveOvmf(string? requested, string qemuPath)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return File.Exists(requested) ? Path.GetFullPath(requested) : null;
        string[] names = ["OVMF_CODE.fd", "OVMF.fd", "edk2-x86_64-code.fd", "edk2-x86_64-secure-code.fd", "OVMF_CODE_4M.fd"];
        List<string> candidates = [];
        foreach (string variable in new[] { "OS_OVMF_CODE", "OVMF_CODE", "QEMU_OVMF_CODE" })
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)) candidates.Add(value);
        List<string> directories = [];
        if (!string.IsNullOrWhiteSpace(qemuPath))
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(qemuPath))!;
            directories.AddRange([directory, Path.Combine(directory, "share"), Path.Combine(directory, "..", "share", "qemu")]);
        }
        directories.AddRange([@"C:\Program Files\qemu", @"C:\Program Files\qemu\share", @"C:\Program Files (x86)\qemu",
            @"C:\ProgramData\chocolatey\lib\qemu\tools", @"C:\msys64\mingw64\share\qemu", "/usr/share/OVMF", "/usr/share/edk2/ovmf", "/usr/share/qemu"]);
        foreach (string directory in directories)
            foreach (string name in names)
                candidates.Add(Path.Combine(directory, name));
        foreach (string path in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            if (File.Exists(path)) return path;
        return null;
    }
    public static int ValidateImage(string path, string format, TextWriter output, string? displayPath = null)
    {
        displayPath ??= path;
        if (!File.Exists(path)) { output.WriteLine("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + displayPath + " exists=false bytes=0"); output.WriteLine("TEST_FAIL qemu.smoke image_not_found path=" + displayPath); return 2; }
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        output.WriteLine("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + displayPath + " exists=true bytes=" + stream.Length);
        if (stream.Length == 0) { output.WriteLine("TEST_FAIL qemu.smoke image_empty path=" + displayPath); return 11; }
        Span<byte> header = stackalloc byte[4];
        string detected = stream.Read(header) == 4 && header.SequenceEqual(new byte[] { 0x51, 0x46, 0x49, 0xfb }) ? "qcow2" : "raw-compatible";
        output.WriteLine("QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT expected=" + format + " detected=" + detected);
        if (format == "raw" && detected == "qcow2" || format == "qcow2" && detected != "qcow2")
        { output.WriteLine("TEST_FAIL qemu.smoke disk_format_mismatch expected=" + format + " detected=" + detected); return 12; }
        return 0;
    }
}
