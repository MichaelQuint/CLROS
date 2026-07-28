using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static class HostToolProjectBoundaryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("tools.project boundary excludes artifact sources", TestToolsExcludeArtifactSources, ref failures);
        Run("tools.project boundary keeps expected host tools", TestToolProjectInventory, ref failures);
        Run(
            "tools.x64 serializer excludes native and object sources",
            TestX64SerializerSourceBoundary,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Host tool project boundary tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestToolsExcludeArtifactSources()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string propsPath = Path.Combine(repositoryRoot, "tools", "Directory.Build.props");
        if (!File.Exists(propsPath))
        {
            return false;
        }

        XDocument document = XDocument.Load(propsPath);
        string? defaultItemExcludes = document
            .Descendants("DefaultItemExcludes")
            .FirstOrDefault()
            ?.Value;

        return defaultItemExcludes is not null
            && defaultItemExcludes.Contains("$(DefaultItemExcludes)", StringComparison.Ordinal)
            && defaultItemExcludes.Contains("$(MSBuildProjectDirectory)\\artifacts\\**\\*", StringComparison.Ordinal);
    }

    private static bool TestToolProjectInventory()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string toolsRoot = Path.Combine(repositoryRoot, "tools");
        string[] projects = Directory.GetFiles(toolsRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return projects.Length == 5
            && projects[0] == "OS.ImageBuilder"
            && projects[1] == "OS.LibvirtRunner"
            && projects[2] == "OS.ManifestTool"
            && projects[3] == "OS.QemuRunner"
            && projects[4] == "OS.X64TextSerializerGenerator";
    }

    private static bool TestX64SerializerSourceBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string generatorRoot = Path.Combine(
            repositoryRoot,
            "tools",
            "OS.X64TextSerializerGenerator");
        if (!Directory.Exists(generatorRoot))
        {
            return false;
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

        return Directory
            .EnumerateFiles(generatorRoot, "*", SearchOption.AllDirectories)
            .Where(path => !HasGeneratedDirectorySegment(generatorRoot, path))
            .All(path => !forbiddenExtensions.Contains(Path.GetExtension(path)));
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
