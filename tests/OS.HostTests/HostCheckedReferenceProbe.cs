using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Copies conditional fixture parts into an isolated child referencing
/// the exact generator selected by this host producer's build graph. The child's
/// literal controls exercise public generator APIs; rebuilding the generator from
/// an independent source checkout remains a separate fixture.</summary>
internal static class HostCheckedReferenceProbe
{
    internal static void Prepare(string root, string folder, string childSymbol)
    {
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        string generator = HostToolProcess.GetBuiltToolPath(root, "OS.X64TextSerializerGenerator");
        Write(root, folder, childSymbol, ConditionalSources(host, childSymbol), generator);
    }

    internal static void Write(string root, string folder, string childSymbol,
        IReadOnlyList<CSharpSourceDocument> sources, string generator)
    {
        if (sources.Count == 0)
            throw new InvalidOperationException("checked_reference_probe_sources_empty");
        _ = EfiEvidence.RelativePath(root, generator, "checked_reference_probe_generator_outside_root");
        AuthorityFiles.RequirePlainPath(root, generator, "checked_reference_probe_generator_link");

        var compile = new XElement("ItemGroup");
        foreach (var source in sources)
        {
            string relative = EfiEvidence.RelativePath(root, source.Path, "checked_reference_probe_source_outside_root");
            AuthorityFiles.RequirePlainPath(root, source.Path, "checked_reference_probe_source_link");
            byte[] bytes = File.ReadAllBytes(source.Path);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != source.Sha256)
                throw new InvalidOperationException("checked_reference_probe_source_changed " + relative);
            string copied = Path.Combine("source", relative);
            string target = Path.Combine(folder, copied);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(bytes);
            compile.Add(new XElement("Compile", new XAttribute("Include", copied)));
        }

        File.Copy(Path.Combine(root, "global.json"), Path.Combine(folder, "global.json"), overwrite: false);
        // Original first-party fixture copies retain strict nullable checking
        // even when the independent project lives beneath artifacts/.
        File.WriteAllText(Path.Combine(folder, ".editorconfig"),
            "root = true\n\n[*.cs]\ngenerated_code = false\n", new UTF8Encoding(false));
        var project = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <UseSharedCompilation>false</UseSharedCompilation><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
            </Project>
            """);
        project.Root!.Element("PropertyGroup")!.Add(new XElement("DefineConstants", childSymbol));
        project.Root.Add(compile, new XElement("ItemGroup",
            new XElement("Reference", new XAttribute("Include", "OS.X64TextSerializerGenerator"),
                new XElement("HintPath", generator), new XElement("Private", "true"))));
        project.Save(Path.Combine(folder, "Probe.csproj"));
    }

    internal static void VerifyRuntimeAssembly(string root, string childAssembly)
    {
        string generator = HostToolProcess.GetBuiltToolPath(root, "OS.X64TextSerializerGenerator");
        string copied = Path.Combine(Path.GetDirectoryName(childAssembly)!, Path.GetFileName(generator));
        using var original = File.OpenRead(generator);
        using var runtime = File.OpenRead(copied);
        if (!SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(runtime)))
            throw new InvalidOperationException("checked_reference_probe_runtime_assembly_changed");
    }

    internal static IReadOnlyList<CSharpSourceDocument> ConditionalSources(CSharpSourceCatalog host, string childSymbol)
    {
        var sources = host.Documents.Where(source => !source.IsGenerated
            && source.Syntax.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).Any(directive =>
                    (directive switch
                    {
                        IfDirectiveTriviaSyntax branch => branch.Condition,
                        ElifDirectiveTriviaSyntax branch => branch.Condition,
                        _ => null
                    })?.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(identifier => identifier.Identifier.ValueText == childSymbol) == true)).ToArray();
        if (sources.Length == 0)
            throw new InvalidOperationException("checked_reference_probe_child_sources_missing " + childSymbol);
        return sources;
    }
}
