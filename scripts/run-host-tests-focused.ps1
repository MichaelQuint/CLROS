param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string[]] $TestFile,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $repositoryRoot 'tests/OS.HostTests'
$utf8 = [Text.UTF8Encoding]::new($false)
$runDirectory = $null
$result = [ordered]@{
    Scope = 'focused'; FullSuite = $false; StartedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    WorkingDirectory = $repositoryRoot
    Inputs = @(); Commands = @(); SourcesUnchanged = $false; Succeeded = $false
}

function Invoke-FocusedCommand {
    param([string[]] $Arguments, [string] $LogName)

    # PowerShell binds the array as native arguments; do not build a shell command string.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() })
        $nativeExit = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    $logPath = Join-Path $runDirectory $LogName
    [IO.File]::WriteAllLines($logPath, [string[]]$output, $utf8)
    $result.Commands += [ordered]@{
        Executable = 'dotnet'; Arguments = $Arguments; NativeExit = $nativeExit; Log = $logPath
    }
    Write-Host "FOCUSED_HOST_COMMAND exit=$nativeExit log=$logPath"
    if ($nativeExit -ne 0) { throw "native_command_failed exit=$nativeExit log=$logPath" }
    return ,$output
}

Write-Host 'TEST_START host.tests.focused'
Push-Location -LiteralPath $repositoryRoot
try {
    $selected = @('HostToolProcess.cs', 'HostFixtureAttribute.cs')
    foreach ($name in $TestFile) {
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_.-]*\.cs$' -or $name -ieq 'Program.cs') {
            throw "invalid_test_file name=$name"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $testRoot $name) -PathType Leaf)) {
            throw "test_file_missing name=$name"
        }
        if ($selected -notcontains $name) { $selected += $name }
    }
    foreach ($name in $selected) {
        $path = Join-Path $testRoot $name
        $result.Inputs += [ordered]@{ Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
    }

    $runDirectory = Join-Path $repositoryRoot ('artifacts/host-tests-focused/' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $runDirectory
    $projectPath = Join-Path $runDirectory 'Focused.HostTests.csproj'
    $entryPath = Join-Path $runDirectory 'FocusedEntry.cs'
    $entry = @'
using System.Reflection;
int failures = 0, count = 0;
foreach (var method in Assembly.GetExecutingAssembly().GetTypes()
    .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
    .Where(method => method.IsDefined(typeof(HostFixtureAttribute), false))
    .OrderBy(method => method.DeclaringType!.FullName + "." + method.Name, StringComparer.Ordinal))
{
    count++;
    try { method.Invoke(null, null); }
    catch (Exception error)
    {
        failures++;
        Console.WriteLine("TEST_FAIL host.tests.focused.fixture " + method.DeclaringType!.Name + "." + method.Name);
        Console.Error.WriteLine(error is TargetInvocationException ? error.InnerException : error);
    }
}
if (count == 0 || failures != 0) return 1;
Console.WriteLine("FOCUSED_HOST_ENTRY_REACHED");
return 0;
'@
    [IO.File]::WriteAllText($entryPath, $entry, $utf8)

    # Retain the original framework, language, unsafe settings and project references.
    [xml]$project = [IO.File]::ReadAllText((Join-Path $testRoot 'OS.HostTests.csproj'))
    $propertyGroup = $project.CreateElement('PropertyGroup')
    $defaultCompile = $project.CreateElement('EnableDefaultCompileItems')
    $defaultCompile.InnerText = 'false'
    $null = $propertyGroup.AppendChild($defaultCompile)
    $null = $project.Project.AppendChild($propertyGroup)
    foreach ($reference in $project.SelectNodes('//ProjectReference')) {
        $reference.SetAttribute('Include', [IO.Path]::GetFullPath((Join-Path $testRoot $reference.GetAttribute('Include'))))
    }
    foreach ($resource in $project.SelectNodes('//EmbeddedResource')) {
        $resource.SetAttribute('Include', [IO.Path]::GetFullPath((Join-Path $testRoot $resource.GetAttribute('Include'))))
    }
    $compileGroup = $project.CreateElement('ItemGroup')
    foreach ($path in @($result.Inputs | ForEach-Object { $_.Path }) + @($entryPath)) {
        $compile = $project.CreateElement('Compile')
        $compile.SetAttribute('Include', $path)
        $null = $compileGroup.AppendChild($compile)
    }
    $null = $project.Project.AppendChild($compileGroup)
    [IO.File]::WriteAllText($projectPath, $project.OuterXml, $utf8)
    Write-Host ('FOCUSED_HOST_FILES ' + ($selected -join ';'))
    Write-Host "FOCUSED_HOST_ARTIFACTS $runDirectory"

    $null = Invoke-FocusedCommand @('build', $projectPath, '--nologo', '-m:1', '-c', $Configuration) 'build.log'
    $output = Invoke-FocusedCommand @('run', '--project', $projectPath, '--no-build', '-c', $Configuration) 'run.log'
    $result.SourcesUnchanged = @($result.Inputs | Where-Object {
        (Get-FileHash -LiteralPath $_.Path).Hash -cne $_.Sha256
    }).Count -eq 0
    if (-not $result.SourcesUnchanged) { throw 'selected_source_changed' }
    if (@($output | Where-Object { $_ -ceq 'FOCUSED_HOST_ENTRY_REACHED' }).Count -ne 1 -or
        @($output | Where-Object { $_.StartsWith('TEST_START ', [StringComparison]::Ordinal) }).Count -eq 0 -or
        @($output | Where-Object { $_.StartsWith('TEST_PASS ', [StringComparison]::Ordinal) }).Count -eq 0 -or
        @($output | Where-Object { $_.StartsWith('TEST_FAIL ', [StringComparison]::Ordinal) }).Count -ne 0 -or
        @($output | Where-Object { $_ -ceq 'TEST_PASS host.tests' }).Count -ne 0) {
        throw 'focused_markers_rejected'
    }
    $result.Succeeded = $true
}
catch {
    $result.Error = ($_.Exception.Message -replace '[\r\n]+', ' ')
    Write-Host ('TEST_FAIL host.tests.focused ' + $result.Error)
}
finally {
    Pop-Location
    $result.EndedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if ($null -ne $runDirectory) {
        [IO.File]::WriteAllText((Join-Path $runDirectory 'result.json'), ($result | ConvertTo-Json -Depth 8), $utf8)
    }
}
if ($result.Succeeded) {
    Write-Host 'TEST_PASS host.tests.focused'
    exit 0
}
exit 1
