using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OS.Tooling;

/// <summary>Reads the selected project's evaluated Compile items, symbols and
/// resolved references and actual compiler arguments, including SDK global usings
/// and receipt-bound embedded generator outputs. Dependencies must already be
/// built. The SDK compiler task runs with SkipCompilerExecution to return its
/// arguments; application compilation, generators and publish do not run. The
/// bounded child is joined on failure or cancellation. Discovery never replaces independent
/// admission rules or an original native-build receipt. Only trusted repository
/// projects are supported: project imports/targets are executable build inputs.
/// Preparing global usings/reference caches may update intermediate files.</summary>
public sealed record CSharpProjectEvaluation(string Project, IReadOnlyDictionary<string, string> Properties,
    string EvaluationJson, CSharpSourceCatalog Sources, CSharpCompilerArtifacts CompilerArtifacts)
{
    public static async Task<CSharpProjectEvaluation> LoadAsync(string root, string project,
        IReadOnlyDictionary<string, string>? properties = null, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        project = Path.GetFullPath(project, root);
        _ = EfiEvidence.RelativePath(root, project, "source_catalog_project_outside_root");
        AuthorityFiles.RequirePlainPath(root, project, "source_catalog_project_link");
        string absent = Path.Combine(root, "artifacts", "source-catalog-evaluation", Guid.NewGuid().ToString("N"));
        if (Path.Exists(absent))
            throw new InvalidOperationException("source_catalog_evaluation_path_exists");
        var arguments = new List<string>
        {
            "msbuild", project, "-nologo", "-m:1", "-nodeReuse:false", "-noAutoResponse", "-target:HostCompilationReceiptEvaluate",
            "-property:BuildProjectReferences=false", "-property:SkipCompilerExecution=true", "-property:ProvideCommandLineArgs=true",
            "-property:NonExistentFile=" + absent,
            "-getItem:Compile,ReferencePathWithRefAssemblies,ProjectReference,CscCommandLineArgs",
            "-getProperty:TargetFramework,Configuration,NETCoreSdkVersion,MSBuildVersion,IntermediateOutputPath,AssemblyName,TargetPath,HostCompilationReceiptPath,HostCompilationReceiptCurrentInputs"
        };
        foreach (var pair in properties ?? new Dictionary<string, string>())
        {
            if (!Regex.IsMatch(pair.Key, "^[A-Za-z_][A-Za-z0-9_]*$")
                || new[] { "BuildProjectReferences", "SkipCompilerExecution", "ProvideCommandLineArgs", "NonExistentFile", "DesignTimeBuild" }
                    .Contains(pair.Key, StringComparer.OrdinalIgnoreCase)
                || pair.Value.IndexOfAny(['\r', '\n', '\0', ';']) >= 0)
                throw new ArgumentException("source_catalog_invalid_property " + pair.Key, nameof(properties));
            arguments.Add("-property:" + pair.Key + "=" + pair.Value);
        }
        var result = await ProcessRunner.RunAsync(new ProcessRequest("dotnet", arguments, root)
        {
            Timeout = TimeSpan.FromMinutes(2),
            OutputCharacterLimit = 16 * 1024 * 1024
        }, cancellation);
        if (!result.Succeeded)
            throw new InvalidOperationException("source_catalog_evaluation_failed " + result.StandardOutput + result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        var values = json.RootElement.GetProperty("Properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var items = json.RootElement.GetProperty("Items");
        string[] compilerArguments = items.GetProperty("CscCommandLineArgs").EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!).ToArray();
        var receipt = CSharpCompilationReceipt.Read(root, values["HostCompilationReceiptPath"],
            values["HostCompilationReceiptCurrentInputs"], compilerArguments);
        var commandLine = CSharpCommandLineParser.Default.Parse(compilerArguments, Path.GetDirectoryName(project)!, sdkDirectory: null);
        if (commandLine.Errors.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_catalog_compiler_arguments");
        var options = commandLine.ParseOptions;
        string intermediate = Path.GetFullPath(values["IntermediateOutputPath"], Path.GetDirectoryName(project)!);
        var sources = items.GetProperty("Compile").EnumerateArray().Select(item =>
        {
            string path = item.GetProperty("FullPath").GetString()!;
            _ = EfiEvidence.RelativePath(root, path, "source_catalog_source_outside_root");
            AuthorityFiles.RequirePlainPath(root, path, "source_catalog_source_link");
            var document = CSharpSourceDocument.Read(path, options);
            return document with
            {
                IsGenerated = document.IsGenerated
                || path.StartsWith(Path.TrimEndingDirectorySeparator(intermediate) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                || item.TryGetProperty("AutoGen", out var autoGenerated)
                    && autoGenerated.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            };
        }).ToArray();
        VerifyCompilerSourceOrder(sources.Select(source => source.Path), commandLine.SourceFiles.Select(source => source.Path));
        receipt.VerifySources(sources);
        var generated = CSharpGeneratedSources.Read(root, values["TargetPath"], intermediate, sources, options, receipt);
        sources = [.. sources, .. generated.Sources];
        var references = commandLine.MetadataReferences.Select(reference =>
            receipt.ReadReference(Path.GetFullPath(reference.Reference, Path.GetDirectoryName(project)!),
                reference.Properties)).ToArray();
        var catalog = new CSharpSourceCatalog(sources, references, values["AssemblyName"], commandLine.CompilationOptions);
        receipt.VerifyFilesUnchanged();
        return new(project, values, result.StandardOutput, catalog, generated.Artifacts);
    }

    internal static void VerifyCompilerSourceOrder(IEnumerable<string> evaluated, IEnumerable<string> compiler)
    {
        // Roslyn preserves ../ spelling for linked inputs. Compare canonical
        // absolute identities while retaining every entry and its exact order.
        if (!evaluated.Select(Canonical).SequenceEqual(compiler.Select(Canonical),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
            throw new InvalidOperationException("source_catalog_compiler_source_order");

        static string Canonical(string path) => Path.IsPathFullyQualified(path) ? Path.GetFullPath(path)
            : throw new InvalidOperationException("source_catalog_compiler_source_order");
    }
}
