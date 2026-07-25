[CmdletBinding()]
param(
    [string]$EfiArtifact = "artifacts/efi/BOOTX64.EFI",
    [string]$ProjectPath = "src/OS/OS.csproj",
    [string]$EntrySourcePath = "src/OS/Boot/EfiEntry.cs",
    [string]$LinkerMapPath = "",
    [string]$LinkResponsePath = "",
    [string]$LinkEvidencePath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$testName = "efi.artifact.verify"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Write-Failure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Reason,
        [string]$Detail = ""
    )

    $safeDetail = $Detail.Replace("`r", " ").Replace("`n", " ").Trim()
    if ([string]::IsNullOrWhiteSpace($safeDetail)) {
        Write-Output "TEST_FAIL $testName $Reason"
    }
    else {
        Write-Output "TEST_FAIL $testName $Reason $safeDetail"
    }

    exit 1
}

function Get-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

function Assert-ByteRange {
    param(
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [long]$Count,
        [Parameter(Mandatory = $true)]
        [long]$Length,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    if ($Offset -lt 0 -or $Count -lt 0 -or $Offset -gt $Length -or $Count -gt ($Length - $Offset)) {
        Write-Failure $Reason
    }
}

function Read-UInt16LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 2 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt16($Bytes, [int]$Offset)
}

function Read-UInt32LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 4 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt32($Bytes, [int]$Offset)
}

function Read-UInt64LittleEndian {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    Assert-ByteRange $Offset 8 $Bytes.LongLength $Reason
    return [System.BitConverter]::ToUInt64($Bytes, [int]$Offset)
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Convert-HexUInt64 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Reason
    )

    try {
        return [System.Convert]::ToUInt64($Value, 16)
    }
    catch {
        Write-Failure $Reason "value=$Value"
    }
}

function Read-DataDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes,
        [Parameter(Mandatory = $true)]
        [long]$OptionalHeaderOffset,
        [Parameter(Mandatory = $true)]
        [long]$OptionalHeaderSize,
        [Parameter(Mandatory = $true)]
        [uint32]$DirectoryCount,
        [Parameter(Mandatory = $true)]
        [int]$Index,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($DirectoryCount -le $Index) {
        return [pscustomobject]@{
            Name = $Name
            Rva = [uint32]0
            Size = [uint32]0
        }
    }

    $directoryOffset = $OptionalHeaderOffset + 112L + ([long]$Index * 8L)
    $optionalHeaderEnd = $OptionalHeaderOffset + $OptionalHeaderSize
    if ($directoryOffset -lt $OptionalHeaderOffset -or ($directoryOffset + 8L) -gt $optionalHeaderEnd) {
        Write-Failure "invalid_data_directory" "directory=$Name"
    }

    return [pscustomobject]@{
        Name = $Name
        Rva = Read-UInt32LittleEndian $Bytes $directoryOffset "truncated_data_directory"
        Size = Read-UInt32LittleEndian $Bytes ($directoryOffset + 4L) "truncated_data_directory"
    }
}

Write-Output "TEST_START $testName"

if (-not [System.BitConverter]::IsLittleEndian) {
    Write-Failure "unsupported_host_endianness"
}

$artifactPath = Get-AbsolutePath $EfiArtifact
$projectFullPath = Get-AbsolutePath $ProjectPath
$entrySourceFullPath = Get-AbsolutePath $EntrySourcePath
if ([string]::IsNullOrWhiteSpace($LinkerMapPath)) {
    $linkerMapFullPath = $artifactPath + ".link.map"
}
else {
    $linkerMapFullPath = Get-AbsolutePath $LinkerMapPath
}
if ([string]::IsNullOrWhiteSpace($LinkResponsePath)) {
    $linkResponseFullPath = $artifactPath + ".link.rsp"
}
else {
    $linkResponseFullPath = Get-AbsolutePath $LinkResponsePath
}
if ([string]::IsNullOrWhiteSpace($LinkEvidencePath)) {
    $linkEvidenceFullPath = $artifactPath + ".link.json"
}
else {
    $linkEvidenceFullPath = Get-AbsolutePath $LinkEvidencePath
}

if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
    Write-Failure "missing_artifact" "path=$artifactPath"
}

try {
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($artifactPath)
}
catch {
    Write-Failure "artifact_read_failed" "path=$artifactPath"
}

if ($bytes.LongLength -eq 0) {
    Write-Failure "empty_artifact" "path=$artifactPath"
}

Assert-ByteRange 0 64 $bytes.LongLength "truncated_dos_header"
if ($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
    Write-Failure "invalid_dos_signature"
}

$peOffset = [long](Read-UInt32LittleEndian $bytes 0x3c "truncated_dos_header")
if ($peOffset -lt 64) {
    Write-Failure "invalid_pe_offset" "offset=$peOffset"
}

Assert-ByteRange $peOffset 24 $bytes.LongLength "truncated_pe_header"
if ($bytes[$peOffset] -ne 0x50 -or
    $bytes[$peOffset + 1] -ne 0x45 -or
    $bytes[$peOffset + 2] -ne 0 -or
    $bytes[$peOffset + 3] -ne 0) {
    Write-Failure "invalid_pe_signature"
}

$coffHeaderOffset = $peOffset + 4L
$machine = Read-UInt16LittleEndian $bytes $coffHeaderOffset "truncated_coff_header"
$sectionCount = Read-UInt16LittleEndian $bytes ($coffHeaderOffset + 2L) "truncated_coff_header"
$coffTimestamp = Read-UInt32LittleEndian $bytes ($coffHeaderOffset + 4L) "truncated_coff_header"
$optionalHeaderSize = Read-UInt16LittleEndian $bytes ($coffHeaderOffset + 16L) "truncated_coff_header"

if ($machine -ne 0x8664) {
    Write-Failure "machine_not_x64" ("expected=0x8664 actual=0x{0:x4}" -f $machine)
}

if ($sectionCount -eq 0 -or $sectionCount -gt 96) {
    Write-Failure "section_count_out_of_range" "count=$sectionCount"
}

if ($optionalHeaderSize -lt 112) {
    Write-Failure "optional_header_too_small" "size=$optionalHeaderSize"
}

$optionalHeaderOffset = $coffHeaderOffset + 20L
Assert-ByteRange $optionalHeaderOffset $optionalHeaderSize $bytes.LongLength "truncated_optional_header"

$optionalMagic = Read-UInt16LittleEndian $bytes $optionalHeaderOffset "truncated_optional_header"
if ($optionalMagic -ne 0x20b) {
    Write-Failure "format_not_pe32_plus" ("expected=0x020b actual=0x{0:x4}" -f $optionalMagic)
}

$entryPointRva = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 16L) "truncated_optional_header"
if ($entryPointRva -eq 0) {
    Write-Failure "entrypoint_zero"
}
$imageBase = Read-UInt64LittleEndian $bytes ($optionalHeaderOffset + 24L) "truncated_optional_header"
if ($imageBase -eq 0) {
    Write-Failure "image_base_zero"
}

$subsystem = Read-UInt16LittleEndian $bytes ($optionalHeaderOffset + 68L) "truncated_optional_header"
if ($subsystem -ne 10) {
    Write-Failure "subsystem_not_efi_application" "expected=10 actual=$subsystem"
}

$directoryCount = Read-UInt32LittleEndian $bytes ($optionalHeaderOffset + 108L) "truncated_optional_header"
$maximumDirectoryCount = [uint32](($optionalHeaderSize - 112) / 8)
if ($directoryCount -gt $maximumDirectoryCount) {
    Write-Failure "data_directory_count_out_of_range" "declared=$directoryCount available=$maximumDirectoryCount"
}

$importDirectories = @(
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 1 "import"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 11 "bound-import"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 12 "iat"),
    (Read-DataDirectory $bytes $optionalHeaderOffset $optionalHeaderSize $directoryCount 13 "delay-import")
)

foreach ($directory in $importDirectories) {
    if (($directory.Rva -eq 0) -ne ($directory.Size -eq 0)) {
        Write-Failure "malformed_import_directory" "directory=$($directory.Name)"
    }

    if ($directory.Rva -ne 0 -or $directory.Size -ne 0) {
        Write-Failure "ordinary_runtime_imports_present" ("directory={0} rva=0x{1:x8} size={2}" -f $directory.Name, $directory.Rva, $directory.Size)
    }
}

$sectionTableOffset = $optionalHeaderOffset + $optionalHeaderSize
$sectionTableSize = [long]$sectionCount * 40L
Assert-ByteRange $sectionTableOffset $sectionTableSize $bytes.LongLength "truncated_section_table"

$entrySectionCount = 0
$entrySectionName = ""
$entrySectionIsExecutable = $false
$entryPointIsFileBacked = $false
$entryPointFileOffset = [uint64]0

for ($index = 0; $index -lt $sectionCount; $index++) {
    $sectionOffset = $sectionTableOffset + ([long]$index * 40L)
    $nameBytes = New-Object byte[] 8
    [System.Array]::Copy($bytes, [int]$sectionOffset, $nameBytes, 0, 8)
    $sectionName = [System.Text.Encoding]::ASCII.GetString($nameBytes).TrimEnd([char]0)

    $virtualSize = Read-UInt32LittleEndian $bytes ($sectionOffset + 8L) "truncated_section_table"
    $virtualAddress = Read-UInt32LittleEndian $bytes ($sectionOffset + 12L) "truncated_section_table"
    $rawSize = Read-UInt32LittleEndian $bytes ($sectionOffset + 16L) "truncated_section_table"
    $rawOffset = Read-UInt32LittleEndian $bytes ($sectionOffset + 20L) "truncated_section_table"
    $characteristics = Read-UInt32LittleEndian $bytes ($sectionOffset + 36L) "truncated_section_table"

    if ($rawSize -ne 0) {
        Assert-ByteRange ([long]$rawOffset) ([long]$rawSize) $bytes.LongLength "section_raw_data_out_of_range"
    }

    $mappedSize = [uint64][System.Math]::Max([uint64]$virtualSize, [uint64]$rawSize)
    if ($mappedSize -eq 0) {
        continue
    }

    $rangeStart = [uint64]$virtualAddress
    $rangeEnd = $rangeStart + $mappedSize
    if ($rangeEnd -gt 0x100000000L) {
        Write-Failure "section_virtual_range_overflow" "section=$sectionName"
    }

    $entry = [uint64]$entryPointRva
    if ($entry -ge $rangeStart -and $entry -lt $rangeEnd) {
        $entrySectionCount++
        $entrySectionName = $sectionName
        $entrySectionIsExecutable = ($characteristics -band 0x20000000) -ne 0

        $entryDelta = $entry - $rangeStart
        if ($entryDelta -lt [uint64]$rawSize) {
            $entryFileOffset = [uint64]$rawOffset + $entryDelta
            $entryPointIsFileBacked = $entryFileOffset -lt [uint64]$bytes.LongLength
            $entryPointFileOffset = $entryFileOffset
        }
    }
}

if ($entrySectionCount -eq 0) {
    Write-Failure "entrypoint_outside_sections" ("rva=0x{0:x8}" -f $entryPointRva)
}

if ($entrySectionCount -ne 1) {
    Write-Failure "entrypoint_section_ambiguous" ("rva=0x{0:x8} matches={1}" -f $entryPointRva, $entrySectionCount)
}

if (-not $entrySectionIsExecutable) {
    Write-Failure "entrypoint_section_not_executable" "section=$entrySectionName"
}

if (-not $entryPointIsFileBacked) {
    Write-Failure "entrypoint_not_file_backed" "section=$entrySectionName"
}

$entryPointFirstByte = $bytes[[int]$entryPointFileOffset]
if ($entryPointFirstByte -in @(0x00, 0xc3, 0xcb, 0xcc)) {
    Write-Failure "entrypoint_trivial_stub" ("byte=0x{0:x2}" -f $entryPointFirstByte)
}

if (-not (Test-Path -LiteralPath $projectFullPath -PathType Leaf)) {
    Write-Failure "missing_build_contract" "path=$projectFullPath"
}

if (-not (Test-Path -LiteralPath $entrySourceFullPath -PathType Leaf)) {
    Write-Failure "missing_entry_source_contract" "path=$entrySourceFullPath"
}

try {
    $projectText = [System.IO.File]::ReadAllText($projectFullPath)
    $entrySourceText = [System.IO.File]::ReadAllText($entrySourceFullPath)
}
catch {
    Write-Failure "entry_contract_read_failed"
}

if ($projectText -notmatch '<EntryPointSymbol>\s*EfiMain\s*</EntryPointSymbol>') {
    Write-Failure "efimain_project_contract_missing" "contract=EntryPointSymbol"
}

if ($projectText -notmatch '<LinkerSubsystem>\s*EFI_APPLICATION\s*</LinkerSubsystem>') {
    Write-Failure "efimain_project_contract_missing" "contract=LinkerSubsystem"
}

if ($projectText -notmatch '/ENTRY:\$\(EntryPointSymbol\)') {
    Write-Failure "efimain_project_contract_missing" "contract=linker-entry"
}

if ($projectText -notmatch '/NODEFAULTLIB') {
    Write-Failure "efimain_project_contract_missing" "contract=no-default-library"
}

if ($entrySourceText -notmatch 'RuntimeExport\s*\(\s*"EfiMain"\s*\)') {
    Write-Failure "efimain_source_contract_missing" "contract=RuntimeExport"
}

foreach ($linkerEvidenceInput in @(
    [pscustomobject]@{ Path = $linkerMapFullPath; Reason = "missing_linker_map"; MaximumBytes = 16777216L },
    [pscustomobject]@{ Path = $linkResponseFullPath; Reason = "missing_link_response"; MaximumBytes = 1048576L },
    [pscustomobject]@{ Path = $linkEvidenceFullPath; Reason = "missing_link_evidence"; MaximumBytes = 1048576L }
)) {
    if (-not (Test-Path -LiteralPath $linkerEvidenceInput.Path -PathType Leaf)) {
        Write-Failure $linkerEvidenceInput.Reason "path=$($linkerEvidenceInput.Path)"
    }

    $evidenceLength = (Get-Item -LiteralPath $linkerEvidenceInput.Path).Length
    if ($evidenceLength -eq 0 -or $evidenceLength -gt $linkerEvidenceInput.MaximumBytes) {
        Write-Failure "link_evidence_size_out_of_range" (
            "path={0} bytes={1}" -f $linkerEvidenceInput.Path, $evidenceLength)
    }
}

try {
    $linkerMapText = [System.IO.File]::ReadAllText($linkerMapFullPath)
    $linkResponseText = [System.IO.File]::ReadAllText($linkResponseFullPath)
    $linkEvidenceText = [System.IO.File]::ReadAllText($linkEvidenceFullPath)
    $linkEvidence = $linkEvidenceText | ConvertFrom-Json
}
catch {
    Write-Failure "link_evidence_read_failed" "type=$($_.Exception.GetType().Name)"
}

$requiredEvidenceProperties = @(
    "schemaVersion",
    "artifactSha256",
    "artifactBytes",
    "linkerMapSha256",
    "linkResponseSha256",
    "sourceProjectSha256",
    "entrySourceSha256",
    "entryPointSymbol",
    "managedEntrySymbol",
    "linkerPath",
    "linkerVersion",
    "linkerSha256"
)
foreach ($propertyName in $requiredEvidenceProperties) {
    if ($null -eq $linkEvidence.PSObject.Properties[$propertyName]) {
        Write-Failure "link_evidence_property_missing" "property=$propertyName"
    }
}

if ($linkEvidence.schemaVersion -ne 1 -or
    $linkEvidence.entryPointSymbol -cne "EfiMain" -or
    $linkEvidence.managedEntrySymbol -cne "OS_OS_Boot_EfiEntry__EfiMain") {
    Write-Failure "link_evidence_contract_invalid"
}

$sha256 = [System.BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
).Replace("-", "").ToLowerInvariant()
$linkerMapSha256 = Get-Sha256 -Path $linkerMapFullPath
$linkResponseSha256 = Get-Sha256 -Path $linkResponseFullPath
$projectSha256 = Get-Sha256 -Path $projectFullPath
$entrySourceSha256 = Get-Sha256 -Path $entrySourceFullPath

foreach ($hashProperty in @(
    "artifactSha256",
    "linkerMapSha256",
    "linkResponseSha256",
    "sourceProjectSha256",
    "entrySourceSha256",
    "linkerSha256"
)) {
    $hashValue = [string] $linkEvidence.$hashProperty
    if ($hashValue -cnotmatch '^[0-9a-f]{64}$') {
        Write-Failure "link_evidence_hash_malformed" "property=$hashProperty"
    }
}

if ($linkEvidence.artifactSha256 -cne $sha256 -or
    [long]$linkEvidence.artifactBytes -ne $bytes.LongLength -or
    $linkEvidence.linkerMapSha256 -cne $linkerMapSha256 -or
    $linkEvidence.linkResponseSha256 -cne $linkResponseSha256 -or
    $linkEvidence.sourceProjectSha256 -cne $projectSha256 -or
    $linkEvidence.entrySourceSha256 -cne $entrySourceSha256) {
    Write-Failure "link_evidence_hash_mismatch"
}

$evidenceLinkerPath = Get-AbsolutePath ([string]$linkEvidence.linkerPath)
if (-not (Test-Path -LiteralPath $evidenceLinkerPath -PathType Leaf) -or
    [string]::IsNullOrWhiteSpace([string]$linkEvidence.linkerVersion) -or
    (Get-Sha256 -Path $evidenceLinkerPath) -cne [string]$linkEvidence.linkerSha256) {
    Write-Failure "linker_identity_mismatch"
}

$entryOptions = [System.Text.RegularExpressions.Regex]::Matches(
    $linkResponseText,
    '(?im)^\s*/ENTRY:([^\s"]+)\s*$')
if ($entryOptions.Count -ne 1 -or
    $entryOptions[0].Groups[1].Value -cne "EfiMain") {
    Write-Failure "efimain_link_response_contract_missing"
}

$preferredBaseMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    '(?im)^\s*Preferred load address is ([0-9a-f]{16})\s*$')
$timestampMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    '(?im)^\s*Timestamp is ([0-9a-f]{8})(?:\s|\(|$)')
$entryPointMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    '(?im)^\s*entry point at\s+([0-9a-f]{4}):([0-9a-f]{8})\s*$')
$efiMainMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    '(?im)^\s*([0-9a-f]{4}):([0-9a-f]{8})\s+EfiMain\s+([0-9a-f]{16})\s+\S.*$')
$managedEntryMatches = [System.Text.RegularExpressions.Regex]::Matches(
    $linkerMapText,
    '(?im)^\s*([0-9a-f]{4}):([0-9a-f]{8})\s+OS_OS_Boot_EfiEntry__EfiMain\s+([0-9a-f]{16})\s+\S.*$')

if ($preferredBaseMatches.Count -ne 1 -or
    $timestampMatches.Count -ne 1 -or
    $entryPointMatches.Count -ne 1 -or
    $efiMainMatches.Count -ne 1 -or
    $managedEntryMatches.Count -ne 1) {
    Write-Failure "efimain_linker_map_contract_missing"
}

$preferredBase = Convert-HexUInt64 $preferredBaseMatches[0].Groups[1].Value "invalid_map_preferred_base"
$mapTimestamp = Convert-HexUInt64 $timestampMatches[0].Groups[1].Value "invalid_map_timestamp"
$efiMainVirtualAddress = Convert-HexUInt64 $efiMainMatches[0].Groups[3].Value "invalid_map_efimain_address"
$managedEntryVirtualAddress = Convert-HexUInt64 $managedEntryMatches[0].Groups[3].Value "invalid_map_managed_entry_address"

if ($preferredBase -ne $imageBase -or
    $mapTimestamp -ne [uint64]$coffTimestamp -or
    $efiMainVirtualAddress -ne $managedEntryVirtualAddress -or
    $efiMainVirtualAddress -lt $preferredBase -or
    ($efiMainVirtualAddress - $preferredBase) -gt [uint64][uint32]::MaxValue -or
    [uint32]($efiMainVirtualAddress - $preferredBase) -ne $entryPointRva -or
    $efiMainMatches[0].Groups[1].Value -cne $entryPointMatches[0].Groups[1].Value -or
    $efiMainMatches[0].Groups[2].Value -cne $entryPointMatches[0].Groups[2].Value -or
    $efiMainMatches[0].Groups[1].Value -cne $managedEntryMatches[0].Groups[1].Value -or
    $efiMainMatches[0].Groups[2].Value -cne $managedEntryMatches[0].Groups[2].Value) {
    Write-Failure "efimain_entrypoint_mismatch"
}

Write-Output "EFI_ARTIFACT_VERIFY_PATH $artifactPath"
Write-Output ("EFI_ARTIFACT_PE format=PE32+ machine=0x{0:x4} subsystem={1} entryRva=0x{2:x8} entrySection={3}" -f $machine, $subsystem, $entryPointRva, $entrySectionName)
Write-Output "EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent"
Write-Output ("EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x{0:x8}" -f $entryPointRva)
Write-Output "EFI_ARTIFACT_LINK_MAP path=$linkerMapFullPath sha256=$linkerMapSha256"
Write-Output "EFI_ARTIFACT_LINK_RESPONSE path=$linkResponseFullPath sha256=$linkResponseSha256"
Write-Output "EFI_ARTIFACT_SHA256 $sha256"
Write-Output "TEST_PASS $testName"
exit 0
