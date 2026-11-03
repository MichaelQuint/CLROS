using static HostRuntimeAllocationBoundary;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static partial class NoStdLibShimBoundaryTests
{
    [HostTest("nostdlib shim boundary includes explicit EFI project", HostLane.Fast, 0)]
    private static bool TestEfiProjectIncludedExplicitly()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(root, HostToolProcess.RepositorySolutionFileName));
        return HostCheck.Result(solution.Descendants("Project").Any(project =>
            ((string?)project.Attribute("Path"))?.Replace('\\', '/') == "src/OS/OS.csproj"));
    }

    [HostTest("nostdlib shim boundary is explicit in EFI project", HostLane.Fast, 1)]
    private static bool TestEfiProjectExplicitShimBoundary()
    {
        var ordinary = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
        HostCheck.Equal(0, ordinary.Compilation.References.Count());
        HostCheck.Equal(Platform.X64, ordinary.Compilation.Options.Platform);
        HostCheck.True(SymbolEqualityComparer.Default.Equals(ordinary.Type("System.Object").ContainingAssembly, ordinary.Compilation.Assembly));
        var exports = HostRuntimeExports.Read(ordinary);
        foreach (string name in new[] { "RhpNewFast", "RhpNewArrayFast", "RhpNewPtrArrayFast", "RhNewString" })
        {
            var symbol = exports[name];
            HostCheck.Equal("System.Runtime.RhpNewFastRuntime", symbol.ContainingType.ToDisplayString());
            HostCheck.True(WrapperSignature(ordinary, symbol, name), name + ":wrapper ABI");
            HostCheck.True(Halts(ordinary, ordinary.Method("System.Runtime.RhpNewFastRuntime", symbol.Name)), name);
        }
        HostCheck.True(Halts(ordinary, ordinary.Method("System.String", "FastAllocateString")));

        var array = ordinary.Type("System.Array");
        var length = array.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).ToArray();
        HostCheck.True(length is [{ Name: "_numComponents", Type.SpecialType: SpecialType.System_Int32 }]);
        HostCheck.True(ReadField(ordinary, array, "Length", length[0]));
        var genericArray = ordinary.Type("System.Array`1");
        HostCheck.True(SymbolEqualityComparer.Default.Equals(genericArray.BaseType, array)
            && genericArray.InstanceConstructors is [{ DeclaredAccessibility: Accessibility.Private, Parameters.Length: 0 }]);
        var text = ordinary.Type("System.String");
        var header = text.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).ToArray();
        HostCheck.True(header is [{ Name: "_stringLength", IsReadOnly: true, Type.SpecialType: SpecialType.System_Int32 },
            { Name: "_firstChar", Type.SpecialType: SpecialType.System_Char }]);
        HostCheck.True(ReadField(ordinary, text, "Length", header[0]));
        HostCheck.True(Reinterprets(ordinary));
        var methodTable = ordinary.Type("Internal.Runtime.MethodTable");
        var tableOf = methodTable.GetMembers("Of").OfType<IMethodSymbol>().Single();
        HostCheck.True(tableOf.IsExtern && tableOf.IsStatic && tableOf.TypeParameters.Length == 1
            && tableOf.Parameters.Length == 0 && tableOf.ReturnType is IPointerTypeSymbol pointer
            && SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, methodTable));

        var fixedAllocation = AllocationProfile();
        var arrayAllocation = AllocationProfile("EnableM04SzArrayAllocationProbe");
        var stringAllocation = AllocationProfile("EnableM04SzArrayAllocationProbe", "EnableM04StringAllocationProbe");
        HostCheck.True(Allocates(fixedAllocation, "RhpNewFast", "Allocate"));
        HostCheck.True(Allocates(arrayAllocation, "RhpNewArrayFast", "AllocateArray", pointerArray: false));
        HostCheck.True(Allocates(arrayAllocation, "RhpNewPtrArrayFast", "AllocateArray", pointerArray: true));
        HostCheck.True(Allocates(stringAllocation, "RhNewString", "AllocateString"));
        HostCheck.True(ordinary.Compilation.GetTypeByMetadataName("System.Runtime.RuntimeImports") is null);
        HostCheck.True(StringAllocator(stringAllocation));
        return HostCheck.Result(true);
    }

    private static bool ReadField(CSharpSourceCatalog catalog, INamedTypeSymbol owner, string name, IFieldSymbol field)
    {
        var property = owner.GetMembers(name).OfType<IPropertySymbol>().Single();
        var declaration = (PropertyDeclarationSyntax)property.DeclaringSyntaxReferences.Single().GetSyntax();
        var getter = declaration.ExpressionBody?.Expression ?? declaration.AccessorList?.Accessors.Single().ExpressionBody?.Expression;
        return getter is not null && catalog.Compilation.GetSemanticModel(getter.SyntaxTree).GetOperation(getter) is IFieldReferenceOperation reference
            && SymbolEqualityComparer.Default.Equals(reference.Field, field);
    }

    [HostTest("nostdlib shim boundary keeps System stubs out of host projects", HostLane.Fast, 2)]
    private static bool TestHostProjectsDoNotCompileMinimalRuntime()
    {
        foreach (var assembly in new[] { typeof(OS.Boot.BootSequence).Assembly, typeof(OS.Std.Memory.BufferOperations).Assembly })
            foreach (string name in new[] { "System.Object", "System.Array", "System.String", "System.Runtime.RuntimeExportAttribute" })
                HostCheck.True(assembly.GetType(name) is null, assembly.GetName().Name + ":" + name);
        return HostCheck.Result(true);
    }
}
