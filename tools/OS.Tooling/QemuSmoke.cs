using System.Globalization;

namespace OS.Tooling;

public static class QemuSmoke
{
    public static async Task<int> RunAsync(string root, QemuSmokeOptions options, TextWriter output,
        Func<string, IReadOnlyList<string>, Task<ProcessResult>>? invoke = null, CancellationToken cancellation = default,
        Func<ProcessRequest, CancellationToken, Task<ProcessResult>>? invokeRunner = null)
    {
        root = Path.GetFullPath(root);
        bool customInvoke = invoke is not null;
        QemuCaptureSession? capture = null;
        string actualQemuForGate = "";
        TimeSpan DefaultRunnerTimeout() => options.WritableDiskFixture.Length != 0
            ? TimeSpan.FromSeconds(options.TimeoutSeconds + QemuCaptureSession.RunnerCaptureAllowanceSeconds + 30)
            : NativeAotRuntimeSelection.Application(options.Flags).Length != 0 || NativeAotSocketSelection.Application(options.Flags).Length != 0 ||
              NativeAotSocketContainmentSelection.Selected(options.Flags) || NativeAotSocketVectorsSelection.Selected(options.Flags) ||
              NativeAotWebSelection.Application(options.Flags).Length != 0 || NativeAotStaticFileSelection.Application(options.Flags).Length != 0
            ? TimeSpan.FromSeconds(options.TimeoutSeconds + 60) : TimeSpan.FromMinutes(10);
        async Task<ProcessResult> RunOriginalRunner(IReadOnlyList<string> args, TimeSpan timeout,
            IReadOnlyDictionary<string, string?> variables, CancellationToken token, bool legacyHook)
        {
            if (options.DryRun)
                return await invoke!("OS.QemuRunner", args);
            using var startup = WhpxLaunchSession.Create(root, options.SerialLog, actualQemuForGate, options.Accelerator);
            if (startup is not null && customInvoke && legacyHook && invokeRunner is null)
                throw new ToolFailure("whpx_original_runner_transport_required",
                    "Provide the typed ProcessRequest runner hook; preserve its handle/environment and retained join.");
            var request = new ProcessRequest("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. args], root)
            {
                Timeout = timeout,
                OutputCharacterLimit = 16 * 1024 * 1024,
                EnvironmentOverrides = variables,
                RetainOwnershipUntilJoined = options.WritableDiskFixture.Length != 0
            };
            if (startup is not null)
                request = startup.Prepare(request);
            ProcessResult result = invokeRunner is not null ? await invokeRunner(request, token) :
                legacyHook && customInvoke && startup is null ? await invoke!("OS.QemuRunner", args) :
                await ProcessRunner.RunAsync(request, token);
            if (startup is not null)
                result = startup.VerifyJoined(result, request.OutputCharacterLimit);
            return result;
        }
        invoke ??= (name, args) => ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, name), .. args], root)
        {
            RetainOwnershipUntilJoined = name == "OS.QemuRunner" && options.WritableDiskFixture.Length != 0,
            Timeout = name == "OS.QemuRunner" && options.WritableDiskFixture.Length != 0
            ? TimeSpan.FromSeconds(options.TimeoutSeconds + QemuCaptureSession.RunnerCaptureAllowanceSeconds + 30)
            : name == "OS.QemuRunner" && (NativeAotRuntimeSelection.Application(options.Flags).Length != 0 || NativeAotSocketSelection.Application(options.Flags).Length != 0 || NativeAotSocketContainmentSelection.Selected(options.Flags) || NativeAotSocketVectorsSelection.Selected(options.Flags) || NativeAotWebSelection.Application(options.Flags).Length != 0 || NativeAotStaticFileSelection.Application(options.Flags).Length != 0)
            ? TimeSpan.FromSeconds(options.TimeoutSeconds + 60) : TimeSpan.FromMinutes(name == "OS.Tooling" ? 30 : 10),
            OutputCharacterLimit = 16 * 1024 * 1024
        }, cancellation);
        async Task<ProcessResult> Run(string name, IReadOnlyList<string> args)
        {
            var result = name == "OS.QemuRunner"
                ? await RunOriginalRunner(args, DefaultRunnerTimeout(), new Dictionary<string, string?>(), cancellation, true)
                : await invoke(name, args);
            output.Write(result.StandardOutput);
            if (result.StandardError.Length != 0)
                output.Write(result.StandardError);
            if (name == "OS.QemuRunner" && options.WritableDiskFixture.Length != 0)
            {
                if (!result.TerminationConfirmed)
                    throw new ToolFailure("writable_original_runner_unjoined");
            }
            else
                QemuTranscripts.RequireCompleteProcess(result, "qemu_child_incomplete tool=" + name);
            return result;
        }
        FileStream? linuxInput = null;
        FileStream? helloDisassemblyInput = null;
        FileStream? threadingDisassemblyInput = null;
        NativeAotHelloProgram? helloProgram = null;
        NativeAotThreadingProgram? threadingProgram = null;
        NativeAotRuntimeProgram? runtimeProgram = null;
        NativeAotSocketProgram? socketProgram = null;
        NativeAotSocketContainmentProgram? containmentProgram = null;
        NativeAotSocketPeer.Completion? socketPeer = null;
        NativeAotSocketVectorsProgram? vectorsProgram = null;
        NativeAotSocketVectorsPeer.Completion? vectorsPeer = null;
        NativeAotWebProgram? webProgram = null;
        NativeAotStaticFileProgram? staticProgram = null;
        NativeAotStagedStaticPeer.Completion? staticPeer = null;
        NativeAotDiskStaticPeer.Completion? diskStaticPeer = null;
        NativeAotDiskStaticBrowser.Completion? diskBrowser = null;
        NativeAotWebRacePeer.Completion? webRacePeer = null;
        NativeAotServiceProgram? serviceProgram = null;
        NativeAotServicePeer.Completion? servicePeer = null;
        NativeAotServiceQuotaPeer.Completion? serviceQuotaPeer = null;
        int serviceForwardPort = 0;
        DateTimeOffset serviceStarted = default, serviceCompleted = default;
        AuthorityFiles? serviceInputs = null;
        AuthorityFile? servicePackageInput = null, serviceDisassemblyInput = null, serviceImageInput = null, serviceEfiInput = null;
        EfiVerification? serviceNative = null;
        int webForwardPort = 0;
        DateTimeOffset webStarted = default, webCompleted = default;
        NativeAotWebProgram.Configuration webConfiguration = default;
        AuthorityFiles? webInputs = null;
        AuthorityFile? webPackageInput = null, webDisassemblyInput = null, webImageInput = null, webEfiInput = null;
        EfiVerification? webNative = null;
        AuthorityFiles? diskInputs = null;
        AuthorityFile? diskImageInput = null, diskEfiInput = null, diskPackageInput = null, diskReceiptInput = null;
        EfiVerification? diskNative = null;
        WebRootImageFixture.Receipt? diskImageReceipt = null;
        WebRootDiskInventory.VerifiedTruncated? diskTruncated = null;
        CombinedWebDataImageFixture.Receipt? writableReceipt = null;
        CombinedWebDataImageFixture.WorkingImage? writableWorking = null;
        try
        {
            QemuSmokePlan plan = QemuProfiles.Create(options, output.WriteLine);
            // Each admitted race has its own complete original consumer. Never
            // run another variant through the positive59-call consumer.
            if (options.HasDiskRaceSelection && options.LinuxDiskRaceVariant is not ("Progress" or "CloseReuse" or "ExitPending") && !options.DryRun)
                throw new ToolFailure("disk_race_complete_consumer_required");
            bool diskFiles = NativeAotDiskSelection.Selected(plan.Flags);
            bool writableDisk = NativeAotDiskSelection.WritableSelected(plan.Flags);
            bool diskStatic = NativeAotDiskSelection.StaticSelected(plan.Flags);
            if (writableDisk)
                RequireWritableOutputPaths(root, options);
            bool signalEpoll = LinuxSignalEpollSelection.Bind(options, plan.Flags);
            bool sockets = !signalEpoll && NativeAotSocketSelection.Application(plan.Flags).Length != 0;
            bool containment = NativeAotSocketContainmentSelection.Selected(plan.Flags);
            bool vectors = NativeAotSocketVectorsSelection.Selected(plan.Flags);
            bool web = NativeAotWebSelection.Application(plan.Flags).Length != 0;
            bool staticFiles = NativeAotStaticFileSelection.Application(plan.Flags).Length != 0;
            bool service = NativeAotServiceSelection.Application(plan.Flags).Length != 0;
            var serviceContract = service ? NativeAotServiceSelection.Contract(options, plan.Flags) : NativeAotServiceContract.V1;
            if ((signalEpoll || service || sockets || containment || vectors || web || staticFiles || diskFiles) && !options.DryRun)
                foreach (string path in new[] { options.SerialLog, options.SummaryLog, options.SerialLog + ".pcap", options.SerialLog + ".peer.log", options.SerialLog + ".peer.log.stderr.log" })
                    if (Path.Exists(Path.GetFullPath(path, root)))
                        throw new ToolFailure("socket_evidence_not_fresh");
            if (options.HasWebSignalRace && !options.DryRun)
            {
                string serial = Path.GetFullPath(options.SerialLog, root), race = serial + ".race.json";
                string[] targets = [serial,Path.GetFullPath(options.SummaryLog,root),serial+".pcap",race,race+".attempts.json",
                    race+".wire.json",race+".runner.log",race+".runner.stderr.log"];
                var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                string[] originals = [options.LinuxElfPackage, options.LinuxThreadingDisassembly, options.Image, options.EfiArtifact];
                if (targets.Distinct(comparer).Count() != targets.Length || targets.Any(p => Path.Exists(p) ||
                    originals.Any(s => s.Length != 0 && comparer.Equals(p, Path.GetFullPath(s, root)))))
                    throw new ToolFailure("web_race_output_not_fresh_or_aliased");
            }
            if (options.LinuxHelloDisassembly.Length != 0 && !plan.IsEnabled("EnableM16009Probe"))
                throw new ToolFailure("hello_profile_requires_m16_009");
            if (plan.IsEnabled("EnableM16002Probe") && !options.DryRun)
            {
                linuxInput = new FileStream(Path.GetFullPath(options.LinuxElfPackage, root), FileMode.Open, FileAccess.Read, FileShare.Read);
                if (linuxInput.Length < 64 || linuxInput.Length > OS.Manifest.LinuxElfPackage.MaximumBytes)
                    throw new ToolFailure("m16_002_package_input_size");
                if (plan.IsEnabled("EnableM16009Probe"))
                {
                    if (options.HasThreadingSelection)
                    {
                        string package = Path.GetFullPath(options.LinuxElfPackage, root), disassembly = Path.GetFullPath(options.LinuxThreadingDisassembly, root);
                        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                        foreach (string path in new[]{options.Image,options.EfiArtifact,options.SerialLog,options.SummaryLog,
                            options.SummaryLog+".stderr.log",options.SerialLog+".pcap",options.SerialLog+".peer.log",options.SerialLog+".peer.log.stderr.log",options.M01RawObservationLog})
                            if (path.Length != 0 && (string.Equals(package, Path.GetFullPath(path, root), comparison) ||
                                string.Equals(disassembly, Path.GetFullPath(path, root), comparison)))
                                throw new ToolFailure("threading_input_output_alias");
                        if (string.Equals(package, disassembly, comparison))
                            throw new ToolFailure("threading_input_alias");
                        threadingDisassemblyInput = new FileStream(disassembly, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (service)
                        {
                            serviceInputs = new();
                            var p = servicePackageInput = serviceInputs.Open(Path.GetDirectoryName(package)!, package, "service package", "service_package", OS.Manifest.LinuxElfPackage.MaximumBytes, singleLink: true);
                            var d = serviceDisassemblyInput = serviceInputs.Open(Path.GetDirectoryName(disassembly)!, disassembly, "service instructions", "service_instructions", 128 * 1024 * 1024, singleLink: true);
                            AuthorityFiles.RequireDistinct(p, d);
                            serviceProgram = NativeAotServiceProgram.Create(root, p.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "service_package"), d.ReadUtf8(128 * 1024 * 1024, "service_instructions"), serviceContract);
                            NativeAotServiceProgram.HoldSources(root, serviceInputs);
                            serviceInputs.VerifyUnchanged();
                        }
                        else if (web || staticFiles)
                        {
                            webInputs = new();
                            var p = webPackageInput = webInputs.Open(Path.GetDirectoryName(package)!, package, "web package", "web_package", OS.Manifest.LinuxElfPackage.MaximumBytes, singleLink: true);
                            var d = webDisassemblyInput = webInputs.Open(Path.GetDirectoryName(disassembly)!, disassembly, "web instructions", "web_instructions", NativeAotWebProgram.MaximumDisassemblyBytes, singleLink: true);
                            AuthorityFiles.RequireDistinct(p, d);
                            byte[] bytes = p.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "web_package");
                            if (staticFiles)
                            {
                                staticProgram = NativeAotStaticFileProgram.Create(root, bytes, d.ReadUtf8(NativeAotStaticFileProgram.MaximumDisassemblyBytes, "static_instructions"), staged: !diskStatic);
                                NativeAotStaticFileProgram.HoldSources(root, webInputs);
                            }
                            else
                            {
                                webProgram = NativeAotWebProgram.Create(root, bytes, d.ReadUtf8(NativeAotWebProgram.MaximumDisassemblyBytes, "web_instructions"));
                                NativeAotWebProgram.HoldSources(root, webInputs);
                                webConfiguration = NativeAotWebProgram.DescribeConfiguration(bytes);
                                if (options.HasWebSignalRace && webConfiguration.LogQuota)
                                    throw new ToolFailure("web_race_normal_logging_required");
                            }
                            webInputs.VerifyUnchanged();
                        }
                        else if (vectors)
                            vectorsProgram = NativeAotSocketVectorsProgram.Read(root, package, disassembly);
                        else if (containment)
                            containmentProgram = NativeAotSocketContainmentProgram.Read(root, package, disassembly);
                        else if (sockets)
                            socketProgram = NativeAotSocketProgram.Read(root, plan.IsEnabled("EnableM16012Probe"), package, disassembly);
                        else if (NativeAotRuntimeSelection.Application(plan.Flags).Length != 0)
                            runtimeProgram = NativeAotRuntimeProgram.Read(root, options.LinuxThreadingApplication, package, disassembly);
                        else
                            threadingProgram = NativeAotThreadingProgram.Read(root, options.LinuxThreadingApplication, package, disassembly);
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
                    else if (diskFiles)
                    {
                        if (linuxInput.Length > 65536)
                            throw new ToolFailure("disk_mechanical_package_size");
                        byte[] bytes = new byte[(int)linuxInput.Length];
                        linuxInput.ReadExactly(bytes);
                        if (writableDisk)
                            NativeAotDiskSelection.RequireWritablePackage(bytes, options.WritableDiskFixture);
                        else
                            NativeAotDiskSelection.RequireMechanicalPackage(bytes, options.LinuxDiskRaceVariant);
                        linuxInput.Position = 0;
                    }
                    else if (signalEpoll)
                        LinuxSignalEpollSelection.RequirePackage(linuxInput);
                    else
                        LinuxSignalProofFixture.RequirePackage(linuxInput);
                }
                else if (plan.IsEnabled("EnableM16008Probe"))
                    LinuxThreadProofFixture.RequirePackage(linuxInput);
            }
            output.WriteLine("QEMU_SCRIPT_BOOT_POLICY policy=" + plan.BootPolicy);
            output.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
            output.WriteLine("TEST_PASS workload.profile.tooling " + (options.WorkloadProfile == "SingleMinimalApi" ? "default single" : "propagates multi"));
            output.WriteLine("QEMU_SCRIPT_PROFILE required=" + plan.Required.Count + " ordered=" + plan.Ordered.Count + " unique=" + plan.Unique.Count + " forbidden=" + plan.Forbidden.Count);
            if (plan.Presets.Count != 0)
                output.WriteLine("QEMU_MARKER_PRESETS " + string.Join(';', plan.Presets));
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
                actualQemuForGate = qemu;
                if (plan.BuildEfi)
                {
                    var build = await Run("OS.Tooling", ["efi", "build", "--boot-policy", plan.BootPolicy, "--efi-output-path", options.EfiArtifact,
                        .. plan.Flags.Where(p => p.Value).Select(p => "-" + p.Key)]);
                    if (build.ExitCode != 0)
                        return build.ExitCode;
                }
                if (!options.SkipImageBuild)
                {
                    string[] package = plan.IsEnabled("EnableM16002Probe") ? ["--package", options.LinuxElfPackage] : [];
                    string imageOutput = diskFiles ? options.Image + ".base.raw" : options.Image;
                    if (diskFiles && (Path.Exists(Path.GetFullPath(imageOutput, root)) || Path.Exists(Path.GetFullPath(options.Image, root))))
                        throw new ToolFailure("disk_image_output_not_fresh");
                    var image = await Run("OS.ImageBuilder", ["create-raw", "--workload-profile", options.WorkloadProfile, "--efi", options.EfiArtifact, "--output", imageOutput, .. package]);
                    if (image.ExitCode != 0)
                        return image.ExitCode;
                    if (diskFiles)
                    {
                        string assets = Path.GetFullPath(options.WebRootAssets, root);
                        var inventory = WebRootImageFixture.SnapshotPublishedRoot(assets);
                        string webImage = Path.GetFullPath(writableDisk ? options.Image + ".web.raw" :
                            options.WebRootFault == "Truncated" ? options.Image + ".positive.raw" : options.Image, root);
                        diskImageReceipt = WebRootImageFixture.Apply(root, Path.GetFullPath(imageOutput, root), assets, inventory,
                            webImage, options.WebRootFixture);
                        WebRootImageFixture.Verify(webImage, diskImageReceipt);
                        using (var receipt = new FileStream(webImage + ".webroot.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                            System.Text.Json.JsonSerializer.Serialize(receipt, diskImageReceipt);
                        if (options.WebRootFault == "Truncated")
                        {
                            string faultImage = Path.GetFullPath(options.Image, root);
                            var fault = WebRootImageFixture.CreateFaultVariant(root, webImage, diskImageReceipt, faultImage, "Truncated");
                            using (var receipt = new FileStream(faultImage + ".fault.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                            {
                                System.Text.Json.JsonSerializer.Serialize(receipt, fault);
                                receipt.Flush(true);
                            }
                            diskTruncated = WebRootDiskInventory.VerifiedTruncated.Verify(root, webImage, diskImageReceipt, faultImage, fault);
                        }
                        if (writableDisk)
                        {
                            string dataImage = Path.GetFullPath(options.Image + ".data.raw", root);
                            _ = WritableDataImageFixture.Create(root, dataImage);
                            writableReceipt = CombinedWebDataImageFixture.Create(root, webImage, dataImage, Path.GetFullPath(options.Image, root));
                            using var receipt = new FileStream(Path.GetFullPath(options.Image + ".combined.json", root), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                            receipt.Write(writableReceipt.CopyJson());
                            receipt.Flush(true);
                        }
                    }
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
                int preflight = writableDisk ? 0 : ValidateImage(Path.GetFullPath(options.Image, root), options.DiskFormat, output, options.Image);
                if (preflight != 0)
                    return preflight;
                if (diskFiles)
                {
                    if (diskImageReceipt is null)
                        throw new ToolFailure("disk_generated_image_receipt_required");
                    diskNative = new(EfiOptions.Parse(["--root", root, "--boot-policy", plan.BootPolicy, "--efi-artifact", options.EfiArtifact,
                        ..plan.Flags.Where(p => p.Value).Select(p => "-" + p.Key)], false), output.WriteLine);
                    diskNative.Verify();
                    diskInputs = new();
                    var image = diskImageInput = diskInputs.Open(root, Path.GetFullPath(options.Image, root), "disk image", "disk_image",
                        writableDisk ? CombinedWebDataImageFixture.ImageBytes : WebRootImageFixture.MaximumImageBytes, singleLink: true);
                    var efi = diskEfiInput = diskInputs.Open(root, Path.GetFullPath(options.EfiArtifact, root), "disk EFI", "disk_efi", 16 * 1024 * 1024, singleLink: true);
                    var package = diskPackageInput = diskInputs.Open(root, Path.GetFullPath(options.LinuxElfPackage, root), "disk package", "disk_package", diskStatic ? OS.Manifest.LinuxElfPackage.MaximumBytes : 65536, singleLink: true);
                    AuthorityFiles.RequireDistinct(image, efi, package);
                    if (diskStatic)
                    {
                        diskReceiptInput = diskInputs.Open(root, Path.GetFullPath(options.Image +
                            (diskTruncated is null ? ".webroot.json" : ".positive.raw.webroot.json"), root), "disk image receipt", "disk_image_receipt", 16 * 1024 * 1024, singleLink: true);
                        AuthorityFiles.RequireDistinct(image, efi, package, diskReceiptInput);
                        if (diskReceiptInput.ReadUtf8(16 * 1024 * 1024, "disk_image_receipt") != System.Text.Json.JsonSerializer.Serialize(diskImageReceipt))
                            throw new ToolFailure("disk_image_receipt_identity");
                        if (diskTruncated is not null)
                        {
                            var positive = diskInputs.Open(root, Path.GetFullPath(options.Image + ".positive.raw", root), "positive disk image", "disk_positive", WebRootImageFixture.MaximumImageBytes, singleLink: true);
                            var faultReceipt = diskInputs.Open(root, Path.GetFullPath(options.Image + ".fault.json", root), "fault disk receipt", "disk_fault_receipt", 4096, singleLink: true);
                            AuthorityFiles.RequireDistinct(image, efi, package, diskReceiptInput, positive, faultReceipt);
                            RequireTruncatedReceipts(diskTruncated, positive, faultReceipt);
                            diskTruncated.RequireValues(diskTruncated.Baseline, image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_fault_image"));
                            diskTruncated.VerifyUnchanged();
                        }
                    }
                    byte[] packageBytes = package.ReadBytes(diskStatic ? OS.Manifest.LinuxElfPackage.MaximumBytes : 65536, "disk_package");
                    if (writableDisk)
                        NativeAotDiskSelection.RequireWritablePackage(packageBytes, options.WritableDiskFixture);
                    else if (diskStatic)
                        NativeAotStaticFileProgram.RequirePackage(packageBytes, false);
                    else
                        NativeAotDiskSelection.RequireMechanicalPackage(packageBytes, options.LinuxDiskRaceVariant);
                    if (writableDisk)
                    {
                        if (writableReceipt is null)
                            throw new ToolFailure("writable_generated_combined_receipt_required");
                        _ = CombinedWebDataImageFixture.Verify(image.Path, writableReceipt);
                        writableWorking = CombinedWebDataImageFixture.CloneForWrite(root, image.Path,
                            Path.GetFullPath(options.Image + ".combined.json", root), writableReceipt, efi.Path, package.Path,
                            Path.GetFullPath(options.Image + ".working.raw", root));
                        diskInputs.VerifyUnchanged();
                        output.WriteLine($"WRITABLE_IMAGE_JOIN originalSha256={image.Sha256} workingIdentity={writableWorking.IdentityKey} initialSha256={writableWorking.InitialSha256} efiSha256={efi.Sha256} packageSha256={package.Sha256}");
                    }
                    else
                    {
                        var locations = LinuxElfImageFixture.Inspect(image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_image"),
                            efi.ReadBytes(16 * 1024 * 1024, "disk_efi"), packageBytes);
                        diskInputs.VerifyUnchanged();
                        output.WriteLine($"DISK_IMAGE_JOIN efiOffset={locations.EfiStart} packageOffset={locations.PackageStart} imageSha256={image.Sha256} efiSha256={efi.Sha256} packageSha256={package.Sha256}");
                    }
                }
                if (web || staticFiles)
                {
                    webNative = new(EfiOptions.Parse(["--root",root,"--boot-policy",plan.BootPolicy,"--efi-artifact",options.EfiArtifact,
                        ..plan.Flags.Where(p=>p.Value).Select(p=>"-"+p.Key)], false), output.WriteLine);
                    webNative.Verify();
                    string imagePath = Path.GetFullPath(options.Image, root), efiPath = Path.GetFullPath(options.EfiArtifact, root), packagePath = Path.GetFullPath(options.LinuxElfPackage, root);
                    var imageInput = webImageInput = webInputs!.Open(Path.GetDirectoryName(imagePath)!, imagePath, "web image", "web_image", 64 * 1024 * 1024, singleLink: true);
                    var efiInput = webEfiInput = webInputs.Open(Path.GetDirectoryName(efiPath)!, efiPath, "web EFI", "web_efi", 16 * 1024 * 1024, singleLink: true);
                    var packageInput = webInputs.Open(Path.GetDirectoryName(packagePath)!, packagePath, "web package", "web_package", OS.Manifest.LinuxElfPackage.MaximumBytes, singleLink: true);
                    AuthorityFiles.RequireDistinct(imageInput, efiInput, packageInput);
                    if (options.HasWebSignalRace)
                        AuthorityFiles.RequireDistinct(webPackageInput!, webDisassemblyInput!, imageInput, efiInput);
                    var locations = LinuxElfImageFixture.Inspect(imageInput.ReadBytes(64 * 1024 * 1024, "web_image"),
                        efiInput.ReadBytes(16 * 1024 * 1024, "web_efi"), packageInput.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "web_package"));
                    webInputs.VerifyUnchanged();
                    output.WriteLine($"WEB_IMAGE_JOIN efiOffset={locations.EfiStart} packageOffset={locations.PackageStart} imageSha256={imageInput.Sha256} efiSha256={efiInput.Sha256} packageSha256={packageInput.Sha256}");
                }
                if (service)
                {
                    serviceNative = new(EfiOptions.Parse(["--root",root,"--boot-policy",plan.BootPolicy,"--efi-artifact",options.EfiArtifact,
                        ..plan.Flags.Where(p=>p.Value).Select(p=>"-"+p.Key)], false), output.WriteLine);
                    serviceNative.Verify();
                    string imagePath = Path.GetFullPath(options.Image, root), efiPath = Path.GetFullPath(options.EfiArtifact, root), packagePath = Path.GetFullPath(options.LinuxElfPackage, root);
                    var imageInput = serviceImageInput = serviceInputs!.Open(Path.GetDirectoryName(imagePath)!, imagePath, "service image", "service_image", 64 * 1024 * 1024, singleLink: true);
                    var efiInput = serviceEfiInput = serviceInputs.Open(Path.GetDirectoryName(efiPath)!, efiPath, "service EFI", "service_efi", 16 * 1024 * 1024, singleLink: true);
                    var packageInput = serviceInputs.Open(Path.GetDirectoryName(packagePath)!, packagePath, "service package", "service_package", OS.Manifest.LinuxElfPackage.MaximumBytes, singleLink: true);
                    AuthorityFiles.RequireDistinct(imageInput, efiInput, packageInput);
                    var locations = LinuxElfImageFixture.Inspect(imageInput.ReadBytes(64 * 1024 * 1024, "service_image"), efiInput.ReadBytes(16 * 1024 * 1024, "service_efi"),
                        packageInput.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "service_package"));
                    serviceInputs.VerifyUnchanged();
                    output.WriteLine($"SERVICE_IMAGE_JOIN efiOffset={locations.EfiStart} packageOffset={locations.PackageStart} imageSha256={imageInput.Sha256} efiSha256={efiInput.Sha256} packageSha256={packageInput.Sha256}");
                }
            }
            int timeout = options.TimeoutSeconds;
            bool rawCapture = plan.IsEnabled("EnableM01OwnedPagingProbe") && !new[] { "M02OwnedGdtTssTerminal", "M02Ud2VectorState", "M02PageFaultCr2ErrorCode", "M02DoubleFaultIst", "M02UnexpectedVectorPanic" }.Any(x => plan.IsEnabled("Enable" + x + "Probe"));
            if (rawCapture)
                output.WriteLine("M01_RAW_OBSERVATION_CONFIG enabled=true active=" + (!options.DryRun).ToString().ToLowerInvariant() + " dryRun=" + options.DryRun.ToString().ToLowerInvariant());
            if (!options.DryRun && rawCapture)
            {
                output.WriteLine("TEST_START m01.026.raw-observations");
                capture = QemuCaptureSession.Create(root, qemu, options.M01RawObservationLog, options.SerialLog, options.SummaryLog, options.TimeoutSeconds, plan.IsEnabled("EnableM02SafeStubProbe"), options.Accelerator);
                output.WriteLine("M01_RAW_OBSERVATION_ACCELERATOR " + capture.Accelerator);
                qemu = capture.Executable;
                timeout = capture.RunnerTimeoutSeconds;
                output.WriteLine("M01_RAW_OBSERVATION_PATH " + capture.ObservationPath);
                output.WriteLine("M01_RAW_OBSERVATION_SUPPORT " + capture.SupportDirectory);
                output.WriteLine("M01_RAW_OBSERVATION_TIMEOUTS guestSeconds=" + capture.GuestTimeoutSeconds + " captureSeconds=" + QemuCaptureSession.CaptureTimeoutSeconds + " runnerSeconds=" + capture.RunnerTimeoutSeconds);
                output.WriteLine("M01_RAW_OBSERVATION_IDENTITIES " + string.Join(' ', capture.Identities.Select(p => p.Key + "=" + p.Value)));
            }
            List<string> arguments = ["--image", options.Image, "--disk-format", options.DiskFormat, "--accelerator", options.Accelerator, "--workload-profile", options.WorkloadProfile,
                "--serial-log", options.SerialLog, "--summary-log", options.SummaryLog, "--timeout", timeout.ToString(CultureInfo.InvariantCulture)];
            if (options.MemoryMiB is int memory)
                arguments.AddRange(["--memory", memory.ToString(CultureInfo.InvariantCulture) + "M"]);
            arguments.AddRange(["--startup-timeout", options.StartupTimeoutSeconds.ToString(CultureInfo.InvariantCulture)]);
            if (plan.ReplaceDefaultRequiredMarkers)
                arguments.Add("--replace-default-required-markers");
            if (plan.IsEnabled("EnableM02UnexpectedVectorPanicProbe"))
                arguments.Add("--expect-unexpected-vector-panic");
            if (plan.IsEnabled("EnableM03OwnedSubsystemPanicProbe"))
                arguments.Add("--expect-owned-subsystem-panic");
            if (plan.IsEnabled("EnableM04UnhandledExceptionProbe"))
                arguments.Add("--expect-unhandled-exception");
            if (plan.IsEnabled("EnableM03FirmwareEntropySeedProbe") || plan.IsEnabled("EnableM16004Probe"))
                arguments.AddRange(["--firmware-rng-source", options.FirmwareRngSource]);
            if (plan.IsEnabled("EnableM05BarDmaProbe"))
                arguments.Add("--pci-bar-test-device");
            if (plan.IsEnabled("EnableM05VirtioNegotiationProbe"))
                arguments.Add("--virtio-negotiation-device");
            if (plan.IsEnabled("EnableM07VirtioNetProbe"))
                arguments.AddRange(["--ethernet-capture", Path.GetFullPath(options.SerialLog + ".pcap", root)]);
            if (plan.IsEnabled("EnableM07ArpReceiveProbe"))
                arguments.Add("--arp-receive-fixture");
            if (containment)
                arguments.Add("--socket-containment-peer");
            else if (plan.IsEnabled("EnableM07026Probe") && !signalEpoll && !service && !sockets && !vectors && !web && !staticFiles && !diskFiles)
                arguments.Add("--continuing-network-peer");
            if (writableDisk)
                arguments.AddRange(["--writable-data-image", Path.GetFullPath(options.Image + ".working.raw", root)]);
            else if (diskFiles)
                arguments.Add("--readonly-webroot-disk");
            if (staticFiles)
                arguments.Add("--finite-static-files");
            if (NativeAotDiskSelection.FullStaticSelected(plan.Flags))
                arguments.Add("--full-disk-static-files");
            if (NativeAotDiskSelection.TimingSelected(plan.Flags))
                arguments.Add("--disk-read-timing");
            if (service)
                arguments.Add("--continuing-service");
            if (service && serviceContract == NativeAotServiceContract.QuotaFailure)
                arguments.Add("--service-quota-failure");
            if ((service || sockets || vectors || staticFiles) && options.DryRun)
                arguments.AddRange(["--socket-forward-port", "20080"]);
            if (web)
            {
                int port = 20080;
                if (!options.DryRun)
                {
                    var lease = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                    try
                    {
                        lease.Start();
                        port = ((System.Net.IPEndPoint)lease.LocalEndpoint).Port;
                    }
                    finally { lease.Stop(); }
                }
                webForwardPort = port;
                arguments.AddRange(["--socket-forward-port",port.ToString(CultureInfo.InvariantCulture),"--continuing-web",
                    "--http-probe-url","http://127.0.0.1:"+port.ToString(CultureInfo.InvariantCulture)+"/healthz",
                    "--http-probe-expected-status","200","--http-probe-expected-body",options.DryRun?"OS_CONFIG_SELECTED_R01":webConfiguration.Body,
                    "--http-probe-timeout","20","--web-config-body",options.DryRun?"{}":webConfiguration.Json,
                    "--web-log-case",!options.DryRun && webConfiguration.LogQuota?"records64":"normal"]);
                if (options.WebEventOwnedSignalRace)
                    arguments.Add("--web-event-owned-signal-race");
                if (options.HasWebSignalRace && options.DryRun)
                    arguments.AddRange(["--web-race-pipe",options.WebEventOwnedSignalRace?
                        "clros-web-race-v3-00000000000000000000000000000000":"clros-web-race-00000000000000000000000000000000"]);
            }
            if (plan.IsEnabled("EnableM06VirtioBlockQueueSetupProbe"))
                arguments.Add("--virtio-block-queue-device");
            if (gptBootImage.Length != 0)
                arguments.AddRange(["--virtio-block-boot-image", gptBootImage]);
            // The runner adds every ordered marker to its required list. Avoid
            // sending an identical list twice: long profiles otherwise exceed
            // Windows' command-line limit without adding a requirement.
            if (signalEpoll || service || sockets || containment || vectors || web || staticFiles || diskFiles)
            {
                // Preserve required insertion order while sending each ordered
                // marker only once. The combined profile otherwise exceeds the
                // Windows process command-line limit before QEMU can start.
                foreach (string value in plan.Required)
                    arguments.AddRange([plan.Ordered.Contains(value, StringComparer.Ordinal) ? "--require-ordered-marker" : "--require-marker", value]);
            }
            else
            {
                IReadOnlyList<string> required = plan.Required.SequenceEqual(plan.Ordered) ? [] : plan.Required;
                foreach (var (name, values) in new[] { ("--require-marker", required), ("--require-ordered-marker", plan.Ordered) })
                    foreach (string value in values)
                        arguments.AddRange([name, value]);
            }
            foreach (var (name, values) in new[] { ("--require-unique-marker", plan.Unique), ("--forbid-marker", plan.Forbidden), ("--marker-preset", plan.Presets) })
                foreach (string value in values)
                    arguments.AddRange([name, value]);
            if (qemu.Length != 0)
                arguments.AddRange(["--qemu", qemu]);
            if (ovmf.Length != 0)
                arguments.AddRange(["--ovmf-code", ovmf]);
            if (options.DryRun)
                arguments.Add("--dry-run");
            async Task<ProcessResult> RunSocketRunner(int port, CancellationToken token)
            {
                if (service)
                {
                    serviceStarted = DateTimeOffset.UtcNow;
                    serviceForwardPort = port;
                }
                if (staticFiles)
                {
                    webStarted = DateTimeOffset.UtcNow;
                    webForwardPort = port;
                }
                var result = await RunOriginalRunner([.. arguments, "--socket-forward-port", port.ToString(CultureInfo.InvariantCulture)],
                    TimeSpan.FromSeconds(capture is null ? options.TimeoutSeconds + 60 : timeout + 30),
                    capture?.Environment ?? new Dictionary<string, string?>(), token, false);
                if (service)
                    serviceCompleted = DateTimeOffset.UtcNow;
                if (staticFiles)
                    webCompleted = DateTimeOffset.UtcNow;
                output.Write(result.StandardOutput);
                output.Write(result.StandardError);
                return result;
            }
            ProcessResult runner;
            if (staticFiles && !options.DryRun)
            {
                if (diskStatic)
                {
                    if (NativeAotDiskSelection.FullStaticSelected(plan.Flags))
                    {
                        if (diskImageInput is null || diskImageReceipt is null)
                            throw new ToolFailure("disk_full_image_owner_required");
                        var inventory = diskTruncated?.Baseline ?? WebRootDiskInventory.Inspect(diskImageInput.Path, diskImageInput.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_full_browser_image"), diskImageReceipt);
                        if (options.HttpBenchmarkWarmSequences is int warmSequences)
                        {
                            var observed = await NativeAotHttpBenchmarkPeer.RunAsync(root, options.SerialLog,
                                RunSocketRunner, inventory, new HttpWorkloadBenchmark.Options
                                {
                                    System = "CLROS",
                                    Backend = options.Accelerator,
                                    WarmSequences = warmSequences
                                }, options.TimeoutSeconds,
                                async (port, directory, token) =>
                                {
                                    diskBrowser = await NativeAotDiskStaticBrowser.RunAsync(root, port, directory, inventory, token);
                                }, cancellation);
                            VerifyDiskDiagnosticInputs();
                            diskBrowser!.VerifyUnchanged();
                            // This diagnostic does not invoke the ordinary full
                            // stream consumer. Retain its final capture bounds
                            // explicitly; these are not total-file writer quotas.
                            using AuthorityFiles captures = new();
                            var heldCaptures = new (string Path, int Maximum)[]
                            {
                                (options.SerialLog, DiskStaticCapturePolicy.SerialBytes),
                                (options.SerialLog + ".pcap", DiskStaticCapturePolicy.PacketBytes),
                                (options.SummaryLog, DiskStaticCapturePolicy.SummaryBytes)
                            }.Select(input => captures.Open(root, Path.GetFullPath(input.Path, root),
                                "HTTP diagnostic capture", "http_benchmark_capture", input.Maximum, singleLink: true)).ToArray();
                            using (FileStream receipt = new(Path.Combine(observed.Directory, "captures.json"),
                                FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                            {
                                System.Text.Json.JsonSerializer.Serialize(receipt,
                                    heldCaptures.Select(input => new { input.Path, input.Length, input.Sha256 }));
                                receipt.Flush(true);
                            }
                            captures.VerifyUnchanged();
                            output.WriteLine($"TEST_PASS qemu.smoke.http-workload-diagnostic responses={observed.Observations.Samples.Count} runner_joined=true full_and_cleanup_qualified=false");
                            return 0;
                        }
                        if (NativeAotDiskSelection.TimingSelected(plan.Flags))
                        {
                            var benchmark = NativeAotDiskTimingPeer.BenchmarkOptions.ForProfile(plan.Flags,
                                () => diskImageInput.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_timing_image"));
                            var diagnostic = await NativeAotDiskTimingPeer.RunAsync(root, options.SerialLog, RunSocketRunner, inventory, benchmark, options.TimeoutSeconds, cancellation);
                            VerifyDiskDiagnosticInputs();
                            output.WriteLine("TEST_PASS qemu.smoke.disk-read-timing-diagnostic responses=5 bytes=65536 runner_joined=true full_and_cleanup_qualified=false");
                            return 0;
                        }
                        void VerifyDiskDiagnosticInputs()
                        {
                            diskInputs!.VerifyUnchanged();
                            diskNative!.VerifyHeldInputs();
                            webInputs!.VerifyUnchanged();
                            webNative!.VerifyHeldInputs();
                            WebRootImageFixture.Verify(Path.GetFullPath(options.Image, root), diskImageReceipt);
                            if (capture is not null)
                                capture.Validate();
                            cancellation.ThrowIfCancellationRequested();
                        }
                        diskStaticPeer = await NativeAotDiskStaticPeer.RunAsync(root, options.SerialLog, RunSocketRunner, cancellation, inventory,
                            async (port, directory, token) => { diskBrowser = await NativeAotDiskStaticBrowser.RunAsync(root, port, directory, inventory, token); }, diskTruncated);
                    }
                    else
                        diskStaticPeer = await NativeAotDiskStaticPeer.RunAsync(root, options.SerialLog, RunSocketRunner, cancellation);
                    runner = diskStaticPeer.Runner;
                }
                else
                {
                    staticPeer = await NativeAotStagedStaticPeer.RunAsync(root, options.SerialLog, options.SerialLog + ".static", RunSocketRunner, cancellation);
                    runner = staticPeer.Runner;
                }
            }
            else if (service && !options.DryRun)
            {
                if (serviceContract == NativeAotServiceContract.QuotaFailure)
                {
                    if (serviceProgram is null || serviceInputs is null || serviceNative is null || servicePackageInput is null ||
                        serviceDisassemblyInput is null || serviceImageInput is null || serviceEfiInput is null)
                        throw new ToolFailure("service_quota_original_inputs_required");
                    serviceQuotaPeer = await NativeAotServiceQuotaPeer.RunObservedOwnedAsync(root, options.SerialLog + ".peer.log", options.SerialLog, RunSocketRunner, cancellation);
                    runner = serviceQuotaPeer.Runner;
                }
                else
                {
                    servicePeer = await NativeAotServicePeer.RunAsync(root, options.SerialLog + ".peer.log", options.SerialLog, RunSocketRunner, cancellation, lifecycle: options.ServiceLifecycle);
                    runner = servicePeer.Runner;
                }
            }
            else if (vectors && !options.DryRun)
            {
                vectorsPeer = await NativeAotSocketVectorsPeer.RunAsync(root, options.SerialLog + ".peer.log", options.SerialLog, RunSocketRunner, cancellation);
                runner = vectorsPeer.Runner;
            }
            else if (sockets && !options.DryRun)
            {
                socketPeer = await NativeAotSocketPeer.RunAsync(root, options.SerialLog + ".peer.log", RunSocketRunner,
                    cancellation, token => NativeAotSocketPeer.WaitForAcceptAsync(Path.GetFullPath(options.SerialLog, root), plan.IsEnabled("EnableM16012Probe"), token),
                    receiveGateSerial: plan.IsEnabled("EnableM16012Probe") ? options.SerialLog : null);
                runner = socketPeer.Runner;
            }
            else if (web && options.HasWebSignalRace && !options.DryRun)
            {
                async Task<ProcessResult> RunWebRaceRunner(string pipe, CancellationToken token)
                {
                    webStarted = DateTimeOffset.UtcNow;
                    try
                    {
                        var result = await RunOriginalRunner([.. arguments, "--web-race-pipe", pipe],
                            TimeSpan.FromSeconds(options.TimeoutSeconds + 60), new Dictionary<string, string?>(), token, false);
                        output.Write(result.StandardOutput);
                        output.Write(result.StandardError);
                        return result;
                    }
                    finally { webCompleted = DateTimeOffset.UtcNow; }
                }
                webRacePeer = options.WebEventOwnedSignalRace ?
                    await NativeAotWebRacePeer.RunEventOwnedAsync(root, webForwardPort, webConfiguration.Body,
                        options.SerialLog + ".race.json", options.SummaryLog, RunWebRaceRunner, cancellation, initialWaitSerial: options.SerialLog) :
                    await NativeAotWebRacePeer.RunOwnedAsync(root, webForwardPort, webConfiguration.Body,
                        options.SerialLog + ".race.json", options.SummaryLog, RunWebRaceRunner, cancellation, initialWaitSerial: options.SerialLog);
                runner = webRacePeer.Runner;
            }
            else if (capture is null)
            {
                if (web)
                    webStarted = DateTimeOffset.UtcNow;
                runner = await Run("OS.QemuRunner", arguments);
                if (web)
                    webCompleted = DateTimeOffset.UtcNow;
            }
            else
            {
                runner = await RunOriginalRunner(arguments, TimeSpan.FromSeconds(timeout + 30),
                    capture.Environment, cancellation, false);
                output.Write(runner.StandardOutput);
                output.Write(runner.StandardError);
                if (writableDisk)
                {
                    if (!runner.TerminationConfirmed)
                        throw new ToolFailure("writable_original_runner_unjoined");
                }
                else
                    QemuTranscripts.RequireCompleteProcess(runner, "qemu_capture_runner_incomplete");
            }
            if (options.DryRun)
                return runner.ExitCode;
            if (writableDisk)
            {
                if (writableWorking is null || writableReceipt is null || diskInputs is null || diskNative is null)
                    throw new ToolFailure("writable_original_owner_required");
                // The opt-in process call remains pending until its original
                // process tree actually joins, including uncertain cleanup.
                if (!runner.TerminationConfirmed)
                    throw new ToolFailure("writable_original_runner_unjoined");
                writableWorking.VerifyOriginalsAndWorkingIdentity();
                diskInputs.VerifyUnchanged();
                diskNative.VerifyHeldInputs();
                using AuthorityFile snapshot = SnapshotJoinedWritableImage(root, writableWorking, options.Image + ".joined.raw");
                var extracted = CombinedWebDataImageFixture.Inspect(snapshot.Path);
                output.WriteLine($"WRITABLE_JOINED_IMAGE sha256={extracted.RawSha256} files={extracted.Data.Files.Count} runnerExit={runner.ExitCode} guestQualified=false physicalDurabilityQualified=false");
                writableWorking.VerifyOriginalsAndWorkingIdentity();
                diskInputs.VerifyUnchanged();
                diskNative.VerifyHeldInputs();
                QemuTranscripts.RequireCompleteProcess(runner, "writable_original_runner_incomplete");
                if (runner.ExitCode != 0)
                    return runner.ExitCode;
                var report = QemuTranscripts.InspectOriginalWritableDiskRun(root, options, plan, runner, diskImageInput!, snapshot,
                    diskEfiInput!, diskPackageInput!, writableWorking, writableReceipt, extracted, qemu, ovmf, () =>
                    {
                        if (capture is not null)
                        {
                            capture.Validate();
                            output.WriteLine("TEST_PASS m01.026.raw-observations");
                        }
                        snapshot.VerifyUnchanged();
                        writableWorking.VerifyOriginalsAndWorkingIdentity();
                        diskInputs.VerifyUnchanged();
                        diskNative.VerifyHeldInputs();
                        cancellation.ThrowIfCancellationRequested();
                    });
                output.WriteLine($"WRITABLE_ORIGINAL_CAPTURE calls={report.Calls} waits={report.Waits.Count} transfers={report.Transfers} ioRows={report.Block.Io.Count} firmware=true packets=true originalInputs=true imageValues=true physicalDurabilityQualified=false");
                return 0;
            }
            if (diskFiles)
            {
                diskInputs!.VerifyUnchanged();
                diskNative!.VerifyHeldInputs();
                if (diskTruncated is null)
                    WebRootImageFixture.Verify(Path.GetFullPath(options.Image, root), diskImageReceipt!);
                else
                    diskTruncated.VerifyUnchanged();
                if (runner.ExitCode != 0)
                    return runner.ExitCode;
                if (diskStatic)
                {
                    if (diskStaticPeer is null || staticProgram is null || diskReceiptInput is null)
                        throw new ToolFailure("disk_static_original_owner_required");
                    var original = QemuTranscripts.InspectDiskStaticRun(root, options, plan, runner, staticProgram, diskStaticPeer,
                        diskImageInput!, diskEfiInput!, diskPackageInput!, diskReceiptInput, diskImageReceipt!, new(webForwardPort, qemu, ovmf, webStarted, webCompleted), () =>
                        {
                            if (capture is not null)
                            {
                                capture.Validate();
                                output.WriteLine("TEST_PASS m01.026.raw-observations");
                            }
                            diskInputs.VerifyUnchanged();
                            diskNative.VerifyHeldInputs();
                            webInputs!.VerifyUnchanged();
                            webNative!.VerifyHeldInputs();
                            cancellation.ThrowIfCancellationRequested();
                            diskBrowser?.VerifyUnchanged();
                        }, NativeAotDiskSelection.FullStaticSelected(plan.Flags) ? (messages, held) =>
                        {
                            if (diskBrowser is null || diskImageInput is null || diskImageReceipt is null || diskBrowser.Port != webForwardPort ||
                                Path.GetFullPath(diskBrowser.Directory) != Path.GetFullPath(Path.Combine(diskStaticPeer.Directory, "browser")))
                                throw new ToolFailure("disk_full_browser_original_owner_required");
                            diskBrowser.VerifyUnchanged();
                            var browserFiles = diskBrowser.ArtifactPaths.Select(path => held.Open(root, path,
                                "original disk browser evidence", "disk_browser_original_capture", NativeAotDiskStaticBrowser.MaximumProtocolBytes, allowEmpty: true, singleLink: true)).ToArray();
                            if (browserFiles.Length is 0 or > 272 || browserFiles.Sum(f => f.Length) > 128 * 1024 * 1024 ||
                                browserFiles.Any(f => Path.GetDirectoryName(f.Path) != diskBrowser.Directory))
                                throw new ToolFailure("disk_full_browser_artifact_inventory");
                            var inventory = diskTruncated?.Baseline ?? WebRootDiskInventory.Inspect(diskImageInput.Path, diskImageInput.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_full_browser_image"), diskImageReceipt);
                            _ = NativeAotDiskStaticBrowser.ValidateOriginal(diskBrowser, inventory, QemuTranscripts.InspectDiskStaticBrowserWireValues(messages));
                            diskBrowser.VerifyUnchanged();
                            return browserFiles;
                        }
                    : null, diskTruncated);
                    output.WriteLine($"TEST_PASS qemu.smoke.{(diskTruncated is null ? "disk-static" : "disk-static-truncated")} scope={original.Http.Scope} calls={original.Replay.Static.Protocol.Calls} sectors={original.Sectors.Device.Counters.Copied - original.Sectors.Device.Counters.BootstrapCopied} responses={original.Http.Responses} {(diskBrowser is null ? "blazor_qualified=false" : "blazor_booted=true")}");
                    return 0;
                }
                void VerifyDiskInputs()
                {
                    if (capture is not null)
                    {
                        capture.Validate();
                        output.WriteLine("TEST_PASS m01.026.raw-observations");
                    }
                    diskInputs.VerifyUnchanged();
                    diskNative.VerifyHeldInputs();
                    cancellation.ThrowIfCancellationRequested();
                }
                if (options.HasDiskRaceSelection)
                {
                    if (options.LinuxDiskRaceVariant == "Progress")
                    {
                        var progress = QemuTranscripts.InspectLinuxDiskProgressRun(root, options, plan, runner,
                            diskImageInput!, diskEfiInput!, diskPackageInput!, diskImageReceipt!, qemu, ovmf, VerifyDiskInputs);
                        output.WriteLine($"TEST_PASS qemu.smoke.disk-progress calls={progress.Leaf.Calls.Count} requests={progress.Requests.Count} deviceProgress={progress.DevicePendingProgress.Count} schedulerReplay=false");
                        return 0;
                    }
                    if (options.LinuxDiskRaceVariant == "CloseReuse")
                    {
                        var closeReuse = QemuTranscripts.InspectLinuxDiskCloseReuseRun(root, options, plan, runner,
                            diskImageInput!, diskEfiInput!, diskPackageInput!, diskImageReceipt!, qemu, ovmf, VerifyDiskInputs);
                        output.WriteLine($"TEST_PASS qemu.smoke.disk-close-reuse calls={closeReuse.Leaf.Calls.Count} requests={closeReuse.Requests.Count} cancelled={closeReuse.Device.Cancelled} schedulerReplay=false");
                        return 0;
                    }
                    var pending = QemuTranscripts.InspectLinuxDiskExitPendingRun(root, options, plan, runner,
                        diskImageInput!, diskEfiInput!, diskPackageInput!, diskImageReceipt!, qemu, ovmf, VerifyDiskInputs);
                    output.WriteLine($"TEST_PASS qemu.smoke.disk-exit-pending calls={pending.Leaf.Calls.Count} copiedDataSectors={pending.CopiedDataSectors.Count} outstanding=1 resetRetired=true");
                    return 0;
                }
                var report = QemuTranscripts.InspectLinuxDiskFileRun(root, options, plan, runner,
                    diskImageInput!, diskEfiInput!, diskPackageInput!, diskImageReceipt!, qemu, ovmf, VerifyDiskInputs);
                output.WriteLine($"TEST_PASS qemu.smoke.disk-files calls={report.Leaf.Calls} sectors={report.Device.Counters.Copied - report.Device.Counters.BootstrapCopied} readWindows={report.ReadWindows.Count} instructionIrqs={report.InstructionIrqs}");
                return 0;
            }
            if (serviceQuotaPeer is not null)
            {
                // Expected runner22 is only the private owner's capture
                // disposition. Inspect every original before quota success;
                // the owner has already joined its still-finishing peer.
                var context = new QemuTranscripts.ServiceRunnerContext(serviceForwardPort, qemu, ovmf, serviceStarted, serviceCompleted);
                using var quotaEvidence = QemuTranscripts.HoldServiceQuotaEvidence(root, options, serviceProgram!, serviceQuotaPeer, runner,
                    context, serviceInputs!, servicePackageInput!, serviceDisassemblyInput!, serviceImageInput!, serviceEfiInput!);
                if (capture is not null)
                {
                    capture.Validate();
                    output.WriteLine("TEST_PASS m01.026.raw-observations");
                }
                _ = QemuTranscripts.InspectOwnedServiceQuota(root, options, plan, serviceProgram!, context, quotaEvidence);
                NativeAotServiceProgram.HoldSources(root, serviceInputs!);
                serviceInputs!.VerifyUnchanged();
                serviceNative!.VerifyHeldInputs();
                quotaEvidence.Verify();
                cancellation.ThrowIfCancellationRequested();
                output.WriteLine("TEST_PASS qemu.smoke.quota");
                return 0;
            }
            if (runner.ExitCode != 0 && (capture is not null || web || service))
                return runner.ExitCode;
            if (webRacePeer is not null)
            {
                if (webProgram is null || webInputs is null || webNative is null || webPackageInput is null ||
                    webDisassemblyInput is null || webImageInput is null || webEfiInput is null)
                    throw new ToolFailure("web_race_original_inputs_required");
                int result = QemuTranscripts.InspectOwnedWebRace(root, options, plan, webProgram, webRacePeer, runner,
                    new(webForwardPort, qemu, ovmf, webStarted, webCompleted), webInputs, webPackageInput, webDisassemblyInput, webImageInput, webEfiInput, output,
                    () => { if (capture is not null) { capture.Validate(); output.WriteLine("TEST_PASS m01.026.raw-observations"); } },
                    () => { NativeAotWebProgram.HoldSources(root, webInputs); webInputs.VerifyUnchanged(); webNative.VerifyHeldInputs(); cancellation.ThrowIfCancellationRequested(); });
                cancellation.ThrowIfCancellationRequested();
                if (result == 0)
                    output.WriteLine("TEST_PASS qemu.smoke.web-race");
                return result;
            }
            // Hold the exact public evidence before the capture hash check and
            // through every subsequent parent/leaf/runner read.
            using AuthorityFiles? threadingEvidence = staticPeer is not null ? QemuTranscripts.HoldStaticFileEvidence(root, options, staticPeer) :
                servicePeer is not null ? QemuTranscripts.HoldServiceEvidence(root, options, servicePeer) :
                vectorsPeer is not null ? QemuTranscripts.HoldSocketVectorsEvidence(root, options, vectorsPeer) :
                containmentProgram is not null ? QemuTranscripts.HoldSocketContainmentEvidence(root, options) :
                socketPeer is not null ? QemuTranscripts.HoldSocketEvidence(root, options, socketPeer) :
                webProgram is not null ? QemuTranscripts.HoldWebEvidence(root, options) :
                threadingProgram is null && runtimeProgram is null && webProgram is null ? null : QemuTranscripts.HoldThreadingEvidence(root, options);
            if (capture is not null)
            {
                capture.Validate();
                output.WriteLine("TEST_PASS m01.026.raw-observations");
            }
            if (staticProgram is not null)
            {
                if (staticPeer is null || webInputs is null || webNative is null)
                    throw new ToolFailure("static_complete_owned_inputs_required");
                int result = QemuTranscripts.InspectStaticFileRun(root, options, plan, runner, output, staticProgram, staticPeer,
                    new(webForwardPort, qemu, ovmf, webStarted, webCompleted));
                NativeAotStaticFileProgram.HoldSources(root, webInputs!);
                webInputs!.VerifyUnchanged();
                webNative!.VerifyHeldInputs();
                threadingEvidence!.VerifyUnchanged();
                cancellation.ThrowIfCancellationRequested();
                if (result == 0)
                    output.WriteLine("TEST_PASS qemu.smoke.static-files");
                return result;
            }
            int validated = serviceProgram is not null ? QemuTranscripts.InspectServiceRun(root, options, plan, runner, output, serviceProgram, servicePeer!,
                new(serviceForwardPort, qemu, ovmf, serviceStarted, serviceCompleted)) :
                webProgram is not null ? QemuTranscripts.InspectWebRun(root, options, plan, runner, output, webProgram,
                new(webForwardPort, qemu, ovmf, webStarted, webCompleted)) :
                QemuTranscripts.ValidateRun(root, options, plan, runner, output, helloProgram, threadingProgram, runtimeProgram, socketProgram, socketPeer, containmentProgram, vectorsProgram, vectorsPeer);
            threadingEvidence?.VerifyUnchanged();
            webInputs?.VerifyUnchanged();
            serviceInputs?.VerifyUnchanged();
            if (serviceProgram is not null)
            {
                NativeAotServiceProgram.HoldSources(root, serviceInputs!);
                if (serviceNative is null)
                    throw new ToolFailure("service_current_native_context_required");
                serviceNative.VerifyHeldInputs();
                if (validated == 0)
                    output.WriteLine("TEST_PASS qemu.smoke.service");
            }
            if (webProgram is not null)
            {
                NativeAotWebProgram.HoldSources(root, webInputs!);
                if (webNative is null)
                    throw new ToolFailure("web_current_native_context_required");
                webNative.VerifyHeldInputs();
                if (validated == 0)
                    output.WriteLine("TEST_PASS qemu.smoke.web");
            }
            return validated;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            output.WriteLine("TEST_FAIL qemu.smoke socket_orchestration_timeout");
            return 1;
        }
        catch (Exception error) when (error is ToolFailure or ArgumentException or IOException or InvalidDataException or InvalidOperationException)
        {
            output.WriteLine("TEST_FAIL qemu.smoke " + error.Message);
            return 1;
        }
        finally { capture?.Dispose(); linuxInput?.Dispose(); helloDisassemblyInput?.Dispose(); threadingDisassemblyInput?.Dispose(); webInputs?.Dispose(); webNative?.Dispose(); serviceInputs?.Dispose(); serviceNative?.Dispose(); writableWorking?.Dispose(); diskInputs?.Dispose(); diskNative?.Dispose(); }
    }

    // The original owner holds these files and all four identities unchanged
    // through final acceptance. Hash text casing carries no byte identity;
    // receipt JSON remains the exact original serialization, without rewriting.
    internal static void RequireTruncatedReceipts(WebRootDiskInventory.VerifiedTruncated truncated,
        AuthorityFile positive, AuthorityFile faultReceipt)
    {
        if (!string.Equals(positive.Sha256, truncated.Baseline.ImageSha256, StringComparison.OrdinalIgnoreCase) ||
            faultReceipt.ReadUtf8(4096, "disk_fault_receipt") != System.Text.Json.JsonSerializer.Serialize(truncated.Fault.Receipt))
            throw new ToolFailure("disk_truncated_receipt_identity");
    }

    // Reject aliases before builders create any output. The runner repeats
    // runtime identity/plain-path checks against its actual child lifetime.
    private static void RequireWritableOutputPaths(string root, QemuSmokeOptions options)
    {
        string[] outputs = [options.Image, options.Image + ".base.raw", options.Image + ".web.raw", options.Image + ".web.raw.webroot.json",
            options.Image + ".data.raw", options.Image + ".combined.json", options.Image + ".working.raw", options.Image + ".joined.raw",
            options.SerialLog, options.SummaryLog, options.SerialLog + ".pcap", options.SummaryLog + ".stderr.log"];
        string[] paths = outputs.Select(p => Path.GetFullPath(p, root)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        string[] inputs = new[] { options.LinuxElfPackage, options.EfiArtifact, options.WebRootAssets, options.Qemu, options.OvmfCode }
            .Where(p => p.Length != 0).Select(p => Path.GetFullPath(p, root)).ToArray();
        if (paths.Distinct(comparer).Count() != paths.Length || paths.Any(p => inputs.Contains(p, comparer)))
            throw new ToolFailure("writable_output_alias");
        foreach (string path in paths)
        {
            OS.Tools.QemuRunner.WritableDataImageLease.RequirePlainRuntimePath(path);
            if (!options.DryRun && Path.Exists(path))
                throw new ToolFailure("writable_output_not_fresh");
        }
    }

    // Called only after the original owned process call confirms tree join.
    // Keep the working identity anchor through this independent snapshot's
    // inspection; read/write sharing itself grants no exclusive-writer proof.
    private static AuthorityFile SnapshotJoinedWritableImage(string root, CombinedWebDataImageFixture.WorkingImage working, string destination)
    {
        string path = Path.GetFullPath(destination, root);
        OS.Tools.QemuRunner.WritableDataImageLease.RequirePlainRuntimePath(path);
        working.VerifyOriginalsAndWorkingIdentity();
        OS.Tooling.Native.FileIdentity created;
        using (var input = new FileStream(working.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            if (OS.Tooling.Native.NativeFile.GetIdentity(input.SafeFileHandle).Key != working.IdentityKey || input.Length != working.Length)
                throw new ToolFailure("writable_snapshot_identity");
            input.CopyTo(output);
            output.Flush(true);
            if (output.Length != working.Length)
                throw new ToolFailure("writable_snapshot_length");
            created = OS.Tooling.Native.NativeFile.GetIdentity(output.SafeFileHandle);
        }
        working.VerifyOriginalsAndWorkingIdentity();
        using var source = new FileStream(working.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var identity = OS.Tooling.Native.NativeFile.GetIdentity(stream.SafeFileHandle);
            if (identity.Key != created.Key || identity.LinkCount != 1 || identity.Length != (ulong)working.Length)
                throw new ToolFailure("writable_snapshot_identity");
            byte[] first = System.Security.Cryptography.SHA256.HashData(source), second = System.Security.Cryptography.SHA256.HashData(stream);
            if (!first.AsSpan().SequenceEqual(second))
                throw new ToolFailure("writable_snapshot_bytes");
            var snapshot = new AuthorityFile(path, root, "joined writable snapshot", "writable_snapshot", stream, identity, true);
            snapshot.VerifyUnchanged();
            working.VerifyOriginalsAndWorkingIdentity();
            return snapshot;
        }
        catch { stream.Dispose(); throw; }
    }

    public static string? ResolveExecutable(string? requested)
    {
        string name = string.IsNullOrWhiteSpace(requested) ? Environment.GetEnvironmentVariable("QEMU") ?? "qemu-system-x86_64" : requested;
        if (string.IsNullOrWhiteSpace(name))
            name = "qemu-system-x86_64";
        if (Path.IsPathRooted(name) || name.Contains('\\') || name.Contains('/'))
            return File.Exists(name) ? Path.GetFullPath(name) : null;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            foreach (string filename in new[] { name, name + ".exe", name + ".cmd", name + ".bat" })
            {
                string candidate = Path.Combine(directory.Trim('"'), filename);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
        }
        return new[] { @"C:\Program Files\qemu\qemu-system-x86_64.exe", @"C:\Program Files (x86)\qemu\qemu-system-x86_64.exe",
            @"C:\ProgramData\chocolatey\bin\qemu-system-x86_64.exe", @"C:\msys64\mingw64\bin\qemu-system-x86_64.exe" }.FirstOrDefault(File.Exists);
    }

    public static string? ResolveOvmf(string? requested, string qemuPath)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return File.Exists(requested) ? Path.GetFullPath(requested) : null;
        string[] names = ["OVMF_CODE.fd", "OVMF.fd", "edk2-x86_64-code.fd", "edk2-x86_64-secure-code.fd", "OVMF_CODE_4M.fd"];
        List<string> candidates = [];
        foreach (string variable in new[] { "OS_OVMF_CODE", "OVMF_CODE", "QEMU_OVMF_CODE" })
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value))
                candidates.Add(value);
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
            if (File.Exists(path))
                return path;
        return null;
    }
    public static int ValidateImage(string path, string format, TextWriter output, string? displayPath = null)
    {
        displayPath ??= path;
        if (!File.Exists(path))
        {
            output.WriteLine("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + displayPath + " exists=false bytes=0");
            output.WriteLine("TEST_FAIL qemu.smoke image_not_found path=" + displayPath);
            return 2;
        }
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        output.WriteLine("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + displayPath + " exists=true bytes=" + stream.Length);
        if (stream.Length == 0)
        {
            output.WriteLine("TEST_FAIL qemu.smoke image_empty path=" + displayPath);
            return 11;
        }
        Span<byte> header = stackalloc byte[4];
        string detected = stream.Read(header) == 4 && header.SequenceEqual(new byte[] { 0x51, 0x46, 0x49, 0xfb }) ? "qcow2" : "raw-compatible";
        output.WriteLine("QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT expected=" + format + " detected=" + detected);
        if (format == "raw" && detected == "qcow2" || format == "qcow2" && detected != "qcow2")
        {
            output.WriteLine("TEST_FAIL qemu.smoke disk_format_mismatch expected=" + format + " detected=" + detected);
            return 12;
        }
        return 0;
    }
}
