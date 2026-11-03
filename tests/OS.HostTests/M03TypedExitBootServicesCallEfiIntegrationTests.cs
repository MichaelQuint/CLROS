using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#if TYPED_EBS_CAPTURE_HOST
using OS.Boot;
using Core = OS.Boot.EfiEntry.TypedExitBootServicesCallProbe;
using MapCore = OS.Boot.EfiEntry.FinalMapRetryProbe;



















































































#else
using Microsoft.CodeAnalysis;
using OS.Tooling;

internal static partial class M03TypedExitBootServicesCallEfiIntegrationTests
{
    private const string Flag = "EnableM03TypedExitBootServicesCallProbe";
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static string Root() => HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag)
    {
        string path = Path.Combine(Root(), "artifacts/host-tests/m03-typed-ebs-" + tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Result Process(string directory, string file, params string[] args)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        DateTime begin = DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new { File = file, Arguments = args, WorkingDirectory = Root(), StartedUtc = begin }));
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        bool timeout = !process.WaitForExit(900000);
        if (timeout)
        {
            process.Kill(true);
            process.WaitForExit();
        }

        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { NativeExit = process.ExitCode, TimedOut = timeout, StartedUtc = begin, EndedUtc = DateTime.UtcNow }));
        return new(process.ExitCode, timeout, output, error);
    }

    private static Result Tool(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, false, result.Output, result.Error);
    }

    private static void Passed(Result r) => Need(r.Exit == 0 && !r.Timeout && r.Error.Length == 0, r.Output + r.Error);
    [HostTest("m03.004.efi conditional source and unchanged typed host authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = Root();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" && (string? )defaults[0].Attribute("Condition") == "'$(" + Flag + ")' == ''", "one false default");
        var define = project.Descendants("DefineConstants").Single(x => x.Value.Contains("M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE", StringComparison.Ordinal));
        Need((string? )define.Parent?.Attribute("Condition") == "'$(" + Flag + ")' == 'true'" && define.Value == "$(DefineConstants);M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE", "exact conditional symbol");
        var compile = project.Descendants("Compile").Select(x => (string)x.Attribute("Include")!).ToArray();
        Need(compile.Length == OS.Tooling.EfiContracts.CompileItems.Count && compile.Distinct(StringComparer.Ordinal).Count() == OS.Tooling.EfiContracts.CompileItems.Count, "actual ordered full closure");
        Need(compile.SequenceEqual(OS.Tooling.EfiContracts.CompileItems.Select(item => item.Split('|')[0]), StringComparer.Ordinal), "exact reviewed compile source order including typed, first-exit and owned sources");
        var admission = EfiProjectAdmission.InspectStatic(root);
        Need(admission.Succeeded, admission.Reason + ":" + admission.Detail);
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());
        CheckTypedHostAdmission(source, root);
        HostCheck.Registered(typeof(TypedNoStdlibExitBootServicesCallAdapterTests));
        var authority = source.Type("OS.Kernel.Diagnostics.Probes")
            .GetMembers("EnableTypedExitBootServicesCallProbe").OfType<IFieldSymbol>().Single();
        Need(authority is { IsStatic: true, IsConst: true, ConstantValue: false,
            DeclaredAccessibility: Accessibility.Public }
            && authority.Type.SpecialType == SpecialType.System_Boolean, "default false authority");
        const string owner = "OS.Boot.EfiEntry+TypedExitBootServicesCallProbe";
        string core = HostSourceText.Declarations(source, owner);
        string raw = HostSourceText.Documents(source, owner);
        foreach (string text in new[]
        {
            "UnmanagedCallersOnly",
            "MethodImplOptions.NoInlining",
            "s_totalEntries++;",
            "s_totalEntries != 4",
            "s_totalEntries != entries + 1",
            "s_totalEntries != 0",
            "ref *state, Available, key",
            "ref *state, Available, 0",
            "sizeof(TypedExitBootServicesState) != 40",
            "Snapshot before = default",
            "0x8000000000000000UL",
            "0xFFFFFFFFFFFFFFFFUL",
            "0xC000000000000123UL"
        }

        )
            Need(HostSourceText.Contains(core, text), "actual finite coordinator " + text);
        foreach (string forbidden in new[]
        {
            "->BootServices",
            "->ExitBootServices",
            "s_memoryMapSnapshot",
            "new byte[",
            "throw ",
            "string ",
            "Marshal.",
            "Reflection",
            "s_totalEntries ="
        }

        )
            Need(!raw.Contains(forbidden, StringComparison.Ordinal), "no original firmware or hidden runtime " + forbidden);
        var callback = source.Method(owner, "Controlled", TypedCallbackSignature);
        _ = source.MethodInvocations(callback);
        string body = HostSourceText.Active(callback);
        int increment = HostSourceText.IndexOf(body, "s_totalEntries++;");
        int guard = HostSourceText.IndexOf(body, "if (state == null");
        Need(increment >= 0 && guard > increment, "count every ABI entry before guard");
        var calls = HostSourceOperations.CallsTo(source, owner, "Run");
        Need(calls.Length == 1
            && HostSourceText.Contains(HostSourceText.Active(calls.Single()),
                "TypedExitBootServicesCallProbe.Run(imageHandle, console, serial)"),
            "real opaque handle reaches one typed probe call");
        Need(source.Documents.Any(document => document.Text.Contains(
            "#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE", StringComparison.Ordinal)),
            "unchanged separate fresh-map profile");
        string[] flags = Regex.Matches(File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj")), @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "complete profile flags");
        var conditions = project.Descendants("Error").Select(x => (string)x.Attribute("Condition")!).ToArray();
        Need(conditions.Count(x => x == "'$(" + Flag + ")' != '' and '$(" + Flag + ")' != 'true' and '$(" + Flag + ")' != 'false'") == 1, "one value guard");
        Need(conditions.Contains("'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "churn inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "constructors inherit GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "dispatch inherits constructor and GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'"), "revocation inherits first-exit prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "unwind inherits original TypeManager prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemSurvivalProbe");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemPanicProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "simple catch inherits exact GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SingleFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "single finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04RethrowProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "rethrow inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')") == 1, "mapping inherits owned first-exit prerequisites");
        Need(conditions.Count(x => x == "'$(EnableM04NestedFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "nested finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04UnhandledExceptionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "unhandled exception inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04FrameDispatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "frame dispatch inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04ConstructorFailureProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "constructor failure inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04FilteredCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "filtered catch inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04NormalFinallyProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "normal finally inherits exact GC prerequisite and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04EhCatalogProbe)' == 'true' and '$(EnableM04FrameDispatchProbe)' != 'true'") == 1, "catalog inherits exact frame prerequisites and conflicts");
        foreach (string other in flags.Where(x => !OS.Tooling.EfiContracts.FirstExitDependentProbes.Contains(x) && x != Flag && x != "EnableM04X64UnwindMetadataProbe" && x != "EnableM04SeededCollectorChurnProbe" && x != "EnableM04ClassConstructorExecutionProbe" && x != "EnableM04InterfaceDispatchProbe" && x != "EnableM04SimpleThrowCatchProbe" && x != "EnableM04SingleFinallyCatchProbe" && x != "EnableM04RethrowProbe" && x != "EnableM04NestedFinallyCatchProbe" && x != "EnableM04UnhandledExceptionProbe" && x != "EnableM04FrameDispatchProbe" && x != "EnableM04ConstructorFailureProbe" && x != "EnableM04FilteredCatchProbe" && x != "EnableM04EhCatalogProbe" && x != "EnableM04NormalFinallyProbe"))
        {
            Need((other == "EnableM03FirstRealExitBootServicesProbe" || conditions.Count(x => x == "'$(" + Flag + ")' == 'true' and '$(" + other + ")' == 'true'") == 1) && conditions.Count(x => x == "'$(" + other + ")' == 'true' and '$(" + Flag + ")' == 'true'") == 1, "exact reciprocal exclusive row " + other);
        }


    }

    [HostTest("m03.004.efi actual mapped stable states and controlled typed callback", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root = Root(), directory = Scratch("actual-core");
        string[] sources = ["src/OS/Boot/EfiEntry.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.cs",
            "src/OS/Boot/EfiEntry.EfiTableHeader.cs",
            "src/OS/Boot/EfiEntry.EfiSimpleTextOutputProtocol.cs",
            "src/OS/Boot/EfiEntry.EfiBootServices.cs",
            "src/OS/Boot/EfiEntry.EfiSystemTable.cs",
            "src/OS/Boot/EfiEntry.EfiMemoryMapSnapshot.cs",
            "src/OS/Boot/EfiEntry.OwnedMemoryMapBuffer.cs",
            "src/OS/Boot/EfiEntry.OwnedMemoryRegionBuffer.cs",
            "src/OS/Boot/EfiEntry.BootPageAllocatorNodeBuffer.cs",
            "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessStatus.cs",
            "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessResult.cs",
            "src/OS/Boot/EfiEntry.EfiGuid.cs",
            "src/OS/Boot/EfiEntry.EfiSerialIoProtocol.cs",
            "src/OS/Boot/EfiEntry.EfiConsoleCharacter.cs",
            "src/OS/Boot/EfiEntry.M04ManagedHeapAdapter.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.Storage.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.State.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Buffer.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Protocol.cs",
            "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.State.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.EfiLoadedImageProtocol.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneMemoryMapBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAllocationRecordBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.AcpiBootstrapWorkspace.cs",
            "src/OS/Boot/EfiEntry.AcpiBootstrapOwnedBuffer.cs",
            "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.cs",
            "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.Authority.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.CooperativeControl.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.TimerControl.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxLoadedImage.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFileSystem.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFile.cs",
            "src/OS/Boot/EfiEntry.M03FirstExitAdapter.AdapterHandles.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Observation.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Storage.cs",
            "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Snapshot.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Attempt.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Case.cs",
            "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Snapshot.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAdapterStatus.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAllocationRecordBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeReservationLedgerBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeFixedRequirementBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRequirementBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRangeBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageImageBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageEntryBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeBindingBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeSemanticBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeStateSnapshotBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePlannerStackBuffer.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePreparationCallContext.cs",
            "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M02OwnedX64GdtEncodingStackWorkspace.cs",
            "src/OS/Boot/EfiEntry.M02PageFaultAdapter.cs",
            "src/OS/Boot/EfiEntry.M02DoubleFaultIstAdapter.cs",
            "src/OS/Boot/EfiEntry.M02IdtrContinuedExecutionStackWorkspace.cs",
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.cs",
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.Observation.cs", "src/OS/Boot/EfiConsoleAdapter.cs", "tests/OS.HostTests/M03TypedExitBootServicesCallEfiIntegrationTests.cs",
            "tests/OS.HostTests/M03TypedExitBootServicesCallEfiIntegrationTests.Result.cs",
            "tests/OS.HostTests/M03TypedExitBootServicesCallEfiIntegrationTests.TypedEbsCaptureHost.cs"];
        string attribute = "        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root, "src/OS/Runtime/RuntimeExportAttribute.cs")).Replace("\r\n", "\n", StringComparison.Ordinal).Contains(attribute, StringComparison.Ordinal), "unchanged host attribute projection");
        File.WriteAllText(Path.Combine(directory, "Attribute.cs"), "namespace System.Runtime {\n" + attribute + "\n}");
        string items = string.Join('\n', sources.Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string project = Path.Combine(directory, "Capture.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>TypedEbsCaptureHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>TYPED_EBS_CAPTURE_HOST;M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE</DefineConstants></PropertyGroup><ItemGroup>" + items + "<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        string[] inputs = [..sources.Select(x => Path.Combine(root, x)), Path.Combine(directory, "Attribute.cs"), project];
        var identities = inputs.Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "inputs.json"), JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory, "build"), "dotnet", "build", project, "-c", "Release", "--nologo", "-m:1", "--disable-build-servers", "-p:UseSharedCompilation=false"));
        Result run = Process(Path.Combine(directory, "run"), "dotnet", Path.Combine(directory, "bin/Release/net11.0/Capture.dll"));
        Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.typedebs.host.tests", StringComparison.Ordinal) && run.Output.Split('\n').Count(x => x.StartsWith("TEST_START m03.typedebs.host ", StringComparison.Ordinal)) == 4, "complete four-group actual core child");
        Result orphan = Process(Path.Combine(directory, "orphan"), "dotnet", Path.Combine(directory, "bin/Release/net11.0/Capture.dll"), "orphan");
        Passed(orphan);
        Need(orphan.Output.Contains("TEST_PASS m03.typedebs.orphan.tests", StringComparison.Ordinal), "separate actual null-active callback entry control");
        foreach (var input in identities)
            Need(Hash(input.Path) == input.Sha256, "unchanged child input " + input.Path);
    }

    [HostTest("m03.004.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string directory = Scratch("transcript");
        byte[] good = FirmwareTranscriptControls.Bytes(ProtocolLines);
        var control = new FirmwareTranscriptControls(directory, path => OS.Tooling.QemuTranscripts.AssertM03TypedExitBootServicesTranscript(path));
        control.Check(good, true);
        for (int i = 0; i < ProtocolLines.Length; i++)
        {
            foreach (string replacement in new[]
            {
                "",
                ProtocolLines[i] + " ",
                " " + ProtocolLines[i],
                ProtocolLines[i] + " extra=1",
                ProtocolLines[i] + "\0"
            }

            )
            {
                string[] copy = [..ProtocolLines];
                copy[i] = replacement;
                control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            }

            string[] lower = [..ProtocolLines];
            lower[i] = lower[i].ToLowerInvariant();
            if (lower[i] != ProtocolLines[i])
                control.Check(FirmwareTranscriptControls.Bytes(lower), false);
            control.Check(FirmwareTranscriptControls.Bytes(ProtocolLines.Take(i + 1).Concat(ProtocolLines.Skip(i))), false);
        }

        foreach (var pair in new (string Old, string New)[]
        {
            ("storageBytes=600", "storageBytes=599"),
            ("stateBytes=40", "stateBytes=48"),
            ("handle=1311768467463790320", "handle=0"),
            ("callback=2097152", "callback=0"),
            ("storage=3145728", "storage=18446744073709551615"),
            ("state=3145768", "state=3145728"),
            ("key=9223372036854775808", "key=0"),
            ("key=18446744073709551615", "key=18446744073709551616"),
            ("status=9223372036854775810", "status=2147483650"),
            ("status=13835058055282164003", "status=291"),
            ("calls=1", "calls=2"),
            ("phase=2", "phase=3"),
            ("reuseAttempt=9", "reuseAttempt=2"),
            ("reusePrepare=9", "reusePrepare=1"),
            ("unchanged=1", "unchanged=0"),
            ("reentryAttempt=8", "reentryAttempt=2"),
            ("reentryPrepare=8", "reentryPrepare=1"),
            ("reentryUnchanged=1", "reentryUnchanged=0"),
            ("realExitBootServices=false", "realExitBootServices=true"),
            ("regions=4", "regions=3"),
            ("freeBytes=81920", "freeBytes=81921"),
            ("getMemoryMap=2097408", "getMemoryMap=0"),
            ("buffer=4194304", "buffer=4194305"),
            ("capacity=131072", "capacity=131071"),
            ("returned=2 exit=3", "returned=3 exit=2"),
            ("captureStatus=0", "captureStatus=1"),
            ("mapBytes=48", "mapBytes=49"),
            ("descriptorBytes=48", "descriptorBytes=32"),
            ("descriptorVersion=1", "descriptorVersion=0"),
            ("exitStatus=9223372036854775810", "exitStatus=2147483650"),
            ("status=4 windows=2", "status=1 windows=2"),
            ("reuse=6 preserved=1", "reuse=6 preserved=0")
        }

        )
        {
            string text = Encoding.ASCII.GetString(good), changed = text.Replace(pair.Old, pair.New);
            Need(text != changed, "mutation changed input");
            control.Check(Encoding.ASCII.GetBytes(changed), false);
        }

        string[] swap = [..ProtocolLines];
        (swap[16], swap[17]) = (swap[17], swap[16]);
        control.Check(FirmwareTranscriptControls.Bytes(swap), false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n", "\n")), false);
        control.Check(good[..^1], false);
        control.Check([..good, 0], false);
        control.Check([..good, 255], false);
        control.Check(good, false, [255]);
        control.Check(good, false, Encoding.ASCII.GetBytes("arbitrary"));
        control.Check(good, false, post: [..FirmwareTranscriptControls.Suffix, 65]);
        control.Check(good, true, post: FirmwareTranscriptControls.ShortSuffix);
        control.Check(good, false, post: [..FirmwareTranscriptControls.ShortSuffix, 65]);
        control.Check(good, false, post: []);
        control.Check(good, true);
        string[] ordered = ["TEST_START efi.main", "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT", "BOOT_PAGE_ALLOCATOR_READY", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "TEST_START m03.004", "M03_TYPED_EBS_IMAGE", "M03_TYPED_EBS_ADMISSION", "M03_TYPED_EBS_CASE id=0", "M03_TYPED_EBS_CASE id=1", "M03_TYPED_EBS_CASE id=2", "M03_TYPED_EBS_CASE id=3", "CLROS_M03_004_READY", "TEST_PASS m03.004", "TEST_START m03.005", "M03_FINAL_MAP_IMAGE", "M03_FINAL_MAP_CALL case=0 index=0", "M03_FINAL_MAP_RESULT case=0", "CLROS_M03_005_READY", "TEST_PASS m03.005", "TEST_START m03.006", "M03_FINAL_MAP_CALL case=1 index=0", "M03_FINAL_MAP_CALL case=1 index=1", "M03_FINAL_MAP_RESULT case=1", "M03_FINAL_MAP_CALL case=2 index=0", "M03_FINAL_MAP_CALL case=2 index=1", "M03_FINAL_MAP_RESULT case=2", "CLROS_M03_006_READY", "TEST_PASS m03.006", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
        var runner = new StandardFirmwareRunnerControls(directory, ProtocolLines, ordered, ["OS_SMOKE_FAIL", "TEST_FAIL", "UEFI_EBS_CALLED", "UEFI_BOOT_SERVICES_EXITED"], Flag, false);
        runner.CompleteMatrix();
        Console.WriteLine("TRANSCRIPT_CONTROLS m03.typedebs cases=" + (control.Cases + runner.Cases) + " stderrWhitespaceRejected=true");
    }

    private static readonly string[] ProtocolLines = ["TEST_START efi.main", "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 4", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "TEST_START m03.004", "M03_TYPED_EBS_IMAGE handle=1311768467463790320 callback=2097152 storage=3145728 storageBytes=600 stateBytes=40", "M03_TYPED_EBS_ADMISSION default=6 missingFunction=3 missingHandle=4 simulated=5 exited=5 readyDenied=5 readyPrepare=7 unchanged=1 calls=0", "M03_TYPED_EBS_CASE id=0 state=3145728 handle=1311768467463790320 key=0 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0", "M03_TYPED_EBS_CASE id=1 state=3145768 handle=1311768467463790320 key=9223372036854775808 status=9223372036854775810 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0", "M03_TYPED_EBS_CASE id=2 state=3145808 handle=1311768467463790320 key=18446744073709551615 status=13835058055282164003 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0", "M03_TYPED_EBS_CASE id=3 state=3145848 handle=1311768467463790320 key=9223372036854775808 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=8 reentryPrepare=8 reentryUnchanged=1", "CLROS_M03_004_READY controlled=true realExitBootServices=false states=4 calls=4 reuse=4 reentry=1 mode=0", "TEST_PASS m03.004", "TEST_START m03.005", "M03_FINAL_MAP_IMAGE handle=1311768467463790320 getMemoryMap=2097408 buffer=4194304 capacity=131072", "M03_FINAL_MAP_CALL case=0 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0", "M03_FINAL_MAP_RESULT case=0 status=1 windows=1 captures=1 exits=1 reuse=3 preserved=1", "CLROS_M03_005_READY controlled=true realExitBootServices=false captures=1 exits=1 reuseRejected=true", "TEST_PASS m03.005", "TEST_START m03.006", "M03_FINAL_MAP_CALL case=1 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810", "M03_FINAL_MAP_CALL case=1 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0", "M03_FINAL_MAP_RESULT case=1 status=1 windows=2 captures=2 exits=2 reuse=6 preserved=1", "M03_FINAL_MAP_CALL case=2 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810", "M03_FINAL_MAP_CALL case=2 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810", "M03_FINAL_MAP_RESULT case=2 status=4 windows=2 captures=2 exits=2 reuse=6 preserved=1", "CLROS_M03_006_READY controlled=true realExitBootServices=false cases=2 maxAttempts=2 captures=4 exits=4 exhausted=1", "TEST_PASS m03.006", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
    [HostTest("m03.004.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root = Root(), directory = Scratch("profile"), script = Path.Combine(root, "tools/OS.Tooling/QemuProfiles.cs"), before = Hash(script);
        Result Dry(params string[] args) => Tool(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", ..args]);
        string[] positive = ["-BuildEfi", "-" + Flag];
        Result result = Dry(positive);
        Passed(result);
        Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal), "actual dry-run terminal");
        Result preflight = Tool("efi", "build", "-PreflightOnly", "-" + Flag, "-LogPath", Path.Combine(directory, "enabled-preflight.log"));
        Passed(preflight);
        Need(preflight.Output.Split('\n').Count(x => x.TrimEnd('\r') == "EFI_PREFLIGHT_OK") == 1, "actual enabled producer preflight succeeds");
        string[] Vector(string prefix) => result.Output.Split('\n').Single(x => x.StartsWith(prefix + " ", StringComparison.Ordinal))[(prefix.Length + 1)..].TrimEnd('\r').Split(';').Select(x => x.Trim()).ToArray();
        var required = Vector("REQUIRED_MARKERS");
        var ordered = Vector("ORDERED_MARKERS");
        var unique = Vector("UNIQUE_MARKERS");
        var forbidden = Vector("FORBIDDEN_MARKERS");
        foreach (var vector in new[]
        {
            required,
            ordered,
            unique
        }

        )
            Need(vector.Length == ProtocolLines.Length && vector.Distinct(StringComparer.Ordinal).Count() == ProtocolLines.Length, "complete canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal), StringComparer.Ordinal) && ordered.SequenceEqual(unique, StringComparer.Ordinal), "actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines)
        {
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", lines) + "\r\n")).Succeeded, "actual streamer Feed");
            return state.Complete();
        }

        var accepted = Stream(ProtocolLines);
        Need(accepted.Passed && accepted.LinesProcessed == ProtocolLines.Length && accepted.OrderedSeenCount == ProtocolLines.Length && accepted.UniqueSeenCount == ProtocolLines.Length, "genuine full-token streamer success");
        foreach (string forbiddenTransition in new[]
        {
            "UEFI_EBS_CALLED",
            "UEFI_BOOT_SERVICES_EXITED"
        }

        )
        {
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            var feed = state.Feed(Encoding.ASCII.GetBytes(forbiddenTransition + "\r\n"));
            Need(!feed.Succeeded || !state.Complete().Passed, "real transition marker is forbidden");
        }

        foreach (int index in Enumerable.Range(13, 24))
        {
            var missing = Stream(ProtocolLines.Where((_, i) => i != index).ToArray());
            Need(!missing.Passed && missing.MissingRequiredMarkers!.Count > 0, "missing controlled row");
            var duplicate = Stream(ProtocolLines.Take(index + 1).Concat(ProtocolLines.Skip(index)).ToArray());
            Need(!duplicate.Passed && duplicate.DuplicateUniqueOccurrenceCount == 1, "duplicate controlled row");
            string[] swapped = [..ProtocolLines];
            (swapped[index], swapped[index + 1]) = (swapped[index + 1], swapped[index]);
            Need(Stream(swapped).OutOfOrderOccurrenceCount > 0, "reordered controlled row");
        }

        void Reject(string[] args, string reason)
        {
            Result failure = Dry(args);
            Need(failure.Exit != 0 && !failure.Timeout && (failure.Output + failure.Error).Contains(reason, StringComparison.Ordinal), "profile rejection " + reason);
        }

        string project = File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj"));
        string[] flags = Regex.Matches(project, @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "complete profile flags");
        foreach (string other in flags.Where(x => x != Flag))
            Reject([..positive, "-" + other], EfiProfileTestControls.MissingParentReason([Flag, other], ordinarySmoke: true) ?? (other == "EnableM03FirstRealExitBootServicesProbe" ? "m03_first_exit_requires property=EnableM01LoadedImageStackProbe" : other == "EnableM04GcStaticRootMaterializationProbe" ? "m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe" : "m03_typed_ebs_profile_conflict property=" + other));
        Reject(["-" + Flag], "m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive, "-SkipImageBuild"], "m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive, "-M01RawObservationLog", ""], "m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive, "-RequireNativeAppMarkers"], "m03_typed_ebs_requires_build_and_original_capture");
        foreach (string option in new[]
        {
            "RequireMarker",
            "RequireOrderedMarker",
            "RequireUniqueMarker",
            "ForbidMarker",
            "MarkerPreset"
        }

        )
            Reject([..positive, "-" + option, "CLROS_M03_004_READY"], "m03_typed_ebs_forbids_override");
        Reject(["-RequireMarker", "CLROS_M03_004_READY"], "m03_typed_ebs_requires_explicit_profile");
        foreach (bool build in new[]
        {
            true,
            false
        }

        )
        {
            var admitted = HostToolProcess.ValidateEfiProfile(build, "-" + Flag, "-EnableM01LoadedImageStackProbe");
            Result failure = new(admitted.ExitCode, false, admitted.Output, admitted.Error);
            Need(failure.Exit != 0 && !failure.Timeout && (failure.Output + failure.Error).Contains("m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal), "closed producer/verifier build=" + build);
        }

        Passed(Dry());
        Need(Hash(script) == before, "unchanged profile input");
    }

    [HostTest("m03.004.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root = Root();
        foreach (bool enabled in new[]
        {
            false,
            true
        }

        )
        {
            string directory = Scratch(enabled ? "enabled" : "default"), artifact = Path.Combine(root, "artifacts/efi/issue546-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result result = Tool(["efi", "build", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), ..(enabled ? new[] { "-EnableM03TypedExitBootServicesCallProbe" } : Array.Empty<string>())]);
            Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), "actual producer/verifier");
            foreach (string suffix in new[]
            {
                "",
                ".link.json",
                ".link.map",
                ".link.rsp",
                ".serializer.obj"
            }

            )
                File.Copy(artifact + suffix, Path.Combine(directory, "image.EFI" + suffix));
            foreach (var pair in new[]
            {
                (OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"), "OS.dll"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.obj", "OS.obj"),
                ("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml", "OS.map.xml")
            }

            )
                File.Copy(Path.Combine(root, pair.Item1), Path.Combine(directory, pair.Item2));
            using var json = JsonDocument.Parse(File.ReadAllText(artifact + ".link.json"));
            var evaluation = json.RootElement.GetProperty("postRestoreEvaluation");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create(null, new Dictionary<string, bool> { [Flag] = enabled }));
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
                Need(string.Equals(Hash(Path.Combine(root, input.GetProperty("path").GetString()!)), input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual raw source identity");
            foreach (var property in evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)))
                Need(property.Value.GetString() == (enabled && property.Name == Flag ? "true" : "false"), "actual profile " + property.Name);
            using var stream = File.OpenRead(Path.Combine(directory, "OS.dll"));
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Need(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
            var types = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t => metadata.GetString(t.Name) == "TypedExitBootServicesCallProbe").ToArray();
            Need(types.Length == (enabled ? 1 : 0), "conditional controlled type absent/default and present/enabled");
            if (enabled)
                foreach (string name in new[]
                {
                    "Execute",
                    "Controlled",
                    "Call",
                    "Same",
                    "Save",
                    "Run"
                }

                )
                {
                    var methods = types[0].GetMethods().Where(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == name).ToArray();
                    Need(methods.Length == 1 && metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress != 0, "actual controlled method " + name);
                }

            string sourceDirectory = Path.Combine(directory, "source-inputs");
            Directory.CreateDirectory(sourceDirectory);
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
            {
                string relative = input.GetProperty("path").GetString()!, destination = Path.Combine(sourceDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(root, relative), destination);
            }

            File.WriteAllText(Path.Combine(directory, "artifact-identities.json"), JsonSerializer.Serialize(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(path) }).ToArray()));
        }
    }
}
#endif
