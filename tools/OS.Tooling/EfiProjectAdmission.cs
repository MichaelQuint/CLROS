using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using OS.Tooling.Native;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
/// <summary>Reviewed static source admission plus isolated SDK evaluation of both workload profiles.</summary>

public static class EfiProjectAdmission
{
    public static XmlDocument ReadXml(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4194304 });
        XmlDocument document = new()
        {
            XmlResolver = null
        };
        document.Load(reader);
        return document;
    }

    public static string CanonicalXmlSha256(XmlDocument document) => ContractAudit.HashText(document.OuterXml);
    private static XmlElement[] Elements(XmlNode document, string xpath) => document.SelectNodes(xpath)!.Cast<XmlElement>().ToArray();
    public static EfiProjectResult InspectStatic(string root, string? projectPath = null)
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(root, "src/OS/OS.csproj"));
            Require(projectPath is null || SamePath(projectPath, project), "noncanonical_project");
            Require(File.Exists(project), "project_missing");
            XmlDocument xml;
            try
            {
                xml = ReadXml(project);
            }
            catch (Exception error) when (error is IOException or XmlException)
            {
                throw new ToolFailure("project_malformed", project);
            }

            VerifySerializerImport(root, project, xml);
            Require(CanonicalXmlSha256(xml) == EfiContracts.ProjectCanonicalXmlSha256, "noncanonical_project_shape");
            // The reviewed canonical shape binds all project defaults, error conditions, imports,
            // task boundaries and property groups. Independent semantic checks below also bind
            // item identity, physical source admission, no-stdlib guards and source contracts.
            XmlElement[] compile = Elements(xml, "/Project/ItemGroup/Compile");
            Require(compile.Length == EfiContracts.CompileItems.Count, "compile_item_count_mismatch");
            VerifySourceFamilyInventory(compile.Select(item => item.GetAttribute("Include")));
            Dictionary<string, string> source = new(StringComparer.Ordinal), active = new(StringComparer.Ordinal);
            HashSet<string> physical = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < compile.Length; i++)
            {
                string include = compile[i].GetAttribute("Include"), link = compile[i].GetAttribute("Link");
                Require(include + "|" + link == EfiContracts.CompileItems[i], "compile_item_mismatch", i + ":" + include);
                string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
                _ = RelativePath(root, path, "source_outside_repository");
                Require(physical.Add(path), "duplicate_compile_include", include);
                Require(File.Exists(path), "source_missing", include);
                AuthorityFiles.RequirePlainPath(root, path, "source_reparse_point");
                string? text = null;
                if (EfiContracts.SourceFamilyIdentities.ContainsKey(include))
                {
                    text = File.ReadAllText(path);
                    VerifySourceFamilyPin(include, text);
                }
                if (!include.StartsWith("..\\OS.Kernel\\", StringComparison.Ordinal) && !EfiContracts.SourceAdmissionReasons.ContainsKey(include))
                    continue;
                text ??= File.ReadAllText(path);
                source[include] = text;
                EfiSourceResult result = EfiSourceAdmission.Inspect(text, include);
                Require(result.Succeeded, result.Reason, result.Detail);
                active[include] = result.ActiveText;
            }

            foreach (var pair in EfiContracts.NostdlibExclusionCounts)
            {
                Require(source.TryGetValue(pair.Key, out string? text), "source_missing", pair.Key);
                Require(Regex.Matches(text!, @"(?m)^\s*#if\s+!NOSTDLIB_EFI\s*$").Count == pair.Value, "nostdlib_efi_exclusion_mismatch", pair.Key);
            }

            foreach (var pair in EfiContracts.SourceContractTokens)
            {
                Require(active.TryGetValue(pair.Key, out string? text), "source_missing", pair.Key);
                foreach (string token in pair.Value)
                    Require(text!.Contains(token, StringComparison.Ordinal), "source_contract_missing", pair.Key + ":" + token);
            }

            foreach (string path in EfiContracts.ReviewedSourceIncludes)
                Require(active.ContainsKey(path), "reviewed_source_missing", path);
            foreach (string path in EfiContracts.PrerequisiteSourceIncludes)
                Require(active.ContainsKey(path), "prerequisite_source_missing", path);
            VerifyCustomization(root, project);
            return new(true, "ok", "", compile.Length, EfiContracts.ReviewedSourceIncludes.Count, EfiContracts.PrerequisiteSourceIncludes.Count);
        }
        catch (ToolFailure failure)
        {
            return new(false, failure.Reason, failure.Detail, EfiContracts.CompileItems.Count, 0, 0);
        }
        catch (Exception error) when (error is IOException or XmlException or UnauthorizedAccessException or ArgumentException)
        {
            return new(false, "source_admission_failed", error.GetType().Name, 0, 0, 0);
        }
    }

    internal static void VerifySourceFamilyInventory(IEnumerable<string> includes)
    {
        var admitted = includes.ToHashSet(StringComparer.Ordinal);
        foreach (string required in EfiContracts.SourceFamilyIdentities.Keys)
            Require(admitted.Contains(required), "source_family_member_missing", required);
    }

    internal static void VerifySourceFamilyPin(string include, string source)
    {
        Require(EfiContracts.SourceFamilyIdentities.TryGetValue(include, out string? expected),
            "source_family_member_unknown", include);
        Require(ContractAudit.HashSource(source) == expected, "source_family_identity_mismatch", include);
    }

    public static void VerifySerializerImport(string root, string project, XmlDocument xml)
    {
        Require(Elements(xml, "//ProjectReference|//ImportGroup|//Choose").Length == 0, "forbidden_project_input", "reference-importgroup-or-choose");
        XmlElement[] all = Elements(xml, "//Import"), imports = Elements(xml, "/Project/Import");
        Require(all.Length == 1 && imports.Length == 1, "serializer_import_count_mismatch");
        XmlElement import = imports[0];
        string value = import.GetAttribute("Project");
        Require(import.Attributes.Count == 1 && import.HasAttribute("Project") && Elements(import, "*").Length == 0 && value == EfiContracts.SerializerTargetsImport && !Path.IsPathRooted(value), "serializer_import_shape_invalid", value);
        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, value)), expected = Path.Combine(root, "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets");
        Require(SamePath(path, expected), "serializer_import_path_invalid");
        Require(File.Exists(path), "serializer_targets_missing");
        XmlDocument targets;
        try
        {
            targets = ReadXml(path);
        }
        catch (Exception error) when (error is IOException or XmlException)
        {
            throw new ToolFailure("serializer_targets_malformed", path);
        }

        Require(targets.DocumentElement?.Attributes.Count == 0, "serializer_targets_shape_invalid", "project-attributes");
        XmlElement[] injected = Elements(targets, "//Compile|//ProjectReference|//Reference|//PackageReference|//Analyzer|//AdditionalFiles|//UsingTask|//Import|//ImportGroup|//Choose|//Csc|//WriteLinesToFile|//Copy|//Delete");
        Require(injected.Length == 0, "serializer_targets_input_injection", injected.FirstOrDefault()?.Name ?? "");
        var cleanup = Elements(targets, "//RemoveDir");
        XmlElement? owner = cleanup.Length == 1 ? cleanup[0].ParentNode as XmlElement : null;
        Require(cleanup.Length == 1 && owner is not null && owner.LocalName == "Target" && owner.Attributes.Count == 4 && owner.GetAttribute("Name") == "RemoveClrosX64InstructionPublicationSerializerToolOutput" && owner.GetAttribute("AfterTargets") == "CoreClean" && owner.GetAttribute("DependsOnTargets") == "TrackClrosX64InstructionPublicationSerializerOutput" && owner.GetAttribute("Condition") == "'$(NativeIntermediateOutputPath)' != ''" && cleanup[0].Attributes.Count == 2 && cleanup[0].GetAttribute("Directories") == "$(_ClrosX64SerializerTrackedToolRoot)" && cleanup[0].GetAttribute("Condition") == "Exists('$(_ClrosX64SerializerTrackedToolRoot)')" && Elements(cleanup[0], "*").Length == 0, "serializer_targets_cleanup_invalid");
        string[] outputNames = ["_ClrosX64SerializerGeneratorProjectSha256", "_ClrosX64SerializerToolsPropsSha256", "_ClrosX64SerializerRootPropsSha256", "_ClrosX64SerializerRootTargetsSha256", "_ClrosX64SerializerActualSha256", "_ClrosCheckedReferenceActualSha256", "_ClrosInterfaceDispatchActualSha256", "_ClrosSimpleCatchActualSha256", "_ClrosSingleFinallyActualSha256", "_ClrosRethrowInnerActualSha256", "_ClrosRethrowOuterActualSha256"];
        outputNames = [.. outputNames, "_ClrosNestedFinallyActualSha256"];
        outputNames = [.. outputNames, "_ClrosFrameDispatchActualSha256"];
        outputNames = [.. outputNames, "_ClrosConstructorFailureActualSha256"];
        outputNames = [.. outputNames, "_ClrosFilteredCatchActualSha256", "_ClrosFilterInvokeActualSha256"];
        var outputs = Elements(targets, "//Output");
        Require(outputs.Length == outputNames.Length, "serializer_targets_output_injection");
        foreach (string name in outputNames)
            Require(outputs.Count(n => n.ParentNode?.Name == "GetFileHash" && n.Attributes.Count == 2 && n.GetAttribute("TaskParameter") == "Hash" && n.GetAttribute("PropertyName") == name && Elements(n, "*").Length == 0) == 1, "serializer_targets_output_injection", name);
        XmlDocument generator, tools;
        try
        {
            generator = ReadXml(Path.Combine(Path.GetDirectoryName(path)!, "OS.X64TextSerializerGenerator.csproj"));
            tools = ReadXml(Path.Combine(root, "tools/Directory.Build.props"));
        }
        catch (Exception error) when (error is IOException or XmlException)
        {
            throw new ToolFailure("serializer_build_input_malformed", error.GetType().Name);
        }

        Require(CanonicalXmlSha256(generator) == EfiContracts.SerializerProjectCanonicalXmlSha256 && CanonicalXmlSha256(tools) == EfiContracts.ToolsPropsCanonicalXmlSha256, "serializer_build_input_shape_invalid");
        Require(CanonicalXmlSha256(targets) == EfiContracts.SerializerTargetsCanonicalXmlSha256, "serializer_targets_shape_invalid");
    }

    public static void VerifyCustomization(string root, string project)
    {
        string? cursor = Path.GetDirectoryName(project);
        root = Path.GetFullPath(root);
        while (cursor is not null && !SamePath(cursor, root))
        {
            foreach (string name in new[]
            {
                "Directory.Build.props",
                "Directory.Build.targets"
            }

            )
                Require(!File.Exists(Path.Combine(cursor, name)), "unexpected_build_customization", Path.Combine(cursor, name));
            cursor = Path.GetDirectoryName(cursor);
        }

        Require(cursor is not null, "repository_root_unreachable");
        foreach ((string name, string hash) in new[]
        {
            ("Directory.Build.props", EfiContracts.RootPropsCanonicalXmlSha256),
            ("Directory.Build.targets", EfiContracts.RootTargetsCanonicalXmlSha256)
        }

        )
        {
            string path = Path.Combine(root, name);
            Require(File.Exists(path), "build_customization_missing", path);
            XmlDocument xml;
            try
            {
                xml = ReadXml(path);
            }
            catch (Exception error) when (error is IOException or XmlException)
            {
                throw new ToolFailure("build_customization_malformed", path);
            }

            Require(CanonicalXmlSha256(xml) == hash, "build_customization_shape_invalid", path);
            Require(Elements(xml, "//Compile|//ProjectReference|//Reference|//PackageReference|//Analyzer|//AdditionalFiles|//UsingTask|//Import|//Choose|//Csc|//Output").Length == 0 && Elements(xml, "//Target/*").All(n => n.Name == "Error"), "build_customization_source_injection", path);
        }

        foreach (string relative in new[]
        {
            "Directory.Build.rsp",
            "tools/Directory.Build.targets",
            "tools/Directory.Build.rsp",
            "tools/OS.X64TextSerializerGenerator/Directory.Build.props",
            "tools/OS.X64TextSerializerGenerator/Directory.Build.targets",
            "tools/OS.X64TextSerializerGenerator/Directory.Build.rsp"
        }

        )
            Require(!File.Exists(Path.Combine(root, relative)), "unexpected_build_customization", relative);
        Require(File.Exists(Path.Combine(root, "tools/Directory.Build.props")), "build_customization_missing");
    }

    public static void Inspect(string root, string configuration, EfiProfile profile, string dotnet, string sdk, Action<string> emit, int timeoutSeconds = EfiBuildPolicy.BuildTimeoutSeconds, int outputCharacters = EfiBuildPolicy.OutputCharacters)
    {
        EfiProjectResult result = InspectStatic(root);
        try
        {
            Require(result.Succeeded, result.Reason, result.Detail);
            Evaluate(root, configuration, profile, dotnet, sdk, timeoutSeconds, outputCharacters);
        }
        catch (ToolFailure failure)
        {
            emit("EFI_PREFLIGHT_M01_SOURCE_CLOSURE reason=" + failure.Reason + " detail=" + EfiPreflight.Quote(failure.Detail) + " invalid");
            throw new ToolFailure("m01_source_closure_" + failure.Reason, failure.Detail);
        }

        emit($"EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed={result.ReviewedSourceCount} prerequisites={result.PrerequisiteSourceCount} compileItems={result.CompileItemCount} prerequisiteGap=UefiMemoryType");
    }

    internal static Dictionary<string, string> Isolation(string root, string sdk, string temp)
    {
        var properties = EfiMsbuild.IsolationProperties(root, temp, Path.Combine(temp, "packages"), sdk);
        properties["BaseIntermediateOutputPath"] = temp.Replace('\\', '/') + "/";
        properties["IntermediateOutputPath"] = temp.Replace('\\', '/') + "/";
        properties["RestoreIgnoreFailedSources"] = "true";
        return properties;
    }

    internal static IEnumerable<string> IsolationKeys => new[]
    {
        "MSBuildSDKsPath",
        "MSBuildExtensionsPath",
        "MSBuildUserExtensionsPath",
        "UseSharedCompilation"
    }.Concat(EfiContracts.MsbuildWildcardPropertyNames);

    internal static JsonObject Query(string root, string dotnet, string project, Dictionary<string, string> properties, IEnumerable<string> requested, string items, string prefix, int timeoutSeconds, int outputCharacters)
    {
        List<string> args = ["msbuild", project, "-noAutoResponse", "-nodeReuse:false", "-nologo", "-getProperty:" + string.Join(',', requested), "-getItem:" + items];
        args.AddRange(properties.Select(p => "-p:" + p.Key + "=" + p.Value));
        var environment = properties.Where(p => p.Key is "MSBuildSDKsPath" or "MSBuildExtensionsPath" or "MSBuildUserExtensionsPath" || p.Key.StartsWith("Import", StringComparison.Ordinal) || p.Key.StartsWith("Custom", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in EfiBuildCommands.ProcessEnvironment)
            environment[pair.Key] = pair.Value!;
        var result = BoundedJobProcessRunner.Run(dotnet, args.ToArray(), root, timeoutSeconds, outputCharacters, environment, EfiBuildCommands.TerminateDescendantsAfterRootExit);
        EfiMsbuild.RequireProcess(result, prefix + "_process");
        int first = result.Output.IndexOf('{'), last = result.Output.LastIndexOf('}');
        Require(first >= 0 && last > first, prefix + "_malformed");
        return Parse(result.Output[first..(last + 1)], prefix + "_malformed");
    }

    private static string Text(JsonNode? node) => String(node, "evaluated_property_mismatch");
    private static void VerifyAnalyzers(JsonArray analyzers, string reason, bool ordered = false)
    {
        string[] expected = ["Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll", "Microsoft.CodeAnalysis.NetAnalyzers.dll"];
        List<string> names = [];
        foreach (var analyzer in analyzers)
        {
            string name = Path.GetFileName(Text(analyzer!["FullPath"]));
            Require(expected.Contains(name, StringComparer.Ordinal) && Path.GetFileName(Text(analyzer["DefiningProjectFullPath"])) == "Microsoft.NET.Sdk.Analyzers.targets", reason);
            names.Add(name);
        }

        Require(names.Count == 2 && (ordered ? names.SequenceEqual(expected, StringComparer.Ordinal) : names.Distinct(StringComparer.Ordinal).Count() == 2), reason);
    }

    /// <summary>Return a fresh ordered inventory for the reviewed profile's
    /// conditional source links. Static declaration admission stays complete;
    /// both isolated and restored evaluations require this exact selection.</summary>

    public static string[] ExpectedCompileItems(EfiProfile profile)
    {
        bool linux = profile.IsEnabled("EnableM16008Probe");
        bool network = profile.IsEnabled("EnableM07026Probe");
        bool wait = profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe");
        bool lifecycle = linux || wait || profile.IsEnabled("EnableM09006Probe");
        bool timer = lifecycle || profile.IsEnabled("EnableM09005Probe"), contexts = timer || profile.IsEnabled("EnableM09004Probe") || network;
        // Mirror the reviewed conditional links; retain static inventory order
        // and admit no new evaluated identity, link or defining-project authority.
        return EfiContracts.CompileItems.Where(item => item switch
        {
            "..\\OS.Kernel\\Diagnostics\\WritableStorageTiming.cs|Diagnostics\\WritableStorageTiming.cs" or
            "..\\OS.Kernel\\Diagnostics\\WritableStorageTiming.Bucket.cs|Diagnostics\\WritableStorageTiming.Bucket.cs" or
            "..\\OS.Kernel\\Diagnostics\\WritableStorageTiming.State.cs|Diagnostics\\WritableStorageTiming.State.cs" or
            "..\\OS.Kernel\\Diagnostics\\WritableStorageTiming.Stamp.cs|Diagnostics\\WritableStorageTiming.Stamp.cs" => profile.IsEnabled("EnableM16027TimingProbe"),
            "..\\OS.Kernel\\Storage\\WritableBlockSession.Admission.cs|Storage\\WritableBlockSession.Admission.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.ClockAccess.cs|Storage\\WritableBlockSession.ClockAccess.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.Completion.cs|Storage\\WritableBlockSession.Completion.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.cs|Storage\\WritableBlockSession.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.Observation.cs|Storage\\WritableBlockSession.Observation.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.ObservationFlags.cs|Storage\\WritableBlockSession.ObservationFlags.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.Operation.cs|Storage\\WritableBlockSession.Operation.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.RequestToken.cs|Storage\\WritableBlockSession.RequestToken.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.State.cs|Storage\\WritableBlockSession.State.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockSession.Status.cs|Storage\\WritableBlockSession.Status.cs" or "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Admission.cs|Storage\\ContinuingFat32Mutation.Admission.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.cs|Storage\\ContinuingFat32Mutation.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Extent.cs|Storage\\ContinuingFat32Mutation.Extent.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.FileToken.cs|Storage\\ContinuingFat32Mutation.FileToken.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.FreeCursor.cs|Storage\\ContinuingFat32Mutation.FreeCursor.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.FreeResult.cs|Storage\\ContinuingFat32Mutation.FreeResult.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Kind.cs|Storage\\ContinuingFat32Mutation.Kind.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Life.cs|Storage\\ContinuingFat32Mutation.Life.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.MutationResult.cs|Storage\\ContinuingFat32Mutation.MutationResult.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.OpenFacts.cs|Storage\\ContinuingFat32Mutation.OpenFacts.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Purpose.cs|Storage\\ContinuingFat32Mutation.Purpose.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.RequestToken.cs|Storage\\ContinuingFat32Mutation.RequestToken.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Result.cs|Storage\\ContinuingFat32Mutation.Result.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.RootFacts.cs|Storage\\ContinuingFat32Mutation.RootFacts.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.State.cs|Storage\\ContinuingFat32Mutation.State.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Status.cs|Storage\\ContinuingFat32Mutation.Status.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Step.cs|Storage\\ContinuingFat32Mutation.Step.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Mutation.Transform.cs|Storage\\ContinuingFat32Mutation.Transform.cs" or "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.Admission.cs|Storage\\ContinuingFat32Bootstrap.Admission.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.cs|Storage\\ContinuingFat32Bootstrap.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.Life.cs|Storage\\ContinuingFat32Bootstrap.Life.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.Result.cs|Storage\\ContinuingFat32Bootstrap.Result.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.State.cs|Storage\\ContinuingFat32Bootstrap.State.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.Status.cs|Storage\\ContinuingFat32Bootstrap.Status.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingFat32Bootstrap.Step.cs|Storage\\ContinuingFat32Bootstrap.Step.cs" or "..\\OS.Kernel\\Storage\\WritableBlockReadPort.Admission.cs|Storage\\WritableBlockReadPort.Admission.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.Completion.cs|Storage\\WritableBlockReadPort.Completion.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.cs|Storage\\WritableBlockReadPort.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.Inspection.cs|Storage\\WritableBlockReadPort.Inspection.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.State.cs|Storage\\WritableBlockReadPort.State.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.Status.cs|Storage\\WritableBlockReadPort.Status.cs" or
            "..\\OS.Kernel\\Storage\\WritableBlockReadPort.Token.cs|Storage\\WritableBlockReadPort.Token.cs" or "..\\OS.Kernel\\Storage\\Fat32Mutation.Change.cs|Storage\\Fat32Mutation.Change.cs" or
            "..\\OS.Kernel\\Storage\\Fat32Mutation.Encoder.cs|Storage\\Fat32Mutation.Encoder.cs" or
            "..\\OS.Kernel\\Storage\\Fat32Mutation.Stage.cs|Storage\\Fat32Mutation.Stage.cs" or
            "..\\OS.Kernel\\Storage\\Fat32Mutation.Status.cs|Storage\\Fat32Mutation.Status.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Progress.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Progress.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Operation.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Operation.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Step.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Step.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.FreeSearch.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.FreeSearch.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Descriptor.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Descriptor.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Description.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Description.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Admission.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Admission.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessSlot.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessSlot.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.RangeLock.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.RangeLock.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockAdmission.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockAdmission.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessToken.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessToken.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessResult.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.ProcessResult.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockRequest.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockRequest.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockResult.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.LockResult.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.DescriptorToken.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.DescriptorToken.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.RequestToken.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.RequestToken.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Result.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Result.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Metadata.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Metadata.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.StatMetadata.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.StatMetadata.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Pending.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.Pending.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.State.cs|Compatibility\\LinuxX64\\LinuxContinuingWritableDescriptorServices.State.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDisk.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDisk.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskProgress.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskProgress.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskToken.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskToken.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskResult.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskResult.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskBinding.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskBinding.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskRequest.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskRequest.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskState.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskState.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskAdmission.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.WritableDiskAdmission.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.WritableDisk.cs|Compatibility\LinuxX64\LinuxThreadServices.WritableDisk.cs" or "Boot\\EfiEntry.LinuxUserCarrier.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxUserCarrier.cs|" or "Boot\\EfiEntry.WritableDisk.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeWritableDiskProgress.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeWritableCarrier.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeWritableOwner.cs|" or "Boot\\EfiEntry.WritableDiskEvidence.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeWritableEvidence.cs|" => profile.IsEnabled("EnableM16027Probe"),
            "Boot\\EfiEntry.DiskNative.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskProgress.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskOwner.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskPollCause.cs|" or @"Boot\EfiEntry.DiskBootstrap.cs|" or "Boot\\EfiEntry.DiskEvidence.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskExitObservation.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskRecord.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NativeDiskEvidence.cs|" or "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.Admission.cs|Storage\\ContinuingReadOnlyBlockService.Admission.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.ClockAccess.cs|Storage\\ContinuingReadOnlyBlockService.ClockAccess.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.Completion.cs|Storage\\ContinuingReadOnlyBlockService.Completion.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.cs|Storage\\ContinuingReadOnlyBlockService.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.Observation.cs|Storage\\ContinuingReadOnlyBlockService.Observation.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.ObservationAccess.cs|Storage\\ContinuingReadOnlyBlockService.ObservationAccess.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.ObservationKind.cs|Storage\\ContinuingReadOnlyBlockService.ObservationKind.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.ReadToken.cs|Storage\\ContinuingReadOnlyBlockService.ReadToken.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.State.cs|Storage\\ContinuingReadOnlyBlockService.State.cs" or
            "..\\OS.Kernel\\Storage\\ContinuingReadOnlyBlockService.Status.cs|Storage\\ContinuingReadOnlyBlockService.Status.cs" or "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Admission.cs|Storage\\ReadOnlyWebRootFileSystem.Admission.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.BorrowedAdmission.cs|Storage\\ReadOnlyWebRootFileSystem.BorrowedAdmission.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.CacheSlot.cs|Storage\\ReadOnlyWebRootFileSystem.CacheSlot.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.cs|Storage\\ReadOnlyWebRootFileSystem.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.FileChains.cs|Storage\\ReadOnlyWebRootFileSystem.FileChains.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Directories.cs|Storage\\ReadOnlyWebRootFileSystem.Directories.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.DirectoryCursor.cs|Storage\\ReadOnlyWebRootFileSystem.DirectoryCursor.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Extent.cs|Storage\\ReadOnlyWebRootFileSystem.Extent.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.FileChainCertificate.cs|Storage\\ReadOnlyWebRootFileSystem.FileChainCertificate.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.FileHandle.cs|Storage\\ReadOnlyWebRootFileSystem.FileHandle.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.FileInspection.cs|Storage\\ReadOnlyWebRootFileSystem.FileInspection.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.FileToken.cs|Storage\\ReadOnlyWebRootFileSystem.FileToken.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.ImmutableDirectoryRecord.cs|Storage\\ReadOnlyWebRootFileSystem.ImmutableDirectoryRecord.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Metadata.cs|Storage\\ReadOnlyWebRootFileSystem.Metadata.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Mount.cs|Storage\\ReadOnlyWebRootFileSystem.Mount.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.OpenResult.cs|Storage\\ReadOnlyWebRootFileSystem.OpenResult.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Operation.cs|Storage\\ReadOnlyWebRootFileSystem.Operation.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.OperationToken.cs|Storage\\ReadOnlyWebRootFileSystem.OperationToken.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Read.cs|Storage\\ReadOnlyWebRootFileSystem.Read.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Scratch.cs|Storage\\ReadOnlyWebRootFileSystem.Scratch.cs" or
            "..\\OS.Kernel\\Storage\\ReadOnlyWebRootFileSystem.Status.cs|Storage\\ReadOnlyWebRootFileSystem.Status.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.Disk.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.Disk.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskProgress.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskProgress.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskToken.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskToken.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskResult.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskResult.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskBinding.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskBinding.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskRequest.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskRequest.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskState.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskState.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskAdmission.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.DiskAdmission.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.Disk.cs|Compatibility\\LinuxX64\\LinuxThreadServices.Disk.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.DiskPumpMeasurements.cs|Compatibility\\LinuxX64\\LinuxThreadServices.DiskPumpMeasurements.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.DiskDispatchMeasurements.cs|Compatibility\\LinuxX64\\LinuxThreadServices.DiskDispatchMeasurements.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.DiskDispatchTotals.cs|Compatibility\\LinuxX64\\LinuxThreadServices.DiskDispatchTotals.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.DiskPumpAccess.cs|Compatibility\\LinuxX64\\LinuxThreadServices.DiskPumpAccess.cs" => profile.IsEnabled("EnableM16025Probe"),
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.DiskVerificationMeasurements.cs|Compatibility\\LinuxX64\\LinuxThreadServices.DiskVerificationMeasurements.cs" => profile.IsEnabled("EnableM10018TimingProbe"),
            "Boot\\EfiEntry.StaticFiles.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxStaticPending.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxStaticFileIdentity.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxStaticFileBefore.cs|" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxStaticFileObservation.cs|Compatibility\\LinuxX64\\LinuxStaticFileObservation.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxStaticFileObservation.Progress.cs|Compatibility\\LinuxX64\\LinuxStaticFileObservation.Progress.cs" => profile.IsEnabled("EnableM10017Probe") || profile.IsEnabled("EnableM10018Probe"),
            @"Boot\EfiEntry.LinuxWeb.cs|" or @"Boot\EfiEntry.Realtime.cs|" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxAppLogBudget.cs|Compatibility\\LinuxX64\\LinuxAppLogBudget.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxAppLogBudget.ChargeStatus.cs|Compatibility\\LinuxX64\\LinuxAppLogBudget.ChargeStatus.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxAppStartupObservation.cs|Compatibility\LinuxX64\LinuxAppStartupObservation.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxAppLogObservation.cs|Compatibility\\LinuxX64\\LinuxAppLogObservation.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxAppLogObservation.StreamState.cs|Compatibility\\LinuxX64\\LinuxAppLogObservation.StreamState.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxAppLogQuotaObservation.cs|Compatibility\LinuxX64\LinuxAppLogQuotaObservation.cs" => profile.IsEnabled("EnableM10004Probe"),
            "Boot\\EfiEntry.LinuxSockets.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxSocketLifetime.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxSocketStorage.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxSocketIoCarrier.cs|" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketServices.cs|Compatibility\\LinuxX64\\LinuxSocketServices.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketServices.Endpoint.cs|Compatibility\\LinuxX64\\LinuxSocketServices.Endpoint.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketServices.State.cs|Compatibility\\LinuxX64\\LinuxSocketServices.State.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.cs|Compatibility\\LinuxX64\\LinuxEpollServices.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.Port.cs|Compatibility\\LinuxX64\\LinuxEpollServices.Port.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.Registration.cs|Compatibility\\LinuxX64\\LinuxEpollServices.Registration.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.State.cs|Compatibility\\LinuxX64\\LinuxEpollServices.State.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.Operation.cs|Compatibility\\LinuxX64\\LinuxEpollServices.Operation.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.ThreadRefresh.cs|Compatibility\\LinuxX64\\LinuxEpollServices.ThreadRefresh.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxEpollServices.ReadinessScan.cs|Compatibility\\LinuxX64\\LinuxEpollServices.ReadinessScan.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketNetwork.cs|Compatibility\\LinuxX64\\LinuxSocketNetwork.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketNetwork.Status.cs|Compatibility\\LinuxX64\\LinuxSocketNetwork.Status.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketNetwork.Slot.cs|Compatibility\\LinuxX64\\LinuxSocketNetwork.Slot.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketNetwork.State.cs|Compatibility\\LinuxX64\\LinuxSocketNetwork.State.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSocketNetwork.LeaseObservation.cs|Compatibility\\LinuxX64\\LinuxSocketNetwork.LeaseObservation.cs" or "..\\OS.Kernel\\Net\\TcpInitialSequence.cs|Net\\TcpInitialSequence.cs" or
            "..\\OS.Kernel\\Net\\TcpInitialSequence.Status.cs|Net\\TcpInitialSequence.Status.cs" or
            "..\\OS.Kernel\\Net\\TcpInitialSequence.Owner.cs|Net\\TcpInitialSequence.Owner.cs" or
            "..\\OS.Kernel\\Net\\TcpInitialSequence.Lease.cs|Net\\TcpInitialSequence.Lease.cs" or
            "..\\OS.Kernel\\Net\\TcpInitialSequence.Result.cs|Net\\TcpInitialSequence.Result.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Epoll.cs|Compatibility\LinuxX64\LinuxThreadServices.Epoll.cs" => profile.IsEnabled("EnableM16011Probe"),
            "Boot\\EfiEntry.NetworkService.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NetworkOwnerStorage.cs|" or "Boot\\EfiEntry.NetworkTcp.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NetworkTcpSlot.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NetworkTcpStorage.cs|" or "Boot\\EfiEntry.NetworkScheduler.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.NetworkSchedulerControl.cs|" or @"..\OS.Kernel\Threading\KernelThreadProgressProgram.cs|Threading\KernelThreadProgressProgram.cs" or "..\\OS.Kernel\\Drivers\\ContinuingVirtioNet.cs|Drivers\\ContinuingVirtioNet.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNet.Status.cs|Drivers\\ContinuingVirtioNet.Status.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNet.State.cs|Drivers\\ContinuingVirtioNet.State.cs" or "..\\OS.Kernel\\Drivers\\ContinuingVirtioNetDevice.cs|Drivers\\ContinuingVirtioNetDevice.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNetDevice.Status.cs|Drivers\\ContinuingVirtioNetDevice.Status.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNetDevice.ClockAccess.cs|Drivers\\ContinuingVirtioNetDevice.ClockAccess.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNetDevice.Admission.cs|Drivers\\ContinuingVirtioNetDevice.Admission.cs" or
            "..\\OS.Kernel\\Drivers\\ContinuingVirtioNetDevice.State.cs|Drivers\\ContinuingVirtioNetDevice.State.cs" or @"..\OS.Kernel\Net\Ipv4Header.cs|Net\Ipv4Header.cs" or @"..\OS.Kernel\Net\IcmpEchoPacket.cs|Net\IcmpEchoPacket.cs" or "..\\OS.Kernel\\Net\\Ipv4IcmpEchoFrame.Ipv4IcmpEchoFrameInfo.cs|Net\\Ipv4IcmpEchoFrame.Ipv4IcmpEchoFrameInfo.cs" or
            "..\\OS.Kernel\\Net\\Ipv4IcmpEchoFrame.cs|Net\\Ipv4IcmpEchoFrame.cs" or "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionLifecycleState.cs|Net\\TcpConnectionTable.TcpConnectionLifecycleState.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionTableStatus.cs|Net\\TcpConnectionTable.TcpConnectionTableStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionKey.cs|Net\\TcpConnectionTable.TcpConnectionKey.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionSequenceMetadata.cs|Net\\TcpConnectionTable.TcpConnectionSequenceMetadata.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionTableEntry.cs|Net\\TcpConnectionTable.TcpConnectionTableEntry.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.TcpConnectionTableResult.cs|Net\\TcpConnectionTable.TcpConnectionTableResult.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.cs|Net\\TcpConnectionTable.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.OwnershipStatus.cs|Net\\TcpConnectionTable.OwnershipStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.Owner.cs|Net\\TcpConnectionTable.Owner.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.Handle.cs|Net\\TcpConnectionTable.Handle.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionTable.ScanResult.cs|Net\\TcpConnectionTable.ScanResult.cs" or "..\\OS.Kernel\\Net\\TcpListenerBacklog.TcpListenerBacklogEntryState.cs|Net\\TcpListenerBacklog.TcpListenerBacklogEntryState.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.TcpListenerBacklogStatus.cs|Net\\TcpListenerBacklog.TcpListenerBacklogStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.TcpListenerBacklogConnection.cs|Net\\TcpListenerBacklog.TcpListenerBacklogConnection.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.TcpListenerBacklogEntry.cs|Net\\TcpListenerBacklog.TcpListenerBacklogEntry.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.TcpListenerBacklogResult.cs|Net\\TcpListenerBacklog.TcpListenerBacklogResult.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.cs|Net\\TcpListenerBacklog.cs" or
            "..\\OS.Kernel\\Net\\TcpListenerBacklog.ScanResult.cs|Net\\TcpListenerBacklog.ScanResult.cs" or "..\\OS.Kernel\\Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeState.cs|Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeState.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeStatus.cs|Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeTransitionResult.cs|Net\\TcpPassiveOpenHandshake.TcpPassiveOpenHandshakeTransitionResult.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveOpenHandshake.cs|Net\\TcpPassiveOpenHandshake.cs" or "..\\OS.Kernel\\Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlanStatus.cs|Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlanStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlannerInfo.cs|Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlannerInfo.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlanResult.cs|Net\\TcpPassiveIpv4FramePlanner.TcpPassiveIpv4FramePlanResult.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveIpv4FramePlanner.cs|Net\\TcpPassiveIpv4FramePlanner.cs" or "..\\OS.Kernel\\Net\\TcpPassiveListenerBacklogBridge.TcpPassiveListenerBacklogBridgeStatus.cs|Net\\TcpPassiveListenerBacklogBridge.TcpPassiveListenerBacklogBridgeStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveListenerBacklogBridge.TcpPassiveListenerBacklogBridgeResult.cs|Net\\TcpPassiveListenerBacklogBridge.TcpPassiveListenerBacklogBridgeResult.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveListenerBacklogBridge.cs|Net\\TcpPassiveListenerBacklogBridge.cs" or "..\\OS.Kernel\\Net\\TcpPassiveConnection.cs|Net\\TcpPassiveConnection.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveConnection.Status.cs|Net\\TcpPassiveConnection.Status.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveConnection.Operations.cs|Net\\TcpPassiveConnection.Operations.cs" or
            "..\\OS.Kernel\\Net\\TcpPassiveConnection.State.cs|Net\\TcpPassiveConnection.State.cs" or @"..\OS.Kernel\Net\TcpHeader.cs|Net\TcpHeader.cs" or "..\\OS.Kernel\\Net\\Ipv4TransportChecksum.Ipv4TransportChecksumStatus.cs|Net\\Ipv4TransportChecksum.Ipv4TransportChecksumStatus.cs" or
            "..\\OS.Kernel\\Net\\Ipv4TransportChecksum.Ipv4TransportChecksumResult.cs|Net\\Ipv4TransportChecksum.Ipv4TransportChecksumResult.cs" or
            "..\\OS.Kernel\\Net\\Ipv4TransportChecksum.cs|Net\\Ipv4TransportChecksum.cs" or "..\\OS.Kernel\\Net\\TcpEstablishedStream.TcpEstablishedStreamStatus.cs|Net\\TcpEstablishedStream.TcpEstablishedStreamStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStream.TcpEstablishedStreamReceiveResult.cs|Net\\TcpEstablishedStream.TcpEstablishedStreamReceiveResult.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStream.TcpEstablishedStreamReadResult.cs|Net\\TcpEstablishedStream.TcpEstablishedStreamReadResult.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStream.TcpEstablishedStreamSendResult.cs|Net\\TcpEstablishedStream.TcpEstablishedStreamSendResult.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStream.cs|Net\\TcpEstablishedStream.cs" or "..\\OS.Kernel\\Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlanStatus.cs|Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlanStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlannerInfo.cs|Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlannerInfo.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlanResult.cs|Net\\TcpEstablishedStreamIpv4FramePlanner.TcpEstablishedStreamIpv4FramePlanResult.cs" or
            "..\\OS.Kernel\\Net\\TcpEstablishedStreamIpv4FramePlanner.cs|Net\\TcpEstablishedStreamIpv4FramePlanner.cs" or "..\\OS.Kernel\\Net\\TcpConnectionClose.TcpConnectionCloseState.cs|Net\\TcpConnectionClose.TcpConnectionCloseState.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionClose.TcpConnectionCloseStatus.cs|Net\\TcpConnectionClose.TcpConnectionCloseStatus.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionClose.TcpConnectionCloseTransitionResult.cs|Net\\TcpConnectionClose.TcpConnectionCloseTransitionResult.cs" or
            "..\\OS.Kernel\\Net\\TcpConnectionClose.cs|Net\\TcpConnectionClose.cs" or "..\\OS.Kernel\\Net\\TcpReceiveWindow.cs|Net\\TcpReceiveWindow.cs" or
            "..\\OS.Kernel\\Net\\TcpReceiveWindow.Status.cs|Net\\TcpReceiveWindow.Status.cs" or
            "..\\OS.Kernel\\Net\\TcpReceiveWindow.Owner.cs|Net\\TcpReceiveWindow.Owner.cs" or "..\\OS.Kernel\\Net\\TcpSendWindow.cs|Net\\TcpSendWindow.cs" or
            "..\\OS.Kernel\\Net\\TcpSendWindow.Status.cs|Net\\TcpSendWindow.Status.cs" or
            "..\\OS.Kernel\\Net\\TcpSendWindow.Owner.cs|Net\\TcpSendWindow.Owner.cs" or @"..\OS.Kernel\Net\TcpSendWindow.Close.cs|Net\TcpSendWindow.Close.cs" or
            @"..\OS.Kernel\Net\TcpSendWindow.Flight.cs|Net\TcpSendWindow.Flight.cs" or "..\\OS.Kernel\\Net\\TcpTimeWaitTable.cs|Net\\TcpTimeWaitTable.cs" or
            "..\\OS.Kernel\\Net\\TcpTimeWaitTable.Status.cs|Net\\TcpTimeWaitTable.Status.cs" or
            "..\\OS.Kernel\\Net\\TcpTimeWaitTable.Record.cs|Net\\TcpTimeWaitTable.Record.cs" or
            "..\\OS.Kernel\\Net\\TcpTimeWaitTable.Entry.cs|Net\\TcpTimeWaitTable.Entry.cs" or
            "..\\OS.Kernel\\Net\\TcpTimeWaitTable.Owner.cs|Net\\TcpTimeWaitTable.Owner.cs" or
            "..\\OS.Kernel\\Net\\TcpTimeWaitTable.Handle.cs|Net\\TcpTimeWaitTable.Handle.cs" or "..\\..\\std\\no-runtime\\shared\\Net\\InternetChecksum.cs|Std\\Net\\InternetChecksum.cs" or
            "..\\..\\std\\no-runtime\\shared\\Net\\InternetChecksum.InternetChecksumAccumulator.cs|Std\\Net\\InternetChecksum.InternetChecksumAccumulator.cs" => profile.IsEnabled("EnableM07026Probe"),
            @"Boot\EfiEntry.LinuxUserInterrupts.cs|" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs|Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs" => profile.IsEnabled("EnableM16009Probe"),
            "Boot\\EfiEntry.ThreadWait.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.ThreadWaitControl.cs|" or @"..\OS.Kernel\Threading\ThreadWaitProgram.cs|Threading\ThreadWaitProgram.cs" or "..\\OS.Kernel\\Threading\\ThreadLocalStorage.cs|Threading\\ThreadLocalStorage.cs" or
            "..\\OS.Kernel\\Threading\\ThreadLocalStorage.Status.cs|Threading\\ThreadLocalStorage.Status.cs" or
            "..\\OS.Kernel\\Threading\\ThreadLocalStorage.Region.cs|Threading\\ThreadLocalStorage.Region.cs" or
            "..\\OS.Kernel\\Threading\\ThreadLocalStorage.Binding.cs|Threading\\ThreadLocalStorage.Binding.cs" or
            "..\\OS.Kernel\\Threading\\ThreadLocalStorage.Slot.cs|Threading\\ThreadLocalStorage.Slot.cs" or
            "..\\OS.Kernel\\Threading\\ThreadLocalStorage.Result.cs|Threading\\ThreadLocalStorage.Result.cs" => wait,
            "..\\OS.Kernel\\Threading\\WaitAddress.cs|Threading\\WaitAddress.cs" or
            "..\\OS.Kernel\\Threading\\WaitAddress.Status.cs|Threading\\WaitAddress.Status.cs" or
            "..\\OS.Kernel\\Threading\\WaitAddress.Binding.cs|Threading\\WaitAddress.Binding.cs" or
            "..\\OS.Kernel\\Threading\\WaitAddress.Slot.cs|Threading\\WaitAddress.Slot.cs" or
            "..\\OS.Kernel\\Threading\\WaitAddress.Registration.cs|Threading\\WaitAddress.Registration.cs" or
            "..\\OS.Kernel\\Threading\\WaitAddress.Result.cs|Threading\\WaitAddress.Result.cs" => wait || linux,
            "Boot\\EfiEntry.KernelThread.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LifecycleControl.cs|" => wait || profile.IsEnabled("EnableM09006Probe"),
            "..\\OS.Kernel\\Threading\\ReadyQueue.cs|Threading\\ReadyQueue.cs" or
            "..\\OS.Kernel\\Threading\\ReadyQueue.Status.cs|Threading\\ReadyQueue.Status.cs" or
            "..\\OS.Kernel\\Threading\\ReadyQueue.Result.cs|Threading\\ReadyQueue.Result.cs" or @"..\OS.Kernel\Threading\KernelThreadSlot.cs|Threading\KernelThreadSlot.cs" or @"..\OS.Kernel\Threading\KernelThreadWait.cs|Threading\KernelThreadWait.cs" or "..\\OS.Kernel\\Threading\\KernelThreadTable.cs|Threading\\KernelThreadTable.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadTable.Status.cs|Threading\\KernelThreadTable.Status.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadTable.State.cs|Threading\\KernelThreadTable.State.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadTable.WaitKind.cs|Threading\\KernelThreadTable.WaitKind.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadTable.Completion.cs|Threading\\KernelThreadTable.Completion.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadTable.Result.cs|Threading\\KernelThreadTable.Result.cs" or "..\\OS.Kernel\\Threading\\KernelThreadEntry.cs|Threading\\KernelThreadEntry.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadEntry.Plan.cs|Threading\\KernelThreadEntry.Plan.cs" or "..\\OS.Kernel\\Threading\\KernelThreadProgram.cs|Threading\\KernelThreadProgram.cs" or
            "..\\OS.Kernel\\Threading\\KernelThreadProgram.Plan.cs|Threading\\KernelThreadProgram.Plan.cs" => lifecycle || network,
            "..\\OS.Kernel\\Threading\\X64ThreadContext.cs|Threading\\X64ThreadContext.cs" or
            "..\\OS.Kernel\\Threading\\X64ThreadContext.Status.cs|Threading\\X64ThreadContext.Status.cs" or "..\\OS.Kernel\\Threading\\ThreadContextSwitch.cs|Threading\\ThreadContextSwitch.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.Status.cs|Threading\\ThreadContextSwitch.Status.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.ResumeKind.cs|Threading\\ThreadContextSwitch.ResumeKind.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.Owner.cs|Threading\\ThreadContextSwitch.Owner.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.Descriptor.cs|Threading\\ThreadContextSwitch.Descriptor.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.StubPlan.cs|Threading\\ThreadContextSwitch.StubPlan.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.ProgramPlan.cs|Threading\\ThreadContextSwitch.ProgramPlan.cs" or
            "..\\OS.Kernel\\Threading\\ThreadContextSwitch.StubWriter.cs|Threading\\ThreadContextSwitch.StubWriter.cs" => contexts,
            "..\\OS.Kernel\\Threading\\DeadlineQueue.cs|Threading\\DeadlineQueue.cs" or
            "..\\OS.Kernel\\Threading\\DeadlineQueue.Status.cs|Threading\\DeadlineQueue.Status.cs" or
            "..\\OS.Kernel\\Threading\\DeadlineQueue.Entry.cs|Threading\\DeadlineQueue.Entry.cs" or
            "..\\OS.Kernel\\Threading\\DeadlineQueue.Result.cs|Threading\\DeadlineQueue.Result.cs" or
            "..\\OS.Kernel\\Threading\\DeadlineQueue.DrainResult.cs|Threading\\DeadlineQueue.DrainResult.cs" or @"..\OS.Kernel\Pal\OSHostHandle.cs|Pal\OSHostHandle.cs" or "..\\OS.Kernel\\Threading\\SchedulerTimer.cs|Threading\\SchedulerTimer.cs" or
            "..\\OS.Kernel\\Threading\\SchedulerTimer.Status.cs|Threading\\SchedulerTimer.Status.cs" or
            "..\\OS.Kernel\\Threading\\SchedulerTimer.Phase.cs|Threading\\SchedulerTimer.Phase.cs" or
            "..\\OS.Kernel\\Threading\\SchedulerTimer.Action.cs|Threading\\SchedulerTimer.Action.cs" or
            "..\\OS.Kernel\\Threading\\SchedulerTimer.Result.cs|Threading\\SchedulerTimer.Result.cs" => timer || network,
            "..\\OS.Kernel\\Threading\\X64TimerInterrupt.cs|Threading\\X64TimerInterrupt.cs" or
            "..\\OS.Kernel\\Threading\\X64TimerInterrupt.WorkerPlan.cs|Threading\\X64TimerInterrupt.WorkerPlan.cs" or
            "..\\OS.Kernel\\Threading\\X64TimerInterrupt.Frame.cs|Threading\\X64TimerInterrupt.Frame.cs" or
            "..\\OS.Kernel\\Threading\\X64TimerInterrupt.Plan.cs|Threading\\X64TimerInterrupt.Plan.cs" or
            "..\\OS.Kernel\\Threading\\X64TimerInterrupt.Writer.cs|Threading\\X64TimerInterrupt.Writer.cs" => timer,
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalMask.cs|Compatibility\\LinuxX64\\LinuxSignalMask.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalMask.Result.cs|Compatibility\\LinuxX64\\LinuxSignalMask.Result.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.cs|Compatibility\\LinuxX64\\LinuxSignalServices.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.Disposition.cs|Compatibility\\LinuxX64\\LinuxSignalServices.Disposition.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.AlternateStack.cs|Compatibility\\LinuxX64\\LinuxSignalServices.AlternateStack.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.State.cs|Compatibility\\LinuxX64\\LinuxSignalServices.State.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.Admission.cs|Compatibility\\LinuxX64\\LinuxSignalServices.Admission.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.ThreadState.cs|Compatibility\\LinuxX64\\LinuxSignalServices.ThreadState.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.Frames.cs|Compatibility\\LinuxX64\\LinuxSignalServices.Frames.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxSignalServices.Delivery.cs|Compatibility\\LinuxX64\\LinuxSignalServices.Delivery.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxSignalServices.Pending.cs|Compatibility\LinuxX64\LinuxSignalServices.Pending.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.cs|Compatibility\\LinuxX64\\LinuxThreadServices.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.Worker.cs|Compatibility\\LinuxX64\\LinuxThreadServices.Worker.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.Slot.cs|Compatibility\\LinuxX64\\LinuxThreadServices.Slot.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.Admission.cs|Compatibility\\LinuxX64\\LinuxThreadServices.Admission.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.View.cs|Compatibility\\LinuxX64\\LinuxThreadServices.View.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Signals.cs|Compatibility\LinuxX64\LinuxThreadServices.Signals.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Interrupts.cs|Compatibility\LinuxX64\LinuxThreadServices.Interrupts.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.Futex.cs|Compatibility\\LinuxX64\\LinuxThreadServices.Futex.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.FutexCell.cs|Compatibility\\LinuxX64\\LinuxThreadServices.FutexCell.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadServices.FutexAdmission.cs|Compatibility\\LinuxX64\\LinuxThreadServices.FutexAdmission.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Requeue.cs|Compatibility\LinuxX64\LinuxThreadServices.Requeue.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadExecutionBudget.cs|Compatibility\LinuxX64\LinuxThreadExecutionBudget.cs" or @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs|Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.Pipes.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.Pipes.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxDescriptorServices.PipeOperation.cs|Compatibility\\LinuxX64\\LinuxDescriptorServices.PipeOperation.cs" or "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadProgram.cs|Compatibility\\LinuxX64\\LinuxThreadProgram.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadProgram.Binding.cs|Compatibility\\LinuxX64\\LinuxThreadProgram.Binding.cs" or
            "..\\OS.Kernel\\Compatibility\\LinuxX64\\LinuxThreadProgram.Plan.cs|Compatibility\\LinuxX64\\LinuxThreadProgram.Plan.cs" or "Boot\\EfiEntry.LinuxThreads.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxNativeControl.cs|" or
            "Boot\\EfiEntry.M03FirstExitAdapter.LinuxQuotaDisposition.cs|" or @"Boot\EfiEntry.LinuxSignals.cs|" or @"Boot\EfiEntry.LinuxThreadMemory.cs|" or @"Boot\EfiEntry.LinuxThreadRun.cs|" or @"Boot\EfiEntry.LinuxThreadTimer.cs|" => linux,
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
        foreach (string workload in new[]
        {
            "SingleMinimalApi",
            "MultiMinimalApi"
        }

        )
        {
            var properties = Isolation(root, sdk, temp);
            properties["Configuration"] = configuration;
            properties["OSWorkloadProfile"] = workload;
            properties["ClrosBootPolicy"] = profile.BootPolicy;
            foreach (var pair in profile.Flags)
                properties[pair.Key] = pair.Value ? "true" : "false";
            string[] requested = ["Configuration", "ClrosBootPolicy", "DefineConstants", "EnableDefaultCompileItems", "NoStdLib", "DisableImplicitFrameworkReferences", .. profile.Flags.Keys, .. IsolationKeys];
            var evaluation = Query(root, dotnet, project, properties, requested, "Compile,ProjectReference,Reference,Analyzer,_ClrosX64SerializerGeneratorInput", "evaluated_project", timeoutSeconds, outputCharacters);
            var values = Object(evaluation["Properties"], "evaluated_project_malformed");
            var items = Object(evaluation["Items"], "evaluated_project_malformed");
            Require(Text(values["Configuration"]) == configuration && Text(values["EnableDefaultCompileItems"]) == "false" && Text(values["NoStdLib"]) == "true" && Text(values["DisableImplicitFrameworkReferences"]) == "true", "evaluated_property_mismatch", "nostdlib");
            foreach (string key in IsolationKeys.Concat(profile.Flags.Keys).Append("ClrosBootPolicy"))
                Require(Text(values[key]) == properties[key], "evaluated_property_mismatch", workload + ":" + key);
            var expectedSymbols = ExpectedSymbols(configuration, workload, profile);
            var symbols = Text(values["DefineConstants"]).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            Require(symbols.SetEquals(expectedSymbols), "evaluated_define_constants_mismatch", workload);
            JsonArray compile = Array(items["Compile"], "evaluated_item_count_mismatch"), analyzers = Array(items["Analyzer"], "evaluated_item_count_mismatch"), inputs = Array(items["_ClrosX64SerializerGeneratorInput"], "evaluated_item_count_mismatch");
            string[] expectedInputs = EfiSerializerInputs.BuildInputs.ToArray();
            Require(compile.Count == expectedCompileItems.Length && Array(items["ProjectReference"], "evaluated_item_count_mismatch").Count == 0 && Array(items["Reference"], "evaluated_item_count_mismatch").Count == 0 && analyzers.Count == 2 && inputs.Count == expectedInputs.Length, "evaluated_item_count_mismatch");
            VerifyAnalyzers(analyzers, "evaluated_analyzer_mismatch");
            for (int i = 0; i < compile.Count; i++)
            {
                string include = expectedCompileItems[i].Split('|')[0];
                var item = compile[i]!;
                Require(Text(item["Identity"]) + "|" + (item["Link"] is null ? "" : Text(item["Link"])) == expectedCompileItems[i] && SamePath(Text(item["FullPath"]), Path.Combine(root, "src/OS", include)) && SamePath(Text(item["DefiningProjectFullPath"]), project), "evaluated_compile_item_mismatch", workload + ":" + i);
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                string expected = Path.Combine(serializerDirectory, expectedInputs[i]);
                Require(SamePath(Text(inputs[i]!["Identity"]), expected) && SamePath(Text(inputs[i]!["FullPath"]), expected) && SamePath(Text(inputs[i]!["DefiningProjectFullPath"]), targets), "evaluated_serializer_input_mismatch", workload + ":" + i);
            }
        }

        string generatorTemp = Path.Combine(Path.GetTempPath(), "clros-generator-evaluation-" + Guid.NewGuid().ToString("N"));
        var generatorProperties = Isolation(root, sdk, generatorTemp);
        generatorProperties["Configuration"] = configuration;
        generatorProperties["BaseOutputPath"] = Path.Combine(serializerDirectory, "obj/clros-m01-034-source-closure/bin").Replace('\\', '/') + "/";
        generatorProperties["BaseIntermediateOutputPath"] = Path.Combine(serializerDirectory, "obj/clros-m01-034-source-closure/obj").Replace('\\', '/') + "/";
        generatorProperties.Remove("IntermediateOutputPath");
        generatorProperties["DefaultItemExcludes"] = "obj/**";
        generatorProperties["DefaultExcludesInProjectFolder"] = "bin/**";
        generatorProperties["CustomBeforeMicrosoftCommonTargets"] = targets;
        generatorProperties["DirectoryBuildPropsPath"] = Path.Combine(root, "tools/Directory.Build.props");
        var generator = Query(root, dotnet, Path.Combine(serializerDirectory, "OS.X64TextSerializerGenerator.csproj"), generatorProperties, ["EnableDefaultCompileItems", .. IsolationKeys], "Compile,ProjectReference,Reference,PackageReference,Analyzer", "serializer_generator_evaluation", timeoutSeconds, outputCharacters);
        var generatorValues = Object(generator["Properties"], "serializer_generator_evaluation_malformed");
        var generatorItems = Object(generator["Items"], "serializer_generator_evaluation_malformed");
        foreach (string key in IsolationKeys)
            Require(Text(generatorValues[key]) == generatorProperties[key], "serializer_generator_property_mismatch", key);
        var generatorCompile = Array(generatorItems["Compile"], "serializer_generator_compile_count_mismatch");
        string[] expectedGenerator = EfiSerializerInputs.CompileItems.ToArray();
        Require(Text(generatorValues["EnableDefaultCompileItems"]) == "true" && generatorCompile.Count == expectedGenerator.Length && new[] { "ProjectReference", "Reference", "PackageReference" }.All(k => Array(generatorItems[k], "serializer_generator_compile_count_mismatch").Count == 0) && Array(generatorItems["Analyzer"], "serializer_generator_compile_count_mismatch").Count == 2, "serializer_generator_compile_count_mismatch");
        for (int i = 0; i < generatorCompile.Count; i++)
            Require(Text(generatorCompile[i]!["Identity"]) == expectedGenerator[i] && SamePath(Text(generatorCompile[i]!["FullPath"]), Path.Combine(serializerDirectory, expectedGenerator[i])) && Path.GetFileName(Text(generatorCompile[i]!["DefiningProjectFullPath"])) == "Microsoft.NET.Sdk.DefaultItems.props", "serializer_generator_compile_item_mismatch", i.ToString());
        VerifyAnalyzers(Array(generatorItems["Analyzer"], "serializer_generator_analyzer_mismatch"), "serializer_generator_analyzer_mismatch", true);
    }

    internal static HashSet<string> ExpectedSymbols(string configuration, string workload, EfiProfile profile)
    {
        HashSet<string> symbols = new(["TRACE", "NOSTDLIB_EFI", configuration == "Debug" ? "DEBUG" : "RELEASE",
            workload == "SingleMinimalApi" ? "OS_PROFILE_SINGLE_MINIMAL_API" : "OS_PROFILE_MULTI_MINIMAL_API"], StringComparer.Ordinal);
        if (profile.BootPolicy == "VerifiedExit")
            symbols.Add("CLROS_NORMAL_VERIFIED_EXIT");
        foreach (var pair in EfiContracts.ProbeSymbols)
            if (profile.IsEnabled(pair.Key))
                symbols.Add(pair.Value);
        if (profile.IsEnabled("EnableM16011Probe"))
            symbols.Add("CLROS_LINUX_SOCKETS");
        return symbols;
    }
}
