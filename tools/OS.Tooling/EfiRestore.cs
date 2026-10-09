using System.Text.Json.Nodes;
using System.Xml.Linq;
using static OS.Tooling.EfiPeImage;
using static OS.Tooling.EfiEvidence;

namespace OS.Tooling;

public static class EfiRestore
{
    public static void VerifyGenerated(AuthorityFiles locks, string projectPath, string extensions, string packages)
    {
        string Read(string filename, string prefix) => locks.Open(extensions, Path.Combine(extensions, filename), prefix, prefix, 4194304).ReadUtf8(4194304, prefix + "_invalid");
        XDocument props = Xml(Read("OS.csproj.nuget.g.props", "restore_generated_props"), "restore_generated_props_invalid");
        XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
        XElement? root = props.Root;
        Require(root is not null && root.Name == ns + "Project" && root.Attributes().Count() == 2 && (string?)root.Attribute("ToolsVersion") == "14.0" && (string?)root.Attribute("xmlns") == ns.NamespaceName, "restore_generated_props_root_invalid");
        Require(props.Descendants().All(e => e.Name.Namespace == ns), "restore_generated_props_namespace_invalid");
        XElement[] children = root!.Elements().ToArray();
        string[] childNames = ["PropertyGroup", "ItemGroup", "ImportGroup", "PropertyGroup"]; int[] childCounts = [7, 1, 2, 3];
        const string condition = " '$(ExcludeRestorePackageImports)' != 'true' ";
        Require(children.Length == childNames.Length, "restore_generated_props_child_count_invalid");
        for (int i = 0; i < children.Length; i++)
        {
            Require(children[i].Name == ns + childNames[i] && children[i].Attributes().Count() == 1 && (string?)children[i].Attribute("Condition") == condition, "restore_generated_props_child_contract_invalid", "index=" + i);
            Require(children[i].Elements().Count() == childCounts[i], "restore_generated_props_direct_child_count_invalid", "index=" + i);
        }
        Require(!props.Descendants().Any(e => e.Name.LocalName is "Target" or "UsingTask" or "Choose" or "When" or "Otherwise" or "ItemDefinitionGroup" || e.Parent?.Name.LocalName == "PropertyGroup" && new[] { "Custom", "Before", "After" }.Any(e.Name.LocalName.Contains)), "restore_generated_props_executable_surface_invalid");
        string[] expectedImports = [@"$(NuGetPackageRoot)\microsoft.net.illink.tasks\" + EfiContracts.NativePackageVersion + @"\build\Microsoft.NET.ILLink.Tasks.props", @"$(NuGetPackageRoot)\microsoft.dotnet.ilcompiler\" + EfiContracts.NativePackageVersion + @"\build\Microsoft.DotNet.ILCompiler.props"];
        XElement[] imports = props.Descendants(ns + "Import").ToArray(); HashSet<string> seen = new(StringComparer.Ordinal);
        Require(imports.Length == expectedImports.Length, "restore_generated_props_import_count_invalid");
        foreach (var import in imports)
        {
            string name = (string?)import.Attribute("Project") ?? "";
            Require(expectedImports.Contains(name, StringComparer.Ordinal) && seen.Add(name) && import.Attributes().Count() == 2 && (string?)import.Attribute("Condition") == "Exists('" + name + "')" && import.Parent?.Name == ns + "ImportGroup" && (string?)import.Parent.Attribute("Condition") == condition, "restore_generated_props_import_invalid");
        }
        string user = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        string userPrefix = user + Path.DirectorySeparatorChar;
        string generatedPackageRoot = packages.StartsWith(userPrefix, StringComparison.OrdinalIgnoreCase) ? @"$(UserProfile)\" + packages[userPrefix.Length..] : packages;
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["RestoreSuccess"] = "True", ["RestoreTool"] = "NuGet", ["ProjectAssetsFile"] = "$(MSBuildThisFileDirectory)project.assets.json", ["NuGetPackageRoot"] = generatedPackageRoot,
            ["NuGetProjectStyle"] = "PackageReference", ["NuGetToolVersion"] = "7.0.0", ["NuGetPackageFolders"] = packages,
            ["Pkgruntime_win-x64_Microsoft_DotNet_ILCompiler"] = Path.Combine(packages, "runtime.win-x64.microsoft.dotnet.ilcompiler", EfiContracts.NativePackageVersion),
            ["PkgMicrosoft_NET_ILLink_Tasks"] = Path.Combine(packages, "microsoft.net.illink.tasks", EfiContracts.NativePackageVersion), ["PkgMicrosoft_DotNet_ILCompiler"] = Path.Combine(packages, "microsoft.dotnet.ilcompiler", EfiContracts.NativePackageVersion)
        };
        XElement[] properties = props.Descendants(ns + "PropertyGroup").Elements().ToArray(); seen.Clear();
        Require(properties.Length == expected.Count, "restore_generated_props_property_count_invalid");
        foreach (var property in properties)
        {
            string name = property.Name.LocalName;
            Require(expected.ContainsKey(name) && seen.Add(name) && property.Attributes().Count() == 1 && (string?)property.Attribute("Condition") == " '$(" + name + ")' == '' ", "restore_generated_props_property_invalid", "name=" + name);
            Require(property.Value == expected[name], name == "NuGetPackageFolders" ? "restore_generated_package_folder_property_invalid" : "restore_generated_props_property_value_invalid", "name=" + name);
        }
        XDocument targets = Xml(Read("OS.csproj.nuget.g.targets", "restore_generated_targets"), "restore_generated_targets_invalid");
        var targetRoot = targets.Root;
        Require(targetRoot is not null && targetRoot.Name == ns + "Project" && targetRoot.Attributes().Count() == 2 && (string?)targetRoot.Attribute("ToolsVersion") == "14.0" && (string?)targetRoot.Attribute("xmlns") == ns.NamespaceName && !targetRoot.Elements().Any(), "restore_generated_targets_not_empty");
        XElement[] roots = props.Descendants(ns + "SourceRoot").ToArray();
        Require(roots.Length == 1 && roots[0].Attributes().Count() == 1 && (string?)roots[0].Attribute("Include") == packages + Path.DirectorySeparatorChar && roots[0].Parent?.Name == ns + "ItemGroup" && (string?)roots[0].Parent!.Attribute("Condition") == condition, "restore_generated_source_root_invalid");
        JsonObject assets = Parse(Read("project.assets.json", "restore_project_assets"), "restore_project_assets_invalid");
        Shape(assets, ["version", "targets", "libraries", "projectFileDependencyGroups", "packageFolders", "project"], "restore_project_assets_root_invalid");
        Require(Integer(assets["version"], "version") == 4, "restore_project_assets_version_invalid");
        var libraries = Object(assets["libraries"], "restore_project_assets_library_count_invalid");
        string[] names = ["Microsoft.DotNet.ILCompiler/" + EfiContracts.NativePackageVersion, "Microsoft.NET.ILLink.Tasks/" + EfiContracts.NativePackageVersion, "runtime.win-x64.Microsoft.DotNet.ILCompiler/" + EfiContracts.NativePackageVersion];
        Require(libraries.Count == names.Length, "restore_project_assets_library_count_invalid");
        foreach (var pair in libraries)
        {
            int index = System.Array.IndexOf(names, pair.Key); Require(index >= 0, "restore_project_assets_library_unexpected", pair.Key);
            EfiPackage contract = EfiContracts.ConsumedPackageTrees[index];
            Shape(pair.Value, ["sha512", "type", "path", "hasTools", "files"], "restore_project_assets_library_shape_invalid");
            var files = Array(pair.Value!["files"], "restore_project_assets_library_contract_invalid");
            Require(String(pair.Value["sha512"], "restore_project_assets_library_contract_invalid") == contract.ContentHashSha512 && String(pair.Value["type"], "restore_project_assets_library_contract_invalid") == "package" && String(pair.Value["path"], "restore_project_assets_library_contract_invalid") == contract.Id + "/" + contract.Version && pair.Value["hasTools"] is JsonValue b && b.TryGetValue<bool>(out bool tools) && tools && files.Count == contract.FileCount, "restore_project_assets_library_contract_invalid");
            seen.Clear();
            foreach (var file in files)
            {
                string path = String(file, "restore_project_assets_library_file_invalid");
                Require(!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains("..", StringComparison.Ordinal) && !path.Contains(':') && seen.Add(path), "restore_project_assets_library_file_invalid");
            }
        }
        JsonObject targetObjects = Object(assets["targets"], "restore_project_assets_target_count_invalid");
        Require(targetObjects.Count == 2, "restore_project_assets_target_count_invalid");
        foreach (string targetName in new[] { "net10.0", "net10.0/win-x64" })
        {
            var target = Object(targetObjects[targetName], "restore_project_assets_target_missing");
            Require(target.Count == names.Length, "restore_project_assets_target_library_count_invalid");
            foreach (var pair in target)
            {
                int index = System.Array.IndexOf(names, pair.Key); Require(index >= 0, "restore_project_assets_target_library_unexpected");
                bool compiler = index == 0, linker = index == 1, rid = targetName.EndsWith("/win-x64", StringComparison.Ordinal);
                Shape(pair.Value, compiler && rid ? ["type", "dependencies", "build"] : compiler || linker ? ["type", "build"] : ["type"], "restore_project_assets_target_library_shape_invalid");
                Require(String(pair.Value!["type"], "restore_project_assets_target_library_type_invalid") == "package", "restore_project_assets_target_library_type_invalid");
                if (compiler || linker)
                {
                    string path = compiler ? "build/Microsoft.DotNet.ILCompiler.props" : "build/Microsoft.NET.ILLink.Tasks.props";
                    Shape(pair.Value["build"], [path], "restore_project_assets_build_import_invalid");
                    Shape(pair.Value["build"]![path], [], "restore_project_assets_build_import_invalid");
                }
                if (compiler && rid)
                {
                    Shape(pair.Value["dependencies"], ["runtime.win-x64.Microsoft.DotNet.ILCompiler"], "restore_project_assets_compiler_dependency_invalid");
                    Require(String(pair.Value["dependencies"]!["runtime.win-x64.Microsoft.DotNet.ILCompiler"], "restore_project_assets_compiler_dependency_invalid") == EfiContracts.NativePackageVersion, "restore_project_assets_compiler_dependency_invalid");
                }
            }
        }
        var folders = Object(assets["packageFolders"], "restore_project_assets_package_folder_invalid");
        Require(folders.Count == 1 && folders.First().Key.Equals(packages, StringComparison.OrdinalIgnoreCase), "restore_project_assets_package_folder_invalid");
        var restore = Object(assets["project"]?["restore"], "restore_project_assets_restore_contract_missing");
        Require(SamePath(String(restore["projectPath"], "restore_project_assets_restore_path_invalid"), projectPath) && SamePath(String(restore["packagesPath"], "restore_project_assets_restore_path_invalid"), packages) && SamePath(String(restore["outputPath"], "restore_project_assets_restore_path_invalid"), extensions + Path.DirectorySeparatorChar), "restore_project_assets_restore_path_contract_invalid");
    }
}
