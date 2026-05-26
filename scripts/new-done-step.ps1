param(
    [string] $Root,
    [switch] $Reserve
)

$ErrorActionPreference = "Stop"

function Write-AllocationFailure {
    param([string] $Reason)

    $safeReason = if ([string]::IsNullOrWhiteSpace($Reason)) {
        "unknown_error"
    }
    else {
        ($Reason -replace "[`r`n]+", " ").Trim()
    }

    Write-Host "TEST_FAIL done.step.allocate $safeReason"
    exit 1
}

Write-Host "TEST_START done.step.allocate"

try {
    if ([string]::IsNullOrWhiteSpace($Root)) {
        $Root = Join-Path $PSScriptRoot ".."
    }

    $rootPath = (Resolve-Path -LiteralPath $Root).ProviderPath
    $donePath = Join-Path $rootPath "done"

    if (-not (Test-Path -LiteralPath $donePath -PathType Container)) {
        Write-AllocationFailure "done_directory_missing path=$donePath"
    }

    $maxStep = 0
    foreach ($file in Get-ChildItem -LiteralPath $donePath -File -Filter "step*.md") {
        if ($file.Name -match '^step([0-9]+)\.md$') {
            $number = [int] $Matches[1]
            if ($number -gt $maxStep) {
                $maxStep = $number
            }
        }
    }

    $nextStep = $maxStep + 1
    $stepName = "step" + $nextStep.ToString("000") + ".md"
    $relativePath = "done/" + $stepName
    $targetPath = Join-Path $donePath $stepName

    if ($Reserve) {
        if (Test-Path -LiteralPath $targetPath) {
            Write-AllocationFailure "target_exists path=$relativePath"
        }

        $template = @(
            "# Step $nextStep - Reserved",
            "",
            "## Summary",
            "",
            "## Why",
            "",
            "## Files changed",
            "",
            "## Tests run",
            "",
            "## Evidence",
            "",
            "## Failures",
            "",
            "## Deferred work",
            ""
        )

        [System.IO.File]::WriteAllLines($targetPath, $template, [System.Text.UTF8Encoding]::new($false))
    }

    Write-Host "NEXT_DONE_STEP $relativePath"
    Write-Host "TEST_PASS done.step.allocate"
}
catch {
    Write-AllocationFailure $_.Exception.Message
}
