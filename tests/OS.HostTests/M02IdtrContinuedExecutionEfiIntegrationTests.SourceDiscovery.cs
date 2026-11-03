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
                ["b211dfd2999018682ba4b6102b2254bb7fe0c81001676dadca04d0f8d027aa29"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionStatus"] =
                ["8656b93701cdf71a2d79c32f37d37a69fbe3bfcffed43de8b5cb55a5eca5af8f"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionContext"] =
                ["491be18a2f0c48b2d1858e8ad7f4caab6ee4455374cd7787da7bd39f3c1d85bd"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult"] =
                ["a96192a71c6a5e1a272824ddb9f8d6ca25b874b95bbc4853420e90666963450f"],
            ["OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult"] =
                ["4d8a4cfe54c78844c282bec3ec40b07b75b2394a31b653bc5e694c830850539a"]
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
