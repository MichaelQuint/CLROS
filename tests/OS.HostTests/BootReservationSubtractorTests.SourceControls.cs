using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class BootReservationSubtractorTests
{
    [HostTest("reservation subtractor discovery preserves moved families and rejects boundary or closure drift")]
    internal static void SourceDiscoveryControls()
    {
        var source = SubtractorEfi();
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        Require(CheckSubtractorSource(source), "baseline");
        var method = source.Method(SubtractorOwner, "Subtract");
        var owner = (TypeDeclarationSyntax)method.Parent!;
        var document = source.Document(method);
        var partialOwner = owner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var remaining = partialOwner.WithMembers(owner.Members.Remove(method));
        var moved = partialOwner.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method));
        var root = document.Syntax.GetRoot().ReplaceNode(owner, remaining);
        string movedText = SyntaxFactory.CompilationUnit().AddMembers(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName("OS.Kernel.Memory")).AddMembers(moved)).NormalizeWhitespace().ToFullString();
        var movedSource = Change(source, document, root.NormalizeWhitespace().ToFullString(), "MovedSubtract.cs", movedText);
        Check(movedSource, true, "moved partial method");
        var maximum = (VariableDeclaratorSyntax)source.Type(SubtractorOwner).GetMembers("MaximumUnsignedLong").OfType<IFieldSymbol>().Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var maximumDocument = source.Document(maximum);
        Check(Change(source, maximumDocument, maximumDocument.Text.Remove(maximum.Initializer!.Value.SpanStart, maximum.Initializer.Value.Span.Length).Insert(maximum.Initializer.Value.SpanStart, "~1UL")), false, "wrong maximum");
        Check(Change(Change(source, document, document.Syntax.GetRoot().ReplaceNode(owner, partialOwner).NormalizeWhitespace().ToFullString()), null, null, "HiddenSubtractor.cs", """
            #if SUBTRACTOR_REVIEW_HIDDEN
            namespace OS.Kernel.Memory;
            public static unsafe partial class BootReservationSubtractor { private static string Hidden() => "hidden"; }
            #endif
            """), false, "inactive forbidden partial");
        Check(Change(source, null, null, "NestedBootCall.cs", """
            namespace OS.Boot;
            public static unsafe partial class EfiEntry
            {
                private static class NestedSubtractorCaller
                {
                    private static void Call() => OS.Kernel.Memory.BootReservationSubtractor.Subtract(null, 0, 0, null, 0, 0, 0, null, 0);
                }
            }
            """), false, "nested integration caller");
        Check(Change(source, null, null, "MovedLegacyPlanner.cs", "namespace OS.Kernel.Memory; public static class BootPageAllocatorPlanner { }"), false, "excluded planner under arbitrary filename");
        string enumOwner = "OS.Kernel.Memory.BootReservationSourceRangeStatus";
        var enumType = source.Type(enumOwner);
        var enumFields = enumType.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).ToArray();
        var expected = enumFields.Select(field => (field.Name, (byte)field.ConstantValue!)).ToArray();
        Require(CheckSubtractorEnum(source, enumOwner, expected), "enum baseline");
        var enumNode = (EnumDeclarationSyntax)enumType.DeclaringSyntaxReferences.Single().GetSyntax();
        var enumDocument = source.Document(enumNode);
        var enumMoved = Change(source, enumDocument, "", "MovedStatuses.cs", enumDocument.Text);
        Require(CheckSubtractorEnum(enumMoved, enumOwner, expected), "moved enum");
        var firstMember = enumNode.Members[0];
        var wrongEnum = enumNode.ReplaceNode(firstMember, firstMember.WithEqualsValue(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(99)))));
        var wrongEnumSource = Change(source, enumDocument, enumDocument.Syntax.GetRoot().ReplaceNode(enumNode, wrongEnum).NormalizeWhitespace().ToFullString());
        Bound(wrongEnumSource);
        Require(!CheckSubtractorEnum(wrongEnumSource, enumOwner, expected), "wrong enum value");
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        var closure = SubtractorSources(repositoryRoot, host, source);
        string shim = SubtractorShimForControls();
        ValidateSubtractorShimClosure(closure, shim);
        var movedHost = Change(host, host.Documents.Single(item => item.Path == document.Path), root.NormalizeWhitespace().ToFullString(), "MovedSubtract.cs", movedText);
        Bound(movedHost);
        ValidateSubtractorShimClosure(SubtractorSources(repositoryRoot, movedHost, movedSource), shim);
        Reject(() => ValidateSubtractorShimClosure(closure.Where(item => item.Path != enumDocument.Path).ToArray(), shim), "missing closure member");
        var drifted = Change(source, enumDocument, enumDocument.Text + "\n// source identity changed\n");
        Reject(() => SubtractorSources(repositoryRoot, host, drifted), "EFI source hash drift");
        Reject(() => ValidateSubtractorShimClosure(closure, shim + "\nnamespace System { public struct UnexpectedShimType { } }"), "unexpected shim type");
        string conditionalText = document.Text.Insert(owner.CloseBraceToken.SpanStart, """

            #if NOSTDLIB_EFI
            private static ConditionalHelper ConditionalDependency() => default;
            #endif

            """);
        const string conditionalHelper = "namespace OS.Kernel.Memory; public readonly struct ConditionalHelper { }";
        var conditionalEfi = Change(source, document, conditionalText, "ConditionalHelper.cs", conditionalHelper);
        var hostDocument = host.Documents.Single(item => item.Path == document.Path);
        var conditionalHost = Change(host, hostDocument, conditionalText, "ConditionalHelper.cs", conditionalHelper);
        Bound(conditionalEfi);
        Bound(conditionalHost);
        var conditionalClosure = SubtractorSources(repositoryRoot, conditionalHost, conditionalEfi);
        Reject(() => ValidateSubtractorShimClosure(conditionalClosure, shim), "missing conditional dependency");
        static void Check(CSharpSourceCatalog candidate, bool expected, string name)
        {
            Bound(candidate);
            Require(CheckSubtractorSource(candidate) == expected, name);
        }
    }

    private static string SubtractorShimForControls() => SelectSubtractorShim(HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj"));
    private static CSharpSourceCatalog Change(CSharpSourceCatalog source, CSharpSourceDocument? document, string? text, string? addedName = null, string? addedText = null)
    {
        var documents = source.Documents.Select(item => item == document ? CSharpSourceDocument.FromBytes(item.Path, System.Text.Encoding.UTF8.GetBytes(text!), (CSharpParseOptions)item.Syntax.Options) : item);
        if (addedName is not null)
            documents = documents.Append(CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(document?.Path ?? source.Documents[0].Path)!, addedName), System.Text.Encoding.UTF8.GetBytes(addedText!), (CSharpParseOptions)source.Documents[0].Syntax.Options));
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static void Bound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, "control binding: " + string.Join("; ", errors.Select(error => error.ToString())));
    }

    private static void Reject(Action action, string name)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("subtractor control accepted " + name);
    }

    private static void Require(bool value, string name)
    {
        if (!value)
            throw new InvalidOperationException("subtractor control failed " + name);
    }

    // Bind the file write and its destination before reading constant content.
    // Literal order and physical placement of the workspace builder are irrelevant.
    private static string SelectSubtractorShim(CSharpSourceCatalog source)
    {
        var file = source.Compilation.GetTypeByMetadataName(typeof(File).FullName!);
        var path = source.Compilation.GetTypeByMetadataName(typeof(Path).FullName!);
        if (file is null || path is null || file.DeclaringSyntaxReferences.Length != 0 || path.DeclaringSyntaxReferences.Length != 0)
            throw new InvalidOperationException("subtractor_shim_framework_identity");
        var method = source.Method(nameof(BootReservationSubtractorTests), "CreateNoStdLibWorkspace");
        var writes = source.MethodInvocations(method).Where(call => call.TargetMethod.Name == nameof(File.WriteAllText) && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file) && HostSourceOperations.Argument(call, 0) is Microsoft.CodeAnalysis.Operations.IInvocationOperation destination && destination.TargetMethod.Name == nameof(Path.Combine) && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path) && destination.TargetMethod.Parameters.Length == 2 && destination.TargetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String) && HostSourceOperations.Argument(destination, 1).ConstantValue is { HasValue: true, Value: "NoStdLibShim.cs" }).ToArray();
        if (writes.Length != 1)
            throw new InvalidOperationException("subtractor_shim_write_count");
        if (HostSourceOperations.Argument(writes[0], 1).ConstantValue is not { HasValue: true, Value: string content })
            throw new InvalidOperationException("subtractor_shim_content_not_constant");
        return content;
    }

    [HostTest("reservation subtractor shim discovery binds its write and rejects missing or ambiguous destinations")]
    internal static void ShimSelectionControls()
    {
        const string target = """"
        FileWriter.WriteAllText(Paths.Combine(workspace, "NoStdLibShim.cs"), """
        shim content
        """, System.Text.Encoding.UTF8);
        """";
        const string unrelated = """"
        string other = """
        unrelated raw literal
        """;
        FileWriter.WriteAllText(Paths.Combine(workspace, "other.txt"), """
        unrelated file content
        """, System.Text.Encoding.UTF8);
        """";
        Require(SelectSubtractorShim(Catalog(target)) == "shim content", "bound shim baseline");
        Require(SelectSubtractorShim(Catalog(target, moved: true)) == "shim content", "moved workspace method");
        Require(SelectSubtractorShim(Catalog(unrelated + "\n" + target)) == "shim content", "unrelated first raw literal and write");
        Reject(() => SelectSubtractorShim(Catalog(unrelated)), "missing shim write");
        Reject(() => SelectSubtractorShim(Catalog(target + "\n" + target)), "ambiguous shim writes");
        Reject(() => SelectSubtractorShim(Catalog(target.Replace("FileWriter.WriteAllText", "Foreign.WriteAllText", StringComparison.Ordinal), additional: "private static class Foreign { internal static void WriteAllText(string path, string contents, System.Text.Encoding encoding) { } }")), "foreign writer with identical spelling");
        static CSharpSourceCatalog Catalog(string body, bool moved = false, string additional = "")
        {
            const string imports = "using FileWriter = System.IO.File; using Paths = System.IO.Path; ";
            string method = "private static void CreateNoStdLibWorkspace() { const string workspace = \"fixture\";\n" + body + "\n}";
            var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
            var documents = new List<CSharpSourceDocument>
            {
                CSharpSourceDocument.FromBytes("arbitrary-fixture.cs", System.Text.Encoding.UTF8.GetBytes(imports + "internal static partial class BootReservationSubtractorTests { " + (moved ? "" : method) + additional + " }"), parse)
            };
            if (moved)
                documents.Add(CSharpSourceDocument.FromBytes("moved-workspace-builder.cs", System.Text.Encoding.UTF8.GetBytes(imports + "internal static partial class BootReservationSubtractorTests { " + method + " }"), parse));
            var source = new CSharpSourceCatalog(documents, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            Bound(source);
            return source;
        }
    }
}
