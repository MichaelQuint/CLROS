using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

internal static class M01InstructionPublicationBuildIntegrationTests
{
    private const string AggregateName = "m01.034";
    private const string SerializerSymbol = OS.Tooling.EfiContracts.SerializerSymbol;
    private const string SerializerObjectName =
        "CLROS.X64.InstructionPublication.obj";
    private const int SerializerObjectBytes = OS.Tooling.EfiContracts.SerializerObjectBytes;
    private const uint ExpectedSuccessCookie = OS.Tooling.EfiContracts.SerializerSuccessCookie;
    private static readonly string SerializerObjectSha256 =
        OS.Tooling.EfiContracts.SerializerObjectSha256.ToUpperInvariant();

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        HostCheck.Run(AggregateName, () =>
        {

            try
            {
                Execute();
                Console.WriteLine(
                    "CLROS_M01_034_READY objectBytes=" +
                    SerializerObjectBytes.ToString(CultureInfo.InvariantCulture) +
                    " objectSha256=" +
                    SerializerObjectSha256 +
                    " successCookie=0x" +
                    ExpectedSuccessCookie.ToString("X8", CultureInfo.InvariantCulture));
            }
            catch (M01BuildIntegrationFailureException ex)
            {
                throw new InvalidOperationException(
                    "M01-034 build integration tests failed: " + ex.Code,
                    ex);
            }
            catch (Exception ex)
            {
                string code = "unexpected-" + ex.GetType().Name;
                throw new InvalidOperationException(
                    "M01-034 build integration tests failed: " + code,
                    ex);
            }
        });
    }

    private static void Execute()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string solutionPath = Path.Combine(repositoryRoot, "CLROS.slnx");
        string osProjectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        string targetsPath = Path.Combine(
            repositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator",
            "OS.X64TextSerializerGenerator.targets");

        ValidateSolutionMembership(solutionPath);
        ValidateProjectAndTargets(osProjectPath, targetsPath);
        ValidatePublicationSource(repositoryRoot);
        BuildAndValidateManagedBinding(repositoryRoot, osProjectPath);
        ValidateBuildTargetExecution(repositoryRoot, osProjectPath);
    }

    private static void ValidateSolutionMembership(string solutionPath)
    {
        Require(File.Exists(solutionPath), "solution-missing");
        XmlDocument solution = LoadSecureXml(solutionPath);
        XmlNodeList projects = solution.SelectNodes("/Solution//Project")
            ?? throw new M01BuildIntegrationFailureException(
                "solution-project-query-failed");
        int matches = 0;
        foreach (XmlElement project in projects.OfType<XmlElement>())
        {
            if (string.Equals(
                    project.GetAttribute("Path").Replace('\\', '/'),
                    "tools/OS.X64TextSerializerGenerator/" +
                    "OS.X64TextSerializerGenerator.csproj",
                    StringComparison.Ordinal))
            {
                matches++;
            }
        }

        Require(matches == 1, "solution-generator-membership-count");
    }

    private static void ValidateProjectAndTargets(
        string osProjectPath,
        string targetsPath)
    {
        Require(File.Exists(osProjectPath), "os-project-missing");
        Require(File.Exists(targetsPath), "serializer-targets-missing");

        XmlDocument project = LoadSecureXml(osProjectPath);
        XmlNodeList projectReferences = project.SelectNodes("//ProjectReference")
            ?? throw new M01BuildIntegrationFailureException(
                "project-reference-query-failed");
        Require(projectReferences.Count == 0, "os-project-reference-present");
        ValidatePinnedNativeAotProjectRoots(project);

        XmlNodeList imports = project.SelectNodes("/Project/Import")
            ?? throw new M01BuildIntegrationFailureException(
                "project-import-query-failed");
        Require(imports.Count == 1, "serializer-import-count");
        XmlElement import = RequireElement(imports[0], "serializer-import-shape");
        Require(
            import.Attributes.Count == 1 &&
            import.GetAttribute("Project") ==
                @"..\..\tools\OS.X64TextSerializerGenerator\" +
                "OS.X64TextSerializerGenerator.targets",
            "serializer-import-contract");

        string[] requiredCompileItems =
        [
            @"Boot\X64InstructionPublication.cs",
            @"Runtime\RuntimeImportAttribute.cs",
            @"Runtime\MethodImplAttribute.cs"
        ];
        XmlNodeList compileNodes = project.SelectNodes("/Project/ItemGroup/Compile")
            ?? throw new M01BuildIntegrationFailureException(
                "compile-query-failed");
        foreach (string requiredCompileItem in requiredCompileItems)
        {
            int count = compileNodes
                .OfType<XmlElement>()
                .Count(node =>
                    string.Equals(
                        node.GetAttribute("Include"),
                        requiredCompileItem,
                        StringComparison.Ordinal) &&
                    !node.HasAttribute("Condition") &&
                    node.ChildNodes.Count == 0);
            Require(count == 1, "compile-item-" + StableCode(requiredCompileItem));
        }

        XmlNodeList projectLinkerArguments =
            project.SelectNodes("/Project/Target/ItemGroup/LinkerArg")
            ?? throw new M01BuildIntegrationFailureException(
                "project-linker-argument-query-failed");
        Require(
            projectLinkerArguments
                .OfType<XmlElement>()
                .Count(node =>
                    string.Equals(
                        node.GetAttribute("Include"),
                        "/BREPRO",
                        StringComparison.Ordinal)) == 1,
            "project-brepro-count");
        Require(
            !projectLinkerArguments
                .OfType<XmlElement>()
                .Any(node =>
                    node.GetAttribute("Include").StartsWith(
                        "/FORCE",
                        StringComparison.OrdinalIgnoreCase)),
            "project-force-linker-option");

        XmlDocument targets = LoadSecureXml(targetsPath);
        XmlNodeList integrationTargets = targets.SelectNodes(
                "/Project/Target[@Name=" +
                "'IntegrateClrosX64InstructionPublicationSerializer']")
            ?? throw new M01BuildIntegrationFailureException(
                "integration-target-query-failed");
        Require(integrationTargets.Count == 1, "integration-target-count");
        XmlElement integration = RequireElement(
            integrationTargets[0],
            "integration-target-shape");
        Require(
            integration.GetAttribute("AfterTargets") ==
                "RemoveNativeAotRuntimeLibraries",
            "integration-target-after");
        Require(
            integration.GetAttribute("BeforeTargets") == "LinkNative",
            "integration-target-before");
        Require(
            !integration.HasAttribute("DependsOnTargets") &&
            !integration.HasAttribute("Inputs") &&
            !integration.HasAttribute("Outputs"),
            "integration-target-scheduling");

        XmlNodeList closureTargets = targets.SelectNodes(
                "/Project/Target[@Name=" +
                "'ValidateClrosX64SerializerGeneratorCompileClosure']")
            ?? throw new M01BuildIntegrationFailureException(
                "closure-target-query-failed");
        Require(closureTargets.Count == 1, "closure-target-count");
        XmlElement closure = RequireElement(
            closureTargets[0],
            "closure-target-shape");
        Require(
            closure.GetAttribute("BeforeTargets") == "PrepareForBuild" &&
            closure.GetAttribute("Condition").Contains(
                "OS.X64TextSerializerGenerator.csproj",
                StringComparison.Ordinal),
            "closure-target-contract");

        Require(
            targets.SelectNodes("//ProjectReference")?.Count == 0,
            "targets-project-reference-present");
        Require(
            targets.SelectNodes("//WriteLinesToFile|//Copy|//Delete")
                ?.Count == 0,
            "targets-destructive-task-present");
        Require(
            targets.SelectNodes(
                "//FileWrites[@Include=" +
                "'$(_ClrosX64SerializerTrackedObject)']")?.Count == 1,
            "targets-filewrites-missing");
        Require(
            targets.SelectNodes("//RemoveDir")?.Count == 1 &&
            targets.SelectNodes(
                "/Project/Target[@Name=" +
                "'TrackClrosX64InstructionPublicationSerializerOutput' and " +
                "@BeforeTargets='_CleanGetCurrentAndPriorFileWrites']")?.Count == 1,
            "targets-clean-tracking-target");
        Require(
            targets.SelectNodes(
                "/Project/Target[@Name=" +
                "'RemoveClrosX64InstructionPublicationSerializerToolOutput' and " +
                "@AfterTargets='CoreClean' and " +
                "@DependsOnTargets=" +
                "'TrackClrosX64InstructionPublicationSerializerOutput']/" +
                "RemoveDir[@Directories=" +
                "'$(_ClrosX64SerializerTrackedToolRoot)']")?.Count == 1,
            "targets-clean-tool-root");
        XmlElement childBuild = RequireElement(
            targets.SelectSingleNode(
                "/Project/Target[@Name=" +
                "'IntegrateClrosX64InstructionPublicationSerializer']/MSBuild"),
            "targets-child-build-missing");
        string childProperties = childBuild.GetAttribute("Properties");
        string removedProperties = childBuild.GetAttribute("RemoveProperties");
        string[] removedPropertyNames = removedProperties.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries);
        Require(
            removedPropertyNames.Length ==
                removedPropertyNames.Distinct(StringComparer.Ordinal).Count(),
            "targets-child-remove-duplicate");
        string[] parentAotPackageAndRestoreProperties =
        [
            "RuntimeIdentifier",
            "RuntimeIdentifiers",
            "SelfContained",
            "PublishAot",
            "PublishReadyToRun",
            "PublishSingleFile",
            "PublishTrimmed",
            "NoStdLib",
            "NoConfig",
            "DisableImplicitFrameworkReferences",
            "EnableDefaultCompileItems",
            "IlcUseEnvironmentalTools",
            "CppLinker",
            "ILCompilerTargetsPath",
            "ILLinkTargetsPath",
            "ILLinkTasksAssembly",
            "IlcBuildTasksPath",
            "IlcHostPackagePath",
            "ClrosIlcHostPackagePath",
            "IlcToolsPath",
            "RuntimePackagePath",
            "ClrosRuntimePackagePath",
            "IlcFrameworkPath",
            "IlcFrameworkNativePath",
            "IlcSdkPath",
            "IlcMibcPath",
            "IlcGenerateMapFile",
            "IlcPgoOptimize",
            "IlcMultiModule",
            "NativeCompilationDuringPublish",
            "PublishAotUsingRuntimePack",
            "DisableFrameworkLibGeneration",
            "ImportRuntimeIlcPackageTargetDependsOn",
            "IlcSetupPropertiesDependsOn",
            "IlcDynamicBuildPropertyDependencies",
            "RestorePackagesPath",
            "NuGetPackageRoot",
            "ExcludeRestorePackageImports"
        ];
        foreach (string removedProperty in parentAotPackageAndRestoreProperties)
        {
            Require(
                removedPropertyNames.Contains(
                    removedProperty,
                    StringComparer.Ordinal),
                "targets-child-remove-" + StableCode(removedProperty));
        }

        string[] parentMsBuildInjectionProperties =
        [
            "MSBuildSDKsPath",
            "MSBuildExtensionsPath",
            "MSBuildUserExtensionsPath",
            "UseSharedCompilation",
            "ImportByWildcardBeforeMicrosoftCommonProps",
            "ImportByWildcardAfterMicrosoftCommonProps",
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps",
            "ImportUserLocationsByWildcardAfterMicrosoftCommonProps",
            "ImportByWildcardBeforeMicrosoftCommonTargets",
            "ImportByWildcardAfterMicrosoftCommonTargets",
            "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets",
            "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets",
            "ImportByWildcardBeforeMicrosoftCSharpTargets",
            "ImportByWildcardAfterMicrosoftCSharpTargets",
            "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets",
            "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets",
            "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets",
            "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets"
        ];
        foreach (string canonicalProperty in parentMsBuildInjectionProperties)
        {
            Require(
                !removedPropertyNames.Contains(
                    canonicalProperty,
                    StringComparer.Ordinal),
                "targets-child-canonical-removed-" +
                    StableCode(canonicalProperty));
        }

        IReadOnlyDictionary<string, string> childPropertyMap =
            ParseMsBuildTaskProperties(childProperties);
        IReadOnlyDictionary<string, string> canonicalChildRestoreProperties =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MSBuildProjectExtensionsPath"] =
                    "$(_ClrosX64SerializerToolIntermediate)",
                ["RestoreOutputPath"] =
                    "$(_ClrosX64SerializerToolIntermediate)",
                ["ProjectAssetsFile"] =
                    "$(_ClrosX64SerializerToolAssetsFile)",
                ["ImportProjectExtensionProps"] = "true",
                ["ImportProjectExtensionTargets"] = "true",
                ["RestoreUseCustomAfterTargets"] = "false",
                ["RestoreForceEvaluate"] = "true",
                ["RestoreIgnoreFailedSources"] = "true",
                ["MSBuildSDKsPath"] = @"$(MSBuildToolsPath)\Sdks",
                ["MSBuildExtensionsPath"] = @"$(MSBuildToolsPath)\",
                ["MSBuildUserExtensionsPath"] =
                    "$(_ClrosX64SerializerToolDisabledUserExtensions)",
                ["UseSharedCompilation"] = "false",
                ["ImportByWildcardBeforeMicrosoftCommonProps"] = "false",
                ["ImportByWildcardAfterMicrosoftCommonProps"] = "false",
                ["ImportUserLocationsByWildcardBeforeMicrosoftCommonProps"] =
                    "false",
                ["ImportUserLocationsByWildcardAfterMicrosoftCommonProps"] =
                    "false",
                ["ImportByWildcardBeforeMicrosoftCommonTargets"] = "false",
                ["ImportByWildcardAfterMicrosoftCommonTargets"] = "false",
                ["ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets"] =
                    "false",
                ["ImportUserLocationsByWildcardAfterMicrosoftCommonTargets"] =
                    "false",
                ["ImportByWildcardBeforeMicrosoftCSharpTargets"] = "false",
                ["ImportByWildcardAfterMicrosoftCSharpTargets"] = "false",
                ["ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets"] =
                    "false",
                ["ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets"] =
                    "false",
                ["ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets"] =
                    "false",
                ["ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets"] =
                    "false"
            };
        foreach ((string propertyName, string expectedValue) in
                 canonicalChildRestoreProperties)
        {
            Require(
                childPropertyMap.TryGetValue(
                    propertyName,
                    out string? actualValue) &&
                actualValue == expectedValue,
                "targets-child-canonical-" + StableCode(propertyName));
            Require(
                !removedPropertyNames.Contains(
                    propertyName,
                    StringComparer.Ordinal),
                "targets-child-canonical-removed-" +
                    StableCode(propertyName));
        }
        foreach (string isolatedProperty in
                 parentAotPackageAndRestoreProperties.Except(
                     canonicalChildRestoreProperties.Keys,
                     StringComparer.Ordinal))
        {
            Require(
                !childPropertyMap.ContainsKey(isolatedProperty),
                "targets-child-reintroduced-" +
                    StableCode(isolatedProperty));
        }

        foreach (string requiredProperty in new[]
                 {
                     "BaseOutputPath=",
                     "BaseIntermediateOutputPath=",
                     "DirectoryBuildPropsPath=",
                     "DirectoryBuildTargetsPath=",
                     "MSBuildProjectExtensionsPath=",
                     "RestoreOutputPath=",
                     "ProjectAssetsFile=",
                     "CustomBeforeMicrosoftCommonTargets=",
                     "CustomAfterMicrosoftCommonTargets=",
                     "CustomBeforeMicrosoftCommonProps=",
                     "CustomAfterMicrosoftCommonProps=",
                     "CustomBeforeDirectoryBuildProps=",
                     "CustomAfterDirectoryBuildProps=",
                     "CustomBeforeDirectoryBuildTargets=",
                     "CustomAfterDirectoryBuildTargets=",
                     "CustomBeforeMicrosoftCSharpTargets=",
                     "CustomAfterMicrosoftCSharpTargets=",
                     "CustomBeforeMicrosoftCommonCrossTargetingTargets=",
                     "CustomAfterMicrosoftCommonCrossTargetingTargets=",
                     "BeforeMicrosoftNETSdkTargets=",
                     "AfterMicrosoftNETSdkTargets=",
                     "LanguageTargets=",
                     "CSharpTargetsPath=",
                     "CommonTargetsPath=",
                     "ImportNuGetBuildTasksPackTargetsFromSdk=false",
                     "NuGetBuildTasksPackTargets="
                 })
        {
            Require(
                childProperties.Contains(requiredProperty, StringComparison.Ordinal),
                "targets-child-property-" + StableCode(requiredProperty));
        }
        foreach (string removedProperty in new[]
                 {
                     "BuildDependsOn",
                     "CoreBuildDependsOn",
                     "CompileDependsOn",
                     "CoreCompileDependsOn",
                     "PrepareForBuildDependsOn",
                     "RebuildDependsOn",
                     "SkipCompilerExecution",
                     "NoBuild",
                     "DesignTimeBuild",
                     "CscToolPath",
                     "CscToolExe",
                     "RoslynTargetsPath"
                 })
        {
            Require(
                removedPropertyNames.Contains(
                    removedProperty,
                    StringComparer.Ordinal),
                "targets-child-remove-" + StableCode(removedProperty));
        }
        Require(
            targets.SelectNodes(
                "//LinkerArg[@Include=" +
                "'/INCLUDE:$(ClrosX64InstructionPublicationSymbol)']")?.Count == 1,
            "targets-include-argument-missing");
        Require(
            targets.SelectNodes(
                "//GetFileHash/Output[@TaskParameter='Hash' and " +
                "@PropertyName='_ClrosX64SerializerActualSha256']")?.Count == 1,
            "targets-hash-output-contract");
    }

    private static void ValidatePinnedNativeAotProjectRoots(
        XmlDocument project)
    {
        (string Name, string Condition, string Value)[] pinnedProperties =
        [
            (
                "ClrosIlcHostPackagePath",
                "'$(ClrosIlcHostPackagePath)' == ''",
                @"$(UserProfile)\.nuget\packages\" +
                    @"runtime.win-x64.microsoft.dotnet.ilcompiler\" + OS.Tooling.EfiContracts.NativePackageVersion),
            (
                "ClrosRuntimePackagePath",
                "'$(ClrosRuntimePackagePath)' == ''",
                @"$(UserProfile)\.nuget\packages\" +
                    @"microsoft.netcore.app.runtime.nativeaot.win-x64\" + OS.Tooling.EfiContracts.NativePackageVersion)
        ];
        foreach ((string name, string condition, string value) in
                 pinnedProperties)
        {
            XmlNodeList nodes = project.SelectNodes(
                    "/Project/PropertyGroup/" + name)
                ?? throw new M01BuildIntegrationFailureException(
                    "pinned-property-query-" + StableCode(name));
            Require(nodes.Count == 1, "pinned-property-count-" + StableCode(name));
            XmlElement node = RequireElement(
                nodes[0],
                "pinned-property-shape-" + StableCode(name));
            Require(
                node.Attributes.Count == 1 &&
                node.GetAttribute("Condition") == condition &&
                node.InnerText == value &&
                node.ChildNodes.Count == 1 &&
                node.FirstChild?.NodeType == XmlNodeType.Text,
                "pinned-property-contract-" + StableCode(name));
        }

        (
            string ItemName,
            string Include,
            string PropertyName)[] resolvedPacks =
        [
            (
                "ResolvedILCompilerPack",
                "runtime.win-x64.Microsoft.DotNet.ILCompiler",
                "ClrosIlcHostPackagePath"),
            (
                "ResolvedTargetILCompilerPack",
                "Microsoft.NETCore.App.Runtime.NativeAOT.win-x64",
                "ClrosRuntimePackagePath")
        ];
        foreach ((string itemName, string include, string propertyName) in
                 resolvedPacks)
        {
            XmlNodeList nodes = project.SelectNodes(
                    "/Project/ItemGroup/" + itemName)
                ?? throw new M01BuildIntegrationFailureException(
                    "resolved-pack-query-" + StableCode(itemName));
            Require(nodes.Count == 1, "resolved-pack-count-" + StableCode(itemName));
            XmlElement node = RequireElement(
                nodes[0],
                "resolved-pack-shape-" + StableCode(itemName));
            XmlElement[] children = node.ChildNodes
                .OfType<XmlElement>()
                .ToArray();
            Require(
                node.Attributes.Count == 1 &&
                node.GetAttribute("Include") == include &&
                children.Length == 1 &&
                children[0].Name == "PackageDirectory" &&
                children[0].Attributes.Count == 0 &&
                children[0].InnerText == "$(" + propertyName + ")",
                "resolved-pack-contract-" + StableCode(itemName));
        }

        XmlNodeList validationTargets = project.SelectNodes(
                "/Project/Target[@Name='ValidateClrosPinnedNativeAotPaths']")
            ?? throw new M01BuildIntegrationFailureException(
                "pinned-validation-query-failed");
        Require(validationTargets.Count == 1, "pinned-validation-count");
        XmlElement validationTarget = RequireElement(
            validationTargets[0],
            "pinned-validation-shape");
        Require(
            validationTarget.Attributes.Count == 2 &&
            validationTarget.GetAttribute("AfterTargets") ==
                "ImportRuntimeIlcPackageTarget",
            "pinned-validation-scheduling");
        XmlElement[] errors = validationTarget.ChildNodes
            .OfType<XmlElement>()
            .Where(node => node.Name == "Error")
            .ToArray();
        Require(errors.Length == 2, "pinned-validation-error-count");
        Require(
            errors.Any(error =>
                error.Attributes.Count == 2 &&
                error.GetAttribute("Condition") ==
                    "'$(IlcHostPackagePath)' != " +
                    "'$(ClrosIlcHostPackagePath)'" &&
                error.GetAttribute("Text") ==
                    "NativeAOT host package path escaped the CLROS pinned " +
                    "package root."),
            "pinned-validation-host");
        Require(
            errors.Any(error =>
                error.Attributes.Count == 2 &&
                error.GetAttribute("Condition") ==
                    "'$(RuntimePackagePath)' != " +
                    "'$(ClrosRuntimePackagePath)'" &&
                error.GetAttribute("Text") ==
                    "NativeAOT runtime package path escaped the CLROS pinned " +
                    "package root."),
            "pinned-validation-runtime");
    }

    private static IReadOnlyDictionary<string, string>
        ParseMsBuildTaskProperties(string value)
    {
        Dictionary<string, string> properties =
            new(StringComparer.Ordinal);
        foreach (string assignment in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = assignment.IndexOf(
                '=',
                StringComparison.Ordinal);
            Require(separator > 0, "targets-child-property-shape");
            string name = assignment[..separator];
            string propertyValue = assignment[(separator + 1)..];
            Require(
                properties.TryAdd(name, propertyValue),
                "targets-child-property-duplicate-" + StableCode(name));
        }

        return properties;
    }

    private static void ValidatePublicationSource(string repositoryRoot)
    {
        string publicationPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "X64InstructionPublication.cs");
        string runtimeImportPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Runtime",
            "RuntimeImportAttribute.cs");
        string methodImplPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Runtime",
            "MethodImplAttribute.cs");

        string publication = File.ReadAllText(publicationPath);
        string runtimeImport = File.ReadAllText(runtimeImportPath);
        string methodImpl = File.ReadAllText(methodImplPath);
        Require(
            publication.Contains(SerializerSymbol, StringComparison.Ordinal),
            "publication-symbol-source-missing");
        Require(
            publication.Contains("ExpectedSuccessCookie", StringComparison.Ordinal) &&
            publication.Contains("0x53524C43u", StringComparison.Ordinal),
            "publication-cookie-source-missing");
        foreach (string forbidden in new[]
                 {
                     "DllImport",
                     "LibraryImport",
                     "Marshal.",
                     "delegate*",
                     "unsafe",
                     "LoadLibrary",
                     "GetProcAddress"
                 })
        {
            Require(
                !publication.Contains(forbidden, StringComparison.Ordinal) &&
                !runtimeImport.Contains(forbidden, StringComparison.Ordinal) &&
                !methodImpl.Contains(forbidden, StringComparison.Ordinal),
                "binding-forbidden-" + StableCode(forbidden));
        }
    }

    private static void BuildAndValidateManagedBinding(
        string repositoryRoot,
        string osProjectPath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "clros m01-034 managed binding " +
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            string baseIntermediatePath = Path.Combine(
                temporaryRoot,
                "managed obj");
            string baseOutputPath = Path.Combine(
                temporaryRoot,
                "managed bin");
            string assemblyPath = Path.Combine(
                baseOutputPath,
                "Release",
                "net11.0",
                "win-x64",
                "OS.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
            byte[] staleAssembly = "CLROS_M01_034_STALE_MANAGED_ASSEMBLY"u8.ToArray();
            File.WriteAllBytes(assemblyPath, staleAssembly);
            File.SetLastWriteTimeUtc(
                assemblyPath,
                DateTime.UtcNow.AddDays(7));

            string normalizedBaseIntermediate =
                Path.GetFullPath(baseIntermediatePath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                "/";
            string normalizedBaseOutput =
                Path.GetFullPath(baseOutputPath)
                    .Replace(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                "/";
            ProcessResult build = RunDotnet(
                repositoryRoot,
                timeoutMilliseconds: 120_000,
                "build",
                osProjectPath,
                "--nologo",
                "--configuration",
                "Release",
                "-m:1",
                "-p:NoIncremental=true",
                "-p:BaseIntermediateOutputPath=" +
                    normalizedBaseIntermediate,
                "-p:MSBuildProjectExtensionsPath=" +
                    normalizedBaseIntermediate,
                "-p:RestoreOutputPath=" +
                    normalizedBaseIntermediate,
                "-p:ProjectAssetsFile=" +
                    normalizedBaseIntermediate +
                    "project.assets.json",
                "-p:BaseOutputPath=" +
                    normalizedBaseOutput,
                "-p:RestoreIgnoreFailedSources=true");
            RequireProcessSuccess(build, "managed-binding-build");
            Require(
                File.Exists(assemblyPath),
                "managed-binding-assembly-missing");
            Require(
                !File.ReadAllBytes(assemblyPath).SequenceEqual(staleAssembly),
                "managed-binding-stale-output-reused");
            Require(
                File.GetLastWriteTimeUtc(assemblyPath) < DateTime.UtcNow.AddDays(1),
                "managed-binding-future-timestamp-preserved");

            ValidateManagedBindingAssembly(assemblyPath);
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
        }
    }

    private static void ValidateManagedBindingAssembly(string assemblyPath)
    {

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new PEReader(stream);
        Require(peReader.HasMetadata, "managed-binding-metadata-missing");
        MetadataReader reader = peReader.GetMetadataReader();
        Require(reader.AssemblyReferences.Count == 0, "managed-binding-reference");

        TypeDefinitionHandle publicationType = FindType(
            reader,
            "OS.Boot",
            "X64InstructionPublication");
        TypeDefinition publication = reader.GetTypeDefinition(publicationType);
        Require(
            (publication.Attributes & TypeAttributes.Abstract) != 0 &&
            (publication.Attributes & TypeAttributes.Sealed) != 0,
            "publication-type-not-static");

        MethodDefinitionHandle serializeHandle = FindMethod(
            reader,
            publication,
            "Serialize");
        MethodDefinition serialize = reader.GetMethodDefinition(serializeHandle);
        ManagedBindingContract bindingContract = DecodeManagedBindingContract(
            reader,
            serialize);
        ValidateManagedBindingContract(bindingContract);
        ValidateManagedBindingHostileContracts(bindingContract);

        Require(
            (serialize.Attributes & MethodAttributes.MemberAccessMask) ==
                MethodAttributes.Assembly,
            "serialize-access");
        Require(
            (serialize.Attributes & MethodAttributes.Static) != 0,
            "serialize-not-static");
        Require(
            (serialize.Attributes & MethodAttributes.PinvokeImpl) == 0,
            "serialize-pinvoke");
        Require(
            (serialize.ImplAttributes & MethodImplAttributes.InternalCall) != 0,
            "serialize-internal-call-missing");
        Require(serialize.RelativeVirtualAddress == 0, "serialize-has-il");
        Require(
            reader.GetBlobBytes(serialize.Signature).SequenceEqual(
                new byte[] { 0x00, 0x00, 0x09 }),
            "serialize-signature");

        CustomAttribute[] attributes = serialize
            .GetCustomAttributes()
            .Select(handle => reader.GetCustomAttribute(handle))
            .ToArray();
        CustomAttribute[] runtimeImports = attributes
            .Where(attribute =>
                GetAttributeTypeName(reader, attribute) ==
                "System.Runtime.RuntimeImportAttribute")
            .ToArray();
        Require(runtimeImports.Length == 1, "runtime-import-count");
        ValidateRuntimeImportBlob(reader, runtimeImports[0]);
        Require(
            !attributes.Any(attribute =>
            {
                string name = GetAttributeTypeName(reader, attribute);
                return name.Contains("DllImport", StringComparison.Ordinal) ||
                    name.Contains("LibraryImport", StringComparison.Ordinal) ||
                    name.Contains("Marshal", StringComparison.Ordinal);
            }),
            "serialize-forbidden-attribute");

        FieldDefinitionHandle cookieHandle = FindField(
            reader,
            publication,
            "ExpectedSuccessCookie");
        FieldDefinition cookie = reader.GetFieldDefinition(cookieHandle);
        FieldAttributes requiredCookieAttributes =
            FieldAttributes.Assembly |
            FieldAttributes.Static |
            FieldAttributes.Literal |
            FieldAttributes.HasDefault;
        Require(
            (cookie.Attributes & requiredCookieAttributes) ==
                requiredCookieAttributes,
            "cookie-attributes");
        Require(
            reader.GetBlobBytes(cookie.Signature).SequenceEqual(
                new byte[] { 0x06, 0x09 }),
            "cookie-signature");
        Constant cookieConstant = reader.GetConstant(cookie.GetDefaultValue());
        Require(
            cookieConstant.TypeCode == ConstantTypeCode.UInt32,
            "cookie-constant-type");
        BlobReader cookieValue = reader.GetBlobReader(cookieConstant.Value);
        Require(
            cookieValue.ReadUInt32() == ExpectedSuccessCookie &&
            cookieValue.RemainingBytes == 0,
            "cookie-constant-value");

        ValidateRuntimeImportType(reader);
        ValidateMethodImplShim(reader);
    }

    private static ManagedBindingContract DecodeManagedBindingContract(
        MetadataReader reader,
        MethodDefinition serialize)
    {
        CustomAttribute[] attributes = serialize
            .GetCustomAttributes()
            .Select(handle => reader.GetCustomAttribute(handle))
            .ToArray();
        (string Name, CustomAttribute Attribute)[] runtimeImportCandidates =
            attributes
                .Select(attribute => (
                    Name: GetAttributeTypeName(reader, attribute),
                    Attribute: attribute))
                .Where(candidate =>
                    candidate.Name.Contains(
                        "RuntimeImport",
                        StringComparison.Ordinal))
                .ToArray();

        string? runtimeImportName = runtimeImportCandidates.Length == 1
            ? runtimeImportCandidates[0].Name
            : null;
        byte[]? runtimeImportConstructorSignature =
            runtimeImportCandidates.Length == 1
                ? GetCustomAttributeConstructorSignature(
                    reader,
                    runtimeImportCandidates[0].Attribute)
                : null;

        TypeDefinitionHandle? optionsHandle = TryFindType(
            reader,
            "System.Runtime.CompilerServices",
            "MethodImplOptions");
        TypeDefinitionHandle? methodImplAttributeHandle = TryFindType(
            reader,
            "System.Runtime.CompilerServices",
            "MethodImplAttribute");

        int? internalCallValue = null;
        if (optionsHandle.HasValue)
        {
            TypeDefinition options = reader.GetTypeDefinition(
                optionsHandle.Value);
            FieldDefinitionHandle? internalCallHandle = TryFindField(
                reader,
                options,
                "InternalCall");
            if (internalCallHandle.HasValue)
            {
                FieldDefinition internalCall = reader.GetFieldDefinition(
                    internalCallHandle.Value);
                if (!internalCall.GetDefaultValue().IsNil)
                {
                    Constant constant = reader.GetConstant(
                        internalCall.GetDefaultValue());
                    if (constant.TypeCode == ConstantTypeCode.Int32)
                    {
                        BlobReader value = reader.GetBlobReader(
                            constant.Value);
                        if (value.RemainingBytes == sizeof(int))
                        {
                            internalCallValue = value.ReadInt32();
                        }
                    }
                }
            }
        }

        bool methodImplConstructorPresent = false;
        bool methodImplConstructorShapeValid = false;
        if (methodImplAttributeHandle.HasValue && optionsHandle.HasValue)
        {
            TypeDefinition methodImplAttribute = reader.GetTypeDefinition(
                methodImplAttributeHandle.Value);
            MethodDefinitionHandle[] constructors = methodImplAttribute
                .GetMethods()
                .Where(handle =>
                    reader.GetString(
                        reader.GetMethodDefinition(handle).Name) == ".ctor")
                .ToArray();
            methodImplConstructorPresent = constructors.Length > 0;
            methodImplConstructorShapeValid =
                constructors.Length == 1 &&
                IsMethodImplConstructorShapeValid(
                    reader,
                    reader.GetMethodDefinition(constructors[0]),
                    optionsHandle.Value);
        }

        return new ManagedBindingContract(
            RuntimeImportAttributeCount: runtimeImportCandidates.Length,
            RuntimeImportAttributeName: runtimeImportName,
            RuntimeImportConstructorSignature:
                runtimeImportConstructorSignature,
            MethodImplAttributePresent: methodImplAttributeHandle.HasValue,
            MethodImplConstructorPresent: methodImplConstructorPresent,
            MethodImplConstructorShapeValid:
                methodImplConstructorShapeValid,
            MethodImplInternalCallValue: internalCallValue,
            SerializeIsStatic:
                (serialize.Attributes & MethodAttributes.Static) != 0,
            SerializeIsExtern:
                (serialize.ImplAttributes &
                    MethodImplAttributes.InternalCall) != 0,
            SerializeHasPInvokeImplementation:
                (serialize.Attributes & MethodAttributes.PinvokeImpl) != 0,
            SerializeHasDllImportAttribute: attributes.Any(attribute =>
            {
                string name = GetAttributeTypeName(reader, attribute);
                return name.Contains("DllImport", StringComparison.Ordinal) ||
                    name.Contains("LibraryImport", StringComparison.Ordinal);
            }),
            SerializeHasManagedBody: serialize.RelativeVirtualAddress != 0,
            SerializeSignature: reader.GetBlobBytes(serialize.Signature));
    }

    private static byte[]? GetCustomAttributeConstructorSignature(
        MetadataReader reader,
        CustomAttribute attribute)
    {
        return attribute.Constructor.Kind switch
        {
            HandleKind.MethodDefinition => reader.GetBlobBytes(
                reader.GetMethodDefinition(
                    (MethodDefinitionHandle)attribute.Constructor).Signature),
            HandleKind.MemberReference => reader.GetBlobBytes(
                reader.GetMemberReference(
                    (MemberReferenceHandle)attribute.Constructor).Signature),
            _ => null
        };
    }

    private static bool IsMethodImplConstructorShapeValid(
        MetadataReader reader,
        MethodDefinition constructor,
        TypeDefinitionHandle optionsHandle)
    {
        try
        {
            BlobReader signature = reader.GetBlobReader(
                constructor.Signature);
            return signature.ReadByte() == 0x20 &&
                signature.ReadCompressedInteger() == 1 &&
                signature.ReadByte() == 0x01 &&
                signature.ReadByte() == 0x11 &&
                DecodeTypeDefOrRef(signature.ReadCompressedInteger()) ==
                    optionsHandle &&
                signature.RemainingBytes == 0;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static void ValidateManagedBindingContract(
        ManagedBindingContract contract)
    {
        Require(
            contract.RuntimeImportAttributeCount != 0,
            "managed-contract-runtime-import-missing");
        Require(
            contract.RuntimeImportAttributeCount == 1,
            "managed-contract-runtime-import-count");
        Require(
            contract.RuntimeImportAttributeName ==
                "System.Runtime.RuntimeImportAttribute",
            "managed-contract-runtime-import-name");
        Require(
            contract.RuntimeImportConstructorSignature is not null,
            "managed-contract-runtime-import-constructor-missing");
        Require(
            contract.RuntimeImportConstructorSignature.SequenceEqual(
                new byte[] { 0x20, 0x02, 0x01, 0x0E, 0x0E }),
            "managed-contract-runtime-import-constructor-shape");

        Require(
            contract.MethodImplAttributePresent,
            "managed-contract-method-impl-attribute-missing");
        Require(
            contract.MethodImplConstructorPresent,
            "managed-contract-method-impl-constructor-missing");
        Require(
            contract.MethodImplConstructorShapeValid,
            "managed-contract-method-impl-constructor-shape");
        Require(
            contract.MethodImplInternalCallValue.HasValue,
            "managed-contract-method-impl-internal-call-missing");
        Require(
            contract.MethodImplInternalCallValue.GetValueOrDefault() == 0x1000,
            "managed-contract-method-impl-internal-call-value");

        Require(
            contract.SerializeIsStatic,
            "managed-contract-serialize-not-static");
        Require(
            contract.SerializeIsExtern,
            "managed-contract-serialize-not-extern");
        Require(
            !contract.SerializeHasPInvokeImplementation,
            "managed-contract-serialize-pinvoke");
        Require(
            !contract.SerializeHasDllImportAttribute,
            "managed-contract-serialize-dllimport");
        Require(
            !contract.SerializeHasManagedBody,
            "managed-contract-serialize-managed-body");
        Require(
            contract.SerializeSignature.Length >= 3 &&
            contract.SerializeSignature[0] == 0x00,
            "managed-contract-serialize-signature-callconv");
        Require(
            contract.SerializeSignature[1] == 0x00,
            "managed-contract-serialize-parameter-signature");
        Require(
            contract.SerializeSignature[2] == 0x09,
            "managed-contract-serialize-return-signature");
        Require(
            contract.SerializeSignature.Length == 3,
            "managed-contract-serialize-signature-trailing-data");
    }

    private static void ValidateManagedBindingHostileContracts(
        ManagedBindingContract valid)
    {
        RequireManagedBindingContractRejected(
            valid with
            {
                RuntimeImportAttributeCount = 0,
                RuntimeImportAttributeName = null,
                RuntimeImportConstructorSignature = null
            },
            "runtime-import-missing",
            "managed-contract-runtime-import-missing");
        RequireManagedBindingContractRejected(
            valid with
            {
                RuntimeImportAttributeName =
                    "System.Runtime.WrongRuntimeImportAttribute"
            },
            "runtime-import-wrong-name",
            "managed-contract-runtime-import-name");
        RequireManagedBindingContractRejected(
            valid with { RuntimeImportConstructorSignature = null },
            "runtime-import-constructor-missing",
            "managed-contract-runtime-import-constructor-missing");
        RequireManagedBindingContractRejected(
            valid with
            {
                RuntimeImportConstructorSignature =
                    new byte[] { 0x20, 0x01, 0x01, 0x0E }
            },
            "runtime-import-constructor-wrong-shape",
            "managed-contract-runtime-import-constructor-shape");

        RequireManagedBindingContractRejected(
            valid with { MethodImplAttributePresent = false },
            "method-impl-attribute-missing",
            "managed-contract-method-impl-attribute-missing");
        RequireManagedBindingContractRejected(
            valid with { MethodImplConstructorPresent = false },
            "method-impl-constructor-missing",
            "managed-contract-method-impl-constructor-missing");
        RequireManagedBindingContractRejected(
            valid with { MethodImplConstructorShapeValid = false },
            "method-impl-constructor-wrong-shape",
            "managed-contract-method-impl-constructor-shape");
        RequireManagedBindingContractRejected(
            valid with { MethodImplInternalCallValue = null },
            "method-impl-internal-call-missing",
            "managed-contract-method-impl-internal-call-missing");
        RequireManagedBindingContractRejected(
            valid with { MethodImplInternalCallValue = 0x1001 },
            "method-impl-internal-call-wrong-value",
            "managed-contract-method-impl-internal-call-value");

        RequireManagedBindingContractRejected(
            valid with { SerializeHasPInvokeImplementation = true },
            "serialize-pinvoke",
            "managed-contract-serialize-pinvoke");
        RequireManagedBindingContractRejected(
            valid with { SerializeHasDllImportAttribute = true },
            "serialize-dllimport",
            "managed-contract-serialize-dllimport");
        RequireManagedBindingContractRejected(
            valid with { SerializeHasManagedBody = true },
            "serialize-managed-body",
            "managed-contract-serialize-managed-body");
        RequireManagedBindingContractRejected(
            valid with { SerializeIsStatic = false },
            "serialize-non-static",
            "managed-contract-serialize-not-static");
        RequireManagedBindingContractRejected(
            valid with { SerializeIsExtern = false },
            "serialize-non-extern",
            "managed-contract-serialize-not-extern");
        RequireManagedBindingContractRejected(
            valid with
            {
                SerializeSignature = new byte[] { 0x00, 0x00, 0x08 }
            },
            "serialize-wrong-return-signature",
            "managed-contract-serialize-return-signature");
        RequireManagedBindingContractRejected(
            valid with
            {
                SerializeSignature =
                    new byte[] { 0x00, 0x01, 0x09, 0x09 }
            },
            "serialize-wrong-parameter-signature",
            "managed-contract-serialize-parameter-signature");

        Console.WriteLine(
            "CLROS_M01_034_MANAGED_ABI_NEGATIVE_PASS cases=16");
    }

    private static void RequireManagedBindingContractRejected(
        ManagedBindingContract hostile,
        string caseName,
        string expectedCode)
    {
        try
        {
            ValidateManagedBindingContract(hostile);
        }
        catch (M01BuildIntegrationFailureException ex)
        {
            Require(
                ex.Code == expectedCode,
                "managed-contract-hostile-" +
                    StableCode(caseName) +
                    "-wrong-code");
            return;
        }

        Require(
            condition: false,
            "managed-contract-hostile-" +
                StableCode(caseName) +
                "-accepted");
    }

    private static void ValidateRuntimeImportBlob(
        MetadataReader reader,
        CustomAttribute attribute)
    {
        BlobReader value = reader.GetBlobReader(attribute.Value);
        Require(value.ReadUInt16() == 1, "runtime-import-prolog");
        Require(
            value.ReadSerializedString() == "*",
            "runtime-import-library");
        Require(
            value.ReadSerializedString() == SerializerSymbol,
            "runtime-import-symbol");
        Require(value.ReadUInt16() == 0, "runtime-import-named-arguments");
        Require(value.RemainingBytes == 0, "runtime-import-trailing-data");
    }

    private static void ValidateRuntimeImportType(MetadataReader reader)
    {
        TypeDefinition type = reader.GetTypeDefinition(
            FindType(reader, "System.Runtime", "RuntimeImportAttribute"));
        Require(
            (type.Attributes & TypeAttributes.Sealed) != 0,
            "runtime-import-type-not-sealed");
        MethodDefinition constructor = reader.GetMethodDefinition(
            FindMethod(reader, type, ".ctor"));
        Require(
            reader.GetBlobBytes(constructor.Signature).SequenceEqual(
                new byte[] { 0x20, 0x02, 0x01, 0x0E, 0x0E }),
            "runtime-import-constructor-signature");
    }

    private static void ValidateMethodImplShim(MetadataReader reader)
    {
        TypeDefinitionHandle optionsHandle = FindType(
            reader,
            "System.Runtime.CompilerServices",
            "MethodImplOptions");
        TypeDefinition options = reader.GetTypeDefinition(optionsHandle);
        FieldDefinition internalCall = reader.GetFieldDefinition(
            FindField(reader, options, "InternalCall"));
        Constant internalCallConstant = reader.GetConstant(
            internalCall.GetDefaultValue());
        BlobReader constantValue = reader.GetBlobReader(
            internalCallConstant.Value);
        Require(
            constantValue.ReadInt32() == 0x1000 &&
            constantValue.RemainingBytes == 0,
            "method-impl-options-internal-call");

        TypeDefinition attribute = reader.GetTypeDefinition(
            FindType(
                reader,
                "System.Runtime.CompilerServices",
                "MethodImplAttribute"));
        Require(
            (attribute.Attributes & TypeAttributes.Sealed) != 0,
            "method-impl-attribute-not-sealed");
        MethodDefinition constructor = reader.GetMethodDefinition(
            FindMethod(reader, attribute, ".ctor"));
        BlobReader signature = reader.GetBlobReader(constructor.Signature);
        Require(signature.ReadByte() == 0x20, "method-impl-constructor-callconv");
        Require(
            signature.ReadCompressedInteger() == 1,
            "method-impl-constructor-parameter-count");
        Require(signature.ReadByte() == 0x01, "method-impl-constructor-return");
        Require(signature.ReadByte() == 0x11, "method-impl-constructor-parameter");
        int codedIndex = signature.ReadCompressedInteger();
        Require(
            DecodeTypeDefOrRef(codedIndex) == optionsHandle,
            "method-impl-constructor-parameter-type");
        Require(
            signature.RemainingBytes == 0,
            "method-impl-constructor-trailing-data");
    }

    private static void ValidateBuildTargetExecution(
        string repositoryRoot,
        string osProjectPath)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "clros m01-034 build integration " +
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            string sequentialRoot = Path.Combine(
                temporaryRoot,
                "absolute path with spaces",
                "native");
            ProcessResult first = RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                sequentialRoot);
            RequireProcessSuccess(first, "target-first");
            Require(
                first.Output.Contains(
                    "X64_SERIALIZER_OBJECT_READY bytes=" + SerializerObjectBytes.ToString(CultureInfo.InvariantCulture) + " sha256=" +
                    SerializerObjectSha256 +
                    " state=created",
                    StringComparison.Ordinal),
                "target-first-marker");
            ValidateGeneratedObject(sequentialRoot, "target-first-object");

            ProcessResult incremental = RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                sequentialRoot);
            RequireProcessSuccess(incremental, "target-incremental");
            Require(
                incremental.Output.Contains(
                    "X64_SERIALIZER_OBJECT_READY bytes=" + SerializerObjectBytes.ToString(CultureInfo.InvariantCulture) + " sha256=" +
                    SerializerObjectSha256 +
                    " state=unchanged",
                    StringComparison.Ordinal),
                "target-incremental-marker");
            ValidateGeneratedObject(sequentialRoot, "target-incremental-object");
            ValidateChildBuildControlIsolation(
                repositoryRoot,
                osProjectPath,
                temporaryRoot,
                sequentialRoot);
            ValidateNestedGeneratorPropertyIsolation(
                repositoryRoot,
                osProjectPath,
                temporaryRoot);

            string parallelRootA = Path.Combine(
                temporaryRoot,
                "parallel a",
                "native");
            string parallelRootB = Path.Combine(
                temporaryRoot,
                "parallel b",
                "native");
            (ProcessResult parallelA, ProcessResult parallelB) =
                RunIntegrationTargetsConcurrently(
                    repositoryRoot,
                    osProjectPath,
                    parallelRootA,
                    parallelRootB);
            RequireProcessSuccess(parallelA, "target-parallel-a");
            RequireProcessSuccess(parallelB, "target-parallel-b");
            ValidateGeneratedObject(parallelRootA, "target-parallel-object-a");
            ValidateGeneratedObject(parallelRootB, "target-parallel-object-b");

            ValidateCleanLifecycle(
                repositoryRoot,
                osProjectPath,
                temporaryRoot);

            string objectPath = Path.Combine(
                sequentialRoot,
                SerializerObjectName);
            byte[] corrupted = File.ReadAllBytes(objectPath);
            corrupted[0] ^= 0xFF;
            File.WriteAllBytes(objectPath, corrupted);
            ProcessResult stale = RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                sequentialRoot);
            Require(!stale.TimedOut, "target-stale-timeout");
            Require(stale.ExitCode != 0, "target-stale-accepted");
            Require(
                stale.Output.Contains(
                    "TEST_FAIL x64.serializer.generate existing_output_mismatch",
                    StringComparison.Ordinal),
                "target-stale-marker");

            ValidatePropertyOverrideRejected(
                repositoryRoot,
                osProjectPath,
                temporaryRoot,
                "ClrosX64InstructionPublicationSymbol",
                "NOT_CLROS",
                "symbol_override");
            ValidatePropertyOverrideRejected(
                repositoryRoot,
                osProjectPath,
                temporaryRoot,
                "_ClrosX64SerializerObjectName",
                "redirected.obj",
                "object_name_override");
            ValidatePropertyOverrideRejected(
                repositoryRoot,
                osProjectPath,
                temporaryRoot,
                "_ClrosX64SerializerActualSha256",
                SerializerObjectSha256,
                "actual_hash_override");
        }
        finally
        {
            TryDeleteDirectory(temporaryRoot);
        }
    }

    private static void ValidateCleanLifecycle(
        string repositoryRoot,
        string osProjectPath,
        string temporaryRoot)
    {
        string lifecycleRoot = Path.Combine(temporaryRoot, "clean lifecycle");
        string baseIntermediatePath = Path.Combine(
            lifecycleRoot,
            "managed obj");
        string baseOutputPath = Path.Combine(
            lifecycleRoot,
            "managed bin");
        string nativeIntermediatePath = Path.Combine(
            baseIntermediatePath,
            "Release",
            "net11.0",
            "win-x64",
            "native");
        string objectPath = Path.Combine(
            nativeIntermediatePath,
            SerializerObjectName);
        string toolRoot = Path.Combine(
            nativeIntermediatePath,
            "clros-x64-serializer-tool");
        string toolExecutable = Path.Combine(
            toolRoot,
            "bin",
            "Release",
            "net11.0",
            "OS.X64TextSerializerGenerator.exe");
        string fileListPath = Path.Combine(
            baseIntermediatePath,
            "Release",
            "net11.0",
            "win-x64",
            "OS.csproj.FileListAbsolute.txt");

        ProcessResult generated = RunIntegrationTarget(
            repositoryRoot,
            osProjectPath,
            nativeIntermediatePath);
        RequireProcessSuccess(generated, "clean-lifecycle-generate");
        ValidateGeneratedObject(
            nativeIntermediatePath,
            "clean-lifecycle-generated-object");
        Require(
            File.Exists(toolExecutable),
            "clean-lifecycle-tool-executable-missing");

        ProcessResult build = RunManagedLifecycleCommand(
            repositoryRoot,
            osProjectPath,
            "build",
            nativeIntermediatePath,
            baseIntermediatePath,
            baseOutputPath);
        RequireProcessSuccess(build, "clean-lifecycle-build");
        Require(File.Exists(fileListPath), "clean-lifecycle-file-list-missing");
        Require(
            File.ReadAllLines(fileListPath).Any(line =>
                string.Equals(
                    Path.GetFullPath(line),
                    Path.GetFullPath(objectPath),
                    StringComparison.OrdinalIgnoreCase)),
            "clean-lifecycle-object-not-recorded");
        Require(
            File.ReadAllLines(fileListPath).Any(line =>
                string.Equals(
                    Path.GetFullPath(line),
                    Path.GetFullPath(toolExecutable),
                    StringComparison.OrdinalIgnoreCase)),
            "clean-lifecycle-tool-not-recorded");

        ProcessResult clean = RunManagedLifecycleCommand(
            repositoryRoot,
            osProjectPath,
            "clean",
            nativeIntermediatePath,
            baseIntermediatePath,
            baseOutputPath);
        RequireProcessSuccess(clean, "clean-lifecycle-clean");
        Require(
            !File.Exists(objectPath),
            "clean-lifecycle-object-not-removed");
        Require(
            !Directory.Exists(toolRoot),
            "clean-lifecycle-tool-root-not-removed");

        ProcessResult regenerated = RunIntegrationTarget(
            repositoryRoot,
            osProjectPath,
            nativeIntermediatePath);
        RequireProcessSuccess(regenerated, "clean-lifecycle-regenerate");
        Require(
            regenerated.Output.Contains(
                "X64_SERIALIZER_OBJECT_READY bytes=" + SerializerObjectBytes.ToString(CultureInfo.InvariantCulture) + " sha256=" +
                SerializerObjectSha256 +
                " state=created",
                StringComparison.Ordinal),
            "clean-lifecycle-regenerate-marker");
        ValidateGeneratedObject(
            nativeIntermediatePath,
            "clean-lifecycle-regenerated-object");
        Require(
            File.Exists(toolExecutable),
            "clean-lifecycle-tool-not-regenerated");
    }

    private static ProcessResult RunManagedLifecycleCommand(
        string repositoryRoot,
        string osProjectPath,
        string command,
        string nativeIntermediatePath,
        string baseIntermediatePath,
        string baseOutputPath)
    {
        string normalizedBaseIntermediate =
            Path.GetFullPath(baseIntermediatePath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            "/";
        string normalizedBaseOutput =
            Path.GetFullPath(baseOutputPath)
                .Replace(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            "/";
        return RunDotnet(
            repositoryRoot,
            timeoutMilliseconds: 120_000,
            command,
            osProjectPath,
            "--nologo",
            "--configuration",
            "Release",
            "-m:1",
            "-p:NativeIntermediateOutputPath=" +
                Path.GetFullPath(nativeIntermediatePath),
            "-p:BaseIntermediateOutputPath=" +
                normalizedBaseIntermediate,
            "-p:BaseOutputPath=" +
                normalizedBaseOutput,
            "-p:RestoreIgnoreFailedSources=true");
    }

    private static void ValidatePropertyOverrideRejected(
        string repositoryRoot,
        string osProjectPath,
        string temporaryRoot,
        string propertyName,
        string propertyValue,
        string expectedReason)
    {
        string intermediateRoot = Path.Combine(
            temporaryRoot,
            "override-" + StableCode(propertyName),
            "native");
        ProcessResult result = RunIntegrationTarget(
            repositoryRoot,
            osProjectPath,
            intermediateRoot,
            [propertyName + "=" + propertyValue]);
        Require(!result.TimedOut, "override-" + expectedReason + "-timeout");
        Require(result.ExitCode != 0, "override-" + expectedReason + "-accepted");
        Require(
            result.Output.Contains(
                "CLROS_M01_034_GENERATOR_FAILED " + expectedReason,
                StringComparison.Ordinal),
            "override-" + expectedReason + "-marker");
    }

    private static void ValidateChildBuildControlIsolation(
        string repositoryRoot,
        string osProjectPath,
        string temporaryRoot,
        string nativeIntermediateRoot)
    {
        const string privateHashOverride =
            "_ClrosX64SerializerGeneratorProjectSha256=FORCED";

        RequireChildBuildControlRejected(
            RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                nativeIntermediateRoot,
                ["BuildDependsOn=", privateHashOverride]),
            "build-depends-on");

        string bypassTargetsPath = Path.Combine(
            temporaryRoot,
            "malicious custom after.targets");
        File.WriteAllText(
            bypassTargetsPath,
            "<Project><Target Name=\"" +
            "ValidateClrosX64SerializerGeneratorCompileClosure" +
            "\" /></Project>",
            new UTF8Encoding(false));

        RequireChildBuildControlRejected(
            RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                nativeIntermediateRoot,
                [
                    "CustomAfterMicrosoftCommonTargets=" + bypassTargetsPath,
                    privateHashOverride
                ]),
            "custom-after");

        string bypassPropsPath = Path.Combine(
            temporaryRoot,
            "malicious directory build.props");
        string escapedBypassTargetsPath =
            System.Security.SecurityElement.Escape(bypassTargetsPath) ??
            throw new InvalidOperationException("Unable to escape bypass target path.");
        File.WriteAllText(
            bypassPropsPath,
            "<Project><PropertyGroup>" +
            "<CustomAfterMicrosoftCommonTargets>" +
            escapedBypassTargetsPath +
            "</CustomAfterMicrosoftCommonTargets>" +
            "</PropertyGroup></Project>",
            new UTF8Encoding(false));

        RequireChildBuildControlRejected(
            RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                nativeIntermediateRoot,
                [
                    "DirectoryBuildPropsPath=" + bypassPropsPath,
                    privateHashOverride
                ]),
            "directory-build-props");

        RequireChildBuildControlRejected(
            RunIntegrationTarget(
                repositoryRoot,
                osProjectPath,
                nativeIntermediateRoot,
                [
                    "DirectoryBuildTargetsPath=" + bypassTargetsPath,
                    privateHashOverride
                ]),
            "directory-build-targets");
    }

    private static void ValidateNestedGeneratorPropertyIsolation(
        string repositoryRoot,
        string osProjectPath,
        string temporaryRoot)
    {
        string poisonRoot = Path.Combine(
            temporaryRoot,
            "parent restore globals must not leak");
        string nativeIntermediateRoot = Path.Combine(
            temporaryRoot,
            "nested property isolation",
            "native");
        ProcessResult result = RunIntegrationTarget(
            repositoryRoot,
            osProjectPath,
            nativeIntermediateRoot,
            [
                "MSBuildProjectExtensionsPath=" +
                    Path.Combine(poisonRoot, "project extensions") +
                    Path.DirectorySeparatorChar,
                "RestoreOutputPath=" +
                    Path.Combine(poisonRoot, "restore output") +
                    Path.DirectorySeparatorChar,
                "ProjectAssetsFile=" +
                    Path.Combine(poisonRoot, "poison.assets.json"),
                "ImportProjectExtensionProps=false",
                "ImportProjectExtensionTargets=false",
                "RestoreUseCustomAfterTargets=true",
                "RestoreForceEvaluate=false",
                "RestoreIgnoreFailedSources=false",
                "RestorePackagesPath=" +
                    Path.Combine(poisonRoot, "packages"),
                "NuGetPackageRoot=" +
                    Path.Combine(poisonRoot, "nuget"),
                "MSBuildUserExtensionsPath=" +
                    Path.Combine(poisonRoot, "user extensions"),
                "UseSharedCompilation=true",
                "ImportByWildcardBeforeMicrosoftCommonProps=true",
                "ImportByWildcardAfterMicrosoftCommonProps=true",
                "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps=true",
                "ImportUserLocationsByWildcardAfterMicrosoftCommonProps=true",
                "ImportByWildcardBeforeMicrosoftCommonTargets=true",
                "ImportByWildcardAfterMicrosoftCommonTargets=true",
                "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets=true",
                "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets=true",
                "ImportByWildcardBeforeMicrosoftCSharpTargets=true",
                "ImportByWildcardAfterMicrosoftCSharpTargets=true",
                "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets=true",
                "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets=true",
                "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets=true",
                "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets=true"
            ]);
        if (result.ExitCode != 0 || result.TimedOut)
        {
            Console.WriteLine(result.Output);
        }
        RequireProcessSuccess(result, "nested-property-isolation");
        Require(
            result.Output.Contains(
                "X64_SERIALIZER_OBJECT_READY bytes=" + SerializerObjectBytes.ToString(CultureInfo.InvariantCulture) + " sha256=" +
                    SerializerObjectSha256,
                StringComparison.Ordinal),
            "nested-property-isolation-marker");
        ValidateGeneratedObject(
            nativeIntermediateRoot,
            "nested-property-isolation-object");
    }

    private static void RequireChildBuildControlRejected(
        ProcessResult result,
        string failurePrefix)
    {
        Require(!result.TimedOut, "child-control-" + failurePrefix + "-timeout");
        Require(
            result.ExitCode != 0,
            "child-control-" + failurePrefix + "-accepted");
        Require(
            result.Output.Contains(
                "CLROS_M01_034_GENERATOR_FAILED build_input_hash_override",
                StringComparison.Ordinal),
            "child-control-" + failurePrefix + "-marker");
    }

    private static ProcessResult RunIntegrationTarget(
        string repositoryRoot,
        string osProjectPath,
        string nativeIntermediateRoot,
        IReadOnlyList<string>? additionalProperties = null)
    {
        string rootedIntermediate = Path.GetFullPath(nativeIntermediateRoot) +
            Path.DirectorySeparatorChar;
        return RunProcess(
            CreateIntegrationTargetStartInfo(
                repositoryRoot,
                osProjectPath,
                rootedIntermediate,
                additionalProperties),
            timeoutMilliseconds: 120_000);
    }

    private static ProcessStartInfo CreateIntegrationTargetStartInfo(
        string repositoryRoot,
        string osProjectPath,
        string rootedIntermediate,
        IReadOnlyList<string>? additionalProperties)
    {
        List<string> arguments =
        [
            "msbuild",
            osProjectPath,
            "-nologo",
            "-t:IntegrateClrosX64InstructionPublicationSerializer",
            "-p:Configuration=Release",
            "-p:NativeIntermediateOutputPath=" + rootedIntermediate,
            "-p:RestoreIgnoreFailedSources=true",
            "-m:1",
            "-nodeReuse:false"
        ];
        if (additionalProperties is not null)
        {
            foreach (string additionalProperty in additionalProperties)
            {
                arguments.Add("-p:" + additionalProperty);
            }
        }

        return CreateDotnetStartInfo(repositoryRoot, arguments);
    }

    private static (
        ProcessResult First,
        ProcessResult Second) RunIntegrationTargetsConcurrently(
            string repositoryRoot,
            string osProjectPath,
            string firstNativeIntermediateRoot,
            string secondNativeIntermediateRoot)
    {
        ProcessStartInfo firstStart = CreateIntegrationTargetStartInfo(
            repositoryRoot,
            osProjectPath,
            Path.GetFullPath(firstNativeIntermediateRoot) +
                Path.DirectorySeparatorChar,
            additionalProperties: null);
        ProcessStartInfo secondStart = CreateIntegrationTargetStartInfo(
            repositoryRoot,
            osProjectPath,
            Path.GetFullPath(secondNativeIntermediateRoot) +
                Path.DirectorySeparatorChar,
            additionalProperties: null);
        using Process first = new Process { StartInfo = firstStart };
        using Process second = new Process { StartInfo = secondStart };
        Require(first.Start(), "target-parallel-a-start");
        Task<string> firstOutput = first.StandardOutput.ReadToEndAsync();
        Task<string> firstError = first.StandardError.ReadToEndAsync();
        try
        {
            Require(second.Start(), "target-parallel-b-start");
        }
        catch
        {
            TryTerminate(first);
            throw;
        }
        Task<string> secondOutput = second.StandardOutput.ReadToEndAsync();
        Task<string> secondError = second.StandardError.ReadToEndAsync();

        Stopwatch stopwatch = Stopwatch.StartNew();
        while ((!first.HasExited || !second.HasExited) &&
            stopwatch.Elapsed < TimeSpan.FromSeconds(120))
        {
            Thread.Sleep(25);
        }
        bool firstExited = first.HasExited;
        bool secondExited = second.HasExited;
        if (!firstExited)
        {
            TryTerminate(first);
        }
        if (!secondExited)
        {
            TryTerminate(second);
        }
        _ = first.WaitForExit(10_000);
        _ = second.WaitForExit(10_000);

        bool streamsCompleted = Task.WaitAll(
            [firstOutput, firstError, secondOutput, secondError],
            millisecondsTimeout: 10_000);
        ProcessResult firstResult = CreateProcessResult(
            first,
            firstExited && streamsCompleted,
            firstOutput,
            firstError);
        ProcessResult secondResult = CreateProcessResult(
            second,
            secondExited && streamsCompleted,
            secondOutput,
            secondError);
        return (firstResult, secondResult);
    }

    private static void ValidateGeneratedObject(
        string nativeIntermediateRoot,
        string failurePrefix)
    {
        string objectPath = Path.Combine(
            nativeIntermediateRoot,
            SerializerObjectName);
        Require(File.Exists(objectPath), failurePrefix + "-missing");
        byte[] bytes = File.ReadAllBytes(objectPath);
        Require(bytes.Length == SerializerObjectBytes, failurePrefix + "-size");
        Require(
            Convert.ToHexString(SHA256.HashData(bytes)) ==
                SerializerObjectSha256,
            failurePrefix + "-hash");
    }

    private static ProcessResult RunDotnet(
        string workingDirectory,
        int timeoutMilliseconds,
        params string[] arguments)
    {
        return RunProcess(
            CreateDotnetStartInfo(workingDirectory, arguments),
            timeoutMilliseconds);
    }

    private static ProcessStartInfo CreateDotnetStartInfo(
        string workingDirectory,
        IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        return start;
    }

    private static ProcessResult RunProcess(
        ProcessStartInfo start,
        int timeoutMilliseconds)
    {
        using Process process = new Process { StartInfo = start };
        try
        {
            Require(process.Start(), "process-start");
        }
        catch (M01BuildIntegrationFailureException)
        {
            throw;
        }
        catch
        {
            return new ProcessResult(-1, false, "process-start-failed");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The bounded timeout result remains a failure even if cleanup races exit.
            }
            _ = process.WaitForExit(10_000);
        }

        bool streamsCompleted = Task.WaitAll(
            [standardOutput, standardError],
            millisecondsTimeout: 10_000);
        return CreateProcessResult(
            process,
            exited && streamsCompleted,
            standardOutput,
            standardError);
    }

    private static ProcessResult CreateProcessResult(
        Process process,
        bool completed,
        Task<string> standardOutput,
        Task<string> standardError)
    {
        string output = completed
            ? standardOutput.GetAwaiter().GetResult() +
                Environment.NewLine +
                standardError.GetAwaiter().GetResult()
            : "process-output-drain-timeout";
        if (output.Length > 1_000_000)
        {
            output = output[..1_000_000] + "\nprocess-output-truncated";
        }

        return new ProcessResult(
            completed && process.HasExited ? process.ExitCode : -1,
            !completed || !process.HasExited,
            output);
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The bounded timeout remains a failure if termination races exit.
        }
    }

    private static void RequireProcessSuccess(
        ProcessResult result,
        string failurePrefix)
    {
        Require(!result.TimedOut, failurePrefix + "-timeout");
        Require(result.ExitCode == 0, failurePrefix + "-exit");
        Require(
            !result.Output.Contains(
                "CLROS_M01_034_GENERATOR_FAILED",
                StringComparison.Ordinal) &&
            !result.Output.Contains(
                "TEST_FAIL x64.serializer.generate",
                StringComparison.Ordinal),
            failurePrefix + "-failure-marker");
    }

    private static XmlDocument LoadSecureXml(string path)
    {
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        XmlDocument document = new XmlDocument
        {
            XmlResolver = null
        };
        using XmlReader reader = XmlReader.Create(path, settings);
        document.Load(reader);
        return document;
    }

    private static TypeDefinitionHandle FindType(
        MetadataReader reader,
        string @namespace,
        string name)
    {
        TypeDefinitionHandle[] matches = reader.TypeDefinitions
            .Where(handle =>
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);
                return reader.GetString(type.Namespace) == @namespace &&
                    reader.GetString(type.Name) == name;
            })
            .ToArray();
        Require(matches.Length == 1, "metadata-type-" + StableCode(@namespace + name));
        return matches[0];
    }

    private static TypeDefinitionHandle? TryFindType(
        MetadataReader reader,
        string @namespace,
        string name)
    {
        TypeDefinitionHandle[] matches = reader.TypeDefinitions
            .Where(handle =>
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);
                return reader.GetString(type.Namespace) == @namespace &&
                    reader.GetString(type.Name) == name;
            })
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static MethodDefinitionHandle FindMethod(
        MetadataReader reader,
        TypeDefinition type,
        string name)
    {
        MethodDefinitionHandle[] matches = type
            .GetMethods()
            .Where(handle =>
                reader.GetString(reader.GetMethodDefinition(handle).Name) == name)
            .ToArray();
        Require(matches.Length == 1, "metadata-method-" + StableCode(name));
        return matches[0];
    }

    private static FieldDefinitionHandle FindField(
        MetadataReader reader,
        TypeDefinition type,
        string name)
    {
        FieldDefinitionHandle[] matches = type
            .GetFields()
            .Where(handle =>
                reader.GetString(reader.GetFieldDefinition(handle).Name) == name)
            .ToArray();
        Require(matches.Length == 1, "metadata-field-" + StableCode(name));
        return matches[0];
    }

    private static FieldDefinitionHandle? TryFindField(
        MetadataReader reader,
        TypeDefinition type,
        string name)
    {
        FieldDefinitionHandle[] matches = type
            .GetFields()
            .Where(handle =>
                reader.GetString(reader.GetFieldDefinition(handle).Name) == name)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string GetAttributeTypeName(
        MetadataReader reader,
        CustomAttribute attribute)
    {
        EntityHandle typeHandle;
        if (attribute.Constructor.Kind == HandleKind.MemberReference)
        {
            MemberReference member = reader.GetMemberReference(
                (MemberReferenceHandle)attribute.Constructor);
            typeHandle = member.Parent;
        }
        else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
        {
            MethodDefinition method = reader.GetMethodDefinition(
                (MethodDefinitionHandle)attribute.Constructor);
            typeHandle = method.GetDeclaringType();
        }
        else
        {
            return "<unsupported-constructor>";
        }

        return typeHandle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeName(
                reader,
                reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle)),
            HandleKind.TypeReference => GetTypeName(
                reader,
                reader.GetTypeReference((TypeReferenceHandle)typeHandle)),
            _ => "<unsupported-type>"
        };
    }

    private static string GetTypeName(
        MetadataReader reader,
        TypeDefinition type)
    {
        string @namespace = reader.GetString(type.Namespace);
        string name = reader.GetString(type.Name);
        return string.IsNullOrEmpty(@namespace)
            ? name
            : @namespace + "." + name;
    }

    private static string GetTypeName(
        MetadataReader reader,
        TypeReference type)
    {
        string @namespace = reader.GetString(type.Namespace);
        string name = reader.GetString(type.Name);
        return string.IsNullOrEmpty(@namespace)
            ? name
            : @namespace + "." + name;
    }

    private static EntityHandle DecodeTypeDefOrRef(int codedIndex)
    {
        int rowId = codedIndex >> 2;
        return (codedIndex & 0x3) switch
        {
            0 => MetadataTokens.TypeDefinitionHandle(rowId),
            1 => MetadataTokens.TypeReferenceHandle(rowId),
            2 => MetadataTokens.TypeSpecificationHandle(rowId),
            _ => default
        };
    }

    private static XmlElement RequireElement(XmlNode? node, string code)
    {
        Require(node is XmlElement, code);
        return (XmlElement)node!;
    }

    private static string StableCode(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-');
        }
        return builder.ToString().Trim('-');
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Test evidence has already been collected; best-effort temp cleanup.
        }
    }

    private static void Require(bool condition, string code)
    {
        if (!condition)
        {
            throw new M01BuildIntegrationFailureException(code);
        }
    }

    private sealed class M01BuildIntegrationFailureException : Exception
    {
        internal M01BuildIntegrationFailureException(string code)
            : base(code)
        {
            Code = code;
        }

        internal string Code { get; }
    }

    private readonly record struct ProcessResult(
        int ExitCode,
        bool TimedOut,
        string Output);

    private sealed record ManagedBindingContract(
        int RuntimeImportAttributeCount,
        string? RuntimeImportAttributeName,
        byte[]? RuntimeImportConstructorSignature,
        bool MethodImplAttributePresent,
        bool MethodImplConstructorPresent,
        bool MethodImplConstructorShapeValid,
        int? MethodImplInternalCallValue,
        bool SerializeIsStatic,
        bool SerializeIsExtern,
        bool SerializeHasPInvokeImplementation,
        bool SerializeHasDllImportAttribute,
        bool SerializeHasManagedBody,
        byte[] SerializeSignature);
}
