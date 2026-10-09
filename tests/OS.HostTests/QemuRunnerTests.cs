using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static class QemuRunnerTests
{
    private static readonly string DefaultBootRequiredMarkers = string.Join(";", OS.Tooling.QemuContracts.RunnerDefaultRequiredMarkers);
    private static readonly string SmokeScriptDefaultRequiredMarkers = string.Join(";", OS.Tooling.QemuSmokeOptions.DefaultRequiredMarkers);
    private static readonly string[] M01StageOneGuestMarkers = OS.Tooling.QemuContracts.M01StageOneGuestMarkers.ToArray();
    private static readonly string[] M01StageTwoGuestMarkers = OS.Tooling.QemuContracts.M01StageTwoGuestMarkers.ToArray();
    private static readonly string[] M01StageTwoForbiddenMarkers = OS.Tooling.QemuContracts.M01StageTwoForbiddenMarkers.ToArray();
    private static readonly string[] M01StageThreeGuestMarkers = OS.Tooling.QemuContracts.M01StageThreeGuestMarkers.ToArray();
    private static readonly string[] M01StageThreeForbiddenMarkers = OS.Tooling.QemuContracts.M01StageThreeForbiddenMarkers.ToArray();
    private static readonly string[] M02HistoricalManifestSuccessMarkers = OS.Tooling.QemuContracts.M02HistoricalManifestSuccessMarkers.ToArray();
    private static readonly string[] M02IssueSuccessMarkers = OS.Tooling.QemuContracts.M02IssueSuccessMarkers.ToArray();
    private static readonly string[] M02IssueForbiddenMarkers = OS.Tooling.QemuContracts.M02IssueForbiddenMarkers.ToArray();
    private static readonly string[] M02GuestMarkers = OS.Tooling.QemuContracts.M02SafeStubRequiredMarkers.ToArray();
    private static readonly string[] M02ForbiddenMarkers = OS.Tooling.QemuContracts.M02SafeStubForbiddenMarkers.ToArray();
    private static readonly string M02CheckedPortIoBindingReadyMarker = OS.Tooling.QemuContracts.M02CheckedPortIoBindingReadyMarker[0];
    private static readonly string[] M02CheckedPortIoBindingGuestMarkers = OS.Tooling.QemuContracts.M02CheckedPortIoBindingRequiredMarkers.ToArray();
    private static readonly string[] M02CheckedPortIoBindingForbiddenMarkers = OS.Tooling.QemuContracts.M02CheckedPortIoBindingForbiddenMarkers.ToArray();
    private static readonly string M02Ud2VectorStateReadyMarker = OS.Tooling.QemuContracts.M02Ud2VectorStateReadyMarker[0];
    private const string M02Ud2VectorStateHostOnlyMarker =
        "CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 auditFields=23 probeEnabled=false executionRequested=false executionAttempted=false syntheticObservation=true guestExecution=false faultTriggered=false idtrLoaded=false postExitBootServices=false";
    private const string M02Ud2VectorStateHostOnlyMarkerPrefix =
        "CLROS_M02_013_HOST_READY";
    private static readonly string[] M02Ud2VectorStateGuestMarkers =
    [
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "TEST_START m02.013",
        M02Ud2VectorStateReadyMarker,
        "TEST_PASS m02.013"
    ];

    [HostFixture(HostLane.Native)]
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
            Run("qemu firmware RNG option preserves default arguments", () => TestFirmwareRngDefault(tempRoot), ref failures);
            Run("qemu firmware RNG virtio arguments and source evidence", () => TestFirmwareRngSource(tempRoot, "virtio"), ref failures);
            Run("qemu firmware RNG missing provider has no device", () => TestFirmwareRngSource(tempRoot, "missing"), ref failures);
            Run("qemu firmware RNG rejects unknown empty and duplicate choices", TestFirmwareRngRejected, ref failures);
            Run("qemu dry-run emits default workload profile", () => TestDryRunDefaultWorkloadProfile(tempRoot), ref failures);
            Run("qemu dry-run propagates multi workload profile", () => TestDryRunMultiWorkloadProfile(tempRoot), ref failures);
            Run("qemu rejects invalid workload profile", () => TestRejectsInvalidWorkloadProfile(tempRoot), ref failures);
            Run("qemu smoke script defaults require boot boundary markers", TestSmokeScriptDefaultMarkers, ref failures);
            Run("qemu smoke script rejects unrequested first-exit records", () => TestSmokeScriptRejectsUnrequestedFirstExitRecords(tempRoot), ref failures);
            Run("qemu smoke script forwards ACPI physical discovery EFI probe", TestSmokeScriptForwardsAcpiPhysicalDiscoveryProbe, ref failures);
            Run("qemu smoke script forwards M01 stage-one evidence controls", () => TestSmokeScriptForwardsM01StageOneEvidenceControls(tempRoot), ref failures);
            Run("qemu smoke script rejects stale M01 probe artifact selection", () => TestSmokeScriptRejectsStaleM01ProbeArtifactSelection(tempRoot), ref failures);
            Run("qemu smoke script forwards M01 stage-two evidence controls", () => TestSmokeScriptForwardsM01StageTwoEvidenceControls(tempRoot), ref failures);
            Run("qemu smoke script rejects invalid M01 stage-two probe selection", () => TestSmokeScriptRejectsInvalidM01StageTwoProbeSelection(tempRoot), ref failures);
            Run("qemu M01 stage-three manifest marker contract is exact", TestM01StageThreeManifestMarkerContract, ref failures);
            Run("qemu smoke script forwards M01 stage-three evidence controls", () => TestSmokeScriptForwardsM01StageThreeEvidenceControls(tempRoot), ref failures);
            Run("qemu smoke script rejects invalid M01 stage-three probe selection", () => TestSmokeScriptRejectsInvalidM01StageThreeProbeSelection(tempRoot), ref failures);
            Run("qemu M01 raw-observation shim source compiles strictly", () => TestM01RawObservationShimSourceCompiles(tempRoot), ref failures);
            Run("qemu M01 raw-observation shim markers match stage three", TestM01RawObservationShimMarkerParity, ref failures);
            Run("qemu M02-017 manifest marker contract is exact", TestM02SafeStubManifestMarkerContract, ref failures);
            Run("qemu smoke script enforces the M02-017 evidence profile", () => TestSmokeScriptEnforcesM02SafeStubEvidenceProfile(tempRoot), ref failures);
            Run("qemu smoke script rejects invalid M02-017 probe selection", () => TestSmokeScriptRejectsInvalidM02SafeStubSelection(tempRoot), ref failures);
            Run("qemu M02-017 raw-observation shim markers match the profile", TestM02RawObservationShimMarkerParity, ref failures);
            Run("qemu smoke script enforces the M02-003 checked-binding profile", () => TestSmokeScriptEnforcesM02CheckedPortIoBindingProfile(tempRoot), ref failures);
            Run("qemu smoke script rejects invalid M02-003 checked-binding selection", () => TestSmokeScriptRejectsInvalidM02CheckedPortIoBindingSelection(tempRoot), ref failures);
            Run("qemu smoke script enforces the M02-013 UD2 vector-state terminal profile", () => TestSmokeScriptEnforcesM02Ud2VectorStateProfile(tempRoot), ref failures);
            Run("qemu smoke script rejects invalid M02-013 UD2 vector-state selections", () => TestSmokeScriptRejectsInvalidM02Ud2VectorStateSelections(tempRoot), ref failures);
            Run("qemu M02-013 terminal transcript validator rejects post-pass marker noise", () => TestM02Ud2VectorStateTerminalTranscriptValidation(tempRoot), ref failures);
            Run("qemu M01 raw-observation dry-run is inactive and side-effect free", () => TestM01RawObservationDryRunIsInactiveAndSideEffectFree(tempRoot), ref failures);
            Run("qemu M01 raw-observation log requires owned paging", () => TestM01RawObservationLogRequiresOwnedPaging(tempRoot), ref failures);
            Run("qemu M01 raw-observation rejects summary path overlap before launch", () => TestM01RawObservationRejectsSummaryPathOverlap(tempRoot), ref failures);
            Run("qemu M01 raw-observation wrapper is fail closed", TestM01RawObservationWrapperIsFailClosed, ref failures);
            Run("qemu M01 raw-observation verifier accepts a complete valid sidecar", () => TestM01RawObservationVerifierAcceptsValidSidecar(tempRoot), ref failures);
            Run("qemu M01 raw-observation verifier rejects a corrupted POD hash", () => TestM01RawObservationVerifierRejectsPodHashMismatch(tempRoot), ref failures);
            Run("qemu M01 raw-observation verifier rejects a serial hash mismatch", () => TestM01RawObservationVerifierRejectsSerialHashMismatch(tempRoot), ref failures);
            Run("qemu M01 raw-observation verifier rejects invalid runtime addresses", () => TestM01RawObservationVerifierRejectsInvalidRuntimeAddresses(tempRoot), ref failures);
            Run("qemu M01 raw-observation verifier rejects a call-gate address mismatch", () => TestM01RawObservationVerifierRejectsCallGateAddressMismatch(tempRoot), ref failures);
            Run("qemu M01 raw-observation verifier rejects a rehashed semantic POD corruption", () => TestM01RawObservationVerifierRejectsSemanticPodCorruption(tempRoot), ref failures);
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
            Run("qemu M01 stage-two ordered and unique evidence succeeds", () => TestM01StageTwoOrderedAndUniqueEvidenceSucceeds(tempRoot), ref failures);
            Run("qemu M01 stage-two rejects cross-stage and operation reordering", () => TestM01StageTwoRejectsReordering(tempRoot), ref failures);
            Run("qemu M01 stage-two rejects duplicate start pass and operation markers", () => TestM01StageTwoRejectsDuplicateMarkers(tempRoot), ref failures);
            Run("qemu M01 stage-two rejects every missing stage-two marker", () => TestM01StageTwoRejectsMissingMarkers(tempRoot), ref failures);
            Run("qemu M01 stage-two rejects adapter failure classifications", () => TestM01StageTwoRejectsAdapterFailures(tempRoot), ref failures);
            Run("qemu M01 stage-two rejects every forbidden boundary marker", () => TestM01StageTwoRejectsForbiddenBoundaryMarkers(tempRoot), ref failures);
            Run("qemu M01 stage-two timeout is deterministic", () => TestM01StageTwoTimeoutIsDeterministic(tempRoot), ref failures);
            Run("qemu M01 stage-two reset is rejected", () => TestM01StageTwoResetIsRejected(tempRoot), ref failures);
            Run("qemu M01 stage-two conflicting terminal is rejected", () => TestM01StageTwoConflictingTerminalIsRejected(tempRoot), ref failures);
            Run("qemu M01 stage-three cumulative ordered and unique evidence succeeds", () => TestM01StageThreeOrderedAndUniqueEvidenceSucceeds(tempRoot), ref failures);
            Run("qemu M01 stage-three rejects every missing cumulative marker", () => TestM01StageThreeRejectsEveryMissingMarker(tempRoot), ref failures);
            Run("qemu M01 stage-three rejects cross-stage and live-boundary reordering", () => TestM01StageThreeRejectsReordering(tempRoot), ref failures);
            Run("qemu M01 stage-three rejects duplicate stage operation aggregate ready and terminal markers", () => TestM01StageThreeRejectsDuplicateMarkers(tempRoot), ref failures);
            Run("qemu M01 stage-three rejects every forbidden boundary marker", () => TestM01StageThreeRejectsEveryForbiddenMarker(tempRoot), ref failures);
            Run("qemu M01 stage-three rejects cumulative failure classifications", () => TestM01StageThreeRejectsFailureClassifications(tempRoot), ref failures);
            Run("qemu M01 stage-three timeout is deterministic", () => TestM01StageThreeTimeoutIsDeterministic(tempRoot), ref failures);
            Run("qemu M01 stage-three reset is rejected", () => TestM01StageThreeResetIsRejected(tempRoot), ref failures);
            Run("qemu M01 stage-three conflicting terminal is rejected", () => TestM01StageThreeConflictingTerminalIsRejected(tempRoot), ref failures);
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
            Run("qemu expected panic rejects invalid options before launch", () => TestUnexpectedPanicOptions(tempRoot), ref failures);
            Run("qemu expected panic accepts exact capture and native 19 summary", () => TestUnexpectedPanicAcceptedCapture(tempRoot), ref failures);
            Run("qemu expected panic rejects malformed metadata prefix and EOF", () => TestUnexpectedPanicMalformedCaptures(tempRoot), ref failures);
            Run("qemu expected panic preserves prerequisite marker validation", () => TestUnexpectedPanicPrefixMarkerIsolation(tempRoot), ref failures);
            Run("qemu expected panic remains isolated from generic smoke", () => TestUnexpectedPanicDefaultIsolation(tempRoot), ref failures);
            Run("qemu expected panic rejects late bytes resets exits and timeout", () => TestUnexpectedPanicLifecycle(tempRoot), ref failures);
            Run("qemu expected panic cleans only owned processes and rejects summary failure", () => TestUnexpectedPanicOwnedCleanup(tempRoot), ref failures);
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

        HostCheck.Run("" + name, test, ref failures);
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

    private static bool TestFirmwareRngDefault(string root)
    {
        ProcessResult r = RunQemuRunner("--dry-run", "--qemu", "qemu-test", "--image", Path.Combine(root,"rng-default.raw"));
        return r.ExitCode == 0 && r.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal) &&
            !r.Output.Contains("FIRMWARE_RNG_SOURCE", StringComparison.Ordinal) && !r.Output.Contains("-cpu", StringComparison.Ordinal) &&
            !r.Output.Contains("rng-builtin", StringComparison.Ordinal) && !r.Output.Contains("virtio-rng-pci", StringComparison.Ordinal);
    }
    private static bool TestFirmwareRngSource(string root, string source)
    {
        ProcessResult r = RunQemuRunner("--dry-run", "--qemu", "qemu-test", "--image", Path.Combine(root,"rng.raw"), "--firmware-rng-source", source);
        if (r.ExitCode != 0 || !r.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal) ||
            r.Output.Split('\n').Count(x => x.TrimEnd('\r') == "FIRMWARE_RNG_SOURCE " + source) != 1) return false;
        string command = r.Output.Split('\n').Single(x => x.StartsWith("qemu-test ", StringComparison.Ordinal));
        return command.Contains("-device ich9-ahci,id=ahci", StringComparison.Ordinal) &&
            command.IndexOf("ide-hd,drive=esp,bus=ahci.0",StringComparison.Ordinal) > command.IndexOf("ich9-ahci,id=ahci",StringComparison.Ordinal) &&
            command.IndexOf("virtio-net-pci,netdev=n0",StringComparison.Ordinal) > command.IndexOf("ide-hd,drive=esp,bus=ahci.0",StringComparison.Ordinal) &&
            command.IndexOf("-cpu qemu64,-rdrand,-rdseed",StringComparison.Ordinal) > command.IndexOf("virtio-net-pci,netdev=n0",StringComparison.Ordinal) &&
            command.Contains("-cpu qemu64,-rdrand,-rdseed", StringComparison.Ordinal) &&
            (source == "virtio" ? command.Contains("-object rng-builtin,id=clrosrng -device virtio-rng-pci,rng=clrosrng", StringComparison.Ordinal) :
                !command.Contains("rng-builtin", StringComparison.Ordinal) && !command.Contains("virtio-rng-pci", StringComparison.Ordinal));
    }
    private static bool TestFirmwareRngRejected()
    {
        foreach (string[] args in new string[][] {
            ["--firmware-rng-source"], ["--firmware-rng-source", ""], ["--firmware-rng-source", "Virtio"],
            ["--firmware-rng-source", "missing "], ["--firmware-rng-source", "host"],
            ["--firmware-rng-source", "virtio", "--firmware-rng-source", "virtio"],
            ["--firmware-rng-source", "virtio", "--firmware-rng-source", "missing"] })
        {
            ProcessResult r = RunQemuRunner(["--dry-run", ..args]);
            if (r.ExitCode == 0 || r.Output.Contains("TEST_PASS", StringComparison.Ordinal) ||
                r.Output.Contains("TEST_START qemu.runner.dryrun", StringComparison.Ordinal)) return false;
        }
        return true;
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
        var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse("--boot-policy", "Diagnostic"));
        return plan.Required.SequenceEqual(OS.Tooling.QemuSmokeOptions.DefaultRequiredMarkers)
            && plan.Forbidden.Contains("OS_SMOKE_FAIL") && !plan.BuildEfi;
    }

    private static bool TestSmokeScriptRejectsUnrequestedFirstExitRecords(string tempRoot)
    {
        string imagePath = CreateRawImageFile(tempRoot, "unrequested-first-exit.raw");
        string ovmfPath = CreateEmptyFile(tempRoot, "unrequested-first-exit-OVMF_CODE.fd");
        string[][] cases =
        [
            ["TEST_START m03.007", "M03_EBS_RETURNED_SUCCESS", "M03_PERMANENT_CR3_ADOPTED",
                "M03_POST_EBS_OWNED_DIAGNOSTIC", "CLROS_M03_007_READY", "TEST_PASS m03.007"],
            ["TEST_START m03.009", "UEFI_EBS_SUCCESS", "POST_FIRMWARE_DIAGNOSTICS_READY",
                "CLROS_M03_009_READY", "TEST_PASS m03.009"],
            // This existing simulation/readiness marker alone makes no actual-exit claim.
            ["POST_FIRMWARE_DIAGNOSTICS_READY"]
        ];
        for (int index = 0; index < cases.Length; index++)
        {
            string name = "unrequested-first-exit-" + index;
            string serialLog = Path.Combine(tempRoot, name + "-serial.log");
            string summaryLog = Path.Combine(tempRoot, name + "-summary.log");
            string qemuPath = CreateFakeQemuScript(tempRoot,
            [
                .. SmokeScriptDefaultRequiredMarkers.Split(';').Where(marker => marker != "OS_SMOKE_PASS"),
                .. cases[index],
                "OS_SMOKE_PASS"
            ]);
            ProcessResult result = RunQemuSmokeScript(
                "-SkipImageBuild", "-Image", imagePath,
                "-SerialLog", serialLog, "-SummaryLog", summaryLog,
                "-Qemu", qemuPath, "-OvmfCode", ovmfPath);
            File.WriteAllText(Path.Combine(tempRoot, name + "-wrapper.log"),
                "WRAPPER_EXIT " + result.ExitCode + Environment.NewLine + result.Output);
            bool mustReject = index < 2;
            if (result.ExitCode != (mustReject ? 1 : 0)
                || !result.Output.Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal)
                || result.Output.Contains("TEST_FAIL qemu.smoke m03_first_exit_unselected_record", StringComparison.Ordinal) != mustReject
                || !File.ReadAllText(serialLog).Contains(cases[index][0], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool TestSmokeScriptForwardsAcpiPhysicalDiscoveryProbe()
    {
        var options = OS.Tooling.QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--build-efi", "--marker-preset", "acpi", "-EnableAcpiPhysicalDiscoveryProbe");
        var plan = OS.Tooling.QemuProfiles.Create(options);
        return plan.IsEnabled("EnableAcpiPhysicalDiscoveryProbe") && plan.BuildEfi && plan.Presets.SequenceEqual(new[] { "acpi" });
    }

    private static bool TestSmokeScriptForwardsM01StageOneEvidenceControls(
        string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
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
                "TEST_FAIL qemu.smoke qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !noBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestSmokeScriptForwardsM01StageTwoEvidenceControls(
        string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string imagePath = Path.Combine(
            tempRoot,
            "m01-stage-two-script-dry-run.raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-stage-two-script-dry-run-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-stage-two-script-dry-run-summary.log");
        string markers = string.Join(",", M01StageTwoGuestMarkers);
        string formattedMarkers = string.Join(";", M01StageTwoGuestMarkers);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
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
            && result.Output.Contains(
                "ORDERED_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsInvalidM01StageTwoProbeSelection(
        string tempRoot)
    {
        string staleEfi = Path.Combine(
            tempRoot,
            "stale-stage-two-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4D, 0x5A, 0x01, 0x02]);
        byte[] before = File.ReadAllBytes(staleEfi);

        ProcessResult missingStageOne = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LiveAllocatorProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingBuild = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult skippedImage = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-SkipImageBuild",
            "-EfiArtifact",
            staleEfi);

        return missingStageOne.ExitCode == 1
            && missingBuild.ExitCode == 1
            && skippedImage.ExitCode == 1
            && missingStageOne.Output.Contains(
                "TEST_FAIL qemu.smoke m01_live_allocator_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !missingStageOne.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestM01StageThreeManifestMarkerContract()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string manifestPath = Path.Combine(
            repositoryRoot,
            ".gitea",
            "backlog",
            "clros-issues.json");
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(manifestPath));

        JsonElement issue = default;
        bool issueFound = false;
        foreach (JsonElement candidate in
            manifest.RootElement.GetProperty("issues").EnumerateArray())
        {
            if (candidate.GetProperty("key").GetString() == "M01-026")
            {
                issue = candidate;
                issueFound = true;
                break;
            }
        }

        if (!issueFound
            || M01StageThreeGuestMarkers.Length != OS.Tooling.QemuContracts.M01StageThreeGuestMarkers.Count
            || M01StageThreeForbiddenMarkers.Length != OS.Tooling.QemuContracts.M01StageThreeForbiddenMarkers.Count
            || M01StageThreeGuestMarkers.Distinct(
                StringComparer.Ordinal).Count() != M01StageThreeGuestMarkers.Length
            || M01StageThreeForbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() != M01StageThreeForbiddenMarkers.Length)
        {
            return false;
        }

        string[] requiredMarkers = issue
            .GetProperty("requiredMarkers")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        string[] expectedRequiredMarkers =
        [
            "TEST_START qemu.smoke",
            .. M01StageThreeGuestMarkers,
            "TEST_PASS qemu.smoke",
            "TEST_FAIL m01.stage3 <reason>",
            "TEST_FAIL qemu.smoke <reason>"
        ];
        if (!requiredMarkers.SequenceEqual(
                expectedRequiredMarkers,
                StringComparer.Ordinal))
        {
            return false;
        }

        string[] commands = issue
            .GetProperty("requiredCommands")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        string? dryRunCommand = commands.SingleOrDefault(
            command => command.Contains(
                "dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun",
                StringComparison.Ordinal));
        string? realCommand = commands.SingleOrDefault(
            command => command.Contains(
                "dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi",
                StringComparison.Ordinal)
                && command.Contains(
                    "-Qemu \"<qemu-system-x86_64.exe>\"",
                    StringComparison.Ordinal));
        string markerArgument =
            "\"" + string.Join(",", M01StageThreeGuestMarkers) + "\"";
        string forbiddenArgument =
            "\"" + string.Join(",", M01StageThreeForbiddenMarkers) + "\"";

        return dryRunCommand is not null
            && realCommand is not null
            && CountOccurrences(
                dryRunCommand,
                "-EnableM01LoadedImageStackProbe") == 1
            && CountOccurrences(
                dryRunCommand,
                "-EnableM01LiveAllocatorProbe") == 1
            && CountOccurrences(
                dryRunCommand,
                "-EnableM01OwnedPagingProbe") == 1
            && CountOccurrences(
                realCommand,
                "-EnableM01LoadedImageStackProbe") == 1
            && CountOccurrences(
                realCommand,
                "-EnableM01LiveAllocatorProbe") == 1
            && CountOccurrences(
                realCommand,
                "-EnableM01OwnedPagingProbe") == 1
            && CountOccurrences(
                dryRunCommand,
                "-RequireMarker " + markerArgument) == 1
            && CountOccurrences(
                dryRunCommand,
                "-RequireOrderedMarker " + markerArgument) == 1
            && CountOccurrences(
                dryRunCommand,
                "-RequireUniqueMarker " + markerArgument) == 1
            && CountOccurrences(
                realCommand,
                "-RequireMarker " + markerArgument) == 1
            && CountOccurrences(
                realCommand,
                "-RequireOrderedMarker " + markerArgument) == 1
            && CountOccurrences(
                realCommand,
                "-RequireUniqueMarker " + markerArgument) == 1
            && CountOccurrences(
                realCommand,
                "-ForbidMarker " + forbiddenArgument) == 1
            && !dryRunCommand.Contains(
                "-ForbidMarker",
                StringComparison.Ordinal)
            && !markerArgument.Contains(
                "M01_OWNED_CR3_ENTERED",
                StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptForwardsM01StageThreeEvidenceControls(
        string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string imagePath = Path.Combine(
            tempRoot,
            "m01-stage-three-script-dry-run.raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-stage-three-script-dry-run-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-stage-three-script-dry-run-summary.log");
        string markers = string.Join(",", M01StageThreeGuestMarkers);
        string formattedMarkers = string.Join(";", M01StageThreeGuestMarkers);
        string normalizedRequiredMarkers =
            DefaultBootRequiredMarkers
            + ";"
            + string.Join(
                ";",
                M01StageThreeGuestMarkers.Where(
                    marker => marker != "OS_SMOKE_PASS"));
        string forbidden = string.Join(",", M01StageThreeForbiddenMarkers);
        string[] runnerForbiddenMarkers =
        [
            "OS_SMOKE_FAIL",
            "TEST_FAIL",
            .. M01StageThreeForbiddenMarkers.Where(
                marker => marker != "OS_SMOKE_FAIL"
                    && marker != "TEST_FAIL")
        ];
        string formattedForbidden = string.Join(
            ";",
            runnerForbiddenMarkers);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
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
            markers,
            "-ForbidMarker",
            forbidden);

        return result.ExitCode == 0
            && result.Output.Contains(
                "REQUIRED_MARKERS " + normalizedRequiredMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "ORDERED_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "FORBIDDEN_MARKERS " + formattedForbidden,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsInvalidM01StageThreeProbeSelection(
        string tempRoot)
    {
        string staleEfi = Path.Combine(
            tempRoot,
            "stale-stage-three-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4D, 0x5A, 0x03, 0x03]);
        byte[] before = File.ReadAllBytes(staleEfi);

        ProcessResult missingStageTwo = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01OwnedPagingProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult transitiveMissingStageOne = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingBuild = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult skippedImage = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-SkipImageBuild",
            "-EfiArtifact",
            staleEfi);

        return missingStageTwo.ExitCode == 1
            && transitiveMissingStageOne.ExitCode == 1
            && missingBuild.ExitCode == 1
            && skippedImage.ExitCode == 1
            && missingStageTwo.Output.Contains(
                "TEST_FAIL qemu.smoke m01_owned_paging_requires_live_allocator",
                StringComparison.Ordinal)
            && transitiveMissingStageOne.Output.Contains(
                "TEST_FAIL qemu.smoke m01_live_allocator_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !missingStageTwo.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !transitiveMissingStageOne.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestM01RawObservationShimSourceCompiles(
        string tempRoot)
    {
        string path = HostToolProcess.GetBuiltToolPath(HostToolProcess.FindRepositoryRoot(), "OS.QemuCapture");
        return File.Exists(path) && File.Exists(Path.ChangeExtension(path, ".exe"));
    }

    private static bool TestM01RawObservationShimMarkerParity()
    {
        return M01StageThreeGuestMarkers.SequenceEqual(OS.Tooling.QemuContracts.M01StageThreeGuestMarkers)
            && M01StageThreeForbiddenMarkers.SequenceEqual(OS.Tooling.QemuContracts.M01StageThreeForbiddenMarkers)
            && M01StageThreeGuestMarkers.Distinct(StringComparer.Ordinal).Count() == M01StageThreeGuestMarkers.Length
            && M01StageThreeForbiddenMarkers.Distinct(StringComparer.Ordinal).Count() == M01StageThreeForbiddenMarkers.Length;
    }

    private static bool TestM02SafeStubManifestMarkerContract()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string manifestPath = Path.Combine(
            repositoryRoot,
            ".gitea",
            "backlog",
            "clros-issues.json");
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        JsonElement issue = default;
        bool found = false;
        foreach (JsonElement candidate in
            manifest.RootElement.GetProperty("issues").EnumerateArray())
        {
            if (candidate.GetProperty("key").GetString() == "M02-017")
            {
                issue = candidate;
                found = true;
                break;
            }
        }

        if (!found
            || M02GuestMarkers.Length != OS.Tooling.QemuContracts.M02SafeStubRequiredMarkers.Count
            || M02ForbiddenMarkers.Length != OS.Tooling.QemuContracts.M02SafeStubForbiddenMarkers.Count
            || M02GuestMarkers.Distinct(
                StringComparer.Ordinal).Count() != M02GuestMarkers.Length
            || M02ForbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() != M02ForbiddenMarkers.Length)
        {
            return false;
        }

        string[] expectedManifestMarkers =
        [
            .. M02HistoricalManifestSuccessMarkers,
            .. M02IssueForbiddenMarkers.Take(8),
            "TEST_FAIL m02.017 <reason>"
        ];
        string[] manifestMarkers = issue
            .GetProperty("requiredMarkers")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        if (!manifestMarkers.SequenceEqual(
                expectedManifestMarkers,
                StringComparer.Ordinal))
        {
            return false;
        }

        string[] commands = issue
            .GetProperty("requiredCommands")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        string? realQemuCommand = commands.SingleOrDefault(command =>
            command.Contains(
                "dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi",
                StringComparison.Ordinal)
            && command.Contains(
                "-EnableM02SafeStubProbe",
                StringComparison.Ordinal));

        return realQemuCommand is not null
            && realQemuCommand.Contains(
                "-EnableM01LoadedImageStackProbe",
                StringComparison.Ordinal)
            && realQemuCommand.Contains(
                "-EnableM01LiveAllocatorProbe",
                StringComparison.Ordinal)
            && realQemuCommand.Contains(
                "-EnableM01OwnedPagingProbe",
                StringComparison.Ordinal)
            && realQemuCommand.Contains(
                "-Qemu \"C:\\Program Files\\qemu\\qemu-system-x86_64.exe\"",
                StringComparison.Ordinal)
            && realQemuCommand.Contains(
                "-OvmfCode \"C:\\Program Files\\qemu\\share\\edk2-x86_64-code.fd\"",
                StringComparison.Ordinal)
            && !realQemuCommand.Contains(
                "-DryRun",
                StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptEnforcesM02SafeStubEvidenceProfile(
        string tempRoot)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string imagePath = Path.Combine(
            tempRoot,
            "m02-safe-stub-dry-" + suffix + ".raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m02-safe-stub-dry-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m02-safe-stub-dry-" + suffix + "-summary.log");
        string formattedGuest = string.Join(";", M02GuestMarkers);
        string normalizedRequired =
            SmokeScriptDefaultRequiredMarkers
            + ";"
            + string.Join(
                ";",
                M02GuestMarkers.Where(marker => marker != "OS_SMOKE_PASS"));
        string[] runnerForbidden =
        [
            "OS_SMOKE_FAIL",
            "TEST_FAIL",
            .. M02ForbiddenMarkers.Where(
                marker => marker != "OS_SMOKE_FAIL"
                    && marker != "TEST_FAIL")
        ];
        string formattedForbidden = string.Join(";", runnerForbidden);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);

        return result.ExitCode == 0
            && result.Output.Contains(
                "REQUIRED_MARKERS " + normalizedRequired,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "ORDERED_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "FORBIDDEN_MARKERS " + formattedForbidden,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(imagePath)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsInvalidM02SafeStubSelection(
        string tempRoot)
    {
        string staleEfi = Path.Combine(
            tempRoot,
            "stale-m02-safe-stub-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4d, 0x5a, 0x02, 0x17]);
        byte[] before = File.ReadAllBytes(staleEfi);

        ProcessResult missingLoadedImage = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM02SafeStubProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingAllocator = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM02SafeStubProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingOwnedPaging = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM02SafeStubProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingBuild = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult skippedImage = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-SkipImageBuild",
            "-EfiArtifact",
            staleEfi);

        return missingLoadedImage.ExitCode == 1
            && missingAllocator.ExitCode == 1
            && missingOwnedPaging.ExitCode == 1
            && missingBuild.ExitCode == 1
            && skippedImage.ExitCode == 1
            && missingLoadedImage.Output.Contains(
                "m02_safe_stub_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingAllocator.Output.Contains(
                "m02_safe_stub_requires_live_allocator",
                StringComparison.Ordinal)
            && missingOwnedPaging.Output.Contains(
                "m02_safe_stub_requires_owned_paging",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !missingLoadedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingAllocator.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingOwnedPaging.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestSmokeScriptEnforcesM02CheckedPortIoBindingProfile(
        string tempRoot)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string imagePath = Path.Combine(
            tempRoot,
            "m02-checked-binding-dry-" + suffix + ".raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m02-checked-binding-dry-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m02-checked-binding-dry-" + suffix + "-summary.log");
        string formattedGuest = string.Join(
            ";",
            M02CheckedPortIoBindingGuestMarkers);
        string normalizedRequired =
            SmokeScriptDefaultRequiredMarkers
            + ";"
            + string.Join(
                ";",
                M02CheckedPortIoBindingGuestMarkers.Where(
                    marker => marker != "OS_SMOKE_PASS"));
        string[] runnerForbidden =
        [
            "OS_SMOKE_FAIL",
            "TEST_FAIL",
            .. M02CheckedPortIoBindingForbiddenMarkers.Where(
                marker => marker != "OS_SMOKE_FAIL"
                    && marker != "TEST_FAIL")
        ];
        string formattedForbidden = string.Join(";", runnerForbidden);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);

        return result.ExitCode == 0
            && result.Output.Contains(
                "REQUIRED_MARKERS " + normalizedRequired,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "ORDERED_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "FORBIDDEN_MARKERS " + formattedForbidden,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(imagePath)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsInvalidM02CheckedPortIoBindingSelection(
        string tempRoot)
    {
        string staleEfi = Path.Combine(
            tempRoot,
            "stale-m02-checked-binding-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4d, 0x5a, 0x02, 0x03]);
        byte[] before = File.ReadAllBytes(staleEfi);

        ProcessResult missingSafeStub = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM02CheckedPortIoBindingProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult missingBuild = RunQemuSmokeScript(
            "-DryRun",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe",
            "-EfiArtifact",
            staleEfi);
        ProcessResult skippedImage = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe",
            "-SkipImageBuild",
            "-EfiArtifact",
            staleEfi);

        return missingSafeStub.ExitCode == 1
            && missingBuild.ExitCode == 1
            && skippedImage.ExitCode == 1
            && missingSafeStub.Output.Contains(
                "m02_checked_binding_requires_safe_stub",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && !missingSafeStub.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !missingBuild.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !skippedImage.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestSmokeScriptEnforcesM02Ud2VectorStateProfile(
        string tempRoot)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string imagePath = Path.Combine(
            tempRoot,
            "m02-ud2-vector-state-dry-" + suffix + ".raw");
        string serialLog = Path.Combine(
            tempRoot,
            "m02-ud2-vector-state-dry-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m02-ud2-vector-state-dry-" + suffix + "-summary.log");
        string terminalTail =
            string.Join(";", M02Ud2VectorStateGuestMarkers);
        string profileTail =
            "TEST_START m02.013;"
            + M02Ud2VectorStateReadyMarker
            + ";TEST_PASS m02.013";

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM02Ud2VectorStateProbe",
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();

        return result.ExitCode == 0
            && result.Output.Contains(
                terminalTail,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "REQUIRED_MARKERS " + SmokeScriptDefaultRequiredMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                profileTail,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "UNIQUE_MARKERS ",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "M02_UD2_VECTOR_STATE_VERIFY_FAILED;TEST_FAIL m02.013 vector_or_register_mismatch;"
                    + M02Ud2VectorStateHostOnlyMarkerPrefix
                    + ";"
                    + M02Ud2VectorStateHostOnlyMarker,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !File.Exists(imagePath)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog);
    }

    private static bool TestSmokeScriptRejectsInvalidM02Ud2VectorStateSelections(
        string tempRoot)
    {
        string staleEfi = Path.Combine(
            tempRoot,
            "stale-m02-ud2-vector-state-BOOTX64.EFI");
        File.WriteAllBytes(staleEfi, [0x4d, 0x5a, 0x02, 0x13]);
        byte[] before = File.ReadAllBytes(staleEfi);
        string[] enabled =
        [
            "-DryRun",
            "-BuildEfi",
            "-EnableM02Ud2VectorStateProbe",
            "-EfiArtifact",
            staleEfi
        ];

        ProcessResult missingBuild = RunQemuSmokeScript(
            enabled.Where(argument => argument != "-BuildEfi").ToArray());
        ProcessResult skippedImage = RunQemuSmokeScript(
            [.. enabled, "-SkipImageBuild"]);
        ProcessResult ownedX64GdtConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02OwnedX64GdtEncodingProbe"]);
        ProcessResult guardedIstConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02GuardedIstStackAllocationProbe"]);
        ProcessResult terminalConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02OwnedGdtTssTerminalProbe"]);
        ProcessResult normalizedConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02NormalizedInterruptFramePageProbe"]);
        ProcessResult idtConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02OwnedIdtGatePopulationProbe"]);
        ProcessResult idtrConflict = RunQemuSmokeScript(
            [.. enabled, "-EnableM02IdtrContinuedExecutionProbe"]);
        ProcessResult independentM04Allowed = RunQemuSmokeScript(
            [.. enabled, "-EnableM04IndependentEndianConformanceProbe"]);
        ProcessResult m04Conflict = RunQemuSmokeScript(
            [
                .. enabled,
                "-EnableM04IndependentEndianConformanceProbe",
                "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
            ]);
        ProcessResult rawObservationConflict = RunQemuSmokeScript(
            [
                .. enabled,
                "-M01RawObservationLog",
                Path.Combine(tempRoot, "m02-013-raw-observation.json")
            ]);
        ProcessResult untypedMarker = RunQemuSmokeScript(
            "-DryRun",
            "-RequireMarker",
            "CLROS_M02_013_READY");
        ProcessResult untypedHostMarker = RunQemuSmokeScript(
            "-DryRun",
            "-RequireMarker",
            "CLROS_M02_013_HOST_READY");

        ProcessResult[] failures =
        [
            missingBuild,
            skippedImage,
            ownedX64GdtConflict,
            guardedIstConflict,
            terminalConflict,
            normalizedConflict,
            idtConflict,
            idtrConflict,
            m04Conflict,
            rawObservationConflict,
            untypedMarker,
            untypedHostMarker
        ];
        return failures.All(result => result.ExitCode == 1)
            && independentM04Allowed.ExitCode == 0
            && independentM04Allowed.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "qemu_profile_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "qemu_profile_forbids_skip_image_build",
                StringComparison.Ordinal)
            && ownedX64GdtConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe",
                StringComparison.Ordinal)
            && guardedIstConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe",
                StringComparison.Ordinal)
            && terminalConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && normalizedConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe",
                StringComparison.Ordinal)
            && idtConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe",
                StringComparison.Ordinal)
            && idtrConflict.Output.Contains(
                "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe",
                StringComparison.Ordinal)
            && m04Conflict.Output.Contains(
                "m04_span_memory_conformance_forbids_m02_ud2_vector_state",
                StringComparison.Ordinal)
            && rawObservationConflict.Output.Contains(
                "m01_raw_observation_log_forbidden_terminal_profile",
                StringComparison.Ordinal)
            && untypedMarker.Output.Contains(
                "m02_ud2_vector_state_requires_explicit_profile",
                StringComparison.Ordinal)
            && untypedHostMarker.Output.Contains(
                "m02_ud2_vector_state_requires_explicit_profile",
                StringComparison.Ordinal)
            && RunQemuSmokeScript([.. enabled, "-EnableM01LoadedImageStackProbe:false"]).ExitCode == 1
            && failures.All(result => !result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal))
            && before.AsSpan().SequenceEqual(File.ReadAllBytes(staleEfi));
    }

    private static bool TestM02Ud2VectorStateTerminalTranscriptValidation(
        string tempRoot)
    {
        string directory = Path.Combine(
            tempRoot,
            "m02-ud2-vector-state-transcript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string successPath = Path.Combine(directory, "success.log");
        string postPassNoisePath = Path.Combine(directory, "post-pass-noise.log");
        string reorderedPath = Path.Combine(directory, "reordered.log");
        string alteredReadyPath = Path.Combine(directory, "altered-ready.log");
        string missingSmokePath = Path.Combine(directory, "missing-smoke.log");
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();

        string[] successLines =
        [
            "EFI_CONSOLE_READY",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS",
            "TEST_START m02.013",
            M02Ud2VectorStateReadyMarker,
            "TEST_PASS m02.013"
        ];
        File.WriteAllLines(successPath, successLines);
        File.WriteAllLines(
            postPassNoisePath,
            [.. successLines, "OS_BOOT_OK"]);
        File.WriteAllLines(
            reorderedPath,
            [
                .. successLines[..4],
                M02Ud2VectorStateReadyMarker,
                "TEST_START m02.013",
                "TEST_PASS m02.013"
            ]);
        File.WriteAllLines(
            alteredReadyPath,
            [
                .. successLines[..5],
                M02Ud2VectorStateReadyMarker.Replace(
                    "registerFields=23",
                    "registerFields=22",
                    StringComparison.Ordinal),
                "TEST_PASS m02.013"
            ]);
        File.WriteAllLines(
            missingSmokePath,
            successLines.Where(line => line != "OS_SMOKE_PASS"));

        ProcessResult success = ValidateUd2TranscriptFixture(
            successPath,
            expectedReason: "none");
        ProcessResult postPassNoise = ValidateUd2TranscriptFixture(
            postPassNoisePath,
            expectedReason: "m02_ud2_vector_state_post_smoke_sequence_invalid");
        ProcessResult reordered = ValidateUd2TranscriptFixture(
            reorderedPath,
            expectedReason: "m02_ud2_vector_state_post_smoke_sequence_invalid");
        ProcessResult alteredReady = ValidateUd2TranscriptFixture(
            alteredReadyPath,
            expectedReason: "m02_ud2_vector_state_post_smoke_sequence_invalid");
        ProcessResult missingSmoke = ValidateUd2TranscriptFixture(
            missingSmokePath,
            expectedReason: "m02_ud2_vector_state_smoke_terminal_missing");

        ProcessResult[] results =
        [
            success,
            postPassNoise,
            reordered,
            alteredReady,
            missingSmoke
        ];
        bool passed = results.All(result => result.ExitCode == 0)
            && success.Output.Contains(
                "M02_UD2_VECTOR_STATE_TRANSCRIPT_VALID",
                StringComparison.Ordinal)
            && postPassNoise.Output.Contains(
                "M02_UD2_VECTOR_STATE_TRANSCRIPT_REJECTED reason=m02_ud2_vector_state_post_smoke_sequence_invalid",
                StringComparison.Ordinal)
            && reordered.Output.Contains(
                "M02_UD2_VECTOR_STATE_TRANSCRIPT_REJECTED reason=m02_ud2_vector_state_post_smoke_sequence_invalid",
                StringComparison.Ordinal)
            && alteredReady.Output.Contains(
                "M02_UD2_VECTOR_STATE_TRANSCRIPT_REJECTED reason=m02_ud2_vector_state_post_smoke_sequence_invalid",
                StringComparison.Ordinal)
            && missingSmoke.Output.Contains(
                "M02_UD2_VECTOR_STATE_TRANSCRIPT_REJECTED reason=m02_ud2_vector_state_smoke_terminal_missing",
                StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(
                "M02-013 transcript validator outputs:\n"
                + string.Join("\n", results.Select(result => result.Output)));
        }

        return passed;
    }

    private static bool TestM02RawObservationShimMarkerParity()
    {
        return M02GuestMarkers.SequenceEqual(OS.Tooling.QemuContracts.M02SafeStubRequiredMarkers)
            && M02ForbiddenMarkers.SequenceEqual(OS.Tooling.QemuContracts.M02SafeStubForbiddenMarkers)
            && M02GuestMarkers.Distinct(StringComparer.Ordinal).Count() == M02GuestMarkers.Length
            && M02ForbiddenMarkers.Distinct(StringComparer.Ordinal).Count() == M02ForbiddenMarkers.Length;
    }

    private static bool TestM01RawObservationDryRunIsInactiveAndSideEffectFree(
        string tempRoot)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string observationLog = Path.Combine(
            tempRoot,
            "m01-raw-dry-" + suffix + ".log");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-raw-dry-serial-" + suffix + ".log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-raw-dry-summary-" + suffix + ".log");
        string imagePath = Path.Combine(
            tempRoot,
            "m01-raw-dry-image-" + suffix + ".raw");
        string[] environmentNames = GetM01RawObservationEnvironmentNames();
        Dictionary<string, string?> environmentBefore =
            SnapshotProcessEnvironment(environmentNames);
        string[] pathsBefore = SnapshotFileSystemEntries(tempRoot);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-BuildEfi",
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-M01RawObservationLog",
            observationLog,
            "-Image",
            imagePath,
            "-SerialLog",
            serialLog,
            "-SummaryLog",
            summaryLog);

        string[] pathsAfter = SnapshotFileSystemEntries(tempRoot);
        Dictionary<string, string?> environmentAfter =
            SnapshotProcessEnvironment(environmentNames);
        bool environmentNamesHidden = environmentNames.All(
            name => !result.Output.Contains(name, StringComparison.Ordinal));

        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_RAW_OBSERVATION_CONFIG enabled=true active=false dryRun=true",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_START m01.026.raw-observations",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_RAW_OBSERVATION_PATH ",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_RAW_OBSERVATION_SUPPORT ",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_RAW_OBSERVATION_TIMEOUTS ",
                StringComparison.Ordinal)
            && environmentNamesHidden
            && EnvironmentSnapshotsEqual(environmentBefore, environmentAfter)
            && pathsBefore.SequenceEqual(pathsAfter, StringComparer.Ordinal)
            && !File.Exists(observationLog)
            && !File.Exists(serialLog)
            && !File.Exists(summaryLog)
            && !File.Exists(observationLog + ".tmp");
    }

    private static bool TestM01RawObservationLogRequiresOwnedPaging(
        string tempRoot)
    {
        string observationLog = Path.Combine(
            tempRoot,
            "m01-raw-invalid-" + Guid.NewGuid().ToString("N") + ".log");
        string[] pathsBefore = SnapshotFileSystemEntries(tempRoot);

        ProcessResult result = RunQemuSmokeScript(
            "-DryRun",
            "-M01RawObservationLog",
            observationLog);

        string[] pathsAfter = SnapshotFileSystemEntries(tempRoot);
        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke m01_raw_observation_log_requires_owned_paging_probe",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_RAW_OBSERVATION_CONFIG ",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_START m01.026.raw-observations",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal)
            && pathsBefore.SequenceEqual(pathsAfter, StringComparer.Ordinal)
            && !File.Exists(observationLog);
    }

    private static bool TestM01RawObservationRejectsSummaryPathOverlap(
        string tempRoot)
    {
        string directory = Path.Combine(tempRoot, "capture-overlap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string same = Path.Combine(directory, "same.log"), serial = Path.Combine(directory, "serial.log");
        string[] before = SnapshotFileSystemEntries(directory);
        try { OS.Tooling.QemuCaptureSession.Create(HostToolProcess.FindRepositoryRoot(), "fake-qemu.exe", same, serial, same, 15, false); return false; }
        catch (OS.Tooling.ToolFailure error) { return error.Reason == "capture_path_overlap" && before.SequenceEqual(SnapshotFileSystemEntries(directory)); }
    }

    private static bool TestM01RawObservationWrapperIsFailClosed()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "tools", "OS.QemuCapture", "Program.cs"));
        string[] events = ExtractM01FatalQmpEvents(source);
        string[] required = ["RESET", "SHUTDOWN", "WATCHDOG", "GUEST_PANICKED", "POWERDOWN", "RESUME"];
        return required.All(events.Contains) && source.Contains("qmp.StopAndRequirePaused();", StringComparison.Ordinal)
            && source.Contains("qmp.Pmemsave(", StringComparison.Ordinal)
            && source.Contains("capture_scratch_not_empty_after_scan", StringComparison.Ordinal)
            && source.Contains("capture_scratch_delete_failed", StringComparison.Ordinal)
            && source.Contains("terminalReleaseAuthorized", StringComparison.Ordinal);
    }

    private static bool TestM01RawObservationVerifierAcceptsValidSidecar(
        string tempRoot)
    {
        M01RawObservationFixture fixture =
            CreateM01RawObservationVerifierFixture(
                tempRoot,
                "valid-sidecar",
                M01RawObservationCorruption.None);
        string expectedSerialHash = M01Sha256(
            File.ReadAllBytes(fixture.PublicSerialPath));
        ProcessResult result = RunM01RawObservationVerifier(
            fixture,
            expectedError: null);

        return result.ExitCode == 0
            && result.Output.Contains(
                "VERIFIER_PASS serialSha256="
                    + expectedSerialHash
                    + " candidates=1",
                StringComparison.Ordinal);
    }

    private static bool TestM01RawObservationVerifierRejectsPodHashMismatch(
        string tempRoot)
    {
        M01RawObservationFixture fixture =
            CreateM01RawObservationVerifierFixture(
                tempRoot,
                "pod-hash-mismatch",
                M01RawObservationCorruption.PodHashMismatch);
        ProcessResult result = RunM01RawObservationVerifier(
            fixture,
            "observation_pod_hash_mismatch");

        return result.ExitCode == 0
            && result.Output.Contains(
                "EXPECTED_FAILURE observation_pod_hash_mismatch",
                StringComparison.Ordinal);
    }

    private static bool TestM01RawObservationVerifierRejectsSerialHashMismatch(
        string tempRoot)
    {
        M01RawObservationFixture fixture =
            CreateM01RawObservationVerifierFixture(
                tempRoot,
                "serial-hash-mismatch",
                M01RawObservationCorruption.SerialHashMismatch);
        ProcessResult result = RunM01RawObservationVerifier(
            fixture,
            "observation_serial_hash_mismatch");

        return result.ExitCode == 0
            && result.Output.Contains(
                "EXPECTED_FAILURE observation_serial_hash_mismatch",
                StringComparison.Ordinal);
    }

    private static bool TestM01RawObservationVerifierRejectsInvalidRuntimeAddresses(
        string tempRoot)
    {
        foreach (M01RawObservationCorruption corruption in
            new[]
            {
                M01RawObservationCorruption.RuntimeAddressMisaligned,
                M01RawObservationCorruption.RuntimeAddressOutOfRange
            })
        {
            M01RawObservationFixture fixture =
                CreateM01RawObservationVerifierFixture(
                    tempRoot,
                    corruption.ToString(),
                    corruption);
            ProcessResult result = RunM01RawObservationVerifier(
                fixture,
                "observation_runtime_address_invalid");
            if (result.ExitCode != 0
                || !result.Output.Contains(
                    "EXPECTED_FAILURE observation_runtime_address_invalid",
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TestM01RawObservationVerifierRejectsCallGateAddressMismatch(
        string tempRoot)
    {
        M01RawObservationFixture fixture =
            CreateM01RawObservationVerifierFixture(
                tempRoot,
                "call-gate-address-mismatch",
                M01RawObservationCorruption.CallGateAddressMismatch);
        ProcessResult result = RunM01RawObservationVerifier(
            fixture,
            "observation_call_gate_address_mismatch");

        return result.ExitCode == 0
            && result.Output.Contains(
                "EXPECTED_FAILURE observation_call_gate_address_mismatch",
                StringComparison.Ordinal);
    }

    private static bool TestM01RawObservationVerifierRejectsSemanticPodCorruption(
        string tempRoot)
    {
        M01RawObservationFixture fixture =
            CreateM01RawObservationVerifierFixture(
                tempRoot,
                "semantic-pod-corruption",
                M01RawObservationCorruption.SemanticRuntimeCr0Mismatch);
        ProcessResult result = RunM01RawObservationVerifier(
            fixture,
            "observation_exact_restoration_mismatch");

        return result.ExitCode == 0
            && result.Output.Contains(
                "EXPECTED_FAILURE observation_exact_restoration_mismatch",
                StringComparison.Ordinal);
    }

    private static bool TestSmokeScriptNativeAppPresetMarkers()
    {
        var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--require-native-app-markers"));
        return OS.Tooling.QemuContracts.nativeAppRequiredMarkers.All(plan.Required.Contains);
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
        var options = OS.Tooling.QemuQcow2Options.Parse("--dry-run");
        var calls = new List<(string Tool, IReadOnlyList<string> Args)>();
        using StringWriter output = new();
        int exit = OS.Tooling.QemuQcow2.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (tool, args) =>
        { calls.Add((tool, args)); return Task.FromResult(new OS.Tooling.ProcessResult(0, "", "", false, false, false, TimeSpan.Zero)); }).GetAwaiter().GetResult();
        return exit == 0 && calls.Count == 2 && calls[0].Tool == "OS.ImageBuilder" && calls[0].Args.Contains("convert-qcow2")
            && calls[1].Tool == "OS.QemuRunner" && calls[1].Args.Contains("qcow2") && calls.All(x => x.Args.Contains("--dry-run"))
            && output.ToString().Contains("TEST_PASS qemu.qcow2.smoke", StringComparison.Ordinal);
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

    private static bool TestM01StageTwoOrderedAndUniqueEvidenceSucceeds(
        string tempRoot)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            CreateM01StageTwoSerialLines());

        ProcessResult result = RunM01StageTwoQemu(
            tempRoot,
            "stage-two-complete",
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
                "FORBIDDEN_MARKER_HIT none",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON none",
                StringComparison.Ordinal);
    }

    private static bool TestM01StageTwoRejectsReordering(string tempRoot)
    {
        List<string> crossStage = CreateM01StageTwoSerialLines();
        int stageOnePass = crossStage.IndexOf("TEST_PASS m01.stage1");
        int stageTwoStart = crossStage.IndexOf("TEST_START m01.stage2");
        (crossStage[stageOnePass], crossStage[stageTwoStart]) =
            (crossStage[stageTwoStart], crossStage[stageOnePass]);

        if (!RunM01StageTwoOutOfOrderCase(
                tempRoot,
                "cross-stage-reordered",
                crossStage,
                "TEST_PASS m01.stage1"))
        {
            return false;
        }

        List<string> operationOrder = CreateM01StageTwoSerialLines();
        int mapRecaptured = operationOrder.IndexOf(
            "M01_STAGE2_MAP_RECAPTURED");
        int reservationsApplied = operationOrder.IndexOf(
            "M01_RESERVATIONS_APPLIED");
        (operationOrder[mapRecaptured], operationOrder[reservationsApplied]) =
            (operationOrder[reservationsApplied], operationOrder[mapRecaptured]);

        return RunM01StageTwoOutOfOrderCase(
            tempRoot,
            "stage-two-operation-reordered",
            operationOrder,
            "M01_STAGE2_MAP_RECAPTURED");
    }

    private static bool TestM01StageTwoRejectsDuplicateMarkers(
        string tempRoot)
    {
        string[] duplicatedMarkers =
        [
            "TEST_START m01.stage2",
            "M01_LIVE_PAGE_WITHIN_ARENA",
            "TEST_PASS m01.stage2"
        ];

        foreach (string duplicatedMarker in duplicatedMarkers)
        {
            List<string> serialLines = CreateM01StageTwoSerialLines();
            int markerIndex = serialLines.IndexOf(duplicatedMarker);
            serialLines.Insert(markerIndex + 1, duplicatedMarker);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageTwoQemu(
                tempRoot,
                "stage-two-duplicate-" + SanitizeFileName(duplicatedMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 17
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke duplicate_unique_marker",
                    StringComparison.Ordinal)
                || !result.Output.Contains(
                    duplicatedMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "DUPLICATE_UNIQUE_MARKERS",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    duplicatedMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FAIL_REASON duplicate_unique_marker",
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

    private static bool TestM01StageTwoRejectsMissingMarkers(string tempRoot)
    {
        string[] stageTwoMarkers =
        [
            "TEST_START m01.stage2",
            "M01_ALLOCATOR_ARENA_ALLOCATED",
            "M01_STAGE2_MAP_RECAPTURED",
            "M01_RESERVATIONS_APPLIED",
            "M01_LIVE_ALLOCATED_4K",
            "M01_LIVE_PAGE_WITHIN_ARENA",
            "M01_LIVE_FREED_4K",
            "M01_DOUBLE_FREE_REJECTED",
            "TEST_PASS m01.stage2"
        ];

        foreach (string missingMarker in stageTwoMarkers)
        {
            List<string> serialLines = CreateM01StageTwoSerialLines();
            serialLines.Remove(missingMarker);
            string qemuPath = CreateFakeQemuScript(
                tempRoot,
                serialLines.ToArray());

            ProcessResult result = RunM01StageTwoQemu(
                tempRoot,
                "stage-two-missing-" + SanitizeFileName(missingMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
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

    private static bool TestM01StageTwoRejectsAdapterFailures(
        string tempRoot)
    {
        (
            string Scenario,
            ulong CoordinatorStatus,
            ulong AdapterStatus,
            ulong Checkpoint,
            ulong FirmwareStatus,
            ulong ArenaCleanupStatus)[] failures =
        [
            (
                "efi-allocation-failure",
                0,
                6,
                0,
                9223372036854775817,
                ulong.MaxValue),
            (
                "arena-geometry-or-type",
                0,
                7,
                0,
                0,
                0),
            (
                "map-recapture-growth-or-overflow",
                0,
                8,
                0,
                9223372036854775813,
                0),
            (
                "stale-map-key",
                0,
                9,
                0,
                0,
                0),
            (
                "arena-map-type-mismatch",
                0,
                10,
                0,
                0,
                0),
            (
                "reservation-mismatch-or-overlap",
                0,
                14,
                0,
                0,
                0),
            (
                "map-ledger-evidence-mismatch",
                0,
                15,
                0,
                0,
                0),
            (
                "page-allocation-failure",
                15,
                16,
                5,
                0,
                0),
            (
                "outside-arena-result",
                16,
                16,
                6,
                0,
                0),
            (
                "page-free-failure",
                17,
                16,
                7,
                0,
                0),
            (
                "double-free-not-rejected",
                19,
                16,
                8,
                0,
                0)
        ];

        foreach ((
            string scenario,
            ulong coordinatorStatus,
            ulong adapterStatus,
            ulong checkpoint,
            ulong firmwareStatus,
            ulong arenaCleanupStatus) in failures)
        {
            string detail =
                "status=" + coordinatorStatus
                + " adapter=" + adapterStatus
                + " checkpoint=" + checkpoint
                + " firmware=" + firmwareStatus
                + " free_arena=" + arenaCleanupStatus;
            List<string> serialLines = CreateM01StageTwoFailurePrefixLines();
            // The runner fails closed on the first forbidden marker and may
            // terminate the child before a following marker is written.
            // Every forbidden marker is exercised separately below.
            serialLines.Add("TEST_FAIL m01.stage2 " + detail);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageTwoQemu(
                tempRoot,
                "stage-two-" + scenario,
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 8
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke forbidden_marker",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FORBIDDEN_MARKER_HIT TEST_FAIL",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    detail,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "TEST_FAIL m01.stage2 " + detail,
                    StringComparison.Ordinal)
                || result.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "QEMU_M01_STAGE2_FAILURE_CLASSIFICATION_DIAGNOSTIC"
                    + " scenario=" + scenario
                    + " adapter=" + adapterStatus
                    + " exit=" + result.ExitCode
                    + " output=" + EscapeDiagnostic(result.Output)
                    + " summary=" + EscapeDiagnostic(summary));
                return false;
            }
        }

        return true;
    }

    private static bool TestM01StageTwoRejectsForbiddenBoundaryMarkers(
        string tempRoot)
    {
        foreach (string forbiddenMarker in M01StageTwoForbiddenMarkers)
        {
            List<string> serialLines = CreateM01StageTwoFailurePrefixLines();
            string serialLine = forbiddenMarker == "TEST_FAIL"
                ? "TEST_FAIL m01.stage2 injected"
                : forbiddenMarker;
            serialLines.Add(serialLine);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageTwoQemu(
                tempRoot,
                "stage-two-forbidden-" + SanitizeFileName(forbiddenMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 8
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke forbidden_marker",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FORBIDDEN_MARKER_HIT",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    forbiddenMarker,
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

    private static bool TestM01StageTwoTimeoutIsDeterministic(string tempRoot)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            CreateM01StageTwoFailurePrefixLines());

        ProcessResult result = RunM01StageTwoQemu(
            tempRoot,
            "stage-two-timeout",
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

    private static bool TestM01StageTwoResetIsRejected(string tempRoot)
    {
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            CreateM01StageTwoSerialLines().ToArray());

        ProcessResult result = RunM01StageTwoQemu(
            tempRoot,
            "stage-two-reset",
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

    private static bool TestM01StageTwoConflictingTerminalIsRejected(
        string tempRoot)
    {
        List<string> serialLines = CreateM01StageTwoSerialLines();
        serialLines.Add("OS_SMOKE_FAIL");
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageTwoQemu(
            tempRoot,
            "stage-two-conflicting-terminal",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode != 0
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke",
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

    private static bool RunM01StageTwoOutOfOrderCase(
        string tempRoot,
        string scenario,
        IReadOnlyList<string> serialLines,
        string expectedMarker)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageTwoQemu(
            tempRoot,
            scenario,
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
                expectedMarker,
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

    private static bool TestM01StageThreeOrderedAndUniqueEvidenceSucceeds(
        string tempRoot)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            CreateM01StageThreeSerialLines());

        ProcessResult result = RunM01StageThreeQemu(
            tempRoot,
            "stage-three-complete",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 8,
            out string summary);

        return result.ExitCode == 0
            && result.Output.Contains(
                "SERIAL_MARKER_STREAM status=Passed terminal=Success",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "orderedSeen=42 outOfOrder=0 uniqueSeen=42 duplicateUnique=0",
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
                "FORBIDDEN_MARKER_HIT none",
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON none",
                StringComparison.Ordinal);
    }

    private static bool TestM01StageThreeRejectsEveryMissingMarker(
        string tempRoot)
    {
        foreach (string missingMarker in M01StageThreeGuestMarkers)
        {
            List<string> serialLines = CreateM01StageThreeSerialLines();
            if (!serialLines.Remove(missingMarker))
            {
                return false;
            }

            string qemuPath = CreateFakeQemuScript(
                tempRoot,
                serialLines.ToArray());
            ProcessResult result = RunM01StageThreeQemu(
                tempRoot,
                "stage-three-missing-" + SanitizeFileName(missingMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 8,
                out string summary);

            if (result.ExitCode != 5
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke missing_marker",
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
                Console.WriteLine(
                    "QEMU_M01_STAGE3_MISSING_DIAGNOSTIC marker="
                    + SanitizeFileName(missingMarker)
                    + " exit="
                    + result.ExitCode
                    + " outputMissingMarker="
                    + result.Output.Contains(
                        "TEST_FAIL qemu.smoke missing_marker",
                        StringComparison.Ordinal)
                    + " summaryMarker="
                    + summary.Contains(missingMarker, StringComparison.Ordinal)
                    + " summaryReason="
                    + summary.Contains(
                        "FAIL_REASON missing_marker",
                        StringComparison.Ordinal)
                    + " unexpectedPass="
                    + result.Output.Contains(
                        "TEST_PASS qemu.smoke",
                        StringComparison.Ordinal));
                return false;
            }
        }

        return true;
    }

    private static bool TestM01StageThreeRejectsReordering(string tempRoot)
    {
        (string First, string Second)[] reorderPairs =
        [
            ("TEST_PASS m01.stage1", "TEST_START m01.stage2"),
            ("TEST_PASS m01.stage2", "TEST_START m01.stage3"),
            ("M01_PAGING_MODE_VALIDATED", "M01_PRE_CR3"),
            ("M01_PRE_CR3", "M01_FIRMWARE_CR3_RESTORED"),
            ("M01_FIRMWARE_CR3_RESTORED", "M01_RFLAGS_RESTORED"),
            ("M01_RFLAGS_RESTORED", "M01_PGE_CYCLE_VERIFIED"),
            ("M01_PGE_CYCLE_VERIFIED", "M01_OWNED_CR3_CANARY_VERIFIED"),
            (
                "M01_OWNED_CR3_CANARY_VERIFIED",
                "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED"),
            (
                "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
                "M01_POST_CR3"),
            ("M01_RW_TO_RX_VERIFIED", "M01_TLB_INVALIDATION_COMPLETE"),
            ("M01_TLB_INVALIDATION_COMPLETE", "M01_RX_PROBE_EXECUTED"),
            ("M01_RX_PROBE_EXECUTED", "M01_PAGE_ZERO_ABSENT"),
            ("TEST_PASS m01.stage3", "CLROS_M01_LIVE_BOOT_MEMORY_READY")
        ];

        foreach ((string first, string second) in reorderPairs)
        {
            List<string> serialLines = CreateM01StageThreeSerialLines();
            int firstIndex = serialLines.IndexOf(first);
            int secondIndex = serialLines.IndexOf(second);
            if (firstIndex < 0 || secondIndex < 0)
            {
                return false;
            }

            (serialLines[firstIndex], serialLines[secondIndex]) =
                (serialLines[secondIndex], serialLines[firstIndex]);
            if (!RunM01StageThreeOutOfOrderCase(
                    tempRoot,
                    "stage-three-reordered-"
                        + SanitizeFileName(first)
                        + "-"
                        + SanitizeFileName(second),
                    serialLines,
                    second))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestM01StageThreeRejectsDuplicateMarkers(
        string tempRoot)
    {
        string[] duplicatedMarkers =
        [
            "TEST_START m01.stage3",
            "M01_CR3_STUB_MATERIALIZED",
            "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
            "TEST_PASS m01.stage3",
            "CLROS_M01_LIVE_BOOT_MEMORY_READY"
        ];

        foreach (string duplicatedMarker in duplicatedMarkers)
        {
            List<string> serialLines = CreateM01StageThreeSerialLines();
            int markerIndex = serialLines.IndexOf(duplicatedMarker);
            serialLines.Insert(markerIndex + 1, duplicatedMarker);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageThreeQemu(
                tempRoot,
                "stage-three-duplicate-" + SanitizeFileName(duplicatedMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 17
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke duplicate_unique_marker",
                    StringComparison.Ordinal)
                || !result.Output.Contains(
                    duplicatedMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "DUPLICATE_UNIQUE_MARKERS",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    duplicatedMarker,
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FAIL_REASON duplicate_unique_marker",
                    StringComparison.Ordinal)
                || result.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        List<string> duplicateTerminal = CreateM01StageThreeSerialLines();
        duplicateTerminal.Add("OS_SMOKE_PASS");
        string duplicateTerminalQemu = CreatePersistentFakeQemuScript(
            tempRoot,
            duplicateTerminal);
        ProcessResult duplicateTerminalResult = RunM01StageThreeQemu(
            tempRoot,
            "stage-three-duplicate-terminal",
            duplicateTerminalQemu,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string duplicateTerminalSummary);

        return duplicateTerminalResult.ExitCode == 13
            && duplicateTerminalResult.Output.Contains(
                "SERIAL_MARKER_STREAM status=DuplicateTerminalMarker terminal=Success",
                StringComparison.Ordinal)
            && duplicateTerminalSummary.Contains(
                "FAIL_REASON serial_marker_stream_duplicateterminalmarker",
                StringComparison.Ordinal)
            && !duplicateTerminalResult.Output.Contains(
                "TEST_PASS qemu.smoke",
                StringComparison.Ordinal);
    }

    private static bool TestM01StageThreeRejectsEveryForbiddenMarker(
        string tempRoot)
    {
        foreach (string forbiddenMarker in M01StageThreeForbiddenMarkers)
        {
            List<string> serialLines =
                CreateM01StageThreeFailurePrefixLines();
            string serialLine = forbiddenMarker switch
            {
                "TEST_FAIL" =>
                    "TEST_FAIL m01.stage3 adapter=24 firmware=0 free_t=0 free_p=0 free_c=0",
                "M01_STAGE3_FAIL" =>
                    "M01_STAGE3_FAIL adapter=24 firmware=0 free_t=0 free_p=0 free_c=0",
                _ => forbiddenMarker
            };
            serialLines.Add(serialLine);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageThreeQemu(
                tempRoot,
                "stage-three-forbidden-"
                    + SanitizeFileName(forbiddenMarker),
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 8
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke forbidden_marker",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FORBIDDEN_MARKER_HIT",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    forbiddenMarker,
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

    private static bool TestM01StageThreeRejectsFailureClassifications(
        string tempRoot)
    {
        for (ulong adapterStatus = 1; adapterStatus <= 26; adapterStatus++)
        {
            ulong firmwareStatus =
                adapterStatus is 4 or 6 or 11
                    ? 9223372036854775817UL
                    : 0UL;
            ulong tableCleanupStatus =
                adapterStatus % 4 == 0
                    ? 9223372036854775815UL
                    : 0UL;
            ulong probeCleanupStatus =
                adapterStatus % 5 == 0
                    ? 9223372036854775815UL
                    : 0UL;
            ulong codeCleanupStatus =
                adapterStatus % 6 == 0
                    ? 9223372036854775815UL
                    : 0UL;
            string detail =
                "adapter=" + adapterStatus
                + " firmware=" + firmwareStatus
                + " free_t=" + tableCleanupStatus
                + " free_p=" + probeCleanupStatus
                + " free_c=" + codeCleanupStatus;
            List<string> serialLines =
                CreateM01StageThreeFailurePrefixLines();
            // The runner fails closed on the first forbidden marker and may
            // terminate the child before a following marker is written.
            // Every forbidden marker is exercised separately above.
            serialLines.Add("TEST_FAIL m01.stage3 " + detail);
            string qemuPath = CreatePersistentFakeQemuScript(
                tempRoot,
                serialLines);

            ProcessResult result = RunM01StageThreeQemu(
                tempRoot,
                "stage-three-adapter-" + adapterStatus,
                qemuPath,
                includeOrdered: true,
                includeUnique: true,
                timeoutSeconds: 5,
                out string summary);

            if (result.ExitCode != 8
                || !result.Output.Contains(
                    "TEST_FAIL qemu.smoke forbidden_marker",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "FORBIDDEN_MARKER_HIT TEST_FAIL",
                    StringComparison.Ordinal)
                || !summary.Contains(
                    "TEST_FAIL m01.stage3 " + detail,
                    StringComparison.Ordinal)
                || result.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "QEMU_M01_STAGE3_FAILURE_CLASSIFICATION_DIAGNOSTIC"
                    + " adapter=" + adapterStatus
                    + " exit=" + result.ExitCode
                    + " output=" + EscapeDiagnostic(result.Output)
                    + " summary=" + EscapeDiagnostic(summary));
                return false;
            }
        }

        return true;
    }

    private static string EscapeDiagnostic(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static bool TestM01StageThreeTimeoutIsDeterministic(
        string tempRoot)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            CreateM01StageThreeFailurePrefixLines());

        ProcessResult result = RunM01StageThreeQemu(
            tempRoot,
            "stage-three-timeout",
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

    private static bool TestM01StageThreeResetIsRejected(string tempRoot)
    {
        string qemuPath = CreateFakeQemuScript(
            tempRoot,
            CreateM01StageThreeSerialLines().ToArray());

        ProcessResult result = RunM01StageThreeQemu(
            tempRoot,
            "stage-three-reset",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 18
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

    private static bool TestM01StageThreeConflictingTerminalIsRejected(
        string tempRoot)
    {
        List<string> serialLines = CreateM01StageThreeSerialLines();
        serialLines.Add("OS_SMOKE_FAIL");
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageThreeQemu(
            tempRoot,
            "stage-three-conflicting-terminal",
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode != 0
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke",
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

    private static bool RunM01StageThreeOutOfOrderCase(
        string tempRoot,
        string scenario,
        IReadOnlyList<string> serialLines,
        string expectedMarker)
    {
        string qemuPath = CreatePersistentFakeQemuScript(
            tempRoot,
            serialLines);

        ProcessResult result = RunM01StageThreeQemu(
            tempRoot,
            scenario,
            qemuPath,
            includeOrdered: true,
            includeUnique: true,
            timeoutSeconds: 5,
            out string summary);

        return result.ExitCode == 10
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke out_of_order_marker",
                StringComparison.Ordinal)
            && summary.Contains(
                "OUT_OF_ORDER_MARKERS " + expectedMarker,
                StringComparison.Ordinal)
            && summary.Contains(
                "FAIL_REASON out_of_order_marker",
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
            "5");

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
            "5");

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

    // Frozen OVMF preamble fixture; no production parser or source extraction supplies test data.
    private static readonly string UnexpectedPanicFirmwarePrefix = System.Text.Encoding.ASCII.GetString(
        Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg=="));

    private static bool TestUnexpectedPanicOptions(string tempRoot)
    {
        string image = CreateEmptyFile(tempRoot, "panic-options.raw");
        string sentinel = Path.Combine(tempRoot, "panic-options-launched.txt");
        string fake = CreateLaunchSentinelFakeQemuScript(tempRoot, sentinel);
        string summary = Path.Combine(tempRoot, "panic-options-summary.txt");
        string[][] invalid =
        [
            ["--expect-unexpected-vector-panic"],
            ["--expect-unexpected-vector-panic", "--summary-log", summary, "--expect-unexpected-vector-panic"],
            ["--expect-unexpected-vector-panic", "--summary-log", summary, "--marker-preset", "acpi"],
            ["--expect-unexpected-vector-panic", "--summary-log", summary, "--http-probe-url", "http://127.0.0.1:1/"]
        ];
        foreach (string[] tail in invalid)
        {
            ProcessResult result = RunQemuRunner(["--qemu", fake, "--image", image, .. tail]);
            PanicCheck(result.ExitCode != 0 && result.ExitCode != 19 && !File.Exists(sentinel),
                "invalid panic options must reject before launching", result.Output);
        }
        return true;
    }

    private static bool TestUnexpectedPanicAcceptedCapture(string tempRoot)
    {
        string transcript = UnexpectedPanicTranscript();
        foreach (string prefix in new[] { string.Empty, UnexpectedPanicFirmwarePrefix })
        {
            var capture = RunUnexpectedPanicCase("accepted", prefix + transcript);
            string[] output = capture.Result.Output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            PanicCheck(capture.Result.ExitCode == 19 && output.Length == 18 &&
                output.Count(x => x == "QEMU_EXPECTED_PANIC outcome=expected-panic reason=expected_unexpected_vector_panic") == 1 &&
                !output.Any(x => x.StartsWith("TEST_PASS qemu.smoke", StringComparison.Ordinal) || x.StartsWith("TEST_FAIL", StringComparison.Ordinal)),
                "explicit panic is finite native 19, never smoke success", capture.Result.Output);
            string native = output.Single(x => x.StartsWith("QEMU_PROCESS_RESULT ", StringComparison.Ordinal));
            string nativePrefix = "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=";
            PanicCheck(native.StartsWith(nativePrefix, StringComparison.Ordinal) &&
                int.TryParse(native[nativePrefix.Length..], System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out int nativeCode) &&
                native == nativePrefix + nativeCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "actual canonical signed native exit and owned termination", native);
            string drain = output.Single(x => x.StartsWith("SERIAL_MARKER_DRAIN ", StringComparison.Ordinal));
            const string drainPrefix = "SERIAL_MARKER_DRAIN milliseconds=750 observedMilliseconds=";
            PanicCheck(drain.StartsWith(drainPrefix, StringComparison.Ordinal) &&
                long.TryParse(drain[drainPrefix.Length..], out long elapsed) && elapsed >= 750,
                "actual quiet interval must be at least 750ms", drain);
            byte[] raw = File.ReadAllBytes(capture.Serial);
            string[] summary = File.ReadAllLines(capture.Summary);
            string hashLine = "EXPECTED_PANIC_CAPTURE bytes=" + raw.Length + " sha256=" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw));
            PanicCheck(raw.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(prefix + transcript)) &&
                summary.Count(x => x == hashLine) == 1 &&
                summary.Count(x => x == "QEMU_SMOKE_STATUS expected-panic") == 1 &&
                summary.Count(x => x == "FAIL_REASON expected_unexpected_vector_panic") == 1 &&
                summary.Contains(native) &&
                summary.Contains(drain.Replace("SERIAL_MARKER_DRAIN", "EXPECTED_PANIC_DRAIN", StringComparison.Ordinal)) &&
                summary.Contains("FORBIDDEN_MARKERS OS_SMOKE_FAIL;TEST_FAIL") &&
                output.Any(x => x.StartsWith("SERIAL_MARKER_STREAM status=ExpectedPanic terminal=ExpectedPanic bytes=" + raw.Length + " lines=" + OS.Tooling.QemuContracts.M02UnexpectedCaptureRecordCount + " invalidBytes=0 invalidLines=0 ", StringComparison.Ordinal)),
                "summary must bind raw bytes, inventories, lifecycle and finite outcome", string.Join('\n', summary));
        }
        return true;
    }

    private static bool TestUnexpectedPanicMalformedCaptures(string tempRoot)
    {
        string valid = UnexpectedPanicTranscript();
        List<(string Name, string Text)> rejected =
        [
            ("metadata-context", valid.Replace("context=0x0000000000201000", "context=0x0000000000201008", StringComparison.Ordinal)),
            ("metadata-width", valid.Replace("callback=0x0000000000400000", "callback=0x400000", StringComparison.Ordinal)),
            ("metadata-case", valid.Replace("tssAccess=0x000000000000008B", "tssAccess=0x000000000000008b", StringComparison.Ordinal)),
            ("metadata-order", valid.Replace("cs=0x0000000000000008 ss=0x0000000000000010", "ss=0x0000000000000010 cs=0x0000000000000008", StringComparison.Ordinal)),
            ("frame-rsp", valid.Replace("rsp=0x0000000000207000", "rsp=0x0000000000206000", StringComparison.Ordinal)),
            ("frame-rip", valid.Replace("rip=0x00000000001013C2", "rip=0x00000000001013C0", StringComparison.Ordinal)),
            ("vector", valid.Replace("vector=32", "vector=33", StringComparison.Ordinal)),
            ("map-accounting", valid.Replace("conventionalBytes=40960", "conventionalBytes=40961", StringComparison.Ordinal)),
            ("foreign-prefix", "unexpected firmware output\r\n" + valid),
            ("changed-firmware-prefix", UnexpectedPanicFirmwarePrefix.Replace("Boot0001", "Boot0002", StringComparison.Ordinal) + valid),
            ("missing-prerequisite", valid.Replace("M01_PAGE_ZERO_ABSENT\r\n", string.Empty, StringComparison.Ordinal)),
            ("extra-record", valid + "unexpected\r\n"),
            ("extra-empty-record", valid + "\r\n"),
            ("missing-crlf", valid[..^2]),
            ("lf-only", valid.Replace("\r\n", "\n", StringComparison.Ordinal)),
            ("embedded-control", valid.Replace("M02_UNEXPECTED_STATE", "M02_UNEXPECTED_\tSTATE", StringComparison.Ordinal)),
            ("early-failure", valid.Replace("EFI_CONSOLE_READY", "TEST_FAIL preparation", StringComparison.Ordinal)),
            ("wrong-fault-family", valid.Replace("reason=unexpected-vector", "reason=page-fault", StringComparison.Ordinal))
        ];
        // The final CMP EAX,0x600 fixes arithmetic flags before INT 32; these are not diagnostic freedom.
        foreach (ulong bit in new ulong[] { 1, 4, 16, 64, 128, 2048 })
            rejected.Add(("arithmetic-flag-" + bit, valid.Replace("rflags=0x0000000000000046",
                "rflags=0x" + (0x46UL ^ bit).ToString("X16"), StringComparison.Ordinal)));
        foreach (var row in rejected)
        {
            PanicCheck(row.Text != valid, "mutation must change fixture: " + row.Name, string.Empty);
            var capture = RunUnexpectedPanicCase(row.Name, row.Text, timeout: 2);
            AssertUnexpectedPanicRejected(capture, row.Name);
        }
        return true;
    }

    private static bool TestUnexpectedPanicPrefixMarkerIsolation(string tempRoot)
    {
        string[][] markerOptions =
        [
            ["--require-marker", "NEVER_EMITTED"],
            ["--forbid-marker", "M01_PAGE_ZERO_ABSENT"],
            ["--require-ordered-marker", "OS_BOOT_OK", "--require-ordered-marker", "EFI_CONSOLE_READY"],
            ["--require-unique-marker", "TEST_PASS"]
        ];
        foreach (string[] options in markerOptions)
        {
            var capture = RunUnexpectedPanicCase("marker-isolation", UnexpectedPanicTranscript(), extraOptions: options);
            AssertUnexpectedPanicRejected(capture, "generic marker rules still apply to prerequisites");
            PanicCheck(File.ReadAllLines(capture.Summary).Contains("FAIL_REASON unexpected_vector_panic_prefix_markers"),
                "exact prefix marker rejection", capture.Result.Output);
        }
        return true;
    }

    private static bool TestUnexpectedPanicDefaultIsolation(string tempRoot)
    {
        var generic = RunUnexpectedPanicCase("generic-panic", UnexpectedPanicTranscript(), expectedMode: false);
        AssertUnexpectedPanicRejected(generic, "generic mode does not forgive an expected panic");
        var ordinary = RunUnexpectedPanicCase("generic-success", "OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n", expectedMode: false);
        PanicCheck(ordinary.Result.ExitCode == 0 && File.ReadAllLines(ordinary.Summary).Contains("QEMU_SMOKE_STATUS pass"),
            "ordinary success remains unchanged", ordinary.Result.Output);
        var explicitOrdinary = RunUnexpectedPanicCase("explicit-ordinary", "OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n");
        AssertUnexpectedPanicRejected(explicitOrdinary, "ordinary success is not panic evidence");
        return true;
    }

    private static bool TestUnexpectedPanicLifecycle(string tempRoot)
    {
        foreach (string behavior in new[] { "append", "rewrite", "truncate", "exit0", "exit7", "timeout" })
        {
            var capture = RunUnexpectedPanicCase(behavior, UnexpectedPanicTranscript(), behavior, timeout: 2);
            AssertUnexpectedPanicRejected(capture, behavior);
            string[] summary = File.ReadAllLines(capture.Summary);
            if (behavior.StartsWith("exit", StringComparison.Ordinal))
                PanicCheck(capture.Result.ExitCode == 18 && summary.Contains("FAIL_REASON qemu_reboot_or_shutdown") &&
                    summary.Contains("QEMU_PROCESS_RESULT disposition=exited exitCode=" + behavior[4..]),
                    "spontaneous actual exit is never owned termination", capture.Result.Output);
            else if (behavior == "timeout")
                PanicCheck(capture.Result.ExitCode == 4 && summary.Contains("FAIL_REASON timeout"),
                    "hard timeout remains a failure", capture.Result.Output);
            else
                PanicCheck(summary.Any(x => x == "FAIL_REASON bytes_after_expected_panic" || x == "FAIL_REASON serial_log_reset" ||
                    x == "FAIL_REASON unexpected_vector_panic_capture_rejected"),
                    "append/reset/truncation must reject whether observed before or after candidate classification", capture.Result.Output);
        }
        return true;
    }

    private static bool TestUnexpectedPanicOwnedCleanup(string tempRoot)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string value in new[] { typeof(QemuTestControl).Assembly.Location, "--qemu-control", "sleep", "60000" }) start.ArgumentList.Add(value);
        using Process unrelated = Process.Start(start) ?? throw new InvalidOperationException("unrelated child start");
        try
        {
            var capture = RunUnexpectedPanicCase("owned-cleanup", UnexpectedPanicTranscript());
            PanicCheck(capture.Result.ExitCode == 19 && !unrelated.HasExited, "cleanup must preserve unrelated processes", capture.Result.Output);
            var failedSummary = RunUnexpectedPanicCase("summary-failure", UnexpectedPanicTranscript(), summaryDirectory: true);
            PanicCheck(failedSummary.Result.ExitCode == 16 && failedSummary.Result.Output.Contains("TEST_FAIL qemu.smoke expected_panic_summary_failed", StringComparison.Ordinal)
                && !unrelated.HasExited, "summary failure is visible after owned cleanup", failedSummary.Result.Output);
        }
        finally
        {
            if (!unrelated.HasExited) unrelated.Kill(entireProcessTree: true);
            PanicCheck(unrelated.WaitForExit(5000), "controlled unrelated process cleanup", string.Empty);
        }
        return true;
    }

    private static void AssertUnexpectedPanicRejected((ProcessResult Result, string Serial, string Summary) capture, string reason)
    {
        PanicCheck(capture.Result.ExitCode != 0 && capture.Result.ExitCode != 19 &&
            !capture.Result.Output.Contains("QEMU_EXPECTED_PANIC outcome=expected-panic", StringComparison.Ordinal) &&
            File.ReadAllLines(capture.Summary).Contains("QEMU_SMOKE_STATUS fail"), reason, capture.Result.Output);
    }

    private static void PanicCheck(bool condition, string reason, string output)
    {
        if (!condition) throw new InvalidOperationException(reason + "\n" + output);
    }

    private static (ProcessResult Result, string Serial, string Summary) RunUnexpectedPanicCase(
        string name, string transcript, string behavior = "hold", bool expectedMode = true,
        int timeout = 5, string[]? extraOptions = null, bool summaryDirectory = false)
    {
        // Retain exact child inputs and results for diagnosis; these are controlled processes, not QEMU.
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "issue526-tests", "runner", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string payload = Path.Combine(directory, "payload.bin");
        File.WriteAllBytes(payload, System.Text.Encoding.UTF8.GetBytes(transcript));
        string pidPath = Path.Combine(directory, "writer-pid.txt");
        string fake = Path.Combine(directory, "fake-qemu.cmd");
        List<string> script = CreateFakeQemuScriptPreamble();
        script.Add(QemuTestControl.Command("panic-writer", "%serial%", payload, pidPath, behavior));
        script.Add("exit /b %ERRORLEVEL%");
        File.WriteAllLines(fake, script);
        string serial = Path.Combine(directory, "serial.log"), summary = Path.Combine(directory, "summary.log");
        if (summaryDirectory) Directory.CreateDirectory(summary);
        List<string> args = ["--qemu", fake, "--ovmf-code", CreateEmptyFile(directory, "OVMF_CODE.fd"),
            "--image", CreateEmptyFile(directory, "image.raw"), "--serial-log", serial, "--summary-log", summary,
            "--timeout", timeout.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        if (expectedMode) args.Add("--expect-unexpected-vector-panic");
        if (extraOptions is not null) args.AddRange(extraOptions);
        File.WriteAllText(Path.Combine(directory, "arguments.json"), JsonSerializer.Serialize(args));
        ProcessResult result = RunQemuRunner(args.ToArray());
        File.WriteAllText(Path.Combine(directory, "runner-output.log"), result.Output);
        File.WriteAllText(Path.Combine(directory, "runner-exit.txt"), result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PanicCheck(File.Exists(pidPath), "controlled writer must actually launch", result.Output);
        int pid = int.Parse(File.ReadAllText(pidPath), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            using Process child = Process.GetProcessById(pid);
            PanicCheck(child.WaitForExit(2000), "runner must reap its controlled writer child", result.Output);
        }
        catch (ArgumentException) { /* The owned writer already exited. */ }
        return (result, serial, summary);
    }

    private static string UnexpectedPanicTranscript()
    {
        const ulong c=0x100000, d=0x200000, target=c+512;
        string[] names=["c","d","context","callback","captureRsp","firmwareCs","cs","ss","ds","es","tr","gdtrBase","gdtrLimit","idtrBase","idtrLimit","tssAccess","gateLow","gateHigh","normalizer0","normalizer1","trigger","cr3","entryCr3","lar"];
        ulong[] values=[c,d,d+4096,0x400000,d+28488,0x38,8,16,16,16,24,d+4120,39,d,4095,0x8b,
            (target&65535)|(8UL<<16)|(0x8eUL<<40)|((target&0xffff0000)<<32),target>>32,
            0x00002068006a006a,0x909000000df2e900,c+5056,0x300000,0x300000,0xa09b00];
        string metadata="M02_UNEXPECTED_STATE "+string.Join(' ',names.Select((name,i)=>name+"=0x"+values[i].ToString("X16")))+"\r\n";
        PanicCheck(System.Text.Encoding.ASCII.GetByteCount(metadata) == 650 && names.Length == 24, "independent 650-byte metadata record", metadata);
        string[] registers=["rip","rsp","rbp","rflags","cr2","cr3","rax","rbx","rcx","rdx","rsi","rdi","r8","r9","r10","r11","r12","r13","r14","r15"];
        ulong[] captured=[c+5058,d+28672,0x10203040,0x46,0,0x300000,24,0x1234,0xa09b00,d+4096,0x5678,0x9abc,0x300000,0x3456,0x789a,0xbcde,0x1357,0x2468,0x369c,0x48ad];
        string fault="OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000 "+string.Join(' ',registers.Select((name,i)=>name+"=0x"+captured[i].ToString("X16")))+"\r\n";
        return QemuTerminalControls.Prefix()+metadata+fault;
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
        return CreateM01SerialLines(M01StageOneGuestMarkers);
    }

    private static List<string> CreateM01StageTwoSerialLines()
    {
        return CreateM01SerialLines(M01StageTwoGuestMarkers);
    }

    private static List<string> CreateM01StageThreeSerialLines()
    {
        return CreateM01SerialLines(M01StageThreeGuestMarkers);
    }

    private static List<string> CreateM01SerialLines(
        IReadOnlyList<string> guestMarkers)
    {
        List<string> serialLines = [];
        foreach (string marker in guestMarkers)
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

    private static List<string> CreateM01StageTwoFailurePrefixLines()
    {
        List<string> serialLines = [];
        foreach (string marker in M01StageTwoGuestMarkers)
        {
            serialLines.Add(marker);
            if (marker == "TEST_START m01.stage2")
            {
                break;
            }
        }

        return serialLines;
    }

    private static List<string> CreateM01StageThreeFailurePrefixLines()
    {
        List<string> serialLines = [];
        foreach (string marker in M01StageThreeGuestMarkers)
        {
            serialLines.Add(marker);
            if (marker == "TEST_START m01.stage3")
            {
                break;
            }
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

    private static ProcessResult RunM01StageTwoQemu(
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
            "m01-stage-two-" + suffix + ".raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "m01-stage-two-" + suffix + "-OVMF_CODE.fd");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-stage-two-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-stage-two-" + suffix + "-summary.log");
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

        foreach (string marker in M01StageTwoGuestMarkers)
        {
            arguments.Add("--require-marker");
            arguments.Add(marker);
        }

        if (includeOrdered)
        {
            foreach (string marker in M01StageTwoGuestMarkers)
            {
                arguments.Add("--require-ordered-marker");
                arguments.Add(marker);
            }
        }

        if (includeUnique)
        {
            foreach (string marker in M01StageTwoGuestMarkers)
            {
                arguments.Add("--require-unique-marker");
                arguments.Add(marker);
            }
        }

        foreach (string marker in M01StageTwoForbiddenMarkers)
        {
            arguments.Add("--forbid-marker");
            arguments.Add(marker);
        }

        ProcessResult result = RunQemuRunner(arguments.ToArray());
        summary = File.Exists(summaryLog)
            ? File.ReadAllText(summaryLog)
            : string.Empty;
        return result;
    }

    private static ProcessResult RunM01StageThreeQemu(
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
            "m01-stage-three-" + suffix + ".raw");
        string ovmfPath = CreateEmptyFile(
            tempRoot,
            "m01-stage-three-" + suffix + "-OVMF_CODE.fd");
        string serialLog = Path.Combine(
            tempRoot,
            "m01-stage-three-" + suffix + "-serial.log");
        string summaryLog = Path.Combine(
            tempRoot,
            "m01-stage-three-" + suffix + "-summary.log");
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

        foreach (string marker in M01StageThreeGuestMarkers)
        {
            arguments.Add("--require-marker");
            arguments.Add(marker);
        }

        if (includeOrdered)
        {
            foreach (string marker in M01StageThreeGuestMarkers)
            {
                arguments.Add("--require-ordered-marker");
                arguments.Add(marker);
            }
        }

        if (includeUnique)
        {
            foreach (string marker in M01StageThreeGuestMarkers)
            {
                arguments.Add("--require-unique-marker");
                arguments.Add(marker);
            }
        }

        foreach (string marker in M01StageThreeForbiddenMarkers)
        {
            arguments.Add("--forbid-marker");
            arguments.Add(marker);
        }

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
            QemuTestControl.Command("sleep", "30000"));
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
                    QemuTestControl.Command("sleep", "250"));
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
            QemuTestControl.Command("sleep", "30000"));
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
        string path = Path.Combine(tempRoot, "fake-delay-" + Guid.NewGuid().ToString("N") + ".cmd");
        var script = CreateFakeQemuScriptPreamble();
        script.Add(QemuTestControl.Command("delayed-terminal", "%serial%", delayedTerminal));
        script.Add("exit /b %ERRORLEVEL%");
        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateDelayedExitFakeQemuScript(
        string tempRoot,
        int exitCode,
        int delayMilliseconds)
    {
        string path = Path.Combine(tempRoot, "fake-delay-" + Guid.NewGuid().ToString("N") + ".cmd");
        var script = CreateFakeQemuScriptPreamble();
        script.Add(QemuTestControl.Command("delayed-exit", "%serial%", delayMilliseconds.ToString(), exitCode.ToString()));
        script.Add("exit /b %ERRORLEVEL%");
        File.WriteAllLines(path, script);
        return path;
    }

    private static string CreateLateMarkerFakeQemuScript(
        string tempRoot,
        int delayMilliseconds)
    {
        string path = Path.Combine(tempRoot, "fake-delay-" + Guid.NewGuid().ToString("N") + ".cmd");
        var script = CreateFakeQemuScriptPreamble();
        script.Add(QemuTestControl.Command("sleep", delayMilliseconds.ToString()));
        AddSerialLines(script, ["OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"]);
        script.Add("exit /b %ERRORLEVEL%");
        File.WriteAllLines(path, script);
        return path;
    }

    private static M01RawObservationFixture
        CreateM01RawObservationVerifierFixture(
            string tempRoot,
            string scenario,
            M01RawObservationCorruption corruption)
    {
        const ulong Cr0 = 0x0000000080010001UL;
        const ulong FirmwareCr3 = 0x0000000000200000UL;
        const ulong RawCr4 = 0x0000000000000020UL;
        const ulong Ia32Efer = 0x0000000000000d00UL;
        const uint CpuidMaximumExtendedLeaf = 0x80000008U;
        const uint CpuidAddressSizesEax = 0x00003030U;
        const ulong Rflags = 0x0000000000000202UL;
        const ulong PhysicalAddressMask = 0x0000ffffffffffffUL;
        const ulong OwnedCr3 = 0x0000000000300000UL;
        const ulong PreflightPhysicalAddress = 0x0000000000100000UL;
        const ulong DefaultRuntimePhysicalAddress = 0x0000000000400000UL;
        const ulong ReplacementCanary = 0x1122334455667788UL;
        const ulong PublicationCookie = 0x8877665544332211UL;
        const ulong PublicationEpoch = 0x0000000000000101UL;
        const ulong MaterializationCookie = 0xa55aa55aa55aa55aUL;
        const ulong RuntimeCodeBase = 0x0000000000500000UL;
        const ulong RuntimeCodeEnd = RuntimeCodeBase + 0x1000UL;
        const ulong RuntimeExpectedReturnAddress =
            RuntimeCodeBase + 0x0fc9UL;

        string directory = Path.Combine(
            tempRoot,
            "m01-raw-verifier-"
                + SanitizeFileName(scenario)
                + "-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string observationPath = Path.Combine(directory, "observation.log");
        string privateSerialPath = Path.Combine(
            directory,
            "private-serial.log");
        string publicSerialPath = Path.Combine(
            directory,
            "public-serial.log");
        string qmpTranscriptPath = Path.Combine(
            directory,
            "qmp-transcript.log");
        string captureScratchPath = Path.Combine(
            directory,
            "capture-scratch");

        ulong runtimePhysicalAddress = corruption switch
        {
            M01RawObservationCorruption.RuntimeAddressMisaligned =>
                DefaultRuntimePhysicalAddress + 8,
            M01RawObservationCorruption.RuntimeAddressOutOfRange =>
                256UL * 1024UL * 1024UL,
            _ => DefaultRuntimePhysicalAddress
        };
        ulong transitionCanaryPhysicalAddress =
            runtimePhysicalAddress + 480;
        ulong callGatePhysicalAddress = runtimePhysicalAddress + 0x300;
        ulong reportedCallGatePhysicalAddress =
            corruption == M01RawObservationCorruption.CallGateAddressMismatch
                ? runtimePhysicalAddress + 0x400
                : callGatePhysicalAddress;

        byte[] preflight = new byte[56];
        WriteM01UInt64(preflight, 0, Cr0);
        WriteM01UInt64(preflight, 8, FirmwareCr3);
        WriteM01UInt64(preflight, 16, RawCr4);
        WriteM01UInt64(preflight, 24, Ia32Efer);
        WriteM01UInt32(
            preflight,
            32,
            CpuidMaximumExtendedLeaf);
        WriteM01UInt32(preflight, 36, CpuidAddressSizesEax);
        WriteM01UInt64(preflight, 40, Rflags);
        WriteM01UInt32(preflight, 48, 0x31464c50U);
        WriteM01UInt32(preflight, 52, 0U);

        byte[] runtimeState = new byte[480];
        WriteM01UInt32(runtimeState, 0, 480U);
        WriteM01UInt16(runtimeState, 4, 1);
        runtimeState[6] = 1;
        runtimeState[7] = 0;
        WriteM01UInt64(runtimeState, 8, OwnedCr3);
        WriteM01UInt64(runtimeState, 48, RuntimeCodeBase);
        WriteM01UInt64(runtimeState, 56, RuntimeCodeEnd);
        WriteM01UInt64(
            runtimeState,
            80,
            RuntimeExpectedReturnAddress);
        WriteM01UInt64(runtimeState, 160, MaterializationCookie);
        WriteM01UInt64(runtimeState, 168, ReplacementCanary);
        WriteM01UInt32(runtimeState, 176, 0x4f534f4bU);
        WriteM01UInt32(runtimeState, 180, 0U);
        WriteM01UInt64(runtimeState, 184, runtimePhysicalAddress);
        WriteM01UInt64(runtimeState, 200, Rflags);
        WriteM01UInt64(
            runtimeState,
            208,
            corruption
                == M01RawObservationCorruption.SemanticRuntimeCr0Mismatch
                    ? Cr0 ^ 0x4UL
                    : Cr0);
        WriteM01UInt64(runtimeState, 216, FirmwareCr3);
        WriteM01UInt64(runtimeState, 224, RawCr4);
        WriteM01UInt64(runtimeState, 232, Ia32Efer);
        WriteM01UInt64(runtimeState, 240, PhysicalAddressMask);
        WriteM01UInt64(runtimeState, 248, PublicationEpoch);
        WriteM01UInt64(runtimeState, 272, Rflags & ~(1UL << 9));
        WriteM01UInt64(runtimeState, 280, RawCr4 & ~(1UL << 7));
        WriteM01UInt64(runtimeState, 288, OwnedCr3);
        WriteM01UInt64(runtimeState, 296, MaterializationCookie);
        WriteM01UInt64(runtimeState, 304, ReplacementCanary);
        WriteM01UInt64(runtimeState, 360, 0x000000004f534f4bUL);
        WriteM01UInt64(runtimeState, 408, FirmwareCr3);
        WriteM01UInt64(runtimeState, 416, ReplacementCanary);
        WriteM01UInt64(runtimeState, 424, RawCr4);
        WriteM01UInt64(runtimeState, 432, Rflags);
        WriteM01UInt64(runtimeState, 440, PublicationEpoch);
        WriteM01UInt64(runtimeState, 448, 1);
        WriteM01UInt64(runtimeState, 456, 1);
        runtimeState[464] = 1;
        runtimeState[465] = 17;
        runtimeState[466] = 4;
        runtimeState[467] = 1;
        runtimeState[468] = 0;
        runtimeState[469] = 1;
        runtimeState[470] = 1;
        runtimeState[471] = 12;
        WriteM01UInt64(runtimeState, 472, 0);

        byte[] callGate = new byte[160];
        WriteM01UInt32(callGate, 0, 160U);
        WriteM01UInt16(callGate, 4, 1);
        callGate[6] = 1;
        callGate[7] = 3;
        WriteM01UInt64(callGate, 8, callGatePhysicalAddress);
        WriteM01UInt64(callGate, 16, RuntimeCodeBase);
        WriteM01UInt64(callGate, 24, RuntimeCodeBase);
        WriteM01UInt64(callGate, 32, RuntimeCodeBase + 0x0f00UL);
        WriteM01UInt64(
            callGate,
            40,
            RuntimeExpectedReturnAddress);
        WriteM01UInt64(callGate, 48, Rflags);
        WriteM01UInt64(callGate, 56, FirmwareCr3);
        WriteM01UInt64(callGate, 64, RawCr4);
        WriteM01UInt64(callGate, 72, PublicationCookie);
        WriteM01UInt32(callGate, 80, 1U);
        callGate[84] = 1;
        callGate[85] = 0;
        callGate[86] = 1;
        callGate[87] = 1;
        WriteM01UInt64(callGate, 88, PublicationCookie);
        WriteM01UInt64(callGate, 96, PublicationCookie);
        WriteM01UInt64(callGate, 104, Rflags);
        WriteM01UInt64(callGate, 112, FirmwareCr3);
        WriteM01UInt64(callGate, 120, RawCr4);
        WriteM01UInt64(callGate, 128, PublicationCookie);
        WriteM01UInt32(callGate, 136, 1U);
        callGate[140] = 1;
        callGate[141] = 1;
        WriteM01UInt16(callGate, 142, 0);
        WriteM01UInt32(callGate, 144, 0x4b47534fU);
        WriteM01UInt32(callGate, 148, 0U);
        WriteM01UInt64(callGate, 152, 0);

        byte[] privateSerial =
            System.Text.Encoding.UTF8.GetBytes(
                "M01 raw observation serial\r\n");
        byte[] publicSerial =
            corruption == M01RawObservationCorruption.SerialHashMismatch
                ? System.Text.Encoding.UTF8.GetBytes(
                    "M01 different public serial\r\n")
                : privateSerial.ToArray();
        File.WriteAllBytes(privateSerialPath, privateSerial);
        File.WriteAllBytes(publicSerialPath, publicSerial);

        string privateSerialHash = M01Sha256(privateSerial);
        string publicSerialHash = M01Sha256(publicSerial);
        string preflightHash = M01Sha256(preflight);
        string runtimeHash = M01Sha256(runtimeState);
        string callGateHash = M01Sha256(callGate);
        if (corruption == M01RawObservationCorruption.PodHashMismatch)
        {
            runtimeHash = runtimeHash[0] == '0'
                ? "1" + runtimeHash[1..]
                : "0" + runtimeHash[1..];
        }

        Dictionary<string, string> values =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema"] = "clros.m01.raw-observations.v1",
                ["captureMethod"] =
                    "qmp-pmemsave-stopped-before-terminal-release",
                ["guestMemoryBytes"] = "268435456",
                ["captureChunkBytes"] = "16777216",
                ["guestTimeoutSeconds"] = "15",
                ["captureTimeoutSeconds"] = "60",
                ["terminalWithheld"] = "true",
                ["qmpPaused"] = "true",
                ["qmpMemoryShapeValidated"] = "true",
                ["memoryDumpRetained"] = "false",
                ["memorySha256"] = M01Sha256(
                    System.Text.Encoding.UTF8.GetBytes(
                        "memory-" + scenario)),
                ["preflightCandidateCount"] = "1",
                ["preflightPhysicalAddress"] =
                    FormatM01TestHex64(PreflightPhysicalAddress),
                ["preflightPhysicalAddresses"] =
                    FormatM01TestHex64(PreflightPhysicalAddress),
                ["preflightPodSha256"] = preflightHash,
                ["preflightPodBase64"] =
                    Convert.ToBase64String(preflight),
                ["cr0Before"] = FormatM01TestHex64(Cr0),
                ["firmwareCr3Before"] =
                    FormatM01TestHex64(FirmwareCr3),
                ["rawCr4Before"] = FormatM01TestHex64(RawCr4),
                ["ia32EferBefore"] = FormatM01TestHex64(Ia32Efer),
                ["cpuidMaximumExtendedLeaf"] =
                    FormatM01TestHex32(CpuidMaximumExtendedLeaf),
                ["cpuidAddressSizesEax"] =
                    FormatM01TestHex32(CpuidAddressSizesEax),
                ["physicalAddressWidth"] = "48",
                ["physicalAddressMask"] =
                    FormatM01TestHex64(PhysicalAddressMask),
                ["ownedCr3"] = FormatM01TestHex64(OwnedCr3),
                ["rflagsBefore"] = FormatM01TestHex64(Rflags),
                ["runtimeStatePhysicalAddress"] =
                    FormatM01TestHex64(runtimePhysicalAddress),
                ["runtimeStatePodSha256"] = runtimeHash,
                ["runtimeStatePodBase64"] =
                    Convert.ToBase64String(runtimeState),
                ["transitionCanaryPhysicalAddress"] =
                    FormatM01TestHex64(
                        transitionCanaryPhysicalAddress),
                ["transitionCanaryValue"] =
                    FormatM01TestHex64(ReplacementCanary),
                ["callGatePhysicalAddress"] =
                    FormatM01TestHex64(
                        reportedCallGatePhysicalAddress),
                ["callGatePodSha256"] = callGateHash,
                ["callGatePodBase64"] =
                    Convert.ToBase64String(callGate),
                ["firmwareCr3After"] =
                    FormatM01TestHex64(FirmwareCr3),
                ["rawCr4After"] = FormatM01TestHex64(RawCr4),
                ["rflagsAfter"] = FormatM01TestHex64(Rflags),
                ["runtimeFirmwareCr3Readback"] =
                    FormatM01TestHex64(FirmwareCr3),
                ["runtimeRestoredCr4Readback"] =
                    FormatM01TestHex64(RawCr4),
                ["runtimeRestoredRflagsIntent"] =
                    FormatM01TestHex64(Rflags),
                ["privateSerialSha256"] = privateSerialHash,
                ["predictedPublicSerialSha256"] = publicSerialHash,
                ["exactEqualityValidation"] = "pass",
                ["terminalReleaseAuthorized"] = "true"
            };
        File.WriteAllLines(
            observationPath,
            values.Select(pair => pair.Key + "=" + pair.Value),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllLines(
            qmpTranscriptPath,
            [
                "qmpMemoryShapeValidated=true",
                "qmpPaused=true",
                "M01_RAW_OBSERVATION_CAPTURED",
                "publicSerialSha256=" + publicSerialHash,
                "privateSerialSha256=" + privateSerialHash,
                "terminalReleaseAuthorized=true",
                "terminalReleased=true"
            ],
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new M01RawObservationFixture(
            observationPath,
            privateSerialPath,
            publicSerialPath,
            qmpTranscriptPath,
            captureScratchPath);
    }

    private static ProcessResult RunM01RawObservationVerifier(
        M01RawObservationFixture fixture,
        string? expectedError)
    {
        try
        {
            OS.Tooling.QemuCaptureSession.ValidateFiles(fixture.ObservationPath, fixture.PrivateSerialPath, fixture.PublicSerialPath, fixture.QmpTranscriptPath, fixture.CaptureScratchPath);
            return new ProcessResult(expectedError is null ? 0 : 1, "VERIFIER_PASS serialSha256=" + M01Sha256(File.ReadAllBytes(fixture.PublicSerialPath)) + " candidates=1");
        }
        catch (Exception error) { return new ProcessResult(error.Message == expectedError ? 0 : 1, "EXPECTED_FAILURE " + error.Message); }
    }



    private static void WriteM01UInt16(
        byte[] bytes,
        int offset,
        ushort value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(offset, sizeof(ushort)),
            value);
    }

    private static void WriteM01UInt32(
        byte[] bytes,
        int offset,
        uint value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(offset, sizeof(uint)),
            value);
    }

    private static void WriteM01UInt64(
        byte[] bytes,
        int offset,
        ulong value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            bytes.AsSpan(offset, sizeof(ulong)),
            value);
    }

    private static string M01Sha256(byte[] bytes)
    {
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static string FormatM01TestHex64(ulong value)
    {
        return "0x" + value.ToString("X16");
    }

    private static string FormatM01TestHex32(uint value)
    {
        return "0x" + value.ToString("X8");
    }



    private static string[] ExtractM01FatalQmpEvents(string source)
    {
        const string eventStart = "if (root.TryGetProperty(";
        int start = source.IndexOf(eventStart, StringComparison.Ordinal);
        while (start >= 0)
        {
            int probeEnd = Math.Min(source.Length, start + 180);
            if (source[start..probeEnd].Contains(
                    "\"event\"",
                    StringComparison.Ordinal))
            {
                break;
            }
            start = source.IndexOf(
                eventStart,
                start + eventStart.Length,
                StringComparison.Ordinal);
        }
        if (start < 0)
        {
            throw new InvalidDataException(
                "M01 raw-observation QMP event handler is missing.");
        }

        int end = source.IndexOf(
            "continue;",
            start,
            StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException(
                "M01 raw-observation QMP event handler is malformed.");
        }
        string handler = source[start..end];
        return System.Text.RegularExpressions.Regex.Matches(
                handler,
                "\"([A-Z][A-Z_]+)\"")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static string[] GetM01RawObservationEnvironmentNames()
    {
        return
        [
            "CLROS_M01_REAL_QEMU",
            "CLROS_M01_PRIVATE_SERIAL",
            "CLROS_M01_RAW_OBSERVATION_LOG",
            "CLROS_M01_QMP_TRANSCRIPT_LOG",
            "CLROS_M01_CAPTURE_SCRATCH",
            "CLROS_M01_GUEST_TIMEOUT_SECONDS",
            "CLROS_M01_CAPTURE_TIMEOUT_SECONDS",
            "CLROS_M02_SAFE_STUB_PROBE"
        ];
    }

    private static Dictionary<string, string?> SnapshotProcessEnvironment(
        IEnumerable<string> names)
    {
        return names.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
    }

    private static bool EnvironmentSnapshotsEqual(
        IReadOnlyDictionary<string, string?> first,
        IReadOnlyDictionary<string, string?> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }
        foreach ((string name, string? value) in first)
        {
            if (!second.TryGetValue(name, out string? secondValue)
                || !string.Equals(
                    value,
                    secondValue,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string[] SnapshotFileSystemEntries(string path)
    {
        return Directory.GetFileSystemEntries(
                path,
                "*",
                SearchOption.AllDirectories)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RemoveWhitespace(string value)
    {
        return string.Concat(value.Where(character => !char.IsWhiteSpace(
            character)));
    }

    private static bool OccursInOrder(
        string source,
        params string[] values)
    {
        int nextIndex = 0;
        foreach (string value in values)
        {
            int index = source.IndexOf(
                value,
                nextIndex,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }
            nextIndex = index + value.Length;
        }
        return true;
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


    private static ProcessResult ValidateUd2TranscriptFixture(string serialLog, string expectedReason)
    {
        try { OS.Tooling.QemuTranscripts.AssertM02Ud2VectorStateTerminalTranscript(serialLog);
            return new ProcessResult(expectedReason == "none" ? 0 : 1, "M02_UD2_VECTOR_STATE_TRANSCRIPT_VALID"); }
        catch (Exception error) { return new ProcessResult(error.Message == expectedReason ? 0 : 1, "M02_UD2_VECTOR_STATE_TRANSCRIPT_REJECTED reason=" + error.Message); }
    }

    private static ProcessResult RunQemuSmokeScript(params string[] args)
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--boot-policy", "Diagnostic", .. args]);
        return new ProcessResult(result.ExitCode, result.Output + result.Error);
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

    private enum M01RawObservationCorruption
    {
        None,
        PodHashMismatch,
        SerialHashMismatch,
        RuntimeAddressMisaligned,
        RuntimeAddressOutOfRange,
        CallGateAddressMismatch,
        SemanticRuntimeCr0Mismatch
    }

    private readonly struct M01RawObservationFixture
    {
        public M01RawObservationFixture(
            string observationPath,
            string privateSerialPath,
            string publicSerialPath,
            string qmpTranscriptPath,
            string captureScratchPath)
        {
            ObservationPath = observationPath;
            PrivateSerialPath = privateSerialPath;
            PublicSerialPath = publicSerialPath;
            QmpTranscriptPath = qmpTranscriptPath;
            CaptureScratchPath = captureScratchPath;
        }

        public string ObservationPath { get; }
        public string PrivateSerialPath { get; }
        public string PublicSerialPath { get; }
        public string QmpTranscriptPath { get; }
        public string CaptureScratchPath { get; }
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
