using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class DiskStaticExecutionPolicyTests
{
    private const string RealtimeOwner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
    private const string PeerOwner = "OS.Tooling.NativeAotDiskStaticPeer";

    private static bool RealtimeSource(CSharpSourceCatalog source)
    {
        var method = source.Method(RealtimeOwner, "AdmitRealtime");
        _ = source.MethodInvocations(method);
        string normalized = string.Concat(method.ToString().Where(character => !char.IsWhiteSpace(character)));
        return normalized.Contains("maximumElapsedSeconds:Probes.EnableM16014Probe?0UL:Probes.EnableM10018FullProbe?DiskStaticExecutionPolicy.RealtimeMaximumElapsedSeconds:600UL,continuing:Probes.EnableM16014Probe", StringComparison.Ordinal);
    }

    private static bool TimeoutSource(CSharpSourceCatalog source)
    {
        var method = source.Method(PeerOwner, "RunCoreAsync");
        _ = source.MethodInvocations(method);
        string normalized = string.Concat(method.ToString().Where(character => !char.IsWhiteSpace(character)));
        var model = source.Compilation.GetSemanticModel(method.SyntaxTree);
        var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression is MemberAccessExpressionSyntax member && member.Expression is IdentifierNameSyntax receiver &&
            receiver.Identifier.ValueText == "matrix" && member.Name.Identifier.ValueText == "CancelAfter").ToArray();
        if (calls.Length != 1 || model.GetSymbolInfo(calls[0]).Symbol is not IMethodSymbol cancel ||
            cancel.ContainingType.ToDisplayString() != "System.Threading.CancellationTokenSource" ||
            calls[0].ArgumentList.Arguments.SingleOrDefault()?.Expression is not InvocationExpressionSyntax seconds ||
            model.GetSymbolInfo(seconds).Symbol is not IMethodSymbol factory ||
            factory.ContainingType.ToDisplayString() != "System.TimeSpan" || factory.Name != "FromSeconds" ||
            seconds.ArgumentList.Arguments.SingleOrDefault()?.Expression is not ConditionalExpressionSyntax duration ||
            duration.Condition is not IdentifierNameSyntax { Identifier.ValueText: "full" })
            return false;
        var full = model.GetConstantValue(duration.WhenTrue);
        var development = model.GetConstantValue(duration.WhenFalse);
        return normalized.Contains("awaitbrowser!(port,Path.Combine(directory,\"browser\"),owned.Token);", StringComparison.Ordinal)
            && full.HasValue && Convert.ToDouble(full.Value, System.Globalization.CultureInfo.InvariantCulture) == 1700
            && development.HasValue && Convert.ToDouble(development.Value, System.Globalization.CultureInfo.InvariantCulture) == DiskStaticHttpContract.MatrixTimeoutSeconds
            && normalized.Contains("owned.Cancel();if(peerisnotnull)try{awaitpeer;}catch{}", StringComparison.Ordinal);
    }

    [HostTest("disk policy source discovery follows moved partials and rejects changed clock or cancellation contracts")]
    private static void DiskPolicySourceControls()
    {
        var realtime = HostSourceCatalog.Efi(EfiOptions.Parse(["--EnableM10018FullProbe"], false).Profile());
        var peer = HostSourceCatalog.Project("tools/OS.Tooling/OS.Tooling.csproj");
        foreach (var (source, owner, name, check) in new[]
        {
            (realtime, RealtimeOwner, "AdmitRealtime", (Func<CSharpSourceCatalog, bool>)RealtimeSource),
            (peer, PeerOwner, "RunCoreAsync", (Func<CSharpSourceCatalog, bool>)TimeoutSource)
        })
        {
            HostCheck.True(check(source), "original source policy");
            var method = source.Method(owner, name);
            var document = source.Document(method);
            var root = method.SyntaxTree.GetCompilationUnitRoot();
            MemberDeclarationSyntax moved = method.WithoutLeadingTrivia().WithoutTrailingTrivia();
            foreach (var parent in method.Ancestors().OfType<TypeDeclarationSyntax>())
                moved = parent.WithMembers(SyntaxFactory.SingletonList(moved))
                    .WithoutLeadingTrivia().WithoutTrailingTrivia();
            string space = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!.ContainingNamespace.ToDisplayString();
            string imports = string.Join("\n", root.Usings.Concat(method.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>().Reverse().SelectMany(parent => parent.Usings))
                .Select(item => item.WithoutTrivia().ToString()));
            string movedText = imports + "\nnamespace " + space + ";\n" + moved.NormalizeWhitespace().ToFullString();
            var changed = DiskPolicyChange(source, document, document.Text.Remove(method.SpanStart, method.Span.Length),
                "ArbitraryPolicyPartial.cs", movedText);
            DiskPolicyBound(changed, owner, name);
            HostCheck.True(check(changed), "policy method moves independently of its original file");
        }
        foreach (var (source, owner, name, before, after, check) in new[]
        {
            (realtime, RealtimeOwner, "AdmitRealtime", ": 600UL", ": 601UL", (Func<CSharpSourceCatalog, bool>)RealtimeSource),
            (realtime, RealtimeOwner, "AdmitRealtime", "continuing: Probes.EnableM16014Probe", "continuing: false", (Func<CSharpSourceCatalog, bool>)RealtimeSource),
            (peer, PeerOwner, "RunCoreAsync", "full ? DiskStaticCapturePolicy.HttpMatrixTimeoutSeconds", "full ? 1701", (Func<CSharpSourceCatalog, bool>)TimeoutSource),
            (peer, PeerOwner, "RunCoreAsync", "Path.Combine(directory, \"browser\"), owned.Token", "Path.Combine(directory, \"browser\"), CancellationToken.None", (Func<CSharpSourceCatalog, bool>)TimeoutSource),
            (peer, PeerOwner, "RunCoreAsync", "owned.Cancel();", "owned.CancelAfter(1);", (Func<CSharpSourceCatalog, bool>)TimeoutSource)
        })
        {
            var method = source.Method(owner, name);
            var document = source.Document(method);
            string text = method.ToString();
            HostCheck.True(text.Contains(before, StringComparison.Ordinal), "mutation anchor exists");
            string changedMethod = text.Replace(before, after, StringComparison.Ordinal);
            string changedText = document.Text.Remove(method.SpanStart, method.Span.Length).Insert(method.SpanStart, changedMethod);
            var changed = DiskPolicyChange(source, document, changedText);
            DiskPolicyBound(changed, owner, name);
            HostCheck.True(!check(changed), "independent clock and cancellation policy rejects mutation");
            var decoy = DiskPolicyChange(source, document,
                changedText + "\n/* Unrelated raw copy:\n" + text.Replace("*/", "* /", StringComparison.Ordinal) + "\n*/\n");
            DiskPolicyBound(decoy, owner, name);
            HostCheck.True(!check(decoy), "unrelated raw text cannot supply the selected method policy");
        }
        var field = peer.Type("OS.Tooling.DiskStaticCapturePolicy").GetMembers("HttpMatrixTimeoutSeconds")
            .OfType<IFieldSymbol>().Single();
        var declaration = (VariableDeclaratorSyntax)field.DeclaringSyntaxReferences.Single().GetSyntax();
        var initializer = declaration.Initializer!.Value;
        var policy = peer.Document(initializer);
        string policyText = policy.Text.Remove(initializer.SpanStart, initializer.Span.Length).Insert(initializer.SpanStart, "1701");
        var changedPolicy = DiskPolicyChange(peer, policy, policyText);
        DiskPolicyBound(changedPolicy, PeerOwner, "RunCoreAsync");
        HostCheck.True(!TimeoutSource(changedPolicy), "changing the resolved constant cannot preserve the accepted timeout");
    }

    private static CSharpSourceCatalog DiskPolicyChange(CSharpSourceCatalog source, CSharpSourceDocument original,
        string text, string? addedName = null, string? addedText = null)
    {
        var options = (CSharpParseOptions)original.Syntax.Options;
        var documents = source.Documents.Select(document => document == original
            ? CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(text), options) : document);
        if (addedName is not null)
            documents = documents.Append(CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(original.Path)!, addedName),
                System.Text.Encoding.UTF8.GetBytes(addedText!), options));
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static void DiskPolicyBound(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        var errors = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDiagnostics(method.Span)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        HostCheck.True(errors.Length == 0, "policy mutation binds: " + string.Join("; ", errors.Select(error => error.ToString())));
    }
}
