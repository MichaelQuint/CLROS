using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Kernel.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static unsafe class M07026EfiIntegrationTests
{
    [HostTest("m07-026 profile defaults off and admits only the complete service and ARP chain", HostLane.Fast, 0)]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!BootSequence.PrepareNetworkService(true, &calls, &Execute) && calls == 0);
        HostCheck.True(!Probes.EnableM07026Probe && !BootSequence.RunOwnedNetworkService(true, true, true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(["--EnableM07026Probe"], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.Equal(13, profile.Flags.Count(x => x.Value));
            foreach (var parent in profile.Flags.Where(x => x.Value && x.Key != "EnableM07026Probe"))
                Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--" + parent.Key, "false"], build).Profile());
            foreach (string extra in EfiContracts.DiagnosticProbes.Except(profile.Flags.Where(x => x.Value).Select(x => x.Key)))
                Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--" + extra], build).Profile());
            Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        var options = QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--RequireMarker", "CLROS_M07_026_READY");
        var plan = QemuProfiles.Create(options);
        HostCheck.Equal(285, options.TimeoutSeconds);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--TimeoutSeconds", "259")));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--TimeoutSeconds", "296")));
        HostCheck.True(plan.Required.Contains("CLROS_M07_026_READY"));
        HostCheck.True(plan.Ordered.SequenceEqual(QemuTranscripts.M07026Markers.Select(x => x.Trim())));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--RequireMarker", "CLROS_M16_003_READY")));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }
    private static void Reject(Action action)
    {
        try { action(); } catch (ToolFailure) { return; }
        throw new InvalidOperationException("m07_026_invalid_profile_accepted");
    }

    [HostTest("m07-026 runner emits the original MAC independent capture and complete protocol", HostLane.Fast, 1)]
    private static void Runner()
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--DryRun", "--BuildEfi", "--EnableM07026Probe"]);
        HostCheck.True(result.ExitCode == 0, result.Error + result.Output);
        string markers = string.Join(';', QemuTranscripts.M07026Markers.Select(x => x.Trim()));
        foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
            HostCheck.True(result.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
        HostCheck.True(result.Output.Contains(",mac=02:00:00:00:00:01", StringComparison.Ordinal) &&
            result.Output.Contains("filter-dump,id=clroscapture,netdev=n0,file=", StringComparison.Ordinal) &&
            result.Output.Contains("socket,id=n0,connect=127.0.0.1:<ephemeral>", StringComparison.Ordinal));
    }

    [HostTest("m07-026 worker reservation participates in ledger and pre-write overlap admission", HostLane.Fast, 3)]
    private static void WorkerReservation()
    {
        foreach (string probe in new[] { "EnableM07026Probe", "EnableM16003Probe" })
        {
            bool workers = probe == "EnableM07026Probe";
            var profile = EfiOptions.Parse(["--" + probe], true).Profile();
            string[] symbols = ["NOSTDLIB_EFI", .. profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key])];
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(),
                "src/OS/Boot/EfiEntry.cs")), new CSharpParseOptions(preprocessorSymbols: symbols));
            var adapter = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Single(x => x.Identifier.Text == "M03FirstExitAdapter");
            int capacity = (int)((LiteralExpressionSyntax)adapter.Members.OfType<FieldDeclarationSyntax>()
                .SelectMany(x => x.Declaration.Variables).Single(x => x.Identifier.Text == "LinuxLedgerCapacity")
                .Initializer!.Value).Token.Value!;
            HostCheck.Equal(workers ? 11 : 9, capacity, probe + " ledger capacity");
            var seal = adapter.Members.OfType<MethodDeclarationSyntax>().Single(x => x.Identifier.Text == "SealLinuxLedger");
            var reservations = seal.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(x => x.Expression.ToString() == "LinuxReservation").ToArray();
            HostCheck.Equal(capacity, 5 + reservations.Length, probe + " complete reservation count");
            var worker = reservations.Where(x => x.ArgumentList.Arguments[1].Expression.ToString() == "state->CooperativePool").ToArray();
            HostCheck.Equal(workers ? 1 : 0, worker.Length, probe + " worker ownership");
            if (workers)
            {
                HostCheck.Equal("BootReservationKind.DiagnosticState", worker[0].ArgumentList.Arguments[0].Expression.ToString());
                HostCheck.Equal("CooperativePoolPages", worker[0].ArgumentList.Arguments[2].Expression.ToString());
                HostCheck.Equal("ledger + count++", worker[0].ArgumentList.Arguments[3].Expression.ToString());
            }
            var overlap = adapter.Members.OfType<MethodDeclarationSyntax>().Single(x => x.Identifier.Text == "LinuxPoolDisjoint");
            var checks = overlap.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => x.Expression.ToString() == "Disjoint").ToArray();
            HostCheck.Equal(workers ? 1 : 0, checks.Length, probe + " pre-write overlap check");
            if (workers)
            {
                HostCheck.Equal("address", checks[0].ArgumentList.Arguments[0].Expression.ToString());
                HostCheck.Equal("pages * Page", checks[0].ArgumentList.Arguments[1].Expression.ToString());
                HostCheck.Equal("state->CooperativePool", checks[0].ArgumentList.Arguments[2].Expression.ToString());
                HostCheck.Equal("CooperativePoolPages * Page", checks[0].ArgumentList.Arguments[3].Expression.ToString());
                HostCheck.True(overlap.ExpressionBody!.Expression.ToString().StartsWith("state->CooperativePool >= Page &&", StringComparison.Ordinal));
            }
            else HostCheck.Equal(SyntaxKind.TrueLiteralExpression, overlap.ExpressionBody!.Expression.Kind());
        }
    }

    [HostTest("m07-026 actual combined helper ranges preserve network and cooperative bytes", HostLane.Fast, 2)]
    private static void HelperLayout()
    {
        var profile = EfiOptions.Parse(["--EnableM07026Probe"], true).Profile();
        string[] symbols = ["NOSTDLIB_EFI", .. profile.Flags.Where(p => p.Value).Select(p => EfiContracts.ProbeSymbols[p.Key])];
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(),
            "src/OS.Kernel/Boot/FirstOptRealExitBootServicesTransition.cs")), new CSharpParseOptions(preprocessorSymbols: symbols));
        var bundle = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(x => x.Identifier.Text == "FirstExitCodeBundle");
        uint Offset(string name) => checked((uint)(int)((LiteralExpressionSyntax)bundle.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(x => x.Declaration.Variables).Single(x => x.Identifier.Text == name).Initializer!.Value).Token.Value!);
        (string Name, uint Start, uint Bytes)[] helpers = [("clock",Offset("ClockAccessOffset"),X64OwnedClockAccess.Bytes),
            ("pci-read",Offset("PciReadOffset"),X64PciConfigurationRead.Bytes), ("pci-bar",Offset("PciBarOffset"),X64PciBarAccess.Bytes),
            ("queue-fence",Offset("QueueFenceOffset"),X64VirtqueueMemoryFence.Bytes),
            ("context-observer",Offset("CooperativeObserveOffset"),ThreadContextSwitch.ObserverBytes),
            ("fx-snapshot",Offset("CooperativeFxOffset"),ThreadContextSwitch.FxSnapshotBytes),
            ("owned-segments",Offset("CooperativeSegmentsOffset"),ThreadContextSwitch.OwnedSegmentsBytes),
            ("guard-publication",Offset("CooperativePublishOffset"),X64PhysicalMappingPublication.GuardRequiredBytes)];
        foreach (var helper in helpers) HostCheck.True(helper.Start >= 4608 && helper.Start + helper.Bytes <= Offset("StackEntryOffset"));
        for (int i = 0; i < helpers.Length; i++)
            for (int j = 0; j < i; j++) HostCheck.True(helpers[i].Start >= helpers[j].Start + helpers[j].Bytes ||
                helpers[j].Start >= helpers[i].Start + helpers[i].Bytes, helpers[i].Name + " overlaps " + helpers[j].Name);
        byte* code = stackalloc byte[8192]; new Span<byte>(code, 8192).Clear();
        HostCheck.True(X64OwnedClockAccess.Emit(code + helpers[0].Start, 8192 - helpers[0].Start) &&
            X64PciConfigurationRead.Emit(code + helpers[1].Start, 8192 - helpers[1].Start) &&
            X64PciBarAccess.Emit(code + helpers[2].Start, 8192 - helpers[2].Start) &&
            X64VirtqueueMemoryFence.Emit(code + helpers[3].Start, 8192 - helpers[3].Start) &&
            ThreadContextSwitch.EmitObserver(code + helpers[4].Start, helpers[4].Bytes) &&
            ThreadContextSwitch.EmitFxSnapshot(code + helpers[5].Start, helpers[5].Bytes) &&
            ThreadContextSwitch.EmitOwnedSegments(code + helpers[6].Start, helpers[6].Bytes) &&
            X64PhysicalMappingPublication.EmitGuardPublication(code + helpers[7].Start, helpers[7].Bytes));
        HostCheck.True(X64OwnedClockAccess.Validate(code + helpers[0].Start, helpers[0].Bytes) &&
            X64PciConfigurationRead.Validate(code + helpers[1].Start, helpers[1].Bytes) &&
            X64PciBarAccess.Validate(code + helpers[2].Start, helpers[2].Bytes) &&
            X64VirtqueueMemoryFence.Validate(code + helpers[3].Start, helpers[3].Bytes) &&
            X64PhysicalMappingPublication.ValidateGuardPublication(code + helpers[7].Start, helpers[7].Bytes));
        byte* scratch = stackalloc byte[(int)ThreadContextSwitch.ObserverBytes];
        HostCheck.True(ThreadContextSwitch.EmitObserver(scratch, ThreadContextSwitch.ObserverBytes));
        HostCheck.True(new ReadOnlySpan<byte>(scratch, (int)helpers[4].Bytes).SequenceEqual(new ReadOnlySpan<byte>(code + helpers[4].Start, (int)helpers[4].Bytes)));
        HostCheck.True(ThreadContextSwitch.EmitFxSnapshot(scratch, ThreadContextSwitch.ObserverBytes));
        HostCheck.True(new ReadOnlySpan<byte>(scratch, (int)helpers[5].Bytes).SequenceEqual(new ReadOnlySpan<byte>(code + helpers[5].Start, (int)helpers[5].Bytes)));
        HostCheck.True(ThreadContextSwitch.EmitOwnedSegments(scratch, ThreadContextSwitch.ObserverBytes));
        HostCheck.True(new ReadOnlySpan<byte>(scratch, (int)helpers[6].Bytes).SequenceEqual(new ReadOnlySpan<byte>(code + helpers[6].Start, (int)helpers[6].Bytes)));
    }
}
