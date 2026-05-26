param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $LogPath = "artifacts/logs/efi-shell-publish.log",
    [string] $EfiOutputPath = "artifacts/efi/BOOTX64.EFI",
    [string] $VsWherePath = "",
    [string[]] $MsvcLinkerSearchRoot = @(),
    [string[]] $WindowsSdkLibSearchRoot = @(),
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
        [string] $PathPattern
    )

    foreach ($candidateRoot in $SearchRoot) {
        if ([string]::IsNullOrWhiteSpace($candidateRoot)) {
            continue
        }

        if (-not (Test-Path -LiteralPath $candidateRoot)) {
            continue
        }

        $match = Get-ChildItem -LiteralPath $candidateRoot -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match $PathPattern } |
            Select-Object -First 1
        if ($null -ne $match) {
            return $match.FullName
        }
    }

    return $null
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

if ([System.IO.Path]::IsPathRooted($LogPath)) {
    $logFullPath = $LogPath
}
else {
    $logFullPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) $LogPath
}
$logDirectory = Split-Path -Parent $logFullPath
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

Write-Host "TEST_START efi.shell.publish"

Write-EfiPreflightLine "EFI_PREFLIGHT_START"

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

$linkerPath = Resolve-ToolPath -SearchRoot $linkerRoots -FileName "link.exe" -PathPattern "Hostx64\\x64"
if ($null -eq $linkerPath) {
    Write-EfiPreflightLine "EFI_PREFLIGHT_MSVC_LINKER missing"
    Save-EfiPreflightLog -Path $logFullPath
    Write-Host "TEST_FAIL efi.shell.publish platform_linker_missing log=$LogPath"
    exit 1
}

Write-EfiPreflightLine "EFI_PREFLIGHT_MSVC_LINKER $linkerPath"

if ($WindowsSdkLibSearchRoot.Count -gt 0) {
    $sdkRoots = $WindowsSdkLibSearchRoot
}
else {
    $sdkRoots = Get-DefaultWindowsSdkLibSearchRoot
}

$sdkLibPath = Resolve-ToolPath -SearchRoot $sdkRoots -FileName "kernel32.lib" -PathPattern "\\um\\x64\\"
if ($null -eq $sdkLibPath) {
    Write-EfiPreflightLine "EFI_PREFLIGHT_WINDOWS_SDK missing"
    Save-EfiPreflightLog -Path $logFullPath
    Write-Host "TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing log=$LogPath"
    exit 1
}

Write-EfiPreflightLine "EFI_PREFLIGHT_WINDOWS_SDK $sdkLibPath"
Write-EfiPreflightLine "EFI_PREFLIGHT_OK"
Save-EfiPreflightLog -Path $logFullPath

if ($PreflightOnly) {
    exit 0
}

$arguments = @(
    "publish",
    "src/OS/OS.csproj",
    "-c", $Configuration,
    "-r", $Runtime,
    "--nologo"
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

& dotnet @arguments 2>&1 | Tee-Object -FilePath $logFullPath -Append
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    $publishOutput = Join-Path $PSScriptRoot "..\src\OS\bin\$Configuration\net10.0\$Runtime\publish\OS.exe"
    $efiFullPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) $EfiOutputPath
    $efiDirectory = Split-Path -Parent $efiFullPath
    New-Item -ItemType Directory -Path $efiDirectory -Force | Out-Null
    Copy-Item -Path $publishOutput -Destination $efiFullPath -Force
    Write-Host "TEST_PASS efi.shell.publish"
    Write-Host "EFI_ARTIFACT $EfiOutputPath"
    exit 0
}

$logText = Get-Content -Raw -Path $logFullPath
if ($logText.IndexOf("Platform linker not found", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
    Write-Host "TEST_FAIL efi.shell.publish platform_linker_missing log=$LogPath"
    exit $exitCode
}

Write-Host "TEST_FAIL efi.shell.publish exit_code=$exitCode log=$LogPath"
exit $exitCode
