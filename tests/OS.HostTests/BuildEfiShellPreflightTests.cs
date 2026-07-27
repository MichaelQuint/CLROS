using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

internal static class BuildEfiShellPreflightTests
{
    private const string SecretCanary = "CLROS_PREFLIGHT_SECRET_MUST_NOT_APPEAR";

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
            "loadedImageStack=false liveAllocator=false ownedPaging=false");
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
                "loadedImageStack=true liveAllocator=false ownedPaging=false")
            && IsM01SourceClosureSuccess(
                stackAndAllocator,
                "loadedImageStack=true liveAllocator=true ownedPaging=false")
            && IsM01SourceClosureSuccess(
                allThree,
                "loadedImageStack=true liveAllocator=true ownedPaging=true")
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
                "loadedImageStack=false liveAllocator=false ownedPaging=false")
            && IsM01SourceClosureSuccess(
                exitBootServicesFreshMap,
                "loadedImageStack=false liveAllocator=false ownedPaging=false")
            && IsM01SourceClosureSuccess(
                acpiPhysicalDiscovery,
                "loadedImageStack=false liveAllocator=false ownedPaging=false")
            && IsM01SourceClosureSuccess(
                debugConfiguration,
                "loadedImageStack=false liveAllocator=false ownedPaging=false")
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

    private static bool TestM01ForbiddenSourceAndGuardMatrix(string tempRoot)
    {
        ToolFixture tools = CreateCompleteToolFixture(tempRoot, "m01-source-negative-tools");
        (string Name, Action<SourceClosureFixture> Mutate, string Reason)[] cases =
        [
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
                    "\ninternal static class InjectedConstant { public const ulong Value = ulong.MaxValue; }\n"),
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
            if (!ExpectM01SourceClosureFailure(tools, source, reason))
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
                    "<PackageReference Include=\"runtime.win-x64.Microsoft.DotNet.ILCompiler\" Version=\"10.0.8\" PrivateAssets=\"all\" />",
                    "<PackageReference Include=\"Microsoft.DotNet.ILCompiler\" Version=\"10.0.8\" PrivateAssets=\"all\" />")),
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
            if (!ExpectM01SourceClosureFailure(tools, source, expectedReason: null))
            {
                Console.WriteLine("M01 project-closure case failed: " + name);
                return false;
            }
        }

        return true;
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
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=28 prerequisites=3 compileItems=41 prerequisiteGap=UefiMemoryType";
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

    private static bool ExpectM01SourceClosureFailure(
        ToolFixture tools,
        SourceClosureFixture source,
        string? expectedReason)
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

        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.Copy(sourceProjectPath, projectPath, overwrite: true);
        File.Copy(sourceScriptPath, scriptPath, overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(root, "Directory.Build.props"),
            overwrite: true);
        File.Copy(
            Path.Combine(repositoryRoot, "Directory.Build.targets"),
            Path.Combine(root, "Directory.Build.targets"),
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
            ScriptPath = scriptPath
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
            "EFI_PREFLIGHT_OVMF missing",
            "ovmf_not_found",
            unexpectedMarker: "EFI_PREFLIGHT_OK");
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
            "EFI_PREFLIGHT_M01_SWITCHES status=ok loadedImageStack=false liveAllocator=false ownedPaging=false",
            "EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=28 prerequisites=3 compileItems=41 prerequisiteGap=UefiMemoryType",
            "EFI_PREFLIGHT_MSVC_LINKER path=\"" + Path.GetFullPath(tools.LinkerPath) + "\" version=\"14.40.33807\"",
            "EFI_PREFLIGHT_WINDOWS_SDK path=\"" + Path.GetFullPath(tools.SdkLibPath) + "\" version=\"10.0.26100.0\"",
            "EFI_PREFLIGHT_ILCOMPILER path=\"" + Path.GetFullPath(tools.IlCompilerPath) + "\" packageVersion=\"10.0.8\"",
            "EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK path=\"" + Path.GetFullPath(tools.NativeAotRuntimePackPath) + "\" packageVersion=\"10.0.8\"",
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
        string ilCompilerDirectory = Path.Combine(ilCompilerSearchRoot, "10.0.8", "tools");
        string ilCompilerPath = Path.Combine(ilCompilerDirectory, "ilc.exe");
        WriteMinimalPeX64(ilCompilerPath);

        string runtimePackSearchRoot = Path.Combine(root, "runtime-packs");
        string runtimePackPath = Path.Combine(
            runtimePackSearchRoot,
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "10.0.8");
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
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("EFI preflight did not exit within 30 seconds.");
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

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start " + fileName + ".");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                fileName + " did not exit within " + timeoutMilliseconds + "ms.");
        }

        return new ProcessResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult(),
            logPath: string.Empty);
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
    }
}
