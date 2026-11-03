using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.Json;
using System.Xml.Linq;

internal static partial class BootPageAllocatorStressTests
{
    private const string StressSourceOwner = nameof(BootPageAllocatorStressTests);
    private static bool StressHostBoundary(string root, CSharpSourceCatalog host, CSharpSourceCatalog kernel)
    {
        string hostDirectory = Path.GetFullPath(Path.Combine(root, "tests", "OS.HostTests")) + Path.DirectorySeparatorChar;
        var family = host.TypeSources(StressSourceOwner);
        if (family.Count == 0 || family.Any(document => !Path.GetFullPath(document.Path).StartsWith(hostDirectory, StringComparison.OrdinalIgnoreCase)) || !StressExcluded(root, kernel, family.Select(document => document.Path)))
            return false;
        var mutator = host.Compilation.GetTypeByMetadataName("OS.Kernel.Memory.BootPageAllocatorMutator");
        if (mutator is null || !mutator.ContainingAssembly.Identity.Equals(kernel.Compilation.Assembly.Identity) || kernel.Compilation.ReferencedAssemblyNames.Any(identity => identity.Equals(host.Compilation.Assembly.Identity)))
            return false;
        string project = Path.Combine(root, "tests", "OS.HostTests", "OS.HostTests.csproj");
        string kernelProject = Path.GetFullPath(Path.Combine(root, "src", "OS.Kernel", "OS.Kernel.csproj"));
        return XDocument.Load(project).Descendants("ProjectReference").Any(item => item.Attribute("Include")is { } include && Path.GetFullPath(include.Value, Path.GetDirectoryName(project)!).Equals(kernelProject, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StressExcluded(string root, CSharpSourceCatalog source, IEnumerable<string> hostPaths)
    {
        string hostDirectory = Path.GetFullPath(Path.Combine(root, "tests", "OS.HostTests")) + Path.DirectorySeparatorChar;
        var paths = hostPaths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return source.Compilation.GetTypeByMetadataName(StressSourceOwner)is null && source.Documents.All(document => !paths.Contains(Path.GetFullPath(document.Path)) && !Path.GetFullPath(document.Path).StartsWith(hostDirectory, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StressEfiBoundary(string root, CSharpEfiProjectEvaluation evaluation, IEnumerable<string> hostPaths, string workload)
    {
        using var document = JsonDocument.Parse(evaluation.EvaluationJson);
        var properties = document.RootElement.GetProperty("Properties");
        var items = document.RootElement.GetProperty("Items");
        var sources = evaluation.Sources;
        if (properties.GetProperty("Configuration").GetString() != "Release" || properties.GetProperty("OSWorkloadProfile").GetString() != workload || properties.GetProperty("NoStdLib").GetString() != "true" || properties.GetProperty("DisableImplicitFrameworkReferences").GetString() != "true" || properties.GetProperty("EnableDefaultCompileItems").GetString() != "false" || items.GetProperty("Reference").GetArrayLength() != 0 || items.GetProperty("ProjectReference").GetArrayLength() != 0 || sources.Compilation.References.Any() || !StressExcluded(root, sources, hostPaths))
            return false;
        var compile = items.GetProperty("Compile").EnumerateArray().ToArray();
        if (compile.Length == 0 || compile.Any(item => string.IsNullOrWhiteSpace(item.GetProperty("Identity").GetString()) || string.IsNullOrWhiteSpace(item.GetProperty("FullPath").GetString()) || !Path.IsPathFullyQualified(item.GetProperty("FullPath").GetString()!)) || !compile.Select(item => Path.GetFullPath(item.GetProperty("FullPath").GetString()!)).SequenceEqual(sources.Documents.Select(item => Path.GetFullPath(item.Path)), StringComparer.OrdinalIgnoreCase))
            return false;
        return sources.Type("OS.Boot.EfiEntry").TypeKind == TypeKind.Class && sources.Type("System.Object").SpecialType == SpecialType.System_Object;
    }

    private static bool StressMarkerShape(CSharpSourceCatalog catalog)
    {
        var method = catalog.Method(StressSourceOwner, nameof(RunAll), symbol => symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid && symbol.Parameters.Length == 0 && symbol.DeclaredAccessibility == Accessibility.Internal);
        _ = catalog.MethodInvocations(method);
        string source = HostSourceText.Documents(catalog, StressSourceOwner);
        string active = HostSourceText.Declarations(catalog, StressSourceOwner);
        string runAll = HostSourceText.Active(method);
        string startWrite = string.Concat("Console.WriteLine(\"TEST_START \" + ", "IssueTestName);");
        string failureWrite = string.Concat("Console.WriteLine(\"TEST_FAIL \" + ", "IssueTestName + \" failures=\" + failures);");
        string readyWrite = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string passWrite = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        int startIndex = HostSourceText.IndexOf(runAll, startWrite);
        int failureGate = HostSourceText.IndexOf(runAll, "if (failures != 0)");
        int failureIndex = HostSourceText.IndexOf(runAll, failureWrite);
        int throwIndex = HostSourceText.IndexOf(runAll, "throw new InvalidOperationException(", Math.Max(0, failureIndex));
        int readyIndex = HostSourceText.IndexOf(runAll, readyWrite);
        int passIndex = HostSourceText.IndexOf(runAll, passWrite);
        string bootMarker = string.Concat("OS_BOOT_", "OK");
        string smokeMarker = string.Concat("OS_SMOKE_", "PASS");
        return CountOccurrences(source, "\"" + ReadyMarker + "\"") == 1 && CountOccurrences(source, startWrite) == 1 && CountOccurrences(source, failureWrite) == 1 && CountOccurrences(source, readyWrite) == 1 && CountOccurrences(source, passWrite) == 1 && HostSourceText.Count(active, startWrite) == 1 && HostSourceText.Count(active, failureWrite) == 1 && HostSourceText.Count(active, readyWrite) == 1 && HostSourceText.Count(active, passWrite) == 1 && startIndex >= 0 && failureGate > startIndex && failureIndex > failureGate && throwIndex > failureIndex && readyIndex > throwIndex && passIndex > readyIndex && !source.Contains(bootMarker, StringComparison.Ordinal) && !source.Contains(smokeMarker, StringComparison.Ordinal);
    }
}
