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
    XElement os = new XElement("os", new XElement("type", new XAttribute("arch", "x86_64"), new XAttribute("machine", "q35"), "hvm"), new XElement("boot", new XAttribute("dev", "hd")));
    if (!string.IsNullOrWhiteSpace(options.OvmfLoaderPath))
    {
        os.Element("type")?.AddAfterSelf(new XElement("loader", new XAttribute("readonly", "yes"), new XAttribute("type", "pflash"), options.OvmfLoaderPath));
    }

    XElement devices = new XElement("devices", new XElement("disk", new XAttribute("type", "file"), new XAttribute("device", "disk"), new XElement("driver", new XAttribute("name", "qemu"), new XAttribute("type", options.DiskFormat)), new XElement("source", new XAttribute("file", options.ImagePath)), new XElement("target", new XAttribute("dev", "vda"), new XAttribute("bus", "virtio"))), new XElement("serial", new XAttribute("type", "file"), new XElement("source", new XAttribute("path", options.SerialLogPath)), new XElement("target", new XAttribute("type", "isa-serial"), new XAttribute("port", "0"), new XElement("model", new XAttribute("name", "isa-serial")))));
    if (options.Network == LibvirtNetwork.Default)
    {
        devices.Add(new XElement("interface", new XAttribute("type", "network"), new XElement("source", new XAttribute("network", "default")), new XElement("model", new XAttribute("type", "virtio"))));
    }

    XElement domain = new XElement("domain", new XAttribute("type", "kvm"), new XElement("name", options.DomainName), new XElement("memory", new XAttribute("unit", "MiB"), options.MemoryMiB.ToStringInvariant()), new XElement("currentMemory", new XAttribute("unit", "MiB"), options.MemoryMiB.ToStringInvariant()), new XElement("vcpu", new XAttribute("placement", "static"), options.VCpuCount.ToStringInvariant()), os, new XElement("features", new XElement("acpi"), new XElement("apic")), devices);
    StringBuilder builder = new StringBuilder();
    using XmlWriter writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, NewLineChars = "\n", OmitXmlDeclaration = true });
    domain.Save(writer);
    writer.Flush();
    return builder.ToString();
}

static string BuildLifecyclePlan(LibvirtOptions options, string xml)
{
    LibvirtLifecyclePlan plan = LibvirtLifecyclePlan.Create(options, Encoding.UTF8.GetByteCount(xml));
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
    if (osType is null || !StringEquals(osType.Value.Trim(), "hvm") || !StringEquals(GetAttribute(osType, "arch"), "x86_64") || !StringEquals(GetAttribute(osType, "machine"), "q35"))
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
        if (!StringEquals(GetAttribute(loader, "readonly"), "yes") || !StringEquals(GetAttribute(loader, "type"), "pflash") || !TryGetRequiredSingleLineText(loader, out ovmfLoaderPath))
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
    if (disk is null || !StringEquals(GetAttribute(disk, "type"), "file") || !StringEquals(GetAttribute(disk, "device"), "disk"))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_invalid");
    }

    XElement? diskDriver = disk.Element("driver");
    string diskFormat = GetAttribute(diskDriver, "type");
    if (diskDriver is null || !StringEquals(GetAttribute(diskDriver, "name"), "qemu") || (!StringEquals(diskFormat, "raw") && !StringEquals(diskFormat, "qcow2")))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_driver_invalid");
    }

    string diskSource = GetAttribute(disk.Element("source"), "file");
    if (!IsRequiredSingleLine(diskSource))
    {
        return LibvirtDomainXmlValidationResult.Fail("disk_source_invalid");
    }

    XElement? diskTarget = disk.Element("target");
    if (diskTarget is null || !StringEquals(GetAttribute(diskTarget, "dev"), "vda") || !StringEquals(GetAttribute(diskTarget, "bus"), "virtio"))
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
    if (serialTarget is null || !StringEquals(GetAttribute(serialTarget, "type"), "isa-serial") || !StringEquals(GetAttribute(serialTarget, "port"), "0") || !StringEquals(GetAttribute(serialTarget.Element("model"), "name"), "isa-serial"))
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
        if (!StringEquals(GetAttribute(networkInterface, "type"), "network") || !StringEquals(GetAttribute(networkInterface.Element("source"), "network"), "default") || !StringEquals(GetAttribute(networkInterface.Element("model"), "type"), "virtio"))
        {
            return LibvirtDomainXmlValidationResult.Fail("network_invalid");
        }

        network = "default";
    }

    return LibvirtDomainXmlValidationResult.Pass(new LibvirtDomainXmlSummary(xmlByteCount, domainName, "kvm", memoryMiB, currentMemoryMiB, vcpuCount, "hvm", "x86_64", "q35", diskFormat, diskSource, serialLogPath, network, hasOvmfLoader, ovmfLoaderPath));
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
    return element is not null && int.TryParse(element.Value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
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
    return !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(new[] { '\r', '\n' }) < 0;
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
        SerialMarkerSummary emptySummary = SerialMarkerSummary.Parse(string.Empty, options.RequiredMarkers, options.ForbiddenMarkers);
        EmitSerialSummary(options, "fail", emptySummary, "serial_log_not_found");
        Console.WriteLine("TEST_FAIL libvirt.serial.summary serial_log_not_found");
        return 3;
    }

    string serialText = File.ReadAllText(options.SerialLogPath, Encoding.UTF8);
    SerialMarkerSummary summary = SerialMarkerSummary.Parse(serialText, options.RequiredMarkers, options.ForbiddenMarkers);
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

static void EmitSerialSummary(LibvirtOptions options, string status, SerialMarkerSummary summary, string failReason)
{
    string summaryText = BuildSerialSummaryText(options, status, summary, failReason);
    Console.Write(summaryText);
    WriteSummaryLogBestEffort(options, summaryText);
}

static string BuildSerialSummaryText(LibvirtOptions options, string status, SerialMarkerSummary summary, string failReason)
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

static void AppendAppExitSummary(StringBuilder builder, IReadOnlyList<AppExitMarkerResult> appExitResults, IReadOnlyList<string> malformedAppExitMarkers)
{
    if (appExitResults.Count == 0)
    {
        builder.Append("APP_EXIT_RESULT none").AppendLine();
    }
    else
    {
        foreach (AppExitMarkerResult result in appExitResults)
        {
            builder.Append("APP_EXIT_RESULT code=").Append(result.ExitCode.ToStringInvariant()).Append(" status=").AppendLine(result.Status);
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
    return markers.Count == 0 ? "none" : string.Join(";", markers);
}
