using System.Reflection;
using System.Text.Json;
using System.Xml;
using OS.Tooling;

internal static partial class HostProjectOutputsTests
{
    [HostTest("host source evaluation preserves the executing compiler's resources receipt and producer bytes")]
    private static async Task ReadOnlyEvaluation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var assembly = typeof(HostProjectOutputsTests).Assembly;
        var inventory = CSharpBuiltSourceInventory.Read(root, assembly.Location);
        var receipt = CSharpCompilationReceipt.ReadBuilt(root, inventory.Artifacts.ReceiptPath!);
        var resources = receipt.Inputs.SelectNodes("/Inputs/*/File")!.Cast<XmlElement>()
            .Where(file => file.GetAttribute("Kind") == "Resource")
            .Select(file => file.GetAttribute("Path")).ToArray();
        foreach (string name in new[] { "host-project-outputs.txt", "host-project-self.txt" })
            HostCheck.True(resources.Any(path => Path.GetFileName(path) == name));
        var original = inventory.InputHashes.ToDictionary(pair => pair.Key, pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        string project = receipt.Inputs.DocumentElement!.GetAttribute("Project");
        string configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new InvalidOperationException("evaluation_control_configuration_missing");
        var properties = new Dictionary<string, string> { ["Configuration"] = configuration };
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var evaluated = await CSharpProjectEvaluation.LoadAsync(root, project, properties);
            HostCheck.Equal(inventory.Artifacts.AssemblySha256, evaluated.CompilerArtifacts.AssemblySha256);
            HostCheck.Equal(inventory.Artifacts.ReceiptSha256, evaluated.CompilerArtifacts.ReceiptSha256);
            using var json = JsonDocument.Parse(evaluated.EvaluationJson);
            var current = new XmlDocument { XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(json.RootElement.GetProperty("Properties")
                .GetProperty("HostCompilationReceiptCurrentInputs").GetString()!),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            current.Load(reader);
            var currentResources = current.SelectNodes("/Inputs/*/File")!.Cast<XmlElement>()
                .Where(file => file.GetAttribute("Kind") == "Resource")
                .Select(file => file.GetAttribute("Path")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in resources)
                HostCheck.True(currentResources.Contains(resource));
            var arguments = json.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs")
                .EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!);
            HostCheck.True(arguments.SequenceEqual(receipt.Arguments, StringComparer.Ordinal),
                "resource registration and complete compiler arguments stay unchanged");
            HostCheck.True(HostTestFingerprint.FilesMatch(original), "all original compiler inputs and producer bytes stay unchanged");
        }
    }

    [HostTest("host project output evaluation requires both retained resources and never regenerates them")]
    private static async Task RequiredRetainedResources()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Scratch();
        string intermediate = Path.Combine(directory, "obj");
        Directory.CreateDirectory(intermediate);
        string project = Path.Combine(directory, "Query.proj");
        string Escape(string value) => System.Security.SecurityElement.Escape(value)!;
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup><IntermediateOutputPath>{Escape(intermediate + Path.DirectorySeparatorChar)}</IntermediateOutputPath></PropertyGroup>
              <Import Project="{Escape(Path.Combine(root, "tools/OS.CompilerReceipts/HostProjectOutputs.targets"))}" />
              <Target Name="ResolveProjectReferences" />
              <Target Name="GetTargetPath" />
            </Project>
            """);
        async Task<ProcessResult> Query() => await ProcessRunner.RunAsync(new ProcessRequest("dotnet",
            ["msbuild", project, "-nologo", "-m:1", "-nodeReuse:false", "-noAutoResponse",
                "-target:CaptureHostProjectOutputs", "-property:SkipCompilerExecution=true"], root)
        {
            Timeout = TimeSpan.FromSeconds(30),
            OutputCharacterLimit = 256 * 1024
        });
        var missingBoth = await Query();
        HostCheck.True(!missingBoth.Succeeded && missingBoth.StandardOutput.Contains(
            "host_project_outputs_evaluation_requires_built_resources", StringComparison.Ordinal));
        string outputs = Path.Combine(intermediate, "host-project-outputs.txt");
        string self = Path.Combine(intermediate, "host-project-self.txt");
        File.WriteAllText(outputs, "exact retained output sentinel\r\n");
        var missingSelf = await Query();
        HostCheck.True(!missingSelf.Succeeded && !File.Exists(self) && missingSelf.StandardOutput.Contains(
            "host_project_outputs_evaluation_requires_built_resources", StringComparison.Ordinal));
        File.WriteAllText(self, "exact retained self sentinel\r\n");
        string outputHash = HostRunCheckpoint.HashFile(outputs), selfHash = HostRunCheckpoint.HashFile(self);
        var retained = await Query();
        HostCheck.True(retained.Succeeded, retained.StandardOutput + retained.StandardError);
        HostCheck.Equal(outputHash, HostRunCheckpoint.HashFile(outputs));
        HostCheck.Equal(selfHash, HostRunCheckpoint.HashFile(self));
    }
}
