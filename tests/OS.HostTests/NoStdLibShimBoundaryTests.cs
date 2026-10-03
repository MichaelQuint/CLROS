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
        string root = HostToolProcess.FindRepositoryRoot();
        string solution = File.ReadAllText(
            Path.Combine(root, HostToolProcess.RepositorySolutionFileName));

        return solution.Contains("src/OS/OS.csproj", StringComparison.OrdinalIgnoreCase)
            || solution.Contains("src\\OS\\OS.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestEfiProjectExplicitShimBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));

        return HasProperty(project, "NoStdLib", "true")
            && HasProperty(project, "DisableImplicitFrameworkReferences", "true")
            && HasProperty(project, "EnableDefaultCompileItems", "false")
            && HasCompileInclude(project, "Runtime\\MinimalRuntime.cs")
            && HasCompileInclude(project, "Runtime\\RhpNewFastAllocation.cs")
            && HasCompileInclude(project, "..\\OS.Kernel\\Runtime\\GuestTestRhpNewFast.cs")
            && HasCompileInclude(project, "Boot\\EfiEntry.cs")
            && FixedAllocationDefaultIsSelfContained(root)
            && ArrayAllocationDefaultIsSelfContained(root)
            && StringAllocationDefaultIsSelfContained(root)
            && !project.Descendants().Any(element => element.Name.LocalName == "ProjectReference");
    }

    private static bool FixedAllocationDefaultIsSelfContained(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/MinimalRuntime.cs"));
        int export = source.IndexOf("[RuntimeExport(\"RhpNewFast\")]", StringComparison.Ordinal);
        if (export < 0 || source.IndexOf("[RuntimeExport(\"RhpNewFast\")]", export + 1, StringComparison.Ordinal) >= 0) return false;
        int enabled = source.IndexOf("#if M04_RHP_NEW_FAST_PROBE", export, StringComparison.Ordinal);
        int disabled = source.IndexOf("#else", enabled, StringComparison.Ordinal);
        int end = source.IndexOf("#endif", disabled, StringComparison.Ordinal);
        if (enabled < export || disabled < enabled || end < disabled) return false;
        string defaults = source[disabled..end];
        return source[enabled..disabled].Contains("RhpNewFastAllocation.Allocate((ulong)methodTable)", StringComparison.Ordinal)
            && defaults.Contains("while (true) RuntimeFailFast.Halt();", StringComparison.Ordinal)
            && !defaults.Contains("OS.Runtime", StringComparison.Ordinal)
            && !defaults.Contains("return ", StringComparison.Ordinal)
            && source.Contains("extern ref TTo As<TFrom, TTo>(ref TFrom value)", StringComparison.Ordinal)
            && source.Contains("extern MethodTable* Of<T>()", StringComparison.Ordinal)
            && source.Contains("class Array<T> : Array { private Array() { } }", StringComparison.Ordinal);
    }

    private static bool ArrayAllocationDefaultIsSelfContained(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/MinimalRuntime.cs"));
        foreach (string name in new[] { "RhpNewArrayFast", "RhpNewPtrArrayFast" })
        {
            string attribute = "[RuntimeExport(\"" + name + "\")]";
            int start = source.IndexOf(attribute, StringComparison.Ordinal);
            if (start < 0 || source.IndexOf(attribute, start + 1, StringComparison.Ordinal) >= 0) return false;
            int enabled = source.IndexOf("#if M04_SZ_ARRAY_ALLOCATION_PROBE", start, StringComparison.Ordinal);
            int disabled = source.IndexOf("#else", enabled, StringComparison.Ordinal), end = source.IndexOf("#endif", disabled, StringComparison.Ordinal);
            if (enabled < start || disabled < enabled || end < disabled ||
                !source[start..enabled].Contains("int length", StringComparison.Ordinal) ||
                !source[start..enabled].Contains("MethodImplOptions.NoInlining", StringComparison.Ordinal) ||
                !source[enabled..disabled].Contains("RhpNewFastAllocation.AllocateArray((ulong)methodTable, length,", StringComparison.Ordinal) ||
                !source[disabled..end].Contains("while (true) RuntimeFailFast.Halt();", StringComparison.Ordinal) ||
                source[disabled..end].Contains("return ", StringComparison.Ordinal)) return false;
        }
        return source.Contains("private int _numComponents;", StringComparison.Ordinal) &&
            source.Contains("public int Length => _numComponents;", StringComparison.Ordinal);
    }

    private static bool StringAllocationDefaultIsSelfContained(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/MinimalRuntime.cs"));
        const string attribute = "[RuntimeExport(\"RhNewString\")]";
        int start = source.IndexOf(attribute, StringComparison.Ordinal);
        if (start < 0 || source.IndexOf(attribute, start + 1, StringComparison.Ordinal) >= 0) return false;
        int enabled = source.IndexOf("#if M04_STRING_ALLOCATION_PROBE", start, StringComparison.Ordinal);
        if (enabled < start) return false;
        int disabled = source.IndexOf("#else", enabled, StringComparison.Ordinal);
        if (disabled < enabled) return false;
        int end = source.IndexOf("#endif", disabled, StringComparison.Ordinal);
        if (end < disabled) return false;
        return source[start..enabled].Contains("nint length", StringComparison.Ordinal) &&
            source[start..enabled].Contains("MethodImplOptions.NoInlining", StringComparison.Ordinal) &&
            source[enabled..disabled].Contains("AllocateString((ulong)methodTable, (long)length)", StringComparison.Ordinal) &&
            source[disabled..end].Contains("while (true) RuntimeFailFast.Halt();", StringComparison.Ordinal) &&
            !source[disabled..end].Contains("return ", StringComparison.Ordinal) &&
            source.Contains("private readonly int _stringLength;", StringComparison.Ordinal) &&
            source.Contains("private char _firstChar;", StringComparison.Ordinal) &&
            source.Contains("#if M04_STRING_ALLOCATION_PROBE\nnamespace System.Runtime\n{\n    internal static unsafe class RuntimeImports", StringComparison.Ordinal) &&
            source.Contains("#if M04_STRING_ALLOCATION_PROBE\n            return Runtime.RuntimeImports.RhNewString", StringComparison.Ordinal);
    }

    private static bool TestHostProjectsDoNotCompileMinimalRuntime()
    {
        string root = HostToolProcess.FindRepositoryRoot();
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

}
