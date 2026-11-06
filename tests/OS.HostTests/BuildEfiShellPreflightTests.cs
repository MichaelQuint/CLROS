using OS.Tooling;
using OS.Tooling.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class BuildEfiShellPreflightTests
{
    private const string SecretCanary = "CLROS_PREFLIGHT_SECRET_MUST_NOT_APPEAR";
    private const int PreflightProcessTimeoutMilliseconds = 120000;
    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "clros-efi-preflight-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            Run("efi preflight enforces stable SDK 401 band and reports exact failures", () => TestSdkVersionContract(tempRoot), ref failures);
            Run("efi preflight discovers Visual Studio prerelease toolchains", () => TestPrereleaseVisualStudioDiscovery(tempRoot), ref failures);
            Console.WriteLine("TEST_START m01.020");
            int m01Failures = 0;
            Run("m01.020 exact source closure and false defaults", () => TestM01ExactSourceClosureAndDefaults(tempRoot), ref m01Failures);
            Run("m01.020 staged switch DAG and propagation", () => TestM01StagedSwitchDagAndPropagation(tempRoot), ref m01Failures);
            Run("m01.020 all-three-switch compile", TestM01AllThreeSwitchCompile, ref m01Failures);
            Run("EFI preflight M02 safe-stub switch DAG and propagation", () => TestM02SafeStubSwitchDagAndPropagation(tempRoot), ref m01Failures);
            Run("EFI project M02 all-four-switch compile", TestM02AllFourSwitchCompile, ref m01Failures);
            Run("EFI preflight M02 checked-binding switch DAG and propagation", () => TestM02CheckedBindingSwitchDagAndPropagation(tempRoot), ref m01Failures);
            Run("EFI project M02 all-five-switch compile", TestM02AllFiveSwitchCompile, ref m01Failures);
            Run("EFI preflight M02-013 terminal profile DAG and propagation", () => TestM02Ud2VectorStateSwitchDagAndPropagation(tempRoot), ref m01Failures);
            Run("EFI project M02-013 terminal profile compile", TestM02Ud2VectorStateProfileCompile, ref m01Failures);
            Run("m01.020 rejects forbidden source surfaces and guards", () => TestM01ForbiddenSourceAndGuardMatrix(tempRoot), ref m01Failures);
            Run("m01.020 rejects project closure bypasses", () => TestM01ProjectClosureBypassMatrix(tempRoot), ref m01Failures);
            Run("m01.020 rejects weakened allocation and CR3 contracts", () => TestM01SemanticContractMatrix(tempRoot), ref m01Failures);
            Run("m01.034 rejects serializer targets import bypasses", () => TestM01SerializerTargetsImportMatrix(tempRoot), ref m01Failures);
            Run("m01.034 locks and records the complete publish source and SDK closure", TestM01PublishSourceAndSdkClosure, ref m01Failures);
            Run("m01.034 pins the isolated restore and package build authority", TestM01PinnedRestoreAndPackageAuthority, ref m01Failures);
            Run("m01.034 holds absolute package inputs until explicit release", () => TestM01AbsolutePackageInputLocks(tempRoot), ref m01Failures);
            Run("m01.034 isolates source evaluation from stale project-extension inputs", () => TestM01ProjectExtensionEvaluationIsolation(tempRoot), ref m01Failures);
            Run("m01.034 bounds every owned build process and reaps descendants", () => TestM01BoundedBuildProcessRunner(tempRoot), ref m01Failures);
            Run("m01.034 freezes SDK paths and disables wildcard imports", () => TestM01MsbuildInjectionIsolation(tempRoot), ref m01Failures);
            Run("m01.034 bounds verifier execution and reaps its process tree", () => TestM01ArtifactVerifierTimeout(tempRoot), ref m01Failures);
            Run("m01.034 serializes canonical output ownership and releases it", () => TestM01PublishOutputMutex(tempRoot), ref m01Failures);
            Run("m01.034 rejects unsafe fresh-output cleanup paths", () => TestM01FreshOutputCleanupGuards(tempRoot), ref m01Failures);
            Run("m01.034 holds source read locks until explicit release", () => TestM01PublishSourceLocks(tempRoot), ref m01Failures);
            Run("m01.034 owns output, log, and verifier file authority", () => TestM01OutputAndVerifierAuthority(tempRoot), ref m01Failures);
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
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestSdkVersionContract(string tempRoot)
    {
        const string approved = "11.0.100-rc.3.26522.148";
        if (EfiPreflight.SdkVersionFailure("ok", [approved]) != "ok")
            return false;
        foreach (string version in new[]
        {
            "",
            "10.0.401",
            "11.0.100",
            "11.0.101",
            "11.0.100-rc.1.26504.105",
            "11.0.100-rc.2.26504.105",
            "11.0.100-rc.3.26522.147",
            "11.0.100-rc.3.26522.149",
            "11.0.100-RC.3.26522.148",
            approved + "+extra",
            "invalid"
        }

        )
            if (EfiPreflight.SdkVersionFailure("ok", [version]) != "dotnet_sdk_version_unsupported")
                return false;
        return EfiPreflight.SdkVersionFailure("ok", []) == "dotnet_sdk_version_output_invalid" && EfiPreflight.SdkVersionFailure("ok", [approved, approved]) == "dotnet_sdk_version_output_invalid" && EfiPreflight.SdkVersionFailure("timed_out", []) == "dotnet_sdk_version_timed_out";
    }

    private static bool TestPrereleaseVisualStudioDiscovery(string tempRoot)
    {
        string path = Path.Combine(tempRoot, "vswhere.exe");
        File.WriteAllBytes(path, [0]);
        int calls = 0;
        var roots = EfiPreflight.DefaultLinkerRoots(path, request =>
        {
            calls++;
            if (request.Executable != path || !request.Arguments.SequenceEqual(new[] { "-latest", "-prerelease", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath" }))
                throw new InvalidOperationException("required_vswhere_arguments_missing");
            return new OS.Tooling.ProcessResult(0, tempRoot + "\n", "", false, false, false, TimeSpan.Zero);
        });
        return calls == 1 && roots.Contains(Path.Combine(tempRoot, "VC/Tools/MSVC"), StringComparer.Ordinal);
    }

    internal static bool HasExactCanonicalEfiCompileItem(string include, string link)
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        System.Xml.Linq.XDocument project = System.Xml.Linq.XDocument.Load(projectPath);
        System.Xml.Linq.XElement[] matches = project.Descendants("Compile").Where(element => string.Equals(element.Attribute("Include")?.Value, include, StringComparison.Ordinal) && string.Equals(element.Attribute("Link")?.Value ?? string.Empty, link, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 && matches[0].Attributes().Count() == (link.Length == 0 ? 1 : 2) && !matches[0].Elements().Any() && matches[0].Attribute("Condition") is null && matches[0].Parent?.Attribute("Condition") is null;
    }

    private static bool TestM01ExactSourceClosureAndDefaults(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-exact-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-exact");
        ProcessResult result = RunPreflight(tools, useSearchRoots: false, scriptPath: source.ScriptPath);
        return IsM01SourceClosureSuccess(result, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false");
    }

    private static bool TestM01StagedSwitchDagAndPropagation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-switches");
        ProcessResult stackOnly = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe");
        ProcessResult stackAndAllocator = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe");
        ProcessResult allThree = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM01OwnedPagingProbe");
        ProcessResult allocatorWithoutStack = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LiveAllocatorProbe");
        ProcessResult pagingWithoutAllocator = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01OwnedPagingProbe");
        ProcessResult unknownSwitch = RunPreflight(tools, false, source.ScriptPath, "-EnableM01UnknownProbe");
        ProcessResult uefiGoneSimulation = RunPreflight(tools, false, source.ScriptPath, "-EnableUefiGoneSimulationProbe");
        ProcessResult exitBootServicesFreshMap = RunPreflight(tools, false, source.ScriptPath, "-EnableExitBootServicesFreshMapProbe");
        ProcessResult acpiPhysicalDiscovery = RunPreflight(tools, false, source.ScriptPath, "-EnableAcpiPhysicalDiscoveryProbe");
        ProcessResult debugConfiguration = RunPreflight(tools, false, source.ScriptPath, "-Configuration", "Debug");
        ProcessResult unsupportedConfiguration = RunPreflight(tools, false, source.ScriptPath, "-Configuration", "Custom");
        bool passed = IsM01SourceClosureSuccess(stackOnly, "loadedImageStack=true liveAllocator=false ownedPaging=false m02SafeStub=false") && IsM01SourceClosureSuccess(stackAndAllocator, "loadedImageStack=true liveAllocator=true ownedPaging=false m02SafeStub=false") && IsM01SourceClosureSuccess(allThree, "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=false") && IsM01SwitchFailure(allocatorWithoutStack, "live_allocator_requires_loaded_image_stack") && IsM01SwitchFailure(pagingWithoutAllocator, "owned_paging_requires_live_allocator") && unknownSwitch.ExitCode != 0 && unknownSwitch.Output.Contains("EnableM01UnknownProbe", StringComparison.Ordinal) && !unknownSwitch.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal) && IsM01SourceClosureSuccess(uefiGoneSimulation, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && IsM01SourceClosureSuccess(exitBootServicesFreshMap, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && IsM01SourceClosureSuccess(acpiPhysicalDiscovery, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && IsM01SourceClosureSuccess(debugConfiguration, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && unsupportedConfiguration.ExitCode != 0 && unsupportedConfiguration.Output.Contains("configuration_invalid", StringComparison.OrdinalIgnoreCase) && !unsupportedConfiguration.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal) && PublishIncludes("EnableM01LoadedImageStackProbe") && PublishIncludes("EnableM01LiveAllocatorProbe") && PublishIncludes("EnableM01OwnedPagingProbe") && PublishIncludes("EnableUefiGoneSimulationProbe") && PublishIncludes("EnableExitBootServicesFreshMapProbe") && PublishIncludes("EnableAcpiPhysicalDiscoveryProbe");
        if (!passed)
        {
            Console.WriteLine("M01 switch outputs:\n" + stackOnly.Output + stackAndAllocator.Output + allThree.Output + allocatorWithoutStack.Output + pagingWithoutAllocator.Output + unknownSwitch.Output + uefiGoneSimulation.Output + exitBootServicesFreshMap.Output + acpiPhysicalDiscovery.Output + debugConfiguration.Output + unsupportedConfiguration.Output);
        }

        return passed;
    }

    private static bool TestM01AllThreeSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        ProcessResult result = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM01LoadedImageStackProbe=true", "-p:EnableM01LiveAllocatorProbe=true", "-p:EnableM01OwnedPagingProbe=true");
        bool passed = result.ExitCode == 0 && result.Output.Contains("Build succeeded.", StringComparison.Ordinal) && !result.Output.Contains(": error ", StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02SafeStubSwitchDagAndPropagation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m02-safe-stub-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m02-safe-stub-switches");
        ProcessResult allFour = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM01OwnedPagingProbe", "-EnableM02SafeStubProbe");
        ProcessResult missingLoadedImage = RunPreflight(tools, false, source.ScriptPath, "-EnableM02SafeStubProbe");
        ProcessResult missingAllocator = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM02SafeStubProbe");
        ProcessResult missingOwnedPaging = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM02SafeStubProbe");
        bool passed = IsM01SourceClosureSuccess(allFour, "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true") && allFour.Output.Contains("EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=true loadedImageStack=true liveAllocator=true ownedPaging=true", StringComparison.Ordinal) && IsM02SwitchFailure(missingLoadedImage, "safe_stub_requires_loaded_image_stack") && IsM02SwitchFailure(missingAllocator, "safe_stub_requires_live_allocator") && IsM02SwitchFailure(missingOwnedPaging, "safe_stub_requires_owned_paging") && EfiContracts.DiagnosticProbes.Count(p => p == "EnableM02SafeStubProbe") == 1 && PublishIncludes("EnableM02SafeStubProbe") && EfiContracts.ProbeSymbols.Values.Contains("M02_SAFE_STUB_PROBE");
        if (!passed)
        {
            Console.WriteLine("M02 switch outputs:\n" + allFour.Output + missingLoadedImage.Output + missingAllocator.Output + missingOwnedPaging.Output);
        }

        return passed;
    }

    private static bool TestM02AllFourSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        ProcessResult result = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM01LoadedImageStackProbe=true", "-p:EnableM01LiveAllocatorProbe=true", "-p:EnableM01OwnedPagingProbe=true", "-p:EnableM02SafeStubProbe=true");
        bool passed = result.ExitCode == 0 && result.Output.Contains("Build succeeded.", StringComparison.Ordinal) && !result.Output.Contains(": error ", StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02CheckedBindingSwitchDagAndPropagation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m02-checked-binding-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m02-checked-binding-switches");
        ProcessResult allFive = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM01OwnedPagingProbe", "-EnableM02SafeStubProbe", "-EnableM02CheckedPortIoBindingProbe");
        ProcessResult missingSafeStub = RunPreflight(tools, false, source.ScriptPath, "-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM01OwnedPagingProbe", "-EnableM02CheckedPortIoBindingProbe");
        bool passed = IsM01SourceClosureSuccess(allFive, "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true") && allFive.Output.Contains("EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=true loadedImageStack=true liveAllocator=true ownedPaging=true checkedPortIoBinding=true", StringComparison.Ordinal) && IsM02SwitchFailure(missingSafeStub, "checked_binding_requires_safe_stub") && EfiContracts.DiagnosticProbes.Count(p => p == "EnableM02CheckedPortIoBindingProbe") == 1 && PublishIncludes("EnableM02CheckedPortIoBindingProbe") && EfiContracts.ProbeSymbols.Values.Contains("M02_CHECKED_PORT_IO_BINDING_PROBE");
        if (!passed)
        {
            Console.WriteLine("M02 checked-binding switch outputs:\n" + allFive.Output + missingSafeStub.Output);
        }

        return passed;
    }

    private static bool TestM02AllFiveSwitchCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        ProcessResult result = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM01LoadedImageStackProbe=true", "-p:EnableM01LiveAllocatorProbe=true", "-p:EnableM01OwnedPagingProbe=true", "-p:EnableM02SafeStubProbe=true", "-p:EnableM02CheckedPortIoBindingProbe=true");
        bool passed = result.ExitCode == 0 && result.Output.Contains("Build succeeded.", StringComparison.Ordinal) && !result.Output.Contains(": error ", StringComparison.OrdinalIgnoreCase);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM02Ud2VectorStateSwitchDagAndPropagation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m02-ud2-vector-state-switch-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m02-ud2-vector-state-switches");
        string[] enabledArguments = ["-EnableM02Ud2VectorStateProbe"];
        ProcessResult enabled = RunPreflight(tools, false, source.ScriptPath, enabledArguments);
        ProcessResult independentM04Allowed = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM04IndependentEndianConformanceProbe"]);
        ProcessResult ownedX64GdtConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02OwnedX64GdtEncodingProbe"]);
        ProcessResult guardedIstConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02GuardedIstStackAllocationProbe"]);
        ProcessResult terminalConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02OwnedGdtTssTerminalProbe"]);
        ProcessResult normalizedConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02NormalizedInterruptFramePageProbe"]);
        ProcessResult idtConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02OwnedIdtGatePopulationProbe"]);
        ProcessResult idtrConflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM02IdtrContinuedExecutionProbe"]);
        ProcessResult m04Conflict = RunPreflight(tools, false, source.ScriptPath, [.. enabledArguments, "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe"]);
        bool passed = IsM01SourceClosureSuccess(enabled, "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true") && enabled.Output.Contains("panicSafeOwnedCom1=true", StringComparison.Ordinal) && enabled.Output.Contains("ud2VectorState=true", StringComparison.Ordinal) && enabled.Output.Contains("ownedX64GdtEncoding=false guardedIstStackAllocation=false", StringComparison.Ordinal) && enabled.Output.Contains("EFI_PREFLIGHT_M02_UD2_VECTOR_STATE_IMPLICATION status=ok m01LoadedImageStack=true m01LiveAllocator=true ownedPaging=true m02SafeStub=true checkedPortIoBinding=true com1OwnedPortIo=true ownedCom1Ready=true panicSafeOwnedCom1=true", StringComparison.Ordinal) && IsM01SourceClosureSuccess(independentM04Allowed, "loadedImageStack=true liveAllocator=true ownedPaging=true m02SafeStub=true") && independentM04Allowed.Output.Contains("EFI_PREFLIGHT_M04_SWITCHES status=ok independentEndianConformance=true spanOverlapMemoryCastUnsafeWidthConformance=false", StringComparison.Ordinal) && Fails(() => EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM02Ud2VectorStateProbe"] = true, ["EnableM01LoadedImageStackProbe"] = false }), "m02_ud2_vector_state_probe_explicit_false_prerequisite") && IsM02ProfileSwitchFailure(ownedX64GdtConflict, "m02_ud2_vector_state_probe_forbids_owned_x64_gdt_encoding_probe") && IsM02ProfileSwitchFailure(guardedIstConflict, "m02_ud2_vector_state_probe_forbids_guarded_ist_stack_allocation_probe") && IsM02ProfileSwitchFailure(terminalConflict, "m02_ud2_vector_state_probe_forbids_owned_gdt_tss_terminal") && IsM02ProfileSwitchFailure(normalizedConflict, "m02_ud2_vector_state_probe_forbids_normalized_interrupt_frame_page_probe") && IsM02ProfileSwitchFailure(idtConflict, "m02_ud2_vector_state_probe_forbids_owned_idt_gate_population_probe") && IsM02ProfileSwitchFailure(idtrConflict, "m02_ud2_vector_state_probe_forbids_idtr_continued_execution_probe") && m04Conflict.ExitCode == 1 && m04Conflict.Output.Contains("reason=span_memory_conformance_forbids_m02_ud2_vector_state", StringComparison.Ordinal) && m04Conflict.Output.Contains("TEST_FAIL efi.shell.publish m04_span_memory_conformance_forbids_m02_ud2_vector_state", StringComparison.Ordinal) && !m04Conflict.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal) && EfiContracts.DiagnosticProbes.Count(p => p == "EnableM02Ud2VectorStateProbe") == 1 && PublishIncludes("EnableM02Ud2VectorStateProbe") && EfiContracts.ProbeSymbols.Values.Contains("M02_UD2_VECTOR_STATE_PROBE");
        if (!passed)
        {
            Console.WriteLine("M02-013 switch outputs:\n" + enabled.Output + independentM04Allowed.Output + ownedX64GdtConflict.Output + guardedIstConflict.Output + terminalConflict.Output + normalizedConflict.Output + idtConflict.Output + idtrConflict.Output + m04Conflict.Output);
        }

        return passed;
    }

    private static bool TestM02Ud2VectorStateProfileCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        ProcessResult result = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM02Ud2VectorStateProbe=true");
        ProcessResult explicitFalse = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM02Ud2VectorStateProbe=true", "-p:EnableM02PanicSafeOwnedCom1Probe=false");
        ProcessResult ownedX64GdtConflict = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM02Ud2VectorStateProbe=true", "-p:EnableM02OwnedX64GdtEncodingProbe=true");
        ProcessResult guardedIstConflict = RunProcess("dotnet", repositoryRoot, timeoutMilliseconds: 120000, "build", projectPath, "--nologo", "-m:1", "--disable-build-servers", "-p:EnableM02Ud2VectorStateProbe=true", "-p:EnableM02GuardedIstStackAllocationProbe=true");
        bool passed = result.ExitCode == 0 && result.Output.Contains("Build succeeded.", StringComparison.Ordinal) && !result.Output.Contains(": error ", StringComparison.OrdinalIgnoreCase) && explicitFalse.ExitCode != 0 && explicitFalse.Output.Contains("EnableM02Ud2VectorStateProbe requires EnableM02PanicSafeOwnedCom1Probe=true.", StringComparison.Ordinal) && ownedX64GdtConflict.ExitCode != 0 && ownedX64GdtConflict.Output.Contains("EnableM02Ud2VectorStateProbe cannot be combined with EnableM02OwnedX64GdtEncodingProbe=true.", StringComparison.Ordinal) && guardedIstConflict.ExitCode != 0 && guardedIstConflict.Output.Contains("EnableM02Ud2VectorStateProbe cannot be combined with EnableM02GuardedIstStackAllocationProbe=true.", StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output + explicitFalse.Output + ownedX64GdtConflict.Output + guardedIstConflict.Output);
        }

        return passed;
    }

    private static bool TestM01ForbiddenSourceAndGuardMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-source-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate, string Reason)[] cases = [("reviewed-type-manager-altered-source", source => AppendSource(source, "Runtime", "TypeManagerModuleMetadataLiveNativeAOTImage.cs", "\n// Alter the complete reviewed source identity.\n"), "forbidden_active_source"), ("reviewed-type-manager-wrong-path", source => File.Copy(SourcePath(source, "Runtime", "TypeManagerModuleMetadataLiveNativeAOTImage.cs"), SourcePath(source, "Memory", "PageProtection.cs"), overwrite: true), "forbidden_active_source"), ("reviewed-typed-ebs-adapter-altered-source", source => AppendSource(source, "Boot", "TypedNoStdlibExitBootServicesCallAdapter.cs", "\n// Alter the complete reviewed source identity.\n"), "forbidden_active_source"), ("reviewed-typed-ebs-adapter-wrong-path", source => File.Copy(SourcePath(source, "Boot", "TypedNoStdlibExitBootServicesCallAdapter.cs"), SourcePath(source, "Memory", "PageProtection.cs"), overwrite: true), "forbidden_active_source"), ("reviewed-final-map-altered-source", source => AppendSource(source, "Boot", "NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs", "\n// Alter the complete reviewed source identity.\n"), "forbidden_active_source"), ("reviewed-bounded-retry-altered-source", source => AppendSource(source, "Boot", "BoundedRetryStaleKeyStatus.cs", "\n// Alter the complete reviewed source identity.\n"), "forbidden_active_source"), ("active-span", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedSpan { public static void Use(Span<byte> value) { } }\n"), "forbidden_active_source"), ("active-system-api", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedSystem { public static void Use() { System.GC.KeepAlive(null); } }\n"), "forbidden_active_source"), ("active-string", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedString { public const string Value = \"host\"; }\n"), "forbidden_active_source"), ("active-flags-attribute", source => AppendSource(source, "Memory", "PageProtection.cs", "\n[System.FlagsAttribute] internal enum InjectedFlags : uint { One = 1 }\n"), "forbidden_active_source"), ("active-exception", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedThrow { public static void Fail() { throw null; } }\n"), "forbidden_active_source"), ("active-managed-delegate", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal delegate void InjectedDelegate();\n"), "forbidden_active_source"), ("active-record-type", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal record InjectedRecord;\n"), "forbidden_active_source"), ("active-generic-holder", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal struct InjectedGeneric<T> { public T Value; }\n"), "forbidden_active_source"), ("active-generic-method", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedGenericMethod { public static T Identity<T>(T value) { return value; } }\n"), "forbidden_active_source"), ("active-inline-nonstatic-class", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedOuter { private sealed class InjectedInner { } }\n"), "forbidden_active_source"), ("active-file-class", source => AppendSource(source, "Memory", "PageProtection.cs", "\nfile class InjectedFileClass { }\n"), "forbidden_active_source"), ("active-multidimensional-array", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal struct InjectedArray { public int[,] Values; }\n"), "forbidden_active_source"), ("active-system-alias", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace Injected { using S = global::System; internal struct Holder { public S.Type Value; } }\n"), "forbidden_active_source"), ("active-system-array", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace System { public static class InjectedArray { public static Array Value; } }\n"), "forbidden_active_source"), ("active-system-namespace-reference", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace System { internal struct Holder { public RuntimeType Value; } }\n"), "forbidden_active_source"), ("active-unicode-escaped-system", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace Syst\\u0065m { internal struct Holder { public RuntimeType Value; } }\n"), "forbidden_active_source"), ("active-verbatim-system", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace @System { internal struct Holder { public RuntimeType Value; } }\n"), "forbidden_active_source"), ("active-nameof-string", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedNameof { public static void Use() { _ = nameof(Use); } }\n"), "forbidden_active_source"), ("active-anonymous-object", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedAnonymous { public static void Use() { var value = new { Value = 1 }; } }\n"), "forbidden_active_source"), ("active-inline-attribute", source => AppendSource(source, "Memory", "PageProtection.cs", "\nnamespace Injected { [Injected] internal struct Holder { } }\n"), "forbidden_active_source"), ("active-parameter-attribute", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedParameterAttribute { public static void Use([Injected] int value) { } }\n"), "forbidden_active_source"), ("unavailable-bcl-constant", source => AppendSource(source, "Memory", "PageProtection.cs", "\ninternal static class InjectedConstant { public const int Value = int.MaxValue; }\n"), "forbidden_active_source"), ("ambiguous-positive-nostdlib", source => ReplaceOnce(SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"), "#if !NOSTDLIB_EFI", "#if NOSTDLIB_EFI"), "ambiguous_preprocessor_condition"), ("ambiguous-composite-nostdlib", source => ReplaceOnce(SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"), "#if !NOSTDLIB_EFI", "#if !NOSTDLIB_EFI && HOST_TESTS"), "ambiguous_preprocessor_condition"), ("ambiguous-else", source => ReplaceOnce(SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs"), "#endif", "#else\n#endif"), "ambiguous_preprocessor_condition"), ("undef-nostdlib", source => AppendSource(source, "Memory", "PageProtection.cs", "\n#undef NOSTDLIB_EFI\n"), "unsupported_preprocessor_directive"), ("missing-exact-exclusion", source => RemoveFirstNostdlibExclusion(SourcePath(source, "Boot", "BootMemoryRegionMaterializer.cs")), "nostdlib_efi_exclusion_mismatch")];
        foreach ((string name, Action<SourceClosureFixture> mutate, string reason) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-source-negative-" + name);
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
        (string Name, Action<SourceClosureFixture> Mutate)[] cases = [("missing-required-source", source => File.Delete(SourcePath(source, "Boot", "UefiMemoryType.cs"))), ("boot-sequence-inclusion", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <Compile Include=\"..\\OS.Kernel\\Boot\\BootSequence.cs\" Link=\"Boot\\BootSequence.cs\" />\n")), ("m01-010-planner-inclusion", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <Compile Include=\"..\\OS.Kernel\\Memory\\BootPageAllocatorState.cs\" Link=\"Memory\\BootPageAllocatorState.cs\" />\n")), ("host-test-inclusion", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <Compile Include=\"..\\..\\tests\\OS.HostTests\\M01LivePagingIntegrationTests.cs\" Link=\"Tests\\M01LivePagingIntegrationTests.cs\" />\n")), ("duplicate-compile-item", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <Compile Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" />\n")), ("duplicate-link", source => ReplaceOnce(source.ProjectPath, "Link=\"Boot\\EfiLoadedImageRangeCapturePlan.cs\"", "Link=\"Boot\\EfiLoadedImageRange.cs\"")), ("noncanonical-project", source => ReplaceOnce(source.ProjectPath, "<AssemblyName>OS</AssemblyName>", "<AssemblyName>CLROS.Alternate</AssemblyName>")), ("conditional-required-property-group", source => ReplaceOnce(source.ProjectPath, "<PropertyGroup>", "<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">")), ("attributed-required-property", source => ReplaceOnce(source.ProjectPath, "<NoStdLib>true</NoStdLib>", "<NoStdLib Label=\"unsafe\">true</NoStdLib>")), ("source-glob", source => ReplaceOnce(source.ProjectPath, "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"", "Include=\"..\\OS.Kernel\\Boot\\*.cs\"")), ("property-expanded-source", source => ReplaceOnce(source.ProjectPath, "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"", "Include=\"$(KernelRoot)\\Boot\\UefiMemoryType.cs\"")), ("conditional-future-slot", source => ReplaceOnce(source.ProjectPath, "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"", "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Condition=\"'$(FutureM01)' == 'true'\"")), ("compile-exclude", source => ReplaceOnce(source.ProjectPath, "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"", "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Exclude=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\"")), ("exists-future-slot", source => ReplaceOnce(source.ProjectPath, "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\"", "Include=\"..\\OS.Kernel\\Boot\\UefiMemoryType.cs\" Link=\"Boot\\UefiMemoryType.cs\" Condition=\"Exists('future.cs')\"")), ("target-hidden-compile", source => InsertBeforeProjectEnd(source.ProjectPath, "  <Target Name=\"InjectCompile\"><ItemGroup><Compile Include=\"future.cs\" /></ItemGroup></Target>\n")), ("project-reference", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <ProjectReference Include=\"..\\OS.Kernel\\OS.Kernel.csproj\" />\n")), ("import", source => InsertBeforeProjectEnd(source.ProjectPath, "  <Import Project=\"future.targets\" />\n")), ("choose", source => InsertBeforeProjectEnd(source.ProjectPath, "  <Choose><When Condition=\"'$(FutureM01)' == 'true'\"><ItemGroup><Compile Include=\"future.cs\" /></ItemGroup></When></Choose>\n")), ("analyzer", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <Analyzer Include=\"future-analyzer.dll\" />\n")), ("extra-package-reference", source => InsertBeforeFirstItemGroupEnd(source.ProjectPath, "    <PackageReference Include=\"Future.Build\" Version=\"1.0.0\" PrivateAssets=\"all\" />\n")), ("duplicate-package-reference", source => ReplaceOnce(source.ProjectPath, "<PackageReference Include=\"runtime.win-x64.Microsoft.DotNet.ILCompiler\" Version=\"" + EfiContracts.NativePackageVersion + "\" PrivateAssets=\"all\" />", "<PackageReference Include=\"Microsoft.DotNet.ILCompiler\" Version=\"" + EfiContracts.NativePackageVersion + "\" PrivateAssets=\"all\" />")), ("define-constants-override", source => InsertBeforeProjectEnd(source.ProjectPath, "  <PropertyGroup><DefineConstants>TRACE</DefineConstants></PropertyGroup>\n")), ("inert-switch-validation", source => ReplaceOnce(source.ProjectPath, "BeforeTargets=\"BeforeCompile\"", "AfterTargets=\"CoreCompile\"")), ("directory-build-compile-injection", source => InsertBeforeProjectEnd(Path.Combine(source.Root, "Directory.Build.targets"), "  <ItemGroup><Compile Include=\"future.cs\" /></ItemGroup>\n")), ("directory-build-output-injection", source => InsertBeforeProjectEnd(Path.Combine(source.Root, "Directory.Build.targets"), "  <Target Name=\"FutureSource\"><SomeTask><Output TaskParameter=\"Files\" ItemName=\"Compile\" /></SomeTask></Target>\n")), ("directory-build-target-collision", source => InsertBeforeProjectEnd(Path.Combine(source.Root, "Directory.Build.targets"), "  <Target Name=\"ValidateM01ProbeSwitches\" Condition=\"false\"><Error Condition=\"false\" Text=\"disabled\" /></Target>\n")), ("validation-target-extra-child", source => ReplaceOnce(source.ProjectPath, "\t</Target>", "\t\t<Exec Command=\"echo unsafe\" />\n\t</Target>")), ("target-time-source-mutation", source => InsertBeforeProjectEnd(source.ProjectPath, "  <Target Name=\"MutateExpectedSource\" BeforeTargets=\"CoreCompile\"><WriteLinesToFile File=\"future.cs\" Lines=\"unsafe\" /></Target>\n")), ("initial-target-injection", source => ReplaceOnce(source.ProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\">", "<Project Sdk=\"Microsoft.NET.Sdk\" InitialTargets=\"MutateExpectedSource\">")), ("missing-switch-group", source => RemoveSwitchPropertyGroup(source.ProjectPath, "EnableM01OwnedPagingProbe"))];
        foreach ((string name, Action<SourceClosureFixture> mutate) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-project-negative-" + name);
            mutate(source);
            bool passed = name == "missing-required-source" ? ExpectM01SourceSnapshotFailure(tools, source, "UefiMemoryType.cs") : ExpectM01SourceClosureFailure(tools, source, expectedReason: null);
            if (!passed)
            {
                Console.WriteLine("M01 project-closure case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool ExpectM01SourceSnapshotFailure(ToolFixture tools, SourceClosureFixture source, string expectedPathFragment)
    {
        ProcessResult result = RunPreflight(tools, useSearchRoots: false, scriptPath: source.ScriptPath);
        bool passed = result.ExitCode == 1 && result.Output.Contains("TEST_FAIL efi.shell.publish source_snapshot_failed", StringComparison.Ordinal) && result.Output.Contains(expectedPathFragment, StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_START", StringComparison.Ordinal) && !result.Output.Contains(SecretCanary, StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool TestM01SemanticContractMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-contract-negative-tools");
        (string Name, string Family, string OldText, string NewText)[] cases =
        [
            ("three-allocation-count", "M01LivePagingIntegration",
                "public const ulong RequiredAllocationRecordCount = 3;", "public const ulong RequiredAllocationRecordCount = 2;"),
            ("distinct-protection-allocation", "M01LivePagingIntegration",
                "public const ulong ProtectionProbeAllocationRecordIndex = 1;", "public const ulong ProtectionProbeAllocationRecordIndex = 0;"),
            ("table-state-allocation", "M01LivePagingIntegration",
                "public const ulong TableStateAllocationRecordIndex = 2;", "public const ulong TableStateAllocationRecordIndex = 1;"),
            ("non-image-map-count", "M01LivePagingIntegration",
                "public const ulong RequiredNonImageMappingRequirementCount = 10;", "public const ulong RequiredNonImageMappingRequirementCount = 9;"),
            ("allocator-arena", "M01LivePagingIntegration",
                "BootReservationKind.AllocatorArena", "BootReservationKind.LoadedImage"),
            ("page-table-arena", "M01LivePagingIntegration",
                "BootReservationKind.PageTableArena", "BootReservationKind.LoadedImage"),
            ("post-allocation-map-order", "M01LivePagingIntegration",
                "_mapCaptureOrdinal > _lastAllocationOrdinal", "_mapCaptureOrdinal >= _lastAllocationOrdinal"),
            ("post-map-ledger-order", "M01LivePagingIntegration",
                "_ledgerBuildOrdinal > _mapCaptureOrdinal", "_ledgerBuildOrdinal >= _mapCaptureOrdinal"),
            ("firmware-callback-exclusion", "M01OwnedCr3CompositeTransition",
                "public const bool InvokesFirmwareCallbacks = false;", "public const bool InvokesFirmwareCallbacks = true;"),
            ("arbitrary-callback-exclusion", "M01OwnedCr3CompositeTransition",
                "public const bool InvokesArbitraryCallbacks = false;", "public const bool InvokesArbitraryCallbacks = true;"),
            ("firmware-call-exclusion", "M01OwnedCr3CompositeTransition",
                "public const bool CallsFirmware = false;", "public const bool CallsFirmware = true;"),
            ("restore-order", "M01OwnedCr3CompositeTransition",
                "RestoreCr3ThenCr4ThenRflags", "RestoreCr4ThenCr3ThenRflags"),
            ("firmware-cr3-restoration", "M01OwnedCr3CompositeTransition",
                "RestoreFirmwareCr3", "RetainOwnedCr3"),
            ("post-restore-canary", "M01OwnedCr3CompositeTransition",
                "PostRestoreCanaryMismatch", "PostRestoreCanaryIgnored"),
            ("expected-canary-state", "M01OwnedCr3CompositeTransition",
                "StateExpectedCanaryOffset", "StateExpectedValueOffset"),
            ("cr4-restoration-readback", "M01OwnedCr3CompositeTransition",
                "Cr4RestoreReadbackMismatch", "Cr4RestoreIgnored"),
            ("rflags-restoration-readback", "M01OwnedCr3CompositeTransition",
                "RflagsRestoreReadbackMismatch", "RflagsRestoreIgnored")
        ];
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-contract-negative");
        foreach ((string name, string family, string oldText, string newText) in cases)
        {
            string path = SemanticContractPath(source.Root, source.ProjectPath, family, oldText, EfiContracts.SourceContractTokens);
            if (!WithSemanticMutation(path, oldText, newText, () => ExpectM01SourceClosureFailure(tools, source, "source_contract_missing")))
            {
                Console.WriteLine("M01 semantic-contract case failed: " + name);
                return false;
            }
        }

        return true;
    }

    private static bool WithSemanticMutation(string path, string oldText, string newText, Func<bool> inspect)
    {
        byte[] original = File.ReadAllBytes(path);
        try
        {
            ReplaceAllRequired(path, oldText, newText);
            return inspect();
        }
        finally
        {
            // Each producer call still acquires and verifies its own complete source authority.
            // Restore raw bytes, including encoding and line endings, before the next vector.
            File.WriteAllBytes(path, original);
        }
    }

    [HostTest("efi preflight semantic mutations restore exact bytes and isolate sibling files")]
    private static void SemanticMutationRestoration()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-semantic-mutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "source.cs"), sibling = Path.Combine(root, "sibling.cs");
        byte[] original = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("before=1;\r\n// before=1;\n")];
        byte[] siblingBytes = [0, 0xff, 0x13];
        try
        {
            File.WriteAllBytes(path, original);
            File.WriteAllBytes(sibling, siblingBytes);
            foreach (bool succeeds in new[]
            {
                true,
                false
            }

            )
            {
                bool result = WithSemanticMutation(path, "before=1", "after=2", () =>
                {
                    HostCheck.Equal("after=2;\r\n// after=2;\n", File.ReadAllText(path));
                    HostCheck.True(File.ReadAllBytes(sibling).AsSpan().SequenceEqual(siblingBytes));
                    return succeeds;
                });
                HostCheck.Equal(succeeds, result);
                HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(original));
            }

            bool rejected = false;
            try
            {
                WithSemanticMutation(path, "before=1", "after=2", () =>
                {
                    HostCheck.Equal("after=2;\r\n// after=2;\n", File.ReadAllText(path));
                    throw new InvalidOperationException("controlled semantic inspection failure");
                });
            }
            catch (InvalidOperationException error) when (error.Message == "controlled semantic inspection failure")
            {
                rejected = true;
            }

            HostCheck.True(rejected);
            HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(original));
            bool inspected = false;
            rejected = false;
            try
            {
                WithSemanticMutation(path, "missing mutation token", "after=2", () =>
                {
                    inspected = true;
                    return true;
                });
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("Mutation token was not found", StringComparison.Ordinal))
            {
                rejected = true;
            }

            HostCheck.True(rejected && !inspected);
            HostCheck.True(File.ReadAllBytes(path).AsSpan().SequenceEqual(original));
            HostCheck.True(File.ReadAllBytes(sibling).AsSpan().SequenceEqual(siblingBytes));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool TestM01SerializerTargetsImportMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-serializer-import-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate, string Reason)[] cases = [("missing-import", source => ReplaceOnce(source.ProjectPath, "<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />", string.Empty), "serializer_import_count_mismatch"), ("duplicate-import", source => InsertBeforeProjectEnd(source.ProjectPath, "  <Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />\n"), "serializer_import_count_mismatch"), ("conditional-import", source => ReplaceOnce(source.ProjectPath, "<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" />", "<Import Project=\"..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets\" Condition=\"Exists('future.targets')\" />"), "serializer_import_shape_invalid"), ("property-expanded-import", source => ReplaceOnce(source.ProjectPath, "..\\..\\tools\\OS.X64TextSerializerGenerator\\OS.X64TextSerializerGenerator.targets", "$(FutureTargets)"), "serializer_import_shape_invalid"), ("wildcard-import", source => ReplaceOnce(source.ProjectPath, "OS.X64TextSerializerGenerator.targets", "*.targets"), "serializer_import_shape_invalid"), ("wrong-import-path", source => ReplaceOnce(source.ProjectPath, "OS.X64TextSerializerGenerator.targets", "Alternate.targets"), "serializer_import_shape_invalid"), ("missing-targets", source => File.Delete(source.SerializerTargetsPath), "serializer_targets_missing"), ("project-dtd", source => File.WriteAllText(source.ProjectPath, "<!DOCTYPE Project [<!ENTITY injected \"unsafe\">]>\n" + File.ReadAllText(source.ProjectPath), new UTF8Encoding(false)), "project_malformed"), ("malformed-targets", source => File.WriteAllText(source.SerializerTargetsPath, "<Project>", new UTF8Encoding(false)), "serializer_targets_malformed"), ("targets-dtd", source => File.WriteAllText(source.SerializerTargetsPath, "<!DOCTYPE Project [<!ENTITY injected \"unsafe\">]>\n" + File.ReadAllText(source.SerializerTargetsPath), new UTF8Encoding(false)), "serializer_targets_malformed"), ("hidden-generator-source", source =>
        {
            string hiddenDirectory = Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, ".hidden");
            Directory.CreateDirectory(hiddenDirectory);
            File.WriteAllText(Path.Combine(hiddenDirectory, "Evil.cs"), "internal static class Evil { }\n", new UTF8Encoding(false));
        }, "serializer_generator_compile_count_mismatch"), ("generator-project-reference", source => InsertBeforeProjectEnd(Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, "OS.X64TextSerializerGenerator.csproj"), "  <ItemGroup><ProjectReference Include=\"..\\OS.ImageBuilder\\OS.ImageBuilder.csproj\" /></ItemGroup>\n"), "serializer_build_input_shape_invalid"), ("generator-package-reference", source => InsertBeforeProjectEnd(Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, "OS.X64TextSerializerGenerator.csproj"), "  <ItemGroup><PackageReference Include=\"Future.Generator\" Version=\"1.0.0\" /></ItemGroup>\n"), "serializer_build_input_shape_invalid"), ("generator-analyzer", source => InsertBeforeProjectEnd(Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, "OS.X64TextSerializerGenerator.csproj"), "  <ItemGroup><Analyzer Include=\"future-generator.dll\" /></ItemGroup>\n"), "serializer_build_input_shape_invalid"), ("tools-props-source-generator", source => InsertBeforeProjectEnd(Path.Combine(Directory.GetParent(Path.GetDirectoryName(source.SerializerTargetsPath)!)!.FullName, "Directory.Build.props"), "  <ItemGroup><Analyzer Include=\"future-source-generator.dll\" /></ItemGroup>\n"), "serializer_build_input_shape_invalid"), ("generator-local-props", source => File.WriteAllText(Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, "Directory.Build.props"), "<Project />\n", new UTF8Encoding(false)), "unexpected_build_customization"), ("generator-local-targets", source => File.WriteAllText(Path.Combine(Path.GetDirectoryName(source.SerializerTargetsPath)!, "Directory.Build.targets"), "<Project />\n", new UTF8Encoding(false)), "unexpected_build_customization"), ("tools-directory-targets", source => File.WriteAllText(Path.Combine(Directory.GetParent(Path.GetDirectoryName(source.SerializerTargetsPath)!)!.FullName, "Directory.Build.targets"), "<Project />\n", new UTF8Encoding(false)), "unexpected_build_customization"), ("compile-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <ItemGroup><Compile Include=\"future.cs\" /></ItemGroup>\n"), "serializer_targets_input_injection"), ("project-reference-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <ItemGroup><ProjectReference Include=\"future.csproj\" /></ItemGroup>\n"), "serializer_targets_input_injection"), ("reference-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <ItemGroup><Reference Include=\"Future\" /></ItemGroup>\n"), "serializer_targets_input_injection"), ("analyzer-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <ItemGroup><Analyzer Include=\"future.dll\" /></ItemGroup>\n"), "serializer_targets_input_injection"), ("using-task-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <UsingTask TaskName=\"Future\" AssemblyFile=\"future.dll\" />\n"), "serializer_targets_input_injection"), ("output-injection", source => InsertBeforeProjectEnd(source.SerializerTargetsPath, "  <Target Name=\"Future\"><FutureTask><Output TaskParameter=\"Files\" ItemName=\"Compile\" /></FutureTask></Target>\n"), "serializer_targets_output_injection"), ("target-shape-mutation", source => ReplaceOnce(source.SerializerTargetsPath, "<ClrosX64InstructionPublicationObjectBytes>336</ClrosX64InstructionPublicationObjectBytes>", "<ClrosX64InstructionPublicationObjectBytes>337</ClrosX64InstructionPublicationObjectBytes>"), "serializer_targets_shape_invalid")];
        foreach ((string name, Action<SourceClosureFixture> mutate, string reason) in cases)
        {
            SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-serializer-import-negative-" + name);
            mutate(source);
            bool passed = name == "missing-targets" ? ExpectM01SourceSnapshotFailure(tools, source, "OS.X64TextSerializerGenerator.targets") : ExpectM01SourceClosureFailure(tools, source, reason);
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
        string original = HostToolProcess.FindRepositoryRoot(), scratch = Path.Combine(Path.GetTempPath(), "clros-source-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = CreateSourceClosureFixture(scratch, "closure");
            var paths = EfiEvidence.SourceClosurePaths(source.Root);
            string[] required = ["global.json", "Directory.Build.props", "Directory.Build.targets", "tools/OS.Tooling/EfiBuild.cs", "tools/OS.Tooling/EfiVerify.cs"];
            if (required.Any(p => !paths.Contains(p, StringComparer.Ordinal)) || !paths.SequenceEqual(paths.Order(StringComparer.Ordinal)))
                return false;
            using (AuthorityFiles locks = new())
            {
                var snapshot = EfiEvidence.Snapshot(locks, source.Root, paths, "source_closure", EfiEvidence.MaximumTextBytes, singleLink: true);
                if (snapshot.FileCount != paths.Count || snapshot.Inputs.Any(p => !EfiEvidence.IsHash(p.Sha256)))
                    return false;
                foreach (string relative in required)
                    if (!IoRejected(() => File.WriteAllText(Path.Combine(source.Root, relative), "changed")))
                        return false;
                locks.VerifyUnchanged();
            }

            string added = Path.Combine(source.Root, "tools/OS.Tooling/AddedAuthority.cs");
            File.WriteAllText(added, "// additional tool input");
            if (EfiEvidence.SourceClosurePaths(source.Root).Count != paths.Count + 1)
                return false;
            var commands = Commands(source.Root, []);
            foreach (var request in new[]
            {
                commands.Restore,
                commands.Publish
            }

            )
                if (request.Count(p => p == "-noAutoResponse") != 1 || request.Count(p => p == "-nodeReuse:false") != 1 || !request.Contains("-p:ImportProjectExtensionProps=false") || !request.Contains("-p:ImportProjectExtensionTargets=false") || !request.Contains("-p:ExcludeRestorePackageImports=true") || !request.Contains("-p:UseSharedCompilation=false"))
                    return false;
            if (!commands.Publish.Contains("--no-restore") || !commands.Publish.Contains("-p:NoIncremental=true") || commands.TimeoutSeconds != EfiBuildPolicy.BuildTimeoutSeconds || commands.MaximumOutputCharacters != EfiBuildPolicy.OutputCharacters)
                return false;
            string[] evidence = ["sourceClosureSha256", "sourceClosureInputs", "dotnetHostPath", "dotnetHostSha256", "dotnetSdkVersion", "restoreClosureSha256", "restoreClosureInputs", "projectAssetsSha256", "packageClosureSha256", "packageBuildClosureSha256", "postRestoreEvaluation"];
            return evidence.All(EfiContracts.EvidenceProperties.Contains);
        }
        finally
        {
            if (Directory.Exists(scratch))
                Directory.Delete(scratch, true);
        }
    }

    private static bool TestM01PinnedRestoreAndPackageAuthority()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var archives = EfiContracts.RestoreArchives;
        var packages = EfiContracts.ConsumedPackageTrees;
        var inputs = EfiContracts.PackageBuildClosure;
        if (archives.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != archives.Count || archives.Any(a => !EfiEvidence.IsHash(a.Sha256) || a.FileName != a.Id + "." + a.Version + ".nupkg"))
            return false;
        if (packages.Any(p => p.FileCount <= 0 || p.TotalBytes <= 0 || !EfiEvidence.IsHash(p.AggregateSha256) || Convert.FromBase64String(p.ContentHashSha512).Length != 64))
            return false;
        if (inputs.Select(i => i.Path).Distinct(StringComparer.Ordinal).Count() != inputs.Count || inputs.Any(i => !EfiEvidence.IsHash(i.Sha256) || !archives.Any(a => i.Path.StartsWith(a.Id + "/" + a.Version + "/", StringComparison.Ordinal))))
            return false;
        if (packages.Where(p => p.RootKind != "runtime").Select(p => p.Id + "/" + p.Version).Order().SequenceEqual(archives.Select(a => a.Id + "/" + a.Version).Order()) == false)
            return false;
        var runtime = packages.Single(p => p.RootKind == "runtime");
        var isolatedRuntime = packages.Single(p => p.RootKind == "restore-only");
        if (runtime with
        {
            RootKind = "restore-only"
        }

        != isolatedRuntime)
            return false;
        var project = ContractAudit.ReadProject(Path.Combine(root, "src/OS/OS.csproj"));
        foreach (var package in project.Descendants("PackageReference"))
            if (!archives.Any(a => a.Id.Equals((string?)package.Attribute("Include"), StringComparison.OrdinalIgnoreCase) && a.Version == (string?)package.Attribute("Version")))
                return false;
        foreach (string binding in EfiContracts.ActivePackageImportEdges.Concat(EfiContracts.PackageTaskBindings))
            if (!inputs.Any(p => p.Path == binding.Split('|')[1]))
                return false;
        var commands = Commands(root, []);
        if (!commands.Restore.Contains("--configfile") || !commands.Restore.Contains("--packages") || !commands.Restore.Contains("--force") || !commands.Restore.Contains("--no-cache") || !commands.Restore.Contains("-p:RestoreForceEvaluate=true"))
            return false;
        string[] required = ["ILCompilerTargetsPath", "ILLinkTasksAssembly", "IlcBuildTasksPath", "IlcHostPackagePath", "RuntimePackagePath", "LanguageTargets", "CSharpTargetsPath", "CommonTargetsPath"];
        return required.All(key => commands.Publish.Count(p => p.StartsWith("-p:" + key + "=", StringComparison.Ordinal)) == 1) && commands.Environment["LINK"] is null && commands.Environment["_LINK_"] is null;
    }

    private static bool TestM01AbsolutePackageInputLocks(string tempRoot)
    {
        string root = CreateEmptyDirectory(tempRoot, "absolute package locks with spaces");
        string[] paths = [Path.Combine(root, "Native.targets"), Path.Combine(root, "Build.Tasks.dll")];
        foreach (string path in paths)
            File.WriteAllText(path, "before");
        using (AuthorityFiles locks = new())
        {
            foreach (string path in paths)
                locks.Open(root, path, "package", "package", singleLink: true);
            foreach (string path in paths)
                if (!IoRejected(() => File.WriteAllText(path, "during")) || !IoRejected(() => File.Move(path, path + ".moved")))
                    return false;
            locks.VerifyUnchanged();
        }

        foreach (string path in paths)
        {
            File.WriteAllText(path, "after");
            if (File.ReadAllText(path) != "after")
                return false;
        }

        return true;
    }

    private static bool TestM01ProjectExtensionEvaluationIsolation(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-project-extension-isolation-tools");
        SourceClosureFixture source = CreateSourceClosureFixture(tempRoot, "m01-project-extension-isolation");
        string staleObjRoot = Path.Combine(source.Root, "src", "OS", "obj");
        Directory.CreateDirectory(staleObjRoot);
        File.WriteAllText(Path.Combine(staleObjRoot, "OS.csproj.nuget.g.props"), """
            <Project>
              <PropertyGroup>
                <NoStdLib>false</NoStdLib>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="stale-injected.cs" />
              </ItemGroup>
            </Project>
            """, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(staleObjRoot, "OS.csproj.nuget.g.targets"), """
            <Project InitialTargets="StaleInjectedTarget">
              <Target Name="StaleInjectedTarget">
                <Error Text="stale project-extension target executed" />
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(staleObjRoot, "project.assets.json"), "{\"stale\":true}", new UTF8Encoding(false));
        ProcessResult result = RunPreflight(tools, useSearchRoots: false, scriptPath: source.ScriptPath);
        return IsM01SourceClosureSuccess(result, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && !result.Output.Contains("stale project-extension target executed", StringComparison.Ordinal);
    }

    private static bool TestM01BoundedBuildProcessRunner(string tempRoot)
    {
        string root = CreateEmptyDirectory(tempRoot, "bounded build runner with spaces");
        string dotnet = HostTools.FindExecutable(null, "", "dotnet")!, assembly = typeof(HostTestRunner).Assembly.Location;
        var positive = BoundedJobProcessRunner.Run(dotnet, [assembly, "--runner-control", "environment", .. EfiBuildCommands.ProcessEnvironment.Keys], root, 10, EfiBuildPolicy.MinimumOutputCharacters, EfiBuildCommands.ProcessEnvironment.ToDictionary(p => p.Key, p => p.Value!));
        if (positive.Status != "Success" || !positive.TerminationConfirmed || positive.Output != "[\"1\",\"0\"]")
            return false;
        var overflow = BoundedJobProcessRunner.Run(dotnet, [assembly, "--runner-control", "output"], root, 10, EfiBuildPolicy.MinimumOutputCharacters);
        if (overflow.Status != "OutputOverflow" || !overflow.TerminationConfirmed || overflow.OutputCharacters > EfiBuildPolicy.MinimumOutputCharacters)
            return false;
        string sentinel = Path.Combine(root, "sentinel"), childPid = Path.Combine(root, "child pid"), parentPid = Path.Combine(root, "parent pid");
        var timeout = BoundedJobProcessRunner.Run(dotnet, [assembly, "--efi-control", "tree", sentinel, childPid, parentPid], root, EfiBuildPolicy.MinimumBuildTimeoutSeconds, EfiBuildPolicy.MinimumOutputCharacters);
        if (timeout.Status != "TimedOut" || !timeout.TerminationConfirmed || !File.Exists(childPid) || !File.Exists(parentPid) || ProcessAlive(File.ReadAllText(childPid)) || ProcessAlive(File.ReadAllText(parentPid)))
            return false;
        using (File.Open(sentinel, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        var invalid = BoundedJobProcessRunner.Run(dotnet, [], root, EfiBuildPolicy.MinimumBuildTimeoutSeconds - 1, EfiBuildPolicy.MinimumOutputCharacters);
        return invalid.Status == "InvalidTimeout";
    }

    private static bool TestM01MsbuildInjectionIsolation(string tempRoot)
    {
        var tools = CreateCompleteToolFixture(tempRoot, "msbuild-injection-tools");
        var source = CreateSourceClosureFixture(tempRoot, "msbuild-injection-source");
        string targets = File.ReadAllText(source.SerializerTargetsPath);
        var commands = Commands(source.Root, []);
        foreach (string property in EfiContracts.MsbuildWildcardPropertyNames)
            if (!commands.Restore.Contains("-p:" + property + "=false") || !commands.Publish.Contains("-p:" + property + "=false") || !targets.Contains(property + "=false", StringComparison.Ordinal))
                return false;
        string poison = Path.Combine(tempRoot, "poison msbuild roots");
        Dictionary<string, string?> injected = new()
        {
            ["MSBuildSDKsPath"] = Path.Combine(poison, "sdks"),
            ["MSBuildExtensionsPath"] = Path.Combine(poison, "extensions") + Path.DirectorySeparatorChar,
            ["MSBuildUserExtensionsPath"] = Path.Combine(poison, "user")
        };
        foreach (string property in EfiContracts.MsbuildWildcardPropertyNames)
            injected[property] = "true";
        var prior = injected.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var pair in injected)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            var result = RunPreflight(tools, scriptPath: source.ScriptPath);
            return IsM01SourceClosureSuccess(result, "loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false") && !result.Output.Contains("poison", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            foreach (var pair in prior)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private static bool TestM01ArtifactVerifierTimeout(string tempRoot)
    {
        foreach (string status in new[]
        {
            "Success",
            "TimedOut",
            "CaptureFailed"
        }

        )
            if (!Fails(() => EfiMsbuild.RequireProcess(new BoundedJobProcessResult(status, 0, "", 0, false, "controlled unconfirmed termination"), "artifact_verification"), "artifact_verification_termination_unconfirmed"))
                return false;
        string root = CreateEmptyDirectory(tempRoot, "verifier timeout with spaces");
        string dotnet = HostTools.FindExecutable(null, "", "dotnet")!, assembly = typeof(HostTestRunner).Assembly.Location;
        foreach (bool exitsParent in new[]
        {
            false,
            true
        }

        )
        {
            string tag = exitsParent ? "orphan" : "timeout";
            string sentinel = Path.Combine(root, tag + ".lock"), childPid = Path.Combine(root, tag + " child.pid"), parentPid = Path.Combine(root, tag + " parent.pid");
            List<string> args = [assembly, "--efi-control", "tree", sentinel, childPid, parentPid];
            if (exitsParent)
                args.Add("exit-parent");
            var time = Stopwatch.StartNew();
            var result = BoundedJobProcessRunner.Run(dotnet, args.ToArray(), root, EfiBuildPolicy.MinimumVerifierTimeoutSeconds, EfiBuildPolicy.MinimumOutputCharacters);
            if (time.Elapsed > TimeSpan.FromSeconds(15) || result.Status != "TimedOut" || !result.TerminationConfirmed || !File.Exists(childPid) || ProcessAlive(File.ReadAllText(childPid)) || ProcessAlive(File.ReadAllText(parentPid)))
                return false;
            if (!Fails(() => EfiMsbuild.RequireProcess(result, "artifact_verification"), "artifact_verification_timeout"))
                return false;
            using (File.Open(sentinel, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }

        return true;
    }

    private static bool TestM01PublishOutputMutex(string tempRoot)
    {
        string root = CreateEmptyDirectory(tempRoot, "publish mutex with spaces"), dotnet = HostTools.FindExecutable(null, "", "dotnet")!;
        string assembly = typeof(HostTestRunner).Assembly.Location;
        using (EfiBuildFiles.OpenPublishLock(root))
        {
            foreach (string configuration in new[]
            {
                "Release",
                "Debug"
            }

            )
            {
                var blocked = RunProcess(dotnet, root, 15000, assembly, "--efi-control", "mutex", root, "blocked", configuration, "win-x64");
                if (blocked.ExitCode != 0)
                    return false;
            }
        }

        return RunProcess(dotnet, root, 15000, assembly, "--efi-control", "mutex", root, "acquire").ExitCode == 0;
    }

    private static bool TestM01FreshOutputCleanupGuards(string tempRoot)
    {
        string root = CreateEmptyDirectory(tempRoot, "fresh cleanup with spaces"), relative = "src/OS/obj/Release/net11.0/win-x64";
        string candidate = Path.Combine(root, relative);
        Directory.CreateDirectory(candidate);
        File.WriteAllText(Path.Combine(candidate, "sentinel"), "remove");
        EfiBuildFiles.FreshDirectory(root, candidate, relative);
        if (Path.Exists(candidate))
            return false;
        string outside = CreateEmptyDirectory(tempRoot, "fresh cleanup outside");
        File.WriteAllText(Path.Combine(outside, "keep"), "keep");
        if (!Fails(() => EfiBuildFiles.FreshDirectory(root, outside, relative), "fresh_publish_path_mismatch") || !File.Exists(Path.Combine(outside, "keep")))
            return false;
        foreach (bool ancestor in new[]
        {
            false,
            true
        }

        )
        {
            string link = ancestor ? Path.Combine(root, "src/OS/obj") : candidate;
            if (ancestor)
                Directory.Delete(link, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            string target = CreateEmptyDirectory(tempRoot, ancestor ? "cleanup ancestor target" : "cleanup leaf target");
            string targetCandidate = ancestor ? Path.Combine(target, "Release/net11.0/win-x64") : target;
            Directory.CreateDirectory(targetCandidate);
            string keep = Path.Combine(targetCandidate, "keep");
            File.WriteAllText(keep, "keep");
            TestJunction.Create(link, target);
            try
            {
                if (!Fails(() => EfiBuildFiles.FreshDirectory(root, candidate, relative), "fresh_publish_path_reparse_point") || File.ReadAllText(keep) != "keep")
                    return false;
            }
            finally
            {
                Directory.Delete(link);
            }
        }

        Directory.CreateDirectory(candidate);
        string childTarget = CreateEmptyDirectory(tempRoot, "cleanup nested target"), childLink = Path.Combine(candidate, "nested");
        TestJunction.Create(childLink, childTarget);
        try
        {
            if (!Fails(() => EfiBuildFiles.FreshDirectory(root, candidate, relative), "fresh_publish_path_reparse_point"))
                return false;
        }
        finally
        {
            Directory.Delete(childLink);
        }

        return true;
    }

    private static bool TestM01PublishSourceLocks(string tempRoot)
    {
        string root = CreateEmptyDirectory(tempRoot, "source locks with spaces"), source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "before");
        using (AuthorityFiles locks = new())
        {
            var snapshot = EfiEvidence.Snapshot(locks, root, ["source.txt"], "source_closure", 4096, singleLink: true);
            if (snapshot.FileCount != 1 || !IoRejected(() => File.WriteAllText(source, "during")) || !IoRejected(() => File.Move(source, source + ".moved")) || File.ReadAllText(source) != "before")
                return false;
            locks.VerifyUnchanged();
        }

        File.WriteAllText(source, "after");
        File.Move(source, source + ".moved");
        if (File.ReadAllText(source + ".moved") != "after")
            return false;
        string target = Path.Combine(root, "hardlink-source"), alias = Path.Combine(root, "hardlink-alias");
        File.WriteAllText(target, "hardlink");
        HardLink(alias, target);
        using (AuthorityFiles locks = new())
            if (!Fails(() => EfiEvidence.Snapshot(locks, root, ["hardlink-alias"], "source_closure", 4096, singleLink: true), "authority_output_hardlink_invalid"))
                return false;
        string junctionTarget = CreateEmptyDirectory(root, "junction-target"), junction = Path.Combine(root, "junction");
        File.WriteAllText(Path.Combine(junctionTarget, "source.txt"), "junction");
        TestJunction.Create(junction, junctionTarget);
        try
        {
            using AuthorityFiles locks = new();
            if (!Fails(() => EfiEvidence.Snapshot(locks, junction, ["source.txt"], "source_closure", 4096), "source_closure_reparse_point"))
                return false;
        }
        finally
        {
            Directory.Delete(junction);
        }

        string leaf = Path.Combine(root, "leaf-link");
        return TestSymlink(leaf, target, () =>
        {
            using AuthorityFiles locks = new();
            return Fails(() => EfiEvidence.Snapshot(locks, root, ["leaf-link"], "source_closure", 4096), "source_closure_reparse_point");
        });
    }

    private static bool TestM01OutputAndVerifierAuthority(string tempRoot)
    {
        foreach (string mode in new[]
        {
            "default",
            "checked",
            "gc"
        }

        )
        {
            string root = CreateEmptyDirectory(tempRoot, "output authority " + mode), logPath = Path.Combine(root, "logs/publish.log");
            string[] chain = ["M01LoadedImageStack", "M01LiveAllocator", "M04IndependentEndianConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance", "M04ArithmeticConformance", "M04LayoutContract", "M04ManagedHeapPageRange", "M04RhpNewFast"];
            string[] selected = mode == "default" ? [] : [.. chain, mode == "checked" ? "M04CheckedReferenceBarrier" : "M04GcStaticRootMaterialization"];
            var profile = EfiProfile.Create("Diagnostic", selected.ToDictionary(p => "Enable" + p + "Probe", _ => true));
            var plan = EfiOutputPlan.Create(root, "artifacts/efi/BOOTX64.EFI", logPath, profile);
            string[] expected = [plan.EfiPath, plan.EfiPath + ".link.map", plan.EfiPath + ".link.rsp", plan.EfiPath + ".link.json", plan.EfiPath + ".serializer.obj", ..mode == "default" ? System.Array.Empty<string>() : new[]
            {
                plan.EfiPath + ".references.obj"
            }

            ];
            if (!plan.Paths.SequenceEqual(expected, StringComparer.Ordinal))
                return false;
            plan.Clear();
            if (plan.Paths.Any(Path.Exists))
                return false;
            using var log = EfiBuildLog.Open(logPath);
            log.WriteLine("first");
            log.WriteLine("second");
            log.VerifyIdentity();
            using var logReader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            string text = logReader.ReadToEnd();
            if (!text.StartsWith("first" + Environment.NewLine + "second", StringComparison.Ordinal))
                return false;
            File.WriteAllText(plan.EfiPath, "stale");
            plan.Clear();
            if (Path.Exists(plan.EfiPath))
                return false;
            string work = CreateEmptyDirectory(root, "work");
            string[] sourceNames = ["published.exe", "linked.map", "linked.rsp", "linked.obj", "managed.dll", ..mode == "default" ? System.Array.Empty<string>() : new[]
            {
                "reference.obj"
            }

            ];
            foreach (string name in sourceNames)
                File.WriteAllText(Path.Combine(work, name), name);
            using (AuthorityFiles copies = new())
            {
                foreach ((string name, string destination) in new[]
                {
                    (sourceNames[0], plan.EfiPath),
                    (sourceNames[1], plan.MapPath),
                    (sourceNames[2], plan.ResponsePath),
                    (sourceNames[3], plan.SerializerObjectPath)
                }

                )
                    EfiBuildFiles.CopyCreateNew(copies, root, Path.Combine(work, name), plan.Directory, destination);
                if (mode != "default")
                    EfiBuildFiles.CopyCreateNew(copies, root, Path.Combine(work, "reference.obj"), plan.Directory, plan.EfiPath + ".references.obj");
                EfiBuildFiles.WriteCreateNew(plan.Directory, plan.EvidencePath, "{\"schemaVersion\":3}"u8);
                if (!IoRejected(() => EfiBuildFiles.CopyCreateNew(copies, root, Path.Combine(work, "published.exe"), plan.Directory, plan.EfiPath)))
                    return false;
                using AuthorityFiles locks = new();
                string[] inputs = [..plan.Paths, Path.Combine(work, "linked.obj"), Path.Combine(work, "linked.map"), Path.Combine(work, "managed.dll"), ..mode == "default" ? System.Array.Empty<string>() : new[]
                {
                    Path.Combine(work, "reference.obj")
                }

                ];
                var records = inputs.Select(path => locks.Open(root, path, "verifier", "verifier", singleLink: true)).ToArray();
                AuthorityFiles.RequireDistinct(records);
                if (records.Length != expected.Length + 3 + (mode == "default" ? 0 : 1))
                    return false;
                if (mode != "default" && !Fails(() => AuthorityFiles.RequireDistinct([.. records[..^1], records[^2]]), "authority_output_file_alias"))
                    return false;
                foreach (string path in inputs)
                    if (!IoRejected(() => File.WriteAllText(path, "changed")) || !IoRejected(() => File.Move(path, path + ".replaced")))
                        return false;
                locks.VerifyUnchanged();
                copies.VerifyUnchanged();
                log.VerifyIdentity();
            }

            plan.Clear();
            string target = Path.Combine(work, "hardlink-target");
            File.WriteAllText(target, "target");
            HardLink(plan.EfiPath, target);
            if (!Fails(plan.Clear, "authority_output_hardlink_invalid"))
                return false;
            File.Delete(plan.EfiPath);
            string logAlias = Path.Combine(root, "log-alias");
            HardLink(logAlias, target);
            if (!Fails(() =>
            {
                using var invalid = EfiBuildLog.Open(logAlias);
            }, "log_authority_hardlink_invalid"))
                return false;
            string targetRoot = CreateEmptyDirectory(root, "junction-target"), junction = Path.Combine(root, "junction");
            TestJunction.Create(junction, targetRoot);
            try
            {
                if (!Fails(() => EfiOutputPlan.Create(junction, "artifacts/efi/BOOTX64.EFI", logPath, profile), "output_authority_ancestor_invalid"))
                    return false;
            }
            finally
            {
                Directory.Delete(junction);
            }

            if (!TestSymlink(plan.EfiPath, target, () => Fails(plan.Clear, "output_cleanup_authority_reparse_point")))
                return false;
        }

        return true;
    }

    private static EfiBuildCommands Commands(string root, string[] flags)
    {
        var options = EfiOptions.Parse(["--root", root, .. flags], true);
        var profile = options.Profile();
        string dotnet = HostTools.FindExecutable(null, "", "dotnet")!, sdk = Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", "fixture-sdk");
        var preflight = new EfiPreflightResult(root, "Release", "win-x64", profile, dotnet, "fixture-sdk", sdk, Path.Combine(root, "link.exe"), "1.0", Path.Combine(root, "kernel32.lib"), "1.0", Path.Combine(root, "ilc.exe"), "1.0", "1.0", Path.Combine(root, "runtime"), "1.0");
        return EfiBuildCommands.Create(options, preflight);
    }

    private static bool PublishIncludes(string property)
    {
        List<string> flags = ["-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM01OwnedPagingProbe", "-EnableM02SafeStubProbe", "-EnableM02CheckedPortIoBindingProbe"];
        string flag = "-" + property;
        if (!flags.Contains(flag))
            flags.Add(flag);
        var commands = Commands(HostToolProcess.FindRepositoryRoot(), flags.ToArray());
        string marker = "EFI_PROBE " + EfiContracts.ProbeSymbols[property].Replace("_PROBE", "", StringComparison.Ordinal);
        return commands.Publish.Count(p => p == "-p:" + property + "=true") == 1 && commands.ProbeMarkers.Count(p => p == marker) == 1;
    }

    private static bool Fails(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            if (error.Reason == reason)
                return true;
            Console.WriteLine("Expected " + reason + "; actual " + error.Message);
            return false;
        }

        return false;
    }

    private static bool IoRejected(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static bool ProcessAlive(string value)
    {
        try
        {
            using var process = Process.GetProcessById(int.Parse(value));
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void HardLink(string path, string target)
    {
        if (!CreateHardLinkW(path, target, IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string path, string target, IntPtr securityAttributes);
    private static bool TestSymlink(string link, string target, Func<bool> inspect)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("SYMLINK_UNAVAILABLE privilege");
            return true;
        }
        catch (IOException error) when ((error.HResult & 0xffff) == 1314)
        {
            Console.WriteLine("SYMLINK_UNAVAILABLE privilege");
            return true;
        }

        try
        {
            return inspect();
        }
        finally
        {
            File.Delete(link);
        }
    }

    private static bool IsM01SourceClosureSuccess(ProcessResult result, string expectedSwitches)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        string switchMarker = "EFI_PREFLIGHT_M01_SWITCHES status=ok " + expectedSwitches;
        string closureMarker = $"EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed={EfiContracts.ReviewedSourceIncludes.Count} prerequisites={EfiContracts.PrerequisiteSourceIncludes.Count} compileItems={EfiContracts.CompileItems.Count} prerequisiteGap=UefiMemoryType";
        bool passed = result.ExitCode == 0 && result.Output.Contains(switchMarker, StringComparison.Ordinal) && result.Output.Contains(closureMarker, StringComparison.Ordinal) && result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal) && log.Contains(switchMarker, StringComparison.Ordinal) && log.Contains(closureMarker, StringComparison.Ordinal) && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal) && !result.Output.Contains(SecretCanary, StringComparison.Ordinal) && !log.Contains(SecretCanary, StringComparison.Ordinal);
        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static bool IsM01SwitchFailure(ProcessResult result, string expectedReason)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1 && result.Output.Contains("EFI_PREFLIGHT_M01_SWITCHES", StringComparison.Ordinal) && result.Output.Contains("reason=" + expectedReason, StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL efi.shell.publish m01_" + expectedReason, StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal) && log.Contains("reason=" + expectedReason, StringComparison.Ordinal);
    }

    private static bool IsM02SwitchFailure(ProcessResult result, string expectedReason)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1 && result.Output.Contains("EFI_PREFLIGHT_M02_SWITCHES", StringComparison.Ordinal) && result.Output.Contains("reason=" + expectedReason, StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL efi.shell.publish m02_" + expectedReason, StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal) && log.Contains("reason=" + expectedReason, StringComparison.Ordinal);
    }

    private static bool IsM02ProfileSwitchFailure(ProcessResult result, string expectedReason)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1 && result.Output.Contains("EFI_PREFLIGHT_M02_SWITCHES", StringComparison.Ordinal) && result.Output.Contains("reason=" + expectedReason, StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL efi.shell.publish " + expectedReason, StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal) && log.Contains("reason=" + expectedReason, StringComparison.Ordinal);
    }

    private static bool ExpectM01SourceClosureFailure(ToolFixture tools, SourceClosureFixture source, string? expectedReason, string? expectedDetail = null)
    {
        ProcessResult result = RunPreflight(tools, useSearchRoots: false, scriptPath: source.ScriptPath);
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        bool passed = result.ExitCode == 1 && result.Output.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal) && result.Output.Contains(" invalid", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL efi.shell.publish m01_source_closure_", StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER", StringComparison.Ordinal) && !result.Output.Contains("EFI_PREFLIGHT_OK", StringComparison.Ordinal) && !result.Output.Contains(SecretCanary, StringComparison.Ordinal) && log.Contains("EFI_PREFLIGHT_M01_SOURCE_CLOSURE", StringComparison.Ordinal);
        if (expectedReason is not null)
        {
            passed = passed && result.Output.Contains("reason=" + expectedReason, StringComparison.Ordinal);
        }

        if (expectedDetail is not null)
        {
            string detail = "detail=\"" + expectedDetail + "\"";
            passed = passed && result.Output.Contains(detail, StringComparison.Ordinal) && log.Contains(detail, StringComparison.Ordinal);
        }

        if (!passed)
        {
            Console.WriteLine(result.Output);
        }

        return passed;
    }

    private static SourceClosureFixture CreateSourceClosureFixture(string tempRoot, string name)
    {
        string original = HostToolProcess.FindRepositoryRoot();
        string root = Path.Combine(tempRoot, name + "-repo");
        foreach (string relative in EfiEvidence.SourceClosurePaths(original))
        {
            string destination = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(original, relative), destination, overwrite: true);
        }

        return new SourceClosureFixture
        {
            Root = root,
            ProjectPath = Path.Combine(root, "src/OS/OS.csproj"),
            ScriptPath = Path.Combine(root, "tools/OS.Tooling/EfiBuild.cs"),
            SerializerTargetsPath = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets")
        };
    }

    private static string SourcePath(SourceClosureFixture source, string area, string file)
    {
        return Path.Combine(source.Root, "src", "OS.Kernel", area, file);
    }

    private static void AppendSource(SourceClosureFixture source, string area, string file, string text)
    {
        File.AppendAllText(SourcePath(source, area, file), text, new UTF8Encoding(false));
    }

    private static void ReplaceOnce(string path, string oldText, string newText)
    {
        string text = File.ReadAllText(path);
        int index = text.IndexOf(oldText, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException("Mutation token was not found in " + path + ": " + oldText);
        }

        string replacement = string.Concat(text.AsSpan(0, index), newText, text.AsSpan(index + oldText.Length));
        File.WriteAllText(path, replacement, new UTF8Encoding(false));
    }

    private static void ReplaceAllRequired(string path, string oldText, string newText)
    {
        string text = File.ReadAllText(path);
        if (!text.Contains(oldText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Mutation token was not found in " + path + ": " + oldText);
        }

        File.WriteAllText(path, text.Replace(oldText, newText, StringComparison.Ordinal), new UTF8Encoding(false));
    }

    private static void RemoveFirstNostdlibExclusion(string path)
    {
        string text = File.ReadAllText(path);
        Regex exclusion = new("^\\s*#if !NOSTDLIB_EFI\\s*$.*?^\\s*#endif\\s*$", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        string replacement = exclusion.Replace(text, string.Empty, count: 1);
        if (string.Equals(text, replacement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("No exact NOSTDLIB_EFI exclusion was found in " + path);
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
        Regex group = new("\\s*<PropertyGroup Condition=\"'\\$\\(" + escapedSwitch + "\\)' == 'true'\">.*?</PropertyGroup>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        string replacement = group.Replace(text, string.Empty, count: 1);
        if (string.Equals(text, replacement, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Switch property group was not found: " + switchName);
        }

        File.WriteAllText(projectPath, replacement, new UTF8Encoding(false));
    }

    private static bool TestMissingLinkerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-linker");
        tools.MsvcLinkerSearchRoot = CreateEmptyDirectory(tools.Root, "empty-msvc");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_MSVC_LINKER missing", "platform_linker_missing", unexpectedMarker: "EFI_PREFLIGHT_WINDOWS_SDK");
    }

    private static bool TestMissingWindowsSdkFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-sdk");
        tools.WindowsSdkLibSearchRoot = CreateEmptyDirectory(tools.Root, "empty-sdk");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_WINDOWS_SDK missing", "windows_sdk_um_x64_missing", unexpectedMarker: "EFI_PREFLIGHT_ILCOMPILER") && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingIlCompilerFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ilcompiler");
        tools.IlCompilerPath = Path.Combine(tools.Root, "missing-ilc.exe");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_ILCOMPILER missing", "ilcompiler_missing", unexpectedMarker: "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK") && result.Output.Contains("EFI_PREFLIGHT_WINDOWS_SDK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingRuntimePackFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-runtime-pack");
        tools.NativeAotRuntimePackPath = Path.Combine(tools.Root, "missing-runtime-pack");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK missing", "nativeaot_runtime_pack_missing", unexpectedMarker: "EFI_PREFLIGHT_QEMU") && result.Output.Contains("EFI_PREFLIGHT_ILCOMPILER path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu");
        tools.QemuPath = Path.Combine(tools.Root, "missing-qemu.exe");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_QEMU missing", "qemu_not_found", unexpectedMarker: "EFI_PREFLIGHT_QEMU_IMG") && result.Output.Contains("EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=", StringComparison.Ordinal);
    }

    private static bool TestMissingQemuImgFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-qemu-img");
        tools.QemuImgPath = Path.Combine(tools.Root, "missing-qemu-img.exe");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_QEMU_IMG missing", "qemu_img_not_found", unexpectedMarker: "EFI_PREFLIGHT_OVMF") && result.Output.Contains("EFI_PREFLIGHT_QEMU path=", StringComparison.Ordinal);
    }

    private static bool TestMissingOvmfFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "missing-ovmf");
        tools.OvmfCodePath = Path.Combine(tools.Root, "missing-OVMF_CODE.fd");
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_OVMF missing", "ovmf_not_found", unexpectedMarker: "EFI_PREFLIGHT_OK") && result.Output.Contains("EFI_PREFLIGHT_QEMU_IMG path=", StringComparison.Ordinal);
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
        string olderLinker = Path.Combine(tools.MsvcLinkerSearchRoot, "14.99.99999", "bin", "Hostx64", "x64", "link.exe");
        WriteMinimalPeX64(olderLinker);
        string newestLinker = Path.Combine(tools.MsvcLinkerSearchRoot, "14.100.10000", "bin", "Hostx64", "x64", "link.exe");
        WriteMinimalPeX64(newestLinker);
        string invalidNewestLinker = Path.Combine(tools.MsvcLinkerSearchRoot, "99.0.0", "bin", "Hostx64", "x64", "link.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidNewestLinker)!);
        File.WriteAllText(invalidNewestLinker, string.Empty);
        string olderSdk = Path.Combine(tools.WindowsSdkLibSearchRoot, "10.0.9999.0", "um", "x64", "kernel32.lib");
        WriteCoffArchive(olderSdk);
        string newestSdk = Path.Combine(tools.WindowsSdkLibSearchRoot, "10.0.100000.0", "um", "x64", "kernel32.lib");
        WriteCoffArchive(newestSdk);
        ProcessResult result = RunPreflight(tools);
        bool passed = result.ExitCode == 0 && result.Output.Contains("EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(newestLinker) + "\"", StringComparison.Ordinal) && result.Output.Contains("EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(newestSdk) + "\"", StringComparison.Ordinal) && !result.Output.Contains(Path.GetFullPath(invalidNewestLinker), StringComparison.Ordinal);
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
        return IsMissingFailure(result, "EFI_PREFLIGHT_OVMF invalid", "ovmf_invalid", unexpectedMarker: "EFI_PREFLIGHT_OK");
    }

    private static bool TestOvmfEvidenceIgnoresPoisonedFileHashModule(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-no-modules");
        tools.PowerShellModulePath = Path.Combine(tools.Root, "poisoned-modules");
        string poisonModule = Path.Combine(tools.PowerShellModulePath, "PoisonedHash");
        Directory.CreateDirectory(poisonModule);
        File.WriteAllText(Path.Combine(poisonModule, "PoisonedHash.psm1"), "function Get-FileHash { throw 'poisoned Get-FileHash executed' }\n" + "Export-ModuleMember -Function Get-FileHash\n");
        File.WriteAllText(Path.Combine(poisonModule, "PoisonedHash.psd1"), "@{ RootModule='PoisonedHash.psm1'; ModuleVersion='1.0.0'; " + "GUID='3de78631-b4c0-42b6-8c70-9f48d0c44a62'; " + "FunctionsToExport=@('Get-FileHash') }");
        ProcessResult result = RunPreflight(tools);
        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestMalformedOvmfSignatureFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "malformed-ovmf");
        File.WriteAllBytes(tools.OvmfCodePath, new byte[65536]);
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_OVMF invalid", "ovmf_invalid", unexpectedMarker: "EFI_PREFLIGHT_OK");
    }

    private static bool TestOvmfPathWithSpacesPasses(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "injected ovmf path with spaces");
        ProcessResult result = RunPreflight(tools);
        return IsSuccessfulEvidence(result, tools);
    }

    private static bool TestOvmfEvidenceWithConcurrentReader(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-concurrent-reader");
        string expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tools.OvmfCodePath)));
        using FileStream reader = new FileStream(tools.OvmfCodePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ProcessResult result = RunPreflight(tools);
        string exactEvidence = "EFI_PREFLIGHT_OVMF path=\"" + Path.GetFullPath(tools.OvmfCodePath) + "\" bytes=65536 sha256=" + expectedHash;
        return IsSuccessfulEvidence(result, tools) && result.Output.Contains(exactEvidence, StringComparison.Ordinal) && reader.Length == 65536;
    }

    private static bool TestUnavailableOvmfEvidenceFails(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-exclusive-reader");
        using FileStream exclusiveReader = new FileStream(tools.OvmfCodePath, FileMode.Open, FileAccess.Read, FileShare.None);
        ProcessResult result = RunPreflight(tools);
        return IsMissingFailure(result, "EFI_PREFLIGHT_OVMF evidence_unavailable", "ovmf_evidence_unavailable", unexpectedMarker: "EFI_PREFLIGHT_OK") && exclusiveReader.Length == 65536;
    }

    private static bool TestRepeatedOvmfEvidenceReleasesHandles(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "ovmf-repeat");
        ProcessResult first = RunPreflight(tools);
        ProcessResult second = RunPreflight(tools);
        if (!IsSuccessfulEvidence(first, tools) || !IsSuccessfulEvidence(second, tools))
        {
            return false;
        }

        using (FileStream exclusive = new FileStream(tools.OvmfCodePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (exclusive.Length != 65536)
            {
                return false;
            }
        }

        File.Delete(tools.OvmfCodePath);
        return !File.Exists(tools.OvmfCodePath);
    }

    private static bool IsMissingFailure(ProcessResult result, string missingMarker, string failureReason, string unexpectedMarker)
    {
        string log = File.Exists(result.LogPath) ? File.ReadAllText(result.LogPath) : string.Empty;
        return result.ExitCode == 1 && result.Output.Contains("TEST_START efi.shell.publish", StringComparison.Ordinal) && result.Output.Contains("EFI_PREFLIGHT_START", StringComparison.Ordinal) && result.Output.Contains(missingMarker, StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL efi.shell.publish " + failureReason, StringComparison.Ordinal) && !result.Output.Contains(unexpectedMarker, StringComparison.Ordinal) && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && log.Contains(missingMarker, StringComparison.Ordinal) && !result.Output.Contains(SecretCanary, StringComparison.Ordinal) && !log.Contains(SecretCanary, StringComparison.Ordinal);
    }

    private static bool IsSuccessfulEvidence(ProcessResult result, ToolFixture tools)
    {
        if (!File.Exists(result.LogPath))
        {
            return false;
        }

        string log = File.ReadAllText(result.LogPath);
        string[] requiredOutput = ["EFI_PREFLIGHT_M01_SWITCHES status=ok loadedImageStack=false liveAllocator=false ownedPaging=false m02SafeStub=false", "EFI_PREFLIGHT_M02_SWITCHES status=ok enabled=false loadedImageStack=false liveAllocator=false ownedPaging=false checkedPortIoBinding=false", $"EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed={EfiContracts.ReviewedSourceIncludes.Count} prerequisites={EfiContracts.PrerequisiteSourceIncludes.Count} compileItems={EfiContracts.CompileItems.Count} prerequisiteGap=UefiMemoryType", "EFI_PREFLIGHT_DOTNET path=\"", "sdkVersion=\"" + EfiContracts.SdkVersion + "\"", "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(tools.LinkerPath) + "\" version=\"14.40.33807\"", "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(tools.SdkLibPath) + "\" version=\"10.0.26100.0\"", "EFI_PREFLIGHT_ILCOMPILER path=\"" + Path.GetFullPath(tools.IlCompilerPath) + "\" packageVersion=\"" + EfiContracts.NativePackageVersion + "\"", "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=\"" + Path.GetFullPath(tools.NativeAotRuntimePackPath) + "\" packageVersion=\"" + EfiContracts.NativePackageVersion + "\"", "EFI_PREFLIGHT_QEMU path=\"" + Path.GetFullPath(tools.QemuPath) + "\" version=\"QEMU emulator version 99.1-test\"", "EFI_PREFLIGHT_QEMU_IMG path=\"" + Path.GetFullPath(tools.QemuImgPath) + "\" version=\"qemu-img version 99.1-test\"", "EFI_PREFLIGHT_OVMF path=\"" + Path.GetFullPath(tools.OvmfCodePath) + "\" bytes=65536 sha256=", "EFI_PREFLIGHT_OK"];
        foreach (string marker in requiredOutput)
        {
            if (!result.Output.Contains(marker, StringComparison.Ordinal) || !log.Contains(marker, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return result.ExitCode == 0 && !result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && !result.Output.Contains("TEST_FAIL efi.shell.publish", StringComparison.Ordinal) && !result.Output.Contains(SecretCanary, StringComparison.Ordinal) && !log.Contains(SecretCanary, StringComparison.Ordinal);
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
        string ilCompilerDirectory = Path.Combine(ilCompilerSearchRoot, EfiContracts.NativePackageVersion, "tools");
        string ilCompilerPath = Path.Combine(ilCompilerDirectory, "ilc.exe");
        WriteMinimalPeX64(ilCompilerPath);
        string runtimePackSearchRoot = Path.Combine(root, "runtime-packs");
        string runtimePackPath = Path.Combine(runtimePackSearchRoot, "microsoft.netcore.app.runtime.nativeaot.win-x64", EfiContracts.NativePackageVersion);
        string runtimePackData = Path.Combine(runtimePackPath, "data");
        Directory.CreateDirectory(runtimePackData);
        File.WriteAllText(Path.Combine(runtimePackData, "RuntimeList.xml"), "<FileList TargetFrameworkIdentifier=\".NETCoreApp\" TargetFrameworkVersion=\"11.0\" />");
        string qemuPath = Path.Combine(root, "qemu-system-x86_64.cmd");
        File.WriteAllLines(qemuPath, ["@echo off", "echo QEMU emulator version 99.1-test", "exit /b 0"]);
        string qemuImgPath = Path.Combine(root, "qemu-img.cmd");
        File.WriteAllLines(qemuImgPath, ["@echo off", "echo qemu-img version 99.1-test", "exit /b 0"]);
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

    private static ProcessResult RunPreflight(ToolFixture tools, bool useSearchRoots = false, string? scriptPath = null, params string[] additionalArguments)
    {
        string root = scriptPath is null ? HostToolProcess.FindRepositoryRoot() : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)))!;
        string logPath = Path.Combine(tools.Root, "efi-shell-publish-" + Guid.NewGuid().ToString("N") + ".log");
        List<string> args = ["--root", root, "--preflight-only", "--log-path", logPath, "--msvc-linker-search-root", tools.MsvcLinkerSearchRoot, "--windows-sdk-lib-search-root", tools.WindowsSdkLibSearchRoot, "--qemu-path", tools.QemuPath, "--qemu-img-path", tools.QemuImgPath];
        if (useSearchRoots)
            args.AddRange(["--il-compiler-search-root", tools.IlCompilerSearchRoot, "--native-aot-runtime-pack-search-root", tools.NativeAotRuntimePackSearchRoot, "--ovmf-search-root", tools.OvmfSearchRoot]);
        else
            args.AddRange(["--il-compiler-path", tools.IlCompilerPath, "--native-aot-runtime-pack-path", tools.NativeAotRuntimePackPath, "--ovmf-code-path", tools.OvmfCodePath]);
        args.AddRange(additionalArguments);
        string? prior = Environment.GetEnvironmentVariable("CLROS_PREFLIGHT_SECRET_CANARY");
        string? priorModules = Environment.GetEnvironmentVariable("PSModulePath");
        try
        {
            Environment.SetEnvironmentVariable("CLROS_PREFLIGHT_SECRET_CANARY", SecretCanary);
            if (tools.PowerShellModulePath is not null)
                Environment.SetEnvironmentVariable("PSModulePath", tools.PowerShellModulePath);
            using StringWriter writer = new();
            int exit = EfiBuild.Run(args.ToArray(), writer);
            return new(exit, writer.ToString(), logPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLROS_PREFLIGHT_SECRET_CANARY", prior);
            Environment.SetEnvironmentVariable("PSModulePath", priorModules);
        }
    }

    private static ProcessResult RunProcess(string fileName, string workingDirectory, int timeoutMilliseconds, params string[] arguments)
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

    private static ProcessResult RunProcess(ProcessStartInfo start, int timeoutMilliseconds, string logPath)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start " + start.FileName + ".");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(start.FileName + " did not exit within " + timeoutMilliseconds + "ms.");
        }

        return new ProcessResult(process.ExitCode, standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult(), logPath);
    }

    private static void AddArgument(ProcessStartInfo start, params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
    }
}
