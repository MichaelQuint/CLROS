param(
    [string] $Image = "artifacts/images/os.raw",
    [string] $DiskFormat = "raw",
    [string] $SerialLog = "artifacts/logs/libvirt-smoke.log",
    [string] $SummaryLog = "artifacts/logs/libvirt-smoke.summary.log",
    [string] $DomainXml = "artifacts/logs/libvirt-smoke.xml",
    [string] $LifecyclePlan = "artifacts/logs/libvirt-lifecycle.plan.log",
    [string] $OvmfLoader = "",
    [string] $Network = "none",
    [string] $Virsh = "",
    [string] $WorkloadProfile = "SingleMinimalApi",
    [int] $TimeoutSeconds = 30,
    [string[]] $RequireMarker = @(
        "OS_SMOKE_PASS",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY"
    ),
    [string[]] $ForbidMarker = @(),
    [switch] $DryRun,
    [switch] $PlanLifecycle,
    [switch] $CheckSerialOnly,
    [switch] $AllowRealLibvirtLifecycle,
    [switch] $ValidatePaths
)

$ErrorActionPreference = "Stop"
$script:LastNativeExitCode = 0
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath

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

function Split-Markers {
    param([string[]] $Markers)

    $result = @()
    foreach ($markerValue in $Markers) {
        foreach ($marker in ($markerValue -split ",")) {
            if (-not [string]::IsNullOrWhiteSpace($marker)) {
                $result += $marker.Trim()
            }
        }
    }

    return $result
}

function Format-Markers {
    param([string[]] $Markers)

    if ($Markers.Count -eq 0) {
        return "none"
    }

    return ($Markers -join ";")
}

function Resolve-VirshPath {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).ProviderPath
        }

        return ""
    }

    $environmentPath = [Environment]::GetEnvironmentVariable("VIRSH")
    if (-not [string]::IsNullOrWhiteSpace($environmentPath)) {
        if (Test-Path -LiteralPath $environmentPath) {
            return (Resolve-Path -LiteralPath $environmentPath).ProviderPath
        }

        return ""
    }

    $pathCommand = Get-Command "virsh.exe" -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    $pathCommand = Get-Command "virsh" -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    return ""
}

function Invoke-NativeCommandCapture {
    param([string[]] $Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & dotnet run @Arguments 2>&1
        $script:LastNativeExitCode = $LASTEXITCODE
        return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).TrimEnd()
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

Push-Location $root
try {
    Assert-WorkloadProfile -Profile $WorkloadProfile
    $msbuildProfileArgument = "/p:OSWorkloadProfile=$WorkloadProfile"

    $DiskFormat = $DiskFormat.ToLowerInvariant()
    if ($DiskFormat -ne "raw" -and $DiskFormat -ne "qcow2") {
        Write-Host ("TEST_FAIL libvirt.smoke.invalid_args disk_format=" + $DiskFormat + " valid=raw,qcow2")
        exit 1
    }

    $Network = $Network.ToLowerInvariant()
    if ($Network -ne "none" -and $Network -ne "default") {
        Write-Host ("TEST_FAIL libvirt.smoke.invalid_args network=" + $Network + " valid=none,default")
        exit 1
    }

    $requiredMarkers = Split-Markers -Markers $RequireMarker
    $forbiddenMarkers = Split-Markers -Markers $ForbidMarker
    if ($requiredMarkers.Count -eq 0) {
        Write-Host "TEST_FAIL libvirt.smoke.invalid_args missing_required_marker"
        exit 1
    }

    Write-Host "TEST_START libvirt.smoke"
    Write-WorkloadProfileEvidence -Profile $WorkloadProfile
    Write-Host ("LIBVIRT_IMAGE_ARTIFACT " + $Image)
    Write-Host ("LIBVIRT_DISK_FORMAT " + $DiskFormat)
    Write-Host ("LIBVIRT_SERIAL_LOG " + $SerialLog)
    Write-Host ("LIBVIRT_SUMMARY_LOG " + $SummaryLog)
    Write-Host ("LIBVIRT_DOMAIN_XML " + $DomainXml)
    Write-Host ("LIBVIRT_LIFECYCLE_PLAN " + $LifecyclePlan)
    Write-Host ("LIBVIRT_TIMEOUT_SECONDS " + $TimeoutSeconds)
    if ($AllowRealLibvirtLifecycle) {
        Write-Host "LIBVIRT_REAL_LIFECYCLE_OPT_IN true"
    }
    else {
        Write-Host "LIBVIRT_REAL_LIFECYCLE_OPT_IN false"
    }
    Write-Host ("REQUIRED_MARKERS " + (Format-Markers -Markers $requiredMarkers))
    Write-Host ("FORBIDDEN_MARKERS " + (Format-Markers -Markers $forbiddenMarkers))

    if ($CheckSerialOnly) {
        Write-Host "LIBVIRT_CHECK_SERIAL_ONLY"
        $checkArguments = @(
            "--project", "tools/OS.LibvirtRunner/OS.LibvirtRunner.csproj",
            $msbuildProfileArgument,
            "--",
            "check-serial",
            "--serial-log", $SerialLog,
            "--summary-log", $SummaryLog
        )

        foreach ($marker in $requiredMarkers) {
            $checkArguments += @("--require-marker", $marker)
        }

        foreach ($marker in $forbiddenMarkers) {
            $checkArguments += @("--forbid-marker", $marker)
        }

        $summaryDirectory = Split-Path -Parent $SummaryLog
        if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
            New-Item -ItemType Directory -Force -Path $summaryDirectory | Out-Null
        }

        $summary = Invoke-NativeCommandCapture -Arguments $checkArguments
        if (-not [string]::IsNullOrWhiteSpace($summary)) {
            foreach ($summaryLine in ($summary -split "`r?`n")) {
                if (-not [string]::IsNullOrWhiteSpace($summaryLine)) {
                    Write-Host $summaryLine
                }
            }
        }

        if ($script:LastNativeExitCode -ne 0) {
            Write-Host ("TEST_FAIL libvirt.smoke.check_serial exit=" + $script:LastNativeExitCode)
            exit $script:LastNativeExitCode
        }

        Write-Host "TEST_PASS libvirt.smoke.check_serial"
        exit 0
    }

    $xmlArguments = @(
        "--project", "tools/OS.LibvirtRunner/OS.LibvirtRunner.csproj",
        $msbuildProfileArgument,
        "--",
        "generate-xml",
        "--image", $Image,
        "--disk-format", $DiskFormat,
        "--serial-log", $SerialLog,
        "--network", $Network
    )

    foreach ($marker in $requiredMarkers) {
        $xmlArguments += @("--require-marker", $marker)
    }

    foreach ($marker in $forbiddenMarkers) {
        $xmlArguments += @("--forbid-marker", $marker)
    }

    if ($OvmfLoader.Length -gt 0) {
        $xmlArguments += @("--ovmf-loader", $OvmfLoader)
    }

    if ($ValidatePaths) {
        $xmlArguments += "--validate-paths"
    }

    $domainDirectory = Split-Path -Parent $DomainXml
    if (-not [string]::IsNullOrWhiteSpace($domainDirectory)) {
        New-Item -ItemType Directory -Force -Path $domainDirectory | Out-Null
    }

    $xml = Invoke-NativeCommandCapture -Arguments $xmlArguments
    if ($script:LastNativeExitCode -ne 0) {
        Write-Host ("TEST_FAIL libvirt.smoke.generate_xml exit=" + $script:LastNativeExitCode)
        exit $script:LastNativeExitCode
    }

    Set-Content -LiteralPath $DomainXml -Value $xml
    Write-Host ("LIBVIRT_XML_ARTIFACT " + $DomainXml)

    if ($PlanLifecycle) {
        $planArguments = @(
            "--project", "tools/OS.LibvirtRunner/OS.LibvirtRunner.csproj",
            $msbuildProfileArgument,
            "--",
            "plan-lifecycle",
            "--image", $Image,
            "--disk-format", $DiskFormat,
            "--serial-log", $SerialLog,
            "--network", $Network,
            "--timeout-seconds", $TimeoutSeconds.ToString()
        )

        foreach ($marker in $requiredMarkers) {
            $planArguments += @("--require-marker", $marker)
        }

        foreach ($marker in $forbiddenMarkers) {
            $planArguments += @("--forbid-marker", $marker)
        }

        if ($OvmfLoader.Length -gt 0) {
            $planArguments += @("--ovmf-loader", $OvmfLoader)
        }

        if ($ValidatePaths) {
            $planArguments += "--validate-paths"
        }

        if ($AllowRealLibvirtLifecycle) {
            $planArguments += "--allow-real-libvirt-lifecycle"
        }

        $planDirectory = Split-Path -Parent $LifecyclePlan
        if (-not [string]::IsNullOrWhiteSpace($planDirectory)) {
            New-Item -ItemType Directory -Force -Path $planDirectory | Out-Null
        }

        $plan = Invoke-NativeCommandCapture -Arguments $planArguments
        if ($script:LastNativeExitCode -ne 0) {
            Write-Host ("TEST_FAIL libvirt.lifecycle.plan exit=" + $script:LastNativeExitCode)
            exit $script:LastNativeExitCode
        }

        Set-Content -LiteralPath $LifecyclePlan -Value $plan
        Write-Host ("LIBVIRT_LIFECYCLE_PLAN_ARTIFACT " + $LifecyclePlan)

        foreach ($planLine in ($plan -split "`r?`n")) {
            if (-not [string]::IsNullOrWhiteSpace($planLine)) {
                Write-Host $planLine
            }
        }

        Write-Host "TEST_PASS libvirt.lifecycle.plan"
        exit 0
    }

    if ($DryRun) {
        Write-Host "LIBVIRT_DRY_RUN"
        Write-Host "TEST_PASS libvirt.smoke.dryrun"
        exit 0
    }

    if (-not (Test-Path -LiteralPath $Image)) {
        Write-Host ("TEST_FAIL libvirt.smoke image_not_found path=" + $Image)
        exit 3
    }

    $resolvedVirsh = Resolve-VirshPath -RequestedPath $Virsh
    if ([string]::IsNullOrWhiteSpace($resolvedVirsh)) {
        Write-Host "TEST_SKIP libvirt.smoke missing_tool=virsh"
        Write-Host "TEST_PASS libvirt.smoke.missing_tool_reported"
        exit 0
    }

    Write-Host ("VIRSH_DISCOVERY result=" + $resolvedVirsh)
    if (-not $AllowRealLibvirtLifecycle) {
        Write-Host "TEST_SKIP libvirt.smoke real_lifecycle_requires_opt_in"
        Write-Host "TEST_PASS libvirt.smoke.real_lifecycle_guarded"
        exit 0
    }

    Write-Host "TEST_FAIL libvirt.smoke.real_run_not_implemented"
    exit 2
}
finally {
    Pop-Location
}
