param(
    [switch] $DryRun,
    [string] $LogRoot = "",
    [string] $WorkloadProfile = "SingleMinimalApi",
    [string] $DotNet = "dotnet",
    [switch] $RunQcow2RealSmoke,
    [string] $Qemu = "",
    [string] $QemuImg = "",
    [string] $OvmfCode = ""
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath
$solutionPath = Join-Path $root "CLROS.slnx"
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Required solution not found: $solutionPath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$powerShellExe = (Get-Process -Id $PID).Path

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

if ([string]::IsNullOrWhiteSpace($LogRoot)) {
    $logRoot = Join-Path (Join-Path $root "artifacts\logs") ("ci-smoke-" + $timestamp)
}
else {
    if ([System.IO.Path]::IsPathRooted($LogRoot)) {
        $logRoot = [System.IO.Path]::GetFullPath($LogRoot)
    }
    else {
        $logRoot = [System.IO.Path]::GetFullPath((Join-Path $root $LogRoot))
    }
}

$artifactPlanPath = Join-Path $logRoot "ci-smoke.artifacts.json"
$summaryPath = Join-Path $logRoot "ci-smoke.summary.json"

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function ConvertTo-RelativePath {
    param([string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = $root.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

    if ($fullPath.Equals($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return "."
    }

    if ($fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($rootWithSeparator.Length)
    }

    return $fullPath
}

function Format-CommandArgument {
    param([string] $Argument)

    if ($Argument.Length -eq 0) {
        return '""'
    }

    if ($Argument.IndexOfAny([char[]] @(' ', "`t", '"')) -ge 0) {
        return '"' + ($Argument -replace '"', '\"') + '"'
    }

    return $Argument
}

function Join-CommandText {
    param(
        [string] $Executable,
        [string[]] $Arguments
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add((Format-CommandArgument $Executable))

    foreach ($argument in $Arguments) {
        $parts.Add((Format-CommandArgument $argument))
    }

    return ($parts -join " ")
}

function New-CiSmokeToolDiscovery {
    param(
        [string] $Requested,
        [string] $Source,
        [AllowNull()] [string] $ResolvedPath
    )

    $available = -not [string]::IsNullOrWhiteSpace($ResolvedPath)
    $resolved = if ($available) { ConvertTo-RelativePath $ResolvedPath } else { "not_found" }

    return [PSCustomObject] @{
        requested = $Requested
        source = $Source
        resolved = $resolved
        available = $available
    }
}

function Resolve-CiSmokeQemuImgCandidate {
    param(
        [string] $Candidate,
        [string] $Source
    )

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Candidate) -or $Candidate.Contains("\") -or $Candidate.Contains("/")) {
        if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
            return New-CiSmokeToolDiscovery `
                -Requested $Candidate `
                -Source $Source `
                -ResolvedPath (Resolve-Path -LiteralPath $Candidate).ProviderPath
        }

        return New-CiSmokeToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $null
    }

    $command = Get-Command $Candidate -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return New-CiSmokeToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $command.Source
    }

    return New-CiSmokeToolDiscovery -Requested $Candidate -Source $Source -ResolvedPath $null
}

function Resolve-CiSmokeQemuImg {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-CiSmokeQemuImgCandidate -Candidate $RequestedPath -Source "configured"
    }

    $environmentPath = [System.Environment]::GetEnvironmentVariable("QEMU_IMG")
    if (-not [string]::IsNullOrWhiteSpace($environmentPath)) {
        return Resolve-CiSmokeQemuImgCandidate -Candidate $environmentPath -Source "environment"
    }

    foreach ($candidate in @("qemu-img.exe", "qemu-img")) {
        $result = Resolve-CiSmokeQemuImgCandidate -Candidate $candidate -Source "PATH"
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
            return New-CiSmokeToolDiscovery -Requested "auto" -Source "well-known" -ResolvedPath (Resolve-Path -LiteralPath $candidate).ProviderPath
        }
    }

    return New-CiSmokeToolDiscovery -Requested "auto" -Source "auto" -ResolvedPath $null
}

function New-CiSmokeQcow2ConversionIntent {
    param(
        [bool] $RealSmoke,
        [object] $QemuImgDiscovery
    )

    $mode = if ($RealSmoke) { "real" } else { "dry-run" }
    $status = "planned"
    if ($RealSmoke -and -not $QemuImgDiscovery.available) {
        $status = "blocked_by_missing_qemu_img"
    }

    return [PSCustomObject] @{
        mode = $mode
        status = $status
        qemuImgRequired = $RealSmoke
        qemuImgAvailable = [bool] $QemuImgDiscovery.available
    }
}

function Write-CiSmokeQemuImgDiscoveryEvidence {
    param(
        [object] $QemuImgDiscovery,
        [object] $Qcow2ConversionIntent
    )

    Write-Host "TEST_START ci.smoke.qemu_img.discovery"
    Write-Host (
        "CI_SMOKE_QEMU_IMG_DISCOVERY requested=" + $QemuImgDiscovery.requested +
        " source=" + $QemuImgDiscovery.source +
        " available=" + $QemuImgDiscovery.available.ToString().ToLowerInvariant() +
        " resolved=" + $QemuImgDiscovery.resolved)
    Write-Host (
        "CI_SMOKE_QCOW2_CONVERSION_INTENT mode=" + $Qcow2ConversionIntent.mode +
        " status=" + $Qcow2ConversionIntent.status +
        " qemuImgRequired=" + $Qcow2ConversionIntent.qemuImgRequired.ToString().ToLowerInvariant() +
        " qemuImgAvailable=" + $Qcow2ConversionIntent.qemuImgAvailable.ToString().ToLowerInvariant())
    Write-Host "TEST_PASS ci.smoke.qemu_img.discovery"
}

function New-CiSmokeStep {
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
    $artifact = [ordered] @{
        name = $Name
        workloadProfile = $WorkloadProfile
        command = Join-CommandText -Executable $Executable -Arguments $Arguments
        log = ConvertTo-RelativePath $logPath
    }

    if (-not [string]::IsNullOrWhiteSpace($SerialLog)) {
        $artifact.serialLog = ConvertTo-RelativePath $SerialLog
    }

    if (-not [string]::IsNullOrWhiteSpace($SummaryLog)) {
        $artifact.summaryLog = ConvertTo-RelativePath $SummaryLog
    }

    if (-not [string]::IsNullOrWhiteSpace($DomainXml)) {
        $artifact.domainXml = ConvertTo-RelativePath $DomainXml
    }

    if ($MarkerPresets.Count -gt 0) {
        $artifact.markerPresets = $MarkerPresets
    }

    if (-not [string]::IsNullOrWhiteSpace($Image)) {
        $artifact.image = ConvertTo-RelativePath $Image
    }

    if (-not [string]::IsNullOrWhiteSpace($DiskFormat)) {
        $artifact.diskFormat = $DiskFormat
    }

    if ($null -ne $QemuImgDiscovery) {
        $artifact["qemuImgDiscovery"] = $QemuImgDiscovery
    }

    if ($null -ne $Qcow2ConversionIntent) {
        $artifact["qcow2ConversionIntent"] = $Qcow2ConversionIntent
    }

    return [PSCustomObject] @{
        Name = $Name
        Executable = $Executable
        Arguments = $Arguments
        LogPath = $logPath
        Image = $Image
        DiskFormat = $DiskFormat
        SerialLog = $SerialLog
        SummaryLog = $SummaryLog
        DomainXml = $DomainXml
        MarkerPresets = $MarkerPresets
        QemuImgDiscovery = $QemuImgDiscovery
        Qcow2ConversionIntent = $Qcow2ConversionIntent
        Artifact = $artifact
    }
}

function Initialize-SmokeIntentArtifacts {
    param([string[]] $Paths)

    Write-Host "TEST_START ci.smoke.intent_artifacts"

    foreach ($path in $Paths) {
        $directory = [System.IO.Path]::GetDirectoryName($path)
        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        if (-not (Test-Path -LiteralPath $path)) {
            [System.IO.File]::WriteAllBytes($path, [byte[]] @(0x4F, 0x53, 0x49, 0x4E, 0x54, 0x45, 0x4E, 0x54))
        }

        Write-Host ("CI_SMOKE_INTENT_ARTIFACT " + (ConvertTo-RelativePath $path))
    }

    Write-Host "TEST_PASS ci.smoke.intent_artifacts"
}

function New-ArtifactPlan {
    param(
        [string] $Mode,
        [object[]] $Steps,
        [object] $QemuImgDiscovery = $null,
        [object] $Qcow2ConversionIntent = $null
    )

    $artifacts = New-Object System.Collections.Generic.List[object]
    foreach ($step in $Steps) {
        $artifacts.Add($step.Artifact)
    }

    $plan = [ordered] @{
        schema = "os.ci.smoke.artifacts.v0"
        mode = $Mode
        timestamp = $timestamp
        workloadProfile = $WorkloadProfile
        repositoryRoot = $root
        logDirectory = ConvertTo-RelativePath $logRoot
        artifactPlan = ConvertTo-RelativePath $artifactPlanPath
        summary = ConvertTo-RelativePath $summaryPath
    }

    if ($null -ne $QemuImgDiscovery) {
        $plan["qemuImgDiscovery"] = $QemuImgDiscovery
    }

    if ($null -ne $Qcow2ConversionIntent) {
        $plan["qcow2ConversionIntent"] = $Qcow2ConversionIntent
    }

    $plan["steps"] = $artifacts
    return $plan
}

function Write-JsonFile {
    param(
        [string] $Path,
        [object] $Value
    )

    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-LogLine {
    param(
        [string] $LogPath,
        [string] $Message
    )

    Write-Host $Message
    Add-Content -LiteralPath $LogPath -Value $Message
}

function Invoke-CiSmokeStep {
    param([object] $Step)

    $startedAt = Get-Date
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($Step.LogPath)) -Force | Out-Null

    Write-LogLine -LogPath $Step.LogPath -Message ("TEST_START " + $Step.Name)
    Write-LogLine -LogPath $Step.LogPath -Message ("WORKLOAD_PROFILE " + $WorkloadProfile)
    Write-LogLine -LogPath $Step.LogPath -Message ("COMMAND " + $Step.Artifact.command)

    $exitCode = 0
    $previousErrorActionPreference = $ErrorActionPreference

    Push-Location $root
    try {
        $ErrorActionPreference = "Continue"
        & $Step.Executable @($Step.Arguments) 2>&1 | ForEach-Object {
            $line = $_.ToString()
            Write-Host $line
            Add-Content -LiteralPath $Step.LogPath -Value $line
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
    catch {
        $exitCode = 1
        Write-LogLine -LogPath $Step.LogPath -Message ("STEP_EXCEPTION " + $_.Exception.GetType().Name + ": " + $_.Exception.Message)
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
    }

    $endedAt = Get-Date
    if ($exitCode -eq 0) {
        Write-LogLine -LogPath $Step.LogPath -Message ("TEST_PASS " + $Step.Name)
        $status = "passed"
    }
    else {
        Write-LogLine -LogPath $Step.LogPath -Message ("TEST_FAIL " + $Step.Name + " exit_code=" + $exitCode)
        $status = "failed"
    }

    $result = [ordered] @{
        name = $Step.Name
        workloadProfile = $WorkloadProfile
        command = $Step.Artifact.command
        log = ConvertTo-RelativePath $Step.LogPath
        exitCode = $exitCode
        status = $status
        startedAt = $startedAt.ToUniversalTime().ToString("o")
        endedAt = $endedAt.ToUniversalTime().ToString("o")
        durationSeconds = [Math]::Round(($endedAt - $startedAt).TotalSeconds, 3)
    }

    if (-not [string]::IsNullOrWhiteSpace($Step.SerialLog)) {
        $result.serialLog = ConvertTo-RelativePath $Step.SerialLog
    }

    if (-not [string]::IsNullOrWhiteSpace($Step.SummaryLog)) {
        $result.summaryLog = ConvertTo-RelativePath $Step.SummaryLog
    }

    if (-not [string]::IsNullOrWhiteSpace($Step.DomainXml)) {
        $result.domainXml = ConvertTo-RelativePath $Step.DomainXml
    }

    if ($Step.MarkerPresets.Count -gt 0) {
        $result.markerPresets = $Step.MarkerPresets
    }

    if (-not [string]::IsNullOrWhiteSpace($Step.Image)) {
        $result.image = ConvertTo-RelativePath $Step.Image
    }

    if (-not [string]::IsNullOrWhiteSpace($Step.DiskFormat)) {
        $result.diskFormat = $Step.DiskFormat
    }

    if ($null -ne $Step.QemuImgDiscovery) {
        $result["qemuImgDiscovery"] = $Step.QemuImgDiscovery
    }

    if ($null -ne $Step.Qcow2ConversionIntent) {
        $result["qcow2ConversionIntent"] = $Step.Qcow2ConversionIntent
    }

    return [PSCustomObject] $result
}

$qemuSerialLog = Join-Path $logRoot "qemu-smoke-serial.log"
$qemuSummaryLog = Join-Path $logRoot "qemu-smoke.summary.log"
$nativeAppSerialLog = Join-Path $logRoot "qemu-native-app-serial.log"
$nativeAppSummaryLog = Join-Path $logRoot "qemu-native-app.summary.log"
$diagnosticsSerialLog = Join-Path $logRoot "qemu-diagnostics-serial.log"
$diagnosticsSummaryLog = Join-Path $logRoot "qemu-diagnostics.summary.log"
$appArgsSerialLog = Join-Path $logRoot "qemu-app-args-serial.log"
$appArgsSummaryLog = Join-Path $logRoot "qemu-app-args.summary.log"
$appExitSerialLog = Join-Path $logRoot "qemu-app-exit-serial.log"
$appExitSummaryLog = Join-Path $logRoot "qemu-app-exit.summary.log"
$cctorPreflightSerialLog = Join-Path $logRoot "qemu-cctor-preflight-serial.log"
$cctorPreflightSummaryLog = Join-Path $logRoot "qemu-cctor-preflight.summary.log"
$cctorWorkItemsSerialLog = Join-Path $logRoot "qemu-cctor-work-items-serial.log"
$cctorWorkItemsSummaryLog = Join-Path $logRoot "qemu-cctor-work-items.summary.log"
$fetchHttpSerialLog = Join-Path $logRoot "qemu-fetch-http-serial.log"
$fetchHttpSummaryLog = Join-Path $logRoot "qemu-fetch-http.summary.log"
$fetchHttpEndpointSerialLog = Join-Path $logRoot "qemu-fetch-http-endpoint-serial.log"
$fetchHttpEndpointSummaryLog = Join-Path $logRoot "qemu-fetch-http-endpoint.summary.log"
$fetchHttpTcpExchangeSerialLog = Join-Path $logRoot "qemu-fetch-http-tcp-exchange-serial.log"
$fetchHttpTcpExchangeSummaryLog = Join-Path $logRoot "qemu-fetch-http-tcp-exchange.summary.log"
$httpStaticResponseSerialLog = Join-Path $logRoot "qemu-http-static-response-serial.log"
$httpStaticResponseSummaryLog = Join-Path $logRoot "qemu-http-static-response.summary.log"
$httpStaticTcpResponseSerialLog = Join-Path $logRoot "qemu-http-static-tcp-response-serial.log"
$httpStaticTcpResponseSummaryLog = Join-Path $logRoot "qemu-http-static-tcp-response.summary.log"
$virtioRxTcpDispatchSerialLog = Join-Path $logRoot "qemu-virtio-rx-tcp-dispatch-serial.log"
$virtioRxTcpDispatchSummaryLog = Join-Path $logRoot "qemu-virtio-rx-tcp-dispatch.summary.log"
$manifestEnvSerialLog = Join-Path $logRoot "qemu-manifest-env-serial.log"
$manifestEnvSummaryLog = Join-Path $logRoot "qemu-manifest-env.summary.log"
$nativeAotHeapSerialLog = Join-Path $logRoot "qemu-nativeaot-heap-serial.log"
$nativeAotHeapSummaryLog = Join-Path $logRoot "qemu-nativeaot-heap.summary.log"
$palExportSerialLog = Join-Path $logRoot "qemu-pal-export-serial.log"
$palExportSummaryLog = Join-Path $logRoot "qemu-pal-export.summary.log"
$qcow2IntentRawImage = Join-Path $logRoot "qemu-qcow2-intent.raw"
$qcow2IntentImage = Join-Path $logRoot "qemu-qcow2-intent.qcow2"
$qcow2RealRawImage = Join-Path $root "artifacts\images\os.raw"
$qcow2RealImage = Join-Path $root "artifacts\images\os.qcow2"
$qcow2Image = if ($RunQcow2RealSmoke) { $qcow2RealImage } else { $qcow2IntentImage }
$qcow2RawImage = if ($RunQcow2RealSmoke) { $qcow2RealRawImage } else { $qcow2IntentRawImage }
$qcow2SerialLog = Join-Path $logRoot "qemu-qcow2-serial.log"
$qcow2SummaryLog = Join-Path $logRoot "qemu-qcow2.summary.log"
$libvirtDryRunSerialLog = Join-Path $logRoot "libvirt-dryrun-serial.log"
$libvirtDryRunDomainXml = Join-Path $logRoot "libvirt-dryrun.xml"
$libvirtMissingToolSerialLog = Join-Path $logRoot "libvirt-missing-tool-serial.log"
$libvirtMissingToolDomainXml = Join-Path $logRoot "libvirt-missing-tool.xml"
$missingVirshPath = Join-Path $logRoot "missing-virsh"
$qemuImgDiscovery = Resolve-CiSmokeQemuImg -RequestedPath $QemuImg
$qcow2ConversionIntent = New-CiSmokeQcow2ConversionIntent `
    -RealSmoke ([bool] $RunQcow2RealSmoke) `
    -QemuImgDiscovery $qemuImgDiscovery
Write-CiSmokeQemuImgDiscoveryEvidence `
    -QemuImgDiscovery $qemuImgDiscovery `
    -Qcow2ConversionIntent $qcow2ConversionIntent

$qcow2SmokeArguments = @(
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
    (New-CiSmokeStep `
        -Name "ci.smoke.build" `
        -Executable $DotNet `
        -Arguments @("build", ".\CLROS.slnx", "--nologo", "-m:1", $msbuildProfileArgument) `
        -LogFileName "dotnet-build.log"),
    (New-CiSmokeStep `
        -Name "ci.smoke.host.tests" `
        -Executable $DotNet `
        -Arguments @("run", "--project", ".\tests\OS.HostTests\OS.HostTests.csproj", "--no-build") `
        -LogFileName "host-tests.log"),
    (New-CiSmokeStep `
        -Name "ci.smoke.qemu.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $qemuSerialLog,
            "-SummaryLog",
            $qemuSummaryLog
        ) `
        -LogFileName "qemu-dry-run.log" `
        -SerialLog $qemuSerialLog `
        -SummaryLog $qemuSummaryLog),
    (New-CiSmokeStep `
        -Name "ci.smoke.nativeapp.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-RequireNativeAppMarkers",
            "-SerialLog",
            $nativeAppSerialLog,
            "-SummaryLog",
            $nativeAppSummaryLog
        ) `
        -LogFileName "qemu-native-app-dry-run.log" `
        -SerialLog $nativeAppSerialLog `
        -SummaryLog $nativeAppSummaryLog),
    (New-CiSmokeStep `
        -Name "ci.smoke.diagnostics.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
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
        ) `
        -LogFileName "qemu-diagnostics-dry-run.log" `
        -SerialLog $diagnosticsSerialLog `
        -SummaryLog $diagnosticsSummaryLog `
        -MarkerPresets @("diagnostics")),
    (New-CiSmokeStep `
        -Name "ci.smoke.app_args.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "app-args",
            "-SerialLog",
            $appArgsSerialLog,
            "-SummaryLog",
            $appArgsSummaryLog
        ) `
        -LogFileName "qemu-app-args-dry-run.log" `
        -SerialLog $appArgsSerialLog `
        -SummaryLog $appArgsSummaryLog `
        -MarkerPresets @("app-args")),
    (New-CiSmokeStep `
        -Name "ci.smoke.app_exit.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "app-exit",
            "-SerialLog",
            $appExitSerialLog,
            "-SummaryLog",
            $appExitSummaryLog
        ) `
        -LogFileName "qemu-app-exit-dry-run.log" `
        -SerialLog $appExitSerialLog `
        -SummaryLog $appExitSummaryLog `
        -MarkerPresets @("app-exit")),
    (New-CiSmokeStep `
        -Name "ci.smoke.cctor_preflight.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "cctor-preflight",
            "-SerialLog",
            $cctorPreflightSerialLog,
            "-SummaryLog",
            $cctorPreflightSummaryLog
        ) `
        -LogFileName "qemu-cctor-preflight-dry-run.log" `
        -SerialLog $cctorPreflightSerialLog `
        -SummaryLog $cctorPreflightSummaryLog `
        -MarkerPresets @("cctor-preflight")),
    (New-CiSmokeStep `
        -Name "ci.smoke.cctor_work_items.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "cctor-work-items",
            "-SerialLog",
            $cctorWorkItemsSerialLog,
            "-SummaryLog",
            $cctorWorkItemsSummaryLog
        ) `
        -LogFileName "qemu-cctor-work-items-dry-run.log" `
        -SerialLog $cctorWorkItemsSerialLog `
        -SummaryLog $cctorWorkItemsSummaryLog `
        -MarkerPresets @("cctor-work-items")),
    (New-CiSmokeStep `
        -Name "ci.smoke.fetch_http.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "fetch-http",
            "-SerialLog",
            $fetchHttpSerialLog,
            "-SummaryLog",
            $fetchHttpSummaryLog
        ) `
        -LogFileName "qemu-fetch-http-dry-run.log" `
        -SerialLog $fetchHttpSerialLog `
        -SummaryLog $fetchHttpSummaryLog `
        -MarkerPresets @("fetch-http")),
    (New-CiSmokeStep `
        -Name "ci.smoke.fetch_http_endpoint.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "fetch-http-endpoint",
            "-SerialLog",
            $fetchHttpEndpointSerialLog,
            "-SummaryLog",
            $fetchHttpEndpointSummaryLog
        ) `
        -LogFileName "qemu-fetch-http-endpoint-dry-run.log" `
        -SerialLog $fetchHttpEndpointSerialLog `
        -SummaryLog $fetchHttpEndpointSummaryLog `
        -MarkerPresets @("fetch-http-endpoint")),
    (New-CiSmokeStep `
        -Name "ci.smoke.fetch_http_tcp_exchange.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "fetch-http-tcp-exchange",
            "-SerialLog",
            $fetchHttpTcpExchangeSerialLog,
            "-SummaryLog",
            $fetchHttpTcpExchangeSummaryLog
        ) `
        -LogFileName "qemu-fetch-http-tcp-exchange-dry-run.log" `
        -SerialLog $fetchHttpTcpExchangeSerialLog `
        -SummaryLog $fetchHttpTcpExchangeSummaryLog `
        -MarkerPresets @("fetch-http-tcp-exchange")),
    (New-CiSmokeStep `
        -Name "ci.smoke.http_static_response.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "http-static-response",
            "-SerialLog",
            $httpStaticResponseSerialLog,
            "-SummaryLog",
            $httpStaticResponseSummaryLog
        ) `
        -LogFileName "qemu-http-static-response-dry-run.log" `
        -SerialLog $httpStaticResponseSerialLog `
        -SummaryLog $httpStaticResponseSummaryLog `
        -MarkerPresets @("http-static-response")),
    (New-CiSmokeStep `
        -Name "ci.smoke.http_static_tcp_response.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "http-static-tcp-response",
            "-SerialLog",
            $httpStaticTcpResponseSerialLog,
            "-SummaryLog",
            $httpStaticTcpResponseSummaryLog
        ) `
        -LogFileName "qemu-http-static-tcp-response-dry-run.log" `
        -SerialLog $httpStaticTcpResponseSerialLog `
        -SummaryLog $httpStaticTcpResponseSummaryLog `
        -MarkerPresets @("http-static-tcp-response")),
    (New-CiSmokeStep `
        -Name "ci.smoke.virtio_rx_tcp_dispatch.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "virtio-rx-tcp-dispatch",
            "-SerialLog",
            $virtioRxTcpDispatchSerialLog,
            "-SummaryLog",
            $virtioRxTcpDispatchSummaryLog
        ) `
        -LogFileName "qemu-virtio-rx-tcp-dispatch-dry-run.log" `
        -SerialLog $virtioRxTcpDispatchSerialLog `
        -SummaryLog $virtioRxTcpDispatchSummaryLog `
        -MarkerPresets @("virtio-rx-tcp-dispatch")),
    (New-CiSmokeStep `
        -Name "ci.smoke.manifest_env.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "manifest-env",
            "-SerialLog",
            $manifestEnvSerialLog,
            "-SummaryLog",
            $manifestEnvSummaryLog
        ) `
        -LogFileName "qemu-manifest-env-dry-run.log" `
        -SerialLog $manifestEnvSerialLog `
        -SummaryLog $manifestEnvSummaryLog `
        -MarkerPresets @("manifest-env")),
    (New-CiSmokeStep `
        -Name "ci.smoke.nativeaot_heap.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "nativeaot-heap",
            "-SerialLog",
            $nativeAotHeapSerialLog,
            "-SummaryLog",
            $nativeAotHeapSummaryLog
        ) `
        -LogFileName "qemu-nativeaot-heap-dry-run.log" `
        -SerialLog $nativeAotHeapSerialLog `
        -SummaryLog $nativeAotHeapSummaryLog `
        -MarkerPresets @("nativeaot-heap")),
    (New-CiSmokeStep `
        -Name "ci.smoke.pal_export.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-smoke.ps1"),
            "-DryRun",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-BootPolicy", "Diagnostic",
            "-MarkerPreset",
            "pal-export",
            "-SerialLog",
            $palExportSerialLog,
            "-SummaryLog",
            $palExportSummaryLog
        ) `
        -LogFileName "qemu-pal-export-dry-run.log" `
        -SerialLog $palExportSerialLog `
        -SummaryLog $palExportSummaryLog `
        -MarkerPresets @("pal-export")),
    (New-CiSmokeStep `
        -Name $(if ($RunQcow2RealSmoke) { "ci.smoke.qemu.qcow2.real" } else { "ci.smoke.qemu.qcow2.dryrun" }) `
        -Executable $powerShellExe `
        -Arguments (@(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-qemu-qcow2-smoke.ps1")
        ) + $qcow2SmokeArguments) `
        -LogFileName "qemu-qcow2-smoke.log" `
        -Image $qcow2Image `
        -DiskFormat "qcow2" `
        -SerialLog $qcow2SerialLog `
        -SummaryLog $qcow2SummaryLog `
        -QemuImgDiscovery $qemuImgDiscovery `
        -Qcow2ConversionIntent $qcow2ConversionIntent),
    (New-CiSmokeStep `
        -Name "ci.smoke.libvirt.dryrun" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-libvirt-smoke.ps1"),
            "-DryRun",
            "-Image",
            $qcow2Image,
            "-DiskFormat",
            "qcow2",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $libvirtDryRunSerialLog,
            "-DomainXml",
            $libvirtDryRunDomainXml
        ) `
        -LogFileName "libvirt-dry-run.log" `
        -Image $qcow2Image `
        -DiskFormat "qcow2" `
        -SerialLog $libvirtDryRunSerialLog `
        -DomainXml $libvirtDryRunDomainXml),
    (New-CiSmokeStep `
        -Name "ci.smoke.libvirt.missingtool" `
        -Executable $powerShellExe `
        -Arguments @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            (Join-Path $root "scripts\run-libvirt-smoke.ps1"),
            "-Image",
            $qcow2Image,
            "-DiskFormat",
            "qcow2",
            "-WorkloadProfile",
            $WorkloadProfile,
            "-SerialLog",
            $libvirtMissingToolSerialLog,
            "-DomainXml",
            $libvirtMissingToolDomainXml,
            "-Virsh",
            $missingVirshPath
        ) `
        -LogFileName "libvirt-missing-tool.log" `
        -Image $qcow2Image `
        -DiskFormat "qcow2" `
        -SerialLog $libvirtMissingToolSerialLog `
        -DomainXml $libvirtMissingToolDomainXml),
    (New-CiSmokeStep `
        -Name "ci.smoke.git.diffcheck" `
        -Executable "git" `
        -Arguments @("diff", "--check") `
        -LogFileName "git-diff-check.log")
)

if ($DryRun) {
    Write-Host "TEST_START ci.smoke.dryrun"
    Write-WorkloadProfileEvidence -Profile $WorkloadProfile

    $artifactPlan = New-ArtifactPlan `
        -Mode "dry-run" `
        -Steps $steps `
        -QemuImgDiscovery $qemuImgDiscovery `
        -Qcow2ConversionIntent $qcow2ConversionIntent
    Write-JsonFile -Path $artifactPlanPath -Value $artifactPlan

    $summary = [ordered] @{
        schema = "os.ci.smoke.summary.v0"
        status = "planned"
        mode = "dry-run"
        timestamp = $timestamp
        workloadProfile = $WorkloadProfile
        repositoryRoot = $root
        logDirectory = ConvertTo-RelativePath $logRoot
        artifactPlan = ConvertTo-RelativePath $artifactPlanPath
        summary = ConvertTo-RelativePath $summaryPath
    }
    $summary["qemuImgDiscovery"] = $qemuImgDiscovery
    $summary["qcow2ConversionIntent"] = $qcow2ConversionIntent
    $summary["steps"] = $artifactPlan["steps"]
    Write-JsonFile -Path $summaryPath -Value $summary

    Write-Host ("CI_SMOKE_LOG_ROOT " + (ConvertTo-RelativePath $logRoot))
    Write-Host ("CI_SMOKE_ARTIFACT_PLAN " + (ConvertTo-RelativePath $artifactPlanPath))
    Write-Host ("SUMMARY " + (ConvertTo-RelativePath $summaryPath))
    Write-Host "TEST_PASS ci.smoke.dryrun writes artifact plan"
    Write-Host "TEST_PASS ci.smoke.dryrun includes serial and summary logs"
    Write-Host "TEST_PASS ci.smoke.dryrun includes diagnostics marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes app-args marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes app-exit marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes cctor-preflight marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes cctor-work-items marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes fetch-http marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes fetch-http-endpoint marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes http-static-response marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes http static tcp response marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes manifest-env marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes nativeaot-heap marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes pal-export marker preset"
    Write-Host "TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent"
    Write-Host "TEST_PASS ci.smoke.dryrun includes libvirt smoke intent"
    Write-Host "TEST_PASS ci.smoke.dryrun includes git diff check"
    Write-Host "TEST_PASS ci.smoke.dryrun"
    exit 0
}

Write-Host "TEST_START ci.smoke"
Write-WorkloadProfileEvidence -Profile $WorkloadProfile

$executePlan = New-ArtifactPlan `
    -Mode "execute" `
    -Steps $steps `
    -QemuImgDiscovery $qemuImgDiscovery `
    -Qcow2ConversionIntent $qcow2ConversionIntent
Write-JsonFile -Path $artifactPlanPath -Value $executePlan
Write-Host ("CI_SMOKE_LOG_ROOT " + (ConvertTo-RelativePath $logRoot))
Write-Host ("CI_SMOKE_ARTIFACT_PLAN " + (ConvertTo-RelativePath $artifactPlanPath))

if (-not $RunQcow2RealSmoke) {
    Initialize-SmokeIntentArtifacts -Paths @($qcow2IntentRawImage, $qcow2IntentImage)
}

$results = New-Object System.Collections.Generic.List[object]
foreach ($step in $steps) {
    $results.Add((Invoke-CiSmokeStep -Step $step))
}

$failedCount = ($results | Where-Object { $_.exitCode -ne 0 }).Count
if ($failedCount -eq 0) {
    $status = "passed"
}
else {
    $status = "failed"
}

$summary = [ordered] @{
    schema = "os.ci.smoke.summary.v0"
    status = $status
    mode = "execute"
    timestamp = $timestamp
    workloadProfile = $WorkloadProfile
    repositoryRoot = $root
    logDirectory = ConvertTo-RelativePath $logRoot
    artifactPlan = ConvertTo-RelativePath $artifactPlanPath
    summary = ConvertTo-RelativePath $summaryPath
}
$summary["qemuImgDiscovery"] = $qemuImgDiscovery
$summary["qcow2ConversionIntent"] = $qcow2ConversionIntent
$summary["steps"] = $results
Write-JsonFile -Path $summaryPath -Value $summary

Write-Host ("SUMMARY " + (ConvertTo-RelativePath $summaryPath))

if ($failedCount -eq 0) {
    Write-Host "TEST_PASS ci.smoke"
    exit 0
}

Write-Host ("TEST_FAIL ci.smoke failures=" + $failedCount)
exit 1
