using System.Text;
using static HostRuntimeAllocationBoundary;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class NoStdLibShimBoundaryTests
{
    [HostTest("nostdlib allocation guards follow moved files and reject truncated or discarded results")]
    private static void AllocationControls()
    {
        const string owner = "System.Runtime.RhpNewFastRuntime";
        var fixedAllocation = AllocationProfile();
        var arrayAllocation = AllocationProfile("EnableM04SzArrayAllocationProbe");
        var declaration = fixedAllocation.Method(owner, "RhpNewFast");
        var document = fixedAllocation.Document(declaration);
        HostCheck.True(Allocates(Rebuild(fixedAllocation, document,
            document.Text, document.Path + ".moved.cs"), "RhpNewFast", "Allocate"), "moved source");

        const string allocation = "OS.Runtime.RhpNewFastAllocation.Allocate((ulong)methodTable)";
        const string reference = "System.Runtime.CompilerServices.Unsafe.As<nuint, object>(ref reference)";
        const string body = "{ nuint reference = (nuint)" + allocation + "; return " + reference + "; }";
        var equivalent = Body(fixedAllocation, owner, "RhpNewFast", body.Replace("reference", "result", StringComparison.Ordinal));
        HostCheck.True(Allocates(equivalent, "RhpNewFast", "Allocate"), "local name does not define the contract");
        foreach (string changed in new[]
        {
            body.Replace("(ulong)methodTable", "(ulong)(uint)(nuint)methodTable", StringComparison.Ordinal),
            body.Replace("(nuint)OS.Runtime", "(nuint)(uint)OS.Runtime", StringComparison.Ordinal),
            body.Replace("return " + reference + ";", reference + "; return null;", StringComparison.Ordinal)
        })
            HostCheck.True(!Allocates(Body(fixedAllocation, owner, "RhpNewFast", changed), "RhpNewFast", "Allocate"), changed);
        var narrowedLength = Body(arrayAllocation, owner, "RhpNewArrayFast",
            "{ nuint reference = (nuint)OS.Runtime.RhpNewFastAllocation.AllocateArray((ulong)methodTable, (long)(short)length, false); return " + reference + "; }");
        HostCheck.True(!Allocates(narrowedLength, "RhpNewArrayFast", "AllocateArray", false), "length narrowing");
        var byReference = Change(fixedAllocation, owner, "RhpNewFast", method => RefParameter(method, 0));
        HostCheck.True(!Allocates(byReference, "RhpNewFast", "Allocate"), "wrapper ref ABI");
        var byValue = Change(fixedAllocation, "System.Runtime.CompilerServices.Unsafe", "As",
            method => method.WithReturnType(SyntaxFactory.ParseTypeName("TTo").WithTrailingTrivia(SyntaxFactory.Space)));
        HostCheck.True(!Allocates(byValue, "RhpNewFast", "Allocate"), "selected Unsafe.As ABI");
    }

    [HostTest("nostdlib string and default guards reject ref ABI ignored imports and returning fallbacks")]
    private static void StringAndFallbackControls()
    {
        var strings = AllocationProfile("EnableM04SzArrayAllocationProbe", "EnableM04StringAllocationProbe");
        HostCheck.True(StringAllocator(strings));
        HostCheck.True(!StringAllocator(Change(strings, "System.Runtime.RuntimeImports", "RhNewString", method => RefParameter(method, 1))), "import ref ABI");
        HostCheck.True(!StringAllocator(Change(strings, "System.String", "FastAllocateString", method => RefParameter(method, 0))), "caller ref ABI");
        HostCheck.True(!StringAllocator(Body(strings, "System.String", "FastAllocateString",
            "{ Runtime.RuntimeImports.RhNewString(Internal.Runtime.MethodTable.Of<string>(), length); return null; }")), "discarded import result");
        var ordinary = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
        foreach (string body in new[]
        {
            "{ while (false) RuntimeFailFast.Halt(); }",
            "{ while (true) {} }",
            "{ _ = 1; while (true) RuntimeFailFast.Halt(); }",
            "{ return null; }"
        })
        {
            var changed = Body(ordinary, "System.String", "FastAllocateString", body);
            HostCheck.True(!Halts(changed, changed.Method("System.String", "FastAllocateString")), body);
        }
    }

    private static MethodDeclarationSyntax RefParameter(MethodDeclarationSyntax method, int index)
    {
        var parameter = method.ParameterList.Parameters[index];
        return method.ReplaceNode(parameter, parameter.WithModifiers(SyntaxFactory.TokenList(
            SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space))));
    }

    private static CSharpSourceCatalog Body(CSharpSourceCatalog catalog, string owner, string name, string body)
        => Change(catalog, owner, name, method => method.WithBody((BlockSyntax)SyntaxFactory.ParseStatement(body)));

    private static CSharpSourceCatalog Change(CSharpSourceCatalog catalog, string owner, string name,
        Func<MethodDeclarationSyntax, MethodDeclarationSyntax> change)
    {
        var method = catalog.Method(owner, name);
        var document = catalog.Document(method);
        return Rebuild(catalog, document, document.Syntax.GetRoot().ReplaceNode(method, change(method)).ToFullString(), document.Path);
    }

    private static CSharpSourceCatalog Rebuild(CSharpSourceCatalog catalog, CSharpSourceDocument original, string text, string path)
    {
        var changed = CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(text), (CSharpParseOptions)original.Syntax.Options);
        return new(catalog.Documents.Select(document => document == original ? changed : document), catalog.Compilation.References,
            compilationOptions: catalog.Compilation.Options);
    }
}
