using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Kernel.Memory;
using OS.Tooling;
using System.Text;

internal static unsafe partial class PageZeroMappingPolicyTests
{
    private static string PageZeroWrittenSource(CSharpSourceCatalog host, string fileName)
    {
        var method = host.Method(typeof(PageZeroMappingPolicyTests).FullName!, "BuildNoStdLibClosure");
        var file = host.Compilation.GetTypeByMetadataName("System.IO.File") ?? throw new InvalidOperationException("page_zero_file_type_missing");
        var path = host.Compilation.GetTypeByMetadataName("System.IO.Path") ?? throw new InvalidOperationException("page_zero_path_type_missing");
        var call = host.MethodInvocations(method).Single(operation =>
            operation.TargetMethod.Name == nameof(File.WriteAllText)
            && SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, file)
            && HostSourceOperations.Argument(operation, 0) is IInvocationOperation destination
            && destination.TargetMethod.Name == nameof(Path.Combine)
            && destination.TargetMethod.Parameters.Length == 2
            && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path)
            && HostSourceOperations.Argument(destination, 1).ConstantValue.Value is string name
            && name == fileName);
        return HostSourceOperations.Argument(call, 1).ConstantValue.Value as string ?? throw new InvalidOperationException("page_zero_original_source_not_constant");
    }

    [HostTest("m01.015 source discovery retains raw policy, shim and dependency ownership")]
    private static void PageZeroSourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var efi = PageZeroEfi("SingleMinimalApi").Sources;
        string shim = PageZeroWrittenSource(host, "NoStdLibShim.cs");
        Check(kernel, efi, host, true, "actual families and independent original shim");
        var method = kernel.Method(typeof(PageZeroMappingPolicy).FullName!, nameof(PageZeroMappingPolicy.AuditTableImage));
        var document = kernel.Document(method);
        var parent = (ClassDeclarationSyntax)method.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method))).NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))).NormalizeWhitespace().ToFullString();
        Check(ChangePageZero(kernel, document.Path, remaining, moved), ChangePageZero(efi, document.Path, remaining, moved), host, true, "policy method can move into an arbitrarily named partial");
        const string hidden = "#if UNRELATED_DISABLED\nnamespace OS.Kernel.Memory; public static partial class PageZeroMappingPolicy { private const string Bad = \"managed\"; }\n#endif\n";
        Check(ChangePageZero(kernel, document.Path, document.Text, hidden), ChangePageZero(efi, document.Path, document.Text, hidden), host, false, "inactive managed literal remains forbidden by original raw sanitizer");
        Check(ChangePageZero(kernel, document.Path, document.Text + "\n// changed host bytes\n"), efi, host, false, "host and selected EFI bytes must join");
        string call = document.Text.Insert(method.Body!.OpenBraceToken.Span.End, "\n_ = global::Foreign.Dependency.Value;\n");
        const string foreign = "namespace Foreign; public static class Dependency { public static int Value => 1; }";
        Check(ChangePageZero(kernel, document.Path, call, foreign), ChangePageZero(efi, document.Path, call, foreign), host, false, "foreign source cannot enter the POD closure");
        string conditional = document.Text.Insert(method.Body.OpenBraceToken.Span.End, "\n#if NOSTDLIB_EFI\n_ = PageZeroConditionalDependency.Value;\n#endif\n");
        const string conditionalType = "#if NOSTDLIB_EFI\nnamespace OS.Kernel.Memory; public static class PageZeroConditionalDependency { public static int Value => 1; }\n#endif\n";
        Check(ChangePageZero(kernel, document.Path, conditional, conditionalType), ChangePageZero(efi, document.Path, conditional, conditionalType), host, false, "missing conditional dependency fails the original no-reference branch");
        var hostOwner = host.Method(typeof(PageZeroMappingPolicyTests).FullName!, nameof(RunAll));
        var hostDocument = host.Document(hostOwner);
        const string unsafeHidden = "#if UNRELATED_DISABLED\ninternal static unsafe partial class PageZeroMappingPolicyTests { private static byte Bad() => *((byte*)0); }\n#endif\n";
        Check(kernel, efi, ChangePageZero(host, hostDocument.Path, hostDocument.Text, unsafeHidden), false, "inactive fixture pointer dereference remains forbidden");
        const string helperHidden = "#if UNRELATED_DISABLED\ninternal static unsafe partial class M01015TestData { private static byte Bad() => *((byte*)0); }\n#endif\n";
        Check(kernel, efi, ChangePageZero(host, hostDocument.Path, hostDocument.Text, helperHidden), false, "original helper family retains inactive pointer exclusion");
        PageZeroReject(() => ValidatePageZeroShim(PageZeroSources(root, kernel, efi), shim.Replace("public struct SByte { }", "", StringComparison.Ordinal)));
        void Check(CSharpSourceCatalog candidateKernel, CSharpSourceCatalog candidateEfi, CSharpSourceCatalog candidateHost, bool expected, string reason)
        {
            bool accepted;
            try
            {
                accepted = PageZeroRawPolicy(candidateKernel, candidateHost);
                if (accepted)
                    ValidatePageZeroShim(PageZeroSources(root, candidateKernel, candidateEfi), shim);
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            HostCheck.True(accepted == expected, reason);
        }
    }

    [HostTest("m01.015 marker ownership follows partial files and retains global raw exclusions")]
    private static void PageZeroMarkerControls()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-page-zero-source-controls-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "tests", "OS.HostTests");
        Directory.CreateDirectory(directory);
        string first = Path.Combine(directory, "arbitrary-owner.cs");
        string second = Path.Combine(directory, "moved-marker.cs");
        File.WriteAllText(first, "internal static partial class PageZeroMappingPolicyTests { internal static void RunAll() { } } internal static class M01015TestData { } internal sealed class M01015PageZeroFixture { }");
        File.WriteAllText(second, "internal static partial class PageZeroMappingPolicyTests { private const string ReadyMarker = \"" + ReadyMarker + "\"; }");
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        CSharpSourceCatalog Read() => new(Directory.EnumerateFiles(directory, "*.cs").Order(StringComparer.Ordinal).Select(path => CSharpSourceDocument.Read(path, parse)), [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        HostCheck.True(PageZeroMarkerBoundary(root, Read()), "marker may move into another owner partial");
        string outside = Path.Combine(root, "tools");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "uncompiled.ps1"), ReadyMarker);
        HostCheck.True(!PageZeroMarkerBoundary(root, Read()), "uncompiled script marker duplicate remains forbidden");
        File.WriteAllText(Path.Combine(outside, "uncompiled.ps1"), "");
        File.AppendAllText(first, "\n#if UNRELATED_DISABLED\ninternal static partial class PageZeroMappingPolicyTests { private const string Bad = \"" + "OS_" + "BOOT_OK\"; }\n#endif\n");
        HostCheck.True(!PageZeroMarkerBoundary(root, Read()), "inactive owner boot marker remains forbidden");
        File.WriteAllText(first, "internal static partial class PageZeroMappingPolicyTests { internal static void RunAll() { } } internal static class M01015TestData { } internal sealed class M01015PageZeroFixture { }\n#if UNRELATED_DISABLED\ninternal static partial class M01015TestData { private const string Bad = \"" + "OS_" + "BOOT_OK\"; }\n#endif\n");
        HostCheck.True(!PageZeroMarkerBoundary(root, Read()), "original helper family retains inactive boot marker exclusion");
    }

    [HostTest("m01.015 original written shim follows its bound destination")]
    private static void PageZeroWrittenSourceControls()
    {
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var method = host.Method(typeof(PageZeroMappingPolicyTests).FullName!, "BuildNoStdLibClosure");
        var document = host.Document(method);
        string expected = PageZeroWrittenSource(host, "NoStdLibShim.cs");
        string unrelated = document.Text.Insert(method.Body!.OpenBraceToken.Span.End,
            "\nFile.WriteAllText(Path.Combine(root, \"Unrelated.cs\"), \"\"\"unrelated\"\"\");\n");
        HostCheck.True(PageZeroWrittenSource(ChangePageZero(host, document.Path, unrelated), "NoStdLibShim.cs") == expected,
            "an unrelated first literal and write cannot replace the shim");
        var owner = (ClassDeclarationSyntax)method.Parent!;
        var partial = owner.Modifiers.Any(SyntaxKind.PartialKeyword) ? owner : owner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(owner, partial.WithMembers(owner.Members.Remove(method))).NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(owner, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))).NormalizeWhitespace().ToFullString();
        HostCheck.True(PageZeroWrittenSource(ChangePageZero(host, document.Path, remaining, moved), "NoStdLibShim.cs") == expected,
            "the writer may move into an arbitrary partial document");
        string absent = document.Text[..method.SpanStart]
            + method.ToString().Replace("\"NoStdLibShim.cs\"", "\"Different.cs\"", StringComparison.Ordinal)
            + document.Text[method.Span.End..];
        PageZeroReject(() => PageZeroWrittenSource(ChangePageZero(host, document.Path, absent), "NoStdLibShim.cs"));
        string duplicate = document.Text.Insert(method.Body.OpenBraceToken.Span.End,
            "\nFile.WriteAllText(Path.Combine(root, \"NoStdLibShim.cs\"), \"different\");\n");
        PageZeroReject(() => PageZeroWrittenSource(ChangePageZero(host, document.Path, duplicate), "NoStdLibShim.cs"));
    }

    private static CSharpSourceCatalog ChangePageZero(CSharpSourceCatalog source, string path, string replacement, string? added = null)
    {
        var original = source.Documents.Single(document => document.Path == path);
        var parse = (CSharpParseOptions)original.Syntax.Options;
        string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryPageZeroPart.cs");
        var documents = source.Documents.Select(document => document.Path == path ? CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse) : document).ToList();
        if (added is not null)
            documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(added), parse));
        var result = new CSharpSourceCatalog(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var document in result.Documents.Where(document => document.Path == path || document.Path == addedPath))
            HostCheck.True(!result.Compilation.GetSemanticModel(document.Syntax).GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "source control binds before guard evaluation");
        return result;
    }

    private static void PageZeroReject(Action operation)
    {
        try
        {
            operation();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("invalid page-zero source accepted");
    }
}
