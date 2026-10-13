using System.Text.Json.Nodes;
using OS.Tooling.Native;

namespace OS.Tooling;

/// <summary>Frozen SDK/package paths and disabled ambient import hooks for every owned evaluation/build.</summary>
public static class EfiMsbuild
{
    public static Dictionary<string, string> CanonicalPaths(string packageRoot, string runtimePack, string sdk)
    {
        string Package(string id) => Path.Combine(packageRoot, id, EfiContracts.RestoreArchives.Single(p => p.Id == id).Version);
        string compiler = Package("microsoft.dotnet.ilcompiler"), host = Package("runtime.win-x64.microsoft.dotnet.ilcompiler"), linker = Package("microsoft.net.illink.tasks");
        string Dir(params string[] parts) => Path.Combine(parts) + Path.DirectorySeparatorChar;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ILCompilerTargetsPath"] = Path.Combine(compiler, "build/Microsoft.DotNet.ILCompiler.SingleEntry.targets"),
            ["ILLinkTargetsPath"] = Path.Combine(linker, "build/Microsoft.NET.ILLink.targets"), ["ILLinkTasksAssembly"] = Path.Combine(linker, "tools/net/ILLink.Tasks.dll"),
            ["IlcBuildTasksPath"] = Path.Combine(compiler, "tools/netstandard/ILCompiler.Build.Tasks.dll"), ["IlcHostPackagePath"] = host, ["IlcToolsPath"] = Dir(host, "tools"),
            ["RuntimePackagePath"] = runtimePack, ["IlcFrameworkPath"] = Dir(runtimePack, "runtimes/win-x64/lib"), ["IlcFrameworkNativePath"] = Dir(runtimePack, "runtimes/win-x64/native"),
            ["IlcSdkPath"] = Dir(runtimePack, "runtimes/win-x64/native"), ["IlcMibcPath"] = Dir(runtimePack, "mibc"),
            ["LanguageTargets"] = Path.Combine(sdk, "Microsoft.CSharp.targets"), ["CSharpTargetsPath"] = Path.Combine(sdk, "Microsoft.CSharp.CurrentVersion.targets"), ["CommonTargetsPath"] = Path.Combine(sdk, "Microsoft.Common.CurrentVersion.targets")
        }.ToDictionary(p => p.Key, p => p.Value.Replace('/', Path.DirectorySeparatorChar), StringComparer.Ordinal);
    }
    public static Dictionary<string, string> IsolationProperties(string root, string extensions, string packages, string sdk)
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal)
        {
            ["DirectoryBuildPropsPath"] = Path.Combine(root, "Directory.Build.props"), ["DirectoryBuildTargetsPath"] = Path.Combine(root, "Directory.Build.targets"),
            ["MSBuildProjectExtensionsPath"] = extensions.Replace('\\', '/').TrimEnd('/') + "/", ["ProjectAssetsFile"] = Path.Combine(extensions, "project.assets.json").Replace('\\', '/'),
            ["RestorePackagesPath"] = packages, ["NuGetPackageRoot"] = packages,
            ["MSBuildSDKsPath"] = Path.Combine(sdk, "Sdks"), ["MSBuildExtensionsPath"] = Path.TrimEndingDirectorySeparator(sdk) + Path.DirectorySeparatorChar,
            ["MSBuildUserExtensionsPath"] = Path.Combine(extensions, "disabled-user-extensions"),
            ["ImportDirectoryBuildProps"] = "true", ["ImportDirectoryBuildTargets"] = "true", ["ImportProjectExtensionProps"] = "false", ["ImportProjectExtensionTargets"] = "false", ["ExcludeRestorePackageImports"] = "true",
            ["BeforeMicrosoftNETSdkTargets"] = "", ["AfterMicrosoftNETSdkTargets"] = "", ["UseSharedCompilation"] = "false"
        };
        foreach (string name in EfiContracts.MsbuildWildcardPropertyNames) properties[name] = "false";
        foreach (string target in new[] { "MicrosoftCommonProps", "DirectoryBuildProps", "DirectoryBuildTargets", "MicrosoftCommonTargets", "MicrosoftCSharpTargets", "MicrosoftCommonCrossTargetingTargets" })
            foreach (string position in new[] { "Before", "After" }) properties["Custom" + position + target] = "";
        return properties;
    }
    public static Dictionary<string, string> NativeProperties(IReadOnlyDictionary<string, string> canonical)
    {
        Dictionary<string, string> properties = new(canonical, StringComparer.Ordinal)
        {
            ["ClrosIlcHostPackagePath"] = canonical["IlcHostPackagePath"], ["ClrosRuntimePackagePath"] = canonical["RuntimePackagePath"],
            ["RunILLink"] = "false", ["NativeCompilationDuringPublish"] = "true", ["IlcMultiModule"] = "false", ["DisableFrameworkLibGeneration"] = "true", ["IlcUseEnvironmentalTools"] = "true", ["PublishAotUsingRuntimePack"] = "false", ["IlcPgoOptimize"] = "false", ["PublishTrimmed"] = "true",
            ["ImportRuntimeIlcPackageTargetDependsOn"] = "", ["IlcSetupPropertiesDependsOn"] = "ImportRuntimeIlcPackageTarget", ["IlcDynamicBuildPropertyDependencies"] = "SetupProperties", ["ImportNuGetBuildTasksPackTargetsFromSdk"] = "false", ["NuGetBuildTasksPackTargets"] = ""
        };
        return properties;
    }
    public static JsonObject Evaluate(string root, string dotnet, string sdk, string configuration, string extensions, string packages, string runtimePack, EfiProfile profile, Action<string>? emit = null, int timeoutSeconds = EfiBuildPolicy.PostRestoreEvaluationTimeoutSeconds, int outputCharacters = EfiBuildPolicy.OutputCharacters)
    {
        EfiPeImage.Require(Directory.Exists(Path.Combine(sdk, "Sdks")), "post_restore_sdk_path_missing");
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(sdk)!, sdk, "post_restore_sdk_path_reparse_point");
        AuthorityFiles.RequirePlainPath(sdk, Path.Combine(sdk, "Sdks"), "post_restore_sdk_path_reparse_point");
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(extensions)!, extensions, "post_restore_project_extensions_reparse_point");
        EfiPeImage.Require(!Path.Exists(Path.Combine(extensions, "disabled-user-extensions")), "post_restore_user_extensions_path_present");
        Dictionary<string, string> canonical = CanonicalPaths(packages, runtimePack, sdk);
        Dictionary<string, string> native = NativeProperties(canonical), isolation = IsolationProperties(root, extensions, packages, sdk);
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["ClrosBootPolicy"] = profile.BootPolicy, ["_targetOS"] = "win", ["_targetArchitecture"] = "x64", ["RuntimeIdentifier"] = "win-x64", ["TargetFramework"] = "net10.0", ["PublishAot"] = "true", ["SelfContained"] = "true"
        };
        foreach (var property in native) expected[property.Key] = property.Value;
        foreach (var property in profile.Flags) expected[property.Key] = property.Value ? "true" : "false";
        foreach (string key in new[] { "ImportProjectExtensionProps", "ImportProjectExtensionTargets", "ExcludeRestorePackageImports", "MSBuildSDKsPath", "MSBuildExtensionsPath", "MSBuildUserExtensionsPath", "UseSharedCompilation" }.Concat(EfiContracts.MsbuildWildcardPropertyNames)) expected[key] = isolation[key];
        List<string> args = ["msbuild", Path.Combine(root, "src/OS/OS.csproj"), "-noAutoResponse", "-nodeReuse:false", "-nologo", "-getItem:Compile,ProjectReference,Reference,Analyzer", "-getProperty:" + string.Join(',', expected.Keys), "-p:Configuration=" + configuration, "-p:RuntimeIdentifier=win-x64", "-p:ClrosBootPolicy=" + profile.BootPolicy];
        args.AddRange(isolation.Select(p => "-p:" + p.Key + "=" + p.Value)); args.AddRange(native.Select(p => "-p:" + p.Key + "=" + p.Value));
        args.AddRange(profile.Flags.Select(p => "-p:" + p.Key + "=" + (p.Value ? "true" : "false")));
        var environment = isolation.Where(p => p.Key is "MSBuildSDKsPath" or "MSBuildExtensionsPath" or "MSBuildUserExtensionsPath" || p.Key.StartsWith("Import", StringComparison.Ordinal) || p.Key.StartsWith("Custom", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EfiBuildCommands.ProcessEnvironment) environment[pair.Key] = pair.Value!;
        var process = BoundedJobProcessRunner.Run(dotnet, args.ToArray(), root, timeoutSeconds, outputCharacters, environment, EfiBuildCommands.TerminateDescendantsAfterRootExit);
        RequireProcess(process, "post_restore_evaluation");
        int first = process.Output.IndexOf('{'), last = process.Output.LastIndexOf('}');
        EfiPeImage.Require(first >= 0 && last > first, "post_restore_evaluation_output_malformed");
        var evaluation = EfiEvidence.Parse(process.Output[first..(last + 1)], "post_restore_evaluation_json_invalid");
        JsonObject properties = EfiEvidence.Object(evaluation["Properties"], "post_restore_evaluation_shape_invalid"), items = EfiEvidence.Object(evaluation["Items"], "post_restore_evaluation_shape_invalid");
        EfiEvidence.Shape(properties, expected.Keys, "post_restore_evaluation_property_set_invalid");
        foreach (var pair in expected) EfiPeImage.Require(EfiEvidence.String(properties[pair.Key], "post_restore_evaluation_property_mismatch") == pair.Value, "post_restore_evaluation_property_mismatch", "property=" + pair.Key);
        var compile = EfiEvidence.Array(items["Compile"], "post_restore_evaluation_shape_invalid");
        var projectReferences = EfiEvidence.Array(items["ProjectReference"], "post_restore_evaluation_shape_invalid");
        var references = EfiEvidence.Array(items["Reference"], "post_restore_evaluation_shape_invalid");
        var analyzers = EfiEvidence.Array(items["Analyzer"], "post_restore_evaluation_shape_invalid");
        string[] expectedCompileItems = EfiProjectAdmission.ExpectedCompileItems(profile);
        EfiPeImage.Require(compile.Count == expectedCompileItems.Length && projectReferences.Count == 0 && references.Count == 0 && analyzers.Count == 2, "post_restore_evaluation_item_count_mismatch");
        List<string> compileKeys = [];
        for (int i = 0; i < compile.Count; i++)
        {
            string key = EfiEvidence.String(compile[i]!["Identity"], "post_restore_evaluation_compile_item_mismatch") + "|" + (compile[i]!["Link"] is null ? "" : EfiEvidence.String(compile[i]!["Link"], "post_restore_evaluation_compile_item_mismatch"));
            EfiPeImage.Require(key == expectedCompileItems[i], "post_restore_evaluation_compile_item_mismatch", "index=" + i); compileKeys.Add(key);
        }
        List<string> analyzerKeys = [];
        foreach (var analyzer in analyzers)
        {
            string name = Path.GetFileName(EfiEvidence.String(analyzer!["FullPath"], "post_restore_evaluation_analyzer_item_mismatch"));
            string defining = Path.GetFileName(EfiEvidence.String(analyzer["DefiningProjectFullPath"], "post_restore_evaluation_analyzer_item_mismatch"));
            EfiPeImage.Require(name is "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll" or "Microsoft.CodeAnalysis.NetAnalyzers.dll" && defining == "Microsoft.NET.Sdk.Analyzers.targets", "post_restore_evaluation_analyzer_item_mismatch");
            analyzerKeys.Add(name + "|" + defining);
        }
        JsonObject propertyJson = new(); foreach (var pair in expected) propertyJson[pair.Key] = pair.Value;
        emit?.Invoke($"EFI_POST_RESTORE_EVALUATION compile={compile.Count} analyzer={analyzers.Count} status=ok");
        return new()
        {
            ["compileCount"] = compile.Count, ["compileSha256"] = EfiEvidence.HashLines(compileKeys), ["projectReferenceCount"] = 0, ["projectReferenceSha256"] = ContractAudit.HashText(""), ["referenceCount"] = 0, ["referenceSha256"] = ContractAudit.HashText(""), ["analyzerCount"] = analyzers.Count, ["analyzerSha256"] = EfiEvidence.HashLines(analyzerKeys.Order(StringComparer.Ordinal)), ["properties"] = propertyJson
        };
    }
    public static void RequireProcess(BoundedJobProcessResult result, string prefix)
    {
        if (!result.TerminationConfirmed) throw new ToolFailure(prefix + "_termination_unconfirmed", result.Detail);
        string? reason = result.Status switch
        {
            "Success" => null, "InvalidTimeout" => "timeout_invalid", "InvalidOutputLimit" => "output_limit_invalid", "TimedOut" => "timeout", "OutputOverflow" => "output_overflow", "CaptureFailed" => "capture_failed", "NonZeroExit" => "nonzero_exit", "TerminationUnconfirmed" => "termination_unconfirmed", "StartFailed" => "start_failed", "JobConfigurationFailed" => "job_configuration_failed", "JobAssignmentFailed" => "job_assignment_failed", "ResumeFailed" => "resume_failed", "JobQueryFailed" => "job_query_failed", _ => "status_invalid"
        };
        if (reason is not null) throw new ToolFailure(prefix + "_" + reason, $"status={result.Status} exitCode={result.ExitCode} {result.Detail}");
    }
    public static void VerifyEvaluation(JsonNode? evidence, JsonObject snapshot)
    {
        EfiEvidence.Shape(evidence, snapshot.Select(p => p.Key), "post_restore_evidence_shape_invalid");
        foreach (string name in new[] { "compileCount", "projectReferenceCount", "referenceCount", "analyzerCount" }) EfiPeImage.Require(EfiEvidence.Integer(evidence![name], name) == EfiEvidence.Integer(snapshot[name], name), "post_restore_evidence_count_mismatch", "property=" + name);
        foreach (string name in new[] { "compileSha256", "projectReferenceSha256", "referenceSha256", "analyzerSha256" }) EfiPeImage.Require(EfiEvidence.String(evidence![name], "post_restore_evidence_hash_mismatch") == EfiEvidence.String(snapshot[name], "post_restore_evidence_hash_mismatch"), "post_restore_evidence_hash_mismatch", "property=" + name);
        var expected = EfiEvidence.Object(snapshot["properties"], "post_restore_evidence_property_set_invalid");
        EfiEvidence.Shape(evidence!["properties"], expected.Select(p => p.Key), "post_restore_evidence_property_set_invalid");
        foreach (var pair in expected) EfiPeImage.Require(EfiEvidence.String(evidence["properties"]![pair.Key], "post_restore_evidence_property_mismatch") == EfiEvidence.String(pair.Value, "post_restore_evidence_property_mismatch"), "post_restore_evidence_property_mismatch", "property=" + pair.Key);
    }
}
