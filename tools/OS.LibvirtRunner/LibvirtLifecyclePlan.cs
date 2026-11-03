using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

sealed class LibvirtLifecyclePlan
{
    private readonly LibvirtLifecyclePlanStep[] steps;
    private LibvirtLifecyclePlan(LibvirtOptions options, int xmlByteCount, LibvirtLifecyclePlanStep[] steps)
    {
        DomainName = options.DomainName;
        ImagePath = options.ImagePath;
        DiskFormat = options.DiskFormat;
        SerialLogPath = options.SerialLogPath;
        Network = options.Network.ToDiagnosticString();
        TimeoutSeconds = options.TimeoutSeconds;
        RequiredMarkers = options.RequiredMarkers.ToArray();
        ForbiddenMarkers = options.ForbiddenMarkers.ToArray();
        AllowRealLifecycle = options.AllowRealLifecycle;
        XmlByteCount = xmlByteCount;
        this.steps = steps;
    }

    public string DomainName
    {
        get;
    }
    public string ImagePath
    {
        get;
    }
    public string DiskFormat
    {
        get;
    }
    public string SerialLogPath
    {
        get;
    }
    public string Network
    {
        get;
    }
    public int TimeoutSeconds
    {
        get;
    }
    public IReadOnlyList<string> RequiredMarkers
    {
        get;
    }
    public IReadOnlyList<string> ForbiddenMarkers
    {
        get;
    }
    public bool AllowRealLifecycle
    {
        get;
    }
    public int XmlByteCount
    {
        get;
    }

    public static LibvirtLifecyclePlan Create(LibvirtOptions options, int xmlByteCount)
    {
        LibvirtLifecyclePlanStep[] steps =
        {
            new LibvirtLifecyclePlanStep(1, "generate-domain-xml"),
            new LibvirtLifecyclePlanStep(2, "validate-host-tools-and-paths"),
            new LibvirtLifecyclePlanStep(3, "connect-libvirt"),
            new LibvirtLifecyclePlanStep(4, "create-transient-domain-from-xml"),
            new LibvirtLifecyclePlanStep(5, "start-domain"),
            new LibvirtLifecyclePlanStep(6, "poll-serial-log-for-required-markers"),
            new LibvirtLifecyclePlanStep(7, "reject-forbidden-serial-markers"),
            new LibvirtLifecyclePlanStep(8, "enforce-timeout"),
            new LibvirtLifecyclePlanStep(9, "collect-serial-and-domain-diagnostics"),
            new LibvirtLifecyclePlanStep(10, "destroy-domain-if-running"),
            new LibvirtLifecyclePlanStep(11, "undefine-domain-if-persistent"),
            new LibvirtLifecyclePlanStep(12, "cleanup-transient-artifacts")
        };
        return new LibvirtLifecyclePlan(options, xmlByteCount, steps);
    }

    public string ToMachineReadableText()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("LIBVIRT_LIFECYCLE_PLAN domain=").AppendLine(DomainName);
        builder.AppendLine("LIBVIRT_LIFECYCLE_MODE plan-only");
        builder.AppendLine("LIBVIRT_LIFECYCLE_DEFAULT_START_DOMAIN false");
        builder.AppendLine("LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false");
        builder.Append("LIBVIRT_LIFECYCLE_REAL_RUN_OPT_IN ").AppendLine(AllowRealLifecycle.ToDiagnosticString());
        builder.AppendLine("LIBVIRT_LIFECYCLE_REQUIRED_OPT_IN_SWITCH --allow-real-libvirt-lifecycle");
        builder.Append("LIBVIRT_LIFECYCLE_IMAGE ").AppendLine(ImagePath);
        builder.Append("LIBVIRT_LIFECYCLE_DISK_FORMAT ").AppendLine(DiskFormat);
        builder.Append("LIBVIRT_LIFECYCLE_SERIAL_LOG ").AppendLine(SerialLogPath);
        builder.Append("LIBVIRT_LIFECYCLE_NETWORK ").AppendLine(Network);
        builder.Append("LIBVIRT_LIFECYCLE_TIMEOUT_SECONDS ").AppendLine(TimeoutSeconds.ToStringInvariant());
        builder.Append("LIBVIRT_LIFECYCLE_XML_UTF8_BYTES ").AppendLine(XmlByteCount.ToStringInvariant());
        builder.Append("LIBVIRT_LIFECYCLE_REQUIRED_MARKERS ").AppendLine(FormatPlanMarkers(RequiredMarkers));
        builder.Append("LIBVIRT_LIFECYCLE_FORBIDDEN_MARKERS ").AppendLine(FormatPlanMarkers(ForbiddenMarkers));
        foreach (LibvirtLifecyclePlanStep step in steps)
        {
            builder.Append("LIBVIRT_LIFECYCLE_STEP ").Append(step.Ordinal.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').AppendLine(step.Name);
        }

        builder.Append("LIBVIRT_LIFECYCLE_CLEANUP_ORDER ").AppendLine("destroy-domain-if-running;undefine-domain-if-persistent;cleanup-transient-artifacts");
        builder.AppendLine("LIBVIRT_LIFECYCLE_PLAN_READY");
        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string FormatPlanMarkers(IReadOnlyList<string> markers)
    {
        return markers.Count == 0 ? "none" : string.Join(";", markers);
    }
}
