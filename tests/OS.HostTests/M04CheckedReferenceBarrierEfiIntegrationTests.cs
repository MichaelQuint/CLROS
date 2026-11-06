using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;

#if CHECKED_REFERENCE_CORE_TEST
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

#else
using Microsoft.CodeAnalysis;
using OS.Tooling;

internal static partial class M04CheckedReferenceBarrierEfiIntegrationTests
{
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m04.010.efi source authority and retained host contracts", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var p = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        const string flag = "EnableM04CheckedReferenceBarrierProbe";
        Need(p.Descendants(flag).Single().Value == "false", "default false");
        Need(p.Descendants("PropertyGroup").Single(x => x.Attribute("Condition")?.Value == "'$(" + flag + ")' == 'true'").Element("DefineConstants")?.Value == "$(DefineConstants);M04_CHECKED_REFERENCE_BARRIER_PROBE", "literal symbol");
        Need(CheckedReferenceHostOracleMatches(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj")),
            "original host family retains reviewed successor bytes");
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(
            [.. Chain.Append(Flag).Select(name => "--" + name)], true).Profile());
        var method = source.Method("OS.Boot.EfiEntry+M01StageOneAdapter", "TryGetOriginalReferenceStack",
            candidate => candidate.IsStatic && candidate.Arity == 0 && !candidate.ReturnsByRef
                && !candidate.ReturnsByRefReadonly && candidate.DeclaredAccessibility == Accessibility.Internal
                && candidate.ReturnType.SpecialType == SpecialType.System_Boolean);
        _ = source.MethodInvocations(method);
        string getter = HostSourceText.Active(method);
        string entry = source.Document(method).Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (string token in new[]
        {
            "fixed (M01LoadedImageStackStaticState* state = &s_staticState)",
            "IsFinalStageOneSuccess(state, M01LoadedImageStackStaticState.RequiredStorageBytes)",
            "record.StorageAddress != (ulong)&state->SwitchStateStorage",
            "!record.ObservedRsp.Equals(plan.ExpectedContinuationStackPointer)",
            "!plan.StackLayout.BackingStart.Equals(state->StackLayoutStorage.BackingStart)",
            "high - low != UsableStackPageCount * PageSize"
        }

        )
            Need(HostSourceText.Contains(getter, token), "original active stack source " + token);
        Need(entry.Contains("#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE\n        internal static bool TryGetOriginalReferenceStack(", StringComparison.Ordinal), "getter limited to the three reviewed stack-observing profiles");
        const string core = "OS.Runtime.RhpNewFastAllocationCore";
        var admission = source.Method(core, "ReferenceStackAdmission");
        _ = source.MethodInvocations(admission);
        var budget = source.Type(core).GetMembers("ReferenceStackBudget").OfType<IFieldSymbol>().Single();
        Need(HostSourceText.Contains(HostSourceText.Active(admission),
                "witness - (start + 16) < ReferenceStackBudget")
            && budget is { IsConst: true, ConstantValue: 16384UL },
            "end-exclusive strip clearance remains exact16KiB");
    }

    [HostTest("m04.010.efi actual POD core roles and rejection atomicity", HostLane.Native, 1)]
    private static void TestCore()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string path = Path.Combine(root, "artifacts/issue537-tests/core", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        string includes = string.Join('\n', new[] { "src/OS/Runtime/RhpNewFastAllocation.RhpAdmissionStatus.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpAllocationControl.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpObjectObservation.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpObservationStorage.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpAllocationAttempt.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpNewFastAllocationCore.cs",
            "src/OS/Runtime/RhpNewFastAllocation.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpPlainFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpReferenceFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpGenericFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpFixedValueFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpReferenceValueFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpGenericValueFixture.cs",
            "src/OS/Runtime/RhpNewFastAllocation.RhpEnumFixture.cs", "src/OS/Runtime/CheckedReferenceGeometry.cs",
            "src/OS/Runtime/CheckedReferenceState.cs",
            "src/OS/Runtime/CheckedReferenceStackState.cs",
            "src/OS/Runtime/RhpNewFastAllocationCore.cs",
            "src/OS/Runtime/CheckedReferenceBarrierAdapter.RhpNewFastAllocation.cs",
            "src/OS/Runtime/ReferenceCell.cs",
            "src/OS/Runtime/ReferencePair.cs",
            "src/OS/Runtime/ReferenceBlock.cs",
            "src/OS/Runtime/ReferenceBlockHolder.cs", "tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.cs",
            "tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.Result.cs",
            "tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.CheckedReferenceCoreProgram.cs",
            "tests/OS.HostTests/CheckedReferenceCoreProgram.S.cs",
            "tests/OS.HostTests/CheckedReferenceCoreProgram.F.cs" }.Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string project = Path.Combine(path, "Core.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>CHECKED_REFERENCE_CORE_TEST</DefineConstants><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup>
            """ + includes + "<ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>", new UTF8Encoding(false));
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string arg in new[]
        {
            "run",
            "--project",
            project,
            "-c",
            "Release",
            "--no-launch-profile",
            "--disable-build-servers",
            "-p:UseSharedCompilation=false"
        }

        )
            start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        File.WriteAllText(Path.Combine(path, "command.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            File = "dotnet",
            Arguments = start.ArgumentList.ToArray()
        }));
        using Process process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(180000);
        if (timedOut)
        {
            process.Kill(true);
            process.WaitForExit();
        }

        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(path, "stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(path, "stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(path, "exit.txt"), process.ExitCode.ToString());
        File.WriteAllText(Path.Combine(path, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            TimedOut = timedOut,
            ExitCode = process.ExitCode
        }));
        if (timedOut)
            throw new TimeoutException("complete reference core; evidence=" + path);
        Need(process.ExitCode == 0 && stderr.Result.Length == 0 && stdout.Result.Split('\n').Count(x => x.TrimEnd('\r') == "TEST_PASS reference.core.tests") == 1, stdout.Result + stderr.Result);
    }

    private const string Flag = "EnableM04CheckedReferenceBarrierProbe";
    private static readonly string[] Chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe"];
    private static string Scratch(string name)
    {
        string p = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue537-tests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static Result Execute(string executable, string[] arguments, string directory, int timeout = 900000)
    {
        Directory.CreateDirectory(directory);
        string root = HostToolProcess.FindRepositoryRoot();
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string arg in arguments)
            start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        var begun = DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory, "command.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Executable = executable,
            Arguments = arguments,
            WorkingDirectory = root,
            StartedUtc = begun
        }));
        using Process p = Process.Start(start)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        bool timedOut = !p.WaitForExit(timeout);
        if (timedOut)
        {
            p.Kill(true);
            p.WaitForExit();
        }

        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            ExitCode = p.ExitCode,
            TimedOut = timedOut,
            StartedUtc = begun,
            EndedUtc = DateTime.UtcNow
        }));
        return new(p.ExitCode, stdout.Result, stderr.Result, timedOut);
    }

    private static void Passed(Result r) => Need(!r.TimedOut && r.Code == 0 && r.Error.Length == 0, r.Output + r.Error);
    private static string[] ToolArguments(string command, params string[] args) => [.. command.Split(' '), .. args];
    private static Result Command(string[] args, string directory)
    {
        Directory.CreateDirectory(directory);
        var result = HostToolProcess.RunTool(args);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), result.Output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), result.Error);
        return new(result.ExitCode, result.Output, result.Error, false);
    }

    private static string Between(string source, string first, string last)
    {
        int a = source.IndexOf(first, StringComparison.Ordinal);
        Need(a >= 0, first);
        int b = source.IndexOf(last, a, StringComparison.Ordinal);
        Need(b > a, last);
        return source[a..b];
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    [HostTest("m04.010.efi compile-time profile prerequisites and conflicts", HostLane.Fast, 5)]
    private static void TestProfileSource() => _ = ProfileConflicts();
    private static string[] ProfileConflicts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string[] flags = project.Descendants("Error").SelectMany(x => System.Text.RegularExpressions.Regex.Matches(x.Attribute("Condition")?.Value ?? "", @"\$\((Enable[^)]+)\)").Select(m => m.Groups[1].Value)).Distinct(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count && Chain.Length == 8, "complete profile inventory/eight prerequisites");
        string[] conflicts = flags.Except(Chain.Append(Flag), StringComparer.Ordinal).ToArray();
        Need(conflicts.Length == flags.Length - Chain.Length - 1, "complete unrelated profile inventory");
        var conditions = project.Descendants("Error").Select(x => x.Attribute("Condition")!.Value).ToHashSet(StringComparer.Ordinal);
        Need(conditions.Contains("'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "churn inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "constructors inherit GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "dispatch inherits constructor and GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'"), "revocation inherits first-exit prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "unwind inherits original TypeManager prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemSurvivalProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemPanicProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "simple catch inherits exact GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SingleFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "single finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04RethrowProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "rethrow inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')"), "mapping inherits owned first-exit prerequisites");
        Need(conditions.Count(x => x == "'$(EnableM04NestedFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "nested finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04UnhandledExceptionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "unhandled exception inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04FrameDispatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "frame dispatch inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04ConstructorFailureProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "constructor failure inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04FilteredCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "filtered catch inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04NormalFinallyProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "normal finally inherits exact GC prerequisite and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04EhCatalogProbe)' == 'true' and '$(EnableM04FrameDispatchProbe)' != 'true'") == 1, "catalog inherits exact frame prerequisites and conflicts");
        foreach (string conflict in conflicts.Where(x => !OS.Tooling.EfiContracts.FirstExitDependentProbes.Contains(x) && x != "EnableM04X64UnwindMetadataProbe" && x != "EnableM04SeededCollectorChurnProbe" && x != "EnableM04ClassConstructorExecutionProbe" && x != "EnableM04InterfaceDispatchProbe" && x != "EnableM04SimpleThrowCatchProbe" && x != "EnableM04SingleFinallyCatchProbe" && x != "EnableM04RethrowProbe" && x != "EnableM04NestedFinallyCatchProbe" && x != "EnableM04UnhandledExceptionProbe" && x != "EnableM04FrameDispatchProbe" && x != "EnableM04ConstructorFailureProbe" && x != "EnableM04FilteredCatchProbe" && x != "EnableM04EhCatalogProbe" && x != "EnableM04NormalFinallyProbe"))
            Need((conflict == "EnableM03FirstRealExitBootServicesProbe" || conditions.Contains("'$(" + Flag + ")' == 'true' and '$(" + conflict + ")' == 'true'")) && conditions.Contains("'$(" + conflict + ")' == 'true' and '$(" + Flag + ")' == 'true'"), "both conflict directions " + conflict);
        foreach (string required in Chain)
            Need(conditions.Contains("'$(" + Flag + ")' == 'true' and '$(" + required + ")' != 'true'"), "prerequisite " + required);
        return conflicts;
    }

    [HostTest("m04.010.efi full profile prerequisites conflicts and default isolation", HostLane.Native, 3)]
    private static void TestProfiles()
    {
        string directory = Scratch("profiles");
        string[] conflicts = ProfileConflicts();
        int id = 0;
        string[] enabled = [.. Chain, Flag];
        foreach (string[] invalid in Chain.Select(m => enabled.Where(x => x != m).ToArray()).Concat(conflicts.Select(c => enabled.Append(c).ToArray())))
            foreach (string script in new[]
            {
                "efi build",
                "qemu smoke",
                "efi verify"
            }

            )
            {
                string[] mode = script.EndsWith("qemu smoke", StringComparison.Ordinal) ? ["-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi"] : script.EndsWith("efi build", StringComparison.Ordinal) ? ["-PreflightOnly"] : ["-EfiArtifact", "missing.EFI"];
                Result r = Command(ToolArguments(script, [.. mode, .. invalid.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString()));
                Need(!r.TimedOut && r.Code != 0 && (EfiProfileTestControls.MissingParentReason(invalid, ordinarySmoke: script.EndsWith("qemu smoke", StringComparison.Ordinal)) is string parentReason ? r.Output.Contains(parentReason, StringComparison.Ordinal) : invalid.Contains("EnableM04GcStaticRootMaterializationProbe", StringComparer.Ordinal) ? r.Output.Contains("m04_gc_static_profile_conflict property=EnableM04CheckedReferenceBarrierProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03FirstRealExitBootServicesProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_first_exit_requires property=EnableAcpiPhysicalDiscoveryProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03TypedExitBootServicesCallProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : invalid.Contains("EnableM04TypeManagerModuleRegistrationProbe", StringComparer.Ordinal) ? r.Output.Contains("m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03FirmwareEntropySeedProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : r.Output.Contains("m04_checked_reference_profile_", StringComparison.Ordinal)), "first checked reference guard " + r.Output + r.Error);
            }

        foreach (string[] wrong in new[]
        {
            new[]
            {
                "-BootPolicy",
                "Diagnostic",
                "-DryRun"
            },
            new[]
            {
                "-BootPolicy",
                "Diagnostic",
                "-DryRun",
                "-BuildEfi",
                "-SkipImageBuild"
            },
            new[]
            {
                "-BootPolicy",
                "Diagnostic",
                "-DryRun",
                "-BuildEfi",
                "-M01RawObservationLog",
                "forbidden.log"
            }
        }

        )
        {
            Result r = Command(ToolArguments("qemu smoke", [.. wrong, .. enabled.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString()));
            Need(!r.TimedOut && r.Code != 0 && r.Output.Contains("m04_checked_reference_requires_build_and_original_capture", StringComparison.Ordinal), r.Output);
        }

        foreach (string[] selected in new[]
        {
            Array.Empty<string>(),
            enabled
        }

        )
        {
            Result r = Command(ToolArguments("qemu smoke", ["-BootPolicy", "Diagnostic", "-DryRun", .. (selected.Length == 0 ? Array.Empty<string>() : new[] { "-BuildEfi" }), .. selected.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString()));
            Passed(r);
            Need(r.Output.Contains("CLROS_M04_010_NATIVE_READY", StringComparison.Ordinal) == (selected.Length != 0), "default profile remains absent");
        }
    }

    [HostTest("m04.010.efi whole reference transcript and finite rejected mutations", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch("transcript");
        Result dry = Command(ToolArguments("qemu smoke", ["-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", .. Chain.Append(Flag).Select(x => "-" + x)]), Path.Combine(directory, "dryrun"));
        Passed(dry);
        string[] Vector(string name) => dry.Output.Replace("\r", "").Split('\n').Single(x => x.StartsWith(name + " ", StringComparison.Ordinal))[(name.Length + 1)..].Split(';');
        string[] required = Vector("REQUIRED_MARKERS"), ordered = Vector("ORDERED_MARKERS"), unique = Vector("UNIQUE_MARKERS"), forbidden = Vector("FORBIDDEN_MARKERS");
        List<string> lines = ["TEST_START efi.main", .. ordered.Take(6), "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 3", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", .. ordered.Skip(6)];
        const string identity = "allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")] += " " + identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")] += " checkpoint=beforeBootPlan " + identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")] += " pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")] += " freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        Need(lines.Count == 71 && lines[44] == "TEST_START m04.010.references" && lines[63] == "TEST_START boot.sequence", "literal71 positions inherited44 plus19 and8");
        lines[45] = "M04_REFERENCE_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46] = "M04_REFERENCE_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        static string H(ulong x) => "0x" + x.ToString("X16");
        ulong used = 0;
        for (int id = 1; id <= 6; id++)
        {
            ulong size = id <= 2 ? 24UL : id <= 4 ? 56UL : 208UL, flags = id <= 2 ? 0x50000000UL : 0x51000000UL, mt = 0x1001100UL + (ulong)((id - 1) / 2) * 32, allocation = 0x900000 + used, offset = id <= 2 ? 8UL : id <= 4 ? 16UL : 0, scalar = id <= 4 ? 0x1111222233334400UL + (ulong)id : 0;
            lines[46 + id] = $"M04_REFERENCE_OBJECT id={id} mt={H(mt)} prefix0={H(size << 32 | flags)} prefix1={H(0)} prefix2={H(0x1234567800020001)} flags={H(flags)} base={size} allocation={H(allocation)} reference={H(allocation + 8)} end={H(allocation + size)} usedBefore={used} usedAfter={used + size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size - 8} zeroOr={H(0)} scalarOffset={offset} scalar={H(scalar)}";
            used += size;
        }

        Need(used == 576, "independent literal host geometry total");
        lines[53] = "M04_REFERENCE_GEOMETRY state=0x0000000000106000 stateBytes=432 cellReference=8 cellTag=16 cellPayload=24 pairFirst=0 pairLast=8 pairTag=16 blockPayload=8";
        ulong cookie = 0x179BCDE02468ACE0;
        var operations = new (ulong Kind, ulong Calls, ulong Cookie)[]
        {
            (2, 18, cookie),
            (1, 19, cookie ^ 0x1111222233334404UL),
            (2, 20, cookie),
            (3, 22, cookie ^ 0x1928374655AABBCCUL),
            (4, 23, cookie ^ 0x3141592653589707UL)
        };
        for (int i = 0; i < 5; i++)
            lines[54 + i] = $"M04_REFERENCE_OPERATION kind={operations[i].Kind} calls={operations[i].Calls} cookie={H(operations[i].Cookie)}";
        lines[59] = "M04_REFERENCE_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=576 count=6 status=1";
        lines[60] = "M04_REFERENCE_STACK start=0x0000000002000000 end=0x0000000002021000 initialRsp=0x0000000002020FD8 witness=0x0000000002020000 sentinel0=0x0000000002000000 sentinel1=0x0000000002000008 expected0=0x195A86B4C327D0EF expected1=0x62C79E1035AB48D6 armed0=0x195A86B4C327D0EF armed1=0x62C79E1035AB48D6 final0=0x195A86B4C327D0EF final1=0x62C79E1035AB48D6 checks=5 words=2 preserved=true";
        lines[61] = "CLROS_M04_010_NATIVE_READY allocations=6 usedBytes=576 calls=23 initialStores=18 fieldStores=1 checkedStores=1 pairHelpers=2 bulkCalls=1 pairComplete=true blockComplete=true exclusiveNonCollecting=true gate4=Fail";
        string good = string.Join("\r\n", lines) + "\r\n";
        // Feed the actual dry-run vectors to the production streamer, separately from the numeric raw parser below.
        const string ready = "CLROS_M04_010_NATIVE_READY allocations=6", oldPartial = ready + " usedBytes=";
        foreach (string[] vector in new[]
        {
            required,
            ordered,
            unique
        }

        )
            Need(vector.Count(x => x == ready) == 1 && !vector.Contains(oldPartial, StringComparer.Ordinal), "actual complete-token READY in every runner vector");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string text, bool oldAnchor = false)
        {
            string[] Select(string[] vector) => oldAnchor ? vector.Select(x => x == ready ? oldPartial : x).ToArray() : vector;
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(Select(required), forbidden, orderedMarkers: Select(ordered), uniqueMarkers: Select(unique));
            Need(state.Feed(Encoding.ASCII.GetBytes(text)).Succeeded, "actual streamer consumes transcript");
            return state.Complete();
        }

        var accepted = Stream(good);
        Need(accepted.Passed && accepted.LinesProcessed == 71 && accepted.RequiredSeenCount == required.Union(ordered, StringComparer.Ordinal).Union(unique, StringComparer.Ordinal).Count() && accepted.OrderedSeenCount == ordered.Length && accepted.UniqueSeenCount == unique.Length && accepted.SuccessTerminalCount == 1, "actual vectors admit full71 transcript exactly once");
        var partial = Stream(good, true);
        Need(!partial.Passed && partial.MissingRequiredMarkers!.Contains(oldPartial, StringComparer.Ordinal) && partial.UniqueSeenCount == unique.Length - 1 && partial.OutOfOrderOccurrenceCount > 0, "old partial-token READY fails actual required/ordered/unique matching");
        string readyLine = lines[61] + "\r\n";
        var missing = Stream(good.Replace(readyLine, "", StringComparison.Ordinal));
        Need(!missing.Passed && missing.MissingRequiredMarkers!.Contains(ready, StringComparer.Ordinal) && missing.UniqueSeenCount == unique.Length - 1, "missing READY rejected by real streamer");
        var duplicate = Stream(good.Replace(readyLine, readyLine + readyLine, StringComparison.Ordinal));
        Need(!duplicate.Passed && duplicate.DuplicateUniqueMarkerHits!.Contains(ready, StringComparer.Ordinal) && duplicate.DuplicateUniqueOccurrenceCount == 1, "duplicate READY rejected by real streamer");
        string[] swappedReady = lines.ToArray();
        (swappedReady[60], swappedReady[61]) = (swappedReady[61], swappedReady[60]);
        var outOfOrder = Stream(string.Join("\r\n", swappedReady) + "\r\n");
        Need(!outOfOrder.Passed && outOfOrder.OutOfOrderMarkerHits!.Contains(ready, StringComparer.Ordinal) && outOfOrder.OutOfOrderOccurrenceCount > 0, "early READY rejected by real streamer");
        const string stackMarker = "M04_REFERENCE_STACK";
        foreach (string[] vector in new[]
        {
            required,
            ordered,
            unique
        }

        )
            Need(vector.Count(x => x == stackMarker) == 1, "STACK appears once in each actual runner vector");
        string stackLine = lines[60] + "\r\n";
        var missingStack = Stream(good.Replace(stackLine, "", StringComparison.Ordinal));
        Need(!missingStack.Passed && missingStack.MissingRequiredMarkers!.Contains(stackMarker, StringComparer.Ordinal) && missingStack.UniqueSeenCount == unique.Length - 1, "missing STACK rejected by real streamer");
        var duplicateStack = Stream(good.Replace(stackLine, stackLine + stackLine, StringComparison.Ordinal));
        Need(!duplicateStack.Passed && duplicateStack.DuplicateUniqueMarkerHits!.Contains(stackMarker, StringComparer.Ordinal) && duplicateStack.DuplicateUniqueOccurrenceCount == 1, "duplicate STACK rejected by real streamer");
        var lateStack = Stream(good.Replace(stackLine, "", StringComparison.Ordinal).Replace(readyLine, readyLine + stackLine, StringComparison.Ordinal));
        Need(!lateStack.Passed && lateStack.MissingRequiredMarkers!.Count == 0 && lateStack.UniqueSeenCount == unique.Length && lateStack.OutOfOrderMarkerHits!.Contains(ready, StringComparer.Ordinal) && lateStack.OutOfOrderOccurrenceCount > 0, "STACK after READY rejected by real streamer order");
        string acceptedPreamble = Encoding.ASCII.GetString(Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg=="));
        Need(Encoding.ASCII.GetByteCount(acceptedPreamble) == OS.Tooling.QemuContracts.FirmwarePrefixBytes && Hash(Encoding.ASCII.GetBytes(acceptedPreamble)) == OS.Tooling.QemuContracts.FirmwarePrefixSha256, "reviewed firmware preamble");
        List<(string Text, bool Accept)> cases = [(good, true), (acceptedPreamble + good, true), (acceptedPreamble.Replace("Boot0001", "Boot0002", StringComparison.Ordinal) + good, false)];
        for (int i = 0; i < lines.Count; i++)
            foreach (string replacement in new[]
            {
                "",
                lines[i] + " ",
                " " + lines[i],
                lines[i] + " diagnostic",
                lines[i] + "\0",
                lines[i].ToLowerInvariant(),
                lines[i] + "\r\n" + lines[i]
            }

            )
                cases.Add((string.Join("\r\n", lines.Select((line, n) => n == i ? replacement : line)) + "\r\n", false));
        for (int i = 44; i < 62; i++)
        {
            var swapped = lines.ToArray();
            (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
            cases.Add((string.Join("\r\n", swapped) + "\r\n", false));
        }

        foreach (string suffix in new[]
        {
            "\r\n",
            "partial",
            "OVMF menu\r\n",
            "TEST_FAIL m04.010.references bad\r\n"
        }

        )
            cases.Add((good + suffix, false));
        cases.Add((good.Replace("\r\n", "\n", StringComparison.Ordinal), false));
        cases.Add((good[..^1], false));
        cases.Add(("arbitrary firmware\r\n" + good, false));
        foreach ((string a, string b) in new[]
        {
            ("recordsBytes=6672", "recordsBytes=6352"),
            ("stateBytes=432", "stateBytes=424"),
            ("state=0x0000000000106000", "state=0x0000000001001110"),
            ("state=0x0000000000106000", "state=0xFFFFFFFFFFFFFFF8"),
            ("cellPayload=24", "cellPayload=32"),
            ("pairLast=8", "pairLast=0"),
            ("blockPayload=8", "blockPayload=16"),
            ("calls=22", "calls=21"),
            ("flags=0x0000000051000000", "flags=0x0000000050000000"),
            ("zeroBytes=16", "zeroBytes=15"),
            ("contentsZero=false", "contentsZero=true"),
            ("used=576", "used=575"),
            ("exclusiveNonCollecting=true", "exclusiveNonCollecting=false")
        }

        )
        {
            Need(good.Contains(a, StringComparison.Ordinal), "control applies " + a);
            cases.Add((good.Replace(a, b, StringComparison.Ordinal), false));
        }

        foreach ((string a, string b) in new[]
        {
            ("start=0x0000000002000000", "start=0x0000000002000001"),
            ("end=0x0000000002021000", "end=0x0000000002020000"),
            ("initialRsp=0x0000000002020FD8", "initialRsp=0x0000000002020FD0"),
            ("witness=0x0000000002020000", "witness=0x0000000002004008"),
            ("sentinel1=0x0000000002000008", "sentinel1=0x0000000002000010"),
            ("expected0=0x195A86B4C327D0EF", "expected0=0x195A86B4C327D0EE"),
            ("armed0=0x195A86B4C327D0EF", "armed0=0x195A86B4C327D0EE"),
            ("armed1=0x62C79E1035AB48D6", "armed1=0x62C79E1035AB48D7"),
            ("final0=0x195A86B4C327D0EF", "final0=0x195A86B4C327D0EE"),
            ("final1=0x62C79E1035AB48D6", "final1=0x62C79E1035AB48D7"),
            ("checks=5", "checks=4"),
            ("words=2", "words=1"),
            ("preserved=true", "preserved=false")
        }

        )
        {
            Need(good.Contains(a, StringComparison.Ordinal), "stack control applies " + a);
            cases.Add((good.Replace(a, b, StringComparison.Ordinal), false));
        }

        var manifest = cases.Select((c, i) => new { Path = Path.Combine(directory, i + ".log"), c.Accept }).ToArray();
        for (int i = 0; i < cases.Count; i++)
            File.WriteAllText(manifest[i].Path, cases[i].Text, new UTF8Encoding(false));
        string malformed = Path.Combine(directory, "malformed.log");
        File.WriteAllBytes(malformed, [0xC0, 0xAF, .. Encoding.ASCII.GetBytes(good)]);
        manifest = [..manifest, new
        {
            Path = malformed,
            Accept = false
        }

        ];
        string json = Path.Combine(directory, "cases.json");
        File.WriteAllText(json, System.Text.Json.JsonSerializer.Serialize(manifest));
        foreach (var entry in manifest)
        {
            bool acceptedCase = true;
            try
            {
                OS.Tooling.QemuTranscripts.AssertM04CheckedReferenceBarrierTranscript(entry.Path);
            }
            catch (OS.Tooling.ToolFailure)
            {
                acceptedCase = false;
            }

            Need(acceptedCase == entry.Accept, "whole reference transcript control " + entry.Path + " expected=" + entry.Accept);
        }

        Console.WriteLine("TRANSCRIPT_CONTROLS m04.010 cases=" + manifest.Length);
    }

    [HostTest("m04.010.efi actual default and reference no-stdlib NativeAOT artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (bool enabled in new[]
        {
            false,
            true
        }

        )
        {
            string directory = Scratch(enabled ? "artifact-enabled" : "artifact-default"), artifact = Path.Combine(root, "artifacts/efi/issue537-" + Guid.NewGuid().ToString("N") + ".EFI");
            string[] flags = enabled ? [.. Chain, Flag] : [];
            Result r = Command(ToolArguments("efi build", ["-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), .. flags.Select(x => "-" + x)]), Path.Combine(directory, "publish"));
            Passed(r);
            Need(r.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), "complete actual publication and verifier");
            foreach (string suffix in new[]
            {
                "",
                ".link.json",
                ".link.map",
                ".link.rsp",
                ".serializer.obj"
            }.Concat(enabled ? new[] { ".references.obj" } : []))
                File.Copy(artifact + suffix, Path.Combine(directory, "image.EFI" + suffix));
            foreach (var file in new[]
            {
                (OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"), "OS.dll"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.obj", "OS.obj"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml", "OS.map.xml")
            }

            )
                File.Copy(Path.Combine(root, file.Item1), Path.Combine(directory, file.Item2));
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact + ".link.json"));
            var e = json.RootElement;
            var inputs = e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();
            Need(inputs.Length == OS.Tooling.EfiEvidence.SourceClosurePaths(root).Count, "complete raw source inventory");
            foreach (var input in inputs)
            {
                string path = input.GetProperty("path").GetString()!;
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, path));
                Need(Hash(bytes).Equals(input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual source " + path);
                string copy = Path.Combine(directory, "source", path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.WriteAllBytes(copy, bytes);
            }

            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create(null, flags.ToDictionary(flag => flag, _ => true, StringComparer.Ordinal)));
            var evaluation = e.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            var actual = evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)).ToArray();
            Need(actual.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "exact52 flags");
            foreach (var f in actual)
                Need(f.Value.GetString() == (flags.Contains(f.Name, StringComparer.Ordinal) ? "true" : "false"), "actual profile " + f.Name);
            using var pe = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(Path.Combine(directory, "OS.dll")));
            var md = pe.GetMetadataReader();
            Need(md.AssemblyReferences.Count == 0, "real no-stdlib zero AssemblyRefs");
            var methods = md.TypeDefinitions.SelectMany(h => md.GetTypeDefinition(h).GetMethods()).Select(h => md.GetMethodDefinition(h)).ToArray();
            foreach (string name in new[]
            {
                "ReferenceCallback",
                "StoreReferenceField",
                "StoreReferenceUnknown",
                "CopyReferencePair",
                "CopyReferenceBlock",
                "AddressReferencePair",
                "AddressReferenceBlock",
                "ReadReferenceFirst",
                "ReadReferenceLast",
                "ReadReferenceTag",
                "ReferenceStackLine"
            }

            )
            {
                var matches = methods.Where(m => md.GetString(m.Name) == name).ToArray();
                Need(matches.Length == (enabled ? 1 : 0), "actual conditional method " + name);
                if (enabled)
                    Need(matches[0].RelativeVirtualAddress != 0, "actual IL " + name);
            }

            Need(methods.Count(m => md.GetString(m.Name) == "TryGetOriginalReferenceStack") == (enabled ? 2 : 0), "original stack getter and forwarder conditional metadata");
            if (enabled)
            {
                Need(Hash(File.ReadAllBytes(artifact + ".references.obj")) == OS.Tooling.EfiContracts.CheckedReferenceObjectSha256.ToUpperInvariant(), "whole generated bridge object");
                Need(r.Output.Contains("EFI_ARTIFACT_CHECKED_REFERENCE_THUNKS bytes=1645 text=1390 exports=3 callbackRel32=3 callbackGraphAccepted=false unwindSupported=false", StringComparison.Ordinal), "actual full COFF-to-PE callback linkage, separate pending graph");
                var callback = methods.Single(m => md.GetString(m.Name) == "ReferenceCallback");
                Need(md.GetBlobBytes(callback.Signature).AsSpan().SequenceEqual(new byte[] { 0, 4, 9, 11, 11, 11, 11 }), "ordinary uint callback four UInt64 values");
            }

            File.WriteAllText(Path.Combine(directory, "identities.json"), System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(p => new { Path = Path.GetRelativePath(directory, p), Bytes = new FileInfo(p).Length, Sha256 = Hash(File.ReadAllBytes(p)) }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
#endif
