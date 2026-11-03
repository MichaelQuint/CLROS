using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class CSharpEfiProjectEvaluationTests
{
    [HostTest("EFI source catalog rejects changed executable build shape before evaluation")]
    private static void StaticEvaluationInputs()
    {
        string original = HostToolProcess.FindRepositoryRoot();
        string root = Path.Combine(original, "artifacts", "source-catalog-controls", Guid.NewGuid().ToString("N"));
        foreach (string relative in new[]
        {
            "src/OS/OS.csproj", "Directory.Build.props", "Directory.Build.targets", "tools/Directory.Build.props",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets"
        })
        {
            string destination = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(original, relative), destination);
        }
        string project = Path.Combine(root, "src", "OS", "OS.csproj");
        CSharpEfiProjectEvaluation.VerifyStaticEvaluationInputs(root, project);
        var document = EfiProjectAdmission.ReadXml(project);
        document.DocumentElement!.SetAttribute("InitialTargets", "UnreviewedInitialTarget");
        document.Save(project);
        try
        {
            CSharpEfiProjectEvaluation.VerifyStaticEvaluationInputs(root, project);
        }
        catch (InvalidOperationException error) when (error.Message == "source_catalog_efi_project_shape")
        {
            return;
        }
        throw new InvalidOperationException("expected rejection before MSBuild evaluation");
    }

    [HostTest("EFI source catalog rejects foreign compile ownership and changed selected properties")]
    private static void AuthorityControls()
    {
        const string project = "selected.csproj";
        var expected = new Dictionary<string, string> { ["EnableProbe"] = "false", ["ImportHook"] = "false" };
        var valid = System.Text.Json.Nodes.JsonNode.Parse("""
            { "Properties": { "EnableProbe":"false", "ImportHook":"false" },
              "Items": { "Compile": [ { "DefiningProjectFullPath":"selected.csproj" } ] } }
            """)!.AsObject();
        CSharpEfiProjectEvaluation.VerifyAuthority(valid, project, expected);
        foreach (string mutation in new[] { "owner", "probe", "import" })
        {
            var changed = valid.DeepClone().AsObject();
            if (mutation == "owner")
                changed["Items"]!["Compile"]![0]!["DefiningProjectFullPath"] = "foreign.targets";
            else
                changed["Properties"]![mutation == "probe" ? "EnableProbe" : "ImportHook"] = "true";
            try
            {
                CSharpEfiProjectEvaluation.VerifyAuthority(changed, project, expected);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("source_catalog_efi_", StringComparison.Ordinal))
            {
                continue;
            }
            throw new InvalidOperationException("expected EFI evaluation authority rejection " + mutation);
        }
    }

    [HostTest("EFI source catalog binds explicit no-stdlib sources under selected default and stage-one profiles")]
    private static void SelectedProfiles()
    {
        var ordinary = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
        var stageOne = HostSourceCatalog.Efi(EfiProfile.Create(null,
            new Dictionary<string, bool> { ["EnableM01LoadedImageStackProbe"] = true }), workload: "MultiMinimalApi");
        foreach (var catalog in new[] { ordinary, stageOne })
        {
            HostCheck.Equal(0, catalog.Compilation.References.Count());
            HostCheck.Equal("OS", catalog.Type("System.Object").ContainingAssembly.Name);
            HostCheck.True(catalog.Compilation.GetDiagnostics().All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error));
            HostCheck.True(catalog.TypeSources("OS.Boot.EfiEntry+M01StageOneAdapter")
                .All(document => File.Exists(document.Path) && !document.IsGenerated));
            HostCheck.Equal("Run", catalog.Method("OS.Boot.EfiEntry+M01StageOneAdapter", "Run").Identifier.ValueText);
        }
        HostCheck.True(((CSharpParseOptions)ordinary.Documents[0].Syntax.Options).PreprocessorSymbolNames.Contains("CLROS_NORMAL_VERIFIED_EXIT"));
        HostCheck.True(!((CSharpParseOptions)stageOne.Documents[0].Syntax.Options).PreprocessorSymbolNames.Contains("CLROS_NORMAL_VERIFIED_EXIT"));
        HostCheck.True(((CSharpParseOptions)stageOne.Documents[0].Syntax.Options).PreprocessorSymbolNames.Contains("OS_PROFILE_MULTI_MINIMAL_API"));
        HostCheck.Equal(0, ordinary.Type("OS.Boot.EfiEntry+M03FirstExitAdapter").GetMembers("CopyLinuxAlias").Length);
    }

    [HostTest("EFI source catalog finds moved Linux alias methods only in their selected profile")]
    private static void DiskProfile()
    {
        var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM10018FullProbe"] = true });
        var catalog = HostSourceCatalog.Efi(profile);
        var copy = catalog.Method("OS.Boot.EfiEntry+M03FirstExitAdapter", "CopyLinuxAlias");
        HostCheck.Equal(5, copy.ParameterList.Parameters.Count);
        HostCheck.True(catalog.Compilation.GetSemanticModel(copy.SyntaxTree).GetDiagnostics(copy.Span)
            .All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error));
        HostCheck.True(catalog.Documents.Count > 0 && catalog.Documents.All(document => document.Sha256.Length == 64));
    }
}
