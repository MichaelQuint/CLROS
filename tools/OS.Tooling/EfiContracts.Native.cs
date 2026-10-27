namespace OS.Tooling;
public sealed record EfiPinnedFile(string Path, string Sha256);
public sealed record EfiPackage(string Id, string Version, string ContentHashSha512, int FileCount, long TotalBytes, string AggregateSha256, string RootKind);
public sealed record EfiArchive(string Id, string Version, string FileName, string Sha256);
public static partial class EfiContracts
{
    public const string SdkVersion = "11.0.100-rc.2.26504.105";
    public const string NativePackageVersion = "11.0.0-rc.2.26504.105";

    /// <summary>Admits only the reviewed SDK identity; another preview or release requires a new qualification.</summary>
    public static bool IsPinnedSdkVersion(string? version) => string.Equals(version, SdkVersion, StringComparison.Ordinal);

    /// <summary>Returns the repository-relative ILC input produced by the pinned SDK's optimized publish.</summary>
    /// <remarks>Only owned build configurations are admitted. This identifies the intermediate assembly, never a copied bin output.</remarks>
    public static string ManagedAssemblyRelativePath(string configuration)
    {
        EfiPeImage.Require(configuration is "Release" or "Debug", "configuration_invalid");
        return $"src/OS/obj/{configuration}/net11.0/win-x64/OS.dll";
    }

    public static System.Collections.ObjectModel.ReadOnlyCollection<(string Property, string Path)> SerializerSourceBindings { get; } = Array.AsReadOnly<(string, string)>(
    [
        ("serializerTargetsSha256", "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets"),
        ("serializerGeneratorProjectSha256", "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj"),
        ("serializerGeneratorProgramSha256", "tools/OS.X64TextSerializerGenerator/Program.cs"),
        ("serializerGeneratorWriterSha256", "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs"),
        ("serializerGeneratorSourceSha256", "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs"),
        ("instructionPublicationSourceSha256", "src/OS/Boot/X64InstructionPublication.cs"),
        ("runtimeImportAttributeSourceSha256", "src/OS/Runtime/RuntimeImportAttribute.cs"),
        ("methodImplAttributeSourceSha256", "src/OS/Runtime/MethodImplAttribute.cs")
    ]);
    public const int CheckedReferenceObjectBytes = 1645, InterfaceDispatchObjectBytes = 814, SimpleCatchObjectBytes = 1350, SingleFinallyObjectBytes = 1707;
    public const string CheckedReferenceObjectSha256 = "e96abf595503b9d1ae38dee740f93ae9b3eb84f05d6395b931e4b5d74f65d808";
    public const string InterfaceDispatchObjectSha256 = "a6122ac2d26139c15d1ae7d004babcbd8649f4d6cfd41846e0cc99a5e3245272";
    public const string SimpleCatchObjectSha256 = "dd32dc30808c3b6dd42981df652b95f62236bdc8c45c1daa3dde32bab1c981ab";
    public const string SingleFinallyObjectSha256 = "d24da7653745da1d37f1fc9d286a2e370c32b687a0415e90fc626a597326025a";
    public const int FilteredCatchObjectBytes = 1361;
    public const int NormalFinallyObjectBytes = 1382;
    public const string NormalFinallyObjectSha256 = "b2d9ef13e71861812b082000698e772b83c3bcdc6a4e38695c6ef2df437a56c8";
    public const string FilteredCatchObjectSha256 = "16fc99408418187da12bab063932e8c0489dd7fc1e0dadb986136f700d0859a3";
    public const int FilterInvokeObjectBytes = 771;
    public const string FilterInvokeObjectSha256 = "e6d84f39a3f159edefaa7a68dd552bf34ccbfa39efbe61f5acdfaa80b838b8a1";
    public const int ConstructorFailureObjectBytes = 1545;
    public const string ConstructorFailureObjectSha256 = "6f17a868dbbccfbaaf0dc46527f8da2c2c92487a7075995eb42df7d6cb019240";
    public const int FrameDispatchObjectBytes = 1869;
    public const string FrameDispatchObjectSha256 = "356bcddd67d7e11c70e46bb235452fc58d66608907df0f8688a0dd2bcd6b6e96";
    public const int NestedFinallyObjectBytes = 2020;
    public const string NestedFinallyObjectSha256 = "94703a587d4d96ca9196c414bcfb6bab5fe3b9321868f35767db62e2937a2e07";
    public const int RethrowInnerObjectBytes = 1732, RethrowOuterObjectBytes = 1382;
    public const string RethrowInnerObjectSha256 = "d30d37f5ad93a70a66d2ed2e69a184a75db903c5a1d69c4e31571569016e7a10";
    public const string RethrowOuterObjectSha256 = "635c903ff77ee3c7de3d1edce3e6f015ddef204454a50432dfcf9692686e337b";
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> SourceClosureExplicitPaths { get; } = Array.AsReadOnly<string>(
    [
        "CLROS.slnx", "global.json", "src/OS/OS.csproj", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj", "tools/OS.X64TextSerializerGenerator/Program.cs",
        "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs", "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs", "tools/OS.X64TextSerializerGenerator/InterfaceDispatchThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/ExceptionHandlingThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/ExceptionFilterThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/NormalFinallyThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/EhContributionCatalogGenerator.cs"
    ]);
    // Bounded host file-backed catalog scan only. The separate kernel unwind
    // decoder retains its2048-function snapshot/lookup contract.
    public const int MaximumRuntimeFunctionRecords = 4096, MaximumLinkResponseTokens = 4096;
    public const int SerializerObjectBytes = 336, SerializerSuccessCookie = 1397902403;
    public const string SerializerSymbol = "CLROS_X64_SerializeInstructionPublication";
    public static System.ReadOnlySpan<byte> SerializerBytes => [0x53, 0xb8, 0, 0, 0, 0, 0xb9, 0, 0, 0, 0, 0x0f, 0xa2, 0xb8, 0x43, 0x4c, 0x52, 0x53, 0x5b, 0xc3];
    public static System.ReadOnlySpan<byte> SerializerUnwindBytes => [0x01, 0x01, 0x01, 0x00, 0x01, 0x30, 0x00, 0x00];
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiPinnedFile> PackageBuildClosure { get; } = Array.AsReadOnly<EfiPinnedFile>(
    [
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets", "17c8492b78a09dacaf17c3721f604551c4e45d592301173c74ec6a4412ef659e"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Publish.targets", "08cd275a39e34abcbdb72f910cea706186277d04088746551fdcde92de18827e"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Windows.targets", "262fa05c2f024b3207dae7da4a41e6afd778eda3794bb491e496ae3ded50a4c4"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.targets", "d1b980014a34e9cc8b28810ca2faef0f1071b2f5675fb9716db5c506cbe29aa7"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/tools/netstandard/ILCompiler.Build.Tasks.dll", "2291cd80d43b11ba8b3f511b1b18c19c68c74c7380e22f017cdd4d4c19e6e888"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/tools/netstandard/ILCompiler.Build.Tasks.deps.json", "52b76ff0992618e15c1b6a4513fc4802b48dfbb778bce0b67ccdf3ae56c35c1d"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/build/Microsoft.NET.ILLink.targets", "4a62e8a4fc04839aedba51146d21e9bee0edcb8079bd85b75d40160cd8e7c601"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/tools/net/ILLink.Tasks.dll", "8a371d95d82959700a975f57f0ca6e3bbb73a8405f20b8fbf5ce2304da630d78"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/tools/net/ILLink.Tasks.deps.json", "6df59b31b28227f58ff6c67cc3170046247d6c314e16c103c1faed80509653fd"),
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiPackage> ConsumedPackageTrees { get; } = Array.AsReadOnly<EfiPackage>(
    [
        new("microsoft.dotnet.ilcompiler", NativePackageVersion, "6yeR3nENReUAbLaOEeX3krUEYCr3ufnbXRnYS3IvM2piBTwKcaA3pzhI2D8XZhj514zX+lU0P/8M04ZrFwTT7w==", 19, 461109L, "88142fc5816ad710abbd782077d0bccc7e05dd69d3c84455558be98ade0eefa3", "restore"),
        new("microsoft.net.illink.tasks", NativePackageVersion, "Mkb6kHfPr4/txc4n/41yUiEBT44Haup2qnyecIO1ADo2QB4YxleZHsOvgYJT6roL1SNuFSpbeNpoe70BJ953Uw==", 43, 5426257L, "93c5b6777c33d943ecc65f55638c0b879a7c73175e197f71daacc78a6621a850", "restore"),
        new("runtime.win-x64.microsoft.dotnet.ilcompiler", NativePackageVersion, "jKElczEvNBZbkzqCilaPufnKkdJKToj5MAO9zenCtSqEacJ1vLTpISuDQfvFeFcn21b1NRK1ekI7n3NYvkDAcA==", 14, 45138567L, "a28fe35ed5c040961950c5224eddd4b6bdf5d49bc754156ab08599488495a7d5", "restore"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "lxoYY14s26hxp+p97TY58i/7bvgenNzdGemjmSoXDXByih0cTgK28h9HzgZV0EBwRlKJffZJAWlamxCWnCRQVQ==", 426, 121081488L, "3e9b553c58600a52450a353d2645c1c3135b81d1841b9a37fd9626f6333e5d5b", "restore-only"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "lxoYY14s26hxp+p97TY58i/7bvgenNzdGemjmSoXDXByih0cTgK28h9HzgZV0EBwRlKJffZJAWlamxCWnCRQVQ==", 426, 121081488L, "3e9b553c58600a52450a353d2645c1c3135b81d1841b9a37fd9626f6333e5d5b", "runtime"),
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiArchive> RestoreArchives { get; } = Array.AsReadOnly<EfiArchive>(
    [
        new("microsoft.dotnet.ilcompiler", NativePackageVersion, "microsoft.dotnet.ilcompiler." + NativePackageVersion + ".nupkg", "40b98ab957285b05930cf84cf76f9f3877c0701dceb4770e49e615efbc25c11b"),
        new("microsoft.net.illink.tasks", NativePackageVersion, "microsoft.net.illink.tasks." + NativePackageVersion + ".nupkg", "1ba431d1a663f151af8269ffb2804970135924848b0e3d386d15c3fa9df6dffc"),
        new("runtime.win-x64.microsoft.dotnet.ilcompiler", NativePackageVersion, "runtime.win-x64.microsoft.dotnet.ilcompiler." + NativePackageVersion + ".nupkg", "6aa4feb026989810cbe1f3c5285b8958817071875521c73f7cb10de586ca20eb"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "microsoft.netcore.app.runtime.nativeaot.win-x64." + NativePackageVersion + ".nupkg", "a527b10ebbecebcc61f96248fa14db76334e43e3936c1480d3081b96ef4e2d68"),
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> ActivePackageImportEdges { get; } = Array.AsReadOnly<string>(
    [
        "sdk|microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets",
        "microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets|microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.targets",
        "microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Windows.targets",
        "microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.targets|microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Publish.targets",
        "sdk|microsoft.net.illink.tasks/" + NativePackageVersion + "/build/Microsoft.NET.ILLink.targets",
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> PackageTaskBindings { get; } = Array.AsReadOnly<string>(
    [
        "ComputeManagedAssembliesToCompileToNative|microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/tools/netstandard/ILCompiler.Build.Tasks.dll",
        "ComputeManagedAssemblies|microsoft.net.illink.tasks/" + NativePackageVersion + "/tools/net/ILLink.Tasks.dll",
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> MsbuildWildcardPropertyNames { get; } = Array.AsReadOnly<string>(
    [
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
        "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets",
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> EvidenceProperties { get; } = Array.AsReadOnly<string>(["schemaVersion", "configuration", "bootPolicy", "runtimeIdentifier", "artifactSha256", "artifactBytes", "linkerMapSha256", "linkResponseSha256", "solutionSha256", "sourceClosureSha256", "sourceClosureInputs", "restoreClosureSha256", "restoreClosureInputs", "restoreArchiveSourceRootPath", "restoreArchiveClosureSha256", "restoreArchiveInputs", "pinnedRestoreSourcePath", "pinnedRestoreSourceSha256", "pinnedRestoreSourceInputs", "restoreImportProjectExtensionProps", "restoreImportProjectExtensionTargets", "restoreExcludeRestorePackageImports", "publishImportProjectExtensionProps", "publishImportProjectExtensionTargets", "publishExcludeRestorePackageImports", "projectExtensionsPath", "projectAssetsPath", "projectAssetsSha256", "packageRootPath", "packageRootKind", "packageClosureSha256", "packages", "packageBuildClosureSha256", "packageBuildClosureInputs", "activePackageImportEdges", "activePackageImportGraphSha256", "packageTaskBindings", "packageTaskBindingsSha256", "postRestoreEvaluation", "sourceProjectSha256", "entrySourceSha256", "entryPointSymbol", "managedEntrySymbol", "serializerSymbol", "serializerExpectedSuccessCookie", "serializerLinkedObjectPath", "serializerObjectPath", "serializerObjectBytes", "serializerObjectSha256", "serializerTargetsSha256", "serializerGeneratorProjectSha256", "serializerGeneratorProgramSha256", "serializerGeneratorWriterSha256", "serializerGeneratorSourceSha256", "instructionPublicationSourceSha256", "runtimeImportAttributeSourceSha256", "methodImplAttributeSourceSha256", "managedAssemblyPath", "managedAssemblySha256", "linkerPath", "linkerVersion", "linkerSha256", "dotnetHostPath", "dotnetHostSha256", "dotnetSdkVersion", "windowsSdkLibraryPath", "windowsSdkVersion", "windowsSdkLibrarySha256", "ilCompilerPath", "ilCompilerDiscoverySourcePath", "ilCompilerPackageVersion", "ilCompilerFileVersion", "ilCompilerSha256", "nativeAotRuntimePackPath", "nativeAotRuntimePackVersion", "nativeAotRuntimePackSha256", "nativeAotRuntimePackFileCount", "nativeAotRuntimePackBytes", "ilCompilerTargetsPath", "ilLinkTargetsPath", "ilLinkTasksAssemblyPath", "ilcBuildTasksPath", "ilcHostPackagePath", "ilcToolsPath", "ilcFrameworkPath", "ilcFrameworkNativePath", "ilcSdkPath", "ilcMibcPath", "languageTargetsPath", "csharpTargetsPath", "commonTargetsPath", "runtimeListSha256", "generatedUtc"]);
}
