using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M16009EfiIntegrationTests
{
    [HostTest("m16-009 default-off signal leaf inherits all real thread prerequisites and excludes unrelated probes")]
    private static void Profile()
    {
        const string flag = "EnableM16009Probe";
        HostCheck.True(!Probes.EnableM16009Probe && !Probes.GetM16009Probe().EnabledByDefault && !BootSequence.RunOwnedLinuxSignals(true,true,true,true,null,null));
        var parent = EfiProfile.Create("Diagnostic",new Dictionary<string,bool> { ["EnableM16008Probe"] = true });
        string[] parents = parent.Flags.Where(p => p.Value).Select(p => p.Key).ToArray();
        foreach (bool build in new[] {false,true})
        {
            HostCheck.True(!EfiOptions.Parse([],build).Profile().IsEnabled(flag));
            var profile = EfiOptions.Parse(["--" + flag],build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled(flag) && parents.All(profile.IsEnabled));
            HostCheck.Equal(parents.Length + 1,profile.Flags.Count(p => p.Value));
            string[] inherited = EfiProjectAdmission.ExpectedCompileItems(parent), actual = EfiProjectAdmission.ExpectedCompileItems(profile);
            string[] added = [@"..\OS.Kernel\Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs|Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs",
                @"Boot\EfiEntry.LinuxUserInterrupts.cs|"];
            HostCheck.True(actual.Except(inherited, StringComparer.Ordinal).SequenceEqual(added, StringComparer.Ordinal) &&
                actual.Where(item => !added.Contains(item, StringComparer.Ordinal)).SequenceEqual(inherited, StringComparer.Ordinal));
            foreach (string prerequisite in parents) Reject(() => EfiOptions.Parse(["--" + flag,"--" + prerequisite + ":false"],build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(p => p != flag && !parents.Contains(p)))
                Reject(() => EfiOptions.Parse(["--" + flag,"--" + conflict],build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy","VerifiedExit","--" + flag],build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun","--BuildEfi","--EnableM16009Probe"]));
        HostCheck.True(plan.Required.Contains("CLROS_M16_009_READY") && plan.Required.Contains("M16_009_RESTORE") &&
            plan.Required.Contains("M16_008_REAP") && !plan.Required.Contains("CLROS_M16_008_READY") && !plan.Required.Contains("M16_005_WRITE"));
        HostCheck.True(!plan.Ordered.Contains("M16_008_CALL") && !plan.Unique.Contains("M16_008_CALL"));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun","--BuildEfi","--EnableM16009Probe","--RequireMarker","CLROS_M16_008_READY"])));
        Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript("missing",linuxSignals:true));
    }

    [HostTest("m16-009 native leaf follows APIC and composes original parent validators without running the ancestor leaf")]
    private static void BootAttachment()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var profile = EfiProfile.Create("Diagnostic",new Dictionary<string,bool> { ["EnableM16009Probe"] = true });
        string[] symbols = profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key]).ToArray();
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs")),new CSharpParseOptions(preprocessorSymbols:symbols));
        HostCheck.True(!tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        var calls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var leaf = calls.Single(c => c.Expression.ToString() == "BootSequence.RunOwnedLinuxSignals");
        HostCheck.True(!calls.Any(c => c.Expression.ToString() is "BootSequence.RunOwnedLinuxThreads" or "BootSequence.RunOwnedLinuxDescriptors"));
        var apic = calls.Single(c => c.Expression.ToString() == "BootSequence.RunOwnedApicSetup");
        HostCheck.True(apic.SpanStart < leaf.SpanStart && apic.Ancestors().OfType<IfStatementSyntax>().First().Statement is ReturnStatementSyntax);
        string[] text = tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => n.IsKind(SyntaxKind.StringLiteralExpression)).Select(n => n.Token.ValueText).ToArray();
        HostCheck.True(text.Contains("CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n") && !text.Contains("CLROS_M16_008_READY\r\nTEST_PASS m16-008\r\n"));
        var validator = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"tools/OS.Tooling/QemuTranscripts.M03.cs"))).GetRoot();
        foreach (string required in new[] {"ValidateLinuxSignalTail","ValidateServiceLoopTail","ValidateLinuxStartupTail","ValidateLinuxElfTail"})
            HostCheck.True(validator.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c => c.Expression.ToString() == required));
    }

    [HostTest("m16-009 runner rejects foreign packages before build and keeps the canonical input read lock")]
    private static void Preflight()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Directory.CreateTempSubdirectory("clros-signal-runner-").FullName;
        string path = Path.Combine(directory,"input.pkg");
        try
        {
            int calls = 0; bool locked = false;
            var options = QemuSmokeOptions.Parse(["--BuildEfi","--EnableM16009Probe","--LinuxElfPackage",path]);
            foreach (byte[] invalid in new[] {LinuxThreadProofFixture.CreatePackage("Threads"),LinuxSignalProofFixture.CreatePackage("AlternateStack")[..^1]})
            {
                File.WriteAllBytes(path,invalid); using StringWriter output = new();
                int result = QemuSmoke.RunAsync(root,options,output,(_,_) => { calls++; return Task.FromResult(new ProcessResult(1,"","",false,false,false,TimeSpan.Zero)); }).GetAwaiter().GetResult();
                HostCheck.True(result == 1 && calls == 0 && output.ToString().Contains("m16_009_package_identity",StringComparison.Ordinal));
            }
            foreach (string variant in new[] {"AlternateStack","FaultStack"})
            {
                File.WriteAllBytes(path,LinuxSignalProofFixture.CreatePackage(variant));
                int before = calls; locked = false;
                int stopped = QemuSmoke.RunAsync(root,options,TextWriter.Null,(_,_) =>
                {
                    calls++;
                    try { using FileStream write = new(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite); }
                    catch (IOException) { locked = true; }
                    return Task.FromResult(new ProcessResult(1,"TEST_FAIL controlled-build-stop\n","",false,false,false,TimeSpan.Zero));
                }).GetAwaiter().GetResult();
                HostCheck.True(stopped == 1 && calls == before + 1 && locked);
            }
            using FileStream released = new(path,FileMode.Open,FileAccess.Write,FileShare.None);
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
