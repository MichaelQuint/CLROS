using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;
using System.Text;

internal static partial class M02OwnedCom1ReadyEfiIntegrationTests
{
    [HostTest("m02.005 panic construction discovery follows partial files and retains rejection controls")]
    private static void OwnedCom1ConstructionControls()
    {
        const string prefix = "namespace OS.Boot { public static partial class EfiEntry {\n";
        const string suffix = "\n} }";
        const string declaration = "private static partial class M02UnexpectedVectorPanicAdapter {\n";
        const string fields = "private struct Authority { public OS.Kernel.Diagnostics.Com1OwnedPortIoBackend Backend; } private static Authority s_authority;\n";
        const string body = "private static void Run() { var binding = default(BindingState); " + PanicBinding + " }\n";
        const string common = "namespace OS.Kernel.Diagnostics { public struct Com1OwnedPortIoBackend { public Com1OwnedPortIoBackend(int value) { } } }\n"
            + prefix + "private struct BindingState { public int Binding; } private static void Ordinary() { "
            + BackendConstruction + "0); " + BackendConstruction + "1); " + BackendConstruction + "2); }" + suffix;
        string Part(string members, string guard = PanicSymbol) => prefix + "#if " + guard + "\n" + declaration + members + "}\n#endif" + suffix;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: [PanicSymbol]);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        Check([common, Part(fields + body)], true);
        Check([common, Part(fields), Part(body)], true);
        Check([common, Part(fields + body, "UNKNOWN_PANIC_PROFILE")], false);
        Check([common, Part(fields + body.Replace(PanicBinding, "s_authority.Backend = default;", StringComparison.Ordinal))], false);
        Check([common, Part(fields + body.Replace("Com1OwnedPortIoBackend(binding.Binding)", "Com1OwnedPortIoBackend(default)", StringComparison.Ordinal))], false);
        Check([common, Part(fields + body), prefix + "private static void Extra() { " + "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(default);" + " }" + suffix], false);
        Check([common, Part(fields + "#if " + PanicSymbol + "\n" + body + "#endif\n")], false);
        Check([common, Part(fields + body) + "\n" + Part("private static void Hidden() {}", "OTHER_PROFILE")], false);
        Check([common, Part(fields + body).Replace("private static partial class", "private partial class", StringComparison.Ordinal)], false);
        Check([common, Part(fields + body), Part("private static void Foreign() {}", "OTHER_PROFILE")
            .Replace("namespace OS.Boot", "namespace Other", StringComparison.Ordinal)], false);

        void Check(string[] texts, bool expected)
        {
            var source = new CSharpSourceCatalog(texts.Select((text, index) => CSharpSourceDocument.FromBytes(
                "arbitrary-" + index + ".cs", Encoding.UTF8.GetBytes(text), parse)), references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "panic control binds");
            bool accepted;
            try
            {
                accepted = HasExpectedBackendConstructions(source);
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "panic construction scope and original rejection policy");
        }
    }

    [HostTest("m02.005 evidence caller discovery follows partial files and rejects wrong owners and signatures")]
    private static void OwnedCom1CallerControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { private partial class M01StageThreeAdapter { ";
        const string suffix = " } }";
        const string marker = "private static void WriteM02OwnedCom1ReadySuccessLine() { }";
        const string call = "WriteM02OwnedCom1ReadySuccessLine();";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (caller, expected) in new[]
        {
            ("private static void Renamed() { " + call + " }", true),
            ("private static void Renamed<T>() { " + call + " }", false),
            ("public static void Renamed() { " + call + " }", false),
            ("private void Renamed() { " + call + " }", false),
            ("private static void Renamed() { " + call + call + " }", false),
            ("private static class Foreign { private static void Renamed() { " + call + " } }", false)
        })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("formatter.cs", Encoding.UTF8.GetBytes(prefix + marker + suffix), parse),
                CSharpSourceDocument.FromBytes("moved-caller.cs", Encoding.UTF8.GetBytes(prefix + caller + suffix), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "COM1 caller control binds");
            bool accepted;
            try
            {
                _ = OwnedCom1Caller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "COM1 caller ownership and uniqueness");
        }
    }
}
