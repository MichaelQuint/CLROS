param(
    [string[]] $Fixture = @(),
    [switch] $Contracts,
    [switch] $List,
    [string] $Resume,
    [string] $Results,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repositoryRoot
try {
    # Incremental build is cheap and prevents running old compiled assertions after a fix.
    & dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $arguments = @("tests/OS.HostTests/bin/$Configuration/net10.0/OS.HostTests.dll")
    if ($List) { $arguments += '--list' }
    if ($Contracts) { $arguments += '--contracts' }
    foreach ($id in $Fixture) { $arguments += @('--fixture', $id) }
    if ($Resume) { $arguments += @('--resume', $Resume) }
    if ($Results) { $arguments += @('--results', $Results) }
    & dotnet @arguments
    exit $LASTEXITCODE
}
finally { Pop-Location }
