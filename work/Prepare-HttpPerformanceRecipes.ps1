param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$EvidenceRoot = 'C:\Projects\CLROS-archive\20261106-184108-pr814-workspace-cleanup\worktrees\refactor-combined-r01\CLROS\artifacts\refactor-r01',
    [string]$Capsule = 'artifacts/performance-r01/baseline-capsule-preparation-r02'
)
$ErrorActionPreference = 'Stop'
$producer = (Get-Location).Path
$output = [IO.Path]::GetFullPath($OutputDirectory, $producer)
$artifacts = [IO.Path]::Combine($producer, 'artifacts') + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) { throw 'fresh artifacts directory required' }
$null = New-Item -ItemType Directory -Path $output
$tool = Join-Path $producer 'tools/OS.Tooling/bin/Debug/net11.0/OS.Tooling.dll'
$inputs = Join-Path $output 'inputs'
$null = New-Item -ItemType Directory -Path $inputs
$bindings = [Collections.Generic.List[object]]::new()
function Import-Input([string]$original) {
    $source = if ([IO.Path]::IsPathRooted($original)) { $original } else {
        if (-not $original.Replace('\','/').StartsWith('artifacts/')) { throw "unexpected retained input $original" }
        Join-Path ([IO.Path]::GetDirectoryName($EvidenceRoot)) $original.Substring(10)
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "missing retained input $source" }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $destination = Join-Path $inputs ($hash.Substring(0,16) + '-' + [IO.Path]::GetFileName($source))
    if (-not (Test-Path -LiteralPath $destination)) { [IO.File]::Copy($source, $destination, $false) }
    if ((Get-FileHash -LiteralPath $destination).Hash -ne $hash -or (Get-FileHash -LiteralPath $source).Hash -ne $hash) { throw 'input changed during copy' }
    $bindings.Add([pscustomobject]@{ Original = $original; ReadFrom = $source; PrivateCopy = $destination; Sha256 = $hash; Bytes = (Get-Item -LiteralPath $destination).Length })
    return $destination
}
$templatePath = Join-Path $EvidenceRoot 'ancestor-matrix-compat-owner-r01/plan-template-r01.json'
$template = Get-Content -Raw -LiteralPath $templatePath | ConvertFrom-Json
$selected = @('ancestor-socket-sync','ancestor-socket-async','ancestor-epoll-stolen','ancestor-vectors','smoke','ancestor-service-v1','quota256','quota512','positive-v2')
$jobs = [Collections.Generic.List[object]]::new()
function Add-Job([string]$name, [string[]]$arguments, [string]$scope) {
    $directory = Join-Path $output $name
    $efi = Join-Path $directory 'BOOTX64.EFI'
    $actual = [Collections.Generic.List[string]]::new()
    foreach ($value in $arguments) { $actual.Add($value) }
    foreach ($pair in @(@('EfiArtifact',$efi),@('Image',(Join-Path $directory 'disk.raw')),@('SerialLog',(Join-Path $directory 'serial.log')),@('SummaryLog',(Join-Path $directory 'summary.log')))) {
        $at = -1
        for ($i=0; $i -lt $actual.Count; $i++) { if ($actual[$i].TrimStart('-') -ieq $pair[0]) { $at=$i; break } }
        if ($at -ge 0) { $actual[$at+1]=$pair[1] } else { $actual.Add('-'+$pair[0]); $actual.Add($pair[1]) }
    }
    $jobs.Add([pscustomobject]@{ Name=$name; Scope=$scope; ExpectedExit=0; Arguments=$actual.ToArray(); EfiArtifact=$efi })
}
foreach ($name in $selected) {
    $row = @($template.jobs | Where-Object { $_.name -eq $name })
    if ($row.Count -ne 1 -or $row[0].expectedExit -ne 0) { throw "missing positive recipe $name" }
    $arguments = [string[]]$row[0].arguments.Clone()
    for ($i=0; $i -lt $arguments.Length; $i++) {
        if ($arguments[$i].TrimStart('-') -in @('LinuxElfPackage','LinuxThreadingDisassembly','LinuxHelloDisassembly')) { $arguments[$i+1]=Import-Input $arguments[$i+1]; $i++ }
    }
    Add-Job $name $arguments 'affected legacy socket/service original; current complete consumer'
}
$capsuleRoot = [IO.Path]::GetFullPath($Capsule,$producer)
$package = Join-Path $capsuleRoot 'inputs/package.pkg'
$elf = Join-Path $capsuleRoot 'inputs/guest.txt'
$assets = Join-Path $capsuleRoot 'inputs/assets'
foreach ($variant in @('DiskA1','DiskA2','DiskB','TruncatedA','Timing','QuietCandidate01','QuietCandidate02','QuietCandidate03')) {
    $probe = if ($variant -eq 'Timing') {'-EnableM10018TimingProbe'} elseif ($variant.StartsWith('Quiet')) {'-EnableM10018ServingBenchmarkProbe'} else {'-EnableM10018FullProbe'}
    $arguments = @('-BuildEfi',$probe,'-Accelerator','whpx','-MemoryMiB','256','-LinuxElfPackage',$package,'-LinuxThreadingDisassembly',$elf,'-WebRootAssets',$assets,'-WebRootFixture',$(if($variant -eq 'DiskB'){'DiskB'}else{'DiskA'}))
    if ($variant -eq 'TruncatedA') { $arguments += @('-WebRootFault','Truncated') }
    if ($variant.StartsWith('Quiet')) { $arguments += @('-HttpBenchmarkWarmSequences','10') }
    Add-Job $variant $arguments $(if($variant.StartsWith('Quiet')){'fresh first-plus-ten quiet diagnostic; deliberate stop'}elseif($variant -eq 'Timing'){'short diagnostic; original finite timing consumer'}else{'ordinary complete HTTP/browser/natural cleanup'})
}
foreach ($variant in @('Progress','CloseReuse','ExitPending')) {
    $path = Join-Path $inputs ('disk-race-'+$variant+'.pkg')
    & dotnet $tool linux disk-race-package $variant $path > (Join-Path $output ('package-'+$variant+'.log'))
    if ($LASTEXITCODE -ne 0) { throw "canonical race package failed $variant" }
    Add-Job ('M16-'+$variant) @('-BuildEfi','-EnableM16025Probe','-Accelerator','whpx','-LinuxElfPackage',$path,'-LinuxDiskRaceVariant',$variant,
        '-WebRootFixture','DiskA','-WebRootAssets',$assets) 'original cancellation/close/reuse race and exact cleanup'
}
$owned = @('EnableM01LoadedImageStackProbe','EnableM01LiveAllocatorProbe','EnableAcpiPhysicalDiscoveryProbe','EnableM03FirstRealExitBootServicesProbe','EnableM03OwnedSubsystemSurvivalProbe','EnableM05PciEnumerationProbe')
Add-Job 'LegacyBlock' (@('-BuildEfi','-Accelerator','whpx') + @($owned | ForEach-Object {'-'+$_}) +
    @('-EnableM06VirtioBlockQueueSetupProbe','-EnableM06VirtioBlockReadProbe','-EnableM06LiveBlockContractProbe')) 'legacy one-page physical block request and reset/release'
$networkPackage = Join-Path $inputs 'network-threads.pkg'
& dotnet $tool linux thread-fixture $networkPackage Threads > (Join-Path $output 'package-network.log')
if ($LASTEXITCODE -ne 0) { throw 'canonical staged network package failed' }
Add-Job 'Network' @('-BuildEfi','-EnableM07026Probe','-Accelerator','whpx','-LinuxElfPackage',$networkPackage,'-TimeoutSeconds','285') 'original legacy single-flight live packet and ownership contract'
# Only query-only managed dry-runs occur here. Native artifacts/VMs are owned by
# the later frozen qualification. Every actual job uses fresh outputs above.
foreach ($job in $jobs) {
    & dotnet $tool qemu smoke @($job.Arguments) -DryRun > (Join-Path $output ($job.Name+'.dry-run.log'))
    if ($LASTEXITCODE -ne 0) { throw "recipe rejected $($job.Name)" }
}
$profiles = @(
    @{Name='ordinary';Flags=@()},
    @{Name='legacy-block';Flags=$owned+@('EnableM06VirtioBlockQueueSetupProbe','EnableM06VirtioBlockReadProbe','EnableM06LiveBlockContractProbe')},
    @{Name='network';Flags=@('EnableM07026Probe')},
    @{Name='sockets';Flags=@('EnableM16011Probe')},
    @{Name='epoll';Flags=@('EnableM16012Probe')},
    @{Name='vectors';Flags=@('EnableM16013Probe')},
    @{Name='web';Flags=@('EnableM10004Probe')},
    @{Name='service';Flags=@('EnableM16014Probe')},
    @{Name='disk';Flags=@('EnableM16025Probe')},
    @{Name='writable-layout';Flags=@('EnableM16027Probe')},
    @{Name='full';Flags=@('EnableM10018FullProbe')},
    @{Name='timing';Flags=@('EnableM10018TimingProbe')},
    @{Name='quiet';Flags=@('EnableM10018ServingBenchmarkProbe')}
)
foreach ($profile in $profiles) {
    & dotnet $tool efi source-inputs @($profile.Flags) > (Join-Path $output ($profile.Name+'.sources.json'))
    if ($LASTEXITCODE -ne 0) { throw "source selection failed $($profile.Name)" }
    $profile.NativeArguments = @('efi','build','-EfiOutputPath',(Join-Path $output ('native/'+$profile.Name+'/BOOTX64.EFI'))) + @($profile.Flags | ForEach-Object {'-'+$_})
}
$manifest = [ordered]@{
    Schema='clros-http-performance-recipes-v1'; PreparationHead=(& git rev-parse HEAD); Dirty=@(& git status --porcelain).Count -ne 0
    OriginalRecipe=$templatePath; OriginalRecipeSha256=(Get-FileHash -LiteralPath $templatePath).Hash
    Inputs=$bindings; NativeProfiles=$profiles; GuestJobs=$jobs
    BaselineJobs=@(1..3 | ForEach-Object { [ordered]@{ Name=('Baseline{0:D2}' -f $_); ExpectedExit=0; Arguments=@('benchmark','retained-serving',$EvidenceRoot,
        'C:\Projects\CLROS-archive\20261106-184108-pr814-workspace-cleanup\worktrees\pr814-qualified-closeout-r01\CLROS\artifacts\pr814-closeout-r01\qualified-receipts-readback-r01.json',
        (Join-Path ([IO.Path]::GetDirectoryName($EvidenceRoot)) 'images/matched-current2e-whpx-boot01-r01.raw'),
        (Join-Path $output ('Baseline{0:D2}' -f $_)),'--run') } })
    BaselineGuestSource='2e81ee3abd1efbcfe6fa92233cd1449a4729ce70'; BaselineBoots=3; CandidateBoots=3; WarmSequencesPerBoot=10
    SourceOnly=$true; NativePublished=$false; GuestExecuted=$false; Qualified=$false
    NativeReviewObligation='Review actual emitted DMA/TCP and shared legacy callers before activation; source selection is not emitted-stack proof.'
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'recipes.json') -Encoding utf8
'PERFORMANCE_RECIPES_PREPARED native_profiles='+$profiles.Count+' guest_jobs='+$jobs.Count+' vm_executed=false'
