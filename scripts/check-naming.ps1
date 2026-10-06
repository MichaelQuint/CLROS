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
    @{
        Pattern = 'SharpOS\s+(project|workspace|core|kernel|release|completion|gate|gates)'
        Reason = "Use CLROS for first-party project naming; SharpOS is a reference only."
        AllowReferenceDiscussion = $true
    },
    @{ Pattern = 'first-party\s+SharpOS'; Reason = "Use first-party CLROS; SharpOS is a reference only." },
    @{ Pattern = '`src/Boot/BootSequence\.cs`'; Reason = "Use src/OS.Kernel/Boot/BootSequence.cs." },
    @{ Pattern = '`src/Kernel/Diagnostics/Probes\.cs`'; Reason = "Use src/OS.Kernel/Diagnostics/Probes.cs." }
)

$prefilterPattern = ($disallowedPatterns | ForEach-Object {
    '(?:' + $_.Pattern + ')'
}) -join '|'
$prefilter = [regex]::new($prefilterPattern,
    [System.Text.RegularExpressions.RegexOptions]::Compiled -bor
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

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

function Test-IsExplicitSharpOsReferenceDiscussion {
    param([string] $Value)

    return $Value -match '(?i)(\breference(d|s)?\b|\bsnapshots?\b|\bupstream\b|\bprovenance\b|\blicense\b|SharpOS-(main|master)|references[\\/].*SharpOS)'
}

$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
$directories = New-Object System.Collections.Generic.Stack[string]
$directories.Push($root)
while ($directories.Count -gt 0) {
    foreach ($item in Get-ChildItem -LiteralPath $directories.Pop()) {
        # Exclude before descent; the same names also remain excluded as files.
        if ($excludedDirectories -contains $item.Name) {
            continue
        }

        if ($item.PSIsContainer) {
            # Match Windows PowerShell's recursive provider: do not follow links.
            if (-not ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                $directories.Push($item.FullName)
            }
        }
        else {
            $files.Add($item)
        }
    }
}

$failures = New-Object System.Collections.Generic.List[string]
$sourceFailures = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    if ($file.FullName -eq $PSCommandPath) {
        continue
    }

    $relative = Get-RelativePath $file.FullName
    $lineNumber = 0

    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++

        if (-not $prefilter.IsMatch($line)) {
            continue
        }

        foreach ($entry in $disallowedPatterns) {
            $isAllowedReferenceDiscussion =
                $entry.ContainsKey("AllowReferenceDiscussion") -and
                $entry.AllowReferenceDiscussion -and
                (Test-IsExplicitSharpOsReferenceDiscussion $line)

            if ($line -match $entry.Pattern -and -not $isAllowedReferenceDiscussion) {
                $failures.Add("${relative}:${lineNumber}: $($entry.Reason)")
            }
        }
    }
}

foreach ($file in $files) {
    if (Test-IsUnderFirstPartyRoot $file.FullName $firstPartySourceRoots) {
        $extension = [System.IO.Path]::GetExtension($file.Name).ToLowerInvariant()
        if ($forbiddenSourceExtensions -contains $extension) {
            $sourceFailures.Add((Get-RelativePath $file.FullName) + ": forbidden_source_language extension=" + $extension)
        }
    }
}

$solutionFileName = "CLROS.slnx"
$solutionPath = Join-Path $root $solutionFileName
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    $sourceFailures.Add("${solutionFileName}: required_solution_missing")
}
else {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($solutionPath)) {
        $lineNumber++
        if (Test-ContainsReferencePath $line) {
            $sourceFailures.Add("${solutionFileName}:${lineNumber}: reference_project_in_solution")
        }
    }
}

$legacySolutionPath = Join-Path $root "OS.slnx"
if (Test-Path -LiteralPath $legacySolutionPath -PathType Leaf) {
    $sourceFailures.Add("OS.slnx: legacy_solution_sentinel_present")
}

$buildInputExtensions = @(
    ".csproj",
    ".vbproj",
    ".fsproj",
    ".props",
    ".targets"
)

$buildInputFiles = $files |
    Where-Object {
        ($buildInputExtensions -contains [System.IO.Path]::GetExtension($_.Name).ToLowerInvariant())
    }

foreach ($buildInputFile in $buildInputFiles) {
    $extension = [System.IO.Path]::GetExtension($buildInputFile.Name).ToLowerInvariant()
    $failureReason = if ($extension -eq ".props" -or $extension -eq ".targets") {
        "reference_import_in_first_party_build_input"
    }
    else {
        "reference_project_in_first_party_project"
    }

    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($buildInputFile.FullName)) {
        $lineNumber++
        if (Test-ContainsReferencePath $line) {
            $sourceFailures.Add(
                (Get-RelativePath $buildInputFile.FullName) + ":${lineNumber}: " + $failureReason)
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
