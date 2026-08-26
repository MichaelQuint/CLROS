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
    [switch] $EnableM04IndependentEndianConformanceProbe,
    [switch] $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe,
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

    if (($EnableM02OwnedGdtTssTerminalProbe -or
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

    if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
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
        "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " +
        (Format-QemuSmokeMarkerList $orderedMarkers))
    Write-Host (
        "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " +
        (Format-QemuSmokeMarkerList $uniqueMarkers))

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
            -not $DryRun.IsPresent))
    if ($EnableM02OwnedGdtTssTerminalProbe.IsPresent) {
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

        if ($EnableM04IndependentEndianConformanceProbe) {
            $buildArguments +=
                "-EnableM04IndependentEndianConformanceProbe"
        }

        if ($EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe) {
            $buildArguments +=
                "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
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
        -not $EnableM02OwnedGdtTssTerminalProbe) {
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
