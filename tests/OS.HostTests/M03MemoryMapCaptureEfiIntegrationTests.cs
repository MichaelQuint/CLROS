using System.Diagnostics;
using System.Reflection;
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

#if MEMORY_MAP_CAPTURE_HOST
using OS.Boot;





















































































































































#else
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M03MemoryMapCaptureEfiIntegrationTests
{
    private const string Flag = "EnableM03MemoryMapCaptureProbe";
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static string Root() => HostToolProcess.FindRepositoryRoot();
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Scratch(string role)
    {
        string p = Path.Combine(Root(), "artifacts", "issue532-tests", role + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static string[] Flags() => XDocument.Load(Path.Combine(Root(), "src/OS/OS.csproj")).Descendants("Error").Select(x => x.Attribute("Condition")!.Value).Where(x => x.StartsWith("'$(" + Flag + ")' == 'true' and '", StringComparison.Ordinal)).Select(x => Regex.Matches(x, @"\$\(([^)]+)\)")[1].Groups[1].Value).ToArray();
    private static Result Process(string directory, string program, params string[] args)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo(program)
        {
            WorkingDirectory = Root(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        DateTime begin = DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory, "command.json"), JsonSerializer.Serialize(new { Program = program, Arguments = args, WorkingDirectory = Root(), StartedUtc = begin }));
        using var p = System.Diagnostics.Process.Start(start)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        bool timeout = !p.WaitForExit(900000);
        if (timeout)
        {
            p.Kill(true);
            p.WaitForExit();
        }

        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), stdout);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), stderr);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { NativeExit = p.ExitCode, TimedOut = timeout, StartedUtc = begin, EndedUtc = DateTime.UtcNow }));
        return new(p.ExitCode, stdout, stderr, timeout);
    }

    private static Result Profile(bool build, string[] args)
    {
        var result = HostToolProcess.ValidateEfiProfile(build, args);
        return new(result.ExitCode, result.Output, result.Error, false);
    }

    private static Result Tool(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, result.Output, result.Error, false);
    }

    private static void Passed(Result r) => Need(!r.TimedOut && r.Code == 0 && r.Error.Length == 0, r.Output + r.Error);
    [HostTest("m03.001.efi source profile closure and original capture", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = Root();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        Need(ContractAudit.Inspect(root).Count == 0, "complete independently reviewed project and guard inventory");
        var admission = EfiProjectAdmission.InspectStatic(root);
        Need(admission.Succeeded, admission.Reason + ":" + admission.Detail);
        var source = CaptureSources();
        var authority = CaptureAuthority(source);
        Need(authority is { IsStatic: true, IsConst: true, ConstantValue: false,
            DeclaredAccessibility: Accessibility.Public }
            && authority.Type.SpecialType == SpecialType.System_Boolean, "actual default-off capture authority");
        var flag = project.Descendants(Flag).Single();
        Need(flag.Value == "false" && flag.Attribute("Condition")!.Value == "'$(" + Flag + ")' == ''", "explicit false default");
        Need(project.Descendants("PropertyGroup").Single(x => x.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true'").Element("DefineConstants")!.Value == "$(DefineConstants);M03_MEMORY_MAP_CAPTURE_PROBE", "exact compiler symbol");
        string[] flags = Flags();
        Need(flags.Length != 0 && flags.Distinct(StringComparer.Ordinal).Count() == flags.Length
            && flags.All(EfiContracts.DiagnosticProbes.Contains), "unique known conflicts from reviewed project");
        Need(flags.Contains("EnableM04TypeManagerModuleRegistrationProbe", StringComparer.Ordinal), "explicit TypeManager conflict member");
        Need(flags.Contains("EnableM03TypedExitBootServicesCallProbe", StringComparer.Ordinal), "explicit typed EBS conflict member");
        var conditions = project.Descendants("Error").Select(x => x.Attribute("Condition")!.Value).ToHashSet(StringComparer.Ordinal);
        Need(conditions.Contains("'$(EnableM03MemoryMapCaptureProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'") && conditions.Contains("'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'"), "exact TypeManager reciprocal conflict rows");
        foreach (string f in flags)
            Need(conditions.Contains("'$(" + f + ")' == 'true' and '$(" + Flag + ")' == 'true'"), "reciprocal " + f);
        var entryOwner = source.Type("OS.Boot.EfiEntry");
        var entryMethod = source.Method("OS.Boot.EfiEntry", "TryCaptureUefiMemoryMap", method =>
            method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly
            && method.DeclaredAccessibility == Accessibility.Private
            && method.ReturnType.SpecialType == SpecialType.System_Boolean);
        _ = source.MethodInvocations(entryMethod);
        var captures = source.Type("OS.Boot.UefiMemoryMapCapture").GetMembers("Capture").OfType<IMethodSymbol>()
            .SelectMany(target => HostSourceOperations.CallsTo(source, target))
            .Where(call => call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } owner
                && SymbolEqualityComparer.Default.Equals(entryOwner,
                    source.Compilation.GetSemanticModel(owner.SyntaxTree).GetDeclaredSymbol(owner))).ToArray();
        Need(captures.Length == 1 && captures[0].SyntaxTree == entryMethod.SyntaxTree
            && entryMethod.Span.Contains(captures[0].Span), "sole original capture belongs to the entry routine");
        string rawEntry = HostSourceText.Documents(source, "OS.Boot.EfiEntry");
        Need(Regex.Matches(rawEntry, @"captureResult = UefiMemoryMapCapture\.Capture\(").Count == 1,
            "original raw capture occurrence remains unique across entry partials");
        string entry = HostSourceText.Active(entryMethod);
        Need(HostSourceText.Count(entry, "captureResult = UefiMemoryMapCapture.Capture(") == 1
            && HostSourceText.Contains(entry, "if (!M03MemoryMapCaptureObserver.ReadBack(captureResult))"),
            "sole actual original Capture and immediate readback");
        const string observerOwner = "OS.Boot.EfiEntry+M03MemoryMapCaptureObserver";
        string observer = HostSourceText.Documents(source, observerOwner);
        string activeObserver = HostSourceText.Declarations(source, observerOwner);
        foreach (string forbidden in new[]
        {
            "FreePool(",
            "new byte[",
            "new object",
            "throw ",
            "RuntimeExport",
            "Serialize("
        }

        )
            Need(!observer.Contains(forbidden, StringComparison.Ordinal), "bounded observer " + forbidden);
        const string tableAssignment = @"services->(?:GetMemoryMap|AllocatePool)\s*=(?!=)";
        Need(!Regex.IsMatch(observer, tableAssignment, RegexOptions.CultureInvariant), "no firmware-table assignment");
        foreach (string field in new[]
        {
            "GetMemoryMap",
            "AllocatePool"
        }

        )
            Need(!Regex.IsMatch("services->" + field + " == null", tableAssignment, RegexOptions.CultureInvariant) && Regex.IsMatch("services->" + field + " = replacement", tableAssignment, RegexOptions.CultureInvariant), "assignment versus equality control " + field);
        // Only these assignments record addresses; no firmware-table mutation exists.
        Need(HostSourceText.Contains(activeObserver, "o->GetMemoryMap = (ulong)services->GetMemoryMap")
            && HostSourceText.Contains(activeObserver, "o->AllocatePool = (ulong)services->AllocatePool"), "retained originals");
    }

    [HostTest("m03.001.efi actual original capture controlled callbacks and guarded copy", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root = Root(), directory = Scratch("actual-capture");
        string includes = string.Join('\n', new[] { "src/OS/Boot/EfiEntry.cs",
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
            "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.Observation.cs", "src/OS/Boot/EfiConsoleAdapter.cs", "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs",
            "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.Result.cs",
            "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.MemoryMapCaptureHost.cs" }.Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string shim = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/RuntimeExportAttribute.cs"));
        string attribute = "        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(shim.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(attribute, StringComparison.Ordinal), "exact unchanged ordinary-host attribute projection");
        File.WriteAllText(Path.Combine(directory, "Attribute.cs"), "namespace System.Runtime {\n" + attribute + "\n}");
        File.WriteAllText(Path.Combine(directory, "Capture.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><StartupObject>MemoryMapCaptureHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>MEMORY_MAP_CAPTURE_HOST;M03_MEMORY_MAP_CAPTURE_PROBE</DefineConstants>
            </PropertyGroup><ItemGroup>
            """ + includes + "<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>");
        string[] directInputs = [Path.Combine(root, "src/OS/Boot/EfiEntry.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageTwoAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiTableHeader.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiSimpleTextOutputProtocol.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiBootServices.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiSystemTable.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiMemoryMapSnapshot.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.OwnedMemoryMapBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.OwnedMemoryRegionBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.BootPageAllocatorNodeBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessStatus.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiBootPageAllocatorReadinessResult.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiGuid.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiSerialIoProtocol.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.EfiConsoleCharacter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M04ManagedHeapAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.Storage.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyReservoir.State.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Buffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.Protocol.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FirmwareEntropyCapture.State.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAdapterStatus.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.EfiLoadedImageProtocol.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneMemoryMapBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneAllocationRecordBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageOneAdapter.M01StageOneReservationLedgerBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoAdapterStatus.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageTwoAdapter.M01StageTwoReservationLedgerBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.AcpiBootstrapWorkspace.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.AcpiBootstrapOwnedBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M02UnexpectedVectorPanicAdapter.Authority.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.CooperativeControl.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.TimerControl.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxLoadedImage.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFileSystem.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.LinuxFile.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03FirstExitAdapter.AdapterHandles.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Observation.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Storage.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.TypedExitBootServicesCallProbe.Snapshot.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FinalMapRetryProbe.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Attempt.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Case.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.FinalMapRetryProbe.Snapshot.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAdapterStatus.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeAllocationRecordBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeReservationLedgerBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeFixedRequirementBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRequirementBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeRangeBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageImageBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePageEntryBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeBindingBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeSemanticBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreeStateSnapshotBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePlannerStackBuffer.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M01StageThreePreparationCallContext.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M01StageThreeAdapter.M02OwnedX64GdtEncodingStackWorkspace.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M02PageFaultAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M02DoubleFaultIstAdapter.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M02IdtrContinuedExecutionStackWorkspace.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.cs"),
                Path.Combine(root, "src/OS/Boot/EfiEntry.M03MemoryMapCaptureObserver.Observation.cs"), Path.Combine(root, "src/OS/Boot/EfiConsoleAdapter.cs"), Path.Combine(root, "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs"),
                Path.Combine(root, "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.Result.cs"),
                Path.Combine(root, "tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.MemoryMapCaptureHost.cs"), Path.Combine(directory, "Attribute.cs"), Path.Combine(directory, "Capture.csproj"), Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")];
        File.WriteAllText(Path.Combine(directory, "direct-inputs.json"), JsonSerializer.Serialize(directInputs.Select(path => new { Path = path, Bytes = new FileInfo(path).Length, Sha256 = Hash(File.ReadAllBytes(path)) }).ToArray()));
        Result r = Process(directory, "dotnet", "run", "--project", Path.Combine(directory, "Capture.csproj"), "-c", "Release", "--nologo");
        Passed(r);
        Need(r.Output.Contains("TEST_PASS m03.capture.host.tests", StringComparison.Ordinal) && r.Output.Contains("distinctCaptureSizeSlots=true", StringComparison.Ordinal), "complete actual Capture child");
    }

    [HostTest("m03.001.efi strict whole transcript and ordinary lifecycle", HostLane.Fast, 3)]
    private static void TestTranscript() => TranscriptControls();
    [HostTest("m03.001.efi ordinary source admission and unchanged generic firewall", HostLane.Fast, 2)]
    private static void TestFirewall()
    {
        var catalog = CaptureSources();
        var document = catalog.Document(CaptureAuthority(catalog).DeclaringSyntaxReferences.Single().GetSyntax());
        string name = Path.GetRelativePath(Path.Combine(Root(), "src", "OS"), document.Path).Replace('/', '\\');
        string source = document.Text;
        Need(OS.Tooling.EfiSourceAdmission.Inspect(source, name).Succeeded, "ordinary authority admission");
        foreach (string bad in new[]
        {
            "using System.Text;",
            "class Bad { object M()=>new object(); }",
            "class Bad { void M(){throw null;} }",
            "class Bad { [System.Runtime.InteropServices.UnmanagedCallersOnly] static void M(){} }"
        }

        )
            Need(!OS.Tooling.EfiSourceAdmission.Inspect(source + "\n" + bad, name).Succeeded, "generic source bypass");
    }

    [HostTest("m03.001.efi all reciprocal profile collisions and ordinary dry runs", HostLane.Native, 4)]
    private static void TestProfiles()
    {
        string directory = Scratch("profiles");
        int index = 0;
        foreach (string other in Flags())
            foreach (string file in new[]
            {
                "build",
                "verify",
                "qemu"
            }

            )
            {
                string[] extra = file == "build" ? ["-PreflightOnly", "-LogPath", Path.Combine(directory, "preflight-" + index + ".log")] : file == "verify" ? ["-EfiArtifact", "missing.EFI"] : ["-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi"];
                // The new reference profile validates first; admit its prerequisites to test the actual reciprocal conflict.
                string[] companion = other == "EnableM04CheckedReferenceBarrierProbe" ? ["-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "-EnableM04ArithmeticConformanceProbe", "-EnableM04LayoutContractProbe", "-EnableM04ManagedHeapPageRangeProbe", "-EnableM04RhpNewFastProbe"] : [];
                string expected = other == "EnableM04GcStaticRootMaterializationProbe" ? "m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe" : other == "EnableM03TypedExitBootServicesCallProbe" ? "m03_typed_ebs_profile_conflict property=EnableM03MemoryMapCaptureProbe" : other == "EnableM04TypeManagerModuleRegistrationProbe" ? "m04_type_manager_requires_original_loaded_image" : other == "EnableM03FirmwareEntropySeedProbe" ? "m03_firmware_entropy_profile_conflict property=EnableM03MemoryMapCaptureProbe" : other == "EnableM04CheckedReferenceBarrierProbe" ? "m04_checked_reference_profile_conflict property=EnableM03MemoryMapCaptureProbe" : "m03_memory_map_capture_profile_conflict";
                Result r = file == "qemu" ? Tool(["qemu", "smoke", ..extra, "-" + Flag, "-" + other, ..companion]) : Profile(file == "build", [..extra, "-" + Flag, "-" + other, ..companion]);
                index++;
                Need(r.Code != 0 && !r.TimedOut && (r.Output + r.Error).Contains(expected, StringComparison.Ordinal), "exact early conflict " + other + " " + r.Output + r.Error);
            }

        foreach (bool enabled in new[]
        {
            false,
            true
        }

        )
        {
            Result r = Tool(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", ..(enabled ? new[] { "-" + Flag } : Array.Empty<string>())]);
            Passed(r);
            string ordered = r.Output.Replace("\r", "").Split('\n').Single(x => x.StartsWith("ORDERED_MARKERS ", StringComparison.Ordinal));
            Need(enabled ? ordered.Split(';').Length == OS.Tooling.QemuContracts.M03MemoryMapCaptureRequiredMarkers.Count : !ordered.Contains("m03.001.capture", StringComparison.Ordinal), "default/enabled exact ordered marker selection");
            if (enabled)
            {
                string[] outputLines = r.Output.Replace("\r", "").Split('\n');
                string[][] vectors = new[]
                {
                    "REQUIRED_MARKERS ",
                    "ORDERED_MARKERS ",
                    "UNIQUE_MARKERS "
                }.Select(prefix => outputLines.Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..].Split(';')).ToArray();
                Need(vectors.All(vector => vector.Length == OS.Tooling.QemuContracts.M03MemoryMapCaptureRequiredMarkers.Count && vector.Distinct(StringComparer.Ordinal).Count() == vector.Length && vector.All(marker => marker == marker.Trim())) && vectors[0].ToHashSet(StringComparer.Ordinal).SetEquals(vectors[1]) && vectors[1].SequenceEqual(vectors[2], StringComparer.Ordinal), "31 canonical unique matching required set and exact ordered unique sequence");
            }
        }
    }

    [HostTest("m03.001.efi default and enabled native artifacts and managed ABI", HostLane.Native, 5)]
    private static void TestArtifacts()
    {
        foreach (bool enabled in new[]
        {
            false,
            true
        }

        )
        {
            string directory = Scratch(enabled ? "enabled" : "default"), artifact = Path.Combine(Root(), "artifacts/efi/issue532-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result r = Tool(["efi", "build", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), ..(enabled ? new[] { "-" + Flag } : Array.Empty<string>())]);
            Passed(r);
            Need(r.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), "complete artifact producer/verifier");
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
                File.Copy(Path.Combine(Root(), pair.Item1), Path.Combine(directory, pair.Item2));
            using var json = JsonDocument.Parse(File.ReadAllText(artifact + ".link.json"));
            var e = json.RootElement;
            var inputs = e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();
            Need(inputs.Select(i => i.GetProperty("path").GetString()).SequenceEqual(OS.Tooling.EfiEvidence.SourceClosurePaths(Root()), StringComparer.Ordinal), "exact raw source inventory");
            foreach (var input in inputs)
                Need(Hash(File.ReadAllBytes(Path.Combine(Root(), input.GetProperty("path").GetString()!))) == input.GetProperty("sha256").GetString(), "actual raw hash");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create(null, new Dictionary<string, bool> { [Flag] = enabled }));
            var evaluation = e.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            var properties = evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)).ToArray();
            Need(properties.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "all evaluated profile flags");
            foreach (var p in properties)
                Need(p.Value.GetString() == (enabled && p.Name == Flag ? "true" : "false"), "exact requested/evaluated " + p.Name);
            InspectMetadata(Path.Combine(directory, "OS.dll"), enabled);
        }
    }

    private static void InspectMetadata(string path, bool enabled)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        Need(reader.AssemblyReferences.Count == 0, "actual no-stdlib zero references");
        var types = reader.TypeDefinitions.Where(h => reader.GetString(reader.GetTypeDefinition(h).Name) == "M03MemoryMapCaptureObserver").ToArray();
        Need(types.Length == (enabled ? 1 : 0), "profile conditional observer");
        if (!enabled)
            return;
        var type = reader.GetTypeDefinition(types[0]);
        foreach (string name in new[]
        {
            "Prepare",
            "ForwardGetMemoryMap",
            "ForwardAllocatePool",
            "ReadBack",
            "Handoff",
            "Report"
        }

        )
        {
            var method = reader.GetMethodDefinition(type.GetMethods().Single(h => reader.GetString(reader.GetMethodDefinition(h).Name) == name));
            Need(method.RelativeVirtualAddress != 0 && pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!.Length > 0, "real method IL " + name);
        }

        foreach (var tuple in new[]
        {
            ("ForwardGetMemoryMap", new byte[] { 0, 5, 11, 15, 11, 15, 1, 15, 11, 15, 11, 15, 9 }),
            ("ForwardAllocatePool", new byte[] { 0, 3, 11, 9, 11, 15, 15, 1 })
        }

        )
        {
            var method = reader.GetMethodDefinition(type.GetMethods().Single(h => reader.GetString(reader.GetMethodDefinition(h).Name) == tuple.Item1));
            Need(reader.GetBlobBytes(method.Signature).SequenceEqual(tuple.Item2), "exact unmanaged callback scalar signature " + tuple.Item1);
            Need(method.GetCustomAttributes().Count == 1, "sole unmanaged entry attribute " + tuple.Item1);
            var attribute = reader.GetCustomAttribute(method.GetCustomAttributes().Single());
            Need(attribute.Constructor.Kind == System.Reflection.Metadata.HandleKind.MethodDefinition, "local no-stdlib attribute constructor");
            var constructor = reader.GetMethodDefinition((System.Reflection.Metadata.MethodDefinitionHandle)attribute.Constructor);
            var declaring = reader.GetTypeDefinition(constructor.GetDeclaringType());
            Need(reader.GetString(declaring.Name) == "UnmanagedCallersOnlyAttribute" && reader.GetString(declaring.Namespace) == "System.Runtime.InteropServices", "actual unmanaged callback boundary");
        }

        File.WriteAllText(Path.ChangeExtension(path, "metadata.json"), JsonSerializer.Serialize(new { AssemblyReferences = reader.AssemblyReferences.Count, GuestExecuted = false, Methods = type.GetMethods().Select(h =>
        {
            var m = reader.GetMethodDefinition(h);
            return new
            {
                Name = reader.GetString(m.Name),
                Rva = m.RelativeVirtualAddress,
                Signature = Convert.ToHexString(reader.GetBlobBytes(m.Signature))
            };
        }).ToArray() }));
    }

    private static void TranscriptControls()
    {
        string directory = Scratch("transcript");
        string[] lines = ["TEST_START efi.main", "EFI_CONSOLE_READY", "TEST_START m03.001.capture", "M03_CAPTURE_AUTH observer=0000000000100000 observerBytes=488 guard=0000000000200000 guardSpan=131104 payload=0000000000200010 capacity=131072 services=0000000000300000 get=0000000000400000 allocate=0000000000400100", "M03_CAPTURE_QUERY calls=1 order=1 inputBytes=0 buffer=0000000000000000 status=8000000000000005 required=96 key=FEDCBA9876543210 stride=48 version=1", "M03_CAPTURE_ALLOC calls=1 order=2 type=2 bytes=480 status=0000000000000000 pool=0000000000500000 admitted=1", "M03_CAPTURE_FINAL calls=1 order=3 inputBytes=480 buffer=0000000000500000 status=0000000000000000 bytes=160 key=123456789ABCDEF0 stride=40 version=2", "M03_CAPTURE_COPY source=0000000000500000 sourceBytes=480 owned=0000000000200010 capacity=131072 bytes=160 count=4 flag=1 compared=160 mismatches=0 firstMismatch=FFFFFFFFFFFFFFFF", "M03_CAPTURE_GUARDS bytes=32 mismatches=0 tailBytes=130912 tailMismatches=0", "M03_CAPTURE_HANDOFF snapshotKey=123456789ABCDEF0 handoffKey=123456789ABCDEF0 flag=1 matched=1", "CLROS_M03_001_READY firmwareAvailable=true exitBootServices=false", "TEST_PASS m03.001.capture", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 4", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
        byte[] good = FirmwareTranscriptControls.Bytes(lines);
        var control = new FirmwareTranscriptControls(directory, OS.Tooling.QemuTranscripts.AssertM03MemoryMapCaptureTranscript);
        control.Check(good, true);
        foreach (string version in new[]
        {
            "0",
            "4294967295"
        }

        )
            control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("stride=48 version=1", "stride=48 version=" + version)), true);
        foreach (string version in new[]
        {
            "4294967296",
            "18446744073709551615"
        }

        )
            control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("stride=48 version=1", "stride=48 version=" + version)), false);
        for (int i = 0; i < lines.Length; i++)
        {
            string[] copy = [..lines];
            copy[i] += " extra=1";
            control.Check(FirmwareTranscriptControls.Bytes(copy), false);
            copy = [..lines];
            copy[i] = "";
            control.Check(FirmwareTranscriptControls.Bytes(copy), false);
        }

        foreach (var pair in new (string Old, string New)[]
        {
            ("observerBytes=488", "observerBytes=487"),
            ("guardSpan=131104", "guardSpan=131103"),
            ("capacity=131072", "capacity=131071"),
            ("pool=0000000000500000", "pool=0000000000200000"),
            ("inputBytes=480", "inputBytes=479"),
            ("compared=160", "compared=159"),
            ("tailMismatches=0", "tailMismatches=1"),
            ("firstMismatch=FFFFFFFFFFFFFFFF", "firstMismatch=0000000000000000"),
            ("snapshotKey=123456789ABCDEF0", "snapshotKey=123456789ABCDEF1"),
            ("required=96", "required=18446744073709551616"),
            ("status=8000000000000005", "status=0000000000000000"),
            ("order=3", "order=2"),
            ("type=2", "type=1"),
            ("version=2", "version=0"),
            ("stride=40", "stride=41"),
            ("count=4", "count=5"),
            ("freeBytes=81920", "freeBytes=81921")
        }

        )
            control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace(pair.Old, pair.New)), false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n", "\n")), false);
        control.Check([..good, 0], false);
        control.Check(good, false, [255]);
        control.Check(good, false, post: [..FirmwareTranscriptControls.Suffix, 65]);
        control.Check(good, false, Encoding.ASCII.GetBytes("arbitrary"));
        foreach (string exit in new[]
        {
            "0",
            "-1",
            "-2147483648",
            "2147483647"
        }

        )
            OS.Tooling.QemuTranscripts.AssertM03MemoryMapCaptureRunnerResult(["FORBIDDEN_MARKERS OS_SMOKE_FAIL|TEST_FAIL", "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit]);
        foreach (string drain in new[]
        {
            "SERIAL_MARKER_DRAIN milliseconds=750\0",
            "SERIAL_MARKER_DRAIN Milliseconds=750"
        }

        )
            FirmwareTranscriptControls.Reject(() => OS.Tooling.QemuTranscripts.AssertM03MemoryMapCaptureRunnerResult([drain, "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1"]), "drain");
        foreach (string exit in new[]
        {
            "none",
            "-0",
            "01",
            "+1",
            "2147483648",
            "-2147483649"
        }

        )
            FirmwareTranscriptControls.Reject(() => OS.Tooling.QemuTranscripts.AssertM03MemoryMapCaptureRunnerResult(["SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit]), "exit");
        foreach (string extra in new[]
        {
            "TEST_FAIL x",
            "OS_SMOKE_FAIL",
            "QEMU_PROCESS_CLEANUP_FAILED",
            "QEMU_PROCESS_RESULT disposition=spontaneous-exit exitCode=0",
            "SERIAL_MARKER_DRAIN milliseconds=750"
        }

        )
            FirmwareTranscriptControls.Reject(() => OS.Tooling.QemuTranscripts.AssertM03MemoryMapCaptureRunnerResult(["SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", extra]), "lifecycle");
        Console.WriteLine("TRANSCRIPT_CONTROLS m03.capture cases=" + control.Cases);
    }
}
#endif
