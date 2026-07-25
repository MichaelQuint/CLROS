param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $LogPath = "artifacts/logs/efi-shell-publish.log",
    [string] $EfiOutputPath = "artifacts/efi/BOOTX64.EFI",
    [string] $VsWherePath = "",
    [string[]] $MsvcLinkerSearchRoot = @(),
    [string[]] $WindowsSdkLibSearchRoot = @(),
    [string] $IlCompilerPath = "",
    [string[]] $IlCompilerSearchRoot = @(),
    [string] $NativeAotRuntimePackPath = "",
    [string[]] $NativeAotRuntimePackSearchRoot = @(),
    [string] $QemuPath = "",
    [string[]] $QemuSearchRoot = @(),
    [string] $QemuImgPath = "",
    [string[]] $QemuImgSearchRoot = @(),
    [string] $OvmfCodePath = "",
    [string[]] $OvmfSearchRoot = @(),
    [ValidateRange(100, 60000)]
    [int] $VersionProbeTimeoutMilliseconds = 5000,
    [switch] $PreflightOnly,
    [switch] $EnableUefiGoneSimulationProbe,
    [switch] $EnableExitBootServicesFreshMapProbe,
    [switch] $EnableAcpiPhysicalDiscoveryProbe
)

$ErrorActionPreference = "Stop"

$script:EfiPreflightLines = New-Object System.Collections.Generic.List[string]

function Write-EfiPreflightLine {
    param([string] $Line)

    Write-Host $Line
    $script:EfiPreflightLines.Add($Line)
}

function Save-EfiPreflightLog {
    param([string] $Path)

    Set-Content -LiteralPath $Path -Value $script:EfiPreflightLines -Encoding UTF8
}

function Resolve-ToolPath {
    param(
        [string[]] $SearchRoot,
        [string] $FileName,
        [string] $PathPattern,
        [scriptblock] $Validator = $null
    )

    $candidateFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    $seenPaths = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($candidateRoot in $SearchRoot) {
        if ([string]::IsNullOrWhiteSpace($candidateRoot)) {
            continue
        }

        if (-not (Test-Path -LiteralPath $candidateRoot)) {
            continue
        }

        $rootMatches = Get-ChildItem -LiteralPath $candidateRoot -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue |
            Where-Object {
                [string]::IsNullOrWhiteSpace($PathPattern) -or
                $_.FullName -match $PathPattern
            }
        foreach ($match in $rootMatches) {
            if ($seenPaths.Add($match.FullName)) {
                $candidateFiles.Add($match)
            }
        }
    }

    $orderedMatches = $candidateFiles |
        Sort-Object `
            @{ Expression = { Get-NumericPathVersion -Path $_.FullName }; Descending = $true },
            @{ Expression = { $_.FullName }; Descending = $false }
    foreach ($match in $orderedMatches) {
        if ($null -eq $Validator -or (& $Validator $match.FullName)) {
            return $match.FullName
        }
    }

    return $null
}

function Get-NumericPathVersion {
    param([string] $Path)

    $best = [version] "0.0"
    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Path,
        '(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)')
    foreach ($match in $matches) {
        try {
            $parsed = [version] $match.Value
            if ($parsed -gt $best) {
                $best = $parsed
            }
        }
        catch {
            # Ignore path components that resemble, but are not, valid versions.
        }
    }

    return $best
}

function Test-PeX64Executable {
    param([string] $Path)

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            if ($stream.Length -lt 256) {
                return $false
            }

            $reader = New-Object System.IO.BinaryReader($stream)
            if ($reader.ReadUInt16() -ne 0x5a4d) {
                return $false
            }

            $stream.Position = 0x3c
            $peOffset = [long] $reader.ReadUInt32()
            if ($peOffset -lt 64 -or $peOffset -gt ($stream.Length - 24)) {
                return $false
            }

            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                return $false
            }

            return $reader.ReadUInt16() -eq 0x8664
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Test-CoffArchive {
    param([string] $Path)

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            if ($stream.Length -lt 68) {
                return $false
            }

            $signature = New-Object byte[] 8
            if ($stream.Read($signature, 0, $signature.Length) -ne $signature.Length) {
                return $false
            }

            return [System.Text.Encoding]::ASCII.GetString($signature) -eq "!<arch>`n"
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Test-NativeAotRuntimePack {
    param([string] $Path)

    try {
        $runtimeListPath = Join-Path $Path "data\RuntimeList.xml"
        if (-not (Test-Path -LiteralPath $runtimeListPath -PathType Leaf) -or
            (Get-Item -LiteralPath $runtimeListPath).Length -eq 0) {
            return $false
        }

        [xml] $runtimeList = Get-Content -LiteralPath $runtimeListPath -Raw
        return $null -ne $runtimeList.DocumentElement -and
            $runtimeList.DocumentElement.LocalName -eq "FileList"
    }
    catch {
        return $false
    }
}

function Test-OvmfCodeImage {
    param([string] $Path)

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::Read)
        try {
            if ($stream.Length -lt 65536) {
                return $false
            }

            $probeLength = [int] [System.Math]::Min(4096L, $stream.Length)
            $probe = New-Object byte[] $probeLength
            if ($stream.Read($probe, 0, $probe.Length) -ne $probe.Length) {
                return $false
            }

            for ($index = 0; $index -le $probe.Length - 4; $index++) {
                if ($probe[$index] -eq 0x5f -and
                    $probe[$index + 1] -eq 0x46 -and
                    $probe[$index + 2] -eq 0x56 -and
                    $probe[$index + 3] -eq 0x48) {
                    return $true
                }
            }

            return $false
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Resolve-ExactFilePath {
    param([string] $Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate) -or
        -not (Test-Path -LiteralPath $Candidate -PathType Leaf)) {
        return $null
    }

    return (Resolve-Path -LiteralPath $Candidate).ProviderPath
}

function Resolve-ExactDirectoryPath {
    param([string] $Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate) -or
        -not (Test-Path -LiteralPath $Candidate -PathType Container)) {
        return $null
    }

    return (Resolve-Path -LiteralPath $Candidate).ProviderPath
}

function Resolve-ExecutableToolPath {
    param(
        [string] $RequestedPath,
        [string[]] $SearchRoot,
        [string] $FileName,
        [string[]] $DefaultPath,
        [string[]] $CommandName,
        [scriptblock] $Validator = $null
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolvedRequested = Resolve-ExactFilePath -Candidate $RequestedPath
        if ($null -eq $resolvedRequested -or
            ($null -ne $Validator -and -not (& $Validator $resolvedRequested))) {
            return $null
        }

        return $resolvedRequested
    }

    if ($SearchRoot.Count -gt 0) {
        return Resolve-ToolPath `
            -SearchRoot $SearchRoot `
            -FileName $FileName `
            -PathPattern "" `
            -Validator $Validator
    }

    foreach ($candidate in $DefaultPath) {
        $resolved = Resolve-ExactFilePath -Candidate $candidate
        if ($null -ne $resolved -and
            ($null -eq $Validator -or (& $Validator $resolved))) {
            return $resolved
        }
    }

    foreach ($command in $CommandName) {
        $resolvedCommand = Get-Command $command -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $resolvedCommand -and
            ($null -eq $Validator -or (& $Validator $resolvedCommand.Source))) {
            return $resolvedCommand.Source
        }
    }

    return $null
}

function Resolve-NativeAotRuntimePackPath {
    param(
        [string] $RequestedPath,
        [string[]] $SearchRoot,
        [string] $DefaultPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolvedRequested = Resolve-ExactDirectoryPath -Candidate $RequestedPath
        if ($null -eq $resolvedRequested -or
            -not (Test-NativeAotRuntimePack -Path $resolvedRequested)) {
            return $null
        }

        return $resolvedRequested
    }

    if ($SearchRoot.Count -gt 0) {
        $candidatePacks = New-Object System.Collections.Generic.List[string]
        $seenPacks = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($candidateRoot in $SearchRoot) {
            $resolvedRoot = Resolve-ExactDirectoryPath -Candidate $candidateRoot
            if ($null -eq $resolvedRoot) {
                continue
            }

            if ($seenPacks.Add($resolvedRoot) -and
                (Test-Path -LiteralPath (Join-Path $resolvedRoot "data\RuntimeList.xml") -PathType Leaf)) {
                $candidatePacks.Add($resolvedRoot)
            }

            $runtimeLists = Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter "RuntimeList.xml" -ErrorAction SilentlyContinue |
                Where-Object { $_.Directory.Name -eq "data" }
            foreach ($runtimeList in $runtimeLists) {
                $packPath = $runtimeList.Directory.Parent.FullName
                if ($seenPacks.Add($packPath)) {
                    $candidatePacks.Add($packPath)
                }
            }
        }

        $orderedPacks = $candidatePacks |
            Sort-Object `
                @{ Expression = { Get-NumericPathVersion -Path $_ }; Descending = $true },
                @{ Expression = { $_ }; Descending = $false }
        foreach ($candidatePack in $orderedPacks) {
            if (Test-NativeAotRuntimePack -Path $candidatePack) {
                return $candidatePack
            }
        }

        return $null
    }

    $resolvedDefault = Resolve-ExactDirectoryPath -Candidate $DefaultPath
    if ($null -ne $resolvedDefault -and
        (Test-NativeAotRuntimePack -Path $resolvedDefault)) {
        return $resolvedDefault
    }

    return $null
}

function Get-PackageReferenceVersion {
    param(
        [string] $ProjectPath,
        [string] $PackageId
    )

    try {
        [xml] $project = Get-Content -LiteralPath $ProjectPath -Raw
        $packageReference = $project.Project.ItemGroup.PackageReference |
            Where-Object { $_.Include -eq $PackageId } |
            Select-Object -First 1
        if ($null -ne $packageReference -and
            -not [string]::IsNullOrWhiteSpace($packageReference.Version)) {
            return [string] $packageReference.Version
        }
    }
    catch {
        return $null
    }

    return $null
}

function Get-VersionFromPath {
    param([string] $Path)

    $matches = [System.Text.RegularExpressions.Regex]::Matches(
        $Path,
        '(?<!\d)\d+\.\d+(?:\.\d+){0,2}(?!\d)')
    if ($matches.Count -eq 0) {
        return "unknown"
    }

    return $matches[$matches.Count - 1].Value
}

function Get-FileVersionEvidence {
    param([string] $Path)

    try {
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
        foreach ($candidate in @($versionInfo.FileVersion, $versionInfo.ProductVersion)) {
            if (-not [string]::IsNullOrWhiteSpace($candidate)) {
                return $candidate.Trim()
            }
        }
    }
    catch {
        # Version metadata is optional for injected test fixtures.
    }

    return Get-VersionFromPath -Path $Path
}

function Stop-VersionProbeProcess {
    param([System.Diagnostics.Process] $Process)

    try {
        if (-not $Process.HasExited) {
            $taskKill = Get-Command "taskkill.exe" -CommandType Application -ErrorAction SilentlyContinue
            if ($null -ne $taskKill) {
                & $taskKill.Source /PID $Process.Id /T /F 2>$null | Out-Null
            }
            elseif (-not $Process.HasExited) {
                $Process.Kill()
            }
        }
    }
    catch {
        # The bounded caller reports timeout even if cleanup races process exit.
    }
}

function Get-ExecutableVersionEvidence {
    param(
        [string] $Path,
        [int] $TimeoutMilliseconds
    )

    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $extension = [System.IO.Path]::GetExtension($Path)
        if ($extension -eq ".cmd" -or $extension -eq ".bat") {
            $startInfo.FileName = $env:ComSpec
            $startInfo.Arguments = '/d /s /c ""' + $Path + '" --version"'
        }
        else {
            $startInfo.FileName = $Path
            $startInfo.Arguments = "--version"
        }

        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            return [pscustomobject] @{
                Status = "failed"
                Version = $null
            }
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            Stop-VersionProbeProcess -Process $process
            return [pscustomobject] @{
                Status = "timeout"
                Version = $null
            }
        }

        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            return [pscustomobject] @{
                Status = "failed"
                Version = $null
            }
        }

        $standardOutputText = $standardOutput.GetAwaiter().GetResult()
        $standardErrorText = $standardError.GetAwaiter().GetResult()
        $versionLines =
            @($standardOutputText -split "`r?`n") +
            @($standardErrorText -split "`r?`n")
        foreach ($line in $versionLines) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                return [pscustomobject] @{
                    Status = "ok"
                    Version = $line.Trim()
                }
            }
        }
    }
    catch {
        return [pscustomobject] @{
            Status = "failed"
            Version = $null
        }
    }

    return [pscustomobject] @{
        Status = "failed"
        Version = $null
    }
}

function Format-EvidenceValue {
    param([string] $Value)

    if ($null -eq $Value) {
        return '""'
    }

    $safeValue = $Value.Replace("`r", " ").Replace("`n", " ").Replace('"', '""')
    return '"' + $safeValue + '"'
}

function Stop-EfiPreflight {
    param(
        [string] $Marker,
        [string] $Reason,
        [string] $LogFullPath,
        [string] $ReportedLogPath,
        [string] $Status = "missing"
    )

    Write-EfiPreflightLine ($Marker + " " + $Status)
    Save-EfiPreflightLog -Path $LogFullPath
    Write-Host ("TEST_FAIL efi.shell.publish " + $Reason + " log=" + $ReportedLogPath)
    exit 1
}

function Get-DefaultMsvcLinkerSearchRoot {
    param([string] $VsWhere)

    $roots = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($VsWhere) -and (Test-Path -LiteralPath $VsWhere)) {
        try {
            $installationPaths = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
            foreach ($installationPath in $installationPaths) {
                if (-not [string]::IsNullOrWhiteSpace($installationPath)) {
                    $roots.Add((Join-Path $installationPath.Trim() "VC\Tools\MSVC"))
                }
            }
        }
        catch {
            # vswhere discovery is best-effort; fall back to well-known install roots below.
        }
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        foreach ($edition in @("BuildTools", "Community", "Professional", "Enterprise")) {
            $roots.Add((Join-Path $programFilesX86 "Microsoft Visual Studio\2022\$edition\VC\Tools\MSVC"))
        }
    }

    return $roots
}

function Get-DefaultWindowsSdkLibSearchRoot {
    $roots = New-Object System.Collections.Generic.List[string]

    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not [string]::IsNullOrWhiteSpace($base)) {
            $roots.Add((Join-Path $base "Windows Kits\10\Lib"))
        }
    }

    return $roots
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).ProviderPath
$efiProjectPath = Join-Path $repositoryRoot "src\OS\OS.csproj"

if ([System.IO.Path]::IsPathRooted($LogPath)) {
    $logFullPath = $LogPath
}
else {
    $logFullPath = Join-Path $repositoryRoot $LogPath
}
$logDirectory = Split-Path -Parent $logFullPath
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

Write-Host "TEST_START efi.shell.publish"

Write-EfiPreflightLine "EFI_PREFLIGHT_START"

if (-not [string]::Equals($Runtime, "win-x64", [System.StringComparison]::OrdinalIgnoreCase)) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_RUNTIME" `
        -Reason "runtime_not_win_x64" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "unsupported"
}

if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $VsWherePath = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    }
}

if ($MsvcLinkerSearchRoot.Count -gt 0) {
    $linkerRoots = $MsvcLinkerSearchRoot
}
else {
    $linkerRoots = Get-DefaultMsvcLinkerSearchRoot -VsWhere $VsWherePath
}

$linkerPath = Resolve-ToolPath `
    -SearchRoot $linkerRoots `
    -FileName "link.exe" `
    -PathPattern "Hostx64\\x64" `
    -Validator ${function:Test-PeX64Executable}
if ($null -eq $linkerPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_MSVC_LINKER" `
        -Reason "platform_linker_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$linkerVersion = Get-FileVersionEvidence -Path $linkerPath
if ($linkerVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_MSVC_LINKER" `
        -Reason "platform_linker_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_MSVC_LINKER path=" + (Format-EvidenceValue $linkerPath) +
    " version=" + (Format-EvidenceValue $linkerVersion))

if ($WindowsSdkLibSearchRoot.Count -gt 0) {
    $sdkRoots = $WindowsSdkLibSearchRoot
}
else {
    $sdkRoots = Get-DefaultWindowsSdkLibSearchRoot
}

$sdkLibPath = Resolve-ToolPath `
    -SearchRoot $sdkRoots `
    -FileName "kernel32.lib" `
    -PathPattern "\\um\\x64\\" `
    -Validator ${function:Test-CoffArchive}
if ($null -eq $sdkLibPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_WINDOWS_SDK" `
        -Reason "windows_sdk_um_x64_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$sdkVersion = Get-VersionFromPath -Path $sdkLibPath
if ($sdkVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_WINDOWS_SDK" `
        -Reason "windows_sdk_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_WINDOWS_SDK path=" + (Format-EvidenceValue $sdkLibPath) +
    " version=" + (Format-EvidenceValue $sdkVersion))

$ilCompilerPackageId = "runtime.$Runtime.Microsoft.DotNet.ILCompiler"
$nativeAotRuntimePackId = "Microsoft.NETCore.App.Runtime.NativeAOT.$Runtime"
$ilCompilerPackageVersion = Get-PackageReferenceVersion `
    -ProjectPath $efiProjectPath `
    -PackageId $ilCompilerPackageId
$nativeAotRuntimePackVersion = Get-PackageReferenceVersion `
    -ProjectPath $efiProjectPath `
    -PackageId $nativeAotRuntimePackId
if ([string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    $nativeAotRuntimePackVersion = Get-PackageReferenceVersion `
        -ProjectPath $efiProjectPath `
        -PackageId "Microsoft.DotNet.ILCompiler"
}

if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $nugetPackageRoot = $env:NUGET_PACKAGES
}
else {
    $nugetPackageRoot = Join-Path $env:USERPROFILE ".nuget\packages"
}

$defaultIlCompilerPath = ""
if (-not [string]::IsNullOrWhiteSpace($ilCompilerPackageVersion)) {
    $defaultIlCompilerPath = Join-Path $nugetPackageRoot (
        $ilCompilerPackageId.ToLowerInvariant() +
        "\" +
        $ilCompilerPackageVersion +
        "\tools\ilc.exe")
}

$resolvedIlCompilerPath = Resolve-ExecutableToolPath `
    -RequestedPath $IlCompilerPath `
    -SearchRoot $IlCompilerSearchRoot `
    -FileName "ilc.exe" `
    -DefaultPath @($defaultIlCompilerPath) `
    -CommandName @() `
    -Validator ${function:Test-PeX64Executable}
if ($null -eq $resolvedIlCompilerPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_ILCOMPILER" `
        -Reason "ilcompiler_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$ilCompilerFileVersion = Get-FileVersionEvidence -Path $resolvedIlCompilerPath
if ([string]::IsNullOrWhiteSpace($ilCompilerPackageVersion) -or
    $ilCompilerFileVersion -eq "unknown") {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_ILCOMPILER" `
        -Reason "ilcompiler_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_ILCOMPILER path=" + (Format-EvidenceValue $resolvedIlCompilerPath) +
    " packageVersion=" + (Format-EvidenceValue $ilCompilerPackageVersion) +
    " fileVersion=" + (Format-EvidenceValue $ilCompilerFileVersion))

$defaultNativeAotRuntimePackPath = ""
if (-not [string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    $defaultNativeAotRuntimePackPath = Join-Path $nugetPackageRoot (
        $nativeAotRuntimePackId.ToLowerInvariant() +
        "\" +
        $nativeAotRuntimePackVersion)
}

$resolvedNativeAotRuntimePackPath = Resolve-NativeAotRuntimePackPath `
    -RequestedPath $NativeAotRuntimePackPath `
    -SearchRoot $NativeAotRuntimePackSearchRoot `
    -DefaultPath $defaultNativeAotRuntimePackPath
if ($null -eq $resolvedNativeAotRuntimePackPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK" `
        -Reason "nativeaot_runtime_pack_missing" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}
if ([string]::IsNullOrWhiteSpace($nativeAotRuntimePackVersion)) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK" `
        -Reason "nativeaot_runtime_pack_version_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unavailable"
}

$runtimeListPath = Join-Path $resolvedNativeAotRuntimePackPath "data\RuntimeList.xml"
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=" +
    (Format-EvidenceValue $resolvedNativeAotRuntimePackPath) +
    " packageVersion=" + (Format-EvidenceValue $nativeAotRuntimePackVersion) +
    " manifest=" + (Format-EvidenceValue $runtimeListPath))

$defaultQemuPaths = New-Object System.Collections.Generic.List[string]
$defaultQemuImgPaths = New-Object System.Collections.Generic.List[string]
$defaultOvmfPaths = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($env:QEMU)) {
    $defaultQemuPaths.Add($env:QEMU)
}
if (-not [string]::IsNullOrWhiteSpace($env:QEMU_IMG)) {
    $defaultQemuImgPaths.Add($env:QEMU_IMG)
}
foreach ($candidate in @($env:OS_OVMF_CODE, $env:OVMF_CODE, $env:QEMU_OVMF_CODE)) {
    if (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $defaultOvmfPaths.Add($candidate)
    }
}
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $qemuInstallRoot = Join-Path $env:ProgramFiles "qemu"
    $defaultQemuPaths.Add((Join-Path $qemuInstallRoot "qemu-system-x86_64.exe"))
    $defaultQemuImgPaths.Add((Join-Path $qemuInstallRoot "qemu-img.exe"))
    $defaultOvmfPaths.Add((Join-Path $qemuInstallRoot "share\edk2-x86_64-code.fd"))
    $defaultOvmfPaths.Add((Join-Path $qemuInstallRoot "share\OVMF_CODE.fd"))
}

$resolvedQemuPath = Resolve-ExecutableToolPath `
    -RequestedPath $QemuPath `
    -SearchRoot $QemuSearchRoot `
    -FileName "qemu-system-x86_64.exe" `
    -DefaultPath $defaultQemuPaths `
    -CommandName @("qemu-system-x86_64.exe", "qemu-system-x86_64")
if ($null -eq $resolvedQemuPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason "qemu_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$qemuVersionEvidence = Get-ExecutableVersionEvidence `
    -Path $resolvedQemuPath `
    -TimeoutMilliseconds $VersionProbeTimeoutMilliseconds
if ($qemuVersionEvidence.Status -ne "ok") {
    $reason = if ($qemuVersionEvidence.Status -eq "timeout") {
        "qemu_version_timeout"
    }
    else {
        "qemu_version_unavailable"
    }
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason $reason `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status $qemuVersionEvidence.Status
}
$qemuVersion = $qemuVersionEvidence.Version
if ($qemuVersion -notmatch '(?i)\bQEMU\b.*\bversion\b') {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU" `
        -Reason "qemu_version_unrecognized" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unrecognized"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_QEMU path=" + (Format-EvidenceValue $resolvedQemuPath) +
    " version=" + (Format-EvidenceValue $qemuVersion))

$resolvedQemuImgPath = Resolve-ExecutableToolPath `
    -RequestedPath $QemuImgPath `
    -SearchRoot $QemuImgSearchRoot `
    -FileName "qemu-img.exe" `
    -DefaultPath $defaultQemuImgPaths `
    -CommandName @("qemu-img.exe", "qemu-img")
if ($null -eq $resolvedQemuImgPath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason "qemu_img_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

$qemuImgVersionEvidence = Get-ExecutableVersionEvidence `
    -Path $resolvedQemuImgPath `
    -TimeoutMilliseconds $VersionProbeTimeoutMilliseconds
if ($qemuImgVersionEvidence.Status -ne "ok") {
    $reason = if ($qemuImgVersionEvidence.Status -eq "timeout") {
        "qemu_img_version_timeout"
    }
    else {
        "qemu_img_version_unavailable"
    }
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason $reason `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status $qemuImgVersionEvidence.Status
}
$qemuImgVersion = $qemuImgVersionEvidence.Version
if ($qemuImgVersion -notmatch '(?i)\bqemu-img\b.*\bversion\b') {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_QEMU_IMG" `
        -Reason "qemu_img_version_unrecognized" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "version_unrecognized"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_QEMU_IMG path=" + (Format-EvidenceValue $resolvedQemuImgPath) +
    " version=" + (Format-EvidenceValue $qemuImgVersion))

if (-not [string]::IsNullOrWhiteSpace($OvmfCodePath)) {
    $resolvedOvmfCodePath = Resolve-ExactFilePath -Candidate $OvmfCodePath
    if ($null -ne $resolvedOvmfCodePath -and
        -not (Test-OvmfCodeImage -Path $resolvedOvmfCodePath)) {
        $resolvedOvmfCodePath = $null
    }
}
elseif ($OvmfSearchRoot.Count -gt 0) {
    $resolvedOvmfCodePath = Resolve-ToolPath `
        -SearchRoot $OvmfSearchRoot `
        -FileName "edk2-x86_64-code.fd" `
        -PathPattern "" `
        -Validator ${function:Test-OvmfCodeImage}
    if ($null -eq $resolvedOvmfCodePath) {
        $resolvedOvmfCodePath = Resolve-ToolPath `
            -SearchRoot $OvmfSearchRoot `
            -FileName "OVMF_CODE.fd" `
            -PathPattern "" `
            -Validator ${function:Test-OvmfCodeImage}
    }
}
else {
    $resolvedOvmfCodePath = $null
    foreach ($candidate in $defaultOvmfPaths) {
        $resolvedOvmfCodePath = Resolve-ExactFilePath -Candidate $candidate
        if ($null -ne $resolvedOvmfCodePath -and
            (Test-OvmfCodeImage -Path $resolvedOvmfCodePath)) {
            break
        }

        $resolvedOvmfCodePath = $null
    }
}

if ($null -eq $resolvedOvmfCodePath) {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_OVMF" `
        -Reason "ovmf_not_found" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath
}

try {
    $ovmfHash = (Get-FileHash -LiteralPath $resolvedOvmfCodePath -Algorithm SHA256).Hash
    $ovmfLength = (Get-Item -LiteralPath $resolvedOvmfCodePath).Length
}
catch {
    Stop-EfiPreflight `
        -Marker "EFI_PREFLIGHT_OVMF" `
        -Reason "ovmf_evidence_unavailable" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "evidence_unavailable"
}
Write-EfiPreflightLine (
    "EFI_PREFLIGHT_OVMF path=" + (Format-EvidenceValue $resolvedOvmfCodePath) +
    " bytes=" + $ovmfLength +
    " sha256=" + $ovmfHash)

Write-EfiPreflightLine "EFI_PREFLIGHT_OK"
Save-EfiPreflightLog -Path $logFullPath

if ($PreflightOnly) {
    exit 0
}

$arguments = @(
    "publish",
    $efiProjectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--nologo",
    "-p:IlcUseEnvironmentalTools=true",
    "-p:CppLinker=$linkerPath",
    "-p:IlcHostPackagePath=$(Split-Path -Parent (Split-Path -Parent $resolvedIlCompilerPath))",
    "-p:IlcToolsPath=$(Split-Path -Parent $resolvedIlCompilerPath)",
    "-p:RuntimePackagePath=$resolvedNativeAotRuntimePackPath",
    "-p:IlcGenerateMapFile=true"
)

if ($EnableUefiGoneSimulationProbe) {
    $arguments += "-p:EnableUefiGoneSimulationProbe=true"
    Write-Host "EFI_PROBE UEFI_GONE_SIMULATION"
}

if ($EnableExitBootServicesFreshMapProbe) {
    $arguments += "-p:EnableExitBootServicesFreshMapProbe=true"
    Write-Host "EFI_PROBE EXIT_BOOT_SERVICES_FRESH_MAP"
}

if ($EnableAcpiPhysicalDiscoveryProbe) {
    $arguments += "-p:EnableAcpiPhysicalDiscoveryProbe=true"
    Write-Host "EFI_PROBE ACPI_PHYSICAL_DISCOVERY"
}

$nativeIntermediateDirectory = Join-Path $repositoryRoot (
    "src\OS\obj\$Configuration\net10.0\$Runtime\native")
$nativeBinaryPath = Join-Path $repositoryRoot (
    "src\OS\bin\$Configuration\net10.0\$Runtime\native\OS.exe")
$linkerMapWorkingPath = Join-Path $nativeIntermediateDirectory "OS.link.map"
$linkResponseWorkingPath = Join-Path $nativeIntermediateDirectory "link.rsp"

foreach ($generatedPath in @($nativeBinaryPath, $linkerMapWorkingPath)) {
    if (Test-Path -LiteralPath $generatedPath -PathType Leaf) {
        Remove-Item -LiteralPath $generatedPath -Force
    }
}

$linkerHash = (Get-FileHash -LiteralPath $linkerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$ilCompilerHash = (Get-FileHash -LiteralPath $resolvedIlCompilerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sdkLibHash = (Get-FileHash -LiteralPath $sdkLibPath -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeListHash = (Get-FileHash -LiteralPath $runtimeListPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host (
    "EFI_PUBLISH_TOOLCHAIN_PIN linkerPath=" + (Format-EvidenceValue $linkerPath) +
    " linkerSha256=" + $linkerHash +
    " ilcompilerPath=" + (Format-EvidenceValue $resolvedIlCompilerPath) +
    " ilcompilerSha256=" + $ilCompilerHash)

$previousLinkEnvironment = $env:LINK
$previousTrailingLinkEnvironment = $env:_LINK_
try {
    $env:LINK = '/MAP:"' + $linkerMapWorkingPath + '" /MAPINFO:EXPORTS'
    $env:_LINK_ = $null
    & dotnet @arguments 2>&1 | Tee-Object -FilePath $logFullPath -Append
    $exitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousLinkEnvironment) {
        $env:LINK = $null
    }
    else {
        $env:LINK = $previousLinkEnvironment
    }

    if ($null -eq $previousTrailingLinkEnvironment) {
        $env:_LINK_ = $null
    }
    else {
        $env:_LINK_ = $previousTrailingLinkEnvironment
    }
}

if ($exitCode -eq 0) {
    $publishOutput = Join-Path $repositoryRoot (
        "src\OS\bin\$Configuration\net10.0\$Runtime\publish\OS.exe")
    if ([System.IO.Path]::IsPathRooted($EfiOutputPath)) {
        $efiFullPath = [System.IO.Path]::GetFullPath($EfiOutputPath)
    }
    else {
        $efiFullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $repositoryRoot $EfiOutputPath))
    }
    $efiDirectory = Split-Path -Parent $efiFullPath
    New-Item -ItemType Directory -Path $efiDirectory -Force | Out-Null

    foreach ($requiredOutput in @(
        $publishOutput,
        $linkerMapWorkingPath,
        $linkResponseWorkingPath
    )) {
        if (-not (Test-Path -LiteralPath $requiredOutput -PathType Leaf) -or
            (Get-Item -LiteralPath $requiredOutput).Length -eq 0) {
            Write-Host (
                "TEST_FAIL efi.shell.publish linker_evidence_missing path=" +
                $requiredOutput)
            exit 1
        }
    }

    Copy-Item -Path $publishOutput -Destination $efiFullPath -Force
    $linkerMapOutputPath = $efiFullPath + ".link.map"
    $linkResponseOutputPath = $efiFullPath + ".link.rsp"
    $linkEvidenceOutputPath = $efiFullPath + ".link.json"
    Copy-Item -LiteralPath $linkerMapWorkingPath -Destination $linkerMapOutputPath -Force
    Copy-Item -LiteralPath $linkResponseWorkingPath -Destination $linkResponseOutputPath -Force

    $artifactHash = (Get-FileHash -LiteralPath $efiFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $linkerMapHash = (Get-FileHash -LiteralPath $linkerMapOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $linkResponseHash = (Get-FileHash -LiteralPath $linkResponseOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $projectHash = (Get-FileHash -LiteralPath $efiProjectPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $entrySourcePath = Join-Path $repositoryRoot "src\OS\Boot\EfiEntry.cs"
    $entrySourceHash = (Get-FileHash -LiteralPath $entrySourcePath -Algorithm SHA256).Hash.ToLowerInvariant()

    $linkEvidence = [ordered] @{
        schemaVersion = 1
        artifactSha256 = $artifactHash
        artifactBytes = (Get-Item -LiteralPath $efiFullPath).Length
        linkerMapSha256 = $linkerMapHash
        linkResponseSha256 = $linkResponseHash
        sourceProjectSha256 = $projectHash
        entrySourceSha256 = $entrySourceHash
        entryPointSymbol = "EfiMain"
        managedEntrySymbol = "OS_OS_Boot_EfiEntry__EfiMain"
        linkerPath = $linkerPath
        linkerVersion = $linkerVersion
        linkerSha256 = $linkerHash
        windowsSdkLibraryPath = $sdkLibPath
        windowsSdkVersion = $sdkVersion
        windowsSdkLibrarySha256 = $sdkLibHash
        ilCompilerPath = $resolvedIlCompilerPath
        ilCompilerPackageVersion = $ilCompilerPackageVersion
        ilCompilerFileVersion = $ilCompilerFileVersion
        ilCompilerSha256 = $ilCompilerHash
        nativeAotRuntimePackPath = $resolvedNativeAotRuntimePackPath
        nativeAotRuntimePackVersion = $nativeAotRuntimePackVersion
        runtimeListSha256 = $runtimeListHash
        generatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    }
    $linkEvidenceJson = $linkEvidence | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText(
        $linkEvidenceOutputPath,
        $linkEvidenceJson,
        (New-Object System.Text.UTF8Encoding($false)))

    $verifierScript = Join-Path $repositoryRoot "scripts\test-efi-artifact.ps1"
    $verificationOutput = @(
        & powershell.exe `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $verifierScript `
            -EfiArtifact $efiFullPath `
            -ProjectPath $efiProjectPath `
            -EntrySourcePath $entrySourcePath `
            -LinkerMapPath $linkerMapOutputPath `
            -LinkResponsePath $linkResponseOutputPath `
            -LinkEvidencePath $linkEvidenceOutputPath 2>&1
    )
    $verificationExitCode = $LASTEXITCODE
    foreach ($verificationLine in $verificationOutput) {
        Write-Host $verificationLine
        Add-Content -LiteralPath $logFullPath -Value $verificationLine -Encoding UTF8
    }
    if ($verificationExitCode -ne 0) {
        Write-Host (
            "TEST_FAIL efi.shell.publish artifact_verification_failed exit_code=" +
            $verificationExitCode +
            " log=" +
            $LogPath)
        exit $verificationExitCode
    }

    Write-Host (
        "EFI_PUBLISH_TOOLCHAIN_VERIFIED linkerPath=" +
        (Format-EvidenceValue $linkerPath) +
        " mapSha256=" +
        $linkerMapHash +
        " responseSha256=" +
        $linkResponseHash)
    Write-Host "TEST_PASS efi.shell.publish"
    Write-Host "EFI_ARTIFACT $EfiOutputPath"
    Write-Host "EFI_LINKER_MAP $linkerMapOutputPath"
    Write-Host "EFI_LINK_EVIDENCE $linkEvidenceOutputPath"
    exit 0
}

$logText = Get-Content -Raw -Path $logFullPath
if ($logText.IndexOf("Platform linker not found", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    Write-Host "TEST_FAIL efi.shell.publish platform_linker_missing log=$LogPath"
    exit $exitCode
}

Write-Host "TEST_FAIL efi.shell.publish exit_code=$exitCode log=$LogPath"
exit $exitCode
