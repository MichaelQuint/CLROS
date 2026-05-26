using System.Text;
using System.Xml;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

try
{
    LibvirtOptions options = LibvirtOptions.Parse(args);

    if (options.ShowHelp)
    {
        LibvirtOptions.PrintHelp();
        return 0;
    }

    if (!options.GenerateXml && !options.PlanLifecycle && !options.CheckSerial && !options.ValidateXml)
    {
        Console.Error.WriteLine("Libvirt boot is not implemented yet. Use generate-xml, plan-lifecycle, validate-xml, check-serial, or --dry-run to emit host-only artifacts.");
        return 2;
    }

    if (options.ValidateXml)
    {
        return ValidateDomainXml(options);
    }

    if (options.CheckSerial)
    {
        return CheckSerialLog(options);
    }

    options.ValidatePaths();

    string xml = BuildDomainXml(options);
    if (options.PlanLifecycle)
    {
        if (options.EmitDiagnostics)
        {
            EmitDiagnostics(options, xml);
        }

        Console.WriteLine(BuildLifecyclePlan(options, xml));
        return 0;
    }

    if (options.EmitDiagnostics)
    {
        EmitDiagnostics(options, xml);
    }

    Console.WriteLine(xml);
    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static string BuildDomainXml(LibvirtOptions options)
{
    XElement os = new XElement(
        "os",
        new XElement("type",
            new XAttribute("arch", "x86_64"),
            new XAttribute("machine", "q35"),
            "hvm"),
        new XElement("boot", new XAttribute("dev", "hd")));

    if (!string.IsNullOrWhiteSpace(options.OvmfLoaderPath))
    {
        os.Element("type")?.AddAfterSelf(new XElement(
            "loader",
            new XAttribute("readonly", "yes"),
            new XAttribute("type", "pflash"),
            options.OvmfLoaderPath));
    }

    XElement devices = new XElement(
        "devices",
        new XElement(
            "disk",
            new XAttribute("type", "file"),
            new XAttribute("device", "disk"),
            new XElement(
                "driver",
                new XAttribute("name", "qemu"),
                new XAttribute("type", options.DiskFormat)),
            new XElement("source", new XAttribute("file", options.ImagePath)),
            new XElement(
                "target",
                new XAttribute("dev", "vda"),
                new XAttribute("bus", "virtio"))),
        new XElement(
            "serial",
            new XAttribute("type", "file"),
            new XElement("source", new XAttribute("path", options.SerialLogPath)),
            new XElement(
                "target",
                new XAttribute("type", "isa-serial"),
                new XAttribute("port", "0"),
                new XElement("model", new XAttribute("name", "isa-serial")))));

    if (options.Network == LibvirtNetwork.Default)
    {
        devices.Add(new XElement(
            "interface",
            new XAttribute("type", "network"),
            new XElement("source", new XAttribute("network", "default")),
            new XElement("model", new XAttribute("type", "virtio"))));
    }

    XElement domain = new XElement(
        "domain",
        new XAttribute("type", "kvm"),
        new XElement("name", options.DomainName),
        new XElement(
            "memory",
            new XAttribute("unit", "MiB"),
            options.MemoryMiB.ToStringInvariant()),
        new XElement(
            "currentMemory",
            new XAttribute("unit", "MiB"),
            options.MemoryMiB.ToStringInvariant()),
        new XElement(
            "vcpu",
            new XAttribute("placement", "static"),
            options.VCpuCount.ToStringInvariant()),
        os,
        new XElement(
            "features",
            new XElement("acpi"),
            new XElement("apic")),
        devices);

    StringBuilder builder = new StringBuilder();
    using XmlWriter writer = XmlWriter.Create(builder, new XmlWriterSettings
    {
        Indent = true,
        NewLineChars = "\n",
        OmitXmlDeclaration = true
    });

    domain.Save(writer);
    writer.Flush();
    return builder.ToString();
}

static string BuildLifecyclePlan(LibvirtOptions options, string xml)
{
    LibvirtLifecyclePlan plan = LibvirtLifecyclePlan.Create(
        options,
        Encoding.UTF8.GetByteCount(xml));

    return plan.ToMachineReadableText();
}

static int ValidateDomainXml(LibvirtOptions options)
{
    Console.WriteLine("TEST_START libvirt.xml.validation");

    if (string.IsNullOrWhiteSpace(options.DomainXmlPath))
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation domain_xml_path_required");
        return 4;
    }

    Console.WriteLine("LIBVIRT_XML_PATH " + options.DomainXmlPath);

    if (!File.Exists(options.DomainXmlPath))
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation xml_not_found");
        return 4;
    }

    byte[] xmlBytes;
    XDocument document;
    try
    {
        xmlBytes = File.ReadAllBytes(options.DomainXmlPath);
        using MemoryStream stream = new MemoryStream(xmlBytes, writable: false);
        document = XDocument.Load(stream, LoadOptions.None);
    }
    catch (XmlException)
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation xml_malformed");
        return 4;
    }
    catch (IOException)
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation xml_read_failed");
        return 4;
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation xml_read_denied");
        return 4;
    }

    LibvirtDomainXmlValidationResult validation = ValidateLibvirtDomainXml(document, xmlBytes.Length);
    if (!validation.Succeeded)
    {
        Console.WriteLine("TEST_FAIL libvirt.xml.validation " + validation.Status);
        return 4;
    }

    EmitDomainXmlSummary(validation.Summary);
    Console.WriteLine("TEST_PASS libvirt.xml.validation");
    return 0;
}

static LibvirtDomainXmlValidationResult ValidateLibvirtDomainXml(XDocument document, int xmlByteCount)
{
    XElement? domain = document.Root;
    if (domain is null || domain.Name.LocalName != "domain")
    {
        return LibvirtDomainXmlValidationResult.Fail("domain_missing");
    }

    if (!StringEquals(GetAttribute(domain, "type"), "kvm"))
    {
        return LibvirtDomainXmlValidationResult.Fail("domain_type_invalid");
    }

    if (!TryGetRequiredSingleLineText(domain.Element("name"), out string domainName))
    {
        return LibvirtDomainXmlValidationResult.Fail("domain_name_missing");
    }

    if (!TryReadMiBElement(domain.Element("memory"), out int memoryMiB))
    {
        return LibvirtDomainXmlValidationResult.Fail("memory_invalid");
    }

    if (!TryReadMiBElement(domain.Element("currentMemory"), out int currentMemoryMiB))
    {
        return LibvirtDomainXmlValidationResult.Fail("current_memory_invalid");
    }

    if (currentMemoryMiB != memoryMiB)
    {
        return LibvirtDomainXmlValidationResult.Fail("current_memory_mismatch");
    }

    if (!TryReadPositiveIntText(domain.Element("vcpu"), out int vcpuCount))
    {
        return LibvirtDomainXmlValidationResult.Fail("vcpu_invalid");
    }

    XElement? os = domain.Element("os");
    XElement? osType = os?.Element("type");
    if (osType is null ||
        !StringEquals(osType.Value.Trim(), "hvm") ||
        !StringEquals(GetAttribute(osType, "arch"), "x86_64") ||
        !StringEquals(GetAttribute(osType, "machine"), "q35"))
    {
        return LibvirtDomainXmlValidationResult.Fail("os_type_invalid");
    }

    XElement? boot = os?.Element("boot");
    if (boot is null || !StringEquals(GetAttribute(boot, "dev"), "hd"))
    {
        return LibvirtDomainXmlValidationResult.Fail("boot_device_invalid");
    }

    XElement? loader = os?.Element("loader");
    string ovmfLoaderPath = "none";
    bool hasOvmfLoader = loader is not null;
    if (loader is not null)
    {
        if (!StringEquals(GetAttribute(loader, "readonly"), "yes") ||
            !StringEquals(GetAttribute(loader, "type"), "pflash") ||
            !TryGetRequiredSingleLineText(loader, out ovmfLoaderPath))
        {
            return LibvirtDomainXmlValidationResult.Fail("ovmf_loader_invalid");
        }
    }

    XElement? features = domain.Element("features");
    if (features?.Element("acpi") is null || features.Element("apic") is null)
    {
        return LibvirtDomainXmlValidationResult.Fail("features_missing");
    }

    XElement? devices = domain.Element("devices");
    if (devices is null)
    {
        return LibvirtDomainXmlValidationResult.Fail("devices_missing");
    }

    XElement[] disks = devices.Elements("disk").ToArray();
    XElement? disk = disks.Length == 1 ? disks[0] : null;
    if (disk is null ||
        !StringEquals(GetAttribute(disk, "type"), "file") ||
        !StringEquals(GetAttribute(disk, "device"), "disk"))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_invalid");
    }

    XElement? diskDriver = disk.Element("driver");
    string diskFormat = GetAttribute(diskDriver, "type");
    if (diskDriver is null ||
        !StringEquals(GetAttribute(diskDriver, "name"), "qemu") ||
        (!StringEquals(diskFormat, "raw") && !StringEquals(diskFormat, "qcow2")))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_driver_invalid");
    }

    string diskSource = GetAttribute(disk.Element("source"), "file");
    if (!IsRequiredSingleLine(diskSource))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_source_invalid");
    }

    XElement? diskTarget = disk.Element("target");
    if (diskTarget is null ||
        !StringEquals(GetAttribute(diskTarget, "dev"), "vda") ||
        !StringEquals(GetAttribute(diskTarget, "bus"), "virtio"))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_target_invalid");
    }

    XElement[] serialPorts = devices.Elements("serial").ToArray();
    XElement? serial = serialPorts.Length == 1 ? serialPorts[0] : null;
    if (serial is null || !StringEquals(GetAttribute(serial, "type"), "file"))
    {
        return LibvirtDomainXmlValidationResult.Fail("serial_invalid");
    }

    string serialLogPath = GetAttribute(serial.Element("source"), "path");
    if (!IsRequiredSingleLine(serialLogPath))
    {
        return LibvirtDomainXmlValidationResult.Fail("serial_source_invalid");
    }

    XElement? serialTarget = serial.Element("target");
    if (serialTarget is null ||
        !StringEquals(GetAttribute(serialTarget, "type"), "isa-serial") ||
        !StringEquals(GetAttribute(serialTarget, "port"), "0") ||
        !StringEquals(GetAttribute(serialTarget.Element("model"), "name"), "isa-serial"))
    {
        return LibvirtDomainXmlValidationResult.Fail("serial_target_invalid");
    }

    IReadOnlyList<XElement> interfaces = devices.Elements("interface").ToArray();
    string network = "none";
    if (interfaces.Count > 1)
    {
        return LibvirtDomainXmlValidationResult.Fail("network_invalid");
    }

    if (interfaces.Count == 1)
    {
        XElement networkInterface = interfaces[0];
        if (!StringEquals(GetAttribute(networkInterface, "type"), "network") ||
            !StringEquals(GetAttribute(networkInterface.Element("source"), "network"), "default") ||
            !StringEquals(GetAttribute(networkInterface.Element("model"), "type"), "virtio"))
        {
            return LibvirtDomainXmlValidationResult.Fail("network_invalid");
        }

        network = "default";
    }

    return LibvirtDomainXmlValidationResult.Pass(new LibvirtDomainXmlSummary(
        xmlByteCount,
        domainName,
        "kvm",
        memoryMiB,
        currentMemoryMiB,
        vcpuCount,
        "hvm",
        "x86_64",
        "q35",
        diskFormat,
        diskSource,
        serialLogPath,
        network,
        hasOvmfLoader,
        ovmfLoaderPath));
}

static void EmitDomainXmlSummary(LibvirtDomainXmlSummary summary)
{
    Console.WriteLine("LIBVIRT_XML_STATUS valid");
    Console.WriteLine("LIBVIRT_XML_BYTES " + summary.XmlByteCount.ToStringInvariant());
    Console.WriteLine("LIBVIRT_XML_DOMAIN name=" + summary.DomainName + " type=" + summary.DomainType);
    Console.WriteLine("LIBVIRT_XML_MEMORY mib=" + summary.MemoryMiB.ToStringInvariant() + " currentMib=" + summary.CurrentMemoryMiB.ToStringInvariant());
    Console.WriteLine("LIBVIRT_XML_VCPU count=" + summary.VCpuCount.ToStringInvariant());
    Console.WriteLine("LIBVIRT_XML_OS type=" + summary.OsType + " arch=" + summary.OsArch + " machine=" + summary.OsMachine);
    Console.WriteLine("LIBVIRT_XML_FEATURES acpi;apic");
    Console.WriteLine("LIBVIRT_XML_DISK format=" + summary.DiskFormat + " bus=virtio source=" + summary.DiskSource);
    Console.WriteLine("LIBVIRT_XML_SERIAL type=file target=isa-serial path=" + summary.SerialLogPath);
    Console.WriteLine("LIBVIRT_XML_NETWORK " + summary.Network);
    Console.WriteLine("LIBVIRT_XML_OVMF_LOADER configured=" + summary.HasOvmfLoader.ToDiagnosticString() + " path=" + summary.OvmfLoaderPath);
}

static bool TryReadMiBElement(XElement? element, out int value)
{
    value = 0;
    if (element is null || !StringEquals(GetAttribute(element, "unit"), "MiB"))
    {
        return false;
    }

    return TryReadPositiveIntText(element, out value);
}

static bool TryReadPositiveIntText(XElement? element, out int value)
{
    value = 0;
    return element is not null &&
        int.TryParse(
            element.Value.Trim(),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out value) &&
        value > 0;
}

static bool TryGetRequiredSingleLineText(XElement? element, out string value)
{
    value = element?.Value.Trim() ?? string.Empty;
    return IsRequiredSingleLine(value);
}

static string GetAttribute(XElement? element, string name)
{
    return element?.Attribute(name)?.Value.Trim() ?? string.Empty;
}

static bool IsRequiredSingleLine(string value)
{
    return !string.IsNullOrWhiteSpace(value) &&
        value.IndexOfAny(new[] { '\r', '\n' }) < 0;
}

static bool StringEquals(string actual, string expected)
{
    return string.Equals(actual, expected, StringComparison.Ordinal);
}

static int CheckSerialLog(LibvirtOptions options)
{
    Console.WriteLine("TEST_START libvirt.serial.summary");
    Console.WriteLine("LIBVIRT_SERIAL_LOG " + options.SerialLogPath);
    Console.WriteLine("REQUIRED_MARKERS " + FormatMarkers(options.RequiredMarkers));
    Console.WriteLine("FORBIDDEN_MARKERS " + FormatMarkers(options.ForbiddenMarkers));

    if (!File.Exists(options.SerialLogPath))
    {
        SerialMarkerSummary emptySummary = SerialMarkerSummary.Parse(
            string.Empty,
            options.RequiredMarkers,
            options.ForbiddenMarkers);
        EmitSerialSummary(options, "fail", emptySummary, "serial_log_not_found");
        Console.WriteLine("TEST_FAIL libvirt.serial.summary serial_log_not_found");
        return 3;
    }

    string serialText = File.ReadAllText(options.SerialLogPath, Encoding.UTF8);
    SerialMarkerSummary summary = SerialMarkerSummary.Parse(
        serialText,
        options.RequiredMarkers,
        options.ForbiddenMarkers);

    if (summary.HasForbiddenMarkers)
    {
        EmitSerialSummary(options, "fail", summary, "forbidden_marker");
        Console.WriteLine("TEST_FAIL libvirt.serial.summary forbidden_marker marker=" + summary.ForbiddenMarkerHits[0]);
        return 8;
    }

    if (!summary.HasAllRequiredMarkers)
    {
        EmitSerialSummary(options, "fail", summary, "missing_marker");
        Console.WriteLine("TEST_FAIL libvirt.serial.summary missing_marker missing=" + FormatMarkers(summary.MissingMarkers));
        return 5;
    }

    EmitSerialSummary(options, "pass", summary, "none");
    Console.WriteLine("TEST_PASS libvirt.serial.summary");
    return 0;
}

static void EmitSerialSummary(
    LibvirtOptions options,
    string status,
    SerialMarkerSummary summary,
    string failReason)
{
    string summaryText = BuildSerialSummaryText(options, status, summary, failReason);
    Console.Write(summaryText);
    WriteSummaryLogBestEffort(options, summaryText);
}

static string BuildSerialSummaryText(
    LibvirtOptions options,
    string status,
    SerialMarkerSummary summary,
    string failReason)
{
    StringBuilder builder = new StringBuilder();
    builder.Append("LIBVIRT_SERIAL_SUMMARY_STATUS ").AppendLine(status);
    builder.Append("SERIAL_LOG ").AppendLine(options.SerialLogPath);
    builder.Append("REQUIRED_MARKERS ").AppendLine(FormatMarkers(options.RequiredMarkers));
    builder.Append("FORBIDDEN_MARKERS ").AppendLine(FormatMarkers(options.ForbiddenMarkers));
    AppendObservedMarkerLines(builder, summary.ObservedMarkerLines);
    AppendAppExitSummary(builder, summary.AppExitResults, summary.MalformedAppExitMarkers);
    builder.Append("MISSING_MARKERS ").AppendLine(FormatMarkers(summary.MissingMarkers));
    builder.Append("FORBIDDEN_MARKER_HIT ").AppendLine(FormatMarkers(summary.ForbiddenMarkerHits));
    builder.Append("FAIL_REASON ").AppendLine(failReason);
    return builder.ToString();
}

static void AppendObservedMarkerLines(StringBuilder builder, IReadOnlyList<string> observedMarkers)
{
    if (observedMarkers.Count == 0)
    {
        builder.Append("OBSERVED_MARKER none").AppendLine();
        return;
    }

    foreach (string marker in observedMarkers)
    {
        builder.Append("OBSERVED_MARKER ").AppendLine(marker);
    }
}

static void AppendAppExitSummary(
    StringBuilder builder,
    IReadOnlyList<AppExitMarkerResult> appExitResults,
    IReadOnlyList<string> malformedAppExitMarkers)
{
    if (appExitResults.Count == 0)
    {
        builder.Append("APP_EXIT_RESULT none").AppendLine();
    }
    else
    {
        foreach (AppExitMarkerResult result in appExitResults)
        {
            builder.Append("APP_EXIT_RESULT code=")
                .Append(result.ExitCode.ToStringInvariant())
                .Append(" status=")
                .AppendLine(result.Status);
        }
    }

    if (malformedAppExitMarkers.Count == 0)
    {
        builder.Append("APP_EXIT_MALFORMED none").AppendLine();
        return;
    }

    foreach (string marker in malformedAppExitMarkers)
    {
        builder.Append("APP_EXIT_MALFORMED ").AppendLine(marker);
    }
}

static void WriteSummaryLogBestEffort(LibvirtOptions options, string summaryText)
{
    if (string.IsNullOrWhiteSpace(options.SummaryLogPath))
    {
        return;
    }

    try
    {
        string fullPath = Path.GetFullPath(options.SummaryLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        File.WriteAllText(fullPath, summaryText, Encoding.UTF8);
        Console.WriteLine("SUMMARY_LOG " + options.SummaryLogPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SUMMARY_LOG_WRITE_FAILED path=" + options.SummaryLogPath + " reason=" + ex.GetType().Name + ": " + ex.Message);
    }
}

static void EmitDiagnostics(LibvirtOptions options, string xml)
{
    string serialLogDirectory = options.GetSerialLogDirectory();

    Console.Error.WriteLine("LIBVIRT_DIAG domain.name=" + options.DomainName);
    Console.Error.WriteLine("LIBVIRT_DIAG memory.mib=" + options.MemoryMiB.ToStringInvariant());
    Console.Error.WriteLine("LIBVIRT_DIAG vcpus=" + options.VCpuCount.ToStringInvariant());
    Console.Error.WriteLine("LIBVIRT_DIAG network=" + options.Network.ToDiagnosticString());
    Console.Error.WriteLine("LIBVIRT_DIAG image.path=" + options.ImagePath);
    Console.Error.WriteLine("LIBVIRT_DIAG disk.format=" + options.DiskFormat);
    Console.Error.WriteLine("LIBVIRT_DIAG image.exists=" + File.Exists(options.ImagePath).ToDiagnosticString());
    Console.Error.WriteLine("LIBVIRT_DIAG image.bytes=" + options.GetImageByteCountDiagnostic());
    Console.Error.WriteLine("LIBVIRT_DIAG image.detected.format=" + options.GetDetectedImageFormatDiagnostic());
    Console.Error.WriteLine("LIBVIRT_DIAG serial.log.path=" + options.SerialLogPath);
    Console.Error.WriteLine("LIBVIRT_DIAG serial.log.directory=" + serialLogDirectory);
    Console.Error.WriteLine("LIBVIRT_DIAG serial.log.directory.exists=" + Directory.Exists(serialLogDirectory).ToDiagnosticString());
    Console.Error.WriteLine("LIBVIRT_DIAG timeout.seconds=" + options.TimeoutSeconds.ToStringInvariant());
    Console.Error.WriteLine("LIBVIRT_DIAG required.markers=" + FormatMarkers(options.RequiredMarkers));
    Console.Error.WriteLine("LIBVIRT_DIAG forbidden.markers=" + FormatMarkers(options.ForbiddenMarkers));
    Console.Error.WriteLine("LIBVIRT_DIAG ovmf.loader.configured=" + (!string.IsNullOrWhiteSpace(options.OvmfLoaderPath)).ToDiagnosticString());
    Console.Error.WriteLine("LIBVIRT_DIAG ovmf.loader.exists=" + (!string.IsNullOrWhiteSpace(options.OvmfLoaderPath) && File.Exists(options.OvmfLoaderPath)).ToDiagnosticString());
    Console.Error.WriteLine("LIBVIRT_DIAG xml.utf8.bytes=" + Encoding.UTF8.GetByteCount(xml).ToStringInvariant());
}

static string FormatMarkers(IReadOnlyList<string> markers)
{
    return markers.Count == 0
        ? "none"
        : string.Join(";", markers);
}

enum LibvirtNetwork
{
    None,
    Default
}

sealed class LibvirtOptions
{
    private static readonly string[] DefaultRequiredMarkers =
    {
        "OS_SMOKE_PASS",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY"
    };
    private readonly List<string> requiredMarkers = new List<string>();
    private readonly List<string> forbiddenMarkers = new List<string>();

    public string DomainName { get; private set; } = "os";

    public int MemoryMiB { get; private set; } = 256;

    public int VCpuCount { get; private set; } = 1;

    public string ImagePath { get; private set; } = "artifacts/images/os.raw";

    public string DiskFormat { get; private set; } = "raw";

    public string SerialLogPath { get; private set; } = "artifacts/logs/libvirt-serial.log";

    public string OvmfLoaderPath { get; private set; } = string.Empty;

    public LibvirtNetwork Network { get; private set; } = LibvirtNetwork.None;

    public int TimeoutSeconds { get; private set; } = 30;

    public IReadOnlyList<string> RequiredMarkers
    {
        get
        {
            return requiredMarkers.Count == 0
                ? DefaultRequiredMarkers
                : requiredMarkers;
        }
    }

    public IReadOnlyList<string> ForbiddenMarkers => forbiddenMarkers;

    public bool GenerateXml { get; private set; }

    public bool PlanLifecycle { get; private set; }

    public bool ValidateXml { get; private set; }

    public bool CheckSerial { get; private set; }

    public bool AllowRealLifecycle { get; private set; }

    public bool EmitDiagnostics { get; private set; }

    public bool RequireExistingPaths { get; private set; }

    public bool ShowHelp { get; private set; }

    public string SummaryLogPath { get; private set; } = string.Empty;

    public string DomainXmlPath { get; private set; } = string.Empty;

    public static LibvirtOptions Parse(string[] args)
    {
        LibvirtOptions options = new LibvirtOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "generate-xml":
                case "--dry-run":
                    options.GenerateXml = true;
                    break;
                case "plan-lifecycle":
                case "--plan-lifecycle":
                    options.PlanLifecycle = true;
                    break;
                case "validate-xml":
                case "--validate-xml":
                    options.ValidateXml = true;
                    break;
                case "check-serial":
                case "--check-serial":
                    options.CheckSerial = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--name":
                    options.DomainName = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--memory-mib":
                    options.MemoryMiB = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--vcpu":
                case "--vcpus":
                    options.VCpuCount = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--image":
                    options.ImagePath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--disk-format":
                    options.DiskFormat = ParseDiskFormat(ReadValue(args, ref i, arg));
                    break;
                case "--serial-log":
                    options.SerialLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--summary-log":
                    options.SummaryLogPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--domain-xml":
                case "--xml":
                    options.DomainXmlPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--ovmf-loader":
                case "--ovmf-code":
                    options.OvmfLoaderPath = RequireNonEmpty(ReadValue(args, ref i, arg), arg);
                    break;
                case "--network":
                    options.Network = ParseNetwork(ReadValue(args, ref i, arg));
                    break;
                case "--timeout-seconds":
                    options.TimeoutSeconds = ReadPositiveInt(args, ref i, arg);
                    break;
                case "--require-marker":
                    options.requiredMarkers.Add(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--forbid-marker":
                    options.forbiddenMarkers.Add(RequireNonEmpty(ReadValue(args, ref i, arg), arg));
                    break;
                case "--diagnostics":
                    options.EmitDiagnostics = true;
                    break;
                case "--validate-paths":
                    options.RequireExistingPaths = true;
                    break;
                case "--allow-real-libvirt-lifecycle":
                    options.AllowRealLifecycle = true;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("OS.LibvirtRunner generate-xml [--name <domain>] [--memory-mib <MiB>] [--vcpu <count>] [--image <raw|qcow2 image>] [--disk-format raw|qcow2] [--serial-log <path>] [--ovmf-loader <OVMF_CODE.fd>] [--network none|default] [--require-marker <marker>]... [--forbid-marker <marker>]... [--diagnostics] [--validate-paths]");
        Console.WriteLine("OS.LibvirtRunner plan-lifecycle emits a plan-only real-libvirt lifecycle contract and never starts a domain.");
        Console.WriteLine("OS.LibvirtRunner validate-xml --domain-xml <path> validates generated domain XML without starting a domain.");
        Console.WriteLine("OS.LibvirtRunner check-serial validates an existing serial log against required and forbidden marker contracts without starting a domain.");
        Console.WriteLine("OS.LibvirtRunner --dry-run uses the same XML generation mode and does not require libvirt.");
        Console.WriteLine("--diagnostics writes LIBVIRT_DIAG lines, including required and forbidden serial markers, to stderr while preserving XML on stdout.");
        Console.WriteLine("--summary-log writes the check-serial marker summary to a file.");
        Console.WriteLine("--timeout-seconds records the future serial-marker polling deadline for lifecycle planning.");
        Console.WriteLine("--allow-real-libvirt-lifecycle records explicit future opt-in intent; real lifecycle execution is still not implemented.");
        Console.WriteLine("--validate-paths requires the image, serial log directory, and configured OVMF loader to exist.");
    }

    public void ValidatePaths()
    {
        if (!RequireExistingPaths)
        {
            return;
        }

        if (!File.Exists(ImagePath))
        {
            throw new ArgumentException("Image file does not exist: " + ImagePath);
        }

        long imageByteCount = GetImageByteCount(ImagePath);
        if (imageByteCount == 0)
        {
            throw new ArgumentException("Image file is empty: " + ImagePath);
        }

        string detectedFormat = DetectImageFormat(ImagePath);
        if (string.Equals(DiskFormat, "qcow2", StringComparison.Ordinal) &&
            !string.Equals(detectedFormat, "qcow2", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Image disk format mismatch: expected qcow2 but detected "
                + detectedFormat
                + ": "
                + ImagePath);
        }

        if (string.Equals(DiskFormat, "raw", StringComparison.Ordinal) &&
            string.Equals(detectedFormat, "qcow2", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Image disk format mismatch: expected raw but detected qcow2: " + ImagePath);
        }

        string serialLogDirectory = GetSerialLogDirectory();
        if (!Directory.Exists(serialLogDirectory))
        {
            throw new ArgumentException("Serial log directory does not exist: " + serialLogDirectory);
        }

        if (!string.IsNullOrWhiteSpace(OvmfLoaderPath) && !File.Exists(OvmfLoaderPath))
        {
            throw new ArgumentException("OVMF loader file does not exist: " + OvmfLoaderPath);
        }
    }

    public string GetImageByteCountDiagnostic()
    {
        if (!File.Exists(ImagePath))
        {
            return "missing";
        }

        try
        {
            return GetImageByteCount(ImagePath).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentException ex)
        {
            return "error=" + ex.Message;
        }
    }

    public string GetDetectedImageFormatDiagnostic()
    {
        if (!File.Exists(ImagePath))
        {
            return "not_checked";
        }

        try
        {
            return DetectImageFormat(ImagePath);
        }
        catch (ArgumentException ex)
        {
            return "error=" + ex.Message;
        }
    }

    public string GetSerialLogDirectory()
    {
        string? directory = Path.GetDirectoryName(SerialLogPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Directory.GetCurrentDirectory();
        }

        return directory;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("Missing value for " + option);
        }

        index++;
        return args[index];
    }

    private static string RequireNonEmpty(string value, string option)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value for " + option + " cannot be empty.");
        }

        return value;
    }

    private static long GetImageByteCount(string imagePath)
    {
        try
        {
            return new FileInfo(imagePath).Length;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new ArgumentException("could_not_read_image_length error=" + ex.GetType().Name);
        }
    }

    private static string DetectImageFormat(string imagePath)
    {
        try
        {
            Span<byte> header = stackalloc byte[4];
            using FileStream stream = new FileStream(
                imagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            int bytesRead = stream.Read(header);
            if (bytesRead == 4 &&
                header[0] == (byte)'Q' &&
                header[1] == (byte)'F' &&
                header[2] == (byte)'I' &&
                header[3] == 0xFB)
            {
                return "qcow2";
            }

            return "raw-compatible";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new ArgumentException("could_not_read_image_header error=" + ex.GetType().Name);
        }
    }

    private static int ReadPositiveInt(string[] args, ref int index, string option)
    {
        string value = ReadValue(args, ref index, option);
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException("Value for " + option + " must be a positive integer.");
        }

        return parsed;
    }

    private static LibvirtNetwork ParseNetwork(string value)
    {
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return LibvirtNetwork.None;
        }

        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return LibvirtNetwork.Default;
        }

        throw new ArgumentException("Value for --network must be none or default.");
    }

    private static string ParseDiskFormat(string value)
    {
        if (string.Equals(value, "raw", StringComparison.OrdinalIgnoreCase))
        {
            return "raw";
        }

        if (string.Equals(value, "qcow2", StringComparison.OrdinalIgnoreCase))
        {
            return "qcow2";
        }

        throw new ArgumentException("Value for --disk-format must be raw or qcow2.");
    }
}

sealed class LibvirtLifecyclePlan
{
    private readonly LibvirtLifecyclePlanStep[] steps;

    private LibvirtLifecyclePlan(
        LibvirtOptions options,
        int xmlByteCount,
        LibvirtLifecyclePlanStep[] steps)
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

    public string DomainName { get; }

    public string ImagePath { get; }

    public string DiskFormat { get; }

    public string SerialLogPath { get; }

    public string Network { get; }

    public int TimeoutSeconds { get; }

    public IReadOnlyList<string> RequiredMarkers { get; }

    public IReadOnlyList<string> ForbiddenMarkers { get; }

    public bool AllowRealLifecycle { get; }

    public int XmlByteCount { get; }

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
            builder.Append("LIBVIRT_LIFECYCLE_STEP ")
                .Append(step.Ordinal.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                .Append(' ')
                .AppendLine(step.Name);
        }

        builder.Append("LIBVIRT_LIFECYCLE_CLEANUP_ORDER ")
            .AppendLine("destroy-domain-if-running;undefine-domain-if-persistent;cleanup-transient-artifacts");
        builder.AppendLine("LIBVIRT_LIFECYCLE_PLAN_READY");

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string FormatPlanMarkers(IReadOnlyList<string> markers)
    {
        return markers.Count == 0
            ? "none"
            : string.Join(";", markers);
    }
}

readonly struct LibvirtLifecyclePlanStep
{
    public LibvirtLifecyclePlanStep(int ordinal, string name)
    {
        Ordinal = ordinal;
        Name = name;
    }

    public int Ordinal { get; }

    public string Name { get; }
}

readonly struct LibvirtDomainXmlValidationResult
{
    private LibvirtDomainXmlValidationResult(
        bool succeeded,
        string status,
        LibvirtDomainXmlSummary summary)
    {
        Succeeded = succeeded;
        Status = status;
        Summary = summary;
    }

    public bool Succeeded { get; }

    public string Status { get; }

    public LibvirtDomainXmlSummary Summary { get; }

    public static LibvirtDomainXmlValidationResult Pass(LibvirtDomainXmlSummary summary)
    {
        return new LibvirtDomainXmlValidationResult(true, "valid", summary);
    }

    public static LibvirtDomainXmlValidationResult Fail(string status)
    {
        return new LibvirtDomainXmlValidationResult(false, status, default);
    }
}

readonly struct LibvirtDomainXmlSummary
{
    public LibvirtDomainXmlSummary(
        int xmlByteCount,
        string domainName,
        string domainType,
        int memoryMiB,
        int currentMemoryMiB,
        int vcpuCount,
        string osType,
        string osArch,
        string osMachine,
        string diskFormat,
        string diskSource,
        string serialLogPath,
        string network,
        bool hasOvmfLoader,
        string ovmfLoaderPath)
    {
        XmlByteCount = xmlByteCount;
        DomainName = domainName;
        DomainType = domainType;
        MemoryMiB = memoryMiB;
        CurrentMemoryMiB = currentMemoryMiB;
        VCpuCount = vcpuCount;
        OsType = osType;
        OsArch = osArch;
        OsMachine = osMachine;
        DiskFormat = diskFormat;
        DiskSource = diskSource;
        SerialLogPath = serialLogPath;
        Network = network;
        HasOvmfLoader = hasOvmfLoader;
        OvmfLoaderPath = ovmfLoaderPath;
    }

    public int XmlByteCount { get; }

    public string DomainName { get; }

    public string DomainType { get; }

    public int MemoryMiB { get; }

    public int CurrentMemoryMiB { get; }

    public int VCpuCount { get; }

    public string OsType { get; }

    public string OsArch { get; }

    public string OsMachine { get; }

    public string DiskFormat { get; }

    public string DiskSource { get; }

    public string SerialLogPath { get; }

    public string Network { get; }

    public bool HasOvmfLoader { get; }

    public string OvmfLoaderPath { get; }
}

static class LibvirtNetworkExtensions
{
    public static string ToDiagnosticString(this LibvirtNetwork value)
    {
        return value == LibvirtNetwork.Default ? "default" : "none";
    }
}

static class IntExtensions
{
    public static string ToStringInvariant(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

static class BoolExtensions
{
    public static string ToDiagnosticString(this bool value)
    {
        return value ? "true" : "false";
    }
}
