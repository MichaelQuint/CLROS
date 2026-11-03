using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class LinuxDiskRaceSourceGuardTests
{
    [HostTest("disk-race actual method movement preserves guards while reordered callbacks and extra progress reject")]
    private static void ActualSnapshotControls() => ActualSnapshotControls(
        HostSourceCatalog.Efi(EfiOptions.Parse(["--EnableM16025Probe"], false).Profile()));

    internal static void ActualSnapshotControls(CSharpSourceCatalog source)
    {
        var method = source.Method(LinuxDiskRaceSourceGuard.Owner, "IsNativeDiskExitPackage");
        var root = method.SyntaxTree.GetRoot();
        var moved = source.Documents.Select(document => document.Syntax == method.SyntaxTree
            ? new CSharpSourceDocument(document.Path, "in-memory method move", CSharpSyntaxTree.Create(
                (CompilationUnitSyntax)root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia)!, (CSharpParseOptions)method.SyntaxTree.Options, document.Path))
            : document).ToList();
        string added = "namespace OS.Boot; public static unsafe partial class EfiEntry { private static partial class M03FirstExitAdapter { " + method.ToString() + " } }";
        moved.Add(new("moved-disk-selector.cs", "in-memory method move", CSharpSyntaxTree.ParseText(added,
            (CSharpParseOptions)method.SyntaxTree.Options, "moved-disk-selector.cs")));
        var shifted = new CSharpSourceCatalog(moved, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(new LinuxDiskRaceSourceGuard(shifted).PackageMatches(LinuxDiskRaceProofFixture.CreatePackage("ExitPending"), true));

        CSharpSourceCatalog Change(string name, Func<MethodDeclarationSyntax, MethodDeclarationSyntax> change)
        {
            var selected = source.Method(LinuxDiskRaceSourceGuard.Owner, name);
            var replacement = (CompilationUnitSyntax)selected.SyntaxTree.GetRoot().ReplaceNode(selected, change(selected));
            var documents = source.Documents.Select(document => document.Syntax == selected.SyntaxTree
                ? new CSharpSourceDocument(document.Path, "in-memory mutation", CSharpSyntaxTree.Create(replacement,
                    (CSharpParseOptions)selected.SyntaxTree.Options, document.Path)) : document);
            return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        }
        MethodDeclarationSyntax Prepend(MethodDeclarationSyntax selected, string suffix) => selected.WithBody(selected.Body!
            .WithStatements(selected.Body.Statements.Insert(0, SyntaxFactory.ParseStatement(selected.ParameterList.Parameters[0].Identifier.Text + suffix))));
        Reject(() => new LinuxDiskRaceSourceGuard(Change("PollNativeCloseReuseDiskReadiness",
            selected => Prepend(selected, "->Process.PollDiskReadiness(" + selected.ParameterList.Parameters[0].Identifier.Text + "->Key, 2);")))
            .CloseReuse(LinuxDiskRaceProofFixture.CreatePackage("CloseReuse").Length), "PollNativeCloseReuseDiskReadiness calls PollDiskReadiness");
        Reject(() => new LinuxDiskRaceSourceGuard(Change("ObserveNativeDiskExitCall", selected =>
            selected.WithBody(selected.Body!.WithStatements(selected.Body.Statements.Insert(0,
                SyntaxFactory.ParseStatement("s_nativeDisk.Device.CompletedCount++;")))))).ExitOrdering(), "ObserveNativeDiskExitCall must not write CompletedCount");
        Reject(() => new LinuxDiskRaceSourceGuard(Change("RunLinuxNativeOwner", selected =>
        {
            var calls = source.MethodInvocations(selected).Where(call => call.TargetMethod.Name is "ClearLinuxRandom" or "ObserveNativeDiskExitMemoryRestored")
                .Select(call => (InvocationExpressionSyntax)call.Syntax).ToArray();
            HostCheck.Equal(2, calls.Length);
            return selected.ReplaceNodes(calls, (before, _) => before.WithExpression(SyntaxFactory.IdentifierName(
                ((IdentifierNameSyntax)before.Expression).Identifier.ValueText == "ClearLinuxRandom" ? "ObserveNativeDiskExitMemoryRestored" : "ClearLinuxRandom")));
        })).ExitOrdering(), "RunLinuxNativeOwner observation order");
    }

    [HostTest("disk-race source discovery accepts moved locals and rejects digest owner and ordering changes")]
    private static void IndependentControls()
    {
        byte[] canonical = [1, 2, 3, 4, 5];
        byte[] digest = System.Security.Cryptography.SHA256.HashData(canonical);
        string Word(int index) => "0x" + System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(digest.AsSpan(index * 8)).ToString("x16");
        string equal = string.Join(" && ", Enumerable.Range(0, 4).Select(index => "digest[" + index + "] == " + Word(index)));
        string unequal = equal.Replace(" == ", " != ", StringComparison.Ordinal).Replace(" && ", " || ", StringComparison.Ordinal);
        string exit = $$"""
            private static bool IsNativeDiskExitPackage(Storage* state)
            {
                if (state == null || state->LinuxBytes != 5 || state->LinuxPackage == 0) return false;
                ulong* digest = stackalloc ulong[4];
                return OS.Std.Hash.Sha256.TryCompute((byte*)state->LinuxPackage, state->LinuxBytes, (byte*)digest, 32) && {{equal}};
            }
            """;
        string close = $$"""
            private static bool SelectNativeCloseReusePackage(Storage* state, out bool selected)
            {
                selected = false;
                if (state == null) return false;
                if (state->LinuxBytes != 5) return true;
                ulong* digest = stackalloc ulong[4];
                if (state->LinuxPackage == 0 || !OS.Std.Hash.Sha256.TryCompute((byte*)state->LinuxPackage, state->LinuxBytes, (byte*)digest, 32)) return false;
                if ({{unequal}}) return true;
                selected = true;
                return true;
            }
            """;
        foreach (var guard in new[] { Create(exit, close), Create(exit.Replace("state", "source").Replace("digest", "hashBuffer"), close),
            Create(exit.Replace("return false;", "{ return false; }").Replace("OS.Std.Hash.Sha256.", "Digest."), close, alias: true) })
        {
            HostCheck.True(guard.PackageMatches(canonical, true));
            HostCheck.True(guard.PackageMatches(canonical, false));
            HostCheck.True(!guard.PackageMatches([1, 2, 3, 4, 6], true));
        }
        foreach (string changed in new[]
        {
            exit.Replace("digest[3]", "digest[2]"),
            exit.Replace(" && " + "digest[0]", " || " + "digest[0]"),
            exit.Replace("LinuxBytes != 5", "LinuxBytes != 4"),
            exit.Replace("(byte*)digest, 32", "(byte*)digest, 8"),
            exit.Replace("return OS.Std.Hash.Sha256.TryCompute", "if (false) return OS.Std.Hash.Sha256.TryCompute").Replace(";\n}", ";\nreturn true;\n}"),
            exit.Replace("digest[0] == " + Word(0), "digest[0] == 1")
        })
            Reject(() => HostCheck.True(Create(changed, close).PackageMatches(canonical, true)));
        Reject(() => HostCheck.True(Create(exit.Replace("OS.Std.Hash.Sha256.", "Foreign."), close).PackageMatches(canonical, true)));
        Reject(() => HostCheck.True(Create(exit, close.Replace("selected = true;", "selected = false;")).PackageMatches(canonical, false)));
        var ordered = Create(exit, close);
        ordered.Ordered("Sequence", "First(VAR_input)", "Second(VAR_input)");
        ordered.Calls("Sequence", "First", count: 1);
        ordered.Calls("Sequence", "Second", count: 1);
        Reject(() => ordered.Ordered("Sequence", "Second(VAR_input)", "First(VAR_input)"));
        Reject(() => ordered.Ordered("Different", "First(VAR_input)", "Second(VAR_input)"));
        Reject(() => ordered.Calls("ForeignSequence", "First"));
        Reject(() => ordered.Ordered("Hidden", "First(VAR_input)"));
    }

    private static LinuxDiskRaceSourceGuard Create(string exit, string close, bool alias = false)
    {
        string Wrapper(string member) => (alias ? "using Digest = OS.Std.Hash.Sha256;\n" : "")
            + "namespace OS.Boot; public static unsafe partial class EfiEntry { private static partial class M03FirstExitAdapter { " + member + " } }";
        string[] sources = [Wrapper(exit), Wrapper(close), Wrapper("""
            private struct Storage { public ulong LinuxBytes, LinuxPackage; }
            private static void First(uint value) { }
            private static void Second(uint value) { }
            private static void Sequence(uint renamed) { First(renamed); Second(renamed); }
            private static void Different(uint one, uint two) { First(one); Second(two); }
            private static void ForeignSequence(uint value) { Foreign.First(value); }
            private static void Hidden(uint value) { void Never() { First(value); } }
            """), """
            namespace OS.Std.Hash { public static unsafe class Sha256 { public static bool TryCompute(byte* input, ulong length, byte* output, uint bytes) => true; } }
            public static unsafe class Foreign { public static bool TryCompute(byte* input, ulong length, byte* output, uint bytes) => true; public static void First(uint value) { } }
            """];
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var documents = sources.Select((text, index) => new CSharpSourceDocument("moved-" + index + ".cs", "synthetic",
            CSharpSyntaxTree.ParseText(text, options, "moved-" + index + ".cs")));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        return new(new CSharpSourceCatalog(documents, references));
    }

    private static void Reject(Action action, string? reason = null)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException error) when (reason is null || error.Message.Contains(reason, StringComparison.Ordinal)) { rejected = true; }
        HostCheck.True(rejected, "mutated disk-race source must reject");
    }
}
