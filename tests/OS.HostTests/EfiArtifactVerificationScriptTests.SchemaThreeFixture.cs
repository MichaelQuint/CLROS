using System.Diagnostics;
using OS.Tooling;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static partial class EfiArtifactVerificationScriptTests
{
    private sealed record SchemaThreeFixture(string RepositoryRoot, string ProjectExtensionsPath, string ProjectAssetsPath, string PinnedRestoreSourcePath, string PackageRootPath, string RestoreArchiveSourceRootPath, SourceClosureFixture RestoreClosure, SourceClosureFixture PinnedRestoreSourceClosure, SourceClosureFixture RestoreArchiveClosure, string PackageClosureSha256, Dictionary<string, object?>[] Packages, SourceClosureFixture PackageBuildClosure, string[] ActivePackageImportEdges, string ActivePackageImportGraphSha256, string[] PackageTaskBindings, string PackageTaskBindingsSha256, Dictionary<string, object?> PostRestoreEvaluation, string IlCompilerPath, string IlCompilerDiscoverySourcePath, string NativeAotRuntimePackPath, string NativeAotRuntimePackSha256, int NativeAotRuntimePackFileCount, long NativeAotRuntimePackBytes, string IlCompilerTargetsPath, string IlLinkTargetsPath, string IlLinkTasksAssemblyPath, string IlcBuildTasksPath, string IlcHostPackagePath, string IlcToolsPath, string IlcFrameworkPath, string IlcFrameworkNativePath, string IlcSdkPath, string IlcMibcPath, string LanguageTargetsPath, string CSharpTargetsPath, string CommonTargetsPath);
}
