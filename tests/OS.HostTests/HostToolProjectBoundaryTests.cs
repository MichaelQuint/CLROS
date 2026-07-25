using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static class HostToolProjectBoundaryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("tools.project boundary excludes artifact sources", TestToolsExcludeArtifactSources, ref failures);
        Run("tools.project boundary keeps four host tools", TestToolProjectInventory, ref failures);

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

        return projects.Length == 4
            && projects[0] == "OS.ImageBuilder"
            && projects[1] == "OS.LibvirtRunner"
            && projects[2] == "OS.ManifestTool"
            && projects[3] == "OS.QemuRunner";
    }
}
