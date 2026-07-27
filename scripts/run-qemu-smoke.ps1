param(
    [string] $Image = "artifacts/images/os.raw",
    [string] $EfiArtifact = "artifacts/efi/BOOTX64.EFI",
    [string] $SerialLog = "artifacts/logs/qemu-smoke.log",
    [string] $SummaryLog = "artifacts/logs/qemu-smoke.summary.log",
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
        "QEMU_SCRIPT_REQUIRE_ORDERED_MARKERS " +
        (Format-QemuSmokeMarkerList $orderedMarkers))
    Write-Host (
        "QEMU_SCRIPT_REQUIRE_UNIQUE_MARKERS " +
        (Format-QemuSmokeMarkerList $uniqueMarkers))

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

    $forbiddenMarkers = @(
        ConvertTo-QemuSmokeMarkerList -Values $ForbidMarker
    )

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

    $arguments = @(
        "--project", "tools/OS.QemuRunner/OS.QemuRunner.csproj",
        "-p:OSWorkloadProfile=$WorkloadProfile",
        "--",
        "--image", $Image,
        "--disk-format", $DiskFormat,
        "--workload-profile", $WorkloadProfile,
        "--serial-log", $SerialLog,
        "--summary-log", $SummaryLog,
        "--timeout", "$TimeoutSeconds"
    )

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

    if ($Qemu.Length -gt 0) {
        $arguments += @("--qemu", $Qemu)
    }

    if ($OvmfCode.Length -gt 0) {
        $arguments += @("--ovmf-code", $OvmfCode)
    }

    if ($DryRun) {
        $arguments += "--dry-run"
    }

    Invoke-NativeCommand {
        & dotnet run @arguments
    }
    exit $script:LastNativeExitCode
}
finally {
    Pop-Location
}
