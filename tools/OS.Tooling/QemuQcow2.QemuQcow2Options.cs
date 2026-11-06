namespace OS.Tooling;

public sealed class QemuQcow2Options
{
    public QemuSmokeOptions Smoke
    {
        get;
    } = new()
    {
        BootPolicy = "Diagnostic",
        Image = "artifacts/images/os.qcow2",
        DiskFormat = "qcow2",
        SerialLog = "artifacts/logs/qemu-smoke-qcow2.log",
        SummaryLog = "artifacts/logs/qemu-smoke-qcow2.summary.log",
        SkipImageBuild = true
    };
    public string RawImage { get; set; } = "artifacts/images/os.raw";
    public string QemuImg { get; set; } = "";
    public bool SkipRawImageBuild
    {
        get; set;
    }

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
            string argument = arguments[i];
            int at = argument.IndexOfAny(['=', ':']);
            string key = QemuSmokeOptions.Key(at < 0 ? argument : argument[..at]);
            string Value() => at >= 0 ? argument[(at + 1)..] : ++i < arguments.Length ? arguments[i] : throw new ArgumentException("qcow2_missing_value " + argument);
            switch (key)
            {
                case "rawimage":
                    options.RawImage = Value();
                    break;
                case "qcow2image":
                    smokeArguments.AddRange(["--image", Value()]);
                    break;
                case "qemuimg":
                    options.QemuImg = Value();
                    break;
                case "skiprawimagebuild":
                    options.SkipRawImageBuild = at < 0 || bool.Parse(argument[(at + 1)..].TrimStart('$'));
                    break;
                default:
                    if (key == "requiremarker")
                        customMarkers = true;
                    smokeArguments.Add(argument);
                    if (at < 0 && key is not ("dryrun" or "buildefi"))
                        smokeArguments.Add(Value());
                    break;
            }
        }

        if (!customMarkers)
            foreach (string marker in options.Smoke.RequireMarker)
                smokeArguments.AddRange(["--require-marker", marker]);
        QemuSmokeOptions parsed = QemuSmokeOptions.Parse(smokeArguments.ToArray());
        options.Smoke.Image = parsed.Image;
        options.Smoke.EfiArtifact = parsed.EfiArtifact;
        options.Smoke.SerialLog = parsed.SerialLog;
        options.Smoke.SummaryLog = parsed.SummaryLog;
        options.Smoke.Qemu = parsed.Qemu;
        options.Smoke.OvmfCode = parsed.OvmfCode;
        options.Smoke.TimeoutSeconds = parsed.TimeoutSeconds;
        options.Smoke.WorkloadProfile = parsed.WorkloadProfile;
        options.Smoke.DryRun = parsed.DryRun;
        options.Smoke.BuildEfi = parsed.BuildEfi;
        options.Smoke.RequireMarker.Clear();
        options.Smoke.RequireMarker.AddRange(parsed.RequireMarker);
        return options;
    }
}
