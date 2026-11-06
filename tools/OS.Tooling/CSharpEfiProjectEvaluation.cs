using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OS.Tooling;

/// <summary>Host-only semantic inspection of the explicit no-stdlib EFI source
/// project under a caller-selected profile. Query-only isolated MSBuild evaluates
/// links and symbols; no build, analyzer, generator or EFI artifact is executed.
/// This source snapshot grants no native-code, stack or guest qualification.</summary>
public sealed record CSharpEfiProjectEvaluation(string EvaluationJson, CSharpSourceCatalog Sources)
{
    public static async Task<CSharpEfiProjectEvaluation> LoadAsync(string root, EfiProfile profile,
        string configuration = "Release", string workload = "SingleMinimalApi", CancellationToken cancellation = default,
        int queryTimeoutSeconds = 120)
    {
        if (queryTimeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(queryTimeoutSeconds));
        root = Path.GetFullPath(root);
        if (configuration is not ("Release" or "Debug") || workload is not ("SingleMinimalApi" or "MultiMinimalApi"))
            throw new ArgumentException("source_catalog_efi_configuration");
        string dotnet = HostTools.FindExecutable(null, "", "dotnet")
            ?? throw new InvalidOperationException("source_catalog_dotnet_missing");
        var version = await ProcessRunner.RunAsync(new ProcessRequest(dotnet, ["--version"], root)
        {
            Timeout = TimeSpan.FromSeconds(30),
            OutputCharacterLimit = 65536
        }, cancellation);
        string[] lines = version.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!version.Succeeded || EfiPreflight.SdkVersionFailure("ok", lines) != "ok")
            throw new InvalidOperationException("source_catalog_sdk_identity");
        string sdk = Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", lines.Single());
        string absent = Path.Combine(root, "artifacts", "source-catalog-evaluation", Guid.NewGuid().ToString("N"));
        if (Path.Exists(absent))
            throw new InvalidOperationException("source_catalog_evaluation_path_exists");
        var properties = EfiProjectAdmission.Isolation(root, sdk, absent);
        properties["Configuration"] = configuration;
        properties["OSWorkloadProfile"] = workload;
        properties["ClrosBootPolicy"] = profile.BootPolicy;
        foreach (var flag in profile.Flags)
            properties[flag.Key] = flag.Value ? "true" : "false";
        var selectedProperties = EfiProjectAdmission.IsolationKeys.Concat(profile.Flags.Keys)
            .Concat(["Configuration", "OSWorkloadProfile", "ClrosBootPolicy"])
            .ToDictionary(name => name, name => properties[name], StringComparer.Ordinal);
        string project = Path.Combine(root, "src", "OS", "OS.csproj");
        VerifyStaticEvaluationInputs(root, project);
        cancellation.ThrowIfCancellationRequested();
        var evaluation = EfiProjectAdmission.Query(root, dotnet, project, properties,
            ["DefineConstants", "LangVersion", "NoStdLib", "DisableImplicitFrameworkReferences", "EnableDefaultCompileItems",
                "AllowUnsafeBlocks", "CheckForOverflowUnderflow", "Optimize", "OutputType", "AssemblyName", "Nullable", "PlatformTarget",
                .. selectedProperties.Keys],
            "Compile,Reference,ProjectReference", "source_catalog_efi", queryTimeoutSeconds, 16 * 1024 * 1024);
        cancellation.ThrowIfCancellationRequested();
        VerifyAuthority(evaluation, project, selectedProperties);
        var values = evaluation["Properties"]!.AsObject();
        var items = evaluation["Items"]!.AsObject();
        string Value(string name) => values[name]!.GetValue<string>();
        if (Value("NoStdLib") != "true" || Value("DisableImplicitFrameworkReferences") != "true"
            || Value("EnableDefaultCompileItems") != "false" || Value("LangVersion") != "14.0"
            || Value("OutputType") != "Exe" || Value("Nullable") != "disable" || items["Reference"]!.AsArray().Count != 0
            || items["ProjectReference"]!.AsArray().Count != 0)
            throw new InvalidOperationException("source_catalog_efi_compiler_boundary");
        var compile = items["Compile"]!.AsArray();
        string[] links = compile.Select(item => item!["Identity"]!.GetValue<string>() + "|"
            + (item["Link"]?.GetValue<string>() ?? "")).ToArray();
        if (!links.SequenceEqual(EfiProjectAdmission.ExpectedCompileItems(profile), StringComparer.Ordinal))
            throw new InvalidOperationException("source_catalog_efi_unreviewed_inventory");
        var symbols = Value("DefineConstants").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!EfiProjectAdmission.ExpectedSymbols(configuration, workload, profile).SetEquals(symbols))
            throw new InvalidOperationException("source_catalog_efi_profile_symbols");
        var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols);
        var documents = compile.Select(item =>
        {
            string path = item!["FullPath"]!.GetValue<string>();
            string expectedPath = Path.GetFullPath(item["Identity"]!.GetValue<string>(), Path.Combine(root, "src", "OS"));
            if (!path.Equals(expectedPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("source_catalog_efi_source_path");
            _ = EfiEvidence.RelativePath(root, path, "source_catalog_source_outside_root");
            AuthorityFiles.RequirePlainPath(root, path, "source_catalog_source_link");
            return CSharpSourceDocument.Read(path, options);
        }).ToArray();
        Platform platform = string.IsNullOrEmpty(Value("PlatformTarget")) ? Platform.AnyCpu
            : Enum.Parse<Platform>(Value("PlatformTarget"), ignoreCase: true);
        var compilation = new CSharpCompilationOptions(OutputKind.ConsoleApplication, platform: platform,
            allowUnsafe: Value("AllowUnsafeBlocks").Equals("true", StringComparison.OrdinalIgnoreCase),
            checkOverflow: Value("CheckForOverflowUnderflow").Equals("true", StringComparison.OrdinalIgnoreCase),
            optimizationLevel: Value("Optimize").Equals("true", StringComparison.OrdinalIgnoreCase)
                ? OptimizationLevel.Release : OptimizationLevel.Debug);
        return new(evaluation.ToJsonString(), new CSharpSourceCatalog(documents, [], Value("AssemblyName"), compilation));
    }

    internal static void VerifyStaticEvaluationInputs(string root, string project)
    {
        // Reject altered project/import hooks before asking MSBuild to evaluate
        // them. This verifies executable build shape, not source or native pins.
        foreach (string path in new[]
        {
            project, Path.Combine(root, "Directory.Build.props"), Path.Combine(root, "Directory.Build.targets"),
            Path.Combine(root, "tools", "Directory.Build.props"),
            Path.Combine(root, "tools", "OS.X64TextSerializerGenerator", "OS.X64TextSerializerGenerator.csproj"),
            Path.Combine(root, "tools", "OS.X64TextSerializerGenerator", "OS.X64TextSerializerGenerator.targets")
        })
            AuthorityFiles.RequirePlainPath(root, path, "source_catalog_efi_build_input_link");
        var xml = EfiProjectAdmission.ReadXml(project);
        if (EfiProjectAdmission.CanonicalXmlSha256(xml) != EfiContracts.ProjectCanonicalXmlSha256)
            throw new InvalidOperationException("source_catalog_efi_project_shape");
        EfiProjectAdmission.VerifySerializerImport(root, project, xml);
        EfiProjectAdmission.VerifyCustomization(root, project);
    }

    internal static void VerifyAuthority(JsonObject evaluation, string project, IReadOnlyDictionary<string, string> selectedProperties)
    {
        foreach (var property in selectedProperties)
            if (evaluation["Properties"]?[property.Key]?.GetValue<string>() != property.Value)
                throw new InvalidOperationException("source_catalog_efi_selected_property " + property.Key);
        foreach (var item in evaluation["Items"]!["Compile"]!.AsArray())
            if (!project.Equals(item!["DefiningProjectFullPath"]?.GetValue<string>(),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("source_catalog_efi_compile_owner");
    }
}
