using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling.Native;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

/// <summary>Fresh, isolated NativeAOT publication with locked source/package provenance.</summary>
public static class EfiBuild
{
    public static int Run(string[] args) => Run(args, Console.Out);
    public static int Run(string[] args, TextWriter output)
    {
        EfiBuildLog? log = null;
        void Emit(string line) { output.WriteLine(line); log?.WriteLine(line); }
        output.WriteLine("TEST_START efi.shell.publish");
        try
        {
            EfiOptions options = EfiOptions.Parse(args, true);
            string root = options.Root;
            string configuration = options.Get("Configuration", "Release"), runtime = options.Get("Runtime", "win-x64");
            Require(configuration is "Release" or "Debug", "configuration_invalid");
            Require(runtime == "win-x64", "runtime_identifier_invalid");
            _ = EfiBuildPolicy.BuildTimeout(options);
            _ = EfiBuildPolicy.OutputLimit(options);
            _ = EfiBuildPolicy.VerifierTimeout(options);
            string logPath = FullPath(root, options.Get("LogPath", EfiBuildPolicy.LogPath), "log_path_invalid");
            IReadOnlyList<string> sourcePaths = [];
            Require(!EfiContracts.SourceClosureExplicitPaths.Any(path => SamePath(Path.Combine(root, path), logPath)), "log_authority_source_path_collision");
            foreach (string protectedPath in new[] { "src", "std", "tools", ".git" })
            {
                string prefix = Path.Combine(root, protectedPath) + Path.DirectorySeparatorChar;
                Require(!logPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "log_authority_source_path_collision");
            }
            _ = EfiOutputPlan.Create(root, options.Get("EfiOutputPath", "artifacts/efi/BOOTX64.EFI"), logPath, null);
            using FileStream publishLock = EfiBuildFiles.OpenPublishLock(root);
            log = EfiBuildLog.Open(logPath);
            log.WriteLine("TEST_START efi.shell.publish");
            Emit("EFI_PUBLISH_OUTPUT_LOCK status=ok");
            EfiProjectResult projectShape = EfiProjectAdmission.InspectStatic(root);
            // Diagnose an invalid project before attempting to snapshot its invented
            // Compile paths. The locked preflight below still repeats admission.
            if (!projectShape.Succeeded && projectShape.Reason is "project_malformed" or "noncanonical_project_shape" or "forbidden_project_input")
            {
                Emit("EFI_PREFLIGHT_M01_SOURCE_CLOSURE reason=" + projectShape.Reason + " detail=" + EfiPreflight.Quote(projectShape.Detail) + " invalid");
                throw new ToolFailure("m01_source_closure_" + projectShape.Reason, projectShape.Detail);
            }
            using AuthorityFiles locks = new();
            EfiSnapshot source;
            try { sourcePaths = SourceClosurePaths(root); source = Snapshot(locks, root, sourcePaths, "source_closure", MaximumTextBytes, singleLink: true); }
            catch (ToolFailure failure) when (failure.Reason == "source_closure_project_malformed")
            {
                Emit("EFI_PREFLIGHT_M01_SOURCE_CLOSURE reason=project_malformed invalid");
                throw new ToolFailure("m01_source_closure_project_malformed", failure.Message);
            }
            catch (ToolFailure failure) { throw new ToolFailure("source_snapshot_failed", failure.Message); }
            RequireSourceInventory(root, sourcePaths);
            Emit("EFI_PUBLISH_SOURCE_LOCK status=ok");
            Emit("EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=" + source.FileCount);
            EfiProfile profile = EfiPreflight.Profile(options, Emit);
            EfiOutputPlan plan = EfiOutputPlan.Create(root, options.Get("EfiOutputPath", "artifacts/efi/BOOTX64.EFI"), logPath, profile);
            EfiPreflightResult preflight = EfiPreflight.Inspect(options, Emit, profile);
            locks.VerifyUnchanged(); log.VerifyIdentity();
            if (options.Flag("PreflightOnly")) return 0;
            Publish(options, preflight, plan, source, locks, log, Emit);
            Emit("TEST_PASS efi.shell.publish");
            Emit("EFI_ARTIFACT " + plan.EfiPath);
            Emit("EFI_LINKER_MAP " + plan.MapPath);
            Emit("EFI_LINK_EVIDENCE " + plan.EvidencePath);
            return 0;
        }
        catch (Exception error)
        {
            string reason = error is ToolFailure failure ? failure.Message : "publish_failed type=" + error.GetType().Name;
            string marker = "TEST_FAIL efi.shell.publish " + reason.Replace('\r', ' ').Replace('\n', ' ');
            output.WriteLine(marker);
            try { log?.WriteLine(marker); } catch { /* The terminal failure remains visible when the log authority itself failed. */ }
            return 1;
        }
        finally { log?.Dispose(); }
    }

    private static void Publish(EfiOptions options, EfiPreflightResult preflight, EfiOutputPlan output, EfiSnapshot source, AuthorityFiles locks, EfiBuildLog log, Action<string> emit)
    {
        string root = options.Root, configuration = preflight.Configuration;
        string project = Path.GetFullPath(Path.Combine(root, "src/OS/OS.csproj"));
        string intermediateRelative = $"src/OS/obj/{configuration}/net10.0/win-x64", binaryRelative = $"src/OS/bin/{configuration}/net10.0/win-x64";
        string intermediate = Path.GetFullPath(Path.Combine(root, intermediateRelative)), binaries = Path.GetFullPath(Path.Combine(root, binaryRelative));
        string extensions = Path.Combine(intermediate, "nuget"), packages = Path.Combine(intermediate, "packages"), pinned = Path.Combine(intermediate, "restore-source");
        string global = Path.GetFullPath(Environment.GetEnvironmentVariable("NUGET_PACKAGES") is string custom && !string.IsNullOrWhiteSpace(custom) ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages"));
        string sdk = preflight.DotnetSdkDirectory, runtimePack = preflight.NativeAotRuntimePackPath;
        Dictionary<string, string> canonical = EfiMsbuild.CanonicalPaths(packages, runtimePack, sdk);
        EfiBuildCommands commands = EfiBuildCommands.Create(options, preflight);
        var environment = commands.Environment;
        foreach (string marker in commands.ProbeMarkers) emit(marker);
        AuthorityFile Open(string path, string reason = "publish_input", long maximum = MaximumFileBytes, bool single = false) => locks.Open(Path.GetPathRoot(path)!, path, reason, reason, maximum, singleLink: single);
        string Hash(string path) => Open(path).Sha256;
        var dotnetFile = Open(preflight.DotnetHostPath, "dotnet_host");
        var linkerFile = Open(preflight.LinkerPath, "linker_identity");
        var sdkLibrary = Open(preflight.WindowsSdkLibraryPath, "windows_sdk_library");
        var discovery = Open(preflight.IlCompilerDiscoverySourcePath, "ilcompiler_discovery_source");
        foreach (string target in new[] { "LanguageTargets", "CSharpTargetsPath", "CommonTargetsPath" }) _ = Open(canonical[target], "sdk_target");
        string version = RunProcess(options, preflight.DotnetHostPath, ["--version"], root, "dotnet_sdk_recheck", emit, environment).Trim();
        Require(version == preflight.DotnetSdkVersion, "dotnet_sdk_changed_before_publish");
        EfiBuildFiles.FreshDirectory(root, intermediate, intermediateRelative);
        EfiBuildFiles.FreshDirectory(root, binaries, binaryRelative);
        emit("EFI_PUBLISH_FRESH_OUTPUTS status=ok directories=2 noIncremental=true");
        string[] archivePaths = EfiContracts.RestoreArchives.Select(a => a.Id + "/" + a.Version + "/" + a.FileName).ToArray();
        var archivePins = EfiContracts.RestoreArchives.ToDictionary(a => a.Id + "/" + a.Version + "/" + a.FileName, a => a.Sha256, StringComparer.Ordinal);
        EfiSnapshot archives = Snapshot(locks, global, archivePaths, "restore_archive", pins: archivePins);
        EfiBuildFiles.InitializeDirectory(pinned, "pinned_restore_source_path_invalid");
        foreach (var archive in EfiContracts.RestoreArchives)
        {
            var input = Open(Path.Combine(global, archive.Id, archive.Version, archive.FileName), "restore_archive");
            EfiBuildFiles.WriteCreateNew(pinned, Path.Combine(pinned, archive.FileName), input.ReadBytes(MaximumFileBytes, "restore_archive_read_failed"));
        }
        string config = Path.Combine(pinned, "NuGet.Config");
        string configText = new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"), new XElement("add", new XAttribute("key", "CLROS_PINNED"), new XAttribute("value", pinned))),
            new XElement("fallbackPackageFolders", new XElement("clear")), new XElement("auditSources", new XElement("clear")))).ToString();
        EfiBuildFiles.WriteCreateNew(pinned, config, Encoding.UTF8.GetBytes(configText));
        VerifyPinnedConfig(configText, pinned);
        string pinnedRelative = RelativePath(root, pinned, "pinned_restore_source_path_invalid");
        string[] pinnedPaths = new[] { pinnedRelative + "/NuGet.Config" }.Concat(EfiContracts.RestoreArchives.Select(a => pinnedRelative + "/" + a.FileName)).Order(StringComparer.Ordinal).ToArray();
        var pinnedPins = EfiContracts.RestoreArchives.ToDictionary(a => pinnedRelative + "/" + a.FileName, a => a.Sha256, StringComparer.Ordinal);
        EfiSnapshot pinnedSource = Snapshot(locks, root, pinnedPaths, "pinned_restore_source", pins: pinnedPins, singleLink: true);
        emit($"EFI_PUBLISH_PINNED_RESTORE_SOURCE status=ok archives={archives.FileCount} sourceInputs={pinnedSource.FileCount} sha256={pinnedSource.AggregateSha256}");
        RunProcess(options, commands.Executable, commands.Restore, commands.WorkingDirectory, "isolated_restore", emit, environment);
        locks.VerifyUnchanged();
        RequireExactEntries(extensions, RestorePaths(configuration).Select(Path.GetFileName)!);
        EfiSnapshot restored = Snapshot(locks, root, RestorePaths(configuration), "restore_closure", 4194304, singleLink: true);
        EfiRestore.VerifyGenerated(locks, project, extensions, packages);
        emit($"EFI_PUBLISH_RESTORE_CLOSURE_LOCKED status=ok inputs={restored.FileCount} sha256={restored.AggregateSha256}");
        EfiSnapshot packageBuild = Snapshot(locks, packages, EfiContracts.PackageBuildClosure.Select(p => p.Path), "package_build_closure", pins: EfiContracts.PackageBuildClosure.ToDictionary(p => p.Path, p => p.Sha256, StringComparer.Ordinal));
        EfiPackageSnapshot packageClosure = SnapshotPackages(locks, packages, runtimePack, pinned);
        string ilc = Path.Combine(packages, "runtime.win-x64.microsoft.dotnet.ilcompiler", preflight.IlCompilerPackageVersion, "tools/ilc.exe");
        var compilerFile = Open(ilc, "ilcompiler");
        Require(compilerFile.Sha256 == discovery.Sha256, "ilcompiler_discovery_source_identity_mismatch");
        string ilcVersion = FileVersionInfo.GetVersionInfo(ilc).FileVersion ?? "";
        Require(ilcVersion.Length != 0 && ilcVersion == preflight.IlCompilerFileVersion, "ilcompiler_version_mismatch");
        JsonObject evaluation = EfiMsbuild.Evaluate(root, preflight.DotnetHostPath, sdk, configuration, extensions, packages, runtimePack, preflight.Profile, emit,
            timeoutSeconds: commands.TimeoutSeconds, outputCharacters: commands.MaximumOutputCharacters);
        emit($"EFI_PUBLISH_PACKAGE_CLOSURE_LOCKED status=ok packages={packageClosure.Packages.Count} buildInputs={packageBuild.FileCount} sha256={packageClosure.AggregateSha256}");
        emit("EFI_PUBLISH_TOOLCHAIN_PIN linkerPath=" + preflight.LinkerPath + " linkerSha256=" + linkerFile.Sha256 + " ilcompilerPath=" + ilc + " ilcompilerSha256=" + compilerFile.Sha256);
        RunProcess(options, commands.Executable, commands.Publish, commands.WorkingDirectory, "nativeaot_publish", emit, environment);
        locks.VerifyUnchanged(); RequireSourceInventory(root, source.Inputs.Select(i => i.Path));
        RequireExactEntries(extensions, RestorePaths(configuration).Select(Path.GetFileName)!);
        RequireExactEntries(pinned, pinnedPaths.Select(Path.GetFileName)!);
        EfiRestore.VerifyGenerated(locks, project, extensions, packages);
        EfiMsbuild.VerifyEvaluation(evaluation, EfiMsbuild.Evaluate(root, preflight.DotnetHostPath, sdk, configuration, extensions, packages, runtimePack, preflight.Profile, emit,
            timeoutSeconds: commands.TimeoutSeconds, outputCharacters: commands.MaximumOutputCharacters));
        EfiPackageSnapshot finalPackages = SnapshotPackages(locks, packages, runtimePack, pinned);
        Require(finalPackages.AggregateSha256 == packageClosure.AggregateSha256, "package_or_restore_input_changed_during_publish");
        output.Clear();
        emit("EFI_PUBLISH_OUTPUT_AUTHORITY status=ok leaves=" + output.Paths.Count);
        string native = Path.Combine(intermediate, "native"), managed = Path.Combine(binaries, "OS.dll");
        var artifact = EfiBuildFiles.CopyCreateNew(locks, root, Path.Combine(binaries, "publish/OS.exe"), output.Directory, output.EfiPath);
        var map = EfiBuildFiles.CopyCreateNew(locks, root, Path.Combine(native, "OS.link.map"), output.Directory, output.MapPath);
        var response = EfiBuildFiles.CopyCreateNew(locks, root, Path.Combine(native, "link.rsp"), output.Directory, output.ResponsePath);
        string serializerPath = Path.Combine(native, "CLROS.X64.InstructionPublication.obj");
        var serializer = EfiBuildFiles.CopyCreateNew(locks, root, serializerPath, output.Directory, output.SerializerObjectPath);
        Require(serializer.Length == EfiContracts.SerializerObjectBytes && serializer.Sha256 == EfiContracts.SerializerObjectSha256, "serializer_object_mismatch");
        JsonObject evidence = new()
        {
            ["schemaVersion"] = 3, ["configuration"] = configuration, ["bootPolicy"] = preflight.Profile.BootPolicy, ["runtimeIdentifier"] = "win-x64",
            ["artifactSha256"] = artifact.Sha256, ["artifactBytes"] = artifact.Length, ["linkerMapSha256"] = map.Sha256, ["linkResponseSha256"] = response.Sha256,
            ["solutionSha256"] = Hash(Path.Combine(root, "CLROS.slnx")), ["sourceClosureSha256"] = source.AggregateSha256, ["sourceClosureInputs"] = source.ToJson(),
            ["restoreClosureSha256"] = restored.AggregateSha256, ["restoreClosureInputs"] = restored.ToJson(), ["restoreArchiveSourceRootPath"] = global,
            ["restoreArchiveClosureSha256"] = archives.AggregateSha256, ["restoreArchiveInputs"] = archives.ToJson(), ["pinnedRestoreSourcePath"] = pinned,
            ["pinnedRestoreSourceSha256"] = pinnedSource.AggregateSha256, ["pinnedRestoreSourceInputs"] = pinnedSource.ToJson(),
            ["restoreImportProjectExtensionProps"] = false, ["restoreImportProjectExtensionTargets"] = false, ["restoreExcludeRestorePackageImports"] = true,
            ["publishImportProjectExtensionProps"] = false, ["publishImportProjectExtensionTargets"] = false, ["publishExcludeRestorePackageImports"] = true,
            ["projectExtensionsPath"] = extensions, ["projectAssetsPath"] = Path.Combine(extensions, "project.assets.json"), ["projectAssetsSha256"] = Hash(Path.Combine(extensions, "project.assets.json")),
            ["packageRootPath"] = packages, ["packageRootKind"] = "isolated", ["packageClosureSha256"] = packageClosure.AggregateSha256, ["packages"] = packageClosure.Packages,
            ["packageBuildClosureSha256"] = packageBuild.AggregateSha256, ["packageBuildClosureInputs"] = packageBuild.ToJson(),
            ["activePackageImportEdges"] = Strings(EfiContracts.ActivePackageImportEdges), ["activePackageImportGraphSha256"] = HashLines(EfiContracts.ActivePackageImportEdges),
            ["packageTaskBindings"] = Strings(EfiContracts.PackageTaskBindings), ["packageTaskBindingsSha256"] = HashLines(EfiContracts.PackageTaskBindings), ["postRestoreEvaluation"] = evaluation,
            ["sourceProjectSha256"] = Hash(project), ["entrySourceSha256"] = Hash(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")), ["entryPointSymbol"] = "EfiMain", ["managedEntrySymbol"] = "OS_OS_Boot_EfiEntry__EfiMain",
            ["serializerSymbol"] = EfiContracts.SerializerSymbol, ["serializerExpectedSuccessCookie"] = EfiContracts.SerializerSuccessCookie,
            ["serializerLinkedObjectPath"] = serializerPath, ["serializerObjectPath"] = output.SerializerObjectPath, ["serializerObjectBytes"] = serializer.Length, ["serializerObjectSha256"] = serializer.Sha256,
            ["managedAssemblyPath"] = managed, ["managedAssemblySha256"] = Open(managed, "managed_assembly", single: true).Sha256,
            ["linkerPath"] = preflight.LinkerPath, ["linkerVersion"] = preflight.LinkerVersion, ["linkerSha256"] = linkerFile.Sha256,
            ["dotnetHostPath"] = preflight.DotnetHostPath, ["dotnetHostSha256"] = dotnetFile.Sha256, ["dotnetSdkVersion"] = preflight.DotnetSdkVersion,
            ["windowsSdkLibraryPath"] = preflight.WindowsSdkLibraryPath, ["windowsSdkVersion"] = preflight.WindowsSdkVersion, ["windowsSdkLibrarySha256"] = sdkLibrary.Sha256,
            ["ilCompilerPath"] = ilc, ["ilCompilerDiscoverySourcePath"] = preflight.IlCompilerDiscoverySourcePath, ["ilCompilerPackageVersion"] = preflight.IlCompilerPackageVersion, ["ilCompilerFileVersion"] = ilcVersion, ["ilCompilerSha256"] = compilerFile.Sha256,
            ["nativeAotRuntimePackPath"] = runtimePack, ["nativeAotRuntimePackVersion"] = preflight.NativeAotRuntimePackVersion, ["nativeAotRuntimePackSha256"] = packageClosure.Runtime.AggregateSha256,
            ["nativeAotRuntimePackFileCount"] = packageClosure.Runtime.FileCount, ["nativeAotRuntimePackBytes"] = packageClosure.Runtime.TotalBytes, ["runtimeListSha256"] = Hash(Path.Combine(runtimePack, "data/RuntimeList.xml")),
            ["generatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        foreach ((string property, string path) in EfiContracts.SerializerSourceBindings) evidence[property] = Hash(Path.Combine(root, path));
        foreach (var pair in canonical)
        {
            if (pair.Key == "RuntimePackagePath") continue;
            string name = pair.Key switch { "ILCompilerTargetsPath" => "ilCompilerTargetsPath", "ILLinkTargetsPath" => "ilLinkTargetsPath", "ILLinkTasksAssembly" => "ilLinkTasksAssemblyPath", "LanguageTargets" => "languageTargetsPath", "CSharpTargetsPath" => "csharpTargetsPath", _ => char.ToLowerInvariant(pair.Key[0]) + pair.Key[1..] };
            evidence[name] = pair.Value;
        }
        AddNativeObjects(preflight.Profile, native, output, locks, evidence);
        if (preflight.Profile.IsEnabled("M04EhCatalog"))
        {
            foreach (var (prefix, name, suffix) in new[] { ("ehCatalog", "CLROS.X64.EhCatalog.obj", ".eh-catalog.obj"),
                ("ehCatalogIlc", "OS.obj", ".ilc.obj"), ("ehCatalogMap", "OS.map.xml", ".ilc.map.xml") })
            {
                string linked = Path.Combine(native, name), preserved = output.EfiPath + suffix;
                var file = EfiBuildFiles.CopyCreateNew(locks, native, linked, output.Directory, preserved);
                evidence[prefix + "LinkedObjectPath"] = linked; evidence[prefix + "ObjectPath"] = preserved;
                evidence[prefix + "ObjectBytes"] = file.Length; evidence[prefix + "ObjectSha256"] = file.Sha256;
            }
        }
        EfiBuildFiles.WriteCreateNew(output.Directory, output.EvidencePath, Encoding.UTF8.GetBytes(evidence.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
        List<AuthorityFile> finalOutputs = output.Paths.Select(path => Open(path, "verifier_input", 536870912, true)).ToList();
        foreach (string path in new[] { serializerPath, Path.Combine(native, "OS.link.map"), managed }) finalOutputs.Add(Open(path, "verifier_input", 536870912, true));
        foreach (var bridge in NativeObjects(preflight.Profile)) finalOutputs.Add(Open(Path.Combine(native, bridge.FileName), "verifier_input", 536870912, true));
        if (preflight.Profile.IsEnabled("M04EhCatalog")) foreach (string name in new[] { "CLROS.X64.EhCatalog.obj", "OS.obj", "OS.map.xml" })
            finalOutputs.Add(Open(Path.Combine(native,name), "verifier_input", 536870912, true));
        AuthorityFiles.RequireDistinct(finalOutputs.ToArray());
        Require(finalOutputs.All(file => file.InitialIdentity.Key != log.IdentityKey), "verifier_input_log_alias");
        emit("EFI_PUBLISH_VERIFIER_INPUT_AUTHORITY status=ok inputs=" + finalOutputs.Count);
        List<string> verify = [typeof(EfiBuild).Assembly.Location, "efi", "verify", "--root", root, "--boot-policy", preflight.Profile.BootPolicy, "--efi-artifact", output.EfiPath, "--linker-map-path", output.MapPath,
            "--link-response-path", output.ResponsePath, "--link-evidence-path", output.EvidencePath, "--serializer-object-path", output.SerializerObjectPath];
        foreach (var flag in preflight.Profile.Flags) { verify.Add("--" + flag.Key); verify.Add(flag.Value ? "true" : "false"); }
        var timer = Stopwatch.StartNew();
        string verification = RunProcess(options, preflight.DotnetHostPath, verify, root, "artifact_verification", emit, environment, EfiBuildPolicy.VerifierTimeout(options));
        Require(verification.Split('\n').Count(line => line.TrimEnd('\r') == "TEST_PASS efi.artifact.verify") == 1 && !verification.Contains("TEST_FAIL ", StringComparison.Ordinal), "artifact_verification_terminal_invalid");
        locks.VerifyUnchanged(); log.VerifyIdentity(); RequireSourceInventory(root, source.Inputs.Select(i => i.Path));
        emit("EFI_PUBLISH_VERIFIER_INPUTS_UNCHANGED status=ok inputs=" + finalOutputs.Count);
        emit($"EFI_ARTIFACT_VERIFIER_BOUND status=ok elapsed_ms={timer.ElapsedMilliseconds} timeout_seconds={EfiBuildPolicy.VerifierTimeout(options)}");
        emit("EFI_PUBLISH_TOOLCHAIN_VERIFIED linkerPath=" + preflight.LinkerPath + " mapSha256=" + map.Sha256 + " responseSha256=" + response.Sha256);
        emit($"EFI_SERIALIZER_OBJECT path={output.SerializerObjectPath} bytes={serializer.Length} sha256={serializer.Sha256}");
    }

    public static string RunProcess(EfiOptions options, string executable, IEnumerable<string> arguments, string root, string operation, Action<string> emit, IReadOnlyDictionary<string, string?>? environment = null, int? timeout = null)
    {
        var watch = Stopwatch.StartNew();
        Dictionary<string, string?> childEnvironment = new(EfiBuildCommands.ProcessEnvironment, StringComparer.OrdinalIgnoreCase);
        if (environment is not null) foreach (var pair in environment) childEnvironment[pair.Key] = pair.Value;
        var result = BoundedJobProcessRunner.Run(executable, arguments.ToArray(), root, timeout ?? EfiBuildPolicy.BuildTimeout(options), EfiBuildPolicy.OutputLimit(options), childEnvironment,
            terminateDescendantsAfterRootExit: EfiBuildCommands.TerminateDescendantsAfterRootExit);
        foreach (string line in result.Output.Split('\n')) if (line.Length != 0) emit(line.TrimEnd('\r'));
        emit($"EFI_BUILD_PROCESS operation={operation} status={result.Status} exitCode={result.ExitCode} elapsedMs={watch.ElapsedMilliseconds} outputCharacters={result.OutputCharacters} terminationConfirmed={result.TerminationConfirmed.ToString().ToLowerInvariant()}");
        EfiMsbuild.RequireProcess(result, operation);
        return result.Output;
    }
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static void RequireSourceInventory(string root, IEnumerable<string> expected) => Require(SourceClosurePaths(root).SequenceEqual(expected, StringComparer.Ordinal), "source_closure_inventory_changed");
    private static void RequireExactEntries(string directory, IEnumerable<string> names)
    {
        Require(Directory.Exists(directory) && !Directory.EnumerateDirectories(directory).Any() && Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal), "restore_closure_entry_count_mismatch", directory);
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(directory)!, directory, "restore_closure_reparse_point");
    }
    private sealed record NativeObject(string Prefix, string Suffix, string FileName, long Bytes, string Sha256);
    private static IEnumerable<NativeObject> NativeObjects(EfiProfile profile)
    {
        if (profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization")) yield return new("checkedReference", ".references.obj", "CLROS.X64.CheckedReferenceBarriers.obj", EfiContracts.CheckedReferenceObjectBytes, EfiContracts.CheckedReferenceObjectSha256);
        if (profile.IsEnabled("M04InterfaceDispatch")) yield return new("interfaceDispatch", ".dispatch.obj", "CLROS.X64.InterfaceDispatch.obj", EfiContracts.InterfaceDispatchObjectBytes, EfiContracts.InterfaceDispatchObjectSha256);
        if (profile.IsEnabled("M04SimpleThrowCatch") || profile.IsEnabled("M04UnhandledException")) yield return new("simpleCatch", ".catch.obj", "CLROS.X64.SimpleThrowCatch.obj", EfiContracts.SimpleCatchObjectBytes, EfiContracts.SimpleCatchObjectSha256);
        if (profile.IsEnabled("M04SingleFinallyCatch")) yield return new("singleFinally", ".finally.obj", "CLROS.X64.SingleFinallyCatch.obj", EfiContracts.SingleFinallyObjectBytes, EfiContracts.SingleFinallyObjectSha256);
        if (profile.IsEnabled("M04FilteredCatch"))
        {
            yield return new("filteredCatch", ".filter-catch.obj", "CLROS.X64.FilteredCatch.obj", EfiContracts.FilteredCatchObjectBytes, EfiContracts.FilteredCatchObjectSha256);
            yield return new("filterInvoke", ".filter-invoke.obj", "CLROS.X64.FilterInvoke.obj", EfiContracts.FilterInvokeObjectBytes, EfiContracts.FilterInvokeObjectSha256);
        }
        if (profile.IsEnabled("M04ConstructorFailure")) yield return new("constructorFailure", ".ctor-failure.obj", "CLROS.X64.ConstructorFailure.obj", EfiContracts.ConstructorFailureObjectBytes, EfiContracts.ConstructorFailureObjectSha256);
        if (profile.IsEnabled("M04FrameDispatch")) yield return new("frameDispatch", ".frames.obj", "CLROS.X64.FrameDispatch.obj", EfiContracts.FrameDispatchObjectBytes, EfiContracts.FrameDispatchObjectSha256);
        if (profile.IsEnabled("M04NestedFinallyCatch")) yield return new("nestedFinally", ".nested-finally.obj", "CLROS.X64.NestedFinallyCatch.obj", EfiContracts.NestedFinallyObjectBytes, EfiContracts.NestedFinallyObjectSha256);
        if (profile.IsEnabled("M04Rethrow"))
        {
            yield return new("rethrowInner", ".rethrow-inner.obj", "CLROS.X64.RethrowInner.obj", EfiContracts.RethrowInnerObjectBytes, EfiContracts.RethrowInnerObjectSha256);
            yield return new("rethrowOuter", ".rethrow-outer.obj", "CLROS.X64.RethrowOuter.obj", EfiContracts.RethrowOuterObjectBytes, EfiContracts.RethrowOuterObjectSha256);
        }
    }
    private static void AddNativeObjects(EfiProfile profile, string native, EfiOutputPlan output, AuthorityFiles locks, JsonObject evidence)
    {
        foreach (var bridge in NativeObjects(profile))
        {
            string linked = Path.Combine(native, bridge.FileName), preserved = output.EfiPath + bridge.Suffix;
            var file = EfiBuildFiles.CopyCreateNew(locks, native, linked, output.Directory, preserved);
            Require(file.Length == bridge.Bytes && file.Sha256 == bridge.Sha256, bridge.Prefix + "_object_identity_mismatch");
            evidence[bridge.Prefix + "LinkedObjectPath"] = linked; evidence[bridge.Prefix + "ObjectPath"] = preserved;
            evidence[bridge.Prefix + "ObjectBytes"] = file.Length; evidence[bridge.Prefix + "ObjectSha256"] = file.Sha256;
        }
    }

    public sealed record EfiPackageSnapshot(JsonArray Packages, string AggregateSha256, EfiSnapshot Runtime);
    public static EfiPackageSnapshot SnapshotPackages(AuthorityFiles locks, string packages, string runtimePack, string pinned)
    {
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(packages)!, packages, "package_root_reparse_point");
        List<string> versions = [];
        foreach (string id in Directory.EnumerateFileSystemEntries(packages))
        {
            Require(Directory.Exists(id) && (File.GetAttributes(id) & FileAttributes.ReparsePoint) == 0, "isolated_package_root_shape_invalid");
            foreach (string version in Directory.EnumerateFileSystemEntries(id))
            {
                Require(Directory.Exists(version) && (File.GetAttributes(version) & FileAttributes.ReparsePoint) == 0, "isolated_package_version_shape_invalid");
                versions.Add(Path.GetFileName(id) + "/" + Path.GetFileName(version));
            }
        }
        Require(versions.Order(StringComparer.Ordinal).SequenceEqual(EfiContracts.ConsumedPackageTrees.Where(p => p.RootKind != "runtime").Select(p => p.Id + "/" + p.Version).Order(StringComparer.Ordinal), StringComparer.Ordinal), "isolated_package_version_set_invalid");
        JsonArray entries = []; List<string> closure = []; EfiSnapshot? runtime = null;
        foreach (var contract in EfiContracts.ConsumedPackageTrees)
        {
            string root = contract.RootKind == "runtime" ? runtimePack : Path.Combine(packages, contract.Id, contract.Version);
            var metadataFile = locks.Open(root, Path.Combine(root, ".nupkg.metadata"), "package-metadata", "package_metadata", 65536);
            JsonObject metadata = Parse(metadataFile.ReadUtf8(65536, "package_metadata_invalid"), "package_metadata_invalid");
            Shape(metadata, ["version", "contentHash", "source"], "package_metadata_shape_invalid");
            Require(Integer(metadata["version"], "version") == 2 && String(metadata["contentHash"], "package_metadata_contract_invalid") == contract.ContentHashSha512 && !string.IsNullOrWhiteSpace(String(metadata["source"], "package_metadata_contract_invalid")), "package_metadata_contract_invalid");
            if (contract.RootKind != "runtime") Require(SamePath(String(metadata["source"], "package_metadata_source_invalid"), pinned), "package_metadata_source_mismatch");
            EfiSnapshot tree = PackageTree(locks, root);
            Require(tree.FileCount == contract.FileCount && tree.TotalBytes == contract.TotalBytes && tree.AggregateSha256 == contract.AggregateSha256, "package_tree_contract_mismatch", "package=" + contract.Id);
            entries.Add(new JsonObject { ["id"] = contract.Id, ["version"] = contract.Version, ["rootKind"] = contract.RootKind, ["rootPath"] = root, ["contentHashSha512"] = contract.ContentHashSha512,
                ["fileCount"] = tree.FileCount, ["totalBytes"] = tree.TotalBytes, ["aggregateSha256"] = tree.AggregateSha256, ["metadataSha256"] = metadataFile.Sha256, ["inputs"] = tree.ToJson(true) });
            closure.Add(contract.Id + "|" + contract.Version + "|" + contract.RootKind + "|" + tree.AggregateSha256);
            if (contract.RootKind == "runtime") runtime = tree;
        }
        return new(entries, HashLines(closure), runtime ?? throw new ToolFailure("nativeaot_runtime_package_snapshot_missing"));
    }
}
