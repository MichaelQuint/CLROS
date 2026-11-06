using System.Xml.Linq;
using System.Text.Json.Nodes;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class M04ConstructorFailureEfiIntegrationTests
{
    private const string Flag = "EnableM04ConstructorFailureProbe", Symbol = "M04_CONSTRUCTOR_FAILURE_PROBE";
    private static readonly string[] Flags = [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04ConstructorFailure"];
    private static readonly string[] Competitors = ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch",
        "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch",
        "M04UnhandledException", "M04FrameDispatch", "M04FilteredCatch"];

    [HostTest("m04.021.ctor-failure.efi exact required profile and missing prerequisites", HostLane.Fast, 0)]
    private static void Prerequisites()
    {
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(Arguments(Flags), build).Profile();
            var expected = Flags.Select(name => "Enable" + name + "Probe").ToHashSet(StringComparer.Ordinal);
            HostCheck.True(profile.BootPolicy == "Diagnostic" && expected.SetEquals(profile.Flags.Where(p => p.Value).Select(p => p.Key)),
                "only the exact GC chain and constructor-failure leaf are enabled");
            foreach (string missing in Flags[..^1])
            {
                string reason = missing == "M04GcStaticRootMaterialization" ? "m04_ctor_failure_requires_gc_profile" : "m04_gc_static_profile_invalid";
                string[] remaining = Arguments(Flags.Where(name => name != missing));
                Reject(remaining, build, reason);
                Reject([.. remaining, "--Enable" + missing + "Probe", "false"], build, reason);
            }
            var disabled = EfiOptions.Parse(["--" + Flag, "false"], build).Profile();
            HostCheck.True(disabled.BootPolicy == "VerifiedExit" && !disabled.IsEnabled("M04ConstructorFailure"), "explicit false remains disabled");
            Reject(["--" + Flag + "=invalid"], build, "argument_boolean_invalid");
        }
    }

    [HostTest("m04.021.ctor-failure.efi all competing runtime profiles reject", HostLane.Fast, 1)]
    private static void Conflicts()
    {
        foreach (bool build in new[] { false, true })
        {
            foreach (string collision in Competitors)
            {
                string[] prerequisites = collision switch
                {
                    "M04InterfaceDispatch" => ["M04ClassConstructorExecution"],
                    "M04X64UnwindMetadata" => ["M04TypeManagerModuleRegistration"],
                    _ => []
                };
                Reject(Arguments([.. Flags, .. prerequisites, collision]), build,
                    collision == "M04FilteredCatch" ? "m04_filter_profile_conflict" : "m04_ctor_failure_profile_conflict");
            }
            Reject(["--boot-policy", "VerifiedExit", .. Arguments(Flags)], build, "clros_boot_policy_probe_conflict");
        }
    }

    [HostTest("m04.021.ctor-failure.efi default off destructive catalog and compile integration", HostLane.Fast, 2)]
    private static void Defaults()
    {
        var project = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" &&
            defaults[0].Attribute("Condition")?.Value == "'$(EnableM04ConstructorFailureProbe)' == ''", "explicit false project default");
        HostCheck.True(!Probes.EnableM04ConstructorFailureProbe && !BootSequence.IsConstructorFailureEnabled(), "host default off");
        var probe = Probes.GetM04ConstructorFailureProbe();
        HostCheck.True(probe.Name == Flag && probe.Group == "runtime.exception" && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive,
            "destructive diagnostic declaration");
        HostCheck.True(Probes.TryGetProbe(43, out var catalog) && catalog.Name == Flag && !catalog.EnabledByDefault &&
            catalog.Risk == ProbeRisk.Destructive, "catalog index 43");
        HostCheck.True(EfiContracts.DiagnosticProbes.Contains(Flag) && EfiContracts.ProbeSymbols.TryGetValue(Flag, out string? symbol) &&
            symbol == Symbol, "shared admission flag and symbol");
        var defines = project.Descendants("DefineConstants").Where(node => node.Value.Split(';').Contains(Symbol)).ToArray();
        HostCheck.True(defines.Length == 1 && defines[0].Parent?.Attribute("Condition")?.Value == "'$(EnableM04ConstructorFailureProbe)' == 'true'",
            "symbol appears only in the explicitly enabled property group");
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(Arguments(Flags), true).Profile());
        var sources = source.TypeSources("System.TypeInitializationException")
            .Append(source.Document(source.Method("OS.Runtime.RhpNewFastAllocation", "RunConstructorFailure")))
            .Append(source.Document(source.Method("OS.Kernel.Runtime.CompilerClassConstructorExecution", "BeginLazy")))
            .Append(source.Document(source.Method("OS.Kernel.Runtime.CompilerClassConstructorExecution", "FinishLazy")))
            .DistinctBy(document => document.Path);
        foreach (var document in sources)
        {
            string include = Path.GetRelativePath(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src", "OS"), document.Path).Replace('/', '\\');
            HostCheck.True(EfiContracts.ReviewedSourceIncludes.Contains(include)
                && HostEfiSourceAdmission.Accepts(include, document.Text), "reviewed compiler input " + include);
        }
        var admission = EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(admission.Succeeded, admission.Reason + " " + admission.Detail);
    }

    [HostTest("m04.021.ctor-failure.efi native geometry and rehashed rejection controls", HostLane.Native, 3)]
    private static void NativeAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot(), name = "ctor-failure-admission-" + Guid.NewGuid().ToString("N"),
            artifact = Path.Combine(root, "artifacts/efi", name + ".EFI"), log = Path.Combine(root, "artifacts/ctor-failure-native", name + ".log");
        using StringWriter output = new();
        int code = EfiBuild.Run(["--EfiOutputPath", artifact, "--LogPath", log, .. Arguments(Flags)], output);
        HostCheck.True(code == 0,
            output.ToString().Length > 4000 ? output.ToString()[^4000..] : output.ToString());
        byte[] original = File.ReadAllBytes(artifact);
        string evidencePath = artifact + ".link.json", originalEvidence = File.ReadAllText(evidencePath), map = File.ReadAllText(artifact + ".link.map");
        EfiPeImage image = new(original);
        image.VerifyMapAndRuntime(map, Path.GetFileName(JsonNode.Parse(originalEvidence)!["serializerLinkedObjectPath"]!.GetValue<string>()));
        ulong Address(string symbol)
        {
            var rows = EfiPeImage.Symbols(map, symbol);
            HostCheck.True(rows.Count == 1, "fresh unique symbol " + symbol);
            return EfiPeImage.Hex(rows[0].Groups[3].Value, "fixture address") - image.ImageBase;
        }
        int Offset(string symbol, int bytes) => image.Resolve(Address(symbol), (ulong)bytes, "fixture backing", "fixture ambiguity").Offset;
        void Verify(string? expected)
        {
            string? failure = null;
            try
            {
                EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact", artifact, .. Arguments(Flags)], false));
            }
            catch (ToolFailure rejected)
            {
                failure = rejected.Reason;
            }
            HostCheck.True(failure == expected, "expected=" + expected + " actual=" + failure);
        }
        Verify(null);
        var controls = new List<(int At, string Reason)>();
        controls.Add((Offset("?__NONGCSTATICS@OS_OS_Runtime_ConstructorFailureFixture@@", 8) - 8, "constructor_failure_context_initializer_mismatch"));
        foreach (string symbol in new[] {"OS_OS_Runtime_ConstructorFailureFixture___cctor","OS_OS_Runtime_ConstructorFailureFixture__Read",
            "__GetNonGCStaticBase_OS_OS_Runtime_ConstructorFailureFixture",
            "OS_System_Runtime_CompilerServices_ClassConstructorRunner__CheckStaticClassConstructionReturnNonGCStaticBase",
            "OS_System_Runtime_CompilerServices_ClassConstructorRunner__EnsureClassConstructorRun",
            "OS_OS_Runtime_RhpNewFastAllocation__EnsureCompilerClassConstructor","OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke",
            "OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter","OS_OS_Runtime_RhpNewFastAllocation__ConstructorFailureWrap"})
            controls.Add((Offset(symbol, 1), "constructor_failure_method_bytes_mismatch"));
        controls.Add((Offset("OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke", 40) + 40, "constructor_failure_method_target_mismatch"));
        controls.Add((Offset("OS_OS_Runtime_ConstructorFailureFixture__Read", 35) + 34, "constructor_failure_method_bytes_mismatch"));
        foreach (string symbol in new[] { "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureInvoke", "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__CtorFailureOuter" })
            controls.Add((Offset(symbol, 8) + 4, "constructor_failure_clause_table_mismatch"));
        controls.Add((Offset("_unwind0OS_OS_Runtime_ConstructorFailureFixture___cctor", 11) + 10, "constructor_failure_unwind_mismatch"));
        controls.Add((Offset("??_7OS_System_TypeInitializationException@@6B@", 24) + 4, "constructor_failure_wrapper_prefix_mismatch"));
        foreach (var (at, reason) in controls)
        {
            try
            {
                byte[] changed = [.. original];
                changed[at] ^= 1;
                File.WriteAllBytes(artifact, changed);
                JsonObject evidence = JsonNode.Parse(originalEvidence)!.AsObject();
                evidence["artifactSha256"] = ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath, evidence.ToJsonString());
                Verify(reason);
            }
            finally
            {
                File.WriteAllBytes(artifact, original);
                File.WriteAllText(evidencePath, originalEvidence);
            }
        }
        string sidecar = artifact + ".ctor-failure.obj";
        byte[] bridge = File.ReadAllBytes(sidecar);
        try
        {
            byte[] changed = [.. bridge];
            changed[0] ^= 1;
            File.WriteAllBytes(sidecar, changed);
            Verify("constructor_failure_object_identity_mismatch");
        }
        finally
        {
            File.WriteAllBytes(sidecar, bridge);
        }
        Verify(null);
        File.WriteAllText(log + ".controls", controls.Count + " rehashed PE controls and one object mutation rejected; originals restored and native geometry admitted again. No actual transfer/guest acceptance.\n");
    }

    private static string[] Arguments(IEnumerable<string> names) => names.Select(name => "--Enable" + name + "Probe").ToArray();
    private static void Reject(string[] arguments, bool build, string expected)
    {
        string? reason = null;
        try
        {
            _ = EfiOptions.Parse(arguments, build).Profile();
        }
        catch (ToolFailure failure)
        {
            reason = failure.Reason;
        }
        HostCheck.True(reason == expected, "expected=" + expected + " actual=" + reason);
    }
}
