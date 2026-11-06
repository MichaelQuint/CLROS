using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class M01LoadedImageStackIntegrationTests
{
    private static bool HasProfileBoundEfiCoordinatorCalls()
    {
        const string entryOwner = "OS.Boot.EfiEntry";
        const string stageOwner = entryOwner + "+M01StageOneAdapter";
        const string coordinator = "OS.Boot.M01LoadedImageStackIntegration";
        foreach (string[] arguments in new string[][]
        {
            ["--BootPolicy", "Diagnostic"],
            ["--EnableM01LoadedImageStackProbe"],
            ["--BootPolicy", "VerifiedExit"]
        })
        {
            var source = HostSourceCatalog.Efi(EfiOptions.Parse(arguments, true).Profile());
            var entry = source.Type(entryOwner);
            var stage = source.Type(stageOwner);
            bool enabled = arguments[0] != "--BootPolicy" || arguments[1] == "VerifiedExit";
            foreach (string method in new[]
            {
                "PrepareFromStaticScratch", "RecordTransferFromStaticState",
                "ObserveContinuationRspFromStaticState", "ReportUnexpectedReturnFromStaticState"
            })
            {
                var calls = HostSourceOperations.CallsTo(source, coordinator, method)
                    .Where(call => call.Ancestors().OfType<TypeDeclarationSyntax>().Any(declaration =>
                        SymbolEqualityComparer.Default.Equals(source.Compilation.GetSemanticModel(declaration.SyntaxTree)
                            .GetDeclaredSymbol(declaration), entry))).ToArray();
                if (calls.Length != (enabled ? 1 : 0) || calls.Any(call =>
                    !SymbolEqualityComparer.Default.Equals(source.Compilation.GetSemanticModel(call.SyntaxTree)
                        .GetDeclaredSymbol(call.Ancestors().OfType<TypeDeclarationSyntax>().First()), stage)))
                    return false;
            }

            // Inspect every raw branch here. Tokenizing a complete document
            // would turn inactive preprocessor bodies into trivia.
            string rawEntry = System.Text.RegularExpressions.Regex.Replace(HostSourceText.Documents(source, entryOwner),
                @"\s+", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            // Each supported-profile call above must also be the only physical
            // occurrence. An extra call in an inactive branch cannot hide.
            foreach (string method in new[]
            {
                "PrepareFromStaticScratch", "RecordTransferFromStaticState",
                "ObserveContinuationRspFromStaticState", "ReportUnexpectedReturnFromStaticState"
            })
                if (System.Text.RegularExpressions.Regex.Matches(HostSourceText.Documents(source, entryOwner),
                    @"\b" + method + @"\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant).Count != 1)
                    return false;
            foreach (string forbidden in new[]
            {
                "RunM01LoadedImageStackIntegration", "RecordTransfer(", "ObserveContinuationRsp(",
                "M01LoadedImageStackIntegration.Prepare(", "M01LoadedImageStackIntegration.PrepareWithStubByteMode("
            })
                if (rawEntry.Contains(forbidden, StringComparison.Ordinal))
                    return false;
            if (!enabled)
            {
                string run = HostSourceText.Active(source.Method(stageOwner, "Run"));
                if (!HostSourceText.Contains(run, "return false;"))
                    return false;
            }
        }

        return true;
    }
}
