using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OS.Tooling;
public static class ContractAudit
{
    public static XDocument ReadProject(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(reader);
    }

    public static IReadOnlyList<ContractMismatch> InspectProject(XDocument project)
    {
        List<ContractMismatch> failures = [];
        string[] compile = project.Descendants("Compile").Select(node => (string? )node.Attribute("Include") + "|" + (string? )node.Attribute("Link")).ToArray();
        CompareOrdered("efi.compile", EfiContracts.CompileItems, compile, failures);
        string[] probes = project.Descendants("DefineConstants").Select(node => (string? )node.Parent?.Attribute("Condition") ?? "").SelectMany(condition => Regex.Matches(condition, @"\$\((Enable\w+Probe)\)").Select(match => match.Groups[1].Value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        CompareOrdered("efi.probes", EfiContracts.DiagnosticProbes, probes, failures);
        foreach ((string name, string expected)in RequiredProperties)
        {
            string[] actual = project.Descendants(name).Select(node => node.Value).ToArray();
            if (actual.Length != 1 || actual[0] != expected)
                failures.Add(new("efi.property." + name, expected, string.Join(";", actual)));
        }

        return failures;
    }

    public static IReadOnlyList<ContractMismatch> Inspect(string root)
    {
        string path = Path.Combine(root, "src", "OS", "OS.csproj");
        List<ContractMismatch> failures = [..InspectProject(ReadProject(path))];
        string source = HashSource(File.ReadAllText(path));
        if (source != EfiContracts.ProjectSourceSha256)
            failures.Add(new("efi.project.sourceSha256", EfiContracts.ProjectSourceSha256, source));
        return failures;
    }

    private static readonly (string Name, string Value)[] RequiredProperties = [("TargetFramework", "net11.0"), ("LangVersion", "14.0"), ("NoStdLib", "true"), ("NoConfig", "true"), ("EnableDefaultCompileItems", "false"), ("DisableImplicitFrameworkReferences", "true"), ("EntryPointSymbol", "EfiMain"), ("LinkerSubsystem", "EFI_APPLICATION")];
    public static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    public static string HashText(string value) => Hash(Encoding.UTF8.GetBytes(value));
    public static string HashSource(string value) => HashText(value.Replace("\r\n", "\n", StringComparison.Ordinal));
    public static void CompareOrdered(string name, IReadOnlyList<string> expected, IReadOnlyList<string> actual, ICollection<ContractMismatch> failures)
    {
        for (int i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            string e = i < expected.Count ? expected[i] : "<absent>";
            string a = i < actual.Count ? actual[i] : "<absent>";
            if (e != a)
                failures.Add(new($"{name}[{i}]", e, a));
        }
    }
}
