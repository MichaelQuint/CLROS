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
            foreach (string parent in parents) Reject(() => EfiOptions.Parse(["--" + flag, "--" + parent + ":false"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(n => n != flag && !parents.Contains(n)))
                Reject(() => EfiOptions.Parse(["--" + flag, "--" + conflict], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--" + flag], build).Profile());
            string[] compile = EfiProjectAdmission.ExpectedCompileItems(profile);
            foreach (string include in new[] { @"..\OS.Kernel\Threading\WaitAddress.cs|Threading\WaitAddress.cs",
                @"Boot\EfiEntry.LinuxThreads.cs|", @"Boot\EfiEntry.LinuxThreadMemory.cs|",
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
        HostCheck.Equal(FirstExitImageMappings.MaximumSections + FirstExitOwnedArena.MappingRangeCount + 18UL, Sum(Capacity("MaximumRanges")));
        var ecam = retained.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Single(n => n.Right.ToString() == "references.EcamStart");
        HostCheck.True(ecam.Left.ToString() == "starts[allocations]");
        var entry = Parse("src/OS/Boot/EfiEntry.cs");
        var fields = entry.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(n => n.Declaration.Variables);
        HostCheck.Equal(64, (int)((LiteralExpressionSyntax)fields.Single(n => n.Identifier.Text == "CooperativePoolPages").Initializer!.Value).Token.Value!);
        HostCheck.Equal(4, (int)((LiteralExpressionSyntax)fields.Single(n => n.Identifier.Text == "CooperativeCodePages").Initializer!.Value).Token.Value!);
        HostCheck.Equal(17, (int)((LiteralExpressionSyntax)fields.Single(n => n.Identifier.Text == "LinuxLedgerCapacity").Initializer!.Value).Token.Value!);
        var guards = entry.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.Text == "CooperativeGuardPage")
            .ExpressionBody!.Expression.DescendantNodes().OfType<LiteralExpressionSyntax>().Select(n => (int)n.Token.Value!).ToArray();
        HostCheck.True(guards.SequenceEqual(new[] { 15, 32, 33, 38, 39, 44, 45, 50, 51, 56, 57, 62 }));

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

    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try { action(); } catch (ToolFailure failure) { reason = failure.Reason; }
        HostCheck.True(reason is not null && (expected is null || reason == expected));
    }
}
