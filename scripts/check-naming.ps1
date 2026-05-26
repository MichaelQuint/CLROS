param(
    [string] $Root
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $PSScriptRoot ".."
}

$root = (Resolve-Path $Root).ProviderPath
$excludedDirectories = @(
    ".claude",
    ".git",
    ".vs",
    "artifacts",
    "bin",
    "obj",
    "references"
)

$firstPartySourceRoots = @(
    "src",
    "std",
    "apps"
)

$firstPartyProjectRoots = @(
    "src",
    "std",
    "apps"
)

$forbiddenSourceExtensions = @(
    ".c",
    ".cpp",
    ".h",
    ".asm",
    ".s"
)

$disallowedPatterns = @(
    @{ Pattern = 'SHARPOS_BOOT_OK'; Reason = "Use OS_BOOT_OK for first-party smoke markers." },
    @{ Pattern = 'sharpos\.raw'; Reason = "Use artifacts/images/os.raw for first-party image defaults." },
    @{ Pattern = 'SharpOS\s+(project|workspace|core|kernel|release|completion|gate|gates)'; Reason = "Use OS for first-party project naming; SharpOS is a reference only." },
    @{ Pattern = 'first-party\s+SharpOS'; Reason = "Use first-party OS; SharpOS is a reference only." },
    @{ Pattern = '`src/Boot/BootSequence\.cs`'; Reason = "Use src/OS.Kernel/Boot/BootSequence.cs." },
    @{ Pattern = '`src/Kernel/Diagnostics/Probes\.cs`'; Reason = "Use src/OS.Kernel/Diagnostics/Probes.cs." }
)

function Test-IsExcludedPath {
    param([string] $Path)

    $relative = Get-RelativePath $Path
    $segments = $relative -split '[\\/]'
    foreach ($segment in $segments) {
        if ($excludedDirectories -contains $segment) {
            return $true
        }
    }

    return $false
}

function Get-RelativePath {
    param([string] $Path)

    if ($Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Path.Substring($root.Length).TrimStart('\', '/')
    }

    return $Path
}

function Test-IsUnderFirstPartyRoot {
    param(
        [string] $Path,
        [string[]] $Roots
    )

    $relative = Get-RelativePath $Path
    $segments = $relative -split '[\\/]'

    return $segments.Length -gt 0 -and ($Roots -contains $segments[0])
}

function Test-ContainsReferencePath {
    param([string] $Value)

    return $Value -match '(^|["''\s=]|[\\/])references([\\/]|$)'
}

$files = Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object {
        -not (Test-IsExcludedPath $_.FullName) -and
        $_.FullName -ne $PSCommandPath
    }

$failures = New-Object System.Collections.Generic.List[string]
$sourceFailures = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $relative = Get-RelativePath $file.FullName
    $lineNumber = 0

    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++

        foreach ($entry in $disallowedPatterns) {
            if ($line -match $entry.Pattern) {
                $failures.Add("${relative}:${lineNumber}: $($entry.Reason)")
            }
        }
    }
}

foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
    if (Test-IsExcludedPath $file.FullName) {
        continue
    }

    if (Test-IsUnderFirstPartyRoot $file.FullName $firstPartySourceRoots) {
        $extension = [System.IO.Path]::GetExtension($file.Name).ToLowerInvariant()
        if ($forbiddenSourceExtensions -contains $extension) {
            $sourceFailures.Add((Get-RelativePath $file.FullName) + ": forbidden_source_language extension=" + $extension)
        }
    }
}

$solutionPath = Join-Path $root "OS.slnx"
if (Test-Path -LiteralPath $solutionPath) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($solutionPath)) {
        $lineNumber++
        if (Test-ContainsReferencePath $line) {
            $sourceFailures.Add("OS.slnx:${lineNumber}: reference_project_in_solution")
        }
    }
}

$projectFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Include *.csproj,*.vbproj,*.fsproj |
    Where-Object {
        -not (Test-IsExcludedPath $_.FullName) -and
        (Test-IsUnderFirstPartyRoot $_.FullName $firstPartyProjectRoots)
    }

foreach ($projectFile in $projectFiles) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($projectFile.FullName)) {
        $lineNumber++
        if (Test-ContainsReferencePath $line) {
            $sourceFailures.Add((Get-RelativePath $projectFile.FullName) + ":${lineNumber}: reference_project_in_first_party_project")
        }
    }
}

Write-Host "TEST_START naming.guard"
if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "TEST_FAIL naming.guard $failure"
    }
} else {
    Write-Host "TEST_PASS naming.guard"
}

Write-Host "TEST_START source.firewall"
if ($sourceFailures.Count -gt 0) {
    foreach ($failure in $sourceFailures) {
        Write-Host "TEST_FAIL source.firewall $failure"
    }
} else {
    Write-Host "TEST_PASS source.firewall"
}

if ($failures.Count -gt 0 -or $sourceFailures.Count -gt 0) {
    exit 1
}
