using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;
using System.Text;

internal static unsafe partial class M02IdtrContinuedExecutionEfiIntegrationTests
{
    // Independently reviewed descendants of the original raw monolith:
    // 7CBF5DD9FB17D8D9608237FC78D06FE00B40D7AFE8669E1196C72B9F1B6848EB.
    // These host witnesses preserve every extracted type; they grant no native admission.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> IdtrOracleTypes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["OS.Kernel.Diagnostics.IDTRContinuedExecution"] =
                ["d10312b39396de6a9d71ad0185cba1dd72f505c0281438339f8330ea33011182"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation"] =
                ["18238952b4e17fbdf4802250ed163aaa435f46f60739989e6767491edb823a43"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionStatus"] =
                ["51f613dd831a0bc893d71df0339a960396bbe0257ea8ceb2e12e850a93d838c7"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionContext"] =
                ["f43f72d842ffdd16495a879b122eeed965bc25dae54d9a57465ca153a1479005"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult"] =
                ["1ec8512a5d0bd937ebdac5d5f07eaa13611a2d28173a9477cc7c636d045e77e8"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult"] =
                ["cd9a2b616930b0711c156d2ce5d457b69c39e1337de2b006032811e56e1b8b07"]
        };

    [HostTest("m02.012 source discovery pins every extracted type independently of source paths")]
    private static void IdtrOracleControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        bool Matches(CSharpSourceCatalog candidate) => HostSourceOracle.MatchesRawFamily(candidate, IdtrOracleTypes);
        HostCheck.True(Matches(source), "complete reviewed kernel family");
        foreach (string owner in IdtrOracleTypes.Keys)
        {
            var document = source.TypeSources(owner).Single();
            CSharpSourceCatalog Change(string path, byte[] bytes) => new(source.Documents.Select(item => item == document
                ? CSharpSourceDocument.FromBytes(path, bytes, (CSharpParseOptions)item.Syntax.Options) : item),
                source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
            byte[] original = Encoding.UTF8.GetBytes(document.Text);
            HostCheck.True(Matches(Change(document.Path + ".moved.cs", original)), "renamed source: " + owner);
            HostCheck.True(!Matches(Change(document.Path, [.. original, (byte)'\n'])), "changed raw bytes: " + owner);
            HostCheck.True(!Matches(Change(document.Path, [0xef, 0xbb, 0xbf, .. original])), "BOM: " + owner);
            HostCheck.True(!Matches(Change(document.Path, Encoding.UTF8.GetBytes(document.Text.ReplaceLineEndings("\r\n")))),
                "alternate newlines: " + owner);
        }
    }
}
