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
    public bool DryRun
    {
        get; init;
    }
    public bool PlanLifecycle
    {
        get; init;
    }
    public bool CheckSerialOnly
    {
        get; init;
    }
    public bool AllowRealLibvirtLifecycle
    {
        get; init;
    }
    public bool ValidatePaths
    {
        get; init;
    }

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
                "--image" => options with
                {
                    Image = Value()
                },
                "--disk-format" => options with
                {
                    DiskFormat = Value().ToLowerInvariant()
                },
                "--serial-log" => options with
                {
                    SerialLog = Value()
                },
                "--summary-log" => options with
                {
                    SummaryLog = Value()
                },
                "--domain-xml" => options with
                {
                    DomainXml = Value()
                },
                "--lifecycle-plan" => options with
                {
                    LifecyclePlan = Value()
                },
                "--ovmf-loader" => options with
                {
                    OvmfLoader = Value()
                },
                "--network" => options with
                {
                    Network = Value().ToLowerInvariant()
                },
                "--virsh" => options with
                {
                    Virsh = Value()
                },
                "--workload-profile" => options with
                {
                    WorkloadProfile = Value()
                },
                "--timeout-seconds" => options with
                {
                    TimeoutSeconds = int.Parse(Value(), System.Globalization.CultureInfo.InvariantCulture)
                },
                "--dry-run" => options with
                {
                    DryRun = true
                },
                "--plan-lifecycle" => options with
                {
                    PlanLifecycle = true
                },
                "--check-serial-only" => options with
                {
                    CheckSerialOnly = true
                },
                "--allow-real-libvirt-lifecycle" => options with
                {
                    AllowRealLibvirtLifecycle = true
                },
                "--validate-paths" => options with
                {
                    ValidatePaths = true
                },
                "--require-marker" or "--forbid-marker" => options,
                _ => throw new ArgumentException("unknown_option " + option)
            };
            if (option == "--require-marker")
            {
                explicitRequired = true;
                required.Add(Value());
            }

            if (option == "--forbid-marker")
                forbidden.Add(Value());
        }

        return options with
        {
            RequireMarkers = explicitRequired ? SmokeContracts.SplitMarkers(required) : options.RequireMarkers,
            ForbidMarkers = SmokeContracts.SplitMarkers(forbidden)
        };
    }
}
