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
using OS.Tooling;
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;

internal static partial class M04NativeAotModuleRegistrationEfiIntegrationTests
{
    private const string Flag = "EnableM04TypeManagerModuleRegistrationProbe";
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static string Root() => HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag)
    {
        string path = Path.Combine(Root(), "artifacts/host-tests/m04-type-manager-" + tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Result Invoke(params string[] args)
    {
        var result = HostToolProcess.RunTool(args);
        return new(result.ExitCode, false, result.Output, result.Error);
    }

    private static void Passed(Result r) => Need(r.Exit == 0 && !r.Timeout && r.Error.Length == 0, r.Output + r.Error);
    [HostTest("m04.011.efi conditional source and original M01 image authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = Root();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" && (string?)defaults[0].Attribute("Condition") == "'$(" + Flag + ")' == ''", "one false default");
        var define = project.Descendants("DefineConstants").Single(x => x.Value.Contains("M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE", StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition") == "'$(" + Flag + ")' == 'true'" && define.Value == "$(DefineConstants);M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE", "exact symbol");
        var catalog = HostSourceCatalog.Efi(EfiOptions.Parse(
            ["--EnableM01LoadedImageStackProbe", "--" + Flag], build: true).Profile());
        var admitted = EfiProjectAdmission.InspectStatic(root);
        Need(admitted.Succeeded, admitted.Reason + ":" + admitted.Detail);
        var authority = catalog.Type("OS.Kernel.Diagnostics.Probes").GetMembers("EnableTypeManagerModuleRegistrationProbe")
            .OfType<Microsoft.CodeAnalysis.IFieldSymbol>().Single();
        // The core and compile-time authority have source-admission policies.
        // The runtime probe is compiled in this profile and checked by its bound
        // image/storage guard below; it has no restricted-kernel source pin.
        foreach (var document in catalog.TypeSources(typeof(Core).FullName!)
            .Append(catalog.Document(authority.DeclaringSyntaxReferences.Single().GetSyntax())).DistinctBy(document => document.Path))
        {
            string relative = Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
            Need(HostEfiSourceAdmission.Accepts(relative, document.Text), "reviewed type family " + relative);
        }
        Need(authority is { IsConst: true, ConstantValue: false }, "disabled compile-time authority");
        Need(HostTypeManagerSourceGuard.OriginalImage(catalog), "bound original image capture and caller");
        Need(HostTypeManagerSourceGuard.RegisteredStorage(catalog), "bound registered storage and slot-derived consumers");
        var errors = project.Descendants("Error").Select(x => (string)x.Attribute("Condition")!).ToArray();
        string[] flags = Regex.Matches(File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj")), @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "actual diagnostic flag inventory");
        Need(errors.Count(x => x == "'$(" + Flag + ")' != '' and '$(" + Flag + ")' != 'true' and '$(" + Flag + ")' != 'false'") == 1, "exact invalid-value guard");
        Need(errors.Count(x => x == "'$(" + Flag + ")' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'") == 1, "exact original M01 prerequisite");
        Need(errors.Count(x => x == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "exact unwind child original-module prerequisite");
        foreach (string other in flags.Where(x => x != Flag && x != "EnableM01LoadedImageStackProbe" && x != "EnableM04X64UnwindMetadataProbe"))
        {
            if (other == "EnableM03FirstRealExitBootServicesProbe" || OS.Tooling.EfiContracts.FirstExitDependentProbes.Contains(other))
            {
                if (other == "EnableM03FirmwareAdapterRevocationProbe")
                    Need(errors.Count(x => x == "'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact inherited revocation prerequisite");
                if (other == "EnableM03OwnedSubsystemPanicProbe")
                    Need(errors.Count(x => x == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact inherited owned panic prerequisite");
                if (other == "EnableM03OwnedSubsystemSurvivalProbe" || other == "EnableM03OwnedSubsystemPanicProbe")
                    Need(errors.Count(x => x == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact inherited owned survival prerequisite");
                if (other == "EnableM05PhysicalMappingProbe")
                    Need(errors.Count(x => x == "'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')") == 1, "exact inherited mapping prerequisite");
                if (other == "EnableM05OwnedAcpiSelectionProbe")
                    Need(errors.Count(x => x == "'$(EnableM05OwnedAcpiSelectionProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')") == 1, "exact inherited ACPI prerequisite");
                Need(errors.Count(x => x == "'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$(" + Flag + ")' == 'true'") == 1, "exact incoming first-exit conflict");
                continue;
            }

            if (other == "EnableM04SeededCollectorChurnProbe" || other == "EnableM04ClassConstructorExecutionProbe" || other == "EnableM04InterfaceDispatchProbe" || other == "EnableM04SimpleThrowCatchProbe" || other == "EnableM04SingleFinallyCatchProbe" || other == "EnableM04RethrowProbe" || other == "EnableM04NestedFinallyCatchProbe" || other == "EnableM04UnhandledExceptionProbe" || other == "EnableM04FrameDispatchProbe" || other == "EnableM04ConstructorFailureProbe" || other == "EnableM04FilteredCatchProbe" || other == "EnableM04EhCatalogProbe" || other == "EnableM04NormalFinallyProbe")
            {
                string gcChild = other;
                if (other == "EnableM04EhCatalogProbe")
                {
                    Need(errors.Count(x => x == "'$(EnableM04EhCatalogProbe)' == 'true' and '$(EnableM04FrameDispatchProbe)' != 'true'") == 1, "exact inherited catalog prerequisite");
                    gcChild = "EnableM04FrameDispatchProbe";
                }

                if (other == "EnableM04InterfaceDispatchProbe")
                {
                    Need(errors.Count(x => x == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "exact inherited dispatch prerequisite");
                    gcChild = "EnableM04ClassConstructorExecutionProbe";
                }

                Need(errors.Count(x => x == "'$(" + gcChild + ")' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1 && errors.Count(x => x == "'$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '$(" + Flag + ")' == 'true'") == 1 && errors.Count(x => x == "'$(" + Flag + ")' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'") == 1, "exact inherited GC conflicts " + other);
                continue;
            }

            Need(errors.Contains("'$(" + Flag + ")' == 'true' and '$(" + other + ")' == 'true'", StringComparer.Ordinal) && errors.Contains("'$(" + other + ")' == 'true' and '$(" + Flag + ")' == 'true'", StringComparer.Ordinal), "reciprocal conflict " + other);
        }
    }

    [HostTest("m04.011.efi actual mapped registration and slot-derived consumer", HostLane.Fast, 1)]
    private static void TestCapture() => Need(TypeManagerCaptureHost.ExecuteCases() == 0, "actual mapped adapter controls");
    [HostTest("m04.011.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript() => EfiTypeManagerTranscriptControls.Run(Scratch("transcript"), ProtocolLines);
    private static readonly string[] ProtocolLines = ["TEST_START efi.main", "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 4", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", "TEST_START m01.stage1", "M01_LOADED_IMAGE_CAPTURED", "M01_STACK_SWITCH_STUB_READY", "M01_STACK_ARENA_ALLOCATED", "M01_STACK_GUARD_RESERVED", "M01_STAGE1_MAP_RECAPTURED", "M01_STAGE1_RESERVATIONS_VALIDATED", "M01_OWNED_STACK_READY", "M01_OWNED_STACK_SWITCHED", "M01_RSP_CONTAINED", "TEST_PASS m01.stage1", "TEST_START m04.011", "CLROS_M04_011_IMAGE base=1048576 bytes=32768", "CLROS_M04_011_VALIDATE required=1024 storage=1060864 capacity=4096", "CLROS_M04_011_REJECT kind=capacity status=7 unchanged=true", "CLROS_M04_011_REGISTER slot=1056768 handle=1061136 osModule=1048576 header=1052672 gc=0 tls=1057024 slotBytes=16 index=0 padding=0 abiBytes=48 classlib=0 classlibCount=0", "CLROS_M04_011_SECTION id=202 status=1 start=1057024 length=8", "CLROS_M04_011_SECTION id=204 status=1 start=1056768 length=16", "CLROS_M04_011_SECTION id=213 status=1 start=1052944 length=0", "CLROS_M04_011_SECTION id=0 status=11 start=0 length=0", "CLROS_M04_011_REJECT kind=repeat status=9 unchanged=true", "CLROS_M04_011_REJECT kind=foreign status=10 unchanged=true", "CLROS_M04_011_READY modules=1 abiBytes=48 slotBytes=16 registered=true consumer=true tailCanaries=true", "TEST_PASS m04.011", "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
    [HostTest("m04.011.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root = Root();
        Result Dry(params string[] args) => Invoke(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", .. args]);
        string[] positive = ["-BuildEfi", "-" + Flag, "-EnableM01LoadedImageStackProbe"];
        Result result = Dry(positive);
        Passed(result);
        Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal), "actual dry-run terminal");
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
            Need(vector.Length == QemuContracts.M04TypeManagerRequiredMarkers.Count && vector.Distinct(StringComparer.Ordinal).Count() == vector.Length, "canonical unique marker inventories");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal), StringComparer.Ordinal) && ordered.SequenceEqual(unique, StringComparer.Ordinal), "actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines)
        {
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", lines) + "\r\n")).Succeeded, "actual streamer Feed");
            return state.Complete();
        }

        var accepted = Stream(ProtocolLines);
        Need(accepted.Passed && accepted.LinesProcessed == ProtocolLines.Length && accepted.OrderedSeenCount == ordered.Length && accepted.UniqueSeenCount == unique.Length, "genuine full-token streamer success");
        foreach (int index in Enumerable.Range(24, 13))
        {
            var missing = Stream(ProtocolLines.Where((_, i) => i != index).ToArray());
            Need(!missing.Passed && missing.MissingRequiredMarkers!.Count > 0, "missing registration row");
            var duplicate = Stream(ProtocolLines.Take(index + 1).Concat(ProtocolLines.Skip(index)).ToArray());
            Need(!duplicate.Passed && duplicate.DuplicateUniqueOccurrenceCount == 1, "duplicate registration row");
            string[] swapped = [.. ProtocolLines];
            (swapped[index], swapped[index + 1]) = (swapped[index + 1], swapped[index]);
            Need(Stream(swapped).OutOfOrderOccurrenceCount > 0, "reordered registration row");
        }

        void Reject(string[] args, string reason)
        {
            Result failure = Dry(args);
            Need(failure.Exit != 0 && !failure.Timeout && (failure.Output + failure.Error).Contains(reason, StringComparison.Ordinal), "profile rejection " + reason);
        }

        string project = File.ReadAllText(Path.Combine(root, "src/OS/OS.csproj"));
        string[] flags = Regex.Matches(project, @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "complete profile inventory");
        foreach (string other in flags.Where(x => x != Flag && x != "EnableM01LoadedImageStackProbe" && x != "EnableM04X64UnwindMetadataProbe"))
        {
            Result failure = Dry([.. positive, "-" + other]);
            string diagnostic = failure.Output + failure.Error;
            Need(failure.Exit != 0 && !failure.Timeout && diagnostic.Contains("TEST_FAIL qemu.smoke", StringComparison.Ordinal) && (diagnostic.Contains("_profile_conflict", StringComparison.Ordinal) || diagnostic.Contains("_requires", StringComparison.Ordinal) || diagnostic.Contains("_profile_invalid", StringComparison.Ordinal)), "mutually exclusive profile rejected " + other + ": " + diagnostic);
        }

        Reject(["-" + Flag, "-EnableM01LoadedImageStackProbe"], "m04_type_manager_requires_build_and_original_capture");
        Reject(["-BuildEfi", "-" + Flag], "m04_type_manager_requires_original_loaded_image");
        Reject([.. positive, "-SkipImageBuild"], "m04_type_manager_requires_build_and_original_capture");
        Reject([.. positive, "-M01RawObservationLog", ""], "m04_type_manager_requires_build_and_original_capture");
        Reject([.. positive, "-RequireNativeAppMarkers"], "m04_type_manager_requires_build_and_original_capture");
        foreach (string option in new[]
        {
            "RequireMarker",
            "RequireOrderedMarker",
            "RequireUniqueMarker",
            "ForbidMarker",
            "MarkerPreset"
        }

        )
            Reject([.. positive, "-" + option, "CLROS_M04_011_READY"], "m04_type_manager_forbids_override");
        Reject(["-RequireMarker", "CLROS_M04_011_READY"], "m04_type_manager_requires_explicit_profile");
        foreach (bool build in new[]
        {
            true,
            false
        }

        )
            foreach (bool missingM01 in new[]
            {
                true,
                false
            }

            )
            {
                string[] args = ["-BootPolicy", "Diagnostic", "-" + Flag, ..(missingM01 ? Array.Empty<string>() : new[]
                {
                    "-EnableM01LoadedImageStackProbe",
                    "-EnableM01LiveAllocatorProbe"
                }

                )];
                var failure = HostToolProcess.ValidateEfiProfile(build, args);
                string reason = missingM01 ? "m04_type_manager_requires_original_loaded_image" : "m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe";
                Need(failure.ExitCode != 0 && (failure.Output + failure.Error).Contains(reason, StringComparison.Ordinal), "closed producer/verifier " + build + " " + reason);
            }

        Passed(Dry());
    }

    [HostTest("m04.011.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
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
            string directory = Scratch(enabled ? "enabled" : "default"), artifact = Path.Combine(root, "artifacts/efi/issue544-" + Guid.NewGuid().ToString("N") + ".EFI");
            Result result = Invoke(["efi", "build", "-BootPolicy", "Diagnostic", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), .. (enabled ? new[] { "-EnableM04TypeManagerModuleRegistrationProbe", "-EnableM01LoadedImageStackProbe" } : Array.Empty<string>())]);
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
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { [Flag] = enabled, ["EnableM01LoadedImageStackProbe"] = enabled }));
            Need(evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length, "complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems), "ordered evaluated compile inventory");
            foreach (var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())
                Need(string.Equals(Hash(Path.Combine(root, input.GetProperty("path").GetString()!)), input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual raw source identity");
            foreach (var property in evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)))
                Need(property.Value.GetString() == (enabled && (property.Name == Flag || property.Name == "EnableM01LoadedImageStackProbe") ? "true" : "false"), "actual profile " + property.Name);
            using var stream = File.OpenRead(Path.Combine(directory, "OS.dll"));
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Need(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
            var types = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t => metadata.GetString(t.Name) == "NativeAotModuleRegistrationProbe").ToArray();
            Need(types.Length == (enabled ? 1 : 0), "conditional registration type absent/default and present/enabled");
            if (enabled)
                foreach (string name in new[]
                {
                    "Execute",
                    "ReadWord",
                    "ReadAbi",
                    "Zero",
                    "Preserved",
                    "Run"
                }

                )
                {
                    var methods = types[0].GetMethods().Where(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == name).ToArray();
                    Need(methods.Length == 1 && metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress != 0, "actual registration method " + name);
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
