using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class LibvirtRunnerTests
{
    [HostFixture(HostLane.Native)]
    public static void Initialize()
    {
        int failures = 0;

        Run("libvirt generate-xml emits deterministic network-none XML", TestGenerateXmlNetworkNone, ref failures);
        Run("libvirt --dry-run emits OVMF and default-network XML", TestDryRunOvmfDefaultNetwork, ref failures);
        Run("libvirt --dry-run emits qcow2 disk XML", TestDryRunQcow2DiskFormat, ref failures);
        Run("libvirt rejects invalid disk format", TestRejectsInvalidDiskFormat, ref failures);
        Run("libvirt validate-xml summarizes generated XML", TestValidateXmlSummarizesGeneratedXml, ref failures);
        Run("libvirt validate-xml rejects invalid disk XML", TestValidateXmlRejectsInvalidDiskXml, ref failures);
        Run("libvirt lifecycle plan documents safe real-run order", TestLifecyclePlanDocumentsSafeRealRunOrder, ref failures);
        Run("libvirt smoke script documents dry-run and missing-tool evidence", TestSmokeScriptDocumentsEvidence, ref failures);
        Run("libvirt diagnostics report real-run path readiness", TestDiagnosticsReportPathReadiness, ref failures);
        Run("libvirt diagnostics emit marker requirements", TestDiagnosticsEmitMarkerRequirements, ref failures);
        Run("libvirt validate-paths rejects missing image", TestValidatePathsRejectsMissingImage, ref failures);
        Run("libvirt validate-paths reports image format diagnostics", TestValidatePathsReportsImageFormatDiagnostics, ref failures);
        Run("libvirt validate-paths rejects raw qcow2 mismatch", TestValidatePathsRejectsRawQcow2Mismatch, ref failures);
        Run("libvirt validate-paths rejects qcow2 raw mismatch", TestValidatePathsRejectsQcow2RawMismatch, ref failures);
        Run("libvirt validate-paths rejects empty image", TestValidatePathsRejectsEmptyImage, ref failures);
        Run("libvirt check-serial passes and writes summary", TestCheckSerialPassesAndWritesSummary, ref failures);
        Run("libvirt check-serial rejects missing marker", TestCheckSerialRejectsMissingMarker, ref failures);
        Run("libvirt check-serial rejects forbidden marker", TestCheckSerialRejectsForbiddenMarker, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL libvirt.runner.tests failures=" + failures);
            Environment.Exit(1);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestGenerateXmlNetworkNone()
    {
        ProcessResult result = RunLibvirt(
            "generate-xml",
            "--name",
            "os-test",
            "--memory-mib",
            "768",
            "--vcpu",
            "2",
            "--image",
            "artifacts/images/os.raw",
            "--serial-log",
            "artifacts/logs/os-serial.log",
            "--network",
            "none");

        string expected = """
            <domain type="kvm">
              <name>os-test</name>
              <memory unit="MiB">768</memory>
              <currentMemory unit="MiB">768</currentMemory>
              <vcpu placement="static">2</vcpu>
              <os>
                <type arch="x86_64" machine="q35">hvm</type>
                <boot dev="hd" />
              </os>
              <features>
                <acpi />
                <apic />
              </features>
              <devices>
                <disk type="file" device="disk">
                  <driver name="qemu" type="raw" />
                  <source file="artifacts/images/os.raw" />
                  <target dev="vda" bus="virtio" />
                </disk>
                <serial type="file">
                  <source path="artifacts/logs/os-serial.log" />
                  <target type="isa-serial" port="0">
                    <model name="isa-serial" />
                  </target>
                </serial>
              </devices>
            </domain>
            """;

        return result.ExitCode == 0
            && string.IsNullOrWhiteSpace(result.StandardError)
            && Normalize(result.StandardOutput) == Normalize(expected);
    }

    private static bool TestDryRunOvmfDefaultNetwork()
    {
        ProcessResult result = RunLibvirt(
            "--dry-run",
            "--name",
            "os-uefi",
            "--memory-mib",
            "1024",
            "--vcpus",
            "4",
            "--image",
            @"C:\vm\os.raw",
            "--serial-log",
            @"C:\vm\serial.log",
            "--ovmf-loader",
            @"C:\firmware\OVMF_CODE.fd",
            "--network",
            "default");

        string xml = Normalize(result.StandardOutput);

        return result.ExitCode == 0
            && string.IsNullOrWhiteSpace(result.StandardError)
            && xml.Contains("<name>os-uefi</name>", StringComparison.Ordinal)
            && xml.Contains("<memory unit=\"MiB\">1024</memory>", StringComparison.Ordinal)
            && xml.Contains("<vcpu placement=\"static\">4</vcpu>", StringComparison.Ordinal)
            && xml.Contains("<loader readonly=\"yes\" type=\"pflash\">C:\\firmware\\OVMF_CODE.fd</loader>", StringComparison.Ordinal)
            && xml.Contains("<source file=\"C:\\vm\\os.raw\" />", StringComparison.Ordinal)
            && xml.Contains("<source path=\"C:\\vm\\serial.log\" />", StringComparison.Ordinal)
            && xml.Contains("<interface type=\"network\">", StringComparison.Ordinal)
            && xml.Contains("<source network=\"default\" />", StringComparison.Ordinal)
            && xml.Contains("<model type=\"virtio\" />", StringComparison.Ordinal);
    }

    private static bool TestDryRunQcow2DiskFormat()
    {
        ProcessResult result = RunLibvirt(
            "--dry-run",
            "--image",
            "artifacts/images/os.qcow2",
            "--disk-format",
            "qcow2",
            "--serial-log",
            "artifacts/logs/libvirt-qcow2.log");

        string xml = Normalize(result.StandardOutput);

        return result.ExitCode == 0
            && string.IsNullOrWhiteSpace(result.StandardError)
            && xml.Contains("<driver name=\"qemu\" type=\"qcow2\" />", StringComparison.Ordinal)
            && xml.Contains("<source file=\"artifacts/images/os.qcow2\" />", StringComparison.Ordinal);
    }

    private static bool TestRejectsInvalidDiskFormat()
    {
        ProcessResult result = RunLibvirt(
            "--dry-run",
            "--image",
            "artifacts/images/os.raw",
            "--disk-format",
            "vmdk");

        return result.ExitCode == 1
            && result.StandardError.Contains("Value for --disk-format must be raw or qcow2.", StringComparison.Ordinal);
    }

    private static bool TestValidateXmlSummarizesGeneratedXml()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string domainXmlPath = Path.Combine(tempDirectory, "domain.xml");
            string imagePath = Path.Combine(tempDirectory, "os.qcow2");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            string ovmfPath = Path.Combine(tempDirectory, "OVMF_CODE.fd");

            ProcessResult generated = RunLibvirt(
                "generate-xml",
                "--name",
                "os-xml-check",
                "--memory-mib",
                "384",
                "--vcpu",
                "2",
                "--image",
                imagePath,
                "--disk-format",
                "qcow2",
                "--serial-log",
                serialLogPath,
                "--ovmf-loader",
                ovmfPath,
                "--network",
                "default");

            if (generated.ExitCode != 0)
            {
                return false;
            }

            File.WriteAllText(domainXmlPath, generated.StandardOutput);

            ProcessResult result = RunLibvirt(
                "validate-xml",
                "--domain-xml",
                domainXmlPath);

            string output = Normalize(result.StandardOutput);

            return result.ExitCode == 0
                && string.IsNullOrWhiteSpace(result.StandardError)
                && output.Contains("TEST_START libvirt.xml.validation", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_PATH " + domainXmlPath, StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_STATUS valid", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_DOMAIN name=os-xml-check type=kvm", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_MEMORY mib=384 currentMib=384", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_VCPU count=2", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_OS type=hvm arch=x86_64 machine=q35", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_DISK format=qcow2 bus=virtio source=" + imagePath, StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_SERIAL type=file target=isa-serial path=" + serialLogPath, StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_NETWORK default", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_XML_OVMF_LOADER configured=true path=" + ovmfPath, StringComparison.Ordinal)
                && output.Contains("TEST_PASS libvirt.xml.validation", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidateXmlRejectsInvalidDiskXml()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string domainXmlPath = Path.Combine(tempDirectory, "domain.xml");
            ProcessResult generated = RunLibvirt(
                "generate-xml",
                "--image",
                Path.Combine(tempDirectory, "os.raw"),
                "--serial-log",
                Path.Combine(tempDirectory, "serial.log"));

            if (generated.ExitCode != 0)
            {
                return false;
            }

            string invalidXml = generated.StandardOutput.Replace(
                "<driver name=\"qemu\" type=\"raw\" />",
                "<driver name=\"qemu\" type=\"vmdk\" />",
                StringComparison.Ordinal);
            File.WriteAllText(domainXmlPath, invalidXml);

            ProcessResult result = RunLibvirt(
                "validate-xml",
                "--domain-xml",
                domainXmlPath);

            string output = Normalize(result.StandardOutput);

            return result.ExitCode == 4
                && string.IsNullOrWhiteSpace(result.StandardError)
                && output.Contains("TEST_START libvirt.xml.validation", StringComparison.Ordinal)
                && output.Contains("TEST_FAIL libvirt.xml.validation disk_driver_invalid", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestLifecyclePlanDocumentsSafeRealRunOrder()
    {
        ProcessResult result = RunLibvirt(
            "plan-lifecycle",
            "--name",
            "os-plan",
            "--image",
            "artifacts/images/os.qcow2",
            "--disk-format",
            "qcow2",
            "--serial-log",
            "artifacts/logs/libvirt-plan.log",
            "--network",
            "default",
            "--timeout-seconds",
            "45",
            "--require-marker",
            "OS_SMOKE_PASS",
            "--require-marker",
            "BOOT_HANDOFF_READY",
            "--forbid-marker",
            "OS_SMOKE_FAIL");

        string plan = Normalize(result.StandardOutput);
        int generateStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 01 generate-domain-xml", StringComparison.Ordinal);
        int createStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 04 create-transient-domain-from-xml", StringComparison.Ordinal);
        int startStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 05 start-domain", StringComparison.Ordinal);
        int pollStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 06 poll-serial-log-for-required-markers", StringComparison.Ordinal);
        int timeoutStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 08 enforce-timeout", StringComparison.Ordinal);
        int destroyStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 10 destroy-domain-if-running", StringComparison.Ordinal);
        int cleanupStep = plan.IndexOf("LIBVIRT_LIFECYCLE_STEP 12 cleanup-transient-artifacts", StringComparison.Ordinal);

        return result.ExitCode == 0
            && string.IsNullOrWhiteSpace(result.StandardError)
            && plan.Contains("LIBVIRT_LIFECYCLE_PLAN domain=os-plan", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_MODE plan-only", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_DEFAULT_START_DOMAIN false", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_REQUIRED_OPT_IN_SWITCH --allow-real-libvirt-lifecycle", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_DISK_FORMAT qcow2", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_NETWORK default", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_TIMEOUT_SECONDS 45", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_REQUIRED_MARKERS OS_SMOKE_PASS;BOOT_HANDOFF_READY", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_FORBIDDEN_MARKERS OS_SMOKE_FAIL", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_CLEANUP_ORDER destroy-domain-if-running;undefine-domain-if-persistent;cleanup-transient-artifacts", StringComparison.Ordinal)
            && plan.Contains("LIBVIRT_LIFECYCLE_PLAN_READY", StringComparison.Ordinal)
            && generateStep >= 0
            && createStep >= 0
            && startStep >= 0
            && pollStep >= 0
            && timeoutStep >= 0
            && destroyStep >= 0
            && cleanupStep >= 0
            && generateStep < createStep
            && createStep < startStep
            && startStep < pollStep
            && timeoutStep < destroyStep
            && destroyStep < cleanupStep;
    }

    private static bool TestSmokeScriptDocumentsEvidence()
    {
        return LibvirtSmokeTests.ValidateDefaultDryRun();
    }

    private static bool TestDiagnosticsReportPathReadiness()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            string ovmfPath = Path.Combine(tempDirectory, "OVMF_CODE.fd");

            WriteRawCompatibleImage(imagePath);
            File.WriteAllBytes(ovmfPath, Array.Empty<byte>());

            ProcessResult result = RunLibvirt(
                "generate-xml",
                "--name",
                "os-diag",
                "--image",
                imagePath,
                "--serial-log",
                serialLogPath,
                "--ovmf-loader",
                ovmfPath,
                "--network",
                "default",
                "--diagnostics",
                "--validate-paths");

            return result.ExitCode == 0
                && Normalize(result.StandardOutput).Contains("<name>os-diag</name>", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG domain.name=os-diag", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG network=default", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG image.exists=true", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG image.bytes=8", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG image.detected.format=raw-compatible", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG serial.log.directory.exists=true", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG ovmf.loader.configured=true", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG ovmf.loader.exists=true", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG xml.utf8.bytes=", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestDiagnosticsEmitMarkerRequirements()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            WriteRawCompatibleImage(imagePath);

            ProcessResult defaultResult = RunLibvirt(
                "generate-xml",
                "--image",
                imagePath,
                "--serial-log",
                serialLogPath,
                "--diagnostics",
                "--validate-paths");

            ProcessResult customResult = RunLibvirt(
                "generate-xml",
                "--image",
                imagePath,
                "--serial-log",
                serialLogPath,
                "--require-marker",
                "OS_SMOKE_PASS",
                "--require-marker",
                "APP_INVOCATION_READY",
                "--forbid-marker",
                "OS_SMOKE_FAIL",
                "--diagnostics",
                "--validate-paths");

            return defaultResult.ExitCode == 0
                && Normalize(defaultResult.StandardOutput).Contains("<domain type=\"kvm\">", StringComparison.Ordinal)
                && defaultResult.StandardError.Contains("LIBVIRT_DIAG required.markers=OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY", StringComparison.Ordinal)
                && defaultResult.StandardError.Contains("LIBVIRT_DIAG forbidden.markers=none", StringComparison.Ordinal)
                && customResult.ExitCode == 0
                && customResult.StandardError.Contains("LIBVIRT_DIAG required.markers=OS_SMOKE_PASS;APP_INVOCATION_READY", StringComparison.Ordinal)
                && customResult.StandardError.Contains("LIBVIRT_DIAG forbidden.markers=OS_SMOKE_FAIL", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidatePathsRejectsMissingImage()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string missingImagePath = Path.Combine(tempDirectory, "missing.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");

            ProcessResult result = RunLibvirt(
                "--dry-run",
                "--image",
                missingImagePath,
                "--serial-log",
                serialLogPath,
                "--validate-paths");

            return result.ExitCode == 1
                && string.IsNullOrWhiteSpace(result.StandardOutput)
                && result.StandardError.Contains("Image file does not exist: " + missingImagePath, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidatePathsReportsImageFormatDiagnostics()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            WriteRawCompatibleImage(imagePath);

            ProcessResult result = RunLibvirt(
                "generate-xml",
                "--image",
                imagePath,
                "--serial-log",
                serialLogPath,
                "--diagnostics",
                "--validate-paths");

            return result.ExitCode == 0
                && Normalize(result.StandardOutput).Contains("<source file=\"" + imagePath + "\" />", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG image.bytes=8", StringComparison.Ordinal)
                && result.StandardError.Contains("LIBVIRT_DIAG image.detected.format=raw-compatible", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidatePathsRejectsRawQcow2Mismatch()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            WriteQcow2HeaderImage(imagePath);

            ProcessResult result = RunLibvirt(
                "--dry-run",
                "--image",
                imagePath,
                "--disk-format",
                "raw",
                "--serial-log",
                serialLogPath,
                "--validate-paths");

            return result.ExitCode == 1
                && string.IsNullOrWhiteSpace(result.StandardOutput)
                && result.StandardError.Contains(
                    "Image disk format mismatch: expected raw but detected qcow2: " + imagePath,
                    StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidatePathsRejectsQcow2RawMismatch()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.qcow2");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            WriteRawCompatibleImage(imagePath);

            ProcessResult result = RunLibvirt(
                "--dry-run",
                "--image",
                imagePath,
                "--disk-format",
                "qcow2",
                "--serial-log",
                serialLogPath,
                "--validate-paths");

            return result.ExitCode == 1
                && string.IsNullOrWhiteSpace(result.StandardOutput)
                && result.StandardError.Contains(
                    "Image disk format mismatch: expected qcow2 but detected raw-compatible: " + imagePath,
                    StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestValidatePathsRejectsEmptyImage()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string imagePath = Path.Combine(tempDirectory, "os.raw");
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            File.WriteAllBytes(imagePath, Array.Empty<byte>());

            ProcessResult result = RunLibvirt(
                "--dry-run",
                "--image",
                imagePath,
                "--serial-log",
                serialLogPath,
                "--validate-paths");

            return result.ExitCode == 1
                && string.IsNullOrWhiteSpace(result.StandardOutput)
                && result.StandardError.Contains("Image file is empty: " + imagePath, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestCheckSerialPassesAndWritesSummary()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            string summaryLogPath = Path.Combine(tempDirectory, "summary.log");
            File.WriteAllText(
                serialLogPath,
                """
                firmware noise
                OS_SMOKE_PASS
                BOOT_HANDOFF_READY
                APP_EXIT code=0 status=OSHOST_STATUS_SUCCESS
                """);

            ProcessResult result = RunLibvirt(
                "check-serial",
                "--serial-log",
                serialLogPath,
                "--summary-log",
                summaryLogPath,
                "--require-marker",
                "OS_SMOKE_PASS",
                "--require-marker",
                "BOOT_HANDOFF_READY",
                "--forbid-marker",
                "OS_SMOKE_FAIL");

            string output = Normalize(result.StandardOutput);
            string summary = File.Exists(summaryLogPath)
                ? Normalize(File.ReadAllText(summaryLogPath))
                : string.Empty;

            return result.ExitCode == 0
                && string.IsNullOrWhiteSpace(result.StandardError)
                && output.Contains("TEST_START libvirt.serial.summary", StringComparison.Ordinal)
                && output.Contains("LIBVIRT_SERIAL_SUMMARY_STATUS pass", StringComparison.Ordinal)
                && output.Contains("OBSERVED_MARKER OS_SMOKE_PASS", StringComparison.Ordinal)
                && output.Contains("OBSERVED_MARKER BOOT_HANDOFF_READY", StringComparison.Ordinal)
                && output.Contains("APP_EXIT_RESULT code=0 status=OSHOST_STATUS_SUCCESS", StringComparison.Ordinal)
                && output.Contains("MISSING_MARKERS none", StringComparison.Ordinal)
                && output.Contains("FORBIDDEN_MARKER_HIT none", StringComparison.Ordinal)
                && output.Contains("FAIL_REASON none", StringComparison.Ordinal)
                && output.Contains("SUMMARY_LOG " + summaryLogPath, StringComparison.Ordinal)
                && output.Contains("TEST_PASS libvirt.serial.summary", StringComparison.Ordinal)
                && summary.Contains("LIBVIRT_SERIAL_SUMMARY_STATUS pass", StringComparison.Ordinal)
                && summary.Contains("SERIAL_LOG " + serialLogPath, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestCheckSerialRejectsMissingMarker()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            string summaryLogPath = Path.Combine(tempDirectory, "summary.log");
            File.WriteAllText(serialLogPath, "OS_SMOKE_PASS" + Environment.NewLine);

            ProcessResult result = RunLibvirt(
                "check-serial",
                "--serial-log",
                serialLogPath,
                "--summary-log",
                summaryLogPath,
                "--require-marker",
                "OS_SMOKE_PASS",
                "--require-marker",
                "BOOT_HANDOFF_READY");

            string output = Normalize(result.StandardOutput);
            string summary = File.Exists(summaryLogPath)
                ? Normalize(File.ReadAllText(summaryLogPath))
                : string.Empty;

            return result.ExitCode == 5
                && string.IsNullOrWhiteSpace(result.StandardError)
                && output.Contains("LIBVIRT_SERIAL_SUMMARY_STATUS fail", StringComparison.Ordinal)
                && output.Contains("MISSING_MARKERS BOOT_HANDOFF_READY", StringComparison.Ordinal)
                && output.Contains("FAIL_REASON missing_marker", StringComparison.Ordinal)
                && output.Contains("TEST_FAIL libvirt.serial.summary missing_marker missing=BOOT_HANDOFF_READY", StringComparison.Ordinal)
                && summary.Contains("MISSING_MARKERS BOOT_HANDOFF_READY", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TestCheckSerialRejectsForbiddenMarker()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "os-libvirt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string serialLogPath = Path.Combine(tempDirectory, "serial.log");
            string summaryLogPath = Path.Combine(tempDirectory, "summary.log");
            File.WriteAllText(
                serialLogPath,
                "OS_SMOKE_PASS" + Environment.NewLine + "OS_SMOKE_FAIL" + Environment.NewLine);

            ProcessResult result = RunLibvirt(
                "check-serial",
                "--serial-log",
                serialLogPath,
                "--summary-log",
                summaryLogPath,
                "--require-marker",
                "OS_SMOKE_PASS",
                "--forbid-marker",
                "OS_SMOKE_FAIL");

            string output = Normalize(result.StandardOutput);
            string summary = File.Exists(summaryLogPath)
                ? Normalize(File.ReadAllText(summaryLogPath))
                : string.Empty;

            return result.ExitCode == 8
                && string.IsNullOrWhiteSpace(result.StandardError)
                && output.Contains("LIBVIRT_SERIAL_SUMMARY_STATUS fail", StringComparison.Ordinal)
                && output.Contains("FORBIDDEN_MARKER_HIT OS_SMOKE_FAIL", StringComparison.Ordinal)
                && output.Contains("FAIL_REASON forbidden_marker", StringComparison.Ordinal)
                && output.Contains("TEST_FAIL libvirt.serial.summary forbidden_marker marker=OS_SMOKE_FAIL", StringComparison.Ordinal)
                && summary.Contains("FORBIDDEN_MARKER_HIT OS_SMOKE_FAIL", StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ProcessResult RunLibvirt(params string[] args)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(repositoryRoot, "OS.LibvirtRunner");
        if (!File.Exists(toolPath))
        {
            return new ProcessResult(-1, string.Empty, "libvirt runner tool not found: " + toolPath);
        }

        ProcessStartInfo startInfo = HostToolProcess.CreateDotnetToolStartInfo(repositoryRoot, "OS.LibvirtRunner");

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        bool exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("OS.LibvirtRunner did not exit within 30 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n');
    }

    private static void WriteRawCompatibleImage(string path)
    {
        File.WriteAllBytes(path, new byte[] { (byte)'O', (byte)'S', (byte)'R', (byte)'A', (byte)'W', 1, 2, 3 });
    }

    private static void WriteQcow2HeaderImage(string path)
    {
        File.WriteAllBytes(path, new byte[] { (byte)'Q', (byte)'F', (byte)'I', 0xFB, 0, 0, 0, 3 });
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }
    }
}
