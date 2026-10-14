using System.Diagnostics;
using System.Text.Json.Nodes;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

internal sealed partial class EfiVerification
{
    private void VerifyProvenance(string project)
    {
        string linker = EvidencePath("linkerPath", "linker_identity_path_invalid");
        var linkerFile = Open(linker, "linker_identity");
        Require(Text("linkerVersion").Length != 0 && linkerFile.Sha256 == Text("linkerSha256"), "linker_identity_mismatch");
        Require(FileVersionInfo.GetVersionInfo(linker).FileVersion is string linkerVersion && linkerVersion.Length != 0 && linkerVersion == Text("linkerVersion"), "linker_version_mismatch");
        configuration = Text("configuration"); Require(configuration is "Release" or "Debug", "configuration_invalid");
        Require(Text("runtimeIdentifier") == "win-x64", "build_identity_invalid");
        string intermediate = Path.Combine(Root, "src/OS/obj", configuration, "net11.0/win-x64");
        extensions = EvidencePath("projectExtensionsPath", "project_extensions_path_invalid");
        Require(SamePath(extensions, Path.Combine(intermediate, "nuget")), "project_extensions_path_not_canonical");
        Require(Directory.Exists(extensions), "project_extensions_directory_missing");
        AuthorityFiles.RequirePlainPath(Root, extensions, "project_extensions_directory_reparse_point");
        Require(!Path.Exists(Path.Combine(extensions, "disabled-user-extensions")), "post_restore_user_extensions_path_present");
        string[] restorePaths = RestorePaths(configuration);
        Require(Directory.EnumerateFileSystemEntries(extensions).Count() == restorePaths.Length && !Directory.EnumerateDirectories(extensions).Any(), "restore_closure_entry_count_mismatch");
        VerifySnapshot(locks, Root, restorePaths, evidence["restoreClosureInputs"], "restore_closure", Text("restoreClosureSha256"), 4194304);
        string assets = EvidencePath("projectAssetsPath", "project_assets_path_invalid");
        Require(SamePath(assets, Path.Combine(extensions, "project.assets.json")), "project_assets_path_not_canonical");
        Require(Open(assets, "project_assets", 4194304, root: extensions).Sha256 == Text("projectAssetsSha256"), "project_assets_hash_mismatch");
        packages = EvidencePath("packageRootPath", "package_root_path_invalid");
        Require(Text("packageRootKind") == "isolated" && SamePath(packages, Path.Combine(intermediate, "packages")) && Directory.Exists(packages), "package_root_contract_invalid");
        AuthorityFiles.RequirePlainPath(Root, packages, "package_root_reparse_point");
        string pinned = EvidencePath("pinnedRestoreSourcePath", "pinned_restore_source_path_invalid");
        Require(SamePath(pinned, Path.Combine(intermediate, "restore-source")) && Directory.Exists(pinned), "pinned_restore_source_not_canonical");
        string pinnedRelative = RelativePath(Root, pinned, "pinned_restore_source_path_invalid");
        string[] pinnedPaths = new[] { pinnedRelative + "/NuGet.Config" }.Concat(EfiContracts.RestoreArchives.Select(a => pinnedRelative + "/" + a.FileName)).Order(StringComparer.Ordinal).ToArray();
        Require(Directory.EnumerateFileSystemEntries(pinned).Count() == pinnedPaths.Length && !Directory.EnumerateDirectories(pinned).Any(), "pinned_restore_source_entry_count_mismatch");
        var pinnedHashes = EfiContracts.RestoreArchives.ToDictionary(a => pinnedRelative + "/" + a.FileName, a => a.Sha256, StringComparer.Ordinal);
        VerifySnapshot(locks, Root, pinnedPaths, evidence["pinnedRestoreSourceInputs"], "pinned_restore_source", Text("pinnedRestoreSourceSha256"), pins: pinnedHashes);
        VerifyPinnedConfig(Open(Path.Combine(pinned, "NuGet.Config"), "pinned_restore_config", 65536, root: pinned).ReadUtf8(65536, "pinned_restore_config_invalid"), pinned);
        string global = Path.GetFullPath(Environment.GetEnvironmentVariable("NUGET_PACKAGES") is string custom && !string.IsNullOrWhiteSpace(custom) ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages"));
        string archiveRoot = EvidencePath("restoreArchiveSourceRootPath", "restore_archive_source_root_path_invalid");
        Require(SamePath(archiveRoot, global), "restore_archive_source_root_not_canonical");
        var archivePaths = EfiContracts.RestoreArchives.Select(a => a.Id + "/" + a.Version + "/" + a.FileName).ToArray();
        var archivePins = EfiContracts.RestoreArchives.ToDictionary(a => a.Id + "/" + a.Version + "/" + a.FileName, a => a.Sha256, StringComparer.Ordinal);
        VerifySnapshot(locks, archiveRoot, archivePaths, evidence["restoreArchiveInputs"], "restore_archive", Text("restoreArchiveClosureSha256"), pins: archivePins);
        EfiRestore.VerifyGenerated(locks, project, extensions, packages);
        dotnet = EvidencePath("dotnetHostPath", "dotnet_host_path_invalid");
        string actualDotnet = HostTools.FindExecutable(null, "", "dotnet") ?? throw new ToolFailure("dotnet_host_unavailable");
        Require(SamePath(dotnet, actualDotnet) && Open(dotnet, "dotnet_host").Sha256 == Text("dotnetHostSha256"), "dotnet_host_identity_mismatch");
        string sdkVersion = Text("dotnetSdkVersion");
        Require(System.Text.RegularExpressions.Regex.IsMatch(sdkVersion, @"^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$"), "dotnet_sdk_version_invalid");
        sdk = Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", sdkVersion);
        Require(Directory.Exists(sdk), "dotnet_sdk_identity_mismatch");
        JsonObject globalJson = Parse(Open(Path.Combine(Root, "global.json"), "global_json", 65536, root: Root).ReadUtf8(65536, "global_json_sdk_contract_invalid"), "global_json_sdk_contract_invalid");
        var sdkContract = Object(globalJson["sdk"], "global_json_sdk_contract_invalid");
        Require(EfiVerify.TestSdkExactVersion(sdkVersion, String(sdkContract["version"], "global_json_sdk_contract_invalid")) && String(sdkContract["rollForward"], "global_json_sdk_contract_invalid") == "disable" && sdkContract["allowPrerelease"] is JsonValue prerelease && prerelease.TryGetValue<bool>(out bool allowPrerelease) && allowPrerelease, "dotnet_sdk_global_contract_mismatch");
        string windowsLibrary = EvidencePath("windowsSdkLibraryPath", "windows_sdk_library_path_invalid"), windowsVersion = Text("windowsSdkVersion");
        Require(windowsVersion.Length != 0 && Path.GetFileName(windowsLibrary).Equals("kernel32.lib", StringComparison.OrdinalIgnoreCase) && windowsLibrary.Contains("\\Lib\\" + windowsVersion + "\\um\\x64\\", StringComparison.OrdinalIgnoreCase) && Open(windowsLibrary, "windows_sdk_library").Sha256 == Text("windowsSdkLibrarySha256"), "windows_sdk_identity_mismatch");
        string ilc = EvidencePath("ilCompilerPath", "ilcompiler_path_invalid"), expectedIlc = Path.Combine(packages, "runtime.win-x64.microsoft.dotnet.ilcompiler", EfiContracts.NativePackageVersion, "tools/ilc.exe");
        Require(Text("ilCompilerPackageVersion") == EfiContracts.NativePackageVersion && Text("ilCompilerFileVersion").Length != 0 && SamePath(ilc, expectedIlc) && Open(ilc, "ilcompiler", root: packages).Sha256 == Text("ilCompilerSha256"), "ilcompiler_identity_mismatch");
        string discovery = EvidencePath("ilCompilerDiscoverySourcePath", "ilcompiler_discovery_source_path_invalid");
        Require(SamePath(discovery, Path.Combine(global, "runtime.win-x64.microsoft.dotnet.ilcompiler", EfiContracts.NativePackageVersion, "tools/ilc.exe")) && Open(discovery, "ilcompiler_discovery_source", root: global).Sha256 == Text("ilCompilerSha256"), "ilcompiler_discovery_source_identity_mismatch");
        Require(FileVersionInfo.GetVersionInfo(ilc).FileVersion is string ilcVersion && ilcVersion.Length != 0 && ilcVersion == Text("ilCompilerFileVersion"), "ilcompiler_version_mismatch");
        runtimePack = EvidencePath("nativeAotRuntimePackPath", "nativeaot_runtime_pack_path_invalid");
        Require(Text("nativeAotRuntimePackVersion") == EfiContracts.NativePackageVersion && Directory.Exists(runtimePack) && Path.GetFileName(runtimePack) == EfiContracts.NativePackageVersion && (Path.GetFileName(Path.GetDirectoryName(runtimePack)) ?? "").Equals("microsoft.netcore.app.runtime.nativeaot.win-x64", StringComparison.OrdinalIgnoreCase), "nativeaot_runtime_pack_identity_mismatch");
        AuthorityFiles.RequirePlainPath(runtimePack, runtimePack, "nativeaot_runtime_pack_reparse_point");
        Require(Open(Path.Combine(runtimePack, "data/RuntimeList.xml"), "nativeaot_runtime_list", MaximumTextBytes, root: runtimePack).Sha256 == Text("runtimeListSha256"), "nativeaot_runtime_list_identity_mismatch");
        VerifyPackages(pinned);
        Dictionary<string, string> canonical = EfiMsbuild.CanonicalPaths(packages, runtimePack, sdk);
        foreach (var pair in canonical)
        {
            if (pair.Key == "RuntimePackagePath") continue;
            string evidenceName = pair.Key switch { "ILLinkTasksAssembly" => "ilLinkTasksAssemblyPath", "LanguageTargets" => "languageTargetsPath", "CSharpTargetsPath" => "csharpTargetsPath", _ => char.ToLowerInvariant(pair.Key[0]) + pair.Key[1..] };
            if (pair.Key == "ILCompilerTargetsPath") evidenceName = "ilCompilerTargetsPath";
            if (pair.Key == "ILLinkTargetsPath") evidenceName = "ilLinkTargetsPath";
            string value = Text(evidenceName);
            bool trailing = Path.EndsInDirectorySeparator(pair.Value), optional = pair.Key == "IlcMibcPath";
            Require(!trailing || value.EndsWith(Path.DirectorySeparatorChar), "package_property_path_trailing_separator_missing", "property=" + evidenceName);
            string full = Absolute(value, "package_property_path_invalid");
            bool existsWithExpectedKind = optional && !Path.Exists(full) ||
                (trailing || pair.Key == "IlcHostPackagePath" ? Directory.Exists(full) : File.Exists(full));
            Require(SamePath(full, pair.Value) && existsWithExpectedKind, "package_property_path_not_canonical", "property=" + evidenceName);
            if (Path.Exists(full))
            {
                AuthorityFiles.RequirePlainPath(Path.GetPathRoot(full)!, full, "package_property_path_reparse_point");
                if (File.Exists(full)) _ = Open(full, "package_property_path");
            }
        }
        JsonObject snapshot = EfiMsbuild.Evaluate(Root, dotnet, sdk, configuration, extensions, packages, runtimePack, profile, emit);
        EfiMsbuild.VerifyEvaluation(evidence["postRestoreEvaluation"], snapshot);
    }
    private void VerifyPackages(string pinned)
    {
        List<string> actualVersions = [];
        foreach (string id in Directory.EnumerateFileSystemEntries(packages))
        {
            Require(Directory.Exists(id) && (File.GetAttributes(id) & FileAttributes.ReparsePoint) == 0, "isolated_package_root_shape_invalid");
            foreach (string version in Directory.EnumerateFileSystemEntries(id))
            {
                Require(Directory.Exists(version) && (File.GetAttributes(version) & FileAttributes.ReparsePoint) == 0, "isolated_package_version_shape_invalid");
                actualVersions.Add(Path.GetFileName(id) + "/" + Path.GetFileName(version));
            }
        }
        string[] expectedVersions = EfiContracts.ConsumedPackageTrees.Where(p => p.RootKind != "runtime").Select(p => p.Id + "/" + p.Version).Order(StringComparer.Ordinal).ToArray();
        Require(actualVersions.Count == expectedVersions.Length, "isolated_package_version_count_invalid");
        Require(actualVersions.Order(StringComparer.Ordinal).SequenceEqual(expectedVersions, StringComparer.Ordinal), "isolated_package_version_set_invalid");
        JsonArray entries = Array(evidence["packages"], "package_evidence_shape_invalid");
        Require(entries.Count == EfiContracts.ConsumedPackageTrees.Count, "package_evidence_count_mismatch");
        List<string> closure = []; EfiSnapshot? runtime = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var contract = EfiContracts.ConsumedPackageTrees[i]; JsonNode? entry = entries[i];
            Shape(entry, ["id", "version", "rootKind", "rootPath", "contentHashSha512", "fileCount", "totalBytes", "aggregateSha256", "metadataSha256", "inputs"], "package_evidence_shape_invalid");
            string Value(string name) => String(entry![name], "package_evidence_shape_invalid");
            Require(Value("id") == contract.Id && Value("version") == contract.Version && Value("rootKind") == contract.RootKind && Value("contentHashSha512") == contract.ContentHashSha512, "package_evidence_identity_mismatch");
            string root = contract.RootKind == "runtime" ? runtimePack : Path.Combine(packages, contract.Id, contract.Version);
            Require(SamePath(Absolute(Value("rootPath"), "package_evidence_root_path_invalid"), root), "package_evidence_root_path_mismatch");
            var metadataFile = Open(Path.Combine(root, ".nupkg.metadata"), "package_metadata", 65536, root: root);
            var metadata = Parse(metadataFile.ReadUtf8(65536, "package_metadata_invalid"), "package_metadata_invalid");
            Shape(metadata, ["version", "contentHash", "source"], "package_metadata_shape_invalid");
            Require(Integer(metadata["version"], "version") == 2 && String(metadata["contentHash"], "package_metadata_contract_invalid") == contract.ContentHashSha512 && !string.IsNullOrWhiteSpace(String(metadata["source"], "package_metadata_contract_invalid")), "package_metadata_contract_invalid");
            if (contract.RootKind != "runtime") Require(SamePath(String(metadata["source"], "package_metadata_source_invalid"), pinned), "package_metadata_source_mismatch");
            Require(IsHash(Value("metadataSha256")) && Value("metadataSha256") == metadataFile.Sha256, "package_metadata_hash_mismatch");
            EfiSnapshot tree = PackageTree(locks, root);
            Require(tree.FileCount == contract.FileCount && tree.TotalBytes == contract.TotalBytes && tree.AggregateSha256 == contract.AggregateSha256 && Integer(entry!["fileCount"], "fileCount") == tree.FileCount && Integer(entry["totalBytes"], "totalBytes") == tree.TotalBytes && Value("aggregateSha256") == tree.AggregateSha256, "package_tree_contract_mismatch", "package=" + contract.Id);
            VerifyPackageInputs(entry!["inputs"], tree);
            closure.Add(contract.Id + "|" + contract.Version + "|" + contract.RootKind + "|" + tree.AggregateSha256);
            if (contract.RootKind == "runtime") runtime = tree;
        }
        Require(HashLines(closure) == Text("packageClosureSha256"), "package_closure_aggregate_mismatch");
        Require(runtime is not null, "nativeaot_runtime_package_snapshot_missing");
        Require(Number("nativeAotRuntimePackFileCount") == runtime!.FileCount && Number("nativeAotRuntimePackBytes") == runtime.TotalBytes && Text("nativeAotRuntimePackSha256") == runtime.AggregateSha256, "nativeaot_runtime_pack_tree_mismatch");
        VerifySnapshot(locks, packages, EfiContracts.PackageBuildClosure.Select(p => p.Path), evidence["packageBuildClosureInputs"], "package_build_closure", Text("packageBuildClosureSha256"), pins: EfiContracts.PackageBuildClosure.ToDictionary(p => p.Path, p => p.Sha256, StringComparer.Ordinal));
        foreach ((string property, IReadOnlyList<string> expected, string hashProperty, string mismatch, string hashMismatch) in new[] { ("activePackageImportEdges", (IReadOnlyList<string>)EfiContracts.ActivePackageImportEdges, "activePackageImportGraphSha256", "active_package_import_edges_mismatch", "active_package_import_graph_hash_mismatch"), ("packageTaskBindings", (IReadOnlyList<string>)EfiContracts.PackageTaskBindings, "packageTaskBindingsSha256", "package_task_bindings_mismatch", "package_task_bindings_hash_mismatch") })
        {
            Require(Array(evidence[property], mismatch).Select(n => String(n, mismatch)).SequenceEqual(expected, StringComparer.Ordinal), mismatch);
            Require(HashLines(expected) == Text(hashProperty), hashMismatch);
        }
    }
}
