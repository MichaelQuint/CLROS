using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static partial class CSharpCompilationReceiptTests
{
    private sealed class ReceiptFixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("clros-receipt-").FullName;
        internal string Path { get; }
        internal string[] Arguments { get; } = ["/target:library", "/langversion:14", "/define:SELECTED"];
        internal XmlDocument Inputs { get; } = new() { XmlResolver = null };
        internal CSharpSourceDocument[] Sources { get; }
        internal CSharpCompilerArtifacts Artifacts { get; }

        internal ReceiptFixture(bool realReference = false)
        {
            Path = System.IO.Path.Combine(Root, "sample.host-compilation.xml");
            Inputs.AppendChild(Inputs.CreateElement("Inputs"));
            var declared = Inputs.CreateElement("Declared");
            Inputs.DocumentElement!.AppendChild(declared);
            foreach (string kind in new[] { "Source", "Source", "Reference", "Analyzer", "Additional", "AnalyzerConfig", "Resource", "Import", "Compiler", "AnalyzerDependency" })
            {
                string path = System.IO.Path.Combine(Root, declared.ChildNodes.Count + ".input");
                if (realReference && kind == "Reference") File.Copy(typeof(object).Assembly.Location, path);
                else File.WriteAllText(path, kind == "Source" ? "partial class Owner {}" : "original " + kind);
                AddFile(declared, path, kind);
            }
            var options = new CSharpParseOptions(LanguageVersion.CSharp14);
            Sources = declared.ChildNodes.Cast<XmlElement>().Where(file => file.GetAttribute("Kind") == "Source")
                .Select(file => CSharpSourceDocument.Read(file.GetAttribute("Path"), options)).ToArray();
            string assembly = System.IO.Path.Combine(Root, "sample.dll"), symbols = System.IO.Path.Combine(Root, "sample.pdb");
            // The consumer validates byte joins; generated-source tests separately
            // exercise real PE/portable PDB structures emitted by Roslyn.
            File.WriteAllText(assembly, "assembly bytes");
            File.WriteAllText(symbols, "symbol bytes");
            Artifacts = new(assembly, Hash(File.ReadAllBytes(assembly)), symbols, Hash(File.ReadAllBytes(symbols)));
            var receipt = new XmlDocument { XmlResolver = null };
            var root = receipt.CreateElement("HostCompilationReceipt");
            receipt.AppendChild(root);
            root.SetAttribute("Version", "1");
            root.SetAttribute("Producer", "SdkTargetsTriggeredByCompilation");
            root.SetAttribute("InputSha256", Hash(Encoding.UTF8.GetBytes(Inputs.OuterXml)));
            root.AppendChild(receipt.ImportNode(Inputs.DocumentElement!, true));
            var arguments = receipt.CreateElement("Arguments");
            root.AppendChild(arguments);
            foreach (string value in Arguments)
            {
                var argument = receipt.CreateElement("Argument");
                argument.InnerText = value;
                arguments.AppendChild(argument);
            }
            var outputs = receipt.CreateElement("Outputs");
            root.AppendChild(outputs);
            AddFile(outputs, assembly, "PE");
            AddFile(outputs, symbols, "PDB");
            receipt.Save(Path);
        }

        internal CSharpCompilationReceipt Read(string[]? arguments = null)
            => CSharpCompilationReceipt.Read(Root, Path, Inputs.OuterXml, arguments ?? Arguments);

        private static void AddFile(XmlElement parent, string path, string kind)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var file = parent.OwnerDocument.CreateElement("File");
            file.SetAttribute("Path", path);
            file.SetAttribute("Kind", kind);
            file.SetAttribute("Length", bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            file.SetAttribute("Sha256", Hash(bytes));
            parent.AppendChild(file);
        }

        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
