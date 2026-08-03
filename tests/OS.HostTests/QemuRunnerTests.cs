using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
    private static readonly string[] M01StageTwoGuestMarkers =
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
        "TEST_START m01.stage2",
        "M01_ALLOCATOR_ARENA_ALLOCATED",
        "M01_STAGE2_MAP_RECAPTURED",
        "M01_RESERVATIONS_APPLIED",
        "M01_LIVE_ALLOCATED_4K",
        "M01_LIVE_PAGE_WITHIN_ARENA",
        "M01_LIVE_FREED_4K",
        "M01_DOUBLE_FREE_REJECTED",
        "TEST_PASS m01.stage2",
        "OS_SMOKE_PASS"
    ];
    private static readonly string[] M01StageTwoForbiddenMarkers =
    [
        "LIVE_ALLOCATOR_NOT_INSTALLED",
        "BOOT_PAGE_ALLOCATOR_NOT_READY",
        "M01_ARBITRARY_CONVENTIONAL_PAGE_USED",
        "M01_PAGE_OUTSIDE_ALLOCATOR_ARENA",
        "TEST_FAIL",
        "OS_SMOKE_FAIL",
        "M01_INTEGRATION_FAIL",
        "M01_STAGE1_FAIL",
        "M01_STAGE2_FAIL"
    ];
    private static readonly string[] M01StageThreeGuestMarkers =
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
        "TEST_START m01.stage2",
        "M01_ALLOCATOR_ARENA_ALLOCATED",
        "M01_STAGE2_MAP_RECAPTURED",
        "M01_RESERVATIONS_APPLIED",
        "M01_LIVE_ALLOCATED_4K",
        "M01_LIVE_PAGE_WITHIN_ARENA",
        "M01_LIVE_FREED_4K",
        "M01_DOUBLE_FREE_REJECTED",
        "TEST_PASS m01.stage2",
        "TEST_START m01.stage3",
        "M01_CR3_STUB_PAGE_ALLOCATED",
        "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
        "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
        "M01_STAGE3_MAP_RECAPTURED",
        "M01_CR3_STUB_MATERIALIZED",
        "M01_PAGE_TABLES_BUILT",
        "M01_PAGING_MODE_VALIDATED",
        "M01_PRE_CR3",
        "M01_FIRMWARE_CR3_RESTORED",
        "M01_RFLAGS_RESTORED",
        "M01_PGE_CYCLE_VERIFIED",
        "M01_OWNED_CR3_CANARY_VERIFIED",
        "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
        "M01_POST_CR3",
        "M01_RW_TO_RX_VERIFIED",
        "M01_TLB_INVALIDATION_COMPLETE",
        "M01_RX_PROBE_EXECUTED",
        "M01_PAGE_ZERO_ABSENT",
        "TEST_PASS m01.stage3",
        "CLROS_M01_LIVE_BOOT_MEMORY_READY",
        "OS_SMOKE_PASS"
    ];
    private static readonly string[] M01StageThreeForbiddenMarkers =
    [
        "LIVE_ALLOCATOR_NOT_INSTALLED",
        "BOOT_PAGE_ALLOCATOR_NOT_READY",
        "M01_ARBITRARY_CONVENTIONAL_PAGE_USED",
        "M01_PAGE_OUTSIDE_ALLOCATOR_ARENA",
        "M01_PAGE_TABLE_OUTSIDE_ARENA",
        "M01_PROTECTION_PROBE_ALLOCATION_FAILED",
        "M01_PROTECTION_PROBE_ALIAS",
        "M01_CR3_STUB_READBACK_FAILED",
        "M01_PAGING_MODE_VALIDATION_FAILED",
        "M01_PGE_CLEAR_FAILED",
        "M01_PGE_RESTORE_FAILED",
        "M01_EXECUTABLE_TABLE_DATA",
        "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED",
        "M01_FIRMWARE_CALLBACK_DURING_OWNED_CR3",
        "M01_FIRMWARE_CR3_RESTORE_FAILED",
        "M01_CR4_RESTORE_FAILED",
        "M01_RFLAGS_RESTORE_FAILED",
        "M01_OWNED_CR3_CANARY_FAILED",
        "M01_RX_PROBE_FAILED",
        "TEST_FAIL",
        "OS_SMOKE_FAIL",
        "M01_INTEGRATION_FAIL",
        "M01_STAGE1_FAIL",
        "M01_STAGE2_FAIL",
        "M01_STAGE3_FAIL"
    ];
    private static readonly string[] M02IssueSuccessMarkers =
    [
        "TEST_START m02.017",
        "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E",
        "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=F85393074A792B1E",
        "M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B",
        "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E temporaryOwnedRootRx=true portIoExecuted=false",
        "TEST_PASS m02.017"
    ];
    private static readonly string[] M02IssueForbiddenMarkers =
    [
        "M02_STUB_PAGE_MATERIALIZATION_FAILED",
        "M02_STUB_PAGE_READBACK_FAILED",
        "M02_STUB_PAGE_HASH_MISMATCH",
        "M02_SAFE_PROBE_FAILED",
        "M02_OUT8_EXECUTED",
        "M02_IN8_EXECUTED",
        "M02_PORT_IO_ATTEMPTED",
        "M02_PRIVILEGED_STUB_TARGETED",
        "TEST_FAIL m02.017"
    ];
    private static readonly string[] M02GuestMarkers =
        BuildM02GuestMarkers();
    private static readonly string[] M02ForbiddenMarkers =
    [
        .. M01StageThreeForbiddenMarkers,
        .. M02IssueForbiddenMarkers
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

    private static bool TestSmokeScriptForwardsM01StageTwoEvidenceControls(
        string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
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
            && CountOccurrences(
                script,
                "[switch] $EnableM01LiveAllocatorProbe") == 1
            && CountOccurrences(
                script,
                "$buildArguments += \"-EnableM01LiveAllocatorProbe\"") == 1
            && result.Output.Contains(
                "QEMU_SCRIPT_M01_LIVE_ALLOCATOR_PROBE enabled=true loadedImageStack=true buildEfi=true skipImageBuild=false dryRun=true",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
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
                "TEST_FAIL qemu.smoke.invalid_args m01_live_allocator_probe_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_live_allocator_probe_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_live_allocator_probe_forbids_skip_image_build",
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
            || M01StageThreeGuestMarkers.Length != 42
            || M01StageThreeForbiddenMarkers.Length != 25
            || M01StageThreeGuestMarkers.Distinct(
                StringComparer.Ordinal).Count() != 42
            || M01StageThreeForbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() != 25)
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
                "run-qemu-smoke.ps1 -DryRun",
                StringComparison.Ordinal));
        string? realCommand = commands.SingleOrDefault(
            command => command.Contains(
                "run-qemu-smoke.ps1 -BuildEfi",
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
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
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
            && CountOccurrences(
                script,
                "[switch] $EnableM01OwnedPagingProbe") == 1
            && CountOccurrences(
                script,
                "$buildArguments += \"-EnableM01OwnedPagingProbe\"") == 1
            && result.Output.Contains(
                "QEMU_SCRIPT_M01_OWNED_PAGING_PROBE enabled=true liveAllocator=true loadedImageStack=true buildEfi=true skipImageBuild=false dryRun=true",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " + formattedMarkers,
                StringComparison.Ordinal)
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
                "TEST_FAIL qemu.smoke.invalid_args m01_owned_paging_probe_requires_live_allocator",
                StringComparison.Ordinal)
            && transitiveMissingStageOne.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_live_allocator_probe_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_owned_paging_probe_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args m01_owned_paging_probe_forbids_skip_image_build",
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
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
        string source = ExtractM01RawObservationShimSource(script);
        string projectDirectory = Path.Combine(
            tempRoot,
            "m01-raw-shim-compile-" + Guid.NewGuid().ToString("N"));
        string outputDirectory = Path.Combine(projectDirectory, "out");
        string projectPath = Path.Combine(
            projectDirectory,
            "M01.RawObservationShim.csproj");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Program.cs"),
            source,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <InvariantGlobalization>true</InvariantGlobalization>
              </PropertyGroup>
            </Project>
            """,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = RunDotnetBuild(
            repositoryRoot,
            projectPath,
            outputDirectory);

        return result.ExitCode == 0
            && File.Exists(
                Path.Combine(outputDirectory, "M01.RawObservationShim.dll"))
            && !result.Output.Contains(
                ": warning ",
                StringComparison.OrdinalIgnoreCase)
            && !result.Output.Contains(
                "Build FAILED",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestM01RawObservationShimMarkerParity()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
        string source = ExtractM01RawObservationShimSource(script);
        string[] guestMarkers = ExtractM01RawObservationShimArray(
            source,
            "GuestMarkers");
        string[] forbiddenMarkers = ExtractM01RawObservationShimArray(
            source,
            "ForbiddenMarkers");

        return guestMarkers.Length == 42
            && forbiddenMarkers.Length == 25
            && guestMarkers.SequenceEqual(
                M01StageThreeGuestMarkers,
                StringComparer.Ordinal)
            && forbiddenMarkers.SequenceEqual(
                M01StageThreeForbiddenMarkers,
                StringComparer.Ordinal)
            && guestMarkers.Distinct(
                StringComparer.Ordinal).Count() == guestMarkers.Length
            && forbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() == forbiddenMarkers.Length;
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
            || M02GuestMarkers.Length != 48
            || M02ForbiddenMarkers.Length != 34
            || M02GuestMarkers.Distinct(
                StringComparer.Ordinal).Count() != 48
            || M02ForbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() != 34)
        {
            return false;
        }

        string[] expectedManifestMarkers =
        [
            .. M02IssueSuccessMarkers,
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
                "run-qemu-smoke.ps1 -BuildEfi",
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
                "QEMU_SCRIPT_M02_SAFE_STUB_PROBE enabled=true ownedPaging=true liveAllocator=true loadedImageStack=true buildEfi=true skipImageBuild=false dryRun=true profileRequired=48 profileForbidden=34",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " + formattedGuest,
                StringComparison.Ordinal)
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
                "m02_safe_stub_probe_requires_loaded_image_stack",
                StringComparison.Ordinal)
            && missingAllocator.Output.Contains(
                "m02_safe_stub_probe_requires_live_allocator",
                StringComparison.Ordinal)
            && missingOwnedPaging.Output.Contains(
                "m02_safe_stub_probe_requires_owned_paging",
                StringComparison.Ordinal)
            && missingBuild.Output.Contains(
                "m02_safe_stub_probe_requires_build_efi",
                StringComparison.Ordinal)
            && skippedImage.Output.Contains(
                "m02_safe_stub_probe_forbids_skip_image_build",
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

    private static bool TestM02RawObservationShimMarkerParity()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
        string source = ExtractM01RawObservationShimSource(script);
        string[] guestMarkers = ExtractM01RawObservationShimArray(
            source,
            "M02GuestMarkers");
        string[] forbiddenMarkers = ExtractM01RawObservationShimArray(
            source,
            "M02ForbiddenMarkers");

        return guestMarkers.Length == 48
            && forbiddenMarkers.Length == 34
            && guestMarkers.SequenceEqual(
                M02GuestMarkers,
                StringComparer.Ordinal)
            && forbiddenMarkers.SequenceEqual(
                M02ForbiddenMarkers,
                StringComparer.Ordinal)
            && guestMarkers.Distinct(
                StringComparer.Ordinal).Count() == 48
            && forbiddenMarkers.Distinct(
                StringComparer.Ordinal).Count() == 34;
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
                "QEMU_SCRIPT_M01_OWNED_PAGING_PROBE enabled=true",
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
                "TEST_FAIL qemu.smoke.invalid_args m01_raw_observation_log_requires_owned_paging_probe",
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
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string suffix = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(
            tempRoot,
            "m01-raw-summary-overlap-" + suffix);
        Directory.CreateDirectory(directory);
        string sharedPath = Path.Combine(directory, "shared.log");
        string serialPath = Path.Combine(directory, "serial.log");
        string launchSentinel = Path.Combine(directory, "qemu-launched.txt");
        string fakeQemu = CreateLaunchSentinelFakeQemuScript(
            directory,
            launchSentinel);
        string harnessPath = Path.Combine(directory, "path-overlap.ps1");
        File.WriteAllText(
            harnessPath,
            GetM01RawObservationPathOverlapHarness(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        string[] pathsBefore = SnapshotFileSystemEntries(directory);
        string productionScript = Path.Combine(
            repositoryRoot,
            "scripts",
            "run-qemu-smoke.ps1");
        string script = File.ReadAllText(productionScript);

        ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };
        string[] arguments =
        [
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            harnessPath,
            "-ProductionScript",
            productionScript,
            "-FakeQemu",
            fakeQemu,
            "-SharedPath",
            sharedPath,
            "-SerialPath",
            serialPath
        ];
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start M01 path-overlap verifier.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            throw new TimeoutException(
                "M01 path-overlap verifier did not exit within 30 seconds.");
        }
        string output = standardOutput.GetAwaiter().GetResult()
            + standardError.GetAwaiter().GetResult();
        string[] pathsAfter = SnapshotFileSystemEntries(directory);

        return process.ExitCode == 0
            && output.Contains(
                "EXPECTED_FAILURE capture_path_overlap",
                StringComparison.Ordinal)
            && script.Contains(
                "-SummaryLog $SummaryLog",
                StringComparison.Ordinal)
            && script.Contains(
                "$summaryPath,",
                StringComparison.Ordinal)
            && pathsBefore.SequenceEqual(pathsAfter, StringComparer.Ordinal)
            && !File.Exists(sharedPath)
            && !File.Exists(serialPath)
            && !File.Exists(launchSentinel)
            && !Directory.GetDirectories(
                    directory,
                    "m01-026-raw-*",
                    SearchOption.TopDirectoryOnly)
                .Any();
    }

    private static bool TestM01RawObservationWrapperIsFailClosed()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "run-qemu-smoke.ps1"));
        string source = ExtractM01RawObservationShimSource(script);
        string compactScript = RemoveWhitespace(script);
        string compactSource = RemoveWhitespace(source);
        string[] environmentNames = GetM01RawObservationEnvironmentNames();
        const string EnvironmentTableStart =
            "$environmentValues = [ordered]@{";
        int environmentTableStart = script.IndexOf(
            EnvironmentTableStart,
            StringComparison.Ordinal);
        int environmentTableEnd = environmentTableStart < 0
            ? -1
            : script.IndexOf(
                "    }",
                environmentTableStart,
                StringComparison.Ordinal);
        string environmentTable =
            environmentTableStart >= 0 && environmentTableEnd > environmentTableStart
                ? script.Substring(
                    environmentTableStart,
                    environmentTableEnd - environmentTableStart)
                : string.Empty;
        string[] discoveredEnvironmentNames =
            System.Text.RegularExpressions.Regex.Matches(
                    environmentTable,
                    @"CLROS_M0[12]_[A-Z_]+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        string[] expectedEnvironmentNames = environmentNames
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] requiredStringEnvironmentNames =
        [
            "CLROS_M01_REAL_QEMU",
            "CLROS_M01_PRIVATE_SERIAL",
            "CLROS_M01_RAW_OBSERVATION_LOG",
            "CLROS_M01_QMP_TRANSCRIPT_LOG",
            "CLROS_M01_CAPTURE_SCRATCH"
        ];
        string[] fatalQmpEvents = ExtractM01FatalQmpEvents(source);
        string[] expectedFatalQmpEvents =
        [
            "RESET",
            "SHUTDOWN",
            "GUEST_PANICKED",
            "WATCHDOG",
            "POWERDOWN",
            "RESUME"
        ];

        bool environmentContract =
            environmentTable.Length > 0
            && discoveredEnvironmentNames.SequenceEqual(
                expectedEnvironmentNames,
                StringComparer.Ordinal)
            && requiredStringEnvironmentNames.All(
                name => CountOccurrences(
                    source,
                    "RequiredEnvironment(\"" + name + "\")") == 1)
            && environmentNames
                .Where(name => name != "CLROS_M02_SAFE_STUB_PROBE")
                .All(
                    name => script.Contains(
                        name + " = $Capture.",
                        StringComparison.Ordinal)
                        || script.Contains(
                            name + " = [string] $Capture.",
                            StringComparison.Ordinal))
            && compactScript.Contains(
                "CLROS_M02_SAFE_STUB_PROBE=(Format-QemuSmokeBoolean$Capture.M02SafeStubEnabled)",
                StringComparison.Ordinal)
            && script.Contains(
                "[System.EnvironmentVariableTarget]::Process",
                StringComparison.Ordinal)
            && OccursInOrder(
                script,
                "Enter-M01RawObservationEnvironment",
                "& dotnet run @arguments",
                "Restore-M01RawObservationEnvironment");

        bool substitutionAndTimeoutContract =
            script.Contains(
                "$runnerQemu = $m01RawObservationCapture.ShimExecutablePath",
                StringComparison.Ordinal)
            && compactScript.Contains(
                "$arguments+=@(\"--qemu\",$runnerQemu)",
                StringComparison.Ordinal)
            && compactScript.Contains(
                "$captureTimeoutSeconds=60",
                StringComparison.Ordinal)
            && compactScript.Contains(
                "$runnerTimeoutSeconds=$GuestTimeoutSeconds+$captureTimeoutSeconds+15",
                StringComparison.Ordinal)
            && compactScript.Contains(
                "\"--timeout\",\"$runnerTimeoutSeconds\"",
                StringComparison.Ordinal)
            && compactSource.Contains(
                "RequiredPositiveInt(\"CLROS_M01_GUEST_TIMEOUT_SECONDS\")",
                StringComparison.Ordinal)
            && compactSource.Contains(
                "RequiredPositiveInt(\"CLROS_M01_CAPTURE_TIMEOUT_SECONDS\")",
                StringComparison.Ordinal)
            && source.Contains(
                "ProcessStartInfo startInfo = new ProcessStartInfo(realQemu)",
                StringComparison.Ordinal);

        bool terminalGateContract =
            OccursInOrder(
                source,
                "gate.ValidateCompleteMarkerContract();",
                "qmp.StopAndRequirePaused();",
                "ObservationScanner.Capture(",
                "DeleteEmptyCaptureScratch(scratchDirectory);",
                "WriteObservationSidecar(",
                "publicStream.Write(")
            && source.Contains(
                "private const string SuccessTerminal = \"OS_SMOKE_PASS\";",
                StringComparison.Ordinal)
            && source.Contains(
                "public bool TerminalWithheld { get; private set; }",
                StringComparison.Ordinal)
            && source.Contains(
                "\"duplicate_success_terminal\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"marker_order_or_duplicate_\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"owned_cr3_serial_marker_forbidden\"",
                StringComparison.Ordinal)
            && source.Contains(
                "writer.WriteLine(\"terminalWithheld=true\");",
                StringComparison.Ordinal)
            && source.Contains(
                "writer.WriteLine(\"terminalReleaseAuthorized=true\");",
                StringComparison.Ordinal)
            && source.Contains(
                "File.Move(temporaryPath, path);",
                StringComparison.Ordinal)
            && source.Contains(
                "stream.Flush(flushToDisk: true);",
                StringComparison.Ordinal);

        bool sidecarPodAndHashContract =
            compactScript.Contains(
                "if($values.Count-ne$expectedKeys.Count)",
                StringComparison.Ordinal)
            && script.Contains(
                "schema = \"clros.m01.raw-observations.v1\"",
                StringComparison.Ordinal)
            && script.Contains(
                "captureMethod = \"qmp-pmemsave-stopped-before-terminal-release\"",
                StringComparison.Ordinal)
            && script.Contains(
                "memoryDumpRetained = \"false\"",
                StringComparison.Ordinal)
            && script.Contains(
                "exactEqualityValidation = \"pass\"",
                StringComparison.Ordinal)
            && script.Contains(
                "terminalReleaseAuthorized = \"true\"",
                StringComparison.Ordinal)
            && compactScript.Contains(
                "$preflightBytes.Length-ne56-or$runtimeStateBytes.Length-ne480-or$callGateBytes.Length-ne160",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_pod_hash_mismatch\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_exact_restoration_mismatch\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_runtime_readback_mismatch\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_runtime_address_invalid\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_call_gate_address_mismatch\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_preflight_structure_invalid\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_runtime_structure_invalid\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_call_gate_structure_invalid\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_serial_hash_mismatch\"",
                StringComparison.Ordinal)
            && script.Contains(
                "throw \"observation_capture_data_retained\"",
                StringComparison.Ordinal)
            && source.Contains(
                "writer.WriteLine(\"memoryDumpRetained=false\");",
                StringComparison.Ordinal)
            && source.Contains(
                "string privateSerialHash = Sha256File(privateSerial);",
                StringComparison.Ordinal)
            && source.Contains(
                "string predictedPublicHash = Sha256FilePlusBytes(",
                StringComparison.Ordinal)
            && source.Contains(
                "\"serial_prediction_hash_mismatch\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"released_public_serial_hash_mismatch\"",
                StringComparison.Ordinal)
            && OccursInOrder(
                script,
                "$runnerExitCode = $script:LastNativeExitCode",
                "Assert-M01RawObservationSidecar",
                "TEST_PASS m01.026.raw-observations");

        bool qmpContract =
            fatalQmpEvents.SequenceEqual(
                expectedFatalQmpEvents,
                StringComparer.Ordinal)
            && source.Contains(
                "\"qmp_terminal_event_\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"qmp_error_\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"qmp_return_missing_\"",
                StringComparison.Ordinal)
            && source.Contains(
                "qmp.StopAndRequirePaused();",
                StringComparison.Ordinal)
            && source.Contains(
                "qmp.Pmemsave(",
                StringComparison.Ordinal)
            && compactSource.Contains(
                "privateconstintGuestMemoryBytes=256*1024*1024;",
                StringComparison.Ordinal)
            && compactSource.Contains(
                "privateconstintCaptureChunkBytes=16*1024*1024;",
                StringComparison.Ordinal)
            && source.Contains(
                "File.Delete(chunkPath);",
                StringComparison.Ordinal)
            && source.Contains(
                "\"capture_scratch_not_empty_after_scan\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"capture_scratch_delete_failed\"",
                StringComparison.Ordinal);

        return environmentContract
            && substitutionAndTimeoutContract
            && terminalGateContract
            && sidecarPodAndHashContract
            && qmpContract;
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
                timeoutSeconds: 5,
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

    private static string[] BuildM02GuestMarkers()
    {
        List<string> markers = [];
        markers.AddRange(M01StageThreeGuestMarkers.Take(25));
        markers.AddRange(M02IssueSuccessMarkers.Take(3));
        markers.AddRange(M01StageThreeGuestMarkers.Skip(25).Take(14));
        markers.AddRange(M02IssueSuccessMarkers.Skip(3));
        markers.AddRange(M01StageThreeGuestMarkers.Skip(39));
        return markers.ToArray();
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
            "set \"CLROS_FAKE_QEMU_SERIAL=%serial%\"",
            "powershell.exe -NoProfile -Command " +
                "\"$path = $env:CLROS_FAKE_QEMU_SERIAL; " +
                "$stream = [System.IO.File]::Open(" +
                "$path, [System.IO.FileMode]::Create, " +
                "[System.IO.FileAccess]::Write, " +
                "[System.IO.FileShare]::ReadWrite); " +
                "$writer = [System.IO.StreamWriter]::new(" +
                "$stream, [System.Text.Encoding]::ASCII); " +
                "try { " +
                "$writer.NewLine = [Environment]::NewLine; " +
                "$writer.WriteLine('OS_BOOT_OK'); " +
                "$writer.WriteLine('TEST_PASS boot.sequence'); " +
                "$writer.WriteLine('OS_SMOKE_PASS'); " +
                "$writer.Flush(); " +
                "Start-Sleep -Milliseconds 200; " +
                "$writer.WriteLine('" +
                delayedTerminal +
                "'); $writer.Flush(); " +
                "} finally { $writer.Dispose() }\"",
            "exit /b %ERRORLEVEL%"
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
            "set \"CLROS_FAKE_QEMU_SERIAL=%serial%\"",
            "powershell.exe -NoProfile -Command " +
                "\"$path = $env:CLROS_FAKE_QEMU_SERIAL; " +
                "$stream = [System.IO.File]::Open(" +
                "$path, [System.IO.FileMode]::Create, " +
                "[System.IO.FileAccess]::Write, " +
                "[System.IO.FileShare]::ReadWrite); " +
                "$writer = [System.IO.StreamWriter]::new(" +
                "$stream, [System.Text.Encoding]::ASCII); " +
                "try { " +
                "$writer.NewLine = [Environment]::NewLine; " +
                "$writer.WriteLine('OS_BOOT_OK'); " +
                "$writer.WriteLine('TEST_PASS boot.sequence'); " +
                "$writer.WriteLine('OS_SMOKE_PASS'); " +
                "$writer.Flush(); " +
                "Start-Sleep -Milliseconds " +
                delayMilliseconds +
                "; } finally { $writer.Dispose() }; " +
                "exit " +
                exitCode +
                "\"",
            "exit /b %ERRORLEVEL%"
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
        string harnessPath = Path.Combine(directory, "verify-sidecar.ps1");

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
        File.WriteAllText(
            harnessPath,
            GetM01RawObservationVerifierHarness(),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new M01RawObservationFixture(
            Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                "scripts",
                "run-qemu-smoke.ps1"),
            harnessPath,
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
        ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = HostToolProcess.FindRepositoryRoot()
        };
        List<string> arguments =
        [
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            fixture.HarnessPath,
            "-ProductionScript",
            fixture.ProductionScriptPath,
            "-ObservationPath",
            fixture.ObservationPath,
            "-PrivateSerialPath",
            fixture.PrivateSerialPath,
            "-PublicSerialPath",
            fixture.PublicSerialPath,
            "-QmpTranscriptPath",
            fixture.QmpTranscriptPath,
            "-CaptureScratchPath",
            fixture.CaptureScratchPath
        ];
        if (expectedError is null)
        {
            arguments.Add("-ExpectSuccess");
        }
        else
        {
            arguments.Add("-ExpectedError");
            arguments.Add(expectedError);
        }
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start M01 raw-observation verifier.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            throw new TimeoutException(
                "M01 raw-observation verifier did not exit within 30 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult()
                + standardError.GetAwaiter().GetResult());
    }

    private static string GetM01RawObservationPathOverlapHarness()
    {
        return
            """
            param(
                [Parameter(Mandatory = $true)] [string] $ProductionScript,
                [Parameter(Mandatory = $true)] [string] $FakeQemu,
                [Parameter(Mandatory = $true)] [string] $SharedPath,
                [Parameter(Mandatory = $true)] [string] $SerialPath
            )

            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $ProductionScript,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                Write-Host "HARNESS_FAIL production_script_parse"
                exit 96
            }

            $requiredFunctions = @(
                "Resolve-M01AbsolutePath",
                "New-M01RawObservationCapture"
            )
            $functionNodes = @($ast.FindAll({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $requiredFunctions -ccontains $node.Name
            }, $true))
            foreach ($functionName in $requiredFunctions) {
                $matching = @($functionNodes | Where-Object {
                    $_.Name -ceq $functionName
                })
                if ($matching.Count -ne 1) {
                    Write-Host (
                        "HARNESS_FAIL function_count " +
                        $functionName +
                        " count=" +
                        $matching.Count)
                    exit 97
                }
                . ([scriptblock]::Create($matching[0].Extent.Text))
            }

            try {
                New-M01RawObservationCapture `
                    -RealQemuPath $FakeQemu `
                    -RequestedObservationLog $SharedPath `
                    -PublicSerialLog $SerialPath `
                    -SummaryLog $SharedPath `
                    -GuestTimeoutSeconds 15 | Out-Null
                Write-Host "HARNESS_FAIL unexpected_capture_creation"
                exit 98
            }
            catch {
                $actualError = $_.Exception.Message
                if (-not [string]::Equals(
                        $actualError,
                        "capture_path_overlap",
                        [System.StringComparison]::Ordinal)) {
                    Write-Host (
                        "HARNESS_FAIL unexpected_error actual=" +
                        $actualError)
                    exit 99
                }
                Write-Host "EXPECTED_FAILURE capture_path_overlap"
                exit 0
            }
            """;
    }

    private static string GetM01RawObservationVerifierHarness()
    {
        return
            """
            param(
                [Parameter(Mandatory = $true)] [string] $ProductionScript,
                [Parameter(Mandatory = $true)] [string] $ObservationPath,
                [Parameter(Mandatory = $true)] [string] $PrivateSerialPath,
                [Parameter(Mandatory = $true)] [string] $PublicSerialPath,
                [Parameter(Mandatory = $true)] [string] $QmpTranscriptPath,
                [Parameter(Mandatory = $true)] [string] $CaptureScratchPath,
                [AllowEmptyString()] [string] $ExpectedError = "",
                [switch] $ExpectSuccess
            )

            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $ProductionScript,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                Write-Host "HARNESS_FAIL production_script_parse"
                exit 96
            }

            $requiredFunctions = @(
                "Get-M01FileSha256",
                "Get-M01ByteSha256",
                "Format-M01Hex64",
                "Format-M01Hex32",
                "Read-M01RawObservationSidecar",
                "Assert-M01RawObservationSidecar"
            )
            $functionNodes = @($ast.FindAll({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $requiredFunctions -ccontains $node.Name
            }, $true))
            foreach ($functionName in $requiredFunctions) {
                $matching = @($functionNodes | Where-Object {
                    $_.Name -ceq $functionName
                })
                if ($matching.Count -ne 1) {
                    Write-Host (
                        "HARNESS_FAIL function_count " +
                        $functionName +
                        " count=" +
                        $matching.Count)
                    exit 97
                }
                . ([scriptblock]::Create($matching[0].Extent.Text))
            }

            $capture = [PSCustomObject]@{
                ObservationPath = $ObservationPath
                GuestTimeoutSeconds = 15
                CaptureTimeoutSeconds = 60
                PrivateSerialPath = $PrivateSerialPath
                PublicSerialPath = $PublicSerialPath
                QmpTranscriptPath = $QmpTranscriptPath
                CaptureScratchPath = $CaptureScratchPath
            }
            try {
                $evidence =
                    Assert-M01RawObservationSidecar -Capture $capture
                if (-not $ExpectSuccess) {
                    Write-Host "HARNESS_FAIL unexpected_verifier_pass"
                    exit 98
                }
                $actualSerialHash =
                    Get-M01FileSha256 -Path $PublicSerialPath
                if ($evidence.CandidateCount -ne 1 -or
                    -not [string]::Equals(
                        $evidence.SerialSha256,
                        $actualSerialHash,
                        [System.StringComparison]::Ordinal) -or
                    -not [string]::Equals(
                        $evidence.ObservationSha256,
                        (Get-M01FileSha256 -Path $ObservationPath),
                        [System.StringComparison]::Ordinal) -or
                    -not [string]::Equals(
                        $evidence.QmpTranscriptSha256,
                        (Get-M01FileSha256 -Path $QmpTranscriptPath),
                        [System.StringComparison]::Ordinal)) {
                    throw "verifier_return_evidence_mismatch"
                }
                Write-Host (
                    "VERIFIER_PASS serialSha256=" +
                    $evidence.SerialSha256 +
                    " candidates=" +
                    $evidence.CandidateCount)
                exit 0
            }
            catch {
                $actualError = $_.Exception.Message
                if ($ExpectSuccess) {
                    Write-Host (
                        "HARNESS_FAIL unexpected_success_error actual=" +
                        $actualError)
                    exit 95
                }
                if (-not [string]::Equals(
                        $actualError,
                        $ExpectedError,
                        [System.StringComparison]::Ordinal)) {
                    Write-Host (
                        "HARNESS_FAIL unexpected_error expected=" +
                        $ExpectedError +
                        " actual=" +
                        $actualError)
                    exit 99
                }
                Write-Host ("EXPECTED_FAILURE " + $actualError)
                exit 0
            }
            """;
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

    private static string ExtractM01RawObservationShimSource(string script)
    {
        const string functionDeclaration =
            "function Get-M01RawObservationShimSource {";
        const string returnDeclaration = "return @'";
        if (CountOccurrences(script, functionDeclaration) != 1)
        {
            throw new InvalidDataException(
                "M01 raw-observation source function is not unique.");
        }

        int functionIndex = script.IndexOf(
            functionDeclaration,
            StringComparison.Ordinal);
        int returnIndex = script.IndexOf(
            returnDeclaration,
            functionIndex + functionDeclaration.Length,
            StringComparison.Ordinal);
        if (returnIndex < 0)
        {
            throw new InvalidDataException(
                "M01 raw-observation source here-string is missing.");
        }

        int sourceIndex = script.IndexOf('\n', returnIndex);
        if (sourceIndex < 0)
        {
            throw new InvalidDataException(
                "M01 raw-observation source start is missing.");
        }
        sourceIndex++;

        int crlfTerminator = script.IndexOf(
            "\r\n'@",
            sourceIndex,
            StringComparison.Ordinal);
        int lfTerminator = script.IndexOf(
            "\n'@",
            sourceIndex,
            StringComparison.Ordinal);
        int sourceEnd = crlfTerminator >= 0
            ? crlfTerminator
            : lfTerminator;
        if (sourceEnd < sourceIndex)
        {
            throw new InvalidDataException(
                "M01 raw-observation source terminator is missing.");
        }

        string source = script[sourceIndex..sourceEnd];
        if (!source.Contains(
                "internal static class Program",
                StringComparison.Ordinal)
            || !source.TrimEnd().EndsWith("}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "M01 raw-observation source body is malformed.");
        }

        return source;
    }

    private static string[] ExtractM01RawObservationShimArray(
        string source,
        string arrayName)
    {
        string declaration =
            "private static readonly string[] " + arrayName;
        if (CountOccurrences(source, declaration) != 1)
        {
            throw new InvalidDataException(
                "M01 raw-observation array is not unique: " + arrayName);
        }

        int declarationIndex = source.IndexOf(
            declaration,
            StringComparison.Ordinal);
        int bodyStart = source.IndexOf('{', declarationIndex);
        int bodyEnd = source.IndexOf(
            "};",
            bodyStart + 1,
            StringComparison.Ordinal);
        if (bodyStart < 0 || bodyEnd < 0)
        {
            throw new InvalidDataException(
                "M01 raw-observation array is malformed: " + arrayName);
        }

        List<string> values = [];
        string body = source[(bodyStart + 1)..bodyEnd];
        foreach (string rawLine in body.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string literal = rawLine.Trim();
            if (literal.Length == 0)
            {
                continue;
            }
            if (literal.EndsWith(",", StringComparison.Ordinal))
            {
                literal = literal[..^1].TrimEnd();
            }
            if (literal.Length < 2
                || literal[0] != '"'
                || literal[^1] != '"')
            {
                throw new InvalidDataException(
                    "M01 raw-observation array contains a non-literal: "
                    + arrayName);
            }

            string? value = JsonSerializer.Deserialize<string>(literal);
            if (value is null)
            {
                throw new InvalidDataException(
                    "M01 raw-observation array contains null: " + arrayName);
            }
            values.Add(value);
        }
        return values.ToArray();
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

    private static ProcessResult RunDotnetBuild(
        string workingDirectory,
        string projectPath,
        string outputDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("-m:1");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start dotnet build.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            throw new TimeoutException(
                "M01 raw-observation shim build did not exit within 120 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult()
                + standardError.GetAwaiter().GetResult());
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
            string productionScriptPath,
            string harnessPath,
            string observationPath,
            string privateSerialPath,
            string publicSerialPath,
            string qmpTranscriptPath,
            string captureScratchPath)
        {
            ProductionScriptPath = productionScriptPath;
            HarnessPath = harnessPath;
            ObservationPath = observationPath;
            PrivateSerialPath = privateSerialPath;
            PublicSerialPath = publicSerialPath;
            QmpTranscriptPath = qmpTranscriptPath;
            CaptureScratchPath = captureScratchPath;
        }

        public string ProductionScriptPath { get; }
        public string HarnessPath { get; }
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
