param(
    [string] $WorkloadProfile = "SingleMinimalApi",
    [switch] $RunQcow2RealSmoke,
    [string] $Qemu = "",
    [string] $QemuImg = "",
    [string] $OvmfCode = "",
    [switch] $PlanOnly,
    [switch] $SelfTest,
    [string] $LogRoot = ""
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath
$solutionPath = Join-Path $root "CLROS.slnx"
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Required solution not found: $solutionPath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($LogRoot)) {
    $logRoot = Join-Path $root (Join-Path "artifacts/logs" $timestamp)
}
else {
    if ([System.IO.Path]::IsPathRooted($LogRoot)) {
        $logRoot = [System.IO.Path]::GetFullPath($LogRoot)
    }
    else {
        $logRoot = [System.IO.Path]::GetFullPath((Join-Path $root $LogRoot))
    }
}
$summaryPath = Join-Path $logRoot "summary.json"
$powerShellExe = (Get-Process -Id $PID).Path

if (Test-Path -LiteralPath $logRoot) {
    $existingEntry = Get-ChildItem -LiteralPath $logRoot -Force |
        Select-Object -First 1
    if ($null -ne $existingEntry) {
        throw "Evidence log directory must be new or empty: $logRoot"
    }
}
else {
    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
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

Assert-WorkloadProfile -Profile $WorkloadProfile
$msbuildProfileArgument = "/p:OSWorkloadProfile=$WorkloadProfile"
$msbuildProfileArguments = @()
if ($WorkloadProfile -ne "SingleMinimalApi") {
    $msbuildProfileArguments = @($msbuildProfileArgument)
}
Write-WorkloadProfileEvidence -Profile $WorkloadProfile

function ConvertTo-RelativePath {
    param([string] $Path)

    if ($Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Path.Substring($root.Length).TrimStart('\', '/')
    }

    return $Path
}

function Write-EvidenceLine {
    param(
        [string] $LogPath,
        [string] $Message
    )

    Write-Host $Message
    Add-Content -LiteralPath $LogPath -Value $Message
}

function ConvertTo-CommandText {
    param(
        [string] $Executable,
        [string[]] $Arguments
    )

    return ($Executable + " " + (($Arguments | ForEach-Object {
        if ($_ -match '\s') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join " ")).Trim()
}

function New-BaselineToolDiscovery {
    param(
        [string] $Requested,
        [string] $Source,
        [AllowNull()] [string] $ResolvedPath
    )

    $available = -not [string]::IsNullOrWhiteSpace($ResolvedPath)
    $resolved = if ($available) { ConvertTo-RelativePath $ResolvedPath } else { "not_found" }

    return [PSCustomObject]@{
        requested = $Requested
        source = $Source
        resolved = $resolved
        available = $available
    }
}

function Resolve-BaselineQemuImgCandidate {
    param(
        [string] $Candidate,
        [string] $Source
    )

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Candidate) -or $Candidate.Contains("\") -or $Candidate.Contains("/")) {
        if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
            return New-BaselineToolDiscovery `
                -Requested $Candidate `
                -Source $Source `
                -ResolvedPath (Resolve-Path -LiteralPath $Candidate).ProviderPath
        }

        return New-BaselineToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $null
    }

    $command = Get-Command $Candidate -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return New-BaselineToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $command.Source
    }

    return New-BaselineToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $null
}

function Resolve-BaselineQemuImg {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-BaselineQemuImgCandidate -Candidate $RequestedPath -Source "configured"
    }

    $environmentPath = [System.Environment]::GetEnvironmentVariable("QEMU_IMG")
    if (-not [string]::IsNullOrWhiteSpace($environmentPath)) {
        return Resolve-BaselineQemuImgCandidate -Candidate $environmentPath -Source "environment"
    }

    foreach ($candidate in @("qemu-img.exe", "qemu-img")) {
        $result = Resolve-BaselineQemuImgCandidate -Candidate $candidate -Source "PATH"
        if ($result.available) {
            return $result
        }
    }

    foreach ($candidate in @(
        "C:\Program Files\qemu\qemu-img.exe",
        "C:\Program Files (x86)\qemu\qemu-img.exe",
        "C:\ProgramData\chocolatey\bin\qemu-img.exe",
        "C:\msys64\mingw64\bin\qemu-img.exe",
        "/usr/bin/qemu-img",
        "/usr/local/bin/qemu-img"
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return New-BaselineToolDiscovery -Requested "auto" -Source "well-known" -ResolvedPath (Resolve-Path -LiteralPath $candidate).ProviderPath
        }
    }

    return New-BaselineToolDiscovery -Requested "auto" -Source "auto" -ResolvedPath $null
}

function New-BaselineQcow2ConversionIntent {
    param(
        [bool] $RealSmoke,
        [object] $QemuImgDiscovery
    )

    $mode = if ($RealSmoke) { "real" } else { "dry-run" }
    $status = "planned"
    if ($RealSmoke -and -not $QemuImgDiscovery.available) {
        $status = "blocked_by_missing_qemu_img"
    }

    return [PSCustomObject]@{
        mode = $mode
        status = $status
        qemuImgRequired = $RealSmoke
        qemuImgAvailable = [bool]$QemuImgDiscovery.available
    }
}

function Write-BaselineQemuImgDiscoveryEvidence {
    param(
        [object] $QemuImgDiscovery,
        [object] $Qcow2ConversionIntent
    )

    Write-Host "TEST_START baseline.qemu_img.discovery"
    Write-Host (
        "BASELINE_QEMU_IMG_DISCOVERY requested=" + $QemuImgDiscovery.requested +
        " source=" + $QemuImgDiscovery.source +
        " available=" + $QemuImgDiscovery.available.ToString().ToLowerInvariant() +
        " resolved=" + $QemuImgDiscovery.resolved)
    Write-Host (
        "BASELINE_QCOW2_CONVERSION_INTENT mode=" + $Qcow2ConversionIntent.mode +
        " status=" + $Qcow2ConversionIntent.status +
        " qemuImgRequired=" + $Qcow2ConversionIntent.qemuImgRequired.ToString().ToLowerInvariant() +
        " qemuImgAvailable=" + $Qcow2ConversionIntent.qemuImgAvailable.ToString().ToLowerInvariant())
    Write-Host "TEST_PASS baseline.qemu_img.discovery"
}

function Add-StepArtifactFields {
    param(
        [object] $Result,
        [string] $Image = "",
        [string] $DiskFormat = "",
        [string] $SerialLog = "",
        [string] $SummaryLog = "",
        [string] $DomainXml = "",
        [string[]] $MarkerPresets = @(),
        [object] $QemuImgDiscovery = $null,
        [object] $Qcow2ConversionIntent = $null
    )

    if (-not [string]::IsNullOrWhiteSpace($SerialLog)) {
        $Result["serialLog"] = ConvertTo-RelativePath $SerialLog
    }

    if (-not [string]::IsNullOrWhiteSpace($SummaryLog)) {
        $Result["summaryLog"] = ConvertTo-RelativePath $SummaryLog
    }

    if (-not [string]::IsNullOrWhiteSpace($DomainXml)) {
        $Result["domainXml"] = ConvertTo-RelativePath $DomainXml
    }

    if (-not [string]::IsNullOrWhiteSpace($Image)) {
        $Result["image"] = ConvertTo-RelativePath $Image
    }

    if (-not [string]::IsNullOrWhiteSpace($DiskFormat)) {
        $Result["diskFormat"] = $DiskFormat
    }

    if ($null -ne $MarkerPresets -and $MarkerPresets.Count -gt 0) {
        $Result["markerPresets"] = @($MarkerPresets)
    }

    if ($null -ne $QemuImgDiscovery) {
        $Result["qemuImgDiscovery"] = $QemuImgDiscovery
    }

    if ($null -ne $Qcow2ConversionIntent) {
        $Result["qcow2ConversionIntent"] = $Qcow2ConversionIntent
    }
}

function New-EvidenceStepResult {
    param(
        [string] $Name,
        [string] $Executable,
        [string[]] $Arguments,
        [string] $LogPath,
        [int] $ExitCode,
        [string] $Status,
        [datetime] $StartedAt,
        [datetime] $EndedAt,
        [string] $Image = "",
        [string] $DiskFormat = "",
        [string] $SerialLog = "",
        [string] $SummaryLog = "",
        [string] $DomainXml = "",
        [string[]] $MarkerPresets = @(),
        [object] $QemuImgDiscovery = $null,
        [object] $Qcow2ConversionIntent = $null
    )

    $result = [ordered]@{
        name = $Name
        workloadProfile = $WorkloadProfile
        command = ConvertTo-CommandText -Executable $Executable -Arguments $Arguments
        log = ConvertTo-RelativePath $LogPath
        exitCode = $ExitCode
        status = $Status
        startedAt = $StartedAt.ToUniversalTime().ToString("o")
        endedAt = $EndedAt.ToUniversalTime().ToString("o")
        durationSeconds = [Math]::Round(($EndedAt - $StartedAt).TotalSeconds, 3)
    }

    Add-StepArtifactFields `
        -Result $result `
        -Image $Image `
        -DiskFormat $DiskFormat `
        -SerialLog $SerialLog `
        -SummaryLog $SummaryLog `
        -DomainXml $DomainXml `
        -MarkerPresets $MarkerPresets `
        -QemuImgDiscovery $QemuImgDiscovery `
        -Qcow2ConversionIntent $Qcow2ConversionIntent

    return [PSCustomObject]$result
}

function Invoke-EvidenceStep {
    param(
        [string] $Name,
        [string] $Executable,
        [string[]] $Arguments,
        [string] $LogFileName,
        [string] $Image = "",
        [string] $DiskFormat = "",
        [string] $SerialLog = "",
        [string] $SummaryLog = "",
        [string] $DomainXml = "",
        [string[]] $MarkerPresets = @(),
        [object] $QemuImgDiscovery = $null,
        [object] $Qcow2ConversionIntent = $null
    )

    $logPath = Join-Path $logRoot $LogFileName
    $startedAt = Get-Date
    $commandText = ConvertTo-CommandText -Executable $Executable -Arguments $Arguments

    Write-EvidenceLine $logPath "TEST_START $Name"
    Write-EvidenceLine $logPath "WORKLOAD_PROFILE $WorkloadProfile"
    Write-EvidenceLine $logPath "COMMAND $commandText"

    Push-Location $root
    try {
        & $Executable @Arguments 2>&1 | ForEach-Object {
            $line = $_.ToString()
            Write-Host $line
            Add-Content -LiteralPath $logPath -Value $line
        }

        $exitCode = $LASTEXITCODE
        if ($null -eq $exitCode) {
            if ($?) {
                $exitCode = 0
            }
            else {
                $exitCode = 1
            }
        }
    }
    finally {
        Pop-Location
    }

    $endedAt = Get-Date
    if ($exitCode -eq 0) {
        Write-EvidenceLine $logPath "TEST_PASS $Name"
    }
    else {
        Write-EvidenceLine $logPath "TEST_FAIL $Name exit_code=$exitCode"
    }

    return New-EvidenceStepResult `
        -Name $Name `
        -Executable $Executable `
        -Arguments $Arguments `
        -LogPath $logPath `
        -ExitCode $exitCode `
        -Status $(if ($exitCode -eq 0) { "passed" } else { "failed" }) `
        -StartedAt $startedAt `
        -EndedAt $endedAt `
        -Image $Image `
        -DiskFormat $DiskFormat `
        -SerialLog $SerialLog `
        -SummaryLog $SummaryLog `
        -DomainXml $DomainXml `
        -MarkerPresets $MarkerPresets `
        -QemuImgDiscovery $QemuImgDiscovery `
        -Qcow2ConversionIntent $Qcow2ConversionIntent
}

function Get-HostTerminalValidation {
    param([string] $Path)

    $result = [ordered]@{
        valid = $false
        reason = "missing_log"
        passCount = 0
        failCount = 0
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]$result
    }

    try {
        $text = Get-Content -LiteralPath $Path -Raw
    }
    catch {
        $result.reason = "read_failed"
        return [PSCustomObject]$result
    }

    $result.passCount = [regex]::Matches(
        $text,
        "(?m)^TEST_PASS host\.tests\r?$").Count
    $result.failCount = [regex]::Matches(
        $text,
        "(?m)^TEST_FAIL host\.tests(?:\s.*)?\r?$").Count
    if ($result.passCount -eq 1 -and $result.failCount -eq 0) {
        $result.valid = $true
        $result.reason = "valid"
    }
    else {
        $result.reason = "terminal_marker_cardinality"
    }

    return [PSCustomObject]$result
}

function Test-BaselineSummaryRoundTrip {
    param(
        [string] $Path,
        [string] $ExpectedStatus,
        [string] $ExpectedMode,
        [int] $ExpectedStepCount
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    try {
        $roundTrip = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json
    }
    catch {
        return $false
    }

    return $roundTrip.schema -eq "os.baseline.evidence.summary.v0" `
        -and $roundTrip.status -eq $ExpectedStatus `
        -and $roundTrip.mode -eq $ExpectedMode `
        -and @($roundTrip.steps).Count -eq $ExpectedStepCount
}

if ($SelfTest) {
    if ($PlanOnly) {
        throw "-SelfTest cannot be combined with -PlanOnly."
    }

    Write-Host "TEST_START baseline.evidence.selftest"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $caseRoot = Join-Path $logRoot "selftest"
    New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null

    $missing = Get-HostTerminalValidation `
        -Path (Join-Path $caseRoot "missing.log")
    if ($missing.valid -or $missing.reason -ne "missing_log") {
        throw "Host-terminal self-test accepted a missing log."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest missing_host_log"

    $validPath = Join-Path $caseRoot "valid.log"
    [System.IO.File]::WriteAllText(
        $validPath,
        "TEST_START host.tests`r`nTEST_PASS host.tests`r`n",
        $utf8NoBom)
    $valid = Get-HostTerminalValidation -Path $validPath
    if (-not $valid.valid -or
        $valid.passCount -ne 1 -or
        $valid.failCount -ne 0) {
        throw "Host-terminal self-test rejected the single valid terminal."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest single_host_pass"

    $duplicatePath = Join-Path $caseRoot "duplicate.log"
    [System.IO.File]::WriteAllText(
        $duplicatePath,
        "TEST_PASS host.tests`r`nTEST_PASS host.tests`r`n",
        $utf8NoBom)
    $duplicate = Get-HostTerminalValidation -Path $duplicatePath
    if ($duplicate.valid -or $duplicate.passCount -ne 2) {
        throw "Host-terminal self-test accepted duplicate pass terminals."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest duplicate_host_pass"

    $conflictPath = Join-Path $caseRoot "conflict.log"
    [System.IO.File]::WriteAllText(
        $conflictPath,
        "TEST_PASS host.tests`r`nTEST_FAIL host.tests failures=1`r`n",
        $utf8NoBom)
    $conflict = Get-HostTerminalValidation -Path $conflictPath
    if ($conflict.valid -or
        $conflict.passCount -ne 1 -or
        $conflict.failCount -ne 1) {
        throw "Host-terminal self-test accepted conflicting terminals."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest conflicting_host_terminal"

    $summaryPathSelfTest = Join-Path $caseRoot "summary.json"
    $validSummary = [ordered]@{
        schema = "os.baseline.evidence.summary.v0"
        status = "passed"
        mode = "execute"
        steps = @([ordered]@{ name = "one" })
    }
    [System.IO.File]::WriteAllText(
        $summaryPathSelfTest,
        (($validSummary | ConvertTo-Json -Depth 4) + "`n"),
        $utf8NoBom)
    if (-not (Test-BaselineSummaryRoundTrip `
            -Path $summaryPathSelfTest `
            -ExpectedStatus "passed" `
            -ExpectedMode "execute" `
            -ExpectedStepCount 1)) {
        throw "Summary self-test rejected matching JSON."
    }
    if (Test-BaselineSummaryRoundTrip `
            -Path $summaryPathSelfTest `
            -ExpectedStatus "failed" `
            -ExpectedMode "execute" `
            -ExpectedStepCount 1) {
        throw "Summary self-test accepted a status mismatch."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest summary_mismatch"

    [System.IO.File]::WriteAllText(
        $summaryPathSelfTest,
        "{not-json",
        $utf8NoBom)
    if (Test-BaselineSummaryRoundTrip `
            -Path $summaryPathSelfTest `
            -ExpectedStatus "passed" `
            -ExpectedMode "execute" `
            -ExpectedStepCount 1) {
        throw "Summary self-test accepted malformed JSON."
    }
    Write-Host "TEST_PASS baseline.evidence.selftest malformed_summary"
    Write-Host "TEST_PASS baseline.evidence.selftest"
    exit 0
}

$qemuSerialLog = Join-Path $logRoot "qemu-smoke-serial.log"
$qemuSummaryLog = Join-Path $logRoot "qemu-smoke.summary.log"
$diagnosticsSerialLog = Join-Path $logRoot "qemu-diagnostics-serial.log"
$diagnosticsSummaryLog = Join-Path $logRoot "qemu-diagnostics.summary.log"
$qcow2IntentRawImage = Join-Path $logRoot "qemu-qcow2-intent.raw"
$qcow2IntentImage = Join-Path $logRoot "qemu-qcow2-intent.qcow2"
$qcow2RealRawImage = Join-Path $root "artifacts\images\os.raw"
$qcow2RealImage = Join-Path $root "artifacts\images\os.qcow2"
$qcow2Image = if ($RunQcow2RealSmoke) { $qcow2RealImage } else { $qcow2IntentImage }
$qcow2RawImage = if ($RunQcow2RealSmoke) { $qcow2RealRawImage } else { $qcow2IntentRawImage }
$qcow2SerialLog = Join-Path $logRoot "qemu-qcow2-serial.log"
$qcow2SummaryLog = Join-Path $logRoot "qemu-qcow2.summary.log"
$libvirtDryRunSerialLog = Join-Path $logRoot "libvirt-dryrun-serial.log"
$libvirtDryRunSummaryLog = Join-Path $logRoot "libvirt-dryrun.summary.log"
$libvirtDryRunDomainXml = Join-Path $logRoot "libvirt-dryrun.xml"
$libvirtMissingToolSerialLog = Join-Path $logRoot "libvirt-missing-tool-serial.log"
$libvirtMissingToolSummaryLog = Join-Path $logRoot "libvirt-missing-tool.summary.log"
$libvirtMissingToolDomainXml = Join-Path $logRoot "libvirt-missing-tool.xml"
$missingVirshPath = Join-Path $logRoot "missing-virsh"

function Initialize-SmokeIntentArtifacts {
    param([string[]] $Paths)

    Write-Host "TEST_START baseline.smoke.intent_artifacts"

    foreach ($path in $Paths) {
        $directory = [System.IO.Path]::GetDirectoryName($path)
        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        if (-not (Test-Path -LiteralPath $path)) {
            [System.IO.File]::WriteAllBytes($path, [byte[]] @(0x4F, 0x53, 0x49, 0x4E, 0x54, 0x45, 0x4E, 0x54))
        }

        Write-Host ("BASELINE_INTENT_ARTIFACT " + (ConvertTo-RelativePath $path))
    }

    Write-Host "TEST_PASS baseline.smoke.intent_artifacts"
}

$qemuImgDiscovery = Resolve-BaselineQemuImg -RequestedPath $QemuImg
$qcow2ConversionIntent = New-BaselineQcow2ConversionIntent `
    -RealSmoke ([bool]$RunQcow2RealSmoke) `
    -QemuImgDiscovery $qemuImgDiscovery
Write-BaselineQemuImgDiscoveryEvidence `
    -QemuImgDiscovery $qemuImgDiscovery `
    -Qcow2ConversionIntent $qcow2ConversionIntent

$qcow2SmokeArguments = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    (Join-Path $root "scripts/run-qemu-qcow2-smoke.ps1"),
    "-RawImage",
    $qcow2RawImage,
    "-Qcow2Image",
    $qcow2Image,
    "-SerialLog",
    $qcow2SerialLog,
    "-SummaryLog",
    $qcow2SummaryLog,
    "-WorkloadProfile",
    $WorkloadProfile
)

if (-not $RunQcow2RealSmoke) {
    $qcow2SmokeArguments += "-DryRun"
}

if (-not [string]::IsNullOrWhiteSpace($Qemu)) {
    $qcow2SmokeArguments += @("-Qemu", $Qemu)
}

if (-not [string]::IsNullOrWhiteSpace($QemuImg)) {
    $qcow2SmokeArguments += @("-QemuImg", $QemuImg)
}

if (-not [string]::IsNullOrWhiteSpace($OvmfCode)) {
    $qcow2SmokeArguments += @("-OvmfCode", $OvmfCode)
}

$steps = @(
    @{
        Name = "baseline.naming.guard"
        Executable = $powerShellExe
        Arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $root "scripts/check-naming.ps1"))
        LogFileName = "naming-guard.log"
    },
    @{
        Name = "baseline.build"
        Executable = "dotnet"
        Arguments = @(
            "build",
            ".\CLROS.slnx",
            "--nologo",
            "-m:1"
        ) + $msbuildProfileArguments
        LogFileName = "dotnet-build.log"
    },
    @{
        Name = "baseline.host.build"
        Executable = "dotnet"
        Arguments = @(
            "build",
            ".\tests\OS.HostTests\OS.HostTests.csproj",
            "--nologo"
        ) + $msbuildProfileArguments
        LogFileName = "host-tests-build.log"
    },
    @{
        Name = "baseline.host.tests"
        Executable = "dotnet"
        Arguments = @(
            "run",
            "--project",
            ".\tests\OS.HostTests\OS.HostTests.csproj",
            "--no-build"
        ) + $msbuildProfileArguments
        LogFileName = "host-tests.log"
    },
    @{
        Name = "baseline.qemu.dryrun"
        Executable = $powerShellExe
        Arguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts/run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $qemuSerialLog,
            "-SummaryLog",
            $qemuSummaryLog
        )
        LogFileName = "qemu-dry-run.log"
        SerialLog = $qemuSerialLog
        SummaryLog = $qemuSummaryLog
    },
    @{
        Name = "baseline.qemu.diagnostics.dryrun"
        Executable = $powerShellExe
        Arguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts/run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "diagnostics",
            "-SerialLog",
            $diagnosticsSerialLog,
            "-SummaryLog",
            $diagnosticsSummaryLog
        )
        LogFileName = "qemu-diagnostics-dry-run.log"
        SerialLog = $diagnosticsSerialLog
        SummaryLog = $diagnosticsSummaryLog
        MarkerPresets = @("diagnostics")
    },
    @{
        Name = if ($RunQcow2RealSmoke) { "baseline.qemu.qcow2.real" } else { "baseline.qemu.qcow2.dryrun" }
        Executable = $powerShellExe
        Arguments = $qcow2SmokeArguments
        LogFileName = "qemu-qcow2-smoke.log"
        Image = $qcow2Image
        DiskFormat = "qcow2"
        SerialLog = $qcow2SerialLog
        SummaryLog = $qcow2SummaryLog
        QemuImgDiscovery = $qemuImgDiscovery
        Qcow2ConversionIntent = $qcow2ConversionIntent
    },
    @{
        Name = "baseline.libvirt.dryrun"
        Executable = $powerShellExe
        Arguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts/run-libvirt-smoke.ps1"),
            "-DryRun",
            "-Image",
            $qcow2Image,
            "-DiskFormat",
            "qcow2",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $libvirtDryRunSerialLog,
            "-SummaryLog",
            $libvirtDryRunSummaryLog,
            "-DomainXml",
            $libvirtDryRunDomainXml
        )
        LogFileName = "libvirt-dry-run.log"
        Image = $qcow2Image
        DiskFormat = "qcow2"
        SerialLog = $libvirtDryRunSerialLog
        SummaryLog = $libvirtDryRunSummaryLog
        DomainXml = $libvirtDryRunDomainXml
    },
    @{
        Name = "baseline.libvirt.missingtool"
        Executable = $powerShellExe
        Arguments = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts/run-libvirt-smoke.ps1"),
            "-Image",
            $qcow2Image,
            "-DiskFormat",
            "qcow2",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $libvirtMissingToolSerialLog,
            "-SummaryLog",
            $libvirtMissingToolSummaryLog,
            "-DomainXml",
            $libvirtMissingToolDomainXml,
            "-Virsh",
            $missingVirshPath
        )
        LogFileName = "libvirt-missing-tool.log"
        Image = $qcow2Image
        DiskFormat = "qcow2"
        SerialLog = $libvirtMissingToolSerialLog
        SummaryLog = $libvirtMissingToolSummaryLog
        DomainXml = $libvirtMissingToolDomainXml
    },
    @{
        Name = "baseline.git.diffcheck"
        Executable = "git"
        Arguments = @("diff", "--check")
        LogFileName = "git-diff-check.log"
    },
    @{
        Name = "baseline.git.status"
        Executable = "git"
        Arguments = @("status", "--short")
        LogFileName = "git-status-short.log"
    }
)

$results = New-Object System.Collections.Generic.List[object]

if ($PlanOnly) {
    Write-Host "TEST_START baseline.artifact_summary"

    foreach ($step in $steps) {
        $plannedAt = Get-Date
        $plannedLogPath = Join-Path $logRoot $step.LogFileName
        $result = New-EvidenceStepResult `
            -Name $step.Name `
            -Executable $step.Executable `
            -Arguments $step.Arguments `
            -LogPath $plannedLogPath `
            -ExitCode 0 `
            -Status "planned" `
            -StartedAt $plannedAt `
            -EndedAt $plannedAt `
            -Image $step.Image `
            -DiskFormat $step.DiskFormat `
            -SerialLog $step.SerialLog `
            -SummaryLog $step.SummaryLog `
            -DomainXml $step.DomainXml `
            -MarkerPresets $step.MarkerPresets `
            -QemuImgDiscovery $step.QemuImgDiscovery `
            -Qcow2ConversionIntent $step.Qcow2ConversionIntent
        $results.Add($result)
    }

    $summary = [ordered]@{
        schema = "os.baseline.evidence.summary.v0"
        status = "planned"
        mode = "plan-only"
        timestamp = $timestamp
        workloadProfile = $WorkloadProfile
        repositoryRoot = $root
        logDirectory = ConvertTo-RelativePath $logRoot
        summary = ConvertTo-RelativePath $summaryPath
        qemuImgDiscovery = $qemuImgDiscovery
        qcow2ConversionIntent = $qcow2ConversionIntent
        steps = $results
    }

    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Write-Host ("SUMMARY " + (ConvertTo-RelativePath $summaryPath))
    Write-Host "TEST_PASS baseline.artifact_summary"
    exit 0
}

if (-not $RunQcow2RealSmoke) {
    Initialize-SmokeIntentArtifacts -Paths @($qcow2IntentRawImage, $qcow2IntentImage)
}

foreach ($step in $steps) {
    $result = Invoke-EvidenceStep `
        -Name $step.Name `
        -Executable $step.Executable `
        -Arguments $step.Arguments `
        -LogFileName $step.LogFileName `
        -Image $step.Image `
        -DiskFormat $step.DiskFormat `
        -SerialLog $step.SerialLog `
        -SummaryLog $step.SummaryLog `
        -DomainXml $step.DomainXml `
        -MarkerPresets $step.MarkerPresets `
        -QemuImgDiscovery $step.QemuImgDiscovery `
        -Qcow2ConversionIntent $step.Qcow2ConversionIntent
    $results.Add($result)
}

$hostTestResult = $results |
    Where-Object { $_.name -eq "baseline.host.tests" } |
    Select-Object -First 1
$hostTestLog = Join-Path $logRoot "host-tests.log"
$hostTerminalValidation = Get-HostTerminalValidation -Path $hostTestLog

if ($null -eq $hostTestResult -or
    -not $hostTerminalValidation.valid) {
    Write-Host (
        "TEST_FAIL baseline.host.tests.terminal reason=" +
        $hostTerminalValidation.reason +
        " passCount=" +
        $hostTerminalValidation.passCount +
        " failCount=" +
        $hostTerminalValidation.failCount)
    if ($null -ne $hostTestResult -and $hostTestResult.exitCode -eq 0) {
        $hostTestResult.exitCode = 90
        $hostTestResult.status = "failed"
    }
}
else {
    Write-Host "TEST_PASS baseline.host.tests.terminal"
}

$failedCount = ($results | Where-Object { $_.exitCode -ne 0 }).Count
$summary = [ordered]@{
    schema = "os.baseline.evidence.summary.v0"
    status = if ($failedCount -eq 0) { "passed" } else { "failed" }
    mode = "execute"
    timestamp = $timestamp
    workloadProfile = $WorkloadProfile
    repositoryRoot = $root
    logDirectory = ConvertTo-RelativePath $logRoot
    summary = ConvertTo-RelativePath $summaryPath
    qemuImgDiscovery = $qemuImgDiscovery
    qcow2ConversionIntent = $qcow2ConversionIntent
    steps = $results
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ("SUMMARY " + (ConvertTo-RelativePath $summaryPath))

$summaryMatches = Test-BaselineSummaryRoundTrip `
    -Path $summaryPath `
    -ExpectedStatus $summary.status `
    -ExpectedMode "execute" `
    -ExpectedStepCount $results.Count
if (-not $summaryMatches) {
    Write-Host "TEST_FAIL baseline.artifact_summary mismatch"
    exit 1
}

Write-Host "TEST_PASS baseline.artifact_summary"

if ($failedCount -gt 0) {
    Write-Host ("TEST_FAIL baseline.evidence failures=" + $failedCount)
    exit 1
}

Write-Host "TEST_PASS baseline.evidence"
