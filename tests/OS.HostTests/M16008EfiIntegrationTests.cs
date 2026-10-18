using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Tooling;

internal static unsafe class M16008EfiIntegrationTests
{
    [HostTest("m16 thread and signal marker vectors survive runner normalization without duplicate requirements")]
    private static void RunnerMarkerBoundary()
    {
        foreach (string flag in new[] { "EnableM16008Probe", "EnableM16009Probe" })
        {
            var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--" + flag]));
            // The CLI adds ordered/unique entries to required with exact-string
            // deduplication; the streaming parser then trims every entry.
            string[] required = plan.Required.Concat(plan.Ordered).Concat(plan.Unique).Distinct(StringComparer.Ordinal).ToArray();
            _ = new OS.Tools.QemuRunner.SerialMarkerStateMachine(required, plan.Forbidden,
                orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
            HostCheck.True(required.All(marker => marker == marker.Trim()));
            foreach (string marker in new[] { "M16_008_CALL", "M16_008_SUPERVISOR", "M16_008_EXIT", "M16_008_REAP" })
                HostCheck.True(plan.Required.Contains(marker) && !plan.Ordered.Contains(marker) && !plan.Unique.Contains(marker));
        }
    }

    [HostTest("m16 signal activation lifecycle passes streaming markers before exact transcript validation")]
    private static void ActivationStreamingMarkers()
    {
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--EnableM16009Probe"]));
        string[] Tail(IReadOnlyList<string> markers) => markers.Where(QemuTranscripts.M16LinuxSignalMarkers.Contains).ToArray();
        foreach (bool early in new[] { false, true })
        {
            string[] rows = LinuxSignalIrqTranscriptTests.Synthetic(early);
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(Tail(plan.Required), plan.Forbidden,
                successTerminalMarker: "TEST_PASS m16-009", orderedMarkers: Tail(plan.Ordered), uniqueMarkers: Tail(plan.Unique));
            state.Feed(System.Text.Encoding.ASCII.GetBytes(string.Join('\n', rows) + "\n"));
            var result = state.Complete();
            HostCheck.True(result.Passed, "streaming status=" + result.Status);
            QemuTranscripts.ValidateLinuxSignalTail(rows, LinuxSignalProofFixture.CreatePackage("Activation"), 0x200000, 0x100000);
        }
    }

    [HostTest("m16.008 default off source profile requires exact descriptor clock and APIC parents")]
    private static void Profile()
    {
        const string flag = "EnableM16008Probe";
        string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableAcpiPhysicalDiscoveryProbe",
            "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe", "EnableM16002Probe",
            "EnableM16003Probe", "EnableM16004Probe", "EnableM16005Probe", "EnableM16006Probe", "EnableM16007Probe",
            "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
        HostCheck.True(!Probes.EnableM16008Probe && !Probes.GetM16008Probe().EnabledByDefault);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(!EfiOptions.Parse([], build).Profile().IsEnabled(flag));
            var profile = EfiOptions.Parse(["--" + flag], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled(flag) && parents.All(profile.IsEnabled));
            HostCheck.Equal(parents.Length + 1, profile.Flags.Count(p => p.Value));
            var web = EfiOptions.Parse(["--" + flag, "--EnableM10004Probe"], build).Profile();
            HostCheck.True(web.IsEnabled("EnableM10004Probe") && profile.Flags.Where(p => p.Value).All(p => web.IsEnabled(p.Key)));
            var vectors = EfiOptions.Parse(["--" + flag, "--EnableM16013Probe"], build).Profile();
            HostCheck.True(vectors.IsEnabled("EnableM16013Probe") && vectors.IsEnabled("EnableM16012Probe") &&
                profile.Flags.Where(p => p.Value).All(p => vectors.IsEnabled(p.Key)));
            Reject(() => EfiOptions.Parse(["--" + flag, "--EnableM16013Probe", "--EnableM16012Probe:false"], build).Profile(),
                "m16_013_explicit_false_prerequisite");
            foreach (string parent in parents) Reject(() => EfiOptions.Parse(["--" + flag, "--" + parent + ":false"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(n => n != flag &&
                n is not ("EnableM16009Probe" or "EnableM09011Probe" or "EnableM09012Probe" or "EnableM16010Probe" or "EnableM16011Probe" or "EnableM16012Probe" or "EnableM16013Probe" or "EnableM10004Probe" or "EnableM16019UnavailableProbe" or "EnableM16019InvalidProbe" or "EnableM07027Probe" or "EnableM07028Probe") && !parents.Contains(n)))
                Reject(() => EfiOptions.Parse(["--" + flag, "--" + conflict], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--" + flag], build).Profile());
            string[] compile = EfiProjectAdmission.ExpectedCompileItems(profile);
            foreach (string include in new[] { @"..\OS.Kernel\Threading\WaitAddress.cs|Threading\WaitAddress.cs",
                @"Boot\EfiEntry.LinuxThreads.cs|", @"Boot\EfiEntry.LinuxSignals.cs|", @"Boot\EfiEntry.LinuxThreadMemory.cs|",
                @"Boot\EfiEntry.LinuxThreadRun.cs|", @"Boot\EfiEntry.LinuxThreadTimer.cs|",
                @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadProgram.cs|Compatibility\LinuxX64\LinuxThreadProgram.cs",
                @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs|Compatibility\LinuxX64\LinuxThreadServices.Pipes.cs" })
                HostCheck.True(compile.Contains(include));
            HostCheck.True(!compile.Contains(@"Boot\EfiEntry.KernelThread.cs|") && !compile.Contains(@"Boot\EfiEntry.ThreadWait.cs|"));
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--EnableM16008Probe"]));
        HostCheck.True(plan.Required.Contains("CLROS_M16_008_READY") && plan.Required.Contains("M16_008_RESTORE") &&
            plan.Required.Contains("M16_008_RESUME") && plan.Required.Contains("M05_APIC 3") &&
            !plan.Required.Contains("CLROS_M16_007_READY") && !plan.Unique.Contains("M16_008_CALL"));
        foreach (string repeated in new[] { "M16_008_CALL", "M16_008_RESUME", "M16_008_SUPERVISOR", "M16_008_EXIT", "M16_008_REAP", "M16_008_TICK" })
            HostCheck.True(!plan.Ordered.Contains(repeated) && !plan.Unique.Contains(repeated));
        HostCheck.True(!BootSequence.RunOwnedLinuxThreads(true, true, true, true, null, null));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--EnableM16008Probe", "--RequireMarker", "CLROS_M16_007_READY"])));
        Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript("missing", linuxThreads: true), "m16_008_profile_invalid");
    }

    [HostTest("m16-008 native owner follows the successful APIC gate and selects explicit user resumption")]
    private static void BootAttachment()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string flag in new[] { "EnableM16008Probe", "EnableM16007Probe" })
        {
            var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { [flag] = true });
            string[] symbols = profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key]).ToArray();
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")), new CSharpParseOptions(preprocessorSymbols: symbols));
            HostCheck.True(!tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            var calls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            var thread = calls.Where(c => c.Expression.ToString() == "BootSequence.RunOwnedLinuxThreads").ToArray();
            var descriptors = calls.Where(c => c.Expression.ToString() == "BootSequence.RunOwnedLinuxDescriptors").ToArray();
            if (flag == "EnableM16007Probe") { HostCheck.True(thread.Length == 0 && descriptors.Length == 1); continue; }
            HostCheck.True(thread.Length == 1 && descriptors.Length == 0);
            var apic = calls.Single(c => c.Expression.ToString() == "BootSequence.RunOwnedApicSetup");
            var gate = apic.Ancestors().OfType<IfStatementSyntax>().First();
            HostCheck.True(gate.Statement is ReturnStatementSyntax && gate.Parent == thread[0].Ancestors().OfType<IfStatementSyntax>().First().Parent && apic.SpanStart < thread[0].SpanStart);
            var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            HostCheck.True(methods.Single(m => m.Identifier.Text == "LinuxUserTransitionPlan").DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left.ToString() == "ResumeUser" && a.Right.ToString() == "true"));
            HostCheck.True(methods.Single(m => m.Identifier.Text == "RunOwnedLinuxSyscalls").DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Count(c => c.Expression.ToString() == "RunLinuxNativeOwner") == 1);
        }
    }

    [HostTest("m16.008 combined source layout keeps clock PCI APIC and context helper bytes disjoint")]
    private static void Layout()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var profile = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16008Probe"] = true });
        string[] symbols = profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key]).ToArray();
        var bundle = Parse("src/OS.Kernel/Boot/FirstOptRealExitBootServicesTransition.cs");
        uint Offset(string name) => checked((uint)(int)((LiteralExpressionSyntax)bundle.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(n => n.Identifier.Text == "FirstExitCodeBundle").Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(n => n.Declaration.Variables).Single(n => n.Identifier.Text == name).Initializer!.Value).Token.Value!);
        (uint Start, uint Bytes)[] bodies = [(Offset("ClockAccessOffset"), X64OwnedClockAccess.Bytes),
            (Offset("LinuxReadBackOffset"), X64LinuxElfReadBackBridge.Bytes),
            (Offset("PciReadOffset"), X64PciConfigurationRead.Bytes), (Offset("PciBarOffset"), X64PciBarAccess.Bytes),
            (Offset("ApicObservationOffset"), X64ApicObservation.Bytes),
            (Offset("CooperativeObserveOffset"), OS.Kernel.Threading.ThreadContextSwitch.ObserverBytes),
            (Offset("CooperativeFxOffset"), 16), (Offset("CooperativeSegmentsOffset"), 96),
            (Offset("CooperativePublishOffset"), 1472)];
        for (int i = 0; i < bodies.Length; i++)
        {
            HostCheck.True(bodies[i].Start >= 4096 && bodies[i].Start + bodies[i].Bytes <= Offset("StackEntryOffset"));
            for (int j = 0; j < i; j++) HostCheck.True(bodies[i].Start >= bodies[j].Start + bodies[j].Bytes ||
                bodies[j].Start >= bodies[i].Start + bodies[i].Bytes);
        }
        var retained = bundle.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(n => n.Identifier.Text == "FirstExitRetainedMappings");
        ExpressionSyntax Capacity(string name) => retained.Members.OfType<FieldDeclarationSyntax>().SelectMany(n => n.Declaration.Variables)
            .Single(n => n.Identifier.Text == name).Initializer!.Value;
        HostCheck.Equal(19UL, Sum(Capacity("MaximumAllocations")));
        HostCheck.Equal(FirstExitImageMappings.MaximumSections + FirstExitOwnedArena.MappingRangeCount + 19UL, Sum(Capacity("MaximumRanges")));
        HostCheck.True(Sum(Capacity("MaximumRanges")) <= SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges,
            "the combined native profile must fit image/table-builder retained-range admission");
        var ecam = retained.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Single(n => n.Right.ToString() == "references.EcamStart");
        HostCheck.True(ecam.Left.ToString() == "starts[allocations]");
        var entry = Parse("src/OS/Boot/EfiEntry.cs");
        var fields = entry.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(n => n.Declaration.Variables);
        HostCheck.Equal("LinuxThreadProgram.PoolPages", fields.Single(n => n.Identifier.Text == "CooperativePoolPages").Initializer!.Value.ToString());
        HostCheck.Equal("LinuxThreadProgram.CodePages", fields.Single(n => n.Identifier.Text == "CooperativeCodePages").Initializer!.Value.ToString());
        HostCheck.Equal(17, (int)((LiteralExpressionSyntax)fields.Single(n => n.Identifier.Text == "LinuxLedgerCapacity").Initializer!.Value).Token.Value!);
        var guards = entry.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.Text == "CooperativeGuardPage")
            .ExpressionBody!.Expression;
        HostCheck.Equal("LinuxThreadProgram.IsGuardPage(page)", guards.ToString());
        var owner = Parse("src/OS/Boot/EfiEntry.LinuxThreads.cs");
        var prepare = owner.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.Text == "LinuxNativePrepare");
        HostCheck.True(prepare.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(n =>
            n.Left.ToString() == "(uint)sizeof(LinuxNativeControl)" && n.OperatorToken.Text == ">" && n.Right.ToString() == "8192"));
        var run = Parse("src/OS/Boot/EfiEntry.LinuxThreadRun.cs");
        var numbers = run.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n => n.Expression.ToString() == "LinuxNumber")
            .Select(n => n.ArgumentList.Arguments[1].Expression.ToString()).ToArray();
        foreach (string geometry in new[] { "NativeProgram.Bytes", "NativeProgram.MetadataPages * Page", "X64LinuxUserTransition.UserImageBytes" })
            HostCheck.Equal(1, numbers.Count(n => n == geometry));
        var rewrite = run.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.Text == "LinuxNativeRewriteEntry");
        var lookup = rewrite.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(n => n.Expression.ToString() == "CooperativeFindLeaf");
        HostCheck.Equal("state->CooperativePool+((NativeProgram.Entries+index*NativeProgram.WorkerStride)&~(Page-1))",
            string.Concat(lookup.ArgumentList.Arguments[1].Expression.DescendantTokens().Select(token => token.Text)));

        foreach(string leaf in new[]{"EnableM16011Probe","EnableM16012Probe"})
        {
            var socketProfile=EfiProfile.Create("Diagnostic",new Dictionary<string,bool>{[leaf]=true});
            symbols=socketProfile.Flags.Where(p=>p.Value).Select(p=>EfiContracts.ProbeSymbols[p.Key]).ToArray();
            var socketRetained=Parse("src/OS.Kernel/Boot/FirstOptRealExitBootServicesTransition.cs").DescendantNodes()
                .OfType<ClassDeclarationSyntax>().Single(n=>n.Identifier.Text=="FirstExitRetainedMappings");
            var capacity=socketRetained.Members.OfType<FieldDeclarationSyntax>().SelectMany(n=>n.Declaration.Variables)
                .Single(n=>n.Identifier.Text=="MaximumRanges").Initializer!.Value;
            ulong ranges=Sum(capacity);HostCheck.Equal(47UL,ranges);
            HostCheck.True(ranges<=SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges,
                "the live NIC and Linux runtime union must fit actual image and table-builder admission");
            var socketEntry=Parse("src/OS/Boot/EfiEntry.cs");
            var ledger=socketEntry.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(n=>n.Declaration.Variables)
                .Single(n=>n.Identifier.Text=="LinuxLedgerCapacity").Initializer!.Value;
            ulong ledgerCapacity=Sum(ledger);HostCheck.Equal(18UL,ledgerCapacity);
            var seal=socketEntry.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n=>n.Identifier.Text=="SealLinuxLedger");
            HostCheck.Equal(ledgerCapacity,5UL+(ulong)seal.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(n=>n.Expression.ToString()=="LinuxReservation"));
        }

        Microsoft.CodeAnalysis.SyntaxNode Parse(string path)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)), new CSharpParseOptions(preprocessorSymbols: symbols));
            HostCheck.True(!tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            return tree.GetRoot();
        }
        static ulong Sum(ExpressionSyntax expression) => expression switch
        {
            BinaryExpressionSyntax b when b.RawKind == (int)SyntaxKind.AddExpression => Sum(b.Left) + Sum(b.Right),
            LiteralExpressionSyntax literal => Convert.ToUInt64(literal.Token.Value),
            MemberAccessExpressionSyntax member when member.ToString() == "FirstExitImageMappings.MaximumSections" => FirstExitImageMappings.MaximumSections,
            MemberAccessExpressionSyntax member when member.ToString() == "FirstExitOwnedArena.MappingRangeCount" => FirstExitOwnedArena.MappingRangeCount,
            _ => throw new InvalidOperationException("unreviewed mapping capacity expression")
        };
    }

    [HostTest("m16-008 terminal memory cleanup follows owner descriptor release and binds the inactive root and static stack")]
    private static void MemoryCleanupBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.LinuxThreadRun.cs")),
            new CSharpParseOptions(preprocessorSymbols: ["M16_008_PROBE", "M16_009_PROBE"]));
        HostCheck.True(!tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var owner = methods.Single(m => m.Identifier.Text == "RunLinuxNativeOwner");
        var calls = owner.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var clear = calls.Single(c => c.Expression.ToString() == "LinuxNativeClearUserMemory");
        var teardown = calls.Single(c => c.Expression.ToString() == "LinuxNativeBeginTeardown");
        HostCheck.True(calls.Single(c => c.Expression.ToString() == "LinuxNativeRelease").SpanStart < clear.SpanStart &&
            calls.Single(c => c.Expression.ToString() == "LinuxDescriptorServices.End").SpanStart < clear.SpanStart &&
            clear.SpanStart < calls.Last(c => c.Expression.ToString() == "CooperativeSetPoolMappings").SpanStart);
        HostCheck.True(calls.Single(c => c.Expression.ToString() == "LinuxNativeRelease").SpanStart < teardown.SpanStart &&
            teardown.SpanStart < clear.SpanStart);
        HostCheck.True(clear.Parent is PrefixUnaryExpressionSyntax unary && unary.RawKind == (int)SyntaxKind.LogicalNotExpression &&
            unary.Parent is IfStatementSyntax gate && gate.Statement is ReturnStatementSyntax returned &&
            returned.Expression is InvocationExpressionSyntax rejected && rejected.Expression.ToString() == "LinuxNativeCleanupRejected");
        var rejection = methods.Single(m => m.Identifier.Text == "LinuxNativeCleanupRejected");
        HostCheck.True(rejection.DescendantNodes().OfType<ReturnStatementSyntax>().All(r =>
            r.Expression!.IsKind(SyntaxKind.FalseLiteralExpression)));
        var cleanup = methods.Single(m => m.Identifier.Text == "LinuxNativeClearUserMemory");
        var bodyCalls = cleanup.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var unmap = bodyCalls.Single(c => c.Expression.ToString() == "LinuxMemoryServices.Unmap");
        HostCheck.True(unmap.ArgumentList.Arguments.Select(a => a.Expression.ToString()).SequenceEqual(new[]
            { "&state->LinuxMemory", "LinuxMemoryServices.DynamicStart", "LinuxSyscallGateway.UserEnd - LinuxMemoryServices.DynamicStart" }));
        HostCheck.True(bodyCalls.Single(c => c.Expression.ToString() == "readRoot").SpanStart < unmap.SpanStart &&
            cleanup.ToString().Contains("range.PhysicalStart == state->LinuxImageBacking + imageBytes", StringComparison.Ordinal) &&
            cleanup.ToString().Contains("state->LinuxMemory.HasMutationGuard", StringComparison.Ordinal));
        HostCheck.True(cleanup.ToString().Contains("state->LinuxMemory.ReservedBytes != 0", StringComparison.Ordinal) &&
            cleanup.ToString().Contains("state->LinuxMemory.BackedPages != 0", StringComparison.Ordinal));
        var restore = bodyCalls.Single(c => c.Expression.ToString() == "LinuxMemoryServices.RestoreImageProtections");
        var baseline = bodyCalls.Single(c => c.Expression.ToString() == "X64UserAddressSpace.Audit");
        HostCheck.True(unmap.SpanStart < restore.SpanStart && restore.SpanStart < baseline.SpanStart &&
            restore.ArgumentList.Arguments.Single().Expression.ToString() == "&state->LinuxMemory" &&
            restore.Parent is BinaryExpressionSyntax reject && reject.IsKind(SyntaxKind.NotEqualsExpression) &&
            reject.Right is LiteralExpressionSyntax success && Convert.ToInt64(success.Token.Value) == 0 &&
            cleanup.ToString().Contains("state->LinuxMemory.HasImageProtections", StringComparison.Ordinal));
    }

    [HostTest("m16-008 native signal attachment drains before scrub and return preparation precedes full installation")]
    private static void SignalBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var trees = new[] { "LinuxThreads", "LinuxSignals" }.Select(name =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry." + name + ".cs")),
                new CSharpParseOptions(preprocessorSymbols: ["M16_008_PROBE"]))).ToArray();
        HostCheck.True(trees.All(t => !t.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)));
        var methods = trees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var bind = methods.Single(m => m.Identifier.Text == "LinuxNativeBind");
        CallsBefore(bind,"c->Process.Initialize","LinuxNativeBindSignals");
        var release = methods.Single(m => m.Identifier.Text == "LinuxNativeRelease");
        CallsBefore(release,"c->Process.Release","LinuxNativeReleaseSignals");
        CallsBefore(methods.Single(m => m.Identifier.Text == "LinuxNativeReleaseSignals"),"LinuxSignalServices.End",null);
        foreach (var pair in new[] { ("DispatchLinuxNativeCore","X64LinuxUserTransition.RestoreActiveUserImage"),
            ("LinuxNativeWorkerPrepare","X64LinuxUserTransition.InstallUserImage") })
            CallsBefore(methods.Single(m => m.Identifier.Text == pair.Item1),"LinuxNativeUserReturn",pair.Item2);
        var core = methods.Single(m => m.Identifier.Text == "DispatchLinuxNativeCore");
        var captured = core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "call");
        HostCheck.True(captured.SpanStart < core.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(c => c.Expression.ToString() == "LinuxSyscallGateway.Dispatch").SpanStart);
        var overwritten = core.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(a => a.Left.ToString() == "call");
        HostCheck.Equal("call.Number != 15",overwritten.Ancestors().OfType<IfStatementSyntax>().First().Condition.ToString());

        static void CallsBefore(MethodDeclarationSyntax method,string first,string? second)
        {
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            var a = calls.Single(c => c.Expression.ToString() == first);
            if (second != null) HostCheck.True(a.SpanStart < calls.Single(c => c.Expression.ToString() == second).SpanStart);
        }
    }

    [HostTest("m16-008 native call witness reads the current retained wait after real dispatch without inventing deadlines")]
    private static void WaitObservationBoundary()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.LinuxThreads.cs")),
            new CSharpParseOptions(preprocessorSymbols:["M16_008_PROBE"]));
        var core=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="DispatchLinuxNativeCore");
        var calls=core.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var observation=calls.Single(c=>c.Expression.ToString()=="c->Process.Inspect" &&
            c.ArgumentList.Arguments.Last().ToString()=="out var waitView");
        HostCheck.Equal("c->Key, 2, c->Threads.Current, out var waitView",observation.ArgumentList.Arguments.ToString());
        HostCheck.True(calls.Single(c=>c.Expression.ToString()=="LinuxSyscallGateway.Dispatch").SpanStart<observation.SpanStart &&
            calls.Single(c=>c.Expression.ToString()=="LinuxNativeUserReturn").SpanStart<observation.SpanStart);
        var dispatched=calls.Single(c=>c.Expression.ToString()=="c->Process.Inspect" &&
            c.ArgumentList.Arguments.Last().ToString()=="out var dispatchedView");
        HostCheck.True(dispatched.SpanStart<calls.Single(c=>c.Expression.ToString()=="LinuxNativeUserReturn").SpanStart);
        var returned=core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v=>v.Identifier.Text=="returned");
        HostCheck.Equal("*frame",returned.Initializer!.Value.ToString());
        HostCheck.True(returned.SpanStart>calls.Single(c=>c.Expression.ToString()=="LinuxSyscallGateway.Dispatch").SpanStart && returned.SpanStart<dispatched.SpanStart);
        HostCheck.True(observation.Parent is PrefixUnaryExpressionSyntax unary && unary.Parent is IfStatementSyntax guard &&
            guard.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c=>c.Expression.ToString()=="LinuxNativeAbort") &&
            guard.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any());
        var logged=core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v=>v.Identifier.Text=="logged");
        string[] observed=logged.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c=>c.Expression.ToString()=="LinuxNumber")
            .Select(c=>c.ArgumentList.Arguments[1].ToString()).TakeLast(13).ToArray();
        HostCheck.True(observed.SequenceEqual(new[]{"c->Gates[index].Calls","waitView.WaitSource","waitView.WaitDeadline",
            "waitView.Wait.IsValid ? waitView.Wait.Sequence : 0","dispatchedView.SignalDepth","dispatchedView.SignalFrame","dispatchedView.BlockedSignals",
            "returned.Rip","returned.Rsp","returned.Rflags","returned.FsBase","returned.GsBase","unchecked((ulong)result.Value)"}));
        var names=logged.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c=>c.Expression.ToString()=="LinuxText")
            .Select(c=>(c.ArgumentList.Arguments[1].Expression as LiteralExpressionSyntax)?.Token.ValueText).TakeLast(14).ToArray();
        HostCheck.True(names.SequenceEqual(new[]{" ordinal="," waitSource="," waitDeadline="," waitSequence="," signalDepth="," signalFrame="," blockedSignals=",
            " returnRip="," returnRsp="," returnFlags="," returnFs="," returnGs="," returnValueBits=","\r\n"}));
    }

    [HostTest("m16 native exit-tail return policy is wired to the current selected gate with mask and generation guards")]
    private static void ExitTailOwner()
    {
        string path=Path.Combine(HostToolProcess.FindRepositoryRoot(),"src/OS/Boot/EfiEntry.LinuxThreads.cs");
        var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(path),new CSharpParseOptions(preprocessorSymbols:["M16_008_PROBE","M16_009_PROBE"])).GetRoot();
        var method=syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="LinuxNativeReturnPolicy");
        var assignment=syntax.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(a=>a.Left.ToString()=="ReturnPolicy");
        HostCheck.True(assignment.Right is PrefixUnaryExpressionSyntax address && address.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddressOfExpression) &&
            address.Operand.ToString()==method.Identifier.Text);
        var calls=method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        foreach(string name in new[]{"LinuxSyscallAuthority","CooperativeAuthority","LinuxNativeOwner","c->Process.Inspect"})
            HostCheck.True(calls.Any(c=>c.Expression.ToString()==name),"native proof includes "+name);
        var policy=calls.Single(c=>c.Expression.ToString()=="LinuxSyscallGateway.ExitTailPolicy");
        HostCheck.True(policy.ArgumentList.Arguments[0].RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword) &&
            policy.ArgumentList.Arguments[0].Expression.ToString()=="c->Gates[index]" && policy.ArgumentList.Arguments[1].Expression.ToString()=="*frame");
        var guards=method.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(b=>b.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression)).ToArray();
        foreach(var pair in new[]{("c->Threads.Current.Value","c->Plan.Bindings[index].Context.Identity"),("view.SignalDepth","0"),("view.BlockedSignals","~LinuxSignalMask.Unblockable")})
            HostCheck.True(guards.Any(g=>g.Left.ToString()==pair.Item1 && g.Right.ToString()==pair.Item2),"native return guard "+pair.Item1);
    }

    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try { action(); } catch (ToolFailure failure) { reason = failure.Reason; }
        HostCheck.True(reason is not null && (expected is null || reason == expected));
    }
}
