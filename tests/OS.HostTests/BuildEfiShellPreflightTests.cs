using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static class BuildEfiShellPreflightTests
{
    private const string SecretCanary = "CLROS_PREFLIGHT_SECRET_MUST_NOT_APPEAR";
    private const int PreflightProcessTimeoutMilliseconds = 120000;

    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-efi-preflight-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            Run("efi preflight enforces stable SDK 401 band and reports exact failures", () => TestSdkVersionContract(tempRoot), ref failures);
            Run("efi preflight discovers Visual Studio prerelease toolchains", () => TestPrereleaseVisualStudioDiscovery(tempRoot), ref failures);

            Console.WriteLine("TEST_START m01.020");
            int m01Failures = 0;
            Run(
                "m01.020 exact source closure and false defaults",
                () => TestM01ExactSourceClosureAndDefaults(tempRoot),
                ref m01Failures);
            Run(
                "m01.020 staged switch DAG and propagation",
                () => TestM01StagedSwitchDagAndPropagation(tempRoot),
                ref m01Failures);
            Run(
                "m01.020 all-three-switch compile",
                TestM01AllThreeSwitchCompile,
                ref m01Failures);
            Run(
                "EFI preflight M02 safe-stub switch DAG and propagation",
                () => TestM02SafeStubSwitchDagAndPropagation(tempRoot),
                ref m01Failures);
            Run(
                "EFI project M02 all-four-switch compile",
                TestM02AllFourSwitchCompile,
                ref m01Failures);
            Run(
                "EFI preflight M02 checked-binding switch DAG and propagation",
                () => TestM02CheckedBindingSwitchDagAndPropagation(tempRoot),
                ref m01Failures);
            Run(
                "EFI project M02 all-five-switch compile",
                TestM02AllFiveSwitchCompile,
                ref m01Failures);
            Run(
                "EFI preflight M02-013 terminal profile DAG and propagation",
                () => TestM02Ud2VectorStateSwitchDagAndPropagation(tempRoot),
                ref m01Failures);
            Run(
                "EFI project M02-013 terminal profile compile",
                TestM02Ud2VectorStateProfileCompile,
                ref m01Failures);
            Run(
                "m01.020 rejects forbidden source surfaces and guards",
                () => TestM01ForbiddenSourceAndGuardMatrix(tempRoot),
                ref m01Failures);
            Run(
                "m01.020 rejects project closure bypasses",
                () => TestM01ProjectClosureBypassMatrix(tempRoot),
                ref m01Failures);
            Run(
                "m01.020 rejects weakened allocation and CR3 contracts",
                () => TestM01SemanticContractMatrix(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 rejects serializer targets import bypasses",
                () => TestM01SerializerTargetsImportMatrix(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 locks and records the complete publish source and SDK closure",
                TestM01PublishSourceAndSdkClosure,
                ref m01Failures);
            Run(
                "m01.034 pins the isolated restore and package build authority",
                TestM01PinnedRestoreAndPackageAuthority,
                ref m01Failures);
            Run(
                "m01.034 holds absolute package inputs until explicit release",
                () => TestM01AbsolutePackageInputLocks(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 isolates source evaluation from stale project-extension inputs",
                () => TestM01ProjectExtensionEvaluationIsolation(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 bounds every owned build process and reaps descendants",
                () => TestM01BoundedBuildProcessRunner(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 freezes SDK paths and disables wildcard imports",
                () => TestM01MsbuildInjectionIsolation(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 bounds verifier execution and reaps its process tree",
                () => TestM01ArtifactVerifierTimeout(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 serializes canonical output ownership and releases it",
                () => TestM01PublishOutputMutex(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 rejects unsafe fresh-output cleanup paths",
                () => TestM01FreshOutputCleanupGuards(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 holds source read locks until explicit release",
                () => TestM01PublishSourceLocks(tempRoot),
                ref m01Failures);
            Run(
                "m01.034 owns output, log, and verifier file authority",
                () => TestM01OutputAndVerifierAuthority(tempRoot),
                ref m01Failures);

            if (m01Failures == 0)
            {
                Console.WriteLine("CLROS_M01_020_READY");
                Console.WriteLine("TEST_PASS m01.020");
            }
            else
            {
                Console.WriteLine("TEST_FAIL m01.020 failures=" + m01Failures);
                failures += m01Failures;
            }

            Run("efi preflight reports missing msvc linker", () => TestMissingLinkerFails(tempRoot), ref failures);
            Run("efi preflight reports missing windows sdk", () => TestMissingWindowsSdkFails(tempRoot), ref failures);
            Run("efi preflight reports missing ilcompiler", () => TestMissingIlCompilerFails(tempRoot), ref failures);
            Run("efi preflight reports missing nativeaot runtime pack", () => TestMissingRuntimePackFails(tempRoot), ref failures);
            Run("efi preflight reports missing qemu", () => TestMissingQemuFails(tempRoot), ref failures);
            Run("efi preflight reports missing qemu-img", () => TestMissingQemuImgFails(tempRoot), ref failures);
            Run("efi preflight reports missing ovmf", () => TestMissingOvmfFails(tempRoot), ref failures);
            Run("efi preflight passes with injected exact paths", () => TestInjectedPathsPass(tempRoot), ref failures);
            Run("efi preflight passes with injected search roots", () => TestInjectedSearchRootsPass(tempRoot), ref failures);
            Run("efi preflight selects newest credible numeric toolsets", () => TestNewestCredibleToolsetsWin(tempRoot), ref failures);
            Run("efi preflight rejects undersized ovmf", () => TestUndersizedOvmfFails(tempRoot), ref failures);
            Run("efi preflight rejects malformed ovmf signature", () => TestMalformedOvmfSignatureFails(tempRoot), ref failures);
            Run("efi preflight preserves ovmf paths with spaces", () => TestOvmfPathWithSpacesPasses(tempRoot), ref failures);
            Run("efi preflight ignores poisoned Get-FileHash module", () => TestOvmfEvidenceIgnoresPoisonedFileHashModule(tempRoot), ref failures);
            Run("efi preflight shares one stable ovmf read", () => TestOvmfEvidenceWithConcurrentReader(tempRoot), ref failures);
            Run("efi preflight releases repeated ovmf evidence reads", () => TestRepeatedOvmfEvidenceReleasesHandles(tempRoot), ref failures);
            Run("efi preflight separates unavailable ovmf evidence", () => TestUnavailableOvmfEvidenceFails(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure should not hide preflight contract results.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("Build EFI shell preflight tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestSdkVersionContract(string tempRoot)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string probe = Path.Combine(tempRoot, "sdk-version-contract.ps1");
        File.WriteAllText(probe, """
            param([string] $BuildScriptPath)
            $ErrorActionPreference = "Stop"
            $tokens = $null; $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath, [ref] $tokens, [ref] $errors)
            if ($errors.Count -ne 0) { throw "publisher_parse_failed" }
            $functions = @($ast.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -ceq "Get-EfiSdkVersionFailureReason"
            }, $true))
            if ($functions.Count -ne 1) { throw "sdk_helper_missing" }
            . ([scriptblock]::Create($functions[0].Extent.Text))
            foreach ($version in @("10.0.401", "10.0.402", "10.0.499")) {
                if ((Get-EfiSdkVersionFailureReason "ok" @($version)) -cne "ok") {
                    throw "supported_sdk_rejected $version"
                }
            }
            foreach ($version in @("", "10.0.300", "10.0.302", "10.0.400", "10.0.500",
                    "11.0.401", "10.1.401", "10.0.401-preview.1", "10.0.401+extra", "invalid")) {
                if ((Get-EfiSdkVersionFailureReason "ok" @($version)) -cne "dotnet_sdk_version_unsupported") {
                    throw "unsupported_sdk_not_classified $version"
                }
            }
            if ((Get-EfiSdkVersionFailureReason "ok" @()) -cne "dotnet_sdk_version_output_invalid" -or
                (Get-EfiSdkVersionFailureReason "ok" @("10.0.401", "10.0.402")) -cne "dotnet_sdk_version_output_invalid" -or
                (Get-EfiSdkVersionFailureReason "timed_out" @()) -cne "dotnet_sdk_version_timed_out") {
                throw "sdk_process_failure_not_classified"
            }
            Write-Output "SDK_VERSION_CONTRACT_PASS"
            """, new UTF8Encoding(false));
        ProcessResult result = RunProcess("powershell.exe", root, 30_000,
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", probe,
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        if (result.ExitCode != 0) Console.WriteLine(result.Output);
        return result.ExitCode == 0 && result.Output.Contains("SDK_VERSION_CONTRACT_PASS", StringComparison.Ordinal);
    }

    private static bool TestPrereleaseVisualStudioDiscovery(string tempRoot)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string probe = Path.Combine(tempRoot, "prerelease-discovery.ps1");
        string vswhere = Path.Combine(tempRoot, "fixture-vswhere.ps1");
        File.WriteAllText(vswhere, """
            if ($args -notcontains "-prerelease" -or $args -notcontains "Microsoft.VisualStudio.Component.VC.Tools.x86.x64") {
                throw "required_vswhere_arguments_missing"
            }
            Write-Output $PSScriptRoot
            """, new UTF8Encoding(false));
        File.WriteAllText(probe, """
            param([string] $BuildScriptPath, [string] $VsWherePath)
            $ErrorActionPreference = "Stop"
            $tokens = $null; $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath, [ref] $tokens, [ref] $errors)
            if ($errors.Count -ne 0) { throw "publisher_parse_failed" }
            $functions = @($ast.FindAll({ param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -ceq "Get-DefaultMsvcLinkerSearchRoot"
            }, $true))
            if ($functions.Count -ne 1) { throw "discovery_helper_missing" }
            . ([scriptblock]::Create($functions[0].Extent.Text))
            $roots = @(Get-DefaultMsvcLinkerSearchRoot $VsWherePath)
            $expected = Join-Path (Split-Path -Parent $VsWherePath) "VC\Tools\MSVC"
            if ($roots -cnotcontains $expected) { throw "prerelease_install_not_discovered" }
            Write-Output "PRERELEASE_VS_DISCOVERY_PASS"
            """, new UTF8Encoding(false));
        ProcessResult result = RunProcess("powershell.exe", root, 30_000,
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", probe,
            Path.Combine(root, "scripts", "build-efi-shell.ps1"), vswhere);
        if (result.ExitCode != 0) Console.WriteLine(result.Output);
        return result.ExitCode == 0 && result.Output.Contains("PRERELEASE_VS_DISCOVERY_PASS", StringComparison.Ordinal);
    }

    internal static bool HasExactCanonicalEfiCompileItem(
        string include,
        string link)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        System.Xml.Linq.XDocument project =
            System.Xml.Linq.XDocument.Load(projectPath);
        System.Xml.Linq.XElement[] matches = project
            .Descendants("Compile")
            .Where(element =>
                string.Equals(
                    element.Attribute("Include")?.Value,
                    include,
                    StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("Link")?.Value ?? string.Empty,
                    link,
                    StringComparison.Ordinal))
            .ToArray();

        return matches.Length == 1
            && matches[0].Attributes().Count() == (link.Length == 0 ? 1 : 2)
            && !matches[0].Elements().Any()
            && matches[0].Attribute("Condition") is null
            && matches[0].Parent?.Attribute("Condition") is null;
    }

    private static bool TestM01ExactSourceClosureAndDefaults(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-exact-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-exact");
        ProcessResult result = RunPreflight(
            tools,
            useSearchRoots: false,
            scriptPath: source.ScriptPath);

        return IsM01SourceClosureSuccess(
            result,
            "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false");
    }

    private static bool TestM01StagedSwitchDagAndPropagation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-switches");

        ProcessResult stackOnly = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe");
        ProcessResult stackAndAllocator = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe");
        ProcessResult allThree = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe");
        ProcessResult allocatorWithoutStack = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LiveAllocatorProbe");
        ProcessResult pagingWithoutAllocator = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01OwnedPagingProbe");
        ProcessResult unknownSwitch = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01UnknownProbe");
        ProcessResult uefiGoneSimulation = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableUefiGoneSimulationProbe");
        ProcessResult exitBootServicesFreshMap = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableExitBootServicesFreshMapProbe");
        ProcessResult acpiPhysicalDiscovery = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableAcpiPhysicalDiscoveryProbe");
        ProcessResult debugConfiguration = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-Configuration",
            "Debug");
        ProcessResult unsupportedConfiguration = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-Configuration",
            "Custom");

        string script = File.ReadAllText(source.ScriptPath);
        bool passed =
            IsM01SourceClosureSuccess(
                stackOnly,
                "loadedImageStack=true liveAllocator=false ownedPaging=false m02SafeStub=false")
            && IsM01SourceClosureSuccess(
                stackAndAllocator,
                "loadedImageStack=true liveAllocator=true ownedPaging=false m02SafeStub=false")
            && IsM01SourceClosureSuccess(
                allThree,
                "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=false")
            && IsM01SwitchFailure(
                allocatorWithoutStack,
                "live_allocator_requires_loaded_image_stack")
            && IsM01SwitchFailure(
                pagingWithoutAllocator,
                "owned_paging_requires_live_allocator")
            && unknownSwitch.ExitCode != 0
            && unknownSwitch.Output.Contains("EnableM01UnknownProbe", StringComparison.Ordinal)
            && !unknownSwitch.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && IsM01SourceClosureSuccess(
                uefiGoneSimulation,
                "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && IsM01SourceClosureSuccess(
                exitBootServicesFreshMap,
                "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && IsM01SourceClosureSuccess(
                acpiPhysicalDiscovery,
                "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && IsM01SourceClosureSuccess(
                debugConfiguration,
                "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && unsupportedConfiguration.ExitCode != 0
            && unsupportedConfiguration.Output.Contains(
                "ValidateSet",
                StringComparison.OrdinalIgnoreCase)
            && unsupportedConfiguration.Output.Contains(
                "Custom",
                StringComparison.Ordinal)
            && !unsupportedConfiguration.Output.Contains(
                "EFI_PREFLIGHT_OK",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableM01LoadedImageStackProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableM01LiveAllocatorProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableM01OwnedPagingProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableUefiGoneSimulationProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableExitBootServicesFreshMapProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:EnableAcpiPhysicalDiscoveryProbe=true\"",
                StringComparison.Ordinal);

        if (!passed)
        {
            Console.WriteLine(
                "M01 switch outputs:\n"
                + stackOnly.Output
                + stackAndAllocator.Output
                + allThree.Output
                + allocatorWithoutStack.Output
                + pagingWithoutAllocator.Output
                + unknownSwitch.Output
                + uefiGoneSimulation.Output
                + exitBootServicesFreshMap.Output
                + acpiPhysicalDiscovery.Output
                + debugConfiguration.Output
                + unsupportedConfiguration.Output);
        }

        return passed;
    }

    private static bool TestM01AllThreeSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        ProcessResult result = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM01LoadedImageStackProbe=true",
            "-p:EnableM01LiveAllocatorProbe=true",
            "-p:EnableM01OwnedPagingProbe=true");

        bool passed = result.ExitCode == 0
            && result.Output.Contains("Build succeeded.", StringComparison.Ordinal)
            && !result.Output.Contains(": error ", StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02SafeStubSwitchDagAndPropagation(
        string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m02-safe-stub-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(
            tempRoot,
            "m02-safe-stub-switches");

        ProcessResult allFour = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe");
        ProcessResult missingLoadedImage = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM02SafeStubProbe");
        ProcessResult missingAllocator = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM02SafeStubProbe");
        ProcessResult missingOwnedPaging = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM02SafeStubProbe");

        string script = File.ReadAllText(source.ScriptPath);
        bool passed = IsM01SourceClosureSuccess(
                allFour,
                "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true")
            && allFour.Output.Contains(
                "EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=true loadedImageStack=true liveAllocator=true ownedPaging=true",
                StringComparison.Ordinal)
            && IsM02SwitchFailure(
                missingLoadedImage,
                "safe_stub_requires_loaded_image_stack")
            && IsM02SwitchFailure(
                missingAllocator,
                "safe_stub_requires_live_allocator")
            && IsM02SwitchFailure(
                missingOwnedPaging,
                "safe_stub_requires_owned_paging")
            && Regex.Matches(
                script,
                Regex.Escape("[switch] $EnableM02SafeStubProbe"),
                RegexOptions.CultureInvariant).Count == 1
            && script.Contains(
                "\"-p:EnableM02SafeStubProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "Write-Host \"EFI_PROBE M02_SAFE_STUB\"",
                StringComparison.Ordinal);

        if (!passed)
        {
            Console.WriteLine(
                "M02 switch outputs:\n"
                + allFour.Output
                + missingLoadedImage.Output
                + missingAllocator.Output
                + missingOwnedPaging.Output);
        }

        return passed;
    }

    private static bool TestM02AllFourSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        ProcessResult result = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM01LoadedImageStackProbe=true",
            "-p:EnableM01LiveAllocatorProbe=true",
            "-p:EnableM01OwnedPagingProbe=true",
            "-p:EnableM02SafeStubProbe=true");

        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "Build succeeded.",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                ": error ",
                StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02CheckedBindingSwitchDagAndPropagation(
        string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m02-checked-binding-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(
            tempRoot,
            "m02-checked-binding-switches");

        ProcessResult allFive = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe");
        ProcessResult missingSafeStub = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            "-EnableM01LoadedImageStackProbe",
            "-EnableM01LiveAllocatorProbe",
            "-EnableM01OwnedPagingProbe",
            "-EnableM02CheckedPortIoBindingProbe");

        string script = File.ReadAllText(source.ScriptPath);
        bool passed = IsM01SourceClosureSuccess(
                allFive,
                "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true")
            && allFive.Output.Contains(
                "EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=true loadedImageStack=true liveAllocator=true ownedPaging=true checkedPortIoBinding=true",
                StringComparison.Ordinal)
            && IsM02SwitchFailure(
                missingSafeStub,
                "checked_binding_requires_safe_stub")
            && Regex.Matches(
                script,
                Regex.Escape(
                    "[switch] $EnableM02CheckedPortIoBindingProbe"),
                RegexOptions.CultureInvariant).Count == 1
            && script.Contains(
                "\"-p:EnableM02CheckedPortIoBindingProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "Write-Host \"EFI_PROBE M02_CHECKED_PORT_IO_BINDING\"",
                StringComparison.Ordinal);

        if (!passed)
        {
            Console.WriteLine(
                "M02 checked-binding switch outputs:\n"
                + allFive.Output
                + missingSafeStub.Output);
        }

        return passed;
    }

    private static bool TestM02AllFiveSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        ProcessResult result = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM01LoadedImageStackProbe=true",
            "-p:EnableM01LiveAllocatorProbe=true",
            "-p:EnableM01OwnedPagingProbe=true",
            "-p:EnableM02SafeStubProbe=true",
            "-p:EnableM02CheckedPortIoBindingProbe=true");

        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "Build succeeded.",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                ": error ",
                StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02Ud2VectorStateSwitchDagAndPropagation(
        string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m02-ud2-vector-state-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(
            tempRoot,
            "m02-ud2-vector-state-switches");
        string[] enabledArguments =
        [
            "-EnableM02Ud2VectorStateProbe"
        ];

        ProcessResult enabled = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            enabledArguments);
        ProcessResult independentM04Allowed = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM04IndependentEndianConformanceProbe"]);
        ProcessResult ownedX64GdtConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02OwnedX64GdtEncodingProbe"]);
        ProcessResult guardedIstConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02GuardedIstStackAllocationProbe"]);
        ProcessResult terminalConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02OwnedGdtTssTerminalProbe"]);
        ProcessResult normalizedConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02NormalizedInterruptFramePageProbe"]);
        ProcessResult idtConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02OwnedIdtGatePopulationProbe"]);
        ProcessResult idtrConflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [.. enabledArguments, "-EnableM02IdtrContinuedExecutionProbe"]);
        ProcessResult m04Conflict = RunPreflight(
            tools,
            false,
            source.ScriptPath,
            [
                .. enabledArguments,
                "-EnableM04IndependentEndianConformanceProbe",
                "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"
            ]);

        string script = File.ReadAllText(source.ScriptPath);
        bool passed = IsM01SourceClosureSuccess(
                enabled,
                "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true")
            && enabled.Output.Contains(
                "panicSafeOwnedCom1=true",
                StringComparison.Ordinal)
            && enabled.Output.Contains(
                "ud2VectorState=true",
                StringComparison.Ordinal)
            && enabled.Output.Contains(
                "ownedX64GdtEncoding=false guardedIstStackAllocation=false",
                StringComparison.Ordinal)
            && enabled.Output.Contains(
                "EFI_PREFLIGHT_M02_UD2_VECTOR_STATE_IMPLICATION status=ok m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true ownedCom1Ready=true panicSafeOwnedCom1=true",
                StringComparison.Ordinal)
            && IsM01SourceClosureSuccess(
                independentM04Allowed,
                "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true")
            && independentM04Allowed.Output.Contains(
                "EFI_PREFLIGHT_M04_SWITCHES status=ok independentEndianConformance=true spanOverlapMemoryCastUnsafeWidthConformance=false",
                StringComparison.Ordinal)
            && script.Contains(
                "m02_ud2_vector_state_probe_explicit_false_prerequisite",
                StringComparison.Ordinal)
            && IsM02ProfileSwitchFailure(
                ownedX64GdtConflict,
                "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe")
            && IsM02ProfileSwitchFailure(
                guardedIstConflict,
                "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe")
            && IsM02ProfileSwitchFailure(
                terminalConflict,
                "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal")
            && IsM02ProfileSwitchFailure(
                normalizedConflict,
                "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe")
            && IsM02ProfileSwitchFailure(
                idtConflict,
                "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe")
            && IsM02ProfileSwitchFailure(
                idtrConflict,
                "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe")
            && m04Conflict.ExitCode == 1
            && m04Conflict.Output.Contains(
                "reason=span_memory_conformance_forbids_m02_ud2_vector_state",
                StringComparison.Ordinal)
            && m04Conflict.Output.Contains(
                "TEST_FAIL efi.shell.publish m04_span_memory_conformance_forbids_m02_ud2_vector_state",
                StringComparison.Ordinal)
            && !m04Conflict.Output.Contains(
                "EFI_PREFLIGHT_M01_SOURCE_CLOSURE",
                StringComparison.Ordinal)
            && Regex.Matches(
                script,
                Regex.Escape("[switch] $EnableM02Ud2VectorStateProbe"),
                RegexOptions.CultureInvariant).Count == 1
            && script.Contains(
                "\"-p:EnableM02Ud2VectorStateProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "Write-Host \"EFI_PROBE M02_UD2_VECTOR_STATE\"",
                StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(
                "M02-013 switch outputs:\n"
                + enabled.Output
                + independentM04Allowed.Output
                + ownedX64GdtConflict.Output
                + guardedIstConflict.Output
                + terminalConflict.Output
                + normalizedConflict.Output
                + idtConflict.Output
                + idtrConflict.Output
                + m04Conflict.Output);
        }

        return passed;
    }

    private static bool TestM02Ud2VectorStateProfileCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        ProcessResult result = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM02Ud2VectorStateProbe=true");

        ProcessResult explicitFalse = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM02Ud2VectorStateProbe=true",
            "-p:EnableM02PanicSafeOwnedCom1Probe=false");

        ProcessResult ownedX64GdtConflict = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM02Ud2VectorStateProbe=true",
            "-p:EnableM02OwnedX64GdtEncodingProbe=true");

        ProcessResult guardedIstConflict = RunProcess(
            "dotnet",
            repositoryRoot,
            timeoutMilliseconds: 120000,
            "build",
            projectPath,
            "--nologo",
            "-m:1",
            "--disable-build-servers",
            "-p:EnableM02Ud2VectorStateProbe=true",
            "-p:EnableM02GuardedIstStackAllocationProbe=true");

        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "Build succeeded.",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                ": error ",
                StringComparison.OrdinalIgnoreCase)
            && explicitFalse.ExitCode != 0
            && explicitFalse.Output.Contains(
                "EnableM02Ud2VectorStateProbe requires EnableM02PanicSafeOwnedCom1Probe=true.",
                StringComparison.Ordinal)
            && ownedX64GdtConflict.ExitCode != 0
            && ownedX64GdtConflict.Output.Contains(
                "EnableM02Ud2VectorStateProbe cannot be combined with EnableM02OwnedX64GdtEncodingProbe=true.",
                StringComparison.Ordinal)
            && guardedIstConflict.ExitCode != 0
            && guardedIstConflict.Output.Contains(
                "EnableM02Ud2VectorStateProbe cannot be combined with EnableM02GuardedIstStackAllocationProbe=true.",
                StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(
                result.Output
                + explicitFalse.Output
                + ownedX64GdtConflict.Output
                + guardedIstConflict.Output);
        }

        return passed;
    }

    private static bool TestM01ForbiddenSourceAndGuardMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-source-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate, string Reason)[] cases =
        [
            (
                "reviewed-type-manager-altered-source",
                source => AppendSource(
                    source, "Runtime", "TypeManagerModuleMetadataLiveNativeAOTImage.cs",
                    "\n// Alter the complete reviewed source identity.\n"),
                "forbidden_active_source"),
            (
                "reviewed-type-manager-wrong-path",
                source => File.Copy(
                    SourcePath(source, "Runtime", "TypeManagerModuleMetadataLiveNativeAOTImage.cs"),
                    SourcePath(source, "Memory", "PageProtection.cs"), overwrite: true),
                "forbidden_active_source"),
            (
                "reviewed-typed-ebs-adapter-altered-source",
                source => AppendSource(
                    source, "Boot", "TypedNoStdlibExitBootServicesCallAdapter.cs",
                    "\n// Alter the complete reviewed source identity.\n"),
                "forbidden_active_source"),
            (
                "reviewed-typed-ebs-adapter-wrong-path",
                source => File.Copy(
                    SourcePath(source, "Boot", "TypedNoStdlibExitBootServicesCallAdapter.cs"),
                    SourcePath(source, "Memory", "PageProtection.cs"), overwrite: true),
                "forbidden_active_source"),
            (
                "reviewed-final-map-altered-source",
                source => AppendSource(
                    source, "Boot", "NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs",
                    "\n// Alter the complete reviewed source identity.\n"),
                "forbidden_active_source"),
            (
                "reviewed-bounded-retry-altered-source",
                source => AppendSource(
                    source, "Boot", "BoundedRetryStaleKeyStatus.cs",
                    "\n// Alter the complete reviewed source identity.\n"),
                "forbidden_active_source"),
            (
                "active-span",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedSpan { public static void Use(Span<byte> value) { } }\n"),
                "forbidden_active_source"),
            (
                "active-system-api",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedSystem { public static void Use() { System.GC.KeepAlive(null); } }\n"),
                "forbidden_active_source"),
            (
                "active-string",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedString { public const string Value = \"host\"; }\n"),
                "forbidden_active_source"),
            (
                "active-flags-attribute",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\n[System.FlagsAttribute] internal enum InjectedFlags : uint { One = 1 }\n"),
                "forbidden_active_source"),
            (
                "active-exception",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedThrow { public static void Fail() { throw null; } }\n"),
                "forbidden_active_source"),
            (
                "active-managed-delegate",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal delegate void InjectedDelegate();\n"),
                "forbidden_active_source"),
            (
                "active-record-type",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal record InjectedRecord;\n"),
                "forbidden_active_source"),
            (
                "active-generic-holder",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal struct InjectedGeneric<T> { public T Value; }\n"),
                "forbidden_active_source"),
            (
                "active-generic-method",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedGenericMethod { public static T Identity<T>(T value) { return value; } }\n"),
                "forbidden_active_source"),
            (
                "active-inline-nonstatic-class",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedOuter { private sealed class InjectedInner { } }\n"),
                "forbidden_active_source"),
            (
                "active-file-class",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nfile class InjectedFileClass { }\n"),
                "forbidden_active_source"),
            (
                "active-multidimensional-array",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal struct InjectedArray { public int[,] Values; }\n"),
                "forbidden_active_source"),
            (
                "active-system-alias",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace Injected { using S = global::System; internal struct Holder { public S.Type Value; } }\n"),
                "forbidden_active_source"),
            (
                "active-system-array",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace System { public static class InjectedArray { public static Array Value; } }\n"),
                "forbidden_active_source"),
            (
                "active-system-namespace-reference",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace System { internal struct Holder { public RuntimeType Value; } }\n"),
                "forbidden_active_source"),
            (
                "active-unicode-escaped-system",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace Syst\\u0065m { internal struct Holder { public RuntimeType Value; } }\n"),
                "forbidden_active_source"),
            (
                "active-verbatim-system",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace @System { internal struct Holder { public RuntimeType Value; } }\n"),
                "forbidden_active_source"),
            (
                "active-nameof-string",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedNameof { public static void Use() { _ = nameof(Use); } }\n"),
                "forbidden_active_source"),
            (
                "active-anonymous-object",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedAnonymous { public static void Use() { var value = new { Value = 1 }; } }\n"),
                "forbidden_active_source"),
            (
                "active-inline-attribute",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\nnamespace Injected { [Injected] internal struct Holder { } }\n"),
                "forbidden_active_source"),
            (
                "active-parameter-attribute",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedParameterAttribute { public static void Use([Injected] int value) { } }\n"),
                "forbidden_active_source"),
            (
                "unavailable-bcl-constant",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\ninternal static class InjectedConstant { public const int Value = int.MaxValue; }\n"),
                "forbidden_active_source"),
            (
                "ambiguous-positive-nostdlib",
                source => ReplaceOnce(
                    SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"),
                    "#if !NOSTDLIB_EFI",
                    "#if NOSTDLIB_EFI"),
                "ambiguous_preprocessor_condition"),
            (
                "ambiguous-composite-nostdlib",
                source => ReplaceOnce(
                    SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"),
                    "#if !NOSTDLIB_EFI",
                    "#if !NOSTDLIB_EFI && HOST_TESTS"),
                "ambiguous_preprocessor_condition"),
            (
                "ambiguous-else",
                source => ReplaceOnce(
                    SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"),
                    "#endif",
                    "#else\n#endif"),
                "ambiguous_preprocessor_condition"),
            (
                "undef-nostdlib",
                source => AppendSource(
                    source,
                    "Memory",
                    "PageProtection.cs",
                    "\n#undef NOSTDLIB_EFI\n"),
                "unsupported_preprocessor_directive"),
            (
                "missing-exact-exclusion",
                source => RemoveFirstNostdlibExclusion(
                    SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs")),
                "nostdlib_efi_exclusion_mismatch")
        ];

        foreach ((string name, Action<SourceClosureFixture> mutate, string reason) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(
                tempRoot,
                "m01-source-negative-" + name);
            mutate(source);
            string? expectedDetail = name switch
            {
                "reviewed-type-manager-altered-source" => @"..\OS.Kernel\Runtime\TypeManagerModuleMetadataLiveNativeAOTImage.cs:reviewed_type_manager_source_identity",
                "reviewed-type-manager-wrong-path" => @"..\OS.Kernel\Memory\PageProtection.cs:using_directive",
                "reviewed-typed-ebs-adapter-altered-source" => @"..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs:reviewed_typed_ebs_adapter_source_identity",
                "reviewed-typed-ebs-adapter-wrong-path" => @"..\OS.Kernel\Memory\PageProtection.cs:using_directive",
                "reviewed-final-map-altered-source" => @"..\OS.Kernel\Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs:reviewed_final_map_window_source_identity",
                "reviewed-bounded-retry-altered-source" => @"..\OS.Kernel\Boot\BoundedRetryStaleKeyStatus.cs:reviewed_bounded_ebs_retry_source_identity",
                _ => null
            };
            if (!ExpectM01SourceClosureFailure(tools, source, reason, expectedDetail))
            {
                Console.WriteLine("M01 forbidden-source case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool TestM01ProjectClosureBypassMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-project-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate)[] cases =
        [
            (
                "missing-required-source",
                source => File.Delete(SourcePath(source, "Boot", "UefiMemoryType.cs"))),
            (
                "boot-sequence-inclusion",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <Compile Include=\"..\\OS.Kernel\\Boot\\BootSequence.cs\" Link=\"Boot\\BootSequence.cs\" />\n")),
            (
                "m01-010-planner-inclusion",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <Compile Include=\"..\\OS.Kernel\\Memory\\BootPageAllocatorState.cs\" Link=\"Memory\\BootPageAllocatorState.cs\" />\n")),
            (
                "host-test-inclusion",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <Compile Include=\"..\\..\\tests\\OS.HostTests\\M01LivePagingIntegrationTests.cs\" Link=\"Tests\\M01LivePagingIntegrationTests.cs\" />\n")),
            (
                "duplicate-compile-item",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <Compile Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" />\n")),
            (
                "duplicate-link",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Link=\"Boot\\EfiLoadedImageRangeCapturePlan.cs\"",
                    "Link=\"Boot\\EfiLoadedImageRange.cs\"")),
            (
                "noncanonical-project",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<AssemblyName>OS</AssemblyName>",
                    "<AssemblyName>CLROS.Alternate</AssemblyName>")),
            (
                "conditional-required-property-group",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<PropertyGroup>",
                    "<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">")),
            (
                "attributed-required-property",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<NoStdLib>true</NoStdLib>",
                    "<NoStdLib Label=\"unsafe\">true</NoStdLib>")),
            (
                "source-glob",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"",
                    "Include=\"..\\OS.Kernel\\Boot\\*.cs\"")),
            (
                "property-expanded-source",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"",
                    "Include=\"$(KernelRoot)\\Boot\\UefiMemoryType.cs\"")),
            (
                "conditional-future-slot",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"",
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Condition=\"'$(FutureM01)' == 'true'\"")),
            (
                "compile-exclude",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"",
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Exclude=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"")),
            (
                "exists-future-slot",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"",
                    "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Condition=\"Exists('future.cs')\"")),
            (
                "target-hidden-compile",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <Target Name=\"InjectCompile\"><ItemGroup><Compile Include=\"future.cs\" /></ItemGroup></Target>\n")),
            (
                "project-reference",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <ProjectReference Include=\"..\\OS.Kernel\\OS.Kernel.csproj\" />\n")),
            (
                "import",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <Import Project=\"future.targets\" />\n")),
            (
                "choose",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <Choose><When Condition=\"'$(FutureM01)' == 'true'\"><ItemGroup><Compile Include=\"future.cs\" /></ItemGroup></When></Choose>\n")),
            (
                "analyzer",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <Analyzer Include=\"future-analyzer.dll\" />\n")),
            (
                "extra-package-reference",
                source => InsertBeforeFirstItemGroupEnd(
                    source.ProjectPath,
                    "    <PackageReference Include=\"Future.Build\" Version=\"1.0.0\" PrivateAssets=\"all\" />\n")),
            (
                "duplicate-package-reference",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<PackageReference Include=\"runtime.win-x64.Microsoft.DotNet.ILCompiler\" Version=\"10.0.12\" PrivateAssets=\"all\" />",
                    "<PackageReference Include=\"Microsoft.DotNet.ILCompiler\" Version=\"10.0.12\" PrivateAssets=\"all\" />")),
            (
                "define-constants-override",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <PropertyGroup><DefineConstants>TRACE</DefineConstants></PropertyGroup>\n")),
            (
                "inert-switch-validation",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "BeforeTargets=\"BeforeCompile\"",
                    "AfterTargets=\"CoreCompile\"")),
            (
                "directory-build-compile-injection",
                source => InsertBeforeProjectEnd(
                    Path.Combine(source.Root, "Directory.Build.targets"),
                    "  <ItemGroup><Compile Include=\"future.cs\" /></ItemGroup>\n")),
            (
                "directory-build-output-injection",
                source => InsertBeforeProjectEnd(
                    Path.Combine(source.Root, "Directory.Build.targets"),
                    "  <Target Name=\"FutureSource\"><SomeTask><Output TaskParameter=\"Files\" ItemName=\"Compile\" /></SomeTask></Target>\n")),
            (
                "directory-build-target-collision",
                source => InsertBeforeProjectEnd(
                    Path.Combine(source.Root, "Directory.Build.targets"),
                    "  <Target Name=\"ValidateM01ProbeSwitches\" Condition=\"false\"><Error Condition=\"false\" Text=\"disabled\" /></Target>\n")),
            (
                "validation-target-extra-child",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "\t</Target>",
                    "\t\t<Exec Command=\"echo unsafe\" />\n\t</Target>")),
            (
                "target-time-source-mutation",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <Target Name=\"MutateExpectedSource\" BeforeTargets=\"CoreCompile\"><WriteLinesToFile File=\"future.cs\" Lines=\"unsafe\" /></Target>\n")),
            (
                "initial-target-injection",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\">",
                    "<Project Sdk=\"Microsoft.NET.Sdk\" InitialTargets=\"MutateExpectedSource\">")),
            (
                "missing-switch-group",
                source => RemoveSwitchPropertyGroup(
                    source.ProjectPath,
                    "EnableM01OwnedPagingProbe"))
        ];

        foreach ((string name, Action<SourceClosureFixture> mutate) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(
                tempRoot,
                "m01-project-negative-" + name);
            mutate(source);
            bool passed = name == "missing-required-source"
                ? ExpectM01SourceSnapshotFailure(
                    tools,
                    source,
                    "UefiMemoryType.cs")
                : ExpectM01SourceClosureFailure(
                    tools,
                    source,
                    expectedReason: null);
            if (!passed)
            {
                Console.WriteLine("M01 project-closure case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool ExpectM01SourceSnapshotFailure(
        ToolFixture tools,
        SourceClosureFixture source,
        string expectedPathFragment)
    {
        ProcessResult result = RunPreflight(
            tools,
            useSearchRoots: false,
            scriptPath: source.ScriptPath);
        bool passed = result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish source_snapshot_failed",
                StringComparison.Ordinal)
            && result.Output.Contains(
                expectedPathFragment,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_PREFLIGHT_START",
                StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM01SemanticContractMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-contract-negative-tools");
        (string Name, string Area, string File, string OldText, string NewText)[] cases =
        [
            (
                "three-allocation-count",
                "Boot",
                "M01LivePagingIntegration.cs",
                "public const ulong RequiredAllocationRecordCount = 3;",
                "public const ulong RequiredAllocationRecordCount = 2;"),
            (
                "distinct-protection-allocation",
                "Boot",
                "M01LivePagingIntegration.cs",
                "public const ulong ProtectionProbeAllocationRecordIndex = 1;",
                "public const ulong ProtectionProbeAllocationRecordIndex = 0;"),
            (
                "table-state-allocation",
                "Boot",
                "M01LivePagingIntegration.cs",
                "public const ulong TableStateAllocationRecordIndex = 2;",
                "public const ulong TableStateAllocationRecordIndex = 1;"),
            (
                "non-image-map-count",
                "Boot",
                "M01LivePagingIntegration.cs",
                "public const ulong RequiredNonImageMappingRequirementCount = 10;",
                "public const ulong RequiredNonImageMappingRequirementCount = 9;"),
            (
                "allocator-arena",
                "Boot",
                "M01LivePagingIntegration.cs",
                "BootReservationKind.AllocatorArena",
                "BootReservationKind.LoadedImage"),
            (
                "page-table-arena",
                "Boot",
                "M01LivePagingIntegration.cs",
                "BootReservationKind.PageTableArena",
                "BootReservationKind.LoadedImage"),
            (
                "post-allocation-map-order",
                "Boot",
                "M01LivePagingIntegration.cs",
                "_mapCaptureOrdinal > _lastAllocationOrdinal",
                "_mapCaptureOrdinal >= _lastAllocationOrdinal"),
            (
                "post-map-ledger-order",
                "Boot",
                "M01LivePagingIntegration.cs",
                "_ledgerBuildOrdinal > _mapCaptureOrdinal",
                "_ledgerBuildOrdinal >= _mapCaptureOrdinal"),
            (
                "firmware-callback-exclusion",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "public const bool InvokesFirmwareCallbacks = false;",
                "public const bool InvokesFirmwareCallbacks = true;"),
            (
                "arbitrary-callback-exclusion",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "public const bool InvokesArbitraryCallbacks = false;",
                "public const bool InvokesArbitraryCallbacks = true;"),
            (
                "firmware-call-exclusion",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "public const bool CallsFirmware = false;",
                "public const bool CallsFirmware = true;"),
            (
                "restore-order",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "RestoreCr3ThenCr4ThenRflags",
                "RestoreCr4ThenCr3ThenRflags"),
            (
                "firmware-cr3-restoration",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "RestoreFirmwareCr3",
                "RetainOwnedCr3"),
            (
                "post-restore-canary",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "PostRestoreCanaryMismatch",
                "PostRestoreCanaryIgnored"),
            (
                "expected-canary-state",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "StateExpectedCanaryOffset",
                "StateExpectedValueOffset"),
            (
                "cr4-restoration-readback",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "Cr4RestoreReadbackMismatch",
                "Cr4RestoreIgnored"),
            (
                "rflags-restoration-readback",
                "Boot",
                "M01OwnedCr3CompositeTransition.cs",
                "RflagsRestoreReadbackMismatch",
                "RflagsRestoreIgnored")
        ];

        foreach ((string name, string area, string file, string oldText, string newText) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(
                tempRoot,
                "m01-contract-negative-" + name);
            ReplaceAllRequired(SourcePath(source, area, file), oldText, newText);
            if (!ExpectM01SourceClosureFailure(
                    tools,
                    source,
                    "source_contract_missing"))
            {
                Console.WriteLine("M01 semantic-contract case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool TestM01SerializerTargetsImportMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m01-serializer-import-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate, string Reason)[] cases =
        [
            (
                "missing-import",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "\t<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />\n",
                    string.Empty),
                "serializer_import_count_mismatch"),
            (
                "duplicate-import",
                source => InsertBeforeProjectEnd(
                    source.ProjectPath,
                    "  <Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />\n"),
                "serializer_import_count_mismatch"),
            (
                "conditional-import",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />",
                    "<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" Condition=\"Exists('future.targets')\" />"),
                "serializer_import_shape_invalid"),
            (
                "property-expanded-import",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets",
                    "$(FutureTargets)"),
                "serializer_import_shape_invalid"),
            (
                "wildcard-import",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "OS.X64TextSerializerGenerator.targets",
                    "*.targets"),
                "serializer_import_shape_invalid"),
            (
                "wrong-import-path",
                source => ReplaceOnce(
                    source.ProjectPath,
                    "OS.X64TextSerializerGenerator.targets",
                    "Alternate.targets"),
                "serializer_import_shape_invalid"),
            (
                "missing-targets",
                source => File.Delete(source.SerializerTargetsPath),
                "serializer_targets_missing"),
            (
                "project-dtd",
                source => File.WriteAllText(
                    source.ProjectPath,
                    "<!DOCTYPE Project [<!ENTITY injected \"unsafe\">]>\n" +
                    File.ReadAllText(source.ProjectPath),
                    new UTF8Encoding(false)),
                "project_malformed"),
            (
                "malformed-targets",
                source => File.WriteAllText(
                    source.SerializerTargetsPath,
                    "<Project>",
                    new UTF8Encoding(false)),
                "serializer_targets_malformed"),
            (
                "targets-dtd",
                source => File.WriteAllText(
                    source.SerializerTargetsPath,
                    "<!DOCTYPE Project [<!ENTITY injected \"unsafe\">]>\n" +
                    File.ReadAllText(source.SerializerTargetsPath),
                    new UTF8Encoding(false)),
                "serializer_targets_malformed"),
            (
                "hidden-generator-source",
                source =>
                {
                    string hiddenDirectory = Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        ".hidden");
                    Directory.CreateDirectory(hiddenDirectory);
                    File.WriteAllText(
                        Path.Combine(hiddenDirectory, "Evil.cs"),
                        "internal static class Evil { }\n",
                        new UTF8Encoding(false));
                },
                "serializer_generator_compile_count_mismatch"),
            (
                "generator-project-reference",
                source => InsertBeforeProjectEnd(
                    Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        "OS.X64TextSerializerGenerator.csproj"),
                    "  <ItemGroup><ProjectReference Include=\"..\\OS.ImageBuilder\\OS.ImageBuilder.csproj\" /></ItemGroup>\n"),
                "serializer_build_input_shape_invalid"),
            (
                "generator-package-reference",
                source => InsertBeforeProjectEnd(
                    Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        "OS.X64TextSerializerGenerator.csproj"),
                    "  <ItemGroup><PackageReference Include=\"Future.Generator\" Version=\"1.0.0\" /></ItemGroup>\n"),
                "serializer_build_input_shape_invalid"),
            (
                "generator-analyzer",
                source => InsertBeforeProjectEnd(
                    Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        "OS.X64TextSerializerGenerator.csproj"),
                    "  <ItemGroup><Analyzer Include=\"future-generator.dll\" /></ItemGroup>\n"),
                "serializer_build_input_shape_invalid"),
            (
                "tools-props-source-generator",
                source => InsertBeforeProjectEnd(
                    Path.Combine(
                        Directory.GetParent(
                            Path.GetDirectoryName(source.SerializerTargetsPath)!)!
                            .FullName,
                        "Directory.Build.props"),
                    "  <ItemGroup><Analyzer Include=\"future-source-generator.dll\" /></ItemGroup>\n"),
                "serializer_build_input_shape_invalid"),
            (
                "generator-local-props",
                source => File.WriteAllText(
                    Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        "Directory.Build.props"),
                    "<Project />\n",
                    new UTF8Encoding(false)),
                "unexpected_build_customization"),
            (
                "generator-local-targets",
                source => File.WriteAllText(
                    Path.Combine(
                        Path.GetDirectoryName(source.SerializerTargetsPath)!,
                        "Directory.Build.targets"),
                    "<Project />\n",
                    new UTF8Encoding(false)),
                "unexpected_build_customization"),
            (
                "tools-directory-targets",
                source => File.WriteAllText(
                    Path.Combine(
                        Directory.GetParent(
                            Path.GetDirectoryName(source.SerializerTargetsPath)!)!
                            .FullName,
                        "Directory.Build.targets"),
                    "<Project />\n",
                    new UTF8Encoding(false)),
                "unexpected_build_customization"),
            (
                "compile-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <ItemGroup><Compile Include=\"future.cs\" /></ItemGroup>\n"),
                "serializer_targets_input_injection"),
            (
                "project-reference-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <ItemGroup><ProjectReference Include=\"future.csproj\" /></ItemGroup>\n"),
                "serializer_targets_input_injection"),
            (
                "reference-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <ItemGroup><Reference Include=\"Future\" /></ItemGroup>\n"),
                "serializer_targets_input_injection"),
            (
                "analyzer-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <ItemGroup><Analyzer Include=\"future.dll\" /></ItemGroup>\n"),
                "serializer_targets_input_injection"),
            (
                "using-task-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <UsingTask TaskName=\"Future\" AssemblyFile=\"future.dll\" />\n"),
                "serializer_targets_input_injection"),
            (
                "output-injection",
                source => InsertBeforeProjectEnd(
                    source.SerializerTargetsPath,
                    "  <Target Name=\"Future\"><FutureTask><Output TaskParameter=\"Files\" ItemName=\"Compile\" /></FutureTask></Target>\n"),
                "serializer_targets_output_injection"),
            (
                "target-shape-mutation",
                source => ReplaceOnce(
                    source.SerializerTargetsPath,
                    "<ClrosX64InstructionPublicationObjectBytes>336</ClrosX64InstructionPublicationObjectBytes>",
                    "<ClrosX64InstructionPublicationObjectBytes>337</ClrosX64InstructionPublicationObjectBytes>"),
                "serializer_targets_shape_invalid")
        ];

        foreach ((string name, Action<SourceClosureFixture> mutate, string reason) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(
                tempRoot,
                "m01-serializer-import-negative-" + name);
            mutate(source);
            bool passed = name == "missing-targets"
                ? ExpectM01SourceSnapshotFailure(
                    tools,
                    source,
                    "OS.X64TextSerializerGenerator.targets")
                : ExpectM01SourceClosureFailure(tools, source, reason);
            if (!passed)
            {
                Console.WriteLine("M01 serializer-import case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool TestM01PublishSourceAndSdkClosure()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string script = File.ReadAllText(scriptPath);

        int snapshotIndex = script.IndexOf(
            "$sourceSnapshotBeforePublish = Get-M01PublishSourceSnapshot",
            StringComparison.Ordinal);
        int preflightLogSaveIndex = script.LastIndexOf(
            "Save-EfiPreflightLog -Path $logFullPath",
            StringComparison.Ordinal);
        int lockIndex = script.IndexOf(
            "$sourceLocks = Open-M01PublishSourceLocks",
            StringComparison.Ordinal);
        int lockedValidationIndex = script.IndexOf(
            "$m01LockedSourceClosure = Test-M01SourceClosure",
            StringComparison.Ordinal);
        int initialValidationIndex = script.IndexOf(
            "$m01SourceClosure = Test-M01SourceClosure",
            StringComparison.Ordinal);
        int outputLockIndex = script.IndexOf(
            "$publishOutputLock = Open-M01PublishOutputLock",
            StringComparison.Ordinal);
        int freshOutputIndex = script.IndexOf(
            "EFI_PUBLISH_FRESH_OUTPUTS status=ok",
            StringComparison.Ordinal);
        int restoreIndex = script.IndexOf(
            "-Operation \"isolated-restore\"",
            StringComparison.Ordinal);
        int restoreLockIndex = script.IndexOf(
            "$restoreLocks = Open-M01PublishSourceLocks",
            StringComparison.Ordinal);
        int restorePostPublishIndex = script.IndexOf(
            "$restoreSnapshotAfterPublish = Get-M01ExactRestoreClosureSnapshot",
            StringComparison.Ordinal);
        int publishIndex = script.LastIndexOf(
            "-Operation \"nativeaot-publish\"",
            StringComparison.Ordinal);
        int verifyIndex = script.IndexOf(
            "$verificationResult = Invoke-M01ArtifactVerifier",
            StringComparison.Ordinal);
        int closeIndex = script.LastIndexOf(
            "Close-M01PublishSourceLocks -Locks $sourceLocks",
            StringComparison.Ordinal);
        int restoreCloseIndex = script.LastIndexOf(
            "Close-M01PublishSourceLocks -Locks $restoreLocks",
            StringComparison.Ordinal);

        return script.Contains("\"global.json\"", StringComparison.Ordinal)
            && script.Contains(
                "\"scripts\\build-efi-shell.ps1\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"scripts\\test-efi-artifact.ps1\"",
                StringComparison.Ordinal)
            && script.Contains(
                "[System.IO.FileShare]::Read",
                StringComparison.Ordinal)
            && script.Contains(
                "source_changed_before_lock",
                StringComparison.Ordinal)
            && script.Contains(
                "$sourceSnapshotAfterPublish = Get-M01PublishSourceSnapshot",
                StringComparison.Ordinal)
            && script.Contains(
                "dotnetHostPath = $script:DotnetHostPath",
                StringComparison.Ordinal)
            && script.Contains(
                "dotnetHostSha256 = $dotnetHostHash",
                StringComparison.Ordinal)
            && script.Contains(
                "dotnetSdkVersion = $dotnetSdkVersion",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:NoIncremental=true\"",
                StringComparison.Ordinal)
            && Regex.Matches(
                script,
                "\"-noAutoResponse\"",
                RegexOptions.CultureInvariant).Count >= 4
            && script.Contains(
                "\"-p:DirectoryBuildPropsPath=\" + $canonicalRootPropsPath",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:DirectoryBuildTargetsPath=\" + $canonicalRootTargetsPath",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_M01_SOURCE_CLOSURE_LOCKED status=ok",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_RESTORE_CLOSURE_LOCKED status=ok",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:MSBuildProjectExtensionsPath=\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:ProjectAssetsFile=\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:ImportProjectExtensionProps=false\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:ImportProjectExtensionTargets=false\"",
                StringComparison.Ordinal)
            && !script.Contains(
                "\"-p:ImportProjectExtensionProps=true\"",
                StringComparison.Ordinal)
            && !script.Contains(
                "\"-p:ImportProjectExtensionTargets=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:ExcludeRestorePackageImports=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "function Test-M01RestoreSemanticClosure",
                StringComparison.Ordinal)
            && script.Contains(
                "function Get-M01ConsumedPackageClosureSnapshot",
                StringComparison.Ordinal)
            && script.Contains(
                "function Get-M01PackageBuildClosureSnapshot",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_PINNED_RESTORE_SOURCE status=ok",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_PACKAGE_CLOSURE_LOCKED status=ok",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_POST_RESTORE_EVALUATION status=ok",
                StringComparison.Ordinal)
            && script.Contains(
                "schemaVersion = 3",
                StringComparison.Ordinal)
            && script.Contains(
                "packageClosureSha256 =",
                StringComparison.Ordinal)
            && script.Contains(
                "packageBuildClosureSha256 =",
                StringComparison.Ordinal)
            && script.Contains(
                "postRestoreEvaluation = $postRestoreEvaluation",
                StringComparison.Ordinal)
            && script.Contains(
                "\"-p:RestoreForceEvaluate=true\"",
                StringComparison.Ordinal)
            && script.Contains("\"--no-restore\"", StringComparison.Ordinal)
            && script.Contains(
                "restoreClosureSha256 =",
                StringComparison.Ordinal)
            && script.Contains(
                "restoreClosureInputs =",
                StringComparison.Ordinal)
            && script.Contains(
                "projectAssetsSha256 = $projectAssetsHash",
                StringComparison.Ordinal)
            && script.Contains(
                "function Remove-M01FreshPublishDirectory",
                StringComparison.Ordinal)
            && script.Contains(
                "function Open-M01PublishOutputLock",
                StringComparison.Ordinal)
            && script.Contains(
                "publish_output_lock_failed",
                StringComparison.Ordinal)
            && script.Contains(
                "fresh_output_cleanup_failed",
                StringComparison.Ordinal)
            && script.Contains(
                "[System.IO.FileAttributes]::ReparsePoint",
                StringComparison.Ordinal)
            && script.Contains(
                "function Stop-M01ProcessTree",
                StringComparison.Ordinal)
            && script.Contains(
                "CLROS_M01_VERIFIER_STARTUP_GATE",
                StringComparison.Ordinal)
            && script.IndexOf(
                "$processJob.Assign($process)",
                StringComparison.Ordinal) <
                script.IndexOf("$startupGate.Set()", StringComparison.Ordinal)
            && script.Contains(
                "artifact_verification_timeout",
                StringComparison.Ordinal)
            && script.Contains(
                "[int] $ArtifactVerifierTimeoutSeconds = 60",
                StringComparison.Ordinal)
            && outputLockIndex >= 0
            && preflightLogSaveIndex > outputLockIndex
            && snapshotIndex > outputLockIndex
            && lockIndex > snapshotIndex
            && initialValidationIndex > lockIndex
            && lockedValidationIndex > lockIndex
            && freshOutputIndex > lockedValidationIndex
            && restoreIndex > freshOutputIndex
            && restoreLockIndex > restoreIndex
            && publishIndex > lockIndex
            && publishIndex > lockedValidationIndex
            && publishIndex > restoreLockIndex
            && restorePostPublishIndex > publishIndex
            && verifyIndex > publishIndex
            && restoreCloseIndex > verifyIndex
            && closeIndex > restoreCloseIndex;
    }

    private static bool TestM01PinnedRestoreAndPackageAuthority()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string script = File.ReadAllText(scriptPath);

        IReadOnlyList<IReadOnlyDictionary<string, string>> restoreArchives =
            ParsePowerShellLiteralRecords(
                ExtractPowerShellBlock(
                    script,
                    "$script:ExpectedRestoreArchives = @(",
                    "$script:ExpectedM01ReviewedSourceIncludes"));
        IReadOnlyList<IReadOnlyDictionary<string, string>> packageTrees =
            ParsePowerShellLiteralRecords(
                ExtractPowerShellBlock(
                    script,
                    "$script:ExpectedConsumedPackageTrees = @(",
                    "$script:ExpectedActivePackageImportEdges"));
        IReadOnlyList<IReadOnlyDictionary<string, string>> buildInputs =
            ParsePowerShellLiteralRecords(
                ExtractPowerShellBlock(
                    script,
                    "$script:ExpectedPackageBuildClosure = @(",
                    "$script:ExpectedRestoreLibraries"));

        string[] expectedArchives =
        [
            "microsoft.dotnet.ilcompiler|10.0.12|" +
                "microsoft.dotnet.ilcompiler.10.0.12.nupkg|" +
                "0b340ade975c2cf3d93d518adddec1e5d910c090b7756e6d37d19b772edc7b8d",
            "microsoft.net.illink.tasks|10.0.12|" +
                "microsoft.net.illink.tasks.10.0.12.nupkg|" +
                "37aa984da51fdc01cc7d57a8647893392f3539cdd5a1d32ce4982be5309bc08b",
            "runtime.win-x64.microsoft.dotnet.ilcompiler|10.0.12|" +
                "runtime.win-x64.microsoft.dotnet.ilcompiler.10.0.12.nupkg|" +
                "7f63d47aea10cd7ac3dd7a2ca04474c7cb683e36ef86b49190382b0c891fbe99",
            "microsoft.netcore.app.runtime.nativeaot.win-x64|10.0.12|" +
                "microsoft.netcore.app.runtime.nativeaot.win-x64.10.0.12.nupkg|" +
                "94e38c861bdd0ef6e5f04492538b66ada2f30876907363a61aa9f03f42fb3406"
        ];
        string[] actualArchives = restoreArchives
            .Select(record =>
                ReadRecordValue(record, "Id") + "|" +
                ReadRecordValue(record, "Version") + "|" +
                ReadRecordValue(record, "FileName") + "|" +
                ReadRecordValue(record, "Sha256"))
            .ToArray();

        string[] expectedPackageTrees =
        [
            "microsoft.dotnet.ilcompiler|10.0.12|restore",
            "microsoft.net.illink.tasks|10.0.12|restore",
            "runtime.win-x64.microsoft.dotnet.ilcompiler|10.0.12|restore",
            "microsoft.netcore.app.runtime.nativeaot.win-x64|10.0.12|restore-only",
            "microsoft.netcore.app.runtime.nativeaot.win-x64|10.0.12|runtime"
        ];
        string[] actualPackageTrees = packageTrees
            .Select(record =>
                ReadRecordValue(record, "Id") + "|" +
                ReadRecordValue(record, "Version") + "|" +
                ReadRecordValue(record, "RootKind"))
            .ToArray();
        bool packageTreeEvidenceComplete = packageTrees.All(record =>
            IsLowerHex(ReadRecordValue(record, "AggregateSha256"), 64) &&
            !string.IsNullOrWhiteSpace(
                ReadRecordValue(record, "ContentHashSha512")) &&
            int.TryParse(
                ReadRecordValue(record, "FileCount"),
                out int fileCount) &&
            fileCount > 0 &&
            long.TryParse(
                ReadRecordValue(record, "TotalBytes"),
                out long totalBytes) &&
            totalBytes > 0);

        string[] expectedBuildInputs =
        [
            "microsoft.dotnet.ilcompiler/10.0.12/build/" +
                "Microsoft.DotNet.ILCompiler.SingleEntry.targets",
            "microsoft.dotnet.ilcompiler/10.0.12/build/" +
                "Microsoft.NETCore.Native.Publish.targets",
            "microsoft.dotnet.ilcompiler/10.0.12/build/" +
                "Microsoft.NETCore.Native.Windows.targets",
            "microsoft.dotnet.ilcompiler/10.0.12/build/" +
                "Microsoft.NETCore.Native.targets",
            "microsoft.dotnet.ilcompiler/10.0.12/tools/netstandard/" +
                "ILCompiler.Build.Tasks.dll",
            "microsoft.dotnet.ilcompiler/10.0.12/tools/netstandard/" +
                "ILCompiler.Build.Tasks.deps.json",
            "microsoft.net.illink.tasks/10.0.12/build/" +
                "Microsoft.NET.ILLink.targets",
            "microsoft.net.illink.tasks/10.0.12/tools/net/" +
                "ILLink.Tasks.dll",
            "microsoft.net.illink.tasks/10.0.12/tools/net/" +
                "ILLink.Tasks.deps.json"
        ];
        string[] actualBuildInputs = buildInputs
            .Select(record => ReadRecordValue(record, "Path"))
            .ToArray();
        bool buildInputHashesComplete = buildInputs.All(record =>
            IsLowerHex(ReadRecordValue(record, "Sha256"), 64));

        string projectExtensionArguments = ExtractPowerShellBlock(
            script,
            "$projectExtensionArguments = @(",
            "$pinnedPackageArguments");
        string pinnedPackageArguments = ExtractPowerShellBlock(
            script,
            "$pinnedPackageArguments = @(",
            "$arguments = @(");
        string restoreArguments = ExtractPowerShellBlock(
            script,
            "$restoreArguments = @(",
            "$nativeBinaryPath");
        string restoreSemanticValidation = ExtractPowerShellBlock(
            script,
            "function Test-M01RestoreSemanticClosure",
            "function Get-M01PackageBuildClosureSnapshot");
        string packageTreeSnapshot = ExtractPowerShellBlock(
            script,
            "function Get-M01ConsumedPackageClosureSnapshot",
            "function Open-M01AbsoluteInputLocks");
        string pinnedRestoreSource = ExtractPowerShellBlock(
            script,
            "function New-M01PinnedRestoreSource",
            "function Get-M01ConsumedPackageClosureSnapshot");
        string finallyBlock = ExtractPowerShellBlock(
            script,
            "finally {\n    Close-M01AuthorityRecords -Records $verifierInputLocks",
            "\n}\n");

        int restoreInvocationIndex = script.IndexOf(
            "$restoreResult = Invoke-M01BoundedProcess `",
            StringComparison.Ordinal);
        int restoreSemanticIndex = script.IndexOf(
            "Test-M01RestoreSemanticClosure `",
            restoreInvocationIndex,
            StringComparison.Ordinal);
        int packageSnapshotIndex = script.IndexOf(
            "$packageClosureSnapshotBeforeLock =",
            restoreSemanticIndex,
            StringComparison.Ordinal);
        int packageLockIndex = script.IndexOf(
            "$packageLocks = Open-M01AbsoluteInputLocks",
            packageSnapshotIndex,
            StringComparison.Ordinal);
        int packageLockedSnapshotIndex = script.IndexOf(
            "$packageClosureSnapshotAfterLock =",
            packageLockIndex,
            StringComparison.Ordinal);
        int postRestoreEvaluationIndex = script.IndexOf(
            "$postRestoreEvaluation = Invoke-M01PostRestoreEvaluation",
            packageLockedSnapshotIndex,
            StringComparison.Ordinal);
        int publishIndex = script.LastIndexOf(
            "$publishResult = Invoke-M01BoundedProcess `",
            StringComparison.Ordinal);
        int postPublishPackageSnapshotIndex = script.IndexOf(
            "$packageClosureSnapshotAfterPublish =",
            publishIndex,
            StringComparison.Ordinal);
        int postPublishComparisonIndex = script.IndexOf(
            "$packageClosureSnapshotBeforeLock.AggregateSha256 -cne",
            postPublishPackageSnapshotIndex,
            StringComparison.Ordinal);

        string[] expectedReleaseOrder =
        [
            "$packageLocks",
            "$restoreLocks",
            "$restoreSourceLocks",
            "$restoreArchiveSourceLocks",
            "$sourceLocks"
        ];
        string[] actualReleaseOrder = Regex.Matches(
                finallyBlock,
                @"Close-M01PublishSourceLocks -Locks (?<lock>\$\w+)",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups["lock"].Value)
            .ToArray();

        bool passed =
            actualArchives.SequenceEqual(
                expectedArchives,
                StringComparer.Ordinal)
            && actualPackageTrees.SequenceEqual(
                expectedPackageTrees,
                StringComparer.Ordinal)
            && packageTreeEvidenceComplete
            && actualBuildInputs.SequenceEqual(
                expectedBuildInputs,
                StringComparer.Ordinal)
            && buildInputHashesComplete
            && CountOrdinal(
                projectExtensionArguments,
                "\"-p:ImportProjectExtensionProps=false\"") == 1
            && CountOrdinal(
                projectExtensionArguments,
                "\"-p:ImportProjectExtensionTargets=false\"") == 1
            && CountOrdinal(
                projectExtensionArguments,
                "\"-p:ExcludeRestorePackageImports=true\"") == 1
            && !projectExtensionArguments.Contains(
                "ImportProjectExtensionProps=true",
                StringComparison.Ordinal)
            && !projectExtensionArguments.Contains(
                "ImportProjectExtensionTargets=true",
                StringComparison.Ordinal)
            && restoreArguments.Contains(
                "\"--configfile\", $restoreConfigPath",
                StringComparison.Ordinal)
            && restoreArguments.Contains(
                "\"--packages\", $restorePackagesDirectory",
                StringComparison.Ordinal)
            && restoreArguments.EndsWith(
                ") + $projectExtensionArguments\n",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += $projectExtensionArguments",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += $pinnedPackageArguments",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += \"--no-restore\"",
                StringComparison.Ordinal)
            && pinnedPackageArguments.Contains(
                "\"-p:ILCompilerTargetsPath=\" + $ilCompilerTargetsPath",
                StringComparison.Ordinal)
            && pinnedPackageArguments.Contains(
                "\"-p:ILLinkTasksAssembly=\" + $ilLinkTasksAssemblyPath",
                StringComparison.Ordinal)
            && pinnedPackageArguments.Contains(
                "\"-p:IlcBuildTasksPath=\" + $ilcBuildTasksPath",
                StringComparison.Ordinal)
            && pinnedPackageArguments.Contains(
                "\"-p:IlcHostPackagePath=\" + $ilCompilerHostPackageRoot",
                StringComparison.Ordinal)
            && pinnedPackageArguments.Contains(
                "\"-p:RuntimePackagePath=\" + $resolvedNativeAotRuntimePackPath",
                StringComparison.Ordinal)
            && pinnedRestoreSource.Contains("<clear />", StringComparison.Ordinal)
            && CountOrdinal(pinnedRestoreSource, "<add key=") == 1
            && pinnedRestoreSource.Contains(
                "key=`\"CLROS_PINNED`\"",
                StringComparison.Ordinal)
            && pinnedRestoreSource.Contains(
                "[System.Security.SecurityElement]::Escape",
                StringComparison.Ordinal)
            && restoreSemanticValidation.Contains(
                "local-name()='UsingTask'",
                StringComparison.Ordinal)
            && restoreSemanticValidation.Contains(
                "restore generated targets must be empty",
                StringComparison.Ordinal)
            && restoreSemanticValidation.Contains(
                "restore project assets library unexpected",
                StringComparison.Ordinal)
            && restoreSemanticValidation.Contains(
                "restore project path contract invalid",
                StringComparison.Ordinal)
            && packageTreeSnapshot.Contains(
                "isolated restore package version set invalid",
                StringComparison.Ordinal)
            && packageTreeSnapshot.Contains(
                "consumed package tree mismatch",
                StringComparison.Ordinal)
            && packageTreeSnapshot.Contains(
                ".Name -cne \".nupkg.metadata\"",
                StringComparison.Ordinal)
            && script.Contains(
                "schemaVersion = 3",
                StringComparison.Ordinal)
            && script.Contains(
                "packageRootKind = \"isolated\"",
                StringComparison.Ordinal)
            && script.Contains(
                "ilCompilerPath = $actualIlCompilerPath",
                StringComparison.Ordinal)
            && script.Contains(
                "$actualIlCompilerPath = Join-Path (\n" +
                    "    $ilCompilerHostPackageRoot) \"tools\\ilc.exe\"",
                StringComparison.Ordinal)
            && restoreInvocationIndex >= 0
            && restoreSemanticIndex > restoreInvocationIndex
            && packageSnapshotIndex > restoreSemanticIndex
            && packageLockIndex > packageSnapshotIndex
            && packageLockedSnapshotIndex > packageLockIndex
            && postRestoreEvaluationIndex > packageLockedSnapshotIndex
            && publishIndex > postRestoreEvaluationIndex
            && postPublishPackageSnapshotIndex > publishIndex
            && postPublishComparisonIndex > postPublishPackageSnapshotIndex
            && actualReleaseOrder.SequenceEqual(
                expectedReleaseOrder,
                StringComparer.Ordinal)
            && finallyBlock.Contains(
                "Close-M01PublishOutputLock -Lock $publishOutputLock",
                StringComparison.Ordinal);

        if (!passed)
        {
            Console.WriteLine(
                "M01 package authority shape mismatch: archives=" +
                string.Join(",", actualArchives) +
                " packages=" +
                string.Join(",", actualPackageTrees) +
                " buildInputs=" +
                string.Join(",", actualBuildInputs) +
                " releases=" +
                string.Join(",", actualReleaseOrder));
        }

        return passed;
    }

    private static bool TestM01AbsolutePackageInputLocks(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string caseRoot = Path.Combine(
            tempRoot,
            "m01 absolute package locks with spaces");
        Directory.CreateDirectory(caseRoot);
        string wrapperPath = Path.Combine(caseRoot, "package locks.ps1");

        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $CaseRoot
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                exit 60
            }
            foreach ($functionName in @(
                    "Open-M01AbsoluteInputLocks",
                    "Close-M01PublishSourceLocks")) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    exit 61
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }

            $targetsPath = Join-Path $CaseRoot "Native.targets"
            $taskPath = Join-Path $CaseRoot "Build.Tasks.dll"
            [System.IO.File]::WriteAllText($targetsPath, "targets-before")
            [System.IO.File]::WriteAllText($taskPath, "task-before")
            $inputs = @(
                [pscustomobject]@{ fullPath = $targetsPath },
                [pscustomobject]@{ fullPath = $taskPath })
            $locks = Open-M01AbsoluteInputLocks -Inputs $inputs
            try {
                foreach ($path in @($targetsPath, $taskPath)) {
                    $writeRejected = $false
                    try {
                        [System.IO.File]::WriteAllText($path, "during")
                    }
                    catch [System.IO.IOException] {
                        $writeRejected = $true
                    }
                    $moveRejected = $false
                    try {
                        [System.IO.File]::Move($path, $path + ".moved")
                    }
                    catch [System.IO.IOException] {
                        $moveRejected = $true
                    }
                    if (-not $writeRejected -or -not $moveRejected) {
                        exit 62
                    }
                }
            }
            finally {
                Close-M01PublishSourceLocks -Locks $locks
            }

            [System.IO.File]::WriteAllText($targetsPath, "targets-after")
            [System.IO.File]::WriteAllText($taskPath, "task-after")
            if ([System.IO.File]::ReadAllText($targetsPath) -ne
                    "targets-after" -or
                [System.IO.File]::ReadAllText($taskPath) -ne "task-after") {
                exit 63
            }
            Write-Output "M01_ABSOLUTE_PACKAGE_LOCKS_PASS"
            exit 0
            """,
            new UTF8Encoding(false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 15_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-CaseRoot",
            caseRoot);
        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_ABSOLUTE_PACKAGE_LOCKS_PASS",
                StringComparison.Ordinal);
    }

    private static bool TestM01ProjectExtensionEvaluationIsolation(
        string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m01-project-extension-isolation-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(
            tempRoot,
            "m01-project-extension-isolation");
        string staleObjRoot = Path.Combine(
            source.Root,
            "src",
            "OS",
            "obj");
        Directory.CreateDirectory(staleObjRoot);
        File.WriteAllText(
            Path.Combine(staleObjRoot, "OS.csproj.nuget.g.props"),
            """
            <Project>
              <PropertyGroup>
                <NoStdLib>false</NoStdLib>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="stale-injected.cs" />
              </ItemGroup>
            </Project>
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(staleObjRoot, "OS.csproj.nuget.g.targets"),
            """
            <Project InitialTargets="StaleInjectedTarget">
              <Target Name="StaleInjectedTarget">
                <Error Text="stale project-extension target executed" />
              </Target>
            </Project>
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(staleObjRoot, "project.assets.json"),
            "{\"stale\":true}",
            new UTF8Encoding(false));

        ProcessResult result = RunPreflight(
            tools,
            useSearchRoots: false,
            scriptPath: source.ScriptPath);
        return IsM01SourceClosureSuccess(
            result,
            "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && !result.Output.Contains(
                "stale project-extension target executed",
                StringComparison.Ordinal);
    }

    private static bool TestM01BoundedBuildProcessRunner(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string script = File.ReadAllText(buildScriptPath);
        string caseRoot = Path.Combine(
            tempRoot,
            "m01 bounded build runner with spaces");
        Directory.CreateDirectory(caseRoot);
        string positivePath = Path.Combine(caseRoot, "positive.ps1");
        string overflowPath = Path.Combine(caseRoot, "overflow.ps1");
        string lockerPath = Path.Combine(caseRoot, "locker.ps1");
        string timeoutPath = Path.Combine(caseRoot, "timeout.ps1");
        string wrapperPath = Path.Combine(caseRoot, "runner wrapper.ps1");
        string childPidPath = Path.Combine(caseRoot, "child pid.txt");
        string sentinelPath = Path.Combine(caseRoot, "sentinel.lock");

        File.WriteAllText(
            positivePath,
            """
            Write-Output (
                "M01_BOUNDED_POSITIVE childNoReuse=" +
                $env:MSBUILDDISABLENODEREUSE +
                " server=" +
                $env:DOTNET_CLI_USE_MSBUILD_SERVER)
            exit 0
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            overflowPath,
            """
            Write-Output ("X" * 8192)
            Start-Sleep -Seconds 30
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            lockerPath,
            """
            param([string] $SentinelPath, [string] $ChildPidPath)
            $stream = [System.IO.File]::Open(
                $SentinelPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            try {
                [System.IO.File]::WriteAllText(
                    $ChildPidPath,
                    $PID.ToString(),
                    (New-Object System.Text.UTF8Encoding($false)))
                Start-Sleep -Seconds 30
            }
            finally {
                $stream.Dispose()
            }
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            timeoutPath,
            """
            param(
                [string] $LockerPath,
                [string] $SentinelPath,
                [string] $ChildPidPath
            )
            $child = Start-Process `
                -FilePath (
                    Join-Path $env:SystemRoot (
                        "System32\WindowsPowerShell\v1.0\powershell.exe")) `
                -ArgumentList @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    ('"' + $LockerPath + '"'),
                    "-SentinelPath",
                    ('"' + $SentinelPath + '"'),
                    "-ChildPidPath",
                    ('"' + $ChildPidPath + '"')
                ) `
                -PassThru `
                -WindowStyle Hidden
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not (Test-Path -LiteralPath $ChildPidPath -PathType Leaf)) {
                if ([DateTime]::UtcNow -ge $deadline) {
                    exit 91
                }
                Start-Sleep -Milliseconds 25
            }
            Write-Output ("M01_BOUNDED_TIMEOUT_READY childPid=" + $child.Id)
            Start-Sleep -Seconds 30
            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $CaseRoot,
                [string] $PositivePath,
                [string] $OverflowPath,
                [string] $LockerPath,
                [string] $TimeoutPath,
                [string] $SentinelPath,
                [string] $ChildPidPath
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                Write-Output "M01_BOUNDED_RUNNER_FAIL parse"
                exit 10
            }
            foreach ($functionName in @(
                    "New-M01ProcessJob",
                    "Stop-M01ProcessTree",
                    "Get-M01BoundedProcessFailureReason",
                    "Invoke-M01BoundedProcess"
                )) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    Write-Output (
                        "M01_BOUNDED_RUNNER_FAIL function=" +
                        $functionName)
                    exit 11
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }
            $powershellPath = Join-Path $env:SystemRoot (
                "System32\WindowsPowerShell\v1.0\powershell.exe")
            $positive = Invoke-M01BoundedProcess `
                -FilePath $powershellPath `
                -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    $PositivePath
                ) `
                -WorkingDirectory $CaseRoot `
                -Operation "positive" `
                -TimeoutSeconds 10 `
                -MaximumOutputCharacters 4096
            if ((Get-M01BoundedProcessFailureReason $positive) -cne "ok" -or
                $positive.OutputCharacters -gt 4096 -or
                $positive.OutputText -cnotmatch
                    'M01_BOUNDED_POSITIVE childNoReuse=1 server=0') {
                Write-Output (
                    "M01_BOUNDED_RUNNER_FAIL positive reason=" +
                    (Get-M01BoundedProcessFailureReason $positive) +
                    " output=" +
                    $positive.OutputText)
                exit 12
            }

            $overflow = Invoke-M01BoundedProcess `
                -FilePath $powershellPath `
                -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    $OverflowPath
                ) `
                -WorkingDirectory $CaseRoot `
                -Operation "overflow" `
                -TimeoutSeconds 10 `
                -MaximumOutputCharacters 512
            if ((Get-M01BoundedProcessFailureReason $overflow) -cne
                    "output_limit_exceeded" -or
                -not $overflow.TerminationConfirmed -or
                $overflow.OutputCharacters -gt 512) {
                Write-Output (
                    "M01_BOUNDED_RUNNER_FAIL overflow reason=" +
                    (Get-M01BoundedProcessFailureReason $overflow) +
                    " chars=" +
                    $overflow.OutputCharacters)
                exit 13
            }

            $timeout = Invoke-M01BoundedProcess `
                -FilePath $powershellPath `
                -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    $TimeoutPath,
                    "-LockerPath",
                    $LockerPath,
                    "-SentinelPath",
                    $SentinelPath,
                    "-ChildPidPath",
                    $ChildPidPath
                ) `
                -WorkingDirectory $CaseRoot `
                -Operation "timeout" `
                -TimeoutSeconds 5 `
                -MaximumOutputCharacters 4096
            if ((Get-M01BoundedProcessFailureReason $timeout) -cne "timeout" -or
                -not $timeout.TerminationConfirmed -or
                -not (Test-Path -LiteralPath $ChildPidPath -PathType Leaf)) {
                Write-Output (
                    "M01_BOUNDED_RUNNER_FAIL timeout reason=" +
                    (Get-M01BoundedProcessFailureReason $timeout))
                exit 14
            }
            $childPid = [int](
                Get-Content -LiteralPath $ChildPidPath -Raw).Trim()
            Start-Sleep -Milliseconds 250
            if ($null -ne (
                    Get-Process -Id $childPid -ErrorAction SilentlyContinue)) {
                Write-Output (
                    "M01_BOUNDED_RUNNER_FAIL child_alive pid=" +
                    $childPid)
                exit 15
            }
            $sentinel = [System.IO.File]::Open(
                $SentinelPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            $sentinel.Dispose()

            $invalidRejected = $false
            try {
                Invoke-M01BoundedProcess `
                    -FilePath $powershellPath `
                    -Arguments @() `
                    -WorkingDirectory $CaseRoot `
                    -Operation "invalid" `
                    -TimeoutSeconds 0 `
                    -MaximumOutputCharacters 4096
            }
            catch [System.Management.Automation.ParameterBindingException] {
                $invalidRejected = $true
            }
            if (-not $invalidRejected) {
                Write-Output "M01_BOUNDED_RUNNER_FAIL invalid_accepted"
                exit 16
            }
            Write-Output "M01_BOUNDED_RUNNER_PASS"
            """,
            new UTF8Encoding(false));

        int directDotnetInvocations = Regex.Matches(
            script,
            @"&\s+\$script:DotnetHostPath",
            RegexOptions.CultureInvariant).Count;
        string[] requiredOperations =
        [
            "source-evaluation-",
            "serializer-generator-evaluation",
            "isolated-restore",
            "post-restore-evaluation",
            "nativeaot-publish",
            "dotnet-sdk-version",
            "dotnet-sdk-recheck"
        ];
        if (directDotnetInvocations != 0 ||
            !script.Contains(
                "[int] $BuildProcessTimeoutSeconds = 300",
                StringComparison.Ordinal) ||
            !script.Contains(
                "[int] $BuildProcessMaximumOutputCharacters = 4194304",
                StringComparison.Ordinal) ||
            requiredOperations.Any(
                operation => !script.Contains(operation, StringComparison.Ordinal)))
        {
            return false;
        }

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 45_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-CaseRoot",
            caseRoot,
            "-PositivePath",
            positivePath,
            "-OverflowPath",
            overflowPath,
            "-LockerPath",
            lockerPath,
            "-TimeoutPath",
            timeoutPath,
            "-SentinelPath",
            sentinelPath,
            "-ChildPidPath",
            childPidPath);
        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "M01_BOUNDED_RUNNER_PASS",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_BOUNDED_RUNNER_FAIL",
                StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(
                "M01 bounded process runner mismatch: exit=" +
                result.ExitCode +
                " output=" +
                result.Output);
        }

        return passed;
    }

    private static bool TestM01MsbuildInjectionIsolation(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string scriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string targetsPath = Path.Combine(
            repositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator",
            "OS.X64TextSerializerGenerator.targets");
        string script = File.ReadAllText(scriptPath);
        string targets = File.ReadAllText(targetsPath);
        string[] wildcardProperties =
        [
            "ImportByWildcardBeforeMicrosoftCommonProps",
            "ImportByWildcardAfterMicrosoftCommonProps",
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps",
            "ImportUserLocationsByWildcardAfterMicrosoftCommonProps",
            "ImportByWildcardBeforeMicrosoftCommonTargets",
            "ImportByWildcardAfterMicrosoftCommonTargets",
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets",
            "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets",
            "ImportByWildcardBeforeMicrosoftCSharpTargets",
            "ImportByWildcardAfterMicrosoftCSharpTargets",
            "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets",
            "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets",
            "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets",
            "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets"
        ];
        foreach (string property in wildcardProperties)
        {
            if (!script.Contains(
                    property + " = \"false\"",
                    StringComparison.Ordinal) ||
                !targets.Contains(
                    property + "=false",
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        if (!script.Contains(
                "MSBuildSDKsPath = $script:VerifiedMsbuildSdksPath",
                StringComparison.Ordinal) ||
            !script.Contains(
                "MSBuildExtensionsPath = $script:VerifiedMsbuildExtensionsPath",
                StringComparison.Ordinal) ||
            !script.Contains(
                "MSBuildUserExtensionsPath = $disabledUserExtensionsPath",
                StringComparison.Ordinal) ||
            !script.Contains(
                "disabled MSBuild user extensions path must remain absent",
                StringComparison.Ordinal) ||
            !targets.Contains(
                "MSBuildSDKsPath=$(MSBuildToolsPath)\\Sdks",
                StringComparison.Ordinal) ||
            !targets.Contains(
                "MSBuildExtensionsPath=$(MSBuildToolsPath)\\;",
                StringComparison.Ordinal) ||
            !targets.Contains(
                "MSBuildUserExtensionsPath=$(_ClrosX64SerializerToolDisabledUserExtensions)",
                StringComparison.Ordinal) ||
            !targets.Contains(
                "UseSharedCompilation=false",
                StringComparison.Ordinal))
        {
            return false;
        }

        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "m01-msbuild-injection-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(
            tempRoot,
            "m01-msbuild-injection-source");
        string logPath = Path.Combine(
            tools.Root,
            "msbuild-injection-" + Guid.NewGuid().ToString("N") + ".log");
        ProcessStartInfo start = new()
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        AddArgument(start, "-NoProfile");
        AddArgument(start, "-ExecutionPolicy", "Bypass");
        AddArgument(start, "-File", source.ScriptPath);
        AddArgument(start, "-PreflightOnly");
        AddArgument(start, "-LogPath", logPath);
        AddArgument(start, "-MsvcLinkerSearchRoot", tools.MsvcLinkerSearchRoot);
        AddArgument(
            start,
            "-WindowsSdkLibSearchRoot",
            tools.WindowsSdkLibSearchRoot);
        AddArgument(start, "-IlCompilerPath", tools.IlCompilerPath);
        AddArgument(
            start,
            "-NativeAotRuntimePackPath",
            tools.NativeAotRuntimePackPath);
        AddArgument(start, "-OvmfCodePath", tools.OvmfCodePath);
        AddArgument(start, "-QemuPath", tools.QemuPath);
        AddArgument(start, "-QemuImgPath", tools.QemuImgPath);
        string poisonRoot = Path.Combine(tempRoot, "poison msbuild roots");
        start.Environment["MSBuildSDKsPath"] =
            Path.Combine(poisonRoot, "sdks");
        start.Environment["MSBuildExtensionsPath"] =
            Path.Combine(poisonRoot, "extensions") +
            Path.DirectorySeparatorChar;
        start.Environment["MSBuildUserExtensionsPath"] =
            Path.Combine(poisonRoot, "user");
        foreach (string property in wildcardProperties)
        {
            start.Environment[property] = "true";
        }
        ProcessResult result = RunProcess(
            start,
            timeoutMilliseconds: 30_000,
            logPath: logPath);
        return IsM01SourceClosureSuccess(
                result,
                "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false")
            && !result.Output.Contains(
                "poison",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestM01ArtifactVerifierTimeout(string tempRoot)
    {
        const int verifierTimeoutSeconds = 5;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string caseRoot = Path.Combine(
            tempRoot,
            "m01 verifier timeout with spaces");
        Directory.CreateDirectory(caseRoot);
        string sleepyVerifierPath = Path.Combine(
            caseRoot,
            "sleepy verifier.ps1");
        string wrapperPath = Path.Combine(
            caseRoot,
            "timeout wrapper.ps1");
        string childPidPath = Path.Combine(
            caseRoot,
            "child pid.txt");
        string verifierPidPath = Path.Combine(
            caseRoot,
            "verifier pid.txt");
        string noKillChildPidPath = Path.Combine(
            caseRoot,
            "no kill child pid.txt");
        string noKillVerifierPidPath = Path.Combine(
            caseRoot,
            "no kill verifier pid.txt");

        File.WriteAllText(
            sleepyVerifierPath,
            """
            param(
                [string] $ChildPidPath,
                [string] $VerifierPidPath
            )
            [System.IO.File]::WriteAllText(
                $VerifierPidPath,
                $PID.ToString(),
                (New-Object System.Text.UTF8Encoding($false)))
            $child = Start-Process `
                -FilePath $env:ComSpec `
                -ArgumentList @(
                    "/d",
                    "/c",
                    "ping -n 60 127.0.0.1 >nul"
                ) `
                -PassThru `
                -WindowStyle Hidden
            [System.IO.File]::WriteAllText(
                $ChildPidPath,
                $child.Id.ToString(),
                (New-Object System.Text.UTF8Encoding($false)))
            Write-Output ("SLEEPY_VERIFIER_READY childPid=" + $child.Id)
            Start-Sleep -Seconds 60
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $SleepyVerifierPath,
                [string] $ChildPidPath,
                [string] $VerifierPidPath,
                [string] $NoKillChildPidPath,
                [string] $NoKillVerifierPidPath,
                [ValidateRange(5, 30)]
                [int] $TimeoutSeconds
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                Write-Output "M01_VERIFIER_TIMEOUT_FAIL parse"
                exit 10
            }
            foreach ($functionName in @(
                    "New-M01ProcessJob",
                    "Stop-M01ProcessTree",
                    "Invoke-M01ArtifactVerifier"
                )) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    Write-Output (
                        "M01_VERIFIER_TIMEOUT_FAIL function=" +
                        $functionName +
                        " count=" +
                        $matches.Count)
                    exit 11
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }
            $result = Invoke-M01ArtifactVerifier `
                -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    $SleepyVerifierPath,
                    "-ChildPidPath",
                    $ChildPidPath,
                    "-VerifierPidPath",
                    $VerifierPidPath
                ) `
                -TimeoutSeconds $TimeoutSeconds
            if (-not $result.TimedOut -or
                $result.ExitCode -ne -1 -or
                -not $result.TerminationConfirmed -or
                -not $result.OutputCaptureSucceeded -or
                $result.OutputDrainTimedOut) {
                Write-Output (
                    "M01_VERIFIER_TIMEOUT_FAIL result timedOut=" +
                    $result.TimedOut +
                    " exitCode=" +
                    $result.ExitCode +
                    " terminated=" +
                    $result.TerminationConfirmed +
                    " output=" +
                    $result.OutputCaptureSucceeded +
                    " drainTimeout=" +
                    $result.OutputDrainTimedOut)
                exit 12
            }
            if (-not (Test-Path -LiteralPath $ChildPidPath -PathType Leaf)) {
                Write-Output "M01_VERIFIER_TIMEOUT_FAIL child_pid_missing"
                exit 13
            }
            $childPid = [int](
                Get-Content -LiteralPath $ChildPidPath -Raw).Trim()
            Start-Sleep -Milliseconds 250
            if ($null -ne (
                    Get-Process -Id $childPid -ErrorAction SilentlyContinue)) {
                Write-Output (
                    "M01_VERIFIER_TIMEOUT_FAIL child_alive pid=" +
                    $childPid)
                exit 14
            }
            $verifierPid = [int](
                Get-Content -LiteralPath $VerifierPidPath -Raw).Trim()
            if ($null -ne (
                    Get-Process -Id $verifierPid -ErrorAction SilentlyContinue)) {
                Write-Output (
                    "M01_VERIFIER_TIMEOUT_FAIL verifier_alive pid=" +
                    $verifierPid)
                exit 15
            }

            function Stop-M01ProcessTree {
                param(
                    [System.Diagnostics.Process] $Process,
                    [object] $ProcessJob
                )
            }
            $noKillStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $noKillResult = Invoke-M01ArtifactVerifier `
                    -Arguments @(
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-File",
                        $SleepyVerifierPath,
                        "-ChildPidPath",
                        $NoKillChildPidPath,
                        "-VerifierPidPath",
                        $NoKillVerifierPidPath
                    ) `
                    -TimeoutSeconds $TimeoutSeconds
                $noKillStopwatch.Stop()
                if (-not $noKillResult.TimedOut -or
                    $noKillResult.ExitCode -ne -1 -or
                    $noKillResult.TerminationConfirmed -or
                    $noKillResult.OutputCaptureSucceeded -or
                    -not $noKillResult.OutputDrainTimedOut -or
                    $noKillStopwatch.ElapsedMilliseconds -gt 15000) {
                    Write-Output (
                        "M01_VERIFIER_TIMEOUT_FAIL unkillable timedOut=" +
                        $noKillResult.TimedOut +
                        " exitCode=" +
                        $noKillResult.ExitCode +
                        " terminated=" +
                        $noKillResult.TerminationConfirmed +
                        " output=" +
                        $noKillResult.OutputCaptureSucceeded +
                        " drainTimeout=" +
                        $noKillResult.OutputDrainTimedOut +
                        " elapsedMs=" +
                        $noKillStopwatch.ElapsedMilliseconds)
                    exit 16
                }
            }
            finally {
                foreach ($pidPath in @(
                        $NoKillChildPidPath,
                        $NoKillVerifierPidPath)) {
                    if (Test-Path -LiteralPath $pidPath -PathType Leaf) {
                        $lingeringPid = [int](
                            Get-Content -LiteralPath $pidPath -Raw).Trim()
                        Stop-Process `
                            -Id $lingeringPid `
                            -Force `
                            -ErrorAction SilentlyContinue
                    }
                }
            }
            Write-Output (
                "M01_VERIFIER_TIMEOUT_PASS timeoutSeconds=" +
                $TimeoutSeconds +
                " childPid=" +
                $childPid +
                " unkillableElapsedMs=" +
                $noKillStopwatch.ElapsedMilliseconds)
            exit 0
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 45_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-SleepyVerifierPath",
            sleepyVerifierPath,
            "-ChildPidPath",
            childPidPath,
            "-VerifierPidPath",
            verifierPidPath,
            "-NoKillChildPidPath",
            noKillChildPidPath,
            "-NoKillVerifierPidPath",
            noKillVerifierPidPath,
            "-TimeoutSeconds",
            verifierTimeoutSeconds.ToString());
        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_VERIFIER_TIMEOUT_PASS timeoutSeconds=" +
                    verifierTimeoutSeconds,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_VERIFIER_TIMEOUT_FAIL",
                StringComparison.Ordinal);
    }

    private static bool TestM01PublishOutputMutex(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string caseRoot = Path.Combine(tempRoot, "m01 publish mutex with spaces");
        Directory.CreateDirectory(caseRoot);
        string probePath = Path.Combine(caseRoot, "mutex probe.ps1");
        string coordinatorPath = Path.Combine(caseRoot, "mutex coordinator.ps1");

        File.WriteAllText(
            probePath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $RepositoryRoot,
                [ValidateSet("blocked", "acquire")]
                [string] $Mode,
                [ValidateSet("Release", "Debug")]
                [string] $Configuration,
                [string] $Runtime
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                exit 20
            }
            foreach ($functionName in @(
                    "Open-M01PublishOutputLock",
                    "Close-M01PublishOutputLock")) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    exit 21
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }

            $lock = $null
            try {
                $lock = Open-M01PublishOutputLock `
                    -RepositoryRoot $RepositoryRoot `
                    -Configuration $Configuration `
                    -Runtime $Runtime
                if ($Mode -eq "blocked") {
                    Write-Output "M01_MUTEX_PROBE_FAIL unexpectedly_acquired"
                    exit 22
                }
                Write-Output "M01_MUTEX_PROBE_ACQUIRED"
                exit 0
            }
            catch {
                if ($Mode -eq "blocked" -and
                    $_.Exception.Message -eq
                        "another EFI publish owns the canonical output paths") {
                    Write-Output "M01_MUTEX_PROBE_BLOCKED"
                    exit 0
                }
                Write-Output (
                    "M01_MUTEX_PROBE_FAIL " +
                    $_.Exception.GetType().Name)
                exit 23
            }
            finally {
                Close-M01PublishOutputLock -Lock $lock
            }
            """,
            new UTF8Encoding(false));

        File.WriteAllText(
            coordinatorPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $RepositoryRoot,
                [string] $ProbePath
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                exit 30
            }
            foreach ($functionName in @(
                    "Open-M01PublishOutputLock",
                    "Close-M01PublishOutputLock")) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    exit 31
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }

            $powershellPath = Join-Path $env:SystemRoot (
                "System32\WindowsPowerShell\v1.0\powershell.exe")
            $first = Open-M01PublishOutputLock `
                -RepositoryRoot $RepositoryRoot `
                -Configuration "Release" `
                -Runtime "win-x64"
            try {
                $blockedOutput = @(& $powershellPath `
                    -NoProfile `
                    -ExecutionPolicy Bypass `
                    -File $ProbePath `
                    -BuildScriptPath $BuildScriptPath `
                    -RepositoryRoot $RepositoryRoot `
                    -Mode blocked `
                    -Configuration Debug `
                    -Runtime linux-x64 2>&1)
                if ($LASTEXITCODE -ne 0 -or
                    ($blockedOutput | Out-String) -notmatch
                        "M01_MUTEX_PROBE_BLOCKED") {
                    Write-Output "M01_MUTEX_COORDINATOR_FAIL blocked"
                    $blockedOutput
                    exit 32
                }
            }
            finally {
                Close-M01PublishOutputLock -Lock $first
            }

            $acquiredOutput = @(& $powershellPath `
                -NoProfile `
                -ExecutionPolicy Bypass `
                -File $ProbePath `
                -BuildScriptPath $BuildScriptPath `
                -RepositoryRoot $RepositoryRoot `
                -Mode acquire `
                -Configuration Debug `
                -Runtime win-x64 2>&1)
            if ($LASTEXITCODE -ne 0 -or
                ($acquiredOutput | Out-String) -notmatch
                    "M01_MUTEX_PROBE_ACQUIRED") {
                Write-Output "M01_MUTEX_COORDINATOR_FAIL reacquire"
                $acquiredOutput
                exit 33
            }
            Write-Output "M01_MUTEX_COORDINATOR_PASS"
            exit 0
            """,
            new UTF8Encoding(false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 20_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            coordinatorPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-RepositoryRoot",
            caseRoot,
            "-ProbePath",
            probePath);
        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_MUTEX_COORDINATOR_PASS",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_MUTEX_COORDINATOR_FAIL",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "M01_MUTEX_PROBE_FAIL",
                StringComparison.Ordinal);
    }

    private static bool TestM01FreshOutputCleanupGuards(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string caseRoot = Path.Combine(
            tempRoot,
            "m01 fresh output guards with spaces");
        Directory.CreateDirectory(caseRoot);
        string wrapperPath = Path.Combine(caseRoot, "fresh output guards.ps1");

        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $CaseRoot
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            $matches = @($ast.FindAll(
                    {
                        $args[0] -is
                            [System.Management.Automation.Language.FunctionDefinitionAst] -and
                        $args[0].Name -ceq
                            "Remove-M01FreshPublishDirectory"
                    },
                    $true))
            if ($parseErrors.Count -ne 0 -or $matches.Count -ne 1) {
                exit 40
            }
            . ([scriptblock]::Create($matches[0].Extent.Text))

            $normalRoot = Join-Path $CaseRoot "normal repo"
            $normalCandidate = Join-Path $normalRoot (
                "src\OS\obj\Release\net10.0\win-x64")
            New-Item `
                -ItemType Directory `
                -Path $normalCandidate `
                -Force | Out-Null
            [System.IO.File]::WriteAllText(
                (Join-Path $normalCandidate "sentinel.txt"),
                "delete me")
            Remove-M01FreshPublishDirectory `
                -RepositoryRoot $normalRoot `
                -CandidatePath $normalCandidate `
                -ExpectedRelativePath (
                    "src\OS\obj\Release\net10.0\win-x64")
            if (Test-Path -LiteralPath $normalCandidate) {
                exit 41
            }

            $outside = Join-Path $CaseRoot "outside mismatch"
            New-Item -ItemType Directory -Path $outside -Force | Out-Null
            $mismatchRejected = $false
            try {
                Remove-M01FreshPublishDirectory `
                    -RepositoryRoot $normalRoot `
                    -CandidatePath $outside `
                    -ExpectedRelativePath (
                        "src\OS\obj\Release\net10.0\win-x64")
            }
            catch {
                $mismatchRejected =
                    $_.Exception.Message -like
                        "fresh publish path mismatch:*"
            }
            if (-not $mismatchRejected -or
                -not (Test-Path -LiteralPath $outside -PathType Container)) {
                exit 42
            }

            $candidateRoot = Join-Path $CaseRoot "candidate junction repo"
            $candidateParent = Join-Path $candidateRoot (
                "src\OS\bin\Release\net10.0")
            $candidateTarget = Join-Path $CaseRoot "candidate junction target"
            New-Item -ItemType Directory -Path $candidateParent -Force |
                Out-Null
            New-Item -ItemType Directory -Path $candidateTarget -Force |
                Out-Null
            $candidateSentinel = Join-Path $candidateTarget "keep.txt"
            [System.IO.File]::WriteAllText($candidateSentinel, "keep")
            $candidateJunction = Join-Path $candidateParent "win-x64"
            New-Item `
                -ItemType Junction `
                -Path $candidateJunction `
                -Target $candidateTarget | Out-Null
            $candidateRejected = $false
            try {
                Remove-M01FreshPublishDirectory `
                    -RepositoryRoot $candidateRoot `
                    -CandidatePath $candidateJunction `
                    -ExpectedRelativePath (
                        "src\OS\bin\Release\net10.0\win-x64")
            }
            catch {
                $candidateRejected =
                    $_.Exception.Message -like
                        "fresh publish path contains reparse point:*"
            }
            if (-not $candidateRejected -or
                -not (Test-Path -LiteralPath $candidateSentinel -PathType Leaf)) {
                exit 43
            }
            [System.IO.Directory]::Delete($candidateJunction)

            $ancestorRoot = Join-Path $CaseRoot "ancestor junction repo"
            $ancestorParent = Join-Path $ancestorRoot "src\OS"
            $ancestorTarget = Join-Path $CaseRoot "ancestor junction target"
            New-Item -ItemType Directory -Path $ancestorParent -Force |
                Out-Null
            $ancestorCandidateInTarget = Join-Path $ancestorTarget (
                "Release\net10.0\win-x64")
            New-Item `
                -ItemType Directory `
                -Path $ancestorCandidateInTarget `
                -Force | Out-Null
            $ancestorSentinel = Join-Path $ancestorCandidateInTarget "keep.txt"
            [System.IO.File]::WriteAllText($ancestorSentinel, "keep")
            $ancestorJunction = Join-Path $ancestorParent "obj"
            New-Item `
                -ItemType Junction `
                -Path $ancestorJunction `
                -Target $ancestorTarget | Out-Null
            $ancestorCandidate = Join-Path $ancestorJunction (
                "Release\net10.0\win-x64")
            $ancestorRejected = $false
            try {
                Remove-M01FreshPublishDirectory `
                    -RepositoryRoot $ancestorRoot `
                    -CandidatePath $ancestorCandidate `
                    -ExpectedRelativePath (
                        "src\OS\obj\Release\net10.0\win-x64")
            }
            catch {
                $ancestorRejected =
                    $_.Exception.Message -like
                        "fresh publish path contains reparse point:*"
            }
            if (-not $ancestorRejected -or
                -not (Test-Path -LiteralPath $ancestorSentinel -PathType Leaf)) {
                exit 44
            }
            [System.IO.Directory]::Delete($ancestorJunction)
            Write-Output "M01_FRESH_OUTPUT_GUARDS_PASS"
            exit 0
            """,
            new UTF8Encoding(false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 20_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-CaseRoot",
            caseRoot);
        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_FRESH_OUTPUT_GUARDS_PASS",
                StringComparison.Ordinal);
    }

    private static bool TestM01PublishSourceLocks(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string caseRoot = Path.Combine(tempRoot, "m01 source locks with spaces");
        Directory.CreateDirectory(caseRoot);
        string wrapperPath = Path.Combine(caseRoot, "source locks.ps1");

        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $CaseRoot
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                exit 50
            }
            foreach ($functionName in @(
                    "Initialize-M01NativeFileIdentity",
                    "Get-M01StreamSha256",
                    "Assert-M01NoReparsePoints",
                    "Open-M01AuthorityReadRecord",
                    "Assert-M01AuthorityRecordUnchanged",
                    "Assert-M01DistinctAuthorityRecords",
                    "Close-M01AuthorityRecords",
                    "Open-M01PublishSourceLocks",
                    "Assert-M01PublishSourceLocks",
                    "Close-M01PublishSourceLocks")) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    exit 51
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }

            function Get-TestFileSha256 {
                param([string] $Path)

                $stream = [System.IO.File]::Open(
                    $Path,
                    [System.IO.FileMode]::Open,
                    [System.IO.FileAccess]::Read,
                    [System.IO.FileShare]::Read)
                try {
                    return Get-M01StreamSha256 -Stream $stream
                }
                finally {
                    $stream.Dispose()
                }
            }

            $sourcePath = Join-Path $CaseRoot "source.txt"
            [System.IO.File]::WriteAllText($sourcePath, "before")
            $sourceHash = Get-TestFileSha256 -Path $sourcePath
            $inputs = @([pscustomobject]@{
                    path = "source.txt"
                    sha256 = $sourceHash
                })
            $locks = Open-M01PublishSourceLocks `
                -RepositoryRoot $CaseRoot `
                -Inputs $inputs
            try {
                Assert-M01PublishSourceLocks `
                    -RepositoryRoot $CaseRoot `
                    -Inputs $inputs `
                    -Locks $locks
                $writeRejected = $false
                try {
                    [System.IO.File]::WriteAllText($sourcePath, "during")
                }
                catch [System.IO.IOException] {
                    $writeRejected = $true
                }
                $moveRejected = $false
                try {
                    [System.IO.File]::Move(
                        $sourcePath,
                        (Join-Path $CaseRoot "moved.txt"))
                }
                catch [System.IO.IOException] {
                    $moveRejected = $true
                }
                if (-not $writeRejected -or -not $moveRejected -or
                    [System.IO.File]::ReadAllText($sourcePath) -ne "before") {
                    exit 52
                }
            }
            finally {
                Close-M01PublishSourceLocks -Locks $locks
            }

            [System.IO.File]::WriteAllText($sourcePath, "after")
            $movedPath = Join-Path $CaseRoot "moved.txt"
            [System.IO.File]::Move($sourcePath, $movedPath)
            if ([System.IO.File]::ReadAllText($movedPath) -ne "after") {
                exit 53
            }

            $hardlinkSource = Join-Path $CaseRoot "hardlink-source.txt"
            $hardlinkAlias = Join-Path $CaseRoot "hardlink-alias.txt"
            [System.IO.File]::WriteAllText($hardlinkSource, "hardlink")
            New-Item `
                -ItemType HardLink `
                -Path $hardlinkAlias `
                -Target $hardlinkSource | Out-Null
            $hardlinkHash = Get-TestFileSha256 -Path $hardlinkAlias
            $hardlinkRejected = $false
            try {
                $unused = Open-M01PublishSourceLocks `
                    -RepositoryRoot $CaseRoot `
                    -Inputs @([pscustomobject]@{
                            path = "hardlink-alias.txt"
                            sha256 = $hardlinkHash
                        })
            }
            catch {
                $hardlinkRejected =
                    $_.Exception.Message -like "*hardlink_invalid*"
            }
            if (-not $hardlinkRejected) {
                exit 54
            }

            $junctionTarget = Join-Path $CaseRoot "junction-target"
            $junctionRoot = Join-Path $CaseRoot "junction-root"
            New-Item -ItemType Directory -Path $junctionTarget | Out-Null
            $junctionSource = Join-Path $junctionTarget "source.txt"
            [System.IO.File]::WriteAllText($junctionSource, "junction")
            New-Item `
                -ItemType Junction `
                -Path $junctionRoot `
                -Target $junctionTarget | Out-Null
            $junctionHash = Get-TestFileSha256 -Path $junctionSource
            $junctionRejected = $false
            try {
                $unused = Open-M01PublishSourceLocks `
                    -RepositoryRoot $junctionRoot `
                    -Inputs @([pscustomobject]@{
                            path = "source.txt"
                            sha256 = $junctionHash
                        })
            }
            catch {
                $junctionRejected =
                    $_.Exception.Message -like "*reparse*"
            }
            if (-not $junctionRejected) {
                exit 55
            }
            [System.IO.Directory]::Delete($junctionRoot)

            $leafTarget = Join-Path $CaseRoot "leaf-target.txt"
            $leafLink = Join-Path $CaseRoot "leaf-link.txt"
            [System.IO.File]::WriteAllText($leafTarget, "leaf")
            $leafLinkCreated = $false
            try {
                New-Item `
                    -ItemType SymbolicLink `
                    -Path $leafLink `
                    -Target $leafTarget `
                    -ErrorAction Stop | Out-Null
                $leafLinkCreated = $true
            }
            catch {
                # Symlink creation can be policy restricted on Windows.
            }
            if ($leafLinkCreated) {
                $leafHash = Get-TestFileSha256 -Path $leafLink
                $leafRejected = $false
                try {
                    $unused = Open-M01PublishSourceLocks `
                        -RepositoryRoot $CaseRoot `
                        -Inputs @([pscustomobject]@{
                                path = "leaf-link.txt"
                                sha256 = $leafHash
                            })
                }
                catch {
                    $leafRejected =
                        $_.Exception.Message -like "*reparse*"
                }
                if (-not $leafRejected) {
                    exit 56
                }
                [System.IO.File]::Delete($leafLink)
            }
            Write-Output "M01_SOURCE_LOCKS_PASS"
            exit 0
            """,
            new UTF8Encoding(false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 15_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-CaseRoot",
            caseRoot);
        return result.ExitCode == 0
            && result.Output.Contains(
                "M01_SOURCE_LOCKS_PASS",
                StringComparison.Ordinal);
    }

    private static bool TestM01OutputAndVerifierAuthority(string tempRoot)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string buildScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string script = File.ReadAllText(buildScriptPath);
        string[] requiredText =
        [
            "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=138",
            "Write-Host (\"EFI_PUBLISH_OUTPUT_AUTHORITY status=ok leaves=\" + $outputPublicationPlan.Paths.Count)",
            "Write-Host (\"EFI_PUBLISH_VERIFIER_INPUT_AUTHORITY status=ok inputs=\" + $verifierInputLocks.Count)",
            "Write-Host (\"EFI_PUBLISH_VERIFIER_INPUTS_UNCHANGED status=ok inputs=\" + $verifierInputLocks.Count)",
            "$expectedVerifierInputCount = if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) { 10 } else { 8 }",
            "if ($verifierInputLocks.Count -ne $expectedVerifierInputCount)",
            "if ($EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe) { $paths += $efiFullPath + \".references.obj\" }",
            "$verifierInputs += [pscustomobject]@{Path=$referenceObjectPath; RootPath=$repositoryRoot; Purpose=\"linked-reference-object\"}",
            "$verifierInputs += [pscustomobject]@{Path=$referenceObjectOutputPath; RootPath=$outputPublicationPlan.Directory; Purpose=\"preserved-reference-object\"}",
            "FileMode]::CreateNew",
            "if ($existingRecords.Count -gt 0)",
            "Assert-M01VerifierInputLocks -Locks $verifierInputLocks",
            "Close-M01AuthorityRecords -Records $verifierInputLocks"
        ];
        if (requiredText.Any(
                value => !script.Contains(value, StringComparison.Ordinal)) ||
            script.Contains("Copy-Item", StringComparison.Ordinal) ||
            script.Contains("Set-Content", StringComparison.Ordinal) ||
            script.Contains("Add-Content", StringComparison.Ordinal))
        {
            return false;
        }

        string caseRoot = Path.Combine(
            tempRoot,
            "m01 output authority with spaces");
        Directory.CreateDirectory(caseRoot);
        string wrapperPath = Path.Combine(caseRoot, "output authority.ps1");
        File.WriteAllText(
            wrapperPath,
            """
            param(
                [string] $BuildScriptPath,
                [string] $CaseRoot
            )
            $ErrorActionPreference = "Stop"
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $BuildScriptPath,
                [ref] $tokens,
                [ref] $parseErrors)
            if ($parseErrors.Count -ne 0) {
                exit 60
            }
            foreach ($functionName in @(
                    "Initialize-M01NativeFileIdentity",
                    "Get-M01StreamSha256",
                    "Assert-M01NoReparsePoints",
                    "Open-M01AuthorityReadRecord",
                    "Assert-M01AuthorityRecordUnchanged",
                    "Assert-M01DistinctAuthorityRecords",
                    "Close-M01AuthorityRecords",
                    "Initialize-M01PlainDirectoryPath",
                    "Assert-M01WritableAuthorityIdentity",
                    "Open-M01LogAuthority",
                    "Write-M01LogLines",
                    "Read-M01LogAuthorityText",
                    "Get-M01OutputPublicationPlan",
                    "Clear-M01OutputPublicationLeaves",
                    "Copy-M01FileCreateNew",
                    "Write-M01Utf8CreateNew",
                    "Open-M01VerifierInputLocks",
                    "Assert-M01VerifierInputLocks")) {
                $matches = @($ast.FindAll(
                        {
                            $args[0] -is
                                [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $args[0].Name -ceq $functionName
                        },
                        $true))
                if ($matches.Count -ne 1) {
                    exit 61
                }
                . ([scriptblock]::Create($matches[0].Extent.Text))
            }

            $caseBase = $CaseRoot
            foreach ($mode in @("default", "checked", "gc")) {
            $EnableM04CheckedReferenceBarrierProbe = $mode -eq "checked"
            $EnableM04GcStaticRootMaterializationProbe = $mode -eq "gc"
            $enabled = $EnableM04CheckedReferenceBarrierProbe -or $EnableM04GcStaticRootMaterializationProbe
            $CaseRoot = Join-Path $caseBase $mode
            New-Item -ItemType Directory -Path $CaseRoot | Out-Null
            $repo = Join-Path $CaseRoot "repo"
            New-Item -ItemType Directory -Path $repo | Out-Null
            $logPath = Join-Path $CaseRoot "logs\publish.log"
            $plan = Get-M01OutputPublicationPlan `
                -RepositoryRoot $repo `
                -EfiOutputPath "artifacts\efi\BOOTX64.EFI" `
                -LogFullPath $logPath
            $expectedPaths = @($plan.EfiPath, ($plan.EfiPath + ".link.map"),
                ($plan.EfiPath + ".link.rsp"), ($plan.EfiPath + ".link.json"), ($plan.EfiPath + ".serializer.obj"))
            if ($enabled) { $expectedPaths += $plan.EfiPath + ".references.obj" }
            $expectedLeaves = if ($enabled) { 6 } else { 5 }
            if ($plan.Paths.Count -ne $expectedLeaves -or
                ($plan.Paths -join '|') -cne ($expectedPaths -join '|')) { exit 72 }
            Clear-M01OutputPublicationLeaves -Plan $plan
            foreach ($path in $plan.Paths) {
                if (Test-Path -LiteralPath $path) {
                    exit 71
                }
            }
            Write-Output "M01_EMPTY_OUTPUT_AUTHORITY_PASS"
            $logAuthority = Open-M01LogAuthority -Path $logPath
            try {
                Write-M01LogLines `
                    -Authority $logAuthority `
                    -Lines @("first", "second") `
                    -Replace
                $logText = Read-M01LogAuthorityText -Authority $logAuthority
                if ($logText -notlike "first*second*") {
                    exit 62
                }

                [System.IO.File]::WriteAllText($plan.EfiPath, "stale")
                Clear-M01OutputPublicationLeaves -Plan $plan
                if (Test-Path -LiteralPath $plan.EfiPath) {
                    exit 63
                }

                $work = Join-Path $repo "work"
                New-Item -ItemType Directory -Path $work | Out-Null
                $publishSource = Join-Path $work "published.exe"
                $mapSource = Join-Path $work "linked.map"
                $responseSource = Join-Path $work "linked.rsp"
                $serializerSource = Join-Path $work "linked.obj"
                $managedSource = Join-Path $work "managed.dll"
                [System.IO.File]::WriteAllText($publishSource, "efi")
                [System.IO.File]::WriteAllText($mapSource, "map")
                [System.IO.File]::WriteAllText($responseSource, "response")
                [System.IO.File]::WriteAllText($serializerSource, "serializer")
                [System.IO.File]::WriteAllText($managedSource, "managed")
                $referenceSource = Join-Path $work "reference.obj"
                $referencePreserved = $plan.EfiPath + ".references.obj"
                if ($enabled) {
                    [System.IO.File]::WriteAllText($referenceSource, "reference-object")
                    Copy-M01FileCreateNew -SourcePath $referenceSource -SourceRootPath $repo `
                        -DestinationPath $referencePreserved -DestinationRootPath $plan.Directory -Purpose "reference-object"
                }
                Copy-M01FileCreateNew `
                    -SourcePath $publishSource `
                    -SourceRootPath $repo `
                    -DestinationPath $plan.EfiPath `
                    -DestinationRootPath $plan.Directory `
                    -Purpose "efi"
                Copy-M01FileCreateNew `
                    -SourcePath $mapSource `
                    -SourceRootPath $repo `
                    -DestinationPath $plan.MapPath `
                    -DestinationRootPath $plan.Directory `
                    -Purpose "map"
                Copy-M01FileCreateNew `
                    -SourcePath $responseSource `
                    -SourceRootPath $repo `
                    -DestinationPath $plan.ResponsePath `
                    -DestinationRootPath $plan.Directory `
                    -Purpose "response"
                Copy-M01FileCreateNew `
                    -SourcePath $serializerSource `
                    -SourceRootPath $repo `
                    -DestinationPath $plan.SerializerObjectPath `
                    -DestinationRootPath $plan.Directory `
                    -Purpose "serializer"
                Write-M01Utf8CreateNew `
                    -Path $plan.EvidencePath `
                    -RootPath $plan.Directory `
                    -Text '{"schemaVersion":3}'

                $overwriteRejected = $false
                try {
                    Copy-M01FileCreateNew `
                        -SourcePath $publishSource `
                        -SourceRootPath $repo `
                        -DestinationPath $plan.EfiPath `
                        -DestinationRootPath $plan.Directory `
                        -Purpose "duplicate"
                }
                catch [System.IO.IOException] {
                    $overwriteRejected = $true
                }
                if (-not $overwriteRejected) {
                    exit 64
                }

                $inputs = @(
                    [pscustomobject]@{
                        Path = $plan.EfiPath
                        RootPath = $plan.Directory
                        Purpose = "final-efi"
                    },
                    [pscustomobject]@{
                        Path = $plan.MapPath
                        RootPath = $plan.Directory
                        Purpose = "final-map"
                    },
                    [pscustomobject]@{
                        Path = $plan.ResponsePath
                        RootPath = $plan.Directory
                        Purpose = "final-response"
                    },
                    [pscustomobject]@{
                        Path = $plan.EvidencePath
                        RootPath = $plan.Directory
                        Purpose = "evidence"
                    },
                    [pscustomobject]@{
                        Path = $plan.SerializerObjectPath
                        RootPath = $plan.Directory
                        Purpose = "preserved-object"
                    },
                    [pscustomobject]@{
                        Path = $serializerSource
                        RootPath = $repo
                        Purpose = "linked-object"
                    },
                    [pscustomobject]@{
                        Path = $mapSource
                        RootPath = $repo
                        Purpose = "linked-map"
                    },
                    [pscustomobject]@{
                        Path = $managedSource
                        RootPath = $repo
                        Purpose = "managed-dll"
                    })
                if ($enabled) {
                    $inputs += [pscustomobject]@{Path=$referenceSource; RootPath=$repo; Purpose="linked-reference-object"}
                    $inputs += [pscustomobject]@{Path=$referencePreserved; RootPath=$plan.Directory; Purpose="preserved-reference-object"}
                    $aliasRejected = $false
                    try { $aliasLocks = Open-M01VerifierInputLocks -Inputs (@($inputs[0..8]) + @($inputs[8])); Close-M01AuthorityRecords -Records $aliasLocks }
                    catch { $aliasRejected = $_.Exception.Message -like '*authority_file_alias*' }
                    if (-not $aliasRejected) { exit 73 }
                }
                $locks = Open-M01VerifierInputLocks -Inputs $inputs
                try {
                    $expectedLocks = if ($enabled) { 10 } else { 8 }
                    if ($locks.Count -ne $expectedLocks) {
                        exit 65
                    }
                    Assert-M01DistinctAuthorityRecords -Records (
                        @($locks.ToArray()) + @($logAuthority))
                    $writeRejected = $false
                    try {
                        [System.IO.File]::WriteAllText($plan.EfiPath, "changed")
                    }
                    catch [System.IO.IOException] {
                        $writeRejected = $true
                    }
                    $replaceRejected = $false
                    try {
                        [System.IO.File]::Move(
                            $plan.EfiPath,
                            (Join-Path $plan.Directory "replaced.efi"))
                    }
                    catch [System.IO.IOException] {
                        $replaceRejected = $true
                    }
                    if (-not $writeRejected -or -not $replaceRejected) {
                        exit 66
                    }
                    if ($enabled) {
                        foreach ($referencePath in @($referenceSource, $referencePreserved)) {
                            $referenceWriteRejected = $false
                            try { [System.IO.File]::WriteAllText($referencePath, "changed") }
                            catch [System.IO.IOException] { $referenceWriteRejected = $true }
                            if (-not $referenceWriteRejected) { exit 74 }
                        }
                    }
                    Assert-M01VerifierInputLocks -Locks $locks
                    Write-Output ("M01_OUTPUT_AUTHORITY_VECTOR_PASS mode=" + $mode + " leaves=" + $plan.Paths.Count + " inputs=" + $locks.Count)
                }
                finally {
                    Close-M01AuthorityRecords -Records $locks
                }

                Clear-M01OutputPublicationLeaves -Plan $plan
                $hardlinkTarget = Join-Path $work "hardlink-target.bin"
                [System.IO.File]::WriteAllText($hardlinkTarget, "target")
                New-Item `
                    -ItemType HardLink `
                    -Path $plan.EfiPath `
                    -Target $hardlinkTarget | Out-Null
                $hardlinkRejected = $false
                try {
                    Clear-M01OutputPublicationLeaves -Plan $plan
                }
                catch {
                    $hardlinkRejected =
                        $_.Exception.Message -like "*hardlink_invalid*"
                }
                if (-not $hardlinkRejected) {
                    exit 67
                }
                [System.IO.File]::Delete($plan.EfiPath)
                [System.IO.File]::Delete($hardlinkTarget)

                $logHardlinkTarget = Join-Path $CaseRoot "log-target.txt"
                $logHardlinkPath = Join-Path $CaseRoot "log-hardlink.txt"
                [System.IO.File]::WriteAllText($logHardlinkTarget, "log")
                New-Item `
                    -ItemType HardLink `
                    -Path $logHardlinkPath `
                    -Target $logHardlinkTarget | Out-Null
                $logHardlinkRejected = $false
                try {
                    $unusedLog = Open-M01LogAuthority -Path $logHardlinkPath
                }
                catch {
                    $logHardlinkRejected =
                        $_.Exception.Message -like "*hardlink_invalid*"
                }
                if (-not $logHardlinkRejected) {
                    exit 68
                }

                $targetRepo = Join-Path $CaseRoot "junction-target-repo"
                $junctionRepo = Join-Path $CaseRoot "junction-repo"
                New-Item -ItemType Directory -Path $targetRepo | Out-Null
                New-Item `
                    -ItemType Junction `
                    -Path $junctionRepo `
                    -Target $targetRepo | Out-Null
                $ancestorRejected = $false
                try {
                    $unusedPlan = Get-M01OutputPublicationPlan `
                        -RepositoryRoot $junctionRepo `
                        -EfiOutputPath "artifacts\efi\BOOTX64.EFI" `
                        -LogFullPath (Join-Path $CaseRoot "other.log")
                }
                catch {
                    $ancestorRejected =
                        $_.Exception.Message -like "*not_plain_directory*" -or
                        $_.Exception.Message -like "*reparse*"
                }
                if (-not $ancestorRejected) {
                    exit 69
                }
                [System.IO.Directory]::Delete($junctionRepo)

                $leafTarget = Join-Path $work "leaf-target.bin"
                $leafLink = $plan.EfiPath
                [System.IO.File]::WriteAllText($leafTarget, "leaf")
                $leafCreated = $false
                try {
                    New-Item `
                        -ItemType SymbolicLink `
                        -Path $leafLink `
                        -Target $leafTarget `
                        -ErrorAction Stop | Out-Null
                    $leafCreated = $true
                }
                catch {
                    # Symlink creation can be policy restricted on Windows.
                }
                if ($leafCreated) {
                    $leafRejected = $false
                    try {
                        Clear-M01OutputPublicationLeaves -Plan $plan
                    }
                    catch {
                        $leafRejected =
                            $_.Exception.Message -like "*reparse*"
                    }
                    if (-not $leafRejected) {
                        exit 70
                    }
                    [System.IO.File]::Delete($leafLink)
                }
            }
            finally {
                if ($null -ne $logAuthority) {
                    $logAuthority.Stream.Dispose()
                }
            }
            }
            Write-Output "M01_OUTPUT_AUTHORITY_PASS"
            exit 0
            """,
            new UTF8Encoding(false));

        ProcessResult result = RunProcess(
            "powershell.exe",
            repositoryRoot,
            timeoutMilliseconds: 30_000,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            wrapperPath,
            "-BuildScriptPath",
            buildScriptPath,
            "-CaseRoot",
            caseRoot);
        return result.ExitCode == 0
            && result.Output.Contains("M01_OUTPUT_AUTHORITY_VECTOR_PASS mode=default leaves=5 inputs=8", StringComparison.Ordinal)
            && result.Output.Contains("M01_OUTPUT_AUTHORITY_VECTOR_PASS mode=checked leaves=6 inputs=10", StringComparison.Ordinal)
            && result.Output.Contains("M01_OUTPUT_AUTHORITY_VECTOR_PASS mode=gc leaves=6 inputs=10", StringComparison.Ordinal)
            && result.Output.Contains(
                "M01_EMPTY_OUTPUT_AUTHORITY_PASS",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "M01_OUTPUT_AUTHORITY_PASS",
                StringComparison.Ordinal);
    }

    private static bool IsM01SourceClosureSuccess(
        ProcessResult result,
        string expectedSwitches)
    {
        string log = File.Exists(result.LogPath)
            ? File.ReadAllText(result.LogPath)
            : string.Empty;
        string switchMarker =
            "EFI_PREFLIGHT_M01_SWITCHES status=ok " + expectedSwitches;
        const string closureMarker =
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=122 prerequisiteGap=UefiMemoryType";
        bool passed = result.ExitCode == 0
            && result.Output.Contains(switchMarker, StringComparison.Ordinal)
            && result.Output.Contains(closureMarker, StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && log.Contains(switchMarker, StringComparison.Ordinal)
            && log.Contains(closureMarker, StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && !log.Contains(SecretCanary, StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool IsM01SwitchFailure(
        ProcessResult result,
        string expectedReason)
    {
        string log = File.Exists(result.LogPath)
            ? File.ReadAllText(result.LogPath)
            : string.Empty;
        return result.ExitCode == 1
            && result.Output.Contains(
                "EFI_PREFLIGHT_M01_SWITCHES",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "reason=" + expectedReason,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish m01_" + expectedReason,
                StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal)
            && log.Contains("reason=" + expectedReason, StringComparison.Ordinal);
    }

    private static bool IsM02SwitchFailure(
        ProcessResult result,
        string expectedReason)
    {
        string log = File.Exists(result.LogPath)
            ? File.ReadAllText(result.LogPath)
            : string.Empty;
        return result.ExitCode == 1
            && result.Output.Contains(
                "EFI_PREFLIGHT_M02_SWITCHES",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "reason=" + expectedReason,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish m02_" + expectedReason,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_PREFLIGHT_M01_SOURCE_CLOSURE",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_PREFLIGHT_MSVC_LINKER",
                StringComparison.Ordinal)
            && log.Contains(
                "reason=" + expectedReason,
                StringComparison.Ordinal);
    }

    private static bool IsM02ProfileSwitchFailure(
        ProcessResult result,
        string expectedReason)
    {
        string log = File.Exists(result.LogPath)
            ? File.ReadAllText(result.LogPath)
            : string.Empty;
        return result.ExitCode == 1
            && result.Output.Contains(
                "EFI_PREFLIGHT_M02_SWITCHES",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "reason=" + expectedReason,
                StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish " + expectedReason,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_PREFLIGHT_M01_SOURCE_CLOSURE",
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "EFI_PREFLIGHT_MSVC_LINKER",
                StringComparison.Ordinal)
            && log.Contains(
                "reason=" + expectedReason,
                StringComparison.Ordinal);
    }

    private static bool ExpectM01SourceClosureFailure(
        ToolFixture tools,
        SourceClosureFixture source,
        string? expectedReason,
        string? expectedDetail = null)
    {
        ProcessResult result = RunPreflight(
            tools,
            useSearchRoots: false,
            scriptPath: source.ScriptPath);
        string log = File.Exists(result.LogPath)
            ? File.ReadAllText(result.LogPath)
            : string.Empty;
        bool passed = result.ExitCode == 1
            && result.Output.Contains(
                "EFI_PREFLIGHT_M01_SOURCE_CLOSURE",
                StringComparison.Ordinal)
            && result.Output.Contains(" invalid", StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish m01_source_closure_",
                StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal)
            && !result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && log.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal);
        if (expectedReason is not null)
        {
            passed = passed
                && result.Output.Contains(
                    "reason=" + expectedReason,
                    StringComparison.Ordinal);
        }

        if (expectedDetail is not null)
        {
            string detail = "detail=\"" + expectedDetail + "\"";
            passed = passed
                && result.Output.Contains(detail, StringComparison.Ordinal)
                && log.Contains(detail, StringComparison.Ordinal);
        }

        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static SourceClosureFixture CreateSourceClosureFixture(
        string tempRoot,
        string name)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string sourceProjectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        string sourceScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "build-efi-shell.ps1");
        string root = Path.Combine(tempRoot, name + "-repo");
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        string scriptPath = Path.Combine(root, "scripts", "build-efi-shell.ps1");
        string serializerTargetsPath = Path.Combine(
            root,
            "tools",
            "OS.X64TextSerializerGenerator",
            "OS.X64TextSerializerGenerator.targets");

        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.Copy(sourceProjectPath, projectPath, overwrite: true);
        File.Copy(sourceScriptPath, scriptPath, overwrite: true);
        Directory.CreateDirectory(Path.GetDirectoryName(serializerTargetsPath)!);
        foreach (string fileName in new[]
                 {
                     "OS.X64TextSerializerGenerator.csproj",
                     "OS.X64TextSerializerGenerator.targets",
                     "Program.cs",
                     "Amd64CoffWriter.cs",
                     "InstructionPublicationSerializerGenerator.cs",
                     "CheckedReferenceBarrierThunkGenerator.cs",
                     "InterfaceDispatchThunkGenerator.cs",
                     "ExceptionHandlingThunkGenerator.cs"
                 })
        {
            File.Copy(
                Path.Combine(
                    repositoryRoot,
                    "tools",
                    "OS.X64TextSerializerGenerator",
                    fileName),
                Path.Combine(Path.GetDirectoryName(serializerTargetsPath)!, fileName),
                overwrite: true);
        }
        File.Copy(
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(root, "Directory.Build.props"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "Directory.Build.targets"),
            Path.Combine(root, "Directory.Build.targets"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(root, "global.json"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "CLROS.slnx"),
            Path.Combine(root, "CLROS.slnx"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "scripts", "test-efi-artifact.ps1"),
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "tools", "Directory.Build.props"),
            Path.Combine(root, "tools", "Directory.Build.props"),
            overwrite: true);

        string project = File.ReadAllText(sourceProjectPath);
        HashSet<string> copied = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(
                     project,
                     "<Compile\\s+Include=\"([^\"]+)\"",
                     RegexOptions.CultureInvariant))
        {
            string include = match.Groups[1].Value;
            string sourcePath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(sourceProjectPath)!, include));
            string destinationPath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(projectPath)!, include));
            if (!copied.Add(destinationPath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        return new SourceClosureFixture
        {
            Root = root,
            ProjectPath = projectPath,
            ScriptPath = scriptPath,
            SerializerTargetsPath = serializerTargetsPath
        };
    }

    private static string SourcePath(
        SourceClosureFixture source,
        string area,
        string file)
    {
        return Path.Combine(source.Root, "src", "OS.Kernel", area, file);
    }

    private static void AppendSource(
        SourceClosureFixture source,
        string area,
        string file,
        string text)
    {
        File.AppendAllText(SourcePath(source, area, file), text, new UTF8Encoding(false));
    }

    private static string ExtractPowerShellBlock(
        string text,
        string startToken,
        string endToken)
    {
        string normalized = text.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string normalizedStart = startToken.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string normalizedEnd = endToken.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        int startIndex = normalized.IndexOf(
            normalizedStart,
            StringComparison.Ordinal);
        int endIndex = startIndex < 0
            ? -1
            : normalized.IndexOf(
                normalizedEnd,
                startIndex + normalizedStart.Length,
                StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            throw new InvalidOperationException(
                "PowerShell contract block was not found: " + startToken);
        }

        return normalized.Substring(startIndex, endIndex - startIndex);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>>
        ParsePowerShellLiteralRecords(string block)
    {
        List<IReadOnlyDictionary<string, string>> records = [];
        foreach (Match recordMatch in Regex.Matches(
                     block,
                     @"(?s)\[ordered\]\s+@\{(?<body>.*?)\}",
                     RegexOptions.CultureInvariant))
        {
            Dictionary<string, string> record =
                new(StringComparer.Ordinal);
            foreach (Match propertyMatch in Regex.Matches(
                         recordMatch.Groups["body"].Value,
                         @"(?m)^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*" +
                         @"(?:'(?<quoted>[^']*)'|(?<bare>[^\r\n#]+?))\s*$",
                         RegexOptions.CultureInvariant))
            {
                string name = propertyMatch.Groups["name"].Value;
                string value = propertyMatch.Groups["quoted"].Success
                    ? propertyMatch.Groups["quoted"].Value
                    : propertyMatch.Groups["bare"].Value.Trim();
                if (!record.TryAdd(name, value))
                {
                    throw new InvalidOperationException(
                        "Duplicate PowerShell record property: " + name);
                }
            }

            records.Add(record);
        }

        return records;
    }

    private static string ReadRecordValue(
        IReadOnlyDictionary<string, string> record,
        string name)
    {
        if (!record.TryGetValue(name, out string? value))
        {
            throw new InvalidOperationException(
                "PowerShell record property was not found: " + name);
        }

        return value;
    }

    private static bool IsLowerHex(string value, int expectedLength)
    {
        return value.Length == expectedLength &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static int CountOrdinal(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void ReplaceOnce(
        string path,
        string oldText,
        string newText)
    {
        string text = File.ReadAllText(path);
        int index = text.IndexOf(oldText, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Mutation token was not found in " + path + ": " + oldText);
        }

        string replacement = string.Concat(
            text.AsSpan(0, index),
            newText,
            text.AsSpan(index + oldText.Length));
        File.WriteAllText(path, replacement, new UTF8Encoding(false));
    }

    private static void ReplaceAllRequired(
        string path,
        string oldText,
        string newText)
    {
        string text = File.ReadAllText(path);
        if (!text.Contains(oldText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Mutation token was not found in " + path + ": " + oldText);
        }

        File.WriteAllText(
            path,
            text.Replace(oldText, newText, StringComparison.Ordinal),
            new UTF8Encoding(false));
    }

    private static void RemoveFirstNostdlibExclusion(string path)
    {
        string text = File.ReadAllText(path);
        Regex exclusion = new(
            "^\\s*#if !NOSTDLIB_EFI\\s*$.*?^\\s*#endif\\s*$",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        string replacement = exclusion.Replace(text, string.Empty, count: 1);
        if (string.Equals(text, replacement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "No exact NOSTDLIB_EFI exclusion was found in " + path);
        }

        File.WriteAllText(path, replacement, new UTF8Encoding(false));
    }

    private static void InsertBeforeFirstItemGroupEnd(string projectPath, string xml)
    {
        ReplaceOnce(projectPath, "</ItemGroup>", xml + "\t</ItemGroup>");
    }

    private static void InsertBeforeProjectEnd(string projectPath, string xml)
    {
        ReplaceOnce(projectPath, "</Project>", xml + "</Project>");
    }

    private static void RemoveSwitchPropertyGroup(string projectPath, string switchName)
    {
        string text = File.ReadAllText(projectPath);
        string escapedSwitch = Regex.Escape(switchName);
        Regex group = new(
            "\\s*<PropertyGroup Condition=\"'\\$\\(" + escapedSwitch
            + "\\)' == 'true'\">.*?</PropertyGroup>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        string replacement = group.Replace(text, string.Empty, count: 1);
        if (string.Equals(text, replacement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Switch property group was not found: " + switchName);
        }

        File.WriteAllText(projectPath, replacement, new UTF8Encoding(false));
    }

    private static bool TestMissingLinkerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-linker");
        tools.MsvcLinkerSearchRoot = CreateEmptyDirectory(tools.Root, "empty-msvc");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_MSVC_LINKER missing",
            "platform_linker_missing",
            unexpectedMarker: "EFI_PREFLIGHT_WINDOWS_SDK");
    }

    private static bool TestMissingWindowsSdkFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-sdk");
        tools.WindowsSdkLibSearchRoot = CreateEmptyDirectory(tools.Root, "empty-sdk");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_WINDOWS_SDK missing",
            "windows_sdk_um_x64_missing",
            unexpectedMarker: "EFI_PREFLIGHT_ILCOMPILER")
            && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingIlCompilerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ilcompiler");
        tools.IlCompilerPath = Path.Combine(tools.Root, "missing-ilc.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_ILCOMPILER missing",
            "ilcompiler_missing",
            unexpectedMarker: "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK")
            && result.Output.Contains("EFI_PREFLIGHT_WINDOWS_SDK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingRuntimePackFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-runtime-pack");
        tools.NativeAotRuntimePackPath = Path.Combine(tools.Root, "missing-runtime-pack");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK missing",
            "nativeaot_runtime_pack_missing",
            unexpectedMarker: "EFI_PREFLIGHT_QEMU")
            && result.Output.Contains("EFI_PREFLIGHT_ILCOMPILER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu");
        tools.QemuPath = Path.Combine(tools.Root, "missing-qemu.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_QEMU missing",
            "qemu_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_QEMU_IMG")
            && result.Output.Contains("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuImgFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu-img");
        tools.QemuImgPath = Path.Combine(tools.Root, "missing-qemu-img.exe");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_QEMU_IMG missing",
            "qemu_img_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OVMF")
            && result.Output.Contains("EFI_PREFLIGHT_QEMU path=", StringComparison.Ordinal);
    }

    private static bool TestMissingOvmfFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ovmf");
        tools.OvmfCodePath = Path.Combine(tools.Root, "missing-OVMF_CODE.fd");

        ProcessResult result = RunPreflight(tools);

        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF missing",
            "ovmf_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OK")
            && result.Output.Contains("EFI_PREFLIGHT_QEMU_IMG path=", StringComparison.Ordinal);
    }

    private static bool TestInjectedPathsPass(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "injected-paths");
        ProcessResult result = RunPreflight(tools);

        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestInjectedSearchRootsPass(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "injected-roots");
        ProcessResult result = RunPreflight(tools, useSearchRoots: true);

        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestNewestCredibleToolsetsWin(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "numeric-toolsets");

        string olderLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "14.99.99999",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        WriteMinimalPeX64(olderLinker);
        string newestLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "14.100.10000",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        WriteMinimalPeX64(newestLinker);
        string invalidNewestLinker = Path.Combine(
            tools.MsvcLinkerSearchRoot,
            "99.0.0",
            "bin",
            "Hostx64",
            "x64",
            "link.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidNewestLinker)!);
        File.WriteAllText(invalidNewestLinker, string.Empty);

        string olderSdk = Path.Combine(
            tools.WindowsSdkLibSearchRoot,
            "10.0.9999.0",
            "um",
            "x64",
            "kernel32.lib");
        WriteCoffArchive(olderSdk);
        string newestSdk = Path.Combine(
            tools.WindowsSdkLibSearchRoot,
            "10.0.100000.0",
            "um",
            "x64",
            "kernel32.lib");
        WriteCoffArchive(newestSdk);

        ProcessResult result = RunPreflight(tools);
        bool passed = result.ExitCode == 0
            && result.Output.Contains(
                "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(newestLinker) + "\"",
                StringComparison.Ordinal)
            && result.Output.Contains(
                "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(newestSdk) + "\"",
                StringComparison.Ordinal)
            && !result.Output.Contains(Path.GetFullPath(invalidNewestLinker), StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestUndersizedOvmfFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "undersized-ovmf");
        File.WriteAllBytes(tools.OvmfCodePath, new byte[] { 0x5f, 0x46, 0x56, 0x48 });

        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF invalid",
            "ovmf_invalid",
            unexpectedMarker: "EFI_PREFLIGHT_OK");
    }

    private static bool TestOvmfEvidenceIgnoresPoisonedFileHashModule(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-no-modules");
        tools.PowerShellModulePath = Path.Combine(tools.Root, "poisoned-modules");
        string poisonModule = Path.Combine(
            tools.PowerShellModulePath,
            "PoisonedHash");
        Directory.CreateDirectory(poisonModule);
        File.WriteAllText(
            Path.Combine(poisonModule, "PoisonedHash.psm1"),
            "function Get-FileHash { throw 'poisoned Get-FileHash executed' }\n" +
            "Export-ModuleMember -Function Get-FileHash\n");
        File.WriteAllText(
            Path.Combine(poisonModule, "PoisonedHash.psd1"),
            "@{ RootModule='PoisonedHash.psm1'; ModuleVersion='1.0.0'; " +
            "GUID='3de78631-b4c0-42b6-8c70-9f48d0c44a62'; " +
            "FunctionsToExport=@('Get-FileHash') }");

        ProcessResult result = RunPreflight(tools);
        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestMalformedOvmfSignatureFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "malformed-ovmf");
        File.WriteAllBytes(tools.OvmfCodePath, new byte[65536]);

        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF invalid",
            "ovmf_invalid",
            unexpectedMarker: "EFI_PREFLIGHT_OK");
    }

    private static bool TestOvmfPathWithSpacesPasses(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(
            tempRoot,
            "injected ovmf path with spaces");

        ProcessResult result = RunPreflight(tools);
        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestOvmfEvidenceWithConcurrentReader(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-concurrent-reader");
        string expectedHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(tools.OvmfCodePath)));

        using FileStream reader = new FileStream(
            tools.OvmfCodePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        ProcessResult result = RunPreflight(tools);

        string exactEvidence =
            "EFI_PREFLIGHT_OVMF path=\"" +
            Path.GetFullPath(tools.OvmfCodePath) +
            "\" bytes=65536 sha256=" +
            expectedHash;
        return IsSuccessfulEvidence(result, tools)
            && result.Output.Contains(exactEvidence, StringComparison.Ordinal)
            && reader.Length == 65536;
    }

    private static bool TestUnavailableOvmfEvidenceFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-exclusive-reader");
        using FileStream exclusiveReader = new FileStream(
            tools.OvmfCodePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(
            result,
            "EFI_PREFLIGHT_OVMF evidence_unavailable",
            "ovmf_evidence_unavailable",
            unexpectedMarker: "EFI_PREFLIGHT_OK")
            && exclusiveReader.Length == 65536;
    }

    private static bool TestRepeatedOvmfEvidenceReleasesHandles(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-repeat");
        ProcessResult first = RunPreflight(tools);
        ProcessResult second = RunPreflight(tools);
        if (!IsSuccessfulEvidence(first, tools) ||
            !IsSuccessfulEvidence(second, tools))
        {
            return false;
        }

        using (FileStream exclusive = new FileStream(
            tools.OvmfCodePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            if (exclusive.Length != 65536)
            {
                return false;
            }
        }

        File.Delete(tools.OvmfCodePath);
        return !File.Exists(tools.OvmfCodePath);
    }

    private static bool IsMissingFailure(
        ProcessResult result,
        string missingMarker,
        string failureReason,
        string unexpectedMarker)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1
            && result.Output.Contains("TEST_START efi.shell.publish", StringComparison.Ordinal)
            && result.Output.Contains("EFI_PREFLIGHT_START", StringComparison.Ordinal)
            && result.Output.Contains(missingMarker, StringComparison.Ordinal)
            && result.Output.Contains(
                "TEST_FAIL efi.shell.publish " + failureReason,
                StringComparison.Ordinal)
            && !result.Output.Contains(unexpectedMarker, StringComparison.Ordinal)
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal)
            && log.Contains(missingMarker, StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && !log.Contains(SecretCanary, StringComparison.Ordinal);
    }

    private static bool IsSuccessfulEvidence(ProcessResult result, ToolFixture tools)
    {
        if (!File.Exists(result.LogPath))
        {
            return false;
        }

        string log = File.ReadAllText(result.LogPath);
        string[] requiredOutput =
        [
            "EFI_PREFLIGHT_M01_SWITCHES status=ok loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false",
            "EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=false loadedImageStack=false liveAllocator=false ownedPaging=false checkedPortIoBinding=false",
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=122 prerequisiteGap=UefiMemoryType",
            "EFI_PREFLIGHT_DOTNET path=\"",
            "sdkVersion=\"10.0.4",
            "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(tools.LinkerPath) + "\" version=\"14.40.33807\"",
            "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(tools.SdkLibPath) + "\" version=\"10.0.26100.0\"",
            "EFI_PREFLIGHT_ILCOMPILER path=\"" + Path.GetFullPath(tools.IlCompilerPath) + "\" packageVersion=\"10.0.12\"",
            "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=\"" + Path.GetFullPath(tools.NativeAotRuntimePackPath) + "\" packageVersion=\"10.0.12\"",
            "EFI_PREFLIGHT_QEMU path=\"" + Path.GetFullPath(tools.QemuPath) + "\" version=\"QEMU emulator version 99.1-test\"",
            "EFI_PREFLIGHT_QEMU_IMG path=\"" + Path.GetFullPath(tools.QemuImgPath) + "\" version=\"qemu-img version 99.1-test\"",
            "EFI_PREFLIGHT_OVMF path=\"" + Path.GetFullPath(tools.OvmfCodePath) + "\" bytes=65536 sha256=",
            "EFI_PREFLIGHT_OK"
        ];

        foreach (string marker in requiredOutput)
        {
            if (!result.Output.Contains(marker, StringComparison.Ordinal)
                || !log.Contains(marker, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return result.ExitCode == 0
            && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal)
            && !result.Output.Contains(SecretCanary, StringComparison.Ordinal)
            && !log.Contains(SecretCanary, StringComparison.Ordinal);
    }

    private static ToolFixture CreateCompleteToolFixture(string tempRoot, string name)
    {
        string root = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(root);

        string msvcRoot = Path.Combine(root, "msvc");
        string linkerDirectory = Path.Combine(msvcRoot, "14.40.33807", "bin", "Hostx64", "x64");
        string linkerPath = Path.Combine(linkerDirectory, "link.exe");
        WriteMinimalPeX64(linkerPath);

        string sdkRoot = Path.Combine(root, "sdk");
        string libraryDirectory = Path.Combine(sdkRoot, "10.0.26100.0", "um", "x64");
        string sdkLibPath = Path.Combine(libraryDirectory, "kernel32.lib");
        WriteCoffArchive(sdkLibPath);

        string ilCompilerSearchRoot = Path.Combine(root, "ilcompiler");
        string ilCompilerDirectory = Path.Combine(ilCompilerSearchRoot, "10.0.12", "tools");
        string ilCompilerPath = Path.Combine(ilCompilerDirectory, "ilc.exe");
        WriteMinimalPeX64(ilCompilerPath);

        string runtimePackSearchRoot = Path.Combine(root, "runtime-packs");
        string runtimePackPath = Path.Combine(
            runtimePackSearchRoot,
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.12");
        string runtimePackData = Path.Combine(runtimePackPath, "data");
        Directory.CreateDirectory(runtimePackData);
        File.WriteAllText(
            Path.Combine(runtimePackData, "RuntimeList.xml"),
            "<FileList TargetFrameworkIdentifier=\".NETCoreApp\" TargetFrameworkVersion=\"10.0\" />");

        string qemuPath = Path.Combine(root, "qemu-system-x86_64.cmd");
        File.WriteAllLines(
            qemuPath,
            [
                "@echo off",
                "echo QEMU emulator version 99.1-test",
                "exit /b 0"
            ]);

        string qemuImgPath = Path.Combine(root, "qemu-img.cmd");
        File.WriteAllLines(
            qemuImgPath,
            [
                "@echo off",
                "echo qemu-img version 99.1-test",
                "exit /b 0"
            ]);

        string ovmfSearchRoot = Path.Combine(root, "firmware");
        Directory.CreateDirectory(ovmfSearchRoot);
        string ovmfCodePath = Path.Combine(ovmfSearchRoot, "edk2-x86_64-code.fd");
        byte[] ovmf = new byte[65536];
        ovmf[40] = (byte)'_';
        ovmf[41] = (byte)'F';
        ovmf[42] = (byte)'V';
        ovmf[43] = (byte)'H';
        File.WriteAllBytes(ovmfCodePath, ovmf);

        return new ToolFixture
        {
            Root = root,
            MsvcLinkerSearchRoot = msvcRoot,
            LinkerPath = linkerPath,
            WindowsSdkLibSearchRoot = sdkRoot,
            SdkLibPath = sdkLibPath,
            IlCompilerSearchRoot = ilCompilerSearchRoot,
            IlCompilerPath = ilCompilerPath,
            NativeAotRuntimePackSearchRoot = runtimePackSearchRoot,
            NativeAotRuntimePackPath = runtimePackPath,
            QemuPath = qemuPath,
            QemuImgPath = qemuImgPath,
            OvmfSearchRoot = ovmfSearchRoot,
            OvmfCodePath = ovmfCodePath
        };
    }

    private static string CreateEmptyDirectory(string root, string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteMinimalPeX64(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = new byte[256];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        bytes[0x3c] = 0x80;
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        bytes[0x84] = 0x64;
        bytes[0x85] = 0x86;
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteCoffArchive(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = new byte[68];
        Encoding.ASCII.GetBytes("!<arch>\n").CopyTo(bytes, 0);
        File.WriteAllBytes(path, bytes);
    }

    private static ProcessResult RunPreflight(
        ToolFixture tools,
        bool useSearchRoots = false,
        string? scriptPath = null,
        params string[] additionalArguments)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        scriptPath ??= Path.Combine(repositoryRoot, "scripts", "build-efi-shell.ps1");
        string logPath = Path.Combine(
            tools.Root,
            "efi-shell-publish-" + Guid.NewGuid().ToString("N") + ".log");

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        start.Environment["CLROS_PREFLIGHT_SECRET_CANARY"] = SecretCanary;
        if (!string.IsNullOrWhiteSpace(tools.PowerShellModulePath))
        {
            start.Environment["PSModulePath"] = tools.PowerShellModulePath;
        }
        AddArgument(start, "-NoProfile");
        AddArgument(start, "-ExecutionPolicy", "Bypass");
        AddArgument(start, "-File", scriptPath);
        AddArgument(start, "-PreflightOnly");
        AddArgument(start, "-LogPath", logPath);
        AddArgument(start, "-MsvcLinkerSearchRoot", tools.MsvcLinkerSearchRoot);
        AddArgument(start, "-WindowsSdkLibSearchRoot", tools.WindowsSdkLibSearchRoot);

        if (useSearchRoots)
        {
            AddArgument(start, "-IlCompilerSearchRoot", tools.IlCompilerSearchRoot);
            AddArgument(
                start,
                "-NativeAotRuntimePackSearchRoot",
                tools.NativeAotRuntimePackSearchRoot);
            AddArgument(start, "-OvmfSearchRoot", tools.OvmfSearchRoot);
        }
        else
        {
            AddArgument(start, "-IlCompilerPath", tools.IlCompilerPath);
            AddArgument(start, "-NativeAotRuntimePackPath", tools.NativeAotRuntimePackPath);
            AddArgument(start, "-OvmfCodePath", tools.OvmfCodePath);
        }

        AddArgument(start, "-QemuPath", tools.QemuPath);
        AddArgument(start, "-QemuImgPath", tools.QemuImgPath);
        AddArgument(start, additionalArguments);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(PreflightProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                "EFI preflight did not exit within " +
                PreflightProcessTimeoutMilliseconds +
                "ms.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult(),
            logPath);
    }

    private static ProcessResult RunProcess(
        string fileName,
        string workingDirectory,
        int timeoutMilliseconds,
        params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        AddArgument(start, arguments);

        return RunProcess(start, timeoutMilliseconds, logPath: string.Empty);
    }

    private static ProcessResult RunProcess(
        ProcessStartInfo start,
        int timeoutMilliseconds,
        string logPath)
    {
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                "Failed to start " + start.FileName + ".");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                start.FileName +
                " did not exit within " +
                timeoutMilliseconds +
                "ms.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult(),
            logPath);
    }

    private static void AddArgument(ProcessStartInfo start, params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
    }

    private readonly struct ProcessResult(int exitCode, string output, string logPath)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;

        public string LogPath { get; } = logPath;
    }

    private sealed class SourceClosureFixture
    {
        public required string Root { get; init; }

        public required string ProjectPath { get; init; }

        public required string ScriptPath { get; init; }

        public required string SerializerTargetsPath { get; init; }
    }

    private sealed class ToolFixture
    {
        public required string Root { get; init; }

        public required string MsvcLinkerSearchRoot { get; set; }

        public required string LinkerPath { get; init; }

        public required string WindowsSdkLibSearchRoot { get; set; }

        public required string SdkLibPath { get; init; }

        public required string IlCompilerSearchRoot { get; init; }

        public required string IlCompilerPath { get; set; }

        public required string NativeAotRuntimePackSearchRoot { get; init; }

        public required string NativeAotRuntimePackPath { get; set; }

        public required string QemuPath { get; set; }

        public required string QemuImgPath { get; set; }

        public required string OvmfSearchRoot { get; init; }

        public required string OvmfCodePath { get; set; }

        public string? PowerShellModulePath { get; set; }
    }
}
