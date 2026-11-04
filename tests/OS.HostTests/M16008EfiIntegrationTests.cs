using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Tooling;

internal static unsafe partial class M16008EfiIntegrationTests
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
            foreach (string parent in parents)
                Reject(() => EfiOptions.Parse(["--" + flag, "--" + parent + ":false"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(n => n != flag &&
                n is not ("EnableM16009Probe" or "EnableM09011Probe" or "EnableM09012Probe" or "EnableM16010Probe" or "EnableM16011Probe" or "EnableM16012Probe" or "EnableM16013Probe" or "EnableM16014Probe" or "EnableM10004Probe" or "EnableM10017Probe" or "EnableM10018Probe" or "EnableM10018FullProbe" or "EnableM10018TimingProbe" or "EnableM10018EvidenceTraceProbe" or "EnableM10018EvidenceSummaryProbe" or "EnableM10018ServingBenchmarkProbe" or "EnableM16025Probe" or "EnableM16027Probe" or "EnableM16027TimingProbe" or "EnableM16019UnavailableProbe" or "EnableM16019InvalidProbe" or "EnableM07027Probe" or "EnableM07028Probe") && !parents.Contains(n)))
                Reject(() => EfiOptions.Parse(["--" + flag, "--" + conflict], build).Profile());
            foreach (string mode in new[] { "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
            {
                var evidence = EfiOptions.Parse(["--" + flag, "--" + mode], build).Profile();
                HostCheck.Equal("Diagnostic", evidence.BootPolicy);
                HostCheck.True(evidence.IsEnabled(flag) && parents.All(evidence.IsEnabled) && profile.Flags.Where(x => x.Value).All(x => evidence.IsEnabled(x.Key)) && evidence.IsEnabled(mode));
                string opposite = mode == "EnableM10018EvidenceTraceProbe"
                    ? "EnableM10018EvidenceSummaryProbe" : "EnableM10018EvidenceTraceProbe";
                HostCheck.True(!evidence.IsEnabled(opposite));
                foreach (string required in new[] { "M10018Timing", "M10018Full", "M10018", "M16025", "M16023", "M16024", "M16012", "M10004", "M16003" })
                    HostCheck.True(evidence.IsEnabled(required), "evidence prerequisite: " + required);
            }
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--" + flag], build).Profile());
            var source = HostSourceCatalog.Efi(profile);
            _ = source.Type("OS.Kernel.Threading.WaitAddress");
            _ = source.Type("OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram");
            _ = source.Method("OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices", "EnablePipes");
            const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
            foreach (string method in new[] { "LinuxNativePrepare", "LinuxNativeBindSignals", "LinuxThreadCopyInput",
                "RunLinuxNativeOwner", "LinuxNativeInstallTimer" })
                _ = source.Method(owner, method);
            foreach (string forbidden in new[] { "RunKernelThreads", "ThreadWaitPrepare" })
                HostCheck.True(source.Type(owner).GetMembers(forbidden).Length == 0);
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
        foreach (string flag in new[] { "EnableM16008Probe", "EnableM16007Probe" })
            ValidateBoot(Select(flag), flag == "EnableM16008Probe");
    }

    private static void ValidateBoot(CSharpSourceCatalog source, bool threads)
    {
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        const string boot = "OS.Boot.BootSequence";
        HostCheck.True(HostBootSourceGuard.CallsConfined(source, "OS.Boot.EfiEntry", owner, boot,
            threads ? ["RunOwnedApicSetup", "RunOwnedLinuxThreads"] : ["RunOwnedLinuxDescriptors"],
            threads ? ["RunOwnedLinuxDescriptors", "RunOwnedLinuxSignals"] : ["RunOwnedLinuxThreads", "RunOwnedLinuxSignals"],
            true, "Diagnose"));
        if (!threads)
            return;
        HostCheck.True(HostBootSourceGuard.OrderedCalls(source, owner, "Diagnose", boot, "RunOwnedApicSetup", "RunOwnedLinuxThreads"));
        var diagnose = source.Method(owner, "Diagnose");
        var model = source.Compilation.GetSemanticModel(diagnose.SyntaxTree);
        var calls = source.MethodInvocations(diagnose).Where(call =>
            SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(boot))).ToArray();
        var apic = calls.Single(call => call.TargetMethod.Name == "RunOwnedApicSetup");
        var thread = calls.Single(call => call.TargetMethod.Name == "RunOwnedLinuxThreads");
        var gate = apic.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        bool returns = gate.Statement is ReturnStatementSyntax
            || gate.Statement is BlockSyntax { Statements.Count: 1 } block && block.Statements[0] is ReturnStatementSyntax;
        HostCheck.True(returns && gate.Parent == thread.Syntax.Ancestors().OfType<IfStatementSyntax>().First().Parent
            && model.GetOperation(gate.Condition) is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation condition }
            && condition.Syntax.Span == apic.Syntax.Span);
        foreach (var call in new[] { apic, thread })
            HostCheck.True(model.AnalyzeControlFlow(call.Syntax.Ancestors().OfType<StatementSyntax>().First())
                is { Succeeded: true, StartPointIsReachable: true });
        var plan = source.Method(owner, "LinuxUserTransitionPlan");
        var planModel = source.Compilation.GetSemanticModel(plan.SyntaxTree);
        var resume = source.Type("OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransitionPlan").GetMembers("ResumeUser").Single();
        HostCheck.True(plan.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            SymbolEqualityComparer.Default.Equals(planModel.GetSymbolInfo(assignment.Left).Symbol, resume)
            && planModel.GetConstantValue(assignment.Right) is { HasValue: true, Value: true }));
        HostCheck.Equal(1, source.MethodInvocations(source.Method(owner, "RunOwnedLinuxSyscalls")).Count(call =>
            call.TargetMethod.Name == "RunLinuxNativeOwner"
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(owner))));
    }

    [HostTest("m16.008 combined source layout keeps clock PCI APIC and context helper bytes disjoint")]
    private static void Layout() => ValidateLayout(Select);

    private static void ValidateLayout(Func<string, CSharpSourceCatalog> select)
    {
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        const string retainedOwner = "OS.Boot.FirstExitRetainedMappings";
        const string tableOwner = "OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables";
        const string userOwner = "OS.Kernel.Memory.X64UserAddressSpace";
        const string program = "OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram";
        var source = select("EnableM16008Probe");
        uint Offset(string name) => checked((uint)Number("OS.Boot.FirstExitCodeBundle", name));
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
            for (int j = 0; j < i; j++)
                HostCheck.True(bodies[i].Start >= bodies[j].Start + bodies[j].Bytes || bodies[j].Start >= bodies[i].Start + bodies[i].Bytes);
        }
        HostCheck.Equal(19UL, Number(retainedOwner, "MaximumAllocations"));
        HostCheck.Equal(FirstExitImageMappings.MaximumSections + FirstExitOwnedArena.MappingRangeCount + 19UL,
            Number(retainedOwner, "MaximumRanges"));
        HostCheck.True(Number(retainedOwner, "MaximumRanges") <= Number(tableOwner, "MaximumRetainedRanges"),
            "the combined native profile must fit image/table-builder retained-range admission");
        HostCheck.True(HostRetainedMappingSourceGuard.Check(source, "EcamStart"));
        foreach (var (field, target) in new[] { ("CooperativePoolPages", "PoolPages"), ("CooperativeCodePages", "CodePages") })
        {
            var initializer = Initializer(owner, field);
            HostCheck.True(SymbolEqualityComparer.Default.Equals(source.Compilation.GetSemanticModel(initializer.SyntaxTree)
                .GetSymbolInfo(initializer).Symbol, Field(program, target)), "bound program geometry " + field);
        }
        HostCheck.Equal(17UL, Number(owner, "LinuxLedgerCapacity"));
        var guard = source.Method(owner, "CooperativeGuardPage");
        var guardCall = source.MethodInvocations(guard).Single();
        var guardModel = source.Compilation.GetSemanticModel(guard.SyntaxTree);
        ExpressionSyntax? returnedGuard = guard.ExpressionBody?.Expression
            ?? (guard.Body is { Statements.Count: 1 } body && body.Statements[0] is ReturnStatementSyntax returned
                ? returned.Expression : null);
        HostCheck.True(guardCall.TargetMethod.Name == "IsGuardPage"
            && SymbolEqualityComparer.Default.Equals(guardCall.TargetMethod.ContainingType, source.Type(program))
            && HostSourceOperations.IsParameter(HostSourceOperations.Argument(guardCall, 0), guardModel.GetDeclaredSymbol(guard)!.Parameters[0])
            && returnedGuard is not null && returnedGuard.SyntaxTree == guardCall.Syntax.SyntaxTree
            && returnedGuard.Span == guardCall.Syntax.Span);
        var prepare = source.Method(owner, "LinuxNativePrepare");
        var prepareModel = source.Compilation.GetSemanticModel(prepare.SyntaxTree);
        HostCheck.True(prepare.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(expression =>
            ControlSize(prepareModel.GetOperation(expression.Left)) && expression.IsKind(SyntaxKind.GreaterThanExpression)
            && prepareModel.GetConstantValue(expression.Right) is { HasValue: true, Value: 8192 }));
        var run = source.Method(owner, "RunLinuxNativeOwner");
        var numbers = source.MethodInvocations(run).Where(call => call.TargetMethod.Name == "LinuxNumber"
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(owner)))
            .Select(call => Tokens(HostSourceOperations.Argument(call, 1).Syntax)).ToArray();
        foreach (string geometry in new[] { "NativeProgram.Bytes", "NativeProgram.MetadataPages*Page", "X64LinuxUserTransition.UserImageBytes" })
            HostCheck.Equal(1, numbers.Count(value => value == geometry));
        var rewrite = source.Method(owner, "LinuxNativeRewriteEntry");
        var lookup = source.MethodInvocations(rewrite).Single(call => call.TargetMethod.Name == "CooperativeFindLeaf"
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(owner)));
        HostCheck.Equal("state->CooperativePool+((NativeProgram.Entries+index*NativeProgram.WorkerStride)&~(Page-1))",
            Tokens(HostSourceOperations.Argument(lookup, 1).Syntax));

        foreach (string leaf in new[] { "EnableM16011Probe", "EnableM16012Probe", "EnableM10017Probe", "EnableM16025Probe" })
        {
            source = select(leaf);
            bool disk = leaf == "EnableM16025Probe";
            ulong ranges = Number(retainedOwner, "MaximumRanges");
            HostCheck.Equal(disk ? 54UL : 47UL, ranges);
            ulong tableCapacity = Number(tableOwner, "MaximumRetainedRanges");
            HostCheck.Equal(disk ? 54UL : 48UL, tableCapacity);
            HostCheck.True(ranges <= tableCapacity, "the live NIC and Linux runtime union must fit actual image and table-builder admission");
            ulong userRanges = Number(userOwner, "MaximumRanges");
            HostCheck.Equal(leaf == "EnableM10017Probe" ? 384UL : disk ? 135UL : 128UL, userRanges);
            HostCheck.Equal(128UL, Number(userOwner, "MaximumTables"));
            if (disk)
                HostCheck.True(65UL + 64 < userRanges, "the measured disk static inventory must retain all 64 mutable regions and candidate slack");
            HostCheck.True(HostRetainedMappingSourceGuard.Check(source, "VirtioBarStart"),
                "device ownership must append using the original paired arrays and advancing index");
            HostCheck.Equal(leaf == "EnableM10017Probe" ? 70UL : 64UL, Number(retainedOwner, "MaximumDescriptorPages"));
            ulong ledgerCapacity = Number(owner, "LinuxLedgerCapacity");
            HostCheck.Equal(disk ? 19UL : 18UL, ledgerCapacity);
            var seal = source.Method(owner, "SealLinuxLedger");
            HostCheck.Equal(ledgerCapacity, 5UL + (ulong)source.MethodInvocations(seal).Count(call =>
                call.TargetMethod.Name == "LinuxReservation" && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(owner))));
        }

        foreach (string leaf in new[] { "EnableM16027Probe", "EnableM10018Probe" })
        {
            source = select(leaf);
            var selectedPrepare = source.Method(owner, "LinuxNativePrepare");
            var model = source.Compilation.GetSemanticModel(selectedPrepare.SyntaxTree);
            var limit = selectedPrepare.DescendantNodes().OfType<BinaryExpressionSyntax>().Single(expression =>
                ControlSize(model.GetOperation(expression.Left)) && expression.IsKind(SyntaxKind.GreaterThanExpression));
            if (leaf == "EnableM16027Probe")
                HostCheck.True(SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(limit.Right).Symbol, Field(program, "WritableControlMaximumBytes")));
            else
                HostCheck.True(model.GetConstantValue(limit.Right) is { HasValue: true, Value: 8192 });
            HostCheck.Equal(1, source.MethodInvocations(selectedPrepare).Count(call => call.TargetMethod.Name == "LinuxNativeTake"
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, source.Type(owner))
                && ControlSize(call.Arguments.Single(argument => argument.Parameter?.Ordinal == 2).Value)));
        }

        bool ControlSize(IOperation? value) => value is IConversionOperation
        { Type.SpecialType: SpecialType.System_UInt32, Operand: ISizeOfOperation size }
            && SymbolEqualityComparer.Default.Equals(size.TypeOperand, source.Type(owner + "+LinuxNativeControl"));
        IFieldSymbol Field(string type, string name) => source.Type(type).GetMembers(name).OfType<IFieldSymbol>().Single();
        ulong Number(string type, string name)
        {
            var field = Field(type, name);
            HostCheck.True(field.HasConstantValue, "constant layout field " + type + "." + name);
            return Convert.ToUInt64(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }
        ExpressionSyntax Initializer(string type, string name) =>
            ((VariableDeclaratorSyntax)Field(type, name).DeclaringSyntaxReferences.Single().GetSyntax()).Initializer!.Value;
        static string Tokens(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(token => token.Text));
    }

    private static CSharpSourceCatalog Select(string flag) =>
        HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { [flag] = true }));

    [HostTest("m16-008 terminal memory cleanup follows owner descriptor release and binds the inactive root and static stack")]
    private static void MemoryCleanupBoundary()
    {
        var methods = OwnerMethods(Select("EnableM16009Probe"));
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
        var methods = OwnerMethods(Select("EnableM16008Probe"));
        var bind = methods.Single(m => m.Identifier.Text == "LinuxNativeBind");
        CallsBefore(bind, "c->Process.Initialize", "LinuxNativeBindSignals");
        var release = methods.Single(m => m.Identifier.Text == "LinuxNativeRelease");
        CallsBefore(release, "c->Process.Release", "LinuxNativeReleaseSignals");
        CallsBefore(methods.Single(m => m.Identifier.Text == "LinuxNativeReleaseSignals"), "LinuxSignalServices.End", null);
        foreach (var pair in new[] { ("DispatchLinuxNativeCore","X64LinuxUserTransition.RestoreActiveUserImage"),
            ("LinuxNativeWorkerPrepare","X64LinuxUserTransition.InstallUserImage") })
            CallsBefore(methods.Single(m => m.Identifier.Text == pair.Item1), "LinuxNativeUserReturn", pair.Item2);
        var core = methods.Single(m => m.Identifier.Text == "DispatchLinuxNativeCore");
        var captured = core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "call");
        HostCheck.True(captured.SpanStart < core.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(c => c.Expression.ToString() == "LinuxSyscallGateway.Dispatch").SpanStart);
        var overwritten = core.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(a => a.Left.ToString() == "call");
        HostCheck.Equal("call.Number != 15", overwritten.Ancestors().OfType<IfStatementSyntax>().First().Condition.ToString());

        static void CallsBefore(MethodDeclarationSyntax method, string first, string? second)
        {
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            var a = calls.Single(c => c.Expression.ToString() == first);
            if (second != null)
                HostCheck.True(a.SpanStart < calls.Single(c => c.Expression.ToString() == second).SpanStart);
        }
    }

    [HostTest("m16-008 native call witness reads the current retained wait after real dispatch without inventing deadlines")]
    private static void WaitObservationBoundary()
    {
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        var core = Select("EnableM16008Probe").Method(owner, "DispatchLinuxNativeCore");
        var calls = core.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var observation = calls.Single(c => c.Expression.ToString() == "c->Process.Inspect" &&
            c.ArgumentList.Arguments.Last().ToString() == "out var waitView");
        HostCheck.Equal("c->Key, 2, c->Threads.Current, out var waitView", observation.ArgumentList.Arguments.ToString());
        HostCheck.True(calls.Single(c => c.Expression.ToString() == "LinuxSyscallGateway.Dispatch").SpanStart < observation.SpanStart &&
            calls.Single(c => c.Expression.ToString() == "LinuxNativeUserReturn").SpanStart < observation.SpanStart);
        var dispatched = calls.Single(c => c.Expression.ToString() == "c->Process.Inspect" &&
            c.ArgumentList.Arguments.Last().ToString() == "out var dispatchedView");
        HostCheck.True(dispatched.SpanStart < calls.Single(c => c.Expression.ToString() == "LinuxNativeUserReturn").SpanStart);
        var returned = core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "returned");
        HostCheck.Equal("*frame", returned.Initializer!.Value.ToString());
        HostCheck.True(returned.SpanStart > calls.Single(c => c.Expression.ToString() == "LinuxSyscallGateway.Dispatch").SpanStart && returned.SpanStart < dispatched.SpanStart);
        HostCheck.True(observation.Parent is PrefixUnaryExpressionSyntax unary && unary.Parent is IfStatementSyntax guard &&
            guard.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(c => c.Expression.ToString() == "LinuxNativeAbort") &&
            guard.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any());
        var logged = core.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "logged");
        string[] observed = logged.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => c.Expression.ToString() == "LinuxNumber")
            .Select(c => c.ArgumentList.Arguments[1].ToString()).TakeLast(13).ToArray();
        HostCheck.True(observed.SequenceEqual(new[]{"LinuxNativeCallOrdinal(c, index)","waitView.WaitSource","waitView.WaitDeadline",
            "waitView.Wait.IsValid ? waitView.Wait.Sequence : 0","dispatchedView.SignalDepth","dispatchedView.SignalFrame","dispatchedView.BlockedSignals",
            "returned.Rip","returned.Rsp","returned.Rflags","returned.FsBase","returned.GsBase","unchecked((ulong)result.Value)"}));
        foreach (bool service in new[] { false, true })
        {
            var source = Select(service ? "EnableM16014Probe" : "EnableM16011Probe");
            var ordinal = source.Method(owner, "LinuxNativeCallOrdinal");
            HostCheck.Equal(1, ordinal.Body!.Statements.Count);
            HostCheck.Equal(service ? "c->Gates[index].TotalCalls" : "c->Gates[index].Calls",
                ((ReturnStatementSyntax)ordinal.Body.Statements.Single()).Expression!.ToString());
            var message = source.Method(owner, "ObserveLinuxSocketMessage");
            var messageOrdinal = message.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "ordinal");
            HostCheck.Equal("LinuxNativeCallOrdinal(c, c->Selected - 1)", messageOrdinal.Initializer!.Value.ToString());
        }
        var names = logged.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(c => c.Expression.ToString() == "LinuxText")
            .Select(c => (c.ArgumentList.Arguments[1].Expression as LiteralExpressionSyntax)?.Token.ValueText).TakeLast(14).ToArray();
        HostCheck.True(names.SequenceEqual(new[]{" ordinal="," waitSource="," waitDeadline="," waitSequence="," signalDepth="," signalFrame="," blockedSignals=",
            " returnRip="," returnRsp="," returnFlags="," returnFs="," returnGs="," returnValueBits=","\r\n"}));
    }

    [HostTest("m16 native exit-tail return policy is wired to the current selected gate with mask and generation guards")]
    private static void ExitTailOwner()
    {
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        var source = Select("EnableM16009Probe");
        var method = source.Method(owner, "LinuxNativeReturnPolicy");
        var assignment = source.Method(owner, "LinuxNativePrepare").DescendantNodes()
            .OfType<AssignmentExpressionSyntax>().Single(a => a.Left.ToString() == "ReturnPolicy");
        HostCheck.True(assignment.Right is PrefixUnaryExpressionSyntax address && address.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddressOfExpression) &&
            address.Operand.ToString() == method.Identifier.Text);
        var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        foreach (string name in new[] { "LinuxSyscallAuthority", "CooperativeAuthority", "LinuxNativeOwner", "c->Process.Inspect" })
            HostCheck.True(calls.Any(c => c.Expression.ToString() == name), "native proof includes " + name);
        var policy = calls.Single(c => c.Expression.ToString() == "LinuxSyscallGateway.ExitTailPolicy");
        HostCheck.True(policy.ArgumentList.Arguments[0].RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword) &&
            policy.ArgumentList.Arguments[0].Expression.ToString() == "c->Gates[index]" && policy.ArgumentList.Arguments[1].Expression.ToString() == "*frame");
        var guards = method.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(b => b.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression)).ToArray();
        foreach (var pair in new[] { ("c->Threads.Current.Value", "c->Plan.Bindings[index].Context.Identity"), ("view.SignalDepth", "0"), ("view.BlockedSignals", "~LinuxSignalMask.Unblockable") })
            HostCheck.True(guards.Any(g => g.Left.ToString() == pair.Item1 && g.Right.ToString() == pair.Item2), "native return guard " + pair.Item1);
    }

    private static MethodDeclarationSyntax[] OwnerMethods(CSharpSourceCatalog source) =>
        source.Type("OS.Boot.EfiEntry+M03FirstExitAdapter").GetMembers().OfType<IMethodSymbol>()
            .SelectMany(method => method.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax()).OfType<MethodDeclarationSyntax>().ToArray();

    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try
        {
            action();
        }
        catch (ToolFailure failure)
        {
            reason = failure.Reason;
        }
        HostCheck.True(reason is not null && (expected is null || reason == expected));
    }
}
