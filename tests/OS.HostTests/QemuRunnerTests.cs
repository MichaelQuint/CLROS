using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class QemuRunnerTests
{
    private const string DefaultBootRequiredMarkers = "OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS";
    private const string SmokeScriptDefaultRequiredMarkers =
        DefaultBootRequiredMarkers
        + ";UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY";
    private static readonly string[] M01StageOneGuestMarkers =
    [
        "TEST_START m01.stage1",
        "M01_LOADED_IMAGE_CAPTURED",
        "M01_STACK_SWITCH_STUB_READY",
        "M01_STACK_ARENA_ALLOCATED",
        "M01_STACK_GUARD_RESERVED",
        "M01_STAGE1_MAP_RECAPTURED",
        "M01_STAGE1_RESERVATIONS_VALIDATED",
        "M01_OWNED_STACK_READY",
        "M01_OWNED_STACK_SWITCHED",
        "M01_RSP_CONTAINED",
        "TEST_PASS m01.stage1",
        "OS_SMOKE_PASS"
    ];

    [ModuleInitializer]
    public static void Initialize()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-qemurunner-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            Run("qemu dry-run preserves command construction", () => TestDryRunCommandConstruction(tempRoot), ref failures);
            Run("qemu dry-run emits serial log arguments", () => TestDryRunSerialLogArguments(tempRoot), ref failures);
            Run("qemu dry-run emits OVMF path arguments", () => TestDryRunOvmfArguments(tempRoot), ref failures);
            Run("qemu preflight validates raw image artifact", () => TestPreflightValidatesRawImageArtifact(tempRoot), ref failures);
            Run("qemu preflight rejects missing image artifact", () => TestPreflightRejectsMissingImageArtifact(tempRoot), ref failures);
            Run("qemu preflight rejects disk format mismatch", () => TestPreflightRejectsDiskFormatMismatch(tempRoot), ref failures);
            Run("qemu dry-run emits marker requirements", () => TestDryRunMarkerRequirements(tempRoot), ref failures);
            Run("qemu.smoke.preset default preserves current markers", () => TestDryRunMarkerPresetDefault(tempRoot), ref failures);
            Run("qemu.smoke.preset acpi adds validation and MCFG markers", () => TestDryRunAcpiMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset app args adds argument-vector marker", () => TestDryRunAppArgsMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset app args aliases normalize", () => TestDryRunAppArgsMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset app exit adds observer readiness marker", () => TestDryRunAppExitMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset app lifecycle adds lifecycle marker", () => TestDryRunAppLifecycleMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset app lifecycle aliases normalize", () => TestDryRunAppLifecycleMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset app selection adds selection marker", () => TestDryRunAppSelectionMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset app selection aliases normalize", () => TestDryRunAppSelectionMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset cctor preflight adds runtime readiness marker", () => TestDryRunCctorPreflightMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset cctor work items adds runtime readiness marker", () => TestDryRunCctorWorkItemsMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset cctor work items aliases normalize", () => TestDryRunCctorWorkItemsMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset diagnostics adds readiness chain markers", () => TestDryRunDiagnosticsMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset uefi gone adds denial and owned serial markers", () => TestDryRunUefiGoneMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset fetch http adds FetchApp completion marker", () => TestDryRunFetchHttpMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset fetch http endpoint adds endpoint marker", () => TestDryRunFetchHttpEndpointMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset fetch http endpoint aliases normalize", () => TestDryRunFetchHttpEndpointMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset fetch http tcp exchange adds exchange marker", () => TestDryRunFetchHttpTcpExchangeMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset fetch http tcp exchange aliases normalize", () => TestDryRunFetchHttpTcpExchangeMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset http static response adds marker requirements", () => TestDryRunHttpStaticResponseMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset http static response aliases normalize", () => TestDryRunHttpStaticResponseMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset http static tcp response includes marker requirements", () => TestDryRunHttpStaticTcpResponseMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset http static tcp response accepts aliases", () => TestDryRunHttpStaticTcpResponseMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset virtio rx tcp dispatch adds marker requirements", () => TestDryRunVirtioRxTcpDispatchMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset virtio rx tcp dispatch aliases normalize", () => TestDryRunVirtioRxTcpDispatchMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset manifest env adds native manifest marker", () => TestDryRunManifestEnvMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset manifest env aliases normalize", () => TestDryRunManifestEnvMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset nativeaot heap adds heap bootstrap marker", () => TestDryRunNativeAotHeapMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset nativeaot heap aliases normalize", () => TestDryRunNativeAotHeapMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset pal export adds PAL catalog marker", () => TestDryRunPalExportMarkerPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset pal export aliases normalize", () => TestDryRunPalExportMarkerPresetAliases(tempRoot), ref failures);
            Run("qemu.smoke.preset rejects unknown preset", () => TestRejectsUnknownMarkerPreset(tempRoot), ref failures);
            Run("qemu runner accepts forbidden markers", () => TestDryRunForbiddenMarkers(tempRoot), ref failures);
            Run("qemu dry-run emits default workload profile", () => TestDryRunDefaultWorkloadProfile(tempRoot), ref failures);
            Run("qemu dry-run propagates multi workload profile", () => TestDryRunMultiWorkloadProfile(tempRoot), ref failures);
            Run("qemu rejects invalid workload profile", () => TestRejectsInvalidWorkloadProfile(tempRoot), ref failures);
            Run("qemu smoke script defaults require boot boundary markers", TestSmokeScriptDefaultMarkers, ref failures);
            Run("qemu smoke script forwards ACPI physical discovery EFI probe", TestSmokeScriptForwardsAcpiPhysicalDiscoveryProbe, ref failures);
            Run("qemu smoke script forwards M01 stage-one evidence controls", () => TestSmokeScriptForwardsM01StageOneEvidenceControls(tempRoot), ref failures);
            Run("qemu smoke script rejects stale M01 probe artifact selection", () => TestSmokeScriptRejectsStaleM01ProbeArtifactSelection(tempRoot), ref failures);
            Run("qemu smoke script native app preset includes app markers", TestSmokeScriptNativeAppPresetMarkers, ref failures);
            Run("qemu smoke script dry-run propagates workload profile", () => TestSmokeScriptDryRunWorkloadProfile(tempRoot), ref failures);
            Run("qemu smoke script dry-run propagates qcow2 disk format", () => TestSmokeScriptDryRunQcow2DiskFormat(tempRoot), ref failures);
            Run("qemu smoke script real mode reports missing qemu prerequisite", () => TestSmokeScriptReportsMissingQemu(tempRoot), ref failures);
            Run("qemu smoke script real mode reports missing ovmf prerequisite", () => TestSmokeScriptReportsMissingOvmf(tempRoot), ref failures);
            Run("qemu smoke script real mode reports missing image before runner", () => TestSmokeScriptReportsMissingImageBeforeRunner(tempRoot), ref failures);
            Run("qemu qcow2 smoke script documents conversion and qcow2 boot", TestQcow2SmokeScriptDocumentsConversionAndBoot, ref failures);
            Run("qemu dry-run emits native app marker requirements", () => TestSmokeScriptDryRunNativeAppMarkers(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes selected preset", () => TestSmokeScriptDryRunAcpiPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes app args preset", () => TestSmokeScriptDryRunAppArgsPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes app exit preset", () => TestSmokeScriptDryRunAppExitPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes app lifecycle preset", () => TestSmokeScriptDryRunAppLifecyclePreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes app selection preset", () => TestSmokeScriptDryRunAppSelectionPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes cctor preflight preset", () => TestSmokeScriptDryRunCctorPreflightPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes cctor work items preset", () => TestSmokeScriptDryRunCctorWorkItemsPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes fetch http preset", () => TestSmokeScriptDryRunFetchHttpPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes fetch http endpoint preset", () => TestSmokeScriptDryRunFetchHttpEndpointPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes fetch http tcp exchange preset", () => TestSmokeScriptDryRunFetchHttpTcpExchangePreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes http static response preset", () => TestSmokeScriptDryRunHttpStaticResponsePreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes http static tcp response preset", () => TestSmokeScriptDryRunHttpStaticTcpResponsePreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes virtio rx tcp dispatch preset", () => TestSmokeScriptDryRunVirtioRxTcpDispatchPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes manifest env preset", () => TestSmokeScriptDryRunManifestEnvPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes nativeaot heap preset", () => TestSmokeScriptDryRunNativeAotHeapPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset script dry-run includes pal export preset", () => TestSmokeScriptDryRunPalExportPreset(tempRoot), ref failures);
            Run("qemu.smoke.preset real mode passes selected markers", () => TestRealModeMarkerPresetSummary(tempRoot), ref failures);
            Run("qemu.smoke.preset acpi fails when MCFG marker is missing", () => TestRealModeAcpiPresetFailsMissingMcfgMarker(tempRoot), ref failures);
            Run("qemu.smoke.preset acpi fails when markers are out of order", () => TestRealModeAcpiPresetFailsOutOfOrderMarkers(tempRoot), ref failures);
            Run("qemu M01 ordered and unique dry-run evidence", () => TestM01OrderedAndUniqueDryRunEvidence(tempRoot), ref failures);
            Run("qemu M01 ordered markers reject reordering", () => TestM01OrderedMarkersRejectReordering(tempRoot), ref failures);
            Run("qemu M01 unique markers reject duplicate stage start", () => TestM01UniqueMarkersRejectDuplicateStageStart(tempRoot), ref failures);
            Run("qemu M01 unique markers reject duplicate stage pass", () => TestM01UniqueMarkersRejectDuplicateStagePass(tempRoot), ref failures);
            Run("qemu M01 unique markers reject duplicate semantic checkpoint", () => TestM01UniqueMarkersRejectDuplicateSemanticCheckpoint(tempRoot), ref failures);
            Run("qemu legacy required markers remain presence-only", () => TestLegacyRequiredMarkersRemainPresenceOnly(tempRoot), ref failures);
            Run("qemu M01 missing checkpoint markers are rejected", () => TestM01MissingCheckpointMarkersAreRejected(tempRoot), ref failures);
            Run("qemu M01 partial marker EOF is rejected", () => TestM01PartialMarkerEofIsRejected(tempRoot), ref failures);
            Run("qemu M01 chunked marker stream succeeds", () => TestM01ChunkedMarkerStreamSucceeds(tempRoot), ref failures);
            Run("qemu M01 allocation cleanup failure is rejected", () => TestM01AllocationCleanupFailureIsRejected(tempRoot), ref failures);
            Run("qemu M01 unexpected switch return is rejected", () => TestM01UnexpectedSwitchReturnIsRejected(tempRoot), ref failures);
            Run("qemu M01 timeout is deterministic", () => TestM01TimeoutIsDeterministic(tempRoot), ref failures);
            Run("qemu M01 reboot signature is rejected", () => TestM01RebootSignatureIsRejected(tempRoot), ref failures);
            Run("qemu M01 conflicting terminals are rejected", () => TestM01ConflictingTerminalsAreRejected(tempRoot), ref failures);
            Run("qemu dry-run emits virtio network arguments", () => TestDryRunVirtioNetworkArguments(tempRoot), ref failures);
            Run("qemu dry-run uses raw disk format by default", () => TestDryRunDefaultRawDiskFormat(tempRoot), ref failures);
            Run("qemu dry-run uses qcow2 disk format", () => TestDryRunQcow2DiskFormat(tempRoot), ref failures);
            Run("qemu rejects invalid disk format", () => TestRejectsInvalidDiskFormat(tempRoot), ref failures);
            Run("qemu dry-run does not write summary artifact", () => TestDryRunDoesNotWriteSummary(tempRoot), ref failures);
            Run("qemu dry-run emits HTTP probe contract", () => TestDryRunHttpProbeContract(tempRoot), ref failures);
            Run("qemu rejects HTTPS HTTP probe", () => TestRejectsHttpsHttpProbe(tempRoot), ref failures);
            Run("qemu rejects invalid HTTP probe status", () => TestRejectsInvalidHttpProbeStatus(tempRoot), ref failures);
            Run("qemu rejects invalid HTTP probe timeout", () => TestRejectsInvalidHttpProbeTimeout(tempRoot), ref failures);
            Run("qemu rejects incomplete HTTP probe", () => TestRejectsIncompleteHttpProbe(tempRoot), ref failures);
            Run("qemu real mode writes passing summary artifact", () => TestRealModeWritesPassingSummary(tempRoot), ref failures);
            Run("qemu real mode rejects duplicate success terminals", () => TestRealModeRejectsDuplicateSuccessTerminals(tempRoot), ref failures);
            Run("qemu real mode requires success terminal with custom marker", () => TestRealModeRequiresSuccessTerminalWithCustomMarker(tempRoot), ref failures);
            Run("qemu real mode rejects nonzero qemu exit", () => TestRealModeRejectsNonzeroQemuExit(tempRoot), ref failures);
            Run("qemu real mode rejects TEST_FAIL before success", () => TestRealModeRejectsTestFailBeforeSuccess(tempRoot), ref failures);
            Run("qemu drain classifies nonzero exit before kill", () => TestDrainClassifiesNonzeroExitBeforeKill(tempRoot), ref failures);
            Run("qemu boot timeout takes precedence over late markers", () => TestBootTimeoutPrecedesLateMarkers(tempRoot), ref failures);
            Run("qemu invalid marker configuration does not launch child", () => TestInvalidMarkerConfigurationDoesNotLaunchChild(tempRoot), ref failures);
            Run("qemu rejects output and input path aliases", () => TestRejectsArtifactPathAliases(tempRoot), ref failures);
            Run("qemu.runner app exit summary emitted", () => TestRealModeWritesAppExitSummary(tempRoot), ref failures);
            Run("qemu runner fails when forbidden marker appears", () => TestRealModeFailsForbiddenMarker(tempRoot), ref failures);
            Run("qemu real mode writes missing marker summary artifact", () => TestRealModeWritesMissingMarkerSummary(tempRoot), ref failures);
            Run("qemu summary includes bounded serial tail", () => TestSummaryIncludesBoundedSerialTail(tempRoot), ref failures);
            Run("qemu runner keeps required marker failure distinct", () => TestRequiredMarkerFailureDistinctFromForbiddenMarker(tempRoot), ref failures);
            Run("qemu summary write failure is visible", () => TestSummaryWriteFailureIsVisible(tempRoot), ref failures);
            Run("qemu real mode rejects missing image before launch", () => TestMissingImage(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Test cleanup failure should not hide validation results.
            }
        }

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL qemu.runner.tests failures=" + failures);
            Environment.Exit(1);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestDryRunCommandConstruction(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string serialLog = Path.Combine(tempRoot, "serial.log");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--qemu",
            "qemu-test",
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--memory",
            "512M");

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_START qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && output.Contains("qemu-test -machine q35,accel=tcg -m 512M -no-reboot -display none", StringComparison.Ordinal)
            && output.Contains("ich9-ahci,id=ahci", StringComparison.Ordinal)
            && output.Contains("if=none,id=esp,format=raw,file=" + imagePath, StringComparison.Ordinal)
            && output.Contains("ide-hd,drive=esp,bus=ahci.0", StringComparison.Ordinal);
    }

    private static bool TestDryRunSerialLogArguments(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string serialDirectory = Path.Combine(tempRoot, "serial logs");
        Directory.CreateDirectory(serialDirectory);
        string serialLog = Path.Combine(serialDirectory, "qemu smoke.log");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialLog);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("-serial", StringComparison.Ordinal)
            && output.Contains("\"file:" + serialLog + "\"", StringComparison.Ordinal);
    }

    private static bool TestDryRunOvmfArguments(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string ovmfPath = Path.Combine(tempRoot, "firmware", "OVMF CODE.fd");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--ovmf-code",
            ovmfPath);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("-drive", StringComparison.Ordinal)
            && output.Contains("\"if=pflash,format=raw,readonly=on,file=" + ovmfPath + "\"", StringComparison.Ordinal);
    }

    private static bool TestPreflightValidatesRawImageArtifact(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "preflight.raw");
        File.WriteAllBytes(imagePath, new byte[] { 0x45, 0x46, 0x49, 0x20 });
        string ovmfPath = CreateEmptyFile(tempRoot, "preflight-OVMF_CODE.fd");
        string serialLog = Path.Combine(tempRoot, "preflight-serial.log");

        ProcessResult result = RunQemuRunner(
            "preflight",
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--ovmf-code",
            ovmfPath,
            "--forbid-marker",
            "OS_SMOKE_FAIL");

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_START qemu.runner.preflight", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_IMAGE path=" + imagePath + " exists=true bytes=4", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_DISK_FORMAT expected=raw detected=raw-compatible", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_SERIAL_DIRECTORY path=" + tempRoot + " exists=true runner_creates=true", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_OVMF configured=true exists=true path=" + ovmfPath, StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_COMMAND_ARGUMENTS count=", StringComparison.Ordinal)
            && output.Contains("if=none,id=esp,format=raw,file=" + imagePath, StringComparison.Ordinal)
            && output.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.preflight", StringComparison.Ordinal)
            && !File.Exists(serialLog);
    }

    private static bool TestPreflightRejectsMissingImageArtifact(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "missing-preflight.raw");

        ProcessResult result = RunQemuRunner(
            "--preflight",
            "--image",
            imagePath);

        string output = result.Output;
        return result.ExitCode == 2
            && output.Contains("TEST_START qemu.runner.preflight", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_IMAGE path=" + imagePath + " exists=false bytes=0", StringComparison.Ordinal)
            && output.Contains("TEST_FAIL qemu.runner.preflight image_not_found", StringComparison.Ordinal)
            && !output.Contains("TEST_PASS qemu.runner.preflight", StringComparison.Ordinal);
    }

    private static bool TestPreflightRejectsDiskFormatMismatch(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "qcow2-magic.raw");
        File.WriteAllBytes(imagePath, new byte[] { (byte)'Q', (byte)'F', (byte)'I', 0xFB, 0x00, 0x00 });

        ProcessResult result = RunQemuRunner(
            "preflight",
            "--image",
            imagePath,
            "--disk-format",
            "raw");

        string output = result.Output;
        return result.ExitCode == 12
            && output.Contains("QEMU_PREFLIGHT_IMAGE path=" + imagePath + " exists=true bytes=6", StringComparison.Ordinal)
            && output.Contains("QEMU_PREFLIGHT_DISK_FORMAT expected=raw detected=qcow2", StringComparison.Ordinal)
            && output.Contains("TEST_FAIL qemu.runner.preflight disk_format_mismatch expected=raw detected=qcow2", StringComparison.Ordinal);
    }

    private static bool TestDryRunMarkerRequirements(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string serialLog = Path.Combine(tempRoot, "serial.log");

        ProcessResult defaultResult = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialLog);

        ProcessResult customResult = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--require-marker",
            "UEFI_GONE_SIM_PASS",
            "--require-marker",
            "OS_BOOT_OK");

        string defaultOutput = defaultResult.Output;
        string customOutput = customResult.Output;
        return defaultResult.ExitCode == 0
            && customResult.ExitCode == 0
            && defaultOutput.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && customOutput.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && customOutput.Contains(
                "REQUIRED_MARKERS " +
                DefaultBootRequiredMarkers +
                ";UEFI_GONE_SIM_PASS",
                StringComparison.Ordinal);
    }

    private static bool TestDryRunMarkerPresetDefault(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult defaultResult = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath);

        ProcessResult presetResult = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "baseline");

        return defaultResult.ExitCode == 0
            && presetResult.ExitCode == 0
            && defaultResult.Output.Contains("MARKER_PRESETS none", StringComparison.Ordinal)
            && defaultResult.Output.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && presetResult.Output.Contains("MARKER_PRESETS baseline", StringComparison.Ordinal)
            && presetResult.Output.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal);
    }

    private static bool TestDryRunAcpiMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "acpi");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";ACPI_RSDP_SNAPSHOT_READY;ACPI_RSDP_VALIDATED;ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS acpi", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunUefiGoneMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "uefi-gone");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";UEFI_GONE_SIM_START;UEFI_GONE_SIM_PLATFORM_DENIED;POST_FIRMWARE_DIAGNOSTICS_READY;UEFI_GONE_SIM_PASS";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS uefi-gone", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppArgsMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app-args");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_ARGS_VECTOR_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-args", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppArgsMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app_args",
            "--marker-preset",
            "app-arguments",
            "--marker-preset",
            "native-app-args");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_ARGS_VECTOR_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-args", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppExitMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app-exit");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_EXIT_OBSERVER_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-exit", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppLifecycleMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app-lifecycle");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_LIFECYCLE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-lifecycle", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppLifecycleMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app_lifecycle",
            "--marker-preset",
            "native-app-lifecycle",
            "--marker-preset",
            "applifecycle");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_LIFECYCLE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-lifecycle", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppSelectionMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app-selection");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_SELECTION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-selection", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunAppSelectionMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "app_selection",
            "--marker-preset",
            "native-app-selection",
            "--marker-preset",
            "appselection");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";APP_SELECTION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS app-selection", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunCctorPreflightMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "cctor-preflight");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS cctor-preflight", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunCctorWorkItemsMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "cctor-work-items");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVEAOT_CCTOR_WORK_ITEMS_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS cctor-work-items", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunCctorWorkItemsMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "cctor_work_items",
            "--marker-preset",
            "cctor-workitem",
            "--marker-preset",
            "cctor-work-item");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVEAOT_CCTOR_WORK_ITEMS_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS cctor-work-items", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunDiagnosticsMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "diagnostics");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY;DIAGNOSTIC_STUB_INVOCATION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS diagnostics", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY;DIAGNOSTIC_STUB_INVOCATION_READY", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunFetchHttpMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "fetch-http");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FETCH_APP_HTTP_COMPLETION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS fetch-http", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunFetchHttpEndpointMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "fetch-http-endpoint");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FETCHAPP_HTTP_ENDPOINT_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS fetch-http-endpoint", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunFetchHttpEndpointMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "fetch_endpoint",
            "--marker-preset",
            "fetchhttp-endpoint",
            "--marker-preset",
            "fetchapp-http-endpoint");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FETCHAPP_HTTP_ENDPOINT_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS fetch-http-endpoint", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunFetchHttpTcpExchangeMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "fetch-http-tcp-exchange");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FETCH_APP_HTTP_TCP_EXCHANGE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS fetch-http-tcp-exchange", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunFetchHttpTcpExchangeMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "fetch_http_tcp_exchange",
            "--marker-preset",
            "fetch-http-exchange",
            "--marker-preset",
            "fetchapp-http-tcp-exchange");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";FETCH_APP_HTTP_TCP_EXCHANGE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS fetch-http-tcp-exchange", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunHttpStaticResponseMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "http-static-response");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";HTTP_STATIC_RESPONSE_READY;HTTP_STATIC_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS http-static-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunHttpStaticResponseMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "http_static_response",
            "--marker-preset",
            "tiny-http-static",
            "--marker-preset",
            "tinyhttp-static");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";HTTP_STATIC_RESPONSE_READY;HTTP_STATIC_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS http-static-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunHttpStaticTcpResponseMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "http-static-tcp-response");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";HTTP_STATIC_TCP_RESPONSE_READY;HTTP_STATIC_TCP_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS http-static-tcp-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunHttpStaticTcpResponseMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "http_static_tcp_response",
            "--marker-preset",
            "tiny-http-tcp-static",
            "--marker-preset",
            "tinyhttp-tcp-static");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";HTTP_STATIC_TCP_RESPONSE_READY;HTTP_STATIC_TCP_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS http-static-tcp-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunVirtioRxTcpDispatchMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "virtio-rx-tcp-dispatch");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";VIRTIO_NET_RX_TCP_DISPATCH_READY;VIRTIO_NET_RX_TCP_DISPATCH";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS virtio-rx-tcp-dispatch", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunVirtioRxTcpDispatchMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "virtio_rx_tcp_dispatch",
            "--marker-preset",
            "virtio-net-rx-tcp-dispatch",
            "--marker-preset",
            "virtionet-rx-tcp-dispatch");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";VIRTIO_NET_RX_TCP_DISPATCH_READY;VIRTIO_NET_RX_TCP_DISPATCH";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS virtio-rx-tcp-dispatch", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunManifestEnvMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "manifest-env");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVE_MANIFEST_ENV_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS manifest-env", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunManifestEnvMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "native-manifest-env",
            "--marker-preset",
            "manifest_environment",
            "--marker-preset",
            "manifestenv");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVE_MANIFEST_ENV_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS manifest-env", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunNativeAotHeapMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "nativeaot-heap");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVEAOT_HEAP_BOOTSTRAP_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS nativeaot-heap", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunNativeAotHeapMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "nativeaot_heap",
            "--marker-preset",
            "heap-bootstrap",
            "--marker-preset",
            "nativeaot-heap-bootstrap");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";NATIVEAOT_HEAP_BOOTSTRAP_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS nativeaot-heap", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunPalExportMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "pal-export");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";PAL_EXPORT_CATALOG_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS pal-export", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestDryRunPalExportMarkerPresetAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "pal_export",
            "--marker-preset",
            "pal-catalog",
            "--marker-preset",
            "pal-export-catalog");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";PAL_EXPORT_CATALOG_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("MARKER_PRESETS pal-export", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("ORDERED_MARKERS none", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static bool TestRejectsUnknownMarkerPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--marker-preset",
            "full-acpi");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("Unknown marker preset 'full-acpi'", StringComparison.Ordinal)
            && result.Output.Contains("baseline,acpi,app-args,app-exit,app-lifecycle,app-selection,cctor-preflight,cctor-work-items,diagnostics,fetch-http,fetch-http-endpoint,fetch-http-tcp-exchange,http-static-response,http-static-tcp-response,manifest-env,nativeaot-heap,pal-export,uefi-gone,virtio-rx-tcp-dispatch", StringComparison.Ordinal);
    }

    private static bool TestDryRunForbiddenMarkers(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string serialLog = Path.Combine(tempRoot, "serial.log");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--forbid-marker",
            "UEFI_EBS_FRESH_MAP_PROBE_START",
            "--forbid-marker",
            "UEFI_EBS_CALL_SKIPPED");

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL;UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_CALL_SKIPPED", StringComparison.Ordinal);
    }

    private static bool TestDryRunDefaultWorkloadProfile(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && result.Output.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal);
    }

    private static bool TestDryRunMultiWorkloadProfile(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--workload-profile",
            "MultiMinimalApi");

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && result.Output.Contains("WORKLOAD_PROFILE MultiMinimalApi", StringComparison.Ordinal);
    }

    private static bool TestRejectsInvalidWorkloadProfile(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--workload-profile",
            "BadProfile");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("Invalid workload profile 'BadProfile'", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptDefaultMarkers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));

        return script.Contains("\"OS_BOOT_OK\"", StringComparison.Ordinal)
            && script.Contains("\"TEST_PASS boot.sequence\"", StringComparison.Ordinal)
            && script.Contains("\"OS_SMOKE_PASS\"", StringComparison.Ordinal)
            && script.Contains("\"UEFI_MEMORY_MAP_OWNED\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_HANDOFF_READY\"", StringComparison.Ordinal)
            && script.Contains("\"UEFI_MEMORY_MAP_NORMALIZED\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_MEMORY_MAP_ACCEPTED\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_MEMORY_REGIONS_READY\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_MEMORY_REGION_COUNT\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_PAGE_ALLOCATOR_READY\"", StringComparison.Ordinal)
            && script.Contains("\"BOOT_ACPI_RSDP_HANDOFF_READY\"", StringComparison.Ordinal)
            && script.Contains("\"UEFI_ACPI_RSDP_READY\"", StringComparison.Ordinal)
            && script.Contains("$ForbidMarker", StringComparison.Ordinal)
            && script.Contains("\"OS_SMOKE_FAIL\"", StringComparison.Ordinal)
            && script.Contains("\"--forbid-marker\"", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptForwardsAcpiPhysicalDiscoveryProbe()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));

        return script.Contains("[switch] $EnableAcpiPhysicalDiscoveryProbe", StringComparison.Ordinal)
            && script.Contains("if ($EnableAcpiPhysicalDiscoveryProbe)", StringComparison.Ordinal)
            && script.Contains("$buildArguments += \"-EnableAcpiPhysicalDiscoveryProbe\"", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptForwardsM01StageOneEvidenceControls(
        string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
        string imagePath = Path.Combine(tempRoot, "m01-script-dry-run.raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-script-dry-run-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-script-dry-run-summary.log");
        const string markers =
            "TEST_START m01.stage1,M01_STACK_SWITCH_STUB_READY,OS_SMOKE_PASS";

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-RequireMarker",
            markers,
            "-RequireOrderedMarker",
            markers,
            "-RequireUniqueMarker",
            markers);

        return result.ExitCode == 0
            && script.Contains(
                "[string[]] $RequireOrderedMarker",
                StringComparison.Ordinal)
            && script.Contains(
                "[string[]] $RequireUniqueMarker",
                StringComparison.Ordinal)
            && script.Contains(
                "[switch] $EnableM01LoadedImageStackProbe",
                StringComparison.Ordinal)
            && script.Contains(
                "$buildArguments += \"-EnableM01LoadedImageStackProbe\"",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += @(\"--require-ordered-marker\", $marker)",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += @(\"--require-unique-marker\", $marker)",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_M01_LOADED_IMAGE_STACK_PROBE enabled=true buildEfi=true skipImageBuild=false dryRun=true",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY;OS_SMOKE_PASS",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY;OS_SMOKE_PASS",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "ORDERED_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY;OS_SMOKE_PASS",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY;OS_SMOKE_PASS",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsStaleM01ProbeArtifactSelection(
        string tempRoot)
    {
        string staleEfi = Path.Combine(tempRoot, "stale-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4D, 0x5A, 0x00, 0x00]);
        byte[] before = File.ReadAllBytes(staleEfi);

        ProcessResult noBuild = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult skippedImage = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-SkipImageBuild",
            "-EfiArtifact",
            staleEfi);

        return noBuild.ExitCode == 1
            && skippedImage.ExitCode == 1
            && noBuild.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_loaded_image_stack_probe_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_loaded_image_stack_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !noBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestSmokeScriptNativeAppPresetMarkers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));

        return script.Contains("$RequireNativeAppMarkers", StringComparison.Ordinal)
            && script.Contains("\"APP_PACKAGE_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_LAUNCH_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_PAYLOAD_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_EXEC_MAPPING_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_EXEC_MEMORY_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_INVOCATION_READY\"", StringComparison.Ordinal)
            && script.Contains("\"APP_ENTRY_READY\"", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptDryRunWorkloadProfile(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "multi-profile.raw");
        string serialLog = Path.Combine(tempRoot, "multi-profile-serial.log");
        string summaryLog = Path.Combine(tempRoot, "multi-profile-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-WorkloadProfile",
            "MultiMinimalApi",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("WORKLOAD_PROFILE MultiMinimalApi", StringComparison.Ordinal)
            && output.Contains("TEST_PASS workload.profile.tooling propagates multi", StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunQcow2DiskFormat(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "script-qcow2.qcow2");
        string serialLog = Path.Combine(tempRoot, "script-qcow2-serial.log");
        string summaryLog = Path.Combine(tempRoot, "script-qcow2-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-DiskFormat",
            "qcow2",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("if=none,id=esp,format=qcow2,file=" + imagePath, StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptReportsMissingQemu(string tempRoot)
    {
        string imagePath = CreateRawImageFile(tempRoot, "script-missing-qemu.raw");
        string serialLog = Path.Combine(tempRoot, "script-missing-qemu-serial.log");
        string summaryLog = Path.Combine(tempRoot, "script-missing-qemu-summary.log");
        string missingQemu = Path.Combine(tempRoot, "missing-qemu.exe");
        string ovmfPath = CreateEmptyFile(tempRoot, "script-missing-qemu-OVMF_CODE.fd");

        ProcessResult result = RunQemuSmokeScript(
            "-SkipImageBuild",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-Qemu",
            missingQemu,
            "-OvmfCode",
            ovmfPath);

        return result.ExitCode == 3
            && result.Output.Contains("TEST_START qemu.smoke.preflight", StringComparison.Ordinal)
            && result.Output.Contains("QEMU_SCRIPT_PREFLIGHT_QEMU requested=" + missingQemu, StringComparison.Ordinal)
            && result.Output.Contains("result=not_found", StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL qemu.smoke qemu_not_found", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_START qemu.runner", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptReportsMissingOvmf(string tempRoot)
    {
        string imagePath = CreateRawImageFile(tempRoot, "script-missing-ovmf.raw");
        string serialLog = Path.Combine(tempRoot, "script-missing-ovmf-serial.log");
        string summaryLog = Path.Combine(tempRoot, "script-missing-ovmf-summary.log");
        string qemuPath = CreateFakeQemuScript(tempRoot, "OS_SMOKE_PASS");
        string missingOvmf = Path.Combine(tempRoot, "missing-OVMF_CODE.fd");

        ProcessResult result = RunQemuSmokeScript(
            "-SkipImageBuild",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-Qemu",
            qemuPath,
            "-OvmfCode",
            missingOvmf);

        return result.ExitCode == 6
            && result.Output.Contains("TEST_START qemu.smoke.preflight", StringComparison.Ordinal)
            && result.Output.Contains("QEMU_SCRIPT_PREFLIGHT_QEMU requested=" + qemuPath, StringComparison.Ordinal)
            && result.Output.Contains("QEMU_SCRIPT_PREFLIGHT_OVMF requested=" + missingOvmf, StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL qemu.smoke ovmf_not_found", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_START qemu.runner", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptReportsMissingImageBeforeRunner(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "script-missing-image.raw");
        string serialLog = Path.Combine(tempRoot, "script-missing-image-serial.log");
        string summaryLog = Path.Combine(tempRoot, "script-missing-image-summary.log");
        string qemuPath = CreateFakeQemuScript(tempRoot, "OS_SMOKE_PASS");
        string ovmfPath = CreateEmptyFile(tempRoot, "script-missing-image-OVMF_CODE.fd");

        ProcessResult result = RunQemuSmokeScript(
            "-SkipImageBuild",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-Qemu",
            qemuPath,
            "-OvmfCode",
            ovmfPath);

        return result.ExitCode == 2
            && result.Output.Contains("TEST_PASS qemu.smoke.preflight", StringComparison.Ordinal)
            && result.Output.Contains("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + imagePath + " exists=false bytes=0", StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL qemu.smoke image_not_found path=" + imagePath, StringComparison.Ordinal)
            && !result.Output.Contains("TEST_START qemu.runner", StringComparison.Ordinal);
    }

    private static bool TestQcow2SmokeScriptDocumentsConversionAndBoot()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "run-qemu-qcow2-smoke.ps1"));

        return script.Contains("convert-qcow2", StringComparison.Ordinal)
            && script.Contains("qemu.qcow2.smoke", StringComparison.Ordinal)
            && script.Contains("-DiskFormat", StringComparison.Ordinal)
            && script.Contains("qcow2", StringComparison.Ordinal)
            && script.Contains("BOOT_MEMORY_REGIONS_READY", StringComparison.Ordinal)
            && script.Contains("BOOT_MEMORY_REGION_COUNT", StringComparison.Ordinal)
            && script.Contains("TEST_FAIL qemu.qcow2.smoke", StringComparison.Ordinal)
            && script.Contains("TEST_PASS qemu.qcow2.smoke", StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptDryRunNativeAppMarkers(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "native-app.raw");
        string serialLog = Path.Combine(tempRoot, "native-app-serial.log");
        string summaryLog = Path.Combine(tempRoot, "native-app-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-RequireNativeAppMarkers");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";APP_PACKAGE_READY;APP_LAUNCH_READY;APP_PAYLOAD_READY;APP_EXEC_MAPPING_READY;APP_EXEC_MEMORY_READY;APP_INVOCATION_READY;APP_ENTRY_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunAcpiPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "acpi-preset.raw");
        string serialLog = Path.Combine(tempRoot, "acpi-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "acpi-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "acpi");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";ACPI_RSDP_SNAPSHOT_READY;ACPI_RSDP_VALIDATED;ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS acpi", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS acpi", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunAppExitPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "app-exit-preset.raw");
        string serialLog = Path.Combine(tempRoot, "app-exit-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "app-exit-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "app-exit");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";APP_EXIT_OBSERVER_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS app-exit", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS app-exit", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunAppLifecyclePreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "app-lifecycle-preset.raw");
        string serialLog = Path.Combine(tempRoot, "app-lifecycle-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "app-lifecycle-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "app-lifecycle");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";APP_LIFECYCLE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS app-lifecycle", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS app-lifecycle", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunAppSelectionPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "app-selection-preset.raw");
        string serialLog = Path.Combine(tempRoot, "app-selection-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "app-selection-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "app-selection");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";APP_SELECTION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS app-selection", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS app-selection", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunAppArgsPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "app-args-preset.raw");
        string serialLog = Path.Combine(tempRoot, "app-args-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "app-args-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "app-args");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";APP_ARGS_VECTOR_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS app-args", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS app-args", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunCctorPreflightPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "cctor-preflight-preset.raw");
        string serialLog = Path.Combine(tempRoot, "cctor-preflight-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "cctor-preflight-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "cctor-preflight");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS cctor-preflight", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS cctor-preflight", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunCctorWorkItemsPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "cctor-work-items-preset.raw");
        string serialLog = Path.Combine(tempRoot, "cctor-work-items-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "cctor-work-items-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "cctor-work-items");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";NATIVEAOT_CCTOR_WORK_ITEMS_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS cctor-work-items", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS cctor-work-items", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunFetchHttpPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "fetch-http-preset.raw");
        string serialLog = Path.Combine(tempRoot, "fetch-http-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "fetch-http-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "fetch-http");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";FETCH_APP_HTTP_COMPLETION_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS fetch-http", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS fetch-http", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunFetchHttpEndpointPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "fetch-http-endpoint-preset.raw");
        string serialLog = Path.Combine(tempRoot, "fetch-http-endpoint-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "fetch-http-endpoint-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "fetch-http-endpoint");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";FETCHAPP_HTTP_ENDPOINT_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS fetch-http-endpoint", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS fetch-http-endpoint", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunFetchHttpTcpExchangePreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "fetch-http-tcp-exchange-preset.raw");
        string serialLog = Path.Combine(tempRoot, "fetch-http-tcp-exchange-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "fetch-http-tcp-exchange-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "fetch-http-tcp-exchange");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";FETCH_APP_HTTP_TCP_EXCHANGE_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS fetch-http-tcp-exchange", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS fetch-http-tcp-exchange", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunHttpStaticResponsePreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "http-static-response-preset.raw");
        string serialLog = Path.Combine(tempRoot, "http-static-response-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "http-static-response-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "http_static_response");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";HTTP_STATIC_RESPONSE_READY;HTTP_STATIC_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS http-static-response", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS http-static-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunHttpStaticTcpResponsePreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "http-static-tcp-response-preset.raw");
        string serialLog = Path.Combine(tempRoot, "http-static-tcp-response-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "http-static-tcp-response-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "http_static_tcp_response");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";HTTP_STATIC_TCP_RESPONSE_READY;HTTP_STATIC_TCP_RESPONSE";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS http-static-tcp-response", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS http-static-tcp-response", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunVirtioRxTcpDispatchPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "virtio-rx-tcp-dispatch-preset.raw");
        string serialLog = Path.Combine(tempRoot, "virtio-rx-tcp-dispatch-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "virtio-rx-tcp-dispatch-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "virtio_rx_tcp_dispatch");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";VIRTIO_NET_RX_TCP_DISPATCH_READY;VIRTIO_NET_RX_TCP_DISPATCH";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS virtio-rx-tcp-dispatch", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS virtio-rx-tcp-dispatch", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunManifestEnvPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "manifest-env-preset.raw");
        string serialLog = Path.Combine(tempRoot, "manifest-env-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "manifest-env-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "manifest-env");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";NATIVE_MANIFEST_ENV_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS manifest-env", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS manifest-env", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunNativeAotHeapPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "nativeaot-heap-preset.raw");
        string serialLog = Path.Combine(tempRoot, "nativeaot-heap-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "nativeaot-heap-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "nativeaot-heap");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";NATIVEAOT_HEAP_BOOTSTRAP_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS nativeaot-heap", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS nativeaot-heap", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptDryRunPalExportPreset(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "pal-export-preset.raw");
        string serialLog = Path.Combine(tempRoot, "pal-export-preset-serial.log");
        string summaryLog = Path.Combine(tempRoot, "pal-export-preset-summary.log");

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog,
            "-MarkerPreset",
            "pal-export");

        string expectedMarkers =
            "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers + ";PAL_EXPORT_CATALOG_READY";

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("QEMU_MARKER_PRESETS pal-export", StringComparison.Ordinal)
            && output.Contains("MARKER_PRESETS pal-export", StringComparison.Ordinal)
            && output.Contains(expectedMarkers, StringComparison.Ordinal)
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestRealModeMarkerPresetSummary(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "preset-real.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "preset-real-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "TEST_START fake.qemu",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "ACPI_RSDP_SNAPSHOT_READY",
            "ACPI_RSDP_VALIDATED",
            "ACPI_ROOT_SDT_SNAPSHOT_READY",
            "ACPI_ROOT_SDT_VALIDATED",
            "ACPI_MCFG_DISCOVERY_READY",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "preset-real-serial.log");
        string summaryLog = Path.Combine(tempRoot, "preset-real-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--marker-preset",
            "acpi",
            "--timeout",
            "3");

        string expectedMarkers =
            "REQUIRED_MARKERS " + DefaultBootRequiredMarkers + ";ACPI_RSDP_SNAPSHOT_READY;ACPI_RSDP_VALIDATED;ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY";

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 0
            && result.Output.Contains("MARKER_PRESETS acpi", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS pass", StringComparison.Ordinal)
            && summary.Contains("MARKER_PRESETS acpi", StringComparison.Ordinal)
            && summary.Contains(expectedMarkers, StringComparison.Ordinal)
            && summary.Contains("OBSERVED_MARKER ACPI_MCFG_DISCOVERY_READY", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS none", StringComparison.Ordinal);
    }

    private static bool TestRealModeAcpiPresetFailsMissingMcfgMarker(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "preset-missing-mcfg.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "preset-missing-mcfg-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "TEST_START fake.qemu",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "ACPI_RSDP_SNAPSHOT_READY",
            "ACPI_RSDP_VALIDATED",
            "ACPI_ROOT_SDT_SNAPSHOT_READY",
            "ACPI_ROOT_SDT_VALIDATED",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "preset-missing-mcfg-serial.log");
        string summaryLog = Path.Combine(tempRoot, "preset-missing-mcfg-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--marker-preset",
            "acpi",
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 5
            && result.Output.Contains("TEST_FAIL qemu.smoke missing_marker", StringComparison.Ordinal)
            && result.Output.Contains("missing=ACPI_MCFG_DISCOVERY_READY", StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS ACPI_MCFG_DISCOVERY_READY", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON missing_marker", StringComparison.Ordinal);
    }

    private static bool TestRealModeAcpiPresetFailsOutOfOrderMarkers(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "preset-out-of-order.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "preset-out-of-order-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "TEST_START fake.qemu",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "ACPI_RSDP_SNAPSHOT_READY",
            "ACPI_ROOT_SDT_SNAPSHOT_READY",
            "ACPI_RSDP_VALIDATED",
            "ACPI_ROOT_SDT_VALIDATED",
            "ACPI_MCFG_DISCOVERY_READY",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "preset-out-of-order-serial.log");
        string summaryLog = Path.Combine(tempRoot, "preset-out-of-order-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--marker-preset",
            "acpi",
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 10
            && result.Output.Contains("TEST_FAIL qemu.smoke out_of_order_marker", StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("OUT_OF_ORDER_MARKERS ACPI_ROOT_SDT_SNAPSHOT_READY;ACPI_ROOT_SDT_VALIDATED;ACPI_MCFG_DISCOVERY_READY", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON out_of_order_marker", StringComparison.Ordinal);
    }

    private static bool TestM01OrderedAndUniqueDryRunEvidence(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "m01-marker-evidence.raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-marker-evidence-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-marker-evidence-summary.log");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--require-marker",
            "TEST_START m01.stage1",
            "--require-marker",
            "M01_STACK_SWITCH_STUB_READY",
            "--require-ordered-marker",
            "TEST_START m01.stage1",
            "--require-ordered-marker",
            "M01_STACK_SWITCH_STUB_READY",
            "--require-unique-marker",
            "TEST_START m01.stage1",
            "--require-unique-marker",
            "M01_STACK_SWITCH_STUB_READY");

        return result.ExitCode == 0
            && result.Output.Contains(
                "REQUIRED_MARKERS "
                    + DefaultBootRequiredMarkers
                    + ";TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "ORDERED_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS TEST_START m01.stage1;M01_STACK_SWITCH_STUB_READY",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestM01OrderedMarkersRejectReordering(string tempRoot)
    {
        List<string> serialLines = CreateM01StageOneSerialLines();
        int imageIndex = serialLines.IndexOf("M01_LOADED_IMAGE_CAPTURED");
        int stubIndex = serialLines.IndexOf("M01_STACK_SWITCH_STUB_READY");
        (serialLines[imageIndex], serialLines[stubIndex]) =
            (serialLines[stubIndex], serialLines[imageIndex]);
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "reordered",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 10
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke out_of_order_marker",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "M01_LOADED_IMAGE_CAPTURED",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "OUT_OF_ORDER_MARKERS",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON out_of_order_marker",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01UniqueMarkersRejectDuplicateStageStart(
        string tempRoot)
    {
        return TestM01DuplicateUniqueMarker(
            tempRoot,
            "duplicate-start",
            "TEST_START m01.stage1");
    }

    private static bool TestM01UniqueMarkersRejectDuplicateStagePass(
        string tempRoot)
    {
        return TestM01DuplicateUniqueMarker(
            tempRoot,
            "duplicate-pass",
            "TEST_PASS m01.stage1");
    }

    private static bool TestM01UniqueMarkersRejectDuplicateSemanticCheckpoint(
        string tempRoot)
    {
        return TestM01DuplicateUniqueMarker(
            tempRoot,
            "duplicate-checkpoint",
            "M01_STAGE1_MAP_RECAPTURED");
    }

    private static bool TestM01DuplicateUniqueMarker(
        string tempRoot,
        string scenario,
        string duplicatedMarker)
    {
        List<string> serialLines = CreateM01StageOneSerialLines();
        int markerIndex = serialLines.IndexOf(duplicatedMarker);
        serialLines.Insert(markerIndex + 1, duplicatedMarker);
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            scenario,
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 17
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke duplicate_unique_marker",
                StringComparison.Ordinal)
            && result.Output.Contains(
                duplicatedMarker,
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "DUPLICATE_UNIQUE_MARKERS",
                StringComparison.Ordinal)
            && summary.Contains(
                duplicatedMarker,
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON duplicate_unique_marker",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestLegacyRequiredMarkersRemainPresenceOnly(
        string tempRoot)
    {
        const string legacyMarker = "M01_LEGACY_PRESENCE_ONLY";
        string imagePath = CreateEmptyFile(
            tempRoot,
            "m01-legacy-presence-only.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "m01-legacy-presence-only-OVMF_CODE.fd");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-legacy-presence-only-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-legacy-presence-only-summary.log");
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            [
                legacyMarker,
                legacyMarker,
                "OS_BOOT_OK",
                "TEST_PASS boot.sequence",
                "OS_SMOKE_PASS"
            ]);

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "5",
            "--require-marker",
            legacyMarker);

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 0
            && result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS pass",
                StringComparison.Ordinal)
            && summary.Contains(
                "UNIQUE_MARKERS none",
                StringComparison.Ordinal)
            && summary.Contains(
                "DUPLICATE_UNIQUE_MARKERS none",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON none",
                StringComparison.Ordinal);
    }

    private static bool TestM01MissingCheckpointMarkersAreRejected(
        string tempRoot)
    {
        string[] missingCases =
        [
            "TEST_START m01.stage1",
            "M01_STACK_SWITCH_STUB_READY",
            "M01_STACK_ARENA_ALLOCATED",
            "M01_STACK_GUARD_RESERVED",
            "M01_STAGE1_MAP_RECAPTURED",
            "M01_STAGE1_RESERVATIONS_VALIDATED",
            "TEST_PASS m01.stage1"
        ];

        foreach (string missingMarker in missingCases)
        {
            List<string> serialLines = CreateM01StageOneSerialLines();
            serialLines.Remove(missingMarker);
            string qemuPath = CreateFakeQemuScript(
                tempRoot,
                serialLines.ToArray());

            ProcessResult result = RunM01StageOneQemu(
                tempRoot,
                "missing-" + SanitizeFileName(missingMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: false,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 5
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke missing_marker",
                    StringComparison.Ordinal)
                || !result.Output.Contains(
                    missingMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "MISSING_MARKERS",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    missingMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FAIL_REASON missing_marker",
                    StringComparison.Ordinal)
                || result.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestM01PartialMarkerEofIsRejected(string tempRoot)
    {
        string qemuPath = CreatePartialMarkerFakeQemuScript(
            tempRoot,
            [
                "OS_BOOT_OK",
                "TEST_PASS boot.sequence",
                "OS_SMOKE_PASS"
            ],
            "TEST_START m01.sta");

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "partial-marker",
            qemuPath,
            includeOrdered: false,
            includeUnique: false,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 13
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke serial_marker_stream status=PartialMarkerAtEndOfStream",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON serial_marker_stream_partialmarkeratendofstream",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01ChunkedMarkerStreamSucceeds(string tempRoot)
    {
        string qemuPath = CreateChunkedPersistentFakeQemuScript(
            tempRoot,
            CreateM01StageOneSerialLines(),
            "M01_STACK_SWITCH_STUB_READY");

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "chunked-marker",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 8,
            out string summary);

        return result.ExitCode == 0
            && result.Output.Contains(
                "SERIAL_MARKER_STREAM status=Passed",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS pass",
                StringComparison.Ordinal)
            && summary.Contains(
                "MISSING_MARKERS none",
                StringComparison.Ordinal)
            && summary.Contains(
                "OUT_OF_ORDER_MARKERS none",
                StringComparison.Ordinal)
            && summary.Contains(
                "DUPLICATE_UNIQUE_MARKERS none",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON none",
                StringComparison.Ordinal);
    }

    private static bool TestM01AllocationCleanupFailureIsRejected(
        string tempRoot)
    {
        const string testFailure =
            "TEST_FAIL m01.stage1 status=9 adapter=7 checkpoint=1 free_data=0 free_code=9223372036854775807";
        const string stageFailure =
            "M01_STAGE1_FAIL status=9 adapter=7 checkpoint=1 free_data=0 free_code=9223372036854775807";
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            [
                "TEST_START m01.stage1",
                testFailure,
                stageFailure
            ]);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "cleanup-failure",
            qemuPath,
            includeOrdered: false,
            includeUnique: false,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 8
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke forbidden_marker",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "marker=TEST_FAIL",
                StringComparison.Ordinal)
            && summary.Contains(
                "FORBIDDEN_MARKER_HIT TEST_FAIL;M01_STAGE1_FAIL",
                StringComparison.Ordinal)
            && summary.Contains(
                testFailure,
                StringComparison.Ordinal)
            && summary.Contains(
                stageFailure,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01UnexpectedSwitchReturnIsRejected(
        string tempRoot)
    {
        const string testFailure =
            "TEST_FAIL m01.stage1 status=20 adapter=29 checkpoint=8 free_data=18446744073709551615 free_code=18446744073709551615";
        const string stageFailure =
            "M01_STAGE1_FAIL status=20 adapter=29 checkpoint=8 free_data=18446744073709551615 free_code=18446744073709551615";
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            [
                "TEST_START m01.stage1",
                testFailure,
                stageFailure
            ]);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "unexpected-switch-return",
            qemuPath,
            includeOrdered: false,
            includeUnique: false,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 8
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke forbidden_marker",
                StringComparison.Ordinal)
            && summary.Contains(
                "FORBIDDEN_MARKER_HIT TEST_FAIL;M01_STAGE1_FAIL",
                StringComparison.Ordinal)
            && summary.Contains(
                "adapter=29",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01TimeoutIsDeterministic(string tempRoot)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            ["TEST_START m01.stage1"]);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "timeout",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 1,
            out string summary);

        return result.ExitCode == 4
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke timeout",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON timeout",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01RebootSignatureIsRejected(string tempRoot)
    {
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            CreateM01StageOneSerialLines().ToArray());

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "reboot-signature",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode != 0
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_reboot_or_shutdown",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON qemu_reboot_or_shutdown",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01ConflictingTerminalsAreRejected(
        string tempRoot)
    {
        List<string> serialLines = CreateM01StageOneSerialLines();
        serialLines.Add("OS_SMOKE_FAIL");
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageOneQemu(
            tempRoot,
            "conflicting-terminals",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode != 0
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "forbidden_marker",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "FORBIDDEN_MARKER_HIT OS_SMOKE_FAIL",
                StringComparison.Ordinal)
            && summary.Contains(
                "SERIAL_TAIL_LINE OS_SMOKE_FAIL",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestDryRunVirtioNetworkArguments(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("-netdev user,id=n0", StringComparison.Ordinal)
            && output.Contains("-device virtio-net-pci,netdev=n0", StringComparison.Ordinal);
    }

    private static bool TestDryRunDefaultRawDiskFormat(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath);

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("if=none,id=esp,format=raw,file=" + imagePath, StringComparison.Ordinal)
            && !output.Contains("if=none,id=esp,format=qcow2,file=" + imagePath, StringComparison.Ordinal);
    }

    private static bool TestDryRunQcow2DiskFormat(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.qcow2");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--disk-format",
            "qcow2");

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("if=none,id=esp,format=qcow2,file=" + imagePath, StringComparison.Ordinal);
    }

    private static bool TestRejectsInvalidDiskFormat(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--disk-format",
            "vmdk");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("Value for --disk-format must be raw or qcow2", StringComparison.Ordinal);
    }

    private static bool TestDryRunDoesNotWriteSummary(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");
        string summaryLog = Path.Combine(tempRoot, "dry-run-summary.log");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--summary-log",
            summaryLog);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && !File.Exists(summaryLog);
    }

    private static bool TestDryRunHttpProbeContract(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--http-probe-url",
            "http://127.0.0.1:8080/healthz",
            "--http-probe-expected-status",
            "204",
            "--http-probe-expected-body",
            "OS_MINIMAL_API_OK",
            "--http-probe-timeout",
            "5");

        string output = result.Output;
        return result.ExitCode == 0
            && output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)
            && output.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && output.Contains("HTTP_PROBE url=http://127.0.0.1:8080/healthz expected_status=204 expected_body_bytes=17 timeout_seconds=5", StringComparison.Ordinal);
    }

    private static bool TestRejectsHttpsHttpProbe(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--http-probe-url",
            "https://127.0.0.1:8443/healthz");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("https/TLS is not supported", StringComparison.Ordinal);
    }

    private static bool TestRejectsInvalidHttpProbeStatus(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--http-probe-url",
            "http://127.0.0.1:8080/healthz",
            "--http-probe-expected-status",
            "99");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("HTTP status code from 100 to 599", StringComparison.Ordinal);
    }

    private static bool TestRejectsInvalidHttpProbeTimeout(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--http-probe-url",
            "http://127.0.0.1:8080/healthz",
            "--http-probe-timeout",
            "0");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("Value for --http-probe-timeout must be a positive integer", StringComparison.Ordinal);
    }

    private static bool TestRejectsIncompleteHttpProbe(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "os.raw");

        ProcessResult result = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--http-probe-expected-status",
            "200");

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal)
            && result.Output.Contains("HTTP probe requires --http-probe-url", StringComparison.Ordinal);
    }

    private static bool TestRealModeWritesPassingSummary(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "pass.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "TEST_START fake.qemu",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "pass-serial.log");
        string summaryLog = Path.Combine(tempRoot, "pass-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal)
            && result.Output.Contains(
                "SERIAL_MARKER_STREAM status=Passed terminal=Success",
                StringComparison.Ordinal)
            && result.Output.Contains("SUMMARY_LOG " + summaryLog, StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS pass", StringComparison.Ordinal)
            && summary.Contains("IMAGE_ARTIFACT " + imagePath, StringComparison.Ordinal)
            && summary.Contains("SERIAL_LOG " + serialLog, StringComparison.Ordinal)
            && summary.Contains("WORKLOAD_PROFILE SingleMinimalApi", StringComparison.Ordinal)
            && summary.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && summary.Contains("OBSERVED_MARKER TEST_START fake.qemu", StringComparison.Ordinal)
            && summary.Contains("OBSERVED_MARKER OS_SMOKE_PASS", StringComparison.Ordinal)
            && summary.Contains("APP_EXIT_RESULT none", StringComparison.Ordinal)
            && summary.Contains("APP_EXIT_MALFORMED none", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL", StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKER_HIT none", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON none", StringComparison.Ordinal);
    }

    private static bool TestRealModeRejectsDuplicateSuccessTerminals(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "duplicate-terminal.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "duplicate-terminal-OVMF_CODE.fd");
        string qemuPath = CreateDelayedTerminalFakeQemuScript(
            tempRoot,
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "duplicate-terminal-serial.log");
        string summaryLog = Path.Combine(tempRoot, "duplicate-terminal-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 13
            && result.Output.Contains(
                "SERIAL_MARKER_STREAM status=DuplicateTerminalMarker terminal=Success",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke serial_marker_stream status=DuplicateTerminalMarker",
                StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON serial_marker_stream_duplicateterminalmarker",
                StringComparison.Ordinal);
    }

    private static bool TestRealModeRequiresSuccessTerminalWithCustomMarker(
        string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "custom-no-terminal.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "custom-no-terminal-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "CUSTOM_READY");
        string serialLog = Path.Combine(
            tempRoot,
            "custom-no-terminal-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "custom-no-terminal-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--require-marker",
            "CUSTOM_READY",
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 5
            && result.Output.Contains(
                "REQUIRED_MARKERS " +
                DefaultBootRequiredMarkers +
                ";CUSTOM_READY",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke missing_marker",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "missing=OS_SMOKE_PASS",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "MISSING_MARKERS OS_SMOKE_PASS",
                StringComparison.Ordinal);
    }

    private static bool TestRealModeRejectsNonzeroQemuExit(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "nonzero-exit.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "nonzero-exit-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScriptWithExitCode(
            tempRoot,
            42,
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "nonzero-exit-serial.log");
        string summaryLog = Path.Combine(tempRoot, "nonzero-exit-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 14
            && result.Output.Contains(
                "QEMU_PROCESS_RESULT disposition=exited exitCode=42",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_exit_nonzero code=42",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
             && summary.Contains(
                 "FAIL_REASON qemu_exit_nonzero",
                 StringComparison.Ordinal);
    }

    private static bool TestRealModeRejectsTestFailBeforeSuccess(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "test-fail.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "test-fail-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "OS_BOOT_OK",
            "TEST_FAIL boot.sequence deliberate",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "test-fail-serial.log");
        string summaryLog = Path.Combine(tempRoot, "test-fail-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 8
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke forbidden_marker marker=TEST_FAIL",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "FORBIDDEN_MARKER_HIT TEST_FAIL",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON forbidden_marker",
                StringComparison.Ordinal);
    }

    private static bool TestDrainClassifiesNonzeroExitBeforeKill(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "drain-exit.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "drain-exit-OVMF_CODE.fd");
        string qemuPath = CreateDelayedExitFakeQemuScript(
            tempRoot,
            42,
            100);
        string serialLog = Path.Combine(tempRoot, "drain-exit-serial.log");
        string summaryLog = Path.Combine(tempRoot, "drain-exit-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 14
            && result.Output.Contains(
                "QEMU_PROCESS_RESULT disposition=exited exitCode=42",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_exit_nonzero code=42",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON qemu_exit_nonzero",
                StringComparison.Ordinal);
    }

    private static bool TestBootTimeoutPrecedesLateMarkers(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "late-markers.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "late-markers-OVMF_CODE.fd");
        string qemuPath = CreateLateMarkerFakeQemuScript(tempRoot, 1500);
        string serialLog = Path.Combine(tempRoot, "late-markers-serial.log");
        string summaryLog = Path.Combine(tempRoot, "late-markers-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "1");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 4
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke timeout",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal)
            && summary.Contains(
                "QEMU_SMOKE_STATUS fail",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON timeout",
                StringComparison.Ordinal);
    }

    private static bool TestInvalidMarkerConfigurationDoesNotLaunchChild(
        string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "invalid-marker.raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "invalid-marker-OVMF_CODE.fd");
        string launchSentinel = Path.Combine(
            tempRoot,
            "invalid-marker-launched.txt");
        string qemuPath = CreateLaunchSentinelFakeQemuScript(
            tempRoot,
            launchSentinel);
        string serialLog = Path.Combine(tempRoot, "invalid-marker-serial.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--require-marker",
            "OS_SMOKE_FAIL");

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL qemu.runner.invalid_args",
                StringComparison.Ordinal)
            && !File.Exists(launchSentinel)
            && !File.Exists(serialLog);
    }

    private static bool TestRejectsArtifactPathAliases(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "alias-image.raw");
        File.WriteAllBytes(imagePath, new byte[] { 0x45, 0x46, 0x49, 0x20 });
        string serialPath = Path.Combine(tempRoot, "alias-serial.log");
        string relativeAlias = Path.Combine(
            tempRoot,
            ".",
            "alias-serial.log");

        ProcessResult serialSummary = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--serial-log",
            serialPath,
            "--summary-log",
            relativeAlias);
        ProcessResult imageSummary = RunQemuRunner(
            "--dry-run",
            "--image",
            imagePath,
            "--summary-log",
            imagePath);

        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS");
        byte[] qemuBytesBefore = File.ReadAllBytes(qemuPath);
        ProcessResult qemuSummary = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--image",
            imagePath,
            "--summary-log",
            qemuPath);
        bool qemuUnchanged = qemuBytesBefore.SequenceEqual(
            File.ReadAllBytes(qemuPath));

        string discoveredOvmfPath = Path.Combine(
            tempRoot,
            "alias-discovered-OVMF_CODE.fd");
        byte[] ovmfBytesBefore = { 0x4F, 0x56, 0x4D, 0x46 };
        File.WriteAllBytes(discoveredOvmfPath, ovmfBytesBefore);
        ProcessResult discoveredOvmfSerial;
        string? previousOvmf = Environment.GetEnvironmentVariable(
            "OS_OVMF_CODE");
        try
        {
            Environment.SetEnvironmentVariable(
                "OS_OVMF_CODE",
                discoveredOvmfPath);
            discoveredOvmfSerial = RunQemuRunner(
                "--qemu",
                qemuPath,
                "--image",
                imagePath,
                "--serial-log",
                discoveredOvmfPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "OS_OVMF_CODE",
                previousOvmf);
        }

        bool discoveredOvmfUnchanged = ovmfBytesBefore.SequenceEqual(
            File.ReadAllBytes(discoveredOvmfPath));

        bool caseAliasRejected = true;
        if (OperatingSystem.IsWindows())
        {
            ProcessResult windowsCaseAlias = RunQemuRunner(
                "--dry-run",
                "--image",
                imagePath,
                "--serial-log",
                serialPath,
                "--summary-log",
                serialPath.ToUpperInvariant());
            caseAliasRejected = windowsCaseAlias.ExitCode == 1
                && windowsCaseAlias.Output.Contains(
                    "Artifact paths must be distinct",
                    StringComparison.Ordinal);
        }

        return serialSummary.ExitCode == 1
            && imageSummary.ExitCode == 1
            && qemuSummary.ExitCode == 1
            && discoveredOvmfSerial.ExitCode == 1
            && qemuUnchanged
            && discoveredOvmfUnchanged
            && caseAliasRejected
            && serialSummary.Output.Contains(
                "Artifact paths must be distinct",
                StringComparison.Ordinal)
            && imageSummary.Output.Contains(
                "Artifact paths must be distinct",
                StringComparison.Ordinal)
            && qemuSummary.Output.Contains(
                "Artifact paths must be distinct",
                StringComparison.Ordinal)
            && discoveredOvmfSerial.Output.Contains(
                "Resolved runtime paths must be distinct",
                StringComparison.Ordinal);
    }

    private static bool TestRealModeWritesAppExitSummary(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "app-exit.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "app-exit-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "TEST_START fake.qemu",
            "APP_EXIT code=0 status=OSHOST_STATUS_OK",
            "APP_EXIT code=abc status=OSHOST_STATUS_OK",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "app-exit-serial.log");
        string summaryLog = Path.Combine(tempRoot, "app-exit-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal)
            && summary.Contains("OBSERVED_MARKER APP_EXIT code=0 status=OSHOST_STATUS_OK", StringComparison.Ordinal)
            && summary.Contains("APP_EXIT_RESULT code=0 status=OSHOST_STATUS_OK", StringComparison.Ordinal)
            && summary.Contains("APP_EXIT_MALFORMED reason=invalid_code line=APP_EXIT code=abc status=OSHOST_STATUS_OK", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON none", StringComparison.Ordinal);
    }

    private static bool TestRealModeFailsForbiddenMarker(string tempRoot)
    {
        const string forbiddenMarker = "UEFI_EBS_FRESH_MAP_PROBE_START";
        string imagePath = CreateEmptyFile(tempRoot, "forbidden.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "forbidden-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(tempRoot, "TEST_START fake.qemu", forbiddenMarker, "OS_SMOKE_PASS");
        string serialLog = Path.Combine(tempRoot, "forbidden-serial.log");
        string summaryLog = Path.Combine(tempRoot, "forbidden-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3",
            "--forbid-marker",
            forbiddenMarker);

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 8
            && result.Output.Contains("OS_SMOKE_FAIL forbidden-marker=" + forbiddenMarker, StringComparison.Ordinal)
            && result.Output.Contains("TEST_FAIL qemu.smoke forbidden_marker marker=" + forbiddenMarker, StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL;" + forbiddenMarker, StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKER_HIT " + forbiddenMarker, StringComparison.Ordinal)
            && summary.Contains("SERIAL_TAIL_LINE " + forbiddenMarker, StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON forbidden_marker", StringComparison.Ordinal);
    }

    private static bool TestRealModeWritesMissingMarkerSummary(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "missing-marker.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "missing-marker-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(tempRoot, "TEST_START fake.qemu");
        string serialLog = Path.Combine(tempRoot, "missing-marker-serial.log");
        string summaryLog = Path.Combine(tempRoot, "missing-marker-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 5
            && result.Output.Contains("TEST_FAIL qemu.smoke missing_marker", StringComparison.Ordinal)
            && result.Output.Contains("SUMMARY_LOG " + summaryLog, StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("REQUIRED_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && summary.Contains("OBSERVED_MARKER TEST_START fake.qemu", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKER_HIT none", StringComparison.Ordinal)
            && summary.Contains("SERIAL_TAIL_LINE TEST_START fake.qemu", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON missing_marker", StringComparison.Ordinal);
    }

    private static bool TestSummaryIncludesBoundedSerialTail(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "bounded-tail.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "bounded-tail-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            "line-01",
            "line-02",
            "line-03",
            "line-04",
            "line-05",
            "line-06",
            "line-07",
            "line-08",
            "line-09",
            "line-10");
        string serialLog = Path.Combine(tempRoot, "bounded-tail-serial.log");
        string summaryLog = Path.Combine(tempRoot, "bounded-tail-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 5
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && summary.Contains("SERIAL_TAIL_LIMIT lines=8 chars=240", StringComparison.Ordinal)
            && CountOccurrences(summary, "SERIAL_TAIL_LINE ") == 8
            && !summary.Contains("SERIAL_TAIL_LINE line-01", StringComparison.Ordinal)
            && !summary.Contains("SERIAL_TAIL_LINE line-02", StringComparison.Ordinal)
            && summary.Contains("SERIAL_TAIL_LINE line-03", StringComparison.Ordinal)
            && summary.Contains("SERIAL_TAIL_LINE line-10", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON missing_marker", StringComparison.Ordinal);
    }

    private static bool TestRequiredMarkerFailureDistinctFromForbiddenMarker(string tempRoot)
    {
        string imagePath = CreateEmptyFile(tempRoot, "missing-required-with-forbidden.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "missing-required-with-forbidden-OVMF_CODE.fd");
        string qemuPath = CreateFakeQemuScript(tempRoot, "TEST_START fake.qemu");
        string serialLog = Path.Combine(tempRoot, "missing-required-with-forbidden-serial.log");
        string summaryLog = Path.Combine(tempRoot, "missing-required-with-forbidden-summary.log");

        ProcessResult result = RunQemuRunner(
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            "3",
            "--forbid-marker",
            "UEFI_EBS_FRESH_MAP_PROBE_START");

        string summary = File.ReadAllText(summaryLog);
        return result.ExitCode == 5
            && result.Output.Contains("TEST_FAIL qemu.smoke missing_marker", StringComparison.Ordinal)
            && !result.Output.Contains("forbidden_marker", StringComparison.Ordinal)
            && summary.Contains("QEMU_SMOKE_STATUS fail", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS " + DefaultBootRequiredMarkers, StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL;UEFI_EBS_FRESH_MAP_PROBE_START", StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKER_HIT none", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON missing_marker", StringComparison.Ordinal);
    }

    private static bool TestSummaryWriteFailureIsVisible(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "summary-write-failure.raw");
        ProcessResult result = RunQemuRunner(
            "--image",
            imagePath,
            "--summary-log",
            tempRoot,
            "--timeout",
            "1");

        return result.ExitCode == 2
            && result.Output.Contains("TEST_FAIL qemu.smoke image_not_found", StringComparison.Ordinal)
            && result.Output.Contains("SUMMARY_LOG_WRITE_FAILED path=" + tempRoot, StringComparison.Ordinal);
    }

    private static bool TestMissingImage(string tempRoot)
    {
        string imagePath = Path.Combine(tempRoot, "missing.raw");
        ProcessResult result = RunQemuRunner("--image", imagePath, "--timeout", "1");

        return result.ExitCode == 2
            && result.Output.Contains("TEST_FAIL qemu.smoke image_not_found", StringComparison.Ordinal);
    }

    private static string CreateEmptyFile(string tempRoot, string fileName)
    {
        string path = Path.Combine(tempRoot, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static string CreateRawImageFile(string tempRoot, string fileName)
    {
        string path = Path.Combine(tempRoot, fileName);
        File.WriteAllBytes(path, new byte[] { 0x4F, 0x53, 0x52, 0x41, 0x57 });
        return path;
    }

    private static string CreateFakeQemuScript(string tempRoot, params string[] serialLines)
    {
        return CreateFakeQemuScriptWithExitCode(
            tempRoot,
            0,
            serialLines);
    }

    private static string CreateFakeQemuScriptWithExitCode(
        string tempRoot,
        int exitCode,
        params string[] serialLines)
    {
        string path = Path.Combine(tempRoot, "fake-qemu-" + Guid.NewGuid().ToString("N") + ".cmd");
        List<string> script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12"
        ];

        bool firstLine = true;
        foreach (string serialLine in serialLines)
        {
            script.Add((firstLine ? ">" : ">>") + " \"%serial%\" echo " + serialLine);
            firstLine = false;
        }

        script.Add("exit /b " + exitCode);
        File.WriteAllLines(path, script);
        return path;
    }

    private static List<string> CreateM01StageOneSerialLines()
    {
        List<string> serialLines = [];
        foreach (string marker in M01StageOneGuestMarkers)
        {
            if (marker == "OS_SMOKE_PASS")
            {
                serialLines.Add("OS_BOOT_OK");
                serialLines.Add("TEST_PASS boot.sequence");
            }

            serialLines.Add(marker);
        }

        return serialLines;
    }

    private static ProcessResult RunM01StageOneQemu(
        string tempRoot,
        string scenario,
        string qemuPath,
        bool includeOrdered,
        bool includeUnique,
        int timeoutSeconds,
        out string summary)
    {
        string suffix =
            SanitizeFileName(scenario) + "-" + Guid.NewGuid().ToString("N");
        string imagePath = CreateEmptyFile(
            tempRoot,
            "m01-" + suffix + ".raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "m01-" + suffix + "-OVMF_CODE.fd");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-" + suffix + "-summary.log");
        List<string> arguments =
        [
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            timeoutSeconds.ToString()
        ];

        foreach (string marker in M01StageOneGuestMarkers)
        {
            arguments.Add("--require-marker");
            arguments.Add(marker);
        }

        if (includeOrdered)
        {
            foreach (string marker in M01StageOneGuestMarkers)
            {
                arguments.Add("--require-ordered-marker");
                arguments.Add(marker);
            }
        }

        if (includeUnique)
        {
            foreach (string marker in M01StageOneGuestMarkers)
            {
                arguments.Add("--require-unique-marker");
                arguments.Add(marker);
            }
        }

        arguments.Add("--forbid-marker");
        arguments.Add("M01_INTEGRATION_FAIL");
        arguments.Add("--forbid-marker");
        arguments.Add("M01_STAGE1_FAIL");

        ProcessResult result = RunQemuRunner(arguments.ToArray());
        summary = File.Exists(summaryLog)
            ? File.ReadAllText(summaryLog)
            : string.Empty;
        return result;
    }

    private static string SanitizeFileName(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]))
            {
                characters[index] = '-';
            }
        }

        return new string(characters).Trim('-');
    }

    private static string CreatePersistentFakeQemuScript(
        string tempRoot,
        IReadOnlyList<string> serialLines)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-persistent-" + Guid.NewGuid().ToString("N") + ".cmd");
        List<string> script = CreateFakeQemuScriptPreamble();
        AddSerialLines(script, serialLines);
        script.Add(":hold");
        script.Add(
            "powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 30\"");
        script.Add("goto hold");
        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreatePartialMarkerFakeQemuScript(
        string tempRoot,
        IReadOnlyList<string> completeLines,
        string partialMarker)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-partial-" + Guid.NewGuid().ToString("N") + ".cmd");
        List<string> script = CreateFakeQemuScriptPreamble();
        AddSerialLines(script, completeLines);
        script.Add(
            (completeLines.Count == 0 ? ">" : ">>")
                + " \"%serial%\" <nul set /p \"="
                + partialMarker
                + "\"");
        script.Add("exit /b 0");
        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateChunkedPersistentFakeQemuScript(
        string tempRoot,
        IReadOnlyList<string> serialLines,
        string chunkedMarker)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-chunked-" + Guid.NewGuid().ToString("N") + ".cmd");
        List<string> script = CreateFakeQemuScriptPreamble();
        bool firstLine = true;
        foreach (string serialLine in serialLines)
        {
            if (serialLine == chunkedMarker)
            {
                int splitIndex = serialLine.Length / 2;
                string prefix = serialLine[..splitIndex];
                string suffix = serialLine[splitIndex..];
                script.Add(
                    (firstLine ? ">" : ">>")
                        + " \"%serial%\" <nul set /p \"="
                        + prefix
                        + "\"");
                script.Add(
                    "powershell.exe -NoProfile -Command "
                        + "\"Start-Sleep -Milliseconds 250\"");
                script.Add(
                    ">> \"%serial%\" <nul set /p \"="
                        + suffix
                        + "\"");
                script.Add(">> \"%serial%\" echo(");
            }
            else
            {
                script.Add(
                    (firstLine ? ">" : ">>")
                        + " \"%serial%\" echo "
                        + serialLine);
            }

            firstLine = false;
        }

        script.Add(":hold");
        script.Add(
            "powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 30\"");
        script.Add("goto hold");
        File.WriteAllLines(path, script);
        return path;
    }

    private static List<string> CreateFakeQemuScriptPreamble()
    {
        return
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12"
        ];
    }

    private static void AddSerialLines(
        List<string> script,
        IReadOnlyList<string> serialLines)
    {
        bool firstLine = true;
        foreach (string serialLine in serialLines)
        {
            script.Add(
                (firstLine ? ">" : ">>")
                    + " \"%serial%\" echo "
                    + serialLine);
            firstLine = false;
        }
    }

    private static string CreateLaunchSentinelFakeQemuScript(
        string tempRoot,
        string launchSentinel)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-sentinel-" + Guid.NewGuid().ToString("N") + ".cmd");
        string[] script =
        [
            "@echo off",
            "> \"" + launchSentinel + "\" echo launched",
            "exit /b 0"
        ];

        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateDelayedTerminalFakeQemuScript(
        string tempRoot,
        string delayedTerminal)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-delayed-" + Guid.NewGuid().ToString("N") + ".cmd");
        string[] script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12",
            "> \"%serial%\" echo OS_BOOT_OK",
            ">> \"%serial%\" echo TEST_PASS boot.sequence",
            ">> \"%serial%\" echo OS_SMOKE_PASS",
            "powershell.exe -NoProfile -Command \"Start-Sleep -Milliseconds 200\"",
            ">> \"%serial%\" echo " + delayedTerminal,
            "exit /b 0"
        ];

        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateDelayedExitFakeQemuScript(
        string tempRoot,
        int exitCode,
        int delayMilliseconds)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-exit-" + Guid.NewGuid().ToString("N") + ".cmd");
        string[] script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12",
            "> \"%serial%\" echo OS_BOOT_OK",
            ">> \"%serial%\" echo TEST_PASS boot.sequence",
            ">> \"%serial%\" echo OS_SMOKE_PASS",
            "powershell.exe -NoProfile -Command \"Start-Sleep -Milliseconds " +
                delayMilliseconds +
                "\"",
            "exit /b " + exitCode
        ];

        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateLateMarkerFakeQemuScript(
        string tempRoot,
        int delayMilliseconds)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-late-" + Guid.NewGuid().ToString("N") + ".cmd");
        string[] script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12",
            "powershell.exe -NoProfile -Command \"Start-Sleep -Milliseconds " +
                delayMilliseconds +
                "\"",
            "> \"%serial%\" echo OS_BOOT_OK",
            ">> \"%serial%\" echo TEST_PASS boot.sequence",
            ">> \"%serial%\" echo OS_SMOKE_PASS",
            "exit /b 0"
        ];

        File.WriteAllLines(path, script);
        return path;
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            index = source.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }

    private static ProcessResult RunQemuSmokeScript(params string[] args)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1");

        ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start powershell.exe.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            throw new TimeoutException("run-qemu-smoke.ps1 did not exit within 120 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult()
                + standardError.GetAwaiter().GetResult());
    }

    private static ProcessResult RunQemuRunner(params string[] args)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(repositoryRoot, "OS.QemuRunner");
        if (!File.Exists(toolPath))
        {
            return new ProcessResult(-1, "qemu runner tool not found: " + toolPath);
        }

        ProcessStartInfo startInfo = HostToolProcess.CreateDotnetToolStartInfo(repositoryRoot, "OS.QemuRunner");

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            throw new TimeoutException("OS.QemuRunner did not exit within 30 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult()
                + standardError.GetAwaiter().GetResult());
    }

    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; }

        public string Output { get; }
    }
}
