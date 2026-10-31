using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;
using System.Runtime.Loader;

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
            var web = EfiOptions.Parse(["--" + flag,"--EnableM10004Probe"],build).Profile();
            HostCheck.True(web.IsEnabled("EnableM10004Probe") && profile.Flags.Where(p => p.Value).All(p => web.IsEnabled(p.Key)));
            var vectors = EfiOptions.Parse(["--" + flag,"--EnableM16013Probe"],build).Profile();
            HostCheck.True(vectors.IsEnabled("EnableM16013Probe") && vectors.IsEnabled("EnableM16012Probe") &&
                profile.Flags.Where(p => p.Value).All(p => vectors.IsEnabled(p.Key)));
            Reject(() => EfiOptions.Parse(["--" + flag,"--EnableM16013Probe","--EnableM16012Probe:false"],build).Profile());
            string[] inherited = EfiProjectAdmission.ExpectedCompileItems(parent), actual = EfiProjectAdmission.ExpectedCompileItems(profile);
            string[] added = [@"..\OS.Kernel\Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs|Compatibility\LinuxX64\LinuxUserInterruptPolicy.cs",
                @"Boot\EfiEntry.LinuxUserInterrupts.cs|"];
            HostCheck.True(actual.Except(inherited, StringComparer.Ordinal).SequenceEqual(added, StringComparer.Ordinal) &&
                actual.Where(item => !added.Contains(item, StringComparer.Ordinal)).SequenceEqual(inherited, StringComparer.Ordinal));
            foreach (string prerequisite in parents) Reject(() => EfiOptions.Parse(["--" + flag,"--" + prerequisite + ":false"],build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(p => p != flag &&
                p is not ("EnableM09011Probe" or "EnableM09012Probe" or "EnableM16010Probe" or "EnableM16011Probe" or "EnableM16012Probe" or "EnableM16013Probe" or "EnableM16014Probe" or "EnableM10004Probe" or "EnableM10017Probe" or "EnableM16025Probe" or "EnableM16027Probe" or "EnableM16027TimingProbe" or "EnableM10018Probe" or "EnableM10018FullProbe" or "EnableM10018TimingProbe" or "EnableM16019UnavailableProbe" or "EnableM16019InvalidProbe" or "EnableM07027Probe" or "EnableM07028Probe") && !parents.Contains(p)))
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

    [HostTest("m16-009 only authenticated common gateway pool admission opts into syscall SS")]
    private static void ActiveGateModes()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        var profile=EfiProfile.Create("Diagnostic",new Dictionary<string,bool>{{"EnableM16009Probe",true}});
        var options=new CSharpParseOptions(preprocessorSymbols:profile.Flags.Where(p=>p.Value).Select(p=>EfiContracts.ProbeSymbols[p.Key]));
        SyntaxNode common=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs")),options).GetRoot();
        SyntaxNode irq=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.LinuxUserInterrupts.cs")),options).GetRoot();
        HostCheck.True(Modes(common,irq));
        var opted=Calls(common,"LinuxNativeUserGates").Single();
        var argument=opted.ArgumentList.Arguments[2];
        HostCheck.True(!Modes(common.ReplaceNode(argument,argument.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))),irq),"common syscall envelope must be selected");
        var handler=irq.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="LinuxNativeUserInterrupt");
        var strict=Calls(handler,"LinuxNativeUserGates").Single();
        HostCheck.True(!Modes(common,irq.ReplaceNode(strict,strict.AddArgumentListArguments(argument))),"direct IRQ must stay strict");
        var authentication=Calls(common,"X64LinuxUserTransition.ValidateCallback").Single();
        HostCheck.True(!Modes(common.ReplaceNode(authentication,SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),irq),"common callback authentication must remain");

        static InvocationExpressionSyntax[] Calls(SyntaxNode node,string name)=>node.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c=>c.Expression.ToString()==name).ToArray();
        static bool Modes(SyntaxNode common,SyntaxNode irq)
        {
            var callers=Calls(common,"LinuxNativeUserGates");
            if(callers.Length!=1||callers[0].ArgumentList.Arguments.Count!=3)return false;
            var call=callers[0];var selected=call.ArgumentList.Arguments[2];
            if(selected.NameColon?.Name.Identifier.Text!="allowSyscall"||!selected.Expression.IsKind(SyntaxKind.TrueLiteralExpression)||
                call.Parent is not ConditionalExpressionSyntax choice||choice.Condition.ToString()!="gatewayCallback"||choice.WhenTrue!=call||
                call.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.Text!="CooperativeAdmitPool")return false;
            var authentication=Calls(common,"X64LinuxUserTransition.ValidateCallback");
            if(authentication.Length!=1||authentication[0].SpanStart>=call.SpanStart||
                authentication[0].Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.Text!="gatewayCallback")return false;
            var methods=irq.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
            var handler=methods.Single(m=>m.Identifier.Text=="LinuxNativeUserInterrupt");
            var direct=Calls(handler,"LinuxNativeUserGates");
            var gates=methods.Single(m=>m.Identifier.Text=="LinuxNativeUserGates");
            var predicate=Calls(gates,"X64LinuxUserTransition.ValidateActiveUserGateObservation");
            return direct.Length==1&&direct[0].ArgumentList.Arguments.Count==2&&
                direct[0].ArgumentList.Arguments[1].Expression.IsKind(SyntaxKind.TrueLiteralExpression)&&
                gates.ParameterList.Parameters.Count==3&&gates.ParameterList.Parameters[2].Default?.Value.IsKind(SyntaxKind.FalseLiteralExpression)==true&&
                predicate.Length==1&&predicate[0].ArgumentList.Arguments.Count==5&&predicate[0].ArgumentList.Arguments[4].Expression.ToString()=="allowSyscall";
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

    [HostTest("m16-009 leaf reuse unchanged and every hardware A-D change")]
    private static void LeafReuseUnchangedAndHardwareBits()
    { using var fixture = CompileLeafReuse(); fixture.Run(0); }

    [HostTest("m16-009 leaf reuse changed live raw values reject or recover like a fresh decode")]
    private static void LeafReuseChangedRaw()
    { using var fixture = CompileLeafReuse(); fixture.Run(1); }

    [HostTest("m16-009 leaf reuse compares all precondition bytes including PAT and reserved state")]
    private static void LeafReuseCompletePreconditions()
    { using var fixture = CompileLeafReuse(); fixture.Run(2); }

    [HostTest("m16-009 leaf reuse preserves the second read before guard evaluation")]
    private static void LeafReuseGuardReadOrder()
    { using var fixture = CompileLeafReuse(); fixture.Run(3); }

    [HostTest("m16-009 leaf reuse preserves walk rejection and the uncaptured FindLeaf caller")]
    private static void LeafReuseWalkRejectionsAndFindLeaf()
    { using var fixture = CompileLeafReuse(); fixture.Run(4); }

    [HostTest("m16-009 leaf reuse capture and fallback remain within the admitted current read")]
    private static void LeafReuseSourceOrder()
    {
        SyntaxNode syntax = LeafReuseSyntax();
        var methods = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var walk = methods.Single(m => m.Identifier.ValueText == "CooperativeWalkPoolLeaf");
        var audit = methods.Single(m => m.Identifier.ValueText == "CooperativeAuditPool");
        var find = methods.Single(m => m.Identifier.ValueText == "CooperativeFindLeaf");
        HostCheck.True(PoolBoundary(syntax));
        HostCheck.Equal(7, walk.ParameterList.Parameters.Count);
        HostCheck.True(walk.ParameterList.Parameters.Skip(5).All(p => p.Default?.Value.IsKind(SyntaxKind.NullLiteralExpression) == true));
        var findWalk = find.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "CooperativeWalkPoolLeaf");
        HostCheck.Equal(5, findWalk.ArgumentList.Arguments.Count);
        var firstRaw = walk.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Initializer?.Value.ToString() == "*slot");
        var firstContext = walk.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Initializer?.Value.ToString() == "memory.Tables.Preconditions");
        var firstDecode = walk.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "X64PageTableEntry.TryDecode");
        HostCheck.True(firstRaw.SpanStart < firstContext.SpanStart && firstContext.SpanStart < firstDecode.SpanStart);
        foreach (string target in new[] { "*leafDecoded", "*leafPreconditions" })
        {
            var store = walk.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(a => a.Left.ToString() == target);
            HostCheck.True(store.Ancestors().OfType<IfStatementSyntax>().Any(i => i.Condition.ToString() == "level == 1"));
        }
        var call = audit.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "CooperativeWalkPoolLeaf");
        var secondRaw = audit.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Initializer?.Value.ToString() == "*leaf");
        var guard = audit.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "CooperativeGuardPage");
        var secondContext = audit.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Initializer?.Value.ToString() == "memory.Tables.Preconditions");
        var fallback = audit.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(c => c.Expression.ToString() == "X64PageTableEntry.TryDecode");
        HostCheck.True(call.SpanStart < secondRaw.SpanStart && secondRaw.SpanStart < guard.SpanStart &&
            guard.SpanStart < secondContext.SpanStart && secondContext.SpanStart < fallback.SpanStart);
    }

    private static SyntaxNode LeafReuseSyntax()
    {
        var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16009Probe"] = true });
        var options = new CSharpParseOptions(LanguageVersion.CSharp14,
            preprocessorSymbols: profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key]));
        return CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.cs")), options).GetRoot();
    }

    private static LeafReuseFixture CompileLeafReuse()
    {
        SyntaxNode syntax = LeafReuseSyntax();
        MethodDeclarationSyntax Method(string name) => syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
        string Text(SyntaxNode node) => node.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString();
        var walk = Method("CooperativeWalkPoolLeaf");
        var audit = Method("CooperativeAuditPool");
        var guard = Method("CooperativeGuardPage");
        HostCheck.Equal(7, walk.ParameterList.Parameters.Count);
        // The oracle is the actual audit with only its reuse decision removed:
        // execute its existing real-decoder assignment on every non-guard read.
        var reuse = audit.DescendantNodes().OfType<IfStatementSyntax>().Single(i =>
            i.Condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(c => c.Expression.ToString() == "CooperativeSameDecodePreconditions"));
        var forced = audit.ReplaceNode(reuse, reuse.Statement).WithIdentifier(SyntaxFactory.Identifier("ForcedDecodeAuditPool"));
        var constants = syntax.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f =>
            f.Declaration.Variables.Any(v => v.Identifier.ValueText is "TablePages" or "CooperativePoolPages")).ToArray();
        HostCheck.Equal(2, constants.Length);
        string source = $$"""
            using System;
            using System.Runtime.InteropServices;
            using OS.Boot;
            using OS.Kernel.Memory;
            using OS.Kernel.Diagnostics;
            using OS.Kernel.Compatibility.LinuxX64;
            using RealDecoder = OS.Kernel.Memory.X64PageTableEntry;
            public static unsafe class LeafReuseProductionFixture
            {
                {{string.Join("\n", constants.Select(Text))}}
                private const ulong Pool = 0x40000000, Present = 1, Writable = 2, Nx = 1UL << 63;
                private struct Storage { public ulong CooperativePool; }
                private static byte* allocation;
                private static ulong* entries;
                private static uint target;
                private static ulong secondRaw, guardRaw, expectedTarget, walkingAddress;
                private static bool mutateGuard, rejectAdmission, sawCaptures;
                private static int targetDecodes, allDecodes;
                private static X64PageTableEncodingPreconditions firstContext, currentContext;
                private static X64PageTableEntryResult targetResult;
                {{Text(walk.WithIdentifier(SyntaxFactory.Identifier("ActualWalkPoolLeaf")))}}
                {{Text(Method("CooperativeFindLeaf"))}}
                {{Text(Method("CooperativeSameDecodePreconditions"))}}
                {{Text(audit)}}
                {{Text(forced)}}
                {{Text(guard.WithIdentifier(SyntaxFactory.Identifier("ActualGuardPage"))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(guard.ExpressionBody!.Expression.WithoutTrivia()))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))}}

                // Only CPU admission and the walk-return test boundary are bound.
                // Every decode below forwards to the real kernel implementation.
                private static bool CooperativeAdmitPool(Storage* state, bool ownedIst, out FirstExitDiagnosticMemory memory, out ulong root)
                {
                    root = (ulong)entries;
                    if (PhysicalAddress.TryCreateFromHardwareRegisterBoundary(root, out var physical) != AddressValueStatus.Success)
                        throw new InvalidOperationException("fixture physical root");
                    memory = new FirstExitDiagnosticMemory { Entries = entries, PageCapacity = TablePages, EntryCapacity = TablePages * 512,
                        Tables = new SeparatePageFaultPageTableRequest(physical, TablePages, currentContext) };
                    return !rejectAdmission;
                }
                private static bool CooperativeWalkPoolLeaf{{walk.ParameterList.ToFullString()}}
                {
                    walkingAddress = address;
                    memory.Tables = new SeparatePageFaultPageTableRequest(memory.Tables.ArenaStart, memory.Tables.ArenaPageCount, firstContext);
                    sawCaptures |= {{walk.ParameterList.Parameters[5].Identifier.ValueText}} != null && {{walk.ParameterList.Parameters[6].Identifier.ValueText}} != null;
                    bool ok = ActualWalkPoolLeaf(memory, pool, root, address, out leaf,
                        {{walk.ParameterList.Parameters[5].Identifier.ValueText}}, {{walk.ParameterList.Parameters[6].Identifier.ValueText}});
                    if (ok && address == Pool + target * Page) { *leaf = secondRaw; expectedTarget = secondRaw; }
                    return ok;
                }
                private static bool CooperativeGuardPage(uint page)
                {
                    if (page == target && mutateGuard) { entries[3 * 512 + page] = guardRaw; expectedTarget = guardRaw; }
                    return ActualGuardPage(page);
                }
                private static class X64PageTableEntry
                {
                    public static X64PageTableEntryResult TryDecode(X64PageTableLevel level, ulong raw, X64PageTableEncodingPreconditions context)
                    {
                        allDecodes++;
                        var result = RealDecoder.TryDecode(level, raw, context);
                        if (level == X64PageTableLevel.Pte && walkingAddress == Pool + target * Page)
                        { targetDecodes++; targetResult = result; }
                        return result;
                    }
                }
                private static void Require(bool condition, string name)
                { if (!condition) throw new InvalidOperationException(name); }
                private static X64PageTableEncodingPreconditions Context(bool oneGiB = true, ulong pat = 0x0006050100070406)
                {
                    Require(X64PatMappingToken.TryCreate(pat, 0, 3, 4, out var token) == X64PageTableEntryStatus.Success, "PAT fixture");
                    return X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, oneGiB, token);
                }
                private static ulong Raw(uint page, bool published) => Pool + page * Page | Present |
                    (!published || page >= CooperativeCodePages ? Writable | Nx : 0);
                private static void Reset(bool published, ulong firstRaw, int damage)
                {
                    for (ulong i = 0; i < TablePages * 512; i++) entries[i] = 0;
                    entries[(Pool >> 39) & 511] = (ulong)(entries + 512) | Present | Writable;
                    entries[512 + ((Pool >> 30) & 511)] = (ulong)(entries + 1024) | Present | Writable;
                    entries[1024 + ((Pool >> 21) & 511)] = (ulong)(entries + 1536) | Present | Writable;
                    for (uint page = 0; page < CooperativePoolPages; page++)
                        entries[1536 + page] = published && ActualGuardPage(page) ? 0 : Raw(page, published);
                    entries[1536 + target] = firstRaw; expectedTarget = firstRaw;
                    if (damage == 1) entries[(Pool >> 39) & 511] = 0;
                    if (damage == 2) entries[(Pool >> 39) & 511] = (ulong)entries | Present | Writable;
                    if (damage == 3) entries[(Pool >> 39) & 511] = (ulong)entries + TablePages * Page | Present | Writable;
                    if (damage == 4) entries[(Pool >> 39) & 511] |= 0x18;
                    if (damage == 5) entries[1024 + ((Pool >> 21) & 511)] = Pool | Present | Writable | 0x80;
                }
                private struct Observation
                { public bool Accepted, Captures; public int TargetCalls, AllCalls; public X64PageTableEntryResult Result; }
                private static Observation Execute(bool forced, bool published, ulong firstRaw, int damage, bool find, ulong query)
                {
                    Reset(published, firstRaw, damage);
                    var saved = new ulong[TablePages * 512];
                    for (ulong i = 0; i < TablePages * 512; i++) saved[i] = entries[i];
                    targetDecodes = allDecodes = 0; targetResult = default; sawCaptures = false;
                    Storage state = new Storage { CooperativePool = Pool };
                    bool result = find ? CooperativeFindLeaf(&state, query, out _) :
                        forced ? ForcedDecodeAuditPool(&state, published) : CooperativeAuditPool(&state, published);
                    for (ulong i = 0; i < TablePages * 512; i++)
                        Require(entries[i] == (i == 1536 + target ? expectedTarget : saved[i]), "no table writes outside fixture mutation");
                    for (ulong i = 0; i < Page; i++)
                        Require(allocation[i] == 0xA5 && allocation[(TablePages + 1) * Page + i] == 0xA5, "table canaries");
                    return new Observation { Accepted = result, Captures = sawCaptures, TargetCalls = targetDecodes, AllCalls = allDecodes, Result = targetResult };
                }
                private static bool SameResult(X64PageTableEntryResult a, X64PageTableEntryResult b)
                {
                    var sa = a.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong pa);
                    var sb = b.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong pb);
                    return a.IsInitialized == b.IsInitialized && a.Status == b.Status && a.Entry.RawValue == b.Entry.RawValue &&
                        a.Level == b.Level && a.Kind == b.Kind && a.Protection == b.Protection && a.CachePolicy == b.CachePolicy &&
                        a.PageSizeBytes == b.PageSizeBytes && a.PatIndex == b.PatIndex && a.Accessed == b.Accessed && a.Dirty == b.Dirty && sa == sb && pa == pb;
                }
                private static void Check(string name, bool published, ulong firstRaw, ulong nextRaw, X64PageTableEncodingPreconditions first,
                    X64PageTableEncodingPreconditions second, bool? expected, int currentCalls, int forcedCalls,
                    int damage = 0, bool find = false, ulong query = Pool, bool changeAtGuard = false, ulong afterGuard = 0, bool deny = false)
                {
                    firstContext = first; currentContext = second; secondRaw = nextRaw; mutateGuard = changeAtGuard; guardRaw = afterGuard; rejectAdmission = deny;
                    var candidate = Execute(false, published, firstRaw, damage, find, query);
                    var oracle = Execute(true, published, firstRaw, damage, find, query);
                    Require(candidate.Accepted == oracle.Accepted && (!expected.HasValue || candidate.Accepted == expected.Value), name + " acceptance");
                    Require(candidate.TargetCalls == currentCalls && oracle.TargetCalls == forcedCalls, name + " real decoder calls");
                    Require(SameResult(candidate.Result, oracle.Result), name + " complete decode result");
                    if (find) Require(!candidate.Captures && !oracle.Captures, name + " original caller omits capture pointers");
                    if (deny) Require(candidate.AllCalls == 0 && oracle.AllCalls == 0, name + " admission before walking");
                }
                public static void Run(int section)
                {
                    Require(CooperativePoolPages > 1 && CooperativePoolPages <= 512, "single-PT fixture extent");
                    allocation = (byte*)NativeMemory.AlignedAlloc((nuint)((TablePages + 2) * Page), (nuint)Page);
                    if (allocation == null) throw new OutOfMemoryException();
                    entries = (ulong*)(allocation + Page);
                    for (ulong i = 0; i < (TablePages + 2) * Page; i++) allocation[i] = 0xA5;
                    try
                    {
                        var valid = Context(); target = 0;
                        if (section == 0)
                        {
                            foreach (bool published in new[] { false, true })
                                foreach (uint page in new[] { 0u, CooperativeCodePages, CooperativePoolPages - 1 })
                                {
                                    target = page; if (published && ActualGuardPage(page)) continue;
                                    ulong raw = Raw(page, published);
                                    Check("unchanged page " + page, published, raw, raw, valid, valid, true, 1, 2);
                                    foreach (ulong bits in new[] { 0x20UL, 0x40UL, 0x60UL })
                                        Check("A/D " + bits, published, raw, raw ^ bits, valid, valid, true, 2, 2);
                                }
                        }
                        if (section == 1)
                        {
                            ulong raw = Raw(0, false);
                            foreach (ulong next in new[] { raw ^ Nx, raw ^ Writable, raw + Page, raw | 0x18, raw | 0x80, raw | (1UL << 52), raw & ~Present, 0UL })
                                Check("changed raw " + next, false, raw, next, valid, valid, false, 2, 2);
                            Check("second protection recovers", false, Raw(0, true), raw, valid, valid, true, 2, 2);
                        }
                        if (section == 2)
                        {
                            Require(sizeof(X64PageTableEncodingPreconditions) == 24, "complete precondition layout");
                            var first = valid;
                            Require(CooperativeSameDecodePreconditions(&first, &valid), "same complete value");
                            for (int i = 0; i < 24; i++)
                            {
                                var changed = valid; ((byte*)&changed)[i] ^= 0x80;
                                Require(!CooperativeSameDecodePreconditions(&first, &changed), "precondition byte " + i);
                                Check("precondition byte " + i, false, Raw(0, false), Raw(0, false), valid, changed, null, 2, 2);
                            }
                            Check("valid1GiB context change", false, Raw(0, false), Raw(0, false), valid, Context(false), true, 2, 2);
                            Check("valid unselected PAT change", false, Raw(0, false), Raw(0, false), valid, Context(true, 0x0606050100070406), true, 2, 2);
                            Check("default second context", false, Raw(0, false), Raw(0, false), valid, default, false, 2, 2);
                            var noNxe = valid; ((byte*)&noNxe)[18] = 0;
                            Check("NXE off", false, Raw(0, false), Raw(0, false), valid, noNxe, false, 2, 2);
                            var noWp = valid; ((byte*)&noWp)[19] = 0;
                            Check("WP off", false, Raw(0, false), Raw(0, false), valid, noWp, false, 2, 2);
                            Require(CooperativeSameDecodePreconditions(&first, &valid), "comparison preserves inputs");
                        }
                        if (section == 3)
                        {
                            while (target < CooperativePoolPages && !ActualGuardPage(target)) target++;
                            Require(target < CooperativePoolPages, "actual guard exists");
                            Check("guard observes second zero", true, 0, 0, valid, valid, true, 1, 1, changeAtGuard: true, afterGuard: Raw(target, false));
                            Check("guard rejects captured nonzero", true, 0, Raw(target, false), valid, valid, false, 1, 1, changeAtGuard: true, afterGuard: 0);
                            target = 0;
                            Check("unmapped non-guard", false, 0, 0, valid, valid, false, 1, 2);
                        }
                        if (section == 4)
                        {
                            ulong raw = Raw(0, false);
                            for (int damage = 1; damage <= 5; damage++)
                                Check("parent rejection " + damage, false, raw, raw, valid, valid, false, 0, 0, damage);
                            foreach (ulong invalid in new[] { raw + Page, raw | 0x18, raw | (1UL << 52), raw & ~Present })
                                Check("first leaf rejects " + invalid, false, invalid, raw, valid, valid, false, 1, 1);
                            Check("FindLeaf unchanged", false, raw, raw, valid, valid, true, 1, 1, find: true);
                            Check("FindLeaf unmapped", false, 0, 0, valid, valid, true, 1, 1, find: true);
                            foreach (ulong address in new[] { Pool - Page, Pool + CooperativePoolPages * Page, Pool + 1 })
                                Check("address rejection " + address, false, raw, raw, valid, valid, false, 0, 0, find: true, query: address);
                            Check("audit admission rejection", false, raw, raw, valid, valid, false, 0, 0, deny: true);
                            Check("FindLeaf admission rejection", false, raw, raw, valid, valid, false, 0, 0, find: true, deny: true);
                        }
                    }
                    finally { NativeMemory.AlignedFree(allocation); allocation = null; entries = null; }
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(OS.Kernel.Memory.X64PageTableEntry).Assembly.Location, typeof(PhysicalAddress).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("LeafReuse" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        HostCheck.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        var context = new AssemblyLoadContext("leaf-reuse-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            Type fixture = context.LoadFromStream(image).GetType("LeafReuseProductionFixture", throwOnError: true)!;
            return new LeafReuseFixture(context, fixture.GetMethod("Run")!.CreateDelegate<Action<int>>());
        }
        catch { context.Unload(); throw; }
    }

    private sealed class LeafReuseFixture(AssemblyLoadContext context, Action<int> run) : IDisposable
    {
        internal void Run(int section) => run(section);
        public void Dispose() => context.Unload();
    }

    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
