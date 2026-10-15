using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class ToolingBuildInputTests
{
    private static string NewRoot()
    {
        string root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "tooling-input-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "Example"));
        return root;
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Manifest(string root, params string[] paths) =>
        "CLROS_BUILD_INPUTS_V1\nROOT|" + root + "\nGLOB|tools/**/*.cs\nGLOB|global.json\n" +
        string.Join('\n', paths.Select(path => "FILE|" + path + "|" + Hash(path))) + "\n";
    private static IReadOnlyList<string> Check(string manifest, string root) => ToolingBuildInputs.Validate(new StringReader(manifest), root);

    [HostTest("tooling inputs reject changed missing and newly added source", HostLane.Fast)]
    private static void Mutations()
    {
        string root = NewRoot(), source = Path.Combine(root, "tools", "Example", "Program.cs"), config = Path.Combine(root, "global.json");
        File.WriteAllText(source, "class Example {}\n"); File.WriteAllText(config, "{}\n");
        string manifest = Manifest(root, source, config);
        HostCheck.Equal(0, Check(manifest, root).Count);
        File.WriteAllText(source, "class Changed {}\n");
        HostCheck.True(Check(manifest, root).Single().StartsWith("stale_build_input ", StringComparison.Ordinal), "changed raw source");
        File.WriteAllText(source, "class Example {}\n"); File.Delete(config);
        HostCheck.True(Check(manifest, root).Single().StartsWith("missing_build_input ", StringComparison.Ordinal), "missing global config");
        File.WriteAllText(config, "{}\n");
        string nested = Path.Combine(root, "tools", "Example", "Nested"); Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Added.cs"), "class Added {}\n");
        HostCheck.True(Check(manifest, root).Single().StartsWith("unrecorded_build_input ", StringComparison.Ordinal), "new nested source");
    }

    [HostTest("tooling inputs relocate exactly and ignore documentation and build outputs", HostLane.Fast)]
    private static void Relocation()
    {
        string original = NewRoot(), relocated = NewRoot();
        foreach (string root in new[] { original, relocated })
        { File.WriteAllText(Path.Combine(root, "tools", "Example", "Program.cs"), "class Example {}\r\n"); File.WriteAllText(Path.Combine(root, "global.json"), "{}\n"); }
        string manifest = Manifest(original, Path.Combine(original, "tools", "Example", "Program.cs"), Path.Combine(original, "global.json"));
        File.WriteAllText(Path.Combine(original, "tools", "Example", "Program.cs"), "the original location is no longer authoritative");
        Directory.CreateDirectory(Path.Combine(relocated, "docs")); File.WriteAllText(Path.Combine(relocated, "docs", "changed.md"), "new docs");
        foreach (string ignored in new[] { "bin", "obj", "artifacts" })
        { string path = Path.Combine(relocated, "tools", "Example", ignored); Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "Generated.cs"), "generated"); }
        HostCheck.Equal(0, Check(manifest, relocated).Count);
        File.WriteAllText(Path.Combine(relocated, "tools", "Example", "Program.cs"), "class Example {}\n");
        HostCheck.True(Check(manifest, relocated).Single().StartsWith("stale_build_input ", StringComparison.Ordinal), "freshness preserves exact raw line endings");
    }

    [HostTest("tooling source manifest rejects malformed paths and binds its compiled inventory", HostLane.Fast)]
    private static void ManifestShape()
    {
        string root = NewRoot(), source = Path.Combine(root, "tools", "Example", "Program.cs"), config = Path.Combine(root, "global.json");
        File.WriteAllText(source, "class Example {}\n"); File.WriteAllText(config, "{}\n");
        string manifest = Manifest(root, source, config);
        foreach (string malformed in new[] { manifest.Replace("CLROS_BUILD_INPUTS_V1", "UNKNOWN", StringComparison.Ordinal),
            manifest.Replace("GLOB|tools/**/*.cs", "GLOB|../*.cs", StringComparison.Ordinal),
            manifest.Replace("GLOB|tools/**/*.cs", "GLOB|D:/tools/**/*.cs", StringComparison.Ordinal),
            manifest.Replace(Hash(source), "invalid", StringComparison.Ordinal),
            manifest + "FILE|" + Path.Combine(Path.GetDirectoryName(root)!, "outside.cs") + "|" + Hash(source) + "\n" })
        {
            bool rejected = false;
            try { Check(malformed, root); } catch (InvalidOperationException) { rejected = true; }
            HostCheck.True(rejected, "malformed manifest rejected");
        }
        string repository = HostToolProcess.FindRepositoryRoot();
        var inventory = ToolingBuildInputs.SourceInputs(repository);
        foreach (string relative in new[] { "tools/OS.Tooling/Program.cs", "tools/OS.Tooling/OS.Tooling.csproj", "tools/OS.QemuRunner/OS.QemuRunner.csproj", "src/OS.Kernel/OS.Kernel.csproj", "src/OS.Std.NoRuntime.Shared/OS.Std.NoRuntime.Shared.csproj", "global.json", "Directory.Build.props", "Directory.Build.targets" })
            HostCheck.True(inventory.Contains(Path.GetFullPath(Path.Combine(repository, relative)), StringComparer.OrdinalIgnoreCase), "compiled inventory includes " + relative);
        var xml = System.Xml.Linq.XDocument.Load(Path.Combine(repository, "tools/OS.Tooling/OS.Tooling.csproj"));
        string patterns = xml.Descendants("_ToolingInputPatterns").Single().Value;
        HostCheck.True(patterns.Contains("tools/**/*.cs", StringComparison.Ordinal) && patterns.Contains("src/OS.Kernel/**/*.cs", StringComparison.Ordinal), "build owns the source patterns");
    }

    [HostTest("tooling captured Windows and POSIX paths relocate across hosts without accepting relative or escaping identities", HostLane.Fast)]
    private static void PortablePaths()
    {
        string root = NewRoot(), source = Path.Combine(root, "tools", "Example", "Program.cs"), config = Path.Combine(root, "global.json");
        File.WriteAllText(source, "class Portable {}\r\n"); File.WriteAllText(config, "{}\n");
        string Captured(string capturedRoot, string capturedSource, string capturedConfig) =>
            "CLROS_BUILD_INPUTS_V1\nROOT|" + capturedRoot + "\nGLOB|tools/**/*.cs\nGLOB|global.json\nFILE|" + capturedSource + "|" + Hash(source) + "\nFILE|" + capturedConfig + "|" + Hash(config) + "\n";
        foreach (var paths in new[] {
            (Root: @"D:\frozen\CLROS", Source: @"d:/frozen/CLROS\tools/Example\Program.cs", Config: @"D:\frozen/CLROS/global.json"),
            (Root: "/clros/frozen", Source: "/clros/frozen/tools/Example/Program.cs", Config: "/clros/frozen/global.json") })
        {
            string manifest = Captured(paths.Root, paths.Source, paths.Config);
            HostCheck.Equal(0, Check(manifest, root).Count);
            HostCheck.True(Check(manifest.Replace(Hash(source), new string('0', 64), StringComparison.Ordinal), root)
                .Single().StartsWith("stale_build_input ", StringComparison.Ordinal), "relocation retains raw source hashes");
        }
        foreach (var paths in new[] {
            ("relative", "relative/tools/Example/Program.cs", "relative/global.json"),
            (@"D:frozen", @"D:frozen\tools\Example\Program.cs", @"D:frozen\global.json"),
            (@"D:\frozen\CLROS", @"D:\frozen\CLROS-other\tools\Example\Program.cs", @"D:\frozen\CLROS\global.json"),
            (@"D:\frozen\CLROS", @"D:\frozen\CLROS\..\CLROS\tools\Example\Program.cs", @"D:\frozen\CLROS\global.json"),
            ("/clros/frozen", "/clros/FROZEN/tools/Example/Program.cs", "/clros/frozen/global.json"),
            ("/clros/frozen", "/clros/frozen//tools/Example/Program.cs", "/clros/frozen/global.json"),
            ("/clros/frozen", "/clros/frozen", "/clros/frozen/global.json"),
            ("/clros/frozen/../frozen", "/clros/frozen/tools/Example/Program.cs", "/clros/frozen/global.json") })
        {
            bool rejected = false;
            try { Check(Captured(paths.Item1, paths.Item2, paths.Item3), root); } catch (InvalidOperationException) { rejected = true; }
            HostCheck.True(rejected, "captured path must be absolute, contained and traversal-free: " + paths.Item2);
        }
    }
}
