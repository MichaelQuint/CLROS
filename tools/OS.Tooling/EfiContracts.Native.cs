namespace OS.Tooling;
public sealed record EfiPinnedFile(string Path, string Sha256);
public sealed record EfiPackage(string Id, string Version, string ContentHashSha512, int FileCount, long TotalBytes, string AggregateSha256, string RootKind);
public sealed record EfiArchive(string Id, string Version, string FileName, string Sha256);
public static partial class EfiContracts
{
    public const string NativePackageVersion = "10.0.12";
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
    public const int CheckedReferenceObjectBytes = 1645, InterfaceDispatchObjectBytes = 828, SimpleCatchObjectBytes = 1350, SingleFinallyObjectBytes = 1707;
    public const string CheckedReferenceObjectSha256 = "e96abf595503b9d1ae38dee740f93ae9b3eb84f05d6395b931e4b5d74f65d808";
    public const string InterfaceDispatchObjectSha256 = "228630b0ad4d6ab298414fda0fcf07e76cb4699a63339fa59ba331afdc3aeb61";
    public const string SimpleCatchObjectSha256 = "dd32dc30808c3b6dd42981df652b95f62236bdc8c45c1daa3dde32bab1c981ab";
    public const string SingleFinallyObjectSha256 = "d24da7653745da1d37f1fc9d286a2e370c32b687a0415e90fc626a597326025a";
    public static System.Collections.ObjectModel.ReadOnlyCollection<string> SourceClosureExplicitPaths { get; } = Array.AsReadOnly<string>(
    [
        "CLROS.slnx", "global.json", "src/OS/OS.csproj", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
        "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj", "tools/OS.X64TextSerializerGenerator/Program.cs",
        "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs", "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs", "tools/OS.X64TextSerializerGenerator/InterfaceDispatchThunkGenerator.cs",
        "tools/OS.X64TextSerializerGenerator/ExceptionHandlingThunkGenerator.cs"
    ]);
    public const int MaximumRuntimeFunctionRecords = 2048, MaximumLinkResponseTokens = 4096;
    public const int SerializerObjectBytes = 336, SerializerSuccessCookie = 1397902403;
    public const string SerializerSymbol = "CLROS_X64_SerializeInstructionPublication";
    public static System.ReadOnlySpan<byte> SerializerBytes => [0x53, 0xb8, 0, 0, 0, 0, 0xb9, 0, 0, 0, 0, 0x0f, 0xa2, 0xb8, 0x43, 0x4c, 0x52, 0x53, 0x5b, 0xc3];
    public static System.ReadOnlySpan<byte> SerializerUnwindBytes => [0x01, 0x01, 0x01, 0x00, 0x01, 0x30, 0x00, 0x00];
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiPinnedFile> PackageBuildClosure { get; } = Array.AsReadOnly<EfiPinnedFile>(
    [
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.DotNet.ILCompiler.SingleEntry.targets", "345c9448182507befa430e80622f68c7ab09debe9c419d98b418f7f1cb79c34e"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Publish.targets", "6fcb3f0491a79fd5a67281b44439e0685c17fe4c848a07477ab2a57a10a68c60"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.Windows.targets", "547266fbf1e772518edda8194413ce99906d3deb8bef074bbcca1033c31bc54c"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/build/Microsoft.NETCore.Native.targets", "22516102af0e4bdafa004795d3bbc30d617efe5bb7b809005d1ea3a00c436f6d"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/tools/netstandard/ILCompiler.Build.Tasks.dll", "dbd168e677d11e9a3daeac5839c63a22d66b3719d5f9d1b4b7dffe3eda05c6c0"),
        new("microsoft.dotnet.ilcompiler/" + NativePackageVersion + "/tools/netstandard/ILCompiler.Build.Tasks.deps.json", "e7e496a9c07e33a18adaf561c5ec26fb283b813d10fb418bc8dd651c8d013e03"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/build/Microsoft.NET.ILLink.targets", "f1bbe7e9e79badb900a594691fe4c6738e906d911ee3e50975434d09035a2568"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/tools/net/ILLink.Tasks.dll", "4d5509b95e246eef7b3fb5f38cc200bf3c199a3268cb62293d02af9d4776e01e"),
        new("microsoft.net.illink.tasks/" + NativePackageVersion + "/tools/net/ILLink.Tasks.deps.json", "518f0256eb3699c88d7c798cbb1f8cfd42845754f4cba8726b499a25d20da6eb"),
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiPackage> ConsumedPackageTrees { get; } = Array.AsReadOnly<EfiPackage>(
    [
        new("microsoft.dotnet.ilcompiler", NativePackageVersion, "AawF393Q+VkdrnrnI1gu612zh5iqpa1AGSvnKCQ3IkMgSKJXIQbO1sXYRgEzOU4f0cPZ6MaCCF29xZSGWlmbuQ==", 20, 404890L, "0262658f79a7fd731130a31b423b28f8bf11810c6bf46f09cbd014af7d9e25fd", "restore"),
        new("microsoft.net.illink.tasks", NativePackageVersion, "xi+BDjFpW+Sb+MHFHaH6Y/gV9I8BluFwRXc1QyCdoZbIK26eNiBeFuMTe/FMwc33G1wdHCyDg7CVTmb8OdQrMQ==", 42, 5339152L, "11d0abb4c856d2c2e8ccce942fa9843df34a026d2c589936e5878b228f859834", "restore"),
        new("runtime.win-x64.microsoft.dotnet.ilcompiler", NativePackageVersion, "clsgU9GnioCJ+PBzQTCoJHxLXRU+7O/BzYrwRT8CUP7jgyYjNgJmNsQ/kbzAA7MG5kDvFoFvIBGHGzYdINh/EQ==", 13, 37502842L, "117ebd47725e7f105b1f3014e610839b88168d8607877e126827316eb1355bc7", "restore"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "MPm78CKSJf8Sb0nbXgvzedp2uwFQX4JONl618NhEeAEznK9JLzQ6ETfJZ+UcoW5ScTqa27a+JGQDXD3bUAMTrA==", 406, 109239870L, "c4048f1c42c12a119d30b5d432e957a961689d72ea3fe44091ade5d54d1324be", "restore-only"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "MPm78CKSJf8Sb0nbXgvzedp2uwFQX4JONl618NhEeAEznK9JLzQ6ETfJZ+UcoW5ScTqa27a+JGQDXD3bUAMTrA==", 406, 109239870L, "c4048f1c42c12a119d30b5d432e957a961689d72ea3fe44091ade5d54d1324be", "runtime"),
    ]);
    public static System.Collections.ObjectModel.ReadOnlyCollection<EfiArchive> RestoreArchives { get; } = Array.AsReadOnly<EfiArchive>(
    [
        new("microsoft.dotnet.ilcompiler", NativePackageVersion, "microsoft.dotnet.ilcompiler." + NativePackageVersion + ".nupkg", "0b340ade975c2cf3d93d518adddec1e5d910c090b7756e6d37d19b772edc7b8d"),
        new("microsoft.net.illink.tasks", NativePackageVersion, "microsoft.net.illink.tasks." + NativePackageVersion + ".nupkg", "37aa984da51fdc01cc7d57a8647893392f3539cdd5a1d32ce4982be5309bc08b"),
        new("runtime.win-x64.microsoft.dotnet.ilcompiler", NativePackageVersion, "runtime.win-x64.microsoft.dotnet.ilcompiler." + NativePackageVersion + ".nupkg", "7f63d47aea10cd7ac3dd7a2ca04474c7cb683e36ef86b49190382b0c891fbe99"),
        new("microsoft.netcore.app.runtime.nativeaot.win-x64", NativePackageVersion, "microsoft.netcore.app.runtime.nativeaot.win-x64." + NativePackageVersion + ".nupkg", "94e38c861bdd0ef6e5f04492538b66ada2f30876907363a61aa9f03f42fb3406"),
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
