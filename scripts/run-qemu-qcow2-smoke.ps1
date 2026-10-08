param(
    [string] $RawImage = "artifacts/images/os.raw",
    [string] $Qcow2Image = "artifacts/images/os.qcow2",
    [string] $EfiArtifact = "artifacts/efi/BOOTX64.EFI",
    [string] $SerialLog = "artifacts/logs/qemu-smoke-qcow2.log",
    [string] $SummaryLog = "artifacts/logs/qemu-smoke-qcow2.summary.log",
    [string] $Qemu = "",
    [string] $QemuImg = "",
    [string] $OvmfCode = "",
    [int] $TimeoutSeconds = 30,
    [string] $WorkloadProfile = "SingleMinimalApi",
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
    [switch] $DryRun,
    [switch] $BuildEfi,
    [switch] $SkipRawImageBuild
)

$ErrorActionPreference = "Stop"
$script:LastNativeExitCode = 0
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath

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

function Resolve-QemuImgPath {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return $RequestedPath
    }

    $environmentPath = [Environment]::GetEnvironmentVariable("QEMU_IMG")
    if (-not [string]::IsNullOrWhiteSpace($environmentPath)) {
        return $environmentPath
    }

    $pathCommand = Get-Command "qemu-img.exe" -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    $pathCommand = Get-Command "qemu-img" -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    $commonWindowsPath = "C:\Program Files\qemu\qemu-img.exe"
    if (Test-Path -LiteralPath $commonWindowsPath) {
        return $commonWindowsPath
    }

    return ""
}

function Fail-Step {
    param(
        [string] $Step,
        [int] $ExitCode
    )

    Write-Host ("TEST_FAIL qemu.qcow2.smoke step=" + $Step + " exit=" + $ExitCode)
    exit $ExitCode
}

Push-Location $root
try {
    Write-Host "TEST_START qemu.qcow2.smoke"
    Write-Host ("QCOW2_SOURCE_RAW " + $RawImage)
    Write-Host ("QCOW2_IMAGE_ARTIFACT " + $Qcow2Image)

    $resolvedQemuImg = Resolve-QemuImgPath -RequestedPath $QemuImg

    if ($DryRun) {
        Write-Host "QCOW2_DRY_RUN"

        $dryRunArguments = @(
            "--project", "tools/OS.ImageBuilder/OS.ImageBuilder.csproj",
            "-p:OSWorkloadProfile=$WorkloadProfile",
            "--",
            "convert-qcow2",
            "--input", $RawImage,
            "--output", $Qcow2Image,
            "--dry-run"
        )

        if ($resolvedQemuImg.Length -gt 0) {
            $dryRunArguments += @("--qemu-img", $resolvedQemuImg)
        }

        Invoke-NativeCommand {
            & dotnet run @dryRunArguments
        }
        if ($script:LastNativeExitCode -ne 0) {
            Fail-Step -Step "convert-qcow2-dry-run" -ExitCode $script:LastNativeExitCode
        }

        $smokeArguments = @(
            "-DryRun",
            "-BootPolicy", "Diagnostic",
            "-SkipImageBuild",
            "-Image", $Qcow2Image,
            "-DiskFormat", "qcow2",
            "-SerialLog", $SerialLog,
            "-SummaryLog", $SummaryLog,
            "-WorkloadProfile", $WorkloadProfile
        )

        if ($RequireMarker.Count -gt 0) {
            $smokeArguments += @("-RequireMarker", ($RequireMarker -join ","))
        }

        if ($Qemu.Length -gt 0) {
            $smokeArguments += @("-Qemu", $Qemu)
        }

        if ($OvmfCode.Length -gt 0) {
            $smokeArguments += @("-OvmfCode", $OvmfCode)
        }

        Invoke-NativeCommand {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "run-qemu-smoke.ps1") @smokeArguments
        }
        if ($script:LastNativeExitCode -ne 0) {
            Fail-Step -Step "qemu-smoke-dry-run" -ExitCode $script:LastNativeExitCode
        }

        Write-Host "TEST_PASS qemu.qcow2.smoke"
        exit 0
    }

    if ($BuildEfi) {
        Invoke-NativeCommand {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-efi-shell.ps1") -BootPolicy Diagnostic -EfiOutputPath $EfiArtifact
        }
        if ($script:LastNativeExitCode -ne 0) {
            Fail-Step -Step "build-efi" -ExitCode $script:LastNativeExitCode
        }
    }

    if (-not $SkipRawImageBuild) {
        Invoke-NativeCommand {
            & dotnet run --project "tools/OS.ImageBuilder/OS.ImageBuilder.csproj" "-p:OSWorkloadProfile=$WorkloadProfile" -- create-raw --workload-profile $WorkloadProfile --efi $EfiArtifact --output $RawImage
        }
        if ($script:LastNativeExitCode -ne 0) {
            Fail-Step -Step "create-raw" -ExitCode $script:LastNativeExitCode
        }
    }

    $convertArguments = @(
        "--project", "tools/OS.ImageBuilder/OS.ImageBuilder.csproj",
        "-p:OSWorkloadProfile=$WorkloadProfile",
        "--",
        "convert-qcow2",
        "--input", $RawImage,
        "--output", $Qcow2Image
    )

    if ($resolvedQemuImg.Length -gt 0) {
        $convertArguments += @("--qemu-img", $resolvedQemuImg)
    }

    Invoke-NativeCommand {
        & dotnet run @convertArguments
    }
    if ($script:LastNativeExitCode -ne 0) {
        Fail-Step -Step "convert-qcow2" -ExitCode $script:LastNativeExitCode
    }

    $realSmokeArguments = @(
        "-BootPolicy", "Diagnostic",
        "-SkipImageBuild",
        "-Image", $Qcow2Image,
        "-DiskFormat", "qcow2",
        "-SerialLog", $SerialLog,
        "-SummaryLog", $SummaryLog,
        "-TimeoutSeconds", "$TimeoutSeconds",
        "-WorkloadProfile", $WorkloadProfile
    )

    if ($RequireMarker.Count -gt 0) {
        $realSmokeArguments += @("-RequireMarker", ($RequireMarker -join ","))
    }

    if ($Qemu.Length -gt 0) {
        $realSmokeArguments += @("-Qemu", $Qemu)
    }

    if ($OvmfCode.Length -gt 0) {
        $realSmokeArguments += @("-OvmfCode", $OvmfCode)
    }

    Invoke-NativeCommand {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "run-qemu-smoke.ps1") @realSmokeArguments
    }
    if ($script:LastNativeExitCode -ne 0) {
        Fail-Step -Step "qemu-smoke" -ExitCode $script:LastNativeExitCode
    }

    Write-Host "TEST_PASS qemu.qcow2.smoke"
}
finally {
    Pop-Location
}
