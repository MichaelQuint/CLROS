using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using OS.Tooling.Native;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public sealed record EfiProjectResult(bool Succeeded, string Reason, string Detail, int CompileItemCount, int ReviewedSourceCount, int PrerequisiteSourceCount);

/// <summary>Reviewed static source admission plus isolated SDK evaluation of both workload profiles.</summary>
public static class EfiProjectAdmission
{
    public static XmlDocument ReadXml(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4194304 });
        XmlDocument document = new() { XmlResolver = null }; document.Load(reader); return document;
    }
    public static string CanonicalXmlSha256(XmlDocument document) => ContractAudit.HashText(document.OuterXml);
    private static XmlElement[] Elements(XmlNode document, string xpath) => document.SelectNodes(xpath)!.Cast<XmlElement>().ToArray();
    public static EfiProjectResult InspectStatic(string root, string? projectPath = null)
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(root, "src/OS/OS.csproj"));
            Require(projectPath is null || SamePath(projectPath, project), "noncanonical_project"); Require(File.Exists(project), "project_missing");
            XmlDocument xml;
            try { xml = ReadXml(project); } catch (Exception error) when (error is IOException or XmlException) { throw new ToolFailure("project_malformed", project); }
            VerifySerializerImport(root, project, xml);
            Require(CanonicalXmlSha256(xml) == EfiContracts.ProjectCanonicalXmlSha256, "noncanonical_project_shape");
            // The reviewed canonical shape binds all project defaults, error conditions, imports,
            // task boundaries and property groups. Independent semantic checks below also bind
            // item identity, physical source admission, no-stdlib guards and source contracts.
            XmlElement[] compile = Elements(xml, "/Project/ItemGroup/Compile");
            Require(compile.Length == EfiContracts.CompileItems.Count, "compile_item_count_mismatch");
            Dictionary<string, string> source = new(StringComparer.Ordinal), active = new(StringComparer.Ordinal);
            HashSet<string> physical = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < compile.Length; i++)
            {
                string include = compile[i].GetAttribute("Include"), link = compile[i].GetAttribute("Link");
                Require(include + "|" + link == EfiContracts.CompileItems[i], "compile_item_mismatch", i + ":" + include);
                string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
                _ = RelativePath(root, path, "source_outside_repository"); Require(physical.Add(path), "duplicate_compile_include", include); Require(File.Exists(path), "source_missing", include);
                AuthorityFiles.RequirePlainPath(root, path, "source_reparse_point");
                if (!include.StartsWith("..\\OS.Kernel\\", StringComparison.Ordinal) &&
                    !EfiContracts.SourceAdmissionReasons.ContainsKey(include)) continue;
                string text = File.ReadAllText(path); source[include] = text;
                EfiSourceResult result = EfiSourceAdmission.Inspect(text, include);
                Require(result.Succeeded, result.Reason, result.Detail); active[include] = result.ActiveText;
            }
            foreach (var pair in EfiContracts.NostdlibExclusionCounts)
            {
                Require(source.TryGetValue(pair.Key, out string? text), "source_missing", pair.Key);
                Require(Regex.Matches(text!, @"(?m)^\s*#if\s+!NOSTDLIB_EFI\s*$").Count == pair.Value, "nostdlib_efi_exclusion_mismatch", pair.Key);
            }
            foreach (var pair in EfiContracts.SourceContractTokens)
            {
                Require(active.TryGetValue(pair.Key, out string? text), "source_missing", pair.Key);
                foreach (string token in pair.Value) Require(text!.Contains(token, StringComparison.Ordinal), "source_contract_missing", pair.Key + ":" + token);
            }
            foreach (string path in EfiContracts.ReviewedSourceIncludes) Require(active.ContainsKey(path), "reviewed_source_missing", path);
            foreach (string path in EfiContracts.PrerequisiteSourceIncludes) Require(active.ContainsKey(path), "prerequisite_source_missing", path);
            VerifyCustomization(root, project);
            return new(true, "ok", "", compile.Length, EfiContracts.ReviewedSourceIncludes.Count, EfiContracts.PrerequisiteSourceIncludes.Count);
        }
        catch (ToolFailure failure) { return new(false, failure.Reason, failure.Detail, EfiContracts.CompileItems.Count, 0, 0); }
        catch (Exception error) when (error is IOException or XmlException or UnauthorizedAccessException or ArgumentException) { return new(false, "source_admission_failed", error.GetType().Name, 0, 0, 0); }
    }
    public static void VerifySerializerImport(string root, string project, XmlDocument xml)
    {
        Require(Elements(xml, "//ProjectReference|//ImportGroup|//Choose").Length == 0, "forbidden_project_input", "reference-importgroup-or-choose");
        XmlElement[] all = Elements(xml, "//Import"), imports = Elements(xml, "/Project/Import");
        Require(all.Length == 1 && imports.Length == 1, "serializer_import_count_mismatch");
        XmlElement import = imports[0]; string value = import.GetAttribute("Project");
        Require(import.Attributes.Count == 1 && import.HasAttribute("Project") && Elements(import, "*").Length == 0 && value == EfiContracts.SerializerTargetsImport && !Path.IsPathRooted(value), "serializer_import_shape_invalid", value);
        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, value)), expected = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets");
        Require(SamePath(path, expected), "serializer_import_path_invalid"); Require(File.Exists(path), "serializer_targets_missing");
        XmlDocument targets;
        try { targets = ReadXml(path); } catch (Exception error) when (error is IOException or XmlException) { throw new ToolFailure("serializer_targets_malformed", path); }
        Require(targets.DocumentElement?.Attributes.Count == 0, "serializer_targets_shape_invalid", "project-attributes");
        XmlElement[] injected = Elements(targets, "//Compile|//ProjectReference|//Reference|//PackageReference|//Analyzer|//AdditionalFiles|//UsingTask|//Import|//ImportGroup|//Choose|//Csc|//WriteLinesToFile|//Copy|//Delete");
        Require(injected.Length == 0, "serializer_targets_input_injection", injected.FirstOrDefault()?.Name ?? "");
        var cleanup = Elements(targets, "//RemoveDir"); XmlElement? owner = cleanup.Length == 1 ? cleanup[0].ParentNode as XmlElement : null;
        Require(cleanup.Length == 1 && owner is not null && owner.LocalName == "Target" && owner.Attributes.Count == 4 && owner.GetAttribute("Name") == "RemoveClrosX64InstructionPublicationSerializerToolOutput" && owner.GetAttribute("AfterTargets") == "CoreClean" && owner.GetAttribute("DependsOnTargets") == "TrackClrosX64InstructionPublicationSerializerOutput" && owner.GetAttribute("Condition") == "'$(NativeIntermediateOutputPath)' != ''" && cleanup[0].Attributes.Count == 2 && cleanup[0].GetAttribute("Directories") == "$(_ClrosX64SerializerTrackedToolRoot)" && cleanup[0].GetAttribute("Condition") == "Exists('$(_ClrosX64SerializerTrackedToolRoot)')" && Elements(cleanup[0], "*").Length == 0, "serializer_targets_cleanup_invalid");
        string[] outputNames = ["_ClrosX64SerializerGeneratorProjectSha256", "_ClrosX64SerializerToolsPropsSha256", "_ClrosX64SerializerRootPropsSha256", "_ClrosX64SerializerRootTargetsSha256", "_ClrosX64SerializerActualSha256", "_ClrosCheckedReferenceActualSha256", "_ClrosInterfaceDispatchActualSha256", "_ClrosSimpleCatchActualSha256", "_ClrosSingleFinallyActualSha256", "_ClrosRethrowInnerActualSha256", "_ClrosRethrowOuterActualSha256"];
        outputNames = [.. outputNames, "_ClrosNestedFinallyActualSha256"];
        outputNames = [.. outputNames, "_ClrosFrameDispatchActualSha256"];
        outputNames = [.. outputNames, "_ClrosConstructorFailureActualSha256"];
        outputNames = [.. outputNames, "_ClrosFilteredCatchActualSha256", "_ClrosFilterInvokeActualSha256"];
        var outputs = Elements(targets, "//Output"); Require(outputs.Length == outputNames.Length, "serializer_targets_output_injection");
        foreach (string name in outputNames) Require(outputs.Count(n => n.ParentNode?.Name == "GetFileHash" && n.Attributes.Count == 2 && n.GetAttribute("TaskParameter") == "Hash" && n.GetAttribute("PropertyName") == name && Elements(n, "*").Length == 0) == 1, "serializer_targets_output_injection", name);
        XmlDocument generator, tools;
        try { generator = ReadXml(Path.Combine(Path.GetDirectoryName(path)!, "OS.X64TextSerializerGenerator.csproj")); tools = ReadXml(Path.Combine(root, "tools/Directory.Build.props")); }
        catch (Exception error) when (error is IOException or XmlException) { throw new ToolFailure("serializer_build_input_malformed", error.GetType().Name); }
        Require(CanonicalXmlSha256(generator) == EfiContracts.SerializerProjectCanonicalXmlSha256 && CanonicalXmlSha256(tools) == EfiContracts.ToolsPropsCanonicalXmlSha256, "serializer_build_input_shape_invalid");
        Require(CanonicalXmlSha256(targets) == EfiContracts.SerializerTargetsCanonicalXmlSha256, "serializer_targets_shape_invalid");
    }
    public static void VerifyCustomization(string root, string project)
    {
        string? cursor = Path.GetDirectoryName(project); root = Path.GetFullPath(root);
        while (cursor is not null && !SamePath(cursor, root))
        {
            foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets" }) Require(!File.Exists(Path.Combine(cursor, name)), "unexpected_build_customization", Path.Combine(cursor, name));
            cursor = Path.GetDirectoryName(cursor);
        }
        Require(cursor is not null, "repository_root_unreachable");
        foreach ((string name, string hash) in new[] { ("Directory.Build.props", EfiContracts.RootPropsCanonicalXmlSha256), ("Directory.Build.targets", EfiContracts.RootTargetsCanonicalXmlSha256) })
        {
            string path = Path.Combine(root, name); Require(File.Exists(path), "build_customization_missing", path); XmlDocument xml;
            try { xml = ReadXml(path); } catch (Exception error) when (error is IOException or XmlException) { throw new ToolFailure("build_customization_malformed", path); }
            Require(CanonicalXmlSha256(xml) == hash, "build_customization_shape_invalid", path);
            Require(Elements(xml, "//Compile|//ProjectReference|//Reference|//PackageReference|//Analyzer|//AdditionalFiles|//UsingTask|//Import|//Choose|//Csc|//Output").Length == 0 && Elements(xml, "//Target/*").All(n => n.Name == "Error"), "build_customization_source_injection", path);
        }
        foreach (string relative in new[] { "Directory.Build.rsp", "tools/Directory.Build.targets", "tools/Directory.Build.rsp", "tools/OS.X64TextSerializerGenerator/Directory.Build.props", "tools/OS.X64TextSerializerGenerator/Directory.Build.targets", "tools/OS.X64TextSerializerGenerator/Directory.Build.rsp" }) Require(!File.Exists(Path.Combine(root, relative)), "unexpected_build_customization", relative);
        Require(File.Exists(Path.Combine(root, "tools/Directory.Build.props")), "build_customization_missing");
    }
    public static void Inspect(string root, string configuration, EfiProfile profile, string dotnet, string sdk, Action<string> emit, int timeoutSeconds = EfiBuildPolicy.BuildTimeoutSeconds, int outputCharacters = EfiBuildPolicy.OutputCharacters)
    {
        EfiProjectResult result = InspectStatic(root);
        try { Require(result.Succeeded, result.Reason, result.Detail); Evaluate(root, configuration, profile, dotnet, sdk, timeoutSeconds, outputCharacters); }
        catch (ToolFailure failure) { emit("EFI_PREFLIGHT_M01_SOURCE_CLOSURE reason=" + failure.Reason + " detail=" + EfiPreflight.Quote(failure.Detail) + " invalid"); throw new ToolFailure("m01_source_closure_" + failure.Reason, failure.Detail); }
        emit($"EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed={result.ReviewedSourceCount} prerequisites={result.PrerequisiteSourceCount} compileItems={result.CompileItemCount} prerequisiteGap=UefiMemoryType");
    }
    private static Dictionary<string, string> Isolation(string root, string sdk, string temp)
    {
        var properties = EfiMsbuild.IsolationProperties(root, temp, Path.Combine(temp, "packages"), sdk);
        properties["BaseIntermediateOutputPath"] = temp.Replace('\\', '/') + "/"; properties["IntermediateOutputPath"] = temp.Replace('\\', '/') + "/";
        properties["RestoreIgnoreFailedSources"] = "true"; return properties;
    }
    private static IEnumerable<string> IsolationKeys => new[] { "MSBuildSDKsPath", "MSBuildExtensionsPath", "MSBuildUserExtensionsPath", "UseSharedCompilation" }.Concat(EfiContracts.MsbuildWildcardPropertyNames);
    private static JsonObject Query(string root, string dotnet, string project, Dictionary<string, string> properties, IEnumerable<string> requested, string items, string prefix, int timeoutSeconds, int outputCharacters)
    {
        List<string> args = ["msbuild", project, "-noAutoResponse", "-nodeReuse:false", "-nologo", "-getProperty:" + string.Join(',', requested), "-getItem:" + items];
        args.AddRange(properties.Select(p => "-p:" + p.Key + "=" + p.Value));
        var environment = properties.Where(p => p.Key is "MSBuildSDKsPath" or "MSBuildExtensionsPath" or "MSBuildUserExtensionsPath" || p.Key.StartsWith("Import", StringComparison.Ordinal) || p.Key.StartsWith("Custom", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EfiBuildCommands.ProcessEnvironment) environment[pair.Key] = pair.Value!;
        var result = BoundedJobProcessRunner.Run(dotnet, args.ToArray(), root, timeoutSeconds, outputCharacters, environment, EfiBuildCommands.TerminateDescendantsAfterRootExit); EfiMsbuild.RequireProcess(result, prefix + "_process");
        int first = result.Output.IndexOf('{'), last = result.Output.LastIndexOf('}'); Require(first >= 0 && last > first, prefix + "_malformed");
        return Parse(result.Output[first..(last + 1)], prefix + "_malformed");
    }
    private static string Text(JsonNode? node) => String(node, "evaluated_property_mismatch");
    private static void VerifyAnalyzers(JsonArray analyzers, string reason, bool ordered = false)
    {
        string[] expected = ["Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll", "Microsoft.CodeAnalysis.NetAnalyzers.dll"];
        List<string> names = [];
        foreach (var analyzer in analyzers) { string name = Path.GetFileName(Text(analyzer!["FullPath"])); Require(expected.Contains(name, StringComparer.Ordinal) && Path.GetFileName(Text(analyzer["DefiningProjectFullPath"])) == "Microsoft.NET.Sdk.Analyzers.targets", reason); names.Add(name); }
        Require(names.Count == 2 && (ordered ? names.SequenceEqual(expected, StringComparer.Ordinal) : names.Distinct(StringComparer.Ordinal).Count() == 2), reason);
    }
    /// <summary>Return a fresh ordered inventory for the reviewed profile's
    /// conditional source links. Static declaration admission stays complete;
    /// both isolated and restored evaluations require this exact selection.</summary>
    public static string[] ExpectedCompileItems(EfiProfile profile)
    {
        bool linux = profile.IsEnabled("EnableM16008Probe");
        bool wait = profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe");
        bool lifecycle = linux || wait || profile.IsEnabled("EnableM09006Probe");
        bool timer = lifecycle || profile.IsEnabled("EnableM09005Probe"), contexts = timer || profile.IsEnabled("EnableM09004Probe");
        // Mirror the reviewed conditional links; retain static inventory order
        // and admit no new evaluated identity, link or defining-project authority.
        return EfiContracts.CompileItems.Where(item => item switch
        {
            @"Boot\EfiEntry.ThreadWait.cs|" or
            @"..\OS.Kernel\Threading\ThreadWaitProgram.cs|Threading\ThreadWaitProgram.cs" or
            @"..\OS.Kernel\Threading\ThreadLocalStorage.cs|Threading\ThreadLocalStorage.cs" => wait,
            @"..\OS.Kernel\Threading\WaitAddress.cs|Threading\WaitAddress.cs" => wait || linux,
            @"Boot\EfiEntry.KernelThread.cs|" => wait || profile.IsEnabled("EnableM09006Probe"),
            @"..\OS.Kernel\Threading\ReadyQueue.cs|Threading\ReadyQueue.cs" or
            @"..\OS.Kernel\Threading\KernelThreadSlot.cs|Threading\KernelThreadSlot.cs" or
            @"..\OS.Kernel\Threading\KernelThreadWait.cs|Threading\KernelThreadWait.cs" or
            @"..\OS.Kernel\Threading\KernelThreadTable.cs|Threading\KernelThreadTable.cs" or
            @"..\OS.Kernel\Threading\KernelThreadEntry.cs|Threading\KernelThreadEntry.cs" or
            @"..\OS.Kernel\Threading\KernelThreadProgram.cs|Threading\KernelThreadProgram.cs" => lifecycle,
            @"..\OS.Kernel\Threading\X64ThreadContext.cs|Threading\X64ThreadContext.cs" or
            @"..\OS.Kernel\Threading\ThreadContextSwitch.cs|Threading\ThreadContextSwitch.cs" => contexts,
            @"..\OS.Kernel\Threading\DeadlineQueue.cs|Threading\DeadlineQueue.cs" or
            @"..\OS.Kernel\Threading\SchedulerTimer.cs|Threading\SchedulerTimer.cs" or
            @"..\OS.Kernel\Pal\OSHostHandle.cs|Pal\OSHostHandle.cs" or
            @"..\OS.Kernel\Threading\X64TimerInterrupt.cs|Threading\X64TimerInterrupt.cs" => timer,
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxSignalMask.cs|Compatibility\LinuxX64\LinuxSignalMask.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxSignalServices.cs|Compatibility\LinuxX64\LinuxSignalServices.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxSignalServices.Frames.cs|Compatibility\LinuxX64\LinuxSignalServices.Frames.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.cs|Compatibility\LinuxX64\LinuxThreadServices.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Signals.cs|Compatibility\LinuxX64\LinuxThreadServices.Signals.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Futex.cs|Compatibility\LinuxX64\LinuxThreadServices.Futex.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs|Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxDescriptorServices.Pipes.cs|Compatibility\LinuxX64\LinuxDescriptorServices.Pipes.cs" or
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadProgram.cs|Compatibility\LinuxX64\LinuxThreadProgram.cs" or
            @"Boot\EfiEntry.LinuxThreads.cs|" or @"Boot\EfiEntry.LinuxThreadMemory.cs|" or
            @"Boot\EfiEntry.LinuxThreadRun.cs|" or @"Boot\EfiEntry.LinuxThreadTimer.cs|" => linux,
            _ => true
        }).ToArray();
    }
    public static void Evaluate(string root, string configuration, EfiProfile profile, string dotnet, string sdk, int timeoutSeconds = EfiBuildPolicy.BuildTimeoutSeconds, int outputCharacters = EfiBuildPolicy.OutputCharacters)
    {
        string project = Path.Combine(root, "src/OS/OS.csproj"), serializerDirectory = Path.Combine(root, "tools/OS.X64TextSerializerGenerator"), targets = Path.Combine(serializerDirectory, "OS.X64TextSerializerGenerator.targets");
        string[] expectedCompileItems = ExpectedCompileItems(profile);
        // The paths are deliberately absent; query-only MSBuild evaluation must not consume a
        // stale project.assets.json or generated NuGet import from any previous publish.
        string temp = Path.Combine(Path.GetTempPath(), "clros-source-evaluation-" + Guid.NewGuid().ToString("N"));
        foreach (string workload in new[] { "SingleMinimalApi", "MultiMinimalApi" })
        {
            var properties = Isolation(root, sdk, temp); properties["Configuration"] = configuration; properties["OSWorkloadProfile"] = workload; properties["ClrosBootPolicy"] = profile.BootPolicy;
            foreach (var pair in profile.Flags) properties[pair.Key] = pair.Value ? "true" : "false";
            string[] requested = ["Configuration", "ClrosBootPolicy", "DefineConstants", "EnableDefaultCompileItems", "NoStdLib", "DisableImplicitFrameworkReferences", .. profile.Flags.Keys, .. IsolationKeys];
            var evaluation = Query(root, dotnet, project, properties, requested, "Compile,ProjectReference,Reference,Analyzer,_ClrosX64SerializerGeneratorInput", "evaluated_project", timeoutSeconds, outputCharacters);
            var values = Object(evaluation["Properties"], "evaluated_project_malformed"); var items = Object(evaluation["Items"], "evaluated_project_malformed");
            Require(Text(values["Configuration"]) == configuration && Text(values["EnableDefaultCompileItems"]) == "false" && Text(values["NoStdLib"]) == "true" && Text(values["DisableImplicitFrameworkReferences"]) == "true", "evaluated_property_mismatch", "nostdlib");
            foreach (string key in IsolationKeys.Concat(profile.Flags.Keys).Append("ClrosBootPolicy")) Require(Text(values[key]) == properties[key], "evaluated_property_mismatch", workload + ":" + key);
            HashSet<string> expectedSymbols = new(["TRACE", "NOSTDLIB_EFI", configuration == "Debug" ? "DEBUG" : "RELEASE", workload == "SingleMinimalApi" ? "OS_PROFILE_SINGLE_MINIMAL_API" : "OS_PROFILE_MULTI_MINIMAL_API"], StringComparer.Ordinal);
            if (profile.BootPolicy == "VerifiedExit") expectedSymbols.Add("CLROS_NORMAL_VERIFIED_EXIT");
            foreach (var pair in EfiContracts.ProbeSymbols) if (profile.IsEnabled(pair.Key)) expectedSymbols.Add(pair.Value);
            var symbols = Text(values["DefineConstants"]).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            Require(symbols.SetEquals(expectedSymbols), "evaluated_define_constants_mismatch", workload);
            JsonArray compile = Array(items["Compile"], "evaluated_item_count_mismatch"), analyzers = Array(items["Analyzer"], "evaluated_item_count_mismatch"), inputs = Array(items["_ClrosX64SerializerGeneratorInput"], "evaluated_item_count_mismatch");
            string[] expectedInputs = ["OS.X64TextSerializerGenerator.csproj", "Program.cs", "Amd64CoffWriter.cs", "CheckedReferenceBarrierThunkGenerator.cs", "InstructionPublicationSerializerGenerator.cs", "InterfaceDispatchThunkGenerator.cs", "ExceptionHandlingThunkGenerator.cs", "ExceptionFilterThunkGenerator.cs", "EhContributionCatalogGenerator.cs", "NormalFinallyThunkGenerator.cs", "OS.X64TextSerializerGenerator.targets"];
            Require(compile.Count == expectedCompileItems.Length && Array(items["ProjectReference"], "evaluated_item_count_mismatch").Count == 0 && Array(items["Reference"], "evaluated_item_count_mismatch").Count == 0 && analyzers.Count == 2 && inputs.Count == expectedInputs.Length, "evaluated_item_count_mismatch");
            VerifyAnalyzers(analyzers, "evaluated_analyzer_mismatch");
            for (int i = 0; i < compile.Count; i++)
            {
                string include = expectedCompileItems[i].Split('|')[0]; var item = compile[i]!;
                Require(Text(item["Identity"]) + "|" + (item["Link"] is null ? "" : Text(item["Link"])) == expectedCompileItems[i] && SamePath(Text(item["FullPath"]), Path.Combine(root, "src/OS", include)) && SamePath(Text(item["DefiningProjectFullPath"]), project), "evaluated_compile_item_mismatch", workload + ":" + i);
            }
            for (int i = 0; i < inputs.Count; i++) { string expected = Path.Combine(serializerDirectory, expectedInputs[i]); Require(SamePath(Text(inputs[i]!["Identity"]), expected) && SamePath(Text(inputs[i]!["FullPath"]), expected) && SamePath(Text(inputs[i]!["DefiningProjectFullPath"]), targets), "evaluated_serializer_input_mismatch", workload + ":" + i); }
        }
        string generatorTemp = Path.Combine(Path.GetTempPath(), "clros-generator-evaluation-" + Guid.NewGuid().ToString("N"));
        var generatorProperties = Isolation(root, sdk, generatorTemp);
        generatorProperties["Configuration"] = configuration; generatorProperties["BaseOutputPath"] = Path.Combine(serializerDirectory, "obj/clros-m01-034-source-closure/bin").Replace('\\', '/') + "/";
        generatorProperties["BaseIntermediateOutputPath"] = Path.Combine(serializerDirectory, "obj/clros-m01-034-source-closure/obj").Replace('\\', '/') + "/";
        generatorProperties.Remove("IntermediateOutputPath"); generatorProperties["DefaultItemExcludes"] = "obj/**"; generatorProperties["DefaultExcludesInProjectFolder"] = "bin/**";
        generatorProperties["CustomBeforeMicrosoftCommonTargets"] = targets; generatorProperties["DirectoryBuildPropsPath"] = Path.Combine(root, "tools/Directory.Build.props");
        var generator = Query(root, dotnet, Path.Combine(serializerDirectory, "OS.X64TextSerializerGenerator.csproj"), generatorProperties, ["EnableDefaultCompileItems", .. IsolationKeys], "Compile,ProjectReference,Reference,PackageReference,Analyzer", "serializer_generator_evaluation", timeoutSeconds, outputCharacters);
        var generatorValues = Object(generator["Properties"], "serializer_generator_evaluation_malformed"); var generatorItems = Object(generator["Items"], "serializer_generator_evaluation_malformed");
        foreach (string key in IsolationKeys) Require(Text(generatorValues[key]) == generatorProperties[key], "serializer_generator_property_mismatch", key);
        var generatorCompile = Array(generatorItems["Compile"], "serializer_generator_compile_count_mismatch"); string[] expectedGenerator = ["Amd64CoffWriter.cs", "CheckedReferenceBarrierThunkGenerator.cs", "EhContributionCatalogGenerator.cs", "ExceptionFilterThunkGenerator.cs", "ExceptionHandlingThunkGenerator.cs", "InstructionPublicationSerializerGenerator.cs", "InterfaceDispatchThunkGenerator.cs", "NormalFinallyThunkGenerator.cs", "Program.cs"];
        Require(Text(generatorValues["EnableDefaultCompileItems"]) == "true" && generatorCompile.Count == expectedGenerator.Length && new[] { "ProjectReference", "Reference", "PackageReference" }.All(k => Array(generatorItems[k], "serializer_generator_compile_count_mismatch").Count == 0) && Array(generatorItems["Analyzer"], "serializer_generator_compile_count_mismatch").Count == 2, "serializer_generator_compile_count_mismatch");
        for (int i = 0; i < generatorCompile.Count; i++) Require(Text(generatorCompile[i]!["Identity"]) == expectedGenerator[i] && SamePath(Text(generatorCompile[i]!["FullPath"]), Path.Combine(serializerDirectory, expectedGenerator[i])) && Path.GetFileName(Text(generatorCompile[i]!["DefiningProjectFullPath"])) == "Microsoft.NET.Sdk.DefaultItems.props", "serializer_generator_compile_item_mismatch", i.ToString());
        VerifyAnalyzers(Array(generatorItems["Analyzer"], "serializer_generator_analyzer_mismatch"), "serializer_generator_analyzer_mismatch", true);
    }
}
