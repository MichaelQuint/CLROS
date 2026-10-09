using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static class HostToolProjectBoundaryTests
{

    [HostTest("tools.project boundary excludes artifact sources", HostLane.Fast, 0)]
    private static bool TestToolsExcludeArtifactSources()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string propsPath = Path.Combine(repositoryRoot, "tools", "Directory.Build.props");
        if (!File.Exists(propsPath))
        {
            return HostCheck.Result(false);
        }

        XDocument document = XDocument.Load(propsPath);
        string? defaultItemExcludes = document
            .Descendants("DefaultItemExcludes")
            .FirstOrDefault()
            ?.Value;

        return HostCheck.Result(defaultItemExcludes is not null
            && defaultItemExcludes.Contains("$(DefaultItemExcludes)", StringComparison.Ordinal)
            && defaultItemExcludes.Contains("$(MSBuildProjectDirectory)\\artifacts\\**\\*", StringComparison.Ordinal));
    }

    [HostTest("tools.project boundary keeps expected host tools", HostLane.Fast, 1)]
    private static bool TestToolProjectInventory()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolsRoot = Path.Combine(repositoryRoot, "tools");
        string[] projects = Directory.GetFiles(toolsRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasGeneratedDirectorySegment(toolsRoot, path) &&
                !Path.GetRelativePath(toolsRoot, path).Split(Path.DirectorySeparatorChar).Contains("artifacts"))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] declared = XDocument.Load(Path.Combine(repositoryRoot, "CLROS.slnx"))
            .Descendants("Project").Select(node => (string?)node.Attribute("Path"))
            .Where(path => path is not null && path.Replace('\\', '/').StartsWith("tools/", StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path!)).Order(StringComparer.Ordinal).ToArray();
        HostCheck.True(declared.Length != 0, "solution declares host tools");
        HostCheck.Equal(string.Join(";", declared), string.Join(";", projects));
        return HostCheck.Result(true);
    }

    [HostTest("tools.x64 serializer excludes native and object sources", HostLane.Fast, 2)]
    private static bool TestX64SerializerSourceBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string generatorRoot = Path.Combine(
            repositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator");
        if (!Directory.Exists(generatorRoot))
        {
            return HostCheck.Result(false);
        }

        HashSet<string> forbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".obj",
            ".c",
            ".cpp",
            ".h",
            ".asm",
            ".s"
        };

        return HostCheck.Result(Directory
            .EnumerateFiles(generatorRoot, "*", SearchOption.AllDirectories)
            .Where(path => !HasGeneratedDirectorySegment(generatorRoot, path))
            .All(path => !forbiddenExtensions.Contains(Path.GetExtension(path))));
    }

    private static bool HasGeneratedDirectorySegment(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        string[] segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(
            segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }
}
