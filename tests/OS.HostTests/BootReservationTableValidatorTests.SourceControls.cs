using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static partial class BootReservationTableValidatorTests
{
    [HostTest("m01.002 source discovery preserves partial families and rejects hidden hosted dependencies")]
    private static void ReservationSourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var actualHost = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var actualEfi = ReservationEfi();
        string shim = ReservationShimForControls();
        var sources = ReservationSources(root, actualHost, actualEfi);
        HostBootPageAllocatorSources.ValidateShimClosure(sources, shim);
        var host = new CSharpSourceCatalog(actualHost.DependencySources(ReservationOwner, RangeOwner), actualHost.Compilation.References);
        var parse = (CSharpParseOptions)sources[0].Syntax.Options;
        var source = new CSharpSourceCatalog(sources.Append(ReservationDocument(Path.Combine(Path.GetTempPath(), "ReservationShim.cs"), shim, parse)), []);
        ReservationBound(host);
        ReservationBound(source);
        ReservationRequire(CheckReservationSource(source), "original complete selected family");
        var method = source.Method(ReservationOwner, "Validate");
        var document = source.Document(method);
        var hostDocument = host.Documents.Single(item => item.Path == document.Path);
        var owner = (TypeDeclarationSyntax)method.Parent!;
        string remaining = document.Text.Remove(method.SpanStart, method.Span.Length);
        string moved = "namespace OS.Kernel.Memory; public static unsafe partial class BootReservationTableValidator {\n" + method + "\n}";
        var movedSource = ReservationChange(source, document, remaining, "ArbitraryValidatorMethod.cs", moved);
        var movedHost = ReservationChange(host, hostDocument, remaining, "ArbitraryValidatorMethod.cs", moved);
        ReservationBound(movedSource);
        ReservationBound(movedHost);
        ReservationRequire(CheckReservationSource(movedSource), "method moved into partial owner");
        HostBootPageAllocatorSources.ValidateShimClosure(ReservationSources(root, movedHost, movedSource), shim);
        foreach (string added in new[]
        {
            "private class HiddenReference { }",
            "private static string HiddenText() => \"forbidden\";"
        })
        {
            string hidden = document.Text.Insert(owner.CloseBraceToken.SpanStart, "\n#if RESERVATION_HIDDEN\n" + added + "\n#endif\n");
            var candidate = ReservationChange(source, document, hidden);
            ReservationBound(candidate);
            var enabled = ReservationDocument(hostDocument.Path, hidden, ((CSharpParseOptions)hostDocument.Syntax.Options).WithPreprocessorSymbols("RESERVATION_HIDDEN"));
            var enabledHost = new CSharpSourceCatalog(host.Documents.Select(item => item.Path == enabled.Path ? enabled : item), host.Compilation.References);
            ReservationBound(enabledHost);
            ReservationRequire(!CheckReservationSource(candidate), "inactive original raw firewall: " + added);
        }
        var inactive = ReservationChange(source, null, null, "HiddenWholePartial.cs",
            "#if RESERVATION_HIDDEN\nnamespace OS.Kernel.Memory; public static unsafe partial class BootReservationTableValidator { private class HiddenReference { } }\n#endif\n");
        ReservationBound(inactive);
        ReservationRequire(!CheckReservationSource(inactive), "fully inactive partial raw class firewall");
        foreach (var (modifiers, accepted) in new[]
        {
            ("public static unsafe partial", true),
            ("public unsafe partial", false),
            ("internal static unsafe partial", false),
            ("private static unsafe partial", false),
            ("public static partial", false),
            ("public static unsafe", false)
        })
        {
            // These additional declarations are inactive in the actual selected
            // compilation, so rejection must come from the raw owner policy.
            var extra = ReservationChange(source, null, null, "InactiveOwner.cs",
                "#if RESERVATION_HIDDEN\nnamespace OS.Kernel.Memory; " + modifiers
                + " class BootReservationTableValidator { }\n#endif\n");
            ReservationBound(extra);
            ReservationRequire(CheckReservationSource(extra) == accepted,
                "inactive owner modifiers and partial consistency: " + modifiers);
        }
        var result = source.TypeSources("OS.Kernel.Memory.BootReservationTableValidationResult").Single();
        var movedResult = ReservationChange(source, result, "", "ArbitraryResult.cs", result.Text);
        ReservationBound(movedResult);
        ReservationRequire(CheckReservationSource(movedResult), "result moved independently");
        var drifted = ReservationChange(source, document, document.Text + "\n// byte drift\n");
        ReservationReject(() => ReservationSources(root, host, drifted), "host/EFI source identity drift");
        ReservationReject(() => HostBootPageAllocatorSources.ValidateShimClosure(sources.Where(item => item.Path != result.Path).ToArray(), shim), "missing dependency");
        ReservationReject(() => HostBootPageAllocatorSources.ValidateShimClosure(sources, shim + "\nnamespace System { public struct Unreviewed { } }"), "expanded shim");
        string conditional = document.Text.Insert(owner.CloseBraceToken.SpanStart,
            "\n#if NOSTDLIB_EFI\nprivate static ConditionalReservationDependency AddedDependency() => default;\n#endif\n");
        const string dependency = "namespace OS.Kernel.Memory; public readonly struct ConditionalReservationDependency { }";
        var conditionalSource = ReservationChange(source, document, conditional, "ConditionalDependency.cs", dependency);
        var conditionalHost = ReservationChange(host, hostDocument, conditional, "ConditionalDependency.cs", dependency);
        ReservationBound(conditionalSource);
        ReservationBound(conditionalHost);
        ReservationReject(() => HostBootPageAllocatorSources.ValidateShimClosure(ReservationSources(root, conditionalHost, conditionalSource), shim), "missing conditional dependency");
        string foreign = document.Text.Insert(owner.CloseBraceToken.SpanStart,
            "\nprivate static Unexpected.Layer.ForeignDependency AddedDependency() => default;\n");
        const string foreignType = "namespace Unexpected.Layer; public readonly struct ForeignDependency { }";
        var foreignSource = ReservationChange(source, document, foreign, "ForeignDependency.cs", foreignType);
        var foreignHost = ReservationChange(host, hostDocument, foreign, "ForeignDependency.cs", foreignType);
        ReservationBound(foreignSource);
        ReservationBound(foreignHost);
        ReservationReject(() => ReservationSources(root, foreignHost, foreignSource), "foreign dependency layer");
    }

    [HostTest("m01.002 append-only source enum is independent of filenames and numeric spelling")]
    private static void ReservationEnumControls()
    {
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        (string Name, byte Value)[] expected = [("First", 0), ("Second", 1)];
        foreach (var (declaration, count, accepted) in new[]
        {
            ("public enum BootReservationKind : byte { First=0, Second=0x1 }", 2, true),
            ("public enum BootReservationKind : byte { First=0, Second=1, Appended=20 }", 3, true),
            ("public enum BootReservationKind : byte { First=0, Second=2 }", 2, false),
            ("public enum BootReservationKind : byte { First=0, Second=1, Alias=1 }", 3, false),
            ("public enum BootReservationKind : byte { First=0, Second=1, Appended=20, Earlier=19 }", 4, false),
            ("public enum BootReservationKind : byte { First=0 }", 1, false),
            ("public enum BootReservationKind : byte { First=0, Second=1,\n#if RESERVATION_HIDDEN\nHidden=20\n#endif\n}", 2, false),
            ("public enum BootReservationKind : int { First=0, Second=1 }", 2, false)
        })
        {
            var document = ReservationDocument(Path.Combine(Path.GetTempPath(), "ArbitraryReservationKind.cs"), "namespace OS.Kernel.Memory; " + declaration, parse);
            var source = new CSharpSourceCatalog([document], references);
            ReservationBound(source);
            ReservationRequire(ReservationKindSource(source, expected, count) == accepted, declaration);
        }
    }

    private static string ReservationShimForControls()
    {
        var source = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var file = source.Compilation.GetTypeByMetadataName(typeof(File).FullName!);
        var path = source.Compilation.GetTypeByMetadataName(typeof(Path).FullName!);
        if (file is null || path is null || file.DeclaringSyntaxReferences.Length != 0 || path.DeclaringSyntaxReferences.Length != 0)
            throw new InvalidOperationException("reservation_shim_framework_identity");
        var method = source.Method(nameof(BootReservationTableValidatorTests), "CreateNoStdLibWorkspace");
        var writes = source.MethodInvocations(method).Where(call => call.TargetMethod.Name == nameof(File.WriteAllText)
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file)
            && HostSourceOperations.Argument(call, 0) is IInvocationOperation destination
            && destination.TargetMethod.Name == nameof(Path.Combine)
            && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path)
            && destination.TargetMethod.Parameters.Length == 2
            && destination.TargetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String)
            && HostSourceOperations.Argument(destination, 1).ConstantValue is { HasValue: true, Value: "NoStdLibShim.cs" }).ToArray();
        if (writes.Length != 1 || HostSourceOperations.Argument(writes[0], 1).ConstantValue is not { HasValue: true, Value: string content })
            throw new InvalidOperationException("reservation_shim_write_identity");
        return content;
    }

    private static CSharpSourceCatalog ReservationChange(CSharpSourceCatalog source, CSharpSourceDocument? document,
        string? text, string? addedName = null, string? addedText = null)
    {
        var documents = source.Documents.Select(item => item == document ? ReservationDocument(item.Path, text!, (CSharpParseOptions)item.Syntax.Options) : item);
        if (addedName is not null)
            documents = documents.Append(ReservationDocument(Path.Combine(Path.GetDirectoryName(document?.Path ?? source.Documents[0].Path)!, addedName), addedText!, (CSharpParseOptions)source.Documents[0].Syntax.Options));
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
    private static CSharpSourceDocument ReservationDocument(string path, string text, CSharpParseOptions options) =>
        CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(text), options);
    private static void ReservationBound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        ReservationRequire(errors.Length == 0, "control binding: " + string.Join("; ", errors.Select(error => error.ToString())));
    }
    private static void ReservationReject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("reservation control accepted " + reason);
    }
    private static void ReservationRequire(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException("reservation control failed " + reason);
    }
}
