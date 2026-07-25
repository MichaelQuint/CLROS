using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static class NoStdLibShimBoundaryTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;

        Run("nostdlib shim boundary includes explicit EFI project", TestEfiProjectIncludedExplicitly, ref failures);
        Run("nostdlib shim boundary is explicit in EFI project", TestEfiProjectExplicitShimBoundary, ref failures);
        Run("nostdlib shim boundary keeps System stubs out of host projects", TestHostProjectsDoNotCompileMinimalRuntime, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("NoStdLib shim boundary tests failed: " + failures);
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

    private static bool TestEfiProjectIncludedExplicitly()
    {
        string root = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(root, "OS.slnx"));

        return solution.Contains("src/OS/OS.csproj", StringComparison.OrdinalIgnoreCase)
            || solution.Contains("src\\OS\\OS.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestEfiProjectExplicitShimBoundary()
    {
        string root = FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));

        return HasProperty(project, "NoStdLib", "true")
            && HasProperty(project, "DisableImplicitFrameworkReferences", "true")
            && HasProperty(project, "EnableDefaultCompileItems", "false")
            && HasCompileInclude(project, "Runtime\\MinimalRuntime.cs")
            && HasCompileInclude(project, "Boot\\EfiEntry.cs")
            && !project.Descendants().Any(element => element.Name.LocalName == "ProjectReference");
    }

    private static bool TestHostProjectsDoNotCompileMinimalRuntime()
    {
        string root = FindRepositoryRoot();
        string[] projectPaths =
        [
            Path.Combine(root, "src", "OS.Kernel", "OS.Kernel.csproj"),
            Path.Combine(root, "std", "no-runtime", "shared", "OS.Std.NoRuntime.Shared.csproj")
        ];

        foreach (string projectPath in projectPaths)
        {
            XDocument project = XDocument.Load(projectPath);
            bool includesShim = project.Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => (string?)element.Attribute("Include"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => value!.Contains("MinimalRuntime.cs", StringComparison.OrdinalIgnoreCase));

            if (includesShim)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasProperty(XDocument project, string name, string expectedValue)
    {
        return project.Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .Any(value => string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCompileInclude(XDocument project, string include)
    {
        return project.Descendants()
            .Where(element => element.Name.LocalName == "Compile")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => string.Equals(value, include, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing OS.slnx.");
    }
}
