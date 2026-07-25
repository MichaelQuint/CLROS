[CmdletBinding()]
param(
    [string]$OutputRoot = ".\artifacts\kestrel-smoke",
    [string]$Configuration = "Release",
    [string]$WorkloadProfile = "SingleMinimalApi",
    [switch]$SkipValidation,
    [switch]$SkipNegativeValidation
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $ScriptRoot "..\.."))
$ProjectPath = Join-Path $ScriptRoot "MinimalApiSmoke.csproj"
$ManifestToolProject = Join-Path $RepoRoot "tools\OS.ManifestTool\OS.ManifestTool.csproj"

if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    $OutputRootFull = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputRoot))
}

$PackageRoot = Join-Path $OutputRootFull "package-root"
$PackageAppRoot = Join-Path $PackageRoot "apps\minimal-api"
$ManifestPath = Join-Path $OutputRootFull "package.json"
$NegativeManifestPath = Join-Path $OutputRootFull "package.missing-expected-body.json"

function Assert-WorkloadProfile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Profile
    )

    if ($Profile -ne "SingleMinimalApi") {
        Write-Host "TEST_FAIL workload.profile.tooling invalid=$Profile"
        exit 1
    }
}

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Contents
    )

    $Encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Contents, $Encoding)
}

function New-KestrelSmokeManifest {
    param(
        [bool]$IncludeExpectedBody
    )

    $Environment = [ordered]@{
        ASPNETCORE_ENVIRONMENT = "Production"
        ASPNETCORE_URLS = "http://0.0.0.0:8080"
    }

    if ($IncludeExpectedBody) {
        $Environment["OS_SMOKE_EXPECTED_BODY"] = "OS_MINIMAL_API_OK"
    }

    return [ordered]@{
        schemaVersion = 0
        workloadProfile = "SingleMinimalApi"
        app = [ordered]@{
            id = "minimal.api.smoke"
            entry = "apps/minimal-api/MinimalApiSmoke.dll"
        }
        args = @()
        env = $Environment
        runtime = [ordered]@{
            kind = "dotnet10"
            assembly = "apps/minimal-api/MinimalApiSmoke.dll"
            kestrel = [ordered]@{
                listen = @(
                    [ordered]@{
                        scheme = "http"
                        host = "0.0.0.0"
                        port = 8080
                    }
                )
            }
        }
    }
}

function Invoke-KestrelPackageValidationCaptured {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Manifest
    )

    $Output = & dotnet run --project $ManifestToolProject "/p:OSWorkloadProfile=$WorkloadProfile" -- validate-kestrel-package --manifest $Manifest --package-root $PackageRoot --profile $WorkloadProfile 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = ($Output -join [Environment]::NewLine)
    }
}

Write-Host "TEST_START kestrel.smoke_app"
Assert-WorkloadProfile -Profile $WorkloadProfile
Write-Host "WORKLOAD_PROFILE $WorkloadProfile"

New-Item -ItemType Directory -Force -Path $PackageAppRoot | Out-Null

& dotnet publish $ProjectPath --configuration $Configuration --framework net10.0 --self-contained false -p:UseAppHost=false "/p:OSWorkloadProfile=$WorkloadProfile" --output $PackageAppRoot
if ($LASTEXITCODE -ne 0) {
    Write-Host "TEST_FAIL kestrel.smoke_app publish_failed exit=$LASTEXITCODE"
    exit $LASTEXITCODE
}

$RequiredFiles = @(
    "MinimalApiSmoke.dll",
    "MinimalApiSmoke.deps.json",
    "MinimalApiSmoke.runtimeconfig.json",
    "appsettings.json"
)

foreach ($RequiredFile in $RequiredFiles) {
    $RequiredPath = Join-Path $PackageAppRoot $RequiredFile
    if (-not (Test-Path -LiteralPath $RequiredPath -PathType Leaf)) {
        Write-Host "TEST_FAIL kestrel.smoke_app missing_publish_file file=$RequiredFile"
        exit 1
    }
}

New-Item -ItemType Directory -Force -Path $OutputRootFull | Out-Null
$ManifestJson = New-KestrelSmokeManifest -IncludeExpectedBody $true | ConvertTo-Json -Depth 12
Write-Utf8NoBomFile -Path $ManifestPath -Contents ($ManifestJson + [Environment]::NewLine)

Write-Host "KESTREL_SMOKE_PACKAGE manifest=$ManifestPath packageRoot=$PackageRoot"
Write-Host "TEST_PASS kestrel.smoke_app publishes package"

if (-not $SkipValidation) {
    & dotnet run --project $ManifestToolProject "/p:OSWorkloadProfile=$WorkloadProfile" -- validate-kestrel-package --manifest $ManifestPath --package-root $PackageRoot --profile $WorkloadProfile
    $ValidationExitCode = $LASTEXITCODE
    if ($ValidationExitCode -ne 0) {
        Write-Host "TEST_FAIL kestrel.smoke_app validation_failed exit=$ValidationExitCode"
        exit $ValidationExitCode
    }

    Write-Host "TEST_PASS kestrel.smoke_app validates package contract"
}

if (-not $SkipNegativeValidation) {
    $NegativeManifestJson = New-KestrelSmokeManifest -IncludeExpectedBody $false | ConvertTo-Json -Depth 12
    Write-Utf8NoBomFile -Path $NegativeManifestPath -Contents ($NegativeManifestJson + [Environment]::NewLine)

    $NegativeResult = Invoke-KestrelPackageValidationCaptured -Manifest $NegativeManifestPath
    if ($NegativeResult.ExitCode -eq 0 -or -not $NegativeResult.Output.Contains("kestrel_package_missing_expected_env")) {
        Write-Host "TEST_FAIL kestrel.smoke_app negative_validation_unexpected exit=$($NegativeResult.ExitCode)"
        exit 1
    }

    Write-Host "TEST_PASS kestrel.smoke_app rejects missing expected body"
}

Write-Host "TEST_PASS kestrel.smoke_app"
