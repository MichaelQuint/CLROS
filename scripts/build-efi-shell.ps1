[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
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
    [switch] $EnableAcpiPhysicalDiscoveryProbe,
    [switch] $EnableM01LoadedImageStackProbe,
    [switch] $EnableM01LiveAllocatorProbe,
    [switch] $EnableM01OwnedPagingProbe
)

$ErrorActionPreference = "Stop"

$script:EfiPreflightLines = New-Object System.Collections.Generic.List[string]

$script:ExpectedEfiCompileItems = @(
    '..\OS.Kernel\Boot\BootHandoffRecord.cs|Boot\BootHandoffRecord.cs',
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs|Boot\BootMemoryRegionMaterializer.cs',
    '..\OS.Kernel\Boot\BootMemoryMapAcceptance.cs|Boot\BootMemoryMapAcceptance.cs',
    '..\OS.Kernel\Boot\EfiConfigurationTableScanner.cs|Boot\EfiConfigurationTableScanner.cs',
    '..\OS.Kernel\Boot\EfiBootHandoffValidator.cs|Boot\EfiBootHandoffValidator.cs',
    '..\OS.Kernel\Boot\EfiMemoryMapNormalizer.cs|Boot\EfiMemoryMapNormalizer.cs',
    '..\OS.Kernel\Boot\UefiExitBootServicesPlan.cs|Boot\UefiExitBootServicesPlan.cs',
    '..\OS.Kernel\Boot\UefiMemoryMapCapture.cs|Boot\UefiMemoryMapCapture.cs',
    '..\OS.Kernel\Boot\UefiMemoryMapCopyPlan.cs|Boot\UefiMemoryMapCopyPlan.cs',
    '..\OS.Kernel\Boot\BootSequencePlan.cs|Boot\BootSequencePlan.cs',
    '..\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs|Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs',
    '..\OS.Kernel\Memory\BootReservedRange.cs|Memory\BootReservedRange.cs',
    '..\OS.Kernel\Memory\BootReservationTableValidator.cs|Memory\BootReservationTableValidator.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRange.cs|Boot\EfiLoadedImageRange.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRangeCapturePlan.cs|Boot\EfiLoadedImageRangeCapturePlan.cs',
    '..\OS.Kernel\Boot\OwnedBootStackLayout.cs|Boot\OwnedBootStackLayout.cs',
    '..\OS.Kernel\Boot\UefiMemoryType.cs|Boot\UefiMemoryType.cs',
    '..\OS.Kernel\Boot\OwnedBootStackSwitchPlan.cs|Boot\OwnedBootStackSwitchPlan.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs|Memory\BootPageAllocatorCoreContracts.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorStateMaterializer.cs|Memory\BootPageAllocatorStateMaterializer.cs',
    '..\OS.Kernel\Memory\BootReservationSubtractor.cs|Memory\BootReservationSubtractor.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs|Memory\BootPageAllocatorMutation.cs',
    '..\OS.Kernel\Memory\PageProtection.cs|Memory\PageProtection.cs',
    '..\OS.Kernel\Memory\PageCachePolicy.cs|Memory\PageCachePolicy.cs',
    '..\OS.Kernel\Memory\X64PageTableEntry.cs|Memory\X64PageTableEntry.cs',
    '..\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs|Boot\X64BootIdentityMappingPlan.cs',
    '..\OS.Kernel\Memory\X64Cr3LoadStubPlan.cs|Memory\X64Cr3LoadStubPlan.cs',
    '..\OS.Kernel\Memory\X64PagingModePreflightStubPlan.cs|Memory\X64PagingModePreflightStubPlan.cs',
    '..\OS.Kernel\Memory\X64ReversibleCr3TransitionStubPlan.cs|Memory\X64ReversibleCr3TransitionStubPlan.cs',
    '..\OS.Kernel\Memory\X64PageMap.cs|Memory\X64PageMap.cs',
    '..\OS.Kernel\Memory\X64TlbInvalidationPlan.cs|Memory\X64TlbInvalidationPlan.cs',
    '..\OS.Kernel\Memory\X64InvlpgStubPlan.cs|Memory\X64InvlpgStubPlan.cs',
    '..\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs|Memory\X64ProtectionProbeStubPlan.cs',
    '..\OS.Kernel\Memory\PageZeroMappingPolicy.cs|Memory\PageZeroMappingPolicy.cs',
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs|Boot\M01LoadedImageStackIntegration.cs',
    '..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs|Boot\M01LiveAllocatorIntegration.cs',
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs|Boot\M01LivePagingIntegration.cs',
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs|Boot\M01OwnedCr3CompositeTransition.cs',
    'Boot\EfiConsoleAdapter.cs|',
    'Boot\EfiEntry.cs|',
    'Runtime\MinimalRuntime.cs|'
)

$script:ExpectedM01ReviewedSourceIncludes = @(
    '..\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs',
    '..\OS.Kernel\Memory\BootReservedRange.cs',
    '..\OS.Kernel\Memory\BootReservationTableValidator.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRange.cs',
    '..\OS.Kernel\Boot\EfiLoadedImageRangeCapturePlan.cs',
    '..\OS.Kernel\Boot\OwnedBootStackLayout.cs',
    '..\OS.Kernel\Boot\OwnedBootStackSwitchPlan.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorStateMaterializer.cs',
    '..\OS.Kernel\Memory\BootReservationSubtractor.cs',
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs',
    '..\OS.Kernel\Memory\PageProtection.cs',
    '..\OS.Kernel\Memory\PageCachePolicy.cs',
    '..\OS.Kernel\Memory\X64PageTableEntry.cs',
    '..\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs',
    '..\OS.Kernel\Memory\X64Cr3LoadStubPlan.cs',
    '..\OS.Kernel\Memory\X64PagingModePreflightStubPlan.cs',
    '..\OS.Kernel\Memory\X64ReversibleCr3TransitionStubPlan.cs',
    '..\OS.Kernel\Memory\X64PageMap.cs',
    '..\OS.Kernel\Memory\X64TlbInvalidationPlan.cs',
    '..\OS.Kernel\Memory\X64InvlpgStubPlan.cs',
    '..\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs',
    '..\OS.Kernel\Memory\PageZeroMappingPolicy.cs',
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs',
    '..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs',
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs',
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs',
    '..\OS.Kernel\Boot\BootSequencePlan.cs'
)

$script:ExpectedM01PrerequisiteSourceIncludes = @(
    '..\OS.Kernel\Boot\BootHandoffRecord.cs',
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs',
    '..\OS.Kernel\Boot\UefiMemoryType.cs'
)

$script:M01NostdlibExclusionCounts = @{
    '..\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs' = 1
    '..\OS.Kernel\Boot\EfiConfigurationTableScanner.cs' = 1
    '..\OS.Kernel\Memory\BootPageAllocatorMutation.cs' = 2
    '..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs' = 2
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs' = 1
}

$script:M01SourceContractTokens = @{
    '..\OS.Kernel\Boot\M01LivePagingIntegration.cs' = @(
        'public const ulong RequiredAllocationRecordCount = 3;',
        'public const ulong Cr3CodeAllocationRecordIndex = 0;',
        'public const ulong ProtectionProbeAllocationRecordIndex = 1;',
        'public const ulong TableStateAllocationRecordIndex = 2;',
        'public const ulong RequiredNonImageMappingRequirementCount = 10;',
        'M01LivePagingMapLedgerEvidence',
        'BootReservationKind.AllocatorArena',
        'BootReservationKind.PageTableArena',
        '_mapCaptureOrdinal > _lastAllocationOrdinal',
        '_ledgerBuildOrdinal > _mapCaptureOrdinal',
        'AreDisjoint(',
        'ProtectionMutationRejected',
        'ProtectionProbeRejected',
        'CallerAttestedPostAllocationOwnedMap',
        'public const bool CallsFirmwarePageAllocator = false;',
        'public const bool InvokesFirmware = false;',
        'public const bool InvokesFirmwareCallbacks = false;',
        'public const bool RestoresModeledProbeLeafToInitialProtection = true;'
    )
    '..\OS.Kernel\Boot\M01OwnedCr3CompositeTransition.cs' = @(
        'public const ulong RequiredRangeBindingCount = 7;',
        'public const bool CallsFirmware = false;',
        'public const bool DefinesSingleCompositeOwnedCr3Interval = true;',
        'public const bool InvokesFirmwareCallbacks = false;',
        'public const bool InvokesArbitraryCallbacks = false;',
        'public const bool ExactProbeIsSoleInternalTransfer = true;',
        'StateFirmwareCallbackCountOffset',
        'StateExpectedCanaryOffset',
        'StateReplacementCanaryOffset',
        'StateOriginalCr3RawOffset',
        'StateOriginalCr4RawOffset',
        'StateOriginalRflagsOffset',
        'StateFirmwareCr3ReadbackOffset',
        'StatePostRestoreCanaryOffset',
        'FirmwareCallbackForbidden',
        'FirmwareCallbackObserved',
        'RestoreCr3ThenCr4ThenRflags',
        'RestoreFirmwareCr3',
        'Cr4RestoreReadbackMismatch',
        'RflagsRestoreReadbackMismatch',
        'PostRestoreCanaryMismatch',
        'CompleteTableArena',
        'RuntimeStateAndCanary',
        'StateTableStateArenaStartOffset',
        'StateCanaryAddressOffset',
        'PostRestoreCanaryReadback'
    )
}

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

function New-M01SourceClosureResult {
    param(
        [bool] $Succeeded,
        [string] $Reason,
        [string] $Detail,
        [int] $CompileItemCount = 0,
        [int] $ReviewedSourceCount = 0,
        [int] $PrerequisiteSourceCount = 0
    )

    return [pscustomobject] @{
        Succeeded = $Succeeded
        Reason = $Reason
        Detail = $Detail
        CompileItemCount = $CompileItemCount
        ReviewedSourceCount = $ReviewedSourceCount
        PrerequisiteSourceCount = $PrerequisiteSourceCount
    }
}

function Get-NostdlibEfiActiveSource {
    param(
        [string] $SourceText,
        [string] $SourceName
    )

    $activeText = New-Object System.Text.StringBuilder
    $frames = New-Object System.Collections.ArrayList
    $currentActive = $true
    $lines = [System.Text.RegularExpressions.Regex]::Split(
        $SourceText,
        "\r\n|\n|\r")

    foreach ($line in $lines) {
        $ifMatch = [System.Text.RegularExpressions.Regex]::Match(
            $line,
            '^\s*#if\s+(.+?)\s*$')
        if ($ifMatch.Success) {
            $conditionText = $ifMatch.Groups[1].Value
            if (($conditionText -ne "!NOSTDLIB_EFI") -or
                ($frames.Count -ne 0)) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "ambiguous_preprocessor_condition"
                    Detail = $SourceName + ":" + $conditionText
                    ActiveText = ""
                }
            }

            [void] $frames.Add([pscustomobject] @{
                ParentActive = $currentActive
                ConditionActive = $false
            })
            $currentActive = $false
            continue
        }

        if ($line -match '^\s*#(?:elif|else)\b') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "ambiguous_preprocessor_condition"
                Detail = $SourceName + ":alternate-branch"
                ActiveText = ""
            }
            continue
        }

        if ($line -match '^\s*#endif\s*$') {
            if ($frames.Count -eq 0) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "unbalanced_preprocessor_condition"
                    Detail = $SourceName + ":#endif"
                    ActiveText = ""
                }
            }

            $frame = $frames[$frames.Count - 1]
            $frames.RemoveAt($frames.Count - 1)
            $currentActive = $frame.ParentActive
            continue
        }

        if ($line -match '^\s*#if\b') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "ambiguous_preprocessor_condition"
                Detail = $SourceName + ":malformed-if"
                ActiveText = ""
            }
        }

        if ($line -match '^\s*#') {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "unsupported_preprocessor_directive"
                Detail = $SourceName + ":" + $line.Trim()
                ActiveText = ""
            }
        }

        if ($currentActive) {
            [void] $activeText.AppendLine($line)
        }
    }

    if ($frames.Count -ne 0) {
        return [pscustomobject] @{
            Succeeded = $false
            Reason = "unbalanced_preprocessor_condition"
            Detail = $SourceName + ":missing-endif"
            ActiveText = ""
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
        ActiveText = $activeText.ToString()
    }
}

function Test-M01ActiveSourceSurface {
    param(
        [string] $SourceText,
        [string] $SourceName
    )

    $active = Get-NostdlibEfiActiveSource `
        -SourceText $SourceText `
        -SourceName $SourceName
    if (-not $active.Succeeded) {
        return $active
    }

    $withoutBlockComments = [System.Text.RegularExpressions.Regex]::Replace(
        $active.ActiveText,
        '/\*.*?\*/',
        "",
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $code = [System.Text.RegularExpressions.Regex]::Replace(
        $withoutBlockComments,
        '//.*$',
        "",
        [System.Text.RegularExpressions.RegexOptions]::Multiline)

    $forbiddenPatterns = [ordered] @{
        "unicode_identifier_escape" = '\\(?:u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})'
        "verbatim_identifier" = '@[A-Za-z_]'
        "span_api" = '\b(?:ReadOnly)?Span\s*<'
        "flags_attribute" = 'System\.FlagsAttribute|\[\s*(?:System\.)?Flags\s*\]'
        "system_api" = '\bSystem\s*\.'
        "system_namespace" = '\bnamespace\s+(?:global\s*::\s*)?System\b'
        "using_directive" = '\busing\b'
        "managed_string" = '\b(?:string|String)\b|"|\bnameof\s*\('
        "managed_object" = '\b(?:object|Object)\b'
        "managed_array" = '\[\s*(?:,\s*)*\]|\bnew\s+[A-Za-z_][A-Za-z0-9_.<>]*\s*\['
        "managed_root_reference" = '\b(?:Array|Type)\b'
        "managed_record_type" = '\brecord\s+(?:(?:class|struct)\s+)?[A-Za-z_][A-Za-z0-9_]*\s*(?:[<(:;{])'
        "generic_type_declaration" = '\b(?:class|struct|interface|record)\s+[A-Za-z_][A-Za-z0-9_]*\s*<'
        "generic_method_shape" = '\b[A-Za-z_][A-Za-z0-9_]*\s*<\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*,\s*)*[A-Za-z_][A-Za-z0-9_]*\s*>\s*\('
        "generic_constraint" = '\bwhere\s+[A-Za-z_][A-Za-z0-9_]*\s*:'
        "managed_anonymous_object" = '\bnew\s*\{'
        "managed_type_shape" = '\b(?:interface|event|dynamic)\b'
        "source_attribute" = '(?m)(?:^|[;{}(,])\s*\[\s*[A-Za-z_]'
        "exception_control_flow" = '\b(?:Exception|throw|try|catch|finally)\b'
        "reflection_api" = '\b(?:typeof|GetType|Activator)\b|System\.Reflection'
        "unavailable_bcl_constant" = '\b[A-Za-z_][A-Za-z0-9_.<>]*\s*\.\s*(?:MaxValue|MinValue|Empty)\b'
        "managed_delegate" = '\bdelegate\b(?!\s*\*)|\b(?:Func|Action|Delegate|MulticastDelegate)\b'
        "managed_runtime_api" = '\b(?:Console|Task|Thread|GC|Marshal)\b'
    }

    foreach ($entry in $forbiddenPatterns.GetEnumerator()) {
        if ([System.Text.RegularExpressions.Regex]::IsMatch(
                $code,
                $entry.Value,
                [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":" + $entry.Key
                ActiveText = ""
            }
        }
    }

    $classMatches = [System.Text.RegularExpressions.Regex]::Matches(
        $code,
        '(?<![@A-Za-z0-9_])class\b',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    foreach ($classMatch in $classMatches) {
        $prefixStart = -1
        if ($classMatch.Index -gt 0) {
            foreach ($delimiter in @("`r", "`n", ";", "{", "}")) {
                $candidate = $code.LastIndexOf(
                    $delimiter,
                    $classMatch.Index - 1,
                    [System.StringComparison]::Ordinal)
                if ($candidate -gt $prefixStart) {
                    $prefixStart = $candidate
                }
            }
        }

        $prefix = $code.Substring(
            $prefixStart + 1,
            $classMatch.Index - $prefixStart - 1).Trim()
        $modifierTokens = @(
            $prefix.Split(
                [char[]] " `t`r`n",
                [System.StringSplitOptions]::RemoveEmptyEntries)
        )
        $allowedClassModifiers = @(
            "public",
            "internal",
            "private",
            "protected",
            "file",
            "new",
            "unsafe",
            "sealed",
            "abstract",
            "partial",
            "static"
        )
        $hasStaticModifier = $false
        $invalidModifier = $false
        foreach ($modifier in $modifierTokens) {
            if ($modifier -eq "static") {
                $hasStaticModifier = $true
            }
            elseif ($allowedClassModifiers -notcontains $modifier) {
                $invalidModifier = $true
            }
        }
        if (-not $hasStaticModifier -or $invalidModifier) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "forbidden_active_source"
                Detail = $SourceName + ":non_static_class"
                ActiveText = ""
            }
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
        ActiveText = $code
    }
}

function Get-M01CanonicalXmlSha256 {
    param([System.Xml.XmlDocument] $Document)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Document.OuterXml)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString(
                $sha256.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Test-M01BuildCustomizationSurface {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    $repositoryFullPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $cursor = $projectDirectory
    while (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($cursor),
            $repositoryFullPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        foreach ($fileName in @("Directory.Build.props", "Directory.Build.targets")) {
            $unexpectedPath = Join-Path $cursor $fileName
            if (Test-Path -LiteralPath $unexpectedPath -PathType Leaf) {
                return [pscustomobject] @{
                    Succeeded = $false
                    Reason = "unexpected_build_customization"
                    Detail = $unexpectedPath
                }
            }
        }

        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals(
                $parent,
                $cursor,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "repository_root_unreachable"
                Detail = $cursor
            }
        }
        $cursor = $parent
    }

    foreach ($fileName in @("Directory.Build.props", "Directory.Build.targets")) {
        $customizationPath = Join-Path $repositoryFullPath $fileName
        if (-not (Test-Path -LiteralPath $customizationPath -PathType Leaf)) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_missing"
                Detail = $customizationPath
            }
        }

        try {
            [xml] $customization =
                Get-Content -LiteralPath $customizationPath -Raw
        }
        catch {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_malformed"
                Detail = $customizationPath
            }
        }

        $expectedCanonicalHash = if ($fileName -eq "Directory.Build.props") {
            "0743a5f401f40744dc0a8c8a4d2d8467d9716a7040df32865974d1f6693115fa"
        }
        else {
            "de2b50f646cebd9b29f23f2a74b2ad5ff9d56f80680d6cf5ba826e40dfc959cf"
        }
        $canonicalHash = Get-M01CanonicalXmlSha256 -Document $customization
        if ($canonicalHash -ne $expectedCanonicalHash) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_shape_invalid"
                Detail = $customizationPath
            }
        }

        $forbiddenNodes = @($customization.SelectNodes(
            "//Compile|//ProjectReference|//Reference|//PackageReference|" +
            "//Analyzer|//AdditionalFiles|//UsingTask|//Import|//Choose|" +
            "//Csc"))
        $compileOutputs = @($customization.SelectNodes("//Output"))
        $nonValidationTargetChildren = @($customization.SelectNodes("//Target/*") |
            Where-Object { $_.Name -ne "Error" })
        if ($forbiddenNodes.Count -ne 0 -or
            $compileOutputs.Count -ne 0 -or
            $nonValidationTargetChildren.Count -ne 0) {
            return [pscustomobject] @{
                Succeeded = $false
                Reason = "build_customization_source_injection"
                Detail = $customizationPath
            }
        }
    }

    return [pscustomobject] @{
        Succeeded = $true
        Reason = "ok"
        Detail = ""
    }
}

function Test-M01EvaluatedSourceClosure {
    param(
        [string] $ProjectPath,
        [string] $Configuration,
        [bool] $LoadedImageStackEnabled,
        [bool] $LiveAllocatorEnabled,
        [bool] $OwnedPagingEnabled,
        [bool] $UefiGoneSimulationEnabled,
        [bool] $ExitBootServicesFreshMapEnabled,
        [bool] $AcpiPhysicalDiscoveryEnabled
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    $expectedSwitchValues = [ordered] @{
        "EnableM01LoadedImageStackProbe" =
            $LoadedImageStackEnabled.ToString().ToLowerInvariant()
        "EnableM01LiveAllocatorProbe" =
            $LiveAllocatorEnabled.ToString().ToLowerInvariant()
        "EnableM01OwnedPagingProbe" =
            $OwnedPagingEnabled.ToString().ToLowerInvariant()
    }
    $expectedSwitchSymbols = [ordered] @{
        "M01_LOADED_IMAGE_STACK_PROBE" = $LoadedImageStackEnabled
        "M01_LIVE_ALLOCATOR_PROBE" = $LiveAllocatorEnabled
        "M01_OWNED_PAGING_PROBE" = $OwnedPagingEnabled
        "UEFI_GONE_SIMULATION_PROBE" = $UefiGoneSimulationEnabled
        "EXIT_BOOT_SERVICES_FRESH_MAP_PROBE" =
            $ExitBootServicesFreshMapEnabled
        "ACPI_PHYSICAL_DISCOVERY_PROBE" = $AcpiPhysicalDiscoveryEnabled
    }

    foreach ($profile in @("SingleMinimalApi", "MultiMinimalApi")) {
        $arguments = @(
            "msbuild",
            $ProjectPath,
            "-nologo",
            "-getItem:Compile,ProjectReference,Reference,Analyzer",
            (
                "-getProperty:" +
                "Configuration," +
                "EnableM01LoadedImageStackProbe," +
                "EnableM01LiveAllocatorProbe," +
                "EnableM01OwnedPagingProbe," +
                "DefineConstants," +
                "EnableDefaultCompileItems," +
                "NoStdLib," +
                "DisableImplicitFrameworkReferences"
            ),
            ("-p:Configuration=" + $Configuration),
            ("-p:OSWorkloadProfile=" + $profile),
            "-p:RestoreIgnoreFailedSources=true"
        )
        if ($LoadedImageStackEnabled) {
            $arguments += "-p:EnableM01LoadedImageStackProbe=true"
        }
        if ($LiveAllocatorEnabled) {
            $arguments += "-p:EnableM01LiveAllocatorProbe=true"
        }
        if ($OwnedPagingEnabled) {
            $arguments += "-p:EnableM01OwnedPagingProbe=true"
        }
        if ($UefiGoneSimulationEnabled) {
            $arguments += "-p:EnableUefiGoneSimulationProbe=true"
        }
        if ($ExitBootServicesFreshMapEnabled) {
            $arguments += "-p:EnableExitBootServicesFreshMapProbe=true"
        }
        if ($AcpiPhysicalDiscoveryEnabled) {
            $arguments += "-p:EnableAcpiPhysicalDiscoveryProbe=true"
        }

        $outputLines = @(& dotnet @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        $outputText = $outputLines | Out-String
        if ($exitCode -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_project_failed" `
                -Detail $profile
        }

        $jsonStart = $outputText.IndexOf(
            "{",
            [System.StringComparison]::Ordinal)
        $jsonEnd = $outputText.LastIndexOf(
            "}",
            [System.StringComparison]::Ordinal)
        if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_project_malformed" `
                -Detail ($profile + ":json")
        }

        try {
            $evaluation = $outputText.Substring(
                $jsonStart,
                $jsonEnd - $jsonStart + 1) | ConvertFrom-Json
        }
        catch {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_project_malformed" `
                -Detail ($profile + ":parse")
        }

        if ($evaluation.Properties.Configuration -ne $Configuration -or
            $evaluation.Properties.EnableDefaultCompileItems -ne "false" -or
            $evaluation.Properties.NoStdLib -ne "true" -or
            $evaluation.Properties.DisableImplicitFrameworkReferences -ne "true") {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_property_mismatch" `
                -Detail ($profile + ":nostdlib")
        }
        foreach ($entry in $expectedSwitchValues.GetEnumerator()) {
            if ($evaluation.Properties.($entry.Key) -ne $entry.Value) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_property_mismatch" `
                    -Detail ($profile + ":" + $entry.Key)
            }
        }

        $symbols = @(
            $evaluation.Properties.DefineConstants.Split(";") |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                ForEach-Object { $_.Trim() }
        )
        $expectedProfileSymbol = if ($profile -eq "SingleMinimalApi") {
            "OS_PROFILE_SINGLE_MINIMAL_API"
        }
        else {
            "OS_PROFILE_MULTI_MINIMAL_API"
        }
        $unexpectedProfileSymbol = if ($profile -eq "SingleMinimalApi") {
            "OS_PROFILE_MULTI_MINIMAL_API"
        }
        else {
            "OS_PROFILE_SINGLE_MINIMAL_API"
        }
        if ($symbols -notcontains "NOSTDLIB_EFI" -or
            $symbols -contains "NOSTDLIB" -or
            $symbols -notcontains $expectedProfileSymbol -or
            $symbols -contains $unexpectedProfileSymbol) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_define_constants_mismatch" `
                -Detail $profile
        }
        foreach ($entry in $expectedSwitchSymbols.GetEnumerator()) {
            $isPresent = $symbols -contains $entry.Key
            if ($isPresent -ne [bool] $entry.Value) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_define_constants_mismatch" `
                    -Detail ($profile + ":" + $entry.Key)
            }
        }
        $expectedSymbols = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($symbol in @(
                "TRACE",
                "NOSTDLIB_EFI",
                $expectedProfileSymbol)) {
            [void] $expectedSymbols.Add($symbol)
        }
        [void] $expectedSymbols.Add(
            $(if ($Configuration -eq "Debug") { "DEBUG" } else { "RELEASE" }))
        foreach ($entry in $expectedSwitchSymbols.GetEnumerator()) {
            if ([bool] $entry.Value) {
                [void] $expectedSymbols.Add([string] $entry.Key)
            }
        }
        $actualSymbols = New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal)
        foreach ($symbol in $symbols) {
            [void] $actualSymbols.Add($symbol)
        }
        if ($actualSymbols.Count -ne $expectedSymbols.Count) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_define_constants_mismatch" `
                -Detail ($profile + ":count=" + $actualSymbols.Count)
        }
        foreach ($symbol in $expectedSymbols) {
            if (-not $actualSymbols.Contains($symbol)) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_define_constants_mismatch" `
                    -Detail ($profile + ":missing=" + $symbol)
            }
        }

        $compileItems = @($evaluation.Items.Compile)
        $projectReferences = @($evaluation.Items.ProjectReference)
        $references = @($evaluation.Items.Reference)
        $analyzers = @($evaluation.Items.Analyzer)
        if ($compileItems.Count -ne $script:ExpectedEfiCompileItems.Count -or
            $projectReferences.Count -ne 0 -or
            $references.Count -ne 0 -or
            $analyzers.Count -ne 2) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_item_count_mismatch" `
                -Detail (
                    $profile +
                    ":compile=" + $compileItems.Count +
                    ";project=" + $projectReferences.Count +
                    ";reference=" + $references.Count +
                    ";analyzer=" + $analyzers.Count)
        }

        $expectedAnalyzerNames = @(
            "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
            "Microsoft.CodeAnalysis.NetAnalyzers.dll"
        )
        $actualAnalyzerNames = New-Object `
            'System.Collections.Generic.HashSet[string]' (
                [System.StringComparer]::Ordinal)
        foreach ($analyzer in $analyzers) {
            $analyzerName = [System.IO.Path]::GetFileName(
                [string] $analyzer.FullPath)
            $definingProjectName = [System.IO.Path]::GetFileName(
                [string] $analyzer.DefiningProjectFullPath)
            if ($expectedAnalyzerNames -notcontains $analyzerName -or
                $definingProjectName -ne "Microsoft.NET.Sdk.Analyzers.targets") {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_analyzer_mismatch" `
                    -Detail ($profile + ":" + $analyzerName)
            }
            [void] $actualAnalyzerNames.Add($analyzerName)
        }
        if ($actualAnalyzerNames.Count -ne $expectedAnalyzerNames.Count) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "evaluated_analyzer_mismatch" `
                -Detail ($profile + ":duplicate")
        }

        for ($index = 0; $index -lt $compileItems.Count; $index++) {
            $item = $compileItems[$index]
            $link = if ($null -eq $item.Link) { "" } else { [string] $item.Link }
            $actualKey = [string] $item.Identity + "|" + $link
            $expectedKey = $script:ExpectedEfiCompileItems[$index]
            $expectedInclude = $expectedKey.Substring(
                0,
                $expectedKey.IndexOf(
                    "|",
                    [System.StringComparison]::Ordinal))
            $expectedFullPath = [System.IO.Path]::GetFullPath(
                (Join-Path $projectDirectory $expectedInclude))
            if (-not [string]::Equals(
                    $actualKey,
                    $expectedKey,
                    [System.StringComparison]::Ordinal) -or
                -not [string]::Equals(
                    [string] $item.FullPath,
                    $expectedFullPath,
                    [System.StringComparison]::OrdinalIgnoreCase) -or
                -not [string]::Equals(
                    [string] $item.DefiningProjectFullPath,
                    [System.IO.Path]::GetFullPath($ProjectPath),
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "evaluated_compile_item_mismatch" `
                    -Detail ($profile + ":" + $index + ":" + $actualKey)
            }
        }
    }

    return New-M01SourceClosureResult `
        -Succeeded $true `
        -Reason "ok" `
        -Detail "" `
        -CompileItemCount $script:ExpectedEfiCompileItems.Count
}

function Test-M01SourceClosure {
    param(
        [string] $RepositoryRoot,
        [string] $ProjectPath,
        [string] $Configuration,
        [bool] $LoadedImageStackEnabled,
        [bool] $LiveAllocatorEnabled,
        [bool] $OwnedPagingEnabled,
        [bool] $UefiGoneSimulationEnabled,
        [bool] $ExitBootServicesFreshMapEnabled,
        [bool] $AcpiPhysicalDiscoveryEnabled
    )

    $canonicalProjectPath = [System.IO.Path]::GetFullPath(
        (Join-Path $RepositoryRoot "src\OS\OS.csproj"))
    $evaluatedProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    if (-not [string]::Equals(
            $canonicalProjectPath,
            $evaluatedProjectPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project" `
            -Detail $evaluatedProjectPath
    }

    if (-not (Test-Path -LiteralPath $canonicalProjectPath -PathType Leaf)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "project_missing" `
            -Detail $canonicalProjectPath
    }

    try {
        $projectText = Get-Content -LiteralPath $canonicalProjectPath -Raw
        [xml] $project = $projectText
    }
    catch {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "project_malformed" `
            -Detail $canonicalProjectPath
    }

    $canonicalProjectHash = Get-M01CanonicalXmlSha256 -Document $project
    if ($canonicalProjectHash -ne
        "bb9f1dc5fab00848c7682478b5651b833fa9a66f3c7ee03a9e3a8b425eb865e3") {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project_shape" `
            -Detail $canonicalProjectHash
    }

    if ($project.Project.Sdk -ne "Microsoft.NET.Sdk") {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "noncanonical_project" `
            -Detail "sdk"
    }

    $requiredSingleProperties = [ordered] @{
        "AssemblyName" = "OS"
        "RootNamespace" = "OS"
        "TargetFramework" = "net10.0"
        "LangVersion" = "14.0"
        "RuntimeIdentifier" = "win-x64"
        "EnableDefaultCompileItems" = "false"
        "NoStdLib" = "true"
        "DisableImplicitFrameworkReferences" = "true"
    }
    foreach ($entry in $requiredSingleProperties.GetEnumerator()) {
        $nodes = @($project.SelectNodes("/Project/PropertyGroup/" + $entry.Key))
        if ($nodes.Count -ne 1 -or
            $nodes[0].InnerText -ne $entry.Value -or
            $nodes[0].Attributes.Count -ne 0 -or
            $nodes[0].SelectNodes("*").Count -ne 0 -or
            $nodes[0].ParentNode.Name -ne "PropertyGroup" -or
            $nodes[0].ParentNode.Attributes.Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "noncanonical_project" `
                -Detail ("property:" + $entry.Key)
        }
    }

    $baseDefineValue = '$' + '(DefineConstants);NOSTDLIB_EFI'
    $defineNodes = @($project.SelectNodes("//DefineConstants"))
    $baseDefineNodes = @($defineNodes | Where-Object {
        -not $_.ParentNode.HasAttribute("Condition")
    })
    if ($baseDefineNodes.Count -ne 1 -or
        $baseDefineNodes[0].HasAttribute("Condition") -or
        $baseDefineNodes[0].InnerText -ne $baseDefineValue) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "nostdlib_efi_symbol_missing" `
            -Detail "NOSTDLIB_EFI"
    }

    $allowedConditionalSymbols = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "M01_LOADED_IMAGE_STACK_PROBE"
        "EnableM01LiveAllocatorProbe" = "M01_LIVE_ALLOCATOR_PROBE"
        "EnableM01OwnedPagingProbe" = "M01_OWNED_PAGING_PROBE"
        "EnableUefiGoneSimulationProbe" = "UEFI_GONE_SIMULATION_PROBE"
        "EnableExitBootServicesFreshMapProbe" = "EXIT_BOOT_SERVICES_FRESH_MAP_PROBE"
        "EnableAcpiPhysicalDiscoveryProbe" = "ACPI_PHYSICAL_DISCOVERY_PROBE"
    }
    if ($defineNodes.Count -ne (1 + $allowedConditionalSymbols.Count)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "define_constants_shape_invalid" `
            -Detail $defineNodes.Count
    }

    foreach ($entry in $allowedConditionalSymbols.GetEnumerator()) {
        $condition = "'" + '$' + '(' + $entry.Key + ")' == 'true'"
        $groups = @($project.SelectNodes("/Project/PropertyGroup") |
            Where-Object { $_.GetAttribute("Condition") -eq $condition })
        if ($groups.Count -ne 1 -or
            $groups[0].SelectNodes("*").Count -ne 1) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_symbol_invalid" `
                -Detail $entry.Key
        }

        $defines = @($groups[0].SelectNodes("DefineConstants"))
        $expectedDefineConstants =
            '$' + '(DefineConstants);' + $entry.Value
        if ($defines.Count -ne 1 -or
            $defines[0].HasAttribute("Condition") -or
            $defines[0].InnerText -ne $expectedDefineConstants) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_symbol_invalid" `
                -Detail $entry.Key
        }
    }

    $switchSymbols = [ordered] @{
        "EnableM01LoadedImageStackProbe" = "M01_LOADED_IMAGE_STACK_PROBE"
        "EnableM01LiveAllocatorProbe" = "M01_LIVE_ALLOCATOR_PROBE"
        "EnableM01OwnedPagingProbe" = "M01_OWNED_PAGING_PROBE"
    }
    foreach ($entry in $switchSymbols.GetEnumerator()) {
        $defaultNodes = @($project.SelectNodes(
            "/Project/PropertyGroup/" + $entry.Key))
        if ($defaultNodes.Count -ne 1) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_default_invalid" `
                -Detail ($entry.Key + ":count")
        }

        $expectedDefaultCondition =
            "'" + '$' + '(' + $entry.Key + ")' == ''"
        $actualDefaultCondition =
            $defaultNodes[0].GetAttribute("Condition")
        if (($defaultNodes[0].InnerText -ne "false") -or
            ($actualDefaultCondition -ne $expectedDefaultCondition)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_default_invalid" `
                -Detail $entry.Key
        }

    }

    $validationTargets = @($project.SelectNodes(
        "/Project/Target[@Name='ValidateM01ProbeSwitches']"))
    if ($validationTargets.Count -ne 1 -or
        $validationTargets[0].GetAttribute("BeforeTargets") -ne "BeforeCompile" -or
        $validationTargets[0].HasAttribute("Condition") -or
        $validationTargets[0].Attributes.Count -ne 2) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_dependency_validation_missing" `
            -Detail "ValidateM01ProbeSwitches"
    }
    $errorNodes = @($validationTargets[0].SelectNodes("Error"))
    $errorConditions = @($errorNodes |
        ForEach-Object { $_.GetAttribute("Condition") })
    $allocatorDependencyCondition =
        "'" + '$' + "(EnableM01LiveAllocatorProbe)' == 'true' and '" +
        '$' + "(EnableM01LoadedImageStackProbe)' != 'true'"
    $pagingDependencyCondition =
        "'" + '$' + "(EnableM01OwnedPagingProbe)' == 'true' and '" +
        '$' + "(EnableM01LiveAllocatorProbe)' != 'true'"
    $requiredErrorConditions = @(
        $allocatorDependencyCondition,
        $pagingDependencyCondition
    )
    if ($errorConditions.Count -ne $requiredErrorConditions.Count -or
        $validationTargets[0].SelectNodes("*").Count -ne $errorNodes.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "switch_dependency_validation_missing" `
            -Detail "count"
    }
    foreach ($condition in $requiredErrorConditions) {
        if ($errorConditions -notcontains $condition) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_dependency_validation_missing" `
                -Detail $condition
        }
    }
    foreach ($errorNode in $errorNodes) {
        if ($errorNode.Attributes.Count -ne 2 -or
            -not $errorNode.HasAttribute("Condition") -or
            -not $errorNode.HasAttribute("Text") -or
            $errorNode.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "switch_dependency_validation_missing" `
                -Detail "error-shape"
        }
    }

    $sourceInjectionNodes = @($project.SelectNodes(
        "//UsingTask|//Analyzer|//AdditionalFiles|//Csc|//Output"))
    if ($sourceInjectionNodes.Count -ne 0) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_project_input" `
            -Detail "task-or-analyzer"
    }

    $packageReferences = @($project.SelectNodes(
        "/Project/ItemGroup/PackageReference"))
    $expectedPackages = [ordered] @{
        "Microsoft.DotNet.ILCompiler" = "10.0.8"
        "runtime.win-x64.Microsoft.DotNet.ILCompiler" = "10.0.8"
    }
    if ($packageReferences.Count -ne $expectedPackages.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "package_reference_shape_invalid" `
            -Detail $packageReferences.Count
    }
    $seenPackageIds = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::Ordinal)
    foreach ($packageReference in $packageReferences) {
        $packageId = $packageReference.GetAttribute("Include")
        if (-not $expectedPackages.Contains($packageId) -or
            -not $seenPackageIds.Add($packageId) -or
            $packageReference.GetAttribute("Version") -ne
                $expectedPackages[$packageId] -or
            $packageReference.GetAttribute("PrivateAssets") -ne "all" -or
            $packageReference.Attributes.Count -ne 3 -or
            $packageReference.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "package_reference_shape_invalid" `
                -Detail $packageId
        }
    }

    if (($project.SelectNodes("//ProjectReference").Count -ne 0) -or
        ($project.SelectNodes("//Import").Count -ne 0) -or
        ($project.SelectNodes("//ImportGroup").Count -ne 0) -or
        ($project.SelectNodes("//Choose").Count -ne 0)) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_project_input" `
            -Detail "reference-or-import"
    }

    $compileItems = @($project.SelectNodes("/Project/ItemGroup/Compile"))
    $allCompileItems = @($project.SelectNodes("//Compile"))
    if ($allCompileItems.Count -ne $compileItems.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "forbidden_generated_compile_item" `
            -Detail ($allCompileItems.Count.ToString()) `
            -CompileItemCount $compileItems.Count
    }

    if ($compileItems.Count -ne $script:ExpectedEfiCompileItems.Count) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason "compile_item_count_mismatch" `
            -Detail ($compileItems.Count.ToString()) `
            -CompileItemCount $compileItems.Count
    }

    $seenItems = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $seenIncludes = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $seenLinks = New-Object 'System.Collections.Generic.HashSet[string]' (
        [System.StringComparer]::OrdinalIgnoreCase)
    $projectDirectory = Split-Path -Parent $canonicalProjectPath
    $repositoryPrefix = [System.IO.Path]::GetFullPath($RepositoryRoot)
    if (-not $repositoryPrefix.EndsWith(
            [System.IO.Path]::DirectorySeparatorChar.ToString(),
            [System.StringComparison]::Ordinal)) {
        $repositoryPrefix += [System.IO.Path]::DirectorySeparatorChar
    }
    $sourceTextByInclude = @{}
    $activeCodeByInclude = @{}

    for ($index = 0; $index -lt $compileItems.Count; $index++) {
        $compile = $compileItems[$index]
        $include = $compile.GetAttribute("Include")
        $link = $compile.GetAttribute("Link")
        $actualKey = $include + "|" + $link
        $expectedKey = $script:ExpectedEfiCompileItems[$index]
        $expectedHasLink = -not $expectedKey.EndsWith(
            "|",
            [System.StringComparison]::Ordinal)
        $expectedAttributeCount = if ($expectedHasLink) { 2 } else { 1 }
        if ($compile.Attributes.Count -ne $expectedAttributeCount -or
            -not $compile.HasAttribute("Include") -or
            ($expectedHasLink -and -not $compile.HasAttribute("Link")) -or
            (-not $expectedHasLink -and $compile.HasAttribute("Link")) -or
            $compile.SelectNodes("*").Count -ne 0) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "compile_item_shape_invalid" `
                -Detail $actualKey `
                -CompileItemCount $compileItems.Count
        }

        $hasExistsCondition = $projectText.IndexOf(
                "Exists(",
                [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        if (($include -match '[*?\[\];]') -or
            ($include -match '\$\(') -or
            ($include -match '@\(') -or
            $compile.HasAttribute("Remove") -or
            $compile.HasAttribute("Update") -or
            $compile.HasAttribute("Condition") -or
            $compile.ParentNode.HasAttribute("Condition") -or
            $hasExistsCondition) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "conditional_or_glob_compile_item" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }

        if (-not $seenItems.Add($actualKey)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_item" `
                -Detail $actualKey `
                -CompileItemCount $compileItems.Count
        }
        if (-not $seenIncludes.Add($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_include" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
        if (-not [string]::IsNullOrWhiteSpace($link) -and
            -not $seenLinks.Add($link)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "duplicate_compile_link" `
                -Detail $link `
                -CompileItemCount $compileItems.Count
        }

        if (-not [string]::Equals(
                $actualKey,
                $expectedKey,
                [System.StringComparison]::Ordinal)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "compile_item_mismatch" `
                -Detail ($index.ToString() + ":" + $actualKey) `
                -CompileItemCount $compileItems.Count
        }

        $sourcePath = [System.IO.Path]::GetFullPath(
            (Join-Path $projectDirectory $include))
        if (-not $sourcePath.StartsWith(
                $repositoryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_outside_repository" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }

        if ($include.StartsWith(
                "..\OS.Kernel\",
                [System.StringComparison]::Ordinal)) {
            $sourceText = Get-Content -LiteralPath $sourcePath -Raw
            $sourceTextByInclude[$include] = $sourceText
            $surface = Test-M01ActiveSourceSurface `
                -SourceText $sourceText `
                -SourceName $include
            if (-not $surface.Succeeded) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason $surface.Reason `
                    -Detail $surface.Detail `
                    -CompileItemCount $compileItems.Count
            }
            $activeCodeByInclude[$include] = $surface.ActiveText
        }
    }

    foreach ($entry in $script:M01NostdlibExclusionCounts.GetEnumerator()) {
        if (-not $sourceTextByInclude.ContainsKey($entry.Key)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $entry.Key `
                -CompileItemCount $compileItems.Count
        }

        $guardCount = [System.Text.RegularExpressions.Regex]::Matches(
            $sourceTextByInclude[$entry.Key],
            '(?m)^\s*#if\s+!NOSTDLIB_EFI\s*$').Count
        if ($guardCount -ne [int] $entry.Value) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "nostdlib_efi_exclusion_mismatch" `
                -Detail ($entry.Key + ":" + $guardCount) `
                -CompileItemCount $compileItems.Count
        }
    }

    foreach ($entry in $script:M01SourceContractTokens.GetEnumerator()) {
        if (-not $activeCodeByInclude.ContainsKey($entry.Key)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "source_missing" `
                -Detail $entry.Key `
                -CompileItemCount $compileItems.Count
        }

        foreach ($token in $entry.Value) {
            if ($activeCodeByInclude[$entry.Key].IndexOf(
                    $token,
                    [System.StringComparison]::Ordinal) -lt 0) {
                return New-M01SourceClosureResult `
                    -Succeeded $false `
                    -Reason "source_contract_missing" `
                    -Detail ($entry.Key + ":" + $token) `
                    -CompileItemCount $compileItems.Count
            }
        }
    }

    foreach ($include in $script:ExpectedM01ReviewedSourceIncludes) {
        if (-not $activeCodeByInclude.ContainsKey($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "reviewed_source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
    }

    foreach ($include in $script:ExpectedM01PrerequisiteSourceIncludes) {
        if (-not $activeCodeByInclude.ContainsKey($include)) {
            return New-M01SourceClosureResult `
                -Succeeded $false `
                -Reason "prerequisite_source_missing" `
                -Detail $include `
                -CompileItemCount $compileItems.Count
        }
    }

    $customizationSurface = Test-M01BuildCustomizationSurface `
        -RepositoryRoot $RepositoryRoot `
        -ProjectPath $canonicalProjectPath
    if (-not $customizationSurface.Succeeded) {
        return New-M01SourceClosureResult `
            -Succeeded $false `
            -Reason $customizationSurface.Reason `
            -Detail $customizationSurface.Detail `
            -CompileItemCount $compileItems.Count
    }

    $evaluatedClosure = Test-M01EvaluatedSourceClosure `
        -ProjectPath $canonicalProjectPath `
        -Configuration $Configuration `
        -LoadedImageStackEnabled $LoadedImageStackEnabled `
        -LiveAllocatorEnabled $LiveAllocatorEnabled `
        -OwnedPagingEnabled $OwnedPagingEnabled `
        -UefiGoneSimulationEnabled $UefiGoneSimulationEnabled `
        -ExitBootServicesFreshMapEnabled $ExitBootServicesFreshMapEnabled `
        -AcpiPhysicalDiscoveryEnabled $AcpiPhysicalDiscoveryEnabled
    if (-not $evaluatedClosure.Succeeded) {
        return $evaluatedClosure
    }

    return New-M01SourceClosureResult `
        -Succeeded $true `
        -Reason "ok" `
        -Detail "" `
        -CompileItemCount $compileItems.Count `
        -ReviewedSourceCount $script:ExpectedM01ReviewedSourceIncludes.Count `
        -PrerequisiteSourceCount $script:ExpectedM01PrerequisiteSourceIncludes.Count
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

$loadedImageStackProbeEnabled =
    $EnableM01LoadedImageStackProbe.IsPresent.ToString().ToLowerInvariant()
$liveAllocatorProbeEnabled =
    $EnableM01LiveAllocatorProbe.IsPresent.ToString().ToLowerInvariant()
$ownedPagingProbeEnabled =
    $EnableM01OwnedPagingProbe.IsPresent.ToString().ToLowerInvariant()
$m01SwitchEvidence =
    "loadedImageStack=" + $loadedImageStackProbeEnabled +
    " liveAllocator=" + $liveAllocatorProbeEnabled +
    " ownedPaging=" + $ownedPagingProbeEnabled

if ($EnableM01LiveAllocatorProbe -and -not $EnableM01LoadedImageStackProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SWITCHES " + $m01SwitchEvidence +
            " reason=live_allocator_requires_loaded_image_stack") `
        -Reason "m01_live_allocator_requires_loaded_image_stack" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

if ($EnableM01OwnedPagingProbe -and -not $EnableM01LiveAllocatorProbe) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SWITCHES " + $m01SwitchEvidence +
            " reason=owned_paging_requires_live_allocator") `
        -Reason "m01_owned_paging_requires_live_allocator" `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M01_SWITCHES status=ok " + $m01SwitchEvidence)

$m01SourceClosure = Test-M01SourceClosure `
    -RepositoryRoot $repositoryRoot `
    -ProjectPath $efiProjectPath `
    -Configuration $Configuration `
    -LoadedImageStackEnabled $EnableM01LoadedImageStackProbe.IsPresent `
    -LiveAllocatorEnabled $EnableM01LiveAllocatorProbe.IsPresent `
    -OwnedPagingEnabled $EnableM01OwnedPagingProbe.IsPresent `
    -UefiGoneSimulationEnabled $EnableUefiGoneSimulationProbe.IsPresent `
    -ExitBootServicesFreshMapEnabled $EnableExitBootServicesFreshMapProbe.IsPresent `
    -AcpiPhysicalDiscoveryEnabled $EnableAcpiPhysicalDiscoveryProbe.IsPresent
if (-not $m01SourceClosure.Succeeded) {
    Stop-EfiPreflight `
        -Marker (
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE" +
            " reason=" + $m01SourceClosure.Reason +
            " detail=" + (Format-EvidenceValue $m01SourceClosure.Detail)) `
        -Reason ("m01_source_closure_" + $m01SourceClosure.Reason) `
        -LogFullPath $logFullPath `
        -ReportedLogPath $LogPath `
        -Status "invalid"
}

Write-EfiPreflightLine (
    "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok" +
    " reviewed=" + $m01SourceClosure.ReviewedSourceCount +
    " prerequisites=" + $m01SourceClosure.PrerequisiteSourceCount +
    " compileItems=" + $m01SourceClosure.CompileItemCount +
    " prerequisiteGap=UefiMemoryType")

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

if ($EnableM01LoadedImageStackProbe) {
    $arguments += "-p:EnableM01LoadedImageStackProbe=true"
    Write-Host "EFI_PROBE M01_LOADED_IMAGE_STACK"
}

if ($EnableM01LiveAllocatorProbe) {
    $arguments += "-p:EnableM01LiveAllocatorProbe=true"
    Write-Host "EFI_PROBE M01_LIVE_ALLOCATOR"
}

if ($EnableM01OwnedPagingProbe) {
    $arguments += "-p:EnableM01OwnedPagingProbe=true"
    Write-Host "EFI_PROBE M01_OWNED_PAGING"
}

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
