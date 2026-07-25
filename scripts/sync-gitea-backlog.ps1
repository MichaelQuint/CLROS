[CmdletBinding()]
param(
    [switch]$Apply,
    [switch]$Offline,
    [switch]$SelfTest,
    [string]$ManifestPath = "",
    [string]$BaseUrl = "http://localhost:3000",
    [string]$Owner = "admin",
    [string]$Repository = "CLROS"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$script:ExpectedBaseUrl = "http://localhost:3000"
$script:ExpectedOwner = "admin"
$script:ExpectedRepository = "CLROS"
$script:ApiHeaders = @{}
$script:ApiRoot = ""
$script:ApplyChanges = [bool]$Apply
$script:ManagedStart = "<!-- clros-managed-start -->"
$script:ManagedEnd = "<!-- clros-managed-end -->"
$script:ClosureEvidenceMarkerFormat = "<!-- clros-evidence-reviewed:{key} -->"
$script:DefaultManifestPath = Join-Path $PSScriptRoot "..\.gitea\backlog\clros-issues.json"
$script:Counters = [ordered]@{
    created = 0
    updated = 0
    unchanged = 0
    skipped = 0
    failed = 0
    wouldCreate = 0
    wouldUpdate = 0
}
$script:ActionKeys = @{
    created = (New-Object System.Collections.Generic.List[string])
    updated = (New-Object System.Collections.Generic.List[string])
    unchanged = (New-Object System.Collections.Generic.List[string])
    skipped = (New-Object System.Collections.Generic.List[string])
    failed = (New-Object System.Collections.Generic.List[string])
    wouldCreate = (New-Object System.Collections.Generic.List[string])
    wouldUpdate = (New-Object System.Collections.Generic.List[string])
}

function Write-SyncLine {
    param([string]$Text)

    Write-Output $Text
}

function Record-SyncAction {
    param(
        [ValidateSet("created", "updated", "unchanged", "skipped", "failed", "wouldCreate", "wouldUpdate")]
        [string]$Action,
        [string]$Key
    )

    switch ($Action) {
        "created" { $script:Counters.created++ }
        "updated" { $script:Counters.updated++ }
        "unchanged" { $script:Counters.unchanged++ }
        "skipped" { $script:Counters.skipped++ }
        "failed" { $script:Counters.failed++ }
        "wouldCreate" { $script:Counters.wouldCreate++ }
        "wouldUpdate" { $script:Counters.wouldUpdate++ }
    }
    $script:ActionKeys[$Action].Add($Key)
}

function Write-SyncActionKeys {
    foreach ($action in @("created", "updated", "unchanged", "skipped", "failed", "wouldCreate", "wouldUpdate")) {
        $keys = @($script:ActionKeys[$action] | Sort-Object -Unique)
        if ($keys.Count -eq 0) {
            Write-SyncLine "SYNC_KEYS action=$action count=0 keys=none"
            continue
        }

        $chunkSize = 50
        $chunkCount = [int][Math]::Ceiling($keys.Count / [double]$chunkSize)
        for ($chunkIndex = 0; $chunkIndex -lt $chunkCount; $chunkIndex++) {
            $start = $chunkIndex * $chunkSize
            $end = [Math]::Min($start + $chunkSize - 1, $keys.Count - 1)
            $chunk = @($keys[$start..$end]) -join ","
            Write-SyncLine "SYNC_KEYS action=$action count=$($keys.Count) chunk=$($chunkIndex + 1)/$chunkCount keys=$chunk"
        }
    }
}

function Get-ObjectProperty {
    param(
        [object]$Object,
        [string]$Name,
        [object]$DefaultValue = $null
    )

    if ($null -eq $Object) {
        return $DefaultValue
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function ConvertTo-NormalizedArray {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value | ForEach-Object { $_ })
}

function Test-StringSetEqual {
    param(
        [string[]]$Left,
        [string[]]$Right
    )

    $leftNormalized = @($Left | Sort-Object -Unique)
    $rightNormalized = @($Right | Sort-Object -Unique)
    if ($leftNormalized.Count -ne $rightNormalized.Count) {
        return $false
    }

    for ($index = 0; $index -lt $leftNormalized.Count; $index++) {
        if ($leftNormalized[$index] -cne $rightNormalized[$index]) {
            return $false
        }
    }

    return $true
}

function Unquote-YamlScalar {
    param([string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $trimmed = $Value.Trim()
    if ($trimmed.Length -ge 2) {
        if (($trimmed[0] -eq '"' -and $trimmed[$trimmed.Length - 1] -eq '"') -or
            ($trimmed[0] -eq "'" -and $trimmed[$trimmed.Length - 1] -eq "'")) {
            return $trimmed.Substring(1, $trimmed.Length - 2)
        }
    }

    return $trimmed
}

function Get-TeaConfigToken {
    $candidatePaths = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $candidatePaths.Add((Join-Path $env:APPDATA "tea\config.yml"))
    }
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $candidatePaths.Add((Join-Path $env:LOCALAPPDATA "tea\config.yml"))
    }
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidatePaths.Add((Join-Path $env:USERPROFILE ".config\tea\config.yml"))
        $candidatePaths.Add((Join-Path $env:USERPROFILE ".tea\config.yml"))
        $candidatePaths.Add((Join-Path $env:USERPROFILE ".gitea\tea.yml"))
    }

    foreach ($candidatePath in @($candidatePaths | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            continue
        }

        $current = $null
        $records = New-Object System.Collections.Generic.List[object]
        foreach ($line in Get-Content -LiteralPath $candidatePath -Encoding UTF8) {
            if ($line -match '^\s*-\s+name\s*:\s*(.+?)\s*$') {
                if ($null -ne $current) {
                    $records.Add($current)
                }
                $current = [ordered]@{
                    name = Unquote-YamlScalar $Matches[1]
                    url = ""
                    token = ""
                }
                continue
            }

            if ($null -eq $current) {
                continue
            }

            if ($line -match '^\s+url\s*:\s*(.+?)\s*$') {
                $current.url = Unquote-YamlScalar $Matches[1]
            }
            elseif ($line -match '^\s+token\s*:\s*(.+?)\s*$') {
                $current.token = Unquote-YamlScalar $Matches[1]
            }
        }
        if ($null -ne $current) {
            $records.Add($current)
        }

        foreach ($record in $records) {
            if ($record.name -eq "local-gitea" -and
                $record.url.TrimEnd("/") -eq $script:ExpectedBaseUrl -and
                -not [string]::IsNullOrWhiteSpace($record.token)) {
                return [string]$record.token
            }
        }
    }

    return ""
}

function Get-ApiHeaders {
    param([bool]$RequireAuthentication)

    $token = ""
    foreach ($variableName in @("CLROS_GITEA_TOKEN", "GITEA_TOKEN", "TEA_TOKEN")) {
        $candidate = [Environment]::GetEnvironmentVariable($variableName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $token = $candidate
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = Get-TeaConfigToken
    }

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        return @{ Authorization = "token $token" }
    }

    $username = [Environment]::GetEnvironmentVariable("CLROS_GITEA_USERNAME")
    $password = [Environment]::GetEnvironmentVariable("CLROS_GITEA_PASSWORD")
    if (-not [string]::IsNullOrWhiteSpace($username) -and
        -not [string]::IsNullOrWhiteSpace($password)) {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("${username}:${password}")
        $basic = [Convert]::ToBase64String($bytes)
        return @{ Authorization = "Basic $basic" }
    }

    if ($RequireAuthentication) {
        throw "Authenticated Gitea access is required for -Apply. Configure the local-gitea tea login or set CLROS_GITEA_TOKEN."
    }

    return @{}
}

function Invoke-GiteaApi {
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "$($script:ApiRoot)$Path"
    $arguments = @{
        Uri = $uri
        Method = $Method
        Headers = $script:ApiHeaders
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $arguments.ContentType = "application/json; charset=utf-8"
        $arguments.Body = $Body | ConvertTo-Json -Depth 32 -Compress
    }

    return Invoke-RestMethod @arguments
}

function Get-GiteaCollection {
    param([string]$Path)

    $items = New-Object System.Collections.Generic.List[object]
    $page = 1
    $limit = 50
    do {
        $separator = "?"
        if ($Path.Contains("?")) {
            $separator = "&"
        }
        $response = Invoke-GiteaApi -Method GET -Path "${Path}${separator}limit=${limit}&page=${page}"
        $batch = @(ConvertTo-NormalizedArray $response)
        foreach ($item in $batch) {
            $items.Add($item)
        }
        $page++
    } while ($batch.Count -eq $limit)

    return $items.ToArray()
}

function Get-HiddenIssueKey {
    param([string]$Body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return ""
    }

    $match = [regex]::Match($Body, '<!--\s*clros-issue-key:([A-Z0-9-]+)\s*-->', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        return ""
    }

    return $match.Groups[1].Value.ToUpperInvariant()
}

function Get-ClosureEvidenceMarker {
    param([string]$Key)

    return $script:ClosureEvidenceMarkerFormat.Replace(
        "{key}",
        $Key.ToUpperInvariant())
}

function Get-NormalizedOwnedPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $normalized = $Path.Trim().Replace("\", "/")
    while ($normalized.StartsWith("./", [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    return $normalized.ToLowerInvariant()
}

function Test-IssueDependsTransitively {
    param(
        [hashtable]$IssueByKey,
        [string]$Key,
        [string]$PotentialAncestor,
        [hashtable]$Visited = $null
    )

    if ($null -eq $Visited) {
        $Visited = @{}
    }
    if ($Visited.ContainsKey($Key)) {
        return $false
    }
    $Visited[$Key] = $true

    if (-not $IssueByKey.ContainsKey($Key)) {
        return $false
    }

    foreach ($dependencyKeyValue in @(ConvertTo-NormalizedArray $IssueByKey[$Key].dependencyKeys)) {
        $dependencyKey = [string]$dependencyKeyValue
        if ($dependencyKey -eq $PotentialAncestor) {
            return $true
        }
        if (Test-IssueDependsTransitively `
                -IssueByKey $IssueByKey `
                -Key $dependencyKey `
                -PotentialAncestor $PotentialAncestor `
                -Visited $Visited) {
            return $true
        }
    }

    return $false
}

function Get-SharedOwnedPaths {
    param(
        [object]$Left,
        [object]$Right
    )

    $leftPaths = @{}
    foreach ($pathValue in @(ConvertTo-NormalizedArray $Left.ownedPaths)) {
        $path = Get-NormalizedOwnedPath -Path ([string]$pathValue)
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $leftPaths[$path] = $true
        }
    }

    $shared = New-Object System.Collections.Generic.List[string]
    foreach ($pathValue in @(ConvertTo-NormalizedArray $Right.ownedPaths)) {
        $path = Get-NormalizedOwnedPath -Path ([string]$pathValue)
        if (-not [string]::IsNullOrWhiteSpace($path) -and
            $leftPaths.ContainsKey($path) -and
            -not $shared.Contains($path)) {
            $shared.Add($path)
        }
    }

    return $shared.ToArray()
}

function Test-IssuesShareCollisionScope {
    param(
        [object]$Left,
        [object]$Right
    )

    $leftLane = [string](Get-ObjectProperty -Object $Left -Name "collisionLane" -DefaultValue "")
    $rightLane = [string](Get-ObjectProperty -Object $Right -Name "collisionLane" -DefaultValue "")
    if (-not [string]::IsNullOrWhiteSpace($leftLane) -and
        $leftLane -eq $rightLane) {
        return $true
    }

    return @(Get-SharedOwnedPaths -Left $Left -Right $Right).Count -gt 0
}

function Test-IsExclusiveOwnedPath {
    param(
        [string]$Path,
        [string]$LeftMilestone,
        [string]$RightMilestone
    )

    if ($LeftMilestone -eq "M00" -and $RightMilestone -eq "M00") {
        return $true
    }

    return -not (
        $Path.StartsWith("docs/", [System.StringComparison]::Ordinal) -or
        $Path.StartsWith("artifacts/", [System.StringComparison]::Ordinal) -or
        $Path.StartsWith("done/", [System.StringComparison]::Ordinal))
}

function Test-ClosureEvidenceVerified {
    param(
        [hashtable]$ClosureEvidenceByKey,
        [string]$Key
    )

    return $null -ne $ClosureEvidenceByKey -and
        $ClosureEvidenceByKey.ContainsKey($Key) -and
        [bool]$ClosureEvidenceByKey[$Key]
}

function Initialize-ClosureEvidenceState {
    param(
        [object[]]$ManifestIssues,
        [hashtable]$RemoteState
    )

    $requested = 0
    $verified = 0
    foreach ($issue in $ManifestIssues) {
        $key = [string]$issue.key
        $RemoteState.closureEvidenceByKey[$key] = $false
        if ([string]$issue.desiredState -ne "closed" -or
            -not $RemoteState.issueByKey.ContainsKey($key)) {
            continue
        }

        $requested++
        $remote = $RemoteState.issueByKey[$key]
        $number = [int](Get-ObjectProperty -Object $remote -Name "number" -DefaultValue 0)
        if ($number -le 0) {
            continue
        }

        $expectedMarker = Get-ClosureEvidenceMarker -Key $key
        $comments = @(Get-GiteaCollection -Path "$($RemoteState.repositoryPath)/issues/$number/comments")
        foreach ($comment in $comments) {
            $body = [string](Get-ObjectProperty -Object $comment -Name "body" -DefaultValue "")
            if ($body.Contains($expectedMarker)) {
                $RemoteState.closureEvidenceByKey[$key] = $true
                $verified++
                break
            }
        }
    }

    Write-SyncLine "SYNC_CLOSURE_EVIDENCE requested=$requested verified=$verified marker=clros-evidence-reviewed"
}

function Test-Manifest {
    param([object]$Manifest)

    $requiredTopLevel = @("schemaVersion", "project", "target", "policy", "milestones", "labels", "issues")
    foreach ($name in $requiredTopLevel) {
        if ($null -eq $Manifest.PSObject.Properties[$name]) {
            throw "Manifest is missing top-level field '$name'."
        }
    }

    if ([int]$Manifest.schemaVersion -ne 1) {
        throw "Unsupported manifest schemaVersion '$($Manifest.schemaVersion)'."
    }
    if ([string]$Manifest.target.baseUrl -ne $script:ExpectedBaseUrl -or
        [string]$Manifest.target.owner -ne $script:ExpectedOwner -or
        [string]$Manifest.target.repository -ne $script:ExpectedRepository) {
        throw "Manifest target must be exactly $($script:ExpectedBaseUrl)/$($script:ExpectedOwner)/$($script:ExpectedRepository)."
    }
    if ([string](Get-ObjectProperty -Object $Manifest.policy -Name "closureEvidenceMarkerFormat" -DefaultValue "") -ne
        $script:ClosureEvidenceMarkerFormat) {
        throw "Manifest policy closureEvidenceMarkerFormat must be exactly '$($script:ClosureEvidenceMarkerFormat)'."
    }

    $milestones = @(ConvertTo-NormalizedArray $Manifest.milestones)
    $labels = @(ConvertTo-NormalizedArray $Manifest.labels)
    $issues = @(ConvertTo-NormalizedArray $Manifest.issues)
    if ($milestones.Count -ne 16) {
        throw "Manifest must contain exactly 16 milestones; found $($milestones.Count)."
    }
    if ($issues.Count -ne 319) {
        throw "Manifest must contain 16 epics plus 303 children; found $($issues.Count)."
    }

    $milestoneByKey = @{}
    $milestoneTitleSet = @{}
    foreach ($milestone in $milestones) {
        $key = [string]$milestone.key
        if ($key -notmatch '^M(?:0[0-9]|1[0-5])$') {
            throw "Invalid milestone key '$key'."
        }
        if ($milestoneByKey.ContainsKey($key)) {
            throw "Duplicate milestone key '$key'."
        }
        if ($milestoneTitleSet.ContainsKey([string]$milestone.title)) {
            throw "Duplicate milestone title '$($milestone.title)'."
        }
        $milestoneByKey[$key] = $milestone
        $milestoneTitleSet[[string]$milestone.title] = $true
    }

    $labelByName = @{}
    foreach ($label in $labels) {
        $name = [string]$label.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            throw "A label has an empty name."
        }
        if ($labelByName.ContainsKey($name)) {
            throw "Duplicate label '$name'."
        }
        if ([string]$label.color -notmatch '^[0-9a-fA-F]{6}$') {
            throw "Label '$name' has invalid six-digit color '$($label.color)'."
        }
        $labelByName[$name] = $label
    }

    $requiredLabels = @(
        "agent:ready", "agent:claimed", "agent:blocked", "agent:review", "agent:done",
        "priority:p0", "priority:p1", "priority:p2", "priority:p3",
        "type:epic", "type:implementation", "type:test", "type:tooling", "type:docs", "type:research",
        "evidence:host", "evidence:qemu", "evidence:libvirt", "evidence:aws", "evidence:azure"
    )
    foreach ($requiredLabel in $requiredLabels) {
        if (-not $labelByName.ContainsKey($requiredLabel)) {
            throw "Manifest is missing required label '$requiredLabel'."
        }
    }

    $requiredIssueFields = @(
        "key", "title", "milestone", "parentKey", "dependencyKeys", "labels", "priority",
        "ownedPaths", "forbiddenPaths", "collisionLane", "summary", "currentEvidence",
        "implementationNotes", "referenceInfluences", "acceptanceCriteria", "requiredMarkers",
        "requiredCommands", "documentationPaths", "evidenceBoundary", "desiredState"
    )
    $issueByKey = @{}
    foreach ($issue in $issues) {
        foreach ($field in $requiredIssueFields) {
            if ($null -eq $issue.PSObject.Properties[$field]) {
                throw "Issue '$($issue.key)' is missing required field '$field'."
            }
        }

        $key = ([string]$issue.key).ToUpperInvariant()
        if ($key -notmatch '^M(?:0[0-9]|1[0-5])-(?:EPIC|[0-9]{3})$') {
            throw "Invalid issue key '$key'."
        }
        if ($issueByKey.ContainsKey($key)) {
            throw "Duplicate issue key '$key'."
        }
        $issueByKey[$key] = $issue

        $milestoneKey = [string]$issue.milestone
        if (-not $milestoneByKey.ContainsKey($milestoneKey)) {
            throw "Issue '$key' references unknown milestone '$milestoneKey'."
        }
        if (-not $key.StartsWith("$milestoneKey-", [System.StringComparison]::Ordinal)) {
            throw "Issue '$key' does not belong to its declared milestone '$milestoneKey'."
        }
        foreach ($labelName in @(ConvertTo-NormalizedArray $issue.labels)) {
            if (-not $labelByName.ContainsKey([string]$labelName)) {
                throw "Issue '$key' references unknown label '$labelName'."
            }
        }
        if ([string]$issue.desiredState -notin @("open", "closed", "reopen")) {
            throw "Issue '$key' has invalid desiredState '$($issue.desiredState)'."
        }
        if (@(ConvertTo-NormalizedArray $issue.acceptanceCriteria).Count -eq 0 -or
            @(ConvertTo-NormalizedArray $issue.requiredCommands).Count -eq 0 -or
            [string]::IsNullOrWhiteSpace([string]$issue.evidenceBoundary)) {
            throw "Issue '$key' does not contain implementation-ready acceptance, commands, and evidence boundary."
        }

        $ownedPathSet = @{}
        foreach ($ownedPathValue in @(ConvertTo-NormalizedArray $issue.ownedPaths)) {
            $ownedPath = Get-NormalizedOwnedPath -Path ([string]$ownedPathValue)
            if ([string]::IsNullOrWhiteSpace($ownedPath)) {
                throw "Issue '$key' contains an empty owned path."
            }
            if ([IO.Path]::IsPathRooted([string]$ownedPathValue) -or
                $ownedPath -eq ".." -or
                $ownedPath.StartsWith("../", [System.StringComparison]::Ordinal) -or
                $ownedPath.Contains("/../")) {
                throw "Issue '$key' contains non-repository owned path '$ownedPathValue'."
            }
            if ($ownedPathSet.ContainsKey($ownedPath)) {
                throw "Issue '$key' contains duplicate owned path '$ownedPathValue'."
            }
            $ownedPathSet[$ownedPath] = $true
        }
    }

    $expectedChildren = @{
        M00 = 22; M01 = 15; M02 = 16; M03 = 11; M04 = 23; M05 = 21; M06 = 19; M07 = 25
        M08 = 20; M09 = 30; M10 = 14; M11 = 18; M12 = 18; M13 = 18; M14 = 18; M15 = 15
    }
    foreach ($milestoneKey in $expectedChildren.Keys) {
        $epicKey = "$milestoneKey-EPIC"
        if (-not $issueByKey.ContainsKey($epicKey)) {
            throw "Manifest is missing epic '$epicKey'."
        }
        for ($number = 1; $number -le [int]$expectedChildren[$milestoneKey]; $number++) {
            $childKey = "{0}-{1:D3}" -f $milestoneKey, $number
            if (-not $issueByKey.ContainsKey($childKey)) {
                throw "Manifest is missing child '$childKey'."
            }
        }
    }

    foreach ($issue in $issues) {
        $key = ([string]$issue.key).ToUpperInvariant()
        $parentKey = [string](Get-ObjectProperty -Object $issue -Name "parentKey" -DefaultValue "")
        $expectedParentKey = "$($issue.milestone)-EPIC"
        if ($key.EndsWith("-EPIC", [System.StringComparison]::Ordinal)) {
            if (-not [string]::IsNullOrWhiteSpace($parentKey)) {
                throw "Epic '$key' must not declare parent '$parentKey'."
            }
        }
        elseif ($parentKey -ne $expectedParentKey) {
            throw "Issue '$key' must declare milestone epic '$expectedParentKey' as its parent."
        }
        if (-not [string]::IsNullOrWhiteSpace($parentKey) -and -not $issueByKey.ContainsKey($parentKey)) {
            throw "Issue '$key' references unknown parent '$parentKey'."
        }
        foreach ($dependencyKey in @(ConvertTo-NormalizedArray $issue.dependencyKeys)) {
            $dependency = [string]$dependencyKey
            if ($dependency -eq $key) {
                throw "Issue '$key' depends on itself."
            }
            if (-not $issueByKey.ContainsKey($dependency)) {
                throw "Issue '$key' references unknown dependency '$dependency'."
            }
        }
    }

    $dependencyClosureByKey = @{}
    foreach ($issue in $issues) {
        $key = ([string]$issue.key).ToUpperInvariant()
        $closure = @{}
        $pending = New-Object System.Collections.Generic.Stack[string]
        foreach ($dependencyKeyValue in @(ConvertTo-NormalizedArray $issue.dependencyKeys)) {
            $pending.Push([string]$dependencyKeyValue)
        }
        while ($pending.Count -gt 0) {
            $dependencyKey = $pending.Pop()
            if ($closure.ContainsKey($dependencyKey)) {
                continue
            }
            $closure[$dependencyKey] = $true
            if (-not $issueByKey.ContainsKey($dependencyKey)) {
                continue
            }
            foreach ($nestedDependencyValue in @(ConvertTo-NormalizedArray $issueByKey[$dependencyKey].dependencyKeys)) {
                $pending.Push([string]$nestedDependencyValue)
            }
        }
        if ($closure.ContainsKey($key)) {
            throw "Issue dependency graph contains a cycle reachable from '$key'."
        }
        $dependencyClosureByKey[$key] = $closure
    }

    $implementationIssues = @($issues | Where-Object {
        -not ([string]$_.key).EndsWith("-EPIC", [System.StringComparison]::Ordinal)
    })
    $ownersByPath = @{}
    foreach ($issue in $implementationIssues) {
        foreach ($ownedPathValue in @(ConvertTo-NormalizedArray $issue.ownedPaths)) {
            $ownedPath = Get-NormalizedOwnedPath -Path ([string]$ownedPathValue)
            if (-not $ownersByPath.ContainsKey($ownedPath)) {
                $ownersByPath[$ownedPath] = New-Object System.Collections.Generic.List[object]
            }
            $ownersByPath[$ownedPath].Add($issue)
        }
    }
    foreach ($ownedPath in $ownersByPath.Keys) {
        $owners = $ownersByPath[$ownedPath].ToArray()
        if ($owners.Count -lt 2) {
            continue
        }
        for ($leftIndex = 0; $leftIndex -lt $owners.Count; $leftIndex++) {
            $left = $owners[$leftIndex]
            for ($rightIndex = $leftIndex + 1; $rightIndex -lt $owners.Count; $rightIndex++) {
                $right = $owners[$rightIndex]
                $serializedByDependency =
                    $dependencyClosureByKey[[string]$left.key].ContainsKey([string]$right.key) -or
                    $dependencyClosureByKey[[string]$right.key].ContainsKey([string]$left.key)
                $leftLane = [string](Get-ObjectProperty -Object $left -Name "collisionLane" -DefaultValue "")
                $rightLane = [string](Get-ObjectProperty -Object $right -Name "collisionLane" -DefaultValue "")
                $serializedByLane =
                    -not [string]::IsNullOrWhiteSpace($leftLane) -and
                    $leftLane -eq $rightLane
                if ($serializedByDependency -or $serializedByLane) {
                    continue
                }

                if (Test-IsExclusiveOwnedPath `
                        -Path $ownedPath `
                        -LeftMilestone ([string]$left.milestone) `
                        -RightMilestone ([string]$right.milestone)) {
                    throw "Issues '$($left.key)' and '$($right.key)' have unserialized owned-path overlap '$ownedPath'. Add a dependency or a shared collision lane."
                }
            }
        }
    }

    if (-not $issueByKey.ContainsKey("M01-001")) {
        throw "Manifest is missing M01-001 milestone entry gate."
    }
    for ($number = 1; $number -le 22; $number++) {
        $requiredRecoveryKey = "M00-{0:D3}" -f $number
        if (-not $dependencyClosureByKey["M01-001"].ContainsKey($requiredRecoveryKey)) {
            throw "M01-001 must remain transitively blocked by recovery issue '$requiredRecoveryKey'."
        }
    }

    return @{
        milestones = $milestones
        labels = $labels
        issues = $issues
        milestoneByKey = $milestoneByKey
        labelByName = $labelByName
        issueByKey = $issueByKey
    }
}

function Get-RemoteState {
    $repositoryPath = "/repos/$([Uri]::EscapeDataString($Owner))/$([Uri]::EscapeDataString($Repository))"
    $labels = @(Get-GiteaCollection -Path "$repositoryPath/labels")
    $milestones = @(Get-GiteaCollection -Path "$repositoryPath/milestones?state=all")
    $issues = @(Get-GiteaCollection -Path "$repositoryPath/issues?state=all&type=issues")

    $labelByName = @{}
    foreach ($label in $labels) {
        $labelByName[[string]$label.name] = $label
    }
    $milestoneByTitle = @{}
    foreach ($milestone in $milestones) {
        $milestoneByTitle[[string]$milestone.title] = $milestone
    }

    $issueByKey = @{}
    foreach ($issue in $issues) {
        if ($null -ne (Get-ObjectProperty -Object $issue -Name "pull_request")) {
            continue
        }
        $body = [string](Get-ObjectProperty -Object $issue -Name "body" -DefaultValue "")
        if ([string]::IsNullOrWhiteSpace($body)) {
            $index = [int](Get-ObjectProperty -Object $issue -Name "number" -DefaultValue 0)
            if ($index -gt 0) {
                $issue = Invoke-GiteaApi -Method GET -Path "$repositoryPath/issues/$index"
                $body = [string](Get-ObjectProperty -Object $issue -Name "body" -DefaultValue "")
            }
        }
        $key = Get-HiddenIssueKey -Body $body
        if ([string]::IsNullOrWhiteSpace($key)) {
            continue
        }
        if ($issueByKey.ContainsKey($key)) {
            throw "Remote tracker contains duplicate hidden key '$key' on more than one issue."
        }
        $issueByKey[$key] = $issue
    }

    return @{
        repositoryPath = $repositoryPath
        labels = $labels
        milestones = $milestones
        issues = $issues
        labelByName = $labelByName
        milestoneByTitle = $milestoneByTitle
        issueByKey = $issueByKey
        closureEvidenceByKey = @{}
    }
}

function Get-RemoteIssueNumber {
    param(
        [hashtable]$RemoteIssueByKey,
        [string]$Key
    )

    if (-not $RemoteIssueByKey.ContainsKey($Key)) {
        return 0
    }

    return [int](Get-ObjectProperty -Object $RemoteIssueByKey[$Key] -Name "number" -DefaultValue 0)
}

function Convert-ListToMarkdown {
    param(
        [object[]]$Items,
        [string]$EmptyText = "None."
    )

    $array = @(ConvertTo-NormalizedArray $Items)
    if ($array.Count -eq 0) {
        return $EmptyText
    }

    return (($array | ForEach-Object { "- $_" }) -join "`n")
}

function Render-ManagedIssueBody {
    param(
        [object]$Issue,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$RemoteIssueByKey
    )

    $key = [string]$Issue.key
    $parentText = "None (milestone epic)."
    $parentKey = [string](Get-ObjectProperty -Object $Issue -Name "parentKey" -DefaultValue "")
    if (-not [string]::IsNullOrWhiteSpace($parentKey)) {
        $parentNumber = Get-RemoteIssueNumber -RemoteIssueByKey $RemoteIssueByKey -Key $parentKey
        if ($parentNumber -gt 0) {
            $parentText = "#$parentNumber (key $parentKey)"
        }
        else {
            $parentText = "$parentKey (will resolve after creation)"
        }
    }

    $dependencyLines = New-Object System.Collections.Generic.List[string]
    foreach ($dependencyKey in @(ConvertTo-NormalizedArray $Issue.dependencyKeys)) {
        $dependencyNumber = Get-RemoteIssueNumber -RemoteIssueByKey $RemoteIssueByKey -Key ([string]$dependencyKey)
        if ($dependencyNumber -gt 0) {
            $dependencyLines.Add("- [ ] #$dependencyNumber (key $dependencyKey)")
        }
        else {
            $dependencyLines.Add("- [ ] $dependencyKey (will resolve after creation)")
        }
    }
    if ($dependencyLines.Count -eq 0) {
        $dependencyLines.Add("- None; this slice is dependency-free.")
    }

    $childLines = New-Object System.Collections.Generic.List[string]
    if ($key.EndsWith("-EPIC", [System.StringComparison]::Ordinal)) {
        $optionalChildKeys = @("M08-020", "M12-017", "M13-017", "M15-010", "M15-011", "M15-012", "M15-013")
        $childIssues = @($ManifestIssueByKey.Values | Where-Object { [string]$_.parentKey -eq $key } | Sort-Object key)
        foreach ($child in $childIssues) {
            $optionalTag = ""
            if ($optionalChildKeys -contains [string]$child.key) {
                $optionalTag = " [OPTIONAL]"
            }
            $childNumber = Get-RemoteIssueNumber -RemoteIssueByKey $RemoteIssueByKey -Key ([string]$child.key)
            if ($childNumber -gt 0) {
                $childLines.Add("- [ ] #$childNumber (key $($child.key))$optionalTag - $($child.summary)")
            }
            else {
                $childLines.Add("- [ ] $($child.key)$optionalTag - $($child.summary) (will resolve after creation)")
            }
        }
    }

    $referenceLines = New-Object System.Collections.Generic.List[string]
    foreach ($reference in @(ConvertTo-NormalizedArray $Issue.referenceInfluences)) {
        $referenceLines.Add("- **$($reference.use)** $($reference.path) - $($reference.notes)")
    }
    if ($referenceLines.Count -eq 0) {
        $referenceLines.Add("- None. Implement from CLROS contracts and authoritative specifications.")
    }

    $collisionLane = [string](Get-ObjectProperty -Object $Issue -Name "collisionLane" -DefaultValue "")
    if ([string]::IsNullOrWhiteSpace($collisionLane)) {
        $collisionLane = "None; dependencies and owned paths still apply."
    }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine($script:ManagedStart)
    [void]$builder.AppendLine("## Summary")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine([string]$Issue.summary)
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Coordination")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- **Manifest key:** ``$key``")
    [void]$builder.AppendLine("- **Parent:** $parentText")
    [void]$builder.AppendLine("- **Milestone:** ``$($Issue.milestone)``")
    [void]$builder.AppendLine("- **Priority:** ``$($Issue.priority)``")
    [void]$builder.AppendLine("- **Collision lane:** ``$collisionLane``")
    [void]$builder.AppendLine("- **Desired state:** ``$($Issue.desiredState)``")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("### Declared managed labels")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.labels))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("### Dependencies")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine(($dependencyLines -join "`n"))
    if ($childLines.Count -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine("### Child checklist")
        [void]$builder.AppendLine()
        [void]$builder.AppendLine(($childLines -join "`n"))
    }
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Current evidence")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine([string]$Issue.currentEvidence)
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Implementation notes")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.implementationNotes))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Reference influences")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine(($referenceLines -join "`n"))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## File ownership")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("### Owned paths")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.ownedPaths))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("### Forbidden paths")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.forbiddenPaths))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Acceptance criteria")
    [void]$builder.AppendLine()
    $criterionNumber = 1
    foreach ($criterion in @(ConvertTo-NormalizedArray $Issue.acceptanceCriteria)) {
        [void]$builder.AppendLine("$criterionNumber. $criterion")
        $criterionNumber++
    }
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Stable markers")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.requiredMarkers))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Required commands")
    [void]$builder.AppendLine()
    foreach ($command in @(ConvertTo-NormalizedArray $Issue.requiredCommands)) {
        [void]$builder.AppendLine("- ``$command``")
    }
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Canonical documentation")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine((Convert-ListToMarkdown -Items $Issue.documentationPaths))
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Evidence boundary")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine([string]$Issue.evidenceBoundary)
    [void]$builder.AppendLine()
    $closureEvidenceMarker = Get-ClosureEvidenceMarker -Key $key
    [void]$builder.AppendLine("Before closure, reserve a unique ``done/stepNN.md`` and post an evidence-review comment containing ``$closureEvidenceMarker`` plus exact commands, logs, hashes, failures, deferred work, and this boundary.")
    [void]$builder.Append($script:ManagedEnd)
    return $builder.ToString()
}

function Merge-ManagedIssueBody {
    param(
        [string]$ExistingBody,
        [string]$Key,
        [string]$ManagedBody
    )

    $hiddenKey = "<!-- clros-issue-key:$Key -->"
    if ([string]::IsNullOrWhiteSpace($ExistingBody)) {
        return "$hiddenKey`n$ManagedBody"
    }

    $body = $ExistingBody
    $keyPattern = '<!--\s*clros-issue-key:[A-Z0-9-]+\s*-->'
    if ([regex]::IsMatch($body, $keyPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $body = [regex]::Replace(
            $body,
            $keyPattern,
            [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $hiddenKey },
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
        )
    }
    else {
        $body = "$hiddenKey`n$body"
    }

    $managedPattern = '(?s)<!-- clros-managed-start -->.*?<!-- clros-managed-end -->'
    if ([regex]::IsMatch($body, $managedPattern)) {
        return [regex]::Replace(
            $body,
            $managedPattern,
            [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $ManagedBody },
            [System.Text.RegularExpressions.RegexOptions]::None
        )
    }

    return "$($body.TrimEnd())`n`n$ManagedBody"
}

function Get-RemoteLabelNames {
    param([object]$RemoteIssue)

    $names = New-Object System.Collections.Generic.List[string]
    foreach ($label in @(ConvertTo-NormalizedArray (Get-ObjectProperty -Object $RemoteIssue -Name "labels" -DefaultValue @()))) {
        $name = [string](Get-ObjectProperty -Object $label -Name "name" -DefaultValue "")
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $names.Add($name)
        }
    }
    return $names.ToArray()
}

function Test-ManifestIssueHasLabel {
    param(
        [object]$ManifestIssue,
        [string]$Label
    )

    foreach ($value in @(ConvertTo-NormalizedArray $ManifestIssue.labels)) {
        if ([string]$value -eq $Label) {
            return $true
        }
    }

    return $false
}

function Get-ClosurePrerequisiteFailure {
    param(
        [string]$Key,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$RemoteIssueByKey,
        [hashtable]$ClosureEvidenceByKey,
        [bool]$AllowPendingRemoteClosure,
        [hashtable]$Visiting
    )

    if ($Visiting.ContainsKey($Key)) {
        return "closure prerequisite cycle includes '$Key'"
    }
    if (-not $ManifestIssueByKey.ContainsKey($Key)) {
        return "closure prerequisite '$Key' is absent from the manifest"
    }

    $issue = $ManifestIssueByKey[$Key]
    if ([string]$issue.desiredState -ne "closed") {
        return "closure prerequisite '$Key' lacks manifest closed intent"
    }
    if (-not (Test-ClosureEvidenceVerified `
            -ClosureEvidenceByKey $ClosureEvidenceByKey `
            -Key $Key)) {
        return "closure prerequisite '$Key' lacks reviewed evidence"
    }
    if (-not $AllowPendingRemoteClosure) {
        if (-not $RemoteIssueByKey.ContainsKey($Key)) {
            return "closure prerequisite '$Key' is absent from the tracker"
        }
        $remoteState = [string](Get-ObjectProperty `
            -Object $RemoteIssueByKey[$Key] `
            -Name "state" `
            -DefaultValue "open")
        if ($remoteState -ne "closed") {
            return "closure prerequisite '$Key' is not closed in the tracker"
        }
    }

    $Visiting[$Key] = $true
    try {
        foreach ($dependencyValue in @(
                ConvertTo-NormalizedArray $issue.dependencyKeys)) {
            $dependencyKey = [string]$dependencyValue
            $failure = Get-ClosurePrerequisiteFailure `
                -Key $dependencyKey `
                -ManifestIssueByKey $ManifestIssueByKey `
                -RemoteIssueByKey $RemoteIssueByKey `
                -ClosureEvidenceByKey $ClosureEvidenceByKey `
                -AllowPendingRemoteClosure $AllowPendingRemoteClosure `
                -Visiting $Visiting
            if ($null -ne $failure) {
                return $failure
            }
        }

        if (Test-ManifestIssueHasLabel `
                -ManifestIssue $issue `
                -Label "type:epic") {
            foreach ($candidate in $ManifestIssueByKey.Values) {
                if ([string](Get-ObjectProperty `
                        -Object $candidate `
                        -Name "parentKey" `
                        -DefaultValue "") -ne $Key) {
                    continue
                }

                $childFailure = Get-ClosurePrerequisiteFailure `
                    -Key ([string]$candidate.key) `
                    -ManifestIssueByKey $ManifestIssueByKey `
                    -RemoteIssueByKey $RemoteIssueByKey `
                    -ClosureEvidenceByKey $ClosureEvidenceByKey `
                    -AllowPendingRemoteClosure $AllowPendingRemoteClosure `
                    -Visiting $Visiting
                if ($null -ne $childFailure) {
                    return $childFailure
                }
            }
        }
    }
    finally {
        $Visiting.Remove($Key) | Out-Null
    }

    return $null
}

function Assert-IssueClosureAllowed {
    param(
        [object]$ManifestIssue,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$RemoteIssueByKey,
        [hashtable]$ClosureEvidenceByKey,
        [bool]$AllowPendingRemoteClosure
    )

    $key = [string]$ManifestIssue.key
    if (-not (Test-ClosureEvidenceVerified `
            -ClosureEvidenceByKey $ClosureEvidenceByKey `
            -Key $key)) {
        $requiredMarker = Get-ClosureEvidenceMarker -Key $key
        throw "Closure for '$key' requires an evidence-review comment containing '$requiredMarker'."
    }

    foreach ($dependencyValue in @(
            ConvertTo-NormalizedArray $ManifestIssue.dependencyKeys)) {
        $dependencyKey = [string]$dependencyValue
        $failure = Get-ClosurePrerequisiteFailure `
            -Key $dependencyKey `
            -ManifestIssueByKey $ManifestIssueByKey `
            -RemoteIssueByKey $RemoteIssueByKey `
            -ClosureEvidenceByKey $ClosureEvidenceByKey `
            -AllowPendingRemoteClosure $AllowPendingRemoteClosure `
            -Visiting @{}
        if ($null -ne $failure) {
            throw "Refusing to close '$key': $failure."
        }
    }

    if (Test-ManifestIssueHasLabel `
            -ManifestIssue $ManifestIssue `
            -Label "type:epic") {
        foreach ($candidate in $ManifestIssueByKey.Values) {
            if ([string](Get-ObjectProperty `
                    -Object $candidate `
                    -Name "parentKey" `
                    -DefaultValue "") -ne $key) {
                continue
            }

            $failure = Get-ClosurePrerequisiteFailure `
                -Key ([string]$candidate.key) `
                -ManifestIssueByKey $ManifestIssueByKey `
                -RemoteIssueByKey $RemoteIssueByKey `
                -ClosureEvidenceByKey $ClosureEvidenceByKey `
                -AllowPendingRemoteClosure $AllowPendingRemoteClosure `
                -Visiting @{}
            if ($null -ne $failure) {
                throw "Refusing to close epic '$key': $failure."
            }
        }
    }
}

function Get-IssueUpdateOrder {
    param(
        [object[]]$ManifestIssues
    )

    $pendingClosures = @{}
    foreach ($issue in $ManifestIssues) {
        if ([string]$issue.desiredState -eq "closed") {
            $pendingClosures[[string]$issue.key] = $issue
        }
    }

    $ordered = New-Object System.Collections.Generic.List[object]
    while ($pendingClosures.Count -gt 0) {
        $progress = $false
        foreach ($issue in $ManifestIssues) {
            $key = [string]$issue.key
            if (-not $pendingClosures.ContainsKey($key)) {
                continue
            }

            $waitsForPendingClosure = $false
            foreach ($dependencyValue in @(
                    ConvertTo-NormalizedArray $issue.dependencyKeys)) {
                if ($pendingClosures.ContainsKey([string]$dependencyValue)) {
                    $waitsForPendingClosure = $true
                    break
                }
            }
            if (-not $waitsForPendingClosure -and
                (Test-ManifestIssueHasLabel `
                    -ManifestIssue $issue `
                    -Label "type:epic")) {
                foreach ($candidate in $ManifestIssues) {
                    if ([string](Get-ObjectProperty `
                            -Object $candidate `
                            -Name "parentKey" `
                            -DefaultValue "") -eq $key -and
                        $pendingClosures.ContainsKey(
                            [string]$candidate.key)) {
                        $waitsForPendingClosure = $true
                        break
                    }
                }
            }
            if ($waitsForPendingClosure) {
                continue
            }

            $ordered.Add($issue)
            $pendingClosures.Remove($key)
            $progress = $true
        }

        if (-not $progress) {
            $sample = @($pendingClosures.Keys | Sort-Object |
                Select-Object -First 20) -join ","
            throw "Could not order desired closures; unresolved dependency or parent cycle: $sample"
        }
    }

    foreach ($issue in $ManifestIssues) {
        if ([string]$issue.desiredState -ne "closed") {
            $ordered.Add($issue)
        }
    }

    return $ordered.ToArray()
}

function Resolve-AgentStateLabel {
    param(
        [object]$ManifestIssue,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$RemoteIssueByKey,
        [hashtable]$ClosureEvidenceByKey
    )

    $key = [string]$ManifestIssue.key
    $desiredState = [string]$ManifestIssue.desiredState
    $remoteIssue = $null
    if ($RemoteIssueByKey.ContainsKey($key)) {
        $remoteIssue = $RemoteIssueByKey[$key]
    }

    if ($desiredState -eq "closed") {
        if ($null -ne $remoteIssue -and
            (Test-ClosureEvidenceVerified -ClosureEvidenceByKey $ClosureEvidenceByKey -Key $key)) {
            return "agent:done"
        }
        return "agent:review"
    }

    if ($null -ne $remoteIssue) {
        $remoteState = [string](Get-ObjectProperty -Object $remoteIssue -Name "state" -DefaultValue "open")
        if ($remoteState -eq "closed" -and $desiredState -ne "reopen") {
            # A remote-only closure is not evidence and must not promote either
            # this issue or any dependent issue.
            return "agent:blocked"
        }

        $currentNames = @(Get-RemoteLabelNames -RemoteIssue $remoteIssue)
        foreach ($sticky in @("agent:claimed", "agent:review")) {
            if ($currentNames -contains $sticky) {
                return $sticky
            }
        }
    }

    $declaredNames = @($ManifestIssue.labels | ForEach-Object { [string]$_ })
    if ($declaredNames -contains "agent:review") {
        return "agent:review"
    }
    if ($declaredNames -contains "agent:claimed") {
        return "agent:claimed"
    }
    if ($declaredNames -contains "type:epic") {
        return "agent:blocked"
    }

    foreach ($dependencyValue in @(
            ConvertTo-NormalizedArray $ManifestIssue.dependencyKeys)) {
        $dependencyFailure = Get-ClosurePrerequisiteFailure `
            -Key ([string]$dependencyValue) `
            -ManifestIssueByKey $ManifestIssueByKey `
            -RemoteIssueByKey $RemoteIssueByKey `
            -ClosureEvidenceByKey $ClosureEvidenceByKey `
            -AllowPendingRemoteClosure $false `
            -Visiting @{}
        if ($null -ne $dependencyFailure) {
            return "agent:blocked"
        }
    }

    foreach ($other in $ManifestIssueByKey.Values) {
        if ([string]$other.key -eq $key -or
            -not (Test-IssuesShareCollisionScope -Left $ManifestIssue -Right $other)) {
            continue
        }
        $otherKey = [string]$other.key
        if (-not $RemoteIssueByKey.ContainsKey($otherKey)) {
            continue
        }
        $otherRemote = $RemoteIssueByKey[$otherKey]
        if ([string](Get-ObjectProperty -Object $otherRemote -Name "state" -DefaultValue "open") -ne "open") {
            continue
        }
        $otherAgentLabels = @(Get-RemoteLabelNames -RemoteIssue $otherRemote)
        if ($otherAgentLabels -contains "agent:claimed" -or
            $otherAgentLabels -contains "agent:review") {
            return "agent:blocked"
        }
    }

    return "agent:ready"
}

function Get-DesiredIssueLabelNames {
    param(
        [object]$ManifestIssue,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$RemoteIssueByKey,
        [hashtable]$ClosureEvidenceByKey,
        [hashtable]$ManagedLabelNames
    )

    $desired = New-Object System.Collections.Generic.List[string]
    $agentState = Resolve-AgentStateLabel `
        -ManifestIssue $ManifestIssue `
        -ManifestIssueByKey $ManifestIssueByKey `
        -RemoteIssueByKey $RemoteIssueByKey `
        -ClosureEvidenceByKey $ClosureEvidenceByKey
    $desired.Add($agentState)
    foreach ($nameValue in @(ConvertTo-NormalizedArray $ManifestIssue.labels)) {
        $name = [string]$nameValue
        if (-not $name.StartsWith("agent:", [System.StringComparison]::Ordinal) -and -not $desired.Contains($name)) {
            $desired.Add($name)
        }
    }

    $key = [string]$ManifestIssue.key
    if ($RemoteIssueByKey.ContainsKey($key)) {
        foreach ($currentName in @(Get-RemoteLabelNames -RemoteIssue $RemoteIssueByKey[$key])) {
            if (-not $ManagedLabelNames.ContainsKey($currentName) -and -not $desired.Contains($currentName)) {
                $desired.Add($currentName)
            }
        }
    }

    return $desired.ToArray()
}

function Get-LabelIds {
    param(
        [string[]]$Names,
        [hashtable]$RemoteLabelByName
    )

    $ids = New-Object System.Collections.Generic.List[int]
    foreach ($name in $Names) {
        if (-not $RemoteLabelByName.ContainsKey($name)) {
            throw "Required remote label '$name' is unavailable."
        }
        $ids.Add([int](Get-ObjectProperty -Object $RemoteLabelByName[$name] -Name "id" -DefaultValue 0))
    }
    return $ids.ToArray()
}

function Record-PreviewAction {
    param(
        [ValidateSet("create", "update", "unchanged", "skip")]
        [string]$Action,
        [string]$Key
    )

    switch ($Action) {
        "create" { Record-SyncAction -Action wouldCreate -Key $Key }
        "update" { Record-SyncAction -Action wouldUpdate -Key $Key }
        "unchanged" { Record-SyncAction -Action unchanged -Key $Key }
        "skip" { Record-SyncAction -Action skipped -Key $Key }
    }
}

function Ensure-Labels {
    param(
        [object[]]$ManifestLabels,
        [hashtable]$RemoteState
    )

    foreach ($label in $ManifestLabels) {
        $name = [string]$label.name
        try {
            if (-not $RemoteState.labelByName.ContainsKey($name)) {
                if (-not $script:ApplyChanges) {
                    Record-PreviewAction -Action create -Key "label:$name"
                    continue
                }
                $created = Invoke-GiteaApi -Method POST -Path "$($RemoteState.repositoryPath)/labels" -Body @{
                    name = $name
                    color = [string]$label.color
                    description = [string]$label.description
                }
                $RemoteState.labelByName[$name] = $created
                Record-SyncAction -Action created -Key "label:$name"
                continue
            }

            $remote = $RemoteState.labelByName[$name]
            $remoteColor = ([string](Get-ObjectProperty -Object $remote -Name "color" -DefaultValue "")).TrimStart("#").ToLowerInvariant()
            $desiredColor = ([string]$label.color).ToLowerInvariant()
            $remoteDescription = [string](Get-ObjectProperty -Object $remote -Name "description" -DefaultValue "")
            $needsUpdate = $remoteColor -ne $desiredColor -or $remoteDescription -cne [string]$label.description
            if (-not $needsUpdate) {
                Record-SyncAction -Action unchanged -Key "label:$name"
                continue
            }
            if (-not $script:ApplyChanges) {
                Record-PreviewAction -Action update -Key "label:$name"
                continue
            }
            $id = [int](Get-ObjectProperty -Object $remote -Name "id" -DefaultValue 0)
            $updated = Invoke-GiteaApi -Method PATCH -Path "$($RemoteState.repositoryPath)/labels/$id" -Body @{
                name = $name
                color = [string]$label.color
                description = [string]$label.description
            }
            $RemoteState.labelByName[$name] = $updated
            Record-SyncAction -Action updated -Key "label:$name"
        }
        catch {
            Record-SyncAction -Action failed -Key "label:$name"
            Write-SyncLine "SYNC_ITEM_FAIL label=$name reason=$($_.Exception.Message)"
        }
    }
}

function Ensure-Milestones {
    param(
        [object[]]$ManifestMilestones,
        [hashtable]$RemoteState
    )

    foreach ($milestone in $ManifestMilestones) {
        $title = [string]$milestone.title
        try {
            if (-not $RemoteState.milestoneByTitle.ContainsKey($title)) {
                if (-not $script:ApplyChanges) {
                    Record-PreviewAction -Action create -Key "milestone:$($milestone.key)"
                    continue
                }
                $created = Invoke-GiteaApi -Method POST -Path "$($RemoteState.repositoryPath)/milestones" -Body @{
                    title = $title
                    description = [string]$milestone.description
                    state = "open"
                }
                $RemoteState.milestoneByTitle[$title] = $created
                Record-SyncAction -Action created -Key "milestone:$($milestone.key)"
                continue
            }

            $remote = $RemoteState.milestoneByTitle[$title]
            $remoteDescription = [string](Get-ObjectProperty -Object $remote -Name "description" -DefaultValue "")
            $remoteStateValue = [string](Get-ObjectProperty -Object $remote -Name "state" -DefaultValue "open")
            $needsUpdate = $remoteDescription -cne [string]$milestone.description -or $remoteStateValue -ne "open"
            if (-not $needsUpdate) {
                Record-SyncAction -Action unchanged -Key "milestone:$($milestone.key)"
                continue
            }
            if (-not $script:ApplyChanges) {
                Record-PreviewAction -Action update -Key "milestone:$($milestone.key)"
                continue
            }
            $id = [int](Get-ObjectProperty -Object $remote -Name "id" -DefaultValue 0)
            $updated = Invoke-GiteaApi -Method PATCH -Path "$($RemoteState.repositoryPath)/milestones/$id" -Body @{
                title = $title
                description = [string]$milestone.description
                state = "open"
            }
            $RemoteState.milestoneByTitle[$title] = $updated
            Record-SyncAction -Action updated -Key "milestone:$($milestone.key)"
        }
        catch {
            Record-SyncAction -Action failed -Key "milestone:$($milestone.key)"
            Write-SyncLine "SYNC_ITEM_FAIL milestone=$title reason=$($_.Exception.Message)"
        }
    }
}

function Create-MissingIssues {
    param(
        [object[]]$ManifestIssues,
        [hashtable]$ManifestMilestoneByKey,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$ManagedLabelNames,
        [hashtable]$RemoteState
    )

    foreach ($issue in $ManifestIssues) {
        $key = [string]$issue.key
        if ($RemoteState.issueByKey.ContainsKey($key)) {
            continue
        }
        if (-not $script:ApplyChanges) {
            Record-PreviewAction -Action create -Key "issue:$key"
            continue
        }

        try {
            $milestoneTitle = [string]$ManifestMilestoneByKey[[string]$issue.milestone].title
            if (-not $RemoteState.milestoneByTitle.ContainsKey($milestoneTitle)) {
                throw "Milestone '$milestoneTitle' was not created."
            }
            $labelNames = @(Get-DesiredIssueLabelNames -ManifestIssue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey -ManagedLabelNames $ManagedLabelNames)
            $labelIds = @(Get-LabelIds -Names $labelNames -RemoteLabelByName $RemoteState.labelByName)
            $managedBody = Render-ManagedIssueBody -Issue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey
            $body = Merge-ManagedIssueBody -ExistingBody "" -Key $key -ManagedBody $managedBody
            $milestoneId = [int](Get-ObjectProperty -Object $RemoteState.milestoneByTitle[$milestoneTitle] -Name "id" -DefaultValue 0)
            $created = Invoke-GiteaApi -Method POST -Path "$($RemoteState.repositoryPath)/issues" -Body @{
                title = [string]$issue.title
                body = $body
                labels = $labelIds
                milestone = $milestoneId
            }
            $RemoteState.issueByKey[$key] = $created
            Record-SyncAction -Action created -Key "issue:$key"
        }
        catch {
            Record-SyncAction -Action failed -Key "issue:$key"
            Write-SyncLine "SYNC_ITEM_FAIL issue=$key operation=create reason=$($_.Exception.Message)"
        }
    }
}

function Update-Issues {
    param(
        [object[]]$ManifestIssues,
        [hashtable]$ManifestMilestoneByKey,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$ManagedLabelNames,
        [hashtable]$RemoteState
    )

    foreach ($issue in $ManifestIssues) {
        $key = [string]$issue.key
        if (-not $RemoteState.issueByKey.ContainsKey($key)) {
            if ($script:ApplyChanges) {
                Record-SyncAction -Action skipped -Key "issue:$key"
            }
            continue
        }

        try {
            $remote = $RemoteState.issueByKey[$key]
            if ([string]$issue.desiredState -eq "closed") {
                Assert-IssueClosureAllowed `
                    -ManifestIssue $issue `
                    -ManifestIssueByKey $ManifestIssueByKey `
                    -RemoteIssueByKey $RemoteState.issueByKey `
                    -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey `
                    -AllowPendingRemoteClosure $false
            }
            $managedBody = Render-ManagedIssueBody -Issue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey
            $existingBody = [string](Get-ObjectProperty -Object $remote -Name "body" -DefaultValue "")
            $desiredBody = Merge-ManagedIssueBody -ExistingBody $existingBody -Key $key -ManagedBody $managedBody
            $desiredLabelNames = @(Get-DesiredIssueLabelNames -ManifestIssue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey -ManagedLabelNames $ManagedLabelNames)
            $currentLabelNames = @(Get-RemoteLabelNames -RemoteIssue $remote)
            $labelsNeedUpdate = -not (
                Test-StringSetEqual `
                    -Left $currentLabelNames `
                    -Right $desiredLabelNames)
            $milestoneTitle = [string]$ManifestMilestoneByKey[[string]$issue.milestone].title
            if (-not $RemoteState.milestoneByTitle.ContainsKey($milestoneTitle)) {
                throw "Milestone '$milestoneTitle' is unavailable."
            }
            $desiredMilestoneId = [int](Get-ObjectProperty -Object $RemoteState.milestoneByTitle[$milestoneTitle] -Name "id" -DefaultValue 0)
            $remoteMilestone = Get-ObjectProperty -Object $remote -Name "milestone" -DefaultValue $null
            $currentMilestoneId = [int](Get-ObjectProperty -Object $remoteMilestone -Name "id" -DefaultValue 0)
            $remoteStateValue = [string](Get-ObjectProperty -Object $remote -Name "state" -DefaultValue "open")
            $desiredStateValue = $null
            if ([string]$issue.desiredState -eq "closed" -and $remoteStateValue -ne "closed") {
                $desiredStateValue = "closed"
            }
            elseif ([string]$issue.desiredState -eq "reopen" -and $remoteStateValue -ne "open") {
                $desiredStateValue = "open"
            }

            $issueFieldsNeedUpdate =
                [string](Get-ObjectProperty -Object $remote -Name "title" -DefaultValue "") -cne [string]$issue.title -or
                $existingBody -cne $desiredBody -or
                $currentMilestoneId -ne $desiredMilestoneId -or
                $null -ne $desiredStateValue
            $needsUpdate = $issueFieldsNeedUpdate -or $labelsNeedUpdate
            if (-not $needsUpdate) {
                Record-SyncAction -Action unchanged -Key "issue:$key"
                continue
            }
            if (-not $script:ApplyChanges) {
                Record-PreviewAction -Action update -Key "issue:$key"
                continue
            }

            $number = [int](Get-ObjectProperty -Object $remote -Name "number" -DefaultValue 0)
            $updated = $remote
            if ($issueFieldsNeedUpdate) {
                # Gitea's EditIssueOption does not accept labels. Replacing
                # them through the issue PATCH is silently ignored by current
                # Gitea releases, so issue fields and labels must cross their
                # documented API boundaries separately.
                $payload = @{
                    title = [string]$issue.title
                    body = $desiredBody
                    milestone = $desiredMilestoneId
                }
                if ($null -ne $desiredStateValue) {
                    $payload.state = $desiredStateValue
                }
                $updated = Invoke-GiteaApi `
                    -Method PATCH `
                    -Path "$($RemoteState.repositoryPath)/issues/$number" `
                    -Body $payload
            }
            if ($labelsNeedUpdate) {
                $desiredLabelIds = @(
                    Get-LabelIds `
                        -Names $desiredLabelNames `
                        -RemoteLabelByName $RemoteState.labelByName)
                $replacedLabels = Invoke-GiteaApi `
                    -Method PUT `
                    -Path "$($RemoteState.repositoryPath)/issues/$number/labels" `
                    -Body @{ labels = $desiredLabelIds }
                if (@(ConvertTo-NormalizedArray $replacedLabels).Count -ne
                    $desiredLabelIds.Count) {
                    throw (
                        "Gitea returned an unexpected label count after " +
                        "replacing labels for '$key'.")
                }
                $updated = Invoke-GiteaApi `
                    -Method GET `
                    -Path "$($RemoteState.repositoryPath)/issues/$number"
            }
            $RemoteState.issueByKey[$key] = $updated
            Record-SyncAction -Action updated -Key "issue:$key"
        }
        catch {
            Record-SyncAction -Action failed -Key "issue:$key"
            Write-SyncLine "SYNC_ITEM_FAIL issue=$key operation=update reason=$($_.Exception.Message)"
        }
    }
}

function Preview-Issues {
    param(
        [object[]]$ManifestIssues,
        [hashtable]$ManifestMilestoneByKey,
        [hashtable]$ManifestIssueByKey,
        [hashtable]$ManagedLabelNames,
        [hashtable]$RemoteState
    )

    foreach ($issue in $ManifestIssues) {
        $key = [string]$issue.key
        if (-not $RemoteState.issueByKey.ContainsKey($key)) {
            continue
        }

        try {
            $remote = $RemoteState.issueByKey[$key]
            if ([string]$issue.desiredState -eq "closed") {
                Assert-IssueClosureAllowed `
                    -ManifestIssue $issue `
                    -ManifestIssueByKey $ManifestIssueByKey `
                    -RemoteIssueByKey $RemoteState.issueByKey `
                    -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey `
                    -AllowPendingRemoteClosure $true
            }
            $managedBody = Render-ManagedIssueBody -Issue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey
            $existingBody = [string](Get-ObjectProperty -Object $remote -Name "body" -DefaultValue "")
            $desiredBody = Merge-ManagedIssueBody -ExistingBody $existingBody -Key $key -ManagedBody $managedBody
            $desiredLabelNames = @(Get-DesiredIssueLabelNames -ManifestIssue $issue -ManifestIssueByKey $ManifestIssueByKey -RemoteIssueByKey $RemoteState.issueByKey -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey -ManagedLabelNames $ManagedLabelNames)
            $currentLabelNames = @(Get-RemoteLabelNames -RemoteIssue $remote)
            $milestoneTitle = [string]$ManifestMilestoneByKey[[string]$issue.milestone].title
            $desiredMilestoneId = 0
            if ($RemoteState.milestoneByTitle.ContainsKey($milestoneTitle)) {
                $desiredMilestoneId = [int](Get-ObjectProperty -Object $RemoteState.milestoneByTitle[$milestoneTitle] -Name "id" -DefaultValue 0)
            }
            $remoteMilestone = Get-ObjectProperty -Object $remote -Name "milestone" -DefaultValue $null
            $currentMilestoneId = [int](Get-ObjectProperty -Object $remoteMilestone -Name "id" -DefaultValue 0)
            $remoteStateValue = [string](Get-ObjectProperty -Object $remote -Name "state" -DefaultValue "open")
            $stateUpdate = ([string]$issue.desiredState -eq "closed" -and $remoteStateValue -ne "closed") -or
                ([string]$issue.desiredState -eq "reopen" -and $remoteStateValue -ne "open")
            $needsUpdate = [string](Get-ObjectProperty -Object $remote -Name "title" -DefaultValue "") -cne [string]$issue.title -or
                $existingBody -cne $desiredBody -or
                -not (Test-StringSetEqual -Left $currentLabelNames -Right $desiredLabelNames) -or
                $currentMilestoneId -ne $desiredMilestoneId -or
                $stateUpdate
            if ($needsUpdate) {
                Record-PreviewAction -Action update -Key "issue:$key"
            }
            else {
                Record-PreviewAction -Action unchanged -Key "issue:$key"
            }
        }
        catch {
            Record-SyncAction -Action failed -Key "issue:$key"
            Write-SyncLine "SYNC_ITEM_FAIL issue=$key operation=preview reason=$($_.Exception.Message)"
        }
    }
}

function Verify-Convergence {
    param(
        [hashtable]$ValidatedManifest,
        [hashtable]$ManagedLabelNames,
        [hashtable]$RemoteState
    )

    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($label in $ValidatedManifest.labels) {
        $labelName = [string]$label.name
        if (-not $RemoteState.labelByName.ContainsKey($labelName)) {
            $failures.Add("missing_label:$($label.name)")
            continue
        }
        $remoteLabel = $RemoteState.labelByName[$labelName]
        $remoteColor = ([string](Get-ObjectProperty -Object $remoteLabel -Name "color" -DefaultValue "")).TrimStart("#").ToLowerInvariant()
        $desiredColor = ([string]$label.color).ToLowerInvariant()
        $remoteDescription = [string](Get-ObjectProperty -Object $remoteLabel -Name "description" -DefaultValue "")
        if ($remoteColor -ne $desiredColor -or $remoteDescription -cne [string]$label.description) {
            $failures.Add("label_metadata:$labelName")
        }
    }
    foreach ($milestone in $ValidatedManifest.milestones) {
        $milestoneTitle = [string]$milestone.title
        if (-not $RemoteState.milestoneByTitle.ContainsKey($milestoneTitle)) {
            $failures.Add("missing_milestone:$($milestone.key)")
            continue
        }
        $remoteMilestone = $RemoteState.milestoneByTitle[$milestoneTitle]
        $remoteDescription = [string](Get-ObjectProperty -Object $remoteMilestone -Name "description" -DefaultValue "")
        $remoteMilestoneState = [string](Get-ObjectProperty -Object $remoteMilestone -Name "state" -DefaultValue "")
        $remoteDueOn = Get-ObjectProperty -Object $remoteMilestone -Name "due_on" -DefaultValue $null
        if ($remoteDescription -cne [string]$milestone.description -or $remoteMilestoneState -ne "open") {
            $failures.Add("milestone_metadata:$($milestone.key)")
        }
        if ($null -ne $remoteDueOn -and -not [string]::IsNullOrWhiteSpace([string]$remoteDueOn)) {
            $failures.Add("milestone_due_date:$($milestone.key)")
        }
    }
    foreach ($issue in $ValidatedManifest.issues) {
        $key = [string]$issue.key
        if (-not $RemoteState.issueByKey.ContainsKey($key)) {
            $failures.Add("missing_issue:$key")
            continue
        }
        $remote = $RemoteState.issueByKey[$key]
        $body = [string](Get-ObjectProperty -Object $remote -Name "body" -DefaultValue "")
        if ([string](Get-ObjectProperty -Object $remote -Name "title" -DefaultValue "") -cne [string]$issue.title) {
            $failures.Add("issue_title:$key")
        }
        if ((Get-HiddenIssueKey -Body $body) -ne $key) {
            $failures.Add("hidden_key:$key")
        }
        if (-not $body.Contains($script:ManagedStart) -or -not $body.Contains($script:ManagedEnd)) {
            $failures.Add("managed_body:$key")
        }
        else {
            $expectedManagedBody = Render-ManagedIssueBody -Issue $issue -ManifestIssueByKey $ValidatedManifest.issueByKey -RemoteIssueByKey $RemoteState.issueByKey
            $expectedBody = Merge-ManagedIssueBody -ExistingBody $body -Key $key -ManagedBody $expectedManagedBody
            if ($body -cne $expectedBody) {
                $failures.Add("managed_body_content:$key")
            }
        }

        $milestoneTitle = [string]$ValidatedManifest.milestoneByKey[[string]$issue.milestone].title
        $desiredMilestoneId = 0
        if ($RemoteState.milestoneByTitle.ContainsKey($milestoneTitle)) {
            $desiredMilestoneId = [int](Get-ObjectProperty -Object $RemoteState.milestoneByTitle[$milestoneTitle] -Name "id" -DefaultValue 0)
        }
        $remoteIssueMilestone = Get-ObjectProperty -Object $remote -Name "milestone" -DefaultValue $null
        $remoteMilestoneId = [int](Get-ObjectProperty -Object $remoteIssueMilestone -Name "id" -DefaultValue 0)
        if ($desiredMilestoneId -le 0 -or $remoteMilestoneId -ne $desiredMilestoneId) {
            $failures.Add("issue_milestone:$key")
        }

        $desiredLabelNames = @(Get-DesiredIssueLabelNames -ManifestIssue $issue -ManifestIssueByKey $ValidatedManifest.issueByKey -RemoteIssueByKey $RemoteState.issueByKey -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey -ManagedLabelNames $ManagedLabelNames)
        $remoteLabelNames = @(Get-RemoteLabelNames -RemoteIssue $remote)
        if (-not (Test-StringSetEqual -Left $remoteLabelNames -Right $desiredLabelNames)) {
            $failures.Add("issue_labels:$key")
        }

        $remoteIssueState = [string](Get-ObjectProperty -Object $remote -Name "state" -DefaultValue "")
        if ([string]$issue.desiredState -eq "closed" -and $remoteIssueState -ne "closed") {
            $failures.Add("issue_state:$key")
        }
        if ([string]$issue.desiredState -eq "closed" -and
            -not (Test-ClosureEvidenceVerified -ClosureEvidenceByKey $RemoteState.closureEvidenceByKey -Key $key)) {
            $failures.Add("closure_evidence:$key")
        }
        if ([string]$issue.desiredState -eq "reopen" -and $remoteIssueState -ne "open") {
            $failures.Add("issue_state:$key")
        }
        $parentKey = [string](Get-ObjectProperty -Object $issue -Name "parentKey" -DefaultValue "")
        if (-not [string]::IsNullOrWhiteSpace($parentKey)) {
            $parentNumber = Get-RemoteIssueNumber -RemoteIssueByKey $RemoteState.issueByKey -Key $parentKey
            if ($parentNumber -le 0 -or -not $body.Contains("#$parentNumber")) {
                $failures.Add("parent_link:$key")
            }
        }
        foreach ($dependencyKeyValue in @(ConvertTo-NormalizedArray $issue.dependencyKeys)) {
            $dependencyKey = [string]$dependencyKeyValue
            $dependencyNumber = Get-RemoteIssueNumber -RemoteIssueByKey $RemoteState.issueByKey -Key $dependencyKey
            if ($dependencyNumber -le 0 -or -not $body.Contains("#$dependencyNumber")) {
                $failures.Add("dependency_link:$($key):$dependencyKey")
            }
        }
    }

    if ($failures.Count -gt 0) {
        $sample = @($failures | Select-Object -First 20) -join ","
        throw "Convergence verification failed count=$($failures.Count) sample=$sample"
    }

    Write-SyncLine "SYNC_VERIFY_PASS milestones=$($ValidatedManifest.milestones.Count) labels=$($ValidatedManifest.labels.Count) issues=$($ValidatedManifest.issues.Count)"
}

try {
    Write-SyncLine "TEST_START gitea.backlog.sync"
    $resolvedBaseUrl = $BaseUrl.TrimEnd("/")
    if ($resolvedBaseUrl -ne $script:ExpectedBaseUrl -or
        $Owner -ne $script:ExpectedOwner -or
        $Repository -ne $script:ExpectedRepository) {
        throw "Refusing non-canonical target. Expected $($script:ExpectedBaseUrl)/$($script:ExpectedOwner)/$($script:ExpectedRepository)."
    }

    if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
        $ManifestPath = $script:DefaultManifestPath
    }
    $resolvedManifestPath = [IO.Path]::GetFullPath($ManifestPath)
    if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
        throw "Manifest not found: $resolvedManifestPath"
    }
    $manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $validated = Test-Manifest -Manifest $manifest
    Write-SyncLine "SYNC_MANIFEST_VALID milestones=$($validated.milestones.Count) labels=$($validated.labels.Count) epics=16 children=303 issues=$($validated.issues.Count)"

    if ($SelfTest) {
        if ($Apply) {
            throw "-SelfTest cannot be combined with -Apply."
        }
        $sampleIssue = $validated.issueByKey["M00-001"]
        $emptyRemoteIssueMap = @{}
        $managedSample = Render-ManagedIssueBody -Issue $sampleIssue -ManifestIssueByKey $validated.issueByKey -RemoteIssueByKey $emptyRemoteIssueMap
        $existingSample = @"
USER_NOTE_BEFORE
<!-- clros-issue-key:M00-001 -->
<!-- clros-managed-start -->
OLD_MANAGED_TEXT
<!-- clros-managed-end -->
USER_NOTE_AFTER
"@
        $mergedSample = Merge-ManagedIssueBody -ExistingBody $existingSample -Key "M00-001" -ManagedBody $managedSample
        $managedStartCount = [regex]::Matches($mergedSample, [regex]::Escape($script:ManagedStart)).Count
        $managedEndCount = [regex]::Matches($mergedSample, [regex]::Escape($script:ManagedEnd)).Count
        $hiddenKeyCount = [regex]::Matches($mergedSample, '<!--\s*clros-issue-key:M00-001\s*-->').Count
        if (-not $mergedSample.Contains("USER_NOTE_BEFORE") -or
            -not $mergedSample.Contains("USER_NOTE_AFTER") -or
            $mergedSample.Contains("OLD_MANAGED_TEXT") -or
            -not $mergedSample.Contains([string]$sampleIssue.summary) -or
            $managedStartCount -ne 1 -or
            $managedEndCount -ne 1 -or
            $hiddenKeyCount -ne 1) {
            throw "Managed body self-test failed to preserve user text or replace exactly one managed region."
        }
        Write-SyncLine "SYNC_SELF_TEST_MANAGED_BODY_PASS"

        $dependencyIssue = [pscustomobject]@{
            key = "SELFTEST-DEPENDENCY"
            desiredState = "open"
            dependencyKeys = @()
            labels = @("agent:blocked", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/dependency.cs")
        }
        $dependentIssue = [pscustomobject]@{
            key = "SELFTEST-DEPENDENT"
            desiredState = "open"
            dependencyKeys = @("SELFTEST-DEPENDENCY")
            labels = @("agent:blocked", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/dependent.cs")
        }
        $stateIssueByKey = @{
            "SELFTEST-DEPENDENCY" = $dependencyIssue
            "SELFTEST-DEPENDENT" = $dependentIssue
        }
        $closedDependencyRemote = [pscustomobject]@{
            number = 1
            state = "closed"
            labels = @([pscustomobject]@{ name = "agent:blocked" })
        }
        $openDependentRemote = [pscustomobject]@{
            number = 2
            state = "open"
            labels = @([pscustomobject]@{ name = "agent:blocked" })
        }
        $stateRemoteByKey = @{
            "SELFTEST-DEPENDENCY" = $closedDependencyRemote
            "SELFTEST-DEPENDENT" = $openDependentRemote
        }
        $stateEvidenceByKey = @{ "SELFTEST-DEPENDENCY" = $true }

        $manualClosureLabel = Resolve-AgentStateLabel `
            -ManifestIssue $dependencyIssue `
            -ManifestIssueByKey $stateIssueByKey `
            -RemoteIssueByKey $stateRemoteByKey `
            -ClosureEvidenceByKey $stateEvidenceByKey
        $closedWithoutManifestIntentLabel = Resolve-AgentStateLabel `
            -ManifestIssue $dependentIssue `
            -ManifestIssueByKey $stateIssueByKey `
            -RemoteIssueByKey $stateRemoteByKey `
            -ClosureEvidenceByKey $stateEvidenceByKey
        if ($manualClosureLabel -ne "agent:blocked" -or
            $closedWithoutManifestIntentLabel -ne "agent:blocked") {
            throw "State self-test promoted a remote-only closure without manifest closure intent."
        }

        $dependencyIssue.desiredState = "closed"
        $readyWithEvidenceLabel = Resolve-AgentStateLabel `
            -ManifestIssue $dependentIssue `
            -ManifestIssueByKey $stateIssueByKey `
            -RemoteIssueByKey $stateRemoteByKey `
            -ClosureEvidenceByKey $stateEvidenceByKey
        $stateEvidenceByKey["SELFTEST-DEPENDENCY"] = $false
        $blockedWithoutEvidenceLabel = Resolve-AgentStateLabel `
            -ManifestIssue $dependentIssue `
            -ManifestIssueByKey $stateIssueByKey `
            -RemoteIssueByKey $stateRemoteByKey `
            -ClosureEvidenceByKey $stateEvidenceByKey
        if ($readyWithEvidenceLabel -ne "agent:ready" -or
            $blockedWithoutEvidenceLabel -ne "agent:blocked") {
            throw "State self-test did not require both manifest closure intent and reviewed evidence."
        }

        $transitiveRoot = [pscustomobject]@{
            key = "SELFTEST-ROOT"
            desiredState = "closed"
            dependencyKeys = @()
            labels = @("agent:review", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/root.cs")
            parentKey = ""
        }
        $transitiveMiddle = [pscustomobject]@{
            key = "SELFTEST-MIDDLE"
            desiredState = "closed"
            dependencyKeys = @("SELFTEST-ROOT")
            labels = @("agent:review", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/middle.cs")
            parentKey = ""
        }
        $transitiveLeaf = [pscustomobject]@{
            key = "SELFTEST-LEAF"
            desiredState = "open"
            dependencyKeys = @("SELFTEST-MIDDLE")
            labels = @("agent:blocked", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/leaf.cs")
            parentKey = ""
        }
        $transitiveIssueByKey = @{
            "SELFTEST-ROOT" = $transitiveRoot
            "SELFTEST-MIDDLE" = $transitiveMiddle
            "SELFTEST-LEAF" = $transitiveLeaf
        }
        $transitiveRemoteByKey = @{
            "SELFTEST-ROOT" = [pscustomobject]@{
                number = 10
                state = "open"
                labels = @()
            }
            "SELFTEST-MIDDLE" = [pscustomobject]@{
                number = 11
                state = "closed"
                labels = @()
            }
            "SELFTEST-LEAF" = [pscustomobject]@{
                number = 12
                state = "open"
                labels = @()
            }
        }
        $transitiveEvidenceByKey = @{
            "SELFTEST-ROOT" = $true
            "SELFTEST-MIDDLE" = $true
        }
        $blockedByOpenTransitiveRoot = Resolve-AgentStateLabel `
            -ManifestIssue $transitiveLeaf `
            -ManifestIssueByKey $transitiveIssueByKey `
            -RemoteIssueByKey $transitiveRemoteByKey `
            -ClosureEvidenceByKey $transitiveEvidenceByKey
        $transitiveRemoteByKey["SELFTEST-ROOT"].state = "closed"
        $readyWithClosedTransitiveTree = Resolve-AgentStateLabel `
            -ManifestIssue $transitiveLeaf `
            -ManifestIssueByKey $transitiveIssueByKey `
            -RemoteIssueByKey $transitiveRemoteByKey `
            -ClosureEvidenceByKey $transitiveEvidenceByKey
        if ($blockedByOpenTransitiveRoot -ne "agent:blocked" -or
            $readyWithClosedTransitiveTree -ne "agent:ready") {
            throw "State self-test did not enforce the complete transitive closure tree."
        }

        $epicIssue = [pscustomobject]@{
            key = "SELFTEST-EPIC"
            desiredState = "closed"
            dependencyKeys = @()
            labels = @("agent:blocked", "type:epic")
            collisionLane = ""
            ownedPaths = @()
            parentKey = ""
        }
        $epicChild = [pscustomobject]@{
            key = "SELFTEST-EPIC-CHILD"
            desiredState = "closed"
            dependencyKeys = @()
            labels = @("agent:review", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/epic-child.cs")
            parentKey = "SELFTEST-EPIC"
        }
        $epicIssueByKey = @{
            "SELFTEST-EPIC" = $epicIssue
            "SELFTEST-EPIC-CHILD" = $epicChild
        }
        $epicRemoteByKey = @{
            "SELFTEST-EPIC" = [pscustomobject]@{
                number = 20
                state = "open"
                labels = @()
            }
            "SELFTEST-EPIC-CHILD" = [pscustomobject]@{
                number = 21
                state = "open"
                labels = @()
            }
        }
        $epicEvidenceByKey = @{
            "SELFTEST-EPIC" = $true
            "SELFTEST-EPIC-CHILD" = $true
        }
        $epicOrder = @(
            Get-IssueUpdateOrder -ManifestIssues @($epicIssue, $epicChild))
        $epicClosureBlocked = $false
        try {
            Assert-IssueClosureAllowed `
                -ManifestIssue $epicIssue `
                -ManifestIssueByKey $epicIssueByKey `
                -RemoteIssueByKey $epicRemoteByKey `
                -ClosureEvidenceByKey $epicEvidenceByKey `
                -AllowPendingRemoteClosure $false
        }
        catch {
            $epicClosureBlocked = $_.Exception.Message.Contains(
                "SELFTEST-EPIC-CHILD")
        }
        Assert-IssueClosureAllowed `
            -ManifestIssue $epicIssue `
            -ManifestIssueByKey $epicIssueByKey `
            -RemoteIssueByKey $epicRemoteByKey `
            -ClosureEvidenceByKey $epicEvidenceByKey `
            -AllowPendingRemoteClosure $true
        if ($epicOrder.Count -ne 2 -or
            [string]$epicOrder[0].key -ne "SELFTEST-EPIC-CHILD" -or
            [string]$epicOrder[1].key -ne "SELFTEST-EPIC" -or
            -not $epicClosureBlocked) {
            throw "State self-test did not order children before epics or block premature epic closure."
        }

        $reopenIssue = [pscustomobject]@{
            key = "SELFTEST-REOPEN"
            desiredState = "reopen"
            dependencyKeys = @()
            labels = @("agent:blocked", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/reopen.cs")
        }
        $reopenIssueByKey = @{ "SELFTEST-REOPEN" = $reopenIssue }
        $reopenRemoteByKey = @{
            "SELFTEST-REOPEN" = [pscustomobject]@{
                number = 3
                state = "closed"
                labels = @([pscustomobject]@{ name = "agent:done" })
            }
        }
        $reopenLabel = Resolve-AgentStateLabel `
            -ManifestIssue $reopenIssue `
            -ManifestIssueByKey $reopenIssueByKey `
            -RemoteIssueByKey $reopenRemoteByKey `
            -ClosureEvidenceByKey @{}
        if ($reopenLabel -ne "agent:ready") {
            throw "State self-test did not compute the reopened issue label in the same synchronization pass."
        }

        $collisionIssue = [pscustomobject]@{
            key = "SELFTEST-COLLISION"
            desiredState = "open"
            dependencyKeys = @()
            labels = @("agent:blocked", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/shared.cs")
        }
        $claimedIssue = [pscustomobject]@{
            key = "SELFTEST-CLAIMED"
            desiredState = "open"
            dependencyKeys = @()
            labels = @("agent:claimed", "type:implementation")
            collisionLane = ""
            ownedPaths = @("tests/selftest/shared.cs")
        }
        $collisionIssueByKey = @{
            "SELFTEST-COLLISION" = $collisionIssue
            "SELFTEST-CLAIMED" = $claimedIssue
        }
        $collisionRemoteByKey = @{
            "SELFTEST-COLLISION" = [pscustomobject]@{
                number = 4
                state = "open"
                labels = @([pscustomobject]@{ name = "agent:blocked" })
            }
            "SELFTEST-CLAIMED" = [pscustomobject]@{
                number = 5
                state = "open"
                labels = @([pscustomobject]@{ name = "agent:claimed" })
            }
        }
        $collisionLabel = Resolve-AgentStateLabel `
            -ManifestIssue $collisionIssue `
            -ManifestIssueByKey $collisionIssueByKey `
            -RemoteIssueByKey $collisionRemoteByKey `
            -ClosureEvidenceByKey @{}
        if ($collisionLabel -ne "agent:blocked") {
            throw "Collision self-test did not block an overlapping owned path claimed by another issue."
        }
        Write-SyncLine "SYNC_SELF_TEST_STATE_POLICY_PASS"
        Write-SyncLine "TEST_PASS gitea.backlog.sync"
        exit 0
    }

    $script:ApiRoot = "$resolvedBaseUrl/api/v1"
    $script:ApiHeaders = Get-ApiHeaders -RequireAuthentication ([bool]$Apply)
    $mode = "preview"
    if ($Apply) {
        $mode = "apply"
    }
    Write-SyncLine "SYNC_MODE $mode"

    if ($Offline) {
        if ($Apply) {
            throw "-Offline cannot be combined with -Apply."
        }
        Write-SyncLine "SYNC_OFFLINE manifest_only"
        foreach ($label in $validated.labels) {
            Record-SyncAction -Action skipped -Key "label:$($label.name)"
        }
        foreach ($milestone in $validated.milestones) {
            Record-SyncAction -Action skipped -Key "milestone:$($milestone.key)"
        }
        foreach ($issue in $validated.issues) {
            Record-SyncAction -Action skipped -Key "issue:$($issue.key)"
        }
        Write-SyncActionKeys
        Write-SyncLine "SYNC_SUMMARY created=$($script:Counters.created) updated=$($script:Counters.updated) unchanged=$($script:Counters.unchanged) skipped=$($script:Counters.skipped) failed=$($script:Counters.failed) wouldCreate=$($script:Counters.wouldCreate) wouldUpdate=$($script:Counters.wouldUpdate)"
        Write-SyncLine "TEST_PASS gitea.backlog.sync"
        exit 0
    }

    $remoteState = Get-RemoteState
    Write-SyncLine "SYNC_REMOTE_READ labels=$($remoteState.labels.Count) milestones=$($remoteState.milestones.Count) keyedIssues=$($remoteState.issueByKey.Count)"
    Initialize-ClosureEvidenceState -ManifestIssues $validated.issues -RemoteState $remoteState

    $managedLabelNames = @{}
    foreach ($label in $validated.labels) {
        $managedLabelNames[[string]$label.name] = $true
    }

    Ensure-Labels -ManifestLabels $validated.labels -RemoteState $remoteState
    Ensure-Milestones -ManifestMilestones $validated.milestones -RemoteState $remoteState
    Create-MissingIssues `
        -ManifestIssues $validated.issues `
        -ManifestMilestoneByKey $validated.milestoneByKey `
        -ManifestIssueByKey $validated.issueByKey `
        -ManagedLabelNames $managedLabelNames `
        -RemoteState $remoteState

    $issueUpdateOrder = @(
        Get-IssueUpdateOrder -ManifestIssues $validated.issues)
    if ($Apply) {
        Update-Issues `
            -ManifestIssues $issueUpdateOrder `
            -ManifestMilestoneByKey $validated.milestoneByKey `
            -ManifestIssueByKey $validated.issueByKey `
            -ManagedLabelNames $managedLabelNames `
            -RemoteState $remoteState

        try {
            $verifiedRemoteState = Get-RemoteState
            Initialize-ClosureEvidenceState -ManifestIssues $validated.issues -RemoteState $verifiedRemoteState
            Verify-Convergence -ValidatedManifest $validated -ManagedLabelNames $managedLabelNames -RemoteState $verifiedRemoteState
        }
        catch {
            Record-SyncAction -Action failed -Key "verification:convergence"
            Write-SyncLine "SYNC_ITEM_FAIL verification=convergence reason=$($_.Exception.Message)"
        }
    }
    else {
        Preview-Issues `
            -ManifestIssues $issueUpdateOrder `
            -ManifestMilestoneByKey $validated.milestoneByKey `
            -ManifestIssueByKey $validated.issueByKey `
            -ManagedLabelNames $managedLabelNames `
            -RemoteState $remoteState
    }

    Write-SyncActionKeys
    Write-SyncLine "SYNC_SUMMARY created=$($script:Counters.created) updated=$($script:Counters.updated) unchanged=$($script:Counters.unchanged) skipped=$($script:Counters.skipped) failed=$($script:Counters.failed) wouldCreate=$($script:Counters.wouldCreate) wouldUpdate=$($script:Counters.wouldUpdate)"
    if ($script:Counters.failed -gt 0) {
        throw "One or more synchronization items failed; rerun is safe after correcting the reported blocker."
    }
    Write-SyncLine "TEST_PASS gitea.backlog.sync"
}
catch {
    $failureLine = [int](Get-ObjectProperty -Object $_.InvocationInfo -Name "ScriptLineNumber" -DefaultValue 0)
    Write-SyncLine "TEST_FAIL gitea.backlog.sync line=$failureLine $($_.Exception.Message)"
    exit 1
}
