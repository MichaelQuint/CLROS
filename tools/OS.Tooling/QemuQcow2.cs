namespace OS.Tooling;

public sealed class QemuQcow2Options
{
    public QemuSmokeOptions Smoke { get; } = new() { BootPolicy = "Diagnostic", Image = "artifacts/images/os.qcow2", DiskFormat = "qcow2", SerialLog = "artifacts/logs/qemu-smoke-qcow2.log", SummaryLog = "artifacts/logs/qemu-smoke-qcow2.summary.log", SkipImageBuild = true };
    public string RawImage { get; set; } = "artifacts/images/os.raw";
    public string QemuImg { get; set; } = "";
    public bool SkipRawImageBuild { get; set; }
    public QemuQcow2Options()
    {
        Smoke.RequireMarker.Clear();
        Smoke.RequireMarker.AddRange(SmokeContracts.RequiredMarkers);
    }
    public static QemuQcow2Options Parse(params string[] arguments)
    {
        QemuQcow2Options options = new();
        List<string> smokeArguments = ["--boot-policy", "Diagnostic", "--skip-image-build", "--disk-format", "qcow2", "--image", options.Smoke.Image, "--serial-log", options.Smoke.SerialLog, "--summary-log", options.Smoke.SummaryLog];
        bool customMarkers = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            string argument = arguments[i]; int at = argument.IndexOfAny(['=', ':']);
            string key = QemuSmokeOptions.Key(at < 0 ? argument : argument[..at]);
            string Value() => at >= 0 ? argument[(at + 1)..] : ++i < arguments.Length ? arguments[i] : throw new ArgumentException("qcow2_missing_value " + argument);
            switch (key)
            {
                case "rawimage": options.RawImage = Value(); break;
                case "qcow2image": smokeArguments.AddRange(["--image", Value()]); break;
                case "qemuimg": options.QemuImg = Value(); break;
                case "skiprawimagebuild": options.SkipRawImageBuild = at < 0 || bool.Parse(argument[(at + 1)..].TrimStart('$')); break;
                default:
                    if (key == "requiremarker") customMarkers = true;
                    smokeArguments.Add(argument);
                    if (at < 0 && key is not ("dryrun" or "buildefi")) smokeArguments.Add(Value());
                    break;
            }
        }
        if (!customMarkers) foreach (string marker in options.Smoke.RequireMarker) smokeArguments.AddRange(["--require-marker", marker]);
        QemuSmokeOptions parsed = QemuSmokeOptions.Parse(smokeArguments.ToArray());
        options.Smoke.Image = parsed.Image; options.Smoke.EfiArtifact = parsed.EfiArtifact;
        options.Smoke.SerialLog = parsed.SerialLog; options.Smoke.SummaryLog = parsed.SummaryLog;
        options.Smoke.Qemu = parsed.Qemu; options.Smoke.OvmfCode = parsed.OvmfCode;
        options.Smoke.TimeoutSeconds = parsed.TimeoutSeconds; options.Smoke.WorkloadProfile = parsed.WorkloadProfile;
        options.Smoke.DryRun = parsed.DryRun; options.Smoke.BuildEfi = parsed.BuildEfi;
        options.Smoke.RequireMarker.Clear(); options.Smoke.RequireMarker.AddRange(parsed.RequireMarker);
        return options;
    }
}

public static class QemuQcow2
{
    public static async Task<int> RunAsync(string root, QemuQcow2Options options, TextWriter output,
        Func<string, IReadOnlyList<string>, Task<ProcessResult>>? invoke = null, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        invoke ??= (name, args) => ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, name), .. args], root)
        { Timeout = TimeSpan.FromMinutes(name == "OS.Tooling" ? 30 : 10) }, cancellation);
        async Task<int> Step(string step, string tool, IReadOnlyList<string> args)
        {
            ProcessResult result = await invoke(tool, args);
            output.Write(result.StandardOutput); output.Write(result.StandardError);
            int code = result.Succeeded ? 0 : result.ExitCode == 0 ? 1 : result.ExitCode;
            if (code != 0) output.WriteLine("TEST_FAIL qemu.qcow2.smoke step=" + step + " exit=" + code);
            return code;
        }
        QemuSmokeOptions smoke = options.Smoke;
        output.WriteLine("TEST_START qemu.qcow2.smoke");
        output.WriteLine("QCOW2_SOURCE_RAW " + options.RawImage); output.WriteLine("QCOW2_IMAGE_ARTIFACT " + smoke.Image);
        // Admit options before any native build or image mutation.
        QemuProfiles.Create(smoke);
        string qemuImg = string.IsNullOrWhiteSpace(options.QemuImg)
            ? HostTools.FindExecutable(null, "QEMU_IMG", "qemu-img", @"C:\Program Files\qemu\qemu-img.exe") ?? "" : options.QemuImg;
        int code;
        if (smoke.DryRun) output.WriteLine("QCOW2_DRY_RUN");
        else
        {
            if (smoke.BuildEfi && (code = await Step("build-efi", "OS.Tooling", ["efi", "build", "--boot-policy", "Diagnostic", "--efi-output-path", smoke.EfiArtifact])) != 0) return code;
            if (!options.SkipRawImageBuild && (code = await Step("create-raw", "OS.ImageBuilder", ["create-raw", "--workload-profile", smoke.WorkloadProfile, "--efi", smoke.EfiArtifact, "--output", options.RawImage])) != 0) return code;
        }
        List<string> convert = ["convert-qcow2", "--input", options.RawImage, "--output", smoke.Image];
        if (smoke.DryRun) convert.Add("--dry-run");
        if (qemuImg.Length != 0) convert.AddRange(["--qemu-img", qemuImg]);
        if ((code = await Step(smoke.DryRun ? "convert-qcow2-dry-run" : "convert-qcow2", "OS.ImageBuilder", convert)) != 0) return code;
        bool build = smoke.BuildEfi; smoke.BuildEfi = false;
        try { code = await QemuSmoke.RunAsync(root, smoke, output, invoke, cancellation); }
        finally { smoke.BuildEfi = build; }
        if (code != 0) output.WriteLine("TEST_FAIL qemu.qcow2.smoke step=" + (smoke.DryRun ? "qemu-smoke-dry-run" : "qemu-smoke") + " exit=" + code);
        else output.WriteLine("TEST_PASS qemu.qcow2.smoke");
        return code;
    }
}
