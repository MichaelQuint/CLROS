using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostCheckedReferenceProbeTests
{
    [HostTest("checked reference child discovery follows exact inactive if and elif symbols across unrelated paths")]
    private static void ConditionalDiscovery()
    {
        var source = new CSharpSourceCatalog(new[]
        {
            Document("moved/one.cs", "#if CHILD_PROBE\nclass ChildOnly {}\n#endif"),
            Document("other/two.cs", "#if OTHER\nclass OtherOnly {}\n#elif CHILD_PROBE\nclass ChildHelper {}\n#endif"),
            Document("negated.cs", "#if !CHILD_PROBE\nclass ParentOnly {}\n#else\nclass NegatedChild {}\n#endif"),
            Document("mixed.cs", "#if CHILD_PROBE || OTHER\nclass MixedChild {}\n#endif"),
            Document("prefix.cs", "#if CHILD_PROBE_EXTRA\nclass PrefixOnly {}\n#endif"),
            Document("comment.cs", "// #if CHILD_PROBE\nclass CommentOnly {}"),
            Document("literal.cs", "class LiteralOnly { const string Text = \"#if CHILD_PROBE\"; }")
        }, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var selected = HostCheckedReferenceProbe.ConditionalSources(source, "CHILD_PROBE");
        HostCheck.True(selected.Select(document => document.Path).SequenceEqual(
            source.Documents.Take(4).Select(document => document.Path), StringComparer.Ordinal));
        try
        {
            HostCheckedReferenceProbe.ConditionalSources(source, "CHILD_MISSING");
            throw new InvalidOperationException("missing child symbol was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("checked_reference_probe_child_sources_missing ", StringComparison.Ordinal))
        {
        }
    }

    [HostTest("checked reference child copies distinct equal basenames with exact bytes and explicit compiler order")]
    private static void CopyInputs()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-checked-reference-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string folder = Path.Combine(root, "artifacts", "child");
            Directory.CreateDirectory(folder);
            byte[] pin = Encoding.UTF8.GetBytes("{}\n");
            File.WriteAllBytes(Path.Combine(root, "global.json"), pin);
            var sources = new[]
            {
                WriteSource("b/shared.cs", [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("class First {}\r\n")]),
                WriteSource("a/shared.cs", Encoding.UTF8.GetBytes("class Second {}\n"))
            };
            string generator = Path.Combine(root, "generator.dll");
            File.WriteAllBytes(generator, [1, 2, 3]);
            HostCheckedReferenceProbe.Write(root, folder, "CHILD_PROBE", sources, generator);
            var project = XDocument.Load(Path.Combine(folder, "Probe.csproj"));
            HostCheck.True(project.Descendants("Compile").Select(item => item.Attribute("Include")!.Value).SequenceEqual(
                new[] { Path.Combine("source", "b/shared.cs"), Path.Combine("source", "a/shared.cs") }, StringComparer.Ordinal));
            HostCheck.True(project.Descendants("EnableDefaultCompileItems").Single().Value == "false");
            HostCheck.True(project.Descendants("Reference").Single().Attribute("Include")!.Value == "OS.X64TextSerializerGenerator"
                && project.Descendants("HintPath").Single().Value == generator
                && project.Descendants("Private").Single().Value == "true");
            HostCheck.True(project.Descendants("Nullable").Single().Value == "enable"
                && project.Descendants("TreatWarningsAsErrors").Single().Value == "true"
                && project.Descendants("DefineConstants").Single().Value == "CHILD_PROBE");
            foreach (var source in sources)
                HostCheck.True(File.ReadAllBytes(Path.Combine(folder, "source", Path.GetRelativePath(root, source.Path)))
                    .AsSpan().SequenceEqual(File.ReadAllBytes(source.Path)));
            HostCheck.True(File.ReadAllBytes(Path.Combine(folder, "global.json")).AsSpan().SequenceEqual(pin));
            HostCheck.True(File.ReadAllText(Path.Combine(folder, ".editorconfig"))
                == "root = true\n\n[*.cs]\ngenerated_code = false\n");

            // A stale catalog cannot publish source bytes with the old identity.
            File.AppendAllText(sources[0].Path, "// changed\n");
            string stale = Path.Combine(root, "artifacts", "stale");
            Directory.CreateDirectory(stale);
            try
            {
                HostCheckedReferenceProbe.Write(root, stale, "CHILD_PROBE", sources, generator);
                throw new InvalidOperationException("changed child source was accepted");
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("checked_reference_probe_source_changed ", StringComparison.Ordinal))
            {
                HostCheck.True(!File.Exists(Path.Combine(stale, "Probe.csproj")));
            }

            CSharpSourceDocument WriteSource(string relative, byte[] bytes)
            {
                string path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
                return CSharpSourceDocument.Read(path, new CSharpParseOptions(LanguageVersion.CSharp14));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CSharpSourceDocument Document(string path, string text) =>
        CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(text), new CSharpParseOptions(LanguageVersion.CSharp14));
}
