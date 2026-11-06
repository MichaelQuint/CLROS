using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;
using System.Text;

internal static partial class M02PageFaultCr2ErrorCodeEfiIntegrationTests
{
    [HostTest("m02.014.efi bound source owners survive moves and reject alternate authority")]
    private static void PageFaultDiscoveryControls()
    {
        const string adapter = "namespace OS.Boot; public static partial class EfiEntry { private static partial class M02PageFaultAdapter { internal static void Run() { } private static void FailBeforeEntry() { } } }";
        const string caller = "namespace OS.Boot; public static partial class EfiEntry { private static partial class M01StageOneAdapter { private static void Continue() { M02PageFaultAdapter.Run(); } } }";
        const string authority = "namespace OS.Kernel.Diagnostics; public static partial class Probes { public const bool EnableNullPointerPanicProbe = false; }";
        var source = PageFaultControlCatalog(("moved-adapter.cs", adapter), ("arbitrary-caller.cs", caller), ("another-authority.cs", authority));
        Require(PageFaultRun(source).Identifier.ValueText == "Run" && PageFaultCaller(source).Identifier.ValueText == "Continue",
            "logical source identity follows moved declarations");
        Require(HostSourceText.Contains(PageFaultAuthority(source), "public const bool EnableNullPointerPanicProbe = false;"),
            "constant authority follows its partial owner");
        string host = HostEfiAdapterSources.ForHost(source, AdapterOwner, "FailBeforeEntry");
        Require(HostSourceText.Contains(host, "internal static void FailBeforeEntry()"), "only requested host seam exposed");
        PageFaultReject(() => PageFaultCaller(PageFaultControlCatalog(("adapter.cs", adapter),
            ("caller.cs", caller.Replace("M01StageOneAdapter", "ForeignOwner", StringComparison.Ordinal)))));
        PageFaultReject(() => PageFaultCaller(PageFaultControlCatalog(("adapter.cs", adapter),
            ("caller.cs", caller.Replace("private static void Continue", "internal static void Continue", StringComparison.Ordinal)))));
        PageFaultReject(() => PageFaultCaller(PageFaultControlCatalog(("adapter.cs", adapter),
            ("caller.cs", caller.Replace("M02PageFaultAdapter.Run();", "M02PageFaultAdapter.Run(); M02PageFaultAdapter.Run();", StringComparison.Ordinal)))));
        PageFaultReject(() => PageFaultAuthority(PageFaultControlCatalog(("authority.cs", authority.Replace("= false", "= true", StringComparison.Ordinal)))));
        PageFaultReject(() => PageFaultAuthority(PageFaultControlCatalog(("authority.cs", authority),
            ("inactive.cs", "#if INACTIVE\n" + authority + "\n#endif"))));
        var wrongReturn = PageFaultControlCatalog(("adapter.cs", adapter.Replace("internal static void Run() { }",
            "internal static int Run() { return 0; }", StringComparison.Ordinal)));
        Require(!wrongReturn.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "wrong-return negative control binds before selector rejection");
        PageFaultReject(() => PageFaultRun(wrongReturn));

        var actual = PageFaultSources();
        var original = HostSourceText.DocumentSources(actual, PageTableSourceOwners().Select(owner => owner.FullName!).ToArray()).First();
        var changed = new CSharpSourceCatalog(actual.Documents.Select(document => document.Path == original.Path
                ? CSharpSourceDocument.FromBytes(document.Path, Encoding.UTF8.GetBytes(document.Text + "\n// changed"),
                    (CSharpParseOptions)document.Syntax.Options) : document),
            actual.Compilation.References, actual.Compilation.AssemblyName!, actual.Compilation.Options);
        PageFaultReject(() => PageFaultPinnedFamily(changed, PageTableSourceOwners()));
    }

    private static CSharpSourceCatalog PageFaultControlCatalog(params (string Path, string Text)[] files) =>
        new(files.Select(file => CSharpSourceDocument.FromBytes(file.Path, Encoding.UTF8.GetBytes(file.Text),
                new CSharpParseOptions(LanguageVersion.CSharp14))),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static void PageFaultReject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("invalid page-fault source selection accepted");
    }
}
