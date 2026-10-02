param(
    [string] $Image = "artifacts/images/os.raw",
    [string] $EfiArtifact = "artifacts/efi/BOOTX64.EFI",
    [string] $SerialLog = "artifacts/logs/qemu-smoke.log",
    [string] $SummaryLog = "artifacts/logs/qemu-smoke.summary.log",
    [string] $M01RawObservationLog = "",
    [string] $Qemu = "",
    [string] $OvmfCode = "",
    [int] $TimeoutSeconds = 30,
    [string] $WorkloadProfile = "SingleMinimalApi",
    [string] $DiskFormat = "raw",
    [string[]] $RequireMarker = @(
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT",
        "BOOT_PAGE_ALLOCATOR_READY",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY"
    ),
    [string[]] $RequireOrderedMarker = @(),
    [string[]] $RequireUniqueMarker = @(),
    [string[]] $MarkerPreset = @(),
    [string[]] $ForbidMarker = @(
        "OS_SMOKE_FAIL"
    ),
    [switch] $DryRun,
    [switch] $RequireNativeAppMarkers,
    [switch] $BuildEfi,
    [switch] $EnableUefiGoneSimulationProbe,
    [switch] $EnableExitBootServicesFreshMapProbe,
    [switch] $EnableAcpiPhysicalDiscoveryProbe,
    [switch] $EnableM01LoadedImageStackProbe,
    [switch] $EnableM01LiveAllocatorProbe,
    [switch] $EnableM01OwnedPagingProbe,
    [switch] $EnableM02SafeStubProbe,
    [switch] $EnableM02CheckedPortIoBindingProbe,
    [switch] $EnableM02Com1OwnedPortIoProbe,
    [switch] $EnableM02OwnedCom1ReadyProbe,
    [switch] $EnableM02PanicSafeOwnedCom1Probe,
    [switch] $EnableM02OwnedX64GdtEncodingProbe,
    [switch] $EnableM02GuardedIstStackAllocationProbe,
    [switch] $EnableM02OwnedGdtTssTerminalProbe,
    [switch] $EnableM02NormalizedInterruptFramePageProbe,
    [switch] $EnableM02OwnedIdtGatePopulationProbe,
    [switch] $EnableM02IdtrContinuedExecutionProbe,
    [switch] $EnableM02Ud2VectorStateProbe,
    [switch] $EnableM02PageFaultCr2ErrorCodeProbe,
    [switch] $EnableM04IndependentEndianConformanceProbe,
    [switch] $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe,
    [switch] $EnableM04ArithmeticConformanceProbe,
    [switch] $EnableM04LayoutContractProbe,
    [switch] $EnableM04ManagedHeapPageRangeProbe,
    [switch] $EnableM04RhpNewFastProbe,
    [switch] $EnableM04RhpNewFastBeforeBindFailureProbe,
    [switch] $EnableM04RhpNewFastExhaustionFailureProbe,
    [switch] $SkipImageBuild
)

$ErrorActionPreference = "Stop"
$script:LastNativeExitCode = 0
$nativeAppRequiredMarkers = @(
    "APP_PACKAGE_READY",
    "APP_LAUNCH_READY",
    "APP_PAYLOAD_READY",
    "APP_EXEC_MAPPING_READY",
    "APP_EXEC_MEMORY_READY",
    "APP_INVOCATION_READY",
    "APP_ENTRY_READY"
)
$script:M02SafeStubRequiredMarkers = @(
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
    "TEST_START m02.017",
    "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56",
    "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=982E5B7E3EE58E56",
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
    "M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B",
    "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56 temporaryOwnedRootRx=true portIoExecuted=false",
    "TEST_PASS m02.017",
    "TEST_PASS m01.stage3",
    "CLROS_M01_LIVE_BOOT_MEMORY_READY",
    "OS_SMOKE_PASS"
)
$script:M02SafeStubForbiddenMarkers = @(
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
    "M01_STAGE3_FAIL",
    "M02_STUB_PAGE_MATERIALIZATION_FAILED",
    "M02_STUB_PAGE_READBACK_FAILED",
    "M02_STUB_PAGE_HASH_MISMATCH",
    "M02_SAFE_PROBE_FAILED",
    "M02_OUT8_EXECUTED",
    "M02_IN8_EXECUTED",
    "M02_PORT_IO_ATTEMPTED",
    "M02_PRIVILEGED_STUB_TARGETED",
    "TEST_FAIL m02.017"
)
$script:M02CheckedPortIoBindingReadyMarker =
    "CLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false"
$script:M02CheckedPortIoBindingRequiredMarkers = @()
foreach ($marker in $script:M02SafeStubRequiredMarkers) {
    $script:M02CheckedPortIoBindingRequiredMarkers += $marker
    if ($marker -eq "TEST_PASS m02.017") {
        $script:M02CheckedPortIoBindingRequiredMarkers +=
            $script:M02CheckedPortIoBindingReadyMarker
    }
}
$script:M02CheckedPortIoBindingForbiddenMarkers = @(
    $script:M02SafeStubForbiddenMarkers
    "M02_CHECKED_PORT_IO_BINDING_FAILED"
)
$script:M02Com1OwnedPortIoReadyMarker =
    "CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false"
$script:M02Com1OwnedPortIoRequiredMarkers = @()
foreach ($marker in $script:M02CheckedPortIoBindingRequiredMarkers) {
    $script:M02Com1OwnedPortIoRequiredMarkers += $marker
    if ($marker -eq $script:M02CheckedPortIoBindingReadyMarker) {
        $script:M02Com1OwnedPortIoRequiredMarkers +=
            $script:M02Com1OwnedPortIoReadyMarker
    }
}
$script:M02Com1OwnedPortIoForbiddenMarkers = @(
    $script:M02CheckedPortIoBindingForbiddenMarkers
    "M02_COM1_CONFIGURATION_FAILED"
)
$script:M02OwnedCom1ReadyStandaloneMarker = "OWNED_COM1_READY"
$script:M02OwnedCom1ReadyEvidenceMarker =
    "CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 configurationWrites=7 lineStatusPolls=18 ownedWriter=true ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial guestPortIo=true guestIn8=true postExitBootServices=false"
$script:M02OwnedCom1ReadyRequiredMarkers = @()
foreach ($marker in $script:M02Com1OwnedPortIoRequiredMarkers) {
    $script:M02OwnedCom1ReadyRequiredMarkers += $marker
    if ($marker -eq $script:M02Com1OwnedPortIoReadyMarker) {
        $script:M02OwnedCom1ReadyRequiredMarkers +=
            $script:M02OwnedCom1ReadyStandaloneMarker
        $script:M02OwnedCom1ReadyRequiredMarkers +=
            $script:M02OwnedCom1ReadyEvidenceMarker
    }
}
$script:M02OwnedCom1ReadyForbiddenMarkers = @(
    $script:M02Com1OwnedPortIoForbiddenMarkers
    "M02_OWNED_COM1_READY_FAILED"
)
$script:M02PanicSafeOwnedCom1ReadyMarker =
    "CLROS_M02_006_READY panicFormatter=bounded ownedCom1Transport=true callerScratch=true statusCoded=true firmwareFallback=false"
$script:M02PanicSafeOwnedCom1EvidenceMarker =
    "CLROS_M02_006_EVIDENCE markerBytes=125 serialBytes=127 configurationWrites=7 lineStatusPolls=127 markerTransport=com1PortIo evidenceTransport=uefiSerial guestPortIo=true guestIn8=true panicFormatter=linked panicReporter=ownedCom1 callerScratch=true statusCoded=true firmwareFallback=false postExitBootServices=false"
$script:M02PanicSafeOwnedCom1RequiredMarkers = @()
foreach ($marker in $script:M02OwnedCom1ReadyRequiredMarkers) {
    $script:M02PanicSafeOwnedCom1RequiredMarkers += $marker
    if ($marker -eq $script:M02OwnedCom1ReadyEvidenceMarker) {
        $script:M02PanicSafeOwnedCom1RequiredMarkers +=
            $script:M02PanicSafeOwnedCom1ReadyMarker
        $script:M02PanicSafeOwnedCom1RequiredMarkers +=
            $script:M02PanicSafeOwnedCom1EvidenceMarker
    }
}
$script:M02PanicSafeOwnedCom1ForbiddenMarkers = @(
    $script:M02OwnedCom1ReadyForbiddenMarkers
    "M02_PANIC_SAFE_OWNED_COM1_FAILED"
)
$script:M02OwnedX64GdtEncodingReadyMarker =
    "CLROS_M02_007_READY encoding=guestValidated callerOwned=true readBack=true gdtBytes=40 tssBytes=104 tableLimit=39 selectors=0,8,16,24 guestEncoding=true guestLgdt=false guestLtr=false allocationFree=true markerTransport=uefiSerial postExitBootServices=false"
$script:M02OwnedX64GdtEncodingRequiredMarkers = @()
foreach ($marker in $script:M02PanicSafeOwnedCom1RequiredMarkers) {
    $script:M02OwnedX64GdtEncodingRequiredMarkers += $marker
    if ($marker -eq $script:M02PanicSafeOwnedCom1EvidenceMarker) {
        $script:M02OwnedX64GdtEncodingRequiredMarkers +=
            $script:M02OwnedX64GdtEncodingReadyMarker
    }
}
$script:M02OwnedX64GdtEncodingForbiddenMarkers = @(
    $script:M02PanicSafeOwnedCom1ForbiddenMarkers
    "M02_OWNED_X64_GDT_ENCODING_FAILED"
)
$script:M02GuardedIstStackAllocationReadyMarker =
    "CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 pageFaultIst=2 nmiIst=3 writableMetadata=true executableMetadata=false allocationFree=true guestArenaAllocated=true guestStackWrites=true guestGuardMappings=false guestTss=false guestIdt=false arenaFreed=true markerTransport=uefiSerial postExitBootServices=false"
$script:M02GuardedIstStackAllocationRequiredMarkers = @()
foreach ($marker in $script:M02OwnedX64GdtEncodingRequiredMarkers) {
    $script:M02GuardedIstStackAllocationRequiredMarkers += $marker
    if ($marker -eq $script:M02OwnedX64GdtEncodingReadyMarker) {
        $script:M02GuardedIstStackAllocationRequiredMarkers +=
            $script:M02GuardedIstStackAllocationReadyMarker
    }
}
$script:M02GuardedIstStackAllocationForbiddenMarkers = @(
    $script:M02OwnedX64GdtEncodingForbiddenMarkers
    "M02_GUARDED_IST_STACK_ALLOCATION_FAILED"
)
$script:M02OwnedGdtTssTerminalReadyMarker =
    "CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 tssBytes=104 gdtrLimit=39 trSelector=24 ownedCom1=true terminalHalt=true postExitBootServices=false"
$script:M02OwnedGdtTssTerminalRequiredMarkers = @(
    $script:M02GuardedIstStackAllocationRequiredMarkers
    "TEST_START m02.009"
    $script:M02OwnedGdtTssTerminalReadyMarker
    "TEST_PASS m02.009"
)
$script:M02OwnedGdtTssTerminalForbiddenMarkers = @(
    $script:M02GuardedIstStackAllocationForbiddenMarkers
    "M02_OWNED_GDT_TSS_PREENTRY_FAILED"
    "M02_OWNED_GDT_TSS_VERIFY_FAILED"
    "M02_OWNED_GDT_TSS_UNEXPECTED_RETURN"
)
$script:M02NormalizedInterruptFramePageReadyMarker =
    "CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 commonTargetDelta=4096 fnv1a64=428D0353A96B7ACE readBack=true guestMaterialization=true loaderData=true pageReleased=true stubExecution=false idtInstalled=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false"
$script:M02NormalizedInterruptFramePageRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    "TEST_START m02.010"
    $script:M02NormalizedInterruptFramePageReadyMarker
    "TEST_PASS m02.010"
    "UEFI_MEMORY_MAP_READY"
)
$script:M02NormalizedInterruptFramePageUniqueMarkers = @(
    "TEST_START m02.010"
    $script:M02NormalizedInterruptFramePageReadyMarker
    "TEST_PASS m02.010"
)
$script:M02NormalizedInterruptFramePageForbiddenMarkers = @(
    "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED"
    "TEST_FAIL"
)
$script:M02OwnedIdtGatePopulationReadyMarker =
    "CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=true loaderData=true allocationPages=3 allocationReleased=true stubExecution=false idtrLoaded=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false"
$script:M02OwnedIdtGatePopulationRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    "TEST_START m02.011"
    $script:M02OwnedIdtGatePopulationReadyMarker
    "TEST_PASS m02.011"
    "UEFI_MEMORY_MAP_READY"
)
$script:M02OwnedIdtGatePopulationUniqueMarkers = @(
    "TEST_START m02.011"
    $script:M02OwnedIdtGatePopulationReadyMarker
    "TEST_PASS m02.011"
)
$script:M02OwnedIdtGatePopulationForbiddenMarkers = @(
    "M02_OWNED_IDT_GATE_POPULATION_FAILED"
    "TEST_FAIL"
)
$script:M02IdtrContinuedExecutionReadyMarker =
    "CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false"
$script:M02IdtrContinuedExecutionHostOnlyMarker =
    "CLROS_M02_012_HOST_READY contextBytes=80 stubBytes=52 idtrBytes=10 tableBytes=4096 limit=4095 lidtPlan=2 sidtPlan=3 cliPlan=1 stiPlan=1 returnCookie=4F53494454523132 readBack=true guestExecution=false idtrLoaded=false idtrRestored=false continuedExecution=false faultTriggered=false postExitBootServices=false"
$script:M02IdtrContinuedExecutionRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    "TEST_START m02.012"
    $script:M02IdtrContinuedExecutionReadyMarker
    "TEST_PASS m02.012"
    "UEFI_MEMORY_MAP_READY"
    "UEFI_MEMORY_MAP_OWNED"
    "BOOT_HANDOFF_READY"
    "UEFI_MEMORY_MAP_NORMALIZED"
    "BOOT_MEMORY_MAP_ACCEPTED"
    "BOOT_MEMORY_REGIONS_READY"
    "BOOT_MEMORY_REGION_COUNT"
    "BOOT_PAGE_ALLOCATOR_READY"
    "BOOT_ACPI_RSDP_HANDOFF_READY"
    "UEFI_ACPI_RSDP_READY"
    "OS_BOOT_OK"
    "TEST_PASS boot.sequence"
    "OS_SMOKE_PASS"
)
$script:M02IdtrContinuedExecutionOrderedMarkers = @(
    $script:M02IdtrContinuedExecutionRequiredMarkers
)
$script:M02IdtrContinuedExecutionUniqueMarkers = @(
    "TEST_START m02.012"
    $script:M02IdtrContinuedExecutionReadyMarker
    "TEST_PASS m02.012"
    "OS_BOOT_OK"
    "TEST_PASS boot.sequence"
    "OS_SMOKE_PASS"
)
$script:M02IdtrContinuedExecutionForbiddenMarkers = @(
    "M02_IDTR_CONTINUED_EXECUTION_FAILED"
    "TEST_FAIL"
    $script:M02IdtrContinuedExecutionHostOnlyMarker
    $script:M02OwnedIdtGatePopulationReadyMarker
)
$script:M02Ud2VectorStateReadyMarker =
    "CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false"
$script:M02Ud2VectorStateHostOnlyMarker =
    "CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 auditFields=23 probeEnabled=false executionRequested=false executionAttempted=false syntheticObservation=true guestExecution=false faultTriggered=false idtrLoaded=false postExitBootServices=false"
$script:M02Ud2VectorStateHostOnlyMarkerPrefix =
    "CLROS_M02_013_HOST_READY"
$script:M02Ud2VectorStateRequiredMarkers = @(
    "OS_BOOT_OK"
    "TEST_PASS boot.sequence"
    "OS_SMOKE_PASS"
    "TEST_START m02.013"
    $script:M02Ud2VectorStateReadyMarker
    "TEST_PASS m02.013"
)
$script:M02Ud2VectorStateOrderedMarkers = @(
    $script:M02Ud2VectorStateRequiredMarkers
)
$script:M02Ud2VectorStateUniqueMarkers = @(
    $script:M02Ud2VectorStateRequiredMarkers
)
$script:M02Ud2VectorStateForbiddenMarkers = @(
    "M02_UD2_VECTOR_STATE_VERIFY_FAILED"
    "TEST_FAIL m02.013 vector_or_register_mismatch"
    "TEST_FAIL"
    $script:M02Ud2VectorStateHostOnlyMarkerPrefix
    $script:M02Ud2VectorStateHostOnlyMarker
    $script:M02NormalizedInterruptFramePageReadyMarker
    $script:M02OwnedIdtGatePopulationReadyMarker
    $script:M02IdtrContinuedExecutionReadyMarker
)
$script:M04IndependentEndianConformanceReadyMarker =
    "CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail"
$script:M04IndependentEndianConformanceRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    $script:M04IndependentEndianConformanceReadyMarker
    "UEFI_MEMORY_MAP_READY"
)
$script:M04IndependentEndianConformanceForbiddenMarkers = @(
    "M04_ENDIAN_CONFORMANCE_FAILED"
    "TEST_FAIL"
)
$script:M04SpanMemoryConformanceReadyMarker =
    "CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail"
$script:M04SpanMemoryConformanceRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
    "UEFI_MEMORY_MAP_READY"
)
$script:M04SpanMemoryConformanceUniqueMarkers = @(
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
)
$script:M04SpanMemoryConformanceForbiddenMarkers = @(
    "M04_ENDIAN_CONFORMANCE_FAILED"
    "M04_SPAN_MEMORY_CONFORMANCE_FAILED"
    "TEST_FAIL"
)

$script:M04ArithmeticConformanceReadyMarker =
    "CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail"
$script:M04ArithmeticConformanceRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
    $script:M04ArithmeticConformanceReadyMarker
    "UEFI_MEMORY_MAP_READY"
)
$script:M04ArithmeticConformanceUniqueMarkers = @(
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
    $script:M04ArithmeticConformanceReadyMarker
)
$script:M04ArithmeticConformanceForbiddenMarkers = @(
    "M04_ENDIAN_CONFORMANCE_FAILED"
    "M04_SPAN_MEMORY_CONFORMANCE_FAILED"
    "M04_ARITHMETIC_CONFORMANCE_FAILED"
    "TEST_FAIL"
)

$script:M04LayoutContractReadyMarker =
    "CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail"
$script:M04LayoutContractRequiredMarkers = @(
    "EFI_CONSOLE_READY"
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
    $script:M04ArithmeticConformanceReadyMarker
    $script:M04LayoutContractReadyMarker
    "UEFI_MEMORY_MAP_READY"
)
$script:M04LayoutContractUniqueMarkers = @(
    $script:M04IndependentEndianConformanceReadyMarker
    $script:M04SpanMemoryConformanceReadyMarker
    $script:M04ArithmeticConformanceReadyMarker
    $script:M04LayoutContractReadyMarker
)
$script:M04LayoutContractForbiddenMarkers = @(
    "M04_ENDIAN_CONFORMANCE_FAILED"
    "M04_SPAN_MEMORY_CONFORMANCE_FAILED"
    "M04_ARITHMETIC_CONFORMANCE_FAILED"
    "M04_LAYOUT_CONTRACT_FAILED"
    "TEST_FAIL"
    "OS_SMOKE_FAIL"
)

$script:M04ManagedHeapPageRangeReadyMarker = "CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail"
$script:M04ManagedHeapPageRangeRequiredMarkers = @(
    $script:M04LayoutContractRequiredMarkers
    "TEST_START m01.stage1"
    "M01_LOADED_IMAGE_CAPTURED"
    "M01_STACK_SWITCH_STUB_READY"
    "M01_STACK_ARENA_ALLOCATED"
    "M01_STACK_GUARD_RESERVED"
    "M01_STAGE1_MAP_RECAPTURED"
    "M01_STAGE1_RESERVATIONS_VALIDATED"
    "M01_OWNED_STACK_READY"
    "M01_OWNED_STACK_SWITCHED"
    "M01_RSP_CONTAINED"
    "TEST_PASS m01.stage1"
    "TEST_START m01.stage2"
    "M01_ALLOCATOR_ARENA_ALLOCATED"
    "M01_STAGE2_MAP_RECAPTURED"
    "M01_RESERVATIONS_APPLIED"
    "M01_LIVE_ALLOCATED_4K"
    "M01_LIVE_PAGE_WITHIN_ARENA"
    "M01_LIVE_FREED_4K"
    "M01_DOUBLE_FREE_REJECTED"
    "TEST_PASS m01.stage2"
    "TEST_START m04.006"
    "M04_HEAP_PAGE_BOUND"
    "M04_HEAP_PAGE_INITIALIZED"
    "M04_HEAP_PAGE_RETAINED"
    "M04_HEAP_PAGE_CURRENT"
    "CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail"
    "TEST_PASS m04.006"
    "TEST_START boot.sequence"
    "BOOT_PHASE PhaseA.Audit START"
    "BOOT_PHASE PhaseA.Audit PASS"
    "BOOT_PHASE PhaseA.Diagnostics START"
    "BOOT_PHASE PhaseA.Diagnostics PASS"
    "OS_BOOT_OK"
    "TEST_PASS boot.sequence"
    "OS_SMOKE_PASS"
)
$script:M04ManagedHeapPageRangeForbiddenMarkers = @(
    $script:M04LayoutContractForbiddenMarkers
    "M04_HEAP_PAGE_RANGE_FAILED"
    "TEST_FAIL m04.006"
)
$script:M04RhpNewFastReadyMarker = 'CLROS_M04_007_READY objects=10 compiledNew=4 direct=6 hostConformance=false guestIntegrated=true gate4=Fail'
$script:M04RhpNewFastRequiredMarkers = @(
    $script:M04ManagedHeapPageRangeRequiredMarkers[0..($script:M04ManagedHeapPageRangeRequiredMarkers.Count - 9)]
    'TEST_START m04.007.rhp_new_fast'
    'M04_RHP_PREP'
    'M04_RHP_BIND'
    for ($rhpId=1; $rhpId -le 10; $rhpId++) {
        "M04_RHP_OBJECT id=$rhpId"
        "M04_RHP_READBACK id=$rhpId"
    }
    'M04_RHP_RETAIN'
    $script:M04RhpNewFastReadyMarker
    'TEST_PASS m04.007.rhp_new_fast'
    $script:M04ManagedHeapPageRangeRequiredMarkers[($script:M04ManagedHeapPageRangeRequiredMarkers.Count - 8)..($script:M04ManagedHeapPageRangeRequiredMarkers.Count - 1)]
)
$script:M04RhpNewFastForbiddenMarkers = @(
    $script:M04ManagedHeapPageRangeForbiddenMarkers
    'M04_RHP_ATTEMPT'
    'M04_RHP_REJECT'
    'M04_RHP_INTERNAL_REJECT'
    'M04_RHP_FILL'
    'TEST_FAIL m04.007.rhp_new_fast'
)

function Invoke-NativeCommand {
    param(
        [scriptblock] $Command
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ("os-native-stderr-" + [System.Guid]::NewGuid().ToString("N") + ".log")
    $ErrorActionPreference = "Continue"
    try {
        & $Command 2> $stderrPath
        $script:LastNativeExitCode = $LASTEXITCODE
        if (Test-Path -LiteralPath $stderrPath) {
            $stderrText = Get-Content -Raw -LiteralPath $stderrPath
            if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
                Write-Host $stderrText.TrimEnd()
            }
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Remove-Item -LiteralPath $stderrPath -ErrorAction SilentlyContinue
    }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath
$m01RawObservationCapture = $null

function New-ToolDiscoveryResult {
    param(
        [AllowNull()] [string] $Path,
        [string] $Requested,
        [string] $Source
    )

    return [PSCustomObject]@{
        Path = $Path
        Requested = $Requested
        Source = $Source
    }
}

function Resolve-QemuSmokeExecutable {
    param([string] $RequestedPath)

    $requested = $RequestedPath
    $source = "configured"
    if ([string]::IsNullOrWhiteSpace($requested)) {
        $requested = "qemu-system-x86_64"
        $source = "default"
    }

    if ([System.IO.Path]::IsPathRooted($requested) -or $requested.Contains("\") -or $requested.Contains("/")) {
        if (Test-Path -LiteralPath $requested -PathType Leaf) {
            return New-ToolDiscoveryResult -Path (Resolve-Path -LiteralPath $requested).ProviderPath -Requested $requested -Source $source
        }

        return New-ToolDiscoveryResult -Path $null -Requested $requested -Source $source
    }

    $pathValue = [System.Environment]::GetEnvironmentVariable("PATH")
    if (-not [string]::IsNullOrWhiteSpace($pathValue)) {
        foreach ($directory in $pathValue.Split([System.IO.Path]::PathSeparator)) {
            if ([string]::IsNullOrWhiteSpace($directory)) {
                continue
            }

            foreach ($candidateName in @($requested, ($requested + ".exe"), ($requested + ".cmd"), ($requested + ".bat"))) {
                $candidate = Join-Path $directory $candidateName
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return New-ToolDiscoveryResult -Path (Resolve-Path -LiteralPath $candidate).ProviderPath -Requested $requested -Source $source
                }
            }
        }
    }

    foreach ($candidate in @(
        "C:\Program Files\qemu\qemu-system-x86_64.exe",
        "C:\Program Files (x86)\qemu\qemu-system-x86_64.exe",
        "C:\ProgramData\chocolatey\bin\qemu-system-x86_64.exe",
        "C:\msys64\mingw64\bin\qemu-system-x86_64.exe"
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return New-ToolDiscoveryResult -Path (Resolve-Path -LiteralPath $candidate).ProviderPath -Requested $requested -Source "well-known"
        }
    }

    return New-ToolDiscoveryResult -Path $null -Requested $requested -Source $source
}

function Add-OvmfCandidate {
    param(
        [System.Collections.Generic.List[string]] $Candidates,
        [AllowNull()] [string] $Path
    )

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $Candidates.Add($Path)
    }
}

function Add-OvmfDirectoryCandidates {
    param(
        [System.Collections.Generic.List[string]] $Candidates,
        [string] $Directory
    )

    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return
    }

    Add-OvmfCandidate -Candidates $Candidates -Path (Join-Path $Directory "OVMF_CODE.fd")
    Add-OvmfCandidate -Candidates $Candidates -Path (Join-Path $Directory "OVMF.fd")
    Add-OvmfCandidate -Candidates $Candidates -Path (Join-Path $Directory "edk2-x86_64-code.fd")
    Add-OvmfCandidate -Candidates $Candidates -Path (Join-Path $Directory "edk2-x86_64-secure-code.fd")
}

function Resolve-QemuSmokeOvmfCode {
    param(
        [string] $RequestedPath,
        [string] $QemuPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
            return New-ToolDiscoveryResult -Path (Resolve-Path -LiteralPath $RequestedPath).ProviderPath -Requested $RequestedPath -Source "configured"
        }

        return New-ToolDiscoveryResult -Path $null -Requested $RequestedPath -Source "configured"
    }

    $candidates = New-Object System.Collections.Generic.List[string]
    foreach ($variableName in @("OS_OVMF_CODE", "OVMF_CODE", "QEMU_OVMF_CODE")) {
        Add-OvmfCandidate -Candidates $candidates -Path ([System.Environment]::GetEnvironmentVariable($variableName))
    }

    if (-not [string]::IsNullOrWhiteSpace($QemuPath)) {
        $qemuDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($QemuPath))
        Add-OvmfDirectoryCandidates -Candidates $candidates -Directory $qemuDirectory
        Add-OvmfDirectoryCandidates -Candidates $candidates -Directory (Join-Path $qemuDirectory "share")
        Add-OvmfDirectoryCandidates -Candidates $candidates -Directory (Join-Path $qemuDirectory "..\share\qemu")
    }

    foreach ($directory in @(
        "C:\Program Files\qemu",
        "C:\Program Files\qemu\share",
        "C:\Program Files (x86)\qemu",
        "C:\ProgramData\chocolatey\lib\qemu\tools",
        "C:\msys64\mingw64\share\qemu",
        "/usr/share/OVMF",
        "/usr/share/edk2/ovmf",
        "/usr/share/qemu"
    )) {
        Add-OvmfDirectoryCandidates -Candidates $candidates -Directory $directory
    }

    $seen = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $normalized = [System.IO.Path]::GetFullPath($candidate)
        if (-not $seen.Add($normalized)) {
            continue
        }

        if (Test-Path -LiteralPath $normalized -PathType Leaf) {
            return New-ToolDiscoveryResult -Path (Resolve-Path -LiteralPath $normalized).ProviderPath -Requested "auto" -Source "discovered"
        }
    }

    return New-ToolDiscoveryResult -Path $null -Requested "auto" -Source "auto"
}

function Invoke-QemuSmokeHostPreflight {
    param(
        [string] $RequestedQemu,
        [string] $RequestedOvmfCode
    )

    Write-Host "TEST_START qemu.smoke.preflight"

    $qemuResult = Resolve-QemuSmokeExecutable -RequestedPath $RequestedQemu
    $qemuMissing = [string]::IsNullOrWhiteSpace($qemuResult.Path)
    $qemuPathText = if ($qemuMissing) { "not_found" } else { $qemuResult.Path }
    Write-Host ("QEMU_SCRIPT_PREFLIGHT_QEMU requested=" + $qemuResult.Requested + " source=" + $qemuResult.Source + " result=" + $qemuPathText)

    $ovmfResult = Resolve-QemuSmokeOvmfCode -RequestedPath $RequestedOvmfCode -QemuPath $qemuResult.Path
    $ovmfMissing = [string]::IsNullOrWhiteSpace($ovmfResult.Path)
    $ovmfPathText = if ($ovmfMissing) { "not_found" } else { $ovmfResult.Path }
    Write-Host ("QEMU_SCRIPT_PREFLIGHT_OVMF requested=" + $ovmfResult.Requested + " source=" + $ovmfResult.Source + " result=" + $ovmfPathText)

    $missing = @()
    if ($qemuMissing) {
        $missing += "qemu_not_found"
    }

    if ($ovmfMissing) {
        $missing += "ovmf_not_found"
    }

    if ($missing.Count -gt 0) {
        $reason = $missing -join " "
        Write-Host ("TEST_FAIL qemu.smoke.preflight " + $reason)
        Write-Host ("TEST_FAIL qemu.smoke " + $reason)
        if ($qemuMissing) {
            exit 3
        }

        exit 6
    }

    Write-Host "TEST_PASS qemu.smoke.preflight"
    return [PSCustomObject]@{
        QemuPath = $qemuResult.Path
        OvmfCodePath = $ovmfResult.Path
    }
}

function Test-QemuSmokeImageArtifact {
    param(
        [string] $ImagePath,
        [string] $ExpectedDiskFormat
    )

    if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
        Write-Host ("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + $ImagePath + " exists=false bytes=0")
        Write-Host ("TEST_FAIL qemu.smoke image_not_found path=" + $ImagePath)
        exit 2
    }

    $fileInfo = Get-Item -LiteralPath $ImagePath
    Write-Host ("QEMU_SCRIPT_PREFLIGHT_IMAGE path=" + $ImagePath + " exists=true bytes=" + $fileInfo.Length)
    if ($fileInfo.Length -eq 0) {
        Write-Host ("TEST_FAIL qemu.smoke image_empty path=" + $ImagePath)
        exit 11
    }

    $stream = [System.IO.File]::Open($fileInfo.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $header = New-Object byte[] 4
        $read = $stream.Read($header, 0, 4)
    }
    finally {
        $stream.Dispose()
    }

    $detected = "raw-compatible"
    if ($read -eq 4 -and $header[0] -eq [byte][char]'Q' -and $header[1] -eq [byte][char]'F' -and $header[2] -eq [byte][char]'I' -and $header[3] -eq 0xFB) {
        $detected = "qcow2"
    }

    Write-Host ("QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT expected=" + $ExpectedDiskFormat + " detected=" + $detected)
    if ($ExpectedDiskFormat -eq "raw" -and $detected -eq "qcow2") {
        Write-Host "TEST_FAIL qemu.smoke disk_format_mismatch expected=raw detected=qcow2"
        exit 12
    }

    if ($ExpectedDiskFormat -eq "qcow2" -and $detected -ne "qcow2") {
        Write-Host ("TEST_FAIL qemu.smoke disk_format_mismatch expected=qcow2 detected=" + $detected)
        exit 12
    }
}

function Assert-WorkloadProfile {
    param([string] $Profile)

    if ($Profile -eq "SingleMinimalApi" -or $Profile -eq "MultiMinimalApi") {
        return
    }

    Write-Host ("TEST_FAIL workload.profile.tooling rejects invalid profile=" + $Profile + " valid=SingleMinimalApi,MultiMinimalApi")
    exit 1
}

function Write-WorkloadProfileEvidence {
    param([string] $Profile)

    Write-Host ("WORKLOAD_PROFILE " + $Profile)
    if ($Profile -eq "SingleMinimalApi") {
        Write-Host "TEST_PASS workload.profile.tooling default single"
        return
    }

    Write-Host "TEST_PASS workload.profile.tooling propagates multi"
}

function ConvertTo-QemuSmokeMarkerList {
    param(
        [AllowNull()] [string[]] $Values
    )

    $markers = New-Object System.Collections.Generic.List[string]
    $seen = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::Ordinal)
    foreach ($markerValue in $Values) {
        if ([string]::IsNullOrWhiteSpace($markerValue)) {
            continue
        }

        foreach ($marker in ($markerValue -split ",")) {
            if ([string]::IsNullOrWhiteSpace($marker)) {
                continue
            }

            $normalized = $marker.Trim()
            if ($seen.Add($normalized)) {
                $markers.Add($normalized)
            }
        }
    }

    return $markers.ToArray()
}

function Test-M02IdtrContinuedExecutionOwnedMarker {
    param(
        [AllowNull()] [string] $Marker
    )

    if ([string]::IsNullOrWhiteSpace($Marker)) {
        return $false
    }

    $normalized = $Marker.Trim()
    if ($normalized.StartsWith(
            "TEST_START m02.012",
            [System.StringComparison]::Ordinal) -or
        $normalized.StartsWith(
            "TEST_PASS m02.012",
            [System.StringComparison]::Ordinal) -or
        $normalized.StartsWith(
            "CLROS_M02_012_READY",
            [System.StringComparison]::Ordinal)) {
        return $true
    }

    return $normalized.StartsWith(
            "CLROS_M02_012",
            [System.StringComparison]::Ordinal) -and
        $script:M02IdtrContinuedExecutionReadyMarker.StartsWith(
            $normalized,
            [System.StringComparison]::Ordinal)
}

function Test-M02Ud2VectorStateOwnedMarker {
    param(
        [AllowNull()] [string] $Marker
    )

    if ([string]::IsNullOrWhiteSpace($Marker)) {
        return $false
    }

    $normalized = $Marker.Trim()
    if ($normalized.StartsWith(
            "TEST_START m02.013",
            [System.StringComparison]::Ordinal) -or
        $normalized.StartsWith(
            "TEST_PASS m02.013",
            [System.StringComparison]::Ordinal) -or
        $normalized.StartsWith(
            "CLROS_M02_013_READY",
            [System.StringComparison]::Ordinal)) {
        return $true
    }

    return $normalized.StartsWith(
        "CLROS_M02_013",
        [System.StringComparison]::Ordinal)
}

function Test-M02Ud2VectorStateTerminalMarkerLine {
    param(
        [AllowNull()] [string] $Line
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $false
    }

    $normalized = $Line.Trim()
    foreach ($prefix in @(
            "OS_",
            "TEST_",
            "CLROS_",
            "M01_",
            "M02_",
            "M03_",
            "M04_",
            "EFI_",
            "UEFI_",
            "BOOT_")) {
        if ($normalized.StartsWith(
                $prefix,
                [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Assert-M02Ud2VectorStateTerminalTranscript {
    param(
        [string] $SerialLogPath
    )

    $resolvedPath = if ([System.IO.Path]::IsPathRooted($SerialLogPath)) {
        [System.IO.Path]::GetFullPath($SerialLogPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $root $SerialLogPath))
    }
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "m02_ud2_vector_state_serial_log_missing"
    }

    $terminalMarkers = @(
        Get-Content -LiteralPath $resolvedPath | Where-Object {
            Test-M02Ud2VectorStateTerminalMarkerLine -Line $_
        } | ForEach-Object {
            $_.Trim()
        }
    )
    $smokeIndex = [System.Array]::IndexOf(
        $terminalMarkers,
        "OS_SMOKE_PASS")
    if ($smokeIndex -lt 0) {
        throw "m02_ud2_vector_state_smoke_terminal_missing"
    }

    $expectedTail = @(
        "TEST_START m02.013",
        $script:M02Ud2VectorStateReadyMarker,
        "TEST_PASS m02.013"
    )
    if ($terminalMarkers.Count -ne $smokeIndex + 1 + $expectedTail.Count) {
        throw "m02_ud2_vector_state_post_smoke_sequence_invalid"
    }
    for ($index = 0; $index -lt $expectedTail.Count; $index++) {
        if ($terminalMarkers[$smokeIndex + 1 + $index] -cne
            $expectedTail[$index]) {
            throw "m02_ud2_vector_state_post_smoke_sequence_invalid"
        }
    }
}

$script:M02PageFaultCr2ErrorCodeReadyMarker = 'CLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false'
$script:M02PageFaultCr2ErrorCodeRequiredMarkers = @(
    'OS_BOOT_OK'
    'TEST_PASS boot.sequence'
    'OS_SMOKE_PASS'
    'TEST_START m02.014'
    'M02_PAGE_FAULT_LAYOUT'
    'M02_PAGE_FAULT_MODE'
    'M02_PAGE_FAULT_ROOT'
    'M02_PAGE_FAULT_ABSENT role=zero'
    'M02_PAGE_FAULT_ABSENT role=df'
    'M02_PAGE_FAULT_ABSENT role=pf'
    'M02_PAGE_FAULT_ABSENT role=nmi'
    'M02_PAGE_FAULT_ABSENT role=transition'
    'M02_PAGE_FAULT_FRAME'
    $script:M02PageFaultCr2ErrorCodeReadyMarker
    'TEST_PASS m02.014'
)
$script:M02PageFaultCr2ErrorCodeOrderedMarkers = @($script:M02PageFaultCr2ErrorCodeRequiredMarkers)
$script:M02PageFaultCr2ErrorCodeUniqueMarkers = @($script:M02PageFaultCr2ErrorCodeRequiredMarkers)
$script:M02PageFaultCr2ErrorCodeForbiddenMarkers = @(
    'TEST_FAIL'
    'OS_SMOKE_FAIL'
    'M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED'
    'CLROS_M02_014_HOST_READY'
    'CLROS_M02_014_PAGE_IMAGE_HOST_READY'
    'CLROS_M02_014_TRANSITION_HOST_READY'
    'CLROS_M02_014_EFI_HOST_READY'
    'CLROS_M02_013_READY'
)

function Test-M02PageFaultCr2ErrorCodeOwnedMarker {
    param([AllowNull()][string] $Marker)
    if ([string]::IsNullOrEmpty($Marker)) { return $false }
    $candidate = [regex]::Replace($Marker, '[\x00-\x20\x7f-\uffff]', '')
    return [regex]::IsMatch($candidate, '(?i)M02_PAGE_FAULT|CLROS_M02_014|TEST_(?:START|PASS|FAIL)m02\.014')
}

function Assert-M02PageFaultCr2ErrorCodeTranscript {
    param([string] $SerialLogPath)
    $path = if ([IO.Path]::IsPathRooted($SerialLogPath)) { $SerialLogPath } else { Join-Path $root $SerialLogPath }
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path))
    # Classification only: split delimiters, controls and case cannot conceal owned/failure records.
    # Acceptance below always uses complete original CRLF lines and ordinal comparisons.
    $candidate = [regex]::Replace($text, '[\x00-\x20\x7f-\uffff]', '')
    if ([regex]::IsMatch($candidate, '(?i)TEST_FAIL|OS_SMOKE_FAIL|M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED|CLROS_M02_014_(?:HOST|PAGE_IMAGE_HOST|TRANSITION_HOST|EFI_HOST)_READY')) { throw 'm02_page_fault_failure_or_host_record' }
    foreach ($pair in @(@('M02_PAGE_FAULT_',9),@('CLROS_M02_014',1),@('TEST_STARTm02\.014',1),@('TEST_PASSm02\.014',1),@('OS_SMOKE_PASS',1))) {
        if ([regex]::Matches($candidate, '(?i)' + $pair[0]).Count -ne $pair[1]) { throw 'm02_page_fault_duplicate_or_disguised_record' }
    }
    $anchor = "OS_BOOT_OK`r`nTEST_PASS boot.sequence`r`nOS_SMOKE_PASS`r`n"
    $start = $text.IndexOf($anchor, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw 'm02_page_fault_smoke_terminal_missing' }
    $tail = $text.Substring($start + $anchor.Length)
    $lines = $tail.Split(@("`r`n"), [StringSplitOptions]::None)
    if ($lines.Length -lt 13) { throw 'm02_page_fault_incomplete_tail' }
    for ($i = 12; $i -lt $lines.Length; $i++) {
        if ($lines[$i].Length -ne 0) { throw 'm02_page_fault_post_pass_noise' }
    }
    if (-not [string]::Equals($lines[0], 'TEST_START m02.014', [StringComparison]::Ordinal) -or
        -not [string]::Equals($lines[10], $script:M02PageFaultCr2ErrorCodeReadyMarker, [StringComparison]::Ordinal) -or
        -not [string]::Equals($lines[11], 'TEST_PASS m02.014', [StringComparison]::Ordinal)) { throw 'm02_page_fault_tail_order' }
    $h = '(0x[0-9A-F]{16})'
    $schemas = @(
        ('M02_PAGE_FAULT_LAYOUT code='+$h+' data='+$h+' tables='+$h+' faultRip='+$h+' codePages=3 dataPages=10 tablePages=43 mappedPages=52')
        ('M02_PAGE_FAULT_MODE firmwareCs='+$h+' cr0='+$h+' firmwareCr3='+$h+' cr4Before='+$h+' efer='+$h+' pat='+$h+' physicalBits='+$h)
        ('M02_PAGE_FAULT_ROOT cr3='+$h+' expectedCr3='+$h+' cr4='+$h+' expectedCr4='+$h)
    )
    foreach ($role in @('zero','df','pf','nmi','transition')) {
        $schemas += 'M02_PAGE_FAULT_ABSENT role='+$role+' linear='+$h+' levelsRead='+$h+' pml4e='+$h+' pdpte='+$h+' pde='+$h+' pte='+$h
    }
    $schemas += 'M02_PAGE_FAULT_FRAME vector='+$h+' hasErrorCode='+$h+' error='+$h+' cr2='+$h+' rip='+$h+' cs='+$h+' rflags='+$h+' savedRsp='+$h+' savedSs='+$h+' handlerRsp='+$h+' handlerSs='+$h+' cr3='+$h
    $values = New-Object 'System.Collections.Generic.List[UInt64]'
    for ($i = 0; $i -lt $schemas.Count; $i++) {
        $match = [regex]::Match($lines[$i+1], '\A' + $schemas[$i] + '\z', [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        if (-not $match.Success) { throw ('m02_page_fault_observation_schema_' + $i) }
        for ($field = 1; $field -lt $match.Groups.Count; $field++) { $values.Add([Convert]::ToUInt64($match.Groups[$field].Value.Substring(2),16)) }
    }
    if ($values.Count -ne 57) { throw 'm02_page_fault_field_count' }
    $c=$values[0]; $d=$values[1]; $t=$values[2]; $rip=$values[3]; $bits=$values[10]
    if ($bits -lt 36 -or $bits -gt 52) { throw 'm02_page_fault_physical_width' }
    $limit=[UInt64]1 -shl [int]$bits
    $starts=@($c,$d,$t); $sizes=@([UInt64]12288,[UInt64]40960,[UInt64]176128)
    for ($i=0; $i -lt 3; $i++) {
        $a=$starts[$i]; $n=$sizes[$i]
        if ($a -eq 0 -or ($a -band 4095) -ne 0 -or $a -ge [UInt64]0x0000800000000000 -or
            $n -ge [UInt64]0x0000800000000000-$a -or $a -ge $limit -or $n -ge $limit-$a) { throw 'm02_page_fault_allocation_extent' }
        for ($j=0; $j -lt $i; $j++) { if ($a -lt $starts[$j]+$sizes[$j] -and $starts[$j] -lt $a+$n) { throw 'm02_page_fault_allocation_overlap' } }
    }
    if ($rip -ne $c+4096+2841) { throw 'm02_page_fault_fault_rip' }
    $cs=$values[4]; $cr0=$values[5]; $firmwareRoot=$values[6]; $before=$values[7]; $efer=$values[8]; $pat=$values[9]
    if ($cs -gt 65535 -or ($cs -band 7) -ne 0 -or ($cs -band 0xfff8) -eq 0 -or
        ($cr0 -band [UInt64]2147549185) -ne [UInt64]2147549185 -or ($before -band 0x20) -eq 0 -or
        ($before -band 0x01821000) -ne 0 -or ($efer -band 0xd00) -ne 0xd00 -or
        $firmwareRoot -ge $limit -or ($firmwareRoot -band 0xfe7) -ne 0 -or ($firmwareRoot -shr 12) -eq 0 -or
        ($pat -band 255) -ne 6) { throw 'm02_page_fault_mode' }
    for ($i=0; $i -lt 8; $i++) { if (@(0,1,4,5,6,7) -notcontains [int](($pat -shr (8*$i)) -band 255)) { throw 'm02_page_fault_pat' } }
    $after=$before -band (-bnot [UInt64]0x80)
    if ($values[11] -ne $t -or $values[12] -ne $t -or $values[13] -ne $after -or $values[14] -ne $after) { throw 'm02_page_fault_root' }
    $linear=@([UInt64]0,($d+0x2000),($d+0x4000),($d+0x6000),($d+0x8000))
    # The five observations share one actual root: a read slot has one structural entry.
    # Hardware may set accessed between walks; no other structural variation is accepted.
    $slots=New-Object 'System.Collections.Generic.Dictionary[string,UInt64]' ([StringComparer]::Ordinal)
    $parents=New-Object 'System.Collections.Generic.Dictionary[UInt64,string]'
    $mappedStarts=@($c,$d,($d+0x3000),($d+0x5000),($d+0x7000),($d+0x9000),$t)
    $mappedSizes=@([UInt64]12288,[UInt64]8192,[UInt64]4096,[UInt64]4096,[UInt64]4096,[UInt64]4096,[UInt64]176128)
    for ($walk=0; $walk -lt 5; $walk++) {
        $offset=15+$walk*6; $levels=$values[$offset+1]
        if ($values[$offset] -ne $linear[$walk] -or $levels -lt 1 -or $levels -gt 4) { throw 'm02_page_fault_absence_identity' }
        # An absent ancestor covers a whole virtual interval, not only the queried byte.
        # It cannot also contain any page this terminal layout necessarily executes or reads.
        $shift=12+9*(4-[int]$levels)
        $absentStart=($linear[$walk] -shr $shift) -shl $shift
        $absentEnd=$absentStart+([UInt64]1 -shl $shift)
        for ($mapped=0; $mapped -lt 7; $mapped++) {
            if ($absentStart -lt $mappedStarts[$mapped]+$mappedSizes[$mapped] -and $mappedStarts[$mapped] -lt $absentEnd) { throw 'm02_page_fault_absence_overlaps_known_mapping' }
        }
        $visited=New-Object 'System.Collections.Generic.HashSet[UInt64]'
        [void]$visited.Add($t)
        $table=$t
        for ($level=0; $level -lt 4; $level++) {
            $entry=$values[$offset+2+$level]
            if ($level -ge $levels) { if ($entry -ne 0) { throw 'm02_page_fault_nonzero_placeholder' }; continue }
            $index=($linear[$walk] -shr (39-9*$level)) -band 511
            $slot=$table.ToString('X16',[Globalization.CultureInfo]::InvariantCulture)+':'+$index.ToString([Globalization.CultureInfo]::InvariantCulture)
            $structural=$entry -band (-bnot [UInt64]0x20)
            if ($slots.ContainsKey($slot)) {
                if ($slots[$slot] -ne $structural) { throw 'm02_page_fault_inconsistent_shared_slot' }
            } else { $slots.Add($slot,$structural) }
            if ($level -eq $levels-1) { if ($entry -ne 0) { throw 'm02_page_fault_nonzero_absence' }; continue }
            # These are ancestors only: P/RW and optional accessed, never dirty/NX/PS/reserved.
            $flags=$entry -band 4095; $target=($entry -shr 12) -shl 12
            if (($flags -ne 3 -and $flags -ne 35) -or $entry -ge $limit -or $target -lt $t -or
                $target -ge $t+176128 -or -not $visited.Add($target)) { throw 'm02_page_fault_ancestor' }
            if ($parents.ContainsKey($target)) {
                if (-not [string]::Equals($parents[$target],$slot,[StringComparison]::Ordinal)) { throw 'm02_page_fault_aliased_child_table' }
            } else { $parents.Add($target,$slot) }
            $table=$target
        }
    }
    $expected=@([UInt64]14,[UInt64]1,[UInt64]0,[UInt64]0,$rip,$cs,[UInt64]0x10046,($d+0xa000),[UInt64]0x10,($d+0x6000-192),[UInt64]0,$t)
    for ($i=0; $i -lt 12; $i++) { if ($values[45+$i] -ne $expected[$i]) { throw ('m02_page_fault_frame_' + $i) } }
}

function Assert-M04ManagedHeapPageRangeTranscript {
    param([string] $SerialLogPath, [switch] $IncludeFixedAllocation)
    $lineCount = if ($IncludeFixedAllocation) { 78 } else { 52 }
    $path = if ([IO.Path]::IsPathRooted($SerialLogPath)) { $SerialLogPath } else { Join-Path $root $SerialLogPath }
    # No BOM sniffing, trimming, Unicode folding or control removal for acceptance.
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path))
    # Scan the whole capture as well: line breaks cannot disguise a failure
    # in firmware output before the first accepted CLROS record.
    $captureCandidate = [regex]::Replace($text, '[\x00-\x20\x7f-\uffff]', '')
    if ([regex]::IsMatch($captureCandidate, '(?i)TEST_FAIL|OS_SMOKE_FAIL|M04_.*FAILED|BOOT_PAGE_ALLOCATOR_NOT_READY|UEFI_ACPI_RSDP_(?:MISSING|INVALID)')) {
        throw 'm04_heap_failure_record'
    }
    $raw = $text.Split([char]10)
    $captured = New-Object 'System.Collections.Generic.List[string]'
    $preambleCandidate = New-Object Text.StringBuilder
    $started = $false
    for ($i = 0; $i -lt $raw.Length; $i++) {
        $complete = $i -lt $raw.Length - 1 -and $raw[$i].EndsWith("`r", [StringComparison]::Ordinal)
        $line = $raw[$i]
        if ($complete) { $line = $line.Substring(0, $line.Length - 1) }
        # Classification alone strips controls/space to expose disguised records.
        $candidate = [regex]::Replace($line, '[\x00-\x20\x7f-\uffff]', '')
        if ([regex]::IsMatch($candidate, '(?i)TEST_FAIL|OS_SMOKE_FAIL|M04_.*FAILED|BOOT_PAGE_ALLOCATOR_NOT_READY|UEFI_ACPI_RSDP_(?:MISSING|INVALID)')) {
            throw 'm04_heap_failure_record'
        }
        if (-not $started) {
            if ([string]::Equals($line, 'TEST_START efi.main', [StringComparison]::Ordinal) -and $complete) { $started = $true }
            else {
                [void]$preambleCandidate.Append($candidate)
                if ([regex]::IsMatch($preambleCandidate.ToString(), '(?i)CLROS_|M01_|M02_|M04_|TEST_|OS_|EFI_|UEFI_|BOOT_')) { throw 'm04_heap_disguised_preamble_record' }
                continue
            }
        }
        if ($captured.Count -ge $lineCount) {
            if (($complete -and $line.Length -eq 0) -or ($i -eq $raw.Length - 1 -and $line.Length -eq 0)) { continue }
            throw 'm04_heap_post_terminal_noise'
        }
        if (-not $complete -or [regex]::IsMatch($line, '[\x00-\x1f\x7f-\uffff]')) { throw 'm04_heap_incomplete_or_control_record' }
        $captured.Add($line)
    }
    if ($captured.Count -ne $lineCount) { throw 'm04_heap_incomplete_sequence' }
    $rhpRecords = @()
    if ($IncludeFixedAllocation) {
        # The exact new 26-line block occurs after the zero checkpoint and before boot.sequence.
        $rhpRecords = @($captured.GetRange(44,26))
        $captured.RemoveRange(44,26)
    }
    $expected = @(
        'TEST_START efi.main'
        $script:M04LayoutContractRequiredMarkers
        'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT'
        'UEFI_MEMORY_MAP_OWNED'
        'BOOT_HANDOFF_READY'
        'UEFI_MEMORY_MAP_NORMALIZED'
        'BOOT_MEMORY_MAP_ACCEPTED'
        'BOOT_MEMORY_REGIONS_READY'
        'BOOT_MEMORY_REGION_COUNT'
        'BOOT_PAGE_ALLOCATOR_READY'
        'BOOT_ACPI_RSDP_HANDOFF_READY'
        'UEFI_ACPI_RSDP_READY'
        'TEST_START m01.stage1'
        'M01_LOADED_IMAGE_CAPTURED'
        'M01_STACK_SWITCH_STUB_READY'
        'M01_STACK_ARENA_ALLOCATED'
        'M01_STACK_GUARD_RESERVED'
        'M01_STAGE1_MAP_RECAPTURED'
        'M01_STAGE1_RESERVATIONS_VALIDATED'
        'M01_OWNED_STACK_READY'
        'M01_OWNED_STACK_SWITCHED'
        'M01_RSP_CONTAINED'
        'TEST_PASS m01.stage1'
        'TEST_START m01.stage2'
        'M01_ALLOCATOR_ARENA_ALLOCATED'
        'M01_STAGE2_MAP_RECAPTURED'
        'M01_RESERVATIONS_APPLIED'
        'M01_LIVE_ALLOCATED_4K'
        'M01_LIVE_PAGE_WITHIN_ARENA'
        'M01_LIVE_FREED_4K'
        'M01_DOUBLE_FREE_REJECTED'
        'TEST_PASS m01.stage2'
        'TEST_START m04.006'
        'M04_HEAP_PAGE_BOUND'
        'M04_HEAP_PAGE_INITIALIZED'
        'M04_HEAP_PAGE_RETAINED'
        'M04_HEAP_PAGE_CURRENT'
        'CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail'
        'TEST_PASS m04.006'
        'TEST_START boot.sequence'
        'BOOT_PHASE PhaseA.Audit START'
        'BOOT_PHASE PhaseA.Audit PASS'
        'BOOT_PHASE PhaseA.Diagnostics START'
        'BOOT_PHASE PhaseA.Diagnostics PASS'
        'OS_BOOT_OK'
        'TEST_PASS boot.sequence'
        'OS_SMOKE_PASS'
    )
    $values = @{}
    $decimal = '(0|[1-9][0-9]*)'
    $hex = '(0x[0-9A-F]{16})'
    $identityFields = @('allocatorState','nodes','heapState','ledger','parentIndex','parentStart','parentEnd','backing','retainedStart','retainedEnd')
    $numericFields = @{
        'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT' = @('descriptorCount')
        'BOOT_MEMORY_REGION_COUNT' = @('regionCount')
        'BOOT_PAGE_ALLOCATOR_READY' = @('regions','conventionalPages','conventionalBytes','freeNodes','freePages','freeBytes')
        'M04_HEAP_PAGE_BOUND' = $identityFields
        'M04_HEAP_PAGE_RETAINED' = $identityFields
        'M04_HEAP_PAGE_INITIALIZED' = @('pages','bytes','freePagesBefore','freeBytesBefore','allocatedPagesBefore','allocatedBytesBefore','freePagesAfter','freeBytesAfter','allocatedPagesAfter','allocatedBytesAfter','readBytes')
        'M04_HEAP_PAGE_CURRENT' = @('freePages','freeBytes','allocatedPages','allocatedBytes','readBytes')
    }
    for ($i = 0; $i -lt $expected.Count; $i++) {
        $key = $expected[$i]
        if (-not $numericFields.ContainsKey($key)) {
            if (-not [string]::Equals($captured[$i], $key, [StringComparison]::Ordinal)) { throw ('m04_heap_exact_sequence_' + $i) }
            continue
        }
        $pattern = '\A' + [regex]::Escape($key)
        if ($key -eq 'M04_HEAP_PAGE_RETAINED') { $pattern += ' checkpoint=beforeBootPlan' }
        if ($key -eq 'BOOT_PAGE_ALLOCATOR_READY') { $pattern += ' status=Ready' }
        foreach ($field in $numericFields[$key]) {
            if ($key -eq 'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT' -or $key -eq 'BOOT_MEMORY_REGION_COUNT') { $pattern += ' ' + $decimal }
            else {
                $pattern += ' ' + $field + '='
                if (($key -eq 'M04_HEAP_PAGE_BOUND' -or $key -eq 'M04_HEAP_PAGE_RETAINED') -and $field -ne 'parentIndex') { $pattern += $hex }
                else { $pattern += $decimal }
            }
        }
        if ($key -eq 'BOOT_PAGE_ALLOCATOR_READY') { $pattern += ' pageSize=4096 statusCode=0' }
        if ($key -eq 'M04_HEAP_PAGE_INITIALIZED' -or $key -eq 'M04_HEAP_PAGE_CURRENT') { $pattern += ' contentsZero=true' }
        $match = [regex]::Match($captured[$i], $pattern + '\z', [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        if (-not $match.Success) { throw ('m04_heap_numeric_shape_' + $key) }
        $record = @{}
        for ($j = 0; $j -lt $numericFields[$key].Count; $j++) {
            $token = $match.Groups[$j + 1].Value
            $number = [UInt64]0
            $style = [Globalization.NumberStyles]::None
            if ($token.StartsWith('0x', [StringComparison]::Ordinal)) { $token = $token.Substring(2); $style = [Globalization.NumberStyles]::AllowHexSpecifier }
            if (-not [UInt64]::TryParse($token, $style, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) { throw 'm04_heap_numeric_overflow' }
            $record[$numericFields[$key][$j]] = $number
        }
        $values[$key] = $record
    }
    $map = $values['BOOT_PAGE_ALLOCATOR_READY']
    if ($map.regions -lt 1 -or $map.regions -gt 4096 -or
        $map.regions -ne $values['UEFI_MEMORY_MAP_DESCRIPTOR_COUNT'].descriptorCount -or
        $map.regions -ne $values['BOOT_MEMORY_REGION_COUNT'].regionCount -or
        $map.freeNodes -lt 1 -or $map.freeNodes -gt $map.regions -or
        $map.freePages -lt $map.freeNodes -or $map.freePages -ne $map.conventionalPages -or
        [decimal]$map.freePages * 4096 -gt [decimal][UInt64]::MaxValue -or
        [decimal]$map.freePages * 4096 -ne [decimal]$map.freeBytes -or $map.freeBytes -ne $map.conventionalBytes) { throw 'm04_heap_map_accounting' }
    $bound = $values['M04_HEAP_PAGE_BOUND']; $retained = $values['M04_HEAP_PAGE_RETAINED']
    foreach ($field in $identityFields) { if ($bound[$field] -ne $retained[$field]) { throw 'm04_heap_retained_identity' } }
    if ($bound.parentIndex -gt 4 -or $bound.parentStart -eq 0 -or $bound.parentStart % 4096 -ne 0 -or
        [decimal]$bound.parentStart + 8192 -gt [decimal][UInt64]::MaxValue -or
        [decimal]$bound.parentStart + 8192 -ne [decimal]$bound.parentEnd -or
        $bound.backing -ne $bound.parentStart -or $bound.retainedStart -ne $bound.parentStart -or
        [decimal]$bound.retainedStart + 4096 -ne [decimal]$bound.retainedEnd) { throw 'm04_heap_parent_interval' }
    $extents = @()
    foreach ($entry in @(@('allocatorState',56),@('nodes',48),@('ledger',200),@('heapState',216))) {
        $start = $bound[$entry[0]]; $end = [decimal]$start + $entry[1]
        if ($start -eq 0 -or $start % 8 -ne 0 -or $end -gt [decimal][UInt64]::MaxValue -or
            ([decimal]$start -lt [decimal]$bound.parentEnd -and [decimal]$bound.parentStart -lt $end)) { throw 'm04_heap_metadata_extent' }
        foreach ($extent in $extents) { if ([decimal]$start -lt $extent.End -and $extent.Start -lt $end) { throw 'm04_heap_metadata_alias' } }
        $extents += [pscustomobject]@{ Start=[decimal]$start; End=$end }
    }
    $initial = $values['M04_HEAP_PAGE_INITIALIZED']; $current = $values['M04_HEAP_PAGE_CURRENT']
    $initialExpected = @{ pages=1; bytes=4096; freePagesBefore=2; freeBytesBefore=8192; allocatedPagesBefore=0; allocatedBytesBefore=0; freePagesAfter=1; freeBytesAfter=4096; allocatedPagesAfter=1; allocatedBytesAfter=4096; readBytes=4096 }
    $currentExpected = @{ freePages=1; freeBytes=4096; allocatedPages=1; allocatedBytes=4096; readBytes=4096 }
    foreach ($field in $initialExpected.Keys) { if ($initial[$field] -ne $initialExpected[$field]) { throw 'm04_heap_initial_accounting' } }
    foreach ($field in $currentExpected.Keys) { if ($current[$field] -ne $currentExpected[$field]) { throw 'm04_heap_current_accounting' } }
    if ($IncludeFixedAllocation) { Assert-M04RhpNewFastRecords -Lines $rhpRecords -Heap $retained }
}

function Assert-M04RhpNewFastTranscript {
    param([string] $SerialLogPath)
    Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $SerialLogPath -IncludeFixedAllocation
}

function Read-M04RhpNewFastRecord {
    param([string] $Line, [string] $Prefix, [string[]] $Fields)
    $pattern = '\A' + [regex]::Escape($Prefix)
    foreach ($field in $Fields) {
        $parts=$field.Split(':'); $pattern += ' ' + [regex]::Escape($parts[0]) + '='
        if ([string]::Equals($parts[1],'hex',[StringComparison]::Ordinal)) { $pattern += '(0x[0-9A-F]{16})' }
        elseif ([string]::Equals($parts[1],'false',[StringComparison]::Ordinal)) { $pattern += '(false)' }
        else { $pattern += '(0|[1-9][0-9]*)' }
    }
    $match=[regex]::Match($Line,$pattern+'\z',[Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) { throw ('m04_rhp_record_shape_' + $Prefix) }
    $result=@{}
    for ($i=0; $i -lt $Fields.Count; $i++) {
        $parts=$Fields[$i].Split(':');$token=$match.Groups[$i+1].Value
        if ([string]::Equals($parts[1],'false',[StringComparison]::Ordinal)) { $result[$parts[0]]=$false;continue }
        $style=[Globalization.NumberStyles]::None
        if ([string]::Equals($parts[1],'hex',[StringComparison]::Ordinal)) { $token=$token.Substring(2);$style=[Globalization.NumberStyles]::AllowHexSpecifier }
        $number=[UInt64]0
        if (-not [UInt64]::TryParse($token,$style,[Globalization.CultureInfo]::InvariantCulture,[ref]$number)) { throw 'm04_rhp_numeric_overflow' }
        $result[$parts[0]]=$number
    }
    return $result
}

function Assert-M04RhpNewFastRecords {
    param([string[]] $Lines, [hashtable] $Heap)
    if ($Lines.Count -ne 26 -or
        -not [string]::Equals($Lines[0],'TEST_START m04.007.rhp_new_fast',[StringComparison]::Ordinal) -or
        -not [string]::Equals($Lines[24],$script:M04RhpNewFastReadyMarker,[StringComparison]::Ordinal) -or
        -not [string]::Equals($Lines[25],'TEST_PASS m04.007.rhp_new_fast',[StringComparison]::Ordinal)) { throw 'm04_rhp_exact_terminal' }
    $bind=Read-M04RhpNewFastRecord $Lines[2] 'M04_RHP_BIND' @('image:hex','imageBytes:dec','allocator:hex','nodes:hex','ledger:hex','ledgerBytes:dec','parentIndex:dec','backing:hex','backingBytes:dec','heap:hex','heapBytes:dec','access:hex','accessBytes:dec','bump:hex','bumpBytes:dec','status:dec','used:dec','count:dec')
    foreach ($pair in @(@('allocator','allocatorState'),@('nodes','nodes'),@('ledger','ledger'),@('parentIndex','parentIndex'),@('backing','backing'),@('heap','heapState'))) {
        if ($bind[$pair[0]] -ne $Heap[$pair[1]]) { throw 'm04_rhp_original_identity' }
    }
    if ($bind.image -eq 0 -or $bind.imageBytes -lt 64 -or [decimal]$bind.image + $bind.imageBytes -gt [decimal][UInt64]::MaxValue -or
        $bind.ledgerBytes -ne 200 -or $bind.backingBytes -ne 8192 -or $bind.heapBytes -ne 216 -or
        $bind.accessBytes -ne 104 -or $bind.bumpBytes -ne 80 -or $bind.status -ne 1 -or $bind.used -ne 0 -or $bind.count -ne 0) { throw 'm04_rhp_bind_contract' }
    $prep=Read-M04RhpNewFastRecord $Lines[1] 'M04_RHP_PREP' @('image:hex','imageBytes:dec','access:hex','accessBytes:dec','bump:hex','bumpBytes:dec','control:hex','controlBytes:dec','records:hex','recordsBytes:dec')
    foreach ($name in @('image','imageBytes','access','accessBytes','bump','bumpBytes')) {
        if ($prep[$name] -ne $bind[$name]) { throw 'm04_rhp_preparation_identity' }
    }
    if ($prep.controlBytes -ne 96 -or $prep.recordsBytes -ne 5872) { throw 'm04_rhp_preparation_size' }
    $storage=@{};foreach($key in $bind.Keys){$storage[$key]=$bind[$key]}
    $storage['control']=$prep.control;$storage['records']=$prep.records
    $extents=@()
    foreach ($slot in @(@('allocator',56),@('nodes',48),@('ledger',200),@('heap',216),@('backing',8192),@('access',104),@('bump',80),@('control',96),@('records',5872))) {
        $start=[decimal]$storage[$slot[0]];$end=$start+$slot[1]
        if ($start -eq 0 -or $start % 8 -ne 0 -or $end -gt [decimal][UInt64]::MaxValue) { throw 'm04_rhp_storage_extent' }
        foreach ($previous in $extents) { if ($start -lt $previous.End -and $previous.Start -lt $end) { throw 'm04_rhp_storage_alias' } }
        $extents += [pscustomobject]@{Start=$start;End=$end}
    }
    # Pinned integrated type shapes. These scalar expectations do not replace the offline
    # same-image COFF/PE/relocation oracle for the three captured raw prefix words.
    $sizes=@(24,32,32,32,24,24,24,32,24,24)
    $flags=@([UInt64]0x50000000,[UInt64]0x51000000,[UInt64]0x53000000,[UInt64]0x52000000,[UInt64]0x20000080,[UInt64]0x20000080,[UInt64]0x40000000,[UInt64]0x41000000,[UInt64]0x43000000,[UInt64]0x4A000200)
    $scalars=@([UInt64]0x1111111111111111,[UInt64]0x2222222222222222,[UInt64]0x3333333333333333,[UInt64]0x4444444444444444)
    $tokens=New-Object 'System.Collections.Generic.HashSet[UInt64]';$used=[UInt64]0
    for ($id=1;$id -le 10;$id++) {
        $route=if($id -le 4){'new'}else{'direct'}
        $o=Read-M04RhpNewFastRecord $Lines[$id*2+1] ("M04_RHP_OBJECT id=$id route=$route") @('mt:hex','prefix0:hex','prefix1:hex','prefix2:hex','flags:hex','base:dec','allocation:hex','reference:hex','end:hex','usedBefore:dec','usedAfter:dec','count:dec','sync:hex','observedMt:hex','zeroBytes:dec','zeroOr:hex')
        $end=[decimal]$bind.backing+$used+$sizes[$id-1]
        if ($o.mt -eq 0 -or $o.mt % 8 -ne 0 -or -not $tokens.Add($o.mt) -or $o.mt -lt $bind.image -or
            [decimal]$o.mt+24 -gt [decimal]$bind.image+$bind.imageBytes -or
            $o.flags -ne $flags[$id-1] -or ($o.prefix0 -band [UInt64]4294967295) -ne $o.flags -or
            ($o.prefix0 -shr 32) -ne $sizes[$id-1] -or $o.base -ne $sizes[$id-1] -or
            [decimal]$o.allocation -ne [decimal]$bind.backing+$used -or [decimal]$o.reference -ne [decimal]$o.allocation+8 -or
            [decimal]$o.end -ne $end -or $o.usedBefore -ne $used -or $o.usedAfter -ne $used+$o.base -or
            $o.count -ne $id -or $o.sync -ne 0 -or $o.observedMt -ne $o.mt -or $o.zeroBytes -ne $o.base-8 -or $o.zeroOr -ne 0) { throw ('m04_rhp_object_' + $id) }
        foreach ($slot in $extents) {
            if ([decimal]$o.mt -lt $slot.End -and $slot.Start -lt [decimal]$o.mt+24) { throw 'm04_rhp_metadata_alias' }
        }
        $read=Read-M04RhpNewFastRecord $Lines[$id*2+2] ("M04_RHP_READBACK id=$id") @('scalarOffset:dec','scalar:hex','referenceSlotBytes:dec','referenceSlotOr:hex','priorCount:dec','priorMismatchBytes:dec','canaryMismatchBytes:dec')
        $offset=if($id -eq 1){8}elseif($id -le 4){16}else{0}
        $scalar=if($id -le 4){$scalars[$id-1]}else{[UInt64]0}
        $untouched=$o.base-16;if($id -le 4){$untouched-=8}
        if ($read.scalarOffset -ne $offset -or $read.scalar -ne $scalar -or $read.referenceSlotBytes -ne $untouched -or
            $read.referenceSlotOr -ne 0 -or $read.priorCount -ne $id -or $read.priorMismatchBytes -ne 0 -or $read.canaryMismatchBytes -ne 0) { throw ('m04_rhp_readback_' + $id) }
        $used=$o.usedAfter
    }
    $retained=Read-M04RhpNewFastRecord $Lines[23] 'M04_RHP_RETAIN' @('allocator:hex','nodes:hex','ledger:hex','parentIndex:dec','backing:hex','heap:hex','bump:hex','heapStart:hex','heapEnd:hex','initialPages:dec','freePages:dec','allocatedPages:dec','readBytes:dec','contentsZero:false','used:dec','count:dec','status:dec')
    foreach ($name in @('allocator','nodes','ledger','parentIndex','backing','heap','bump')) {
        if ($retained[$name] -ne $bind[$name]) { throw 'm04_rhp_retained_identity' }
    }
    if ($retained.heapStart -ne $Heap.retainedStart -or $retained.heapEnd -ne $Heap.retainedEnd -or
        $retained.initialPages -ne 2 -or $retained.freePages -ne 1 -or $retained.allocatedPages -ne 1 -or $retained.readBytes -ne 4096 -or
        $retained.used -ne $used -or $retained.count -ne 10 -or $retained.status -ne 1) { throw 'm04_rhp_retained_accounting' }
}

function Assert-M04ManagedHeapRunnerResult {
    param([string[]] $Lines)
    $results = @($Lines | Where-Object { $_.StartsWith('QEMU_PROCESS_RESULT ', [StringComparison]::Ordinal) })
    $drains = @($Lines | Where-Object { $_.StartsWith('SERIAL_MARKER_DRAIN ', [StringComparison]::Ordinal) })
    if ($results.Count -ne 1 -or $drains.Count -ne 1 -or
        -not [string]::Equals($drains[0], 'SERIAL_MARKER_DRAIN milliseconds=750', [StringComparison]::Ordinal)) { throw 'm04_heap_terminal_disposition' }
    $match = [regex]::Match($results[0], '\AQEMU_PROCESS_RESULT disposition=runner-terminated exitCode=(0|-?[1-9][0-9]*)\z')
    $nativeExitCode = [Int32]0
    if (-not $match.Success -or -not [Int32]::TryParse($match.Groups[1].Value,
            [Globalization.NumberStyles]::AllowLeadingSign,
            [Globalization.CultureInfo]::InvariantCulture, [ref]$nativeExitCode)) { throw 'm04_heap_native_exit_invalid' }
}

function Assert-M02PageFaultRunnerResult {
    param([string[]] $Lines)
    $results = @($Lines | Where-Object { $_.StartsWith('QEMU_PROCESS_RESULT ', [StringComparison]::Ordinal) })
    $drains = @($Lines | Where-Object { $_.StartsWith('SERIAL_MARKER_DRAIN ', [StringComparison]::Ordinal) })
    if ($results.Count -ne 1 -or $drains.Count -ne 1 -or
        -not [string]::Equals($drains[0], 'SERIAL_MARKER_DRAIN milliseconds=750', [StringComparison]::Ordinal)) { throw 'm02_page_fault_terminal_disposition' }
    $match = [regex]::Match($results[0], '\AQEMU_PROCESS_RESULT disposition=runner-terminated exitCode=(0|-?[1-9][0-9]*)\z')
    $nativeExitCode = [Int32]0
    if (-not $match.Success -or -not [Int32]::TryParse($match.Groups[1].Value,
            [Globalization.NumberStyles]::AllowLeadingSign,
            [Globalization.CultureInfo]::InvariantCulture, [ref]$nativeExitCode)) { throw 'm02_page_fault_native_exit_invalid' }
}

function Assert-M04LayoutContractTranscript {
    param([string] $SerialLogPath)

    $resolvedPath = if ([System.IO.Path]::IsPathRooted($SerialLogPath)) {
        [System.IO.Path]::GetFullPath($SerialLogPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $root $SerialLogPath))
    }
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "m04_layout_contract_serial_log_missing"
    }

    $expected = @($script:M04LayoutContractRequiredMarkers) + @("OS_SMOKE_PASS")
    $next = 0
    # Decode the capture without BOM sniffing/removal: a BOM on a CLROS
    # record is extra content, not a license to normalize that record.
    $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($resolvedPath))
    $lines = $text.Split([char]10)
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        # Remove only the CR in a complete CRLF delimiter. Nothing else is
        # trimmed or normalized for acceptance, including a final lone CR.
        $complete = $index -lt $lines.Length - 1
        if ($complete -and $line.EndsWith("`r", [System.StringComparison]::Ordinal)) {
            $line = $line.Substring(0, $line.Length - 1)
        }

        # Classification removes whitespace/control/non-ASCII solely to spot
        # disguised records. Equality below always uses the unmodified line.
        # Firmware ANSI display output before/after CLROS is not itself a fail.
        $candidate = [System.Text.RegularExpressions.Regex]::Replace($line, '[\x00-\x20\x7f-\uffff]', '')
        foreach ($failure in $script:M04LayoutContractForbiddenMarkers) {
            $failureKey = $failure.Replace(' ', '')
            if ($candidate.IndexOf($failureKey, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "m04_layout_contract_failure_record"
            }
        }
        $isM04 = $candidate.IndexOf("CLROS_M04", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $candidate.IndexOf("M04_", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        $isBoundary = $false
        foreach ($boundary in @("EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "OS_SMOKE_PASS")) {
            if ($candidate.IndexOf($boundary, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $isBoundary = $true
            }
        }
        $hasControl = [System.Text.RegularExpressions.Regex]::IsMatch($line, '[\x00-\x1f\x7f-\uffff]')
        if ($hasControl) {
            foreach ($prefix in @("CLROS_", "M01_", "M02_", "M04_", "TEST_", "OS_", "EFI_", "UEFI_", "BOOT_")) {
                if ($candidate.IndexOf($prefix, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "m04_layout_contract_control_in_record"
                }
            }
        }
        if ($isM04 -or $isBoundary) {
            if (-not $complete -or $next -ge $expected.Count -or
                -not [string]::Equals($line, $expected[$next], [System.StringComparison]::Ordinal)) {
                throw "m04_layout_contract_exact_sequence_invalid"
            }
            $next++
        }
    }
    if ($next -ne $expected.Count) {
        throw "m04_layout_contract_exact_sequence_incomplete"
    }
}

function Merge-QemuSmokeMarkerList {
    param(
        [AllowNull()] [string[]] $Values
    )

    $markers = New-Object System.Collections.Generic.List[string]
    $seen = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::Ordinal)
    foreach ($markerValue in $Values) {
        if ([string]::IsNullOrWhiteSpace($markerValue)) {
            continue
        }

        $normalized = $markerValue.Trim()
        if ($seen.Add($normalized)) {
            $markers.Add($normalized)
        }
    }

    return $markers.ToArray()
}

function Format-QemuSmokeMarkerList {
    param(
        [AllowNull()] [string[]] $Markers
    )

    if ($null -eq $Markers -or $Markers.Count -eq 0) {
        return "none"
    }

    return $Markers -join ";"
}

function Format-QemuSmokeBoolean {
    param([bool] $Value)

    if ($Value) {
        return "true"
    }

    return "false"
}

function Get-M01RawObservationShimSource {
    return @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private const int GuestMemoryBytes = 256 * 1024 * 1024;
    private const int CaptureChunkBytes = 16 * 1024 * 1024;
    private const int ScanCarryBytes = 1024;
    private const string SuccessTerminal = "OS_SMOKE_PASS";

    private static readonly string[] GuestMarkers =
    {
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
    };

    private static readonly string[] M02GuestMarkers =
    {
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
        "TEST_START m02.017",
        "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56",
        "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=982E5B7E3EE58E56",
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
        "M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B",
        "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56 temporaryOwnedRootRx=true portIoExecuted=false",
        "TEST_PASS m02.017",
        "TEST_PASS m01.stage3",
        "CLROS_M01_LIVE_BOOT_MEMORY_READY",
        "OS_SMOKE_PASS"
    };

    private static readonly string[] ForbiddenMarkers =
    {
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
    };

    private static readonly string[] M02ForbiddenMarkers =
    {
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
        "M01_STAGE3_FAIL",
        "M02_STUB_PAGE_MATERIALIZATION_FAILED",
        "M02_STUB_PAGE_READBACK_FAILED",
        "M02_STUB_PAGE_HASH_MISMATCH",
        "M02_SAFE_PROBE_FAILED",
        "M02_OUT8_EXECUTED",
        "M02_IN8_EXECUTED",
        "M02_PORT_IO_ATTEMPTED",
        "M02_PRIVILEGED_STUB_TARGETED",
        "TEST_FAIL m02.017"
    };

    private static readonly object LogLock = new object();
    private static StreamWriter? s_log;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Log("M01_RAW_OBSERVATION_SHIM_FAIL reason=" + Sanitize(exception.Message));
            Console.Error.WriteLine(
                "M01_RAW_OBSERVATION_SHIM_FAIL reason="
                + Sanitize(exception.Message));
            return 90;
        }
        finally
        {
            if (s_log is not null)
            {
                s_log.Flush();
                s_log.Dispose();
            }
        }
    }

    private static int Run(string[] args)
    {
        string realQemu = RequiredEnvironment("CLROS_M01_REAL_QEMU");
        string privateSerial = FullPath(
            RequiredEnvironment("CLROS_M01_PRIVATE_SERIAL"));
        string observationLog = FullPath(
            RequiredEnvironment("CLROS_M01_RAW_OBSERVATION_LOG"));
        string qmpLog = FullPath(
            RequiredEnvironment("CLROS_M01_QMP_TRANSCRIPT_LOG"));
        string scratchDirectory = FullPath(
            RequiredEnvironment("CLROS_M01_CAPTURE_SCRATCH"));
        int guestTimeoutSeconds = RequiredPositiveInt(
            "CLROS_M01_GUEST_TIMEOUT_SECONDS");
        int captureTimeoutSeconds = RequiredPositiveInt(
            "CLROS_M01_CAPTURE_TIMEOUT_SECONDS");
        bool m02SafeStubEnabled = RequiredBoolean(
            "CLROS_M02_SAFE_STUB_PROBE");

        RequireCreateNewPath(privateSerial, "private_serial");
        RequireCreateNewPath(observationLog, "observation_log");
        RequireCreateNewPath(qmpLog, "qmp_log");
        if (Directory.Exists(scratchDirectory)
            || File.Exists(scratchDirectory))
        {
            throw new InvalidOperationException("scratch_path_exists");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(qmpLog)
                ?? throw new InvalidOperationException("qmp_log_parent_missing"));
        {
            FileStream logStream = new FileStream(
                qmpLog,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            s_log = new StreamWriter(
                logStream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: false)
            {
                AutoFlush = true
            };

            List<string> childArguments = new List<string>(args);
            string publicSerial = ReplaceAndValidateArguments(
                childArguments,
                privateSerial);
            RequireDistinctPaths(
                publicSerial,
                privateSerial,
                observationLog,
                qmpLog,
                scratchDirectory);

            Directory.CreateDirectory(
                Path.GetDirectoryName(publicSerial)
                    ?? throw new InvalidOperationException(
                        "public_serial_parent_missing"));
            Directory.CreateDirectory(
                Path.GetDirectoryName(privateSerial)
                    ?? throw new InvalidOperationException(
                        "private_serial_parent_missing"));
            Directory.CreateDirectory(
                Path.GetDirectoryName(observationLog)
                    ?? throw new InvalidOperationException(
                        "observation_parent_missing"));
            Directory.CreateDirectory(scratchDirectory);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            Process? child = null;
            bool childStarted = false;
            try
            {
                listener.Start(1);
                int qmpPort =
                    ((IPEndPoint)listener.LocalEndpoint).Port;
                childArguments.Add("-qmp");
                childArguments.Add(
                    "tcp:127.0.0.1:"
                    + qmpPort.ToString(CultureInfo.InvariantCulture));

                Log("schema=clros.m01.qmp-transcript.v1");
                Log("qmpTransport=outbound-loopback");
                Log("qmpServerMode=false");
                Log("guestMemoryBytes=" + GuestMemoryBytes);
                Log("captureChunkBytes=" + CaptureChunkBytes);
                Log("realQemuBase64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(realQemu)));
                for (int index = 0; index < childArguments.Count; index++)
                {
                    Log(
                        "childArg"
                        + index.ToString("D3", CultureInfo.InvariantCulture)
                        + "Base64="
                        + Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(childArguments[index])));
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(realQemu)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                child = new Process { StartInfo = startInfo };
                if (!child.Start())
                {
                    throw new InvalidOperationException(
                        "qemu_process_start_failed");
                }
                childStarted = true;

                Task<TcpClient> acceptTask =
                    listener.AcceptTcpClientAsync();
                if (!acceptTask.Wait(
                        TimeSpan.FromSeconds(
                            Math.Min(guestTimeoutSeconds, 15))))
                {
                    throw new TimeoutException("qmp_connect_timeout");
                }

                using TcpClient qmpSocket = acceptTask.Result;
                listener.Stop();
                using QmpConnection qmp = new QmpConnection(
                    qmpSocket,
                    captureTimeoutSeconds,
                    Log);
                qmp.Initialize();
                qmp.RequireMemoryShape();

                using FileStream publicStream = new FileStream(
                    publicSerial,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.WriteThrough);
                SerialTerminalGate gate = new SerialTerminalGate(
                    publicStream,
                    m02SafeStubEnabled ? M02GuestMarkers : GuestMarkers,
                    m02SafeStubEnabled
                        ? M02ForbiddenMarkers
                        : ForbiddenMarkers);
                long privateOffset = 0;
                DateTime guestDeadline =
                    DateTime.UtcNow.AddSeconds(guestTimeoutSeconds);

                while (!gate.TerminalWithheld)
                {
                    privateOffset = DrainPrivateSerial(
                        privateSerial,
                        privateOffset,
                        gate);
                    if (gate.TerminalWithheld)
                    {
                        break;
                    }

                    if (child.HasExited)
                    {
                        privateOffset = DrainPrivateSerial(
                            privateSerial,
                            privateOffset,
                            gate);
                        throw new InvalidOperationException(
                            "qemu_exited_before_terminal_"
                            + child.ExitCode.ToString(
                                CultureInfo.InvariantCulture));
                    }

                    if (DateTime.UtcNow >= guestDeadline)
                    {
                        throw new TimeoutException(
                            "guest_terminal_timeout");
                    }

                    Thread.Sleep(20);
                }

                gate.ValidateCompleteMarkerContract();
                qmp.StopAndRequirePaused();

                long previousOffset = -1;
                for (int stablePass = 0; stablePass < 3;)
                {
                    privateOffset = DrainPrivateSerial(
                        privateSerial,
                        privateOffset,
                        gate);
                    if (privateOffset == previousOffset)
                    {
                        stablePass++;
                    }
                    else
                    {
                        stablePass = 0;
                        previousOffset = privateOffset;
                    }
                    Thread.Sleep(20);
                }
                gate.CompleteAfterStop();

                publicStream.Flush(flushToDisk: true);
                byte[] withheldBytes = gate.GetWithheldBytes();
                string privateSerialHash = Sha256File(privateSerial);
                string predictedPublicHash = Sha256FilePlusBytes(
                    publicSerial,
                    withheldBytes);
                if (!string.Equals(
                        privateSerialHash,
                        predictedPublicHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "serial_prediction_hash_mismatch");
                }

                ObservationScanner.ObservationEvidence evidence =
                    ObservationScanner.Capture(
                        qmp,
                        scratchDirectory,
                        captureTimeoutSeconds);
                DeleteEmptyCaptureScratch(scratchDirectory);
                WriteObservationSidecar(
                    observationLog,
                    evidence,
                    privateSerialHash,
                    predictedPublicHash,
                    guestTimeoutSeconds,
                    captureTimeoutSeconds);

                Log("M01_RAW_OBSERVATION_CAPTURED");
                Log("publicSerialSha256=" + predictedPublicHash);
                Log("privateSerialSha256=" + privateSerialHash);
                Log("terminalReleaseAuthorized=true");

                publicStream.Position = publicStream.Length;
                publicStream.Write(
                    withheldBytes,
                    0,
                    withheldBytes.Length);
                publicStream.Flush(flushToDisk: true);
                string actualPublicHash = Sha256File(publicSerial);
                if (!string.Equals(
                        actualPublicHash,
                        predictedPublicHash,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "released_public_serial_hash_mismatch");
                }

                Log("terminalReleased=true");

                while (true)
                {
                    if (child.HasExited)
                    {
                        return child.ExitCode;
                    }
                    Thread.Sleep(1000);
                }
            }
            catch
            {
                if (child is not null && childStarted)
                {
                    try
                    {
                        if (!child.HasExited)
                        {
                            child.Kill(entireProcessTree: true);
                            child.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                    }
                }
                throw;
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
                child?.Dispose();
                TryDeleteCaptureScratch(scratchDirectory);
            }
        }
    }

    private static string ReplaceAndValidateArguments(
        List<string> arguments,
        string privateSerial)
    {
        int serialIndex = -1;
        int memoryIndex = -1;
        int machineIndex = -1;
        int displayIndex = -1;
        int noRebootCount = 0;
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (string.Equals(argument, "-serial", StringComparison.Ordinal))
            {
                if (serialIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "serial_argument_duplicate_or_missing");
                }
                serialIndex = index;
            }
            else if (string.Equals(argument, "-m", StringComparison.Ordinal))
            {
                if (memoryIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "memory_argument_duplicate_or_missing");
                }
                memoryIndex = index;
            }
            else if (string.Equals(
                argument,
                "-machine",
                StringComparison.Ordinal))
            {
                if (machineIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "machine_argument_duplicate_or_missing");
                }
                machineIndex = index;
            }
            else if (string.Equals(
                argument,
                "-display",
                StringComparison.Ordinal))
            {
                if (displayIndex >= 0 || index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException(
                        "display_argument_duplicate_or_missing");
                }
                displayIndex = index;
            }
            else if (string.Equals(
                argument,
                "-no-reboot",
                StringComparison.Ordinal))
            {
                noRebootCount++;
            }
            else if (string.Equals(argument, "-qmp", StringComparison.Ordinal)
                || string.Equals(
                    argument,
                    "-monitor",
                    StringComparison.Ordinal)
                || string.Equals(
                    argument,
                    "-chardev",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "preexisting_monitor_argument");
            }
        }

        if (serialIndex < 0)
        {
            throw new InvalidOperationException("serial_argument_missing");
        }
        if (memoryIndex < 0
            || !string.Equals(
                arguments[memoryIndex + 1],
                "256M",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "guest_memory_must_be_256M");
        }
        if (machineIndex < 0
            || !string.Equals(
                arguments[machineIndex + 1],
                "q35,accel=tcg",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "machine_must_be_q35_tcg");
        }
        if (displayIndex < 0
            || !string.Equals(
                arguments[displayIndex + 1],
                "none",
                StringComparison.Ordinal)
            || noRebootCount != 1)
        {
            throw new InvalidOperationException(
                "headless_no_reboot_contract_invalid");
        }

        string serialValue = arguments[serialIndex + 1];
        if (!serialValue.StartsWith("file:", StringComparison.Ordinal)
            || serialValue.Length == 5)
        {
            throw new InvalidOperationException(
                "serial_must_be_file_backend");
        }
        string publicSerial = FullPath(serialValue.Substring(5));
        arguments[serialIndex + 1] = "file:" + privateSerial;
        return publicSerial;
    }

    private static long DrainPrivateSerial(
        string privateSerial,
        long offset,
        SerialTerminalGate gate)
    {
        if (!File.Exists(privateSerial))
        {
            return offset;
        }

        using FileStream stream = new FileStream(
            privateSerial,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < offset)
        {
            throw new InvalidDataException(
                "private_serial_truncated");
        }
        stream.Position = offset;
        byte[] buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int index = 0; index < read; index++)
            {
                gate.Accept(buffer[index]);
            }
        }
        return stream.Position;
    }

    private static void WriteObservationSidecar(
        string path,
        ObservationScanner.ObservationEvidence evidence,
        string privateSerialHash,
        string predictedPublicHash,
        int guestTimeoutSeconds,
        int captureTimeoutSeconds)
    {
        string temporaryPath =
            path + ".tmp-" + Guid.NewGuid().ToString("N");
        bool moved = false;
        try
        {
            using (FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: true))
            {
                writer.WriteLine("schema=clros.m01.raw-observations.v1");
                writer.WriteLine(
                    "captureMethod=qmp-pmemsave-stopped-before-terminal-release");
                writer.WriteLine("guestMemoryBytes=" + GuestMemoryBytes);
                writer.WriteLine("captureChunkBytes=" + CaptureChunkBytes);
                writer.WriteLine(
                    "guestTimeoutSeconds="
                    + guestTimeoutSeconds.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "captureTimeoutSeconds="
                    + captureTimeoutSeconds.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine("terminalWithheld=true");
                writer.WriteLine("qmpPaused=true");
                writer.WriteLine("qmpMemoryShapeValidated=true");
                writer.WriteLine("memoryDumpRetained=false");
                writer.WriteLine(
                    "memorySha256=" + evidence.MemorySha256);
                writer.WriteLine(
                    "preflightCandidateCount="
                    + evidence.PreflightCandidateCount.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "preflightPhysicalAddress="
                    + Hex(evidence.PreflightPhysicalAddress));
                writer.WriteLine(
                    "preflightPhysicalAddresses="
                    + evidence.PreflightPhysicalAddresses);
                writer.WriteLine(
                    "preflightPodSha256=" + evidence.PreflightPodSha256);
                writer.WriteLine(
                    "preflightPodBase64=" + evidence.PreflightPodBase64);
                writer.WriteLine("cr0Before=" + Hex(evidence.Cr0));
                writer.WriteLine(
                    "firmwareCr3Before=" + Hex(evidence.RawCr3));
                writer.WriteLine("rawCr4Before=" + Hex(evidence.RawCr4));
                writer.WriteLine(
                    "ia32EferBefore=" + Hex(evidence.Ia32Efer));
                writer.WriteLine(
                    "cpuidMaximumExtendedLeaf="
                    + Hex32(evidence.CpuidMaximumExtendedLeaf));
                writer.WriteLine(
                    "cpuidAddressSizesEax="
                    + Hex32(evidence.CpuidAddressSizesEax));
                writer.WriteLine(
                    "physicalAddressWidth="
                    + evidence.PhysicalAddressWidth.ToString(
                        CultureInfo.InvariantCulture));
                writer.WriteLine(
                    "physicalAddressMask="
                    + Hex(evidence.PhysicalAddressMask));
                writer.WriteLine("ownedCr3=" + Hex(evidence.OwnedCr3));
                writer.WriteLine("rflagsBefore=" + Hex(evidence.Rflags));
                writer.WriteLine(
                    "runtimeStatePhysicalAddress="
                    + Hex(evidence.RuntimeStatePhysicalAddress));
                writer.WriteLine(
                    "runtimeStatePodSha256="
                    + evidence.RuntimeStatePodSha256);
                writer.WriteLine(
                    "runtimeStatePodBase64="
                    + evidence.RuntimeStatePodBase64);
                writer.WriteLine(
                    "transitionCanaryPhysicalAddress="
                    + Hex(evidence.TransitionCanaryPhysicalAddress));
                writer.WriteLine(
                    "transitionCanaryValue="
                    + Hex(evidence.TransitionCanaryValue));
                writer.WriteLine(
                    "callGatePhysicalAddress="
                    + Hex(evidence.CallGatePhysicalAddress));
                writer.WriteLine(
                    "callGatePodSha256=" + evidence.CallGatePodSha256);
                writer.WriteLine(
                    "callGatePodBase64=" + evidence.CallGatePodBase64);
                writer.WriteLine(
                    "firmwareCr3After="
                    + Hex(evidence.PostReturnCr3));
                writer.WriteLine(
                    "rawCr4After="
                    + Hex(evidence.PostReturnCr4));
                writer.WriteLine(
                    "rflagsAfter="
                    + Hex(evidence.PostReturnRflags));
                writer.WriteLine(
                    "runtimeFirmwareCr3Readback="
                    + Hex(evidence.FirmwareCr3Readback));
                writer.WriteLine(
                    "runtimeRestoredCr4Readback="
                    + Hex(evidence.RestoredCr4Readback));
                writer.WriteLine(
                    "runtimeRestoredRflagsIntent="
                    + Hex(evidence.RestoredRflagsIntent));
                writer.WriteLine(
                    "privateSerialSha256=" + privateSerialHash);
                writer.WriteLine(
                    "predictedPublicSerialSha256="
                    + predictedPublicHash);
                writer.WriteLine("exactEqualityValidation=pass");
                writer.WriteLine("terminalReleaseAuthorized=true");
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path);
            moved = true;
        }
        finally
        {
            if (!moved && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string Sha256FilePlusBytes(
        string path,
        byte[] suffix)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        {
            byte[] buffer = new byte[65536];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        hash.AppendData(suffix);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string RequiredEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "missing_environment_" + name);
        }
        return value;
    }

    private static int RequiredPositiveInt(string name)
    {
        string value = RequiredEnvironment(name);
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsed)
            || parsed <= 0
            || parsed > 300)
        {
            throw new InvalidOperationException(
                "invalid_environment_" + name);
        }
        return parsed;
    }

    private static bool RequiredBoolean(string name)
    {
        string value = RequiredEnvironment(name);
        if (string.Equals(value, "true", StringComparison.Ordinal))
        {
            return true;
        }
        if (string.Equals(value, "false", StringComparison.Ordinal))
        {
            return false;
        }
        throw new InvalidOperationException(
            "invalid_boolean_environment_" + name);
    }

    private static string FullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static void RequireCreateNewPath(string path, string name)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException(name + "_already_exists");
        }
    }

    private static void RequireDistinctPaths(params string[] paths)
    {
        HashSet<string> seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<string> normalizedPaths = new List<string>();
        foreach (string path in paths)
        {
            string normalized = Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            if (!seen.Add(normalized))
            {
                throw new InvalidOperationException("capture_path_overlap");
            }
            normalizedPaths.Add(normalized);
        }
        for (int left = 0; left < normalizedPaths.Count; left++)
        {
            for (int right = left + 1;
                right < normalizedPaths.Count;
                right++)
            {
                string leftPrefix =
                    normalizedPaths[left]
                    + Path.DirectorySeparatorChar;
                string rightPrefix =
                    normalizedPaths[right]
                    + Path.DirectorySeparatorChar;
                if (normalizedPaths[right].StartsWith(
                        leftPrefix,
                        StringComparison.OrdinalIgnoreCase)
                    || normalizedPaths[left].StartsWith(
                        rightPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "capture_path_containment");
                }
            }
        }
    }

    private static void TryDeleteCaptureScratch(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(directory))
            {
                File.Delete(file);
            }
            Directory.Delete(directory, recursive: false);
        }
        catch
        {
        }
    }

    private static void DeleteEmptyCaptureScratch(string directory)
    {
        if (!Directory.Exists(directory)
            || Directory.GetFileSystemEntries(directory).Length != 0)
        {
            throw new IOException(
                "capture_scratch_not_empty_after_scan");
        }
        Directory.Delete(directory, recursive: false);
        if (Directory.Exists(directory))
        {
            throw new IOException(
                "capture_scratch_delete_failed");
        }
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(character)
                    || character == '_'
                    || character == '-'
                    || character == '.'
                    ? character
                    : '_');
        }
        return builder.ToString();
    }

    private static string Hex(ulong value)
    {
        return "0x" + value.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string Hex32(uint value)
    {
        return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
    }

    private static void Log(string value)
    {
        lock (LogLock)
        {
            s_log?.WriteLine(value);
            s_log?.Flush();
        }
    }

    private sealed class SerialTerminalGate
    {
        private readonly FileStream _publicStream;
        private readonly string[] _guestMarkers;
        private readonly string[] _forbiddenMarkers;
        private readonly List<byte> _line = new List<byte>();
        private readonly MemoryStream _withheld = new MemoryStream();
        private int _nextMarker;

        public SerialTerminalGate(
            FileStream publicStream,
            string[] guestMarkers,
            string[] forbiddenMarkers)
        {
            _publicStream = publicStream;
            _guestMarkers = guestMarkers;
            _forbiddenMarkers = forbiddenMarkers;
        }

        public bool TerminalWithheld { get; private set; }

        public void Accept(byte value)
        {
            _line.Add(value);
            if (value == (byte)'\n')
            {
                ProcessCompleteLine();
            }
        }

        public void ValidateCompleteMarkerContract()
        {
            if (!TerminalWithheld
                || _nextMarker != _guestMarkers.Length)
            {
                throw new InvalidDataException(
                    "marker_contract_incomplete");
            }
        }

        public void CompleteAfterStop()
        {
            if (_line.Count != 0)
            {
                if (!TerminalWithheld)
                {
                    throw new InvalidDataException(
                        "partial_line_before_terminal");
                }
                byte[] partial = _line.ToArray();
                _withheld.Write(partial, 0, partial.Length);
                _line.Clear();
            }
        }

        public byte[] GetWithheldBytes()
        {
            return _withheld.ToArray();
        }

        private void ProcessCompleteLine()
        {
            byte[] bytes = _line.ToArray();
            _line.Clear();
            string line = Encoding.UTF8.GetString(bytes)
                .TrimEnd('\r', '\n');

            foreach (string forbidden in _forbiddenMarkers)
            {
                if (string.Equals(line, forbidden, StringComparison.Ordinal)
                    || line.StartsWith(
                        forbidden + " ",
                        StringComparison.Ordinal))
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException(
                        "forbidden_marker_" + Sanitize(forbidden));
                }
            }
            if (string.Equals(
                    line,
                    "M01_OWNED_CR3_ENTERED",
                    StringComparison.Ordinal)
                || line.StartsWith(
                    "M01_OWNED_CR3_ENTERED ",
                    StringComparison.Ordinal))
            {
                WriteCurrent(bytes);
                throw new InvalidDataException(
                    "owned_cr3_serial_marker_forbidden");
            }

            int markerIndex = Array.IndexOf(_guestMarkers, line);
            if (markerIndex >= 0)
            {
                if (markerIndex != _nextMarker)
                {
                    WriteCurrent(bytes);
                    throw new InvalidDataException(
                        "marker_order_or_duplicate_"
                        + Sanitize(line));
                }
                _nextMarker++;
            }

            if (string.Equals(
                    line,
                    SuccessTerminal,
                    StringComparison.Ordinal))
            {
                if (TerminalWithheld)
                {
                    throw new InvalidDataException(
                        "duplicate_success_terminal");
                }
                TerminalWithheld = true;
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }

            WriteCurrent(bytes);
        }

        private void WriteCurrent(byte[] bytes)
        {
            if (TerminalWithheld)
            {
                _withheld.Write(bytes, 0, bytes.Length);
                return;
            }
            _publicStream.Write(bytes, 0, bytes.Length);
            _publicStream.Flush();
        }
    }

    private sealed class QmpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Action<string> _log;
        private int _nextId;

        public QmpConnection(
            TcpClient client,
            int timeoutSeconds,
            Action<string> log)
        {
            _client = client;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = checked(timeoutSeconds * 1000);
            stream.WriteTimeout = checked(timeoutSeconds * 1000);
            _reader = new StreamReader(
                stream,
                new UTF8Encoding(false),
                false,
                4096,
                leaveOpen: true);
            _writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            _log = log;
        }

        public void Initialize()
        {
            string greeting = ReadLineRequired("qmp_greeting_missing");
            _log("qmpGreetingBase64=" + Convert.ToBase64String(
                Encoding.UTF8.GetBytes(greeting)));
            using JsonDocument document = JsonDocument.Parse(greeting);
            if (!document.RootElement.TryGetProperty(
                    "QMP",
                    out JsonElement qmp)
                || !qmp.TryGetProperty(
                    "version",
                    out JsonElement version)
                || !version.TryGetProperty(
                    "qemu",
                    out JsonElement qemuVersion)
                || !qemuVersion.TryGetProperty(
                    "major",
                    out _))
            {
                throw new InvalidDataException(
                    "qmp_greeting_invalid");
            }
            Send("qmp_capabilities", null);
        }

        public void RequireMemoryShape()
        {
            JsonElement summary = Send(
                "query-memory-size-summary",
                null);
            long pluggedMemoryBytes = 0;
            if (!summary.TryGetProperty(
                    "base-memory",
                    out JsonElement baseMemory)
                || !baseMemory.TryGetInt64(out long baseMemoryBytes)
                || baseMemoryBytes != GuestMemoryBytes
                || (summary.TryGetProperty(
                        "plugged-memory",
                        out JsonElement pluggedMemory)
                    && !pluggedMemory.TryGetInt64(
                        out pluggedMemoryBytes))
                || pluggedMemoryBytes != 0)
            {
                throw new InvalidDataException(
                    "qmp_memory_shape_invalid");
            }
            _log("qmpMemoryShapeValidated=true");
        }

        public void StopAndRequirePaused()
        {
            Send("stop", null);
            JsonElement status = Send("query-status", null);
            if (!status.TryGetProperty(
                    "running",
                    out JsonElement running)
                || running.ValueKind != JsonValueKind.False
                || !status.TryGetProperty(
                    "status",
                    out JsonElement statusName)
                || !string.Equals(
                    statusName.GetString(),
                    "paused",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "qmp_stop_not_paused");
            }
            _log("qmpPaused=true");
        }

        public void Pmemsave(
            long physicalAddress,
            int size,
            string path,
            int chunkIndex)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>
                {
                    ["val"] = physicalAddress,
                    ["size"] = size,
                    ["filename"] = path
                };
            Send(
                "pmemsave",
                arguments,
                "pmemsave-"
                    + chunkIndex.ToString(
                        "D2",
                        CultureInfo.InvariantCulture));
        }

        public JsonElement Send(
            string execute,
            Dictionary<string, object>? arguments,
            string? explicitId = null)
        {
            string id = explicitId
                ?? "command-"
                    + Interlocked.Increment(ref _nextId)
                        .ToString(
                            "D3",
                            CultureInfo.InvariantCulture);
            Dictionary<string, object> request =
                new Dictionary<string, object>
                {
                    ["execute"] = execute,
                    ["id"] = id
                };
            if (arguments is not null)
            {
                request["arguments"] = arguments;
            }
            string json = JsonSerializer.Serialize(request);
            _log("qmpRequestBase64=" + Convert.ToBase64String(
                Encoding.UTF8.GetBytes(json)));
            _writer.WriteLine(json);

            while (true)
            {
                string response = ReadLineRequired(
                    "qmp_response_missing_" + Sanitize(execute));
                _log("qmpResponseBase64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(response)));
                using JsonDocument document = JsonDocument.Parse(response);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty(
                        "event",
                        out JsonElement eventName))
                {
                    string? name = eventName.GetString();
                    if (string.Equals(
                                name,
                                "RESET",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "SHUTDOWN",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "GUEST_PANICKED",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "WATCHDOG",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "POWERDOWN",
                                StringComparison.Ordinal)
                            || string.Equals(
                                name,
                                "RESUME",
                                StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "qmp_terminal_event_"
                            + Sanitize(name ?? "unknown"));
                    }
                    continue;
                }
                if (!root.TryGetProperty(
                        "id",
                        out JsonElement responseId)
                    || !string.Equals(
                        responseId.GetString(),
                        id,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (root.TryGetProperty("error", out JsonElement error))
                {
                    throw new InvalidDataException(
                        "qmp_error_"
                        + Sanitize(execute)
                        + "_"
                        + Sanitize(error.ToString()));
                }
                if (!root.TryGetProperty(
                        "return",
                        out JsonElement result))
                {
                    throw new InvalidDataException(
                        "qmp_return_missing_"
                        + Sanitize(execute));
                }
                return result.Clone();
            }
        }

        public void Dispose()
        {
            _writer.Dispose();
            _reader.Dispose();
            _client.Dispose();
        }

        private string ReadLineRequired(string reason)
        {
            string? line = _reader.ReadLine();
            if (line is null)
            {
                throw new EndOfStreamException(reason);
            }
            return line;
        }
    }

    private sealed class ObservationScanner
    {
        private readonly List<PreflightCandidate> _preflight =
            new List<PreflightCandidate>();
        private readonly List<RuntimeCandidate> _runtime =
            new List<RuntimeCandidate>();
        private readonly HashSet<long> _preflightAddresses =
            new HashSet<long>();
        private readonly HashSet<long> _runtimeAddresses =
            new HashSet<long>();

        public static ObservationEvidence Capture(
            QmpConnection qmp,
            string scratchDirectory,
            int captureTimeoutSeconds)
        {
            DateTime deadline =
                DateTime.UtcNow.AddSeconds(captureTimeoutSeconds);
            ObservationScanner scanner = new ObservationScanner();
            using IncrementalHash memoryHash =
                IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] carry = Array.Empty<byte>();

            for (int chunkIndex = 0;
                chunkIndex < GuestMemoryBytes / CaptureChunkBytes;
                chunkIndex++)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        "observation_capture_timeout");
                }
                long physicalAddress =
                    (long)chunkIndex * CaptureChunkBytes;
                string chunkPath = Path.Combine(
                    scratchDirectory,
                    "memory-"
                        + chunkIndex.ToString(
                            "D2",
                            CultureInfo.InvariantCulture)
                        + ".bin");
                if (File.Exists(chunkPath))
                {
                    throw new IOException(
                        "capture_chunk_already_exists");
                }
                try
                {
                    qmp.Pmemsave(
                        physicalAddress,
                        CaptureChunkBytes,
                        chunkPath,
                        chunkIndex);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException(
                            "observation_capture_timeout");
                    }
                    FileInfo info = new FileInfo(chunkPath);
                    if (!info.Exists
                        || info.Length != CaptureChunkBytes)
                    {
                        throw new InvalidDataException(
                            "capture_chunk_size_mismatch_"
                            + chunkIndex.ToString(
                                CultureInfo.InvariantCulture));
                    }
                    byte[] bytes = File.ReadAllBytes(chunkPath);
                    memoryHash.AppendData(bytes);

                    byte[] scanBytes =
                        new byte[carry.Length + bytes.Length];
                    Buffer.BlockCopy(
                        carry,
                        0,
                        scanBytes,
                        0,
                        carry.Length);
                    Buffer.BlockCopy(
                        bytes,
                        0,
                        scanBytes,
                        carry.Length,
                        bytes.Length);
                    scanner.Scan(
                        scanBytes,
                        physicalAddress - carry.Length);
                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException(
                            "observation_capture_timeout");
                    }

                    int carryLength = Math.Min(
                        ScanCarryBytes,
                        scanBytes.Length);
                    carry = new byte[carryLength];
                    Buffer.BlockCopy(
                        scanBytes,
                        scanBytes.Length - carryLength,
                        carry,
                        0,
                        carryLength);
                }
                finally
                {
                    if (File.Exists(chunkPath))
                    {
                        File.Delete(chunkPath);
                    }
                }
            }

            string memorySha256 = Convert.ToHexString(
                memoryHash.GetHashAndReset());
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    "observation_capture_timeout");
            }
            return scanner.Complete(memorySha256);
        }

        private void Scan(byte[] bytes, long physicalBase)
        {
            long firstAligned8 =
                (physicalBase + 7L) & ~7L;
            for (long physical = firstAligned8;
                physical >= 0
                    && physical + PreflightCandidate.Size
                        <= physicalBase + bytes.Length;
                physical += 8)
            {
                int offset = checked((int)(physical - physicalBase));
                if (ReadUInt32(bytes, offset + 48) == 0x31464c50U
                    && ReadUInt32(bytes, offset + 52) == 0
                    && _preflightAddresses.Add(physical))
                {
                    _preflight.Add(
                        PreflightCandidate.Read(
                            bytes,
                            offset,
                            physical));
                }
            }

            long firstPage =
                (physicalBase + 4095L) & ~4095L;
            for (long physical = firstPage;
                physical >= 0
                    && physical + RuntimeCandidate.TotalSpan
                        <= physicalBase + bytes.Length;
                physical += 4096)
            {
                int offset = checked((int)(physical - physicalBase));
                RuntimeCandidate? candidate =
                    RuntimeCandidate.TryRead(
                        bytes,
                        offset,
                        physical);
                if (candidate is not null
                    && _runtimeAddresses.Add(physical))
                {
                    _runtime.Add(candidate);
                }
            }
        }

        private ObservationEvidence Complete(string memorySha256)
        {
            if (_runtime.Count != 1)
            {
                throw new InvalidDataException(
                    "runtime_candidate_count_"
                    + _runtime.Count.ToString(
                        CultureInfo.InvariantCulture));
            }
            RuntimeCandidate runtime = _runtime[0];
            List<PreflightCandidate> matching =
                new List<PreflightCandidate>();
            foreach (PreflightCandidate candidate in _preflight)
            {
                if (candidate.Matches(runtime))
                {
                    matching.Add(candidate);
                }
            }
            if (matching.Count == 0)
            {
                throw new InvalidDataException(
                    "preflight_candidate_count_"
                    + matching.Count.ToString(
                        CultureInfo.InvariantCulture));
            }
            PreflightCandidate preflight = matching[0];
            for (int index = 1; index < matching.Count; index++)
            {
                if (!preflight.Bytes.AsSpan().SequenceEqual(
                        matching[index].Bytes))
                {
                    throw new InvalidDataException(
                        "preflight_candidate_bytes_conflict");
                }
            }
            preflight.ValidateModeAndAddress(runtime);
            return new ObservationEvidence(
                memorySha256,
                matching,
                preflight,
                runtime);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(
                bytes[offset]
                | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(
                bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return
                bytes[offset]
                | ((ulong)bytes[offset + 1] << 8)
                | ((ulong)bytes[offset + 2] << 16)
                | ((ulong)bytes[offset + 3] << 24)
                | ((ulong)bytes[offset + 4] << 32)
                | ((ulong)bytes[offset + 5] << 40)
                | ((ulong)bytes[offset + 6] << 48)
                | ((ulong)bytes[offset + 7] << 56);
        }

        private static byte[] Copy(
            byte[] source,
            int offset,
            int count)
        {
            byte[] copy = new byte[count];
            Buffer.BlockCopy(source, offset, copy, 0, count);
            return copy;
        }

        public sealed class PreflightCandidate
        {
            public const int Size = 56;

            private PreflightCandidate(
                long physicalAddress,
                byte[] bytes,
                ulong cr0,
                ulong rawCr3,
                ulong rawCr4,
                ulong ia32Efer,
                uint cpuidMaximumExtendedLeaf,
                uint cpuidAddressSizesEax,
                ulong rflags)
            {
                PhysicalAddress = physicalAddress;
                Bytes = bytes;
                Cr0 = cr0;
                RawCr3 = rawCr3;
                RawCr4 = rawCr4;
                Ia32Efer = ia32Efer;
                CpuidMaximumExtendedLeaf =
                    cpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax = cpuidAddressSizesEax;
                Rflags = rflags;
            }

            public long PhysicalAddress { get; }
            public byte[] Bytes { get; }
            public ulong Cr0 { get; }
            public ulong RawCr3 { get; }
            public ulong RawCr4 { get; }
            public ulong Ia32Efer { get; }
            public uint CpuidMaximumExtendedLeaf { get; }
            public uint CpuidAddressSizesEax { get; }
            public byte PhysicalAddressWidth =>
                (byte)CpuidAddressSizesEax;
            public ulong Rflags { get; }

            public static PreflightCandidate Read(
                byte[] bytes,
                int offset,
                long physicalAddress)
            {
                return new PreflightCandidate(
                    physicalAddress,
                    Copy(bytes, offset, Size),
                    ReadUInt64(bytes, offset),
                    ReadUInt64(bytes, offset + 8),
                    ReadUInt64(bytes, offset + 16),
                    ReadUInt64(bytes, offset + 24),
                    ReadUInt32(bytes, offset + 32),
                    ReadUInt32(bytes, offset + 36),
                    ReadUInt64(bytes, offset + 40));
            }

            public bool Matches(RuntimeCandidate runtime)
            {
                return Cr0 == runtime.OriginalCr0
                    && RawCr3 == runtime.OriginalCr3
                    && RawCr4 == runtime.OriginalCr4
                    && Ia32Efer == runtime.OriginalEfer
                    && Rflags == runtime.OriginalRflags;
            }

            public void ValidateModeAndAddress(
                RuntimeCandidate runtime)
            {
                const ulong RequiredCr0 = 0x80010001UL;
                const ulong RequiredEfer = 0x0d00UL;
                const ulong Cr4Pae = 1UL << 5;
                const ulong Cr4La57 = 1UL << 12;
                const ulong Cr4Pcide = 1UL << 17;
                const ulong Cr4Cet = 1UL << 23;
                const ulong RflagsOne = 1UL << 1;
                const ulong RflagsTrap = 1UL << 8;
                const ulong RflagsDirection = 1UL << 10;
                const ulong Cr3AllowedLow = (1UL << 3) | (1UL << 4);

                byte width = PhysicalAddressWidth;
                if (CpuidMaximumExtendedLeaf < 0x80000008U
                    || width < 36
                    || width > 52
                    || (Cr0 & RequiredCr0) != RequiredCr0
                    || (RawCr4 & Cr4Pae) == 0
                    || (RawCr4
                        & (Cr4La57 | Cr4Pcide | Cr4Cet)) != 0
                    || (Ia32Efer & RequiredEfer) != RequiredEfer
                    || (Rflags & RflagsOne) == 0
                    || (Rflags
                        & (RflagsTrap | RflagsDirection)) != 0)
                {
                    throw new InvalidDataException(
                        "preflight_mode_invalid");
                }
                ulong physicalMask = (1UL << width) - 1;
                ulong allowedCr3 =
                    (physicalMask & ~0xfffUL) | Cr3AllowedLow;
                ulong firmwareRoot =
                    RawCr3 & (physicalMask & ~0xfffUL);
                if (runtime.PhysicalAddressMask != physicalMask
                    || (RawCr3 & ~allowedCr3) != 0
                    || firmwareRoot == 0)
                {
                    throw new InvalidDataException(
                        "preflight_cr3_or_width_invalid");
                }
            }
        }

        public sealed class RuntimeCandidate
        {
            public const int StateSize = 480;
            public const int GateOffset = 0x300;
            public const int GateSize = 160;
            public const int TotalSpan = GateOffset + GateSize;

            private RuntimeCandidate(
                long physicalAddress,
                byte[] stateBytes,
                byte[] gateBytes,
                ulong transitionCanaryValue)
            {
                PhysicalAddress = physicalAddress;
                StateBytes = stateBytes;
                GateBytes = gateBytes;
                OriginalRflags = ReadUInt64(stateBytes, 200);
                OriginalCr0 = ReadUInt64(stateBytes, 208);
                OriginalCr3 = ReadUInt64(stateBytes, 216);
                OriginalCr4 = ReadUInt64(stateBytes, 224);
                OriginalEfer = ReadUInt64(stateBytes, 232);
                PhysicalAddressMask = ReadUInt64(stateBytes, 240);
                OwnedCr3 = ReadUInt64(stateBytes, 8);
                TransitionCanaryValue = transitionCanaryValue;
                FirmwareCr3Readback = ReadUInt64(stateBytes, 408);
                RestoredCr4Readback = ReadUInt64(stateBytes, 424);
                RestoredRflagsIntent = ReadUInt64(stateBytes, 432);
                PostReturnRflags = ReadUInt64(gateBytes, 104);
                PostReturnCr3 = ReadUInt64(gateBytes, 112);
                PostReturnCr4 = ReadUInt64(gateBytes, 120);
            }

            public long PhysicalAddress { get; }
            public long GatePhysicalAddress =>
                PhysicalAddress + GateOffset;
            public byte[] StateBytes { get; }
            public byte[] GateBytes { get; }
            public ulong OriginalRflags { get; }
            public ulong OriginalCr0 { get; }
            public ulong OriginalCr3 { get; }
            public ulong OriginalCr4 { get; }
            public ulong OriginalEfer { get; }
            public ulong PhysicalAddressMask { get; }
            public ulong OwnedCr3 { get; }
            public ulong TransitionCanaryValue { get; }
            public ulong FirmwareCr3Readback { get; }
            public ulong RestoredCr4Readback { get; }
            public ulong RestoredRflagsIntent { get; }
            public ulong PostReturnRflags { get; }
            public ulong PostReturnCr3 { get; }
            public ulong PostReturnCr4 { get; }

            public static RuntimeCandidate? TryRead(
                byte[] bytes,
                int offset,
                long physicalAddress)
            {
                if (ReadUInt32(bytes, offset) != StateSize
                    || ReadUInt16(bytes, offset + 4) != 1
                    || bytes[offset + 6] != 1
                    || bytes[offset + 7] != 0
                    || ReadUInt64(bytes, offset + 184)
                        != (ulong)physicalAddress)
                {
                    return null;
                }

                byte[] state = Copy(
                    bytes,
                    offset,
                    StateSize);
                byte[] gate = Copy(
                    bytes,
                    offset + GateOffset,
                    GateSize);
                ulong originalRflags = ReadUInt64(state, 200);
                ulong originalCr3 = ReadUInt64(state, 216);
                ulong originalCr4 = ReadUInt64(state, 224);
                ulong ownedCr3 = ReadUInt64(state, 8);
                ulong cliRflags = ReadUInt64(state, 272);
                ulong physicalPageMask =
                    ReadUInt64(state, 240) & ~0xfffUL;
                ulong allowedOwnedCr3Mask =
                    physicalPageMask | (1UL << 3) | (1UL << 4);
                const ulong MutableBeforeCliReadback =
                    (1UL << 0)
                    | (1UL << 2)
                    | (1UL << 4)
                    | (1UL << 6)
                    | (1UL << 7)
                    | (1UL << 9)
                    | (1UL << 11);
                bool originalPge =
                    (originalCr4 & (1UL << 7)) != 0;

                if (ReadUInt32(gate, 0) != GateSize
                    || ReadUInt16(gate, 4) != 1
                    || gate[6] != 1
                    || gate[7] != 3
                    || ReadUInt64(gate, 8)
                        != (ulong)(physicalAddress + GateOffset)
                    || ReadUInt64(gate, 48) != originalRflags
                    || ReadUInt64(gate, 56) != originalCr3
                    || ReadUInt64(gate, 64) != originalCr4
                    || ReadUInt64(gate, 72) == 0
                    || ReadUInt32(gate, 80) != 1
                    || gate[84]
                        != (((originalRflags & (1UL << 9)) != 0)
                            ? (byte)1
                            : (byte)0)
                    || gate[85]
                        != (originalPge ? (byte)1 : (byte)0)
                    || gate[86] != 1
                    || gate[87] != 1
                    || ReadUInt64(gate, 88)
                        != ReadUInt64(gate, 72)
                    || ReadUInt64(gate, 96)
                        != ReadUInt64(gate, 72)
                    || ReadUInt64(gate, 104) != originalRflags
                    || ReadUInt64(gate, 112) != originalCr3
                    || ReadUInt64(gate, 120) != originalCr4
                    || ReadUInt64(gate, 128)
                        != ReadUInt64(gate, 72)
                    || ReadUInt32(gate, 136) != 1
                    || gate[140] != 1
                    || gate[141] != 1
                    || ReadUInt16(gate, 142) != 0
                    || ReadUInt32(gate, 144) != 0x4b47534fU
                    || ReadUInt32(gate, 148) != 0
                    || ReadUInt64(gate, 152) != 0
                    || ReadUInt32(state, 180) != 0
                    || physicalPageMask == 0
                    || (ownedCr3 & ~allowedOwnedCr3Mask) != 0
                    || (ownedCr3 & physicalPageMask) == 0
                    || (ownedCr3 & physicalPageMask)
                        == (originalCr3 & physicalPageMask)
                    || (cliRflags & ~MutableBeforeCliReadback)
                        != (originalRflags & ~MutableBeforeCliReadback)
                    || (cliRflags & (1UL << 9)) != 0
                    || ReadUInt64(state, 280)
                        != (originalCr4 & ~(1UL << 7))
                    || ReadUInt64(state, 288) != ownedCr3
                    || ReadUInt64(state, 408) != originalCr3
                    || ReadUInt64(state, 424) != originalCr4
                    || ReadUInt64(state, 432) != originalRflags
                    || ReadUInt64(state, 248)
                        != ReadUInt64(state, 440)
                    || ReadUInt64(state, 448) != 1
                    || ReadUInt64(state, 456) != 1
                    || state[464] != 1
                    || state[465] != 17
                    || state[466] < 1
                    || state[466] > 4
                    || state[467] != 1
                    || state[468]
                        != (originalPge ? (byte)1 : (byte)0)
                    || state[469] != 1
                    || state[470] != 1
                    || state[471] != 12
                    || ReadUInt64(state, 472) != 0
                    || ReadUInt64(state, 296)
                        != ReadUInt64(state, 160)
                    || ReadUInt64(state, 304)
                        != ReadUInt64(state, 168)
                    || ReadUInt64(state, 416)
                        != ReadUInt64(state, 168)
                    || ReadUInt64(bytes, offset + StateSize)
                        != ReadUInt64(state, 168))
                {
                    return null;
                }

                return new RuntimeCandidate(
                    physicalAddress,
                    state,
                    gate,
                    ReadUInt64(bytes, offset + StateSize));
            }
        }

        public sealed class ObservationEvidence
        {
            public ObservationEvidence(
                string memorySha256,
                List<PreflightCandidate> preflightCandidates,
                PreflightCandidate preflight,
                RuntimeCandidate runtime)
            {
                MemorySha256 = memorySha256;
                PreflightCandidateCount = preflightCandidates.Count;
                StringBuilder addresses = new StringBuilder();
                for (int index = 0;
                    index < preflightCandidates.Count;
                    index++)
                {
                    if (index != 0)
                    {
                        addresses.Append(';');
                    }
                    addresses.Append(
                        Hex(checked(
                            (ulong)preflightCandidates[index]
                                .PhysicalAddress)));
                }
                PreflightPhysicalAddresses = addresses.ToString();
                PreflightPhysicalAddress =
                    checked((ulong)preflight.PhysicalAddress);
                PreflightPodSha256 = HashBytes(preflight.Bytes);
                PreflightPodBase64 =
                    Convert.ToBase64String(preflight.Bytes);
                Cr0 = preflight.Cr0;
                RawCr3 = preflight.RawCr3;
                RawCr4 = preflight.RawCr4;
                Ia32Efer = preflight.Ia32Efer;
                CpuidMaximumExtendedLeaf =
                    preflight.CpuidMaximumExtendedLeaf;
                CpuidAddressSizesEax =
                    preflight.CpuidAddressSizesEax;
                PhysicalAddressWidth =
                    preflight.PhysicalAddressWidth;
                PhysicalAddressMask =
                    runtime.PhysicalAddressMask;
                OwnedCr3 = runtime.OwnedCr3;
                Rflags = preflight.Rflags;
                RuntimeStatePhysicalAddress =
                    checked((ulong)runtime.PhysicalAddress);
                RuntimeStatePodSha256 =
                    HashBytes(runtime.StateBytes);
                RuntimeStatePodBase64 =
                    Convert.ToBase64String(runtime.StateBytes);
                TransitionCanaryPhysicalAddress = checked(
                    (ulong)(runtime.PhysicalAddress
                        + RuntimeCandidate.StateSize));
                TransitionCanaryValue =
                    runtime.TransitionCanaryValue;
                CallGatePhysicalAddress =
                    checked((ulong)runtime.GatePhysicalAddress);
                CallGatePodSha256 =
                    HashBytes(runtime.GateBytes);
                CallGatePodBase64 =
                    Convert.ToBase64String(runtime.GateBytes);
                PostReturnRflags = runtime.PostReturnRflags;
                PostReturnCr3 = runtime.PostReturnCr3;
                PostReturnCr4 = runtime.PostReturnCr4;
                FirmwareCr3Readback =
                    runtime.FirmwareCr3Readback;
                RestoredCr4Readback =
                    runtime.RestoredCr4Readback;
                RestoredRflagsIntent =
                    runtime.RestoredRflagsIntent;
            }

            public string MemorySha256 { get; }
            public int PreflightCandidateCount { get; }
            public string PreflightPhysicalAddresses { get; }
            public ulong PreflightPhysicalAddress { get; }
            public string PreflightPodSha256 { get; }
            public string PreflightPodBase64 { get; }
            public ulong Cr0 { get; }
            public ulong RawCr3 { get; }
            public ulong RawCr4 { get; }
            public ulong Ia32Efer { get; }
            public uint CpuidMaximumExtendedLeaf { get; }
            public uint CpuidAddressSizesEax { get; }
            public byte PhysicalAddressWidth { get; }
            public ulong PhysicalAddressMask { get; }
            public ulong OwnedCr3 { get; }
            public ulong Rflags { get; }
            public ulong RuntimeStatePhysicalAddress { get; }
            public string RuntimeStatePodSha256 { get; }
            public string RuntimeStatePodBase64 { get; }
            public ulong TransitionCanaryPhysicalAddress { get; }
            public ulong TransitionCanaryValue { get; }
            public ulong CallGatePhysicalAddress { get; }
            public string CallGatePodSha256 { get; }
            public string CallGatePodBase64 { get; }
            public ulong PostReturnRflags { get; }
            public ulong PostReturnCr3 { get; }
            public ulong PostReturnCr4 { get; }
            public ulong FirmwareCr3Readback { get; }
            public ulong RestoredCr4Readback { get; }
            public ulong RestoredRflagsIntent { get; }

            private static string HashBytes(byte[] bytes)
            {
                return Convert.ToHexString(
                    SHA256.HashData(bytes));
            }
        }
    }
}
'@
}

function Write-Utf8NoBom {
    param(
        [string] $Path,
        [string] $Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::Read)
    try {
        $writer = New-Object System.IO.StreamWriter(
            $stream,
            $encoding,
            4096,
            $true)
        try {
            $writer.Write($Content)
            $writer.Flush()
            $stream.Flush($true)
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Resolve-M01AbsolutePath {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "M01 raw-observation path must not be empty."
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath(
        (Join-Path (Get-Location).ProviderPath $Path))
}

function Get-M01FileSha256 {
    param([string] $Path)

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString(
                $sha256.ComputeHash($stream)).Replace("-", "")
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-M01ByteSha256 {
    param([byte[]] $Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString(
            $sha256.ComputeHash($Bytes)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Format-M01Hex64 {
    param([UInt64] $Value)

    return "0x" + $Value.ToString("X16", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Format-M01Hex32 {
    param([UInt32] $Value)

    return "0x" + $Value.ToString("X8", [System.Globalization.CultureInfo]::InvariantCulture)
}

function New-M01RawObservationCapture {
    param(
        [string] $RealQemuPath,
        [string] $RequestedObservationLog,
        [string] $PublicSerialLog,
        [string] $SummaryLog,
        [int] $GuestTimeoutSeconds,
        [bool] $M02SafeStubEnabled
    )

    if ($GuestTimeoutSeconds -lt 1 -or $GuestTimeoutSeconds -gt 300) {
        throw "guest_timeout_out_of_range"
    }

    $publicSerialPath = Resolve-M01AbsolutePath -Path $PublicSerialLog
    $summaryPath = Resolve-M01AbsolutePath -Path $SummaryLog
    if ([string]::IsNullOrWhiteSpace($RequestedObservationLog)) {
        $observationPath = Resolve-M01AbsolutePath -Path (
            $PublicSerialLog +
            ".m01-raw-" +
            [System.Guid]::NewGuid().ToString("N") +
            ".log")
    }
    else {
        $observationPath = Resolve-M01AbsolutePath -Path $RequestedObservationLog
    }

    $observationParent = [System.IO.Path]::GetDirectoryName($observationPath)
    if ([string]::IsNullOrWhiteSpace($observationParent)) {
        throw "observation_parent_missing"
    }

    if ([System.IO.File]::Exists($observationPath) -or
        [System.IO.Directory]::Exists($observationPath)) {
        throw "observation_log_already_exists"
    }

    $supportDirectory = Join-Path $observationParent (
        "m01-026-raw-" + [System.Guid]::NewGuid().ToString("N"))
    $sourcePath = Join-Path $supportDirectory "Program.cs"
    $projectPath = Join-Path $supportDirectory "M01.RawObservationShim.csproj"
    $outputDirectory = Join-Path $supportDirectory "out"
    $privateSerialPath = Join-Path $supportDirectory "private-serial.log"
    $qmpTranscriptPath = Join-Path $supportDirectory "qmp-transcript.log"
    $captureScratchPath = Join-Path $supportDirectory "capture-scratch"

    $distinctPaths = @(
        $observationPath,
        $publicSerialPath,
        $summaryPath,
        $privateSerialPath,
        $qmpTranscriptPath,
        $captureScratchPath,
        $sourcePath,
        $projectPath,
        $outputDirectory
    )
    $seenPaths = @{}
    foreach ($candidatePath in $distinctPaths) {
        $normalizedPath = [System.IO.Path]::GetFullPath($candidatePath)
        if ($seenPaths.ContainsKey($normalizedPath)) {
            throw "capture_path_overlap"
        }
        $seenPaths.Add($normalizedPath, $true)
    }

    [System.IO.Directory]::CreateDirectory($observationParent) | Out-Null
    [System.IO.Directory]::CreateDirectory($supportDirectory) | Out-Null
    Write-Utf8NoBom `
        -Path $sourcePath `
        -Content (Get-M01RawObservationShimSource)
    $projectContent = @'
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
'@
    Write-Utf8NoBom -Path $projectPath -Content $projectContent

    $buildOutput = @(
        Invoke-NativeCommand {
            & dotnet build `
                $projectPath `
                -c Release `
                --nologo `
                -m:1 `
                -o $outputDirectory
        }
    )
    $buildExitCode = $script:LastNativeExitCode
    foreach ($line in $buildOutput) {
        Write-Host $line
    }
    if ($buildExitCode -ne 0) {
        throw ("shim_build_failed_" + $buildExitCode)
    }

    $shimExecutablePath = Join-Path $outputDirectory "M01.RawObservationShim.exe"
    if (-not [System.IO.File]::Exists($shimExecutablePath)) {
        $shimExecutablePath = Join-Path $outputDirectory "M01.RawObservationShim"
    }
    if (-not [System.IO.File]::Exists($shimExecutablePath)) {
        throw "shim_executable_missing"
    }

    $captureTimeoutSeconds = 60
    $runnerTimeoutSeconds =
        $GuestTimeoutSeconds + $captureTimeoutSeconds + 15
    return [PSCustomObject]@{
        RealQemuPath = [System.IO.Path]::GetFullPath($RealQemuPath)
        PublicSerialPath = $publicSerialPath
        ObservationPath = $observationPath
        SupportDirectory = $supportDirectory
        SourcePath = $sourcePath
        ProjectPath = $projectPath
        OutputDirectory = $outputDirectory
        ShimExecutablePath = $shimExecutablePath
        PrivateSerialPath = $privateSerialPath
        QmpTranscriptPath = $qmpTranscriptPath
        CaptureScratchPath = $captureScratchPath
        GuestTimeoutSeconds = $GuestTimeoutSeconds
        M02SafeStubEnabled = $M02SafeStubEnabled
        CaptureTimeoutSeconds = $captureTimeoutSeconds
        RunnerTimeoutSeconds = $runnerTimeoutSeconds
        SourceSha256 = Get-M01FileSha256 -Path $sourcePath
        ProjectSha256 = Get-M01FileSha256 -Path $projectPath
        ShimExecutableSha256 = Get-M01FileSha256 -Path $shimExecutablePath
        RealQemuSha256 = Get-M01FileSha256 -Path $RealQemuPath
        EnvironmentState = $null
    }
}

function Enter-M01RawObservationEnvironment {
    param([PSCustomObject] $Capture)

    $environmentValues = [ordered]@{
        CLROS_M01_REAL_QEMU = $Capture.RealQemuPath
        CLROS_M01_PRIVATE_SERIAL = $Capture.PrivateSerialPath
        CLROS_M01_RAW_OBSERVATION_LOG = $Capture.ObservationPath
        CLROS_M01_QMP_TRANSCRIPT_LOG = $Capture.QmpTranscriptPath
        CLROS_M01_CAPTURE_SCRATCH = $Capture.CaptureScratchPath
        CLROS_M01_GUEST_TIMEOUT_SECONDS = [string] $Capture.GuestTimeoutSeconds
        CLROS_M01_CAPTURE_TIMEOUT_SECONDS = [string] $Capture.CaptureTimeoutSeconds
        CLROS_M02_SAFE_STUB_PROBE = (
            Format-QemuSmokeBoolean $Capture.M02SafeStubEnabled)
    }
    $entries = @()
    try {
        foreach ($name in $environmentValues.Keys) {
            $previousValue = [System.Environment]::GetEnvironmentVariable(
                $name,
                [System.EnvironmentVariableTarget]::Process)
            $entries += [PSCustomObject]@{
                Name = $name
                WasDefined = $null -ne $previousValue
                Value = $previousValue
            }
            [System.Environment]::SetEnvironmentVariable(
                $name,
                [string] $environmentValues[$name],
                [System.EnvironmentVariableTarget]::Process)
        }
    }
    catch {
        foreach ($entry in $entries) {
            $restoredValue = if ($entry.WasDefined) {
                [string] $entry.Value
            }
            else {
                $null
            }
            [System.Environment]::SetEnvironmentVariable(
                $entry.Name,
                $restoredValue,
                [System.EnvironmentVariableTarget]::Process)
        }
        throw
    }

    return [PSCustomObject]@{
        Entries = $entries
        Restored = $false
    }
}

function Restore-M01RawObservationEnvironment {
    param(
        [AllowNull()] [PSCustomObject] $State,
        [switch] $BestEffort
    )

    if ($null -eq $State -or $State.Restored) {
        return
    }

    try {
        foreach ($entry in $State.Entries) {
            $restoredValue = if ($entry.WasDefined) {
                [string] $entry.Value
            }
            else {
                $null
            }
            [System.Environment]::SetEnvironmentVariable(
                $entry.Name,
                $restoredValue,
                [System.EnvironmentVariableTarget]::Process)
        }
        $State.Restored = $true
    }
    catch {
        if (-not $BestEffort) {
            throw
        }
    }
}

function Read-M01RawObservationSidecar {
    param([string] $Path)

    if (-not [System.IO.File]::Exists($Path)) {
        throw "observation_sidecar_missing"
    }
    $values = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            throw "observation_sidecar_blank_line"
        }
        $separator = $line.IndexOf("=")
        if ($separator -le 0) {
            throw "observation_sidecar_malformed_line"
        }
        $name = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        if ($values.ContainsKey($name)) {
            throw ("observation_sidecar_duplicate_" + $name)
        }
        $values.Add($name, $value)
    }
    return $values
}

function Assert-M01RawObservationSidecar {
    param([PSCustomObject] $Capture)

    $values = Read-M01RawObservationSidecar -Path $Capture.ObservationPath
    $expectedKeys = @(
        "schema",
        "captureMethod",
        "guestMemoryBytes",
        "captureChunkBytes",
        "guestTimeoutSeconds",
        "captureTimeoutSeconds",
        "terminalWithheld",
        "qmpPaused",
        "qmpMemoryShapeValidated",
        "memoryDumpRetained",
        "memorySha256",
        "preflightCandidateCount",
        "preflightPhysicalAddress",
        "preflightPhysicalAddresses",
        "preflightPodSha256",
        "preflightPodBase64",
        "cr0Before",
        "firmwareCr3Before",
        "rawCr4Before",
        "ia32EferBefore",
        "cpuidMaximumExtendedLeaf",
        "cpuidAddressSizesEax",
        "physicalAddressWidth",
        "physicalAddressMask",
        "ownedCr3",
        "rflagsBefore",
        "runtimeStatePhysicalAddress",
        "runtimeStatePodSha256",
        "runtimeStatePodBase64",
        "transitionCanaryPhysicalAddress",
        "transitionCanaryValue",
        "callGatePhysicalAddress",
        "callGatePodSha256",
        "callGatePodBase64",
        "firmwareCr3After",
        "rawCr4After",
        "rflagsAfter",
        "runtimeFirmwareCr3Readback",
        "runtimeRestoredCr4Readback",
        "runtimeRestoredRflagsIntent",
        "privateSerialSha256",
        "predictedPublicSerialSha256",
        "exactEqualityValidation",
        "terminalReleaseAuthorized"
    )
    if ($values.Count -ne $expectedKeys.Count) {
        throw "observation_sidecar_key_count_mismatch"
    }
    foreach ($key in $expectedKeys) {
        if (-not $values.ContainsKey($key)) {
            throw ("observation_sidecar_missing_" + $key)
        }
    }

    $exactValues = @{
        schema = "clros.m01.raw-observations.v1"
        captureMethod = "qmp-pmemsave-stopped-before-terminal-release"
        guestMemoryBytes = "268435456"
        captureChunkBytes = "16777216"
        guestTimeoutSeconds = [string] $Capture.GuestTimeoutSeconds
        captureTimeoutSeconds = [string] $Capture.CaptureTimeoutSeconds
        terminalWithheld = "true"
        qmpPaused = "true"
        qmpMemoryShapeValidated = "true"
        memoryDumpRetained = "false"
        exactEqualityValidation = "pass"
        terminalReleaseAuthorized = "true"
    }
    foreach ($key in $exactValues.Keys) {
        if (-not [string]::Equals(
                $values[$key],
                $exactValues[$key],
                [System.StringComparison]::Ordinal)) {
            throw ("observation_sidecar_value_" + $key)
        }
    }

    $candidateCount = 0
    if (-not [int]::TryParse(
            $values["preflightCandidateCount"],
            [System.Globalization.NumberStyles]::None,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref] $candidateCount) -or
        $candidateCount -lt 1) {
        throw "observation_preflight_candidate_count_invalid"
    }
    $candidateAddresses = $values["preflightPhysicalAddresses"].Split(";")
    if ($candidateAddresses.Count -ne $candidateCount) {
        throw "observation_preflight_address_count_mismatch"
    }
    $seenAddresses = @{}
    foreach ($address in $candidateAddresses) {
        if ($address -notmatch "\A0x[0-9A-F]{16}\z" -or
            $seenAddresses.ContainsKey($address)) {
            throw "observation_preflight_address_invalid"
        }
        $numericAddress = [System.Convert]::ToUInt64(
            $address.Substring(2),
            16)
        if ($numericAddress -gt (268435456 - 56) -or
            ($numericAddress % 8) -ne 0) {
            throw "observation_preflight_address_out_of_range"
        }
        $seenAddresses.Add($address, $true)
    }
    if (-not [string]::Equals(
            $values["preflightPhysicalAddress"],
            $candidateAddresses[0],
            [System.StringComparison]::Ordinal)) {
        throw "observation_preflight_primary_address_mismatch"
    }

    $hashKeys = @(
        "memorySha256",
        "preflightPodSha256",
        "runtimeStatePodSha256",
        "callGatePodSha256",
        "privateSerialSha256",
        "predictedPublicSerialSha256"
    )
    foreach ($key in $hashKeys) {
        if ($values[$key] -notmatch "\A[0-9A-F]{64}\z") {
            throw ("observation_hash_invalid_" + $key)
        }
    }

    $hex64Keys = @(
        "preflightPhysicalAddress",
        "cr0Before",
        "firmwareCr3Before",
        "rawCr4Before",
        "ia32EferBefore",
        "physicalAddressMask",
        "ownedCr3",
        "rflagsBefore",
        "runtimeStatePhysicalAddress",
        "transitionCanaryPhysicalAddress",
        "transitionCanaryValue",
        "callGatePhysicalAddress",
        "firmwareCr3After",
        "rawCr4After",
        "rflagsAfter",
        "runtimeFirmwareCr3Readback",
        "runtimeRestoredCr4Readback",
        "runtimeRestoredRflagsIntent"
    )
    foreach ($key in $hex64Keys) {
        if ($values[$key] -notmatch "\A0x[0-9A-F]{16}\z") {
            throw ("observation_hex64_invalid_" + $key)
        }
    }
    foreach ($key in @("cpuidMaximumExtendedLeaf", "cpuidAddressSizesEax")) {
        if ($values[$key] -notmatch "\A0x[0-9A-F]{8}\z") {
            throw ("observation_hex32_invalid_" + $key)
        }
    }

    try {
        $preflightBytes = [System.Convert]::FromBase64String(
            $values["preflightPodBase64"])
        $runtimeStateBytes = [System.Convert]::FromBase64String(
            $values["runtimeStatePodBase64"])
        $callGateBytes = [System.Convert]::FromBase64String(
            $values["callGatePodBase64"])
    }
    catch {
        throw "observation_pod_base64_invalid"
    }
    if ($preflightBytes.Length -ne 56 -or
        $runtimeStateBytes.Length -ne 480 -or
        $callGateBytes.Length -ne 160) {
        throw "observation_pod_length_invalid"
    }
    if (-not [string]::Equals(
            (Get-M01ByteSha256 -Bytes $preflightBytes),
            $values["preflightPodSha256"],
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            (Get-M01ByteSha256 -Bytes $runtimeStateBytes),
            $values["runtimeStatePodSha256"],
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            (Get-M01ByteSha256 -Bytes $callGateBytes),
            $values["callGatePodSha256"],
            [System.StringComparison]::Ordinal)) {
        throw "observation_pod_hash_mismatch"
    }

    $preflightCr0 = [System.BitConverter]::ToUInt64($preflightBytes, 0)
    $preflightCr3 = [System.BitConverter]::ToUInt64($preflightBytes, 8)
    $preflightCr4 = [System.BitConverter]::ToUInt64($preflightBytes, 16)
    $preflightEfer = [System.BitConverter]::ToUInt64($preflightBytes, 24)
    $preflightMaximumLeaf = [System.BitConverter]::ToUInt32($preflightBytes, 32)
    $preflightAddressSizes = [System.BitConverter]::ToUInt32($preflightBytes, 36)
    $preflightRflags = [System.BitConverter]::ToUInt64($preflightBytes, 40)
    $runtimeRflags = [System.BitConverter]::ToUInt64($runtimeStateBytes, 200)
    $runtimeCr0 = [System.BitConverter]::ToUInt64($runtimeStateBytes, 208)
    $runtimeCr3 = [System.BitConverter]::ToUInt64($runtimeStateBytes, 216)
    $runtimeCr4 = [System.BitConverter]::ToUInt64($runtimeStateBytes, 224)
    $runtimeEfer = [System.BitConverter]::ToUInt64($runtimeStateBytes, 232)
    $runtimeMask = [System.BitConverter]::ToUInt64($runtimeStateBytes, 240)
    $runtimeOwnedCr3 = [System.BitConverter]::ToUInt64($runtimeStateBytes, 8)
    $runtimeReplacementCanary =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 168)
    $runtimeFirmwareCr3Readback =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 408)
    $runtimeRestoredCr4Readback =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 424)
    $runtimeRestoredRflagsIntent =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 432)
    $postRflags = [System.BitConverter]::ToUInt64($callGateBytes, 104)
    $postCr3 = [System.BitConverter]::ToUInt64($callGateBytes, 112)
    $postCr4 = [System.BitConverter]::ToUInt64($callGateBytes, 120)

    $physicalAddressWidth = 0
    if (-not [int]::TryParse(
            $values["physicalAddressWidth"],
            [System.Globalization.NumberStyles]::None,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref] $physicalAddressWidth) -or
        $physicalAddressWidth -ne ($preflightAddressSizes -band 0xff) -or
        $physicalAddressWidth -lt 36 -or
        $physicalAddressWidth -gt 52) {
        throw "observation_physical_width_mismatch"
    }

    if ([System.BitConverter]::ToUInt32($preflightBytes, 48) -ne
            [uint32] 0x31464c50 -or
        [System.BitConverter]::ToUInt32($preflightBytes, 52) -ne 0) {
        throw "observation_preflight_structure_invalid"
    }

    $expectedPhysicalAddressMask =
        [uint64]::MaxValue -shr (64 - $physicalAddressWidth)
    $physicalPageMask =
        $expectedPhysicalAddressMask -band ([uint64]::MaxValue - 0xfff)
    $allowedCr3Mask = $physicalPageMask -bor [uint64] 0x18
    $firmwareRoot = $preflightCr3 -band $physicalPageMask
    if ($preflightMaximumLeaf -lt [uint32] 2147483656 -or
        ($preflightCr0 -band [uint64] 2147549185) -ne
            [uint64] 2147549185 -or
        ($preflightCr4 -band [uint64] 0x20) -eq 0 -or
        ($preflightCr4 -band [uint64] 0x00821000) -ne 0 -or
        ($preflightEfer -band [uint64] 0x0d00) -ne [uint64] 0x0d00 -or
        ($preflightRflags -band [uint64] 0x2) -eq 0 -or
        ($preflightRflags -band [uint64] 0x500) -ne 0 -or
        $runtimeMask -ne $expectedPhysicalAddressMask -or
        ($preflightCr3 -band $allowedCr3Mask) -ne $preflightCr3 -or
        $firmwareRoot -eq 0) {
        throw "observation_mode_or_root_invalid"
    }

    $runtimePhysicalAddress = [System.Convert]::ToUInt64(
        $values["runtimeStatePhysicalAddress"].Substring(2),
        16)
    $transitionCanaryPhysicalAddress = [System.Convert]::ToUInt64(
        $values["transitionCanaryPhysicalAddress"].Substring(2),
        16)
    $callGatePhysicalAddress = [System.Convert]::ToUInt64(
        $values["callGatePhysicalAddress"].Substring(2),
        16)
    if ($runtimePhysicalAddress -eq 0 -or
        ($runtimePhysicalAddress % 4096) -ne 0 -or
        $runtimePhysicalAddress -gt (268435456 - 0x3a0)) {
        throw "observation_runtime_address_invalid"
    }
    if ($transitionCanaryPhysicalAddress -ne
        ($runtimePhysicalAddress + 480)) {
        throw "observation_transition_canary_address_mismatch"
    }
    if ($callGatePhysicalAddress -ne
        ($runtimePhysicalAddress + 0x300)) {
        throw "observation_call_gate_address_mismatch"
    }

    $runtimeCodeBase =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 48)
    $runtimeCodeEnd =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 56)
    $runtimeExpectedReturnAddress =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 80)
    $runtimeExpectedCanary =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 160)
    $runtimeEntryRsp =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 248)
    $runtimeCliRflags =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 272)
    $runtimeClearedCr4 =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 280)
    $runtimeOwnedCr3Readback =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 288)
    $runtimeExitRsp =
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 440)
    $runtimeOwnedRoot = $runtimeOwnedCr3 -band $physicalPageMask
    $immutableRflagsMask =
        [uint64]::MaxValue -bxor [uint64] 0x0ad5
    $expectedPgeByte = if (
        ($runtimeCr4 -band [uint64] 0x80) -ne 0) {
        1
    }
    else {
        0
    }
    $expectedInterruptByte = if (
        ($runtimeRflags -band [uint64] 0x200) -ne 0) {
        1
    }
    else {
        0
    }

    if ([System.BitConverter]::ToUInt32($runtimeStateBytes, 0) -ne 480 -or
        [System.BitConverter]::ToUInt16($runtimeStateBytes, 4) -ne 1 -or
        $runtimeStateBytes[6] -ne 1 -or
        $runtimeStateBytes[7] -ne 0 -or
        [System.BitConverter]::ToUInt32($runtimeStateBytes, 180) -ne 0 -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 184) -ne
            $runtimePhysicalAddress -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 472) -ne 0) {
        throw "observation_runtime_structure_invalid"
    }
    if ([System.BitConverter]::ToUInt32($runtimeStateBytes, 176) -ne
            [uint32] 0x4f534f4b -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 360) -ne
            [uint64] 0x4f534f4b -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 448) -ne 1 -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 456) -ne 1 -or
        $runtimeStateBytes[464] -ne 1 -or
        $runtimeStateBytes[465] -ne 17 -or
        $runtimeStateBytes[466] -lt 1 -or
        $runtimeStateBytes[466] -gt 4 -or
        $runtimeStateBytes[467] -ne 1 -or
        $runtimeStateBytes[468] -ne $expectedPgeByte -or
        $runtimeStateBytes[469] -ne 1 -or
        $runtimeStateBytes[470] -ne 1 -or
        $runtimeStateBytes[471] -ne 12) {
        throw "observation_runtime_completion_invalid"
    }
    if ($physicalPageMask -eq 0 -or
        ($runtimeOwnedCr3 -band $allowedCr3Mask) -ne $runtimeOwnedCr3 -or
        $runtimeOwnedRoot -eq 0 -or
        $runtimeOwnedRoot -eq $firmwareRoot -or
        ($runtimeCliRflags -band $immutableRflagsMask) -ne
            ($runtimeRflags -band $immutableRflagsMask) -or
        ($runtimeCliRflags -band [uint64] 0x200) -ne 0 -or
        $runtimeClearedCr4 -ne
            ($runtimeCr4 -band ([uint64]::MaxValue - 0x80)) -or
        $runtimeOwnedCr3Readback -ne $runtimeOwnedCr3 -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 296) -ne
            $runtimeExpectedCanary -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 304) -ne
            $runtimeReplacementCanary -or
        [System.BitConverter]::ToUInt64($runtimeStateBytes, 416) -ne
            $runtimeReplacementCanary -or
        $runtimeFirmwareCr3Readback -ne $runtimeCr3 -or
        $runtimeRestoredCr4Readback -ne $runtimeCr4 -or
        $runtimeRestoredRflagsIntent -ne $runtimeRflags -or
        $runtimeEntryRsp -ne $runtimeExitRsp) {
        throw "observation_runtime_readback_invalid"
    }

    $gateBoundaryCanary =
        [System.BitConverter]::ToUInt64($callGateBytes, 72)
    if ([System.BitConverter]::ToUInt32($callGateBytes, 0) -ne 160 -or
        [System.BitConverter]::ToUInt16($callGateBytes, 4) -ne 1 -or
        $callGateBytes[6] -ne 1 -or
        $callGateBytes[7] -ne 3 -or
        [System.BitConverter]::ToUInt64($callGateBytes, 8) -ne
            $callGatePhysicalAddress -or
        [System.BitConverter]::ToUInt64($callGateBytes, 16) -ne
            $runtimeCodeBase -or
        [System.BitConverter]::ToUInt64($callGateBytes, 24) -ne
            $runtimeCodeBase -or
        [System.BitConverter]::ToUInt64($callGateBytes, 32) -ne
            ($runtimeCodeBase + 0x0f00) -or
        [System.BitConverter]::ToUInt64($callGateBytes, 40) -ne
            $runtimeExpectedReturnAddress -or
        $runtimeExpectedReturnAddress -ne
            ($runtimeCodeBase + 0x0fc9) -or
        $runtimeCodeEnd -ne ($runtimeCodeBase + 0x1000) -or
        [System.BitConverter]::ToUInt64($callGateBytes, 48) -ne
            $runtimeRflags -or
        [System.BitConverter]::ToUInt64($callGateBytes, 56) -ne
            $runtimeCr3 -or
        [System.BitConverter]::ToUInt64($callGateBytes, 64) -ne
            $runtimeCr4 -or
        $gateBoundaryCanary -eq 0 -or
        [System.BitConverter]::ToUInt32($callGateBytes, 80) -ne 1 -or
        $callGateBytes[84] -ne $expectedInterruptByte -or
        $callGateBytes[85] -ne $expectedPgeByte -or
        $callGateBytes[86] -ne 1 -or
        $callGateBytes[87] -ne 1 -or
        [System.BitConverter]::ToUInt64($callGateBytes, 88) -ne
            $gateBoundaryCanary -or
        [System.BitConverter]::ToUInt64($callGateBytes, 96) -ne
            $gateBoundaryCanary -or
        $postRflags -ne $runtimeRflags -or
        $postCr3 -ne $runtimeCr3 -or
        $postCr4 -ne $runtimeCr4 -or
        [System.BitConverter]::ToUInt64($callGateBytes, 128) -ne
            $gateBoundaryCanary -or
        [System.BitConverter]::ToUInt32($callGateBytes, 136) -ne 1 -or
        $callGateBytes[140] -ne 1 -or
        $callGateBytes[141] -ne 1 -or
        [System.BitConverter]::ToUInt16($callGateBytes, 142) -ne 0 -or
        [System.BitConverter]::ToUInt32($callGateBytes, 144) -ne
            [uint32] 0x4b47534f -or
        [System.BitConverter]::ToUInt32($callGateBytes, 148) -ne 0 -or
        [System.BitConverter]::ToUInt64($callGateBytes, 152) -ne 0) {
        throw "observation_call_gate_structure_invalid"
    }

    $podFieldExpectations = @{
        cr0Before = Format-M01Hex64 -Value $preflightCr0
        firmwareCr3Before = Format-M01Hex64 -Value $preflightCr3
        rawCr4Before = Format-M01Hex64 -Value $preflightCr4
        ia32EferBefore = Format-M01Hex64 -Value $preflightEfer
        cpuidMaximumExtendedLeaf = Format-M01Hex32 -Value $preflightMaximumLeaf
        cpuidAddressSizesEax = Format-M01Hex32 -Value $preflightAddressSizes
        physicalAddressMask = Format-M01Hex64 -Value $runtimeMask
        ownedCr3 = Format-M01Hex64 -Value $runtimeOwnedCr3
        transitionCanaryValue =
            Format-M01Hex64 -Value $runtimeReplacementCanary
        rflagsBefore = Format-M01Hex64 -Value $preflightRflags
        firmwareCr3After = Format-M01Hex64 -Value $postCr3
        rawCr4After = Format-M01Hex64 -Value $postCr4
        rflagsAfter = Format-M01Hex64 -Value $postRflags
        runtimeFirmwareCr3Readback =
            Format-M01Hex64 -Value $runtimeFirmwareCr3Readback
        runtimeRestoredCr4Readback =
            Format-M01Hex64 -Value $runtimeRestoredCr4Readback
        runtimeRestoredRflagsIntent =
            Format-M01Hex64 -Value $runtimeRestoredRflagsIntent
    }
    foreach ($key in $podFieldExpectations.Keys) {
        if (-not [string]::Equals(
                $values[$key],
                $podFieldExpectations[$key],
                [System.StringComparison]::Ordinal)) {
            throw ("observation_pod_field_mismatch_" + $key)
        }
    }
    if ($preflightCr0 -ne $runtimeCr0 -or
        $preflightCr3 -ne $runtimeCr3 -or
        $preflightCr4 -ne $runtimeCr4 -or
        $preflightEfer -ne $runtimeEfer -or
        $preflightCr3 -ne $postCr3 -or
        $preflightCr4 -ne $postCr4 -or
        $preflightRflags -ne $runtimeRflags -or
        $preflightRflags -ne $postRflags) {
        throw "observation_exact_restoration_mismatch"
    }
    if (-not [string]::Equals(
            $values["runtimeFirmwareCr3Readback"],
            $values["firmwareCr3Before"],
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $values["runtimeRestoredCr4Readback"],
            $values["rawCr4Before"],
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $values["runtimeRestoredRflagsIntent"],
            $values["rflagsBefore"],
            [System.StringComparison]::Ordinal)) {
        throw "observation_runtime_readback_mismatch"
    }

    if (-not [System.IO.File]::Exists($Capture.PrivateSerialPath) -or
        -not [System.IO.File]::Exists($Capture.PublicSerialPath)) {
        throw "observation_serial_missing"
    }
    $privateSerialHash = Get-M01FileSha256 -Path $Capture.PrivateSerialPath
    $publicSerialHash = Get-M01FileSha256 -Path $Capture.PublicSerialPath
    if (-not [string]::Equals(
            $values["privateSerialSha256"],
            $privateSerialHash,
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $values["predictedPublicSerialSha256"],
            $publicSerialHash,
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            $privateSerialHash,
            $publicSerialHash,
            [System.StringComparison]::Ordinal)) {
        throw "observation_serial_hash_mismatch"
    }

    if (-not [System.IO.File]::Exists($Capture.QmpTranscriptPath)) {
        throw "observation_qmp_transcript_missing"
    }
    $qmpLines = [System.IO.File]::ReadAllLines($Capture.QmpTranscriptPath)
    foreach ($requiredLine in @(
            "qmpMemoryShapeValidated=true",
            "qmpPaused=true",
            "M01_RAW_OBSERVATION_CAPTURED",
            ("publicSerialSha256=" + $publicSerialHash),
            ("privateSerialSha256=" + $privateSerialHash),
            "terminalReleaseAuthorized=true",
            "terminalReleased=true")) {
        if ($qmpLines -notcontains $requiredLine) {
            throw ("observation_qmp_transcript_missing_" + $requiredLine)
        }
    }
    if ([System.IO.Directory]::Exists($Capture.CaptureScratchPath) -and
        [System.IO.Directory]::GetFileSystemEntries(
            $Capture.CaptureScratchPath).Length -ne 0) {
        throw "observation_capture_data_retained"
    }

    return [PSCustomObject]@{
        ObservationSha256 = Get-M01FileSha256 -Path $Capture.ObservationPath
        QmpTranscriptSha256 = Get-M01FileSha256 -Path $Capture.QmpTranscriptPath
        SerialSha256 = $publicSerialHash
        MemorySha256 = $values["memorySha256"]
        Cr0 = $values["cr0Before"]
        FirmwareCr3 = $values["firmwareCr3Before"]
        RawCr4 = $values["rawCr4Before"]
        Ia32Efer = $values["ia32EferBefore"]
        CpuidAddressSizesEax = $values["cpuidAddressSizesEax"]
        PhysicalAddressWidth = $values["physicalAddressWidth"]
        Rflags = $values["rflagsBefore"]
        CandidateCount = $candidateCount
    }
}

function Remove-M01RawObservationTransient {
    param(
        [AllowNull()] [PSCustomObject] $Capture,
        [switch] $BestEffort
    )

    if ($null -eq $Capture) {
        return
    }

    try {
        if ([System.IO.File]::Exists($Capture.PrivateSerialPath)) {
            [System.IO.File]::Delete($Capture.PrivateSerialPath)
        }
        if ([System.IO.Directory]::Exists($Capture.CaptureScratchPath)) {
            $supportPrefix = $Capture.SupportDirectory.TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar) +
                [System.IO.Path]::DirectorySeparatorChar
            $scratchPath = [System.IO.Path]::GetFullPath(
                $Capture.CaptureScratchPath)
            if (-not $scratchPath.StartsWith(
                    $supportPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "capture_scratch_outside_support_directory"
            }
            Remove-Item `
                -LiteralPath $scratchPath `
                -Recurse `
                -Force
        }
    }
    catch {
        if (-not $BestEffort) {
            throw
        }
    }
}

function Normalize-MarkerPreset {
    param([string] $Preset)

    if ([string]::IsNullOrWhiteSpace($Preset)) {
        Write-Host "TEST_FAIL qemu.smoke.preset invalid empty"
        exit 1
    }

    $normalized = $Preset.Trim().ToLowerInvariant()
    if ($normalized -eq "default") {
        return "baseline"
    }

    if ($normalized -eq "baseline" -or $normalized -eq "acpi" -or $normalized -eq "app-args" -or $normalized -eq "app-exit" -or $normalized -eq "app-lifecycle" -or $normalized -eq "app-selection" -or $normalized -eq "cctor-preflight" -or $normalized -eq "cctor-work-items" -or $normalized -eq "diagnostics" -or $normalized -eq "fetch-http" -or $normalized -eq "fetch-http-endpoint" -or $normalized -eq "fetch-http-tcp-exchange" -or $normalized -eq "http-static-response" -or $normalized -eq "http-static-tcp-response" -or $normalized -eq "manifest-env" -or $normalized -eq "nativeaot-heap" -or $normalized -eq "pal-export" -or $normalized -eq "uefi-gone" -or $normalized -eq "virtio-rx-tcp-dispatch") {
        return $normalized
    }

    if ($normalized -eq "app_args" -or $normalized -eq "app-arguments" -or $normalized -eq "native-app-args") {
        return "app-args"
    }

    if ($normalized -eq "appexit" -or $normalized -eq "app_exit") {
        return "app-exit"
    }

    if ($normalized -eq "app_lifecycle" -or $normalized -eq "native-app-lifecycle" -or $normalized -eq "applifecycle") {
        return "app-lifecycle"
    }

    if ($normalized -eq "app_selection" -or $normalized -eq "native-app-selection" -or $normalized -eq "appselection") {
        return "app-selection"
    }

    if ($normalized -eq "cctor" -or $normalized -eq "cctor_preflight") {
        return "cctor-preflight"
    }

    if ($normalized -eq "cctor_work_items" -or $normalized -eq "cctor-workitem" -or $normalized -eq "cctor-work-item") {
        return "cctor-work-items"
    }

    if ($normalized -eq "fetchhttp" -or $normalized -eq "fetch_http" -or $normalized -eq "fetchapp-http" -or $normalized -eq "fetchapp_http") {
        return "fetch-http"
    }

    if ($normalized -eq "fetch_endpoint" -or $normalized -eq "fetchhttp-endpoint" -or $normalized -eq "fetchapp-http-endpoint") {
        return "fetch-http-endpoint"
    }

    if ($normalized -eq "fetch_http_tcp_exchange" -or $normalized -eq "fetch-http-exchange" -or $normalized -eq "fetchapp-http-tcp-exchange") {
        return "fetch-http-tcp-exchange"
    }

    if ($normalized -eq "http_static_response" -or $normalized -eq "tiny-http-static" -or $normalized -eq "tinyhttp-static") {
        return "http-static-response"
    }

    if ($normalized -eq "http_static_tcp_response" -or $normalized -eq "tiny-http-tcp-static" -or $normalized -eq "tinyhttp-tcp-static") {
        return "http-static-tcp-response"
    }

    if ($normalized -eq "virtio_rx_tcp_dispatch" -or $normalized -eq "virtio-net-rx-tcp-dispatch" -or $normalized -eq "virtionet-rx-tcp-dispatch") {
        return "virtio-rx-tcp-dispatch"
    }

    if ($normalized -eq "native-manifest-env" -or $normalized -eq "manifest_environment" -or $normalized -eq "manifestenv") {
        return "manifest-env"
    }

    if ($normalized -eq "nativeaot_heap" -or $normalized -eq "heap-bootstrap" -or $normalized -eq "nativeaot-heap-bootstrap") {
        return "nativeaot-heap"
    }

    if ($normalized -eq "pal_export" -or $normalized -eq "pal-catalog" -or $normalized -eq "pal-export-catalog") {
        return "pal-export"
    }

    if ($normalized -eq "uefigone" -or $normalized -eq "uefi_gone") {
        return "uefi-gone"
    }

    Write-Host ("TEST_FAIL qemu.smoke.preset invalid preset=" + $Preset + " valid=baseline,acpi,app-args,app-exit,app-lifecycle,app-selection,cctor-preflight,cctor-work-items,diagnostics,fetch-http,fetch-http-endpoint,fetch-http-tcp-exchange,http-static-response,http-static-tcp-response,manifest-env,nativeaot-heap,pal-export,uefi-gone,virtio-rx-tcp-dispatch")
    exit 1
}

Push-Location $root
try {
    Assert-WorkloadProfile -Profile $WorkloadProfile
    $DiskFormat = $DiskFormat.ToLowerInvariant()
    if ($DiskFormat -ne "raw" -and $DiskFormat -ne "qcow2") {
        Write-Host ("TEST_FAIL qemu.smoke.invalid_args disk_format=" + $DiskFormat + " valid=raw,qcow2")
        exit 1
    }

    $rhpReason = ""
    if (($EnableM04RhpNewFastBeforeBindFailureProbe -or $EnableM04RhpNewFastExhaustionFailureProbe) -and -not $EnableM04RhpNewFastProbe) { $rhpReason = "m04_rhp_new_fast_negative_requires_main" }
    if ($EnableM04RhpNewFastBeforeBindFailureProbe -and $EnableM04RhpNewFastExhaustionFailureProbe) { $rhpReason = "m04_rhp_new_fast_negative_conflict" }
    if ($EnableM04RhpNewFastProbe) {
        foreach ($rhpRequired in @("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe")) {
            if (-not [bool](Get-Variable -Name $rhpRequired -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_requires_explicit_chain_" + $rhpRequired }
        }
        foreach ($rhpConflict in @("EnableM02SafeStubProbe", "EnableM02CheckedPortIoBindingProbe", "EnableM02Com1OwnedPortIoProbe", "EnableM02OwnedCom1ReadyProbe", "EnableM02PanicSafeOwnedCom1Probe", "EnableM02OwnedX64GdtEncodingProbe", "EnableM02GuardedIstStackAllocationProbe", "EnableM02OwnedGdtTssTerminalProbe", "EnableM02NormalizedInterruptFramePageProbe", "EnableM02OwnedIdtGatePopulationProbe", "EnableM02IdtrContinuedExecutionProbe", "EnableM02Ud2VectorStateProbe", "EnableM02PageFaultCr2ErrorCodeProbe", "EnableM01OwnedPagingProbe", "EnableUefiGoneSimulationProbe", "EnableExitBootServicesFreshMapProbe", "EnableAcpiPhysicalDiscoveryProbe")) {
            if ([bool](Get-Variable -Name $rhpConflict -ValueOnly)) { $rhpReason = "m04_rhp_new_fast_conflicting_profile_" + $rhpConflict }
        }
    }
    if ($rhpReason.Length -ne 0) { Write-Host ("TEST_FAIL qemu.smoke.invalid_args " + $rhpReason); exit 1 }

    if ($EnableM02Ud2VectorStateProbe) {
        $m02Ud2VectorStatePrerequisites = @(
            "EnableM01LoadedImageStackProbe",
            "EnableM01LiveAllocatorProbe",
            "EnableM01OwnedPagingProbe",
            "EnableM02SafeStubProbe",
            "EnableM02CheckedPortIoBindingProbe",
            "EnableM02Com1OwnedPortIoProbe",
            "EnableM02OwnedCom1ReadyProbe",
            "EnableM02PanicSafeOwnedCom1Probe"
        )
        foreach ($prerequisiteName in $m02Ud2VectorStatePrerequisites) {
            if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
                -not [bool]$PSBoundParameters[$prerequisiteName]) {
                Write-Host (
                    "TEST_FAIL qemu.smoke.invalid_args " +
                    "m02_ud2_vector_state_probe_explicit_false_prerequisite " +
                    "property=" + $prerequisiteName)
                exit 1
            }
        }

        $EnableM01LoadedImageStackProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01LiveAllocatorProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01OwnedPagingProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02SafeStubProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02CheckedPortIoBindingProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02Com1OwnedPortIoProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02OwnedCom1ReadyProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02PanicSafeOwnedCom1Probe =
            [System.Management.Automation.SwitchParameter]::new($true)
        Write-Host (
            "QEMU_SCRIPT_M02_UD2_VECTOR_STATE_PREREQUISITES status=implied " +
            "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
            "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
            "ownedCom1Ready=true panicSafeOwnedCom1=true")
    }
    if ($EnableM02PageFaultCr2ErrorCodeProbe) {
        foreach ($conflict in @(
            "EnableM02OwnedX64GdtEncodingProbe"
            "EnableM02GuardedIstStackAllocationProbe"
            "EnableM02OwnedGdtTssTerminalProbe"
            "EnableM02NormalizedInterruptFramePageProbe"
            "EnableM02OwnedIdtGatePopulationProbe"
            "EnableM02IdtrContinuedExecutionProbe"
            "EnableM02Ud2VectorStateProbe"
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
            "EnableM04ArithmeticConformanceProbe"
            "EnableM04LayoutContractProbe"
            "EnableM04ManagedHeapPageRangeProbe"
        )) {
            if ([bool](Get-Variable -Name $conflict -ValueOnly)) {
                Write-Host ("TEST_FAIL qemu.smoke.invalid_args m02_page_fault_cr2_error_code_profile_conflict property=" + $conflict)
                exit 1
            }
        }
    }

    if ($EnableM02PageFaultCr2ErrorCodeProbe) {
        $m02PageFaultCr2ErrorCodePrerequisites = @(
            "EnableM01LoadedImageStackProbe",
            "EnableM01LiveAllocatorProbe",
            "EnableM01OwnedPagingProbe",
            "EnableM02SafeStubProbe",
            "EnableM02CheckedPortIoBindingProbe",
            "EnableM02Com1OwnedPortIoProbe",
            "EnableM02OwnedCom1ReadyProbe",
            "EnableM02PanicSafeOwnedCom1Probe"
        )
        foreach ($prerequisiteName in $m02PageFaultCr2ErrorCodePrerequisites) {
            if ($PSBoundParameters.ContainsKey($prerequisiteName) -and
                -not [bool]$PSBoundParameters[$prerequisiteName]) {
                Write-Host (
                    "TEST_FAIL qemu.smoke.invalid_args " +
                    "m02_page_fault_cr2_error_code_probe_explicit_false_prerequisite " +
                    "property=" + $prerequisiteName)
                exit 1
            }
        }

        $EnableM01LoadedImageStackProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01LiveAllocatorProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01OwnedPagingProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02SafeStubProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02CheckedPortIoBindingProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02Com1OwnedPortIoProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02OwnedCom1ReadyProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM02PanicSafeOwnedCom1Probe =
            [System.Management.Automation.SwitchParameter]::new($true)
        Write-Host (
            "QEMU_SCRIPT_M02_PAGE_FAULT_CR2_ERROR_CODE_PREREQUISITES status=implied " +
            "m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true " +
            "m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true " +
            "ownedCom1Ready=true panicSafeOwnedCom1=true")
    }

    if (($EnableM02OwnedGdtTssTerminalProbe -or
            $EnableM02Ud2VectorStateProbe -or
            $EnableM02GuardedIstStackAllocationProbe -or
            $EnableM02OwnedX64GdtEncodingProbe -or
            $EnableM02PanicSafeOwnedCom1Probe -or
            $EnableM02OwnedCom1ReadyProbe) -and
        $EnableM02Com1OwnedPortIoProbe -and
        $EnableM02CheckedPortIoBindingProbe -and
        $EnableM02SafeStubProbe) {
        $EnableM01LoadedImageStackProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01LiveAllocatorProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        $EnableM01OwnedPagingProbe =
            [System.Management.Automation.SwitchParameter]::new($true)
        Write-Host (
            "QEMU_SCRIPT_M02_OWNED_COM1_READY_PREREQUISITES " +
            "m01LoadedImageStack=true m01LiveAllocator=true m01OwnedPaging=true")
    }

    Write-WorkloadProfileEvidence -Profile $WorkloadProfile

    $requiredMarkers = @(
        ConvertTo-QemuSmokeMarkerList -Values $RequireMarker
    )
    $orderedMarkers = @(
        ConvertTo-QemuSmokeMarkerList -Values $RequireOrderedMarker
    )
    $uniqueMarkers = @(
        ConvertTo-QemuSmokeMarkerList -Values $RequireUniqueMarker
    )
    $forbiddenMarkers = @(
        ConvertTo-QemuSmokeMarkerList -Values $ForbidMarker
    )

    if ($EnableM04RhpNewFastProbe) {
        $requiredMarkers = @(ConvertTo-QemuSmokeMarkerList -Values @($requiredMarkers; $script:M04RhpNewFastRequiredMarkers))
        $orderedMarkers = @($script:M04RhpNewFastRequiredMarkers)
        $uniqueMarkers = @($script:M04RhpNewFastRequiredMarkers)
        $forbiddenMarkers = @(ConvertTo-QemuSmokeMarkerList -Values @($forbiddenMarkers; $script:M04RhpNewFastForbiddenMarkers))
    }
    elseif ($EnableM04ManagedHeapPageRangeProbe) {
        $requiredMarkers = @(ConvertTo-QemuSmokeMarkerList -Values @($requiredMarkers; $script:M04ManagedHeapPageRangeRequiredMarkers))
        $orderedMarkers = @($script:M04ManagedHeapPageRangeRequiredMarkers)
        $uniqueMarkers = @($script:M04ManagedHeapPageRangeRequiredMarkers)
        $forbiddenMarkers = @(ConvertTo-QemuSmokeMarkerList -Values @($forbiddenMarkers; $script:M04ManagedHeapPageRangeForbiddenMarkers))
    }
    elseif ($EnableM04LayoutContractProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M04LayoutContractRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04LayoutContractRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04LayoutContractUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M04LayoutContractForbiddenMarkers
            )
        )
    }
    elseif ($EnableM04ArithmeticConformanceProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M04ArithmeticConformanceRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04ArithmeticConformanceRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04ArithmeticConformanceUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M04ArithmeticConformanceForbiddenMarkers
            )
        )
    }
    elseif ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M04SpanMemoryConformanceRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04SpanMemoryConformanceRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $script:M04SpanMemoryConformanceUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M04SpanMemoryConformanceForbiddenMarkers
            )
        )
    }
    elseif ($EnableM04IndependentEndianConformanceProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M04IndependentEndianConformanceRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M04IndependentEndianConformanceRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M04IndependentEndianConformanceReadyMarker
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M04IndependentEndianConformanceForbiddenMarkers
            )
        )
    }

    if ($EnableM02NormalizedInterruptFramePageProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02NormalizedInterruptFramePageRequiredMarkers
            )
        )
        $orderedMarkersBeforeMemoryMap = @(
            $orderedMarkers | Where-Object {
                $_ -cne "UEFI_MEMORY_MAP_READY"
            }
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkersBeforeMemoryMap
                $script:M02NormalizedInterruptFramePageRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02NormalizedInterruptFramePageUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02NormalizedInterruptFramePageForbiddenMarkers
            )
        )
    }

    if ($EnableM02OwnedIdtGatePopulationProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02OwnedIdtGatePopulationRequiredMarkers
            )
        )
        $orderedMarkersBeforeMemoryMap = @(
            $orderedMarkers | Where-Object {
                $_ -cne "UEFI_MEMORY_MAP_READY"
            }
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkersBeforeMemoryMap
                $script:M02OwnedIdtGatePopulationRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02OwnedIdtGatePopulationUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02OwnedIdtGatePopulationForbiddenMarkers
            )
        )
    }

    if ($EnableM02IdtrContinuedExecutionProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $script:M02IdtrContinuedExecutionRequiredMarkers
                $requiredMarkers
            )
        )
        $orderedMarkersBeforeMemoryMap = @(
            $orderedMarkers | Where-Object {
                $_ -cne "UEFI_MEMORY_MAP_READY"
            }
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkersBeforeMemoryMap
                $script:M02IdtrContinuedExecutionOrderedMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02IdtrContinuedExecutionUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02IdtrContinuedExecutionForbiddenMarkers
            )
        )
    }

    if ($EnableM02OwnedGdtTssTerminalProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $script:M02OwnedGdtTssTerminalRequiredMarkers
            )
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $script:M02OwnedGdtTssTerminalRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $script:M02OwnedGdtTssTerminalRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $script:M02OwnedGdtTssTerminalForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02GuardedIstStackAllocationProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02GuardedIstStackAllocationRequiredMarkers
            )
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02GuardedIstStackAllocationRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02GuardedIstStackAllocationRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02GuardedIstStackAllocationForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02OwnedX64GdtEncodingProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02OwnedX64GdtEncodingRequiredMarkers
            )
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02OwnedX64GdtEncodingRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02OwnedX64GdtEncodingRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02OwnedX64GdtEncodingForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02PanicSafeOwnedCom1Probe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02PanicSafeOwnedCom1RequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02PanicSafeOwnedCom1RequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02PanicSafeOwnedCom1RequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02PanicSafeOwnedCom1ForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02OwnedCom1ReadyProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02OwnedCom1ReadyRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02OwnedCom1ReadyRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02OwnedCom1ReadyRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02OwnedCom1ReadyForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02Com1OwnedPortIoProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02Com1OwnedPortIoRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02Com1OwnedPortIoRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02Com1OwnedPortIoRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02Com1OwnedPortIoForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02CheckedPortIoBindingProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02CheckedPortIoBindingRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02CheckedPortIoBindingRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02CheckedPortIoBindingRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02CheckedPortIoBindingForbiddenMarkers
            )
        )
    }
    elseif ($EnableM02SafeStubProbe) {
        $requiredMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02SafeStubRequiredMarkers
            )
        )
        $orderedMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $orderedMarkers
                $script:M02SafeStubRequiredMarkers
            )
        )
        $uniqueMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02SafeStubRequiredMarkers
            )
        )
        $forbiddenMarkers = @(
            ConvertTo-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02SafeStubForbiddenMarkers
            )
        )
    }

    if ($EnableM02Ud2VectorStateProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02Ud2VectorStateRequiredMarkers
            )
        )
        $orderedMarkersBeforeTerminal = @(
            $orderedMarkers | Where-Object {
                $_ -cne "OS_BOOT_OK" -and
                $_ -cne "TEST_PASS boot.sequence" -and
                $_ -cne "OS_SMOKE_PASS"
            }
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkersBeforeTerminal
                $script:M02Ud2VectorStateOrderedMarkers
            )
        )
        $uniqueMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02Ud2VectorStateUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02Ud2VectorStateForbiddenMarkers
            )
        )
    }
    if ($EnableM02PageFaultCr2ErrorCodeProbe) {
        $requiredMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $requiredMarkers
                $script:M02PageFaultCr2ErrorCodeRequiredMarkers
            )
        )
        $orderedMarkersBeforeTerminal = @(
            $orderedMarkers | Where-Object {
                $_ -cne "OS_BOOT_OK" -and
                $_ -cne "TEST_PASS boot.sequence" -and
                $_ -cne "OS_SMOKE_PASS"
            }
        )
        $orderedMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $orderedMarkersBeforeTerminal
                $script:M02PageFaultCr2ErrorCodeOrderedMarkers
            )
        )
        $uniqueMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $uniqueMarkers
                $script:M02PageFaultCr2ErrorCodeUniqueMarkers
            )
        )
        $forbiddenMarkers = @(
            Merge-QemuSmokeMarkerList -Values @(
                $forbiddenMarkers
                $script:M02PageFaultCr2ErrorCodeForbiddenMarkers
            )
        )
    }

    Write-Host ("QEMU_SCRIPT_M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent) +
        " profileRequired=15 profileOrdered=15 profileUnique=15 profileForbidden=8")
    if ($RequireNativeAppMarkers) {
        foreach ($marker in $nativeAppRequiredMarkers) {
            if ($requiredMarkers -notcontains $marker) {
                $requiredMarkers += $marker
            }
        }
    }

    if ($requiredMarkers.Count -eq 0) {
        throw "At least one required marker must be specified."
    }

    Write-Host (
        "QEMU_SCRIPT_M01_LOADED_IMAGE_STACK_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM01LoadedImageStackProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent))
    Write-Host (
        "QEMU_SCRIPT_M01_LIVE_ALLOCATOR_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM01LiveAllocatorProbe.IsPresent) +
        " loadedImageStack=" +
        (Format-QemuSmokeBoolean $EnableM01LoadedImageStackProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent))
    Write-Host (
        "QEMU_SCRIPT_M01_OWNED_PAGING_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM01OwnedPagingProbe.IsPresent) +
        " liveAllocator=" +
        (Format-QemuSmokeBoolean $EnableM01LiveAllocatorProbe.IsPresent) +
        " loadedImageStack=" +
        (Format-QemuSmokeBoolean $EnableM01LoadedImageStackProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent))
    Write-Host (
        "QEMU_SCRIPT_M02_SAFE_STUB_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02SafeStubProbe.IsPresent) +
        " ownedPaging=" +
        (Format-QemuSmokeBoolean $EnableM01OwnedPagingProbe.IsPresent) +
        " liveAllocator=" +
        (Format-QemuSmokeBoolean $EnableM01LiveAllocatorProbe.IsPresent) +
        " loadedImageStack=" +
        (Format-QemuSmokeBoolean $EnableM01LoadedImageStackProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=48 profileForbidden=34")
    Write-Host (
        "QEMU_SCRIPT_M02_CHECKED_PORT_IO_BINDING_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02CheckedPortIoBindingProbe.IsPresent) +
        " safeStub=" +
        (Format-QemuSmokeBoolean $EnableM02SafeStubProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=49 profileForbidden=35")
    Write-Host (
        "QEMU_SCRIPT_M02_COM1_OWNED_PORT_IO_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02Com1OwnedPortIoProbe.IsPresent) +
        " checkedBinding=" +
        (Format-QemuSmokeBoolean $EnableM02CheckedPortIoBindingProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=50 profileForbidden=36")
    Write-Host (
        "QEMU_SCRIPT_M02_OWNED_COM1_READY_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedCom1ReadyProbe.IsPresent) +
        " com1OwnedPortIo=" +
        (Format-QemuSmokeBoolean $EnableM02Com1OwnedPortIoProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=52 profileForbidden=37")
    Write-Host (
        "QEMU_SCRIPT_M02_PANIC_SAFE_OWNED_COM1_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02PanicSafeOwnedCom1Probe.IsPresent) +
        " ownedCom1Ready=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedCom1ReadyProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=54 profileForbidden=38")
    Write-Host (
        "QEMU_SCRIPT_M02_OWNED_X64_GDT_ENCODING_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedX64GdtEncodingProbe.IsPresent) +
        " panicSafeOwnedCom1=" +
        (Format-QemuSmokeBoolean $EnableM02PanicSafeOwnedCom1Probe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=55 profileForbidden=39")
    Write-Host (
        "QEMU_SCRIPT_M02_GUARDED_IST_STACK_ALLOCATION_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02GuardedIstStackAllocationProbe.IsPresent) +
        " ownedX64GdtEncoding=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedX64GdtEncodingProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=56 profileForbidden=40")
    Write-Host (
        "QEMU_SCRIPT_M02_OWNED_GDT_TSS_TERMINAL_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " guardedIstStackAllocation=" +
        (Format-QemuSmokeBoolean $EnableM02GuardedIstStackAllocationProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " rawObservationActive=false" +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=59 profileForbidden=43")
    Write-Host (
        "QEMU_SCRIPT_M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02NormalizedInterruptFramePageProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=17 profileOrdered=5 profileUnique=3 profileForbidden=3")
    Write-Host (
        "QEMU_SCRIPT_M02_OWNED_IDT_GATE_POPULATION_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedIdtGatePopulationProbe.IsPresent) +
        " normalizedInterruptFrame=" +
        (Format-QemuSmokeBoolean $EnableM02NormalizedInterruptFramePageProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=17 profileOrdered=5 profileUnique=3 profileForbidden=3")
    Write-Host (
        "QEMU_SCRIPT_M02_IDTR_CONTINUED_EXECUTION_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02IdtrContinuedExecutionProbe.IsPresent) +
        " normalizedInterruptFrame=" +
        (Format-QemuSmokeBoolean $EnableM02NormalizedInterruptFramePageProbe.IsPresent) +
        " ownedIdtGatePopulation=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedIdtGatePopulationProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=17 profileOrdered=17 profileUnique=6 profileForbidden=5")
    Write-Host (
        "QEMU_SCRIPT_M02_UD2_VECTOR_STATE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM02Ud2VectorStateProbe.IsPresent) +
        " panicSafeOwnedCom1=" +
        (Format-QemuSmokeBoolean $EnableM02PanicSafeOwnedCom1Probe.IsPresent) +
        " ownedX64GdtEncoding=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedX64GdtEncodingProbe.IsPresent) +
        " guardedIstStackAllocation=" +
        (Format-QemuSmokeBoolean $EnableM02GuardedIstStackAllocationProbe.IsPresent) +
        " normalizedInterruptFrame=" +
        (Format-QemuSmokeBoolean $EnableM02NormalizedInterruptFramePageProbe.IsPresent) +
        " ownedIdtGatePopulation=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedIdtGatePopulationProbe.IsPresent) +
        " idtrContinuedExecution=" +
        (Format-QemuSmokeBoolean $EnableM02IdtrContinuedExecutionProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=6 profileOrdered=6 profileUnique=6 profileForbidden=8")
    Write-Host (
        "QEMU_SCRIPT_M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM04IndependentEndianConformanceProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=15 profileForbidden=3")
    Write-Host (
        "QEMU_SCRIPT_M04_SPAN_MEMORY_CONFORMANCE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent) +
        " independentEndianConformance=" +
        (Format-QemuSmokeBoolean $EnableM04IndependentEndianConformanceProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=16 profileOrdered=4 profileUnique=2 profileForbidden=4")
    Write-Host (
        "QEMU_SCRIPT_M04_ARITHMETIC_CONFORMANCE_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM04ArithmeticConformanceProbe.IsPresent) +
        " spanMemoryConformance=" +
        (Format-QemuSmokeBoolean $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=17 profileOrdered=5 profileUnique=3 profileForbidden=5")
    Write-Host (
        "QEMU_SCRIPT_M04_LAYOUT_CONTRACT_PROBE enabled=" +
        (Format-QemuSmokeBoolean $EnableM04LayoutContractProbe.IsPresent) +
        " arithmeticConformance=" +
        (Format-QemuSmokeBoolean $EnableM04ArithmeticConformanceProbe.IsPresent) +
        " ownedGdtTssTerminal=" +
        (Format-QemuSmokeBoolean $EnableM02OwnedGdtTssTerminalProbe.IsPresent) +
        " buildEfi=" +
        (Format-QemuSmokeBoolean $BuildEfi.IsPresent) +
        " skipImageBuild=" +
        (Format-QemuSmokeBoolean $SkipImageBuild.IsPresent) +
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent) +
        " profileRequired=18 profileOrdered=6 profileUnique=4 profileForbidden=6 strictWholeLines=true")
    Write-Host (
        "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " +
        (Format-QemuSmokeMarkerList $orderedMarkers))
    Write-Host (
        "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " +
        (Format-QemuSmokeMarkerList $uniqueMarkers))

    if ($EnableM04ManagedHeapPageRangeProbe -and
        (-not $EnableM01LoadedImageStackProbe -or
        -not $EnableM01LiveAllocatorProbe -or
        -not $EnableM04IndependentEndianConformanceProbe -or
        -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
        -not $EnableM04ArithmeticConformanceProbe -or
        -not $EnableM04LayoutContractProbe)) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_managed_heap_page_range_requires_explicit_chain"
        exit 1
    }
    if ($EnableM04ManagedHeapPageRangeProbe -and
        ($EnableM01OwnedPagingProbe -or
        $EnableM02SafeStubProbe -or
        $EnableM02CheckedPortIoBindingProbe -or
        $EnableM02Com1OwnedPortIoProbe -or
        $EnableM02OwnedCom1ReadyProbe -or
        $EnableM02PanicSafeOwnedCom1Probe -or
        $EnableM02OwnedX64GdtEncodingProbe -or
        $EnableM02GuardedIstStackAllocationProbe -or
        $EnableM02OwnedGdtTssTerminalProbe -or
        $EnableM02NormalizedInterruptFramePageProbe -or
        $EnableM02OwnedIdtGatePopulationProbe -or
        $EnableM02IdtrContinuedExecutionProbe -or
        $EnableM02Ud2VectorStateProbe -or
        $EnableUefiGoneSimulationProbe -or
        $EnableExitBootServicesFreshMapProbe -or
        $EnableAcpiPhysicalDiscoveryProbe -or $SkipImageBuild -or -not $BuildEfi -or $M01RawObservationLog.Length -gt 0)) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_managed_heap_page_range_forbids_optional_profiles_or_missing_build"
        exit 1
    }

    if ($EnableM04LayoutContractProbe -and
        (-not $EnableM04ArithmeticConformanceProbe -or
        -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -or
        -not $EnableM04IndependentEndianConformanceProbe)) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_probe_requires_explicit_m04_chain"
        exit 1
    }

    if ($EnableM04LayoutContractProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_probe_forbids_owned_gdt_tss_terminal"
        exit 1
    }

    if ($EnableM04LayoutContractProbe -and
        $EnableM02Ud2VectorStateProbe) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_probe_forbids_m02_ud2_vector_state"
        exit 1
    }

    if ($EnableM04LayoutContractProbe -and
        -not $BuildEfi) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_probe_requires_build_efi"
        exit 1
    }

    if ($EnableM04LayoutContractProbe -and
        $SkipImageBuild) {
        Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_probe_forbids_skip_image_build"
        exit 1
    }

    foreach ($requestedMarker in @($RequireMarker) + @($RequireOrderedMarker) + @($RequireUniqueMarker)) {
        if (-not $EnableM04RhpNewFastProbe -and $null -ne $requestedMarker -and
            [regex]::IsMatch($requestedMarker, '(?i)CLROS_M04_007|M04_RHP_|m04\.007\.rhp_new_fast')) {
            Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_rhp_new_fast_marker_requires_typed_profile"
            exit 1
        }
        if (-not $EnableM04ManagedHeapPageRangeProbe -and $null -ne $requestedMarker -and
            [regex]::IsMatch($requestedMarker, '(?i)CLROS_M04_006|M04_HEAP_|m04\.006')) {
            Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_managed_heap_marker_requires_typed_profile"
            exit 1
        }
        if (-not $EnableM04LayoutContractProbe -and
            $null -ne $requestedMarker -and
            $requestedMarker.IndexOf("CLROS_M04_005", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Write-Host "TEST_FAIL qemu.smoke.invalid_args m04_layout_contract_marker_requires_typed_profile"
            exit 1
        }
    }

    if ($EnableM02NormalizedInterruptFramePageProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM02NormalizedInterruptFramePageProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_normalized_interrupt_frame_page_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02NormalizedInterruptFramePageProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_normalized_interrupt_frame_page_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedIdtGatePopulationProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM02OwnedIdtGatePopulationProbe -and
        $EnableM02NormalizedInterruptFramePageProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe")
        exit 1
    }

    if ($EnableM02OwnedIdtGatePopulationProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_idt_gate_population_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02OwnedIdtGatePopulationProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_idt_gate_population_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM04ArithmeticConformanceProbe -and
        -not $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_arithmetic_conformance_probe_requires_span_memory_conformance")
        exit 1
    }

    if ($EnableM04ArithmeticConformanceProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_arithmetic_conformance_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM04ArithmeticConformanceProbe -and
        -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_arithmetic_conformance_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM04ArithmeticConformanceProbe -and
        $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_arithmetic_conformance_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM04ArithmeticConformanceProbe -and
        $EnableM02Ud2VectorStateProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_arithmetic_conformance_probe_forbids_m02_ud2_vector_state")
        exit 1
    }

    if ($EnableM02IdtrContinuedExecutionProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM02IdtrContinuedExecutionProbe -and
        $EnableM02NormalizedInterruptFramePageProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe")
        exit 1
    }

    if ($EnableM02IdtrContinuedExecutionProbe -and
        $EnableM02OwnedIdtGatePopulationProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe")
        exit 1
    }

    if ($EnableM02IdtrContinuedExecutionProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02IdtrContinuedExecutionProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_probe_forbids_skip_image_build")
        exit 1
    }

    $userRequestedM02IdtrContinuedExecutionMarker = @(
        ConvertTo-QemuSmokeMarkerList -Values @(
            $RequireMarker
            $RequireOrderedMarker
            $RequireUniqueMarker
        ) | Where-Object {
            Test-M02IdtrContinuedExecutionOwnedMarker -Marker $_
        }
    ).Count -ne 0
    if (-not $EnableM02IdtrContinuedExecutionProbe -and
        $userRequestedM02IdtrContinuedExecutionMarker) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_idtr_continued_execution_marker_requires_typed_profile")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        -not $EnableM02PanicSafeOwnedCom1Probe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_requires_panic_safe_owned_com1")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02OwnedX64GdtEncodingProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02GuardedIstStackAllocationProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02NormalizedInterruptFramePageProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02OwnedIdtGatePopulationProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM02IdtrContinuedExecutionProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_m04_span_memory_conformance_probe")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02Ud2VectorStateProbe -and
        -not [string]::IsNullOrWhiteSpace($M01RawObservationLog)) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_probe_forbids_m01_raw_observation_log")
        exit 1
    }

    if ($EnableM02PageFaultCr2ErrorCodeProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_page_fault_cr2_error_code_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02PageFaultCr2ErrorCodeProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_page_fault_cr2_error_code_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02PageFaultCr2ErrorCodeProbe -and
        -not [string]::IsNullOrWhiteSpace($M01RawObservationLog)) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_page_fault_cr2_error_code_probe_forbids_m01_raw_observation_log")
        exit 1
    }

    $userRequestedM02Ud2VectorStateMarker = @(
        ConvertTo-QemuSmokeMarkerList -Values @(
            $RequireMarker
            $RequireOrderedMarker
            $RequireUniqueMarker
        ) | Where-Object {
            Test-M02Ud2VectorStateOwnedMarker -Marker $_
        }
    ).Count -ne 0
    if (-not $EnableM02Ud2VectorStateProbe -and
        $userRequestedM02Ud2VectorStateMarker) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_ud2_vector_state_marker_requires_typed_profile")
        exit 1
    }
    $userRequestedM02PageFaultCr2ErrorCodeMarker = @(
        ConvertTo-QemuSmokeMarkerList -Values @(
            $RequireMarker
            $RequireOrderedMarker
            $RequireUniqueMarker
        ) | Where-Object {
            Test-M02PageFaultCr2ErrorCodeOwnedMarker -Marker $_
        }
    ).Count -ne 0
    if (-not $EnableM02PageFaultCr2ErrorCodeProbe -and
        $userRequestedM02PageFaultCr2ErrorCodeMarker) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_page_fault_cr2_error_code_marker_requires_typed_profile")
        exit 1
    }

    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
        -not $EnableM04IndependentEndianConformanceProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_span_memory_conformance_probe_requires_independent_endian_conformance")
        exit 1
    }

    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
        $EnableM02OwnedGdtTssTerminalProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_span_memory_conformance_probe_forbids_owned_gdt_tss_terminal")
        exit 1
    }

    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
        -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_span_memory_conformance_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -and
        $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_span_memory_conformance_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM04IndependentEndianConformanceProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_independent_endian_conformance_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM04IndependentEndianConformanceProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m04_independent_endian_conformance_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedGdtTssTerminalProbe -and
        -not $EnableM02GuardedIstStackAllocationProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_gdt_tss_terminal_probe_requires_guarded_ist_stack_allocation")
        exit 1
    }

    if ($EnableM02OwnedGdtTssTerminalProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_gdt_tss_terminal_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02OwnedGdtTssTerminalProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_gdt_tss_terminal_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedGdtTssTerminalProbe -and
        -not [string]::IsNullOrWhiteSpace($M01RawObservationLog)) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_gdt_tss_terminal_probe_forbids_m01_raw_observation_log")
        exit 1
    }

    if ($EnableM02GuardedIstStackAllocationProbe -and
        -not $EnableM02OwnedX64GdtEncodingProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_guarded_ist_stack_allocation_probe_requires_owned_x64_gdt_encoding")
        exit 1
    }

    if ($EnableM02GuardedIstStackAllocationProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_guarded_ist_stack_allocation_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02GuardedIstStackAllocationProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_guarded_ist_stack_allocation_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedX64GdtEncodingProbe -and
        -not $EnableM02PanicSafeOwnedCom1Probe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_x64_gdt_encoding_probe_requires_panic_safe_owned_com1")
        exit 1
    }

    if ($EnableM02OwnedX64GdtEncodingProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_x64_gdt_encoding_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02OwnedX64GdtEncodingProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_x64_gdt_encoding_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedCom1ReadyProbe -and
        -not $EnableM02Com1OwnedPortIoProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_com1_ready_probe_requires_com1_owned_port_io")
        exit 1
    }

    if ($EnableM02PanicSafeOwnedCom1Probe -and
        -not $EnableM02OwnedCom1ReadyProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_panic_safe_owned_com1_probe_requires_owned_com1_ready")
        exit 1
    }

    if ($EnableM02PanicSafeOwnedCom1Probe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_panic_safe_owned_com1_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02PanicSafeOwnedCom1Probe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_panic_safe_owned_com1_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02OwnedCom1ReadyProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_com1_ready_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02OwnedCom1ReadyProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_owned_com1_ready_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02Com1OwnedPortIoProbe -and
        -not $EnableM02CheckedPortIoBindingProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_com1_owned_port_io_probe_requires_checked_binding")
        exit 1
    }

    if ($EnableM02Com1OwnedPortIoProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_com1_owned_port_io_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02Com1OwnedPortIoProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_com1_owned_port_io_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02CheckedPortIoBindingProbe -and
        -not $EnableM02SafeStubProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_checked_port_io_binding_probe_requires_safe_stub")
        exit 1
    }

    if ($EnableM02CheckedPortIoBindingProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_checked_port_io_binding_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02CheckedPortIoBindingProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_checked_port_io_binding_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM02SafeStubProbe -and -not $EnableM01LoadedImageStackProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_safe_stub_probe_requires_loaded_image_stack")
        exit 1
    }

    if ($EnableM02SafeStubProbe -and -not $EnableM01LiveAllocatorProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_safe_stub_probe_requires_live_allocator")
        exit 1
    }

    if ($EnableM02SafeStubProbe -and -not $EnableM01OwnedPagingProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_safe_stub_probe_requires_owned_paging")
        exit 1
    }

    if ($EnableM02SafeStubProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_safe_stub_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM02SafeStubProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m02_safe_stub_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM01OwnedPagingProbe -and -not $EnableM01LiveAllocatorProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_owned_paging_probe_requires_live_allocator")
        exit 1
    }

    if ($EnableM01OwnedPagingProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_owned_paging_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM01OwnedPagingProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_owned_paging_probe_forbids_skip_image_build")
        exit 1
    }

    if (-not [string]::IsNullOrWhiteSpace($M01RawObservationLog) -and
        -not $EnableM01OwnedPagingProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_raw_observation_log_requires_owned_paging_probe")
        exit 1
    }

    if ($EnableM01OwnedPagingProbe -and
        ($TimeoutSeconds -lt 1 -or $TimeoutSeconds -gt 300)) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_owned_paging_timeout_must_be_1_through_300")
        exit 1
    }

    $m01RawObservationConfig =
        "M01_RAW_OBSERVATION_CONFIG enabled=" +
        (Format-QemuSmokeBoolean $EnableM01OwnedPagingProbe.IsPresent) +
        " active=" +
        (Format-QemuSmokeBoolean (
            $EnableM01OwnedPagingProbe.IsPresent -and
            -not $EnableM02OwnedGdtTssTerminalProbe.IsPresent -and
            -not $EnableM02Ud2VectorStateProbe.IsPresent -and
            -not $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent -and
            -not $DryRun.IsPresent))
    if ($EnableM02OwnedGdtTssTerminalProbe.IsPresent -or
        $EnableM02Ud2VectorStateProbe.IsPresent -or $EnableM02PageFaultCr2ErrorCodeProbe.IsPresent) {
        $m01RawObservationConfig += " terminalProfile=true"
    }
    $m01RawObservationConfig +=
        " dryRun=" +
        (Format-QemuSmokeBoolean $DryRun.IsPresent)
    Write-Host $m01RawObservationConfig

    if ($EnableM01LiveAllocatorProbe -and -not $EnableM01LoadedImageStackProbe) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_live_allocator_probe_requires_loaded_image_stack")
        exit 1
    }

    if ($EnableM01LiveAllocatorProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_live_allocator_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM01LiveAllocatorProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_live_allocator_probe_forbids_skip_image_build")
        exit 1
    }

    if ($EnableM01LoadedImageStackProbe -and -not $BuildEfi) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_loaded_image_stack_probe_requires_build_efi")
        exit 1
    }

    if ($EnableM01LoadedImageStackProbe -and $SkipImageBuild) {
        Write-Host (
            "TEST_FAIL qemu.smoke.invalid_args " +
            "m01_loaded_image_stack_probe_forbids_skip_image_build")
        exit 1
    }

    $markerPresets = @()
    foreach ($presetValue in $MarkerPreset) {
        foreach ($preset in ($presetValue -split ",")) {
            if (-not [string]::IsNullOrWhiteSpace($preset)) {
                $normalizedPreset = Normalize-MarkerPreset -Preset $preset
                if ($markerPresets -notcontains $normalizedPreset) {
                    $markerPresets += $normalizedPreset
                }
            }
        }
    }

    if ($markerPresets.Count -gt 0) {
        Write-Host ("QEMU_MARKER_PRESETS " + ($markerPresets -join ";"))
    }

    if (-not $DryRun) {
        $hostPreflight = Invoke-QemuSmokeHostPreflight -RequestedQemu $Qemu -RequestedOvmfCode $OvmfCode
        $Qemu = $hostPreflight.QemuPath
        $OvmfCode = $hostPreflight.OvmfCodePath
    }

    if (-not $DryRun -and $BuildEfi) {
        $buildArguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $PSScriptRoot "build-efi-shell.ps1"),
            "-EfiOutputPath",
            $EfiArtifact
        )

        if ($EnableUefiGoneSimulationProbe) {
            $buildArguments += "-EnableUefiGoneSimulationProbe"
        }

        if ($EnableExitBootServicesFreshMapProbe) {
            $buildArguments += "-EnableExitBootServicesFreshMapProbe"
        }

        if ($EnableAcpiPhysicalDiscoveryProbe) {
            $buildArguments += "-EnableAcpiPhysicalDiscoveryProbe"
        }

        if ($EnableM01LoadedImageStackProbe) {
            $buildArguments += "-EnableM01LoadedImageStackProbe"
        }

        if ($EnableM01LiveAllocatorProbe) {
            $buildArguments += "-EnableM01LiveAllocatorProbe"
        }

        if ($EnableM01OwnedPagingProbe) {
            $buildArguments += "-EnableM01OwnedPagingProbe"
        }

        if ($EnableM02SafeStubProbe) {
            $buildArguments += "-EnableM02SafeStubProbe"
        }

        if ($EnableM02CheckedPortIoBindingProbe) {
            $buildArguments += "-EnableM02CheckedPortIoBindingProbe"
        }

        if ($EnableM02Com1OwnedPortIoProbe) {
            $buildArguments += "-EnableM02Com1OwnedPortIoProbe"
        }

        if ($EnableM02OwnedCom1ReadyProbe) {
            $buildArguments += "-EnableM02OwnedCom1ReadyProbe"
        }

        if ($EnableM02PanicSafeOwnedCom1Probe) {
            $buildArguments += "-EnableM02PanicSafeOwnedCom1Probe"
        }

        if ($EnableM02OwnedX64GdtEncodingProbe) {
            $buildArguments += "-EnableM02OwnedX64GdtEncodingProbe"
        }

        if ($EnableM02GuardedIstStackAllocationProbe) {
            $buildArguments +=
                "-EnableM02GuardedIstStackAllocationProbe"
        }

        if ($EnableM02OwnedGdtTssTerminalProbe) {
            $buildArguments +=
                "-EnableM02OwnedGdtTssTerminalProbe"
        }

        if ($EnableM02NormalizedInterruptFramePageProbe) {
            $buildArguments +=
                "-EnableM02NormalizedInterruptFramePageProbe"
        }

        if ($EnableM02OwnedIdtGatePopulationProbe) {
            $buildArguments +=
                "-EnableM02OwnedIdtGatePopulationProbe"
        }

        if ($EnableM02IdtrContinuedExecutionProbe) {
            $buildArguments +=
                "-EnableM02IdtrContinuedExecutionProbe"
        }

        if ($EnableM02Ud2VectorStateProbe) {
            $buildArguments += "-EnableM02Ud2VectorStateProbe"
        }

        if ($EnableM02PageFaultCr2ErrorCodeProbe) {
            $buildArguments += "-EnableM02PageFaultCr2ErrorCodeProbe"
        }

        if ($EnableM04IndependentEndianConformanceProbe) {
            $buildArguments +=
                "-EnableM04IndependentEndianConformanceProbe"
        }

        if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
            $buildArguments +=
                "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
        }
        if ($EnableM04ArithmeticConformanceProbe) {
            $buildArguments +=
                "-EnableM04ArithmeticConformanceProbe"
        }
        if ($EnableM04LayoutContractProbe) {
            $buildArguments +=
                "-EnableM04LayoutContractProbe"
        }
        if ($EnableM04ManagedHeapPageRangeProbe) {
            $buildArguments +=
                "-EnableM04ManagedHeapPageRangeProbe"
        }
        if ($EnableM04RhpNewFastProbe) {
            $buildArguments +=
                "-EnableM04RhpNewFastProbe"
        }
        if ($EnableM04RhpNewFastBeforeBindFailureProbe) {
            $buildArguments +=
                "-EnableM04RhpNewFastBeforeBindFailureProbe"
        }
        if ($EnableM04RhpNewFastExhaustionFailureProbe) {
            $buildArguments +=
                "-EnableM04RhpNewFastExhaustionFailureProbe"
        }

        Invoke-NativeCommand {
            & powershell.exe @buildArguments
        }
        if ($script:LastNativeExitCode -ne 0) {
            exit $script:LastNativeExitCode
        }
    }

    if (-not $DryRun -and -not $SkipImageBuild) {
        Invoke-NativeCommand {
            & dotnet run --project "tools/OS.ImageBuilder/OS.ImageBuilder.csproj" "-p:OSWorkloadProfile=$WorkloadProfile" -- create-raw --workload-profile $WorkloadProfile --efi $EfiArtifact --output $Image
        }
        if ($script:LastNativeExitCode -ne 0) {
            exit $script:LastNativeExitCode
        }
    }

    if (-not $DryRun) {
        Test-QemuSmokeImageArtifact -ImagePath $Image -ExpectedDiskFormat $DiskFormat
    }

    $runnerQemu = $Qemu
    $runnerTimeoutSeconds = $TimeoutSeconds
    if (-not $DryRun -and
        $EnableM01OwnedPagingProbe -and
        -not $EnableM02OwnedGdtTssTerminalProbe -and
        -not $EnableM02Ud2VectorStateProbe -and -not $EnableM02PageFaultCr2ErrorCodeProbe) {
        Write-Host "TEST_START m01.026.raw-observations"
        try {
            $m01RawObservationCapture =
                New-M01RawObservationCapture `
                    -RealQemuPath $Qemu `
                    -RequestedObservationLog $M01RawObservationLog `
                    -PublicSerialLog $SerialLog `
                    -SummaryLog $SummaryLog `
                    -GuestTimeoutSeconds $TimeoutSeconds `
                    -M02SafeStubEnabled $EnableM02SafeStubProbe.IsPresent
        }
        catch {
            Write-Host (
                "TEST_FAIL m01.026.raw-observations reason=" +
                $_.Exception.Message)
            exit 90
        }
        $runnerQemu = $m01RawObservationCapture.ShimExecutablePath
        $runnerTimeoutSeconds =
            $m01RawObservationCapture.RunnerTimeoutSeconds
        Write-Host (
            "M01_RAW_OBSERVATION_PATH " +
            $m01RawObservationCapture.ObservationPath)
        Write-Host (
            "M01_RAW_OBSERVATION_SUPPORT " +
            $m01RawObservationCapture.SupportDirectory)
        Write-Host (
            "M01_RAW_OBSERVATION_TIMEOUTS guestSeconds=" +
            $m01RawObservationCapture.GuestTimeoutSeconds +
            " captureSeconds=" +
            $m01RawObservationCapture.CaptureTimeoutSeconds +
            " runnerSeconds=" +
            $m01RawObservationCapture.RunnerTimeoutSeconds)
        Write-Host (
            "M01_RAW_OBSERVATION_IDENTITIES realQemuSha256=" +
            $m01RawObservationCapture.RealQemuSha256 +
            " sourceSha256=" +
            $m01RawObservationCapture.SourceSha256 +
            " projectSha256=" +
            $m01RawObservationCapture.ProjectSha256 +
            " shimSha256=" +
            $m01RawObservationCapture.ShimExecutableSha256)
    }

    $arguments = @(
        "--project", "tools/OS.QemuRunner/OS.QemuRunner.csproj",
        "-p:OSWorkloadProfile=$WorkloadProfile",
        "--",
        "--image", $Image,
        "--disk-format", $DiskFormat,
        "--workload-profile", $WorkloadProfile,
        "--serial-log", $SerialLog,
        "--summary-log", $SummaryLog,
        "--timeout", "$runnerTimeoutSeconds"
    )

    if ($EnableM02OwnedGdtTssTerminalProbe) {
        $arguments += "--replace-default-required-markers"
    }

    foreach ($marker in $requiredMarkers) {
        $arguments += @("--require-marker", $marker)
    }

    foreach ($marker in $orderedMarkers) {
        $arguments += @("--require-ordered-marker", $marker)
    }

    foreach ($marker in $uniqueMarkers) {
        $arguments += @("--require-unique-marker", $marker)
    }

    foreach ($preset in $markerPresets) {
        $arguments += @("--marker-preset", $preset)
    }

    foreach ($marker in $forbiddenMarkers) {
        $arguments += @("--forbid-marker", $marker)
    }

    if ($runnerQemu.Length -gt 0) {
        $arguments += @("--qemu", $runnerQemu)
    }

    if ($OvmfCode.Length -gt 0) {
        $arguments += @("--ovmf-code", $OvmfCode)
    }

    if ($DryRun) {
        $arguments += "--dry-run"
    }

    if ($null -ne $m01RawObservationCapture) {
        try {
            $m01RawObservationCapture.EnvironmentState =
                Enter-M01RawObservationEnvironment `
                    -Capture $m01RawObservationCapture
            Invoke-NativeCommand {
                & dotnet run @arguments
            }
        }
        finally {
            Restore-M01RawObservationEnvironment `
                -State $m01RawObservationCapture.EnvironmentState
            $m01RawObservationCapture.EnvironmentState = $null
        }
    }
    elseif (($EnableM04ManagedHeapPageRangeProbe -or $EnableM02PageFaultCr2ErrorCodeProbe) -and -not $DryRun) {
        $heapRunnerLines = @(Invoke-NativeCommand { & dotnet run @arguments })
        foreach ($line in $heapRunnerLines) { Write-Host $line }
    }
    else {
        Invoke-NativeCommand {
            & dotnet run @arguments
        }
    }
    $runnerExitCode = $script:LastNativeExitCode

    if ($null -ne $m01RawObservationCapture) {
        if ($runnerExitCode -ne 0) {
            Remove-M01RawObservationTransient `
                -Capture $m01RawObservationCapture `
                -BestEffort
            Write-Host (
                "TEST_FAIL m01.026.raw-observations reason=" +
                "qemu_runner_exit_" +
                $runnerExitCode)
            exit $runnerExitCode
        }

        try {
            $m01RawObservationEvidence =
                Assert-M01RawObservationSidecar `
                    -Capture $m01RawObservationCapture
            Remove-M01RawObservationTransient `
                -Capture $m01RawObservationCapture
            Write-Host (
                "M01_RAW_OBSERVATION_EVIDENCE " +
                "observationSha256=" +
                $m01RawObservationEvidence.ObservationSha256 +
                " qmpTranscriptSha256=" +
                $m01RawObservationEvidence.QmpTranscriptSha256 +
                " serialSha256=" +
                $m01RawObservationEvidence.SerialSha256 +
                " memorySha256=" +
                $m01RawObservationEvidence.MemorySha256 +
                " preflightCandidates=" +
                $m01RawObservationEvidence.CandidateCount +
                " cr0=" +
                $m01RawObservationEvidence.Cr0 +
                " firmwareCr3=" +
                $m01RawObservationEvidence.FirmwareCr3 +
                " rawCr4=" +
                $m01RawObservationEvidence.RawCr4 +
                " ia32Efer=" +
                $m01RawObservationEvidence.Ia32Efer +
                " cpuidAddressSizesEax=" +
                $m01RawObservationEvidence.CpuidAddressSizesEax +
                " physicalAddressWidth=" +
                $m01RawObservationEvidence.PhysicalAddressWidth +
                " rflags=" +
                $m01RawObservationEvidence.Rflags)
            Write-Host "TEST_PASS m01.026.raw-observations"
        }
        catch {
            Remove-M01RawObservationTransient `
                -Capture $m01RawObservationCapture `
                -BestEffort
            Write-Host (
                "TEST_FAIL m01.026.raw-observations reason=" +
                $_.Exception.Message)
            exit 91
        }
    }

    if ($EnableM02Ud2VectorStateProbe -and
        -not $DryRun -and
        $runnerExitCode -eq 0) {
        try {
            Assert-M02Ud2VectorStateTerminalTranscript `
                -SerialLogPath $SerialLog
        }
        catch {
            Write-Host (
                "TEST_FAIL qemu.smoke.m02.013 terminal_transcript_invalid reason=" +
                $_.Exception.Message)
            exit 92
        }
        Write-Host (
            "QEMU_SCRIPT_M02_UD2_VECTOR_STATE_TRANSCRIPT " +
            "postSmokeSequence=true postPassMarkerNoise=false")
    }
    if ($EnableM02PageFaultCr2ErrorCodeProbe -and -not $DryRun -and $runnerExitCode -eq 0) {
        try {
            Assert-M02PageFaultRunnerResult -Lines $heapRunnerLines
            Assert-M02PageFaultCr2ErrorCodeTranscript -SerialLogPath $SerialLog
        }
        catch {
            Write-Host ("TEST_FAIL qemu.smoke.m02.014 terminal_transcript_invalid reason=" + $_.Exception.Message)
            exit 95
        }
        Write-Host "QEMU_SCRIPT_M02_PAGE_FAULT_TRANSCRIPT postSmokeSequence=true rawObservations=true guardAbsence=true postPassMarkerNoise=false"
    }
    if ($EnableM04RhpNewFastProbe -and -not $DryRun -and $runnerExitCode -eq 0) {
        try {
            Assert-M04ManagedHeapRunnerResult -Lines $heapRunnerLines
            Assert-M04RhpNewFastTranscript -SerialLogPath $SerialLog
        }
        catch {
            Write-Host ("TEST_FAIL qemu.smoke.m04.007 strict_terminal_transcript_invalid reason=" + $_.Exception.Message)
            exit 96
        }
        Write-Host "QEMU_SCRIPT_M04_RHP_NEW_FAST_TRANSCRIPT exactWholeLines=true lines=78 objects=10 compiledNew=4 direct=6 terminal=true"
    }
    elseif ($EnableM04ManagedHeapPageRangeProbe -and -not $DryRun -and $runnerExitCode -eq 0) {
        try {
            Assert-M04ManagedHeapRunnerResult -Lines $heapRunnerLines
            Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $SerialLog
        }
        catch {
            Write-Host ("TEST_FAIL qemu.smoke.m04.006 strict_terminal_transcript_invalid reason=" + $_.Exception.Message)
            exit 94
        }
        Write-Host "QEMU_SCRIPT_M04_MANAGED_HEAP_TRANSCRIPT exactWholeLines=true lines=52 retained=true terminal=true"
    }
    elseif ($EnableM04LayoutContractProbe -and -not $DryRun -and $runnerExitCode -eq 0) {
        try {
            Assert-M04LayoutContractTranscript -SerialLogPath $SerialLog
        }
        catch {
            Write-Host ("TEST_FAIL qemu.smoke.m04.005 strict_transcript_invalid reason=" + $_.Exception.Message)
            exit 93
        }
        Write-Host "QEMU_SCRIPT_M04_LAYOUT_CONTRACT_TRANSCRIPT exactWholeLines=true orderedChain=true uniqueChain=true"
    }
    if (-not $EnableM02PageFaultCr2ErrorCodeProbe -and -not $DryRun -and $runnerExitCode -eq 0) {
        $serialPath = if ([IO.Path]::IsPathRooted($SerialLog)) { $SerialLog } else { Join-Path $root $SerialLog }
        if (Test-M02PageFaultCr2ErrorCodeOwnedMarker -Marker ([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($serialPath)))) {
            Write-Host 'TEST_FAIL qemu.smoke.m02.014 unexpected_profile_record'
            exit 95
        }
    }
    exit $runnerExitCode
}
finally {
    if ($null -ne $m01RawObservationCapture) {
        Restore-M01RawObservationEnvironment `
            -State $m01RawObservationCapture.EnvironmentState `
            -BestEffort
        Remove-M01RawObservationTransient `
            -Capture $m01RawObservationCapture `
            -BestEffort
    }
    Pop-Location
}
