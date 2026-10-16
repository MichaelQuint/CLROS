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
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(p => p != flag &&
                p is not ("EnableM09011Probe" or "EnableM09012Probe" or "EnableM16010Probe" or "EnableM16011Probe" or "EnableM16012Probe") && !parents.Contains(p)))
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

    [HostTest("m16-009 pool audit authenticates each call once before all leaf walks and rejects admission bypass")]
    private static void PoolAuditBoundary()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.cs"));
        HostCheck.Equal(0UL, OS.Kernel.Threading.ThreadContextSwitch.AdmittedFlags & 0x200UL);
        foreach (string flag in new[] { "EnableM09004Probe", "EnableM16008Probe", "EnableM16009Probe" })
        {
            var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { [flag] = true });
            string[] symbols = profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key]).ToArray();
            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols));
            HostCheck.True(!tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error));
            var syntax = tree.GetRoot();
            HostCheck.True(PoolBoundary(syntax), flag);
            foreach (string caller in new[] { "CooperativeFindLeaf", "CooperativeAuditPool" })
            {
                var method = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == caller);
                var admission = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "CooperativeAdmitPool");
                var guard = admission.Ancestors().OfType<IfStatementSyntax>().First();
                HostCheck.True(!PoolBoundary(syntax.RemoveNode(guard, SyntaxRemoveOptions.KeepNoTrivia)!), caller + " must reject missing admission");
            }
            var common = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "CooperativeAdmitPool");
            var names = common.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(c => c.Expression.ToString()).ToArray();
            HostCheck.Equal(1, names.Count(n => n == "FirstExitCodeBundle.ValidateInstalledReadBack"));
            if (flag == "EnableM16009Probe")
                foreach (string required in new[] { "LinuxNativeOwner", "LinuxNativeUserWorker", "LinuxNativeUserGates", "LinuxNativeTimerInstalled" })
                    HostCheck.True(names.Contains(required), required);
        }
    }

    // Native CPU observations cannot run in a host fixture. Check that their
    // rejecting gate dominates both private walk callers; removed gates are
    // independent negatives. Actual code/descriptor/PTE rejection remains in
    // the native owner's existing host contracts and guest qualification.
    private static bool PoolBoundary(SyntaxNode syntax)
    {
        var methods = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        if (syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(c => c.Expression.ToString() == "CooperativeWalkPoolLeaf") != 2) return false;
        foreach (string name in new[] { "CooperativeFindLeaf", "CooperativeAuditPool" })
        {
            var method = methods.Single(m => m.Identifier.Text == name);
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            var admission = calls.Where(c => c.Expression.ToString() == "CooperativeAdmitPool").ToArray();
            var walks = calls.Where(c => c.Expression.ToString() == "CooperativeWalkPoolLeaf").ToArray();
            if (admission.Length != 1 || walks.Length != 1 ||
                admission[0].Parent is not PrefixUnaryExpressionSyntax unary || !unary.IsKind(SyntaxKind.LogicalNotExpression) ||
                unary.Parent is not IfStatementSyntax gate || gate.Condition != unary || gate.Parent != method.Body ||
                gate.Statement is not ReturnStatementSyntax rejected || rejected.Expression?.IsKind(SyntaxKind.FalseLiteralExpression) != true ||
                gate.Span.End > walks[0].SpanStart) return false;
            if (name == "CooperativeFindLeaf")
            {
                if (walks[0].Parent is not ReturnStatementSyntax) return false;
            }
            else
            {
                var loops = method.DescendantNodes().OfType<ForStatementSyntax>().ToArray();
                if (loops.Length != 1 || loops[0].Parent != method.Body || !walks[0].Ancestors().Contains(loops[0]) ||
                    !loops[0].DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c => c.Expression.ToString() == "CooperativeGuardPage") ||
                    !loops[0].DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c => c.Expression.ToString() == "X64PageTableEntry.TryDecode")) return false;
            }
        }
        var walk = methods.Single(m => m.Identifier.Text == "CooperativeWalkPoolLeaf");
        return walk.DescendantNodes().OfType<InvocationExpressionSyntax>().All(c =>
            c.Expression.ToString() is "X64PageTableEntry.TryDecode" or "decoded.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary");
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
            foreach (string variant in new[] {"AlternateStack","FaultStack","Activation"})
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
